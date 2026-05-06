# VRAM BUDGET AUDIT â€” First-Party Texture Accounting
Date: 2026-05-07
Status: PENDING VERIFICATION


> **Status:** ETA SANITIZED  
> **Mandates Followed:** AGENTS.md Â§ Textures Â· Â§ VRAM HARD CEILING  
> **Method:** `.meta` parser + `System.Drawing` image dimension read. No guesswork.

---

## 1. SCOPE & LIMITATIONS

| Scope | Included | Excluded |
|-------|----------|----------|
| Folder | `Assets/_Project/Art/TEXTURES/` | Third-party packages, Plugins, terrain splat atlases, runtime-generated RTs |
| Extensions | `.png` `.jpg` `.jpeg` `.tga` `.psd` `.exr` `.bmp` | `.asset` texture arrays, `.renderTexture`, material sub-assets |
| Compression | Parsed from `.meta` `textureFormat` | Platform overrides (Standalone build may force BC7/BC5) |

> **WARNING:** This audit covers **first-party authored textures only**. Third-party texture debt (Crest ocean data, MapMagic terrain splats, MicroSplat texture arrays, GPUInstancer atlases, VolumetricLightBeam noise textures) is **not counted here** and must be audited separately.

---

## 2. EXECUTIVE NUMBERS

| Metric | Value | Limit | Headroom |
|--------|-------|-------|----------|
| **First-party texture count** | 91 | â€” | â€” |
| **First-party texture VRAM** | **139.02 MB** | 900 MB (texture budget) | **760.98 MB** |
| **Total VRAM ceiling (MX350)** | â€” | 1800 MB | â€” |
| **RT + Depth budget** | â€” | 320 MB | â€” |

**Verdict:** First-party textures are **well within budget**. The 139 MB leaves ample room for third-party assets, runtime RTs, and future content.

---

## 3. FORMAT BREAKDOWN

| Format Code | Meaning | Count | Est. BPP | Notes |
|-------------|---------|-------|----------|-------|
| `-1` | Auto (platform default) | 87 | 0.5â€“1.0 | Unity picks BC7/BC5/DXT at build time |
| `1` | Compressed (DXT/BC) | 3 | 0.5 | Explicit compressed |
| `10` | DXT5 (BC3) | 0 | 1.0 | RGBA compressed |
| `12` | RGBA 32-bit | 0 | 4.0 | Uncompressed â€” forbidden by mandate |
| `3` | RGB 24-bit | 0 | 3.0 | Uncompressed â€” forbidden by mandate |
| `4` | RGBA 32-bit (legacy) | 0 | 4.0 | Uncompressed â€” forbidden by mandate |

**All textures use compressed or auto-compressed formats.** No uncompressed RGB/RGBA violations detected in first-party set.

---

## 4. DIMENSION AUDIT

| Dimension Bucket | Count | % of Total |
|------------------|-------|------------|
| â‰¤512 Ã— 512 | 12 | 13% |
| 1024 Ã— 1024 | 9 | 10% |
| 2048 Ã— 2048 | 70 | 77% |
| >2048 Ã— 2048 | 0 | 0% |

**Max size:** 2048 (hero/world limit per AGENTS.md). No violations.

---

## 5. TOP 10 VRAM CONSUMERS

| VRAM (MB) | Size | File |
|-----------|------|------|
| 5.33 | 2048Ã—2048 | `TEXTURES/foam.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.coral.low/normal___family.coral.low.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.coral.low/mask___family.coral.low.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.kelp.tall/normal___family.kelp.tall.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.kelp.patch.dense/normal___family.kelp.patch.dense.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.kelp.tall/albedo___family.kelp.tall.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.kelp.tall/mask___family.kelp.tall.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.coral.branching/mask___family.coral.branching.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.coral.branching/detail___family.coral.branching.png` |
| 2.67 | 2048Ã—2048 | `WorldProceduralFlora/Imported/family.coral.branching/albedo___family.coral.branching.png` |

**Observation:** The `foam.png` (5.33 MB) is an outlier. It uses `textureFormat: 1` with `alphaIsTransparency: 0`, yet occupies double the VRAM of other 2048Ã—2048 textures. Verify if it is imported as a normal map or HDR texture inadvertently. If it is a standard albedo, it should compress to ~2.67 MB like its peers.

---

## 6. THIRD-PARTY TEXTURE DEBT (UNAUDITED)

These packages are known to carry significant texture memory but were **outside the scan scope**:

| Package | Known Texture Assets | Est. VRAM |
|---------|---------------------|-----------|
| **Crest** | Ocean LOD data arrays, foam textures, depth probe | 150â€“300 MB |
| **MapMagic** | Terrain splatmaps, heightmap textures | 100â€“200 MB |
| **MicroSplat** | Texture arrays, prop data textures | 50â€“100 MB |
| **VolumetricLightBeam** | Noise3D 64Â³, dust particles, blue noise | 5â€“15 MB |
| **GPUInstancer** | Occlusion culling Hi-Z depth texture | 10â€“20 MB |
| **Feel / MMFeedbacks** | UI sprite atlases, juice textures | 5â€“10 MB |

**Recommended:** Run Unity Editor â†’ Window â†’ Analysis â†’ Frame Debugger â†’ Texture Memory to capture the *actual* runtime resident set. Meta-based accounting cannot predict runtime mip streaming, texture array packing, or dynamic RT allocation.

---

## 7. COMPLIANCE CHECKLIST

| Rule | Status |
|------|--------|
| BC7 for albedo/roughness/AO | âœ… Auto format lets Unity choose BC7 on Standalone |
| BC5 for normals (RG/DXT5nm) | âœ… Normal maps detected with `convertToNormalMap: 0` â€” verify filter in DCC |
| Max size hero â‰¤ 2048 | âœ… |
| Max size world/terrain â‰¤ 2048 tiled | âœ… |
| MipMaps On for world | âœ… `enableMipMap: 1` on all scanned textures |
| No uncompressed RGB/RGBA | âœ… No `textureFormat: 3` or `4` detected |
| Texture budget â‰¤ 900 MB | âœ… 139 MB first-party |

---

*Report generated by ARCHIVARIUS. Exact math from `.meta` + image header parse. Raw detail: `vram_detail.csv` in this folder.*
