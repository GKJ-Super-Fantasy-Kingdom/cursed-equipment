"""Create a private player and snapshot the controller-test profile."""
from pathlib import Path
import os
import json
import shutil
import sys
from zipfile import ZipFile

repo = Path(__file__).resolve().parents[2]
private = repo / ".analysis" / "combined-player"
source = repo / ".analysis" / "player"
profile = Path(os.environ["APPDATA"]) / "Thunderstore Mod Manager/DataFolder/SuperFantasyKingdom/profiles/GKJ - Controller Test/BepInEx"
old = b"Cursed Equip QA 00001"
new = b"Cursed Combo QA 00001"
data = Path("Super Fantasy Kingdom_Data")
assert len(old) == len(new)
if not private.exists():
    assert (source / data / "app.info").read_text().splitlines() == ["GKJ QA Isolated Lab", old.decode()]
    shutil.copytree(source, private, ignore=shutil.ignore_patterns("BepInEx"))
    for name in ("app.info", "globalgamemanagers"):
        file = private / data / name
        assert old in file.read_bytes()
        file.write_bytes(file.read_bytes().replace(old, new))
    ignored = shutil.ignore_patterns("GKJ-HoldTheLine", "GKJ-CursedEquipment", "GKJ-NightsOfTerror", "GKJ-CursedRelics")
    for part in ("core", "patchers", "config", "plugins"):
        shutil.copytree(profile / part, private / "BepInEx" / part, ignore=ignored)

if len(sys.argv) == 5:
    for name, path in zip(("HoldTheLine", "NightsOfTerror", "CursedRelics", "CursedEquipment"), sys.argv[1:]):
        with ZipFile(path) as archive:
            assert archive.testzip() is None
            manifest = json.loads(archive.read("manifest.json"))
            assert manifest["name"] == name
            required = {"HoldTheLine": "0.2.3", "NightsOfTerror": "0.1.3"}
            if name in required:
                assert manifest["version_number"] == required[name]
            contents = archive.read(f"plugins/{name}.dll")
            (private / "BepInEx/plugins" / f"{name}.dll").write_bytes(contents)
else:
    assert len(sys.argv) == 1
print(private)
