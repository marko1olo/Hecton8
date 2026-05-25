# Rationale_SHINOBU_250

Status: PENDING VERIFICATION

## Initial Route

Problem: KCC currently needs environmental truth injected without physics triggers, hot registry polling, or managed wrappers.
Solution: Owner-local Burst kernels over unmanaged DTO buffers. Apply current advection, metabolic penalty, mud friction, hydrodynamic drag, and slope sliding as velocity/delta transforms around KCC capsule cast phases.
Rejected Alternatives: `OnTriggerStay`, `Rigidbody.AddForce`, `CharacterController.slopeLimit`, and per-frame scene queries are rejected because they create PhysX broadphase cost, managed callback risk, nondeterministic ordering, and conflict with deferred capsule cast batching.
Scalability potential: Low uses nearest-flow dominance and lower sampling blend; Middle blends flow cheaply; High uses trilinear dominance; Ultra preserves expensive visual/presentation overkill while gameplay truth remains deterministic.
Hardware Impact: Expected low-end i3/MX350 gain is removal of trigger/Rigidbody paths and bounded Burst array traversal; exact microseconds are pending static implementation and profiling.

## Mandate Selection

Problem: Assignment spans physics, flow field, AUP, native memory, layout, telemetry, and zero-GC hot paths.
Solution: Read eight mandates before code: physics integrity, abyssal flow fields, ARM64 runtime layout, AUP determinism, floating-origin precision, zero-GC, native memory/jobs, and post-mortem telemetry.
Rejected Alternatives: Reading only the prompt is rejected because the task touches existing project-wide authority boundaries and DataVault/job discipline.
Scalability potential: Mandates define continuous quality weight, flow sampling load-shed, AUP probe cadence, and blackbox telemetry for Low/Middle/High/Ultra paths.
Hardware Impact: Prevents adding unmanaged allocation/growth, hidden `.Complete()`, and trigger-based force routing that would spike low-end frame time.

## Loop 1 Decisions - Tasks 01-05

Problem: First-party gameplay still had one movement-affecting trigger-stay loop in `SargassumPhysicsZone`, while KCC environmental movement needed one mathematical authority.
Solution: Remove `OnTriggerStay` and leave enter/exit contact state only; move environmental drag/advection/slope authority into KCC Burst jobs.
Rejected Alternatives: Keeping `StayZone` as a hot trigger refresh was rejected because it preserves PhysX callback cadence as movement authority. Removing `PhysicsApplySystem.AddForce` was rejected because it is the centralized force packet owner, not a current trigger.
Scalability potential: Low/Middle avoid callback storms in dense sargassum; High/Ultra spend the saved CPU on trilinear flow and richer visual feedback instead of managed overlap churn.
Hardware Impact: Estimated 12-35 us saved per overlapping trigger contact on i3/MX350 class CPU, with larger wins under contact churn.

Problem: KCC needed deterministic environmental inputs before capsule cast without depending on absent flow/SDF owners.
Solution: Allocate KCC-owned DataVault staging buffers and fill them via `GenerateMockEnvironmentalForcesJob` using deterministic math over a 16x8x16 grid.
Rejected Alternatives: Scene lookup of `CurrentVolume`, `TrySampleAbyssalFlow`, or voxel component APIs was rejected because those are managed, owner-foreign, and not guaranteed to exist during KCC scheduling.
Scalability potential: Low uses cheap nearest-dominant sampling; Middle blends; High/Ultra use trilinear dominance and stronger visual overkill while truth remains bounded.
Hardware Impact: Estimated 40-120 us saved versus managed authored-current traversal and voxel sampling on weak CPU.

Problem: Environmental profile data must be ABI-stable on ARM64 and not vulnerable to CS1612 struct copy errors.
Solution: Added explicit 32-byte `KccEnvironmentProfileDTO` with raw fields/padding and layout validation through `UnsafeUtility.SizeOf`, `AlignOf`, and offsets.
Rejected Alternatives: Auto-layout structs, properties, or `bool` fields were rejected because they destabilize ABI and copy semantics.
Scalability potential: Same DTO is valid for Low/Middle/High/Ultra; quality changes sampling cadence/fidelity, not layout or authority.
Hardware Impact: Runtime cost 0 us; prevents layout fault and NativeArray stride mismatch on ARM64.

## Loop 2 Decisions - Tasks 06-15

Problem: Ocean current must move the KCC before capsule cast without trigger volumes or Rigidbody forces.
Solution: `ApplyEnvironmentalForcesJob` samples a Vault-backed 3D `float3` flow grid and adds advection into proposed velocity before `BuildCapsuleCastCommandsJob`.
Rejected Alternatives: `CurrentVolume.SampleAt`, `TrySampleAbyssalFlow`, and `Rigidbody.AddForce` were rejected because they are managed or PhysX-owned routes outside KCC schedule control.
Scalability potential: Low uses nearest-dominant results; Middle/High/Ultra continuously increase trilinear contribution through `GlobalQualityWeight`.
Hardware Impact: Estimated 40-120 us saved on i3/MX350 versus managed current traversal in dense authored fields.

Problem: Steep slopes need deterministic wall sliding after collision data exists but before final KCC resolution.
Solution: `EvaluateSlopeFrictionJob` reads extracted capsule hits, normalizes hit normals with `math.normalizesafe`, computes angle via `acos(dot(normal, up))`, and projects gravity along the slope face when over profile limit.
Rejected Alternatives: `CharacterController.slopeLimit`, downward `Physics.Raycast`, and multi-contact physical friction solves were rejected as managed/expensive or non-owner authority.
Scalability potential: Low uses the same math with low iteration counts from existing KCC quality; High/Ultra preserve more hit iterations and stronger slide presentation.
Hardware Impact: Estimated 18-45 us saved versus probe-based slide logic on weak CPU.

Problem: Player exhaustion, toxicity, and dehydration must punish motion without binary gameplay switches.
Solution: Read `MetabolicStateDTO` fields from KCC-owned staging, compute continuous exhaustion, scale acceleration down, and add analytical drag.
Rejected Alternatives: Binary starving/dehydrated movement gates and animation-state penalties were rejected because they change authority and create discontinuities.
Scalability potential: Low/Middle/High/Ultra use the same continuous scalar; only sampling fidelity and visual response change.
Hardware Impact: Estimated 5-12 us saved versus managed survival callback route; no allocation.

Problem: Mud/silt should feel physically sticky without simulating contact grains or voxel collisions.
Solution: Sample SDF distance band and damp lateral velocity analytically.
Rejected Alternatives: Per-voxel contact patch, Rigidbody material friction, and high-frequency terrain raycasts were rejected as frame-time liabilities.
Scalability potential: Low uses coarse SDF band; High/Ultra can spend saved cycles on visual silt/propwash while KCC truth stays simple.
Hardware Impact: Estimated 60-300 us saved against naive contact/voxel solve on low-end silicon.

Problem: NaN/fault autopsy must not depend on chat memory.
Solution: Added `KccEnvironmentTelemetryEntry` 300-frame ring and dump writer to `Docs/AgentLogs/Dump_SHINOBU_250.bin` on fault mask.
Rejected Alternatives: Debug.Log-only or profiler-only evidence was rejected because it vanishes on crash and does not satisfy black-box doctrine.
Scalability potential: Ring size and DTO layout stay fixed across quality levels; compute cost is one aggregate job.
Hardware Impact: Runtime cost is bounded; postmortem saves repeated repro cycles.

## Loop 3 Decisions - Tasks 16-19

Problem: Designers need a cold tuning route for environmental KCC constants without in-game mutable UI.
Solution: Extend the existing Hydro KCC editor tuner to write `KccEnvironmentProfileDTO` into DataVault.
Rejected Alternatives: Runtime IMGUI, per-scene components, or ScriptableObject polling were rejected because they add hot path coupling or new ownership routes.
Scalability potential: Sliders define continuous constants used across Low/Middle/High/Ultra; visual overkill remains separate from movement truth.
Hardware Impact: Runtime 0 us; editor-only mutation.

Problem: Locomotion environment profiles need deterministic cold ingest.
Solution: Added `KccEnvironmentProfileCsvParser` using `ReadOnlySpan<byte>`, manual float parsing, and FNV-1a profile names, plus `locomotion_environment_profiles.csv`.
Rejected Alternatives: `string.Split`, LINQ, managed CSV libraries, and runtime file polling were rejected because they allocate and blur cold/hot boundaries.
Scalability potential: Suit/profile constants can scale flow/friction/exhaustion without changing DTO layout.
Hardware Impact: Runtime 0 us; cold boot parse only.

Problem: Force debugging must expose current and slope vectors without adding runtime UI.
Solution: Extend existing KCC gizmo to draw applied flow and slope-slide vectors from completed debug DTOs.
Rejected Alternatives: Per-frame in-game text or line renderer allocation was rejected as visual/debug pollution.
Scalability potential: Low devices pay nothing when gizmos are off; high-end editor can visualize richer vectors.
Hardware Impact: Runtime player build 0 us.

Problem: The assignment needs static proof of trigger/slope debt removal.
Solution: Added `Environment_Trigger_Scanner` and a SHINOBU_250 JSON proof report.
Rejected Alternatives: Manual grep claims were rejected. Full Roslyn AST was rejected for this pass because Unity editor static scanner pattern matches the mandated debt and is cheaper to maintain inside the existing editor tool pattern.
Scalability potential: Editor-only enforcement prevents regressions before runtime.
Hardware Impact: Runtime 0 us; editor scan cost only.

## Loop 4/5 Self-Audit

Problem: The compile gate briefly cleared, but Unity-generated `.csproj` files do not include the edited KCC runtime/editor files.
Solution: Refuse `dotnet build` as false proof and record compile verification as blocked by stale Unity project generation. Static checks still ran: brace balance, old job-name scan, OnTriggerStay purge scan, BufferID duplicate scan, JSON validation, CSV presence, and `git diff --check`.
Rejected Alternatives: Launching `dotnet build Hecton8.Core.csproj` was rejected because it only references `H8Memory.cs` and `SargassumPhysicsZone.cs`, not `HydrodynamicKccRuntime.cs`; it would report success while skipping the main Burst jobs.
Scalability potential: Verification route must be regenerated Unity project files or editor import; same Low/Middle/High/Ultra behavior remains in code.
Hardware Impact: Avoided wasting CPU on a non-authoritative compile; runtime impact 0 us.

## Loop 6 Hardening After Subagent Audit

Problem: Core KCC imported `Hecton8.Physiology`, which would require a Core -> Physiology asmdef edge while Physiology already references Core.
Solution: Move the shared `MetabolicStateDTO` and metabolism Vault constants into `Hecton8.Core.Contracts.Physiology`; KCC and Physiology both consume that neutral contract. KCC opens the published 70238 metabolism lane only if the descriptor exists, is long enough, and is not actively locked; otherwise it fails closed to KCC-owned mock metabolism lane 71764.
Rejected Alternatives: Adding `Hecton8.Physiology` to `Hecton8.Core.asmdef` was rejected because it creates an assembly cycle and compile-wall blast radius. Duplicating the DTO in KCC was rejected because `GlobalDataVault` type hashes include `typeof(T).TypeHandle`, so duplicate shapes are not the same Vault ABI.
Scalability potential: Low/Middle/High/Ultra share the same 32-byte metabolism DTO; quality changes flow sampling and visual response, not cross-domain ABI.
Hardware Impact: Runtime cost 0 us; prevents compile-wall failure and DataVault type mismatch faults.

Problem: Environment profile CSV bucket lookup trusted a single modulo bucket and could silently activate the wrong profile on FNV collision.
Solution: Add Vault lane `71770` for profile hashes, clear it during cold parse, linear-probe buckets, and verify the stored hash before applying a profile.
Rejected Alternatives: Widening `KccEnvironmentProfileDTO` was rejected because the XML mandates exact 32-byte offsets. Managed dictionaries were rejected because profile lookup must remain Vault-owned and cold deterministic.
Scalability potential: All hardware tiers keep the same profile layout; profile lookup cost is cold/editor only.
Hardware Impact: Runtime hot path 0 us; cold parse adds bounded integer probes only.

Problem: The tuner graph read kinematic speed instead of environmental force telemetry.
Solution: Switch the graph to `KccEnvironmentTelemetryEntry` and draw applied flow magnitude, slope angle, and exhaustion penalty from the environmental telemetry ring.
Rejected Alternatives: Runtime UI graph and managed debug overlays were rejected; editor-only UI Toolkit repaint reads completed Vault rows.
Scalability potential: Low devices pay 0 us in player builds; high-end editor can visualize richer environmental response.
Hardware Impact: Runtime player cost 0 us.

Problem: Static scanner originally claimed too much for a text search and overwrote shared physics reports without a dedicated SHINOBU_250 sidecar.
Solution: Scanner first recorded token-aware C# lexer mode, preserves previous canonical report, and writes `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_250.json`. Follow-up source audit found existing project Roslyn references under `Assets/Plugins/Roslyn`, so `Environment_Trigger_Scanner.cs` now imports `Microsoft.CodeAnalysis`, parses `CSharpSyntaxTree`, and falls back to token scan only on parser failure.
Rejected Alternatives: Claiming an AST report before Unity executes the upgraded scanner was rejected. Adding new package references was rejected because project-local Roslyn assemblies already exist and widening package dependencies would add compile-wall risk.
Scalability potential: Editor-only proof route; no runtime quality impact.
Hardware Impact: Runtime 0 us; editor scan cost only.

## Loop 7 Pre-Capsule Wall Slide Reconciliation

Problem: The XML requires `EvaluateSlopeFrictionJob` after capsule hits, but the latest user instruction explicitly requires currents, wall sliding, and metabolic exhaustion to meet inside one Burst node before capsule cast.
Solution: Extend `ApplyEnvironmentalForcesJob` to sample the SDF at the capsule foot, derive a finite SDF gradient normal by central differences, compute over-limit slope angle, remove into-wall velocity, and inject a pre-capsule slide vector into `ProposedVelocities` before `BuildCapsuleCastCommandsJob`. `EvaluateSlopeFrictionJob` remains as the post-cast hit-normal correction required by Task 07, and it now preserves the pre-capsule debug vector unless the real hit correction overrides it.
Rejected Alternatives: Moving all slope logic after the cast was rejected because it fails the user's unified pre-capsule node requirement. Replacing the SDF gradient with `Physics.Raycast(down)` was rejected because it reintroduces managed physics probes. Removing post-cast correction was rejected because SDF anticipation is coarse and Task 07 explicitly wants returned cast normals.
Scalability potential: Low/Middle/High/Ultra share the same gameplay route; `GlobalQualityWeight` continuously blends SDF-gradient normal fidelity and slide gain, while the post-cast correction preserves deterministic contact truth. Low hardware receives a cheaper flatter SDF anticipation plus exact hit correction; high hardware gets richer pre-contact trench slide feel and stronger debug/visual vectors.
Hardware Impact: Expected cost is six bounded SDF scalar reads plus vector algebra per controlled KCC entity, replacing any raycast/component slope probe. Estimate remains analytical until Burst profiler proof; expected low-end gain against raycast slide probes stays 18-45 us per entity.

## Loop 8 Subagent Audit Hardening

Problem: Read-only audits found that the KCC editor scanner would destructively overwrite the shared canonical physics report and that KCC editor code was still compiled through the broad `Hecton8.Core` assembly until Unity regenerates project/Bee files.
Solution: Added `Hecton8.Physics.KCC.Editor.asmdef` with explicit `Hecton8.Core`, `Hecton8.Core.Memory`, `Unity.Mathematics`, `Unity.Collections`, and Roslyn precompiled references. `Environment_Trigger_Scanner` now writes the full SHINOBU_250 sidecar and merges only `shinobu250KccEnvironmentScanner` into the shared canonical JSON. It also falls back to token scan when Roslyn returns syntax error diagnostics instead of relying only on exceptions.
Rejected Alternatives: Overwriting `PHYSICS_OPTIMIZATION_REPORT.json` was rejected because parallel agents already use the canonical file. Leaving the scanner in `Hecton8.Core` was rejected because an editor-only Roslyn failure should not block core runtime import. Using a new JSON package was rejected because Unity already has a simple report shape and adding dependencies would widen the editor compile surface.
Scalability potential: Editor-only proof route has 0 runtime cost on all hardware. Low/Middle/High/Ultra KCC gameplay math remains unchanged; the saved risk budget protects iteration velocity instead of frame time.
Hardware Impact: Runtime impact 0 us. Developer iteration impact is reduced after Unity import because KCC editor scanner changes no longer force broad Core recompilation.

Problem: Safety audit found two stale/overbroad suppressions and one incomplete safety justification.
Solution: Removed `NativeDisableContainerSafetyRestriction` from KCC queue writer jobs, corrected the `ApplyEnvironmentalForcesJob` comment to state direct per-index `KinematicStateDTO` mutation, and replaced `DispatcherJobFence.TryComplete(ref _postSimulationHandle, false)` with `TryFinalizeCompleted` so LateFrame does not emit the non-forced completion warning path.
Rejected Alternatives: Keeping the suppressions was rejected because `NativeQueue<T>.ParallelWriter` already expresses the producer pattern. Forcing completion was rejected because it can stall the main thread; the post path now only finalizes handles that are already complete.
Scalability potential: No gameplay fidelity change. The dependency graph remains continuous and dispatcher-owned.
Hardware Impact: Runtime savings are not claimed; this is correctness and safety-surface reduction.

Problem: Audit called `Hecton8.Core.Contracts.Physiology` a direct Physiology dependency.
Solution: Kept the Core.Contracts ABI route because the runtime imports no `Hecton8.Physiology` assembly and the XML explicitly requires reading `MetabolicStateDTO`. The shared DTO and buffer id remain in `Hecton8.Core.Contracts.Physiology`, consumed by both KCC and Physiology.
Rejected Alternatives: Duplicating `MetabolicStateDTO` in KCC was rejected because DataVault type hashes would diverge. Referencing the `Hecton8.Physiology` runtime assembly was rejected as a compile-wall cycle. Renaming the contract again without compile proof was rejected because it would churn multiple domains for no behavioral gain.
Scalability potential: Same 32-byte contract across all quality levels; quality only changes sampling fidelity and visual response.
Hardware Impact: Runtime impact 0 us; preserves cross-domain ABI and avoids assembly-cycle risk.

Problem: Compile/import proof is still unavailable after the source hardening.
Solution: Keep verification at static proof level. CPU counter sampled 100 percent, so Unity import, batch compile, and `dotnet build` remain blocked by the active build gate. Bee still lists KCC editor files in `Hecton8.Core.rsp` until Unity regenerates project artifacts after the new editor asmdef import.
Rejected Alternatives: Launching `dotnet build` was rejected because stale `.csproj` files would not verify the KCC runtime/editor changes. Launching Unity import at 100 percent CPU was rejected because it violates the no-build-under-load rule and would risk editor/cache contention.
Scalability potential: No runtime change; this protects developer iteration hardware while preserving the evidence chain.
Hardware Impact: Runtime 0 us; avoids forcing a saturated machine into another compile/import pass.

## Loop 9 Black-Box Artifact Hardening

Problem: `DumpTelemetry` wrote directly to final `.bin` paths with `FileMode.Create`, so an I/O failure or process kill could leave `Dump_SHINOBU_250.bin` partially truncated and unusable for the required 300-frame forensic record.
Solution: Fail-close dump directory creation, stage dump bytes into `path + ".tmp"` with `FileOptions.WriteThrough`, close the stream, then swap the artifact into the final path via `File.Replace` when possible or move fallback when no prior file exists. On failure, delete only the temporary file and leave any prior final dump intact when the platform replacement path succeeds.
Rejected Alternatives: Writing direct final bytes was rejected because it destroys the previous valid black-box artifact before the new one is complete. Leaving `Directory.CreateDirectory` outside a guarded fault path was rejected because file-system failure could cascade from NaN reporting into another managed exception. Adding a background managed writer thread was rejected because it would add ownership, lifetime, and compile risk outside the KCC dispatcher scope. Logging failures from the dump catch was rejected to avoid recursive fault/log pressure during crash handling.
Scalability potential: Runtime KCC math is unchanged across Low/Middle/High/Ultra. The fault artifact route is quality-independent; stronger hardware gets no different truth path, only a more reliable proof artifact if a fault occurs.
Hardware Impact: Hot path 0 us. Fault-only managed I/O adds a temp-file rename/replace cost after a NaN/fault mask, outside the normal 16.67 ms simulation budget.

Problem: The compile gate remained blocked after the black-box hardening patch.
Solution: Re-ran static gates only: prompt extraction returned `TASK_COUNT=20`, KCC raw braces are `321/321`, focused KCC scan found no `NativeDisableContainerSafetyRestriction`, no `TryComplete(ref _postSimulationHandle, false)`, no direct `Hecton8.Physiology` import, and no old `HydrodynamicIntegrationJob`; `git diff --check` stayed clean except the existing CRLF warning.
Rejected Alternatives: Launching Unity import or `dotnet build` at a sampled 100 percent CPU was rejected under the active no-build-under-load rule. Root project files remain stale for KCC, so `dotnet build` would still not prove this runtime/editor code.
Scalability potential: No gameplay route change. This preserves verification honesty while the machine is saturated.
Hardware Impact: Runtime 0 us; avoids adding compile/import contention to an already loaded system.

## Loop 10 Metabolism Route Collision Repair

Problem: The shared `MetabolicStateDTO` publication contract used BufferID `70265`, but `DroneFleetManager` also uses `70265` as `DroneFleetStateDtoBufferId` and `DRONE_FLEET_PROTOCOL.md` documents that lane as `DroneStateDTO[512]`. KCC opening `70265` as `MetabolicStateDTO` would depend on DataVault type rejection to fail closed, but the route itself violates one-owner/one-route semantics.
Solution: Move `ShinobuMetabolismVaultContract.MetabolismStatesBufferId` to unused physiology-range lane `70238`, add `BufferID.ShinobuMetabolismStates = 70238` to reserve the lane in `H8Memory`, and update SHINOBU_250 architecture/binary ledger wording. KCC still reads the published lane only through the shared Core.Contracts DTO and still falls back to KCC-owned mock metabolism `71764` when the published descriptor is absent, locked, short, or invalid.
Rejected Alternatives: Keeping `70265` was rejected because it leaves a live cross-domain BufferID alias. Editing DroneFleet local IDs was rejected because that is Construction domain and has wider documented local lanes `70265..70275`; SHINOBU_250 only needs a clean physiology read route. Duplicating the metabolism DTO in KCC was rejected because DataVault type identity would diverge.
Scalability potential: Low/Middle/High/Ultra gameplay math is unchanged. The route move changes ownership identity only; `GlobalQualityWeight` still affects sampling fidelity, not DTO layout or authority.
Hardware Impact: Hot path 0 us. The gain is correctness: KCC no longer probes a known DroneFleet DTO lane before falling back.

Problem: Static proof was required after moving the route.
Solution: Re-ran scoped scans and structural gates. SHINOBU contracts/Physiology/KCC docs now use `70238` for the published metabolism lane; `70265` remains only as the documented DroneFleet conflict and in repair rationale text. `git diff --check` passed with existing CRLF warnings only. Brace counts: KCC runtime `321/321`, H8Memory `174/174`, MetabolicStateContract `3/3`.
Rejected Alternatives: Running compile/import at sampled 99-100 percent CPU was rejected under the no-build-under-load rule.
Scalability potential: No gameplay tier change; route identity is fixed across all quality weights.
Hardware Impact: Runtime 0 us; reduces failed descriptor probing and removes a cross-domain alias.

## Loop 11 Cross-Domain Ownership Audit

Problem: After context compaction, the tracked status underreported the real blast radius of the metabolism ABI repair. The shared DTO move also changes Physiology source files because Physiology must consume the same `Hecton8.Core.Contracts.Physiology.MetabolicStateDTO` that KCC reads, otherwise `GlobalDataVault` type identity diverges.
Solution: Re-audit focused diffs and record the cross-domain contract files explicitly: `ShinobuMetabolismData.cs` now aliases size, flags, and BufferID from `ShinobuMetabolismVaultContract`; `ShinobuMetabolismJobs.cs` and `ShinobuMetabolismRuntime.cs` import the shared contract. This keeps one DTO type identity across Physiology and KCC while leaving metabolism ownership inside Physiology.
Rejected Alternatives: Hiding the Physiology edits in the KCC report was rejected because it violates evidence-based handoff. Reverting Physiology consumption was rejected because KCC would then read a different DTO type from the Vault and fail the ABI route. Editing DroneFleet was rejected because `70265` belongs to Construction/DroneFleet documentation and is outside SHINOBU_250 ownership.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; the contract repair changes only ownership identity and type safety. Exhaustion remains continuous scalar drag in KCC, with `GlobalQualityWeight` affecting sampling fidelity rather than metabolism layout.
Hardware Impact: Runtime hot path 0 us. The value is compile-wall containment and one-route DataVault correctness; the only extra source impact is neutral contract imports in Physiology.

Problem: The current worktree contains broad unrelated modifications and untracked neighboring artifacts, including `KinematicSleepStateJobs.cs` under KCC from SHINOBU_249 and a huge replacement diff in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
Solution: Treat non-SHINOBU_250 files as read-only unless they are required for the metabolism ABI route. Do not revert or normalize the binary ledger because it may contain current documentation surgery from other agents; only the SHINOBU_250 route addendum and collision note are relied on by this task.
Rejected Alternatives: Blindly restoring the ledger from HEAD was rejected because it would delete current R51-style ledger content and other agents' route changes. Editing the SHINOBU_249 sleep jobs was rejected because it is a neighboring KCC task and not needed for environmental force integration.
Scalability potential: No runtime behavior change. This prevents concurrent-agent damage while preserving the KCC environmental route proof.
Hardware Impact: Runtime 0 us; reduces merge and compile-wall risk.

Problem: Compile/import gate was rechecked after the audit.
Solution: Refuse build. `Get-Process` shows seven active `dotnet` processes and CPU sampled 100 percent. Unity/Bee project regeneration is still needed before KCC editor asmdef proof is meaningful.
Rejected Alternatives: Launching `dotnet build` was rejected because it violates the CPU/build gate and stale `.csproj` state still would not prove the edited KCC runtime/editor sources.
Scalability potential: Verification honesty is independent of hardware tier.
Hardware Impact: Avoids adding build contention to an already saturated machine.

## Loop 12 Pointer-Lane Fail-Closed Hardening

Problem: `ApplyEnvironmentalForcesJob` and `KinematicResolutionJob` use pointer/ref row mutation for cache locality, but their first pointer calculations assumed the scheduler always passes arrays at matching capacity. Vault allocation should guarantee that, but memory safety law prefers a local fail-closed guard before pointer arithmetic.
Solution: Add early `IsCreated` and length guards for mandatory lanes before `NativeArrayUnsafeUtility.GetUnsafePtr` in both jobs. `ApplyEnvironmentalForcesJob` also now checks `Inputs.IsCreated` before reading the optional input row. If a required lane is absent or short, the job returns without touching memory; the owner will surface missing-lane state through static/runtime verification rather than corrupting memory.
Rejected Alternatives: Trusting only schedule-time capacity was rejected because it leaves a hard crash if a stale handle or partial Vault setup slips through. Adding a new diagnostic queue was rejected because it would widen the dependency graph and allocate more global surface for a lane absence that should be caught by owner setup.
Scalability potential: Gameplay math is unchanged across Low/Middle/High/Ultra. This only protects the route when authority lanes are invalid; quality still controls continuous sampling and slide response.
Hardware Impact: One predictable guard block per scheduled row; expected cost is below measurable noise and buys memory safety on malformed setup. No hot GC.

Problem: Build gate changed shape after the hardening pass.
Solution: Static proof only. KCC raw braces are `323/323`; focused scan confirms no `NativeDisableContainerSafetyRestriction`, no forbidden non-forced `_postSimulationHandle` completion, no direct `Hecton8.Physiology` import, and no `HydrodynamicIntegrationJob`. CPU later sampled 39 percent, but seven active `dotnet` processes still block compile/import under the user's rule.
Rejected Alternatives: Launching a build while another dotnet/MSBuild workload is active was rejected even though CPU dipped below 50 percent.
Scalability potential: No runtime tier change.
Hardware Impact: Avoids compile contention; runtime verification remains pending.

## Loop 13 Optional Output Guard Audit

Problem: `ApplyEnvironmentalForcesJob` treated wake packets and environment debug DTOs as optional outputs, but the write guards checked `Length` without first checking `IsCreated`. Default `NativeArray<T>.Length` is expected to be benign, but fail-closed memory discipline should not rely on that implementation detail.
Solution: Add explicit `WakePackets.IsCreated` and `EnvironmentDebugOutputs.IsCreated` checks before the optional output writes. Mandatory route lanes remain guarded at the top of the job; `FaultFlags` still guards inside `WriteFault`.
Rejected Alternatives: Promoting wake/debug outputs to mandatory early-return lanes was rejected because environmental movement truth should still compute if optional proof/presentation lanes are unavailable during editor or partial Vault setup. Leaving the code as-is was rejected because the latest hardening pass is specifically about local lane validity before touching memory.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. This is a proof-lane safety guard, not a quality-path switch.
Hardware Impact: Two predictable branch predicates before optional writes; no GC and no material frame-time cost. Static verification remains the only available proof while compile/import is gated.

## Loop 14 Dump Integrity Audit

Problem: Read-only subagent audit found two fault-artifact integrity risks. KCC no longer wrote directly to final dump files, but the unsupported-`File.Replace` fallback deleted the previous final artifact before moving the new temp file. Physiology `DumpBlackBox` still wrote directly to `_dumpPath` with `FileMode.Create`, so an I/O fault could truncate `Dump_METABOLISM_SURGEON.bin`.
Solution: Change KCC fallback replacement to a backup/restore route: delete only stale `.bak`, move existing final to `.bak`, move temp to final, and restore `.bak` if the move fails. Change Physiology dump to create the target directory, write `_dumpPath + ".tmp"` with `FileOptions.WriteThrough`, then use `File.Replace` or the same backup/restore fallback; catch paths delete only the temp artifact.
Rejected Alternatives: Keeping the KCC delete-then-move fallback was rejected because it could destroy the last valid dump on a failed move. Leaving Physiology direct write was rejected because KCC now consumes Physiology-owned metabolism truth and the black-box doctrine requires a reliable proof artifact. Adding async/background dump writers was rejected because crash-path managed lifetime and thread ownership would widen the fault surface.
Scalability potential: No Low/Middle/High/Ultra gameplay route change. Fault artifact reliability is quality-independent; the visual/performance scaler still affects only sampling fidelity and response, not dump identity.
Hardware Impact: Hot path 0 us. Fault-only I/O gains artifact survival; no GC or gameplay-time allocation is introduced.

## Loop 15 Log Ordering Correction

Problem: The Loop 14 report was inserted above an older Loop 13 `<SELF_AUDIT>` block, so the bottom-most audit in `LOG_SHINOBU_250.md` still claimed currency only through Loop 13.
Solution: Append `<SELF_AUDIT revision="4">` at the bottom of the log with the current dump-integrity, pointer-guard, task, Vault, layout, and compile-gate facts.
Rejected Alternatives: Editing history in-place was rejected because the log is append-only by protocol. Leaving the stale bottom block was rejected because the CTO reads the file bottom-up and would see outdated proof.
Scalability potential: No runtime tier change; this is report integrity only.
Hardware Impact: Runtime 0 us. Latest build gate remains CPU-only: CPU sampled 73 percent and no `dotnet/csc/MSBuild/Unity` process was visible, so compile/import is still blocked by the >50 percent rule.
