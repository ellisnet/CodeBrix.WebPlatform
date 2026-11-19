================================================================================
MAINTAINER-README: the WorkSite sample application (CodeBrix.WebPlatform)
For people (and AI coding agents) changing the sample under samples/WorkSite
================================================================================

The libraries of this repository (src/, libs/, tests/, tools/ and the NuGet
packages) have their own maintainer file, MAINTAINER-README.txt at the
repository root. This file is about the sample only.

PURPOSE AND SCOPE
=================
WorkSite is a complete web application built on CodeBrix.WebPlatform, and the
test-bed the platform is proved on. Functionality is built here first and then
moved out into a library, so that every platform application gets it; the
sample then keeps only what is its own.

  - a .NET REST backend (ASP.NET Core controllers and SignalR hubs) that
    renders no UI,
  - PostgreSQL, which only the backend talks to,
  - a frontend written in Rust with Dioxus, compiled to WebAssembly,
  - Tailwind CSS for all styling.

It consumes the platform the way any application does: its backend references
CodeBrix.WebPlatform.Core, and its build program (build/WorkSite.BuildFrontend)
references CodeBrix.WebPlatform.FrontendBuilder and the host-tools package
for the machine it runs on. There is no contract document: the backend's
controllers and hubs are the contract.

The sample holds APPLICATION CODE ONLY. The owner's split: HOW to talk to a
backend is the platform's (the shared crate: the one set-up call, the REST
transport, the error type, the SignalR client, the browser helpers); the
DESCRIPTION of the backend is generated on every build (its endpoints, hubs,
contract types, request marker and session-refresh path); only WHAT WORKSITE
DOES with the answers is written here - its pages and components (forms,
dialogs, toasts, icons), its session handling, what it does with the hubs.


THE RULES
=========
These are the repository owner's rules FOR THIS APPLICATION; the platform's
own rules (no Node.js, almost no JavaScript, and the rest) are in the root
maintainer file and hold here too.

  - NOTHING UNAPPROVED. A library, crate, package, tool, container image or
    license is used only after the repository owner has approved it by name.
    Raise it first; do not add it.
  - PLATFORM CRATES ONLY. The frontend uses the platform's crates (the build
    program's default crate mode, offline). A crate of its own would switch
    it to crates.io; that is a change for the owner to approve.
  - STYLING IS TAILWIND, or nothing. No other CSS framework, no inline style
    attributes (the site's Content-Security-Policy forbids them).
  - THE BACKEND HAS NO UI. REST controllers only: no MVC views, no Razor
    Pages, no Blazor.
  - THE BACKEND'S CONTRACT IS DECLARED. Every action and every hub carries
    what CodeBrix.WebPlatform.BackendMapper needs (see THE CONTRACT below);
    the build fails, listing every gap, when one does not. The frontend's
    client code is generated, never written by hand.
  - STABLE RELEASES ONLY.
  - NO SECRETS IN THE REPOSITORY. Passwords and keys live in
    samples/WorkSite/.env, which git ignores.
  - NO SHELL SCRIPTS. Build and run steps are .NET programs so that they work
    the same on Windows, macOS and Linux.

The repository's licence gate does NOT check the sample (the owner's ruling:
an application's code and crates are its own). The sample vendors nothing:
its crates are the platform's, materialized into the frontend at build time.


LAYOUT
======
All paths are inside samples/WorkSite.

  WorkSite.slnx                 every .NET project of the sample (the root
                                CodeBrix.WebPlatform.slnx does NOT include it)
  MAINTAINER-README.txt         this file
  THIRD-PARTY-NOTICES.txt       WorkSite's own notices, written by hand (see
                                NOTICES)
  .env.example                  the settings the containers need (no values)
  tailwindcss/                  version.txt: the oldest Tailwind WorkSite
                                accepts; UPGRADE-README.txt
  backend/
    WorkSite.Api/               the REST backend and the hubs; also serves
                                the built frontend from its wwwroot
      Controllers/, Hubs/       the contract BackendMapper reads
    libs/WorkSite.Core/         everything that crosses the wire: the model,
                                request and response classes, the hubs'
                                message classes and client interfaces. It
                                references CodeBrix.WebPlatform.Core.
    libs/WorkSite.DataAccess/   all SQL, the versioned schema, demo data, and
                                the classes only the server sees
    tests/                      the three backend test projects
  frontend/                     the Dioxus crate (Rust): Cargo.toml, its own
                                Cargo.lock, Dioxus.toml, tailwind.css, assets
    src/main.rs                 the app root: the one set-up call
                                (generated::configure_platform, with
                                WorkSite's session hooks), the stylesheet,
                                the router and the start-up splash
    src/platform.rs             WorkSite's own browser helpers: its classes
                                on the document, the dialogs' scroll lock
                                (the general ones are the platform's
                                codebrix_webplatform::browser)
    src/state/session.rs        WorkSite's session handling: who is signed
                                in, what a renewed or ended session does
    src/live/                   what WorkSite does with its hubs (the
                                SignalR client itself is the platform's)
    src/generated/              BUILD OUTPUT: the client code generated from
                                the backend
    platform/                   BUILD OUTPUT: the platform's shared Rust
                                crate (codebrix-webplatform/) and the
                                platform's crates (vendor/)
    .cargo/config.toml          BUILD OUTPUT: tells cargo to build from
                                platform/vendor, offline
                                (all three are written by `generate`; never
                                edited, not committed)
  docker/                       Dockerfile and compose.yaml
  build/
    WorkSite.BuildFrontend/     the sample's build program, a thin caller of
                                the platform's FrontendBuilder library plus
                                WorkSite's own steps (containers, .env,
                                development certificate, notices)
    WorkSite.BuildFrontend.Tests/
                                its tests (and the proof that the real
                                backend's contract is fully declared)
  artifacts/                    build output of the build program (ignored)

At the repository root, and shared with the libraries: LICENSE, global.json,
.gitattributes and .gitignore, and the TEMPORARY package feed, nuget.config
and temp-packages/ (see TEMPORARY PACKAGES below).


THE CONTRACT
============
The frontend talks to the backend through code that
CodeBrix.WebPlatform.BackendMapper writes from the compiled backend. The
mapper refuses a backend that leaves anything undeclared. What it needs, and
the platform's rules, are in AGENT-README.txt at the repository root; in
short, for WorkSite:

  - every action has an HTTP method attribute with its route, is covered by
    [Authorize] or [AllowAnonymous], and declares its success answer with
    [ProducesResponseType(typeof(T), ...)] (or (204) for no body);
  - every class that crosses the wire is in WorkSite.Core and says, with
    [WireRequired] / [WireOptional], which of its reference-type members can
    be null. [WireRequired] is a promise: such a member is never sent as null;
  - every error answer is the platform's ApiProblem
    (CodeBrix.WebPlatform.Core); a 401 for "not signed in" has an empty body;
  - every hub derives from Hub<TClient>, with its client interface and
    message classes in WorkSite.Core/Hubs, carries [WireHub("/api/hubs/...")]
    and [Authorize] or [AllowAnonymous], and is mapped in WorkSiteHubs with
    WireHubAttribute.GetPath so the path is written once;
  - what the frontend never calls (the health probe, the "no such API route"
    controller) is marked [WireIgnore];
  - the CSV export is marked [WireFile]: the frontend gets a function that
    builds its address;
  - the backend DECLARES what the frontend must know about it, where it
    lives: the request marker, [assembly: WireRequestMarker("WorkSite")] in
    backend/WorkSite.Api/WireDeclarations.cs, which RequestedWithMiddleware
    reads back (WireRequestMarkerAttribute.GetValue) - so the value exists
    once; and the session refresh, [WireSessionRefresh] on
    AuthController.Refresh. The generated code carries both
    (REQUEST_MARKER, SESSION_REFRESH_PATH, configure_platform); the
    frontend never types them.

THE RULE FOR WHERE A CLASS LIVES: anything that crosses the wire to the
frontend belongs in WorkSite.Core; anything only the server sees (the user
with its password hash, sessions, repository inputs and filters, all SQL)
stays in WorkSite.DataAccess or WorkSite.Api.

After changing the contract, run `WorkSite.BuildFrontend generate` (or any
command that builds the frontend) and fix what the Rust compiler then reports
in the pages: that is how a contract change reaches the frontend.

The build program's WorkSiteFrontend class holds WorkSite's choices for the
generator: the type mapping (the platform's defaults; change it in
CreateTypeMap), with the rest of WorkSite's settings for the platform library
(CreateOptions). The client code is written with the generator's default
options, so the generated hub code calls the shared crate's SignalR client
(codebrix_webplatform::signalr).



THE HUBS
========
Nine SignalR hubs make WorkSite the test-bed for the platform's SignalR
handling. Between them they use every feature the mapper and the Rust client
support. Where the data is made up, the pages say so. The client is the
shared crate's (codebrix_webplatform::signalr); frontend/src/live holds only
what WorkSite does with the hubs. Its handlers write signals, global ones
included, directly: the client runs them inside the Dioxus runtime. main.rs
sets it up, with the REST client, in ONE call -
generated::configure_platform(session::update_user, session::session_ended)
- which gives the hubs the REST client's shared session refresh and the
request marker for negotiate.

  no sign-in needed
    ClockHub           world clocks (Paris, New York, Los Angeles) pushed to
                       everyone each second; the server works out each
                       city's local time
    ActivityHub        made-up live figures, pushed at irregular intervals
    EstimatorHub       a call with a result, and with errors fit to show
  sign-in needed
    TimerHub           a running timer, ticking in every tab of its user;
                       stopping it records a time entry
    ChangesHub         "your data changed", sent by the REST controllers
    NotificationsHub   made-up reminders, shown as toasts
    ReportProgressHub  a stream of progress steps that can be cancelled
    ProjectFeedHub     groups: made-up teammate activity per project
    SessionHub         "this session has ended", so other tabs sign out

Three things every hub relies on:
  - THE PATH IS UNDER /api. The session cookie is scoped to /api, so a hub
    anywhere else would never see who is signed in.
  - THE ORIGIN CHECK. A browser does not apply the same-origin policy to
    WebSockets and sends cookies with the handshake, and the X-Requested-With
    protection of the REST API does not cover a WebSocket upgrade. So
    HubOriginMiddleware refuses any request to a hub path whose Origin header
    is not this site's own - for the public hubs too. (The negotiate call,
    being a POST under /api, needs X-Requested-With as well; the one set-up
    call makes the SignalR client send it.)
  - TYPED HUBS ONLY. An untyped Clients.All.SendAsync("name", ...) is a string
    at run time and invisible to the mapper.

The timers, feed listeners and activity figures are held in memory and are
lost when the backend restarts. That is acceptable for a demonstration.

The backend's hub tests speak the SignalR JSON protocol over the test server's
WebSocket themselves (tests/WorkSite.Api.Tests/Infrastructure/
HubTestConnection.cs), with an Origin header and the session cookies, as a
browser does. No SignalR client package is referenced.



BUILDING AND RUNNING THE SAMPLE
===============================
What must be installed (the build program checks and tells you; it installs
nothing):
  - the .NET 10 SDK;
  - Rust (rustup), at the platform's minimum version or later, with the
    WebAssembly target (`rustup target add wasm32-unknown-unknown`). No C
    compiler: dx and wasm-bindgen come prebuilt in the host-tools package;
  - the standalone Tailwind program on PATH, at the version in
    tailwindcss/version.txt OR LATER (WorkSite passes that file's version to
    the platform as its minimum);
  - Docker with Compose, to run the containers;
  - the ASP.NET Core HTTPS development certificate, trusted by your browsers
    (`dotnet dev-certs https --trust`, once).

From samples/WorkSite:

    dotnet run --project build/WorkSite.BuildFrontend -- check
    dotnet run --project build/WorkSite.BuildFrontend -- run
    dotnet run --project build/WorkSite.BuildFrontend -- stop

`check` reports each prerequisite and where dx, wasm-bindgen, wasm-opt and
the libgit2 library dx needs to start come from (the packages); it starts dx
once to prove it starts. `run` creates .env with random secrets if it is
missing, exports the development certificate for the web container if that
has not been done, checks the prerequisites, materializes the frontend
(builds the backend, generates the frontend's client code from it, writes
the platform's shared crate and crates and the cargo configuration), builds
the frontend into the backend's wwwroot with the notices, publishes the
backend, builds the web image, starts the two containers, waits for the site
to answer and prints its address. `stop` removes the containers and keeps the
database volume. Nothing ever deletes the database volume by itself; to start
from an empty database, remove the `worksite_worksite-db` volume with Docker
yourself.

Other commands: `generate` (materialize only: after it, plain cargo works in
frontend/, offline, on a fresh clone too), `build` (generate + checks +
frontend), `serve` (dx serve with hot reload), `env`, `cert`. `--help` lists
them.

The demo account's password is in samples/WorkSite/.env
(WORKSITE_DEMO_PASSWORD). Demo data is created once, when the database is
first set up with WORKSITE_DEMO_ENABLED=true.

WHAT IS THE PLATFORM'S AND WHAT IS WORKSITE'S. The generic steps are calls
into CodeBrix.WebPlatform.FrontendBuilder (its PlatformFrontend class; see
the root AGENT-README.txt): the prerequisite check, `generate` (its
MaterializeAsync), the frontend build with the dx environment and shrinking
(its BuildAsync), `serve` (its ServeAsync) and the platform's notices
(PlatformCrates.ReadNotices). dx and wasm-bindgen come from the host-tools
package: build/WorkSite.BuildFrontend/WorkSite.BuildFrontend.csproj
references all five, each only on its own platform, so the sample restores on
any of them. What stays in build/WorkSite.BuildFrontend is WorkSite's own:
its command line (Program, Builder), its paths and settings (WorkSiteLayout,
BuilderPaths, WorkSiteFrontend - the type mapping is in CreateTypeMap - and
the Tailwind version file it accepts), the containers (ContainerStep), .env
(EnvFile), the development certificate (DevCertificateStep) and the notices
file in wwwroot, with the small process runner those steps use.

The backend alone builds and tests with plain dotnet:

    dotnet build WorkSite.slnx
    dotnet test --solution WorkSite.slnx

The backend tests start a throw-away PostgreSQL container through
CodeBrix.Docker and fail (they do not skip) when Docker is not available.
The frontend's own unit tests run with `cargo test --offline` in frontend/,
after `generate` has run once. Those for live/ drive the generated hub types
against the platform's fake hub (codebrix_webplatform::signalr::testing::
FakeHub). The SignalR client's own tests run with the shared crate's, at the
repository root (see the root MAINTAINER-README.txt).

A FRESH CLONE: the frontend does not compile, with cargo or with dx, until
`generate` has written src/generated, platform/ and .cargo/ - and the
Tailwind stylesheet main.rs names (assets/tailwind.css), which the platform's
Materialize writes with the installed Tailwind when it is missing. So: clone,
`generate`, `cargo check`. (Without Tailwind, `generate` says the stylesheet
was not generated, and cargo fails on the asset!() until a build has run.)
The repository's maintainer tests that use the sample as their subject (the
Node-free proof build, the dx native build's functional gate) need it
materialized too.


NOTICES
=======
samples/WorkSite/THIRD-PARTY-NOTICES.txt is WorkSite's OWN notices file,
written by hand: what the LICENSE covers, and what WorkSite uses beyond the
platform (Tailwind and its generated stylesheet, the NuGet packages, the
container images). Edit it when such a component is added, removed or
changes license. Everything the platform compiles into the application - its
shared crate and crates - is recorded by the platform, in the generated file
the FrontendBuilder package carries.

The compiled application contains third-party code, so `build` writes the
notices the site serves into the backend's wwwroot: this file, followed by
the platform's notices (PlatformCrates.ReadNotices). It is shipped in the
container image and served at /THIRD-PARTY-NOTICES.txt.

The root LICENSE covers the code written here. Everything third-party keeps
its own license.


GIT
===
Never commit: samples/WorkSite/.env, any `target` folder, the backend's
wwwroot content (it is build output), samples/WorkSite/artifacts, the
temp-packages folder at the repository root, and the build output written
into the frontend: frontend/src/generated, frontend/platform and
frontend/.cargo. The .gitignore at the repository root covers all of them.
frontend/Cargo.lock IS committed: it is the application's own.


TEMPORARY PACKAGES
==================
WorkSite uses CodeBrix packages that are not on nuget.org yet: the packages
of this repository (WorkSite.Core references Core; WorkSite.BuildFrontend
references FrontendBuilder, which brings Core and BackendMapper, and the
host-tools package of the machine). (CodeBrix.WebPlatform.WasmShrink,
CodeBrix.LibGit2 and CodeBrix.RustTools, which FrontendBuilder also brings,
are published and come from nuget.org.)

They come from the ONE temporary feed at the repository root - nuget.config
and temp-packages/ - which also serves the libraries' own solution. NuGet
finds that nuget.config by walking up from each project; there is no
nuget.config in samples/WorkSite.

Consequences:
  - A fresh clone cannot restore the backend or the build program until
    either the packages are published or the same .nupkg files are put in
    temp-packages/.
  - WorkSite does not see a change to a library until the feed is refreshed.
    The exact procedure (build the libraries in Release, replace the eight
    .nupkg files, set the version in backend/libs/WorkSite.Core/
    WorkSite.Core.csproj and in build/WorkSite.BuildFrontend/
    WorkSite.BuildFrontend.csproj, whose one property
    WebPlatformPackageVersion covers FrontendBuilder and the five host-tools
    references) is TEMPORARY PACKAGES in the root
    MAINTAINER-README.txt.

When the packages are on nuget.org the feed is deleted (see the root file),
and this section with it.


SHRINKING THE WEBASSEMBLY
=========================
The release build of the frontend is shrunk by wasm-opt. NOTHING IS INSTALLED
FOR THAT: the program comes from the CodeBrix.WebPlatform.WasmShrink package,
which the platform's FrontendBuilder library depends on; everything about it
is the platform library's behaviour (see the root AGENT-README.txt,
SHRINKING). WorkSite only chooses where DX_HOME is (artifacts/dx-home) and
how hard wasm-opt works ([web.wasm_opt] in frontend/Dioxus.toml; level "z",
the smallest file, when nothing is set). The platform's Shrink option can
also leave a release build unshrunk (dx then skips wasm-opt); WorkSite does
not use it.

dx AND libgit2. dx does not start without the libgit2 library; the platform
library supplies it from the CodeBrix.LibGit2 package (a dependency of
FrontendBuilder) on every start, from artifacts/dx-home/libgit2 (inside
WorkSite's DX_HOME). To start dx yourself, see DX NEEDS libgit2 in the root
AGENT-README.txt.


CODING CONVENTIONS (.NET)
=========================
  - Target net10.0. Nullable reference types are OFF: never `?` on a
    reference type, never the `!` operator. Value-type nullables are fine.
  - No <ImplicitUsings>, no global usings. File-scoped namespaces, one type
    per file, usings at the top (System.* first, alphabetical).
  - 0 warnings and 0 errors in Debug and Release. Fix a warning at its
    source; never suppress it.
  - Tests are xUnit v3 with SilverAssertions; files named
    <ClassUnderTest>Tests.cs; methods MemberName_snake_case_description or
    snake_case_description.
  - Usings sorted System.* first, then alphabetically by namespace
    (`using System;` before `using System.Collections.Generic;`).
  - The repository owner commits. Tools and agents never run git commit,
    push, add or reset.

================================================================================
END OF MAINTAINER-README
================================================================================
