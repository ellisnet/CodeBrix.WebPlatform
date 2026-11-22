================================================================================
tools/dx-native-tools/macos - building dx and wasm-bindgen for osx-arm64 and
                              osx-x64
================================================================================

>>> STATUS 2026-11-18: build.sh is WRITTEN BUT NEVER RUN - it was written on
    Linux, which has no Apple SDK. There are NO osx-* folders in
    ../../../native_libraries/ yet; ../BUILD-PROVENANCE.txt lists both as
    PENDING. Expect to fix something on the first real run; fix it in the
    script, record it in ../BUILD-PROVENANCE.txt, and rewrite this block.
    UPDATED 2026-11-19, STILL NEVER RUN: the six-patch dx (its git features
    back, linked against libgit2), the libgit2 package step, the
    DX_SKIP_WASM_OPT and `dx new` checks of the shared functional gate.
    UPDATED 2026-11-19 again, STILL NEVER RUN: the script builds whatever the
    tool sources hold, so it picks up dx's current patch set by itself - the
    seven dioxus-cli patches, the seventh of which swaps in the patched bundle
    splitter (sources/wasm-split-cli, its three patches: the call-graph fixes
    0001 and 0002, and 0003, which makes a split build give the same files
    from the same source). Nothing in the script names the patches. <<<

WHAT THIS IS
--------------------------------------------------------------------------------
build.sh builds dx and wasm-bindgen from the tool sources where they are
(TOOL_SOURCES_DIR in ../linux/pins.env), with the pinned Rust toolchain, one
thin executable per slice (not a universal file), gates them and writes
../output/osx-<arch>/. BOTH slices are built on ONE Apple Silicon Mac: Rust
builds x86_64-apple-darwin there with the same Xcode SDK. It installs nothing,
and downloads nothing but the libgit2 package when the local NuGet cache does
not have it.

LIBGIT2. dx links libgit2 1.9.7 through the declarations-only stand-in
../sources/codebrix-libgit2-sys. The library is libgit2-49e408b.dylib from the
published package CodeBrix.LibGit2.MitLicenseForever (LIBGIT2_* in
../linux/pins.env); ../linux/fetch-libgit2.sh takes it out of the package
(NuGet cache, else nuget.org), checks it against the pinned checksums and puts
it in ../output/libgit2/osx-<arch>/, also under the name
libgit2-49e408b.1.9.dylib. The stand-in's build.rs links it
(CODEBRIX_LIBGIT2_LIB_DIR). Nothing of it is committed.

dx DOES NOT START WITHOUT THE LIBRARY. Its load command is the library's
install name, @rpath/libgit2-49e408b.1.9.dylib, and dx has no LC_RPATH, so
dyld finds it only through DYLD_LIBRARY_PATH naming a folder that holds
libgit2-49e408b.1.9.dylib. macOS drops DYLD_* variables when it starts a
protected program (/bin/sh, /usr/bin/env, /usr/bin/arch without -e), so set it
for dx itself; the functional gate passes it through its shell under another
name for that reason. UNVERIFIED until the first run. No OpenSSL is involved
on macOS (git2's OpenSSL dependency is for Linux only).

THE MINIMUM macOS IS SET EXPLICITLY AND ENFORCED. MACOSX_DEPLOYMENT_TARGET is
set from pins.env for every compile and link - 11.0 for arm64 (the first macOS
on Apple Silicon), 10.15 for x64 (the floor of WasmShrink's osx-x64 wasm-opt)
- and the gate reads LC_BUILD_VERSION's minos from each program and fails
unless it equals the pin. To change a floor, change MACOS_DEPLOYMENT_TARGET_*
in pins.env and record why.

Each program is stripped and then signed again AD HOC (codesign --sign -):
stripping invalidates the linker's signature and arm64 macOS refuses unsigned
code. An ad-hoc signature is not a Developer ID; a notarised application that
ships these programs signs them again with its own identity.


STEP BY STEP, ON THE APPLE SILICON MAC
--------------------------------------------------------------------------------
  1. Install, once (nothing here installs them for you). The exact commands
     for each, and how to check the machine, are in BUILD MACHINES (MACOS)
     in the repository's root MAINTAINER-README.txt:
       * the Xcode Command Line Tools (clang, the macOS SDK, otool, lipo,
         strip, codesign):          xcode-select --install
       * the .NET SDK, 10.0.404 or later 10.0 (the Arm64 installer; ALSO the
         x64 installer, run under Rosetta, for step 6's osx-x64 tests);
       * rustup (https://rustup.rs) with TWO toolchains:
           - the DEFAULT, 1.100.0 or newer (FrontendRequirements.
             MinimumRustVersion), with the WebAssembly target - the
             functional gate builds the WorkSite frontend with the default
             cargo:
                 rustup default stable
                 rustup target add wasm32-unknown-unknown
           - EXACTLY the pinned Rust (RUST_TOOLCHAIN in ../linux/pins.env,
             today 1.100.0) with both Apple targets, which build.sh builds dx
             and wasm-bindgen with (cargo +1.100.0) - installed beside the
             default, which it does not change:
                 rustup toolchain install 1.100.0 --profile minimal \
                     --target aarch64-apple-darwin --target x86_64-apple-darwin
       * Rosetta 2, to RUN the osx-x64 slice (its --version and functional
         gate):                     softwareupdate --install-rosetta
       * the standalone tailwindcss program on PATH, at the version in
         samples/WorkSite/tailwindcss/version.txt (tailwindcss-macos-arm64
         from that GitHub release, checked against its sha256sums.txt,
         installed as tailwindcss, and its quarantine attribute removed:
         xattr -d com.apple.quarantine <path> - see BUILD MACHINES);
       * git (Xcode's), for the local template repository the functional gate
         tests `dx new` with;
       * the osx-<arch> wasm-opt for the functional gate comes from the
         CodeBrix.WebPlatform.WasmShrink package in the local NuGet cache
         (FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION in ../linux/pins.env); a
         restore of the repository's solution puts it there. WASM_OPT_DIR
         names another folder.
  2. Clone (or pull) this repository and materialize the sample's frontend
     once (in samples/WorkSite:  dotnet run --project
     build/WorkSite.BuildFrontend -- generate), so its platform/ (shared
     crate and the platform's crates), src/generated/ and .cargo/ exist.
  3. Build each slice:
         cd <repository>/tools/dx-native-tools/macos
         ./build.sh arm64
         ./build.sh x64
     BUILD_ROOT (default $TMPDIR/cbx-dx-build) holds cargo's target folders;
     it must be outside the repository. The source folders are never written.
  4. Read ../output/osx-<arch>/GATE.txt (no [FAIL], nothing SKIPPED) and
     BUILD-INFO.txt (minos above all).
  5. Adopt each slice (../README.txt, ADOPTING A BUILT BINARY): copy dx,
     wasm-bindgen and the four LICENCE_FILES files to
     ../../../native_libraries/osx-<arch>/, write dx.provenance.txt and
     wasm-bindgen.provenance.txt from BUILD-INFO.txt, replace that RID's
     section of ../BUILD-PROVENANCE.txt.
  6. Build src/CodeBrix.WebPlatform.FrontendBuilder.MacOSArm64 (and .MacOSX64)
     in Release and run tests/CodeBrix.WebPlatform.FrontendBuilder.HostTools.Tests
     on the Mac: the osx-arm64 tests stop being skipped and must pass. (The
     tests check the platform they RUN on; to exercise osx-x64, run them under
     an x64 .NET SDK through Rosetta, or on an Intel Mac.)


THE GATE
--------------------------------------------------------------------------------
Per program:
  1. lipo -archs is exactly the requested slice (thin).
  2. Links only system libraries: every otool -L entry is under /usr/lib/ or
     /System/Library/Frameworks/ (no Homebrew), except, for dx,
     @rpath/libgit2-49e408b.1.9.dylib, which dx MUST link; nothing links
     libssl, libcrypto, libssh2 or libz; no LC_RPATH.
  3. minos (LC_BUILD_VERSION) equals MACOS_DEPLOYMENT_TARGET_<ARCH>.
  4. codesign --verify --strict passes.
  5. No source, build-root, repository or home path in the binary.
  6. `--version` exactly as pins.env says, where the Mac can run the slice
     (osx-x64 through Rosetta 2), dx with DYLD_LIBRARY_PATH naming the
     library's folder; otherwise [skip] - run it on a Mac that can before
     adopting. dx without the library must not start.
Then the functional gate - ../linux/functional-gate.sh, the same script as on
Linux (bash 3.2 compatible) - with the wasm-opt folder first on dx's PATH and
the library's folder on DYLD_LIBRARY_PATH: the frontend build, the
DX_SKIP_WASM_OPT build without wasm-opt, and `dx new` from a local template
with no git program on PATH. Without a wasm-opt it is reported SKIPPED and the
slice must not be adopted. For osx-x64 on Apple Silicon it is SKIPPED too
(the script runs dx natively): run it on an Intel Mac, or from an x86_64
shell, before adopting.

OUTPUT (../output/osx-<arch>/)
--------------------------------------------------------------------------------
  dx, wasm-bindgen, the LICENCE_FILES files, GATE.txt, BUILD-INFO.txt (with
  gzip -9 sizes), SHA256SUMS.txt, FUNCTIONAL-GATE.txt; the functional gate's
  site and log in ../output/functional-gate-osx-<arch>/.
================================================================================
