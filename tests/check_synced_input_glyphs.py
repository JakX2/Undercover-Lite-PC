#!/usr/bin/env python3
"""Regression: hashes, sizes, labels, and C# controller profile cannot diverge."""
from pathlib import Path
import importlib.util
import io
import json
import subprocess
import sys
import tempfile
import zipfile
from PIL import Image

root=Path(__file__).resolve().parents[1]
source=root/"tools/build_synced_input_glyphs.py"
spec=importlib.util.spec_from_file_location("syncglyphs", source)
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

mapping=module.extract_mapping(root/"src/UndercoverLite.Core/UndercoverKeyboardProfile.cs")
assert mapping["Square"] == "X" and mapping["Start"] == "F"
assert mapping["Left"] == "Left" and mapping["RLeft"] == "J"
assert len(module.HASHES) == 10 and "Square" not in module.HASHES
assert all(len(p.split("-")) in (3,4) for p in module.HASHES.values())
with tempfile.TemporaryDirectory() as d:
    base=Path(d)/"corrected"
    subprocess.run([sys.executable,str(source),"--out",str(base)],check=True)
    zipfile_path=Path(str(base)+".zip")
    with zipfile.ZipFile(zipfile_path) as z:
        assert z.testzip() is None
        all_names=z.namelist()
        for serial in module.SERIALS:
            prefix=f"packs/{serial}/matched-driving-first/"
            mani=json.loads(z.read(prefix+"glyph-pack.json"))
            assert mani["inputProfile"] == "UndercoverLite-DrivingFirst"
            assert mani["gameSerial"] == serial
            assert mani["declaredBindings"]["Start"] == "F"
            assert mani["declaredBindings"]["Square"] == "X"
            assert mani["declaredBindings"]["Cross"] == "W"
            assert mani["declaredBindings"]["Triangle"] == "Space"
            names=[p for p in all_names if p.startswith(prefix+"replacements/")]
            assert len(names) == 11,(serial,names)
            assert {Path(n).name for n in names}==set(module.HASHES.values())|{module.ATLAS}
            for name in names:
                with Image.open(io.BytesIO(z.read(name))) as image:
                    assert image.mode=="RGBA"
                    assert image.size == ((256,256) if name.endswith(module.ATLAS) else (32,32))
                    assert 0<image.getchannel("A").getextrema()[1]<=128
        assert not any(name.lower().endswith((".iso",".chd",".bin")) for name in all_names)
        atlas=z.read("previews/keyboard_controller_256x256.png")
        assert atlas
        readme=z.read("README_FIRST.txt").decode("utf8")
        assert "B=Speedbreaker" in readme and "F=Pause" in readme
        assert "W = ACCEPT" in readme or "W -> Cross" in readme
        print("PASS: 3 game-region themes, 33 images total, source-consistent labels, PNG alpha and zip integrity.")
    # Mutate source inside a scratch file: generator MUST fail instead of silently
    # releasing misleading art when future controller mappings change.
    altered=Path(d)/"changed.cs"
    src=(root/"src/UndercoverLite.Core/UndercoverKeyboardProfile.cs").read_text(encoding="utf8")
    assert '("Start", "F")' in src
    altered.write_text(src.replace('("Start", "F")','("Start", "Escape")'),encoding="utf8")
    proc=subprocess.run([sys.executable,str(source),"--out",str(Path(d)/"bad"),
                         "--profile-source",str(altered)],capture_output=True,text=True)
    assert proc.returncode!=0
    assert "C# profile changed: Start" in (proc.stderr+proc.stdout)
    print("PASS: deliberate C# button remapping fails generation rather than emitting stale labels.")
