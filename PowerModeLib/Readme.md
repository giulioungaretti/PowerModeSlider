# PowerModeSwitcher

A simple .NET library for controlling Windows 11 power modes using official documented APIs.

## Overview

This library provides a clean interface to get and set Windows power modes programmatically using the documented Windows Power Management APIs introduced in Windows 11.

## Features

- ✅ Uses **official documented Windows APIs**
- ✅ Separate control for AC (plugged in) and DC (battery) power states
- ✅ Simple, clean API with no dependencies
- ✅ Works on Windows 11 and later

## Installation

Add the library to your project by referencing the compiled DLL or including the source code directly.

## Usage

```csharp
using PowerModeSwitcher;

// Set power mode for battery state (DC)
PowerMode.TrySetPowerModeDC(PowerMode.BestPowerEfficiency);

// Set power mode for plugged-in state (AC)
PowerMode.TrySetPowerModeAC(PowerMode.BestPerformance);

// Get current power modes
var dcMode = PowerMode.GetPowerModeDC();
var acMode = PowerMode.GetPowerModeAC();
```

## Available Power Modes

The library provides three power mode GUIDs:

- **BestPowerEfficiency** (`961cc777-2547-4f9d-8174-7d86181b8a7a`): Saves power by reducing PC performance and screen brightness
- **Balanced** (`00000000-0000-0000-0000-000000000000`): Offers full performance when needed and saves power when not in use (default)
- **BestPerformance** (`ded574b5-45a0-4f42-8737-46345c09c238`): Maximizes performance and screen brightness

## API Reference

### Methods

- `TrySetPowerModeDC(Guid)` - Sets power mode for DC (battery) state
- `TrySetPowerModeAC(Guid)` - Sets power mode for AC (plugged-in) state
- `GetPowerModeDC()` - Gets current power mode for DC state
- `GetPowerModeAC()` - Gets current power mode for AC state

## Technical Details

This library uses the following documented Windows APIs:
- `PowerSetUserConfiguredDCPowerMode` - [Documentation](https://learn.microsoft.com/en-us/windows/win32/power/power-management-functions)
- `PowerSetUserConfiguredACPowerMode` - [Documentation](https://learn.microsoft.com/en-us/windows/win32/power/power-management-functions)
- `PowerGetUserConfiguredDCPowerMode` - [Documentation](https://learn.microsoft.com/en-us/windows/win32/power/power-management-functions)
- `PowerGetUserConfiguredACPowerMode` - [Documentation](https://learn.microsoft.com/en-us/windows/win32/power/power-management-functions)

## Requirements

- .NET 8.0 or later
- Windows 11 or later

## License

MIT License