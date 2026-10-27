param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = Split-Path $PSScriptRoot -Parent
$appExe = Join-Path $root "src\FlexCanvas.App\bin\$Configuration\net8.0-windows\FlexCanvas.App.exe"
$probeExe = Join-Path $root "tools\Probes\Win32Probe\bin\$Configuration\net8.0-windows\FlexCanvas.Win32Probe.exe"

function Wait-Until([scriptblock]$Test, [string]$Description) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt 12) {
        $result = & $Test
        if ($result) { return $result }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out: $Description"
}
function Find-Element($Parent, $Property, [string]$Value) {
    $condition = [System.Windows.Automation.PropertyCondition]::new($Property, $Value)
    return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Get-Rows($Grid) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)
    return $Grid.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

$app = $null
$probe = $null
try {
    $probe = Start-Process $probeExe -PassThru
    $null = Wait-Until { $probe.Refresh(); $probe.MainWindowHandle -ne 0 } "probe visible"
    $app = Start-Process $appExe -PassThru
    $null = Wait-Until { $app.Refresh(); $app.MainWindowHandle -ne 0 } "app visible"
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle)
    $nameProperty = [System.Windows.Automation.AutomationElement]::NameProperty
    $idProperty = [System.Windows.Automation.AutomationElement]::AutomationIdProperty
    $search = Wait-Until { Find-Element $window $nameProperty "Search windows" } "search control"
    $grid = Find-Element $window $nameProperty "External windows"
    $value = $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $value.SetValue("PID $($probe.Id)")
    $null = Wait-Until { (Get-Rows $grid).Count -eq 1 } "one controlled probe row"
    $row = @(Get-Rows $grid)[0]
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $null = Wait-Until {
        $button = Find-Element $window $idProperty "CheckWindow"
        if ($button -and $button.Current.IsEnabled) { return $button }
    } "check enabled"
    $button = Find-Element $window $idProperty "CheckWindow"
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $null = Wait-Until {
        $report = Find-Element $window $idProperty "CompatibilityReport"
        $report.Current.Name.Contains("Medium")
    } "risk report"
    $value.SetValue("no-such-FlexCanvas-window")
    $null = Wait-Until { (Get-Rows $grid).Count -eq 0 } "empty search"
    $status = Find-Element $window $idProperty "OperationStatus"
    if (-not $status.Current.Name.Contains("0")) { throw "Empty result status missing" }
    $value.SetValue("PID $($probe.Id)")
    $null = Wait-Until { (Get-Rows $grid).Count -eq 1 } "restore search"
    $row = @(Get-Rows $grid)[0]
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    if (-not $probe.CloseMainWindow()) { throw "Normal probe close failed" }
    if (-not $probe.WaitForExit(10000)) { throw "Probe close timed out" }
    $null = Wait-Until { (Get-Rows $grid).Count -eq 0 } "exited selected row removed"
    $status = Find-Element $window $idProperty "OperationStatus"
    if (-not $status.Current.Name.Contains("WINDOW_NOT_FOUND")) { throw "Exit result missing" }
    if (-not $app.CloseMainWindow()) { throw "Normal app close failed" }
    if (-not $app.WaitForExit(10000)) { throw "App shutdown timed out" }
    if ($app.ExitCode -ne 0) { throw "App exit code: $($app.ExitCode)" }
    Write-Output "PASS: visible App, search, exact selection, risk report, empty results, selected target exit, normal shutdown."
} finally {
    foreach ($process in @($app, $probe)) {
        if ($null -eq $process -or $process.HasExited) { continue }
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(10000)) { Write-Warning "Owned test process did not close normally; not killed." }
    }
}
