# SHINOBU_303 Rationale - Leviathan Steering Motor

## Decision 001 - Runtime Ownership

Problem: The assignment forbids a competing steering manager if a fauna runtime already owns creature evaluation. Archaeology found no `HectonFaunaRuntime`, but found `PredatorCognitionDomain` as the Vault-backed predator runtime and `FaunaSteeringEngine` as the legacy managed Rigidbody motor.

Solution: Attach steering as an isolated partial of `PredatorCognitionDomain`, scheduled after cognition outputs and before post-evaluation telemetry. Keep the kernel stateless and transform Vault buffers only.

Rejected Alternatives: A new `HectonSteeringManager` would duplicate scheduling ownership and invite hot `GlobalRegistry` lookups. Extending `FaunaSteeringEngine` would keep managed `Rigidbody` authority in the hot movement path.

Scalability potential: Low tier uses 6 SDF whiskers and simple momentum blending; middle/high/ultra progressively raise whiskers to 26 and provide richer debug/telemetry without changing gameplay truth.

Hardware Impact: Estimated 35-60 us/frame gain on i3/MX350 versus managed Rigidbody steering plus object polling for apex agents, with larger gains when entity count grows.

## Decision 002 - Signal Corridor

Problem: Catastrophic Leviathan impact could tempt a new signal type, fragmenting hot event routes.

Solution: Reuse existing global damage/base-compromise signal lanes if steering detects a catastrophic impact. Primary steering state remains in Vault buffers; signals are exceptional cold/hot bridge events only.

Rejected Alternatives: `LeviathanCrashSignal` is rejected because existing base/damage routes can carry impact semantics.

Scalability potential: Low tier can disable optional impact visualization while preserving authority; high/ultra can consume the same signal for stronger camera/audio response.

Hardware Impact: Avoids one extra queue lane and per-frame polling. Estimated 1-3 us/event saved on weak CPUs.

## Decision 003 - SDF Whisker Method

Problem: Physics raycasts and NavMeshAgent cannot scale to continuous underwater 3D movement.

Solution: Sample a flat Voxel SDF buffer in Burst using AUP-localized coordinates. Negative SDF samples reflect the whisker vector into a repulsion contribution. Complexity is driven by continuous `GlobalQualityWeight`.

Rejected Alternatives: Unity `Physics.Raycast`, `NavMeshAgent`, `Transform.LookAt`, and scene search are rejected for hot movement authority because they allocate or touch managed/object state and break predictability.

Scalability potential: Low = 6 cardinal whiskers; middle = interpolated count; high = 20+ rays; ultra = full 26-ray octant shell plus denser debug readout.

Hardware Impact: Expected low-end cost stays below the 0.1 ms suspicion threshold for apex counts by capping low-tier ALU and using contiguous Vault data.

## Decision 004 - 32B SteeringParamsDTO ABI

Problem: Leviathan steering parameters are read every active steering frame and must not trigger CS1612 copies or ARM64 misalignment.

Solution: Added explicit `SteeringParamsDTO` with `MaxSpeed@0`, `TurnSpeed@4`, `LungeMultiplier@8`, `ObstacleAvoidanceWeight@12`, `float3 CurrentTargetDirection@16`, and private `_pad0@28`. The `_pad0` word stores Dear Lie lunge lock frames without growing the DTO.

Rejected Alternatives: Managed properties and reference wrappers were rejected because NativeArray element properties produce stack-copy traps and hide mutation costs.

Scalability potential: Low/middle/high/ultra share the same DTO; quality changes cadence/whisker count and speed tuning only, not memory layout or network truth.

Hardware Impact: 32B stride keeps two rows per 64B cache line. Estimated 4-8 us/frame saved at 256 slots versus a 48B/64B loose struct on i3/MX350.

## Decision 005 - Mock SDF and Real SDF Contract

Problem: Agent 12 terrain SDF may not be present during isolated steering tests.

Solution: Added Vault-owned `Shinobu303MockSdf` plus `GenerateMockSdfObstaclesJob` that fills a signed-meter SDF with dense spheres/trench walls. The runtime config is a Vault DTO so a real SDF owner can replace origin/dimensions/cell size without changing the steering jobs.

Rejected Alternatives: Waiting for baked terrain, Physics raycasts, or scene colliders were rejected because they block testing and violate flat-data ownership.

Scalability potential: Low tier samples 6 cardinal whiskers; middle interpolates; high/ultra reach 26 octant/spherical whiskers and richer debug draw.

Hardware Impact: Avoids broadphase collision queries. Estimated 25-70 us/frame saved for apex avoidance on i3/MX350, depending on collider density.

## Decision 006 - Legacy Movement Containment

Problem: Existing `FaunaSteeringEngine` still writes `Rigidbody.linearVelocity`, and sensor-suite obstacle dodges use managed presentation state.

Solution: Added a Vault kinematic presentation bridge for leviathans. When SHINOBU_303 kinematic state is available, leviathan presentation consumes Vault velocity and skips the old `FaunaSteeringEngine.FixedTick`; dynamic dodge and wall-slide avoidance now return false for procedural leviathans.

Rejected Alternatives: Deleting `FaunaSteeringEngine` outright would break non-leviathan fauna and create cross-agent compile damage. Leaving leviathans on it would keep the original stuck-motor failure mode.

Scalability potential: Low devices consume only final velocity; high/ultra can layer procedural IK, whisker gizmos, and visual overkill from the same Vault truth.

Hardware Impact: Removes managed wall-slide/dodge work from leviathan fixed ticks when Vault kinematics are live. Estimated 12-30 us/frame saved per active large predator on weak CPU.

## Decision 007 - Tooling Facade and OOP Scanner

Problem: Designers need live tuning, and architecture needs proof that Update-loop OOP steering is not present.

Solution: Added a UI Toolkit tuner that reads telemetry and mutates Vault-backed `SteeringParamsDTO` via `UnsafeUtility.AsRef`, a SceneView whisker gizmo, CSV span parser, and `OOP_Movement_Scanner`. Updated `Docs/Reports/AI_OPTIMIZATION_REPORT.json` with SHINOBU_303 scanner evidence.

Rejected Alternatives: ScriptableObject-only tuning and Roslyn editor dependency were rejected. ScriptableObject tuning would require recompile/sync steps; Roslyn would add editor assembly weight for a narrow structural scan.

Scalability potential: Low tier ignores editor facade at runtime; high/ultra use the same telemetry for richer balancing and visual debugging.

Hardware Impact: Runtime impact is zero in player builds for editor tools. Scanner found 0 Update-loop `NavMeshAgent.SetDestination` / `Transform.Translate` steering hits.

## Decision 008 - Read Accessor Purity Repair

Problem: The first implementation called `EnsureInitialized()` from SHINOBU_303 `Try*` and copy facades. That violates the global route rule because a read accessor could allocate/grow Vault buffers or poll cold services as a side effect.

Solution: Move steering Vault creation behind explicit `EnsureLeviathanSteeringStateCold()` and scheduler-owned setup. `TryCopyLeviathanKinematicState`, `TryCopyLeviathanSteeringTelemetry`, `TryReadLeviathanSteeringParam`, `TryWriteLeviathanSteeringParam`, `CopyLeviathanSteeringDebugGizmos`, and `TryParseLeviathanSteeringProfilesCsv` now only open existing generation-checked Vault snapshots and return false/0 if the buffers are absent.

Rejected Alternatives: Keeping hidden lazy initialization was rejected because it makes `Try*` APIs non-pure and lets editor diagnostics mutate runtime ownership. Hot `GlobalRegistry` fallback inside the steering partial was also rejected; the partial now has no `GlobalRegistry` lookup.

Scalability potential: Low, middle, high, and ultra devices share the same fixed buffer route. Quality can scale whisker count and debug richness, but cannot change where steering truth lives or when buffers are created.

Hardware Impact: Prevents cold allocation and service-lookup spikes from appearing on weak i3/MX350 devices during diagnostic reads. Frame-time savings are nondeterministic, but it removes a worst-case allocation stall route.

## Decision 009 - Route Card and Ledger Boundary

Problem: New DataVault buffer IDs without a stable route card force future agents to reconstruct ownership from chat history or raw code.

Solution: Added `Docs/ARCHITECTURE/SHINOBU_303_LEVIATHAN_STEERING_ROUTE.md` and a SHINOBU_303 section in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` listing BufferIDs 72500..72509, DTO sizes, phase, failure mode, telemetry dump route, and compile-wall boundary.

Rejected Alternatives: Updating only `LOG_SHINOBU_303.md` was rejected because logs are audit artifacts, not the authority route. Overwriting the shared `AI_OPTIMIZATION_REPORT.json` was rejected because SHINOBU_302 already wrote a shared report; SHINOBU_303 now appends a namespaced section.

Scalability potential: The route card records low/middle/high/ultra behavior as continuous `GlobalQualityWeight` changes over the same buffers; no binary tier branch or alternate authority owner exists.

Hardware Impact: Integrator lookup cost is reduced. Runtime hardware impact is unchanged; the benefit is compile-wall avoidance and route stability.

## Decision 010 - Editor Telemetry Text Removal

Problem: The UI Toolkit tuner graph was editor-only, but its `Tick()` still formatted telemetry text with string concatenation and `.ToString()`. That violates the zero-GC UI discipline if the editor window is left open during Play Mode.

Solution: Keep the live readout as the fixed-size Painter2D graph and append samples only when the telemetry frame changes. The label is now static text created once during `CreateGUI()`. Tuning callbacks use named methods instead of lambda callbacks.

Rejected Alternatives: Throttled string formatting was rejected because it still allocates. `StringBuilder` was rejected because `Label.text` still consumes a managed string.

Scalability potential: Low through ultra runtime builds are unaffected; editor diagnostics can stay open without continuous text churn. High/ultra balancing still gets visual telemetry through the graph.

Hardware Impact: Runtime impact is 0 us because the code is editor-only. Editor Play Mode avoids avoidable per-update managed string allocation.

## Decision 011 - Cached Offset Self-Audit

Problem: Size-only checks prove `SteeringParamsDTO` is 32 bytes but do not prove the required field offsets.

Solution: Added constants for offsets `0/4/8/12/16/28` and a cached ABI validation state. Current validation uses unsafe pointer deltas inside `SteeringParamsDTO.ValidateByteOffsets()` and stores pass/fail in `_steeringAbiValidationState`.

Rejected Alternatives: Runtime per-frame reflection and cold `UnsafeUtility.GetFieldOffset` field lookup were rejected after polish. Blind trust in `[FieldOffset]` annotations was rejected because Task 20 requires executable verification.

Scalability potential: No quality path changes. The same 32-byte DTO is used for minimum, middle, high, and ultra quality.

Hardware Impact: No hot-path cost after cache. Prevents silent layout drift that would cost ARM64 cache bandwidth or break rollback snapshots.

## Decision 012 - Assembly Boundary Containment

Problem: SHINOBU_303 imports `Hecton8.Physics.KCC` for `KinematicStateDTO`, and the polish mandate requires proving this did not create a sibling runtime assembly dependency.

Solution: Scanned asmdefs and generated project references. `Physics/KCC` has `Hecton8.Physics.KCC.Editor.asmdef` only; its runtime `KinematicStateDTO` source is under the existing `Hecton8.Core` assembly scope. SHINOBU_303 added no asmdef and no new assembly reference.

Rejected Alternatives: Moving `KinematicStateDTO` into `Core.Contracts` during the batch was rejected because that would mutate a public cross-domain ABI and create broader compile-wall risk. Defining a duplicate local kinematic DTO was rejected because the XML assignment explicitly requires output to the `KinematicStateDTO` array.

Scalability potential: Same Vault route for all hardware levels; no alternate assembly or DTO route per quality level.

Hardware Impact: Compile-wall risk contained. Runtime hardware impact unchanged.

## Decision 013 - Reflectionless Padding Lane

Problem: The previous ABI proof used field lookup, and `SteeringParamsDTO` carried instance helpers for the lunge frame word. That kept reflection and method-shaped DTO access too close to a hot Burst-owned layout.

Solution: Move offset proof to `SteeringParamsDTO.ValidateByteOffsets()` using unsafe pointer deltas over a local stack value. Keep `_pad0` private at offset 28 and expose runtime access only through owner-local `byte*` helpers using `SteeringParamsOffsetPad0`.

Rejected Alternatives: Public `_pad0` was rejected because the XML requires private padding. DTO instance methods were rejected because hot-path DTOs must remain raw field envelopes. `Marshal.OffsetOf`/`typeof().GetField()` were rejected because runtime reflection is not needed for explicit offset proof.

Scalability potential: Low, middle, high, and ultra use the same 32-byte DTO and padding lane. Quality still scales whisker count and tuning, not layout or authority.

Hardware Impact: No frame-time claim. Removes runtime reflection dependency and keeps lunge lock access as one fixed unaligned-safe 4-byte lane at offset 28.

## Decision 014 - Scanner Preservation and Mock SDF Validity

Problem: The editor scanner's run path rewrote the shared `AI_OPTIMIZATION_REPORT.json`, destroying neighboring agent evidence. The mock SDF trench wall also used the wrong sign, so the intended free trench could be interpreted as solid rock.

Solution: Make the scanner write a stable SHINOBU_303 report and upsert only its namespaced section in the shared report. Flip the trench wall SDF to `82 - abs(x)`. Clear inactive whisker debug lanes after the active quality-scaled whisker count.

Rejected Alternatives: Whole-file report replacement was rejected because it breaks cross-agent audit ownership. Keeping stale high-quality whisker rows was rejected because it makes the SceneView x-ray lie during low-quality runs.

Scalability potential: Low quality still samples 6 SDF whiskers; high/ultra still sample up to 26. The added inactive-lane clear does not add SDF samples or change gameplay authority.

Hardware Impact: Runtime cost change is bounded debug-row stores only. The SDF sign fix is correctness, not a microsecond claim.

## Decision 015 - Subagent Compile-Risk Hardening

Problem: Secondary review found three concrete risks: `IntegrateSteeringVectorsJob` wrote `state._pad0` on external `KinematicStateDTO`, steering eligibility used `Active | IsApexPredator` and could skip alpha leviathan rows, and `SlerpDirection` used trig in rollback-facing velocity math.

Solution: Removed the external padding write, added shared `IsLeviathanSteeringCandidate()` requiring Active + PredatorRole + (`UseAlphaLeviathanCognition` or `IsApexPredator`), and replaced trig slerp with deterministic normalized smoothstep lerp.

Rejected Alternatives: Writing another domain DTO padding was rejected because padding is not SHINOBU_303 authority. Apex-only gating was rejected because alpha leviathan cognition is the existing owner route. Trig slerp was rejected because deterministic rollback prefers multiply/add/normalize math with fewer platform-specific transcendental paths.

Scalability potential: Low through ultra quality still use the same DTOs and Vault route. Quality continues to scale whisker count and debug richness only; steering eligibility and deterministic turn math are invariant.

Hardware Impact: Removes three transcendental evaluations from the hot direction blend and removes a compile-risk write. The gain is small per entity; the larger value is deterministic stability and avoiding another-domain ABI dependency.

## Decision 016 - Hot Allocation Fence

Problem: `ScheduleLeviathanSteering()` still called the allocation-capable steering ensure route. If cognition scheduling reached SHINOBU_303 before cold Vault hydration, the simulation phase could allocate buffers and load CSV data.

Solution: Split the predicate into `HasLeviathanSteeringVaultState()` and use it in `ScheduleLeviathanSteering()`. The scheduler now fails closed by returning the incoming dependency when buffers are absent. `EnsureInitialized()` remains the cognition owner cold setup hook that hydrates BufferIDs 72500..72509.

Rejected Alternatives: Lazy allocation inside the schedule path was rejected because it violates read/schedule purity and can create frame spikes. Moving ownership into a new manager was rejected because `PredatorCognitionDomain` already owns the cognition chain and Vault route.

Scalability potential: Low, middle, high, and ultra quality keep the same fixed BufferIDs and DTO layout. Quality still only changes whisker count, whisker length, and optional debug richness.

Hardware Impact: Removes a worst-case cold allocation and CSV read from the simulation scheduling path on i3/MX350-class CPUs. No fake steady-state microsecond number is claimed.

## Decision 017 - Read Race and Hash Authority Fence

Problem: Secondary static review found that `FaunaBrain` could read SHINOBU_303 kinematic output while the steering writer job was still in flight. The same read facades used `TryResolveHandle`, which can mutate Vault generation-fault telemetry. CSV profile keys also lived in a separate lowercase ASCII FNV namespace, while producer species IDs are numeric or masked `LocHash.Compute` values.

Solution: Add an in-flight predicate over `_evaluationScheduled & _steeringEvaluationJobScheduled` and fail SHINOBU_303 read/write/profile/gizmo facades while steering writes may still be scheduled. Add `VaultArray<T>.OpenRead()` over `IDataVault.TryReadHandle` and route SHINOBU_303 read facades through it. Parse numeric CSV species IDs directly; otherwise hash ASCII bytes as masked LocHash-compatible UTF-16 code units. Non-finite `GlobalQualityWeight` now falls to `0f`.

Rejected Alternatives: Completing the scheduled job from `FaunaBrain` was rejected because it would block the main thread. Adding a double-buffer ABI was rejected inside this pass because it changes BufferIDs/layout and needs cross-domain integration. Keeping lowercase FNV was rejected because it creates two species-key authorities.

Scalability potential: Low devices fail cheap and fall back one frame instead of blocking. Middle/high/ultra continue using the same buffers and continuous whisker curve once the completed frame is published.

Hardware Impact: Avoids a potential data race and prevents corrupt quality from forcing 26 whiskers. On weak CPUs, the worst-case bad-signal saving is up to 20 SDF samples per active leviathan frame.

## Decision 018 - Fault Dump Route Tightening

Problem: The SHINOBU_303 blackbox dump path still opened Vault buffers through the mutable route and did extra temp/delete/replace filesystem calls while handling a fault.

Solution: Open telemetry and cursor with `OpenRead()` / `TryReadHandle`, write the mandated `Docs/AgentLogs/Dump_SHINOBU_303.bin` directly with a stackalloc 24-byte little-endian header plus raw `ReadOnlySpan<byte>` telemetry payload, flush it with `Flush(true)`, and publish `SteeringDumpFaultHash` to `GlobalTelemetryBus` before the local dump.

Rejected Alternatives: Moving the full 300-frame SHINOBU_303 dump into the central SHINOBU_33 blackbox was rejected because that source-slot API captures 64-byte payloads, not a domain-specific 300-row ring. Removing the task-specific file was rejected because Task 15 explicitly requires `Dump_SHINOBU_303.bin`.

Scalability potential: Low through ultra quality share the same 300-frame Vault ring and fault route. Quality changes SDF sample count only; it does not change dump layout or proof ownership.

Hardware Impact: No hot-path savings are claimed. The rare fault path performs fewer filesystem operations and no mutable Vault read, reducing crash-report side effects on low-end storage.
