#!/usr/bin/env python3
"""CI preflight for original keyboard-controller texture generation."""
import io,json,subprocess,sys,tempfile,zipfile
from pathlib import Path
from PIL import Image

with tempfile.TemporaryDirectory() as d:
    output=Path(d)/'generated'
    subprocess.run([sys.executable, 'tools/build_keyboard_controller_atlas.py', '--out',str(output)], check=True)
    zip_path=Path(str(output)+'.zip')
    with zipfile.ZipFile(zip_path) as z:
        assert z.testzip() is None
        atlases=[n for n in z.namelist() if n.endswith('b99d9ef8125d8894-f3379f9602408f67-00002213.png')]
        assert len(atlases)==3,atlases
        for n in atlases:
            with Image.open(io.BytesIO(z.read(n))) as im:
                assert im.mode=='RGBA' and im.size==(256,256)
                assert 0 < im.getchannel('A').getextrema()[1] <=128
        manifests=[n for n in z.namelist() if n.endswith('glyph-pack.json')]
        assert len(manifests)==3
        for n in manifests:
            m=json.loads(z.read(n))
            assert m['schemaVersion']==1
            assert m['gameSerial'] in ('SLUS-21801','SLES-55353','SLPM-55127')
        assert not any(n.endswith('.iso') or 'BIOS' in n for n in z.namelist())
    print('3 region packs, 256x256 RGBA alpha<=128, manifests and ZIP integrity verified')