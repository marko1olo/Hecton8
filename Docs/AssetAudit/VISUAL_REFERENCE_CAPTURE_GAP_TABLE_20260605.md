# Visual Reference Capture Gap Table - 2026-06-05

Status: `REJECTED / H8_1475_MISSING / STATIC_IMAGE_QA_ONLY`.
Evidence class: `STATIC_IMAGE_QA + STATIC_DOC + FILE_INVENTORY`.
Runtime proof: absent.
Profiler/GC/memory proof: absent.
Unity run/build/import: not executed by this worker.

## Scope

Mission: compare mandatory reference examples against current diagnostic screenshots and record the capture gaps. This worker did not touch `Assets/`, `ProjectSettings/`, `Packages`, code, scenes, prefabs, materials, or Status/Rationale/LOG files.

First-20 route blocker: current captures do not prove the bright semi-open first exit with readable surface, water, terrain, Aegir, shoreline, underwater route density, and instrument/cockpit product face.

Mandates followed:

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

Authority reads:

- `AGENTS.md`
- `TASTE.md`
- `water.md`
- `rendering.md`
- `ui.md`
- `presentation.md`
- `quality.md`
- `terrain.md`
- `celestial.md`
- `world.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_SYNTHESIS_20260605.md`

## Inventory Checked

Mandatory reference folder:

- `Docs/ОБЯЗАТЕЛЬНЫЕ ПРИМЕРЫ ПО КАРТИНКАМ/photo_1_2026-06-04_11-12-33.jpg`
- `Docs/ОБЯЗАТЕЛЬНЫЕ ПРИМЕРЫ ПО КАРТИНКАМ/photo_2_2026-06-04_11-12-33.jpg`
- `Docs/ОБЯЗАТЕЛЬНЫЕ ПРИМЕРЫ ПО КАРТИНКАМ/photo_3_2026-06-04_11-12-33.jpg`
- `Docs/ОБЯЗАТЕЛЬНЫЕ ПРИМЕРЫ ПО КАРТИНКАМ/photo_4_2026-06-04_11-12-33.jpg`
- `Docs/ОБЯЗАТЕЛЬНЫЕ ПРИМЕРЫ ПО КАРТИНКАМ/ССС.jpg`

Current diagnostic comparison set:

- `Docs/Screenshots/MCP/h8_1913_surface_patch_a.png`
- `Docs/Screenshots/MCP/h8_1912_surface_after_quarantine_b.png`
- `Docs/Screenshots/MCP/h8_1908_surface_runtime_ui_on.png`
- `Docs/Screenshots/MCP/h8_1474_surface_coast_aegir_ui_off.png`
- `Docs/Screenshots/MCP/h8_1474_underwater_20_50m_route.png`
- `Docs/Screenshots/MCP/h8_1474_shoreline_close_1m.png`
- `Docs/Screenshots/MCP/h8_1473_mainrt_underwater_0_5m.png`
- `Docs/Screenshots/MCP/h8_1473_mainrt_crest_foam_shoreline.png`

Proof packet check:

- `Docs/Screenshots/HectonProofPackets/` is missing.
- No `h8_1475*` file exists under `Docs/Screenshots/MCP`.
- Current MCP PNGs are diagnostic only. They are not acceptance artifacts.

## Summary Verdict

Current visuals are rejected.

The mandatory references demand premium water volume, readable shoreline contact, terrain material truth, hero-grade Aegir/sky, dense underwater route composition, and cockpit/HUD integration. Current diagnostic screenshots show a flat green/teal water sheet, weak or absent underwater route density, dark/noisy primitive terrain, no convincing shoreline foam/contact, smeared Aegir texture, and no accepted product-face cockpit proof.

`h8_1475` is missing. Until `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` exists with `manifest.json`, `manifest.sha256`, copied Unity log, and canonical screenshot set, no visual promotion is valid.

## Gap Table

| Requirement | Reference image | Current artifact | Current gap | Owner packet | Required future capture | Forbidden fake | Status |
|---|---|---|---|---|---|---|---|
| Water volume | `photo_2_2026-06-04_11-12-33.jpg` | `h8_1473_mainrt_underwater_0_5m.png`; `h8_1474_underwater_20_50m_route.png` | Reference requires readable water ceiling, refraction, depth falloff, shallow terrain visibility, and non-flat ocean color. Current underwater evidence reads as green/yellow slab water or mislabeled route proof with weak/no volume. | Ocean/Crest owner; Underwater VFX/source owner | `h8_1475` underwater 0-5m and 20-50m route captures inside canonical proof packet. | Blue/green fog sheet, darkness, bloom, full-screen haze, or post-process cover for absent water volume. | `REJECTED / PENDING_H8_1475_PROOF` |
| Shoreline foam/contact | `photo_3_2026-06-04_11-12-33.jpg` | `h8_1473_mainrt_crest_foam_shoreline.png`; `h8_1474_shoreline_close_1m.png` | Reference requires transparent shallows, wet-rock transition, foam/contact breakup, shoreline material scale, and waterline detail. Current shoreline shows water adjacent to dark terrain with no convincing foam/contact proof. | Ocean/Crest owner; Material/texture owner | `h8_1475` shoreline close 1m capture plus Frame Debugger/proof packet for foam/contact/caustic contribution. | Artist textures bound into Crest wave-data slots, fog cover, black terrain edge, or decorative foam that does not touch geometry believably. | `REJECTED / PENDING_H8_1475_PROOF` |
| Terrain material truth | `photo_3_2026-06-04_11-12-33.jpg`; `SSS.jpg / Cyrillic source basename` | `h8_1913_surface_patch_a.png`; `h8_1474_surface_coast_aegir_ui_off.png`; `h8_1473_mainrt_crest_foam_shoreline.png` | Reference requires wet geology, strata/erosion, sediment breakup, scale witnesses, and readable coast silhouettes. Current terrain reads as dark crushed silhouettes, noisy slick slopes, primitive blobs, and toy-like surface experiments. | Material/texture owner; Product-face prefab owner; Terrain/geology owner | `h8_1475` surface/coast captures at gameplay height plus material role proof and visible source mesh/LOD/collider proof where relevant. | Darkness/noir grade, noisy green specular, random rock scatter, primitive meshes, or coral/rocks placed as camouflage over failed terrain. | `REJECTED / PENDING_H8_1475_PROOF` |
| Aegir/sky hero quality | `photo_4_2026-06-04_11-12-33.jpg`; `SSS.jpg / Cyrillic source basename` | `h8_1913_surface_patch_a.png`; `h8_1474_surface_coast_aegir_ui_off.png`; `h8_1908_surface_runtime_ui_on.png` | Reference allows blue/purple Aegir only when cloud bands, atmospheric limb, texture quality, scale, and lighting context are premium. Current Aegir is oversized but smeared, muddy green-black, weak at the limb, and disconnected from water/terrain quality. | Sky/Aegir owner | `h8_1475` surface sky/Aegir long and gameplay-height captures with sky dome/material readback and no duplicate sun/backdrop ownership. | Muddy sine stripes, low-resolution procedural bands, permanent surface gloom, pasted sphere scale without material detail, or bloom/fog hiding the sky. | `REJECTED / PENDING_H8_1475_PROOF` |
| Underwater route density | `photo_1_2026-06-04_11-12-33.jpg` | `h8_1473_mainrt_underwater_0_5m.png`; `h8_1474_underwater_20_50m_route.png` | Reference requires dense route walls, cliffs/shelves, coral/flora, particles, fish/fauna silhouettes, navigable negative space, and instrument context. Current evidence is nearly empty slab water or invalid route proof with no product route density. | Underwater VFX/source owner; World/terrain route owner; Product-face prefab owner | `h8_1475` first-20 photic route captures from gameplay height, compact and high views, with route/decision read statement. | Full-screen marine snow, blue fog, black water, random coral carpets, or beauty shots with no route decision. | `REJECTED / PENDING_H8_1475_PROOF` |
| HUD/cockpit integration | `photo_1_2026-06-04_11-12-33.jpg` | `h8_1908_surface_runtime_ui_on.png`; `h8_1473_surface_coast_aegir_ui_on.png` | Reference shows cockpit frame, physical controls, and readouts integrated into the water route. Current UI-on captures do not prove diegetic HUD/cockpit integration or first-person product-face instrument quality. | UI/HUD owner; Player/cockpit owner; Product-face prefab owner | `h8_1475` cockpit/HUD route capture with oxygen/pressure/power/signal state readable and physical carrier visible. | Flat overlay HUD, fake telemetry, decorative grids, screen-space UI posing as cockpit proof, or UI that does not expose a player decision. | `REJECTED / PENDING_H8_1475_PROOF` |
| Proof packet validity | `photo_1` through `photo_4`; `SSS.jpg / Cyrillic source basename` | `Docs/Screenshots/MCP/*.png`; missing `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` | MCP PNGs are diagnostic only. No canonical h8_1475 proof packet, no manifest, no checksum, no copied Unity log, and no accepted visual chain. | Unity proof owner; Visual QA controller | Create `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` with manifest, checksum, copied Unity log, canonical screenshots, and mapping notes. | Any acceptance claim based on raw MCP screenshots, static docs, stale batch notes, or controller prose without canonical packet. | `REJECTED / H8_1475_MISSING` |

## Scalability Consequences

- Low/Compact: must still show clean ocean color, water volume, shoreline contact, readable terrain silhouette, route cue, and instrument state. Current captures fail.
- Middle: must add wet shoreline breakup, sparse particles, better route density, and coherent Aegir/sky composition. Current captures do not prove this.
- High: should buy richer normals, foam/contact masks, denser route flora/geology, stronger sky/cloud/Aegir detail, and better cockpit material response. No current proof.
- Ultra: may add visual overkill only after compact readability and h8_1475 packet validity pass. It cannot hide failed base art.

## Decision

Product-face visual promotion remains rejected.

Next valid work is owner-specific remediation plus a real `h8_1475` proof packet. Static text, raw MCP PNGs, and controller prose cannot upgrade the evidence class.
