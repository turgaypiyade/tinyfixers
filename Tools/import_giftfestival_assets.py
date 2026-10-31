"""Copy supplied Gift Festival artwork unchanged and create deterministic Unity sprite metadata.

No raster conversion or repainting: rectangles below only describe Unity sprite slicing.
Run from the repository root. Existing GUIDs are preserved.
"""
from pathlib import Path
import hashlib
import json
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/_Project/Art/UI/MainScreenEvents/GiftFestival"
DOWNLOADS = Path("/Users/turgayp/Downloads")
CLIPBOARD = Path("/var/folders/54/y51scw3j7cj3198kjy1bv4cw0000gn/T")

SOURCES = {
    "References/OriginalComposition.png": CLIPBOARD / "codex-clipboard-Dafvop.png",
    "TopSign.png": CLIPBOARD / "codex-clipboard-YTOOCQ.png",
    "References/Board_Original.png": CLIPBOARD / "codex-clipboard-gafdpN.png",
    "Background.png": CLIPBOARD / "codex-clipboard-W7JeaY.png",
    "References/Rabbit_OriginalSheet.png": CLIPBOARD / "codex-clipboard-puzbb4.png",
    "References/Rabbit_Intermediate1.png": DOWNLOADS / "Beyzbolara1.png",
    "References/Rabbit_Intermediate2.png": DOWNLOADS / "Beyzbolara2.png",
    "References/Bear_OriginalSheet.png": CLIPBOARD / "codex-clipboard-mOFB6r.png",
    "Board.png": CLIPBOARD / "codex-clipboard-y6fmIN.png",
    "Glass.png": CLIPBOARD / "codex-clipboard-wBQpA1.png",
    "References/GlassOverlay_Original.png": CLIPBOARD / "codex-clipboard-8PsgAV.png",
    "MetalCorners.png": CLIPBOARD / "codex-clipboard-keBoAS.png",
    "GiftSymbol.png": CLIPBOARD / "codex-clipboard-0wymik.png",
    "TargetFrame.png": CLIPBOARD / "codex-clipboard-zhhCoo.png",
    "References/Rabbit_Release_Original.png": DOWNLOADS / "RB5.png",
    "Rabbit/RB5V1.png": DOWNLOADS / "RB5V1.png",
    "Baseball.png": DOWNLOADS / "Baseball_ball.png",
    "BaseballSpin.png": DOWNLOADS / "baseball_balls.png",
    "HitButton.png": DOWNLOADS / "Hitbutton.png",
    "HitFrame.png": DOWNLOADS / "Hitframe.png",
    "HitPressed.png": DOWNLOADS / "HitPressed.png",
}
SOURCES.update({f"Bear/BBB{i}.png": DOWNLOADS / f"BBB{i}.png" for i in range(1, 7)})
SOURCES.update({f"Rabbit/RB{i}.png": DOWNLOADS / f"RB{i}.png" for i in range(1, 5)})
for part in ("Assembly", "Frame", "Indicator", "Zones"):
    SOURCES[f"Vectors/TimingBar_{part}.svg"] = DOWNLOADS / "Baseball_TimingBar_Editable_Vectors" / f"TimingBar_{part}.svg"


def guid_for(path):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        match = re.search(r"^guid: (\w+)", meta.read_text(), re.M)
        if match:
            return match.group(1)
    return uuid.uuid5(uuid.NAMESPACE_URL, "tinyfixers/giftfestival/" + str(path.relative_to(ROOT))).hex


def folder(path):
    path.mkdir(parents=True, exist_ok=True)
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid_for(path)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def sprites_for(name, width, height):
    # All rectangles use top-left artwork coordinates; Unity conversion is below.
    if name == "TopSign.png":
        return [("TopSign", (0, 228, width, 448))]
    if name == "Glass.png":
        return [("Glass", (140, 155, 978, 944))]
    if name == "GiftSymbol.png":
        return [("GiftSymbol", (233, 217, 790, 836))]
    if name == "Baseball.png":
        return [("Baseball", (104, 104, 1060, 1080))]
    if name == "BaseballSpin.png":
        # Independent bounds rather than slicing the whole canvas into five equal cells.
        return [(f"Spin{i}", (x, 133, 426, 440)) for i, x in enumerate((5, 437, 865, 1302, 1742))]
    if name == "MetalCorners.png":
        return [(n, (x, y, 240, 240)) for n, x, y in (
            ("TopLeft", 40, 88), ("TopRight", 974, 88),
            ("BottomLeft", 40, 918), ("BottomRight", 974, 918))]
    if name == "TargetFrame.png":
        return [(n, (x, y, 380, 358)) for n, x, y in (
            ("TopLeft", 44, 50), ("TopRight", 831, 50),
            ("BottomLeft", 44, 838), ("BottomRight", 831, 838))]
    if name == "HitButton.png":
        return [("HitButton", (218, 146, 1014, 780))]
    if name == "HitPressed.png":
        # Same crop/canvas as normal: the shorter lower lip is the pressed state.
        return [("HitPressed", (218, 146, 1014, 780))]
    if name == "HitFrame.png":
        return [("HitFrame", (111, 55, 1226, 964))]
    return [(Path(name).stem, (0, 0, width, height))]


def texture_meta(path, name):
    from PIL import Image
    width, height = Image.open(path).size
    sprites = sprites_for(name, width, height)
    guid = guid_for(path)
    # UI textures: no mipmaps/readback/physics, clamp, retain original NPOT proportions.
    text = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable:
"""
    for i, (sprite_name, _) in enumerate(sprites):
        text += f"  - first:\n      213: {21300000+i}\n    second: {sprite_name}\n"
    text += """  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  compressionQuality: 75
  spriteMode: 2
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 75
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites:
"""
    for i, (sprite_name, (x, y, w, h)) in enumerate(sprites):
        assert x >= 0 and y >= 0 and x+w <= width and y+h <= height, (name, sprite_name)
        sprite_id = uuid.uuid5(uuid.NAMESPACE_URL, f"{guid}/{sprite_name}").hex
        text += f"""    - serializedVersion: 2
      name: {sprite_name}
      rect:
        serializedVersion: 2
        x: {x}
        y: {height-y-h}
        width: {w}
        height: {h}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      bones: []
      spriteID: {sprite_id}
      internalID: {21300000+i}
      vertices: []
      indices: 
      edges: []
      weights: []
"""
    text += "    outline: []\n    physicsShape: []\n    bones: []\n    secondaryTextures: []\n    nameFileIdTable:\n"
    for i, (sprite_name, _) in enumerate(sprites):
        text += f"      {sprite_name}: {21300000+i}\n"
    text += "  userData: GiftFestival supplied artwork; original pixels preserved\n  assetBundleName: \n  assetBundleVariant: \n"
    Path(str(path)+".meta").write_text(text)


def main():
    missing = [str(p) for p in SOURCES.values() if not p.is_file()]
    if missing:
        raise FileNotFoundError("Missing source artwork: " + ", ".join(missing))
    folder(ART)
    manifest = []
    for name, source in SOURCES.items():
        target = ART / name
        folder(target.parent)
        if target.exists() and target.read_bytes() != source.read_bytes():
            raise RuntimeError(f"Refusing to overwrite different artwork: {target}")
        if not target.exists():
            shutil.copy2(source, target)
        if target.suffix == ".png":
            texture_meta(target, name)
        elif not Path(str(target)+".meta").exists():
            Path(str(target)+".meta").write_text(f"fileFormatVersion: 2\nguid: {guid_for(target)}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: Vector design source; rendered with native UI geometry\n  assetBundleName: \n  assetBundleVariant: \n")
        manifest.append({"file": name, "source": str(source), "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "referenceOnly": name.startswith("References/")})
    (ART / "AssetSources.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n")
    print(f"Copied and verified {len(manifest)} supplied assets into {ART.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
