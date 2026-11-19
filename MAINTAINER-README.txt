================================================================================
MAINTAINER-README: CodeBrix.WebPlatform
Notes for people and agents MAINTAINING this repository - not for package
consumers
================================================================================

If you are CONSUMING the NuGet packages, stop reading and open AGENT-README.txt
instead. Everything below is about the repository itself: how it is laid out,
how it builds, how it is tested, how it is packaged, how the third-party code
it carries is kept and checked, and the conventions the source follows.

The WorkSite sample application under samples/WorkSite has a maintainer file
of its own, samples/WorkSite/MAINTAINER-README.txt. Read that one when you are
changing the sample.


PURPOSE AND SCOPE
=================
This repository produces eight NuGet packages:

    PackageId:  CodeBrix.WebPlatform.Core.ApacheLicenseForever
    Project:    src/CodeBrix.WebPlatform.Core/CodeBrix.WebPlatform.Core.csproj

    PackageId:  CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever
    Project:    src/CodeBrix.WebPlatform.BackendMapper/
                CodeBrix.WebPlatform.BackendMapper.csproj

    PackageId:  CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever
    Project:    src/CodeBrix.WebPlatform.FrontendBuilder/
                CodeBrix.WebPlatform.FrontendBuilder.csproj

    PackageId:  CodeBrix.WebPlatform.FrontendBuilder.<Platform>
                .ApacheLicenseForever, Platform = LinuxX64, LinuxArm64,
                WindowsX64, MacOSArm64, MacOSX64 (the host-tools packages)
    Project:    src/CodeBrix.WebPlatform.FrontendBuilder.<Platform>/
                CodeBrix.WebPlatform.FrontendBuilder.<Platform>.csproj

    Assembly names and root namespaces are the package ids without the
    .ApacheLicenseForever suffix.
    License:    Apache-2.0 (all eight). Every package this repository
                produces carries the .ApacheLicenseForever suffix.
    Consumer documentation: AGENT-README.txt (repo root), ONE file shared by
                all the packages and packed into each.

and one UNPUBLISHED maintainer tool, libs/CodeBrix.WebPlatform.LibraryChecker
(the licence and vendoring gate, re-vendoring and the Node-free proof build;
see THE MAINTAINER TOOL). It is no part of any package.

CodeBrix.WebPlatform is a platform for web applications with a .NET backend
(REST controllers and SignalR hubs) and a Rust frontend compiled to
WebAssembly. The project is chiefly about how the FRONTEND gets built; how an
application's backend works inside is the application's business, beyond what
BackendMapper reads from it.

  Core            what every platform backend references: the contract
                  attributes, the backend's declarations about itself (its
                  request marker, its session-refresh action) and the
                  standard error body.
  BackendMapper   build-time only: reads a compiled backend's metadata and
                  writes the frontend's Rust client code through
                  CodeBrix.Templating templates.
  FrontendBuilder build-time only: the library a build PROGRAM calls. It
                  checks the machine's prerequisites, materializes the
                  frontend (builds and maps the backend, writes the generated
                  code, the shared Rust crate it carries embedded, the
                  platform's crates it carries as one archive, the cargo
                  configuration and, when it is missing, the Tailwind
                  stylesheet by running the installed Tailwind once), and
                  builds or serves the site with dx. It
                  hands the build program the notices for what the platform
                  compiles into a site.
  <Platform>      build-time only: the prebuilt dx and wasm-bindgen programs
  (host tools)    for one build machine, copied beside the build program as
                  frontend-tools/<rid>/. FrontendBuilder's HostTools class
                  owns that contract.

The WorkSite sample is where functionality is proved first; it then moves out
of the sample into a library (see RULES WHEN CODE MOVES OUT OF THE SAMPLE).
The sample holds only what is its own.


THE RULES OF THE LIBRARIES
==========================
These hold for everything under src/ and libs/ and for what we ship. (The
WorkSite sample has rules of its own; the two sets coexist.)

  - NO NODE.JS. Nothing these libraries ship uses, relies on or talks to
    Node.js, npm or any JavaScript tooling, at build time or at run time. The
    platform assumes Node.js is not installed. A consumer who decides they
    need it is not stopped, and nothing here may depend on that choice.
  - ALMOST NO JAVASCRIPT. Be extremely careful about any JavaScript in a
    platform application. The shared Rust crate and the generated client code
    reach the browser only through the web-sys / js-sys bindings: no .js
    files, no `eval`, no inline scripts, no wasm-bindgen snippets.
  - CORE IS A PLAIN .NET 10 LIBRARY with no ASP.NET Core dependence and no
    NuGet dependencies. It is deployed inside every backend, so it stays
    small. ASP.NET Core plumbing does not go into Core; a separate backend
    package is to be discussed with the repository owner if a need appears.
  - BACKENDMAPPER NEVER SHIPS with an application and never runs the backend.
    It reads metadata only (System.Reflection.MetadataLoadContext).
  - FRONTENDBUILDER NEVER SHIPS with an application either, and knows
    nothing about any one application: no application's names, paths or
    constants. What differs between applications is an option. It installs
    nothing; a missing prerequisite is reported with what to do.
  - AN APPLICATION MAY ADD ANY CRATE OF ITS OWN. The platform's crates are the
    default and work offline; an application that adds crates switches to
    crates.io (CrateMode.ApplicationAddsCrates) and is never stopped.
  - NO CONTAINERS AND NO DATABASE IN THE PACKAGES. Deploying to containers
    and PostgreSQL are the WorkSite sample's own business; no package here
    depends on CodeBrix.Docker, CodeBrix.RustTools.Docker or anything
    PostgreSQL. (The maintainer tool uses a container for the proof build;
    it is not a package.)
  - THE LICENCE SCOPE (the repository owner's ruling): the gate checks ONLY
    what is in this repository outside samples/ - the platform's crates the
    FrontendBuilder package ships, the tool sources and their crates, the
    patches, the notices and the no-Node rule. An application's own code
    and crates, the sample's included, are the application's business.
  - NO NEW NUGET PACKAGE, crate or assembly without the repository owner's
    approval by name. BackendMapper's approved dependencies are
    CodeBrix.Templating and System.Reflection.MetadataLoadContext;
    FrontendBuilder's are Core, BackendMapper, CodeBrix.WebPlatform.WasmShrink,
    CodeBrix.LibGit2 (the libgit2 library dx needs) and CodeBrix.RustTools;
    the host-tools packages have none; the maintainer tool adds
    CodeBrix.RustTools.Docker.
  - NO SHELL SCRIPTS outside tools/dx-native-tools (whose native builds are
    scripts, like every CodeBrix native-tools folder). Build steps are .NET
    programs.
  - STABLE RELEASES ONLY of anything this project chooses.
  - The repository owner commits and publishes. Tools and agents never run
    git commit, push, add or reset.


REPOSITORY LAYOUT
=================
    src/CodeBrix.WebPlatform.Core/           packable
      Mapping/                               the Wire* attributes (the
                                             contract, and WireRequestMarker /
                                             WireSessionRefresh)
      Problems/                              ApiProblem, ApiProblemCodes

    src/CodeBrix.WebPlatform.BackendMapper/  packable
      (root)                                 BackendMapper, BackendMapOptions,
                                             BackendMapException,
                                             MappingProblem
      Model/                                 the findings: BackendMap and what
                                             it holds (public, read-only)
      TypeMapping/                           RustTypeMap, RustType
      Reading/                               internal: opening the assembly,
                                             reading attributes as data, the
                                             type / controller / hub readers,
                                             route templates, naming rules
      Rust/                                  RustClientGenerator, options,
                                             RustTemplates, GeneratedFile
      Rust/View/                             internal: the view the templates
                                             see, and ViewBuilder which works
                                             out every Rust name and type
      Rust/Templates/*.sbn                   the four built-in templates,
                                             embedded

    src/CodeBrix.WebPlatform.FrontendBuilder/  packable
      (root)                                 PlatformFrontend (check,
                                             materialize, build, serve),
                                             PlatformFrontendOptions,
                                             FrontendConfiguration,
                                             FrontendRequirements, the two
                                             results, FrontendBuildException
      Prerequisites/                         the report and its items;
                                             internal PrerequisiteRules
      Tools/                                 HostTools, HostToolsSource;
                                             internal HostToolsLocator
      SharedCrate/                           SharedRustCrate, SharedRustFile
      Stylesheet/                            StylesheetOutcome; internal
                                             StylesheetRules (Materialize's
                                             Tailwind-stylesheet step)
      Crates/                                PlatformCrates, CrateMode;
                                             internal CargoConfiguration
      Internal/                              process runner, output tail,
                                             execute permission
      buildTransitive/                       copies frontend-crates/ into a
                                             consuming build's output
      Rust/codebrix-webplatform/             the shared Rust crate's source
        Cargo.toml, src/                       embedded in the assembly
        Cargo.lock, .cargo/, target/           NOT embedded: for `cargo test`
                                               in this repository only
      Rust/platform-crates/                  the platform's CRATE SET: a
                                             manifest naming every crate a
                                             frontend may use offline, and its
                                             Cargo.lock (also embedded, as the
                                             starting lock file). Never built.
      Rust/vendor/                           THE PLATFORM'S CRATES, written by
                                             `cargo vendor` from the crate set;
                                             never edited. Packed as one
                                             archive (see BUILDING)
      Rust/vendoring/<library>/              UPGRADE-README.txt for the crates
                                             chosen by name (dioxus, reqwest,
                                             serde)
      Rust/notices/                          notices-header-PlatformCrates.txt
                                             (hand-written) and
                                             THIRD-PARTY-NOTICES-PlatformCrates
                                             .txt (GENERATED; packed)

    src/CodeBrix.WebPlatform.FrontendBuilder.<Platform>/  packable (x5)
      HostTools.cs                           names the programs and folder
      buildTransitive/                       copies frontend-tools/<rid>/ into
                                             a consuming build's output

    libs/CodeBrix.WebPlatform.LibraryChecker/  NOT packable: the maintainer
                                             tool (see THE MAINTAINER TOOL)
      Checks/, Patching/                     the gate
      Maintenance/                           vendor, vendor-tools, proof

    tests/CodeBrix.WebPlatform.Core.Tests/
    tests/CodeBrix.WebPlatform.BackendMapper.Tests/
    tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/
    tests/CodeBrix.WebPlatform.FrontendBuilder.HostTools.Tests/
    tests/CodeBrix.WebPlatform.LibraryChecker.Tests/
    tests/CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend/
    tests/CodeBrix.WebPlatform.BackendMapper.Tests.BrokenBackend/
                                             two backends the mapper tests
                                             read (FrontendBuilder.Tests
                                             materializes from SampleBackend
                                             too); see EXTRAS-README.txt

    tools/dx-native-tools/                   builds dx and wasm-bindgen for the
                                             host-tools packages (start at its
                                             README.txt)
      sources/                               THE TOOL SOURCES: dioxus-cli/
                                             (patched), codebrix-libgit2-sys/
                                             (OURS: the declarations-only
                                             stand-in for libgit2-sys; no C
                                             code), wasm-bindgen-cli/, vendor/
                                             (every crate the two are built
                                             from) and .cargo/ (points cargo at
                                             vendor/). Read-only to every build
      vendoring/<tool>/                      per tool: the pristine crates.io
                                             archive, its checksum, the
                                             numbered patches, PATCH-NOTES.txt,
                                             UPGRADE-README.txt
      notices/                               the tools' notices: hand-written
                                             headers and GENERATED files
      licences/, linux/, windows/, macos/    licence file, scripts, pins
      output/                                build results (git-ignored)
    tools/licensing/
      license-allowlist.json                 the approved licences, by tier.
                                             ONLY THE REPOSITORY OWNER edits it

    native_libraries/<rid>/                  the committed dx and wasm-bindgen
                                             programs, their licence and
                                             notices files and provenance
                                             records (packed by the host-tools
                                             packages, provenance excepted)

    samples/WorkSite/                        the sample application; its own
                                             solution, maintainer file and
                                             notices. See EXTRAS-README.txt.

    CodeBrix.WebPlatform.slnx                the solution: the eight packable
                                             projects, the maintainer tool,
                                             and in its Tests folder the test
                                             projects and the two fixture
                                             backends; its Solution Items
                                             folder carries .gitignore,
                                             AGENT-README.txt,
                                             EXTRAS-README.txt, global.json,
                                             icon-codebrix-128.png, LICENSE,
                                             MAINTAINER-README.txt,
                                             nuget.config, README-INDEX.txt,
                                             README.md and
                                             THIRD-PARTY-NOTICES.txt. WorkSite
                                             is NOT in it and must stay out.

    nuget.config, temp-packages/             TEMPORARY: the one local package
                                             feed, for this solution and for
                                             WorkSite. See TEMPORARY
                                             PACKAGES below.

    global.json                              selects the
                                             Microsoft.Testing.Platform test
                                             runner, and pins the .NET 10 SDK
                                             (this repository builds nothing
                                             for Android)
    .gitattributes, .gitignore               see GIT AND THE VENDORED FOLDERS

    AGENTS.md, CLAUDE.md, .clinerules, .cursorrules, .windsurfrules,
    .cursor/rules/agent-readme.mdc, .github/copilot-instructions.md,
    .junie/guidelines.md                     the AI-assistant pointer files,
                                             byte-for-byte the family's
                                             canonical copies. Never edit.


BUILDING
========
From the repository root:

    dotnet restore CodeBrix.WebPlatform.slnx
    dotnet build   CodeBrix.WebPlatform.slnx

Target framework: net10.0 only. 0 warnings and 0 errors in Debug and Release.
GenerateDocumentationFile is ON in every library, so every public member
carries an XML doc comment; fix CS1591 by writing the comment, never with
<NoWarn>.

THE EMBEDDED RUST CRATE. The FrontendBuilder project embeds every file under
Rust/codebrix-webplatform except target/, Cargo.lock and .cargo/, as resources
named "CodeBrix.WebPlatform.FrontendBuilder.Rust/<path inside the crate>".
Building FrontendBuilder does NOT compile the Rust; it only packs the source.
A new file in the crate is picked up by the glob. SharedRustCrateTests checks
that the manifest and the expected modules are present and that cargo's output
is not. Rust/platform-crates/Cargo.lock is embedded too, as
"CodeBrix.WebPlatform.FrontendBuilder.PlatformCrates/Cargo.lock".

THE CRATES ARCHIVE. Rust/vendor is NOT embedded and NOT packed as loose files.
The target _CreatePlatformCratesArchive (in the project file) zips it with
MSBuild's ZipDirectory task into obj/frontend-crates/platform-crates.zip -
incrementally: only when a file of Rust/vendor or the crate set's Cargo.lock
is newer than the archive (Debug and Release share it). The archive and
Rust/notices/THIRD-PARTY-NOTICES-PlatformCrates.txt are None items that are
packed under tools/frontend-crates/ and copied to the output folder as
frontend-crates/ (so a project reference - the tests, the maintainer tool -
gets them as well); in a consuming project, the package's buildTransitive
targets file does that copy. PlatformCrates looks for
<application base folder>/frontend-crates/platform-crates.zip: that folder
name is a contract. Rust/vendor is in DefaultItemExcludes, so its files are
never project items.

THE HOST-TOOLS PACKAGES pack native_libraries/<rid>/ (the *.provenance.txt
files excepted) under tools/frontend-tools/<rid>/ and copy it to
frontend-tools/<rid>/ the same two ways. A platform whose programs have not
been built yet packs its small assembly only.

FrontendBuilder references CodeBrix.WebPlatform.WasmShrink, CodeBrix.LibGit2
and CodeBrix.RustTools, all from nuget.org. WasmShrink's buildTransitive
targets copy wasm-opt into the output folder of every project that references
FrontendBuilder, the test project included; NuGet puts CodeBrix.LibGit2's
native library there too (runtimes/<rid>/native/, or the program's own folder
for a build for one runtime).

LIBGIT2 AND dx. dx links libgit2 as a shared library and does not start
without it (tools/dx-native-tools/README.txt, "dx NEEDS libgit2 TO START").
The library is NEVER in native_libraries/ or in a host-tools package: it comes
only from the CodeBrix.LibGit2 package. Before every start of dx
(BuildAsync, ServeAsync, CheckPrerequisitesAsync) the library reads from dx
the name it was linked against, checks the package's file is that library,
copies it under that name into <DX_HOME>/libgit2 (Tools/Libgit2Supply.cs,
Tools/Libgit2Naming.cs) and puts that folder first on dx's LD_LIBRARY_PATH /
DYLD_LIBRARY_PATH / PATH. The maintainer tool's proof build makes the same
preparation for its container (it may see the library's internals).

System.Reflection.MetadataLoadContext is referenced at a version from the
.NET 11 package line while the library targets net10.0; that package supports
net10.0. Do not retarget the libraries to follow it.


TESTING
=======
    dotnet test --solution CodeBrix.WebPlatform.slnx

THE TEST RUNNER IS Microsoft.Testing.Platform, selected by global.json at the
repo root. Do not delete the "test" section: without it `dotnet test` falls
back to the VSTest bridge, which xUnit v3 no longer supports on this SDK. The
output ends in a "Test run summary:" block. If `dotnet test --solution` ever
reports zero tests, run a test assembly directly and read its counts:

    dotnet tests/CodeBrix.WebPlatform.Core.Tests/bin/Debug/net10.0/CodeBrix.WebPlatform.Core.Tests.dll

The test projects are xUnit v3 with SilverAssertions. No coverage collector is
referenced. No network, database or container is needed. A few FrontendBuilder
tests run programs of the machine (`dotnet msbuild`, and the prerequisite
probes: rustc, tailwindcss, wasm-opt); they assert what is true on any
machine. One runs the installed Tailwind program to write a missing
stylesheet, and is SKIPPED, with the reason, on a machine without Tailwind at
the platform's minimum; the stylesheet step's decisions are tested without it
(a Tailwind program name that does not exist stands in for "not installed").
Some tests START THE ADOPTED dx (native_libraries/<rid>/, or the host-tools
package's copy in HostTools.Tests) through the library, with the
CodeBrix.LibGit2 package's libgit2 - `dx --version`, dx without the library
(it must not start) and `dx new` from a local template made with the
CodeBrix.LibGit2 API, with no git program on PATH; they are skipped on a
machine with no adopted programs. No test builds a frontend with dx. The
LibraryChecker tests run the whole gate
against this repository and so need cargo (offline); they do not start the
proof build. The HostTools tests check, for the machine they run on, that the
programs are in the output, report the expected versions and carry the
generated notices.

HOW THE MAPPER IS TESTED. BackendMapper.Tests has project references to two
small backends (class libraries with a FrameworkReference to
Microsoft.AspNetCore.App) so that they are built with it and copied beside the
test assembly, where Helpers/Backends.cs finds them. SampleBackend is mapped
once (default options) and shared by the tests; BrokenBackend must produce the
expected problems, all in one exception. When the mapper learns a new feature,
add a use of it to SampleBackend; when it learns a new refusal, add a case to
BrokenBackend. (BrokenBackend must LACK a [WireSessionRefresh] action, so
its wrong refresh actions are in RefreshMistakesController, which is marked
[WireIgnore] and read on its own by a ControllerReader test.) The mapper runs
against the ASP.NET Core runtime installed beside the .NET runtime the tests
run on.

The generated Rust is checked as text in RustClientGeneratorTests. That it
COMPILES is proved by the WorkSite sample, whose frontend is built from it:
after a change to the templates or to ViewBuilder, refresh the temporary
packages and build the sample (see the WorkSite maintainer file).

THE SHARED CRATE'S RUST TESTS

    cd src/CodeBrix.WebPlatform.FrontendBuilder/Rust/codebrix-webplatform
    cargo test --offline
    cargo check --offline --target wasm32-unknown-unknown

The native `cargo test` runs the SignalR client against its fake hub (the
same connection logic as in the browser, with a virtual clock) and creates
Dioxus VirtualDoms to prove that handlers and the session refresh run inside
the application's runtime. The wasm32 check is the only build of the browser
transport (signalr/web.rs) here; it is exercised for real by the WorkSite
sample in a browser.

These need Rust with the wasm32-unknown-unknown target. The crate's
dependencies come from the platform's crates: the .cargo folder beside the
crate points cargo at ../vendor, so nothing is downloaded. The crate may
depend only on the platform's crates: the check `LibraryChecker vendor`
fails when its own Cargo.lock names anything else.


PACKAGING AND PUBLISHING
========================
GeneratePackageOnBuild is true in every library, so every build emits fresh
.nupkg files under src/<project>/bin/<Configuration>/.

Versioning is the CodeBrix date-stamped scheme, computed in each csproj from
System.DateTime.UtcNow as 1.<years-since-base>.<day-of-year>.<minute-of-day>.
It is monotonically increasing but is NOT SemVer. Two builds inside the same
UTC minute produce the SAME version - never publish two packages from one
minute. Do not replace the version block with a literal <Version>.

SHIP THE FAMILY TOGETHER. BackendMapper references Core, and FrontendBuilder
references both, as projects; when packed, those become package dependencies
on the versions of the same build. Publish ALL EIGHT packages from ONE build
of the solution, so they match: a frontend's shared crate and crates (from
FrontendBuilder) are the ones the generated code (from BackendMapper) was
written against, and the host-tools packages' dx and wasm-bindgen match the
platform's crates. Check that the .nupkg files carry the same version: a
build that crosses a UTC minute can give them different ones.

What ships inside each nupkg:
    the assembly and its XML documentation
    icon-codebrix-128.png    (PackageIcon)
    README.md                (PackageReadmeFile)
    AGENT-README.txt         (the consumer guide, shared, from the repo root)
    THIRD-PARTY-NOTICES.txt  (the ROOT file - see NOTICES)
  and:
    FrontendBuilder          the shared Rust crate's source and the
                             platform's Cargo.lock (in the ASSEMBLY);
                             tools/frontend-crates/platform-crates.zip and
                             THIRD-PARTY-NOTICES-PlatformCrates.txt;
                             buildTransitive/<id>.targets
    BackendMapper            the four templates (in the assembly)
    each host-tools package  tools/frontend-tools/<rid>/: dx, wasm-bindgen,
                             LICENSE-Dioxus.txt, LICENSE-WasmBindgen.txt,
                             THIRD-PARTY-NOTICES-Dioxus.txt,
                             THIRD-PARTY-NOTICES-WasmBindgen.txt;
                             buildTransitive/<id>.targets
MAINTAINER-README.txt, EXTRAS-README.txt, README-INDEX.txt, the tests, the
maintainer tool, the tool sources and the whole samples/ folder are repo-only
and are NOT packed.

PACKAGE SIZE. The crates archive is almost all of the FrontendBuilder package,
and a host-tools package is almost all its two programs. The repository
owner's limit is 100 MB per package: look at the sizes after every
re-vendoring and every adopted tool build, and raise it with him before a
package comes near it.

PackageLicenseExpression is Apache-2.0 and PackageRequireLicenseAcceptance is
true. The repository owner publishes.


THE MAINTAINER TOOL: LibraryChecker
===================================
    dotnet run --project libs/CodeBrix.WebPlatform.LibraryChecker
                                                   (every check)
    dotnet run --project libs/CodeBrix.WebPlatform.LibraryChecker -- <check>
    ... -- notices --write                         (regenerate the notices)
    ... -- revendor | revendor-tools               (re-vendor; network)
    ... -- proof                                   (Node-free proof; Docker)

It finds the repository root by walking up to the folder that holds
CodeBrix.WebPlatform.slnx and tools/licensing/license-allowlist.json (or take
--root). It checks ONLY what the repository holds outside samples/ (see THE
RULES OF THE LIBRARIES, THE LICENCE SCOPE). Run every check before a
commit that touches a vendored tree, a patch, the allowlist or a notices
header; its tests run every check too. The checks, by group:

  no-node    no package.json, node_modules, Node lock file or JavaScript /
             TypeScript source in the files the repository owns outside
             samples/ (the vendored trees, the tool sources, the committed
             programs, the feed and build output are not scanned)
  vendor     the platform's crates (Rust/vendor) hold exactly the crates the
             crate set's Cargo.lock names, every file as cargo wrote it; the
             shared crate's own Cargo.lock names only those crates, at those
             versions; the tools' vendor folder holds exactly what the two
             tools' Cargo.lock files name, untouched; git ignores no file of
             the vendored trees or the tool sources
  patches    each tool source is its crates.io archive plus the numbered
             patches recorded for it, byte for byte (one check per tool)
  licenses   the platform's crates compiled into a platform application (the
             wasm32 build of the crate set) against the strictest tier; all
             of the platform's crates against the vendored tier; each tool's
             vendored tree against the tooling tier and, for each of the five
             platforms, the crates compiled into the distributed program
             against the strictest tier plus the exceptions accepted by name;
             and every vendored crate of both folders audited
  notices    the four generated notices files are what the audits produce:
             THIRD-PARTY-NOTICES-PlatformCrates.txt (Rust/notices),
             THIRD-PARTY-NOTICES-Dioxus.txt, -WasmBindgen.txt and
             -ToolSources.txt (tools/dx-native-tools/notices)

LICENSE TIERS (tools/licensing/license-allowlist.json):
  everywhere          allowed anywhere, including what is compiled into an
                      application and into the distributed programs
  vendoredButNot...   allowed in a vendored crate that is NOT linked into an
                      application
  toolingOnly         allowed only in the tools' tree (not in what the
                      distributed programs link)
  compiledIntoTheDistributedTools
                      the exceptions accepted inside dx and wasm-bindgen: an
                      SPDX exception list, reviewed bundled files, and
                      licences accepted for NAMED crates only (MPL-2.0 for
                      thirteen crates of dx - smartstring, which came back
                      with dx's project templates, is the thirteenth -
                      CDLA-Permissive-2.0 for webpki-roots)
"Linked into" is decided by asking cargo which crates the build links
(`cargo metadata` for the target, without build scripts' and procedural
macros' dependencies), not by guessing. A crate that offers a choice
("A OR B") passes when one choice is allowed; the notices record which was
elected. "A AND B" needs every part allowed.

WHEN THE GATE REJECTS SOMETHING, do not work round it. The options, in order:
  1. switch off the feature that pulls the crate in, so it leaves Cargo.lock;
  2. replace the dependency with code of our own;
  3. patch a tool so that it no longer needs the crate (tools only; see
     PATCHES);
  4. ask the repository owner to approve the licence, or to accept one
     crate's bundled files with a recorded reason (a "bundled license
     override").
ONLY THE REPOSITORY OWNER edits license-allowlist.json.

The bundled-file scan recognises well-known licence texts in files named like
licence files. A text it does not recognise is recorded but not judged, and
source-file headers are not read. It is a net, not a legal review.


VENDORED SNAPSHOTS
==================
Rust/vendor (the platform's crates) and tools/dx-native-tools/sources/vendor
(the tools' crates) are point-in-time snapshots, exactly as crates.io
published them. They are never edited:
  - `cargo vendor` records a checksum for every file, and cargo refuses to
    build from a crate whose files no longer match;
  - `LibraryChecker vendor` reports any modified, missing or added file.
Do not put notes or patches inside a vendored crate folder. `cargo vendor`
rewrites those folders, and the check would flag the extra file. What we know
about a library lives in Rust/vendoring/<library>/ (the platform's crates) or
tools/dx-native-tools/vendoring/<tool>/ (the tools) instead.

`cargo vendor` copies every crate a Cargo.lock names: crates for other
operating systems, build-time helpers and optional dependencies included. So
the folders hold far more than what is linked.

THE CRATE SET decides the platform's crates: Rust/platform-crates/Cargo.toml
names the shared crate (as a path dependency) and every Dioxus feature an
application may use, and its Cargo.lock fixes the versions. Adding a crate or
a feature to what applications get offline is a change there, followed by
`LibraryChecker revendor` - and it is a new crate set, so the owner approves it
first.


PATCHES
=======
A third-party source may be changed only as a last resort and only with the
repository owner's approval. Today the one patched source is the Dioxus CLI.

  - The pristine source is the .crate archive in
    tools/dx-native-tools/vendoring/<tool>/, with its crates.io checksum in
    the .sha256 file beside it.
  - Each change is one numbered patch file in that folder, in unified diff
    format with paths that start a/ and b/.
  - A file that a text diff cannot carry (a binary) is removed with one
    extra line in a patch file, on its own outside any hunk:
        delete-file sha256:<the file's SHA-256> a/<path>
    The file must exist and have exactly that checksum, or the patch fails.
    This line is LibraryChecker's own form; the standard `patch` program
    ignores it, so remove such a file by hand when applying a patch file
    with that program.
  - PATCH-NOTES.txt in that folder says, for each patch: what it changes,
    why, what effect it has, and when it can be dropped.
  - The working copy, tools/dx-native-tools/sources/<tool>/, must equal
    archive + patches.
  - Every changed place in the source carries a comment starting
    "CodeBrix change:".

To make or change a patch: edit the working copy, then write the patch files
again from a pristine unpack of the archive (each patch is the difference
between two successive states, so that they apply in order), update
PATCH-NOTES.txt, run `LibraryChecker patches`, and rebuild the programs
(tools/dx-native-tools/README.txt).


UPGRADING A VENDORED LIBRARY (generic procedure)
================================================
The libraries here move quickly. The procedure is the same every time; the
UPGRADE-README.txt of the library (Rust/vendoring/<library>/ or
tools/dx-native-tools/vendoring/<tool>/) adds what is particular to it,
including which other libraries have to move with it. Read it first.

 1. CHANGE THE PIN. For a platform crate: the version in the crate set's
    Cargo.toml (and in the shared crate's, when it names the crate). For a
    tool: the .crate archive and unpacked source (the tool's UPGRADE-README
    says how).
 2. RE-RESOLVE AND RE-VENDOR. `LibraryChecker revendor` (the platform's crates)
    or `LibraryChecker revendor-tools` (the tools). This is the only step that
    needs network access; it lets cargo re-resolve the lock file against
    crates.io and rewrites the vendor folder. Let cargo re-resolve the shared
    crate's own Cargo.lock too (`cargo update -p <crate> --offline` in its
    folder, now that the crates are vendored).
 3. RUN THE GATE. `LibraryChecker`. Every new or changed crate is judged.
    Anything rejected goes through "WHEN THE GATE REJECTS SOMETHING" above
    before the upgrade continues. Do not commit a vendor folder the gate
    rejects.
 4. RE-APPLY THE PATCHES (tools). Apply each numbered patch to the new
    source in order. One that no longer applies is re-made by hand against
    the new code, guided by its PATCH-NOTES.txt entry; first check whether
    upstream has made it unnecessary.
 5. REGENERATE THE NOTICES. `LibraryChecker notices --write`, and read the
    difference.
 6. BUILD AND TEST. The solution (the package re-packs the crates), the
    shared crate's Rust tests, the tools' native builds when a tool changed
    (tools/dx-native-tools), the temporary feed refreshed and the WorkSite
    sample built, tested and looked at in a browser, and `LibraryChecker
    proof`. Check the package sizes (PACKAGING AND PUBLISHING).
 7. UPDATE THE RECORDS. The library's UPGRADE-README.txt and PATCH-NOTES.txt
    must describe what is now true.

Keep a snapshot change separate from changes to our own code: a re-vendor
replaces hundreds of folders, and a commit that mixes the two cannot be
reviewed.


NOTICES
=======
THE ROOT THIRD-PARTY-NOTICES.txt is packed into every package, so it is short
and written by hand: what the LICENSE covers, the third-party code inside the
assemblies (the SignalR client's origin, with its licence text), and an entry
for each piece of third-party software a package CARRIES - the platform's
crates (FrontendBuilder) and dx and wasm-bindgen (host tools) - with the
accepted exceptions stated plainly and a pointer to the GENERATED notices that
travel with them. Never paste generated text into it.

THE GENERATED FILES (written by `LibraryChecker notices --write` from the
same licence audits the gate judges; each is a hand-written header plus the
generated part; edit only the header):
  Rust/notices/THIRD-PARTY-NOTICES-PlatformCrates.txt
      header notices-header-PlatformCrates.txt beside it, which carries the
      SignalR client's notice too, so the file is complete for a built site.
      Packed into the FrontendBuilder package and handed to applications by
      PlatformCrates.ReadNotices.
  tools/dx-native-tools/notices/THIRD-PARTY-NOTICES-Dioxus.txt, -WasmBindgen
      .txt
      what is compiled into each distributed program on any of the five
      platforms. Copied beside the programs into every
      native_libraries/<rid>/ folder after regenerating (the HostTools tests
      check the copies), and so packed into the host-tools packages.
  tools/dx-native-tools/notices/THIRD-PARTY-NOTICES-ToolSources.txt
      every crate vendored for the tools. Repository-only.
After regenerating, read the difference; when the platform crates' file
changed, the next build re-packs it.

The root LICENSE covers the code written here. Everything third-party keeps
its own licence. Do not describe the repository as "Apache-2.0" without that
qualification anywhere.


GIT AND THE VENDORED FOLDERS
============================
Two things would silently break a clone, so the repository guards against
both:
  - LINE ENDINGS. .gitattributes keeps text files LF everywhere, and marks
    src/CodeBrix.WebPlatform.FrontendBuilder/Rust/vendor and
    tools/dx-native-tools/sources `-text` so git never touches a byte in them
    (some upstream files have Windows line endings, and their checksums - and
    the patch check - cover the exact bytes).
  - IGNORED FILES. The last lines of the root .gitignore un-ignore everything
    inside those two trees, because a general ignore rule (bin/, debug/,
    *.log, *.env ...) would otherwise hide a file a vendored crate needs, and
    tools/dx-native-tools/.gitignore starts with "!*" for the same reason
    (and ignores only output/ and a tool's target/ folder). `LibraryChecker
    vendor` asks git and fails if any such file is ignored. Check by hand
    with `git status --ignored --porcelain`: nothing inside the two trees may
    be listed. The upstream .gitignore inside the dioxus-cli source ignored
    its own Cargo.lock; a patch removes that line.

Never commit: any `target` folder, temp-packages/, tools/dx-native-tools/
output/, and what the sample's build writes (see its maintainer file). The
.gitignore at the repository root covers all of them.


THE HOST-TOOLS PROGRAMS
=======================
dx and wasm-bindgen are built from tools/dx-native-tools/sources by the
scripts in tools/dx-native-tools (Linux in a pinned container with the
network off; Windows and macOS scripts written for the owner's machines),
gated, and ADOPTED into native_libraries/<rid>/ with a provenance record.
Start at tools/dx-native-tools/README.txt; what was built, and the
reproducibility record, is in its BUILD-PROVENANCE.txt. The Linux build is
reproducible: a rebuild gives byte-identical programs. The versions are
pinned in tools/dx-native-tools/linux/pins.env and named again in each
package's HostTools class (and checked by the HostTools tests).

dx's six patches (tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt):
  0001 image encoders without imagequant / mozjpeg
  0002 libgit2-sys -> the declarations-only stand-in (no libgit2 source;
       dx links the prebuilt libgit2 of the CodeBrix.LibGit2 package)
  0003 web builds without esbuild
  0004 the regenerated Cargo.lock
  0005 no prebuilt OpenSSL archive
  0006 DX_SKIP_WASM_OPT: an unshrunk release build on request
Every dx command is there (`dx new`, `dx init`, `dx components` included).
BUILD PREREQUISITE on Linux: the OpenSSL DEVELOPMENT HEADERS (the derived
build image installs them; a bare Debian host needs `sudo apt install
libssl-dev`), because dx's project templates bring git2's `https` feature and
with it the openssl-sys crate, whose build script reads them. Nothing of
OpenSSL is linked into dx; the build gate proves it. libgit2's source never
enters this repository; `LibraryChecker vendor` checks that.


THE NODE-FREE PROOF
===================
    dotnet run --project libs/CodeBrix.WebPlatform.LibraryChecker -- proof
        [--frontend <materialized frontend folder>]

A maintainer test of the platform, not package functionality: it builds a
MATERIALIZED frontend - by default the WorkSite sample's, which uses every
part of the platform (materialize it with the sample's `generate` first) -
in a throw-away container that has Rust but no Node.js, npm or other
JavaScript runtime (probed: the proof fails if one is found), with the
container's network switched off. It copies in the frontend with its crates,
the standalone Tailwind program from PATH, the WasmShrink package's Linux
wasm-opt, the Linux dx and wasm-bindgen of the host-tools packages (the
maintainer tool references the LinuxX64 and LinuxArm64 package projects) and
the CodeBrix.LibGit2 package's Linux libgit2 under the name dx asks for (on
dx's LD_LIBRARY_PATH in the container), and refuses an image that has a
wasm-opt, dx or wasm-bindgen of its own. Nothing
is compiled but the frontend. It needs Docker (CodeBrix.RustTools.Docker).


RULES WHEN CODE MOVES OUT OF THE SAMPLE
=======================================
Functionality is built and proved in samples/WorkSite, then moved into a
library so that every application gets it. At that moment it stops being
governed by WorkSite's rules and is judged under the libraries' rules above:
each rule it carried either becomes a platform guarantee, becomes a consumer's
option, or stays behind in WorkSite.

  - Remove every reference to the application's own types (decouple through
    configuration and hooks first; this is a real change, not a file move).
  - Move its third-party notice out of samples/WorkSite/THIRD-PARTY-NOTICES.txt
    into the right place in the same step: the root THIRD-PARTY-NOTICES.txt
    for what is in an assembly or carried by a package (deliberately - it is
    packed into every nupkg), and the platform crates' notices header when
    it is compiled into applications.
  - Update AGENT-README.txt and the WorkSite maintainer file in the same
    step.

The SignalR client moved this way, and so did the vendored crates, the tool
sources, the licence gate and the tool build: what is general is the
platform's, and the sample holds only what is its own.


TEMPORARY PACKAGES
==================
Some CodeBrix packages are not on nuget.org yet. Until they are, nuget.config
at the repository root adds the folder temp-packages/ beside it (git ignores
it) as a package source for EXACTLY these package ids; everything else comes
from nuget.org:

    CodeBrix.WebPlatform.Core.ApacheLicenseForever           built here
    CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever  built here
    CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever
                                                             built here
    CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever,
      .LinuxArm64, .WindowsX64, .MacOSArm64, .MacOSX64 (same suffix)
                                                             built here
(those eight and nothing else; CodeBrix.RustTools, CodeBrix.RustTools.Docker,
CodeBrix.LibGit2 and CodeBrix.WebPlatform.WasmShrink come from nuget.org)

The one feed serves both solutions. NuGet finds nuget.config by walking up
from each project, so every project under the repository uses it:
    samples/WorkSite/backend/libs/WorkSite.Core   Core
    samples/WorkSite/build/WorkSite.BuildFrontend FrontendBuilder (it brings
                                                  Core and BackendMapper; and
                                                  from nuget.org RustTools,
                                                  WasmShrink and LibGit2)
                                                  and the host-tools package
                                                  of the machine
A fresh clone cannot restore those projects until the packages are published
or the same .nupkg files are put in temp-packages/.

AFTER CHANGING A LIBRARY OF THIS REPOSITORY (WorkSite does not see the change
until this is done; do it once, after the last library change):
  1. dotnet build CodeBrix.WebPlatform.slnx --configuration Release
  2. The eight new .nupkg files are in src/<project>/bin/Release/. Check that
     they carry the SAME version (the date stamp); if not, build again.
  3. In temp-packages/, delete the older CodeBrix.WebPlatform.Core,
     .BackendMapper, .FrontendBuilder and the five
     .FrontendBuilder.<Platform> files, and copy the eight new ones in.
  4. Set that version in
       samples/WorkSite/backend/libs/WorkSite.Core/WorkSite.Core.csproj
       samples/WorkSite/build/WorkSite.BuildFrontend/
         WorkSite.BuildFrontend.csproj
         (one property, WebPlatformPackageVersion, for the FrontendBuilder
         package and the five host-tools references)
  5. Build WorkSite.slnx. Every refresh has a new version, so NuGet's cache
     never serves an old copy.

CodeBrix.WebPlatform.WasmShrink IS PUBLISHED and comes from nuget.org (it was
served from this feed until 2026-11-18). To take a newer published version,
set it in src/CodeBrix.WebPlatform.FrontendBuilder/
CodeBrix.WebPlatform.FrontendBuilder.csproj, then refresh the packages of
this repository as above (the FrontendBuilder package names the WasmShrink
version it was built with; WorkSite gets WasmShrink through it).

CodeBrix.RustTools IS PUBLISHED (it was served from this feed until
2026-11-19) and CodeBrix.LibGit2 is published too; both come from nuget.org.
To take a newer published version, set it in the FrontendBuilder library's
project file (RustTools also in the maintainer tool's, with RustTools.Docker),
then refresh the packages of this repository as above. A newer CodeBrix.LibGit2
must carry the libgit2 release dx was linked against: the library refuses to
start dx with a library of another release, naming both.

THE GOAL IS TO DELETE THE FEED. When the packages are on nuget.org: delete
nuget.config and temp-packages/, set the published versions in the project
files, and remove this section, the temp-packages lines of .gitignore and the
"Local builds" comments in the project files.


PROVENANCE AND VENDORED SOURCES
===============================
The assemblies contain one piece of third-party source: the shared Rust
crate's SignalR client (src/signalr) started from the source of the
signalr-client crate (MIT License) and was reworked; protocol.rs and
connection.rs carry a header naming the origin, and the root
THIRD-PARTY-NOTICES.txt carries the notice with the original licence text.
Keep both if those files change. The rest of the crate - the set-up call,
REST transport, error type, query-string, date and browser code, the rest of
the SignalR module - was written for the WorkSite sample in this repository
and moved here.

The repository also CARRIES third-party code, all of it outside the
assemblies and recorded in generated notices (NOTICES above): the platform's
crates (src/CodeBrix.WebPlatform.FrontendBuilder/Rust/vendor, shipped in the
FrontendBuilder package as an archive), the tool sources and their crates
(tools/dx-native-tools/sources; dioxus-cli patched), and the programs built
from them (native_libraries/, shipped in the host-tools packages). The
sample carries none.


CODING CONVENTIONS
==================
.NET
  - Target net10.0. Nullable reference types are OFF: never '?' on a
    reference type, never the '!' operator. Value-type nullables are fine.
  - No <ImplicitUsings>, no global usings. Every file lists its own usings at
    the top, System.* first, then the rest alphabetically; no usings below
    the namespace line.
  - File-scoped namespaces. One type per file. Sources in sub-folders that
    match their namespaces.
  - XML doc comments on every public and protected member. Internal types
    carry a summary where it helps the next reader.
  - No fabricated file headers.
  - The maintainer tool (libs/) follows the same conventions; it is no
    package, so it may reference what the packages may not (the container
    library for the proof build).
  - Tests: files named <ClassUnderTest>Tests.cs; methods
    MemberName_snake_case_description or snake_case_description;
    multi-statement tests carry //Arrange //Act //Assert comments and
    single-statement tests are expression-bodied; assertions in the
    SilverAssertions fluent form. (The two fixture backends group their small
    types in a few files; they are test data.)

The mapper
  - Attributes are recognised BY FULL TYPE NAME and read as data
    (Reading/KnownNames.cs, Reading/Attributes.cs). A metadata-only context
    cannot instantiate an attribute, and some reflection calls are not
    supported there (MethodInfo.GetBaseDefinition, for one); read flags and
    names instead.
  - A reader never throws for something wrong in the backend: it records a
    MappingProblem with a location and carries on, so that every problem is
    reported at once. Only "the assembly cannot be read" throws directly.
  - A problem message says what is wrong AND what to do about it.
  - All Rust naming and typing decisions are made in C# (Rust/View/
    ViewBuilder.cs). The templates lay text out; keep logic out of them.
  - Anything the mapper cannot see (run-time configuration) is a platform
    rule in AGENT-README.txt, not a guess in the code. A FACT about the
    backend the frontend needs (a hub's path, the request marker, the
    session-refresh action) is DECLARED in the backend with a Core attribute
    the backend can also read back at run time, so that there is one source.
  - A rule that depends on sign-in (the marker and the refresh action are
    required only then) counts actions and hubs that need sign-in even when
    they were dropped for other problems (MapContext.SignInSeen), so that
    every problem is still reported in one pass.

Rust (the shared crate)
  - No dependency on any application's types: what only the application
    knows comes in through the ONE set-up call (setup.rs:
    codebrix_webplatform::configure with a PlatformConfig), which the
    generated configure_platform (mod.sbn) makes with what the backend
    declares. A new setting goes into PlatformConfig and is passed on from
    there; the per-client configure functions stay as the layer underneath.
  - What every platform page needs from the browser and cannot get from
    Dioxus under the site's Content-Security-Policy (the page title) goes in
    browser.rs.
  - Pure logic compiles and is tested on the host; browser facilities go
    through web-sys / js-sys only, in browser.rs and, for the SignalR client,
    signalr/web.rs and signalr/runtime.rs (wasm32 only; their native
    counterparts are the fake hub and the virtual clock).
  - APPLICATION CODE THE CRATE CALLS from its own tasks or from browser
    callbacks (hub handlers, the session refresh, the REST client's
    on_refreshed / on_session_ended) runs through app_runtime::AppRuntime:
    captured where the application's Dioxus runtime is current, entered
    again (at the root scope) when the code runs. An application never wraps
    anything itself. Anything new of that kind goes the same way, with a
    test that writes a global signal from outside the runtime.
  - A HUB TOKEN IS NEVER USED ACROSS A CHANGE OF SESSION COOKIES. ASP.NET
    Core's connection dispatcher binds the connection token to the user
    who negotiated it (by the `sub` claim) and answers the socket 403 when
    another user, or nobody, opens it. The REST client (api/client.rs) keeps
    note of the state-changing requests in flight - the only ones that set
    or clear the cookies - and the connection task (establish in
    signalr/connection.rs) negotiates only while none is, and negotiates
    again when one ran meanwhile. Any new code path that changes the
    cookies goes through send_once (or holds a StateChangeInFlight) so
    that the hubs see it. The fake hub refuses such a socket the same way,
    so a test shows a regression.
  - A `//!` module comment on every file. The two files derived from
    third-party code (signalr/protocol.rs and signalr/connection.rs) keep
    their origin header; see PROVENANCE.

Documentation
  - AGENT-README.txt is consumer-only and carries NO version numbers.
  - README.md follows the family template: no upstream references, no
    versions, sizes or dates that go stale, no history.
  - Describe policy, not counts that change ("every hub", not a number).


NOTES
=====
  - WorkSite consumes the libraries as NuGet packages from the temporary
    feed until they are published. After changing a library, WorkSite does
    not see the change until the feed is refreshed (TEMPORARY PACKAGES).
  - The family's document collector reads README-INDEX.txt; keep every path
    it lists real.
  - When this repository is first published, add it to the family's list of
    repositories and to the document collector's list.

================================================================================
END OF MAINTAINER-README
================================================================================
