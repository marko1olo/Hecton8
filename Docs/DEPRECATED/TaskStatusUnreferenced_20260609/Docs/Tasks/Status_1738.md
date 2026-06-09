# Agent 1738 Status - Remote Drone & Automation Probe Assembler

Prompt: `Docs/Tasks/CURRENT_BATCH.md` contains `<AGENT_PROMPT id="1738">`; extracted by CLI. Domain: AI / Creatures / Sonar / Drones. Task count: 23.

Domain source file requested by AGENTS (`Docs/Actual Domains of Project.txt`) is missing; fallback domain evidence is the 1738 XML, `drones.md`, and `PROCEDURAL_ASSET_PIPELINE.md`.

Relevant mandates read:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` - cold DI only; no hot registry polling.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - 0 B hot path; editor-only reflection/search allowed.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` - no hidden `.Complete()` or tiny same-frame jobs.
- `DATA_Runtime_Struct_Layout_ARM64.txt` - unmanaged runtime structs, 8-byte alignment, no runtime bool.
- `ANIM_IK_FABRIK_GroundSnapping_Procedural.txt` - Burst IK consumes cached arrays and clamped joint data.
- `ANIM_Contextual_Physical_IK.txt` - no per-bone traversal in IK path; cache bone indices at spawn/authoring.
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt` - primitive/cached collider discipline; no hot `GetComponent`.
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` - bounded proxies before expensive physics.

## Checklist

- [x] Task 01 - Prompt/domain/mandate extraction. DOD: CLI-extracted 1738 XML from `CURRENT_BATCH.md`; counted 23 tasks; read drone/pipeline docs and 8 mandates. Alternative rejected: relying on old chat-only fallback. Static estimate: 3600 us.
- [x] Task 02 - Existing drone factory/runtime audit. DOD: found no first-party `DronePrefabFactory.cs`; audited drone runtime/fleet routes and neighboring editor factories. Alternative rejected: runtime mesh/bone assembly. Static estimate: 9400 us.
- [x] Task 03 - Editor drone prefab factory. DOD: `DronePrefabFactory.cs` creates root hierarchy, discovers source prefab/mesh groups, combines visual meshes per bone, assigns shared materials, saves `PFB_[DroneName].prefab`, and cleans temp roots in `finally`. Alternative rejected: scene-time prefab construction. Runtime estimate: 0 us; editor only.
- [x] Task 04 - Bone metadata SOA. DOD: `DroneBoneMetadata` owns ordered bone refs and explicit-layout 128-byte joint DTOs; exports to caller-owned `NativeArray<DroneBoneJointRuntimeData>` without hierarchy search. Alternative rejected: runtime transform/name traversal. Static estimate: one spawn-time traversal eliminated per configured prefab.
- [x] Task 05 - Attachment metadata SOA. DOD: `DroneAttachmentMetadata` is the sole owner of tool socket/thruster DTOs; `DroneBoneMetadata` duplicate attachment types were removed; runtime DTO is 96 bytes and 8-byte aligned. Alternative rejected: merged duplicate component definitions. Static estimate: one socket/VFX lookup traversal eliminated per configured prefab.
- [x] Task 06 - Primitive physics proxy gate. DOD: factory searches `COL_[DroneName]`, validates only `BoxCollider`/`SphereCollider`, rejects `MeshCollider`/`ParticleSystem`, assigns dynamic layer, and attaches one kinematic `Rigidbody`; fallback proxy uses `BoxCollider` primitives only. Alternative rejected: capsule/mesh collision proxies. Static estimate: no collider cooking in runtime.
- [x] Task 07 - BRG/material gate. DOD: renderer materials must be asset-backed shared materials with `_EmissionColor` and SRP/BRG-compatible `UnityPerMaterial` CBUFFER evidence. Alternative rejected: material clones or unbatched emission variants. Static estimate: fleet material clones avoided.
- [x] Task 08 - DroneFleetManager cold metadata facade. DOD: cached bone and attachment tables in prewarmed static arrays during `ConfigureHeadlessRenderSource`; public facade copies to caller-owned `NativeArray`s; only cold `TryGetComponent` calls exist. Alternative rejected: hot `GetComponentInChildren` or `GlobalRegistry.Get<T>()` polling. Static estimate: 0 hot allocations.
- [x] Task 09 - DataVault lock flattening in drone render upload. DOD: render/culling DTOs are prepared into scoped `GraphicsBuffer.LockBufferForWrite` windows outside vault write locks; vault locks contain only linear copy and strict `finally ReleaseWriteLock`; no manager-owned persistent `NativeArray` scratch remains. Alternative rejected: GPU upload while holding write lock or persistent manager NativeArray aliasing. Static estimate: removes lock-held GPU upload stall vector.
- [x] Task 10 - Fleet black-box lock flattening. DOD: black-box hash/NaN scan is built before acquiring the vault write lock; the lock writes one `DroneFleetBlackBoxEntry` and releases in `finally`; dump uses read-only vault snapshot after lock release. Alternative rejected: hashing 512 drones under DataVault write lock. Static estimate: lock duration reduced to one struct write.
- [x] Task 11 - Validation. DOD: Unity MCP validation passed for `DroneBoneMetadata.cs`, `DroneAttachmentMetadata.cs`, and `DronePrefabFactory.cs`; `DroneFleetManager.cs` MCP validation timed out on regex engine, so targeted brace/preprocessor scans and forbidden-token scans were used. `dotnet build` not launched because CPU/compiler gate is closed. Alternative rejected: launching build under active external compiler.
- [x] Task 12 - CSV tuning import lock flattening. DOD: `TryApplyDroneSpecsCsv` now reads into `stackalloc Span<byte>` and parses outside DataVault write locks; removed the obsolete vault CSV scratch handle/buffer/method so no nested tuning/chassis write-lock can occur from the import route. Alternative rejected: keeping a 16 KB DataVault scratch buffer as dead cold memory. Static estimate: removes file IO and two CSV passes from DataVault critical sections.
- [x] Task 13 - Factory proof-I/O and collision layer polish. DOD: removed the optional JSON report disk writer/toggle from `DronePrefabFactory` and forced primitive proxy assignment through the existing `World_Dynamic` layer route instead of preferring an invented `Drone_Collision` layer. Alternative rejected: keeping a disabled JSON proof path or adding a new collision layer contract. Runtime estimate: 0 us; editor I/O surface reduced.
- [x] Task 14 - Metadata layout validation reflection purge. DOD: removed `System.Reflection`, `FieldInfo`, `UnsafeUtility.GetFieldOffset`, and `OffsetOfRuntime` from bone/attachment metadata; `ValidateStaticLayout()` is now pure `UnsafeUtility.SizeOf<T>()` plus 8-byte alignment against explicit-layout DTO sizes. Alternative rejected: editor-only field-offset reflection called from runtime integration checks. Static estimate: removes managed reflection route from metadata validation.
- [x] Task 15 - Factory hard-surface normal preservation. DOD: `DronePrefabFactory` now preserves authored mesh normals during bone mesh combine and only calls `RecalculateNormals()` when a source mesh lacks the normal vertex attribute. Alternative rejected: blind normal rebuild that erases weighted/hard-surface authoring on drone chassis meshes. Runtime estimate: 0 us; editor-only visual quality gate.
- [x] Task 16 - Direct-index SOA contract hardening. DOD: removed the unused linear `TryGetJointByBoneIndex` API and made bone/attachment validation reject tables where row index does not equal `BoneIndex`/`AnchorIndex`; bone parents must be parent-before-child, solver weights and quality weights must be clamped to 0..1, and negative stiffness/damping is rejected. Alternative rejected: leaving a public O(n) lookup route for future IK code. Static estimate: removes one API-level search vector.
- [x] Task 17 - Authoring bone topology normalization. DOD: `DronePrefabFactory` now normalizes JSON-authored bones in-place so parent rows precede child rows before prefab bake; unresolved parents or cycles reject the authoring file and fall back to the deterministic default rig. Alternative rejected: baking authoring order directly and forcing runtime remap/search tables. Runtime estimate: 0 us; editor-only route.

## Loop Log

Loop 01: Prompt/domain extraction. Result: XML present; 23 tasks.
Loop 02: Existing-code audit. Result: no first-party factory; runtime fleet route is the cold integration point.
Loop 03: Factory/metadata implementation. Result: editor-only assembler, bone metadata, attachment metadata, primitive proxy gates.
Loop 04: Self-read cleanup. Result: removed duplicate attachment definitions from `DroneBoneMetadata.cs`.
Loop 05: Runtime integration. Result: cold cached bone/attachment SOA facade in `DroneFleetManager`.
Loop 06: Lock audit. Result: render/culling GPU upload moved outside DataVault write locks.
Loop 07: Native sovereignty pass. Result: removed persistent render/culling NativeArray scratch; direct GPU lock windows now stay scoped to upload methods.
Loop 08: Verification gate. Result: three Unity script validations clean; manager regex validation blocked by tool timeout; CPU 100% and active `dotnet` PIDs 43220/51024 block project build.
Loop 09: CSV lock pass. Result: CSV import uses stack scratch; obsolete vault scratch buffer removed; CPU 100% and active external `dotnet` processes still block project build.
Loop 10: Factory polish pass. Result: JSON report writer removed; collision layer route pinned to `World_Dynamic`; Unity MCP standard validation clean for factory and basic validation clean for manager.
Loop 11: DTO validation polish. Result: reflection removed from metadata layout validators; Unity MCP standard validation clean for both metadata scripts and basic validation clean for manager.
Loop 12: Factory visual quality pass. Result: combined drone meshes preserve authored normals unless the source mesh has no normal vertex attribute; Unity MCP standard validation clean for factory.
Loop 13: Metadata SOA contract pass. Result: direct-index bone/attachment tables are enforced at validation; Unity MCP standard validation clean for bone metadata, attachment metadata, and factory.
Loop 14: Authoring topology pass. Result: JSON bone order is normalized before bake; cycles/unresolved parents fall back to deterministic default rig; Unity MCP standard validation clean for factory.

## Proof

- Current batch extract: `EXPLICIT_TASK_COUNT=23` by `^Task\s+\d+:` inside `<AGENT_PROMPT id="1738">`.
- `DronePrefabFactory.cs` SHA256: `720C1E098BC00BF42E3F5A4C59EAC735BB2B33255CFE30B05BFA0F45507AFF86`
- `DroneBoneMetadata.cs` SHA256: `B3F64E8C60BFFC8AB02444AB59AD15F01DC17F110E0A3D0C564A3F03D6E2E4CF`
- `DroneAttachmentMetadata.cs` SHA256: `68A875BB72A984D93A4D628B52BD629729EE5FEEE8A599AD8BDA0DC1E19638C3`
- `DroneFleetManager.cs` SHA256: `F317C790A6391D314CBD36F6CD85A8BD85725735C7794B4F6DF2FEC33F1D7BB2`
- `git diff --check -- <1738 files>`: no whitespace errors; Git warned only about future CRLF normalization. Full-repo `git diff --check` is blocked by pre-existing trailing whitespace in `Assets/_Project/Scenes/02_HECTON_WORLD.unity` lines 26200/26204.
- Orphan `.meta` scan: `ORPHAN_META_COUNT=0`.
- Hot lookup scan: no `GlobalRegistry.Get<`, `GetComponent()`, `GetComponentInChildren`, `GameObject.Find`, `.Complete()`, `WaitForCompletion`, or LINQ tokens in touched files; only two cold `TryGetComponent` prefab-cache calls remain.
- CSV lock scan: `File.OpenRead` is separate from all `TryAcquireDroneVaultWriteBuffer`/`ReleaseWriteLock` sites in `DroneFleetManager`; removed all `DroneSpecsCsvScratch` references.
- Factory source proof-I/O scan: no `ReportPath`, `WriteReportToDisk`, `File.WriteAllText`, `JsonUtility.ToJson`, `DRONE_PREFAB_FACTORY_REPORT`, `Drone_Collision`, or `DroneCollisionLayerName` symbols remain in `DronePrefabFactory.cs`.
- Metadata reflection scan: no `System.Reflection`, `FieldInfo`, `UnsafeUtility.GetFieldOffset`, or `OffsetOfRuntime` symbols remain in `DroneBoneMetadata.cs` / `DroneAttachmentMetadata.cs`.
- Unity MCP validation: `DronePrefabFactory.cs` standard validation 0 errors / 0 warnings; `DroneBoneMetadata.cs` standard validation 0 errors / 0 warnings; `DroneAttachmentMetadata.cs` standard validation 0 errors / 0 warnings; `DroneFleetManager.cs` basic validation 0 errors / 0 warnings.
- Unity console: last successful `read_console` query returned 0 error entries; latest retry blocked by Unity MCP session not ready.
- Build throttle: project `dotnet build` skipped; CPU 100%, active external `dotnet` processes present.
