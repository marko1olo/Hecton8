# 1909 Static Visual Debt Audit Proof Matrix

ID: 1909
Role: STATIC_VISUAL_DEBT_AUDIT_PROOF_MATRIX
Evidence class: STATIC_DOC / STATIC_TEXT_VERIFIED / STATIC_PATH_VERIFIED / STATIC_IMAGE_SOURCE_VERIFIED
Unity, build, import, material assignment, scene edits, profiler, PlayMode, player capture: NOT RUN

## Executive Debt Summary

This pass found static evidence for real visual debt and larger proof gaps. It did not produce current Unity proof and did not edit assets.

Highest risk debt:

1. Surface/waterline/coastline remains a first-hour blocker. Old screenshot evidence shows black/muddy surface mismatch, hard white foam/ribbon lines, flat/neon water attempts, and grey procedural coast. Static reports also show inactive foam scene objects and empty foam ribbon texture slots.
2. Aegir/sky/ocean is candidate-rich but proof-poor. Aegir materials/textures exist, but old captures still show scale/integration problems. Moon texture/phase roles remain unresolved. Photic-shallow water clarity is marked missing in the Batch18 matrix.
3. Flora/coral/geology have broad source/package presence but missing proof. Batch18 records 338 `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`, 338 `MISSING_MANIFEST`, 338 `MISSING_NAMED_PROOF`, and 249 `SOURCE_ONLY_PACKAGE` issues. The 1901 matrix has 46 rows, all `PENDING UNITY`.
4. Product-face material debt is hard-blocked, not cosmetic. Batch18 1893 scanned 42 prefabs and 61 material assignments: 55 rows blocked, including 17 package/default `Lit.mat`, 23 tool placeholders, 8 resource flat-color shells, and 6 player blockout rows.
5. PBR channel contracts are unsafe to guess. Batch18 1888 has 12 `BLOCKED_CHANNEL_CONTRACT_REQUIRED` rows out of 17. ARM/ORM/MRAO/Packed naming is insufficient without the exact shader contract.
6. Current Batch19 optional source packets do not close source debt. 1905 reports 24 prompt rows but zero image candidates. 1906 wet-basalt static QA exists but seam diffs are high and maps are QA-only. 1907 is prompt-only. 1908 has no prompt files at this check.

Machine-readable outputs:

- `Docs/Reports/Batch19/1909_STATIC_VISUAL_DEBT_AUDIT_PROOF_MATRIX.csv`
- `Docs/Reports/Batch19/1909_NEXT_OWNER_QUEUE.csv`

## Evidence Classes Used

- `STATIC_TEXT_VERIFIED`: report, CSV, markdown, or metrics text inspected.
- `STATIC_PATH_VERIFIED`: path existence or absence inspected.
- `STATIC_IMAGE_SOURCE_VERIFIED`: local source image, QA preview, or old screenshot inspected outside Unity.
- `STATIC_REJECTED`: static evidence shows placeholder, primitive, empty slot, weak visual result, or forbidden route.
- `PENDING_SOURCE`: no usable source package or candidate exists yet.
- `PENDING_CHANNEL_QA`: source candidate exists but channels, seams, PBR derivation, or import policy are not proven.
- `PENDING_UNITY_OWNER`: import, binding, active scene object, screenshot, render pass, profiler, or player capture is required.
- `BLOCKED_BY_CONTRACT`: required shader/material/channel/source contract is absent or ambiguous.

## Highest-Risk Rows

| Rank | Domain | Item | Static evidence | Status |
|---:|---|---|---|---|
| 1 | SURFACE_SHALLOW | Old surface mismatch and bad foam captures | `Docs/Screenshots/1428_current_world_surface_mismatch.png`; `Docs/Screenshots/1428_bad_foam_disabled_retry_game.png` | `STATIC_REJECTED` |
| 2 | TERRAIN_COASTLINE | Inactive foam objects and empty foam ribbon slots | `Docs/Reports/Batch18/1821_SHORELINE_WATERLINE_OFFLINE_BAKE_SPEC.md` | `PENDING_UNITY_OWNER` / `STATIC_REJECTED` |
| 3 | PRODUCT_FACE | Package/default Lit and placeholders | `Docs/Reports/Batch18/1893_PRODUCT_FACE_ACTUAL_MATERIAL_ASSIGNMENT_MATRIX.csv` | `STATIC_REJECTED` |
| 4 | PBR_CHANNELS | 12 blocked ProductFace channel contracts | `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST.csv` | `BLOCKED_BY_CONTRACT` |
| 5 | FLORA_CORAL_KELP | 338 shallow visual proof pending warnings | `Docs/Reports/Batch18/1858_GENERATED_FLORA_GEOLOGY_MANIFEST_PROOF_PACKET.md` | `PENDING_UNITY_OWNER` |
| 6 | SKY_OCEAN_AEGIR_MOON | Moon texture roles unresolved, photic clarity missing | `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_MATRIX.csv` | `PENDING_SOURCE` / `BLOCKED_BY_CONTRACT` |
| 7 | TERRAIN_COASTLINE | Gemini wet basalt candidate is QA-only with high seam diffs | `Docs/GeneratedAssets/Gemini/QA/1906/TX_H8_WetBasaltShoreline_1428_static_channel_QA_metrics.txt` | `PENDING_CHANNEL_QA` |
| 8 | PROOF_ARTIFACTS | GeneratedAssets proof folders missing | `Docs/Screenshots/GeneratedAssets`; `Docs/Reports/GeneratedAssets` | `PENDING_SOURCE` |

## Not Proven

- No current Unity scene state.
- No active material binding.
- No imported texture state.
- No current GameView/SceneView screenshot packet from this task.
- No PlayMode behavior.
- No Frame Debugger or RenderGraph proof.
- No Unity Profiler, GC, memory, or VRAM proof.
- No player capture.
- No acceptance of Gemini sources as production textures.

## Controller Decisions Needed

1. Assign a Unity owner for shoreline/waterline activation, material binding, and current screenshots after source/PBR blockers are addressed.
2. Assign a PBR QA owner to lock wet basalt, foam, ProductFace, and moon/shallow-water channel contracts before relinks.
3. Assign source operators for ProductFace tools/resources/player materials, moon texture roles, photic-shallow water clarity, and first-wave flora/geology manifests.
4. Assign a profiler owner only after a Unity visual owner changes water, foam, material, instancing, scatter, VFX, or render passes.
5. Keep Batch19 sibling outputs optional. Consume 1905/1906/1907 if useful; do not block on them.

## False Closure Rejections

- Static manifest equals visual proof: rejected.
- Gemini source equals Unity asset: rejected.
- Channel name equals shader contract: rejected.
- Old screenshot equals current pass: rejected.
- Compact equals cheap or ugly: rejected.
- Source prep equals import: rejected.
- Material path exists equals active renderer binding: rejected.
- Prefab exists equals production-quality mesh/material package: rejected.
- Text audit zero findings equals Unity validator pass: rejected.
- Storm/noir/depth/fog hides weak surface art: rejected.

## Quality Consequences

Compact: preserve silhouette, material identity, water color, sky/Aegir/moon readability, route cue, and product-face close-read. No flat foam, black surface, default material, or placeholder shell.

Middle: add richer masks, wet/dry gradients, coral/kelp density, terrain breakup, and product-face wear without changing route truth.

High: add stronger waterline/foam breakup, wet basalt strata, Aegir atmosphere, moon detail, near-field material response, and longer visual residency with proof.

Ultra: add visual overkill only: denser foam lace, reflections, cloud depth, source/detail richness, flora/geology near-field detail, and product-face micro wear. No gameplay truth, collider identity, save identity, DTO layout, route ownership, or material channel semantics change.

## Final State

STATIC AUDIT PACKET COMPLETE.

Runtime/editor acceptance remains `PENDING UNITY OWNER` or `PENDING PROFILER OWNER` where the matrix says so. No Assets path was written.
