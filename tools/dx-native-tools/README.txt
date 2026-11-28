================================================================================
dx-native-tools - everything needed to build the dx and wasm-bindgen programs
================================================================================

WHAT THIS FOLDER IS FOR
--------------------------------------------------------------------------------
CodeBrix.WebPlatform ships two prebuilt Rust programs, one NuGet package per
build platform, so that a developer no longer compiles them (and needs no C
compiler for them):

  dx            the Dioxus CLI - dioxus-cli 0.7.10 with the repository's
                thirteen patches (the seventh swaps in a patched copy of the
                bundle splitter, sources/wasm-split-cli), built with the cargo
                features no-downloads and disable-telemetry; every dx
                command is there, `dx new`,
                `dx init` and `dx components` included. dx LINKS libgit2
                1.9.7 as a shared library, the prebuilt one the published
                package CodeBrix.LibGit2.MitLicenseForever ships (see "dx
                NEEDS libgit2 TO START" below)
  wasm-bindgen  the wasm-bindgen CLI - wasm-bindgen-cli 0.2.130, unpatched

This folder holds the source they are built from (sources/) with the record of
how that source was taken and patched (vendoring/), the scripts that build
them, the pins every build uses, the gate every build must pass, the licence
file and the generated third-party notices that travel with them, and the
record of what was built. The finished
programs are committed under ../../native_libraries/<rid>/ and packed by the
five projects src/CodeBrix.WebPlatform.FrontendBuilder.<Platform>/.


THE RULE THIS FOLDER IS HELD TO
--------------------------------------------------------------------------------
The rule every CodeBrix native-tools folder follows (Jeremy, 2026-09-15): every
source file a build needs is in the repository, and the build pulls nothing
from outside it. The exception is the build machine's tools - here the Rust
toolchain, a C compiler / linker, a container engine, Visual Studio, Xcode -
which each platform README lists with the command that installs them. No
script installs anything.

  * The SOURCE is read where it is and never written to: the tool sources and
    their vendored crates, in the folder named by TOOL_SOURCES_DIR in
    linux/pins.env - sources/ in this folder, which holds dioxus-cli/,
    wasm-bindgen-cli/, vendor/ and .cargo/config.toml. (The trees were moved
    here from the WorkSite sample on 2026-11-19 by changing that one line;
    the linux-x64 rebuild from the new place gave byte-identical programs -
    see BUILD-PROVENANCE.txt.)
  * Every build is `cargo build --release --locked --offline`, so cargo cannot
    fetch a crate: .cargo/config.toml replaces crates.io with the vendor folder.
  * libgit2's SOURCE is never in this repository, in any form. dx reaches
    libgit2 through the git2 crate, whose crates.io dependency libgit2-sys
    carries libgit2's C source; dx's Cargo.toml (patch 0002) substitutes
    sources/codebrix-libgit2-sys, our declarations-only stand-in, which
    compiles no C code and links the PREBUILT libgit2 of the published
    package CodeBrix.LibGit2.MitLicenseForever (LIBGIT2_* in linux/pins.env).
    Each build takes that library out of the package - from the local NuGet
    cache, else downloaded from nuget.org into output/libgit2/ - and checks
    it against the pinned checksums; it is never committed here.
    `LibraryChecker vendor` fails if a crates.io libgit2-sys is vendored, if
    the stand-in holds C code, or if libgit2's header shows up anywhere.
  * On Linux the build runs in a container started with `--network none`, and
    the build log starts with the proof (only the loopback interface). The
    tool sources are mounted READ-ONLY and cargo's target folder is inside the
    container. Building the derived image (Rust, clang, the OpenSSL headers)
    and fetching the libgit2 package when it is not in the NuGet cache are
    the only steps that use the network.
  * The OpenSSL DEVELOPMENT HEADERS are a build-machine tool on Linux (the
    derived image installs them, OPENSSL_DEVEL_VERSION in linux/pins.env; on
    a bare Debian host `sudo apt install libssl-dev`). dx's project templates
    come from cargo-generate, which turns on git2's `https` feature, which on
    Linux makes git2 depend on the openssl-sys crate. git2 never calls it:
    nothing from OpenSSL is compiled into or linked by dx, and the gate
    proves it (no libssl / libcrypto in NEEDED, no OpenSSL symbol imported).
    openssl-sys's build script reads the headers to learn the version and
    stops without them. Windows and macOS need nothing of the kind.


FOLDER MAP
--------------------------------------------------------------------------------
  README.txt              this document
  sources/                THE TOOL SOURCES (read-only to every build):
    dioxus-cli/           dioxus-cli 0.7.10 with its recorded patches applied
    codebrix-libgit2-sys/ OURS: the declarations-only stand-in for the
                          libgit2-sys crate (README.txt there is its notice:
                          what was taken from upstream, the three changes)
    wasm-bindgen-cli/     wasm-bindgen-cli 0.2.130, unpatched
    wasm-split-cli/       dx's bundle splitter, wasm-split-cli 0.7.10 with its
                          recorded patches (dx's patch 0007 puts it in place
                          of the crates.io crate)
    vendor/               every crate the two are built from (cargo vendor)
    .cargo/config.toml    replaces crates.io with vendor/
  split-check/            OURS, maintainer tooling only (in no program and no
                          package): a check of a site built with bundle
                          splitting - see CHECKING A SPLIT BUILD; built
                          offline from sources/vendor (its .cargo/config.toml)
  vendoring/<tool>/       per tool: the pristine crates.io archive, its
                          .sha256, the numbered patches (dioxus-cli,
                          wasm-split-cli),
                          PATCH-NOTES.txt and UPGRADE-README.txt
  BUILD-PROVENANCE.txt    what was actually built, when, by what, with what
                          hashes and gate results - one section per RID
  .gitignore              re-includes this folder from the root .gitignore
                          (which ignores *.env, bin/ ...) and ignores only
                          output/ and a tool's target/ folder
  licences/
    LICENSE-Dioxus.txt    dx's licence file: dioxus-cli is "MIT OR
                          Apache-2.0", used under Apache-2.0; the crate as
                          published has no licence file, so this one states
                          the facts and carries the Apache-2.0 text
  notices/
    notices-header-Dioxus.txt, notices-header-WasmBindgen.txt
                          the hand-written openings of the notices files
    notices-header-ToolSources.txt
    THIRD-PARTY-NOTICES-Dioxus.txt, THIRD-PARTY-NOTICES-WasmBindgen.txt
                          GENERATED - see NOTICES below; travel with the
                          programs
    THIRD-PARTY-NOTICES-ToolSources.txt
                          GENERATED: every crate in sources/vendor
                          (repository-only)
  linux/                  linux-x64 and linux-arm64: pins.env (every pin, read
                          by every platform), build.sh, container-build.sh,
                          Containerfile.x86_64, export-sysroot.sh,
                          fetch-libgit2.sh and functional-gate.sh (both
                          shared with macOS), README.txt
  windows/                win-x64: build.dx.ps1, README.txt. RUN
                          2026-11-22 (built and adopted), 2026-11-24 and
                          2026-11-27 (dx.exe rebuilt and adopted, the last
                          time from the current sources; wasm-bindgen.exe
                          kept)
  macos/                  osx-arm64 and osx-x64: build.sh, README.txt. RUN
                          2026-11-22 (both slices built and adopted),
                          2026-11-24 and 2026-11-27 (dx rebuilt and adopted,
                          the last time from the current sources;
                          wasm-bindgen kept)
  output/                 build results (git-ignored). Disposable. Includes
                          output/libgit2/ (the libgit2 package and, per RID,
                          the library under its link name and loader name)


WHICH README TO READ
--------------------------------------------------------------------------------
  Building on Linux (x86_64)   -> linux/README.txt
  Building on Windows x64      -> windows/README.txt
  Building on a Mac            -> macos/README.txt (Apple Silicon: both slices)


dx NEEDS libgit2 TO START
--------------------------------------------------------------------------------
dx is linked against libgit2 as a shared library and the operating system's
loader resolves it when dx STARTS. Without it dx does not run at all - not
`dx --version`, not `dx build`, no command - whether or not the command uses
git. (Measured on linux-x64: exit 127, "error while loading shared libraries:
libgit2-49e408b.so.1.9: cannot open shared object file: No such file or
directory"; the same under qemu for linux-arm64.) Whoever starts dx must make
the library findable:

  Linux     the library under its SONAME, libgit2-49e408b.so.1.9, in a folder
            on LD_LIBRARY_PATH (the package's file is libgit2-49e408b.so: a
            copy or symbol link under the SONAME is needed). dx has no
            RPATH/RUNPATH.
  macOS     the library under the name its install name asks for,
            @rpath/libgit2-49e408b.1.9.dylib - the file libgit2-49e408b.1.9.dylib
            (the package's file is libgit2-49e408b.dylib) - in a folder on
            DYLD_LIBRARY_PATH (dx has no LC_RPATH). macOS removes DYLD_*
            variables when it starts a protected program such as /bin/sh or
            /usr/bin/env, so set it for dx itself. VERIFIED 2026-11-22 on
            both slices (without it: exit 134, dyld "Library not loaded").
  Windows   git2-49e408b.dll beside dx.exe or in a folder on PATH. VERIFIED
            2026-11-22 (without it: exit 0xC0000135, STATUS_DLL_NOT_FOUND).

The library itself is libgit2 1.9.7 from CodeBrix.LibGit2.MitLicenseForever
(LIBGIT2_PACKAGE_VERSION in linux/pins.env); it is not part of the dx files
this folder produces, and it carries its own licence files in that package.
The FrontendBuilder library depends on that package and SUPPLIES the library
on every start of dx (build, serve, the prerequisite check): it copies the
package's file under the name dx asks for into <DX_HOME>/libgit2 and puts
that folder first on the variable above, for dx's process only. dx STARTED
DIRECTLY - from a shell, a script, an editor - needs the same setting by
hand; after one build through the library, <DX_HOME>/libgit2 is such a
folder (see DX NEEDS libgit2 in the root AGENT-README.txt).


THE FIVE RUNTIME IDENTIFIERS
--------------------------------------------------------------------------------
  RID          built by                     shipped files            status
  -----------  ---------------------------  -----------------------  -------
  linux-x64    linux/build.sh x64           dx, wasm-bindgen         BUILT
  linux-arm64  linux/build.sh arm64 (cross) dx, wasm-bindgen         BUILT
  win-x64      windows/build.dx.ps1         dx.exe, wasm-bindgen.exe BUILT
  osx-arm64    macos/build.sh arm64         dx, wasm-bindgen         BUILT
  osx-x64      macos/build.sh x64           dx, wasm-bindgen         BUILT

BUILT = built here from the current sources, gated and adopted into
../../native_libraries/<rid>/. BEHIND = built and adopted, but from older
sources: the dx there must be rebuilt.
The current sources hold THIRTEEN dioxus-cli patches and SEVEN splitter
patches, dx linked against libgit2. Which RIDs' adopted programs carry which
patches (BUILD-PROVENANCE.txt):
  linux-x64, linux-arm64   ALL of them: dioxus-cli 0001 to 0013, splitter
                           0001 to 0007 - built 2026-11-27 on the x86_64
                           laptop, each RID twice, byte-identical
  osx-arm64, osx-x64       ALL of them (dx): built 2026-11-27 (Pacific time)
                           on the owner's Apple Silicon Mac, each slice once;
                           osx-x64's functional gate run from an x86_64 shell
                           under Rosetta 2, not yet run on an Intel Mac.
                           wasm-bindgen (unpatched, its source unchanged) is
                           the first build's, of 2026-11-22
  win-x64                  ALL of them (dx.exe): built 2026-11-27 (Pacific
                           time) on the owner's Windows x64 machine, once,
                           from scratch. wasm-bindgen.exe (unpatched, its
                           source unchanged) is the first build's, of
                           2026-11-22
All five platforms' packages carry their programs.

NOT SUPPORTED, and neither built nor scripted: linux-riscv64, win-arm64,
android-*.


WHAT TRAVELS WITH THE PROGRAMS
--------------------------------------------------------------------------------
Every ../../native_libraries/<rid>/ folder holds, and the package for that RID
ships under tools/frontend-tools/<rid>/ (copied into a consuming build's output
as frontend-tools/<rid>/ - a contract the FrontendBuilder library relies on):

  dx, wasm-bindgen (.exe on Windows)  the programs, stripped
  LICENSE-Dioxus.txt                  licences/LICENSE-Dioxus.txt
  LICENSE-WasmBindgen.txt             wasm-bindgen-cli/LICENSE-APACHE, verbatim
  THIRD-PARTY-NOTICES-Dioxus.txt      notices/, generated
  THIRD-PARTY-NOTICES-WasmBindgen.txt notices/, generated

and, in the repository only (not packed), dx.provenance.txt and
wasm-bindgen.provenance.txt (dx.exe.provenance.txt ... on Windows). The names
are never plainly LICENSE: these files land in a consuming application's
output folder. The list is LICENCE_FILES in linux/pins.env; every build copies
it beside the programs and the gate fails if a file is missing.


NOTICES
--------------------------------------------------------------------------------
The two THIRD-PARTY-NOTICES-*.txt files that travel with the programs list
every crate compiled into the program on at least one of the five platforms -
name, version, declared licence, the licence it is used under, crates.io
source, checksum - and every distinct licence text once.
THIRD-PARTY-NOTICES-ToolSources.txt records every crate in sources/vendor the
same way. All three are written by the repository's maintainer tool (its
notices generator is CodeBrix.RustTools'), from the same license audit that
gates the distributed programs:

    cd libs/CodeBrix.WebPlatform.LibraryChecker      (from the repository root)
    dotnet run -- notices --write

`LibraryChecker notices` FAILS when they are out of date. After regenerating,
copy the two that travel with the programs into every
../../native_libraries/<rid>/ folder (the host-tools tests check the copies),
or rebuild. Edit only the hand-written openings in notices/.

The licence rules for what may be compiled into these programs are in
tools/licensing/license-allowlist.json (only the repository owner edits it),
section compiledIntoTheDistributedTools: the "everywhere" tier, plus MPL-2.0
for thirteen named crates (dx only; smartstring, which came back with dx's
project templates, is the thirteenth), CDLA-Permissive-2.0 for webpki-roots,
the LLVM exception, and the reviewed caniuse data. The patches and the vendored
tree are checked by the same tool (`LibraryChecker patches`, `vendor`).


ADOPTING A BUILT BINARY (any platform)
--------------------------------------------------------------------------------
  1. The build must have passed its gate (output/<rid>/GATE.txt has no
     [FAIL]; on Windows and macOS the functional gate must not be SKIPPED).
  2. Copy the two programs and the four files of LICENCE_FILES from
     output/<rid>/ to ../../native_libraries/<rid>/. libgit2 is NOT among
     them: output/libgit2/ is build scratch, and the library reaches a
     consuming build through the CodeBrix.LibGit2 package, which the
     FrontendBuilder library depends on.
  3. Write <program>.provenance.txt beside each program from
     output/<rid>/BUILD-INFO.txt (model: ../../native_libraries/linux-x64/
     dx.provenance.txt).
  4. Add an ADOPTED block at the top of BUILD-PROVENANCE.txt for the build
     and mark in that RID's older section what it replaced.
  5. Build the package (dotnet build -c Release
     src/CodeBrix.WebPlatform.FrontendBuilder.<Platform>) and run
     tests/CodeBrix.WebPlatform.FrontendBuilder.HostTools.Tests ON THAT
     PLATFORM: it checks the programs are in the output, report the expected
     versions, and that the notices are the generated ones.


CHECKING A SPLIT BUILD (after a change to the splitter or to dx's split path)
--------------------------------------------------------------------------------
The functional gate builds an UNSPLIT frontend only. A dx whose bundle
splitting changed is checked by hand as well:
  1. The opt-in build test of the FrontendBuilder tests (TESTING in the root
     MAINTAINER-README.txt): its split tests build a small split frontend
     with a threshold of one byte (several chunks; the same files twice) and
     with a limit of one (one chunk), and check the piece file names. To run
     them with a dx before it is adopted, name the folder that holds it and
     wasm-bindgen in CODEBRIX_WEBPLATFORM_DX_BUILD_TESTS_DX_FOLDER.
  2. split-check on the built sites - that split frontend's, and a split
     build of the ApplicationSplitter sample's sales application (with the
     current threshold, and with DX_WASM_SPLIT_CHUNK_THRESHOLD lowered to get
     many chunks when dx is run by hand; delete the frontend's target/dx
     first - dx never cleans its output folder, so an earlier build's chunks,
     pieces and loader would stay beside the new ones and split-check would
     refuse the folder: "2 loaders". BuildAsync deletes target/dx before
     every build, and replaces the application's part of frontend-built/, so
     a platform build never ships such files):
         cd split-check
         CARGO_TARGET_DIR=<a folder outside the repository> \
             cargo build --release --offline --locked
         <that folder>/release/split-check <the site folder: the one with
             index.html, e.g. backend/<Api>/frontend-built/sales>
     It reads the loader and every WebAssembly file of the site and checks:
     every piece imports the main module's table; no table slot is filled by
     two pieces, nor by a piece and the main module; every call through a
     constant table index reaches a filled slot of the same signature; a
     module that calls into a chunk waits for it, and calls into no other
     module; no two pieces write different bytes at one address. One summary
     line; "PROBLEM ..." lines and exit code 1 when something is wrong.
  3. The sample in headless Chrome (its MAINTAINER-README.txt, CHECKING IN A
     BROWSER) and its reproducibility steps (CHECKING THAT A BUILD IS
     REPRODUCIBLE there).


A ONE-MINUTE TOUR (on Linux)
--------------------------------------------------------------------------------
    cd linux
    ./build.sh x64              # a few minutes once the image exists
    cat ../output/linux-x64/BUILD-INFO.txt
    cd ../output && sha256sum -c SHA256SUMS
================================================================================
