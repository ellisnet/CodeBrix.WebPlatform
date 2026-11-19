================================================================================
AGENT-README: CodeBrix.WebPlatform
A Guide for AI Coding Agents — CONSUMING the
CodeBrix.WebPlatform.Core.ApacheLicenseForever,
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
    * a frontend written in Rust, compiled to WebAssembly, that talks to that
      backend.

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
          - the standard error body (ApiProblem) and its codes.

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
        frontend. It checks the machine and says what is missing; it
        MATERIALIZES the frontend (builds and maps the backend with
        BackendMapper, writes the generated client code, the shared Rust
        crate every platform frontend compiles in, the platform's Rust crates,
        the cargo configuration and, when it is missing, the Tailwind
        stylesheet, so that plain cargo works offline); and
        it BUILDS the servable site, or SERVES it with hot reload, by running
        the Dioxus build tool (dx), with release builds shrunk by wasm-opt. It
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
no session handling: those are the application's.


INSTALLATION
============

    dotnet add package CodeBrix.WebPlatform.Core.ApacheLicenseForever
    dotnet add package CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever
        (brings CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever)

IMPORTANT: the package ids carry the ".ApacheLicenseForever" suffix. There are
no packages named plain CodeBrix.WebPlatform.Core,
CodeBrix.WebPlatform.BackendMapper or CodeBrix.WebPlatform.FrontendBuilder.
The assembly names and root namespaces are CodeBrix.WebPlatform.Core,
CodeBrix.WebPlatform.BackendMapper and CodeBrix.WebPlatform.FrontendBuilder.

Which project references what:

    the backend (and every project that holds contract   Core
      classes or hub client interfaces)
    the program that builds the frontend                 FrontendBuilder (it
                                                         brings BackendMapper
                                                         and Core), and the
                                                         host-tools package
                                                         for the machine
    the frontend (Rust)                                  none; it gets Rust
                                                         source and the
                                                         platform's crates,
                                                         below

NuGet dependencies:
    Core            none. It uses only the .NET base class library.
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

THE HOST-TOOLS PACKAGE. The dx and wasm-bindgen programs a build runs are NOT
in FrontendBuilder: they are native programs, one package per build platform.
The build program references the one for the machine it is built on:

    Linux x64             CodeBrix.WebPlatform.FrontendBuilder.LinuxX64
    Linux ARM64           CodeBrix.WebPlatform.FrontendBuilder.LinuxArm64
    Windows x64           CodeBrix.WebPlatform.FrontendBuilder.WindowsX64
    macOS, Apple Silicon  CodeBrix.WebPlatform.FrontendBuilder.MacOSArm64
    macOS, Intel          CodeBrix.WebPlatform.FrontendBuilder.MacOSX64
                          (each id ends in .ApacheLicenseForever)

Each copies its programs beside the build program, as
frontend-tools/<rid>/dx[.exe] and frontend-tools/<rid>/wasm-bindgen[.exe]
(rid = linux-x64, linux-arm64, win-x64, osx-arm64, osx-x64). Those five are
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
are published together). CheckPrerequisitesAsync says which package the
programs came from, or which one to add.

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
      No C compiler is needed, and no network access for the platform's
      crates;
    - the standalone Tailwind program `tailwindcss` on PATH,
      FrontendRequirements.MinimumTailwindVersion or later (the standalone
      program, not the npm package: nothing here uses Node.js).
Everything else arrives with the packages.

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
        // FrontendBuildException

    using CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;
        // StylesheetOutcome

    using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
        // PrerequisiteReport, PrerequisiteItem, PrerequisiteKind,
        // PrerequisiteStatus, WasmOptSource

    using CodeBrix.WebPlatform.FrontendBuilder.Tools;
        // HostTools, HostToolsSource

    using CodeBrix.WebPlatform.FrontendBuilder.Crates;
        // PlatformCrates, CrateMode

    using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;
        // SharedRustCrate, SharedRustFile

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
      and its own check reads it back from there (see below); the generated
      client code sends it.

  SIGN-IN
    * A backend with any action or hub that needs sign-in declares its
      request marker ([assembly: WireRequestMarker]) and marks the one action
      that renews an expired session ([WireSessionRefresh]): a POST anyone may
      call ([AllowAnonymous]) that takes nothing and answers 401 (empty body)
      when the session cannot be renewed. A backend where nothing needs
      sign-in needs neither.

  HUBS
    * Every hub derives from Hub<TClient> with an interface that names each
      message it sends, and carries [WireHub("/path")].

The backend produces those answers with its own ASP.NET Core code (a problem
writer, an invalid-model-state response factory, status-code pages, a
middleware for the header). These packages do not contain that code; Core has
no web framework dependency.


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
Target: method (a REST action). The action's answer is a file for the browser
to download or show, not JSON for the frontend to read. The action must answer
GET and needs no declared response type. The generated code gets a function
that BUILDS THE ADDRESS (returns String) instead of one that fetches it; the
frontend hands the address to the browser.

[WireIgnore]
------------
Targets: class, method, property. Leaves a controller, hub, action, hub method
or property out of the generated code entirely. Use it for what the frontend
never calls: a health probe, a catch-all "no such route" controller.

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
negotiate request), and the backend's own check reads it back, so the two
cannot drift apart:

    [assembly: WireRequestMarker("MyApp")]    // e.g. in WireDeclarations.cs

    public const string WireRequestMarkerAttribute.HeaderName;
        // "X-Requested-With"
    public static string WireRequestMarkerAttribute.GetValue(Assembly assembly)
        // throws ArgumentNullException (null), InvalidOperationException
        // (no attribute, or a value that is not valid)
    public static bool WireRequestMarkerAttribute.IsValidValue(string value)
        // visible ASCII, single spaces inside, at most 100 characters

    // in the backend's middleware:
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
does not apply the same-origin policy to WebSockets, the backend should check
the Origin header of every hub request (public hubs too).


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
    }

Naming and shape rules of the generated code:
  * module   = the controller's name without "Controller", in snake_case
  * function = the action's name in snake_case, without an "Async" suffix; a
               [WireFile] action gets "_url" appended and returns String
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
                   return_type, method, auth, query_statement, path, body)
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

    public sealed class PlatformFrontendOptions
    {
        public string FrontendDirectory { get; set; }     // REQUIRED: the
            // frontend crate's folder (Cargo.toml, Dioxus.toml)
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
            // platform's crates are written; default <frontend>/platform/vendor
        public string DxPath { get; set; }                // see HOST TOOLS
        public string WasmBindgenPath { get; set; }
        public string DxHomeDirectory { get; set; }       // dx's DX_HOME;
            // default <frontend>/target/dx-home
        public string MinimumTailwindVersion { get; set; }
            // default FrontendRequirements.MinimumTailwindVersion
        public bool GenerateMissingStylesheet { get; set; }  // default true:
            // MaterializeAsync writes a missing Tailwind stylesheet (STEP 2)
        public string TailwindInputFile { get; set; }     // default
            // <frontend>/tailwind.css (relative: from the frontend folder)
        public string TailwindOutputFile { get; set; }    // default
            // <frontend>/assets/tailwind.css (where dx writes it)
        public bool? Shrink { get; set; }                 // see SHRINKING
        public IList<string> ServeFeatures { get; }       // cargo features
            // for ServeAsync, e.g. the one that enables dioxus/devtools
        public TextWriter Output { get; set; }            // progress lines;
            // null = none
        public string GeneratedClientDirectory { get; }
            // <frontend>/src/generated
        public string SharedCrateDirectory { get; }
            // <frontend>/platform/codebrix-webplatform
        public string ResolvedVendorDirectory { get; }
            // VendorDirectory, or <frontend>/platform/vendor
        public string CargoConfigurationFile { get; }
            // <frontend>/.cargo/config.toml
        public string ResolvedTailwindInputFile { get; }  // full paths of the
        public string ResolvedTailwindOutputFile { get; } //   two stylesheets
    }

    public sealed class PlatformFrontend
    {
        public PlatformFrontend(PlatformFrontendOptions options);
            // ArgumentNullException; ArgumentException (blank
            // FrontendDirectory)
        public PlatformFrontendOptions Options { get; }
        public string FrontendDirectory { get; }          // full path
        public string DxHomeDirectory { get; }

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

        public HostTools FindHostTools();
        public string GetOutputDirectory(FrontendConfiguration configuration);
            // <frontend>/target/dx/<package name>/<release|debug>/web/public
        public static IReadOnlyList<string> GetDxBuildArguments(
            FrontendConfiguration configuration);
            // Release: build --platform web --release --debug-symbols false
            // Debug:   build --platform web
    }

    public enum FrontendConfiguration { Release, Debug }

    public static class FrontendRequirements
    {
        public const string MinimumRustVersion;     // the oldest Rust accepted
        public const string WebAssemblyTarget;      // "wasm32-unknown-unknown"
        public const string MinimumTailwindVersion; // the default minimum
        public const string TailwindProgram;        // "tailwindcss"
        public static string ParseTailwindVersion(string helpOutput);
    }

Every failure of a step is a FrontendBuildException whose Message says what
failed and what to do (with the end of a program's output where that helps).
A contract problem in the backend keeps BackendMapper's BackendMapException
(with every problem) as the InnerException.

STEP 1 - CheckPrerequisitesAsync
    Probes the machine and NEVER throws for something missing. The report has
    one PrerequisiteItem per PrerequisiteKind, in this order:
        Rust       rustc at MinimumRustVersion or later with the WebAssembly
                   target (detected with CodeBrix.RustTools; a missing piece
                   comes with the commands that install it). No C compiler.
        Tailwind   `tailwindcss --help` on PATH reports MinimumTailwindVersion
                   or later.
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
        WasmOptSource WasmOpt; HostTools HostTools (null when not found);
        string DxVersion;   // dx --version, started with libgit2; null when
                            // dx was not found or did not start
        PrerequisiteItem this[PrerequisiteKind kind];
    }
    public sealed class PrerequisiteItem
    {
        PrerequisiteKind Kind; PrerequisiteStatus Status (Ok, Note, Missing);
        string Message;   // fit to show; for Missing it says how to fix it
        string ToString();   // "MISSING  The standalone Tailwind program ..."
    }

STEP 2 - MaterializeAsync
    Writes everything the frontend needs and does not write itself:
      1. builds BackendProjectPath with `dotnet build -c Release` (when set);
      2. maps the backend assembly with BackendMapper (Types);
      3. writes the generated client code into GeneratedClientDirectory
         (RustClientGenerator.WriteTo, with ClientOptions);
      4. writes the shared Rust crate into SharedCrateDirectory
         (SharedRustCrate.ExtractTo);
      5. CrateMode.PlatformCratesOnly (the default): writes the platform's
         crates into ResolvedVendorDirectory - unpacked from the package's
         archive only when that archive changed (a stamp file in the folder
         records which archive it holds). CrateMode.ApplicationAddsCrates:
         removes that folder if this library wrote it;
      6. writes the cargo configuration, CargoConfigurationFile
         (<frontend>/.cargo/config.toml), for the crate mode;
      7. when the frontend has NO Cargo.lock yet, writes the platform's as its
         starting point (the platform's versions). From then on Cargo.lock is
         the application's own file: commit it;
      8. THE TAILWIND STYLESHEET: when the frontend uses Tailwind (its input
         stylesheet, ResolvedTailwindInputFile, exists) and the generated one
         (ResolvedTailwindOutputFile, the file the application's
         asset!("/assets/tailwind.css") names and dx normally writes during a
         build) is MISSING, it starts the installed Tailwind program once -
         `tailwindcss -i <input> -o <output>` in the frontend folder. An
         existing stylesheet is never touched (dx rewrites it on every
         build). Nothing is installed: when Tailwind is missing or older than
         MinimumTailwindVersion, or fails, Materialize does NOT fail - the
         result says so (Stylesheet, StylesheetMessage), and the message is
         also written to Output. Switch the step off with
         GenerateMissingStylesheet = false.
    Only changed files are rewritten, so cargo does not rebuild for nothing.
    AFTERWARDS PLAIN `cargo check`, `cargo test` AND `cargo build` WORK in the
    frontend folder - offline, in the default crate mode - and so does an
    editor's Rust support, on a fresh clone too: clone, Materialize, cargo
    check. (Unless the result reports StylesheetMissing: then they fail on
    the asset!() until a build has run or Tailwind is installed.)

    public sealed class MaterializeResult
    {
        BackendMap Map;                          // what the mapper read
        IReadOnlyList<string> GeneratedFiles;    // written or removed
        IReadOnlyList<string> SharedCrateFiles;  // written or removed
        bool VendorChanged;                      // crates written / removed
        bool CargoConfigurationWritten;
        bool LockFileWritten;                    // a starting Cargo.lock
        StylesheetOutcome Stylesheet;            // see below
        string StylesheetMessage;                // a sentence fit to show
        bool StylesheetMissing;                  // missing and not generated
        bool WasUpToDate;                        // nothing written
    }

    public enum StylesheetOutcome   // CodeBrix.WebPlatform.FrontendBuilder.Stylesheet
    {
        Generated,                   // it was missing; Tailwind wrote it
        AlreadyPresent,              // left alone
        NotApplicable,               // no input stylesheet: no Tailwind here
        SwitchedOff,                 // GenerateMissingStylesheet = false
        SkippedTailwindNotAvailable, // missing: Tailwind not installed or too old
        SkippedTailwindFailed,       // missing: Tailwind ran and failed
    }

    Fails when: the frontend folder has no Cargo.toml; neither backend option
    is set; the project does not exist or does not build; the contract is not
    fully declared (every problem listed); src/generated holds a file without
    the generated marker; the shared crate's folder holds something else; the
    vendor folder holds files this library did not write (no stamp); the
    frontend's .cargo/config.toml was not written by this library; the
    platform's crates are not beside the build program (the package was not
    restored, or the program was copied without its frontend-crates folder).

STEP 3 - BuildAsync / ServeAsync
    BuildAsync runs dx in the frontend folder, from a clean target/dx folder:
        dx <GetDxBuildArguments(configuration)>
    with this environment:
        PATH     the packaged wasm-opt's folder FIRST (when the WasmShrink
                 package has one for this machine), then wasm-bindgen's
                 folder, then the existing PATH (the Tailwind program is found
                 there);
        DX_HOME  DxHomeDirectory: a folder the build owns, because dx looks in
                 its own data folder BEFORE PATH, and a wasm-opt some other dx
                 once downloaded there would be used instead;
        the loader's search variable - LD_LIBRARY_PATH (Linux),
                 DYLD_LIBRARY_PATH (macOS), PATH (Windows) - with
                 Libgit2Directory FIRST (see DX NEEDS libgit2);
        DX_SKIP_WASM_OPT  "1" for a Release build with Shrink = false, "0"
                 otherwise (so a value in your own environment never decides
                 it).
    Only dx's process gets these; the build program's own environment is not
    changed. ServeAsync starts dx with the same environment.
    It fails when dx fails or leaves no index.html. With a webRootDirectory it
    then replaces that folder's content with the built site (a .gitkeep file
    is kept) - typically the backend's wwwroot.

    public sealed class FrontendBuildResult
    {
        FrontendConfiguration Configuration; string OutputDirectory;
        bool Shrunk; WasmOptSource WasmOpt; HostTools Tools;
        string WebRootDirectory; int WebRootFileCount;
    }

    ServeAsync runs `dx serve --platform web [--features <ServeFeatures>]`
    with the same environment, passes dx's output to Options.Output, and
    returns dx's exit code when it ends (cancel the token to stop it). The
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
    asset, and devtools for hot reload), at the platform's versions, exactly
    as crates.io published them. An application chooses, with
    PlatformFrontendOptions.CrateMode, where its frontend's crates come from:

    CrateMode.PlatformCratesOnly (THE DEFAULT)
        The crates are written into the frontend (platform/vendor) and the
        cargo configuration replaces crates.io with that folder and switches
        cargo to offline mode:

            [source.crates-io]
            replace-with = "codebrix-webplatform-crates"
            [source.codebrix-webplatform-crates]
            directory = "platform/vendor"
            [net]
            offline = true

        Nothing is downloaded, ever. A crate (or a version) that is not among
        the platform's is an error ("no matching package"); that is the
        moment to switch mode.

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
        its own in a folder above the frontend.

    Either way the frontend keeps a SMALL Cargo.toml of its own: the shared
    crate as a path dependency, dioxus with the features it uses, the
    platform crates its own code names directly, and - in the second mode -
    any crate of its own. Its Cargo.lock is its own (commit it). Everything
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
    public enum CrateMode { PlatformCratesOnly, ApplicationAddsCrates }

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

    (Write it AFTER BuildAsync: BuildAsync replaces the web root's content.)
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
         process: LD_LIBRARY_PATH (Linux), DYLD_LIBRARY_PATH (macOS - not yet
         verified on a Mac), PATH (Windows - not yet verified on Windows).
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
        - any non-success answer becomes an ApiError;
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
    // Program.cs:
    //     app.MapHub<NotesHub>(WireHubAttribute.GetPath(typeof(NotesHub)));
    //     the X-Requested-With check compares with
    //     WireRequestMarkerAttribute.GetValue(typeof(Program).Assembly)

2. The build program (builds the frontend)
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

3. Using the generated code (frontend, Rust)
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

(Replace `*` with the current package version; the backend's Core and the
build program's FrontendBuilder come from the same release.) RollForward lets
the build program run on a newer .NET than it was built for. Program.cs is
example 2 above.

The frontend crate keeps a small Cargo.toml of its own:

    [dependencies]
    codebrix-webplatform = { path = "platform/codebrix-webplatform" }
    dioxus = { version = "...", default-features = false,
               features = ["web", ...] }
    # ... and, with CrateMode.ApplicationAddsCrates, any crate of its own

    [features]
    devtools = ["dioxus/devtools"]      # for ServeAsync's hot reload

and its own Cargo.lock (committed), and git-ignores what is materialized and
built: frontend/src/generated/, frontend/platform/, frontend/.cargo/ and
frontend/target/.


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
  * BuildAsync starts from a clean target/dx folder on purpose; cargo's own
    compiled crates in target/ are kept, so a second build is quick.


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

  * A catch-all controller ("api/{**path}") or a health probe that answers
    with something other than ApiProblem: mark the controller [WireIgnore].

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

  * Installing the Tailwind npm package. The platform needs the STANDALONE
    `tailwindcss` program on PATH; nothing here uses Node.js.

  * Running cargo or an editor on a fresh clone before MaterializeAsync has
    run once: the frontend does not compile until src/generated,
    platform/codebrix-webplatform, platform/vendor and .cargo/config.toml
    exist (and the Tailwind stylesheet the asset!() names, which Materialize
    also writes when Tailwind is installed).

  * "Asset at /assets/tailwind.css doesn't exist" from cargo after
    MaterializeAsync: the result's StylesheetMessage says why it was not
    generated (Tailwind not installed, too old, or failed). Install Tailwind
    and materialize again, or run a build once.

  * Setting the page title with Dioxus's document::Title: under the
    platform's Content-Security-Policy the browser refuses the script it runs
    and the title never changes. Use codebrix_webplatform::browser::set_title.

  * Typing the request marker or the refresh path into the frontend (or a
    second copy into the backend's middleware). Declare them in the backend
    ([assembly: WireRequestMarker], [WireSessionRefresh]); the generated
    configure_platform carries them, and the middleware reads
    WireRequestMarkerAttribute.GetValue.

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


WHAT THIS PACKAGE DOES NOT DO
=============================

  * It does not run, host or configure the backend. There is no ASP.NET Core
    code in any of the packages: the problem writer, the X-Requested-With
    check, authentication and the hub mapping are the application's.
  * Core and BackendMapper do not build the frontend; FrontendBuilder does,
    by running dx. None of them installs anything: Rust, its WebAssembly
    target and the Tailwind program are the developer's to install, and
    CheckPrerequisitesAsync says what is missing.
  * FrontendBuilder does not deploy: no containers, no database, no hosting.
    Publishing the backend with the built site in its wwwroot is the
    application's own step.
  * FrontendBuilder does not include dx or wasm-bindgen; they come from the
    host-tools package for the build machine.
  * It does not check the licences of the crates an application adds
    (CrateMode.ApplicationAddsCrates) or record them in notices: an
    application's own code and crates are the application's.
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


QUICK REFERENCE CARD
====================

  BACKEND (references Core)
    [WireRequired] / [WireOptional]   on contract classes, client interfaces,
                                      members; nearest wins
    [WireRequired] on a `T?` member   the frontend always sends it
    [WireType("T", Import=, With=)]   one value's Rust type
    [WireFile]                        GET action that answers with a file
    [WireIgnore]                      leave out of the generated code
    [WireHub("/api/hubs/x")]          a hub's path; map it with
                                      WireHubAttribute.GetPath(typeof(XHub))
    ApiProblem / ApiProblemCodes      every error body; 401 "not signed in"
                                      is empty
    each action: [HttpX] + route, [Authorize]|[AllowAnonymous],
                 [ProducesResponseType(typeof(T), 200)] | (204) | ActionResult<T>
    each hub:    Hub<TClient>, [WireHub], [Authorize]|[AllowAnonymous]
    with sign-in: [assembly: WireRequestMarker("MyApp")]  (the X-Requested-With
                  value; the check reads WireRequestMarkerAttribute.GetValue)
                  and ONE [WireSessionRefresh] action: POST, [AllowAnonymous],
                  takes nothing

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
    await frontend.ServeAsync();         // dx serve, hot reload
    PlatformCrates.ReadNotices()          // serve after the app's notices
    catch (FrontendBuildException e)  ->  e.Message says what to do
    options.Types.Map<decimal>(new RustType("Money") { Import = "..." });
    options.ClientOptions = new RustClientOptions { MaxSeparateQueryArguments = 3 };
    Shrink: null = Release shrunk, Debug not; true = Debug too; false =
            Release not shrunk (dx skips wasm-opt)
    dx needs libgit2 to start: the library supplies it (Libgit2Directory)
    lower level: BackendMapper.Map, RustClientGenerator.Generate/WriteTo,
                 SharedRustCrate.ExtractTo

  FRONTEND (Rust)
    Cargo.toml: codebrix-webplatform = { path = "platform/codebrix-webplatform" }
    Cargo.lock: the application's own; commit it
    after MaterializeAsync: cargo check / test / build, offline by default
    mod generated;
    use_hook(|| generated::configure_platform(on_refreshed, on_ended));
                                    // the ONE set-up call, first render
                                    // (codebrix_webplatform::configure inside)
    browser::set_title(..)          // never document::Title (CSP)
    browser::local_hour() / local_time(instant) / today() / navigate(path)
    signalr::session_ended() / session_started()   on sign-out / sign-in
    generated::api::<controller>::<action>(..).await -> ApiResult<T>
    generated::hubs::<Hub>::use_hub() / connect() / on_<message>() /
        use_<message>() / <method>(..).await / <stream>(..).next().await
    signalr::use_subscription(|| hub.on_x(..))   handlers may write signals
    tests: signalr::testing::{FakeHub, settle, advance, block_on}
    ApiError::message() and HubError::message() are always fit to show

================================================================================
END OF AGENT-README
================================================================================
