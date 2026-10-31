#!/usr/bin/env python3
"""Bake small restoration textures offline; no Unity, runtime blur or source-art changes.

python3 -B Tools/bake_wonder_blur.py [--check] [--preview]
Requires Pillow. The checked-in PNGs are normal non-readable Texture2D assets.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import uuid

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets/_Project/Art/UI/Missions"
OUT = ASSETS / "BlurredPreviews"
REVIEW = ROOT / "Docs/Checks/Missions-Blur"
MAX_EDGE = 512
RADIUS_FRACTION = 0.032


def guid_for(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, "tinyfixers/" + str(path.relative_to(ROOT))).hex


def meta_text(path):
    text = f"""fileFormatVersion: 2
guid: {guid_for(path)}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: 512
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 0
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  compressionQuality: 50
  spriteMode: 0
  alphaUsage: 0
  alphaIsTransparency: 0
  textureType: 0
  textureShape: 1
  platformSettings:
"""
    for target in ("DefaultTexturePlatform", "Standalone", "iOS", "Android"):
        mobile = target in ("iOS", "Android")
        text += f"""  - serializedVersion: 4
    buildTarget: {target}
    maxTextureSize: 512
    resizeAlgorithm: 0
    textureFormat: {50 if mobile else -1}
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: {int(mobile)}
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
"""
    return text + "  userData: WonderBlur-v1; offline; max512; sigma0.032\n  assetBundleName: \n  assetBundleVariant: \n"


def sources():
    paths = {}
    for meta in ASSETS.rglob("*.png.meta"):
        match = re.search(r"^guid: (\w+)", meta.read_text(), re.M)
        if match:
            paths[match[1]] = Path(str(meta)[:-5])
    for definition in sorted((ROOT / "Assets/_Project/Settings/Wonders").glob("Wonder_mis*.asset")):
        text = definition.read_text()
        ident = re.search(r"^  wonderId: (\S+)", text, re.M)[1]
        source = paths[re.search(r"  backgroundSprite:.*guid: (\w+)", text)[1]]
        # Current production art uses full-image Single sprites. Reject atlas/crop changes
        # rather than silently generating a texture with different sprite-local coordinates.
        assert "  spriteMode: 1\n" in Path(str(source) + ".meta").read_text(), source
        yield definition, text, ident, source


def check_importer(path):
    # Unity may serialize additional defaults on import; validate semantics, not text layout.
    meta = Path(str(path) + ".meta").read_text()
    assert re.search(r"^guid: (\w+)", meta, re.M)[1] == guid_for(path)
    for key, value in (("enableMipMap", 0), ("sRGBTexture", 1), ("isReadable", 0),
                       ("textureType", 0), ("filterMode", 1), ("wrapU", 1), ("wrapV", 1)):
        assert re.search(r"^\s+" + key + r": (\d+)", meta, re.M)[1] == str(value), (path, key)
    for platform in ("iOS", "Android"):
        block = re.search(r"    buildTarget: " + platform + r"\n(.*?)(?=\n  - serializedVersion:|\Z)", meta, re.S)[1]
        assert "    textureFormat: 50\n" in block and "    overridden: 1\n" in block
        assert "    maxTextureSize: 512\n" in block


def bake(source):
    with Image.open(source) as im:
        image = im.convert("RGB")
    image.thumbnail((MAX_EDGE, MAX_EDGE), Image.Resampling.LANCZOS)
    image = image.filter(ImageFilter.GaussianBlur(max(image.size) * RADIUS_FRACTION))
    image = ImageEnhance.Color(image).enhance(0.65)
    image = ImageEnhance.Contrast(image).enhance(0.72)
    return ImageEnhance.Brightness(image).enhance(0.85)


def write_if_changed(path, value):
    data = value.encode() if isinstance(value, str) else value
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)


def preview(items):
    # Static implementation preview: existing task ownership, without shader edge wobble,
    # welding, patina noise, UI tint or compression. Not a Unity screenshot or GPU test.
    from review_wonder_tasks import load_wonders, owner_at
    definitions = {w["id"]: w for w in load_wonders()}
    font = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 19)
    panels = []
    for item in items:
        source = ROOT / item["source"]
        with Image.open(source) as im:
            photo = im.convert("RGB")
        photo.thumbnail((300, 480), Image.Resampling.LANCZOS)
        w, h = photo.size
        with Image.open(ROOT / item["texture"]) as im:
            blurred = im.resize(photo.size, Image.Resampling.BILINEAR).convert("RGB")
        wonder = definitions[item["id"]]
        mask = Image.new("L", photo.size)
        # Three completed tasks: subject parts are sharp; scenery follows existing 3/N fade.
        stage = 3
        context = round(255 * stage / len(wonder["tasks"]))
        mask.putdata([context if (owner := owner_at(wonder, (x + .5) / w, 1 - (y + .5) / h)) < 0
                      else 255 if owner < stage else 0 for y in range(h) for x in range(w)])
        partial = Image.composite(photo, blurred, mask)
        panel = Image.new("RGB", (w * 3, h + 65), "#1b2029")
        draw = ImageDraw.Draw(panel)
        for x, label, image in [(0, "Başlangıç / blur", blurred), (w, "3 görev sonrası", partial), (w * 2, "Tamamlanmış", photo)]:
            panel.paste(image, (x, 55))
            draw.text((x + 8, 30), label, font=font, fill="white")
        draw.text((8, 5), item["id"] + " · statik önizleme", font=font, fill="white")
        panel.save(REVIEW / (item["id"] + ".jpg"), quality=90)
        panels.append(panel)
    contact = Image.new("RGB", (panels[0].width, sum(p.height for p in panels)), "#1b2029")
    y = 0
    for p in panels:
        contact.paste(p, (0, y))
        y += p.height
    contact.save(REVIEW / "All-Missions.jpg", quality=90)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Read-only: validate assets and reference wiring")
    parser.add_argument("--preview", action="store_true", help="Export static progression contact sheet")
    args = parser.parse_args()
    if not args.check:
        OUT.mkdir(parents=True, exist_ok=True)
        write_if_changed(Path(str(OUT) + ".meta"), f"fileFormatVersion: 2\nguid: {guid_for(OUT)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
        REVIEW.mkdir(parents=True, exist_ok=True)
    items = []
    for definition, text, ident, source in sources():
        path = OUT / (ident + "-blur.png")
        reference = f"  unrestoredTexture: {{fileID: 2800000, guid: {guid_for(path)}, type: 3}}\n"
        expected = bake(source)
        if args.check:
            assert reference in text, f"Missing/wrong reference: {definition}"
            check_importer(path)
            with Image.open(path) as actual:
                assert actual.mode == "RGB" and actual.size == expected.size
                assert actual.tobytes() == expected.tobytes(), f"Stale baked texture: {path}"
        else:
            import io
            encoded = io.BytesIO()
            expected.save(encoded, format="PNG", optimize=True)
            write_if_changed(path, encoded.getvalue())
            write_if_changed(Path(str(path) + ".meta"), meta_text(path))
            updated = re.sub(r"^  unrestoredTexture:.*\n", "", text, flags=re.M)
            updated, count = re.subn(r"(^  backgroundSprite:.*\n)", lambda m: m[0] + reference, updated, flags=re.M)
            assert count == 1, definition
            write_if_changed(definition, updated)
        w, h = expected.size
        items.append(dict(id=ident, source=str(source.relative_to(ROOT)), texture=str(path.relative_to(ROOT)),
                          sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(), width=w, height=h,
                          pngBytes=path.stat().st_size, rgba32Bytes=w*h*4,
                          astc6x6Bytes=math.ceil(w/6)*math.ceil(h/6)*16))
    assert len(items) == 8
    if not args.check:
        write_if_changed(REVIEW / "Manifest.json", json.dumps(items, indent=2, ensure_ascii=False) + "\n")
        if args.preview:
            preview(items)
    print(f"PASS: {len(items)} baked textures; largest edge <= {MAX_EDGE}; no mipmaps/CPU readback.")
    print(f"PNG files: {sum(i['pngBytes'] for i in items):,} bytes; ASTC 6x6 payload: {sum(i['astc6x6Bytes'] for i in items):,} bytes; RGBA32 upper fallback: {sum(i['rgba32Bytes'] for i in items):,} bytes.")


if __name__ == "__main__":
    main()
