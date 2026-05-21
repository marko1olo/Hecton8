# Rationale - SHINOBU_SYSTEMIC_SURGEON

Status: PENDING VERIFICATION
Domain: Systemic DOD sanitation across all 85 domains

## Bootstrap Decision

Problem: `Docs/Tasks/CURRENT_BATCH.md` did not contain `<AGENT_PROMPT id="SHINOBU_SYSTEMIC_SURGEON">`, while the session includes the full XML prompt directly.
Solution: Recorded CLI extraction failure and accepted the user-supplied XML as the current assignment source. No neighboring batch prompt was used.
Rejected Alternatives: Reading archived batch prompts was rejected because AGENTS.md forbids previous-batch contamination unless explicitly ordered.
Scalability potential: Not a runtime path.
Hardware Impact: 0 us runtime effect; prevents wrong-domain edits.

## Initial Architecture Constraints

Problem: The task spans native memory, Burst jobs, DTO layout, AUP math, SignalBus queues, Addressables, rendering, audio, editor tooling, and CSV ingestion.
Solution: Phase execution is constrained to evidence-first source scans and small patches tied to existing code. New DataVault or global authority surfaces require route-card evidence before source changes.
Rejected Alternatives: Broad speculative refactor was rejected because public interface churn and fabricated DataVault APIs would break parallel-agent work.
Scalability potential: Low/Middle/High/Ultra decisions must use continuous `GlobalQualityWeight` or equivalent quality parameters where runtime code changes cadence/fidelity.
Hardware Impact: Expected gains must be static-estimate only until profiler evidence exists; no fake microsecond claim will be marked verified.

## Burst Alias Lane Patch

Problem: Several active jobs had independent result lanes without explicit `[WriteOnly]` and `[NoAlias]`, leaving Burst alias analysis conservative.
Solution: Added write-only/noalias annotations only where access mode was proven: Fluid impulse outputs, audio virtualization selections/statistics/mock outputs/occlusion output, tether solver outputs, and Verlet solver outputs.
Rejected Alternatives: Marking mixed read/write lanes as write-only was rejected because it would make in-place compaction and solver feedback invalid.
Scalability potential: Low tier gets fewer memory dependency stalls. Middle/High/Ultra get more SIMD/vectorization opportunity without gameplay changes.
Hardware Impact: Static estimate 1-8 us per active pass on i3/MX350-class CPU when the job is scheduled; unprofiled.

## Tether And Verlet NaN Clamp

Problem: Tether and Verlet constraints could propagate non-finite direction/tension under extreme stretch or near-zero distance, and stale uninitialized force slots could survive after active constraint count shrank.
Solution: Guarded denominator with 0.0001f, capped tension at 250000 N, sanitized directions, wrote capped force packets, and cleared inactive tension/force output slots after the solve.
Rejected Alternatives: Increasing solver iterations was rejected because it spends frame time on instability. Unbounded physical tension was rejected because it can poison PhysicsApplySystem with NaNs.
Scalability potential: Low uses the same finite clamp with lower solver cadence. Middle/High/Ultra can spend quality budget on spline/render fidelity without changing truth ownership.
Hardware Impact: Static estimate 1-4 us avoided during fault recovery on i3/MX350; primary gain is crash prevention, not steady-state speed.

## Addressables TTL Lock Order

Problem: TTL evaluation resolved native tracker/handle-map views before acquiring DataVault locks, leaving a race window against managed load/release bridge mutation.
Solution: Lock Addressable heap buffers first, resolve views second, and release the locks immediately if resolution fails.
Rejected Alternatives: Rewriting the managed Addressables bridge into a new ring was rejected this turn because it crosses a large public owner surface and needs broader integration proof.
Scalability potential: Low/Middle/High/Ultra all keep the same release authority; only the race window is removed.
Hardware Impact: 0 us steady-state claim; prevents rare release/load race corruption.

## Autopilot SDF Feeler Early-Out

Problem: EvaluateCollisionAvoidanceJob marched every feeler even when the first SDF sample proved the vehicle was more than twice the collision radius from solid rock.
Solution: Added one SDF sample at the vehicle origin and skipped per-feeler raymarches when signed distance is finite and clear enough.
Rejected Alternatives: Physics.Raycast or MeshCollider queries were rejected as nondeterministic and too expensive for the AUP world.
Scalability potential: Low skips most open-water work. Middle retains interpolation where needed. High/Ultra still run full feelers only near geometry.
Hardware Impact: Static estimate avoids `activeVehicles * feelers * steps` SDF samples in open water; exact us not verified.

## Decal Ring Fade Before Overwrite

Problem: Full decal ring overwrote active slots at CurrentWriteIndex, causing visible popping when impact density exceeded capacity.
Solution: Under full capacity, exponentially faded the oldest 10% of slots before overwrite and dropped new requests until the target slot opacity decays under the replacement threshold.
Rejected Alternatives: Growing the ring or allocating a managed overflow queue was rejected because capacity must remain fixed and predictable.
Scalability potential: Low keeps a small fixed ring and fades under pressure. Middle/High/Ultra can raise capacity elsewhere without changing this replacement rule.
Hardware Impact: Adds a bounded O(capacity/10) pass only when full; expected visual gain, not CPU savings.

## Dynamic Resolution Ringing Guard

Problem: CPU-side sharpening was already scaled, but the shader could still over-amplify detail if material/global constants drifted at low render scale.
Solution: Added shader-side continuous scale-deficit ringing guard and bounded dither before the existing Dear Lie grain pass.
Rejected Alternatives: A binary low-resolution branch was rejected because quality must be a continuous curve.
Scalability potential: Low uses stronger damping and more mask dither. Middle transitions smoothly. High/Ultra retains sharper reconstruction when scale is near native.
Hardware Impact: CPU 0 us; shader adds a few scalar ALU ops, expected below measurable frame cost.

## Editor CSV Split Removal

Problem: BlackboxXRayViewer parsed dictionary CSV lines with `Split`, allocating a string array per line.
Solution: Replaced it with span separator parsing and one final string allocation only for the stored event name.
Rejected Alternatives: Removing the dictionary name store was rejected because the UI requires stable display strings.
Scalability potential: Editor-only; no runtime gameplay impact.
Hardware Impact: 0 us runtime; reduces editor load garbage.

## Telemetry Dump Validator

Problem: There was no generic editor path to inspect `.bin`/`.h8dump` payload headers, little-endian fields, checksum candidates, and the last 300 rows across heterogeneous black-box dump formats.
Solution: Added `TelemetryDumpValidatorWindow` under the editor diagnostics menu. It reads a dump, reports header fields, computes XXHash3 for payload regions, and displays up to the last 300 fixed-size entries as byte previews.
Rejected Alternatives: Hard-coding one system DTO reader was rejected because the task requires cross-system forensic triage.
Scalability potential: Editor-only. Low/Middle/High/Ultra runtime unaffected; improves postmortem route proof.
Hardware Impact: 0 us runtime; editor file read cost only when manually invoked.

## AUP Double-Before-Float Repair

Problem: Voxel dynamic nav patching and world chunk origins converted absolute coordinates to `Vector3` before floating-origin subtraction.
Solution: Built absolute positions as `double3`, subtracted through `HectonFloatingOrigin.ToRuntimePosition(double3)`, then downcast only the local runtime result.
Rejected Alternatives: Keeping the `Vector3` overload was rejected because it truncates absolute coordinates before origin subtraction near large world offsets.
Scalability potential: Low/Middle/High/Ultra all retain identical navigation and chunk placement truth; visual tier does not alter coordinate authority.
Hardware Impact: 0 us measurable claim; removes boundary jitter risk rather than buying steady-state CPU.

## GlobalSignals SPSC Cache-Line Padding

Problem: `SpscSignalRingBuffer<T>` stored producer tail and consumer head as adjacent `int` fields, creating false-sharing pressure between producer and consumer cores.
Solution: Replaced head/tail with explicit 64-byte padded index structs and allowed cache-line-critical payload strides that are exact 64-byte multiples up to 192 bytes.
Rejected Alternatives: Replacing the queue with a new signal surface was rejected because `GlobalSignals` is a legacy bridge and new gameplay lanes require route-card review.
Scalability potential: Low tier reduces cache coherency stalls. Middle/High/Ultra keep the same API while improving SPSC throughput headroom.
Hardware Impact: Static estimate 1-3 us under high audio/signal contention on i3/MX350; unprofiled.

## Jacobi Three-Growth Gate

Problem: Active logistics Jacobi solvers damped omega after a single residual increase but did not require a three-successive-growth fast-fail gate.
Solution: Added residual growth counters; after three consecutive residual increases, the solver copies the previous stable potential buffer forward, marks divergent flags, and exits without same-frame completion.
Rejected Alternatives: More iterations were rejected because oscillating grids spend frame time without convergence. Main-thread `.Complete()` telemetry was rejected because dispatcher-owned windows must remain the only synchronization route.
Scalability potential: Low fails fast and preserves stable potential. Middle/High/Ultra can still use higher quality iteration budgets until the residual gate trips.
Hardware Impact: Static estimate avoids up to remaining Jacobi iterations during oscillation; exact us requires profiler proof.

## Terrain Chunk SignalBus Eviction

Problem: `TerrainChunkGeneratedEvents` owned a private persistent `NativeQueue<TerrainChunkGeneratedSignal>` with local allocation, prewarm, and disposal code outside the central signal authority.
Solution: Converted `TerrainChunkGeneratedSignal` to the existing explicit 64-byte `ISignal` payload and routed publish/dequeue through `SignalBus<TerrainChunkGeneratedSignal>` configured with a 32-signal frame limit and 4-signal survival limit.
Rejected Alternatives: A new `GlobalDataVault` buffer route was rejected because no TerrainChunkGenerated Vault buffer ID or route card exists. Keeping the local queue was rejected because the task explicitly targets private persistent native allocation.
Scalability potential: Low tier drains at the survival limit through SignalBus frame-limit policy. Middle/High/Ultra can consume the full 32-signal terrain lane without changing MapMagic or seam-applier public API.
Hardware Impact: 0 us measured; removes one local persistent queue lifecycle and centralizes signal telemetry/drop accounting.

## Audio Emergency Fallback Capacity Guard

Problem: `GenerateEmergencyMockAcoustics(NativeParallelHashMap<uint, AcousticMaterialCoefficientDTO>)` cleared the table and then wrote three rows through indexer assignment without checking native capacity.
Solution: Added a capacity check and bounded `TryAdd` sequence, returning the actual number of rows installed.
Rejected Alternatives: Growing the hash map in the fallback path was rejected because emergency data loading must use preallocated native storage. Managed fallback dictionaries were rejected for runtime GC and authority drift.
Scalability potential: Low/Middle/High/Ultra all receive deterministic baseline constants; the table fills only to its preallocated capacity.
Hardware Impact: 0 us hot-path claim; prevents fallback-time native capacity faults.

## Auxiliary Producer Finite Shield

Problem: Auxiliary equipment Burst jobs wrote flare, sonar, and tether signals directly through legacy `NativeQueue<T>.ParallelWriter`, bypassing the managed-side SignalBus finite guard until dispatcher flush.
Solution: Added `[WriteOnly]`, `[NoAlias]`, and `NativeDisableContainerSafetyRestriction` to the three producer writers and inserted inline finite checks before enqueue.
Rejected Alternatives: Replacing all `NativeQueue<T>.ParallelWriter` producers with a new wrapper was rejected because the repo has many sibling producers and a generic API change would be cross-domain churn. Dropping all legacy MPSC writers was rejected because several systems still depend on the compatibility ABI.
Scalability potential: Low tier avoids queue pollution from invalid visual/audio auxiliary payloads. Middle/High/Ultra keep the same visual cadence and spend saved recovery budget on richer auxiliary VFX.
Hardware Impact: Static estimate below 1 us steady-state; primary impact is avoiding corrupted signal flush work and downstream fault recovery.

## GPU Scatter AUP Stable Cell Base

Problem: `GPUScatterDirector` converted the accumulated origin offset to `Vector2` and the compute shader added that float offset to local cell anchors before hashing. At large AUP offsets, this reintroduced absolute-float precision loss in the procedural scatter hash.
Solution: Store the accumulated scatter origin offset as `double2`, snap the field origin in double space, compute the stable integer cell base on the CPU after double-space origin addition, and pass that base to the compute shader. The shader now hashes `stableCellBase + localCell` instead of adding large floats.
Rejected Alternatives: Leaving the shader to add `_HectonScatterAupGridOffset.xy` to local float coordinates was rejected because it preserves the failure mode. Moving the whole scatter generation to CPU was rejected because scatter is already a Dear Lie GPU path.
Scalability potential: Low keeps the smaller scatter radius and budget through continuous quality curves. Middle/High/Ultra increase density smoothly without changing coordinate authority or hash stability.
Hardware Impact: 0 us measured; removes boundary shimmer risk while preserving the GPU-only scatter fake.

## GPU Scatter Continuous Quality Curve

Problem: Scatter radius, projected pixel threshold, and instance budget were driven by binary/enum quality-tier checks.
Solution: Cache `GlobalQualityWeight01` from the SignalBus/Homeostasis path and resolve min projected pixel radius, micro-scatter cull distance, and instance budget through smoothstep/lerp curves.
Rejected Alternatives: `if (Low/Mx350/Mid/High)` branching was rejected because quality pressure must be continuous under thermal throttling. Per-device hard tables were rejected because they create visible popping.
Scalability potential: Low collapses toward the survival budget and shorter cull radius. Middle receives interpolated density and cull range. High/Ultra receive the full visual-overkill scatter budget without changing DTO layout or save authority.
Hardware Impact: Static estimate is visual stability rather than CPU gain; avoided overdraw depends on scene density and quality weight.

## Hull Deformation GPU Bridge Audit

Problem: Task 16 required proof that deformation states cross from Vault-owned C# data to GPU structured buffers with double-buffering and local-space shader displacement.
Solution: Audited `HullIntegrityRuntime`, `HullIntegrityTypes`, and `Hecton8_UberNoir.hlsl`. The runtime uses Vault buffers 70090-70099, validates `DeformationStateDTO` at 64 bytes, uploads through A/B `GraphicsBuffer.LockBufferForWrite`, then binds `_HectonDeformationStateBuffer`. The shader performs Gaussian local-space dent displacement from the structured buffer.
Rejected Alternatives: Adding a duplicate deformation upload bridge was rejected because the compliant route already exists and duplicate state would violate one fact/one route.
Scalability potential: Low clamps shader dent count via quality-weight-derived limits. Middle/High/Ultra raise the visual dent limit continuously and spend saved CPU cycles in the vertex shader.
Hardware Impact: 0 us code-change claim; existing GPU fake avoids CPU mesh deformation and preserves SRP batching.

## Chronicler Master Heatmap Window

Problem: Existing diagnostic windows were siloed; Task 18 required a master UI Toolkit facade over Memory, Signals, Netcode, Autopilot, Physics, and Telemetry rings without adding runtime owners.
Solution: Added `ChroniclerDiagnosticHeatmapWindow`. It reads GlobalDataVault telemetry snapshots, SignalBus lane telemetry, SignalTelemetryRingBuffer frames, SystemDispatcher phase/fence telemetry including physics/audio/netcode handle bits, SignalThreadLocalScratchpad contention telemetry, and GlobalTelemetryBus blackbox frames/events. Graph columns are created once and reused on refresh.
Rejected Alternatives: Polling scene objects or inventing a new runtime telemetry aggregator was rejected because GlobalRegistry is cold identity only and telemetry facts already have owners. Clearing/rebuilding UI rows every tick was rejected for the graph path; fixed strips reuse their elements.
Scalability potential: Editor-only. Low/Middle/High/Ultra runtime behavior is unchanged; the window exposes quality pressure and contention so developers can tune continuous curves instead of binary tiers.
Hardware Impact: 0 us runtime. Editor refresh cost is bounded by fixed 96-column strips, 256 signal lanes, and 300-frame rings.

## Symbiosis Emergency Mock Bulk Clear

Problem: `GenerateEmergencyMockSymbiosisJob` cleared inactive Flora/FloraAup/Link/MockFish slots with scalar stores, did not tag proven output-only lanes as write-only, and allowed negative requested mock counts to leak into active counters/boid DTOs.
Solution: Converted the job to an unsafe Burst job with `[WriteOnly, NoAlias]` on output-only buffers, added `MemClearArray` and `MemClearTail` helpers using `NativeArrayUnsafeUtility.GetUnsafePtr` plus `UnsafeUtility.MemClear`, clamped requested flora/fish/link counts to non-negative limits, and sanitized a non-finite center AUP to default before all mock AUP offsets.
Rejected Alternatives: Managed staging arrays or `Array.Clear` were rejected because fallback hydration targets Vault-owned native buffers. Marking `FloraAups` write-only was rejected because the job reads active flora AUPs back while constructing mock fish anchors.
Scalability potential: Low tier gets deterministic small fallback data without scalar tail pollution. Middle/High/Ultra keep the same authored mock profile and can raise counts through Vault capacity without changing the fallback route.
Hardware Impact: 0 us hot-path effect. Cold fallback tail clearing changes from per-element stores to a bulk native clear; exact us requires Unity profiler, but memory bandwidth is the only remaining cost.

## Fabrication Signal Producer Shield

Problem: `EmitFabricationSignalsJob` wrote completed, tick, and deconstruct signals through producer-only `NativeQueue<T>.ParallelWriter` fields with safety suppression but without `[WriteOnly]` metadata, and it could enqueue non-finite target AUP payloads.
Solution: Marked `Jobs` as `[ReadOnly, NoAlias]`, marked all producer writers `[WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction]`, and added finite `double3` target AUP gates before completion, tick, and deconstruct enqueues.
Rejected Alternatives: A managed completion callback or immediate UI notification was rejected because fabrication completion crosses crafting/inventory/UI domains and must stay phase-separated through signal queues.
Scalability potential: Low tier avoids corrupted signal pressure and keeps sparse event emission. Middle/High/Ultra keep the same visual cadence while Burst gets clearer alias constraints on the batch job.
Hardware Impact: Static estimate below 1 us steady-state; primary gain is queue hygiene and better alias metadata for an existing dispatcher-owned job.

## Fabrication ReadOnly Pointer Tightening

Problem: After `Jobs` became `[ReadOnly, NoAlias]`, `EmitFabricationSignalsJob` still acquired the job DTO through `GetUnsafeBufferPointerWithoutChecks`, which exposes a mutable pointer and weakens the compile-time alias contract.
Solution: Hoisted a single `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Jobs)` before the loop and reads each `FabricationJobDTO` as `ref readonly` while keeping the runtime state lane mutable.
Rejected Alternatives: Copying `Jobs[index]` was rejected because it adds a DTO copy per active slot. Leaving the unchecked mutable pointer was rejected because it contradicts the read-only container metadata.
Scalability potential: Low/Middle/High/Ultra keep identical gameplay truth; the change improves Burst alias clarity without changing cadence, capacity, or route ownership.
Hardware Impact: Static estimate below 1 us steady-state; removes one mutable pointer exposure and one repeated container pointer lookup per loop iteration.

## Persistent World DTO Property Purge

Problem: `PersistentWorldItemRecord` stored quantity and flags behind C# properties and exposed boolean flag properties; `PersistentWorldDeltaRecord` also exposed boolean/validity properties. These DTOs flow through `NativeList<T>`, save snapshots, sector paging, and hydration scans, so accessor properties preserve the CS1612/hidden-copy risk.
Solution: Replaced `PersistentWorldItemRecord.Quantity` and `Flags` with raw fields at offsets 196 and 204, added explicit pad bytes 205-207, kept the struct at 256 bytes, and converted all boolean checks to static `in` helper predicates. Replaced `PersistentWorldDeltaRecord` boolean/validity properties with static `in` helpers and converted stale call sites in `PersistentWorldRegistry`, `PlayerExplorationTracker`, `FloraRegrowthDirector`, and `SaveBinaryStorage`.
Rejected Alternatives: Keeping the packed `uint` property pair was rejected because it keeps getter/setter methods in a native DTO. Expanding this into a public interface was rejected because it would add virtual/cross-domain surface. Changing `PersistentWorldDeltaRecord` layout was rejected because it is the on-disk/save compact section ABI.
Scalability potential: Low tier persistence scans avoid per-record accessor dispatch and direct `NativeList` indexer property copies. Middle/High/Ultra retain the same save identity and can spend budget on richer hydration visuals without changing DTO layout by quality tier.
Hardware Impact: Static estimate below 1 us per persistence scan on i3/MX350-class CPU; primary value is eliminating hidden struct-copy/accessor risk and restoring ARM64-explicit field visibility.

## Physics Impact Signal Layout Purge

Problem: `PhysicsImpactSignal` is a deferred high-frequency impact payload consumed by audio, camera, inventory, acoustic zones, and physics wake routes, but it was a get-only property struct without explicit byte layout.
Solution: Converted it to `[StructLayout(LayoutKind.Explicit, Size = 128)]` with readonly raw fields and static `in` predicates for heavy/AUP availability. Updated heavy-impact consumers to call `PhysicsImpactSignal.IsHeavy(in impactSignal)` and added layout assertions in `BinaryLayoutManifest`.
Rejected Alternatives: Keeping auto-properties was rejected because the payload crosses hot event listeners and can be copied through `in` references. A new SignalBus lane was rejected this pass because the existing event listener bridge is broader than the property/layout defect and needs a route card before replacement.
Scalability potential: Low tier listeners avoid property accessor overhead while keeping the same deferred event route. Middle/High/Ultra can still spend impact severity on richer audio/VFX response without changing signal layout or truth ownership.
Hardware Impact: Static estimate below 1 us per impact burst; primary gain is explicit 128-byte cache-line layout and removal of hidden accessors from the impact fan-out path.

## Fauna Interaction Response Layout Purge

Problem: `FaunaInteractionResponse` was a readonly struct with get-only properties used during fauna interaction handling, including forced-retreat and damage/fear response.
Solution: Converted it to `[StructLayout(LayoutKind.Explicit, Size = 32)]` with raw readonly fields, byte-backed `ForceRetreatFlag`, and a static `ShouldForceRetreat(in response)` predicate. Added layout assertions in `BinaryLayoutManifest`.
Rejected Alternatives: Keeping a `bool ForceRetreat` property was rejected because it retains an accessor and an implementation-defined bool layout. Routing interaction through a managed interface was rejected because fauna reaction is gameplay state, not editor authoring.
Scalability potential: Low tier keeps cheap direct scalar response. Middle/High/Ultra can amplify visuals/audio around the same response fields without changing gameplay truth or DTO shape.
Hardware Impact: Static estimate below 1 us per interaction; removes accessor methods and pins the payload to one 32-byte aligned row.

## Gas Dynamics Producer Metadata

Problem: `GasDynamicsStepJob` writes toxicity signals and a telemetry ring entry through output-only lanes, but the queue writer and telemetry buffer were not marked write-only, leaving Burst alias analysis more conservative than the actual data flow.
Solution: Marked `ToxicitySignals` as `[WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction]` and `TelemetryRing` as `[WriteOnly, NoAlias]`.
Rejected Alternatives: Rewriting the GasDynamics private persistent arrays to Vault in this pass was rejected because the route is larger than metadata hardening and the status file already tracks it as pending route-card work.
Scalability potential: Low tier reduces queue/telemetry alias pressure while sleeping room hibernation still gates work. Middle/High/Ultra keep the same gas truth and telemetry contract.
Hardware Impact: Static estimate below 1 us steady-state; primarily improves vectorization confidence and producer-lane hygiene.

## Player Noise Signal Layout Purge

Problem: `NoiseSystem.PlayerNoiseSignal` was a global player-acoustic signal with get-only properties and managed bool lanes. It feeds fauna awareness, vegetation threat deposition, active sonar dispatch, and spatial transient registration.
Solution: Converted it to `[BinaryBlittableSafe] [StructLayout(LayoutKind.Explicit, Size = 96)]` with AUP first, scalar lanes second, and byte flags at the tail. Replaced `FlashlightOn` and `IsActiveSonarPing` property reads with static `in` predicates.
Rejected Alternatives: Keeping public bool properties was rejected because it preserves accessor methods and implementation-defined bool layout. Replacing the signal route was rejected because the current owner is a cold static snapshot plus spatial hash dispatch, and a new route needs a separate authority card.
Scalability potential: Low tier keeps the same cheap scalar signal and shorter downstream budgets. Middle/High/Ultra can spend acoustic intensity on richer fauna/audio/visual response without changing signal truth or layout.
Hardware Impact: Static estimate below 1 us per dispatch on i3/MX350-class CPU; primary value is explicit 96-byte ARM64-safe layout and no hidden accessor copies.

## Fauna Cognition DTO Layout Purge

Problem: `CreatureUtilityContext` and `CreatureUtilityEvaluation` are active fauna cognition value payloads in the tick loop, but they were property-backed structs with bool lanes spread through the layout.
Solution: Converted `CreatureUtilityContext` to a 256-byte explicit row with packed flags at offset 232 and `CreatureUtilityEvaluation` to an 80-byte explicit row with packed flags at offset 60. Updated boolean reads in `CreatureUtilityBrain` and `FaunaBrain` to static `in` predicates.
Rejected Alternatives: Leaving get-only properties was rejected because the payloads are copied through `in` calls and feed active AI state. Storing raw bool fields was rejected because bool layout is not a stable binary contract for the manifest.
Scalability potential: Low tier keeps cheap scalar cognition and foveated cadence. Middle/High/Ultra can spend saved budget on high-tier apex steering and richer threat/audio presentation without changing gameplay truth ownership.
Hardware Impact: Static estimate below 1 us per active cognition tick; the measurable value is removal of accessor copies and stable cache-aligned rows for ARM64.

## Survival Death Record Layout Purge

Problem: `SurvivalDeathRecord` was a persisted telemetry record with get-only properties and no explicit byte contract, while death UX, profile events, and save hydration pass it by value.
Solution: Converted it to `[BinaryBlittableSafe] [StructLayout(LayoutKind.Explicit, Size = 64)]` with doubles first, Vector3/scalars next, enum byte at the tail, and full manual padding through byte 63. Added manifest assertions for the gameplay layout.
Rejected Alternatives: Keeping property accessors was rejected because the record is passed through multiple systems and should be a raw data row. Changing save DTO fields was rejected because the save ABI already stores scalar death fields separately.
Scalability potential: Low/Middle/High/Ultra unchanged; this is deterministic telemetry hygiene, not quality-tier behavior.
Hardware Impact: 0 us hot-path claim; removes layout ambiguity and accessor copies on cold death/profile/UI paths.

## GasDynamics BaseAwake Vault Fallback Eviction

Problem: `GasDynamicsSolver.ResolveBaseAwakeStateBuffer` already requested `BufferID.HabitatBaseAwakeState` from `GlobalDataVault`, but when the Vault was absent it allocated a private persistent `NativeArray<byte>` fallback.
Solution: Removed the fallback allocation. `EnsureNativeState` now resolves the Vault buffer before allocating local SOA lanes and fails closed when the generation-checked Vault handle is unavailable.
Rejected Alternatives: Keeping the fallback was rejected because it creates a second owner for base awake truth. Inventing the full gas SOA Vault route in this pass was rejected because only `HabitatBaseAwakeState` has an existing BufferID/authority proof in the current code.
Scalability potential: Low/Middle/High/Ultra unchanged; the base awake fact now has one route through the Vault instead of a degraded private fallback.
Hardware Impact: 0 us hot-path claim; removes one cold persistent allocation and prevents divergent base-awake state under bootstrap failure.

## PersistentWorld Vault Route Blocker

Problem: `PersistentWorldRegistry` owns many private persistent native containers, but the project has no PersistentWorld-owned `BufferID` lane or route card for live records, chunk indexes, tombstone sets, hydration queues, entity-state maps, or spawn impulse maps.
Solution: Recorded the blocker after read-only route inspection. Full eviction is deferred until approved PersistentWorld buffer IDs and generation-handle ownership exist. Existing local ownership remains ugly but single-owner.
Rejected Alternatives: Reusing `FaunaSimulationPoolSlots` was rejected because it is AI-owned and represents a different fact. Reusing save-compression BufferIDs was rejected because those lanes are staging DTOs for Merkle/entity delta compression, not live world persistence authority.
Scalability potential: Low/Middle/High/Ultra unchanged until route card exists. Correct route design prevents save/sector paging corruption under every quality tier.
Hardware Impact: 0 us runtime change; prevents a high-risk false migration that would merge unrelated authority lanes.

## BIOS Scanner Loot Sphere AUP Repair

Problem: The BIOS scanner diagnostic passed loot sphere centers to the shader as large absolute floats, and the shader reconstructed `absoluteWorld` by adding `_TotalUniverseOffset` to runtime depth positions.
Solution: `HectonScanRenderRegistry` now subtracts `GlobalSignals.CurrentRuntimeOriginAup()` from the cached loot center in double precision, downcasts only the runtime-local delta, and the shader compares depth-derived runtime `worldPos` directly against that local sphere.
Rejected Alternatives: Keeping `_TotalUniverseOffset` in shader math was rejected because it reintroduces absolute-float precision loss at large world offsets. A CPU physics probe for scanner highlighting was rejected because this is a visual diagnostic fake and already has depth buffer data.
Scalability potential: Low uses the same cheap full-screen depth compare. Middle/High/Ultra can spend BIOS intensity on richer shader grain/scanline treatment without changing coordinate authority.
Hardware Impact: 0 us CPU claim; removes boundary shimmer risk and one shader vector add while preserving the Dear Lie screen-space highlight path.

## Seismic Fault Fallback Native Clear

Problem: Seismic static-data, legacy binary, and emergency fallback loaders cleared `NativeArray<SeismicEventDTO>` slots with scalar loops or could leave stale slots after invalid legacy records.
Solution: Added a shared unsafe `ClearSeismicEvents` helper using `NativeArrayUnsafeUtility.GetUnsafePtr` and `UnsafeUtility.MemClear`. Static-data load, legacy binary load, and `GenerateEmergencyMockFaults` now clear the Vault-owned event array before installing sanitized finite records.
Rejected Alternatives: Changing `SeismicEventDTO` from 40 bytes to 64 bytes was rejected because the legacy binary ABI uses 40-byte records and needs a route-card/importer migration before layout expansion. Managed staging arrays were rejected because fallback hydration targets native Vault storage.
Scalability potential: Low/Middle/High/Ultra unchanged; fallback data remains deterministic and cheap, with no quality-dependent truth changes.
Hardware Impact: 0 us hot-path claim; cold fallback clear changes from per-element scalar stores to one native bulk clear and prevents stale seismic events after invalid records.

## Seismic Evaluation Alias Metadata

Problem: `SeismicEvaluationJob` output pointers and the `NativeQueue<SeismicShockwaveSignal>.ParallelWriter` were architecturally write-only but lacked explicit `[WriteOnly]` metadata.
Solution: Marked shake, turbidity, telemetry, mock-silt pointers and shockwave queue writer as `[WriteOnly, NoAlias]`, leaving `Events` read/write because the job reads magnitude/epicenter and decays slots in place.
Rejected Alternatives: Marking `Events` write-only was rejected because it is a legitimate read/write state lane. Splitting decay into a second job was rejected because it would add a dependency edge and extra scheduling overhead for one compact quake table.
Scalability potential: Low/Middle/High/Ultra unchanged; quality still scales with continuous `SystemHealthIndex` and the alias metadata only improves Burst memory reasoning.
Hardware Impact: Static estimate below 1 us steady-state; primary gain is vectorization confidence and explicit queue output contract.

## Cultivation Slot ARM64 Layout

Problem: `CultivationSlotState` is stored in a `NativeArray` and consumed by atmosphere/cultivation scans, but its 8-byte genetics lane sat after a 4-byte item hash plus padding.
Solution: Reordered the explicit layout to put `GeneticsMask` at offset 0, `SeedItemHashId` at 8, growth/quality floats at 12/16, and tail padding at 20/24. Added `[BinaryBlittableSafe]` and `BinaryLayoutManifest` assertions.
Rejected Alternatives: Migrating the private cultivation slot array to Vault was rejected in this pass because no `BufferID.Cultivation*` route exists. Changing save payload shape was rejected because save code serializes field values separately and does not need ABI churn.
Scalability potential: Low/Middle/High/Ultra unchanged; cultivation truth remains a fixed four-slot table independent of quality weight.
Hardware Impact: Static estimate below 1 us per cultivation/atmosphere scan; primary gain is ARM64-safe sorted layout and cold-boot manifest proof.

## Survival Legacy Database Split Removal

Problem: `HectonSurvivalSystem` retained a legacy injected survival database parser that tokenized headers and rows with `Split('|')`, allocating a managed string array for every parsed line even though the active runtime parser below already uses span-delimited native row hydration.
Solution: Replaced header and row tokenization with the existing `TryReadNextDelimitedToken` cursor and `TrimSurvivalDatabaseSpan` helper. Removed the stale string hash overload and forced hash parsing through the span overload. The only remaining string allocation in that legacy route is `stableId.ToString()` at the managed `SurvivalDatabaseItemParameters` DTO constructor boundary; the live runtime route still writes `SurvivalDatabaseItemRecord` into a native temp buffer.
Rejected Alternatives: Deleting the whole legacy overload was rejected in this pass because it is private but interleaved with existing injected database code and broader removal would risk changing editor/dev injection behavior without a dedicated route-card review. Keeping `Split` was rejected because it violates the CSV/token hardening mandate even on cold paths.
Scalability potential: Low tier avoids avoidable cold-load allocation spikes during injected table hydration. Middle/High/Ultra use the same active native database row path and can spend saved load budget on richer survival feedback without changing gameplay truth.
Hardware Impact: 0 us hot-path claim. Cold parse removes one managed `string[]` allocation per header/row line and one obsolete string-normalization path.

## Voxel Sculptor CSV Split Removal

Problem: `ShinobuVoxelSculptorWindow` is editor-only under `#if UNITY_EDITOR`, but it lives in the runtime script tree and retained a tuning CSV importer that called `Split(',')` per data row.
Solution: Replaced row tokenization with `ReadOnlySpan<char>` cursor reads, ASCII-only case-insensitive header detection, and span-based float/int parsing. `_Project/Scripts` excluding Editor/Test folders now has no `Split(` hits.
Rejected Alternatives: Moving the file into an Editor folder was rejected because that is a project layout change outside this task. Keeping `Split` because the code is editor-only was rejected because it leaves false positives in runtime-tree scans and weakens the CSV hardening proof.
Scalability potential: Low tier and mobile builds do not compile this file. Middle/High/Ultra editor tooling gets the same tuning values with less cold garbage during CSV import and binary bake.
Hardware Impact: 0 us runtime. Cold editor import removes one `string[]` allocation per parsed tuning row.

## Scatter Backend Native Layout Hardening

Problem: The scatter hybrid backend used NativeArray rows and Burst/job transfer DTOs with default layout, a `bool` candidate validity lane, and accessor-based result/parity/schedule payloads. The shadow-completion payload also stored a managed `string` status label beside numeric parity state.
Solution: Converted scatter quota, cell-state, parity, config, candidate, parity-reference, schedule-request, and shadow-completion payloads to explicit layouts sized 16, 32, 64, 96, or 128 bytes. Candidate validity is now byte-backed. Shadow completion carries a byte parity status code and resolves the debug label only at the director boundary. `BinaryLayoutManifest.VerifyWorldScatterLayouts` now asserts the static offsets through external-contract checks without adding a sibling assembly dependency to World.Contracts.
Rejected Alternatives: Expanding this into a full Vault migration was rejected because scatter working memory currently owns local scene scratch and no approved scatter `BufferID` route exists. Rewriting voxel mesh vertex structs was rejected because their 76-byte stride is a shader/mesh ABI. Keeping the managed shadow status string inside the payload was rejected because it mixed debug presentation with numeric transfer state.
Scalability potential: Low tier keeps the same bounded scatter cell/candidate count and benefits from smaller proofable payloads with byte flags. Middle/High/Ultra can spend scatter budget on richer BRG/vegetation visuals while the gameplay/presentation seam keeps the same DTO layout and authority route.
Hardware Impact: Static estimate below 1 us per scatter pass on i3/MX350-class CPU; primary gain is ARM64-safe field order, one-cache-line candidate rows, a 64-byte atomic counter row, and removal of accessor/string/bool payload hazards from the Burst-backed scatter seam.

## Scatter Backend Binding Sentinel Registration

Problem: `ScatterBackendBindingState` allocates persistent scene-lifetime height and cell-state bridge `NativeArray` buffers, but those arrays were not registered with `NativeMemorySentinel`.
Solution: Added owner/lifetime constants, registered both bridge arrays immediately after allocation, and routed resize/dispose through a single unregister-then-dispose helper.
Rejected Alternatives: Moving these buffers to `GlobalDataVault` was rejected because they are local scene scratch for the scatter shadow backend and no approved scatter `BufferID` route exists. Leaving them unregistered was rejected because persistent allocations require sentinel visibility even when local ownership is acceptable.
Scalability potential: Low/Middle/High/Ultra unchanged; this hardens allocation accounting without changing scatter truth, DTO layout, save identity, or quality-tier behavior.
Hardware Impact: 0 us hot-path claim. The value is leak/fragmentation forensic visibility and correct scene-lifetime teardown tracking.

## Scatter Working Memory Bulk Zero

Problem: `WorldProceduralScatterDirector.ScatterWorkingMemory.ResetGridPlacementSpatialCache` cleared four native scratch arrays through a scalar generic loop, despite every call passing zero for unmanaged int/float lanes.
Solution: Replaced the value-writing loop with an unsafe `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks` plus `UnsafeUtility.MemClear` helper constrained to unmanaged arrays.
Rejected Alternatives: Keeping the generic value loop was rejected because the call sites only require zero clears and the scalar loop hides reset cost in the scatter streaming path. Moving all scatter working memory to `GlobalDataVault` was rejected because no approved scatter `BufferID` route exists.
Scalability potential: Low tier benefits from cheaper cache-reset maintenance during constrained scatter budgets. Middle/High/Ultra can spend the same scatter authority route on richer BRG placement density without changing DTO shape or ownership.
Hardware Impact: Static estimate below 1 us per scatter cache reset on i3/MX350-class CPU; replaces four small scalar loops with native bulk zero, no new gameplay allocation.

## Biome Transition Alias Tightening

Problem: The biome transition runtime was already Vault-backed and explicitly laid out, but several Burst jobs still exposed output-only `NativeArray` and signal writer lanes as plain `[NoAlias]`, forcing conservative alias assumptions around atmosphere, mask, shader payload, acoustic stage, telemetry, emergency mock, and CSV ingest writes.
Solution: Added `[WriteOnly, NoAlias]` to the proven output-only lanes. `BlendAtmosphereJob` now keeps a local `BiomeBlendMaskDTO` copy for hashing instead of reading back `BlendMask[0]`, preserving the mask buffer as an output-only lane.
Rejected Alternatives: Expanding DTO layouts was rejected because `BiomeTransitionNativeLayout.Validate()` already proves explicit 8/32/64/128-byte layouts and Vault BufferIDs exist. Marking read/write `Counters` blindly as write-only was rejected where jobs legitimately read counter state before mutation.
Scalability potential: Low tier benefits because GlobalQualityWeight already reduces scan count and blend count continuously. Middle/High/Ultra keep the same Vault truth and can spend quality on four-way fog/audio blending and richer shader payloads without changing authority routes.
Hardware Impact: Static estimate below 1 us per transition cadence on i3/MX350-class CPU; primary gain is improved Burst vectorization and fewer hidden readback hazards on output lanes.

## BRG Vegetation Job Bool Payload Purge

Problem: Shared BRG helper jobs and the vegetation renderer's BRG culling/finalize jobs used public `bool` fields inside Burst job structs. That makes the job payload layout less explicit across x86/ARM64 and violates the runtime transfer-shape rule even though the branch semantics are binary.
Solution: Replaced job bool fields with byte-backed `*Flag` fields at the scheduler boundary. The producer visibility masks are now `[WriteOnly, NoAlias]`. Managed public helper APIs still accept bool where they are not Burst payloads.
Rejected Alternatives: Collapsing every flag into one bitfield was rejected in this pass to keep the patch local and reduce call-site risk; byte flags already remove managed bool payload ambiguity. Rewriting the BRG path into a new indirect renderer was rejected because the existing code already uses BRG/indirect draw semantics and only needed payload hardening.
Scalability potential: Low tier keeps density decimation, CPU culling, darkness bypass, far/shadow/motion passes selected by existing continuous quality/cadence decisions. Middle/High/Ultra can spend saved culling stability on denser vegetation BRG draws without changing authority or allocation routes.
Hardware Impact: Static estimate below 1 us per BRG cull callback on i3/MX350-class CPU; primary value is deterministic job ABI, fewer platform-specific bool layout hazards, and cleaner visibility-mask producer aliasing.

## Thermal Slumping Bool Payload Purge

Problem: `ThermalSlumpingJob` is a Burst terrain deformation job and carried a public `bool WriteWearMask` field for an optional native write lane.
Solution: Replaced the bool with `byte WriteWearMaskFlag` and updated all schedule sites in editor smoke tests, the erosion harness, and the MapMagic erosion node.
Rejected Alternatives: Removing the wear-mask lane was rejected because the job legitimately supports an optional deformation proof artifact. Collapsing this into a larger erosion refactor was rejected because the safe boundary is the single job ABI flag.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; terrain deformation still uses the same deterministic solver and optional wear output while avoiding platform-sensitive bool layout in the job payload.
Hardware Impact: No runtime hot-path timing claim; removes one Burst job bool lane and keeps the optional native write branch explicit.

## Procedural Wreck Mesh Merge Flag Packing

Problem: `CombineMeshDataJob` merges wreck module mesh data in Burst and carried three public bool fields for normal, UV, and color stream availability.
Solution: Replaced the three bool fields with one `uint AttributeFlags` mask and constants for normals, UVs, and colors. Both synchronous and async construction sites now compose the bitmask at the managed boundary.
Rejected Alternatives: Keeping three byte fields was rejected because a single mask is a smaller, stable transfer lane and keeps future attribute expansion cheap. Rewriting the mesh merger into a new GPU path was rejected because this patch targets job payload ABI and the existing path already uses `Mesh.MeshData` plus native arrays.
Scalability potential: Low tier keeps the procedural color fallback when color streams are absent. Middle/High/Ultra keep the same mesh merge route and can spend budget on richer wreck visuals without changing save identity or authority ownership.
Hardware Impact: Static estimate below 1 us per wreck mesh copy job; primary gain is deterministic job payload shape and branch-state packing.

## Lore Unlock Native Sentinel Registration

Problem: `LoreDatabaseManager` allocated the persistent `_unlockedWords` native bitmask without registering it with `NativeMemorySentinel`.
Solution: Added a session-lifetime native owner label, registered `_unlockedWords` immediately after allocation, and unregistered it before deferred disposal in `OnDestroy`.
Rejected Alternatives: Moving the two-word lore unlock bitmask to `GlobalDataVault` was rejected because no lore unlock BufferID route exists and the fact remains save-owned/manager-local. Completing the deferred dispose synchronously was rejected because that would add a shutdown blocking edge without need.
Scalability potential: Low/Middle/High/Ultra unchanged; this is allocation-forensics hardening for a fixed two-word unlock bitmask.
Hardware Impact: 0 us hot-path claim; improves persistent native allocation visibility and leak detection.

## Indirect Vegetation Native Read Token Accessor Purge

Problem: `HectonIndirectVegetationNativeReadBuffer` is a native front/back buffer transfer token carrying NativeArrays and a JobHandle, but it exposed accessor-backed properties and a bool `HasExplicitBounds`.
Solution: Replaced accessor properties with raw readonly fields, converted explicit-bounds state to `byte HasExplicitBoundsFlag`, and moved validity/bounds checks to static `in` helpers. Renderer sync now calls those helpers instead of property accessors.
Rejected Alternatives: Applying `[StructLayout(LayoutKind.Explicit)]` to this token was rejected because it contains `NativeArray<T>`, `JobHandle`, and `Bounds` transport wrappers rather than a NativeArray element or binary DTO. Keeping `IsValid` as a property was rejected because the token is passed by value in a hot sync seam.
Scalability potential: Low tier keeps native buffer sync cheap for reduced vegetation counts. Middle/High/Ultra can keep richer vegetation readback/upload density without changing producer ownership or BRG draw routes.
Hardware Impact: Static estimate below 1 us per native buffer sync; removes accessor methods and a managed bool lane from a native transfer token.

## MapMagic Height Payload Accessor Purge

Problem: `HectonMapMagicVegetationBridge.TerrainHeightSamplePayload` exposed native height samples through get-only properties and an `IsValid` property; `MapMagicBridge.QuantizedHeightmapPayload` still exposed validity as a property even though it is consumed by fauna, geology, and ore placement payload resolution.
Solution: Converted the terrain sample token to raw readonly fields and moved both payload validity checks to static `in` helpers. Updated runtime bridge, fauna terrain fallback, geology seam copy, and procedural ore payload call sites to use the static helper predicates.
Rejected Alternatives: Applying explicit layout to these tokens was rejected because they carry `NativeArray<T>` transport handles and `Vector3` wrapper values rather than NativeArray element rows or binary records. Replacing the existing heightmap alias with a copied Vault buffer was rejected because these paths only validate and forward the bridge-owned sample alias.
Scalability potential: Low tier keeps terrain fallback payload validation cheap while using reduced terrain/vegetation density. Middle/High/Ultra can keep richer terrain sampling and ore/geology visuals without changing the MapMagic owner route.
Hardware Impact: Static estimate below 1 us per terrain-height payload resolution on i3/MX350-class CPU; primary gain is removing accessor methods from native heightmap transfer tokens and keeping call sites by-value safe through `in` predicates.

## Scatter Runtime Rule Token Accessor Purge

Problem: Scatter scoring and reconciliation repeatedly pass `ScatterRuntimeRuleEntry`, scoring contexts, previews, and candidates by value/in-ref, but those tokens still exposed get-only auto-properties. The payloads are not blittable Burst DTOs because they carry managed rule/family/string references, yet the accessor layer still adds unnecessary method surfaces in a dense managed hot seam.
Solution: Converted `ScatterRuntimeRuleEntry`, `ScatterBiomeScoreContext`, `ScatterPatternScoreContext`, `ScatterCandidatePreview`, `ScatterPreviewGizmoRecord`, and `ScatterCandidate` to raw readonly fields while preserving names and existing owner routes.
Rejected Alternatives: Forcing explicit layout was rejected because these tokens include managed references and arrays and are not NativeArray elements or binary rows. Moving runtime rules into Vault in this pass was rejected because placement rules/families remain authoring-owned managed assets; a numeric baked rule table would need a route-card and data-monolith migration.
Scalability potential: Low tier benefits from cheaper managed scatter scoring when density/cadence are reduced. Middle/High/Ultra keep the same rules but can spend saved CPU on denser BRG placement and richer biome-pattern visuals.
Hardware Impact: Static estimate below 1 us per scatter scoring/reconcile pass on i3/MX350-class CPU; primary gain is property/accessor removal on the candidate/rule token path.

## Terrain Hole Native Bool Eviction

Problem: `TerrainHoleMaskBuildJob` wrote a `NativeArray<bool>` and consumed default-layout `TerrainHoleRecord` rows. Native bool lanes have platform-sensitive representation, and the terrain-hole record is a Burst input NativeArray row without an explicit ARM64 layout proof.
Solution: Replaced the native mask with `NativeArray<byte>` where `1` means terrain remains and `0` means hole. The Unity-facing `bool[,]` staging buffer is filled from byte flags only after the dispatcher-owned completion check. `TerrainHoleRecord` now has `[StructLayout(LayoutKind.Explicit, Size = 32)]` with float lanes at offsets 0..19, `HoleId@20`, `SourceType@24`, and 7 bytes of tail padding. The job marks terrain holes as `[ReadOnly, NoAlias]` and output as `[WriteOnly, NoAlias]`.
Rejected Alternatives: Removing the managed `bool[,]` staging buffer was rejected because `TerrainData.SetHolesDelayLOD` requires that API shape. Keeping `NativeArray<bool>` was rejected because it leaves native transfer representation to compiler/runtime implementation details.
Scalability potential: Low tier keeps tile-hole masking deterministic with byte flags and smaller validation overhead. Middle/High/Ultra can spend terrain/vegetation budget on denser cave and wreck masks without changing the Unity boundary.
Hardware Impact: Static estimate below 1 us per tile terrain-hole mask job on i3/MX350-class CPU; primary gain is deterministic native flag width, explicit 32-byte hole rows, and Burst alias metadata.

## World Streaming Row Layout Pinning

Problem: `TerrainHoleStreamingRecord` and `HLODData` are published through NativeArrays to voxel/vegetation/render consumers but still relied on default field layout.
Solution: Added explicit layouts: `TerrainHoleStreamingRecord` is 32 bytes with `Position@0`, `Radius@12`, `HoleId@16`, `SourceType@20`, and pad bytes 21..31; `HLODData` is 48 bytes with `Center@0`, `Size@12`, `Fade01@24`, `StructureId@28`, `Type@32`, and pad bytes 33..47.
Rejected Alternatives: Leaving these as default-layout serializable structs was rejected because they are native streaming rows. Reworking `ArtificialInteriorState` was rejected in this pass because it is owner-local managed state and not a NativeArray row.
Scalability potential: Low tier has deterministic small HLOD/terrain-hole rows. Middle/High/Ultra can increase visible HLOD counts without hidden row-size drift across ARM64/x86.
Hardware Impact: Static estimate below 1 us per streaming/HLOD scan; primary gain is predictable row stride and field offsets for NativeArray consumers.

## Cave Graph Temp Native Bool Eviction

Problem: `CaveGraphGenerator.GenerateEntrances` used `NativeArray<bool>` for temporary used-room scratch. It is not persistent, but it still leaves native bool representation in a runtime generation path.
Solution: Replaced the temp native bool array with `NativeArray<byte>` and used `0/1` flags for room selection.
Rejected Alternatives: Replacing it with a managed `bool[]` was rejected because the generator already operates around native output containers and this scratch must not add managed allocation pressure. Keeping native bool because it is `Allocator.Temp` was rejected because the mandate is representation hygiene, not just lifetime hygiene.
Scalability potential: Low tier cave generation keeps deterministic byte flags. Middle/High/Ultra can spend generation budget on richer cave topology without bool-width ambiguity.
Hardware Impact: Static estimate below 1 us in cold cave generation; primary gain is eliminating the last runtime-script `NativeArray<bool>` hit found by the sweep.

## Scatter Runtime Context Accessor Purge

Problem: Scatter runtime sampling/reconcile still carried get-only properties on transfer/context structs (`SamplingSnapshot`, `ScatterSamplingBeginContext`, `ScatterBiomeTransitionContext`, `ScatterBackendRuntimeStatus`) plus accessor-heavy `ScatterPlacement` class state in the dense placement reconciliation seam.
Solution: Converted the context/status/snapshot structs to raw readonly fields, changed secondary/status flags to byte-backed lanes where practical, and changed `ScatterPlacement` property state to raw fields. The computed runtime-space position is now `ReadRuntimePosition()` so call sites do not hide coordinate conversion behind a property accessor.
Rejected Alternatives: Forcing explicit layout was rejected for these managed/reference-bearing tokens because they carry strings, `IReadOnlyList`, Unity objects, and authoring profile references rather than NativeArray element rows or binary DTOs. Moving scatter managed rule/placement state to Vault was rejected because no approved scatter placement BufferIDs or numeric data-monolith rule table exist.
Scalability potential: Low tier keeps the scatter cadence and budgets cheap while removing accessor dispatch from the active placement/reconcile path. Middle/High/Ultra can spend the same route on denser BRG placement and richer biome transition visuals without changing DTO layout, save identity, or authority ownership.
Hardware Impact: Static estimate below 1 us per scatter sampling/reconcile pass on i3/MX350-class CPU; primary gain is eliminating property calls and bool-backed transfer flags on hot managed scatter seams.

## Fauna Perception Snapshot Flag Packing

Problem: `FaunaPerceptionSnapshot` carried seven public bool transfer fields between `FaunaBrain` and `FaunaSensorSuite`, a hot sensory seam executed by active fauna.
Solution: Replaced bool lanes with one `uint Flags` bitmask and static `in` predicates. `FaunaBrain` now sets flag bits at the producer boundary, and `FaunaSensorSuite` reads the predicates without field-width ambiguity.
Rejected Alternatives: Explicit layout was rejected because this snapshot carries a managed `Component` reference and Unity `Vector3` values, so it is not a NativeArray/binary/Burst DTO. Leaving bool fields was rejected because the transfer shape is hot and already has a natural bitmask representation.
Scalability potential: Low tier keeps sensory snapshots compact when fauna cadence is reduced. Middle/High/Ultra can run richer sensory contact, flashlight, and scavenge-tool checks without changing ownership or adding SignalBus lanes.
Hardware Impact: Static estimate below 1 us per active fauna sensory tick on i3/MX350-class CPU; primary gain is compact flag reads and removing bool field representation from a repeated hot snapshot.

## Indirect Vegetation Binding Cache Flag Packing

Problem: `HectonIndirectVegetationRenderer` still stored public bool lanes inside repeated render/culling cache structs for material bindings, compute bindings, indirect-args clear state, and CPU culling scratch activity.
Solution: Replaced those bool lanes with byte-backed flags and updated all assignment/comparison sites to compare `!= 0`. The structs still carry managed Unity objects and native handles, so they remain managed cache records rather than explicit-layout NativeArray rows.
Rejected Alternatives: Applying explicit layout was rejected because the structs contain `Material`, `ComputeShader`, `GraphicsBuffer`, `Mesh`, `NativeArray<T>`, and `JobHandle` transport wrappers; forcing offsets there would be false ABI theater. Rebuilding the renderer around a new Vault-owned binding table was rejected because this state is owner-local render cache, not gameplay truth or rollback state.
Scalability potential: Low tier keeps the same continuous density decimation, far-culling cadence, darkness culling, and HZB/indirect draw routes while reducing hot cache flag ambiguity. Middle/High/Ultra can retain richer vegetation passes and motion/shadow/depth variants without changing authority routes.
Hardware Impact: Static estimate below 1 us per render/cull path on i3/MX350-class CPU; primary gain is deterministic flag width, simpler cache comparisons, and less platform-sensitive bool state in repeated vegetation binding paths.

## TBDR Native Support Flag Packing

Problem: `TBDRVertexBudgetVault`, `TBDRTextureStreamingTracker`, and `TBDRPipelineTelemetryRecorder` used public bool lifecycle flags around persistent/Vault-backed native buffers and the 300-frame telemetry ring.
Solution: Added `TBDRByteFlags` and replaced Vault ownership, external-ring ownership, and dump-state booleans with byte-backed fields. Disposal, registration, and dump guards now compare the byte flags directly.
Rejected Alternatives: Reworking these records into a new global service was rejected because they already use approved `TBDRBufferIds` for production Vault routes and documented cold fallback paths for CI/mock use. Explicit layout was rejected for these containers because they own `NativeArray<T>` and Unity resource references rather than serving as NativeArray element rows.
Scalability potential: Low tier keeps fixed-budget tile/vertex/texture telemetry without bool lane ambiguity. Middle/High/Ultra can raise visible vertex and texture-residency budgets through the existing continuous quality route without changing ownership flags or native row layouts.
Hardware Impact: 0 us hot-path timing claim. The gain is lifecycle determinism and removal of public bool fields from native-support records that gate registration, disposal, and blackbox dump behavior.

## DRS/WaterOptics Output Alias Tightening

Problem: Bilateral DRS and WaterOptics jobs already had explicit DTO layouts and `[NoAlias]`, but output-only `NativeArray` lanes were still missing `[WriteOnly]`.
Solution: Marked the DRS mock-state lane, DRS parameter output lane, DRS telemetry output lane, WaterOptics mock output lane, and WaterOptics mapped-buffer destination lane as `[WriteOnly, NoAlias]`.
Rejected Alternatives: Rewriting the DRS and WaterOptics schedulers was rejected because the jobs already return handles through the dispatcher-owned path and their DTOs already use explicit 32/64-byte layouts. Marking `TelemetryCursor` write-only was rejected because the job reads the current cursor before writing the next value.
Scalability potential: Low tier keeps DRS/water shader parameter generation cheap and clear under reduced render scale. Middle/High/Ultra can preserve richer bilateral and spectral-water visuals without changing the Vault route or shader buffer ABI.
Hardware Impact: Static estimate below 1 us per render parameter cadence on i3/MX350-class CPU; primary gain is Burst alias/write proof on GPU-constant producer jobs.

## SeedShip Anomaly Job Alias Tightening

Problem: SeedShip anomaly Burst jobs used `[NoAlias]` but omitted read/write intent on producer-only signal/telemetry lanes and the rebase input lane.
Solution: Added `[WriteOnly, NoAlias]` to the mock rebase producer and to field-job glitch/HUD/thermal/telemetry outputs. Added `[ReadOnly, NoAlias]` to the field-job rebase input. Kept field, tuning, globals, and leviathan arrays read-write because the job reads and mutates them.
Rejected Alternatives: Marking all lanes write-only was rejected because `Field`, `Tuning`, `Globals`, and `Leviathans` are read-modify-write state. Replacing the anomaly shader/radar presentation with heavier physics was rejected; the domain already uses a deterministic field and shader command fake.
Scalability potential: Low tier keeps one deterministic field update plus shader/HUD/radar output lanes. Middle/High/Ultra can spend visual budget on richer glitch/noise/shader presentation without changing the single-field truth owner.
Hardware Impact: Static estimate below 1 us per anomaly cadence on i3/MX350-class CPU; primary gain is Burst alias proof on non-overlapping command and telemetry buffers.

## Procedural Coral/Wreckage Output Alias Tightening

Problem: Procedural coral and wreckage generation jobs already use GPU-driven indirect draw, HZB culling, and deterministic fake-growth/debris math, but many producer-only `NativeArray<T>` lanes still carried only `[NoAlias]`. Burst could not distinguish render/telemetry/loot/proxy output buffers from read-write state lanes.
Solution: Added `[WriteOnly, NoAlias]` to proven producer-only lanes: sector trigger outputs, coral L-system telemetry, coral spatial cell output, coral render matrices, coral indirect args, coral GPU sway, sync pulses, coral collision proxies, coral self-audit results, wreckage collapse node output, wreckage collapse telemetry, wreckage debris output, wreckage render matrices, wreckage indirect args, wreckage GPU scalars, loot requests, wreckage collision proxies, and wreckage self-audit results. Counters, grids, branch state, debug cells, telemetry cursors, and post-render telemetry patch lanes stay read-write because the job body reads them before writing.
Rejected Alternatives: Marking every `[NoAlias]` lane as write-only was rejected because that would lie to Burst for counters/grid/state arrays. Replacing coral/wreckage fake-growth with physics or instantiated GameObjects was rejected; the current path is already the correct Dear Lie: deterministic L-system/WFC rows generate matrices and indirect args for GPU presentation.
Scalability potential: Low tier collapses density and distance through existing `GlobalQualityWeight` curves while producing fewer matrices/proxies. Middle/High/Ultra keep the same authority route and can spend saved CPU on denser coral/wreck matrices, richer sway/rust/silt shader scalar payloads, and deeper HZB-filtered draw lists.
Hardware Impact: Static estimate below 1 us per generation/render extraction cadence on i3/MX350-class CPU; primary gain is compiler alias proof on non-overlapping GPU/Signal staging lanes and better NEON/AVX vectorization eligibility.

## Delta Crusher Output Alias And Continuous Cap Curve

Problem: `ShinobuDeltaCrusherJobs` had output-only native lanes without `[WriteOnly, NoAlias]`, and debris capacity/particles-per-carve still used binary low/high tier branching through `ResolveDebrisCap(bool lowTier, bool highEndTier, ...)`.
Solution: Added explicit native access metadata to producer-only Delta Crusher lanes and read-only input lanes. Replaced the binary cap helper with `ResolveDebrisCap(float globalQualityWeight01, int configuredCap)` using `SmoothQuality01` and `math.lerp`. `CarveDebrisComputeRenderer` now caches `SignalBusRegistry.GlobalQualityWeight01` once per tick and uses it for active capacity and particles per carve.
Rejected Alternatives: Leaving tier booleans as the primary cap route was rejected because it creates visual popping and violates the continuous quality law. Rewriting the GPU renderer around CPU debris rigidbodies was rejected; this system is already a Dear Lie using compute advection, indirect args, SDF/flow sampling, and compact mirror uploads.
Scalability potential: Low tier smoothly converges to 500 active debris and 16 particles per carve. Middle tier moves through the existing 4096-ish range without a branch cut. High/Ultra ramps toward 10000 active debris and 128 particles per carve, buying richer visual rock-chip density without changing gameplay truth.
Hardware Impact: Static CPU estimate below 1 us; primary gain is reduced branch discontinuity and stricter Burst alias proof. GPU work now scales continuously with thermal quality instead of snapping across low/high tier predicates.

## Russell Scan Top-Hit Alias Pass

Problem: The delegated source scan identified remaining Burst job payload fields where the job body proves output-only or read-only behavior, but metadata still exposed vague mutable `NativeArray<T>` lanes.
Solution: Added `[WriteOnly, NoAlias]` to `ScannerSpatialQueryJob` result/stat outputs, `AcousticPathJob.Result`, `DebrisSimulationJob.WriteStates`, combat damage result lanes, and combat status active/result lanes. Added `[ReadOnly, NoAlias]` to proven read-only scanner/acoustic/debris/combat input lanes and `[NoAlias]` to read-write scratch/state lanes. Changed combat jobs from bare `[BurstCompile]` to deterministic compile flags because they mutate authoritative health/status state.
Rejected Alternatives: Marking combat health/status/counters or acoustic scratch as write-only was rejected because those lanes are read before write. Splitting those jobs into separate input/output arrays was rejected in this micro-pass because it changes ownership and memory footprint across gameplay systems without route-card proof.
Scalability potential: Low tier benefits from clearer Burst contracts in scanner/audio/combat/debris jobs while cadence remains reduced by upstream systems. Middle/High/Ultra can keep richer scanner query, acoustic path, and combat status feedback without hidden alias pessimization.
Hardware Impact: Static estimate below 1 us per affected cadence on i3/MX350-class CPU; primary gain is stronger vectorization eligibility and deterministic Burst compile behavior for authoritative combat state.

## Russell Alias Namespace Verification

Problem: The alias metadata pass needed namespace verification before compile. A stale assumption treated `[NoAlias]` as an unsafe-namespace attribute, but the local Burst package defines `NoAliasAttribute` in `Unity.Burst`.
Solution: Verified `Library/PackageCache/com.unity.burst*/Runtime/NoAliasAttribute.cs`, confirmed Debris/Combat already import `Unity.Burst`, and removed the extra unsafe namespace imports that were not required.
Rejected Alternatives: Keeping unused imports was rejected because compile-wall discipline includes tight using surfaces. Removing `[NoAlias]` was rejected because it weakens Burst alias proof.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this preserves the alias proof path for all hardware tiers.
Hardware Impact: 0 us runtime; this is compile-hygiene for the existing alias optimization.

## Combat Damage Quality Truth Detachment

Problem: `CombatDamageRuntime` used a binary low/high math LOD derived from global scalability tier and math precision inside `ProcessDamageQueueJob`. That branch could change armor-projection damage, which makes hardware quality part of authoritative health truth.
Solution: Removed scalability-tier and math-precision polling from the combat policy. The Burst job now always evaluates directional armor proof deterministically for damage truth. `SignalBusRegistry.GlobalQualityWeight01` is cached once at schedule time and drives only visual wound detail: surface-normal amplitude and deterministic high-fidelity wound dither.
Rejected Alternatives: Keeping the low/high damage branch was rejected because quality must not change gameplay truth. Making damage continuously scale with quality was rejected for the same reason. A new combat Vault route was rejected because no CombatDamageRuntime-owned BufferIDs or route card exist.
Scalability potential: Low tier collapses wound-detail normals and high-fidelity wound markers through a smooth quality curve. Middle gradually emits more detailed visual feedback. High/Ultra reaches full wound normals and dense wound-detail markers without changing health, status, or authority ownership.
Hardware Impact: Static estimate below 1 us per combat batch on i3/MX350-class CPU; primary gain is deterministic rollback safety and removal of hot `GlobalRegistry` tier polling.

## Inventory Economy Burst Determinism Flags

Problem: `Shinobu19EconomyLedger` had fifteen authoritative inventory, crafting, loot, RLE, and telemetry jobs using bare `[BurstCompile]`, leaving compile mode implicit for state mutations that must survive rollback and cross-platform replay.
Solution: Replaced every bare inventory ledger Burst attribute with `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
Rejected Alternatives: Using `FloatMode.Fast` was rejected because the ledger mutates authoritative gameplay inventory state. Leaving bare attributes was rejected because compile settings become implicit and harder to audit.
Scalability potential: Low/Middle/High/Ultra all use identical inventory truth; quality may affect UI/visual cadence elsewhere, not ledger transaction results.
Hardware Impact: 0 us steady-state claim; this hardens deterministic compiler settings rather than adding a new optimization path.

## Inventory Economy Alias Metadata Pass

Problem: Inventory ledger jobs had deterministic Burst attributes after the previous pass, but their `NativeArray` lanes still lacked explicit alias/read/write proof. Recipe/query lanes, result lanes, and transaction arrays were all exposed as generic mutable buffers.
Solution: Added `[ReadOnly, NoAlias]` to immutable recipe, hash, quantity, physical-constant, hotbar, and debris-signal input lanes. Added `[WriteOnly, NoAlias]` to result, telemetry, carry total, equip, broken-tool, destroyed-debris, craftable, accepted, and mock consume output lanes. Kept inventory hash/quantity/durability lanes read-write with `[NoAlias]` where helper calls mutate item state.
Rejected Alternatives: Marking transaction arrays as write-only was rejected because the jobs read current slots before mutation. Splitting inventory into separate input/output arrays was rejected because it changes ledger ownership and rollback memory footprint.
Scalability potential: Low tier gets clearer Burst contracts during sparse ledger/crafting ticks. Middle/High/Ultra can run denser loot/crafting query batches without alias pessimization, while inventory truth remains quality-invariant.
Hardware Impact: Static estimate below 1 us per ledger batch on i3/MX350-class CPU; primary gain is vectorization eligibility and clearer job contracts.

## Player/Vehicle CCD Quality Truth Detachment

Problem: Player and vehicle scheduled sweep resolution used `KinematicCcdMath.IsLowTier(...)` to change CCD truth. On low tier, slide projection, voxel-proxy slide velocity, SDF squeeze, corner behavior, and projected velocity could differ from high tier.
Solution: Removed hardware tier from the CCD decision in both `HectonPlayerMotor` and `VehicleMotor`. `lowTierStop` is now false for authoritative sweep resolution, so geometry and collision normals decide the outcome. Existing impact flags remain available for corner halt, but low-tier stop is no longer emitted from CCD truth.
Rejected Alternatives: Continuously blending collision response by quality was rejected because movement/collision truth must not depend on hardware pressure. Keeping low-tier hard stop as a performance shortcut was rejected because it changes rollback and player/vehicle pathing.
Scalability potential: Low/Middle/High/Ultra all resolve the same CCD position and velocity. Quality can still scale VFX/audio/telemetry outside movement truth.
Hardware Impact: 0 us speed claim; may spend a small amount of extra geometry math on low devices, but removes deterministic divergence and wall/voxel behavior drift.

## Submarine Ballast And Docking Quality Truth Detachment

Problem: Submarine auto-level and docking paths used hardware quality as authoritative math input. Low-tier ballast collapsed four tank fills into one average fill, flood solve cadence changed by tier, PID torque was scaled by tier, maelstrom tangential force could be removed, flood drag tensor and tail-heavy fluid impulse were suppressed, and docking used `GlobalRegistry.ScalabilityTier` to choose progress/evaluation behavior.
Solution: Removed the tier branch from ballast, PID, flood, and docking truth. Ballast always resolves front/aft/port/starboard fill deltas, flood mass cadence is fixed for authority, PID gains/max torque are not tier-scaled, maelstrom sampling uses the canonical full approximation tier, flood drag/visual impulse emission no longer keys off hardware math LOD, and docking progress/evaluation use one canonical inertial spline path. `DockingAutopilotMath.AuthoritativeMathLod` is a compatibility byte for the existing explicit spline DTO, not a runtime branch.
Rejected Alternatives: Continuously scaling ballast, PID torque, collision/docking progress, or maelstrom force by `GlobalQualityWeight` was rejected because those values are movement and physics truth. Keeping low-tier shortcuts was rejected because rollback and replay would diverge between mobile and desktop. Creating new spline DTOs was rejected because the existing 144-byte explicit layout is already shared with the Vault/service path.
Scalability potential: Low/Middle/High/Ultra all get identical submarine and docking truth. Quality pressure must be spent on bubbles, wake visuals, haptics, shader wake/caustic density, and optional telemetry cadence, not tank fill, torque, docking pose, or maelstrom force.
Hardware Impact: 0 us speed claim. Low devices may execute more deterministic ballast/PID/docking math than before, but this removes hardware-dependent motion divergence and prevents low-tier shortcuts from corrupting player/vehicle authority.

## Fauna Path And Tool Kinematics Quality Truth Detachment

Problem: Remaining AI and tool paths still used hardware/stress math tiers as truth inputs. Fauna steering and cognition read `GlobalRegistry.ScalabilityTier`/math precision to decide smooth apex steering. Funnel smoothing reduced portal lookahead under stressed/low LOD, changing path output. Tool kinematics used low-tier snap to skip IK, recoil, collision spring, pivot compensation, and SDF raymarch hit quality, which can alter tool pose and hit results.
Solution: Fauna apex steering is now species-role based, not device based. Path funnel records the compatibility `MathLod` byte as Ultra and uses one fixed authoritative lookahead cap. Tool runtime stops emitting low-tier snap triggers, IK always executes the full two-bone solve, SDF raymarch uses the canonical maximum step budget for hit truth, recoil/pivot compensation always executes, and only beam ring geometry uses a continuous visual stress curve.
Rejected Alternatives: Continuous scaling of steering direction, path lookahead, raymarch steps, or tool recoil by quality was rejected because those are AI/movement/tool truth. Keeping low-tier snap was rejected because it changes tool reach and hit feedback. Removing public legacy flag/enum values was rejected in this pass because they are part of the existing contracts ABI; active producers now clear them instead.
Scalability potential: Low/Middle/High/Ultra all produce identical fauna steering truth, path waypoints, tool IK, recoil, and SDF hits. Low devices may shed only visual beam tessellation through a continuous curve, while High/Ultra use denser visual beam rings without changing hit authority.
Hardware Impact: 0 us speed claim; deterministic consistency is the explicit tradeoff. Tool visual mesh work still scales continuously from 4 to 8 beam ring sides based on stress-derived quality.

## Narrative POI Quality Continuum And Hash Row Pinning

Problem: `HectonNarrativeDirector` still used `GlobalRegistry.ScalabilityTier` inside scan scheduling helpers. POI scan cadence snapped between low/default/high constants, and the dominant-axis pre-cull was gated by low-tier hardware even though the cull is mathematically equivalent to a safe early rejection before the exact distance check. The native `NarrativeNode` hash-map value also had implicit layout.
Solution: Replaced the tier switch with a continuous `HomeostasisBrain.GlobalQualityWeight` smoothstep curve mapping 1.0s survival cadence to 0.5s visual-overkill cadence. Made dominant-axis pre-cull unconditional because `max(abs(delta))^2 >= radiusSq` implies `lengthsq(delta) >= radiusSq`, so it cannot hide an in-radius POI. Pinned `NarrativeNode` to explicit 16-byte layout and marked triggered result arrays as `[WriteOnly, NoAlias]`.
Rejected Alternatives: Keeping `GlobalRegistry.ScalabilityTier` was rejected because registry tier is cold bootstrap state, not a hot cadence bus. Removing the pre-cull was rejected because it would spend more ALU for no correctness gain. Moving POI arrays to Vault was rejected in this micro-pass because no Narrative-owned BufferIDs or route card exists; existing local scene-lifetime arrays are sentinel-registered and need an approved owner route before migration.
Scalability potential: Low devices scan POIs at the survival cadence through a continuous curve, Middle/High interpolate smoothly, and Ultra gets faster narrative focus responsiveness without changing POI identity, save mask, or triggered signal ownership.
Hardware Impact: Static estimate below 1 us per POI scan on i3/MX350-class CPU. The main gain is removal of binary tier polling and explicit native row layout; profiler proof remains absent.

## Player Narcosis Hardware Branch Removal

Problem: `HectonPlayerMovement` disabled runtime narcosis look drift when `SystemInfo.graphicsMemorySize <= 2048`. That made player control feedback depend on device memory instead of physiological status severity.
Solution: Removed the low-memory boolean and the early return. The severity-driven look scale still applies, and the authored deterministic triangle-wave drift now runs on every device.
Rejected Alternatives: Replacing the branch with a continuous quality blend was rejected because narcosis input/camera impairment is gameplay feedback, not visual-only shader detail. Keeping a cheaper static path for low devices was rejected because it forks player-control feel by hardware.
Scalability potential: Low/Middle/High/Ultra all receive the same narcosis control feedback. Any quality shedding must target postprocess/visor distortion/audio layers, not the input effect.
Hardware Impact: 0 us speed claim; this spends a few scalar ops on low devices to preserve gameplay parity.

## Construction Deconstruction DFS Authority Repair

Problem: Deconstruction rollback validation skipped isolation DFS on Unknown/Low/Mx350 tiers, allowing hardware quality to change whether removing a habitat module is legal.
Solution: Removed the skip flag from the manager call and from `HabitatGraphManager.TryValidateDeconstructionRollback`. The DFS validation now executes through one authority path for every device.
Rejected Alternatives: Continuous quality scaling was rejected because topology legality is gameplay truth. Keeping the low-tier bypass as a performance shortcut was rejected because it can create different base graphs across clients.
Scalability potential: Low/Middle/High/Ultra all validate the same construction topology. Visual/editor feedback cadence can scale elsewhere; legal graph truth cannot.
Hardware Impact: 0 us speed claim; this may spend more DFS work on weak devices, but removes hardware-dependent construction divergence.

## Loot Magnet Authority Pull Determinism And Alias Pass

Problem: Loot magnet acquisition had a previous low-tier lerp shortcut that changed entity motion/acquisition timing by hardware tier, and the pull job exposed NativeArray lanes without complete alias/read-write proof.
Solution: Verified the active low-tier lerp path is absent, kept fast tick scheduling through one pull kernel, changed `LootMagnetJob` to deterministic Burst flags, marked read/write lanes `[NoAlias]`, read-only lanes `[ReadOnly, NoAlias]`, and signal output `[WriteOnly, NoAlias]`. Removed the unused low-tier lerp rate and replaced the Vault view property with a static `in` helper.
Rejected Alternatives: Keeping the lerp shortcut was rejected because acquisition truth must not depend on hardware. Blending pull strength by `GlobalQualityWeight` was also rejected because it would change pickup timing. Marking EntityAups/Flags/Velocities as write-only was rejected because the job reads their current state before mutation.
Scalability potential: Low/Middle/High/Ultra now share identical loot attraction and acquisition truth. Quality pressure should only reduce acoustic/wake presentation budgets or shader/audio detail, not entity motion.
Hardware Impact: Static estimate below 1 us per loot pull batch from clearer Burst alias metadata on i3/MX350-class CPU; profiler proof absent.

## RTG Decay Cadence Authority And Alias Pass

Problem: `RadioisotopeThermalGenerator` used hardware tier and a serialized force-low flag to move decay evaluation between 1s ColdTick and 10s FrostTick, which changed power output timing and warning/dead-state transitions.
Solution: Removed the low-tier cadence route and FrostTick registration. RTG decay now runs from one 1s authority cadence, and `RtgDecayJob` uses deterministic Burst flags with explicit read-only, write-only, and read-write noalias slice metadata.
Rejected Alternatives: Continuously scaling decay cadence by `GlobalQualityWeight` was rejected because isotope output is power truth. Keeping FrostTick as a low-device shortcut was rejected because it can desync power state.
Scalability potential: Low/Middle/High/Ultra share identical power decay. Quality can only scale radiation/heat presentation, telemetry cadence, or warning UI effects outside output truth.
Hardware Impact: 0 us speed claim; the alias metadata may help below 1 us per decay batch, unprofiled.

## Fluid Pipe Solver Authority Cadence And Layout Pass

Problem: `FluidPipeGraphRuntime` read `GlobalRegistry.ScalabilityTier` to choose pipe solve cadence, making pressure transfer, rupture timing, and room exchange timing depend on hardware tier. `FluidPipeRuptureRecord` also had a 40-byte stride that does not satisfy the current ARM64 multiple-of-16 mandate.
Solution: Runtime solve cadence now uses `FluidPipeGraphConstants.AuthoritativeCadenceSeconds` at 0.1s. The legacy `FluidPipeMathLod` helper returns the same cadence for ABI callers. `FluidPipeRuptureRecord` is explicit 48 bytes, and `FluidPipePressureSolveJob` uses deterministic Burst flags plus precise alias/read-write annotations.
Rejected Alternatives: Scaling solver cadence by quality was rejected because pipe pressure is gameplay truth. Removing the public enum was rejected to avoid unnecessary ABI churn.
Scalability potential: Low/Middle/High/Ultra share identical pipe pressure/rupture truth. Quality should scale flow particles, leak audio, or optional telemetry, not the solver.
Hardware Impact: Static estimate below 1 us per pipe solve from alias proof; main effect is determinism and layout safety.

## Metabolism Authority Quality Detachment

Problem: Metabolism quality stretched the authoritative cadence from 0.5s to 3s and changed thermal/chemical interpolation, which can alter body temperature, toxicity, threshold signals, and physiology state timing across devices.
Solution: `ResolveCadenceSeconds` now returns the nominal 0.5s cadence. `MetabolicIntegrationJob` receives quality 1.0 for authority sampling, while telemetry and shader-global emission still receive the real quality weight.
Rejected Alternatives: Multiplying rates by a longer quality-derived tick was rejected because clamping and threshold events are not purely linear. Keeping nearest-neighbor low-quality environmental sampling was rejected because it changes health truth.
Scalability potential: Low/Middle/High/Ultra share identical metabolism state. Low devices may reduce frost/toxicity shader detail and optional telemetry outside the integration kernel.
Hardware Impact: 0 us speed claim; this intentionally spends full authority math to remove physiology divergence.

## GlobalWorldSampler Canonical Authority Sampling

Problem: `GlobalWorldSampler` used `GlobalQualityWeight` to skip sample frames, blend nearest vs bilinear/trilinear height/SDF sampling, estimate cheap normals, and reduce raymarch steps. That changes terrain collision/material answers by device.
Solution: Authority sampling helpers now return canonical full-quality cadence and expensive/overkill weights. Raymarch jobs use full-quality step budgets. Stored quality is still recorded to telemetry/probe presentation instead of changing sample truth.
Rejected Alternatives: Continuous quality blending inside sampler truth was rejected because smooth divergence is still divergence. Duplicating sampler APIs was rejected in this pass because current callers do not prove visual-only vs authority ownership.
Scalability potential: Low/Middle/High/Ultra share identical terrain, SDF, material, and normal outputs. Future visual-only sampler lanes can reintroduce continuous quality after route separation.
Hardware Impact: 0 us speed claim; low devices do more canonical sampling to preserve world truth.

## Laser Cutter Carve Truth Detachment

Problem: `LaserCutResolveHitsJob` fed `GlobalQualityWeight` into SDF carve estimation, so battery/progression requests could differ by hardware while sparks/glow also scaled visually.
Solution: SDF carve progress now uses an authoritative curve of 1.0. The quality curve remains only for spark count, glow radius/lifetime, impact intensity, and work-estimate telemetry.
Rejected Alternatives: Blending carve progress by quality was rejected because tool progression and battery state are gameplay truth. Removing visual quality scaling was rejected because sparks/glow are presentation fakes.
Scalability potential: Low keeps cheap spark/glow output while carving identically. Middle/High/Ultra buy denser sparks and longer glow without changing battery/progress.
Hardware Impact: 0 us speed claim; visual work still scales continuously through existing quality curves.

## Habitat Hydrodynamic Stress Registry Poll Removal

Problem: `HabitatGraphManager.ApplyHydrodynamicStress` polled `GlobalRegistry.ScalabilityTier` and used the result to choose analytical stress precision, flood traversal pressure-root approximation, module stress upload behavior, and low-tier feedback. That is both a hot registry read and a hardware-dependent habitat truth route.
Solution: Replaced the hot poll with the canonical Ultra authority path for the current pass. Existing visual tier helpers remain ABI-compatible, but active pressure/flood/stress evaluation no longer depends on registry tier.
Rejected Alternatives: Blending analytical stress or pressure-root approximation by quality was rejected because smooth quality-dependent stress is still divergent gameplay truth. Removing all legacy tier helper names was rejected because this file has existing visual/signal ABI surfaces that need a separate visual-only route pass.
Scalability potential: Low/Middle/High/Ultra share identical habitat pressure, flood, and stress truth. Future savings must come from visual upload density, groan/acoustic cadence, and optional telemetry, not from pressure math.
Hardware Impact: 0 us speed claim; low devices may run more canonical stress math to preserve base integrity parity.

## Physiology Haldane Authority Quality Detachment

Problem: `ShinobuPhysiologyRuntime` used smoothed `GlobalQualityWeight` to stretch the authoritative physiology tick from about 16 ms to 200 ms, and `IntegrateBloodGasTensionsJob` used the same quality to evaluate only 4-16 Haldane tissue compartments. That changes decompression risk, bends flags, narcosis signals, toxic damage cadence, and rollback state by device.
Solution: The runtime now schedules physiology authority with `AuthoritativeUpdateIntervalSeconds = 0.016f` and passes `AuthoritativeQualityWeight = 1f` into the blood-gas and CNS jobs. `ShinobuPhysiologyJobMath.ResolveActiveCompartmentCount` now returns the full 16-compartment row count, so all tissue lanes are integrated and signaled on every device. Smoothed quality remains cached only as a presentation/future visual input, not as authority.
Rejected Alternatives: Continuously scaling compartment count or cadence by quality was rejected because smooth divergence is still gameplay divergence. Removing the existing quality plumbing entirely was rejected in this micro-pass because visual/shader sync lanes may still need a presentation-quality value after route separation.
Scalability potential: Low/Middle/High/Ultra share identical decompression, gas toxicity, and physiology timing truth. Visual reductions must move to postprocess hypoxia, audio, telemetry cadence, or shader scalars outside the Vault physiology state.
Hardware Impact: 0 us speed claim. Weak devices spend the canonical 16-compartment math to preserve health-state parity; the corrected cost buys deterministic survival state instead of a false low-tier shortcut.

## Ecosystem Swarm Authority Quality Detachment

Problem: `ShinobuEcosystemBalancer` used `GlobalQualityWeight` and system stress to reduce entity budget, neighbor sample budget, spatial-hash chain depth, update stride, simulation tick delta, neighbor solve weight, macro rehydration density, and flocking force richness. The swarm rows feed biomass/symbiosis and encounter threat modifiers, so this is not purely render density.
Solution: Added an `AuthoritativeQualityWeight` lane and routed entity count, spatial hash, update stride, flocking, simulation tick delta, and macro Lotka/rehydration through canonical authority. Helper functions now return full capacity, full neighbor/chain budget, stride 1, fixed 1/60 second tick, and full neighbor solve weight. The render payload and GPU culling path still receive the real visual quality weight through `_lastGlobalQualityWeight`.
Rejected Alternatives: Leaving low-quality skip-update flags was rejected because biomass and encounter state would drift by device. Scaling biomass reproduction or rehydration density continuously was rejected because smooth hardware-dependent ecosystem truth is still divergent truth. Removing visual quality from BRG/GPU culling was rejected because draw density is a valid presentation budget.
Scalability potential: Low/Middle/High/Ultra now share the same ecology simulation and biomass rows. Weak devices can still reduce visible swarm density, compute culling density, and shader payload richness without changing entity ownership, biomass, or encounter inputs.
Hardware Impact: 0 us speed claim; this deliberately spends canonical AI ecology work for deterministic biomass. Render-side density remains the place to buy back frame time.

## Hydrodynamic KCC Authority Quality Detachment

Problem: `HydrodynamicKccRuntime` used `GlobalQualityWeight` inside movement authority: mock input strafe, fallback environment field generation, added mass, acceleration, flow/SDF sampling, friction, slope slide, drag, collision iteration count, and rollback replay frame budget. That made player/body kinematics and rollback recovery differ by device.
Solution: Added `HydrodynamicKccMath.AuthoritativeQualityWeight = 1f`, made collision iteration count fixed at 8, and routed mock input, mock environment fields, environmental force application, slope friction, and rollback fast-forward through canonical authority. The real quality weight remains on wake/turbulence, telemetry cost estimates, and visual interpolation only.
Rejected Alternatives: Continuously scaling physical drag, SDF sampling, or rollback replay budget by quality was rejected because smooth hardware-dependent movement is still divergent movement. Removing presentation-quality use was rejected because wake and visual sync are valid Dear Lie lanes.
Scalability potential: Low/Middle/High/Ultra share identical KCC movement, flow interaction, SDF friction, slope slide, collision iteration, and rollback catch-up truth. Weak devices can still shed wake radius/turbulence, visual interpolation richness, and optional telemetry while preserving state authority.
Hardware Impact: 0 us speed claim; low devices spend canonical KCC authority work to preserve deterministic motion. Presentation cost still scales through wake/visual lanes.

## Buoyancy Force And Sleep Authority Quality Detachment

Problem: `BuoyancyDisplacementRuntime` used quality to thin authority evaluation stride and ambient-current wake polling, while `EvaluateBuoyancyJob` used quality for sleep thresholds, surface snap depth, dense-layer force, flow-noise amplitude, speed approximation, and drag blend. That changed buoyant force packets, resting/sleep promotion, and physics wake timing by device.
Solution: Added `BuoyancyDisplacementConstants.AuthoritativeQualityWeight = 1f`, fixed evaluation stride to every authority frame, fixed ambient-current wake cadence to the canonical full-quality path, and routed the force/sleep kernel through canonical quality. Telemetry and editor SIMD benchmark quality fields remain real-quality presentation/diagnostic lanes.
Rejected Alternatives: Continuous scaling of buoyancy force, drag, or sleep thresholds was rejected because smooth hardware-dependent force is still force divergence. Leaving low-quality stride thinning was rejected because skipped authority rows alter packet timing and sleep promotion. Removing quality from telemetry was rejected because quality pressure remains valid diagnostic context.
Scalability potential: Low/Middle/High/Ultra share identical buoyancy force packets, surface snap, density/flow evaluation, and sleep/static-promotion truth. Weak devices can still shed draw/debug/benchmark richness outside physics authority.
Hardware Impact: 0 us speed claim; low devices spend canonical buoyancy math to preserve deterministic physics. The valid scalability lane is presentation/diagnostics, not force truth.

## Tether Verlet Authority Quality Detachment

Problem: `TetherInstance` used quality tier or `HomeostasisBrain.GlobalQualityWeight` to change Verlet segment count, default constraint iterations, and fallback velocity damping. Those values affect cable endpoint force and payload motion, not only cable appearance.
Solution: Kept explicit tuning overrides for designer-authored iteration count, but made the default authority path use the full `VerletUltraIterationCount`, fixed segment count at `VerletDefaultSegmentCount`, and fixed fallback damping at `VerletHighVelocityDamping`. The existing low-tier taut-line path remains isolated inside `UpdateVerletVisualUpload`.
Rejected Alternatives: Continuously scaling Verlet solver count or nodes by quality was rejected because smooth cable force divergence is still gameplay divergence. Removing the visual straight-line fake was rejected because it is a valid GPU/presentation cost shed after the authority solve.
Scalability potential: Low/Middle/High/Ultra share identical tether force and payload constraints. Weak devices can still collapse visible cable curvature in the upload path when tension is high, while High/Ultra retain the solved cable visual.
Hardware Impact: 0 us speed claim; low devices may spend more canonical Verlet work. The cost is accepted to preserve cable authority parity.

## Vegetation Abyssal Path Authority Tier Removal

Problem: `VegetationNavGridSynchronizer` polled `GlobalRegistry.ScalabilityTier` during abyssal path smoothing and reduced portal lookahead plus DDA sample count on lower tiers. That changes smoothed path output and fauna navigation truth.
Solution: Removed the hot registry poll from the path scheduler. Smoothing now uses the canonical high-lookahead path and the configured safe DDA cap for every device.
Rejected Alternatives: Continuous quality scaling of lookahead or DDA samples was rejected because path-output divergence is still AI/navigation divergence. Keeping the registry poll was rejected because GlobalRegistry is cold identity/dependency injection only, not a hot quality bus.
Scalability potential: Low/Middle/High/Ultra share identical abyssal path results. Future quality shedding must target visual vegetation density, optional debug path rendering, or non-authority telemetry, not route geometry.
Hardware Impact: 0 us speed claim; low devices may run the full smoothing route to preserve navigation parity.

## Stress Spawn Director Authority Quality Detachment

Problem: `StressDrivenSpawnDirector` used `GlobalQualityWeight` for candidate score, threat budget, spawn probability, hidden spawn radius, distant cull radius, and spawned cognition movement/sensory parameters. Those values decide encounter presence, spawn placement, despawn, and initial AI behavior.
Solution: Added `AuthoritativeQualityWeight = 1f` and routed those authority decisions through it. Real quality remains captured into input/selection/telemetry/debug fields so QA can correlate hardware pressure without changing the spawn facts.
Rejected Alternatives: Continuous scaling of spawn chance, despawn radius, or cognition radius by quality was rejected because smooth encounter divergence is still multiplayer divergence. Removing quality telemetry was rejected because it is useful black-box context.
Scalability potential: Low/Middle/High/Ultra share identical encounter selection, spawn placement envelope, distant cull decision, and spawned cognition input. Device savings should target visual population density overlays, spawn debug rendering, and non-authority VFX/audio.
Hardware Impact: 0 us speed claim; canonical encounter truth is preserved at the cost of spending the same decision math on weak devices.

## Predator Cognition And Alpha Leviathan Authority Quality Detachment

Problem: `PredatorCognitionDomain` used `HomeostasisBrain.GlobalQualityWeight`, `GlobalRegistry.ScalabilityTierProfileByte`, frame pressure, and `HighTierSmoothSteering` input flags to alter mesofauna slice cadence, perception radius, tuning quality, retinal predator evaluation cadence, telemetry fallback flags, and predator steering math. `LeviathanStalkJob` also converted `SystemStress01` and `MathLodSurvival` into reduced SDF quality, survival steering blend, slower recommended cadence, and radial fallback flags. Those routes influence AI targets and apex predator behavior, not only visuals.
Solution: Added `AuthoritativeCognitionQualityWeight = 1f`, routed mesofauna quality through it, made retinal low-cadence mode permanently false, removed the now-unused scalability-tier registry poll, and forced predator smooth steering authority on. The Alpha Leviathan stalk kernel now uses canonical precision math LOD with stress/flag input ignored for steering cadence and fallback flags.
Rejected Alternatives: Continuous hardware scaling for AI cadence, steering quality, or mesofauna vision was rejected because smooth encounter divergence is still multiplayer divergence. Keeping `MathLodSurvival` as a hot runtime flag was rejected because the current flag is fed by system pressure, not a proven gameplay truth route.
Scalability potential: Low/Middle/High/Ultra share identical predator cognition cadence, mesofauna perception, steering, and Alpha Leviathan stalk intent. Device savings must move to visual silhouette/noise density, shader payloads, optional debug rendering, or non-authority telemetry cadence.
Hardware Impact: 0 us speed claim; weak devices now pay canonical AI authority cost. The removed hot tier poll avoids a registry read in `EnsureInitialized`, but the primary gain is deterministic apex behavior.

## Volcanic Updraft Authority Quality Detachment

Problem: `VolcanicUpdraftVault.TryEvaluateVent` used `settings.GlobalQualityWeight` to blend strict vertical lift toward authored vent vectors, so submarine, leviathan, and mock entity force direction changed by hardware. The mock debris authority lane also disabled debris lift below quality 0.3.
Solution: Added `VolcanicUpdraftVault.AuthoritativeQualityWeight = 1f`, routed turbulence/up-vector authority and mock debris lift through it, and left real quality only on dynamic wake, mock flow, visual overkill, particle budget, and presentation signal density.
Rejected Alternatives: Smoothly degrading force direction or debris lift by quality was rejected because smooth force divergence is still physics divergence. Removing visual wake quality was rejected because curl/noise flow is a valid Dear Lie lane.
Scalability potential: Low/Middle/High/Ultra share identical updraft force vectors and debris-lift authority. Weak devices can reduce visual curl density, particle budget, and emitted presentation debris without changing physics.
Hardware Impact: 0 us speed claim; low devices spend canonical force evaluation while presentation lanes remain scalable.

## Deployable SDF Drill Mining LOD Pinning

Problem: `DeployableSdfDrillRuntime` converted scalability tier into MathLod, then used it to reduce runtime extraction cycles from 8 to 1, offline macro catch-up from 512 to 64, and persisted `LowTierSdfSkipped` state. That changes ore production, save/macro hydration, and rollback-visible flags by device.
Solution: Added `AuthoritativeMathLod = Ultra`, initialized cached/target LOD to it, made scalability change callbacks reset to it, removed the cold `GlobalRegistry.ScalabilityTier` read, and made runtime/offline max-cycle helpers return canonical full caps.
Rejected Alternatives: Keeping LOD for extraction cycles was rejected because ore production is gameplay truth. Keeping low-tier SDF visual skip was rejected in this pass because it wrote a persisted flag into macro state rather than staying in a pure presentation buffer.
Scalability potential: Low/Middle/High/Ultra share identical extraction and macro catch-up. Future savings should target visual carve packet density or drill screen refresh through non-persistent presentation state.
Hardware Impact: 0 us speed claim; weak devices spend the same extraction catch-up math to preserve inventory/save parity.

## Save Macro Database Compaction Tier Pinning

Problem: `SaveManager.ResolveMacroDatabaseCompactionTier` polled `GlobalRegistry.ScalabilityTier` to pick Low/Middle/High/Ultra macro database compaction tier. That changes persistence compaction thresholds and telemetry by hardware, violating save route invariance.
Solution: The resolver now returns canonical `MacroDatabaseTier.Middle`, matching the macro database default config without reading hardware tier.
Rejected Alternatives: Continuous quality scaling was not applicable because compaction tier is an enum that affects persistence behavior, not presentation. Ultra was rejected because the existing database default is Middle; matching the default is the least invasive authority route.
Scalability potential: All hardware uses the same compaction threshold route. Device-specific IO masking must happen through blind-frame scheduling and persistence gates, not save identity tier.
Hardware Impact: 0 us speed claim; one registry poll is removed from FrostTick compaction selection.

## Physics Authority Quality Detachment II

Problem: Several active physics kernels still converted `HomeostasisBrain.GlobalQualityWeight`, stress, or tuning quality into authority math. KCC SDF squeeze widened sample steps and flagged stress slow cadence; seaglide quality changed thrust cadence, metabolism cadence, and force model weights; exosuit quality changed deterministic input/tuning, actuator damping, SDF skin, and collision iterations; habitat fluid quality changed solver cadence, ingress cap, and flooded-mass angular drag.
Solution: Pinned those gameplay paths to explicit authority quality 1.0. KCC SDF squeeze now samples at full step quality and reports slow cadence only when the caller explicitly marks cadence. Seaglide force and cadence use `SeaglideSimdMath.AuthoritativeQualityWeight`. Exosuit tuning/input, integration, and SDF collision use `ExosuitMathGuards.AuthoritativeQualityWeight`. Habitat fluid tuning uses `HabitatFluidIncursionMath.AuthoritativeQualityWeight`, full solver iterations, full ingress cap, and canonical angular-drag multiplier.
Rejected Alternatives: Smooth quality blends were rejected because smooth hardware-dependent movement/fluid divergence is still divergence. Removing presentation quality from telemetry/signal emission was rejected where those fields document pressure or bound VFX/audio fanout without owning gameplay truth.
Scalability potential: Low/Middle/High/Ultra share identical SDF squeeze normals, seaglide forces, exosuit collision response, and flooded-mass behavior. Weak-device savings must move to wake/bubble/audio/shader/debug density and optional telemetry cadence, not force or state ownership.
Hardware Impact: 0 us speed claim. Weak devices intentionally spend canonical authority math for rollback parity; targeted `git diff --check` passed, compile deferred because CPU load was 100%.

## Power And Construction Authority Quality Detachment

Problem: Power and construction systems still used global quality or hardware tier to change authority convergence and task cadence. Power Jacobi propagation iterations, tolerance, omega, residual sampling, submarine thermal-grid cadence, cable thermal iterations, and logistics adaptive solve slices changed power/heat truth. Drone steering tick rate, A* solve budget, task rebuild interval, docking obstacle raycast segmentation, repair signal tier, and bulkhead authority cadence changed construction/repair/containment truth.
Solution: Added canonical `PowerSolverConvergenceMath.AuthoritativeQualityWeight` and pinned convergence helpers to full authority values. PowerGrid, PowerGridManager, LogisticsNetworkGraph, and SubmarineOsThermalGridRuntime now feed canonical quality into solve/cadence paths. Drone authority helpers use a new `ResolveAuthoritativeQualityWeight()` and repair signals emit canonical Ultra tier instead of polling `GlobalRegistry`. Bulkhead tuning and simulation cadence use `AuthoritativeQualityWeight`.
Rejected Alternatives: Continuous quality degradation inside power convergence, construction pathfinding, or bulkhead closure cadence was rejected because smooth hardware-dependent brownout/repair/containment divergence is still gameplay divergence. Brownout shader scalar, drone phantom draw/render distance, bulkhead shader q, and scavenging VFX multiplier were left as presentation lanes because they do not own the fact being solved.
Scalability potential: Low/Middle/High/Ultra share identical power distribution, thermal propagation, drone task cadence, docking collision probes, and bulkhead closure state. Device savings must come from shader brownout visuals, drone phantom rendering, signal/debug density, and VFX fanout.
Hardware Impact: 0 us speed claim; weak devices now spend canonical authority solve work to preserve deterministic base systems. Targeted `git diff --check` passed, compile deferred because CPU load was 95.36%.

## Bulkhead And Battery Charger Final Cadence Sink Closure

Problem: Follow-up inspection found two remaining authority sinks after the broad power/construction pass. `UpdateBulkheadClosureJob` still multiplied door closure progression by a value derived from `GlobalQualityWeight`, and `BatteryChargerLogisticsRuntime` still locked the tuning buffer to sample a quality override before cadence resolution.
Solution: Bulkhead closure cadence is now a literal canonical multiplier in the Burst job. Battery charger scheduling now assigns `AuthoritativeQualityWeight` directly, resolves cadence to 60Hz, deletes the tuning-buffer quality sampling helper, and removes stale quality override resolution helpers from the authority path.
Rejected Alternatives: Keeping editor quality overrides as cadence inputs was rejected because it changes charge transfer truth. Keeping the buffer lock for canonical quality was rejected because it adds a hot owner lock without reading any authoritative fact.
Scalability potential: Low/Middle/High/Ultra share identical battery charging cadence and bulkhead closure progression. Device savings must come from charger/bulkhead visuals, shader globals, UI refresh, debug density, or telemetry cadence.
Hardware Impact: 0 us speed claim. One hot Vault tuning lock/read is removed from charger scheduling; compile remains deferred because CPU load was 54%, above the project build guard.

## Ecosystem Population, Symbiosis, And Migration Authority Detachment

Problem: Scout output found performance-pressure routes still mutating ecology truth. `EcosystemPopulationBalancer` used `SystemStress01` to cull active ecology entities. `ShinobuFloraFaunaSymbiosisSolver` let global/local quality alter exchange cadence, flora/ambient sample stride, oxygen emitter strength, biomass transfer, toxemia/camouflage coverage, and CSV quality override. `MigrationDirector` used quality for migration field rebuild cadence, sampling interpolation, blood-cloud POI stride/attraction, and field magnitude.
Solution: Removed stress cull authority and kept stress only as telemetry. Added canonical symbiosis quality, made micro exchange run every authority pass, overwrote tuning quality to 1.0, removed CSV quality override, and removed quality from the exchange kernel. Migration now feeds canonical quality to the field job, uses full interpolation, and computes rebuild cadence from the canonical high-fidelity path.
Rejected Alternatives: Smooth quality-based ecology thinning was rejected because continuous hardware divergence still changes population, biomass, oxygen, and route facts. Keeping macro-average fallback as a low-tier authority path was rejected because it mutates different flora/fish rows than the micro exchange path. Keeping real-quality migration interpolation was rejected because route direction would differ by device.
Scalability potential: Low/Middle/High/Ultra share ecology population, symbiosis exchange, oxygen/emitter, and migration route truth. Device savings must move to scanner VFX density, shader/path debug rendering, optional telemetry cadence, BRG/swarm presentation density, and non-authority audio.
Hardware Impact: 0 us speed claim. Weak devices spend canonical ecology math to preserve rollback/co-op parity; targeted `git diff --check` passed. Compile probe timed out after 124s without diagnostics, so it is not accepted as a pass.

## Atmosphere Authority Quality Detachment

Problem: Atmosphere systems still used hardware/global quality to mutate survival truth. Gas dynamics changed authority cadence and base hibernation distance. Base logistics changed diffusion Jacobi iteration count and capped low-tier reactor damage signals. The legacy base engine changed cold tick interval and solved only an active subset of compartments, copying unsolved rooms forward. Toxic chemistry changed grid resolution, tick interval, source budget, density sampling blend, diffusion/advection, flora absorption, toxemia exposure, corrosion damage, and biolum signal stride.
Solution: Gas dynamics now uses canonical quality 1.0, full cadence, and a conservative fixed hibernation distance floor. Base logistics now uses eight diffusion iterations and equal reactor signal frame capacity on all tiers while keeping smoothed quality only for shader scalar publication. The legacy base atmosphere engine now runs the high 5 Hz cadence and solves every compartment. Toxic chemistry now resolves quality to canonical 1.0, high 32^3 grid resolution, high tick cadence, full source budget, full sampling/diffusion/flora/exposure/corrosion paths, and canonical telemetry quality.
Rejected Alternatives: Continuous quality scaling was rejected for every route that changes oxygen, CO2, toxin density, exposure, corrosion, or signal fanout because smooth hardware divergence is still survival-state divergence. Keeping low-resolution toxic grids as an authority Math LOD was rejected because it changes sampled exposure and damage. Keeping low-tier reactor frame limits was rejected because signal drops alter toxic-source facts.
Scalability potential: Low/Middle/High/Ultra share identical gas diffusion, hibernation wake-up, oxygen/CO2, toxic plume, source, exposure, corrosion, and signal authority. Valid savings must move to shader caustics, fog density, biolum rendering, UI refresh, debug grid readback, non-authority telemetry cadence, and postprocess presentation.
Hardware Impact: 0 us speed claim. Weak devices now pay canonical atmosphere/toxin authority work for deterministic survival. CPU was 52% with no compiler processes, so build was skipped under AGENTS.md; targeted scans and diff checks passed.

## Global Physics Culling Authority Distance Pinning

Problem: Physics culling used `HomeostasisBrain.GlobalQualityWeight` to shrink/expand rigidbody sleep distance, wake distance, and activation radius scale. Those values decide when bodies are slept, woken, made kinematic, or restored, so hardware quality changed physics participation and collision availability.
Solution: Removed the global quality helper from the physics culling partial route. Distance sleep now uses `DefaultSleepDistanceMeters`, wake uses `DefaultWakeDistanceMeters` constrained by hysteresis, and activation radius scale is pinned to the conservative full-authority 2.25 value.
Rejected Alternatives: Smooth quality scaling of culling radius was rejected because continuous hardware-dependent sleep/wake divergence is still physics divergence. Keeping low-tier shorter ranges was rejected because it can remove collision/rigidbody participation earlier on weak devices.
Scalability potential: Low/Middle/High/Ultra share identical rigidbody sleep/wake and kinematic culling thresholds. Device savings should move to collider LOD visuals, mesh-collider strip policy with explicit authoring, broadphase debug rendering, or presentation-only impact wake density.
Hardware Impact: 0 us speed claim. Weak devices retain the same conservative activation radius and may keep more physics bodies active; targeted diff check passed, build skipped because CPU was 97%.

## Thermodynamics Authority Solver Pinning

Problem: Abyssal thermodynamics used global quality to change authority cadence, grid resolution, Jacobi iteration count, solver omega, residual tolerance/sampling, and temperature interpolation. Those values feed heat hazards, thermal damage, and shader proof buffers; hardware quality was changing the thermal truth surface.
Solution: Added canonical thermodynamics authority constants and pinned the solver to 1/60s, 32^3 cells, 6 Jacobi iterations, omega 1.0, tolerance 0.001, full residual sampling, and trilinear temperature sampling. Real quality remains only in visual buffer metadata and shader-side presentation.
Rejected Alternatives: Continuous degradation of resolution, solver count, or nearest-neighbor sampling was rejected because smooth thermal divergence is still hazard/damage divergence. Removing visual quality publication was rejected because shader caustics and heat shimmer are valid Dear Lie presentation lanes.
Scalability potential: Low/Middle/High/Ultra share identical thermal diffusion, temperature sampling, heat hazard, and damage truth. Weak devices can only shed visual shimmer, debug readback, optional telemetry cadence, or shader overkill around the same authoritative scalar field.
Hardware Impact: 0 us speed claim. Weak devices spend canonical thermodynamics work to preserve survival parity.

## Core Content Namespace Compile Repair

Problem: `ContentRuntimeServices.cs` referenced Optimization services (`VRAMMonitor`, `VRAMPressureMonitor`, `AssetLifecycleGovernor`) without importing the namespace that owns them. The compile wall was a using-boundary error, not a missing runtime dependency.
Solution: Added `using Hecton8.Optimization;` to the content runtime file. This does not add an assembly definition reference; the project already compiles the Optimization sources in the same C# assembly route exposed by the existing project file.
Rejected Alternatives: Moving types, adding wrappers, or changing asmdef references was rejected because the failure was local namespace visibility and broad dependency churn would enlarge the compile wall.
Scalability potential: No runtime scalability impact. The change preserves existing content/optimization ownership while restoring compile visibility.
Hardware Impact: 0 us runtime.

## RenderGraph Texture And Survival Context Compile Repair

Problem: Unity 6000 `RasterCommandBuffer.SetGlobalTexture` accepts RenderGraph `TextureHandle`s for command-buffer texture globals, not arbitrary `UnityEngine.Texture` assets. Visor post/decal passes were binding asset textures through the wrong API. `HectonSurvivalSystem` also called `TryGetPlayerPoseSnapshot` on the concrete `PlayerRuntimeContext`, but the snapshot route is owned by `IPlayerRuntimeContext`.
Solution: Kept RenderGraph handles on `RasterCommandBuffer` and moved regular asset textures to persistent material `SetTexture` calls inside the render function. Added a cached `IPlayerRuntimeContext` lane in survival, populated during player-root binding and hot-swap replacement, then used that cached interface for pose fallback reads.
Rejected Alternatives: Importing every asset texture into RenderGraph was rejected because these are stable material textures, not transient graph resources, and would add unnecessary graph ownership. Polling `GlobalRegistry.Player` in the fallback read was rejected after patch review because GlobalRegistry is cold DI, not a hot read path.
Scalability potential: Low/Middle/High/Ultra render the same bound visor/decal texture assets; quality remains expressed through existing scalar uniforms and texture flags. Survival AUP fallback now reads one immutable cached interface route without changing authority state.
Hardware Impact: below 1 us. The main gain is compile correctness and avoiding hot registry polling; no frame-time speed claim.

## Ladder Climb IK Hardware Tier Detachment

Problem: `ProceduralLadderClimbRuntime` sampled `GlobalRegistry.ScalabilityTierProfileByte` during climb begin and used tier 0 to force the camera-slide/fake-elbow path even in VR grip mode. That lets hardware tier change hand target fidelity, grip presentation flags, and player climb feedback.
Solution: Removed the scalability tier cache. The camera-slide fake is now selected only when VR grip mode is not required, which is an input-mode route rather than a hardware route. The IK job flag and local names now describe the camera-slide fake instead of low-tier hardware.
Rejected Alternatives: Keeping a binary tier branch for VR climb IK was rejected because VR grip parity and hand feedback should not collapse on weaker hardware. Driving the climb fake directly from `GlobalQualityWeight` was rejected because this is not an optional visual density parameter; it switches animation/control presentation mode.
Scalability potential: Low/Middle/High/Ultra share the same VR grip IK behavior. Non-VR keeps the cheap camera-slide Dear Lie because there is no tracked grip authority to solve, while VR hardware uses the full elbow solution on every tier.
Hardware Impact: 0 us speed claim. One cold registry tier read is removed from climb begin; weak VR devices now spend the same IK solve path to preserve player-control feedback.

## Biome Boundary SDF Authority Radius Pinning

Problem: `BiomeBoundarySdfRuntime` reduced sample radius from 5x5 to 3x3 when forced low tier, low-memory profile, tier profile byte 0, Unknown, Low, or Mx350. That changes biome gradient blend, biome hashes, and transition signals by hardware. The slow-tick AUP read also polled `GlobalRegistry.Player` directly.
Solution: Removed the low-tier kernel resolver and serialized force flag from runtime. `SampleRadiusCells` is now pinned to 2 for the full 5x5 kernel with no runtime low-tier flag. The player runtime context is cached during lifecycle cold paths and the slow tick reads the cached interface.
Rejected Alternatives: Continuous sample radius scaling was rejected because radius is integer topology over the heatmap and changes biome boundary facts. Keeping a forced low-tier diagnostic override was rejected because it still mutates runtime authority output.
Scalability potential: Low/Middle/High/Ultra share identical biome gradient sampling and signal output. Device savings must come from heatmap debug display, biome VFX, map UI refresh, or presentation-only transition effects.
Hardware Impact: 0 us speed claim. Weak devices spend the full 5x5 boundary sample to preserve environmental parity; one slow-tick registry poll is removed.

## Submarine Flood State Math LOD Pinning

Problem: `SubmarineFluidDynamics` converted `HomeostasisBrain.GlobalQualityWeight` into a 0..3 `SubmarineFloodStateSignal.MathLod`. That signal is consumed downstream as flood-state fidelity, so hardware pressure could change how flood mass/center signals are interpreted.
Solution: Added `AuthoritativeFloodStateMathLod = 3`, initialized the cached value to it, and made scalability-event refreshes reapply the canonical value instead of recomputing from quality.
Rejected Alternatives: Keeping the continuous curve was rejected because the LOD byte leaves the owner as a gameplay-adjacent signal, not just a shader scalar. Deleting the scalability listener outright was rejected in this pass to avoid broad lifecycle churn; it now writes a canonical value.
Scalability potential: Low/Middle/High/Ultra share the same flood-state signal fidelity. Device savings must move to VFX, audio, dashboard refresh, or non-authority telemetry around the same flood facts.
Hardware Impact: 0 us speed claim. One quality read and lerp/smoothstep path is removed from scalability refresh; the main result is consumer parity.

## Fauna Leviathan IK Authority Quality Pinning

Problem: `FaunaKinematicsRuntime` still let hardware and pressure state reach leviathan terrain/bite IK authority. It cached scalability tier, registered as a scalability listener, fed real `GlobalQualityWeight` into segment count/constraint iteration/SDF availability, and converted pressure into bite fallback flags and stress input.
Solution: Added a canonical authority quality lane and routed terrain IK, SDF payload selection, constraint iteration hysteresis, emergency mock bend, and bite IK stress through authority constants. Removed the scalability listener/tier cache and deleted the dead system-stress resolver. Real quality remains only in `_globalQualityWeight` for debris quantity, hull dent presentation, material metadata, and telemetry lanes.
Rejected Alternatives: Smoothly lowering terrain IK segment count, disabling SDF hug, or enabling bite fallback flags from quality/stress was rejected because those paths change collider alignment, jaw contact, and strike feedback truth. Keeping the listener but writing a canonical value was rejected because there was no remaining cold dependency to refresh.
Scalability potential: Low/Middle/High/Ultra share terrain/bite IK truth. Weak devices must recover budget through shader caustics, debris density, hull dent presentation, optional debug gizmos, or non-authority telemetry cadence; high-tier devices can spend the saved presentation budget on richer bite sparks and material response around the same jaw facts.
Hardware Impact: 0 us speed claim. Weak devices now run the same IK authority topology as high-end devices; one cold scalability listener and tier cache are removed.

## Ocean Fluid Authority Wave And Flow Pinning

Problem: `HectonFluidEngine` used cached scalability tier and low-memory state to reduce CPU fluid truth. Water height and buoyancy wave data used smaller Gerstner budgets, flow sampling disabled high-tier vector-noise math, buoyancy skipped exact normals/tidal shear, maelstrom packing collapsed to one strongest vortex, low-tier whirlpool sampling removed tangent velocity and clamped max speed, and splashdown impulse fields were skipped on low tier.
Solution: Added canonical fluid authority constants. CPU flow, water height, Gerstner data publication, WaveQueryJob, BuoyancyJob, maelstrom native packing, maelstrom telemetry clamp, and splashdown impulse upload now take the high/Ultra authority path. Removed the maelstrom low-tier cache, low-memory hot poll, binary fluid-advection low-tier resolver, and cached high-tier byte resolver. Dynamic wake/advection presentation now resolves capacity/low-signal strength from continuous `HomeostasisBrain.GlobalQualityWeight`.
Rejected Alternatives: Keeping a strongest-whirlpool shortcut or low-tier tangent removal was rejected because it changes force direction and velocity magnitude. Keeping reduced wave counts was rejected because water height and buoyancy forces become hardware-dependent. Keeping binary advection low-tier presentation was rejected because the project requires continuous quality shedding for VFX lanes.
Scalability potential: Low/Middle/High/Ultra share ocean surface, buoyancy force, current, maelstrom, and splashdown authority. Weak devices may reduce VFX wake/advection capacity continuously via `GlobalQualityWeight`; high-tier devices get full dynamic wake presentation without changing force truth.
Hardware Impact: 0 us speed claim. Weak devices spend canonical fluid authority work; one hot low-memory registry poll and several binary low-tier branches were removed from the frame path.

## Procedural Bite IK Kernel Fake Eviction

Problem: `ProceduralBiteJob` still contained a low-tier/stress-fallback branch after the runtime route was pinned. If any stale producer set those public flags, the kernel would skip mandibles, force a 1-frame blend, stretch the head bone, and mark the solve as a low-tier fake.
Solution: Removed the low-tier and stress-fallback constants and branch. The job now always solves mandibles and uses the three-frame blend. System stress is retained only in DTO/telemetry fields for black-box forensics. High-tier/Ultra flags still enable wrap/visual-overkill appendages explicitly.
Rejected Alternatives: Leaving dormant flags was rejected because public constants become future hidden authority switches. Mapping stress into a smoother blend was rejected because bite contact and feedback must not vary with frame pressure.
Scalability potential: Low/Middle/High/Ultra share jaw contact, head scale, mandible positions, and bite feedback. Device savings must remain presentation-only: sparks, dents, shader response, tentacle VFX density, and telemetry cadence.
Hardware Impact: 0 us speed claim. Weak devices spend the full jaw solve; removed a branch and stale fake result path from the Burst kernel.

## VR Hand Presence Hardware Fallback Removal

Problem: `VRPhysicalHandPresenceJob` accepted a `RuntimeFlagLowTier` bit that forced the non-VR screen-space fallback even when VR was active. It also allowed SDF hand projection through a `RuntimeFlagHighTier` bit, which tied contact richness to hardware tier instead of explicit surface data.
Solution: Removed the low/high tier constants from the hand-presence contract. The fallback path now triggers only when VR is inactive, and SDF projection is enabled only by the explicit `RuntimeFlagSdfProjection` capability bit. Surface-plane projection remains an explicit capability path.
Rejected Alternatives: Keeping low-tier screen-space fallback for weak VR devices was rejected because it changes hand position, haptic scrape, lock state, and interaction feedback. Keeping high-tier as an SDF shortcut was rejected because capability and hardware tier are different facts.
Scalability potential: Low/Middle/High/Ultra share VR hand contact truth. Device savings must come from visual ghost opacity, controller mesh detail, haptic amplitude smoothing, optional telemetry cadence, or shader VFX, not from disabling physical hand presence.
Hardware Impact: 0 us speed claim. Weak devices retain the same physical hand solve; two stale hardware flag checks were removed from the job.

## Leviathan Terrain IK Kernel Authority Hardening

Problem: `LeviathanTerrainIkJob` still had an internal quality collapse path that capped segment count, capped constraint iterations, disabled SDF hugging, and marked low-tier telemetry based on `GlobalQualityWeight`. Runtime had been pinned, but the kernel itself remained unsafe for future callers.
Solution: The terrain IK kernel now pins authority quality to 1.0, uses `RequestedSegmentCount` and `ConstraintIterations` directly within hard bounds, keeps SDF eligibility tied only to explicit payload/capability validity, and removes low-tier runtime/telemetry constants.
Rejected Alternatives: Trusting the runtime to always pass quality 1.0 was rejected because the kernel is a shared Burst contract and must be self-defending. Keeping low-tier telemetry was rejected because it no longer corresponds to a real authority state.
Scalability potential: Low/Middle/High/Ultra share terrain pose, collision proxy, SDF hug, and tail-follow topology. Device savings must be purchased through visual-only bone upload density, material VFX, debug draw, or optional telemetry cadence.
Hardware Impact: 0 us speed claim. Weak devices retain full authority IK topology; stale segment/iteration/SDF quality clamps were removed.

## Fauna Retinal Biolum Continuous Presentation

Problem: `FaunaBrain` cached `GlobalRegistry.ScalabilityTierProfileByte` and used a hard `< 2` gate to suppress retinal-blind biolum feedback. This was visual-only, but it still produced a binary quality pop and a stale tier cache.
Solution: Removed the cached profile byte. Retinal-blind signals are consumed on all tiers, and strobe intensity now scales by a smooth polynomial of `HomeostasisBrain.GlobalQualityWeight` with a low-end visible floor.
Rejected Alternatives: Keeping the binary tier gate was rejected because visual lanes must degrade continuously. Dropping the effect entirely at low quality was rejected because the player feedback becomes inconsistent; scaling intensity preserves the cue with lower presentation cost.
Scalability potential: Low devices get a reduced but visible strobe; middle devices get partial intensity; high/ultra get full strobe. The gameplay blind state remains unchanged.
Hardware Impact: 0 us speed claim. One cold registry tier field was removed; the route is presentation only.

## Visor Material Texture Binding Repair II

Problem: Compile Check 87 proved the previous Visor fix did not reach the actual RenderGraph render function lines. `RasterCommandBuffer.SetGlobalTexture` still received ordinary `UnityEngine.Texture` / `Texture2DArray` assets, but this Unity 6000 overload expects RenderGraph `TextureHandle` values.
Solution: Bound ordinary visor crack, lens dirt, blue-noise, VR comfort mask, and decal atlas textures through the persistent fullscreen `Material.SetTexture(int, Texture)` API. Kept transient source/depth `TextureHandle` bindings on `RasterCommandBuffer`.
Rejected Alternatives: Importing stable asset textures into RenderGraph was rejected because ownership is persistent material state, not transient graph storage. String property names were rejected because existing shader IDs are already precomputed integer IDs and avoid per-call lookup/allocation risk.
Scalability potential: Low/Middle/High/Ultra use the same shader assets; fidelity continues to scale through existing scalar uniforms and continuous presentation weights, not through compile-invalid binding paths.
Hardware Impact: 0 us runtime. This is a compile-wall repair; no gameplay or frame-time speed claim.

## Procedural Crab Leg IK Raycast Authority Pinning

Problem: `ProceduralCrabLegIKRuntime` cached `GlobalRegistry.ScalabilityTier` and used Low/Mx350 to raycast only two rotating legs per entity. That changes foot targets, grounded pose, body tilt, and indirect joint matrices by hardware tier.
Solution: Removed the cached tier and pinned `RaycastBudgetMode` to `RaycastBudgetHighAllLegs`. Deleted the Burst helper that accepted the two-leg budget so future callers cannot silently restore hardware-dependent grounding.
Rejected Alternatives: Continuous raycast-count scaling was rejected because leg contact topology is not a visual density parameter; it changes pose truth. Keeping the unused low-budget helper was rejected because stale public constants become future regression switches.
Scalability potential: Low/Middle/High/Ultra share crab grounding and body pose. Device savings must move to crab population density, draw distance, shader detail, animation upload cadence, or telemetry, not to physical foot contact truth.
Hardware Impact: 0 us speed claim. Weak devices spend full grounding authority; one cold registry tier read and one branchy rotating-leg selector were removed.

## Leviathan Tentacle Verlet Authority Quality Pinning

Problem: `LeviathanTentacleVerletSolver` registered as a scalability listener and routed `HomeostasisBrain.GlobalQualityWeight` into the Burst Verlet job. Low quality reduced segment budget, capped Jacobi iterations, and damped flow-noise/suction pulse response, changing tentacle pose, stretch, grab contact, and matrices by hardware pressure.
Solution: Removed scalability listener registration, hardware tier cache, and job quality input. The solver now uses full `SegmentsPerTentacle`, authored high-tier constraint iterations, and full flow/pulse response in authority. Real quality remains only on `_H8LeviathanTentacleFxTier` material binding for shader presentation.
Rejected Alternatives: Smoothly lowering tentacle segment count or solver iterations was rejected because active tentacle topology and grab stretch are gameplay-adjacent facts. Keeping job quality as a caller-controlled input was rejected because shared Burst kernels must be self-defending.
Scalability potential: Low/Middle/High/Ultra share tentacle pose, grab stretch, root/target AUP telemetry, and indirect matrix truth. Weak devices can shed cost through tentacle material FX tier, draw density, culling distance, or optional telemetry cadence; high/ultra can spend presentation budget on richer shader motion.
Hardware Impact: 0 us speed claim. Weak devices retain full solver authority; one scalability listener route and several quality-dependent branches were removed from the solver setup/job.

## Submarine Leak Plume Continuous Presentation Budget

Problem: `SubmarineStructuralGrid.ResolveVisibleBreachCount` used `GlobalRegistry.H8_LOW_MEMORY_PROFILE` and `GlobalRegistry.MathPrecision` to hard-clamp leak-plume rendering to eight breaches. The underlying breach truth stayed intact, but presentation popped by binary hardware flags and hot-polled registry state.
Solution: Replaced the binary branch with `ResolveLeakPresentationQuality01()` and a smooth polynomial budget curve from `MinVisibleBreachLimit` to `MaxActiveBreaches`. Active breach count and damage/flood authority are unchanged; only visible shader plume density scales.
Rejected Alternatives: Keeping low-memory/math precision switches was rejected because visual quality lanes must be continuous. Reducing `_activeBreachCount` itself was rejected because breach truth belongs to the structural/flooding authority route.
Scalability potential: Low devices render fewer leak plumes but keep every breach in authority. Middle devices interpolate plume density. High/Ultra render all active breach plumes and can spend extra shader detail on the same facts.
Hardware Impact: 0 us speed claim. Removed two hot registry flag reads from the presentation count resolver; shader draw density now degrades smoothly.

## Voxel Biome SDF Modifier Authority Tier Removal

Problem: `HectonVoxelEngine.ResolveBiomeSdfModifierEnabled` disabled biome SDF modifiers for Low/Mx350/Unknown hardware tiers. That mutates voxel density and generated chunk content by device, which can affect collision, navigation, resource visibility, and save identity.
Solution: Removed the hardware-tier branch. The modifier remains disabled only for existing deterministic LOD >= 2, so the same seed/LOD produces the same density path across devices.
Rejected Alternatives: Converting this to `GlobalQualityWeight` was rejected because voxel density/content is not a presentation budget. Moving biome modifiers into a visual-only shader overlay was out of scope for this static sanitation pass and would require a separate route card.
Scalability potential: Low/Middle/High/Ultra share generated terrain content for the same LOD. Device savings must come from chunk scheduling cadence, mesh density LOD, shader material detail, vegetation draw density, or streaming budget, not from changing biome SDF truth.
Hardware Impact: 0 us speed claim. Weak devices spend the same biome modifier work for near LOD chunks; removed one chunk-generation hardware tier read.

## VR Somatic Ghost Hand Quality Continuum

Problem: `VRSomaticProvider` cached `GlobalRegistry.ScalabilityTier` and `H8_LOW_MEMORY_PROFILE`, listened to scalability events, disabled ghost hands on low tier, and emitted a low-tier black-box flag. This made VR hand presentation pop by binary hardware state and carried stale hardware identity through telemetry.
Solution: Removed scalability listener implementation/registration, cached tier and low-memory fields, low-tier telemetry flag, and `IsLowTier`. Ghost-hand threshold now scales continuously from `GlobalQualityWeight` with a 2.5x low-quality threshold and 1.0x high-quality threshold. The serialized setting is preserved with `FormerlySerializedAs`.
Rejected Alternatives: Keeping a hard low-tier disable was rejected because hand feedback should degrade smoothly. Changing hand physical spring or target authority was rejected because those affect control feel; only the ghost visibility threshold is presentation.
Scalability potential: Low devices show fewer ghost hand offsets unless the hand separation is large, middle devices interpolate, and high/ultra retain the authored threshold. Physical hand target and spring simulation remain unchanged.
Hardware Impact: 0 us speed claim. Removed two cold registry hardware reads and one scalability event route; runtime behavior now uses the already cached continuous quality weight.

## Hull Integrity Quality Profile Continuum

Problem: `HullIntegrityRuntime` cached `GlobalRegistry.ScalabilityTierProfileByte` and drained `ScalabilityChangedEvent.CurrentTier` into deformation samples, compromised-module signals, hull-deformed signals, and shader parameter metadata. That leaked binary hardware identity into deformation presentation/proof lanes.
Solution: Replaced the tier byte with `_cachedQualityProfileByte`, derived from the health-capped continuous `GlobalQualityWeight` as a 0..255 byte. Removed the scalability profile signal drain and cold registry tier read. Dent capacity and shader dent limit continue to use the existing continuous curves.
Rejected Alternatives: Keeping the tier event as metadata was rejected because downstream consumers can treat metadata as authority. Mapping profile tier through `ScalabilityTierProfiles.Normalize` was rejected because the source remains binary hardware identity.
Scalability potential: Low/Middle/High/Ultra deformation metadata now moves smoothly with quality and structural health pressure. Weak devices reduce tracked dent/shader upload capacity through continuous curves; high/ultra receive full overkill metadata without changing structural facts.
Hardware Impact: 0 us speed claim. Removed one cold registry read and one SignalBus snapshot scan; dent/deformation authority remains unchanged.

## Vocal Warning Radio Degradation Continuum

Problem: `VocalWarningSystem` cached `GlobalRegistry.ScalabilityTier` and drained scalability-tier signals every update to decide whether habitat integrity warnings got radio degradation. That made an audio presentation cue jump by binary hardware tier.
Solution: Removed the scalability signal drain, tier cache, and cold tier read. Habitat integrity warning distortion now resolves as a continuous lerp from 0.38 to 0.72 using a smooth `GlobalQualityWeight` curve. Warning ID, queue order, cooldown, subtitle, and telemetry routes are unchanged.
Rejected Alternatives: Keeping degradation as high-tier-only was rejected because audio presentation should shed continuously. Moving warning queue cadence by quality was rejected because warning timing and priority are player-safety feedback.
Scalability potential: Low devices use cleaner/cheaper radio treatment, middle devices receive partial degradation, high/ultra receive the full damaged-radio noir treatment on the same warning facts.
Hardware Impact: 0 us speed claim. Removed one per-update SignalBus scalability snapshot scan and one cold registry tier read.

## Prologue Acoustic Quality Continuum

Problem: `PrologueAcousticOrchestrator` cached low-memory and low-tier flags, drained scalability tier signals, disabled granular plasma audio on low tier, and emitted a low-tier proxy flag through the prologue transition state. That leaked binary hardware identity into an audio presentation route.
Solution: Removed the low-memory/low-tier fields, scalability signal drain, binary granular gate, and low-tier proxy flag publication. The prologue keeps publishing the existing quality byte, now derived directly from continuous `HomeostasisBrain.GlobalQualityWeight`; granular plasma stress is multiplied by a smooth polynomial quality curve.
Rejected Alternatives: Keeping the tier event as metadata was rejected because downstream audio can treat metadata as a branch key. Disabling granular plasma below a threshold was rejected because prologue audio should degrade continuously, not pop between completely absent and present.
Scalability potential: Low devices get reduced granular plasma stress, middle devices interpolate, and high/ultra receive full granular overdrive. Transition timing, stage identity, splashdown, portal blend, and low-pass/LFE facts remain unchanged.
Hardware Impact: 0 us speed claim. Removed one per-frame scalability signal scan and two cached binary hardware fields from the prologue audio route.

## Audio Smoke Proof Realignment

Problem: `AdvancedAcousticsSmokeTester` still required the exact Prologue and Vocal warning scalability signal drains, low-memory registry seed, and scalability payload handler that the runtime pass intentionally removed. The runtime was clean, but the proof artifact would reject the corrected architecture.
Solution: Updated the smoke tester to assert `ResolveGlobalQualityWeight01()` and smooth quality-curve/radio-distortion routes, while negative-checking the removed `ConsumeScalabilitySignals`, `ReadOnlySpan<ScalabilityChangedEvent>`, low-memory cache policy, and hardware tier seed strings for Prologue/Vocal warning.
Rejected Alternatives: Leaving the smoke tester stale was rejected because CI proof must encode the current contract. Deleting the smoke assertions was rejected because the audio bridge still needs a regression guard against reintroducing binary scalability drains.
Scalability potential: Low/Middle/High/Ultra audio presentation now has a test-enforced continuous quality route for Prologue plasma and Vocal radio degradation.
Hardware Impact: 0 us runtime. Editor-only proof update; `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -m:1` returned 0 errors and 132 pre-existing warnings.

## Player Critical Audio Quality Continuum

Problem: `PlayerCriticalProceduralAudioRenderer` cached `GlobalRegistry.ScalabilityTier`, `GlobalRegistry.QualityTier`, and `GlobalRegistry.H8_LOW_MEMORY_PROFILE`, then drained scalability snapshots to switch granular voice count, sonar SDF probes, reverb DSP tier, and kinetic impact fallback behavior by binary hardware identity.
Solution: Removed the cached tier/profile fields, scalability snapshot drain, payload handler, and low-tier kinetic fallback gate. The renderer now caches a per-frame continuous `HomeostasisBrain.GlobalQualityWeight`, smooths it with a polynomial curve, and uses that curve for granular voice capacity, sonar probe count, reverb tier selection, and a fade-in cheap impact layer.
Rejected Alternatives: Keeping scalability signals as an audio metadata lane was rejected because the renderer used them as branch selectors. Preserving clip-only kinetic fallback was rejected because it changed impact presentation discontinuously; the clip is now a minimum-quality layer, not an exclusive hardware route.
Scalability potential: Low devices keep a cheap impact layer, lower granular voice ceiling, and fewer sonar probes; middle devices interpolate; high/ultra reach full voice/probe counts and native convolution. Impact truth and signal admission remain unchanged.
Hardware Impact: 0 us speed claim. Removed a per-frame scalability snapshot scan and three cold hardware registry fields from the critical audio presentation path.

## Spatial Audio Virtual Voice Quality Continuum

Problem: `SpatialAudioManager` cached scalability tier and low-memory profile, drained scalability snapshots every tick, and clamped virtual voice quality/physical voice budget through binary Low/Mx350/low-memory hardware identity.
Solution: Removed the scalability event alias, snapshot drain, handler, cached tier/profile fields, and cached-tier accessors. Spatial audio policy now caches continuous `HomeostasisBrain.GlobalQualityWeight` once per frame, combines it with the native virtual-voice quality DTO, and smooths the result before resolving voice budget.
Rejected Alternatives: Keeping low-memory/tier clamps as a separate survival profile was rejected because virtual voice count is presentation budget and should follow the global quality continuum. Deleting the smoke coverage was rejected; the editor proof now asserts the continuous spatial-quality route.
Scalability potential: Low devices receive fewer active physical voices through the same continuous resolver, middle devices interpolate, and high/ultra get full physical voice budget. Listener AUP, source routing, and acoustic impulse facts remain unchanged.
Hardware Impact: 0 us speed claim. Removed one per-frame scalability snapshot scan and two hardware profile fields from the spatial audio presentation route. Build returned 0 errors and 161 warnings.

## Prologue Proxy Telemetry Consumer Removal

Problem: `PlayerCriticalProceduralAudioRenderer.RecordPrologueTransitionTelemetry` still consumed `AudioTransitionState.FlagLowTierProxy` and encoded it into DSP telemetry bit 2 after the Prologue producer stopped emitting that binary hardware proxy. A stale ABI flag in a downstream proof lane can be reinterpreted later as an active hardware branch.
Solution: Removed the `FlagLowTierProxy` telemetry branch from the player-critical consumer. Portal proximity, granular stress, splashdown, and nonfinite guard bits remain unchanged, so the telemetry route still proves the meaningful prologue audio facts.
Rejected Alternatives: Keeping the stale consumer was rejected because unused binary hardware identity must not survive in forensic output. Renaming the bit to "minimum quality" was rejected because the producer no longer owns or publishes that fact.
Scalability potential: Low/Middle/High/Ultra all publish the same prologue transition identity bits; presentation degradation remains in continuous quality curves already logged in Prologue and Player Critical audio passes.
Hardware Impact: 0 us speed claim. One dead branch was removed from telemetry recording. Build not launched because `VBCSCompiler` was active; targeted scan and `git diff --check` passed.

## Adaptive Stem Mixer Binary Quality Fallback Removal

Problem: `AdaptiveStemAudioMixer.DrainSignalInputs` still read `SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot()` and converted `CurrentQualityTier` into a fallback quality weight. The same file configured `DynamicMusicScalarSignal` with `lowTierFrameSignals: 8`, allowing a binary hardware profile to reduce scalar audio event capacity.
Solution: Removed the scalability-event drain and `ResolveQualityTierFallbackWeight`. The mixer now uses only the Homeostasis-owned vault lane `ScalabilityStateDTO.GlobalQualityWeight`, preserving the last sanitized continuous value when the handle is temporarily unavailable. Dynamic music scalar lane minimum capacity now equals the full 64-frame budget, and `AdvancedAcousticsSmokeTester` asserts the continuous route.
Rejected Alternatives: Keeping the tier fallback was rejected because quality-tier payloads are binary hardware identity, not the continuous global weight. Keeping the 8-signal low-tier capacity was rejected because dropped audio scalar events can alter musical presentation discontinuously.
Scalability potential: Low devices stretch the Burst audio-kernel cadence continuously toward 5 Hz and fade decorative depth/boss layers through polynomial quality; middle devices interpolate; high/ultra run near 60 Hz and full decorative layer weight. Stem identity, beat phase, biome transition, and tension facts stay on the same route.
Hardware Impact: 0 us speed claim. Removed one per-frame typed scalability snapshot scan and one tier-to-weight helper. Build not launched because `VBCSCompiler` remained active; targeted scan and `git diff --check` passed.

## Dynamic Music Scalar Lane Capacity Unification

Problem: After the adaptive stem mixer was corrected, `HectonMusicDirector` and `DynamicMusicGranularSynthesizer` could still reconfigure the same `DynamicMusicScalarSignal` lane with `lowTierFrameSignals: 8`. Whichever owner initialized last could restore binary event shedding for the shared music scalar route.
Solution: Set the music director and granular synth signal configuration to `lowTierFrameSignals: 64`, matching the full `maxFrameSignals` budget and the adaptive stem owner. The smoke tester now reads all three files and asserts full minimum-quality capacity.
Rejected Alternatives: Leaving only the adaptive owner patched was rejected because SignalBus configuration is shared by type, not by producer. Reducing all owners to 8 was rejected because music scalar event loss is a binary hardware behavior.
Scalability potential: Low/Middle/High/Ultra keep the same scalar-event route capacity. Weak devices still shed cost through continuous synth quality and kernel cadence; high/ultra can use all scalar updates for richer stingers and granular motion.
Hardware Impact: 0 us speed claim. No new hot-path work beyond preserving signal capacity. Build not launched because `dotnet` and `csc` were active; targeted scan and `git diff --check` passed.

## Acoustic Echo Quality Event Drain Removal

Problem: `AcousticEchoLocationRuntime.RefreshForFrame` refreshed `_cachedQualityWeightByte` from continuous `HomeostasisBrain.GlobalQualityWeight`, then called `ConsumeScalabilityChangedSignals`, which scanned `SignalBus<ScalabilityChangedEvent>` and re-read the same byte when any binary scalability event existed. This preserved a stale hot snapshot route in AI sensory.
Solution: Removed the scalability event drain and helper. Acoustic trail state, pending tap queue, portal/DSP hydration, AUP deltas, deterministic Burst tracking, and blackbox rows are unchanged. The optional `QualityWeightByte` still refreshes once per frame from the continuous global quality scalar.
Rejected Alternatives: Mapping `CurrentQualityTier` into a byte was rejected because acoustic head-sweep presentation should follow the continuous quality scalar, not hardware class. Removing `QualityWeightByte` entirely was rejected because it is part of existing explicit DTO layouts and a visual/head-sweep proof lane.
Scalability potential: Low devices can reduce the visual head-sweep amplitude through the existing quality curve; middle devices interpolate; high/ultra get full sweep amplitude. Predator acoustic target, trail intensity, source AUP, and hunt trigger facts remain hardware-invariant.
Hardware Impact: 0 us speed claim. Removed one typed scalability snapshot scan from the per-frame acoustic refresh path. Guarded build rerun passed with 0 errors and 152 warnings.
