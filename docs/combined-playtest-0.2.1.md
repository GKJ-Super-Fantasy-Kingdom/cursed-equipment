# Cursed Equipment 0.2.1 combined playtest

Super Fantasy Kingdom 0.8.068 ran in a visible foreground window with a private snapshot of the 14 GKJ DLLs from `GKJ - Controller Test`, the two local candidates, and the private QA harness. The published Hold the Line and Nights of Terror DLLs came from their exact Thunderstore ZIPs. The game used a copied Human kingdom and the shared `Local\SFKControllerRetest` mutex. The profile’s unrelated Achievement Enabler was outside this pass.

## Packages

| Mod | Loaded | DLL SHA-256 |
| --- | --- | --- |
| Cursed Equipment | 0.2.1 | `936fb51decc2b9df69b6685f5e7a25f26775e26867fcf4d50f6c76e945b53d39` |
| Cursed Relics | 0.2.2 | `7bd2af240862282a7b26a1d9f35cecdfe02a1032ac9010c4b96394daa3041731` |
| Nights of Terror | 0.1.3 | `2d36428c6bd4fa5647d3892adc98d8006ccd15f2b7d9bf0069ec70f9c70dcaeb` |
| Hold the Line | 0.2.3 | `5b9cf019469d1cad4ecb8d2494fd61ada5e99676105a161afeb181e78feaf8fb` |

The [runtime evidence](combined-playtest-0.2.1-results.txt) records the complete loaded inventory, including assembly versions and archive checksums. The package DLL passed 71 installed-assembly, logic, asset, and copied-save assertions.

## Interface and equipment

- The natural Blacksmith roll offered Emberwake Blade, Tome, and Knife. Ten rare cursed items were registered in the native stock pool. A controlled row placed Bitter Chalice beside Sword and Bow and used native pricing and Buy controls. Cursed Equipment used its original plum and crimson frame, which replaces the wooden backdrop only on its own cards. Ordinary items retained their original frame and wood. The first render exposed the overlapping wood layer, and the rebuilt ZIP was used for the captures and reruns. [1× mixed row](screenshots/0.2.1/mixed-1x.png) · [1.5× mixed row](screenshots/0.2.1/mixed-1p5x.png)
- The virtual controller reached all ten Buy buttons and the native Back control at both scales. All ten titles and descriptions fit, and the red Curse and black Gift stayed readable. The card hover and selected sprites and the focused price button were checked separately. [Individual cards](release-assets-0.2.1.md)
- The KeyboardMouse scheme passed at 1× and 1.5× with the mixed row, native Leave button, and a mouse purchase. Escape was exercised before using Leave, which closed the native merchant.
- With the offer IDs controlled, the native purchase charged 2 Beer and created exactly one Chalice. The fixture used native world drop methods and a Magnet to move it from the hero to the Archer. The old queued heal was canceled. The hero returned to base healing, the Archer received one 50% stronger heal after three game seconds, and an Archer death canceled a subsequent queued heal without revival. [Archer after transfer](screenshots/0.2.1/transferred-archer.png)
- Continue used the saved controller checkpoint. It restored exactly one Chalice on the Archer, and that wearer received one enhanced heal after the same delay. Vulture's Covenant restored with the equipment. [Archer after Continue](screenshots/0.2.1/continued-archer.png)

## Interactions

- In a controlled dusk invocation, Glass Crown reduced actual Archer maximum Health from 17.34 to 13.01. Red Mercy and Bitter Chalice turned a 2 HP heal into a single 3.9 HP delivery after the delay. With Leadheart Locket also equipped, amplified overheal stopped at 9.7575 HP, or 75% of the Glass Crown maximum. Ending the fixture restored the native maximum.
- The natural-speed combat pass spawned ordinary enemies with the cursed relics held, but the copied kingdom lost before the midnight probe. The controlled hit on an ordinary Shroom started at 200 HP. The 10-unit circle missed at 12 units. Spellglass plus Mirror and Vulture's Covenant left 186.4 HP on the opener and 181.3 HP after Mirror was removed and re-equipped.
- The replay on the same DLLs added 750 wall durability through the game API and restored hero Health nine times below half Health to keep the copied run alive. The clock still ran at normal speed. It spawned 119 ordinary enemies and all 60 scheduled midnight reinforcements. At the first reinforcement, 102 ordinary enemies were still alive. Both the ordinary Shroom and the midnight Satyr passed the same radius, damage, and re-equip checks with Vulture's Covenant held. [Midnight overlap](screenshots/0.2.1/midnight.png)

The signed-in profile and live saves were outside the private player. The private process closed and released its mutex. The player and BepInEx logs contained no new error or exception entries. The initial lost run is recorded as a QA assertion failure in the probe transcript.

## Coverage limits

Input came from Unity virtual devices. The shop offer fixture guarantees the same offers and prices across runs. The Magnet transfer drives the game's native drop methods directly. The combat probe uses temporary target Health and position to measure the real damage hooks. The pass covers the selected overlaps on this Human save. Physical input, fully manual drag, keyboard-only buying, full-night settlement, other kingdoms, and balance across a full campaign remain open.
