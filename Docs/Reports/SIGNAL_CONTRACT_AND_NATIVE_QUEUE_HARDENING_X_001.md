# X_001 Signal Contract And Native Queue Hardening

## Scope

Pass after `SIGNAL_LOCAL_EVENT_COUNTER_RECOVERY_X_001.md`. Runtime files touched in this closure: 27.

Primary target: hidden signal ingress and native queue overflow paths left after GlobalSignals decentralization.

## Changes

- `SignalBus<T>.ConfigureInternal` now rejects mismatched late configuration after a lane is initialized. Matching late config is a no-op; mismatched config increments corrupted signal telemetry and does not mutate capacity/hash.
- `SignalBus<T>.TryEnqueueBounded` rejects fatal-interrupt lanes. Fatal signals must go through owner-phase `TryPush` so the fatal latch path is executed.
- Added DTO-owned capacity/hash contracts for `DeflectSignal`, `DeconstructResultSignal`, `InteractionUiSignal`, `FluidIncursionSignal`, `HabitatFloodAcousticMuffleSignal`, and `ToxicityExposureSignal`.
- Replaced conflicting `FluidIncursionSignal`, `ToxicityExposureSignal`, and `HabitatFloodAcousticMuffleSignal` configure calls with DTO constants.
- Removed job-side `PlayerFatalPressureSignal` enqueue from respawn mock damage. Reconciliation now publishes the fatal signal in owner phase with `TryPush`.
- Added native budget/drop counters for `BurstCallback`, hydraulic erosion height-delta queue, and anomaly deferred flood-fill state queue.
- Bounded procedural wreck propagation queue with explicit count/drop state and deterministic `PropagationQueueOverflow` termination.
- Reset `SargassumGlobalDragManager` debris timer pending count on failed dequeue.
- Banned `VoxelChunkModifiedEvents.Publish` and `VehicleCommandSignalBus.Publish` as compile-time obsolete APIs; runtime call sites use `TryPublish`.
- Converted submarine audio-caption ingress from string sidecar to hash-only `AudioCaptionEvents.TryRaiseHash`. `AudioCaptionPayload` carries `CaptionHashId`; the UI edge resolves known hashes to static strings.

## Capacity And Overflow Rules

| Lane | Capacity | Overflow rule | Managed allocation |
| --- | ---: | --- | --- |
| `SignalBus<T>.ParallelWriter` | `min(expectedCapacity, LaneOverflowFaultThreshold)` per closed generic | Atomic pre-enqueue decrement; overflow increments native drop counter and returns false | 0 bytes |
| `BurstCallback<T>` | configured expected capacity | pending count rejects before native enqueue; parallel writer returns budget on overflow and increments drop counter | 0 bytes |
| `HydraulicErosionHeightDelta` | `applyBudget * applyPassCount`, clamped to `int.MaxValue` | atomic pre-enqueue budget; overflow increments native drop counter | 0 bytes |
| `AnomalyBasinFloodFillState` deferred queue | 1 deferred state per slice | atomic pre-enqueue budget; overflow sets `StatusDeferredOverflow` and stops slice | 0 bytes |
| `ProceduralWreckGenerator` propagation | active grid length | count-gated enqueue; overflow increments drop count and terminates solve with `PropagationQueueOverflow` | 0 bytes |
| `AudioCaptionPayload` | 32 pending payloads | count-gated enqueue; overflow warning once per frame | 0 bytes on hot path |
| `VoxelChunkModifiedEvents` | existing fixed event lane | `TryPublish` only; legacy `Publish` compile-time banned | 0 bytes |
| `VehicleCommandSignalBus` | existing fixed command lane | `TryPublish` only; legacy `Publish` compile-time banned | 0 bytes |

## Canonical Lane Contracts

- `FluidIncursionSignal`: expected 64, max-frame 128, low-tier 16, hash `2553418623`.
- `ToxicityExposureSignal`: expected 64, max-frame 64, low-tier 16, hash `0x54584F58`.
- `HabitatFloodAcousticMuffleSignal`: expected 32, max-frame 32, low-tier 8, hash `0x464C4D46`.
- `DeflectSignal`: expected 128, max-frame 128, low-tier 32, hash `2742711508`.
- `DeconstructResultSignal`: expected 64, max-frame 64, low-tier 16, hash `146807682`.
- `InteractionUiSignal`: expected 128, max-frame 128, low-tier 32, hash `38002005`.

## Verification

- `SignalBus<...>.Push`: 0 project source hits.
- `ThreadSafeCommandQueue.Enqueue`: 0 project source hits.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` outside Core/Signals, Editor, Tests, ModdingAPI: 0 hits.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside Editor, Tests, ModdingAPI: 0 hits.
- `AudioCaptionEvents.Raise`, string `AudioCaptionRequest` constructors, `_captionReferenceSlots`, `_referenceSlotOccupied`: 0 hits in `SpatialAudioManager.cs` and `HectonSubmarineOS.cs`.
- `VehicleCommandSignalBus.Publish` and `VoxelChunkModifiedEvents.Publish`: 0 project source call hits.
- DTO banned-field scan over extracted signal payloads/contracts and toxicity signal types: 0 `GameObject`, `Transform`, `string`, `FixedString`, or native-container fields.
- `AudioCaptionPayload` banned-field scan: 0.
- Configure/prewarm heuristic over runtime `SignalBus<T>.Configure`: `MissingImmediateEnsure=0`.
- Touched-file brace delta: 0.
- `git diff --check` on touched files: no whitespace errors; LF-to-CRLF warnings only.
- Build not launched: guard reported CPU 100 percent with active `csc` and `dotnet` processes.

## Hardware Impact

No profiler/GCMonitor run; runtime microseconds are not claimed. Static impact is lower native queue block pressure and no hot managed string sidecar for submarine captions. Low tier gets deterministic native rejection/coalescing/drop behavior. Middle/high/ultra can spend explicit lane capacity through DTO contracts without changing authority route or DTO layout.
