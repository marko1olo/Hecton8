# Legacy World Reference

Date: 2026-05-07
Status: PENDING VERIFICATION

Purpose: hold older but still useful world-structure and terrain reference docs outside repo root.

## 2026-05-11 Current-State Override

- Current data boundary: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Current manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current visual-realistic-fake doctrine: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R41 current external root `Hecton8*.csproj` no-restore CLI compile surface is `0 Warning(s)` / `0 Error(s)` after restore assets exist; full restore graphs still carry vendor/package warnings. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## 2026-05-04 Current-State Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using these files.
- This folder is historical/reference material only, not current runtime, terrain, scatter, or scene authority.
- Internal assumptions may predate the current bootstrap, registry, world generation, and procedural asset pipeline.

## Files

- `TERRAIN_108_BIOMES_VISION.md`
- `terrain_description.txt`

These are reference material, not the primary runtime execution contract.

## 2026-05-12 DOC_VULCAN Deprecation Audit

Status: SOURCE-SCANNED, RUNTIME PENDING VERIFICATION.

[REQ] Treat every file in this folder as historical visual reference only. Do not use these files as terrain generation, scatter, flora, fauna, navigation, atmosphere, or voxel persistence authority.

[REQ] New work must cite the DOD replacement before using an idea from this folder. The replacement must point to active source-backed documentation or source files.

### Master List Of Deprecated Systems

| Deprecated file | Deprecated assumption | Current replacement |
| --- | --- | --- |
| `TERRAIN_108_BIOMES_VISION.md` | Hand-authored 108-biome lore matrix as terrain/runtime authority. | `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`, active voxel/scatter/flora/fauna source, and source-scanned pipeline READMEs. |
| `terrain_description.txt` | Manifesto-style world terrain text as generation contract. | `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`, `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md`, `Docs/ARCHITECTURE/MIGRATORY_FLORA_SYSTEM.md`, `Docs/ARCHITECTURE/HEADLESS_ECOSYSTEM_SIMULATION.md`, and current source scans. |

### Technical Replacement Rules

[REQ] Terrain carving must use voxel deltas, byte masks, and persistent compressed snapshots where source supports it. Do not resurrect sculpted GameObject terrain chunks from legacy prose.

[REQ] Scatter must use compute candidate generation, Hi-Z rejection, foveated cadence, compact GPU buffers, and BRG/indirect consumers. Do not restore per-prop GameObject placement.

[REQ] Flora must use shader age morphs, atlased materials, and flow-field deformation. Do not restore CPU transform growth.

[REQ] Fauna must use Utility AI, packed headless sectors, compute boids, and POI weights. Do not restore fully simulated distant creature scenes.

[REQ] Atmosphere and survival must use scalar fakes such as stress-scaled O2. Do not restore gas simulation language.

### Voxel Carving Replacement Contract

[SOURCE] `VoxelDeltaProcessor.cs` owns voxel delta persistence. Source includes `DeltaModeAdditive`, `DeltaModeReplace`, dirty masks, compacted replacement chunks, sparse RLE snapshot chunks, uniform SDF RLE chunks, byte-quantized SDF values, and a 300-entry voxel carving black-box ring.

[REQ] Subtractive carving must persist as sparse deltas and byte masks. A full voxel-field save is forbidden unless a recovery path proves no delta representation can encode the change.

[REQ] RLE persistence must prefer uniform SDF RLE for fully replaced chunks and sparse RLE runs for dirty cells. The loader must validate bounds and hashes before accepting chunk payload.

[REQ] Visual terrain damage must use subtractive deltas, material IDs, heat rings, and shader response. Do not rebuild old terrain-GameObject chunks from this legacy folder.

[SOURCE] `VoxelDeltaProcessor.cs`, `VoxelChunkModifiedEvents.cs`, `VoxelDynamicNavGridRuntime.cs`, and `VoxelDeformationSmokeTester.cs` define the current carving/event proof path.

[REQ] Carving ingress must use bounded `NativeQueue<VoxelCarveEvent>` packets. Invalid/non-finite carve payloads must be rejected before queue or scheduler admission.

[REQ] Successful carve commits must publish localized nav-grid patches through `VoxelDynamicNavGridRuntime.QueueLocalizedSdfPatch` and bounded 64-byte `VoxelChunkModifiedEvent` packets through `VoxelChunkModifiedEvents`.

[REQ] Terrain burn marks must use vertex color R and shader burn masks. Do not add `DecalProjector` dependencies for voxel laser damage.

[REQ] Collider refresh must use the async mesh bake path. Do not reassign rebuilt `MeshCollider` data synchronously on the carve frame.

[REQ] Voxel carving must keep a 300-entry black-box ring and dump `Docs/AgentLogs/Dump_WORLD_VOXEL_CAVING.bin` on fault.

### Troubleshooting Legacy Contamination

[FAIL] A new document cites this folder as current truth: add a replacement citation and mark the legacy idea as visual reference only.

[FAIL] A feature plan proposes GameObjects for mass scatter, flora growth, or fauna swarms: reject the plan and route it to compute, shader, BRG, or packed SoA contracts.

[FAIL] A carve plan proposes terrain decals, full-chunk save rewrites, or synchronous collider rebuilds: reject the plan and route it to voxel deltas, vertex burn colors, localized nav patches, and async bake.

[FAIL] A biome count or terrain shape conflicts with source-backed architecture: keep the visual intent if useful, but discard the runtime claim.
