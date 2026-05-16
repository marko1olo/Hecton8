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

Solution: Define the Pack=1 signal structs in `GlobalSignals.cs`, prewarm their `SignalBus` lanes during `OnEnable`, publish `LockstepSnapshotSignal` after the completed master hash fence, and publish `SystemGlitchSignal` plus request existing dispatcher visual static on desync.

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

## Signal Contract Ownership Repair

Problem: `GlobalSignals.cs` already references `LockstepSnapshotSignal` and `SystemGlitchSignal`, but the payload structs were placed inside `LockstepStateValidator.cs` under the Determinism asmdef. That creates an assembly ownership fault because `Hecton8.Core` cannot depend on `Hecton8.Core.Determinism`.

Solution: Move the two Pack=1, 32-byte payload contracts into `Assets/_Project/Scripts/Core/GlobalSignals.cs`, the existing signal-lane authority file, and remove the duplicate determinism-local definitions. The validator now only consumes the typed lanes.

Rejected Alternatives: Keeping duplicate namespace-local structs in Determinism, adding an asmdef cycle, or moving the full `SignalBus<T>` stack into Contracts during a lockstep pass.

Scalability potential: Low tier keeps bounded 16/8 signal capacities. High/Ultra still get 60-frame lockstep snapshots and fault-only visor glitch pulses without increasing normal gameplay broadcast volume.

Hardware Impact: 0us hot-path runtime change. This is a compile/link correctness repair and a cache-lane ownership cleanup.

## Non-Finite Evidence Repair

Problem: The mirror path sanitized room water and player pose values before hashing, but the previous implementation could erase the evidence by writing safe fallback values with no array-level non-finite flag. That would block the required FatalDesync path.

Solution: Room water mirroring now returns a non-finite evidence bit that marks the room hash category before the master job. Player state keeps the high-bit non-finite flag and the player hash job treats that flag as a non-finite element even after safe fallback values are written. Snapshot/history flags now use the telemetry flag domain after array flags are mapped, avoiding overlap between `ArrayFlag*` and `TelemetryFlag*` bit positions.

Rejected Alternatives: Writing raw NaN into the Vault, relying on GPU/physics consumers to survive it, silently zeroing the value and calling the hash clean, or OR-ing array flags and telemetry flags into one ambiguous field.

Scalability potential: Toaster mode gets safe fallback values without NaN propagation. High/Ultra retain deterministic fault evidence and blackbox dump behavior.

Hardware Impact: Unmeasured sub-microsecond branch/select cost at mirror/hash cadence. No profiler proof is available because Unity compile is still blocked.

## Hot-Path Registry Cache Repair

Problem: `ResolveDataVault()` refreshed dependencies through `GlobalRegistry` if `_dataVault` was null, and that helper is called by `PostFixedTick` through `GetVaultBuffer`. That is a hidden hot-path registry poll on dependency failure.

Solution: `ResolveDataVault()` now returns the cached field only. Dependency refresh remains in `OnEnable` and explicit ghost replay start, which are cold/control paths.

Rejected Alternatives: Lazy service lookup inside every vault helper, or polling registry until DataVault appears.

Scalability potential: Low tier avoids pointless registry traffic if initialization is broken. High/Ultra keep the same hash cadence without hidden dependency lookups.

Hardware Impact: Normal initialized path is unchanged. Failure path avoids repeated service refresh; measured microseconds are unavailable.

## Post-Patch Validation Wall Refresh

Problem: The signal placement and NaN evidence repairs required fresh validation, but the project compiler wall remains outside the lockstep domain.

Solution: Run static purge scans, `git diff --check`, Unity batchmode, and direct Bee csc invocations for `Hecton8.Core` and `Hecton8.Core.Determinism`.

Rejected Alternatives: Reporting the old compile attempt as current proof, or patching animation/audio/bucketing assemblies from the determinism role.

Scalability potential: No runtime scalability change. The pass reduces integration risk and preserves the lockstep cadence rules already implemented.

Hardware Impact: 0us runtime. Unity batchmode timed out after 900s in AssetDatabase/script compilation request with no compiler diagnostics; direct csc is blocked by missing external ref assemblies.

## Omega Hash Guard Polish

Problem: The Omega mandate requires `math.select` handling for zero-length hash guards. The validator already skipped empty arrays before scheduling, but the count resolution still depended on branch-only guard shape in the hash-count and default-hash paths.

Solution: Add `ResolveScheduleCount<T>()` and use `math.select` in hash count resolution, room count resolution, schedule-count clamping, and default array hash count normalization. Jobs still early-out before invalid zero-count scheduling, because scheduling a zero-length Burst job is not useful proof and can hide data-absence mistakes.

Rejected Alternatives: Scheduling zero-count jobs, using managed helper collections, or leaving all guard logic as plain branch predicates. Those options either add no determinism value or violate the exact Omega guard requirement.

Scalability potential: Low/MX350 keeps the cheapest no-work path for missing or empty buffers. High/Ultra keep full hash fidelity without spending cycles on empty categories.

Hardware Impact: No measured microseconds are claimed. The expected runtime delta is 0us to sub-microsecond per hash fence because this is integer guard selection at the 60/300/1200-frame cadence, not per-frame simulation work.

## Omega Signal Lane Purge

Problem: A concurrent edit restored `GlobalSignals.InitializeAllQueues()` inside `ConfigureSignalLanes()`. That violates the typed-lane ownership rule by letting the lockstep validator initialize every global queue instead of only the two signals it emits.

Solution: Re-apply narrow typed-lane configuration for `LockstepSnapshotSignal` and `SystemGlitchSignal`, using fixed capacities and lane hashes that match `GlobalSignals.cs`. The validator now configures and ensures only its two lanes.

Rejected Alternatives: Calling monolithic global initialization from determinism, relying on another system's initialization ordering, or adding managed delegate fallbacks.

Scalability potential: Low tier avoids broad cold-start signal work from this system. High/Ultra still receive high-cadence hash snapshots and fault-only glitch signals through typed lanes.

Hardware Impact: Hot-path cost remains 0us because configuration is cold `OnEnable` work. Cold-start savings are unmeasured; no profiler number is claimed.

## Steam Deck Replay I/O Polish

Problem: Ghost replay load used default read buffering for fixed 300-frame replay blocks. On MicroSD or pressure-heavy storage, small default buffering increases stutter risk during replay initialization.

Solution: Open replay reads with `FileOptions.SequentialScan` and a `ReplayBlockBytes * 4` buffer. The writer already used sequential scan. Disk I/O remains cold replay setup work, not fixed-tick work.

Rejected Alternatives: Per-frame disk reads, memory-mapped save-header writes from this domain, or default read buffering with no access hint.

Scalability potential: Low/Middle devices get lower replay-start I/O pressure. High/Ultra keep the same deterministic replay block format and can spend the saved pressure on tighter 60-frame hash validation.

Hardware Impact: No exact microseconds are claimed. Expected gain is reduced read-call churn and lower MicroSD stutter probability during replay load, not a measured fixed-frame saving.

## Replay Authority API Reality Check

Problem: The XML names `GlobalRegistry.IsReplayActive`, but active source contains no such API. Inventing it inside the lockstep pass would create a direct dependency and collide with registry ownership.

Solution: Keep low-tier hash exception tied to `_ghostReplayActive`, the validator-owned state set by its ghost replay loader. Record the missing registry API as an integration reality rather than fabricating a field.

Rejected Alternatives: Adding `GlobalRegistry.IsReplayActive` without a registry-owner contract, polling unrelated replay systems, or disabling low-tier hashing during replay.

Scalability potential: Toaster mode still skips normal gameplay hashing. Replay mode still forces validation because replay desync detection is the reason to spend the budget.

Hardware Impact: 0us runtime change. The decision prevents an unmanaged ownership expansion and keeps registry traffic out of the hot path.

## Omega Validation Refresh

Problem: After the final lane repair, validation had to reflect the current files. A separate `UBER_NOIR_INTEGRATOR` Unity batch process is active in the same project, so launching another Unity compile would create editor/process contention instead of clean proof.

Solution: Run strict static scans for determinism hot-path debt, Pack=1 layout drift, typed-lane configuration, whitespace errors, and active Unity processes. Defer Unity rerun while PID 47176 owns the project batch session.

Rejected Alternatives: Running a competing Unity batchmode instance, killing another agent's Unity process, or reporting the previous compile wall as a fresh run.

Scalability potential: No runtime behavior change. This preserves cross-agent build stability while keeping deterministic validator evidence current.

Hardware Impact: 0us runtime. Static scans show only the vault helper NativeArray return, which is not a local allocation or persistent private array.
