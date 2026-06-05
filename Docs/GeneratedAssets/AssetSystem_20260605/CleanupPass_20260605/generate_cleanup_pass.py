from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont, PngImagePlugin


DATE = "20260605"
STATUS = "SOURCE_ONLY_NOT_IMPORTED / PENDING_VERIFICATION"


def root() -> Path:
    return Path(__file__).resolve().parents[4]


def out_dir() -> Path:
    return Path(__file__).resolve().parent


def save(image: Image.Image, path: Path, note: str) -> None:
    meta = PngImagePlugin.PngInfo()
    meta.add_text("H8_Status", STATUS)
    meta.add_text("H8_Note", note)
    image.save(path, pnginfo=meta)


def normalize(values: np.ndarray, low: float = 2.0, high: float = 98.0) -> np.ndarray:
    lo = np.percentile(values, low)
    hi = np.percentile(values, high)
    if hi <= lo + 1e-6:
        return np.zeros_like(values, dtype=np.float32)
    return np.clip((values - lo) / (hi - lo), 0.0, 1.0).astype(np.float32)


def to_array(image: Image.Image, size: tuple[int, int]) -> np.ndarray:
    return np.asarray(image.convert("RGBA").resize(size, Image.Resampling.LANCZOS), dtype=np.float32) / 255.0


def from_rgba(values: np.ndarray) -> Image.Image:
    return Image.fromarray(np.clip(values * 255.0, 0.0, 255.0).astype(np.uint8), "RGBA")


def from_gray(values: np.ndarray) -> Image.Image:
    return Image.fromarray(np.clip(values * 255.0, 0.0, 255.0).astype(np.uint8), "L")


def smooth_channel(channel: np.ndarray, blur_radius: float, contrast_gamma: float) -> np.ndarray:
    image = from_gray(channel).filter(ImageFilter.GaussianBlur(blur_radius))
    values = np.asarray(image, dtype=np.float32) / 255.0
    values = normalize(values, 4.0, 96.0)
    return np.power(values, contrast_gamma).astype(np.float32)


def contact_sheet(items: list[tuple[str, Image.Image]], title: str, columns: int = 3) -> Image.Image:
    tile_w = 320
    tile_h = 366
    rows = int(np.ceil(len(items) / columns))
    sheet = Image.new("RGB", (columns * tile_w, 62 + rows * tile_h), (12, 15, 18))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    draw.text((12, 12), title, fill=(235, 242, 244), font=font)
    draw.text((12, 31), STATUS, fill=(238, 188, 72), font=font)
    for index, (label, image) in enumerate(items):
        x = (index % columns) * tile_w
        y = 62 + (index // columns) * tile_h
        thumb = image.convert("RGB").resize((tile_w, 320), Image.Resampling.LANCZOS)
        tile = Image.new("RGB", (tile_w, tile_h), (18, 22, 24))
        tile.paste(thumb, (0, 0))
        ImageDraw.Draw(tile).text((8, 330), label, fill=(230, 236, 238), font=font)
        sheet.paste(tile, (x, y))
    return sheet


def cleanup_foam(base: Path, output: Path) -> None:
    foam = base / "FoamContactPrototype_20260605"
    albedo = Image.open(foam / f"TX_H8_FoamContact_Albedo_SourcePreview_{DATE}.png").convert("RGBA")
    normal = Image.open(foam / f"TX_H8_FoamContact_DetailNormal_SourcePreview_{DATE}.png").convert("RGB")
    mask = Image.open(foam / f"TX_H8_FoamContact_MaskRGBA_SourcePreview_{DATE}.png")
    mrao = Image.open(foam / f"TX_H8_FoamContact_MRAO_SourcePreview_{DATE}.png")

    size = (1024, 1024)
    mask_values = to_array(mask, size)
    mrao_values = to_array(mrao, size)

    salt = smooth_channel(mask_values[..., 0], 7.5, 1.35)
    wet = smooth_channel(mask_values[..., 1], 11.0, 1.15)
    bubble = smooth_channel(mask_values[..., 2], 5.0, 1.55)
    residue = smooth_channel(mask_values[..., 3], 13.0, 1.25)

    # Remove large harsh islands by blending in a second broad field, but keep shoreline texture.
    broad = smooth_channel((salt * 0.35) + (wet * 0.25) + (bubble * 0.20) + (residue * 0.20), 18.0, 1.0)
    salt = np.clip((salt * 0.62) + (broad * 0.24), 0.0, 0.86)
    wet = np.clip((wet * 0.72) + (broad * 0.22), 0.0, 0.92)
    bubble = np.clip((bubble * 0.58) + (broad * 0.14), 0.0, 0.82)
    residue = np.clip((residue * 0.66) + (broad * 0.20), 0.0, 0.88)

    clean_mask = np.dstack((salt, wet, bubble, residue))

    m_r = np.zeros_like(salt)
    m_g = np.clip((mrao_values[..., 1] * 0.55) + (0.42 + salt * 0.24), 0.18, 0.92)
    m_b = np.clip((mrao_values[..., 2] * 0.45) + (wet * 0.30) + (residue * 0.28), 0.0, 0.84)
    m_a = np.clip((mrao_values[..., 3] * 0.50) + (wet * 0.36) + (bubble * 0.14), 0.0, 0.90)
    clean_mrao = np.dstack((m_r, m_g, m_b, m_a))

    albedo_clean = ImageEnhance.Contrast(albedo.resize(size, Image.Resampling.LANCZOS)).enhance(0.82)
    albedo_clean = ImageEnhance.Color(albedo_clean).enhance(0.72)
    normal_clean = ImageEnhance.Contrast(normal.resize(size, Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(0.65))).enhance(0.88)

    save(albedo_clean, output / f"TX_H8_FoamContact_CleanedSource_Albedo_{DATE}.png", "Source-only cleaned albedo preview. Not imported.")
    save(normal_clean, output / f"TX_H8_FoamContact_CleanedSource_DetailNormal_{DATE}.png", "Source-only softened detail normal preview. Not imported.")
    save(from_rgba(clean_mrao), output / f"TX_H8_FoamContact_CleanedSource_MRAO_{DATE}.png", "Source-only cleaned packed MRAO preview. Not imported.")
    save(from_rgba(clean_mask), output / f"TX_H8_FoamContact_CleanedSource_MaskRGBA_{DATE}.png", "Source-only cleaned RGBA mask preview. Not imported.")
    save(
        contact_sheet(
            [
                ("Clean albedo", albedo_clean),
                ("Clean normal", normal_clean),
                ("Clean MRAO", from_rgba(clean_mrao)),
                ("Clean RGBA mask", from_rgba(clean_mask)),
                ("R salt rim softened", from_gray(salt)),
                ("G wet edge softened", from_gray(wet)),
                ("B bubble breakup softened", from_gray(bubble)),
                ("A residue softened", from_gray(residue)),
            ],
            "H8 Foam Contact Cleanup Pass",
            4,
        ),
        output / f"FoamContact_CleanupPass_ContactSheet_SOURCE_ONLY_{DATE}.png",
        "Source-only cleanup contact sheet. Not imported.",
    )


def cleanup_aegir(base: Path, output: Path) -> None:
    aegir = base / "AegirCloudPrototype_20260605"
    band = Image.open(aegir / f"TX_H8_AegirBand_Albedo_SOURCE_ONLY_NOT_IMPORTED_PENDING_VERIFICATION_{DATE}.png").convert("RGB")
    storm = Image.open(aegir / f"TX_H8_AegirStorm_MaskRGBA_SOURCE_ONLY_NOT_IMPORTED_PENDING_VERIFICATION_{DATE}.png")
    detail = Image.open(aegir / f"TX_H8_AegirCloud_Detail_SOURCE_ONLY_NOT_IMPORTED_PENDING_VERIFICATION_{DATE}.png").convert("L")

    size = (1024, 512)
    storm_values = to_array(storm, size)
    band_clean = ImageEnhance.Color(band.resize(size, Image.Resampling.LANCZOS)).enhance(0.72)
    band_clean = ImageEnhance.Contrast(band_clean).enhance(0.88)
    detail_clean = ImageEnhance.Contrast(detail.resize(size, Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(0.85))).enhance(0.90)

    cells = smooth_channel(storm_values[..., 0], 4.0, 1.35)
    turbulence = smooth_channel(storm_values[..., 1], 8.0, 1.15)
    limb = smooth_channel(storm_values[..., 2], 10.0, 1.0)
    opacity = smooth_channel(storm_values[..., 3], 12.0, 1.1)
    storm_clean = np.dstack((
        np.clip(cells * 0.78, 0.0, 0.88),
        np.clip(turbulence * 0.70, 0.0, 0.82),
        np.clip(limb * 0.65, 0.0, 0.76),
        np.clip(opacity * 0.72, 0.0, 0.86),
    ))

    save(band_clean, output / f"TX_H8_AegirCloud_CleanedSource_BandAlbedo_{DATE}.png", "Source-only cleaned Aegir band albedo preview. Not imported.")
    save(from_rgba(storm_clean), output / f"TX_H8_AegirCloud_CleanedSource_StormMaskRGBA_{DATE}.png", "Source-only cleaned Aegir storm mask preview. Not imported.")
    save(detail_clean.convert("RGB"), output / f"TX_H8_AegirCloud_CleanedSource_Detail_{DATE}.png", "Source-only cleaned cloud detail preview. Not imported.")
    save(
        contact_sheet(
            [
                ("Clean band albedo", band_clean),
                ("Clean storm RGBA", from_rgba(storm_clean)),
                ("Clean detail", detail_clean.convert("RGB")),
                ("R storm cells", from_gray(storm_clean[..., 0])),
                ("G turbulence", from_gray(storm_clean[..., 1])),
                ("B limb breakup", from_gray(storm_clean[..., 2])),
                ("A opacity/detail", from_gray(storm_clean[..., 3])),
            ],
            "H8 Aegir Cloud Cleanup Pass",
            4,
        ),
        output / f"AegirCloud_CleanupPass_ContactSheet_SOURCE_ONLY_{DATE}.png",
        "Source-only cleanup contact sheet. Not imported.",
    )


def main() -> None:
    base = root() / "Docs/GeneratedAssets/AssetSystem_20260605"
    output = out_dir()
    output.mkdir(parents=True, exist_ok=True)
    cleanup_foam(base, output)
    cleanup_aegir(base, output)


if __name__ == "__main__":
    main()
