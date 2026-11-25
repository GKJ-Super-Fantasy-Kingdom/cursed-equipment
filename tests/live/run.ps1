param([ValidateSet('mouse','mouse-small','pad','pad-large','reload','content','roster','roster-large')] [string]$Phase = 'pad')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$player = (Resolve-Path "$root/.analysis/player").Path
$data = "$player/Super Fantasy Kingdom_Data"
$identity = Get-Content "$data/app.info"
if ($identity[0] -cne 'GKJ QA Isolated Lab' -or $identity[1] -cne 'Cursed Equip QA 00001' -or
    ![Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$data/globalgamemanagers")).Contains($identity[1])) {
    throw 'Expected private player and private save identity'
}
$mutex = [Threading.Mutex]::new($false,'Local\SFKControllerRetest')
$held = $false
$p = $null
try {
    while (!$held) {
        try { $held = $mutex.WaitOne(5000) } catch [Threading.AbandonedMutexException] { $held = $true }
    }
    $out = "$root/.analysis/evidence/$Phase-$(Get-Date -Format yyyyMMdd-HHmmss)"
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    if ($Phase -ne 'reload') {
        $save = Join-Path $env:USERPROFILE 'AppData/LocalLow/GKJ QA Isolated Lab/Cursed Equip QA 00001'
        if ((Get-Item -LiteralPath $save).Attributes -band [IO.FileAttributes]::ReparsePoint) {throw 'Unexpected save directory'}
        $previous = New-Item -ItemType Directory -Path "$out/before"
        Copy-Item "$save/*.data" -Destination $previous.FullName
        Copy-Item "$root/../hold-the-line/.analysis/combined-qa/seed/*.data" -Destination $save
    }
    Copy-Item "$root/bin/Release/netstandard2.1/CursedEquipment.dll","$PSScriptRoot/bin/Release/netstandard2.1/Probe.dll" "$player/BepInEx/plugins" -Force
    $env:SFK_EQUIP_EVIDENCE = $out
    $env:SFK_EQUIP_PHASE = $Phase
    $p = Start-Process -FilePath "$player/Super Fantasy Kingdom.exe" -WorkingDirectory $player -WindowStyle Hidden -PassThru -ArgumentList "-batchmode -force-d3d11 -nosound -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile `"$out/player.log`""
    if (!$p.WaitForExit(110000)) { $p.Kill(); $p.WaitForExit(); throw 'Private player timed out' }
    Copy-Item "$player/BepInEx/LogOutput.log" "$out/loader.log"
    Get-Content "$out/$Phase.txt"
    if (!(Select-String -LiteralPath "$out/$Phase.txt" -Pattern '^FINISHED$' -Quiet)) { throw 'Private playtest failed' }
} finally {
    if ($p -and !$p.HasExited) { $p.Kill(); $p.WaitForExit() }
    if ($held) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
