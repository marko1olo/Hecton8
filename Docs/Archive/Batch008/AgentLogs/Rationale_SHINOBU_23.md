# SHINOBU_23 Rationale - Quest DAG And Event Resolver

Date: 2026-05-18
Status: PENDING VERIFICATION

## Decision 00 - Runtime Shape Before Code
Problem: Quest checks must handle 10,000 triggers without string, dictionary, coroutine, or UI coupling.
Solution: Use flat unmanaged data: `NativeArray<ulong>` state masks, fixed-size DTOs, Burst jobs, AUP-local trigger math, and typed unmanaged signals.
Rejected Alternatives: Standard Unity quest `ScriptableObject` graphs, `Dictionary<string,bool>`, enums with `ToString`, `Timeline`/cutscene references, and coroutine timers are too slow, allocating, and presentation-coupled.
Scalability potential: Low uses 4Hz dilation and adjacent-cell trigger scan; Middle keeps 15Hz/30Hz checks; High uses full 60Hz local cell checks; Ultra spends saved CPU on richer editor diagnostics and telemetry, not gameplay truth bloat.
Hardware Impact: Estimated low-end i3/MX350 gain is eliminating string/hash-map quest polling: target under 50 us for 10,000 trigger set with spatial culling, versus millisecond-class managed checks.

## Decision 01 - Vault Ownership With Transient Spatial Index
Problem: H-Phi requires persistent truth in `GlobalDataVault`, but the prompt also requires a `NativeMultiHashMap<int,int>` spatial grid rebuilt during PRE_SIMULATION.
Solution: All quest truth arrays live behind `VaultBufferHandle<T>`: `GlobalStateMasks`, `OldStateMasks`, nodes, runtime metadata, inventory links, faction standings, telemetry, counters. The only private native collection is the transient spatial hash; it contains no quest truth and is `.Clear()` reused per tick.
Rejected Alternatives: Owning private `NativeArray` state in a quest manager, or putting trigger lookup data in dictionaries. Both break data sovereignty and add allocator/hash overhead.
Scalability potential: Low uses 4Hz resolver cadence with 100m cell pruning; Middle/High raise cadence while retaining identical truth; Ultra can spend saved CPU on richer downstream presentation because the DAG output is just bits.
Hardware Impact: Low-end i3/MX350 avoids scanning 10,000 trigger volumes every frame; expected savings are hundreds of microseconds to millisecond-class depending on trigger density. Measured proof is still absent.

## Decision 02 - OSHINO Binary Plus Emergency Mock
Problem: Current narrative binary exists, but parallel batch churn can remove or corrupt files; the resolver must not depend on story/inventory/player owners being live.
Solution: Added cold `TryLoadOshinoBinary()` for `H8QG` records and `GenerateEmergencyMockDAG()` for deterministic dummy chains. Mock signals feed typed queues without direct cross-domain class dependencies.
Rejected Alternatives: Throwing on missing binary, or pulling data from `QuestData` ScriptableObjects at runtime. Both create integration stalls and managed state pressure.
Scalability potential: Low can run the hash-only mock/path with no presentation; High/Ultra can map the same node hashes to richer narrative VFX outside the resolver.
Hardware Impact: Binary parse is boot/cold only. Hot path remains `ulong` masks and AUP-local math.

## Decision 03 - Atomic Mask/Faction Mutation
Problem: Burst jobs must flip completion bits and reputation deltas without managed locks or copied struct properties.
Solution: `GetStateMaskRef()` uses `UnsafeUtility.ArrayElementAsRef<ulong>()`; completion and faction writes use Interlocked CAS loops over raw vault memory.
Rejected Alternatives: C# properties, managed locks, or main-thread post-processing lists. They either reintroduce CS1612 copies or frame-time jitter.
Scalability potential: Low keeps one writer job; higher tiers can schedule additional candidate producers while preserving atomic state mutation.
Hardware Impact: Atomic CAS overhead only occurs on node completion, not on every candidate rejection.

## Decision 04 - Dear Lie Narrative Timing
Problem: Delayed narrative events commonly become coroutine/timeline piles and allocate scheduler state.
Solution: Store `ulong TargetTimestamp` in node runtime data and compare it in `GraphResolverJob`. The resolver only flips truth bits; holograms/audio are downstream consumers of `StateChangedSignal`.
Rejected Alternatives: Unity Timeline references, string event names, or coroutine waits. They couple presentation into narrative truth and create GC risk.
Scalability potential: Toaster sees the same delayed truth with up to 0.2s cadence dilation; Ultra can spend downstream presentation cycles on saltwater radio harmonics and scanner oil-sheen visuals.
Hardware Impact: Replaces delayed-action objects with one integer compare per candidate.

## Decision 05 - Editor Facade Without Runtime Pollution
Problem: Designers need to see and override binary bitmasks, but runtime cannot carry strings or editor-only maps.
Solution: Added `Narrative DAG Inspector` under `#if UNITY_EDITOR`, `node_names.csv`, span CSV override ingestion, and force-complete state toggles. Strings live only in editor/cold files.
Rejected Alternatives: Runtime dictionary of node names or live Unity UI binding. That would contaminate the resolver and violate zero-GC hot path constraints.
Scalability potential: Low runtime remains hash-only; High/Ultra can use editor-visible metadata to tune presentation payloads without C# recompiles.
Hardware Impact: 0 us player hot path; editor-only allocations accepted.

## Decision 06 - Compile Boundary Handling
Problem: Initial Core compile passed, then generated MSBuild Temp assets disappeared and a restore exposed unrelated current-disk errors in `GlobalTelemetryBus`, `ShinobuEcosystemBalancer`, and `SpatialAudioManager`.
Solution: Did not touch unrelated files. Recorded compile wall as external dependency after confirming compiler output has no SHINOBU_23 file errors.
Rejected Alternatives: Fixing forward in global telemetry/audio/ecosystem code from this domain. That would violate domain boundary and risk sabotaging other agents.
Scalability potential: Keeping Quest DAG isolated protects future compile graph churn and lets this domain be verified when integrator restores the global build.
Hardware Impact: No runtime impact; this is build hygiene and dependency containment.

## Decision 07 - Unity Import Proof Over Generated Csprj Assumption
Problem: The root generated `.csproj` files did not include the new SHINOBU_23 files, so plain MSBuild output could not prove or disprove the Quest DAG source.
Solution: Ran Unity 6000.4.1f1 batchmode import/compile. R1 included the Quest files and caught a real `ISignal` namespace defect. The fix restored the `Hecton8.Core.Contracts.Signals` using in Quest DAG runtime/mock files. R2 again included all Quest files and no longer reported `QuestDag*` or `NarrativeDagInspector*` errors.
Rejected Alternatives: Editing generated `.csproj` files by hand, or claiming `dotnet build` covered files it never compiled. Both would create false proof and build-surface churn.
Scalability potential: Unity/Bee is the authoritative import path for this project state; keeping proof tied to actual imported files prevents downstream integrators from trusting stale MSBuild surfaces.
Hardware Impact: No runtime impact. Build-time impact was one controlled Unity batchmode compile instead of repeated blind rebuild spam.

## Decision 08 - Polish Pass: Layout Audit, H8 Dump, Deferred Native Disposal
Problem: The forensic mandate required layout proof, `.h8dump` blackbox output, and tighter native lifetime behavior than a synchronous `Dispose()` path.
Solution: Added `QuestDagLayoutAudit` constants for DTO size/offset proof, emitted both `Dump_QUEST_DAG.bin` and `Dump_QUEST_DAG.h8dump` on fixed-point lock, prewarmed `SignalBus<StateChangedSignal>` in the resolver constructor, and added `Dispose(JobHandle)` so the transient spatial hash can retire behind the active resolver fence.
Rejected Alternatives: Reflection-based offset printing, first-use SignalBus allocation during schedule, and relying only on `IDisposable.Dispose()` blocking completion. Reflection and first-use allocation are banned from hot logic; blocking dispose remains only as the `IDisposable` fallback.
Scalability potential: Low tier keeps the same 4Hz dilation and bounded hash clear; Middle/High/Ultra get the same deterministic truth plus stronger crash artifacts for diagnosing bad OSHINO data without adding presentation coupling.
Hardware Impact: Prevents first-tick lane allocation jitter for state-change emission and avoids mandatory main-thread disposal stalls when the owning bootstrap can use deferred disposal.
