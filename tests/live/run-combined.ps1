param(
    [string[]]$Phases = @('combined-pad-large','combined-reload','combined-pad','combined-mouse','combined-mouse-large'),
    [string]$ReloadFrom,
    [switch]$Expansion
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$playerFolder = if ($Expansion) { 'expansion-player' } else { 'combined-player' }
$product = if ($Expansion) { 'Cursed Cards QA 00001' } else { 'Cursed Combo QA 00001' }
$player = (Resolve-Path "$root/.analysis/$playerFolder").Path
$data = "$player/Super Fantasy Kingdom_Data"
$identity = Get-Content "$data/app.info"
if ($identity[0] -cne 'GKJ QA Isolated Lab' -or $identity[1] -cne $product -or
    ![Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$data/globalgamemanagers")).Contains($identity[1])) {
    throw 'Expected private combined player and save identity'
}
$logRoot = Join-Path $root ('.analysis/combined-evidence/' + (Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$save = Join-Path $env:USERPROFILE "AppData/LocalLow/GKJ QA Isolated Lab/$product"
New-Item -ItemType Directory -Force -Path $save | Out-Null
if ((Get-Item -LiteralPath $save).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected save directory' }
$mutex = [Threading.Mutex]::new($false,'Local\SFKControllerRetest')
$held = $false
$failed = $false
if (!('CursedEquipmentTestWindow' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class CursedEquipmentTestWindow {
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint current, uint other, bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    public static void Activate(IntPtr window) {
        uint current = GetCurrentThreadId();
        uint owner = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        bool joined = current != owner && owner != 0 && AttachThreadInput(current, owner, true);
        try {
            ShowWindowAsync(window, 9);
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally { if (joined) AttachThreadInput(current, owner, false); }
    }
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    public static bool IsUnityWindow(IntPtr window) {
        var name = new StringBuilder(128);
        GetClassName(window, name, name.Capacity);
        return name.ToString() == "UnityWndClass";
    }
}
'@
}
try {
    while (!$held) {
        try { $held = $mutex.WaitOne(5000) } catch [Threading.AbandonedMutexException] { $held = $true }
    }
    Copy-Item "$PSScriptRoot/bin/Release/netstandard2.1/Probe.dll" "$player/BepInEx/plugins" -Force
    $config = "$player/BepInEx/config/BepInEx.cfg"
    $settings = Get-Content -LiteralPath $config -Raw
    $settings = [regex]::Replace($settings,'(?ms)(^\[Logging\.Console\][^\[]*?^Enabled = )true','$1false')
    Set-Content -LiteralPath $config -Value $settings -NoNewline
    Get-ChildItem "$player/BepInEx/plugins" -Recurse -Filter '*.dll' | Get-FileHash |
        Format-List | Out-File "$logRoot/staged.txt"
    $lastCaptured = $null
    foreach ($phase in $Phases) {
        $out = Join-Path $logRoot $phase
        New-Item -ItemType Directory -Path $out | Out-Null
        if ($phase -ne 'combined-reload') {
            $before = New-Item -ItemType Directory -Path "$out/previous"
            Get-ChildItem -LiteralPath $save -Filter '*.data*' | Copy-Item -Destination $before.FullName
            Copy-Item "$root/../hold-the-line/.analysis/combined-qa/seed/*.data*" -Destination $save -Force
        } elseif ($ReloadFrom -or $lastCaptured) {
            $inputCheckpoint = if ($ReloadFrom) { $ReloadFrom } else { $lastCaptured }
            $checkpoint = (Resolve-Path -LiteralPath $inputCheckpoint).Path
            $privateEvidence = (Resolve-Path "$root/.analysis/combined-evidence").Path + [IO.Path]::DirectorySeparatorChar
            if (!$checkpoint.StartsWith($privateEvidence,[StringComparison]::OrdinalIgnoreCase) -or
                (Split-Path $checkpoint -Leaf) -ne '1_human_game.data') { throw 'Expected preserved private human checkpoint' }
            $before = New-Item -ItemType Directory -Path "$out/previous"
            Get-ChildItem -LiteralPath $save -Filter '*.data*' | Copy-Item -Destination $before.FullName
            Copy-Item -LiteralPath $checkpoint -Destination $save -Force
            if (Test-Path -LiteralPath "$checkpoint.bak") {
                Copy-Item -LiteralPath "$checkpoint.bak" -Destination $save -Force
            }
            Get-FileHash -LiteralPath $checkpoint | Format-List | Out-File "$out/continue-input.txt"
        }
        $env:SFK_EQUIP_EVIDENCE = $out
        $env:SFK_EQUIP_PHASE = $phase
        $env:SFK_EQUIP_VISIBLE = '1'
        $env:SFK_EQUIP_EXPANSION = if ($Expansion) { '1' } else { '0' }
        $p = Start-Process -FilePath "$player/Super Fantasy Kingdom.exe" -WorkingDirectory $player -WindowStyle Normal -PassThru -ArgumentList "-force-d3d11 -nosound -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile `"$out/player.log`""
        try {
            $until = (Get-Date).AddSeconds(30)
            $visible = $false
            do {
                Start-Sleep -Milliseconds 250
                $p.Refresh()
                $window = $p.MainWindowHandle
                $visible = $window -ne [IntPtr]::Zero -and [CursedEquipmentTestWindow]::IsUnityWindow($window) -and
                    [CursedEquipmentTestWindow]::IsWindowVisible($window) -and ![CursedEquipmentTestWindow]::IsIconic($window)
            } while (!$p.HasExited -and !$visible -and (Get-Date) -lt $until)
            if ($visible) {
                $front = $false
                for ($attempt = 0; $attempt -lt 16 -and !$front -and !$p.HasExited; $attempt++) {
                    [CursedEquipmentTestWindow]::Activate($window)
                    Start-Sleep -Milliseconds 300
                    $front = [CursedEquipmentTestWindow]::GetForegroundWindow() -eq $window
                }
                "VISIBLE $phase PID=$($p.Id) HWND=$window foreground=$front" | Tee-Object "$out/window.txt"
            } else { "Private window could not be confirmed for $phase" | Tee-Object "$out/window.txt" }
            if (!$visible -or !$front) { throw 'Private Unity window could not be brought to the foreground' }
            New-Item -ItemType File -Path "$out/foreground-ready" | Out-Null
            if (!$p.WaitForExit(300000)) { Write-Warning "$phase timed out" }
        } finally {
            if (!$p.HasExited) { $p.Kill(); $p.WaitForExit() }
        }
        Copy-Item "$player/BepInEx/LogOutput.log" "$out/loader.log"
        if (!(Test-Path "$out/$phase.txt") -or !(Select-String -LiteralPath "$out/$phase.txt" -Pattern '^FINISHED$' -Quiet)) {
            Write-Warning "$phase failed. See $out"
            $failed = $true
        }
        if (Test-Path "$out/$phase.txt") { Get-Content "$out/$phase.txt" | Select-Object -Last 18 }
        if (Test-Path -LiteralPath "$out/checkpoint/1_human_game.data") {
            $lastCaptured = "$out/checkpoint/1_human_game.data"
        }
    }
    if ($failed) { throw "A combined phase failed. See $logRoot" }
} finally {
    if ($held) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
