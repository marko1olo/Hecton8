# Rationale_SHINOBU_30

Agent: SHINOBU_30
Domain: Origin Shift (AUP Manager)
Status: PENDING VERIFICATION

## Decision 000 - Mandate Selection

Problem: AUP origin shift touches spatial authority, physics freeze, signal ordering, GPU offsets, native memory, and post-mortem telemetry.
Solution: Locked eight task-relevant mandates before code edits: AUP precision, AUP determinism, zero-GC, native jobs, signal segregation, GlobalRegistry DI, execution phases, and crash telemetry.
Rejected Alternatives: Reading all registry files would waste context and increase neighboring-domain contamination. Reading only AGENTS.md would miss task-specific AUP laws.
Scalability potential: Low uses coarse thresholds, sparse probes, and zero-GC native buffers; Middle increases probe cadence; High keeps richer samples; Ultra can keep full debug payloads and visual overkill in VISUAL_SYNC.
Hardware Impact: Prevents Transform-space drift and managed allocations that would spike i3/MX350 frames; expected baseline gain is stability, not a claimed measured microsecond value.

## Decision 001 - Binary Threshold Fallback

Problem: Archive/StreamingAssets scan found prior AUP logs and dumps but no active `aup_sector_grid.h8bin` or `rebase_thresholds.bin` payload to trust for live constants.
Solution: `GenerateEmergencyMockThresholds()` writes 4000m rebase limit, 5000m sector size, 10k batch size, and 50k mock entity count into unmanaged vault state.
Rejected Alternatives: Reading old logs as binary truth or using serialized `_threshold=1000` was rejected because neither is a deterministic runtime contract.
Scalability potential: Low uses 4000m early shift and 10k chunks; Middle can raise threshold by CSV; High keeps 4000m but richer shader continuity; Ultra may use larger sector visuals while preserving authority.
Hardware Impact: i3/MX350 avoids late file probing during shift; estimated shift-frame save 3-10 us and 0 B/frame.

## Decision 002 - AUP DTO and Native Rebase Authority

Problem: Existing origin shift was presentation-heavy; the batch demanded a 48-byte AUP state with direct local-position mutation.
Solution: Added `AUP_StateDTO` with explicit 48-byte layout and `AupStateRebaseJob` over contiguous vault memory using unsafe NoAlias pointers and `UnsafeUtility.AsRef`.
Rejected Alternatives: Wrapping NativeArray access in C# properties, using `Vector3` authority, or mutating scene transforms as the data source was rejected because it creates copies, jitter, and CS1612 risk.
Scalability potential: Low shifts only mandatory local fields; Middle shifts hot entity local cache; High adds historical arrays; Ultra keeps the same authority and spends saved cycles on richer visual continuity.
Hardware Impact: 50k-state shift is targeted at sub-1ms; static estimate 0.18-0.35 ms for the native batch on desktop Burst, pending profiler proof on MX350.

## Decision 003 - Velocity Preservation Law

Problem: Origin shifts are positional coordinate changes. Applying shift delta to velocity corrupts momentum and creates physics bugs.
Solution: Velocity buffers are deliberately absent from `AupStateRebaseJob`; `VaultHotEntityRebaseJob` copies `Velocity` through untouched, and existing Rigidbody resync still restores linear/angular velocity after teleport.
Rejected Alternatives: Subtracting `ShiftDelta`, zeroing velocity, or recomputing velocity from old/new positions was rejected because all three introduce non-physical impulses.
Scalability potential: Low keeps stable player/fish motion; Middle/High preserve AI steering continuity; Ultra can layer camera/particle smoothing without altering physics truth.
Hardware Impact: Prevents correction storms and solver explosions; exact microseconds saved depend on physics scene, but avoids potentially multi-ms recovery spikes on i3/MX350.

## Decision 004 - Signal Fence and Vault Allocation Lock

Problem: Rebasing while queued signals or vault reallocations are in flight can make cached local positions point at the wrong epoch.
Solution: `HectonFloatingOrigin` now locks vault allocations for the shift frame, calls `GlobalSignals.FlushPreSimulation()`, schedules the native rebase, and publishes a `MemoryAddressShiftSignal` after commit.
Rejected Alternatives: Adding a new single-use event lane or directly invoking pathfinding/AI repair code was rejected because cross-domain dependencies are forbidden during batch parallelism.
Scalability potential: Low consumes one existing typed signal; Middle/High can use the same fence for richer cache repair; Ultra can add debug consumers without changing the core lane.
Hardware Impact: Expected 30-80 us avoided on shift frames by preventing stale-cache repair churn; normal-frame overhead is one branch-free idle path outside rebase.

## Decision 005 - Dear Lie GPU Offset

Problem: Terrain/static visual continuity must not require rewriting vertices or moving large static meshes in CPU memory.
Solution: Kept double total offset accumulation and pushed `_TotalUniverseOffset` through the existing shader global DataVault bridge after shift commit.
Rejected Alternatives: Terrain vertex rebake, static mesh transform walks as authority, or terrain collider rebuild was rejected because all are expensive and violate presentation-only terrain movement.
Scalability potential: Low gets cheap shader offset; Middle adds jitter mask; High/Ultra can spend saved CPU on caustic/noise continuity and denser visual overkill.
Hardware Impact: Avoids >1ms static-geometry CPU work and preserves MX350 frame budget; shader-side float offset remains bounded by current sector.

## Decision 006 - Historical Trail and Cable Repair

Problem: Cables, splines, and trails keep previous positions. Current-position-only rebase causes a one-frame 4000m stretch.
Solution: Scheduled native float3 historical rebase for tether current, previous, visual segment, visual anchor, and mock historical point arrays.
Rejected Alternatives: Waiting for listeners to self-heal or rebuilding spline histories was rejected because hidden old positions break before listener repair can run.
Scalability potential: Low shifts only DataVault float3 history; Middle adds more registered arrays; High/Ultra can keep longer visible histories without changing the shift law.
Hardware Impact: Estimated 0.1-0.4 ms recovery avoided during cable-heavy shift frames; no velocity vectors are touched.

## Decision 007 - Stress Time Slicing

Problem: A single 50k rebase on stressed hardware can exceed the 0.1ms suspicion threshold.
Solution: If `HomeostasisBrain.SystemHealthIndex01 > 0.85`, the coordinator shifts 10k AUP records per PRE_SIM continuation while the camera/global offset commits immediately.
Rejected Alternatives: Always forcing one 50k job or permanently lowering entity caps was rejected because low hardware needs flattened spikes and high hardware should still get full visual density.
Scalability potential: Low = 10k chunks; Middle = 25k chunks through CSV; High = one batch; Ultra = one batch plus heavier visual continuity because CPU spike is contained.
Hardware Impact: Worst-frame native rebase estimate drops from 0.18-0.35 ms to roughly 0.04-0.08 ms chunks on i3/MX350-class silicon, pending profiler proof.

## Decision 008 - Black Box Telemetry

Problem: "I don't know why it crashed" is banned. AUP needs evidence when NaN or >1ms shift happens.
Solution: Added 300-entry native telemetry ring with rebase count, entities shifted, historical points shifted, compute ms, sector hash, flags, and dump path `Docs/AgentLogs/Dump_ORIGIN_SHIFT.bin`.
Rejected Alternatives: Debug.Log, managed JSON, or relying only on CrashTelemetryBuffer was rejected because logs allocate and do not provide the requested binary SHINOBU_30 dump.
Scalability potential: Low stores compact high-level state; Middle/High can increase consumers; Ultra can correlate richer visual debug externally without hot-path string work.
Hardware Impact: Normal frame cost is native write only on rebase; fault dump alloc/file IO occurs only on watchdog or NaN.

## Decision 009 - Human Control Facade and CSV Ingestor

Problem: Lead testing needs direct AUP control without flying 4000m, and constants must be editable without code recompilation.
Solution: Added `AUP Universe Tuner` editor window and a native-scratch CSV byte parser for `aup_constants.csv`.
Rejected Alternatives: Text-based runtime UI, `string.Split`, LINQ, or reflection was rejected because this is editor-only control plus zero-GC parser semantics.
Scalability potential: Low can clamp threshold lower for early rebases; Middle/High can tune batch size; Ultra can raise visuals while retaining the same authority.
Hardware Impact: Gameplay hot cost is 0 us for the editor facade; CSV poll is outside rebase and parser uses existing scratch.

## Decision 010 - Compile Wall Classification

Problem: Build verification must distinguish SHINOBU_30 errors from unrelated batch-wide breakage.
Solution: Fixed two SHINOBU_30 compile errors (`HectonPhysicsContract` namespace dependency and `DispatcherJobSwap` cross-domain dependency). Later Core build attempts report no SHINOBU_30-touched file errors but remain blocked by unrelated concurrent churn; latest observed blocker is `VoxelDeltaProcessor` missing `IDataVault` / `VaultBufferHandle<>` visibility.
Rejected Alternatives: Editing save/fauna/audio domains was rejected as architectural sabotage outside Origin Shift ownership.
Scalability potential: Low/Middle/High/Ultra all benefit from keeping the origin patch isolated and avoiding a broad refactor loop.
Hardware Impact: No runtime impact; developer-time impact is contained by not adding sibling-domain dependencies.

## Decision 011 - Polish Mandate Corrections

Problem: The polish audit found three unacceptable local risks: filesystem CSV polling was still called from PRE_SIM, support DTOs used sequential layout despite containing `double3`, and the vault allocation fence could be locked before cold AUP buffers were ensured.
Solution: Removed CSV polling from `TickPreSimulation`; the editor/dev tuner now polls/reloads `aup_constants.csv` outside the simulation path. Converted `MockCameraAUP`, `AupUniverseTunerSnapshot`, `AupOriginShiftScheduleInfo`, `AupOriginShiftRuntimeState`, and `AupOriginShiftTelemetryEntry` to explicit 8-byte-friendly layouts. Ensured origin-shift Vault buffers before `LockAllocationsForAupShift()` so the lock does not hide a cold allocation failure.
Rejected Alternatives: A background runtime file watcher was rejected because cross-thread NativeArray mutation and managed watcher allocations create a worse contract than editor/dev cold reload. Keeping sequential layout was rejected because ARM64 alignment cannot be left to hope. Allocating after the vault lock was rejected because it makes the lock ceremonial instead of protective.
Scalability potential: Low/Toaster gets zero filesystem work in gameplay PRE_SIM and 10k rebase chunks; Middle keeps editable threshold/batch tuning; High runs full native rebase without layout traps; Ultra spends saved CPU on visual continuity while gameplay truth remains unchanged.
Hardware Impact: i3/MX350 avoids MicroSD stat/read calls in gameplay and removes misalignment risk on ARM64/Quest-class chips. Expected gameplay tick I/O cost is 0 us; layout correction is a correctness/perf-risk removal, not a measured profiler claim.

## Decision 012 - Time-Sliced Hot Cache and Frame Truth

Problem: The low-tier time-slice path rebased `AUP_StateDTO` batches but skipped `VaultHotEntityData.LocalPosition` when `SystemHealthIndex01 > 0.85`, leaving hot local caches in the old coordinate epoch. The blackbox also behaved like an event recorder, not a true last-300-frame ring.
Solution: Added matching hot-cache rebase slices for the same start/count window used by `AUP_StateDTO`, with `Velocity` deliberately untouched. Added `RecordFrameTelemetry()` in PRE_SIM so the 300-slot ring always contains recent frame state, while rebase commit overwrites the current frame slot with shift-specific evidence.
Rejected Alternatives: Shifting all hot entities in one stressed frame was rejected because it defeats Task 13. Recomputing velocity from pre/post local positions was rejected because origin shifts are coordinate-frame changes, not motion. Keeping event-only telemetry was rejected because it fails the blackbox law for the preceding 300 frames.
Scalability potential: Low/Toaster gets bounded 10k AUP + hot-cache slices; Middle can raise batch size through CSV; High/Ultra can run a full batch and use saved CPU for visual continuity while gameplay truth stays deterministic.
Hardware Impact: Avoids stale hot-cache correction storms on i3/MX350 and adds one 128B native telemetry write per PRE_SIM frame. No GC or file I/O is added to the gameplay tick.

## Decision 013 - Release I/O Guard and Dual Dump Evidence

Problem: The editor/dev CSV reload bridge was cold but still callable in release builds, and the blackbox fault path produced only the original `.bin` file while the mandate also required an `.h8dump` artifact.
Solution: Wrapped `TryReloadCsvOverrideFromDisk()` and `ReloadAupConstantsForTuner()` in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, wrapped the editor window in `#if UNITY_EDITOR`, and wrote both `Dump_ORIGIN_SHIFT.bin` and `Dump_ORIGIN_SHIFT.h8dump` from the same 300-entry native telemetry ring on watchdog/NaN fault.
Rejected Alternatives: A release file watcher or background poller was rejected because Steam Deck MicroSD and mobile storage cannot be allowed into gameplay timing. Replacing the mandated `.bin` with `.h8dump` was rejected because the original task explicitly named `Dump_ORIGIN_SHIFT.bin`.
Scalability potential: Low/Toaster has no release gameplay file polling; Middle keeps editor/development hot reload; High/Ultra can consume richer post-mortem tooling from `.h8dump` without changing the hot rebase path.
Hardware Impact: Release gameplay CSV I/O remains 0 us and 0 B/frame. Fault dumping stays off the normal path; when it triggers, two sequential writes are acceptable because the system is already in a forensic failure branch.

## Decision 014 - Cardinality Truth, Vault Owner Reset, and AUP Signal Alignment

Problem: The blackbox entity count could double-count one authority row and one hot-cache row as two shifted entities. Static origin-shift handles also survived `IDataVault` owner changes, which could trip the Vault stale-handle fatal path during PlayMode reloads or test vault swaps. The existing AUP signal corridor still used `Pack=1` on `AupPreShiftSignal`, `AupShiftSignal`, and `MemoryAddressShiftSignal`.
Solution: Split telemetry into `EntitiesShifted` for AUP authority rows and `HotEntitiesShifted` for hot-cache rows while preserving the 128-byte telemetry row. Added `ResetVaultHandles()` when `EnsureRuntimeState()` sees a different vault owner. Converted the three AUP corridor signal structs to explicit 32-byte layouts without changing fields, queue APIs, or payload size.
Rejected Alternatives: Keeping one inflated entity counter was rejected because blackbox evidence must not lie. Letting `ResolveBuffer()` detect stale handles was rejected because it would dump/fatal on a recoverable owner swap. Creating a new SHINOBU-only signal was rejected because it would fragment the signal corridor.
Scalability potential: Low gets truthful time-slice evidence and safe test reloads; Middle/High/Ultra can correlate AUP authority shifts, hot-cache shifts, and historical-point shifts without changing gameplay math.
Hardware Impact: Counter split costs no extra hot allocation and uses former padding. Vault owner reset is a cold branch and prevents fatal stale-handle recovery cost. Explicit signal layouts remove `Pack=1` risk from the AUP lane without changing queue capacity.
