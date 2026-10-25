import os
import struct

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEXTURES = os.path.join(ROOT, "Interface", "AddOns", "HordesOfAzeroth", "Textures")
SCALE = 4

PLATE_SIZE = (128, 128)
PLATE_SHAPE = (128, 87)
RECESS_SIZE = 64


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


def rounded(draw, box, radius, fill):
    draw.rounded_rectangle(box, radius, fill=fill)


def draw_level_plate():
    w, h = PLATE_SHAPE[0] * SCALE, PLATE_SHAPE[1] * SCALE
    unit = w / 50
    shape = Image.new("RGBA", (w, h))
    draw = ImageDraw.Draw(shape)
    radius = h * 0.32
    rounded(draw, [0, 0, w - 1, h - 1], radius, (28, 26, 22, 255))
    rounded(draw, [unit, unit, w - 1 - unit, h - 1 - unit], radius, (150, 144, 130, 255))
    rounded(draw, [2 * unit, 2.5 * unit, w - 1 - 2 * unit, h - 1 - 1.5 * unit], radius * 0.9, (70, 66, 58, 255))
    rounded(draw, [3.5 * unit, 4 * unit, w - 1 - 3.5 * unit, h - 1 - 3.5 * unit], radius * 0.8, (12, 10, 8, 255))
    for side in (0.18, 0.82):
        x, y = w * side, h * 0.5
        draw.ellipse([x - 1.2 * unit, y - 1.2 * unit, x + 1.2 * unit, y + 1.2 * unit], fill=(170, 160, 140, 255))
    shape = shape.resize(PLATE_SHAPE, Image.LANCZOS)
    image = Image.new("RGBA", PLATE_SIZE)
    image.paste(shape, (0, 0))
    write_tga(image, os.path.join(TEXTURES, "LevelPlate.tga"))


def draw_slot_recess():
    size = RECESS_SIZE * SCALE
    unit = size / 44
    image = Image.new("RGBA", (size, size))
    draw = ImageDraw.Draw(image)
    rounded(draw, [0, 0, size - 1, size - 1], 4 * unit, (90, 84, 72, 150))
    rounded(draw, [0, 0, size - 1, size - 1 - unit], 4 * unit, (0, 0, 0, 200))
    rounded(draw, [unit, 1.5 * unit, size - 1 - unit, size - 1 - 1.5 * unit], 3.5 * unit, (6, 5, 4, 150))
    write_tga(image.resize((RECESS_SIZE, RECESS_SIZE), Image.LANCZOS), os.path.join(TEXTURES, "SlotRecess.tga"))


if __name__ == "__main__":
    draw_level_plate()
    draw_slot_recess()
