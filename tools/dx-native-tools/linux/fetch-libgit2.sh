#!/usr/bin/env bash
# ==============================================================================================
# fetch-libgit2.sh - take the prebuilt libgit2 out of the published CodeBrix.LibGit2 package
# ==============================================================================================
#
#   fetch-libgit2.sh <rid> [<rid> ...]        rid: linux-x64 linux-arm64 osx-arm64 osx-x64
#
# dx links libgit2 as a shared library (through the declarations-only stand-in
# sources/codebrix-libgit2-sys). That library is NEVER built in this repository and NEVER
# committed to it: it is the one the published NuGet package LIBGIT2_PACKAGE_ID
# LIBGIT2_PACKAGE_VERSION (pins.env) ships under runtimes/<rid>/native/. This script puts it,
# for each RID asked for, in
#
#     tools/dx-native-tools/output/libgit2/<rid>/        (build scratch, git-ignored)
#         libgit2-49e408b.so        + libgit2-49e408b.so.1.9      (Linux: the link name + SONAME)
#         libgit2-49e408b.dylib     + libgit2-49e408b.1.9.dylib   (macOS: + install name's leaf)
#         SOURCE.txt                                              where it came from, checksums
#
# so one folder serves both the linker (CODEBRIX_LIBGIT2_LIB_DIR) and the loader
# (LD_LIBRARY_PATH / DYLD_LIBRARY_PATH) when the built dx is run by the gates.
#
# WHERE THE PACKAGE COMES FROM, in this order:
#   1. output/libgit2/<package>.nupkg, from an earlier run;
#   2. the local NuGet cache (NUGET_PACKAGES, else ~/.nuget/packages) - a restore of a project
#      that references the package leaves the very file nuget.org served there;
#   3. nuget.org (LIBGIT2_NUPKG_URL) - THE NETWORK, and only when 1 and 2 have no copy.
# The package file must have the checksum LIBGIT2_NUPKG_SHA256 and each library file its
# LIBGIT2_SHA256_<RID>; anything else stops the script. Needs curl (step 3 only) and unzip.
# Written for bash 3.2 as well (the macOS build runs it).
# ==============================================================================================

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_DIR="$(cd "$HERE/.." && pwd)"
# shellcheck source=pins.env
. "$HERE/pins.env"
OUT="$TOOLS_DIR/output/libgit2"

[ "$#" -ge 1 ] || { echo "usage: $0 <rid> [<rid> ...]" >&2; exit 2; }

sha256_of() {
    if command -v sha256sum > /dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1
    else shasum -a 256 "$1" | cut -d' ' -f1; fi
}
command -v unzip > /dev/null 2>&1 || {
    echo "ERROR: unzip is not installed. This script installs nothing; install it yourself:" >&2
    echo "         sudo apt install unzip          (Debian-based Linux; macOS has it)" >&2
    exit 1; }

ID_LOWER="$(printf '%s' "$LIBGIT2_PACKAGE_ID" | tr 'A-Z' 'a-z')"
NUPKG_NAME="$ID_LOWER.$LIBGIT2_PACKAGE_VERSION.nupkg"
NUPKG="$OUT/$NUPKG_NAME"
CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}/$ID_LOWER/$LIBGIT2_PACKAGE_VERSION/$NUPKG_NAME"
mkdir -p "$OUT"

FROM=""
if [ -f "$NUPKG" ] && [ "$(sha256_of "$NUPKG")" = "$LIBGIT2_NUPKG_SHA256" ]; then
    FROM="output/libgit2 (an earlier run)"
elif [ -f "$CACHE" ] && [ "$(sha256_of "$CACHE")" = "$LIBGIT2_NUPKG_SHA256" ]; then
    cp "$CACHE" "$NUPKG.tmp" && mv -f "$NUPKG.tmp" "$NUPKG"
    FROM="the local NuGet cache ($CACHE)"
else
    command -v curl > /dev/null 2>&1 || {
        echo "ERROR: the package is not in the NuGet cache and curl is not installed to download it:" >&2
        echo "         sudo apt install curl" >&2
        exit 1; }
    echo "  libgit2   : downloading $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION from nuget.org (THE NETWORK)"
    curl --fail --silent --show-error --location --proto '=https' -o "$NUPKG.tmp" "$LIBGIT2_NUPKG_URL"
    mv -f "$NUPKG.tmp" "$NUPKG"
    FROM="nuget.org ($LIBGIT2_NUPKG_URL)"
fi
GOT="$(sha256_of "$NUPKG")"
[ "$GOT" = "$LIBGIT2_NUPKG_SHA256" ] || {
    echo "ERROR: $NUPKG has sha256 $GOT; pins.env LIBGIT2_NUPKG_SHA256 says $LIBGIT2_NUPKG_SHA256." >&2
    exit 1; }

for RID in "$@"; do
    case "$RID" in
        linux-x64)   FILE="lib$LIBGIT2_LINK_NAME.so";    WANT="$LIBGIT2_SHA256_LINUX_X64";  LOADER_NAME="$LIBGIT2_SONAME_LINUX" ;;
        linux-arm64) FILE="lib$LIBGIT2_LINK_NAME.so";    WANT="$LIBGIT2_SHA256_LINUX_ARM64"; LOADER_NAME="$LIBGIT2_SONAME_LINUX" ;;
        osx-arm64)   FILE="lib$LIBGIT2_LINK_NAME.dylib"; WANT="$LIBGIT2_SHA256_OSX_ARM64";  LOADER_NAME="${LIBGIT2_INSTALL_NAME_MACOS##*/}" ;;
        osx-x64)     FILE="lib$LIBGIT2_LINK_NAME.dylib"; WANT="$LIBGIT2_SHA256_OSX_X64";    LOADER_NAME="${LIBGIT2_INSTALL_NAME_MACOS##*/}" ;;
        *) echo "ERROR: unknown RID '$RID' (linux-x64, linux-arm64, osx-arm64, osx-x64; win-x64 is build.dx.ps1's)" >&2; exit 2 ;;
    esac
    # Files are overwritten in place (nothing is deleted); a file that fails its checksum is
    # left as INVALID-<name>, never under the name the build uses.
    DEST="$OUT/$RID"
    mkdir -p "$DEST"
    unzip -p "$NUPKG" "runtimes/$RID/native/$FILE" > "$DEST/INVALID-$FILE"
    GOT="$(sha256_of "$DEST/INVALID-$FILE")"
    [ "$GOT" = "$WANT" ] || {
        echo "ERROR: runtimes/$RID/native/$FILE in the package has sha256 $GOT; pins.env says $WANT." >&2
        exit 1; }
    mv -f "$DEST/INVALID-$FILE" "$DEST/$FILE"
    ln -sfn "$FILE" "$DEST/$LOADER_NAME"
    cat > "$DEST/SOURCE.txt" <<EOF
libgit2 $LIBGIT2_VERSION for $RID - for LINKING dx and for RUNNING it in the gates; never committed
Package  : $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION, sha256 $LIBGIT2_NUPKG_SHA256
Taken    : $(date -u '+%Y-%m-%d %H:%M:%S UTC'), from $FROM
File     : runtimes/$RID/native/$FILE, sha256 $WANT
Loader   : $LOADER_NAME -> $FILE (the name the built dx asks the loader for)
EOF
    echo "  libgit2   : $RID <- $LIBGIT2_PACKAGE_ID $LIBGIT2_PACKAGE_VERSION, from $FROM; $FILE sha256 $WANT (+ $LOADER_NAME)"
done
