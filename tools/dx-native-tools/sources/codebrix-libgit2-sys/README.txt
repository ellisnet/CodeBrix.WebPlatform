================================================================================
codebrix-libgit2-sys - the declarations-only stand-in for the libgit2-sys crate
================================================================================

WHAT THIS IS
--------------------------------------------------------------------------------
The Dioxus CLI (dx) reaches git through the `git2` crate, which builds on the
crates.io crate `libgit2-sys`. That crate carries libgit2's C source (GPL-2.0
with a linking exception) and compiles it. This repository never carries
libgit2's source, in any form.

This folder is a crate of our own that takes `libgit2-sys`'s place:

  * the same crate name (libgit2-sys, library libgit2_sys), the same 0.18.3
    version, the same five features and the same public Rust API as
    libgit2-sys 0.18.3+1.9.2, the release dx's locked git2 0.20.4 uses. Its
    version is 0.18.3+1.9.7: the build metadata names the libgit2 release it
    links, and cargo ignores build metadata when it matches versions;
  * NO C source and nothing compiled. lib.rs only DECLARES libgit2's functions
    and types; build.rs only tells cargo where the prebuilt library is
    (CODEBRIX_LIBGIT2_LIB_DIR) and what it is called (git2-49e408b);
  * it LINKS the prebuilt libgit2 1.9.7 (commit 49e408b) that the published
    NuGet package CodeBrix.LibGit2.MitLicenseForever ships, as a shared
    library. The build scripts in ../../linux, ../../windows and ../../macos
    take it from that package (pins.env names the version and the checksums);
    it is never committed here;
  * the features https, ssh, vendored, vendored-openssl and zlib-ng-compat
    exist so that git2's and cargo-generate's requests still resolve, and are
    EMPTY. Upstream uses them to compile libgit2 or to pull in openssl-sys,
    libssh2-sys and libz-sys; this crate depends on `libc` only.

dx's Cargo.toml substitutes it with
    [patch.crates-io.libgit2-sys]
    path = "../codebrix-libgit2-sys"
(patch 0002-libgit2-sys-stand-in.patch; see
../../vendoring/dioxus-cli/PATCH-NOTES.txt). Because of that substitution
`cargo vendor` never takes the crates.io libgit2-sys, so it is not in
../vendor either.

A program linked this way NEEDS the library at run time and does not start
without it (see ../../README.txt, "dx NEEDS libgit2 TO START").


FILES
--------------------------------------------------------------------------------
  Cargo.toml        ours: the package, the empty features, the `libc`
                    dependency
  build.rs          ours: link directives only
  lib.rs            upstream's lib.rs with the three changes below
  LICENSE-MIT       upstream's, verbatim (sha256 378f5840b258e2779c39418f3f2d7b
                    2ba96f1c7917dd6be0713f88305dbda397)
  LICENSE-APACHE    upstream's, verbatim (sha256 a60eea817514531668d7e0076573
                    1449fe14d059d3249e0bc93b36de45f759f2)
  README.txt        this notice

No file of libgit2 itself is here: no C source, no header, nothing from
upstream's libgit2/ folder, and none of upstream's build.rs.


NOTICE - WHERE lib.rs COMES FROM
--------------------------------------------------------------------------------
lib.rs is the file lib.rs of the crate libgit2-sys 0.18.3+1.9.2 as published
on crates.io (https://crates.io/crates/libgit2-sys; archive sha256
c9b3acc4b91781bb0b3386669d325163746af5f6e4f73e6d2d630e09a35f3487; upstream
repository https://github.com/rust-lang/git2-rs, commit
591952f8cf344b2676db16b4f67c68875713f2e8, folder libgit2-sys). The file as
published has sha256
116534b6e9c32b96d2dacb9c0f55fab8fe5422c9bc5a80b37877339f677aeab6.

  Copyright (c) 2014 Alex Crichton (LICENSE-MIT); crate authors Josh Triplett
  and Alex Crichton.
  Licensed under the MIT license or the Apache License, Version 2.0, at your
  option (LICENSE-MIT, LICENSE-APACHE beside this file). This crate is offered
  under the same terms, "MIT OR Apache-2.0".

Only lib.rs was taken. Upstream's build.rs (which compiles libgit2), its
Cargo.toml, and its libgit2/ folder (libgit2's C source) were not.


THE CHANGES TO lib.rs (each marked "CodeBrix change:" in the file)
--------------------------------------------------------------------------------
  1. The top of the file: the line `extern crate libz_sys as libz;` and the
     line `use libssh2_sys as libssh2;` (under the `ssh` feature) are
     removed; the stand-in depends on neither crate. A comment says where the
     file comes from.
  2. `GIT_REBASE_NO_OPERATION`: `usize::max_value()` becomes `usize::MAX`, the
     same value; the old form is deprecated, and cargo shows the warnings of
     a local crate (it hides those of a crates.io dependency).
  3. The end of the file: `openssl_init()` (which upstream implements with
     `openssl_sys::init()` on Unix with the `https` feature) and `ssh_init()`
     (`libssh2::init()` with the `ssh` feature) are kept with the same names
     and visibility and do nothing. The prebuilt libgit2 loads and
     initialises the system's OpenSSL itself when it first needs it, has zlib
     built in, and does SSH by running the `ssh` program.

Everything else - every constant, type, struct, enum and function
declaration - is upstream's, unchanged. `vendored()` still answers
cfg!(libgit2_vendored), which this build never sets, so it answers false.


ON AN UPGRADE OF dx
--------------------------------------------------------------------------------
When dx's git2 moves to another libgit2-sys release, take that release's
lib.rs (from the crates.io archive, opened in a scratch folder outside the
repository - the archive holds libgit2's source and must not be put here),
re-make the three changes, update the version in Cargo.toml and the facts in
this file, and check that the prebuilt libgit2 of the CodeBrix.LibGit2
package is the libgit2 release that libgit2-sys version targets (its build
metadata, "+1.9.x"). See ../../vendoring/dioxus-cli/UPGRADE-README.txt.
================================================================================
