================================================================================
AGENT-README: CodeBrix.WebPlatform
A Guide for AI Coding Agents — CONSUMING the
CodeBrix.WebPlatform.Core.ApacheLicenseForever,
CodeBrix.WebPlatform.Backend.ApacheLicenseForever,
CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever,
CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever and
CodeBrix.WebPlatform.FrontendBuilder.<Platform>.ApacheLicenseForever (the
five host-tools packages) NuGet packages
================================================================================


OVERVIEW
========

CodeBrix.WebPlatform is a platform for building web applications with

    * a .NET backend that serves REST controllers and SignalR hubs and renders
      no UI, and
    * one or more frontend applications written in Rust, compiled to
      WebAssembly, that talk to that backend (see SEVERAL FRONTEND
      APPLICATIONS).

No Node.js is used, relied on or talked to by anything in these packages, and
the platform assumes none is installed. The frontend is Rust; the only
JavaScript in a platform application is the glue the Rust toolchain generates
to connect WebAssembly to the browser.

This file documents every package of the platform. They target .NET 10
(net10.0).

    CodeBrix.WebPlatform.Core
        A plain .NET library with no web framework dependency. EVERY platform
        backend references it. It holds
          - the attributes a backend uses to declare its contract, and what
            it declares about itself (its request marker, the action that
            renews an expired session),
          - the standard error body (ApiProblem) and its codes,
          - the description of the frontend applications (their names and
            where each is served), which the frontend build and the backend
            share,
          - the registry of crate bundles: Rust crates that a NuGet package
            brings to the frontend (see CRATE BUNDLES).

    CodeBrix.WebPlatform.Backend
        The ASP.NET Core plumbing every platform backend needs, in one
        services call and one pipeline call. It serves the frontend
        applications the frontend build wrote into the web root (with the
        build's compressed copies, to a browser that accepts them), answers
        what the platform's rules say about API errors, demands the
        backend's request marker, checks the Origin of hub requests and sends
        the security headers. Every piece can be switched off. On request it
        also compresses the backend's own answers (off by default; see
        COMPRESSION). Referenced by
        the backend (it brings Core). It is the one package with ASP.NET Core
        code; Core still has none.

    CodeBrix.WebPlatform.BackendMapper
        A BUILD-TIME library. It reads the compiled backend (its metadata
        only; the backend is never run), finds every REST controller and
        SignalR hub, and writes the typed Rust client code the frontend calls
        them with. It REFUSES a backend whose contract is not fully declared,
        and lists every gap at once. It is referenced by the program that
        builds the application, never by the backend or the frontend, and it
        is not part of the built application.

    CodeBrix.WebPlatform.FrontendBuilder
        A BUILD-TIME library: the one a build PROGRAM calls to build the
        frontend applications. It checks the machine and says what is
        missing; it MATERIALIZES every frontend application (builds and maps
        the backend with BackendMapper, writes the generated client code, the
        shared Rust crate every platform frontend compiles in, the platform's
        Rust crates and those of the registered crate bundles, the cargo
        configuration and, when it is missing, the Tailwind stylesheet, so
        that plain cargo works offline); and it BUILDS the servable site of
        every application into the backend's web root, or SERVES one with hot
        reload, by running the Dioxus build tool (dx), with release builds
        shrunk by wasm-opt, Brotli and gzip copies of the built files in the
        web root (see COMPRESSION) and, on request, the WebAssembly split
        into pieces that load when they are needed (see CODE SPLITTING). It
        carries the shared Rust crate (the one set-up call, the REST client,
        the SignalR client, the browser helpers and the other code every
        frontend needs) inside its assembly, and the
        platform's crates - every crate a platform frontend may use without
        adding its own, exactly as crates.io published them - as one archive
        beside it, with their third-party notices. Like BackendMapper it is
        referenced by the build program only and is not part of the built
        application.

    CodeBrix.WebPlatform.FrontendBuilder.<Platform> (five packages)
        BUILD-TIME host tools: the prebuilt dx and wasm-bindgen programs for
        ONE build machine (Linux x64 or ARM64, Windows x64, macOS on Apple
        Silicon or Intel), with their licence and notices files. The build
        program references the one for the machine it is built on; nothing
        is compiled or installed for them (no C compiler is needed).

The idea: nobody writes or maintains a contract document, and nobody writes
the frontend's API client by hand. The backend's controllers and hubs ARE the
contract; the frontend's client code is generated from them on every build.
The split: HOW to talk to a backend is the platform's (the shared crate); the
DESCRIPTION of a particular backend is generated on every build (BackendMapper);
only WHAT THE APPLICATION DOES with the answers is the application's code. The
platform supplies no interface components (forms, dialogs, toasts, icons) and
no session handling: those are the application's. The backend's PLUMBING -
serving the frontend applications, the standard error answers, the
cross-site checks, the security headers - is the platform's too (the Backend
package); the backend's controllers, hubs, sign-in and data are its own.


INSTALLATION
============

    dotnet add package CodeBrix.WebPlatform.Backend.ApacheLicenseForever
        (brings CodeBrix.WebPlatform.Core.ApacheLicenseForever)
    dotnet add package CodeBrix.WebPlatform.Core.ApacheLicenseForever
        (for a project of contract classes beside the backend)
    dotnet add package CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever
        (brings CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever)

IMPORTANT: the package ids carry the ".ApacheLicenseForever" suffix. There are
no packages named plain CodeBrix.WebPlatform.Core,
CodeBrix.WebPlatform.Backend, CodeBrix.WebPlatform.BackendMapper or
CodeBrix.WebPlatform.FrontendBuilder. The assembly names and root namespaces
are CodeBrix.WebPlatform.Core, CodeBrix.WebPlatform.Backend,
CodeBrix.WebPlatform.BackendMapper and CodeBrix.WebPlatform.FrontendBuilder.

Which project references what:

    the backend (the ASP.NET Core project)               Backend (it brings
                                                         Core)
    every other project that holds contract classes      Core
      or hub client interfaces
    the program that builds the frontend                 FrontendBuilder (it
                                                         brings BackendMapper
                                                         and Core), and the
                                                         host-tools package
                                                         for the machine
    the frontend applications (Rust)                     none; they get Rust
                                                         source and the
                                                         platform's crates,
                                                         below

A backend may leave the Backend package out and write that plumbing itself
(see THE PLATFORM RULES); it then references Core alone.

NuGet dependencies:
    Core            none. It uses only the .NET base class library.
    Backend         CodeBrix.WebPlatform.Core.ApacheLicenseForever and
                    CodeBrix.TomlParse.BsdLicenseForever (it reads the
                    frontend manifest), and the ASP.NET Core shared
                    framework (a framework reference, as in every backend).
    BackendMapper   CodeBrix.WebPlatform.Core.ApacheLicenseForever,
                    CodeBrix.Templating.BsdLicenseForever (the template
                    engine the Rust code is written with) and
                    System.Reflection.MetadataLoadContext.
    FrontendBuilder CodeBrix.WebPlatform.Core.ApacheLicenseForever,
                    CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever,
                    CodeBrix.WebPlatform.WasmShrink.ApacheLicenseForever (the
                    wasm-opt program, copied beside the build program),
                    CodeBrix.LibGit2.MitLicenseForever (the libgit2 library dx
                    needs to start; see DX NEEDS libgit2) and
                    CodeBrix.RustTools.MitLicenseForever (Rust toolchain
                    detection, Cargo.toml reading).

THE HOST-TOOLS PACKAGE. The dx, wasm-bindgen and Tailwind programs a build
runs are NOT in FrontendBuilder: they are native programs, one package per
build platform. The build program references the one for the machine it is
built on:

    Linux x64             CodeBrix.WebPlatform.FrontendBuilder.LinuxX64
    Linux ARM64           CodeBrix.WebPlatform.FrontendBuilder.LinuxArm64
    Windows x64           CodeBrix.WebPlatform.FrontendBuilder.WindowsX64
    macOS, Apple Silicon  CodeBrix.WebPlatform.FrontendBuilder.MacOSArm64
    macOS, Intel          CodeBrix.WebPlatform.FrontendBuilder.MacOSX64
                          (each id ends in .ApacheLicenseForever)

Each copies its programs beside the build program, as
frontend-tools/<rid>/dx[.exe] and frontend-tools/<rid>/wasm-bindgen[.exe]
(rid = linux-x64, linux-arm64, win-x64, osx-arm64, osx-x64). Each depends on
CodeBrix.WebPlatform.Styling.Tailwind4.<Platform>.MitLicenseForever (same
platform names), which copies the standalone Tailwind program beside them as
tailwind/<rid>/tailwindcss[.exe] - so Tailwind is never installed (see
FRONTENDBUILDER API REFERENCE: THE PACKAGED TAILWIND). Those five are
the ONLY machines a platform application can be built on. A build program
without the package can name its own dx and wasm-bindgen in the options
instead (see FRONTENDBUILDER API REFERENCE: HOST TOOLS).

WHICH ONE TO ADD. The package of the machine that BUILDS and runs the build
program - not of the server the site is deployed to. A build program that is
built on more than one kind of machine names all five, each for its own
platform, so that it restores everywhere:

    <ItemGroup Condition="$([MSBuild]::IsOSPlatform('Linux')) And
        '$([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)' == 'X64'">
      <PackageReference Include="CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever" Version="..." />
    </ItemGroup>
    (and the same with 'Linux' / 'Arm64' -> LinuxArm64, 'Windows' / 'X64' ->
    WindowsX64, 'OSX' / 'Arm64' -> MacOSArm64, 'OSX' / 'X64' -> MacOSX64)

Use the same version for FrontendBuilder and the host-tools package (they
are published together), and the same release for the backend's Backend and
Core (the backend reads the manifest the frontend build writes, and the
generated code is written against Core's attributes). CheckPrerequisitesAsync
says which package the programs came from, or which one to add.

THE PLATFORM'S CRATES come with FrontendBuilder itself: the package copies
them beside the build program as frontend-crates/platform-crates.zip (one
archive) with frontend-crates/THIRD-PARTY-NOTICES-PlatformCrates.txt.
MaterializeAsync writes them into the frontend (see THE PLATFORM'S CRATES).

Requirements for BackendMapper at build time: the .NET runtime it runs on must
have the ASP.NET Core shared framework installed beside it (it is, wherever the
backend itself can run or be built), so that the backend's references can be
resolved. Nothing native, no OS restrictions.

What a developer installs to BUILD a platform application (FrontendBuilder
checks all of it and says what is missing; it installs nothing):
    - the .NET SDK (the libraries target net10.0);
    - Rust (rustup), FrontendRequirements.MinimumRustVersion or later, with
      the WebAssembly target: `rustup target add wasm32-unknown-unknown`.
      Nothing in the platform compiles C code, and the platform's crates
      need no network access; Rust itself needs the system linker it asks
      for when it is installed (build scripts and macros are linked for the
      build machine): build-essential on Linux, the Xcode Command Line Tools
      on macOS, the Visual Studio Build Tools' "Desktop development with
      C++" on Windows.
Everything else arrives with the packages - the standalone Tailwind program
too: the host-tools package of the build machine depends on the
CodeBrix.WebPlatform.Styling.Tailwind4.<Platform>.MitLicenseForever package,
which puts it beside the build program (see HOST TOOLS). Tailwind is NOT
installed, and one installed on the machine is never used; nothing here uses
Node.js.

License: Apache-2.0 (every package). XML documentation ships with each. The
third-party code the packages carry keeps its own licence; see NOTICES FOR A
BUILT SITE and the THIRD-PARTY-NOTICES.txt in each package.


KEY NAMESPACES / USINGS
=======================

    using CodeBrix.WebPlatform.Core.Mapping;
        // WireRequiredAttribute, WireOptionalAttribute, WireTypeAttribute,
        // WireFileAttribute, WireIgnoreAttribute, WireHubAttribute,
        // WireRequestMarkerAttribute, WireSessionRefreshAttribute

    using CodeBrix.WebPlatform.Core.Problems;
        // ApiProblem, ApiProblemCodes

    using CodeBrix.WebPlatform.Core.Applications;
        // FrontendApplication, FrontendApplicationSet, FrontendManifest

    using CodeBrix.WebPlatform.Core.Crates;
        // CrateBundle, CrateBundles

    using CodeBrix.WebPlatform.Backend;
        // AddPlatformBackend, UsePlatformBackend (extension methods),
        // PlatformBackendOptions, PlatformBackendPaths, ResponseCompression

    using CodeBrix.WebPlatform.Backend.Problems;
        // ApiProblems, ApiProblemWriter, ApiProblemResult,
        // InvalidModelStateResponse

    using CodeBrix.WebPlatform.Backend.Applications;
        // FrontendManifestReader, FrontendManifestException

    using CodeBrix.WebPlatform.BackendMapper;
        // BackendMapper, BackendMapOptions, BackendMapException,
        // MappingProblem

    using CodeBrix.WebPlatform.BackendMapper.Model;
        // BackendMap, RestController, RestEndpoint, EndpointParameter,
        // ParameterSource, RouteSegment, HubMap, HubMethod, HubMethodKind,
        // HubMessage, HubArgument, WireType, WireTypeKind, WireStruct,
        // WireField, WireEnum, WireEnumMember

    using CodeBrix.WebPlatform.BackendMapper.TypeMapping;
        // RustTypeMap, RustType

    using CodeBrix.WebPlatform.BackendMapper.Rust;
        // RustClientGenerator, RustClientOptions, RustTemplates,
        // GeneratedFile

    using CodeBrix.WebPlatform.FrontendBuilder;
        // PlatformFrontend, PlatformFrontendOptions, FrontendConfiguration,
        // FrontendRequirements, MaterializeResult, FrontendBuildResult,
        // FrontendBuildException, FrontendCompression,
        // FrontendCompressionResult, CompressedFile

    using CodeBrix.WebPlatform.FrontendBuilder.Applications;
        // FrontendApplicationOptions, FrontendManifestWriter

    using CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;
        // StylesheetOutcome

    using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
        // PrerequisiteReport, PrerequisiteItem, PrerequisiteKind,
        // PrerequisiteStatus, WasmOptSource

    using CodeBrix.WebPlatform.FrontendBuilder.Tools;
        // HostTools, HostToolsSource

    using CodeBrix.WebPlatform.FrontendBuilder.Crates;
        // PlatformCrates, CrateMode, CrateBundleOutcome

    using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;
        // SharedRustCrate, SharedRustFile

THE ENUM ResponseCompression (CodeBrix.WebPlatform.Backend) is named like
ASP.NET Core's namespace Microsoft.AspNetCore.ResponseCompression; a `using`
of that namespace does not clash with it (a using brings in types, not the
namespace's name), so `ResponseCompression.FileAnswers` works as written.

NAME CLASH TO KNOW ABOUT: the static class BackendMapper has the same name as
its namespace. Inside a file that has `using CodeBrix.WebPlatform.BackendMapper;`
write `BackendMapper.Map(...)` as usual; from a namespace that itself contains
the word, qualify it: `CodeBrix.WebPlatform.BackendMapper.BackendMapper.Map`.


THE PLATFORM RULES
==================

BackendMapper reads metadata. Whatever a backend configures at run time (in
Program.cs) is invisible to it. So a platform backend follows these rules; the
mapper enforces the ones it can see and the generated client code assumes the
rest.

  JSON
    * camelCase property names on the wire, always (the ASP.NET Core and
      SignalR defaults). Do not change the serializer's naming policy.
    * An enum is written as its number unless the enum TYPE carries
      [JsonConverter(typeof(JsonStringEnumConverter))], in which case it is
      written as the member's name. Do not register an enum converter globally.
    * [JsonPropertyName] and [JsonIgnore] on a property are honoured.

  ROUTING
    * Attribute routing only: [Route] on the controller, [HttpGet] / [HttpPost]
      / [HttpPut] / [HttpDelete] / [HttpPatch] on each action. Conventional
      routes cannot be seen.
    * Every controller has [ApiController].
    * One HTTP method attribute and one route per action.

  WHO MAY CALL
    * Every action is covered by [Authorize] or [AllowAnonymous], on the
      action or on its controller. Every hub class carries one of them. A
      global authorization filter or fallback policy cannot be seen and does
      not count.

  ANSWERS
    * Every action declares its success answer (see DECLARING A REST ACTION).
    * Every error answer is an ApiProblem written as application/problem+json,
      including the ones ASP.NET Core produces by itself (a malformed body, an
      unknown /api route). The one exception: a 401 that means "not signed in"
      or "the session has ended" has an EMPTY body.
    * A request that changes state (anything but GET/HEAD) is refused unless
      it carries the X-Requested-With header with the application's value.
      The backend DECLARES that value once, [assembly: WireRequestMarker],
      and the check (the Backend package's, or the backend's own) reads it
      back from there (see below); the generated client code sends it.

  SIGN-IN
    * A backend with any action or hub that needs sign-in declares its
      request marker ([assembly: WireRequestMarker]) and marks the one action
      that renews an expired session ([WireSessionRefresh]): a POST anyone may
      call ([AllowAnonymous]) that takes nothing and answers 401 (empty body)
      when the session cannot be renewed. A backend where nothing needs
      sign-in needs neither for BackendMapper. (The Backend package's
      X-Requested-With check is on by default and needs a marker even then:
      declare one anyway, or switch the check off - see BACKEND API
      REFERENCE: THE OPTIONS.)

  HUBS
    * Every hub derives from Hub<TClient> with an interface that names each
      message it sends, and carries [WireHub("/path")].

The ANSWERS come from ASP.NET Core code, and the Backend package has all of
it: a backend that makes its two calls (builder.Services.AddPlatformBackend
and app.UsePlatformBackend, see BACKEND API REFERENCE) gets the problem
bodies for what the framework answers by itself, the model-binding answer,
the unknown-API answer, the X-Requested-With check, the hub Origin check, the
security headers and the serving of the frontend applications. A backend may
still write any of it itself (switch that piece off in the options) or leave
the package out altogether. Core itself has no web framework dependency.


SEVERAL FRONTEND APPLICATIONS
=============================

A platform application has ONE backend and ONE OR MORE frontend
applications. Each frontend application is a separate Dioxus application:
its own Rust crate, in its own folder, with its own Cargo.toml, Cargo.lock,
Dioxus.toml and pages. They all share the backend, the generated client code
(it is written into every one of them), the platform's crates and the crate
bundles, and the one build program and build. A developer who wants
different backends makes separate platform applications.

THE DEFAULT APPLICATION. The application that is given NO name is the
default application. Its internal name is "app" (FrontendApplication
.DefaultName) and it is served at the site root - https://host/ - which is
what a platform application with one frontend application normally has. At
most one application has no name, and no application can be NAMED "app".

A NAMED APPLICATION is served below its name: "sales" at https://host/sales
and every address below it. That goes for the only application too, if the
developer names it. Inside an application its routes are written from its
own root (#[route("/")] is https://host/sales/ for "sales"); the build gives
dx the name as the base path, so the router and the assets follow it.

THE NAME RULES. A name is lowercase letters (a-z), digits and hyphens; it
starts with a letter, does not end with a hyphen, and has at most 40
characters (FrontendApplication.MaximumNameLength). "api" and "assets" are
the platform's. Capitals are REFUSED, not lowered: the name is the address
and the folder, exactly. FrontendApplication.DescribeNameProblem(name) says
what is wrong with a name, or null.

WHEN EVERY APPLICATION HAS A NAME, nothing of the platform's is at the site
root: "/" answers the developer's own wwwroot/index.html if there is one,
and 404 otherwise. Any address outside every application is 404 too (there
is no site-wide fallback).

EVERYTHING IS SHARED. Moving between applications is a FULL PAGE LOAD (each
is its own WebAssembly): link to another application with a plain anchor,
a { href: "/admin", "Admin" }, not with the router's Link. The API (/api) is
the same for all of them, and so is the session cookie: the cookie a sign-in
in one application sets is sent by the others too (what each application
shows about the session is its own code).

How the build program lists the applications: FRONTENDBUILDER API
REFERENCE (PlatformFrontendOptions.Applications). How the backend serves
them: BACKEND: HOW THE FRONTEND APPLICATIONS ARE SERVED.


CORE API REFERENCE: THE CONTRACT ATTRIBUTES
===========================================

All in CodeBrix.WebPlatform.Core.Mapping. They are read by BackendMapper and
have no effect at run time (except WireHubAttribute.GetPath and
WireRequestMarkerAttribute.GetValue, which the backend calls itself).

[WireRequired] / [WireOptional]
-------------------------------
Targets: class, struct, interface, method, property, parameter, return value.

A .NET reference type (string, a class, a collection) says nothing about
whether it can be null, but the Rust code must hold either `T` or `Option<T>`.
So EVERY reference-type property, parameter and return value of the contract
needs one of the two. The NEAREST declaration wins:

    1. on the property / parameter / return value itself, otherwise
    2. on the method, otherwise
    3. on the declaring class or interface.

The practical way: put [WireRequired] on each contract class and each hub
client interface, and [WireOptional] on the few members that may be null.

    [WireRequired]
    public sealed class Client
    {
        public Guid Id { get; set; }             // value type: always present
        public string Name { get; set; }         // required (from the class)
        [WireOptional]
        public string Notes { get; set; }        // Option<String> in Rust
        public decimal? HourlyRate { get; set; } // nullable value: optional
    }

Value types need neither attribute: `int` is always present, `int?` is
optional. ONE SPECIAL CASE: [WireRequired] placed DIRECTLY on a nullable
value-type property or parameter declares that the frontend always sends the
value even though the backend accepts null (typically so that it can answer a
missing value with its own validation message):

    [WireRequired]
    public Guid? ProjectId { get; set; }         // Rust: project_id: String

A declaration on the method or class does not have that effect.

What does NOT need a marker: the items of a list and the values of a
dictionary (always present), a request body parameter, a route parameter, the
success answer of a REST action.

Return value of a hub method: `[return: WireOptional]`, or the method/class
default.

[WireType("RustType")]
----------------------
Targets: property, parameter, return value. Overrides the Rust type for ONE
value.

    [WireType("Money", Import = "crate::money::Money",
              With = "crate::money::as_text")]
    public decimal Amount { get; set; }

    RustType   the type as written in the generated code
    Import     the path the generated code imports it from (optional)
    With       the module that reads/writes it as JSON, serde's `with`
               (optional)

For an application-wide change use RustTypeMap instead (below).

[WireFile]
----------
Target: method (a REST action). The action's answer is a file, not JSON. The
action must answer GET, reads no body and needs no declared response type.
The generated code gets TWO functions for it, with the same arguments:
  <name>_url(..) -> String
      BUILDS THE ADDRESS; the frontend hands it to the browser to download or
      show (browser::navigate).
  <name>_bytes(..).await -> ApiResult<Vec<u8>>
      FETCHES THE FILE for the frontend's own code (model data, glyph data,
      any binary blob), through the shared REST client like every other
      generated call: the session refresh and retry for an action that needs
      sign-in, and an error answer (the standard JSON problem body) as an
      ApiError. The whole answer is read into memory before it is returned.
      THE BROWSER'S CACHE: <name>_bytes asks with the fetch cache mode
      "no-cache" - the browser may keep the answer and asks the server every
      time whether it changed (If-None-Match with the stored ETag), so an
      unchanged file costs a 304 instead of a download. The backend's own
      answer decides: what its Cache-Control allows is kept, an answer with
      Cache-Control: no-store never is. An action that answers private data
      (one user's file) should answer Cache-Control: no-store. Every other
      generated call is sent "no-store" and never stored.
Both names are taken, so no other action of the same controller may be named
<Name>Url or <Name>Bytes; BackendMapper reports such a clash as a problem.
With the backend's CompressResponses = FileAnswers, the answers of [WireFile]
actions - and nothing else - are compressed as they are sent (see
COMPRESSION, and THE RISK there).

    // in MeshesController ([Route("api/meshes")], [Authorize])
    [HttpGet("{id:guid}/data.bin")]
    [WireFile]
    public async Task<IActionResult> Data(Guid id, CancellationToken token)
    {
        byte[] data = await _store.ReadAsync(id, token);
        return data == null
            ? new ApiProblemResult(ApiProblems.NotFound())
            : File(data, "application/octet-stream");
    }

    // frontend (Rust): read it ...
    use crate::generated::api::meshes;
    let data: Vec<u8> = meshes::data_bytes(&id).await.map_err(|e| e.message())?;
    // ... or let the browser download it
    codebrix_webplatform::browser::navigate(&meshes::data_url(&id));

[WireIgnore]
------------
Targets: class, method, property. Leaves a controller, hub, action, hub method
or property out of the generated code entirely. Use it for what the frontend
never calls: a health probe, a catch-all "no such route" controller of the
backend's own (the Backend package answers unknown /api addresses without
one).

[WireHub("/path")]
------------------
Target: class (a hub). Declares the path the hub is served at. The frontend
connects to it, and the backend maps the hub at the SAME path by reading it
back, so the two cannot drift apart:

    public static string WireHubAttribute.GetPath(Type hubType)
        // throws ArgumentNullException (null), InvalidOperationException
        // (no attribute, or a path that does not start with '/')

    app.MapHub<ClockHub>(WireHubAttribute.GetPath(typeof(ClockHub)));

[assembly: WireRequestMarker("value")]
--------------------------------------
Target: the backend ASSEMBLY (the one BackendMapper reads), once. Declares the
value the backend demands in X-Requested-With on every request that changes
state. The generated client code sends it (REST calls and the SignalR
negotiate request), and the check reads it back, so the two cannot drift
apart. The Backend package's check reads it by itself (from the backend's
assembly); a backend that writes its own check calls GetValue:

    [assembly: WireRequestMarker("MyApp")]    // e.g. in WireDeclarations.cs

    public const string WireRequestMarkerAttribute.HeaderName;
        // "X-Requested-With"
    public static string WireRequestMarkerAttribute.GetValue(Assembly assembly)
        // throws ArgumentNullException (null), InvalidOperationException
        // (no attribute, or a value that is not valid)
    public static bool WireRequestMarkerAttribute.IsValidValue(string value)
        // visible ASCII, single spaces inside, at most 100 characters

    // in a check of the backend's own (not needed with the Backend package):
    static readonly string Marker =
        WireRequestMarkerAttribute.GetValue(typeof(Program).Assembly);

Required when anything of the backend needs sign-in; BackendMapper reports
its absence (or a value that is not valid) as a problem.

[WireSessionRefresh]
--------------------
Target: method (a REST action), exactly one in the backend. Marks the action
that renews an expired session. It must answer POST, be [AllowAnonymous],
take nothing (no route values, query values or body), and answer 401 with an
empty body when the session cannot be renewed; BackendMapper reports each of
those it can see, and more than one marked action. Its success answer (a body
or none) is handed to the application (see THE ONE SET-UP CALL). Required
when anything of the backend needs sign-in.

    [HttpPost("refresh")]
    [AllowAnonymous]
    [WireSessionRefresh]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken token)


CORE API REFERENCE: THE STANDARD ERROR BODY
===========================================

CodeBrix.WebPlatform.Core.Problems

    public sealed class ApiProblem
    {
        public const string ContentType = "application/problem+json";
        public string Title  { get; set; }   // a sentence fit to show the user
        public int    Status { get; set; }   // the HTTP status code
        public string Code   { get; set; }   // stable, machine-readable
        public IDictionary<string, string[]> Errors { get; set; }
            // messages per camelCase field name; left out of the JSON when null
    }

    public static class ApiProblemCodes
    {
        ValidationFailed     = "validation_failed"        // 400
        MissingRequestHeader = "missing_request_header"   // 400
        InvalidCredentials   = "invalid_credentials"      // 401, with a body
        Forbidden            = "forbidden"                // 403: the caller is
                                                          // known but may not
                                                          // do this
        ForbiddenOrigin      = "forbidden_origin"         // 403: a hub request
                                                          // not from a page of
                                                          // this site
        NotFound             = "not_found"                // 404
        Conflict             = "conflict"                 // 409
        RateLimited          = "rate_limited"             // 429
        ServerError          = "server_error"             // 5xx
    }

An application adds codes of its own beside these. Serialize ApiProblem with
JsonSerializerOptions.Web (camelCase):

    {"title":"One or more fields are invalid.","status":400,
     "code":"validation_failed","errors":{"email":["Enter a valid email."]}}

The frontend's shared error type (ApiError, below) reads exactly this shape.
The Backend package builds the standard ones (ApiProblems, see BACKEND API
REFERENCE: THE ERROR BODIES).


CORE API REFERENCE: FRONTEND APPLICATIONS
=========================================

CodeBrix.WebPlatform.Core.Applications. The names and places of the frontend
applications (see SEVERAL FRONTEND APPLICATIONS), shared by the frontend
build and the backend. A build program normally names its applications with
FrontendApplicationOptions (FrontendBuilder) and a backend normally reads
them from the manifest (Backend); these types are what both pass around.

    public sealed class FrontendApplication : IEquatable<FrontendApplication>
    {
        public const string DefaultName = "app";       // the one with no name
        public const int MaximumNameLength = 40;
        public static FrontendApplication Default { get; }   // at the site root
        public static FrontendApplication Named(string name);
            // ArgumentException when the name breaks a rule (the message
            // says which)
        public static FrontendApplication FromName(string name);
            // null or empty: Default; otherwise Named(name)
        public static string DescribeNameProblem(string name);
            // what is wrong with a name, as a sentence; null when it is fine
        public string Name { get; }        // DefaultName for the default one
        public bool IsDefault { get; }
        public string BasePath { get; }    // first address segment: "" for
                                           // the default one, else the name
        public string Folder { get; }      // its folder below the web root:
                                           // "" (the web root), else the name
        public override string ToString(); // "sales", "app (site root)"
    }

    public sealed class FrontendApplicationSet
        : IReadOnlyList<FrontendApplication>
    {
        public FrontendApplicationSet(
            IEnumerable<FrontendApplication> applications);
            // ArgumentNullException; ArgumentException: none, a name twice,
            // more than one default application
        public static FrontendApplicationSet FromNames(
            IEnumerable<string> names);
            // null or empty entry = the default application
        public static FrontendApplicationSet SingleDefault { get; }
            // one application, the default one
        public FrontendApplication Default { get; }   // null when every
                                                      // application has a name
        public IReadOnlyList<FrontendApplication> Named { get; }
        public FrontendApplication Find(string name); // null/"" = the default
                                                      // one; null when absent
        public FrontendApplication this[int index] { get; }
        public int Count { get; }
    }

    public static class FrontendManifest
        // the names of the file the frontend build writes into the web root
        // for the backend: FileName = "codebrix-frontends.toml", and the names
        // used inside it. What it is for: BACKEND: HOW THE FRONTEND
        // APPLICATIONS ARE SERVED. Its layout is the platform's business:
        // never write or edit the file yourself.


CORE API REFERENCE: CRATE BUNDLES
=================================

CodeBrix.WebPlatform.Core.Crates. How a NuGet package brings Rust crates of
its own to a platform frontend (see CRATE BUNDLES for the whole mechanism).

    public sealed class CrateBundle
    {
        public CrateBundle(string name, string packageName,
            string packageVersion, Func<Stream> openArchive);
            // name: short, unique among the bundles of one build;
            // packageName / packageVersion: named in error messages;
            // openArchive: opens the zip of crates, called each time the
            // build needs it (the caller disposes the stream).
            // ArgumentException (a blank text), ArgumentNullException
        public string Name { get; }
        public string PackageName { get; }
        public string PackageVersion { get; }
        public string Origin { get; }      // "<package> <version>"
        public Stream OpenArchive();       // InvalidOperationException when
                                           // the function gave no stream
    }

    public static class CrateBundles
    {
        public static void Register(CrateBundle bundle);
            // the same bundle again (same name, package and version) does
            // nothing; the same NAME from another package or another version
            // of it throws InvalidOperationException
        public static IReadOnlyList<CrateBundle> Registered { get; }
            // a copy, in registration order
    }


DECLARING A REST ACTION
=======================

What the mapper needs from each action, and what it does with it.

  HTTP method and route
      [HttpGet("{id:guid}")] on the action, joined to the controller's
      [Route("api/clients")]. `[controller]` and `[action]` tokens are
      replaced. An action template that starts with "/" or "~/" stands alone.
      Route constraints (":guid") are dropped from the generated path.
      NOT SUPPORTED: catch-all parameters ({*rest}), optional route parameters
      ({id?}), default values ({page=1}), more than one method or route on an
      action, [HttpHead]/[HttpOptions].

  Parameters, by where they travel
      route   a parameter whose name matches a route token, or [FromRoute]
      body    [FromBody], or a class-type parameter without an attribute; at
              most one
      query   [FromQuery], or a single-value parameter without an attribute
      skipped CancellationToken, [FromServices], [FromKeyedServices]
      NOT SUPPORTED: [FromHeader], [FromForm], ref/out parameters, a
      collection in the query string.

      A query parameter is OPTIONAL when it is a nullable value type, has a
      default value, or is a reference type marked [WireOptional]. A class
      read with [FromQuery] is spread into one query value per settable
      single-value property.

  Who may call
      [Authorize] or [AllowAnonymous] on the action or the controller.
      [AllowAnonymous] anywhere wins. Needs-sign-in endpoints get the session
      refresh-and-retry treatment in the frontend (see THE SHARED RUST CRATE).

  The success answer - one of
      [ProducesResponseType(typeof(T), StatusCodes.Status200OK)]
      [ProducesResponseType<T>(StatusCodes.Status201Created)]
      [ProducesResponseType(StatusCodes.Status204NoContent)]   // no body
      a return type of ActionResult<T>, T, Task<T> or ValueTask<T>
      (an action that returns IActionResult / Task<IActionResult> tells the
      mapper nothing by itself and needs the attribute).
      An action has ONE success shape: two different success bodies, or one
      success with a body and another without, is a problem.

  Error answers
      Need no declaration: they are ApiProblem by platform rule. If you do
      declare one ([ProducesResponseType(typeof(ApiProblem), 400)],
      [ProducesResponseType(401)]) the body type must be ApiProblem or absent.

Example:

    [ApiController]
    [Authorize]
    [Route("api/clients")]
    public sealed class ClientsController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<Client>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] bool? includeArchived,
            CancellationToken cancellationToken) { ... }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(Guid id,
            [FromBody] ClientRequest request, CancellationToken cancellationToken) { ... }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(Guid id,
            CancellationToken cancellationToken) { ... }
    }

Only the controllers in the backend assembly itself are mapped. Contract
classes may live in any assembly the backend references.


DECLARING A HUB
===============

    [WireRequired]
    public interface IClockClient            // what the hub SENDS
    {
        Task Tick(ClockTick tick);
    }

    [WireHub("/api/hubs/clock")]
    [AllowAnonymous]                         // or [Authorize]
    [WireRequired]
    public sealed class ClockHub : Hub<IClockClient>
    {
        public ClockTick Now() => ...;                       // answers once
        public Task<Estimate> Estimate(decimal hours, decimal rate) => ...;
        public IAsyncEnumerable<Progress> Generate(DateOnly from, DateOnly to,
            CancellationToken cancellationToken) => ...;     // a stream
    }

  * The hub derives from Hub<TClient>; TClient is an interface. Each of its
    methods is one message; it returns Task. A message that expects an answer
    from the frontend (Task<T>) is not supported.
  * Every public method declared on the hub class is callable by the frontend,
    except overrides (OnConnectedAsync, ...) and [WireIgnore] methods. A
    method that returns IAsyncEnumerable<T> or ChannelReader<T> is a stream.
    A CancellationToken parameter is skipped. A stream FROM the frontend TO
    the hub is not supported. SignalR does not allow overloads.
  * Method and message names are sent as they are spelled in .NET ("Tick",
    "JoinAsync").
  * [Authorize] / [AllowAnonymous] on the hub CLASS only; on a hub method
    they are a problem.
  * A hub with nothing to send still needs a (possibly empty) client
    interface.

IMPORTANT for a hub that needs sign-in: a browser sends a cookie only to the
paths the cookie is scoped to. If the session cookie is scoped to /api, serve
the hubs under /api too (for example /api/hubs/clock). And because a browser
does not apply the same-origin policy to WebSockets, the backend checks the
Origin header of every hub request (public hubs too): the Backend package does
that for every address below /api/hubs (PlatformBackendPaths.Hubs), so map
the hubs there.


BACKEND API REFERENCE: THE TWO CALLS
====================================

CodeBrix.WebPlatform.Backend. One services call and one pipeline call give a
backend everything THE PLATFORM RULES ask of its answers, and serve its
frontend applications:

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddControllers();
    builder.Services.AddSignalR();
    builder.Services.AddAuthentication(...);      // the application's own
    builder.Services.AddAuthorization();
    builder.Services.AddPlatformBackend(options =>
    {
        options.ContentSecurityPolicy =
            "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; "
            + "style-src 'self'; img-src 'self' data:; connect-src 'self'; "
            + "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    });
    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();             // BEFORE: whatever must see every answer
    }

    app.UsePlatformBackend();      // the platform's part of the pipeline
    app.UseRouting();              // AFTER, in this order: the application's
    app.UseRateLimiter();          //   own routing, rate limiter,
    app.UseAuthentication();       //   authentication, authorization,
    app.UseAuthorization();        //   controllers and hubs
    app.MapControllers();
    app.MapHub<ClockHub>(WireHubAttribute.GetPath(typeof(ClockHub)));
    await app.RunAsync();

THE ORDER IN THE PIPELINE. UseHsts (and anything else that must be on every
answer, the platform's own refusals and static files included) goes BEFORE
UsePlatformBackend. Everything else of the application goes AFTER it,
starting with its own app.UseRouting(). Call UseRouting yourself: without it
WebApplication puts routing at the very start of the pipeline, and then a
controller wins over a web-root file at the same address. HSTS, rate
limiting, authentication, authorization, CORS, the controllers and the hubs'
mapping stay the application's.

    public static IServiceCollection AddPlatformBackend(
        this IServiceCollection services,
        Action<PlatformBackendOptions> configure = null);
        // registers the options and MVC's settings (the model-binding answer;
        // no MVC problem details for bodiless 4xx answers). The order of this
        // call and AddControllers() does not matter. May be called more than
        // once; the settings add up. ArgumentNullException (services).

    public static WebApplication UsePlatformBackend(this WebApplication app);
        // adds the platform's part of the pipeline and serves the frontend
        // applications the manifest in the web root names - read ONCE, here
    public static WebApplication UsePlatformBackend(this WebApplication app,
        FrontendApplicationSet applications);
        // the same, but serves the applications given instead of reading the
        // manifest (tests, or a developer who prefers to say it in code);
        // each is served from its folder in the web root

    public const string PlatformBackendApplicationExtensions.LogCategory;
        // "CodeBrix.WebPlatform.Backend": the start-up log lines
    public static class PlatformBackendPaths
    {
        public const string Api  = "/api";        // every API address
        public const string Hubs = "/api/hubs";   // every hub's address
    }

UsePlatformBackend adds, in this order and each only when its option is on:
the compression of the backend's answers (CompressResponses; off by
default), first, so that it covers the platform's own error bodies too; the
security headers; the standard body for bodiless 4xx answers below /api;
the X-Requested-With check; the hub Origin check; the web root's static files
(the manifest and the .br / .gz copies never by name; a file's copy in its
place when ServePrecompressedFiles is on); then it maps the frontend
applications' fallbacks (which serve an index.html's copy the same way) and
the unknown-API answer. Those two are endpoints of the lowest priority, so a
controller or a hub always wins over them.

It THROWS at start-up (the message says what to do):
    InvalidOperationException   AddPlatformBackend was not called; the
                                X-Requested-With check is on and no marker is
                                declared (or the assembly cannot be told or
                                loaded); RequestMarker is not a valid marker;
                                ContentSecurityPolicy is not one line; a set
                                of applications was given while
                                ServeFrontendApplications is off;
                                CompressResponses is not a ResponseCompression
                                value
    FrontendManifestException   the web root holds a manifest that cannot be
                                used
    ArgumentNullException       app (or applications) is null


BACKEND API REFERENCE: THE OPTIONS
==================================

    public sealed class PlatformBackendOptions   // every switch: true, except
    {                                            // CompressResponses (None)
        bool ServeFrontendApplications;     // the web root's static files
                                            // (.wasm as application/wasm) and
                                            // the applications' fallbacks
        bool ServePrecompressedFiles;       // a file's .br / .gz copy (written
                                            // by the frontend build) to a
                                            // browser that accepts it; see
                                            // COMPRESSION
        ResponseCompression CompressResponses;
                                            // DEFAULT None. FileAnswers: the
                                            // answers of [WireFile] actions,
                                            // nothing else; FileAnswersAndJson:
                                            // JSON answers too; read
                                            // COMPRESSION (the risk) first
        bool AnswerUnknownApiRoutes;        // 404 not_found, standard body,
                                            // for every /api address nothing
                                            // else answers, every HTTP method
        bool AddProblemBodiesToStatusCodes; // the standard body for every 4xx
                                            // below /api the framework wrote
                                            // without one (401 stays EMPTY;
                                            // 5xx and answers outside /api are
                                            // left alone)
        bool AnswerModelBindingFailures;    // MVC's invalid-model-state answer:
                                            // 400 validation_failed, one
                                            // message per camelCase field
        bool RequireRequestMarker;          // X-Requested-With on every POST,
                                            // PUT, PATCH and DELETE below /api,
                                            // exactly once and equal to the
                                            // marker; else 400
                                            // missing_request_header
        bool RequireHubOrigin;              // Origin must be this site's
                                            // scheme and host on every request
                                            // below /api/hubs; else 403
                                            // forbidden_origin
        bool SendSecurityHeaders;           // on every answer:
                                            // X-Content-Type-Options: nosniff,
                                            // X-Frame-Options: DENY,
                                            // Referrer-Policy: no-referrer, and
                                            // the policy below
        string ContentSecurityPolicy;       // sent exactly as given, on one
                                            // line. NO platform default: null
                                            // or blank = the header is not sent
        string RequestMarker;               // normally null (see below)
        Assembly BackendAssembly;           // normally null (see below)
    }

THE REQUEST MARKER the check demands is, in this order: RequestMarker; the
[assembly: WireRequestMarker] of BackendAssembly; the one of the
application's own assembly (the host's application name - the backend's
assembly in a normal run and under WebApplicationFactory<Program>). Declare
it once on the backend, [assembly: WireRequestMarker("MyApp")]: the
generated frontend code sends that value. A backend where nothing needs
sign-in still declares one, or sets RequireRequestMarker = false.

THE CONTENT-SECURITY-POLICY a platform site needs is the one in the example
above: 'wasm-unsafe-eval' is what lets the browser compile WebAssembly, and
nothing else of a platform frontend needs more. It is the same with code
splitting (the pieces are WebAssembly from the same site). The platform sends
the policy you give, and none when you give none.

A setting that comes from the application's own configuration goes in with
the options pattern, after AddPlatformBackend:

    builder.Services.AddOptions<PlatformBackendOptions>()
        .Configure<MySettings>((options, settings) =>
            options.ContentSecurityPolicy = settings.ContentSecurityPolicy);


BACKEND: HOW THE FRONTEND APPLICATIONS ARE SERVED
=================================================

THE MANIFEST. When the frontend build fills the web root it writes, beside
the applications, a small file named codebrix-frontends.toml
(FrontendManifest.FileName): it tells the backend which frontend
applications were built and where they are. The backend reads it ONCE, in
UsePlatformBackend, and serves what it names; the backend's code names no
application anywhere. The file is BUILD OUTPUT: it is rewritten on every
build into a web root, never edited, never committed, and never served.
Restart the backend after building the frontend into its web root.

NO MANIFEST (no frontend built into the web root yet, or an empty web root):
the API works as usual; the web root's files are still served and "/"
answers <web root>/index.html if there is one; nothing else falls back. A
start-up log line (LogCategory) says what is served; a warning names an
application whose index.html is missing (its addresses then answer 404).

For GET and HEAD the backend serves:

    the web root's files           as files (.wasm as application/wasm)
    the default application        "/" and every address that is not a file,
                                   not below a named application and not
                                   below /api -> <web root>/index.html
    a named application X          /X, /X/ and every address below it that
                                   is not a file -> <web root>/X/index.html
    no default application         "/" -> <web root>/index.html when the
                                   developer put one there, else 404; any
                                   address outside every application -> 404
    below /api                     NEVER an index.html: an address nothing
                                   answers is 404 not_found with the standard
                                   body, for every method
    the manifest                   never, under any spelling (404)
    a .br / .gz copy by its name   never (404, as for an unknown file): a
      (x.wasm.br beside x.wasm)      copy is served only in its file's place
                                   (see COMPRESSION). A .br / .gz file with
                                   no file of the shorter name beside it is
                                   an ordinary file.
    a file-like address that       404. An address whose last segment has a
      does not exist                 dot is a file (/assets/x.js), as in
                                   ASP.NET Core's MapFallbackToFile.
    POST/PUT/DELETE to a frontend  405 with an empty body (a fallback answers
      address                        GET and HEAD only)

The fallbacks allow anonymous access, so a fallback authorization policy of
the application never locks a frontend out.

    public static class FrontendManifestReader   // ...Backend.Applications
    {
        public static FrontendApplicationSet Read(string webRootDirectory);
            // null when there is no manifest (or no web root)
        public static FrontendApplicationSet Parse(string text,
            string sourceName = "codebrix-frontends.toml");
        public static string GetPath(string webRootDirectory);
    }
    public sealed class FrontendManifestException : Exception
    {
        public string FilePath { get; }
        public string Problem { get; }     // what is wrong, alone
    }

The reader is strict: anything the frontend build of the same release would
not have written is refused with a FrontendManifestException whose message
names the file and says to build the frontend again. The application does
not normally call the reader; UsePlatformBackend does.


COMPRESSION: THE BUILT FILES AND THE BACKEND'S ANSWERS
======================================================

THE RULE. The built frontend files are the same for every visitor and hold
no secrets, so the platform compresses them - ONCE, when the frontend is
built - and the backend serves the compressed copies: ON by default.
Anything the backend GENERATES may hold a user's data, so compressing it as
it is sent is the developer's decision: OFF by default (read THE RISK
below before switching it on).

THE THREE OPTIONS

    options.CompressOutput = FrontendCompression.BrotliAndGzip;
        // the build program's PlatformFrontendOptions (the default)
    options.ServePrecompressedFiles = true;
        // AddPlatformBackend's PlatformBackendOptions (the default)
    options.CompressResponses = ResponseCompression.FileAnswers;
        // AddPlatformBackend's PlatformBackendOptions (default None)

    public enum FrontendCompression     // CodeBrix.WebPlatform.FrontendBuilder
    { None, Brotli, Gzip, BrotliAndGzip }
    public enum ResponseCompression     // CodeBrix.WebPlatform.Backend
    { None, FileAnswers, FileAnswersAndJson }

WHAT THE BUILD DOES TO THE WEB ROOT (CompressOutput)
  - BuildAsync with a web root, after it has filled it, writes beside every
    file of the built applications (dx's files, in each application's folder
    of the web root) whose extension is one of
        .wasm .js .mjs .css .html .htm .json .map .webmanifest .svg .txt
        .xml .csv .md .toml .yaml .yml .wgsl .glsl .gltf .obj .ttf .otf
    a <name>.br copy (Brotli at its strongest: quality 11, window 24) and/or
    a <name>.gz copy (gzip at its smallest size), each ONLY when it is
    smaller than the file. Every other file - PNG, JPEG, GIF, WebP, AVIF,
    ICO, WOFF, WOFF2, audio, video, ZIP, .gz, .br and the like, which are
    compressed already - gets no copy.
  - The copies are deterministic: the same file gives the same bytes, on
    every machine.
  - Copies never outlive their files: the build replaces the applications'
    folders on every build, and in each folder that holds an application's
    files a .br / .gz whose file no longer exists is removed. A .br / .gz
    that dx itself wrote beside a file (Dioxus.toml's pre_compress) is
    replaced by the platform's copy, or removed when the option does not ask
    for that copy; None leaves dx's output exactly as dx wrote it.
  - THE DEVELOPER'S OWN FILES ARE NEVER GIVEN COPIES. When every application
    has a name, the web root's other files (a page of your own at "/",
    images beside it) are left exactly as they are; so is a file the build
    program writes into the web root AFTER BuildAsync (the notices, for
    example). A <name>.br / <name>.gz you put beside such a file yourself is
    served in the same way as the platform's.
  - NOTHING for ServeAsync (dx serve answers the browser itself), and
    nothing without a web root.
  - THE TIME. Brotli at its strongest takes seconds on a large .wasm (the
    files are compressed in parallel); result.Compression.Elapsed says how
    long it took, and the build writes one line per application to Output.
    CompressOutput = None skips it, for example for quick local builds.
  - The copies are BUILD OUTPUT, like everything else the build writes into
    the web root: never edited, never committed. The manifest
    (codebrix-frontends.toml) does not change: the backend finds the copies
    on disk.

    public sealed class FrontendCompressionResult   // result.Compression
    {
        FrontendCompression Compression;         // what was asked for
        IReadOnlyList<CompressedFile> Files;     // every compressible file
                                                 // of the application, by
                                                 // path; empty with None
        int BrotliFileCount; int GzipFileCount;  // copies written
        long OriginalBytes;                      // the files themselves
        long BrotliBytes;                        // what a browser taking
                                                 // Brotli downloads for them
        long GzipBytes;                          // ... taking gzip, not Brotli
        int RemovedCopyCount;                    // .br / .gz files removed
        TimeSpan Elapsed;                        // time spent compressing
    }
    public sealed class CompressedFile
    {
        string Path;                             // in the application's
                                                 // folder, "/" separated
        long OriginalBytes;
        long? BrotliBytes; long? GzipBytes;      // null: no such copy
        string ToString();                       // "wasm/x.wasm 1000 bytes,
                                                 //  br 300, gz 400"
    }

HOW THE BACKEND SERVES THE COPIES (ServePrecompressedFiles, default true)
  - A GET or HEAD of a web-root file that has a copy - and an application's
    index.html reached through its fallback (/sales/orders) - is answered
    with the copy when the request's Accept-Encoding prefers it: the .br
    copy, else the .gz copy, with Content-Encoding, the FILE's own
    Content-Type, the copy's Content-Length, and the copy's own
    Last-Modified and ETag (If-None-Match / If-Modified-Since answer 304,
    If-Match / If-Unmodified-Since 412).
  - Accept-Encoding is read with its q-values: the heaviest coding the
    request accepts wins, Brotli before gzip at equal weight; "identity"
    (the file itself) wins only when it is listed (or "*" is) with a
    greater weight; "*" counts for a coding that is not listed; q=0 refuses
    a coding; x-gzip is gzip. A request without Accept-Encoding, or one
    that accepts neither coding, gets the file itself.
  - A request with a Range header gets the file itself, with ranges, as
    before: ranges are not offered on a copy.
  - Every answer for a file that has a copy carries Vary: Accept-Encoding,
    so a cache keeps the two apart.
  - A copy is never served under its own name: /x.wasm.br answers what any
    unknown file answers (404), whatever ServePrecompressedFiles says. A
    .br / .gz file with no file of the shorter name beside it is an
    ordinary file of the web root.
  - Off: every file is served as it is (the copies stay hidden). With no
    copies in the web root the option changes nothing.
  - THE FRONTEND NEEDS NOTHING. The browser decompresses: the frontend's
    fetches - the generated REST client, <name>_bytes (send_bytes), the
    hubs' negotiate call - go through the browser's fetch (reqwest in the
    browser IS fetch) and receive the original bytes.

COMPRESSING THE BACKEND'S ANSWERS (CompressResponses, default None)
  - FileAnswers compresses the answers of [WireFile] actions, and nothing else.
    The matched endpoint decides, per request (the action's [WireFile] is in
    its metadata), not the address and not the Content-Type. An action
    WITHOUT [WireFile] is never compressed under FileAnswers, whatever it
    answers: text it writes itself (text/plain, text/html, text/csv), a
    File(...) or PhysicalFile(...) result, JSON. To have a download
    compressed, mark its action [WireFile] (which also gives the frontend
    its <name>_url and <name>_bytes functions), or let a proxy compress it.
    Inside a [WireFile] answer, the types that are compressed already are
    skipped; every other type is compressed: application/octet-stream,
    text/* (not text/event-stream), XML and every +xml type (SVG too),
    JavaScript, application/wasm, YAML, TOML, JSON - and the action's error
    answers (its problem bodies) as well.
  - FileAnswersAndJson: the [WireFile] answers as above, and the JSON
    answers of EVERY action too - application/json, text/json, NDJSON and
    every +json type, application/problem+json (the platform's error bodies,
    those of the X-Requested-With and hub Origin checks included). Other
    answers of actions without [WireFile] are still not compressed.
  - NEVER compressed this way: types that are compressed already (images,
    archives, PDF, fonts, audio, video - every type not listed above), not
    even in a [WireFile] answer; the
    web root's files (compressed when the frontend is built, served as they
    are); every address below /api/hubs and every hub endpoint (negotiate,
    long polling, WebSockets); text/event-stream; an answer that already
    has a Content-Encoding or a Content-Range (a range answer).
  - Brotli when the request prefers it, otherwise gzip (the same reading of
    Accept-Encoding as above), at the FASTEST setting; over HTTPS as well;
    with Vary: Accept-Encoding and without Content-Length (the body is sent
    in chunks). A flush passes through, so an answer that flushes as it
    goes still reaches the client as it is written. There is no option for
    the encoder or the level.
  - It is ASP.NET Core's response-compression middleware (from the shared
    framework), set up by UsePlatformBackend with these rules, first in the
    platform's part of the pipeline. Do NOT add AddResponseCompression /
    UseResponseCompression of your own as well.

THE RISK (read this before you switch CompressResponses on)
  Compressing, over HTTPS, an answer that holds a SECRET together with TEXT
  AN ATTACKER CONTROLS can leak the secret. An attacker who can make the
  victim's browser send requests (a page of his that the victim opens) and
  can see the sizes of the encrypted answers (on the same network) changes
  the text he controls - a search term, a name he typed into the
  application - and watches the answer shrink when his guess matches part
  of the secret, one character after another. That is the BREACH class of
  attack. It is why CompressResponses is OFF by default, and why JSON -
  where a user's data, tokens and echoed input usually sit side by side -
  is a separate, explicit choice. Switch it on only for answers that hold
  no secret, or no text anyone else can put into them (a public download;
  an export made only of the signed-in user's own data). Never let an
  answer that carries a token, a CSRF value or one user's data be
  compressed together with text from the request or from another user.
  The built frontend files carry no such risk: they are the same for
  everyone and are compressed once, at build time.

BEHIND A PROXY THAT COMPRESSES
  A site behind a reverse proxy or a CDN that compresses (nginx's gzip or
  brotli modules, a CDN's compression) turns the platform's compression off
  with the three options:
      options.CompressOutput = FrontendCompression.None;     // build program
      options.ServePrecompressedFiles = false;               // backend
      options.CompressResponses = ResponseCompression.None;  // backend
  (A proxy that passes Content-Encoding through unchanged can keep the
  copies instead.) The proxy's own compression of the backend's answers
  carries THE RISK above just the same.

WHAT YOU CANNOT DO
  - fetch a copy by its name (x.wasm.br is 404);
  - have ServeAsync (dx serve) compress anything;
  - have the build compress your own files in the web root;
  - choose the encoder or the level of the answers compressed as they are
    sent.


BACKEND API REFERENCE: THE ERROR BODIES
=======================================

CodeBrix.WebPlatform.Backend.Problems. The standard ApiProblem bodies, for
the backend's own answers too:

    ApiProblems.Validation(errors) / Validation(field, message)
                                                  400 validation_failed
    ApiProblems.MissingRequestHeader()            400 missing_request_header
    ApiProblems.Forbidden(title = "You are not allowed to do that.")
                                                  403 forbidden
    ApiProblems.ForbiddenOrigin()                 403 forbidden_origin
    ApiProblems.NotFound(title = null)            404 not_found
                                                  (DefaultNotFoundTitle when
                                                  no title is given)
    ApiProblems.Conflict(title, field, message) / Conflict(title, errors = null)
                                                  409 conflict
    ApiProblems.RateLimited()                     429 rate_limited
    ApiProblems.ForStatus(status)
        // the body the platform gives a bodiless 4xx: 404 and 405 not_found
        // ("There is no such API route.", with the real status), 403
        // forbidden, 409 conflict, 429 rate_limited, 415 and every other
        // validation_failed
    const string ValidationTitle, NoSuchApiRouteTitle, DefaultNotFoundTitle

    return new ApiProblemResult(problem);             // from a controller
                                                      // action: status =
                                                      // problem.Status
    await ApiProblemWriter.WriteAsync(httpContext, problem);
        // from middleware or an endpoint delegate; InvalidOperationException
        // when the answer has already started
    InvalidModelStateResponse.Create(actionContext)   // what AddPlatformBackend
        // installs in MVC; BodyField "body" names the request body as a whole

An application's own codes (invalid_credentials and the like) are a new
ApiProblem { Title, Status, Code } of its own, returned the same ways. A rate
limiter's rejection is written with ApiProblemWriter and RateLimited():

    options.OnRejected = (context, token) => new ValueTask(
        ApiProblemWriter.WriteAsync(context.HttpContext,
            ApiProblems.RateLimited()));


BACKENDMAPPER API REFERENCE: MAPPING
====================================

    public static class BackendMapper
    {
        public static BackendMap Map(BackendMapOptions options);
    }

    public sealed class BackendMapOptions
    {
        public string AssemblyPath { get; set; }
            // the compiled backend, e.g. bin/Release/net10.0/MyApp.Api.dll
        public IList<string> ReferencePaths { get; }
            // extra folders/files to resolve references from; normally empty
        public RustTypeMap Types { get; set; }   // defaults; see TYPE MAPPING
    }

How references are resolved, in this order: the folder of the backend
assembly; ReferencePaths; the ASP.NET Core and .NET runtime folders of the
runtime the mapper is running on. BUILD THE BACKEND FIRST and point
AssemblyPath at the build (or publish) output, where its other projects and
packages sit beside it.

Map throws:
    ArgumentNullException   options is null
    BackendMapException     always this for anything else

    public sealed class BackendMapException : Exception
    {
        public IReadOnlyList<MappingProblem> Problems { get; }
    }
    public sealed class MappingProblem
    {
        public string Location { get; }   // "ClientsController.Update",
                                          // "TimeEntry.Description",
                                          // "ClockHub.Tick(tick)"
        public string Message  { get; }   // what is missing and what to do
        public override string ToString();   // "Location: Message"
    }

Two kinds of failure:
  * The backend's contract is incomplete or uses something unsupported.
    Problems lists EVERY problem found in the whole backend, in one throw, so
    they can all be fixed in one pass. Message is the count plus one line per
    problem.
  * The assembly (or one it refers to) cannot be read. Problems is empty and
    Message says what could not be read.

The result:

    BackendMap
        string AssemblyName
        IReadOnlyList<RestController> Controllers   // ordered by name
        IReadOnlyList<HubMap>         Hubs          // ordered by name
        IReadOnlyList<WireStruct>     Structs       // contract classes
        IReadOnlyList<WireEnum>       Enums

    RestController   Name (without "Controller"), ClrName, Endpoints
    RestEndpoint     ActionName, HttpMethod ("GET"...), RouteTemplate
                     ("/api/clients/{id}"), Route (RouteSegment: Text,
                     IsParameter), Parameters (route values in path order,
                     then the body, then the query values), RequiresSignIn,
                     IsFile, SuccessStatus, Response (WireType; null when the
                     success answer has no body)
    EndpointParameter  Name, WireName (route token / query key; null for the
                     body), Source (ParameterSource.Route|Query|Body), Type
    HubMap           Name ("ClockHub"), ClrName, Path, RequiresSignIn,
                     ClientInterfaceClrName, Methods, Messages
    HubMethod        Name, Kind (HubMethodKind.Invoke|Stream), Arguments,
                     Result (the answer, or each streamed item; null for none)
    HubMessage       Name, Arguments
    HubArgument      Name, Type
    WireType         Kind (WireTypeKind.Scalar|List|Map|Struct|Enum), ClrName,
                     IsOptional, Rust (for a Scalar), Element (item of a List,
                     value of a Map), Struct, Enum
    WireStruct       Name (the generated name), ClrName, Fields
    WireField        ClrName, JsonName, Type
    WireEnum         Name, ClrName, AsText, Members (Name, Value)

A closed generic contract class gets a combined name: Page<Widget> becomes
PageOfWidget. Two different .NET types that would get the same generated name
are a problem.


BACKENDMAPPER API REFERENCE: TYPE MAPPING
=========================================

    public sealed class RustTypeMap
    {
        public RustTypeMap();                         // the defaults
        public RustTypeMap Map(string clrTypeName, RustType rustType);
        public RustTypeMap Map<T>(RustType rustType);
        public bool TryGet(string clrTypeName, out RustType rustType);
        public IReadOnlyCollection<string> ClrTypeNames { get; }
        public const string SharedDateImport = "codebrix_webplatform::dates::Date";
    }

    public sealed class RustType
    {
        public RustType(string name);
        public string Name   { get; }        // "f64", "String", "Date"
        public string Import { get; init; }  // use path, or null
        public string With   { get; init; }  // serde `with` module, or null
        public bool   IsCopy { get; init; }  // taken by value in functions
    }

THE DEFAULTS

    .NET                                  Rust
    string, Guid, DateTime,               String
      DateTimeOffset, TimeOnly, TimeSpan,
      Uri, char, byte[]
    bool                                  bool
    byte/sbyte/short/ushort/int/uint/     u8/i8/i16/u16/i32/u32/i64/u64
      long/ulong
    float, double                         f32, f64
    decimal                               f64   (SEE THE CAVEAT BELOW)
    DateOnly                              Date  (the shared crate's type)
    System.Text.Json.JsonElement          serde_json::Value
    T? (nullable value type)              Option<T>
    T[], List<T>, IEnumerable<T>,         Vec<T>
      IReadOnlyList<T>, HashSet<T>, ...
    Dictionary<string, T> and its         BTreeMap<String, T>
      interfaces
    an application class or struct        a generated struct
    an application enum                   a generated enum

Anything else (object, a type from System.* or Microsoft.* that is not in the
table, an interface or abstract class as a contract type, a dictionary with
non-string keys, a class that contains itself directly) is a PROBLEM, with the
place it was found. Fix it by mapping the type or by changing the contract.

THE decimal CAVEAT: a .NET decimal is written to JSON as a number, and f64
reads it without anything extra, but f64 is not exact. For money that must
stay exact, map decimal to a type of your own, either for the whole
application:

    var options = new BackendMapOptions { AssemblyPath = path };
    options.Types.Map<decimal>(new RustType("Money")
    {
        Import = "crate::money::Money",
    });

or for one value with [WireType] (above). The override type must implement
serde's Serialize and Deserialize (or name a With module), Clone, Debug and
PartialEq, and ToString if it is used in a route or query string.


BACKENDMAPPER API REFERENCE: WRITING THE RUST CODE
==================================================

    public static class RustClientGenerator
    {
        public const string GeneratedMarker =
            "// @generated by CodeBrix.WebPlatform.BackendMapper";
        public static IReadOnlyList<GeneratedFile> Generate(
            BackendMap map, RustClientOptions options = null);
        public static IReadOnlyList<string> WriteTo(
            string directory, IEnumerable<GeneratedFile> files);
    }

    public sealed class GeneratedFile { string RelativePath; string Content; }

    public sealed class RustClientOptions
    {
        public string SharedCratePath { get; set; }      // "codebrix_webplatform"
        public string SignalRModulePath { get; set; }    // see below
        public int MaxSeparateQueryArguments { get; set; }   // 2
        public RustTemplates Templates { get; set; }
    }

Generate returns, ordered by path:

    mod.rs      declares the others; carries #![allow(dead_code,
                unused_imports)] because an application rarely calls every
                endpoint
    models.rs   one struct per contract class, one enum per enum
    api.rs      one module per controller, one function per endpoint
                (left out when the backend has no endpoints)
    hubs.rs     one descriptor and one type per hub (left out when there are
                no hubs)

Every file starts with GeneratedMarker and ends with one newline; line endings
are \n.

WriteTo(directory, files) makes a folder hold exactly those files:
  * a file that already has the right content is NOT touched (so cargo does
    not rebuild for nothing);
  * a stale file that carries the generated marker is deleted;
  * a file WITHOUT the marker makes it throw InvalidOperationException and
    write nothing, so hand-written code is never destroyed. Give the generated
    code a folder of its own (for example the frontend's src/generated).
  Returns the relative paths written or deleted (empty when up to date).

The generated code is BUILD OUTPUT: never edit it, do not commit it, and
ignore its folder in git.

WHAT THE GENERATED CODE LOOKS LIKE

    // models.rs
    #[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
    #[serde(rename_all = "camelCase")]
    pub struct Client {
        pub id: String,
        pub name: String,
        #[serde(default)]
        pub notes: Option<String>,
        pub total_hours: f64,
    }

    // api.rs
    pub mod clients {
        /// `GET /api/clients` (needs sign-in).
        pub async fn list(include_archived: Option<bool>) -> ApiResult<Vec<Client>>
        /// `PUT /api/clients/{id}` (needs sign-in).
        pub async fn update(id: &str, request: &ClientRequest) -> ApiResult<Client>
        /// `DELETE /api/clients/{id}` (needs sign-in).
        pub async fn delete(id: &str) -> ApiResult<()>
        /// The address of `GET /api/clients/{id}/logo.png` (needs sign-in), ...
        pub fn logo_url(id: &str) -> String                      // [WireFile]
        /// `GET /api/clients/{id}/logo.png` (needs sign-in), the file read as bytes.
        pub async fn logo_bytes(id: &str) -> ApiResult<Vec<u8>>  // [WireFile]
    }

Naming and shape rules of the generated code:
  * module   = the controller's name without "Controller", in snake_case
  * function = the action's name in snake_case, without an "Async" suffix; a
               [WireFile] action gets two: "_url" appended (returns String,
               the address) and "_bytes" appended (an async fn returning
               ApiResult<Vec<u8>>, the file's bytes)
  * arguments: route values in path order, then the body (`&T`), then the
               query values. String-like values are taken as `&str`, numbers,
               flags, dates and enums by value, everything else by reference.
  * an endpoint with MORE query values than MaxSeparateQueryArguments takes
    them as one generated struct named <Action>Query in the controller's
    module, with a Default: `time_entries::list(&time_entries::ListQuery {
    page: Some(2), ..Default::default() })`
  * struct fields are snake_case; a Rust keyword becomes a raw identifier
    (`r#type`); `Default` is derived when every field has a natural default
  * a generated enum has `ALL`, `as_str()`, `value()`, `from_value()` and
    Display (the member's name), and is Copy with the first member as Default

    // hubs.rs
    pub static CLOCK_HUB: HubDescriptor = HubDescriptor {
        name: "ClockHub", path: "/api/hubs/clock", requires_sign_in: false };

    impl ClockHub {
        pub fn connect() -> Self            // a share of the hub's connection
        pub fn use_hub() -> Self            // the same, as a Dioxus hook
        pub fn connection(&self) -> &HubConnection
        pub fn state(&self) -> ConnectionState
        pub fn use_state(&self) -> ReadSignal<ConnectionState>
        // one per hub method:
        pub async fn now(&self) -> Result<ClockTick, HubError>
        pub fn generate(&self, from: Date, to: Date) -> HubStream<Progress>
        // two per message the hub sends:
        pub fn on_tick(&self, handler: impl Fn(ClockTick) + 'static) -> Subscription
        pub fn use_tick() -> ReadSignal<Option<ClockTick>>   // latest value
    }

  * a message with one argument delivers that argument; with several, a
    generated struct <Hub><Message>Args; with none, `()`.
  * WHEN to connect, WHICH handlers to register and WHAT the pages do with the
    data is the application's own code.

    // mod.rs - declares the other files, and what the BACKEND DECLARES about
    // itself, with the one set-up call made from it
    pub const REQUEST_MARKER: &str = "MyApp";   // [assembly: WireRequestMarker]
    pub const SESSION_REFRESH_PATH: &str = "/api/auth/refresh";
                                                // the [WireSessionRefresh] action
    pub fn configure_platform(
        on_session_refreshed: impl Fn(UserResponse) + 'static, // refresh answer
        on_session_ended: impl Fn() + 'static)
        // calls codebrix_webplatform::configure(PlatformConfig { .. }) with the
        // two values above and the two hooks (see THE ONE SET-UP CALL)

  * REQUEST_MARKER is written only when the backend declares one;
    SESSION_REFRESH_PATH and the two hook parameters only when it has a
    [WireSessionRefresh] action. A refresh action whose success answer has no
    body gives `on_session_refreshed: impl Fn() + 'static`. A backend where
    nothing needs sign-in gets `pub fn configure_platform()` (no hooks). So
    when the backend gains or loses sign-in, the compiler points at the one
    call that must change.

THE SIGNALR CLIENT THE HUB CODE CALLS is the shared crate's
`codebrix_webplatform::signalr` (see THE SHARED RUST CRATE), the default of
RustClientOptions.SignalRModulePath. Leave the option alone. hubs.rs imports
from that path the names `arg`, `to_arg`, `ConnectionState`, `HubConnection`,
`HubDescriptor`, `HubError`, `HubStream`, `Subscription`, and the functions
`connect`, `use_hub`, `use_connection_state` and `use_hub_message`; only an
application with a SignalR client of its own offering exactly those would
point the option elsewhere.

CHANGING WHAT IS WRITTEN: TEMPLATES

    public sealed class RustTemplates
    {
        public string Module { get; set; }   // mod.rs
        public string Models { get; set; }   // models.rs
        public string Api    { get; set; }   // api.rs
        public string Hubs   { get; set; }   // hubs.rs
        public static string BuiltIn(string name);   // "mod","models","api","hubs"
    }

Each property starts as the built-in template. The language is the one of
CodeBrix.Templating (`{{ value }}`, `{{ for x in list }} ... {{ end }}`,
`{{~ ~}}` to trim a statement's own line). Start a changed template from
RustTemplates.BuiltIn(name). `import` is a keyword of the template language;
do not use it as a variable name.

What a template sees (every Rust name, type and expression is already worked
out; names are snake_case):

    marker, assembly_name, shared_crate, signalr_module, has_api, has_hubs
    has_request_marker, request_marker (text inside a Rust string literal),
    has_session_refresh, session_refresh_path, session_refresh_action,
    session_refresh_answer (Rust type, "" for none), setup_parameters,
    session_refreshed_hook, setup_uses_models, setup_imports[]
    model_imports, api_imports, hub_imports      lists of `use` paths
    structs[]      name, clr_name, derives, fields[] (name, type,
                   attributes[])
    enums[]        name, clr_name, as_text, count, members[] (name, wire_name,
                   value, is_first), distinct_members[]
    controllers[]  name, clr_name, module,
                   query_structs[] (name, doc, fields[] (name, type)),
                   functions[] (name, doc, parameters, is_file, has_response,
                   return_type, method, auth, query_statement, path, body,
                   and for a file endpoint bytes_name, bytes_doc,
                   bytes_return_type, bytes_path - the function that reads
                   the file's bytes; empty otherwise)
    hubs[]         name, clr_name, descriptor, path, requires_sign_in,
                   sign_in_doc,
                   methods[] (name, wire_name, is_stream, parameters,
                   arguments, result_type),
                   messages[] (name, wire_name, decoder, payload_type, decode,
                   has_args_struct, args_fields[] (name, type))

A template with a syntax error, or one that fails while applied (an unknown
variable is an error), makes Generate throw BackendMapException naming the
template.


FRONTENDBUILDER API REFERENCE: BUILDING A FRONTEND
==================================================

The build program describes the application's frontend once and calls three
steps. Everything is in CodeBrix.WebPlatform.FrontendBuilder.

ONE frontend application: set FrontendDirectory (and, to serve it below a
name, ApplicationName). SEVERAL: leave both unset and add one
FrontendApplicationOptions per application to Applications, first one first;
everything else in the options is shared by all of them.

    public sealed class PlatformFrontendOptions
    {
        public string FrontendDirectory { get; set; }     // the one
            // application's frontend crate folder (Cargo.toml, Dioxus.toml);
            // REQUIRED unless Applications has entries
        public string ApplicationName { get; set; }       // the one
            // application's name; null or empty (the default): no name, served
            // at the site root. See SEVERAL FRONTEND APPLICATIONS.
        public IList<FrontendApplicationOptions> Applications { get; }
            // several applications; empty by default. When it has entries,
            // FrontendDirectory and ApplicationName must be unset.
        public bool? WasmSplit { get; set; }              // bundle splitting
            // (see CODE SPLITTING); null or false: off. With Applications it
            // is the first application's setting when that one sets none.
        public string BackendProjectPath { get; set; }    // the backend's
            // .csproj; built in Release by MaterializeAsync
        public string BackendAssemblyPath { get; set; }   // the assembly to
            // read; default: what the Release build of the project produces.
            // Set only this (no project) to read an assembly already built.
        public RustTypeMap Types { get; set; }            // see TYPE MAPPING
        public RustClientOptions ClientOptions { get; set; }
        public CrateMode CrateMode { get; set; }          // where crates come
            // from; default PlatformCratesOnly (offline) - see THE
            // PLATFORM'S CRATES
        public string VendorDirectory { get; set; }       // where the
            // crates are written; default <frontend>/platform/vendor (the
            // FIRST application's: all applications share the one folder)
        public string DxPath { get; set; }                // see HOST TOOLS
        public string WasmBindgenPath { get; set; }
        public string DxHomeDirectory { get; set; }       // dx's DX_HOME;
            // default <frontend>/target/dx-home of each application; when
            // set and there are several applications, <it>/<name> each
        public bool GenerateMissingStylesheet { get; set; }  // default true:
            // MaterializeAsync writes a missing Tailwind stylesheet (STEP 2)
        public string TailwindInputFile { get; set; }     // default
            // <frontend>/tailwind.css (relative: from the frontend folder)
        public string TailwindOutputFile { get; set; }    // default
            // <frontend>/assets/tailwind.css (where dx writes it)
        public bool? Shrink { get; set; }                 // see SHRINKING
        public FrontendCompression CompressOutput { get; set; }
            // default BrotliAndGzip: .br and .gz copies beside the built
            // files in the web root (BuildAsync only); see COMPRESSION
        public IList<string> ServeFeatures { get; }       // cargo features
            // for ServeAsync, e.g. the one that enables dioxus/devtools
        public TextWriter Output { get; set; }            // progress lines;
            // null = none
        // the paths below are the ONE application's (FrontendDirectory);
        // null when only Applications is used
        public string GeneratedClientDirectory { get; }
            // <frontend>/src/generated
        public string SharedCrateDirectory { get; }
            // <frontend>/platform/codebrix-webplatform
        public string CargoConfigurationFile { get; }
            // <frontend>/.cargo/config.toml
        public string ResolvedTailwindInputFile { get; }  // full paths of the
        public string ResolvedTailwindOutputFile { get; } //   two stylesheets
        public string ResolvedVendorDirectory { get; }
            // VendorDirectory, or <frontend>/platform/vendor (of the first
            // application in Applications when that list is used)
    }

    public sealed class FrontendApplicationOptions   // ...Applications
    {
        public string Name { get; set; }                  // null or empty: the
            // default application (at the site root); at most one
        public string FrontendDirectory { get; set; }     // REQUIRED: its own
            // crate folder; no two applications share one
        public bool? WasmSplit { get; set; }              // null: the first
            // application's setting; the first's null: the options' WasmSplit,
            // else off
        public string TailwindInputFile { get; set; }     // as in the options,
        public string TailwindOutputFile { get; set; }    //   for this one
        public IList<string> ServeFeatures { get; }       // empty: the
            // options' ServeFeatures
    }

    var options = new PlatformFrontendOptions
    {
        BackendProjectPath = "backend/MyApp.Api/MyApp.Api.csproj",
    };
    options.Applications.Add(new FrontendApplicationOptions
        { FrontendDirectory = "frontends/home" });                 // site root
    options.Applications.Add(new FrontendApplicationOptions
        { Name = "sales", FrontendDirectory = "frontends/sales",
          WasmSplit = true });                                     // /sales
    options.Applications.Add(new FrontendApplicationOptions
        { Name = "admin", FrontendDirectory = "frontends/admin",
          WasmSplit = false });                                    // /admin

    public sealed class PlatformFrontend
    {
        public PlatformFrontend(PlatformFrontendOptions options);
            // ArgumentNullException; ArgumentException (the message says
            // which): no frontend folder named; FrontendDirectory or
            // ApplicationName set together with Applications; a name that
            // breaks a rule or is used twice; more than one application
            // without a name; two applications with the same folder
        public PlatformFrontendOptions Options { get; }
        public FrontendApplicationSet Applications { get; }   // in the order
                                                              // of the options
        public string FrontendDirectory { get; }  // the FIRST application's,
        public string DxHomeDirectory { get; }    //   full paths
        public string GetFrontendDirectory(string applicationName);
            // null or empty: the one without a name; FrontendBuildException
            // when there is no application of that name

        public Task<PrerequisiteReport> CheckPrerequisitesAsync(
            CancellationToken cancellationToken = default);
        public Task<MaterializeResult> MaterializeAsync(
            CancellationToken cancellationToken = default);
        public Task<FrontendBuildResult> BuildAsync(
            FrontendConfiguration configuration = FrontendConfiguration.Release,
            string webRootDirectory = null,
            CancellationToken cancellationToken = default);
        public Task<int> ServeAsync(
            CancellationToken cancellationToken = default);
        public Task<int> ServeAsync(string applicationName,
            CancellationToken cancellationToken = default);

        public HostTools FindHostTools();
        public string GetOutputDirectory(FrontendConfiguration configuration);
            // the first application's:
            // <frontend>/target/dx/<package name>/<release|debug>/web/public
        public string GetOutputDirectory(string applicationName,
            FrontendConfiguration configuration);
        public static IReadOnlyList<string> GetDxBuildArguments(
            FrontendConfiguration configuration);
            // Release: build --platform web --release --debug-symbols false
            // Debug:   build --platform web
        public static IReadOnlyList<string> GetDxBuildArguments(
            FrontendConfiguration configuration, string applicationName,
            bool wasmSplit);
            // the same, then --base-path <name> for a named application
            // (null, "" or "app": none), then --wasm-split when splitting;
            // ArgumentException for a name that breaks a rule
    }

    public enum FrontendConfiguration { Release, Debug }

    public static class FrontendRequirements
    {
        public const string MinimumRustVersion;     // the oldest Rust accepted
        public const string WebAssemblyTarget;      // "wasm32-unknown-unknown"
        public static string ParseTailwindVersion(string helpOutput);
    }

Every failure of a step is a FrontendBuildException whose Message says what
failed and what to do (with the end of a program's output where that helps).
A contract problem in the backend keeps BackendMapper's BackendMapException
(with every problem) as the InnerException.

STEP 1 - CheckPrerequisitesAsync
    Probes the machine and NEVER throws for something missing (BuildAsync and
    ServeAsync run it too, and DO refuse - see STEP 3). The report has
    one PrerequisiteItem per PrerequisiteKind, in this order:
        Rust       rustc at MinimumRustVersion or later with the WebAssembly
                   target (detected with CodeBrix.RustTools; a missing piece
                   comes with the commands that install it). No C compiler.
        Tailwind   the packaged program (PackagedTailwind, see HOST TOOLS) is
                   beside the build program and reports its version for
                   `--help`; Missing names the package to add. A Tailwind on
                   PATH is never looked at.
        WasmOpt    where wasm-opt comes from (WasmOptSource): Package (the
                   CodeBrix.WebPlatform.WasmShrink package - normal), Installed
                   (the package has no binary for this machine yet; one on
                   PATH is used - a Note), Missing.
        HostTools  dx and wasm-bindgen (see HOST TOOLS), or which package to
                   add.
        Libgit2    the libgit2 library dx needs to start (see DX NEEDS
                   libgit2): it is prepared and dx is started once with it
                   (`dx --version`, reported as DxVersion). Missing when the
                   package's file is not beside the build program, when dx and
                   the library disagree on the library version (both are
                   named), or when dx does not start with it; a Note when dx
                   was not found.

    public sealed class PrerequisiteReport
    {
        IReadOnlyList<PrerequisiteItem> Items;   bool IsReady (nothing Missing);
        IReadOnlyList<string> Problems (messages of the Missing items);
        string RustVersion, TailwindVersion (null when not found);
        PackagedTailwind Tailwind (null when not found or not started);
        WasmOptSource WasmOpt; HostTools HostTools (null when not found);
        string DxVersion;   // dx --version, started with libgit2; null when
                            // dx was not found or did not start
        PrerequisiteItem this[PrerequisiteKind kind];
    }
    public sealed class PrerequisiteItem
    {
        PrerequisiteKind Kind; PrerequisiteStatus Status (Ok, Note, Missing);
        string Message;   // fit to show; for Missing it says how to fix it
        string ToString();   // "MISSING  The packaged Tailwind program ..."
    }

STEP 2 - MaterializeAsync
    Writes everything the frontend applications need and do not write
    themselves. ONCE for the whole platform application:
      1. builds BackendProjectPath with `dotnet build -c Release` (when set);
      2. maps the backend assembly with BackendMapper (Types);
      3. checks the registered crate bundles against the platform's crates
         (see CRATE BUNDLES); CrateMode.PlatformCratesOnly (the default):
         writes the platform's crates and the bundles' into
         ResolvedVendorDirectory, ONE folder that every application uses -
         unpacked only when an archive changed (a stamp file in the folder
         records the platform's archive and every bundle it holds).
         CrateMode.ApplicationAddsCrates: removes that folder if this library
         wrote it;
    and FOR EACH frontend application, in its own folder:
      4. writes the generated client code into <frontend>/src/generated
         (RustClientGenerator.WriteTo, with ClientOptions) - the same code
         for every application;
      5. writes the shared Rust crate into
         <frontend>/platform/codebrix-webplatform (SharedRustCrate.ExtractTo);
      6. writes the cargo configuration, <frontend>/.cargo/config.toml, for
         the crate mode (pointing at the one vendor folder);
      7. THE FRONTEND'S Cargo.lock (see THE FRONTEND'S Cargo.lock below):
         when the frontend has NO Cargo.lock yet, writes the platform's as its
         starting point (the platform's versions), with the platform's MARKER
         LINE as its third line. Commit the file. In
         CrateMode.PlatformCratesOnly it then compares the lock file with the
         vendor folder: an entry that names a version of a crate the folder
         holds ONLY in other versions (a platform crate or a bundle's crate
         that moved with a package upgrade) is STALE. WITH the marker line,
         exactly those entries are brought in step by running
         `cargo update --offline -p <crate> ...` in the frontend folder (every
         other entry stays as it is; the result's LockFileRepairs says what
         changed). WITHOUT it, the file is never touched: the result's
         LockFileWarning (also written to Output) names the stale entries and
         the exact command to run. A lock file with nothing stale is never
         touched, marker or not;
      8. THE TAILWIND STYLESHEET: when the frontend uses Tailwind (its input
         stylesheet, ResolvedTailwindInputFile, exists) and the generated one
         (ResolvedTailwindOutputFile, the file the application's
         asset!("/assets/tailwind.css") names and dx normally writes during a
         build) is MISSING, it starts the packaged Tailwind program once, by
         its full path - `tailwindcss -i <input> -o <output>` in the frontend
         folder. An existing stylesheet is never touched (dx rewrites it on
         every build). When the packaged program is not beside the build
         program, or fails, Materialize does NOT fail - the
         result says so (Stylesheet, StylesheetMessage), and the message is
         also written to Output. Switch the step off with
         GenerateMissingStylesheet = false.
    Only changed files are rewritten, so cargo does not rebuild for nothing.
    AFTERWARDS PLAIN `cargo check`, `cargo test` AND `cargo build` WORK in
    every frontend folder - offline, in the default crate mode - and so does an
    editor's Rust support, on a fresh clone too: clone, Materialize, cargo
    check. (Unless the result reports StylesheetMissing: then they fail on
    the asset!() until a build has run.)

    public sealed class MaterializeResult    // the FIRST application's
    {
        string ApplicationName;                  // "app" for the default one
        IReadOnlyList<MaterializeResult> Applications;
                                                 // one per application, in
                                                 // order; the first is this
                                                 // object
        BackendMap Map;                          // what the mapper read
        IReadOnlyList<string> GeneratedFiles;    // written or removed
        IReadOnlyList<string> SharedCrateFiles;  // written or removed
        bool VendorChanged;                      // crates written / removed
        bool CargoConfigurationWritten;
        bool LockFileWritten;                    // a starting Cargo.lock
        bool LockFileMarked;                     // Cargo.lock carries the
                                                 // marker line
        IReadOnlyList<LockFileRepair> LockFileRepairs;
                                                 // entries brought in step
                                                 // (marked file only)
        IReadOnlyList<string> StaleLockFileEntries;
                                                 // "name version" of an
                                                 // UNMARKED file, not changed
        string LockFileUpdateCommand;            // the command for those, run
                                                 // in the frontend folder;
                                                 // null when nothing is stale
        string LockFileWarning;                  // a message fit to show with
                                                 // that command; empty if none
        StylesheetOutcome Stylesheet;            // see below
        string StylesheetMessage;                // a sentence fit to show
        bool StylesheetMissing;                  // missing and not generated
        bool WasUpToDate;                        // nothing written for this
                                                 // application (a lock-file
                                                 // repair counts as written)
        bool AllWereUpToDate;                    // nothing written for any
        IReadOnlyList<CrateBundleOutcome> CrateBundles;
                                                 // per registered bundle, in
                                                 // registration order (the
                                                 // same for every application)
    }

    public sealed class CrateBundleOutcome   // ...FrontendBuilder.Crates
    {
        string Name;                             // the bundle's
        string Origin;                           // "<package> <version>"
        IReadOnlyList<string> AddedCrates;       // "name version", only it
        IReadOnlyList<string> SharedCrates;      // "name version", already
                                                 // there, identical (one
                                                 // copy is kept)
    }

    public sealed class LockFileRepair       // ...FrontendBuilder.Crates
    {
        string Crate;                            // the crate's name
        string OldVersion;                       // the version that was locked
        string NewVersion;                       // the version locked now; null
                                                 // when cargo dropped the crate
        string ToString();                       // "name old -> new"
    }

    public enum StylesheetOutcome   // CodeBrix.WebPlatform.FrontendBuilder.Stylesheet
    {
        Generated,                   // it was missing; Tailwind wrote it
        AlreadyPresent,              // left alone
        NotApplicable,               // no input stylesheet: no Tailwind here
        SwitchedOff,                 // GenerateMissingStylesheet = false
        SkippedTailwindNotAvailable, // missing: no packaged Tailwind beside the program
        SkippedTailwindFailed,       // missing: Tailwind ran and failed
    }

    Fails when: a frontend folder has no Cargo.toml; neither backend option
    is set; the project does not exist or does not build; the contract is not
    fully declared (every problem listed); src/generated holds a file without
    the generated marker; the shared crate's folder holds something else; the
    vendor folder holds files this library did not write (no stamp); a
    frontend's .cargo/config.toml was not written by this library; the
    platform's crates are not beside the build program (the package was not
    restored, or the program was copied without its frontend-crates folder);
    a crate bundle cannot be read or breaks the strict rule (see CRATE
    BUNDLES; nothing is written then); bringing a MARKED Cargo.lock in step
    fails (cargo is missing, or the frontend's Cargo.toml asks for a version
    the vendor folder does not hold) - the file is then left exactly as it
    was, and the message says what to do. A stale UNMARKED lock file is NOT
    a failure of MaterializeAsync: the command it names needs the vendor
    folder and the cargo configuration MaterializeAsync has just written.

STEP 3 - BuildAsync / ServeAsync
    BuildAsync builds EVERY application, one after the other, by running dx
    in its frontend folder, each from a clean target/dx folder:
        dx <GetDxBuildArguments(configuration, name, wasmSplit)>
    (--base-path <name> for a named application, --wasm-split for one built
    with splitting) with this environment:
        PATH     the packaged Tailwind's folder FIRST (tailwind/<rid>/ beside
                 the build program; dx runs the tailwindcss it finds on
                 PATH, so a Tailwind installed on the machine is never the
                 one it runs), then the packaged wasm-opt's folder (when the
                 WasmShrink package has one for this machine), then
                 wasm-bindgen's folder, then the existing PATH;
        DX_HOME  the application's DX_HOME (DxHomeDirectory for one
                 application): a folder the build owns, because dx looks in
                 its own data folder BEFORE PATH, and a wasm-opt some other dx
                 once downloaded there would be used instead;
        the loader's search variable - LD_LIBRARY_PATH (Linux),
                 DYLD_LIBRARY_PATH (macOS), PATH (Windows) - with
                 Libgit2Directory FIRST (see DX NEEDS libgit2);
        DX_SKIP_WASM_OPT  "1" for an unsplit Release build with Shrink =
                 false, "0" otherwise (so a value in your own environment
                 never decides it).
    Only dx's process gets these; the build program's own environment is not
    changed. ServeAsync starts dx with the same environment.
    It fails when dx fails or leaves no index.html.
    BEFORE dx is started, BuildAsync (every application) and ServeAsync (the
    one it serves) check, in CrateMode.PlatformCratesOnly, that no
    frontend's Cargo.lock is stale (see STEP 2, item 7): a stale one is a
    FrontendBuildException with the stale entries and the exact
    `cargo update --offline -p ...` command, and dx is not started - so the
    developer reads that message instead of cargo's "failed to select a
    version" from inside dx's output.
    THEN BOTH RUN STEP 1 THEMSELVES and refuse, before anything is started,
    when something they need is MISSING: Rust (too old, or without the
    WebAssembly target), the packaged Tailwind (not beside the build program,
    or does not start), dx and wasm-bindgen, the libgit2 library, and -
    for BuildAsync, only when the build will shrink - wasm-opt. The
    FrontendBuildException's message starts "The build cannot start: ..."
    (or "dx serve cannot start: ...") and lists each missing item with the
    check's own advice. So a build program without the packaged Tailwind gets
    that sentence, not
    dx's "Failed to hash asset .../assets/tailwind.css" - and never a build
    that quietly keeps an old generated stylesheet. Calling
    CheckPrerequisitesAsync first yourself is still useful to fail before
    slower work (MaterializeAsync builds the backend), as WorkSite's build
    program does.

    THE WEB ROOT. With a webRootDirectory (typically the backend's wwwroot),
    once EVERY application has been built, BuildAsync fills it:
      - the default application's site goes into the web root itself, a
        named application's into <web root>/<name>/, and the manifest
        codebrix-frontends.toml beside them (for the backend; see BACKEND:
        HOW THE FRONTEND APPLICATIONS ARE SERVED);
      - WITH a default application the build owns the WHOLE web root:
        everything in it is replaced (a .gitkeep file is kept);
      - when EVERY application has a name, the build owns only the
        applications' sub-folders (this build's, and those the previous
        manifest names) and the manifest; any other file in the web root -
        a page of the developer's own at "/", for example - is left alone;
      - a default application's site with an entry named like another
        application (a folder "sales" beside an application "sales") is
        refused with a FrontendBuildException, before the web root is
        touched;
      - then, beside each compressible file of the built applications, the
        compressed copies CompressOutput asks for (<name>.br and <name>.gz
        by default; see COMPRESSION). The developer's own files in the web
        root are never given copies.
    Without a webRootDirectory each site stays where dx left it
    (GetOutputDirectory), no manifest is written and nothing is compressed.

    public sealed class FrontendBuildResult   // the FIRST application's
    {
        string ApplicationName;                  // "app" for the default one
        IReadOnlyList<FrontendBuildResult> Applications;
                                                 // one per application, in
                                                 // order; the first is this
                                                 // object
        FrontendConfiguration Configuration; string OutputDirectory;
        bool Shrunk; WasmOptSource WasmOpt; HostTools Tools;
        bool WasmSplit;                          // built with splitting
        string WebRootDirectory;                 // null without a web root
        string WebRootApplicationDirectory;      // the web root itself, or
                                                 // <web root>/<name>
        int WebRootFileCount;                    // files copied for it
                                                 // (dx's files; the .br /
                                                 // .gz copies not counted)
        string ManifestFile;                     // the manifest written;
                                                 // null without a web root
        FrontendCompressionResult Compression;   // the copies written for
                                                 // it (see COMPRESSION);
                                                 // null without a web root
    }

    ServeAsync(applicationName) runs `dx serve --platform web [--base-path
    <name>] [--features <features>]` in that application's folder, with the
    same environment, its own ServeFeatures (or the options'), passes dx's
    output to Options.Output, and returns dx's exit code when it ends (cancel
    the token to stop it). Which application:
      - a name: that application; an unknown name is a FrontendBuildException
        listing the names;
      - no name (null, "", or ServeAsync without the argument): the
        application without a name; if there is none and there is exactly one
        application, that one; otherwise a FrontendBuildException listing the
        names.
    ServeAsync does NOT split the bundle, whatever WasmSplit says: hot reload
    serves one WebAssembly file. Nor does it compress anything (dx serve
    answers the browser itself; CompressOutput is for BuildAsync). Use BuildAsync to see the split pieces. The
    frontend's API calls need a running backend; Dioxus.toml can proxy /api
    to it.

    Call MaterializeAsync before BuildAsync or ServeAsync; neither does it by
    itself (a build program may run checks of its own in between).

THE PLATFORM'S CRATES
    The platform's crates are every crate a platform frontend may use without
    adding crates of its own: the shared crate's dependencies (dioxus,
    reqwest, serde, serde_json, web-sys, js-sys, wasm-bindgen,
    wasm-bindgen-futures, futures-core and what they need) with the Dioxus
    features the platform offers (minimal, web, router, document, mounted,
    asset, wasm-split, and devtools for hot reload), at the platform's
    versions, exactly as crates.io published them. An application chooses, with
    PlatformFrontendOptions.CrateMode, where its frontend's crates come from:

    CrateMode.PlatformCratesOnly (THE DEFAULT)
        The crates - the platform's and those of the registered crate
        bundles (see CRATE BUNDLES) - are written into the frontend
        (platform/vendor; with several applications, the first one's, shared
        by all) and the cargo configuration replaces crates.io with that
        folder and switches cargo to offline mode:

            [source.crates-io]
            replace-with = "codebrix-webplatform-crates"
            [source.codebrix-webplatform-crates]
            directory = "platform/vendor"
            [net]
            offline = true

        (directory is the path from that frontend to the vendor folder.)
        Nothing is downloaded, ever. A crate (or a version) that is not among
        the platform's or a registered bundle's is an error ("no matching
        package"); that is the moment to switch mode.

    CrateMode.ApplicationAddsCrates
        For an application that adds crates of its own - it MAY add any crate.
        No vendor folder is written (one written before is removed) and the
        cargo configuration sets nothing, so cargo fetches from crates.io as
        usual. The application's Cargo.lock still pins the platform's crates
        to the platform's versions, and cargo checks every download against
        the checksum recorded there. Start from a lock file resolved in the
        default mode (or the starting one MaterializeAsync writes) so the
        platform's versions are the ones locked. Keeping such an application
        offline (vendoring its own crates as well) is the application's
        business: `cargo vendor` in the frontend and a cargo configuration of
        its own in a folder above the frontend. Registered crate bundles are
        checked as in the default mode but not unpacked: cargo fetches their
        crates from crates.io like any other (see CRATE BUNDLES for what pins
        their versions then).

    Either way the frontend keeps a SMALL Cargo.toml of its own: the shared
    crate as a path dependency, dioxus with the features it uses, the
    platform crates its own code names directly, and - in the second mode -
    any crate of its own. Its Cargo.lock is its own (commit it; see THE
    FRONTEND'S Cargo.lock for what the build may change in it). Everything
    MaterializeAsync writes - platform/, src/generated/, .cargo/ - is build
    output: git-ignore it and never edit it. A cargo setting of the
    application's own belongs in a .cargo/config.toml in a folder ABOVE the
    frontend (cargo reads both).

    public static class PlatformCrates      // ...FrontendBuilder.Crates
    {
        public const string FolderName = "frontend-crates";
        public const string ArchiveFileName = "platform-crates.zip";
        public const string NoticesFileName =
            "THIRD-PARTY-NOTICES-PlatformCrates.txt";
        public const string StampFileName = ".codebrix-platform-crates";
        // each takes (string applicationBaseDirectory = null): the build
        // program's folder; null = AppContext.BaseDirectory of the program
        public static string GetDirectory(...);    // <it>/frontend-crates
        public static string GetArchivePath(...);  // .../platform-crates.zip
        public static string GetNoticesPath(...);  // .../THIRD-PARTY-NOTICES-..
        public static string ReadNotices(...);     // the text; throws
            // FrontendBuildException when the file is missing
        public static string LockFileText { get; } // the platform's Cargo.lock
    }
    // and, in the same class:
        public const string LockFileMarker;       // the marker line (below)
        public const string LockFileMarkerPrefix; // how it is recognised
    public enum CrateMode { PlatformCratesOnly, ApplicationAddsCrates }

THE FRONTEND'S Cargo.lock
    The frontend's Cargo.lock is the application's file and is committed.
    WHAT THE BUILD DOES TO IT, and nothing else:
      * NO FILE: MaterializeAsync writes the platform's lock file as the
        starting point, with this MARKER LINE as its THIRD line, directly
        after cargo's own two header comment lines:

            # CodeBrix.WebPlatform keeps this file in step with the platform's crates. Remove this line to manage the file yourself.

        (PlatformCrates.LockFileMarker; recognised by its start,
        PlatformCrates.LockFileMarkerPrefix, so a later version may reword
        the rest.)
      * MARKER PRESENT, CrateMode.PlatformCratesOnly: when the lock file names
        a version of a crate that the vendor folder (the platform's crates
        and the registered bundles') holds ONLY in other versions - after a
        package upgrade moved that crate - MaterializeAsync runs
        `cargo update --offline -p <crate> ...` for EXACTLY those crates in
        the frontend folder, after the vendor folder and the cargo
        configuration are written. Every other entry stays as it is: crates
        that did not move, crates the vendor folder does not carry at all,
        and a version the folder holds beside another. The marker line
        stays. MaterializeResult.LockFileRepairs says what changed (crate,
        old version, new version); commit the changed file. When the update
        fails, the file is left exactly as it was and MaterializeAsync throws
        a FrontendBuildException that says what to do.
      * MARKER ABSENT: the file is the developer's. The build NEVER changes
        it. When it is stale, MaterializeAsync reports it (LockFileWarning,
        StaleLockFileEntries, LockFileUpdateCommand, and a WARNING line on
        Output) with the exact command, and BuildAsync / ServeAsync refuse to
        start dx with the same message. Run the command in that frontend
        folder, then commit the file.
      * CrateMode.ApplicationAddsCrates: NOTHING happens, marker or not. cargo
        takes crates from crates.io, which keeps every published version, so
        a locked version never disappears and the lock file is never stale in
        this sense; moving to newer versions is the developer's own
        `cargo update`. (The marker is harmless there and is acted on again
        if the application goes back to the default mode.)
      * A lock file whose versions all exist is never touched, marker or not.
    WHAT THE DEVELOPER CANNOT DO: move the marker line. cargo keeps a comment
    on the third line, on an ordinary build and when it rewrites the file,
    but it DELETES one at the end of the file, and a comment above its own
    header makes it write the header again. Leave the line where it is.
    REMOVING the line is how the developer takes the file over; putting it
    back as the third line hands it to the platform again.

NOTICES FOR A BUILT SITE
    The WebAssembly file of a platform application contains third-party code:
    the platform's crates it compiles in, and the shared crate's SignalR
    client (derived from the MIT-licensed signalr-client crate). The notices
    for all of it are generated with the package and handed to the build
    program:

        string platformNotices = PlatformCrates.ReadNotices();

    Serve them with the site - typically as THIRD-PARTY-NOTICES.txt in the
    backend's wwwroot, after the application's own notices (crates it adds,
    its stylesheet tool, anything else it ships):

        File.WriteAllText("backend/MyApp.Api/wwwroot/THIRD-PARTY-NOTICES.txt",
            File.ReadAllText("THIRD-PARTY-NOTICES.txt") + "\n\n\n"
                + PlatformCrates.ReadNotices());

    (Write it AFTER BuildAsync: with a default application BuildAsync
    replaces the web root's whole content. When every application has a
    name the build leaves the file alone, but writing it after the build is
    right either way.) The Backend package serves it like any other file of
    the web root, at /THIRD-PARTY-NOTICES.txt. One file serves every
    frontend application of the site. PlatformCrates.ReadNotices covers the
    platform's crates only: it does not cover the crates a registered crate
    bundle adds (serve their notices too, from the package that brings
    them), and crates an application adds of its own are the application's
    to record.
    The dx and wasm-bindgen programs are build tools and are not part of the
    site; their licence and notices files travel beside them in the build
    program's frontend-tools folder.

SHRINKING
    Shrink = null (default)  a Release build is shrunk, a Debug build is not.
    Shrink = true            a Debug build is shrunk too (each .wasm file of
                             the output, after dx; debug file names are not
                             content-hashed, so this is safe).
    Shrink = false           a Release build is NOT shrunk either: dx is told
                             to skip wasm-opt (the environment variable
                             DX_SKIP_WASM_OPT, which the platform's dx reads),
                             prints "Skipping wasm-opt at the caller's
                             request", and no wasm-opt is needed. A Debug
                             build is not shrunk anyway.
    FrontendBuildResult.Shrunk tells what happened: true for Release (unset or
    true) and for Debug with true; false for Debug (unset or false) and for
    Release with false - unless dx did not confirm the skip (a dx without the
    platform's patch ignores the variable and shrinks), then true. When nothing
    was shrunk the "wasm-opt failed" check does not apply, and WasmOpt is
    Missing.
    A release build is shrunk BY dx, because dx names the files after their
    content AFTER shrinking; shrinking later would leave names that do not
    match. dx keeps going when wasm-opt fails (it logs "wasm-opt failed" and
    keeps the unshrunk file); BuildAsync reads dx's output and FAILS the build
    in that case. How hard wasm-opt works is dx's setting: [web.wasm_opt] in
    the frontend's Dioxus.toml (level "z", the smallest file, when unset).
    AN APPLICATION BUILT WITH SPLITTING (WasmSplit, see CODE SPLITTING) is
    ALWAYS shrunk, by dx, the main file and every piece, in a Debug build
    too: Shrink is ignored for it, a wasm-opt must be available, and Shrunk
    is true. Each application of a build is decided on its own.

HOST TOOLS
    FindHostTools (and BuildAsync, ServeAsync, CheckPrerequisitesAsync) look
    for dx and wasm-bindgen in this order:
      1. <build program folder>/frontend-tools/<rid>/ - put there by the
         host-tools package (see INSTALLATION). Used whenever that folder
         exists; both files must be in it.
      2. DxPath and WasmBindgenPath from the options (both, or neither) - for
         a build program that provides its own builds of the two programs.
      3. Otherwise FrontendBuildException naming the package to add for this
         machine, or saying that the machine is not one of the five build
         platforms.
    On Linux and macOS a found program is given the execute permission if it
    lacks it (a NuGet package does not carry that permission).
    The host-tools packages ship dx (the Dioxus CLI, with patches of the
    platform's: it never downloads anything, sends no telemetry, can skip
    wasm-opt on request, and every dx command is there, `dx new`, `dx init`
    and `dx components` included) and wasm-bindgen, each matching the
    platform's crates.

THE PACKAGED TAILWIND
    Each host-tools package depends on the Styling package of its platform,
    CodeBrix.WebPlatform.Styling.Tailwind4.<Platform>.MitLicenseForever,
    which carries Tailwind Labs' standalone program unchanged (with its
    licence, its third-party notices and version.txt) and copies it into the
    build program's output as tailwind/<rid>/tailwindcss[.exe]. (That package
    depends on CodeBrix.WebPlatform.Styling.Tailwind4.Sources, the source of
    the program, which travels with it.) The library uses ONLY that program:
      - the prerequisite check finds it there and starts it with `--help`;
      - MaterializeAsync runs it by its full path for a missing stylesheet;
      - dx is started with its folder FIRST on PATH (dx runs the tailwindcss
        it finds on PATH).
    There is no options path and no fallback to a Tailwind installed on the
    machine; without the folder, the check, BuildAsync and ServeAsync name
    the package to add. A build program that names its own dx and
    wasm-bindgen (DxPath, WasmBindgenPath) and references no host-tools
    package references the Styling package of the machine itself. On Linux
    and macOS the program is given the execute permission if it lacks it.

    public sealed class PackagedTailwind   // CodeBrix.WebPlatform.FrontendBuilder.Tools
    {
        public const string FolderName = "tailwind";
        public const string ProgramName = "tailwindcss";   // .exe on Windows
        public static string GetPackageId(string runtimeIdentifier);
            // for "linux-x64":
            // "CodeBrix.WebPlatform.Styling.Tailwind4.LinuxX64.MitLicenseForever";
            // null for a machine the platform does not build on
        public string Path, Directory, RuntimeIdentifier;
    }

DX NEEDS libgit2
    dx links the libgit2 library as a SHARED library (it compiles none of
    libgit2's code in) and the operating system's loader resolves it when dx
    STARTS: without it dx does not run at all, for any command. The library
    comes from the CodeBrix.LibGit2.MitLicenseForever package, which
    FrontendBuilder depends on; its native file lands beside the build program
    (runtimes/<rid>/native/, or the program's own folder for a build for one
    runtime). Before every start of dx - BuildAsync, ServeAsync and the
    prerequisite check - the library:
      1. reads from dx the exact name it was linked against, for example
         libgit2-<commit>.so.<abi> (Linux), @rpath/libgit2-<commit>.<abi>.dylib
         (macOS) or git2-<commit>.dll (Windows);
      2. finds the package's file, named after the libgit2 commit the package
         reports for itself, and checks that it IS that library (it reads the
         library's own name); a disagreement fails with both names;
      3. copies it, under the name the loader looks for, into
         Libgit2Directory (DxHomeDirectory/libgit2), again only when the
         package's file changed;
      4. puts that folder first on the loader's search variable of dx's
         process: LD_LIBRARY_PATH (Linux), DYLD_LIBRARY_PATH (macOS - verified
         on a Mac 2026-11-22), PATH (Windows - verified on Windows 2026-11-22).
    public string Libgit2Directory { get; }   // DxHomeDirectory/libgit2

    STARTING dx YOURSELF (outside the library) needs the same: on Linux a
    folder holding the package's libgit2-<commit>.so under the name dx asks
    for (libgit2-<commit>.so.<abi>; `readelf -d dx` lists it) on
    LD_LIBRARY_PATH; on macOS the package's libgit2-<commit>.dylib under its
    install name's last part (libgit2-<commit>.<abi>.dylib) on
    DYLD_LIBRARY_PATH, set for dx itself (macOS drops DYLD_* variables when it
    starts a protected program such as /bin/sh or /usr/bin/env); on Windows
    git2-<commit>.dll beside dx.exe or in a folder on PATH. After one build,
    Libgit2Directory is such a folder. Without it dx prints a loader error -
    on Linux "error while loading shared libraries: libgit2-...: cannot open
    shared object file" - and exits.

    public sealed class HostTools
    {
        public const string FolderName = "frontend-tools";
        public const string DxName = "dx", WasmBindgenName = "wasm-bindgen";
        public static IReadOnlyList<string> SupportedRuntimeIdentifiers;
        public static string CurrentRuntimeIdentifier;   // e.g. "linux-x64"
        public static string GetPackageId(string runtimeIdentifier);
            // for "linux-x64":
            // "CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever";
            // null for a machine the platform does not build on
        public string DxPath, WasmBindgenPath, WasmBindgenDirectory;
        public HostToolsSource Source;                   // Package | Options
        public string RuntimeIdentifier;
    }


CODE SPLITTING
==============

Bundle splitting (lazy loading) cuts an application's WebAssembly into a
main file and PIECES that the browser downloads when they are first needed,
so a visitor does not download the code of pages they never open. It is
dx's `--wasm-split`, which dx itself calls EXPERIMENTAL. It is decided per
frontend application.

SWITCHING IT ON takes both of these:

    # 1. the frontend's Cargo.toml: the wasm-split feature of dioxus (it is
    #    among the features the platform's crates offer)
    dioxus = { version = "...", default-features = false,
               features = ["minimal", "web", "router", "wasm-split", ...] }

    // 2. the build program: WasmSplit for that application
    options.Applications.Add(new FrontendApplicationOptions
        { Name = "sales", FrontendDirectory = "frontends/sales",
          WasmSplit = true });
    // (one application: options.WasmSplit = true)

WHAT IS SPLIT. With the feature on, EVERY route of the application's
Routable enum becomes a piece of its own, loaded when the route is first
shown: dx writes one module_<n>_route<Name>...wasm per route, the main file,
and shared chunk_<n>_split...wasm files for code several pieces use. A
second visit to a page downloads nothing. The lower-level forms, in
dioxus::wasm_split, split off code by hand:

    #[wasm_split::wasm_split(forecast)]       // an ASYNC function whose code
    async fn run_forecast(runs: u32) -> Forecast { ... }   // is the piece
                                              // "forecast", downloaded on the
                                              // first call
    static PANEL: wasm_split::LazyLoader<Order, Element> =
        wasm_split::lazy_loader!(extern "details"
            fn panel(order: Order) -> Element);
                                              // PANEL.load().await downloads
                                              // the piece; PANEL.call(order)
                                              // then runs it
    #[component(lazy)]                        // a component whose body is a
    fn Insights(..) -> Element { ... }        // piece (every lazy component of
                                              // an application shares the one
                                              // piece "lazy"); it suspends
                                              // until the piece has arrived

  * #[wasm_split] takes async functions only, and drops the function's `pub`
    in the WebAssembly build: keep it private and call it from a public
    function beside it.
  * In a build without splitting (ServeAsync, or WasmSplit off) all of this
    is ordinary code that is already loaded.

THE LOADING STATE. A route's piece (or a lazy component) SUSPENDS while it
downloads. Put a SuspenseBoundary around the Router, or around the Outlet
inside the layout, so that the layout stays on screen and a fallback shows:

    SuspenseBoundary {
        fallback: |_| rsx! { PageLoading {} },
        Outlet::<Route> {}
    }

A WARNING PER ROUTE. With the feature on, the router macro generates a
function and a loader per route named after the route in camel case
(route<Name><hash>), which the compiler reports as non_snake_case, once per
route. The names are the macro's; an allow on the enum does not reach them.
Keep the Routable enum in a module of its own and allow the lint there:

    // routes.rs - the Routable enum and nothing else
    #![allow(non_snake_case)]

SHRINKING. A split build ALWAYS runs wasm-opt: dx shrinks the main file and
every piece, for a Debug build too; Shrink is ignored for it and
FrontendBuildResult.Shrunk is true (see SHRINKING). dx switches on link-time
optimisation and debug information for the compile by itself; the shipped
files carry no debug or name sections.

WHAT ELSE CHANGES. dx writes one extra generated JavaScript file for a split
build, the loader of the pieces, beside the main script (toolchain glue,
like the rest of the generated JavaScript). The pieces are fetched from
/<base path>/assets/..., so splitting works for a named application as well
as for the one at the site root. The Content-Security-Policy a platform site
needs is unchanged: the pieces are WebAssembly from the same site, compiled
under the 'wasm-unsafe-eval' the site needs anyway. ServeAsync never splits
(see STEP 3).

FILE NAMES AND THE BROWSER'S CACHE. In a Release build every .wasm, .js and
.css file of the output carries a hash of its content in its name
(name-dxh<hash>.ext), the pieces and the loader included. An UNCHANGED
application built again IN THE SAME FOLDER gets the same file names and the
same bytes, split or not, so a returning visitor's browser keeps using what
it has. What a CHANGE renames depends on the change:
  * a change that keeps the length of the source file (a word swapped for
    one of the same length) renames that page's piece and the files that
    name it - the loader, the main script - and index.html gets new bytes;
    the main .wasm and every other piece keep their names;
  * a change that makes a source file longer or shorter can rename every
    piece and the main .wasm: the wasm-split macro of the platform's Dioxus
    names each split point from its position in the compiled source, and
    the main file's data moves. Expect the whole split application to be
    downloaded again after such a change. The stylesheet (unless the change
    adds or removes a class), the images and the generated snippets keep
    their names.
NOT the same: a build of the same source in ANOTHER folder. The compiled
code carries the absolute paths of the frontend's folder and its crates
(dx's asset references are named from them), so the main file, the pieces
and everything that names them get other bytes and other names. Build a
site that is deployed from one place in one fixed folder.
KEEP target/ IGNORED: Tailwind looks for class names in every file of the
frontend folder that a .gitignore does not exclude. Without a .gitignore
line for target/ (the build output) it reads the build's own files, and the
stylesheet - and its name - changes from one build to the next.


CRATE BUNDLES
=============

A crate bundle is a set of Rust crates that a NuGet package brings to a
platform frontend, beside the platform's own crates - for example a package
whose frontend half is Rust. The frontend build writes the bundles' crates
into the one vendor folder, so a frontend that uses them still builds
offline in the default crate mode.

USING A PACKAGE THAT BRINGS A BUNDLE (the application developer):

  1. Reference the package from the BUILD PROGRAM (and wherever else its
     documentation says).
  2. Make the package's ONE registration call in the build program, BEFORE
     MaterializeAsync. Nothing registers itself: a package that is only
     referenced adds no crate. The call hands a CrateBundle to
     CrateBundles.Register; registering the same bundle twice is harmless.
  3. Name the crate(s) you use in the frontend's own Cargo.toml, as with any
     platform crate.
  4. Materialize. MaterializeResult.CrateBundles says, per bundle, which
     crates it added (AddedCrates) and which the platform or an earlier
     bundle already had (SharedCrates).

THE STRICT RULE. A crate is identified by its name and version. The same
name and version must have the same content wherever it comes from (compared
through the crate's .cargo-checksum.json); ONE copy of it is written however
many bundles carry it. And a bundle must not bring a version of a crate that
the platform or ANOTHER bundle carries in a different version. (The
platform's own set holds some crates in two versions, and one bundle may
bring two versions of a crate nobody else has: neither is a violation.) On a
violation NOTHING is written, and one FrontendBuildException lists every
such crate with the versions and the packages, and says to use versions of
those packages built against the same platform version, or to leave one of
them out.

THE VENDOR FOLDER'S STAMP covers the platform's archive and every bundle, so
adding, removing or updating a bundle rewrites the folder, and an unchanged
set costs nothing.

UPDATING A PACKAGE THAT BRINGS A BUNDLE can move one of its crates to a new
version. The frontend's Cargo.lock still names the old one, which is no
longer in the vendor folder. With the marker line in the lock file,
MaterializeAsync brings exactly that entry in step; without it, it names the
`cargo update --offline -p <crate>` command to run (see THE FRONTEND'S
Cargo.lock).

IN CrateMode.ApplicationAddsCrates the same check runs, but nothing is
unpacked: cargo fetches the bundles' crates from crates.io like any other.
NOTHING then pins a bundle's crate to the bundle's version: that is the
APPLICATION'S job, with an exact requirement in its Cargo.toml
(`some-crate = "=x.y.z"`, the version the bundle carries) or its committed
Cargo.lock.

WRITING A PACKAGE THAT BRINGS A BUNDLE (the package author):

  * The package depends on Core ONLY - never on FrontendBuilder - with an
    EXACT version pin on Core, because its crates are chosen against one
    platform release:
        <PackageReference Include="CodeBrix.WebPlatform.Core.ApacheLicenseForever"
                          Version="[x.y.z.w]" />
  * It carries ONE zip archive of crates exactly as crates.io published them,
    what `cargo vendor` writes: one folder per crate at the top of the
    archive, each with its Cargo.toml and .cargo-checksum.json, and nothing
    else at the top. Name the folders name-version (what
    `cargo vendor --versioned-dirs` writes), as the platform's own are, so
    that no folder name can meet another crate's.
  * It offers ONE public call that registers the bundle:

        public static class GraphicsCrates
        {
            public static void Register() =>
                CrateBundles.Register(new CrateBundle(
                    "graphics",                      // unique in a build
                    "MyCompany.Graphics",            // the package id
                    "x.y.z",                         // the package version
                    () => File.OpenRead(Path.Combine(
                        AppContext.BaseDirectory,
                        "graphics-crates.zip"))));   // opened when needed
        }

    and the archive reaches the build program's output folder the way the
    package arranges it (for example a buildTransitive targets file that
    copies it beside the program).
  * Registering ANOTHER bundle under a name that is already registered by
    another package, or another version of the same package, throws
    InvalidOperationException.
  * The package's own notices for its crates are its business: the
    platform's notices (PlatformCrates.ReadNotices) do not cover them.


FRONTENDBUILDER API REFERENCE: THE SHARED RUST CRATE
====================================================

Every platform frontend compiles in one small crate of shared code,
`codebrix-webplatform` (in Rust source: `codebrix_webplatform`). Its source
travels INSIDE the FrontendBuilder assembly; MaterializeAsync writes it out
(or call SharedRustCrate.ExtractTo yourself).

    public static class SharedRustCrate     // ...FrontendBuilder.SharedCrate
    {
        public const string CrateName = "codebrix-webplatform";
        public const string RustName  = "codebrix_webplatform";
        public static IReadOnlyList<SharedRustFile> Files { get; }
        public static IReadOnlyList<string> ExtractTo(string directory);
    }
    public sealed class SharedRustFile { string RelativePath; byte[] Content; }

ExtractTo(directory) makes a folder hold exactly the crate: unchanged files
are left untouched, changed and missing files are written, stray files are
deleted (cargo's own `target` folder and Cargo.lock are kept). It returns the
relative paths it wrote or deleted. It throws InvalidOperationException, and
writes nothing, when the folder is not empty and is not a copy of this crate:
give the crate a folder of its own. ArgumentException for a blank folder name.

The frontend names that folder as a path dependency:

    # frontend/Cargo.toml
    [dependencies]
    codebrix-webplatform = { path = "platform/codebrix-webplatform" }

The extracted crate is BUILD OUTPUT, like the generated code: never edit it in
an application, do not commit it, ignore its folder in git. It always matches
the FrontendBuilder package in use (and so the BackendMapper the generated code
was written with).

The crate depends on dioxus, reqwest, serde, serde_json, web-sys, js-sys,
wasm-bindgen, wasm-bindgen-futures and futures-core. The versions it names are
the platform's: the application's frontend uses the same ones (one Cargo.lock
resolves both). It switches on the web-sys features its own code needs; the
frontend names only the ones ITS code uses.

THE ONE SET-UP CALL

The frontend sets the platform's clients up ONCE, on its first render, before
any request, with ONE call - the one generated from its backend:

    #[component]
    fn App() -> Element {
        use_hook(|| generated::configure_platform(session::update_user, session::session_ended));
        ...
    }

The generated configure_platform fills in what the BACKEND declares - its
request marker ([assembly: WireRequestMarker]) and the path of its
[WireSessionRefresh] action - and hands over what the APPLICATION does when a
session is renewed (on_session_refreshed, given the refresh action's decoded
answer) or ends (on_session_ended). It calls the crate's own set-up call:

    codebrix_webplatform::configure(config: PlatformConfig)
    pub struct PlatformConfig {          // codebrix_webplatform (Default)
        pub request_marker: Option<String>,        // X-Requested-With value
        pub session_refresh_path: Option<String>,  // None: no sign-in
        pub on_session_refreshed: Option<Rc<dyn Fn(&str)>>,  // the answer's body
        pub on_session_ended: Option<Rc<dyn Fn()>>,
    }
    pub fn setup::json_hook<T: DeserializeOwned>(hook: impl Fn(T) + 'static)
        -> Rc<dyn Fn(&str)>   // decodes the answer as T; anything else is ignored

which sets up BOTH clients from that one value:
  - the REST client sends the marker on every state-changing request, renews
    an expired session at the refresh path and calls the two hooks;
  - the SignalR client renews an expired session for a hub that needs sign-in
    through the SAME shared refresh (REST calls and hubs never refresh at the
    same time), and sends the marker with every negotiate request (negotiate
    is a POST, and the hubs live under /api, see DECLARING A HUB).
Both hooks run inside the application's Dioxus runtime, so they may write
global signals. Calling it from the first render (a use_hook in the root
component) lets the crate learn that runtime at once. Calling it again
replaces everything set before. Session HANDLING - what is stored, what is
shown when a session ends - is the application's own code; the platform only
calls the hooks. The lower-level `api::client::configure` and
`signalr::configure` / `signalr::set_negotiate_headers` still exist for an
application with needs of its own; a platform application does not call
them.

WHAT THE CRATE PROVIDES (Rust)

  codebrix_webplatform::{configure, PlatformConfig}, codebrix_webplatform::setup
      The one set-up call (above).

  codebrix_webplatform::api::client
      The one HTTP client. Every generated REST function goes through it.
        - same-origin absolute URLs, so the browser sends the HttpOnly session
          cookies itself;
        - the X-Requested-With header on every request that changes state;
        - on 401 from an endpoint that needs sign-in: ONE refresh call, shared
          by every request waiting on it, and ONE retry; if that fails too the
          application is told the session ended;
        - a success answer read as JSON (send_json), as nothing
          (send_empty) or as the bytes it is (send_bytes, which the
          generated <name>_bytes functions of [WireFile] actions call);
        - the browser's HTTP cache: send_bytes with GET or HEAD uses the
          fetch cache mode "no-cache" (the browser may keep the answer and
          revalidates it with the server every time, so the backend's
          Cache-Control and ETag decide whether it is downloaded again);
          everything else - send_json, send_empty, the session refresh,
          send_bytes with any other method - is "no-store";
        - any non-success answer becomes an ApiError, whichever of the three
          sent it (an error answer is always the standard JSON problem body);
        - it keeps note of the requests that change state while they are in
          flight (any of them may set or clear the session cookies: sign-in,
          sign-out, a refresh); the SignalR client waits for them before it
          negotiates (see THE SESSION COOKIES under the SignalR client).

      What it is told comes from the one set-up call; it lands here as:
      pub struct ClientConfig {             // Default: everything None
          pub requested_with: Option<String>,   // the header value; None: none sent
          pub refresh_path: Option<String>,     // None: a 401 means "ended"
          pub on_refreshed: Option<Rc<dyn Fn(&str)>>,   // body of the refresh answer
          pub on_session_ended: Option<Rc<dyn Fn()>>,
      }
      pub const REQUEST_MARKER_HEADER: &str = "X-Requested-With";
      pub fn configure(config: ClientConfig);   // the REST client alone
      pub async fn refresh_session() -> bool;   // shared with the SignalR client;
                                                // false at once without a path
      pub enum Auth { Session, Public }
      pub use reqwest::Method;
      pub async fn send_json<T>(method, path, body, auth) -> Result<T, ApiError>
      pub async fn send_empty(method, path, body, auth) -> Result<(), ApiError>
      pub async fn send_bytes(method, path, body, auth) -> Result<Vec<u8>, ApiError>
      pub fn body<B: Serialize>(value: &B) -> Result<String, ApiError>

  codebrix_webplatform::api::{ApiError, ApiResult}
      pub enum ApiError {
          Problem { status: u16, title: String, code: String,
                    errors: BTreeMap<String, Vec<String>> },   // an ApiProblem
          Unauthorized,   // 401 with an empty body: the session is gone
          Network,        // no answer
          Decode,         // an answer that could not be read
      }
      message() -> String        a sentence fit to show, always
      status() -> Option<u16>    code() -> &str    is_not_found() -> bool
      field(name) -> Option<String>     the first message for a field
      form_message(known_fields) -> String   the title plus messages for
                                             fields the form does not show
      pub type ApiResult<T> = Result<T, ApiError>;

  codebrix_webplatform::dates::Date
      A calendar date without time or zone; the default Rust type of a .NET
      DateOnly. JSON is the text yyyy-MM-dd. Copy, Ord, Default (1970-01-01),
      Display. new, parse, parse_timestamp, to_iso, add_days, add_months,
      start_of_week (Monday), start_of_month, end_of_month, start_of_year,
      weekday_short, format_medium ("Nov 17, 2026"), format_day_month,
      format_with_weekday; free functions format_range, month_name,
      month_short, days_in_month, is_leap_year.

  codebrix_webplatform::query::{encode, QueryString}
      Percent-encoding and `?a=1&b=2` building that skips empty values.

  codebrix_webplatform::browser
      What every platform page needs from the browser, through web-sys /
      js-sys (no JavaScript):
      pub fn set_title(title: &str)      // document.title, only when it changes
      pub fn origin() -> String          // window.location.origin
      pub fn today() -> Date             // the local calendar date
      pub fn local_hour() -> u32         // the local hour, 0-23
      pub fn local_time(instant: &str) -> String
                                         // "14:05" for an ISO-8601 instant in
                                         // the local zone; "" when not one
      pub fn navigate(path: &str)        // what to hand a [WireFile] address to
      pub async fn sleep_ms(ms: i32)
      THE PAGE TITLE: set it with browser::set_title (for example from a small
      PageTitle component each page renders), NOT with Dioxus's
      document::Title. That component sets the title by running script text,
      which the Content-Security-Policy a platform site is served with forbids:
      the browser refuses it and logs a violation, and the title never changes.

  codebrix_webplatform::signalr
      The SignalR client: the JSON hub protocol over WebSockets, through the
      web-sys bindings (no JavaScript). The generated hub types (hubs.rs) call
      it; use it directly for anything they do not offer.

      CONNECTING. There is ONE connection per hub path, shared by everyone who
      holds a share of it. `XHub::use_hub()` in a component (released when it
      unmounts) or `XHub::connect()` elsewhere takes a share. The first share
      opens the connection: it negotiates at the page's own origin plus the
      hub's path (so the browser sends the session cookie), opens the socket,
      pings when it has sent nothing for 15 s, counts the connection as lost
      when the server has been silent for 30 s, and reconnects with backoff
      (at once, then 2 s, 10 s, 30 s, then every 30 s; at once when the tab
      becomes visible again or the network comes back). The connection closes
      about 2 s after the last share is dropped.

      THE SESSION COOKIES. ASP.NET Core binds a hub's connection token to the
      user who negotiated it and refuses (403) the socket when the browser
      opens it as another user, or as nobody. The cookies change when a
      request that changes state is answered - sign-in, sign-out, a refresh -
      so an attempt to connect never negotiates while such a request (sent
      through api::client) is in flight: it waits for it (up to 10 s), and
      when one is sent or answered while it negotiates, it throws the token
      away and negotiates again (up to 3 times) instead of opening a socket
      the server would refuse. Public hubs too: their requests carry the
      cookies like any other. A request made some other way than through
      api::client is not seen.

      SET-UP: the one set-up call (THE ONE SET-UP CALL) does it. When
        negotiate for a hub that needs sign-in answers 401, the session is
        renewed ONCE through api::client::refresh_session, the same shared
        refresh the REST client uses, and the attempt is repeated; without a
        refresh path a 401 leaves the connection Disconnected. Every
        negotiate request carries the backend's request marker. (Underneath:
        signalr::configure(SignalRConfig::using_rest_refresh()) and
        signalr::set_negotiate_headers; an application with a refresh of its
        own may call those after the set-up call:
        `SignalRConfig { refresh: Some(Rc::new(|| Box::pin(async { true })
        as RefreshFuture)) }`.)

      SIGNING IN AND OUT. Tell the client when it happens:
          signalr::session_ended();     // signed out: every hub that needs
                                        // sign-in closes and stays closed;
                                        // its calls fail with Unauthorized
          signalr::session_started();   // signed in: those connections that
                                        // still have shares reconnect
      Calling session_ended() as soon as the user asks to sign out, before
      the sign-out request is answered, is fine: a hub that connects
      meanwhile (a public one on the page shown next) waits for that request
      (THE SESSION COOKIES).

      THE DIOXUS RUNTIME IS TAKEN CARE OF. Messages, state changes and
      reconnects are handled, and sessions refreshed, on the client's own
      tasks, which Dioxus does not poll. The client captures the application's
      Dioxus runtime when a handler is registered (and at the set-up call and
      connect) and runs every handler, and the refresh, inside it, at the
      root scope. So a handler may read and write any signal, global signals
      included, with no wrapper of your own. Register handlers while
      rendering (in a hook) or from a Dioxus task. A task spawned inside a
      handler belongs to the root scope, not to the component that registered
      the handler. Outside any Dioxus runtime (a native unit test) handlers
      simply run.

      WHAT THE GENERATED HUB TYPE OFFERS (per hub; see WHAT THE GENERATED
      CODE LOOKS LIKE): connect() / use_hub(); connection(); state() /
      use_state() (a ReadSignal that re-renders); one `async fn` per hub
      method -> Result<T, HubError>; a HubStream<T> per streaming method;
      per message the hub sends, on_<message>(handler) -> Subscription and
      use_<message>() -> ReadSignal<Option<T>>, the latest message (it starts
      with the last one the shared connection has seen).

          #[component]
          fn Clock() -> Element {
              let tick = ClockHub::use_tick();          // latest Tick
              let hub = ClockHub::use_hub();
              let state = hub.use_state();
              let count = use_signal(|| 0);
              use_subscription(|| hub.on_tick(move |_| {
                  let mut count = count;              // a signal is Copy
                  count += 1;
              }));
              rsx! { "{state:?} {count} {tick:?}" }
          }

      HOOKS (all register once, on the first render)
          use_hub(&HUB) -> HubConnection
          use_connection_state(&connection) -> ReadSignal<ConnectionState>
          use_hub_message(&HUB, "Method", decode) -> ReadSignal<Option<T>>
          use_hub_handler(&HUB, "Method", handler)
          use_subscription(|| subscription(s))   keeps one Subscription, an
                                                 array or a tuple of them
                                                 while the component is
                                                 mounted

      pub fn connect(hub: &'static HubDescriptor) -> HubConnection
      HubConnection (Clone; every clone is a share of the connection)
          hub(), state()
          async invoke::<T>(method, args) -> Result<T, HubError>  (T = () for
              nothing; waits while the connection is being made)
          send(method, args) -> Result<(), HubError>   (no answer; fails at
              once unless Connected)
          stream::<T>(method, args) -> HubStream<T>
          on(method, |args: &[Value]| ..), on_state_changed(|state| ..),
          on_reconnected(|| ..) -> Subscription
          last_message(method) -> Option<Vec<Value>>
        Arguments are built with to_arg(&value) and read with
        arg::<T>(args, index). Method names match without regard to case.
      Subscription: dropping it unregisters the handler; forget() keeps it
        for the life of the connection.
      HubStream<T>: `stream.next().await` gives Some(Ok(item)) per item, a
        failure once as Some(Err(error)), then None. It is also a
        futures_core::Stream. Nothing is sent until it is first polled;
        dropping it before the end cancels the stream on the server. It is
        read by the task that awaits it (a Dioxus task in a component).
      ConnectionState: Disconnected, Connecting, Connected, Reconnecting.
      HubError: NotConnected, Unauthorized, Network, Server(text), Decode,
        Cancelled, Closed. message() (and Display) is a sentence fit to show;
        for Server it is the server's text, and for a HubException thrown by
        the hub, the exception's own message.
      HubDescriptor { name, path, requires_sign_in }: one static per hub,
        written by the generator (CLOCK_HUB and so on).

      TESTING WITH THE FAKE HUB. `codebrix_webplatform::signalr::testing`
      exists in native builds only (plain `cargo test`; not in a wasm32
      build). A FakeHub stands in for the server; the client logic under test
      is the same code that runs in the browser, with a virtual clock that
      moves only when the test moves it.

          use codebrix_webplatform::signalr::testing::{advance, block_on,
              settle, FakeHub};
          use serde_json::json;

          #[test]
          fn ticks_reach_the_handler() {
              let fake = FakeHub::install(&CLOCK_HUB);  // BEFORE connecting
              fake.on_invoke("Now", |_| Ok(json!({"city": "Paris"})));
              let hub = ClockHub::connect();
              let now = block_on(hub.now());            // runs the client
              let seen = Rc::new(Cell::new(0));
              let sink = seen.clone();
              let _subscription = hub.on_tick(move |_| sink.set(sink.get() + 1));
              settle();
              fake.push("Tick", vec![json!({"city": "Paris"})]);
              assert_eq!(seen.get(), 1);
          }

        Free functions: settle() runs the client until nothing more happens;
        advance(ms) moves the clock (backoff, keep-alive, the 2 s linger);
        block_on(future) runs an invoke or a stream's next() to the end and
        panics if that needs the clock to move.
        FakeHub: install(&HUB) (dropping it uninstalls it and closes the
        connection); push(method, args); on_invoke(method, answer) (Ok(value)
        or Err(server text)); never_answer(method); on_stream(method, items),
        on_stream_open, on_stream_error(method, items, error); and to look
        at: sent(), cancelled_streams(), pings_received(), negotiations(),
        last_negotiate_url(), sockets_opened(), open_sockets(),
        last_socket_url(), refused_sockets(); and to misbehave:
        drop_connection(), refuse_connections(bool),
        require_sign_in_failure(bool) (401 until the refresh has run),
        silence(bool) (stop answering pings), on_negotiate(hook) (runs as
        each negotiate request arrives). Like the server, the fake refuses a
        socket opened after the session cookies changed since its token was
        negotiated (it counts the cookies as changed whenever a state-changing
        request through api::client finishes); refused_sockets() counts them.
        Everything is per thread. Keep the decisions in plain functions that
        take what a handler receives, and test those.


COMPLETE EXAMPLES
=================

1. A contract class, a controller and a hub (backend)
-----------------------------------------------------

    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using CodeBrix.WebPlatform.Core.Mapping;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;

    [WireRequired]
    public sealed class Note
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        [WireOptional]
        public string Body { get; set; }
        public DateOnly Day { get; set; }
    }

    [WireRequired]
    public sealed class NoteRequest
    {
        public string Title { get; set; }
        [WireOptional]
        public string Body { get; set; }
    }

    [ApiController]
    [Authorize]
    [Route("api/notes")]
    public sealed class NotesController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<Note>), StatusCodes.Status200OK)]
        public Task<IActionResult> List([FromQuery] DateOnly? day,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        [HttpPost]
        [ProducesResponseType(typeof(Note), StatusCodes.Status201Created)]
        public Task<IActionResult> Create([FromBody] NoteRequest request,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        [HttpGet("export.csv")]
        [WireFile]
        public IActionResult Export() => throw new NotImplementedException();
    }

    [WireRequired]
    public interface INotesClient
    {
        Task Added(Note note);
    }

    [WireHub("/api/hubs/notes")]
    [Authorize]
    public sealed class NotesHub : Hub<INotesClient>
    {
    }

    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        [HttpPost("refresh")]
        [AllowAnonymous]
        [WireSessionRefresh]       // renews an expired session; 401 when it cannot
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public Task<IActionResult> Refresh(CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    // WireDeclarations.cs (no namespace):
    //     [assembly: WireRequestMarker("MyApp")]

2. The backend's Program.cs (the two Backend calls)
---------------------------------------------------

    using CodeBrix.WebPlatform.Backend;
    using CodeBrix.WebPlatform.Core.Mapping;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public partial class Program
    {
        public const string ContentSecurityPolicy =
            "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; "
            + "style-src 'self'; img-src 'self' data:; connect-src 'self'; "
            + "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            // the application's own sign-in:
            //     builder.Services.AddAuthentication(...); AddAuthorization();
            builder.Services.AddPlatformBackend(options =>
                options.ContentSecurityPolicy = ContentSecurityPolicy);
            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            // serves the frontend applications the build wrote into wwwroot
            // (read from its manifest), the error bodies, the X-Requested-With
            // and Origin checks, the security headers
            app.UsePlatformBackend();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<NotesHub>(WireHubAttribute.GetPath(typeof(NotesHub)));
            app.Run();
        }
    }

The backend's project is a Microsoft.NET.Sdk.Web project that references the
Backend package; nothing in it names a frontend application. Its wwwroot is
the web root the build program builds into.

3. The build program (builds the frontend)
------------------------------------------

    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CodeBrix.WebPlatform.FrontendBuilder;
    using CodeBrix.WebPlatform.FrontendBuilder.Crates;

    public static class Program
    {
        public static async Task<int> Main()
        {
            var frontend = new PlatformFrontend(new PlatformFrontendOptions
            {
                FrontendDirectory = "frontend",
                BackendProjectPath = "backend/MyApp.Api/MyApp.Api.csproj",
                ServeFeatures = { "devtools" },
                Output = Console.Out,
            });
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                stop.Cancel();
            };

            var report = await frontend.CheckPrerequisitesAsync(stop.Token);
            foreach (var item in report.Items)
            {
                Console.WriteLine("  " + item);       // OK / NOTE / MISSING
            }

            if (!report.IsReady)
            {
                return 1;
            }

            try
            {
                // backend -> src/generated; shared crate and the platform's
                // crates -> platform/; .cargo/config.toml (offline)
                await frontend.MaterializeAsync(stop.Token);
                // the site, shrunk, copied into the backend's static files
                var built = await frontend.BuildAsync(
                    FrontendConfiguration.Release,
                    "backend/MyApp.Api/wwwroot",
                    stop.Token);
                // the notices the site serves: the application's, then the
                // platform's
                File.WriteAllText(
                    "backend/MyApp.Api/wwwroot/THIRD-PARTY-NOTICES.txt",
                    File.ReadAllText("THIRD-PARTY-NOTICES.txt") + "\n\n\n"
                        + PlatformCrates.ReadNotices());
                Console.WriteLine($"Built {built.WebRootFileCount} files.");
                // for development instead:
                //     return await frontend.ServeAsync(stop.Token);
                return 0;
            }
            catch (FrontendBuildException e)
            {
                Console.Error.WriteLine(e.Message);   // what failed, what to do
                return 1;
            }
        }
    }

The same steps by hand, with the lower-level APIs (no dx involved):

    using CodeBrix.WebPlatform.BackendMapper;
    using CodeBrix.WebPlatform.BackendMapper.Rust;
    using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

    var map = BackendMapper.Map(new BackendMapOptions
    {
        AssemblyPath = "backend/MyApp.Api/bin/Release/net10.0/MyApp.Api.dll",
    });                         // throws BackendMapException: all problems
    RustClientGenerator.WriteTo("frontend/src/generated",
        RustClientGenerator.Generate(map));
    SharedRustCrate.ExtractTo("frontend/platform/codebrix-webplatform");
    // the platform's crates and the cargo configuration are MaterializeAsync's

4. A build program with three frontend applications and splitting
-----------------------------------------------------------------

    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CodeBrix.WebPlatform.FrontendBuilder;
    using CodeBrix.WebPlatform.FrontendBuilder.Applications;
    using CodeBrix.WebPlatform.FrontendBuilder.Crates;

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var options = new PlatformFrontendOptions
            {
                BackendProjectPath = "backend/MyApp.Api/MyApp.Api.csproj",
                ServeFeatures = { "devtools" },
                Output = Console.Out,
            };
            // no name: served at the site root
            options.Applications.Add(new FrontendApplicationOptions
                { FrontendDirectory = "frontends/home" });
            // served below /sales, split into pieces (its Cargo.toml turns
            // on dioxus's wasm-split feature)
            options.Applications.Add(new FrontendApplicationOptions
                { Name = "sales", FrontendDirectory = "frontends/sales",
                  WasmSplit = true });
            // served below /admin, one WebAssembly file
            options.Applications.Add(new FrontendApplicationOptions
                { Name = "admin", FrontendDirectory = "frontends/admin" });
            var frontend = new PlatformFrontend(options);
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                stop.Cancel();
            };

            try
            {
                // a package that brings crates registers them here, BEFORE
                // materializing:  MyCompany.Graphics.GraphicsCrates.Register();
                await frontend.MaterializeAsync(stop.Token);   // all three
                if (args.Length > 0 && args[0] == "serve")
                {
                    // one application with hot reload: "sales", "admin", or
                    // none for the one at the site root
                    return await frontend.ServeAsync(
                        args.Length > 1 ? args[1] : null, stop.Token);
                }

                var webRoot = "backend/MyApp.Api/wwwroot";
                var built = await frontend.BuildAsync(
                    FrontendConfiguration.Release, webRoot, stop.Token);
                File.WriteAllText(
                    Path.Combine(webRoot, "THIRD-PARTY-NOTICES.txt"),
                    File.ReadAllText("THIRD-PARTY-NOTICES.txt") + "\n\n\n"
                        + PlatformCrates.ReadNotices());
                foreach (var application in built.Applications)
                {
                    Console.WriteLine($"{application.ApplicationName}: "
                        + $"{application.WebRootFileCount} files in "
                        + application.WebRootApplicationDirectory
                        + (application.WasmSplit ? " (split)" : ""));
                }

                return 0;
            }
            catch (FrontendBuildException e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
        }
    }

The web root then holds home's site at its top, sales/ and admin/ beside it,
and codebrix-frontends.toml; the backend of example 2 serves all three with
no change.

5. Using the generated code (frontend, Rust)
--------------------------------------------

    // main.rs
    mod generated;

    use crate::generated::models::UserResponse;

    #[component]
    fn App() -> Element {
        // the one set-up call, generated from the backend: marker and refresh
        // path are the backend's; what happens next is the application's
        use_hook(|| generated::configure_platform(
            |user: UserResponse| { /* store the renewed user */ },
            || { /* the session is gone: show the sign-in page */ }));
        rsx! { /* the router */ }
    }

    // a page sets its title through the browser binding (CSP-safe)
    codebrix_webplatform::browser::set_title("Notes \u{00b7} MyApp");

    // a page
    use crate::generated::api::notes;
    use crate::generated::models::NoteRequest;

    async fn add(title: &str) -> Result<(), String> {
        let request = NoteRequest { title: title.to_string(), body: None };
        notes::create(&request).await.map_err(|e| e.message())?;
        let all = notes::list(None).await.map_err(|e| e.message())?;
        let _ = all.len();
        Ok(())
    }

    fn download() {
        codebrix_webplatform::browser::navigate(&notes::export_url());
    }

    // the same [WireFile] action, read by the page's own code
    async fn export_size() -> Result<usize, String> {
        let bytes: Vec<u8> = notes::export_bytes().await.map_err(|e| e.message())?;
        Ok(bytes.len())
    }

    // the hub: a component that lists notes as other tabs add them
    use codebrix_webplatform::signalr::use_subscription;
    use crate::generated::hubs::NotesHub;

    #[component]
    fn NotesFeed() -> Element {
        let hub = NotesHub::use_hub();               // connects while mounted
        let titles = use_signal(Vec::<String>::new);
        // runs inside the Dioxus runtime: writing the signal is fine
        use_subscription(|| hub.on_added(move |note| {
            let mut titles = titles;
            titles.write().push(note.title);
        }));
        let latest = NotesHub::use_added();         // or just the latest one
        rsx! { for title in titles() { p { "{title}" } } "{latest:?}" }
    }


MINIMUM VIABLE PROJECT TEMPLATE
===============================

A contract library that the backend references:

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.WebPlatform.Core.ApacheLicenseForever" Version="*" />
      </ItemGroup>
    </Project>

The build program (shown for a Linux x64 machine; reference the host-tools
package of the machine it is built on - see INSTALLATION):

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <RollForward>Major</RollForward>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever" Version="*" />
        <PackageReference Include="CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever" Version="*" />
      </ItemGroup>
    </Project>

(Replace `*` with the current package version; the backend's Backend and Core
and the build program's FrontendBuilder come from the same release.)
RollForward lets
the build program run on a newer .NET than it was built for. Program.cs is
example 3 above (or 4, for several frontend applications).

The backend (Program.cs is example 2 above):

    <Project Sdk="Microsoft.NET.Sdk.Web">
      <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.WebPlatform.Backend.ApacheLicenseForever" Version="*" />
      </ItemGroup>
    </Project>

The frontend crate keeps a small Cargo.toml of its own:

    [dependencies]
    codebrix-webplatform = { path = "platform/codebrix-webplatform" }
    dioxus = { version = "...", default-features = false,
               features = ["web", ...] }   # + "wasm-split" for CODE SPLITTING
    # ... a registered crate bundle's crates it uses, and, with
    # CrateMode.ApplicationAddsCrates, any crate of its own

    [features]
    devtools = ["dioxus/devtools"]      # for ServeAsync's hot reload

and its own Cargo.lock (committed), and git-ignores what is materialized and
built: frontend/src/generated/, frontend/platform/, frontend/.cargo/ and
frontend/target/. With several frontend applications each folder is such a
crate (frontends/home, frontends/sales, ...) and each ignores the same four
folders. What the build writes into the backend's wwwroot (the sites and the
manifest) is build output as well: ignore it in git - with a default
application that is the folder's whole content (a .gitkeep keeps the
folder).


PERFORMANCE TIPS
================

  * Map once per build. Mapping reads metadata only and takes well under a
    second for a typical backend.
  * Always write through PlatformFrontend.MaterializeAsync (or
    RustClientGenerator.WriteTo and SharedRustCrate.ExtractTo), never by
    deleting and rewriting the folders: unchanged files are left untouched,
    which is what keeps cargo from recompiling the frontend when the
    backend's contract has not changed.
  * Materialize before starting a hot-reload session (ServeAsync), not during
    it.
  * The stylesheet step runs Tailwind only when the generated stylesheet is
    missing, so it costs nothing on every later Materialize.
  * Several frontend applications share one backend build and map, and ONE
    vendor folder; only the generated code, the shared crate and the cargo
    configuration are written per application.
  * Split the application whose pages are many or heavy (CODE SPLITTING):
    the first page then downloads the main file and its own piece only.
  * BuildAsync starts from a clean target/dx folder on purpose; cargo's own
    compiled crates in target/ are kept, so a second build is quick.
  * Leave CompressOutput and ServePrecompressedFiles on (the defaults): the
    Brotli copy of a platform application's .wasm is a fraction of its size
    (see COMPRESSION), compressed once at build time and served without any
    work per request. CompressOutput = None makes a local build quicker.


COMMON PITFALLS TO AVOID
========================

  * "the reference type String is neither [WireRequired] nor [WireOptional]"
    Put [WireRequired] on the contract class (or the hub / client interface)
    and [WireOptional] on the members that may be null.

  * "the success answer is not declared"
    The action returns IActionResult. Add [ProducesResponseType(typeof(T),
    StatusCodes.Status200OK)], or [ProducesResponseType(StatusCodes
    .Status204NoContent)] when there is no body, or return ActionResult<T>.

  * "neither the action nor its controller says who may call it"
    Add [Authorize] or [AllowAnonymous]. A fallback policy in Program.cs does
    not count: the mapper cannot see it.

  * "a [WireFile] action must answer GET, because the browser fetches it by
    address." / "a [WireFile] action cannot read a body."
    An action that answers with a file is a GET with its inputs in the route
    or the query string. To send a body and get a file back, make two
    actions: one that takes the body and answers with an identifier, and a
    [WireFile] GET that answers with the file for that identifier.

  * "the generated function '..._url' (or '..._bytes') would have the same
    name as one generated for another action of this controller"
    A [WireFile] action named X takes the names x_url and x_bytes as well, so
    the same controller cannot also have an action named XUrl or XBytes.
    Rename one of the two actions.

  * Expecting <name>_bytes to stream. It reads the whole answer into memory
    before it returns; for a very large file hand <name>_url to the browser
    instead.

  * Leaving private data cacheable. The browser may store a <name>_bytes
    answer (it revalidates it every time, but keeps the copy). A [WireFile]
    action that answers one user's data should answer
    Cache-Control: no-store (Response.Headers.CacheControl = "no-store");
    a public, unchanging file is best answered with an ETag, so a returning
    visitor gets a 304.

  * A catch-all controller ("api/{**path}") or a health probe that answers
    with something other than ApiProblem: mark the controller [WireIgnore].
    (With the Backend package no catch-all controller is needed: unknown /api
    addresses are answered for you.)

  * A required string arrives as null and the frontend reports "The server
    sent an answer this page could not read" (ApiError::Decode).
    [WireRequired] is a promise the backend must keep. Either never send null
    or declare the member [WireOptional].

  * Changing the JSON naming policy, or registering JsonStringEnumConverter
    globally. The mapper cannot see either; the generated code would silently
    disagree with the backend. Put the converter attribute on the enum type.

  * Pointing AssemblyPath at an assembly without its dependencies beside it
    (an obj/ folder, a lone copied dll). Use the build or publish output.

  * Editing a generated file or the extracted crate. Both are rewritten on the
    next build. Change the backend, the type map or a template instead.

  * Putting hand-written Rust in the generated folder. WriteTo refuses the
    whole folder as soon as it finds a file without the generated marker.

  * A hub that needs sign-in served outside the session cookie's path: the
    browser never sends the cookie, and every connection is refused with 401.

  * Wrapping hub handlers in a Dioxus runtime guard of your own. The SignalR
    client already runs every handler, and the session refresh, inside the
    application's runtime; a second wrapper is harmless but pointless.

  * Capturing a hub type or a HubConnection inside one of its own handlers.
    The handler then holds a share of the connection, which never closes.
    Capture a std::rc::Weak instead.

  * Connecting before FakeHub::install in a test. The connection is then made
    to the "offline" stand-in; install the fake first (installing closes and
    forgets an existing connection to that hub).

  * Forgetting signalr::session_ended() / session_started() when the user
    signs out and in: hubs that need sign-in keep trying with a session that
    is gone.

  * Referencing BackendMapper from the backend. It is a build-time library;
    only the build program references it.

  * Expecting decimal to be exact in the frontend. The default is f64; see THE
    decimal CAVEAT.

  * "dx and wasm-bindgen were not found. Add the
    CodeBrix.WebPlatform.FrontendBuilder.<Platform>.ApacheLicenseForever
    package ..." The build program has no host-tools package for the machine
    it was built on. Add the one the message names, and rebuild the build
    program (the package copies the programs beside it).

  * "dx does not start without libgit2 ..." / "dx and the libgit2 library
    disagree on the library version ...". The build program has no copy of
    the CodeBrix.LibGit2 package's native file beside it (restore and build
    it again; do not copy the program without its runtimes/ folder), or its
    FrontendBuilder and host-tools packages are of different releases. Use
    the same version for both.

  * Starting dx by hand and getting "error while loading shared libraries:
    libgit2-...". dx needs libgit2 on its loader path; see DX NEEDS libgit2,
    STARTING dx YOURSELF.

  * Installing Tailwind - the npm package or the standalone program. The
    platform brings the standalone program in the host-tools package's
    Styling dependency and never uses one installed on the machine; nothing
    here uses Node.js.

  * Running cargo or an editor on a fresh clone before MaterializeAsync has
    run once: the frontend does not compile until src/generated,
    platform/codebrix-webplatform, platform/vendor and .cargo/config.toml
    exist (and the Tailwind stylesheet the asset!() names, which Materialize
    also writes, with the packaged Tailwind).

  * "Asset at /assets/tailwind.css doesn't exist" from cargo after
    MaterializeAsync: the result's StylesheetMessage says why it was not
    generated (the packaged Tailwind is not beside the build program, or it
    failed). Reference the machine's host-tools package (it brings the
    Styling package), build the build program, and materialize again.

  * Setting the page title with Dioxus's document::Title: under the
    platform's Content-Security-Policy the browser refuses the script it runs
    and the title never changes. Use codebrix_webplatform::browser::set_title.

  * Typing the request marker or the refresh path into the frontend (or a
    second copy into the backend). Declare them in the backend
    ([assembly: WireRequestMarker], [WireSessionRefresh]); the generated
    configure_platform carries them, and the Backend package's check reads
    the marker back (a check of your own calls
    WireRequestMarkerAttribute.GetValue).

  * "actions or hubs of the backend need sign-in, but no action is marked
    [WireSessionRefresh]" / "... does not declare the value it demands in
    X-Requested-With": a backend with sign-in declares both (see THE
    PLATFORM RULES, SIGN-IN).

  * Calling api::client::configure, signalr::configure and
    signalr::set_negotiate_headers by hand. The generated configure_platform
    makes the one set-up call that does all three from what the backend
    declares.

  * "no matching package named `x` found" (location searched: the directory
    source codebrix-webplatform-crates), or cargo refusing to go online: the
    frontend uses a crate that is not among the platform's while the build
    program uses CrateMode.PlatformCratesOnly. Set CrateMode =
    CrateMode.ApplicationAddsCrates and materialize again.

  * Keeping settings of your own in the frontend's .cargo/config.toml.
    MaterializeAsync owns that file and refuses one it did not write; put
    them in a .cargo/config.toml in a folder above the frontend.

  * Committing frontend/platform/ or frontend/.cargo/. They are build output
    (platform/vendor alone is the size of the platform's crates); they are
    rewritten from the package on every materialize.

  * Pointing FrontendDirectory at a folder that also holds other projects'
    output in src/generated or platform/codebrix-webplatform: both folders
    belong to the build; MaterializeAsync refuses to write over files it did
    not write.

  * The pipeline in the wrong order: UseRouting, MapControllers or the
    authentication BEFORE UsePlatformBackend, or no UseRouting of your own at
    all. Without your own app.UseRouting() after UsePlatformBackend, routing
    runs first and a controller wins over a web-root file at the same
    address. The order: Build; UseHsts (and whatever must see every answer);
    UsePlatformBackend; UseRouting; rate limiter; authentication;
    authorization; controllers and hubs.

  * "... declares none. Add [assembly: WireRequestMarker(...)]" at start-up:
    the Backend package's X-Requested-With check is on and the backend
    declares no marker. Declare one (even when nothing needs sign-in), set
    BackendAssembly when it is declared on another assembly, or set
    RequireRequestMarker = false.

  * Expecting the platform to send a Content-Security-Policy. It sends the
    one you give (ContentSecurityPolicy) and none by default.

  * A hub mapped outside /api/hubs: the Backend package does not check its
    Origin, and a session cookie scoped to /api never reaches it.

  * A HEAD request to a GET-only API action answers 404: MVC's [HttpGet]
    does not answer HEAD, and the unknown-API answer takes it.

  * Expecting a new frontend build to be served by a running backend. The
    backend reads the manifest once, at start-up: restart it.

  * Editing, committing or deleting codebrix-frontends.toml. It is build
    output: an edited one is refused at start-up (FrontendManifestException),
    and without it the backend serves no frontend application. Build the
    frontend again.

  * Naming an application "app", "api" or "assets", or with capitals
    ("Sales"). "app" is the name of the application that has no name; "api"
    and "assets" are the platform's; capitals are refused, not lowered. Give
    the site-root application no name at all.

  * A default application whose built site has a top-level folder or file
    named like another application (a "sales" folder at the top of the
    site-root application's output while there is an application "sales"):
    BuildAsync refuses it. Rename one of them.

  * Linking to another frontend application with the router's Link. The
    router's navigation is for the application's own routes; moving to
    another application is a full page load: use a plain anchor,
    a { href: "/sales" }.

  * Expecting ServeAsync to split the bundle: it never does. Build with
    BuildAsync to see the pieces.

  * Setting WasmSplit = true and forgetting the wasm-split feature of dioxus
    in the frontend's Cargo.toml (or the other way round): splitting needs
    both, and with nothing marked for splitting the build is one file.

  * One non_snake_case warning per route after switching splitting on: the
    router macro's generated names. Allow the lint in the module that holds
    the Routable enum (CODE SPLITTING).

  * No SuspenseBoundary around the Router (or the layout's Outlet) of a
    split application: a page whose piece is downloading has no loading
    state to show.

  * Referencing a package that brings a crate bundle and not calling its
    registration in the build program before MaterializeAsync: nothing
    registers itself, and cargo reports "no matching package".

  * "WARNING: The Cargo.lock of the frontend '...' names versions of crates
    that the platform's crates and the registered crate bundles no longer
    hold: some-crate x.y.z (the vendor folder holds x.y.w). The offline
    build fails until the file is brought in step. The file has no
    CodeBrix.WebPlatform marker line, so it is the application's own and
    was not changed. Run this command in that folder and commit the changed
    file:
        cargo update --offline -p some-crate"
    A package upgrade moved a crate, and the lock file is the developer's
    (the marker line was removed, or the file was never written by the
    platform). Run exactly that command in that frontend folder (it needs
    the folders MaterializeAsync wrote, so materialize first), or put the
    marker line back as the file's third line and materialize again.
    BuildAsync and ServeAsync refuse to start dx with the same message.
    (cargo's own words for the same thing are "failed to select a version
    for the requirement ... (locked to ...)" and "perhaps a crate was
    updated and forgotten to be re-vendored?".)

  * Moving the marker line of Cargo.lock, or putting other comments above
    cargo's header. cargo deletes a comment at the end of the file and
    rewrites its header above one placed first; only the third line is
    kept. Remove the line to manage the file yourself; never move it.

  * Expecting a registered bundle's crate version to be pinned in
    CrateMode.ApplicationAddsCrates. Nothing pins it there: write the exact
    version in the frontend's Cargo.toml (`name = "=x.y.z"`) or keep it in
    the committed Cargo.lock.

  * Expecting `dx serve` (ServeAsync) to serve compressed files. It never
    does: only BuildAsync into a web root writes the .br / .gz copies, and
    only the platform backend serves them. Look at sizes and
    Content-Encoding on a build served by the backend.

  * Committing the web root's .br / .gz files. They are build output, like
    the rest of what the build writes into the web root; the repository's
    .gitignore should ignore the web root's content (.gitkeep excepted).

  * Editing a file in the web root by hand after a build. Its .br / .gz
    copies still hold the old content, and they are what browsers get.
    Change the frontend and build again (or delete the copies).

  * Linking to x.wasm.br, or expecting it to download. A copy is never
    served under its own name (404); ask for x.wasm and the backend picks
    the copy.

  * "My file download is not compressed" with CompressResponses =
    FileAnswers. FileAnswers compresses the answers of [WireFile] actions,
    and nothing else: an action without the attribute that answers with
    File(...), PhysicalFile(...) or text it writes itself is sent as it is.
    Mark the action [WireFile] (it must answer GET and read no body; the
    frontend gets its <name>_url and <name>_bytes functions with it), or let
    a proxy compress the download. Read THE RISK first either way. (A type
    that is compressed already - PNG, ZIP, PDF ... - is never compressed,
    marked or not.)

  * Expecting CompressResponses = FileAnswers to compress JSON. It does not,
    on purpose (THE RISK in COMPRESSION), except in a [WireFile] answer;
    FileAnswersAndJson compresses every action's JSON answers. Do not
    add app.UseResponseCompression() of your own on top: it compresses by
    its own rules, without the platform's exclusions (the hubs, the web
    root's files).

  * "The registered crate bundles cannot be combined with the platform's
    crates ...": two packages (or a package and the platform) bring
    different versions of one crate, or different content for one version.
    Use releases of those packages built against the same platform release,
    or leave one out.


WHAT THIS PACKAGE DOES NOT DO
=============================

  * It does not run, host or configure the backend. The Backend package
    (the only one with ASP.NET Core code) adds the platform's plumbing to the
    backend's pipeline; HSTS, rate limiting, authentication, authorization,
    CORS, the controllers and the hub mapping are the application's.
  * The Backend package does not build the frontend, and serves only the
    frontend applications the manifest names (or the set given to
    UsePlatformBackend), from the web root.
  * A platform application has ONE backend: frontend applications that need
    different backends are separate platform applications.
  * Core and BackendMapper do not build the frontend; FrontendBuilder does,
    by running dx. None of them installs anything: Rust and its WebAssembly
    target are the developer's to install (Tailwind comes in a package), and
    CheckPrerequisitesAsync says what is missing.
  * FrontendBuilder does not deploy: no containers, no database, no hosting.
    Publishing the backend with the built site in its wwwroot is the
    application's own step.
  * FrontendBuilder does not include dx, wasm-bindgen or Tailwind; they come
    from the host-tools package for the build machine (Tailwind through its
    Styling package).
  * It does not check the licences of the crates an application adds
    (CrateMode.ApplicationAddsCrates), or of the crates a registered crate
    bundle brings, or record them in notices: an application's own code and
    crates are the application's, and a bundle's are its package's.
  * Its SignalR client speaks the JSON hub protocol over WebSockets only: no
    long polling, server-sent events or MessagePack, and it returns no result
    when the server invokes the client and waits for one.
  * It does not map minimal-API endpoints (app.MapGet), Razor Pages, MVC
    views, gRPC or anything but attribute-routed controllers and typed hubs.
  * It does not read run-time configuration: conventional routes, global
    filters, serializer options and policies are invisible to it.
  * It does not generate code in any language but Rust.
  * It does not use, need or talk to Node.js.


WORKING EXAMPLES ON GITHUB
==========================

Core
    attributes          https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Core.Tests/Mapping
    ApiProblem          https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Core.Tests/Problems
    frontend applications, their names
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Core.Tests/Applications
    crate bundles       https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Core.Tests/Crates

Backend
    a backend that makes the two calls (controllers, a hub, sign-in)
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Backend.Tests.SampleBackend
    serving the frontend applications
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Backend.Tests/Serving
    the checks, headers, error bodies and the pipeline order
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Backend.Tests/Plumbing
    the manifest        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Backend.Tests/Applications
    ApiProblems         https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.Backend.Tests/Problems

BackendMapper
    a well-formed backend to read (contract classes, controllers, hubs)
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend
    a backend with every kind of problem
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.BackendMapper.Tests.BrokenBackend
    mapping, problems   https://github.com/ellisnet/CodeBrix.WebPlatform/blob/main/tests/CodeBrix.WebPlatform.BackendMapper.Tests/BackendMapperTests.cs
    type mapping        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.BackendMapper.Tests/TypeMapping
    generated Rust, templates, WriteTo
                        https://github.com/ellisnet/CodeBrix.WebPlatform/blob/main/tests/CodeBrix.WebPlatform.BackendMapper.Tests/Rust/RustClientGeneratorTests.cs

FrontendBuilder
    check, materialize, build, serve, the dx environment
                        https://github.com/ellisnet/CodeBrix.WebPlatform/blob/main/tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/PlatformFrontendTests.cs
    several frontend applications, splitting, ServeAsync(name)
                        https://github.com/ellisnet/CodeBrix.WebPlatform/blob/main/tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/PlatformFrontendApplicationsTests.cs
    the application options, the web root, the manifest
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/Applications
    crate bundles and the strict rule
                        https://github.com/ellisnet/CodeBrix.WebPlatform/blob/main/tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/Crates/CrateBundleMergeTests.cs
    finding dx and wasm-bindgen
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/Tools
    SharedRustCrate     https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/SharedCrate
    the platform's crates, the crate modes, the cargo configuration
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests/CodeBrix.WebPlatform.FrontendBuilder.Tests/Crates

The shared Rust crate
    the SignalR client against the fake hub
                        https://github.com/ellisnet/CodeBrix.WebPlatform/blob/main/src/CodeBrix.WebPlatform.FrontendBuilder/Rust/codebrix-webplatform/src/signalr/tests.rs

A whole application built this way (backend, hubs, build program, frontend):
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/samples/WorkSite

One backend with three frontend applications (one at the site root, two
named; one of them split into pieces):
                        https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/samples/ApplicationSplitter


QUICK REFERENCE CARD
====================

  BACKEND (references Backend, which brings Core)
    builder.Services.AddPlatformBackend(o => o.ContentSecurityPolicy = "...");
    app.UseHsts();                    // (outside Development) BEFORE
    app.UsePlatformBackend();         // serves wwwroot's applications (its
                                      // manifest), error bodies, checks,
                                      // security headers
    app.UseRouting(); ...auth...; app.MapControllers(); app.MapHub<..>(..);
    return new ApiProblemResult(ApiProblems.NotFound());   // in an action
    await ApiProblemWriter.WriteAsync(context, problem);   // in middleware
    PlatformBackendOptions: every piece on by default; switch one off
    o.ServePrecompressedFiles (true)  a built file's .br / .gz copy to a
                                      browser that accepts it
    o.CompressResponses = ResponseCompression.FileAnswers   // default None;
                                      // [WireFile] answers only; read
                                      // COMPRESSION (THE RISK) first
    [WireRequired] / [WireOptional]   on contract classes, client interfaces,
                                      members; nearest wins
    [WireRequired] on a `T?` member   the frontend always sends it
    [WireType("T", Import=, With=)]   one value's Rust type
    [WireFile]                        GET action that answers with a file:
                                      <name>_url (address) + <name>_bytes
                                      (the bytes, Vec<u8>)
    [WireIgnore]                      leave out of the generated code
    [WireHub("/api/hubs/x")]          a hub's path; map it with
                                      WireHubAttribute.GetPath(typeof(XHub))
    ApiProblem / ApiProblemCodes      every error body; 401 "not signed in"
                                      is empty
    each action: [HttpX] + route, [Authorize]|[AllowAnonymous],
                 [ProducesResponseType(typeof(T), 200)] | (204) | ActionResult<T>
    each hub:    Hub<TClient>, [WireHub], [Authorize]|[AllowAnonymous]
    [assembly: WireRequestMarker("MyApp")]  the X-Requested-With value
                  (the Backend package's check reads it; needed with the
                  check on, sign-in or not)
    with sign-in: ONE [WireSessionRefresh] action: POST, [AllowAnonymous],
                  takes nothing
    hubs below /api/hubs (Origin-checked; the /api session cookie reaches
                  them)

  BUILD PROGRAM (references FrontendBuilder + the machine's host-tools package)
    var frontend = new PlatformFrontend(new PlatformFrontendOptions {
        FrontendDirectory = "frontend", BackendProjectPath = "backend/X.csproj" });
    (await frontend.CheckPrerequisitesAsync()).IsReady / .Items / .Problems
    await frontend.MaterializeAsync();   // src/generated, platform/ (crate +
                                         // crates), .cargo/config.toml, and a
                                         // missing Tailwind stylesheet
    result.Stylesheet / .StylesheetMessage   // Generated, AlreadyPresent, ...
    options.GenerateMissingStylesheet = false;   // TailwindInputFile/OutputFile
    options.CrateMode = CrateMode.ApplicationAddsCrates;   // own crates
    await frontend.BuildAsync(FrontendConfiguration.Release, "wwwroot");
                                         // every application, + the manifest
    await frontend.ServeAsync();         // dx serve, hot reload (never split)
    several applications: options.Applications.Add(new
        FrontendApplicationOptions { Name = "sales", FrontendDirectory =
        "frontends/sales", WasmSplit = true });   // no Name: the site root
    names: a-z 0-9 -, letter first, no "app"/"api"/"assets", <= 40
    await frontend.ServeAsync("sales");  // one application by name
    splitting: dioxus feature "wasm-split" + WasmSplit = true;
               SuspenseBoundary around the Router; always wasm-opt
    crate bundles: the package's Register call BEFORE MaterializeAsync;
               result.CrateBundles; strict rule: one version per crate
    PlatformCrates.ReadNotices()          // serve after the app's notices
    catch (FrontendBuildException e)  ->  e.Message says what to do
    options.Types.Map<decimal>(new RustType("Money") { Import = "..." });
    options.ClientOptions = new RustClientOptions { MaxSeparateQueryArguments = 3 };
    Shrink: null = Release shrunk, Debug not; true = Debug too; false =
            Release not shrunk (dx skips wasm-opt)
    CompressOutput: BrotliAndGzip (default) = .br + .gz beside the built
            files in the web root (BuildAsync only); None = no copies;
            result.Compression says what was written and how long it took
    dx needs libgit2 to start: the library supplies it (Libgit2Directory)
    lower level: BackendMapper.Map, RustClientGenerator.Generate/WriteTo,
                 SharedRustCrate.ExtractTo

  FRONTEND (Rust)
    Cargo.toml: codebrix-webplatform = { path = "platform/codebrix-webplatform" }
    Cargo.lock: the application's own; commit it. The marker line (3rd
                line) lets MaterializeAsync keep moved crates in step; no
                marker: never touched, the stale entries are reported with
                the `cargo update --offline -p ...` command
    after MaterializeAsync: cargo check / test / build, offline by default
    mod generated;
    use_hook(|| generated::configure_platform(on_refreshed, on_ended));
                                    // the ONE set-up call, first render
                                    // (codebrix_webplatform::configure inside)
    browser::set_title(..)          // never document::Title (CSP)
    browser::local_hour() / local_time(instant) / today() / navigate(path)
    signalr::session_ended() / session_started()   on sign-out / sign-in
    generated::api::<controller>::<action>(..).await -> ApiResult<T>
    generated::api::<controller>::<file>_bytes(..).await -> ApiResult<Vec<u8>>
                                    // a [WireFile] action; <file>_url(..) is
                                    // its address
    generated::hubs::<Hub>::use_hub() / connect() / on_<message>() /
        use_<message>() / <method>(..).await / <stream>(..).next().await
    signalr::use_subscription(|| hub.on_x(..))   handlers may write signals
    tests: signalr::testing::{FakeHub, settle, advance, block_on}
    ApiError::message() and HubError::message() are always fit to show

================================================================================
END OF AGENT-README
================================================================================
