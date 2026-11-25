================================================================================
tools/dx-native-tools/macos - building dx and wasm-bindgen for osx-arm64 and
                              osx-x64
================================================================================

>>> STATUS 2026-11-22: RUN FOR THE FIRST TIME, on the owner's Apple Silicon
    Mac (Apple M2 Pro, macOS 27.2.1): both slices built, gated and adopted
    into ../../../native_libraries/osx-arm64/ and osx-x64/
    (../BUILD-PROVENANCE.txt, ADOPTED 2026-11-22). The first real run needed
    two fixes to build.sh: its executable bit (it was committed 100644), and
    a `local` line that used a variable it declared - macOS's bash 3.2
    expands the whole line first, so `set -u` stopped the build. osx-x64's
    functional gate was run by hand from an x86_64 shell (THE GATE, below);
    neither slice has been run on an Intel Mac yet, and the HostTools tests
    have not been run under an x64 .NET SDK. The script builds whatever the
    tool sources hold (today ten dioxus-cli patches, six splitter
    patches); nothing in it names the patches.
    REBUILT 2026-11-24 (Pacific time): dx for both slices, with all ten
    dioxus-cli and six splitter patches, built, gated and adopted
    (../BUILD-PROVENANCE.txt, the newest ADOPTED block); wasm-bindgen KEPT
    from 2026-11-22, as on Linux and Windows. The WorkSite frontend was
    brought to the current layout from the Linux side's archive (step 2).
    The functional gate's comparison with today's tools failed with exactly
    the expected difference of dioxus-cli patch 0010 (root addresses
    "/assets/..." instead of "/./assets/..."), as on linux-x64; it compares
    against these programs from now on. A rebuild from scratch gives the
    same programs apart from the 16-byte Mach-O UUID (and so the ad-hoc
    signature): macOS builds are not byte-identical across runs. <<<

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
name for that reason. VERIFIED 2026-11-22 on both slices (without the
library dx exits 134, dyld "Library not loaded"). No OpenSSL is involved
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
       * git (Xcode's), for the local template repository the functional gate
         tests `dx new` with;
       * the osx-<arch> wasm-opt for the functional gate comes from the
         CodeBrix.WebPlatform.WasmShrink package in the local NuGet cache
         (FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION in ../linux/pins.env); a
         restore of the repository's solution puts it there. WASM_OPT_DIR
         names another folder. The standalone Tailwind program comes the same
         way, from the CodeBrix.WebPlatform.Styling.Tailwind4.MacOS<Arch>
         package (FUNCTIONAL_STYLING_PACKAGE_VERSION; TAILWIND_DIR names
         another folder) - nothing is installed for it.
  2. Clone (or pull) this repository and have the sample's frontend
     MATERIALIZED BY A FrontendBuilder OF THE CURRENT LAYOUT (in
     samples/WorkSite:  dotnet run --project
     build/WorkSite.BuildFrontend -- generate), so its platform/ (shared
     crate and the platform's crates), src/generated/, .cargo/ and
     assets/generated/tailwind.css exist. Having materialized it once is not
     enough: a frontend materialized by an older FrontendBuilder has its
     stylesheet elsewhere and is not the layout dx now builds. While the
     sample names packages that are not published yet, `generate` does not
     build; those four folders are build output with relative paths only and
     hold nothing of the machine they were written on, so they can be copied
     into the clone from a machine that has them in the current layout.
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
shell, before adopting. From the x86_64 shell it is the shared script with
the arguments build.sh would pass (this is how osx-x64 was gated on
2026-11-22), from this folder's parent:
    W=$HOME/.nuget/packages/codebrix.webplatform.wasmshrink.apachelicenseforever/<FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION>/tools/wasm-opt/osx-x64
    WASM_OPT_DIR="$W" LIBGIT2_DIR="$PWD/output/libgit2/osx-x64" GATE_RID=osx-x64 \
      arch -x86_64 /bin/bash linux/functional-gate.sh "$PWD/output/osx-x64/dx" \
        "$PWD/output/osx-x64/wasm-bindgen" "$PWD/output/functional-gate-osx-x64" \
      2>&1 | tee output/osx-x64/FUNCTIONAL-GATE.txt
It must end "every check passed".

OUTPUT (../output/osx-<arch>/)
--------------------------------------------------------------------------------
  dx, wasm-bindgen, the LICENCE_FILES files, GATE.txt, BUILD-INFO.txt (with
  gzip -9 sizes), SHA256SUMS.txt, FUNCTIONAL-GATE.txt; the functional gate's
  site and log in ../output/functional-gate-osx-<arch>/.
================================================================================
