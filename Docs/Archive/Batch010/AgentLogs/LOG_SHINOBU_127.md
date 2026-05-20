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

## 2026-05-19 - Ultra Polish Pass 5

What was wrong: Static ballistics counters could survive a DataVault replacement during tests/bootstrap and falsely describe the new buffer set. Impact VFX staging had no bounded accessor, inviting future presentation code to scan stale entries beyond the current solve. The CSV parser hashed token names but previously used positional row/column order as the real authority. The editor layout verifier did not cover VFX/tuning DTOs.

What was done: `EnsureInitialized` now detects Vault instance changes, force-finalizes old jobs, unlocks old buffers, and resets transient counters before acquiring new handles. `Shutdown` routes through the same transient reset. Added `TryGetImpactVfxStaging` with `frame` and bounded `stagingCount`. Reworked CSV LUT ingestion to map weapon rows and material headers by FNV-1a hash constants and fixed CRLF delimiter skipping. Expanded `BallisticsLayoutVerifier` to check `BallisticImpactVfxDTO=80B` and `BallisticsTuningDTO=64B` offsets.

Cinematic Cheats used: The VFX stage remains a bounded unmanaged DTO stream, not bullet-hole GameObjects or combat-owned GPU upload. CSV remains a cold human-tuning source; runtime truth is still the flat 8x8 LUT in Vault.

Exact Microseconds saved: 0 us measured in Unity Profiler. Expected protected cost is avoiding full-buffer stale presentation scans and avoiding per-frame clear work. The CSV change is cold-path correctness, not a hot-path speed claim.

Verification: Owned ballistic/flora static scans remained clean for `Rigidbody`, `OnCollisionEnter`, `Physics.Raycast`, `.linearVelocity`, `Instantiate(`, LINQ/foreach/UnityEngine.Random/Time.deltaTime/native hot allocation markers, DTO properties, `Pack=1`, and hot arrays of interfaces. `git diff --check` passed with CRLF normalization warnings only. Build was initially blocked by CPU at 82.9%; after CPU dropped to 30.9% and no `dotnet`/`csc` process existed, `dotnet build Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 -p:BuildProjectReferences=false` passed with 0 warnings and 0 errors in 24.16s. Full project build remains blocked by unrelated Visor/Somatic missing DTOs in `Hecton8.Core.csproj`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_5">
  <task_reconciliation>Tasks 01-20 remain PASS. Loop 9 hardens Task 12 VFX staging ownership, Task 17 editor verifier completeness, Task 18 CSV name/hash authority, Task 20 lifecycle/aliasing proof, and Vault sovereignty across DataVault swaps.</task_reconciliation>
  <struct_layout_verification>`BallisticImpactVfxDTO` is now editor-verified as 80 bytes with `Matrix` at offset 0 and `MaterialHash` at offset 64. `BallisticsTuningDTO` is editor-verified as 64 bytes with `Revision` at offset 44. Existing trajectory/AABB/hit/telemetry/counter checks remain.</struct_layout_verification>
  <scalability_curve>Low quality can consume only the bounded VFX staging count and skip stale entries. Higher quality can consume the same deterministic staging DTOs for richer impact rendering without changing hit truth.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocations were introduced. Transient static counters are now reset on Vault replacement; persistent buffers remain VaultBufferHandles only.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Vault replacement path completes old work before resetting handles. VFX presentation now has a count/frame read contract, so readers do not need to infer live entries by scanning the entire buffer.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling asmdef reference, core enum edit, or cross-domain DTO repair was introduced. Owner-local Assembly-CSharp compile remains green; full build wall remains outside SHINOBU_127.</compile_guard>
  <dear_lie_confirmation>CSV authoring is a cold control surface that hydrates the LUT. Runtime penetration remains O(1) table lookup; presentation remains staged DTOs rather than physical decals/projectiles.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 6

What was wrong: The CSV loader could accept a file truncated at `CsvScratchBytes`, and the parser wrote directly into the live Vault LUT while parsing. That made a malformed or oversized designer file capable of leaving partial armor-penetration truth in memory.

What was done: `TryLoadPenetrationCsv` now fails closed when the file exceeds the Vault scratch buffer. `ApplyPenetrationCsvBytes` now parses into a 64-float stack transaction buffer, validates a complete 8-row matrix, validates all 8 named header columns when a header is present, rejects malformed header/data tokens, and commits the LUT only after validation passes. `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` now records SHINOBU_127 Vault IDs `71270..71279`, the 64-byte primary DTO stride, CSV authority, and the pending proof boundary.

Cinematic Cheats used: No physical material simulation was added. Runtime penetration remains the Dear Lie: a validated 8x8 scalar LUT, one indexed load per hit.

Exact Microseconds saved: 0 us measured. This is cold-path correctness and corruption prevention, not a hot-loop speed claim. Hot ballistic solver math and VFX staging were not changed.

Verification: Focused owned projectile path scan found no `Rigidbody`, `OnCollisionEnter`, `Physics.Raycast`, `.linearVelocity`, or `Instantiate(` in `BallisticsRuntime`, `BallisticsEditorFacade`, `HostileFlora`, or `FloraProjectile`. Ballistics hot-path scan found no LINQ/foreach/UnityEngine.Random/Time.deltaTime/native hot allocation markers. DTO scan found no `get; set;`, `get; private set;`, `Pack=1`, or arrays of interfaces in owned ballistics/flora files. CSV FNV-1a constants were verified against the actual seed CSV token set. `git diff --check` passed with CRLF normalization warnings only. Owner-local compile is pending because the CPU gate read 65% and later 66-68%; the project forbids launching `dotnet build` above 50% CPU; no `dotnet`/`csc` process was active.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_6">
  <task_reconciliation>Task 18 remains PASS but was hardened: CSV ingest is now transactional and fail-closed. Tasks 01-17 and 19-20 are unchanged by this cold-path patch.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed in this pass. Primary layout proof remains `BallisticTrajectoryDTO=64B`, `AABBPrimitiveDTO=96B`, `BallisticHitResultDTO=112B`, `BallisticImpactVfxDTO=80B`, `BallisticsTuningDTO=64B`, telemetry/counters 64B.</struct_layout_verification>
  <scalability_curve>Runtime quality curves are unchanged: low-to-ultra all consume the same validated LUT; high-tier visual richness still comes from staged VFX, not heavier material simulation.</scalability_curve>
  <h_phi_vault_status>No private persistent allocations were introduced. The parser uses Vault CSV scratch plus a 64-float stack transaction buffer and commits into Vault `PenetrationLut` only after validation. Stable ledger entry records Vault IDs `71270..71279`.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>No job graph changed. CSV loading remains rejected while `_jobScheduled` is true, so it cannot mutate LUT during active solver reads.</pointer_aliasing_and_dependency_graph>
  <compile_guard>Owner-local compile not rerun yet: CPU gate 65%. Full build wall remains unrelated Visor/Somatic DTO dependencies.</compile_guard>
  <dear_lie_confirmation>The ballistic material system is still O(1) LUT math. Oversized or malformed human CSV now fails closed instead of partially rewriting combat truth.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 7

What was wrong: `BallisticsRuntime.cs` carried an unnecessary `using Hecton8.World;` even though the owned runtime only needs `HectonFloatingOrigin`, which is in `Hecton8.Core`. That weakens compile-wall evidence if this combat runtime is later isolated into an asmdef.

What was done: Removed the unused World import from the owned ballistics runtime. Left existing `CombatDamageRuntime`/`HostileFlora` world references alone because those are pre-existing gameplay/player-target routes, not projectile authority or the new Burst ballistics kernel.

Cinematic Cheats used: None added. This pass is dependency hygiene only; the Dear Lie remains the 8x8 penetration LUT and unmanaged VFX staging.

Exact Microseconds saved: 0 us measured or claimed. The gain is compile-boundary evidence, not frame-time.

Verification: `rg` confirmed no `using Hecton8.World;` remains in `BallisticsRuntime.cs` or the editor facade. `git diff --check` on owned files passed with CRLF normalization warnings only. Owner-local compile remains pending because CPU gate read 74%; no `dotnet`/`csc` process was active, but the rule still forbids launching `dotnet build` above 50% CPU.

Build gate follow-up: CPU later dropped to 25% with no active `dotnet`/`csc`, so the narrow owner-local build was launched. It failed before SHINOBU-owned code with CS2001 for missing unrelated archive files still referenced by `Assembly-CSharp.csproj`: `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`. `git status --short` reports both as deleted. I did not restore those files or edit `Assembly-CSharp.csproj` because that would revert/touch unrelated work outside the ballistics domain.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_7">
  <task_reconciliation>Tasks 01-20 remain implemented. This pass specifically tightens Phase 1 compile-wall evidence without changing projectile authority or hit math.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Existing explicit sizes and offsets remain the active proof surface.</struct_layout_verification>
  <scalability_curve>No quality curve changed; low-to-ultra runtime behavior stays governed by `GlobalQualityWeight` in primitive admission, ricochet budget, signal budget, and VFX staging.</scalability_curve>
  <h_phi_vault_status>No allocation route changed. Ballistics persistent state remains Vault handles `71270..71279`.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>No job graph changed. No new aliasing surface was introduced.</pointer_aliasing_and_dependency_graph>
  <compile_guard>Owned runtime no longer imports `Hecton8.World`; AUP access comes from Core `HectonFloatingOrigin`. Owner-local compile proof is blocked by unrelated missing archive files referenced by `Assembly-CSharp.csproj`, not by SHINOBU_127 code.</compile_guard>
  <dear_lie_confirmation>No physical projectile or material simulation was reintroduced. The runtime remains mathematical trajectories plus LUT penetration.</dear_lie_confirmation>
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

## 2026-05-19 - Ultra Polish Pass 8

What was wrong: Compile proof is currently blocked outside SHINOBU ownership, so the remaining useful verification had to be narrowed to static owner-surface evidence. A broad grep pass produced third-party/package/doc noise and was not valid proof for the owned projectile path.

What was done: Re-ran explicit-path scans over `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `HostileFlora.cs`, and `FloraProjectile.cs`. Verified no projectile `Rigidbody`, collision callback, `Physics.Raycast`, `.linearVelocity`, or `Instantiate(` remains in the owned ballistic/flora fire path. Verified `BallisticsRuntime.cs` has no hot LINQ/foreach/UnityEngine.Random/Time.deltaTime/native collection allocation markers. Verified the editor facade has no checked `string.Format`, `ToString`, `foreach`, native allocation, PhysX, or projectile-instantiation markers. Re-checked the dependency wall: `Assembly-CSharp.csproj` still includes the deleted unrelated files `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`.

Cinematic Cheats used: No new runtime fake was added. The active Dear Lie remains the flat 8x8 penetration LUT, deterministic ricochet budget, conservative AABB miss rejection, and deferred unmanaged impact VFX staging instead of physical projectile bodies or CPU decal GameObjects.

Exact Microseconds saved: 0 us measured or claimed in this pass. Static proof only. Expected protected cost remains removal of per-shot PhysX body/collision/raycast authority and bounded low-quality hit routing.

Verification: Targeted owner scans returned no matches for the forbidden projectile/hot-path patterns. Burst graph inspection remains `BallisticIntersectionJob -> EmitBallisticDamageSignalsJob -> StageImpactVFXJob -> BallisticsTelemetryJob`; jobs carry deterministic Burst flags and hot arrays use `[NoAlias]`. No `dotnet build` was rerun because the missing archive-file blocker is still present in the project file.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_8">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 12 adds static proof for Task 01, Task 02, Task 10, Task 14, Task 16, Task 17, and Task 20 under a compile dependency wall.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Explicit runtime layouts remain: trajectory 64B, AABB primitive 96B, hit result 112B, impact VFX 80B, tuning 64B, telemetry 64B, counters 64B.</struct_layout_verification>
  <scalability_curve>Below 0.3 quality, root-biased primitive admission, near-zero ricochet budget, and a 16-signal deterministic cap keep the solver cheap. Middle/high/ultra increase admitted primitives, signal budget, ricochet budget, and staged VFX richness continuously through `GlobalQualityWeight`.</scalability_curve>
  <h_phi_vault_status>No new private native allocations were introduced. Persistent state remains Vault handles `71270..71279`; static audit found no new `NativeArray`, `NativeList`, or `NativeHashMap` allocations in `BallisticsRuntime.cs`.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Intersection writes hit DTOs in parallel with `[NoAlias]`; serial EmitDamageSignals scans hit DTOs deterministically; StageImpactVFX reads final hit state; Telemetry records counters and dumps on NaN/over-budget. Normal path still uses non-blocking finalization.</pointer_aliasing_and_dependency_graph>
  <compile_guard>Owned runtime still has no `Hecton8.World` import. Owner-local compile cannot currently be proven again because unrelated deleted `_Archive` files remain referenced by `Assembly-CSharp.csproj`.</compile_guard>
  <dear_lie_confirmation>No projectile Rigidbody, hot combat raycast, or physical material fracture simulation was reintroduced. Hit truth remains Burst AABB + drag + LUT + deterministic ricochet.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 9

What was wrong: Cold/editor mutation routes still had weaker Vault lifetime discipline than the scheduled solver. Tuning writes, mock firefight generation, and CSV LUT ingest could resolve Vault buffers without explicit guard/lock coverage. That is not a hot gameplay bug by itself, but it is a bad precedent for a system that claims DataVault ownership proof.

What was done: Wrapped `WriteTuning` with the Vault mutation guard. Wrapped `GenerateMockBallistics` with mutation guard plus explicit locks for the active write trajectory buffer and `AabbPrimitives`; reset `_activeReadCount` after mock generation to prevent stale solved-frame debug display. Wrapped CSV ingest with locks for `CsvScratch` and `PenetrationLut`, used file-size fail-closed checks, read through a `Span<byte>` over Vault scratch, and applied the already-transactional LUT parser only under the mutation guard.

Cinematic Cheats used: No new visual fake was added. This pass preserves the existing fake stack: no projectile bodies, no combat raycasts, flat 8x8 penetration LUT, deterministic ricochet budget, conservative miss rejection, and deferred unmanaged VFX staging.

Exact Microseconds saved: 0 us measured or claimed. This is cold-path correctness hardening. Hot-frame cost is unchanged.

Verification: Explicit projectile-path scan over `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `HostileFlora.cs`, and `FloraProjectile.cs` returned no `Rigidbody`, collision callback, combat `Physics.Raycast`, `.linearVelocity`, or `Instantiate` hits. Runtime hot-path scan returned only `handle.Complete()` in the cold/manual mock path and forced teardown/reset completion. `git diff --check` passed with CRLF normalization warnings only. Compile proof was not rerun because `Assembly-CSharp.csproj` still references missing unrelated files `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`; both `Test-Path` checks are false.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_9">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 13 hardens Task 05 mock firefight, Task 17 tuning facade, Task 18 CSV LUT ingest, and Task 20 Vault proof without changing gameplay hit truth.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Explicit layouts remain: trajectory 64B, AABB primitive 96B, hit result 112B, impact VFX 80B, tuning 64B, telemetry 64B, counters 64B.</struct_layout_verification>
  <scalability_curve>Quality behavior is unchanged: below 0.3 the solver remains root-biased with low signal/ricochet budget; middle/high/ultra continuously admit more primitives, signals, ricochets, and VFX staging through `GlobalQualityWeight`.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocations were introduced. Cold mutation routes now explicitly guard or lock Vault buffers `TrajectoriesA/B`, `AabbPrimitives`, `PenetrationLut`, `Tuning`, and `CsvScratch` before mutation.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>No gameplay job graph changed. Normal path remains Intersection -> EmitDamageSignals -> StageImpactVFX -> Telemetry. Cold mock generation is explicitly documented as a manual sync job, not a frame-tick dependency.</pointer_aliasing_and_dependency_graph>
  <compile_guard>Owned runtime still avoids direct sibling-domain imports. New compile proof is blocked by unrelated missing `_Archive` files in `Assembly-CSharp.csproj`, not by SHINOBU_127 code.</compile_guard>
  <dear_lie_confirmation>No physical bullet, hot combat raycast, or material fracture simulation was reintroduced. Ballistic truth remains Burst AABB + hydrodynamic drag + LUT penetration + deterministic ricochet.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 10

What was wrong: The conservative AABB broadphase still used a square root to turn `radiusSq` into `radius`, then used that value only for segment range rejection. That is unnecessary ALU in the primitive miss path. The solver Vault lock chain also needed stronger rollback discipline: if lock acquisition failed midway, a blanket unlock path could touch locks this call never acquired.

What was done: Rechecked the current SHINOBU_127 XML from `CURRENT_BATCH.md`, re-read the active architecture ledger and task-relevant mandates, then audited the owned ballistics files. The broadphase is now sqrt-free: behind-segment and past-segment rejection compare squared projection/excess distance against `radiusSq`, and perpendicular rejection stays squared before exact rotated slab math. `TryLockSolverBuffers` tracks each lock acquisition in its own boolean and only unlocks acquired buffers when the chain fails.

Cinematic Cheats used: No physical bullet simulation, combat `Physics.Raycast`, CPU decal GameObject, or material fracture path was introduced. The active Dear Lie remains mathematical AABB hit truth plus hydrodynamic scalar drag, an 8x8 penetration LUT, deterministic ricochet budget, conservative miss rejection, and deferred unmanaged impact VFX staging.

Exact Microseconds saved: 0 us measured in Unity Profiler. Static code evidence removes one `sqrt` from each admitted primitive miss broadphase path. The Vault lock rollback change is correctness hardening and does not claim frame-time savings.

Verification: Static owned-path scan found no `math.sqrt`, projectile `Rigidbody`, collision callback, combat `Physics.Raycast`, `.linearVelocity`, or `Instantiate(` in `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `HostileFlora.cs`, or `FloraProjectile.cs`. Hot-path scan found only the cold/manual mock `handle.Complete()` and forced teardown/reset completion. DTO property/`Pack=1` scan returned no matches. `git diff --check` reported CRLF normalization warnings only. `dotnet build` was not launched because the user explicitly restricted build use and `Assembly-CSharp.csproj` still references deleted unrelated `_Archive` files.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_10">
  <task_reconciliation>
    <task id="01" status="PASS">Physical projectile authority remains removed from the owned flora fire path.</task>
    <task id="02" status="PASS">No owned combat `Physics.Raycast` hit authority is present.</task>
    <task id="03" status="PASS">Hot DTOs use public fields and explicit unmanaged layouts, no hot properties.</task>
    <task id="04" status="PASS">Trajectory DTO remains explicitly 64B with editor offset verifier.</task>
    <task id="05" status="PASS">Mock firefight remains Vault-backed and cold/manual.</task>
    <task id="06" status="PASS">Burst AABB solver remains the hit truth path; broadphase is now sqrt-free.</task>
    <task id="07" status="PASS">Hydrodynamic drag remains analytical scalar attenuation.</task>
    <task id="08" status="PASS">Penetration remains a flat 8x8 LUT, not material fracture simulation.</task>
    <task id="09" status="PASS">Ricochet remains deterministic and quality-budgeted.</task>
    <task id="10" status="PASS">Damage remains routed through the unmanaged damage signal bridge, not direct health mutation.</task>
    <task id="11" status="PASS">AABB admission remains continuous via `GlobalQualityWeight`.</task>
    <task id="12" status="PASS">Impact presentation remains deferred unmanaged VFX staging.</task>
    <task id="13" status="PASS">AUP-local subtraction occurs before float slab math.</task>
    <task id="14" status="PASS">Jobs remain deterministic Burst due rollback authority.</task>
    <task id="15" status="PASS">Vault buffers remain requested with uninitialized memory where fully overwritten.</task>
    <task id="16" status="PASS">300-frame telemetry ring and dump path remain active.</task>
    <task id="17" status="PASS">Editor tuner remains the human-control facade.</task>
    <task id="18" status="PASS">CSV LUT ingest remains cold, span-based, and fail-closed.</task>
    <task id="19" status="PASS">Debug gizmo remains editor-only and reads Vault buffers without gameplay stalls.</task>
    <task id="20" status="PASS">This log appends the current proof boundary and failure modes.</task>
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
      <math>24 + 12 + 4 + 4 + 4 + 4 + 4 + 4 + 4 = 64 bytes. Exactly one 64B cache line; multiple of 16; no Pack=1.</math>
    </BallisticTrajectoryDTO>
  </struct_layout_verification>
  <scalability_curve>Below 0.3 quality, primitive admission remains root-biased, ricochet budget is near zero, signal budget is near 16, and the broadphase now avoids square roots before exact slab math. Middle/high/ultra continuously increase primitive admission, ricochet budget, signal budget, and VFX staging through `GlobalQualityWeight`.</scalability_curve>
  <h_phi_vault_status>No private persistent native arrays were introduced. Requested Vault IDs remain `71270 TrajectoriesA`, `71271 TrajectoriesB`, `71272 AabbPrimitives`, `71273 HitResults`, `71274 PenetrationLut`, `71275 TelemetryRing`, `71276 Counters`, `71277 Tuning`, `71278 ImpactVfx`, `71279 CsvScratch`.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Normal graph remains Intersection -> EmitDamageSignals -> StageImpactVFX -> Telemetry. Hot job fields retain `[NoAlias]`. Solver lock acquisition now rolls back only acquired Vault locks on failure.</pointer_aliasing_and_dependency_graph>
  <compile_guard>Owned ballistics runtime does not import `Hecton8.World` and no sibling asmdef reference was added. Current compile proof is blocked by unrelated deleted `_Archive` files still referenced by `Assembly-CSharp.csproj`; build was not launched in this pass.</compile_guard>
  <dear_lie_confirmation>The expensive path remains rejected: no projectile Rigidbody, no hot combat raycast, no material fracture. AABB math plus LUT penetration is O(T * admitted_P * ricochet_budget); the rejected physical projectile/material route would add PhysX solver/callback work and non-deterministic collision authority.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 11

What was wrong: The trajectory buffer helper name still described the solver phase incorrectly. `ResolveReadBufferId()` returned the current pending write buffer, which later becomes the active solver read buffer. That ambiguity sits next to Vault lock ownership and is an avoidable maintenance hazard. The queue/solver math also retained avoidable raw division and implicit square-root paths: `Vector3.magnitude`, `velocity / speed`, `1f / safeDirection`, a hot reciprocal constant written as division, and power-of-two mock-grid divides.

What was done: Renamed the private helper to `ResolveWriteBufferId()`, renamed the frame-local array to `solverTrajectories`, and renamed the partial-lock state to `trajectoryLocked`. Replaced velocity queue normalization with `math.lengthsq` + guarded `math.rsqrt`; replaced slab reciprocal with `math.rcp(safeDirection)` after signed epsilon selection; replaced LUT and primitive-admission reciprocal divisions with constants; replaced mock grid `/16` and `/128` with bit mask/shift extraction.

Cinematic Cheats used: No physical route was added. The active fake remains data-only ballistics: trajectory DTOs, AABB math, scalar drag, flat 8x8 LUT penetration, deterministic ricochet, and deferred impact VFX DTOs.

Exact Microseconds saved: 0 us measured in Unity Profiler. Static ALU cleanup removes one velocity magnitude sqrt path, one vector division in queue normalization, one explicit slab vector division, one primitive-admission reciprocal division, and two power-of-two integer divides from the owned code path. Runtime proof remains pending.

Verification: Static scans found no owned projectile `Rigidbody`, collision callback, combat `Physics.Raycast`, `.linearVelocity`, or `Instantiate(`. DTO scans found no hot property wrappers, `Pack=1`, or sequential runtime DTO layout markers. Hot-path scan reports only the cold/manual mock `handle.Complete()` and forced teardown/reset completion. Burst scan confirms all five jobs use deterministic Burst flags and `[NoAlias]` fields. Division scan now leaves the cold CSV parser division guarded by `math.max(divisor, 1f)` and an editor header string slash. `git diff --check` reports CRLF normalization warnings only. Build was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_11">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 15 hardens Task 05 mock arithmetic, Task 06 slab math, Task 11 primitive admission arithmetic, Task 14 deterministic math hygiene, and Task 20 proof naming.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Primary trajectory DTO remains 64B at offsets `0/24/36/40/44/48/52/56/60`; hit result remains 112B; counters and telemetry remain 64B.</struct_layout_verification>
  <scalability_curve>Below 0.3 quality, the compatibility queue now avoids `Vector3.magnitude`, primitive admission uses reciprocal multiplication, and mock profiling avoids power-of-two divides. Continuous quality behavior is unchanged: root-biased primitives and low signal/ricochet budget scale upward through `GlobalQualityWeight`.</scalability_curve>
  <h_phi_vault_status>No private persistent native arrays were introduced. Vault IDs remain `71270..71279` and no new global route was added.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Dependency graph is unchanged: Intersection -> EmitDamageSignals -> StageImpactVFX -> Telemetry. The lock helper now names the trajectory buffer by its current write phase before it becomes the active solver read buffer.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No direct sibling-domain import or asmdef reference was added. Compile proof remains blocked by unrelated deleted `_Archive` file references, and build was intentionally not launched in this pass.</compile_guard>
  <dear_lie_confirmation>The Dear Lie remains mathematical hit truth and deferred presentation data instead of PhysX bullets, hot combat raycasts, CPU decals, or material fracture simulation.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 12

What was wrong: The legacy velocity queue path still performed an avoidable normalized-direction roundtrip: `float3` velocity math -> Unity `Vector3` direction -> `float3` again inside the runtime queue. The static arithmetic scan also still reported a cold CSV fractional division and a string-only inspector slash, reducing the value of the scan as a regression proof.

What was done: Introduced `QueueResolvedTrajectoryNoMarker` as the shared internal writer for resolved `float3` direction data. `QueueTrajectoryFromVelocity` now passes `velocity3 * invSpeed` directly into that writer. `QueueTrajectoryFromRuntime` keeps its public `Vector3` API for existing call sites but delegates to the same writer. Replaced the cold CSV division with `math.rcp(math.max(divisor, 1f))`. Changed the legacy inspector header from `VFX / Audio` to `VFX and Audio`.

Cinematic Cheats used: No new simulation path was added. The route remains a fake-first ballistic model: no Rigidbody projectile authority, no combat PhysX raycast, flat 8x8 penetration LUT, scalar hydrodynamic drag, deterministic ricochet, and deferred unmanaged impact VFX data.

Exact Microseconds saved: 0 us measured. Static ALU cleanup removes one Unity struct reconstruction/conversion path from velocity queueing. The CSV parser change is cold-path proof hygiene, not a frame-time claim.

Verification: Arithmetic scan over `BallisticsRuntime.cs` and `FloraProjectile.cs` returned no matches for `math.sqrt`, `.magnitude`, distance helpers, or raw slash patterns. Projectile authority scan over `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `HostileFlora.cs`, and `FloraProjectile.cs` returned no `Physics.Raycast`, `Rigidbody`, collision callback, `.linearVelocity`, or `Instantiate(` matches. DTO property/`Pack=1` scan returned no matches. Hot-path scan reports only the cold/manual mock `handle.Complete()` and forced teardown/reset completion. `git diff --check` reports CRLF normalization warnings only. `dotnet build` was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_12">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 16 hardens Task 01/02 proof scans, Task 03 hot DTO/queue purity, Task 06 arithmetic hygiene, Task 14 deterministic normalization route, Task 18 CSV parser hygiene, and Task 20 evidence quality.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. `BallisticTrajectoryDTO` remains exactly 64B: `OriginAUP=0/24`, `Direction=24/12`, `Velocity=36/4`, `Mass=40/4`, `WeaponHash=44/4`, `SourceEntityID=48/4`, `Flags=52/4`, `_pad0=56/4`, `_pad1=60/4`.</struct_layout_verification>
  <scalability_curve>Below 0.3 quality, the velocity queue now avoids the Unity direction roundtrip before root-biased primitive admission and low signal/ricochet budgets. Middle/high/ultra behavior is unchanged and remains continuously driven by `GlobalQualityWeight`.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation was introduced. Vault IDs remain `71270..71279`; no new buffer request or global enum edit was added.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Normal graph remains `BallisticIntersectionJob -> EmitBallisticDamageSignalsJob -> StageImpactVFXJob -> BallisticsTelemetryJob`. Hot job fields retain `[NoAlias]`; queue writer only mutates the current Vault-backed trajectory write buffer.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No direct sibling-domain import or asmdef reference was added. Build proof remains intentionally deferred because `Assembly-CSharp.csproj` still references absent unrelated `_Archive` files and the user constrained build launch.</compile_guard>
  <dear_lie_confirmation>The Dear Lie remains O(T * admitted_P * ricochet_budget) mathematical AABB/LUT truth instead of PhysX projectile/collision/raycast authority or material fracture simulation.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 13

What was wrong: Scheduled Burst jobs still mixed raw unsafe refs with `NativeArray[index]` access. That was functionally workable but not strong enough for the authenticated SHINOBU_127 XML, which demands raw pointer iteration over flat Vault-backed ballistic DTOs.

What was done: Converted scheduled job data access to `NativeArrayUnsafeUtility` plus `UnsafeUtility.AsRef<T>` in the remaining places. Mock firefight writes trajectories/primitives through refs. The intersection job reuses primitive and LUT pointers, including the final hit primitive and 8x8 penetration lookup. Damage signal emission mutates overflow flags through a hit-result ref. VFX staging writes impact DTOs through refs. Telemetry reads hit results and writes the telemetry ring/counter DTO through refs.

Cinematic Cheats used: No new physical simulation. The active fake remains mathematical AABB hit truth, hydrodynamic scalar drag, 8x8 penetration LUT, deterministic ricochet, and deferred unmanaged impact VFX staging.

Exact Microseconds saved: 0 us measured. Static evidence removes scheduled-job `NativeArray[index]` access from the owned hot graph; expected benefit is lower abstraction overhead and stronger Burst/vectorization proof, not a measured claim.

Verification: Indexer scan now reports only `writeTrajectories[index] = trajectory` in the main-thread queue API. Job pointer scan shows `GenerateMockBallisticsJob`, `BallisticIntersectionJob`, `EmitBallisticDamageSignalsJob`, `StageImpactVFXJob`, and `BallisticsTelemetryJob` using unsafe refs. Arithmetic, projectile authority, DTO property, `Pack=1`, and unsafe-container-suppression scans remain clean. `git diff --check` reports CRLF normalization warnings only. Build was not launched because the known unrelated `_Archive` references are still absent and the user constrained build use.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_13">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 17 directly strengthens Task 03 CS1612/raw field access, Task 06 AABB kernel, Task 08 LUT lookup, Task 10 signal overflow mutation, Task 12 VFX staging, Task 16 telemetry, and Task 20 aliasing proof.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Primary trajectory DTO remains 64B and ARM64-safe; hit result remains 112B; VFX DTO remains 80B; telemetry/counter/tuning DTOs remain 64B.</struct_layout_verification>
  <scalability_curve>Quality behavior is unchanged: below 0.3 the solver keeps root-biased primitive admission, low ricochet budget, and low signal budget; middle/high/ultra scale continuously through `GlobalQualityWeight`.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation was introduced. Pointer refs target Vault buffers `71270..71279` already registered for this lane.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>All scheduled jobs retain `[NoAlias]` NativeArray fields for Burst dependency/safety metadata, then resolve raw refs inside `Execute`. Normal graph remains Intersection -> EmitDamageSignals -> StageImpactVFX -> Telemetry.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling-domain import, global enum edit, or asmdef reference was added. Compile proof remains deferred by request and by unrelated absent `_Archive` file references.</compile_guard>
  <dear_lie_confirmation>The Dear Lie remains O(T * admitted_P * ricochet_budget) flat math and LUT truth. PhysX projectiles, hot combat raycasts, CPU decals, and material fracture simulation remain absent from the owned route.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 14

What was wrong: Static NaN proof still depended on reasoning instead of explicit code. Mock trajectory generation used `math.normalize`, and legacy flora velocity clamping used `math.rsqrt(speedSq)` after branch validation rather than guarding the denominator at the call.

What was done: Replaced mock normalization with `BallisticsRuntime.NormalizeOrDefault`, which validates finite input and applies `math.rsqrt(math.max(lengthSq, Epsilon))`. Replaced the legacy clamp with `math.rsqrt(math.max(speedSq, 0.000001f))`.

Cinematic Cheats used: No new simulation. The mathematical ballistic fake remains unchanged: Vault trajectories, AABB math, scalar drag, LUT penetration, deterministic ricochet, and deferred impact VFX staging.

Exact Microseconds saved: 0 us measured or claimed. This is NaN-resilience proof, not a performance claim.

Verification: `rg` over owned runtime/projectile files now finds no `math.normalize`. All remaining `math.rsqrt` matches are explicitly guarded by `math.max`. Arithmetic slash/sqrt/magnitude scan is empty. PhysX authority, DTO property, `Pack=1`, and unsafe-container-suppression scans are empty. Build was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_14">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 18 strengthens Task 05 mock firefight, Task 06 solver math hygiene, Task 14 deterministic rollback resilience, and Task 20 NaN proof.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Primary trajectory DTO remains 64B; telemetry/counter false-sharing-relevant DTOs remain 64B.</struct_layout_verification>
  <scalability_curve>Quality behavior is unchanged. Below 0.3 quality, the same low-cost path runs with explicit rsqrt guards; middle/high/ultra retain richer primitive/ricochet/VFX budgets through `GlobalQualityWeight`.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation was introduced. Vault IDs remain `71270..71279`.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Dependency graph is unchanged and pointer-ref job access from Loop 17 remains in place.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling-domain import or project metadata edit was added. Compile proof remains deferred by explicit build constraint and unrelated missing `_Archive` file references.</compile_guard>
  <dear_lie_confirmation>The Dear Lie remains mathematical and data-only. This pass only prevents hidden normalization from becoming a NaN source.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 15

What was wrong: The final owned trajectory-buffer indexer was in `QueueResolvedTrajectoryNoMarker`. It was not inside a Burst job, but it was still the hot ingress path for every ballistic trajectory and contradicted the stricter raw-pointer proof standard applied to the scheduled jobs.

What was done: Replaced `writeTrajectories[index] = trajectory` with a local unsafe pointer resolution and `UnsafeUtility.AsRef<BallisticTrajectoryDTO>` write at `index * sizeof(BallisticTrajectoryDTO)`. The pointer is resolved at mutation time from the current Vault-backed `NativeArray`, not cached.

Cinematic Cheats used: No new fake was added. The ballistic route remains trajectory DTOs + AABB slab math + hydrodynamic drag + flat LUT penetration + deterministic ricochet + deferred impact VFX staging.

Exact Microseconds saved: 0 us measured. This is structural hot-path consistency and pointer-proof hardening; no profiler claim is made.

Verification: Indexer scan over `BallisticsRuntime.cs` returned no matches for trajectory, primitive, hit, impact VFX, telemetry, counter, or LUT buffer indexing. Projectile PhysX scan, DTO property/`Pack=1` scan, and arithmetic slash/sqrt/magnitude scan remain empty. All remaining `math.rsqrt` calls are visibly guarded by `math.max`. Build was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_15">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 19 strengthens Task 03 raw DTO mutation, Task 06 solver ingress, Task 14 deterministic data route, and Task 20 pointer-aliasing proof.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. The raw queue write uses the 64B `BallisticTrajectoryDTO` stride: offsets remain `0/24/36/40/44/48/52/56/60`.</struct_layout_verification>
  <scalability_curve>Quality behavior is unchanged. This pass affects trajectory ingress before continuous `GlobalQualityWeight` controls primitive admission, ricochet budget, signal cap, and VFX richness.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation or cached raw pointer was introduced. Pointer is resolved from Vault-backed `NativeArray` at mutation time.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Queue ingress now matches the scheduled job pattern: Vault `NativeArray` field/handle for lifetime, raw ref for mutation. Job graph remains Intersection -> EmitDamageSignals -> StageImpactVFX -> Telemetry.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling-domain import, asmdef reference, or project metadata edit was added. Compile remains intentionally deferred under the user build constraint and known unrelated `_Archive` file references.</compile_guard>
  <dear_lie_confirmation>The Dear Lie remains pure math and deferred data. No PhysX projectile, hot raycast, CPU decal, or material fracture authority was introduced.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 16

What was wrong: `HostileFlora`, a touched firing component, still carried sibling-domain dependency residue: a dead `Hecton8.Audio` import and a direct `Hecton8.World.WorldRuntimeReferenceUtility` player lookup. Ballistics runtime was already clean, but the fire-path proof was not.

What was done: Removed the dead audio import. Replaced the World utility target lookup with Core `GlobalRegistry.Player` and `IPlayerRuntimeContext.PlayerTransform`. No new contract, asmdef, or global enum was added.

Cinematic Cheats used: No new simulation. This pass protects architecture boundaries while preserving the mathematical ballistic fake: no physical projectile, no hot combat raycast, LUT penetration, deterministic ricochet, and deferred VFX staging.

Exact Microseconds saved: 0 us measured or claimed. This is compile-wall/dependency hygiene.

Verification: Dependency scan over `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `HostileFlora.cs`, and `FloraProjectile.cs` now reports only `Hecton8.Core`, `Hecton8.Core.Contracts.Signals`, and `Hecton8.Core.Memory`. Projectile authority scan, DTO property/`Pack=1` scan, and arithmetic slash/sqrt/magnitude scan remain empty. Build was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_16">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 20 strengthens Task 01 fire-path isolation, Task 02 non-PhysX authority proof, and Task 20 compile-wall verification.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Primary trajectory DTO remains 64B and the queue writer still writes by explicit 64B stride.</struct_layout_verification>
  <scalability_curve>Quality behavior is unchanged. Target acquisition remains cold/slow-tick; solver complexity continues to scale via `GlobalQualityWeight`.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation or new Vault buffer was introduced.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Dependency graph is unchanged for jobs. Fire-path service lookup now routes through Core registry rather than World utility.</pointer_aliasing_and_dependency_graph>
  <compile_guard>Owned/touched fire-path files now avoid direct `Hecton8.World` and `Hecton8.Audio` references; only Core/Core.Contracts/Core.Memory remain in the checked Hecton8 dependency surface.</compile_guard>
  <dear_lie_confirmation>No physical projectile authority was reintroduced. The target lookup cleanup does not alter mathematical hit truth.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 17

What was wrong: `HostileFlora` used `_shotSeed` as both RNG salt and damage source identity, spread used a custom hash-to-float helper instead of `Unity.Mathematics.Random`, and slow-tick cooldown/aim interpolation still assumed `0.5f` while the dispatcher slow lane is documented as 10 Hz. That made authored cooldowns fire too fast and weakened rollback/source proof.

What was done: Added distinct folded Core source IDs for flora shots and legacy facade shots. Added `BallisticsRuntime.ResolveNextSimulationFrameCounter()` and seeded flora spread from AUP-derived 1 km sector hash + next ballistic frame + source salt through `Unity.Mathematics.Random`. Replaced stale `0.5f` cooldown/aim scalars with `NominalSlowTickSeconds = 0.1f`, and replaced `Mathf.Clamp` with `math.clamp`.

Cinematic Cheats used: Still no physical projectile authority. The Dear Lie remains a mathematical shot DTO with deterministic spread, Vault-backed AABB hit truth, LUT penetration, scalar drag, ricochet math, and deferred VFX staging.

Exact Microseconds saved: 0 us measured or claimed. This pass fixes determinism/provenance and gameplay cadence, not frame-time math.

Verification: Targeted scans over `BallisticsRuntime.cs`, `HostileFlora.cs`, `FloraProjectile.cs`, and the editor facade found no `UnityEngine.Random`, `Random.`, `Mathf.`, `Time.deltaTime`, `Time.time`, `Time.frameCount`, sibling `Hecton8.World`/`Audio` imports, PhysX projectile authority, DTO property wrappers, `Pack=1`, `math.normalize`, or `math.sqrt`. Build was not launched; unrelated `_Archive` source references remain absent.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_17">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 21 strengthens Task 01 projectile authority replacement, Task 10 source-routed damage signals, Task 14 rollback deterministic seed proof, and Task 20 self-audit evidence.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Primary trajectory DTO remains 64B, hit result remains 112B, telemetry/counter DTOs remain 64B.</struct_layout_verification>
  <scalability_curve>Quality behavior is unchanged after queue ingress. Below 0.3 quality the solver still collapses primitive admission, ricochet budget, signal cap, and VFX richness through `GlobalQualityWeight`; high/ultra keep richer math and staging.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation or new Vault handle was introduced. Vault IDs remain `71270..71279`.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Job graph remains Intersection -> EmitDamageSignals -> StageImpactVFX -> Telemetry with `[NoAlias]` NativeArray fields and pointer/ref access inside job bodies. Fire ingress now uses source ID and deterministic RNG before queueing.</pointer_aliasing_and_dependency_graph>
  <compile_guard>Checked fire-path dependencies remain Core/Core.Contracts/Core.Memory only; no direct sibling World/Audio import was reintroduced.</compile_guard>
  <dear_lie_confirmation>The Dear Lie now begins before queueing: deterministic angular jitter replaces any projectile physics or stochastic Unity random authority, then the existing math-only ballistic solver owns hit truth.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 18

What was wrong: `HostileFlora` still had `_shotOrdinal++` in the deterministic spread seed. That counter is local MonoBehaviour state, not a ballistic DTO field, and no rollback rewind proof existed for it.

What was done: Removed `_shotOrdinal`. Flora spread seed is now pure: folded source ID salt + AUP sector hash + next ballistic simulation frame. The same rollback-restored frame/sector/source reproduces the same spread without a local mutable counter.

Cinematic Cheats used: Still the same visual fake: deterministic angular jitter plus mathematical trajectory DTOs replace projectile physics and stochastic Unity random behavior.

Exact Microseconds saved: 0 us measured or claimed. The value is rollback-state reduction.

Verification: Targeted scan found no `_shotOrdinal`, no `UnityEngine.Random`, no `Random.`, no `Mathf.`, no Unity time reads, no sibling World/Audio imports, no PhysX projectile authority, no `Pack=1`, no `math.normalize`, and no `math.sqrt` in the owned fire path. `git diff --check` reported CRLF warnings only. Build was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_18">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 22 strengthens Task 14 rollback netcode state fence and Task 20 deterministic proof.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Removing `_shotOrdinal` affects managed fire ingress only.</struct_layout_verification>
  <scalability_curve>Quality behavior is unchanged. The continuous `GlobalQualityWeight` solver path remains the only cost scaler after ingress.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation or new Vault handle was introduced.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Job graph is unchanged. Seed ingress now has less local mutable state before the Vault trajectory write.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling-domain import or project metadata edit was added.</compile_guard>
  <dear_lie_confirmation>The Dear Lie remains deterministic source/sector/frame angular jitter plus AABB/LUT hit math; no projectile Rigidbody or hot raycast route exists in the owned path.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 19

What was wrong: `HostileFlora` comments still described GameTickManager, projectile spawning, projectile prefab assignment, and player tag caching. Those comments no longer matched the math-only fire path.

What was done: Updated comments to name Core registry dispatcher ownership and mathematical trajectory queueing. Removed unused `PlayerTag`.

Cinematic Cheats used: No runtime change. This protects the existing fake by removing source-level instructions that implied prefab projectile authority.

Exact Microseconds saved: 0 us.

Verification: Stale-comment scan no longer finds `PlayerTag`, `projectile spawning`, `GameTickManager`, `~2x`, `must have FloraProjectile`, or `Pre-cached player reference` in `HostileFlora.cs`. Build was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_19">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 23 strengthens Task 01 authority hygiene and Task 20 source-level architecture proof.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. The pass removed stale comments and one unused managed constant only.</struct_layout_verification>
  <scalability_curve>Runtime quality behavior is unchanged; comments now point maintainers toward the existing continuous `GlobalQualityWeight` solver route.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation or new Vault handle was introduced.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Job graph and pointer/ref access remain unchanged.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling-domain import or project metadata edit was added.</compile_guard>
  <dear_lie_confirmation>Source comments now describe mathematical trajectory queueing instead of projectile spawning, reducing risk of future PhysX regression.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 20

What was wrong: Non-finite `GlobalQualityWeight` failed open to `1.0f` in the global resolver and the old intersection-job smoothing curve. That would turn a corrupted quality read into ultra ricochet/primitive/signal/VFX work. `LimbAdmissionFloor` also allowed values up to `1.0f`, which can collapse or invert the `smoothstep(floor, 0.95f, quality)` edge interval. The main runtime damage-signal budget referenced the old smoothing helper from outside its owning type, weakening compile proof.

What was done: Added `BallisticsRuntime.SanitizeQualityWeight(float)` and `BallisticsRuntime.SmoothQualityWeight(float)`, then routed global quality, damage-signal budgeting, intersection quality smoothing, impact VFX scale, and telemetry counters through those runtime helpers. Non-finite quality now becomes `0.0f`. Clamped `LimbAdmissionFloor` to `0.0f..0.9f` in tuning sanitation and again inside the intersection job before `smoothstep`.

Cinematic Cheats used: No new simulation. This hardens the existing Dear Lie: quality faults collapse to cheaper AABB/LUT/ricochet/VFX math instead of promoting physical complexity.

Exact Microseconds saved: 0 us measured or claimed. Expected protection is preventing accidental ultra workload and NaN propagation when the quality scalar is corrupt.

Verification: Focused scan shows all quality consumers in `BallisticsRuntime.cs` now route through `SanitizeQualityWeight` or receive the sanitized value. Barrier scan shows the job graph remains scheduled and non-blocking; only cold/manual mock generation uses direct `handle.Complete()`, and force completion stays behind teardown/reset `DispatcherJobSwap.TryComplete`. Build was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_20">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 24 strengthens Task 11 continuous scalability, Task 14 deterministic/fault resilience, Task 16 telemetry truth, and Task 20 self-audit proof.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed. Primary `BallisticTrajectoryDTO` remains 64B with offsets `0/24/36/40/44/48/52/56/60`; hit/VFX/tuning/telemetry/counter sizes are unchanged.</struct_layout_verification>
  <scalability_curve>Below 0.3 quality, the sanitized polynomial curve preserves low-cost root-biased AABB admission, minimum signal cap, and zero/low ricochet work. Non-finite quality now follows that low-cost path instead of ultra. Middle/high/ultra still ramp continuously through the same scalar.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation or new Vault handle was introduced. Vault IDs remain `71270..71279`.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Graph remains Intersection -> EmitDamageSignals -> StageImpactVFX -> Telemetry. `[NoAlias]` NativeArray fields and pointer/ref DTO access remain in the scheduled jobs.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling-domain import, asmdef reference, or project metadata edit was added. Compile remains intentionally deferred under the user build constraint and known unrelated `_Archive` file references.</compile_guard>
  <dear_lie_confirmation>The Dear Lie remains mathematical AABB/LUT/drag/ricochet/staged-VFX work. This pass ensures a broken quality scalar collapses the fake to survival cost instead of trying to buy visual overkill with invalid data.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Ultra Polish Pass 21

What was wrong: `HostileFlora` still exposed an unused `playerLayerMask` and inspector/source text about projectile spawning. That contradicted the runtime route: Core registry player lookup and math-only ballistic trajectory queueing.

What was done: Removed the unused layer-mask serialized field and its defaulting block. Updated the state comment, class summary, muzzle tooltip, speed tooltip, and legacy visual shell tooltip. The remaining legacy shell reference is explicitly documented as ignored by combat authority.

Cinematic Cheats used: No new simulation. This pass protects the existing Dear Lie by removing designer-facing text that implied spawned projectile damage.

Exact Microseconds saved: 0 us measured or claimed. It removes one cold validation branch and misleading inspector state, not hot solver work.

Verification: Static stale-authority scan over `HostileFlora.cs` found no `playerLayerMask`, `CompareTag`, `HectonLayerMasks`, `spawned projectile`, `projectiles spawn`, `shoots projectiles`, `Firing projectile`, `projectile spawning`, `GameTickManager`, `PlayerTag`, or `Pre-cached player reference`. Owned PhysX/random/time/sibling-import/property/layout scans remain empty. Build was not launched.

<SELF_AUDIT_DELTA agent_id="SHINOBU_127" pass="ultra_polish_21">
  <task_reconciliation>Tasks 01-20 remain implemented. Loop 25 strengthens Task 01 projectile authority eradication, Task 02 non-PhysX hit proof, and Task 20 source/inspector audit.</task_reconciliation>
  <struct_layout_verification>No DTO layout changed.</struct_layout_verification>
  <scalability_curve>Runtime quality behavior is unchanged. The edit removes stale inspector routes that could bypass the quality-scaled solver in future work.</scalability_curve>
  <h_phi_vault_status>No private persistent native allocation or new Vault handle was introduced.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Job graph and pointer/ref access are unchanged.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No sibling-domain import, asmdef reference, or project metadata edit was added.</compile_guard>
  <dear_lie_confirmation>The Dear Lie remains the only combat authority: queued trajectory DTOs, AABB math, LUT penetration, scalar drag, deterministic ricochet, and staged impact VFX.</dear_lie_confirmation>
</SELF_AUDIT_DELTA>
