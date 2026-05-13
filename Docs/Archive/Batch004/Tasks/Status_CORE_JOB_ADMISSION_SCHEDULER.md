# CORE_JOB_ADMISSION_SCHEDULER Status

Status: PENDING VERIFICATION
Domain: CORE & MEMORY INFRASTRUCTURE
Prompt: `CORE_JOB_ADMISSION_SCHEDULER`
Task Count: 19

## Loop 1: Tasks 1-5

- [x] 1. SINGLETON ERADICATION | Justification: Bootstrap owns `BurstTokenBucketJobAdmissionService`; registered through `GlobalRegistry.JobAdmission`, bridged to wrappers. DOD: no `JobManager.Instance`. | Rejected: hidden static singleton owner | Estimate: saves 40us lookup churn by avoiding scene searches/service discovery.
- [x] 2. SIGNAL MIGRATION | Justification: Denials publish fixed-size `CpuStarvationSignal` through `GlobalSignals` and crash telemetry. DOD: NativeQueue signal path. | Rejected: managed event/listener fanout | Estimate: saves 15us per denial versus managed diagnostics allocation.
- [x] 3. ASMDEF ISOLATION | Justification: `Hecton8.Core.Scheduling` isolated from Core runtime and references Contracts plus Unity package assemblies required for Jobs/NativeArray/Math. DOD: Bee compiled scheduling DLL. | Rejected: putting scheduler in monolithic Core | Estimate: saves 250us+ future compile invalidation per dependent edit.
- [x] 4. DOMAIN CONSTANTS | Justification: ABI-stable six-lane enum and constants defined in Contracts. DOD: no string lane names. | Rejected: dynamic category map | Estimate: saves 5us per admission.
- [x] 5. EWMA COST TRACKING | Justification: Fixed 256-slot FNV1a cost table updates via `math.lerp(previous, measured, 0.10f)` and caller completion reports. DOD: no dictionary/string key frame path. | Rejected: Stopwatch-only logging without feedback | Estimate: prevents multi-ms pileups; hot admission lookup target under 8us.

## Loop 2: Tasks 6-10

- [x] 6. TIERED BUCKETS | Justification: Six persistent `NativeArray<float>` lane budgets refilled from dispatcher PreSimulation using scalability byte. DOD: persistent fixed storage only. | Rejected: per-system quotas in MonoBehaviours | Estimate: saves 0.2-2.0ms stalls on weak CPUs by refusing background jobs.
- [x] 7. DYNAMIC REFILL | Justification: Refill uses clamped delta scalar and halves refill after missed budget. DOD: deterministic scalar math. | Rejected: realtime worker-thread introspection | Estimate: sheds 450us+ of background work after a bad frame.
- [x] 8. ADMISSION GATE | Justification: `TryAdmitJob` reserves tokens, denies low lanes, and fail-opens only before initialization. DOD: bool gate with estimated cost out. | Rejected: post-schedule cancellation | Estimate: saves full job cost when denied.
- [x] 9. SCHEDULE WRAPPERS | Justification: `ScheduleAdmitted`/`ScheduleParallelAdmitted` extensions compile in scheduling assembly and avoid boxing. DOD: generic struct constraints. | Rejected: interface boxing wrappers around `IJob` | Estimate: 0 allocations; under 10us overhead.
- [x] 10. LOAD SHEDDING | Justification: `PredatorCognitionDomain` denies `Lane3_AI` and reuses previous NativeArray state; no cognition job scheduled on starvation. DOD: visual fake. | Rejected: blocking complete or reduced-count partial AI job | Estimate: saves 300-2000us on predator-heavy frames.

## Loop 3: Tasks 11-14

- [x] 11. VOXEL THROTTLING | Justification: `HectonVoxelEngine` physics bake jobs gate on `Lane2_Voxel`; first denial waits one frame, second denial defers caller cleanup. `WorldChunkResidencyManager` has no `Mesh.BakePhysics` site, so residency scan/sort are gated as world work. DOD: real bake schedule path throttled. | Rejected: inventing a fake residency bake dependency | Estimate: saves 500-3000us PhysX bake spikes.
- [x] 12. CRITICAL LANE GUARANTEE | Justification: `Lane0_Critical` always admits and borrows from lower lanes down to debt floor. DOD: debt-backed admission. | Rejected: denying player-critical physics | Estimate: preserves player frame path under worker saturation.
- [x] 13. ZERO-GC | Justification: NativeArray budgets/EWMA/blackbox, FNV1a job hashes, no frame dictionaries or strings. DOD: fixed capacities. | Rejected: `Dictionary<string,float>` cost table | Estimate: eliminates 0.5-2KB/frame GC risk.
- [x] 14. WATCHDOG INTEGRATION | Justification: 60 consecutive critical-debt frames set `SystemKillSwitchMask` and `Lane4_VFX` admission denies while mask is active. DOD: VFX kill switch. | Rejected: logging debt only | Estimate: reclaims 450us+ VFX worker budget on low-end.

## Loop 4: Tasks 15-17

- [x] 15. AUP SAFETY | Justification: `AupPreShiftSignal` activates non-critical admission barrier; `AupShiftSignal` clears it. DOD: handles defer across pre-shift barrier. | Rejected: coordinate-aware scheduler math | Estimate: avoids shift race without adding per-job AUP transforms.
- [x] 16. BLACKBOX DUMP | Justification: 300-entry scheduler blackbox plus `CrashTelemetryBuffer` job admission entries on denial/non-finite; NaN path exports immediately. DOD: postmortem state exists. | Rejected: chat/log-only diagnostics | Estimate: saves forensic time; runtime overhead bounded to fixed writes.
- [x] 17. RECONNAISSANCE | Justification: `Docs/Tasks/RECON_CORE_JOB_ADMISSION_SCHEDULER.md` records 266 naked schedules and core hotspots. DOD: file artifact written. | Rejected: unbounded mass migration | Estimate: prevents hours of cross-domain conflict churn.

## Loop 5: Tasks 18-19 + Omega

- [x] 18. MATH LOD | Justification: Low tier uses 0.60 refill scalar; High/Ultra keep full budget for visual overkill lanes. DOD: low/mid/high/ultra policy in rationale. | Rejected: balanced middle refill | Estimate: cuts 40% background admission on i3/MX350.
- [!] 19. OMEGA COMPILE CHECK | Justification: `Hecton8.Core.Scheduling.dll` compiled through Unity Bee and standalone csc; full Core compile blocked by unrelated shared dependency errors. DOD: scheduler wrapper compile verified, project compile blocked. | Rejected: editing Save/Audio/Power/Player domains to hide unrelated failures | Estimate: no scheduler boxing detected.
- [!] OMEGA POLISH MANDATE | Justification: First `<POLISH_MANDATE id="OMEGA_POLISH">` extracted after core tasks were done/blocked. Scheduler-owned anti-bloat scan found no `foreach`, string formatting/interpolation, `.ToString()`, `math.sqrt`, or `math.normalize`; division scan hit XML comments only. DOD: no extra runtime math expansion. | Rejected: `dotnet build` because the user explicitly forbade it and Unity MCP validation returned `no_unity_session` | Estimate: 0us extra hot-path cost.

## Verification Log

- Prompt extracted by CLI from `Docs/Tasks/CURRENT_BATCH.md`.
- Relevant mandates read before coding.
- Initial state files were empty or absent at session start.
- Loop 1 complete: scheduling contracts/service/wrappers compile standalone.
- Loop 2 complete: dispatcher refill and AI shed path installed.
- Loop 3 complete: voxel bake throttling, critical debt, zero-GC storage, kill switch installed.
- Loop 4 complete: AUP barrier, blackbox telemetry, recon artifact installed.
- Loop 5 compile: Unity Bee compiled `Hecton8.Core.Scheduling.dll`; `Hecton8.Core.dll` blocked by unrelated `SaveManager`, `PowerDrainSignal`, `EcosystemDirector`, and `HectonPlayerMovement` errors.
- Omega polish parse completed after tasks were checked/blocked; first `OMEGA_POLISH` mandate applied to scheduler-owned code. Build step remains PENDING because user forbade `dotnet build` and Unity MCP returned `no_unity_session`.
- Continuation audit 2026-05-13: patched `BurstTokenBucketJobAdmissionService` so recovered critical lane debt clears the VFX kill mask, `Lane0_Critical` borrows only newly created debt instead of the full job cost, AUP/VFX denials report the current EWMA estimate, full EWMA tables fall back to a conservative overflow EWMA, and blackbox entries carry refill frame sequence. DOD: deterministic fixed storage, no dictionaries, no managed frame allocations. Rejected: broad schedule-site migration and `dotnet build`. Estimate: prevents unnecessary 50-200us/lane starvation bleed on normal critical admissions and reopens VFX after recovery.
- Continuation verification 2026-05-13: `git diff --check` passed for the scheduler service. Unity MCP `validate_script` and console read both returned `no_unity_session`; no `dotnet build` was run per user instruction.
- Continuation audit 2026-05-13B: split EWMA lookup from allocation so AUP/VFX-denied jobs do not consume the 256 fixed slots before they complete. DOD: table capacity is reserved for measured jobs, denied jobs still get default/overflow estimates. Rejected: growing the table or adding a dictionary. Estimate: protects 256-slot cost table on barrier-heavy frames; avoids false overflow under AUP churn.
- Continuation audit 2026-05-13C: tightened low-tier bucket cap to match the 0.60 refill scalar, normalized clamped lane IDs before denial/non-finite telemetry, guarded critical-debt borrowing from non-finite lower-lane budgets, and added one-per-fault-frame EWMA cost table dumps into `CrashTelemetryBuffer` before forced non-finite export. DOD: low-tier cannot bank high-tier burst capacity; crash dump includes lane budgets plus measured EWMA slots. Rejected: dynamic dictionaries, broad cross-domain rewrites, and `dotnet build`. Estimate: prevents 180-560us of low-tier background burst after idle and preserves postmortem evidence for 256 measured job families.
- Continuation verification 2026-05-13C: `git diff --check` passed for scheduler/telemetry edits with LF/CRLF warnings only. Scoped fixed-string scans found no `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `new Dictionary`, `new List`, `.Where(`, or `.Select(` in scheduler-owned files. Unity MCP `validate_script` and console read returned `no_unity_session`; no `dotnet build` was run.
- Continuation audit 2026-05-13D: normalized `ReportJobCompleted` non-finite lane telemetry and added `typeof(TJob).FullName ?? typeof(TJob).Name` for cold FNV1a job hashing. DOD: invalid completion lanes cannot leak raw enum bytes; generic type hashing does not depend on nullable metadata. Rejected: broad caller audits and per-frame managed job names. Estimate: 0us steady-frame cost; prevents corrupt crash lane evidence and cold metadata null hazards.
- Continuation verification 2026-05-13D: `git diff --check` passed for scheduler/telemetry edits with LF/CRLF warnings only. Scoped fixed-string scans confirmed no `ReportNonFinite(lane` path remains in scheduler admission/completion flow and no unguarded `FullName` hash remains. Unity MCP validation and console reads returned `no_unity_session`; no `dotnet build` was run.
