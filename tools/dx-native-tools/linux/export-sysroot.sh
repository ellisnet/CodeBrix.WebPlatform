#!/usr/bin/env bash
# ==============================================================================================
# export-sysroot.sh - the aarch64 cross-link sysroot, from the PINNED manylinux_2_28_aarch64
#                     image, as a tar for Containerfile.x86_64
# ==============================================================================================
#
#   ./export-sysroot.sh <output tar>
#
# Runs the digest-pinned manylinux_2_28_aarch64 image ONCE (qemu-user, --network none,
# --pull=never, a few seconds: `rpm -ql` + tar) and writes the files a linker needs to produce an
# executable FOR that image's userland:
#   glibc + glibc-devel + glibc-headers + kernel-headers + libgcc    (rpm -ql: exactly the
#                                                                    distribution's files)
#   the image's GCC support directory (crtbegin.o, libgcc.a, libgcc_s.so ...), placed at
#   usr/lib/gcc/aarch64-redhat-linux/<version> where clang looks for it
# Linking against THAT glibc is what gives the cross-built linux-arm64 executables the glibc
# floor of the image (2.28) - the same floor a native build in that image would give.
#
# Called by build.sh before it builds the derived image; reads only the pinned image. Modelled
# on CodeBrix.WebGpu's wgpu-native-tools/linux/export-sysroot.sh.
# ==============================================================================================
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=pins.env
. "$SCRIPT_DIR/pins.env"
ENGINE="${CONTAINER_ENGINE:-podman}"
out="${1:?usage: $0 <output tar>}"
image="${IMAGE_ARM64%%:*}@$IMAGE_ARM64_DIGEST"
gccdir=/opt/rh/gcc-toolset-14/root/usr/lib/gcc/aarch64-redhat-linux
mkdir -p "$(dirname "$out")"
"$ENGINE" run --rm --pull=never --network none --platform linux/arm64 "$image" sh -c "
    set -e
    cd /
    {
        rpm -ql glibc glibc-devel glibc-headers kernel-headers libgcc 2>/dev/null | grep '^/' || true
        find $gccdir
    } | sed -e 's|^/lib64/|/usr/lib64/|' -e 's|^/lib/|/usr/lib/|' | sort -u | \\
      while read -r f; do if [ -e \"\$f\" ] || [ -L \"\$f\" ]; then echo \"\${f#/}\"; fi; done > /tmp/files
    tar -cf - --no-recursion -T /tmp/files --transform 's|^opt/rh/gcc-toolset-14/root/||'
" > "$out.tmp"
mv -f "$out.tmp" "$out"
echo "aarch64 sysroot: $out ($(tar -tf "$out" | wc -l) entries, $(stat -c %s "$out") bytes) from $image"
