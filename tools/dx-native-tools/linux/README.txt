================================================================================
tools/dx-native-tools/linux - building dx and wasm-bindgen for linux-x64 and
                              linux-arm64
================================================================================

>>> STATUS 2026-11-18: both RIDs BUILT, gated and adopted (see
    ../BUILD-PROVENANCE.txt). Written and first run on the x86_64 LMDE 7
    laptop with podman 5.4.2: linux-x64 natively, linux-arm64 CROSS-compiled
    in the same x86_64 image and run under qemu. Two from-scratch runs gave
    byte-identical programs.
    STATUS 2026-11-19: both RIDs REBUILT from the six-patch sources (dx linked
    to libgit2 through the stand-in; image rebuilt with the OpenSSL headers);
    every gate passed, the functional gate's new checks included; two linux-x64
    runs byte-identical; wasm-bindgen unchanged. ADOPTED into
    ../../../native_libraries/ on 2026-11-19 (../BUILD-PROVENANCE.txt).
    STATUS 2026-11-19, later: both RIDs REBUILT again after patch 0003 was
    extended (without esbuild, a `--wasm-split` build's loader is copied
    beside the glue file under a hashed name); every gate passed, the
    functional gate included (functional-gate.sh's comparison with today's
    tools fixed to give the reference dx the libgit2 library); two linux-x64
    runs byte-identical; the new dx builds the unsplit WorkSite frontend
    byte-identically to the dx it replaces; wasm-bindgen and the licence
    files unchanged. ADOPTED into ../../../native_libraries/ on 2026-11-19
    (../BUILD-PROVENANCE.txt).
    STATUS 2026-11-19, fourth rebuild (the programs shipped now): both RIDs
    REBUILT after the bundle splitter's output was made reproducible (its
    patch 0003: a split build gives the same files, byte for byte, from the
    same source; still SEVEN dioxus-cli patches, now THREE splitter
    patches); every gate passed, the linux-x64 functional gate included
    (the new dx builds the unsplit WorkSite frontend byte-identically to the
    dx it replaces); each RID built twice, byte-identical both times
    (linux-x64 runs 19 and 20, linux-arm64 runs 21 and 22); wasm-bindgen and
    the four licence and notices files unchanged. ADOPTED into
    ../../../native_libraries/ on 2026-11-19 (../BUILD-PROVENANCE.txt).
    STATUS 2026-11-19, third rebuild (replaced by the fourth): both RIDs
    REBUILT after the bundle splitter's call graph was fixed (patch 0007 and
    the patched copy ../sources/wasm-split-cli: SEVEN patches); every gate
    passed, the linux-x64 functional gate included; two linux-x64 runs
    byte-identical, and a later linux-x64 re-build the same day matched the
    adopted files byte for byte; linux-arm64 built once (its reproducibility
    not re-checked); wasm-bindgen unchanged; THIRD-PARTY-NOTICES-Dioxus.txt
    regenerated. ADOPTED into ../../../native_libraries/ on 2026-11-19
    (../BUILD-PROVENANCE.txt). <<<

NOTHING HERE INSTALLS ANYTHING ON YOUR MACHINE. Every script checks what it
needs, and if something is missing it names it, prints the command that
installs it, and stops.


PREREQUISITES (Debian-based Linux, x86_64)
--------------------------------------------------------------------------------
  1. A container engine - podman (preferred) or docker:
         sudo apt install podman
  2. qemu user-mode emulation, for the linux-arm64 gate (it RUNS the
     cross-built programs in the pinned aarch64 image):
         sudo apt install qemu-user-static binfmt-support
     Verify:  ls /proc/sys/fs/binfmt_misc | grep qemu-aarch64
  3. unzip, to take libgit2 out of its NuGet package, and curl, to download
     that package when the local NuGet cache does not have it:
         sudo apt install unzip curl
  4. For the linux-x64 FUNCTIONAL gate only (it builds the WorkSite frontend,
     its deliberate subject, on the host with the new programs): Rust through
     rustup, 1.100.0 or newer as the default toolchain, with the WebAssembly
     target (the commands are in BUILD MACHINES in the root
     MAINTAINER-README.txt), the Styling package's tailwindcss and the
     WasmShrink package's wasm-opt in the local NuGet cache (a restore of the
     repository's solution puts both there; or TAILWIND_DIR=<folder>,
     WASM_OPT_DIR=<folder>; a Tailwind installed on the machine is never
     used), the sample's frontend
     materialized once (platform/ with the platform's crates, src/generated/
     and .cargo/), and git (only to make the local template repository that
     `dx new` is tested with):
         sudo apt install git
     SKIP_FUNCTIONAL_GATE=1 skips it.
  5. Disk: about 3 GB for the derived image, about 3 GB of cargo target folder
     per run inside the container (discarded when it exits).

  NOT REQUIRED ON THE HOST, AND DELIBERATELY SO for the build itself: Rust,
  clang, a cross compiler, the OpenSSL headers. They live in the derived
  image, at pinned versions. (Building dx on a bare Debian host instead would
  need `sudo apt install libssl-dev` for the headers - see THE IMAGE.)


libgit2: LINKED, NOT BUILT
--------------------------------------------------------------------------------
dx links libgit2 1.9.7 through the declarations-only stand-in
../sources/codebrix-libgit2-sys. The library is the one the published package
CodeBrix.LibGit2.MitLicenseForever (LIBGIT2_PACKAGE_VERSION in pins.env)
ships. build.sh runs fetch-libgit2.sh before each build: it takes the package
from ../output/libgit2/ (an earlier run), else the local NuGet cache, else
nuget.org (THE NETWORK), checks it against LIBGIT2_NUPKG_SHA256, and unpacks
runtimes/<rid>/native/libgit2-49e408b.so, checked against
LIBGIT2_SHA256_LINUX_<ARCH>, into ../output/libgit2/<rid>/ with a symbol link
under its SONAME, libgit2-49e408b.so.1.9. The build container sees that
folder READ-ONLY as /libgit2; the stand-in's build.rs links it
(CODEBRIX_LIBGIT2_LIB_DIR). It is never committed.

The built dx does NOT START without that library (exit 127, "error while
loading shared libraries: libgit2-49e408b.so.1.9: cannot open shared object
file"): run it with a folder holding the library under its SONAME on
LD_LIBRARY_PATH. The gates do exactly that, and run dx once without it to
record the failure.


THE IMAGE
--------------------------------------------------------------------------------
ONE derived image, codebrix-dx-build-x86_64 (Containerfile.x86_64), builds both
RIDs. It adds to the digest-pinned quay.io/pypa/manylinux_2_28_x86_64 base
(AlmaLinux 8, GCC 14.2.1, glibc 2.28 - the same digest WasmShrink, LibGit2 and
WebGpu pin):
  * clang, lld, llvm, `file` and cpio (dnf);
  * the OpenSSL DEVELOPMENT HEADERS, exactly OPENSSL_DEVEL_VERSION (pins.env,
    the release both base images carry): openssl-devel for x86_64 (dnf), and
    the distribution's aarch64 openssl-devel + openssl-libs of the same
    release unpacked into the cross sysroot. WHY: dx's project templates
    (cargo-generate) turn on git2's `https` feature, which on Linux makes git2
    depend on the openssl-sys crate. git2 never calls it, so nothing of
    OpenSSL is compiled into or linked by dx - the gate checks NEEDED and the
    imported symbols - but openssl-sys's build script reads these headers and
    stops without them. container-build.sh passes their folders as
    <TRIPLE>_OPENSSL_INCLUDE_DIR / _LIB_DIR;
  * rustup and EXACTLY Rust 1.100.0 (RUST_TOOLCHAIN), profile minimal, with
    the aarch64-unknown-linux-gnu target;
  * the aarch64 cross sysroot: glibc, kernel headers and GCC support files
    exported from the PINNED manylinux_2_28_aarch64 image by
    export-sysroot.sh, and aarch64-linux-gnu-gcc/g++/ar wrappers around
    clang --target=aarch64-redhat-linux --sysroot=... -fuse-ld=lld.
It ends by probing all of it (a C program cross-linked for aarch64, a Rust
program for both targets) and records the versions in toolchain.txt, which
every BUILD-INFO.txt copies. Building it, and fetching the libgit2 package
when it is not in the NuGet cache, are the ONLY steps that use the network.
To rebuild it:   FORCE_IMAGE_REBUILD=1 ./build.sh   (an image built before the
OpenSSL headers were added is refused by container-build.sh with that advice)


WHY linux-arm64 IS CROSS-COMPILED, NOT BUILT UNDER qemu
--------------------------------------------------------------------------------
dx is several hundred crates. Under qemu every rustc process would be emulated, which
turns a ninety-second build into hours. Rust cross-compiles by design; the only
thing that must be right is the target's C library, and the cross linker links
against the sysroot of the pinned aarch64 image itself, so the glibc floor is
that image's 2.28 - what a native build there gives. The C code in dx (ring,
zstd) is compiled by clang for aarch64 against the same sysroot. This is the
route CodeBrix.WebGpu takes for its Rust natives. The programs are then RUN
(--version, ldd) in the pinned aarch64 image under qemu; qemu is not silicon,
so run the managed tests on real arm64 hardware before a release.


USAGE
--------------------------------------------------------------------------------
    cd tools/dx-native-tools/linux
    ./build.sh                  # both RIDs, x64 first
    ./build.sh x64              # or arm64

  Environment variables: CONTAINER_ENGINE=docker, FORCE_IMAGE_REBUILD=1,
  SKIP_FUNCTIONAL_GATE=1, WASM_OPT_DIR=<folder>, REFERENCE_TOOLS_DIR=<folder>
  (the "today's tools" of the functional gate).

  Timings (24-core laptop): image build about 3 minutes, once; linux-x64 about
  4 minutes end to end with the functional gate (it builds the frontend three
  times); linux-arm64 about 2 minutes.

  DO NOT RUN TWO COPIES OF build.sh AT ONCE (both rewrite ../output/SHA256SUMS).


THE BUILD, IN ONE PARAGRAPH
--------------------------------------------------------------------------------
container-build.sh checks the network is off, that rustc is exactly
RUST_TOOLCHAIN, that the image has the OpenSSL headers, that the sources are
the pinned versions (and dx's .cargo_vcs_info.json names DX_GIT_SHA), that
/src is read-only, and that /libgit2/<rid> holds the pinned libgit2 (checksum
and SONAME). Then, in /src/<tool>, with CARGO_TARGET_DIR in the container's
/tmp:

    cargo build --release --locked --offline --target <triple> [--features ...]

with RUSTFLAGS="--remap-path-prefix=/src=tool-sources --remap-path-prefix=
<target>=target --remap-path-prefix=/usr/local/cargo=cargo-home -C link-arg=
-Wl,--build-id=sha1", CFLAGS_<triple>="-ffile-prefix-map=..." for the C code,
DIOXUS_CLI_GIT_SHA=<DX_GIT_SHA> (dx's build.rs otherwise bakes in the
commit of whatever .git folder is above the source - THIS repository's),
CODEBRIX_LIBGIT2_LIB_DIR=/libgit2/<rid> and CODEBRIX_LIBGIT2_LINK_NAME (the
stand-in's build.rs), and <TRIPLE>_OPENSSL_INCLUDE_DIR / _LIB_DIR. The
--target flag keeps RUSTFLAGS away from build scripts and procedural macros.
None of this changes generated code. The programs are then stripped
(llvm-strip --strip-all), the LICENCE_FILES copied beside them, and the gate
run. `cargo tree -e normal` for the target is saved as compiled-in-<tool>.txt.


THE VERIFICATION GATE
--------------------------------------------------------------------------------
Per program, inside the container (network off):
  1. ELF64 and the right machine (x86-64 / AArch64).
  2. Stripped: no .symtab, .strtab or .debug_* section.
  3. No RPATH / RUNPATH, no TEXTREL.
  4. NEEDED only from ALLOWED_DEPS: glibc's libraries and loader, and
     libgcc_s.so.1 - and, for dx, libgit2 under its SONAME
     (LIBGIT2_SONAME_LINUX), which dx MUST need and wasm-bindgen must not.
     libgcc_s IS needed - every Rust program for the gnu targets links it for
     unwinding - and is part of every glibc distribution's base system.
     FORBIDDEN_DEP_PATTERN (OpenSSL, zlib, zstd, libssh2, any other libgit2,
     libstdc++, lzma, bz2, curl) fails the gate, and so does any imported
     OpenSSL symbol (OPENSSL_SYMBOL_PATTERN). ldd must resolve (dx with
     libgit2 on LD_LIBRARY_PATH).
  5. glibc floor (highest GLIBC_ version referenced) <= 2.28 - checked.
  6. No build path in the binary (/src/<tool or vendor>, the target folder,
     /work, /home, /root, /usr/local/cargo|rustup). Rust's own
     /rustc/<commit>/ standard-library paths are counted and recorded, as are
     system header paths (/usr/include/).
  7. `--version` prints exactly DX_EXPECTED_VERSION / WASM_BINDGEN_EXPECTED_
     VERSION (linux-x64 in the container; linux-arm64 by build.sh under qemu).
  8. Every LICENCE_FILES file is present; libgit2's own glibc floor is
     <= 2.28 too.
Then, by build.sh:
  9. linux-x64: both programs run ON THE HOST (its own glibc) - HOST-CHECK.txt;
     linux-arm64: both run in the pinned aarch64 image under qemu, and ldd
     resolves there - QEMU-RUN.txt. Both times dx runs with libgit2 on
     LD_LIBRARY_PATH, and once without it: it must NOT start (exit 127, the
     loader naming libgit2-49e408b.so.1.9) - recorded, not hidden.
 10. linux-x64 FUNCTIONAL gate (functional-gate.sh): the new dx and
     wasm-bindgen, with the WasmShrink package's wasm-opt first on PATH,
     run `dx build --platform web --release --debug-symbols false` on a
     SCRATCH COPY of samples/WorkSite/frontend in a new network namespace
     (unshare --net). It checks exit 0, no "wasm-opt failed", index.html, one
     .wasm with the wasm magic, the glue .js that index.html loads and that
     loads the .wasm, the Tailwind stylesheet, wasm-bindgen's snippets, and
     the same file list as the reference build in the real frontend's target/
     folder. The copy's path is padded to the same length as the real one
     (Rust embeds source paths in the .wasm); the bytes still differ a little
     because dx's asset symbols and the crate's symbol hash are derived from
     the absolute path. Then, on the same copy at the same path:
       - TODAY'S TOOLS: the programs committed in ../../native_libraries/
         linux-x64 build it again, and every site file must be BYTE-IDENTICAL
         to the new tools' output;
       - DX_SKIP_WASM_OPT (patch 0006): a release build with DX_SKIP_WASM_OPT=1
         and no wasm-opt anywhere on PATH or in dx's data folder must exit 0,
         print "Skipping wasm-opt at the caller's request", and give a valid
         .wasm larger than the shrunk one;
       - `dx new` from a LOCAL template: a small git repository made in the
         scratch folder is cloned (file://) and filled in by `dx new
         --template file://... --branch main --yes --vcs none`, with the
         network off and NO git program on PATH - so by libgit2.


OUTPUT (git-ignored)
--------------------------------------------------------------------------------
  ../output/<rid>/dx, wasm-bindgen         the shipped files
  ../output/<rid>/LICENSE-*.txt, THIRD-PARTY-NOTICES-*.txt
  ../output/<rid>/BUILD-INFO.txt           toolchain, pins, sizes (as linked,
                                           stripped, gzip -9), sha256,
                                           build-id, NEEDED, glibc floor, gate
  ../output/<rid>/GATE.txt                 the gate's lines
  ../output/<rid>/compiled-in-<tool>.txt   every package compiled in
  ../output/<rid>/cargo-<tool>.log         cargo's log
  ../output/<rid>/HOST-CHECK.txt, FUNCTIONAL-GATE.txt (x64), QEMU-RUN.txt (arm64)
  ../output/functional-gate/               the functional gate's site and log
  ../output/SHA256SUMS                     every program, one line each

To adopt: ../README.txt, ADOPTING A BUILT BINARY.


BUILDING ON AN ARM64 LINUX MACHINE
--------------------------------------------------------------------------------
build.sh refuses a non-x86_64 host: the image is x86_64 and the aarch64 build
is a cross build from it. On an arm64 machine, use an x86_64 one, or extend
the tooling with an aarch64-native image first (not done; not needed).
================================================================================
