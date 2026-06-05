# Batch20 2008 ProductFace Unity Owner Validation Checklist

Status: STATIC HANDOFF. This checklist is for the later Unity/editor owner.

## Static Preflight

- Confirm no parallel Unity/editor owner is already editing ProductFace assets.
- Confirm worktree state and preserve unrelated agent changes.
- Confirm `Tools/ProductFaceStaticRouteAudit.py --root . --json` returns no ProductFace route findings.
- Confirm ProductFace texture/material manifest exists and is not `ai_texture_prefab_bindings.csv`.
- Confirm every candidate relink has a source mesh, material, texture stack, import settings, shader slot contract, and rollback path.
- Confirm Batch18 1892 and Batch19 1906 evidence is still the active ProductFace handoff basis.
- Treat missing Batch18 task files `1902`, `1903`, and `1904` as absent unless later supplied by the controller.

## Unity Slot Sequence

1. Take the single ProductFace owner slot.
2. Snapshot dirty state and abort on unrelated active prefab/material/texture edits.
3. Generate ProductFace mesh source assets only from approved editor authoring scripts.
4. Import ProductFace textures into approved ProductFace texture roots only.
5. Set import settings before binding: albedo sRGB, normals as Normal Map, packed masks linear, mips on, Read/Write off, compressed platform format.
6. Create or update owned ProductFace materials only after shader-specific channel contracts are recorded.
7. Relink prefabs family by family: tools, resources, transport, player suit, legacy aliases, sky/ocean gate exceptions.
8. Preserve serialized gameplay references, anchors, colliders, data refs, pickup behavior, camera/HUD links, and route ownership.
9. Run gates before any acceptance claim.

## Required Gates

- ProductFace Prefab Quality Gate.
- ProductFace Material Texture Gate.
- ProductFace Sky/Ocean Source Primitive Gate.
- Generated Asset Production Audit.
- Generated Asset Production Audit fail-on-error mode.
- Material import settings audit.
- Static route audit after relinks.

## Visual And Gameplay Readability Proof Set

Required screenshots before visual acceptance:

- Surface coastline.
- Aegir and moons horizon.
- Waterline.
- 5-20m under-surface.
- Photic 30-100m.
- Medium-depth hero route.
- Held tool close-ups.
- World tool pickup close-ups.
- Resource pickup close-ups.
- Player glove/forearm/visor close-ups.
- Transport hull/glass/rider-anchor readability close-ups.

Required gameplay checks:

- Tool silhouettes, held/world identity, and function origins remain readable.
- Resource families remain distinct and preserve data refs.
- Suit does not occlude camera/HUD or break hand anchors.
- Transport entry/exit, rider/dismount anchors, collision clearance, and role readability remain intact.
- No route hides weak assets with darkness, fog, or distance.

## Channel Contract Checklist

- Do not infer ORM/MRAO/ARM channels from filenames.
- For every material, record shader name, texture slot, sRGB flag, compression, mip policy, and RGBA meaning.
- `Hecton_ToolDecayLit` PackedMaskV1 uses `_MaskMap` R=Metallic, G=AO/Occlusion, B=Smoothness, A=EmissionMask.
- `Hecton_ProceduralBio` uses `_ORMAtlas` R=Occlusion, G=Roughness, B=Metallic, A=EmissionMask.
- `Hecton_MraoAtlasLit` uses `_MraoMap` R=Metallic, G=Roughness, B=AO, A=EmissionMask.
- `SuitVisor` uses `_VisorMaskTex` R=Dirt, G=Scratch, B=Salt, A=Condensation.
- Block AI/UberNoir ARM, resource minerals, resource organics, TitaniumScrap, transport hull/rubber/trim, transport glass, and player suit body/trim until the owner records exact contracts.

## Performance And Runtime Rejection Checks

- No runtime texture generation.
- No runtime compression.
- No hot-path material clone.
- No `renderer.material` mutation path.
- No runtime prefab relink.
- No managed allocation hot path introduced by repair code.
- No tiny jobs or same-frame schedule/readback loops if any validation utility is added.
- No ProductFace repair system may exceed budget claims without profiler proof.

## Low / Middle / High / Ultra Consequences

Low:

- Smaller approved texture imports and cheaper shader variants are allowed.
- Silhouette, anchors, data refs, gameplay truth, pickup readability, and ProductFace visual floor are not allowed to degrade into primitive/flat assets.

Middle:

- Full authored ProductFace materials and stable mips are expected.
- Optional detail layers stay conservative.

High:

- Add detail masks, decals, richer emissive response, and close-up material fidelity after base proof passes.

Ultra:

- Higher resolution, stronger secondary masks, and hero-close detail are allowed.
- Ultra does not change gameplay truth ownership, save identity, DTO layout, authority route, anchors, or colliders.

## Abort Conditions

- Unity owner slot is not exclusive.
- Material contract is missing.
- Source asset route is outside ProductFace-approved roots.
- Candidate source is an environment donor, package default, placeholder, blockout, diagnostic, or flat shell.
- `ai_texture_prefab_bindings.csv` is proposed as ProductFace truth.
- Relink would split held/world tool identity.
- Relink would change item data refs, camera/HUD references, rider anchors, dismount anchors, or colliders without scoped owner approval.
- Validation fails and there is no rollback.
