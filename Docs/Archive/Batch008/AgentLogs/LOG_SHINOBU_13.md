# LOG_SHINOBU_13

## 2026-05-17 - WFC Outpost Logistics Router

What was wrong:
- The batch required a zero-GC WFC outpost logistics router, but the existing project still had component-oriented power paths and a separate graph runtime that did not expose the exact SHINOBU DTO contract: 32-byte LogisticsNodeDTO, ulong StateFlags, NativeQueue BFS scratch, mock isolation, oxygen diffusion, docking transfer, and Grid Architect Tuner.
- Docs/Archive contained old rationale references but no usable `wfc_module_costs.h8bin` or `base_energy_profiles.bin`; StreamingAssets was absent. Binary archaeology therefore could not recover legacy room constants.
- Full compile verification is currently blocked outside this domain: Unity batch mode cannot open the project because another Unity instance owns C:\hades\Hecton8, and dotnet build reports 130 unrelated ecosystem/VFX/environment/core manifest errors.

What was done:
- Added `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs`.
- Added exact `LogisticsNodeDTO` layout: int NodeIndex, int ParentIndex, ulong ConnectionMask, float PowerDemand, float OxygenDemand, ulong StateFlags = 32 bytes.
- Added `ConnectionEdgeDTO` as int2 = 8 bytes.
- Allocated all runtime buffers through H8Memory with `NativeArrayOptions.UninitializedMemory`, then initialized them with a Burst `IJobParallelFor`.
- Prewarmed persistent `NativeQueue<int>`, `NativeList<int>`, `NativeParallelMultiHashMap<int,int>`, mock signal queue, and breach queue.
- Implemented Burst BFS from generator and docking nodes with UnsafeUtility.AsRef direct DTO mutation.
- Implemented priority load shedding: life support, corridors, industrial/fabricator, optional.
- Implemented iterative oxygen diffusion over edge buffers with hardware tier cadence: normal 10Hz lane cadence, low-tier 2Hz equivalent by divisor.
- Implemented pressure/yield breach logic and local partial `HullBreachSignal`.
- Implemented local partial `MockModuleStateSignal` and explicit `MockWFCGraphGenerator` for 10-room isolated graph proof.
- Implemented docking umbilical transfer by treating DockingCompleteSignal as a docking node battery source.
- Implemented AUP double3 to camera-relative float3 `LocalShiftResolverJob`.
- Implemented 300-frame native black box and binary dump to `Docs/AgentLogs/Dump_LOGISTICS_GRAPH.bin`.
- Implemented zero-GC CSV parser for `base_module_stats.csv` using one fixed byte buffer, ASCII key hashing, and no line/string split.
- Added `Assets/_Project/Scripts/Editor/GridArchitectTunerWindow.cs` with required sliders and green/red/blue graph visualization.
- Wired SHINOBU lifecycle through `PowerGridManager` create/tick/dispose only.
- Wrote `Docs/Tasks/Status_SHINOBU_13.md` and `Docs/AgentLogs/Rationale_SHINOBU_13.md` with task checklist, decisions, verification wall, and XML self-audit.

Cinematic cheats used:
- Power is binary propagation, not electrical physics.
- Oxygen is two-buffer scalar diffusion, not gas particles.
- Pressure breach is scalar gradient vs yield threshold, not structural FEM.
- Visual blackout is a ulong StateFlags StructuredBuffer target, not CPU light toggles.
- Low-tier cadence trades smooth oxygen spread for reclaimed CPU while preserving readable survival state.

Exact microseconds saved:
- Component/GetComponent/Find traversal: expected removal of O(GameObject) search; counted as 0 hot-path SHINOBU us because no scene object traversal exists in the router.
- BFS state propagation: target 70-100 us for 500 nodes after Burst warmup.
- Oxygen diffusion: target 20-35 us for 3000-edge budget at normal cadence; low tier saves about 80 percent of that by running 2Hz equivalent cadence.
- State flag pass: target under 5 us for 500 nodes through ulong bit operations.
- Pressure pass: target 5-10 us for 500 nodes.
- Graph splicing after mutation: cold-path target under 50 us for 3000 edges, no recursion.
- Editor facade/gizmos: 0 player hot-path us.

Verification:
- Static forbidden traversal scan: PASS for SHINOBU files. Matches were mandated NativeQueue<T>, not managed Queue<T>.
- dotnet build filtered result: no errors in `ShinobuLogisticsRouter.cs`, `PowerGridManager.cs`, or `GridArchitectTunerWindow.cs`.
- Full compile: BLOCKED BY DEPENDENCY. See `Docs/AgentLogs/DotnetBuild_SHINOBU_13.log` and `Docs/AgentLogs/UnityCompile_SHINOBU_13.log`.

---

## 2026-05-17 Ultra Polish Pass

What was wrong:
- The prior log claimed H8Memory-owned arrays. That was not acceptable under the Ultra H-Phi mandate. Persistent graph truth had to live in GlobalDataVault.
- Telemetry and local signal DTOs were fixed-size but not ordered with 8-byte lanes first.
- CSV override monitoring rebuilt file paths from the tick path. Player builds do not need that I/O pressure.
- Editor facade verification had not been separated from runtime verification.

What was done:
- Added dedicated GlobalDataVault BufferID lanes `ShinobuLogisticsNodes` through `ShinobuLogisticsBlackBox` at IDs 70180-70196.
- Changed SHINOBU persistent arrays to GlobalDataVault-owned buffers resolved through `VaultBufferHandle<T>`.
- Kept NativeArray fields only as internal aliases refreshed from generation-checked handles before scheduling jobs. Dispose now clears aliases instead of releasing vault-owned buffers.
- Reordered `LogisticsGraphTelemetryEntry`, `MockModuleStateSignal`, and `HullBreachSignal` for ARM64-friendly 8-byte-leading layout. No Pack=1.
- Kept CSV parsing zero-split and moved `SlowTick` reload behind `UNITY_EDITOR || DEVELOPMENT_BUILD`; the CSV path is cached during initialization.
- Wrapped the Grid Architect Tuner source in `#if UNITY_EDITOR` in addition to placing it under the editor assembly folder.
- Re-ran static forbidden scans and controlled runtime/editor CLI builds.

Struct Layout:
- `LogisticsNodeDTO`: 0 `NodeIndex` int, 4 `ParentIndex` int, 8 `ConnectionMask` ulong, 16 `PowerDemand` float, 20 `OxygenDemand` float, 24 `StateFlags` ulong, size 32.
- `ConnectionEdgeDTO`: 0 `Nodes` int2, size 8.
- `LogisticsGraphTelemetryEntry`: 0 `StateHash` ulong, 8/12/16/20 float metrics, 24/28/32/36 int frame/count/faults, 40/44/48 breach/unpowered/cadence, 52/56/60 padding ints, size 64.
- `MockModuleStateSignal`: 0 `SectorHash` ulong, 8 `Reserved1` ulong, 16 `Frame` uint, 20 `SourceHash` uint, 24 `NodeIndex` int, 28 `Reserved0` ushort, 30 `Flags` byte, 31 `State` byte, size 32.
- `HullBreachSignal`: 0 `SectorHash` ulong, 8 `Reserved0` ulong, 16 `Position` float3, 28 `PressureDeltaKpa`, 32 `Oxygen01`, 36 `Frame`, 40 `SourceHash`, 44 `Flags`, 48 `NodeIndex`, 52/56/60 padding ints, size 64.

H-Phi Check:
- Arrays are in GlobalDataVault: nodes, edges, state flags, oxygen front/back, pressure lanes, reinforcement, AUP, local positions, priority tier, visited, cell-to-node, counters, tuning, blackbox.
- Local `NativeQueue<T>`, `NativeList<int>`, and `NativeParallelMultiHashMap<int,int>` remain native scratch structures because GlobalDataVault exposes array buffers, not queue/hashmap primitives. They are prewarmed and registered with NativeMemorySentinel.

Dear Lie:
- Low tier uses binary power propagation and 2Hz-equivalent oxygen cadence. No electrons, gas particles, FEM, raycasts, or CPU light toggles.
- High/Ultra can spend the saved CPU on shader/VFX overkill through `ShinobuLogisticsStateFlags`; gameplay truth remains the same bitmask graph.

Blackbox:
- 300-frame ring is active in `ShinobuLogisticsBlackBox`.
- Fatal loop or oxygen NaN writes `Docs/AgentLogs/Dump_LOGISTICS_GRAPH.bin`.

Compile Guard:
- No asmdef files were edited.
- No sibling runtime domain was referenced directly. Runtime uses `GlobalRegistry.DataVault`, `SignalBus<T>`, and `Hecton8.Logistics.Grid.Contracts`.
- Runtime CLI build reached C# and failed on external `ShinobuEcosystemBalancer`/`GlobalTelemetryBus` errors. Filtered search found no `ShinobuLogisticsRouter`, `PowerGridManager`, or SHINOBU logistics BufferID errors. `Hecton8.Core.csproj` references `Library/ScriptAssemblies/Hecton8.Core.Memory.dll`, so the H8Memory.cs source edit still needs Unity/Core.Memory import proof.
- `Hecton8.Core.Memory.csproj` is absent, so separate CLI proof for the new BufferID enum lanes is unavailable from generated projects.
- Editor `--no-restore` build stopped at missing `Temp/obj/Hecton8.Editor/project.assets.json`; restore build then failed through the same external Core errors before editor compilation. The generated editor csproj is stale and does not include the new `GridArchitectTunerWindow.cs`, so this editor facade needs Unity import/regeneration for compile proof.

Exact microseconds saved:
- Private array ownership to vault handles: no direct per-frame speed claim; architectural risk reduction only.
- CSV path rebuild removed from player tick: estimated 0 B GC and removes path/metadata I/O from shipping hot path.
- ARM64 signal/telemetry reorder: estimated low single-digit microsecond protection in fault-heavy/debug-heavy frames; not profiler-measured.
- BFS/oxygen estimates remain: 70-100 us BFS target for 500 nodes, 20-35 us oxygen pass for 3000 directed edges, low-tier oxygen cadence saves roughly 80 percent of oxygen work.

<SELF_AUDIT>
  <Task01 status="PASS">Binary archaeology fallback exists through GenerateEmergencyMockProfiles.</Task01>
  <Task02 status="PASS">No component traversal; truth is vault-owned NativeArray graph.</Task02>
  <Task03 status="PASS">Raw DTO fields and UnsafeUtility.AsRef mutation; no C# properties on node DTO.</Task03>
  <Task04 status="PASS">ConnectionEdgeDTO is int2/8 bytes; neighbor lookup is NativeParallelMultiHashMap.</Task04>
  <Task05 status="PASS">MockModuleStateSignal and mock signal job exist without mesh assembler dependency.</Task05>
  <Task06 status="PASS">Burst BFS uses prewarmed NativeQueue and NativeList.</Task06>
  <Task07 status="PASS">Oxygen diffusion is scalar two-buffer edge iteration.</Task07>
  <Task08 status="PASS">Room booleans are ulong bitmasks.</Task08>
  <Task09 status="PASS">Destroyed/locked rooms rebuild adjacency and sever traversal.</Task09>
  <Task10 status="PASS">StateFlags buffer is vault-owned for renderer/shader consumers.</Task10>
  <Task11 status="PASS">Load shedding priority is life support, corridor, industrial, optional.</Task11>
  <Task12 status="PASS">Pressure gradient flips breached/flooded bits and queues HullBreachSignal.</Task12>
  <Task13 status="PASS">System health/low-tier mode dilates oxygen cadence.</Task13>
  <Task14 status="PASS">Vault buffers request UninitializedMemory and Burst initializes defaults.</Task14>
  <Task15 status="PASS">Docking signal marks docking node as submarine power source.</Task15>
  <Task16 status="PASS">AUP double3 subtracts camera double3 before float3 local cast.</Task16>
  <Task17 status="PASS">300-frame blackbox ring plus binary dump exists.</Task17>
  <Task18 status="PASS">Grid Architect Tuner editor window exists.</Task18>
  <Task19 status="PASS">CSV override parser is fixed-buffer and editor/development only from tick.</Task19>
  <Task20 status="PASS">SceneView graph visualizer draws green/red/blue edges.</Task20>
  <ZeroGC status="PASS">No GetComponent, FindObjectsOfType, LINQ, managed Queue/HashSet/Dictionary, Pack=1, or player tick string path rebuild in SHINOBU files.</ZeroGC>
  <AUP status="PASS">Absolute positions are double3; local visual positions are camera-relative float3.</AUP>
  <Dependency status="PASS">No asmdef edits; contracts/signals/GlobalRegistry only.</Dependency>
</SELF_AUDIT>

## 2026-05-18 CSR Hot-Path Polish Pass

What was wrong:
- Burst BFS still expanded neighbors through `NativeParallelMultiHashMap` iterators. That met the XML wording but left cache-line locality on the table.
- The public `SignalBus<HullBreachSignal>` lane duplicated existing flood/incursion semantics in `GlobalSignals.cs`.
- `ShinobuLogisticsRouter` could still resolve `GlobalRegistry.DataVault` internally during initialization, which made the router less deterministic under the compile-wall mandate.
- Blackbox output only wrote the prompt-required `.bin`, while the Ultra mandate also demanded `.h8dump`.

What was done:
- Packed CSR metadata into the existing `ShinobuLogisticsCounters` GlobalDataVault int lane:
  - counters: indexes 0..7
  - edge offsets: base 8, length `MaxNodes + 1`
  - edge write cursors: base `8 + MaxNodes + 1`, length `MaxNodes`
  - edge destinations: base after cursors, length `MaxDirectedEdges * 2`
- Rebuilt CSR after WFC graph build, mock graph build, and dynamic graph splicing.
- Resized the cold `NativeParallelMultiHashMap<int,int>` mirror to `MaxDirectedEdges * 2`, because each undirected edge writes two adjacency entries.
- Bounded the second CSR fill pass to the accepted adjacency count from the first pass, preventing overflow-path segment corruption when a generated base exceeds the edge budget.
- Changed Burst BFS neighbor expansion to indexed CSR scans:
  - `start = Counters[EdgeOffsetsBase + current]`
  - `end = Counters[EdgeOffsetsBase + current + 1]`
  - `neighbor = Counters[EdgeDestinationsBase + edgeCursor]`
- Kept `NativeParallelMultiHashMap<int,int>` as the cold adjacency/splicing mirror required by Task 04.
- Added `InjectDataVault(IDataVault)` and routed DataVault binding through `PowerGridManager`, including DataVault hot-swap handling.
- Converted internal job-local `HullBreachSignal` payloads into the existing `FluidIncursionSignal` public lane during POST_SIMULATION.
- Added a second blackbox export path: `Docs/AgentLogs/Dump_SHINOBU_13.h8dump`.

Cinematic cheats used:
- No new physical simulation was added. Power remains bit propagation, oxygen remains scalar edge diffusion, pressure remains a threshold fake, and public water-leak effects now use the existing fluid-incursion signal corridor.

Exact microseconds saved:
- CSR BFS replaces hash-bucket iterator setup and pointer chasing with contiguous int reads. Expected saving is low single-digit microseconds on sparse graphs and larger on dense 500-node bases. No profiler number is claimed because Unity compile is blocked outside SHINOBU.
- Avoiding a duplicate public breach lane has no direct frame-time claim; it reduces listener fan-out and signal fragmentation risk.
- DataVault injection has no direct frame-time claim; it removes hidden registry lookup behavior from the router.

Verification:
- Static forbidden traversal scan: PASS. No `TryGetFirstValue`, `TryGetNextValue`, `NativeParallelMultiHashMapIterator`, `SignalBus<HullBreachSignal>`, `GetComponent<T>`, `FindObjectsOfType`, LINQ, managed Queue/HashSet/Dictionary, or `Pack=1` matches in SHINOBU runtime/editor files. Only expected `NativeQueue<T>` scratch and PowerGridManager cold `GlobalRegistry.DataVault` injection remain.
- Unity batch compile 2026-05-18: BLOCKED BY EXTERNAL DEPENDENCIES. The log has 1200 compiler-error lines in Quest/Input/World/GlobalPhysics/Audio editor domains. Filtered search found no `ShinobuLogisticsRouter.cs(`, `PowerGridManager.cs(`, `GridArchitectTunerWindow.cs(`, or `H8Memory.cs(` error lines. Log: `Docs/AgentLogs/UnityCompile_SHINOBU_13_CSR.log`.
- `dotnet build Hecton8.Core.csproj --no-restore`: BLOCKED BY EXTERNAL DEPENDENCIES before SHINOBU errors. First failures are in InputDispatcher/SystemDispatcher/WorldChunkResidencyManager. Filtered search found no SHINOBU file error lines. Log: `Docs/AgentLogs/DotnetBuild_SHINOBU_13_CSR.log`.

<SELF_AUDIT>
  <Task01 status="PASS">Fallback mock profiles still inject 16-byte tuning defaults.</Task01>
  <Task02 status="PASS">Runtime graph truth is DataVault NativeArray DTO lanes.</Task02>
  <Task03 status="PASS">DTO fields are raw; Burst mutates via UnsafeUtility.AsRef.</Task03>
  <Task04 status="PASS">ConnectionEdgeDTO is int2/8B; NativeMultiHashMap remains cold mirror; BFS uses CSR hot path.</Task04>
  <Task05 status="PASS">MockModuleStateSignal job remains local and isolated.</Task05>
  <Task06 status="PASS">BFS uses NativeQueue, NativeList, and contiguous CSR neighbor lanes.</Task06>
  <Task07 status="PASS">Oxygen solver is scalar two-buffer diffusion.</Task07>
  <Task08 status="PASS">State is ulong bitmask.</Task08>
  <Task09 status="PASS">Dynamic splicing rebuilds hash mirror and CSR mirror.</Task09>
  <Task10 status="PASS">StateFlags buffer remains ready for shader StructuredBuffer consumers.</Task10>
  <Task11 status="PASS">Load shedding is deterministic fixed-priority linear scan.</Task11>
  <Task12 status="PASS">Pressure breach publishes existing FluidIncursionSignal, not duplicate public HullBreachSignal.</Task12>
  <Task13 status="PASS">Low-tier health/stress lowers oxygen cadence.</Task13>
  <Task14 status="PASS">Vault buffers use UninitializedMemory and Burst default slam.</Task14>
  <Task15 status="PASS">Docking source is part of the same BFS graph.</Task15>
  <Task16 status="PASS">double3 AUP subtracts camera AUP before float3 cast.</Task16>
  <Task17 status="PASS">300-frame blackbox dumps .bin and .h8dump.</Task17>
  <Task18 status="PASS">Grid Architect Tuner exists behind UNITY_EDITOR.</Task18>
  <Task19 status="PASS">CSV override parser is fixed-buffer and dev/editor-only from tick.</Task19>
  <Task20 status="PASS">Editor graph visualizer exists.</Task20>
  <StructLayout>LogisticsNodeDTO offsets: 0 NodeIndex, 4 ParentIndex, 8 ConnectionMask, 16 PowerDemand, 20 OxygenDemand, 24 StateFlags, size 32.</StructLayout>
  <H-Phi>Persistent arrays are GlobalDataVault-owned; CSR lanes are inside ShinobuLogisticsCounters; native queue/list/hashmap are prewarmed scratch with Sentinel registration.</H-Phi>
  <CompileGuard>No asmdef edits. New public signal dependency avoided by using existing FluidIncursionSignal. Fresh build wall is external.</CompileGuard>
</SELF_AUDIT>

---

## 2026-05-18 CSR Overflow Verification Pass

What was wrong:
- The CSR overflow path needed an explicit proof trail. First pass accepted a bounded number of adjacency entries, but the second fill pass had to be documented and verified as bounded to that accepted count.

What was done:
- Confirmed `RebuildCsrFromEdges()` uses `writtenEntries` and stops when `writtenEntries + 2 > adjacencyCount`.
- Updated Status and Rationale with the accepted-adjacency overflow guard.
- Re-ran static scans and build/import checks after the guard landed.

Cinematic cheats used:
- No new simulation. Power remains bit propagation; oxygen remains scalar diffusion; pressure remains scalar threshold plus existing fluid-incursion signal.

Exact microseconds saved:
- No new measured claim. This pass is correctness under edge-budget overflow, not a frame-time optimization claim.

Verification:
- Static forbidden traversal scan: PASS. No `TryGetFirstValue`, `TryGetNextValue`, `NativeParallelMultiHashMapIterator`, `SignalBus<HullBreachSignal>`, `GetComponent<T>`, `FindObjectsOfType`, LINQ, managed Queue/HashSet/Dictionary, `foreach`, or `Pack=1` matches in SHINOBU runtime/editor files.
- `git diff --check` scoped to SHINOBU-touched files: PASS for whitespace errors; Git reported only existing LF-to-CRLF conversion warnings for `H8Memory.cs` and `PowerGridManager.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore`: BLOCKED BY EXTERNAL DEPENDENCIES. 128 compiler-error lines, first failures in `GlobalRegistry`, `InputDispatcher`, `SystemDispatcher`, and `WorldChunkResidencyManager`. Precise filtered search found no SHINOBU-owned compiler-error lines. Log: `Docs/AgentLogs/DotnetBuild_SHINOBU_13_CSR3.log`.
- Unity 6000.4.1f1 batch compile: BLOCKED BY EXTERNAL DEPENDENCIES. 1855 compiler-error lines, first visible walls in `SabineReverbDspTunerWindow`, `BinaryLayoutManifest`, `GlobalPhysicsStateManager`, `WorldChunkResidencyManager`, and `WristHologramHudRuntime`. Precise filtered search found no SHINOBU-owned compiler-error lines. Log: `Docs/AgentLogs/UnityCompile_SHINOBU_13_CSR3.log`.
- Process hygiene: PASS. No `Unity` process remained after verification.

---

## 2026-05-18 Breach Signal Capacity Polish Pass

What was wrong:
- `_breachSignals` was registered/prewarmed for 32 entries while the pressure job can breach up to `MaxNodes` compartments in one solve.
- Local `HullBreachSignal` still implemented `ISignal` even though the public corridor had been de-duped to `FluidIncursionSignal`.
- `SignalBus<FluidIncursionSignal>` would default to a 10000-entry frame snapshot if SHINOBU only configured expected capacity.

What was done:
- Changed `HullBreachSignal` to an internal unmanaged payload, not a public `ISignal`.
- Resized and prewarmed `_breachSignals` to `MaxNodes`.
- Configured and initialized `SignalBus<FluidIncursionSignal>` with expected/max/low-tier capacity `MaxNodes`.
- Changed breach publication to `TryPush`; rejection/shed records `LogisticsGraphFaultFlags.SignalOverflow`.

Cinematic cheats used:
- No new physics. Breaches remain scalar threshold events and public effects stay on the existing fluid-incursion signal.

Exact microseconds saved:
- No measured time claim. This pass removes worst-case NativeQueue block growth and reduces cold signal snapshot reserve from default 10000 entries to MaxNodes when SHINOBU configures the lane first.

Verification:
- Static forbidden traversal/signal scan: PASS. No `SignalBus<HullBreachSignal>`, no `HullBreachSignal : ISignal`, no BFS hash iterator, no `GetComponent<T>`, no `FindObjectsOfType`, no LINQ, no managed Queue/HashSet/Dictionary, no `foreach`, and no `Pack=1` matches in SHINOBU runtime/editor files.
- `git diff --check` scoped to SHINOBU-touched files: PASS for whitespace errors; Git reported only LF-to-CRLF conversion warnings for `H8Memory.cs` and `PowerGridManager.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore`: BLOCKED BY EXTERNAL DEPENDENCIES. 4 compiler-error lines in `GlobalPhysicsStateManager` for missing `WakeRequestSignal`. Precise filtered search found no SHINOBU-owned compiler-error lines. Log: `Docs/AgentLogs/DotnetBuild_SHINOBU_13_CSR4.log`.
- Unity 6000.4.1f1 batch compile: BLOCKED BY EXTERNAL DEPENDENCIES. 43 compiler-error lines in Input/Quest/GlobalPhysics domains. Precise filtered search found no SHINOBU-owned compiler-error lines. Log: `Docs/AgentLogs/UnityCompile_SHINOBU_13_CSR4.log`.
- Process hygiene: PASS. No `Unity` process remained after verification.

---

## 2026-05-18 Public Fluid Lane Preboot Pass

What was wrong:
- `SignalBus<FluidIncursionSignal>` capacity was configured from SHINOBU initialization. If another publisher initialized the lane earlier, the lane could keep the generic default frame snapshot instead of the 500-node SHINOBU budget.
- The direct dependency audit found external `Pack=1` debt in core `AbsoluteUniversePosition` and `GlobalSignals`, including the existing `FluidIncursionSignal`. That is real debt, but it is outside SHINOBU's binary contract authority.

What was done:
- Added `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` in `ShinobuLogisticsRouter`.
- The subsystem hook resets SHINOBU static state and configures `SignalBus<FluidIncursionSignal>` with expected/max/low-tier capacity `MaxNodes`.
- Kept the repeat configure call before explicit lane initialization.
- Did not mutate global AUP/save/signal layout contracts.

Cinematic cheats used:
- No new physical simulation. Power remains bit propagation; oxygen remains scalar diffusion; pressure breach remains scalar threshold routed into existing fluid-incursion effects.

Exact microseconds saved:
- No measured frame-time claim. This is initialization-order hardening and memory-reserve containment, avoiding the generic 10000-entry signal snapshot for a 500-node domain when SHINOBU preboots the lane.

Verification:
- Static forbidden traversal/signal scan: PASS. No BFS hash iterator, no `SignalBus<HullBreachSignal>`, no `HullBreachSignal : ISignal`, no `GetComponent<T>`, no `FindObjectsOfType`, no LINQ, no managed Queue/HashSet/Dictionary, no `foreach`, and no `Pack=1` in SHINOBU-owned files.
- Cross-domain interface audit: RECORDED. Existing core AUP/global signal files still contain `Pack=1`; SHINOBU did not edit those binary contracts.
- `git diff --check` scoped to SHINOBU-touched files: PASS for whitespace errors; Git reported only LF-to-CRLF conversion warnings for `H8Memory.cs` and `PowerGridManager.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore`: BLOCKED BY EXTERNAL DEPENDENCIES. First failures are in `TerminalOS`, `GlobalPhysicsStateManager`, and `InputDispatcher`. Precise filtered search found no SHINOBU-owned compiler-error lines. Log: `Docs/AgentLogs/DotnetBuild_SHINOBU_13_CSR5.log`.
- Unity 6000.4.1f1 batch compile CSR5: INFRASTRUCTURE RUN ONLY. The first run exited with return code 1 before compiler output and wrote only startup text. Log: `Docs/AgentLogs/UnityCompile_SHINOBU_13_CSR5.log`.
- Unity 6000.4.1f1 batch compile CSR5B: BLOCKED BY EXTERNAL DEPENDENCY. Six compiler-error lines, all `Assets/_Project/Scripts/Core/InputDispatcher.cs(3694,1): error CS1022`. Precise filtered search found no SHINOBU-owned compiler-error lines. Log: `Docs/AgentLogs/UnityCompile_SHINOBU_13_CSR5B.log`.
- Process hygiene: PASS. No `Unity` process remained after verification.
