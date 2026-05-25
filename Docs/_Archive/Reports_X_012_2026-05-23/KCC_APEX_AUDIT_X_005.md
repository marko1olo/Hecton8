# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# KCC APEX Audit X_005

## Scoped PhysX Result
- X_005 scoped forbidden call count: 0
- Whole non-Editor runtime forbidden call count: 107
- Whole-runtime residuals outside X_005 are listed in JSON; they are not claimed clean by this agent.

## Solver Bound
- ResolveIterationCount max: 8
- Hard local stride clamps 1..8 found: 3
- Max contact samples per entity: 24 (8 sweep samples * 3 capsule probes)
- Max resolution plane projections per entity: 8
- 100 m/s at dt 0.016666667 moves 1.666667 m/frame.
- No recursion is used by the KCC collision build or resolution jobs; bounded for-loops terminate after fixed counters.
- Three-plane corner proof: At most 8 plane projections are executed. A 3-plane corner consumes no recursion and no stack growth: each projection computes v' = v - n * min(dot(v,n),0), then the next fixed-index contact is evaluated. The loop index is monotonic and capped, so degenerate coplanar/orthogonal contacts terminate after <=8 projections even when velocity becomes zero.
- 100 m/s cone proof: At 60 Hz, 100 m/s is 1.6666667 m per frame. The speculative SDF stage emits up to 8 hit slots per entity from 3 capsule probe lines, so the resolver receives a finite plane set before integration. Cone/corner degeneracy can lose exact collider fidelity if the SDF cell is coarser than the cone tip radius; the failure mode is bounded conservative stop/slide, not an unbounded loop.

## LockstepPlayerKinematicState Layout
- Size: 96 bytes
- Covered bytes: 96
- Gaps: []

- 00..08: long SectorX
- 08..16: long SectorY
- 16..24: long SectorZ
- 24..36: float3 LocalPosition
- 36..48: float3 Velocity
- 48..60: float3 Forward
- 60..64: uint Frame
- 64..68: uint Flags
- 68..72: uint InputActions
- 72..76: uint StableId
- 76..80: uint HashCadenceFrames
- 80..84: uint Reserved1
- 84..88: uint Reserved2
- 88..92: uint Reserved3
- 92..96: uint Reserved4

## KinematicStateDTO Layout
- Size: 64 bytes
- Covered bytes: 64
- Gaps: []

- 00..24: double3 AUP_Position
- 24..36: float3 Velocity
- 36..48: float3 AngularVelocity
- 48..52: float Mass
- 52..56: uint Flags
- 56..60: float DragCoefficient
- 60..61: byte RestingFrameCount
- 61..62: byte DeepSleepTickCount
- 62..63: byte SleepMaterialIndex
- 63..64: byte _pad0
