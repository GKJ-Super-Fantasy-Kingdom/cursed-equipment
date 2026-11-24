# Cursed Equipment 0.1.0 playtest

Tested on 24 November 2026 with Super Fantasy Kingdom 0.8.068, BepInEx 5.4.23.5 and a copied human kingdom checkpoint that opens on the morning of Day 13. Unity ran in a private batch player with a separate save directory and preferences. Each run held the shared `Local\SFKControllerRetest` mutex.

## Live results

Six completed runs passed 72 live assertions. The recorded output also includes all 39 managed checks. [Assertion results](playtest-results.txt)

| Scenario | Observed result |
| --- | --- |
| Merchant and controls | The native Forge accepted all three IDs alongside ordinary stock. The fixture supplied the three choices together and used the Forge’s price of 2 Beer. Simulated mouse clicks and D-pad navigation plus South confirmation selected the expected item at both 1× and 1.5×. Each purchase spawned the corresponding native drop. |
| Readability | On all three native cards, both terms appeared together and TextMesh Pro reported that the title and description fit at both scales. The English pixel font rendered the straight apostrophe in Mirror’s title. [1× merchant](screenshots/0.1.0/forge-mouse-1x.png) · [1.5× merchant](screenshots/0.1.0/forge-gamepad-1p5x.png) |
| Equipment rules | The native drop endpoint accepted an eligible wearer. A pinned wearer could not equip the item, and a fourth ordinary item could not enter a full three-item loadout. The Magnet released the Chalice as a movable item and another unit equipped it. |
| Spellglass and Mirror | A test projectile used the native circular physics search with a base radius of 10 and a monster centered 12 world units away. Each run first missed without Spellglass. With the glass, a shield fully absorbed a hit and preserved Mirror’s opener. The next 10-damage attack took the monster from 200 to 184 HP. Removing and re-equipping Mirror left the next hit at 6 damage, taking the monster to 178 HP. |
| Chalice | A damaged wearer received zero immediately, then 1.5 times the original heal after three simulation seconds. Moving the Chalice with the Magnet canceled the first wearer’s pending heal. Killing the second wearer during the delay left it dead at zero HP after the deadline. |
| Save and Continue | The game wrote the three equipped IDs into its ordinary checkpoint. A fresh player opened the saved tavern through Continue. The test advanced the tavern through its native save and scene APIs. The restored hero held all three pieces exactly once. [Restored loadout](screenshots/0.1.0/continued-loadout.png) · [Kingdom after Continue](screenshots/0.1.0/continued-kingdom.png) |
| Compatible content | A separate QA plugin registered an Archer-derived native unit and Bow-derived gear under new IDs. The game spawned the recruit from a native unit record. It equipped the new gear, Spellglass and Mirror in its three slots. The same radius, shield and 16/6 damage checks passed on that unit. |

## Managed and package checks

The 39 managed assertions ran against the DLL extracted from the ZIP, using the installed game assemblies. They exercised both circle-search implementations, both native damage-delivery implementations, and the monster health branch used by Mirror. They also covered duplicate copies, gear transfers and immediate re-equips, independent wearer and monster histories, promotion, pre-equipment damage, non-damaging effects, overheal, shield and rage exclusions, independent heal deadlines, recursive-delivery protection and copied-save inventory round-tripping.

The release build completed with zero warnings. The ZIP passed CRC, membership and byte-for-byte checks for its DLL, manifest, README, changelog, license and 256 × 256 icon. The private playtest DLL matched the packaged binary.

## Coverage limits

The merchant row and combat encounters were supplied by a QA plugin. A naturally rolled three-item offer, physical controller hardware, keyboard-only navigation and an independently released unit or gear pack remain untested. The in-game card text is English. These playtest values have not had a full-run balance pass.

The screenshots render the running game’s cameras and native canvases into an offscreen texture. The UI click and gamepad tests used Unity Input System events. Equipment placement and the Magnet transfer used the game’s native drop methods programmatically.
