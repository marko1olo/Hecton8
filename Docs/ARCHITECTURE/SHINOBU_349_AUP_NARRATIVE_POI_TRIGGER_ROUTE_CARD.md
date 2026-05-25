# SHINOBU_349 AUP Narrative POI Trigger Route Card

Date: 2026-05-23
Owner: `SystemID.NarrativePoiTriggers`
Domain: Echelon 8 Presentation and UX / AUP Narrative Triggers

## Authority

- Fact: narrative POI trigger truth.
- Owner: `HectonNarrativeDirector` plus `SystemID.NarrativePoiTriggers` Vault buffers.
- Route: `GlobalDataVault` rows evaluated by deterministic Burst, then `SignalBus<ProgressionEventSignal>`.
- Proof: telemetry ring, dump path, scanner JSON, route card.

`GlobalRegistry` is used only in cold owner phases.

Cold resolves: `IDataVault`, dispatcher registration, existing service ownership.

Burst job receives Vault-backed arrays and cached SignalBus writer. It does not poll registry, scene objects, quest managers, colliders, or dialogue systems.

## Vault Lanes

| BufferID | Name | Type | Capacity | Owner |
| --- | --- | --- | --- | --- |
| `74000` | `NarrativePoiTriggers` | `NarrativePoiDTO` | `10000` | `NarrativePoiTriggers` |
| `74001` | `NarrativePoiBucketRanges` | `NarrativePoiBucketRangeDTO` | `4096` | `NarrativePoiTriggers` |
| `74002` | `NarrativePoiBucketIndices` | `int` | `4096 * 8` | `NarrativePoiTriggers` |
| `74003` | `NarrativePoiStateMasks` | `ulong` | `(PoiCapacity + 63) / 64`, default `157` | `NarrativePoiTriggers` |
| `74004` | `NarrativePoiTelemetryRing` | `AupNarrativeTriggerTelemetryEntry` | `300` | `NarrativePoiTriggers` |
| `74005` | `NarrativePoiTelemetryCursor` | `int` | `1` | `NarrativePoiTriggers` |
| `74006` | `NarrativePoiCounters` | `int` | `16` | `NarrativePoiTriggers` |
| `74007` | `NarrativePoiCsvScratch` | `long` | `4` | `NarrativePoiTriggers` |
| `74008` | `NarrativePoiPresentation` | `NarrativePoiPresentationDTO` | `10000` | `NarrativePoiTriggers` |

All lanes use `NativeArrayOptions.UninitializedMemory`.

Owner code overwrites active POI rows, bucket ranges, counters, cursor, and state masks before use. Release path: `AupNarrativePoiVault.ReleaseBuffers`.

## DTO ABI

`NarrativePoiDTO` is `[StructLayout(LayoutKind.Explicit, Size = 64)]`:

| Offset | Size | Field |
| --- | ---: | --- |
| `0` | `24` | `double3 PoiAUP` |
| `24` | `4` | `uint EventHashID` |
| `28` | `4` | `float TriggerRadiusMeters` |
| `32` | `8` | `ulong PrerequisiteBitmask` |
| `40` | `4` | `uint StateFlags` |
| `44` | `20` | five `uint` padding fields |

The layout is one 64-byte cache line. The only hot-path fields are raw public fields; no properties are used in the DTO.

`NarrativePoiPresentationDTO` is `[StructLayout(LayoutKind.Explicit, Size = 64)]`.

Offsets: `PoiHash@0`, `QuestHash@4`, `BiomeHash@8`, `SoundscapeHash@12`, `LoreHash@16`, `Flags@20`, `BitIndex@24`, `Reserved0@28`, pads `32..63`.

It is the Vault-owned cold managed-presentation mirror lane, not a private `NativeArray`.

## Runtime Route

- 1. Authoring or CSV boot writes `NarrativePoiDTO` rows and cold `NarrativePoiPresentationDTO` metadata rows.
- 2. `BuildNarrativePoiBucketsJob` builds same-cell bucket ranges and indices from AUP spatial hash.
- 3. `Tick(float deltaTime)` applies continuous cadence: `math.lerp(0.05f, 0.25f, 1.0f - smoothQuality)`.
- 4. `EvaluatePoiTriggersJob` reads player `double3` AUP and finds the player cell.
- 5. It evaluates only local bucket indices.
- 6. It subtracts `PlayerAUP - PoiAUP` in double precision.
- 7. It casts only localized delta to `float3`, then checks squared radius.
- 5. Prerequisite truth is `(GlobalNarrativeStateMask & PrerequisiteBitmask) == PrerequisiteBitmask`. Authored first-hour POIs resolve prerequisite masks through generated `H8QuestMasks`; CSV/mock POIs carry the mask directly.
- 8. First entry sets flags: `Triggered | Inside | Exhausted | DispatchPending`.
- 9. Vault state mask word array flips the POI index bit.
- 10. `ProgressionEventSignal` enqueues through `NativeQueue<ProgressionEventSignal>.ParallelWriter`.
- 11. Source: `AupNarrativePoiRuntimeConstants.ProgressionSourceNarrativePoi`.
- 7. Completion scans `DispatchPending` DTO flags and marks `Dispatched`.
- Managed presentation runs from the Vault presentation lane.
- It does not diff one `ulong` or read private native mirrors.
- It does not alias 10k POI rows onto 64 bits.
- 8. Registry rebuild/sync treats discovered POI hash identity as the AUP solver exhaustion source.
- 9. Legacy serialized `narrativeAupTriggeredMask` remains a mirror for old public save/API compatibility.
- 10. It is not the large-POI runtime identity route.
- 9. `LateFrameTick()` finalizes only completed jobs through `DispatcherJobFence.TryFinalizeCompleted`. Forced completion is limited to teardown/registry mutation.
- `HectonNarrativeDirector` declares no private `NativeArray`, `NativeList`, or `NativeHashMap` fields for the POI trigger route.

## Quality And Dear Lie

There is no binary hardware switch.

Quality scales cadence continuously, not gameplay truth or DTO layout. Low weight moves toward `0.25s`; high weight toward `0.05s`. Spatial hashing keeps work local.

Dear Lie: replace invisible story colliders and PhysX broadphase updates with local-cell mathematical radius check plus 1.2x exit hysteresis.

Before: scene-trigger behavior trends toward broadphase/callback overhead across authored trigger volumes. After: `O(localBucketCount)` per cadence tick, normally 2-3 POIs, with no collider callbacks.

## First 20 Minutes Route Impact

Early discoveries, starter wreck breadcrumbs, and starter POIs no longer require physical trigger volumes.

Quest/audio consumers receive existing `ProgressionEventSignal` lane. First-session beats remain routed without collider-driven broadphase cost in starting biome.

## Forensics

- `AupNarrativeTriggerTelemetryEntry[300]` records frame, player AUP, evaluated count, signal count, player cell hash, compatibility state-mask word0, flags, full state hash, and schedule-to-completion timing.
- `StateHash` is FNV over every `NarrativePoiStateMasks` word plus the player cell hash, so 10k POI state changes do not collapse into the first 64 bits.
- `NarrativePoiBucketRangeFlags.Overflow` maps directly to `AupNarrativeTriggerTelemetryFlags.BucketOverflow`; invalid player/POI AUP, bucket overflow, or >0.1ms timing stages a raw dump to `Docs/AgentLogs/Dump_SHINOBU_349.bin`.

`Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_349.json` is sidecar scanner proof.

Shared report keeps non-destructive `SHINOBU_349_AUP_Narrative_Poi_Trigger_Scanner` section. Scanner uses Roslyn syntax-node detection with token fallback only on parser failure.

## Verification Boundary

- Static verification only.
- No Unity import, Play Mode profiler, GCMonitor, or green build proof is implied.
- A narrow `dotnet build Assembly-CSharp.csproj --no-restore -m:2 /nr:false` attempt stopped before C# compilation with `NETSDK1004` because `Temp/obj/Assembly-CSharp/project.assets.json` is missing.
- Later CPU samples remained above threshold; the latest was 100% with 7 active `dotnet` processes, above the 50% build policy gate, so restore/build was not continued.
