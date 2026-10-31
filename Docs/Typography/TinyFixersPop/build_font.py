#!/usr/bin/env python3
"""Build the original TinyFixers Pop outline font (v0.5).

Requires fontTools, skia-pathops and Pillow. No source font is read by this script:
all outlines below are drawn in this file. The TTF and individual SVGs are outputs.

v0.2 "chunky" pass (see STYLE below): the hand-drawn v0.1 skeletons are post-processed
into a heavier, bouncier cartoon display face — taller lowercase, double-storey a,
per-letter fixed bounce (tilt + baseline hop), rounded dilation (fatter strokes,
smaller counters, soft corners), tighter spacing, and every glyph merged into clean,
non-overlapping clockwise contours so TMP SDF outline/underlay/softness/glow render
without seams.
"""
from __future__ import annotations

import json
import math
from pathlib import Path
import unicodedata
import zlib

import pathops
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.recordingPen import RecordingPen

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = ROOT / "Assets/_Project/Fonts/TinyFixersPop"
FAMILY = "TinyFixers Pop"
VERSION = "0.5"
GLYPHS: dict[str, dict] = {}

# v0.2 chunky style. Units: 1000 UPM.
# v0.3 (measured against Grobold): letters stay UPRIGHT on a flat baseline — the per-letter
# tilt/hop of v0.2 read as crooked. Weight comes from size in the em, fat stems and tight set.
STYLE = {
    "x_height_scale": 1.0,    # v0.1 ratio x/cap = 0.756 already matches Grobold (0.74)
    "scale": 1.12,            # cap 700 → ~818 with dilation (Grobold 822): bigger in the em
    "condense": 0.92,         # horizontal: Grobold sets ~10% narrower than our skeletons
    "dilate": 34,             # stem/cap ≈ 0.37 like Grobold (v0.2: 26 → 0.33)
    "round": 8,               # closing radius (small: dilate+round must stay < half the e-eye or it fills)
    "tighten": 30,            # tighter set; round letters nearly touch like Grobold
    "bounce_deg": 0.0,        # no tilt
    "bounce_dy": 0,           # no baseline hop
    "digit_bounce_deg": 0.0,
    "digit_bounce_dy": 0,
}
LOWER = 'abcdefghijklmnopqrstuvwxyzı'
# Multi-bar symbols: full dilation closes their inner gaps (₺ bars, € bars, # grid) → lighter pass.
DETAILED = {ch: (10, 6) for ch in '₺€$#%@&*'}


def poly(points, radius=12, hole=False):
    """Rounded polygon. Outer contours clockwise; counters anticlockwise."""
    points = [tuple(p) for p in points]
    area = sum(a[0] * b[1] - b[0] * a[1]
               for a, b in zip(points, points[1:] + points[:1]))
    if (area > 0) != hole:
        points.reverse()
    entries, exits = [], []
    for i, b in enumerate(points):
        a, c = points[i - 1], points[(i + 1) % len(points)]
        ab, bc = math.dist(a, b), math.dist(b, c)
        r = min(radius, ab * .24, bc * .24)
        entries.append((b[0] + (a[0] - b[0]) * r / ab,
                        b[1] + (a[1] - b[1]) * r / ab))
        exits.append((b[0] + (c[0] - b[0]) * r / bc,
                      b[1] + (c[1] - b[1]) * r / bc))
    commands = [["M", *entries[0]]]
    for i, corner in enumerate(points):
        if i:
            commands.append(["L", *entries[i]])
        commands.append(["Q", *corner, *exits[i]])
    commands.append(["Z"])
    return commands


def rect(x0, y0, x1, y1, radius=14, hole=False):
    return poly([(x0, y0), (x0, y1), (x1, y1), (x1, y0)], radius, hole)


def bowl(x0, y0, x1, y1, hole=False):
    """Soft rectangular oval; direct curves avoid bevels on large counters."""
    w, h = x1 - x0, y1 - y0
    if hole:
        return path(('M', x0+.46*w, y0),
                    ('Q', x1, y0, x1, y0+.43*h),
                    ('L', x1, y0+.56*h),
                    ('Q', x1, y1, x0+.52*w, y1),
                    ('Q', x0, y1, x0, y0+.56*h),
                    ('L', x0, y0+.43*h),
                    ('Q', x0, y0, x0+.46*w, y0), ('Z',))
    return path(('M', x0+.46*w, y0),
                ('Q', x0, y0, x0, y0+.41*h),
                ('L', x0, y0+.58*h),
                ('Q', x0, y1, x0+.51*w, y1),
                ('Q', x1, y1, x1, y0+.58*h),
                ('L', x1, y0+.41*h),
                ('Q', x1, y0, x0+.46*w, y0), ('Z',))


def path(*commands):
    return [list(c) for c in commands]


def transformed(contour, dx=0, dy=0, sx=1, sy=1, shear=0, rotate=0, cx=0, cy=0):
    rad = math.radians(rotate)
    co, si = math.cos(rad), math.sin(rad)
    result = []
    for command in contour:
        out = [command[0]]
        for i in range(1, len(command), 2):
            x, y = command[i] * sx, command[i+1] * sy
            x += shear * y
            x, y = x-cx, y-cy
            out.extend([round(cx + x*co - y*si + dx, 2),
                        round(cy + x*si + y*co + dy, 2)])
        result.append(out)
    return result


def glyph(char, width, *contours, lean=0):
    GLYPHS[char] = {"advance": width,
                    "contours": [transformed(c, shear=lean) for c in contours]}


def copy(char, source, width=None, **transform):
    original = GLYPHS[source]
    GLYPHS[char] = {"advance": width or original["advance"],
                    "contours": [transformed(c, **transform) for c in original["contours"]]}


def draw_alphabet():
    # Capitals: 700-unit cap height, 155–180-unit stems; blunted terminals.
    glyph('A', 650,
          poly([(22, 0), (203, 706), (420, 700), (630, 0), (446, -3),
                (409, 147), (226, 147), (189, 0)], 17),
          poly([(263, 301), (366, 301), (311, 520)], 9, True))
    glyph('B', 635,
          path(('M', 53, 0), ('L', 48, 700), ('L', 335, 706),
               ('Q', 570, 706, 580, 535), ('Q', 587, 431, 500, 373),
               ('Q', 605, 323, 605, 207), ('Q', 605, -5, 352, -5),
               ('L', 53, 0), ('Z',)),
          bowl(224, 429, 405, 552, True), bowl(226, 156, 421, 299, True))
    glyph('C', 620,
          path(('M', 577, 604), ('L', 485, 474), ('Q', 424, 551, 333, 551),
               ('Q', 204, 551, 205, 349), ('Q', 201, 150, 333, 147),
               ('Q', 427, 143, 494, 227), ('L', 591, 105),
               ('Q', 492, -13, 324, -13), ('Q', 33, -13, 32, 345),
               ('Q', 29, 712, 327, 712), ('Q', 486, 715, 577, 604), ('Z',)))
    glyph('D', 665,
          path(('M', 53, 0), ('L', 47, 700), ('L', 328, 705),
               ('Q', 635, 705, 632, 352), ('Q', 632, -5, 328, -5),
               ('L', 53, 0), ('Z',)), bowl(224, 155, 454, 548, True))
    glyph('E', 565, poly([(48, 0), (44, 700), (522, 700), (520, 545),
                           (219, 545), (219, 424), (483, 424), (483, 278),
                           (219, 278), (219, 157), (532, 157), (532, 0)], 13))
    glyph('F', 550, poly([(48, 0), (44, 700), (520, 700), (520, 543),
                           (218, 543), (218, 401), (474, 401), (474, 245),
                           (218, 245), (218, 0)], 13))
    glyph('G', 675,
          path(('M', 602, 604), ('L', 497, 482), ('Q', 437, 550, 337, 551),
               ('Q', 205, 552, 205, 348), ('Q', 205, 147, 346, 147),
               ('Q', 416, 145, 468, 182), ('L', 468, 285), ('L', 337, 285),
               ('L', 337, 427), ('L', 630, 427), ('L', 630, 91),
               ('Q', 511, -14, 337, -14), ('Q', 29, -14, 29, 347),
               ('Q', 27, 714, 337, 714), ('Q', 500, 714, 602, 604), ('Z',)))
    glyph('H', 650, poly([(51, 0), (49, 701), (221, 701), (221, 438),
                           (431, 438), (431, 701), (600, 701), (600, 0),
                           (431, 0), (431, 282), (221, 282), (221, 0)], 15))
    glyph('I', 320, poly([(72, 0), (69, 700), (249, 700), (249, 0)], 19))
    glyph('J', 505,
          path(('M', 451, 700), ('L', 451, 203), ('Q', 451, -11, 227, -11),
               ('Q', 81, -11, 15, 69), ('L', 104, 196), ('Q', 144, 145, 207, 145),
               ('Q', 279, 145, 279, 226), ('L', 279, 700), ('Z',)))
    glyph('K', 650, poly([(50, 0), (47, 700), (219, 700), (219, 442),
                           (429, 700), (633, 700), (367, 376), (648, 0),
                           (435, 0), (257, 250), (219, 203), (219, 0)], 12))
    glyph('L', 540, poly([(49, 0), (45, 700), (222, 700), (222, 163),
                           (505, 163), (505, 0)], 15))
    glyph('M', 800, poly([(50, 0), (44, 700), (246, 700), (401, 339),
                           (555, 700), (755, 700), (755, 0), (586, 0),
                           (586, 399), (463, 102), (336, 102), (215, 397),
                           (215, 0)], 15))
    glyph('N', 670, poly([(49, 0), (44, 700), (227, 700), (455, 286),
                           (455, 700), (623, 700), (623, 0), (445, 0),
                           (217, 414), (217, 0)], 13))
    glyph('O', 680, bowl(32, -13, 647, 713), bowl(211, 147, 468, 552, True), lean=.012)
    glyph('P', 610,
          path(('M', 50, 0), ('L', 47, 700), ('L', 339, 705),
               ('Q', 580, 705, 581, 464), ('Q', 580, 225, 338, 225),
               ('L', 221, 225), ('L', 221, 0), ('Z',)), bowl(221, 374, 410, 552, True))
    copy('Q', 'O', width=700)
    GLYPHS['Q']['contours'].append(poly([(360, 205), (480, 276), (675, -87), (526, -132)], 17))
    glyph('R', 650,
          path(('M', 51, 0), ('L', 48, 700), ('L', 336, 705),
               ('Q', 581, 705, 582, 477), ('Q', 584, 328, 465, 273),
               ('L', 634, 0), ('L', 434, 0), ('L', 300, 238), ('L', 222, 238),
               ('L', 222, 0), ('Z',)), bowl(222, 390, 409, 552, True))
    glyph('S', 600,
          path(('M', 548, 621), ('L', 456, 487), ('Q', 378, 557, 283, 557),
               ('Q', 204, 557, 203, 505), ('Q', 201, 461, 311, 430),
               ('Q', 574, 356, 572, 195), ('Q', 570, -14, 298, -14),
               ('Q', 128, -14, 22, 87), ('L', 117, 226),
               ('Q', 205, 146, 308, 146), ('Q', 393, 146, 394, 197),
               ('Q', 397, 243, 277, 280), ('Q', 31, 354, 33, 517),
               ('Q', 36, 714, 290, 714), ('Q', 446, 714, 548, 621), ('Z',)))
    glyph('T', 610, poly([(21, 542), (21, 700), (588, 700), (588, 542),
                           (392, 542), (392, 0), (217, 0), (217, 542)], 14))
    glyph('U', 665,
          path(('M', 46, 700), ('L', 221, 700), ('L', 221, 247),
               ('Q', 221, 149, 334, 149), ('Q', 446, 149, 446, 247),
               ('L', 446, 700), ('L', 619, 700), ('L', 619, 235),
               ('Q', 619, -14, 333, -14), ('Q', 46, -14, 46, 235), ('Z',)))
    glyph('V', 655, poly([(11, 700), (200, 700), (329, 196), (455, 700),
                           (642, 700), (439, 0), (216, 0)], 18))
    glyph('W', 920, poly([(12, 700), (192, 700), (279, 233), (373, 700),
                           (550, 700), (644, 233), (730, 700), (907, 700),
                           (755, 0), (546, 0), (460, 382), (374, 0), (166, 0)], 16))
    glyph('X', 645, poly([(20, 700), (221, 700), (326, 487), (435, 700),
                           (630, 700), (428, 358), (641, 0), (438, 0),
                           (323, 228), (209, 0), (8, 0), (219, 357)], 14))
    glyph('Y', 645, poly([(10, 700), (212, 700), (325, 439), (440, 700),
                           (633, 700), (410, 267), (410, 0), (234, 0),
                           (234, 267)], 17))
    glyph('Z', 595, poly([(39, 700), (553, 700), (553, 554), (246, 155),
                           (560, 155), (560, 0), (28, 0), (28, 144),
                           (337, 545), (39, 545)], 12))

    # Lowercase is separately drawn, with a tall x-height and a single-storey a/g.
    glyph('a', 565,
          path(('M', 517, 0), ('L', 360, 0), ('L', 351, 45),
               ('Q', 292, -12, 203, -12), ('Q', 25, -12, 27, 256),
               ('Q', 28, 541, 239, 541), ('Q', 324, 541, 364, 493),
               ('L', 371, 529), ('L', 517, 529), ('Z',)), bowl(193, 132, 354, 394, True))
    glyph('b', 570,
          path(('M', 48, 0), ('L', 43, 734), ('L', 211, 734), ('L', 211, 491),
               ('Q', 260, 541, 346, 541), ('Q', 541, 541, 540, 263),
               ('Q', 541, -12, 335, -12), ('Q', 260, -12, 204, 40),
               ('L', 197, 0), ('Z',)), bowl(212, 130, 372, 395, True))
    glyph('c', 515,
          path(('M', 483, 457), ('L', 390, 342), ('Q', 348, 395, 280, 395),
               ('Q', 195, 395, 194, 264), ('Q', 193, 133, 283, 133),
               ('Q', 343, 132, 395, 190), ('L', 489, 74),
               ('Q', 409, -13, 278, -13), ('Q', 26, -13, 26, 263),
               ('Q', 26, 542, 278, 542), ('Q', 409, 542, 483, 457), ('Z',)))
    copy('d', 'b', width=570, sx=-1, dx=570)
    glyph('e', 550,
          path(('M', 523, 214), ('L', 192, 214), ('Q', 203, 119, 301, 119),
               ('Q', 376, 118, 434, 168), ('L', 510, 61),
               ('Q', 422, -14, 286, -14), ('Q', 25, -14, 25, 263),
               ('Q', 25, 542, 284, 542), ('Q', 530, 542, 525, 287), ('Z',)),
          path(('M', 193, 322), ('L', 365, 322), ('Q', 360, 410, 283, 410),
               ('Q', 205, 410, 193, 322), ('Z',)))
    glyph('f', 395,
          path(('M', 89, 0), ('L', 89, 389), ('L', 22, 389), ('L', 22, 529),
               ('L', 89, 529), ('L', 89, 564), ('Q', 89, 747, 289, 747),
               ('Q', 348, 747, 388, 722), ('L', 355, 587),
               ('Q', 330, 604, 299, 604), ('Q', 253, 605, 253, 560),
               ('L', 253, 529), ('L', 368, 529), ('L', 368, 389),
               ('L', 253, 389), ('L', 253, 0), ('Z',)))
    glyph('g', 572,
          path(('M', 524, 529), ('L', 524, -7), ('Q', 524, -203, 269, -203),
               ('Q', 128, -203, 47, -135), ('L', 112, -15),
               ('Q', 175, -65, 265, -65), ('Q', 359, -65, 359, 17),
               ('L', 359, 46), ('Q', 309, -7, 221, -7),
               ('Q', 27, -7, 27, 259), ('Q', 27, 542, 237, 542),
               ('Q', 322, 542, 369, 490), ('L', 376, 529), ('Z',)),
          bowl(193, 135, 359, 395, True))
    glyph('h', 574,
          path(('M', 48, 0), ('L', 44, 734), ('L', 211, 734), ('L', 211, 486),
               ('Q', 260, 541, 351, 541), ('Q', 526, 541, 526, 341),
               ('L', 526, 0), ('L', 359, 0), ('L', 359, 315),
               ('Q', 359, 393, 292, 393), ('Q', 212, 393, 212, 302),
               ('L', 212, 0), ('Z',)))
    glyph('ı', 285, poly([(57, 0), (52, 529), (223, 529), (223, 0)], 16))
    copy('i', 'ı')
    GLYPHS['i']['contours'].append(bowl(52, 603, 225, 754))
    glyph('j', 293,
          path(('M', 66, 529), ('L', 235, 529), ('L', 235, -3),
               ('Q', 235, -203, 49, -203), ('Q', 0, -203, -28, -186),
               ('L', -5, -47), ('Q', 18, -57, 33, -57),
               ('Q', 68, -57, 68, 0), ('Z',)), bowl(64, 603, 237, 754))
    glyph('k', 560, poly([(48, 0), (43, 734), (211, 734), (211, 327),
                           (356, 529), (552, 529), (350, 283), (559, 0),
                           (363, 0), (244, 178), (211, 139), (211, 0)], 13))
    glyph('l', 285, poly([(58, 0), (52, 734), (223, 734), (223, 0)], 17))
    glyph('m', 830,
          path(('M', 46, 0), ('L', 42, 529), ('L', 200, 529), ('L', 209, 486),
               ('Q', 260, 542, 340, 542), ('Q', 435, 542, 478, 477),
               ('Q', 542, 542, 630, 542), ('Q', 784, 542, 784, 345),
               ('L', 784, 0), ('L', 621, 0), ('L', 621, 315),
               ('Q', 621, 394, 564, 394), ('Q', 499, 394, 499, 312),
               ('L', 499, 0), ('L', 335, 0), ('L', 335, 315),
               ('Q', 335, 394, 278, 394), ('Q', 212, 394, 212, 312),
               ('L', 212, 0), ('Z',)))
    glyph('n', 574,
          path(('M', 48, 0), ('L', 43, 529), ('L', 201, 529), ('L', 210, 483),
               ('Q', 263, 542, 351, 542), ('Q', 526, 542, 526, 341),
               ('L', 526, 0), ('L', 359, 0), ('L', 359, 315),
               ('Q', 359, 394, 292, 394), ('Q', 212, 394, 212, 302),
               ('L', 212, 0), ('Z',)))
    glyph('o', 568, bowl(27, -13, 540, 542), bowl(194, 132, 373, 396, True))
    copy('p', 'b', dy=-200)
    # p's ascender becomes an x-height stem; its bowl must stay at baseline.
    GLYPHS['p'] = {"advance": 570, "contours": [
        path(('M', 48, -193), ('L', 43, 529), ('L', 201, 529), ('L', 210, 489),
             ('Q', 260, 542, 346, 542), ('Q', 541, 542, 540, 263),
             ('Q', 541, -12, 335, -12), ('Q', 260, -12, 212, 38),
             ('L', 212, -193), ('Z',)), bowl(212, 130, 372, 395, True)]}
    copy('q', 'p', width=570, sx=-1, dx=570)
    glyph('r', 421,
          path(('M', 48, 0), ('L', 43, 529), ('L', 201, 529), ('L', 210, 464),
               ('Q', 260, 539, 348, 539), ('Q', 380, 539, 403, 529),
               ('L', 383, 370), ('Q', 361, 382, 324, 382),
               ('Q', 212, 382, 212, 258), ('L', 212, 0), ('Z',)))
    glyph('s', 490,
          path(('M', 451, 471), ('L', 376, 359), ('Q', 311, 410, 240, 410),
               ('Q', 188, 410, 186, 379), ('Q', 185, 353, 269, 331),
               ('Q', 464, 280, 462, 148), ('Q', 459, -13, 244, -13),
               ('Q', 105, -13, 20, 62), ('L', 95, 178),
               ('Q', 165, 118, 245, 118), ('Q', 302, 118, 303, 151),
               ('Q', 304, 181, 218, 205), ('Q', 29, 256, 31, 388),
               ('Q', 34, 542, 242, 542), ('Q', 367, 542, 451, 471), ('Z',)))
    glyph('t', 412,
          path(('M', 90, 672), ('L', 255, 693), ('L', 255, 529), ('L', 383, 529),
               ('L', 383, 389), ('L', 255, 389), ('L', 255, 190),
               ('Q', 255, 132, 308, 132), ('Q', 345, 132, 375, 151),
               ('L', 403, 21), ('Q', 344, -12, 264, -12),
               ('Q', 90, -12, 90, 174), ('L', 90, 389), ('L', 20, 389),
               ('L', 20, 529), ('L', 90, 529), ('Z',)))
    glyph('u', 574,
          path(('M', 46, 529), ('L', 213, 529), ('L', 213, 215),
               ('Q', 213, 136, 280, 136), ('Q', 359, 136, 359, 227),
               ('L', 359, 529), ('L', 526, 529), ('L', 526, 0),
               ('L', 369, 0), ('L', 360, 46), ('Q', 308, -13, 221, -13),
               ('Q', 46, -13, 46, 188), ('Z',)))
    glyph('v', 550, poly([(13, 529), (190, 529), (278, 173), (367, 529),
                           (540, 529), (375, 0), (178, 0)], 16))
    glyph('w', 785, poly([(13, 529), (181, 529), (244, 182), (320, 529),
                           (468, 529), (543, 182), (608, 529), (772, 529),
                           (650, 0), (467, 0), (394, 291), (321, 0), (138, 0)], 15))
    glyph('x', 552, poly([(16, 529), (203, 529), (278, 371), (355, 529),
                           (537, 529), (377, 269), (545, 0), (354, 0),
                           (276, 166), (196, 0), (9, 0), (176, 269)], 12))
    glyph('y', 552,
          path(('M', 12, 529), ('L', 194, 529), ('L', 288, 210),
               ('L', 374, 529), ('L', 548, 529), ('L', 360, -24),
               ('Q', 300, -203, 112, -203), ('Q', 46, -203, 10, -183),
               ('L', 48, -46), ('Q', 73, -57, 108, -57),
               ('Q', 178, -57, 197, 0), ('Z',)))
    glyph('z', 493, poly([(35, 529), (461, 529), (461, 403), (232, 136),
                           (465, 136), (465, 0), (27, 0), (27, 125),
                           (256, 393), (35, 393)], 11))


def redesign_v02():
    """Character redesigns for the chunky style. Shapes may overlap freely; they are merged later."""
    # Double-storey a: top hook + right stem + lower bowl (hole cut out explicitly).
    GLYPHS['a'] = {"advance": 560, "contours": boolean(
        add=[path(('M', 517, 330), ('Q', 517, 541, 283, 541), ('Q', 140, 541, 52, 462),
                  ('L', 128, 352), ('Q', 190, 400, 268, 400), ('Q', 352, 400, 352, 330), ('Z',)),
             rect(352, 0, 517, 360, 18),
             bowl(25, -12, 405, 305)],
        sub=[bowl(170, 92, 318, 212)])}


def scale_lowercase():
    k = STYLE["x_height_scale"]
    for char in LOWER:
        GLYPHS[char]["contours"] = [transformed(c, sy=k) for c in GLYPHS[char]["contours"]]


# ── pathops bridge ───────────────────────────────────────────────────────────

def to_pathops(contours):
    p = pathops.Path()
    pen = p.getPen()
    for contour in contours:
        for cmd in contour:
            kind = cmd[0]
            if kind == 'M': pen.moveTo(tuple(cmd[1:3]))
            elif kind == 'L': pen.lineTo(tuple(cmd[1:3]))
            elif kind == 'Q': pen.qCurveTo(tuple(cmd[1:3]), tuple(cmd[3:5]))
            elif kind == 'Z': pen.closePath()
    return p


def from_pathops(p):
    """pathops Path → our M/L/Q/Z contours (cubics/conics converted to quadratics)."""
    p.convertConicsToQuads()
    rec = RecordingPen()
    p.draw(Cu2QuPen(rec, max_err=0.8))
    contours, current = [], None
    for op, args in rec.value:
        if op == 'moveTo':
            current = [['M', *args[0]]]
        elif op == 'lineTo':
            current.append(['L', *args[0]])
        elif op == 'qCurveTo':
            pts = list(args)
            if pts[-1] is None:          # all-off-curve closed contour: not produced by Skia
                raise ValueError('unexpected implied-start quadratic contour')
            offs, end = pts[:-1], pts[-1]
            for i, off in enumerate(offs):
                on = end if i == len(offs) - 1 else ((off[0] + offs[i+1][0]) / 2, (off[1] + offs[i+1][1]) / 2)
                current.append(['Q', *off, *on])
        elif op in ('closePath', 'endPath'):
            current.append(['Z'])
            contours.append([[c[0], *[round(v, 2) for v in c[1:]]] for c in current])
            current = None
    return contours


def copy_path(p):
    q = pathops.Path()
    p.draw(q.getPen())
    return q


def boolean(add, sub=()):
    """Union of `add` shapes minus `sub` shapes; contour directions don't matter."""
    result = None
    for c in add:
        piece = to_pathops([c])
        result = piece if result is None else pathops.op(result, piece, pathops.PathOp.UNION)
    for c in sub:
        result = pathops.op(result, to_pathops([c]), pathops.PathOp.DIFFERENCE)
    return from_pathops(pathops.simplify(result, clockwise=True))


def stroked(p, width):
    s = copy_path(p)
    s.stroke(width, pathops.LineCap.ROUND_CAP, pathops.LineJoin.ROUND_JOIN, 4)
    s.convertConicsToQuads()   # round joins come out as conics; boolean ops need quads
    return s


def chunk(contours, d=None, r=None):
    """Rounded dilation: merge overlaps, grow by d (STYLE.dilate), round inner joins (closing).
    Output: clean, non-overlapping, clockwise TrueType contours."""
    d = STYLE["dilate"] if d is None else d
    r = STYLE["round"] if r is None else r
    p = pathops.simplify(to_pathops(contours), clockwise=True)
    grown = pathops.op(p, stroked(p, 2 * (d + r)), pathops.PathOp.UNION) if d + r > 0 else p
    # r == 0: no erosion — a zero-width stroke is treated as the whole shape and would erase the glyph.
    shaped = pathops.op(grown, stroked(grown, 2 * r), pathops.PathOp.DIFFERENCE) if r > 0 else grown
    return from_pathops(pathops.simplify(shaped, clockwise=True))


def bounce_of(char):
    """Fixed per-character tilt/hop (authored by a stable hash, identical on every build)."""
    base = unicodedata.normalize('NFD', char)[0]
    if base.isalpha():
        deg, dy = STYLE["bounce_deg"], STYLE["bounce_dy"]
    elif base.isdigit():
        deg, dy = STYLE["digit_bounce_deg"], STYLE["digit_bounce_dy"]
    else:
        return 0.0, 0.0
    h = zlib.crc32(base.encode('utf-8'))
    unit = lambda bits: ((h >> bits) & 0xFFF) / 0xFFF * 2 - 1   # -1..1
    return deg * unit(0), dy * unit(12)


def draw_numbers_and_symbols():
    glyph('0', 620, bowl(32, -13, 587, 713), bowl(207, 145, 412, 555, True))
    glyph('1', 445, poly([(44, 548), (182, 700), (350, 700), (350, 0),
                           (178, 0), (178, 484), (140, 445)], 13))
    glyph('2', 575,
          path(('M', 29, 587), ('Q', 128, 714, 292, 714),
               ('Q', 548, 714, 548, 514), ('Q', 548, 400, 414, 292),
               ('L', 239, 157), ('L', 552, 157), ('L', 552, 0),
               ('L', 28, 0), ('L', 28, 147), ('L', 293, 365),
               ('Q', 371, 430, 371, 489), ('Q', 371, 555, 290, 555),
               ('Q', 207, 555, 136, 475), ('Z',)))
    glyph('3', 575,
          path(('M', 42, 629), ('Q', 139, 714, 290, 714),
               ('Q', 537, 714, 537, 522), ('Q', 537, 422, 438, 373),
               ('Q', 553, 334, 553, 211), ('Q', 553, -14, 285, -14),
               ('Q', 118, -14, 22, 81), ('L', 120, 209),
               ('Q', 192, 142, 279, 142), ('Q', 376, 142, 376, 214),
               ('Q', 376, 285, 271, 285), ('L', 190, 285), ('L', 190, 434),
               ('L', 268, 434), ('Q', 364, 434, 364, 496),
               ('Q', 364, 559, 284, 559), ('Q', 209, 559, 139, 504), ('Z',)))
    glyph('4', 610, poly([(19, 275), (289, 700), (477, 700), (477, 279),
                           (584, 279), (584, 129), (477, 129), (477, 0),
                           (310, 0), (310, 129), (19, 129)], 10),
          poly([(177, 279), (310, 279), (310, 491)], 6, True))
    glyph('5', 575,
          path(('M', 81, 700), ('L', 533, 700), ('L', 533, 544),
               ('L', 228, 544), ('L', 219, 437), ('Q', 266, 457, 322, 457),
               ('Q', 551, 457, 551, 224), ('Q', 551, -14, 283, -14),
               ('Q', 121, -14, 20, 81), ('L', 117, 211),
               ('Q', 187, 144, 277, 144), ('Q', 376, 144, 376, 224),
               ('Q', 376, 307, 278, 307), ('Q', 211, 307, 158, 274),
               ('L', 48, 332), ('Z',)))
    glyph('6', 605,
          path(('M', 543, 620), ('L', 450, 498), ('Q', 394, 555, 318, 555),
               ('Q', 209, 555, 202, 413), ('Q', 261, 459, 345, 459),
               ('Q', 575, 459, 575, 225), ('Q', 575, -14, 308, -14),
               ('Q', 28, -14, 28, 325), ('Q', 28, 714, 319, 714),
               ('Q', 461, 714, 543, 620), ('Z',)), bowl(204, 137, 405, 314, True))
    glyph('7', 560, poly([(26, 700), (536, 700), (536, 559), (293, 0),
                           (99, 0), (343, 543), (26, 543)], 13))
    glyph('8', 610,
          path(('M', 164, 371), ('Q', 39, 414, 41, 526),
               ('Q', 44, 714, 307, 714), ('Q', 569, 714, 569, 525),
               ('Q', 569, 414, 450, 371), ('Q', 584, 324, 584, 200),
               ('Q', 584, -14, 307, -14), ('Q', 28, -14, 28, 200),
               ('Q', 28, 324, 164, 371), ('Z',)),
          bowl(216, 430, 397, 562, True), bowl(205, 135, 408, 295, True))
    glyph('9', 605,
          path(('M', 62, 80), ('L', 156, 203), ('Q', 211, 146, 287, 146),
               ('Q', 396, 146, 403, 288), ('Q', 345, 242, 260, 242),
               ('Q', 30, 242, 30, 476), ('Q', 30, 714, 298, 714),
               ('Q', 577, 714, 577, 376), ('Q', 577, -14, 286, -14),
               ('Q', 144, -14, 62, 80), ('Z',)), bowl(200, 386, 401, 563, True))
    glyph(' ', 235)
    copy('\u00a0', ' ')
    glyph('.', 285, bowl(57, -5, 229, 156))
    glyph('·', 285, bowl(66, 231, 219, 384))
    glyph(',', 285, poly([(63, -123), (77, 44), (61, 154), (231, 154),
                           (219, 12), (149, -135)], 13))
    glyph(':', 285, bowl(57, -5, 229, 156), bowl(57, 338, 229, 499))
    glyph(';', 285, GLYPHS[',']['contours'][0], bowl(57, 338, 229, 499))
    glyph('!', 300, poly([(56, 700), (247, 700), (226, 228), (78, 228)], 15),
          bowl(63, -5, 236, 156))
    glyph('?', 550,
          path(('M', 34, 598), ('Q', 117, 714, 282, 714),
               ('Q', 516, 714, 516, 521), ('Q', 516, 421, 419, 352),
               ('Q', 345, 301, 345, 237), ('L', 345, 216),
               ('L', 185, 216), ('L', 185, 252), ('Q', 185, 380, 278, 446),
               ('Q', 345, 491, 345, 523), ('Q', 345, 564, 281, 564),
               ('Q', 205, 564, 139, 486), ('Z',)), bowl(177, -5, 351, 156))
    glyph('-', 410, rect(40, 229, 371, 376, 14))
    glyph('_', 570, rect(17, -139, 553, -12, 10))
    glyph('+', 575, poly([(38, 260), (207, 260), (207, 440), (367, 440),
                           (367, 260), (537, 260), (537, 108), (367, 108),
                           (367, -72), (207, -72), (207, 108), (38, 108)], 12))
    glyph('=', 575, rect(38, 297, 537, 430, 12), rect(38, 71, 537, 204, 12))
    glyph('/', 440, poly([(19, -104), (266, 758), (426, 758), (179, -104)], 12))
    copy('\\', '/', sx=-1, dx=440)
    glyph('(', 365,
          path(('M', 344, 731), ('Q', 187, 558, 187, 286),
               ('Q', 187, 17, 344, -157), ('L', 194, -229),
               ('Q', 20, -17, 20, 286), ('Q', 20, 591, 194, 803), ('Z',)))
    copy(')', '(', sx=-1, dx=365)
    glyph('[', 335, poly([(47, -193), (47, 767), (302, 767), (302, 629),
                           (198, 629), (198, -55), (302, -55), (302, -193)], 10))
    copy(']', '[', sx=-1, dx=335)
    glyph('{', 410,
          path(('M', 367, 783), ('L', 367, 642), ('Q', 245, 647, 245, 549),
               ('L', 245, 431), ('Q', 245, 333, 174, 290),
               ('Q', 245, 248, 245, 149), ('L', 245, 28),
               ('Q', 245, -70, 367, -65), ('L', 367, -207),
               ('Q', 86, -221, 86, 12), ('L', 86, 144),
               ('Q', 86, 223, 24, 223), ('L', 24, 358),
               ('Q', 86, 358, 86, 436), ('L', 86, 570),
               ('Q', 86, 799, 367, 783), ('Z',)))
    copy('}', '{', sx=-1, dx=410)
    glyph('^', 510, poly([(28, 430), (176, 700), (334, 700), (482, 430),
                          (333, 430), (255, 583), (177, 430)], 12))
    glyph('`', 300, poly([(40, 770), (190, 770), (266, 591), (148, 591)], 12))
    glyph('~', 600,
          path(('M', 31, 243), ('Q', 71, 440, 211, 440),
               ('Q', 277, 440, 346, 385), ('Q', 425, 322, 455, 425),
               ('L', 570, 390), ('Q', 529, 196, 388, 196),
               ('Q', 320, 196, 251, 250), ('Q', 173, 313, 144, 208), ('Z',)))
    glyph("'", 250, poly([(55, 701), (226, 701), (182, 470), (67, 470)], 13))
    glyph('"', 435, GLYPHS["'"]['contours'][0],
          transformed(GLYPHS["'"]['contours'][0], dx=185))
    glyph('%', 800, bowl(22, 390, 330, 711), bowl(132, 491, 221, 608, True),
          bowl(470, -10, 778, 311), bowl(579, 91, 669, 208, True),
          poly([(167, -18), (525, 714), (651, 714), (293, -18)], 9))
    glyph('*', 450, poly([(185, 700), (265, 700), (267, 601), (355, 646),
                           (399, 577), (309, 528), (396, 478), (354, 410),
                           (266, 459), (265, 359), (185, 359), (184, 459),
                           (96, 410), (52, 478), (141, 528), (52, 577),
                           (94, 646), (184, 601)], 5))
    glyph('<', 500, poly([(440, 497), (440, 327), (223, 219), (440, 110),
                           (440, -60), (44, 145), (44, 291)], 9))
    copy('>', '<', sx=-1, dx=500)
    glyph('|', 280, rect(68, -200, 213, 750, 10))
    # A deliberately simple original hash, ampersand and at sign for UI labels.
    glyph('#', 660, rect(39, 395, 622, 531, 6), rect(20, 169, 604, 305, 6),
          poly([(109, 0), (213, 700), (352, 700), (248, 0)], 6),
          poly([(374, 0), (478, 700), (617, 700), (513, 0)], 6))
    glyph('&', 690,
          path(('M', 620, 0), ('L', 444, 0), ('L', 401, 42),
               ('Q', 330, -13, 219, -13), ('Q', 30, -13, 30, 181),
               ('Q', 30, 301, 156, 369), ('Q', 86, 455, 86, 529),
               ('Q', 86, 714, 300, 714), ('Q', 501, 714, 501, 543),
               ('Q', 501, 431, 365, 365), ('L', 434, 292),
               ('Q', 469, 352, 481, 419), ('L', 632, 390),
               ('Q', 602, 259, 539, 174), ('Z',)),
          bowl(238, 466, 350, 576, True),
          poly([(190, 170), (238, 263), (302, 193), (252, 124)], 12, True))
    glyph('@', 880, bowl(28, -48, 852, 726), bowl(173, 95, 706, 586, True),
          bowl(272, 167, 608, 517), bowl(388, 278, 490, 402, True), rect(510, 172, 647, 515, 8))
    glyph('$', 600, *GLYPHS['S']['contours'], rect(260, -112, 342, 801, 7))
    # Turkish lira: original upright stem, two diagonal bars and broad lower bowl.
    glyph('₺', 610,
          path(('M', 147, 0), ('L', 147, 700), ('L', 323, 700),
               ('L', 323, 185), ('Q', 464, 218, 474, 367), ('L', 587, 345),
               ('Q', 576, 1, 147, 0), ('Z',)),
          poly([(35, 385), (35, 492), (444, 626), (444, 517)], 5),
          poly([(35, 207), (35, 314), (444, 448), (444, 339)], 5))
    glyph('€', 670, *GLYPHS['C']['contours'], rect(1, 369, 440, 472, 5),
          rect(1, 219, 405, 322, 5))
    glyph('…', 780, *GLYPHS['.']['contours'],
          transformed(GLYPHS['.']['contours'][0], dx=245),
          transformed(GLYPHS['.']['contours'][0], dx=490))
    copy('–', '-', width=600, sx=1.5)
    copy('—', '-', width=850, sx=2.1)
    copy('’', "'")
    copy('‘', "'", sx=-1, dx=250)
    copy('”', '"')
    copy('“', '"', sx=-1, dx=435)


def add_accents():
    # Accent marks are original vectors, sized to leave an outline/shadow gap.
    accents = {
        '\u0308': [bowl(-173, 0, -39, 127), bowl(39, 0, 173, 127)],
        '\u0307': [bowl(-79, 0, 79, 141)],
        '\u0301': [poly([(-111, 0), (15, 156), (180, 156), (9, 0)], 10)],
        '\u0300': [poly([(-180, 156), (-15, 156), (111, 0), (-9, 0)], 10)],
        '\u0302': [poly([(-197, 0), (-78, 153), (78, 153), (197, 0),
                           (61, 0), (0, 73), (-61, 0)], 9)],
        '\u0306': [path(('M', -193, 139), ('L', -73, 139),
                       ('Q', -65, 67, 0, 67), ('Q', 65, 67, 73, 139),
                       ('L', 193, 139), ('Q', 182, -43, 0, -43),
                       ('Q', -182, -43, -193, 139), ('Z',))],
        '\u0303': [path(('M', -198, 22), ('Q', -168, 157, -69, 157),
                       ('Q', -14, 157, 37, 116), ('Q', 101, 64, 126, 151),
                       ('L', 219, 119), ('Q', 191, -17, 94, -17),
                       ('Q', 40, -17, -14, 24), ('Q', -77, 76, -103, -10), ('Z',))],
        '\u030c': [poly([(-197, 153), (-61, 153), (0, 80), (61, 153),
                           (197, 153), (78, 0), (-78, 0)], 9)],
        '\u0304': [rect(-175, 10, 175, 110, 9)],
        '\u030a': [bowl(-113, 0, 113, 219), bowl(-26, 78, 26, 141, True)],
        '\u0327': [path(('M', -25, 29), ('L', 75, 12), ('L', 37, -54),
                       ('Q', 127, -62, 123, -139), ('Q', 120, -236, -36, -236),
                       ('Q', -103, -236, -144, -206), ('L', -110, -128),
                       ('Q', -78, -151, -36, -151), ('Q', 14, -151, 15, -129),
                       ('Q', 17, -106, -65, -105), ('Z',))],
        '\u0328': [path(('M', 27, 20), ('L', 128, 0),
                       ('Q', 20, -57, 29, -109), ('Q', 34, -148, 96, -125),
                       ('L', 118, -202), ('Q', -69, -268, -83, -130),
                       ('Q', -94, -49, 27, 20), ('Z',))],
    }
    for code in range(0x00C0, 0x0250):
        char = chr(code)
        parts = unicodedata.normalize('NFD', char)
        if len(parts) != 2 or parts[0] not in GLYPHS or parts[1] not in accents:
            continue
        base, mark = parts
        # Accent replaces the dot in i; Turkish capital İ uses the separate I stem.
        source = 'ı' if base == 'i' else base
        copy(char, source)
        GLYPHS[char]['accent_base'] = source
        GLYPHS[char]['accent_start'] = len(GLYPHS[char]['contours'])
        GLYPHS[char]['below_mark'] = mark in ('\u0327', '\u0328')
        cx = GLYPHS[source]['advance'] * .5
        y = 0 if mark in ('\u0327', '\u0328') else (775 if base.isupper() else round(607 * STYLE["x_height_scale"]))
        for contour in accents[mark]:
            GLYPHS[char]['contours'].append(transformed(contour, dx=cx, dy=y))
    # Common combining-mark forms are also available independently.
    for char, contours in accents.items():
        glyph(char, 0, *[transformed(c, dx=-240, dy=775) for c in contours])


# ── v0.4 hand-drawn display letters ──────────────────────────────────────────
# Drawn directly in FINAL units (after scale/dilation of the rest): caps 815, x-height 626,
# flat bottoms -22. Grobold-inspired grammar, our own outlines:
#   • "flat" edges rise to the right (SLOPE), stems taper (wedge), corners only lightly rounded;
#   • rounds are tilted eggs with small leaning counters;
#   • S / e / 9 / a are written with a broad pen along a centreline (butt caps → angled cuts).
CAP, XH, BASE = 815, 626, -22
SLOPE = 0.045        # rise per unit x on straight edges (~2.6°)


def shear_y(contour, s, cx):
    """y += s·(x − cx): tilts every 'horizontal' edge of a contour (control points included)."""
    out = []
    for cmd in contour:
        o = [cmd[0]]
        for i in range(1, len(cmd), 2):
            x, y = cmd[i], cmd[i+1]
            o.extend([x, round(y + s * (x - cx), 2)])
        out.append(o)
    return out


def P(*contours):
    return to_pathops(list(contours))


def pen_stroke(width, *cmds):
    """Open centreline (M then Q/L) stroked with a broad pen: butt caps give angled terminal cuts."""
    p = pathops.Path()
    pen = p.getPen()
    for cmd in cmds:
        if cmd[0] == 'M': pen.moveTo(tuple(cmd[1:3]))
        elif cmd[0] == 'L': pen.lineTo(tuple(cmd[1:3]))
        elif cmd[0] == 'Q': pen.qCurveTo(tuple(cmd[1:3]), tuple(cmd[3:5]))
    pen.endPath()
    p.stroke(width, pathops.LineCap.BUTT_CAP, pathops.LineJoin.ROUND_JOIN, 4)
    p.convertConicsToQuads()
    return pathops.simplify(p)


def merge(add, sub=(), soften=6):
    result = None
    for piece in add:
        result = piece if result is None else pathops.op(result, piece, pathops.PathOp.UNION)
    for piece in sub:
        result = pathops.op(result, piece, pathops.PathOp.DIFFERENCE)
    contours = from_pathops(pathops.simplify(result, clockwise=True))
    return chunk(contours, soften, 0) if soften else contours


def ellipse(x0, y0, x1, y1, deg=0, taper=0.0):
    """True ellipse (8 quadratic arcs), rotated by deg. taper > 0 narrows the top (egg)."""
    cx, cy, rx, ry = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
    k = 1 / math.cos(math.pi / 8)
    def pt(a, scale=1.0):
        x, y = math.cos(a) * rx * scale, math.sin(a) * ry * scale
        x *= 1 - taper * (y / ry)           # egg: top narrower than bottom
        return (cx + x, cy + y)
    cmds = [['M', *pt(0)]]
    for i in range(8):
        a0 = i * math.pi / 4
        cmds.append(['Q', *pt(a0 + math.pi / 8, k), *pt(a0 + math.pi / 4)])
    cmds.append(['Z'])
    return transformed(cmds, rotate=deg, cx=cx, cy=cy)


def egg(x0, y0, x1, y1, deg, hole=False):
    c = bowl(x0, y0, x1, y1, hole)
    return transformed(c, rotate=deg, cx=(x0 + x1) / 2, cy=(y0 + y1) / 2)


def hand_drawn():
    B, T, X = BASE, CAP, XH
    g = {}

    def slanted(adv, contours):
        return [shear_y(c, SLOPE, adv / 2) for c in contours]

    # H — two leaning stems (right one taller and further out), short crossbar.
    adv = 730
    g['H'] = (adv, merge([P(poly([(14, 786), (304, 812), (272, 450), (418, 460), (430, 806), (714, 820),
                                  (634, 52), (404, 24), (414, 244), (310, 228), (300, -2), (52, -8)], 12))],
                         soften=0))

    # A — asymmetric tilted tent: apex left of centre, steep left leg, wide right leg spreading out,
    # sloped feet; narrow high counter. One 7-corner outline, lightly rounded corners.
    adv = 850
    g['A'] = (adv, merge(
        [P(poly([(392, 868), (890, 110), (590, -10), (500, 130), (296, 122), (255, -26), (-40, 72)], 14))],
        [P(poly([(350, 286), (458, 304), (410, 430)], 6))], soften=0))

    # B — near-vertical left edge, top rising to the right, lower bowl clearly wider than the upper;
    # small leaning lens-shaped counters.
    adv = 720
    g['B'] = (adv, merge(
        [P(path(('M', 36, 782), ('Q', 260, 826, 440, 824), ('Q', 642, 818, 642, 640),
                ('Q', 642, 540, 556, 478), ('Q', 700, 420, 700, 250), ('Q', 700, -16, 380, -16),
                ('Q', 180, -16, 40, 0), ('Z',)))],
        [P(path(('M', 298, 592), ('L', 294, 488), ('Q', 392, 490, 392, 556), ('Q', 392, 612, 298, 592), ('Z',))),
         P(path(('M', 284, 312), ('L', 298, 206), ('Q', 446, 198, 445, 276), ('Q', 440, 350, 284, 312), ('Z',)))],
        soften=0))

    # C — wide round body, small round counter, wedge aperture whose two terminals are cut by
    # straight lines (top falls inward, bottom rises outward).
    adv = 750
    g['C'] = (adv, merge(
        [P(path(('M', 718, 674), ('L', 543, 459), ('Q', 490, 524, 402, 540), ('Q', 284, 540, 282, 392),
                ('Q', 290, 256, 404, 250), ('Q', 490, 252, 560, 349), ('L', 755, 141),
                ('Q', 640, 10, 440, -32), ('Q', 260, -42, 140, 36), ('Q', -20, 160, -20, 382),
                ('Q', 4, 616, 200, 790), ('Q', 330, 852, 440, 846), ('Q', 600, 826, 718, 674), ('Z',)))],
        soften=0))

    # D — top rising to the right, big round right side, small leaning counter.
    adv = 720
    g['D'] = (adv, merge(
        [P(path(('M', 42, 782), ('Q', 230, 820, 400, 838), ('Q', 560, 852, 680, 740), ('Q', 756, 640, 754, 460),
                ('Q', 750, 280, 640, 160), ('Q', 520, 30, 410, 6), ('Q', 300, -16, 58, 0), ('Z',)))],
        [P(path(('M', 300, 228), ('Q', 420, 250, 472, 360), ('Q', 484, 420, 476, 460),
                ('Q', 462, 540, 410, 556), ('Q', 350, 560, 310, 515), ('Z',)))], soften=0))

    # E / F — rising arms of different lengths, slightly wavy (hand-cut) edges.
    adv = 600
    g['E'] = (adv, merge([P(poly([(36, 776), (588, 812), (560, 560), (304, 550), (304, 500), (484, 512),
                                  (466, 344), (298, 330), (296, 276), (580, 298), (576, 26), (40, -6)], 12))],
                         soften=0))
    adv = 560
    g['F'] = (adv, merge([P(poly([(38, 776), (588, 812), (560, 552), (306, 544), (298, 468), (504, 480),
                                  (518, 272), (308, 258), (298, 2), (44, -6)], 12))], soften=0))

    # G — wide round body, short spur bar pointing in, top terminal cut on a diagonal.
    adv = 820
    g['G'] = (adv, merge(
        [P(path(('M', 586, 496), ('Q', 560, 560, 474, 562), ('Q', 380, 560, 300, 480), ('Q', 248, 410, 250, 334),
                ('Q', 256, 206, 390, 204), ('Q', 470, 208, 514, 300), ('L', 372, 278), ('L', 388, 438),
                ('L', 824, 492), ('Q', 840, 330, 740, 190), ('Q', 640, 40, 470, -30), ('Q', 330, -56, 170, 0),
                ('Q', 30, 80, 0, 300), ('Q', -14, 520, 110, 660), ('Q', 240, 800, 420, 830),
                ('Q', 560, 844, 680, 780), ('Q', 740, 740, 774, 694), ('Z',)))], soften=0))

    # I J K L M N O S T — same grammar: leaning stems, rising tops, hand-cut corners.
    adv = 380
    g['I'] = (adv, merge([P(poly([(48, 786), (340, 820), (300, 14), (38, -16)], 14))], soften=0))
    adv = 590
    g['J'] = (adv, merge([P(path(('M', 536, 824), ('Q', 546, 560, 546, 300), ('Q', 540, 180, 470, 90),
                                 ('Q', 380, -20, 240, -24), ('Q', 120, -24, 40, 60), ('Q', -16, 140, -2, 276),
                                 ('L', 218, 346), ('Q', 206, 260, 232, 206), ('Q', 268, 210, 284, 296),
                                 ('L', 250, 776), ('Z',)))], soften=0))
    adv = 650
    g['K'] = (adv, merge([P(poly([(676, 634), (400, 374), (644, 168), (506, -42), (312, 140), (310, 14),
                                  (46, -16), (31, 786), (324, 820), (322, 642), (482, 862)], 12))], soften=0))
    adv = 590
    g['L'] = (adv, merge([P(poly([(40, 782), (340, 822), (316, 262), (560, 312), (572, 32), (46, -22)], 12))],
                         soften=0))
    adv = 880
    g['M'] = (adv, merge([P(poly([(212, 862), (456, 556), (690, 800), (936, 128), (696, -70), (588, 254),
                                  (452, 18), (288, 266), (190, -26), (-42, 124)], 12))], soften=0))
    adv = 780
    g['N'] = (adv, merge([P(poly([(51, 814), (432, 500), (442, 828), (740, 808), (694, 48), (312, 236),
                                  (324, 2), (38, -16)], 12))], soften=0))
    adv = 770
    g['O'] = (adv, merge(
        [P(path(('M', 640, 82), ('Q', 520, -40, 320, -34), ('Q', 140, -20, 40, 120), ('Q', -36, 250, 0, 520),
                ('Q', 60, 700, 260, 800), ('Q', 380, 852, 560, 830), ('Q', 680, 780, 740, 696),
                ('Q', 800, 600, 800, 450), ('Q', 796, 250, 640, 82), ('Z',)))],
        [P(path(('M', 506, 508), ('Q', 470, 570, 380, 566), ('Q', 280, 556, 236, 450), ('Q', 212, 380, 220, 316),
                ('Q', 240, 210, 340, 198), ('Q', 430, 200, 476, 280), ('Q', 530, 380, 506, 508), ('Z',)))],
        soften=0))
    adv = 600
    g['S'] = (adv, merge([P(path(('M', 422, 502), ('Q', 600, 440, 608, 277), ('Q', 610, 100, 420, 10),
                                 ('Q', 330, -24, 250, -22), ('Q', 110, -20, -22, 44), ('L', 54, 256),
                                 ('Q', 150, 206, 236, 206), ('Q', 322, 212, 322, 256), ('Q', 320, 280, 260, 300),
                                 ('Q', 120, 336, 60, 396), ('Q', 8, 450, 8, 527), ('Q', 10, 700, 180, 790),
                                 ('Q', 260, 830, 340, 828), ('Q', 470, 826, 578, 788), ('L', 520, 558),
                                 ('Q', 440, 596, 350, 606), ('Q', 244, 604, 240, 560), ('Q', 244, 540, 300, 534),
                                 ('Q', 380, 520, 422, 502), ('Z',)))], soften=0))
    adv = 530
    g['T'] = (adv, merge([P(poly([(518, 596), (398, 582), (398, 20), (114, -16), (128, 554), (-2, 542),
                                  (4, 786), (544, 830)], 12))], soften=0))

    # P Q R U V W X Y Z.
    adv = 650
    g['P'] = (adv, merge(
        [P(path(('M', 406, 820), ('Q', 600, 800, 680, 700), ('Q', 716, 600, 690, 480), ('Q', 650, 360, 540, 260),
                ('Q', 430, 170, 302, 156), ('L', 298, -22), ('L', 56, 2), ('L', 40, 782),
                ('Q', 230, 804, 406, 820), ('Z',)))],
        [P(path(('M', 422, 510), ('Q', 424, 580, 340, 578), ('Q', 310, 572, 285, 558), ('L', 275, 398),
                ('Q', 400, 430, 422, 510), ('Z',)))], soften=0))
    adv = 780
    g['Q'] = (adv, merge(
        [P(path(('M', 738, 694), ('Q', 800, 560, 790, 400), ('Q', 770, 230, 676, 126), ('L', 720, 82),
                ('L', 572, -64), ('L', 520, 6), ('Q', 380, -50, 230, -24), ('Q', 80, 20, 20, 150),
                ('Q', -40, 300, 0, 500), ('Q', 70, 700, 260, 800), ('Q', 380, 852, 560, 830),
                ('Q', 680, 780, 738, 694), ('Z',)))],
        [P(path(('M', 214, 337), ('Q', 214, 196, 372, 206), ('L', 350, 238), ('L', 434, 360), ('L', 486, 310),
                ('Q', 530, 400, 504, 508), ('Q', 470, 570, 370, 564), ('Q', 214, 540, 214, 337), ('Z',)))],
        soften=0))
    adv = 680
    g['R'] = (adv, merge(
        [P(path(('M', 692, 144), ('L', 536, -30), ('L', 294, 148), ('L', 292, -22), ('L', 50, 2),
                ('L', 34, 782), ('Q', 220, 804, 400, 820), ('Q', 600, 830, 676, 700), ('Q', 712, 600, 670, 470),
                ('Q', 600, 340, 484, 280), ('Z',)))],
        [P(path(('M', 279, 558), ('L', 269, 398), ('Q', 400, 430, 415, 510), ('Q', 420, 580, 330, 578),
                ('Q', 300, 572, 279, 558), ('Z',)))], soften=0))
    adv = 650
    g['U'] = (adv, merge([P(path(('M', 618, 790), ('Q', 630, 520, 631, 330), ('Q', 630, 150, 540, 50),
                                 ('Q', 450, -30, 320, -30), ('Q', 160, -24, 70, 70), ('Q', 6, 160, 7, 300),
                                 ('Q', 10, 560, 21, 824), ('L', 306, 776), ('L', 281, 306),
                                 ('Q', 280, 220, 312, 210), ('Q', 366, 230, 368, 356), ('L', 347, 740), ('Z',)))],
                         soften=0))
    adv = 750
    g['V'] = (adv, merge([P(poly([(238, 834), (384, 486), (554, 826), (802, 684), (364, -70), (-46, 668)], 12))],
                         soften=0))
    adv = 990
    g['W'] = (adv, merge([P(poly([(-40, 728), (230, 844), (336, 522), (516, 796), (668, 538), (788, 892),
                                  (1016, 728), (770, -6), (520, 250), (264, -72)], 12))], soften=0))
    adv = 810
    g['X'] = (adv, merge([P(poly([(214, 834), (408, 636), (594, 834), (816, 668), (580, 424), (822, 178),
                                  (632, -28), (406, 256), (224, -36), (-4, 154), (222, 410), (-8, 668)], 12))],
                         soften=0))
    adv = 710
    g['Y'] = (adv, merge([P(poly([(156, 830), (332, 556), (522, 830), (766, 664), (464, 272), (460, -16),
                                  (194, 18), (194, 312), (-58, 664)], 12))], soften=0))
    adv = 820
    g['Z'] = (adv, merge([P(poly([(34, -30), (276, 556), (-14, 540), (40, 800), (782, 824), (444, 264),
                                  (818, 308), (768, 18)], 12))], soften=0))

    # Digits — leaning, compact, small leaning counters; 6/9 swing a diagonal "wing", 7 has a notched bar.
    def dg(ch, adv, add, sub=()):
        g[ch] = (adv, merge([P(c) for c in add], [P(c) for c in sub], soften=0))

    dg('0', 676, [path(('M', 567, 102), ('Q', 470, -30, 300, -28), ('Q', 130, -10, 60, 130), ('Q', 0, 260, 10, 420),
                       ('Q', 30, 620, 160, 730), ('Q', 250, 812, 340, 822), ('Q', 500, 830, 600, 720),
                       ('Q', 676, 600, 674, 420), ('Q', 670, 220, 567, 102), ('Z',))],
       [path(('M', 426, 509), ('Q', 400, 566, 330, 556), ('Q', 236, 500, 236, 332), ('Q', 250, 206, 315, 204),
             ('Q', 390, 214, 417, 290), ('Q', 446, 380, 426, 509), ('Z',))])
    dg('1', 520, [poly([(95, 488), (-3, 614), (194, 801), (470, 829), (432, -13), (187, 19), (184, 563)], 10)])
    dg('2', 674, [path(('M', 639, 694), ('Q', 690, 560, 560, 380), ('Q', 470, 300, 367, 247), ('L', 638, 278),
                       ('L', 632, 25), ('L', 39, -5), ('L', 38, 148), ('Q', 40, 240, 190, 370), ('Q', 340, 500, 400, 570),
                       ('Q', 410, 600, 380, 604), ('Q', 300, 600, 124, 461), ('L', -10, 654), ('Q', 140, 800, 330, 850),
                       ('Q', 480, 860, 570, 780), ('Q', 620, 740, 639, 694), ('Z',))])
    dg('3', 640, [path(('M', 502, 456), ('Q', 600, 410, 632, 250), ('Q', 640, 150, 600, 96), ('Q', 520, 0, 400, -24),
                       ('Q', 260, -36, 108, 43), ('Q', 40, 80, -5, 119), ('L', 142, 329), ('Q', 260, 240, 340, 250),
                       ('Q', 380, 262, 378, 300), ('Q', 370, 350, 275, 363), ('L', 211, 357), ('L', 222, 528),
                       ('Q', 330, 520, 356, 570), ('Q', 352, 612, 290, 620), ('Q', 200, 610, 124, 550), ('L', 26, 732),
                       ('Q', 180, 840, 330, 845), ('Q', 480, 840, 552, 765), ('Q', 612, 700, 612, 610),
                       ('Q', 600, 510, 502, 456), ('Z',))])
    dg('4', 604, [poly([(330, 837), (575, 761), (201, 409), (303, 409), (306, 467), (536, 467), (526, 408), (615, 424),
                        (601, 192), (512, 191), (501, 8), (246, -3), (264, 189), (16, 170), (-5, 503)], 8)])
    dg('5', 572, [path(('M', 219, 539), ('Q', 380, 560, 500, 490), ('Q', 570, 430, 569, 340), ('Q', 560, 180, 440, 80),
                       ('Q', 300, -12, 48, 0), ('L', 3, 234), ('Q', 150, 214, 240, 256), ('Q', 296, 290, 296, 330),
                       ('Q', 290, 384, 180, 385), ('Q', 100, 382, 34, 352), ('L', 68, 783), ('L', 543, 806),
                       ('L', 548, 607), ('L', 244, 610), ('Z',))])
    dg('6', 562, [path(('M', 509, 477), ('Q', 590, 380, 560, 230), ('Q', 520, 90, 400, 30), ('Q', 240, -30, 104, 21),
                       ('Q', -20, 120, -15, 330), ('Q', -5, 500, 120, 660), ('Q', 220, 790, 335, 854), ('L', 488, 706),
                       ('Q', 400, 640, 305, 522), ('Q', 400, 560, 509, 477), ('Z',))],
       [path(('M', 313, 212), ('Q', 380, 250, 363, 327), ('Q', 330, 376, 253, 349), ('Q', 184, 300, 203, 234),
             ('Q', 240, 190, 313, 212), ('Z',))])
    dg('7', 540, [poly([(560, 825), (471, 519), (536, 521), (504, 339), (433, 336), (351, -4), (76, 28), (170, 328),
                        (90, 325), (101, 503), (226, 510), (265, 605), (6, 574), (-8, 794)], 8)])
    dg('8', 604, [path(('M', 554, 421), ('Q', 610, 320, 580, 190), ('Q', 540, 70, 420, 10), ('Q', 300, -30, 160, 0),
                       ('Q', 40, 40, 10, 180), ('Q', 0, 330, 102, 417), ('Q', 70, 440, 60, 480), ('Q', 30, 580, 90, 700),
                       ('Q', 170, 810, 304, 825), ('Q', 440, 836, 530, 770), ('Q', 590, 700, 582, 576),
                       ('Q', 570, 500, 518, 458), ('Q', 545, 440, 554, 421), ('Z',))],
       [path(('M', 356, 631), ('Q', 340, 660, 290, 650), ('Q', 244, 620, 244, 565), ('Q', 260, 510, 300, 511),
             ('Q', 362, 530, 356, 631), ('Z',)),
        path(('M', 341, 185), ('Q', 370, 240, 358, 300), ('Q', 330, 340, 260, 320), ('Q', 210, 280, 210, 213),
             ('Q', 230, 140, 284, 141), ('Q', 320, 150, 341, 185), ('Z',))])
    dg('9', 568, [path(('M', 57, 343), ('Q', -30, 450, 0, 600), ('Q', 40, 720, 170, 800), ('Q', 300, 850, 461, 799),
                       ('Q', 580, 700, 581, 461), ('Q', 560, 250, 363, 85), ('Q', 300, 20, 231, -34), ('L', 77, 115),
                       ('Q', 200, 220, 261, 298), ('Q', 150, 260, 57, 343), ('Z',))],
       [path(('M', 253, 608), ('Q', 190, 570, 203, 493), ('Q', 240, 450, 312, 472), ('Q', 380, 510, 363, 586),
             ('Q', 330, 630, 253, 608), ('Z',))])

    # e — carved, not written: a fat, nearly round bowl minus a rounded-triangle eye and a wedge aperture.
    # Targets measured against the reference style: width/height ≈ 0.95, flat-ish bottom (≈ -6),
    # top stroke ≈ 0.32 of height, eye ≈ 0.16 of width across its middle, bar rising ~37° and
    # exiting the right side high as a clean corner; lower terminal cut by a steep line.
    adv = 670
    g['e'] = (adv, merge(
        # One outer curve: round top/left, bottom sweeping right into a low terminal corner.
        [P(path(('M', 650, 314), ('Q', 650, 447, 557, 542), ('Q', 464, 636, 332, 636),
                ('Q', 200, 636, 107, 542), ('Q', 14, 447, 14, 314), ('Q', 14, 181, 107, 86),
                ('Q', 200, -8, 332, -8), ('Q', 512, -12, 640, 78), ('Z',)))],
        # Open the eye by ~18% in each axis; keep the outer silhouette and diagonal bar.
        [P(path(('M', 226, 281), ('L', 427, 434), ('Q', 408, 484, 349, 484), ('Q', 269, 481, 229, 415),
                ('Q', 205, 373, 212, 326), ('Q', 217, 297, 226, 281), ('Z',))),
         P(path(('M', 306, 194), ('L', 410, 294), ('L', 800, 602), ('L', 800, -90), ('L', 714, -90),
                ('L', 530, 240), ('Q', 466, 194, 412, 186), ('Q', 356, 178, 318, 206), ('Z',)))]))

    # a — low hook with an angled terminal, small bowl with a leaning oval counter,
    # right stem leaning slightly inward towards the foot.
    adv = 610
    g['a'] = (adv, merge(
        [P(path(('M', 349, 68), ('Q', 300, 0, 189, 0), ('Q', 80, 0, 30, 80), ('Q', -10, 140, -9, 206),
                ('Q', -6, 330, 90, 375), ('Q', 150, 400, 197, 396), ('Q', 280, 394, 328, 352), ('L', 328, 382),
                ('Q', 326, 456, 220, 458), ('Q', 140, 456, 61, 428), ('L', 100, 587), ('Q', 200, 612, 306, 612),
                ('Q', 450, 610, 520, 537), ('Q', 564, 470, 561, 395), ('L', 550, 17), ('L', 349, 0), ('Z',)))],
        [P(path(('M', 222, 252), ('Q', 150, 244, 147, 188), ('Q', 156, 140, 239, 148), ('Q', 318, 160, 312, 214),
                ('Q', 302, 258, 222, 252), ('Z',)))], soften=0))

    # s — narrow, heavy, tight apertures; terminals cut straight up/down.
    adv = 450
    g['s'] = (adv, merge([P(path(('M', 14, 198), ('Q', 80, 180, 130, 180), ('Q', 215, 184, 215, 210),
                                 ('Q', 212, 240, 139, 254), ('Q', 60, 276, 30, 320), ('Q', 8, 360, 9, 422),
                                 ('Q', 12, 540, 120, 585), ('Q', 190, 606, 250, 603), ('Q', 350, 600, 421, 582),
                                 ('L', 421, 406), ('Q', 360, 420, 320, 420), ('Q', 242, 416, 242, 392),
                                 ('Q', 246, 368, 321, 346), ('Q', 400, 322, 430, 280), ('Q', 458, 230, 456, 177),
                                 ('Q', 450, 60, 330, 12), ('Q', 260, -10, 178, -10), ('Q', 90, -8, 14, 11),
                                 ('Z',)))], soften=0))

    # Remaining lowercase — same grammar: heavy, compact, leaning stems, small leaning counters,
    # straight-cut terminals. (ı has no reference: it is our i stem without the dot.)
    def lc(ch, adv, add, sub=()):
        g[ch] = (adv, merge([P(c) for c in add], [P(c) for c in sub], soften=0))

    lc('b', 640, [path(('M', 389, 607), ('Q', 520, 600, 610, 500), ('Q', 664, 420, 664, 306), ('Q', 660, 140, 530, 40),
                       ('Q', 450, -6, 358, -5), ('Q', 270, -2, 196, 45), ('L', 196, 2), ('L', 38, 2), ('L', 32, 808),
                       ('L', 263, 826), ('L', 254, 566), ('Q', 320, 606, 389, 607), ('Z',))],
       [path(('M', 371, 204), ('Q', 460, 230, 452, 322), ('Q', 430, 392, 330, 382), ('Q', 240, 360, 238, 262),
             ('Q', 260, 192, 371, 204), ('Z',))])
    lc('c', 530, [path(('M', 542, 71), ('Q', 450, -6, 309, -5), ('Q', 140, 0, 50, 110), ('Q', -7, 190, -7, 294),
                       ('Q', -4, 460, 120, 550), ('Q', 210, 608, 325, 608), ('Q', 440, 606, 537, 538), ('L', 405, 365),
                       ('Q', 350, 396, 290, 394), ('Q', 208, 370, 210, 282), ('Q', 240, 206, 355, 225), ('L', 406, 251),
                       ('Z',))])
    lc('d', 660, [path(('M', 602, 7), ('L', 406, -6), ('L', 406, 51), ('Q', 340, -10, 255, -10), ('Q', 120, -6, 40, 100),
                       ('Q', -9, 180, -9, 291), ('Q', -6, 450, 100, 550), ('Q', 180, 602, 267, 602),
                       ('Q', 340, 600, 385, 565), ('L', 376, 803), ('L', 606, 821), ('Z',))],
       [path(('M', 325, 205), ('Q', 400, 240, 392, 326), ('Q', 370, 388, 280, 384), ('Q', 200, 360, 192, 290),
             ('Q', 200, 200, 325, 205), ('Z',))])
    lc('f', 420, [path(('M', 81, 404), ('L', 12, 404), ('L', 44, 576), ('L', 81, 576), ('Q', 84, 700, 166, 770),
                       ('Q', 230, 814, 315, 813), ('Q', 350, 812, 374, 806), ('L', 400, 640), ('L', 366, 640),
                       ('Q', 298, 636, 298, 576), ('L', 384, 576), ('L', 384, 404), ('L', 298, 404), ('L', 304, 8),
                       ('L', 80, -10), ('Z',))])
    lc('g', 590, [path(('M', 96, 16), ('Q', 170, -12, 240, -12), ('Q', 330, -6, 330, 47), ('Q', 326, 108, 214, 111),
                       ('Q', 160, 108, 111, 84), ('L', 130, 144), ('Q', -9, 230, -9, 366), ('Q', -4, 500, 110, 575),
                       ('Q', 180, 612, 261, 612), ('Q', 330, 610, 375, 594), ('L', 392, 699), ('L', 552, 630),
                       ('L', 500, 528), ('Q', 565, 460, 565, 370), ('Q', 560, 250, 464, 185), ('Q', 562, 120, 562, 19),
                       ('Q', 556, -110, 430, -172), ('Q', 340, -202, 239, -202), ('Q', 120, -200, -1, -160), ('Z',))],
       [path(('M', 289, 297), ('Q', 360, 316, 352, 372), ('Q', 330, 428, 264, 415), ('Q', 196, 396, 201, 340),
             ('Q', 214, 286, 289, 297), ('Z',))])
    lc('h', 590, [path(('M', 248, 534), ('Q', 300, 596, 382, 596), ('Q', 480, 592, 530, 515), ('Q', 556, 470, 555, 397),
                       ('L', 549, 10), ('L', 338, -8), ('L', 338, 357), ('Q', 336, 398, 294, 398),
                       ('Q', 248, 396, 248, 355), ('L', 248, -9), ('L', 38, 9), ('L', 31, 797), ('L', 255, 815), ('Z',))])
    stem_i = poly([(40, 567), (263, 585), (252, 8), (40, -10)], 12)
    lc('ı', 305, [stem_i])
    lc('i', 305, [stem_i, poly([(39, 820), (268, 830), (256, 654), (38, 638)], 16)])
    lc('j', 390, [path(('M', 335, 56), ('Q', 330, -120, 160, -190), ('Q', 130, -193, 106, -193), ('L', 59, -193),
                       ('L', 4, -10), ('L', 27, -10), ('Q', 118, -6, 118, 74), ('L', 118, 564), ('L', 335, 582), ('Z',)),
                  poly([(334, 652), (116, 635), (117, 818), (346, 828)], 16)])
    lc('k', 580, [poly([(29, 795), (256, 813), (236, 391), (333, 594), (554, 576), (424, 324), (574, 93),
                        (366, -10), (236, 241), (242, 8), (34, -10)], 10)])
    lc('l', 300, [poly([(35, 795), (261, 813), (252, 8), (42, -10)], 12)])
    lc('m', 890, [path(('M', 654, 356), ('Q', 650, 399, 603, 399), ('Q', 552, 396, 551, 353), ('L', 557, 11),
                       ('L', 340, -7), ('L', 344, 354), ('Q', 342, 399, 291, 399), ('Q', 240, 396, 240, 353),
                       ('L', 250, -8), ('L', 40, 10), ('L', 28, 579), ('L', 232, 597), ('L', 232, 505),
                       ('Q', 300, 597, 404, 597), ('Q', 490, 594, 540, 537), ('Q', 600, 597, 672, 597),
                       ('Q', 780, 592, 830, 515), ('Q', 860, 460, 859, 392), ('L', 865, 11), ('L', 648, -7), ('Z',))])
    lc('n', 590, [path(('M', 236, 514), ('Q', 300, 591, 383, 591), ('Q', 480, 588, 530, 512), ('Q', 556, 465, 556, 393),
                       ('L', 556, 6), ('L', 340, -12), ('L', 343, 352), ('Q', 342, 394, 295, 394),
                       ('Q', 246, 392, 245, 351), ('L', 249, -10), ('L', 36, 5), ('L', 28, 574), ('L', 236, 591), ('Z',))])
    lc('o', 620, [path(('M', 623, 344), ('Q', 640, 200, 530, 70), ('Q', 440, -12, 320, -8), ('Q', 160, 0, 60, 100),
                       ('Q', -10, 180, -7, 255), ('Q', -20, 400, 90, 530), ('Q', 180, 606, 290, 606),
                       ('Q', 440, 604, 550, 515), ('Q', 615, 450, 623, 344), ('Z',))],
       [path(('M', 203, 259), ('Q', 240, 190, 341, 216), ('Q', 420, 250, 406, 344), ('Q', 380, 400, 269, 387),
             ('Q', 180, 350, 203, 259), ('Z',))])
    lc('p', 640, [path(('M', 239, 598), ('L', 239, 530), ('Q', 310, 598, 388, 598), ('Q', 520, 594, 600, 480),
                       ('Q', 654, 390, 654, 294), ('Q', 650, 140, 540, 40), ('Q', 460, -15, 378, -15),
                       ('Q', 300, -12, 248, 30), ('L', 254, -169), ('L', 43, -187), ('L', 32, 580), ('Z',))],
       [path(('M', 375, 211), ('Q', 460, 250, 435, 341), ('Q', 410, 400, 297, 377), ('Q', 210, 340, 236, 246),
             ('Q', 270, 190, 375, 211), ('Z',))])
    lc('q', 640, [path(('M', 382, -165), ('L', 388, 34), ('Q', 330, -10, 258, -10), ('Q', 130, -6, 40, 100),
                       ('Q', -18, 190, -18, 298), ('Q', -14, 450, 90, 550), ('Q', 170, 602, 248, 602),
                       ('Q', 340, 600, 397, 534), ('L', 397, 602), ('L', 605, 584), ('L', 594, -183), ('Z',))],
       [path(('M', 401, 345), ('Q', 380, 396, 300, 395), ('Q', 190, 370, 201, 250), ('Q', 240, 190, 339, 215),
             ('Q', 430, 260, 401, 345), ('Z',))])
    lc('r', 420, [path(('M', 230, 598), ('L', 230, 487), ('Q', 270, 598, 382, 598), ('Q', 410, 598, 433, 595),
                       ('L', 381, 382), ('Q', 370, 383, 346, 383), ('Q', 243, 378, 243, 269), ('L', 243, 12),
                       ('L', 33, -6), ('L', 27, 580), ('Z',))])
    lc('t', 370, [path(('M', 329, -7), ('Q', 290, -10, 267, -10), ('Q', 70, -4, 64, 175), ('L', 64, 412), ('L', 9, 411),
                       ('L', 15, 584), ('L', 62, 584), ('L', 62, 650), ('L', 279, 748), ('L', 279, 584), ('L', 360, 596),
                       ('L', 349, 412), ('L', 281, 412), ('L', 281, 237), ('Q', 284, 187, 328, 187),
                       ('Q', 352, 187, 364, 191), ('Z',))])
    lc('u', 590, [path(('M', 362, 88), ('Q', 310, -13, 198, -13), ('Q', 36, -8, 30, 161), ('L', 30, 581),
                       ('L', 253, 599), ('L', 247, 229), ('Q', 250, 184, 293, 184), ('Q', 336, 186, 337, 228),
                       ('L', 331, 581), ('L', 561, 599), ('L', 546, 4), ('L', 362, -4), ('Z',))])
    lc('v', 520, [poly([(193, 603), (250, 208), (318, 603), (535, 586), (397, 9), (109, -8), (-22, 586)], 10)])
    lc('w', 790, [poly([(259, 327), (316, 586), (473, 598), (532, 324), (593, 607), (811, 589), (657, 12),
                        (450, -4), (394, 275), (335, 12), (130, -5), (-26, 589), (198, 607)], 10)])
    lc('x', 560, [poly([(276, 430), (323, 598), (554, 580), (435, 305), (574, 12), (334, -5), (276, 176),
                        (217, -5), (-19, 12), (114, 305), (-10, 580), (222, 598)], 10)])
    lc('y', 510, [poly([(271, 368), (321, 588), (527, 570), (313, -184), (87, -166), (173, 69), (-15, 561),
                        (203, 588)], 10)])
    lc('z', 490, [poly([(54, 580), (471, 580), (303, 211), (481, 211), (438, 3), (-15, 3), (155, 372), (12, 372)], 10)])
    return g


# Side bearings (left, right) per base letter/digit, measured from the reference display style.
# The tight, leaning-together set is a big part of the look (most sides are ~0 or negative);
# our own advance widths left 10–100 units of extra air ("e looks detached").
METRICS = {
    'A': (-50, -50), 'B': (32, -23), 'C': (-16, -7), 'D': (42, -31), 'E': (35, 5), 'F': (37, -44), 'G': (-6, -12), 'H': (13, 6),
    'I': (38, 29), 'J': (-7, 33), 'K': (31, -42), 'L': (40, -12), 'M': (-43, -54), 'N': (38, 22), 'O': (-26, -34), 'P': (40, -55),
    'Q': (-30, -21), 'R': (33, -22), 'S': (-23, -17), 'T': (-3, -23), 'U': (7, 13), 'V': (-48, -56), 'W': (-42, -33), 'X': (-10, -17),
    'Y': (-60, -68), 'Z': (-16, -3), 'a': (-9, 42), 'b': (32, -24), 'c': (-7, -14), 'd': (-9, 58), 'e': (-11, -1), 'f': (12, 16),
    'g': (-9, 27), 'h': (31, 32), 'i': (38, 37), 'j': (4, 46), 'k': (29, 4), 'l': (35, 38), 'm': (28, 26), 'n': (28, 35),
    'o': (-11, -10), 'p': (32, -15), 'q': (-18, 32), 'r': (27, -15), 's': (9, -10), 't': (9, 7), 'u': (30, 30), 'v': (-22, -22),
    'w': (-26, -23), 'x': (-19, -17), 'y': (-15, -25), 'z': (-15, 5), '0': (7, 2), '1': (-3, 48), '2': (-10, 3), '3': (-5, 1),
    '4': (-5, -12), '5': (3, 2), '6': (-17, -19), '7': (-8, -21), '8': (-6, 9), '9': (-14, -14),
}


WORD_SPACE = 300   # reference word gap; ours was 227 and words crowded together
CAPITAL_SPACING = 20  # +0.96 px at 48 px: ease touching outlines without loosening lowercase/digits.


def apply_metrics():
    """Re-space letters/digits to METRICS. Accented forms take their base letter's spacing."""
    for space in (' ', '\u00a0'):
        if space in GLYPHS:
            GLYPHS[space]['advance'] = WORD_SPACE
    bounds = {}
    for char, entry in GLYPHS.items():
        if entry['contours'] and entry['advance']:
            b = to_pathops(entry['contours']).bounds
            bounds[char] = (b[0], b[2])
    for char, entry in GLYPHS.items():
        if not entry['contours'] or not entry['advance']:
            continue
        base = unicodedata.normalize('NFD', char)[0]
        key = 'i' if base in ('i', 'ı') else base
        if key not in METRICS:
            continue
        # i/ı/İ: own stem; others: the base letter's outline (accents may overhang, like the reference).
        ref = char if base in ('i', 'ı') else (base if base in bounds else char)
        x0, x1 = bounds[ref]
        lsb, rsb = METRICS[key]
        if base.isupper():
            rsb += CAPITAL_SPACING
        dx = lsb - x0
        entry['contours'] = [transformed(c, dx=dx) for c in entry['contours']]
        entry['advance'] = max(1, round(x1 + dx + rsb))


HAND: dict[str, tuple] = {}
# Accent centre for asymmetric hand-drawn letters (default: advance / 2). A's apex sits left of centre.
ACCENT_CX = {'A': 400}
# Extra accent lift where the hand-drawn letter is taller than the cap line (A's sharp apex).
ACCENT_DY = {'A': 80}


def apply_hand_drawn(pre_hand):
    """Swap in the hand-drawn letters; rebuild their accented forms on the new shapes."""
    k, kx = STYLE["scale"], STYLE["scale"] * STYLE["condense"]
    shift = STYLE["dilate"] - STYLE["tighten"] / 2
    for char, (adv, contours) in HAND.items():
        GLYPHS[char]['advance'], GLYPHS[char]['contours'] = adv, contours
    for char, (base, old_adv, accent) in pre_hand.items():
        new_adv = HAND[base][0]
        old_final = round(old_adv * kx) + 2 * STYLE["dilate"] - STYLE["tighten"]
        cx = ACCENT_CX.get(base, new_adv / 2)
        # Hand-drawn caps run taller than the old cap line → every upper mark sits a bit higher.
        lift = ACCENT_DY.get(base, 45) if base.isupper() and not GLYPHS[char].get('below_mark') else 0
        moved = [transformed(transformed(c, sx=kx, sy=k), dx=shift + cx - old_final / 2, dy=lift) for c in accent]
        GLYPHS[char]['advance'] = new_adv
        # Cedilla/ogonek touch the base: merge so the glyph stays overlap-free for SDF.
        merged = to_pathops(HAND[base][1] + chunk(moved))
        GLYPHS[char]['contours'] = from_pathops(pathops.simplify(merged, clockwise=True))


def to_pen(contours):
    pen = TTGlyphPen(None)
    for contour in contours:
        for cmd in contour:
            kind = cmd[0]
            values = [round(n) for n in cmd[1:]]
            if kind == 'M': pen.moveTo(tuple(values))
            elif kind == 'L': pen.lineTo(tuple(values))
            elif kind == 'Q': pen.qCurveTo(tuple(values[:2]), tuple(values[2:]))
            elif kind == 'Z': pen.closePath()
    return pen.glyph()


def build():
    draw_alphabet()
    redesign_v02()
    scale_lowercase()
    draw_numbers_and_symbols()
    add_accents()
    # Subtle character-specific optical lean, authored here rather than random at runtime.
    lean = {'B': -.010, 'D': .010, 'E': -.007, 'L': -.009, 'S': .006,
            'a': -.009, 'e': .009, 'n': -.006, 'v': .008, 'y': .011,
            '2': -.011, '3': .009, '5': -.007, '7': .008, '9': -.009}
    for char, entry in GLYPHS.items():
        base = unicodedata.normalize('NFD', char)[0]
        amount = lean.get(base, 0)
        if amount:
            entry['contours'] = [transformed(c, shear=amount) for c in entry['contours']]
    hand = hand_drawn()
    pre_hand = {}   # derived glyph (Ş, Ö, é…) → its accent contours + original advance, before transforms
    for char, entry in GLYPHS.items():
        base = entry.get('accent_base')
        if base in hand:
            pre_hand[char] = (base, entry['advance'], entry['contours'][entry['accent_start']:])
    HAND.update(hand)
    # Global size → (optional bounce) → rounded dilation → tighter set.
    shift = STYLE["dilate"] - STYLE["tighten"] / 2
    k = STYLE["scale"]
    for char, entry in GLYPHS.items():
        kx = k * STYLE["condense"]
        entry['advance'] = round(entry['advance'] * kx)
        entry['contours'] = [transformed(c, sx=kx, sy=k) for c in entry['contours']]
        if not entry['contours']:
            if entry['advance']:
                entry['advance'] = max(1, entry['advance'] - STYLE["tighten"] // 2)   # space
            continue
        deg, hop = bounce_of(char)
        if deg or hop:
            cx = entry['advance'] / 2
            entry['contours'] = [transformed(c, dy=hop, rotate=deg, cx=cx, cy=330) for c in entry['contours']]
        d, r = DETAILED.get(char, (STYLE["dilate"], STYLE["round"]))
        entry['contours'] = chunk(entry['contours'], d, r)
        if entry['advance']:
            # Same advance/shift for every glyph keeps spacing uniform; detailed symbols just sit lighter.
            entry['contours'] = [transformed(c, dx=shift) for c in entry['contours']]
            entry['advance'] += 2 * STYLE["dilate"] - STYLE["tighten"]
    apply_hand_drawn(pre_hand)
    apply_metrics()
    OUT.mkdir(parents=True, exist_ok=True)
    order = ['.notdef'] + [f'uni{ord(c):04X}' for c in GLYPHS]
    cmap = {ord(c): f'uni{ord(c):04X}' for c in GLYPHS}
    glyphs = {'.notdef': to_pen([rect(50, 0, 550, 700), rect(150, 100, 450, 600, hole=True)])}
    metrics = {'.notdef': (600, 50)}
    for char, entry in GLYPHS.items():
        name = cmap[ord(char)]
        glyphs[name] = to_pen(entry['contours'])   # merged in chunk(): no overlap flag needed
        metrics[name] = (entry['advance'], 0)
    fb = FontBuilder(1000, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    for name, g in glyphs.items():
        g.recalcBounds(fb.font['glyf'])
        metrics[name] = (metrics[name][0], getattr(g, 'xMin', 0))
    fb.setupHorizontalMetrics(metrics)
    k, d = STYLE["scale"], STYLE["dilate"]
    # Line metrics = the reference display style's (800 / -200): TMP places text in its box from these,
    # so a taller ascent pushed our titles/buttons visibly lower. Accents may rise above the ascent
    # (TMP does not clip); Windows clipping metrics cover the real glyph extremes.
    asc, desc = 800, -200
    y_max = max((to_pathops(e['contours']).bounds[3] for e in GLYPHS.values() if e['contours']), default=asc)
    y_min = min((to_pathops(e['contours']).bounds[1] for e in GLYPHS.values() if e['contours']), default=desc)
    fb.setupHorizontalHeader(ascent=asc, descent=desc, lineGap=0)
    fb.setupOS2(version=4, sTypoAscender=asc, sTypoDescender=desc, sTypoLineGap=0,
                usWinAscent=math.ceil(y_max), usWinDescent=math.ceil(-y_min),
                sxHeight=round(529 * STYLE["x_height_scale"] * k + d), sCapHeight=round(700 * k + d),
                usWeightClass=400, usWidthClass=5, fsType=0,
                fsSelection=0xC0, yStrikeoutSize=48, yStrikeoutPosition=290)
    fb.setupNameTable({
        'familyName': FAMILY, 'styleName': 'Regular',
        'uniqueFontIdentifier': f'TinyFixers: TinyFixers Pop Regular: {VERSION}',
        'fullName': FAMILY + ' Regular', 'psName': 'TinyFixersPop-Regular',
        'version': f'Version {VERSION}00',
        'copyright': 'Original outlines created for TinyFixers, 2026.',
        'description': f'Original TinyFixers display typeface. Design draft v{VERSION}. '
                       'All outlines are authored in the accompanying source; no third-party font outlines used.',
        'designer': 'TinyFixers',
    })
    fb.setupPost(underlinePosition=-90, underlineThickness=50)
    fb.setupMaxp()
    pairs = [('A', 'V', -35), ('V', 'A', -35), ('T', 'a', -25), ('T', 'o', -30),
             ('Y', 'a', -30), ('Y', 'o', -35), ('W', 'a', -20), ('F', 'o', -20),
             ('O', 'y', -15), ('o', 'y', -15), ('y', 'n', -10), ('v', 'i', -10),
             ('L', 'T', -20), ('A', 'T', -20)]
    fea = 'languagesystem DFLT dflt; languagesystem latn dflt; feature kern {\n'
    # Precomposed accents must space like their base letters (To / Tö, Ta / Tá, etc.).
    variants = {}
    for char in GLYPHS:
        variants.setdefault(unicodedata.normalize('NFD', char)[0], []).append(char)
    fea += '\n'.join(f'pos {cmap[ord(a)]} {cmap[ord(b)]} {value};'
                     for left, right, value in pairs
                     for a in variants[left] for b in variants[right])
    fea += '\n} kern;'
    addOpenTypeFeaturesFromString(fb.font, fea)
    target = OUT / 'TinyFixersPop-Regular.ttf'
    fb.save(target)
    (HERE / 'glyphs.json').write_text(json.dumps(GLYPHS, indent=2, ensure_ascii=False) + '\n')
    font = TTFont(target)
    svg_dir = HERE / 'glyphs'
    svg_dir.mkdir(exist_ok=True)
    glyph_set = font.getGlyphSet()
    for char in 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789çÇşŞğĞıİöÖüÜ':
        name = cmap[ord(char)]
        pen = SVGPathPen(glyph_set)
        glyph_set[name].draw(pen)
        width = GLYPHS[char]['advance']
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="-30 -1070 {width+60} 1400">'
               f'<title>{char} — {FAMILY} v{VERSION}</title>'
               f'<g transform="scale(1,-1)"><path d="{pen.getCommands()}"/></g></svg>\n')
        (svg_dir / f'U{ord(char):04X}.svg').write_text(svg)
    check(font)
    preview(target)
    print(f'Created {target.relative_to(ROOT)}: {len(cmap)} characters, {target.stat().st_size} bytes')


def check(font):
    cmap = font.getBestCmap()
    required = ''.join(chr(c) for c in range(32, 127)) + 'çÇşŞğĞıİöÖüÜ·₺€'
    missing = [c for c in required if ord(c) not in cmap]
    assert not missing, missing
    for code, name in cmap.items():
        assert font['hmtx'][name][0] > 0 or unicodedata.combining(chr(code))
    assert font['OS/2'].usWeightClass == 400
    assert len(font['glyf'].glyphs) == len(cmap) + 1
    print('TTF checks passed: Latin, digits, Turkish; source outlines only.')


def preview(target):
    im = Image.new('RGB', (1600, 1500), '#f7f0df')
    d = ImageDraw.Draw(im)
    # Captions are in the OS UI font; every design sample is in our new TTF.
    caption = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 27)
    small = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 22)
    d.text((65, 36), f'TINYFIXERS POP / v{VERSION}', font=caption, fill='#34232c')
    d.text((65, 78), 'Özgün harf taslağı · Türkçe + Latin · Düzenlenebilir çizimler + TTF',
           font=small, fill='#78656c')
    d.rounded_rectangle((55, 140, 1545, 420), 48, fill='#77142f', outline='#f9b91a', width=10)
    sample(d, target, 'Bölüm 229', (800, 277), 170, '#fffbe8', '#350717', 5)
    d.rounded_rectangle((70, 468, 790, 705), 42, fill='#67ad05', outline='#f9b91a', width=9)
    d.rounded_rectangle((830, 468, 1530, 705), 42, fill='#067fc1', outline='#f9b91a', width=9)
    sample(d, target, 'Oyna', (430, 579), 165, '#fffbe8', '#1b4107', 5)
    sample(d, target, 'Devam', (1180, 579), 138, '#fffbe8', '#073053', 4)
    sample(d, target, 'Seviye 147 · Tekrar Dene', (800, 801), 79, '#443343', '#443343', 0)
    sample(d, target, 'Çç Şş Ğğ Iı İi Öö Üü', (800, 934), 90, '#77142f', '#77142f', 0)
    for i, text in enumerate(['ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz',
                              '0123456789  ! ? + − = ₺ €']):
        sample(d, target, text.replace('−', '-'), (800, 1080+i*113),
               67 if i < 2 else 85, '#34232c', '#34232c', 0)
    d.text((65, 1435), 'İlk çizim taslağıdır; harf şekilleri ve aralıkları geri bildirime göre düzenlenecek.',
           font=small, fill='#78656c')
    im.save(HERE / f'Preview-v{VERSION.replace(".", "")}.png')


def sample(draw, target, text, center, size, face, edge, stroke):
    font = ImageFont.truetype(str(target), size)
    x, y = center
    if stroke:
        draw.text((x, y+7), text, font=font, anchor='mm', fill=edge,
                  stroke_fill=edge, stroke_width=stroke+1)
    draw.text((x, y), text, font=font, anchor='mm', fill=face,
              stroke_fill=edge, stroke_width=stroke)


if __name__ == '__main__':
    build()
