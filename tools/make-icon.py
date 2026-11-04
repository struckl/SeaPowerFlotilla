"""Draws src/Flotilla/flotilla.ico: three hulls in line ahead on the R.A.T.S. navy, cyan wakes. Needs Pillow."""
from pathlib import Path

from PIL import Image, ImageDraw

NAVY, CYAN, WHITE = (16, 28, 40, 255), (70, 205, 225, 255), (235, 242, 248, 255)
OUT = Path(__file__).resolve().parent.parent / "src" / "Flotilla" / "flotilla.ico"


def ship(draw, x, y, w):
    """A side view: hull, superstructure, mast. (x, y) = bow at the waterline."""
    h = w * 0.16
    draw.polygon([(x, y - h), (x + w, y - h), (x + w * 0.92, y), (x + w * 0.06, y)], fill=WHITE)
    draw.rectangle([x + w * 0.38, y - h * 2.0, x + w * 0.70, y - h], fill=WHITE)
    draw.rectangle([x + w * 0.50, y - h * 3.2, x + w * 0.54, y - h * 2.0], fill=WHITE)
    draw.line([(x + w * 0.95, y + h * 0.35), (x + w * 1.25, y + h * 0.35)], fill=CYAN, width=max(1, int(h * 0.35)))


def draw(size=256):
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(image)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=size * 0.18, fill=NAVY)
    for i, (x, y, w) in enumerate([(0.10, 0.36, 0.52), (0.26, 0.60, 0.52), (0.42, 0.84, 0.46)]):
        ship(d, x * size, y * size, w * size)
    return image


if __name__ == "__main__":
    big = draw(256)
    big.save(OUT, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print(OUT)
