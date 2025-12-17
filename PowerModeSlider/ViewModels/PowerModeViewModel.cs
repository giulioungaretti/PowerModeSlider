using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerModeSlider.Services;

namespace PowerModeSlider.ViewModels;

public partial class PowerModeViewModel : ObservableObject
{
    private readonly IPowerModeService _powerModeService;

    [ObservableProperty]
    private int _selectedModeIndex;

    [ObservableProperty]
    private string _currentModeName = string.Empty;

    [ObservableProperty]
    private string _currentModeDescription = string.Empty;

    [ObservableProperty]
    private string _currentModeIcon = string.Empty;

    public PowerModeViewModel(IPowerModeService powerModeService)
    {
        _powerModeService = powerModeService;
        RefreshCurrentMode();
    }

    public void RefreshCurrentMode()
    {
        var currentMode = _powerModeService.GetPowerMode();

        if (currentMode == _powerModeService.BestPowerEfficiency)
        {
            SelectedModeIndex = 0;
            CurrentModeName = "Best Power Efficiency";
            CurrentModeDescription = "Extends battery life";
            CurrentModeIcon = "\uE945"; // Battery saver icon
        }
        else if (currentMode == _powerModeService.Balanced)
        {
            SelectedModeIndex = 1;
            CurrentModeName = "Balanced";
            CurrentModeDescription = "Balances performance and battery";
            CurrentModeIcon = "\uEC49"; // Balance icon
        }
        else if (currentMode == _powerModeService.BestPerformance)
        {
            SelectedModeIndex = 2;
            CurrentModeName = "Best Performance";
            CurrentModeDescription = "Maximum performance";
            CurrentModeIcon = "\uE945"; // Performance/speed icon
        }
    }

    partial void OnSelectedModeIndexChanged(int value)
    {
        var targetMode = value switch
        {
            0 => _powerModeService.BestPowerEfficiency,
            1 => _powerModeService.Balanced,
            2 => _powerModeService.BestPerformance,
            _ => _powerModeService.Balanced
        };

        _powerModeService.TrySetPowerMode(targetMode);
        RefreshCurrentMode();
    }

    [RelayCommand]
    private void SetBestEfficiency()
    {
        SelectedModeIndex = 0;
    }

    [RelayCommand]
    private void SetBalanced()
    {
        SelectedModeIndex = 1;
    }

    [RelayCommand]
    private void SetBestPerformance()
    {
        SelectedModeIndex = 2;
    }
}
