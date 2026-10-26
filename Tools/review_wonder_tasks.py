#!/usr/bin/env python3
"""Read-only mission asset audit and annotated art review; never runs Unity or changes assets."""
import argparse
import ast
import colorsys
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
REVIEW = ROOT / "Docs/Checks/Mission-Task-Alignment"
ACTIONS = ["Onar", "Parlat", "Boya", "Aydınlat", "Temizle", "Açık yüzeyi parlat"]


def load_wonders():
    result = []
    for path in sorted((ROOT / "Assets/_Project/Settings/Wonders").glob("Wonder_mis*.asset")):
        text = path.read_text()
        prefix, rest = text.split("  tasks:\n", 1)
        body, suffix = rest.split("  starCosts:", 1)
        item = {"id": re.search(r"^  wonderId: (.+)", text, re.M)[1], "tasks": [],
                "outsideTasksSha256": hashlib.sha256((prefix + "  starCosts:" + suffix).encode()).hexdigest()}
        for chunk in body.split("  - name: ")[1:]:
            raw = chunk.split("\n", 1)[0]
            regions = [dict(zip(["x", "y", "width", "height"], map(float, m))) for m in re.findall(
                r"      x: (\S+)\n      y: (\S+)\n      width: (\S+)\n      height: (\S+)", chunk)]
            item["tasks"].append({"name": ast.literal_eval(raw) if raw.startswith('"') else raw,
                                  "action": int(re.search(r"    action: (\d+)", chunk)[1]),
                                  "iconGuid": re.search(r"    icon:.*guid: (\w+)", chunk)[1], "regions": regions})
        result.append(item)
    return result


def owner_at(item, x, y):
    # -1 means scenery: it regains colour smoothly throughout the sequence.
    owner, smallest = -1, float("inf")
    for index, task in enumerate(item["tasks"]):
        if task["action"] == 5:
            continue
        for r in task["regions"]:
            area = r["width"] * r["height"]
            if r["x"] <= x <= r["x"] + r["width"] and r["y"] <= y <= r["y"] + r["height"] and area < smallest:
                owner, smallest = index, area
    return owner


def legacy_owner_at(item, x, y):
    # The rejected first/last-task ownership, retained for honest before previews.
    fallback = max(i for i, t in enumerate(item["tasks"]) if not t["regions"])
    owner, smallest = fallback, float("inf")
    for index, task in enumerate(item["tasks"]):
        if task["action"] == 5 or index == fallback:
            continue
        for r in task["regions"]:
            area = r["width"] * r["height"]
            if r["x"] <= x <= r["x"] + r["width"] and r["y"] <= y <= r["y"] + r["height"] and area < smallest:
                owner, smallest = index, area
    return owner


def validate(items, before):
    localization = json.loads((ROOT / "Assets/_Project/Resources/Localization/tinyfixers_localization.json").read_text())
    languages = {l["code"]: {e["key"]: e["value"] for e in l["entries"]} for l in localization["languages"]}
    icons = {re.search(r"^guid: (\w+)", p.read_text(), re.M)[1]
             for p in (ROOT / "Assets/_Project/Art/UI/Missions/TaskIcons").glob("*.png.meta")}
    old = {w["id"]: w for w in before}
    changed = 0
    for w in items:
        previous = old[w["id"]]
        assert w["outsideTasksSha256"] == previous["outsideTasksSha256"], f"Non-task data changed: {w['id']}"
        assert len(w["tasks"]) == len(previous["tasks"]) == 8
        assert all(t["regions"] or t["action"] in (3, 4) for t in w["tasks"]), f"Missing work target: {w['id']}"
        assert sum(len(t["regions"]) for t in w["tasks"] if t["action"] != 5) <= 24
        for n, t in enumerate(w["tasks"]):
            assert 0 <= t["action"] <= 5 and t["iconGuid"] in icons
            key = f"wonder_{w['id']}_task_{n+1}"
            assert languages["tr"][key] == t["name"] and languages["en"][key]
            changed += t["name"] != previous["tasks"][n]["name"]
            for r in t["regions"]:
                assert 0 <= r["x"] < r["x"] + r["width"] <= 1.000001
                assert 0 <= r["y"] < r["y"] + r["height"] <= 1.000001
        coverage = [0] * len(w["tasks"])
        context = 0
        for y in range(160):
            for x in range(100):
                owner = owner_at(w, (x + .5) / 100, (y + .5) / 160)
                if owner < 0:
                    context += 1
                else:
                    coverage[owner] += 1
        for n, t in enumerate(w["tasks"]):
            assert coverage[n] > 0 or t["action"] in (3, 5), f"Empty reveal: {w['id']} task {n+1}"
        w["subjectCoveragePercent"] = [round(c / 160, 2) for c in coverage]
        w["contextCoveragePercent"] = round(context / 160, 2)
        # Equivalent colour contribution, not fully restored geometric area.
        w["approximateCoveragePercent"] = [round((c + context / len(coverage)) / 160, 2) for c in coverage]
        assert max(w["approximateCoveragePercent"]) < 30, f"Oversized reveal: {w['id']}"
        assert min(w["approximateCoveragePercent"]) > 2, f"Negligible reveal: {w['id']}"
        print(w["id"], w["approximateCoveragePercent"])
    rhodes = next(w for w in items if w["id"] == "mis8")
    assert owner_at(rhodes, .27, .84) == 3
    assert owner_at(rhodes, .26, .97) == 7
    print(f"PASS: 8 wonders / 64 tasks, {changed} renamed, TR/EN, icon/region coverage; non-task data unchanged.")


def export_images(items, before, out):
    from PIL import Image, ImageDraw, ImageFont
    font_path = "/System/Library/Fonts/Supplemental/Arial.ttf"
    try:
        font = ImageFont.truetype(font_path, 17)
    except OSError:
        font = ImageFont.load_default()
    old = {w["id"]: w for w in before}
    for w in items:
        photo = Image.open(ROOT / old[w["id"]]["image"]).convert("RGB")
        photo.thumbnail((360, 610))
        width, height = photo.size
        canvas = Image.new("RGB", (width * 3, height + 300), (22, 25, 33))
        draw = ImageDraw.Draw(canvas)
        for column, (label, definition) in enumerate([("Art", None), ("Before regions", old[w["id"]]), ("After regions", w)]):
            x = column * width
            canvas.paste(photo, (x, 32))
            draw.text((x + 6, 5), label, font=font, fill="white")
            if definition is None:
                continue
            for n, task in enumerate(definition["tasks"]):
                color = tuple(round(c * 255) for c in colorsys.hsv_to_rgb(n / 8, .8, 1))
                for r in task["regions"]:
                    box = (x + int(r["x"] * width), 32 + int((1 - r["y"] - r["height"]) * height),
                           x + int((r["x"] + r["width"]) * width), 32 + int((1 - r["y"]) * height))
                    draw.rectangle(box, outline=color, width=2)
                    draw.rectangle((box[0], box[1], box[0] + 19, box[1] + 20), fill=color)
                    draw.text((box[0] + 2, box[1]), str(n + 1), font=font, fill="black")
        draw.text((10, height + 40), f"{w['id']} — {old[w['id']]['name']}", font=font, fill="white")
        for n, task in enumerate(w["tasks"]):
            draw.text((10, height + 70 + n * 24), f"{n+1}. {task['name']} — {ACTIONS[task['action']]}", font=font, fill="white")
        canvas.save(out / f"{w['id']}.jpg", quality=90)
        export_progression(w, old[w["id"]], out, font)


def export_progression(wonder, original, out, font):
    from PIL import Image, ImageDraw, ImageOps
    photo = Image.open(ROOT / original["image"]).convert("RGB")
    photo.thumbnail((220, 365))
    width, height = photo.size
    weathered = ImageOps.colorize(ImageOps.grayscale(photo), (38, 31, 24), (151, 125, 88))
    owners = [owner_at(wonder, (x + .5) / width, 1 - (y + .5) / height)
              for y in range(height) for x in range(width)]
    tile_height = height + 66
    sheet = Image.new("RGB", (width * 3, tile_height * 3 + 62), (22, 25, 33))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 5), original["name"] + " — 0–8 adım", font=font, fill="white")
    draw.text((8, 30), "Maske önizlemesi; Unity/GPU ekran görüntüsü değildir.", font=font, fill=(185, 190, 200))
    for stage in range(9):
        mask = Image.frombytes("L", photo.size, bytes(round(255 * stage / 8) if owner < 0 else 255 if owner < stage else 0 for owner in owners))
        frame = Image.composite(photo, weathered, mask)
        x, y = stage % 3 * width, stage // 3 * tile_height + 62
        sheet.paste(frame, (x, y))
        draw.text((x + 6, y + height + 4), f"Adım {stage}/8", font=font, fill="white")
        label = "Başlangıç" if stage == 0 else wonder["tasks"][stage - 1]["name"]
        words, line, lines = label.split(), "", []
        for word in words:
            if len(line) + len(word) > 23:
                lines.append(line)
                line = word
            else:
                line = (line + " " + word).strip()
        lines.append(line)
        for row, text in enumerate(lines[:2]):
            draw.text((x + 6, y + height + 25 + row * 18), text, font=font, fill=(190, 200, 215))
    sheet.save(out / f"{wonder['id']}-progression.jpg", quality=92)

    # Compare exactly the state rejected by the user, including the previous names/regions.
    previous = next(item for item in json.loads((REVIEW / "Before-Progression.json").read_text()) if item["id"] == wonder["id"])
    old_owners = [legacy_owner_at(previous, (x + .5) / width, 1 - (y + .5) / height)
                  for y in range(height) for x in range(width)]
    old_mask = Image.frombytes("L", photo.size, bytes(255 if owner == 0 else 0 for owner in old_owners))
    new_mask = Image.frombytes("L", photo.size, bytes(32 if owner < 0 else 255 if owner == 0 else 0 for owner in owners))
    comparison = Image.new("RGB", (width * 2, height + 65), (22, 25, 33))
    comparison.paste(Image.composite(photo, weathered, old_mask), (0, 35))
    comparison.paste(Image.composite(photo, weathered, new_mask), (width, 35))
    draw = ImageDraw.Draw(comparison)
    old_percent = 100 * old_owners.count(0) / len(old_owners)
    draw.text((5, 6), f"Eski 1. adım: %{old_percent:.1f}", font=font, fill="white")
    draw.text((width + 5, 6), "Yeni 1. adım", font=font, fill="white")
    draw.text((5, height + 40), "Yaklaşık maske önizlemesi; Unity kaydı değildir.", font=font, fill=(185, 190, 200))
    comparison.save(out / f"{wonder['id']}-first-step.jpg", quality=92)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, help="Write JSON and annotated art; these are not GPU screenshots.")
    args = parser.parse_args()
    before = json.loads((REVIEW / "Before.json").read_text())
    items = load_wonders()
    validate(items, before)
    if args.export:
        args.export.mkdir(parents=True, exist_ok=True)
        (args.export / "After.json").write_text(json.dumps(items, ensure_ascii=False, indent=2) + "\n")
        export_images(items, before, args.export)


if __name__ == "__main__":
    main()
