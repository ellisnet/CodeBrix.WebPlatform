================================================================================
MAINTAINER-README: the ApplicationSplitter sample (CodeBrix.WebPlatform)
For people (and AI coding agents) changing the sample under
samples/ApplicationSplitter
================================================================================

The libraries of this repository (src/, libs/, tests/, tools/ and the NuGet
packages) have their own maintainer file, MAINTAINER-README.txt at the
repository root. This file is about this sample only. How the platform's
features work is in AGENT-README.txt at the repository root (SEVERAL FRONTEND
APPLICATIONS, BACKEND API REFERENCE, CODE SPLITTING).

PURPOSE AND SCOPE
=================
ApplicationSplitter is a deliberately small application that PROVES two
platform features:

  - SEVERAL FRONTEND APPLICATIONS in one platform application: ONE backend,
    ONE build program, ONE build, and three separate Dioxus applications
    that share the backend, its API, the generated client code and the
    platform's crates;
  - CODE SPLITTING (dx's bundle splitting, lazy loading): one of the three
    applications is cut into a main WebAssembly file and pieces that the
    browser downloads when a page is first opened.

The three frontend applications, under frontends/:

    home    NO name: served at the site root, https://host/. A landing page
            that links to the other two. Not split.
    sales   served below /sales. Built WITH bundle splitting (every route a
            piece, plus the lower-level forms). A light theme.
    admin   served below /admin. Built WITHOUT splitting, one WebAssembly
            file. A dark theme by design (always dark).

Most of what the pages show is made up; the sample is about the platform,
not about sales or administration.

It consumes the platform the way any application does: the backend
references CodeBrix.WebPlatform.Backend (which brings
CodeBrix.WebPlatform.Core) and makes its two calls, and the build program
references CodeBrix.WebPlatform.FrontendBuilder and the host-tools package
for the machine it runs on.


WHAT IS DELIBERATELY MISSING
============================
  - NO DATABASE: the backend's data is made up, in memory.
  - NO SIGN-IN: every controller and the hub are [AllowAnonymous], so the
    backend has no [WireSessionRefresh] action. It still declares its
    request marker, [assembly: WireRequestMarker("ApplicationSplitter")] in
    backend/ApplicationSplitter.Api/WireDeclarations.cs, because the Backend
    package's X-Requested-With check is on.
  - NO CONTAINERS and no deployment step: `run` starts the backend with
    `dotnet run`.
  - NO CRATE BUNDLES: the build program registers none; the frontends use
    the platform's crates only.
  - NO NOTICES FILE OF ITS OWN: the sample uses no third-party material
    beyond the platform's. The site serves the platform's notices
    (PlatformCrates.ReadNotices) as wwwroot/THIRD-PARTY-NOTICES.txt.
  - NO TEST PROJECTS: what the sample proves is checked by building it and
    looking at it in a browser (CHECKING IN A BROWSER below). The platform's
    behaviour is tested in the libraries' own test projects.


THE RULES
=========
The platform's own rules (no Node.js, almost no JavaScript, and the rest)
are in the root maintainer file and hold here too. For this sample:

  - THE BACKEND NAMES NO FRONTEND APPLICATION. It serves what the frontend
    build wrote into its wwwroot, read from the manifest
    (codebrix-frontends.toml) by app.UsePlatformBackend(). The names exist
    in ONE place: the build program.
  - THE BASE PATH IS NOT IN Dioxus.toml. The build program gives dx each
    application's name (dx's --base-path), so the name is written once.
  - THE PIPELINE ORDER of AGENT-README.txt: AddPlatformBackend with the
    site's Content-Security-Policy; UsePlatformBackend; then the backend's
    own UseRouting, controllers and hub.
  - MOVING BETWEEN APPLICATIONS IS A FULL PAGE LOAD: a link to another
    application is a plain anchor (a { href: "/sales" }), never the router's
    Link.
  - PLATFORM CRATES ONLY (CrateMode.PlatformCratesOnly, offline), and no
    crate beyond those each Cargo.toml names. Only sales switches on the
    wasm-split feature of dioxus; home and admin must not.
  - STYLING IS TAILWIND utility classes in the applications' own Rust; no
    inline style attributes (the Content-Security-Policy forbids them), no
    JavaScript of our own, no `eval`.
  - THE THREE APPLICATIONS SHARE NO CODE. Each is a separate crate; the
    repetition between them is intended.
  - 0 warnings, from the .NET build, `cargo check` and the dx build. The
    router macro's names for split routes would give one non_snake_case
    warning per route in sales; the lint is allowed in sales' routes module,
    which holds the Routable enum and nothing else.
  - NO SHELL SCRIPTS. Build and run steps are .NET programs.
  - STABLE RELEASES ONLY.

The repository's licence gate does NOT check the sample (an application's
code and crates are its own). The sample vendors nothing.


LAYOUT
======
All paths are inside samples/ApplicationSplitter.

  ApplicationSplitter.slnx      the backend and the build program (the root
                                CodeBrix.WebPlatform.slnx does NOT include
                                it)
  MAINTAINER-README.txt         this file
  backend/
    ApplicationSplitter.Api/    the one backend: Program.cs (the two Backend
                                calls), WireDeclarations.cs (the request
                                marker), Controllers/ (api/sales and
                                api/admin, read-only made-up data and one
                                state-changing action), Hubs/ (ClockHub, a
                                public hub at /api/hubs/clock, and the
                                ticker that sends the server's time once a
                                second), Contracts/ (the classes and enums
                                that cross the wire)
      wwwroot/                  BUILD OUTPUT: home's site at the top,
                                sales/ and admin/ beside it, the manifest
                                and THIRD-PARTY-NOTICES.txt (.gitkeep
                                excepted, never committed)
  build/
    ApplicationSplitter.BuildFrontend/
                                the build program: names the three
                                applications (CreateOptions) and calls the
                                platform's FrontendBuilder
  frontends/
    home/, sales/, admin/       one Dioxus crate each: Cargo.toml, its own
                                Cargo.lock (committed), Dioxus.toml,
                                index.html, tailwind.css, src/
      src/generated/            BUILD OUTPUT: the client code generated from
                                the backend (the same in all three)
      platform/                 BUILD OUTPUT: the shared Rust crate
                                (codebrix-webplatform/); in home/ also the
                                platform's crates (vendor/), which the other
                                two use from there
      .cargo/config.toml        BUILD OUTPUT: tells cargo to build from that
                                vendor folder, offline
      assets/tailwind.css       BUILD OUTPUT: the generated stylesheet
      target/                   BUILD OUTPUT: cargo's and dx's

At the repository root, and shared with the libraries: LICENSE, global.json,
.gitattributes and .gitignore, and the TEMPORARY package feed, nuget.config
and temp-packages/ (see TEMPORARY PACKAGES below).


BUILDING AND RUNNING THE SAMPLE
===============================
What must be installed (the build program checks and tells you; it installs
nothing): the .NET 10 SDK; Rust (rustup) at the platform's minimum version
or later with the WebAssembly target (`rustup target add
wasm32-unknown-unknown`). No C compiler, and no Tailwind: dx and
wasm-bindgen come prebuilt in the host-tools package, and the standalone
Tailwind program in its Styling package dependency.

From samples/ApplicationSplitter:

    dotnet run --project build/ApplicationSplitter.BuildFrontend -- check
    dotnet run --project build/ApplicationSplitter.BuildFrontend -- generate
    dotnet run --project build/ApplicationSplitter.BuildFrontend -- build
    dotnet run --project build/ApplicationSplitter.BuildFrontend -- run
        (each also with --debug)
    dotnet run --project build/ApplicationSplitter.BuildFrontend -- serve [name]

  check     says what the machine has and lacks for a build (Rust, Tailwind,
            wasm-opt, dx and wasm-bindgen, the libgit2 library dx needs).
  generate  builds the backend and writes, into each of the three frontends,
            the generated client code, the shared crate, the cargo
            configuration and a missing Tailwind stylesheet; the crates go
            into the one shared vendor folder. Afterwards plain
            `cargo check --target wasm32-unknown-unknown --offline` works in
            each frontend folder.
  build     generate, then build all three with dx into the backend's
            wwwroot (with the manifest) and write the notices there; it
            prints each application's file count and folder and which one
            was split. Release by default; --debug for a debug build (sales
            is shrunk by wasm-opt either way: a split build always is).
  run       build, then start the backend at http://localhost:5210/ (home at
            /, sales at /sales, admin at /admin). Ctrl+C stops it.
  serve     generate, then `dx serve` with hot reload for ONE application:
            no name for home, or sales, or admin. Serving never splits the
            bundle; use build or run to see the pieces. The frontend's API
            calls need the backend running.

The backend alone builds with plain dotnet: `dotnet build
ApplicationSplitter.slnx`. Restart the backend after a new build of the
frontends: it reads the manifest once, when it starts.

A FRESH CLONE: the frontends do not compile, with cargo or dx, until
`generate` has written src/generated, platform/ and .cargo/ (and the
Tailwind stylesheet, which it writes with the packaged Tailwind when it is
missing). So: clone, `generate`, `cargo check`.


CHECKING IN A BROWSER
=====================
There are no screenshots in this check. Start the site with `run`, then
drive headless Chrome over its debugging protocol (the Chrome DevTools
Protocol), started with its own profile folder in a scratch location, for
example

    google-chrome --headless=new --remote-debugging-port=<port>
                  --user-data-dir=<a scratch folder>

and a small client of the protocol kept OUTSIDE the repository (the
repository holds no scripts). Read the page text, the console, the network
log and computed styles through the protocol. Stop the Chrome you started
(by the process you started, never by a name search) and the backend when
you are done.

What to check:
  - home loads at /, sales at /sales and admin at /admin, each showing its
    own pages (every page has an element with id="page-title" holding its
    heading); a deep link (for example /sales/orders or an admin page
    address typed directly) loads that page;
  - the links between the applications load the other application;
  - REST calls work from every application: a GET, and in admin the one
    POST (it must carry the X-Requested-With marker, which the generated
    code sends);
  - the console shows NO errors and NO Content-Security-Policy violations,
    in all three, under the site's policy (Program.ContentSecurityPolicy);
  - in sales, the network log shows the lazy loading: the first page
    requests the main .wasm and ITS OWN route piece (module_..._route...)
    only; opening another page for the first time requests that page's
    piece; a second visit requests nothing new; a deep link requests only
    the piece of the page it opens. The parts split by hand (with the
    lower-level forms of CODE SPLITTING) request their pieces when they are
    first used;
  - in admin, there is one .wasm file and no pieces;
  - the build's compressed copies: `build` prints, per application, how many
    files it compressed, their size before and after and the time it took;
    in the network log the .wasm, .js and .css files (the pieces of sales
    included) arrive with Content-Encoding br, or gzip when the browser does
    not ask for Brotli (look at the request's Accept-Encoding), and the
    pages work exactly as without; /sales/index.html.br answers 404;
  - the clock hub's live time shows and updates in sales (the SignalR client
    from an application served below a name);
  - /codebrix-frontends.toml answers 404, and an unknown /api address
    answers 404 with the platform's problem body;
  - the themes: sales light, admin dark whatever the browser's setting;
    no horizontal overflow at narrow (360 px) and desktop widths.


CHECKING THAT A BUILD IS REPRODUCIBLE
=====================================
An unchanged application must keep its file names (they carry a hash of the
content) so that a returning visitor's browser reuses what it has; sales,
which is split, is the case that broke before (see REPRODUCIBLE APPLICATION
BUILDS in the root maintainer file). After a change to the platform's tools:

  1. Delete each frontend's target/ and the generated files (platform/,
     .cargo/, src/generated/, assets/tailwind.css) and the web root's content
     (.gitkeep excepted), run `build`, and keep the list of every file of the
     web root with its SHA-256.
  2. Do it again, from clean, and compare: every name and every byte must be
     the same, in all three applications, the compressed copies and the
     manifest included. An incremental `build` (nothing deleted) must give
     the same files too.
  3. Change one string in one sales page WITHOUT changing the file's length
     and build: only that page's piece, the loader (__wasm_split-dxh...js),
     the main script (splitter-sales-dxh...js), their compressed copies and
     index.html may change. (A change of length renames most of the pieces
     and the main .wasm: upstream names the split points from their position
     in the source.)

Build in the same folder each time: a copy of the sample in another folder
gives other names (the compiled code carries absolute paths). A copy must
keep the repository's .gitignore, whose line for the frontends' target/
keeps Tailwind from reading the build output.


TEMPORARY PACKAGES
==================
The sample uses CodeBrix packages that are not on nuget.org yet: the
Backend package (backend/ApplicationSplitter.Api) and FrontendBuilder with
the host-tools package of the machine (build/ApplicationSplitter
.BuildFrontend). They come from the ONE temporary feed at the repository
root - nuget.config and temp-packages/ - which also serves the libraries'
solution and WorkSite. NuGet finds that nuget.config by walking up from each
project; there is no nuget.config in samples/ApplicationSplitter.

  - A fresh clone cannot restore the backend or the build program until the
    packages are published or the same .nupkg files are put in
    temp-packages/.
  - The sample does not see a change to a library until the feed is
    refreshed. The version is set in TWO files, each with one property,
    WebPlatformPackageVersion: backend/ApplicationSplitter.Api/
    ApplicationSplitter.Api.csproj and build/ApplicationSplitter
    .BuildFrontend/ApplicationSplitter.BuildFrontend.csproj. The procedure
    is TEMPORARY PACKAGES in the root MAINTAINER-README.txt.

When the packages are on nuget.org the feed is deleted (see the root file),
and this section with it.


GIT
===
Never commit: the backend's wwwroot content (.gitkeep excepted), any
`target` folder, and what is written into each frontend: src/generated,
platform/, .cargo/ and assets/tailwind.css. The .gitignore at the
repository root covers all of them. Each frontend's Cargo.lock IS
committed: it is that application's own. The third line of each is the
platform's marker line, so the build keeps them in step when a platform
crate moves (AGENT-README.txt, THE FRONTEND'S Cargo.lock): commit what the
build changed there, and never move the line.


CODING CONVENTIONS
==================
  - .NET: net10.0; nullable reference types OFF (never `?` on a reference
    type, never `!`); no implicit or global usings; file-scoped namespaces,
    one type per file, usings at the top (System.* first, then
    alphabetically by namespace); XML doc comments on public members; 0
    warnings in Debug and Release.
  - Rust: a `//!` comment at the top of each module saying what it is;
    0 warnings from `cargo check --target wasm32-unknown-unknown`.
  - The repository owner commits. Tools and agents never run git commit,
    push, add or reset.

================================================================================
END OF MAINTAINER-README
================================================================================
