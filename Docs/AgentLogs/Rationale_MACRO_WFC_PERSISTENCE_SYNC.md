# MACRO_WFC_PERSISTENCE_SYNC Rationale

Status: PENDING VERIFICATION

## Decision 0: Task Scope Identification
Problem: WFC-generated outpost mutable state disappears on reload because generated topology and player mutations are not mapped into the binary delta persistence path.
Solution: Inspect existing save/database/WFC/signal contracts first, then add the thinnest interface-backed persistence bridge that stores mutable bits by absolute sector hash.
Rejected Alternatives: Direct concrete WFC-to-database reference; JSON save sidecar; full grid blob writes every state change. These violate decoupling, SaveManager contract, and microSD write budget.
Scalability potential: Low tier stores exact mutable truth with the smallest bitmask. Middle tier may batch more sectors per write. High/Ultra can spend saved IO on richer restored presentation state without changing persistence truth.
Hardware Impact: Expected i3/MX350 gain versus full 500-byte mutable byte-grid writes is sub-0.01 ms CPU and fewer microSD writes; measured proof absent.

## Decision 1: Contract Surface
Problem: WFC persistence needs to be callable before solver execution without taking a concrete outpost generator dependency.
Solution: Extend `IAsyncPersistenceService` with `TryPersistWfcOutpostStateSnapshot` and `TryApplyWfcOutpostStateOverride`, both using `NativeArray<byte>` and `WfcOutpostPersistenceStatus`.
Rejected Alternatives: SaveManager singleton lookup from WFC runtime; direct `H8MacroDatabaseService` calls from outpost code. Both would hard-couple generation to storage and violate registry-owned service boundaries.
Scalability potential: Low tier calls the exact same contract with a 500-cell grid. Middle/High/Ultra can add richer solver state later without changing the persistence call site.
Hardware Impact: Interface dispatch only; expected cost below 1 microsecond on i3/MX350.

## Decision 2: DataVault Grid And Bit Packing
Problem: A 10x10x5 outpost byte grid is cheap but still wasteful to write as a blob for each door/power/loot mutation.
Solution: Reserve `BufferID.WfcOutpostGrid` and pack four mutable planes into 32 `ulong` words through a Burst `IJob`.
Rejected Alternatives: Per-cell byte payload, JSON DTO, or managed `BitArray`. They increase IO, allocate managed memory, or make Burst validation impossible.
Scalability potential: Low tier keeps exact bit truth at 256 raw bytes before RLE. Ultra can spend saved IO on restored visual state while this truth layer stays unchanged.
Hardware Impact: Expected packing cost is under 10 microseconds for 500 cells on i3/MX350; RLE can reduce disk bytes to the 32-byte header plus short runs for mostly-default bases.

## Decision 3: Compile Boundary
Problem: Full `Hecton8.Core` compile is blocked by unrelated missing assembly references from concurrent agents and stale generated project files.
Solution: Verify the changed contract/database/memory/paging dependencies with Unity Roslyn response files where possible, then mark the full Core compile check as dependency-blocked instead of hiding it.
Rejected Alternatives: Killing the active Unity editor owned by another agent; rewriting generated `.csproj` files; fixing unrelated Audio/AI/Outpost generation references inside this WFC persistence pass.
Scalability potential: No runtime impact. This keeps the persistence patch isolated until the integration owner clears the project-wide compile wall.
Hardware Impact: Zero runtime effect; prevents false compile reporting.

## Decision 4: Dirty And Hydration Path
Problem: MacroDB writes must not happen for no-op interactions, and restore must happen before the generator treats a sector as fresh.
Solution: Drain `WfcOutpostStateChangedSignal` snapshots, compare previous/current mutable bits, and call `MarkDirty` only after a real bit transition. Drain bounded `SectorHydratedSignal` snapshots and silently attempt WFC payload decode into the DataVault grid.
Rejected Alternatives: Marking every interaction dirty; destructive signal reads that could steal hydration packets from other systems; polling MacroDB every frame. All would waste IO or create cross-system side effects.
Scalability potential: Low = at most 8 mutation signals and 4 hydration probes per tick. Middle/High/Ultra can raise these caps without changing payload semantics.
Hardware Impact: Expected hot-frame cost is under 20 microseconds for the capped mutation drain on i3/MX350; no managed allocation.

## Decision 5: Absolute Sector Key And RLE Codec
Problem: AUP shifts make local coordinates unsafe as save keys, and raw 500-byte grids waste MicroSD write bandwidth.
Solution: Use the incoming absolute `sectorHash` directly for `IMacroDatabaseService.MarkDirty` / `TryGetPayload`; encode the 32-word bitmask through `SaveBinaryPayloadCodec` with a 32-byte validation header and byte-RLE payload.
Rejected Alternatives: Derived world-pager payload hashes; local grid coordinate keys; bespoke sidecar RLE class. Derived keys break the prompt, local keys are not AUP-safe, and a sidecar codec fragments save validation.
Scalability potential: Low = exact bitmask and compact RLE. High/Ultra can add new bit planes by increasing versioned header fields while preserving the absolute key.
Hardware Impact: Worst raw WFC payload is 288 bytes including header; unchanged snapshots skip MacroDB dirty writes entirely.

## Decision 6: Solver Injection Boundary
Problem: The concrete outpost runtime is owned by the World domain and is still incomplete/blocked in the current compile graph.
Solution: Expose `TryApplyWfcOutpostStateOverride` on the core persistence service and hydrate the DataVault WFC grid from MacroDB; the World-owned generator can call it before scheduling `MarauderOutpostSolveJob` without a database dependency.
Rejected Alternatives: Editing World outpost jobs from the backend persistence pass; directly referencing `SaveManager` from WFC; inventing a new outpost runtime. Those violate the domain boundary and concurrent-agent protocol.
Scalability potential: Low/Middle/High/Ultra all consume the same exact mutable override. Visual tiering can remain in the outpost renderer.
Hardware Impact: Restore decode is bounded to 288 bytes and 500 cells; expected under 20 microseconds on i3/MX350 when invoked.

## Decision 7: Exact Persistence, Not Math LOD
Problem: WFC mutable save truth cannot degrade by distance or hardware tier without corrupting player-authored state.
Solution: Keep persistence exact for every tier and reserve Math LOD for presentation systems that consume restored state.
Rejected Alternatives: Lower-tier probabilistic restore, nearest-cell approximation, or visual-only fake state. These save negligible CPU and break deterministic reload correctness.
Scalability potential: Low uses the same exact bitmask with cheapest IO. Middle can batch more dirty sectors. High/Ultra can use saved CPU/IO to restore extra visual dressing while the truth bitmask remains invariant.
Hardware Impact: No branch cost; expected i3/MX350 benefit is correctness without extra simulation.

## Decision 8: Background Append And Zero-GC Audit
Problem: MacroDB append can touch disk/MMF and must not stall the SaveManager hot tick while WFC mutations arrive.
Solution: `MarkDirty` copies the bounded payload, then `TryAppendDirtyPayload` runs after `Awaitable.BackgroundThreadAsync()` and returns to main thread before telemetry. Packing uses fixed `NativeArray` buffers, for-loops, and a Burst `IJob`.
Rejected Alternatives: Synchronous Tick-time append; managed `BitArray`; managed byte arrays; LINQ; Coroutine disk flush. These either allocate or risk frame spikes.
Scalability potential: Low keeps append off main thread and compresses to <=288 bytes. High/Ultra can raise signal drain caps or attach richer restored visual states without changing disk truth.
Hardware Impact: Hot path queues the append only; expected main-thread saving versus synchronous append is workload-dependent and unmeasured. Packing remains bounded under 10 microseconds target on i3/MX350.

## Decision 9: Telemetry And Compile Honesty
Problem: The task demands `WfcBytesSaved` telemetry and Burst compile proof, but Unity/Burst import is unavailable and full Core compile is blocked by unrelated assemblies.
Solution: Emit `GlobalTelemetryBus.PublishModTelemetry(WFCP, WFBS, savedBytes)` on successful persist and record compile verification as dependency-blocked instead of fabricating a green result.
Rejected Alternatives: Debug logging telemetry; claiming Burst verification from static code; fixing unrelated Audio/AI/World compile errors in the backend persistence pass.
Scalability potential: Telemetry lets later tier tuning track compression effectiveness on weak storage and stronger machines.
Hardware Impact: Telemetry enqueue is expected below 1 microsecond; measured proof absent until Unity profiling is available.

## OMEGA POLISH CHANGES
Problem: The first WFC packer used four branch tests per cell and a written-only async append counter existed after the background append path was made fire-and-forget.
Solution: Replaced the four mutable-state branches with masked `ulong` OR writes inside `PackWfcOutpostMutableStateJob`; removed the unused `_wfcOutpostAppendActive` counter and `System.Threading` dependency; verified the active `SaveManager` compaction API has one method set and one `FrostTick`.
Rejected Alternatives: Keeping the readable branch form; reintroducing an append counter just for diagnostics; editing World WFC generator ownership from the backend pass. Branch readability costs unnecessary instructions, the counter had no consumer, and World call-site wiring belongs to the World domain.
Scalability potential: Low/MX350 keeps exact bit truth with the cheapest branchless pack path and <=288 byte payload. Middle raises batching only. High/Ultra spend the saved CPU/IO on restored visual dressing, richer outpost powered-state presentation, and denser loot feedback without changing persistence truth.
Hardware Impact: Expected i3/MX350 gain is small but real: four branch tests per 500 cells removed from the pack pass, plus one unused interlocked counter path removed. Measured microseconds absent; target remains under 10 microseconds for the 500-cell pack.
Cinematic Cheats Used: Exact mutable-state bitmask instead of resimulating outpost internals; byte-RLE over the bitmask instead of full grid blob; dirty-on-transition snapshot hash instead of always-writing; restore injection into DataVault grid instead of rerunning loot/power history.
Final Git Diff: Key changed files are `Assets/_Project/Scripts/SaveManager.cs`, `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`, `Assets/_Project/Scripts/Core/Contracts/PersistencePagingContracts.cs`, `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`, `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`, `Docs/Tasks/Status_MACRO_WFC_PERSISTENCE_SYNC.md`, and this rationale log. Full diff remains in the worktree for integrator review.

## POST-COMPACTION RECHECK
Problem: The current World outpost runtime packs topology kind in the low nibble and adjacency in the high nibble of `WfcGrid`. The persistence mutable flags also occupy four low bits. Directly applying saved mutable bits into the topology grid would corrupt rooms, doors, datapads, and adjacency masks.
Solution: Keep backend persistence as a separate mutable-state truth grid. Add World-side `_wfcMutableStateGrid`, restore it through `GlobalRegistry.AsyncPersistence` before solve scheduling, and pass it into `MarauderOutpostMatrixExtractionJob`. Extraction merges mutable bits into cell/proxy metadata while leaving the solver topology byte untouched.
Rejected Alternatives: Applying `TryApplyWfcOutpostStateOverride` directly to `MarauderOutpostGenerationService.WfcGrid`; changing the topology byte layout; adding a managed dictionary keyed by cell; replaying interaction history during generation. The first corrupts topology, the second breaks the World owner's packed renderer contract, the third allocates/boxes under pressure, and the fourth is slow and nondeterministic under missing history.
Scalability potential: Low clears/restores one 500-byte mutable grid and extracts only 5x5x3 topology. Middle/High/Ultra keep exact mutable truth while spending visuals on richer shell/proxy metadata and powered/looted presentation. Persistence truth stays invariant across tiers.
Hardware Impact: Low-end i3/MX350 cost is one 500-byte native clear plus a bounded <=288-byte decode on cold generation only; no new steady-frame work. Visual metadata packing adds one byte read and one mask operation per extracted solid cell.

Problem: The DataVault WFC mutable grid is a single buffer, but mutation signals carry sector hashes. Reusing the grid across sectors can leak one sector's mutable cells into another sector's payload.
Solution: Track `_wfcOutpostMutableGridSectorHash`; on sector switch, clear the 500-cell mutable grid, attempt MacroDB restore for the incoming sector, then apply the changed cell before packing.
Rejected Alternatives: Persisting a full managed per-sector cache; ignoring multi-sector signals because current first outpost is singular; allocating one DataVault buffer per sector without a registry contract. Those choices either add memory churn, hide correctness risk, or invent ownership outside this pass.
Scalability potential: Low pays only on rare sector switch. Middle/High/Ultra can add keyed native cache later behind DataVault without changing payload format.
Hardware Impact: Sector switch clear is 500 byte writes; restore remains <=288 bytes. Expected impact is below 20 microseconds on i3/MX350 in the bounded signal path; measured proof absent.

Problem: Payload reader accepted any future flag bits as long as RLE bit semantics passed, and restore unpack still used a branch loop.
Solution: Reject unknown WFC payload flags and raw payloads whose stored byte length does not equal raw bitmask length. Replace restore unpack loop with direct bit-plane shifts and ORs.
Rejected Alternatives: Forward-compatible silent flags; branch loop for readability. Silent flags risk mis-decoding future payloads, and branch loops cost unnecessary instructions in a fixed four-plane format.
Scalability potential: Low gets fail-closed loads and branchless restore. High/Ultra can add future planes through a version bump, not hidden flag behavior.
Hardware Impact: Unknown-flag validation adds one bitmask check; branchless restore removes 2,000 branch checks per full 500-cell restore. Measured microseconds absent.

Verification Update: `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` still fails on unrelated missing Audio/AI/Physics/World contracts. Unity/Bee support checks now fail earlier in `Hecton8.Core.Memory` on unrelated GlobalDataVault defrag symbols (`DefragFlagStressBlocked`, `CompactionStressThreshold`, `VaultMemMoveJob`, etc.). `Hecton8.World.Outposts` response-file check is blocked by missing stale `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.ref.dll`. No green runtime claim is made.

## MUTABLE-STATE PURITY RECHECK
Problem: `RestoreWfcMutableState` cleared the World mutable grid only after accepting a nonzero sector hash. A zero-hash debug or invalid generation request could preserve previous restored mutable bits.
Solution: Clear `_wfcMutableStateGrid` first, then reject `sectorHash == 0UL` before querying persistence.
Rejected Alternatives: Treating zero hash as impossible; clearing only on successful restore. The service accepts a `ulong` sector hash and debug generation paths exist, so stale state must be removed at the local owner.
Scalability potential: All tiers pay only one 500-byte clear on cold generation. High/Ultra visual metadata remains deterministic because stale low-nibble flags are not carried forward.
Hardware Impact: 500 byte writes on cold generation only; expected below 1 microsecond on i3/MX350, measured proof absent.

Problem: SaveManager restore and signal mutation still preserved non-mutable bits even after the contract became "mutable-state grid only." Preserving bits is now a stale-data hazard, not a topology feature.
Solution: Write exact mutable flags into the mutable grid: changed-cell signal assignment is `wfcGrid[cell] = mutableFlags`, and restore writes `cells[cell] = flags`.
Rejected Alternatives: Keeping the old topology-preserving mask/OR for backwards compatibility; accepting caller garbage outside the mutable nibble. The contract now explicitly forbids passing topology/adjacency-packed grids.
Scalability potential: Low/Middle/High/Ultra all use clean four-plane truth. Future planes must version the payload rather than hiding state in preserved high bits.
Hardware Impact: Removes one mask/OR on each changed-cell write and each restored cell write. Small but deterministic; measured proof absent.

Verification Update 2: `Hecton8.Core.Contracts` response-file compile still exits 0. Full `dotnet build Hecton8.Core.csproj` timed out at 132 seconds under the existing dependency wall; follow-up process scan found no lingering `dotnet` or `MSBuild` process.

## BINARY BOUNDARY AND EXTRACTION COST RECHECK
Problem: The WFC payload reader rejected short payloads but still accepted a valid WFC header/body with trailing bytes. For this fixed binary format, a valid prefix with extra bytes is still corrupted storage.
Solution: Require exact payload record length: `length == PayloadHeaderBytes + storedBytes`.
Rejected Alternatives: Prefix-tolerant decoding for future extension. Future WFC payloads must use a version bump; hidden trailing bytes would make corruption and compatibility indistinguishable.
Scalability potential: Low devices get fail-closed save loading with one cheap integer comparison. High/Ultra can extend payloads through versioned format changes, not ambiguous trailing bytes.
Hardware Impact: One equality check on restore. Expected impact below measurement noise on i3/MX350.

Problem: `MarauderOutpostMatrixExtractionJob` checked `MutableGrid.IsCreated` and `cellIndex < MutableGrid.Length` for every solid extracted cell, but this service allocates `_wfcMutableStateGrid` alongside `WfcGrid` and active dimensions are bounded to 500 cells.
Solution: Read `MutableGrid[cellIndex]` directly and keep bounds guaranteed by service-owned allocation and active dimension constants.
Rejected Alternatives: Keeping defensive per-cell checks; copying mutable flags into topology to avoid a second grid. The first spends branch budget in a cold Burst extraction loop; the second corrupts topology.
Scalability potential: Low removes the branch from up to 75 cells; Middle/High/Ultra remove it from up to 500 cells while keeping exact mutable truth.
Hardware Impact: Removes one `IsCreated` branch and one length compare per solid extracted cell. Measured proof absent.

Verification Update 3: Static scans confirm exact-length WFC payload guard, no `MutableGrid.IsCreated` branch in outpost extraction, no old `UnpackWfcOutpostGrid`, and no `immutableMask` in SaveManager WFC path. `Hecton8.Core.Contracts` response-file compile still exits 0.

## SIGNAL BACKPRESSURE, TELEMETRY, AND DRIFT RECHECK
Problem: `DrainWfcOutpostStateChangedSignals` processed only the first 8 entries from a fixed snapshot lane configured for more events. A burst of doors, power state changes, or loot interactions beyond that cap could be ignored after the frame snapshot expired. The same path also packed and dirtied the WFC payload once per processed signal, even when all changes belonged to one sector.
Solution: Scan the full bounded snapshot and batch contiguous dirty sector groups. The mutable DataVault grid is resolved only after the first valid changed signal, same-sector cell writes accumulate in-place, and `TryPersistWfcOutpostStateSnapshot` runs once when the dirty sector changes or at the end of the scan.
Rejected Alternatives: Keeping the 8-signal cap; raising the cap while still packing per signal; adding a managed per-sector dictionary. The cap risks lost persistence, per-signal packing wastes CPU on same-sector bursts, and a managed dictionary violates the zero-GC persistence path.
Scalability potential: Low/MX350 handles the full lane without allocation and usually packs once for one outpost sector. Middle/High/Ultra can tolerate denser interaction bursts without changing disk format; richer visual consumers can react to the restored truth without increasing save payload size.
Hardware Impact: Common same-sector burst removes up to 7 redundant 500-cell pack passes compared with the old 8-signal cap path. Worst-case alternating sectors remains bounded by the signal lane capacity and pays correctness cost rather than dropping state. Measured microseconds absent.

Problem: `WfcBytesSaved` used packed bitmask bytes as the baseline, so the worst-case 288-byte payload reported 0 bytes saved even though it replaces a 500-byte mutable byte grid.
Solution: Report `CellCount - payloadBytes`, clamped at zero. The telemetry now measures net disk-byte savings against the old raw mutable-grid baseline.
Rejected Alternatives: Keeping the packed-word baseline; excluding the 32-byte header from telemetry. The first hides real savings, and the second overstates disk savings because the header is written.
Scalability potential: Low-tier storage tuning gets truthful savings values for mostly-default outposts and worst-case dirty grids. High/Ultra can use the same metric to justify richer restored visuals without changing persistence truth.
Hardware Impact: One integer subtraction and clamp on successful persist; expected cost below 1 microsecond on i3/MX350. Data quality improves; runtime proof absent.

Problem: The anti-amnesia protocol requires repeated CLI extraction of the prompt, but the current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `MACRO_WFC_PERSISTENCE_SYNC`. During the next continuation, `SaveManager.cs` had also drifted back to the old 8-entry cap while docs still described the full snapshot fix.
Solution: Record the failed extraction as a hygiene/verification constraint, continue from the status/rationale files, re-read current code before trusting prior reports, and reapply the signal batching/telemetry fix to the actual file.
Rejected Alternatives: Claiming a successful prompt extraction from the rotated batch file; reading unrelated current batch prompts; trusting stale logs over current source.
Scalability potential: No runtime effect from the hygiene note. The reapplication restores bounded full-lane persistence behavior.
Hardware Impact: Zero runtime effect from documentation; restored code has the same impact profile as above.

Verification Update 4: Static scans confirm the full WFC state-change snapshot loop, removal of `MaxWfcOutpostStateSignalsPerTick`, corrected `CellCount - payloadBytes` telemetry baseline, exact WFC payload length guard, and direct `MutableGrid[cellIndex]` extraction read. `git diff --check` reports no whitespace errors for touched files. `Hecton8.Core.Contracts` response-file compile exits 0. `Hecton8.Core` response-file compile remains blocked by unrelated missing Audio Virtualization, AI Cognition/Fauna, Prologue, Outpost generation, WFC power boot, and World Ore symbols. No runtime, Burst Inspector, GCMonitor, or profiler green claim is made.

## PRODUCER/RESTORE CLOSURE AND H-PHI HYGIENE
Problem: The persistence consumer was present, but the durable gameplay loop was incomplete unless spawned outpost doors and datapads both consumed restored mutable flags and produced typed mutation signals. A restored open door could also be relocked by a generic `Lock()` call, and hybrid datapad prefabs could produce duplicate same-cell `DatapadLooted` signals.
Solution: Keep persistence decoupled through `WfcOutpostStateChangedSignal` and component-local bridges. `SealedDoor` applies restored open/unlocked/powered flags and refuses to relock an already-open restored door. Outpost spawn configuration wires owned door/datapad components; datapad wiring now prefers `MessageTerminal` and falls back to `AudioLogPickup`, giving one producer per cell.
Rejected Alternatives: Direct `SaveManager` calls from interactables; a managed per-cell dictionary; UnityEvent/managed callbacks; configuring every datapad-like component on one prefab. Those choices add hard dependencies, allocations, or duplicate signal writes.
Scalability potential: Low/toaster pays cold spawn component resolution and a fixed 32-byte typed signal only on a real state transition. Middle keeps identical save truth with denser interaction bursts handled by the existing full-lane drain. High/Ultra can spend the saved IO/CPU budget on richer restored door, light, and datapad presentation without changing the four-plane disk truth.
Hardware Impact: No new steady-frame persistence work beyond the existing bounded power-signal path. Cold recursive component resolution is capped by outpost interactable count; mutation payload remains one fixed signal and a compact <=288-byte WFC save payload. Measured runtime proof is absent.
H-Phi Evidence: The qualitative H-Phi improvement is stronger SynapticDensity and PhaseDiscipline: gameplay producers use a typed signal lane and registry-backed persistence instead of direct concrete references. The static H-Phi audit process timed out, so no numeric H-Phi score is recorded.
Verification Update 5: Static-only verification was used after the user's no-rebuild instruction. `git diff --check` and targeted source scans are the valid evidence for this loop; no `dotnet` rebuild, Unity import, Burst Inspector, GCMonitor, profiler, or PlayMode claim is made.

## HOT-PATH REGISTRY DECOUPLING
Problem: The WFC save drain was architecturally typed and batched, but its helper path could still call `GlobalRegistry.DataVault` and `GlobalRegistry.MacroDatabase` from `SaveManager.Tick()` when cached dependencies were missing. That weakens H-Phi SynapticDensity because the hot path has an implicit service-locator fallback.
Solution: Move WFC dependency refresh to service initialization, `SlowTick`, and cold public persistence calls. The frame drain now uses cached `IDataVault`/`IMacroDatabaseService` and a private `TryPersistWfcOutpostStateSnapshotInternal` path; public persist/restore methods still refresh before cold world-generation use.
Rejected Alternatives: Removing refresh entirely; keeping lazy registry lookup inside `TryEnsureWfcOutpostGrid`; changing the public `IAsyncPersistenceService` signatures. Removing refresh risks cold-start restore misses, lazy registry lookup keeps the H-Phi penalty in Tick, and public signature churn violates interface immutability.
Scalability potential: Low/MX350 avoids hidden registry/property lookup work during WFC mutation bursts. Middle/High/Ultra preserve the same exact save truth and can spend the saved frame budget on restored presentation.
Hardware Impact: Hot path removes worst-case registry dependency lookup when cached services are missing; normal cached path is unchanged. SlowTick pays one branch group and occasional dependency refresh. Measured microseconds absent.
H-Phi Evidence: Static evidence now confines `GlobalRegistry.DataVault` and `GlobalRegistry.MacroDatabase` references to `RefreshWfcOutpostDependencies`, outside the WFC Tick drain. This improves SynapticDensity/PhaseDiscipline qualitatively; numeric H-Phi still unclaimed.
Verification Update 6: Static scans confirm WFC Tick drains call cached/internal paths, `GlobalRegistry.*` dependency resolution is isolated, and `git diff --check` has no whitespace errors except CRLF normalization warnings. No `dotnet` rebuild was run by user order.

## WFC PAYLOAD CHECKSUM HARDENING
Problem: The WFC payload codec rejected wrong magic, version, dimensions, unsupported flags, short records, raw-length mismatches, and trailing bytes, but a bit flip inside the stored raw/RLE payload could still decode if the structural header stayed valid.
Solution: Use the existing 32-bit flags field as a packed control word: low 8 bits remain payload flags, high 24 bits store a WFC-local checksum when the checksum flag is set. The checksum mixes stored length, raw length, word count, payload flags, and the stored payload bytes before decode.
Rejected Alternatives: Expanding the WFC header; changing `WfcOutpostPersistenceConstants.PayloadHeaderBytes`; adding a general MacroDB checksum pass. Header expansion would break the current hydration length filter and old payloads; general MacroDB checksumming would add broad read cost outside this WFC pass.
Scalability potential: Low/MX350 gets fail-closed WFC restore with no payload-size increase. Middle/High/Ultra keep the same disk truth and can trust restored visuals more because silent bit flips now reject before state injection.
Hardware Impact: WFC checksum loops over at most 256 stored bytes plus four mixed metadata values. Expected cost is below 1 microsecond on i3/MX350; measured proof absent. No managed allocation.
H-Phi Evidence: Improves DataSovereignty by making the vault-owned WFC binary truth self-validating without new managed sidecars or direct concrete dependencies.
Verification Update 7: Static scans confirm checksum flag/write/read paths, legacy zero-checksum compatibility, unchanged WFC header size, and no whitespace errors except CRLF normalization warnings. No `dotnet` rebuild was run.

## HYDRATION TELEMETRY AND PROBE FAIRNESS
Problem: The compacted task notes identified silent hydration corruption as a suspected gap, but the current source already warns on corrupt hydration payloads. The actual gap was slot-order bias: WFC hydration inspected only the first four `SectorHydratedSignal` entries, so a valid WFC payload later in the fixed lane could expire if earlier database hydrations occupied entries 0..3.
Solution: Preserve the corrupt-payload warning path and change the hydration drain to scan the full signal snapshot while capping only WFC-sized restore probes at four. Resolve the DataVault WFC grid lazily after the first candidate.
Rejected Alternatives: Repatching the already-fixed corrupt warning path; removing the cap and allowing up to 64 MacroDB restore attempts per Tick; destructive signal reads that would steal events from other systems. The first would be churn, the second risks frame spikes, and the third violates signal-lane ownership.
Scalability potential: Low/MX350 avoids lost restored outpost state caused by lane ordering while still bounding database work. Middle/High/Ultra can hydrate denser outpost windows without changing WFC payload format; richer restored visuals still consume the same exact four-plane truth.
Hardware Impact: Static cost is scanning up to 64 fixed-size signal structs and performing at most four WFC-sized restore probes. Static gain is removing a frame-order correctness failure without allocations. Measured microseconds absent.
H-Phi Evidence: Improves PhaseDiscipline and DataSovereignty by making the hydration consumer respect the full typed lane instead of assuming important events are always in the first four slots. Numeric H-Phi remains unclaimed.
Verification Update 8: Static scans confirm `MaxWfcSectorHydrationProbesPerTick`, full snapshot scanning, lazy grid resolve, and `PublishWfcCorruptPayloadWarning()` in hydration decode failure. `git diff --check` reports no whitespace errors beyond Git CRLF normalization warnings. No `dotnet` rebuild was run.

## POOLED DATAPAD BASELINE RESTORE
Problem: A WFC `MessageTerminal` restored as looted marks every message read. If the same pooled object is reused for a later unlooted WFC cell, the previous read flags can leak because unlooted configuration only cleared persistence identity, not terminal read state.
Solution: Capture a cold baseline of authored `MessageEntry.isRead` values, rebuild the read-message set from message state, and restore that baseline whenever WFC config carries no `DatapadLooted` bit. Null `MessageEntry` slots are skipped consistently.
Rejected Alternatives: Resetting all unlooted WFC terminals to unread; clearing read state on every `OnDisable`; adding a managed per-cell datapad map. Reset-all destroys authored pre-read messages, `OnDisable` can break non-WFC terminal lifecycle, and a map adds memory churn outside the four-plane persistence truth.
Scalability potential: Low/MX350 gets deterministic pooled proxy reuse without extra per-frame work. Middle/High/Ultra can pool more datapad proxies and still restore exact WFC looted truth without cross-sector contamination.
Hardware Impact: One cold `bool[messages.Length]` allocation per terminal instance plus branch-only message scans during configuration/editor validation. No Tick allocation; measured microseconds absent.
H-Phi Evidence: Improves DataSovereignty by separating authored terminal baseline from WFC mutable save truth, and improves PhaseDiscipline by keeping restore/reset inside the component that owns the read state.
Verification Update 9: Static scans confirm `_initialReadStates`, `RestoreWfcOutpostDatapadBaselineState`, `RebuildReadMessageSetFromMessageStates`, null-entry guards, and no direct unsafe `messages[i].isRead/messageId/audioClip` access remains. `git diff --check` reports no whitespace errors beyond Git CRLF normalization warnings. No `dotnet` rebuild was run.

## POOLED DATAPAD TRANSIENT RESET
Problem: Baseline state restoration still depended on `UpdateState()`, but `UpdateState()` refuses to interrupt `TerminalState.Playing`. A pooled terminal reused while playback was active could carry previous playback state into the new WFC cell.
Solution: Add a WFC-only transient reset before restored flags are applied: current message index, playback timer, blink timer, and blink state are cleared; `Playing` is converted to `Idle` so looted/unlooted restore can resolve cleanly.
Rejected Alternatives: Resetting playback on every `OnDisable`; changing `UpdateState()` to interrupt playback globally. Both are broader than WFC persistence and can affect authored non-WFC terminal behavior.
Scalability potential: Low/MX350 keeps pooled datapad reuse deterministic without per-frame cost. Middle/High/Ultra can use larger pools without carrying stale playback state between outpost cells.
Hardware Impact: Cold configure-only scalar writes; no managed allocation and no Tick work. Measured microseconds absent.
H-Phi Evidence: Improves PhaseDiscipline by making WFC restore own the transient presentation reset it requires, instead of relying on pooled object lifecycle ordering.
Verification Update 10: Static scans confirm `ResetWfcOutpostTransientPlaybackState()` is called from `ConfigureWfcOutpostPersistence` before the looted/unlooted branch, and `git diff --check` reports no whitespace errors beyond CRLF normalization warnings. No `dotnet` rebuild was run.
