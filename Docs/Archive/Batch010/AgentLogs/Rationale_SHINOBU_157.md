# SHINOBU_157 Rationale

## Decision 0 - Autopilot authority boundary
Problem: Huge submarines need obstacle avoidance without owning physics integration, voxel generation, weather, or pathfinding domains.
Solution: Publish a 64-byte AutopilotStateDTO and auxiliary fixed-size buffers through GlobalDataVault under VehiclesPhysics ownership. The job computes desired velocity and repulsion only; existing kinematic/motor systems remain the movement authority.
Rejected Alternatives: NavMeshAgent/NavMeshPath cannot represent massive free-volume submarines and violates the prompt. Physics.Raycast/SphereCast creates engine coupling and cannot sample the Voxel SDF deterministically in Burst.
Scalability potential: Low uses 5 feelers and short march count; middle raises steps; high/ultra expands to 32 feelers and richer debug data through the same buffers.
Hardware Impact: Low-end i3/MX350 avoids managed path objects and engine casts; expected hot-path cost stays below 0.1 ms for 16 submarines at low quality.

## Decision 1 - SDF as first-class obstacle oracle
Problem: Collision avoidance must work against voxel cave geometry and not depend on scene colliders.
Solution: Jobs sample encoded byte SDF from DataVault, with a mock SDF job as deterministic fallback until the voxel sonar payload is available.
Rejected Alternatives: Collider sweeps and precomputed graph nodes were rejected as non-Burst, less deterministic, and not AUP-native.
Scalability potential: Low samples fewer steps; middle samples trilinear SDF; high/ultra adds gradient-based repulsion and denser feeler fan.
Hardware Impact: Byte SDF has predictable memory bandwidth and no allocator pressure on low-end hardware.

## Decision 2 - Ray feeler fan
Problem: A 100-meter submarine needs early obstacle warning without NavMesh, node grids, or engine casts.
Solution: EvaluateCollisionAvoidanceJob generates 5-32 deterministic ray-marched feelers from velocity/rotation, samples the encoded SDF, resolves open-space normals, and accumulates a potential-field repulsion vector.
Rejected Alternatives: Physics.SphereCast and main-thread Raycast were rejected because they couple navigation to collider authoring and stall the main thread.
Scalability potential: Low=5 feelers/4 steps, Middle=interpolated counts, High=more steps, Ultra=32 feelers with richer hit telemetry and gizmos.
Hardware Impact: Low-end i3/MX350 path estimates under 0.1 ms for 16 vehicles; high-end uses saved budget for visible debug overkill.

## Decision 3 - Flow compensation
Problem: Abyssal currents push submarines off route, but the autopilot cannot wait on Agent 105 implementation details.
Solution: ComputeDesiredVelocityJob samples a Vault-backed flow buffer when present and falls back to deterministic analytic currents; desired velocity subtracts flow * compensation weight.
Rejected Alternatives: Main-thread HectonFluidEngine sampling was rejected because it creates a direct domain dependency and cannot run inside Burst.
Scalability potential: Low uses analytic fallback, middle/high can consume a coarser or denser flow grid without changing the autopilot ABI.
Hardware Impact: Flow sampling adds bounded trilinear float3 reads; fallback is a few trig ops per vehicle.

## Decision 4 - Black box and editor facade
Problem: Autopilot faults must be diagnosable and tunable without recompiling gameplay code.
Solution: RecordAutopilotTelemetryJob writes a 300-entry Vault ring; faults dump to Docs/AgentLogs/Dump_NAVIGATION_SURGEON.bin. The UI Toolkit tuner writes the tuning DTO and injects AUP waypoints by Scene View plane intersection.
Rejected Alternatives: Debug.Log streams and inspector-only serialized fields were rejected because they are not deterministic state and do not survive crash triage.
Scalability potential: Low keeps telemetry fixed-size; high/ultra can draw all 32 feelers per submarine through the debug buffer.
Hardware Impact: Hot path writes one 64-byte telemetry entry per frame, not per feeler; editor-only facade is outside runtime cost.

## Decision 5 - Owner-local Vault IDs instead of global enum widening
Problem: Extending `H8Memory.cs` for a single autopilot route widens the global compile surface and violates the owner-local first rule.
Solution: Declare IDs 71592-71603 in `SubmarineAutopilotVaultRoute` as typed `BufferID` constants inside the autopilot domain, and document that route in `ROUTE_CARD_SHINOBU_157_AUTOPILOT.md`.
Rejected Alternatives: Adding `BufferID.Shinobu157Autopilot*` entries to the central enum was rejected because it creates merge pressure and unnecessary core recompilation.
Scalability potential: Low/Middle/High/Ultra all use the same route IDs; adding visual-overkill telemetry later remains domain-local unless another owner consumes it.
Hardware Impact: Runtime cost is unchanged; engineering impact is lower compile-wall blast radius and less core churn on low-end developer hardware.

## Decision 6 - Low-quality mathematical collapse
Problem: The previous solver still performed multi-step trilinear SDF and gradient taps at low quality, which is too expensive under thermal throttling.
Solution: `GlobalQualityWeight` now controls solver cadence (12->1 frames), feelers (5->32), steps (1->12), nearest/trilinear interpolation weight, and whether six-tap gradient normals are sampled.
Rejected Alternatives: A binary low/high hardware branch was rejected. Fixed 4-step trilinear probing was also rejected because it does not collapse enough for Quest/MX350 thermal load.
Scalability potential: Low uses 5 feelers, 1 nearest SDF lookup, no gradient, reduced cadence; Middle blends nearest to trilinear; High/Ultra restores dense feelers, 12 steps, and gradient-derived repulsion.
Hardware Impact: Low-end i3/MX350 path drops from roughly 320 base SDF samples/frame for 16 subs plus gradient taps to 80 base samples on solver frames, further reduced by cadence. Exact profiler us remains PENDING VERIFICATION.

## Decision 7 - Burst alias contract and cold-path byte slices
Problem: Raw pointer fields without alias proof make Burst conservative, while fault dump/CSV paths used managed scratch patterns that weaken the zero-GC story.
Solution: Added `[NoAlias]` to distinct Vault pointer fields in every Burst job. Replaced dump scratch with `FileStream.Write(ReadOnlySpan<byte>)` and changed CSV ingest to `Span<byte>` read plus `ReadOnlySpan<byte>` parsing.
Rejected Alternatives: Leaving pointer aliasing implicit was rejected because it can block SIMD. `byte[]` fault scratch and `ReadByte()` were rejected as unnecessary managed/file-loop overhead.
Scalability potential: Low benefits from simpler NEON/AVX-friendly memory assumptions; High/Ultra can spend saved CPU on denser feelers and editor debug.
Hardware Impact: Expected improvement is vectorizer-dependent; no profiler proof yet because CPU guard blocked compile/runtime validation.

## Decision 8 - Vault fixed profile table instead of NativeHashMap
Problem: The task named `NativeHashMap`, but this project route requests persistent memory through `GlobalDataVault`, which currently exposes typed fixed buffers rather than a persistent hash-map object contract for this domain.
Solution: Implement a fixed 32-slot open-addressed `AutopilotHandlingProfileDTO` table in Vault, keyed by FNV-1a lowercase hashes parsed from `ReadOnlySpan<byte>`.
Rejected Alternatives: Creating a private persistent `NativeHashMap` field was rejected by the Vault Law. Inventing a new global hash-map Vault surface was rejected as global-authority overreach for a cold tuning file.
Scalability potential: Low ships with 32 fixed rows and no allocator; High/Ultra can increase capacity by changing one Vault capacity constant if design data requires it.
Hardware Impact: Cold parser only; hot path remains 0 B GC and no persistent private native collection fragmentation.

## Decision 9 - FixedTick Vault handle cache
Problem: Calling `GetBufferHandle` from the steady fixed tick path makes boot-time Vault negotiation look like gameplay work and risks dictionary/lock overhead inside the frame budget.
Solution: Cache resolved handles behind `_resolvedVehicleCapacity`; steady state returns through `AreVaultHandlesReady`, while capacity changes mark the solver uninitialized and force deterministic re-init.
Rejected Alternatives: Re-resolving every handle each FixedTick was rejected as a compile-wall-safe but frame-budget-hostile pattern.
Scalability potential: Low benefits most because reduced management overhead preserves the small feeler budget; High/Ultra spend the saved CPU on denser SDF probing.
Hardware Impact: Removes repeated Vault handle negotiation from the hot path; exact microseconds remain PENDING because compile/profiler proof is blocked by unrelated project file errors.

## Decision 10 - Editor-only reflection and read fences
Problem: The layout validator used reflection and editor read APIs could sample Vault rows while scheduled jobs held the route locks.
Solution: Wrap `AutopilotStateDTOLayout` in `UNITY_EDITOR` and make read facades fail closed while `_buffersLocked`, `_solverPending`, or `_initPending`.
Rejected Alternatives: Runtime reflection was rejected because offset validation is an editor/static proof. Torn editor reads were rejected because they corrupt the tuning/debug story even if gameplay remains safe.
Scalability potential: Same behavior across tiers; high/ultra debug remains accurate because it reads only stable rows.
Hardware Impact: No hot-path cost; reduces player-build metadata/reflection surface and editor-side race noise.

## Decision 11 - Typed editor telemetry readout
Problem: The tuner facade was editor-only, but the telemetry status still formatted values into a managed string every refresh and missed the `Hecton8.Core` namespace required for AUP injection.
Solution: Add the missing `Hecton8.Core` import and replace the formatted status line with disabled `IntegerField`/`FloatField` readouts updated through `SetValueWithoutNotify`.
Rejected Alternatives: Keeping `StringBuilder` plus `ToString()` was rejected because the prompt explicitly asks for a zero-GC telemetry facade; moving Scene View AUP conversion into runtime was rejected as unnecessary boundary creep.
Scalability potential: Low/Middle/High/Ultra runtime cost is unchanged; high/ultra editor debug can refresh telemetry without allocating formatted strings.
Hardware Impact: Runtime impact is 0 us. Editor allocation pressure is reduced by eliminating one formatted managed status string and numeric `ToString()` calls per 0.25s refresh.

## Decision 12 - Handling profiles must alter steering, not just parse
Problem: The CSV parser wrote profile rows to Vault, but the steering job did not consume those rows, making Task 17 a cold data demo instead of a controllable handling surface.
Solution: `ComputeDesiredVelocityJob` now reads the Vault `AutopilotHandlingProfiles` table through a `[NoAlias]` pointer, resolves the profile hash stored in `AutopilotStateDTO.SubmarineHashID`, and applies max turn rate, acceleration limit, speed scale, and repulsion scale. Cold defaults seed default/scout/freighter rows, and the editor facade can assign those hashes per selected submarine.
Rejected Alternatives: A managed dictionary or private persistent `NativeHashMap` was rejected by the Vault Law. String submarine type matching was rejected because hashes are deterministic and cheaper in Burst.
Scalability potential: Low uses one default-profile open-address probe and coarser avoidance; middle/high/ultra can use differentiated profiles to buy smoother, heavier craft silhouettes without changing the ABI.
Hardware Impact: Low-end i3/MX350 gains actual controllability with a bounded one-to-32 probe table lookup; default rows resolve in one probe. Flow low-tier sampling now drops from 8 grid taps to 1 nearest-cell tap below the interpolation gate.

## Decision 13 - Runtime completion helper instead of World Dispatcher reference
Problem: The runtime autopilot file referenced `Hecton8.World.DispatcherJobSwap` only to complete two local job handles, creating an unnecessary sibling namespace dependency for an owner-local vehicle subsystem.
Solution: Replace it with a private `TryCompleteJobHandle(ref JobHandle, bool)` helper using `JobHandle.IsCompleted` for non-blocking post-fixed checks and `Complete()` only when the handle is ready or shutdown forces completion.
Rejected Alternatives: Keeping the World dependency was rejected as compile-wall exposure. Arbitrary `Complete()` in FixedTick was rejected because it can stall the main thread.
Scalability potential: Same behavior across Low/Middle/High/Ultra; no quality-tier branch is introduced.
Hardware Impact: Runtime branch cost is equivalent; engineering impact is lower dependency surface for weak developer machines and future asmdef isolation.

## Decision 14 - Transactional Vault lock rollback and write fences
Problem: Public write facades could attempt Vault writes while the solver route held `_buffersLocked`, and the old partial-lock rollback unlocked every SHINOBU route buffer even when only a subset was acquired. `GlobalDataVault.TryUnlockBuffer` is a refcount decrement, not an owner-token release, so broad rollback could corrupt another writer's lock count.
Solution: Add `_buffersLocked` guards to `SlowTick`, `TryWriteTargetAup`, `TryWriteHandlingProfileHash`, and `TryWriteTuning`. Add an owner-local `_lockMask` with one bit per locked Vault buffer; `UnlockBuffers()` now releases only the bits acquired by the current navigator lock transaction.
Rejected Alternatives: Trusting pending-job booleans alone was rejected because `_buffersLocked` is the direct ownership boundary. Unlocking the whole route after a failed acquisition was rejected because lock refcounts are shared and do not record the releasing owner.
Scalability potential: Low/Middle/High/Ultra solver math is unchanged; high/ultra debug/editing remains deterministic because editor writes fail closed during the active job window.
Hardware Impact: Hot Burst path cost is unchanged. Main-thread lock rollback becomes O(acquired bits) instead of O(route size), and more importantly prevents a rare cross-writer lock refcount fault that would be catastrophic under parallel editor/runtime agents.

## Decision 15 - Route handoff as Span DTO write, not mission graph ownership
Problem: The solver could advance route DTOs, but there was no zero-GC public ingress for a Logistics/Editor owner to seed a route without reaching into Vault internals or allocating managed waypoint objects.
Solution: Add `TryWriteRoute(int, ReadOnlySpan<AutopilotWaypointDTO>, float, uint)`. It validates finite double3 AUP targets, writes a fixed per-submarine slice of `AutopilotWaypoints`, initializes `AutopilotRouteRangeDTO`, and points `AutopilotStateDTO.TargetAUP` at the first waypoint. The method fails closed during job locks and uses local acquired-lock booleans for synchronous rollback.
Rejected Alternatives: `List<Vector3>`, mission-graph object references, or a direct Logistics assembly dependency were rejected because this domain owns only the navigation math ABI. Reusing the scheduled-job `_lockMask` for a synchronous facade write was rejected to keep job-route ownership and editor/logistics writes separate.
Scalability potential: Low uses short fixed route slices and coarse feelers; Middle/High/Ultra can seed longer routes up to the same Vault capacity without changing the Burst solver ABI.
Hardware Impact: Hot Burst path remains unchanged. Cold route ingress is O(route count) with no managed allocation and no path node objects; low-end i3/MX350 avoids graph traversal and collider path staging in the autopilot domain.

## Decision 16 - Editor route probe must exercise route advancement, not only single targets
Problem: The editor facade could write one target AUP, but it did not exercise the multi-waypoint route DTO path that Task 09 relies on.
Solution: Add `Scene Click Route` mode. One Scene View click constructs a three-point dogleg route with `stackalloc Span<AutopilotWaypointDTO>` and calls `TryWriteRoute`; the click still uses `HandleUtility.GUIPointToWorldRay` plus plane intersection, not Physics.Raycast.
Rejected Alternatives: `List<AutopilotWaypointDTO>` staging, ScriptableObject route assets, or a direct Logistics graph reference were rejected because this is an editor test facade, not route ownership. A single target write was retained but no longer treated as proof of route advancement.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; high/ultra editor testing can rapidly generate dogleg routes to visualize feeler repulsion and route cursor advancement.
Hardware Impact: Runtime cost is 0 us. Editor click path uses stack memory for three DTOs and writes fixed Vault rows, avoiding managed route allocations.

## Decision 17 - Route flags and capacity must be explicit ABI facts
Problem: The route writer used raw `1u` flags and derived waypoint slices from serialized `vehicleCapacity` even after Vault handles were resolved. That leaves binary records harder to audit and can mismatch active capacity if inspector state and resolved Vault capacity diverge.
Solution: Add `WaypointFlagActive` and `RouteFlagActive` constants, and derive route slice capacity from `_resolvedVehicleCapacity` when available.
Rejected Alternatives: Keeping raw literals was rejected because route records are blittable binary facts. Trusting inspector capacity after Vault negotiation was rejected because the Vault route is the active memory contract.
Scalability potential: Low/Middle/High/Ultra route capacity math remains fixed and deterministic; route writer behavior is stable after capacity normalization.
Hardware Impact: Constants fold away. Capacity selection is cold/editor ingress only and prevents wrong-slice writes that would be expensive to diagnose through telemetry.

## Decision 18 - All Vault DTOs need editor-time layout proof
Problem: The initial layout validator proved only `AutopilotStateDTO`, while avoidance, feeler, waypoint, route, tuning, telemetry, and handling profile DTOs also cross Vault, rollback, debug, or black-box boundaries.
Solution: Extend the editor-only `AutopilotStateDTOLayout` guard with `ValidateAll()` and exact per-DTO size/offset checks, including every tuning, telemetry, and handling profile field. Reflection remains inside `UNITY_EDITOR`, so player builds do not carry the validation surface.
Rejected Alternatives: Trusting `[StructLayout(Explicit)]` without an executable editor check was rejected because silent DTO drift is a binary compatibility failure. Moving reflection into runtime was rejected because layout checks are an import/editor proof, not gameplay work.
Scalability potential: Same across Low/Middle/High/Ultra; the proof protects all tiers from ARM64 layout drift.
Hardware Impact: 0 us runtime. Editor/import validation cost is negligible and prevents expensive ARM64 alignment regressions.

## Decision 19 - Authored quality cap must not be overwritten by thermal quality
Problem: `ScheduleSolver` overwrote `AutopilotTuningDTO.GlobalQualityWeight` with the live `HomeostasisBrain.GlobalQualityWeight` every frame. A thermal dip could therefore permanently lower the authored tuning cap until a designer rewrote the DTO, and rollback snapshots had no explicit lane distinguishing authored cap from the scalar actually used by jobs.
Solution: Reuse tuning offset 120, previously padding, as `ResolvedQualityWeight`. `GlobalQualityWeight` remains the authored/network cap. Scheduler and read facades compute `ResolvedQualityWeight = quantized min(HomeostasisBrain.GlobalQualityWeight, GlobalQualityWeight)` and pass only the resolved scalar into Burst cadence, feeler density, telemetry, and flow interpolation.
Rejected Alternatives: Keeping one mutable `GlobalQualityWeight` field was rejected because it conflates human tuning and thermal pressure. Adding a new global quality contract was rejected as cross-domain authority creep. Reading `HomeostasisBrain.GlobalQualityWeight` inside Burst jobs was rejected because jobs need one frozen scalar per scheduled batch.
Scalability potential: Low still collapses to sparse feelers, nearest SDF, nearest flow, and reduced cadence; Middle/High/Ultra recover continuously as the resolved scalar rises, while designers can clamp the whole route for testing or platform-specific tuning.
Hardware Impact: Runtime scheduling adds one min, finite sanitize, and 0.001 quantization. The cost is below measurement noise; the benefit is avoiding sticky low-quality navigation and giving rollback snapshots a concrete resolved-quality lane.

## Decision 20 - Black-box dump paths must satisfy both AGENTS and XML task contracts
Problem: The XML task names `Dump_NAVIGATION_SURGEON.bin`, while AGENTS mandates `Dump_[YourID].bin`. A single dump path would make one authority look false during crash forensics.
Solution: Fault dump writes the same 300-entry telemetry span to `Dump_SHINOBU_157.bin` and `Dump_NAVIGATION_SURGEON.bin`. The writer uses `FileStream.Write(ReadOnlySpan<byte>)` directly over Vault telemetry memory.
Rejected Alternatives: Renaming the existing file only was rejected because it breaks the XML prompt evidence path. Writing text logs or managed byte scratch was rejected because the binary black box must be memcpy-adjacent and allocation-free for telemetry bytes.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; the alias only affects fatal or slow-solver forensic output.
Hardware Impact: 0 us normal hot path. Fault path writes an additional 19.2 KB file for 300 * 64-byte telemetry entries, which is acceptable because the system is already in crash/forensic mode.

## Decision 21 - Runtime must not import World for editor-only AUP conversion
Problem: `SubmarineAutopilotSdfNavigator.cs` carried an unused `using Hecton8.World`, which violates the compile-wall rule even if no symbol was referenced.
Solution: Remove the runtime World import. Keep `Hecton8.World` only in `SubmarineAutopilotTunerWindow.cs`, where Scene View target injection needs editor-only AUP conversion.
Rejected Alternatives: Keeping the unused import was rejected because future asmdef isolation would turn it into an unnecessary sibling assembly route. Moving editor AUP conversion into runtime was rejected as boundary creep.
Scalability potential: Runtime math is unchanged across all tiers; compile-wall isolation remains cleaner for every hardware target.
Hardware Impact: 0 us runtime. Developer iteration risk is reduced because the runtime domain no longer advertises a World namespace dependency.

## Decision 22 - Low-frequency solver cadence must preserve simulation time
Problem: When `GlobalQualityWeight` drops cadence toward 5Hz, the solver previously received only the current fixed tick delta. Turn-rate and acceleration clamps therefore acted as if only one 60Hz tick elapsed, making low-tier submarines under-steer after skipped updates.
Solution: Accumulate sanitized dispatcher fixed delta across skipped or pending solver windows, clamp the accumulated window to 0.25s, and pass that value into `ComputeDesiredVelocityJob`. `ScheduleSolver` now returns `bool`, so the accumulated window resets only after the job is actually scheduled.
Rejected Alternatives: Multiplying by the resolved cadence was rejected because cadence can change while pending or skipped ticks accumulate. Ignoring skipped time was rejected because it makes quality shedding alter vehicle handling more than intended.
Scalability potential: Low-tier cadence still sheds SDF work, but the solver applies turn and acceleration over the real deterministic window. Middle/High/Ultra converge to the same per-frame behavior because accumulated delta is approximately one fixed tick when cadence is 1.
Hardware Impact: Adds one float add/min per fixed tick and one scheduler bool branch. No extra SDF or flow samples are introduced; steering quality improves specifically under thermal throttling.
