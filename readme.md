```markdown
# PowerModeSlider

A lightweight Windows 11 system tray application for quickly switching power modes.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 11](https://img.shields.io/badge/Windows-11+-0078D4)
![WinUI 3](https://img.shields.io/badge/WinUI-3-blue)

## Overview

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

## Project Structure

```
PowerModeSlider/
├── PowerModeSlider/    # WinUI 3 tray application
└── PowerModeLib/       # .NET library for power mode APIs
```


