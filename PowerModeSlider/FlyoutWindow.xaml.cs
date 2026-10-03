using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinUIEx;
using PowerModeSlider.ViewModels;

namespace PowerModeSlider;

/// <summary>
/// A flyout-style window that appears above the tray icon.
/// Dismisses on deactivation, with a mouse hook for non-activating outside targets.
/// </summary>
public sealed partial class FlyoutWindow : WindowEx
{
    #region Win32 Interop

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out ScreenRect lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    #endregion

    public PowerModeViewModel ViewModel { get; }

    // Logical (DPI-independent) size of the flyout in device-independent pixels.
    // The window is resized to physical pixels on every show so it scales with the
    // monitor DPI; without this the window stays at 400x90 physical px while WinUI
    // renders the content scaled, clipping it on high-DPI displays.
    private const int LogicalWidth = 400;
    private const int LogicalHeight = 90;

    private readonly FlyoutDismissalState _dismissal = new();
    private TrayIconBounds? _trayIconBounds;
    private IntPtr _mouseHookHandle;
    private LowLevelMouseProc? _mouseProc;
    private IntPtr _windowHandle;

    public FlyoutWindow(PowerModeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // Apply rounded corners to the window
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(_windowHandle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));

        Activated += OnActivated;

        Closed += (s, e) =>
        {
            _dismissal.Hide();
            UninstallMouseHook();
        };
    }

    internal void SetTrayIcon(uint iconId)
    {
        _trayIconBounds = new TrayIconBounds(iconId);
    }

    public void ToggleFromTray()
    {
        if (_dismissal.ShouldHideForTraySelection())
        {
            Hide();
        }
        else
        {
            ShowFlyout();
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated && IsShowing)
        {
            QueueDismissal(requireDeactivation: true);
        }
    }

    /// <summary>
    /// Shows the flyout window, refreshing the UI with current power mode.
    /// </summary>
    public void ShowFlyout()
    {
        _dismissal.Show();

        ViewModel.RefreshCurrentMode();
        CurrentModeText.Text = ViewModel.CurrentModeName;
        PowerModeSlider.Value = ViewModel.SelectedModeIndex;
        KeepAwakeToggle.IsChecked = ViewModel.IsKeepAwake;

        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ResizeForDpi();
        PositionNearTray();
        InstallMouseHook();
        AppWindow.Show();
        SetForegroundWindow(_windowHandle);
    }

    /// <summary>
    /// Hides the flyout window.
    /// </summary>
    public void Hide()
    {
        _dismissal.Hide();
        UninstallMouseHook();
        AppWindow.Hide();
    }

    /// <summary>
    /// Gets whether the flyout is currently visible.
    /// </summary>
    public bool IsShowing => _dismissal.IsShowing;

    #region Mouse Hook for Light-Dismiss

    private void InstallMouseHook()
    {
        if (_mouseHookHandle != IntPtr.Zero) return;

        _mouseProc = MouseHookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(module!.ModuleName), 0);
        if (_mouseHookHandle == IntPtr.Zero)
        {
            Trace.TraceWarning($"Unable to install outside-click hook. Win32 error: {Marshal.GetLastWin32Error()}");
            _mouseProc = null;
        }
    }

    private void UninstallMouseHook()
    {
        if (_mouseHookHandle == IntPtr.Zero) return;
        if (!UnhookWindowsHookEx(_mouseHookHandle))
        {
            Trace.TraceWarning($"Unable to remove outside-click hook. Win32 error: {Marshal.GetLastWin32Error()}");
            return;
        }
        _mouseHookHandle = IntPtr.Zero;
        _mouseProc = null;
    }

    private void QueueDismissal(bool requireDeactivation)
    {
        var presentation = _dismissal.Presentation;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (_dismissal.CanDismiss(presentation) &&
                (!requireDeactivation || GetForegroundWindow() != _windowHandle))
            {
                Hide();
            }
        }))
        {
            Trace.TraceWarning("Unable to queue flyout dismissal.");
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsShowing &&
            (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN || wParam == WM_LBUTTONUP))
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var isTrayIcon = _trayIconBounds?.Contains(hookStruct.pt.X, hookStruct.pt.Y) == true;
            if (wParam == WM_LBUTTONUP)
            {
                if (_dismissal.PointerReleased(isTrayIcon))
                {
                    QueueDismissal(requireDeactivation: true);
                }
            }
            else
            {
                // A tray press belongs to the toggle callback, not light-dismiss.
                _dismissal.PointerPressed(wParam == WM_LBUTTONDOWN && isTrayIcon);
                if (GetWindowRect(_windowHandle, out var rect))
                {
                    if (!rect.Contains(hookStruct.pt.X, hookStruct.pt.Y))
                    {
                        QueueDismissal(requireDeactivation: false);
                    }
                }
                else
                {
                    Trace.TraceWarning($"Unable to read flyout bounds. Win32 error: {Marshal.GetLastWin32Error()}");
                }
            }
        }

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    #endregion

    private void PowerModeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!IsShowing || ViewModel == null) return;
        ViewModel.SelectedModeIndex = (int)e.NewValue;
        CurrentModeText.Text = ViewModel.CurrentModeName;
    }

    private void KeepAwakeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        ViewModel.IsKeepAwake = KeepAwakeToggle.IsChecked == true;
    }

    /// <summary>
    /// Resizes the window to its logical size scaled to the current monitor DPI.
    /// AppWindow works in physical pixels, so we must apply the scale factor
    /// ourselves; otherwise the content (rendered at the monitor scale) is clipped.
    /// </summary>
    private void ResizeForDpi()
    {
        var dpi = GetDpiForWindow(_windowHandle);
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;
        var width = (int)Math.Round(LogicalWidth * scale);
        var height = (int)Math.Round(LogicalHeight * scale);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void PositionNearTray()
    {
        var workArea = Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea;
        var x = workArea.X + workArea.Width - AppWindow.Size.Width - 12;
        var y = workArea.Y + workArea.Height - AppWindow.Size.Height - 12;
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }
}
