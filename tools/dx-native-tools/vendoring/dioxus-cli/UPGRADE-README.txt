================================================================================
UPGRADE NOTES: dioxus-cli (the `dx` build tool)
================================================================================

Read the generic procedure, UPGRADING A VENDORED LIBRARY in the
MAINTAINER-README.txt at the repository root, first. This file holds what is
particular to this library.

WHERE IT COMES FROM
-------------------
The `dioxus-cli` crate on crates.io (https://crates.io/crates/dioxus-cli),
from the Dioxus project (https://github.com/DioxusLabs/dioxus). Licensed
MIT OR Apache-2.0. The archive beside this file is the pristine source;
tools/dx-native-tools/sources/dioxus-cli is that source plus the patches
listed in PATCH-NOTES.txt.

WHAT IT IS TIED TO
------------------
  - Its version must equal the `dioxus` version of the platform's crates
    (src/CodeBrix.WebPlatform.FrontendBuilder/Rust/platform-crates/Cargo.toml
    and the shared crate's Cargo.toml beside it). The tool and the library
    are released together and the tool refuses or misbuilds a mismatched
    project. Upgrade both in the same change.
  - It runs the `wasm-bindgen` program and checks that its version equals the
    `wasm-bindgen` crate in the platform's Cargo.lock; see
    ../wasm-bindgen-cli/UPGRADE-README.txt.
  - It shares tools/dx-native-tools/sources/vendor with the wasm-bindgen CLI.
  - Its git2 builds on the repository's libgit2-sys stand-in
    (tools/dx-native-tools/sources/codebrix-libgit2-sys, patch 0002), whose
    version must satisfy what the new git2 asks for, and whose declarations
    must be those of the libgit2-sys release that git2 version uses. That
    stand-in links the prebuilt libgit2 of CodeBrix.LibGit2.MitLicenseForever
    (LIBGIT2_* in tools/dx-native-tools/linux/pins.env): the libgit2 release
    that libgit2-sys targets (its "+1.9.x" build metadata) and the package's
    must share an ABI (the same 1.x minor version). A git2 that moves to a
    new libgit2 minor version needs a CodeBrix.LibGit2 release with it FIRST.
  - DX_CRATE_VERSION, DX_GIT_SHA and DX_EXPECTED_VERSION in
    tools/dx-native-tools/linux/pins.env name this version, and so do the
    host-tools packages' HostTools.DxVersion constants.
  - Only stable releases are used. Never take an alpha, beta or release
    candidate.

HOW TO UPGRADE
--------------
 1. Download the new archive from
    https://static.crates.io/crates/dioxus-cli/dioxus-cli-<version>.crate
    into this folder, check its SHA-256 against the checksum crates.io shows
    for that version, write the .sha256 file, and remove the old archive and
    its .sha256.
 2. Replace tools/dx-native-tools/sources/dioxus-cli with the unpacked
    archive (keep nothing of the old folder except by re-applying patches).
 3. Re-apply patches 0001 to 0003, 0005 and 0006 to the new source, in
    order (0002 only adds the [patch] section; keep it). A
    patch that no longer applies must be re-made by hand against the new
    code; read its
    entry in PATCH-NOTES.txt for what it has to achieve, and check whether
    upstream has made it unnecessary ("Drop when").
 4. Regenerate the lock file and patch 0004 as its entry describes (start
    from the new version's own Cargo.lock, and switch libgit2-sys to the
    stand-in with `cargo update -p libgit2-sys@<locked version>`). If the new
    git2 uses another libgit2-sys release, renew the stand-in first (its
    README.txt, ON AN UPGRADE OF dx) - open that release's crates.io archive
    in a scratch folder OUTSIDE the repository: it holds libgit2's source.
 5. Re-vendor the build tools (`LibraryChecker revendor-tools`; see the
    root MAINTAINER-README.txt), then run `LibraryChecker`. New crates mean new
    licenses to judge: anything the gate rejects goes to the repository owner
    before it is vendored.
 6. Re-write the patch files from the result, update PATCH-NOTES.txt, and
    regenerate the notices (`LibraryChecker notices --write`).
 7. Update the pins in tools/dx-native-tools/linux/pins.env, rebuild the
    programs for every platform (tools/dx-native-tools/README.txt), adopt
    them into native_libraries/<rid>/, build the sample's frontend with them
    and run the Node-free proof build (`LibraryChecker proof`).

THINGS TO WATCH
---------------
  - Read the new version's src/esbuild.rs, src/tailwind.rs, src/wasm_opt.rs
    and src/wasm_bindgen.rs for what it downloads or expects on PATH; the
    `no-downloads` feature must still cover every download.
  - Read src/settings.rs and src/logging.rs to confirm that the
    `disable-telemetry` feature still stops every upload path.
  - Search the new Cargo.toml for dependencies that bundle third-party
    source (crates ending in -sys); the license gate scans their folders,
    but a new GPL-family dependency usually means a new patch.
    `LibraryChecker vendor` fails if a crates.io libgit2-sys is vendored, if
    the stand-in holds any C code, or if libgit2's source shows up anywhere.
  - Read src/build/web.rs where the main module is shrunk: patch 0006 reads
    DX_SKIP_WASM_OPT there. If upstream moves or reworks that step, re-make
    the patch so a release build still skips wasm-opt on request, and check
    whether upstream now offers that itself ("Drop when").
  - The git crates bring openssl-sys on Linux (git2's `https` feature, which
    cargo-generate turns on). Check that git2 still never calls it - the
    Linux gate fails if dx imports an OpenSSL symbol or NEEDS libssl /
    libcrypto - and whether its version still accepts the OpenSSL headers of
    OPENSSL_DEVEL_VERSION in pins.env.
  - `dx` picks the Tailwind version by the files it finds (a `tailwind.css`
    at the crate root means v4) and, with downloads off, runs whatever
    `tailwindcss` is on PATH. The FrontendBuilder library puts the packaged
    one (the CodeBrix.WebPlatform.Styling.Tailwind4 package of the machine)
    first on that PATH.

================================================================================
END OF UPGRADE NOTES
================================================================================
