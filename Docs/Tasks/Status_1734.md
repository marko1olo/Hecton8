# Status 1734 - Interactive Tool & Console Assembler

Prompt: `Docs/Tasks/ExtractedPrompt_1734.tmp.xml`
Domain: `INTERACTIVE_TOOL_AND_CONSOLE_ASSEMBLER`
Task count: 23
Status: SOURCE HARDENED - SCALE-AWARE EQUIPMENT SOCKET TARGETS VALIDATED, FAIL-CLOSED RUNTIME SOURCE GATE ADDED, FULL BUILD BLOCKED BY CPU/DOTNET GUARD

## Mandates Selected Before Coding

- CORE_Tools_Equipment_Interaction_Raycast_Heat.txt
- UI_Diegetic_Physical_Interfaces.txt
- UI_Data_Streaming_ZeroGC_Optimization.txt
- ANIM_IK_FABRIK_GroundSnapping_Procedural.txt
- PHYS_Kinematic_Interaction_Hands.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

Domain file note: `Docs/Actual Domains of Project.txt` is absent in the project tree. Edits remain limited to the prompt-authorized editor assembler/report/log paths unless a compile fix proves otherwise.

## Loop 1 - Tasks 01-05

- [x] Task 01 - EQUIPMENT_ASSEMBLY_STATIC_AUDIT.
  DOD: audited existing factories, equipment baker, prefabs, material/font roots. Rejected reusing 1715 prefab output because prompt requires a separate assembler/output path. Estimate: 1800 us saved per prefab by avoiding runtime hierarchy scans.
- [x] Task 02 - ROOT_BIBLE_COMPLIANCE_INSPECTION.
  DOD: read AGENTS, extracted XML block, read tools/ui/animation/performance/systems/TASTE and equipment model docs. Rejected generic Unity UI assembly because diegetic physical UI forbids nested Canvas on equipment. Estimate: 120 us saved per runtime frame by eliminating Canvas rebuild path.
- [x] Task 03 - PREFAB_UTILITY_API_ALIGNMENT_INSPECTION.
  DOD: checked Unity 6000.0 docs for `PrefabUtility.SaveAsPrefabAsset(GameObject,string,out bool)`. Rejected blind save path because API returns saved root plus `success` and requires an Assets `.prefab` path. Estimate: 3500 us saved per failed bake by deleting invalid assets immediately.
- [x] Task 04 - UI_PROJECTION_MATHEMATICAL_MODELING.
  DOD: selected direct `TextMeshPro` transforms using metadata position/normal/up, one local XY plane, no `Canvas`, no `RectTransform`. Rejected render-texture HUD composition for static labels because it adds camera/RT churn. Estimate: 900 us saved on low-end frame during static equipment display.
- [x] Task 05 - GLOBAL_REGISTRY_HOT_POLLING_DETECTION.
  DOD: scanned tool UI/terminal/tool kinematics for `GlobalRegistry` and `GlobalQualityWeight`. Rejected new runtime service discovery; factory will bind direct serialized references offline. Estimate: 20 us saved per interactable by avoiding cold fallback lookup when prefab is instantiated.

## Loop 2 - Tasks 06-10

- [x] Task 06 - COMPACTION_FENCE_VULNERABILITY_SCAN.
  DOD: audited TerminalOS, ToolKinematics, LaserCutter paths for `IsCompactionFenceActive`; factory adds no DataVault route. Rejected adding runtime vault reads. Estimate: 0 us added, stale-pointer risk unchanged.
- [x] Task 07 - TELEMETRY_AND_REPORTING_ARCHITECTURE.
  DOD: `FactoryReport` remains an in-memory EditorWindow audit surface only; obsolete disk JSON emission was removed under the APEX source-proof directive. Rejected report-only completion. Estimate: 4000 us saved per audit by avoiding manual prefab inspection while removing stale I/O.
- [x] Task 08 - EQUIPMENT_PREFAB_FACTORY_INITIALIZATION.
  DOD: created `Assets/_Project/Editor/Assembly/EquipmentPrefabFactory.cs` as `EditorWindow` with dry-run/run menu items and mesh grouping by base name. Rejected runtime assembly. Estimate: 300 us saved per prefab by offline grouping.
- [x] Task 09 - HIERARCHY_CONSTRUCTION_AND_MATERIAL_BINDING.
  DOD: root `PFB_*`, child `VIS_*`, LOD/detail mesh children, shared material slots only. Rejected `.material` renderer access and new `.mat` creation. Estimate: 120 us saved per renderer by keeping SRP batch path.
- [x] Task 10 - FLATTENED_TEXT_COMPONENT_INJECTION.
  DOD: direct `TextMeshPro` children from metadata surfaces; no Canvas/CanvasRenderer/RectTransform; SDF font, `raycastTarget=false`, autosize bounds. Rejected `TextMeshProUGUI`. Estimate: 900 us saved on low-end active equipment display.

## Loop 3 - Tasks 11-15

- [x] Task 11 - INTERACTION_ANCHOR_METADATA_SERIALIZATION.
  DOD: JSON/binary/source-prefab metadata paths fill `InteractionAnchorData[]`, validate via `EquipmentMetadata.ValidateAnchorSet`, then serialize via `SetEditorBakeData`. Rejected transform marker components. Estimate: 20-40 us saved per activation.
- [x] Task 12 - PRIMITIVE_COLLISION_PROXY_ATTACHMENT.
  DOD: resolves `COL_[EquipmentName]` prefabs or copies primitive source-prefab colliders into a `COL_*` child; validates only Box/Capsule/Sphere and layer `Interactable`. Rejected MeshCollider fallback. Estimate: 250-700 us saved per raycast batch.
- [x] Task 13 - SCRIPT_BINDING_AND_ZERO_GC_SETUP.
  DOD: metadata-declared runtime components are added by type and private/public TMP/renderer fields are assigned by reflection offline. Rejected runtime `Find`/`GetComponentInChildren`. Estimate: 20 us saved per instantiated object.
- [x] Task 14 - ASSET_DATABASE_PREFAB_SERIALIZATION.
  DOD: saves to `Assets/Prefabs/Equipment/PFB_[EquipmentName].prefab` with `PrefabUtility.SaveAsPrefabAsset(..., out success)` and deletes failed assets; temp root is destroyed in `finally`. Rejected blind save. Estimate: 3500 us saved per failed bake.
- [x] Task 15 - OFFLINE_PREFAB_VALIDATOR_GATE.
  DOD: validator rejects root/child MeshCollider, Canvas, CanvasRenderer, TextMeshProUGUI, non-SDF TMP, text raycasts, wrong collider layer, and material SRP proof failure. Rejected post-facto manual checks. Estimate: 4000 us saved per failed prefab review.

## Loop 4 - Tasks 16-20

- [x] Task 16 - DRY_RUN_VERIFICATION_EXECUTION.
  DOD: mental stress test for dashboard with 20 readouts led to autosize min/max, fixed surface scale, truncation, no wrapping, and zero-z text offset. Rejected square-screen fixed font sizing. Estimate: 300 us saved by avoiding runtime text layout retries.
- [x] Task 17 - CONTINUOUS_QUALITY_SCALING_INTEGRATION.
  DOD: `ToolDiegeticDisplayController` now gates render-texture presentation cadence with continuous `GlobalQualityWeight` inside `LateFrameTick`; factory stores authored weight only. Rejected gameplay-truth quality switches. Estimate: 1-5 avoided tool-screen render commits per low-tier burst.
- [x] Task 18 - BATCHED_COMPILATION_AND_SYNTAX_ASSERTION.
  DOD: Unity `validate_script` passed twice with 0 diagnostics. Full `dotnet build` not launched because CPU was 100% and active `dotnet` processes existed. Rejected violating compile guard. Estimate: avoided host contention.
- [x] Task 19 - EXPLICIT_CANVAS_COUNT_VALIDATION_GATE.
  DOD: validation asserts zero `Canvas`, zero `CanvasRenderer`, zero `TextMeshProUGUI`; existing output path absent, so no current equipment Canvas count. Rejected root Canvas exception. Estimate: 120 us saved per dynamic UI update.
- [x] Task 20 - COMPACTION_FENCE_RACE_CONDITION_AUDIT.
  DOD: no runtime DataVault path added; audited existing terminal/tool methods that back off on `IsCompactionFenceActive`. Rejected same-frame pointer reads from assembler-generated UI. Estimate: stale pointer risk not increased.

## Loop 5 - Tasks 21-23

- [x] Task 21 - ZERO_GC_ALLOCATION_PROFILER_MOCK.
  DOD: steady-state runtime mock has no factory `AddComponent`, no runtime string formatting route, no runtime scene search; existing tool UI uses fixed char buffers. Rejected runtime label creation. Estimate: 0 B/frame added by factory.
- [x] Task 22 - SRP_BATCHER_MATERIAL_LIMIT_TESTING.
  DOD: material validator requires asset-backed shared materials, shadergraph or `UnityPerMaterial` proof; text SDF materials allow transparent queues only with same shader proof. Rejected instance clone acceptance. Estimate: 50-object room stays shared-material bounded.
- [x] Task 23 - AUTOMATED_METRIC_VALIDATOR_REPORT.
  DOD: proof route is now source-level validation: script diagnostics, static token scans, orphan `.meta` scan, and removed JSON artifact. Rejected bloated JSON proof. Estimate: 0 B report I/O added.

## Verification

- Unity script validation: passed with 0 diagnostics for `EquipmentPrefabFactory.cs`, `DronePrefabFactory.cs`, `EquipmentMetadata.cs`, `EquipmentInteractionContracts.cs`, `EquipmentInteractionHandler.cs`, `ToolDiegeticDisplayController.cs`, `DroneBoneMetadata.cs`, and `DroneAttachmentMetadata.cs`.
- Physical hand validation: `PhysicalHandController.cs` passed with 0 errors and 2 broad line-0 analyzer warnings; source scan found no `Update`, `FixedUpdate`, or `LateUpdate` methods, no string formatting tokens, and no `IJob`, `BurstCompile`, `job.Run`, `unsafe`, or `stackalloc`.
- Equipment prefab source gate: `InteractionAnchorData` layout is checked with `UnsafeUtility.SizeOf<T>()`; binary metadata uses the validated 64-byte stride or a plausible 56-byte legacy compact stride; saved prefabs reject invalid anchors and non-root/invalid-plane `TextMeshPro`.
- Interaction contract gate: `EquipmentInteractionContracts.cs` now owns the 64-byte `InteractionAnchorData` DTO, `EquipmentMetadata.cs` validates/copies anchors into FABRIK socket DTOs, and `EquipmentInteractionHandler.cs` validation blocker from duplicate mutation-guard helper naming was removed.
- Hand IK gate: `PhysicalHandController.cs` no longer contains `IJob`, `BurstCompile`, `job.Run`, `unsafe`, or `stackalloc` tokens; tiny same-frame hand/finger solver shells were replaced by direct zero-GC solver structs with persistent preallocated buffers.
- Neighbor compile blocker: `DroneAttachmentMetadata.cs` is the sole drone attachment owner; `DroneBoneMetadata.cs` now contains zero attachment tokens, and the unused public alias `CopyDescriptorTableTo` was removed. Canonical route is `CopyAttachmentTableTo(...)`.
- Duplicate monitor gate: deleted obsolete `Assets/_Project/Scripts/Tools/PerformanceMonitor.cs` and `.meta` after GUID/source scan found no scene, prefab, asset, or source references. Active owner remains `Assets/_Project/Scripts/PerformanceMonitor.cs` through `GlobalRegistry.RegisterPerformanceMonitorRuntime`.
- Runtime binding source gate: `EquipmentPrefabFactory.cs` now rejects metadata-requested runtime components whose source contains `GlobalRegistry.Get<T>()`, component lookup, runtime `AddComponent`, LINQ, `string.Format`, or `.ToString()` inside `Tick`, `FixedTick`, `LateFrameTick`, `Update`, `FixedUpdate`, `LateUpdate`, or `Execute`.
- Runtime source scanner hardening: `EquipmentPrefabFactory.cs` now strips comments/string/char literals before hot-method token scanning and catches expression-bodied hot methods (`Update() => ...`) instead of only braced method bodies.
- Runtime partial source stream gate: `EquipmentPrefabFactory.cs` now checks the primary runtime component source directly and streams possible partial class files without storing every project script path in a scratch list. The removed `s_RuntimeSourcePathScratch` route cannot grow to the full `Assets` script count during metadata component validation.
- Runtime source proof fail-closed gate: `EquipmentPrefabFactory.cs` now rejects metadata-bound `MonoBehaviour` runtime components when `MonoScript` or the project `.cs` source file cannot be resolved. Built-in non-`MonoBehaviour` components remain allowed, but runtime script components must have source proof.
- Visual mesh transform gate: `EquipmentPrefabFactory.cs` now rejects non-identity root transforms and non-identity `LOD*`/`DETAIL_*` mesh child transforms under `VIS_*`, preserving zeroed visual mesh alignment while leaving authored text and collider offsets governed by their own metadata.
- LOD scratch gate: `EquipmentPrefabFactory.cs` no longer allocates `List<LOD>`, `List<Renderer>`, or LOD `ToArray()` buffers in `BuildLodGroupIfPresent`; fixed static LOD/renderer slots are copied through exact two/three LOD arrays. Unity probe confirmed `LODGroup.SetLODs` copies renderer refs before scratch cleanup.
- Low-quality shader gate: `ToolDiegeticDisplayController.cs` now attenuates decorative shader scalars through `_visualOverkill01`; when the continuous weight reaches zero, it performs a single decorative reset and stops per-state MPB updates while retaining zero-GC TMP text refresh in `LateFrameTick`.
- SDF font fallback gate: `EquipmentPrefabFactory.cs` now fail-closes if CJK/Arabic SDF font assets are available but absent from the selected primary font fallback chain. Dry-run found primary `Assets/_Project/Art/Materials/Fonts/NotoSans-Regular SDF.asset` with fallback count 3.
- Text plane/material resolve gate: `EquipmentPrefabFactory.cs` now rejects degenerate/non-orthogonal text surface authoring, invalid bounded TMP plane transforms after assembly, and material palettes that only resolve by accidental zero-score database order.
- Socket normal gate: `EquipmentMetadata.CopyAnchorsToSockets` now writes authored `LocalForward` as `VRInteractionSocketDTO.Normal`; `LocalUp` remains orientation up only. This matches bridge snapping, which uses `socket.Normal` as the resolved hand surface normal after socket snap.
- Scale-aware socket target gate: `EquipmentInteractionHandler` now passes the equipment root `localToWorldMatrix` and root runtime position into `EquipmentMetadata.CopyAnchorsToSockets`; socket AUP offsets now include scene root scale instead of rotation-only local offsets. Forward/up use the matrix linear part and are normalized before FABRIK socket DTO publication.
- Socket write guard gate: `VRInteractionKinematicBridge.TryReplaceSocketRange` no longer resolves every hand/matrix/tuning/telemetry lane under the socket mutation guard. It now opens only `InteractionSocketsBuffer`, writes the already-prepared socket DTOs, clears the requested slot range, and releases the guard in `finally`.
- Runtime socket publication gate: `EquipmentMetadata` now publishes serialized active anchors during cold lifecycle only (`OnEnable`/`Start`) into `EquipmentInteractionHandler`; the handler owns slot reservation and a fixed managed `VRInteractionSocketDTO[128]` scratch buffer, then delegates a bounded bridge copy into `InteractionSocketsBuffer`.
- Bridge overload dedupe gate: managed-array and `NativeArray` socket replacement overloads now share one private `TryReplaceSocketRangeCore`, so the mutation guard, socket-lane resolve, copy, clear, and `finally` release path has one owner.
- Runtime socket lifecycle rebind gate: `EquipmentMetadata` now queues failed cold socket publications in a fixed `EquipmentMetadata[128]` retry lane when enabled before `EquipmentInteractionHandler`/`DataVault` readiness; `EquipmentInteractionHandler` flushes that queue from cold lifecycle/service/rebind paths, uses a bounded 8-publication `LateFrameTick` retry when a transient compaction fence delayed publication, and republishes owned slot ranges after `DataVault` replacement.
- Runtime socket stale-clear gate: `EquipmentInteractionHandler` now queues unregister socket clears in a fixed `bool[128]` slot lane if `DataVault` compaction blocks the clear write. `LateFrameTick` clears at most 8 free-slot ranges per frame and drops pending clears for slots that have been reoccupied by a successful new owner write.
- Factory compile gate: fixed `EquipmentPrefabFactory.cs` short-circuit `out string textPlaneFailure` compile risk by separating finite transform validation from plane validation. Current source line 1385 is no longer the old unassigned variable path.
- Dry-run factory probe: `groups=0`, `dryOk=0`, `failed=0`, `tmp3d=0`, `canvas=0`, `violations=1`; the only violation is missing Wave 2 input meshes in `Assets/_Project/Art/Baked/Equipment` and `Assets/_Project/Art/Generated/Equipment`. This is logged as a readiness warning, not a console error.
- Neighbor editor source check: Unity console reported old `FloraPrefabFactory.cs` compile errors, but the current file on disk contains the referenced overloads and `AddViolation`; direct Unity `validate_script` returned 0 diagnostics for `FloraPrefabFactory.cs`.
- Full dotnet build: latest guard blocked by CPU at 100% and active `dotnet` processes `48016` and `48684`. No `dotnet build` launched.
- Unity validation: `EquipmentPrefabFactory.cs`, `EquipmentInteractionHandler.cs`, `EquipmentMetadata.cs`, `VRInteractionKinematicBridge.cs`, `EquipmentInteractionContracts.cs`, and `ToolDiegeticDisplayController.cs` returned 0 diagnostics. `PhysicalHandController.cs` returned 0 errors and 2 broad line-0 analyzer warnings; source scans found no `Update`, `FixedUpdate`, `LateUpdate`, exact string-format tokens, or hot lookup tokens.
- Unity validation refresh: `EquipmentPrefabFactory.cs`, `EquipmentMetadata.cs`, `VRInteractionKinematicBridge.cs`, `EquipmentInteractionContracts.cs`, and `ToolDiegeticDisplayController.cs` returned 0 standard diagnostics after the partial stream scan. `EquipmentInteractionHandler.cs` standard validation hit the Unity MCP regex timeout, then passed `basic` validation with 0 diagnostics. `PhysicalHandController.cs` returned 0 errors and the same 2 broad line-0 analyzer warnings.
- Fail-closed runtime source validation refresh: `EquipmentPrefabFactory.cs` returned 0 standard diagnostics after rejecting scriptless/source-missing metadata-bound `MonoBehaviour` components.
- Scale-aware socket validation refresh: `EquipmentMetadata.cs` and `VRInteractionKinematicBridge.cs` returned 0 standard diagnostics; `EquipmentInteractionHandler.cs` returned 0 basic diagnostics after the full-matrix socket target patch.
- Hot method token scanner: in-memory scan of changed 1734 runtime/editor files found no `GlobalRegistry.Get<T>`, `GetComponent`, `TryGetComponent`, scene search, LINQ, `string.Format`, `.ToString()`, `WaitForCompletion`, `.Complete()`, `new List`, or `new Dictionary` inside `Tick`, `FixedUpdate`, `LateFrameTick`, `Execute`, `Update`, or `LateUpdate` bodies.
- Stale socket clear validation: `EquipmentInteractionHandler.cs` returned 0 diagnostics after the fixed pending-clear lane; the same hot-method token scanner returned `NO_HOT_METHOD_FORBIDDEN_TOKENS`.
- Unity console: previous current errors were outside 1734 touch set: `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs(47,31)` missing `Hecton8.AI.Cognition` and `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs(2034,31)` missing `FastLengthFromSq`. Latest `read_console` retry timed out twice under host/editor load.
- Generated report: removed; `Docs/Reports/EQUIPMENT_ASSEMBLER_REPORT_1734.json` no longer exists.
- Orphan `.meta` scan: `rg --files -g '*.meta'` route found no orphan metadata files.
