# CodeBrix.WebPlatform

A platform for building web applications with a .NET backend (REST controllers and SignalR hubs) and one or more frontend applications written in Rust and compiled to WebAssembly - with no Node.js anywhere and almost no JavaScript. The frontend's client code for the backend is generated from the backend itself on every build, so there is no contract document to write and no API client to maintain by hand. The backend gets the platform's plumbing - serving the frontend applications, the standard error answers, the cross-site checks and the security headers - from one services call and one pipeline call. A build program calls one library to check the machine, write the generated code and the crates into the frontends and build or serve the site, with prebuilt build tools from a package - nothing to compile or download for them. CodeBrix.WebPlatform is provided as .NET 10 libraries and the associated `CodeBrix.WebPlatform.Core.ApacheLicenseForever`, `CodeBrix.WebPlatform.Backend.ApacheLicenseForever`, `CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever` and `CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever` NuGet packages, and the host-tools packages `CodeBrix.WebPlatform.FrontendBuilder.<Platform>.ApacheLicenseForever`.

CodeBrix.WebPlatform supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.WebPlatform.Backend.ApacheLicenseForever
dotnet add package CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever
dotnet add package CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever
```

(The first goes into the backend and brings `CodeBrix.WebPlatform.Core`; the other two go into the build program. The last one is the host-tools package of the machine the frontend is built on: `LinuxX64`, `LinuxArm64`, `WindowsX64`, `MacOSArm64` or `MacOSX64`.)

Note that the NuGet package IDs and the namespaces are different - there are no packages named plain `CodeBrix.WebPlatform.Core`, `CodeBrix.WebPlatform.Backend`, `CodeBrix.WebPlatform.BackendMapper` or `CodeBrix.WebPlatform.FrontendBuilder`:

* NuGet package ID: `CodeBrix.WebPlatform.Core.ApacheLicenseForever` - assembly and primary namespace `CodeBrix.WebPlatform.Core`
* NuGet package ID: `CodeBrix.WebPlatform.Backend.ApacheLicenseForever` - assembly and primary namespace `CodeBrix.WebPlatform.Backend`
* NuGet package ID: `CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever` - assembly and primary namespace `CodeBrix.WebPlatform.BackendMapper`
* NuGet package ID: `CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever` - assembly and primary namespace `CodeBrix.WebPlatform.FrontendBuilder`

XML documentation (IntelliSense) ships alongside each assembly.

Which project references which package:

* The backend references `CodeBrix.WebPlatform.Backend`, which depends on `CodeBrix.WebPlatform.Core`, `CodeBrix.TomlParse` and the ASP.NET Core shared framework. Every other project that holds contract classes references `CodeBrix.WebPlatform.Core`, which has no dependencies other than .NET.
* The program that builds the frontend references `CodeBrix.WebPlatform.FrontendBuilder`, which depends on `CodeBrix.WebPlatform.BackendMapper` (which depends on `CodeBrix.WebPlatform.Core`, `CodeBrix.Templating` and `System.Reflection.MetadataLoadContext`), `CodeBrix.WebPlatform.WasmShrink`, `CodeBrix.LibGit2` (the libgit2 library the `dx` program needs to start) and `CodeBrix.RustTools`, and the host-tools package for the machine it runs on, `CodeBrix.WebPlatform.FrontendBuilder.<Platform>.ApacheLicenseForever` (Linux x64 and ARM64, Windows x64, macOS on Apple Silicon and Intel), which provides the `dx` and `wasm-bindgen` programs and, through its dependency `CodeBrix.WebPlatform.Styling.Tailwind4.<Platform>.MitLicenseForever`, the standalone Tailwind program. These are used while an application is built and are not part of the built application.

To build a platform application a developer installs the .NET SDK and Rust with its WebAssembly target - no Tailwind, no C compiler, and no network access for the platform's crates. Everything else arrives as NuGet packages.

## CodeBrix.WebPlatform supports:

* Declaring a backend's contract where it already lives: attributes on its controllers, hubs and contract classes say what may be null, which Rust type a value becomes, which action answers with a file and where a hub is served
* Declaring what the backend says about itself once, in the backend: the request marker it demands on every state-changing request (read back by the backend's check) and the action that renews an expired session - written into the generated client code, so the frontend never types them by hand
* One standard error body for every error answer, defined once and read by the frontend's shared error type
* The backend's plumbing in one services call and one pipeline call: serving the frontend applications with their deep links (never for an API address), the standard error body for unknown API addresses, framework-produced errors and unreadable requests, the request-marker check on state-changing requests, the Origin check on hub connections and the security headers - each piece on by default and each one can be switched off; the built frontend files served from their precompressed Brotli or gzip copies, and the backend's own answers compressed as they are sent when the developer asks for it
* Several frontend applications for one backend: the one without a name at the site root, each named one below its name (`/sales`, `/admin`), all sharing the backend, the API, the session cookie, the crates and the one build
* Reading a compiled backend without running it - every attribute-routed REST controller and every typed SignalR hub, with the classes and enums they use
* Refusing a backend whose contract is not fully declared, listing every gap in one pass with the controller, action or member it is in
* Writing the typed Rust client code for that backend: a struct per contract class, a function per REST endpoint, and per hub a function for each method and a handler and a latest-value hook for each message
* Overridable .NET-to-Rust type mapping, for the whole application or for one value
* Replaceable templates for everything that is written
* A shared Rust crate that every platform frontend compiles in - the REST transport with session refresh and retry, the SignalR client, the error type, query strings, calendar dates and browser helpers - shipped inside the FrontendBuilder package and written out at build time
* One set-up call for the frontend, generated from the backend: it sets up the REST and SignalR clients together from what the backend declares, and the application passes only what it does when a session is renewed or ends
* Browser helpers every platform page needs, through the Rust browser bindings: the page title (set the way the site's Content-Security-Policy allows), the local date and clock, navigation
* A SignalR client for the browser (the JSON hub protocol over WebSockets, no JavaScript): one shared connection per hub with keep-alive and reconnection, sign-in that renews an expired session through the same refresh as the REST calls, streams that cancel when dropped, handlers that run inside the Dioxus runtime so they may update any signal, and a fake hub for plain `cargo test`
* A prerequisite check that says what is missing on the build machine, and how to fix it, without installing anything
* The platform's Rust crates - every crate a platform frontend may use - carried by the FrontendBuilder package exactly as crates.io published them, so a frontend builds offline; an application may add any crate of its own and then takes crates from crates.io, with its lock file pinning the platform's
* Materializing a frontend: one call writes the generated client code, the shared crate, the platform's crates and the cargo configuration into it, rewriting only what changed, and generates the Tailwind stylesheet when it is missing, after which plain `cargo check`, `cargo test` and `cargo build` work there - on a fresh clone too
* Prebuilt `dx` and `wasm-bindgen` programs for the build platforms (Linux x64 and ARM64, Windows x64, macOS on Apple Silicon and Intel), one package each
* The third-party notices for everything the platform compiles into a site, handed to the build program to serve with the site
* Building the servable site of every frontend application into the backend's web root, or serving one with hot reload, with the Dioxus build tool - release builds shrunk by a packaged `wasm-opt` (or, on request, left unshrunk), and a build failed when shrinking fails; the built files in the web root given Brotli and gzip copies at the strongest setting, once, at build time
* Code splitting, per application: the WebAssembly cut into a main file and pieces that the browser downloads when a page is first opened, under the same Content-Security-Policy
* Crate bundles: a NuGet package can bring Rust crates of its own to the frontend, registered by the build program and written beside the platform's crates under one strict rule - one version of each crate, with the same content wherever it comes from
* No Node.js: nothing here uses, needs or talks to it

## Sample Code

### Declare the contract in the backend

```csharp
using CodeBrix.WebPlatform.Core.Mapping;

[WireRequired]
public sealed class Note
{
    public Guid Id { get; set; }
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
    public async Task<IActionResult> List(CancellationToken cancellationToken) { /* ... */ }
}
```

### Add the platform's plumbing to the backend

```csharp
using CodeBrix.WebPlatform.Backend;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddPlatformBackend(options => options.ContentSecurityPolicy =
    "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data:; "
    + "connect-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'");
var app = builder.Build();

app.UsePlatformBackend(); // the frontend applications in wwwroot, error bodies, checks, headers
app.UseRouting();         // then the backend's own routing, sign-in, controllers and hubs
app.MapControllers();
app.Run();
```

### Build the frontend from a build program

```csharp
using CodeBrix.WebPlatform.FrontendBuilder;

var frontend = new PlatformFrontend(new PlatformFrontendOptions
{
    FrontendDirectory = "frontend",
    BackendProjectPath = "backend/MyApp.Api/MyApp.Api.csproj",
    Output = Console.Out,
});

var report = await frontend.CheckPrerequisitesAsync(); // what is missing, and how to fix it
if (report.IsReady)
{
    await frontend.MaterializeAsync(); // generated client code, shared crate, the platform's crates
    await frontend.BuildAsync(FrontendConfiguration.Release, "backend/MyApp.Api/wwwroot");
}
```

Several frontend applications for the same backend, one of them split into pieces that load when a page is first opened:

```csharp
using CodeBrix.WebPlatform.FrontendBuilder.Applications;

var options = new PlatformFrontendOptions { BackendProjectPath = "backend/MyApp.Api/MyApp.Api.csproj" };
options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = "frontends/home" });              // https://host/
options.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = "frontends/sales", WasmSplit = true }); // /sales
options.Applications.Add(new FrontendApplicationOptions { Name = "admin", FrontendDirectory = "frontends/admin" }); // /admin
```

### Call it from the Rust frontend

```rust
#[component]
fn App() -> Element {
    // the one set-up call, generated from the backend
    use_hook(|| generated::configure_platform(session::update_user, session::session_ended));
    rsx! { Router::<Route> {} }
}
```

```rust
use crate::generated::api::notes;

let all = notes::list().await?; // Vec<Note>, or an ApiError that is always fit to show
```

```rust
use codebrix_webplatform::signalr::use_subscription;
use crate::generated::hubs::NotesHub;

#[component]
fn NotesFeed() -> Element {
    let hub = NotesHub::use_hub(); // connected while the component is mounted
    let titles = use_signal(Vec::<String>::new);
    use_subscription(|| {
        hub.on_added(move |note| {
            let mut titles = titles;
            titles.write().push(note.title); // runs inside the Dioxus runtime
        })
    });
    rsx! { for title in titles() { p { "{title}" } } }
}
```

## Documentation

The NuGet packages include `AGENT-README.txt`, a complete API reference and usage guide written for AI coding agents - point your agent at that file when it is writing code against these libraries.

Additional sample code and usage examples are available in the test projects:
https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/tests

A complete application built on the platform - backend, hubs, build program and frontend - is in the `samples/WorkSite` folder:
https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/samples/WorkSite

One backend with several frontend applications - one at the site root and two below their names, one of them with code splitting - is in the `samples/ApplicationSplitter` folder:
https://github.com/ellisnet/CodeBrix.WebPlatform/tree/main/samples/ApplicationSplitter

## License

CodeBrix.WebPlatform is licensed under the Apache License 2.0 - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.WebPlatform/blob/main/LICENSE) file.

For licensing and provenance information about the open source code included in
this package, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.WebPlatform/blob/main/THIRD-PARTY-NOTICES.txt).
