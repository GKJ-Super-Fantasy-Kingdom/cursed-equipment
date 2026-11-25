# Cursed Equipment

Ten rare pieces of equipment join the Blacksmith’s stock in Super Fantasy Kingdom. Each has original pixel art inside the game’s native item frame. The Gift and red Curse appear together on the card and tooltip. Buy and equip gear with the game’s usual controls. Use the Magnet to remove and redistribute it.

| Item | Gift | Curse |
| --- | --- | --- |
| Cracked Spellglass | Damaging circular projectile searches have 40% more radius. | Each qualifying damage effect deals 20% less damage per target. |
| Bitter Chalice | The wearer receives 50% stronger health heals and overheals. | Each heal arrives three game seconds later. Death or losing the last Chalice cancels pending heals. |
| Cracked Duelist’s Mirror | The wearer’s first hit that reaches each monster’s health deals double damage. | Each later hit by that wearer deals 25% less damage to that monster. |
| Duelist’s Fang | Repeat hits on the same monster gain 10% damage each, up to 50%. | The first hit after switching targets deals 30% less damage. |
| Martyr’s Plate | The wearer takes 30% less Health damage. | The wearer receives 50% less health healing. |
| Gravedigger’s Lantern | Deal 40% more damage to a target no other ally has hit in the past two game seconds. | Deal 15% less damage if an ally hit that target during the window. |
| Hangman’s Quarrel | Deal 60% more damage against monsters below 20% Health. | Deal 15% less damage against healthier targets. |
| Emberwake Blade | Every fourth qualifying hit applies Burn to the surviving monster. | Each trigger costs 5% of the wearer’s maximum Health, down to a floor of 1 HP. |
| Leadheart Locket | Take 35% less Health damage below half Health. | Health healing stops at 75% of maximum Health. Existing Health above that threshold is retained. |
| The Borrowed Halo | Once per battle, survive a lethal Health hit and block further Health damage for two game seconds. | After surviving, damage to monsters is halved for eight game seconds. |

**Playtest rules.** Copies of the same item occupy normal slots and grant their effect once. Different items work together. With Spellglass and Mirror, a qualifying 100-damage hit against an unarmored target deals 160 on the opener and 60 thereafter, subject to the target’s remaining health. Fully blocked hits preserve the opener. Each monster tracks who has already damaged its health, even before a Mirror was equipped. Removing and returning the Mirror preserves that history.

Spellglass applies to the game’s two circular enemy-search projectile actions, including compatible modded projectiles using those actions and native damage effects. The radius bonus applies to hit detection. Targeting range, rectangular attacks, and healing areas keep their normal behavior. Chalice follows the simulation clock and the original recipient. Shield, rage, and mana regeneration retain their normal timing.

Fang, Lantern, Quarrel and Emberwake use health hits that come directly from a unit. Burn and other status ticks skip their hit counters and modifiers. Emberwake uses the game’s Burn status and requires a wearer with positive Health. Plate, Locket and Halo work on the wearer’s remaining Health damage after native wall and shield handling. Their reductions compose with each other and with the Chalice when equipped together.

**Continue.** Equipped items and drops use the game’s item IDs and ordinary save path. The game recreates combatants on Continue. Pending heals and hit histories belong to the previous combatants. Keep the mod enabled when continuing a run that holds these items.

## Local installation

Import the ZIP into a Super Fantasy Kingdom Thunderstore profile with BepInEx 5. For manual installation, extract `plugins/CursedEquipment.dll` to the profile’s `BepInEx/plugins` directory. The DLL includes all ten item icons. Start the game from that profile.

## Build

Run `dotnet build CursedEquipment.csproj -c Release` followed by `python package.py`. The project reads Unity and game references from the installed game and BepInEx references from the local profile. Override the paths with MSBuild properties `GameDir` and `ProfileDir` when needed. The package is written to `dist/CursedEquipment-0.2.0.zip`.
