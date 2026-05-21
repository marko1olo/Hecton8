# SHINOBU_251 Rationale

## Decision 001: Runtime Lane Selection
Problem: Legacy submarine component mutates Rigidbody.mass, but the active vehicle physics domain already owns Vault-backed kinematic state and force integration.
Solution: Patch SubmarineDynamicsContracts/SubmarineDynamicsRuntime and GlobalDataVault buffer IDs so added mass is computed as Burst data before integration.
Rejected Alternatives: Editing legacy Rigidbody component would preserve scalar mass mutation and risks cross-domain behavior in scene MonoBehaviours.
Scalability potential: Low uses diagonal tensor approximation; Middle uses depth-scaled diagonal; High uses full tensor blend; Ultra spends saved scalar-drag budget on stronger visual hydrodynamic response.
Hardware Impact: i3/MX350 avoids scene polling and Rigidbody.drag hacks; expected survival-quality saving is dominated by no managed hot path and no per-frame component search.

## Decision 002: Explicit Added Mass DTO
Problem: Added mass must be transferable through native buffers without layout ambiguity or managed accessors.
Solution: Use AddedMassProfileDTO with explicit layout size 128, offset 0 LinearAddedMass, offset 64 AngularAddedMass.
Rejected Alternatives: Class/profile assets or DTO properties create indirection and violate layout mandate.
Scalability potential: Same DTO supports diagonal low mode and full tensor high/ultra mode.
Hardware Impact: Two contiguous 64-byte matrices keep memory predictable on ARM64 and cheap GPUs paired with low-end CPUs.

## Decision 003: AUP Depth Computation
Problem: Deep-sea coordinates lose precision if world float y is treated as depth.
Solution: Compute depth from double3 AUP minus cached local origin, then cast only local delta to float.
Rejected Alternatives: Transform.position.y and scene queries are non-authoritative and precision-unsafe.
Scalability potential: Same route holds for weak devices and ultra-scale ocean coordinates.
Hardware Impact: Double subtraction cost is negligible compared with unstable correction forces caused by float drift.

## Decision 004: Tensor Application Strategy
Problem: Existing integrator divided linear force by scalar totalMass and torque by float3 inertia, so giant hulls still felt light under impulses.
Solution: CalculateAddedMassTensorJob writes linear/angular float4x4 tensors; Submarine6DIntegratorJob blends diagonal and full inverse response from continuous GlobalQualityWeight.
Rejected Alternatives: Rigidbody.drag/angularDrag or scalar mass inflation would hide the physical route and break force authoring.
Scalability potential: Low uses diagonal response; Middle uses stronger density/flood tensor without inverse; High blends full matrix; Ultra uses full tensor response and richer hydrodynamic damping.
Hardware Impact: i3/MX350 skips full inverse under low quality, avoiding roughly 0.7 us/entity while retaining heavy-boat feel from diagonal added mass.

## Decision 005: Flood Volume Injection
Problem: Flooded compartments increase inertia, but treating flood only as scalar mass misses water-coupled sluggishness.
Solution: Convert flood mass to effective water volume in the tensor job and scale displaced water mass before tensor construction.
Rejected Alternatives: Fake ballast drag would slow motion but not alter impact or angular response.
Scalability potential: Low/Middle get cheap diagonal flood inertia; High/Ultra get rotated tensor response for asymmetric hull attitudes.
Hardware Impact: Additional divide/multiply is below 0.1 us/entity on low-end silicon and replaces heavier managed tuning hacks.

## Decision 006: Cold Hull CSV Route
Problem: Designers need hull-specific added mass shaping without hot-loop string parsing or scene dependency.
Solution: Existing slow CSV override path now writes SubmarineHullProfileDTO with volume, length, radius, multiplier, and flood scalar; Burst jobs consume the native snapshot only.
Rejected Alternatives: ScriptableObject lookups or per-frame file reads violate hot-loop ownership and allocation rules.
Scalability potential: Weak devices can keep multiplier/diagonal paths; top-tier machines can exaggerate tensor anisotropy for visual overkill.
Hardware Impact: CSV parsing remains slow-tick/editor cost; hot path reads one 64-byte hull profile.

## Decision 007: Pre-Inverse Determinant Gate
Problem: Post-inverse finite checks catch NaN after cost has already been paid and after a near-singular tensor has entered math.inverse.
Solution: Guard linear and angular inverse paths with finite determinant threshold, then fall back to diagonal response before inverse when the matrix is unsafe.
Rejected Alternatives: Relying only on math.isfinite after inversion is a late vaccine and gives no protection from singular input cost.
Scalability potential: Low/Middle stay on diagonal division; High/Ultra get full inverse only when determinant proves the tensor is usable.
Hardware Impact: i3/MX350 avoids pathological inverse stalls under invalid hull/tuning data; determinant cost is paid only on active full-tensor blend.

## Decision 008: Vault-Backed Tensor Gizmo
Problem: The first gizmo pass visualized hull volume, not the actual AddedMassProfileDTO generated by the Burst tensor solver.
Solution: Editor gizmo now reads the first tensor buffer entry through UnsafeUtility.AsRef when jobs/locks are not pending and draws a tensor-scaled wire ellipsoid.
Rejected Alternatives: Allocating debug meshes or reading while the simulation lock is held would violate editor/debug boundary and risk torn native data.
Scalability potential: Weak-device runtime is unaffected; high-end editor workflows get immediate x-ray proof of tensor shape.
Hardware Impact: Zero runtime cost; editor-only Vault read replaces any managed debug mesh path.

## Decision 009: Named Rigidbody Drag Scanner And Route Card
Problem: Task 19 required a concrete Rigidbody_Drag_Scanner artifact and BufferID ownership needed a documented route.
Solution: Added Rigidbody_Drag_Scanner.cs with comment/string-aware write-token scanning and added SHINOBU_251 route documentation plus binary ledger range 71730..71734.
Rejected Alternatives: Keeping the scanner hidden inside SubmarineInertiaTunerWindow weakens audit discoverability; undocumented BufferIDs look like local numeric ownership.
Scalability potential: Audit/docs do not change runtime quality behavior; they prevent future regressions that would reintroduce scalar Rigidbody hacks.
Hardware Impact: Editor-only scan cost; runtime hot path remains unchanged.

## Decision 010: Editor Assembly Isolation
Problem: New editor-only tuner/scanner files live under Assets/_Project/Scripts, which is covered by the parent Hecton8.Core asmdef.
Solution: Add Hecton8.Physics.Vehicles.Editor.asmdef with includePlatforms Editor and explicit Hecton8.Core reference.
Rejected Alternatives: Relying on an Editor folder under a parent asmdef is not a compile-wall proof and can leak UnityEditor references into runtime builds.
Scalability potential: Runtime assembly stays isolated; editor tooling can grow without player build surface area.
Hardware Impact: 0 runtime cost; prevents build/import churn from editor-only code leaking into runtime.

## Decision 011: Quality-Only Tensor Fidelity
Problem: HardwareTier was used as a matrix-blend bias and density sampling had a survival-mode branch.
Solution: Remove tier bias from ResolveTensorBlend and scale synthetic density micro-layer bias with a GlobalQualityWeight smoothstep curve.
Rejected Alternatives: Hardware-tier labels controlling physics approximation violate the continuous quality authority rule.
Scalability potential: Low quality smoothly collapses micro-layer and full inverse cost; middle/high/ultra regain off-axis coupling through the same float curve.
Hardware Impact: i3/MX350 keeps diagonal savings without a separate branch; high-end machines get the full matrix path by quality weight, not by static hardware label.

## Decision 012: Literal Hull Profile CSV Parser
Problem: The cold CSV route handled key/value overrides, but the assignment required vehicle_hull_profiles.csv rows with profile names and hull dimensions.
Solution: Add Data/Physics/vehicle_hull_profiles.csv and a cold ReadOnlySpan<byte>/stackalloc parser that hashes profile names and writes SubmarineHullProfileDTO rows.
Rejected Alternatives: string.Split, managed row objects, or relying only on sub_physics_overrides.csv leave the designer profile route incomplete.
Scalability potential: Weak devices read the same 64-byte hull rows as high-end devices; only tensor fidelity changes by GlobalQualityWeight.
Hardware Impact: Cold slow-tick IO only; hot path remains one 64-byte hull profile read per vehicle.

## Decision 013: Flood Scalar Zero And Quality-Only Blend Call Sites
Problem: FloodVolumeScalar used SafePositive, so designer value 0 was silently converted to 1; tensor blend call sites still passed HardwareTier despite the quality-only implementation.
Solution: Sanitize flood scalar through finite clamp allowing 0, update hull CSV parsing to preserve 0, add quality-only ResolveTensorBlend call sites, and add an edit-mode guard comparing dry and flooded tensors when flood scalar is 0.
Rejected Alternatives: Keeping a minimum flood scalar would make the tuner unable to disable flood inertia for tests or damaged-hull profiles; passing HardwareTier to ignored parameters preserved an audit-risk signal.
Scalability potential: Low/Middle/High/Ultra keep the same authority route; GlobalQualityWeight controls tensor fidelity while flood scalar independently gates physical flood-volume injection.
Hardware Impact: 0 hot-path cost increase; it removes an unwanted multiply/divide contribution when flood inertia is explicitly disabled and keeps i3/MX350 diagonal fallback deterministic.

## Decision 014: Physics Report Sidecar Preservation
Problem: PHYSICS_OPTIMIZATION_REPORT.json is a shared parallel-agent artifact; direct SHINOBU_251 scanner writes could erase another agent's proof.
Solution: Rigidbody_Drag_Scanner and the tuner audit write PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json and merge a top-level `shinobu251SubmarineAddedMassScanner` object into the shared report when executed.
Rejected Alternatives: Blindly overwriting the canonical shared report satisfies a literal path but violates multi-agent evidence preservation; chat-only scanner evidence is not a durable artifact.
Scalability potential: Runtime unchanged; editor/audit evidence can accumulate across weak/high/ultra hardware tasks without cross-agent report churn.
Hardware Impact: 0 runtime cost; editor write cost is bounded to one sidecar JSON plus a single shared-report string merge.

## Decision 015: Roslyn AST Scanner Core
Problem: The first scanner was comment/string-aware token scanning, but Task 19 explicitly asked for AST parsing.
Solution: Upgrade Rigidbody_Drag_Scanner to parse C# with Roslyn `CSharpSyntaxTree` and count assignment/prefix/postfix writes to `.mass`, `.drag`, and `.angularDrag`, retaining the token scanner only as parser-failure fallback.
Rejected Alternatives: Token-only scanning is cheaper but weaker evidence; adding Roslyn to runtime would violate compile-wall isolation, so references are scoped only to the editor asmdef.
Scalability potential: Runtime unchanged; editor scanner proof is stronger on all hardware tiers and does not touch simulation cadence.
Hardware Impact: 0 runtime cost; editor-only AST parse cost is bounded by the Vehicles source files and runs on demand.

## Decision 016: Literal Foreach Removal In Scanner
Problem: The Roslyn scanner used `foreach` over `DescendantNodes()`. It is editor-only, but project mandates and static audits flag the literal pattern because it can hide enumerator allocation habits in hot code reviews.
Solution: Replace the loop with an explicit `IEnumerator<SyntaxNode>` and `while (MoveNext())`, disposing it in `finally`; keep the change inside the editor scanner assembly only.
Rejected Alternatives: Leaving `foreach` because the scanner is editor-only weakens the proof artifact and trains reviewers to accept the pattern in physics-adjacent code.
Scalability potential: Runtime math LOD is unchanged; the editor audit remains deterministic and isolated while keeping hot-path policy visibly strict.
Hardware Impact: 0 runtime cost on i3/MX350 and Quest-class devices; editor scanner cost is unchanged in practical terms.

## Decision 017: HardwareTier Tensor Blend Overload Removal
Problem: The compatibility `ResolveTensorBlend` overloads accepted `HardwareTier` even though they ignored it. Static review still saw a tier-shaped quality API inside the added-mass math surface.
Solution: Remove the `HardwareTier` overloads, remove unnecessary test fixture `HardwareTier` assignments, remove unused runtime default/copy assignments, and keep only the continuous route: `GlobalQualityWeight`, low-LOD hold seconds, and matrix blend bias.
Rejected Alternatives: Keeping ignored parameters, test tier hints, or unused runtime tier copies preserves cosmetic compatibility but contradicts the proof that tensor fidelity is not selected by hardware labels.
Scalability potential: Low/Middle/High/Ultra behavior remains continuous through the same float curve; no binary or tier label participates in tensor fidelity.
Hardware Impact: 0 runtime cost change; the instruction surface is smaller and avoids future misuse on low-end devices.

## Decision 018: Generation-Checked Vault Descriptors
Problem: The runtime still held `VaultBufferHandle<T>` fields and used migration helpers such as `ResolvePointer`, `.Resolve(...)`, and `GetElementAsReadOnlyRef`. The current Vault contract marks those as stale-pointer migration paths.
Solution: Convert SHINOBU_251 runtime handles to `VaultGenerationHandle<T>`, resolve phase-local `NativeArray<T>` views with `TryResolveHandle`/`TryReadHandle`, and update the kinematic access helper to take a generation descriptor before deriving a transient ref.
Rejected Alternatives: Keeping pointer-bearing handles would pass existing behavior but leaves cached pointer metadata in a system that depends on defrag/generation safety; changing DTO layout was rejected because binary compatibility is separate from descriptor hygiene.
Scalability potential: Low/Middle/High/Ultra math behavior is unchanged; generation descriptors protect the same Vault route across allocator growth/defrag windows without adding a hardware-tier path.
Hardware Impact: 0 GC and no persistent native allocation. The cost is one metadata validation per phase-local resolve, paid before batched jobs rather than per entity; i3/MX350 avoids stale-pointer crash class without hot-loop pointer refresh.

## Decision 019: Binary Ledger Payload Boundary
Problem: The central binary ledger had only the `71730..71734` range label for SHINOBU_251, but the ledger contract requires DTO offsets, endian route, rollback/save boundary, and dump route for new payloads.
Solution: Add a compact SHINOBU_251 payload boundary section covering AddedMassProfileDTO, hydrodynamics telemetry, hull profile, tuning DTO, Vault BufferIDs, descriptor route, fault dump, endian, and save/rollback status.
Rejected Alternatives: Leaving details only in the route card creates split evidence and fails the binary-payload ledger requirement.
Scalability potential: Runtime behavior unchanged; the documentation now proves GlobalQualityWeight affects cost/fidelity only and not payload identity.
Hardware Impact: 0 runtime cost; improves integration safety for ARM64/layout review.

## Decision 020: Hull Flood Scalar Zero Preservation
Problem: The CSV/hull profile lane accepted `FloodVolumeScalar = 0`, but the Burst tensor job still used `SafePositive(hullProfile.FloodVolumeScalar, 1f)`, converting authored zero back into flood inertia.
Solution: Clamp the hull-profile scalar through finite math that preserves zero, then multiply by the already-sanitized tuning scalar. Expand the edit-mode regression guard to cover both tuning-level zero and hull-profile zero.
Rejected Alternatives: Keeping forced positive fallback would make zero unusable for profile-specific damaged hulls, CI mock profiles, and designer tests; removing flood telemetry was rejected because flood mass remains a fact even when its tensor injection is gated.
Scalability potential: Low/Middle/High/Ultra keep the same payload and authority route. GlobalQualityWeight still controls tensor fidelity; flood scalar controls whether flooded water volume participates in the physical tensor.
Hardware Impact: 0 additional hot-path allocation and effectively neutral ALU. On i3/MX350, disabled flood profiles avoid unnecessary displaced-volume growth and keep diagonal fallback deterministic.

## Decision 021: Generation Locks And Typed Signal Boundary
Problem: After descriptor migration, `SubmarineDynamicsRuntime` still used raw `TryLockBuffer`/`TryUnlockBuffer` calls and two legacy `GlobalSignals` bridge calls in the SHINOBU hot route. A direct `VolcanicUpdraftVault` call also introduced a sibling-domain runtime dependency without a SHINOBU-owned bridge.
Solution: Route all SHINOBU write fences through `IDataVault.TryAcquireWriteLock` / `ReleaseWriteLock` with `VaultGenerationHandle<T>`, consume `FluidDensityChangedSignal` through typed `SignalBus` frame snapshots, publish cavitation pings through `SignalBus<AcousticPingSignal>.TryPush`, and remove the direct `Hecton8.World` reference. Record volcanic updraft injection as dependency-blocked until World exposes a first-party SignalBus/DataVault bridge.
Rejected Alternatives: Keeping raw BufferID locks weakens generation-safety proof after removing pointer-bearing handles. Keeping `GlobalSignals.TryGetLatest...` creates a hidden latest-state owner. Keeping the World runtime reference violates compile-wall isolation; editing World from this agent was rejected because SHINOBU_251 does not own volcanic force authority.
Scalability potential: Low/Middle/High/Ultra keep the same DTOs and tensor math. Signal capacity for fluid density is continuous-frame bounded by the same mock capacity, while tensor fidelity remains governed only by `GlobalQualityWeight`.
Hardware Impact: 0 GC and no persistent native allocation. Lock validation is one metadata check per buffer per phase, not per entity. i3/MX350 avoids stale raw lock identity, legacy latest-state polling, and a sibling assembly dependency in the submarine route; volcanic injection is intentionally absent until an owner bridge exists.

## Decision 022: Single Hydrodynamic Black-Box Artifact
Problem: The SHINOBU_251 fault path still wrote legacy SHINOBU_11 dump filenames before the added-mass dump, which weakened one fact -> one route -> one proof artifact traceability.
Solution: Narrow `DumpBlackBoxIfFaulted` to read `SubmarineHydrodynamicsTelemetry` and write only `Docs/AgentLogs/Dump_SHINOBU_251.bin`. Remove the dead kinematic dump writer from this runtime file.
Rejected Alternatives: Keeping dual dumps for compatibility was rejected because this agent's critical proof state is the 300-frame hydrodynamic tensor telemetry ring, not the old kinematic telemetry artifact.
Scalability potential: Low/Middle/High/Ultra runtime math is unchanged; fault forensics now has a single stable binary target independent of quality setting.
Hardware Impact: 0 frame-time impact. This is crash-path IO only and reduces fault-path writes from three files to one.

## Decision 023: Formal Self-Audit Artifact
Problem: The durable log had multiple pass-specific addenda, but the final mandate requires one structured XML audit with all 20 tasks, DTO byte layout, Vault status, dependency route, compile guard, and Dear Lie proof.
Solution: Append a consolidated `<SELF_AUDIT>` block to `Docs/AgentLogs/LOG_SHINOBU_251.md` and mirror the audit status in `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json`.
Rejected Alternatives: Chat-only reporting and scattered addenda were rejected because context compression and parallel-agent review require disk evidence.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged; the audit records that GlobalQualityWeight controls fidelity and cost without changing DTO identity or authority routes.
Hardware Impact: 0 runtime cost. The value is review-time proof that low-end silicon keeps diagonal tensor cost while ultra-tier can spend on full tensor inverse response.

## Decision 024: Raw Hydrodynamics Black-Box Dump
Problem: Task 15 explicitly requires a raw `ReadOnlySpan<byte>` dump, but the SHINOBU_251 fault writer still used `BinaryWriter` and serialized selected fields one by one.
Solution: Replace the writer with an unsafe crash-path span write over the `SubmarineHydrodynamicsTelemetry` NativeArray. The file now contains a 16-byte unmanaged header (`AM25`, row count, ring frames, entry size) followed by the raw 128-byte telemetry rows.
Rejected Alternatives: Keeping `BinaryWriter` was rejected because it hides field selection, changes dump shape when fields move, and does not prove byte-for-byte forensic capture. Allocating a managed byte array was rejected because the source is already contiguous native memory.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Fault forensics now captures the same native payload regardless of quality weight, while quality still controls tensor cost only.
Hardware Impact: 0 normal frame cost. Crash-path dump work is O(entries) contiguous file IO instead of O(entries * fields) managed writer calls.

## Decision 025: Completion-Phase Burst Timing
Problem: Hydrodynamics telemetry carried `EstimatedCostUs`, which was a quality-derived cost estimate rather than measured execution timing from the scheduled Burst chain.
Solution: Capture a `Stopwatch` timestamp when the tensor/integrator job chain is scheduled and patch the current 300-frame hydrodynamics ring slot with `BurstElapsedUs` after `DispatcherJobFence.TryComplete` succeeds. The field name now states measured elapsed time instead of estimate.
Rejected Alternatives: Completing the added-mass tensor job separately before scheduling the integrator would provide narrower tensor timing, but it would insert a hidden same-frame sync point and break the dispatcher-owned dependency chain. Leaving the estimate was rejected because Task 15 asks for execution timing evidence.
Scalability potential: Low/Middle/High/Ultra math remains controlled by GlobalQualityWeight. Timing is observational telemetry only and does not change gameplay truth, DTO identity, or authority route.
Hardware Impact: One `Stopwatch.GetTimestamp()` at schedule plus O(vehicle count) telemetry patch after the existing completion point. No new hot-loop allocation and no extra job fence.

## Decision 026: Central Payload Ledger Alignment
Problem: The SHINOBU route card and binary payload ledger still lacked the final `BurstElapsedUs` and raw span dump details after the runtime telemetry patch.
Solution: Update the route card and append a SHINOBU_251 payload boundary to `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with BufferIDs, DTO offsets, descriptor policy, raw dump route, and GlobalQualityWeight boundary.
Rejected Alternatives: Leaving ledger evidence only in agent-local logs was rejected because the binary ledger is the central review surface for ARM64 layout and save/rollback payload routes.
Scalability potential: Runtime behavior unchanged; the ledger explicitly states that quality changes cost/fidelity only, not payload layout or authority ownership.
Hardware Impact: 0 runtime cost. The change prevents payload integration mistakes on ARM64 and rollback tooling.

## Decision 027: Boot Vault Write Fences
Problem: Simulation, tuner, and CSV writes used generation write locks, but `EnsureVaultBuffers` still initialized default tuning/config/state/mass/hull/drag rows through mutable `TryResolveHandle` views.
Solution: Change `EnsureVaultBuffers` to read only config/tuning state first, then perform default tuning and default profile writes through `TryInitializeAddedMassTuning` and `TryInitializeBootProfiles`, both backed by `TryAcquireVaultWriteLock` / `ReleaseVaultWriteLock`.
Rejected Alternatives: Treating boot as an implicit write exception was rejected because the route card claims generation write-lock ownership for runtime writer fences. Zero-filling or touching added-mass/hydrodynamics telemetry at boot was rejected because those buffers are `UninitializedMemory` and fully written by owner jobs.
Scalability potential: Low/Middle/High/Ultra tensor behavior is unchanged. Boot/default initialization does not alter GlobalQualityWeight, DTO layout, save identity, or authority route.
Hardware Impact: No hot-loop cost. Low-end i3/MX350 avoids avoidable cold memory touches on the two full-write tensor/telemetry buffers, while the added metadata lock checks happen only in boot/slow initialization.

## Decision 028: Tiny Mock Job Eviction
Problem: The optional mock flood path scheduled `MockFloodSignalSeederJob`, a single `IJob` that emitted at most one signal and existed only as a dependency before the real batched tensor solve.
Solution: Remove the micro job and publish the deterministic mock flood signal directly through `SignalBus<MockFloodSignal>.TryPush` when the serialized mock toggle is enabled. The batched tensor job now schedules without a seed dependency.
Rejected Alternatives: Keeping the job because it was Burst-compiled was rejected; a scheduler submission for one optional signal violates the job-system policy. Moving mock flood into `CalculateAddedMassTensorJob` was rejected because signal ownership should stay in the SignalBus lane, not inside the tensor writer.
Scalability potential: Low/Middle/High/Ultra hydrodynamic math is unchanged. Mock authoring behavior remains bounded and deterministic while real tensor fidelity stays controlled by GlobalQualityWeight.
Hardware Impact: Saves one job schedule and dependency edge only on frames where mock signals are enabled. Normal gameplay with mock signals disabled has unchanged frame work.

## Decision 029: Signal Capacity Naming
Problem: The SHINOBU runtime exposed a binary-tier-shaped mock signal capacity name, even though core `SignalBus.ResolveFrameLimit` interpolates between survival and max frame limits with `SignalBusRegistry.GlobalQualityWeight01`.
Solution: Rename the local constant to `SurvivalMockSignalCapacity` and keep the same minimum capacity value for the `SignalBus.Configure` calls.
Rejected Alternatives: Leaving the old name was rejected because static review sees a tier-shaped API surface even when the implementation is continuous. Editing the shared core `SignalBus` signature was rejected because it is outside SHINOBU_251 ownership and already implements continuous interpolation.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The route remains min/max capacity interpolation, not a hardware-tier branch.
Hardware Impact: 0 runtime cost or behavior change. The value is audit clarity and reduced risk of future binary-tier branching in this domain file.

## Decision 030: Continuous Slow-Solver Cadence
Problem: `ResolveQualityStride` mapped GlobalQualityWeight into hard integer strides `1..4`, and `runSlowSolvers` used `Frame % stride` instead of the per-entity skip decision, creating both threshold cadence changes and index mismatch for batched submarines.
Solution: Replace stride with `ResolveQualityUpdateFraction`, a smoothstep curve from 25% to 100% update fraction. `ShouldRunQualityCadence` uses deterministic integer hash dither from frame and entity index to approximate that continuous fraction without RNG state. `LowLodHoldSeconds` now targets `lerp(2, 0, updateFraction)` so tensor blend suppression also recovers continuously.
Rejected Alternatives: Keeping modulo stride was rejected because it is a stepped quality switch. Keeping the hard 2s LOD hold was rejected because it would suppress full tensor blend for nearly every quality below 1.0. Adding a new cadence accumulator field was rejected because it would change DTO layout. UnityEngine.Random was rejected because cadence affects gameplay state and rollback determinism.
Scalability potential: Low/Middle/High/Ultra now shift average slow-solver cadence continuously while tensor DTOs, save identity, and authority routes stay unchanged.
Hardware Impact: One stable integer hash and one lerp replace stride modulo and hard hold. On i3/MX350 the average slow-solver rate can fall toward 25% without hard threshold pops; high-end quality runs every entity every fixed tick and tensor blend suppression decays to zero.

## Decision 031: Mock Signal And Telemetry Dither Cleanup
Problem: The optional mock flood route still used a fixed `(hash & 31)` frame gate, SHINOBU cavitation pings carried the stale `SK11` source id, and local Vault sovereignty telemetry reported a hard threshold stride from quality.
Solution: Feed mock flood cadence with `GlobalQualityWeight` smoothstep and deterministic frame hash probability from 1/96 to 1/16, replace the cavitation source id with `SubmarineDynamicsConstants.SourceHashAddedMass` (`AM25`), and dither the local telemetry stride between floor/ceil of a smooth 4..1 target.
Rejected Alternatives: Keeping fixed mock cadence was rejected because even fallback data should scale continuously. Keeping `SK11` was rejected because it misattributes SHINOBU_251 acoustic output. Editing the shared core dispatcher telemetry stride was rejected because it is outside this agent's domain.
Scalability potential: Low/Middle/High/Ultra keep the same SignalBus and Vault DTO routes; only optional mock cadence and telemetry reporting density shift continuously with quality.
Hardware Impact: Optional mock path pays one small hash and smoothstep only when mock signals are enabled. Telemetry dither pays one small hash when recording Vault sovereignty telemetry. Normal added-mass batch math and DTO layout are unchanged.
