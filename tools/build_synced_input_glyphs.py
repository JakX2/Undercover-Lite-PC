#!/usr/bin/env python3
"""Generate original PCSX2 button glyphs AND a matching controller graphic.
The single source of key names is UndercoverKeyboardProfile.cs, not handwritten art.
Original asset hashes were catalogued from the player's PCSX2 texture dump;
no original PS2 game pixels are used.
"""
import argparse
import io
import json
import pathlib
import re
import zipfile
from PIL import Image, ImageDraw, ImageFont

ROOT = pathlib.Path(__file__).resolve().parents[1]
DEFAULT_PROFILE = ROOT / "src/UndercoverLite.Core/UndercoverKeyboardProfile.cs"
SERIALS = ["SLUS-21801", "SLES-55353", "SLPM-55127"]
ATLAS = "b99d9ef8125d8894-f3379f9602408f67-00002213.png"
HASHES = {
    "Triangle": "3932bc820cff2295-7e347471dfd31f91-00001554.png",
    "Circle": "3ef5ed33ce7400e6-5d5080dd80f7a56d-00001554.png",
    "Select": "565c987dcc9942d1-be3e24ef4b371b8d-00001554.png",
    "R1": "5e8b3773662cfffd-15ab2dbca5c4e797-00001554.png",
    "Start": "6a9f9e4d69bb02d2-be3e24ef4b371b8d-00001554.png",
    "R3": "99752cf8c7f203b4-37a640d9a823710-00001554.png",
    "R2": "ae0846b61eea3645-9c05bbf6e9ad7089-00001554.png",
    "L2": "b504e4faa31b1c73-d78b51572ac25e65-00001554.png",
    "L1": "d2faf9a272a7cd2d-3959933ff5fb8b26-00001554.png",
    "Cross": "fa7c4c608af78f38-99e1a7bd2d4d93c5-00001554.png"
}
# The missing standalone Square hash is not invented.
EXPECTED = {
    "Cross": "W", "Circle": "S", "Triangle": "Space",
    "Square": "X", "Start": "F", "Select": "R", "L1": "Q",
    "R1": "E", "L2": "C", "R2": "Shift", "R3": "V",
    "LLeft": "A", "LRight": "D",
    "Up": "Up", "Down": "Down", "Left": "Left", "Right": "Right",
    "RUp": "I", "RDown": "K", "RLeft": "J", "RRight": "L"
}
INK = (240, 245, 248, 255)
ORANGE = (255, 185, 64, 255)
PANEL = (20, 29, 38, 245)
EDGE = (152, 175, 193, 225)

def font(bold, size):
    paths = [
        "C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
            if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
    ]
    for path in paths:
        if pathlib.Path(path).exists():
            return ImageFont.truetype(path, size)
    raise RuntimeError("Install a TrueType sans font on your build machine.")

def extract_mapping(path):
    text = pathlib.Path(path).read_text(encoding="utf8")
    start = text.index("private static readonly (string Ps2, string PcKey)[] Bindings =")
    end = text.index("];", start)
    chunk = text[start:end]
    pairs = re.findall(r'\("([A-Za-z][A-Za-z0-9]*)",\s*"([A-Za-z0-9]+)"\)', chunk)
    values = dict(pairs)
    if len(values) != len(pairs) or len(pairs) != 22:
        raise ValueError("Expected 22 unique PS2 input bindings in C# source")
    for ps2, key in EXPECTED.items():
        if values.get(ps2) != key:
            raise ValueError("C# profile changed: " + ps2 + " now maps to " + str(values.get(ps2)) +
                             ". Update art layout and tests before generating.")
    return values

def cap_alpha(im):
    alpha = im.getchannel("A").point(lambda v: round(v * 128 / 255))
    im.putalpha(alpha)
    return im

def keycap(label):
    # Render at 4x and downsample for robust tiny 32x32 art.
    scale = 4
    im = Image.new("RGBA", (32*scale, 32*scale), (0,0,0,0))
    d = ImageDraw.Draw(im)
    def box(v): return tuple(int(q*scale) for q in v)
    d.rounded_rectangle(box((3,5,28,27)), radius=5*scale, fill=PANEL,
                        outline=ORANGE, width=1*scale)
    d.rounded_rectangle(box((5,7,26,25)), radius=3*scale, outline=EDGE, width=1*scale)
    visible = {"Space":"SPC", "Shift":"SHFT"}.get(label, label.upper())
    sizes = {1:13, 2:11, 3:9, 4:7}
    size=sizes[min(len(visible),4)]*scale
    d.text((16*scale,16*scale), visible, font=font(True,size),anchor="mm",fill=INK)
    return cap_alpha(im.resize((32,32), Image.Resampling.LANCZOS))

def diagram(mapping):
    s=4
    im=Image.new("RGBA",(256*s,256*s),(0,0,0,0))
    d=ImageDraw.Draw(im)
    def b(x):return int(round(x*s))
    def label(x,y,value,size=8,color=INK,bold=False):
        d.text((b(x),b(y)),value,font=font(bold,b(size)),fill=color)
    d.rounded_rectangle((b(8),b(12),b(248),b(239)),radius=b(10),
                        fill=(14,19,24,247),outline=EDGE,width=b(2))
    d.rounded_rectangle((b(13),b(17),b(243),b(39)),radius=b(5),
                        fill=(37,43,48,250))
    label(20,22,"UNDERCOVER  /  PC KEYS",10,ORANGE,True)
    label(198,25,"0.11",8,INK,True)
    def cell(x,y,k,action,ps2):
        key_w=39 if len(k)<=5 else 51
        d.rounded_rectangle((b(x),b(y),b(x+key_w),b(y+18)),radius=b(3),
                            fill=PANEL,outline=ORANGE,width=b(1))
        keysize=9 if len(k)<=4 else 7
        d.text((b(x+key_w/2),b(y+8.5)),k,font=font(True,b(keysize)),
               fill=INK,anchor="mm")
        label(x+key_w+5,y+1,action,7,INK,True)
        label(x+key_w+5,y+10,ps2,6,(170,187,202,240))
    rows_left=[
        (mapping["Cross"],"GAS","Cross"),
        (mapping["Circle"],"BRAKE","Circle"),
        (mapping["LLeft"]+"/"+mapping["LRight"],"STEER","L stick"),
        (mapping["Triangle"].upper(),"HANDBRK","Triangle"),
        (mapping["Square"],"SPEEDBRK","Square*"),
        (mapping["R2"].upper(),"NITRO","R2"),
        (mapping["L2"],"ALT HBRK","L2"),
        (mapping["Start"],"PAUSE","Start")
    ]
    rows_right=[
        (mapping["L1"],"GEAR -","L1"),
        (mapping["R1"],"GEAR +","R1"),
        (mapping["Select"],"RESET","Select"),
        (mapping["R3"],"VIEW","R3"),
        ("ARROWS","D-PAD","direction"),
        ("IJKL","LOOK","R stick"),
        ("Z","STICK BTN","L3"),
        (mapping["Cross"],"ACCEPT","in menus")
    ]
    for x,rows in ((17,rows_left),(137,rows_right)):
        for i,(key,action,ps2) in enumerate(rows):
            cell(x,44+i*22,key,action,ps2)
    label(19,222,"* SQUARE ICON NOT YET DUMPED",7,ORANGE,True)
    label(19,232,"W = ACCEPT  /  SPACE = BACK IN MENUS",6,INK)
    return cap_alpha(im.resize((256,256), Image.Resampling.LANCZOS))

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--out", default="build/synced-input-glyphs-0.11")
    p.add_argument("--profile-source",default=str(DEFAULT_PROFILE))
    args=p.parse_args()
    values=extract_mapping(args.profile_source)
    out=pathlib.Path(args.out)
    out.mkdir(parents=True,exist_ok=True)
    preview=out/"previews"
    preview.mkdir(exist_ok=True)
    atlas=diagram(values)
    atlas.save(preview/"keyboard_controller_256x256.png")
    backdrop=Image.new("RGBA",(768,768),(33,38,44,255))
    backdrop.alpha_composite(atlas.resize((768,768),Image.Resampling.NEAREST))
    backdrop.convert("RGB").save(preview/"controller_enlarged.png")
    icons={ps2:keycap(values[ps2]) for ps2 in HASHES}
    for serial in SERIALS:
        base=out/"packs"/serial/"matched-driving-first"
        targets=base/"replacements"
        targets.mkdir(parents=True,exist_ok=True)
        atlas.save(targets/ATLAS)
        for ps2,name in HASHES.items():
            icons[ps2].save(targets/name)
        manifest={
            "schemaVersion":1,"id":"matched-driving-first-"+serial.lower(),
            "name":"Undercover Lite PC matched driving keys 0.11 ["+serial+"]",
            "gameSerial":serial,"inputProfile":"UndercoverLite-DrivingFirst",
            "description":"Static PCSX2 input profile match. No context switching; actual active profile must be selected separately.",
            "declaredBindings":{k:values[k] for k in list(HASHES)+["Square","LLeft","LRight"]},
            "uncaptured":["Square"],
        }
        (base/"glyph-pack.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf8")
    text="""UNDERCOVER LITE PC 0.11 - INPUT-ALIGNED GLYPH PACK
====================================================
All 10 available 32x32 prompts and the 256x256 controller diagram
are newly drawn using bindings parsed directly from the same C# source
that generates UndercoverLite-DrivingFirst.ini. The Square texture hash
is unknown and is NOT included as a separate icon.

Use only after explicitly selecting UndercoverLite-DrivingFirst as the
Input Profile for Undercover in PCSX2 Game Properties.
The artwork CANNOT confirm that PCSX2 is actively using the profile.
Do not use with the old 0.5b menu-reference theme or custom bindings.

The old 0.7 diagram was WRONG (B=Speedbreaker, Esc=Pause,
1-4=D-pad, arrows=freelook). This version fixes them to
X=Speedbreaker, F=Pause, arrows=D-pad, IJKL=right stick.
Other icon labels exactly match PS2 bindings in the 0.9 C# source.

The same PS2 button changes meaning between contexts:
W -> Cross -> Accelerate while driving / Accept in menu;
Space -> Triangle -> Handbrake / Back;
F -> Start -> Pause / Done on name entry;
X -> Square -> Speedbreaker / Delete.

GAMEPLAY WARNING:
PCSX2 normally uses Space for its own Pause hotkey; inspect Hotkeys
and rebind it if needed. The launcher never changes PCSX2 hotkeys.

The 11 images use previously captured hashes, with no game-original
pixels. Only Cross [W] has been verified to render in-game so far.
Regional hashes still need checking. Close PCSX2 before switching
with Undercover Lite's Button glyphs / texture packs manager.
If you have a 0.5b pack, first adopt it (if byte-identical) or back
up manually, then select this new 'matched-driving-first' theme.
"""
    (out/"README_FIRST.txt").write_text(text,encoding="utf8")
    (out/"bindings.json").write_text(json.dumps(values,indent=2)+"\n",encoding="utf8")
    archive=pathlib.Path(str(out)+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        for file in out.rglob("*"):
            if file.is_file():
                z.write(file,file.relative_to(out))
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
    print(archive, "with",len(SERIALS),"region packs of 11 textures")
if __name__ == "__main__":
    main()
