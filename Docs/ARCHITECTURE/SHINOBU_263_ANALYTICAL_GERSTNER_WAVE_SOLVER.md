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
- `Shinobu263WaveCounters`: 64-byte `WaveMathCounterLane` rows for evaluated/coarse/nonfinite/stale-origin counters; each mutated lane is isolated to one cache line to prevent counter false sharing. The four rows are cleared synchronously in the locked owner window, not through a tiny scheduled job.

## Math Rules

- AUP is localized in double precision as `SampleAUP - LocalOriginAUP`, then each octave adds a double-precision `dot(direction, LocalOriginAUP) mod wavelength` phase term before float SIMD trig. This preserves absolute Gerstner phase across floating-origin shifts without raw absolute double trigonometry in the hot lane loop.
- Time phase uses `GerstnerWaveTuningDTO.PhaseTimeSeconds@120` as a double and wraps `phaseVelocity * waveNumber * time` in double before float SIMD trig. `ResolvePhaseTimeSeconds` seeds from sanitized legacy `TimeSeconds` only when the double lane is not yet positive/finite, so hot-loaded or partially hydrated DTOs do not snap phase to zero or propagate legacy NaN.
- `GlobalQualityWeight` continuously resolves an octave budget through `math.smoothstep`; the budget is cached once per SIMD group/scalar sample, `ResolveActiveOctaves` schedules the partially active last octave, and `ResolveOctaveWeight` fades its amplitude instead of popping octave rows on/off. It does not change DTO layout, request identity, or authority route.
- Coarse samples use the macro grid. Mixed vector groups still compute full lanes and select per lane; all-coarse groups skip full Gerstner accumulation.
- Packed full evaluation and scalar macro-grid generation both route amplitude through `AnalyticalGerstnerWaveMath.ResolveAmplitude`, including authored storm weight, so quality LOD changes octave count and sampling path without changing the swell amplitude envelope.
- The Dear Lie is explicit: buoyancy samples use requested XZ without iterative horizontal inversion. Rendering remains presentation-owned.
- Fixed tick time is consumed from the dispatcher-provided `fixedDeltaTime`; the runtime does not read Unity `Time.*` inside the solver cadence.
- `GlobalRegistry.DataVault` is used only during cold dependency refresh and hot-swap. `FixedTick` uses the cached Vault interface and generation handles.
- Floating-origin authority is consumed through `IOriginShiftListener` and `HectonFloatingOrigin.LastShiftEvent` cold snapshots. `FixedTick` does not call registry-backed `HectonFloatingOrigin.CurrentTotalOffsetDouble`; `GerstnerWaveTuningDTO.OriginShiftSequence`, `OceanSampleResultDTO.OriginShiftSequence`, and `WaveMathTelemetryEntry.OriginShiftSequence` carry the rebase proof.
- Request `ShiftFrameID` must match `GerstnerWaveTuningDTO.OriginShiftSequence` before any AUP localization. Stale lanes are rejected into `FlagStaleOrigin`, counted in counter lane 3, and excluded from analytical height/normal evaluation.

## Black Box

The solver records the last 300 high-level frames to `WaveMathTelemetryEntry`. `PostFixedTick` locks `Shinobu263WaveTelemetryRing` and `Shinobu263WaveTelemetryCursor` before writing the ring/cursor and before dump readback. `TelemetryCursor[0]` is a monotonic write count, not a wrapped slot, so early dumps and wrapped dumps can both be decoded. If elapsed solver time exceeds the tuning threshold or nonfinite output is detected, it dumps `Docs/AgentLogs/Dump_SHINOBU_263.bin` as a 32-byte little-endian header followed by 64-byte telemetry rows in oldest-to-newest ring order.

Header bytes start with ASCII `H8S263`. Header fields include row size, telemetry capacity, monotonic write count, `AnalyticalGerstnerWaveConstants.KernelHash`, oldest-start slot, and valid-row count. Reserved bytes are zeroed before field writes. The dump header is diagnostic only; it does not change gameplay truth, save identity, rollback state, BufferIDs, or telemetry row stride.

## Verification Status

Implementation is pending Unity import/Burst compile verification. No frame-time claim in this document is treated as verified until a fresh Console/profiler artifact is attached by the integrator.
