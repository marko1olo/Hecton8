# Agent 1707 Rationale

State: IMPLEMENTED / STATIC VERIFIED / BUILD BLOCKED BY HOST GATE

Problem: Initial state files missing.
Solution: Created explicit status and rationale files before source edits. This satisfies disk-backed memory requirements for the 1707 batch lane.
Rejected Alternatives: Proceeding from chat memory only; rejected because context compression would erase assignment state.
Scalability potential: No runtime impact.
Hardware Impact: 0 us runtime; documentation-only.

Problem: Initial source state did not match the prompt's exact type and path names.
Solution: Resolved live files by CLI scan. Current quest runtime uses `GraphResolverJob` inside `QuestDagResolverRuntime.cs`; no `QuestStateDTO` exists yet. Scanner/PDA sync uses root `ScannerTool.cs`, `Gameplay/ScannerDataMiningRouter.cs`, and `UI/PDAEncyclopediaStreamer.cs`.
Rejected Alternatives: Blindly creating a parallel `EvaluateQuestOverlapJob` or editing a nonexistent `Tools/Scanner/ScannerToolRuntime.cs`; both would create dead code or compile churn.
Scalability potential: Low/Middle/High/Ultra all benefit from extending the existing contiguous Vault/Burst path instead of a second route.
Hardware Impact: Static audit only. Runtime 0 us. Avoided future duplicate scan/quest dispatch cost.

Problem: Scanner-to-PDA route still has legacy bridge calls adjacent to the typed `SignalBus<LoreFragmentScannedSignal>` path.
Solution: Keep the typed lane as authority and treat legacy `ScanEvents.TryRaiseEntryDiscovered` as compatibility only unless source edits prove it can be safely removed without breaking existing consumers.
Rejected Alternatives: Removing legacy events immediately; current source has old consumers and editor validators, so hard deletion risks unrelated regressions.
Scalability potential: Low tier consumes bounded typed snapshots; Ultra may add presentation-only haptics/glitch without changing unlock truth.
Hardware Impact: Typed lane is already O(1) bounded. Removing duplicate bridge later saves one native queue write per scan completion, estimated <1 us on i3/MX350.

Problem: PDA unlock path mutates Vault-backed unlock masks and metadata through raw resolved refs without a write-lock fence.
Solution: Refactor `UnlockEntry` mutations behind narrow `TryAcquireWriteLock`/`finally ReleaseWriteLock` scopes, keeping binary search/prerequisite work outside the lock.
Rejected Alternatives: Broad lock around signal consumption and UTF-8 lookup; that would block compaction longer and increase frame risk.
Scalability potential: Low tier fails closed during compaction; Ultra can add visual/haptic unlock presentation after deterministic bit mutation.
Hardware Impact: Extra lock checks cost estimated 1-3 us per unlock event, not per frame; prevents stale pointer mutation during compaction.

Problem: Task 1-5 compile checkpoint was requested, but host was above build gate.
Solution: Sampled CPU and compiler processes. CPU was 82 and two `dotnet` processes existed, so no build was launched.
Rejected Alternatives: Starting `dotnet build` anyway; forbidden when CPU >50 or compiler process is active.
Scalability potential: No runtime impact.
Hardware Impact: Avoided build contention on host.

Problem: Prompt referenced `QuestStateDTO` BufferID 75210, but current `H8Memory.BufferID` owns QuestDag IDs contiguously at 70150-70165.
Solution: Added named `QuestDagQuestStates = 70166` and `QuestDagDependencyLinks = 70167` beside existing QuestDag buffers. Extended `QuestDagBufferHandles`, `QuestDagBuffers`, scheduler pins, release, and resolver inputs.
Rejected Alternatives: Casting magic `(BufferID)75210`; rejected because it hides ownership and risks future ID collision outside the existing QuestDag range.
Scalability potential: Low uses 64 active quest states and binary lookup only; Middle/High/Ultra can raise capacities without changing DTO layout or route ownership.
Hardware Impact: Additional persistent native memory is 64*16 + 10000*16 bytes by default, cold allocated. Per-frame overlap scan is <=64 quest states plus O(log N) link lookup per armed state; estimated <8 us on i3/MX350, lower when QuestStateCount is zero.

Problem: The prompt named `EvaluateQuestOverlapJob`, but live resolver uses `GraphResolverJob` as the Burst truth owner.
Solution: Integrated `EvaluateQuestOverlap` into `GraphResolverJob`, which already has `[BurstCompile(CompileSynchronously = true, FloatMode = Fast, FloatPrecision = Standard)]`. The overlap pass writes only the unmanaged `QuestStateDTO` snapshot and telemetry counters.
Rejected Alternatives: Creating a second job with unmanaged aliases to the same quest truth; rejected because it would create scheduling order risk and duplicate DataVault pinning.
Scalability potential: Low tier can keep `QuestStateCount` low or zero; Ultra can populate more active presentation states while DAG truth remains bitmask-owned.
Hardware Impact: Zero managed allocations. On armed state, one binary search over sorted links costs roughly 14 comparisons at 10k links.

Problem: Dependent quest lookup needed a hot unmanaged route without managed dictionaries.
Solution: Populated a cold sorted `QuestDependencyLinkDTO` table from `QuestNodeDTO.RequiredStateHash -> NodeHash`. Emergency mock DAG now uses previous-node hashes as parents so tests prove the chain. Missing child writes `ZeigarnikFailClosedCount` instead of inventing an ID.
Rejected Alternatives: LINQ/order-by or managed `Dictionary<uint,uint>`; rejected for allocation and cache locality violations.
Scalability potential: Low/Middle/High/Ultra share the same table. High/Ultra can spend saved CPU on HUD presentation only, not gameplay truth.
Hardware Impact: Cold shell sort cost occurs on load only. Runtime lookup is O(log N), estimated 1-3 us for one armed state on i3/MX350.

Problem: PDA accepted any scanned uint and reserved metadata/unlock bits before proving lore payload existence.
Solution: Added a zero-copy hash gate against AppliedLore DataMonolith, H8LR, Babel B-tree-backed dictionary, and mock UTF-8 before unlock mutation. `0xFFFFFFFF` records `FaultInvalidHash` and forces fail-closed UI state.
Rejected Alternatives: Allowing empty PDA entries for unknown scannables; rejected because it pollutes persistent lore state and hides bad scanner data.
Scalability potential: Low tier pays only bounded lookup on scan events; Ultra can add presentation effects after the same deterministic bit flip.
Hardware Impact: Babel lookup uses existing B-tree cache path, O(log N), no managed strings. Estimated <10 us per scan event at 10k entries on i3/MX350.

Problem: PDA unlock and encrypted-dependent promotion wrote DataVault-backed structs through raw refs without write-lock ownership.
Solution: Added ordered write-lock acquisition for unlock mask, runtime state, and metadata, then mutate local DTO copies and assign them back under `try/finally`. Locks release in reverse order.
Rejected Alternatives: Broad lock around signal snapshot iteration or UTF-8 source lookup; rejected because it would hold the compaction fence longer than necessary.
Scalability potential: Low tier fails closed if compaction is active; Middle/High/Ultra get identical persistent truth with optional richer visuals.
Hardware Impact: Lock overhead is event-bound, not per frame. Estimated 2-5 us per lore unlock on i3/MX350; removes stale pointer mutation risk.

Problem: Lore unlock needed tactile feedback without managed input callbacks.
Solution: Initialized the existing haptic lane and pushed one 16-byte `HapticPulseSignal` when an unlock bit flips from locked to unlocked.
Rejected Alternatives: Calling a rumble component directly or raising UnityEvent; rejected for cross-domain coupling and allocation risk.
Scalability potential: Low receives the same short pulse; Ultra haptic synthesizer can enrich output downstream without changing PDA truth.
Hardware Impact: One bounded SignalBus write per new lore unlock, estimated <1 us on i3/MX350.

Problem: Task 6-10 compile checkpoint was requested, but host was still above build gate.
Solution: Re-extracted the 1707 XML block from `CURRENT_BATCH.md`, sampled CPU and compiler processes. CPU was 100 and multiple `dotnet` processes existed, so no build was launched.
Rejected Alternatives: Starting `dotnet build` under contention; forbidden by batch prompt.
Scalability potential: No runtime impact.
Hardware Impact: Avoided build contention on host.

Problem: PDA encrypted-dependent promotion would have nested write locks with normal read accessors if left as first patched.
Solution: Added `AreAppliedLorePrerequisitesSatisfiedLocked`, `TryFindBitIndexLocked`, and `ReadMaskWord` so promotion evaluates prerequisites from the already-locked metadata array and local mask copy.
Rejected Alternatives: Calling `IsUnlocked()` while write locks are held; rejected because it could re-enter DataVault resolution under a lock and increase deadlock risk.
Scalability potential: Low devices avoid lock nesting; High/Ultra get identical promotion behavior with deterministic mask reads.
Hardware Impact: Same O(256 * prerequisite_count) promotion bound, but no nested DataVault lookup. Estimated 5-15 us per promotion pass on i3/MX350.

Problem: Task 19 demanded presentation quality scaling, but no assigned-file quest HUD consumer for `InjectedSubQuestHashID` exists.
Solution: Kept quest truth unscaled and documented that PDA presentation already consumes continuous `GlobalQualityWeight`. Did not add quality fields to `QuestStateDTO`.
Rejected Alternatives: Adding `GlobalQualityWeight` to `QuestStateDTO`; rejected because it breaks the exact 16-byte DTO required by Task 08 and would mix presentation with gameplay truth.
Scalability potential: Low/Middle/High/Ultra differ only in presentation systems that already read quality; DAG state remains deterministic.
Hardware Impact: 0 us added to quest runtime.

Problem: Need proof for the 0.94/0.95 overlap and invalid hash edge without a permitted build.
Solution: Dry-ran the masks: progress 0.94 -> `progressMask=0`, `injectMask=0`; progress 0.95 with missing child -> fail-closed flag/counter; `0xFFFFFFFF` hash -> reject before metadata reservation.
Rejected Alternatives: Claiming compile/test proof without running compiler; rejected as fake evidence.
Scalability potential: No runtime impact.
Hardware Impact: Static reasoning only.

Problem: Final compile remained prohibited by host state.
Solution: Sampled final gate. CPU was 100 and active `dotnet` PIDs `3100` and `15308` existed. `dotnet build` was not launched. Touched-file `git diff --check` passed; full-tree diff check remains dirty due unrelated scene/CURRENT_BATCH whitespace.
Rejected Alternatives: Running build under active compiler load; forbidden. Editing unrelated scene whitespace; rejected because it belongs to other agents.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: Final report had to include objective hashes and exact line evidence.
Solution: Wrote `Docs/Reports/ZEIGARNIK_AND_LORE_PURIFICATION_1707.json`, validated it with `ConvertFrom-Json`, and included SHA-256 hashes for the six modified source/test files.
Rejected Alternatives: Chat-only report; rejected by reporting protocol.
Scalability potential: No runtime impact.
Hardware Impact: Documentation-only.

Problem: APEX polish found the PDA unlock path still held three DataVault writer locks at once.
Solution: Replaced the ordered multi-lock helper with snapshot planning and four single-buffer writer helpers: metadata entry, runtime state, unlock mask, and promoted metadata flags. Each helper has exactly one `TryAcquireWriteLock`, one `ReleaseWriteLock`, and a strict `finally`.
Rejected Alternatives: Keeping ordered multi-lock acquisition; rejected because it still leaves a deadlock vector if another system ever acquires the same buffers in a different order.
Scalability potential: Low devices now fail closed on one contended buffer instead of holding three fences. Middle/High/Ultra keep the same unlock truth and can add presentation effects downstream.
Hardware Impact: One unlock may perform up to three short lock scopes instead of one wide scope. Estimated +1-3 us per unlock event on i3/MX350, traded for removal of nested writer-lock risk and shorter compaction stalls.

Problem: `TryEnsureBitIndex` could reserve PDA metadata through raw vault refs, bypassing the new single-lock discipline.
Solution: Reworked bit-index reservation to read metadata/state/mask snapshots, plan local DTO copies, then commit through the same single-buffer writer helpers.
Rejected Alternatives: Treating the helper as cold-only; rejected because metadata seeding and unlock maintenance paths share the same helper.
Scalability potential: Low/Middle/High/Ultra get identical metadata ownership. Contention only drops the current reservation attempt; no partial unlock bit is exposed.
Hardware Impact: Event-bound only. Estimated <4 us extra on i3/MX350 for a new metadata slot.

Problem: Zeigarnik injection lacked tactile presentation despite the original task requiring haptics on new objective injection.
Solution: Added a post-job haptic bridge in `QuestDagResolverService.CompleteScheduled`, gated by `QuestDagTelemetryFlags.ZeigarnikInjected`. The Burst job remains data-only.
Rejected Alternatives: Publishing haptics inside `GraphResolverJob`; rejected because signal publication belongs after job completion and outside simulation mutation.
Scalability potential: Low receives one short pulse; Ultra haptic synthesizer can enrich it through existing downstream quality-weighted synthesis.
Hardware Impact: One bounded `SignalBus<HapticPulseSignal>` write per injected quest, estimated <1 us on i3/MX350.

Problem: Build remained prohibited during polish verification.
Solution: Sampled CPU and compiler state again. CPU was 100 and active `dotnet` PID `3100` existed, so no build was launched.
Rejected Alternatives: Running `dotnet build` under active compiler load; forbidden by batch prompt.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: Production quest UI route was still split between the native QuestDag resolver snapshot and the live `QuestManager`/`QuestEvents` owner.
Solution: Integrated Zeigarnik child activation into `QuestManager` so the child quest becomes active before the completed event is drained in late frame. The existing `QuestEvents` and marker/HUD listeners remain the presentation path.
Rejected Alternatives: Adding a new HUD overlay, coroutine, or manager for Zeigarnik prompts; rejected because it duplicates the quest presentation topology and can race the victory/completion listener order.
Scalability potential: Low/Middle/High/Ultra keep one quest truth owner. Low devices pay only event-bound authored-array scans; High/Ultra can enrich downstream HUD/haptics without changing quest state authority.
Hardware Impact: Completion-time scan is O(authored quest count), not per frame. Estimated 5-25 us on i3/MX350 for typical quest registries; zero steady-state Tick cost.

Problem: `QuestManager` initially released shared QuestDag DataVault buffers while acting only as a snapshot publisher.
Solution: Changed `ReleaseQuestDagSnapshotHandles` to clear local generation handles only. `QuestDagResolverService` remains the owner that may call `QuestDagVault.ReleaseBuffers` when its scheduled jobs are complete.
Rejected Alternatives: Calling `QuestDagVault.ReleaseBuffers` from every publisher; rejected because `GlobalDataVault.EnsureGenerationHandle` does not increment `RefCount` for existing buffers, so a publisher could free shared buffers owned by the resolver route.
Scalability potential: Low devices avoid rare route teardown corruption; Middle/High/Ultra retain shared native memory continuity across hot-swap/unbind.
Hardware Impact: 0 us steady-state. Avoids full buffer deallocation/reallocation churn on enable/disable and hot-swap.

Problem: Repeated QuestDag snapshot handle checks could re-count authored dependency strings during event-bound publishes.
Solution: Cached authored dependency-link count during `BuildLookup` and reused it as the DataVault capacity hint.
Rejected Alternatives: Counting quest prerequisites on each snapshot publish; rejected as unnecessary string traversal on the late-frame transition path.
Scalability potential: Low devices avoid repeated managed asset-array scans; High/Ultra retain capacity for denser authored quest graphs.
Hardware Impact: Saves an O(authored prerequisite count) scan on repeated transition snapshots; estimated 2-15 us per publish on i3/MX350.

Problem: Final verification after production integration was still blocked from compiling.
Solution: Ran static gates instead: hot-window forbidden-token scan, writer lock-scope count, touched-file `git diff --check`, targeted orphan `.meta` scan, contract symbol grep for haptic/DataVault/QuestDag constants. CPU was 95.3 with active `dotnet` PID `3100`, so build was not launched.
Rejected Alternatives: Starting `dotnet build` under active compiler/CPU contention; explicitly forbidden.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: Final pre-response host gate remained closed.
Solution: Re-sampled CPU/compiler state. CPU was 99.0 with active `dotnet` PID `3100`. No build or test process was started.
Rejected Alternatives: Violating the build throttle to obtain a compile stamp; rejected because the prompt explicitly forbids build under CPU/compiler contention.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: `QuestManager` still had null-unsafe quest registry access paths around presentation cache refresh, quest-index resolution, and lookup rebuild.
Solution: Changed `RefreshQuestPresentationCaches`, `GetQuestDataByIndex`, and `BuildLookup` to use local null-guarded quest arrays before length/index access.
Rejected Alternatives: Relying on the serialized `Array.Empty<QuestData>()` initializer; rejected because editor/import tools can still leave serialized arrays null during validation or damaged asset load.
Scalability potential: Low/Middle/High/Ultra avoid a hard null crash while keeping the same authored quest truth and notification cache route.
Hardware Impact: `Array.Empty<QuestData>()` is static and non-allocating; runtime cost is one null branch on cold/cache rebuild paths, estimated <1 us.

Problem: `MissionManager` listened to activated quest events but only removed completed state; old mission UI consumers could miss Zeigarnik-injected quests because `_activeMissions` was not materialized.
Solution: On `QuestEventType.Activated`, resolve the authored id through cached `IQuestSystem.TryGetQuestIdByHash` and call the existing `EnsureActiveInstance` cache path after confirming the quest is active.
Rejected Alternatives: Polling `GlobalRegistry` or `GetComponent()` from the event handler; rejected by cold-DI doctrine and because the interface already exposes the required pure read.
Scalability potential: Low devices pay only one event-bound dictionary/id lookup on activation; High/Ultra can enrich downstream mission presentation without changing quest authority.
Hardware Impact: Zero steady-state Tick cost. Activation path may allocate one existing `MissionInstance` wrapper, already documented as cold compatibility cache allocation.

Problem: Continued verification was requested, but compile gate stayed closed.
Solution: Ran static gates instead: brace counts, hot-window forbidden-token scan, writer-lock scope counts, touched-file `git diff --check`, targeted orphan `.meta` scan, and contract symbol grep. CPU was 100 with active `dotnet` PIDs `3100` and `12768`, so build was not launched.
Rejected Alternatives: Starting `dotnet build` under active compiler/CPU contention; explicitly forbidden.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: Latest pre-response compile gate remained closed after docs were updated.
Solution: Re-sampled CPU/compiler state. CPU was 100 with active `dotnet` PIDs `3100` and `19708`. No build or test process was started.
Rejected Alternatives: Running a compiler for a cleaner final message; rejected by build-throttle rule.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: Zeigarnik-injected child activation updated quest state and emitted an event but did not enter the quest transition audit ring.
Solution: Record the injected child activation through `QuestStateManager.RecordManualTransition` immediately after successful child activation.
Rejected Alternatives: Treating the emitted HUD event as enough proof; rejected because black-box/audit state must reflect the actual mutation path.
Scalability potential: Low/Middle/High/Ultra get identical audit proof with no presentation coupling.
Hardware Impact: One fixed-ring struct write per injected quest, estimated <1 us.

Problem: Quest transition audit retained only 128 entries while the project black-box standard and QuestDag telemetry capacity are 300 samples.
Solution: Bound `QuestTransitionAuditCapacity` to `QuestDagRuntimeConstants.TelemetryCapacity`.
Rejected Alternatives: Keeping a smaller local constant; rejected because it creates two black-box retention standards inside the same quest domain.
Scalability potential: Low devices pay a small cold memory increase; High/Ultra retain deeper diagnosis under event bursts.
Hardware Impact: Cold array grows by 172 audit structs. No steady-state allocation.

Problem: Quest event and mission compatibility caches still used legacy small capacities after Zeigarnik began emitting linked activation events.
Solution: Prewarm `MissionManager` caches and `QuestEvents` pending queues to `QuestDagRuntimeConstants.DefaultQuestStateCapacity`.
Rejected Alternatives: Letting dictionaries/queues grow or drop under bursty completions; rejected due allocation/drop risk.
Scalability potential: Low devices avoid runtime growth; High/Ultra can handle denser authored objective bursts.
Hardware Impact: Cold memory increase only. Avoids NativeQueue pending overflow at 16 events and Dictionary growth past 32 active missions.

Problem: Capacity-polish verification was requested, but compile gate stayed closed.
Solution: Ran static gates instead. CPU was 97 with active `dotnet` PID `3100`, so build was not launched.
Rejected Alternatives: Starting `dotnet build` under active compiler/CPU contention; forbidden.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: `MissionManager` could register after quests were already active and keep an empty compatibility cache until a future activation event arrived.
Solution: Expanded `IQuestSystem` with the existing quest-owner non-alloc active-hash copy route, made `QuestManager.CopyActiveQuestHashes` public, and resynced `MissionManager` from a preallocated `uint[64]` scratch on cold bind/hot-swap.
Rejected Alternatives: Concrete-casting to `QuestManager`, polling `GlobalRegistry` every frame, or duplicating a quest-state mirror; rejected because the interface route keeps one owner and no hot lookup.
Scalability potential: Low/Middle/High/Ultra all get deterministic mission UI continuity after hot registration. Higher tiers can add presentation effects downstream without changing quest truth.
Hardware Impact: Zero steady-state Tick cost. Cold/hot-swap resync scans at most 64 active quest hashes and may allocate existing `MissionInstance` wrappers only for compatibility cache misses.

Problem: `QuestEvents` capacity comments still claimed `[16]` after the queue capacity was raised.
Solution: Replaced the two NativeQueue COLD ALLOC comments with `[64]` ASCII comments matching `QuestDagRuntimeConstants.DefaultQuestStateCapacity`.
Rejected Alternatives: Leaving stale capacity comments; rejected because allocation declarations are used as local proof artifacts.
Scalability potential: No runtime impact.
Hardware Impact: Documentation-only, no code execution change.

Problem: Mission cache resync could leave stale completed hashes after quest-system replacement.
Solution: Clear `_completedMissions` together with `_activeMissions` inside `RefreshMissionCacheFromQuestSystem` before active hashes are copied from quest authority.
Rejected Alternatives: Keeping completed cache as fallback truth while quest service changes; rejected because stale compatibility state can contradict the authoritative quest system.
Scalability potential: Low/Middle/High/Ultra get one mission truth after hot-swap. No per-frame reconciliation is introduced.
Hardware Impact: Cold/hot-swap `HashSet.Clear()` only; zero steady-state Tick cost.

Problem: `MissionMarkerSystem` still depended on concrete `QuestManager` and only reacted to `QuestRuntime` replacement.
Solution: Bind markers through `IQuestSystem`, use `GlobalRegistry.QuestSystem` for cold cache, and handle both `QuestRuntime` and `QuestSystem` hot-swap slots.
Rejected Alternatives: Keeping concrete quest runtime access; rejected because marker presentation only needs the public non-alloc copy/presentation interface.
Scalability potential: Low devices avoid stale marker state after service replacement; higher tiers can add marker visuals without changing quest authority.
Hardware Impact: No steady-state cost. Interface dispatch occurs in existing marker rebuild/event paths only.

Problem: Marker presentation cache could survive quest authority replacement and reuse stale target positions for matching quest hashes.
Solution: Added `ResetQuestMarkerState` and call it when `CacheQuestRuntime` observes a different `IQuestSystem`, clearing fixed active hashes, cache keys, cache DTOs, and matrices.
Rejected Alternatives: Waiting for future quest events to overwrite marker caches; rejected because marker cache lookup can return stale data before any event arrives.
Scalability potential: Low/Middle/High/Ultra get deterministic marker presentation after hot-swap without per-frame cache validation.
Hardware Impact: Cold/hot-swap loop over 32 fixed slots only; zero steady-state Tick cost.

Problem: `MissionMarkerSystem` still lazy-created `MaterialPropertyBlock` through a helper reachable from `Render` after a late material assignment.
Solution: Move the `MaterialPropertyBlock` to readonly field initialization and remove the lazy helper from render-reachable code.
Rejected Alternatives: Keeping a null guard and relying on normal initialization order; rejected because late resource assignment can cross the helper from the render path.
Scalability potential: Low/Middle/High/Ultra retain the same marker visuals with no frame-time allocation spike.
Hardware Impact: Removes one possible managed allocation from the render path. Cold allocation remains one object per marker system instance.

Problem: `HectonNarrativeDirector` gated HUD POI breadcrumbs by converting quest hash to managed quest id and then querying `IsActive(string)`.
Solution: Query `IQuestSystem.IsActive(uint)` directly on the existing quest hash.
Rejected Alternatives: Keeping the string route for readability; rejected because the hash-native route already exists and avoids repeated string traversal on the HUD waypoint gate.
Scalability potential: Low/Middle/High/Ultra keep the same breadcrumb behavior with less CPU work in narrative presentation.
Hardware Impact: Removes one dictionary string-return path and one string-hash path from each quest-gated HUD POI check; zero steady-state allocation added.

Problem: `MissionMarkerSystem.RegisterRuntime` blocked edit-mode `IUpdatable` registration but still allowed edit-mode `IRenderable` registration into `GlobalRegistry.Renderables`.
Solution: Gate renderable registration with `Application.isPlaying` in the same method.
Rejected Alternatives: Relying on `OnDisable` to unregister editor-time entries; rejected because global render buckets should not accept play-runtime presentation systems outside play mode.
Scalability potential: Low/Middle/High/Ultra avoid stale marker render callbacks after editor enable/disable churn.
Hardware Impact: Zero runtime cost; removes an editor-time global registry contamination path.

Problem: Quest and scanner marker renderers still used per-draw `MaterialPropertyBlock` mutation for `_BaseColor`, `_FlickerFrequency`, and `_FlickerIntensity` while their shader already declares those values in `UnityPerMaterial`.
Solution: Removed MPB fields, dirty flags, property IDs, and material-apply helpers from `MissionMarkerSystem` and `HectonScanMarkerSystem`; both `DrawMeshInstanced` calls now pass `null` MPB and consume authored instanced material constants.
Rejected Alternatives: Keeping MPB as a cold allocation; rejected because the renderer mandate forbids MPB on instanced/standard geometry and the shader path already supports material-owned CBUFFER constants.
Scalability potential: Low devices avoid SRP batch breaks and CPU property-block traffic; Middle/High/Ultra keep the same marker shader and can tune visuals through material assets without changing code.
Hardware Impact: Removes per-visible-draw property-block mutation and one possible managed allocation route. Estimated 2-8 us saved on i3/MX350 during scanner/quest marker presentation frames.

Problem: `ScannerTool` initializes `HectonScanMarkerSystem` with mesh/material, but the held scanner prefab still serialized the old `scannerMarkerShader` field and a dynamically added marker component could assert before `Initialize`.
Solution: Updated `Tool_Scanner_Held.prefab` to reference existing `M_ScannerMarkerQuad` mesh and `MAT_HUD_ThreatChevronInstanced` material, and made `HectonScanMarkerSystem` fail-soft before marker refs are configured.
Rejected Alternatives: Restoring runtime mesh/material synthesis; rejected because runtime asset creation was the original allocation and asset-lifecycle fault.
Scalability potential: Low/Middle/High/Ultra get deterministic scanner marker startup from authored assets; higher tiers can overdrive material visuals without source churn.
Hardware Impact: Removes dynamic component pre-initialize assert noise and avoids runtime mesh/material creation. Estimated 0 us steady-state, one avoided cold allocation burst on first scanner setup.

Problem: `ScannerTool.UpdatePowerIndicator` read the renderer property block and wrote it again whenever the dirty bit was queued, including brownout frames where the resolved emission had not changed.
Solution: Cache the last applied emission color, compute the target color without allocation, remove `GetPropertyBlock`, and skip `SetPropertyBlock` when color delta is below `0.0001f`.
Rejected Alternatives: Removing the power indicator MPB completely; rejected for this pass because it is an existing narrow tool-indicator path, not the instanced marker geometry path already migrated to authored material constants.
Scalability potential: Low/Middle avoid repeated native property-block traffic during scanner brownout and equip/spawn churn; High/Ultra keep identical indicator behavior and can still drive authored emission values.
Hardware Impact: Removes one renderer property-block read and most redundant writes from `LateFrameTick` power presentation. Estimated 1-3 us saved on weak CPUs during sustained low-battery scanner frames.

Problem: Latest verification was requested while the host build gate remained closed.
Solution: Performed static method scans, prefab GUID validation, targeted hot-token checks, and diff hygiene. CPU sampled at 91.5 with active `VBCSCompiler` PID `31612`, so no build was launched.
Rejected Alternatives: Running `dotnet build` under active compiler/CPU contention; rejected by the build throttle.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: PDA scan signal ingestion validated a lore hash payload and then called the default `UnlockEntry` overload, which repeated the same DataMonolith/H8LR/Babel payload probe before planning the unlock.
Solution: Keep the fail-closed prevalidation in `ConsumeScanSignals`, then call the internal `UnlockEntry` overload with `validatePayload:false`.
Rejected Alternatives: Removing prevalidation and relying only on `UnlockEntry`; rejected because the signal loop needs explicit rejection and black-box fault routing before mutation planning.
Scalability potential: Low/Middle avoid duplicate O(log N) or store lookup probes per scan signal; High/Ultra keep identical unlock presentation and haptic behavior.
Hardware Impact: Removes one repeated payload validation per accepted scan/lore signal. Estimated 2-6 us saved under dense scanner signal bursts.

Problem: `PDAEncyclopediaStreamer.RecordTelemetry` wrote telemetry ring and cursor through raw `GetVaultElementRef` refs during `LateFrameTick`.
Solution: Resolve immutable snapshots first, build `PdaEncyclopediaTelemetryEntry` outside locks, then acquire/release the telemetry row write lock and telemetry cursor write lock in two separate try/finally scopes.
Rejected Alternatives: Holding telemetry row and cursor locks simultaneously; rejected because the DataVault rule forbids nested write-lock ownership.
Scalability potential: Low/Middle avoid compaction-fence races in PDA telemetry; High/Ultra keep the same 300-frame black-box signal with deterministic lock windows.
Hardware Impact: Adds two lightweight lock acquisitions to telemetry recording but removes unsafe raw writes in the visual phase. Assignment-only lock bodies keep stall risk bounded.

Problem: Final PDA/marker gate could not use the compiler because the host stayed above the build throttle.
Solution: Re-ran static gates only. `RecordTelemetry` range scan reports `GetVaultElementRef=0`, `TryAcquireWriteLock=2`, `ReleaseWriteLock=2`, `finally=2`; brace scan is balanced for touched source files; targeted `.meta` scan reports `ORPHAN_META_COUNT=0`; touched-file `git diff --check` reports CRLF warnings only.
Rejected Alternatives: Launching `dotnet build` with CPU at 100 and active compiler processes; rejected by build-throttle rule.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention; no game runtime change.

Problem: `PDAEncyclopediaStreamer` still had cold/editor `GetVaultElementRef` write paths after the scan-signal and telemetry lock flattening.
Solution: Removed the raw-ref helper and converted editor snapshot/bulk lock, cold buffer reset, DataMonolith/H8LR seeding, mock seed counters, and CSV metadata import to snapshot planning plus single-buffer writer helpers.
Rejected Alternatives: Treating cold/import paths as exempt from DataVault discipline; rejected because compaction fences do not care whether the caller is editor, bootstrap, or runtime.
Scalability potential: Low/Middle/High/Ultra share one PDA mutation route. Low devices fail closed on a contended buffer; higher tiers can enrich presentation without changing lore truth.
Hardware Impact: Event/cold path adds short lock acquisitions only. Removes stale alias write risk and nested-lock collision path; estimated 0 us steady-state frame cost.

Problem: Mock fallback and editor CSV ingestion wrote Vault-owned byte/index buffers directly.
Solution: Mock seed now clears/writes index rows and UTF8 bytes through separate single-buffer write locks. CSV ingestion reads file chunks into a stack buffer, then copies each chunk into the Vault scratch buffer under a short write lock, so file I/O is outside the lock.
Rejected Alternatives: Holding a scratch-buffer lock across `FileStream.Read`; rejected because I/O under a vault lock can stall compaction and editor responsiveness.
Scalability potential: Low devices avoid lock stalls and raw aliasing in fallback paths. High/Ultra keep identical mock/CSV authoring behavior.
Hardware Impact: Cold/editor only. Chunked copy costs more calls than one raw span read but removes unowned Vault mutation; no gameplay steady-state cost.

Problem: `HectonScanMarkerSystem` still carried `_InstanceData` / `MaterialPropertyBlock` payload after the authored-material marker conversion.
Solution: Removed the instance-data property id, Vector4 mirror, MPB field, `SetVectorArray`, and per-visible instance writes. Scanner marker draw now passes `null` MPB; fade remains encoded in matrix scale.
Rejected Alternatives: Keeping per-instance alpha payload; rejected because marker shader already has authored material constants and scale-fade preserves the visible behavior without SRP-batcher-breaking state.
Scalability potential: Low/Middle avoid property-block traffic; High/Ultra can tune marker look through material assets.
Hardware Impact: Removes per-draw MPB mutation in scanner marker presentation; estimated 1-4 us saved on i3/MX350 marker frames.

Problem: Build verification remained unavailable after the 2026-06-03 code pass.
Solution: Sampled host gate. CPU was 100 and no build was launched. Static method-window scans reported `BAD_TOKEN_COUNT=0`; marker forbidden-token scan returned no hits; brace scan balanced; orphan `.meta` scan returned zero.
Rejected Alternatives: Running `dotnet build` despite CPU >50; rejected by explicit throttle rule.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: `ScannerTool.WriteScannerBlackBox` wrote the 300-frame black-box ring through a mutable resolved DataVault array from `ToolTick`.
Solution: Build `ScannerBlackBoxEntry` outside vault ownership, then commit one slot through `TryWriteScannerBlackBoxEntry`, which has one `TryAcquireWriteLock`, one assignment, and one `finally ReleaseWriteLock`. Dump/export reads now use `TryReadOnlyHandle`.
Rejected Alternatives: Keeping direct `TryResolveHandle` writes because the ring is diagnostic; rejected because black-box telemetry is still steady-state memory owned by DataVault.
Scalability potential: Low/Middle/High/Ultra get the same 300-frame scanner proof ring without stale alias writes; higher tiers can add richer scanner visuals without touching black-box truth.
Hardware Impact: One short write lock per scanner tool tick replaces raw alias write. Estimated +0.5-1.5 us on i3/MX350, traded for compaction safety.

Problem: `ScannerDataMiningRouter` could resolve vault views or initialize settings while the compaction/allocation fence was active.
Solution: Added explicit `IsCompactionFenceActive` gates to settings read/write and vault-view resolution; added `IsAllocationLocked`/compaction early-out before handle creation. Transient init failure now registers the existing `ColdTick` retry path instead of becoming a permanent missed init.
Rejected Alternatives: Adding per-buffer write locks around Burst job arrays; rejected because the router already owns a bounded mutation guard for batch job views and per-buffer locks would fragment the job path.
Scalability potential: Low devices fail closed during compaction and retry cold; Middle/High/Ultra keep batch scanner cadence and do not add hot service polling.
Hardware Impact: Zero added steady-state allocation. Fence checks are scalar branches; estimated <1 us per scanner router tick.

Problem: Scanner cold mock seeding and bucket clearing wrote DataVault-backed arrays without a scoped ownership fence.
Solution: Wrapped cold bucket clear in `ScannerQueryMutationGuardMask` and mock seed in `ScannerCompletionMutationGuardMask`, both with strict `try/finally` release. Pose/math/settings planning remain outside the guarded section where possible.
Rejected Alternatives: Leaving cold writes unguarded; rejected because DataVault compaction can overlap bootstrap/fallback paths, not only gameplay ticks.
Scalability potential: Low devices avoid bootstrap corruption; High/Ultra keep the same mock/scanner content path for richer presentation testing.
Hardware Impact: Cold path only. No per-frame cost after initialization succeeds.

Problem: QuestDag late-frame telemetry helpers read and patched vault-backed telemetry without an explicit compaction-fence test at the direct callsite.
Solution: Added `vault.IsCompactionFenceActive` fail-closed gates to direct telemetry cursor/ring reads and telemetry/counter write-lock callsites that bypass the central `QuestDagVault` helper.
Rejected Alternatives: Assuming `TryReadOnlyHandle`/`TryAcquireWriteLock` alone was enough; rejected because the 1707 protocol requires visible fence proof near direct vault access.
Scalability potential: All tiers skip one haptic/telemetry patch during compaction rather than risking stale telemetry view.
Hardware Impact: One scalar branch on late-frame completion path; estimated <0.2 us.

Problem: Build verification remained blocked after scanner vault polish.
Solution: Sampled host gate. CPU was 99.8, so no build was launched. Static gates: dangerous API sweep over Scanner/PDA/Quest/Marker targets returned no hits; scanner black-box mutable access scan shows only the writer helper; brace scan balanced; targeted `git diff --check` reports CRLF warnings only.
Rejected Alternatives: Running `dotnet build` with CPU >50; explicitly forbidden by the build throttle.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: Final static sweep initially produced false positives on `math.select`.
Solution: Re-ran the dangerous API scan case-sensitively. Result: `DANGEROUS_HIT_COUNT=0`. Verified marker geometry files separately: `MARKER_MPB_HIT_COUNT=0`. Verified `ScannerTool.TryWriteScannerBlackBoxEntry` lines 1330-1366: one write lock, one assignment window, one `finally` release.
Rejected Alternatives: Reporting the default PowerShell scan as-is; rejected because `Select-String` is case-insensitive and incorrectly matches `math.select`.
Scalability potential: No runtime impact.
Hardware Impact: Static verification only.

Problem: Final build gate stayed closed.
Solution: Sampled CPU/compiler state. CPU was 76.5 with no compiler processes, still above the 50 percent throttle, so no `dotnet build` was launched.
Rejected Alternatives: Running build because compilers were idle; rejected because CPU alone violates the throttle.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: Pre-final build gate recheck still failed.
Solution: Sampled CPU/compiler state again. CPU was 83.4 and active `dotnet` PID `32588` existed, so no `dotnet build` was launched.
Rejected Alternatives: Running build under active `dotnet`; rejected by compilation throttle and concurrent-agent safety.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: PDA scanner/lore path still used mutable DataVault resolves for snapshot-only planning and display probes.
Solution: Converted unlock planning, encrypted-dependent promotion, bit-index reservation, metadata seed/import reads, last-discovery AUP, fault-state readback, telemetry dump reads, buffer-resolvability probes, bootstrap magic checks, and mock index lookup to `TryReadVaultBuffer` / `TryReadOnlyHandle`. Commits still flow through existing single-buffer writer helpers.
Rejected Alternatives: Leaving mutable aliases because most paths were event/cold; rejected because compaction-fence safety should not depend on caller temperature when immutable snapshots are already supported.
Scalability potential: Low/Middle devices fail closed during compaction without stale aliases. High/Ultra keep identical lore unlock truth and can spend saved stability budget on richer PDA presentation.
Hardware Impact: Read-only handle checks add scalar branches only on event/cold paths. Steady-state frame cost remains 0 B GC; mutable alias exposure is reduced to mock UTF8 byte span and editor CSV scratch span.

Problem: Compilation was requested by protocol, but the host gate remained closed after the readonly snapshot pass.
Solution: Ran static gates only: remaining PDA mutable scan, hot-window forbidden-token scan, non-cold dangerous-token scan, brace counts, strict extension `.meta` scan, and targeted `git diff --check`. CPU was 100.0 with active `dotnet` PID `32588`; no build was launched.
Rejected Alternatives: Starting `dotnet build` despite CPU >50 and active `dotnet`; rejected by explicit build throttle.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.

Problem: `ScannerDataMiningRouter` kept settings in the same mutable view as job-owned scanner buffers.
Solution: Removed `ScannerSettingsDTO` from `ScannerVaultViews`, stopped resolving `_settingsHandle` through `TryResolveHandle`, and read settings through the existing `TryReadOnlyHandle` snapshot route before passing them into scanner jobs by value.
Rejected Alternatives: Keeping settings in the batch view because the buffer is small; rejected because it expands mutable alias ownership and couples tuning reads to scanner job buffer guards.
Scalability potential: Low/Middle devices keep short fail-closed compaction checks; High/Ultra can tune scanner cadence/quality without touching job-owned buffer views.
Hardware Impact: Removes one mutable handle resolve from scanner view refresh and keeps settings reads outside guarded job buffer ownership. Estimated <1 us saved on i3/MX350 per scheduled scanner query, with lower compaction collision risk.

Problem: `FastTick` resolved the whole scanner mutable view before acquiring the scanner query mutation guard, then resolved it again inside the guarded scheduling window.
Solution: Removed the pre-guard view resolve. `FastTick` now computes cadence from a value settings snapshot and resolves scanner views only after `TryAcquireQueryMutationGuard` succeeds.
Rejected Alternatives: Keeping the early view check to avoid a guard acquisition when buffers are missing; rejected because transient missing buffers are already handled by the guarded fail-closed path.
Scalability potential: Low devices avoid unnecessary native view touches on cadence-skipped frames; High/Ultra keep the same query cadence math and job route.
Hardware Impact: Saves one scanner view resolution on every query-eligible frame before guard acquisition. Estimated 2-4 us on i3/MX350 when scanner is active.

Problem: Router anomaly dumps still used the mutable scanner view resolver just to read the telemetry ring.
Solution: Changed dump acquisition to `TryReadOnlyHandle(in _telemetryHandle)` and added a read-only telemetry dump overload that writes bytes directly from the read-only NativeArray view.
Rejected Alternatives: Reusing `TryReadVaultViews` for convenience; rejected because a fault dump should not widen mutable alias exposure or require unrelated scanner buffers.
Scalability potential: All tiers get the same 300-frame black-box dump with a narrower DataVault route; Ultra diagnostic richness remains possible downstream.
Hardware Impact: Fault-only path. Removes unrelated mutable buffer resolves during anomaly dump; no steady-state frame cost.

Problem: Build verification remained prohibited after scanner readonly view polish.
Solution: Ran static gates only: hot-window forbidden-token scan, guarded `TryReadVaultViews` callsite sweep, outside-method `TryResolveHandle` sweep, brace scan, strict `.meta` scan, and targeted `git diff --check`. CPU was 73 with active `dotnet` PID `2588` and `VBCSCompiler` PID `23852`.
Rejected Alternatives: Running `dotnet build` under active compiler and CPU >50; rejected by compilation throttle.
Scalability potential: No runtime impact.
Hardware Impact: Avoided host contention.
