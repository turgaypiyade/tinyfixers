#!/usr/bin/env python3
import html
import re
import struct
import zlib
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
LEVEL_DIR = ROOT / "Assets/_Project/Settings/ProductionLevels"
OUT_DIR = ROOT / "Exports/LevelPreviews"
IMG_DIR = OUT_DIR / "images"
XLSX_PATH = OUT_DIR / "LevelP_Previews.xlsx"


OBSTACLE_LABELS = {
    1: "ST", 2: "S1", 3: "S2", 4: "C1", 5: "C2", 6: "C3",
    7: "PL", 8: "PO", 9: "PY", 10: "PV", 11: "PB", 12: "PR",
    13: "PG", 14: "OL", 20: "B4", 21: "CC", 22: "EC", 23: "BB",
    24: "OM", 25: "MD", 26: "TU", 27: "WR", 28: "SC", 29: "MG",
    30: "HP", 31: "HL", 32: "GM", 33: "SF", 34: "CG", 35: "PS",
    36: "ES", 37: "BR", 38: "RB", 39: "SS", 40: "P2", 41: "B2",
}

OBSTACLE_COLORS = {
    1: (142, 142, 152), 2: (92, 140, 190), 3: (70, 110, 170),
    4: (180, 118, 62), 5: (154, 92, 48), 6: (122, 70, 40),
    7: (166, 160, 178), 8: (231, 132, 48), 9: (239, 208, 64),
    10: (85, 156, 188), 11: (74, 126, 217), 12: (216, 77, 84),
    13: (74, 168, 95), 14: (44, 40, 38), 20: (94, 88, 98),
    21: (132, 82, 188), 22: (58, 183, 210), 23: (245, 183, 63),
    24: (112, 210, 226), 25: (122, 91, 58), 26: (110, 170, 190),
    27: (166, 112, 66), 28: (132, 132, 132), 29: (210, 72, 92),
    30: (226, 226, 230), 31: (208, 176, 88), 32: (246, 202, 62),
    33: (96, 104, 124), 34: (170, 104, 64), 35: (80, 176, 230),
    36: (214, 88, 116), 37: (145, 94, 46), 38: (194, 76, 62),
    39: (116, 96, 154), 40: (170, 150, 206), 41: (126, 78, 40),
}

TILE_COLORS = {
    0: (245, 200, 64),   # Gear
    1: (225, 80, 90),    # Core
    2: (72, 142, 226),   # Bolt
    3: (88, 190, 100),   # Plate
    4: (206, 206, 214),  # Normal
    5: (250, 152, 48),   # LineEmitter_H
    6: (80, 204, 214),   # LineEmitter_V
    7: (172, 104, 218),  # PatchBot
    8: (70, 70, 86),     # SystemOverride
}

SPECIAL_LABELS = {1: "H", 2: "V", 3: "P", 4: "U", 5: "O"}

FONT = {
    " ": ["000", "000", "000", "000", "000", "000", "000"],
    "-": ["000", "000", "000", "111", "000", "000", "000"],
    "_": ["000", "000", "000", "000", "000", "000", "111"],
    "0": ["111", "101", "101", "101", "101", "101", "111"],
    "1": ["010", "110", "010", "010", "010", "010", "111"],
    "2": ["111", "001", "001", "111", "100", "100", "111"],
    "3": ["111", "001", "001", "111", "001", "001", "111"],
    "4": ["101", "101", "101", "111", "001", "001", "001"],
    "5": ["111", "100", "100", "111", "001", "001", "111"],
    "6": ["111", "100", "100", "111", "101", "101", "111"],
    "7": ["111", "001", "001", "010", "010", "100", "100"],
    "8": ["111", "101", "101", "111", "101", "101", "111"],
    "9": ["111", "101", "101", "111", "001", "001", "111"],
    "A": ["010", "101", "101", "111", "101", "101", "101"],
    "B": ["110", "101", "101", "110", "101", "101", "110"],
    "C": ["111", "100", "100", "100", "100", "100", "111"],
    "D": ["110", "101", "101", "101", "101", "101", "110"],
    "E": ["111", "100", "100", "110", "100", "100", "111"],
    "F": ["111", "100", "100", "110", "100", "100", "100"],
    "G": ["111", "100", "100", "101", "101", "101", "111"],
    "H": ["101", "101", "101", "111", "101", "101", "101"],
    "I": ["111", "010", "010", "010", "010", "010", "111"],
    "J": ["001", "001", "001", "001", "101", "101", "111"],
    "K": ["101", "101", "110", "100", "110", "101", "101"],
    "L": ["100", "100", "100", "100", "100", "100", "111"],
    "M": ["101", "111", "111", "101", "101", "101", "101"],
    "N": ["101", "111", "111", "111", "111", "111", "101"],
    "O": ["111", "101", "101", "101", "101", "101", "111"],
    "P": ["111", "101", "101", "111", "100", "100", "100"],
    "Q": ["111", "101", "101", "101", "111", "001", "001"],
    "R": ["110", "101", "101", "110", "110", "101", "101"],
    "S": ["111", "100", "100", "111", "001", "001", "111"],
    "T": ["111", "010", "010", "010", "010", "010", "010"],
    "U": ["101", "101", "101", "101", "101", "101", "111"],
    "V": ["101", "101", "101", "101", "101", "101", "010"],
    "W": ["101", "101", "101", "101", "111", "111", "101"],
    "X": ["101", "101", "101", "010", "101", "101", "101"],
    "Y": ["101", "101", "101", "010", "010", "010", "010"],
    "Z": ["111", "001", "001", "010", "100", "100", "111"],
}


class Canvas:
    def __init__(self, width, height, bg=(255, 255, 255, 255)):
        self.width = width
        self.height = height
        self.pixels = bytearray(bg * (width * height))

    def rect(self, x, y, w, h, color):
        r, g, b, a = color
        x0 = max(0, int(x)); y0 = max(0, int(y))
        x1 = min(self.width, int(x + w)); y1 = min(self.height, int(y + h))
        for yy in range(y0, y1):
            base = (yy * self.width + x0) * 4
            for _ in range(x0, x1):
                self.pixels[base:base + 4] = bytes((r, g, b, a))
                base += 4

    def outline(self, x, y, w, h, color, thickness=1):
        self.rect(x, y, w, thickness, color)
        self.rect(x, y + h - thickness, w, thickness, color)
        self.rect(x, y, thickness, h, color)
        self.rect(x + w - thickness, y, thickness, h, color)

    def text(self, x, y, value, color=(30, 30, 36, 255), scale=2):
        cursor = int(x)
        for ch in str(value).upper():
            glyph = FONT.get(ch, FONT[" "])
            for gy, row in enumerate(glyph):
                for gx, bit in enumerate(row):
                    if bit == "1":
                        self.rect(cursor + gx * scale, y + gy * scale, scale, scale, color)
            cursor += (4 * scale)

    def centered_text(self, x, y, w, h, value, color=(255, 255, 255, 255), scale=2):
        value = str(value).upper()
        text_w = max(0, len(value) * 4 * scale - scale)
        text_h = 7 * scale
        self.text(x + (w - text_w) // 2, y + (h - text_h) // 2, value, color, scale)

    def save_png(self, path):
        raw = bytearray()
        stride = self.width * 4
        for y in range(self.height):
            raw.append(0)
            raw.extend(self.pixels[y * stride:(y + 1) * stride])
        compressed = zlib.compress(bytes(raw), 9)
        def chunk(kind, data):
            crc = zlib.crc32(kind + data) & 0xffffffff
            return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", crc)
        data = b"\x89PNG\r\n\x1a\n"
        data += chunk(b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 6, 0, 0, 0))
        data += chunk(b"IDAT", compressed)
        data += chunk(b"IEND", b"")
        Path(path).write_bytes(data)


def int_field(text, name, default=0):
    m = re.search(rf"^  {re.escape(name)}:\s*(-?\d+)", text, re.MULTILINE)
    return int(m.group(1)) if m else default


def str_field(text, name, default=""):
    m = re.search(rf"^  {re.escape(name)}:\s*(.+)$", text, re.MULTILINE)
    return m.group(1).strip() if m else default


def int_array(text, name, size, default=0):
    m = re.search(rf"^  {re.escape(name)}:\s*([0-9a-fA-F]*)\s*$", text, re.MULTILINE)
    if not m or not m.group(1):
        return [default] * size
    blob = bytes.fromhex(m.group(1))
    count = min(size, len(blob) // 4)
    values = [struct.unpack_from("<i", blob, i * 4)[0] for i in range(count)]
    if len(values) < size:
        values.extend([default] * (size - len(values)))
    return values[:size]


def parse_entries(text, key):
    lines = text.splitlines()
    start = None
    for i, line in enumerate(lines):
        if line == f"  {key}: []":
            return []
        if line == f"  {key}:":
            start = i + 1
            break
    if start is None:
        return []

    entries = []
    current = None
    for line in lines[start:]:
        if line.startswith("  ") and not line.startswith("  -") and re.match(r"^  [A-Za-z_][A-Za-z0-9_]*:", line):
            break
        stripped = line.strip()
        if stripped.startswith("- "):
            if current:
                entries.append(current)
            current = {}
            stripped = stripped[2:]
        if current is None:
            continue
        if ":" in stripped:
            k, v = stripped.split(":", 1)
            v = v.strip()
            if re.fullmatch(r"-?\d+", v):
                current[k] = int(v)
    if current:
        entries.append(current)
    return entries


def load_level(path):
    text = path.read_text(encoding="utf-8")
    width = int_field(text, "width", 9)
    height = int_field(text, "height", 9)
    size = width * height
    match = re.search(r"LevelP_(\d+)", path.stem)
    number = int(match.group(1)) if match else 0
    return {
        "path": path,
        "number": number,
        "id": str_field(text, "m_Name", path.stem),
        "width": width,
        "height": height,
        "moves": int_field(text, "moves", 0),
        "cells": int_array(text, "cells", size, 1),
        "obstacles": int_array(text, "obstacles", size, 0),
        "origins": int_array(text, "obstacleOrigins", size, -1),
        "pinned_tiles": int_array(text, "pinnedTileTypes", size, 0),
        "pinned_specials": int_array(text, "pinnedSpecialTypes", size, 0),
        "tubes": parse_entries(text, "tubes"),
        "safes": parse_entries(text, "safes"),
        "stacked": parse_entries(text, "stackedObstacles"),
    }


def luminance(color):
    r, g, b = color
    return (0.299 * r + 0.587 * g + 0.114 * b)


def draw_level(level, path):
    cell = 28
    pad = 14
    top = 26
    bottom = 18
    w = level["width"] * cell + pad * 2
    h = level["height"] * cell + pad * 2 + top + bottom
    c = Canvas(w, h, (248, 247, 243, 255))
    c.text(pad, 8, f"L{level['number']} M{level['moves']} {level['width']}X{level['height']}", (34, 34, 44, 255), 2)

    board_x = pad
    board_y = top
    c.rect(board_x - 2, board_y - 2, level["width"] * cell + 4, level["height"] * cell + 4, (54, 50, 58, 255))

    for y in range(level["height"]):
        for x in range(level["width"]):
            i = y * level["width"] + x
            px = board_x + x * cell
            py = board_y + y * cell
            if level["cells"][i] == 0:
                c.rect(px, py, cell - 1, cell - 1, (62, 62, 68, 255))
                continue

            c.rect(px, py, cell - 1, cell - 1, (230, 224, 212, 255))
            c.outline(px, py, cell, cell, (196, 190, 180, 255), 1)

            pinned = level["pinned_tiles"][i]
            if pinned > 0:
                tile_color = TILE_COLORS.get(pinned - 1, (190, 190, 198))
                c.rect(px + 5, py + 5, cell - 10, cell - 10, (*tile_color, 255))

            obs = level["obstacles"][i]
            if obs > 0:
                color = OBSTACLE_COLORS.get(obs, (136, 126, 150))
                c.rect(px + 2, py + 2, cell - 5, cell - 5, (*color, 255))
                text_color = (20, 20, 24, 255) if luminance(color) > 150 else (255, 255, 255, 255)
                c.centered_text(px, py, cell, cell, OBSTACLE_LABELS.get(obs, str(obs)), text_color, 2)
                if level["origins"][i] == i:
                    c.outline(px + 2, py + 2, cell - 5, cell - 5, (255, 255, 255, 255), 2)
                elif level["origins"][i] >= 0:
                    c.outline(px + 3, py + 3, cell - 7, cell - 7, (48, 48, 56, 255), 1)

            special = level["pinned_specials"][i]
            if special > 0:
                c.rect(px + cell - 11, py + 2, 9, 9, (255, 255, 255, 255))
                c.centered_text(px + cell - 12, py + 1, 11, 11, SPECIAL_LABELS.get(special, "S"), (28, 28, 34, 255), 1)

    for entry in level["stacked"]:
        idx = entry.get("originCellIndex", -1)
        obs = entry.get("obstacleId", 0)
        if 0 <= idx < level["width"] * level["height"]:
            x = idx % level["width"]; y = idx // level["width"]
            px = board_x + x * cell; py = board_y + y * cell
            c.rect(px + 4, py + 4, cell - 9, cell - 9, (255, 255, 255, 255))
            c.centered_text(px, py, cell, cell, OBSTACLE_LABELS.get(obs, str(obs)), (40, 40, 46, 255), 2)

    for entry in level["safes"]:
        idx = entry.get("originCellIndex", -1)
        sw = max(1, entry.get("width", 1)); sh = max(1, entry.get("height", 1))
        if 0 <= idx < level["width"] * level["height"]:
            x = idx % level["width"]; y = idx // level["width"]
            px = board_x + x * cell; py = board_y + y * cell
            c.outline(px + 1, py + 1, sw * cell - 3, sh * cell - 3, (28, 28, 36, 255), 3)
            c.centered_text(px, py, sw * cell, sh * cell, "SF", (28, 28, 36, 255), 2)

    for entry in level["tubes"]:
        idx = entry.get("originCellIndex", -1)
        direction = entry.get("direction", 0)
        length = max(2, entry.get("length", 2))
        dx, dy = {0: (0, -1), 1: (0, 1), 2: (-1, 0), 3: (1, 0)}.get(direction, (0, 1))
        for step in range(length):
            ti = idx + step * dx + step * dy * level["width"]
            if 0 <= ti < level["width"] * level["height"]:
                x = ti % level["width"]; y = ti // level["width"]
                px = board_x + x * cell; py = board_y + y * cell
                c.rect(px + 7, py + 7, cell - 14, cell - 14, (120, 196, 212, 255))
                c.outline(px + 7, py + 7, cell - 14, cell - 14, (34, 98, 116, 255), 1)

    c.save_png(path)
    return w, h


def cell_xml(ref, value, numeric=False):
    if numeric:
        return f'<c r="{ref}"><v>{value}</v></c>'
    return f'<c r="{ref}" t="inlineStr"><is><t>{html.escape(str(value))}</t></is></c>'


def col_name(idx):
    s = ""
    while idx:
        idx, rem = divmod(idx - 1, 26)
        s = chr(65 + rem) + s
    return s


def write_xlsx(levels):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    image_infos = []
    for level in levels:
        img_name = f"{level['id']}.png"
        img_path = IMG_DIR / img_name
        image_infos.append((img_name, *draw_level(level, img_path)))

    rows = []
    rows.append(
        '<row r="1" ht="24" customHeight="1">'
        + cell_xml("A1", "Level Number")
        + cell_xml("B1", "Level ID")
        + cell_xml("C1", "Preview")
        + "</row>"
    )
    for i, level in enumerate(levels, start=2):
        _, _, img_h = image_infos[i - 2]
        row_h = max(120, int(img_h * 0.75) + 8)
        rows.append(
            f'<row r="{i}" ht="{row_h}" customHeight="1">'
            + cell_xml(f"A{i}", level["number"], True)
            + cell_xml(f"B{i}", level["id"])
            + "</row>"
        )

    sheet = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
        'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
        '<sheetViews><sheetView workbookViewId="0"/></sheetViews>'
        '<sheetFormatPr defaultRowHeight="15"/>'
        '<cols><col min="1" max="1" width="14" customWidth="1"/>'
        '<col min="2" max="2" width="22" customWidth="1"/>'
        '<col min="3" max="3" width="48" customWidth="1"/></cols>'
        f'<sheetData>{"".join(rows)}</sheetData>'
        '<drawing r:id="rId1"/></worksheet>'
    )

    anchors = []
    rels = []
    for idx, (img_name, img_w, img_h) in enumerate(image_infos, start=1):
        row_zero = idx
        cx = img_w * 9525
        cy = img_h * 9525
        anchors.append(
            '<xdr:oneCellAnchor>'
            f'<xdr:from><xdr:col>2</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{row_zero}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>'
            f'<xdr:ext cx="{cx}" cy="{cy}"/>'
            '<xdr:pic>'
            f'<xdr:nvPicPr><xdr:cNvPr id="{idx}" name="Picture {idx}"/><xdr:cNvPicPr/></xdr:nvPicPr>'
            f'<xdr:blipFill><a:blip r:embed="rId{idx}"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill>'
            '<xdr:spPr><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></xdr:spPr>'
            '</xdr:pic><xdr:clientData/></xdr:oneCellAnchor>'
        )
        rels.append(
            f'<Relationship Id="rId{idx}" '
            'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" '
            f'Target="../media/{html.escape(img_name)}"/>'
        )

    drawing = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" '
        'xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" '
        'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
        + "".join(anchors)
        + '</xdr:wsDr>'
    )

    drawing_rels = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        + "".join(rels)
        + '</Relationships>'
    )

    with zipfile.ZipFile(XLSX_PATH, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("[Content_Types].xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
            '<Default Extension="xml" ContentType="application/xml"/>'
            '<Default Extension="png" ContentType="image/png"/>'
            '<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>'
            '<Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>'
            '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
            '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
            '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>'
            '<Override PartName="/xl/drawings/drawing1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/>'
            '</Types>')
        z.writestr("_rels/.rels",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>'
            '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>'
            '<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>'
            '</Relationships>')
        z.writestr("docProps/core.xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" '
            'xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>LevelP Previews</dc:title></cp:coreProperties>')
        z.writestr("docProps/app.xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties">'
            '<Application>tinyfixers export_level_previews.py</Application></Properties>')
        z.writestr("xl/workbook.xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
            'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
            '<sheets><sheet name="LevelP Previews" sheetId="1" r:id="rId1"/></sheets></workbook>')
        z.writestr("xl/_rels/workbook.xml.rels",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>'
            '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>'
            '</Relationships>')
        z.writestr("xl/styles.xml",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
            '<fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>'
            '<fills count="1"><fill><patternFill patternType="none"/></fill></fills>'
            '<borders count="1"><border/></borders>'
            '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
            '<cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>'
            '</styleSheet>')
        z.writestr("xl/worksheets/sheet1.xml", sheet)
        z.writestr("xl/worksheets/_rels/sheet1.xml.rels",
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing1.xml"/>'
            '</Relationships>')
        z.writestr("xl/drawings/drawing1.xml", drawing)
        z.writestr("xl/drawings/_rels/drawing1.xml.rels", drawing_rels)
        for img_name, _, _ in image_infos:
            z.write(IMG_DIR / img_name, f"xl/media/{img_name}")


def main():
    IMG_DIR.mkdir(parents=True, exist_ok=True)
    levels = [load_level(path) for path in sorted(LEVEL_DIR.glob("LevelP_*.asset"))]
    levels.sort(key=lambda l: (l["number"], l["id"]))
    write_xlsx(levels)
    print(f"Wrote {len(levels)} levels")
    print(XLSX_PATH)
    print(IMG_DIR)


if __name__ == "__main__":
    main()
