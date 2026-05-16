# LOCKSTEP_STATE_VALIDATOR LOG

## 2026-05-16 Active Start

What was wrong: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="LOCKSTEP_STATE_VALIDATOR">`, so the mandated XML task list cannot be extracted from the active batch. Active audit already lists this ID as missing.

What was done: Created active status/rationale/log files and recorded the blocker. Continuing under the user's direct fallback directive: implement/verify 300-frame Master State Hashing within `CORE/DETERMINISM`.

Cinematic Cheats used: None yet; this is core deterministic state, not a visual simulation.

Exact Microseconds saved: 0 runtime; documentation and extraction only.

## 2026-05-16 300-Frame Master State Hashing Pass

What was wrong: The active lockstep validator already contained the 300-frame master hash implementation, but its signal namespace import pointed at non-existent `Hecton8.Core.Signals`. Active batch XML for this prompt is still absent.

What was done: Corrected `LockstepStateValidator.cs` to import `Hecton8.Core.Contracts.Signals`. Verified the existing implementation has `HashCadenceFrames = 300`, a fixed 300-frame telemetry ring, 300-frame replay input blocks, Burst jobs for subsystem array hashes, a 64-bit master fold, desync pause/reporting, and `Docs/AgentLogs/Dump_LOCKSTEP_STATE_VALIDATOR.bin` blackbox output.

Cinematic Cheats used: None. This is deterministic simulation telemetry; visual fakes do not apply.

Exact Microseconds saved: 0us measured. Runtime cost unchanged by the namespace patch. Static estimate: avoiding duplicate validator ownership prevents a second 300-frame hash pass, approximately tens of microseconds per hash frame on compact DataVault buffers.

Verification: Static scan passed for the validator: no stale `Hecton8.Core.Signals`, no LINQ/coroutine/Update additions, fixed 300-frame constants present, and `git diff --check` has no whitespace errors beyond CRLF normalization warning. Unity batchmode compile ran and returned `EXIT=1`; latest log has no lockstep/determinism diagnostics, but global compile is blocked by unrelated audio/editor/save tooling assembly errors.

## 2026-05-16 Injected XML Completion Pass

What was wrong: The active XML was injected after the first pass. The validator still violated the new task list by owning persistent private `NativeArray` buffers, reading player velocity through a Rigidbody path, missing typed `LockstepSnapshotSignal`, lacking high-end/stress cadence adaptation, and relying on a 300-frame telemetry ring instead of a literal last-10 successful hash history.

What was done: Updated status to task count 18. Moved lockstep scratch/result/replay/telemetry/history buffers to `GlobalDataVault` via typed `BufferID` entries. Removed direct Rigidbody velocity authority and read `PlayerKinematicVelocities` from the vault. Added Pack=1 lockstep snapshot, glitch, replay, telemetry, history, and job structs. Added `HashDouble3ArrayJob` for current `RigidbodyAUPs` `double3` storage. Added 60-frame High/Ultra cadence, 1200-frame SHI backoff, typed snapshot signal publication, typed glitch signal publication, existing visual static request on desync, vault-owned 10-entry master hash history, and `FatalDesyncException` after non-finite blackbox dump.

Cinematic Cheats used: Determinism domain does not render. The applicable cheat is cadence LOD: toaster tier skips normal-play hashing, critical stress defers to 1200 frames, and High/Ultra spend saved cycles on tighter 60-frame validation that downstream visual systems can react to through typed signals.

Exact Microseconds saved: Removing Rigidbody component access is estimated under 3us per hash fence. Low/MX350 normal play avoids the full hash pass. Critical SHI mode avoids three out of four normal 300-frame hash fences. No profiler microseconds claimed; Unity compile is blocked before playmode profiling.

Verification: Static scans show no private persistent lockstep `NativeArray` fields, no `H8Memory.Allocate`, no `new NativeArray`, no direct `Rigidbody`/`linearVelocity`, no `Transform.position`, no `UnityEngine.Random`, no stale signal namespace, and no `StructLayout(LayoutKind.Sequential)` without `Pack = 1` in the determinism file. `git diff --check` reports only CRLF normalization warnings. Unity batchmode `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260516_0500_unity.log` returns `EXIT=1`; diagnostics are external `AudioVirtualizationJobs.cs`, `ModuloSimulationBucketer.cs`, and `VRPhysicalHandPresenceIkJobs.cs`. No lockstep/determinism diagnostics are present. Isolated determinism csc is blocked because `Hecton8.Core.ref.dll` is not emitted while those external assemblies fail.

## 2026-05-16 Continuation Purge Pass

What was wrong: Lockstep signal payloads were defined in the Determinism asmdef while `GlobalSignals.cs` already referenced them from the Core signal lane registry. Room water and player mirror sanitizers could replace non-finite values with safe fallbacks without forcing `ArrayFlagNonFinite`. `ResolveDataVault()` hid a lazy `GlobalRegistry` refresh under `GetVaultBuffer`, which is called from `PostFixedTick`.

What was done: Moved `LockstepSnapshotSignal` and `SystemGlitchSignal` into `Assets/_Project/Scripts/Core/GlobalSignals.cs` as single Pack=1, 32-byte typed lane payloads. Removed the duplicate determinism-local definitions. Room water mirroring now carries non-finite evidence into the room hash category. Player state hash treats the high-bit sanitizer flag as non-finite. `LockstepMasterHashHistory` now records only clean successful hashes, while the 300-frame telemetry ring records degraded/fault frames. Snapshot/history `Flags` now use telemetry flags after category flags are mapped, avoiding overlapping bit domains. `ResolveDataVault()` now returns the cached DataVault dependency only.

Cinematic Cheats used: Safe fallback values are written to Vault instead of propagating NaN while the hash flags preserve the fault evidence. This is the Dear Lie path: stable presentation/simulation memory, deterministic failure proof. Low tier still skips normal-play hashing; High/Ultra still buy tighter 60-frame lockstep snapshots.

Microseconds saved: Exact measured microseconds are unavailable because Unity/player profiling is blocked. Estimated hot-path runtime change is 0us on initialized frames. Failure-path registry refresh is removed. Estimated NaN evidence cost is sub-microsecond branch/select work at mirror/hash cadence.

Verification: `rg` confirms one definition each for `LockstepSnapshotSignal` and `SystemGlitchSignal`, both in `GlobalSignals.cs`. Static purge scan found no determinism `Update/FixedUpdate/LateUpdate`, `H8Memory.Allocate`, `new NativeArray`, direct Rigidbody velocity, `linearVelocity`, `Transform.position`, `UnityEngine.Random`, stale `Hecton8.Core.Signals`, or hot `foreach`. `git diff --check` reports only CRLF normalization warnings. Unity batchmode `Build_LOCKSTEP_STATE_VALIDATOR_20260516_0945_unity.log` reached AssetDatabase/script compilation request, emitted no compiler diagnostics, hung past 900s, and was killed. No Unity process was left running. Direct `Hecton8.Core` csc is blocked by missing `Hecton8.Animation.IK.ref.dll`, `Hecton8.Audio.Virtualization.ref.dll`, and `Hecton8.Core.Bucketing.ref.dll`. Direct `Hecton8.Core.Determinism` csc is blocked by missing `Hecton8.Core.ref.dll`.

## 2026-05-16 Omega Polish Pass

What was wrong: Omega required explicit `math.select` zero-length hash guards and no NativeArray `foreach`. A concurrent edit also brought back monolithic `GlobalSignals.InitializeAllQueues()` inside the validator, and replay reads still used default read buffering.

What was done: Re-read the exact XML prompt from `CURRENT_BATCH.md`. Added `math.select` guard resolution in `ResolveHashCount`, `ResolveRoomHashCount`, `ResolveScheduleCount`, and `SetDefaultArrayHash`. Replaced the monolithic signal initialization with narrow `SignalBus<LockstepSnapshotSignal>` and `SignalBus<SystemGlitchSignal>` configuration using fixed capacities/lane hashes. Changed ghost replay reads to `FileOptions.SequentialScan` with a 4-block buffer. Confirmed no `GlobalRegistry.IsReplayActive` API exists, so replay low-tier enablement remains governed by the validator-owned `_ghostReplayActive` state instead of an invented registry dependency.

Cinematic Cheats used: Determinism domain does not render. The relevant cheat remains mathematical LOD: Low/MX350 skips normal-play hashing, replay forces validation, High/Ultra spend budget on 60-frame hashes, and high stress defers to 1200 frames.

Exact Microseconds saved: No exact profiler microseconds are claimed. Hot-path signal change is 0us because it is cold `OnEnable` setup. Hash guard delta is expected 0us to sub-microsecond per hash fence. Replay read change targets MicroSD stutter risk during cold replay load, not fixed-frame time.

Verification: Static scans after patch show no `GlobalSignals.InitializeAllQueues()` in `LockstepStateValidator`, no determinism `foreach`, no `Update/FixedUpdate/LateUpdate`, no `string.Format`, no `new NativeArray`, no private persistent NativeArray fields, no `H8Memory.Allocate`, no direct physics/transform authority reads, no `UnityEngine.Random`, no stale `Hecton8.Core.Signals`, and no `StructLayout(LayoutKind.Sequential)` missing `Pack = 1`. The only NativeArray scan hit is `GetVaultBuffer<T>`, which returns vault-owned storage. `git diff --check` reports only CRLF normalization warnings. Unity rerun was deferred because PID 47176 is an active `UBER_NOIR_INTEGRATOR` batch process in this project. Project compile remains blocked by external assemblies; direct csc is blocked by missing external reference DLLs and missing `Hecton8.Core.ref.dll`.
