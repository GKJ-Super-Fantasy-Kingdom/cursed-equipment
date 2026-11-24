"""Prepare a new private branch of the sibling playtest installation and its copied save."""
from pathlib import Path
import os
import shutil

repo = Path(__file__).resolve().parents[2]
fixture = repo.parent / "hold-the-line" / ".analysis" / "combined-qa"
player = repo / ".analysis" / "player"
old_name = b"Hold The Line QA 7e53"
new_name = b"Cursed Equip QA 00001"
save = Path(os.environ["USERPROFILE"]) / "AppData/LocalLow/GKJ QA Isolated Lab" / new_name.decode()

if player.exists() or save.exists():
    raise SystemExit("A private fixture already exists. Reuse it with run.ps1.")
source = fixture / "game"
data = Path("Super Fantasy Kingdom_Data")
if (source / data / "app.info").read_text().splitlines() != ["GKJ QA Isolated Lab", old_name.decode()]:
    raise SystemExit("Expected the sibling's isolated QA player.")
metadata = (source / data / "globalgamemanagers").read_bytes()
if len(old_name) != len(new_name) or old_name not in metadata:
    raise SystemExit("Unexpected private player metadata.")

shutil.copytree(source, player, ignore=shutil.ignore_patterns("BepInEx", "*_BackUpThisFolder_ButDontShipItWithYourGame"))
for part in ("core", "config", "patchers"):
    shutil.copytree(source / "BepInEx" / part, player / "BepInEx" / part)
(player / "BepInEx/plugins").mkdir()
for name in ("app.info", "globalgamemanagers"):
    file = player / data / name
    file.write_bytes(file.read_bytes().replace(old_name, new_name))
save.mkdir(parents=True)
for checkpoint in (fixture / "seed").glob("*.data"):
    shutil.copy2(checkpoint, save)
print(player)
