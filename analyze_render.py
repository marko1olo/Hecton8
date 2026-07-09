from pathlib import Path
from PIL import Image, ImageStat

image_path = Path(r"C:\hades\Hecton8\Docs\Reports\CleanRoom\Naked_Biome_Transition.png")
if not image_path.exists():
    raise FileNotFoundError(image_path)

with Image.open(image_path) as image:
    rgb = image.convert("RGB")
    width, height = rgb.size
    patch_size = 50
    left = width // 2 - patch_size // 2
    top = height // 2 - patch_size // 2
    patch = rgb.crop((left, top, left + patch_size, top + patch_size))
    stat = ImageStat.Stat(patch)
    mean = tuple(round(v, 4) for v in stat.mean)
    stddev = tuple(round(v, 4) for v in stat.stddev)
    luminance_mean = round(sum(stat.mean) / 3.0, 4)
    stddev_scalar = round(sum(stat.stddev) / 3.0, 4)

print(f"IMAGE={image_path}")
print(f"SIZE={width}x{height}")
print(f"PATCH_CENTER=({width // 2},{height // 2}) PATCH_SIZE={patch_size}x{patch_size}")
print(f"AVG_RGB={mean}")
print(f"STDDEV_RGB={stddev}")
print(f"AVG_LUMA_APPROX={luminance_mean}")
print(f"STDDEV_SCALAR={stddev_scalar}")
