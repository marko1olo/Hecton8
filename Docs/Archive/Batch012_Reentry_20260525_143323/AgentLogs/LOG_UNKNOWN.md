## 2026-05-25 - Last 3 Days Architecture Audit

Agent identity was not supplied through a batch XML prompt; report filed under UNKNOWN.

Evidence class: static source audit plus read-only subagent audits. No build run. Green build, log noise, and warning noise were intentionally excluded from the verdict.

Verdict:
The project is not "doing nothing". The last-three-day direction contains real architecture work: scene search removal, concrete dependency reduction, SignalBus typed payload migration, GlobalDataVault handle ownership, and dispatcher frame ownership. However, the implementation is currently RED for architecture compliance because several fixes replace old coupling with new global authority surfaces.

What was right:
- Runtime scene-search patterns are currently zero in non-editor C# for FindObjectOfType, FindObjectsByType, GameObject.Find, and Resources.Load.
- GlobalRegistry.TryGet usage is currently zero.
- GlobalSignals.Publish usage is currently zero; HectonEventBus.Publish remains isolated to ModdingAPI.
- GlobalDataVault introduces generation handles, write locks, and black-box telemetry surfaces.
- SystemDispatcher.CurrentFrameId and TimeSliceScheduler frame ownership exist.

What is wrong:
- GlobalRegistry surface is still too large and has grown into command transport. Current GlobalRegistry contains 176 service slots and hundreds of public accessors. New routes such as PersistentDroppedItems, AtlasSignalDecodeSink, EndingRuntimeService, Atlas6DirectiveCommandSink, EnvironmentalStrainIndustrialSink, ResourceScarcityReadModel, FaunaWorldSeed, and LoreDatabaseReadModel lack route-card proof.
- Registry is used for gameplay commands. PDAExchangeSystem pulls IAtlas6DirectiveCommandSink from GlobalRegistry during barter execution. AutonomousExtractorSystem calls persistent dropped item registration through a registry route.
- Hot registry polling remains in fauna selection. FaunaDirector calls MigrationDirector.ResolveSelectionMultiplier; MigrationDirector reads GlobalRegistry.Migration and GlobalRegistry.FaunaWorldSeed without a cached fauna seed route.
- DataVault job views are not proven pinned. HazardZoneManager schedules jobs on vault views acquired through TryAcquireWriteLock, while GlobalDataVault live defrag can move unlocked blocks via MemMove. TryAcquireWriteLock is not equivalent to relocation pinning.
- HazardZoneManager public read accessors resolve mutable vault views through indexers. This violates the pure read accessor doctrine and turns read-model access into hot DataVault resolution.
- DataVault write-lock authority is weak in player builds: owner check is tied to collection checks in at least one resolve path, and TryAcquireWriteLock accepts caller systemID as active writer after generation checks.
- Continuous GlobalQualityWeight exists, but runtime still collapses quality into tiers and lets quality affect authority-adjacent data. Examples include cartography discovery shell width, foundation snapping solve budgets, HabitatGraph flood node budgets, and AI cognition cadence without proof of truth invariance.
- Frame authority is partial. SystemDispatcher frame ID exists, but raw Time.frameCount is still written into runtime payload-style fields in Core, Input, Construction, Animation, and World systems.
- Jobs completion audit is incomplete. Several .Complete() calls are annotated cold/QA, but runtime dispatcher/fence/voxel paths still require explicit completion-window proof.

Cinematic cheats / scalability:
- Positive: continuous GlobalQualityWeight and weighted math helpers exist and can buy visual overkill without binary quality switches.
- Failure: tier bridges, High/Ultra gates, and quality-dependent gameplay/data writes remain. Toaster, middle, high, and ultra tiers must be different cost paths for presentation/cadence/capacity only; they must not change save-visible truth or authority routes.

Required next architecture corrections:
- Freeze new GlobalRegistry slots until each route is classified as read model, command sink, or cold DI identity. Command sinks must move to owner-local cached interfaces or typed SignalBus command lanes unless there is a documented owner route.
- Remove Unity concrete leaks from Core contracts where possible: GameObject, BaseModule, BuildableData, mutable char[] buffers, and broad IReadOnlyList<GameObject> surfaces.
- Make SignalBus lane initialization a bootstrap invariant. Publish path must not allocate or lazily initialize persistent native storage.
- Add a real DataVault job pin or prohibit live defrag while any scheduled job holds vault views. TryAcquireWriteLock must not be treated as a relocation pin unless it sets the same lock bits used by defrag.
- Convert HazardZone read accessors to immutable snapshots or cached read-only views. Do not resolve mutable DataVault handles inside public Get/TryGet read methods.
- Replace binary quality/tier branches in gameplay and save-visible paths with continuous GlobalQualityWeight only where it affects fidelity/cadence, not authority.
- Split Time.frameCount hits into allowed visual/diagnostic use and forbidden gameplay/signal-frame use; migrate forbidden cases to SystemDispatcher.CurrentFrameId or TimeSliceScheduler.CurrentFrameId.

Microsecond estimate:
- No credible saved-microsecond claim was made. Static architecture audit cannot prove frame-time savings. The only defensible estimate is that removing scene search and TryGet polling reduces unpredictable spikes, but the current DataVault and GlobalRegistry risks can reintroduce worse costs or correctness failures.

## 2026-05-25 - Deep Architecture Audit, Proof-Backed Append

Agent identity: UNKNOWN. No `CURRENT_BATCH.md` XML agent block was supplied in this session, so this report is filed under the UNKNOWN audit bucket.

Scope:
- User requested a last-three-day architecture audit, not green-build/noise review.
- Baseline selected by command: `git rev-list -1 --before='2026-05-22T00:00:00+04:00' HEAD`.
- Baseline commit: `3d9c1023e413eb96c32fbddb4fe99c95738c9a87`, `2026-05-21T23:56:38+04:00`, `chore: checkpoint project audit status tail`.
- Current HEAD: `f163671f23d634ae06f13f0ee98bc3323c4eb02f`, `2026-05-25T07:20:40+04:00`, `docs(agent): record registry recovery closure`.
- Commits audited: `237`.
- Evidence class: `STATIC_GIT`, `STATIC_SOURCE`, `STATIC_DOC`, `STATIC_FILESYSTEM`, and read-only subagent audits. No compile, Unity import, PlayMode, profiler, or player build proof was produced in this turn.

Relevant mandates applied:
- `ARCH_Global_Registry_ServiceLocator_DI_Init`: GlobalRegistry must be cold identity/DI. Hot polling and gameplay command transport are not acceptable without route proof.
- `ARCH_Signal_Lane_Segregation`: `SignalBus<T>` is hot typed broadcast; `GlobalSignals` direct publish is legacy bridge; `HectonEventBus` is mod/API/cold isolation.
- `OPT_Native_Memory_Collections_JobSystem_Protocol`: scheduled jobs must own stable native views and completion windows; hidden same-frame completion and relocation hazards require proof.
- `QA_Evidence_Text_Filter_Audit`: text search is source evidence only. No runtime/profiler claim is allowed from grep.
- `DBG_Telemetry_Crash_Reporting_PostMortem`: critical native systems need black-box proof, but telemetry does not excuse unsafe ownership.
- `DATA_Runtime_Struct_Layout_ARM64`: runtime DTO/signal structs need explicit unmanaged layout.

Diff scale:
- C# committed diff, baseline..HEAD: `2053 files changed, 259944 insertions(+), 68219 deletions(-)`.
- C# dirty-inclusive diff, baseline..worktree: `2054 files changed, 262387 insertions(+), 69567 deletions(-)`.
- Architecture docs committed diff, baseline..HEAD: `192 files changed, 18891 insertions(+), 10557 deletions(-)`.
- Architecture docs dirty-inclusive diff, baseline..worktree: `192 files changed, 18897 insertions(+), 10557 deletions(-)`.
- Dirty tree exists. Static audit includes the current working tree because the user asked what remains now.

Current broad source counters:
- `GlobalRegistry.`: `6616` hits under `Assets/_Project/Scripts`.
- `GlobalRegistry.TryGet`: `0`.
- `?? GlobalRegistry`: `3`.
- `TryGetLatestCreated(`: `21`.
- `SignalBus<T>.TryPush/TryPushTracked`: `517` total, `507` non-editor by signal auditor scan.
- `GlobalSignals.Publish(`: `1` broad hit, editor/audit string only; runtime call sites found: `0`.
- `HectonEventBus.Publish(`: `11` broad hits; runtime publish sites found by signal auditor are confined to ModdingAPI/cold bridge.
- `Time.frameCount`: `322` broad hits.
- `.Complete(`: `159` broad hits including editor/QA/string tests; runtime subset still needs explicit classification.

Top-level verdict:
- Direction is partly correct, execution is not acceptable yet.
- This was not pure wheel-spinning. Real cleanup happened: `GlobalSignals.Publish` runtime calls are gone, `GlobalRegistry.TryGet` is gone, typed `SignalBus<T>` payloads exist, most signal payload layout is explicit, Data Monolith exists, and dispatcher-owned frame ID exists.
- The architecture is still RED because old coupling was often converted into new global command surfaces, mutable/broad contracts, weak native ownership, and source-only closure claims without route cards or runtime proof.

What improved and is backed by current source:
- Legacy signal publish path:
  - `Assets/_Project/Scripts/Core/GlobalSignals.cs:9` is now a shell partial class.
  - `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:17` forwards legacy wrappers through `SignalBus<T>.TryPush`.
  - `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:22` marks obsolete publish API as compile-error obsolete.
  - Runtime `GlobalSignals.Publish(` call sites found: `0`.
- Mod/API boundary:
  - Runtime `HectonEventBus.Publish(` sites are in ModdingAPI/cold bridge paths, e.g. `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:731`, `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:1386`, `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs:133`.
- Signal payload shape:
  - Signal auditor scan found `292` `ISignal` structs, `292` with `[StructLayout]`, `0` missing layout, `0` forbidden managed/reference public fields.
  - `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:1313` validates signal stride positive, <=192 bytes, and 8-byte aligned.
- Data Monolith artifact:
  - `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists; reported filesystem size: `1064384` bytes. This is existence proof only, not runtime boot proof.
- Dispatcher frame ownership exists:
  - `Assets/_Project/Scripts/Core/SystemDispatcher.cs:947` exposes `CurrentFrameId => TimeSliceScheduler.CurrentFrameId`.
  - `Assets/_Project/Scripts/Core/SystemDispatcher.cs:2709` begins the scheduler frame with `HomeostasisBrain.GlobalQualityWeight` and timing frame data.

What was claimed as closed but is not closed:
- `?? GlobalRegistry` is not zero:
  - `Assets/_Project/Scripts/Construction/VRConstructionWeldTarget.cs:457`.
  - `Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:280`.
  - `Assets/_Project/Scripts/UI/PDAConstructionTab.cs:373`.
  - Docs claiming project-wide zero for `?? GlobalRegistry` are stale against current dirty source.
- Frame authority is partial, not solved:
  - `Assets/_Project/Scripts/Core/GlobalRegistry.cs:847` writes a signal frame from `Time.frameCount`.
  - `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7368` writes `FrameIndex = unchecked((uint)Time.frameCount)`.
  - `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2375` writes `Frame = Time.frameCount`.
  - `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5150` writes `Frame = Time.frameCount`.
  - `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:758` writes `Frame = Time.frameCount`.
  - Many hits are visual/diagnostic/cadence and may be allowed, but the global closure claim is false without classification.
- Binary quality/tier removal is partial:
  - `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:4053` still defines `HectonQualityTier`.
  - `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:569` hardcodes `const HectonQualityTier scalabilityTier = HectonQualityTier.Ultra`.
  - `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:779` branches High/Ultra.
  - `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2503` resolves graph flood node budget from discrete tier.

GlobalRegistry and route ownership findings:
- Net direction: interface extraction reduced some direct concrete coupling, but the authority surface expanded too much.
- Diff proof from global route auditor: since the local GlobalRegistry baseline, `GlobalRegistry.cs` gained `+513/-43`, `GlobalRegistryContracts.cs` gained `+1872/-50`, with `69` added/expanded registry slots and `91` added interfaces in that slice.
- New/widened routes are listed in `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md:118`, but listing is not approval. No dedicated route-card proof was found for the new interfaces by name.
- Review proof requirement exists in `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md:77` and `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md:267`: route card, owner, source diff, hot-path grep, compile/Unity/profiler/player proof before GREEN.

GlobalRegistry violations with source proof:
- Command sink pulled during gameplay barter:
  - `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:376`: reads `GlobalRegistry.Atlas6DirectiveCommandSink`.
  - `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:378`: calls `RegisterBarterTransaction()`.
  - Contract: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2639`, `IAtlas6DirectiveCommandSink`; method at line `2641`.
- Persistent dropped item command route:
  - `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:1165`: reads `GlobalRegistry.PersistentDroppedItems`.
  - `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:1174`: calls `TryRegisterDroppedItem(...)`.
  - Contract exposes concrete economy types: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3516`, `3518`, `3520`, `3527`, `3534`.
- Hot/static fauna selection still reaches registry:
  - `Assets/_Project/Scripts/FaunaDirector.cs:3807`: calls `MigrationDirector.ResolveSelectionMultiplier(...)`.
  - `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs:302`: static helper.
  - `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs:304`: reads `GlobalRegistry.Migration`.
  - `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs:390`: reads `GlobalRegistry.FaunaWorldSeed`.
  - This is not cold DI. This is hot/static selection path unless runtime call frequency is separately disproven.
- Mutating read model:
  - `Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs:204`: `HasDiscoveredRelayInDrivenChain()`.
  - `Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs:206`: calls `RefreshRelayDiscoveryState()`.
  - `Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs:320`: refresh mutates relay discovery state.
  - `Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs:220`: `TryBuildContextualGuidanceMessageSpan(...)` mutates guidance cache per route auditor.
  - Contract: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2646` and `2650`.
- Managed callback smuggled into read model:
  - `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:4704`: `IFluidSurfaceCurrentReadModel`.
  - `Assets/_Project/Scripts/HectonFluidEngine.cs:1291`: managed event backing field.
  - `Assets/_Project/Scripts/HectonFluidEngine.cs:1293`: explicit interface event `CurrentSettingsChanged`.
  - `Assets/_Project/Scripts/HectonFluidEngine.cs:1302`: invokes event.
  - `Assets/_Project/Scripts/FlowFieldVisualizer.cs:1293`: reads `GlobalRegistry.FluidSurfaceCurrent`.
  - `Assets/_Project/Scripts/FlowFieldVisualizer.cs:1296` and `1305`: subscribes/unsubscribes.
  - This should be typed `SignalBus<T>` or immutable owner snapshot revision, not a managed global callback lane.

SignalBus and dispatcher findings:
- Good: runtime direct `GlobalSignals.Publish` is zero and `HectonEventBus.Publish` is confined to ModdingAPI/cold bridge.
- Good: signal payload struct layout scan passed.
- Failure: `SignalBus<T>.TryPush` can allocate on first publish if the lane was not prewarmed.
  - `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:585`: `TryPush` calls `EnsureInitialized()`.
  - `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:531`: first init allocates `new NativeQueue<T>(Allocator.Persistent)`.
  - `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:548`: allocates persistent writer budget.
  - `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:1393`: resolves/creates vault generation handle.
- Probable missing/lazy lane evidence:
  - `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1004`: pushes `EncyclopediaUnlockSignal`.
  - `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1030`: pushes `EntityDepletedSignal`.
  - `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:2740` and `2754`: local signal structs.
  - `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1786`: `PlayVoiceOverSignal` struct exists.
  - `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1121`: pushes `ThermalUpdraftSignal`.
  - `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:77`: `ThermalUpdraftSignal` struct.
  - Static scan did not prove these specific lanes are prewarmed. This needs Roslyn or explicit initializer proof before GREEN.
- Frame stamping remains mixed:
  - `Assets/_Project/Scripts/Core/SystemDispatcher.cs:947` is the correct route.
  - `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:1167` uses `TimeSliceScheduler.CurrentFrameId`, good.
  - Runtime raw `Time.frameCount` payload-style writes still exist in GlobalRegistry, construction, animation, world, VFX, input, and QA/headless paths. They must be split into allowed visual/diagnostic and forbidden authority/signal-frame cases.

GlobalDataVault and job safety findings:
- Hard verdict: current source does not prove job-safe DataVault relocation semantics.
- Generation handles exist, but owner/type checks are partly development-only:
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:1279`: owner/type validation path in resolve.
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:1316`: legacy mutable read handle path.
- Legacy mutable read exists:
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:115`: interface labels `TryReadHandle` as legacy mutable read.
  - `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:511`: PathFunnel uses `TryReadHandle` for buffers.
- Write lock is not relocation pin:
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:1441`: `TryAcquireWriteLock(...)`.
  - It CASes active writer state but does not set the same block lock bits used by defrag.
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:1767`: `TryLockBuffer(...)` is the separate pin-like route.
- Defrag can move memory based on block flags, not active writer:
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:3257`: `UnsafeUtility.MemMove(...)`.
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:3606`: arena growth path.
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:3641`: `TryGrowArena(...)`.
- H8Memory job registration is not a DataVault pin:
  - `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2548`: active job registration.
  - `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:3132`: teardown completion.
  - `Assets/_Project/Scripts/Core/SystemDispatcher.cs:4337`: dispatcher defrag route passes `dataVault.ActiveBurstLockMask`; this does not include plain `TryAcquireWriteLock` unless pin path is used.
- HazardZone exposed path:
  - `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:1205`, `1211`, `1218`, `1229`, `1295`: schedules with write-locked vault arrays.
  - `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:1330`: job schedule.
  - `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:1522` range: locks released after finalization.
  - Correction to earlier draft: current `HazardZoneManager` public read wrapper uses `TryReadOnlyHandle`, not a mutable indexer. Proof: `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:359`. The remaining issue is hot read accessor resolution and unpinned scheduled views, not "mutable indexer read".
- Stale release risk:
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:1527`: release returns false on generation mismatch before decrement/free.
  - `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:380`: wrapper calls release and clears local handle without checking the return.
- `TryGetLatestCreated` classification:
  - Normal runtime gameplay use not found by static scan.
  - `Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultMemoryGizmoVisualizer.cs:28`: diagnostic visualizer.
  - `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:3002`: crash-dump route.
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:612`: definition.
  - Debug/editor/QA uses exist. No current proof of gameplay fallback through this API.

GlobalQualityWeight and scalability findings:
- Positive:
  - Continuous quality weight exists and is widely used.
  - Many visual/cadence/capacity uses may be legitimate.
- RED where quality appears authority-adjacent:
  - `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1541`: `ApplySonarDiscoveryJob`.
  - `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1544`: writes `DiscoveredSectors`.
  - `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1554`: carries `GlobalQualityWeight`.
  - `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1577`: computes quality.
  - `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1586`: obtains unsafe pointer to `DiscoveredSectors`.
  - If `DiscoveredSectors` is save-visible player truth, quality-dependent discovery radius/shell is a doctrine violation.
  - `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1956`: mock exploration job.
  - `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1964`: mock job carries `GlobalQualityWeight`.
  - `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1983`: writes `DiscoveredSectors`.
  - `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:837`, `847`, `856`: ray budget, march steps, interpolation from `GlobalQualityWeight`.
  - `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorJobs.cs:128`, `129`, `130`, `136`: solver budget/steps/interpolation enter the snapping job.
  - `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorJobs.cs:366`: quality affects corner/solve adjustment.
  - If this influences legal placement truth instead of preview-only visuals, it is a violation.
  - `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2503`: graph flood node budget from discrete tier.
- Mandatory correction: quality may scale visual overkill, cadence, telemetry, or approximation fidelity. It must not change DTO layout, save identity, command authority, player discovery truth, or construction legality without a separate deterministic truth route.

`.Complete()` and jobs:
- Broad count is `159`, but this includes editor, QA, comments, and smoke-test string checks.
- Known cold/QA examples exist and should not be counted as hot-path failures by text alone.
- Runtime paths still need explicit completion-window proof:
  - `Assets/_Project/Scripts/Core/DispatcherJobFence.cs:78` and `89`.
  - `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:3169`.
  - Voxel/outpost/runtime paths were present in earlier scan and need per-call classification.
- No profiler or player proof was produced. No microsecond savings or losses can be honestly reported.

What was done in the last three days, objectively:
- Large-scale owner-cache/service-reader routing work occurred; subagent timeline grouped `48` commit subjects in that family.
- `GlobalSignals.Publish` was effectively retired from runtime source.
- Typed `SignalBus<T>` expanded heavily.
- DataVault was given generation handles, lock APIs, defrag telemetry, and black-box surfaces.
- Frame ownership infrastructure was added/used in some systems.
- Documentation ledger/checkpoint volume was high; subagent timeline found `148` checkpoint/noise commits and `29` docs/report ledger commits.
- The work reduced several old anti-patterns, but did not complete architecture proof.

Main complaint remaining:
- The team appears to be optimizing counters and closing grep slices instead of enforcing "one fact -> one owner -> one route -> one proof artifact".
- Removing `TryGet` and legacy publish was good.
- Expanding `GlobalRegistry` into command sinks, broad read models, managed callbacks, concrete data leaks, and gameplay mutation routes is the wrong architectural trade unless every route has owner proof and hot-path evidence. That proof is missing.

Priority fix order:
1. DataVault relocation/job safety. Either make every scheduled vault view hold a real block pin/growth fence, or prohibit compaction/growth until all registered vault jobs complete. This is correctness/memory safety, not style.
2. Freeze new `GlobalRegistry` routes. No new slot without route card, owner, call-site grep, hot-path classification, compile/import/profiler proof.
3. Split command sinks from read models. Barter, dropped item registration, ending/runtime commands, industrial strain commands, and Atlas commands must not be pulled ad hoc from `GlobalRegistry` during gameplay actions.
4. Remove mutating read accessors. `Get*`, `TryGet*`, `Resolve*`, `Read*` must not publish, search, refresh scene state, allocate, complete jobs, or mutate caches.
5. Make `SignalBus<T>` bootstrap complete. First publish must not allocate persistent native queues.
6. Audit quality-truth boundaries. Cartography discovery, foundation snapping legality, graph flood/integrity, and AI decisions need deterministic truth proof independent of quality weight.
7. Split all `Time.frameCount` hits into allowed visual/diagnostic and forbidden authority/signal-frame cases, then migrate forbidden cases to dispatcher/scheduler frame IDs.
8. Classify `.Complete()` call sites with owner phase and profiler/player evidence. Do not use raw counts as proof.

Toaster / middle / high / ultra scalability assessment:
- Low-end i3/MX350: current direction removes some old spike sources, but DataVault relocation and lazy SignalBus init can create unpredictable stalls or memory hazards. No microsecond gain can be claimed.
- Middle tier: typed signal expansion and cached owners can scale, but only if initialization is complete and global command routes are frozen.
- High tier: continuous `GlobalQualityWeight` can buy visual overkill, but current tier/quality paths risk changing gameplay truth instead of only presentation/cadence.
- Ultra tier: visual overkill is viable only after deterministic truth and owner routes are locked. Otherwise ultra mode is just a bigger surface for inconsistent behavior.

Final audit answer:
- Are we going in the right direction? Partly.
- Are we "doing bullshit"? Some streams are real architecture cleanup; some streams are counter-chasing and route inflation.
- Honest status: architecture is RED/PENDING, not GREEN. The correct direction is owner snapshots + typed signals + cold DI + continuous quality as presentation currency. The current implementation still violates that doctrine in GlobalRegistry command routing, DataVault job ownership, lazy SignalBus allocation, raw frame stamping, and quality-dependent truth surfaces.

Microseconds:
- Claimed saved microseconds: `0`.
- Reason: no profiler/player proof was produced in this audit. Static source can identify risk and direction; it cannot measure frame-time savings.
- Any report claiming exact microseconds from this evidence alone would be fake.

## 2026-05-25 - Architecture Fix Loop 1

What was wrong:
- DataVault read handle owner/type/length checks were development-only. Write locks did not set the arena block lock/refcount used by defrag/growth relocation gates.
- Runtime helpers and action paths still used `GlobalRegistry` as a fallback route.
- `IEmergencyRelayRouteReadModel` mutated cache/discovery/guidance state from read-named accessors.
- Several typed `SignalBus<T>` lanes could allocate on first publish.
- `GlobalQualityWeight` changed player-visible truth in cartography discovery, foundation snapping accuracy, and habitat flood/pressure outcomes.
- Multiple black-box/signal/telemetry payloads stamped Unity `Time.frameCount` instead of project frame authority.

What was done:
- Modified 31 target files in this loop.
- `GlobalDataVault`: owner/type/length fences now run outside dev-only checks; write locks set `BlockFlagLocked`/`Reserved1`; release drops the block lock before clearing writer ownership.
- `PDAExchangeSystem`, `AutonomousExtractorSystem`, `MigrationDirector`, construction UI/weld targets: hot/action paths now use cached provider/context/hot-swap routes instead of ad hoc registry fallback.
- `EmergencyServiceRelayDirector`/`EmergencyServiceRelay`/`FirstHourDirector`: relay registry refresh moved to owner/event path; read model reads cached snapshot; duplicate guidance suppression moved to the publisher owner.
- `GlobalSignals.RuntimeLifecycle`, `BabelDictionaryStore`, `ThermodynamicsHazardGridRuntime`: added cold SignalBus prewarm for scanner, voice-over, and thermal updraft lanes.
- `CartographyGridJobs`, `FoundationSnappingCalculatorData`, `HabitatGraphManager`: removed quality-dependent truth from discovery masks, sonar SDF shell, pylon solver ray/march/interpolation, graph flood traversal, and pressure root approximation.
- Frame stamps moved to `SystemDispatcher.CurrentFrameId` for targeted black-box/signal/telemetry entries in ladder IK, foveated sim, docking, drone, lighting, scatter, abyssal thermal, biome SDF, vegetation sync, marine snow, fluid, submarine hydro, habitat flood, input, and QA endurance paths.

Cinematic cheats used:
- Cartography still uses cheap fake surface/mask generation, but weak hardware no longer changes the discovery result.
- Habitat pressure still uses a LUT, not a heavier physical simulation.
- Marine snow/propwash remains visual-event presentation; only frame authority was corrected.

Verification:
- `git diff --check` on the edited target set: no whitespace errors; only line-ending normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal`: PASS, 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:minimal`: PASS, 0 errors, 2 pre-existing missing `Hecton8.Input.csproj` reference warnings.
- Full `dotnet build Hecton8.slnx --no-restore -m:1 -v:minimal`: not a clean source verdict. It compiled `Hecton8.Core` and `Assembly-CSharp`, then failed on missing `project.assets.json` for unrelated editor/package projects.
- Verification mistake: two narrow dotnet builds were briefly launched in parallel through multi-tool after the solution build failed. I stopped, waited for `dotnet/csc` to return to zero, then reran one runtime build at a time.

Exact microseconds saved:
- `0` claimed. No Unity Profiler/player capture was run.

Remaining risk:
- Full Unity batchmode import was not run.
- Raw `Time.frameCount` remains in local scheduling/probe/cooldown paths that need owner-by-owner review before replacement.
- Full solution build needs restore/package cleanup for editor/package projects before it can serve as a global compile gate.

## 2026-05-25 - Architecture Fix Loop 2

What was wrong:
- Loop 1 fixed several frame-authority sites, but many dispatcher-owned telemetry, cache throttles, overflow guards, warning cool-downs, and black-box writers still stamped Unity `Time.frameCount`.
- That mixed Unity render-frame identity with `SystemDispatcher.CurrentFrameId/CurrentFrameIndex`, making crash evidence, QA samples, signal dedupe, and owner-cache cadence harder to compare across systems.
- One audio consumer still assumed semantic fields on `PhysicsEventPayload`; the real shared ABI uses scalar slots.

What was done:
- Modified 36 source files in this loop.
- Converted verified dispatcher-owned frame stamps to `Hecton8.Core.SystemDispatcher.CurrentFrameId` or `CurrentFrameIndex` in construction logistics, drone/docking, ItemCatalog LRU, raycast batch reset, QA endurance, atmosphere/weather/celestial warnings, fluid splashdown/advection/ocean dumps, fauna cognition/cache cadence, Sargassum/biolum telemetry, destructible organic job/black-box paths, LOD/scatter/debris refresh, material decay acoustic telemetry, VR somatic black box, input dispatcher retry/cache/replay capture, runtime/frame watchdogs, DOD replay, noise/scan/submarine/voxel queues, player kinematics cadence, scatter helper caches, and performance monitor snapshots.
- Left `InputDispatcher` input-to-render latency frames on Unity `Time.frameCount`; that path measures render-frame latency, not dispatcher authority.
- Left `FaunaBrain` floating-origin same-frame pause on Unity `Time.frameCount`; that mirrors the current floating-origin frame lock semantics.
- Fixed `PlayerCriticalProceduralAudioRenderer` to consume `PhysicsEventPayload.Scalar1` for acoustic volume and `Scalar2` for pitch scale, matching the producer mapping.

Cinematic cheats used:
- No new simulation complexity was introduced.
- The loop preserves existing visual fakes and cadence throttles; it only aligns diagnostic/owner frame identity where the owner is dispatcher-owned.
- Performance was not sold as a fake win. No profiler means no microsecond claim.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal`: PASS, 0 warnings, 0 errors, elapsed 00:01:36.52.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:minimal`: PASS, 0 errors, 2 pre-existing warnings for missing `Hecton8.Input.csproj`, elapsed 00:01:34.70.
- Build gate was obeyed before the Assembly pass: waited through CPU readings 97.33%, 72.55%, 84.45%, 86.08%, 85.16%, then ran only after CPU reached 16.3% and no `dotnet/csc` process was active.
- Targeted residual scan across the 36 edited source files returned only:
  - `Assets/_Project/Scripts/Core/InputDispatcher.cs:3859`
  - `Assets/_Project/Scripts/Core/InputDispatcher.cs:3862`
  - `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:2866`
  - `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:2885`
- `git diff --check` on the edited target set reported no whitespace errors; only Git line-ending normalization warnings.

Exact microseconds saved:
- `0` claimed. This pass was authority/diagnostic consistency plus compile correctness. No Unity Profiler/player capture was run.

Remaining risk:
- Full Unity batchmode import/playmode was not run.
- Global raw `Time.frameCount` closure is not claimed; only the verified dispatcher-owned subset in this loop was converted.
- Wider `.Complete()` ownership proof, full Data Monolith validation, and profiler evidence remain separate work.

## 2026-05-25 - Architecture Fix Loop 3

What was wrong:
- Dispatcher-owned frame evidence was still mixed with Unity render-frame identity in registry signals, GC/memory audit cadence, scalability snapshots, prologue signal consumption, mod sandbox quotas/dumps, QA determinism bots, voxel budgets, save compression throttle, inventory command signals, vegetation telemetry, impostor cache cadence, and world residency budgets.
- Build also exposed a contract ownership leak: `IPhysicsImpactMaterialProvider` is owned by `Hecton8.Core.Contracts`, but item/module/physics code could resolve the unqualified symbol toward `Hecton8.Physics`.

What was done:
- Modified 31 source files in this loop.
- Converted owner/diagnostic frame stamps to `SystemDispatcher.CurrentFrameId` or `SystemDispatcher.CurrentFrameIndex` in Core, ModdingAPI, QA headless, prologue VFX, voxel, save, inventory, light-shaft signal, vegetation telemetry, impostor cache, wreck debris cadence, and world residency paths.
- Left one `Time.frameCount` in the edited target set: `ScreenSpaceLightShaftRuntime.cs:789`, where it drives a brownout triangle-wave visual fake. It is not authority, telemetry, cache ownership, or save/gameplay truth.
- Fixed contract namespace resolution by explicitly using `Hecton8.Core.Contracts.IPhysicsImpactMaterialProvider` in `PickupItem`, `HectonItem`, `BaseModule`, `HectonPlayerMovement`, and `GlobalPhysicsStateManager`.
- Restored/imported `Hecton8.Physics` only where `BuoyancyObject` is actually used by item classes.

Cinematic cheats used:
- Preserved shader-side brownout stutter as a cheap visual fake.
- Did not replace visual-only frame phasing with dispatcher authority because that would make presentation look less alive without improving truth.
- No new physical simulation was introduced.

Verification:
- Targeted source count: 31 changed source files in this loop.
- `rg -n "Time\.frameCount"` over those 31 files: only `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:789` remains, intentionally.
- `git diff --check` over those 31 files: no whitespace errors; only line-ending normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal`: PASS, 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:minimal`: PASS, 0 errors, 2 pre-existing `Hecton8.Input.csproj` missing-reference warnings.
- Build gate discipline: waited for CPU <=50% and no active `dotnet/csc` before each build attempt.

Exact microseconds saved:
- `0` claimed. No Unity Profiler/player capture was run.

Remaining risk:
- Unity import/playmode/profiler proof is still absent.
- Full-project raw frame closure is not claimed; this loop closed the verified dispatcher-owned subset it touched.
- `.Complete()` ownership proof and Data Monolith boot validation remain separate architecture work.

## 2026-05-25 - Architecture Fix Loop 4

What was wrong:
- Remaining source still had direct `JobHandle.Complete()` calls outside the central fence helper.
- Marauder outpost generation used binary `LowTier` switches for generated density, room thresholds, support height, descriptor flags, and material decay inputs.
- Debug/sonar presentation still had discrete quality branches.
- Compile verification exposed more concrete route leaks: gameplay used the physics determinism facade, bootstrap contracts depended on `H8Debug`, `BaseModule` called `PhysicsApplySystem` statics, and crash telemetry used the concrete player runtime instead of `IPlayerRuntimeContext`.

What was done:
- Routed runtime job finalization through `DispatcherJobFence` in outpost teardown, voxel GPU upload finalization/release, modulo bucketing, H8Memory owner teardown, voxel async publish, nutrient drift, carrion drift, and armor penetration cold QA/editor paths.
- Replaced marauder outpost `LowTier` job input with `GlobalQualityWeight` and continuous curves for density, dimensions, descriptor survival band, material decay, and support height.
- Converted chemical debug draw step and submarine sonar refresh cadence to continuous quality curves.
- Replaced gameplay `PhysicsDeterminismSignals` consumers with `CoreDeterminismSignals`, including explicit `KccVelocitySignal` construction for the core ABI.
- Kept `BootstrapStatus` contract-safe with `Debug.LogError`.
- Added `IPhysicsService.QueueDepressurizationVortex` and `QueueImplosionImpulse`, implemented them in `PhysicsApplySystem`, and routed `BaseModule` through the interface.
- Fixed FirstHour contextual guidance cooldown clock scope and crash telemetry player-pose reads through `IPlayerRuntimeContext`.

Cinematic cheats used:
- Outpost scaling now buys density and presentation continuously instead of switching binary generation shape.
- Breach vortex and implosion remain cinematic physics-owner effects, not a new simulation layer in `BaseModule`.
- No exact performance win is claimed; this pass bought route correctness and smoother fidelity control.

Verification:
- Runtime `.Complete()` scan: only `Assets/_Project/Scripts/Core/DispatcherJobFence.cs:78` and `:89` remain.
- Targeted route scan: no `PhysicsDeterminismSignals`, `PhysicsFrame.Current`, or `PhysicsApplySystem.Trigger*` remains in the repaired gameplay/contract files.
- `git diff --check` on the loop target set: no whitespace errors; only line-ending normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors, elapsed 00:00:54.97 on final pass.
- `dotnet build Hecton8.Bootstrap.Contracts.csproj --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors, elapsed 00:00:01.91.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: PASS, 0 errors, 2 pre-existing `Hecton8.Input.csproj` missing-reference warnings, elapsed 00:01:36.72.

Exact microseconds saved:
- `0` claimed. No Unity Profiler/player capture was run.

Remaining risk:
- Unity import/playmode/profiler proof is still absent.
- Full dirty worktree contains hundreds of unrelated edits by other agents; this log does not claim ownership or correctness for all 660 changed source files.
- Full Data Monolith import/bake/boot validation remains separate work.
## 2026-05-25 - Loop 5 Architecture Repair Report

What was wrong:
- Verified runtime code still mixed Unity render frame identity into dispatcher-owned telemetry, cache throttles, warnings, cooldowns, and signal payloads.
- KCC velocity publication still had a physics facade route and non-authoritative frame-source residue.
- Several presentation/simulation payloads were still fed by binary low-tier or hardcoded quality-tier literals instead of continuous `GlobalQualityWeight`/Q8 pressure.
- The headless Jacobi fuzzer kept direct forced `JobHandle.Complete()` calls outside the first-party fence route.
- `PlayerKinematicsRuntime` depended on `SdfSqueezeResult/SdfSqueezeJob`, but `Hecton8.Core.csproj` did not include `Assets\_Project\Scripts\Physics\KCC\SdfSqueezeJob.cs`; first gated compile exposed this as a real compile wall.

What was done:
- Changed 21 source files plus the Core project map in this loop.
- Frame/facade repairs: `SystemDispatcher`, `HectonFloatingOrigin`, `FaunaBrain`, `PhysicsApplySystem`, `HydrodynamicKccRuntime`, `PlayerSwimBlockoutRig`, `SaveThumbnailSystem`, `SceneRuntimeService`, `HectonVoxelVolume`, `MapMagicRuntimeBridge`, `HectonCrestOceanDepthCacheBootstrap`, `RuntimeDiagnosticsTrace`, `RuntimePerformanceProfiler`.
- Continuous-quality repairs: `SomaticKinematicsRuntime`, `ThermodynamicsHazardGridRuntime`, `TetherManager`, `TetherInstance`, `SargassumMicroFaunaBoids`, `ModEventProjectionBridge`, `HeadlessStressFractureBot`.
- Job-fence/project-map repairs: `PowerGridJacobiStressFuzzer.cs` and `Hecton8.Core.csproj`.
- Added the missing KCC squeeze job compile include.
- Routed Jacobi fuzzer forced completions through `DispatcherJobFence.TryComplete(..., true)`.
- Kept ABI field names where changing names/layouts would create wider save/mod risk; changed the values feeding them instead.

Cinematic cheats used:
- Tether visual fidelity now scales from continuous quality weight through smoothed iteration/damping weights instead of binary low/high selection.
- Synthetic stress and swarm/mod quality payloads now encode continuous Q8/weight pressure rather than hardcoded tier switches.

Proof:
- `rg -n "Time\.frameCount|UnityEngine\.Time\.frameCount" Assets/_Project/Scripts --glob '*.cs'` excluding editor/smoke/tuner/scanner paths returned no non-editor runtime hits.
- `rg -n "PhysicsDeterminismSignals|PhysicsFrame\.Current|PhysicsApplySystem\.Trigger" Assets/_Project/Scripts --glob '*.cs'` excluding editor paths returned only the legacy compatibility class declaration in `PhysicsDeterminismSignals.cs`.
- `rg -n "\.Complete\(\)" Assets/_Project/Scripts --glob '*.cs'` excluding editor paths and `DispatcherJobFence` returned no hits.
- `git diff --check` over the loop target set returned no whitespace errors; only CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: PASS, 0 errors, 2 pre-existing `Hecton8.Input.csproj` missing-reference warnings.

Exact Microseconds saved:
- 0 claimed. No Unity player/profiler capture was run. This pass buys architectural determinism, route hygiene, and continuous scalability correctness; performance claims remain unmade until profiler evidence exists.
