# HECTON-8 Audio DSP Pipeline

Date: 2026-05-07

Status: PENDING VERIFICATION

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not Unity audio-thread behavior, profiler, GC, console, or player-build proof.

- `Assets/_Project/Scripts/SpatialAudioManager.cs`

- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`

- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`

- `Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs`

- `Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs`

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.

- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).

- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.

- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

Historical 2026-05-04 boundary:

- This is the first-party audio DSP architecture reference, not live mixer/profiler proof.

- Older project-state orientation used dated reports. Those paths are historical and may be archived or absent; use the current actuality ledger and topology docs.

- `SpatialAudioManager` and `PlayerCriticalProceduralAudioRenderer` are source-backed owners; runtime pool state, console health, audio-thread cost, and zero-GC transport still require fresh Unity/profiler verification.

## Scope

First-party procedural audio owner path:

- `Assets/_Project/Scripts/SpatialAudioManager.cs`

- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`

The target is zero-allocation runtime transport with sample-stable DSP parameters and deterministic underwater psychoacoustics.

## Documentation Lifecycle

This file is the normative audio DSP architecture reference for first-party runtime systems.

Historical notes and iteration logs under:

- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/`

are archived records only. They are not the source of truth for current runtime ownership, DSP order, or transport contracts.

## Runtime Ownership

- `SpatialAudioManager` owns world-source playback pools, binaural telemetry, passive radar payloads, and delayed acoustic world events.

- `PlayerCriticalProceduralAudioRenderer` owns player-local procedural DSP: hull stress, sonar, thrusters, dread sub-bass, heartbeat, bubble synthesis, and final binaural shaping.

## SPSC Parameter Sync

The procedural renderer uses a double-buffered snapshot handoff between the main thread and the audio producer thread.

### Producer Write Sequence

1. Main thread writes the next `AudioParameterSnapshot` into the inactive slot.

2. Main thread publishes the slot with `Interlocked.Exchange(ref _currentSnapshotReadIndex, nextIndex)`.

3. No audio-thread field mutation is allowed.

### Consumer Read Sequence

1. Audio worker enters one synthesis block.

2. Worker performs `Volatile.Read(ref _currentSnapshotReadIndex)` once.

3. Worker copies the chosen snapshot into a local struct.

4. All samples in the block use that local copy only.

This prevents mid-block parameter tearing and satisfies the SPSC mandate.

## World Event Propagation Delay

Large underwater events must not arrive instantly.

### Data Path

- Ingress queue: `NativeQueue<DelayedAudioEvent>`

- Active schedule: `NativeList<DelayedAudioEvent>`

- Owner: `SpatialAudioManager`

### Delay Math

```text

DelaySeconds = distance(listenerPos, eventPos) / 1480.0

Dispatch when: Time.time >= EventTimeSeconds + DelaySeconds

```

### Current Implosion Path

`HandleFatalPressureImplosion(...)` computes delay from absolute listener position to event position, enqueues `DelayedAudioEvent`, and `ProcessDelayedAudioEvents(...)` dispatches the clip and trauma only after the underwater travel time has elapsed.

## Active Sonar Cone Echoes

The player sonar ping is owned by `PlayerCriticalProceduralAudioRenderer`, not `SpatialAudioManager`, because the final echo clicks must land on the sample-accurate DSP timeline.

### Query Pattern

- dry ping publishes immediately through the existing double-buffered sonar trigger state

- the same ping also schedules `12` async `RaycastCommand` probes in a forward cone

- when the batch resolves, the main thread publishes a same-sequence sonar-state revision with up to `12` echo taps

- the audio worker consumes that revision once per block and renders the delayed clicks through the Hermite delay ring

### Echo Math

```text

EchoDelaySeconds = (hitDistanceMeters * 2.0) / 1480.0

TransmissionLossDb = 20 * log10(pathDistance) + k_absorption * pathDistance

EchoAttenuation = 10^(-TransmissionLossDb / 20) * materialTransmission

```

Each tap also carries:

- Doppler ratio for read-pointer advance

- material-derived low-pass cutoff

- precomputed biquad coefficients when low-pass filtering is required

## 4-Point Hermite Resampling

The canonical fractional-delay / pitch-resample kernel is a 4-point Hermite interpolator over a power-of-two ring buffer.

### Wrap Rule

Every tap must be masked. Naked offset reads are forbidden.

```text

xm1 = buffer[(i - 1) & mask]

x0  = buffer[i & mask]

x1  = buffer[(i + 1) & mask]

x2  = buffer[(i + 2) & mask]

```

### Polynomial

```text

c0 = x0

c1 = 0.5 * (x1 - xm1)

c2 = xm1 - 2.5*x0 + 2*x1 - 0.5*x2

c3 = 0.5*(x2 - xm1) + 1.5*(x0 - x1)

y(t) = ((c3*t + c2)*t + c1)*t + c0

```

### Doppler / Fractional Delay Use

For variable-rate playback:

```text

frac = readPtr - floor(readPtr)

i = floor(readPtr)

sample = hermite(buffer[(i-1)&mask], buffer[i&mask], buffer[(i+1)&mask], buffer[(i+2)&mask], frac)

readPtr += dopplerRatio

```

The renderer already uses masked Hermite helpers for tonal/ring interpolation. Any future world-voice fractional resampler must use the same masked-tap form.

## Hull Stress FM Kernel

Hull stress synthesis is finalized in the procedural renderer and follows the stress-driven FM recipe.

```text

modFreq = lerp(5 Hz, 80 Hz, stress^2)

modIndex = lerp(0.1, 12.0, stress)

carrierFreq = 80 Hz + sin(modPhase * 2Ï€) * modIndex * noise

output = tanh(raw * (1 + stress * 3))

```

At high modulation index the kernel can oversample before decimation to reduce aliasing.

## Psychoacoustic Pressure Filter

High frequencies collapse as the player goes deeper.

### Current Law

```text

HF_cutoff = LPF_OPEN_HZ / (1.0 + depth / 500.0)

```

The renderer clamps this against a minimum cutoff floor to avoid fully killing intelligibility.

## Dread Sub-Bass

Tight caves intensify low-frequency dread.

```text

f_rumble = lerp(30 Hz, 15 Hz, EDI)

gain = baseDepthGain * lerp(0.35, 1.65, EDI)

```

`EDI` is the enclosure density index derived from the six-axis enclosure probe result.

## Sabine Enclosure Reverb

Listener enclosure probing is owned by `AcousticOcclusionUtility` and consumed by `PlayerCriticalProceduralAudioRenderer`.

### Probe Pattern

- 6 orthogonal rays: up, down, left, right, forward, back

- probes are time-sliced in batches of `2` to avoid one-frame spikes

- each hit contributes span and absorption data

### Sabine RT60

```text

V_approx = spanVertical * spanHorizontal * spanDepth

RT60 = 0.161 * (V_approx / TotalAbsorption)

```

`TotalAbsorption` is equivalent absorption area from six surfaces.

The result is clamped to authored floor/ceiling limits before updating listener reverb decay.

## Procedural Bubble Synthesis

Bubble chirps are generated as short decaying sine bursts while the plasma cutter boils water.

### Minnaert Frequency

```text

f_bubble = (1 / (2πR)) * sqrt((3 * γ * P_ambient) / ρ_water)

```

Where:

- `R` = bubble radius in meters

- `γ` = heat capacity ratio

- `P_ambient = 101325 + ρ_water * g * depth`

- `ρ_water` = water density

### Envelope

Each spawned bubble uses:

- randomized radius within authored min/max bounds

- exponentially decaying gain

- sine carrier at `f_bubble`

## Water / Air Binaural Blend

`SpatialAudioManager` publishes `WaterDensityMul` in `BinauralEmitterTelemetry`.

`PlayerCriticalProceduralAudioRenderer` consumes it per block to blend:

- ITD behavior between air and water propagation

- ILD contra-ear floor between air and underwater shadow models

This keeps interior-base acoustics from sounding fully submerged.

## OnAudioFilterRead Contract

`OnAudioFilterRead(float[] data, int channels)` is not an approved synthesis/decode route.

Current owner status:

- `PlayerCriticalProceduralAudioRenderer`: no managed `OnAudioFilterRead`; native bridge route only.
- `DynamicMusicGranularSynthesizer`: transitional managed transfer bridge. The callback may only copy from `_audioThreadCopyA/B` into Unity's buffer or zero-fill underruns. Synthesis, DataVault locks, job scheduling, `Stopwatch`, and `AudioSettings` queries must stay outside the callback.
- `VocalBankPlaybackRuntime`: release player callback is fail-closed and silent. Editor/Development legacy decode exists only as a temporary authoring/debug seam. Release vocal playback requires native/DSPGraph/native audio-kernel output before acceptance.

Rules:

- no `new float[]`

- no LINQ

- no per-call container growth

- no synthesis work inside the managed callback

- no decode work inside the managed callback

- no DataVault lock/mutation guard acquisition inside the managed callback

- no `Stopwatch`, `AudioSettings`, scene lookup, or gameplay query inside the managed callback

Any remaining managed callback only pulls interleaved frames from a prebuilt ring/copy buffer into Unity's output buffer, or fail-closes to silence.
