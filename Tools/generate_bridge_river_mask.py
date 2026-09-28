#!/usr/bin/env python3
"""Bake numerical shader mask data from traced polygons, without editing the artwork.

Coordinates refer to BridgeRepairBG (941 x 1671), origin at top left.
Requires Pillow. Run from any directory; the result is deterministic.
"""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/_Project/Art/UI/MainScreenEvents/BridgeRepair"
SIZE = (471, 836)
SOURCE_SIZE = (941, 1671)

# Conservative shoreline: sky, distant arches, waterfall and foreground stay still.
RIVER = [
    (360,448),(410,440),(576,436),(633,433),(625,465),(630,493),
    (705,514),(740,533),(735,604),(702,654),(714,706),(723,760),
    (728,827),(719,871),(734,908),(731,955),(719,984),(697,1036),
    (681,1078),(669,1113),(681,1142),(680,1183),(657,1230),(642,1260),
    (228,1260),(203,1231),(198,1206),(211,1185),(209,1148),(208,1107),
    (206,1067),(200,1053),(183,1019),(184,989),(193,963),(207,938),
    (211,899),(216,870),(230,832),(253,802),(264,743),(288,701),
    (304,669),(306,623),(303,581),(301,552),(305,529),(341,527),
    (350,520),(348,506),(337,495),(324,480),(365,461)
]
STILL_OBJECTS = {
    "distant_islet": [(351,518),(360,494),(377,478),(395,485),(411,519)],
    "tiny_islet": [(312,531),(317,510),(328,501),(343,530)],
    "left_upper_islet": [(329,604),(339,574),(350,552),(368,542),
                         (390,553),(398,582),(413,604)],
    "right_upper_islet": [(612,571),(627,545),(636,523),(663,509),
                          (684,522),(694,549),(711,573)],
    "far_boat": [(576,487),(580,468),(591,443),(601,426),(611,445),(628,489)],
    "near_boat": [(463,638),(466,614),(482,594),(505,529),(516,527),
                  (541,598),(551,612),(547,636)],
    "right_middle_islet": [(601,730),(607,697),(623,683),(630,661),
                           (649,642),(674,646),(693,665),(705,691),(721,731)],
    "left_middle_island": [(248,812),(256,780),(274,752),(278,718),
                           (298,697),(322,677),(344,672),(372,700),
                           (385,737),(400,767),(418,812)],
    "right_lower_island": [(602,957),(609,920),(626,888),(641,856),
                           (668,841),(694,850),(711,879),(731,915),(751,957)],
    "middle_lower_islet": [(529,981),(539,945),(548,922),(573,908),
                          (594,925),(609,950),(623,981)],
    "left_lower_island": [(192,1058),(205,1023),(222,994),(230,972),
                          (255,952),(278,963),(295,995),(310,1031),(328,1058)],
    "left_foreground_island": [(228,1261),(241,1223),(264,1195),(275,1161),
                               (300,1137),(322,1134),(348,1158),(361,1195),
                               (382,1232),(391,1261)],
    "right_foreground_islet": [(579,1205),(589,1176),(606,1148),(626,1125),
                               (649,1130),(667,1152),(682,1180),(689,1205)]
}


def scaled(points):
    return [(round(x * SIZE[0] / SOURCE_SIZE[0]),
             round(y * SIZE[1] / SOURCE_SIZE[1])) for x, y in points]


def main():
    with Image.open(ART / "BridgeRepairBG.png") as source:
        assert source.size == SOURCE_SIZE, "Artwork changed; retrace mask before baking."
    hard = Image.new("L", SIZE, 0)
    draw = ImageDraw.Draw(hard)
    draw.polygon(scaled(RIVER), fill=255)
    for points in STILL_OBJECTS.values():
        draw.polygon(scaled(points), fill=0)
    # Feather inward, keeping every excluded pixel exactly black.
    soft = hard.filter(ImageFilter.MinFilter(11)).filter(ImageFilter.GaussianBlur(2))
    mask = ImageChops.multiply(soft, hard)
    for name, points in STILL_OBJECTS.items():
        probe = Image.new("L", SIZE, 0)
        ImageDraw.Draw(probe).polygon(scaled(points), fill=255)
        assert ImageChops.multiply(mask, probe).getbbox() is None, name
    for point in [(470,100),(510,580),(600,460),(335,740),(470,1450)]:
        assert mask.getpixel(scaled([point])[0]) == 0, point
    assert mask.getpixel(scaled([(480,850)])[0]) > 240, "Open river should flow."
    target = ART / "BridgeRepairRiverMask.png"
    mask.save(target, optimize=True)
    print(f"Saved {target.relative_to(ROOT)} ({SIZE[0]}x{SIZE[1]}).")
    print(f"Protected all {len(STILL_OBJECTS)} object silhouettes, sky and podium.")


if __name__ == "__main__":
    main()
