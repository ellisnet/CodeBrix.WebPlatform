#!/usr/bin/env bash
# ==============================================================================================
# build.sh - build dx and wasm-bindgen for osx-arm64 and osx-x64, from this repository only
# ==============================================================================================
#
#   NEVER YET RUN. Written on Linux on 2026-11-18 (updated 2026-11-19 for libgit2, still unrun),
#   which has no Apple SDK. Expect to fix
#   something on the first real run; fix it IN THE SCRIPT, record it in ../BUILD-PROVENANCE.txt,
#   and rewrite README.txt's status block with what the run established.
#
#   ./build.sh               both slices (osx-arm64, then osx-x64)
#   ./build.sh arm64         osx-arm64 only
#   ./build.sh x64           osx-x64 only
#
# Options (environment variables):
#   BUILD_ROOT=<folder>      where cargo's target folders live (default: $TMPDIR/cbx-dx-build;
#                            never inside the repository or the tool sources)
#   WASM_OPT_DIR=<folder>    the wasm-opt the functional gate puts first on dx's PATH (default:
#                            the CodeBrix.WebPlatform.WasmShrink package's osx-<arch> copy in the
#                            local NuGet cache, FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION in pins.env);
#                            without one the functional gate is SKIPPED and the result must not be
#                            adopted
#   TAILWIND_DIR=<folder>    the tailwindcss the functional gate puts first on dx's PATH (default:
#                            the CodeBrix.WebPlatform.Styling.Tailwind4 package's osx-<arch> copy
#                            in the local NuGet cache, FUNCTIONAL_STYLING_PACKAGE_VERSION in
#                            pins.env)
#   SKIP_FUNCTIONAL_GATE=1   do not build the frontend with the new tools
#
# WHAT IT DOES
#   Reads ../linux/pins.env (the one source of truth), then for each slice builds both tools
#   exactly as the Linux build does - cargo +<RUST_TOOLCHAIN> build --release --locked --offline
#   --target <triple> (dx with DX_FEATURES) - from the sources where they are (TOOL_SOURCES_DIR),
#   with CARGO_TARGET_DIR under BUILD_ROOT, so the source folders are never written to; with
#   MACOSX_DEPLOYMENT_TARGET set EXPLICITLY from pins.env (11.0 arm64, 10.15 x64); dx LINKS the
#   prebuilt libgit2 of the published CodeBrix.LibGit2 package (LIBGIT2_* in pins.env), which
#   ../linux/fetch-libgit2.sh takes out of the package into ../output/libgit2/osx-<arch>/ (from
#   the local NuGet cache, else nuget.org - THE ONE STEP THAT MAY USE THE NETWORK); strips each
#   program and signs it again ad hoc; runs the gate (README.txt, THE GATE) and writes
#   ../output/osx-<arch>/. Both slices are built on ONE Apple Silicon Mac (Rust cross-compiles
#   between the two Apple targets with the same Xcode SDK).
#
# It installs nothing, and downloads nothing but the libgit2 package when it is not in the NuGet
# cache (--offline + the vendored crates: cargo has nothing to fetch; the gate checks the build
# log printed no "Downloading"/"Updating" line). Anything missing is named, with the command
# that installs it, and the script stops.
#
# THE BUILT dx NEEDS libgit2 TO START. Its load command names @rpath/libgit2-49e408b.1.9.dylib
# (the library's install name) and it has no LC_RPATH, so dyld finds the library only through
# DYLD_LIBRARY_PATH (searched by leaf name, libgit2-49e408b.1.9.dylib - fetch-libgit2.sh puts the
# file under that name too). The gate runs dx with it set, and once without it, to record that
# dx then does not start. UNVERIFIED until the first run on a Mac.
#
# Written for macOS's /bin/bash 3.2: no associative arrays, no mapfile.
# ==============================================================================================

set -euo pipefail
trap 'rc=$?; echo "ERROR: build.sh failed (exit $rc) at line $LINENO: $BASH_COMMAND" >&2' ERR

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$TOOLS_DIR/../.." && pwd)"
OUTPUT_DIR="$TOOLS_DIR/output"
# shellcheck source=../linux/pins.env
. "$TOOLS_DIR/linux/pins.env"
SOURCES="$REPO_ROOT/$TOOL_SOURCES_DIR"
BUILD_ROOT="${BUILD_ROOT:-${TMPDIR:-/tmp}/cbx-dx-build}"
BUILD_ROOT="${BUILD_ROOT%/}"
first_line() { sed -n '1p'; }

# ----------------------------------------------------------------------------------------------
# Prerequisites - checked, never installed
# ----------------------------------------------------------------------------------------------
[ "$(uname -s)" = "Darwin" ] || { echo "ERROR: this script runs on macOS only." >&2; exit 1; }
for t in xcrun otool lipo strip codesign shasum gzip perl unzip; do
    command -v "$t" > /dev/null 2>&1 || { echo "ERROR: $t is missing. Install the Xcode Command Line Tools:  xcode-select --install" >&2; exit 1; }
done
command -v rustup > /dev/null 2>&1 || {
    echo "ERROR: rustup is not installed. Install it yourself (https://rustup.rs), then:" >&2
    echo "  rustup toolchain install $RUST_TOOLCHAIN --profile minimal --target aarch64-apple-darwin --target x86_64-apple-darwin" >&2
    exit 1; }
rustup toolchain list | grep -q "^$RUST_TOOLCHAIN-" || {
    echo "ERROR: Rust $RUST_TOOLCHAIN is not installed:" >&2
    echo "  rustup toolchain install $RUST_TOOLCHAIN --profile minimal --target aarch64-apple-darwin --target x86_64-apple-darwin" >&2
    exit 1; }
RUSTC_VERSION="$(rustc "+$RUST_TOOLCHAIN" --version)"
CARGO_VERSION="$(cargo "+$RUST_TOOLCHAIN" --version)"
case "$RUSTC_VERSION" in "rustc $RUST_TOOLCHAIN "*) ;; *) echo "ERROR: rustc +$RUST_TOOLCHAIN reports '$RUSTC_VERSION'" >&2; exit 1 ;; esac
for f in "$DX_DIR/Cargo.toml" "$DX_DIR/Cargo.lock" "$DX_DIR/.cargo_vcs_info.json" "codebrix-libgit2-sys/Cargo.toml" "$WASM_BINDGEN_DIR/Cargo.toml" "$WASM_BINDGEN_DIR/Cargo.lock" ".cargo/config.toml"; do
    [ -f "$SOURCES/$f" ] || { echo "ERROR: $SOURCES/$f is missing (TOOL_SOURCES_DIR in pins.env). Nothing is downloaded." >&2; exit 1; }
done
grep -q "$DX_GIT_SHA" "$SOURCES/$DX_DIR/.cargo_vcs_info.json" || { echo "ERROR: $DX_DIR/.cargo_vcs_info.json does not name $DX_GIT_SHA (pins.env DX_GIT_SHA)" >&2; exit 1; }
case "$BUILD_ROOT/" in "$REPO_ROOT/"*) echo "ERROR: BUILD_ROOT ($BUILD_ROOT) must be outside the repository." >&2; exit 1 ;; esac
XCODE="$(xcode-select -p 2>/dev/null || echo unknown)"
SDK_VERSION="$(xcrun --sdk macosx --show-sdk-version 2>/dev/null || echo unknown)"
CLANG_VERSION="$(xcrun clang --version | first_line)"
host_arch="$(uname -m)"

slice_triple() { case "$1" in arm64) echo aarch64-apple-darwin;; x64) echo x86_64-apple-darwin;; esac; }
slice_lipo()   { case "$1" in arm64) echo arm64;; x64) echo x86_64;; esac; }
slice_minos()  { case "$1" in arm64) echo "$MACOS_DEPLOYMENT_TARGET_ARM64";; x64) echo "$MACOS_DEPLOYMENT_TARGET_X64";; esac; }
can_run() {
    [ "$1" = arm64 ] && [ "$host_arch" = arm64 ] && return 0
    [ "$1" = x64 ] && [ "$host_arch" = x86_64 ] && return 0
    [ "$1" = x64 ] && /usr/bin/arch -x86_64 /usr/bin/true 2>/dev/null && return 0   # Rosetta 2
    return 1
}

build_slice() {
    local slice="$1" rid triple minos out target started gate failed=0
    rid="osx-$slice"; triple="$(slice_triple "$slice")"; minos="$(slice_minos "$slice")"
    out="$OUTPUT_DIR/$rid"; target="$BUILD_ROOT/$rid"; started="$SECONDS"
    echo
    echo "=============================================================================="
    echo " BUILD $rid ($triple), MACOSX_DEPLOYMENT_TARGET=$minos"
    echo "=============================================================================="
    rustup target list --installed --toolchain "$RUST_TOOLCHAIN" | grep -qx "$triple" || {
        echo "ERROR: rustup target add $triple --toolchain $RUST_TOOLCHAIN" >&2; exit 1; }
    mkdir -p "$target"
    bash "$TOOLS_DIR/linux/fetch-libgit2.sh" "$rid"
    local lib="$OUTPUT_DIR/libgit2/$rid"
    export CODEBRIX_LIBGIT2_LIB_DIR="$lib"
    export CODEBRIX_LIBGIT2_LINK_NAME="$LIBGIT2_LINK_NAME"
    export MACOSX_DEPLOYMENT_TARGET="$minos"
    export RUSTFLAGS="--remap-path-prefix=$SOURCES=tool-sources --remap-path-prefix=$BUILD_ROOT=target --remap-path-prefix=$HOME=user-home"
    export "CFLAGS_$(echo "$triple" | tr '-' '_')=-ffile-prefix-map=$SOURCES=tool-sources -ffile-prefix-map=$BUILD_ROOT=target -mmacosx-version-min=$minos"
    export DIOXUS_CLI_GIT_SHA="$DX_GIT_SHA"
    export CARGO_INCREMENTAL=0
    local secs_dx secs_wb
    for pair in "$DX_DIR:$DX_FEATURES" "$WASM_BINDGEN_DIR:$WASM_BINDGEN_FEATURES"; do
        # Two statements: macOS's bash 3.2 expands a whole `local` line before assigning any of
        # it, so $dir would be unbound in log= (found on the first real run, 2026-11-22).
        local dir="${pair%%:*}" features="${pair#*:}" t0="$SECONDS"
        local log="$target/$dir.build.log"
        echo "--- cargo +$RUST_TOOLCHAIN build --release --locked --offline --target $triple ${features:+--features $features}   ($dir)"
        ( cd "$SOURCES/$dir" && CARGO_TARGET_DIR="$target/$dir" cargo "+$RUST_TOOLCHAIN" build --release --locked --offline --target "$triple" ${features:+--features "$features"} ) > "$log" 2>&1 \
            || { tail -60 "$log" >&2; echo "ERROR: cargo failed for $dir (log: $log)" >&2; exit 1; }
        if grep -Eq '^ *(Downloading|Downloaded|Updating) ' "$log"; then echo "ERROR: cargo reported a download for $dir - the build must fetch nothing" >&2; exit 1; fi
        [ "$dir" = "$DX_DIR" ] && secs_dx=$(( SECONDS - t0 )) || secs_wb=$(( SECONDS - t0 ))
        echo "  $(( SECONDS - t0 ))s"
    done

    rm -rf "$out"; mkdir -p "$out"
    for pair in "$DX_DIR:$DX_EXE" "$WASM_BINDGEN_DIR:$WASM_BINDGEN_EXE"; do
        local dir="${pair%%:*}" exe="${pair#*:}"
        cp "$target/$dir/$triple/release/$exe" "$out/$exe"
        strip "$out/$exe"
        codesign --force --sign - "$out/$exe"
        chmod 0755 "$out/$exe"
    done
    local names=""
    while IFS= read -r entry; do
        [ -n "$entry" ] || continue
        local name="${entry%%:*}" from="${entry#*:}" path
        case "$from" in @src/*) path="$SOURCES/${from#@src/}" ;; *) path="$TOOLS_DIR/$from" ;; esac
        [ -f "$path" ] || { echo "ERROR: licence / notices source missing: $from" >&2; exit 1; }
        cp "$path" "$out/$name"; names="$names $name"
    done <<< "$LICENCE_FILES"

    # ---- THE GATE ------------------------------------------------------------------------------
    gate="$out/GATE.txt"; : > "$gate"
    pass() { echo "  [ok] $1" | tee -a "$gate"; }
    fail() { echo "  [FAIL] $1" | tee -a "$gate"; failed=1; }
    info() { echo "  [info] $1" | tee -a "$gate"; }
    for exe in "$DX_EXE" "$WASM_BINDGEN_EXE"; do
        local p="$out/$exe" archs libs bad rpath got want minver
        echo "  === $exe ===" | tee -a "$gate"
        archs="$(lipo -archs "$p")"
        [ "$archs" = "$(slice_lipo "$slice")" ] && pass "thin $archs slice" || fail "lipo -archs: '$archs', expected '$(slice_lipo "$slice")'"
        libs="$(otool -L "$p" | tail -n +2 | awk '{print $1}')"
        bad="$(printf '%s\n' "$libs" | grep -Ev '^(/usr/lib/|/System/Library/Frameworks/)' || true)"
        # dx links libgit2 (its install name, LIBGIT2_INSTALL_NAME_MACOS) and nothing else that is
        # not the system's; wasm-bindgen links only the system's.
        if [ "$exe" = "$DX_EXE" ]; then
            bad="$(printf '%s\n' "$bad" | grep -vxF "$LIBGIT2_INSTALL_NAME_MACOS" | grep . || true)"
            printf '%s\n' "$libs" | grep -qxF "$LIBGIT2_INSTALL_NAME_MACOS" && pass "dx links libgit2 as $LIBGIT2_INSTALL_NAME_MACOS (from $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION)" \
                || fail "dx does not link $LIBGIT2_INSTALL_NAME_MACOS: the libgit2-sys stand-in was not linked as intended"
        fi
        printf '%s\n' "$libs" | grep -Eq 'libssl|libcrypto|libssh2|libz\.' && fail "links OpenSSL, libssh2 or zlib: $(printf '%s ' $libs)"
        [ -z "$bad" ] && pass "links only system libraries$([ "$exe" = "$DX_EXE" ] && echo " and libgit2"): $(printf '%s ' $libs)" || fail "non-system libraries: $(printf '%s ' $bad)"
        rpath="$(otool -l "$p" | grep -c LC_RPATH || true)"
        [ "$rpath" = 0 ] && pass "no LC_RPATH" || fail "$rpath LC_RPATH entries"
        minver="$(otool -l "$p" | awk '/LC_BUILD_VERSION/{f=1} f && $1=="minos"{print $2; exit}')"
        [ -z "$minver" ] && minver="$(otool -l "$p" | awk '/LC_VERSION_MIN_MACOSX/{f=1} f && $1=="version"{print $2; exit}')"
        [ "$minver" = "$minos" ] && pass "minimum macOS (minos) $minver = MACOS_DEPLOYMENT_TARGET in pins.env" || fail "minimum macOS is '$minver', pins.env says $minos"
        codesign --verify --strict "$p" 2>/dev/null && pass "codesign --verify --strict (ad-hoc signature)" || fail "codesign --verify failed"
        if strings -a "$p" | grep -E -e "$SOURCES" -e "$BUILD_ROOT" -e "$HOME" -e "$REPO_ROOT" > /dev/null; then fail "build paths found in the binary"
        else pass "no source, build-root, repository or home path in the binary"; fi
        if [ "$exe" = "$DX_EXE" ]; then want="$DX_EXPECTED_VERSION"; else want="$WASM_BINDGEN_EXPECTED_VERSION"; fi
        if can_run "$slice"; then
            # /usr/bin/arch is protected, so DYLD_LIBRARY_PATH is handed over with its -e option.
            if [ "$slice" = x64 ] && [ "$host_arch" = arm64 ]; then got="$(/usr/bin/arch -x86_64 -e DYLD_LIBRARY_PATH="$lib" "$p" --version | first_line)"; else got="$(DYLD_LIBRARY_PATH="$lib" "$p" --version | first_line)"; fi
            [ "$got" = "$want" ] && pass "$exe --version (DYLD_LIBRARY_PATH=$lib): $got" || fail "$exe --version: expected '$want', got '$got'"
            if [ "$exe" = "$DX_EXE" ]; then
                local rc=0 nolib
                if [ "$slice" = x64 ] && [ "$host_arch" = arm64 ]; then nolib="$(/usr/bin/arch -x86_64 "$p" --version 2>&1)" || rc=$?; else nolib="$(env -u DYLD_LIBRARY_PATH "$p" --version 2>&1)" || rc=$?; fi
                if [ "$rc" != 0 ] && printf '%s\n' "$nolib" | grep -q "${LIBGIT2_INSTALL_NAME_MACOS##*/}"; then
                    pass "dx WITHOUT libgit2 does not start (exit $rc): $(printf '%s\n' "$nolib" | first_line)"
                else fail "dx without libgit2: expected dyld to refuse it, got exit $rc: $(printf '%s\n' "$nolib" | first_line)"; fi
            fi
        else
            info "$exe --version: [skip] this Mac cannot run $rid (install Rosetta 2: softwareupdate --install-rosetta) - run it on one that can before adopting"
        fi
    done
    echo "  === functional gate ===" | tee -a "$gate"
    local wasm_opt_dir="${WASM_OPT_DIR:-${NUGET_PACKAGES:-$HOME/.nuget/packages}/codebrix.webplatform.wasmshrink.apachelicenseforever/$FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION/tools/wasm-opt/$rid}"
    if [ "${SKIP_FUNCTIONAL_GATE:-0}" = 1 ]; then info "skipped (SKIP_FUNCTIONAL_GATE=1)"
    elif [ ! -x "$wasm_opt_dir/wasm-opt" ]; then info "SKIPPED - no $rid wasm-opt in $wasm_opt_dir (restore the repository's solution, or set WASM_OPT_DIR). DO NOT ADOPT until it has passed."
    elif ! can_run "$slice"; then info "SKIPPED - this Mac cannot run $rid"
    elif [ "$slice" = x64 ] && [ "$host_arch" = arm64 ]; then info "SKIPPED - the functional gate runs dx natively; run it for osx-x64 on an Intel Mac, or under an x86_64 shell (arch -x86_64 bash), before adopting"
    elif WASM_OPT_DIR="$wasm_opt_dir" LIBGIT2_DIR="$lib" GATE_RID="$rid" bash "$TOOLS_DIR/linux/functional-gate.sh" "$out/$DX_EXE" "$out/$WASM_BINDGEN_EXE" "$OUTPUT_DIR/functional-gate-$rid" 2>&1 | tee "$out/FUNCTIONAL-GATE.txt"; then pass "functional gate passed (FUNCTIONAL-GATE.txt)"
    else fail "functional gate failed (FUNCTIONAL-GATE.txt)"; fi

    if [ "$failed" != 0 ]; then echo "VERIFICATION FAILED for $rid - do not adopt." >&2; exit 1; fi

    # ---- Record ------------------------------------------------------------------------------------
    # shellcheck disable=SC2086
    ( cd "$out" && shasum -a 256 "$DX_EXE" "$WASM_BINDGEN_EXE" $names > SHA256SUMS.txt )
    {
        echo "dx and wasm-bindgen - build information"
        echo "=============================================================================="
        echo "RID              : $rid ($triple)"
        echo "Built            : $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
        echo "Build duration   : dx ${secs_dx}s, wasm-bindgen ${secs_wb}s ($(( SECONDS - started ))s with the gate)"
        echo "Built by         : tools/dx-native-tools/macos/build.sh"
        echo "Build host       : $(sw_vers -productName) $(sw_vers -productVersion) ($(sw_vers -buildVersion)), $host_arch, $(sysctl -n machdep.cpu.brand_string 2>/dev/null || echo unknown)"
        echo "Xcode            : $XCODE, SDK macosx $SDK_VERSION, $CLANG_VERSION"
        echo "Rust             : $RUSTC_VERSION / $CARGO_VERSION"
        echo "Deployment target: MACOSX_DEPLOYMENT_TARGET=$minos (checked against minos)"
        echo "Sources          : $TOOL_SOURCES_DIR (dioxus-cli $DX_CRATE_VERSION, upstream commit $DX_GIT_SHA, the recorded patches; wasm-bindgen-cli $WASM_BINDGEN_VERSION)"
        echo "libgit2          : $LIBGIT2_VERSION from $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION (nupkg sha256 $LIBGIT2_NUPKG_SHA256), linked as $LIBGIT2_INSTALL_NAME_MACOS; not shipped by this build"
        echo "RUSTFLAGS        : --remap-path-prefix for the sources, the build root and \$HOME"
        echo "Stripping        : strip, then codesign --force --sign - (ad hoc)"
        echo
        for exe in "$DX_EXE" "$WASM_BINDGEN_EXE"; do
            echo "File             : $exe"
            echo "Size (stripped)  : $(stat -f %z "$out/$exe") bytes"
            echo "Size (gzip -9 -n): $(gzip -9 -n -c "$out/$exe" | wc -c | tr -d ' ') bytes"
            echo "SHA256           : $(shasum -a 256 "$out/$exe" | cut -d' ' -f1)"
            echo "Links            : $(otool -L "$out/$exe" | tail -n +2 | awk '{print $1}' | tr '\n' ' ')"
            echo
        done
        echo "Files beside them:$names"
        echo
        echo "Gate (GATE.txt)"
        echo "------------------------------------------------------------------------------"
        cat "$gate"
    } > "$out/BUILD-INFO.txt"
    echo "  $rid done: $out"
}

case "${1:-all}" in
    arm64|x64) build_slice "$1" ;;
    all)       build_slice arm64; build_slice x64 ;;
    *) echo "usage: $0 [all|arm64|x64]" >&2; exit 2 ;;
esac
