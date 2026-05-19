# LOG_SHINOBU_127

## 2026-05-19 - Intake Blocked

What was wrong: Active `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="SHINOBU_127">` block. CLI extraction by exact ID failed, and `Select-String` listed only SHINOBU_100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 111, 112, 113, 114, 115, 116, 117, 118, 119, and 120.

What was done: Read `AGENTS.md`, domain map, active batch prompt list, and relevant mandates for physics, damage, and cinematic fake-first constraints. Created required status and rationale files. No code was edited because the active XML task list and task count cannot be authenticated.

Cinematic Cheats used: None implemented. Future accepted route should replace projectile Rigidbody truth and Physics.Raycast hot-path truth with deterministic Vault-backed AABB sweep math, using pooled VFX/audio/haptic signals for presentation.

Exact Microseconds saved: 0 us measured. No runtime path changed. Potential savings from removing `Rigidbody` bullets and synchronous raycasts remain pending valid prompt and profiler proof.

Verification: Static intake only. No Unity import, Play Mode, profiler, GCMonitor, or dotnet build was run. Status remains BLOCKED BY DEPENDENCY.

## 2026-05-19 - Ultra Mandate Recheck

What was wrong: The second mandate demands a 20-task reconciliation, but the active batch file still lacks `<AGENT_PROMPT id="SHINOBU_127">`. The supplied `<ULTRA_THINK_POLISH_MANDATE agent_id="[YourID]">` is a polish mandate, not the missing agent assignment.

What was done: Re-read status, rationale, `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and re-ran exact CLI extraction against `Docs/Tasks/CURRENT_BATCH.md`. Extraction returned `MISSING: <AGENT_PROMPT id="SHINOBU_127">`.

Cinematic Cheats used: None implemented. Future authorized design remains a deterministic mathematical fake for projectile physics: swept AABB trajectory tests plus LUT penetration/ricochet and presentation-only impact signals, avoiding projectile `Rigidbody` and synchronous `Physics.Raycast`.

Exact Microseconds saved: 0 us measured. No runtime code changed.

Verification: Static file evidence only. No dotnet build was launched, matching the user's build restraint. Status remains BLOCKED BY DEPENDENCY.

## 2026-05-19 - Ballistics Vault Implementation

What was wrong: The authenticated `SHINOBU_127` block reappeared in `Docs/Tasks/CURRENT_BATCH.md` with 20 tasks. The owned flora projectile path was still built around a physical projectile component and damage-by-collision authority. The combat path had no owner-local Burst ballistic kernel, no 8x8 penetration LUT, no Vault-backed trajectory DTOs, no blackbox ballistics ring, and no designer facade for cold tuning.

What was done: Added `Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs` with explicit-layout DTOs, Vault buffer handles, double-buffered trajectory queues, `BallisticIntersectionJob`, `StageImpactVFXJob`, `BallisticsTelemetryJob`, deterministic mock firefight generation, cold CSV LUT ingestion, and telemetry dump support. Added `Assets/_Project/Scripts/Gameplay/Combat/BallisticsEditorFacade.cs` for layout verification, UI Toolkit tuning, and gizmos. Rewired `HostileFlora` and the legacy `FloraProjectile` facade to queue mathematical trajectories instead of relying on projectile physics. Extended `CombatDamageRuntime` to refresh target AABBs only when shots are pending, schedule/finalize ballistics, and route hits through the existing unmanaged `CombatDamageSignal` lane.

Cinematic Cheats used: Penetration is an 8x8 LUT, not material fracture. Impact visuals are staged as unmanaged matrices and material hashes, not instantiated bullet holes. Low quality collapses non-root AABB admission and ricochet budget through continuous math, not a binary device switch. Physical bullets are treated as a designer-facing illusion; authoritative truth is a flat Vault trajectory plus AABB slab math.

Exact Microseconds saved: 0 us measured in Unity Profiler in this turn. Expected savings are from removing per-shot PhysX body integration, collision callbacks, and engine ray queries from the owned flora/ballistics path. Exact frame-time proof remains pending Unity runtime profiling; the code records solver microseconds into the 300-frame telemetry ring and dumps if it exceeds 500 us.

Verification: Static scans found no `Rigidbody`, `OnCollisionEnter`, `Physics.Raycast`, `.linearVelocity`, or `Instantiate(` in the owned patched ballistic/flora fire path. Static scans found no LINQ/foreach/UnityEngine.Random/Time.deltaTime/native container hot-path allocation markers in `BallisticsRuntime.cs`. `git diff --check` passed with only CRLF normalization warnings. Full `dotnet build Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1` is blocked by unrelated Visor and Somatic missing types in `Hecton8.Core.csproj`. Narrow owner verification passed: `dotnet build Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 -p:BuildProjectReferences=false` returned 0 warnings and 0 errors.

<SELF_AUDIT agent_id="SHINOBU_127" domain="ARMOR_PENETRATION_BALLISTICS_EXPERT">
  <task_reconciliation>
    <task id="01" status="PASS">Rigidbody bullet authority removed from the owned flora projectile path. `HostileFlora` queues a trajectory; `FloraProjectile` is a compatibility facade that despawns after queuing.</task>
    <task id="02" status="PASS">Owned combat hit path uses mathematical Vault trajectories and AABB primitives. No owned `Physics.Raycast` combat hitscan call remains in the patched fire path.</task>
    <task id="03" status="PASS">Ballistic hot DTOs are explicit-layout public-field structs. No hot-path `get; set;` DTO properties were added.</task>
    <task id="04" status="PASS">Editor verifier checks `BallisticTrajectoryDTO` size and field offsets with `UnsafeUtility`.</task>
    <task id="05" status="PASS">`GenerateMockBallisticsJob` injects deterministic mock trajectories and mock AABB targets into Vault buffers for isolated solver stress.</task>
    <task id="06" status="PASS">`BallisticIntersectionJob` uses raw pointers, target-local AUP deltas, inverse primitive rotation, and slab AABB intersection.</task>
    <task id="07" status="PASS">Hydrodynamic drag uses `v_final = v_initial * exp(-DragCoefficient * distance)` and kills sub-lethal trajectories.</task>
    <task id="08" status="PASS">Penetration uses a flat 8x8 `NativeArray<float>` LUT keyed by weapon class and material class.</task>
    <task id="09" status="PASS">Ricochet uses incidence dot, reflected direction, friction loss, and continuous quality-driven bounce budget from 0 to 3.</task>
    <task id="10" status="PASS">Lethal hits emit unmanaged `CombatDamageSignal` through `GlobalSignals.DamageSignalWriter`; no direct health mutation was added.</task>
    <task id="11" status="PASS">AABB precision scales continuously: root primitives always admit, non-root primitives pass through a `smoothstep(GlobalQualityWeight)` hash gate.</task>
    <task id="12" status="PASS">Combat domain writes deferred impact VFX DTOs to Vault. Direct `GraphicsBuffer` upload remains presentation ownership to preserve compile-wall routing.</task>
    <task id="13" status="PASS">Hit reporting restores absolute `double3` hit AUP from primitive center AUP plus local hit offset.</task>
    <task id="14" status="PASS">Ballistic jobs use `FloatMode.Deterministic` for rollback-compatible authoritative hit truth.</task>
    <task id="15" status="PASS">Overwritten staging buffers use `NativeArrayOptions.UninitializedMemory` when requested from the Vault.</task>
    <task id="16" status="PASS">A 300-entry `BallisticsTelemetryEntry` ring and `Dump_BALLISTICS_SURGEON.bin` path exist for NaN or over-budget solver frames.</task>
    <task id="17" status="PASS">Editor-only UI Toolkit `Abyssal Ballistics Tuner` edits Vault-backed tuning and exposes mock/CSV/layout controls.</task>
    <task id="18" status="PASS">Cold CSV ingestion reads bytes, parses via span logic, hashes ASCII tokens with FNV-1a, and mutates the flat LUT.</task>
    <task id="19" status="PASS">Editor gizmo reads Vault trajectories, hits, and AABB primitives to draw live debug lines and wire boxes.</task>
    <task id="20" status="PASS">Self-audit, static scans, owned compile verification, byte layout notes, Vault IDs, and dependency boundary are recorded here.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <BallisticTrajectoryDTO size_bytes="64">
      <field offset="0" size="24">double3 OriginAUP</field>
      <field offset="24" size="12">float3 Direction</field>
      <field offset="36" size="4">float Velocity</field>
      <field offset="40" size="4">float Mass</field>
      <field offset="44" size="4">uint WeaponHash</field>
      <field offset="48" size="4">uint SourceEntityID</field>
      <field offset="52" size="4">uint Flags</field>
      <field offset="56" size="4">uint _pad0</field>
      <field offset="60" size="4">uint _pad1</field>
      <math>24 + 12 + 4 + 4 + 4 + 4 + 4 + 4 + 4 = 64 bytes. Alignment target: one 64-byte L1 cache line.</math>
    </BallisticTrajectoryDTO>
    <AABBPrimitiveDTO size_bytes="96">
      <field offset="0" size="24">double3 CenterAUP</field>
      <field offset="24" size="12">float3 HalfExtents</field>
      <field offset="36" size="4">uint TargetEntityID</field>
      <field offset="40" size="16">quaternion Rotation</field>
      <field offset="56" size="4">uint MaterialHash</field>
      <field offset="60" size="4">uint PrimitiveHash</field>
      <field offset="64" size="4">uint Flags</field>
      <field offset="68" size="4">float DamageMultiplier</field>
      <field offset="72" size="4">float ArmorScalar</field>
      <field offset="76" size="4">uint _pad0</field>
      <field offset="80" size="8">ulong _pad1</field>
      <field offset="88" size="8">ulong _pad2</field>
      <math>24 + 12 + 4 + 16 + 4 + 4 + 4 + 4 + 4 + 4 + 8 + 8 = 96 bytes. Multiple of 16, no Pack=1.</math>
    </AABBPrimitiveDTO>
    <BallisticsCountersDTO size_bytes="64">Explicit 64-byte counter DTO used to avoid false sharing on aggregate counters.</BallisticsCountersDTO>
  </struct_layout_verification>
  <scalability_curve>
    GlobalQualityWeight is clamped and smoothed with polynomial math. Under 0.3, ricochet budget collapses to 0 or near-zero, non-root primitive admission is suppressed by the hash gate, and the solver spends work on root AABBs plus one drag/penetration evaluation. Mid quality admits a deterministic subset and one bounce. High admits most primitives and two bounces. Ultra admits all registered primitives and three bounces, then stages richer impact VFX data instead of simulating physical bullets.
  </scalability_curve>
  <h_phi_vault_status>
    <private_persistent_arrays>0 NativeArray, NativeList, or NativeHashMap persistent private fields were added. Persistent state is routed through VaultBufferHandle fields.</private_persistent_arrays>
    <vault_ids>TrajectoriesA=71270, TrajectoriesB=71271, AabbPrimitives=71272, HitResults=71273, PenetrationLut=71274, TelemetryRing=71275, Counters=71276, Tuning=71277, ImpactVfx=71278, CsvScratch=71279.</vault_ids>
  </h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>
    <noalias>GenerateMockBallisticsJob, BallisticIntersectionJob, StageImpactVFXJob, and BallisticsTelemetryJob mark separate NativeArray fields with `NoAlias` where applicable.</noalias>
    <graph>CombatDamageRuntime.FrameTick refreshes target AABBs only if trajectories are pending, then BallisticsRuntime schedules Intersection -> StageImpactVFX -> Telemetry. LateFrameTick finalizes only completed work through DispatcherJobSwap; teardown is the only forced completion path.</graph>
  </pointer_aliasing_and_dependency_graph>
  <compile_guard>
    No core enum expansion was made. The domain uses local numeric BufferID casts and existing signal/Vault routes. No new sibling runtime assembly reference or asmdef dependency was introduced by SHINOBU_127.
  </compile_guard>
  <dear_lie_confirmation>
    Before: projectile GameObject plus PhysX body/collision authority has engine broadphase, solver, callback, and tunneling nondeterminism per shot. After: flat trajectory DTOs run O(T * P_admitted * (1 + R)) where P_admitted is quality-gated and R is 0..3; material response is O(1) LUT lookup. Visual impact truth is staged, not physically simulated.
  </dear_lie_confirmation>
  <verification_boundary>
    Full project build is not green because unrelated Visor/Somatic DTO dependencies are missing outside this domain. Owner-local `Assembly-CSharp.csproj` verification with `BuildProjectReferences=false` is green. Unity runtime profiling and GCMonitor proof were not executed in this turn.
  </verification_boundary>
</SELF_AUDIT>

## 2026-05-19 - Ultra Polish Pass 2

What was wrong: The first implementation compiled owner-locally but still exposed job-safety and pressure-control risks. The editor gizmo could read hit/primitive buffers while the solver job owned them. Target AABB refresh could run before a completed solve was non-blockingly finalized, leaving moving target boxes stale for the next solve. Impact VFX staging used local hit coordinates as matrix translation. Signal emission into `CombatDamageSignal` was bounded only by hit count, not by quality pressure. Primitive tombstoning lacked a Vault mutation guard. Active-buffer telemetry identified the post-swap write buffer instead of the solve read buffer. `DamageWriter` had an unnecessary `NativeDisableContainerSafetyRestriction`.

What was done: Added `PrepareFrameForTargetRefresh()` and routed `CombatDamageRuntime.FrameTick` through it before AABB refresh. Made `TryGetDebugBuffers` fail closed if a solver job is still scheduled. Added continuous quality-based `SignalEmitBudget` from 16 to 128 signals per solve and a `SignalDropped` hit flag. Updated telemetry to count emitted and dropped signals separately. Converted impact VFX matrices from `HitAUP - PresentationOriginAUP` instead of local target coordinates. Wrapped primitive tombstoning in the Vault mutation guard. Corrected active-read buffer telemetry. Removed the unsafe container safety suppression from the damage signal writer.

Cinematic Cheats used: Damage truth remains a flat trajectory plus AABB slab/LUT/ricochet math path. Low-tier polish now drops excess signal presentation pressure instead of simulating every impact callback. Impact visuals remain staged DTOs for a presentation owner rather than bullet-hole GameObjects or direct GPU upload from combat.

Exact Microseconds saved: 0 us measured in Unity Profiler. The concrete protected costs are hidden main-thread job completions, unbounded signal queue growth, and invalid debug buffer reads. The solver telemetry still records microseconds and dumps when it crosses 500 us.

Verification: Static scans after the polish pass found no `Rigidbody`, `OnCollisionEnter`, `Physics.Raycast`, `.linearVelocity`, or `Instantiate(` in the owned ballistic/flora fire path, and no LINQ/foreach/UnityEngine.Random/Time.deltaTime/native container allocation markers in `BallisticsRuntime.cs`. `git diff --check` on owned runtime files passed with only CRLF normalization warnings. CPU gate was 45.9% with no `dotnet`/`csc` process. Narrow owner verification passed: `dotnet build Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 -p:BuildProjectReferences=false` returned 0 warnings and 0 errors in 37.22s. Full project build remains blocked by unrelated Visor/Somatic missing DTOs in `Hecton8.Core.csproj`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_2">
  <task_reconciliation>Tasks 01-20 remain PASS. Loop 6 hardened Task 10 damage signal pressure, Task 12 VFX staging coordinates, Task 16 telemetry signal accounting, Task 19 debug read safety, and Task 20 dependency/mutation proof.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed in this pass. `BallisticTrajectoryDTO` remains 64 bytes with XML-required offsets. `AABBPrimitiveDTO` remains 96 bytes, 16-byte aligned. `BallisticsCountersDTO` remains 64 bytes for false-sharing isolation.</struct_layout_verification>
  <scalability_curve>Below 0.3 quality, the signal budget trends to 16, ricochet budget collapses, and non-root primitive admission is suppressed. Above that, budget and primitive fidelity rise continuously toward 128 signals and full registered primitive evaluation.</scalability_curve>
  <h_phi_vault_status>No private persistent NativeArray, NativeList, or NativeHashMap fields were introduced. New polish paths reuse existing Vault handles and mutation guards.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Debug reads are denied while the job graph owns buffers. Frame graph is now PrepareFrameForTargetRefresh -> RefreshBallisticTargetAabbs -> BallisticsRuntime.FrameTick -> CombatDamageSignal drain -> LateFrameTick non-blocking finalize.</pointer_aliasing_and_dependency_graph>
  <compile_guard>Still no core enum expansion, sibling asmdef reference, or cross-domain DTO repair. Full build failure remains outside SHINOBU_127 ownership.</compile_guard>
  <dear_lie_confirmation>Overflow hit presentation is deliberately dropped under low quality; authoritative combat remains bounded and deterministic instead of expanding into callback/object simulation.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 4

What was wrong: The Vault primitive table could leak capacity during long target churn. `TombstonePrimitivesForTarget` cleared primitive flags, but `RegisterAabbPrimitiveFromRuntime` appended new slots instead of reusing inactive ones. Mock firefight data lived around AUP zero, so it did not prove the floating-origin subtraction path under shifted world origins. Runtime half extents trusted caller sign. The intersection loop always entered inverse-rotation/slab math for admitted primitives even when a conservative reach test could reject them first.

What was done: Added inactive-slot reuse in `RegisterAabbPrimitiveFromRuntime`, preserving primitive capacity without array compaction. Sanitized registered half extents with `math.abs` before min clamping. Passed `MockOriginAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble` into `GenerateMockBallisticsJob` and placed mock AABB/trajectory AUPs around the current floating origin. Added a conservative bounding-sphere early reject before exact rotated AABB slab intersection.

Cinematic Cheats used: The broadphase sphere is a Dear Lie only for rejection. It never reports a hit and cannot replace the exact rotated AABB slab; it just avoids paying exact math for primitives outside trajectory reach. The mock firefight now fakes a dense combat scene at the current AUP without spawning projectiles or requiring AI/player systems.

Exact Microseconds saved: 0 us measured in Unity Profiler. Expected protected costs are quaternion inverse/slab setup on far misses and long-session Vault primitive exhaustion from unregister/register churn. Runtime telemetry still owns actual solve microsecond proof.

Verification: Static scans after this pass found no `Rigidbody`, `OnCollisionEnter`, `Physics.Raycast`, `.linearVelocity`, or `Instantiate(` in the owned ballistic/flora fire path. Hot-path scans found no LINQ/foreach/UnityEngine.Random/Time.deltaTime/native container allocation markers in `BallisticsRuntime.cs`. `git diff --check` passed with CRLF normalization warnings only. CPU gate was 26.5% with no `dotnet`/`csc` process. Narrow owner verification passed: `dotnet build Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 -p:BuildProjectReferences=false` returned 0 warnings and 0 errors in 31.64s. Full project build remains blocked by unrelated Visor/Somatic missing DTOs in `Hecton8.Core.csproj`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_4">
  <task_reconciliation>Tasks 01-20 remain PASS. Loop 8 hardens Task 05 AUP-valid mock firefight, Task 06 primitive broadphase pruning, Task 11 continuous primitive pressure, Task 13 floating-origin proof, and Task 20 long-session Vault correctness.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed in this pass. `BallisticTrajectoryDTO` remains 64 bytes with XML offsets. `AABBPrimitiveDTO` remains 96 bytes. `BallisticHitResultDTO` remains 112 bytes. Counter/telemetry DTOs remain 64 bytes.</struct_layout_verification>
  <scalability_curve>Below 0.3 quality, non-root primitive admission remains suppressed and the new conservative sphere reject cuts obvious misses before rotated slab math. Higher quality admits more primitives continuously but still gets the cheap rejection before exact AABB truth.</scalability_curve>
  <h_phi_vault_status>No new private persistent native allocations were introduced. Inactive primitive slots are reused inside the existing `AabbPrimitives=71272` Vault buffer.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>No new job dependency branch was added. The early reject runs inside `BallisticIntersectionJob` before exact slab math; existing `NoAlias` field annotations and job graph remain unchanged.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling asmdef reference, core enum edit, or cross-domain DTO repair was introduced. Owner-local Assembly-CSharp compile remains green; full build wall remains outside SHINOBU_127.</compile_guard>
  <dear_lie_confirmation>Sphere broadphase is a mathematically safe fake for miss rejection only. Final hit identity, damage, drag, ricochet, and AUP reporting remain exact AABB/LUT math.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 3

What was wrong: Signal emission was still coupled to the parallel intersection kernel. That made NativeQueue enqueue order scheduler-dependent and made the quality budget reject hits by trajectory index instead of by actual emitted hit count. The hit result DTO did not carry final post-ricochet impact direction, so a later deterministic signal pass could not preserve exact impulse direction without expanding the payload. The editor tuner readout formatted telemetry through managed string concatenation and `ToString`. The debug gizmo could pair the current write trajectory buffer with stale solved hit results.

What was done: Added `ImpactDirection` to `BallisticHitResultDTO` and expanded it to an explicit 112-byte layout. Removed SignalBus writes from `BallisticIntersectionJob`. Added `EmitBallisticDamageSignalsJob`, a deterministic Burst `IJob` that scans hit results in trajectory order, emits up to the continuous quality budget, and marks overflow hits with `SignalDropped`. Reordered the job graph to Intersection -> EmitDamageSignals -> StageImpactVFX -> Telemetry. Updated the layout verifier to check `BallisticHitResultDTO=112B` and `ImpactDirection` offset 48. Updated debug buffer resolution to prefer the last solved trajectory buffer/count. Reworked the UI Toolkit tuner telemetry readout to disabled numeric fields updated with `SetValueWithoutNotify`, removing our per-refresh string formatting.

Cinematic Cheats used: The combat truth remains mathematical AABB + LUT + ricochet. The signal budget is now a deterministic load-shed fake: low-quality frames do not simulate or route every possible damage presentation event; they route the first stable hit subset and preserve forensic flags for dropped signals.

Exact Microseconds saved: 0 us measured in Unity Profiler. The pass trades a bounded 4096-entry linear signal scan for deterministic ordering and removal of parallel queue ordering risk. Runtime solver telemetry still records microseconds and dumps at >500 us.

Verification: Static forbidden scans remained clean for the owned ballistic/flora fire path. Static hot-path scans found no LINQ/foreach/UnityEngine.Random/Time.deltaTime/native allocation markers in `BallisticsRuntime.cs`. `git diff --check` passed for the touched ballistics files. CPU gate was 31% with no `dotnet`/`csc` process. Narrow owner verification passed: `dotnet build Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 -p:BuildProjectReferences=false` returned 0 warnings and 0 errors in 27.83s. Full project build remains blocked by unrelated Visor/Somatic missing DTOs in `Hecton8.Core.csproj`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_3">
  <task_reconciliation>Tasks 01-20 remain PASS. Loop 7 hardens Task 10 deterministic damage signal emission, Task 14 rollback ordering, Task 17 zero-GC facade intent, Task 19 solved-frame debug correctness, and Task 20 DTO layout proof.</task_reconciliation>
  <struct_layout_verification>
    <BallisticHitResultDTO size_bytes="112">
      <field offset="0" size="24">double3 HitAUP</field>
      <field offset="24" size="12">float3 LocalHitPoint</field>
      <field offset="36" size="12">float3 Normal</field>
      <field offset="48" size="12">float3 ImpactDirection</field>
      <field offset="60" size="4">float Damage</field>
      <field offset="64" size="4">float RemainingVelocity</field>
      <field offset="68" size="4">float Distance</field>
      <field offset="72" size="4">uint TargetEntityID</field>
      <field offset="76" size="4">uint SourceEntityID</field>
      <field offset="80" size="4">uint WeaponHash</field>
      <field offset="84" size="4">uint MaterialHash</field>
      <field offset="88" size="4">uint Flags</field>
      <field offset="92" size="4">uint Frame</field>
      <field offset="96" size="4">uint RicochetCount</field>
      <field offset="100" size="4">uint PrimitiveHash</field>
      <field offset="104" size="8">ulong _pad0</field>
      <math>24 + 12 + 12 + 12 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 8 = 112 bytes. Multiple of 16, no Pack=1.</math>
    </BallisticHitResultDTO>
  </struct_layout_verification>
  <scalability_curve>Below 0.3 quality, valid damage signal emission is budgeted near 16 deterministic hits; higher quality rises continuously toward 128. Primitive admission and ricochet budgets remain continuous and independent of any binary hardware switch.</scalability_curve>
  <h_phi_vault_status>No new private persistent native allocations were introduced. The larger hit result DTO still lives in the existing Vault `HitResults=71273` buffer.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Intersection writes HitResults, EmitDamageSignals mutates only hit flags and SignalBus output, StageImpactVFX reads post-emission HitResults, Telemetry reads final flags. No parallel signal writer ordering is part of rollback truth now.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling asmdef reference or core enum edit was introduced. Owner-local Assembly-CSharp compile is green; full build wall remains outside SHINOBU_127.</compile_guard>
  <dear_lie_confirmation>Signal overflow is a deterministic quality fake, not an attempt to physically process every impact under thermal pressure. Mathematical hit truth remains available in the hit buffer and blackbox counters.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>
