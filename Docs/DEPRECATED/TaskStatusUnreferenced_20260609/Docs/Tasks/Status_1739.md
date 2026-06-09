# Status 1739 - Inventory/Container/Loot Prefab Assembly

Source state:
- `Docs/Tasks/CURRENT_BATCH.md` now contains `<AGENT_PROMPT id="1739">` with 23 tasks. CLI extraction was repeated after the user corrected the batch file.
- Active directive is the XML prompt plus user polish brief: editor-only inventory prefab factory; `ItemNodeData`; `ContainerMetadata` with `IK_Handle`; shared material/BRG audit; primitive Box/Capsule colliders on `Interactable`; no JSON report as proof artifact.
- Domain file `Docs/Actual Domains of Project.txt` is missing in this checkout. Domain inferred from bibles: Inventory, Items, Interaction, Authoring, Procedural Asset Pipeline.

Mandates selected:
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Loop 1 - Discovery / Tasks 1-5

- [x] 1. Extract 1739 assignment from current batch. DOD: CLI extraction against `CURRENT_BATCH.md`; no `<AGENT_PROMPT id="1739">` block found, only index lines 3423-3424. Alternative rejected: inventing missing XML. Estimate: 0 us runtime.
- [x] 2. Map existing inventory/container/loot authoring code. DOD: `rg` and source reads across `ItemData`, `PickupItem`, existing assembly factories, equipment/logistics metadata. Alternative rejected: standalone factory detached from existing contracts. Estimate: 0 us runtime.
- [x] 3. Identify `ItemNodeData` and `ContainerMetadata` contracts. DOD: confirmed absent before creation; added passive contracts under `Assets/_Project/Scripts/Inventory`. Alternative rejected: duplicate runtime pickup behavior. Estimate: 0 us runtime.
- [x] 4. Identify prefab/asset generation conventions and allowed output paths. DOD: matched `Assets/_Project/Editor/Assembly/*PrefabFactory.cs` conventions and `Interactable` layer from `ProjectSettings/TagManager.asset`. Alternative rejected: random generated path. Estimate: 0 us runtime.
- [x] 5. Draft implementation route. DOD: rationale entry recorded before code. Alternative rejected: runtime procedural generation. Estimate: 0 us runtime.

## Loop 2 - Implementation / Tasks 6-10

- [x] 6. Implement or extend `InventoryPrefabFactory.cs`. DOD: editor/offline factory added at `Assets/_Project/Editor/Assembly/InventoryPrefabFactory.cs`. Alternative rejected: gameplay-time prefab authoring. Estimate: 0 us runtime; editor bake cost reported per prefab.
- [x] 7. Attach `ItemNodeData` with stable item id and base weight. DOD: factory writes `itemHashId`, `baseWeightKg`, `baseVolumeM3`, stack/category/family/flags from `ItemData` or metadata. Alternative rejected: item identity from name lookup at runtime. Estimate: 0 us runtime.
- [x] 8. Calculate lid hinge axis and bake into `ContainerMetadata`. DOD: metadata axis/pivot accepted when authored; deterministic bounds fallback otherwise; `ContainerMetadata.TryGetLidAxis` validates baked axis. Alternative rejected: runtime hinge search. Estimate: 0 us runtime.
- [x] 9. Generate primitive colliders only. DOD: copied source colliders stripped; final validation rejects `MeshCollider`; factory creates only `BoxCollider`/`CapsuleCollider` children on `Interactable`. Alternative rejected: MeshCollider/LOD0 collision. Estimate: 0 us runtime.
- [x] 10. Preserve assembly/API compatibility. DOD: no existing public contract mutated; new components are passive, additive, and editor-configurable. Alternative rejected: breaking batch-shared interfaces. Estimate: 0 us runtime.

## Loop 3 - Verification / Tasks 11-15

- [x] 11. Static scan for forbidden hot-path patterns in touched files. DOD: `rg` found no `Update`, `FixedUpdate`, `LateUpdate`, scene finds, LINQ, resource loads, `NativeArray` allocation, or hidden `.Complete()` in touched files. Alternative rejected: assuming editor code is harmless. Estimate: 0 us runtime.
- [x] 12. Validate C# compile surface. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for all three touched scripts. Full Unity refresh compile not requested because `dotnet` PID 43220 was active and CPU probe timed out. Alternative rejected: launching build during active compiler/CPU load. Estimate: 0 us runtime.
- [x] 13. Read Unity console if available. DOD: Unity MCP console read returned 0 error entries after script validation. Alternative rejected: claiming clean without console. Estimate: 0 us runtime.
- [x] 14. Re-read factory code after edit. DOD: verified `BuildPrefab`, `BakeContainerMetadata`, `AttachPrimitiveColliders`, and `ValidatePrefab` paths after patching vector hash formatting. Alternative rejected: one-pass write. Estimate: 0 us runtime.
- [x] 15. Update final log. DOD: created `Docs/AgentLogs/LOG_1739.md` with exact evidence class and no player-build profiler claims. Alternative rejected: chat-only report. Estimate: 0 us runtime.

## Loop 4 - XML Reconciliation / Tasks 16-20

- [x] 16. Re-extract full XML prompt after batch correction. DOD: PowerShell regex extraction returned full `<AGENT_PROMPT id="1739">` block with 23 tasks. Alternative rejected: continuing from stale short brief. Estimate: 0 us runtime.
- [x] 17. Remove JSON-report write path from `InventoryPrefabFactory`. DOD: `File.WriteAllText`, `JsonUtility.ToJson`, `ReportPath`, and stale scratch lists removed from touched inventory factory. Alternative rejected: optional report toggle, because current proof requirement is source code. Estimate: 0 us runtime.
- [x] 18. Add SOA DTO projection gates. DOD: `ItemNodeData.TryBuildStackLimit`, `ContainerMetadata.TryBuildContainerRange`, and factory `InventoryRoutingNetwork.RuntimeLayoutValid()` gate validate ARM64-safe DTO routes. Alternative rejected: runtime lookup by component hierarchy. Estimate: 0 us runtime.
- [x] 19. Add XML-required container interaction anchor/material gates. DOD: factory bakes root `IK_Handle`, validates shared asset-backed material, SRP/BRG `CBUFFER_START(UnityPerMaterial)` proof, and `_EmissionStrength` for locked/sealed names/metadata. Alternative rejected: adding runtime `MaterialPropertyBlock` to MeshRenderer, because local registry says it breaks SRP Batcher residency. Estimate: 0 us runtime.
- [x] 20. Add resource-node binding without pickup duplication. DOD: `ScavengeTarget` is attached only for loot/resource/salvage/node/deposit or metadata `harvestUnits`; containers and ordinary dropped items stay passive. Alternative rejected: attaching active pickup logic blindly. Estimate: 0 us runtime.

## Loop 5 - Cross-Assembly Compile Blocker / Tasks 21-23

- [x] 21. Investigate Unity console compile blocker. DOD: console showed `DronePrefabFactory.cs` missing attachment metadata/types; this was unrelated to inventory output but inside allowed `Editor/Assembly` compile surface. Alternative rejected: claiming clean compile while console had errors. Estimate: 0 us runtime.
- [x] 22. Repair missing drone attachment contract minimally. DOD: added passive `DroneAttachmentMetadata` using existing `DroneAttachmentRuntimeData`; added missing editor DTO/scratch declarations in `DronePrefabFactory`. Alternative rejected: removing drone attachment calls or changing drone runtime behavior. Estimate: 0 us runtime.
- [x] 23. Validate touched scripts without dotnet spam. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for inventory files, drone factory, and drone attachment metadata; `dotnet` PID 43220 remained active, so full build was not launched. Alternative rejected: violating compile throttle. Estimate: 0 us runtime.

## Loop 6 - Self-Refinement / 1739 Polish

- [x] Preserve first validation failure. DOD: `ValidatePrefab` now uses first-failure retention instead of overwriting root cause with later collider/component checks. Alternative rejected: last-write failure diagnostics. Estimate: 0 us runtime.
- [x] Stop after invalid container metadata bake. DOD: `BuildPrefab` now fails immediately if `BakeContainerMetadata` sets `metric.failure`, before scavenge/collider/save work. Alternative rejected: continuing assembly after invalid lid/IK metadata. Estimate: 0 us runtime.
- [x] Record non-container stack capacity in metrics. DOD: `metric.slotCapacity` is populated from `ItemNodeData` stack capacity before container override. Alternative rejected: leaving ordinary loot metrics underreported. Estimate: 0 us runtime.
- [x] Re-run low-impact validation. DOD: Unity MCP validation returned 0 errors/0 warnings for `ItemNodeData`, `ContainerMetadata`, `InventoryPrefabFactory`, `DroneAttachmentMetadata`, and `DronePrefabFactory`; strict hot-body scan found no `GlobalRegistry.Get`, `GetComponent`, or `TryGetComponent` in hot method bodies; orphan `.meta` scan found none. Unity console currently contains 2 unrelated `GameBootstrapper` runtime errors from main-menu scene activation, not compile diagnostics from touched files. Full `dotnet build` blocked by active `dotnet` PID 43220. Estimate: 0 us runtime.

## Loop 7 - Deep Polish / Factory Contract Hardening

- [x] Harden direct `ItemNodeData.ConfigureEditorBake(ItemData)` usage. DOD: fallback now computes the same stable `LocHash` route used by the factory when `PersistentHashId` is zero. Alternative rejected: writing a zero item hash from editor direct calls. Estimate: 0 us runtime.
- [x] Prevent random lid transform binding. DOD: `ResolveLidTransform` now returns null unless a lid/hatch/door/cover/hinge name scores above zero or metadata gives an exact name. Alternative rejected: binding the first visual child as a fake lid. Estimate: 0 us runtime.
- [x] Sanitize authored collider centers. DOD: Box/Capsule collider centers now fall back to zero if metadata contains NaN/Infinity. Alternative rejected: trusting external JSON coordinates. Estimate: 0 us runtime.
- [x] Cache shader BRG proof per run. DOD: `HasSrpBatcherProof` uses a pre-capacity `Dictionary<Shader,bool>` and clears it after the run, avoiding repeated shader source reads for large item piles. Alternative rejected: re-reading the same shader file per renderer material slot. Estimate: 0 us runtime; editor I/O reduced.
- [x] Re-run validation after hardening. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `ItemNodeData`, `ContainerMetadata`, and `InventoryPrefabFactory`; case-sensitive hot-body scan found no forbidden lookup/alloc tokens; orphan `.meta` scan found none; `git diff --check` clean. Full build blocked by active `dotnet` PID 43220 and CPU load 100. Unity console read was not available because the Unity MCP ping did not answer twice. Estimate: 0 us runtime.

## Loop 8 - Asset Bible Compliance Polish

- [x] Add serialized LOD policy to inventory prefabs. DOD: `InventoryPrefabFactory` now preserves authored `LODGroup` components or bakes a root one-step CrossFade `LODGroup` for renderer hierarchies without one; `ValidatePrefab` rejects renderer prefabs missing LOD policy. Alternative rejected: undocumented LOD exemption for touchable loot/container prefabs. Estimate: 0 us runtime; serialized authoring cost only.
- [x] Add explicit interaction anchors. DOD: loot prefabs receive root-local `ANCHOR_Loot`; containers receive root-local `ANCHOR_Open` aligned to the same baked pivot/axis/forward as `ContainerMetadata`; existing named anchors/`IK_Handle` are normalized instead of duplicated. Alternative rejected: relying on arbitrary source hierarchy child-space anchors. Estimate: 0 us runtime.
- [x] Clean material scoring dead branch. DOD: removed invalid `matquipmentatlas` scoring path and kept the normalized `matequipmentatlas` route for `MAT_Equipment_Atlas`. Alternative rejected: keeping a typo in material selection logic. Estimate: 0 us runtime.
- [x] Re-run validation after LOD/anchor patch. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `InventoryPrefabFactory`, `ItemNodeData`, and `ContainerMetadata`; method-signature hot scan across Inventory/Items/Interaction returned clean; touched-file scan found no LINQ, `.Complete`, or `WaitForCompletion`; console showed only MCP transport warnings; orphan `.meta` scan found none. Full `dotnet build` blocked by active `dotnet` PID 43220 and CPU load 79. Estimate: 0 us runtime.

## Loop 9 - Visual Sync / Emission Binding Polish

- [x] Add direct renderer route for emissive inventory state. DOD: added `InventoryEmissionStatePresenter` under Inventory domain; `InventoryPrefabFactory` attaches it only to assets that require emission state by metadata or locked/sealed/electronic naming and serializes direct `MeshRenderer[]` bindings during bake. Alternative rejected: editing `ScavengeTarget` in World domain or doing runtime renderer discovery. Estimate: 0 us gameplay truth; visual LateFrame only when emission pulse is enabled.
- [x] Keep emission presentation phase-safe and quality-gated. DOD: presenter implements `ILateFrameTickable`; `LateFrameTick` uses a triangle-wave visual fake, continuous `HomeostasisBrain.GlobalQualityWeight`, and pre-created `MaterialPropertyBlock`; low quality disables MPB pulse and unregisters the late-frame lane. Alternative rejected: Tick/FixedUpdate emission updates and trigonometric pulse simulation. Estimate: 0 B steady-state managed allocation in `LateFrameTick` by static body scan.
- [x] Extend factory validation. DOD: final prefab validator rejects multiple `InventoryEmissionStatePresenter` components and rejects presenter with zero renderer bindings; metadata schema accepts numeric `emissionBaseStrength`, `emissionPulseStrength`, `emissionPulseHz`, `emissionMinQuality`. Alternative rejected: string state parsing or material instance clones. Estimate: 0 us runtime authoring route.
- [x] Re-run low-impact validation. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `InventoryEmissionStatePresenter`, `InventoryPrefabFactory`, `ContainerMetadata`, and `ItemNodeData`; Unity console returned 0 error entries; focused `LateFrameTick` hot-body scan found no `new`, component search, LINQ, `GlobalRegistry.Get<T>()`, `.Complete`, `WaitForCompletion`, string formatting, or `.ToString`; `git diff --check` clean. Scoped orphan `.meta` scan for 1739 folders returned `NO_ORPHAN_META_FOUND_1739_SCOPE`; global scan found pre-existing unrelated orphan `.mat.meta` files under `Assets/Shapes` and two unrelated prefab metas, not deleted by 1739. Full `dotnet build` blocked by CPU 100 and active `dotnet` PIDs 43220/49404. Estimate: 0 us runtime claim without player profiler.

## Loop 10 - Existing Prefab Audit / Slot Order Polish

- [x] Preserve authored container slot order. DOD: removed numeric `Array.Sort` from `ContainerMetadata.SanitizeSlotConnectivity` and `InventoryPrefabFactory.ResolveSlotConnectivity`; valid authored permutation order now survives serialization, invalid/missing maps still regenerate `0..N-1`. Alternative rejected: sorting every valid map, because that destroys physical compartment order for multi-slot containers. Estimate: 0 us runtime.
- [x] Reduce emission presenter allocation pressure. DOD: replaced per-instance `MaterialPropertyBlock` field with one shared cold `s_sharedPropertyBlock`; `LateFrameTick` still returns if the block is not cold-created and contains no allocation/search tokens. Alternative rejected: per-prefab MPB allocation on every emission-enabled object enable. Estimate: no steady-state GC by static body scan.
- [x] Add existing item-prefab static audit. DOD: `InventoryPrefabFactory.Run` now audits `Assets/Prefabs/Items`/output folder before source discovery and counts existing prefab MeshColliders, missing `ItemNodeData`, missing primitive colliders, deep hierarchy, and missing emission bindings. Alternative rejected: relying only on newly generated prefab validation while legacy item prefabs remain unchecked. Estimate: editor-only scan.
- [x] Re-run validation. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `InventoryPrefabFactory`, `InventoryEmissionStatePresenter`, and `ContainerMetadata`; `git diff --check` clean; forbidden-token scan on touched files returned no hits; scoped orphan `.meta` scan returned `NO_ORPHAN_META_FOUND_1739_SCOPE`. Unity console now reports unrelated `DroneFleetManager.cs` missing symbols and MCP regex timeout logs; full `dotnet build` blocked by CPU 100 and active `dotnet` PID 43220. Estimate: 0 runtime claim without player profiler.

## Loop 11 - Legacy Prefab Gate Hardening

- [x] Extend existing prefab audit to full asset-package gates. DOD: `AuditExistingPrefab` now counts ParticleSystems, non-primitive colliders, wrong collider layers, missing LOD policy, missing interaction anchors, missing container metadata, and material/BRG/emission violations. Alternative rejected: creating a second scanner or mutating legacy prefabs during audit. Estimate: editor-only scan.
- [x] Reuse the factory material proof route for legacy assets. DOD: `AuditExistingRendererMaterials` calls the same shared-material/SRP-batcher/`_EmissionStrength` validator used for newly assembled prefabs and records material slots audited. Alternative rejected: accepting already-saved prefab materials as trusted. Estimate: 0 us runtime.
- [x] Re-run low-impact validation after audit patch. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `InventoryPrefabFactory`, `InventoryEmissionStatePresenter`, `ContainerMetadata`, and `ItemNodeData`; Unity console returned 0 error entries; runtime touched-file forbidden-token scan returned no hits; scoped orphan `.meta` scan returned `NO_ORPHAN_META_FOUND_1739_SCOPE`; `git diff --check` clean. Full `dotnet build` was not launched because CPU was 69.75 and active `dotnet` PID 43220 existed. Estimate: 0 runtime claim without player profiler.

## Loop 12 - Item Physical DTO Projection

- [x] Route item mass/volume into an existing first-party DTO. DOD: `ItemNodeData` now exposes `TryBuildPhysicalConstants(out ItemPhysicalConstantsDTO)` and validates `ItemPhysicalConstantsDTO` layout with `UnsafeUtility.SizeOf<T>()`. Alternative rejected: inventing a second physical constants struct or leaving volume as a passive-only serialized float. Estimate: 0 us runtime; cold projection only.
- [x] Harden `ItemNodeData.IsValid`. DOD: validity now requires finite positive mass, finite positive volume, nonzero stack, and nonzero item hash. Alternative rejected: relying only on `OnValidate` to clean serialized assets. Estimate: 0 us runtime.
- [x] Gate prefab assembly on physical DTO projection. DOD: `InventoryPrefabFactory.Run` checks the physical constants DTO layout and `BuildPrefab` fails if `ItemNodeData` cannot project to `ItemPhysicalConstantsDTO`. Alternative rejected: allowing prefabs to pass with stack DTO only. Estimate: editor-only gate.
- [x] Validate after DTO patch. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `ItemNodeData` and `InventoryPrefabFactory`; `git diff --check` clean for both files. Estimate: 0 runtime claim without player profiler.

## Loop 13 - Container Metadata Contract Closure

- [x] Harden `ContainerMetadata.IsValid`. DOD: validity now requires finite pivot, finite normalized axis/closed-forward, finite open-angle bounds, finite positive base mass, finite non-negative capacity, finite quality weight, valid slot map, and IK handle. Alternative rejected: assuming `OnValidate` always repaired serialized container assets. Estimate: 0 us runtime.
- [x] Use the full metadata contract in factory validation. DOD: `ValidatePrefab` and `AuditExistingPrefab` now check `metadata.IsValid` before accepting container prefabs. Alternative rejected: partial axis/IK checks that could miss bad mass/capacity/pivot/open range. Estimate: editor-only gate.
- [x] Validate after container contract patch. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `ContainerMetadata` and `InventoryPrefabFactory`; `git diff --check` clean for both files. Estimate: 0 runtime claim without player profiler.

## Loop 14 - SRP Batcher Emission Contract Correction

- [x] Remove runtime MPB writes from standard inventory geometry. DOD: `InventoryEmissionStatePresenter` is now a passive serialized emission binding component; it no longer implements `ILateFrameTickable` or `IGlobalRegistryHotSwapListener`, no longer touches `GlobalRegistry`, and no longer allocates or writes `MaterialPropertyBlock`. Alternative rejected: keeping XML-requested per-renderer MPB pulse, because `AGENTS.md` and `REND_DescriptorBinding_Reality_Check.txt` forbid MPB on standard world geometry. Estimate: 0 us runtime active work.
- [x] Keep cold renderer binding proof. DOD: `InventoryPrefabFactory` still serializes direct `MeshRenderer[]` bindings for emission-capable prefabs and now validates `HasValidBinding` instead of only `RendererCount`, covering finite quality/strength/frequency profile. Alternative rejected: deleting emission binding entirely, because XML 1739 requires renderer references and material/emission audit. Estimate: editor-only gate.
- [x] Validate after SRP correction. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `ItemNodeData`, `ContainerMetadata`, `InventoryEmissionStatePresenter`, and `InventoryPrefabFactory`; Unity console retry returned 0 error entries; runtime Inventory scan returned no `MaterialPropertyBlock`, `SetPropertyBlock`, `LateFrameTick`, `GlobalRegistry`, LINQ, `.Complete`, or `WaitForCompletion` hits; 1739 scope orphan `.meta` scan and trailing-whitespace scan were clean. Full `dotnet build` was not launched because CPU probe reported 100. Estimate: 0 runtime claim without player profiler.

## Loop 15 - Cold Binding / Editor Allocation Closure

- [x] Reject partial or out-of-range emission bindings. DOD: `InventoryEmissionStatePresenter.HasValidBinding` now requires every serialized renderer slot to be non-null, quality gates inside `[0,1]`, non-negative emission strengths, and pulse frequency inside the authoring clamp. Alternative rejected: finite-only validation, because legacy prefabs could pass with negative pulse strength or impossible quality thresholds. Estimate: 0 us runtime active work.
- [x] Remove avoidable editor scratch array churn. DOD: `InventoryPrefabFactory` now uses `s_singleLodScratch` for one-step `LODGroup.SetLODs` and `s_singleFolderSearchScope` through `FindAssetsInFolder` for AssetDatabase folder scopes. Alternative rejected: repeated `new[] { folder }` and per-policy LOD scratch arrays during large batch audits. Estimate: 0 us runtime; editor-only allocation reduction.
- [x] Re-run low-impact validation after Loop 15. DOD: Unity MCP `validate_script` returned 0 errors/0 warnings for `ItemNodeData`, `ContainerMetadata`, and `InventoryEmissionStatePresenter`; `InventoryPrefabFactory` basic validation returned 0 errors/0 warnings while standard validation hit MCP regex timeout on the large file. Static scans on touched files found no `MaterialPropertyBlock`, `SetPropertyBlock`, `LateFrameTick`, `GlobalRegistry`, `GlobalDataVault`, LINQ, `.Complete`, `WaitForCompletion`, or `new[] { ... }`; 1739 scope orphan `.meta` scan and `git diff --check` were clean. Unity console contains one MCP regex timeout entry from the failed standard validator and one unrelated AI compile error in `ShinobuEcosystemBalancer.FlockingAvoidance.cs`. Full `dotnet build` was not launched because CPU was 52.59 and active `dotnet` PID 53256 existed. Estimate: 0 runtime claim without player profiler.
