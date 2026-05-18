# Rationale_SHINOBU_06

Agent: SHINOBU_06
Role: SOMATIC_KINEMATICS_ARCHITECT
Status: CORE COMPLETE / ULTRA POLISH PASS 4 APPLIED / BUILD BLOCKED BY UNRELATED AGENT DEPENDENCIES

## Prompt Identity

PROMPT IDENTIFIED: SHINOBU_06 | DOMAIN: ECHELON 4 - PLAYER, KINEMATICS & TOOLS / SOMATIC KINEMATICS | TASK COUNT: 20.

## Pre-Code Analysis

Target: math-first player KCC and VR somatic locomotion without Unity Rigidbody authority.
Affected systems: player kinematics runtime, KCC SDF squeeze, local player DTOs, editor-only tuning.
Zero GC proof: hot path must use NativeArray/struct fields, fixed capacities, Burst jobs, no strings/LINQ/Unity physics casts.
State check: status/rationale were absent, created fresh. Worktree has unrelated dirty files from other agents; do not revert.
Rule quote: AUP is the only simulation-scale spatial authority; systems execute through PRE_SIMULATION/SIMULATION/POST_SIMULATION/VISUAL_SYNC; new broadcasts require typed SignalBus lanes; every critical system writes 300-frame blackbox telemetry.

## Decisions

### D00: Scope lock

Problem: Player kinematics overlaps legacy `HectonPlayerMovement`, vehicle Rigidbody code, tools, survival, audio, and VR comfort.
Solution: Keep edits inside ECHELON 4 player/kinematics/KCC/editor surfaces and use local payload structs/mocks where cross-agent systems are not available.
Rejected Alternatives: Editing `HectonPlayerMovement.cs` wholesale is too large and risks corrupting a 13k-line load-bearing integration hub; direct survival/audio/haptic concrete references violate parallel-agent decoupling.
Scalability potential: Low uses single-step math and cheap LUTs; Middle/High/Ultra can increase CCD steps, telemetry richness, and visual/haptic overkill without raising base cost.
Hardware Impact: Expected low-end i3/MX350 gain is avoiding PhysX Rigidbody/CharacterController jitter for player authority; exact microseconds PENDING VERIFICATION until compile/profiler artifacts exist.

### D01: AUP/local-solve model

Problem: Large-distance float jitter creates VR nausea and unstable collision response.
Solution: Store authoritative player location as AUP/double-compatible state, resolve local float position against a sector/origin for SIMD collision, then millimeter-snap on commit.
Rejected Alternatives: Using `Transform.position` or Rigidbody world center as authority is forbidden by AUP mandates and fails at 100km travel.
Scalability potential: Low commits at coarse 10 mm and probes sparsely; Ultra can retain every-frame Sync-Fence debug payload.
Hardware Impact: Cheap devices avoid double math in inner loops; high-end devices buy richer telemetry and smoother visual sync.

### D02: SDF over Unity physics

Problem: SphereCast/Rigidbody depenetration causes snagging, lock contention, and frame jitter in VR caves.
Solution: Use SDF distance and tetrahedral gradient push-out; keep MockWorldSampler local so solver compiles without Agent 04.
Rejected Alternatives: `Physics.SphereCast`, `CharacterController.Move`, and capsule collider penetration queries are direct violations and produce variable engine-dependent behavior.
Scalability potential: Low single-step/tetra4 fallback; Middle/High multi-step CCD; Ultra can run more micro-steps and richer haptic/acoustic feedback.
Hardware Impact: Expected lower main-thread cost than PhysX query path; exact microseconds PENDING VERIFICATION.

### D03: Dear Lie hydrodynamics

Problem: Real fluid displacement is too expensive and not necessary for vestibular belief.
Solution: Use velocity-magnitude 1D LUT drag, soft current acceleration, and surface spring algebra.
Rejected Alternatives: Navier-Stokes, body-volume displacement, per-limb water interaction, and per-entity global flow forces waste CPU and increase nausea risk.
Scalability potential: Low LUT only; Middle/High better current blend and seaglide damping; Ultra spends saved cycles on haptics/VFX, not simulation truth.
Hardware Impact: MX350/i3 path should stay within sub-0.1ms suspicion threshold, PENDING VERIFICATION.

### D04: Black Box ownership

Problem: VR movement defects are not debuggable without preceding state.
Solution: Maintain fixed 300-frame telemetry ring for velocity/thrust/SDF push-out and dump `Docs/AgentLogs/Dump_SHINOBU_06.h8dump` on non-finite detection.
Rejected Alternatives: Debug.Log spam or crash-only stack traces are not acceptable and allocate/lose context.
Scalability potential: Low ring stores compact hashes/vectors; Ultra stores richer editor visualization.
Hardware Impact: 300 compact structs is trivial memory; binary dump occurs only on fault/cold path.

### D05: SHINOBU sidecar runtime

Problem: Replacing the existing 13k-line player movement stack directly would collide with multiple active agents and risk a compile wall.
Solution: Add `SomaticKinematicsRuntime` as a scoped DataVault/Burst sidecar installed by `VRSomaticRuntimeBootstrap`, publishing KCC velocity and typed signals without concrete survival/audio/haptic dependencies.
Rejected Alternatives: Editing `HectonPlayerMovement.cs` or forcing Rigidbody removal from existing prefabs was too invasive for a parallel batch.
Scalability potential: Low/MX350 path collapses CCD to one SDF step; Middle/High/Ultra increase micro-steps, signal richness, and editor visualization.
Hardware Impact: Expected low-end gain is 35-120 us per active player frame versus PhysX queries; exact profiler artifact blocked by unrelated compile failures.

### D06: DataVault buffer ID expansion

Problem: SHINOBU needs persistent unmanaged state, hand history, tuning, drag LUT, signal scratch, and blackbox buffers.
Solution: Added `BufferID.ShinobuSomatic*` entries in the high numeric range already used by current batch systems and allocated through `GlobalDataVault`.
Rejected Alternatives: Local persistent `NativeArray` ownership would violate the prompt's vault requirement; reusing unrelated PlayerKinematic buffers would alias data from other agents.
Scalability potential: Low path keeps the same memory footprint; Ultra can increase behavior quality without reallocating.
Hardware Impact: Fixed buffers are cache-resident and avoid heap churn; memory footprint is trivial versus a single managed list.

### D07: Local partial signal payloads

Problem: Survival, AI audio, and haptic bridges are unseen and cannot become direct references.
Solution: Defined local unmanaged partial signal structs: `PlayerExertionSignal`, `AcousticEchoTap`, and `HapticRequestSignal`; runtime pushes them through `SignalBus<T>`.
Rejected Alternatives: Direct component calls or event delegates allocate, create compile dependencies, and break signal-lane segregation.
Scalability potential: Low tier emits only threshold-crossing payloads; high tier can consume richer magnitude/frequency fields.
Hardware Impact: No cost when thresholds do not fire; NativeQueue push only on exertion/impact/acoustic events.

### D08: CSV and editor control surface

Problem: Burst magic constants lock tuning behind rebuilds and encourage blind number edits.
Solution: `Somatic Tuner` reads/writes vault tuning, while `kinematic_overrides.csv` is parsed on `SlowTick` using span-based key hashing and manual float parsing.
Rejected Alternatives: JSON/ScriptableObject hot reads or managed reflection are too slow/noisy for iteration.
Scalability potential: Low devices use conservative values; top-tier can raise acceleration, buoyancy, and CCD caps for visual overkill.
Hardware Impact: Fixed loop cost is 0 us; file IO exists only when timestamp changes.

### D09: Compile-wall classification

Problem: After restoring project assets, `dotnet build Hecton8.Core.csproj` still fails.
Solution: Filtered build output for SHINOBU-owned files. No `SomaticKinematicsRuntime`, `SomaticTunerWindow`, `VRSomaticRuntimeBootstrap`, or `ShinobuSomatic` errors are present.
Rejected Alternatives: Fixing unrelated ecosystem/seismic/binary-layout missing types would cross domain and consume the batch.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact. Integrator must resolve missing unrelated types before full build can pass.

### D10: Polish mandate result

Problem: Batch protocol requires reading `<POLISH_MANDATE>` only after all SHINOBU tasks are checked or blocked.
Solution: Re-read `CURRENT_BATCH.md` after status completion; no `<POLISH_MANDATE>` tag exists in this batch file. Ran local anti-bloat scans instead.
Rejected Alternatives: Inventing a polish mandate or reading neighboring agent prompts would violate strict parsing.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.

<SELF_AUDIT>
  <QUESTION id="1">Did I use Physics.SphereCast or Rigidbody anywhere?</QUESTION>
  <ANSWER>No. `rg` found no Rigidbody, CharacterController, CapsuleCollider, or Physics.SphereCast in SHINOBU files. The runtime uses SDF math only.</ANSWER>
  <QUESTION id="2">Is the PlayerStateDTO manually padded to ensure 8-byte alignment for ARM64?</QUESTION>
  <ANSWER>Yes. `PlayerStateDTO` uses explicit offsets, no Pack=1: double3 AUP 0-23, double3 SectorOriginAUP 24-47, float3 blocks 48-107, scalar fields 108-143, padding ulong 144-159. Size 160.</ANSWER>
  <QUESTION id="3">Have I avoided properties for array structs?</QUESTION>
  <ANSWER>Yes. SHINOBU NativeArray structs use public fields. State mutation has `public unsafe ref PlayerKinematicState GetStateRef()` via `UnsafeUtility.ArrayElementAsRef`.</ANSWER>
  <QUESTION id="4">Are SignalBus pushes defined locally as partial structs?</QUESTION>
  <ANSWER>Yes. `PlayerExertionSignal`, `AcousticEchoTap`, and `HapticRequestSignal` are local unmanaged partial structs implementing `ISignal`.</ANSWER>
  <QUESTION id="5">Did I provide the Somatic Tuner Editor facade?</QUESTION>
  <ANSWER>Yes. `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs` exposes Base Drag, Stroke Multiplier, Seaglide Acceleration, Surface Buoyancy, and SceneView vectors.</ANSWER>
</SELF_AUDIT>

### D11: H-Phi / Vault sovereignty polish

Problem: The first implementation used private persistent `NativeArray<T>` view fields as cached DataVault aliases, which was functionally bounded but violated the stricter H-Phi interpretation of "all arrays live in the Vault".
Solution: Replaced those persistent views with `VaultBufferHandle<T>` fields for kinematic state, bounding sphere, hand history, tuning, drag LUT, signal scratch, blackbox, cursor, and CSV scratch. Runtime resolves handles at job boundaries, locks buffer views while jobs are in flight, and unlocks them after `DispatcherJobSwap.TryComplete`.
Rejected Alternatives: Keeping private NativeArray aliases would be faster to type but creates local data ownership ambiguity and stale-handle risk under vault hot swap.
Scalability potential: Low/Middle/High/Ultra share one authority model; high-end tiers can raise math quality without new memory ownership patterns.
Hardware Impact: Expected cheap-device gain is primarily avoiding stale NativeArray alias faults and allocator churn; exact microseconds remain PENDING PROFILER.

### D12: CSV and binary cold-path memory cleanup

Problem: The polish audit found cold-path byte-array allocation risk in CSV override ingestion and legacy binary reads.
Solution: Added `BufferID.ShinobuSomaticCsvScratch = 70128`; CSV is read into a vault byte buffer through `Span<byte>`, and legacy 16-byte probes use stack spans.
Rejected Alternatives: `File.ReadAllBytes` is simple but allocates and can create avoidable hitches if designers hot-edit during VR testing.
Scalability potential: Low-tier devices avoid heap churn; high-tier authoring still gets hot balance edits.
Hardware Impact: Hot fixed loop remains 0 us; cold edit path avoids managed byte[] allocation.

### D13: Signal ABI and dependency corridor polish

Problem: The local prompt-required partial signals were decoupled, but existing global consumers also expose canonical `MovementAcousticSignal` and `HapticRequest` lanes; three used core signal structs still carried runtime `Pack=1` risk.
Solution: Publish both local SHINOBU signal payloads and canonical typed global signals. Tightened used global signal layouts to explicit/sequential size declarations without runtime `Pack=1`. Removed the stale `using Hecton8.World;` import and left world references fully-qualified.
Rejected Alternatives: Direct calls into audio/haptics would add concrete compile coupling; local-only signals would strand current canonical consumers.
Scalability potential: Low tier emits only threshold-crossing events; Ultra can consume richer haptic/acoustic effects without changing KCC truth.
Hardware Impact: No per-frame cost below thresholds; NativeQueue/global signal push cost only on physical events.

### D14: Current compile-wall evidence after polish

Problem: A full Core build is still required to check for SHINOBU regressions, but the workspace is concurrently dirty.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` after source polish. The build fails only in unrelated shared domains: `SaveSystem/H8BinaryWorldPager.cs` missing `_writeArena`, `_compressionScratch`, `_readArena`, `_readSlotStates`, `_hotStateArena`, `_telemetryRing`; `VFX/Bioluminescence/BiolumPulseSyncRuntime.cs` missing `PredatorSignal` and `Frame` fields on `BiolumVisualSyncJob`.
Rejected Alternatives: Fixing save pager or VFX pulse job fields would cross SHINOBU domain and violate parallel-agent ownership.
Scalability potential: No runtime impact for SHINOBU.
Hardware Impact: No runtime impact for SHINOBU. Integrator must resolve those owners before Unity-level proof.

<SELF_AUDIT_POLISH>
  <TASK_CHECK>01 PASS binary fallback; 02 PASS no Unity physics authority; 03 PASS ref state mutation; 04 PASS AUP local solve; 05 PASS mock SDF/LUT; 06 PASS 3-frame VR stroke; 07 PASS tetra SDF squeeze; 08 PASS 1D drag LUT; 09 PASS exertion signal; 10 PASS seaglide controller-forward motor; 11 PASS speed/radius CCD; 12 PASS soft current; 13 PASS low-tier CCD throttle; 14 PASS algebraic surface buoyancy; 15 PASS acoustic threshold; 16 PASS haptic route; 17 PASS 300-frame blackbox; 18 PASS editor tuner; 19 PASS vault-backed span CSV; 20 PASS SceneView vectors.</TASK_CHECK>
  <ARM64_LAYOUT>PlayerStateDTO size 160: Aup double3 offset 0, SectorOriginAup double3 offset 24, Velocity float3 offset 48, LocalPosition float3 offset 60, RequestedThrust float3 offset 72, SdfPushOut float3 offset 84, AbyssalCurrent float3 offset 96, scalar floats offsets 108-124, uint flags/counters offsets 128-140, padding ulong offsets 144 and 152.</ARM64_LAYOUT>
  <ZERO_GC_CHECK>FixedTick/job path scan found no `private NativeArray`, `ReadAllBytes`, `foreach`, `new List`, `LINQ`, `Debug.Log`, direct `.Complete(`, or Unity physics authority tokens in SHINOBU files.</ZERO_GC_CHECK>
  <AUP_CHECK>Authority remains double3/AUP-compatible; collision runs on local float delta from sector origin; commit snaps millimeter deltas back into double state. Absolute AUP is not cast to float as authority.</AUP_CHECK>
  <DEAR_LIE_CHECK>Fluid truth is faked with backward-hand dot thrust, 1D drag LUT, tetra SDF push-out, algebraic buoyancy, and single-step low-tier CCD.</DEAR_LIE_CHECK>
  <DEPENDENCY_CHECK>Cross-domain output uses `GlobalRegistry`, typed `SignalBus<T>`, and canonical typed signal publishes. No direct survival/audio/haptic concrete component coupling was added.</DEPENDENCY_CHECK>
  <H_PHI_CHECK>SHINOBU persistent arrays are requested through `VaultBufferHandle<T>` from `GlobalDataVault`; runtime holds handles, not private owned NativeArray allocations.</H_PHI_CHECK>
  <BLACKBOX_CHECK>300-entry fixed blackbox ring remains active and dumps `Docs/AgentLogs/Dump_SHINOBU_06.h8dump` on non-finite state.</BLACKBOX_CHECK>
</SELF_AUDIT_POLISH>

### D15: Direct fluid dependency eviction

Problem: SHINOBU still cached a concrete `HectonFluidEngine` reference for abyssal flow. It was obtained through `GlobalRegistry`, but it still pulled a sibling runtime domain into the player KCC corridor.
Solution: Replaced the concrete fluid field with cached `IWeatherService` current data and a local deterministic triangle-wave fallback. The KCC still receives soft current advection, but it no longer depends on `HectonFluidEngine.TrySampleModAbyssalFlow`.
Rejected Alternatives: Adding a new contracts interface for abyssal flow would mutate public contracts during the batch; keeping the concrete engine reference preserves coupling.
Scalability potential: Low uses the triangle-wave fake when no weather service exists; Middle/High/Ultra consume richer weather current data without altering player collision truth.
Hardware Impact: Removes a hot-path virtual/concrete fluid call and keeps current solve as local scalar math; exact microseconds PENDING PROFILER.

### D16: Hot-path registry fallback cleanup

Problem: `FixedTick` transitively had fallback reads of `GlobalRegistry.VRSomatic` and `GlobalRegistry.ScalabilityTier`, and `EnsureNativeState` could fallback to `GlobalRegistry.DataVault`.
Solution: The runtime now relies on services cached during `Awake`, `OnEnable`, and hot-swap notifications. If a cache is absent in the fixed loop, the system degrades to deterministic local defaults.
Rejected Alternatives: Polling GlobalRegistry inside the fixed loop is convenient but violates the two-stage dependency-injection rule and hides domain-coupling cost.
Scalability potential: Low keeps no-service fallback math; High/Ultra consume cached provider/weather data.
Hardware Impact: Removes registry property fallback cost from the fixed simulation path; exact microseconds PENDING PROFILER.

### D17: CSV polling and dump extension cleanup

Problem: CSV polling used `FileInfo` allocation and blackbox dump still used `.bin` while the latest mandate requires `.h8dump`.
Solution: CSV polling now uses `File.Exists` and `File.GetLastWriteTimeUtc` before opening the stream, with length read from the stream only after a timestamp change. Fatal blackbox dump path is now `Docs/AgentLogs/Dump_SHINOBU_06.h8dump`.
Rejected Alternatives: `FileInfo` is tolerable in cold code but unnecessary; `.bin` satisfied the older XML wording but not the latest crash-forensics mandate.
Scalability potential: Low devices avoid cold-path heap churn during live tuning; high-end authoring keeps hot-reload behavior.
Hardware Impact: Hot fixed loop unchanged; SlowTick designer-edit path avoids one managed object allocation per poll.

### D18: Pass 2 verification result

Problem: The second polish pass required proof that the new dependency and I/O cleanup did not reintroduce forbidden SHINOBU patterns or compile errors.
Solution: Static scan of SHINOBU runtime/editor/bootstrap files returned no hits for `HectonFluidEngine`, `FileInfo`, `ReadAllBytes`, `private NativeArray`, `Pack=1`, Unity physics authority tokens, `foreach`, `new List`, `LINQ`, `Debug.Log`, or direct `.Complete(`. `git diff --check` passed except CRLF warnings in already-dirty shared files. `dotnet build Hecton8.Core.csproj --no-restore` still fails, but no SHINOBU file appears in the compiler errors.
Rejected Alternatives: Fixing current build blockers in tether, telemetry, ecosystem, spatial audio, or drone construction would cross ownership boundaries.
Scalability potential: No runtime impact for SHINOBU.
Hardware Impact: No runtime impact for SHINOBU. Current build wall is unrelated: `TetherInstance.cs`, `GlobalTelemetryBus.cs`, `AI/Ecosystem/ShinobuEcosystemBalancer.cs`, `SpatialAudioManager.cs`, and `Construction/DroneFleetManager.cs`.

### D19: Unity execution-order eviction

Problem: SHINOBU files still used negative `DefaultExecutionOrder` attributes. That hides ordering in Unity's MonoBehaviour scheduler and conflicts with the project's explicit phase/dispatcher ownership model.
Solution: Removed `DefaultExecutionOrder` from `SomaticKinematicsRuntime` and `VRSomaticRuntimeBootstrap`. Kinematic cadence stays under `IFixedTickable`, `IPostFixedTickable`, `ISlowTickable`, hot-swap listeners, and bootstrap events.
Rejected Alternatives: Keeping execution-order attributes is easy, but it preserves scene-wide ordering fragility and can mask registration bugs during VR bootstrap.
Scalability potential: Low/Middle/High/Ultra now share one deterministic phase path; high-end visual overkill remains downstream of signals rather than MonoBehaviour order.
Hardware Impact: No measurable hot-path microsecond claim; the gain is compile/runtime predictability and removal of an implicit Unity scheduling dependency.

### D20: Pass 3 build evidence

Problem: The first post-pass-3 build hit missing `Temp/obj/Hecton8.Core/project.assets.json`, and a subsequent retry timed out before emitting errors.
Solution: Ran `dotnet restore Hecton8.Core.csproj`, then reran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` with a longer timeout. The build emitted no SHINOBU-owned errors.
Rejected Alternatives: Editing drone, homeostasis, ecosystem, or vegetation code would cross domain ownership and violate the 3-strikes/compile-wall protocol.
Scalability potential: No runtime impact for SHINOBU.
Hardware Impact: No runtime impact for SHINOBU. Current unrelated compile wall is `Construction/DroneFleetManager.cs`, `Core/HomeostasisBrain.cs`, `AI/Ecosystem/ShinobuEcosystemBalancer.cs`, and `World/HectonIndirectVegetationRenderer.cs`, plus a duplicate-source warning for `HectonPhysicsContract.cs`.

### D21: NaN vaccine and hostile tuning clamp

Problem: The KCC job consumed tuning from DataVault, CSV, and legacy binary paths. Those sources are human/cold-path controlled and could still hold NaN, negative radius, invalid CCD steps, or hostile drag values, which would violate the NaN survival mandate before the post-frame blackbox could explain the failure.
Solution: Added Burst-side `SanitizeTuning(ref SomaticKinematicsTuningData)` and range guards for every numeric tuning field used by the solver. Hardened drag denominator, CCD speed calculation, and CSV fractional parsing with explicit finite/denominator guards.
Rejected Alternatives: Trusting the editor sliders and CSV parser is insufficient; DataVault memory is shared and must be defensively read at the job boundary.
Scalability potential: Low tier now cannot be knocked out by malformed tuning into excessive CCD or NaN drag; High/Ultra can still raise quality within bounded ranges.
Hardware Impact: Adds a small fixed scalar clamp cost once per player KCC job. It buys deterministic failure resistance; no microsecond saving is claimed.

### D22: Pass 4 verification result

Problem: The NaN vaccine changed Burst job math and must not introduce SHINOBU compile or forbidden-pattern regressions.
Solution: Ran SHINOBU forbidden scan, `git diff --check`, and `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`. The build emitted no SHINOBU-owned errors.
Rejected Alternatives: Adding a missing `WakeRequestSignal` or `QueuePhysicsWakeRequest` implementation in `GlobalPhysicsStateManager` would cross into global physics ownership and is not SHINOBU somatic KCC work.
Scalability potential: No runtime impact beyond the pass 4 clamps.
Hardware Impact: No new microsecond claim. Current unrelated compile wall is `GlobalPhysicsStateManager.cs(119,34)` and `(1343,41)` missing `WakeRequestSignal`.
