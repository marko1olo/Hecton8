# Rationale 1908

Evidence class: STATIC_SOURCE

## Decisions

1. Atlas groups were split by material behavior, not by asset file folder:
   - hard calcified coral;
   - soft porous coral;
   - kelp blade/fiber;
   - kelp holdfast/root;
   - shallow rubble/geology support;
   - biolum/detail masks.

Reason: these groups map to different source roles, mip risks, shader channel needs, and ecology placement rules. Mixing kelp blades with coral calcium or wet geology would damage material identity and streaming locality.

2. `kelp_patch` and `kelp_patch_dense` are both tracked.

Reason: task names `kelp_patch`; Batch18 static evidence names `kelp_patch_dense`. Treating them as the same without Unity-owner confirmation would be invented completion.

3. Geology support stayed in the source atlas prep.

Reason: coral/kelp source quality is not enough if substrate looks like smooth blobs. Cave entrances, shelves, arches, spires, clusters, floors, rubble, strata, wetness, and mineral chips are the ecological carrier for shallow placement.

4. Bioluminescence is a mask group, not a color identity.

Reason: `VISION_LOCKS.md`, `TASTE.md`, and `ecosystem.md` allow bioluminescence as beauty and system information, but reject random glow noise. Emission sources must be sparse, biologically placed, and separate from gameplay truth.

5. Prompt packs repeat the no-import/no-object-render constraints in every file.

Reason: source prompts can be misused as final texture/import instructions. This lane cannot create Unity textures or visual proof.

6. Proof naming uses future required artifacts but marks all Unity-dependent files `PENDING UNITY OWNER`.

Reason: text cannot clear `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`, LOD/proxy proof, collider proof, material import proof, compact/high screenshots, or profiler capture.

## Hardware Consequences

- Compact: fewer atlas variants, packed masks, 512/1024 source masters where appropriate, 8 px minimum 512-source padding, no alpha-blend field dependence, silhouette/material readability preserved.
- Middle: 1024/2048 key sources, 12 px 1024-source padding, better cavity/roughness separation, richer shared overlays.
- High: 2048 hero sources, 16 px padding, stronger pore/vein/strata normals and wetness/AO detail.
- Ultra: 2048/4096 hero-only sources, 24 px padding for 4096 sources, visual overkill through richer source bakes only; no gameplay truth or authority changes.

`GlobalQualityWeight` remains continuous. It may scale source resolution, mask precision, atlas page count, detail density, and future LOD distance. It must not change harvest anchors, collider identity, vertex color semantics, save identity, ecology ownership, or gameplay truth.
