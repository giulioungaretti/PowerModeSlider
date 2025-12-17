using System;

namespace PowerModeSlider.Services;

/// <summary>
/// Abstraction for power mode operations.
/// </summary>
public interface IPowerModeService
{
    /// <summary>
    /// Gets the Best Power Efficiency mode GUID.
    /// </summary>
    Guid BestPowerEfficiency { get; }

    /// <summary>
    /// Gets the Balanced mode GUID.
    /// </summary>
    Guid Balanced { get; }

    /// <summary>
    /// Gets the Best Performance mode GUID.
    /// </summary>
    Guid BestPerformance { get; }

    /// <summary>
    /// Checks if the power mode APIs are supported on the current platform.
    /// </summary>
    bool IsSupported();

    /// <summary>
    /// Gets the current effective power mode.
    /// </summary>
    /// <returns>The current power mode GUID.</returns>
    Guid GetPowerMode();

    /// <summary>
    /// Attempts to set the power mode for both AC and DC states.
    /// </summary>
    /// <param name="modeId">The power mode GUID to set.</param>
    /// <returns>True if the power mode was successfully set.</returns>
    bool TrySetPowerMode(Guid modeId);

    /// <summary>
    /// Gets the power mode for AC (plugged in) state.
    /// </summary>
    Guid GetPowerModeAC();

    /// <summary>
    /// Gets the power mode for DC (battery) state.
    /// </summary>
    Guid GetPowerModeDC();

    /// <summary>
    /// Sets the power mode for AC (plugged in) state.
    /// </summary>
    bool TrySetPowerModeAC(Guid modeId);

    /// <summary>
    /// Sets the power mode for DC (battery) state.
    /// </summary>
    bool TrySetPowerModeDC(Guid modeId);
}
