param([string]$Configuration = "Release", [switch]$VerifySessionSelection, [string]$ArtifactsPath = "")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class S2OwnedButton {
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    private delegate bool EnumProc(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int length);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam,
        StringBuilder text, uint flags, uint timeout, out IntPtr result);
    public static IntPtr FindEdit(IntPtr host, uint ownedPid) {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(host, (hwnd, parameter) => {
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity);
            if (pid == ownedPid && name.ToString() == "Edit") { found = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static bool TextRoundtrip(IntPtr edit, string value) {
        IntPtr result;
        if (SendMessageTimeout(edit, 0xC, IntPtr.Zero, new StringBuilder(value), 2, 1000, out result) == IntPtr.Zero || result == IntPtr.Zero)
            return false;
        var text = new StringBuilder(256);
        return SendMessageTimeout(edit, 0xD, (IntPtr)text.Capacity, text, 2, 1000, out result) != IntPtr.Zero
            && text.ToString() == value;
    }
}
'@
$root = Split-Path $PSScriptRoot -Parent
$appExe = Join-Path $root "src\FlexCanvas.App\bin\$Configuration\net8.0-windows\FlexCanvas.App.exe"
$probeExe = Join-Path $root "tools\Probes\Win32Probe\bin\$Configuration\net8.0-windows\FlexCanvas.Win32Probe.exe"
if ($ArtifactsPath) {
    $configurationFolder = $Configuration.ToLowerInvariant()
    $appExe = Join-Path $ArtifactsPath "bin\FlexCanvas.App\$configurationFolder\FlexCanvas.App.exe"
    $probeExe = Join-Path $ArtifactsPath "bin\Win32Probe\$configurationFolder\FlexCanvas.Win32Probe.exe"
}
$nameProperty = [System.Windows.Automation.AutomationElement]::NameProperty
$idProperty = [System.Windows.Automation.AutomationElement]::AutomationIdProperty
$processProperty = [System.Windows.Automation.AutomationElement]::ProcessIdProperty
$confirmationName = -join ([char[]](0x786E, 0x8BA4, 0x5D4C, 0x5165))

function Wait-Until([scriptblock]$Test, [string]$Description) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt 12) {
        $result = & $Test
        if ($result) { return $result }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out: $Description"
}
function Find-Element($Parent, $Property, $Value) {
    $condition = [System.Windows.Automation.PropertyCondition]::new($Property, $Value)
    return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Get-Rows($Grid) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)
    return $Grid.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Invoke-Button($Button) {
    $Button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Find-Confirmation {
    $condition = [System.Windows.Automation.PropertyCondition]::new($processProperty, [int]$app.Id)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $condition)
    foreach ($window in $windows) {
        if ($window.Current.Name -eq $confirmationName -and $window.Current.ClassName -eq "#32770") { return $window }
        $dialog = Find-Element $window $nameProperty $confirmationName
        if ($dialog -and $dialog.Current.ProcessId -eq $app.Id -and $dialog.Current.ClassName -eq "#32770") { return $dialog }
    }
}
function Confirm-Embedding($Dialog, [bool]$Accept) {
    $buttonId = if ($Accept) { "6" } else { "7" }
    $button = Find-Element $Dialog $idProperty $buttonId
    if (-not $button -or $button.Current.ProcessId -ne $app.Id -or
        $button.Current.ClassName -ne "Button" -or $button.Current.NativeWindowHandle -eq 0) {
        throw "Unexpected owned confirmation button"
    }
    # Native MessageBox buttons can have no UIA patterns on this Windows build.
    if (-not [S2OwnedButton]::PostMessage([IntPtr]$button.Current.NativeWindowHandle, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero)) {
        throw "Owned confirmation click failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }
    $null = Wait-Until { -not (Find-Confirmation) } "confirmation dismissed"
}
function Open-Host($Main, $Probe, [bool]$Accept) {
    Write-Host "Selecting owned PID $($Probe.Id); accept=$Accept"
    $refresh = Wait-Until { Find-Element $Main $idProperty "RefreshWindows" } "refresh control rendered"
    $null = Wait-Until { $refresh.Current.IsEnabled } "refresh ready"
    Invoke-Button $refresh
    $search = Find-Element $Main $nameProperty "Search windows"
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("PID $($Probe.Id)")
    $grid = Find-Element $Main $nameProperty "External windows"
    $null = Wait-Until { (Get-Rows $grid).Count -eq 1 } "exact target row"
    $row = @(Get-Rows $grid)[0]
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Invoke-Button (Find-Element $Main $idProperty "EmbedWindow")
    $dialog = Wait-Until { Find-Confirmation } "embedding confirmation"
    Confirm-Embedding $dialog $Accept
    if (-not $Accept) { return }
    $hostWindow = Wait-Until {
        Find-Element ([System.Windows.Automation.AutomationElement]::RootElement) $nameProperty "FlexCanvas Host - PID $($Probe.Id)"
    } "host window"
    $null = Wait-Until { (Find-Element $hostWindow $idProperty "HostStatus").Current.Name.Contains("Embedded") } "real embedded state"
    return $hostWindow
}
$app = $null; $first = $null; $second = $null
try {
    $first = Start-Process $probeExe -PassThru
    $second = Start-Process $probeExe -PassThru
    $null = Wait-Until { $first.Refresh(); $second.Refresh(); $first.MainWindowHandle -ne 0 -and $second.MainWindowHandle -ne 0 } "probes visible"
    $app = Start-Process $appExe -PassThru
    $null = Wait-Until { $app.Refresh(); $app.MainWindowHandle -ne 0 } "app visible"
    $main = [System.Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle)
    $null = Open-Host $main $first $false
    $first.Refresh()
    if ($first.MainWindowHandle -eq 0) { throw "Cancelled confirmation changed target" }
    $hostA = Open-Host $main $first $true
    $hostB = Open-Host $main $second $true
    foreach ($hostWindow in @($hostA, $hostB)) {
        if ([S2OwnedButton]::GetWindow([IntPtr]$hostWindow.Current.NativeWindowHandle, 4) -ne [IntPtr]::Zero) {
            throw "Host still has native ownership and can cover the main window"
        }
        Invoke-Button (Find-Element $hostWindow $idProperty "ReturnToMain")
        $null = Wait-Until {
            [S2OwnedButton]::GetForegroundWindow() -eq [IntPtr]$main.Current.NativeWindowHandle
        } "main foreground without host minimization"
        if ($hostWindow.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState -eq
            [System.Windows.Automation.WindowVisualState]::Minimized) { throw "Returning to main minimized host" }
    }
    if ($VerifySessionSelection) {
        $tab = Find-Element $main $idProperty "HostedSessionsTab"
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Invoke-Button (Find-Element $main $idProperty "RefreshSessions")
        $sessions = Find-Element $main $idProperty "HostedSessions"
        $null = Wait-Until { (Get-Rows $sessions).Count -eq 2 } "two real session rows"
        $row = @(Get-Rows $sessions)[0]
        $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        $selectedId = Wait-Until {
            $text = (Find-Element $main $idProperty "SessionSelectionStatus").Current.Name
            if ($text -match '[0-9a-f]{32}') { return $Matches[0] }
        } "selected session id rendered"
        Invoke-Button (Find-Element $main $idProperty "LocateHost")
        $null = Wait-Until {
            (Find-Element $main $idProperty "SessionSelectionStatus").Current.Name.Contains($selectedId)
        } "selection retained during host lookup"
    }
    $edit = Wait-Until { [S2OwnedButton]::FindEdit([IntPtr]$hostA.Current.NativeWindowHandle, [uint32]$first.Id) } "hosted native edit"
    if (-not [S2OwnedButton]::TextRoundtrip($edit, "S2-owned-probe-input-smoke")) { throw "Hosted control text roundtrip failed" }
    $transform = $hostA.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
    $transform.Resize(950, 700)
    $transform.Move(160, 140)
    Invoke-Button (Find-Element $hostA $idProperty "DetachHost")
    $null = Wait-Until { $first.Refresh(); $first.MainWindowHandle -ne 0 } "first target restored"
    if ($first.HasExited) { throw "Detach killed target" }
    if ($VerifySessionSelection) {
        Invoke-Button (Find-Element $main $idProperty "RefreshSessions")
        $null = Wait-Until { (Get-Rows $sessions).Count -eq 1 } "detached session removed"
        Write-Host "PASS: real session selection, lookup revalidation and detached row removal."
    }
    $main.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (-not $app.WaitForExit(12000)) { throw "Main window shutdown timed out" }
    if ($app.ExitCode -ne 0) { throw "App failed during shutdown" }
    $null = Wait-Until { $second.Refresh(); $second.MainWindowHandle -ne 0 } "second target restored on main close"
    if ($second.HasExited) { throw "Shutdown killed target" }
    Write-Output "PASS: cancel is nonmutating, two real UI hosts, hosted edit roundtrip, host move/resize, explicit detach and main-close restoration; targets remain alive."
} finally {
    if ($app -and -not $app.HasExited) {
        try {
            $dialog = Find-Confirmation
            if ($dialog) { Confirm-Embedding $dialog $false }
        } catch { Write-Warning "Owned confirmation cleanup failed: $_" }
    }
    foreach ($process in @($app, $first, $second)) {
        if ($null -eq $process -or $process.HasExited) { continue }
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(12000)) { Write-Warning "Owned test process did not close; not killed." }
    }
}
