#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $AppPath,
    [string] $ResultsPath
)

$ErrorActionPreference = 'Stop'
try {
    if (-not $IsWindows) { throw 'The runtime tests require an interactive Windows desktop.' }
    $AppPath = (Resolve-Path -LiteralPath $AppPath).Path
    if (-not (Test-Path -LiteralPath $AppPath -PathType Leaf) -or [IO.Path]::GetExtension($AppPath) -ne '.exe') {
        throw 'AppPath must identify the executable to test.'
    }
    if ($ResultsPath) {
        $ResultsPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ResultsPath)
        if (-not (Test-Path -LiteralPath (Split-Path $ResultsPath -Parent) -PathType Container)) {
            throw 'The results directory must already exist.'
        }
    }
    $assemblyPath = [IO.Path]::ChangeExtension($AppPath, '.dll')
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw 'Pass a built, unpackaged PowerModeSlider executable with its managed assembly.'
    }

    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public sealed class FlyoutBehaviorFailure : Exception
{
    public FlyoutBehaviorFailure(string message) : base(message) { }
}

public static class FlyoutTestNative
{
    private delegate bool EnumProc(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconIdentifier
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public Guid Guid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Message
    {
        public IntPtr Window;
        public uint Id;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Position;
        public uint Private;
    }

    public sealed class AppWindows
    {
        public IntPtr Flyout;
        public IntPtr Tray;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref IconIdentifier identifier, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetPhysicalCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetPhysicalCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string title,
        uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu,
        IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr window);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetMessage(out Message message, IntPtr window, uint min, uint max);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref Message message);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    public static AppWindows FindAppWindows(uint processId)
    {
        var windows = new AppWindows();
        EnumWindows((window, parameter) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != processId) return true;

            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString() == "Power Mode") windows.Flyout = window;

            var identifier = new IconIdentifier
            {
                Size = (uint)Marshal.SizeOf<IconIdentifier>(), Window = window, Id = 1
            };
            if (Shell_NotifyIconGetRect(ref identifier, out _) == 0) windows.Tray = window;
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static void SelectTray(IntPtr window)
    {
        // WinUIEx 2.9.x notification callback: icon 1, NIN_SELECT.
        if (!PostMessage(window, 0x8765, IntPtr.Zero, new IntPtr((1 << 16) | 0x400)))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Posting tray selection failed.");
    }

    public static void ClickInside(IntPtr window)
    {
        var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            if (!GetWindowRect(window, out var rect))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Reading test window bounds failed.");
            Click(rect.Left + 8, rect.Top + 8);
        }
        finally { SetThreadDpiAwarenessContext(previous); }
    }

    public static void RestoreCursor(Point point)
    {
        if (!SetPhysicalCursorPos(point.X, point.Y))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Restoring the cursor failed.");
    }

    private static void Click(int x, int y)
    {
        if (!SetPhysicalCursorPos(x, y))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Positioning the test cursor failed.");
        mouse_event(2, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(5);
        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
    }
}

public sealed class FlyoutTestWindow : IDisposable
{
    private readonly Thread _thread;
    private uint _threadId;
    private int _error;
    public IntPtr Handle { get; private set; }

    public FlyoutTestWindow(bool canActivate)
    {
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            FlyoutTestNative.SetThreadDpiAwarenessContext(new IntPtr(-4));
            _threadId = FlyoutTestNative.GetCurrentThreadId();
            Handle = FlyoutTestNative.CreateWindowEx(canActivate ? 8u : 0x08000008u,
                "BUTTON", "Flyout regression target", 0x90000000, 50, 50, 500, 150,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (Handle == IntPtr.Zero) _error = Marshal.GetLastWin32Error();
            ready.Set();
            if (Handle == IntPtr.Zero) return;

            int result;
            while ((result = FlyoutTestNative.GetMessage(out var message, IntPtr.Zero, 0, 0)) > 0)
            {
                FlyoutTestNative.TranslateMessage(ref message);
                FlyoutTestNative.DispatchMessage(ref message);
            }
            if (result < 0) _error = Marshal.GetLastWin32Error();
            if (!FlyoutTestNative.DestroyWindow(Handle)) _error = Marshal.GetLastWin32Error();
        }) { IsBackground = true };
        _thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Fixture startup timed out.");
        ready.Dispose();
        if (Handle == IntPtr.Zero) throw new Win32Exception(_error, "Creating the fixture failed.");
    }

    public void Dispose()
    {
        if (_thread.IsAlive &&
            !FlyoutTestNative.PostThreadMessage(_threadId, 0x12, UIntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Stopping the fixture failed.");
        if (!_thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Fixture shutdown timed out.");
        if (_error != 0) throw new Win32Exception(_error, "The fixture message loop failed.");
    }
}
'@
} catch {
    Write-Error "Runtime test setup failed: $($_.Exception.Message)" -ErrorAction Continue
    exit 2
}

$results = [Collections.Generic.List[object]]::new()
$process = $null
$target = $null
$infrastructureError = $null
$originalForeground = [FlyoutTestNative]::GetForegroundWindow()
$originalCursor = [FlyoutTestNative+Point]::new()
if (-not [FlyoutTestNative]::GetPhysicalCursorPos([ref] $originalCursor)) {
    Write-Error 'Reading the original cursor position failed.' -ErrorAction Continue
    exit 2
}

function Assert-Fixture([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw "Invalid runtime fixture: $Message" }
}

function Assert-Behavior([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw [FlyoutBehaviorFailure]::new($Message) }
}

function Wait-Until([scriptblock] $Condition, [int] $TimeoutMs) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.ElapsedMilliseconds -lt $TimeoutMs) {
        if (& $Condition) { return $true }
        Start-Sleep -Milliseconds 2
    }
    return [bool] (& $Condition)
}

function Open-Flyout {
    [FlyoutTestNative]::ClickInside($target.Handle)
    Assert-Fixture (Wait-Until { [FlyoutTestNative]::GetForegroundWindow() -eq $target.Handle } 1000) `
        'the controlled target did not gain foreground.'
    Assert-Fixture (Wait-Until { -not [FlyoutTestNative]::IsWindowVisible($windows.Flyout) } 1000) `
        'the previous presentation could not be reset.'
    Assert-Fixture ([FlyoutTestNative]::AllowSetForegroundWindow($process.Id)) `
        'foreground permission for the synthetic tray callback was denied.'

    $clock = [Diagnostics.Stopwatch]::StartNew()
    [FlyoutTestNative]::SelectTray($windows.Tray)
    Assert-Fixture (Wait-Until {
        [FlyoutTestNative]::IsWindowVisible($windows.Flyout) -and
        [FlyoutTestNative]::GetForegroundWindow() -eq $windows.Flyout
    } 5000) 'the tray callback did not open a visible, active flyout.'
    return $clock
}

function Test-ActivationLoss([bool] $Early) {
    $clock = Open-Flyout
    if ($Early) {
        Start-Sleep -Milliseconds 20
        Assert-Fixture ($clock.ElapsedMilliseconds -lt 125) `
            'opening was too slow to exercise the original 150 ms arming gap.'
    } else {
        Start-Sleep -Milliseconds 200
    }

    Assert-Fixture ([FlyoutTestNative]::IsWindowVisible($windows.Flyout) -and
        [FlyoutTestNative]::GetForegroundWindow() -eq $windows.Flyout) `
        'the flyout lost activation before the controlled focus switch.'
    Assert-Fixture ([FlyoutTestNative]::SetForegroundWindow($target.Handle)) 'foreground switching was denied.'
    $lossAfterMs = $clock.ElapsedMilliseconds
    if ($Early) {
        Assert-Fixture ($lossAfterMs -lt 150) 'the actual foreground switch missed the 150 ms arming gap.'
    }
    $dismissed = Wait-Until { -not [FlyoutTestNative]::IsWindowVisible($windows.Flyout) } 500
    Assert-Fixture ([FlyoutTestNative]::GetForegroundWindow() -eq $target.Handle) `
        'foreground changed unexpectedly during the assertion.'
    Assert-Behavior $dismissed "Flyout stayed visible after focus loss at ${lossAfterMs} ms."
    return @{ FocusLossAfterMs = $lossAfterMs }
}

function Invoke-Regression([string] $Name, [scriptblock] $Test) {
    try {
        $details = & $Test
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Details = $details })
        Write-Host "PASS $Name"
    } catch [FlyoutBehaviorFailure] {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Failure = $_.Exception.Message })
        Write-Host "FAIL $Name - $($_.Exception.Message)"
    }
}

try {
    $target = [FlyoutTestWindow]::new($true)
    $process = Start-Process -FilePath $AppPath -PassThru
    $windows = $null
    Assert-Fixture (Wait-Until {
        $process.Refresh()
        if ($process.HasExited) { throw "App exited during startup with code $($process.ExitCode)." }
        $script:windows = [FlyoutTestNative]::FindAppWindows($process.Id)
        $windows.Flyout -ne [IntPtr]::Zero -and $windows.Tray -ne [IntPtr]::Zero
    } 10000) 'the app windows and tray callback owner were not found.'

    # Warm up WinUI/JIT before measuring the short activation-loss window.
    $null = Open-Flyout
    [FlyoutTestNative]::SelectTray($windows.Tray)
    Assert-Fixture (Wait-Until { -not [FlyoutTestNative]::IsWindowVisible($windows.Flyout) } 1000) `
        'the warm-up presentation did not close.'

    for ($trial = 1; $trial -le 5; $trial++) {
        Invoke-Regression "Early focus loss $trial" { Test-ActivationLoss $true }
    }
    Invoke-Regression 'Later focus loss' { Test-ActivationLoss $false }

    Invoke-Regression 'Interior click keeps the flyout visible' {
        $null = Open-Flyout
        Start-Sleep -Milliseconds 200
        Assert-Fixture ([FlyoutTestNative]::GetForegroundWindow() -eq $windows.Flyout) `
            'the flyout was not active before the interior click.'
        [FlyoutTestNative]::ClickInside($windows.Flyout)
        Start-Sleep -Milliseconds 100
        Assert-Behavior ([FlyoutTestNative]::IsWindowVisible($windows.Flyout)) 'An interior click dismissed the flyout.'
        Assert-Fixture ([FlyoutTestNative]::GetForegroundWindow() -eq $windows.Flyout) `
            'the interior click did not target the active flyout.'
    }

    Invoke-Regression 'Non-activating outside click dismisses' {
        $null = Open-Flyout
        Start-Sleep -Milliseconds 200
        $passiveTarget = [FlyoutTestWindow]::new($false)
        try {
            Assert-Fixture ([FlyoutTestNative]::GetForegroundWindow() -eq $windows.Flyout) `
                'creating the passive target changed activation.'
            [FlyoutTestNative]::ClickInside($passiveTarget.Handle)
            Assert-Behavior (Wait-Until { -not [FlyoutTestNative]::IsWindowVisible($windows.Flyout) } 500) `
                'A non-activating outside click did not dismiss the flyout.'
        } finally { $passiveTarget.Dispose() }
    }

    Invoke-Regression 'Repeated tray callback toggles' {
        for ($cycle = 1; $cycle -le 5; $cycle++) {
            $null = Open-Flyout
            [FlyoutTestNative]::SelectTray($windows.Tray)
            Assert-Behavior (Wait-Until { -not [FlyoutTestNative]::IsWindowVisible($windows.Flyout) } 500) `
                "Tray selection did not close presentation $cycle."
        }
    }
} catch {
    $infrastructureError = $_.Exception.Message
    Write-Error "Runtime test infrastructure failed: $infrastructureError" -ErrorAction Continue
} finally {
    try {
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
        if ($target) {
            [FlyoutTestNative]::ClickInside($target.Handle)
            if ($originalForeground -ne [IntPtr]::Zero -and
                -not [FlyoutTestNative]::SetForegroundWindow($originalForeground)) {
                Write-Warning 'The original foreground window could not be restored.'
            }
            $target.Dispose()
        }
        [FlyoutTestNative]::RestoreCursor($originalCursor)
    } catch {
        $infrastructureError = "Cleanup failed: $($_.Exception.Message)"
        Write-Error $infrastructureError -ErrorAction Continue
    }
}

$failed = @($results | Where-Object { -not $_.Passed }).Count
$report = [pscustomobject]@{
    HarnessSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    AppAssemblySha256 = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
    Passed = $results.Count - $failed
    Failed = $failed
    InfrastructureError = $infrastructureError
    Tests = $results.ToArray()
}
if ($ResultsPath) {
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
}
Write-Host "Runtime results: $($report.Passed) passed, $failed failed."
if ($infrastructureError) { exit 2 }
if ($failed -gt 0) { exit 1 }
exit 0
