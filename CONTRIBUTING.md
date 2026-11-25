# Contributing to Cursed Equipment

## Report a bug

Include the game, mod and BepInEx versions, the item, its wearer, and the sequence that reproduces the problem. For card or control issues, include the UI scale and input scheme. Remove personal paths from excerpts before sharing logs.

## Build and test

Build with `dotnet build CursedEquipment.csproj -c Release` and package with `python package.py`. Pass `GameDir` and `ProfileDir` as MSBuild properties to use your own game and BepInEx installations. The [test guide](tests/README.md) covers the managed checks and copied-save playtests.

Keep game binaries, copied saves, raw personal logs and local build output out of commits.

## Submit a change

Use a branch and Conventional Commits. Describe the player-visible behavior and which checks you ran.
