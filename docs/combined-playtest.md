# Combined playtest · 25 November 2026

The test used Super Fantasy Kingdom 0.8.068, BepInEx 5.4.23.5, a private snapshot of GKJ - Controller Test, and a copied human kingdom on day 13. The launcher held the shared `Local\SFKControllerRetest` mutex. The private Unity window used the desktop foreground. The original game profile and saves were outside the test paths.

## Package fingerprints

| Loaded plugin | Version | Loaded DLL SHA-256 | Source ZIP SHA-256 |
| --- | --- | --- | --- |
| Cursed Equipment | 0.1.0 | `30680463c6656776f867646ed8d0c495aabc42091660d6b2cad16bedc12e7b19` | `65ac8bc148cba4bc995c36ff6a1551b4388345c5d4bc1a26dfa3560f59a991c0` |
| Cursed Relics | 0.1.1 | `5b5a790a7301d581efb642a1c89054083a43b197403b85e04ce8052c323b4309` | `9c708b5e0562c9d77dc7349af27338198bc76ee3f3d2b5c932778694dca25031` |
| Hold the Line | 0.2.3 | `5b9cf019469d1cad4ecb8d2494fd61ada5e99676105a161afeb181e78feaf8fb` | `95413c2c0180cd9e465acc02d20123cb9972d14ce0433c0f57943bf067255d89` |
| Nights of Terror | 0.1.3 | `2d36428c6bd4fa5647d3892adc98d8006ccd15f2b7d9bf0069ec70f9c70dcaeb` | `0dcd77166ff946e041af1cc7b64cc17d322b0dd47ba2dbba613c650b2d5be7e1` |

Cursed Relics supplied its verified 0.1.1 build and explicitly released the live slot. Both published partner archives came directly from Thunderstore. The published Nights of Terror ZIP and the workspace 0.1.3 ZIP differ in the manifest and README. Their DLLs are byte-identical. [Published Nights of Terror ZIP](https://thunderstore.io/package/download/GKJ/NightsOfTerror/0.1.3/) · [Published Hold the Line ZIP](https://thunderstore.io/package/download/GKJ/HoldTheLine/0.2.3/)

## What ran

Five foreground cases passed 137 assertions. The 39 managed checks passed against the extracted Equipment DLL. The final player and loader logs contained zero error, fatal, exception, or failed-assertion lines. [Runtime inventory and assertion transcript](combined-playtest-results.txt)

| Foreground case | Passed assertions | In-game capture |
| --- | --- | --- |
| Virtual controller at 1× | 31 | [Blacksmith](screenshots/combined-2026-11-25/forge-pad-1x.png) |
| Virtual controller at 1.5× | 39 | [Blacksmith](screenshots/combined-2026-11-25/forge-pad-1p5x.png) |
| Keyboard/mouse scheme at 1× | 29 | [Blacksmith](screenshots/combined-2026-11-25/forge-mouse-1x.png) |
| Keyboard/mouse scheme at 1.5× | 29 | [Blacksmith](screenshots/combined-2026-11-25/forge-mouse-1p5x.png) |
| Continue from the preserved controller checkpoint | 9 | [Restored Archer](screenshots/combined-2026-11-25/continued-archer.png) |

- The unaltered Blacksmith pool included all three cursed IDs and rolled Bow, Tome and Sword. The test then supplied the three cursed IDs to the real Blacksmith to repeat the same UI and payment checks at both scales. The game generated the cards, buy buttons, price, shop callback and world drop.
- The virtual controller moved focus to the paid Chalice and confirmed the purchase. It paid 2 Beer once. The fixture used the native drop method to equip it. A native Magnet unloaded the first wearer’s gear and the fixture gave the Chalice drop to the copied kingdom’s Archer. The former wearer lost the queued heal and received immediate base healing again. The Archer received exactly one delayed +50% heal. Death canceled the queue without revival. [Focused 1.5× Forge](screenshots/combined-2026-11-25/forge-pad-1p5x.png) · [Archer after transfer](screenshots/combined-2026-11-25/transferred-archer.png)
- The simulation advanced at normal speed. It spawned 119 ordinary enemies and 60 midnight reinforcements. 71 ordinary enemies remained at the first reinforcement. One regular Lich and one reinforcement Shaman temporarily received a controlled 200 HP baseline and a position 12 units from a native circular projectile. The base radius of 10 missed. With Spellglass and Mirror, the 10-damage probe dealt 16 HP on the opener and 6 HP after Mirror was removed and re-equipped. The probe then restored the targets’ original position and health. [Midnight overlap](screenshots/combined-2026-11-25/midnight.png)
- Continue restored one Chalice to the Archer and zero to the former wearer. The restored Archer received the same delay and one +50% heal. The replay used the preserved day-13 checkpoint produced after the 1× controller purchase and transfer.

The first QA attempts exposed a foreground activation problem and pauses during the purchase cutscene and Continue. The launcher now verifies the Unity window is actually in front before the test proceeds. The probe resumes the copied kingdom at normal speed and bounds timed checks. The successful retests used the same Equipment DLL. The private player exited and the launcher released its mutex.

## Coverage limits

The controlled row guaranteed the cursed offers. The unaltered Blacksmith roll for this checkpoint offered ordinary gear. A naturally rolled cursed offer and a fully manual purchase and drag sequence remain unverified. The inputs came from virtual devices. In the keyboard/mouse scheme, the probe attempted Escape and used the native Leave button when Escape kept the merchant open. Keyboard-only purchase and equipment placement remain unverified. Equipment placement and the Magnet transfer used the game’s drop methods directly. The combat test ended ten seconds after the midnight group finished spawning. Full-night victory, campaign balance, other kingdoms, and physical input hardware remain outside this pass.
