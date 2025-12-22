using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PowerModeLib;

/// <summary>
/// Provides methods to get and set Windows power modes using documented Windows APIs.
/// These APIs are available in Windows 11 (build 22000) and later.
/// </summary>
[SupportedOSPlatform("windows10.0.22000")]
public static class PowerMode
{
    /// <summary>
    /// Power mode GUIDs for Windows 11+ power modes.
    /// </summary>
    public static readonly Guid BestPowerEfficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    public static readonly Guid Balanced = new("00000000-0000-0000-0000-000000000000");
    public static readonly Guid BestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");

    /// <summary>
    /// Checks if the current operating system supports the power mode APIs.
    /// </summary>
    /// <returns>True if running on Windows 11 (build 22000) or later</returns>
    public static bool IsSupported()
    {
        return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
    }

    /// <summary>
    /// Sets the user-configured power mode for DC (battery) power state.
    /// </summary>
    /// <param name="id">Power mode GUID (BestPowerEfficiency, Balanced, or BestPerformance)</param>
    /// <returns>True if the power mode was changed, false if it was already set or if the API is not available</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when called on a platform other than Windows 11 or later</exception>
    public static bool TrySetPowerModeDC(Guid id)
    {
        if (!IsSupported())
            return false;

        try
        {
            if (GetPowerModeDC() == id) return false;
            var hResult = PowerSetUserConfiguredDCPowerMode(ref id);
            return hResult == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the user-configured power mode for AC (plugged in) power state.
    /// </summary>
    /// <param name="id">Power mode GUID (BestPowerEfficiency, Balanced, or BestPerformance)</param>
    /// <returns>True if the power mode was changed, false if it was already set or if the API is not available</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when called on a platform other than Windows 11 or later</exception>
    public static bool TrySetPowerModeAC(Guid id)
    {
        if (!IsSupported())
            return false;

        try
        {
            if (GetPowerModeAC() == id) return false;
            var hResult = PowerSetUserConfiguredACPowerMode(ref id);
            return hResult == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the user-configured power mode for DC (battery) power state.
    /// </summary>
    /// <returns>The current power mode GUID for battery state</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when called on a platform other than Windows 11 or later</exception>
    /// <exception cref="InvalidOperationException">Thrown when the API call fails</exception>
    public static Guid GetPowerModeDC()
    {
        if (!IsSupported())
            throw new PlatformNotSupportedException("Power mode APIs require Windows 11 (build 22000) or later");

        var hResult = PowerGetUserConfiguredDCPowerMode(out var id);
        if (hResult != 0)
            throw new InvalidOperationException($"PowerGetUserConfiguredDCPowerMode failed with HRESULT: 0x{hResult:X8}");
        return id;
    }

    /// <summary>
    /// Gets the user-configured power mode for AC (plugged in) power state.
    /// </summary>
    /// <returns>The current power mode GUID for plugged-in state</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when called on a platform other than Windows 11 or later</exception>
    /// <exception cref="InvalidOperationException">Thrown when the API call fails</exception>
    public static Guid GetPowerModeAC()
    {
        if (!IsSupported())
            throw new PlatformNotSupportedException("Power mode APIs require Windows 11 (build 22000) or later");

        var hResult = PowerGetUserConfiguredACPowerMode(out var id);
        if (hResult != 0)
            throw new InvalidOperationException($"PowerGetUserConfiguredACPowerMode failed with HRESULT: 0x{hResult:X8}");
        return id;
    }

    [DllImport("powrprof.dll")]
    private static extern int PowerSetUserConfiguredDCPowerMode(ref Guid ModeGuid);

    [DllImport("powrprof.dll")]
    private static extern int PowerSetUserConfiguredACPowerMode(ref Guid ModeGuid);

    [DllImport("powrprof.dll")]
    private static extern int PowerGetUserConfiguredDCPowerMode(out Guid ModeGuid);

    [DllImport("powrprof.dll")]
    private static extern int PowerGetUserConfiguredACPowerMode(out Guid ModeGuid);
}