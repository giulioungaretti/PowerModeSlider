using System;
using System.Runtime.Versioning;
using PowerModeLib;

namespace PowerModeSlider.Services;

/// <summary>
/// Implementation of <see cref="IPowerModeService"/> that wraps the PowerModeSwitcher library.
/// </summary>
[SupportedOSPlatform("windows10.0.22000")]
public class PowerModeService : IPowerModeService
{
    /// <inheritdoc/>
    public Guid BestPowerEfficiency => PowerMode.BestPowerEfficiency;

    /// <inheritdoc/>
    public Guid Balanced => PowerMode.Balanced;

    /// <inheritdoc/>
    public Guid BestPerformance => PowerMode.BestPerformance;

    /// <inheritdoc/>
    public bool IsSupported() => PowerMode.IsSupported();

    /// <inheritdoc/>
    public Guid GetPowerMode()
    {
        // Return AC power mode as the "current" mode (most common scenario when plugged in)
        // Could be enhanced to detect actual power state and return appropriate mode
        return GetPowerModeAC();
    }

    /// <inheritdoc/>
    public bool TrySetPowerMode(Guid modeId)
    {
        // Set both AC and DC modes for consistent behavior
        var acResult = TrySetPowerModeAC(modeId);
        var dcResult = TrySetPowerModeDC(modeId);
        return acResult || dcResult;
    }

    /// <inheritdoc/>
    public Guid GetPowerModeAC() => PowerMode.GetPowerModeAC();

    /// <inheritdoc/>
    public Guid GetPowerModeDC() => PowerMode.GetPowerModeDC();

    /// <inheritdoc/>
    public bool TrySetPowerModeAC(Guid modeId) => PowerMode.TrySetPowerModeAC(modeId);

    /// <inheritdoc/>
    public bool TrySetPowerModeDC(Guid modeId) => PowerMode.TrySetPowerModeDC(modeId);
}
