# LOCKSTEP_STATE_VALIDATOR Status

Prompt: `LOCKSTEP_STATE_VALIDATOR`
Domain: `CORE/DETERMINISM`
Task Count: `18` from active `Docs/Tasks/CURRENT_BATCH.md`
Directive Source: injected XML block in active batch.

## Mandatory Reads

- [x] Status/rationale reread before response | DOD: `Docs/Tasks/Status_LOCKSTEP_STATE_VALIDATOR.md` and `Docs/AgentLogs/Rationale_LOCKSTEP_STATE_VALIDATOR.md` read with PowerShell `cat` | Alternative rejected: relying on compressed chat memory | Estimate: 12000us wall time, 0us runtime.
- [x] XML block extracted after injection | DOD: exact `<AGENT_PROMPT id="LOCKSTEP_STATE_VALIDATOR">` block extracted from `CURRENT_BATCH.md`; task count is 18 | Alternative rejected: retaining stale missing-tag status | Estimate: 8000us wall time, 0us runtime.
- [x] Mandates selected and reread | DOD: `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `MATH_AUP_Determinism_Sync.txt`, and `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` read before new code edits | Alternative rejected: carrying previous mandate assumptions after XML changed | Estimate: 25000us wall time, 0us runtime.

## Phase 1: The Great Purge

- [x] 1. `[PURGE_REFS]` Scan all Determinism scripts for direct physics/transform authority reads | DOD: `rg` over `Assets/_Project/Scripts/Core/Determinism` found no `Transform.position`, `UnityEngine.Random`, or `Rigidbody.velocity`; one direct `Rigidbody` velocity dependency was removed from `LockstepStateValidator` and replaced with `BufferID.PlayerKinematicVelocities` | Alternative rejected: reading player velocity from `IPlayerRuntimeContext.PlayerRigidbody` | Estimate: 3us hot-path delta saved by avoiding component dereference; 35000us wall time.
- [x] 2. `[DEBT_CLEANUP]` Fix signal namespace drift | DOD: determinism file imports `Hecton8.Core.Contracts.Signals`; `rg` finds no `Hecton8.Core.Signals` in `Core/Determinism` | Alternative rejected: alias namespace shim | Estimate: 0us runtime.
- [x] 3. `[DATA_EVICTION]` Store MasterStateHash in GlobalDataVault | DOD: added typed `BufferID.LockstepMasterStateHash` and moved lockstep scratch/result/replay/telemetry buffers to vault-owned buffers; `LastMasterStateHash` reads from the vault | Alternative rejected: private persistent `NativeArray<ulong>` in the system | Estimate: 0us steady-state allocation; 15-40us cold vault request cost.

## Phase 2: Kernel

- [x] 4. `[BURST_HASH_JOB]` Master hash job over `RigidbodyAUPs`, `EntityAUPs`, and `RoomWaterLevels` | DOD: existing `MasterStateHashJob` retained; missing `HashDouble3ArrayJob` added for current `RigidbodyAUPs` `double3` vault type | Alternative rejected: casting double AUPs to float before hash | Estimate: deterministic double quantization cost pending profiler.
- [x] 5. `[AUP_INTEGRITY]` Relative-position hashing | DOD: validator hashes vault `RigidbodyAUPs` as published by `GlobalPhysicsStateManager`; current producer labels the buffer player-relative and uses `double3` | Alternative rejected: hashing absolute `Transform.position` | Estimate: no extra runtime beyond hash pass.
- [x] 6. `[DOD_SOA_LAYOUT]` Single parallelized hash pass | DOD: rigidbody/entity/player/water element hashes run as Burst `IJobParallelFor` categories and fold through category combines before one master job | Alternative rejected: managed per-object loop | Estimate: O(n) contiguous native reads; profiler pending.
- [x] 7. `[SIGNAL_FLOW]` Emit `LockstepSnapshotSignal(uint64 hash)` at hash fence | DOD: added Pack=1 `LockstepSnapshotSignal`, prewarmed its typed `SignalBus`, and publish it after the completed master hash fence with frame, hash, cadence, flags, and category masks | Alternative rejected: chat/log-only hash reporting or untyped EventBus | Estimate: one 32-byte native signal enqueue per hash fence.

## Phase 3: Scalability

- [x] 8. `[LOW_TIER_SKIP]` MX350/Celeron skip during normal gameplay | DOD: existing `IsLowTierDisabledForNormalPlay` skips hashing on Low/Mx350 unless replay is active | Alternative rejected: hashing every 300 frames on toaster tier during normal play | Estimate: full hash pass avoided on low tier.
- [x] 9. `[HIGH_END_OVERKILL]` High-end 60-frame hash cadence | DOD: `ResolveHashCadenceFrames` returns 60 on High/Ultra unless system stress forces backoff | Alternative rejected: fixed 300-frame cadence on 4090-tier debug sessions | Estimate: +4 hash fences per 300 frames on High/Ultra.
- [x] 10. `[REACTIVE_VFX]` Replay mismatch visor glitch signal | DOD: added Pack=1 `SystemGlitchSignal`, prewarmed its typed lane, publishes on desync, and calls `SystemDispatcher.RequestVisualStaticGlitch` for the existing visor/static path | Alternative rejected: inventing a UI dependency from determinism | Estimate: fault path only.
- [x] 11. `[STP_STABILIZATION]` N/A recorded | DOD: CPU determinism domain has no render STP path | Alternative rejected: inventing shader work in determinism | Estimate: 0us.

## Phase 4: Stability, Telemetry, Blackbox

- [x] 12. `[NAN_VACCINATION]` Non-finite hash payload guards | DOD: Burst hash jobs flag non-finite payloads; player pose/input/water mirroring now sanitizes finite values before vault writes | Alternative rejected: allowing NaN into replay or hash buffers | Estimate: sub-microsecond branch/select per mirrored scalar.
- [x] 13. `[BLACKBOX_LOGGING]` Last hashes in telemetry ring | DOD: 300-entry vault-owned telemetry ring records frame state, and a separate vault-owned 10-entry `LockstepMasterHashHistory` ring records the last 10 successful hash fences | Alternative rejected: relying on per-frame telemetry repetition as proof | Estimate: one 32-byte native write per successful hash fence.
- [x] 14. `[TRIPLE_STRIKE_REPAIR]` Compile validation and local syntax isolation `[BLOCKED BY DEPENDENCY]` | DOD: Unity batchmode rerun reached compiler diagnostics with no `LockstepStateValidator` or `Hecton8.Core.Determinism` errors in the log; isolated csc for determinism is blocked because `Hecton8.Core.ref.dll` is not produced while external assemblies fail | Alternative rejected: editing audio/bucketing/IK domains from lockstep scope | Estimate: 0us runtime.
- [x] 15. `[HOMEOSTASIS_ADAPTATION]` Stress >0.9 increases interval to 1200 frames | DOD: `HomeostasisBrain.SystemHealthIndex01` is finite-guarded and above `0.9` returns `HighStressHashCadenceFrames = 1200` | Alternative rejected: ignoring SHI during replay/hash debug | Estimate: avoids 3 of 4 normal 300-frame hash fences while hardware is critical.
- [x] 16. `[MMF_RECORDING]` Save header integration or dependency blocker `[BLOCKED BY DEPENDENCY]` | DOD: searched MacroDatabase/MMF APIs; no public save-header contract exists for writing lockstep hash from determinism, and direct database header mutation is internal to `H8MacroDatabaseService` | Alternative rejected: unsafe header writes into database internals | Estimate: 0us runtime.
- [x] 17. `[GHOST_REPLAY_HOOK]` Replay input override | DOD: existing ghost replay loader and `PhysicsDeterminismSignals.PublishInputOverride` preserved; replay buffers are now vault-owned | Alternative rejected: managed replay list | Estimate: fixed 300-frame block I/O off main thread.
- [x] 18. `[FINAL_VALIDATION]` Determinism-domain build validation `[BLOCKED BY DEPENDENCY]` | DOD: Unity batchmode returned `EXIT=1`; diagnostics are external audio virtualization, core bucketing, and animation IK errors, not determinism. Direct determinism csc is blocked by missing `Hecton8.Core.ref.dll` caused by those external assembly failures | Alternative rejected: fabricating pass status | Estimate: 0us runtime.

## Current Verification

- [x] Static grep pass after Phase 1-2 edits | DOD: no private persistent `NativeArray` fields, no `H8Memory.Allocate`, no `new NativeArray`, no direct `Rigidbody`/`linearVelocity`, no `Transform.position`, no `UnityEngine.Random`, no stale signal namespace in `LockstepStateValidator` | Alternative rejected: relying on visual inspection only | Estimate: 15000us wall time, 0us runtime.
- [x] ARM64/Quest alignment pass | DOD: lockstep replay/hash/telemetry structs and Burst job structs use `[StructLayout(LayoutKind.Sequential, Pack = 1)]` | Alternative rejected: implicit pack defaults | Estimate: 0us runtime.
- [x] Unity compile gate `[BLOCKED BY DEPENDENCY]` | DOD: latest log `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260516_0500_unity.log` has no lockstep diagnostics; blockers are `AudioVirtualizationJobs.cs`, `ModuloSimulationBucketer.cs`, and `VRPhysicalHandPresenceIkJobs.cs` | Alternative rejected: cross-domain fixes without ownership | Estimate: 0us runtime.
