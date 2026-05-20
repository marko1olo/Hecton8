# SHINOBU_158 Status - Buoyancy And Displacement Solver

Date: 2026-05-19
Status: PENDING VERIFICATION
Domain: Echelon 4 Player/Kinematics/Tools - Hydrodynamic Drag & Buoyancy
Task Count: 20
First 20 Minutes moment: Swim / Hazard

## Mandates Loaded

- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Execution_Phases.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt

## Loop 0 - Setup

- [x] Extract prompt SHINOBU_158 from CURRENT_BATCH.md | DOD: raw `IndexOf` extraction over exact XML tag. | Rejected: neighboring-prompt memory. | Estimate: 450 us cold file scan.
- [x] Read AGENTS.md, domain, binary ledger, route docs, first-20-minutes contract, and 8 mandates | DOD: on-disk authority rehydrated before code/status edits. | Rejected: chat-only recall. | Estimate: 2500 us cached reads.
- [x] Create/update status and rationale files | DOD: disk-backed state exists. | Rejected: chat-only task tracking. | Estimate: 120 us metadata write.

## Loop 1 - Tasks 01-05

- [x] Task 01 MONOBEHAVIOUR_BUOYANCY_ERADICATION | DOD: scanned `FixedUpdate` plus force/buoyancy keywords; no direct per-object `FixedUpdate`+`Rigidbody.AddForce` offender found. Logged `BuoyancyObject`, `HectonFluidEngine`, `Floater`, `DeployableBeacon` dependencies. | Rejected: deleting dry-zone/acoustics/player/floater facades outside owner boundary. | Estimate: avoids future per-object script dispatch, static estimate 5-20 us per 100 active objects.
- [x] Task 02 MESH_VOLUME_CALCULATION_PURGE | DOD: `Physics.ComputePenetration` absent; new solver uses prebaked `VolumeCubicMeters` and sphere/box height fake. Legacy `HectonFluidEngine` bounds sampling logged, not blindly deleted. | Rejected: runtime MeshCollider submerged-volume truth and cross-domain submarine rewrites. | Estimate: replaces O(n*samples/mesh) with O(n) scalar math.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD: `BuoyancyStateDTO` has only public fields, no properties; SHINOBU files scan clean for `{ get; set; }`. | Rejected: DTO properties and class-backed state. | Estimate: removes defensive-copy/property-call risk in NativeArray traversal.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD: `BuoyancyStateDTO` explicit 64 bytes with offsets 0/24/36/40/44/48/52/56 and cached layout validation. | Rejected: `Pack=1`, sequential guesswork, and unpadded 52-byte record. | Estimate: one L1 cache line per state; avoids unaligned ARM64 cache-line split.
- [x] Task 05 EMERGENCY_MOCK_PHYSICS_DATA | DOD: `GenerateMockBuoyantObjectsJob` seeds up to 1000 deterministic synthetic DTOs into Vault state/debug buffers. | Rejected: managed mock lists and scene helper GameObjects. | Estimate: cold vectorized setup; runtime profiling can run without inventory dependency.
- [ ] Verify compile/static after tasks 01-05 | Static scans passed for owned files; CLI compile pending CPU/dotnet gate.

## Loop 2 - Tasks 06-10

- [x] Task 06 BURST_ARCHIMEDES_KERNEL | DOD: `EvaluateBuoyancyJob` computes depth, density, displaced volume, and buoyant force over flat NativeArrays with `[NoAlias]` and deterministic Burst flags. | Rejected: MonoBehaviour physics and virtual interface arrays. | Estimate: O(n) linear traversal; static target below 100 us for 1000 objects after Burst proof.
- [x] Task 07 FLUID_DRAG_INTEGRATION | DOD: drag opposes relative velocity and blends linear/quadratic through quality curve. | Rejected: Euler velocity mutation in object scripts. | Estimate: low quality skips exact sqrt path; saves several ALU ops per evaluated object.
- [x] Task 08 THE_DEAR_LIE_SURFACE_SNAP | DOD: near-surface damping/snap forces stable surface rest instead of harmonic bobbing. | Rejected: per-object harmonic water surface truth. | Estimate: prevents persistent jitter and later force churn.
- [x] Task 09 MATHEMATICAL_SLEEP_STATE | DOD: stable surface/seafloor objects set `FlagSleeping`; job returns immediately for sleepers. | Rejected: continuous micro-force integration at equilibrium. | Estimate: sleeper cost falls to one state read/branch/write-debug path.
- [x] Task 10 FORCE_PACKET_ROUTING | DOD: job writes unmanaged `BuoyancyForcePacketDTO` rows to Vault buffer `71621`; `PhysicsApplySystem` is the only drain/apply owner and no job calls `Rigidbody`. | Rejected: direct `Rigidbody.AddForce`, private runtime `NativeQueue`, and generic `SignalBus<T>` force fan-out. | Estimate: force application remains in existing main-thread owner.
- [ ] Verify compile/static after tasks 06-10 | Static scans passed for owned files; CLI compile pending CPU/dotnet gate.

## Loop 3 - Tasks 11-15

- [x] Task 11 CONTINUOUS_SCALABILITY_DRS | DOD: `GlobalQualityWeight` drives stride `12->1`, drag blend, flow amplitude, snap depth, and cheap/exact speed blend. | Rejected: binary low/high hardware switches. | Estimate: at q~0.1 evaluates roughly 1/12 of active records per fixed tick.
- [x] Task 12 ABYSSAL_CURRENT_ADVECTION | DOD: reads Vault flow samples when active; falls back to deterministic triangle-wave current fake. | Rejected: main-thread flow queries and Navier-Stokes. | Estimate: sample path O(1), fallback no trigonometric functions.
- [x] Task 13 AUP_PRECISION_DEPTH_MATH | DOD: subtracts `double3 OceanSurfaceAUP - CurrentAUP`, then casts vertical delta to float. | Rejected: absolute float world depth. | Estimate: prevents 100 km edge jitter with negligible ALU cost.
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE | DOD: deterministic Burst jobs, blittable explicit DTOs, state in Vault, no `Time.deltaTime`. | Rejected: nondeterministic float fast mode for authority math. | Estimate: no runtime gain claimed; correctness gate for rollback.
- [x] Task 15 ZERO_INIT_OVERHEAD_BYPASS | DOD: large Vault buffers request `UninitializedMemory`; readable flow/material/debug/telemetry/binding/counter buffers are cleared by a cold Burst initializer before use. | Rejected: private persistent NativeArray allocations and OS zero-fill on every large buffer. | Estimate: avoids broad zero-fill while preventing random readable flags.
- [ ] Verify compile/static after tasks 11-15 | Static scans passed for owned files; CLI compile pending CPU/dotnet gate.

## Loop 4 - Tasks 16-20

- [x] Task 16 TELEMETRY_BUOYANCY_RECORDER | DOD: 300-entry Vault telemetry ring, counters, compute micros, non-finite flag, and `Dump_FLUID_DYNAMICS.bin` dump path. | Rejected: string logs as forensic source. | Estimate: fixed 19.2 KB ring, O(active) reduction.
- [x] Task 17 BUOYANCY_TUNER_EDITOR_WINDOW | DOD: UI Toolkit window modifies Vault tuning DTO and displays active/sleep/packet/non-finite/compute/depth/quality readout. | Rejected: ScriptableObject recompiles and runtime UI polling. | Estimate: editor-only; no gameplay hot-path cost.
- [x] Task 18 CSV_MATERIAL_VOLUMES_INGESTOR | DOD: cold `ReadOnlySpan<byte>` parser hashes item names with FNV-1a into fixed Vault table. | Rejected: `string.Split`, LINQ, managed dictionary, private persistent NativeHashMap. | Estimate: bounded 64 KB scratch, cold-only parse.
- [x] Task 19 LIVE_FORCE_DEBUG_GIZMO | DOD: `OnDrawGizmos` draws blue buoyancy, red gravity, green drag from debug DTOs under editor guard. | Rejected: runtime debug GameObjects and line-renderer allocation. | Estimate: editor-only; gameplay build hot path unaffected.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: `<SELF_AUDIT>` appended to `Docs/AgentLogs/LOG_SHINOBU_158.md` with Task 20 marked FAIL for missing Unity import/compile/Burst/profiler/GC proof. | Rejected: claiming runtime proof under CPU/dotnet gate. | Estimate: no runtime cost.
- [ ] Verify compile/static after tasks 16-20 | Static scans passed for owned files; CLI compile pending CPU/dotnet gate.

## Loop 5 - Self-Review

- [x] Re-read SHINOBU_158 prompt after implementation | DOD: exact XML extracted again. | Rejected: memory-only reconciliation. | Estimate: 450 us cached read.
- [x] Static owned-file hygiene scans | DOD: no matches in SHINOBU files for `Pack=`, DTO properties, gameplay `Update/FixedUpdate/LateUpdate`, direct `Rigidbody.AddForce`, runtime `MeshCollider` volume APIs, private NativeArray/List/HashMap allocations, LINQ, `StringBuilder`, or numeric `.ToString()`. | Rejected: relying on visual inspection. | Estimate: source-only proof, no runtime claim.
- [x] Global route docs updated | DOD: route card and binary payload ledger entry added. | Rejected: chat-only architecture proof. | Estimate: no runtime cost.
- [x] CPU/dotnet gate check before CLI compile | DOD: 3-sample CPU average captured at `99.94%`; no compile launched under user gate. | Rejected: starting `dotnet build` while CPU > 50%. | Estimate: prevents compile-wall contention.
- [x] Force-packet route reconciliation | DOD: named `ShinobuBuoyancyForcePackets = 71621` added and route docs/rationale corrected from stale NativeQueue wording to the Vault-owned force-packet window. | Rejected: hidden second packet route. | Estimate: no hot-path cost, removes raw BufferID cast.
- [x] Source-level contract checks | DOD: verified referenced dispatcher registration, `HomeostasisBrain.GlobalQualityWeight`, AUP runtime projection, `PhysicsApplySystem.QueueForce`, and partial `GlobalPhysicsStateManager` bridge exist in source. | Rejected: claiming Unity import proof without Console. | Estimate: static confidence only.
- [x] CPU/dotnet gate retry | DOD: 3-sample CPU average captured at `100%` with 7 `dotnet`/`csc` processes; no compile launched under user gate. | Rejected: adding another compile process during active compile wall. | Estimate: prevents developer-hardware contention.
- [x] CPU/dotnet gate final retry | DOD: after 20 seconds, `dotnet`/`csc` count dropped to 0 but CPU average remained `94.19%`; no compile launched under user gate. | Rejected: starting build while CPU > 50%. | Estimate: prevents local machine contention.
- [ ] Retry CLI compile only after CPU <= 50% and no `dotnet`/`csc` process is active.
- [x] Append LOG_SHINOBU_158.md with `<SELF_AUDIT>` after compile decision | DOD: audit appended with compile status `BLOCKED_CPU_DOTNET_GATE`, not accepted as runtime proof. | Rejected: chat-only forensic report. | Estimate: no runtime cost.

## Loop 6 - Polish Source Review

- [x] AUP current fallback correction | DOD: fallback triangle current now uses `CurrentAUP - SectorAUP` before `float3` conversion; flow-sample radius remains local delta. | Rejected: absolute AUP X/Z casts at 100 km edge. | Estimate: prevents precision jitter from breaking sleep; no measured runtime claim.
- [x] Low-quality drag collapse | DOD: `math.step` gates keep q<0.25 on linear drag and bypass relative-speed/sqrt work; q>0.25 blends quadratic drag through `Smooth01`, q>0.3 blends exact speed. | Rejected: always computing quadratic/exact path then lerping it away. | Estimate: saves one `lengthsq` and possible sqrt per evaluated object below q=0.25.
- [x] Play-mode Vault boot guard | DOD: `Awake` returns when not playing, preventing editor import/selection from requesting runtime Vault buffers or scheduling cold jobs. | Rejected: edit-mode runtime memory ownership. | Estimate: editor responsiveness protection, no gameplay cost.
- [x] Quality recovery fix | DOD: authored `GlobalQualityWeight` remains a cap; runtime writes `ResolvedQualityWeight` into existing tuning DTO padding and passes the resolved value to jobs/telemetry. | Rejected: overwriting the cap and making thermal recovery sticky low. | Estimate: correctness fix; avoids long-term under-evaluation after throttle release.
- [x] Explicit cold clear methods | DOD: replaced generic `Clear<T>` inside the Burst initializer with concrete typed loops for every readable Vault buffer. | Rejected: generic helper inside Burst cold job. | Estimate: cold-only; removes a source-level Burst compatibility risk.
- [x] Live sector AUP binding | DOD: scheduler writes `SectorAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble` before job scheduling, so local current math survives origin shifts. | Rejected: static zero sector origin. | Estimate: prevents precision churn at map edges; no measured runtime claim.
- [x] Bottom sleep correction | DOD: seafloor contact now sleeps on low velocity without requiring buoyancy/gravity force equilibrium; surface sleep still requires force balance. | Rejected: keeping sunk objects awake forever because residual force is non-zero. | Estimate: cargo/debris piles on the bottom drop to sleeper branch after settling.
- [x] Deferred force drain fence | DOD: `_forcePacketsReadyToDrain` blocks the next schedule/packet reset until `PostFixedTick` drains late-completed solver output. | Rejected: blocking post-fixed completion and late-frame force application. | Estimate: prevents overwritten packet work; low-end may skip one schedule slot instead of dropping force packets.

## Loop 7 - Stride Scheduling Polish

- [x] True strided scheduling | DOD: `FixedTick` now schedules `ceil((ActiveStateCount - EvaluationOffset) / EvaluationStride)` work items instead of scheduling every active row and returning early inside the job. | Rejected: branch-only stride that still pays one scheduled job item per active state. | Estimate: at q~0.1, 1000 active rows schedule roughly 83-84 job items instead of 1000.
- [x] Round-robin starvation guard | DOD: when active count is smaller than stride and the current offset has no rows, a reduce-only telemetry job still advances `_simulationFrame`, preserving deterministic offset rotation. | Rejected: returning without frame advance, which can pin the scheduler to an empty offset forever. | Estimate: no force work on empty-offset frames; exact runtime savings unclaimed.
- [x] Stale debug telemetry correction | DOD: `ReduceBuoyancyTelemetryJob` now takes force packet count from the false-sharing-padded atomic counter and only accumulates evaluated/force totals from debug rows matching `SimulationFrame`. | Rejected: counting stale `FlagForceQueued` rows from prior strided frames. | Estimate: prevents false packet/evaluated counts under low-quality stride.
- [x] Unity meta stabilization | DOD: fixed `.meta` files were added for all six new SHINOBU_158 C# assets plus the two new asset folders. | Rejected: letting Unity generate unstable GUIDs during import. | Estimate: no runtime impact; protects scene/editor reference stability.
- [x] CPU/dotnet gate after stride patch | DOD: `dotnet/csc` count was `0`, but CPU load was `100%`; no CLI compile launched under the user gate. | Rejected: running build while system CPU exceeds 50%. | Estimate: protects local compile wall.

## Loop 8 - Runtime Rot Pass

- [x] Runtime reflection purge | DOD: `BuoyancyDisplacementLayout.OffsetOf` now uses explicit constants and contains no `System.Reflection`/`GetField` path. | Rejected: runtime `typeof(T).GetField` during Vault boot. | Estimate: cold boot allocation/reflection risk removed from player path.
- [x] Idempotent cold boot | DOD: Awake/OnEnable now route through `_coldBootCompleted` so CSV load and 1000-object mock generation run once per acquired Vault, not twice in normal Unity lifecycle. | Rejected: duplicate FileStream read and mock seeding across Awake+OnEnable. | Estimate: cold startup avoids one duplicate CSV pass and one duplicate 4096-row mock schedule.
- [x] Drain-fence deadlock guard | DOD: if post-fixed cannot resolve Vault/packet handles, `_forcePacketsReadyToDrain` is cleared instead of permanently blocking future `FixedTick` scheduling. | Rejected: preserving a stale drain flag after DataVault loss. | Estimate: prevents route deadlock; may drop stale packets only when the owner memory route is unavailable.
- [x] Current-frame telemetry vector | DOD: `LastNetForce` now records the last current-frame evaluated row and sanitizes vector components before storing. | Rejected: using the final debug array row, which can be stale under strided scheduling. | Estimate: forensic accuracy fix, no hot-path speed claim.
- [x] Black-box dump alias | DOD: fault dump writes both XML-required `Dump_FLUID_DYNAMICS.bin` and AGENTS-required `Dump_SHINOBU_158.bin`. | Rejected: satisfying only one documented crash artifact path. | Estimate: fatal-path I/O only.
- [x] Editor facade preprocessor guard | DOD: the UI Toolkit tuner file is explicitly wrapped in `#if UNITY_EDITOR` in addition to living under `Editor/`. | Rejected: relying only on folder placement for the mandate wording. | Estimate: no runtime impact.
- [x] CPU/dotnet gate after Loop 8 | DOD: `dotnet/csc` count was `0`, CPU load was `100%`; no CLI build launched. | Rejected: build under CPU > 50%. | Estimate: protects developer hardware.

## Loop 9 - AsRef State Mutation Pass

- [x] Hot state mutation via `UnsafeUtility.AsRef` | DOD: `EvaluateBuoyancyJob` maps `workIndex` to a raw `BuoyancyStateDTO*`, mutates through `ref BuoyancyStateDTO`, and has no `States[index]` setter path. | Rejected: NativeArray indexer writeback for hot state mutation. | Estimate: removes one defensive-copy/writeback risk per evaluated state.
- [x] Cold mock state mutation via `UnsafeUtility.AsRef` | DOD: `GenerateMockBuoyantObjectsJob` also writes mock state through `UnsafeUtility.AsRef<BuoyancyStateDTO>`. | Rejected: leaving a second state-writer style in the same domain. | Estimate: cold-only consistency; no gameplay frame claim.
- [x] Static scan after AsRef pass | DOD: no direct `States[index]` state write remains; forbidden hot-path scan still returns no matches. | Rejected: relying on manual inspection. | Estimate: static proof only.
- [x] CPU/dotnet gate after Loop 9 | DOD: `dotnet/csc` count was `0`, CPU load was `100%`; no CLI build launched. | Rejected: build under CPU > 50%. | Estimate: protects developer hardware.

## Loop 10 - Unity Job Safety Pass

- [x] Strided writer safety annotations | DOD: `EvaluateBuoyancyJob` marks `States` and `DebugForces` with `[NativeDisableParallelForRestriction]` because scheduled `workIndex` maps to `stateIndex = workIndex * stride + offset`. | Rejected: relying on Unity's per-index parallel-for safety while writing non-workIndex rows. | Estimate: prevents safety exceptions without changing mathematical disjointness.
- [x] Mock writer safety annotation | DOD: `GenerateMockBuoyantObjectsJob.States` is also marked `[NativeDisableParallelForRestriction]` because it writes through a raw pointer. | Rejected: unsafe pointer writes under default parallel-for write restriction. | Estimate: cold-only correctness guard.
- [x] Disjointness proof | DOD: for fixed `stride >= 1` and fixed `offset`, `workIndexA != workIndexB` implies `(workIndexA * stride + offset) != (workIndexB * stride + offset)`, so state/debug writers do not collide. | Rejected: disabling safety without proof. | Estimate: no runtime speed claim.
- [x] CPU/dotnet gate after Loop 10 | DOD: `dotnet/csc` count was `0`, CPU load was `100%`; no CLI build launched. | Rejected: build under CPU > 50%. | Estimate: protects developer hardware.

## Loop 11 - Documentation Proof Sync

- [x] Rationale job-safety record | DOD: Decision 17 records the mapped-index safety problem, scoped annotations, rejected branch-only fallback, and hardware impact. | Rejected: leaving safety proof only in source comments. | Estimate: no runtime cost.
- [x] Route card and binary ledger safety boundary | DOD: both docs now state the injective mapping proof for strided `States`/`DebugForces` writers. | Rejected: accepting a global route without a durable writer-proof note. | Estimate: no runtime cost.
- [x] LOG forensic addendum | DOD: `LOG_SHINOBU_158.md` now has a strided job safety addendum with exact issue, fix, and no false speed claim. | Rejected: chat-only proof. | Estimate: no runtime cost.
- [x] Static scan and compile gate after Loop 11 | DOD: no direct `States[index] =` setter remains; forbidden owned-surface scan returned no matches; all four jobs retain deterministic Burst directives; targeted `git diff --check` reports only existing LF-to-CRLF warnings and no SHINOBU_158 whitespace errors. CPU gate is `100%` with `dotnet/csc=0`, so no build launched. | Rejected: starting compile under CPU > 50%. | Estimate: source-only proof, no runtime claim.

## Loop 12 - Emergency Mock Producer Gate

- [x] Mock overwrite risk fixed | DOD: cold boot now calls emergency mock generation only when tuning reports zero active state rows. | Rejected: unconditional 4096-row mock write that can overwrite a real producer's Vault state. | Estimate: no hot-path claim; cold boot avoids one mock schedule when live data exists.
- [x] Default active count corrected | DOD: `BuoyancyTuningDTO.Default()` starts at `ActiveStateCount = 0`, preserving CI fallback through `MockStateCount` without pretending real actors are live. | Rejected: defaulting active truth to 1000 before the mock owner actually writes rows. | Estimate: avoids false active window before mock or producer commit.
- [x] Static scan and compile gate after Loop 12 | DOD: mock-gate anchors found in source/docs; forbidden owned-surface scan returned no matches; targeted `git diff --check` reports only LF-to-CRLF warning in the shared binary ledger. CPU gate is `100%` with `dotnet/csc=0`, so no build launched. | Rejected: compile under CPU > 50%. | Estimate: source-only proof, no runtime claim.
