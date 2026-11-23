================================================================================
tools/dx-native-tools/windows - building dx.exe and wasm-bindgen.exe for win-x64
================================================================================

>>> STATUS 2026-11-18: build.dx.ps1 is WRITTEN BUT NEVER RUN - it was written
    on Linux, where it cannot run. There is NO win-x64 folder in
    ../../../native_libraries/ yet; ../BUILD-PROVENANCE.txt lists win-x64 as
    PENDING. Expect to fix something on the first real run; fix it in the
    script, record it in ../BUILD-PROVENANCE.txt, and rewrite this block.
    UPDATED 2026-11-19, STILL NEVER RUN: the six-patch dx (its git features
    back, linked against libgit2), the libgit2 package step and the import
    library it makes, the DX_SKIP_WASM_OPT and `dx new` checks.
    UPDATED 2026-11-19 again, STILL NEVER RUN: the script builds whatever the
    tool sources hold, so it picks up dx's current patch set by itself - the
    seven dioxus-cli patches, the seventh of which swaps in the patched bundle
    splitter (sources/wasm-split-cli, its three patches: the call-graph fixes
    0001 and 0002, and 0003, which makes a split build give the same files
    from the same source). Nothing in the script names the patches. <<<

WHAT THIS IS
--------------------------------------------------------------------------------
One script, build.dx.ps1. It builds dx.exe and wasm-bindgen.exe from the tool
sources where they are (TOOL_SOURCES_DIR in ../linux/pins.env), with the pinned
Rust toolchain and MSVC, gates the result (including running both programs
and building the WorkSite frontend with them), and writes ../output/win-x64/.
It installs nothing, and downloads nothing but the libgit2 package when the
local NuGet cache does not have it. x64 only: win-arm64 is not a supported
RID.

LIBGIT2. dx links libgit2 1.9.7 through the declarations-only stand-in
..\sources\codebrix-libgit2-sys; the library is git2-49e408b.dll from the
published package CodeBrix.LibGit2.MitLicenseForever (LIBGIT2_* in
..\linux\pins.env). The script takes the package from the local NuGet cache
(%NUGET_PACKAGES%, else %USERPROFILE%\.nuget\packages), else downloads it from
nuget.org into ..\output\libgit2\, checks it and the DLL against the pinned
checksums, and puts the DLL in ..\output\libgit2\win-x64\. THE PACKAGE CARRIES
NO IMPORT LIBRARY, and MSVC links a DLL only through one, so the script MAKES
it from the DLL: `dumpbin /exports git2-49e408b.dll` -> git2-49e408b.def (one
line per exported name) -> `lib /def:git2-49e408b.def /machine:x64
/out:git2-49e408b.lib`. The stand-in's build.rs links that .lib
(CODEBRIX_LIBGIT2_LIB_DIR). Nothing of it is committed.

dx.exe DOES NOT START WITHOUT git2-49e408b.dll: it must be beside dx.exe or in
a folder on PATH. The gate runs dx.exe with the DLL's folder first on PATH,
and once without it, expecting Windows to refuse it (exit 0xC0000135,
STATUS_DLL_NOT_FOUND). Windows may show a "system error" dialog for that run;
the script waits at most 30 seconds and then ends the process. No OpenSSL is
involved on Windows (git2's OpenSSL dependency is Linux-only).

Differences from the Linux build, both stated in pins.env (WINDOWS_RUSTFLAGS):
  * the C runtime is linked STATICALLY (-C target-feature=+crt-static; the C
    code in ring and zstd follows with /MT), so no Visual C++ Redistributable
    is needed. The gate fails on any VCRUNTIME / MSVCP / ucrtbase /
    api-ms-win-crt import;
  * -C link-arg=/PDBALTPATH:%_PDB% keeps the build folder's path out of the
    executable's debug directory. The .pdb files cargo writes stay in the
    build folder and are not shipped.
Windows has no switch for "no network" like the Linux container's; the build
is --offline against the vendored crates, and the script fails if cargo's log
shows any download.


STEP BY STEP, ON THE WINDOWS x64 MACHINE
--------------------------------------------------------------------------------
  1. Install, once (nothing here installs them for you). The exact commands
     for each, and how to check the machine, are in BUILD MACHINES (WINDOWS)
     in the repository's root MAINTAINER-README.txt:
       * the .NET SDK, 10.0.404 or later 10.0:
             winget install Microsoft.DotNet.SDK.10
       * Visual Studio 2022 or newer, or its Build Tools, with the workload
         "Desktop development with C++" (MSVC links Rust programs and
         compiles the C code; dumpbin is used by the gate):
             winget install Microsoft.VisualStudio.2022.BuildTools
         then add the workload in the Visual Studio Installer;
       * rustup with TWO toolchains:
           - the DEFAULT, 1.100.0 or newer (FrontendRequirements.
             MinimumRustVersion), with the WebAssembly target - the
             functional gate builds the WorkSite frontend with the default
             cargo:
                 winget install Rustlang.Rustup
                 rustup default stable
                 rustup target add wasm32-unknown-unknown
           - EXACTLY the pinned Rust (RUST_TOOLCHAIN in ..\linux\pins.env,
             today 1.100.0) with the Windows target, which build.dx.ps1
             builds dx.exe and wasm-bindgen.exe with (cargo +1.100.0) -
             installed beside the default, which it does not change:
                 rustup toolchain install 1.100.0 --profile minimal --target x86_64-pc-windows-msvc
       * git (only to make the local template repository the functional
         gate tests `dx new` with):
             winget install Git.Git
       * the win-x64 wasm-opt.exe for the functional gate comes from the
         CodeBrix.WebPlatform.WasmShrink package in the local NuGet cache
         (FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION in ..\linux\pins.env); a
         restore of the repository's solution puts it there. -WasmOptDir
         names another folder. tailwindcss.exe comes the same way, from the
         CodeBrix.WebPlatform.Styling.Tailwind4.WindowsX64 package
         (FUNCTIONAL_STYLING_PACKAGE_VERSION; -TailwindDir names another
         folder) - nothing is installed for it.
  2. Clone (or pull) this repository and materialize the sample's frontend
     once (in samples\WorkSite:  dotnet run --project
     build\WorkSite.BuildFrontend -- generate), so its platform\ (shared crate
     and the platform's crates), src\generated\ and .cargo\ folders exist.
  3. Open PowerShell (5.1 or 7) and run:
         cd <repository>\tools\dx-native-tools\windows
         .\build.dx.ps1
     -BuildRoot (default C:\cbx-dx) is where cargo's target folder and the
     functional gate's copy live. Keep it a short path OUTSIDE your user
     profile and outside the repository; the gate fails if either path is
     found inside a shipped .exe. The source folders are never written to.
     If PowerShell refuses to run the script:
         powershell -NoProfile -ExecutionPolicy Bypass -File .\build.dx.ps1 ...
  4. Read ..\output\win-x64\GATE.txt - no [FAIL], functional gate not
     SKIPPED - and BUILD-INFO.txt.
  5. Adopt (..\README.txt, ADOPTING A BUILT BINARY): copy dx.exe,
     wasm-bindgen.exe and the four files of LICENCE_FILES to
     ..\..\..\native_libraries\win-x64\, write dx.exe.provenance.txt and
     wasm-bindgen.exe.provenance.txt from BUILD-INFO.txt, replace the win-x64
     section of ..\BUILD-PROVENANCE.txt. Measure the gzip -9 sizes on Linux
     for the record (the script does not compute them).
  6. Build src\CodeBrix.WebPlatform.FrontendBuilder.WindowsX64 in Release and
     run tests\CodeBrix.WebPlatform.FrontendBuilder.HostTools.Tests on the
     Windows machine (dotnet test --project ...): the win-x64 tests stop
     being skipped and must pass.


THE GATE
--------------------------------------------------------------------------------
Per program: PE machine 8664 (x64); imports only operating-system DLLs (the
allow-list is in the script - an import outside it fails the gate: decide,
then add it there with a reason) - and for dx.exe git2-49e408b.dll, which it
MUST import (wasm-bindgen.exe must not); no VCRUNTIME / MSVCP / ucrtbase /
api-ms-win-crt / libssl / libcrypto / zlib / libssh2 import; no user-profile,
repository, source or build-root path in the file (ASCII or UTF-16);
`--version` exactly as pins.env says (dx.exe with the DLL on PATH); dx.exe
without the DLL does not start. Then the functional gate: the new dx.exe and
wasm-bindgen.exe, with the wasm-opt folder and the DLL's folder on PATH, run
`dx build --platform web --release --debug-symbols false` on a copy of the
materialized samples\WorkSite\frontend (robocopy without target\; the
platform's crates come with it, in platform\vendor); exit 0, no "wasm-opt
failed", an index.html that loads the glue .js, one .wasm, the Tailwind
stylesheet and wasm-bindgen's snippets. Then a release build with
DX_SKIP_WASM_OPT=1 and no wasm-opt.exe on PATH must exit 0, say it skipped
wasm-opt, and give a larger .wasm; and `dx new` from a local git template,
with no git program on PATH, must clone (by libgit2) and fill in the
project. (No byte-for-byte comparison with today's tools: there are no
adopted win-x64 programs to compare with.)

OUTPUT (..\output\win-x64\)
--------------------------------------------------------------------------------
  dx.exe, wasm-bindgen.exe, the LICENCE_FILES files, GATE.txt, BUILD-INFO.txt,
  SHA256SUMS.txt. The functional gate's copy and dx's log are under
  -BuildRoot\functional-gate.
================================================================================
