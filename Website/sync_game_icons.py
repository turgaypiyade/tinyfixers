#!/usr/bin/env python3
"""Export current game-library icons for the website; never changes Unity assets.

Run from any directory: python3 Website/sync_game_icons.py
Requires Pillow. Sources are resolved by GUID, not guessed from asset filenames.
"""
import hashlib
import json
from pathlib import Path
import re

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parent
LIBRARY = PROJECT / "Assets/_Project/Resources/TileIconLibrary_Main.asset"
ICONS = {
    "tile-gear.webp": "gear", "tile-core.webp": "core",
    "tile-bolt.webp": "bolt", "tile-plate.webp": "plate",
    "sp-rocket.webp": "lineH", "sp-patchbot.webp": "patchBotAll",
    "sp-pulse.webp": "pulseCore", "sp-spark.webp": "overrideRed",
    "bo-hammer.webp": "boosterHammer", "bo-drill.webp": "boosterRow",
    "bo-elevator.webp": "boosterColumn", "bo-shuffle.webp": "boosterShuffle",
    "bo-paint.webp": "boosterPaintBucket",
}


def sync():
    library = LIBRARY.read_text(encoding="utf-8")
    references = {}
    for field in ICONS.values():
        match = re.search(r"^  " + field + r": \{fileID: 21300000, guid: ([0-9a-f]{32}), type: 3\}", library, re.M)
        if not match:
            raise ValueError(f"Unsupported/missing sprite reference: {field}")
        references[field] = match[1]
    sources = {}
    for meta in (PROJECT / "Assets").rglob("*.png.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8"), re.M)
        if match and match[1] in references.values():
            sources[match[1]] = Path(str(meta)[:-5])
    # Resolve every source before touching website files.
    missing = set(references.values()) - set(sources)
    if missing:
        raise ValueError(f"Missing icon sources: {sorted(missing)}")
    report = []
    html = (ROOT / "index.html").read_text(encoding="utf-8")
    for filename, field in ICONS.items():
        source = sources[references[field]]
        with Image.open(source) as original:
            image = original.convert("RGBA")
            bounds = image.getchannel("A").getbbox()
            if not bounds:
                raise ValueError(f"Empty icon: {source}")
            image = ImageOps.contain(image.crop(bounds), (288, 288), Image.Resampling.LANCZOS)
            canvas = Image.new("RGBA", (320, 320), (0, 0, 0, 0))
            canvas.paste(image, ((320-image.width)//2, (320-image.height)//2))
            destination = ROOT / "img" / filename
            canvas.save(destination, "WEBP", lossless=True, method=6, exact=True)
        sha = hashlib.sha256(destination.read_bytes()).hexdigest()
        # A content version keeps the browser from showing an old cached icon.
        html = re.sub(r"/img/" + re.escape(filename) + r'(?:\?v=[0-9a-f]+)?(?=["\s])',
                      f"/img/{filename}?v={sha[:12]}", html)
        report.append({"file": "Website/img/" + filename, "libraryField": field,
                       "source": source.relative_to(PROJECT).as_posix(),
                       "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                       "exportSha256": sha, "size": [320, 320], "lossless": True})
    (ROOT / "index.html").write_text(html, encoding="utf-8")
    target = PROJECT / "Docs/Website/Game-Icon-Sources.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    print(f"Updated {len(sync())} website icons from TileIconLibrary_Main. No Unity changes or deploy.")
