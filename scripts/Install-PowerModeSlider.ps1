#Requires -Version 5.1
<#
.SYNOPSIS
    Installs (sideloads) PowerModeSlider from the release assets.

.DESCRIPTION
    The release MSIX is signed with a self-signed certificate. Windows will not
    install it until that certificate is trusted on the machine, otherwise you
    get error 0x800B010A ("the publisher certificate could not be verified").

    This script:
      1. Imports the bundled .cer into LocalMachine\Root and LocalMachine\TrustedPeople
         so the package's signature chains to a trusted root.
      2. Installs the bundled Windows App Runtime dependency (if present).
      3. Installs the PowerModeSlider MSIX that matches this PC's architecture.

    Run it from an elevated (Administrator) PowerShell, from the folder that
    contains the downloaded release assets (.cer + .msix files).

.EXAMPLE
    # Right-click > Run as administrator, or from an elevated prompt:
    powershell -ExecutionPolicy Bypass -File .\Install-PowerModeSlider.ps1
#>
[CmdletBinding()]
param(
    # Folder containing the .cer and .msix files. Defaults to the script's folder.
    [string]$SourceDir = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

function Assert-Admin {
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This script must be run as Administrator (needed to trust the certificate in LocalMachine).'
    }
}

if (-not $SourceDir) { $SourceDir = (Get-Location).Path }
Write-Host "Using source folder: $SourceDir" -ForegroundColor Cyan

Assert-Admin

# 1. Trust the signing certificate ------------------------------------------------
$cer = Get-ChildItem -Path $SourceDir -Filter *.cer -File | Select-Object -First 1
if (-not $cer) { throw "No .cer file found in '$SourceDir'. Download the certificate from the release." }

foreach ($store in 'Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPeople') {
    Write-Host "Importing $($cer.Name) into $store" -ForegroundColor Cyan
    Import-Certificate -FilePath $cer.FullName -CertStoreLocation $store | Out-Null
}

# 2. Install the Windows App Runtime dependency (if shipped alongside) -------------
$runtime = Get-ChildItem -Path $SourceDir -Filter '*WindowsAppRuntime*.msix' -File | Select-Object -First 1
if ($runtime) {
    Write-Host "Installing dependency $($runtime.Name)" -ForegroundColor Cyan
    Add-AppxPackage -Path $runtime.FullName -ErrorAction SilentlyContinue
}

# 3. Install the app package matching this machine's architecture ------------------
$arch = switch ($env:PROCESSOR_ARCHITECTURE) {
    'AMD64' { 'x64' }
    'ARM64' { 'arm64' }
    'x86'   { 'x86' }
    default { $env:PROCESSOR_ARCHITECTURE.ToLower() }
}
Write-Host "Detected architecture: $arch" -ForegroundColor Cyan

$app = Get-ChildItem -Path $SourceDir -Filter 'PowerModeSlider*.msix' -File |
    Where-Object { $_.Name -match "_$arch" } | Select-Object -First 1
if (-not $app) {
    # Fall back to any PowerModeSlider package that is not the runtime.
    $app = Get-ChildItem -Path $SourceDir -Filter 'PowerModeSlider*.msix' -File |
        Where-Object { $_.Name -notmatch 'WindowsAppRuntime' } | Select-Object -First 1
}
if (-not $app) { throw "No PowerModeSlider*.msix found in '$SourceDir'." }

Write-Host "Installing $($app.Name)" -ForegroundColor Cyan
Add-AppxPackage -Path $app.FullName

Write-Host 'PowerModeSlider installed successfully. Find it in the Start menu / system tray.' -ForegroundColor Green
