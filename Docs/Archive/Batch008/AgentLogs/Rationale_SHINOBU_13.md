# Rationale_SHINOBU_13

Date: 2026-05-17
Status: IMPLEMENTED / CSR HOT-PATH POLISHED / PUBLIC LANE PREBOOTED / FULL BUILD BLOCKED BY EXTERNAL DEPENDENCIES

## Pre-Code Analysis

Problem: WFC outpost logistics currently has multiple legacy/object paths and a newer graph kernel, but the SHINOBU_13 prompt requires a direct DTO-level BFS/oxygen router with ulong state masks, no recursion, no GetComponent search, mock isolation, and a 300-frame black box.

Solution: Add an isolated Hecton8.Power logistics router around flat native buffers, Burst jobs, NativeQueue traversal, NativeParallelMultiHashMap adjacency, and fixed telemetry. Integrate through PowerGridManager only as a service owner, leaving visual mesh assembly and other agents' files decoupled.

Rejected Alternatives: Extending PowerNode/PowerGrid component search was rejected because it keeps MonoBehaviour membership and HashSet/List truth in the gameplay path. Mutating Core contracts/BufferID was rejected because GlobalDataVault and Core files are dirty from other agents and interface immutability forbids casual contract edits during batch execution.

Scalability potential: Low uses 2 Hz oxygen diffusion, 500-node cap, greedy priority distribution. Middle uses 5-10 Hz logic with full BFS and diffusion. High uses 10 Hz diffusion plus denser visual state publication. Ultra spends saved CPU on visual overkill through shader StructuredBuffer consumers and editor diagnostics, not particle gas truth.

Hardware Impact: For i3/MX350 the target gain is eliminating recursive/component traversal and replacing it with linear native graph passes. Estimated budget for 500 nodes is 70-100 us per logistics solve after Burst compilation; measured proof absent until Unity profiler/GCMonitor run.

## Decision 01: Domain Boundary

Problem: Existing code spans Power, Construction, World Outposts, Core Memory, and GlobalSignals.

Solution: Implement runtime under Assets/_Project/Scripts/Power and editor facade under Assets/_Project/Scripts/Editor. Touch PowerGridManager only for lifecycle wiring if required.

Rejected Alternatives: Editing World.Outposts mesh/generation code would couple to Agent 26/World ownership. Editing Core GlobalDataVault enum would create cross-domain contract risk.

Scalability potential: The router owns math-only state; renderers can consume flags at any quality tier without owning simulation.

Hardware Impact: No GameObject search or visual coupling in the solver path; expected low-end CPU saving against component traversal is substantial but unmeasured.

## Decision 02: Mandates

Problem: The task crosses logistics, native memory, dispatcher phases, and crash debugging.

Solution: Use LOGI, Zero-GC, Native Memory Jobs, Debug Telemetry, Global Registry DI, and Execution Phases mandates as controlling references.

Rejected Alternatives: Reading only AGENTS.md was rejected because task-specific logistics and native-memory mandates define acceptance details.

Scalability potential: Mandate-driven phase split gives explicit Low/Middle/High/Ultra behavior.

Hardware Impact: Explicit 0 B hot-path allocation target prevents MX350/i3 stalls from GC.

## Decision 03: Flat Graph Runtime

Problem: Marauder outpost power and oxygen must not depend on MonoBehaviour membership, object search, recursive split discovery, or visual mesh availability.

Solution: Added `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` with H8Memory-owned NativeArrays, prewarmed NativeQueue<int>, NativeList<int> reachable order, NativeParallelMultiHashMap adjacency, 32-byte LogisticsNodeDTO, and 8-byte int2 ConnectionEdgeDTO.

Rejected Alternatives: Reusing PowerNode/PowerGrid component lists was rejected because those still permit GameObject identity to become simulation truth. Rewriting Core GlobalDataVault enums was rejected because Core contracts are cross-domain and dirty.

Scalability potential: Low uses 500-node cap, 2Hz oxygen cadence, greedy priority. Middle uses full BFS and oxygen at normal cadence. High keeps diagnostics active. Ultra spends the saved CPU on shader overkill through the ulong state buffer.

Hardware Impact: On i3/MX350, replacing object traversal with flat contiguous buffers should save the O(N) component walk and avoid GC spikes. Expected SHINOBU solve budget remains 70-100 us for 500 nodes after Burst warmup.

## Decision 04: Dear Lie Physics

Problem: Real gas/electron flow would burn frame time and produce non-deterministic edge cases.

Solution: Power is binary propagation from generator/docking nodes. Oxygen uses iterative edge diffusion over two float buffers. Pressure is a scalar delta against yield threshold that flips Breached/Flooded bits and queues HullBreachSignal.

Rejected Alternatives: Navmesh-style flood fill per resource, particle gas, CPU light toggles, and per-room physics deformation were rejected as visual-expensive truth models.

Scalability potential: Low gets chunkier oxygen at 2Hz but readable survival state. Middle/High keep 10Hz oxygen. Ultra can bind the same StateFlags to heavier shader/vfx responses.

Hardware Impact: One BFS pass plus one edge diffusion pass replaces multiple recursive component searches. Estimated savings on low-end silicon are frame-stability, not peak FPS vanity.

## Decision 05: Dependency Mocking And Signals

Problem: Mesh assembler and submarine docking systems are parallel-agent domains and cannot be direct dependencies.

Solution: Defined local partial MockModuleStateSignal and HullBreachSignal. Consumed existing WfcOutpostGeneratedSignal, WfcOutpostStateChangedSignal, DockingCompleteSignal, and SystemHealthSignal through SignalBus snapshots.

Rejected Alternatives: Direct references to Agent 26 mesh code or Construction internals were rejected. Polling scene objects was rejected.

Scalability potential: Low and high tiers share the same signals; only solver cadence changes.

Hardware Impact: Signal snapshots are contiguous and cold-path consumed before scheduling the Burst solve. No hot GlobalRegistry lookup is used inside the job.

## Decision 06: Human Control Facade

Problem: Binary/unmanaged logistics constants are not debuggable without an editor surface.

Solution: Added `Assets/_Project/Scripts/Editor/GridArchitectTunerWindow.cs` with Reactor Output, Life Support Drain, Oxygen Diffusion Rate, Crush Depth Multiplier, mock graph rebuild, black box dump, and scene line visualization.

Rejected Alternatives: Serialized MonoBehaviour tuning was rejected because it would not edit the unmanaged runtime constants directly during play.

Scalability potential: Editor diagnostics are capped at 3000 edges and are editor-only. Player runtime pays 0 us.

Hardware Impact: No player hardware impact. Developer can tune low/middle/high/ultra tradeoffs without adding runtime UI.

## Decision 07: Verification Wall

Problem: Unity batch compile could not open the project because another Unity instance owns C:\hades\Hecton8. dotnet build reaches C# compilation but the global assembly already contains unrelated missing symbols from ecosystem/VFX/environment/core manifest work.

Solution: Ran dotnet build anyway and filtered the compiler output for SHINOBU paths. No errors were reported for ShinobuLogisticsRouter.cs, PowerGridManager.cs, or GridArchitectTunerWindow.cs. Last dotnet run reported 130 external errors. Logs are in Docs/AgentLogs.

Rejected Alternatives: Editing unrelated ecosystem/VFX/environment files to force a clean global build was rejected as domain sabotage.

Scalability potential: Verification must resume when integrator clears external errors or closes the active Unity instance.

Hardware Impact: No runtime impact; this is a dependency wall, not a logistics algorithm cost.

## Decision 08: Polish Mandate Result

Problem: Status_SHINOBU_13.md is fully checked, so the batch polish tag had to be read before final reporting.

Solution: Searched CURRENT_BATCH.md for POLISH_MANDATE. No tag exists in this batch. Used the self-reflection audit as the active polish gate and added the explicit MockWFCGraphGenerator class to match the prompt name.

Rejected Alternatives: Inventing a polish mandate was rejected. Skipping the named mock generator was rejected because the prompt explicitly names it.

Scalability potential: The generator remains deterministic and allocation-free; it provides Low-tier and isolated integration smoke coverage without WFC dependency.

Hardware Impact: Editor/dev mock only. No player hot-path cost outside emergency fallback graph construction.

<SELF_AUDIT>
  <ManagedCollectionsInBfs>No List, HashSet, Dictionary, Queue, LINQ, GetComponent, or FindObjectsOfType are used inside the Burst BFS traversal. Traversal uses preallocated NativeQueue&lt;int&gt;, NativeList&lt;int&gt;, NativeArray&lt;byte&gt; visited, and NativeParallelMultiHashMap&lt;int,int&gt;.</ManagedCollectionsInBfs>
  <LogisticsNodeDTOLayout>PASS: int NodeIndex 4 bytes + int ParentIndex 4 bytes + ulong ConnectionMask 8 bytes + float PowerDemand 4 bytes + float OxygenDemand 4 bytes + ulong StateFlags 8 bytes = 32 bytes. StructLayout Sequential Size=32, no Pack=1.</LogisticsNodeDTOLayout>
  <CS1612>PASS: LogisticsNodeDTO exposes raw fields. LogisticsSolveJob mutates node state through UnsafeUtility.AsRef&lt;LogisticsNodeDTO&gt;(NodesPtr + index), not properties.</CS1612>
  <SignalMocks>SUPERSEDED/PASS: MockModuleStateSignal remains a local unmanaged ISignal. HullBreachSignal was later converted into an internal unmanaged job payload and no longer implements ISignal, preventing duplicate public signal lanes.</SignalMocks>
  <EditorFacade>PASS: Grid Architect Tuner exists with required sliders and scene graph visualizer. It writes LogisticsTuningDTO back to SHINOBU unmanaged runtime memory.</EditorFacade>
</SELF_AUDIT>

## Decision 09: Ultra H-Phi Correction

Problem: The first implementation satisfied the flat graph and BFS mandate, but it still allocated persistent runtime arrays through H8Memory and cached them as private system memory. Under the Ultra mandate this is a data-sovereignty breach: the WFC logistics graph truth must live in GlobalDataVault.

Solution: Added dedicated BufferID lanes `ShinobuLogisticsNodes` through `ShinobuLogisticsBlackBox` and changed SHINOBU runtime arrays into GlobalDataVault-owned buffers resolved through `VaultBufferHandle<T>`. The runtime now caches only generation-checked handles plus raw NativeArray aliases, refreshes aliases before scheduling jobs, and clears aliases on dispose without freeing vault-owned memory.

Rejected Alternatives: Reusing `WfcOutpostGrid` for multiple unrelated element types was rejected because it would create type conflicts inside the vault. Keeping H8Memory allocations was rejected because it keeps SHINOBU as a private data owner. Releasing all `SystemID.Power` buffers on dispose was rejected because it could destroy buffers owned by other power systems.

Scalability potential: Low keeps the same 500-node cap and 2Hz oxygen cadence. Middle/High use the same vault truth for runtime solve and editor inspection. Ultra can bind `ShinobuLogisticsStateFlags` directly to rendering/VFX consumers without duplicating graph truth.

Hardware Impact: On i3/MX350 this removes private persistent array ownership and makes compaction/generation checks explicit. Expected runtime solve budget remains 70-100 us for 500 nodes after Burst warmup; this is still an estimate because Unity profiler evidence is blocked.

## Decision 10: ARM64 And CSV Polish

Problem: Telemetry and local mock signals were fixed-size but field order did not put 8-byte fields first. CSV polling also rebuilt path strings from `SlowTick`, which is unacceptable for a zero-GC hot path.

Solution: Reordered `LogisticsGraphTelemetryEntry`, `MockModuleStateSignal`, and `HullBreachSignal` so 8-byte fields lead and total sizes stay 64/32/64 bytes. CSV reload is now editor/development-only from `SlowTick`, with the path cached during initialization.

Rejected Alternatives: `[StructLayout(Pack=1)]` was rejected because it is forbidden for runtime ARM64 memory. Player-runtime CSV polling was rejected because Steam Deck/MicroSD file metadata checks do not belong in the shipping hot path.

Scalability potential: Toaster mode does not pay player CSV I/O. High/Ultra editor workflows still allow instant human tuning during Play Mode.

Hardware Impact: ARM64 layout avoids forced unaligned reads in signal/telemetry buffers. CSV path caching removes managed path construction from the player tick; estimated hot-path GC remains 0 B.

## Decision 11: Verification Boundary Correction

Problem: `Hecton8.Editor.csproj` is stale and does not include the newly added `GridArchitectTunerWindow.cs`, so CLI editor build cannot be treated as compile proof for the editor facade.

Solution: Recorded editor verification as static-only until Unity regenerates project files or completes script import. Runtime `Hecton8.Core.csproj` does include `ShinobuLogisticsRouter.cs` and `PowerGridManager.cs`, and its filtered error log contains no SHINOBU runtime errors. `Hecton8.Core.Memory.csproj` is absent, so the BufferID source edit is static-verified until Unity imports Core.Memory.

Rejected Alternatives: Editing generated csproj files was rejected because Unity owns them. Claiming editor compile success from a stale project file was rejected as fake evidence.

Scalability potential: No runtime impact. This protects integration accuracy.

Hardware Impact: No player hardware impact; this prevents false green reports.

## Decision 12: CSR Hot-Path And Signal De-Dupe

Problem: The Ultra audit found two defects left after H-Phi correction. First, the Burst BFS still expanded neighbors through `NativeParallelMultiHashMap` iterators, which satisfies the XML Task 04 wording but is not the fastest cache-line traversal for a 500-node megabase. Second, the public `HullBreachSignal` lane duplicated existing flood/incursion signal semantics already present in `GlobalSignals.cs`.

Solution: Kept `NativeParallelMultiHashMap<int,int>` as the cold adjacency/splice mirror required by Task 04, sized it to `MaxDirectedEdges * 2` because every undirected edge writes two adjacency entries, and added a CSR hot-path mirror inside the existing `ShinobuLogisticsCounters` GlobalDataVault buffer: counters [0..7], `EdgeOffsets` length MaxNodes+1, `EdgeWriteCursor` length MaxNodes, and `EdgeDestinations` length MaxDirectedEdges*2. `LogisticsSolveJob` now performs BFS neighbor expansion with contiguous int indexing instead of hash iteration. The CSR build pass now bounds the second fill pass to the accepted adjacency count from the first pass, so capacity-exceeded graphs cannot corrupt segment boundaries. Breach publication now converts the job-local `HullBreachSignal` queue payload into the existing `FluidIncursionSignal` lane in POST_SIMULATION, preserving the prompt's internal mock payload while not fragmenting the public signal corridor.

Rejected Alternatives: Adding new BufferID lanes for CSR was rejected because it would widen Core.Memory API surface and force extra compile/import churn when the existing counters lane can safely hold all int metadata. Removing `NativeParallelMultiHashMap` entirely was rejected because the original XML explicitly requires it for dynamic graph splicing. Publishing `SignalBus<HullBreachSignal>` was rejected after the duplicate-signal scan found `FluidIncursionSignal` and `PipeRuptureSignal`; `FluidIncursionSignal` is the closest existing gameplay lane for flooded compartments.

Scalability potential: Low/MX350 gets tighter BFS cache locality and keeps the 2Hz oxygen cadence. Middle/High retain the same gameplay truth with cleaner signal consumption. Ultra can attach richer VFX/audio consumers to the existing FluidIncursionSignal without increasing SHINOBU solver cost.

Hardware Impact: The CSR change removes NativeParallelMultiHashMap iterator setup and hash-bucket chasing from the BFS hot loop. Expected saving is low single-digit microseconds for sparse bases and higher on dense 500-node bases, but no profiler number is claimed because Unity still fails on external compile walls. The signal de-dupe removes one public lane from the runtime nervous system and reduces downstream listener fragmentation risk.

## Decision 13: DataVault Injection Instead Of Router Registry Lookup

Problem: `ShinobuLogisticsRouter.EnsureInitialized()` previously fell back to `GlobalRegistry.DataVault`. That is not a Burst-job issue, but it still puts registry lookup behavior inside the router's tick-driven initialization path and weakens compile-wall isolation.

Solution: Added `InjectDataVault(IDataVault)` to the router and wired it from `PowerGridManager` during initialization and `IGlobalRegistryHotSwapListener` DataVault replacement. The router now resolves buffers only from its injected `IDataVault` reference. If the vault is absent, it records the missing-vault warning and does not allocate fallback arrays.

Rejected Alternatives: Keeping the fallback lookup was rejected because the mandate explicitly asks for GlobalRegistry interfaces and controlled wiring, not hidden service polling in domain logic. Creating a new logistics service slot was rejected because it would change registry contracts and widen compile scope.

Scalability potential: All tiers share one vault truth; hot-swap can rebind the router when the vault moves or is replaced. Low tier does not pay repeated registry probing from the router.

Hardware Impact: No measured frame-time claim. This is primarily compile-wall and initialization determinism protection.

## Decision 14: Breach Queue Capacity And Public Signal Lane Prewarm

Problem: The internal breach payload queue was prewarmed for 32 entries, but a single 500-node pressure solve can legitimately breach many compartments in one frame. That would force `NativeQueue` block growth from inside/around the Burst solve under the exact emergency case where determinism matters. The local `HullBreachSignal` also still implemented `ISignal`, which left a duplicate public-lane type in the codebase even though SHINOBU now publishes existing `FluidIncursionSignal`.

Solution: Converted `HullBreachSignal` into an internal unmanaged payload only, not an `ISignal`. Resized/register-prewarmed `_breachSignals` to `MaxNodes`. Configured and initialized `SignalBus<FluidIncursionSignal>` with expected/max/low-tier capacity `MaxNodes`, then changed publication to `TryPush` so a shed/rejected public signal records `LogisticsGraphFaultFlags.SignalOverflow` instead of silently vanishing.

Rejected Alternatives: Leaving the breach queue at 32 was rejected because emergency pressure failure can exceed it. Publishing a duplicate `SignalBus<HullBreachSignal>` lane was rejected because `FluidIncursionSignal` already owns the public flood/incursion corridor. Configuring `FluidIncursionSignal` with the default frame snapshot was rejected because it would reserve a 10000-entry snapshot for a 500-node domain.

Scalability potential: Low tier avoids emergency NativeQueue growth and keeps fluid effects bounded to the existing incursion lane. Middle/High/Ultra can attach richer alarms, water shaders, and audio to the same public signal without adding solver cost.

Hardware Impact: On low-end hardware this removes a worst-case native queue block-growth risk during mass breach events. The public signal snapshot reserve is reduced from the default 10000 entries to MaxNodes when SHINOBU configures the lane before first use; no profiler number is claimed.

## Decision 15: Public Fluid Lane Preboot And Interface Debt Boundary

Problem: The breach-capacity pass still relied on SHINOBU `EnsureInitialized()` running before any other `FluidIncursionSignal` publisher. If another runtime pushed that signal first, `SignalBus<FluidIncursionSignal>` could allocate its default 10000-entry snapshot before SHINOBU configured the 500-node budget. The audit also found external `Pack=1` layout debt in core `AbsoluteUniversePosition` and many `GlobalSignals` DTOs, including the existing `FluidIncursionSignal`.

Solution: Added a `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` hook in `ShinobuLogisticsRouter` that resets SHINOBU static state and calls `ConfigurePublicSignalLanes()` before scene `Awake` ordering. `EnsureInitialized()` still repeats the configure call before `EnsureInitialized()` for the lane. The core `FluidIncursionSignal` binary layout was not edited; SHINOBU's own DTOs and internal breach payload remain aligned and pack-free.

Rejected Alternatives: Editing the global AUP/save/signal structs to remove every `Pack=1` was rejected as cross-domain binary-contract surgery outside SHINOBU's mandate. Leaving lane configuration only in `EnsureInitialized()` was rejected because subsystem boot order can vary across scenes and tests.

Scalability potential: Low tier gets a bounded public incursion snapshot before gameplay systems wake. Middle/High/Ultra keep the same existing public signal corridor for alarms, water VFX, and audio without a duplicate breach lane.

Hardware Impact: This removes an initialization-order memory reserve risk, not a measured frame-time cost. The worst-case avoided reserve is the generic SignalBus default 10000-frame snapshot for a 500-node logistics domain.

<SELF_AUDIT>
  <Task01 status="PASS">Binary archaeology fallback remains GenerateEmergencyMockProfiles.</Task01>
  <Task02 status="PASS">Runtime truth remains GlobalDataVault-backed NativeArray DTO lanes; no component traversal.</Task02>
  <Task03 status="PASS">LogisticsNodeDTO fields are raw and mutated through UnsafeUtility.AsRef.</Task03>
  <Task04 status="PASS">ConnectionEdgeDTO is int2/8 bytes; NativeParallelMultiHashMap exists as cold splice mirror; BFS hot path uses CSR int lanes.</Task04>
  <Task05 status="PASS">MockModuleStateSignal and mock job remain isolated from mesh assembler.</Task05>
  <Task06 status="PASS">Burst BFS uses NativeQueue/NativeList and CSR neighbor indexing.</Task06>
  <Task07 status="PASS">Oxygen diffusion remains scalar two-buffer edge iteration.</Task07>
  <Task08 status="PASS">State is ulong bitmask.</Task08>
  <Task09 status="PASS">Destroyed/locked state rebuilds cold hash adjacency and CSR mirror.</Task09>
  <Task10 status="PASS">StateFlags buffer remains vault-owned for shader consumers.</Task10>
  <Task11 status="PASS">Load shedding remains fixed four-priority linear pass.</Task11>
  <Task12 status="PASS">Pressure breach uses internal HullBreachSignal payload and publishes existing FluidIncursionSignal.</Task12>
  <Task13 status="PASS">System health lowers oxygen cadence for low-tier/stressed hardware.</Task13>
  <Task14 status="PASS">Vault buffers use UninitializedMemory and Burst initialization job.</Task14>
  <Task15 status="PASS">Docking signal merges submarine battery into BFS source set.</Task15>
  <Task16 status="PASS">AUP double3 subtracts camera double3 before float cast.</Task16>
  <Task17 status="PASS">Blackbox ring is 300 entries and dumps both .bin and .h8dump.</Task17>
  <Task18 status="PASS">Grid Architect Tuner editor facade remains behind UNITY_EDITOR.</Task18>
  <Task19 status="PASS">CSV bridge remains fixed-byte parser and editor/development tick only.</Task19>
  <Task20 status="PASS">SceneView graph visualizer remains editor-only.</Task20>
  <ARM64 status="PASS">Primary DTO layout: 0 NodeIndex int, 4 ParentIndex int, 8 ConnectionMask ulong, 16 PowerDemand float, 20 OxygenDemand float, 24 StateFlags ulong, size 32.</ARM64>
  <ZeroGC status="PASS">No managed collections, LINQ, closures, strings, or hash-map iterators in Burst BFS; Signal snapshots are ReadOnlySpan loops.</ZeroGC>
  <AUP status="PASS">Absolute graph positions stay double3; presentation conversion subtracts camera AUP first.</AUP>
  <DearLie status="PASS">Power is bit propagation, oxygen is scalar diffusion, pressure breach is scalar threshold.</DearLie>
  <Dependency status="PASS">No asmdef edit. Router receives IDataVault injection from PowerGridManager, configures the existing FluidIncursionSignal lane at subsystem registration, and publishes that lane instead of a duplicate public breach signal. Local HullBreachSignal is no longer an ISignal.</Dependency>
  <ExternalDebt status="RECORDED">Core AbsoluteUniversePosition and many GlobalSignals structs still use Pack=1. SHINOBU did not mutate those binary contracts; SHINOBU-owned DTOs remain pack-free and 8-byte aligned.</ExternalDebt>
</SELF_AUDIT>
