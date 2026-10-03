# PowerModeSlider

A lightweight Windows 11 system tray application for quickly switching power modes.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 11](https://img.shields.io/badge/Windows-11+-0078D4)
![WinUI 3](https://img.shields.io/badge/WinUI-3-blue)

## Overview

<img width="640" alt="PowerModeSlider flyout showing the power-mode slider and keep-awake toggle in the system tray" src="docs/flyout.png" />

PowerModeSlider lives in your system tray and provides a simple slider flyout to switch between Windows 11 power modes instantly—no need to dig through Settings.

## Features

- 🔋 **System Tray App** – Runs quietly in the background
- ⚡ **One-Click Access** – Click the tray icon to show/hide the slider
- 🎚️ **Simple Slider UI** – Drag to switch between three power modes
- ☕ **Keep Awake** – Toggle the coffee button to stop the machine sleeping (ON forever / OFF, no timers)
- 🔄 **Dynamic Icon** – Tray icon updates to reflect current power mode, with an amber badge while keep-awake is on
- 🪟 **Native Look** – Uses Windows 11 Acrylic backdrop

## Power Modes

| Mode | Description |
|------|-------------|
| 🔋 Best Power Efficiency | Saves power by reducing performance and brightness |
| ⚖️ Balanced | Full performance when needed, saves power when idle |
| ⚡ Best Performance | Maximum performance and screen brightness |

## How It Works

The app uses the official Windows Power Management APIs (`PowerSetUserConfiguredACPowerMode` / `PowerSetUserConfiguredDCPowerMode`) introduced in Windows 11 to change power modes for both AC (plugged in) and DC (battery) states.

## Requirements

- Windows 11 (Build 22000 or later)
- .NET 10 Runtime

## Usage

1. Launch the app – it minimizes to the system tray
2. **Left-click** the tray icon to open the power slider
3. Drag the slider to your desired power mode
4. **Right-click** the tray icon to exit

## Development

This project supports the [Windows App Development CLI (`winapp`)](https://github.com/microsoft/winappCli), which lets you build and run the packaged WinUI 3 app directly from the terminal — no Visual Studio required.

### Prerequisites

Install the .NET 10 SDK and the `winapp` CLI:

```powershell
winget install Microsoft.DotNet.SDK.10 --source winget
winget install Microsoft.winappcli --source winget
```

### Run (dotnet run)

The project includes the `Microsoft.Windows.SDK.BuildTools.WinApp` NuGet package, which hooks `dotnet run` into `winapp run` automatically. Run from the `PowerModeSlider/` sub-folder, passing a runtime identifier (the packaged app cannot build as the default `AnyCPU`):

```powershell
cd PowerModeSlider
dotnet run -r win-x64
```

This registers a loose-layout package with Windows and launches the app with full package identity.

### Run (manual winapp)

Build first, then invoke `winapp run` pointing at the build output:

```powershell
dotnet build -c Debug -r win-x64
winapp run .\bin\Debug\net10.0-windows10.0.19041.0\win-x64
```

### Package for distribution (MSIX)

Build in Release, then pack and sign. Because the app's `Package.appxmanifest`
declares `Publisher="CN=gungaretti"`, the signing certificate's subject **must
match** that publisher and **must be trusted** on the machine, or Windows refuses
to install the package (`0x800B010A`). `winapp pack` handles this for you — it
reads the manifest, generates a matching development certificate, trusts it, and
signs the package in one step:

```powershell
dotnet build -c Release -r win-x64
winapp pack .\bin\Release\net10.0-windows10.0.19041.0\win-x64 --generate-cert --install-cert
```

Then install the generated `.msix` (e.g. `Add-AppxPackage .\PowerModeSlider_*.msix`).

Prefer to manage the certificate explicitly? Generate one whose publisher matches
the manifest, trust it, then sign with it:

```powershell
winapp cert generate --manifest .\Package.appxmanifest --install --output .\devcert.pfx --if-exists skip
dotnet build -c Release -r win-x64
winapp pack .\bin\Release\net10.0-windows10.0.19041.0\win-x64 --cert .\devcert.pfx
```

Use `winapp cert info .\devcert.pfx` to confirm the certificate subject matches the
manifest publisher before signing.

> **Tip:** Increment the `Version` in `Package.appxmanifest` before re-packing to allow Windows to update the installed package.

### Tests

Run the UI-independent dismissal and tray-toggle regression tests:

```powershell
dotnet test .\PowerModeSlider.Tests\PowerModeSlider.Tests.csproj
```

These tests compile the same dismissal state and screen-bounds code used by the
app, without loading WinUI or changing system power settings. CI runs them
alongside the Windows builds. Actual mouse/touch input and Windows activation
still require runtime checks.

#### Runtime regression tests

`scripts\Test-FlyoutDismissal.ps1` tests a **compiled executable**, without
referencing or modifying its source. On an interactive Windows desktop, build
an unpackaged app for the machine's architecture, then run the harness:

```powershell
dotnet build .\PowerModeSlider\PowerModeSlider.csproj -c Debug -r win-x64 -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true
pwsh -NoProfile -File .\scripts\Test-FlyoutDismissal.ps1 -AppPath .\PowerModeSlider\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\PowerModeSlider.exe -ResultsPath .\runtime-results.json
```

For ARM64, substitute `win-arm64` and `Platform=ARM64`, including the executable
path. The harness launches and stops only its own app instance, uses controlled
native windows, briefly moves the cursor/focus, and restores them afterward.
It exercises five early focus-loss trials, later focus loss, interior clicks,
non-activating outside clicks, and repeated native tray-selection callbacks.
It does not change slider/toggle settings or require replacing an installed app.

Run the **same harness** against separately built unchanged and fixed binaries
to establish red/green regression evidence. Exit code **0** means all nine
runtime cases pass, **1** means behavioral assertions failed, and **2** means
the runtime fixture failed and the run is not valid regression evidence.
Optional JSON results record harness and application assembly hashes.

The harness needs an interactive desktop and is not run on hosted CI. It does
not verify physical tray-icon clicks or touchscreen input; those remain manual
checks. The UI-independent unit tests continue to run in CI.

## Project Structure

```
PowerModeSlider/
├── PowerModeSlider/    # WinUI 3 tray application
├── PowerModeLib/       # .NET library for power mode APIs
├── KeepAwakeLib/       # .NET library for keep-awake (SetThreadExecutionState)
└── PowerModeSlider.Tests/ # UI-independent dismissal regression tests
```
