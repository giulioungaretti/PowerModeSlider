using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using PowerModeSlider.ViewModels;
using WinUIEx;

namespace PowerModeSlider;

/// <summary>
/// PowerModeSlider - Tray-only application with flyout window.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly TrayIcon _trayIcon;
    private readonly FlyoutWindow _flyoutWindow;

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
}

