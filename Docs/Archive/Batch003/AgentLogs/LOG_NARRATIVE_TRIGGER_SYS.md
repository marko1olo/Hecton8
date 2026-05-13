# LOG_NARRATIVE_TRIGGER_SYS

## 2026-05-13 - AUP Spatial Trigger Purge

What was wrong:
- Narrative triggers depended on managed scene/POI scans and the architecture allowed Unity trigger-zone thinking to survive.
- There was no `ISpatialTriggerSystem` service contract in `GlobalRegistry`.
- Entering a narrative area needed deterministic hash signals instead of direct quest mutation.
- POI state was not exposed as a fixed bitmask for save/RLE sync.
- Origin shifts could invalidate runtime trigger positions unless the POI registry moved with the floating origin.

What was done:
- Added `ISpatialTriggerSystem`, `NarrativeSpatialTriggerAuthoring`, and `NarrativeSpatialTriggerFlags` to `GlobalRegistryContracts`.
- Registered/unregistered the spatial trigger runtime through `GlobalRegistry`.
- Added unmanaged signal lanes for `ProgressionEventSignal`, `BiomeChangedSignal`, `NarrativeHudWaypointSignal`, `SoundscapeProfileSignal`, and `NarrativePoiStateSignal`.
- Extended `NarrativeDiscovery` with authored quest, biome, soundscape, and HUD breadcrumb hashes.
- Replaced old AUP trigger scanning in `HectonNarrativeDirector` with a fixed 64-slot NativeArray POI registry and a Burst `IJob`.
- Implemented one-shot `NativeArray<byte>` state latches, 3x3 AUP sector culling, exact squared radius compare, POI state mask sync, and a 300-entry native blackbox ring.
- Added origin-shift synchronization and completed any pending scan before mutating `_poiAups`.
- Audited `Assets/_Project/Scenes`: only two `SphereCollider` entries exist in `01_ORBIT.unity`, both non-trigger orbit colliders; no story trigger zones were deleted.

Cinematic Cheats used:
- PhysX trigger callbacks replaced with a fixed native spatial hash/sector cull.
- Low/MX350 uses 1 Hz checks.
- Low/MX350 adds a dominant-axis squared pre-cull before exact `math.distancesq`; it rejects impossible hits without changing `distSq < radiusSq` semantics.
- HUD/audio/save consequences are hash signals, not GameObject spawning or direct subsystem calls.

Exact Microseconds saved:
- Low/MX350 dominant-axis pre-cull: estimated 2-8 us per 64-POI scan when most same-sector POIs are outside radius.
- PhysX trigger-zone purge: estimated 40-120 us avoided during overlap bursts.
- Save sync bitmask vs managed payload: estimated 5-15 us per triggered POI event.
- Profiler proof is absent. These are engineering estimates, not measured frame captures.

Verification:
- Core build before OMEGA polish: `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly` succeeded with 0 errors / 8 warnings.
- Post-polish build attempt is blocked outside this domain: `Assets/_Project/Scripts/LaserCutter.cs(1424,66)` uses `string.AsSpan`, unavailable to the current target. I did not edit that file.
- Static trigger audit: no `TriggerManager.Instance`, `Quest.Update`, `UpdateQuest`, `new GameObject`, `Instantiate`, or `AddComponent` in scoped trigger files.
- Math audit: no `math.sqrt`, `Vector3.Distance`, or `.magnitude`; Burst gate remains `distanceSq >= radiusSq` skip after `math.distancesq`.
- Job GC audit: `NarrativePoiSpatialCheckJob` fields are NativeArrays/primitives only. Managed `List`/`HashSet` remain outside the job in cold/main-thread discovery bookkeeping.

Final scoped diff:
- `Assets/_Project/Scripts/HectonNarrativeDirector.cs`
- `Assets/_Project/Scripts/NarrativeDiscovery.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`
- `Docs/Tasks/Status_NARRATIVE_TRIGGER_SYS.md`
- `Docs/AgentLogs/Rationale_NARRATIVE_TRIGGER_SYS.md`

## 2026-05-13 - Continued Audit, No Build

What was wrong:
- Spatial POI dispatch had hash-safe progression signals, but save-facing discovery identity could still depend on a later `NarrativeEvents` hash-to-id resolution path. Hash-only events do not populate that map.
- Pending scan completion during origin shift could dispatch before `_poiAups` were rebased, while `HectonFloatingOrigin` had already shifted transforms and `TotalOffset`.
- Native disposal scheduled array frees against a job dependency and discarded the returned dispose handles. Cold shutdown should be deterministic.
- Save data population left stale string slots past `narrativeDiscoveryCount` when a reused `SaveData` buffer had older entries.

What was done:
- Added a fixed cold `string[64]` POI discovery-id lane and populated it during native registry rebuild.
- Spatial dispatch now calls the string `NarrativeEvents.RaiseDiscoveryMade` path when an authored ID exists and records the director's own discovery identity immediately.
- Split pending job completion from result dispatch. Registry mutation still dispatches before registry change; origin shift now completes first, subtracts the shift from `_poiAups`, adjusts cached player runtime telemetry, then dispatches.
- Native disposal now completes any pending scan and disposes arrays immediately on cold shutdown.
- `PopulateSaveData` clears unused discovery-id slots after the active count.

Cinematic Cheats used:
- No new simulation. This pass preserved the fixed native sector check and Low/MX350 dominant-axis pre-cull.
- Identity lane is cold managed storage only; no managed data enters the Burst job.

Exact Microseconds saved:
- Deferred-disposal removal: no runtime frame saving; cold shutdown determinism improvement.
- Save stale-slot clear: estimated under 2 us per save for 64 string slots.
- Origin-shift dispatch ordering: correctness fix, not a frame-time optimization.

Verification:
- User explicitly prohibited `dotnet build`; no build command was launched.
- `git diff --check` passed for scoped edited files.
- Scoped forbidden-pattern scan found no `math.sqrt`, `Vector3.Distance`, `.magnitude`, `new GameObject`, `Instantiate`, `AddComponent`, `TriggerManager.Instance`, `Quest.Update`, or `UpdateQuest`.

## 2026-05-13 - Continued Audit 2, No Build

What was wrong:
- External discovery and save-load paths could write `_poiState` without first completing a pending spatial job.
- Save-load could have dispatched stale pre-load scan results while applying an authoritative snapshot.
- Registry overflow telemetry counted all active POIs before excluding POIs that were not valid spatial triggers.
- Persistent non-finite POI faults could rewrite `Dump_NARRATIVE_TRIGGER_SYS.bin` every detection frame.
- Authored quest hashes did not match the quest system hash kernel, so HUD breadcrumbs gated by active quest could miss.
- Legacy save data with discovery IDs but no packed AUP mask could re-arm already discovered POIs.
- Load-time POI state signals used the same operation byte as one-shot trigger signals.

What was done:
- Added pending-job completion before external discovery/load native latch mutation.
- Added a state-overwrite completion path that drains pending scans and discards stale trigger results before applying loaded state.
- Counted only valid `TryGetSpatialTrigger` authoring records for overflow warnings.
- Added a 120-frame blackbox dump cooldown.
- Mirrored `QuestFlagHashKernel` UTF-16 FNV hashing in `NarrativeDiscovery` without adding a quest assembly dependency.
- Restored native POI latches from either `narrativeAupTriggeredMask` or `_discoveredHashLookup`, then repacked the bitmask.
- Split `NarrativePoiStateSignal.Operation` usage into snapshot (`0`) and triggered (`1`).

Cinematic Cheats used:
- No new physical simulation. The spatial trigger remains a deterministic native sector scan with Low/MX350 dominant-axis pre-cull.
- Save/load repair is cold-path state packing; it does not enter the Burst scan.

Exact Microseconds saved:
- False overflow filtering: estimated under 3 us per registry rebuild, mainly removes bogus telemetry work.
- Blackbox dump throttle: can avoid repeated disk writes during persistent faults; savings are IO-dependent and not measured.
- Legacy latch restore: correctness fix, estimated under 5 us per 64-POI load pass.

Verification:
- User explicitly prohibited `dotnet build`; no build command was launched.
- `git diff --check` passed for scoped edited files, with only LF-to-CRLF warnings.
- Scoped forbidden-pattern scan found no `math.sqrt`, `Vector3.Distance`, `.magnitude`, `new GameObject`, `Instantiate`, `AddComponent`, `TriggerManager.Instance`, `Quest.Update`, `UpdateQuest`, or `.Dispose(dependency)`.
- Static comparison confirmed `NarrativeDiscovery.ComputeQuestHash` mirrors `QuestFlagHashKernel` byte order and FNV constants.
