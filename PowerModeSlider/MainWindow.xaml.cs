using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using PowerModeSlider.ViewModels;
using Windows.ApplicationModel;
using WinUIEx;

namespace PowerModeSlider;

/// <summary>
/// PowerModeSlider - Tray-only application with flyout window.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly TrayIcon _trayIcon;
    private readonly FlyoutWindow _flyoutWindow;

    private const string StartupTaskId = "PowerModeSliderStartup";
    private bool _startupSupported;
    private bool _startupEnabled;
    private bool _startupLocked;

    public PowerModeViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<PowerModeViewModel>();

        InitializeComponent();

        // Create flyout window once (reused for all tray clicks)
        _flyoutWindow = new FlyoutWindow(ViewModel);

        // Create tray icon with initial icon based on current power mode
        var initialIconPath = GetIconPathForMode(ViewModel.SelectedModeIndex);
        _trayIcon = new TrayIcon(1, initialIconPath, "Power Mode Switcher");
        _trayIcon.IsVisible = true;

        // Left-click toggles the flyout window
        _trayIcon.Selected += TrayIcon_Selected;

        // Right-click shows context menu with exit option
        _trayIcon.ContextMenu += TrayIcon_ContextMenu;

        // Listen for power mode changes to update the icon
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Cache the "Start with Windows" state so the context menu can render synchronously
        _ = RefreshStartupStateAsync();

        // Hide main window completely - we only use tray
        this.AppWindow.Hide();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedModeIndex))
        {
            UpdateTrayIcon();
        }
    }

    private string GetIconPathForMode(int modeIndex)
    {
        var iconName = modeIndex switch
        {
            0 => "PowerEfficiency.ico",
            1 => "PowerBalanced.ico",
            2 => "PowerPerformance.ico",
            _ => "PowerBalanced.ico"
        };

        return Path.Combine(AppContext.BaseDirectory, "Assets", iconName);
    }

    private void UpdateTrayIcon()
    {
        var iconPath = GetIconPathForMode(ViewModel.SelectedModeIndex);

        if (File.Exists(iconPath))
        {
            _trayIcon.SetIcon(iconPath);
        }
    }

    private void TrayIcon_Selected(object? sender, TrayIconEventArgs e)
    {
        // Toggle flyout visibility
        if (_flyoutWindow.IsShowing)
        {
            _flyoutWindow.Hide();
        }
        else
        {
            _flyoutWindow.ShowFlyout();
        }
    }

    private void TrayIcon_ContextMenu(object? sender, TrayIconEventArgs e)
    {
        var flyout = new MenuFlyout();

        // Start with Windows toggle (packaged/MSIX builds only)
        if (_startupSupported)
        {
            var startupItem = new ToggleMenuFlyoutItem
            {
                Text = "Start with Windows",
                IsChecked = _startupEnabled,
                IsEnabled = !_startupLocked
            };
            startupItem.Click += async (s, args) => await ToggleStartupAsync(startupItem.IsChecked);
            flyout.Items.Add(startupItem);

            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        // Exit option
        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (s, args) =>
        {
            _trayIcon.Dispose();
            Application.Current.Exit();
        };
        flyout.Items.Add(exitItem);

        e.Flyout = flyout;
    }

    #region Start with Windows

    /// <summary>
    /// Reads the current StartupTask state and caches it so the context menu can
    /// render synchronously. Marks the feature unsupported on unpackaged builds.
    /// </summary>
    private async System.Threading.Tasks.Task RefreshStartupStateAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            _startupEnabled = task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            // Locked when the user (Task Manager) or policy controls the choice.
            _startupLocked = task.State is StartupTaskState.DisabledByUser
                or StartupTaskState.DisabledByPolicy
                or StartupTaskState.EnabledByPolicy;
            _startupSupported = true;
        }
        catch
        {
            // StartupTask is only available for packaged (MSIX) builds.
            _startupSupported = false;
        }
    }

    private async System.Threading.Tasks.Task ToggleStartupAsync(bool enable)
    {
        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (enable)
            {
                await task.RequestEnableAsync();
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
        finally
        {
            // Re-sync the cache so the next menu open reflects the real state
            // (e.g. when the user blocks startup via Task Manager).
            await RefreshStartupStateAsync();
        }
    }

    #endregion
}

