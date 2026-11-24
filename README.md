# Cursed Equipment

Three rare pieces of equipment join the Blacksmith’s stock in Super Fantasy Kingdom. The Gift and Curse appear together on the ordinary item card and tooltip. Buy and equip them with the game’s usual mouse or controller controls. Use the Magnet to remove and redistribute gear.

| Item | Gift | Curse |
| --- | --- | --- |
| Cracked Spellglass | Damaging circular projectile searches have 40% more radius. | Each qualifying damage effect deals 20% less damage per target. |
| Bitter Chalice | The wearer receives 50% stronger health heals and overheals. | Each heal arrives three game seconds later. Death or losing the last Chalice cancels pending heals. |
| Cracked Duelist’s Mirror | The wearer’s first hit that reaches each monster’s health deals double damage. | Each later hit by that wearer deals 25% less damage to that monster. |

**Playtest rules.** Copies of the same item occupy normal slots and grant their effect once. Different items work together. With Spellglass and Mirror, a qualifying 100-damage hit against an unarmored target deals 160 on the opener and 60 thereafter, subject to the target’s remaining health. Fully blocked hits preserve the opener. Each monster tracks who has already damaged its health, even before a Mirror was equipped. Removing and returning the Mirror preserves that history.

Spellglass applies to the game’s two circular enemy-search projectile actions, including compatible modded projectiles using those actions and native damage effects. The radius bonus applies to hit detection. Targeting range, rectangular attacks, and healing areas keep their normal behavior. Chalice follows the simulation clock and the original recipient. Shield, rage, and mana regeneration retain their normal timing.

**Continue.** Equipped items and drops use the game’s item IDs and ordinary save path. The game recreates combatants on Continue. Pending heals and hit histories belong to the previous combatants. Keep the mod enabled when continuing a run that holds these items.

## Local installation

Import the ZIP into a Super Fantasy Kingdom Thunderstore profile with BepInEx 5. For manual installation, extract `plugins/CursedEquipment.dll` to the profile’s `BepInEx/plugins` directory. The DLL includes all three item icons. Start the game from that profile.

## Build

Run `dotnet build CursedEquipment.csproj -c Release` followed by `python package.py`. The project reads Unity and game references from the installed game and BepInEx references from the local profile. Override the paths with MSBuild properties `GameDir` and `ProfileDir` when needed. The package is written to `dist/CursedEquipment-0.1.0.zip`.
