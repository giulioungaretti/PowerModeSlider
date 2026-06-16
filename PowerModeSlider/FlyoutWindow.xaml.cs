using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinUIEx;
using Windows.ApplicationModel;
using PowerModeSlider.ViewModels;

namespace PowerModeSlider;

/// <summary>
/// A flyout-style window that appears above the tray icon.
/// Uses a low-level mouse hook for light-dismiss behavior since WinUI 3
/// doesn't reliably fire deactivation events when clicking outside.
/// </summary>
public sealed partial class FlyoutWindow : WindowEx
{
    #region Win32 Interop

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

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
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    #endregion

    public PowerModeViewModel ViewModel { get; }

    private bool _isShowing;
    private bool _lightDismissEnabled;
    private bool _suppressStartupToggle;
    private readonly DispatcherTimer _activationTimer;
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

        // Timer to delay enabling light-dismiss until window activation settles
        _activationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _activationTimer.Tick += OnActivationTimerTick;

        Closed += (s, e) => UninstallMouseHook();
    }

    private void OnActivationTimerTick(object? sender, object e)
    {
        _activationTimer.Stop();
        _lightDismissEnabled = true;
        InstallMouseHook();
    }

    /// <summary>
    /// Shows the flyout window, refreshing the UI with current power mode.
    /// </summary>
    public void ShowFlyout()
    {
        _isShowing = true;
        _lightDismissEnabled = false;

        ViewModel.RefreshCurrentMode();
        CurrentModeText.Text = ViewModel.CurrentModeName;
        PowerModeSlider.Value = ViewModel.SelectedModeIndex;
        RefreshStartupToggle();

        PositionNearTray();
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        AppWindow.Show();
        SetForegroundWindow(_windowHandle);

        _activationTimer.Start();
    }

    /// <summary>
    /// Hides the flyout window.
    /// </summary>
    public void Hide()
    {
        _activationTimer.Stop();
        UninstallMouseHook();
        _isShowing = false;
        _lightDismissEnabled = false;
        AppWindow.Hide();
    }

    /// <summary>
    /// Gets whether the flyout is currently visible.
    /// </summary>
    public bool IsShowing => _isShowing;

    #region Mouse Hook for Light-Dismiss

    private void InstallMouseHook()
    {
        if (_mouseHookHandle != IntPtr.Zero) return;

        _mouseProc = MouseHookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(module!.ModuleName), 0);
    }

    private void UninstallMouseHook()
    {
        if (_mouseHookHandle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_mouseHookHandle);
        _mouseHookHandle = IntPtr.Zero;
        _mouseProc = null;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _lightDismissEnabled &&
            (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            if (GetWindowRect(_windowHandle, out var rect) &&
                (hookStruct.pt.X < rect.Left || hookStruct.pt.X > rect.Right ||
                 hookStruct.pt.Y < rect.Top || hookStruct.pt.Y > rect.Bottom))
            {
                DispatcherQueue.TryEnqueue(Hide);
            }
        }

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    #endregion

    private void PowerModeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isShowing || ViewModel == null) return;
        ViewModel.SelectedModeIndex = (int)e.NewValue;
        CurrentModeText.Text = ViewModel.CurrentModeName;
    }

    #region Start with Windows

    private const string StartupTaskId = "PowerModeSliderStartup";

    /// <summary>
    /// Reflects the current StartupTask state in the toggle without firing the
    /// Toggled handler. Also disables the toggle when the choice is locked by the
    /// user (via Task Manager) or by policy.
    /// </summary>
    private async void RefreshStartupToggle()
    {
        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            _suppressStartupToggle = true;
            StartupToggle.IsOn = task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            StartupToggle.IsEnabled = task.State is StartupTaskState.Enabled or StartupTaskState.Disabled;
            _suppressStartupToggle = false;
        }
        catch
        {
            // StartupTask is only available for packaged (MSIX) builds; hide otherwise.
            StartupToggle.Visibility = Visibility.Collapsed;
        }
    }

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressStartupToggle) return;

        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (StartupToggle.IsOn)
            {
                var state = await task.RequestEnableAsync();
                if (state is not (StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy))
                {
                    // The user disabled it in Task Manager, or policy blocks it: revert the UI.
                    _suppressStartupToggle = true;
                    StartupToggle.IsOn = false;
                    _suppressStartupToggle = false;
                }
            }
            else
            {
                task.Disable();
            }
        }
        catch
        {
            // Ignore: unsupported in unpackaged builds.
        }
    }

    #endregion

    private void PositionNearTray()
    {
        var workArea = Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea;
        var x = workArea.X + workArea.Width - AppWindow.Size.Width - 12;
        var y = workArea.Y + workArea.Height - AppWindow.Size.Height - 12;
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }
}
