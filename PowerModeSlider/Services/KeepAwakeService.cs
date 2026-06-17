using System.Runtime.Versioning;
using KeepAwakeLib;

namespace PowerModeSlider.Services;

/// <summary>
/// Implementation of <see cref="IKeepAwakeService"/> backed by the
/// <see cref="KeepAwake"/> library. Keeps the system and display awake while on.
/// </summary>
/// <remarks>
/// <see cref="KeepAwake"/> ties its execution-state to the calling thread, so all
/// methods must be invoked from the same long-lived thread. This service is
/// resolved as a singleton and driven from the UI thread.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class KeepAwakeService : IKeepAwakeService
{
    /// <inheritdoc/>
    public bool IsAwake { get; private set; }

    /// <inheritdoc/>
    public bool SetAwake(bool awake)
    {
        var success = awake ? KeepAwake.Enable() : KeepAwake.Disable();
        if (success)
        {
            IsAwake = awake;
        }

        return success;
    }
}
