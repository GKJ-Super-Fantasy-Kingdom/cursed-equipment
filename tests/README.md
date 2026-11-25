# Local checks

## Installed assemblies

From the repository root, build the release and run the Windows PowerShell 5.1 harness against a copy of a human kingdom checkpoint.

```powershell
dotnet build CursedEquipment.csproj -c Release
powershell -NoProfile -File tests/installed.ps1 -CopiedSave .analysis/save/1_human_game.data
```

The harness reads the checkpoint, round-trips the equipment list in memory, and calls the built plugin using game classes from the installed managed assemblies. Pass `-PluginDll` to check a DLL extracted from the ZIP. It also applies each combat transpiler to the installed IL.

## Private Unity player

### Combined foreground run

Use the published ZIPs for Hold the Line 0.2.3 and Nights of Terror 0.1.3, plus the chosen Cursed Relics and Equipment ZIPs. Pass those four archives to `python tests/live/stage-combined.py` in that order. The stage script uses the private player created by `tests/live/stage.py` and snapshots GKJ - Controller Test into another isolated profile. Build the probe, coordinate the live slot, and launch with `./tests/live/run-combined.ps1`. The runner takes the shared mutex and starts the probe only after verifying the private Unity window is in the foreground. The Continue phase uses the immediately preceding phase's private checkpoint. To replay Continue alone, run `./tests/live/run-combined.ps1 -Phases combined-reload`.

### Equipment-only run

`live/stage.py` prepares a new player from the sibling Hold the Line copied-save fixture. It gives the player its own company/product identity, preferences and save path. The QA plugin checks that identity before running. The test launcher takes the same Windows mutex as the sibling projects and runs this private player in batch mode.

```powershell
python tests/live/stage.py
dotnet build tests/live/Probe.csproj -c Release
./tests/live/run.ps1 -Phase pad-large
./tests/live/run.ps1 -Phase mouse-small
./tests/live/run.ps1 -Phase pad
./tests/live/run.ps1 -Phase mouse
./tests/live/run.ps1 -Phase reload
./tests/live/run.ps1 -Phase content
```

The mouse and controller phases start from a copied checkpoint. The reload phase reuses the native checkpoint from the mouse phase. The fixture requests all three items from the native merchant, so each run can exercise the same choices. Synthetic input events pass through Unity’s Input System. Scene cameras and existing canvases render into an offscreen texture for screenshots. The QA plugin only runs in the private fixture and is excluded from the distribution ZIP.
