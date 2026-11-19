#!/usr/bin/env bash
# ==============================================================================================
# build.sh - build dx and wasm-bindgen for linux-x64 and linux-arm64, from this repository only
# ==============================================================================================
#
#   ./build.sh                build both (linux-x64, then linux-arm64)
#   ./build.sh all            the same
#   ./build.sh x64            build linux-x64 only
#   ./build.sh arm64          build linux-arm64 only
#
# Options (environment variables):
#   CONTAINER_ENGINE=podman|docker   force an engine (default: podman, then docker)
#   FORCE_IMAGE_REBUILD=1            rebuild the derived build image even if it exists
#   SKIP_FUNCTIONAL_GATE=1           do not build the frontend with the new linux-x64 tools
#   WASM_OPT_DIR=<folder>            the wasm-opt the functional gate puts on dx's PATH
#                                    (default: the WasmShrink package in the local NuGet
#                                    cache, FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION in pins.env)
#
# WHAT THIS DOES, IN ONE PARAGRAPH
#   It makes sure the derived build image exists (the x86_64 manylinux base + clang/lld/llvm +
#   the OpenSSL headers + Rust RUST_TOOLCHAIN + the aarch64 cross sysroot - see
#   Containerfile.x86_64), and takes the prebuilt libgit2 for the RID out of the published
#   CodeBrix.LibGit2 package into output/libgit2/<rid>/ (fetch-libgit2.sh: from the NuGet cache,
#   else nuget.org). Those two are the only steps that may use the network. It then runs
#   container-build.sh inside the image WITH THE NETWORK DISABLED, with tools/dx-native-tools/
#   mounted as /work (only output/ is written), the tool sources (TOOL_SOURCES_DIR in pins.env)
#   mounted READ-ONLY as /src and output/libgit2 READ-ONLY as /libgit2. container-build.sh builds
#   both tools with cargo, offline, from the vendored crates (dx links libgit2 through the
#   stand-in sources/codebrix-libgit2-sys), strips them and gates them. Then this script runs
#   the gate's last part outside that image:
#     * linux-x64:   both tools ON THIS HOST (dx with libgit2 on LD_LIBRARY_PATH, and once
#                    without it, to record that it does not start), then functional-gate.sh: the
#                    new tools build a scratch copy of the sample frontend, compared byte for
#                    byte with today's tools; a release build with DX_SKIP_WASM_OPT and no
#                    wasm-opt; `dx new` from a local template, with no git program;
#     * linux-arm64: both tools in the PINNED manylinux_2_28_aarch64 image, under qemu-user
#                    emulation, with the network disabled (dx with and without libgit2).
#
# WHY linux-arm64 IS CROSS-COMPILED
#   dx is about 550 crates. Under qemu every rustc process is emulated, which would turn a
#   ten-minute build into many hours; and Rust cross-compiles by design. The one thing that has
#   to be right is the target's C library: the cross linker links against the sysroot of the
#   pinned aarch64 image itself (export-sysroot.sh), so the glibc floor is the image's 2.28 - the
#   same as a native build there. The C code dx contains (ring, zstd) is compiled by clang for
#   aarch64 against that sysroot. CodeBrix.WebGpu builds its Rust natives the same way.
#
# THIS SCRIPT NEVER INSTALLS ANYTHING ON YOUR MACHINE. If a prerequisite is missing it names it,
# prints the command that installs it, and stops. See README.txt for the full list.
#
# Modelled on CodeBrix.WebPlatform.WasmShrink's binaryen-native-tools/linux/build.sh.
# ==============================================================================================

set -euo pipefail

trap 'rc=$?; echo "ERROR: build.sh failed (exit $rc) at line $LINENO: $BASH_COMMAND" >&2' ERR

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"           # tools/dx-native-tools/
REPO_ROOT="$(cd "$TOOLS_DIR/../.." && pwd)"
OUTPUT_DIR="$TOOLS_DIR/output"

# shellcheck source=pins.env
. "$SCRIPT_DIR/pins.env"
SOURCES="$REPO_ROOT/$TOOL_SOURCES_DIR"

# ----------------------------------------------------------------------------------------------
# Prerequisites - checked, never installed
# ----------------------------------------------------------------------------------------------
pick_engine() {
    if [ -n "${CONTAINER_ENGINE:-}" ]; then
        command -v "$CONTAINER_ENGINE" > /dev/null 2>&1 || {
            echo "ERROR: CONTAINER_ENGINE=$CONTAINER_ENGINE is not on PATH." >&2; exit 1; }
        echo "$CONTAINER_ENGINE"
        return
    fi
    if command -v podman > /dev/null 2>&1; then echo podman; return; fi
    if command -v docker > /dev/null 2>&1; then echo docker; return; fi
    cat >&2 <<'EOF'
ERROR: neither podman nor docker is installed.

  This script does not install anything. Install a container engine yourself:

    sudo apt install podman

  Then re-run. See README.txt (PREREQUISITES) for the complete list.
EOF
    exit 1
}

ENGINE="$(pick_engine)"

for f in "$DX_DIR/Cargo.toml" "$DX_DIR/Cargo.lock" "$WASM_BINDGEN_DIR/Cargo.toml" "$WASM_BINDGEN_DIR/Cargo.lock" ".cargo/config.toml"; do
    [ -f "$SOURCES/$f" ] || {
        echo "ERROR: $SOURCES/$f is missing." >&2
        echo "       TOOL_SOURCES_DIR in pins.env ($TOOL_SOURCES_DIR) must name the folder that holds" >&2
        echo "       $DX_DIR/, $WASM_BINDGEN_DIR/, vendor/ and .cargo/config.toml. Nothing is downloaded." >&2
        exit 1; }
done
[ -d "$SOURCES/vendor" ] || { echo "ERROR: $SOURCES/vendor (the vendored crates) is missing." >&2; exit 1; }
[ -f "$SOURCES/codebrix-libgit2-sys/Cargo.toml" ] || { echo "ERROR: $SOURCES/codebrix-libgit2-sys (the libgit2-sys stand-in) is missing." >&2; exit 1; }
for f in container-build.sh functional-gate.sh export-sysroot.sh fetch-libgit2.sh Containerfile.x86_64; do
    [ -f "$SCRIPT_DIR/$f" ] || { echo "ERROR: $SCRIPT_DIR/$f is missing; it is part of the repository." >&2; exit 1; }
done

host_machine="$(uname -m)"
[ "$host_machine" = "x86_64" ] || {
    echo "ERROR: this script builds both Linux RIDs from the x86_64 image and needs an x86_64 host" >&2
    echo "       (this one is $host_machine). See README.txt, BUILDING ON AN ARM64 LINUX MACHINE." >&2
    exit 1; }

arch_rid()    { case "$1" in x64) echo linux-x64;; arm64) echo linux-arm64;; esac; }
arch_triple() { case "$1" in x64) echo x86_64-unknown-linux-gnu;; arm64) echo aarch64-unknown-linux-gnu;; esac; }
arch_glibc_max() { case "$1" in x64) echo "$GLIBC_MAX_X64";; arm64) echo "$GLIBC_MAX_ARM64";; esac; }

check_emulation() {
    # Only the linux-arm64 --version gate needs it. grep WITHOUT -q: -q exits at the first match,
    # `ls` then dies of SIGPIPE, and under `set -o pipefail` that reads as "no handler".
    if [ -d /proc/sys/fs/binfmt_misc ] && ls /proc/sys/fs/binfmt_misc 2>/dev/null | grep -x 'qemu-aarch64' > /dev/null; then
        echo "  emulation : qemu-aarch64 binfmt handler registered (for the arm64 --version gate)"
        return 0
    fi
    cat >&2 <<EOF

ERROR: the linux-arm64 gate RUNS the cross-built tools under qemu user-mode emulation, and no
       binfmt handler for qemu-aarch64 is registered. Set it up yourself (it is not done for you):

       sudo apt install qemu-user-static binfmt-support

  Verify with:  ls /proc/sys/fs/binfmt_misc | grep qemu-aarch64

EOF
    exit 1
}

ensure_image() {
    local base context
    base="${IMAGE_X64%%:*}@$IMAGE_X64_DIGEST"
    if [ "${FORCE_IMAGE_REBUILD:-0}" != "1" ] && "$ENGINE" image exists "$DERIVED_IMAGE" 2>/dev/null; then
        echo "  image     : $DERIVED_IMAGE (already built - reusing)"
        return 0
    fi
    echo "  image     : $DERIVED_IMAGE - building it now."
    echo "              THIS STEP USES THE NETWORK: it installs clang, lld, llvm, \`file\`, cpio and"
    echo "              the OpenSSL headers $OPENSSL_DEVEL_VERSION (dnf) and Rust $RUST_TOOLCHAIN (rustup)"
    echo "              into the digest-pinned base image. Every tool build runs with --network none."
    context="$OUTPUT_DIR/image-context"
    mkdir -p "$context"
    # The aarch64 sysroot comes from the pinned aarch64 image (pulled if absent - network).
    "$ENGINE" image exists "${IMAGE_ARM64%%:*}@$IMAGE_ARM64_DIGEST" 2>/dev/null \
        || "$ENGINE" pull --platform linux/arm64 "${IMAGE_ARM64%%:*}@$IMAGE_ARM64_DIGEST"
    check_emulation
    CONTAINER_ENGINE="$ENGINE" bash "$SCRIPT_DIR/export-sysroot.sh" "$context/sysroot-aarch64.tar"
    "$ENGINE" build \
        --platform linux/amd64 \
        -f "$SCRIPT_DIR/Containerfile.x86_64" \
        -t "$DERIVED_IMAGE" \
        --build-arg BASE_IMAGE="$base" \
        --build-arg RUST_TOOLCHAIN="$RUST_TOOLCHAIN" \
        --build-arg OPENSSL_DEVEL_VERSION="$OPENSSL_DEVEL_VERSION" \
        "$context"
}

# The --version gate for linux-arm64: run both tools in the pinned aarch64 image under qemu.
arm64_run_gate() {
    local out="$OUTPUT_DIR/linux-arm64" image log ok=1 got
    image="${IMAGE_ARM64%%:*}@$IMAGE_ARM64_DIGEST"
    log="$out/QEMU-RUN.txt"
    {
        echo "linux-arm64 - the shipped tools RUN under qemu-user emulation"
        echo "=============================================================================="
        echo "Run at   : $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
        echo "Image    : $image (the pinned manylinux_2_28_aarch64 base; --network none)"
        echo "Host     : $(uname -srm)"
    } > "$log"
    echo "libgit2  : $OUTPUT_DIR/libgit2/linux-arm64 on LD_LIBRARY_PATH (output/libgit2/linux-arm64/SOURCE.txt)" >> "$log"
    for pair in "$DX_EXE|$DX_EXPECTED_VERSION" "$WASM_BINDGEN_EXE|$WASM_BINDGEN_EXPECTED_VERSION"; do
        local exe="${pair%%|*}" want="${pair#*|}"
        got="$("$ENGINE" run --rm --pull=never --network none --platform linux/arm64 \
                 -v "$out":/out:ro -v "$OUTPUT_DIR/libgit2/linux-arm64":/libgit2:ro -e LD_LIBRARY_PATH=/libgit2 \
                 "$image" sh -c "uname -m; /out/$exe --version; ldd /out/$exe" 2>&1 || true)"
        printf '\n--- %s ---\n%s\n' "$exe" "$got" >> "$log"
        if [ "$(printf '%s\n' "$got" | sed -n 2p)" = "$want" ] && printf '%s\n' "$got" | sed -n 1p | grep -x aarch64 > /dev/null \
           && ! printf '%s\n' "$got" | grep -q 'not found'; then
            echo "  [ok] $exe --version under qemu (aarch64): $want; ldd resolves in the aarch64 image"
            echo "[ok] $exe --version: $want" >> "$log"
        else
            echo "  [FAIL] $exe under qemu: expected '$want', got:"; printf '%s\n' "$got" | sed 's/^/         /'
            echo "[FAIL] $exe --version" >> "$log"
            ok=0
        fi
    done
    # Without the library: dx must refuse to start (the loader names the missing SONAME).
    got="$("$ENGINE" run --rm --pull=never --network none --platform linux/arm64 \
             -v "$out":/out:ro "$image" sh -c "/out/$DX_EXE --version; echo \"exit \$?\"" 2>&1 || true)"
    printf '\n--- %s WITHOUT libgit2 on the loader path ---\n%s\n' "$DX_EXE" "$got" >> "$log"
    if printf '%s\n' "$got" | grep -q "$LIBGIT2_SONAME_LINUX" && printf '%s\n' "$got" | grep -qx 'exit 127'; then
        echo "  [ok] $DX_EXE WITHOUT libgit2 under qemu: does not start (exit 127, the loader names $LIBGIT2_SONAME_LINUX)"
        echo "[ok] $DX_EXE without libgit2: does not start, exit 127" >> "$log"
    else
        echo "  [FAIL] $DX_EXE without libgit2 under qemu: expected the loader to refuse it, got:"; printf '%s\n' "$got" | sed 's/^/         /'
        echo "[FAIL] $DX_EXE without libgit2" >> "$log"
        ok=0
    fi
    cat "$log" >> "$out/GATE.txt"
    [ "$ok" = 1 ] || { echo "VERIFICATION FAILED for linux-arm64 (qemu run). Do not adopt." >&2; exit 1; }
}

# The last gate items for linux-x64: run on THIS host (its own glibc, outside any image), then
# the functional gate.
x64_host_gate() {
    local out="$OUTPUT_DIR/linux-x64" log ok=1 got lib="$OUTPUT_DIR/libgit2/linux-x64"
    log="$out/HOST-CHECK.txt"
    {
        echo "linux-x64 - the shipped tools run on the build HOST, outside the container"
        echo "=============================================================================="
        echo "Run at   : $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
        echo "Host     : $(. /etc/os-release 2>/dev/null; echo "${PRETTY_NAME:-unknown}"), $(uname -srm)"
        echo "glibc    : $(ldd --version 2>&1 | sed -n 1p)"
        echo "libgit2  : $lib on LD_LIBRARY_PATH (output/libgit2/linux-x64/SOURCE.txt)"
    } > "$log"
    for pair in "$DX_EXE|$DX_EXPECTED_VERSION" "$WASM_BINDGEN_EXE|$WASM_BINDGEN_EXPECTED_VERSION"; do
        local exe="${pair%%|*}" want="${pair#*|}"
        got="$(LD_LIBRARY_PATH="$lib" "$out/$exe" --version 2>&1 | sed -n 1p || true)"
        { echo; echo "--- $exe ---"; echo "--version: $got"; LD_LIBRARY_PATH="$lib" ldd "$out/$exe" 2>&1 | sed 's/^/  /'; } >> "$log"
        if [ "$got" = "$want" ] && ! LD_LIBRARY_PATH="$lib" ldd "$out/$exe" 2>&1 | grep -q 'not found'; then
            echo "  [ok] $exe --version on this host: $want; ldd resolves"
        else
            echo "  [FAIL] $exe on this host: expected '$want', got '$got'"; ok=0
        fi
    done
    # Without the library: dx must refuse to start - the fact the owner needs, recorded.
    local rc=0
    got="$(env -u LD_LIBRARY_PATH "$out/$DX_EXE" --version 2>&1)" || rc=$?
    { echo; echo "--- $DX_EXE WITHOUT libgit2 on the loader path ---"; echo "exit $rc: $got"; } >> "$log"
    if [ "$rc" = 127 ] && printf '%s\n' "$got" | grep -q "$LIBGIT2_SONAME_LINUX"; then
        echo "  [ok] $DX_EXE WITHOUT libgit2: does not start (exit 127): $got"
    else
        echo "  [FAIL] $DX_EXE without libgit2: expected the loader to refuse it (exit 127), got exit $rc: $got"; ok=0
    fi
    [ "$ok" = 1 ] || { echo "VERIFICATION FAILED for linux-x64 (host run). Do not adopt." >&2; exit 1; }
    if [ "${SKIP_FUNCTIONAL_GATE:-0}" = "1" ]; then
        echo "  functional gate: skipped (SKIP_FUNCTIONAL_GATE=1)" | tee -a "$log"
        return 0
    fi
    echo "  --- functional gate: the new dx + wasm-bindgen build a scratch copy of the frontend ---"
    if LIBGIT2_DIR="$lib" bash "$SCRIPT_DIR/functional-gate.sh" "$out/$DX_EXE" "$out/$WASM_BINDGEN_EXE" "$OUTPUT_DIR/functional-gate" 2>&1 | tee "$out/FUNCTIONAL-GATE.txt"; then
        echo "  [ok] functional gate passed ($out/FUNCTIONAL-GATE.txt)"
    else
        echo "  [FAIL] functional gate failed - see $out/FUNCTIONAL-GATE.txt" >&2
        exit 1
    fi
}

build_arch() {
    local arch="$1" rid triple started elapsed
    rid="$(arch_rid "$arch")"
    triple="$(arch_triple "$arch")"
    started="$SECONDS"

    echo
    echo "=============================================================================="
    echo " BUILD $rid ($triple)"
    echo "=============================================================================="
    echo "  base tag  : $IMAGE_X64"
    echo "  base pin  : ${IMAGE_X64%%:*}@$IMAGE_X64_DIGEST"
    echo "  sources   : $SOURCES (mounted READ-ONLY)"
    [ "$arch" = arm64 ] && check_emulation
    ensure_image
    bash "$SCRIPT_DIR/fetch-libgit2.sh" "$rid"
    echo "  network   : DISABLED for the build itself (--network none)"
    echo

    "$ENGINE" run --rm \
        --network none \
        --platform linux/amd64 \
        -v "$TOOLS_DIR":/work:Z \
        -v "$SOURCES":/src:ro \
        -v "$OUTPUT_DIR/libgit2":/libgit2:ro \
        -e TARGET_RID="$rid" \
        -e TARGET_TRIPLE="$triple" \
        -e DERIVED_IMAGE="$DERIVED_IMAGE" \
        -e BASE_IMAGE_REF="${IMAGE_X64%%:*}@$IMAGE_X64_DIGEST" \
        -e GLIBC_MAX="$(arch_glibc_max "$arch")" \
        -e BUILD_HOST="$(uname -srm)" \
        "$DERIVED_IMAGE" \
        bash /work/linux/container-build.sh

    case "$arch" in
        x64)   x64_host_gate ;;
        arm64) arm64_run_gate ;;
    esac

    elapsed=$(( SECONDS - started ))
    echo "  $rid finished in ${elapsed}s (end to end, including any image build and the gates)"
    echo "End to end       : ${elapsed}s (build.sh, including any image build and every gate)" >> "$OUTPUT_DIR/$rid/BUILD-INFO.txt"
}

write_sha256sums() {
    # One checksum file covering every built RID, including any built on other machines and
    # copied in. Paths are relative to output/.
    [ -d "$OUTPUT_DIR" ] || return 0
    ( cd "$OUTPUT_DIR" && \
      find . -mindepth 2 -maxdepth 2 -type f \( -name "$DX_EXE" -o -name "$DX_EXE.exe" -o -name "$WASM_BINDGEN_EXE" -o -name "$WASM_BINDGEN_EXE.exe" \) -print \
        | sed 's|^\./||' | sort | xargs -r sha256sum > SHA256SUMS )
    echo
    echo "  output/SHA256SUMS:"
    sed 's/^/    /' "$OUTPUT_DIR/SHA256SUMS"
}

# ----------------------------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------------------------
TARGET="${1:-all}"

echo "container engine : $ENGINE"
echo "tools folder     : $TOOLS_DIR"
echo "tool sources     : $SOURCES (TOOL_SOURCES_DIR=$TOOL_SOURCES_DIR)"
echo "dioxus-cli       : $DX_CRATE_VERSION, features $DX_FEATURES, upstream commit $DX_GIT_SHA"
echo "libgit2          : $LIBGIT2_VERSION from $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION (linked, not built)"
echo "wasm-bindgen-cli : $WASM_BINDGEN_VERSION"
echo "Rust             : $RUST_TOOLCHAIN (pinned, inside the image)"

case "$TARGET" in
    x64|arm64) build_arch "$TARGET" ;;
    all)       for a in x64 arm64; do build_arch "$a"; done ;;
    *) echo "usage: $0 [all|x64|arm64]" >&2; exit 2 ;;
esac

write_sha256sums

echo
echo "=============================================================================="
echo " Outputs are in tools/dx-native-tools/output/<rid>/"
echo "   $DX_EXE, $WASM_BINDGEN_EXE       stripped - the files the package ships"
echo "   LICENSE-*.txt, THIRD-PARTY-NOTICES-*.txt   what travels with them"
echo "   BUILD-INFO.txt            toolchain, pins, sizes, sha256, glibc floor, the gate"
echo "   GATE.txt                  the gate's lines, verbatim"
echo "   compiled-in-<tool>.txt    every crate cargo compiles into each tool for this target"
echo "   HOST-CHECK.txt, FUNCTIONAL-GATE.txt   (linux-x64)  QEMU-RUN.txt   (linux-arm64)"
echo " To adopt them into the package, follow ADOPTING A BUILT BINARY in README.txt."
echo "=============================================================================="
