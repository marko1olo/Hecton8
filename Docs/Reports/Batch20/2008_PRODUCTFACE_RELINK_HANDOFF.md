# Batch20 2008 ProductFace Relink And Channel Contract Unity Handoff

Status: STATIC VERIFIED HANDOFF ONLY. DO NOT APPLY RELINKS FROM THIS REPORT.

Worker: Batch20 / 2008  
Scope: ProductFace static debt consolidation for the later Unity/editor owner.  
Forbidden work respected: no Unity launch, no prefab edits, no material edits, no texture edits, no blind relinks, no deletion of package/default materials.

## Evidence Boundary

This report is a static handoff. It does not claim editor validation, playmode validation, profiler validation, or in-game visual proof.

Static route audit result:

`python Tools\ProductFaceStaticRouteAudit.py --root . --json`

Result: `ERROR=0`, `WARNING=0`, `INFO=0`. This proves only that the static route audit found no current source/report route drift.

Batch18 task files `1902`, `1903`, and `1904` were not located by scoped text search under `Docs/`. Their contents were not inferred.

## Authority Loaded

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `presentation.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `performance.md`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Static Debt Counts

Inherited static evidence from Batch18/Batch19:

- Built-in primitive mesh debt: 42 ProductFace prefab errors reported by the Batch18 1892 handoff from the 1867 gate.
- Material assignment audit: 61 ProductFace assignments scanned, 55 blocked rows reported by Batch19 1909 from Batch18 1893 evidence.
- Package/default `Lit.mat`: 17 blocked rows.
- Tool placeholder material rows: 23 blocked rows.
- Resource flat-color shell rows: 8 blocked rows.
- Player blockout material rows: 6 blocked rows.
- Direct source evidence remains for `Item_Titanium.prefab`: built-in primitive mesh plus package/default material GUID `31321ba15b8f8eb4c954353edc038b1d`.
- Direct source evidence remains for `Buildings/Cube.prefab`: built-in primitive mesh. This is not deletion approval.
- Current ProductFace static route audit findings: 0.

These counts are static evidence, not a current Unity gate rerun.

## ProductFace Sources Inspected

ProductFace editor scripts define source-authoring routes only. They do not apply prefab relinks.

- `ProductFaceToolMeshSourceAuthoring.cs`: 12 tool mesh source specs.
- `ProductFaceResourcePickupMeshSourceAuthoring.cs`: 8 resource pickup mesh source specs.
- `ProductFaceTransportMeshSourceAuthoring.cs`: 4 transport mesh source specs.
- `ProductFacePlayerSuitMeshSourceAuthoring.cs`: 10 player suit mesh source specs.
- `ProductFacePrefabQualityValidator.cs`: prefab renderer and built-in primitive mesh gate.
- `ProductFaceMaterialTextureValidator.cs`: ProductFace material/default/package/placeholder/channel gate.
- `ProductFaceSkyOceanSourceValidator.cs`: sky/ocean primitive gate with narrow Crest hidden-input exceptions only.

The possible generated mesh-source scope is 34 source assets. None are marked generated, imported, relinked, or validated by this task.

## Relink Law For Later Unity Owner

The later Unity owner must run a serialized owner slot. Do not split ProductFace relinks across parallel agents.

Required flow:

1. Preflight dirty worktree and Unity editor state.
2. Run static route audits and confirm no route drift.
3. Confirm ProductFace material/texture manifest exists and is ProductFace-specific.
4. Generate or import approved ProductFace mesh/material/texture sources only.
5. Apply import settings before any prefab relink.
6. Relink prefabs by family with rollback.
7. Preserve serialized gameplay references, anchors, colliders, data refs, and route ownership.
8. Run ProductFace prefab quality gate, material texture gate, sky/ocean source gate, generated asset production audit, screenshots, Frame Debugger, profiler, GC, and memory proof before acceptance claims.

No candidate in `2008_CANDIDATE_RELINKS_DO_NOT_APPLY.csv` may be applied from filename resemblance, material name resemblance, or `ai_texture_prefab_bindings.csv`.

## Channel Contract Requirements

Shader-specific channel contracts win. The project has multiple legitimate packing orders:

- `Hecton_ToolDecayLit` / PackedMaskV1: `_MaskMap` R=Metallic, G=AO/Occlusion, B=Smoothness, A=EmissionMask.
- `Hecton_ProceduralBio`: `_ORMAtlas` R=Occlusion, G=Roughness, B=Metallic, A=EmissionMask.
- `Hecton_MraoAtlasLit`: `_MraoMap` R=Metallic, G=Roughness, B=AO, A=EmissionMask.
- `SuitVisor`: `_VisorMaskTex` R=Dirt, G=Scratch, B=Salt, A=Condensation.

Because route bibles, mandates, and static tools contain different generic ORM/MRAO/ARM conventions, ProductFace must not infer channels from filenames. The material owner must provide a shader, slot, and channel contract per material family before import or binding.

## Material Import And Compression Expectations

- Albedo/base color: sRGB on, mipmaps on, compressed platform format, Read/Write off.
- Normal: Texture Type Normal Map, sRGB off, BC5 or platform equivalent where available, mipmaps on, Read/Write off.
- Packed masks: sRGB off, documented channel order, mipmaps on unless explicitly blocked by shader use, compressed linear format, Read/Write off.
- Emissive/display masks: channel contract must state whether alpha is used.
- No runtime `Texture2D` generation, runtime compression, hot-path material clone, `renderer.material` edits, or runtime relink.
- No separate AO/roughness/metallic samplers unless shader ABI forces compatibility and the owner records the exception.

## Unsafe Sources Rejected

- Unity default/package `Lit.mat`.
- `Mat_Tool_*_Placeholder`.
- `Mat_Resource_*` flat shells as final material proof.
- `MAT_PlayerSwimBlockout`.
- `RuntimeVisualProof` swatches as final ProductFace materials.
- Diagnostic, checker, error, flat-color, and missing texture slots.
- Sky/ocean/terrain/Crest/flora/depth/noir materials as ProductFace body PBR donors.
- `ai_texture_prefab_bindings.csv` as ProductFace truth.

## Gameplay Readability Checks

Tools:

- Held/world pair must remain visually identical enough to read as the same object.
- `toolData`, muzzle/beam/scan origins, hand anchors, pickup behavior, screen/emission readability, and icon identity must survive relink.
- Tool silhouettes cannot collapse into generic cylinders, boxes, or crayon color shells.

Resources:

- Pickup identity must be readable at gameplay distance and close inspection.
- Mineral, organic, membrane, resin, shard, sulfur, copper, silver, and scrap families need distinct material language.
- `Data_Copper` and `Data_TitaniumScrap` references must be preserved. Do not create a new `Data_Titanium` route from the legacy alias.

Suits:

- First-person gloves, forearms, visor rim, HUD readability, and body trim must remain coherent with camera and hand anchors.
- Visor dirt/scratch/salt/condensation channels are not a generic body/trim contract.

Transport:

- Hull, rubber, glass, cockpit/handle zones, rider anchors, dismount anchors, and collision clearance must survive.
- The player must read entry/exit affordances and transport role without relying on UI labels.

## Scalability Consequences

Low:

- Use approved lower-resolution imports, fewer optional emissive/detail layers, stable mip bias, and cheaper shader variants.
- Gameplay truth, silhouette readability, anchors, colliders, and data refs must not change.

Middle:

- Use full ProductFace authored materials at normal cadence with conservative mask/detail use.
- No runtime allocation, relink, or material clone paths.

High:

- Add richer material detail, decals, controlled emissive response, and route-specific close-up fidelity after base proof passes.
- Spend performance on readability and premium surface response, not hidden complexity.

Ultra:

- Use visual overkill only after the same contracts pass: higher texture resolution, richer secondary masks, stronger close-up proof, and better hero-route capture.
- Ultra must not change DTO layout, save identity, authority route, pickup truth, or gameplay semantics.

## Unity Owner Acceptance Packet Required Later

The future owner must attach proof artifacts before marking ProductFace debt resolved:

- ProductFace Prefab Quality Gate output.
- ProductFace Material Texture Gate output.
- ProductFace Sky/Ocean Source Gate output.
- Generated Asset Production Audit output with fail-on-error.
- Material import settings proof.
- Unity slot relink diff summary.
- Screenshots for tools, resources, suits, transport, surface/coastline, waterline, photic shallows, medium-depth hero.
- Frame Debugger/profiler/GC/memory proof if performance or runtime acceptance is claimed.

No in-game proof exists from worker 2008.
