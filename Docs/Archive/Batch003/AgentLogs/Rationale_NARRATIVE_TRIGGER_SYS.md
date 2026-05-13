# Rationale_NARRATIVE_TRIGGER_SYS

Status: PENDING VERIFICATION.

## Mandates Ingested
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `PROG_Quest_State_Graph_Logic.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Decisions

### Initial Architecture Read
Problem: Narrative POI triggers must stop depending on Unity trigger callbacks and direct quest mutation while other agents are changing adjacent systems.
Solution: Inspect existing Narrative, GlobalRegistry, EventBus, signal, and asmdef boundaries before edits. Target a NativeArray-backed AUP spatial service with decoupled unmanaged signals.
Rejected Alternatives: Directly patching scene colliders first; standard Unity `OnTriggerEnter`; direct `Quest.Update()` calls.
Scalability potential: Low checks fewer POIs at 1 Hz; Middle keeps SlowTick cadence; High/Ultra can drive richer HUD/audio feedback from the same signal stream without heavier trigger physics.
Hardware Impact: Expected MX350/i3 gain comes from removing physics trigger broadphase work and replacing it with contiguous squared-distance scans; measured proof absent.

### Registry and Signal Surface
Problem: POI triggers had no contract-level service slot and downstream systems would need direct narrative/quest references if events were not explicit.
Solution: Added `ISpatialTriggerSystem` to contracts, registered it through `GlobalRegistry`, and added unmanaged `ProgressionEventSignal`, `BiomeChangedSignal`, `NarrativeHudWaypointSignal`, `SoundscapeProfileSignal`, and `NarrativePoiStateSignal` lanes in `GlobalSignals`.
Rejected Alternatives: Reviving a `TriggerManager.Instance`; direct `Quest.Update()` calls; managed string event names; per-consumer concrete references.
Scalability potential: Low tier receives only the cheap hash/state packets; Middle can consume HUD/audio selectively; High/Ultra can layer richer breadcrumb/audio presentation from the same broadcast stream without changing trigger math.
Hardware Impact: Expected i3/MX350 gain is avoiding PhysX trigger callbacks and managed quest mutation during entry events; exact microseconds require Unity profiler logs.

### Native POI Registry and Collider Purge
Problem: Existing scene audit showed only two `SphereCollider` components in `01_ORBIT.unity`, both non-trigger physical/visual colliders, while narrative POIs were still scanned through managed POI metadata.
Solution: Left non-story colliders untouched, added NativeArray POI positions/radii/hashes/state/sector metadata, and copied cold authoring data from `NarrativeDiscovery` into contiguous runtime storage.
Rejected Alternatives: Raw YAML deletion of all sphere colliders; physics trigger replacement with `OnTriggerEnter`; widening scene edits outside narrative ownership.
Scalability potential: Low uses 1 Hz checks; Middle/High share the same arrays; Ultra can spend saved CPU on stronger waypoint/audio feedback instead of extra physics.
Hardware Impact: Fixed-capacity 64 POI registry is cache-friendly and bounds scan cost on MX350/i3; profiler proof absent.

### Burst Spatial Check and Dispatch
Problem: Managed POI traversal could still re-trigger discoveries and did not expose deterministic hash events for progression, biome, or ambience consumers.
Solution: Added `NarrativePoiSpatialCheckJob` over NativeArrays, one-shot byte latch, 3x3 sector cull, strict squared radius compare, and main-thread dispatch into unmanaged signal queues.
Rejected Alternatives: `Vector3.Distance`, `math.sqrt`, Unity trigger colliders, calling quest state mutation from POI entry.
Scalability potential: Low/MX350 checks at 1 Hz; Middle keeps the same deterministic state; High/Ultra consume the same hash events for richer HUD/audio without increasing spatial math.
Hardware Impact: A 64-slot contiguous scan is expected to stay below the 0.1 ms suspicion threshold on i3/MX350; measured proof absent because project compile is blocked by unrelated dependency errors.

### Consequence Coupling, Save Sync, and Origin Shift
Problem: Trigger consequences needed HUD, soundscape, save, and blackbox handoff without creating cross-domain hard dependencies or runtime GameObjects.
Solution: Added authored hashes to POIs, dispatched HUD/audio/save signals, used `IQuestSystem` only for active-quest read checks, wrote a fixed 300-entry NativeArray blackbox, and subscribed to origin shifts to subtract shift offsets from native POI runtime AUP positions.
Rejected Alternatives: Instantiating waypoint GameObjects; direct `QuestManager` calls; polling `AupShiftSignal` queues and stealing packets from other consumers; JSON or managed save deltas.
Scalability potential: Low gets delayed but deterministic POI state; Middle keeps diegetic waypoint and ambience toggles; High/Ultra can consume the same signals for more aggressive overlay/audio polish.
Hardware Impact: Save and dispatch are cold main-thread work after one-shot trigger; MX350/i3 frame risk is bounded by NativeQueue enqueues and one fixed blackbox write per trigger, not per-frame allocation.

### Origin Shift Race and Compile Verification
Problem: A final self-audit found `OnOriginShift` could subtract from `_poiAups` while `NarrativePoiSpatialCheckJob` was still reading the same native array.
Solution: Complete and dispatch any pending scan through `CompleteSpatialJobForRegistryMutation()` before applying the native shift. Re-ran `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly`; result: build succeeded, 8 warnings, 0 errors.
Rejected Alternatives: Letting floating-origin update race the job; adding locks; consuming shared `AupShiftSignal` packets; widening edits into unrelated physics/cartography files after those dependencies were fixed by others.
Scalability potential: Low/MX350 keeps a one-second scan cadence and safe origin-shift synchronization; Middle/High/Ultra keep the same data path and can spend spare frame time on richer presentation consumers.
Hardware Impact: Completing only on origin-shift mutation avoids per-frame fences. Expected cost is cold-path only; low-end devices avoid undefined native memory races without adding hot-loop synchronization.

### OMEGA POLISH CHANGES
Problem: The first pass used an exact squared-distance check for every POI surviving sector cull, even on low-tier hardware where most POIs are obvious misses.
Solution: Added `UseDominantAxisPreCull` to the Burst job. Low/MX350 performs a dominant-axis squared reject before the exact `math.distancesq` compare. This is a cinematic cheat in cost only: it rejects impossible sphere hits and preserves exact `distSq < radiusSq` trigger semantics for candidates that survive.
Rejected Alternatives: Replacing the sphere check with an approximate cube trigger; using `math.sqrt`; using per-POI floating divisions; adding managed collections to the job; editing unrelated `LaserCutter.cs` to force a green build outside this domain.
Scalability potential: Low/MX350: 1 Hz scan plus dominant-axis pre-cull. Middle: 1 Hz exact squared check. High: 0.5s exact squared check. Ultra: 0.5s exact squared check with saved budget available for richer HUD/audio consumers.
Hardware Impact: Dominant-axis pre-cull is estimated to save 2-8 us per 64-POI low-tier scan when nearby sectors contain mostly outside-radius POIs. PhysX trigger purge is estimated to avoid 40-120 us spikes during narrative-zone overlap bursts. These are estimates; profiler proof is absent.

Final Git Diff:
- `Assets/_Project/Scripts/HectonNarrativeDirector.cs`: NativeArray POI registry, Burst `NarrativePoiSpatialCheckJob`, one-shot latch, sector cull, low-tier dominant-axis pre-cull, signal dispatch, save mask, blackbox, origin-shift job fence.
- `Assets/_Project/Scripts/NarrativeDiscovery.cs`: authored quest/biome/soundscape/HUD metadata and `TryGetSpatialTrigger`.
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: `ISpatialTriggerSystem`, authoring payload, service slot.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`: spatial trigger registration/unregistration/resolve path.
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`: unmanaged narrative progression, biome, HUD, soundscape, and POI state signal queues.
- `git diff --stat` for scoped tracked files: 5 files changed, 3436 insertions, 125 deletions. Shared core files were dirty with other agents' registry/signal work; narrative-owned hunks were kept to contracts, registry slotting, signal lanes, and trigger runtime.

OMEGA Verification:
- Full build after core race fix succeeded: 0 errors, 8 warnings.
- Full build after polish is currently blocked by unrelated `Assets/_Project/Scripts/LaserCutter.cs(1424,66)` using `string.AsSpan` against a target where `string` lacks `AsSpan`.
- Scoped static audit: no `TriggerManager.Instance`, no `Quest.Update`, no `new GameObject`, no `Instantiate`, no `AddComponent` in edited trigger files.
- Scoped math audit: no `math.sqrt`, no `Vector3.Distance`, no `.magnitude`; radius gate remains `distanceSq >= radiusSq` with `math.distancesq`.

### Continued AAA Audit - Identity, Shift Ordering, Disposal
Problem: Spatial triggers raised the POI hash, but the legacy `NarrativeEvents` hash-to-id map is only populated by the string overload. A queued hash-only discovery could therefore fail to populate `discoveredIds`, weakening save/debug identity. A second issue was origin-shift ordering: `HectonFloatingOrigin` broadcasts after transform and `TotalOffset` rebasing, so dispatching completed scan results before subtracting the shift from `_poiAups` could publish wrong AUP payloads.
Solution: Added a fixed cold `string[64]` POI id lane outside the Burst job, record authored discovery identity immediately on spatial dispatch, and still publish `NarrativeEvents` plus unmanaged signals for cross-system consumers. Split job completion from dispatch so origin shifts complete the scan, subtract the native shift, adjust cached player runtime telemetry, then dispatch results. Disposal now completes any pending scan and disposes native arrays immediately instead of scheduling untracked deferred disposals.
Rejected Alternatives: Adding managed strings to `NarrativeSpatialTriggerAuthoring`; relying on `NarrativeEvents` queue flush for local save identity; dispatching origin-shift results against stale runtime POI positions; running `dotnet build` after the user explicitly prohibited it.
Scalability potential: Low/MX350 keeps fixed 64-slot native scan and one managed id lookup only on one-shot dispatch. Middle/High/Ultra keep deterministic hash signals while retaining authored ID persistence for save/debug tools.
Hardware Impact: No new per-frame allocation. The new `string[64]` is cold memory only; dispatch-side string checks occur once per POI trigger. Completing jobs before native disposal is cold shutdown work and removes deferred disposal uncertainty.

### Continued AAA Audit - Save Latches, Fault Throttle, Quest Hash
Problem: External discovery and save-load paths could mutate `_poiState` while a spatial job still owned the NativeArray. Overflow telemetry counted all active POIs before filtering out non-spatial authoring, producing false alarms. Repeated non-finite POI faults could spam the blackbox dump path. Authored quest hashes used localization hashing while `IQuestSystem.TryGetQuestIdByHash` is backed by `QuestFlagHashKernel`. Legacy saves with discovery IDs but no AUP mask could also re-arm already discovered POIs.
Solution: Complete pending scans before external discovery or save-load `_poiState` mutation. Registry/external-discovery mutation dispatches pending results first; save-load state overwrite drains and discards pending pre-load results because the snapshot is authoritative. Count only POIs that pass `TryGetSpatialTrigger` for capacity warnings. Throttle blackbox disk writes to one dump per 120 frames while still emitting telemetry warnings. Mirror `QuestFlagHashKernel` UTF-16 FNV hashing inside `NarrativeDiscovery` without adding a narrative-to-quest assembly dependency. During load, restore native latches from the packed bitmask or `_discoveredHashLookup`, then repack the mask and publish a snapshot operation distinct from one-shot trigger operations.
Rejected Alternatives: Adding locks around native arrays; dispatching stale pre-load scan results after applying save data; widening asmdef dependencies to call internal quest hashing directly; treating every save-state signal as a trigger; ignoring legacy `discoveredIds`; running `dotnet build` despite the explicit no-build instruction.
Scalability potential: Low/MX350 keeps the same 64-slot fixed scan and no new hot allocations. Middle/High/Ultra gain cleaner downstream save/HUD interpretation through operation bytes without changing spatial math.
Hardware Impact: Job completion remains cold-path only for load, registration, disposal, origin shift, and external discovery. Blackbox dump throttling can avoid repeated file IO stalls during persistent NaN faults; estimated worst-case avoidance is milliseconds of disk IO per repeated fault window, with no measured profiler proof.
