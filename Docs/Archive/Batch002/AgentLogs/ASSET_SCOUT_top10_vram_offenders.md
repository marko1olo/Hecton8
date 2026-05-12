# ASSET_SCOUT Top 10 VRAM Offenders

Date: 2026-05-12
Basis: static texture dimensions plus import `.meta` settings. `EstimatedRGBA32MB` shows RGBA32 + mip cost. `Savings` shows RGBA32 to BC7/BC5 8bpp equivalent.

| Rank | Asset | Size | Format | RGBA32 MB | BC7/BC5 MB | Savings MB | Required Fake / Fix |
|---:|---|---:|---|---:|---:|---:|---|
| 1 | `Assets/ScifiFacility/Textures/DetailSheet_normal.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | BC5 normal, downscale to 2048/1024, use detail-normal atlas for closeups. |
| 2 | `Assets/ScifiFacility/Textures/DetailSheet_mask.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | BC7/mask channel-pack, split hero trim from bulk atlas. |
| 3 | `Assets/ScifiFacility/Textures/plane_2x2_DefaultMaterial_Normal.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | BC5 normal, replace distant panels with baked decal/impostor. |
| 4 | `Assets/ScifiFacility/Textures/Transparent_basecolor.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | BC7, move transparent surface to opaque+dither where possible. |
| 5 | `Assets/ScifiFacility/Textures/BrushedMetal_dirt_roughness.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | Channel-pack roughness/dirt; 1024 tiled detail on MX350. |
| 6 | `Assets/ScifiFacility/Textures/Base_02_dirt_roughness.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | BC7/channel-pack; shared grime atlas. |
| 7 | `Assets/ScifiFacility/Textures/Transparent_normal.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | BC5, reduce transparent normal detail to tileable 1024. |
| 8 | `Assets/ScifiFacility/Textures/Base_normal.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | BC5, shared panel normal atlas. |
| 9 | `Assets/ScifiFacility/Textures/Base_dirt_roughness.png` | 4096x4096 | DXT5 | 85.333 | 21.333 | 64.000 | BC7/channel-pack; material variation via vertex color. |
| 10 | `Assets/_Project/Art/Models/Rocks/Rock 7/Materials/2.jpg` | 4000x4000 | BC7 | 81.380 | 20.380 | 61.000 | Keep BC7 but downscale to 2048 max unless hero rock; distant impostor. |

Total top-10 RGBA32-to-BC7/BC5 theoretical savings: `637.000MB`.

RGBA32 to BC7/BC5 calculation:

```text
RGBA32 bytes = width * height * 4 * mipFactor
BC7/BC5 bytes = width * height * 1 * mipFactor
4096^2 RGBA32 with mips = 4096 * 4096 * 4 * 1.333333 / 1048576 = 85.333MB
4096^2 BC7/BC5 with mips = 4096 * 4096 * 1 * 1.333333 / 1048576 = 21.333MB
Savings per 4K texture = 64.000MB
```

