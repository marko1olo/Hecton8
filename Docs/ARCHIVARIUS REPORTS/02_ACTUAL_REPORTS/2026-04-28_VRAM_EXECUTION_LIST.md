# VRAM_EXECUTION_LIST.md — Top 20 VRAM Offenders
Date: 2026-04-28
Status: REFERENCE


## Current-State Addendum (2026-04-29)

This file is a dated static estimate list, not a live measured VRAM truth page.

Its most dangerous stale implication is deletion confidence:

- recommendation to delete or move `Sandbox/` textures is no longer supported as a current fact
- later filesystem recheck downgraded those candidates to `NOT PROVEN DEAD`

Use this file only as a hypothesis list for follow-up profiling/import-setting review.
Do not use it as direct deletion authority.

Preferred current cross-checks:

- `2026-04-28_DEAD_ASSET_SWEEP.md`
- `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md`
- current Codex audit bundles under `Docs/2026-04-29_*`
**Status:** ⚠️ 73% BUDGET CONSUMED  
**Scan Date:** 2026-04-28  
**Budget:** 900 MB (Texture) / 1800 MB (Total VRAM ceiling for MX350)

---

## Methodology
- Estimate: `(width × height × 4 bytes) / CompressionRatio`
- BC7 assumed for opaque albedo/normal/roughness (4 bpp)
- BC5 assumed for normal maps (RG, 2 channels → ~4 bpp effective)
- Non-POT textures flagged for deletion or resize

## Top Offenders by Category

| Rank | Category | Est. Size | % of Budget | Action |
|------|----------|-----------|-------------|--------|
| 1 | Flora/World (atlases + tiles) | ~300 MB | 33% | Audit import settings — ensure BC7 |
| 2 | Coral/Reef (multiple 2K sets) | ~150 MB | 17% | Atlas merge candidates |
| 3 | Modular/Base (construction mats) | ~100 MB | 11% | Trim sheets OK |
| 4 | Terrain/MapMagic splats | ~80 MB | 9% | 4 layers/chunk max |
| 5 | Rocks (2K PBR sets × N) | ~60 MB | 7% | `Rock 4` folder has dupes |
| 6 | Skyboxes / Panoramas | ~40 MB | 4% | OK if compressed |
| 7 | Hero props (scanner, suit) | ~30 MB | 3% | Justified |
| — | **TOTAL ESTIMATED** | **~660 MB** | **73%** | — |

## Critical Findings

### Non-POT / Oversized
| File | Size | Issue |
|------|------|-------|
| `Rock 4 - УНИВЕРСАЛЬНЫЙ ВЫБОР/*.jpg` | 2K | Folder name non-ASCII; possible duplicate sets |
| `Sandbox/Coral_Albedo.png` | Unknown | In `Sandbox/` — verify if used in production mats |
| `Skyboxes/panorama_den.png` | Large | Non-POT? Verify compression |

### Recommendations
1. **Immediate:** Delete or move `Sandbox/` textures if unused.
2. **High:** Merge Coral/Kelp albedo+normal into 2 atlases (save ~40%).
3. **Medium:** Enable `Crunch Compression` on all BC7 world textures (save ~30-50%).
4. **Monitor:** If total > 90% (810 MB), trigger automatic Mip-downgrade policy.

## Verdict
- **Current:** 660 / 900 MB = 73% ⚠️
- **Threshold:** 90% (810 MB) → Mip-downgrade mandatory
- **Headroom:** ~240 MB before critical path
