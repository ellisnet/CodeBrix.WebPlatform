#!/usr/bin/env bash
# ==============================================================================================
# container-build.sh - builds AND verifies dx and wasm-bindgen INSIDE the build container
# ==============================================================================================
#
# Do not run this on your workstation. build.sh runs it inside the derived image
# (codebrix-dx-build-x86_64), WITH THE NETWORK DISABLED:
#
#     podman run --network none -v <tools/dx-native-tools>:/work -v <tool sources>:/src:ro ...
#
# That is not decoration. It is the mechanical proof of the rule this folder exists for: if any
# step ever tried to fetch a crate, it would fail here instead of quietly working on the machine
# that happened to have a network. Step 0 below prints the proof into the build log.
#
# It expects:
#   /work            tools/dx-native-tools/, read-write (only output/ is written)
#   /src             TOOL_SOURCES_DIR (pins.env), READ-ONLY: dioxus-cli/, codebrix-libgit2-sys/,
#                    wasm-bindgen-cli/, vendor/, .cargo/config.toml. cargo's target folder is in
#                    the container's /tmp, so nothing is ever written to the source trees.
#   /libgit2         output/libgit2 (fetch-libgit2.sh), READ-ONLY: /libgit2/<rid>/ holds the
#                    prebuilt libgit2 from the CodeBrix.LibGit2 package, under its link name and
#                    its SONAME - what dx links, and what the gate runs it with
#   $TARGET_RID      linux-x64 | linux-arm64
#   $TARGET_TRIPLE   x86_64-unknown-linux-gnu | aarch64-unknown-linux-gnu
#   $GLIBC_MAX       the glibc floor allowed for that RID (build.sh takes it from pins.env)
#   $DERIVED_IMAGE, $BASE_IMAGE_REF, $BUILD_HOST   recorded only
# Every other pin is read from /work/linux/pins.env, the same file build.sh sources.
# ==============================================================================================

set -euo pipefail

trap 'rc=$?; echo "ERROR: container-build.sh failed (exit $rc) at line $LINENO: $BASH_COMMAND" >&2' ERR

# first_line: the first line of stdin WITHOUT the `head -1` pitfall (SIGPIPE under pipefail).
first_line() { sed -n '1p'; }

WORK=/work
SRC=/src
# shellcheck source=pins.env
. "$WORK/linux/pins.env"

TARGET_ROOT=/tmp/codebrix-dx-target
OUT="$WORK/output/$TARGET_RID"
STARTED_AT="$(date -u '+%Y-%m-%d %H:%M:%S UTC')"
START_SECONDS="$SECONDS"
CONTAINER_OS="$(grep PRETTY_NAME /etc/os-release 2>/dev/null | first_line | cut -d= -f2- | tr -d '"')"

echo "=============================================================================="
echo " dx $DX_CRATE_VERSION + wasm-bindgen $WASM_BINDGEN_VERSION for $TARGET_RID ($TARGET_TRIPLE)"
echo "=============================================================================="
echo " started   : $STARTED_AT"
echo " container : $CONTAINER_OS"
echo " image     : ${DERIVED_IMAGE:-unknown}"
echo " base      : ${BASE_IMAGE_REF:-unknown}"
echo " arch      : $(uname -m)"
echo

# ----------------------------------------------------------------------------------------------
# 0. Prove there is no network.
# ----------------------------------------------------------------------------------------------
IFACES="$(ls /sys/class/net 2>/dev/null | tr '\n' ' ' | sed 's/ *$//')"
echo "--- network ---"
echo "  interfaces: ${IFACES:-none}"
if [ "$IFACES" = "lo" ] || [ -z "$IFACES" ]; then
    echo "  [ok] no external network interface - this build cannot reach outside the repository"
else
    echo "  [warn] this container HAS a network interface ($IFACES). build.sh normally passes"
    echo "         --network none; something overrode it."
fi
echo

# ----------------------------------------------------------------------------------------------
# 1. Toolchain. Installed by Containerfile.x86_64 when the image was built; nothing is installed
#    now. The Rust release must be exactly the pinned one.
# ----------------------------------------------------------------------------------------------
echo "--- toolchain ---"
for t in cc clang llvm-ar llvm-strip cargo rustc readelf strings file gzip sha256sum; do
    command -v "$t" > /dev/null 2>&1 || { echo "ERROR: $t is missing from this image. Rebuild it: FORCE_IMAGE_REBUILD=1 ./build.sh" >&2; exit 1; }
done
RUSTC_VERSION="$(rustc --version)"
CARGO_VERSION="$(cargo --version)"
case "$RUSTC_VERSION" in
    "rustc $RUST_TOOLCHAIN "*) echo "  [ok] $RUSTC_VERSION (pinned $RUST_TOOLCHAIN)" ;;
    *) echo "ERROR: the image has '$RUSTC_VERSION', pins.env says $RUST_TOOLCHAIN. FORCE_IMAGE_REBUILD=1 ./build.sh" >&2; exit 1 ;;
esac
rustup target list --installed | grep -x "$TARGET_TRIPLE" > /dev/null \
    || { echo "ERROR: the Rust target $TARGET_TRIPLE is not installed in the image." >&2; exit 1; }
# The OpenSSL headers (read by openssl-sys's build script; nothing of OpenSSL is linked). An image
# built before they were added to Containerfile.x86_64 lacks them.
case "$TARGET_TRIPLE" in
    aarch64-*) OPENSSL_INCLUDE=/opt/sysroot/aarch64/usr/include; OPENSSL_LIB=/opt/sysroot/aarch64/usr/lib64 ;;
    *)         OPENSSL_INCLUDE=/usr/include;                    OPENSSL_LIB=/usr/lib64 ;;
esac
[ -f "$OPENSSL_INCLUDE/openssl/opensslv.h" ] || {
    echo "ERROR: the image has no OpenSSL headers in $OPENSSL_INCLUDE (Containerfile.x86_64 installs them" >&2
    echo "       since dx's git features came back). Rebuild it: FORCE_IMAGE_REBUILD=1 ./build.sh" >&2; exit 1; }
echo "  [ok] OpenSSL headers for $TARGET_TRIPLE in $OPENSSL_INCLUDE (read by openssl-sys's build script only)"
echo "  $CARGO_VERSION"
TOOLCHAIN_RECORD="$(cat /usr/local/share/codebrix-dx-build/toolchain.txt 2>/dev/null || echo 'toolchain.txt missing')"
printf '%s\n' "$TOOLCHAIN_RECORD" | sed 's/^/  /'
echo

# ----------------------------------------------------------------------------------------------
# 2. Source. Read where it is (read-only mount); checked against the pins.
# ----------------------------------------------------------------------------------------------
echo "--- source (/src = $TOOL_SOURCES_DIR, read-only) ---"
manifest_version() { sed -n 's/^version = "\(.*\)"$/\1/p' "$1" | first_line; }
DX_SRC_VERSION="$(manifest_version "$SRC/$DX_DIR/Cargo.toml")"
WB_SRC_VERSION="$(manifest_version "$SRC/$WASM_BINDGEN_DIR/Cargo.toml")"
DX_VCS_SHA="$(sed -n 's/.*"sha1": *"\([0-9a-f]*\)".*/\1/p' "$SRC/$DX_DIR/.cargo_vcs_info.json" | first_line)"
[ "$DX_SRC_VERSION" = "$DX_CRATE_VERSION" ] || { echo "ERROR: $DX_DIR/Cargo.toml says $DX_SRC_VERSION, pins.env $DX_CRATE_VERSION" >&2; exit 1; }
[ "$WB_SRC_VERSION" = "$WASM_BINDGEN_VERSION" ] || { echo "ERROR: $WASM_BINDGEN_DIR/Cargo.toml says $WB_SRC_VERSION, pins.env $WASM_BINDGEN_VERSION" >&2; exit 1; }
[ "$DX_VCS_SHA" = "$DX_GIT_SHA" ] || { echo "ERROR: $DX_DIR/.cargo_vcs_info.json names commit $DX_VCS_SHA, pins.env DX_GIT_SHA $DX_GIT_SHA" >&2; exit 1; }
if touch "$SRC/.codebrix-write-probe" 2>/dev/null; then
    rm -f "$SRC/.codebrix-write-probe"
    echo "ERROR: /src is WRITABLE. build.sh mounts the tool sources read-only; something overrode it." >&2; exit 1
fi
VENDOR_CRATES="$(find "$SRC/vendor" -mindepth 1 -maxdepth 1 -type d | wc -l)"
DX_LOCK_SHA="$(sha256sum "$SRC/$DX_DIR/Cargo.lock" | cut -d' ' -f1)"
WB_LOCK_SHA="$(sha256sum "$SRC/$WASM_BINDGEN_DIR/Cargo.lock" | cut -d' ' -f1)"
echo "  [ok] dioxus-cli $DX_SRC_VERSION (upstream commit $DX_VCS_SHA), Cargo.lock sha256 $DX_LOCK_SHA"
echo "  [ok] wasm-bindgen-cli $WB_SRC_VERSION, Cargo.lock sha256 $WB_LOCK_SHA"
echo "  [ok] vendor/: $VENDOR_CRATES crates; .cargo/config.toml:"
sed 's/^/        /' "$SRC/.cargo/config.toml"
echo "  [ok] /src is read-only"
STANDIN_VERSION="$(manifest_version "$SRC/codebrix-libgit2-sys/Cargo.toml")"
STANDIN_LIBRS_SHA="$(sha256sum "$SRC/codebrix-libgit2-sys/lib.rs" | cut -d' ' -f1)"
echo "  [ok] codebrix-libgit2-sys: the libgit2-sys stand-in, version $STANDIN_VERSION, lib.rs sha256 $STANDIN_LIBRS_SHA"
echo

# ----------------------------------------------------------------------------------------------
# 2b. libgit2. The prebuilt library dx links (never built here), checked against the pins.
# ----------------------------------------------------------------------------------------------
echo "--- libgit2 (/libgit2/$TARGET_RID, read-only) ---"
LIBGIT2_DIR="/libgit2/$TARGET_RID"
LIBGIT2_FILE="$LIBGIT2_DIR/lib$LIBGIT2_LINK_NAME.so"
case "$TARGET_RID" in
    linux-x64)   LIBGIT2_WANT="$LIBGIT2_SHA256_LINUX_X64" ;;
    linux-arm64) LIBGIT2_WANT="$LIBGIT2_SHA256_LINUX_ARM64" ;;
    *)           LIBGIT2_WANT="?" ;;
esac
[ -f "$LIBGIT2_FILE" ] && [ -e "$LIBGIT2_DIR/$LIBGIT2_SONAME_LINUX" ] || {
    echo "ERROR: $LIBGIT2_FILE (and $LIBGIT2_SONAME_LINUX beside it) is missing; build.sh runs fetch-libgit2.sh first." >&2; exit 1; }
LIBGIT2_GOT="$(sha256sum "$LIBGIT2_FILE" | cut -d' ' -f1)"
[ "$LIBGIT2_GOT" = "$LIBGIT2_WANT" ] || {
    echo "ERROR: $LIBGIT2_FILE has sha256 $LIBGIT2_GOT; pins.env says $LIBGIT2_WANT." >&2; exit 1; }
LIBGIT2_SONAME_GOT="$(readelf -d "$LIBGIT2_FILE" | sed -n 's/.*(SONAME).*\[\(.*\)\].*/\1/p')"
[ "$LIBGIT2_SONAME_GOT" = "$LIBGIT2_SONAME_LINUX" ] || {
    echo "ERROR: $LIBGIT2_FILE has SONAME '$LIBGIT2_SONAME_GOT'; pins.env says $LIBGIT2_SONAME_LINUX." >&2; exit 1; }
echo "  [ok] libgit2 $LIBGIT2_VERSION from $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION: lib$LIBGIT2_LINK_NAME.so sha256 $LIBGIT2_GOT, SONAME $LIBGIT2_SONAME_GOT"
echo

# ----------------------------------------------------------------------------------------------
# 3. Build. The tools' build command (cargo build --release --locked --offline, dx with
#    DX_FEATURES), plus:
#      --target <triple>             so RUSTFLAGS reach only the shipped code, never build
#                                    scripts or procedural macros
#      RUSTFLAGS --remap-path-prefix the source and target paths become relative names, so no
#                                    build path lands in the binary (panic locations embed file
#                                    paths); -Wl,--build-id=sha1 gives the file an identity
#      CFLAGS -ffile-prefix-map      the same for the C code compiled in (ring, zstd)
#      DIOXUS_CLI_GIT_SHA            the commit `dx --version` reports (pins.env, DX_GIT_SHA)
#      for aarch64: the cross linker / C compiler / archiver of Containerfile.x86_64
# ----------------------------------------------------------------------------------------------
TRIPLE_UPPER="$(echo "$TARGET_TRIPLE" | tr 'a-z-' 'A-Z_')"
TRIPLE_LOWER="$(echo "$TARGET_TRIPLE" | tr '-' '_')"
REMAP="--remap-path-prefix=$SRC=tool-sources --remap-path-prefix=$TARGET_ROOT=target --remap-path-prefix=/usr/local/cargo=cargo-home"
export RUSTFLAGS="$REMAP -C link-arg=-Wl,--build-id=sha1"
export "CFLAGS_${TRIPLE_LOWER}=-ffile-prefix-map=$SRC=tool-sources -ffile-prefix-map=$TARGET_ROOT=target"
export DIOXUS_CLI_GIT_SHA="$DX_GIT_SHA"
export CARGO_INCREMENTAL=0
# The libgit2-sys stand-in's build.rs: where the library to link is, and its link name.
export CODEBRIX_LIBGIT2_LIB_DIR="$LIBGIT2_DIR"
export CODEBRIX_LIBGIT2_LINK_NAME="$LIBGIT2_LINK_NAME"
# openssl-sys's build script (target-prefixed, so they apply to this target only): the headers
# it reads. It links nothing into dx - git2 never calls it - which the gate checks.
export "${TRIPLE_UPPER}_OPENSSL_INCLUDE_DIR=$OPENSSL_INCLUDE"
export "${TRIPLE_UPPER}_OPENSSL_LIB_DIR=$OPENSSL_LIB"
LINKER_NOTE="cc (the image's GCC 14, native)"
if [ "$TARGET_TRIPLE" = "aarch64-unknown-linux-gnu" ]; then
    export "CARGO_TARGET_${TRIPLE_UPPER}_LINKER=aarch64-linux-gnu-gcc"
    export "CC_${TRIPLE_LOWER}=aarch64-linux-gnu-gcc"
    export "CXX_${TRIPLE_LOWER}=aarch64-linux-gnu-g++"
    export "AR_${TRIPLE_LOWER}=llvm-ar"
    LINKER_NOTE="aarch64-linux-gnu-gcc = clang --target=aarch64-redhat-linux --sysroot=/opt/sysroot/aarch64 -fuse-ld=lld (cross)"
fi
echo "--- environment ---"
echo "  RUSTFLAGS=$RUSTFLAGS"
echo "  CFLAGS_${TRIPLE_LOWER}=$(printenv "CFLAGS_${TRIPLE_LOWER}")"
echo "  DIOXUS_CLI_GIT_SHA=$DIOXUS_CLI_GIT_SHA"
echo "  CODEBRIX_LIBGIT2_LIB_DIR=$CODEBRIX_LIBGIT2_LIB_DIR CODEBRIX_LIBGIT2_LINK_NAME=$CODEBRIX_LIBGIT2_LINK_NAME"
echo "  ${TRIPLE_UPPER}_OPENSSL_INCLUDE_DIR=$OPENSSL_INCLUDE ${TRIPLE_UPPER}_OPENSSL_LIB_DIR=$OPENSSL_LIB"
echo "  linker: $LINKER_NOTE"
echo

build_tool() {
    local dir="$1" features="$2" log="$TARGET_ROOT/$1.build.log" args started
    args="build --release --locked --offline --target $TARGET_TRIPLE"
    [ -n "$features" ] && args="$args --features $features"
    echo "--- cargo $args   (in $dir) ---"
    started="$SECONDS"
    mkdir -p "$TARGET_ROOT"
    # shellcheck disable=SC2086
    ( cd "$SRC/$dir" && CARGO_TARGET_DIR="$TARGET_ROOT/$dir" cargo $args ) > "$log" 2>&1 \
        || { echo "ERROR: cargo failed for $dir:" >&2; tail -60 "$log" >&2; exit 1; }
    tail -1 "$log" | sed 's/^ */  /'
    local warnings
    warnings="$(grep -c '^warning' "$log" || true)"
    echo "  ${dir}: $(( SECONDS - started ))s, $(grep -c '^ *Compiling ' "$log" || true) crates compiled, $warnings warning lines"
    eval "BUILD_SECONDS_${dir//-/_}=$(( SECONDS - started ))"
    eval "WARNINGS_${dir//-/_}=$warnings"
    # Evidence: every crate cargo compiles into the tool for this target (normal dependencies;
    # build scripts' and procedural macros' own dependencies are not linked in).
    # shellcheck disable=SC2086
    ( cd "$SRC/$dir" && CARGO_TARGET_DIR="$TARGET_ROOT/$dir" cargo tree --locked --offline --target "$TARGET_TRIPLE" \
        ${features:+--features $features} -e normal --prefix none --format '{p} | {l}' ) 2>/dev/null \
        | sed 's/ (\*)//' | sort -u > "$TARGET_ROOT/compiled-in-$dir.txt"
}
build_tool "$DX_DIR" "$DX_FEATURES"
build_tool "$WASM_BINDGEN_DIR" "$WASM_BINDGEN_FEATURES"
echo

# ----------------------------------------------------------------------------------------------
# 4. Collect + strip + licence files.
# ----------------------------------------------------------------------------------------------
echo "--- collecting ---"
rm -rf "$OUT"
mkdir -p "$OUT"
declare -A SIZE_LINKED
for pair in "$DX_DIR:$DX_EXE" "$WASM_BINDGEN_DIR:$WASM_BINDGEN_EXE"; do
    dir="${pair%%:*}"; exe="${pair#*:}"
    built="$TARGET_ROOT/$dir/$TARGET_TRIPLE/release/$exe"
    [ -f "$built" ] || { echo "ERROR: cargo did not produce $built" >&2; exit 1; }
    SIZE_LINKED[$exe]="$(stat -c %s "$built")"
    cp "$built" "$OUT/$exe"
    llvm-strip --strip-all "$OUT/$exe"
    chmod 0755 "$OUT/$exe"
    cp "$TARGET_ROOT/compiled-in-$dir.txt" "$OUT/compiled-in-$dir.txt"
    cp "$TARGET_ROOT/$dir.build.log" "$OUT/cargo-$dir.log"
    echo "  $exe: as linked ${SIZE_LINKED[$exe]} bytes -> stripped $(stat -c %s "$OUT/$exe") bytes"
done
LICENCES_MISSING=""
while IFS= read -r entry; do
    [ -n "$entry" ] || continue
    name="${entry%%:*}"; from="${entry#*:}"
    case "$from" in @src/*) path="$SRC/${from#@src/}" ;; *) path="$WORK/$from" ;; esac
    if [ -f "$path" ]; then cp "$path" "$OUT/$name"; echo "  $name <- $from"
    else echo "  $name: SOURCE MISSING ($from)"; LICENCES_MISSING="$LICENCES_MISSING $name"; fi
done <<< "$LICENCE_FILES"
echo

# ----------------------------------------------------------------------------------------------
# 5. THE GATE. A build that fails any check is reported as failed and this script exits
#    non-zero; output/<rid> is left for inspection but must not be adopted.
# ----------------------------------------------------------------------------------------------
echo "--- verifying ---"
GATE_LOG="$OUT/GATE.txt"
case "$TARGET_RID" in
    linux-x64)   WANT_MACHINE="Advanced Micro Devices X86-64" ;;
    linux-arm64) WANT_MACHINE="AArch64" ;;
    *)           WANT_MACHINE="?" ;;
esac
glibc_versions() { readelf -V -W "$1" 2>/dev/null | grep -oE 'GLIBC_[0-9]+\.[0-9]+(\.[0-9]+)?' | sed 's/GLIBC_//' | sort -V -u | tr '\n' ' ' | sed 's/ *$//'; }
needed_of() { readelf -d "$1" | sed -n 's/.*(NEEDED).*\[\(.*\)\].*/\1/p' | tr '\n' ' ' | sed 's/ *$//'; }
{
gate_one() {
    local exe="$1" path="$OUT/$1" f
    echo "  === $exe ==="
    local class machine type
    class="$(readelf -h "$path" | awk -F: '/Class:/{gsub(/^ +/,"",$2); print $2}')"
    machine="$(readelf -h "$path" | awk -F: '/Machine:/{gsub(/^ +/,"",$2); print $2}')"
    type="$(readelf -h "$path" | awk -F: '/Type:/{gsub(/^ +/,"",$2); print $2}')"
    if [ "$class" = "ELF64" ] && [ "$machine" = "$WANT_MACHINE" ]; then
        echo "  [ok] ELF class/machine: $class / $machine, type $type ($(file -b "$path" | cut -d, -f1-2))"
    else echo "  [FAIL] ELF class/machine: got '$class' / '$machine', expected ELF64 / '$WANT_MACHINE'"; fi

    local leftover
    leftover="$(readelf -S -W "$path" | awk '/^ *\[ *[0-9]+\]/{sub(/^ *\[ *[0-9]+\] */,""); print $1}' | grep -E '^\.(symtab|strtab|debug_)' | tr '\n' ' ' || true)"
    if [ -z "$leftover" ]; then echo "  [ok] stripped: no .symtab / .strtab / .debug_* sections"
    else echo "  [FAIL] not stripped: $leftover"; fi

    if readelf -d "$path" | grep -E '\((RPATH|RUNPATH)\)' > /dev/null; then echo "  [FAIL] RPATH/RUNPATH present"
    else echo "  [ok] no RPATH / RUNPATH"; fi
    if readelf -d "$path" | grep -q TEXTREL; then echo "  [FAIL] TEXTREL present"; else echo "  [ok] no TEXTREL"; fi

    # dx must need libgit2 under its SONAME (and nothing else new); wasm-bindgen must not.
    local deps unexpected="" forbidden allowed="$ALLOWED_DEPS" extra="" run_env=""
    if [ "$exe" = "$DX_EXE" ]; then allowed="$ALLOWED_DEPS $LIBGIT2_SONAME_LINUX"; extra=" + $LIBGIT2_SONAME_LINUX"; run_env="LD_LIBRARY_PATH=$LIBGIT2_DIR"; fi
    deps="$(needed_of "$path")"
    for f in $deps; do case " $allowed " in *" $f "*) ;; *) unexpected="$unexpected $f" ;; esac; done
    forbidden="$(printf '%s\n' $deps | grep -vxF "$LIBGIT2_SONAME_LINUX" | grep -E "$FORBIDDEN_DEP_PATTERN" | tr '\n' ' ' || true)"
    if [ "$exe" = "$WASM_BINDGEN_EXE" ] && printf '%s\n' $deps | grep -qxF "$LIBGIT2_SONAME_LINUX"; then forbidden="$forbidden $LIBGIT2_SONAME_LINUX"; fi
    [ -z "$forbidden" ] || echo "  [FAIL] FORBIDDEN dependencies: $forbidden"
    if [ -n "$unexpected" ]; then echo "  [FAIL] unexpected dynamic dependencies:$unexpected (allowed: $allowed)"
    else echo "  [ok] NEEDED, in file order: $deps (glibc + libgcc_s$extra only)"; fi
    if [ "$exe" = "$DX_EXE" ]; then
        if printf '%s\n' $deps | grep -qxF "$LIBGIT2_SONAME_LINUX"; then echo "  [ok] dx NEEDS libgit2 as $LIBGIT2_SONAME_LINUX (linked through the stand-in, from $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION)"
        else echo "  [FAIL] dx does not NEED $LIBGIT2_SONAME_LINUX: the libgit2-sys stand-in was not linked as intended"; fi
    fi
    local ossl
    ossl="$(readelf --dyn-syms -W "$path" | awk '$7 == "UND" { sub(/@.*/, "", $8); print $8 }' | grep -E "$OPENSSL_SYMBOL_PATTERN" | sort -u | tr '\n' ' ' || true)"
    if [ -z "$ossl" ]; then echo "  [ok] no OpenSSL symbol imported (nothing of OpenSSL is linked; openssl-sys is a dependency git2 never calls)"
    else echo "  [FAIL] OpenSSL symbols imported: $ossl"; fi
    if [ "$TARGET_RID" = "linux-x64" ]; then
        if env $run_env ldd "$path" 2>&1 | grep -q 'not found'; then echo "  [FAIL] ldd: unresolved libraries"
        else echo "  [ok] ldd: every NEEDED library resolves in the image${run_env:+ ($run_env)}"; fi
    fi

    local versions floor
    versions="$(glibc_versions "$path")"
    floor="$(printf '%s\n' $versions | tail -1)"
    if [ -z "$floor" ]; then echo "  [FAIL] glibc floor: no GLIBC_ version found (unexpected for a dynamic glibc program)"
    elif printf '%s\n%s\n' "$floor" "$GLIBC_MAX" | sort -V -C; then
        echo "  [ok] glibc floor: $floor (allowed <= $GLIBC_MAX); versions referenced: $versions"
    else echo "  [FAIL] glibc floor $floor is NEWER than the allowed $GLIBC_MAX"; fi
    local gccver
    gccver="$(readelf -V -W "$path" | grep -oE '(GCC|GLIBCXX|CXXABI)_[0-9.]+' | sort -V -u | tr '\n' ' ' || true)"
    echo "  [info] libgcc_s / C++ runtime symbol versions referenced: ${gccver:-none}"

    local hits
    hits="$(strings -a "$path" | grep -E -e "$SRC/($DX_DIR|$WASM_BINDGEN_DIR|vendor)" -e "$TARGET_ROOT" -e '(^|[^a-zA-Z0-9_.])/work/' -e '/home/' -e '/usr/local/(cargo|rustup)' -e '/root/' | sort -u || true)"
    if [ -z "$hits" ]; then echo "  [ok] no build paths (/src/..., $TARGET_ROOT, /work, /home, /root, /usr/local/cargo|rustup) in the binary"
    else echo "  [FAIL] build paths found in the binary: $(printf '%s\n' "$hits" | wc -l) strings, first: $(printf '%s\n' "$hits" | first_line)"; fi
    local rustc_paths cc_paths
    rustc_paths="$(strings -a "$path" | grep -c '/rustc/[0-9a-f]\{40\}/' || true)"
    cc_paths="$(strings -a "$path" | grep -E '^/(builddir|opt/rh|usr/include|opt/sysroot)/' | sort -u | tr '\n' ' ' || true)"
    echo "  [info] /rustc/<commit>/ standard-library paths (rustc's own remapping, not ours): $rustc_paths strings"
    echo "  [info] toolchain/system paths: ${cc_paths:-none}"

    if [ "$TARGET_RID" = "linux-x64" ]; then
        local want got
        if [ "$exe" = "$DX_EXE" ]; then want="$DX_EXPECTED_VERSION"; else want="$WASM_BINDGEN_EXPECTED_VERSION"; fi
        got="$(env $run_env "$path" --version 2>&1 | first_line || true)"
        if [ "$got" = "$want" ]; then echo "  [ok] $exe --version (in the image, network off${run_env:+, $run_env}): $got"
        else echo "  [FAIL] $exe --version: expected '$want', got '$got'"; fi
    else
        echo "  [info] $exe --version: run by build.sh in the pinned aarch64 image under qemu (QEMU-RUN.txt)"
    fi
}
gate_one "$DX_EXE"
gate_one "$WASM_BINDGEN_EXE"
echo "  === libgit2 (linked, not shipped by this build) ==="
LIBGIT2_FLOOR="$(glibc_versions "$LIBGIT2_FILE" | tr ' ' '\n' | tail -1)"
if printf '%s\n%s\n' "$LIBGIT2_FLOOR" "$GLIBC_MAX" | sort -V -C; then
    echo "  [ok] libgit2 (lib$LIBGIT2_LINK_NAME.so, sha256 $LIBGIT2_GOT): glibc floor $LIBGIT2_FLOOR (allowed <= $GLIBC_MAX); NEEDED $(needed_of "$LIBGIT2_FILE")"
else echo "  [FAIL] libgit2's glibc floor $LIBGIT2_FLOOR is newer than the allowed $GLIBC_MAX"; fi
echo "  === files beside them ==="
if [ -z "$LICENCES_MISSING" ]; then echo "  [ok] every file of LICENCE_FILES is present: $(printf '%s\n' "$LICENCE_FILES" | cut -d: -f1 | tr '\n' ' ')"
else echo "  [FAIL] licence / notices files missing:$LICENCES_MISSING"; fi
for t in "$DX_DIR" "$WASM_BINDGEN_DIR"; do
    echo "  [info] compiled into $t for $TARGET_TRIPLE: $(grep -c . "$OUT/compiled-in-$t.txt") packages (compiled-in-$t.txt, the tool itself included)"
done
} 2>&1 | tee "$GATE_LOG"
if grep -q '^  \[FAIL\]' "$GATE_LOG"; then
    echo
    echo "VERIFICATION FAILED for $TARGET_RID. output/$TARGET_RID is left in place for inspection,"
    echo "but this build must not be adopted into the package."
    exit 1
fi
echo

# ----------------------------------------------------------------------------------------------
# 6. Record.
# ----------------------------------------------------------------------------------------------
ELAPSED=$(( SECONDS - START_SECONDS ))
record_exe() {
    local exe="$1" path="$OUT/$1" bid
    bid="$( { readelf -n "$path" 2>/dev/null || true; } | awk '/Build ID/{print $3}' | first_line)"
    cat <<EOF
File             : $exe
Size (as linked) : ${SIZE_LINKED[$exe]} bytes (not kept)
Size (stripped)  : $(stat -c %s "$path") bytes  <- the shipped file
Size (gzip -9 -n): $(gzip -9 -n -c "$path" | wc -c) bytes
SHA256           : $(sha256sum "$path" | cut -d' ' -f1)
Build-id (sha1)  : ${bid:-none}
ELF              : $(readelf -h "$path" | awk -F: '/Class:/{gsub(/^ +/,"",$2); print $2}'), $(readelf -h "$path" | awk -F: '/Machine:/{gsub(/^ +/,"",$2); print $2}'), $(readelf -h "$path" | awk -F: '/Type:/{gsub(/^ +/,"",$2); print $2}')
NEEDED           : $(needed_of "$path")
glibc floor      : $(printf '%s\n' $(glibc_versions "$path") | tail -1) (allowed <= $GLIBC_MAX)
GLIBC versions   : $(glibc_versions "$path")

EOF
}
NAMES="$DX_EXE $WASM_BINDGEN_EXE $(printf '%s\n' "$LICENCE_FILES" | cut -d: -f1 | tr '\n' ' ')"
# shellcheck disable=SC2086
( cd "$OUT" && sha256sum $NAMES > SHA256SUMS.txt )

cat > "$OUT/BUILD-INFO.txt" <<EOF
dx and wasm-bindgen - build information
==============================================================================
RID              : $TARGET_RID ($TARGET_TRIPLE)
Built            : $STARTED_AT
Build duration   : ${ELAPSED}s (wall clock inside the container; dx ${BUILD_SECONDS_dioxus_cli}s, wasm-bindgen ${BUILD_SECONDS_wasm_bindgen_cli}s)
Built by         : tools/dx-native-tools/linux/build.sh -> container-build.sh
Build host       : ${BUILD_HOST:-unknown}
Network          : DISABLED during this build (interfaces: ${IFACES:-none})

Build machine
------------------------------------------------------------------------------
Derived image    : ${DERIVED_IMAGE:-unknown}
Base image       : ${BASE_IMAGE_REF:-unknown}
Container OS     : $CONTAINER_OS
$(printf '%s\n' "$TOOLCHAIN_RECORD")
Linker           : $LINKER_NOTE

Source (in-repo, read-only; nothing fetched at build time)
------------------------------------------------------------------------------
Folder           : $TOOL_SOURCES_DIR (TOOL_SOURCES_DIR in pins.env)
dioxus-cli       : $DX_SRC_VERSION, upstream commit $DX_VCS_SHA, with the repository's
                   six patches (tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt);
                   Cargo.lock sha256 $DX_LOCK_SHA
libgit2-sys      : the stand-in codebrix-libgit2-sys $STANDIN_VERSION (declarations only, no C
                   code), lib.rs sha256 $STANDIN_LIBRS_SHA
wasm-bindgen-cli : $WB_SRC_VERSION, unpatched; Cargo.lock sha256 $WB_LOCK_SHA
Vendored crates  : $VENDOR_CRATES folders in vendor/

libgit2 (linked as a shared library; not built, not committed, not shipped by this build)
------------------------------------------------------------------------------
Package          : $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION (nupkg sha256 $LIBGIT2_NUPKG_SHA256)
Library          : runtimes/$TARGET_RID/native/lib$LIBGIT2_LINK_NAME.so, libgit2 $LIBGIT2_VERSION,
                   sha256 $LIBGIT2_GOT, SONAME $LIBGIT2_SONAME_GOT
OpenSSL headers  : $OPENSSL_INCLUDE (openssl-sys's build script reads them; nothing linked)

Configuration
------------------------------------------------------------------------------
dx               : cargo build --release --locked --offline --target $TARGET_TRIPLE --features $DX_FEATURES
                   with CODEBRIX_LIBGIT2_LIB_DIR=$CODEBRIX_LIBGIT2_LIB_DIR, CODEBRIX_LIBGIT2_LINK_NAME=$LIBGIT2_LINK_NAME
wasm-bindgen     : cargo build --release --locked --offline --target $TARGET_TRIPLE
RUSTFLAGS        : $RUSTFLAGS
CFLAGS           : $(printenv "CFLAGS_${TRIPLE_LOWER}")
DIOXUS_CLI_GIT_SHA: $DIOXUS_CLI_GIT_SHA
Stripping        : llvm-strip --strip-all
Warning lines    : dx ${WARNINGS_dioxus_cli}, wasm-bindgen ${WARNINGS_wasm_bindgen_cli} (cargo's "warning" lines; upstream code)

Result
------------------------------------------------------------------------------
$(record_exe "$DX_EXE")
$(record_exe "$WASM_BINDGEN_EXE")
Files beside them: $(printf '%s\n' "$LICENCE_FILES" | cut -d: -f1 | tr '\n' ' ')
Compiled in      : compiled-in-$DX_DIR.txt ($(grep -c . "$OUT/compiled-in-$DX_DIR.txt") packages),
                   compiled-in-$WASM_BINDGEN_DIR.txt ($(grep -c . "$OUT/compiled-in-$WASM_BINDGEN_DIR.txt") packages)

Gate (GATE.txt)
------------------------------------------------------------------------------
$(cat "$GATE_LOG")
EOF

echo "--- done ---"
echo "  $OUT"
for exe in "$DX_EXE" "$WASM_BINDGEN_EXE"; do
    echo "  $exe: $(stat -c %s "$OUT/$exe") bytes, sha256 $(sha256sum "$OUT/$exe" | cut -d' ' -f1)"
done
echo "  ${ELAPSED}s"
