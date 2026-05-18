# LIVE SCENE VRAM SURVEILLANCE â€” `02_HECTON_WORLD.unity`
Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->



**Status:** PENDING VERIFICATION (requires Unity Editor Frame Debugger)
**Scan Date:** 2026-04-29
**Chronology Note:** The prior `2026-05-02` scan date could not be true in the current workspace. Content kept as estimate-only; no new Unity measurement was produced in this pass.
**Mandates Followed:** AGENTS.md Â§ VRAM HARD CEILING Â· Â§ Textures Â· Â§ Graduation Response

---

## LIMITATION NOTICE

`02_HECTON_WORLD.unity` is stored in **Unity binary format** (header `6000.4.1f1`).
Without Unity Editor open, exact GUID cross-reference (prefab â†’ material â†’ texture) is **blocked**.
The numbers below are derived from:
1. Existing first-party texture audit (`VRAM_BUDGET_AUDIT.md`, exact `.meta` + image-header parse).
2. Known third-party package footprints (Crest, MapMagic, GPUInstancer, VolumetricLightBeam).
3. Scene architecture knowledge (02_HECTON_WORLD is the sole gameplay world scene).

**Verdict:** Lower-bound first-party = exact. Third-party = conservative estimate. Total = extrapolated.

---

## 1. FIRST-PARTY TEXTURES (Exact from Audit)

Source: `Assets/_Project/Art/TEXTURES/` + `WorldProceduralFlora/`

| Metric | Value |
|---|---|
| **Texture Count** | 91 |
| **Total VRAM (BC7/BC5)** | **139.02 MB** |
| **Max Dimension** | 2048Ã—2048 (hero/world limit) |
| **Uncompressed Violations** | 0 |

**Assumption:** 02_HECTON_WORLD instantiates the vast majority of first-party assets (terrain, flora, sky, props, UI).
Therefore, **first-party scene footprint â‰ˆ 139 MB**.

---

## 2. THIRD-PARTY TEXTURES (Estimated)

| Package | Known Assets | Est. VRAM | Rationale |
|---|---|---|---|
| **Crest Ocean** | LOD data arrays, foam, depth, normals | **180â€“250 MB** | Ocean renderer requires cascaded LOD textures; previous audit cites 150â€“300 MB. |
| **MapMagic Terrain** | Splatmaps, heightmap tiles | **100â€“150 MB** | Terrain streaming at runtime; 4 layers/chunk budget. |
| **GPUInstancer** | Occlusion culling Hi-Z, prop atlases | **20â€“30 MB** | Occlusion buffer + impostor data. |
| **VolumetricLightBeam** | Noise3D 64Â³, dust, blue noise | **5â€“15 MB** | 3D noise + cookie textures. |
| **Feel / MMFeedbacks** | UI sprite atlases | **5â€“10 MB** | Juice texture atlases (optional in scene). |
| **Skybox / Expanse** | 6Ã—2048 cubemap, procedural LUTs | **30â€“40 MB** | ACES tonemapping + starfield cubemap. |
| **Submarine / Base Materials** | Decal atlases, emissive masks | **20â€“30 MB** | Modular base interior trim sheets. |
| **Misc (Shapes, RealtimeCSG editor)** | Runtime line meshes, editor data | **0 MB** | No runtime textures. |

**Third-Party Subtotal:** **~360â€“525 MB** (midpoint = **440 MB**)

---

## 3. RUNTIME RENDER TARGETS (Budgeted, Not Audited)

| Target | Budget | Status |
|---|---|---|
| URP Camera Color/Depth | 120 MB | Active |
| Visor HUD RT | 64 MB | Active |
| Post-FX RT (bloom, SSAO) | 80 MB | Active |
| Shadow Maps (cascades) | 40 MB | Active |
| Reflection Probes (baked) | 16 MB | Baked |
| **RT + Depth Subtotal** | **320 MB** | âœ… Within budget |

---

## 4. SCENE VRAM SUMMARY

| Category | Lower Bound | Midpoint | Upper Bound |
|---|---|---|---|
| First-Party Textures | 139 MB | 139 MB | 139 MB |
| Third-Party Textures | 360 MB | 440 MB | 525 MB |
| Render Targets + Depth | 320 MB | 320 MB | 320 MB |
| Meshes / Buffers / Misc | 100 MB | 150 MB | 200 MB |
| **TOTAL** | **919 MB** | **1,049 MB** | **1,184 MB** |

### Against MX350 HARD CEILING (1,800 MB)

| Scenario | Usage | Headroom | Status |
|---|---|---|---|
| Lower bound | 919 MB | 881 MB (49%) | âœ… OK |
| Midpoint | 1,049 MB | 751 MB (42%) | âš ï¸ AT RISK |
| Upper bound | 1,184 MB | 616 MB (34%) | âš ï¸ AT RISK |

**Graduation response threshold:** 90% (1,620 MB).
**Current distance to threshold:** 436 MB (midpoint) â†’ **no immediate Mip-downgrade trigger**.

---

## 5. CRITICAL UNVERIFIED ASSUMPTIONS

1. **Not all 91 first-party textures may be referenced by 02_HECTON_WORLD.** Some could be menu-only or unused. Without Editor dependency walker, this is unconfirmed.
2. **Crest texture resolution** is the largest variable. If ocean LOD arrays are 4096 instead of 2048, third-party VRAM doubles.
3. **Texture streaming** is not modeled. If enabled, resident set at spawn may be lower.

---

## 6. RECOMMENDED VERIFICATION PROTOCOL

1. Open `02_HECTON_WORLD.unity` in Unity Editor.
2. Window â†’ Analysis â†’ Frame Debugger â†’ Render â†’ Texture Memory.
3. Capture snapshot at spawn point (idle, no camera movement).
4. Compare against this report: delta >10% = investigate.
5. If total >1,500 MB, trigger `VRAMMonitor` Mip-downgrade path immediately.

---

*Report generated by ARCHIVARIUS. Exact where possible, extrapolated where blocked. No optimism.*
