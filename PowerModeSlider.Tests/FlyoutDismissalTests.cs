namespace PowerModeSlider.Tests;

public sealed class FlyoutDismissalTests
{
    [Fact]
    public void PresentationCanDismissImmediatelyAfterOpening()
    {
        var state = new FlyoutDismissalState();
        state.Show();

        Assert.True(state.CanDismiss(state.Presentation));
    }

    [Fact]
    public void HiddenPresentationCannotDismiss()
    {
        var state = new FlyoutDismissalState();
        state.Show();
        var queuedPresentation = state.Presentation;
        state.Hide();

        Assert.False(state.CanDismiss(queuedPresentation));
    }

    [Fact]
    public void QueuedDismissalCannotHideAReopenedFlyout()
    {
        var state = new FlyoutDismissalState();
        state.Show();
        var queuedPresentation = state.Presentation;
        state.Hide();
        state.Show();

        Assert.False(state.CanDismiss(queuedPresentation));
        Assert.True(state.CanDismiss(state.Presentation));
    }

    [Fact]
    public void RefreshingThePresentationAlsoInvalidatesQueuedDismissal()
    {
        var state = new FlyoutDismissalState();
        state.Show();
        var queuedPresentation = state.Presentation;
        state.Show();

        Assert.False(state.CanDismiss(queuedPresentation));
        Assert.True(state.CanDismiss(state.Presentation));
    }

    [Fact]
    public void TrayPressDefersDismissalUntilTheToggleClosesTheFlyout()
    {
        var state = new FlyoutDismissalState();
        state.Show();
        var queuedPresentation = state.Presentation;

        state.PointerPressed(isTrayIcon: true);
        Assert.False(state.CanDismiss(queuedPresentation));
        Assert.False(state.PointerReleased(isTrayIcon: true));
        Assert.False(state.CanDismiss(queuedPresentation));
        Assert.True(state.ShouldHideForTraySelection());
        state.Hide();

        Assert.False(state.IsShowing);
        Assert.False(state.CanDismiss(queuedPresentation));
    }

    [Fact]
    public void TraySelectionOpensAHiddenFlyout()
    {
        var state = new FlyoutDismissalState();

        Assert.False(state.ShouldHideForTraySelection());
        state.Show();

        Assert.True(state.IsShowing);
        Assert.True(state.ShouldHideForTraySelection());
    }

    [Fact]
    public void ReleasingOutsideTheTrayCancelsThePendingToggle()
    {
        var state = new FlyoutDismissalState();
        state.Show();
        state.PointerPressed(isTrayIcon: true);

        Assert.True(state.PointerReleased(isTrayIcon: false));
        Assert.True(state.CanDismiss(state.Presentation));
    }

    [Fact]
    public void UnrelatedPointerReleaseDoesNotCreateATrayInteraction()
    {
        var state = new FlyoutDismissalState();
        state.Show();

        Assert.False(state.PointerReleased(isTrayIcon: true));
        Assert.False(state.IsTrayPressPending);
        Assert.True(state.CanDismiss(state.Presentation));
    }

    [Fact]
    public void NewOutsidePressDoesNotKeepAStaleTrayPressPending()
    {
        var state = new FlyoutDismissalState();
        state.Show();
        state.PointerPressed(isTrayIcon: true);
        state.PointerPressed(isTrayIcon: false);

        Assert.True(state.CanDismiss(state.Presentation));
    }

    [Fact]
    public void ReopeningClearsAnUncompletedTrayPress()
    {
        var state = new FlyoutDismissalState();
        state.Show();
        state.PointerPressed(isTrayIcon: true);
        state.Hide();
        state.Show();

        Assert.True(state.CanDismiss(state.Presentation));
    }

    [Theory]
    [InlineData(100, 200, true)]
    [InlineData(119, 219, true)]
    [InlineData(99, 200, false)]
    [InlineData(100, 199, false)]
    [InlineData(120, 200, false)]
    [InlineData(100, 220, false)]
    public void ScreenBoundsUseExclusiveRightAndBottomEdges(int x, int y, bool expected)
    {
        var bounds = new ScreenRect { Left = 100, Top = 200, Right = 120, Bottom = 220 };

        Assert.Equal(expected, bounds.Contains(x, y));
    }
}
