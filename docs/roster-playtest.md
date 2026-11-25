# Ten-item roster playtest

The 0.2.0 archive passed its CRC check and contains the same DLL loaded by the private Super Fantasy Kingdom 0.8.068 player. The embedded art uses ten distinct 32 × 32 sprites. The game supplies the frames and rarity accents.

| Blacksmith row | 1× | 1.5× |
| --- | --- | --- |
| Cracked Spellglass, Bitter Chalice, Cracked Duelist’s Mirror | [Capture](screenshots/0.2.0/blacksmith-1x-1.png) | [Capture](screenshots/0.2.0/blacksmith-1p5x-1.png) |
| Duelist’s Fang, Martyr’s Plate, Gravedigger’s Lantern | [Capture](screenshots/0.2.0/blacksmith-1x-2.png) | [Capture](screenshots/0.2.0/blacksmith-1p5x-2.png) |
| Hangman’s Quarrel, Emberwake Blade, Leadheart Locket | [Capture](screenshots/0.2.0/blacksmith-1x-3.png) | [Capture](screenshots/0.2.0/blacksmith-1p5x-3.png) |
| The Borrowed Halo | [Capture](screenshots/0.2.0/blacksmith-1x-4.png) | [Capture](screenshots/0.2.0/blacksmith-1p5x-4.png) |

Each scale passed 34 live assertions. The copied-save fixture presented all ten cards with the native merchant and Forge price. It checked the frame, text fit, and rendered text color for every item. It also confirmed that Plate and Locket limited a native overheal, and that Emberwake’s fourth hit applied native Burn and charged Health down to a floor of 1 HP. The two completed player logs contained zero errors or exceptions.

The packed DLL passed another 68 managed assertions covering the original items and the new damage, healing, and survival rules. The 0.2.0 run used controlled offers and hit sequences. Full-campaign balancing, natural acquisition of every new item, and a combined live replay with partner mods remain open.

ZIP SHA-256 `a5568607aa3b6d7b75ab14eaa22114a9b28f669b493fed9849fe91114174a400`

DLL SHA-256 `5d32af0c11c0bdca09593bb0a6f4c8258e0da3fe892c769845663e398d8a76bc`
