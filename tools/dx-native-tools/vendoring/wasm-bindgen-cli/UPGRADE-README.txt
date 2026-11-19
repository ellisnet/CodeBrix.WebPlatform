================================================================================
UPGRADE NOTES: wasm-bindgen-cli (the `wasm-bindgen` program)
================================================================================

Read the generic procedure, UPGRADING A VENDORED LIBRARY in the
MAINTAINER-README.txt at the repository root, first. This file holds what is
particular to this library.

WHERE IT COMES FROM
-------------------
The `wasm-bindgen-cli` crate on crates.io
(https://crates.io/crates/wasm-bindgen-cli), from the wasm-bindgen project
(https://github.com/wasm-bindgen/wasm-bindgen). Licensed MIT OR Apache-2.0.
The archive beside this file is the pristine source and
tools/dx-native-tools/sources/wasm-bindgen-cli is exactly that source: this
library is NOT patched.

WHAT IT DOES HERE
-----------------
After cargo compiles the frontend to WebAssembly, `dx` runs this program on
the result. It writes the JavaScript glue that loads the WebAssembly file and
carries every call between Rust and the browser. That generated glue is the
JavaScript a platform application cannot do without.

WHAT IT IS TIED TO
------------------
  - Its version must EXACTLY equal the version of the `wasm-bindgen` crate in
    the platform's Cargo.lock
    (src/CodeBrix.WebPlatform.FrontendBuilder/Rust/platform-crates). The
    program refuses a WebAssembly file built against any other version. The
    crate's version is chosen by cargo when `dioxus` is upgraded, so this
    tool usually has to move whenever Dioxus does.
  - It shares tools/dx-native-tools/sources/vendor with the Dioxus CLI.
  - WASM_BINDGEN_VERSION and WASM_BINDGEN_EXPECTED_VERSION in
    tools/dx-native-tools/linux/pins.env name this version, and so do the
    host-tools packages' HostTools.WasmBindgenVersion constants.

HOW TO UPGRADE
--------------
 1. After upgrading the platform's crates, read the `wasm-bindgen` version
    from the platform's Cargo.lock.
 2. Download https://static.crates.io/crates/wasm-bindgen-cli/wasm-bindgen-cli-<that version>.crate
    into this folder, check its SHA-256 against crates.io, write the .sha256
    file, and remove the old archive and its .sha256.
 3. Replace tools/dx-native-tools/sources/wasm-bindgen-cli with the unpacked
    archive. The archive carries its own Cargo.lock; keep it.
 4. Re-vendor the build tools (`LibraryChecker revendor-tools`), run
    `LibraryChecker`, regenerate the notices, update the pins and rebuild the
    programs (tools/dx-native-tools/README.txt).

THINGS TO WATCH
---------------
  - The crate also builds `wasm-bindgen-test-runner` and `wasm2es6js`; only
    `wasm-bindgen` is used.
  - Do not set `strip = true` in a frontend's release profile: this program
    needs the custom sections the linker writes. `wasm-opt` shrinks the file
    afterwards.

================================================================================
END OF UPGRADE NOTES
================================================================================
