# SHINOBU_263 Analytical Gerstner Wave Solver

Owner: ECHELON 4 / Hydrodynamic Drag & Buoyancy.

## Route

- Producer phase: `AnalyticalGerstnerWaveRuntime.FixedTick`.
- Completion phase: `AnalyticalGerstnerWaveRuntime.PostFixedTick`.
- Data owner: `GlobalDataVault`, `SystemID.Physics`.
- Consumer route: buoyancy systems read `Shinobu263WaveResults` after the post-fixed owner phase. No GPU or Crest readback is authority.

## DataVault Buffers

- `Shinobu263WaveSpectrum`: packed 64-byte `GerstnerWaveParamsDTO`, 4 wave lanes per row.
- `Shinobu263WaveTuning`: one 128-byte `GerstnerWaveTuningDTO`.
- `Shinobu263WaveRequests`: AUP sample requests with `ShiftFrameID` at byte 40.
- `Shinobu263WaveResults`: height, analytical normal, displacement, result flags, and preserved origin shift sequence at byte 60. `FlagStaleOrigin` marks rejected requests whose `ShiftFrameID` did not match the tuning snapshot sequence.
- `Shinobu263WaveMacroGrid`: cached low-octave swell proxy for coarse requests.
- `Shinobu263WaveTelemetryRing`: 300-entry black box.
- `Shinobu263WaveTelemetryCursor`: telemetry write cursor.
- `Shinobu263WaveProfiles`: CSV wave profile rows.
- `Shinobu263WaveCounters`: 64-byte `WaveMathCounterLane` rows.
- Counters: evaluated, coarse, nonfinite, stale-origin.
- Each mutated lane is one cache line to prevent false sharing.
- Four rows clear synchronously in locked owner window, not tiny scheduled job.

## Math Rules

- AUP localizes in double precision as `SampleAUP - LocalOriginAUP`.
- Each octave adds double-precision `dot(direction, LocalOriginAUP) mod wavelength` phase before float SIMD trig.
- This preserves absolute Gerstner phase across floating-origin shifts without raw absolute double trigonometry in the hot lane loop.
- Time phase:
  - Lane: `GerstnerWaveTuningDTO.PhaseTimeSeconds@120` as double.
  - Wrap: `phaseVelocity * waveNumber * time` in double before float SIMD trig.
  - Legacy seed: sanitized `TimeSeconds` only when the double lane is not positive/finite.
  - Guard: hot-loaded or partially hydrated DTOs cannot snap phase to zero or propagate legacy NaN.
- `GlobalQualityWeight` continuously resolves octave budget through `math.smoothstep`.
- Budget is cached once per SIMD group/scalar sample.
- `ResolveActiveOctaves` schedules the partially active last octave; `ResolveOctaveWeight` fades amplitude instead of toggling rows.
- DTO layout, request identity, and authority route are unchanged.
- Coarse samples use the macro grid. Mixed vector groups still compute full lanes and select per lane; all-coarse groups skip full Gerstner accumulation.
- Packed evaluation and macro-grid generation route amplitude through `ResolveAmplitude`, including storm weight. Quality LOD changes octave count/sampling path, not swell amplitude envelope.
- The Dear Lie is explicit: buoyancy samples use requested XZ without iterative horizontal inversion. Rendering remains presentation-owned.
- Fixed tick time is consumed from the dispatcher-provided `fixedDeltaTime`; the runtime does not read Unity `Time.*` inside the solver cadence.
- `GlobalRegistry.DataVault` is used only during cold dependency refresh and hot-swap. `FixedTick` uses the cached Vault interface and generation handles.
- Floating-origin authority is consumed through `IOriginShiftListener` and `HectonFloatingOrigin.LastShiftEvent` cold snapshots. `FixedTick` does not call registry-backed `HectonFloatingOrigin.CurrentTotalOffsetDouble`; `GerstnerWaveTuningDTO.OriginShiftSequence`, `OceanSampleResultDTO.OriginShiftSequence`, and `WaveMathTelemetryEntry.OriginShiftSequence` carry the rebase proof.
- Request `ShiftFrameID` must match `GerstnerWaveTuningDTO.OriginShiftSequence` before any AUP localization. Stale lanes are rejected into `FlagStaleOrigin`, counted in counter lane 3, and excluded from analytical height/normal evaluation.

## Black Box

- The solver records the last 300 high-level frames to `WaveMathTelemetryEntry`.
- `PostFixedTick` locks `Shinobu263WaveTelemetryRing` and `Shinobu263WaveTelemetryCursor` before writing the ring/cursor and before dump readback.
- `TelemetryCursor[0]` is a monotonic write count, not a wrapped slot, so early dumps and wrapped dumps can both be decoded.
- On elapsed-time breach or nonfinite output, dump target is `Docs/AgentLogs/Dump_SHINOBU_263.bin`.
- Format: 32-byte little-endian header, then 64-byte telemetry rows in oldest-to-newest ring order.

Dump header:

- Magic: ASCII `H8S263`.
- Fields: row size, telemetry capacity, monotonic write count, `AnalyticalGerstnerWaveConstants.KernelHash`, oldest-start slot, valid-row count.
- Reserved bytes: zeroed before field writes.
- Scope: diagnostic only.
- Does not change gameplay truth, save identity, rollback state, BufferIDs, or telemetry row stride.

## Verification Status

Implementation is pending Unity import/Burst compile verification. Frame-time claims require fresh Console/profiler artifact.
