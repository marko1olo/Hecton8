# DSP_AUDIO_SYNTHESIS.md

## RING_BUFFER_ARCHITECTURE

### SPSC_LOCK_FREE_QUEUE
- Capacity: Power-of-two (16384/32768/65536). Mask wrap: `index & (capacity - 1)`
- Write Index: Producer-owned `volatile int`. Read Index: Consumer-owned `volatile int`
- Thread Barrier: `Interlocked.MemoryBarrier()` after write-index advance, before read-index advance
- Availability Calc: `available = (writeIdx - readIdx) & mask`. Never allow `available == capacity` (full detection ambiguity)
- Underrun Protocol: Consumer reads zeros if `available == 0`. No stale memory access
- NativeArray Backing: Allocate `Allocator.Persistent`. Dispose only after producer thread confirmed dead via `ManualResetEventSlim`

### DOUBLE_BUFFER_PARAMETER_SYNC
- Producer reads from `paramsReadCopy`. Consumer writes to `paramsWriteCopy`. Swap pointers via `Interlocked.Exchange` every 512 samples
- Eliminates mutex in hot path. Allows <1ms parameter lag tolerance

---

## PHASE_ACCUMULATION_INTEGRITY

### CANONICAL_PHASE_ADVANCE
```
phase += freqHz * invSampleRate  // invSampleRate = 1.0f / 48000.0f precomputed
phase -= floor(phase)            // Wrap to [0,1). FORBID modulo due to sign ambiguity with negative freq
```
- FORBID `time * freq`: Accumulates float precision error. Causes phase discontinuities after ~10 seconds at 440Hz
- Use `double` for phase accumulator if synth runs >60sec continuous. Downcast to `float` only for sin/table lookup

### WAVETABLE_INTERPOLATION
- Table Size: 2048 samples (covers 20Hz-20kHz at 48kHz with <0.01% THD)
- Index Calc: `idx = phase * tableSize`. Fractional part: `frac = (phase * tableSize) - floor(phase * tableSize)`
- Linear Interp: `out = table[i0] * (1-frac) + table[i1] * frac` where `i1 = (i0+1) & tableMask`
- Hermite Interp (Quality Tier): 4-point. Cost: 9 FLOPS vs 3 FLOPS linear. Use for tonal sounds >200Hz only

---

## FM_SYNTHESIS_KERNEL

### OPERATOR_CHAIN_STRUCTURE
```
Modulator Phase Advance -> sin(modPhase) * modulationIndex
Carrier Phase = basePhase + modulatorOutput
Carrier Output = sin(carrierPhase * TWO_PI)
```
- Modulation Index: Scales frequency deviation. `index=5` at 100Hz carrier = ±500Hz deviation
- Anti-Alias: Limit `modulationIndex` to `sampleRate / (4 * carrierFreq)` or use minBLEP residuals

### HULL_STRESS_RECIPE
```
stressParam ∈ [0,1]  // 0=intact, 1=critical

NOISE_GEN:
  white = (rand() / RAND_MAX) * 2 - 1
  filtered = biquadHP(white, cutoff=300Hz)  // Remove rumble
  
MODULATOR:
  modFreq = lerp(5Hz, 80Hz, stressParam²)   // Exponential stress feels more urgent
  modIndex = lerp(0.1, 12, stressParam)
  modPhase += modFreq * invSampleRate
  modOut = sin(modPhase * TWO_PI) * modIndex * filtered
  
CARRIER:
  carrierFreq = 80Hz + modOut
  carrierPhase += carrierFreq * invSampleRate
  carrierPhase -= floor(carrierPhase)
  raw = sin(carrierPhase * TWO_PI)
  
SHAPER:
  output = tanh(raw * (1 + stressParam * 3))  // Soft-clip distortion at high stress
  dcBlock = highpass1Pole(output, 20Hz)       // Kill DC offset from tanh asymmetry
```

### PROPULSION_THRUSTER_SYNTHESIS
```
throttle ∈ [0,1]

BASE_NOISE:
  white = hash(sampleIndex) * 2 - 1         // PCG or xorshift. FORBID rand() in audio thread
  pink = pinkingFilter(white)               // 1/f noise via Paul Kellett's economy filter
  
BANDPASS_SHAPING:
  center = lerp(200Hz, 1200Hz, throttle)
  Q = 2.5  // Resonant mechanical character
  bp = biquadBP(pink, center, Q)
  
COMB_RESONANCE:
  delaysamples = sampleRate / (120Hz + throttle * 400Hz)  // Blade-pass frequency simulation
  combOut = delayLine[writeIdx - delaySamples] * 0.6 + bp * 0.4
  delayLine[writeIdx] = combOut
  writeIdx = (writeIdx + 1) & delayMask
  
AMPLITUDE_ENVELOPE:
  targetAmp = throttle² * 0.7               // Quadratic response feels more mechanical
  currentAmp += (targetAmp - currentAmp) * 0.01  // 1-pole smoothing (100ms @ 48kHz)
  output = combOut * currentAmp
```

---

## BIQUAD_FILTER_OPTIMIZATION

### COEFFICIENT_CALCULATION_CADENCE
- Compute `a0,a1,a2,b0,b1,b2` once per **512-sample block** or on parameter change
- Intra-block Freq Modulation: LERP coefficient sets. Precompute `coeffStart[6]` and `coeffEnd[6]`, interpolate per-sample

### CANONICAL_COOKBOOK_FORMULAE
```
// Lowpass
ω0 = TWO_PI * cutoffFreq * invSampleRate
α = sin(ω0) / (2 * Q)
b0 = (1 - cos(ω0)) / 2
b1 = 1 - cos(ω0)
b2 = b0
a0 = 1 + α
a1 = -2 * cos(ω0)
a2 = 1 - α

// Normalize
b0/=a0; b1/=a0; b2/=a0; a1/=a0; a2/=a0;

// Direct Form I
y[n] = b0*x[n] + b1*x[n-1] + b2*x[n-2] - a1*y[n-1] - a2*y[n-2]
```

### DENORMAL_PREVENTION
- Add `1e-15f` to `y[n-1]` and `y[n-2]` state variables before multiply
- Or flush-to-zero via `_MM_SET_FLUSH_ZERO_MODE(_MM_FLUSH_ZERO_ON)` (x86 only, not Burst-compatible)
- Burst Denormal Handling: Automatic via `[BurstCompile(FloatMode = FloatMode.Fast)]`

### TRANSPOSED_DIRECT_FORM_II
```
// More cache-friendly. 2 states instead of 4
s1 = b1*x + a1*y_prev + s2
s2 = b2*x + a2*y_prev
y = b0*x + s1
```
- Use for reverb/delay where filters chain deeply

---

## ANTI_ALIASING_STRATEGIES

### POLYBLEP_OSCILLATOR
- For square/saw waves. Add residual correction at phase discontinuity
```
t = phase
dt = freqHz * invSampleRate

if t < dt:
  polyBlep = (t/dt - 1)² - 1
else if t > 1 - dt:
  polyBlep = ((t-1)/dt + 1)² - 1
else:
  polyBlep = 0
  
sawOutput = (2*phase - 1) - polyBlep
```
- Cost: 6 FLOPS per discontinuity. Amortized over cycle length

### OVERSAMPLING_PROTOCOL
- 2x Oversample for FM synthesis with `modulationIndex > 8`
- Upsample: Zero-stuff + 2-pole Butterworth LPF at Nyquist/2
- Downsample: 4-pole Elliptic LPF + decimate
- Memory Cost: 2x scratch buffer. CPU: +60% synthesis cost. Reserve for Quality/Ultra tiers only

---

## DOPPLER_SHIFT_IMPLEMENTATION

### VELOCITY_BASED_PITCH_SHIFT
```
soundSpeed = 1484 m/s  // Underwater
relativeVelocity = dot(listenerVel - emitterVel, normalize(emitterPos - listenerPos))

// Guard clamping
relativeVelocity = clamp(relativeVelocity, -soundSpeed*0.9, soundSpeed*0.9)

dopplerRatio = (soundSpeed + relativeVelocity) / (soundSpeed - relativeVelocity)
shiftedFreq = baseFreq * dopplerRatio
```

### PITCH_SHIFT_VIA_RESAMPLING
- Variable Playback Rate: Advance read pointer by `dopplerRatio` samples per output sample
- Fractional Delay: 4-point Hermite interpolation on ring buffer
```
frac = readPtr - floor(readPtr)
i = floor(readPtr)
out = hermite(buffer[i-1], buffer[i], buffer[i+1], buffer[i+2], frac)
readPtr += dopplerRatio
```

### DISCONTINUITY_SMOOTHING
- LERP `dopplerRatio` over 128 samples when velocity changes >10 m/s/frame
- Prevents pitch "popping" during rapid sub rotations

---

## REVERB_CONVOLUTION_LITE

### FEEDBACK_DELAY_NETWORK
- 8 All-Pass filters (delays: 142, 107, 379, 277, 1491, 1153, 2311, 1913 samples at 48kHz)
- 4 Comb filters with modulated delay (LFO depth ±3 samples, rate 0.3Hz) for chorus shimmer
- Hadamard Matrix mixing between comb outputs (orthogonal energy distribution)

### SCHROEDER_REVERB_RECIPE
```
// Parallel Comb Bank
for i in 0..3:
  combOut[i] = combFilter(input, delayTime[i], feedback=0.7)
  
combSum = (combOut[0] + combOut[1] + combOut[2] + combOut[3]) * 0.25

// Series Allpass Chain
ap1 = allpass(combSum, 142, feedback=0.5)
ap2 = allpass(ap1, 107, feedback=0.5)
wetOutput = ap2
```

### OCEAN_ACOUSTIC_TUNING
- PreDelay: 80ms (simulates distance to surface/floor reflection)
- Comb Delay Times: Scale to 340ms, 420ms, 580ms, 710ms (low-frequency resonance of confined spaces)
- High-Frequency Damping: 1-pole LPF at 2kHz in comb feedback path (water absorbs treble)

---

## PROCEDURAL_AMBIENT_LAYERS

### PRESSURE_CREAKING_SYNTHESIS
```
trigger = poissonProcess(λ = 0.2 + depthParam * 0.8)  // Events per second

if trigger:
  grainDuration = rand(0.3, 1.2) seconds
  grainPitch = rand(60, 180) Hz
  grainEnv = ADSR(attack=0.05, decay=0.2, sustain=0, release=grainDuration)
  
  modulator = bandlimitedNoise(100Hz, 800Hz) * depthParam
  carrier = sin(grainPitch + modulator * 20)
  output = carrier * grainEnv * tanh(depthParam * 2)
```

### BIOLOGICAL_AMBIENCE
- Poisson-distributed whale calls (λ = 0.05/sec)
- FM synthesis: Carrier 200Hz, Modulator 0.5Hz, Index sweeps 0→40 over 3 seconds
- Reverb send: 60% wet. Simulates distant biological activity

### THERMAL_VENT_RUMBLE
- Brownian noise (integrate white noise): `brown[n] = brown[n-1] * 0.998 + white[n] * 0.002`
- Bandpass 15-60Hz. Amplitude tied to proximity to heat source (game world query)
- Spatialize via HRTF or stereo pan

---

## VOICE_MANAGEMENT_POOL

### FIXED_POOL_ALLOCATION
- Preallocate 64 `SynthVoice` structs in `NativeArray`. Zero GC
- Voice State: `{ isActive, phase, amplitude, freq, filterState[4], age }`
- Allocation: Linear scan for `isActive == false`. If none free, steal oldest voice (LRU)

### PRIORITY_SYSTEM
```
priority = baseImportance * (1 / (distanceToListener + 1)) * loudness

if pool full:
  victim = voice with min(priority) where priority < newVoice.priority
  if victim exists: kill victim, allocate newVoice
```

### FADE_OUT_ON_STEAL
- Apply 32-sample linear ramp to `amplitude` before deactivation
- Prevents click artifacts

---

## SPATIALIZATION_KERNEL

### DISTANCE_ATTENUATION_CURVE
```
// Inverse-square with near-field linearization
attenuation = min(1.0, refDistance / (refDistance + rolloff * distance))
```
- `refDistance = 1.0m`, `rolloff = 1.0` (underwater sound travels farther: use 0.5 rolloff)

### HRTF_PANNING_LIGHTWEIGHT
- Use azimuth-only HRTF lookup table (8° resolution, 45 entries)
- Interpolate between nearest 2 entries via `frac = (azimuth % 8.0) / 8.0`
- Elevation: Simple gain scaling `elevation > 0 ? 0.9 : 1.0` (cheap approximation)

### OCCLUSION_LOWPASS
```
raycast from listener to emitter
if hit:
  occlusionFactor = 1 - (material.density * 0.3)  // Metal=1.0, water=0.1
  lpfCutoff = lerp(400Hz, 8000Hz, occlusionFactor)
  output = biquadLP(spatializedSignal, lpfCutoff, Q=0.7)
```

---

## BURST_COMPILATION_CONSTRAINTS

### ALLOWED_OPERATIONS
- Math.sin/cos/sqrt/abs/floor/ceil (Burst intrinsics)
- NativeArray read/write
- Struct field access (no managed references)
- Static function calls (no virtuals)

### FORBIDDEN_IN_JOBS
- `UnityEngine.Random` (use `Unity.Mathematics.Random` with per-job seed)
- Managed arrays `float[]` (use `NativeArray<float>`)
- `Debug.Log` (use `[BurstDiscard]` wrapper or conditional compilation)

### SIMD_VECTORIZATION_HINTS
```
[MethodImpl(MethodImplOptions.AggressiveInlining)]
float4 ProcessQuad(float4 phase, float4 freq) {
  return sin(phase * TWO_PI);  // Burst auto-vectorizes to SIMD
}
```
- Process 4 voices per loop iteration when possible

---

## PERFORMANCE_BUDGETS

### MX350_AUDIO_THREAD_LIMITS
- Max Simultaneous FM Operators: 16 (4 voices × 4 operators each)
- Max Biquad Filters: 48 (assumes 3 per voice)
- Reverb: Single global instance. Update every 512 samples (10.7ms @ 48kHz)
- Total CPU: <15% of single core (leaves 85% for main thread)

### MEMORY_FOOTPRINT
- Ring Buffer: 65536 samples × 4 bytes = 256KB
- Voice Pool: 64 voices × 128 bytes = 8KB
- Wavetables: 2048 samples × 8 tables × 4 bytes = 64KB
- Reverb Delay Lines: 96000 samples × 4 bytes = 375KB
- **Total: ~700KB persistent**

### LATENCY_TARGET
- Buffer Size: 512 samples @ 48kHz = 10.7ms
- Producer runs 2 buffers ahead: Effective latency 32ms worst-case
- Acceptable for non-musical sound design (explosion→sound delay imperceptible)

---

## DEBUGGING_INSTRUMENTATION

### SIGNAL_CHAIN_TAPS
- Export intermediate buffers (`modulatorOut`, `carrierOut`, `filterOut`) to circular log (16384 samples)
- Write to disk on keypress (not every frame). Analyze in Audacity/MATLAB

### NAN_INFINITY_GUARDS
```
#if UNITY_EDITOR
  if(float.IsNaN(output) || float.IsInfinity(output)) {
    output = 0;
    SignalCorruption(voiceID, sampleIndex);
  }
#endif
```

### UNDERRUN_TELEMETRY
- Increment `static int underrunCount` when ring buffer empty
- Display in dev UI overlay. Target: 0 underruns over 60 seconds

---

## SHUTDOWN_PROTOCOL

### SAFE_THREAD_TERMINATION
```
1. Set `volatile bool shutdownRequested = true`
2. Signal producer thread via ManualResetEventSlim
3. producerThread.Join(timeout=1000ms)
4. Dispose NativeArrays only after Join() returns
5. If Join() times out: log error, force-abort (leak detected)
```

### DISPOSE_ORDER
```
ringBuffer.Dispose()
voicePool.Dispose()
wavetables.Dispose()
reverbBuffers.Dispose()
jobHandles.Complete()  // Ensure no jobs reading disposed memory
```

---

## QUALITY_TIER_SCALABILITY

### LOW (MX350 Target)
- Synthesis: 16 FM operators max
- Reverb: Schroeder (8 filters)
- Sample Rate: 24kHz (downsample all sources)
- Spatialization: Stereo pan only

### MEDIUM
- Synthesis: 32 operators
- Reverb: FDN (12 filters)
- Sample Rate: 48kHz
- Spatialization: Azimuth-only HRTF

### HIGH
- Synthesis: 64 operators
- Reverb: Hybrid convolution (early reflections) + FDN tail
- Sample Rate: 48kHz
- Spatialization: Full HRTF (azimuth + elevation)

### ULTRA
- Synthesis: 128 operators
- Reverb: 2-second convolution (ocean-acoustic IR)
- Sample Rate: 96kHz (professional DAW integration)
- Spatialization: Binaural Ambisonics (3rd order)

---

## INTEGRATION_HOOKS

### GAME_EVENT_TO_SYNTHESIS_FLOW
```
GameEvent: HullDamage(severity, impactPoint)
  ↓
AudioController.TriggerHullStress(severity)
  ↓
SynthVoicePool.Allocate(priority, voiceType=HullStress)
  ↓
Set voice.stressParam = severity
  ↓
Producer thread reads stressParam, synthesizes 512 samples
  ↓
Ring buffer enqueue
  ↓
OnAudioFilterRead dequeue, write to Unity audio output
```

### PARAMETER_SNAPSHOT_SYSTEM
- Game thread writes to `ParamSnapshot` struct (12 floats)
- Audio thread reads snapshot once per 512-sample block
- Double-buffer swap via `Interlocked.Exchange` pointer
- Zero locks in hot path

---

## FAILURE_MODE_MITIGATION

### DENORMAL_CPU_SPIKE
- Symptom: Audio thread intermittently takes 50ms instead of 5ms
- Root Cause: Biquad state variables decay to subnormal floats (10⁻³⁸)
- Fix: Add `1e-15f` to feedback state OR use `FloatMode.Fast` in Burst

### PHASE_DISCONTINUITY_CRACKLE
- Symptom: Audible pops when modulating frequency
- Root Cause: `time * freq` causes phase to reset on freq change
- Fix: Use phase accumulator with `phase += dPhase` pattern

### RING_BUFFER_RACE_CONDITION
- Symptom: Intermittent noise bursts or silence
- Root Cause: Write/read index updated without memory barrier
- Fix: `Interlocked.MemoryBarrier()` after index increment

### REVERB_EXPLOSION
- Symptom: Output suddenly maxes at ±1.0 and stays
- Root Cause: Comb filter feedback >1.0 or missing damping
- Fix: Clamp feedback to 0.85, add 1-pole LPF in loop

---

## MATH_REFERENCE_CONSTANTS

```
TWO_PI = 6.28318530718f
INV_TWO_PI = 0.15915494309f
SQRT2 = 1.41421356237f
LN2 = 0.69314718056f
SAMPLE_RATE = 48000.0f
INV_SAMPLE_RATE = 0.00002083333f  // 1/48000
```

---

## PROCEDURAL_NOISE_PRIMITIVES

### WHITE_NOISE (PCG Hash)
```
uint state = seed;
state = state * 747796405u + 2891336453u;
uint word = ((state >> ((state >> 28) + 4)) ^ state) * 277803737u;
word = (word >> 22) ^ word;
return (word / 4294967296.0f) * 2 - 1;  // Map to [-1,1]
```

### PINK_NOISE (Paul Kellett Filter)
```
b0 = 0.99886 * b0 + white * 0.0555179;
b1 = 0.99332 * b1 + white * 0.0750759;
b2 = 0.96900 * b2 + white * 0.1538520;
b3 = 0.86650 * b3 + white * 0.3104856;
b4 = 0.55000 * b4 + white * 0.5329522;
b5 = -0.7616 * b5 - white * 0.0168980;
pink = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362) * 0.11;
b6 = white * 0.115926;
```

### BROWNIAN_NOISE (Integrated Random Walk)
```
brown += (white * 0.02);
brown *= 0.998;  // Leak to prevent drift
```
```