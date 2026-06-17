# PowerModeSlider

A lightweight Windows 11 system tray application for quickly switching power modes.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 11](https://img.shields.io/badge/Windows-11+-0078D4)
![WinUI 3](https://img.shields.io/badge/WinUI-3-blue)

## Overview

<img width="640" height="275" alt="Screenshot 2025-12-17 140227" src="https://github.com/user-attachments/assets/d2ece320-20c7-4602-ba8c-1223a64f37a8" />

PowerModeSlider lives in your system tray and provides a simple slider flyout to switch between Windows 11 power modes instantly—no need to dig through Settings.

## Features

- 🔋 **System Tray App** – Runs quietly in the background
- ⚡ **One-Click Access** – Click the tray icon to show/hide the slider
- 🎚️ **Simple Slider UI** – Drag to switch between three power modes
- 🔄 **Dynamic Icon** – Tray icon updates to reflect current power mode
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

## Project Structure

```
PowerModeSlider/
├── PowerModeSlider/    # WinUI 3 tray application
└── PowerModeLib/       # .NET library for power mode APIs
```


