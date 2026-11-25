# Cursed Equipment 0.2.3 local review

Equipment uses the same original trim, crimson title ribbon, corner sigil and icon background as Cursed Relics 0.2.8. The four PNGs are byte-identical in both packaged DLLs. The ordinary game's image still draws the rounded rim and state-specific focus. Equipment masks the trim to that image and leaves the native title, item icon, rarity, kingdom badge and price on their own layers.

## Card captures

- Blacksmith with Sword, Bitter Chalice and Bow at [1×](../screenshots/0.2.3/mixed-1x.png) and [1.5×](../screenshots/0.2.3/mixed-1p5x.png). The native Buy focus is on Bitter Chalice.
- Sword, Bitter Chalice and Bell of the Last Watch compared in one controlled merchant row at [1×](../screenshots/0.2.3/shared-row-resting-1x.png) and [1.5×](../screenshots/0.2.3/shared-row-resting-1p5x.png). Same-state comparisons also show [1× hover](../screenshots/0.2.3/shared-row-hover-1x.png), [1.5× hover](../screenshots/0.2.3/shared-row-hover-1p5x.png), [1× selected](../screenshots/0.2.3/shared-row-selected-1x.png) and [1.5× selected](../screenshots/0.2.3/shared-row-selected-1p5x.png).
- [Ten individual 1.5× card PNGs and selectable Gift and Curse text](../../README.md#screenshots) · [Thunderstore Details draft](../../thunderstore/DETAILS.md#screenshots)
- [Bitter Chalice before and after at 1.5×](../screenshots/0.2.3/before-after-bitter-chalice.png)

The native faces shared a width and height of **240 × 320 px at 1×** and **360 × 480 px at 1.5×** in all three states. The native image mesh, texture coordinates and rounding sprite matched between the ordinary item, the cursed item, and the cursed relic. At both scales the Equipment roster had at least **15.8 card units** of side clearance and **5.3** at the bottom. Signed percentages remained on one line. All ten Thunderstore images point to the `v0.2.3` GitHub tag.

## Item sprites

All ten icons are original 16 × 16 pixel sprites with a centered pivot, compact silhouettes and a one-pixel dark contour. The ground renderer uses the same sprite as the Blacksmith at 16 pixels per world unit, with the game's drop shadow, bobbing motion and material. The [purchased Bitter Chalice](../screenshots/0.2.3/purchased-drop.png) shows the native drop after it landed from the Blacksmith sale.

The [ground comparison](../screenshots/0.2.3/ground-icons-detail.png) shows controlled native drops with Sword, Tome, Bow and Magnet in the left column. All ten cursed drops matched the ordinary sprite size, pixel pitch, pivot, scale, shader and shadow. [Full ground screenshot](../screenshots/0.2.3/ground-icons.png)

## Copied-save playtest

The private window ran Super Fantasy Kingdom 0.8.068 with Equipment 0.2.3, Relics 0.2.8, Nights of Terror 0.1.3, Hold the Line 0.2.3 and the remaining twelve GKJ DLLs from the installed Controller Test profile. The DLL inventory and complete archive checksums are recorded in [the runtime transcript](results.txt).

The original local playtest ZIP SHA-256 was `ba61dd7d9e6e18c92d3993299fd1f2dbd7281bcb473bfbe8b560023224d382b4`. The release ZIP SHA-256 is `8614c01ecc29d42b4fbdc1ac14e826323acc059cc6e90dabd287707b708a9540`. Both contain the same Equipment DLL, SHA-256 `d649a9315e008452a2f29e8cad70290a99b9b364508255a74ec4903aa9e198a3`. The DLL passed 72 managed checks. The release changes package art and listing material.

The virtual controller bought one Bitter Chalice for 2 Beer. A Magnet and native drop callbacks transferred it to the Archer. The former wearer regained normal healing, and the Archer received one delayed amplified heal. Death canceled a queued heal. Continue loaded the native checkpoint with the Chalice on the Archer and Vulture's Covenant still held. A controlled dusk sample verified that Glass Crown set maximum Health to 13.01 from 17.34, Chalice and Red Mercy delivered 3.9 HP from a 2 HP heal, and Locket capped amplified overheal at 75% of the reduced maximum. [Transfer](../screenshots/0.2.3/transferred-archer.png) · [Continue](../screenshots/0.2.3/continued-archer.png)

The 1× night fixture boosted defender power and wall capacity, reduced incoming native attacks on copied defenders, and enabled automatic tavern entry. Controlled hits measured each equipment effect separately. Day 13 spawned 119 ordinary enemies and all 60 midnight reinforcements. A tagged 2 HP heal during the reinforcement wave arrived once after three game seconds as 3.9 HP with Chalice and Red Mercy. After that measurement and the last spawn, the probe cleared the remaining 101 non-boss enemies. The game settled the night and continued into day 14. [Day 13 combat](../screenshots/0.2.3/combat-13.png) · [Midnight reinforcements](../screenshots/0.2.3/midnight-13.png)

All ten effects passed again during day 14. The Borrowed Halo rearmed for the new battle, expired its two- and eight-second windows, and stayed spent for the rest of that fight. The native boss spawned and died, and the game paused naturally for both cutscenes. The probe opened the native reward chest, and the day settled without a controlled enemy clear. The maximum observed simulation speed was 1× on both nights. [Day 14 combat](../screenshots/0.2.3/combat-14.png)

Six foreground cases passed 444 assertions. The player and loader logs captured one shared Unity exception at the end of the 1× frame run, in the game's `UIGamepadCursor.OnControlsChanged` from `OnDisable` during shutdown. The stack contained no Equipment call, and the frame run finished all assertions. The combat logs had no error or exception entries. The private player closed and released the mutex.

The offer rotation, icon placement, incoming survivability and item damage samples used the documented fixtures. This verifies the native timers, settlement and selected effect transitions in the copied Human kingdom. Physical input, fully manual dragging and unassisted combat balance remain outside this pass.

## Release partner smoke

The focused follow-up used the exact Cursed Relics 0.2.9 DLL `fa3f1bf894a008aa6d06fdcc11cb03e4b5a8e42a9e9ac416a0b511a51df2b934` from its verified gameplay ZIP, with Equipment 0.2.3, Hold the Line 0.2.3, Nights of Terror 0.1.3 and the other Controller Test partners. Two foreground copied-save passes completed **128 assertions**. The probe opened a native kingdom chest at a controlled location, claimed Bell of the Last Watch with a virtual controller, then bought and transferred Bitter Chalice through the Blacksmith route. Chalice and Red Mercy delivered 3.9 HP from a 2 HP input. Glass Crown and Locket matched the expected maximum Health and healing cap. Continue restored the Chalice and Vulture's Covenant. The native `UIGamepadCursor.OnDisable` exception recurred during shutdown after the purchase pass. The separate Continue run produced no error or exception entries. The private player closed and the mutex was released. [Smoke transcript and hashes](partner-smoke-0.2.9.txt)
