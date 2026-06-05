# 1823 Audio Callback Zero-Allocation Audit Packet

ID: 1823  
Role: AUDIO_CALLBACK_ZERO_ALLOC_AUDIT_PACKET  
Proof class: STATIC VERIFIED only  
Runtime proof: PENDING VERIFICATION  

No Unity, dotnet build, Play Mode, profiler, Frame Debugger, Memory Profiler, import, compile, or runtime audio test was run. This packet is a static source audit and patch plan only.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `audio.md`
- `performance.md`
- `Docs/Reports/Batch18/1805_AGENT_OUTPUT_TRIAGE_DASHBOARD.md`
- `Docs/Reports/Batch18/1813_STALE_BLOCKER_ERRATA_PACKET.md`
- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`

Checked and absent at root:

- `architecture.md`
- `memory.md`

Relevant mandates read:

- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`

## Callback Inventory

Runtime callbacks found under `Assets/_Project/Scripts/Audio`:

| File | Callback | Static classification |
|---|---:|---|
| `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` | `OnAudioFilterRead(float[] data, int channels)` at line 775 | `YELLOW_MANAGED_TRANSFER_BRIDGE_RELEASE_BLOCKED` |
| `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs` | `OnAudioFilterRead(float[] data, int channels)` at line 421 | `RED_MANAGED_CALLBACK_DECODE_RELEASE_BLOCKED` |

Editor smoke/audit references to `OnAudioFilterRead` were not counted as runtime callbacks.

## Confirmed Findings

| File | Callback / callee | Pattern | Risk | Static proof |
|---|---|---|---|---|
| `DynamicMusicGranularSynthesizer.cs` | `OnAudioFilterRead`, lines 775-827 | Managed Unity audio callback remains active in runtime source. | Release audio cannot claim native/DSP-clean output from static source alone. | `rg -n "OnAudioFilterRead"` |
| `DynamicMusicGranularSynthesizer.cs` | callback body, lines 785-786 | Callback writes `_lastAudioChannels` and `_lastAudioRequestSamples`. | Audio thread mutates owner scheduling/request state. | `Volatile.Write` lines in callback body. |
| `DynamicMusicGranularSynthesizer.cs` | callback body, lines 802-814 | Resolves local `NativeArray<float>` copy buffer and copies into managed `float[]` via `fixed` and `UnsafeUtility.MemCpy`. | Static shape is transfer-only, but still not accepted as release DSP route without native bridge/profiler proof. | `TryResolvePublishedAudioThreadCopyBuffer`, `fixed`, `MemCpy`. |
| `DynamicMusicGranularSynthesizer.cs` | `ZeroManagedAudioBuffer`, lines 2268-2273 | Manual zero-fill of Unity callback buffer on underrun/invalid host. | No static allocation token found; accepted as fail-closed silence path for underrun only. | Manual `for` loop. |
| `VocalBankPlaybackRuntime.cs` | `OnAudioFilterRead`, lines 421-513 | Callback acquires views, decodes vocal bank, times work, writes counters/telemetry, and releases guard. | Managed callback is doing decode and shared-state mutation. Release blocker. | Callback body lines 421-513. |
| `VocalBankPlaybackRuntime.cs` | `TryAcquireAudioCallbackViews`, lines 515-555 | Callback acquires state, codec, telemetry, counters, waveform, and bank byte views. | DataVault/mutation-guard work is on audio callback path. | Calls to `TryAcquireLockedView` at lines 526-531. |
| `VocalBankPlaybackRuntime.cs` | `TryAcquireLockedView`, lines 809-858 | Uses mutation guard and `vault.TryReadHandle`. | Callback may contend on shared runtime ownership. | Guard acquire/read-handle code. |
| `VocalBankPlaybackRuntime.cs` | callback body, lines 463 and 488-489 | `Stopwatch.GetTimestamp()` and elapsed microsecond computation in callback. | Timing work belongs to producer/owner phase, not managed audio callback. | `Stopwatch` lines. |
| `VocalBankPlaybackRuntime.cs` | callback body, lines 464-485 | Pins managed callback data and calls `VocalDecodeKernel.DecodeIntoAudioBuffer`. | Vocal decode happens inside managed callback. | `fixed` and decode call lines. |
| `VocalBankPlaybackRuntime.cs` | callback body, lines 490-505 | Writes counters/telemetry and sets `_dumpRequested`. | Telemetry mutation and dump request are callback-side. | `views.Counters`, `views.Telemetry`, `_dumpRequested`. |
| `VocalBankPlaybackRuntime.cs` | `WriteVocalFault`, lines 873-898 | Fault path writes counters and telemetry from callback context. | Failure telemetry is callback-side. | Direct callee from line 459. |
| `VocalBankContracts.cs` | `VocalDecodeKernel.DecodeIntoAudioBuffer`, line 538 | Unmanaged-style decode kernel is directly invoked by callback. | Kernel shape is not the primary issue; caller phase is. | Direct call from line 472. |

CSV artifact:

- `Docs/Reports/Batch18/1823_AUDIO_CALLBACK_PATTERN_SCAN.csv`

## Confirmed vs Stale Claims

Confirmed:

- `1805` is correct that both runtime callbacks still exist.
- `VocalBankPlaybackRuntime.OnAudioFilterRead` is a hard blocker because it decodes and mutates telemetry in the managed callback.
- `DynamicMusicGranularSynthesizer.OnAudioFilterRead` is still release-blocked because it is a managed callback endpoint without profiler/native bridge proof.

Corrected/overstated:

- DynamicMusic is not currently proven to synthesize or decode inside the callback. Current source stages synthesis in jobs and copies a late-frame buffer into the Unity callback array.
- A static scan cannot prove `0 B` GC, underrun-free playback, CPU budget, audio continuity, or release acceptance.
- Editor smoke tests that assert callback absence elsewhere do not close these two callback blockers.

## Existing Helpers To Reuse

- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`
  - `AudioFrameSpscRingBuffer`
  - `AudioBufferCapacity = 65536`
  - `TryWriteInterleaved(...)`
  - overflow telemetry and 300-entry bridge telemetry
  - descriptor creation for native bridge

- `Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs`
  - `NativeAudioKernelRingBufferDescriptor`
  - `TryRegisterWithRetryGate(...)`
  - native plugin registration/clear/status/dump entry points

- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
  - states `OnAudioFilterRead(float[] data, int channels)` is transfer bridge only
  - requires prebuilt SPSC ring buffer, no synthesis inside managed callback

DynamicMusic also has local `_audioThreadCopyA/_audioThreadCopyB` buffers allocated cold at lines 955-970 and filled in `PublishAudioThreadCopyBufferLateFrame` at lines 1754-1808. Those buffers are useful as a staging concept, but the later patch should prefer the first-party native/SPSC bridge for release output rather than leaving Unity managed callback as the final endpoint.

## Owner-Thread Preparation Targets

DynamicMusic:

- Keep `Tick -> ScheduleSynthJobs` as the synthesis producer path.
- Keep `LateFrameTick -> TryFlushCompletedSynthJob -> PublishAudioThreadCopyBufferLateFrame` as the owner-thread handoff point.
- Move audio block sizing/channel policy out of the callback. Resolve from cold AudioSettings/DSP buffer configuration or a prepublished owner snapshot.
- Write final interleaved blocks into `AudioFrameSpscRingBuffer` or DSPGraph/native audio-kernel output from owner/producer phase.

VocalBank:

- Drain `SignalBus<VocalCueSignal>` and resolve vessel tone in owner Tick only.
- Acquire DataVault views and mutation guards outside the audio callback.
- Decode or pre-mix vocal output into native output staging from owner/worker producer phase.
- Publish output blocks through `AudioFrameSpscRingBuffer` or a DSPGraph/native output job.
- Move timing, counters, fault flags, and telemetry ring writes to producer completion or owner phase.
- Keep dump trigger as owner-phase telemetry response; do not write dump data from the callback.

## Minimal Patch Plan - DynamicMusicGranularSynthesizer

1. Add or reuse an `AudioFrameSpscRingBuffer` field owned by DynamicMusic or shared audio output owner. Initialize cold with power-of-two capacity and channel count.
2. In `LateFrameTick`, after `PublishAudioThreadCopyBufferLateFrame`, write the published interleaved block into the SPSC/native bridge path with `TryWriteInterleaved`.
3. Replace callback-dependent `_lastAudioRequestSamples`/`_lastAudioChannels` writes with a cold/precomputed block request snapshot. The producer should not rely on callback-side mutable state.
4. Remove release use of `OnAudioFilterRead` after native bridge/DSPGraph output is proven. If a temporary editor shim remains, compile-gate it to editor/dev and mark it non-release.
5. Preserve underrun telemetry through ring overflow/underflow counters. Do not log from the audio thread.
6. Validation owner later proves audio continuity, 0 GC, no underruns, and CPU budget in Unity Profiler.

## Minimal Patch Plan - VocalBankPlaybackRuntime

1. Stop calling `VocalDecodeKernel.DecodeIntoAudioBuffer` from `OnAudioFilterRead`.
2. Add a producer phase that acquires state/codec/bank/telemetry views once, decodes the next vocal block into native staging, then releases the mutation guard before audio consumption.
3. Feed decoded vocal frames into `AudioFrameSpscRingBuffer` or a DSPGraph/native audio output route.
4. Move `Stopwatch.GetTimestamp`, `LastDspMicroseconds`, counter writes, waveform writes, and `DspMicroseconds` telemetry writes out of the callback.
5. Keep `WriteVocalFault` behavior but execute it from owner/producer failure handling, not from callback.
6. Treat generated mock bank as diagnostic/fail-closed only. Release path needs authored bank proof and mixer/component prewarm proof.
7. Remove or release-disable the managed callback after native/DSP output is integrated and verified.

## Forbidden Quick Fixes

- Do not silence DynamicMusic or VocalBank to remove findings.
- Do not replace vocal playback with one-shot placeholder loops or flat beeps.
- Do not leave decode/synthesis in `OnAudioFilterRead` and rename helpers to look clean.
- Do not add runtime string logging or dynamic fallback dispatch in the callback.
- Do not add new global buses, event strings, or broad `GlobalRegistry` polling.
- Do not create a second audio ring protocol while `AudioFrameSpscRingBuffer` and native bridge already exist.
- Do not claim `0 B` GC, underrun-free output, or CPU budget without profiler artifacts.

## Low / Middle / High / Ultra Consequences

Low:

- Same cue truth, same music/vocal behavior, fewer concurrent voices/layers.
- SPSC capacity and underrun policy remain fixed and fail-closed.
- Critical warnings, route cues, suit voice, and threat cues remain audible.

Middle:

- Full intended music/vocal behavior at normal sample rate and conservative layer count.
- Owner-thread decode/prep cadence remains bounded.

High:

- More vocal/music layering, richer crossfades, and stronger acoustic detail may be added after profiler proof.
- Gameplay truth and cue IDs do not change.

Ultra:

- Extra reverb/detail/secondary layers are allowed only in presentation/audio output.
- No additional callback allocation, registry polling, DataVault guard contention, or gameplay truth changes.

## Later Validation Plan

Static after patch:

- `rg -n "OnAudioFilterRead" Assets/_Project/Scripts/Audio`
- Scan callback bodies and direct callees for `new`, LINQ, `foreach`, string formatting, `.ToString()`, logs, locks, DataVault view acquisition, scene search, `GetComponent`, registry polling, and `.Complete()`.
- Confirm DynamicMusic and VocalBank no longer invoke decode/synthesis from managed callback.

Editor/build only when assigned and safe:

- Unity script validation/import for touched files.
- No broad build while another build/import/compiler lane is active.

Runtime/profiler when assigned:

- Play scene with DynamicMusic and vocal cues active.
- Unity Profiler with allocation recording: callback/native output path `0 B` GC.
- DSP/audio CPU budget on compact target class.
- Underrun/overflow counters: stable and explained.
- Audio continuity capture: music/vocal behavior preserved, no silence regression.
- Verify authored banks, mixer bindings, listener/audio root components, and pool prewarm.

## Conditions For Source Patching To Become Safe

- Unity editor idle; no import, compile, shader compile, Play Mode, profiler, or player build active.
- No `dotnet`, `csc`, `VBCSCompiler`, Unity compiler, or other build process active.
- System CPU below 50 percent before any compile/build validation.
- No other agent owns the same two source files.
- Current file content re-read immediately before patching.
- Rollback chunk known for each edited file.
- Patch owner is allowed to edit source and report runtime validation honestly.

## Dependencies / Separate Tasks

- Native audio bridge availability and platform coverage must be verified.
- DynamicMusic needs an owner decision: own its ring buffer directly or publish into a central audio output owner.
- VocalBank needs producer-phase decode scheduling and a fixed native output staging contract.
- Authored vocal bank presence must be proven; mock bank must be diagnostic/fail-closed, not release normal.
- A later Unity/profiler agent must produce the actual audio continuity and GC proof.

## Evidence Boundary

STATIC VERIFIED:

- Source paths exist.
- Callback locations and direct call chains were inspected.
- Blocker classification is based on current source text.

PENDING VERIFICATION:

- GC allocation.
- Audio thread CPU time.
- Underrun-free playback.
- Audio continuity.
- Unity import/compile state.
- Player build behavior.
- Native plugin availability.
- DSPGraph/native output behavior.

## Final Classification

- `DynamicMusicGranularSynthesizer`: `YELLOW_MANAGED_TRANSFER_BRIDGE_RELEASE_BLOCKED`.
- `VocalBankPlaybackRuntime`: `RED_MANAGED_CALLBACK_DECODE_RELEASE_BLOCKED`.

AUDIT_PACKET_COMPLETE.

