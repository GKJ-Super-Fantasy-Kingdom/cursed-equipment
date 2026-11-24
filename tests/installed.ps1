param(
    [Parameter(Mandatory)] [string] $CopiedSave,
    [string] $GameDir = 'D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom',
    [string] $ProfileDir = "$env:APPDATA\Thunderstore Mod Manager\DataFolder\SuperFantasyKingdom\profiles\GKJ - Controller Test",
    [string] $PluginDll
)
# Run under Windows PowerShell 5.1 for the installed HarmonyX IL reader.
$ErrorActionPreference = 'Stop'
if (!$PluginDll) { $PluginDll = Join-Path $PSScriptRoot "../bin/Release/netstandard2.1/CursedEquipment.dll" }
$core = Join-Path $ProfileDir 'BepInEx/core'
$managed = Join-Path $GameDir 'Super Fantasy Kingdom_Data/Managed'
$refs = @("$managed/Assembly-CSharp.dll","$managed/UnityEngine.CoreModule.dll",
    "$managed/UnityEngine.Physics2DModule.dll","$managed/UnityEngine.dll","$managed/netstandard.dll",
    "$managed/Newtonsoft.Json.dll","$core/0Harmony.dll")
foreach ($ref in $refs) { [void][Reflection.Assembly]::LoadFrom($ref) }
[void][Reflection.Assembly]::LoadFrom("$core/BepInEx.dll")
Add-Type -IgnoreWarnings -Path "$PSScriptRoot/Installed.cs" -ReferencedAssemblies ($refs + 'System.Core.dll')
[Installed]::Run((Resolve-Path -LiteralPath $PluginDll).Path,(Resolve-Path -LiteralPath $CopiedSave).Path)
