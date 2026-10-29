# Restarts the Unity editor on this project and waits until it is ready again.
# Why: every domain reload of a long editor session grows slower (TST-037, docs/TESTING.md 3), and a restart brings
# reloads back to about 3 s. The owner allowed agents to run this script on 2026-10-29.
#
# Usage (from the repository root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/restart_unity.ps1 [-WaitExitSeconds 90] [-WaitReadySeconds 600] [-NoStart]
#
# Steps: ask the editor to close (WM_CLOSE, the same as clicking the window's X), force-stop it and its asset import
# workers if it has not exited after -WaitExitSeconds, reopen the project with Unity 6000.4.0f1 (it reopens the last
# scene), then wait until the window title shows the project and Editor.log has been quiet for -QuietSeconds.
# Never run it while a scenario batch is queued (ScenarioBatchDriver keeps its queue in SessionState) or while an agent
# is in the middle of a Unity call. After it, check through the Unity MCP that Application.dataPath is
# F:/DNDPrototype/Assets and MainScene is the active scene.
#
# Exit codes: 0 ready, 1 the old editor could not be stopped, 2 the new editor was not ready in time, 3 start failed.
# The editor's command line holds a Hub access token, so this script prints process ids only, never command lines.

param(
    [int]$WaitExitSeconds = 90,
    [int]$WaitReadySeconds = 600,
    [int]$QuietSeconds = 15,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$project = 'F:\DNDPrototype'
$editor = 'F:\Unity\6000.4.0f1\Editor\Unity.exe'
$editorLog = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
$projectPattern = '(?i)-projectpath"?\s+"?F:[\\/]DNDPrototype\b'

function Get-ProjectUnityProcesses {
    Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine -match $projectPattern } |
        ForEach-Object {
            [pscustomobject]@{
                Id = [int]$_.ProcessId
                IsWorker = ($_.CommandLine -match '(?i)-batchMode')
            }
        }
}

function Write-Step([string]$text) {
    Write-Output ("[restart_unity] {0:HH:mm:ss} {1}" -f (Get-Date), $text)
}

# 1. Stop the running editor.
$running = @(Get-ProjectUnityProcesses)
$main = @($running | Where-Object { -not $_.IsWorker })
if ($main.Count -eq 0) {
    Write-Step 'no editor is open on F:\DNDPrototype'
} else {
    foreach ($p in $main) {
        Write-Step "asking editor pid $($p.Id) to close"
        $proc = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
        if ($proc) { [void]$proc.CloseMainWindow() }
    }
    $deadline = (Get-Date).AddSeconds($WaitExitSeconds)
    while ((Get-Date) -lt $deadline -and @(Get-ProjectUnityProcesses | Where-Object { -not $_.IsWorker }).Count -gt 0) {
        Start-Sleep -Seconds 2
    }
    $left = @(Get-ProjectUnityProcesses | Where-Object { -not $_.IsWorker })
    if ($left.Count -gt 0) {
        Write-Step "editor still open after $WaitExitSeconds s; force-stopping pid $(($left | ForEach-Object { $_.Id }) -join ', ')"
        foreach ($p in $left) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    } else {
        Write-Step 'editor closed'
    }
}

# Asset import workers normally exit with the editor; stop any that remain.
Start-Sleep -Seconds 3
$workers = @(Get-ProjectUnityProcesses | Where-Object { $_.IsWorker })
foreach ($p in $workers) {
    Write-Step "stopping leftover asset import worker pid $($p.Id)"
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2
if (@(Get-ProjectUnityProcesses).Count -gt 0) {
    Write-Step 'a Unity process on this project is still running'
    exit 1
}

if ($NoStart) {
    Write-Step 'stopped; -NoStart given, not reopening'
    exit 0
}

# 2. Reopen the project.
$started = Get-Date
try {
    $proc = Start-Process -FilePath $editor -ArgumentList @('-projectPath', "`"$project`"") -PassThru
} catch {
    Write-Step "could not start the editor: $($_.Exception.Message)"
    exit 3
}
Write-Step "started editor pid $($proc.Id)"

# 3. Wait until the window shows the project and Editor.log (moved to Editor-prev.log and rewritten at start) has been
#    quiet.
$deadline = $started.AddSeconds($WaitReadySeconds)
$lastLength = -1
$quietSince = $null
$title = ''
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    $live = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if (-not $live) {
        Write-Step 'the new editor exited during start-up'
        exit 3
    }
    $title = $live.MainWindowTitle
    $logItem = Get-Item $editorLog -ErrorAction SilentlyContinue
    # LastWriteTime, not CreationTime: Windows file-name tunneling can carry the old log's creation time over.
    if (-not $logItem -or $logItem.LastWriteTime -lt $started -or $title -notmatch 'DNDPrototype') {
        $quietSince = $null
        continue
    }
    if ($logItem.Length -ne $lastLength) {
        $lastLength = $logItem.Length
        $quietSince = Get-Date
        continue
    }
    if (((Get-Date) - $quietSince).TotalSeconds -ge $QuietSeconds) {
        $reload = Select-String -Path $editorLog -SimpleMatch 'Domain Reload Profiling:' -ErrorAction SilentlyContinue | Select-Object -Last 1
        Write-Step ("ready after {0:N0} s; window: {1}" -f ((Get-Date) - $started).TotalSeconds, $title)
        if ($reload) { Write-Step ("last reload: {0}" -f $reload.Line.Trim()) }
        exit 0
    }
}

Write-Step ("not ready after {0} s; window: {1}" -f $WaitReadySeconds, $title)
exit 2
