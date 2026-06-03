# Rationale 1734 - Interactive Equipment Prefab Factory

Status: SOURCE HARDENED - TARGET SCRIPTS VALIDATED, FULL BUILD BLOCKED BY ACTIVE COMPILER PROCESS

## Decisions

### 01 - Scope Boundary
Problem: Equipment assembly prompt requires prefabs in `Assets/Prefabs/Equipment`, while Agent 1715 already writes a generated prefab to `Assets/_Project/Prefabs/Equipment`.
Solution: Build a separate editor-only assembler that consumes generated mesh/metadata/proxy assets and writes the requested `PFB_*` output path.
Rejected Alternatives: Reusing 1715 output directly would skip flat TMP injection, SDF font binding, report metrics, and the required destination path.
Scalability potential: Low uses static mesh + direct TMP labels; middle/high/ultra keep the same truth data while materials/LODs/text density can scale.
Hardware Impact: i3/MX350 avoids runtime authoring scans and Canvas rebuilds; estimated 0.10-0.20 ms saved on equipment-heavy scenes.

### 02 - Text Surface Route
Problem: Equipment labels need readable diegetic text without nested Canvas/CanvasRenderer/RectTransform.
Solution: Inject `TextMeshPro` 3D components directly under the prefab root, orienting each by metadata local position, surface normal, and up vector.
Rejected Alternatives: `TextMeshProUGUI` and render-texture Canvas are higher overhead and violate the one-plane physical UI requirement.
Scalability potential: Low keeps essential labels only; middle adds gauges; high/ultra can add more authored surfaces without changing runtime lookup rules.
Hardware Impact: Removes Canvas rebuild and event-raycast participation. Low-end estimate: 900 us saved during active equipment UI compared to nested world-space Canvas.

### 03 - Anchor Serialization
Problem: FABRIK hand targets need exact grip coordinates without scene search or inferred handles.
Solution: Parse authored anchor records into `InteractionAnchorData[]` and pass them through `EquipmentMetadata.SetEditorBakeData`.
Rejected Alternatives: Adding a new anchor MonoBehaviour per grip would add transform traversal and duplicate ownership.
Scalability potential: Same 64-byte anchor DTO works on every tier; higher tiers can add more active authored anchors within socket capacity.
Hardware Impact: Fixed array copy into native socket DTOs avoids component scans; estimated 20-40 us saved per equipment activation on i3/MX350.

### 04 - Prefab Save Gate
Problem: Failed prefab writes can leave bad assets that downstream agents will trust.
Solution: Use `PrefabUtility.SaveAsPrefabAsset(root, path, out success)`, validate the loaded prefab, and delete on any failure.
Rejected Alternatives: Saving without the `success` gate or relying on console messages is not a proof artifact.
Scalability potential: Offline validation cost scales with asset count, not frame time.
Hardware Impact: Editor-only; runtime gain is indirect by rejecting MeshCollider/Canvas/material-instance mistakes before play.

### 05 - Collision Proxy Enforcement
Problem: Interaction scan cost explodes if high-poly equipment meshes are used as raycast targets.
Solution: Require `COL_` primitive proxy roots or copy only primitive colliders from source prefabs into a `COL_[EquipmentName]` child on `Interactable`.
Rejected Alternatives: MeshCollider fallback and visual mesh colliders were rejected because RaycastCommand batches need cheap primitive math.
Scalability potential: Low/middle/high/ultra all use primitive truth; high tiers spend saved time on visuals, not collision complexity.
Hardware Impact: i3/MX350 avoids mesh-triangle tests; estimated 250-700 us saved in dense interaction scans.

### 06 - Runtime Binding Route
Problem: Runtime UI scripts must not search children for labels or renderers during activation.
Solution: Metadata-declared runtime components are added offline and known TMP/renderer fields are assigned by reflection before prefab save.
Rejected Alternatives: Runtime `GetComponentInChildren`, `GameObject.Find`, and dynamic `AddComponent` were rejected as hot-path allocation/search risks.
Scalability potential: Low keeps minimal text references; high/ultra can bind more fields without changing runtime discovery cost.
Hardware Impact: Low-end activation avoids hierarchy scans; estimated 20 us saved per tool/console activation.

### 07 - BRG/SDF Material Gate
Problem: Text SDF materials may be transparent, while mesh materials must remain shared and batchable.
Solution: Validator requires asset-backed shared materials and shadergraph or `UnityPerMaterial` proof. Transparent queues are allowed only for TMP SDF materials with the same proof.
Rejected Alternatives: Blanket transparent rejection would incorrectly reject SDF text; accepting material instances would break SetPass bounds.
Scalability potential: 50 tools in one room remain bounded by shared equipment atlas and shared SDF font materials.
Hardware Impact: Fewer material state changes; estimated 0.05-0.15 ms saved in equipment-heavy rooms on i3/MX350.

### 08 - Quality Scaling Boundary
Problem: The prompt requires continuous `GlobalQualityWeight` without changing gameplay truth.
Solution: Factory serializes authored quality weight in `EquipmentMetadata`; runtime display systems already scale visual-only cadence, fallback, and resolution via continuous weight.
Rejected Alternatives: Adding low/high binary switches to equipment state would violate authority route and DTO stability.
Scalability potential: Low reduces visual cadence/resolution, middle keeps readable surfaces, high/ultra enable visual overkill without changing anchor/collider truth.
Hardware Impact: No new runtime cost; existing terminal/tool UI can save updates on weak devices while preserving deterministic interaction.

### 09 - Build Guard
Problem: The batch requires a full build only when CPU is under 50 percent and no compiler is active.
Solution: Ran Unity script validation twice with zero diagnostics; skipped `dotnet build` because CPU was 100 percent and active `dotnet` processes were present.
Rejected Alternatives: Launching another build under load would violate the protocol and add compile contention for other agents.
Scalability potential: Validation remains local and repeatable; full build can be run later when host load clears.
Hardware Impact: Avoided saturating the host further; no project runtime impact.

### 10 - Compaction Fence Behavior
Problem: UI updates must not read stale native buffers if DataVault compaction begins mid-frame.
Solution: Factory adds no DataVault access. Existing audited terminal/tool readers check `IsCompactionFenceActive` and back off rather than resolving handles.
Rejected Alternatives: Passing native pointers from assembler-generated UI into jobs was rejected as an ownership violation.
Scalability potential: All quality tiers use the same safe route; visual state can hold previous frame if compaction blocks.
Hardware Impact: No new contention or hidden `.Complete()` calls; frame stability preserved.

### 11 - APEX Source-Proof Hardening
Problem: The old proof route wrote a JSON artifact and left `ToolDiegeticDisplayController` render-texture resource flush in `SlowTick`, outside the visual presentation phase.
Solution: Remove disk JSON emission, keep equipment audit data in memory, move render-texture resource flush into `LateFrameTick`, and gate camera render commits by continuous `GlobalQualityWeight`.
Rejected Alternatives: Report-first completion and binary low/high UI switches were rejected because they do not improve runtime source and violate continuous quality doctrine.
Scalability potential: Low tier renders tool screens at a 6-frame cadence, middle/high interpolate toward faster commits, ultra reaches every-frame presentation without changing tool truth.
Hardware Impact: Low-end i3/MX350 avoids 1-5 unnecessary tool-screen render commits per burst; high-end spends saved budget on visual-overkill material state.

### 12 - Anchor Layout and Text Plane Gate
Problem: Binary equipment metadata can become ambiguous if old 56-byte anchor payloads and current 64-byte `InteractionAnchorData` layouts are read with the same stride, and text can silently drift into nested or non-planar transforms.
Solution: Validate `InteractionAnchorData` with `UnsafeUtility.SizeOf<T>()`, select the binary anchor stride by checking the following surface block, validate `EquipmentMetadata` anchors after prefab save, and reject any `TextMeshPro` that is not a direct root child or has non-finite/non-planar local transform data.
Rejected Alternatives: Blind binary stride assumptions and transform-marker GameObjects were rejected because they hide FABRIK target drift until runtime.
Scalability potential: Low/middle/high/ultra all get the same exact grip coordinates; high tiers can add more authored text density without Canvas or hierarchy search.
Hardware Impact: i3/MX350 avoids runtime anchor correction and text hierarchy traversal; estimated 20-40 us saved per equipment activation and zero Canvas rebuild risk.

### 13 - Interaction Guard Validation Fix
Problem: `EquipmentInteractionHandler.cs` failed Unity script validation on a duplicate `InteractionMutationGuardBit` signature warning, blocking clean proof for the interaction route that consumes equipment sockets.
Solution: Remove the helper method and compute the 32-lane mutation guard mask directly at static field sites and the reset site.
Rejected Alternatives: Ignoring the validator fault was rejected because the handler owns queued interaction mutation guards and must remain proof-clean.
Scalability potential: Same guard lane math protects low/middle/high/ultra interaction queues without adding a new manager or buffer.
Hardware Impact: No runtime allocation change; removes a validation wall while preserving lock-flattened guard release paths.

### 14 - Direct Hand Solver Shell
Problem: `PhysicalHandController.cs` had tiny same-frame `IJob`/Burst shells for somatic IK and finger pose solve, including synchronous `job.Run()` and unsafe pointer output.
Solution: Convert those shells into direct solver structs that reuse existing persistent arrays, keep the same bounded 2-4 iteration quality curve, and remove `IJob`, `BurstCompile`, `job.Run`, `unsafe`, and `stackalloc`.
Rejected Alternatives: Keeping tiny Burst jobs was rejected because dispatcher-owned completion windows are required for jobs; this work is too small and same-frame by design.
Scalability potential: Low uses 2 direct iterations at lower cadence; middle/high interpolate cadence; ultra reaches 4 iterations without changing IK truth or socket DTO layout.
Hardware Impact: i3/MX350 avoids first-use Burst compile/scheduler overhead and pointer shell risk; expected gain is stability and sub-0.1 ms preserved budget, not a new feature.

### 15 - Drone Attachment Compile Contract
Problem: Neighboring `DronePrefabFactory.cs` needed drone attachment metadata, and a duplicate attachment block briefly existed in `DroneBoneMetadata.cs`, causing namespace collisions against the real owner.
Solution: Keep `DroneAttachmentMetadata.cs` as the sole attachment owner and remove all attachment tokens from `DroneBoneMetadata.cs`; both construction metadata sources and `DronePrefabFactory.cs` validate with 0 diagnostics.
Rejected Alternatives: Keeping duplicate contracts or disabling the drone factory was rejected; one fact needs one owner and the existing attachment source is the correct route.
Scalability potential: Low keeps one tool socket and one thruster anchor; middle/high/ultra can serialize more anchors/emission renderers without runtime hierarchy search.
Hardware Impact: Editor-only compile unblock; runtime consumers can copy a compact 96-byte attachment table instead of resolving transforms by name.

### 16 - Attachment Copy Route Deduplication
Problem: `DroneAttachmentMetadata.cs` exposed two public copy names for the same runtime table, creating unnecessary API surface in a shared construction metadata route.
Solution: Remove the unused `CopyDescriptorTableTo` alias and keep `CopyAttachmentTableTo` for both `NativeArray<DroneAttachmentRuntimeData>` and managed array editor validation paths.
Rejected Alternatives: Keeping both names was rejected because one fact needs one route; changing the factory to a third name would add churn without improving ownership.
Scalability potential: Low/middle/high/ultra all copy the same 96-byte DTO table; richer tiers can add more authored anchors without a second table owner.
Hardware Impact: Editor/API hygiene. Runtime path remains a direct bounded copy and avoids name-based transform resolution.

### 17 - Obsolete Tools Performance Monitor Removal
Problem: `Assets/_Project/Scripts/Tools/PerformanceMonitor.cs` duplicated runtime monitoring while the active owner is `Assets/_Project/Scripts/PerformanceMonitor.cs` registered through `GlobalRegistry.RegisterPerformanceMonitorRuntime`.
Solution: Delete the unreferenced tools monitor and its `.meta` after scanning its GUID and fully-qualified type route across source, scenes, prefabs, assets, and project settings.
Rejected Alternatives: Keeping both monitors or patching the unreferenced monitor's debug strings was rejected; duplicated monitoring creates API drift and keeps dead hot-path-looking code alive.
Scalability potential: Low/middle/high/ultra all use the same core monitor truth route; debug/playtest reporting remains a cold route on the core owner.
Hardware Impact: Removes one obsolete MonoBehaviour script/type from the compile/runtime surface. No runtime microsecond claim; benefit is ownership stability and less accidental component attachment.

### 18 - Runtime Component Source Gate
Problem: Metadata-driven runtime script binding could attach a component that still performs `GlobalRegistry.Get<T>()`, hierarchy lookup, runtime `AddComponent`, LINQ, or string formatting inside a high-frequency phase.
Solution: `EquipmentPrefabFactory` now reads the source file of every metadata-requested `MonoBehaviour` and rejects it when forbidden tokens appear inside `Tick`, `FixedTick`, `LateFrameTick`, `Update`, `FixedUpdate`, `LateUpdate`, or `Execute`.
Rejected Alternatives: Trusting metadata or checking only field binding was rejected because prefab assembly would then serialize a clean-looking hierarchy around a dirty runtime component.
Scalability potential: Low/middle/high/ultra all receive the same cold-bound component contract; richer displays can add serialized references without hot discovery.
Hardware Impact: Prevents per-frame hierarchy/registry work from entering assembled prefabs. No microsecond claim without profiler; expected gain is avoiding hidden hot-path regressions on i3/MX350.

### 19 - SDF Fallback Chain Fail-Closed Gate
Problem: Selecting a primary SDF font does not prove the CJK/Arabic fallback chain from Agent 1729 is actually reachable by diegetic TextMeshPro surfaces.
Solution: `EquipmentPrefabFactory` now checks available SDF fonts and fail-closes if Arabic or CJK assets exist outside the selected primary font fallback chain. Dry-run confirmed `NotoSans-Regular SDF.asset` with three fallbacks.
Rejected Alternatives: Mutating font assets during assembly was rejected because Agent 1729 owns font generation; 1734 should validate and bind, not silently rewrite atlas ownership.
Scalability potential: Low/middle/high/ultra share one SDF route; low devices avoid dynamic font fallback misses, high-tier scenes can show localized console labels without runtime atlas work.
Hardware Impact: Prevents runtime font fallback compilation/material churn. No frame claim; it removes a known class of localization-time allocation spikes.

### 20 - Hot Source Scanner Lexical Gate
Problem: The runtime component source gate could miss expression-bodied hot methods and could reject harmless comments or string literals containing forbidden tokens.
Solution: Strip comments/string/char literals before scanning and detect both braced and expression-bodied `Tick`, `FixedTick`, `LateFrameTick`, `Update`, `FixedUpdate`, `LateUpdate`, and `Execute` bodies.
Rejected Alternatives: Trusting string search on raw source was rejected because it creates both false negatives and false positives in prefab component binding.
Scalability potential: Low/middle/high/ultra all receive prefabs whose attached scripts are checked against the same cold-binding law before serialization.
Hardware Impact: Prevents hot lookup/allocation regressions from entering assembled equipment. No profiler microsecond claim; source-level gate removes a failure class.

### 21 - Visual Mesh Identity Gate
Problem: A prefab could pass Canvas/TMP/collider validation while carrying shifted `LOD*` or `DETAIL_*` visual mesh transforms under `VIS_*`, causing grab and panel alignment drift.
Solution: Validate the prefab root transform and every generated visual mesh child transform for zero position, identity rotation, and unit scale while leaving authored text planes and collider proxy offsets on their separate validators.
Rejected Alternatives: Blindly accepting authored visual offsets was rejected because the prompt requires generated detailed meshes to be identity-aligned at assembly time.
Scalability potential: Low/middle/high/ultra use the same mesh truth; higher tiers can add detail meshes without adding transform ambiguity.
Hardware Impact: Prevents runtime compensation and designer-side correction passes. No runtime microsecond claim; the benefit is stable zero-offset prefab truth.

### 22 - Fixed LOD Scratch Assembly
Problem: `BuildLodGroupIfPresent` allocated transient `List<LOD>`, `List<Renderer>`, and `ToArray()` buffers for every assembled equipment prefab.
Solution: Replace the transient containers with fixed static LOD and renderer slots, then feed exact two/three-entry buffers into `LODGroup.SetLODs`; a Unity probe confirmed `SetLODs` copies renderer references before scratch cleanup.
Rejected Alternatives: Leaving editor churn was rejected because the assembler is intended to batch dozens of cockpit panels and tools without avoidable garbage pressure.
Scalability potential: Low/middle/high/ultra prefab variants can carry LOD groups without changing runtime truth or factory allocation shape.
Hardware Impact: Editor-time allocation reduction only. No frame claim; it reduces batch assembly churn and prevents stale scratch references.

### 23 - Low-Quality Decorative MPB Gate
Problem: `ToolDiegeticDisplayController` still wrote all material scalars when state changed, even when low quality should spend only on core text readability.
Solution: Decorative shader values now fade continuously through `_visualOverkill01`; when the weight reaches zero, the controller performs one reset write and then skips per-state MPB updates while continuing TMP `SetCharArray` text refresh in `LateFrameTick`.
Rejected Alternatives: A hard low/high switch was rejected; the existing continuous quality curve remains the scalar that controls visual cost.
Scalability potential: Low keeps text-only status; middle gradually restores decorative shader feedback; high/ultra receive full material expressiveness without altering equipment truth.
Hardware Impact: On i3/MX350 this avoids repeated `GetPropertyBlock`/`SetPropertyBlock` work for decorative CRT/fault/hue values in low-quality mode. Exact microseconds require profiler capture.

### 24 - Text Plane and Material Resolve Gate
Problem: The assembler could accept text metadata whose normal/up vectors were degenerate or non-orthogonal after binary import, and `MaterialPalette.ResolveMaterial` could return a random zero-score material if no equipment/material-name match existed.
Solution: Add authoring-time text surface validation, saved-prefab TMP plane transform validation, bounded text extents, and a minimum material resolve score.
Rejected Alternatives: Silently orthonormalizing every bad text surface or accepting the first shared material was rejected because it hides Wave 2 metadata drift and can put panels on the wrong atlas.
Scalability potential: Low/middle/high/ultra all keep identical physical text plane truth; high/ultra can add richer labels while still requiring exact authored planes and shared equipment materials.
Hardware Impact: Editor/offline gate. Runtime gain is preventing wrong-material SetPass drift and avoiding late transform/material correction on i3/MX350.

### 25 - FABRIK Socket Normal Correction
Problem: `EquipmentMetadata.CopyAnchorsToSockets` wrote `LocalUp` into `VRInteractionSocketDTO.Normal`, while `VRInteractionKinematicBridge` uses `socket.Normal` as the resolved snapped hand surface normal.
Solution: Write rotated `LocalForward` into `Normal` and keep `LocalUp` only as the orientation up vector for `quaternion.LookRotationSafe`.
Rejected Alternatives: Leaving `Normal = up` was rejected because it can rotate/telemetry-align the hand against the panel's tangent instead of the authored grip normal.
Scalability potential: Low/middle/high/ultra all use the same exact socket target and normal; richer tiers can add hand polish without changing anchor truth.
Hardware Impact: No allocation or frame-cost change. Prevents runtime correction and wrong-surface snap behavior on all devices.

### 26 - Socket Write Guard Flattening
Problem: `VRInteractionKinematicBridge.TryReplaceSocketRange` acquired the socket mutation guard and then called `TryResolveExisting`, which resolved hand state, previous hand state, controller matrix, socket, tuning, telemetry, cursor, and output matrix lanes even though the operation only writes equipment sockets.
Solution: Resolve only `InteractionSocketsBuffer` under the mutation guard, copy the precomputed `VRInteractionSocketDTO` range, clear the requested unused slots, and release the same guard in `finally`.
Rejected Alternatives: Keeping the broad `TryResolveExisting` route was rejected because it expands the guarded critical section and increases the number of vault lanes touched by an equipment-only write. Adding a new manager or duplicate socket table was rejected because the existing bridge already owns socket truth.
Scalability potential: Low/middle/high/ultra all publish the same socket DTOs; richer tiers can add more authored sockets within capacity without multiplying vault lane resolves.
Hardware Impact: Reduces guarded vault work during equipment socket publication. No profiler microsecond claim; the concrete gain is smaller deadlock surface and less critical-section work on i3/MX350.

### 27 - Runtime Equipment Socket Publication Route
Problem: Prefab assembly serialized exact `InteractionAnchorData`, but no runtime owner published those anchors into the FABRIK socket lane. Leaving this implicit would force future scene search, duplicate managers, or hand-side correction.
Solution: Extend existing `EquipmentInteractionHandler` with cold slot reservation and a fixed managed `VRInteractionSocketDTO[128]` scratch buffer. `EquipmentMetadata` now publishes active anchors only through lifecycle callbacks and unregisters them on disable/destroy. `VRInteractionKinematicBridgeVault` exposes a managed-array socket replacement overload routed through one shared guarded core.
Rejected Alternatives: A new socket publisher manager was rejected because socket truth already belongs to the VR interaction bridge and interaction handler. A persistent `NativeArray` scratch field in `EquipmentInteractionHandler` was rejected because runtime MonoBehaviours must not retain native aliases outside vault scope.
Scalability potential: Low/middle/high/ultra all get the same serialized grip truth and slot capacity. Higher tiers can author more valid sockets within the existing bridge capacity without changing DTO layout, save identity, or hand authority.
Hardware Impact: Avoids runtime hierarchy scans and avoids broad vault lane resolution when equipment appears. No profiler microsecond claim; low-end gain is bounded cold activation work and smaller guarded write sections on i3/MX350.

### 28 - Runtime Socket Lifecycle Rebind
Problem: `EquipmentMetadata.OnEnable` could run before `EquipmentInteractionHandler` or `DataVault` readiness. That made socket publication fail once and never retry; `DataVault` replacement also released handler descriptors without repopulating the already-owned equipment socket ranges.
Solution: Add a bounded static `EquipmentMetadata[128]` pending publication lane, flush it from cold handler lifecycle/service/rebind paths, add an 8-publication `LateFrameTick` retry for transient compaction-fence delays, and republish existing handler-owned contiguous slot ranges after `DataVault` rebind. Socket DTO preparation stays outside the bridge mutation guard, and the bridge still owns the only socket-lane guarded copy.
Rejected Alternatives: A scene-wide `FindObjectsByType<EquipmentMetadata>()` bootstrap was rejected because it allocates and searches the scene. A new socket publisher manager was rejected because the existing handler and bridge already own interaction socket truth.
Scalability potential: Low/middle/high/ultra all use the same deterministic socket slot truth. Low devices pay a max 8 pending publications per late frame only when pending work exists; high/ultra can keep denser authored panels within the same bridge capacity.
Hardware Impact: Prevents missing grip targets after bootstrap/rebind without scene search or unbounded retry work. No profiler microsecond claim; the source-level gain is avoiding future per-frame lookup or manual recovery logic on i3/MX350.

### 29 - Runtime Socket Stale-Clear Fence
Problem: Disabling equipment during `DataVault` compaction released the handler owner slots before the socket lane could be cleared. That could leave stale active `VRInteractionSocketDTO` targets in the vault until another write happened.
Solution: Add a fixed `bool[128]` pending-clear lane owned by `EquipmentInteractionHandler`. Unregister now clears immediately or marks the free slots for bounded late-frame clearing; pending clears never touch slots reoccupied by a successful new owner write.
Rejected Alternatives: Keeping owner slots reserved until clear succeeds was rejected because it can permanently block new equipment registration during a compaction fence. A managed list of clear ranges was rejected because one boolean per bridge slot is bounded and cannot grow.
Scalability potential: Low/middle/high/ultra all preserve exact socket truth. Low pays at most 8 clear ranges per frame only when compaction delayed an unregister; high/ultra keep the same lane and capacity without changing DTO layout.
Hardware Impact: Prevents stale FABRIK targets after disable/unregister without scene search or hot registry polling. No profiler microsecond claim; the source-level gain is removing a latent stale-target recovery path on i3/MX350.

### 30 - Runtime Partial Source Stream Scan
Problem: The metadata runtime component validator expanded a scratch list with every `MonoScript` path under `Assets` before filtering possible partial class sources. In a multi-agent project this can grow to thousands of paths during editor validation.
Solution: Check the primary component source directly, then stream every other script file only long enough to test whether it is a possible partial declaration for the component type and whether it contains a forbidden hot invocation.
Rejected Alternatives: Keeping `s_RuntimeSourcePathScratch` was rejected because it stores non-candidate scripts and can grow with unrelated project source. A full Roslyn project parse was rejected here because the Unity editor validator needs a bounded, cheap source gate, not a second compile pipeline.
Scalability potential: Low/middle/high/ultra equipment variants all receive the same cold-bound component contract; dense cockpit batches no longer pay a scratch-list growth path while validating runtime components.
Hardware Impact: Editor-time allocation/work reduction only. It prevents full-project script path retention during prefab assembly on i3/MX350-class machines; no runtime frame microseconds are claimed.

### 31 - Runtime Script Source Proof Fail-Closed Gate
Problem: `ValidateRuntimeComponentSource` returned success when `MonoScript.FromMonoBehaviour`, the source asset path, or the `.cs` file was unavailable. That allowed metadata-bound runtime scripts to bypass the hot-path source proof entirely.
Solution: Fail-close only for metadata-bound `MonoBehaviour` runtime components when source proof cannot be resolved; keep non-`MonoBehaviour` built-in components outside this source proof rule.
Rejected Alternatives: Failing every non-`MonoBehaviour` component was rejected because built-in Unity components do not carry project source and are not runtime script logic. Leaving scriptless `MonoBehaviour` components accepted was rejected because it makes the source gate unverifiable.
Scalability potential: Low/middle/high/ultra prefab batches all reject unverifiable runtime scripts before serialization; richer cockpit panels can still bind more authored script references if their source passes the same gate.
Hardware Impact: Editor-time gate. Prevents unverifiable hot polling/allocation scripts from entering equipment prefabs; no runtime microsecond number is claimed without profiler capture.

### 32 - Scale-Aware Equipment Socket Targets
Problem: Runtime socket publication used `rootAUP + rotate(anchor.LocalPosition)`, ignoring the equipment root scale. Any scene-scaled tool or console would publish FABRIK targets at the wrong physical location.
Solution: Pass the equipment root `localToWorldMatrix` and root runtime position into `EquipmentMetadata.CopyAnchorsToSockets`; calculate socket offset from the transformed local point and calculate forward/up through the matrix linear basis before normalization.
Rejected Alternatives: Forcing all scene instances to scale 1 was rejected because the runtime socket path should preserve authored physical truth under valid Unity transforms. Using `TransformPoint` per anchor inside metadata was rejected because metadata should remain math/data based and not hold a `Transform` dependency in the copy loop.
Scalability potential: Low/middle/high/ultra all get the same exact socket target math; high-tier cockpit layouts can scale panel assemblies without hand IK drift or runtime correction scripts.
Hardware Impact: Adds a small matrix-vector multiply per active anchor only during cold publication/rebind, not every frame. Prevents per-frame IK compensation or designer-authored correction components on i3/MX350-class devices.
