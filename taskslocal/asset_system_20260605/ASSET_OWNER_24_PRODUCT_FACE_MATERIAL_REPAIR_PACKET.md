# Asset Owner 24 - Product-Face Material / Texture Repair Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_YAML_SCAN`, `STATIC_IMAGE_QA`, `UNITY_BATCHMODE_LOG`.
Runtime proof: absent.
Visual acceptance: blocked until `h8_1475` proof packet exists.

This packet is a future owner task order. It does not edit `Assets/`, `ProjectSettings/`, `Packages/`, scenes, prefabs, materials, code, Status, Rationale, or LOG files.

## Objective

Repair product-face material and texture routes so visible player, tool, resource, transport, construction, sky, ocean, shoreline, terrain, flora/coral, and route-facing material surfaces stop using placeholder, blockout, package-default, proxy, null, wrong-channel, source-only, rejected, or visually failed material paths.

First-20 route moment improved: bright first exit, ocean skin, shoreline/waterline, photic shallows, held tools, resource pickups, first transport/tool view, Aegir/sky context, and medium-depth route material trust.

Route blocker removed: current product-face source gates failed and current visual references were rejected. This owner must produce route-owned material families plus proof, not static claims.

## Evidence Basis

- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_SYNTHESIS_20260605.md`: Unity batchmode source gates failed. Material/texture gate: `Prefabs=42`, `Materials=43`, `Failures=183`, `Warnings=4`. Prefab quality gate failed for all `42` checked prefabs. Sky/ocean source primitive gate failed with `2` checked prefabs and `2` failures.
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`: current visual state rejected; `h8_1475` proof packet absent; raw MCP screenshots are diagnostic only.
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`: mandatory image-read digest for user-visible water, terrain, sky/Aegir, flora/coral, UI/cockpit, shoreline, and medium-depth reference signals.
- `Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.md`: current VREF-to-owner routing for material owners.
- `Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.md`: `392` material files scanned; `290` have no static texture GUIDs; `314` have empty texture slot tokens; `41` include `WorldProceduralProxy`; `42` include proxy/placeholder tokens; `260` unresolved shader GUIDs by static map.
- `Docs/AssetAudit/TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.md`: active route blocker rows include `45` P0 rows, `44` P1 rows, and `20` P2 rows. Key families: terrain/geology `51`, flora/coral/fauna `48`, sky/Aegir/cloud `8`, water foam rejected support `1`, water/caustic support `1`.
- `Docs/AssetAudit/TEXTURE_AUTHORING_RECIPES_20260605.md`: foam/contact, Aegir/sky, wet basalt/shell sand, and UI oxygen route recipes are source-only until cleanup, PBR role separation, import readback, material binding, screenshots, and proof exist.
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`: static ledgers are routing evidence only. Unity import quality, material binding, Crest state, Addressables residency, visual quality, VRAM safety, and runtime behavior remain unproven.

## Authority Docs

Read before execution:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `rendering.md`
- `shaders.md`
- `water.md`
- `streaming.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Premium_Approximation_Protocol.txt`
- `Docs/ARCHITECTURE/PREMIUM_APPROXIMATION_LEDGER.md`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_SYNTHESIS_20260605.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`
- `Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.md`
- `Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.md`
- `Docs/AssetAudit/TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.md`
- `Docs/AssetAudit/TEXTURE_AUTHORING_RECIPES_20260605.md`

Read `HECTON8_ORCHESTRATOR.md` only if the future owner is explicitly assigned controller/orchestration work. An ordinary material/texture execution owner must not read it.

## Owned Scope

Future owner may inspect:

- Product-face prefabs, materials, textures, importers, and route scenes needed to prove the failed gates.
- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Tools/Held/`
- `Assets/_Project/Prefabs/Items/Tools/`
- `Assets/_Project/Prefabs/Resources/Pickups/`
- `Assets/_Project/Prefabs/Transport/`
- `Assets/_Project/Prefabs/Sky_System.prefab`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- `Assets/_Project/Art/Materials/WorldProceduralFlora/Imported/`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/`
- `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/`
- `Assets/_Project/Art/TEXTURES/`
- `Assets/Crest/Crest/Textures/foam.png` as rejected reference only.

Future owner may edit only through a separate authorized execution run after discovery proves exact targets:

- Texture import settings through Unity importer APIs.
- Material asset bindings through Unity APIs.
- Route-owned `MAT_*` material assets.
- Generated/cleaned source maps only after manifest, cleanup, channel roles, import proof, and visual proof.

This packet does not authorize runtime code, wrappers, packages, project settings, raw scene saves, broad prefab rewrites, or raw YAML mutation.

## No-Go Rules

- No raw YAML `.mat`, `.unity`, `.prefab`, `.asset`, or `.meta` edits.
- No Crest runtime wrappers, runtime material clones, material overrides, or instantiation paths.
- No artist texture bound into Crest `_WD_*` wave-data slots. `_WD_*` slots are data lanes, not visible art slots.
- No fog, darkness, bloom, vignette, exposure crush, storm grade, cropped camera angle, or green haze coverup for weak material art.
- No final binding of watermarked, seamed, baked-light, false-PBR, low-resolution, or source-only generated files without cleanup, PBR role separation, import settings proof, material binding proof, screenshot proof, and memory proof.
- Do not promote `foam.png`, `WorldProceduralProxy`, `WorldRuntime/ProceduralPlaceholders`, package-default `Lit.mat`, blockout materials, proxy materials, null material slots, or empty texture-role materials into visible product-face routes.
- Do not claim Addressables, residency, shader variant, SRP Batcher, SetPass, memory, VRAM, GC, or visual readiness from static scans.
- Do not add per-object material uniqueness to hide missing texture work.
- Do not change gameplay truth, DTO layout, save identity, collision truth, Crest ownership, or material authority based on quality tier.

## Phase-Gated Tasks

### Phase 0 - Process Gate And Exact Target Discovery

1. Confirm process gate before Unity work: CPU below project threshold, no active `dotnet`, `csc`, MSBuild, Unity import, shader compiler, or package manager process. If blocked, stop with `PENDING PROCESS GATE`.
2. Read all authority and evidence docs listed above. Record mandates followed in the future owner report.
3. Parse `MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.csv` and `TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.csv` for exact row targets. Do not broaden scope by filename guessing.
4. Run readback-only Unity inspection of the failed product-face targets: shader object, material asset path, texture slots, shader keywords, render queue, active scene users, null slots, package-default refs, proxy refs, and unresolved refs.
5. Produce a target table with `RendererOrMaterial`, `CurrentMaterial`, `CurrentTextureRoles`, `BlockingReason`, `OwnerRoute`, `RequiredReplacement`, `ProofNeeded`.
6. Checkpoint: if exact active targets cannot be proven, mark later binding tasks `BLOCKED BY READBACK` and do not edit assets.

### Phase 1 - Material Family Contracts

7. Define route-owned material families for each proven product-face group: player/suit, held tools, item/tool prefabs, pickups, transport, construction/product shell, sky/Aegir, ocean/foam/contact, terrain/geology, flora/coral.
8. For every family, write a channel contract: albedo, normal/detail-normal, packed MRAO or mask, emission/wetness/special channel, color space, compression target, mip policy, streaming mip policy, max-size lane, and material slot.
9. Reject any target that would need per-object material clones, MPB on standard geometry, or one-off shader copies to pass.
10. For Crest/ocean, identify visible material slots separately from wave-data slots. Confirm no `_WD_*` slot receives artist foam/contact/caustic art.
11. For generated/source-only maps, classify each as `REFERENCE_ONLY`, `CLEANUP_REQUIRED`, `IMPORT_CANDIDATE`, or `REJECTED`. Watermarked, seamed, baked-light, or false-channel sources stay out of final binding.
12. Checkpoint: no texture or material binding may proceed without a complete family contract and active target proof.

### Phase 2 - Texture Cleanup, PBR Role Separation, And Import Proof

13. Build or clean candidate texture packs offline for the proven blocker families. Minimum product-face stack: albedo/base, normal/detail-normal, packed MRAO/mask, and any family-specific emission/wetness/contact mask.
14. For foam/contact, replace `foam.png` visible use with authored contact masks: salt rim, wet edge, bubble breakup, shoreline residue, shallow normal breakup, and low-contrast ocean foam albedo. Do not reuse the turquoise sheet as final.
15. For Aegir/sky/cloud, clean source candidates into band/detail/storm/terminator roles. Do not accept the baked disc or stale YAML slot evidence as hero proof.
16. For terrain/geology and wet shoreline, prove tileability with 1x, 2x, and 4x contact sheets. Reject repeated macro islands, baked directional light, watermark crop, and normal overcrank.
17. For flora/coral, prove organic channel semantics: tissue/calcification albedo, normal structure, AO/cavity, wetness/roughness, alpha/dither role, optional biolum mask, and LOD/silhouette compatibility.
18. Import through Unity importer APIs only. Read back sRGB, texture type, normal map handling, compression, mip chain, streaming mips, platform overrides, max size, and read/write state.
19. Checkpoint: if importer readback fails or source maps remain seamed/watermarked/false-PBR, revert the candidate binding and keep the source as reference only.

### Phase 3 - Material Binding And Route Repair

20. Bind only route-owned `MAT_*` assets to proven active product-face renderers. No raw YAML, no scene save outside explicit authorized edit flow, no Crest clone path.
21. Replace package-default, blockout, placeholder, proxy, null, and empty-slot material routes with family-correct material assets only after Phase 2 proof.
22. Confirm SRP Batcher compatibility, shader family, keyword budget, material instance count, SetPass risk, and shared-material use through Unity readback and Frame Debugger.
23. For standard geometry, reject MaterialPropertyBlock dependency unless the route is approved UI or legacy particle usage. For repeated geometry, prefer shared materials, instancing, or texture arrays where proof supports it.
24. For Crest/ocean, assign the approved asset material only through the canonical owner route. Do not create wrapper scripts or runtime material copies. Confirm visible foam/contact contribution through Frame Debugger.
25. Checkpoint: if binding increases material uniqueness, SetPass, shader variants, texture memory, or visual clutter without proof, roll back the binding and report `REGRESSION RISK`.

### Phase 4 - h8_1475 Visual, Render, Memory, And Regression Proof

26. Create a required proof packet under `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` with `manifest.json`, `manifest.sha256`, copied Unity log, route screenshots, readback summaries, and evidence labels.
27. Capture canonical Game View and Scene View screenshots: bright surface/ocean skin, shoreline/waterline, photic shallow, medium-depth hero route, player/suit, held tools, pickup, transport/product-face prefab, sky/Aegir/cloud context, and flora/coral material sample where active.
28. Capture Frame Debugger or RenderGraph/Stats evidence: shader names, material assets, texture slots, keyword state, transparent pressure, SetPass, batches, material instance count, and no Crest clone/wrapper path.
29. Capture memory/VRAM/import evidence: texture memory, total reserved memory, streaming mip state, async upload spike behavior, and compact 1800MB VRAM / 900MB texture budget pressure. Runtime claims without profiler or Memory Profiler stay `PENDING_VERIFICATION`.
30. Final gate: compare captures against `VISUAL_REFERENCE_REJECTION_20260605.md` and `CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`. If water, terrain, sky/Aegir, shore contact, tools, pickups, flora/coral, UI/cockpit, or product-face materials remain flat, muddy, primitive, blurry, dark-covered, or below the digest's surface/shallow/mid-depth floor, mark `REJECTED`, not accepted.

## Proof Requirements

Required acceptance proof:

- Unity readback table for every repaired product-face material route.
- Texture import readback for every new or changed map.
- Material family/channel manifest.
- Contact sheets for generated/cleaned textures, including tile/seam/mip views where tileable.
- `h8_1475` proof packet: `manifest.json`, `manifest.sha256`, copied Unity log, canonical screenshots, and evidence list.
- Frame Debugger or RenderGraph/Stats evidence for material assets, shader names, keywords, SetPass, batches, material instance count, and Crest visible slot use.
- Memory/VRAM proof for texture residency and compact texture budget risk.
- Explicit proof label from `quality.md`, with static text-only work limited to `STATIC_STRUCTURE_REVIEWED` or `PENDING VERIFICATION`; runtime/editor/player/profiler labels require matching artifacts.

Static docs may only support structure review. Product-face visual acceptance requires player-capture proof plus matching readback/render/memory evidence.

## Abort Conditions

Abort and report if:

- Process gate is blocked by active build/import/compiler work.
- Active renderer/material targets cannot be read back.
- Required target is absent and no owner route exists.
- Any task would require raw YAML `.mat`, `.unity`, `.prefab`, `.asset`, or `.meta` edits.
- Crest repair would require runtime wrapper, clone, override, or `_WD_*` artist texture misuse.
- Final candidate source remains watermarked, seamed, baked-light, false-PBR, or channel-undocumented.
- Screenshot proof relies on fog/darkness/post/crop to hide weak assets.
- Screenshot proof omits mandatory digest comparison for any user-visible water, terrain, sky/Aegir, flora/coral, UI/cockpit, shoreline, or medium-depth material route.
- Material binding breaks compile/import, creates shader errors, introduces package-default fallback, or increases SetPass/material uniqueness without proof.
- Compact lane loses readable ocean color, sky/Aegir, waterline, route cues, material identity, or instrument/product-face readability.

## Low / Middle / High / Ultra Consequences

- Low / compact, near `GlobalQualityWeight = 0.0`: compressed route-owned maps, stable shared materials, baked AO/channel packing, conservative mips, reduced secondary contact/detail density, and no proxy substitution. Bright ocean, sky/Aegir, shoreline, product-face tools, route cues, and material identity remain mandatory.
- Middle, around `0.35`: full route-owned PBR stacks, stable texture roles, controlled streaming mips, dithered LOD, clear waterline/shoreline breakup, and proven player/tool/pickup material identity.
- High, around `0.7`: richer detail normals, wet-edge/contact response, stronger Aegir/cloud material depth, improved terrain/geology breakup, denser organic detail, longer near-field texture residency after memory proof.
- Ultra, near `1.0`: layered hero material response, capture-grade sky/ocean/contact detail, richer flora/coral/geology material overkill, extended reflection/lighting response, and denser near-field dressing after Stats, Frame Debugger, screenshots, profiler, and memory evidence. Gameplay truth, Crest ownership, material authority, DTO layout, save identity, and route ownership do not change.

Final status: `PENDING_VERIFICATION`.
