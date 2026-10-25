import os
import struct
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Interface", "AddOns", "HordesOfAzeroth", "Textures", "ControllerGlyphs.tga")
FONT = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "arialbd.ttf")

CELL = 64
SCALE = 4
BIG = CELL * SCALE
COLUMNS = 8
ROWS = 8

FILL = (21, 19, 15, 238)
GOLD = (200, 162, 74, 255)
CREAM = (242, 234, 210, 255)
DIM = (90, 82, 72, 255)
LIT = (255, 209, 0, 255)
XBOX = {"a": (95, 191, 63, 255), "b": (224, 64, 58, 255), "x": (63, 143, 232, 255), "y": (240, 196, 25, 255)}
PLAYSTATION = {"a": (127, 168, 240, 255), "b": (239, 95, 106, 255), "x": (228, 138, 216, 255), "y": (79, 208, 168, 255)}

LAYOUT = [
    [("xbox", n) for n in ("a", "b", "x", "y", "lb", "rb", "lt", "rt")],
    [("ps", n) for n in ("a", "b", "x", "y", "lb", "rb", "lt", "rt")],
    [("xbox", n) for n in ("view", "menu", "ls", "rs", "l3", "r3", "home")],
    [("ps", n) for n in ("view", "menu", "ls", "rs", "l3", "r3", "home")],
    [("any", n) for n in ("dpad", "up", "down", "left", "right", "updown", "leftright")],
]
PILLS = {
    "xbox": {"lb": "LB", "rb": "RB", "lt": "LT", "rt": "RT", "view": "View", "menu": "Menu"},
    "ps": {"lb": "L1", "rb": "R1", "lt": "L2", "rt": "R2", "view": "Create", "menu": "Options"},
}


def font(size):
    return ImageFont.truetype(FONT, size)


def centered_text(draw, text, size, color, box):
    face = font(size)
    left, top, right, bottom = draw.textbbox((0, 0), text, font=face)
    x = box[0] + (box[2] - box[0] - (right - left)) / 2 - left
    y = box[1] + (box[3] - box[1] - (bottom - top)) / 2 - top
    draw.text((x, y), text, font=face, fill=color)


def ring(draw):
    pad = 6 * SCALE
    draw.ellipse((pad, pad, BIG - pad, BIG - pad), fill=FILL, outline=GOLD, width=4 * SCALE)


def face_button(draw, kind, name):
    ring(draw)
    if kind == "xbox":
        centered_text(draw, name.upper(), 30 * SCALE, XBOX[name], (0, 0, BIG, BIG))
        return
    color = PLAYSTATION[name]
    c = BIG / 2
    r = 13 * SCALE
    w = 5 * SCALE
    if name == "a":
        draw.line((c - r, c - r, c + r, c + r), fill=color, width=w)
        draw.line((c + r, c - r, c - r, c + r), fill=color, width=w)
    elif name == "b":
        draw.ellipse((c - r, c - r, c + r, c + r), outline=color, width=w)
    elif name == "x":
        draw.rectangle((c - r + SCALE, c - r + SCALE, c + r - SCALE, c + r - SCALE), outline=color, width=w)
    else:
        draw.polygon([(c, c - r - 2 * SCALE), (c + r + 2 * SCALE, c + r), (c - r - 2 * SCALE, c + r)], outline=color, width=w)


def pill(draw, text):
    box = (2 * SCALE, 15 * SCALE, BIG - 2 * SCALE, BIG - 15 * SCALE)
    draw.rounded_rectangle(box, radius=10 * SCALE, fill=FILL, outline=GOLD, width=4 * SCALE)
    size = 22 if len(text) <= 2 else (15 if len(text) <= 4 else 12)
    centered_text(draw, text, size * SCALE, CREAM, box)


def stick(draw, label):
    ring(draw)
    c = BIG / 2
    r = 17 * SCALE
    draw.ellipse((c - r, c - r, c + r, c + r), fill=(44, 40, 32, 255), outline=(138, 122, 80, 255), width=2 * SCALE)
    centered_text(draw, label, (18 if len(label) == 1 else 15) * SCALE, CREAM, (0, 0, BIG, BIG))


def dpad(draw, lit):
    ring(draw)
    c = BIG / 2
    arm = 18 * SCALE
    half = 7 * SCALE
    parts = {
        "up": (c - half, c - arm - half, c + half, c - half),
        "down": (c - half, c + half, c + half, c + arm + half),
        "left": (c - arm - half, c - half, c - half, c + half),
        "right": (c + half, c - half, c + arm + half, c + half),
    }
    for direction, box in parts.items():
        draw.rectangle(box, fill=LIT if direction in lit else DIM)
    draw.rectangle((c - half, c - half, c + half, c + half), fill=DIM)


def home(draw, kind):
    ring(draw)
    centered_text(draw, "PS" if kind == "ps" else "X", (18 if kind == "ps" else 26) * SCALE, CREAM, (0, 0, BIG, BIG))


def draw_glyph(kind, name):
    image = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    if name in ("a", "b", "x", "y"):
        face_button(draw, kind, name)
    elif kind in PILLS and name in PILLS[kind]:
        pill(draw, PILLS[kind][name])
    elif name in ("ls", "rs"):
        stick(draw, "L" if name == "ls" else "R")
    elif name in ("l3", "r3"):
        stick(draw, name.upper())
    elif name == "home":
        home(draw, kind)
    else:
        lit = {
            "dpad": ("up", "down", "left", "right"),
            "updown": ("up", "down"),
            "leftright": ("left", "right"),
        }.get(name, (name,))
        dpad(draw, lit)
    return image.resize((CELL, CELL), Image.LANCZOS)


def write_tga(image, path):
    width, height = image.size
    header = bytearray(18)
    header[2] = 2
    header[12:14] = struct.pack("<H", width)
    header[14:16] = struct.pack("<H", height)
    header[16] = 32
    header[17] = 8
    pixels = image.load()
    body = bytearray()
    for y in range(height - 1, -1, -1):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            body += bytes((b, g, r, a))
    with open(path, "wb") as f:
        f.write(header + body)


GLOW_OUT = os.path.join(ROOT, "Interface", "AddOns", "HordesOfAzeroth", "Textures", "FocusGlow.tga")
GLOW_SIZE = 128
GLOW_LINE = 32


def draw_focus_glow():
    big = GLOW_SIZE * SCALE
    line = GLOW_LINE * SCALE
    alpha = Image.new("L", (big, big), 0)
    pixels = alpha.load()
    for y in range(big):
        for x in range(big):
            edge = min(x, y, big - 1 - x, big - 1 - y)
            distance = abs(edge - line) / SCALE
            if edge >= line:
                value = 255 * max(0.0, 1.0 - distance / 5.0) ** 1.5
            else:
                value = 255 * max(0.0, 1.0 - distance / 28.0) ** 2.2
            pixels[x, y] = int(min(255, value))
    glow = Image.new("RGBA", (big, big), (255, 255, 255, 0))
    glow.putalpha(alpha)
    return glow.resize((GLOW_SIZE, GLOW_SIZE), Image.LANCZOS)


ART_SOURCE = os.path.join(ROOT, "docs", "ui", "art")
ART_OUT = os.path.join(ROOT, "Interface", "AddOns", "HordesOfAzeroth", "Textures")
ART_SIZE = 512
ART_FILES = {"controller_xbox.png": "ControllerXbox.tga", "controller_ps.png": "ControllerPlayStation.tga"}


def write_controller_art():
    for source, target in ART_FILES.items():
        path = os.path.join(ART_SOURCE, source)
        if not os.path.exists(path):
            continue
        art = Image.open(path).convert("RGBA")
        art = art.crop(art.getbbox())
        height = round(ART_SIZE * art.height / art.width)
        canvas = Image.new("RGBA", (ART_SIZE, ART_SIZE), (0, 0, 0, 0))
        canvas.paste(art.resize((ART_SIZE, height), Image.LANCZOS), (0, 0))
        write_tga(canvas, os.path.join(ART_OUT, target))
        print(target, "art height", height)


SHADE_OUT = os.path.join(ROOT, "Interface", "AddOns", "HordesOfAzeroth", "Textures", "CornerShade.tga")
SHADE_WIDTH = 256
SHADE_HEIGHT = 128
SHADE_STOPS = ((0.0, 0.82), (0.38, 0.62), (0.62, 0.25), (0.8, 0.0))


def shade_alpha(distance):
    for (d0, a0), (d1, a1) in zip(SHADE_STOPS, SHADE_STOPS[1:]):
        if distance <= d1:
            return a0 + (a1 - a0) * (distance - d0) / (d1 - d0)
    return 0.0


def draw_corner_shade():
    alpha = Image.new("L", (SHADE_WIDTH, SHADE_HEIGHT), 0)
    pixels = alpha.load()
    for y in range(SHADE_HEIGHT):
        for x in range(SHADE_WIDTH):
            dx = (SHADE_WIDTH - 1 - x) / (SHADE_WIDTH - 1)
            dy = (SHADE_HEIGHT - 1 - y) / (SHADE_HEIGHT - 1)
            pixels[x, y] = int(255 * shade_alpha((dx * dx + dy * dy) ** 0.5))
    shade = Image.new("RGBA", (SHADE_WIDTH, SHADE_HEIGHT), (0, 0, 0, 0))
    shade.putalpha(alpha)
    return shade


BAND_OUT = os.path.join(ROOT, "Interface", "AddOns", "HordesOfAzeroth", "Textures", "PromptShade.tga")
BAND_WIDTH = 256
BAND_HEIGHT = 64
BAND_ALPHA = 0.72
BAND_EDGE_X = 0.22
BAND_EDGE_Y = 0.42


def smooth_edge(t, edge):
    t = min(t, 1.0 - t) / edge
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def draw_prompt_band():
    alpha = Image.new("L", (BAND_WIDTH, BAND_HEIGHT), 0)
    pixels = alpha.load()
    for y in range(BAND_HEIGHT):
        for x in range(BAND_WIDTH):
            fx = smooth_edge((x + 0.5) / BAND_WIDTH, BAND_EDGE_X)
            fy = smooth_edge((y + 0.5) / BAND_HEIGHT, BAND_EDGE_Y)
            pixels[x, y] = int(255 * BAND_ALPHA * fx * fy)
    band = Image.new("RGBA", (BAND_WIDTH, BAND_HEIGHT), (0, 0, 0, 0))
    band.putalpha(alpha)
    return band


ARC_OUT = os.path.join(ROOT, "Interface", "AddOns", "HordesOfAzeroth", "Textures", "DashArc.tga")
ARC_FRAME = 64
ARC_COLUMNS = 8
ARC_ROWS = 4
ARC_HALF_SIDE = 27.0
ARC_WIDTH = 4.0
ARC_CORNER = 4.0
ARC_COLOR = (255, 209, 0)


def square_distance(dx, dy):
    import math
    inner = ARC_HALF_SIDE - ARC_CORNER
    qx, qy = abs(dx) - inner, abs(dy) - inner
    edge = math.hypot(max(qx, 0.0), max(qy, 0.0)) + min(max(qx, qy), 0.0) - ARC_CORNER
    return edge + ARC_HALF_SIDE


def perimeter_position(dx, dy):
    side = ARC_HALF_SIDE
    if abs(dy) >= abs(dx):
        if dy < 0:
            return dx if dx >= 0 else 8 * side + dx
        return 4 * side - dx
    if dx > 0:
        return 2 * side + dy
    return 6 * side - dy


def draw_arc_frame(fraction):
    big = ARC_FRAME * SCALE
    centre = big / 2
    alpha = Image.new("L", (big, big), 0)
    pixels = alpha.load()
    sweep = fraction * 8 * ARC_HALF_SIDE
    for y in range(big):
        for x in range(big):
            dx, dy = (x + 0.5 - centre) / SCALE, (y + 0.5 - centre) / SCALE
            distance = square_distance(dx, dy)
            band = max(0.0, 1.0 - max(0.0, abs(distance - ARC_HALF_SIDE) - ARC_WIDTH / 2) / 1.0)
            if band <= 0:
                continue
            if perimeter_position(dx, dy) <= sweep:
                pixels[x, y] = int(255 * band)
    frame = Image.new("RGBA", (big, big), ARC_COLOR + (0,))
    frame.putalpha(alpha)
    return frame.resize((ARC_FRAME, ARC_FRAME), Image.LANCZOS)


def draw_dash_arcs():
    count = ARC_COLUMNS * ARC_ROWS
    sheet = Image.new("RGBA", (ARC_FRAME * ARC_COLUMNS, ARC_FRAME * ARC_ROWS), ARC_COLOR + (0,))
    for index in range(count):
        frame = draw_arc_frame((index + 1) / count)
        sheet.paste(frame, ((index % ARC_COLUMNS) * ARC_FRAME, (index // ARC_COLUMNS) * ARC_FRAME))
    return sheet


KEYCAP_OUT = os.path.join(ROOT, "Interface", "AddOns", "HordesOfAzeroth", "Textures", "KeyCap.tga")
KEYCAP_WIDTH = 64
KEYCAP_HEIGHT = 32


def draw_keycap():
    big = Image.new("RGBA", (KEYCAP_WIDTH * SCALE, KEYCAP_HEIGHT * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(big)
    pad = 2 * SCALE
    draw.rounded_rectangle((pad, pad, KEYCAP_WIDTH * SCALE - pad, KEYCAP_HEIGHT * SCALE - pad), radius=8 * SCALE,
                           fill=FILL, outline=GOLD, width=3 * SCALE)
    return big.resize((KEYCAP_WIDTH, KEYCAP_HEIGHT), Image.LANCZOS)


def main():
    atlas = Image.new("RGBA", (CELL * COLUMNS, CELL * ROWS), (0, 0, 0, 0))
    for row, entries in enumerate(LAYOUT):
        for column, (kind, name) in enumerate(entries):
            atlas.paste(draw_glyph(kind, name), (column * CELL, row * CELL))
    write_tga(atlas, OUT)
    write_tga(draw_focus_glow(), GLOW_OUT)
    write_controller_art()
    write_tga(draw_corner_shade(), SHADE_OUT)
    write_tga(draw_prompt_band(), BAND_OUT)
    write_tga(draw_dash_arcs(), ARC_OUT)
    write_tga(draw_keycap(), KEYCAP_OUT)
    if len(sys.argv) > 1:
        atlas.save(sys.argv[1])
    print("wrote", OUT, "and", GLOW_OUT)


if __name__ == "__main__":
    main()
