from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, PngImagePlugin


SIZE = 1024
DATE = "20260605"
STATUS = "SOURCE_ONLY_NOT_IMPORTED / PENDING_VERIFICATION"


def project_root() -> Path:
    return Path(__file__).resolve().parents[4]


def normalize(values: np.ndarray, low: float = 1.0, high: float = 99.0) -> np.ndarray:
    lo = np.percentile(values, low)
    hi = np.percentile(values, high)
    if hi <= lo + 1e-6:
        return np.zeros_like(values, dtype=np.float32)
    return np.clip((values - lo) / (hi - lo), 0.0, 1.0).astype(np.float32)


def smoothstep(edge0: float, edge1: float, values: np.ndarray) -> np.ndarray:
    x = np.clip((values - edge0) / max(edge1 - edge0, 1e-6), 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def load_gray(root: Path, relative_path: str) -> np.ndarray:
    image = Image.open(root / relative_path).convert("L")
    image = image.resize((SIZE, SIZE), Image.Resampling.BICUBIC)
    return normalize(np.asarray(image, dtype=np.float32) / 255.0)


def load_rgb(root: Path, relative_path: str) -> np.ndarray:
    image = Image.open(root / relative_path).convert("RGB")
    image = image.resize((SIZE, SIZE), Image.Resampling.BICUBIC)
    return np.asarray(image, dtype=np.float32) / 255.0


def periodic_wave_noise(seed: int, wave_count: int, max_frequency: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    axis = np.linspace(0.0, np.pi * 2.0, SIZE, endpoint=False, dtype=np.float32)
    x, y = np.meshgrid(axis, axis)
    result = np.zeros((SIZE, SIZE), dtype=np.float32)
    amplitude_sum = 0.0

    for _ in range(wave_count):
        kx = int(rng.integers(1, max_frequency + 1))
        ky = int(rng.integers(1, max_frequency + 1))
        phase = float(rng.uniform(0.0, np.pi * 2.0))
        amplitude = float(rng.uniform(0.35, 1.0)) / max(kx + ky, 1)
        result += np.sin((kx * x) + (ky * y) + phase).astype(np.float32) * amplitude
        amplitude_sum += amplitude

    if amplitude_sum > 1e-6:
        result /= amplitude_sum
    return normalize(result, 0.5, 99.5)


def periodic_gaussian(values: np.ndarray, sigma_pixels: float) -> np.ndarray:
    fy = np.fft.fftfreq(values.shape[0])[:, None]
    fx = np.fft.rfftfreq(values.shape[1])[None, :]
    kernel = np.exp(-0.5 * ((2.0 * np.pi * sigma_pixels) ** 2) * ((fx * fx) + (fy * fy)))
    blurred = np.fft.irfft2(np.fft.rfft2(values) * kernel, s=values.shape)
    return normalize(blurred.astype(np.float32))


def gradient_magnitude(values: np.ndarray) -> np.ndarray:
    dx = (np.roll(values, -1, axis=1) - np.roll(values, 1, axis=1)) * 0.5
    dy = (np.roll(values, -1, axis=0) - np.roll(values, 1, axis=0)) * 0.5
    return normalize(np.sqrt((dx * dx) + (dy * dy)))


def make_rgba(rgb: np.ndarray, alpha: np.ndarray) -> Image.Image:
    rgba = np.dstack((rgb, alpha))
    return Image.fromarray(np.clip(rgba * 255.0, 0.0, 255.0).astype(np.uint8), "RGBA")


def make_rgb(rgb: np.ndarray) -> Image.Image:
    return Image.fromarray(np.clip(rgb * 255.0, 0.0, 255.0).astype(np.uint8), "RGB")


def make_gray(values: np.ndarray) -> Image.Image:
    return Image.fromarray(np.clip(values * 255.0, 0.0, 255.0).astype(np.uint8), "L")


def save_png(image: Image.Image, output_path: Path, note: str) -> None:
    meta = PngImagePlugin.PngInfo()
    meta.add_text("H8_Status", STATUS)
    meta.add_text("H8_Note", note)
    image.save(output_path, pnginfo=meta)


def labeled_tile(image: Image.Image, label: str, size: int = 256) -> Image.Image:
    tile = Image.new("RGB", (size, size + 34), (18, 22, 24))
    thumb = image.convert("RGB").resize((size, size), Image.Resampling.LANCZOS)
    tile.paste(thumb, (0, 0))
    draw = ImageDraw.Draw(tile)
    font = ImageFont.load_default()
    draw.text((8, size + 8), label, fill=(230, 236, 238), font=font)
    return tile


def contact_sheet(items: list[tuple[str, Image.Image]], columns: int, title: str) -> Image.Image:
    tile_width = 256
    tile_height = 290
    rows = int(np.ceil(len(items) / columns))
    sheet = Image.new("RGB", (columns * tile_width, 48 + rows * tile_height), (10, 13, 16))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    draw.text((10, 12), title, fill=(235, 242, 244), font=font)
    draw.text((10, 28), STATUS, fill=(238, 188, 72), font=font)
    for index, (label, image) in enumerate(items):
        x = (index % columns) * tile_width
        y = 48 + (index // columns) * tile_height
        sheet.paste(labeled_tile(image, label, tile_width), (x, y))
    return sheet


def main() -> None:
    root = project_root()
    out_dir = Path(__file__).resolve().parent

    mineral_a = load_gray(root, "Assets/_Project/Art/TEXTURES/Detali/mineral seep mask - looks seamless.png")
    mineral_b = load_gray(root, "Assets/_Project/Art/TEXTURES/Detali/Mineral Seep Mask - second try.png")
    plume = load_gray(root, "Assets/_Project/Art/TEXTURES/Detali/Soft Plume Noise - second try.png")
    plume_dark = load_gray(root, "Assets/_Project/Art/TEXTURES/Detali/soft_plume_noise_-_kakoy_to_seryy_nu_norm.png")
    droplets = load_gray(root, "Assets/_Project/Art/TEXTURES/Detali/visor droplet mask.png")
    runoff_rgb = load_rgb(root, "Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png")

    wave_low = periodic_wave_noise(8017, 36, 9)
    wave_high = periodic_wave_noise(9131, 90, 34)
    cellular = normalize((mineral_a * 0.38) + (mineral_b * 0.34) + (plume * 0.14) + (wave_low * 0.14))
    cellular_soft = periodic_gaussian(cellular, 10.0)
    cellular_edge = gradient_magnitude(cellular_soft)

    runoff_deviation = normalize(np.sqrt(((runoff_rgb[..., 0] - 0.5) ** 2) + ((runoff_rgb[..., 1] - 0.5) ** 2)))
    salt_rim = np.clip(smoothstep(0.20, 0.78, cellular_edge) * (0.45 + 0.55 * mineral_a), 0.0, 1.0)
    wet_edge = smoothstep(0.28, 0.82, periodic_gaussian((mineral_b * 0.45) + (plume * 0.35) + (plume_dark * 0.20), 18.0))
    # The droplet source has a visible visor/lens silhouette. Keep it in the reference sheet only;
    # world shoreline contact cannot inherit that circular artifact.
    bubble_seed = normalize((wave_high * 0.52) + (cellular_edge * 0.22) + (plume_dark * 0.16) + (runoff_deviation * 0.10))
    bubble_breakup = smoothstep(0.52, 0.95, bubble_seed)
    shoreline_residue = smoothstep(0.24, 0.88, normalize((cellular_soft * 0.46) + (plume * 0.26) + (salt_rim * 0.18) + (bubble_breakup * 0.10)))

    # Low-contrast preview color only. This is not a production albedo import.
    blue_gray = np.array([0.58, 0.70, 0.72], dtype=np.float32)
    foam_white = np.array([0.84, 0.92, 0.93], dtype=np.float32)
    salt_tan = np.array([0.86, 0.85, 0.78], dtype=np.float32)
    wet_blue = np.array([0.36, 0.48, 0.50], dtype=np.float32)
    foam_mix = np.clip((shoreline_residue * 0.62) + (bubble_breakup * 0.30) + (salt_rim * 0.28), 0.0, 1.0)
    rgb = (blue_gray[None, None, :] * (1.0 - foam_mix[..., None] * 0.55))
    rgb += foam_white[None, None, :] * (foam_mix[..., None] * 0.40)
    rgb += salt_tan[None, None, :] * (salt_rim[..., None] * 0.14)
    rgb = rgb * (1.0 - wet_edge[..., None] * 0.15) + wet_blue[None, None, :] * (wet_edge[..., None] * 0.10)
    rgb = np.clip(rgb, 0.0, 1.0)
    alpha = np.clip((shoreline_residue * 0.64) + (salt_rim * 0.28) + (bubble_breakup * 0.20), 0.0, 1.0)
    albedo = make_rgba(rgb, alpha)

    height = normalize((salt_rim * 0.34) + (bubble_breakup * 0.24) + (shoreline_residue * 0.22) + (runoff_deviation * 0.12) + (wave_high * 0.08))
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 2.9
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 2.9
    nz = np.ones_like(height)
    length = np.sqrt((dx * dx) + (dy * dy) + (nz * nz))
    normal_rgb = np.dstack(((-dx / length) * 0.5 + 0.5, (-dy / length) * 0.5 + 0.5, (nz / length) * 0.5 + 0.5))
    normal = make_rgb(normal_rgb)

    metallic = np.zeros_like(salt_rim)
    roughness = np.clip(0.48 + (salt_rim * 0.30) + (bubble_breakup * 0.20) - (wet_edge * 0.26), 0.05, 0.94)
    contact_ao = np.clip((wet_edge * 0.42) + (shoreline_residue * 0.30) + (salt_rim * 0.16), 0.0, 1.0)
    wetness_family = np.clip((wet_edge * 0.70) + (shoreline_residue * 0.20) + (bubble_breakup * 0.10), 0.0, 1.0)
    mrao = make_rgba(np.dstack((metallic, roughness, contact_ao)), wetness_family)

    mask_rgba = make_rgba(np.dstack((salt_rim, wet_edge, bubble_breakup)), shoreline_residue)

    albedo_path = out_dir / f"TX_H8_FoamContact_Albedo_SourcePreview_{DATE}.png"
    normal_path = out_dir / f"TX_H8_FoamContact_DetailNormal_SourcePreview_{DATE}.png"
    mrao_path = out_dir / f"TX_H8_FoamContact_MRAO_SourcePreview_{DATE}.png"
    mask_path = out_dir / f"TX_H8_FoamContact_MaskRGBA_SourcePreview_{DATE}.png"

    save_png(albedo, albedo_path, "Albedo preview. Alpha is preview coverage only. Not imported.")
    save_png(normal, normal_path, "Detail normal source preview. Not imported.")
    save_png(mrao, mrao_path, "MRAO source preview: R metallic 0, G roughness, B contact AO, A wetness/family mask.")
    save_png(mask_rgba, mask_path, "RGBA contact mask source preview: R salt rim, G wet edge, B bubble breakup, A shoreline residue.")

    channel_items = [
        ("Albedo preview", albedo),
        ("Detail normal", normal),
        ("MRAO packed", mrao),
        ("RGBA mask packed", mask_rgba),
        ("Mask R salt rim", make_gray(salt_rim)),
        ("Mask G wet edge", make_gray(wet_edge)),
        ("Mask B bubble breakup", make_gray(bubble_breakup)),
        ("Mask A residue", make_gray(shoreline_residue)),
        ("MRAO R metallic", make_gray(metallic)),
        ("MRAO G roughness", make_gray(roughness)),
        ("MRAO B contact AO", make_gray(contact_ao)),
        ("MRAO A wetness", make_gray(wetness_family)),
    ]
    source_items = [
        ("Rejected foam ref", Image.open(root / "Assets/_Project/Art/TEXTURES/foam.png")),
        ("Crest foam ref", Image.open(root / "Assets/Crest/Crest/Textures/foam.png")),
        ("Mineral seep A", make_gray(mineral_a)),
        ("Mineral seep B", make_gray(mineral_b)),
        ("Soft plume", make_gray(plume)),
        ("Droplet support", make_gray(droplets)),
        ("Runoff normal ref", Image.open(root / "Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png")),
        ("Procedural periodic field", make_gray(wave_low)),
    ]
    save_png(
        contact_sheet(channel_items, 4, "H8 Foam Contact Prototype Channel Sheet"),
        out_dir / f"FoamContact_ChannelContactSheet_SOURCE_ONLY_{DATE}.png",
        "Channel contact sheet. Static image QA only.",
    )
    save_png(
        contact_sheet(source_items, 4, "H8 Foam Contact Prototype Source Reference Sheet"),
        out_dir / f"FoamContact_SourceReferenceSheet_SOURCE_ONLY_{DATE}.png",
        "Source reference sheet. Existing sources inspected as reference only.",
    )

    print("Generated FoamContact source-only prototype PNGs.")
    print(f"Output folder: {out_dir}")


if __name__ == "__main__":
    main()
