using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PowerModeSlider;

internal sealed class TrayIconBounds(uint iconId)
{
    private IntPtr _ownerWindow;
    private bool _reportedFailure;

    public bool Contains(int x, int y)
    {
        if (TryGetBounds(out var bounds))
        {
            _reportedFailure = false;
            return bounds.Contains(x, y);
        }

        if (!_reportedFailure)
        {
            Trace.TraceWarning("Unable to locate the tray icon bounds; tray press detection is unavailable.");
            _reportedFailure = true;
        }

        return false;
    }

    private bool TryGetBounds(out ScreenRect bounds)
    {
        if (_ownerWindow != IntPtr.Zero && TryGetBounds(_ownerWindow, out bounds))
        {
            return true;
        }

        // WinUIEx owns the notification icon through a private hidden window.
        // Find its public Shell identifier without depending on library internals.
        _ownerWindow = IntPtr.Zero;
        ScreenRect foundBounds = default;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var processId);
            if (processId == (uint)Environment.ProcessId && TryGetBounds(window, out foundBounds))
            {
                _ownerWindow = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        bounds = foundBounds;
        return _ownerWindow != IntPtr.Zero;
    }

    private bool TryGetBounds(IntPtr window, out ScreenRect bounds)
    {
        var identifier = new NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            Window = window,
            Id = iconId
        };
        return Shell_NotifyIconGetRect(ref identifier, out bounds) == 0;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public Guid Guid;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out ScreenRect bounds);
}
