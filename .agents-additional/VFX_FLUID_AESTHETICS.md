# VFX_FLUID_AESTHETICS.md
# HECTON-8 | Fluid VFX Technical Mandate v1.0
# Authority: Principal System Architect

---

## §0 — FOUNDATIONAL CONTRACTS

**IMMUTABLE INVARIANTS:**
- All particle state lives GPU-side. CPU reads = forbidden except diagnostics.
- All temporal state uses double-buffered UAV (ping-pong). Never read/write same buffer same dispatch.
- Particle budget is a hard ceiling, not a suggestion. Overflow = silent discard, never stall.
- Every dispatch group must be power-of-2. Non-aligned counts = pad with dead particles (Life ≤ 0).

**BUDGET ALLOCATION (VRAM = 2GB CEILING):**

| Pool | Max Count | Bytes/Particle | Total |
|---|---|---|---|
| Marine Snow | 32,768 | 48B | 1.5MB |
| Bubbles | 4,096 | 64B | 256KB |
| Debris | 1,024 | 96B | 96KB |
| FlowField Cache | — | — | 512KB |

**Rule:** Total VFX VRAM budget ≤ 8MB (includes CBs, indirect args, scratch buffers).

---

## §1 — PARTICLE STRUCT SPECIFICATION

```hlsl
// GPU CANONICAL LAYOUT — DO NOT REORDER (alignment-critical)
struct Particle {
    float3 Pos;       // World-space. 12B
    float  Life;      // [0..1] normalized. 0 = dead. 4B
    float3 Vel;       // m/s. 12B
    float  Size;      // World-space radius. 4B
    float3 PrevPos;   // Verlet history. 12B — DEBRIS ONLY, zero-fill bubbles
    uint   Flags;     // Bitfield below. 4B
    float2 UV;        // Atlas tile selector [0..1]. 8B
    float2 Pad;       // Align to 64B — fill with RNG seed for wobble phase
};
// Flags bitfield:
// bit0  → isBubble
// bit1  → inCurrent
// bit2  → isDebris
// bit3  → isSnow
// bit4  → collided (screen-space depth hit this frame)
// bit5  → useVerlet
// bit6  → clusterAnchor (LPPV probe assignment leader)
// bit7  → reserved
```

**Dispatch group stride:** 64 threads/group. AppendStructuredBuffer for emission; ConsumeStructuredBuffer for recycling dead slots.

---

## §2 — COMPUTE SHADER PIPELINE TOPOLOGY

```
[FRAME START]
    │
    ├─► [DISPATCH: Emission.compute]
    │       Emit new particles into AppendBuffer
    │       Read FlowFieldNative → cache local flow sample
    │
    ├─► [DISPATCH: Simulate.compute]       ← MAIN KERNEL
    │       Per-particle: integrate, drag, buoyancy, wobble, clamp
    │       Write PrevPos before integration (Verlet)
    │
    ├─► [DISPATCH: Cull.compute]
    │       Frustum cull + distance cull (>15m → clear draw flag)
    │       Near-camera cull (<0.4m → skip small debris only)
    │       Write IndirectArgs buffer for DrawMeshInstancedIndirect
    │
    ├─► [INDIRECT DRAW: GPU-driven render]
    │       Particle mesh = camera-facing quad (2 tris)
    │       No CPU draw calls
    │
    └─► [LPPV PROBE UPDATE]
            Per-cluster-anchor particle: sample LPPV, write SH to CB
            Frequency: every 4 frames (jittered per cluster)
```

**Synchronization:** Insert UAV barriers between Emission→Simulate→Cull. No implicit sync assumptions.

**Indirect Args Layout:**
```
uint VertexCountPerInstance = 6
uint InstanceCount          ← written by Cull.compute
uint StartVertexLocation    = 0
uint StartInstanceLocation  = 0
```

---

## §3 — FLOW FIELD INTEGRATION

**Source:** Cartographer's `FlowFieldNative` (NativeArray<float3>, world-aligned 3D grid).

**Sampling:**
```
GridCoord = floor((WorldPos - FieldOrigin) / CellSize)
Clamp GridCoord to [0, FieldDims-1]
FlowSample = trilinear_sample(FlowFieldNative, GridCoord)
// Trilinear: 8-tap manual lerp in compute — no Texture3D overhead
```

**Caching:** Each particle stores last-sampled FlowSample. Resample every N frames:
```
ResampleMask = (ParticleID + FrameCount) % 4 == 0
// Staggers resampling across 4 frames → 75% flow-sample cost reduction
```

**FlowField Upload:** CPU writes NativeArray → GPU StructuredBuffer<float3> via AsyncGPUReadback inverse (CPU→GPU only). Upload on dirty flag. Never per-frame if field is static.

---

## §4 — ANISOTROPIC DRAG

**Core Formula:**
```
FlowDir_N  = normalize(FlowSample)               // Unit flow direction
ParticleDir_N = normalize(Vel)                   // Unit velocity direction
Alignment  = dot(ParticleDir_N, FlowDir_N)       // [-1 .. 1]
AlignmentN = saturate((Alignment + 1.0) * 0.5)  // remap → [0 .. 1]

// DragMultiplier: 0.1 = fully aligned (with flow), 1.0 = fully opposed
DragMultiplier = lerp(1.0, 0.1, AlignmentN)

// Apply: exponential drag preserves physical plausibility
Vel *= pow(1.0 - DragMultiplier * BaseDragCoeff, DeltaTime * 60.0)
// BaseDragCoeff per type: Snow=0.15, Bubble=0.08, Debris=0.22
```

**Critical:** `pow(x, DeltaTime*60)` is frame-rate independent drag. Never multiply drag linearly.

**Turbulence injection on high drag:**
```
if (DragMultiplier > 0.7):
    TurbulenceScale = (DragMultiplier - 0.7) / 0.3   // [0..1]
    Vel += noise3D(Pos * 0.3 + Time) * TurbulenceScale * 0.15
// noise3D: value noise via 3D hash — no Perlin texture dependency
```

---

## §5 — BUOYANCY (BUBBLES)

**Physics Formula:**
```
// Archimedes
F_buoy = Volume * Gravity * (ρ_water - ρ_gas)
// Constants:
//   ρ_water = 1025.0 kg/m³ (deep salt water)
//   ρ_gas   = 1.2 kg/m³ (air)
//   Gravity = 9.81 m/s²
//   Volume  = (4/3)π * r³  where r = Particle.Size * 0.5

// Acceleration upward:
A_buoy = F_buoy / BubbleMass   // BubbleMass ≈ ρ_gas * Volume

// Integration:
Vel.y += A_buoy * DeltaTime
```

**Terminal Velocity Guard:**
```
MaxRiseSpeed = 0.35 + Particle.Size * 1.2   // Larger bubbles rise faster (Stokes)
Vel.y = min(Vel.y, MaxRiseSpeed)
```

**Depth Pressure Compression:**
```
// Bubbles shrink with depth (approximation of Boyle's Law)
SurfacePressure = 101325.0
DepthPressure   = SurfacePressure + (ρ_water * Gravity * abs(Pos.y))
Particle.Size  *= sqrt(SurfacePressure / DepthPressure)
// Clamp: [0.005 .. 0.12] meters
```

---

## §6 — BUBBLE WOBBLE (LATERAL OSCILLATION)

**Formula:**
```
// Phase: spatially varying → prevents lockstep visual
φ = Particle.Pad.x   // RNG seed baked at emission [0..2π]

WobbleFreq    = 2.1 + Particle.Size * 8.0   // Smaller bubbles wobble faster
WobbleMag     = 0.12 * Particle.Size

LateralOffset.x = sin(_SimulationTime * WobbleFreq + Pos.y * 1.7 + φ) * WobbleMag
LateralOffset.z = cos(_SimulationTime * WobbleFreq * 0.83 + Pos.y * 1.3 + φ + 1.1) * WobbleMag

// Apply as velocity perturbation, not direct position write
Vel.xz += LateralOffset * DeltaTime * 12.0
```

**Wobble suppression in strong current:**
```
CurrentStrength = length(FlowSample)
WobbleScale = 1.0 - saturate(CurrentStrength / 2.5)
// Full wobble at calm water, suppressed in 2.5+ m/s currents
```

---

## §7 — MARINE SNOW SIMULATION

**Behavioral Contract:**
- Base drift: downward + slow random horizontal wander
- Responds to FlowField anisotropic drag
- Screen-space depth collision (§9)
- Pooled: dead particles recycled via counter, no realloc

**Descent Base Velocity:**
```
V_terminal_snow = 0.02 + hash1(ParticleID) * 0.015   // [0.02 .. 0.035] m/s
Vel.y = -V_terminal_snow
```

**Horizontal Wander (replace Perlin with gradient noise hash):**
```
T    = _SimulationTime * 0.15 + ParticleID * 0.0001
Wander.x = hash_sin(float2(T, Pos.z * 0.4)) * 0.008
Wander.z = hash_sin(float2(T + 31.7, Pos.x * 0.4)) * 0.008
Vel.xz += Wander
```

**hash_sin kernel:**
```
hash_sin(v) = frac(sin(dot(v, float2(127.1, 311.7))) * 43758.5453)
```

**Flow Accumulation:** Marine snow accumulates on geometry (depth buffer collision). On collide (bit4 set):
```
Vel = float3(0,0,0)
Life -= DeltaTime * 0.5   // Faster decay when settled
```

---

## §8 — DEBRIS (VERLET INTEGRATION)

**Condition:** bit2=1 (isDebris), bit5=1 (useVerlet).

**Verlet Kernel:**
```
Acceleration = Gravity_vector + DragForce + FlowForce
// Gravity_vector = float3(0, -9.81, 0) * DebrisMass_inv
// DragForce = -Vel * DragMultiplier * BaseDragCoeff_debris
// FlowForce = (FlowSample - Vel) * FlowCoupling   // FlowCoupling = 0.3 for debris

NewPos    = Pos + (Pos - PrevPos) + Acceleration * DeltaTime²
PrevPos   = Pos
Pos       = NewPos
Vel       = (Pos - PrevPos) / DeltaTime   // Reconstruct for drag calc next frame
```

**Floor Contact:**
```
if Pos.y < FloorHeight + Particle.Size:
    Pos.y = FloorHeight + Particle.Size
    PrevPos.y = Pos.y + (Pos.y - PrevPos.y) * Restitution   // Restitution = 0.15
    Vel.xz *= 0.7   // Sliding friction
```

**Rotation:** Store angular velocity in Pad.xy. Rotate billboard UV offset per frame:
```
RotAngle += AngularVel * DeltaTime
UV_rotated = rotate2D(UV, RotAngle)
```

---

## §9 — SCREEN-SPACE DEPTH COLLISION

**Kernel (Cull/Simulate pass — runs after depth prepass):**
```
// Project particle to clip space
ClipPos   = mul(VP_matrix, float4(Pos, 1.0))
ScreenUV  = ClipPos.xy / ClipPos.w * 0.5 + 0.5

// Sample depth buffer (point sample — no bilinear on depth)
SceneDepth_raw  = _CameraDepthTexture.SampleLevel(PointClamp, ScreenUV, 0)
SceneDepth_lin  = LinearEyeDepth(SceneDepth_raw)
ParticleDepth   = ClipPos.w   // Eye-space depth

// Collision threshold: particle radius in view-space
CollisionMargin = Particle.Size * 0.5 / ParticleDepth

if (SceneDepth_lin < ParticleDepth + CollisionMargin):
    Flags |= (1u << 4)   // Set collided bit
    // Reflect velocity along surface normal (approx: Y-up)
    Vel.y = abs(Vel.y) * Restitution
```

**MX350 Fillrate Protection:**
- ScreenUV outside [0,1] → skip collision entirely
- Skip if ParticleDepth > 15.0 (already culled by distance)
- Skip debris with Size < 0.01 m (too small to matter)

---

## §10 — VELOCITY CLAMPING & TUNNELING PREVENTION

**Per-particle, post-integration:**
```
// Type-driven speed ceilings
MaxSpeed = select(Flags):
    isBubble  → 0.8 m/s
    isSnow    → 0.15 m/s
    isDebris  → 3.5 m/s

SpeedSq = dot(Vel, Vel)
if SpeedSq > MaxSpeed²:
    Vel *= MaxSpeed / sqrt(SpeedSq)   // Normalize + scale

// Sub-step guard: if displacement > voxel size (assume 0.25m min wall)
MaxDisplacement = 0.20   // 80% of min wall thickness
StepDist = length(Vel) * DeltaTime
if StepDist > MaxDisplacement:
    SubSteps = ceil(StepDist / MaxDisplacement)
    DeltaTime_sub = DeltaTime / SubSteps
    // Re-integrate in loop (max SubSteps = 4, else clamp vel)
    SubSteps = min(SubSteps, 4)
```

---

## §11 — CULLING PIPELINE

**Distance Cull (Cull.compute):**
```
Dist = length(Pos - CameraPos)
DrawMask = Dist <= 15.0

// Near-camera cull: small debris only (bit2=1, Size < 0.03)
if isDebris && Size < 0.03 && Dist < 0.6:
    DrawMask = false

// Frustum cull: dot(Pos - CameraPos, FrustumPlaneN[i]) + FrustumPlaneD[i] < -Size
for each of 6 frustum planes:
    if dot(Pos, PlaneN) + PlaneD < -Size:
        DrawMask = false; break
```

**LOD Logic (Hardware Tier Flag, set CPU-side per session):**
```
// Tier 0 = MX350 / Low
// Tier 1 = Mid (GTX 1060 class)
// Tier 2 = High

if Tier == 0:
    Snow: visual drift only, no depth collision, no flow resample
    Bubble: no Boyle compression, simplified wobble (freq halved)
    Debris: Verlet disabled → Euler only, no rotation

if Tier == 1:
    Snow: depth collision enabled, flow resample every 8 frames
    Bubble: Boyle enabled, full wobble
    Debris: Verlet enabled, no sub-step

if Tier == 2:
    All systems full fidelity
    Snow: accumulation persistence (Life drain on settle halved)
    Debris: sub-step enabled, 4-max
    Bubble: turbulence injection enabled (§4)
```

---

## §12 — LPPV INTEGRATION (LIGHTING)

**Cluster Assignment (Emission.compute):**
```
// First particle in each 64-thread group = clusterAnchor (bit6=1)
if LocalThreadID == 0: Flags |= (1u << 6)
ClusterID = GroupID   // Group index = cluster index
```

**LPPV Update (separate pass, every 4 frames, jittered):**
```
UpdateMask = (ClusterID + FrameCount) % 4 == 0

if UpdateMask && isClusterAnchor:
    SH_L0L1 = LPPV.SampleSH(Pos)   // Unity LPPV API call via CB injection
    CB_ClusterSH[ClusterID] = SH_L0L1
```

**Shader Evaluation:**
```
// In particle vertex/fragment shader
ClusterID = InstanceID / 64   // Match compute grouping
FinalColor *= EvaluateSH(CB_ClusterSH[ClusterID], SurfaceNormal)
// SurfaceNormal for billboards = camera forward (approximate diffuse)
```

**Fallback (Tier 0):** Skip per-cluster SH. Sample single global ambient probe. Write to CB once per frame.

---

## §13 — EMISSION LOGIC

**Emission.compute kernel:**
```
// Bubble sources: registered emitter positions (CB array, max 32 emitters)
EmitterIndex = ThreadID % NumActiveEmitters
EmitPos      = EmitterPositions[EmitterIndex]

// Stochastic emission: only emit if hash(ThreadID, FrameCount) < EmitRate
EmitProb = EmitRates[EmitterIndex]   // [0..1] per frame per thread
if hash2(uint2(ThreadID, FrameCount)) < EmitProb:
    Particle p
    p.Pos  = EmitPos + random_in_sphere(0.05)
    p.Vel  = float3(0, 0.05, 0) + RandomOffset * 0.02
    p.Life = 1.0
    p.Size = lerp(0.005, 0.03, hash1(ThreadID))
    p.Flags = (1u << 0)   // isBubble
    p.Pad  = hash2(uint2(ThreadID, 7919)) * TWO_PI   // Phase seeds
    AppendBuffer.Append(p)

// Snow: continuous injection at ceiling plane
// Debris: event-driven (physics event writes to EmitRequest CB)
```

**Dead Slot Recycling:**
```
// Simulate.compute: if Life <= 0 → ConsumeBuffer.Append(ParticleID)
// Emission.compute: prefer recycled slots before allocating new
// Counter buffer: InterlockedAdd on dead-slot counter
```

---

## §14 — RENDERING CONTRACTS

**Particle Shader Inputs:**
```
StructuredBuffer<Particle>  _ParticleBuffer
StructuredBuffer<float4x4>  _ClusterSH     // SH coefficients per cluster
Buffer<uint>                _IndirectArgs
```

**Billboard Construction (vertex shader):**
```
WorldPos   = _ParticleBuffer[InstanceID].Pos
Size       = _ParticleBuffer[InstanceID].Size
// Camera-facing: right = CameraRight, up = CameraUp
QuadOffset = (UV.x - 0.5) * CameraRight + (UV.y - 0.5) * CameraUp
FinalPos   = WorldPos + QuadOffset * Size
```

**Soft Particle Depth Fade (fragment):**
```
SceneDepth = LinearEyeDepth(_CameraDepthTexture.SampleLevel(...))
ParticleDepth = i.positionCS.w
Fade = saturate((SceneDepth - ParticleDepth) / SoftFadeDistance)
// SoftFadeDistance: Bubble=0.1, Snow=0.05, Debris=0.15
Alpha *= Fade
```

**Blend Mode:** Premultiplied alpha. Bubbles: additive rim + alpha center. Snow: alpha blend only.

**Atlas Layout:** 4×4 sprite atlas (16 tiles). Tile select via UV field. Snow: 4 unique flake shapes. Bubble: 2 specular variations. Debris: 10 organic shapes.

---

## §15 — MEMORY & BUFFER MANAGEMENT

**Buffer Topology:**
```
ParticleBuffer_A   (StructuredBuffer, full pool)   ← simulation reads
ParticleBuffer_B   (RWStructuredBuffer, ping-pong) ← simulation writes
// Swap references each frame via CB constant (no copy)

DeadListBuffer     (AppendConsumeBuffer, uint)     ← recycled slot indices
EmitRequestBuffer  (StructuredBuffer<EmitRequest>) ← debris event queue, max 64/frame
IndirectArgsBuffer (Buffer<uint4>)                 ← written by Cull, read by Draw
ClusterSHBuffer    (StructuredBuffer<float4x4>)    ← 512 clusters × SH L1 = 32KB
```

**Initialization:** Fill DeadList with [0..MaxParticles-1] at startup. Emission consumes indices; death appends.

**Frame Lifecycle Guarantee:**
```
Frame N:   Simulate reads A, writes B
Frame N+1: Simulate reads B, writes A
// SwapFlag in CB: single uint toggle
```

---

## §16 — DIAGNOSTICS & SAFETY RAILS

**Overflow Guard:**
```
// Emission.compute: before Append, check counter
uint AliveCount;
DeadListBuffer.GetDimensions(AliveCount)  // Use counter
if AliveCount == 0: discard emit   // Pool exhausted, silent skip
```

**NaN Propagation Guard:**
```
// Post-integration, pre-write:
if any(isnan(Vel)) || any(isinf(Vel)):
    Vel = float3(0, -0.01, 0)   // Safe default
if any(isnan(Pos)):
    Life = 0.0   // Kill particle
```

**Velocity Audit (Debug Tier only):**
```
// If DIAGNOSTICS_ENABLED defined:
RWBuffer<float> _MaxVelocityDebug
InterlockedMax(_MaxVelocityDebug[0], asuint(length(Vel)))
// CPU reads async, logs if > threshold
```

**Performance Circuit Breaker:**
```
// If GPU frame time > 16ms (detected via timestamp queries):
// CB flag: EmergencyLOD = 1
// Simulate.compute: if EmergencyLOD → skip turbulence, skip wobble, halve snow count
// Reset after 30 stable frames
```

---

## §17 — CONSTANTS REGISTRY

```
// Immutable physical constants (CB slot 0, global)
WATER_DENSITY          = 1025.0f
AIR_DENSITY            = 1.2f
GRAVITY                = 9.81f
PI                     = 3.14159265f
TWO_PI                 = 6.28318530f
PARTICLE_POOL_SIZE     = 32768u + 4096u + 1024u   // Snow + Bubble + Debris

// Per-tier tuneables (CB slot 1, set at session start)
BASE_DRAG_BUBBLE       = 0.08f
BASE_DRAG_SNOW         = 0.15f
BASE_DRAG_DEBRIS       = 0.22f
FLOW_COUPLING_DEBRIS   = 0.30f
WOBBLE_SUPPRESSION_VEL = 2.50f   // m/s
SOFT_FADE_BUBBLE       = 0.10f
SOFT_FADE_SNOW         = 0.05f
SOFT_FADE_DEBRIS       = 0.15f
MAX_CULL_DISTANCE      = 15.0f
BOYLE_SURFACE_P        = 101325.0f
RESTITUTION_DEBRIS     = 0.15f
RESTITUTION_BUBBLE     = 0.05f
SNOW_TERMINAL_MIN      = 0.020f
SNOW_TERMINAL_MAX      = 0.035f
```

---

## §18 — INTEGRATION CHECKLIST (PRE-SHIP GATE)

```
[ ] All particle updates zero CPU involvement per frame
[ ] FlowFieldNative upload path: dirty-flag gated
[ ] Double-buffer swap verified: no same-frame read-write alias
[ ] DeadList initialized full at startup
[ ] LPPV update jitter confirmed: 4-frame stagger, no same-frame cluster double-update
[ ] NaN guards active in simulate kernel
[ ] Velocity clamp verified: no particle exceeds type max
[ ] Soft particle fade active on all types
[ ] IndirectArgs reset to 0 before Cull.compute each frame
[ ] Tier flags propagate to compute CB before first dispatch
[ ] Emergency LOD circuit breaker timestamp query active
[ ] Boyle size clamp enforced: [0.005 .. 0.12]m
[ ] Screen-space depth collision skipped: Tier 0 snow path
[ ] Atlas UV tile assignment set at emission, never mutated
[ ] Debris rotation angular velocity clamped: ±720 deg/s max
```
```