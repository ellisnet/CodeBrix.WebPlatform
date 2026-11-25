================================================================================
EXTRAS-README: CodeBrix.WebPlatform
Samples, tools and other content in this repository that is not part of a NuGet
package
================================================================================

The packable projects of this repository are src/CodeBrix.WebPlatform.Core,
src/CodeBrix.WebPlatform.Backend, src/CodeBrix.WebPlatform.BackendMapper,
src/CodeBrix.WebPlatform.FrontendBuilder and the host-tools projects
src/CodeBrix.WebPlatform.FrontendBuilder.<Platform>. Everything listed below
exists to build, check, test, demonstrate or document them. None of it is
included in a package.


TEST PROJECTS
=============
    tests/CodeBrix.WebPlatform.Core.Tests/
    tests/CodeBrix.WebPlatform.Backend.Tests/
    tests/CodeBrix.WebPlatform.BackendMapper.Tests/
    tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/
    tests/CodeBrix.WebPlatform.FrontendBuilder.HostTools.Tests/
    tests/CodeBrix.WebPlatform.LibraryChecker.Tests/

xUnit v3; run them as described in MAINTAINER-README.txt (TESTING).

    tests/CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend/
    tests/CodeBrix.WebPlatform.BackendMapper.Tests.BrokenBackend/

Two small backends that exist only for the tests to read (BackendMapper's, and
SampleBackend also FrontendBuilder's). They are compiled, never run; every
action body throws. SampleBackend uses each feature the mapper supports (its
request marker and session-refresh action included); BrokenBackend has one of
every problem the mapper reports (its RefreshMistakesController, marked
[WireIgnore], is read on its own by a test, so that the backend still lacks a
session-refresh action). They are also the shortest complete examples of a
declared (and an undeclared) contract, and AGENT-README.txt links to them.

    tests/CodeBrix.WebPlatform.Backend.Tests.SampleBackend/

A small backend that the Backend library's tests start in a test server:
public and signed-in controllers, a hub, a stand-in sign-in scheme, routes
outside /api (to prove that a controller wins over a frontend fallback), and
the backend's own pipeline lines after UsePlatformBackend. It is the shortest
example of a backend that makes the two Backend calls, and AGENT-README.txt
links to it.


THE SHARED RUST CRATE'S OWN TESTS
=================================
    src/CodeBrix.WebPlatform.FrontendBuilder/Rust/codebrix-webplatform/

The crate's source is part of the FrontendBuilder package (it is embedded in
the assembly). Its Rust unit tests - in the source files, and the SignalR
client's in src/signalr/tests.rs - travel with that source but are compiled
only by `cargo test` here, never in an application. Its Cargo.lock and its
.cargo folder are not in the package: they serve `cargo test --offline` in
this repository only. See MAINTAINER-README.txt (TESTING).

The SignalR client's fake hub (src/signalr/testing.rs) is NOT test-only: it is
part of the crate's API, for applications' own native unit tests.


THE MAINTAINER TOOL
===================
    libs/CodeBrix.WebPlatform.LibraryChecker/

Not packable and never published: the repository's licence and vendoring
gate over what the repository holds outside samples/ (the platform's crates,
the tool sources and their crates, the patches, the notices, the no-Node
rule), the re-vendoring commands and the Node-free proof build. See THE
MAINTAINER TOOL in MAINTAINER-README.txt. Its policy file is
tools/licensing/license-allowlist.json (only the repository owner edits it).


THE NATIVE BUILD OF dx AND wasm-bindgen
=======================================
    tools/dx-native-tools/            scripts, pins, gate, records, notices
    tools/dx-native-tools/sources/    the tool sources and their crates
    tools/dx-native-tools/vendoring/  pristine archives, patches, notes
    native_libraries/<rid>/           the adopted programs

How the programs the host-tools packages ship are built, gated and adopted.
Start at tools/dx-native-tools/README.txt.


THE PLATFORM'S CRATES
=====================
    src/CodeBrix.WebPlatform.FrontendBuilder/Rust/platform-crates/
    src/CodeBrix.WebPlatform.FrontendBuilder/Rust/vendor/
    src/CodeBrix.WebPlatform.FrontendBuilder/Rust/vendoring/
    src/CodeBrix.WebPlatform.FrontendBuilder/Rust/notices/

Inside the FrontendBuilder project, but only the archive built from vendor/
and the generated notices are packed: the crate set that decides the
platform's crates, the vendored crates themselves, their upgrade notes, and
the notices header. See MAINTAINER-README.txt (BUILDING, VENDORED SNAPSHOTS,
NOTICES).


THE WORKSITE SAMPLE APPLICATION
===============================
    samples/WorkSite/

A complete application built on the platform, and the test-bed the platform is
proved on: a time tracker with public pages and a signed-in area.

  - a .NET REST backend with nine SignalR hubs, PostgreSQL behind it;
  - a frontend in Rust (Dioxus) compiled to WebAssembly, styled with Tailwind
    and its own typeface (Roboto, with its licence file, served from a
    fixed-name assets folder; see FONTS in AGENT-README.txt), holding only
    application code: it sets the platform up with the one
    generated call and keeps its pages, components, session handling and
    what it does with the hubs;
  - a build program (build/WorkSite.BuildFrontend) that calls the
    FrontendBuilder library to check the machine, materialize the frontend
    and build it with the packaged dx and wasm-bindgen, and adds what is
    WorkSite's own: the containers, the settings file, the development
    certificate and the notices the site serves.

WorkSite is a CONSUMING APPLICATION and holds only what is its own; it vendors
nothing and the repository's licence gate does not check it. Its own rules
(nothing unapproved, the platform's crates only, no shell scripts, and so on)
are that application's choices, not requirements the platform places on
applications. They are written down, with how to build, run and maintain the
sample, in:

    samples/WorkSite/MAINTAINER-README.txt

WorkSite has its own solution (samples/WorkSite/WorkSite.slnx) and is NOT
referenced by CodeBrix.WebPlatform.slnx. It consumes the libraries as
published NuGet packages, from nuget.org, like any other application (THE
SAMPLES' PACKAGES in MAINTAINER-README.txt).

What the sample uses beyond the platform is recorded in
samples/WorkSite/THIRD-PARTY-NOTICES.txt; its site serves that file followed
by the platform's notices.


THE APPLICATIONSPLITTER SAMPLE APPLICATION
==========================================
    samples/ApplicationSplitter/

A small application that proves the platform's frontend applications and
code splitting: ONE backend (a few REST endpoints and one public hub; no
database, no containers, no sign-in) and THREE frontend applications under
frontends/ - home, which has no name and is served at the site root; sales,
served below /sales and built with code splitting; and admin, served below
/admin and built as one WebAssembly file. Its build program
(build/ApplicationSplitter.BuildFrontend: check, generate, build [name],
run, serve [name]) names the three applications; the backend names none and
serves what the build wrote into its frontend-built/ folder, through the
Backend package.

Like WorkSite it is a CONSUMING APPLICATION: it has its own solution
(samples/ApplicationSplitter/ApplicationSplitter.slnx), is NOT referenced by
CodeBrix.WebPlatform.slnx, consumes the libraries as published NuGet
packages, and is not checked by the licence gate. It uses no
third-party material beyond the platform's, so it has no notices file of its
own. How it is built, run and checked:

    samples/ApplicationSplitter/MAINTAINER-README.txt
