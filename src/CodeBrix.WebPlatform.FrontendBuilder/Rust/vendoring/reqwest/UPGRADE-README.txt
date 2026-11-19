================================================================================
UPGRADE NOTES: reqwest (HTTP client)
================================================================================

Read the generic procedure, UPGRADING A VENDORED LIBRARY in the
MAINTAINER-README.txt at the repository root, first. This file holds what is
particular to this library.

WHERE IT COMES FROM
-------------------
The `reqwest` crate on crates.io (https://crates.io/crates/reqwest), from
https://github.com/seanmonstar/reqwest. Licensed MIT OR Apache-2.0. Not
patched.

HOW IT IS USED
--------------
The shared crate makes every REST call through it, with `default-features =
false` and only the `json` feature (../codebrix-webplatform/Cargo.toml).
Compiled to WebAssembly it has no network stack of its own: it calls the
browser's fetch, and the browser handles HTTPS and sends the session cookies.

WHAT IT IS TIED TO
------------------
Dioxus depends on reqwest too. The shared crate must name the SAME release
line Dioxus uses, so that cargo resolves one copy. Naming a newer line would
vendor and compile a second reqwest and its dependencies.

HOW TO UPGRADE
--------------
Do not upgrade it on its own. When Dioxus is upgraded, read which reqwest
line the new Dioxus crates depend on (the platform's Cargo.lock,
../platform-crates/Cargo.lock) and set the version requirement in the shared
crate's Cargo.toml to that line.

THINGS TO WATCH
---------------
  - In the browser a request URL must be absolute; the shared crate builds
    it from the page's origin.
  - Leave the default features off: they enable TLS and other native-only
    code that a browser build never uses but that cargo would still resolve
    and vendor.

================================================================================
END OF UPGRADE NOTES
================================================================================
