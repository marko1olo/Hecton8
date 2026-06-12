# AUDIO DSP Scout Report X_016

Agent: `X_016`
Role: `SPATIAL_AUDIO_DSP_AND_PORTAL_GRAPH_SCOUT`
Mode: read-only C# audit. No source mutation. No compile run.

## Critical Finding

`NativeAudioKernelRingBufferDescriptor` requires 8-byte alignment for `WriteIndex`, but `AudioFrameSpscRingBuffer.TryCreateNativeDescriptor` sets it to `sharedStatePtr + WriteIndexSlot` where `sharedStatePtr` is `int*` and `WriteIndexSlot` is `1`.

Evidence:
- `Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs:31-38` defines `RequiredAlignmentBytes = 8`, `ReadIndexSlot = 0`, `WriteIndexSlot = 1`.
- `Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs:85-90` rejects non-8-byte-aligned `WriteIndex`.
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:254-267` assigns `WriteIndex = (IntPtr)(sharedStatePtr + 1)`.
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:7224-7265` clears the native bridge and logs failure when the descriptor is rejected.

Static consequence: if the `NativeArray<int>` shared-state base is 8-aligned, slot 1 is base+4, so `WriteIndex` fails the descriptor alignment test. This can prevent native master-bus registration.

## Portal Graph

Capacities:
- `AcousticPortalConstants.MaxPathNodes = 30`, `MaxPathEdges = 60`, `TelemetryFrameCount = 300`: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:12-16`.
- SpatialAudioManager mirrors the node/edge caps at `Assets/_Project/Scripts/SpatialAudioManager.cs:523-524`.
- Portal cache capacity is `16`: `Assets/_Project/Scripts/SpatialAudioManager.cs:525`.
- Maximum delayed play presentation is `1.25` seconds: `Assets/_Project/Scripts/SpatialAudioManager.cs:528`, used at `7133-7142`.

Byte layouts:
- `AcousticAup`: 40 bytes, explicit, ARM64-aligned. Fields: `long` grid x/y/z at 0/8/16, `float3 Local` at 24, `uint _pad0` at 36. Evidence: `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:10-19`.
- `AcousticPortalNode`: 56 bytes, explicit, ARM64-aligned. `AcousticAup Position` at 0, edge metadata at 40/44, room volume at 48, flags/reserved bytes at 52-55. Evidence: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:49-66`.
- `AcousticPortalEdge`: 16 bytes, explicit, ARM64-aligned. To-node at 0, distance at 4, flags/reserved at 8-15. Evidence: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:68-83`.
- `AcousticPathQuery`: 112 bytes, explicit, ARM64-aligned. Source/listener AUPs consume 0-79, listener right 80-91, counts/quality/gates 92-111. Evidence: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:85-108`.
- `AcousticPathResult`: 104 bytes, explicit, ARM64-aligned, but bytes 100-103 are unnamed tail padding. Evidence: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:149-217`.
- `AcousticPortalCacheEntry`: 256 bytes, explicit, ARM64-aligned. Source AUP 0-39, listener AUP 40-79, result 80-183, key/frame/valid 184-192, explicit padding through 255. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:642-677`.

Graph build flow:
- Quality gate uses `smoothstep(0.12, 0.92, GlobalQualityWeight)`: `Assets/_Project/Scripts/SpatialAudioManager.cs:7335-7339`.
- Cache read occurs before graph build and writes telemetry on hit: `Assets/_Project/Scripts/SpatialAudioManager.cs:7244-7251`.
- Habitat graph is tried before voxel macro route: `Assets/_Project/Scripts/SpatialAudioManager.cs:7342-7368`.
- Voxel route clamps to 30 nodes and emits adjacent edges under the 60-edge cap: `Assets/_Project/Scripts/SpatialAudioManager.cs:7370-7433`.
- Habitat BFS expands mapped habitat nodes until local node cap 30, then emits local edges until 60: `Assets/_Project/Scripts/SpatialAudioManager.cs:7448-7586`.
- `AcousticPathJob` is executed synchronously through `pathJob.Execute()`: `Assets/_Project/Scripts/SpatialAudioManager.cs:7296-7311`.

Pathfinding:
- `AcousticPathJob` clamps node count, edge count, and max expansions: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:287-310`.
- Main traversal loop exits on `openCount <= 0` or `expanded >= maxExpansions`: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:343-398`.
- Corners and sealed bulkheads reduce transmission/low-pass and add delay; ITD uses listener right vector: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:462-573`.

## Virtual Voice System

Capacity facts:
- Maximum virtual voices: `1000`: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:732-735`, mirrored at `Assets/_Project/Scripts/SpatialAudioManager.cs:495`.
- Maximum physical voices: `64`: same constants, mirrored at `SpatialAudioManager.cs:496`.
- Low-tier physical voices: `12`: same constants, mirrored at `SpatialAudioManager.cs:497`.
- Continuous budget: `clamp((int)lerp(12, 64, saturate(GlobalQualityWeight)), 12, 64)`: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:871-879`.
- Leviathan roar limit: `3`; bubble limit: `10`: `Assets/_Project/Scripts/SpatialAudioManager.cs:436-437`, `9666-9677`.

Core layouts:
- `VirtualVoiceDTO`: 48 bytes, explicit, ARM64-aligned, no implicit tail padding. Evidence: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:52-73`.
- `AcousticSourceDTO`: 64 bytes, explicit, ARM64-aligned. Evidence: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:78-92`.
- `AcousticDspOutputDTO`: 64 bytes, explicit, ARM64-aligned. Evidence: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:97-116`.
- `VirtualVoiceRequest`: 128 bytes, explicit, ARM64-aligned, unnamed padding at bytes 105-107 and 112-127. Evidence: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:138-255`.
- `VirtualVoice`: 160 bytes, explicit, ARM64-aligned, unnamed padding at bytes 125-159. Evidence: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:261-312`.
- `VirtualVoiceSelection`: 144 bytes, explicit, ARM64-aligned, unnamed padding at bytes 117-143. Evidence: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:334-381`.
- `VirtualVoiceTelemetryEntry`: 64 bytes, explicit, ARM64-aligned. Evidence: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:476-521`.

Allocation and culling:
- `QueueSoundEmissionSignal` rejects when pools are unavailable/full, AUP is nonfinite, or clip lookup fails: `Assets/_Project/Scripts/SpatialAudioManager.cs:2442-2455`.
- `AppendVirtualVoice` computes stable key and writes `VirtualVoice`, `VirtualVoiceDTO`, `AcousticSourceDTO`, and previous AUP to NativeArray pools: `Assets/_Project/Scripts/SpatialAudioManager.cs:2685-2838`.
- `FastTick` swaps write/sort buffers, clamps write count, computes quality and physical budget, then schedules `VirtualVoiceSortJob`: `Assets/_Project/Scripts/SpatialAudioManager.cs:1670-1796`.
- `VirtualVoiceSortJob` computes weight from volume, priority, distance, occlusion, foveation, and rollback state: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:80-190`.
- Sort implementation is iterative quicksort with `stackalloc int[64]` and shell-sort fallback; comparison is weight descending, stable key ascending: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:269-375`.
- Selection is capped to physical voice limit; excess audible voices become `StolenVoices`: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:197-229`.
- If sort is not complete during no-wait finalization, it records dropped batch telemetry instead of blocking: `Assets/_Project/Scripts/SpatialAudioManager.cs:3342-3359`.

Presentation hydration:
- Selected virtual voices hydrate onto Unity `AudioSource` pool entries: `Assets/_Project/Scripts/SpatialAudioManager.cs:3420-3482`, `3538-3685`.
- World pool default is 32 and range is 4..32; 2D pool default is 8 and range is 2..16: `Assets/_Project/Scripts/SpatialAudioManager.cs:721-729`.
- Source eviction picks inactive first, otherwise quietest active source with oldest start-time tie breaker: `Assets/_Project/Scripts/SpatialAudioManager.cs:5560-5607`.
- SpatialAudioManager uses `source.Play()` / `source.PlayDelayed()`, not `PlayOneShot`: `Assets/_Project/Scripts/SpatialAudioManager.cs:7133-7142`.

## DSP Flow

Hull stress unmanaged kernel:
- `EvaluateHullStressGranularAudioDelegate` takes raw pointers for output, voices, PCM bank, telemetry ring, cursor, and block params: `Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs:118-128`.
- Burst function pointer is built through `GetOrCreateAudioCallback`: `Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs:231-265`.
- `GranularVoiceDTO` is 64 bytes explicit; `AudioDspTelemetryEntry` is 64 bytes explicit; `HullStressAudioBlockParamsDTO` is 96 bytes explicit. Evidence: `Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs:18-116`.
- Polyphony scales continuously from 8 to 64 by `GlobalQualityWeight`: `Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs:146-153`.
- `EvaluateBlock` uses unmanaged pointers and bounded loops; no managed allocation instruction found in this inner loop: `Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs:270-470`.

Player-critical renderer:
- `GranularVoiceCapacity = 64`, `GranularTelemetryCapacity = 300`: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:136-138`.
- `AudioParameterSnapshot` is 256 bytes explicit; `AudioParameterSnapshotSlot` is 320 bytes explicit and cache-line padded: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:1516-1648`.
- Snapshot publishing writes the inactive slot and publishes the index with `Interlocked.Exchange`: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:11662-11733`.
- A managed background thread named `Hecton8ProceduralAudioProducer` produces audio blocks, not an `IAudioOutputJob`: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:2786-2876`.
- The producer reads one snapshot per block, renders hull/sonar/impact/thruster/heartbeat/bubble/reverb/binaural stages, writes to SPSC ring, and increments produced sample count: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:3070-3232`.
- The SPSC ring default capacity is 65536 frames; write path uses `Volatile.Read` and `Volatile.Write`: `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:13`, `150-217`, `318-340`.

Adjacent contamination:
- `PlayerCriticalProceduralAudioRenderer` contains no `OnAudioFilterRead` hit in the audit search.
- Adjacent synthesis modules still declare managed callbacks: `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:318` and `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:616`.

## Black Boxes

- Virtual voice black box: 300 entries, dump path `Docs/AgentLogs/Dump_ACOUSTIC_SURGEON.bin`, writes on sort/occlusion telemetry and dumps on nonfinite or over-budget thresholds. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:499-501`, `4699-4776`, `4811-4855`.
- Portal black box: 300 entries, dump path `Docs/AgentLogs/Dump_ACOUSTIC_PORTAL_PROPAGATION.bin`, dumps on nonfinite portal result. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:529`, `7746-7865`.
- Procedural synth telemetry: 300 granular entries and cold dump paths under `Docs/AgentLogs`. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:136-140`, `10580-10753`.

## Risk Ledger

1. Critical: native ring `WriteIndex` alignment mismatch can reject bridge registration. Evidence above.
2. High: portal graph path uses synchronous `IJob.Execute()` on caller path. Bounded to 30 nodes/60 edges, but not scheduled. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:7296-7311`.
3. Medium: force-complete barriers exist for virtual sort and acoustic occlusion on origin shift and explicit barrier paths. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:1834-1843`, `2990-2999`, `3370-3380`.
4. Medium: several explicit-layout DTOs have unnamed tail padding. ARM64 size alignment is valid; padding hygiene is incomplete.
5. Medium: adjacent audio modules still use `OnAudioFilterRead`; not part of player-critical renderer path, but present in first-party audio tree.

## APEX Override Addendum

### AcousticPortalNode Exact Serialization

`AcousticPortalNode` is an explicit 56-byte value struct. Evidence: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:49-66`.

Field map:
- `Position : AcousticAup` at offset `0`, size `40`.
- `FirstEdge : int` at offset `40`, size `4`.
- `EdgeCount : int` at offset `44`, size `4`.
- `RoomVolumeCubicMeters : float` at offset `48`, size `4`.
- `Flags : AcousticPortalFlags` at offset `52`, size `1`.
- `_reserved0 : byte` at offset `53`, size `1`.
- `_reserved1 : ushort` at offset `54`, size `2`.

`AcousticAup` is explicit 40 bytes: `GridX long` offset `0`, `GridY long` offset `8`, `GridZ long` offset `16`, `Local float3` offset `24`, `_pad0 uint` offset `36`. Evidence: `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:10-19`.

Alignment verdict:
- `AcousticPortalNode` size is `56`, divisible by `8`, but not a 64-byte cache line.
- It uses `56 / 64 = 0.875` cache lines per isolated node and `8` nodes consume exactly `448` bytes, or `7` full 64-byte cache lines.
- No implicit tail padding exists in `AcousticPortalNode`; offsets cover bytes `0..55`.
- The struct is ARM64-safe for 8-byte fields because `AcousticAup` begins at offset `0`; subsequent fields are int/float/byte/ushort.
- It is not cache-line padded by design. That is not overengineering; it saves 8 bytes per node versus a 64-byte padded node. With `MaxPathNodes = 30`, savings are `30 * 8 = 240` bytes per node buffer. Evidence for cap: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:12-16`.

`AcousticPortalEdge` is 16 bytes, explicit, offsets `0..15`. Evidence: `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:68-83`.
With `MaxPathEdges = 60`, edge storage is `60 * 16 = 960` bytes exactly `15` cache lines. Node storage is `30 * 56 = 1680` bytes exactly `26.25` cache lines. Combined portal node+edge storage is `2640` bytes, `41.25` cache lines. This is compact, not cache-line isolated.

### Virtual Voice Culling and Sort Mathematics

Ingress reject conditions from `QueueSoundEmissionSignal`: reject if write pool unavailable, `_virtualVoiceWriteCount >= 1000`, clip lookup fails, or `AcousticAup` is nonfinite. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:2442-2455`.

Priority before append:
- If `foveatedTier >= 2`, priority is `0`. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:4302-4305`.
- Base priority is `max(0.001, saturate(volume))`. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:4307`.
- `SealedBulkhead` multiplies priority by `0.75`. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:4308-4309`.
- `Solid` multiplies priority by `0.5`. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:4310-4311`.

Sort job limits:
- `totalVoices = clamp(VoiceCount, 0, min(Voices.Length, SortKeys.Length))`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:42-43`.
- `qualityWeight = saturate(SanitizeFinite(GlobalQualityWeight, 0))`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:48`.
- `budgetLimit = ResolveContinuousVoiceBudget(qualityWeight)`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:49`.
- `safeLimit = clamp(min(PhysicalVoiceLimit, budgetLimit), 0, Selections.Length)`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:50`.
- `ResolveContinuousVoiceBudget(q) = clamp((int)lerp(12, 64, saturate(q)), 12, 64)`. Evidence: `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:871-879`.

Per voice cull:
- Cull if source AUP is nonfinite or `FoveatedTier >= 2`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:80-88`.
- `volume = saturate(SanitizeFinite(voice.Volume, 0))`.
- `importance = max(0, SanitizeFinite(voice.Priority, 0))`.
- `relative = AcousticAup.RelativeFloat3(source, listener)`.
- `distanceSq = lengthsq(relative)`, nonfinite becomes `float.MaxValue * 0.25`.
- `attenuation = 1 / max(1, distanceSq)`.
- `effectiveVolume = volume * attenuation`.
- `weight = effectiveVolume * importance`.
Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:90-119`.

Occlusion:
- Occluded if `SdfOccluded` flag is set or `ResolveSdfLineOcclusion(...)` returns true. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:103-112`.
- If occluded: `volume *= occlusionPenalty`, `lowPass = min(lowPass, occludedLowPass)`, count increments. Evidence: same range.
- SDF taps: `taps = ResolveSdfTapCount(q)`, where `curve = q*q*(3-2*q)` and taps are `round(lerp(1, 8, curve))`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:233-250`, `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:881-887`.
- `solid01 = solid / taps`; occluded if `solid01 > lerp(0.95, 0.08, saturate(q))`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:238-249`.

Hard drop conditions inside sort:
- Rollback active forces volume/effectiveVolume/weight to zero and culls. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:120-137`.
- Cull if `importance <= 0` or `effectiveVolume < 0.01`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:139-143`, constant at `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:738`.

Selection:
- Audible voices are compacted into the front of `Voices`; sort keys use `{ Weight, VoiceIndex, StableKey }`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:181-190`.
- Sort is iterative quicksort with `stackalloc int[64]` and shell-sort fallback on stack overflow. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:269-366`.
- Comparator: higher `Weight` wins; equal weight chooses lower `StableKey`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:368-375`.
- `selectedCount = min(safeLimit, audibleCount)`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:196-203`.
- `StolenVoices = max(0, audibleCount - selectedCount)`. Evidence: `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs:211-229`.

Overload hang prevention:
- Sort job is scheduled once per FastTick, not force-completed there. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:1760-1797`.
- If previous sort is incomplete during no-wait finalization, it records telemetry and returns false; current write count is accounted as dropped at the FastTick guard. Evidence: `Assets/_Project/Scripts/SpatialAudioManager.cs:1579-1593`, `Assets/_Project/Scripts/SpatialAudioManager.cs:3342-3359`.
- This prevents a hot-path wait at overload, but drops the batch. It is stable by refusal, not by infinite throughput.

### Hull Stress DSP Parameter Conveyor

Vault allocations for hull/granular DSP:
- `MetallicGrainBank`, `VoiceActive`, `VoiceElapsed`, `VoiceLength`, `VoiceStart`, `VoiceSeed`, `VoiceCursor`, `VoicePlaybackRate`, `VoiceGain`, and `GranularTelemetryRing` are DataVault buffers. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6545-6555`.
- `GranularVoiceVaultViews` stores those NativeArrays. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:624-635`.
- Frame scratch buffers include `HullScratch` and `StereoMixScratch`. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:661-672`.

Packing:
- `PublishAudioParameterSnapshot` builds a 256-byte `AudioParameterSnapshot`, including hull stress, structural stress, velocity, fatigue, snap, pressure depth, absolute depth, enclosure density, granular max voice count, pitch/length/overlap/FM tuning, and binaural fields. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:1516-1639`, `11662-11723`.
- Snapshot publish writes the inactive slot and uses `Interlocked.Exchange(ref _audioParameterSnapshotReadIndex, inactiveIndex)`, then wakes the producer thread. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:11725-11733`.

Producer:
- `ProduceAudioBlock` resolves Vault views through `CanProduceAudioBlock`, then reads one snapshot with `Volatile.Read(ref _audioParameterSnapshotReadIndex)`. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:3070-3087`.
- It clamps granular max voices to `0..64`, derives `granularAccelerationPitchWobble = lerp(0.96, 1.08, thrusterAcceleration)`, clamps granular tuning, then calls `RenderHullStressBlock`. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:3128-3174`.
- `RenderHullStressBlock` trims inactive voices above budget, interpolates per-frame stress parameters, generates pressure bed/creak/granular/fatigue/snap/impact/subbass/scrubber components, soft-clips, and writes `HullScratch[frameIndex]`. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9288-9410`.
- `RenderStructuralGranularVoices` clamps `voiceLimit`, arms impact cluster grains on cooldown, arms stochastic structural grains by `eventThreshold = saturate(eventsPerSecond / sampleRate)`, renders active voices only up to `voiceLimit`, and deactivates voices when elapsed reaches length. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:10372-10558`.
- `ResolveGranularVoiceSlot` returns first inactive slot. If full and not high-priority, returns `-1`. If high-priority, steals only the voice with shortest remaining tail when `tailSamples <= GranularImpactStealTailSamples`. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:10942-10980`.
- Final block path mixes/filter/spatializes, then writes interleaved stereo to `_sampleRingBuffer.TryWriteInterleaved(...)`; on success it increments `_producedSampleCount`. Evidence: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:3206-3231`.
- `TryWriteInterleaved` rejects invalid capacity/channels/overflow, writes samples into NativeArray ring, then publishes `WriteIndex` with `Volatile.Write`. Evidence: `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:150-217`.
