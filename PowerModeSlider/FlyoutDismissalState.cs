namespace PowerModeSlider;

internal sealed class FlyoutDismissalState
{
    public bool IsShowing { get; private set; }
    public int Presentation { get; private set; }
    public bool IsTrayPressPending { get; private set; }

    public void Show()
    {
        Presentation++;
        IsShowing = true;
        IsTrayPressPending = false;
    }

    public void Hide()
    {
        Presentation++;
        IsShowing = false;
        IsTrayPressPending = false;
    }

    public void PointerPressed(bool isTrayIcon)
    {
        IsTrayPressPending = isTrayIcon;
    }

    public bool PointerReleased(bool isTrayIcon)
    {
        if (!IsTrayPressPending || isTrayIcon) return false;

        IsTrayPressPending = false;
        return true;
    }

    public bool ShouldHideForTraySelection()
    {
        IsTrayPressPending = false;
        return IsShowing;
    }

    public bool CanDismiss(int presentation) =>
        IsShowing && Presentation == presentation && !IsTrayPressPending;
}
