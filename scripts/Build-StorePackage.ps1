#Requires -Version 7.0

[CmdletBinding(DefaultParameterSetName = 'Store')]
param(
    [Parameter(ParameterSetName = 'Store')]
    [string]$IdentityName,

    [Parameter(ParameterSetName = 'Store')]
    [string]$Publisher,

    [Parameter(ParameterSetName = 'Store')]
    [string]$PublisherDisplayName,

    [Parameter(ParameterSetName = 'Store')]
    [string]$DisplayName,

    [Parameter(Mandatory, ParameterSetName = 'LocalValidation')]
    [switch]$LocalValidation,

    [string]$Version,

    [ValidateSet('x64', 'arm64')]
    [string[]]$Architecture = @('x64', 'arm64'),

    [string]$OutputRoot,

    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($LocalValidation) {
    $IdentityName = 'PowerModeSlider.LocalPackagingValidation'
    $Publisher = 'CN=PowerModeSlider Local Packaging Validation'
    $PublisherDisplayName = 'Local packaging validation'
    $DisplayName = 'PowerModeSlider - LOCAL VALIDATION ONLY'
    Write-Warning 'LOCAL VALIDATION ONLY: this fixture is not a reserved Store product. Never submit it.'
}

$identity = [ordered]@{
    IdentityName = $IdentityName
    Publisher = $Publisher
    PublisherDisplayName = $PublisherDisplayName
    DisplayName = $DisplayName
}
$missing = @($identity.Keys | Where-Object { [string]::IsNullOrWhiteSpace($identity[$_]) })
if ($missing.Count -gt 0) {
    throw "Missing Partner Center identity: $($missing -join ', '). Reserve the app name and copy Product identity values; see docs\store-release.md. No Store identity defaults are supplied."
}
foreach ($key in $identity.Keys) {
    $value = $identity[$key]
    if ($value -cne $value.Trim() -or $value -match '[\x00-\x1f]') {
        throw "$key must match Partner Center exactly, without surrounding whitespace or control characters."
    }
    [void][System.Xml.XmlConvert]::VerifyXmlChars($value)
}
if ($IdentityName -cnotmatch '^[A-Za-z0-9.-]{3,50}$') {
    throw 'IdentityName must be the 3-50 character Package/Identity/Name from Partner Center, not the Store product ID.'
}
if ($Publisher -cnotmatch '^CN=.+') {
    throw 'Publisher must be the complete Package/Identity/Publisher distinguished name from Partner Center, including CN=.'
}
[void][System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new($Publisher)
if ($DisplayName.Length -gt 256 -or $PublisherDisplayName.Length -gt 256) {
    throw 'DisplayName and PublisherDisplayName cannot exceed 256 characters.'
}
if (-not $LocalValidation -and (
    $IdentityName -eq 'a9d9c69a-754e-4826-9387-3dc0e855285c' -or
    $Publisher -eq 'CN=gungaretti' -or
    ($identity.Values -join ' ') -match '(?i)LocalPackagingValidation|LOCAL VALIDATION|YOUR_|REPLACE_ME|CHANGEME|<[^>]+>'
)) {
    throw 'Development, fixture, and placeholder identities are not Store identities. Copy the real reserved product values from Partner Center.'
}
if ($Version -cnotmatch '^[1-9][0-9]{0,4}\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.0$' -or
    @($Version.Split('.') | Where-Object { [int]$_ -gt 65535 }).Count -gt 0) {
    throw 'Version must be Major.Minor.Build.0: major 1-65535, minor/build 0-65535, no leading zeros. The final component is reserved for the Store.'
}
$Architecture = @($Architecture | ForEach-Object { $_.ToLowerInvariant() })
if ($Architecture.Count -eq 0 -or @($Architecture | Select-Object -Unique).Count -ne $Architecture.Count) {
    throw 'Choose one or both distinct architectures: x64, arm64.'
}

if ($ValidateOnly) {
    Write-Host 'Inputs are syntactically valid. Reservation ownership and exact identity still require confirmation in Partner Center.'
    return
}
if (-not $IsWindows) {
    throw 'MSIX packaging requires Windows, the .NET 10 SDK, and Visual Studio MSBuild with the Windows SDK.'
}

$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = if ($LocalValidation) { 'artifacts\store-validation' } else { 'artifacts\store' }
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot, $repoRoot)
$versionRoot = Join-Path $OutputRoot $Version
foreach ($arch in $Architecture) {
    $directory = Join-Path $versionRoot $arch
    if (Test-Path -LiteralPath $directory) {
        throw "Output already exists: $directory. Move it aside or choose a fresh -OutputRoot. Existing files are never deleted or reused as release artifacts."
    }
}

$dotnet = (Get-Command dotnet.exe -CommandType Application -ErrorAction Stop).Source
$sdks = & $dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.')) {
    throw 'Install the .NET 10 SDK before building Store packages.'
}
$msbuildCommand = Get-Command msbuild.exe -CommandType Application -ErrorAction SilentlyContinue
if ($msbuildCommand) {
    $msbuild = $msbuildCommand.Source
} else {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw 'Visual Studio MSBuild was not found. Install the WinUI/.NET desktop build tools and Windows SDK, or use a VS Developer PowerShell.'
    }
    $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\Current\Bin\amd64\MSBuild.exe'
    if ($LASTEXITCODE -ne 0) { throw 'vswhere failed while locating MSBuild.' }
    if (-not $msbuild) {
        $msbuild = & $vswhere -latest -prerelease -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\Current\Bin\amd64\MSBuild.exe'
        if ($LASTEXITCODE -ne 0) { throw 'vswhere failed while locating prerelease MSBuild.' }
    }
    if (-not $msbuild -or -not (Test-Path -LiteralPath $msbuild)) {
        throw '64-bit Visual Studio MSBuild was not found. winapp alone does not provide this Store upload/symbol build path.'
    }
}

function Get-ZipEntry {
    param([System.IO.Compression.ZipArchive]$Archive, [string]$Name)
    # MSIX paths retain source casing, while Windows resolves file names case-insensitively.
    $entries = @($Archive.Entries | Where-Object { $_.FullName -ieq $Name })
    if ($entries.Count -ne 1) { throw "Package must contain exactly one $Name; found $($entries.Count)." }
    return $entries[0]
}

function Read-ZipText {
    param([System.IO.Compression.ZipArchive]$Archive, [string]$Name)
    $entry = Get-ZipEntry $Archive $Name
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Assert-PeArchitecture {
    param([System.IO.Compression.ZipArchive]$Archive, [string]$Name, [string]$Arch)
    $stream = (Get-ZipEntry $Archive $Name).Open()
    $buffer = [System.IO.MemoryStream]::new()
    try {
        $stream.CopyTo($buffer)
        $reader = [System.IO.BinaryReader]::new($buffer, [System.Text.Encoding]::UTF8, $true)
        try {
            $buffer.Position = 0x3c
            $buffer.Position = $reader.ReadInt32()
            if ($reader.ReadUInt32() -ne 0x00004550) { throw "$Name is not a PE binary." }
            $expected = if ($Arch -eq 'x64') { 0x8664 } else { 0xaa64 }
            if ($reader.ReadUInt16() -ne $expected) { throw "$Name does not contain native $Arch code." }
        } finally { $reader.Dispose() }
    } finally {
        $stream.Dispose()
        $buffer.Dispose()
    }
}

foreach ($arch in $Architecture) {
    $directory = Join-Path $versionRoot $arch
    $packagesDirectory = Join-Path $directory 'packages'
    [void](New-Item -ItemType Directory -Path $packagesDirectory)
    $manifestPath = Join-Path $directory 'Package.Store.appxmanifest'
    [xml]$source = Get-Content -LiteralPath (Join-Path $repoRoot 'PowerModeSlider\Package.appxmanifest') -Raw
    $source.Package.Identity.SetAttribute('Name', $IdentityName)
    $source.Package.Identity.SetAttribute('Publisher', $Publisher)
    $source.Package.Identity.SetAttribute('Version', $Version)
    $source.Package.Properties.DisplayName = $DisplayName
    $source.Package.Properties.PublisherDisplayName = $PublisherDisplayName
    $source.Package.Dependencies.TargetDeviceFamily.SetAttribute('MinVersion', '10.0.22000.0')
    $source.Package.Dependencies.TargetDeviceFamily.SetAttribute('MaxVersionTested', '10.0.22000.0')
    foreach ($element in $source.SelectNodes("//*[local-name()='VisualElements' or local-name()='StartupTask']")) {
        $element.SetAttribute('DisplayName', $DisplayName)
    }
    $source.Save($manifestPath)

    # .NET's artifacts layout isolates restore, XAML codegen, and outputs for every project/architecture.
    $arguments = @(
        (Join-Path $repoRoot 'PowerModeSlider\PowerModeSlider.csproj'),
        '-restore', '-nologo', '-verbosity:minimal',
        '-p:Configuration=Release',
        "-p:Platform=$arch",
        "-p:RuntimeIdentifier=win-$arch",
        '-p:StorePackaging=true',
        "-p:StoreManifestPath=$manifestPath",
        '-p:UseArtifactsOutput=true',
        "-p:ArtifactsPath=$(Join-Path $directory 'build')",
        "-p:AppxPackageDir=$packagesDirectory\",
        '-p:ContinuousIntegrationBuild=true',
        '-p:TreatWarningsAsErrors=true'
    )
    & $msbuild @arguments
    if ($LASTEXITCODE -ne 0) { throw "Store packaging failed for $arch (MSBuild exit $LASTEXITCODE)." }

    $uploads = @(Get-ChildItem -LiteralPath $packagesDirectory -Recurse -File -Filter '*.msixupload')
    if ($uploads.Count -ne 1) {
        throw "Expected exactly one .msixupload in $packagesDirectory; found $($uploads.Count). Raw MSIX files are not a substitute."
    }
    $upload = [System.IO.Compression.ZipFile]::OpenRead($uploads[0].FullName)
    try {
        $packageEntries = @($upload.Entries | Where-Object { $_.Name -like '*.msix' })
        $symbolEntries = @($upload.Entries | Where-Object { $_.Name -like '*.appxsym' })
        if ($packageEntries.Count -ne 1 -or $symbolEntries.Count -ne 1 -or $upload.Entries.Count -ne 2) {
            throw 'The Store upload must contain exactly one MSIX and one SDK-generated .appxsym symbol archive.'
        }
        $packageStream = $packageEntries[0].Open()
        $package = [System.IO.Compression.ZipArchive]::new($packageStream, [System.IO.Compression.ZipArchiveMode]::Read)
        try {
            $manifestText = Read-ZipText $package 'AppxManifest.xml'
            [xml]$manifest = $manifestText
            $actual = $manifest.Package.Identity
            if ($actual.Name -cne $IdentityName -or $actual.Publisher -cne $Publisher -or
                $actual.Version -cne $Version -or $actual.ProcessorArchitecture -cne $arch -or
                $manifest.Package.Properties.DisplayName -cne $DisplayName -or
                $manifest.Package.Properties.PublisherDisplayName -cne $PublisherDisplayName) {
                throw 'The generated package identity, version, architecture, or display names do not match the requested inputs.'
            }
            $family = $manifest.Package.Dependencies.TargetDeviceFamily
            if ($family.Name -cne 'Windows.Desktop' -or $family.MinVersion -cne '10.0.22000.0') {
                throw 'Store packages must target Windows 11 desktop (build 22000 or later).'
            }
            $dependencies = @($manifest.SelectNodes("//*[local-name()='PackageDependency']") | ForEach-Object {
                [ordered]@{ Name = $_.GetAttribute('Name'); Publisher = $_.GetAttribute('Publisher'); MinVersion = $_.GetAttribute('MinVersion') }
            })
            foreach ($dependency in $dependencies) {
                if ($dependency.Name -notin @('Microsoft.VCLibs.140.00.UWPDesktop', 'Microsoft.VCLibs.140.00')) {
                    throw "Unexpected framework dependency '$($dependency.Name)'. The Store build must include .NET and Windows App SDK."
                }
            }
            $requiredFiles = @(
                'PowerModeSlider.exe', 'PowerModeSlider.dll', 'PowerModeLib.dll', 'KeepAwakeLib.dll',
                'PowerModeSlider.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
                'Microsoft.UI.Xaml.dll', 'resources.pri',
                'Assets/PowerBalanced.ico', 'Assets/PowerEfficiency.ico', 'Assets/PowerPerformance.ico',
                'Assets/PowerBalancedAwake.ico', 'Assets/PowerEfficiencyAwake.ico', 'Assets/PowerPerformanceAwake.ico'
            )
            foreach ($file in $requiredFiles) {
                [void](Get-ZipEntry $package $file)
            }
            foreach ($file in @('PowerModeSlider.exe', 'coreclr.dll', 'Microsoft.UI.Xaml.dll')) {
                Assert-PeArchitecture $package $file $arch
            }
            $runtime = Read-ZipText $package 'PowerModeSlider.runtimeconfig.json' | ConvertFrom-Json -AsHashtable
            if ($runtime.runtimeOptions.ContainsKey('framework') -or $runtime.runtimeOptions.ContainsKey('frameworks') -or
                -not $runtime.runtimeOptions.ContainsKey('includedFrameworks')) {
                throw 'The packaged .NET runtime configuration is framework-dependent, not self-contained.'
            }
            if ($null -ne $package.GetEntry('AppxSignature.p7x') -or
                @($package.Entries | Where-Object { $_.Name -match '\.(pfx|p12|cer)$' }).Count -gt 0) {
                throw 'Store upload packages must be unsigned and must not contain signing certificates.'
            }
            if (@($package.Entries | Where-Object { $_.Name -like '*.pdb' }).Count -gt 0) {
                throw 'Debug symbols belong in the upload symbol archive, not the installed MSIX.'
            }
            $startup = $manifest.SelectSingleNode("//*[local-name()='StartupTask' and @TaskId='PowerModeSliderStartup']")
            $fullTrust = $manifest.SelectSingleNode("//*[local-name()='Capability' and @Name='runFullTrust']")
            if ($null -eq $startup -or $startup.GetAttribute('Enabled') -cne 'false' -or $null -eq $fullTrust) {
                throw 'The package must retain runFullTrust and the disabled-by-default startup task.'
            }
            $manifestText | Set-Content -LiteralPath (Join-Path $directory 'AppxManifest.xml') -Encoding utf8
            $payloadCount = $package.Entries.Count
        } finally {
            $package.Dispose()
            $packageStream.Dispose()
        }
        $symbolStream = $symbolEntries[0].Open()
        $symbols = [System.IO.Compression.ZipArchive]::new($symbolStream, [System.IO.Compression.ZipArchiveMode]::Read)
        try {
            if (@($symbols.Entries | Where-Object { $_.Name -eq 'PowerModeSlider.pdb' }).Count -ne 1) {
                throw 'The symbol archive is missing PowerModeSlider.pdb.'
            }
        } finally {
            $symbols.Dispose()
            $symbolStream.Dispose()
        }
    } finally { $upload.Dispose() }

    $prefix = if ($LocalValidation) { 'LOCAL-VALIDATION-ONLY_' } else { '' }
    $uploadPath = Join-Path $directory "${prefix}PowerModeSlider_${Version}_${arch}.msixupload"
    Copy-Item -LiteralPath $uploads[0].FullName -Destination $uploadPath
    [ordered]@{
        LocalValidationOnly = [bool]$LocalValidation
        Identity = $identity
        Version = $Version
        Architecture = $arch
        MinimumWindowsVersion = '10.0.22000.0'
        IncludedDotNetFrameworks = $runtime.runtimeOptions.includedFrameworks
        FrameworkDependencies = $dependencies
        PayloadFileCount = $payloadCount
        UploadFile = [System.IO.Path]::GetFileName($uploadPath)
        SHA256 = (Get-FileHash -LiteralPath $uploadPath -Algorithm SHA256).Hash
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $directory 'package-validation.json') -Encoding utf8
    Write-Host "Package: $uploadPath"
    if ($LocalValidation) {
        Write-Warning 'This is a local packaging fixture, NOT a Store submission package.'
    }
}
