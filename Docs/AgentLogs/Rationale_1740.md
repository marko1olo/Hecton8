# Rationale 1740

## Prompt Recovery Defect

Problem: `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="1740">`; XML prompt extraction returned no match and `rg` shows a gap from 1737 to 1741.
Solution: Treat user brief as the primary bounded assignment: `PowerGridPrefabFactory.cs` for reactors, nuclear batteries/RTGs, and relays.
Rejected Alternatives: Editing neighboring prompt 1737 or 1741 would contaminate another domain. Inventing a 30-task XML block would be false reporting.
Scalability potential: Low keeps serialized power metadata and simple emissive proxy. Middle/High/Ultra can add richer visual diagnostics through shared materials and MPB values without changing power truth.
Hardware Impact: Avoids runtime search/material allocation. Estimated i3/MX350 gain is 20-100 us per scene load and 0 B/frame by moving setup to editor-time serialized assets. STATIC ESTIMATE, PENDING PROFILER.

## Domain Boundary

Problem: Power prefab assembly touches authoring, logistics, rendering, and interaction anchors but must not become a second power owner.
Solution: Keep gameplay truth in existing `PowerGridManager`/power domain. Factory writes serialized metadata and prefab components for cold integration only.
Rejected Alternatives: New runtime manager, new GlobalRegistry slot, or direct hot polling of prefab components.
Scalability potential: Low/Middle/High/Ultra differ only in visual proxy richness and LOD/material detail, not base watt truth or graph identity.
Hardware Impact: Editor-time assembly prevents runtime hierarchy repair and material lookup. Estimated compact gain: 10-60 us during module spawn/load; frame steady-state remains unchanged except MPB visual sync by existing owner.

## Prompt Recovery Update

Problem: User inserted `<AGENT_PROMPT id="1740">` into `Docs/Tasks/CURRENT_BATCH.md` after the fallback pass; stale checklist had an incorrect 4-task count.
Solution: Re-extracted the XML with PowerShell regex and replaced the fallback checklist with the 23 prompt tasks. Existing evidence remains valid for tasks 01, 02, 05, and 07.
Rejected Alternatives: Continuing from the fallback brief would under-report scope. Restarting discovery from zero would waste completed, disk-backed evidence.
Scalability potential: Correct scope preserves the full Low/Middle/High/Ultra prefab path: primitive collision at all tiers, richer emissive/BRG visuals only where quality budget allows.
Hardware Impact: No runtime impact. Prevents missing gates that would otherwise allow MeshCollider or material-instance regressions into low-end builds.

## Editor-Only Prefab Save Route

Problem: Prefab assembly can leave temporary GameObjects and colliders in the editor scene if `PrefabUtility.SaveAsPrefabAsset` fails midway.
Solution: `PowerGridPrefabFactory` creates one root per group, validates it, calls `PrefabUtility.SaveAsPrefabAsset(root, path, out success)`, deletes corrupt prefabs on failure, and always destroys the temporary root in `finally`.
Rejected Alternatives: Scene-resident staging objects or runtime `AddComponent` repair would leave ghosts and violate offline-only construction.
Scalability potential: Low/Middle/High/Ultra all load the same serialized prefab; higher tiers only use richer MPB visual sync.
Hardware Impact: Moves hierarchy construction and collider stripping out of player runtime. Estimated low-end gain: 40-180 us per spawned infrastructure object and 0 B/frame steady state. STATIC ESTIMATE.

## MeshCollider Rejection

Problem: The XML contains a permissive convex `MeshCollider` proxy clause and a later hard `MeshCollider == 0` validation clause.
Solution: Enforce the stricter final gate: visual source colliders are stripped, collision proxies strip every `MeshCollider`, and fallback proxies generate only `BoxCollider` or `CapsuleCollider`.
Rejected Alternatives: Accepting convex MeshColliders would satisfy one task but fail task 19 and risk high-poly physics cost.
Scalability potential: Toaster uses one primitive proxy; middle/high/ultra can use multiple primitive proxy assets, still zero MeshCollider.
Hardware Impact: Avoids expensive mesh collision broadphase/narrowphase. Estimated MX350 savings: 15-90 us per dense base interaction sweep, depending relay count. STATIC ESTIMATE.

## Serialized Visual Sync

Problem: Power status glow must pulse without material clones or graph-solver quality switches.
Solution: `PowerStatusEmissiveBinding` serializes direct renderer references and owns one cold `MaterialPropertyBlock`; `_H8GlobalQualityWeight` affects only visual MPB update quantization and pulse strength.
Rejected Alternatives: `renderer.material`, per-prefab material variants, or solver-side quality branches.
Scalability potential: Low bypasses subtle phase updates, middle/high/ultra increase pulse granularity without changing wattage truth.
Hardware Impact: Avoids material instancing and hot renderer searches. Estimated low-end gain: 0 B/frame and 5-35 us per visual sync group versus search/material-clone paths. STATIC ESTIMATE.

## Six-Port Breaker Junction Dry Run

Problem: A multi-breaker junction can corrupt runtime snapping if physical handle order and connectivity port order diverge.
Solution: `NormalizePorts` sorts the int port array; `BuildPowerPorts` projects it into `NetworkPortDescriptor[]`; `BuildBreakerHandles` serializes each handle pose/axis/angle, then sorts handle records by `portIndex` and stable hash. `NetworkNodeData` stores the sorted power ports, and `BreakerMetadata` stores the direct `Transform` references.
Rejected Alternatives: Hierarchy order, scene search, or designer-visible child order as authority. Those are unstable under prefab edits.
Scalability potential: Low uses one handle/proxy if metadata is absent; middle/high/ultra can serialize six or more exact handles without changing graph truth.
Hardware Impact: Avoids per-interaction handle lookup and remapping. Estimated i3/MX350 gain: 3-12 us per breaker interaction. STATIC ESTIMATE.

## Static Proof Artifact

Problem: The first proof route wrote a disk report; latest APEX protocol requires source code proof and rejects report I/O.
Solution: Superseded the JSON file after the APEX protocol. Proof is now source-level: prefab validators, struct layout guards, static scans, and in-memory EditorWindow metrics.
Rejected Alternatives: Persistent JSON proof churn and file hashes after the latest instruction made source code the proof artifact.
Scalability potential: Low/Middle/High/Ultra use the same source gates; only visual cadence and serialized proxy richness scale.
Hardware Impact: Removes editor report write I/O. Runtime remains 0 B/frame for this proof route.

## Compile Dependency Block

Problem: Task 18 requires compiler proof, but host CPU stayed above 50 percent and active `dotnet` processes were present. Console entries can be stale across editor imports.
Solution: Did not launch `dotnet build` or Unity refresh/compile. Used Unity MCP `validate_script` standard mode on the five 1740 C# files; all returned 0 errors/0 warnings. Treated console PowerNodeTypeID entries as non-final until a lawful import refresh can run.
Rejected Alternatives: Launching a second build/compile under load violates the prompt. Clearing console and claiming import success without a refresh would be false proof.
Scalability potential: No runtime impact. Keeping compile verification blocked rather than fake-passed preserves integration truth.
Hardware Impact: Avoided additional CPU contention on the host and avoided forcing a second compiler workload over an active one.

## APEX Polish Pass

Problem: The first pass left managed handle fields inside `BreakerHandleData`, immediate emissive presentation calls, and a report artifact that no longer matched the latest proof protocol.
Solution: `BreakerHandleData` is now a 64-byte explicit unmanaged row; `NetworkNodeData` validates `NetworkNodeBakeDTO`, `NetworkPortDescriptor`, `FluidPipeNodeBakeDTO`, and `PowerNodeDTO` through `UnsafeUtility.SizeOf<T>()`. `PowerBreakerRuntime` queues floats and writes MPB only in `LateFrameTick`; `PowerStatusEmissiveBinding` uses a triangle-wave pulse instead of sine.
Rejected Alternatives: String handle ids, runtime transform lookup, immediate MPB updates from arbitrary caller phase, and persistent JSON reports.
Scalability potential: Low uses 12-frame visual cadence and coarse quantization; middle reduces cadence cost; high/ultra can run per-frame triangle-pulse MPB updates without changing watts, ports, DTO layout, save identity, or authority route.
Hardware Impact: Low-end i3/MX350 avoids hot lookup/material clone/trig/report I/O. Static estimate: 0 B/frame, 2-20 us saved on low-tier visual sync, plus 3-12 us saved per breaker interaction by serialized grips.

## Unity Asset Hygiene

Problem: New Unity C# assets without `.meta` files would get importer-generated GUIDs and create integration drift.
Solution: Added five C# `.meta` files, verified each GUID appears exactly once, and verified orphan `.meta` count is zero under `Assets` and `Docs`.
Rejected Alternatives: Letting Unity generate transient GUIDs on import.
Scalability potential: No runtime tier difference. Stable GUIDs preserve prefab/component references across low/middle/high/ultra builds.
Hardware Impact: No frame impact. Prevents import churn and reference repair work.

## Duplicate Power Metadata Collapse

Problem: The 1740 pass created a parallel `PowerNodeData` component while first-party `NetworkNodeData` already owns graph identity, power DTO projection, port descriptors, and unmanaged layout validation.
Solution: Deleted `PowerNodeData.cs` and its `.meta`; rewired `PowerGridPrefabFactory` to add `NetworkNodeData`, serialize `PowerDc` metadata, validate `TryBuildPowerNodeDTO()`, and use `NetworkPortDescriptor[]` for sorted power ports.
Rejected Alternatives: Keeping both components would create two graph identity owners and a future chunk-load ambiguity. Mapping breaker/junction prefabs through `PowerRelayNode` was also rejected because passive boxes should not inherit relay drain/handoff behavior.
Scalability potential: Low keeps cheap primitive collision and stable port descriptors. Middle/High/Ultra can add richer visual mesh/material detail while using the same graph DTO route and stable hashes.
Hardware Impact: Removes duplicate component parse risk and preserves O(1) first-party node projection. Estimated i3/MX350 gain remains structural: 8-25 us avoided per chunk-load node versus runtime graph repair/search. STATIC ESTIMATE, PENDING PROFILER.

## Breaker Activation Authority Gate

Problem: `PowerBreakerRuntime` initially toggled only `PowerNode` fallback wattage. `PowerGrid` also sums separate `IPowerComponent` providers on the same object, so `BatteryBankModule`, `PowerRelayNode`, and RTG output could bypass an open breaker.
Solution: Added `IPowerActivationTarget` as a narrow power-domain contract. `PowerNode`, `BatteryBankModule`, `PowerRelayNode`, and `RadioisotopeThermalGenerator` implement it. `PowerGridPrefabFactory` serializes a direct `MonoBehaviour[]` target list, and `PowerBreakerRuntime` applies 0/1 activation through those cached targets. `PowerGrid.ShouldPublishPowerEdge` rejects runtime-open nodes, so an open switch stops both component wattage and power edge publication without marking the node as damaged.
Rejected Alternatives: Using `PowerNode.SetRuptured(true)` for switch-open state was rejected because rupture is damage semantics and leaks into logistics status bits. Runtime `GetComponents` or scene search during breaker toggles was rejected because the factory can serialize direct refs.
Scalability potential: Low keeps one binary switch and no extra graph work. Middle/High/Ultra can layer richer emissive and cable presentation over the same activation scalar without changing DTO layout or solver ownership.
Hardware Impact: Prevents stale generator/battery contribution after switch-open with 0 B/frame added. Static estimate: avoids 1-3 graph rebuild passes caused by inconsistent edge/component state during authored switch use; pending profiler.

## Pool Spawn Activation Ordering

Problem: `ObjectPoolManager` activates the GameObject and then calls `IPoolable.OnSpawn()` in cached component order. If a target component reset `_runtimeActivation01` in `OnSpawn` after `PowerBreakerRuntime.OnSpawn()`, an authored open breaker could be overwritten back to conductive state.
Solution: Removed activation reset from `OnSpawn` on `PowerNode`, `BatteryBankModule`, `PowerRelayNode`, and `RadioisotopeThermalGenerator`. Kept reset in `OnDespawn` so pooled objects return clean. Removed `PowerBreakerRuntime.OnDespawn()` authority application so despawn order cannot publish a transient switch state.
Rejected Alternatives: `DefaultExecutionOrder` was rejected because prefab/component order remains a brittle hidden dependency. Relying on `PowerNode.SetRuptured` was rejected because switch-open is not damage. Runtime lookup during spawn was rejected because the factory serializes direct activation targets.
Scalability potential: Low/Middle/High/Ultra all keep one authority scalar. Visual quality can scale in `LateFrameTick`; switch truth does not scale with device tier and does not depend on component order.
Hardware Impact: Prevents one-order-dependent graph dirty/rebuild churn with 0 B/frame added. Static estimate: avoids 1-3 extra graph dirty passes during pooled open-breaker spawn on low-end CPU; pending runtime profiler.

## PowerGrid Thermal DataVault Lock Flattening

Problem: `PowerGrid.BuildThermalDissipationSnapshot` acquired DataVault write locks before neighbor traversal, room-temperature reads, heat injection resolution, hull sink resolution, and edge resistance math. That made the locked window larger than a direct copy window and violated the lock-sovereignty rule.
Solution: Added prewarmed thermal scratch arrays on `PowerGrid`. The snapshot now computes offsets, temperatures, heat injection, hull sink conductance, destinations, and edge conductance before any write lock. Each DataVault write lock now wraps only `scratch[index] -> NativeArray[index]` copies and is released in `finally`.
Rejected Alternatives: Keeping the old lock window was rejected because it blocks compaction longer than necessary. Acquiring all thermal buffers at once was rejected because it would create a multi-lock deadlock surface. Allocating temporary arrays per snapshot was rejected because steady-state GC is forbidden.
Scalability potential: Low uses the same deterministic copy route with short lock windows. Middle/High/Ultra can spend more thermal iterations after pinning without increasing write-lock residency or changing gameplay truth.
Hardware Impact: Reduces write-lock hold time on i3/MX350 and limits compaction stalls. Exact microseconds pending profiler; expected saving is proportional to relay edge count because resistance math no longer runs inside the lock.

## PowerGrid Thermal Scratch Sovereignty Correction

Problem: Loop 8 replaced managed scratch growth with persistent `NativeArray<T>` fields on `PowerGrid`, but the current AGENTS memory-sovereignty rule forbids runtime owners from keeping persistent native aliases outside `GlobalDataVault`.
Solution: Removed the local persistent `NativeArray<T>` scratch fields. `PowerGrid` now uses its existing prewarmed `List<T>` capacity pattern for transient snapshot assembly, while all persistent native job buffers remain `GlobalDataVault` generation handles. DataVault write locks still wrap only direct copy loops and release in `finally`.
Rejected Alternatives: Keeping local `NativeArray<T>` fields was rejected as a sovereignty violation. Filling the vault buffers while doing neighbor traversal/resistance math under write locks was rejected because it widens compaction-blocking lock windows. Per-snapshot native temporaries were rejected because they add allocator churn.
Scalability potential: Low keeps the same cheap thermal approximation and short write locks. Middle/High/Ultra can spend more thermal iterations through the existing quality-weight route without changing buffer ownership or thermal truth.
Hardware Impact: Removes local persistent native ownership from the runtime grid. Steady-state remains no new GC after list capacity is warmed; topology-growth capacity can still allocate managed memory and needs profiler validation on large bases. Full profiler proof pending.

## Emissive Dirty Gate

Problem: `PowerStatusEmissiveBinding` only skipped repeated MPB writes on the minimum-quality lane. Medium and high tiers could submit the same quantized status state again, wasting renderer property-block traffic.
Solution: Added enable/validate/editor-bake dirty resets and changed the duplicate-state gate to apply at every quality level. High-tier pulse animation remains intact because phase is already part of the quantized state when quality is above the minimum lane.
Rejected Alternatives: Per-frame writes were rejected because they burn presentation bandwidth without changing pixels. Removing phase from the state was rejected because it would flatten high-tier pulse feedback.
Scalability potential: Low uses static/coarse status. Middle/High/Ultra keep smoother pulse cadence, but identical quantized states no longer touch renderer MPBs.
Hardware Impact: Static estimate only: avoids redundant `GetPropertyBlock`/`SetPropertyBlock` pairs when load/failure/quality/phase quantize to the previous state. Unity MCP validation passed; GPU/CPU profiler pending.

## Prefab Emissive And Handle Contract Tightening

Problem: `PowerGridPrefabFactory` validated only the default `_EmissionStrength` material property even when metadata specified a different emissive property. Handle metadata could also provide a rotation axis parallel to forward, producing unstable `Quaternion.LookRotation` output and bad grip basis data.
Solution: Resolved metadata property names once per prefab and validated the actual emission color/strength material properties. Added safe axis orthogonalization before handle transform rotation and `BreakerHandleData` serialization, plus matching sanitation in `BreakerMetadata`. Post-save validation now checks that `PowerBreakerRuntime`, `BreakerMetadata`, and `PowerStatusEmissiveBinding` survived prefab serialization with direct activation bindings.
Rejected Alternatives: Trusting metadata blindly was rejected because prefab bake is the only cheap place to catch bad art data. Runtime repair of handle axes was rejected because it would add gameplay-side ambiguity and hide broken source assets.
Scalability potential: Low keeps one cheap serialized grip and primitive collider; middle/high/ultra can use richer authored handles/materials while the same validator proves the MPB pulse route.
Hardware Impact: Prevents failed MPB visual response and grip-basis repair at runtime. Static estimate: 0 B/frame added, 3-12 us per interaction still avoided by serialized handles.

## UberNoir Emission Strength And Include Validator

Problem: The power prefab pulse path writes `_EmissionStrength` through `MaterialPropertyBlock`, but the first-party `Hecton8_UberNoir` shader did not expose or consume that property. The factory also proved SRP Batcher compatibility only by reading the root `.shader`; `UberNoir` keeps `CBUFFER_START(UnityPerMaterial)` in `Hecton8_UberNoir.hlsl`, so valid materials could be rejected.
Solution: Added `_EmissionStrength` to the shader property block, declared it in `UnityPerMaterial`, and multiplied the three direct `_EmissionColor` emission routes through a finite-safe strength helper. Added `MAT_Equipment_Atlas.mat` with stable `.meta` under the existing construction material folder, using the current `Hecton8_UberNoir.shader` GUID. Extended the editor validator to scan quoted shader includes to depth 4 before rejecting a material.
Rejected Alternatives: Creating material instances at runtime was rejected because it breaks batching and allocation discipline. Blindly accepting any shader with `_EmissionStrength` was rejected because SRP Batcher proof still matters. Rewriting all existing materials with the old shader GUID was rejected as outside 1740 and risky under parallel-agent edits.
Scalability potential: Low tier keeps the cheap triangle pulse and lower visual cadence. Middle tier uses the same MPB route with better cadence. High/Ultra can push brighter status feedback and richer atlas art without changing graph truth, DTO layout, save identity, or power authority.
Hardware Impact: Fixes a zero-visual-response bug without adding runtime lookup or material clones. Static estimate: 0 B/frame added; avoids per-prefab material fallback/search churn during editor assembly. Runtime GPU cost remains the existing emission multiply; profiler pending because Unity session is unavailable and full build is CPU-gated.

## Analytic Power Prefab Fallback Coverage

Problem: The factory could discover no source groups because the expected power visual folders are absent in the current project state. A zero-output factory leaves reactors, RTGs, battery banks, relays, breakers, and junctions in manual assembly territory.
Solution: Added editor-only analytic fallback seeds for missing baseline power node types. The factory now appends only missing types, builds primitive hard-surface visuals, strips source colliders, assigns shared `MAT_Equipment_Atlas`, and runs the same `NetworkNodeData`, `BreakerMetadata`, primitive collider, SRP material, and activation-target validators before save.
Rejected Alternatives: Creating a separate fallback prefab generator was rejected as duplicate tooling. Runtime placeholder generation was rejected because prefab assembly must be offline and serialized. Adding fallback only when the source set is completely empty was rejected because partial art coverage still leaves missing power gameplay objects.
Scalability potential: Low tier receives cheap blockout geometry with primitive colliders and serialized handles. Middle tier can swap source meshes while preserving the same node metadata route. High/Ultra can replace fallback visuals with authored atlas meshes without changing power graph truth, activation authority, DTO layout, or port ownership.
Hardware Impact: Prevents runtime/manual repair paths and keeps prefab load deterministic. Static estimate remains editor-time only; runtime cost is the saved prefab geometry and one primitive collider route per object. Unity MCP C# validation passed; full build and prefab execution are still gated by host load.

## Breaker Dispatcher Hot-Swap Rebind

Problem: `PowerBreakerRuntime` could enable before `GlobalRegistry.Dispatcher` was available. In that ordering, breaker authority still applied, but emissive state presentation never entered `LateFrameTick`.
Solution: Added `IGlobalRegistryHotSwapListener` to `PowerBreakerRuntime`. Dispatcher replacement now unregisters the stale late-frame route and re-registers when the current dispatcher exists. Disable, despawn, and destroy all unregister both late-frame and hot-swap listener state.
Rejected Alternatives: Polling `GlobalRegistry.Dispatcher` inside `LateFrameTick` or a managed coroutine retry loop was rejected. Both add hot/cadence ambiguity for a cold DI problem.
Scalability potential: Low keeps sparse emissive cadence; middle/high/ultra get smoother visual sync after dispatcher rebind without changing watts, graph edges, DTO layout, or save identity.
Hardware Impact: Prevents missing status pulse after bootstrap ordering variance with 0 B/frame added. Static estimate: avoids a silent visual-sync dropout; Unity MCP validation passed.

## Activation Target Save Gate

Problem: A prefab could pass validation with a non-empty activation target array even if a serialized target did not implement `IPowerActivationTarget`; runtime would ignore that target and only the fallback `PowerNode` might be gated.
Solution: Added `PowerBreakerRuntime.HasValidActivationTargets` and changed `PowerGridPrefabFactory` post-save validation to reject any saved prefab with invalid activation targets.
Rejected Alternatives: Letting `ApplyAuthorityState` silently skip bad targets was rejected because prefab assembly is the cheap proof phase. Runtime discovery/repair was rejected because direct serialized references are the contract.
Scalability potential: Low/middle/high/ultra all use the same direct target authority. Visual richness can scale, but switch authority cannot be device-dependent.
Hardware Impact: No runtime allocation or extra lookup. Static estimate: prevents wrong prefab authoring from causing extra graph dirty/debug passes; profiler not applicable until prefab generation is run.

## Prefab Power Ownership Deduplication

Problem: RTG, battery, and relay prefabs could own power twice: a typed `IPowerComponent` carried domain-specific wattage/storage/loss, while `PowerNode.fallbackPowerRating` still advertised a second generic source or drain.
Solution: `PowerGridPrefabFactory` now attaches typed runtime components first, then resolves the `PowerNode` fallback wattage. Typed owners receive a zero fallback; untyped reactor/generator/RTG fallback paths retain base wattage. The pre-save validator compares the serialized fallback value against the expected ownership route.
Rejected Alternatives: Keeping both sources was rejected because it skews generation/drain accounting. Removing `PowerNode` fallback entirely was rejected because generic/untyped source prefabs still need a first-party graph authority. Runtime correction was rejected because prefab bake is the cheap deterministic proof phase.
Scalability potential: Low, middle, high, and ultra tiers all keep identical wattage truth. Visual richness scales through material/mesh/cadence only, not through duplicated electrical authority.
Hardware Impact: Prevents extra graph dirty/debug work caused by wrong authoring. Static estimate: 0 B/frame added; wattage route is resolved offline.

## NetworkNodeData Base Wattage Contract

Problem: `NetworkNodeData` serialized capacity, resistance, storage, and flags, but not the base wattage fact required by the power prefab prompt. That left future chunk registration dependent on `PowerNode` fallback or typed component inspection.
Solution: Added cold serialized `PowerBaseWattage` to `NetworkNodeData`, consumed existing `NetworkNodeBakeDTO` padding for `BaseWattage` at offset 24, and extended `ConfigureEditorBake` with a trailing optional parameter so existing logistics calls remain source-compatible. `PowerGridPrefabFactory` writes `baseWattage` and rejects prefabs that lose it in either the serialized component field or unmanaged bake row. `PowerNodeDTO` remains unchanged to preserve the current 32-byte solver ABI.
Rejected Alternatives: Extending `PowerNodeDTO` was rejected because solver ABI churn is unnecessary for an authoring fact. Creating a duplicate `PowerNodeData` component was rejected because `NetworkNodeData` already owns the first-party graph projection. Growing `NetworkNodeBakeDTO` was rejected because its existing padding can carry the field with no copy-width change.
Scalability potential: Every device tier reads the same authored watts. Future high-tier presentation can show richer load feedback without changing DTO layout, save identity, or graph authority.
Hardware Impact: No hot-path cost. Unity MCP validation passed for `NetworkNodeData.cs` and `PowerGridPrefabFactory.cs`; full profiler proof is not applicable until prefab generation/import is run.

## Logistics Node Type Ownership Mapping

Problem: `PowerGridPrefabFactory.ResolveLogisticsNodeType` treated any positive `baseWattage` as `Producer`. Authored metadata could therefore turn relay, breaker, battery, or junction prefabs into source-class graph rows even when their type says they are not generators.
Solution: Removed watt-sign classification from node-type mapping. Reactor, RTG, and Generator map to `Producer`; Relay and Battery map to `Relay`; Breaker, Junction, and default map to `Junction`.
Rejected Alternatives: Keeping the watt shortcut was rejected because node class is authority, not a derived presentation value. Adding runtime correction in `PowerGridManager` was rejected because prefab bake must serialize the right graph row before chunk registration.
Scalability potential: All device tiers receive the same graph topology. Visual or telemetry richness can scale, but source/relay/junction ownership cannot be quality-dependent.
Hardware Impact: No runtime cost. Prevents wrong source rows from causing extra solver work or false generation accounting.
