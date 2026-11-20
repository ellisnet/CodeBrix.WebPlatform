#!/usr/bin/env bash
# ==============================================================================================
# functional-gate.sh - USE a built dx and wasm-bindgen: build a scratch copy of the WorkSite
#                      frontend with them, exactly as FrontendBuilder runs dx, and check the site
# ==============================================================================================
#
#   functional-gate.sh <path to dx> <path to wasm-bindgen> <scratch folder (emptied first)>
#
# build.sh runs it on the build HOST for linux-x64, after the container build. (The macOS
# script runs it on the Mac; the Windows script performs the same steps in PowerShell.) It
# needs what building the frontend always needs on the machine: rustup's cargo with the
# wasm32-unknown-unknown target, and the standalone tailwindcss program on PATH; the wasm-opt of
# the CodeBrix.WebPlatform.WasmShrink package (from the local NuGet cache, at
# FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION in pins.env, or WASM_OPT_DIR=<folder>); the `git`
# program (only to make the local template repository of check 6); and
#     LIBGIT2_DIR=<folder>   the folder holding libgit2 under the name the built dx asks for
#                            (output/libgit2/<rid>/, written by fetch-libgit2.sh). dx does not
#                            start without it; it goes on LD_LIBRARY_PATH (DYLD_LIBRARY_PATH on
#                            macOS) for every dx run.
#
# NOTHING IN THE REPOSITORY IS WRITTEN. FUNCTIONAL_FRONTEND (pins.env) is COPIED - with what
# materializing writes into it, which must therefore exist: platform/ (the shared crate and the
# platform's crates, platform/vendor), src/generated/ and .cargo/config.toml, which points cargo
# at platform/vendor by a relative path - into <build root>/WorkSite/frontend. dx writes only
# into the copy and into <scratch>/dx-home.
#
# THE BUILD ROOT'S PATH HAS THE SAME LENGTH AS THE REAL ONE. Rust embeds the source paths of
# panic locations in the .wasm, so a copy at a longer path gives a larger file (measured: a copy
# under output/functional-gate added 4,274 bytes). The copy is therefore made in a temporary
# folder (under $TMPDIR, else /tmp) padded so that <build root>/WorkSite/frontend is exactly as
# long as the real frontend's path; the .wasm then has the reference build's size, and with the
# folder name swapped back (same length, byte for byte) it can be compared with it exactly. The
# site and the log are copied into <scratch>; the temporary folder is removed.
#
# When `unshare` can create a user and network namespace, the build runs WITH THE NETWORK
# DISABLED (only loopback) - the proof that dx, wasm-bindgen, wasm-opt and cargo fetch nothing.
#
# Checks, each printed as [ok] or [FAIL]:
#    1. dx exits 0 and its output does not say "wasm-opt failed" (dx keeps the unshrunk file
#       and reports success when wasm-opt fails)
#    2. the site folder has index.html, one .wasm (wasm magic + version 1), the JavaScript glue
#       file, the Tailwind stylesheet and wasm-bindgen's snippets
#    3. index.html names the .wasm and the glue file that are there
#    4. the file list matches the reference build of the same frontend in the real folder, when
#       there is one (names compared with dx's content hashes removed); the .wasm sizes are
#       reported, and whether the new .wasm, with the build root's name swapped for the real
#       one, is byte-identical to the reference (reported, not a failure: dx's asset symbols
#       are hashed from the absolute source path, so the bytes cannot match exactly; the
#       printable strings that differ are written to <scratch>/strings-diff.txt)
#    5. TODAY'S TOOLS: a fresh copy of the same snapshot, at the same path, is built with the
#       tools in REFERENCE_TOOLS_DIR (default FUNCTIONAL_REFERENCE_TOOLS_DIR in pins.env: the
#       programs committed for the RID), run from the same folder as the new ones, and every site
#       file must be BYTE-IDENTICAL to the new tools' (skipped, saying so, when that folder has no
#       dx)
#    6. DX_SKIP_WASM_OPT: a release build with DX_SKIP_WASM_OPT=1 and NO wasm-opt anywhere on
#       PATH (nor in dx's data folder) exits 0, says it skipped wasm-opt at the caller's
#       request, and gives a valid .wasm LARGER than the shrunk one of check 1
#    7. `dx new` FROM A LOCAL TEMPLATE: a small git repository made in <scratch>/template is
#       the template; dx new clones it (file://) and fills it in with the network off and NO
#       git program on PATH, so the clone can only have been made by libgit2
#
# Exit status: 0 when every check passed, 1 otherwise. Written for bash 3.2 as well.
# ==============================================================================================

set -uo pipefail

DX="${1:-}"
WASM_BINDGEN="${2:-}"
SCRATCH="${3:-}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$HERE/../../.." && pwd)"
# shellcheck source=pins.env
. "$HERE/pins.env"

[ -n "$DX" ] && [ -n "$WASM_BINDGEN" ] && [ -n "$SCRATCH" ] || {
    echo "usage: $0 <path to dx> <path to wasm-bindgen> <scratch folder>" >&2; exit 2; }
abs() { case "$1" in /*) echo "$1" ;; *) echo "$(pwd)/$1" ;; esac; }
DX="$(abs "$DX")"; WASM_BINDGEN="$(abs "$WASM_BINDGEN")"; SCRATCH="$(abs "$SCRATCH")"
FAILED=0
pass() { echo "  [ok] $1"; }
fail() { echo "  [FAIL] $1"; FAILED=1; }

FRONTEND="$REPO_ROOT/$FUNCTIONAL_FRONTEND"
case "$(uname -s)/$(uname -m)" in
    Darwin/arm64)  HOST_RID=osx-arm64; LOADER_VAR=DYLD_LIBRARY_PATH ;;
    Darwin/x86_64) HOST_RID=osx-x64;   LOADER_VAR=DYLD_LIBRARY_PATH ;;
    Linux/aarch64) HOST_RID=linux-arm64; LOADER_VAR=LD_LIBRARY_PATH ;;
    *)             HOST_RID=linux-x64; LOADER_VAR=LD_LIBRARY_PATH ;;
esac
GATE_RID="${GATE_RID:-$HOST_RID}"
NUGET_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
WASM_OPT_DIR="${WASM_OPT_DIR:-$NUGET_CACHE/codebrix.webplatform.wasmshrink.apachelicenseforever/$FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION/tools/wasm-opt/$GATE_RID}"
REFERENCE_TOOLS_DIR="${REFERENCE_TOOLS_DIR:-$REPO_ROOT/${FUNCTIONAL_REFERENCE_TOOLS_DIR%/*}/$GATE_RID}"
LIBGIT2_DIR="${LIBGIT2_DIR:-}"
EXE=""; case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) EXE=".exe" ;; esac

echo "functional gate - dx builds the WorkSite frontend (a scratch copy)"
echo "  dx           : $DX"
echo "  wasm-bindgen : $WASM_BINDGEN"
echo "  wasm-opt     : $WASM_OPT_DIR/wasm-opt$EXE"
echo "  frontend     : $FRONTEND (copied, never written)"
echo "  scratch      : $SCRATCH"
echo "  libgit2      : ${LIBGIT2_DIR:-NOT SET} (on $LOADER_VAR for every dx run)"
echo "  today's tools: $REFERENCE_TOOLS_DIR"
[ -n "$LIBGIT2_DIR" ] && [ -d "$LIBGIT2_DIR" ] || { fail "LIBGIT2_DIR must name the folder with libgit2 (output/libgit2/<rid>, written by fetch-libgit2.sh); dx does not start without it"; exit 1; }
LIBGIT2_DIR="$(cd "$LIBGIT2_DIR" && pwd)"
LOADER_ENV="$LOADER_VAR=$LIBGIT2_DIR"
# Builds run dx through `sh -c`. macOS removes DYLD_* variables from the environment of a
# protected program such as /bin/sh, so the loader variable is handed to that shell under other
# names and exported by it just before it starts dx (LOADER_EXPORT); on Linux this is the same as
# passing it directly.
LOADER_EXPORT='export "$CBX_LOADER_VAR=$CBX_LIBGIT2_DIR"; '

for f in "$DX" "$WASM_BINDGEN" "$WASM_OPT_DIR/wasm-opt$EXE"; do
    [ -x "$f" ] || { fail "missing or not executable: $f"; exit 1; }
done
command -v cargo > /dev/null 2>&1 || { fail "cargo is not on PATH (install Rust with rustup)"; exit 1; }
command -v tailwindcss > /dev/null 2>&1 || { fail "tailwindcss is not on PATH (see samples/WorkSite/tailwindcss)"; exit 1; }
if command -v rustup > /dev/null 2>&1; then
    rustup target list --installed 2>/dev/null | grep -x wasm32-unknown-unknown > /dev/null \
        || { fail "the Rust target wasm32-unknown-unknown is not installed (rustup target add wasm32-unknown-unknown)"; exit 1; }
fi
for f in platform/codebrix-webplatform/Cargo.toml platform/vendor/.codebrix-platform-crates src/generated/mod.rs .cargo/config.toml; do
    [ -f "$FRONTEND/$f" ] || { fail "$FUNCTIONAL_FRONTEND/$f does not exist: materialize the frontend first (\`dotnet run --project build/WorkSite.BuildFrontend -- generate\` in samples/WorkSite)"; exit 1; }
done

echo "  tools        : $(env "$LOADER_ENV" "$DX" --version 2>&1 | sed -n 1p) / $("$WASM_BINDGEN" --version 2>&1 | sed -n 1p) / $("$WASM_OPT_DIR/wasm-opt$EXE" --version 2>&1 | sed -n 1p)"
echo "  cargo        : $(cargo --version) / tailwindcss: $(tailwindcss --help 2>&1 | grep -o 'v[0-9][0-9.]*' | sed -n 1p)"

# --- the scratch copy ------------------------------------------------------------------------
rm -rf "$SCRATCH"
mkdir -p "$SCRATCH/bin" "$SCRATCH/dx-home"
FRONTEND="$(cd "$FRONTEND" && pwd -P)"
TEMP_PARENT="$(mktemp -d "${TMPDIR:-/tmp}/cbx-fg.XXXXXX")"
trap 'rm -rf "$TEMP_PARENT"' EXIT
REAL_ROOT="$(dirname "$(dirname "$FRONTEND")")"          # the folder above WorkSite/frontend
PAD=$(( ${#REAL_ROOT} - ${#TEMP_PARENT} - 1 ))
if [ "$PAD" -ge 1 ]; then
    BUILD_ROOT="$TEMP_PARENT/$(printf '%*s' "$PAD" '' | tr ' ' 'p')"
    SAME_LENGTH=1
else
    BUILD_ROOT="$TEMP_PARENT/b"; SAME_LENGTH=0
fi
mkdir -p "$BUILD_ROOT/WorkSite/frontend"
# ONE snapshot of the frontend, taken now; every build below starts from a fresh copy of it, at
# the same path, so the builds compared differ only in the tools. (A second build in a used copy
# is NOT comparable: Tailwind scans the folder, finds class-like words in cargo's target/ files and
# writes a different stylesheet, whose name dx then compiles into the .wasm.)
SNAPSHOT="$TEMP_PARENT/frontend-snapshot.tar"
( cd "$FRONTEND" && tar --exclude=./target -cf "$SNAPSHOT" . )
( cd "$BUILD_ROOT/WorkSite/frontend" && tar -xf "$SNAPSHOT" )
cp "$DX" "$SCRATCH/bin/dx$EXE"
cp "$WASM_BINDGEN" "$SCRATCH/bin/wasm-bindgen$EXE"
COPY="$BUILD_ROOT/WorkSite/frontend"
# A used copy is moved out of the build root (inside the temporary folder, removed at the end)
# and a fresh one made from the snapshot at the same path.
fresh_copy() {
    mv "$BUILD_ROOT/WorkSite" "$TEMP_PARENT/$1"
    mkdir -p "$COPY"
    ( cd "$COPY" && tar -xf "$SNAPSHOT" )
}
echo "  copied       : $(find "$COPY" -type f | wc -l | tr -d ' ') files (target/ left out) to $COPY"
echo "                 $([ "$SAME_LENGTH" = 1 ] && echo "(path as long as the real one: ${#COPY} characters)" || echo "(the temporary folder's path is too long to match the real one; sizes may differ by the embedded paths)")"

# --- the build -------------------------------------------------------------------------------
LOG="$SCRATCH/dx-build.log"
RUNNER=""
if command -v unshare > /dev/null 2>&1 && unshare --user --map-root-user --net true 2> /dev/null; then
    RUNNER="$(command -v unshare) --user --map-root-user --net"
fi
echo "  network      : $([ -n "$RUNNER" ] && echo "DISABLED (unshare --net: a new network namespace, loopback only)" || echo "not isolated (unshare unavailable) - recorded, not a failure")"
STARTED="$(date +%s)"
# shellcheck disable=SC2086
( cd "$COPY" && env PATH="$WASM_OPT_DIR:$SCRATCH/bin:$PATH" DX_HOME="$SCRATCH/dx-home" CBX_LOADER_VAR="$LOADER_VAR" CBX_LIBGIT2_DIR="$LIBGIT2_DIR" \
    $RUNNER sh -c 'echo "network interfaces in the build: $(tail -n +3 /proc/net/dev 2>/dev/null | cut -d: -f1 | tr -d " " | tr "\n" " ")"; '"$LOADER_EXPORT"'exec dx '"$FUNCTIONAL_DX_ARGS" ) > "$LOG" 2>&1
RC=$?
ELAPSED=$(( $(date +%s) - STARTED ))
sed -n 1p "$LOG" | sed 's/^/  /'
echo "  dx $FUNCTIONAL_DX_ARGS: exit $RC after ${ELAPSED}s (log: $LOG)"
[ "$RC" = 0 ] && pass "dx exited 0" || { fail "dx exited $RC"; tail -40 "$LOG" | sed 's/^/         /'; exit 1; }
if grep -qi 'wasm-opt failed' "$LOG"; then fail "dx says \"wasm-opt failed\" (the WebAssembly was left unshrunk)"; else pass "no \"wasm-opt failed\" in dx's output"; fi
grep -i 'wasm-opt\|esbuild' "$LOG" | sed 's/\x1b\[[0-9;]*m//g' | sed -n 1,4p | sed 's/^/         dx: /'

# --- the site --------------------------------------------------------------------------------
PUBLIC="$(find "$COPY/target/dx" -type d -path '*/release/web/public' 2>/dev/null | sed -n 1p)"
[ -n "$PUBLIC" ] && [ -f "$PUBLIC/index.html" ] && pass "site folder: ${PUBLIC#$COPY/} with index.html" || { fail "no release/web/public/index.html under target/dx"; exit 1; }
WASMS="$(find "$PUBLIC" -name '*.wasm' | sort)"
NWASM="$(printf '%s\n' "$WASMS" | grep -c . || true)"
if [ "$NWASM" = 1 ]; then
    WASM="$WASMS"; WASM_SIZE="$(wc -c < "$WASM" | tr -d ' ')"
    MAGIC="$(od -An -tx1 -N8 "$WASM" | tr -d ' \n')"
    [ "$MAGIC" = "0061736d01000000" ] && pass "one .wasm: ${WASM#$PUBLIC/}, $WASM_SIZE bytes, wasm magic + version 1" || fail "the .wasm does not start with the wasm magic (got $MAGIC)"
else
    fail "expected exactly one .wasm in the site, found $NWASM"; WASM=""; WASM_SIZE=0
fi
GLUE="$(find "$PUBLIC/assets" -maxdepth 1 -name '*.js' | sort)"
[ "$(printf '%s\n' "$GLUE" | grep -c . || true)" = 1 ] && pass "one JavaScript glue file: ${GLUE#$PUBLIC/}" || fail "expected one glue .js in assets/, found: $(printf '%s ' $GLUE)"
CSS="$(find "$PUBLIC/assets" -maxdepth 1 -name 'tailwind*.css' | sed -n 1p)"
[ -n "$CSS" ] && [ -s "$CSS" ] && pass "Tailwind stylesheet: ${CSS#$PUBLIC/}, $(wc -c < "$CSS" | tr -d ' ') bytes" || fail "no tailwind*.css in assets/"
SNIPPETS="$(find "$PUBLIC/assets/snippets" -type f 2>/dev/null | wc -l | tr -d ' ')"
[ "${SNIPPETS:-0}" -gt 0 ] && pass "wasm-bindgen snippets: $SNIPPETS files under assets/snippets" || fail "no assets/snippets (wasm-bindgen's JavaScript snippets)"
GLUE1="$(printf '%s\n' "$GLUE" | sed -n 1p)"
if [ -n "$GLUE1" ] && grep -q "$(basename "$GLUE1")" "$PUBLIC/index.html"; then pass "index.html loads the glue file that is there"
else fail "index.html does not name the glue file that was written"; fi
if [ -n "$WASM" ] && [ -n "$GLUE1" ] && grep -q "$(basename "$WASM")" "$GLUE1"; then pass "the glue file loads the .wasm that is there"
else fail "the glue file does not name the .wasm that was written"; fi
FILES="$(find "$PUBLIC" -type f | wc -l | tr -d ' ')"
echo "  site files   : $FILES"
( cd "$PUBLIC" && find . -type f | sort | sed 's/^/         /' )
mkdir -p "$SCRATCH/site"
cp -R "$PUBLIC/." "$SCRATCH/site/"
echo "  site copied to $SCRATCH/site"

# --- compared with the reference build in the real folder (if there is one) -------------------
REF="$(find "$FRONTEND/target/dx" -type d -path '*/release/web/public' 2>/dev/null | sed -n 1p)"
if [ -n "$REF" ] && [ -f "$REF/index.html" ]; then
    norm() { ( cd "$1" && find . -type f | sed -E 's/-dxh[0-9a-f]+//' | sort ); }
    if [ "$(norm "$PUBLIC")" = "$(norm "$REF")" ]; then pass "same file list as the reference build in $FUNCTIONAL_FRONTEND/target/dx (hashes aside)"
    else fail "file list differs from the reference build:"; diff <(norm "$REF") <(norm "$PUBLIC") | sed 's/^/         /'; fi
    REF_WASM="$(find "$REF" -name '*.wasm' | sed -n 1p)"
    if [ -n "$REF_WASM" ] && [ -n "$WASM" ]; then
        REF_SIZE="$(wc -c < "$REF_WASM" | tr -d ' ')"
        SWAPPED="$SCRATCH/swapped.wasm"
        OLD="$BUILD_ROOT/WorkSite" NEW="$REAL_ROOT/WorkSite" perl -0777 -pe 's/\Q$ENV{OLD}\E/$ENV{NEW}/g' "$WASM" > "$SWAPPED"
        if cmp -s "$REF_WASM" "$WASM"; then SAME="byte-identical"
        elif [ "$SAME_LENGTH" = 1 ] && cmp -s "$REF_WASM" "$SWAPPED"; then SAME="byte-identical once the build root's name is swapped for the real one"
        else SAME="not byte-identical: dx's asset symbols and the crate's symbol hash are derived from the ABSOLUTE source path, so a copy in another folder never gives the same bytes (strings-diff.txt lists what differs)"
             diff <(strings -a -n 8 "$REF_WASM" | sort -u) <(strings -a -n 8 "$SWAPPED" | sort -u) > "$SCRATCH/strings-diff.txt"; fi
        echo "  [info] reference .wasm: $(basename "$REF_WASM"), $REF_SIZE bytes; this build: $WASM_SIZE bytes - $SAME"
    fi
else
    echo "  [info] no reference build in $FUNCTIONAL_FRONTEND/target/dx to compare with"
fi

# The first build's output is moved aside (never deleted), so each later build starts clean in the
# same folder - the same absolute path, so the same bytes.
mv "$COPY/target/dx" "$SCRATCH/run-new-tools"
NEW_PUBLIC="$SCRATCH/run-new-tools/${PUBLIC#$COPY/target/dx/}"

# --- 5. today's tools: the same copy, the same path, byte for byte ----------------------------
REF_DX="$REFERENCE_TOOLS_DIR/dx$EXE"; REF_WB="$REFERENCE_TOOLS_DIR/wasm-bindgen$EXE"
TODAY_RESULT="skipped"
if [ -x "$REF_DX" ] && [ -x "$REF_WB" ]; then
    fresh_copy used-by-the-new-tools
    # Today's tools run from THE SAME FOLDER as the new ones did (<scratch>/bin): dx hands its own
    # path to cargo, and cargo's metadata hash - so the symbols, so the bytes - follows it. The new
    # tools are kept aside in <scratch>/bin-new and put back afterwards.
    mkdir -p "$SCRATCH/bin-new" "$SCRATCH/dx-home-today"
    cp "$SCRATCH/bin/dx$EXE" "$SCRATCH/bin/wasm-bindgen$EXE" "$SCRATCH/bin-new/"
    cp "$REF_DX" "$SCRATCH/bin/dx$EXE"; cp "$REF_WB" "$SCRATCH/bin/wasm-bindgen$EXE"
    LOG5="$SCRATCH/dx-build-today.log"
    # shellcheck disable=SC2086
    # Today's dx is linked to libgit2 like the new one, so it gets the library the same way.
    ( cd "$COPY" && env PATH="$WASM_OPT_DIR:$SCRATCH/bin:$PATH" DX_HOME="$SCRATCH/dx-home-today" CBX_LOADER_VAR="$LOADER_VAR" CBX_LIBGIT2_DIR="$LIBGIT2_DIR" \
        $RUNNER sh -c "$LOADER_EXPORT"'exec dx '"$FUNCTIONAL_DX_ARGS" ) > "$LOG5" 2>&1
    RC5=$?
    cp "$SCRATCH/bin-new/dx$EXE" "$SCRATCH/bin-new/wasm-bindgen$EXE" "$SCRATCH/bin/"
    TODAY_PUBLIC="$(find "$COPY/target/dx" -type d -path '*/release/web/public' 2>/dev/null | sed -n 1p)"
    if [ "$RC5" = 0 ] && [ -n "$TODAY_PUBLIC" ]; then
        TODAY_WASM="$(find "$TODAY_PUBLIC" -name '*.wasm' | sed -n 1p)"
        TODAY_SIZE="$(wc -c < "$TODAY_WASM" | tr -d ' ')"
        if diff -r "$TODAY_PUBLIC" "$NEW_PUBLIC" > "$SCRATCH/today-diff.txt" 2>&1; then
            pass "today's tools ($(env "$LOADER_ENV" "$REF_DX" --version 2>&1 | sed -n 1p), $REFERENCE_TOOLS_DIR) build the same copy at the same path BYTE-IDENTICALLY: every one of the $(find "$TODAY_PUBLIC" -type f | wc -l | tr -d ' ') site files, .wasm $TODAY_SIZE bytes both"
            TODAY_RESULT="byte-identical ($TODAY_SIZE bytes)"
        else
            fail "the site built by today's tools differs from the new tools' (.wasm $TODAY_SIZE bytes against $WASM_SIZE; $SCRATCH/today-diff.txt)"
            TODAY_RESULT="DIFFERENT"
        fi
    else
        fail "today's dx ($REF_DX) did not build the copy (exit $RC5; $LOG5)"
    fi
    [ -d "$COPY/target/dx" ] && mv "$COPY/target/dx" "$SCRATCH/run-today-tools"
else
    echo "  [info] no programs in $REFERENCE_TOOLS_DIR: the comparison with today's tools is skipped"
fi

# --- 6. DX_SKIP_WASM_OPT: a release build that is not shrunk, with no wasm-opt anywhere -------
NOOPT_PATH=""
OLD_IFS="$IFS"; IFS=":"
for d in $PATH; do
    [ -n "$d" ] && [ -x "$d/wasm-opt$EXE" ] && continue
    NOOPT_PATH="${NOOPT_PATH:+$NOOPT_PATH:}$d"
done
IFS="$OLD_IFS"
NOOPT_PATH="$SCRATCH/bin:$NOOPT_PATH"
mkdir -p "$SCRATCH/dx-home-noopt"
SKIP_SIZE=0
if [ -d "$TEMP_PARENT/used-by-the-new-tools" ]; then fresh_copy used-by-todays-tools; else fresh_copy used-by-the-new-tools; fi
if PATH="$NOOPT_PATH" command -v wasm-opt > /dev/null 2>&1; then
    fail "a wasm-opt is still on the reduced PATH ($(PATH="$NOOPT_PATH" command -v wasm-opt)); the check cannot run"
else
    LOG6="$SCRATCH/dx-build-skip-wasm-opt.log"
    # shellcheck disable=SC2086
    ( cd "$COPY" && env PATH="$NOOPT_PATH" DX_HOME="$SCRATCH/dx-home-noopt" DX_SKIP_WASM_OPT=1 CBX_LOADER_VAR="$LOADER_VAR" CBX_LIBGIT2_DIR="$LIBGIT2_DIR" \
        $RUNNER sh -c "$LOADER_EXPORT"'exec dx '"$FUNCTIONAL_DX_ARGS" ) > "$LOG6" 2>&1
    RC6=$?
    SKIP_PUBLIC="$(find "$COPY/target/dx" -type d -path '*/release/web/public' 2>/dev/null | sed -n 1p)"
    SKIP_WASM="$( [ -n "$SKIP_PUBLIC" ] && find "$SKIP_PUBLIC" -name '*.wasm' | sed -n 1p )"
    if [ "$RC6" = 0 ] && [ -n "$SKIP_WASM" ]; then
        SKIP_SIZE="$(wc -c < "$SKIP_WASM" | tr -d ' ')"
        pass "DX_SKIP_WASM_OPT=1, no wasm-opt on PATH or in dx's data folder: the release build exited 0"
        if grep -q "Skipping wasm-opt at the caller's request" "$LOG6"; then pass "dx said: $(grep -o "Skipping wasm-opt at the caller's request[^\"]*" "$LOG6" | sed 's/\x1b\[[0-9;]*m//g' | sed -n 1p)"
        else fail "dx did not print the \"Skipping wasm-opt at the caller's request\" line ($LOG6)"; fi
        [ "$(od -An -tx1 -N8 "$SKIP_WASM" | tr -d ' \n')" = "0061736d01000000" ] && pass "the unshrunk .wasm is valid WebAssembly (magic + version 1)" || fail "the unshrunk .wasm does not start with the wasm magic"
        if [ "$SKIP_SIZE" -gt "$WASM_SIZE" ]; then pass "the unshrunk .wasm is LARGER: $SKIP_SIZE bytes against $WASM_SIZE shrunk"
        else fail "the unshrunk .wasm ($SKIP_SIZE bytes) is not larger than the shrunk one ($WASM_SIZE)"; fi
    else
        fail "DX_SKIP_WASM_OPT=1 release build without wasm-opt: dx exited $RC6 (log: $LOG6)"; tail -20 "$LOG6" | sed 's/^/         /'
    fi
    [ -d "$COPY/target/dx" ] && mv "$COPY/target/dx" "$SCRATCH/run-skip-wasm-opt"
fi

# --- 7. dx new from a local template, through libgit2 ------------------------------------------
NEW_RESULT="not run"
if command -v git > /dev/null 2>&1; then
    TEMPLATE="$SCRATCH/template"
    mkdir -p "$TEMPLATE/src" "$SCRATCH/empty-bin" "$SCRATCH/new-home"
    printf '[package]\nname = "{{project-name}}"\nversion = "0.1.0"\nedition = "2021"\n\n[dependencies]\n' > "$TEMPLATE/Cargo.toml"
    printf 'fn main() {\n    println!("Hello from {{project-name}}");\n}\n' > "$TEMPLATE/src/main.rs"
    printf '[template]\ncargo_generate_version = ">=0.10.0"\n' > "$TEMPLATE/cargo-generate.toml"
    ( cd "$TEMPLATE" && git init -q && git checkout -q -b main \
        && git add -A && git -c user.name=gate -c user.email=gate@example.invalid commit -q -m "functional gate template" ) \
        || fail "could not make the template repository with git"
    LOG7="$SCRATCH/dx-new.log"
    DXBIN="$SCRATCH/bin/dx$EXE"
    # shellcheck disable=SC2086
    env -i HOME="$SCRATCH/new-home" USER=gate PATH="$SCRATCH/empty-bin" "$LOADER_ENV" \
        $RUNNER "$DXBIN" new --template "file://$TEMPLATE" --branch main --yes --vcs none "$SCRATCH/created/gate-app" > "$LOG7" 2>&1
    RC7=$?
    if [ "$RC7" = 0 ] && grep -qx 'name = "gate-app"' "$SCRATCH/created/gate-app/Cargo.toml" 2>/dev/null \
       && grep -q 'Hello from gate-app' "$SCRATCH/created/gate-app/src/main.rs" 2>/dev/null; then
        pass "dx new --template file://<scratch>/template --branch main --yes --vcs none <scratch>/created/gate-app: exit 0, the network off, PATH holding no git program - the project was cloned by libgit2 and filled in (Cargo.toml name = \"gate-app\", src/main.rs prints \"Hello from gate-app\")"
        NEW_RESULT="passed"
    else
        fail "dx new from the local template: exit $RC7 (log: $LOG7)"; tail -20 "$LOG7" | sed 's/^/         /'
        NEW_RESULT="FAILED"
    fi
else
    fail "git is not installed; it is needed to make the local template repository (sudo apt install git)"
fi

echo "  RESULT: .wasm $WASM_SIZE bytes shrunk, $SKIP_SIZE bytes with DX_SKIP_WASM_OPT; today's tools: $TODAY_RESULT; dx new: $NEW_RESULT; $FILES site files; $([ "$FAILED" = 0 ] && echo "every check passed" || echo "FAILED")"
exit "$FAILED"
