param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "src\FlexCanvas.App\bin\$Configuration\net8.0-windows\FlexCanvas.App.exe"
function Wait-Until([scriptblock]$Test, [string]$Description) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt 12) {
        $result = & $Test
        if ($result) { return $result }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out: $Description"
}
function Find-Id($Parent, [string]$Id) {
    return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id))
}
function Invoke-Id($Parent, [string]$Id) {
    (Find-Id $Parent $Id).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
$app = $null
try {
    $app = Start-Process $exe -PassThru
    $null = Wait-Until { $app.Refresh(); $app.MainWindowHandle -ne 0 } "owned app visible"
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle)
    $tab = Wait-Until { Find-Id $window "CanvasTab" } "canvas tab"
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $status = Wait-Until { Find-Id $window "CanvasViewportStatus" } "canvas status"
    $initial = $status.Current.Name
    Invoke-Id $window "CanvasZoomIn"
    $null = Wait-Until { $status.Current.Name -ne $initial } "zoom in reflected"
    Invoke-Id $window "CanvasReset"
    $null = Wait-Until { $status.Current.Name -eq $initial } "reset restores initial state"
    $viewport = Find-Id $window "CanvasViewport"
    $scroll = $viewport.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    $scroll.SetScrollPercent(50, 50)
    $null = Wait-Until { $status.Current.Name -ne $initial } "scroll reflected"
    Invoke-Id $window "CanvasReset"
    $null = Wait-Until { $status.Current.Name -eq $initial } "scroll reset"
    for ($i = 0; $i -lt 10; $i++) { Invoke-Id $window "CanvasZoomIn" }
    $null = Wait-Until { -not (Find-Id $window "CanvasZoomIn").Current.IsEnabled } "maximum zoom gate"
    for ($i = 0; $i -lt 15; $i++) { Invoke-Id $window "CanvasZoomOut" }
    $null = Wait-Until { -not (Find-Id $window "CanvasZoomOut").Current.IsEnabled } "minimum zoom gate"
    Invoke-Id $window "CanvasReset"
    $null = Wait-Until { $status.Current.Name -eq $initial } "final reset"
    if (-not $app.CloseMainWindow()) { throw "Owned app normal close failed" }
    if (-not $app.WaitForExit(12000) -or $app.ExitCode -ne 0) { throw "Owned app shutdown failed" }
    "PASS: owned empty canvas tab, zoom/reset, finite scrolling, endpoint gates, normal shutdown. No keyboard/IME/native canvas hosting claim."
} finally {
    if ($app -and -not $app.HasExited) {
        $null = $app.CloseMainWindow()
        if (-not $app.WaitForExit(12000)) { Write-Warning "Owned app did not close normally; not killed." }
    }
}
