#!/usr/bin/env python3
"""Build an original, hash-matched PC keyboard diagram for NFS Undercover PS2.
Artwork is original Pillow vector drawing; no original game pixels are used.
Requires Pillow. All pack assets are labeled experimental until gameplay verified.
"""
from PIL import Image, ImageDraw, ImageFont
import zipfile, io, json, hashlib, pathlib, textwrap, argparse

parser = argparse.ArgumentParser(description='Create original PC keyboard diagram glyph packs for PCSX2')
parser.add_argument('--out', default='build/keyboard-controller-0.7', help='Output directory; does not modify PCSX2 settings')
parser.add_argument('--source-poc', help='Optional original 0.5b proof zip containing the 10 driving-first PNGs')
args = parser.parse_args()
OUT=pathlib.Path(args.out)
ATLAS='b99d9ef8125d8894-f3379f9602408f67-00002213.png'
SRC=args.source_poc
S=4
def choose_font(candidates):
    for font in candidates:
        if pathlib.Path(font).exists():
            return font
    raise SystemExit('No suitable font available. Install Lato or DejaVu Sans (or use Windows Segoe UI).')
FONT=choose_font(['C:/Windows/Fonts/segoeuib.ttf', '/usr/share/fonts/truetype/lato/Lato-Bold.ttf', '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'])
FONTLIGHT=choose_font(['C:/Windows/Fonts/segoeui.ttf', '/usr/share/fonts/truetype/lato/Lato-Regular.ttf', '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'])
SERIALS=['SLUS-21801','SLES-55353','SLPM-55127']

im=Image.new('RGBA',(256*S,256*S),(0,0,0,0));d=ImageDraw.Draw(im)
def B(x):return int(round(x*S))
def box(coords,fill,outline=None,radius=0,width=1):
 c=tuple(map(B,coords));
 if radius: d.rounded_rectangle(c,radius=B(radius),fill=fill,outline=outline,width=B(width))
 else:d.rectangle(c,fill=fill,outline=outline,width=B(width))
def text(x,y,t,size=8,color=(244,245,246,255),bold=False,anchor=None):
 d.text((B(x),B(y)),t,font=ImageFont.truetype(FONT if bold else FONTLIGHT,B(size)),fill=color,anchor=anchor,stroke_width=0)
def line(coords,color,width=1):d.line(tuple(B(x) for x in coords),fill=color,width=B(width),joint='curve')
W=(233,237,241,238);ORANGE=(255,194,33,255);MUTED=(182,194,204,234);DARK=(8,13,16,235)
# Preserve the *placement* of connectors from source (not its pixel data).
d.arc((B(-10),B(9),B(55),B(110)),270,360,fill=W,width=B(2))
line((54,54,54,68),W,1.6)
d.arc((B(180),B(9),B(268),B(110)),180,270,fill=W,width=B(2))
line((181,54,181,68),W,1.6)
line((0,205,76,205),W,1.4)
d.arc((B(66),B(187),B(96),B(216)),15,95,fill=W,width=B(1.5))
# dark frame / color story from 2008 NFS-inspired UI, no original artwork
box((17,70,231,201),(10,14,18,224),(210,218,223,195),radius=12,width=2)
box((20,73,228,89),(25,34,39,255),radius=5)
text(27,75,'PC DRIVING',size=10,color=ORANGE,bold=True)
text(212,78,'v0.7',size=7,color=MUTED,anchor='ra')
# key helper: label and a microcaption below

def key(x,y,w,h,k,c,accent=False,keysize=11):
    box((x,y,x+w,y+h),(25,32,37,252),(255,195,48,255) if accent else (158,178,190,234),radius=4,width=1)
    if accent:box((x+2,y+2,x+w-2,y+4),(255,177,20,215),radius=1)
    text(x+w/2,y+3,k,size=keysize,color=W if not accent else ORANGE,bold=True,anchor='mt')
    text(x+w/2,y+h-7,c,size=6,color=MUTED,anchor='mt')

# left control cluster: steering / accelerate, right cluster: gearbox and actions
text(28,93,'STEER / PEDALS',size=8,color=MUTED,bold=True)
text(146,93,'RACE ACTIONS',size=8,color=MUTED,bold=True)
key(64,103,32,28,'W','GAS',True,12)
key(27,137,32,28,'A','LEFT',False,12)
key(64,137,32,28,'S','BRAKE',False,12)
key(101,137,32,28,'D','RIGHT',False,12)
key(145,103,33,28,'Q','DOWN')
key(184,103,33,28,'E','UP')
key(145,137,33,28,'C','H.BRK')
key(184,137,33,28,'SHIFT','NITRO',True,8)
# mini bar icons for actions and digital navigation
box((27,173,134,195),(29,36,43,251),(148,159,171,186),radius=3)
text(34,176,'SPACE  /  HB',size=8,color=W,bold=True)
text(34,186,'B SPEEDBREAKER',size=7,color=MUTED)
box((143,173,217,195),(29,36,43,251),(148,159,171,186),radius=3)
text(150,176,'ESC  PAUSE',size=8,color=W,bold=True)
text(150,186,'R RESET  V VIEW',size=6,color=MUTED)
text(22,218,'D-PAD  1–4',size=7,color=MUTED,bold=True)
text(136,218,'LOOK  ARROWS',size=7,color=MUTED,bold=True)
text(19,230,'SUGGESTED KEYS  •  MATCH PCSX2 BINDINGS',size=6,color=ORANGE,bold=True)
# Maintain same alpha ceiling as source (0-128) to minimize unexpected GS blending.
im=im.resize((256,256),Image.Resampling.LANCZOS)
a=im.getchannel('A').point(lambda v: round(v*128/255))
im.putalpha(a)
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'previews').mkdir(exist_ok=True)
im.save(OUT/'previews'/'keyboard_controller_256x256.png')
# Previews: transparent texture enlarged on neutral backdrop, no game pixels.
bg=Image.new('RGBA',(768,768),(37,41,45,255));big=im.resize((768,768),Image.Resampling.NEAREST);bg.alpha_composite(big);bg.convert('RGB').save(OUT/'previews'/'keyboard_controller_atlas_enlarged.png')
zip_in=zipfile.ZipFile(SRC) if SRC else None
for serial in SERIALS:
    dest=OUT/'packs'/serial/'driving-first-plus-controller'
    (dest/'replacements').mkdir(parents=True,exist_ok=True)
    source_prefix=f'packs/{serial}/driving-first/replacements/'
    names=[]
    if zip_in:
        for name in zip_in.namelist():
            if name.startswith(source_prefix) and name.endswith('.png'):
                fn=name[len(source_prefix):]
                (dest/'replacements'/fn).write_bytes(zip_in.read(name));names.append(fn)
        if len(names)!=10:
            raise ValueError(f'Expected 10 driving-first icons for {serial}; found {len(names)}')
    im.save(dest/'replacements'/ATLAS)
    manifest={'schemaVersion':1,'id':f'driving-plus-diagram-{serial.lower()}',
              'name':f'Undercover Lite PC driving-first + keyboard diagram [{serial}]',
              'gameSerial':serial,'inputProfile':'driving-first',
              'description':'Experimental original PC art. No keyboard remap. New controller diagram is not in-game verified; region-specific hashes unverified.'}
    (dest/'glyph-pack.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')

readme='''# Undercover Lite PC 0.7: Keyboard controller diagram (experimental)

This mod adds ONE new original 256x256 PNG replacement for the PS2 controller diagram, plus the 10 original PC keyboard glyph textures from the already-shared 0.5b `driving-first` proof.

## What is known
- The 256x256 controller picture was identified in your 97-image `dumps.zip` under hash:
  b99d9ef8125d8894-f3379f9602408f67-00002213.png
- Your prior screenshot *confirmed* one of the 32x32 Cross hashes rendered as `Accept [W]` in-game. It does not establish that THIS 256x256 image will successfully replace or align in the game.
- All 11 included textures are newly authored keycap art, not original game textures; the original 10 were generated previously and are carried into this complete pack.

## How to test with Undercover Lite 0.6+
1. Close the game and PCSX2; back up any existing texture replacements.
2. If you already installed `driving-first` with an older Undercover Lite release, first use `Activate / Switch` to select that SAME original pack and explicitly adopt its byte-identical files. Do not adopt modified or unknown files.
3. Use `Activate / Switch` to select the `driving-first-plus-controller` folder for your verified game serial. It includes the SAME 10 32x32 driving glyphs, so managed switching preserves them and adds the controller atlas as the 11th file.
4. Check that PCSX2 Graphics > Texture Replacement > Search Directory matches Undercover Lite's selected directory; keep Load Textures ON.
5. Relaunch Undercover, enter Options > Controls, and see if the middle gamepad illustration is replaced by the PC keyboard diagram. Screenshot the result to validate anchors, alpha, scale and text layout.
6. To revert to just the 10 icons, switch back to the old `driving-first` pack, or use `Disable managed glyphs` to remove all tracked replacements.

## Controls displayed by this experimental diagram
W Accelerate / Cross, S Brake/Reverse / Circle, A/D Steer / left analog, Q/E Shift down/up / L1/R1, C Handbrake / L2, Shift Nitrous / R2, Space Handbrake / Triangle, B Speedbreaker / Square, R Reset / Select, V View / R3, Esc Pause / Start, 1-4 D-pad, arrows freelook / right stick. These are *illustrative suggestions*, NOT input bindings actually installed in PCSX2. You must map every corresponding PS2 control yourself, and some labels may be cramped in game.

## Caveats
- PCSX2 matches textures by hash, and hashes might differ by game version. The files use the hashes supplied by the user, not validated hashes for every region. Only test the pack matching your own game serial.
- The original game control labels and connector lines rendered outside the atlas remain PS2-specific; they are not rewritten by this image alone.
- This is a conservative graphics proof of concept, not a completed native PC Controls menu or context-sensitive controller mapper.
- No BIOS, game image, proprietary texture bitmaps or game data are included.
'''
if not SRC:
    readme = readme.replace('plus the 10 original PC keyboard glyph textures from the already-shared 0.5b `driving-first` proof.',
        'without the 10 individual icons. Pass --source-poc <0.5b.zip> to include them. Activate as a separate theme only when you mean to replace an existing managed theme; theme switching removes files absent from the new theme.')
    readme = readme.replace('It includes the SAME 10 32x32 driving glyphs, so managed switching preserves them and adds the controller atlas as the 11th file.',
        'This atlas-only pack has no individual 32x32 glyphs. To preserve them, rebuild with --source-poc and activate the complete 11-image theme.')
(OUT/'README_FIRST.txt').write_text(readme,encoding='utf-8')
(OUT/'texture_inventory.json').write_text(json.dumps({'source':'user 97 image PCSX2 dump','diagramHash':ATLAS,'diagramDimensions':[256,256], 'includedPerRegion':11 if SRC else 1,'serials':SERIALS,'status':'unverified in-game; 32x32 cross icon validated separately'},indent=2)+'\n')
# Verify the exact PCSX2 name and alpha budget of the new image.
with Image.open(OUT/'previews'/'keyboard_controller_256x256.png') as atlas_check:
    assert atlas_check.size == (256, 256) and atlas_check.getchannel('A').getextrema()[1] <= 128
from zipfile import ZipFile,ZIP_DEFLATED
with ZipFile(str(OUT)+'.zip','w',compression=ZIP_DEFLATED) as archive:
  for p in OUT.rglob('*'):
   if p.is_file():archive.write(p,p.relative_to(OUT))
print('Wrote',str(OUT)+'.zip', 'with', 11 if SRC else 1, 'texture(s) per region')