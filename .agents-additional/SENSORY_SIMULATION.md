# SENSORY_SIMULATION.md — HECTON-8 ENGINE MANDATE
# Domain: Sonar · Occlusion · Acoustic Radar · Psychoacoustic Rendering
# Rev: 1.0 | Target: i3-10G / MX350-2GB / 6-8GB RAM

---

## MODULE 0 — CONSTANTS & GLOBAL TRUTH

```
C_SOUND        = 1480.0f          // m/s seawater
C_AIR          = 343.0f           // pocket/bubble zones only
SONAR_BASE_F   = 22000.0f         // Hz — active ping carrier
SONAR_DECAY_K  = 0.0035f          // absorption coeff seawater @ 22kHz
EPSILON_DIST   = 0.001f           // NaN guard minimum
MAX_PROBE_DIST = 200.0f           // hard raycast ceiling
CULL_RADIUS    = 30.0f            // MX350 skip threshold (m)
SLOW_TICK_HZ   = 10               // SlowTick rate (updates/sec)
LPF_OPEN_HZ    = 22000.0f         // no occlusion
LPF_1HIT_HZ    = 4000.0f
LPF_2HIT_HZ    = 800.0f
LPF_NHIT_HZ    = max(80.0f, LPF_OPEN_HZ / (2^n_hits))
```

---

## MODULE 1 — EXECUTION SCHEDULER

### 1.1 SlowTick Time-Slicer
```
STATE:
  axis_index     : int ∈ [0..5]   // rotates each SlowTick
  hit_buffer     : float[6]        // pre-allocated, PERSISTENT
  semantic_hits  : LayerHit[6]     // pre-allocated SpatialQueryHit[]

ALGORITHM — OnSlowTick():
  axis      = axis_index % 6
  dir       = AXIS_TABLE[axis]     // see §1.2
  hit       = NonAlloc_Raycast(origin, dir, MAX_PROBE_DIST, SENSORY_MASK)
  dist      = hit.valid ? hit.distance : MAX_PROBE_DIST
  dist      = max(dist, EPSILON_DIST)            // NaN guard
  hit_buffer[axis]    = dist
  semantic_hits[axis] = hit
  axis_index++
  // Full 6-axis scan completes in 6 SlowTick cycles = 0.6s @ 10Hz
```

### 1.2 Axis Table
```
AXIS_TABLE[6] = {
  [0] UP      =  float3(0, 1, 0),
  [1] DOWN    =  float3(0,-1, 0),
  [2] LEFT    =  float3(-1,0, 0),
  [3] RIGHT   =  float3( 1,0, 0),
  [4] FORWARD =  float3(0, 0, 1),
  [5] BACK    =  float3(0, 0,-1)
}
```

### 1.3 Source Distance Cull Gate
```
BEFORE any sensory computation for source S:
  delta = length(S.position - player.position)
  if delta > CULL_RADIUS: SKIP_ALL → return
// MX350 hard gate — no exceptions
```

---

## MODULE 2 — ENCLOSURE VOLUME ENGINE

### 2.1 Volume Approximation
```
dU = hit_buffer[0],  dD = hit_buffer[1]
dL = hit_buffer[2],  dR = hit_buffer[3]
dF = hit_buffer[4],  dB = hit_buffer[5]

V_approx = (dU + dD) * (dL + dR) * (dF + dB)
// Units: m³ · approx. Valid range: [EPSILON³ .. MAX_PROBE_DIST³]
```

### 2.2 Reverb Decay Scaling
```
// Map V_approx → RT60 (reverberation time in seconds)
RT60 = REVERB_MIN + (REVERB_MAX - REVERB_MIN) *
       saturate(log10(V_approx + 1.0f) / log10(MAX_V + 1.0f))

// Recommended values:
REVERB_MIN = 0.15f   // tight crevice
REVERB_MAX = 6.0f    // abyssal open water
MAX_V      = (MAX_PROBE_DIST * 2)^3

// Drive Unity Audio Mixer "ReverbDecay" param directly:
mixer.SetFloat("RT60", RT60)
```

### 2.3 Enclosure Density Index (EDI)
```
// Normalized scalar [0..1]: tight=1, open=0
EDI = 1.0f - saturate(V_approx / (CULL_RADIUS^3 * 8.0f))
// Feed EDI to: reverb wet mix, tension music layer, creature AI hearing radius
```

---

## MODULE 3 — ACOUSTIC ABSORPTION LAYER MASK SEMANTIC MAP

### 3.1 Static Layer → Absorption Factor Table
```
ABSORPTION_TABLE : Dictionary<LayerID, float> = {
  LAYER_ROCK_SOLID    → 0.98f,
  LAYER_METAL_HULL    → 0.85f,
  LAYER_CORAL_ORGANIC → 0.70f,
  LAYER_SEDIMENT      → 0.60f,
  LAYER_PLANT_KELP    → 0.30f,
  LAYER_WATER_VOLUME  → 0.05f,   // volumetric water body
  LAYER_BUBBLE_POCKET → 0.50f,   // partial diffusion
  LAYER_GLASS_PORT    → 0.10f    // near-transparent transmission
}
// ALL layers not listed → default absorption = 0.50f
// Triggers / transparents: EXCLUDED from SENSORY_MASK bitmask (compile-time)
```

### 3.2 Composite Path Absorption
```
// For N sequential hits along occlusion ray:
A_total = 1.0f
for each hit_i in occlusion_chain:
    A_total *= ABSORPTION_TABLE[hit_i.layerID]
// A_total ∈ [0..1]: 1=fully open, 0=total block
// Apply to source amplitude: amplitude_final = amplitude_source * A_total
```

---

## MODULE 4 — OCCLUSION PIPELINE

### 4.1 Multi-Hit Occlusion Ray
```
ALGORITHM — ComputeOcclusion(source_pos, listener_pos):
  ray_dir   = normalize(listener_pos - source_pos)
  ray_dist  = length(listener_pos - source_pos)
  // Non-alloc multi-hit into pre-allocated OcclusionHit[8] buffer
  n_hits    = Physics.RaycastNonAlloc(source_pos, ray_dir,
                                       occlusionHitBuffer, ray_dist,
                                       SENSORY_MASK)
  // Enforce:
  [FORBID] Physics.Raycast() single-hit — misses layered geometry
  [FORBID] OverlapSphere — use SpatialHash contact list §6.1
```

### 4.2 LPF Cutoff Derivation
```
// Per-source, per-frame (cached until source moves > 0.5m):
f_cutoff = max(80.0f, LPF_OPEN_HZ / pow(2.0f, n_hits))
// Apply f_cutoff → per-source DSP LowPassFilter component
// Clamp: f_cutoff = clamp(f_cutoff, 80.0f, 22000.0f)
```

### 4.3 Occlusion Smooth Interpolation
```
// Avoid LPF click artifacts on geometry transitions:
f_cutoff_current = lerp(f_cutoff_prev, f_cutoff_target,
                        Time.deltaTime * LPF_SLEW_RATE)
LPF_SLEW_RATE = 3.0f   // Hz tracking speed
```

---

## MODULE 5 — ACTIVE SONAR ENGINE

### 5.1 Ping Emission
```
// Player triggers active ping:
ping_origin    = player.hydrophone_position
ping_timestamp = Time.time
ping_id++      // uint, wraps — used for echo matching

// Emit 6-axis + N_CONE directional rays simultaneously (not time-sliced):
// Active ping is player-initiated, justify full synchronous cost (≤1/interaction)
N_CONE = 12   // hemispherical forward cone, angular step = 15°
```

### 5.2 Echo Travel Time
```
// For each reflecting surface at distance d_hit:
t_echo = (2.0f * d_hit) / C_SOUND
// Schedule echo audio event: AudioScheduler.QueueAt(ping_timestamp + t_echo)
```

### 5.3 Doppler Shift — Moving Player, Stationary Obstacle
```
f_echo = SONAR_BASE_F * ((C_SOUND + v_player_radial) /
                          (C_SOUND - v_player_radial))
// v_player_radial = dot(player.velocity, normalize(hit.point - player.pos))
// Clamp denominator: if (C_SOUND - v_player_radial) < EPSILON → clamp to EPSILON
// Pitch output: pitch_shift = f_echo / SONAR_BASE_F
```

### 5.4 Doppler Shift — Moving Target
```
// Source moving at velocity v_src, listener moving at v_listener:
f_perceived = SONAR_BASE_F * ((C_SOUND + v_listener_radial) /
                               (C_SOUND + v_src_radial))
// v_src_radial      = dot(src.velocity,      normalize(listener.pos - src.pos))
// v_listener_radial = dot(player.velocity,   normalize(src.pos - listener.pos))
// Sign convention: approaching = positive radial, receding = negative
```

### 5.5 Sonar Amplitude Decay (Spherical + Absorption)
```
// Transmission loss (TL) model — sonar equation:
TL = 20.0f * log10(d_hit) + SONAR_DECAY_K * d_hit
// Source Level SL assumed normalized to 1.0
amplitude_echo = pow(10.0f, -TL / 20.0f)
amplitude_echo = max(amplitude_echo, 0.0f)   // clamp negative log artifacts
```

### 5.6 Echo Semantic Classification
```
// Per echo hit, classify by layer:
echo_type = ECHO_CLASS_TABLE[hit.layerID]
ECHO_CLASS_TABLE = {
  LAYER_ROCK_SOLID    → HARD_SPECULAR,
  LAYER_METAL_HULL    → METALLIC_RING,
  LAYER_CORAL_ORGANIC → DIFFUSE_SOFT,
  LAYER_CREATURE_BODY → BIOLOGICAL,
  LAYER_SEDIMENT      → DEAD_MUFFLED
}
// Map echo_type → audio clip variant + material impulse response selection
```

---

## MODULE 6 — SPATIAL HASH CONTACT SYSTEM (ZERO-GC)

### 6.1 SpatialHash Architecture
```
CELL_SIZE = 5.0f   // meters — tuned to CULL_RADIUS / 6
hash(pos) = (floor(pos.x/CELL_SIZE) * P1) XOR
            (floor(pos.y/CELL_SIZE) * P2) XOR
            (floor(pos.z/CELL_SIZE) * P3)
P1 = 73856093, P2 = 19349663, P3 = 83492791   // large primes

// ContactList: NativeArray<ContactEntry> — persistent, pre-allocated capacity 512
// ContactEntry: { entityID:uint, layerID:int, position:float3 }
// Update: OnPhysicsContact callbacks write into hash — no OverlapSphere polling
```

### 6.2 Neighbor Query
```
// Query cells in 3x3x3 neighborhood (27 cells):
results = SpatialHash.QueryRadius(player.pos, QUERY_RADIUS=8.0f)
// Returns NativeSlice — zero allocation
// Filter by layer inside query loop, not before
```

---

## MODULE 7 — CARRIER / PARENT BLINDNESS SYSTEM

### 7.1 LayerMask Exclusion Protocol
```
SENSORY_MASK = LayerMask.All
            & ~(1 << LAYER_PLAYER_BODY)
            & ~(1 << LAYER_PLAYER_HULL)
            & ~(1 << LAYER_CARRIER_INTERIOR)
            & ~(1 << LAYER_TRIGGER_VOLUME)
            & ~(1 << LAYER_TRANSPARENT_FX)
// [FORBID] IsChildOf(), transform.parent traversal in hot path
// [FORBID] tag string comparison in any sensory loop
// LayerMask computed ONCE at init — stored as const int bitmask
```

---

## MODULE 8 — PSYCHOACOUSTIC PRESSURE SIMULATION

### 8.1 Depth Pressure Factor
```
// Modulates tinnitus / equalization sensation:
P_depth = 1.0f + (player.depth / MAX_DEPTH) * PRESSURE_SCALE
PRESSURE_SCALE = 3.0f
MAX_DEPTH      = 1500.0f  // meters

// Apply P_depth to:
//   - High-frequency hearing rolloff: HF_cutoff = LPF_OPEN_HZ / P_depth
//   - Tinnitus layer volume: vol_tinnitus = saturate((P_depth - 1.5f) / 2.0f)
//   - Suit creak event probability: P_creak = saturate(P_depth / 4.0f)
```

### 8.2 Bubble Resonance Frequency
```
// Minnaert frequency for spherical bubble radius R (meters):
f_bubble = (1.0f / (2.0f * PI * R)) * sqrt(3.0f * GAMMA * P_ambient / RHO_WATER)
GAMMA     = 1.4f         // adiabatic index air
RHO_WATER = 1025.0f      // kg/m³ seawater
P_ambient = 101325.0f + (RHO_WATER * 9.81f * player.depth)   // Pa

// Use f_bubble to tune procedural bubble audio oscillator per-bubble-cluster
// Cluster R estimated from particle system emission radius
```

### 8.3 Haas Effect Zone Masking
```
// If two sound sources arrive within 35ms of each other:
// Suppress secondary source perceived localization:
if abs(t_arrival_A - t_arrival_B) < 0.035f:
    source_B.spatialBlend = lerp(source_B.spatialBlend, 0.0f, 0.8f)
// Restore when delta > 40ms
```

---

## MODULE 9 — ACOUSTIC RADAR (PASSIVE HYDROPHONE)

### 9.1 Directional Energy Accumulation
```
// Per SlowTick: scan N_RADAR_SOURCES nearest active sound emitters:
N_RADAR_SOURCES = 8   // budget cap

for each source_i in nearest_8:
    dir_i      = normalize(source_i.pos - player.pos)
    energy_i   = source_i.amplitude * A_total_i * (1.0f / dist_i²)
    azimuth_i  = atan2(dir_i.x, dir_i.z)   // horizontal bearing
    elevation_i= asin(dir_i.y)              // vertical bearing
    // Accumulate into RadarGrid (8-sector azimuth × 4-sector elevation)
    sector = sector_encode(azimuth_i, elevation_i)
    radar_grid[sector] += energy_i
```

### 9.2 Radar Grid Decay
```
// Each SlowTick, decay all cells to prevent ghost trails:
for each cell in radar_grid:
    cell *= RADAR_DECAY_FACTOR
RADAR_DECAY_FACTOR = 0.75f   // per SlowTick (10Hz → 0.1s half-life ≈ 0.26s)
```

### 9.3 Radar UI Mapping
```
// Map radar_grid[sector] → UI pip brightness:
brightness = saturate(sqrt(radar_grid[sector]) / RADAR_MAX_ENERGY)
// sqrt perceptual linearization — raw energy² feels too extreme
RADAR_MAX_ENERGY = max(radar_grid) over last 30 ticks  // auto-gain
```

---

## MODULE 10 — CREATURE HEARING SIMULATION (SONAR COUNTER-DETECTION)

### 10.1 Active Ping Detection Radius
```
// Creature hears player's active ping if within detection sphere:
r_detect = sqrt(SL_ping / (CREATURE_HEARING_THRESHOLD * TL_factor))
// SL_ping = ping source level (normalized amplitude²)
// TL_factor = transmission loss at creature distance
// Simpler budget form:
r_detect = PING_BASE_RADIUS * amplitude_ping * (1.0f / (1.0f + SONAR_DECAY_K * dist))
PING_BASE_RADIUS = 80.0f   // meters at full amplitude
```

### 10.2 Creature Acoustic Shadow Check
```
// Before alerting creature: verify line-of-acoustic-sight:
// Single raycast player → creature, SENSORY_MASK
// If A_total (§3.2) < 0.15f → creature in deep shadow → no detection
// Threshold 0.15 = empirically tuned to felt gameplay stealth
```

---

## MODULE 11 — SCALABILITY GATES

### 11.1 Quality Tier Binding
```
TIER_LOW  (MX350):
  N_CONE             = 6
  N_RADAR_SOURCES    = 4
  OcclusionHitBuffer = [4]
  SLOW_TICK_HZ       = 8
  REVERB: Unity built-in reverb zones only, no convolution

TIER_MED  (GTX1060/RX580):
  N_CONE             = 12
  N_RADAR_SOURCES    = 8
  OcclusionHitBuffer = [8]
  SLOW_TICK_HZ       = 10
  REVERB: SteamAudio zone reverb

TIER_HIGH (RTX2070+):
  N_CONE             = 24
  N_RADAR_SOURCES    = 16
  OcclusionHitBuffer = [16]
  SLOW_TICK_HZ       = 20
  REVERB: SteamAudio full convolution IR + occlusion
```

### 11.2 Runtime Tier Detection
```
// At init: sample GPU VRAM via SystemInfo.graphicsMemorySize
// ≤ 2048MB → TIER_LOW
// ≤ 6144MB → TIER_MED
// >  6144MB → TIER_HIGH
// Override via Settings.json "acoustic_quality_override" : [0|1|2]
```

---

## MODULE 12 — NaN / STABILITY GUARDS

### 12.1 Pre-Computation Epsilon Gates
```
// Before ANY formula consuming distances:
dU = max(hit_buffer[0], EPSILON_DIST)
dD = max(hit_buffer[1], EPSILON_DIST)
// ... all 6 axes
// Before Doppler denominator:
denom = C_SOUND - v_radial
if abs(denom) < EPSILON_DIST: denom = sign(denom) * EPSILON_DIST
// Before log10 in TL:
d_safe = max(d_hit, EPSILON_DIST)
// Before sqrt in bubble frequency:
arg_safe = max(3.0f * GAMMA * P_ambient / RHO_WATER, 0.0f)
```

### 12.2 Output Clamp Policy
```
// ALL audio parameter outputs clamped before mixer write:
amplitude : clamp(x, 0.0f, 1.0f)
pitch     : clamp(x, 0.05f, 4.0f)
f_cutoff  : clamp(x, 80.0f, 22000.0f)
RT60      : clamp(x, 0.05f, 12.0f)
EDI       : clamp(x, 0.0f, 1.0f)
```

---

## MODULE 13 — MEMORY BUDGET

```
// All sensory buffers: pre-allocated at scene load, NEVER reallocated:

hit_buffer          : float[6]              =  24 bytes
semantic_hits       : LayerHit[6]           = ~96 bytes  (estimate per struct)
occlusionHitBuffer  : RaycastHit[16]        = ~880 bytes (Unity RaycastHit ~55B)
radar_grid          : float[32]             = 128 bytes  (8az × 4el)
spatial_hash        : NativeArray[512]      = ~8 KB
sonar_echo_queue    : FixedQueue<EchoEvent>[64] = ~2 KB

TOTAL SENSORY BUDGET: < 16 KB working set
// Fits L1/L2 cache on i3-10G — zero thrashing
```

---

## MODULE 14 — INTEGRATION CHECKLIST

```
[ ] SENSORY_MASK bitmask: verified excludes player/trigger layers at compile time
[ ] SlowTick scheduler: confirm single raycast per OnSlowTick invocation
[ ] SpatialHash: NativeArray allocated in OnEnable, disposed OnDisable
[ ] Doppler: denominator epsilon guard tested at v_radial = C_SOUND edge case
[ ] RT60 output: drives mixer param key string constant (no runtime string alloc)
[ ] Tier detection: runs before first audio frame, cached static field
[ ] Bubble freq: P_ambient updates each 1m depth increment only (delta-check)
[ ] Echo queue: EchoEvent pooled, no new() in ping hot path
[ ] Radar decay: runs on SlowTick, NOT Update (confirmed)
[ ] All log10/sqrt inputs: epsilon-guarded (verified by unit test suite)
```
```