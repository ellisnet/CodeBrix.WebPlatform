<#
=============================================================================================
build.dx.ps1 - build dx.exe and wasm-bindgen.exe for win-x64, from this repository only
=============================================================================================

  NEVER YET RUN. Written on Linux on 2026-11-18 (updated 2026-11-19 for libgit2, still unrun),
  where it cannot run. Expect to fix something
  on the first real run; fix it IN THE SCRIPT and record it in ..\BUILD-PROVENANCE.txt, then
  rewrite this header and README.txt's status block with what the run established.

USAGE (from a PowerShell prompt, in a clone of the repository):

    cd tools\dx-native-tools\windows
    .\build.dx.ps1
    .\build.dx.ps1 -BuildRoot D:\cbx-dx -WasmOptDir <folder holding wasm-opt.exe>

WHAT IT DOES
  * reads ..\linux\pins.env (the one source of truth: TOOL_SOURCES_DIR, RUST_TOOLCHAIN, the
    tool versions and features, LICENCE_FILES, WINDOWS_RUSTFLAGS);
  * takes the prebuilt libgit2 that dx links out of the published NuGet package
    LIBGIT2_PACKAGE_ID LIBGIT2_PACKAGE_VERSION (pins.env): the package file from the local NuGet
    cache, else downloaded from nuget.org (THE ONE STEP THAT MAY USE THE NETWORK), checked
    against LIBGIT2_NUPKG_SHA256, and runtimes\win-x64\native\git2-49e408b.dll from it, checked
    against LIBGIT2_SHA256_WIN_X64, into ..\output\libgit2\win-x64\. The package carries NO
    import library, so the script MAKES one from the DLL: dumpbin /exports -> a .def file ->
    lib /def /machine:x64 -> git2-49e408b.lib. Nothing of it is committed;
  * builds both tools exactly as the Linux build does - cargo +<RUST_TOOLCHAIN> build
    --release --locked --offline --target x86_64-pc-windows-msvc (dx with DX_FEATURES; the
    libgit2-sys stand-in links the import library) - from the sources where they are, with
    CARGO_TARGET_DIR under -BuildRoot, so the source folders are never written to;
  * runs the gate (README.txt, THE GATE) and writes ..\output\win-x64\.

  It installs nothing, and downloads nothing but the libgit2 package when it is not in the NuGet
  cache: --offline and the vendored crates mean cargo has nothing to fetch (Windows has no
  equivalent of the Linux build's --network none; the build log shows that no "Downloading" or
  "Updating" line was printed - the gate checks it). Anything missing is named, with the command
  that installs it, and the script stops.

  THE BUILT dx.exe NEEDS git2-49e408b.dll TO START (beside dx.exe or on PATH); Windows refuses
  to start it without the DLL. The gate runs it with the DLL's folder first on PATH, and once
  without, to record that it does not start (Windows may show a "system error" dialog for that
  run; the script waits at most 30 seconds and then ends the process).
=============================================================================================
#>

[CmdletBinding()]
param(
    # Where cargo's target folder lives. Kept short and OUTSIDE the user profile: the gate fails
    # if the user-profile path or the repository path is found inside a shipped .exe.
    [string] $BuildRoot = (Join-Path $env:SystemDrive 'cbx-dx'),
    # The folder holding the wasm-opt.exe the functional gate puts first on dx's PATH (the
    # CodeBrix.WebPlatform.WasmShrink package's win-x64 copy). Without one the functional gate is
    # reported as SKIPPED, and the result must not be adopted until it has been run.
    [string] $WasmOptDir = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = $PSScriptRoot
$ToolsDir  = Split-Path -Parent $ScriptDir
$RepoRoot  = Split-Path -Parent (Split-Path -Parent $ToolsDir)
$OutputDir = Join-Path $ToolsDir 'output'
$Rid       = 'win-x64'
$Triple    = 'x86_64-pc-windows-msvc'
$Started   = Get-Date

# ---------------------------------------------------------------------------------------------
# Pins - ..\linux\pins.env. Multi-line "..." values are joined with newlines (LICENCE_FILES);
# '...' values are taken literally.
# ---------------------------------------------------------------------------------------------
function Read-Pins {
    param([Parameter(Mandatory)][string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "pins.env was not found at $Path. It is part of the repository - if it is missing after a clone, check the root .gitignore's '*.env' rule (see ..\.gitignore)."
    }
    $pins = @{}
    $key = $null
    $acc = $null
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($null -ne $key) {
            if ($line.EndsWith('"')) {
                $acc += "`n" + $line.Substring(0, $line.Length - 1)
                $pins[$key] = $acc
                $key = $null
            } else {
                $acc += "`n" + $line
            }
            continue
        }
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
        $eq = $trimmed.IndexOf('=')
        if ($eq -lt 1) { continue }
        $k = $trimmed.Substring(0, $eq).Trim()
        if ($k -notmatch '^[A-Z0-9_]+$') { continue }
        $value = $trimmed.Substring($eq + 1)
        if ($value.StartsWith('"')) {
            if ($value.Length -ge 2 -and $value.EndsWith('"')) {
                $pins[$k] = $value.Substring(1, $value.Length - 2)
            } else {
                $key = $k
                $acc = $value.Substring(1)
            }
        } elseif ($value.StartsWith("'") -and $value.EndsWith("'") -and $value.Length -ge 2) {
            $pins[$k] = $value.Substring(1, $value.Length - 2)
        } else {
            $pins[$k] = $value
        }
    }
    foreach ($required in @('TOOL_SOURCES_DIR', 'RUST_TOOLCHAIN', 'DX_DIR', 'DX_CRATE_VERSION', 'DX_FEATURES', 'DX_GIT_SHA',
                            'DX_EXPECTED_VERSION', 'DX_EXE', 'WASM_BINDGEN_DIR', 'WASM_BINDGEN_VERSION',
                            'WASM_BINDGEN_EXPECTED_VERSION', 'WASM_BINDGEN_EXE', 'LICENCE_FILES', 'WINDOWS_RUSTFLAGS',
                            'FUNCTIONAL_FRONTEND', 'FUNCTIONAL_DX_ARGS', 'FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION',
                            'LIBGIT2_PACKAGE_ID', 'LIBGIT2_PACKAGE_VERSION', 'LIBGIT2_NUPKG_SHA256', 'LIBGIT2_NUPKG_URL',
                            'LIBGIT2_VERSION', 'LIBGIT2_LINK_NAME', 'LIBGIT2_SHA256_WIN_X64')) {
        if (-not $pins.ContainsKey($required)) { throw "pins.env has no $required." }
    }
    return $pins
}

$Pins    = Read-Pins (Join-Path $ToolsDir 'linux\pins.env')
$Sources = Join-Path $RepoRoot ($Pins.TOOL_SOURCES_DIR -replace '/', '\')
$Out     = Join-Path $OutputDir $Rid
$Target  = Join-Path $BuildRoot 'target'

# DLLs a shipped .exe may import: the operating system only. With the static CRT there must be
# no VCRUNTIME*, MSVCP*, ucrtbase or api-ms-win-crt-* import - the gate fails on any. An import
# outside this list fails the gate too: decide, then add it here (and record why) - never
# silently. api-ms-win-core-* are Windows API sets (part of the operating system).
$AllowedImports = @('KERNEL32.dll', 'ADVAPI32.dll', 'USER32.dll', 'SHELL32.dll', 'ole32.dll', 'OLEAUT32.dll',
                    'bcrypt.dll', 'bcryptprimitives.dll', 'ntdll.dll', 'SHLWAPI.dll', 'WS2_32.dll', 'USERENV.dll',
                    'CRYPT32.dll', 'Secur32.dll', 'ncrypt.dll', 'IPHLPAPI.DLL', 'dbghelp.dll', 'PSAPI.DLL',
                    'RPCRT4.dll', 'WINMM.dll', 'GDI32.dll', 'COMCTL32.dll', 'NETAPI32.dll', 'mswsock.dll')
$ForbiddenImportPattern = '^(VCRUNTIME|MSVCP|ucrtbase|api-ms-win-crt-|libssl|libcrypto|zlib|libgit2|libssh2)'
# dx.exe imports libgit2's DLL too (and must); wasm-bindgen.exe must not.
$Libgit2Dll = "$($Pins.LIBGIT2_LINK_NAME).dll"

# ---------------------------------------------------------------------------------------------
# Prerequisites - checked, never installed.
# ---------------------------------------------------------------------------------------------
function Find-VisualStudio {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw @"
Visual Studio was not found (no vswhere.exe). Install Visual Studio 2022 or newer, or the Build
Tools, with the "Desktop development with C++" workload (MSVC links Rust programs and compiles
the C code in ring and zstd). This script does not install anything:
    winget install Microsoft.VisualStudio.2022.BuildTools
"@
    }
    $path = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $path) { throw "No Visual Studio instance with the C++ x64/x86 build tools was found (vswhere)." }
    return $path.Trim()
}

function Import-DeveloperEnvironment {
    param([Parameter(Mandatory)][string] $VsPath)
    $vcvars = Join-Path $VsPath 'VC\Auxiliary\Build\vcvarsall.bat'
    if (-not (Test-Path -LiteralPath $vcvars)) { throw "vcvarsall.bat not found at $vcvars" }
    $lines = & cmd.exe /s /c "`"$vcvars`" x64 > nul && set"
    if ($LASTEXITCODE -ne 0) { throw "vcvarsall.bat x64 failed - is the MSVC x64 toolset installed?" }
    foreach ($line in $lines) {
        $eq = $line.IndexOf('=')
        if ($eq -gt 0) { [Environment]::SetEnvironmentVariable($line.Substring(0, $eq), $line.Substring($eq + 1), 'Process') }
    }
    foreach ($tool in @('cl.exe', 'link.exe', 'dumpbin.exe', 'lib.exe')) {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not on PATH after vcvarsall x64." }
    }
}

function Assert-Rust {
    if (-not (Get-Command rustup -ErrorAction SilentlyContinue)) {
        throw "rustup was not found. Install it (this script does not):`n    winget install Rustlang.Rustup`nthen:  rustup toolchain install $($Pins.RUST_TOOLCHAIN) --profile minimal --target $Triple"
    }
    $toolchains = (& rustup toolchain list) -join "`n"
    if ($toolchains -notmatch [regex]::Escape("$($Pins.RUST_TOOLCHAIN)-")) {
        throw "Rust $($Pins.RUST_TOOLCHAIN) is not installed. Install it (this script does not):`n    rustup toolchain install $($Pins.RUST_TOOLCHAIN) --profile minimal --target $Triple"
    }
    $targets = (& rustup target list --installed --toolchain $Pins.RUST_TOOLCHAIN) -join "`n"
    if ($targets -notmatch [regex]::Escape($Triple)) {
        throw "The Rust target $Triple is not installed for $($Pins.RUST_TOOLCHAIN):`n    rustup target add $Triple --toolchain $($Pins.RUST_TOOLCHAIN)"
    }
    $v = & cargo "+$($Pins.RUST_TOOLCHAIN)" --version
    $r = & rustc "+$($Pins.RUST_TOOLCHAIN)" --version
    if ($r -notmatch "^rustc $([regex]::Escape($Pins.RUST_TOOLCHAIN)) ") { throw "rustc +$($Pins.RUST_TOOLCHAIN) reports '$r'." }
    return @{ Cargo = $v; Rustc = $r }
}

foreach ($f in @("$($Pins.DX_DIR)\Cargo.toml", "$($Pins.DX_DIR)\Cargo.lock", "$($Pins.WASM_BINDGEN_DIR)\Cargo.toml",
                 "$($Pins.WASM_BINDGEN_DIR)\Cargo.lock", '.cargo\config.toml', "$($Pins.DX_DIR)\.cargo_vcs_info.json")) {
    if (-not (Test-Path -LiteralPath (Join-Path $Sources $f))) {
        throw "$Sources\$f is missing. TOOL_SOURCES_DIR in pins.env ($($Pins.TOOL_SOURCES_DIR)) must name the folder that holds $($Pins.DX_DIR)\, $($Pins.WASM_BINDGEN_DIR)\, vendor\ and .cargo\config.toml. Nothing is downloaded."
    }
}
$vcs = Get-Content -Raw -LiteralPath (Join-Path $Sources "$($Pins.DX_DIR)\.cargo_vcs_info.json")
if ($vcs -notmatch $Pins.DX_GIT_SHA) { throw "$($Pins.DX_DIR)\.cargo_vcs_info.json does not name commit $($Pins.DX_GIT_SHA) (pins.env DX_GIT_SHA)." }
if ($BuildRoot.StartsWith($env:USERPROFILE, [StringComparison]::OrdinalIgnoreCase) -or $BuildRoot.StartsWith($RepoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "-BuildRoot $BuildRoot is inside the user profile or the repository; choose a short folder outside both (default C:\cbx-dx)."
}

if (-not (Test-Path -LiteralPath (Join-Path $Sources 'codebrix-libgit2-sys\Cargo.toml'))) { throw "$Sources\codebrix-libgit2-sys (the libgit2-sys stand-in) is missing." }

$VsPath = Find-VisualStudio
Import-DeveloperEnvironment -VsPath $VsPath
$Rust = Assert-Rust

# ---------------------------------------------------------------------------------------------
# libgit2 - out of the published package, never built here; plus an import library made from
# the DLL (the package has none). Into ..\output\libgit2\win-x64\ (build scratch).
# ---------------------------------------------------------------------------------------------
function Get-Sha256([string] $Path) { (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant() }
function Get-Libgit2 {
    $idLower = $Pins.LIBGIT2_PACKAGE_ID.ToLowerInvariant()
    $nupkgName = "$idLower.$($Pins.LIBGIT2_PACKAGE_VERSION).nupkg"
    $dir = Join-Path $OutputDir 'libgit2'
    $lib = Join-Path $dir $Rid
    New-Item -ItemType Directory -Force -Path $lib | Out-Null
    $nupkg = Join-Path $dir $nupkgName
    $cacheRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    $cached = Join-Path $cacheRoot "$idLower\$($Pins.LIBGIT2_PACKAGE_VERSION)\$nupkgName"
    $from = ''
    if ((Test-Path -LiteralPath $nupkg) -and ((Get-Sha256 $nupkg) -eq $Pins.LIBGIT2_NUPKG_SHA256)) {
        $from = 'output\libgit2 (an earlier run)'
    } elseif ((Test-Path -LiteralPath $cached) -and ((Get-Sha256 $cached) -eq $Pins.LIBGIT2_NUPKG_SHA256)) {
        Copy-Item -LiteralPath $cached -Destination $nupkg -Force
        $from = "the local NuGet cache ($cached)"
    } else {
        Write-Host "libgit2       : downloading $($Pins.LIBGIT2_PACKAGE_ID) $($Pins.LIBGIT2_PACKAGE_VERSION) from nuget.org (THE NETWORK)"
        Invoke-WebRequest -UseBasicParsing -Uri $Pins.LIBGIT2_NUPKG_URL -OutFile $nupkg
        $from = "nuget.org ($($Pins.LIBGIT2_NUPKG_URL))"
    }
    $got = Get-Sha256 $nupkg
    if ($got -ne $Pins.LIBGIT2_NUPKG_SHA256) { throw "$nupkg has sha256 $got; pins.env LIBGIT2_NUPKG_SHA256 says $($Pins.LIBGIT2_NUPKG_SHA256)." }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $dll = Join-Path $lib $Libgit2Dll
    $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg)
    try {
        $entry = $zip.GetEntry("runtimes/$Rid/native/$Libgit2Dll")
        if ($null -eq $entry) { throw "the package has no runtimes/$Rid/native/$Libgit2Dll" }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dll, $true)
    } finally { $zip.Dispose() }
    $got = Get-Sha256 $dll
    if ($got -ne $Pins.LIBGIT2_SHA256_WIN_X64) { throw "$Libgit2Dll from the package has sha256 $got; pins.env LIBGIT2_SHA256_WIN_X64 says $($Pins.LIBGIT2_SHA256_WIN_X64)." }

    # The import library: every exported name, from dumpbin /exports, into a .def file, then lib.
    # dumpbin prints one line per export: "ordinal hint RVA name".
    $exports = @(& dumpbin.exe /nologo /exports $dll | ForEach-Object {
        if ($_ -match '^\s+\d+\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]{8}\s+(\S+)') { $Matches[1] }
    })
    if ($exports.Count -lt 100) { throw "only $($exports.Count) exports were read from $Libgit2Dll by dumpbin /exports - check the parser against its output" }
    $def = Join-Path $lib "$($Pins.LIBGIT2_LINK_NAME).def"
    @("LIBRARY $Libgit2Dll", 'EXPORTS') + ($exports | ForEach-Object { "    $_" }) | Set-Content -LiteralPath $def -Encoding ascii
    $importLib = Join-Path $lib "$($Pins.LIBGIT2_LINK_NAME).lib"
    & lib.exe /nologo "/def:$def" /machine:x64 "/out:$importLib" | Write-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $importLib)) { throw "lib.exe could not make $importLib from $def" }
    @(
        "libgit2 $($Pins.LIBGIT2_VERSION) for $Rid - for LINKING dx and for RUNNING it in the gate; never committed",
        "Package  : $($Pins.LIBGIT2_PACKAGE_ID) $($Pins.LIBGIT2_PACKAGE_VERSION), sha256 $($Pins.LIBGIT2_NUPKG_SHA256)",
        "Taken    : $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) UTC, from $from",
        "File     : runtimes/$Rid/native/$Libgit2Dll, sha256 $($Pins.LIBGIT2_SHA256_WIN_X64)",
        "Made here: $($Pins.LIBGIT2_LINK_NAME).def ($($exports.Count) exports, from dumpbin /exports) and $($Pins.LIBGIT2_LINK_NAME).lib (lib /def /machine:x64), sha256 $(Get-Sha256 $importLib)"
    ) | Set-Content -LiteralPath (Join-Path $lib 'SOURCE.txt') -Encoding utf8
    Write-Host "libgit2       : $Libgit2Dll from $from; import library made from its $($exports.Count) exports"
    return @{ Dir = $lib; From = $from; Exports = $exports.Count; ImportLibSha = (Get-Sha256 $importLib) }
}
$Libgit2 = Get-Libgit2
Write-Host "Visual Studio : $VsPath"
Write-Host "Rust          : $($Rust.Rustc) / $($Rust.Cargo)"
Write-Host "Sources       : $Sources (read only)"
Write-Host "Build root    : $BuildRoot"

# ---------------------------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $Target | Out-Null
$env:RUSTFLAGS = "--remap-path-prefix=$Sources=tool-sources --remap-path-prefix=$Target=target --remap-path-prefix=$env:USERPROFILE=user-home $($Pins.WINDOWS_RUSTFLAGS)"
$env:DIOXUS_CLI_GIT_SHA = $Pins.DX_GIT_SHA
$env:CARGO_INCREMENTAL = '0'
$env:CODEBRIX_LIBGIT2_LIB_DIR = $Libgit2.Dir
$env:CODEBRIX_LIBGIT2_LINK_NAME = $Pins.LIBGIT2_LINK_NAME
Write-Host "RUSTFLAGS     : $env:RUSTFLAGS"

$BuildSeconds = @{}
function Invoke-ToolBuild {
    param([string] $Dir, [string] $Features)
    $cargoArgs = @("+$($Pins.RUST_TOOLCHAIN)", 'build', '--release', '--locked', '--offline', '--target', $Triple)
    if ($Features) { $cargoArgs += @('--features', $Features) }
    $log = Join-Path $BuildRoot "$Dir.build.log"
    Write-Host "--- cargo $($cargoArgs -join ' ')   (in $Dir) ---"
    $env:CARGO_TARGET_DIR = Join-Path $Target $Dir
    $t0 = Get-Date
    Push-Location (Join-Path $Sources $Dir)
    try {
        & cargo @cargoArgs *> $log
        if ($LASTEXITCODE -ne 0) { Get-Content $log -Tail 60 | Write-Host; throw "cargo failed for $Dir (log: $log)" }
    } finally { Pop-Location }
    $BuildSeconds[$Dir] = [int]((Get-Date) - $t0).TotalSeconds
    if (Select-String -Path $log -Pattern '^\s*(Downloading|Downloaded|Updating) ' -Quiet) { throw "cargo reported a download for $Dir - the build must fetch nothing (log: $log)." }
    Write-Host "  $Dir built in $($BuildSeconds[$Dir])s"
}
Invoke-ToolBuild -Dir $Pins.DX_DIR -Features $Pins.DX_FEATURES
Invoke-ToolBuild -Dir $Pins.WASM_BINDGEN_DIR -Features $Pins.WASM_BINDGEN_FEATURES

# ---------------------------------------------------------------------------------------------
# Collect + licence files
# ---------------------------------------------------------------------------------------------
if (Test-Path -LiteralPath $Out) { Remove-Item -Recurse -Force -LiteralPath $Out }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Exes = @("$($Pins.DX_EXE).exe", "$($Pins.WASM_BINDGEN_EXE).exe")
Copy-Item (Join-Path $Target "$($Pins.DX_DIR)\$Triple\release\$($Exes[0])") $Out
Copy-Item (Join-Path $Target "$($Pins.WASM_BINDGEN_DIR)\$Triple\release\$($Exes[1])") $Out
$LicenceNames = @()
foreach ($entry in ($Pins.LICENCE_FILES -split "`n")) {
    $entry = $entry.Trim(); if (-not $entry) { continue }
    $name = $entry.Substring(0, $entry.IndexOf(':')); $from = $entry.Substring($entry.IndexOf(':') + 1)
    $path = if ($from.StartsWith('@src/')) { Join-Path $Sources ($from.Substring(5) -replace '/', '\') } else { Join-Path $ToolsDir ($from -replace '/', '\') }
    if (-not (Test-Path -LiteralPath $path)) { throw "licence / notices source missing: $from" }
    Copy-Item $path (Join-Path $Out $name)
    $LicenceNames += $name
}

# ---------------------------------------------------------------------------------------------
# THE GATE
# ---------------------------------------------------------------------------------------------
$Gate = New-Object System.Collections.Generic.List[string]
$Failed = $false
function Pass([string] $m) { $script:Gate.Add("  [ok] $m"); Write-Host "  [ok] $m" }
function Fail([string] $m) { $script:Gate.Add("  [FAIL] $m"); Write-Host "  [FAIL] $m"; $script:Failed = $true }
function Info([string] $m) { $script:Gate.Add("  [info] $m"); Write-Host "  [info] $m" }

$Expected = @{ $Exes[0] = $Pins.DX_EXPECTED_VERSION; $Exes[1] = $Pins.WASM_BINDGEN_EXPECTED_VERSION }
foreach ($exe in $Exes) {
    $path = Join-Path $Out $exe
    $Gate.Add("  === $exe ==="); Write-Host "  === $exe ==="
    $headers = (& dumpbin.exe /nologo /headers $path) -join "`n"
    if ($headers -match '8664 machine \(x64\)') { Pass 'PE machine 8664 (x64)' } else { Fail 'PE machine is not 8664 (x64)' }
    $importsText = & dumpbin.exe /nologo /imports $path
    $imports = @($importsText | Where-Object { $_ -match '^\s{4}(\S+\.(dll|DLL))\s*$' } | ForEach-Object { $Matches[1] } | Sort-Object -Unique)
    if ($imports.Count -eq 0) { Fail 'no imports parsed from dumpbin /imports - check the parser against its output' }
    $isDx = ($exe -eq $Exes[0])
    $allowed = if ($isDx) { $AllowedImports + @($Libgit2Dll) } else { $AllowedImports }
    $bad = @($imports | Where-Object { $_ -match $ForbiddenImportPattern })
    $unknown = @($imports | Where-Object { ($allowed -notcontains $_) -and ($_ -notmatch '^api-ms-win-core-') -and ($_ -notmatch $ForbiddenImportPattern) })
    if ($bad.Count -gt 0) { Fail "FORBIDDEN imports (static CRT did not take?): $($bad -join ' ')" }
    if ($unknown.Count -gt 0) { Fail "imports outside the operating-system list: $($unknown -join ' ')" }
    if ($bad.Count -eq 0 -and $unknown.Count -eq 0) { Pass "imports are operating-system DLLs only$(if ($isDx) { ' and libgit2' }): $($imports -join ' ')" }
    if ($isDx) {
        if ($imports -contains $Libgit2Dll) { Pass "dx.exe imports $Libgit2Dll (linked through the stand-in, from $($Pins.LIBGIT2_PACKAGE_ID) $($Pins.LIBGIT2_PACKAGE_VERSION))" }
        else { Fail "dx.exe does not import $Libgit2Dll - the libgit2-sys stand-in was not linked as intended" }
    }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
    $unicode = [System.Text.Encoding]::Unicode.GetString($bytes)
    $leaks = @($env:USERPROFILE, $RepoRoot, $Sources, $Target) | Where-Object { $ascii.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $unicode.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
    if ($leaks) { Fail "build paths found in the binary: $($leaks -join '; ')" } else { Pass 'no user-profile, repository, source or target path in the binary' }
    $savedPath = $env:PATH
    $env:PATH = "$($Libgit2.Dir);$savedPath"
    try { $got = (& $path --version | Select-Object -First 1) } finally { $env:PATH = $savedPath }
    if ($got -eq $Expected[$exe]) { Pass "$exe --version ($Libgit2Dll's folder first on PATH): $got" } else { Fail "$exe --version: expected '$($Expected[$exe])', got '$got'" }
    if ($isDx) {
        # Without the DLL: Windows must refuse to start dx.exe (0xC0000135, STATUS_DLL_NOT_FOUND).
        $p = Start-Process -FilePath $path -ArgumentList '--version' -PassThru -NoNewWindow
        if (-not $p.WaitForExit(30000)) { $p.Kill(); Fail "dx.exe without $Libgit2Dll did not end within 30 seconds (a system error dialog?) - ended it" }
        elseif ($p.ExitCode -eq -1073741515) { Pass "dx.exe WITHOUT $Libgit2Dll does not start: exit 0xC0000135 (STATUS_DLL_NOT_FOUND)" }
        else { Fail "dx.exe without $Libgit2Dll: expected exit 0xC0000135, got $($p.ExitCode) (is a copy of the DLL on PATH or beside dx.exe?)" }
    }
}

# Functional: build a scratch copy of the frontend, as ..\linux\functional-gate.sh does.
$Gate.Add('  === functional gate ==='); Write-Host '  === functional gate ==='
if (-not $WasmOptDir) {
    $cacheRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    $WasmOptDir = Join-Path $cacheRoot "codebrix.webplatform.wasmshrink.apachelicenseforever\$($Pins.FUNCTIONAL_WASMSHRINK_PACKAGE_VERSION)\tools\wasm-opt\$Rid"
}
$WasmSize = 'n/a'
$SkipSize = 'n/a'
$FunctionalSkipped = $false
if (-not (Test-Path -LiteralPath (Join-Path $WasmOptDir 'wasm-opt.exe'))) {
    Info "SKIPPED - no wasm-opt.exe in $WasmOptDir (pass -WasmOptDir). DO NOT ADOPT until the functional gate has passed."
    $FunctionalSkipped = $true
} elseif (-not (Get-Command tailwindcss -ErrorAction SilentlyContinue)) {
    Fail 'tailwindcss is not on PATH (see samples\WorkSite\tailwindcss)'
} else {
    $FunctionalSkipped = $false
    $fg = Join-Path $BuildRoot 'functional-gate'
    if (Test-Path -LiteralPath $fg) { Remove-Item -Recurse -Force -LiteralPath $fg }
    $copy = Join-Path $fg 'WorkSite\frontend'
    New-Item -ItemType Directory -Force -Path $copy, (Join-Path $fg 'bin'), (Join-Path $fg 'dx-home') | Out-Null
    $frontend = Join-Path $RepoRoot ($Pins.FUNCTIONAL_FRONTEND -replace '/', '\')
    foreach ($f in @('platform\codebrix-webplatform\Cargo.toml', 'platform\vendor\.codebrix-platform-crates', 'src\generated\mod.rs', '.cargo\config.toml')) { if (-not (Test-Path -LiteralPath (Join-Path $frontend $f))) { throw "$frontend\$f does not exist: materialize the frontend first (``dotnet run --project build\WorkSite.BuildFrontend -- generate`` in samples\WorkSite)." } }
    & robocopy $frontend $copy /E /XD (Join-Path $frontend 'target') /NFL /NDL /NJH /NJS /NP | Out-Null
    Copy-Item (Join-Path $Out $Exes[0]), (Join-Path $Out $Exes[1]) (Join-Path $fg 'bin')
    $savedPath = $env:PATH
    $env:PATH = "$WasmOptDir;$(Join-Path $fg 'bin');$($Libgit2.Dir);$savedPath"
    $env:DX_HOME = Join-Path $fg 'dx-home'
    Remove-Item Env:RUSTFLAGS, Env:CARGO_TARGET_DIR, Env:DIOXUS_CLI_GIT_SHA, Env:CODEBRIX_LIBGIT2_LIB_DIR, Env:CODEBRIX_LIBGIT2_LINK_NAME -ErrorAction SilentlyContinue
    Push-Location $copy
    try {
        $dxLog = Join-Path $fg 'dx-build.log'
        & (Join-Path $fg "bin\$($Exes[0])") @($Pins.FUNCTIONAL_DX_ARGS -split ' ') *> $dxLog
        $rc = $LASTEXITCODE
    } finally { Pop-Location; $env:PATH = $savedPath }
    if ($rc -eq 0) { Pass 'dx exited 0' } else { Fail "dx exited $rc (log: $dxLog)" }
    if (Select-String -Path $dxLog -Pattern 'wasm-opt failed' -Quiet) { Fail 'dx says "wasm-opt failed"' } else { Pass 'no "wasm-opt failed" in dx''s output' }
    $public = Get-ChildItem -Recurse -Directory (Join-Path $copy 'target\dx') | Where-Object { $_.FullName -like '*\release\web\public' } | Select-Object -First 1
    if ($public -and (Test-Path (Join-Path $public.FullName 'index.html'))) { Pass "site folder with index.html: $($public.FullName)" } else { Fail 'no release\web\public\index.html' }
    if ($public) {
        $wasm = @(Get-ChildItem -Recurse -File $public.FullName -Filter *.wasm)
        if ($wasm.Count -eq 1) { $WasmSize = $wasm[0].Length; Pass "one .wasm: $($wasm[0].Name), $WasmSize bytes" } else { Fail "expected one .wasm, found $($wasm.Count)" }
        $glue = @(Get-ChildItem -File (Join-Path $public.FullName 'assets') -Filter *.js)
        if ($glue.Count -eq 1 -and (Get-Content -Raw (Join-Path $public.FullName 'index.html')).Contains($glue[0].Name)) { Pass "index.html loads the glue file $($glue[0].Name)" } else { Fail 'glue .js missing or not named by index.html' }
        if (@(Get-ChildItem -File (Join-Path $public.FullName 'assets') -Filter 'tailwind*.css').Count -eq 1) { Pass 'Tailwind stylesheet present' } else { Fail 'no tailwind*.css' }
        if (Test-Path (Join-Path $public.FullName 'assets\snippets')) { Pass 'wasm-bindgen snippets present' } else { Fail 'no assets\snippets' }
        Info "site files: $(@(Get-ChildItem -Recurse -File $public.FullName).Count)"
        Move-Item -LiteralPath (Join-Path $copy 'target\dx') -Destination (Join-Path $fg 'run-new-tools')
    }

    # DX_SKIP_WASM_OPT: a release build with no wasm-opt anywhere on PATH (nor in dx's data
    # folder) must succeed, say so, and give a larger .wasm (..\linux\functional-gate.sh, check 6).
    $noOptPath = (($savedPath -split ';') | Where-Object { $_ -and -not (Test-Path -LiteralPath (Join-Path $_ 'wasm-opt.exe')) }) -join ';'
    $env:PATH = "$(Join-Path $fg 'bin');$($Libgit2.Dir);$noOptPath"
    $env:DX_HOME = Join-Path $fg 'dx-home-noopt'
    $env:DX_SKIP_WASM_OPT = '1'
    New-Item -ItemType Directory -Force -Path $env:DX_HOME | Out-Null
    Push-Location $copy
    try {
        $skipLog = Join-Path $fg 'dx-build-skip-wasm-opt.log'
        & (Join-Path $fg "bin\$($Exes[0])") @($Pins.FUNCTIONAL_DX_ARGS -split ' ') *> $skipLog
        $rc = $LASTEXITCODE
    } finally { Pop-Location; $env:PATH = $savedPath; Remove-Item Env:DX_SKIP_WASM_OPT -ErrorAction SilentlyContinue }
    $skipWasm = @(Get-ChildItem -Recurse -File (Join-Path $copy 'target\dx') -Filter *.wasm -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like '*\release\web\public\*' })
    if ($rc -eq 0 -and $skipWasm.Count -eq 1) {
        $SkipSize = $skipWasm[0].Length
        Pass "DX_SKIP_WASM_OPT=1 with no wasm-opt: the release build exited 0"
        if (Select-String -Path $skipLog -Pattern "Skipping wasm-opt at the caller's request" -SimpleMatch -Quiet) { Pass 'dx said it skipped wasm-opt at the caller''s request' } else { Fail "no 'Skipping wasm-opt at the caller's request' line ($skipLog)" }
        if ($WasmSize -ne 'n/a' -and $SkipSize -gt $WasmSize) { Pass "the unshrunk .wasm is larger: $SkipSize bytes against $WasmSize" } else { Fail "the unshrunk .wasm ($SkipSize bytes) is not larger than the shrunk one ($WasmSize)" }
    } else { Fail "DX_SKIP_WASM_OPT=1 release build without wasm-opt: dx exited $rc (log: $skipLog)" }

    # dx new from a local template, with no git program on PATH: the clone is libgit2's
    # (..\linux\functional-gate.sh, check 7). git itself only makes the template repository.
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $tpl = Join-Path $fg 'template'
        New-Item -ItemType Directory -Force -Path (Join-Path $tpl 'src'), (Join-Path $fg 'empty-bin') | Out-Null
        "[package]`nname = `"{{project-name}}`"`nversion = `"0.1.0`"`nedition = `"2021`"`n`n[dependencies]`n" | Set-Content -LiteralPath (Join-Path $tpl 'Cargo.toml') -NoNewline -Encoding ascii
        "fn main() {`n    println!(`"Hello from {{project-name}}`");`n}`n" | Set-Content -LiteralPath (Join-Path $tpl 'src\main.rs') -NoNewline -Encoding ascii
        "[template]`ncargo_generate_version = `">=0.10.0`"`n" | Set-Content -LiteralPath (Join-Path $tpl 'cargo-generate.toml') -NoNewline -Encoding ascii
        Push-Location $tpl
        try { & git init -q; & git checkout -q -b main; & git add -A; & git -c user.name=gate -c user.email=gate@example.invalid commit -q -m 'functional gate template' } finally { Pop-Location }
        $env:PATH = "$(Join-Path $fg 'empty-bin');$($Libgit2.Dir);$env:SystemRoot\System32"
        $created = Join-Path $fg 'created\gate-app'
        try {
            $newLog = Join-Path $fg 'dx-new.log'
            & (Join-Path $fg "bin\$($Exes[0])") new --template "file:///$($tpl -replace '\\', '/')" --branch main --yes --vcs none $created *> $newLog
            $rc = $LASTEXITCODE
        } finally { $env:PATH = $savedPath }
        if ($rc -eq 0 -and (Select-String -Path (Join-Path $created 'Cargo.toml') -Pattern 'name = "gate-app"' -SimpleMatch -Quiet)) { Pass 'dx new from a local git template (no git on PATH): the project was cloned by libgit2 and filled in' }
        else { Fail "dx new from the local template: exit $rc (log: $newLog)" }
    } else { Fail 'git is not installed; it is needed to make the local template repository (winget install Git.Git)' }
}

$Gate | Set-Content -LiteralPath (Join-Path $Out 'GATE.txt') -Encoding utf8
if ($Failed) {
    Write-Host "VERIFICATION FAILED for $Rid. output\$Rid is left for inspection but must not be adopted."
    exit 1
}

# ---------------------------------------------------------------------------------------------
# Record. The gzip size from .NET's GZipStream is NOT comparable with `gzip -9` on Linux;
# measure the shipped files with gzip -9 on Linux for ..\BUILD-PROVENANCE.txt.
# ---------------------------------------------------------------------------------------------
$names = $Exes + $LicenceNames
$sums = foreach ($n in $names) { "$((Get-FileHash -Algorithm SHA256 (Join-Path $Out $n)).Hash.ToLowerInvariant())  $n" }
$sums | Set-Content -LiteralPath (Join-Path $Out 'SHA256SUMS.txt') -Encoding ascii
$info = @(
    'dx and wasm-bindgen - build information',
    '==============================================================================',
    "RID              : $Rid ($Triple)",
    "Built            : $($Started.ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) UTC",
    "Build duration   : dx $($BuildSeconds[$Pins.DX_DIR])s, wasm-bindgen $($BuildSeconds[$Pins.WASM_BINDGEN_DIR])s",
    "Built by         : tools/dx-native-tools/windows/build.dx.ps1",
    "Build host       : $([Environment]::OSVersion.VersionString), $env:PROCESSOR_ARCHITECTURE, $env:COMPUTERNAME",
    "Visual Studio    : $VsPath",
    "Rust             : $($Rust.Rustc) / $($Rust.Cargo)",
    "Sources          : $($Pins.TOOL_SOURCES_DIR) (dioxus-cli $($Pins.DX_CRATE_VERSION), upstream commit $($Pins.DX_GIT_SHA), the recorded patches; wasm-bindgen-cli $($Pins.WASM_BINDGEN_VERSION))",
    "libgit2          : $($Pins.LIBGIT2_VERSION) from $($Pins.LIBGIT2_PACKAGE_ID) $($Pins.LIBGIT2_PACKAGE_VERSION) (nupkg sha256 $($Pins.LIBGIT2_NUPKG_SHA256)), $Libgit2Dll sha256 $($Pins.LIBGIT2_SHA256_WIN_X64), taken from $($Libgit2.From); import library made here from $($Libgit2.Exports) exports, sha256 $($Libgit2.ImportLibSha); not shipped by this build",
    "dx               : cargo build --release --locked --offline --target $Triple --features $($Pins.DX_FEATURES)",
    "wasm-bindgen     : cargo build --release --locked --offline --target $Triple",
    "RUSTFLAGS        : --remap-path-prefix (sources, target, user profile) $($Pins.WINDOWS_RUSTFLAGS)",
    "Functional gate  : $(if ($FunctionalSkipped) { 'SKIPPED (no wasm-opt.exe)' } else { ".wasm $WasmSize bytes shrunk, $SkipSize bytes with DX_SKIP_WASM_OPT" })",
    ''
)
foreach ($exe in $Exes) {
    $p = Join-Path $Out $exe
    $info += "File             : $exe"
    $info += "Size             : $((Get-Item $p).Length) bytes"
    $info += "SHA256           : $((Get-FileHash -Algorithm SHA256 $p).Hash.ToLowerInvariant())"
    $info += ''
}
$info += "Files beside them: $($LicenceNames -join ' ')"
$info += ''
$info += 'Gate (GATE.txt)'
$info += '------------------------------------------------------------------------------'
$info += $Gate
$info | Set-Content -LiteralPath (Join-Path $Out 'BUILD-INFO.txt') -Encoding utf8
Write-Host "Done: $Out"
