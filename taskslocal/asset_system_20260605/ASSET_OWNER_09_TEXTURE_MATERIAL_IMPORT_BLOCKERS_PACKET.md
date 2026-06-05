# Asset Owner 09 - Texture/Material Import Blockers Packet

Status: `PENDING VERIFICATION`.
Scope: next texture/material owner execution packet.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`, `STATIC_IMAGE_QA` only.

No Unity run, import, material edit, prefab edit, scene save, Addressables build, profiler, Memory Profiler, Frame Debugger, runtime test, or asset mutation is covered by this packet. It is static routing evidence only.

## Mandates Followed

- `STRM_Async_Asset_Upload_Texture_Settings`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`

## Hard Boundary

- Do not edit `Assets/` from this packet alone.
- Do not raw patch `.meta`, `.mat`, `.prefab`, `.unity`, or `.asset` YAML unless FileID, GUID, and property alignment are mathematically proven and validated afterward.
- Prefer Unity Editor API/importer-driven changes for texture import settings, material slot binding, prefab/scene mutation, and Addressables labels/groups.
- Do not create runtime wrappers, clones, or material overrides for Crest. Assign the correct asset material through the owner route.
- Do not claim importer state, compression, streaming mip behavior, material slot effect, scene visibility, memory, VRAM, SetPass, GC, or visual acceptance without fresh Unity proof.

## Prioritized Blocker Groups

Counts are from `Docs/AssetAudit/TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.csv`.

| Priority | Group | Rows | Blocker |
|---|---|---:|---|
| P0 | terrain/geology | 51 | Reachable terrain/geology sources include proxy/placeholder material contamination and unproven PBR/channel roles. Needs non-proxy route-owned materials, importer readback, tile/seam proof, and bright route screenshot. |
| P0 | flora/coral/fauna | 48 | Imported organic stacks are reachable, but proxy contamination, import/streaming mip status, alpha/dither path, LOD/silhouette proof, and material binding are unproven. |
| P1 | sky/Aegir/cloud | 8 | Aegir/cloud/sky sources are reachable, but hero material slots, storm/band channel response, bright surface screenshot, and in-scene readback are absent. |
| P0 | water foam/contact | 1 | `foam.png` is rejected static art and active-route reachable through the world/ocean material path. Replace with authored contact art and prove Crest/ocean slot binding. |
| P1 | water caustic | 1 | Caustic support source needs role-correct import, material binding, shader response, and screenshot proof before route use. |

## Hard Rejections

- Flat foam visible at shoreline/contact is rejected.
- Soft/toy Aegir disc cannot stand as surface-route proof.
- `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders` materials are not final route materials.
- Generated/source-only packs under `Docs/GeneratedAssets/AssetSystem_20260605/` are reference inputs only. Source files without import settings, material readback, screenshot, and memory proof are not product art.
- Static contact sheets cannot prove in-game visual quality.
- Future screenshots and contact sheets must compare against the mandatory visual-reference digest. Reject water, terrain, sky/Aegir, flora/coral, caustic, or UI-facing import changes that fail the digest's bright surface, shoreline, photic, dense organic, and medium-depth route signals.

## Safe Execution Route

1. Start from the CSV rows, not broad filename guesses.
2. For each texture family, assign owner, route moment, import role, material target, and rejection rule before touching Unity assets.
3. Use Editor API/importers for texture import changes: sRGB, texture type, normal map handling, compression, mipmaps, streaming mips, max size, platform overrides, and read/write state.
4. Preserve Addressables/material ownership: heavy route assets require group/key/release planning and must not bypass async upload budget.
5. Bind materials through the existing route owner. No per-object material clones, no MPB on standard geometry, no runtime Crest material instantiation.
6. After import/material changes, collect Unity readback before any route claim: importer settings, material slots, active scene renderer/user, screenshots/contact sheets, Stats/Frame Debugger, memory/VRAM.

## Acceptance Gates

- Static CSV coverage: all blocker rows remain addressable by `TexturePath`, `TextureFamily`, `Priority`, `BlockingRisk`, and `OwnerNextAction`.
- Importer readback: `PENDING UNITY`. Required for sRGB, type, compression, mipmaps, streaming mips, platform overrides, max size, and read/write.
- Material readback: `PENDING UNITY`. Required for Crest/ocean, sky/Aegir, terrain/geology, flora/coral/fauna, and caustic bindings.
- Contact sheet/screenshot proof: `PENDING UNITY`. Required for bright surface route, waterline contact, Aegir/sky, terrain/geology seams, organic silhouettes, and caustic response.
- Digest comparison proof: `PENDING UNITY`. Required before any material/import blocker can be promoted for user-visible water, terrain, sky, flora, UI, or route VFX contexts.
- Memory/VRAM proof: `PENDING UNITY`. Required before any readiness claim against compact 2GB VRAM / 900MB texture-budget lane.
- Render proof: `PENDING UNITY`. Required for SetPass, batches, shader variants, SRP Batcher, HLOD/LOD/dither behavior, and Frame Debugger route.

## Regression Model

- CPU: importer/material work can increase render prep, shader variant load, or upload spikes. Must be measured in Unity/player; no CPU improvement claimed here.
- GC: no runtime code touched. Future tooling must not add hot-path allocations; runtime proof remains absent.
- Memory: higher-resolution or added maps can increase resident memory. Must prove texture memory and total reserved memory after scene load.
- VRAM: compact ceiling is 1800MB with 900MB texture budget. Compression, streaming mips, and async upload buffer use require readback and Memory Profiler evidence.
- SetPass: new material families or shader variants can increase SetPass/batches. Must prove SRP Batcher compatibility and avoid per-object material clones.
- Correctness: wrong sRGB/linear, normal type, MRAO channel order, or material slot can create false visual proof. Import roles must be read back from Unity.
- Visual floor: surface, sky, Aegir, coastline, ocean surface, photic shallows, and medium-depth hero routes must stay premium and readable. Darkness/fog/post cannot hide weak art.

## GlobalQualityWeight Consequences

- Low: use compressed route-owned maps, conservative streaming mips, baked AO/channel-packed masks, stable silhouettes, and readable water/sky. Do not substitute proxy materials or flat foam.
- Middle: keep route-owned PBR stacks, stable import roles, dithered LOD, controlled residency, and verified material slots.
- High: spend saved budget on richer wet-edge detail, Aegir/cloud response, geology breakup, organic detail maps, caustic response, and longer LOD residency after proof.
- Ultra: add layered material detail, denser near-field dressing, richer reflections/lighting, and extended hero texture residency after measured memory/render proof. Gameplay truth and asset ownership route do not change.

Final status: `PENDING VERIFICATION`.
