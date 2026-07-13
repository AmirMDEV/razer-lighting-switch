from pathlib import Path
from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets" / "keyboard-source.png"
OUT = ROOT / "assets"


def remove_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, _ = pixels[x, y]
            distance = max(abs(r - 0), abs(g - 255), abs(b - 0))
            alpha = max(0, min(255, int((distance - 12) * 255 / 100)))
            if g > r * 1.35 and g > b * 1.35:
                alpha = min(alpha, max(r, b) * 3)
            pixels[x, y] = (r, g, b, alpha)
    return rgba


def make_icon(source: Image.Image, mode: str) -> Image.Image:
    canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    tile_color = (18, 20, 24, 255) if mode == "black" else (246, 247, 249, 255)
    border_color = (82, 87, 96, 255) if mode == "black" else (205, 209, 216, 255)
    tile = Image.new("RGBA", (860, 860), tile_color)
    mask = Image.new("L", tile.size, 0)
    from PIL import ImageDraw
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle((0, 0, 859, 859), radius=170, fill=255)
    border = Image.new("RGBA", tile.size, border_color)
    canvas.paste(border, (82, 82), mask)
    inner = tile.resize((842, 842))
    inner_mask = mask.resize((842, 842))
    canvas.paste(inner, (91, 91), inner_mask)

    keyboard = source.copy()
    bbox = keyboard.getbbox()
    keyboard = keyboard.crop(bbox)
    keyboard.thumbnail((730, 500), Image.Resampling.LANCZOS)
    if mode == "black":
        keyboard = ImageEnhance.Brightness(keyboard).enhance(0.58)
        keyboard = ImageEnhance.Contrast(keyboard).enhance(1.2)
    else:
        rgb = keyboard.convert("RGB")
        rgb = ImageOps.grayscale(rgb).convert("RGB")
        rgb = ImageEnhance.Brightness(rgb).enhance(1.65)
        keyboard = Image.merge("RGBA", (*rgb.split(), keyboard.getchannel("A")))

    x = (1024 - keyboard.width) // 2
    y = (1024 - keyboard.height) // 2
    canvas.alpha_composite(keyboard, (x, y))
    return canvas


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    source = remove_green(Image.open(SOURCE))
    source.save(OUT / "keyboard-transparent.png")
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    for mode in ("black", "white"):
        icon = make_icon(source, mode)
        icon.save(OUT / f"keyboard-{mode}.png")
        icon.save(OUT / f"keyboard-{mode}.ico", sizes=sizes)


if __name__ == "__main__":
    main()
