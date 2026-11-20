================================================================================
README-INDEX: CodeBrix.WebPlatform
Map of the README files in this repository
================================================================================

If you are an AI coding agent: find the NuGet package you are consuming below
and read its AGENT-README file in full. Read MAINTAINER-README.txt only if you
are changing this repository itself.

AGENT-README FILES (consumer documentation, one per NuGet package)
------------------------------------------------------------------
  AGENT-README.txt
      One file for every package of this repository:
      CodeBrix.WebPlatform.Core.ApacheLicenseForever - the attributes a .NET
      backend declares its REST and SignalR contract with, the standard
      error body, the description of the frontend applications and the
      registry of crate bundles.
      CodeBrix.WebPlatform.Backend.ApacheLicenseForever - the ASP.NET Core
      plumbing of a backend in one services call and one pipeline call:
      serving the frontend applications, the standard error answers, the
      request-marker and hub Origin checks and the security headers.
      CodeBrix.WebPlatform.BackendMapper.ApacheLicenseForever - the build-time
      library that reads a compiled backend and writes the frontend's typed
      Rust client code for it.
      CodeBrix.WebPlatform.FrontendBuilder.ApacheLicenseForever - the
      build-time library a build program calls to check the machine,
      materialize the frontend applications (generated client code, the
      shared Rust crate every platform frontend compiles in, the platform's
      crates and those of registered crate bundles, and the cargo
      configuration) and build them (with code splitting on request) or
      serve one with dx.
      CodeBrix.WebPlatform.FrontendBuilder.<Platform>.ApacheLicenseForever
      (LinuxX64, LinuxArm64, WindowsX64, MacOSArm64, MacOSX64) - the
      prebuilt dx and wasm-bindgen programs for one build machine.

MAINTAINER AND EXTRAS
---------------------
  MAINTAINER-README.txt
      Building, testing, packaging, versioning and provenance notes for
      maintainers.
  EXTRAS-README.txt
      Samples, tools and other non-package content in this repository.
  tools/dx-native-tools/README.txt
      How the dx and wasm-bindgen programs of the host-tools packages are
      built, gated and adopted.
  samples/WorkSite/MAINTAINER-README.txt
      The WorkSite sample application: its own rules and how it is built and
      run.
  samples/ApplicationSplitter/MAINTAINER-README.txt
      The ApplicationSplitter sample: one backend with several frontend
      applications and code splitting; what it proves and how it is built,
      run and checked.

GENERAL
-------
  README.md
      Human-facing overview shown on GitHub and nuget.org.
  README-INDEX.txt
      This file.
  THIRD-PARTY-NOTICES.txt
      What came from where, and under which licences, for the packages;
      it points to the generated notices below.
  src/CodeBrix.WebPlatform.FrontendBuilder/Rust/notices/THIRD-PARTY-NOTICES-PlatformCrates.txt
      The platform's crates and the shared crate's third-party code
      (generated; packed into the FrontendBuilder package).
  tools/dx-native-tools/notices/THIRD-PARTY-NOTICES-Dioxus.txt
  tools/dx-native-tools/notices/THIRD-PARTY-NOTICES-WasmBindgen.txt
      What is compiled into dx and wasm-bindgen (generated; packed into the
      host-tools packages).
  tools/dx-native-tools/notices/THIRD-PARTY-NOTICES-ToolSources.txt
      Every crate vendored to build the two programs (generated).
  samples/WorkSite/THIRD-PARTY-NOTICES.txt
      What the WorkSite sample uses beyond the platform (written by hand).

================================================================================
