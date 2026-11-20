================================================================================
UPGRADE NOTES: dioxus (the frontend framework)
================================================================================

Read the generic procedure, UPGRADING A VENDORED LIBRARY in the
MAINTAINER-README.txt at the repository root, first. This file holds what is
particular to this library.

WHERE IT COMES FROM
-------------------
The `dioxus` crate on crates.io (https://crates.io/crates/dioxus), from the
Dioxus project (https://github.com/DioxusLabs/dioxus). Licensed
MIT OR Apache-2.0. It is NOT patched: its source, and that of every crate it
pulls in, is in ../vendor (the platform's crates, which the FrontendBuilder
package ships) exactly as published.

HOW IT IS USED
--------------
The platform pins an exact version, with the default features off, in two
manifests: the shared crate's (../codebrix-webplatform/Cargo.toml, "minimal"
only) and the platform's crate set (../platform-crates/Cargo.toml), which
names every feature an application may switch on and so decides what is
vendored. The features offered, and why:
  - minimal   the macro, HTML elements, signals, hooks and launch
  - web       the browser renderer
  - router    client-side routing
  - document  page title and stylesheet links
  - mounted   access to mounted elements
  - asset     the asset!() macro, which `dx` resolves at build time
  - wasm-split  splitting the wasm bundle (brings in wasm-splitter,
              wasm-split-macro and async-once-cell)
The `devtools` feature (hot reload) is offered too: an application reaches it
through a `devtools` feature of its own, which no shipping build enables.
Not used, and not to be added: fullstack, server, ssr, liveview, desktop,
mobile, native. The server is ASP.NET; the frontend talks REST.

Only stable releases are used. Never take an alpha, beta or release
candidate. Write 0.7-style code only: components are #[component] functions
with their props as arguments, state is held in signals; the old
cx / Scope / use_state API does not exist in this version.

WHAT IT IS TIED TO
------------------
  - `dioxus-cli` (tools/dx-native-tools/sources/dioxus-cli, the `dx` of the
    host-tools packages) must be the same version.
  - `wasm-bindgen-cli` must equal the `wasm-bindgen` crate cargo resolves.
  - `reqwest` must stay on the release line Dioxus itself depends on.
  - `web-sys`, `js-sys`, `wasm-bindgen` and `wasm-bindgen-futures`, as the
    manifests name them, are crates Dioxus already depends on; let cargo
    keep them at the versions Dioxus resolves to.

HOW TO UPGRADE
--------------
 1. Change the pinned `dioxus` version in both manifests (the shared crate's
    and the crate set's).
 2. Re-resolve and re-vendor the platform's crates (`LibraryChecker revendor`,
    which needs network access to crates.io for this one step), and let
    cargo re-resolve the shared crate's own Cargo.lock.
 3. Run `LibraryChecker`. A Dioxus upgrade usually moves dozens of other
    crates; any new license goes to the repository owner.
 4. Upgrade `dioxus-cli` and `wasm-bindgen-cli` to match (their own notes,
    in tools/dx-native-tools/vendoring).
 5. Build the solution (the package re-packs the crates), run the shared
    crate's tests, refresh the sample's packages and build and test it, run
    the Node-free proof build, and regenerate the notices.

THINGS TO WATCH
---------------
  - dioxus-web uses the browser's `Location` binding in its router history
    but only enables it under its `devtools` feature. An application's
    frontend names `web-sys` with the `Location` feature for that reason (the
    shared crate does too). If a new
    version fixes this the feature can stay; it does no harm.
  - Dioxus's own dependencies can include pre-release crates (for example
    `const-serialize`). Those are Dioxus's choice, pinned by its manifest;
    the "stable releases only" rule is about the versions this project picks.
  - Trimming features changes Cargo.lock, and so what is vendored. After any
    feature change, re-vendor and re-run the checks.

================================================================================
END OF UPGRADE NOTES
================================================================================
