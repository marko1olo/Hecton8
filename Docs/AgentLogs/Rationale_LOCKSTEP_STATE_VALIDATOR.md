# LOCKSTEP_STATE_VALIDATOR Rationale

## Active Prompt Blocker

Problem: The requested `LOCKSTEP_STATE_VALIDATOR` XML block is absent from active `Docs/Tasks/CURRENT_BATCH.md`, while the user requests implementation of 300-frame Master State Hashing.

Solution: Record task count as `0` for the active XML extraction, use the user's explicit fallback task as the working directive, and constrain edits to `CORE/DETERMINISM` unless a verified interface dependency forces a narrow cross-domain touch.

Rejected Alternatives: Synthesizing a missing prompt from archive content, borrowing neighboring agent tasks, or editing unrelated domains to satisfy assumed objectives.

Scalability potential: Low tier must hash compact truth buffers at fixed cadence with no managed allocation; Middle/High/Ultra may preserve richer subsystem hashes and dump data for better desync diagnosis without changing simulation authority.

Hardware Impact: On i3/MX350, master hashing must stay under the 0.1ms suspicion threshold and avoid per-frame disk or managed allocations. Expected cost target is tens of microseconds for compact native buffers; profiler proof remains pending until Unity can compile and run.

## Mandate Selection

Problem: Master State Hashing touches deterministic state, telemetry, native memory, zero-GC policy, registry access, and dispatcher cadence.

Solution: Read the six targeted mandates listed in the status file before code edits.

Rejected Alternatives: Reading all registry files or proceeding from memory.

Scalability potential: Mandate-limited scope keeps implementation focused and prevents broad architectural churn.

Hardware Impact: Avoids unnecessary scan/edit time in unrelated systems and reduces risk of cross-agent conflicts.

## Signal Namespace Compile Defect

Problem: `LockstepStateValidator.cs` imported `Hecton8.Core.Signals`, but the active signal bus and `InputStateSignal` contracts are declared in `Hecton8.Core.Contracts.Signals`. If Unity compiles the `Hecton8.Core.Determinism` asmdef, this namespace drift blocks the validator before any master hash can run.

Solution: Change the import to `Hecton8.Core.Contracts.Signals` and leave the existing 300-frame hash implementation intact.

Rejected Alternatives: Moving `SignalBus<T>` namespaces, adding compatibility aliases, or editing global signal contracts for one validator. Those would widen the blast radius and collide with signal-authority work.

Scalability potential: Low/MX350 keeps the validator's existing low-tier skip for normal play; High/Ultra retain the 300-frame replay/hash path. The namespace fix changes no runtime cadence.

Hardware Impact: Runtime cost unchanged. Compile availability restored for the determinism assembly; expected runtime delta is 0us.

## Unity Compile Blocker

Problem: Unity 6000.4.1f1 batchmode script compile returns `EXIT=1`, but the current diagnostics are outside `CORE/DETERMINISM`. The latest log has no `LockstepStateValidator`, `Hecton8.Core.Determinism`, or `Core\\Determinism` errors. Blocking errors are in `AudioVirtualizationJobs.cs` asmdef/reference setup and Editor tooling missing types such as `HectonSocketHelper`, `MapMagicBridge`, and save-slot DTOs.

Solution: Record the build as blocked by external dependencies, keep the lockstep patch narrow, and avoid editing audio/editor/save tooling from this scope.

Rejected Alternatives: Fixing audio virtualization, editor tooling, MapMagic editor references, or save slot windows inside the lockstep pass. That violates domain ownership and risks parallel-agent conflicts.

Scalability potential: No runtime scalability change. Lockstep hashing remains Low/MX350 skip in normal play and High/Ultra 300-frame replay validation path.

Hardware Impact: No measured runtime delta. Unity compile proof for this validator remains blocked until external assemblies compile.

## Injected XML Correction

Problem: The active batch was updated after the initial missing-tag pass. Continuing with task count `0` would corrupt the state machine and cause future compaction to revive the wrong directive.

Solution: Re-extract the exact `LOCKSTEP_STATE_VALIDATOR` XML block from `CURRENT_BATCH.md`, update task count to 18, and restart execution from Phase 1 while preserving already valid namespace work.

Rejected Alternatives: Keeping the old missing-tag rationale as authority, or inferring tasks from chat text only.

Scalability potential: Correct task parsing prevents unrelated signal/memory/audio work from leaking into the determinism domain. Low/Middle/High/Ultra behavior remains tied to the lockstep XML.

Hardware Impact: 0us runtime. Reduces integration risk only.

## Phase 1 Data Eviction

Problem: `LockstepStateValidator` owned persistent private `NativeArray` fields for hash scratch, master result, telemetry, replay input, and ghost replay. That violates the GlobalDataVault sovereignty rule and prevents the master hash from being persistent state.

Solution: Reserve lockstep `BufferID` values in `H8Memory.cs` and resolve every persistent lockstep buffer through `GlobalDataVault`. The validator now holds no persistent NativeArray fields; it resolves vault buffers at the 300-frame fence or when writing telemetry/input rings.

Rejected Alternatives: Keeping private arrays with `H8Memory.Allocate`, adding another local allocator, or storing only the final `ulong` in a managed field.

Scalability potential: Low/MX350 keeps the existing normal-play skip and writes only lightweight telemetry. Middle/High/Ultra can retain full 300-frame blackbox and replay buffers without duplicating state.

Hardware Impact: Removes independent persistent allocations from the validator. Normal frame cost is one vault telemetry write; hash scratch requests happen at fence cadence and reuse existing vault memory.

## Physics Authority Purge

Problem: The player hash path read velocity from `IPlayerRuntimeContext.PlayerRigidbody`, tying determinism to a scene component and making replays vulnerable to presentation/physics timing drift.

Solution: Source player velocity from `BufferID.PlayerKinematicVelocities` and source player position from AUP pose data. No direct `Rigidbody` or `Transform.position` read remains in the determinism script.

Rejected Alternatives: Reading `Rigidbody.linearVelocity`, reading `Transform.position`, or reconstructing authority from camera-relative runtime coordinates.

Scalability potential: Low tier still skips expensive validation during normal play. High/Ultra get tighter truth hashing because component timing no longer contaminates the sample.

Hardware Impact: Avoids component dereference and physics object access at the hash fence. Estimated savings is small per fence, roughly 3us or less, but the determinism gain is the primary value.

## ARM64 And NaN Hardening

Problem: Replay/hash structs used `Pack = 4`, and mirror paths could forward non-finite input, water, or player vectors before the hash job flagged them.

Solution: Change lockstep serialized/job structs to `Pack = 1`, add finite sanitizers for replay input, player local position/forward/velocity, and room water levels, and preserve non-finite evidence through high-bit flags and category flags.

Rejected Alternatives: Letting Burst hash jobs be the first NaN barrier, or trusting Unity component data to be finite.

Scalability potential: Toaster mode receives safe zero/fallback values rather than GPU-killing NaNs. High/Ultra retain diagnostic non-finite flags for deeper replay investigations.

Hardware Impact: Per mirrored scalar guard is sub-microsecond and only runs on input capture/water mirror/player mirror paths; hash jobs already had finite checks.

## Signal Flow And Reactive Fault Path

Problem: The XML requires a typed lockstep snapshot signal and a visor glitch on replay desync, but no current `LockstepSnapshotSignal` or `SystemGlitchSignal` contract existed in the active source.

Solution: Add Pack=1 signal structs in the determinism file, prewarm their `SignalBus` lanes during `OnEnable`, publish `LockstepSnapshotSignal` after the completed master hash fence, and publish `SystemGlitchSignal` plus request existing dispatcher visual static on desync.

Rejected Alternatives: Editing the dirty global signal registry during parallel signal-authority work, using a managed delegate/event, or logging the hash without a typed lane.

Scalability potential: Low/MX350 normal play still skips hash work. High/Ultra get 60-frame hash signals for tighter replay/debug observability. Fault-only visor glitch buys developer feedback without any normal-path visual cost.

Hardware Impact: Normal hash-fence signal cost is one 32-byte native enqueue. Glitch signal is fault-path only.

## Adaptive Hash Cadence

Problem: Fixed 300-frame cadence under-serves High/Ultra debugging and over-spends during critical hardware stress.

Solution: Resolve cadence per frame: Low/MX350 normal play still skips, High/Ultra hash every 60 frames, and any finite `HomeostasisBrain.SystemHealthIndex01 > 0.9` backs off to 1200 frames.

Rejected Alternatives: One balanced cadence for all hardware, or polling hardware APIs directly from determinism.

Scalability potential: Toaster mode preserves frame time; High/Ultra buys stricter desync detection; stress mode defers work before hardware collapse.

Hardware Impact: High/Ultra add four extra fences per 300 frames. Critical-stress mode removes three of four normal 300-frame hash fences.

## Compile Validation Wall

Problem: Unity batchmode cannot currently produce a clean project compile, and isolated determinism csc cannot run because `Hecton8.Core.ref.dll` is not emitted while upstream assemblies fail.

Solution: Run Unity batchmode twice. First run stopped on AssetDatabase churn from unrelated UI/bucketing edits. Second run reached compiler diagnostics; no `LockstepStateValidator` or `Hecton8.Core.Determinism` errors were present. Record the external blockers and do not edit those domains.

Rejected Alternatives: Fixing audio virtualization asmdefs, bucketing namespace imports, or animation IK variable shadowing inside the lockstep validator pass.

Scalability potential: None. This is integration hygiene.

Hardware Impact: 0us runtime. Validation remains blocked by external compile state, not by observed determinism diagnostics.

## MMF Save Header Blocker

Problem: The XML asks for save-header hash integration through `BACKEND_MACRO_DB_COMPACTOR`, but the active code exposes no public lockstep-hash save-header API. Macro database header writes are internal to `H8MacroDatabaseService`.

Solution: Search the MacroDatabase/MMF contracts and mark task 16 blocked by missing public integration contract. The lockstep hash is still persisted in `GlobalDataVault` and replay block headers.

Rejected Alternatives: Writing directly into the macro database header memory, inventing a parallel save file, or coupling determinism to backend internals.

Scalability potential: Vault/replay persistence works on all tiers; save-header persistence needs a backend-owned API to avoid cross-domain corruption.

Hardware Impact: 0us runtime.
