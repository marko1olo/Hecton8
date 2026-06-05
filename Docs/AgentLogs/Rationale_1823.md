# Rationale 1823

Proof class: STATIC VERIFIED only.

## Decisions

1. Classified `DynamicMusicGranularSynthesizer.OnAudioFilterRead` as `YELLOW_MANAGED_TRANSFER_BRIDGE_RELEASE_BLOCKED`, not as confirmed in-callback synthesis. Current source copies from a prefilled audio-thread copy buffer into Unity's managed callback array. The blocker is the remaining managed callback endpoint and missing compact-device DSP/profiler proof.

2. Classified `VocalBankPlaybackRuntime.OnAudioFilterRead` as `RED_MANAGED_CALLBACK_DECODE_RELEASE_BLOCKED`. Current source acquires DataVault views, decodes the bank, runs `Stopwatch`, writes counters/telemetry, and releases mutation guards inside the managed callback.

3. Patch packet routes both systems toward the existing first-party native audio bridge and `AudioFrameSpscRingBuffer` instead of adding a new transport. The project already has descriptor validation, power-of-two ring capacity, overflow telemetry, and native bridge registration scaffolding.

4. No source patch was made. User explicitly restricted this run to report artifacts.

