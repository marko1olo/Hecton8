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

## Continuation Blackbox I/O Repair

Problem: `DumpBlackBox()` wrote the 300-frame telemetry dump through `WriteByte` in a nested struct loop. The path is fault-only, but on Steam Deck or MicroSD that still creates worst-case tiny synchronous writes exactly when crash evidence needs to survive.

Solution: Add one cold preallocated 19208-byte dump staging buffer, clamp serialization to the actual vault telemetry buffer length, serialize the dump header and up to 300 Pack=1 telemetry records into it with `UnsafeUtility.CopyStructureToPtr`, then issue one `FileStream.Write` and flush the file stream.

Rejected Alternatives: Keeping per-byte writes, reading 300 entries blindly if the vault returns a shorter buffer, allocating a fresh dump byte array during the fault, or moving crash evidence into a managed list/string log.

Scalability potential: Low/MX350 and Steam Deck get lower fault-path I/O call pressure. High/Ultra retain the same full 300-frame binary evidence and do not spend extra normal-frame time.

Hardware Impact: No measured microseconds are claimed. Static operation count removes up to 19208 single-byte write calls per blackbox dump and replaces them with one block write; hot-path impact is 0us because dump serialization only runs on desync/NaN fault.

## Fresh Unity Compile Evidence

Problem: Previous Unity validation was stale and one run was skipped while another agent owned the Unity process. After the typed-lane regression was repaired again, the compiler state needed fresh evidence.

Solution: Run Unity 6000.4.1f1 batchmode after confirming no active Unity process. Parse `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260516_blackbox_unity.log` after the blackbox dump change.

Rejected Alternatives: Relying on old logs, claiming determinism compile success without a clean Unity pass, or fixing editor/audio/scheduling/bucketing compile walls from the lockstep domain.

Scalability potential: No runtime behavior change. This is integration proof and blocker isolation.

Hardware Impact: 0us runtime. The fresh log reports external compile errors in editor tooling, audio virtualization, core scheduling, and bucketing. It reports no `LockstepStateValidator` or `Hecton8.Core.Determinism` diagnostics.

## Final Concurrent Drift Check

Problem: Parallel work repeatedly restored monolithic signal initialization in the validator. After the blackbox bounds guard, source truth had to be checked again instead of trusting the status file.

Solution: Re-scan `LockstepStateValidator.cs` for `GlobalSignals.InitializeAllQueues`, typed signal configuration, Pack=1 layout drift, hot-path GC patterns, and active Unity processes. Re-apply typed lane setup when the drift reappeared.

Rejected Alternatives: Leaving source and status inconsistent, killing the active `UBER_NOIR_INTEGRATOR` Unity process, or launching a competing Unity batch process in the same project.

Scalability potential: Low/MX350 keeps bounded lockstep lane capacities and avoids broad global queue init from this validator. High/Ultra keep the richer 60-frame snapshot cadence without extra gameplay broadcast volume.

Hardware Impact: 0us hot-path runtime. Current Unity ownership is PID 44132 for `Unity_UBER_NOIR_INTEGRATOR_loop23.log`; no fresh lockstep Unity rerun was started after that process appeared.

## Final Compiler Slot Evidence

Problem: The previous source-state proof was correct, but the final compiler result still needed a current batchmode log after the Unity slot became available.

Solution: Launch Unity 6000.4.1f1 batchmode against `C:\hades\Hecton8`, wait for the child editor process to exit, and parse `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260516_final_unity.log` for compiler diagnostics and lockstep-domain hits.

Rejected Alternatives: Leaving the live Unity child process behind, treating the early launcher return as a completed compile, or repairing external audio/editor/bucketing/AI ownership failures from the lockstep pass.

Scalability potential: No runtime behavior change. The value is integration proof that the 300-frame master state hashing source is not producing current compiler diagnostics while external assembly walls still exist.

Hardware Impact: 0us runtime. The final log fails on external errors in audio virtualization, editor tooling, core bucketing, and AI cognition; no `LockstepStateValidator` or `Hecton8.Core.Determinism` diagnostics were found.

## Vault Length Guard And Drift Repair

Problem: The active source had drifted back to `GlobalSignals.InitializeAllQueues()`, and several vault reads treated `NativeArray.IsCreated` as enough proof before indexing fixed slots. A created-but-undersized vault view would crash the validator before the 300-frame blackbox evidence could be dumped.

Solution: Restore typed lane-only setup for the two lockstep-owned signals. Add required-length validation for master hash reads, replay input reads, replay block serialization, ghost replay loading, category masks, history cursor writes, and public master-hash folding. `GetVaultBuffer` now returns default when the vault cannot satisfy the requested length.

Rejected Alternatives: Trusting vault `IsCreated`, adding local fallback arrays, or broad-initializing every global signal lane from this system.

Scalability potential: Low/MX350 keeps bounded typed signal lanes and gets fail-safe replay/hash exits instead of exception storms. High/Ultra keep full 60-frame debug cadence and 300-frame blackbox payloads with stronger guard rails.

Hardware Impact: No measured microseconds are claimed. Added guards are O(1) integer length checks at hash/replay/fault cadence, not per-entity math. Hot-path heap impact remains 0 B; Unity compile remains blocked by external editor/audio/bucketing assemblies with no lockstep-domain diagnostics.

## Ring Cursor Guard

Problem: The validator normalized most ring cursors after ordinary updates, but a stale private cursor after replay fault, re-enable, or cross-agent state churn could still index a replay or telemetry ring before normalization. That is the wrong failure mode for a blackbox owner.

Solution: Normalize `_inputWriteIndex` before replay-input writes, reject negative or out-of-window `_ghostInputCursor`, normalize replay block ring start before serialization, and normalize `_telemetryWriteIndex` before telemetry writes. Re-apply typed signal lane setup after another concurrent drift restored broad global queue initialization.

Rejected Alternatives: Trusting cursor invariants, adding managed try/catch around every ring access, or allocating local fallback arrays.

Scalability potential: Low/MX350 gets deterministic fail-safe ring access without disk or heap pressure. High/Ultra keep richer replay and telemetry evidence without increasing per-entity hash cost.

Hardware Impact: No measured microseconds are claimed. The change adds O(1) unsigned bounds checks at replay/telemetry cadence, preserves 0 B hot-path heap allocation, and prevents an out-of-range crash before blackbox evidence can be written.

## ABI Sentinel And Drift Repair

Problem: The active source drifted back to `GlobalSignals.InitializeAllQueues()` again, and the binary replay/blackbox path still depended on raw 128/48/64 byte constants. Pack=1 annotations are necessary, but not enough proof that a future field edit or platform-specific layout change will preserve Quest/Android replay evidence.

Solution: Restore typed lane-only configuration for the lockstep snapshot and glitch lanes. Add named byte constants and a cold `ValidateBinaryLayout()` sentinel using `UnsafeUtility.SizeOf<T>()` for `LockstepPlayerKinematicState`, `LockstepReplayBlockHeader`, `LockstepReplayInputFrame`, `LockstepArrayHash`, `LockstepTelemetryEntry`, `LockstepMasterHashHistoryEntry`, `LockstepSnapshotSignal`, and `SystemGlitchSignal`. Replace raw replay offsets with those constants. If layout validation fails, set `TelemetryFlagLayoutInvalid`, publish numeric telemetry, attempt one blackbox dump, block ghost replay load, and block replay block writes.

Rejected Alternatives: Trusting the attributes without runtime verification, leaving raw byte offsets in serialization code, broad-initializing every signal lane, or throwing repeated exceptions/dumps every fixed tick on an invalid ABI.

Scalability potential: Low/MX350 and Android/Quest get fail-fast binary evidence guards without heap pressure. High/Ultra keep full 60-frame validation and 300-frame blackbox fidelity with stronger ABI proof.

Hardware Impact: No measured microseconds are claimed. Normal hot-path heap remains 0 B. Layout checks run cold in `OnEnable`; invalid-layout dump throttling uses one `Interlocked.Exchange` only on the fault path. Dotnet build remains blocked by external UI/navigation and dispatcher compile walls with no lockstep-domain diagnostics.

## Integration Compile Constants Revalidation

Problem: A later shared-worktree drift removed the lockstep typed-lane capacity/hash constants while leaving `ConfigureSignalLanes()` wired to them. The result was a Core compile wall in the integration pass.

Solution: Restore the four compile constants only: `LockstepSnapshotSignalCapacity`, `SystemGlitchSignalCapacity`, `LockstepSnapshotLaneHash`, and `SystemGlitchLaneHash`. No lane size or payload behavior was changed.

Rejected Alternatives: Broad `GlobalSignals.InitializeAllQueues()` was rejected because the validator owns two typed lanes and must not initialize unrelated queues. Changing capacity values during integration was rejected because it would alter lockstep telemetry behavior outside a lockstep task.

Scalability potential: Low/MX350 keeps bounded lockstep snapshot/glitch lanes. High/Ultra keep richer validation cadence without hidden global queue initialization.

Hardware Impact: Runtime frame savings are 0 us measured. Evidence is the integration green build `Build_INTEGRATION_ASSEMBLY_SURGEON_20260516_inquisition31_typed_compass_final.log`; final static scan remains clean for Core layouts and global signal packing.

## Ghost Cursor Pre-Arithmetic Guard

Problem: `ApplyGhostReplayInput()` requested a vault buffer length with `_ghostInputCursor + 1` before proving `_ghostInputCursor` was non-negative and inside the replay window. A stale negative or corrupted cursor could turn the guard into the failure site, which is unacceptable for the system responsible for replay proof and blackbox evidence.

Solution: Snapshot `_ghostInputCursor` into a local scalar, reject negative or out-of-window values before any addition, then request the required DataVault length and re-check the returned buffer length before indexing. The cursor advance now writes `_ghostInputCursor = ghostInputCursor + 1` only after a valid frame match.

Rejected Alternatives: Trusting private cursor invariants, catching the exception, allocating a managed fallback replay list, or creating new contract stubs to get a clean build. The first three weaken blackbox determinism; the contract stubs would cross into `Core/Contracts` ownership and mutate public interface surface from a determinism task.

Scalability potential: Low/MX350 and Steam Deck get deterministic replay fail-safe behavior with no disk or heap pressure. High/Ultra keep the same 60-frame hash validation and ghost replay override path, but corrupted replay cursor state now exits through controlled replay shutdown instead of an out-of-range crash.

Hardware Impact: No measured microseconds are claimed. Added work is O(1) scalar checks at replay cadence and 0 B heap. `dotnet build Hecton8.Core.csproj` is blocked before determinism compile by missing `Core/Contracts/HectonPlatformContract.cs`, `HectonDataSovereigntyContract.cs`, and `HectonVisualOverkillContract.cs`; the new build log has 0 `LockstepStateValidator` or `Hecton8.Core.Determinism` hits.

## Post-Cursor Typed Lane Drift Repair

Problem: A final broad determinism scan caught `GlobalSignals.InitializeAllQueues()` restored again inside `ConfigureSignalLanes()` after the ghost cursor patch and documentation update. That invalidated the typed-lane source truth even though the status already recorded typed lanes.

Solution: Re-apply the narrow `SignalBus<LockstepSnapshotSignal>.Configure` and `SignalBus<SystemGlitchSignal>.Configure` setup with fixed capacities and lane hashes, then re-scan the determinism folder.

Rejected Alternatives: Leaving a known source/status mismatch, broad-initializing every signal queue from the validator, or depending on a different system to configure the lockstep lanes.

Scalability potential: Low/MX350 avoids broad cold signal setup from this validator. High/Ultra keep the tighter 60-frame snapshot cadence and fault-only glitch path through bounded typed lanes.

Hardware Impact: 0us hot-path runtime. This is cold `OnEnable` lane setup; no profiler microseconds are claimed. Immediate post-repair scan shows only typed lane configuration and the DataVault helper NativeArray return.

## Final Ghost Cursor Compile Wall

Problem: After the final typed-lane drift repair, `dotnet build Hecton8.Core.csproj` no longer stopped on the three missing contract source files. It now stops on `Assets/_Project/Scripts/HectonFloatingOrigin.cs(1426,66)` with CS0120 against `_totalOffsetDouble`. That file is modified outside the `CORE/DETERMINISM` domain.

Solution: Re-scan the determinism source after the build and confirm the lockstep validator still has typed lanes, no broad signal initialization, no Update-family methods, no local NativeArray allocation, no direct physics/transform authority reads, and no Pack=1 drift. Record the build wall as external dependency evidence.

Rejected Alternatives: Editing `HectonFloatingOrigin.cs` from this role, because it is outside the authoritative determinism folder and already modified by another worker; fabricating a green build; or ignoring the newer compiler log and retaining stale missing-contract evidence as final truth.

Scalability potential: No runtime behavior change inside lockstep. The validator remains bounded for Low/MX350 and retains 60-frame High/Ultra validation. Floating-origin repair belongs to the AUP/core owner because it controls world rebasing semantics.

Hardware Impact: 0us runtime. The final build log reports 0 `LockstepStateValidator` and 0 `Hecton8.Core.Determinism` diagnostics; the compile wall is external.

## Post-AUP Compile Revalidation

Problem: The previously recorded compile wall became stale after the active `HectonFloatingOrigin.cs` source changed from under the shared worktree. The status had to be revalidated against disk truth.

Solution: Re-read the XML block, inspect the current AUP method, verify the lockstep typed lanes are still present, and rerun `dotnet build Hecton8.Core.csproj` into `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260516_rerun_after_aup_dotnet.log`.

Rejected Alternatives: Reporting stale blocked status, editing another agent's AUP file without necessity, or skipping the compiler slot after source drift.

Scalability potential: No runtime behavior change. The value is integration proof that the lockstep validator now sits behind a green Core compiler pass while preserving Low/MX350 skip, High/Ultra 60-frame cadence, and 300-frame blackbox behavior.

Hardware Impact: 0us runtime. `dotnet build` returned `EXIT=0`; this is compiler evidence only, not profiler timing.

## Typed Lane Constants Repair And Defrag Build Wall

Problem: The active source drifted again: `ConfigureSignalLanes()` had reverted to `GlobalSignals.InitializeAllQueues()` and the local lockstep typed-lane constants were absent. That left the status file claiming typed lanes while disk truth used broad global initialization. After repair, the current `dotnet build Hecton8.Core.csproj` no longer reports lockstep errors, but it now stops before determinism on `Hecton8.Core.Memory.Defrag` / `MemoryDefragPhase` symbols referenced by `SystemDispatcher.cs` and `GlobalDataVault.cs`.

Solution: Restore one authoritative set of `LockstepSnapshotSignalCapacity`, `SystemGlitchSignalCapacity`, `LockstepSnapshotLaneHash`, and `SystemGlitchLaneHash` constants in `LockstepStateValidator.cs`. Restore `ConfigureSignalLanes()` to configure and ensure only `SignalBus<LockstepSnapshotSignal>` and `SignalBus<SystemGlitchSignal>`. Re-scan the determinism folder and run `dotnet build Hecton8.Core.csproj` into `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260516_typed_lane_repair_dotnet.log`.

Rejected Alternatives: Broad `GlobalSignals.InitializeAllQueues()` was rejected because the validator owns two typed lanes, not the global queue registry. Duplicate constants were rejected because they caused compile churn in the shared worktree. Editing generated `Hecton8.Core.csproj` or core-memory defrag files was rejected because those files are outside `CORE/DETERMINISM`, active Unity/dotnet workers are already running, and the build failure is a cross-domain assembly wiring issue rather than a lockstep source diagnostic.

Scalability potential: Low/MX350 keeps bounded snapshot/glitch lane capacities and avoids broad cold signal setup from this validator. High/Ultra keep the 60-frame debug hash cadence and replay glitch path without hidden global queue traffic.

Hardware Impact: 0us hot-path runtime. This is cold `OnEnable` lane configuration and compiler validation. No profiler microseconds are claimed. The current build wall has 4 external core-memory defrag errors and 0 `LockstepStateValidator` / 0 `Hecton8.Core.Determinism` diagnostics.

## 2026-05-17 Typed Lane Drift Repair And Green Core Build

Problem: The active file drifted back again: `ConfigureSignalLanes()` called `GlobalSignals.InitializeAllQueues()` and the lockstep lane constants were missing from `LockstepStateValidator.cs`. That violates signal lane segregation and makes the status log false. The previous external defrag compile wall also needed fresh evidence because Unity had regenerated the script assembly references overnight.

Solution: Re-read status/rationale, the XML prompt, `AGENTS.md`, the domain map, and the relevant mandates. Restore one set of `LockstepSnapshotSignalCapacity`, `SystemGlitchSignalCapacity`, `LockstepSnapshotLaneHash`, and `SystemGlitchLaneHash`. Restore narrow typed `SignalBus<LockstepSnapshotSignal>` and `SignalBus<SystemGlitchSignal>` configuration. Re-run static debt scans and `dotnet build Hecton8.Core.csproj` into `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260517_default_obj_dotnet.log`.

Rejected Alternatives: Broad `GlobalSignals.InitializeAllQueues()` was rejected because this validator owns two lanes, not every global queue. Deleting the conditional `Debug.LogError` helper was rejected because it is guarded by `UNITY_EDITOR`/`DEVELOPMENT_BUILD` and is not a hot path. Treating `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260517_typed_lane_repair_dotnet.log` as source failure was rejected because that isolated-object `--no-restore` run only lacked `project.assets.json`.

Scalability potential: Low/MX350 keeps bounded lockstep signal capacity and skips normal-play hashing unless replay is active. High/Ultra keep 60-frame hash snapshots and replay glitch evidence through typed lanes without broad global queue initialization.

Hardware Impact: 0us hot-path runtime. The repair is cold `OnEnable` lane setup. Static scans show no hot-path allocation/string/update debt in `Core/Determinism`; `dotnet build Hecton8.Core.csproj` returned `EXIT=0`, 0 warnings, 0 errors. No profiler microseconds are claimed.

## 2026-05-17 Post-Green Drift Repair And External Submarine Wall

Problem: A post-documentation validation scan caught the same source drift after the green Core build: `ConfigureSignalLanes()` had again reverted to `GlobalSignals.InitializeAllQueues()` and the lane constants were absent. That means the green log was valid for the previous file state but not sufficient as final source truth. After repairing the drift again, the current build no longer stays green because another domain introduced `SubmarineFluidDynamics.cs(5095,49)` CS9342.

Solution: Re-apply the typed-lane constants and narrow `SignalBus<LockstepSnapshotSignal>` / `SignalBus<SystemGlitchSignal>` configuration. Re-run static scans over `Core/Determinism`, then rerun `dotnet build Hecton8.Core.csproj` into `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260517_post_scan_drift_dotnet.log`.

Rejected Alternatives: Leaving status and source inconsistent was rejected. Editing `SubmarineFluidDynamics.cs` was rejected because it is outside `CORE/DETERMINISM` and the compiler diagnostic is a submarine/fluid vector type ambiguity, not a lockstep source error. Treating the earlier green build as final was rejected because the source had drifted after it.

Scalability potential: Low/MX350 keeps bounded typed lockstep lanes and avoids broad signal queue cold setup. High/Ultra keep high-cadence snapshot evidence without extra global queue churn.

Hardware Impact: 0us hot-path runtime. The repair remains cold signal configuration. Current static scans pass for lockstep; current build wall is external and reports 0 `LockstepStateValidator` / 0 `Hecton8.Core.Determinism` diagnostics.

## 2026-05-17 Final Drift Repair And Gameplay Compile Wall

Problem: The final source scan after compaction caught the same concurrent drift again: `ConfigureSignalLanes()` had reverted to `GlobalSignals.InitializeAllQueues()` and the local typed-lane constants were absent. That made the status file false and reintroduced broad queue initialization into the lockstep validator.

Solution: Restore the four typed-lane constants and narrow `SignalBus<LockstepSnapshotSignal>` / `SignalBus<SystemGlitchSignal>` configuration. Re-run static debt scans, Pack=1 layout scan, and `dotnet build Hecton8.Core.csproj` into `Docs/AgentLogs/Build_LOCKSTEP_STATE_VALIDATOR_20260517_final_drift_repair_dotnet.log`.

Rejected Alternatives: Broad `GlobalSignals.InitializeAllQueues()` was rejected because this system owns two typed lanes, not the global queue registry. Editing `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` was rejected because the current compiler wall is a gameplay interface implementation issue outside `CORE/DETERMINISM`. Reporting the earlier green Core build as final was rejected because source drift occurred after that log.

Scalability potential: Low/MX350 keeps bounded snapshot/glitch lane capacity and avoids broad cold signal setup. High/Ultra keep 60-frame hash snapshots and replay glitch evidence through typed lanes without hidden global queue traffic.

Hardware Impact: 0us hot-path runtime. This is cold `OnEnable` lane setup plus compiler validation. No profiler microseconds are claimed. Current static scans pass for lockstep; current build wall is external `HectonPlayerMotor.cs` CS0535 and reports 0 `LockstepStateValidator` / 0 `Hecton8.Core.Determinism` diagnostics.

## 2026-05-17 Desync Blackbox Evidence Pass

Problem: Replay desync reporting called `DumpBlackBox()` inside `ReportDesync()` before the current desync frame was written into the 300-frame telemetry ring. The ghost input mismatch path could also report a desync and then let the tick continue, which risked stale or duplicate evidence and a replay block write after replay mode had been stopped.

Solution: Convert `ValidateReplayHash()` and `ApplyGhostReplayInput()` into fault-returning gates. On replay hash mismatch, frame mismatch, poisoned replay cursor, or undersized replay buffer, `PostFixedTick()` now marks `TelemetryFlagDesync`, writes the current frame heartbeat to the DataVault-owned telemetry ring, dumps `Docs/AgentLogs/Dump_LOCKSTEP_STATE_VALIDATOR.bin`, and returns before staging a replay write. `ReportDesync()` now publishes typed desync/glitch signals and pauses/stops replay only; dumping is owned by the caller after telemetry is current.

Rejected Alternatives: Keeping the dump inside `ReportDesync()` was rejected because it serialized stale pre-fault telemetry. Adding a second local NativeArray or managed queue was rejected because DataVault already owns the 300-frame ring. Throwing exceptions on ordinary replay mismatch was rejected because the XML reserves fatal throw for non-finite state and replay mismatch needs binary evidence plus a controlled pause.

Scalability potential: Low/MX350 gets the same cheap replay skip during normal play and only pays O(1) fault gates in replay mode. High/Ultra keep 60-frame hash validation and now get fault-frame evidence before the expensive dump, making replay debugging tighter without extra steady-state broadcast volume.

Hardware Impact: No profiler microseconds are claimed. Added work is one boolean return and O(1) branch checks at replay/hash cadence. Hot-path heap remains 0 B. `dotnet build Hecton8.Core.csproj` is green in `Build_LOCKSTEP_STATE_VALIDATOR_20260517_desync_blackbox_dotnet.log` with 0 warnings and 0 errors.

## 2026-05-17 Final Post-Doc Lane Repair And Restore-State Build

Problem: A final validation scan after the desync blackbox documentation caught concurrent drift again: `ConfigureSignalLanes()` had reverted to `GlobalSignals.InitializeAllQueues()` and the typed lane constants were absent. The first post-repair build attempt then timed out and left `Temp/obj/Hecton8.Core/project.assets.json` missing, making the next `--no-restore` build fail before source compilation.

Solution: Restore the same four typed-lane constants and narrow snapshot/glitch `SignalBus.Configure` calls. Stop the orphaned `dotnet` children from the timed-out validation, run `dotnet restore Hecton8.Core.csproj`, then rerun `dotnet build Hecton8.Core.csproj --no-restore`.

Rejected Alternatives: Treating the restore-state `NETSDK1004` as a source failure was rejected because it was caused by the killed build environment. Leaving broad global signal initialization in the validator was rejected because it violates signal lane segregation. Editing unrelated domains was rejected because the final compiler pass is green.

Scalability potential: Low/MX350 keeps bounded cold signal setup for two lanes only. High/Ultra keep 60-frame replay/hash evidence and typed glitch pulses without monolithic queue initialization.

Hardware Impact: 0us hot-path runtime. The final build log `Build_LOCKSTEP_STATE_VALIDATOR_20260517_final_desync_lane_repair_dotnet.log` returns `EXIT=0`, 0 warnings, 0 errors. No profiler microseconds are claimed.

## 2026-05-17 Raw Hash Source NaN Preservation

Problem: `GlobalDataVault.TryGetBuffer<T>` calls `SanitizeFinitePayload<T>` before returning float/vector views. The lockstep hash jobs were designed to mark non-finite `RigidbodyAUPs`, `EntityAUPs`, and room water payloads, but the normal vault read path could zero those NaNs before the Burst jobs inspected them. That weakens task 12 because a corrupted vault lane can become a clean zero in the master hash instead of a fatal non-finite proof.

Solution: Leave GlobalDataVault ownership untouched and change only lockstep hash-source acquisition. `ExecuteHashJobs()` now resolves the four source lanes with `TryGetBufferHandle` and creates raw native views after a local `UnsafeUtility.AlignOf<T>()` pointer-alignment check. This bypasses vault finite sanitization for the validator's read path while preserving DataVault storage ownership and ARM64-safe alignment validation. Existing room-water mirroring still records its own non-finite source flag before writing sanitized water levels.

Rejected Alternatives: Changing `GlobalDataVault.TryGetBuffer<T>` was rejected because it is shared core-memory behavior outside the determinism domain. Leaving sanitized reads was rejected because it can hide NaN evidence. Copying source lanes into local NativeArrays was rejected because the XML requires DataVault-owned state and zero hot-path allocation. A broad rebuild loop was rejected per the explicit user instruction; only one targeted `dotnet build --no-restore` was run after the code edit.

Scalability potential: Low/MX350 still skips normal-play hashing and pays nothing outside replay/hash cadence. High/Ultra keep 60-frame validation, but the evidence is now stricter: high-end replay debugging sees actual vault corruption instead of sanitized clean hashes. Ultra gets better blackbox truth without adding per-frame simulation cost.

Hardware Impact: No profiler microseconds are claimed. Runtime cost is four handle resolves plus four pointer-alignment checks only when a hash fence runs; hot-path heap remains 0 B. Static scans pass for lockstep. `Build_LOCKSTEP_STATE_VALIDATOR_20260517_raw_hash_source_dotnet.log` exits 1 on external `World/Biolum/HectonBiolumManager.cs` missing methods/fields with 0 `LockstepStateValidator` and 0 `Hecton8.Core.Determinism` diagnostics.

## 2026-05-17 Telemetry Cursor Restore And Writer Fence

Problem: The 300-frame telemetry ring is DataVault-owned and persists across component churn, but `_telemetryWriteIndex` and `_postSimulationFrame` were private scalars reset on re-enable. That could make a crash dump serialize old and new heartbeat slots out of chronological intent after validator recreation. The replay writer also disposed its `AutoResetEvent` and `FileStream` after a 250 ms join timeout even if the background writer thread was still alive, which is a Steam Deck / MicroSD race.

Solution: Restore the telemetry cursor on `OnEnable()` by scanning the vault-owned 300-entry ring, selecting the newest non-zero frame with wrap-safe unsigned comparison, restoring `_postSimulationFrame`, and placing `_telemetryWriteIndex` on the next slot. Repaired the recurring typed-lane drift again by restoring the snapshot/glitch capacity/hash constants and local `SignalBus.Configure` calls. Hardened `StopReplayWriter()` so a timed-out writer is marked faulted and left stopped instead of disposing stream/event resources under a live thread.

Rejected Alternatives: Clearing the DataVault telemetry ring on enable was rejected because it destroys blackbox evidence. Adding a second local NativeArray cursor/ring was rejected because the XML requires vault-owned state. Blocking indefinitely on replay writer join was rejected because shutdown should not hang the main thread on bad removable storage. Disposing the stream under a live writer was rejected because it converts I/O slowness into an avoidable race.

Scalability potential: Low/MX350 keeps normal-play hash skips and pays only a cold O(300) scan when the validator is created. Steam Deck gets safer replay shutdown under slow MicroSD flushes. High/Ultra retain 60-frame hash evidence and typed glitch lanes without global signal queue setup. Ultra keeps stricter crash evidence continuity after component churn.

Hardware Impact: No profiler microseconds are claimed. Normal fixed-tick cost is unchanged and hot-path heap remains 0 B. The cursor restore is one cold 300-slot DataVault scan; the writer fence is cold shutdown-only. Static scans pass; no rebuild was rerun per the explicit instruction to avoid rebuild loops.

## 2026-05-17 Near-Domain Occupancy Audit

Problem: The user requested continued tech-debt cleanup in the lockstep domain and nearby domains, but the nearby Core signal surface is actively changing. During inspection, `PlayerMovementPresentationSignals.cs` temporarily appeared as only `WaterTransitionKind`, while `GlobalSignals.cs` then changed to contain the player presentation payload structs and `WaterTransitionSignal` again. That is live adjacent ownership churn, not a safe idle target for the lockstep role.

Solution: Keep the lockstep domain bounded and verify it against disk truth: typed snapshot/glitch lanes are present, broad `GlobalSignals.InitializeAllQueues()` is absent from determinism, the replay writer self-cleanup fence is present, Pack=1 scans are clean, and no hot-path allocation/string/update debt was found. Record the Core/Signals observation as an occupancy audit instead of moving payload structs or creating duplicate contracts.

Rejected Alternatives: Moving player presentation payloads between `GlobalSignals.cs` and `PlayerMovementPresentationSignals.cs` was rejected because those files changed during the read-only audit and are outside `CORE/DETERMINISM`. Duplicating signal structs was rejected because it would create ABI ambiguity. Running another `dotnet build` was rejected because this pass made no new source patch after the writer cleanup and the user explicitly rejected rebuild loops.

Scalability potential: Low/MX350 retains the no-normal-play-hash path and bounded lockstep typed lanes. Steam Deck keeps the fenced replay writer shutdown for slow removable storage. High/Ultra keep 60-frame hash evidence and typed glitch signals without broad global queue initialization.

Hardware Impact: 0us hot-path runtime change in this audit. No profiler microseconds are claimed. Static scans only; existing writer-cleanup build evidence remains external-wall-only with 0 `LockstepStateValidator` and 0 `Hecton8.Core.Determinism` hits.

## 2026-05-17 Raw Hash Regression And Writer Signal Guard

Problem: The raw hash-source path drifted back through `VaultBufferHandle.Resolve(vault)`. `Resolve()` calls `GlobalDataVault.ResolveBuffer()`, and `ResolveBuffer()` calls `SanitizeFinitePayload<T>()`; that can erase NaN evidence before `HashDouble3ArrayJob`, `HashFloat3ArrayJob`, and room/player hash jobs can set the non-finite flags. A second cold race remained in `StageReplayWrite()`: `_writerSignal.Set()` could throw after the background writer completed self-cleanup from a previous I/O fault.

Solution: Restore direct raw native views for lockstep hash-source acquisition with `H8Memory.CreateNativeArrayView<T>(handle.ptr, handle.Length)` after the local alignment gate. Snapshot the replay writer signal in `StageReplayWrite()` and classify disposed-handle races as `TelemetryFlagWriterBusy`, clearing pending write state and setting `_writerFaulted` for controlled recovery.

Rejected Alternatives: Changing `GlobalDataVault.ResolveBuffer()` was rejected because that sanitizer is shared memory-domain behavior outside `CORE/DETERMINISM`. Leaving `handle.Resolve(vault)` was rejected because it falsifies NaN vaccination. Letting `AutoResetEvent.Set()` escape was rejected because the 300-frame blackbox must classify survival faults. Patching dirty `IPlatformIntegration`, `GlobalSignals`, `CameraJuiceSystem`, or `HectonMarineSnowRenderer` was rejected because those files are active nearby/external work, not idle lockstep ownership.

Scalability potential: Low/MX350 still skips normal-play hashing and pays nothing outside replay/hash cadence. Steam Deck gets stricter replay writer fault classification under slow or failing storage. High/Ultra keep 60-frame validation with raw corruption evidence instead of sanitized clean hashes.

Hardware Impact: No profiler microseconds are claimed. Normal fixed-tick work is unchanged except the existing hash-cadence raw view creation; the writer signal guard is cold fault-path only. The targeted build log exits on external dirty files with 0 `LockstepStateValidator` and 0 `Hecton8.Core.Determinism` diagnostics.

## 2026-05-17 Post-Documentation Typed Lane Drift Repair

Problem: The final verification after status/rationale updates found `GlobalSignals.InitializeAllQueues()` restored again inside `ConfigureSignalLanes()`, and the local snapshot/glitch capacity/hash constants were missing. That invalidated the freshly written claim that the validator used only typed lanes.

Solution: Re-apply the local `LockstepSnapshotSignal` and `SystemGlitchSignal` capacity/lane-hash constants and narrow `SignalBus.Configure` calls, then re-run the static lockstep scan.

Rejected Alternatives: Leaving broad global queue initialization was rejected because the validator owns two lanes, not the whole global queue registry. Running another build was rejected because the source returned to the same typed-lane state already covered by `Build_LOCKSTEP_STATE_VALIDATOR_20260517_writer_signal_guard_dotnet.log`, and the known wall is external dirty Core/VFX code.

Scalability potential: Low/MX350 keeps bounded cold signal setup. High/Ultra keep 60-frame hash snapshot evidence through typed lanes without broad global queue churn.

Hardware Impact: 0us hot-path runtime. This is cold `OnEnable` configuration only; no profiler microseconds are claimed.
