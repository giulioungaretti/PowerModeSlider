using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KeepAwakeLib;

/// <summary>
/// Keeps the machine awake indefinitely using the documented Win32
/// <c>SetThreadExecutionState</c> API.
///
/// This is the minimal "ON forever / OFF" subset of the PowerToys Awake module
/// (https://github.com/microsoft/PowerToys/tree/main/src/modules/awake) — there
/// are deliberately no timers, scheduling, or expiry options.
///
/// <para>
/// The execution-state flags set by <see cref="SetThreadExecutionState"/> are
/// scoped to the <b>calling thread</b> and persist until they are cleared or
/// that thread exits. Always call <see cref="Enable"/> and <see cref="Disable"/>
/// from the same long-lived thread (e.g. the app's UI thread).
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public static class KeepAwake
{
    [Flags]
    private enum ExecutionState : uint
    {
        /// <summary>Informs the system the state being set should remain until explicitly cleared.</summary>
        Continuous = 0x80000000,

        /// <summary>Forces the system to be in the working state by resetting the system idle timer.</summary>
        SystemRequired = 0x00000001,

        /// <summary>Forces the display to be on by resetting the display idle timer.</summary>
        DisplayRequired = 0x00000002,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    /// <summary>
    /// Keeps the system awake indefinitely.
    /// </summary>
    /// <param name="keepDisplayOn">
    /// When <see langword="true"/> (default) the display is also kept on; when
    /// <see langword="false"/> only the system is kept awake and the screen may
    /// still turn off.
    /// </param>
    /// <returns><see langword="true"/> if the call succeeded.</returns>
    public static bool Enable(bool keepDisplayOn = true)
    {
        var flags = ExecutionState.Continuous | ExecutionState.SystemRequired;
        if (keepDisplayOn)
        {
            flags |= ExecutionState.DisplayRequired;
        }

        return SetThreadExecutionState(flags) != 0;
    }

    /// <summary>
    /// Clears the keep-awake state, allowing the system to sleep normally again.
    /// </summary>
    /// <returns><see langword="true"/> if the call succeeded.</returns>
    public static bool Disable()
    {
        // Passing ES_CONTINUOUS alone clears the previously set requirements.
        return SetThreadExecutionState(ExecutionState.Continuous) != 0;
    }
}
