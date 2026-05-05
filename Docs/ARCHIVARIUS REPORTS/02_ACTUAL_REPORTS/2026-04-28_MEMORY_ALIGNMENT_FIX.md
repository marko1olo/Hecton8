# MEMORY_ALIGNMENT_FIX.md — DOD Struct Layout Surgery
Date: 2026-04-28
Status: REFERENCE


## Current-State Addendum (2026-04-29)

This file is a dated surgery proposal, not current verified source truth.

Important boundary:

- multiple layouts in this document are explicitly estimated or inferred
- no fresh same-pass struct reread validated every proposed padding/reorder step against current owner code
- do not apply these surgeries blindly; re-read each live struct first, then verify size/alignment in code

Use this file as a candidate queue for future work, not as direct implementation authority.
**Status:** PENDING SURGERY  
**Scan Date:** 2026-04-28  
**Scope:** All `[StructLayout]` and `NativeArray<T>` structs in `Assets/_Project/Scripts`

---

## Executive Summary

6 structs flagged for misalignment. 3 require padding surgery. 3 are Burst-compatible but suboptimal.

---

## Struct 1: HectonVegetationInstanceData
**File:** `Assets/_Project/Scripts/World/InstancedFloraRenderer.cs` (inferred)  
**Current Size:** ~104 bytes (estimated)  
**Alignment:** Default (~4-byte)  
**Target:** 16-byte aligned for GPU instancing

### Current Layout (estimated)
```csharp
public struct HectonVegetationInstanceData
{
    public Vector3 Position;      // 12 bytes
    public Quaternion Rotation;   // 16 bytes
    public Vector3 Scale;         // 12 bytes
    public float BendAmount;      // 4 bytes
    public float SwayPhase;       // 4 bytes
    public int TypeIndex;         // 4 bytes
    public int VariantIndex;      // 4 bytes
    public float Age;             // 4 bytes
    public float Health;          // 4 bytes
    public float LightExposure;   // 4 bytes
    public uint PackedColor;      // 4 bytes
    // Total: ~104 bytes, alignment 4
}
```

### Surgery: Padded Layout
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct HectonVegetationInstanceData
{
    public Vector3 Position;      // 12 bytes
    private float _padding0;       // 4 bytes → 16-byte boundary
    public Quaternion Rotation;   // 16 bytes
    public Vector3 Scale;         // 12 bytes
    private float _padding1;       // 4 bytes → 16-byte boundary
    public float BendAmount;      // 4 bytes
    public float SwayPhase;       // 4 bytes
    public float Age;             // 4 bytes
    public float Health;          // 4 bytes
    // 32 bytes so far
    public float LightExposure;   // 4 bytes
    public int TypeIndex;         // 4 bytes
    public int VariantIndex;      // 4 bytes
    public uint PackedColor;      // 4 bytes
    // Total: 48 bytes, 16-byte aligned
}
```

---

## Struct 2: ForcePacket
**File:** `Assets/_Project/Scripts/Physics/PhysicsApplySystem.cs` (inferred)  
**Current Size:** ~36 bytes  
**Alignment:** Default

### Current Layout (estimated)
```csharp
public struct ForcePacket
{
    public Vector3 Force;         // 12 bytes
    public Vector3 Position;      // 12 bytes
    public float Torque;          // 4 bytes
    public uint BodyId;           // 4 bytes
    public byte ForceMode;        // 1 byte
    // Total: ~36 bytes, alignment 4
}
```

### Surgery: Padded Layout
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct ForcePacket
{
    public Vector3 Force;         // 12 bytes
    public Vector3 Position;      // 12 bytes
    public float Torque;          // 4 bytes
    public uint BodyId;           // 4 bytes
    public byte ForceMode;        // 1 byte
    private byte _padding0;        // 1 byte
    private byte _padding1;        // 1 byte
    private byte _padding2;        // 1 byte
    // Total: 40 bytes, 8-byte aligned (acceptable for physics queue)
}
```

---

## Struct 3: BoidData
**File:** `Assets/_Project/Scripts/Fauna/BoidData.cs` (inferred)  
**Current Size:** 32 bytes  
**Alignment:** 4-byte  
**Status:** ✅ Burst-compatible, no surgery needed

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct BoidData
{
    public float3 Position;       // 12 bytes
    public float3 Velocity;       // 12 bytes
    public int FlockId;           // 4 bytes
    public float Padding;         // 4 bytes
    // Total: 32 bytes, 4-byte aligned
}
```

---

## Struct 4: CognitionCore
**File:** `Assets/_Project/Scripts/AI/CognitionCore.cs` (inferred)  
**Current Size:** 64 bytes  
**Alignment:** 64-byte (cache-aligned)  
**Status:** ✅ COMPLIANT — cache line aligned

---

## Struct 5: AbsoluteUniversePositionBlit128
**File:** `Assets/_Project/Scripts/Core/AbsoluteUniversePosition.cs` (inferred)  
**Current Size:** 48 bytes  
**Alignment:** 16-byte  
**Status:** ✅ GPU-friendly, no surgery needed

---

## Struct 6: QueryKey (AcousticOcclusionUtility)
**File:** `Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs` line ~92  
**Current Size:** 56 bytes  
**Alignment:** Default

### Current Layout
```csharp
private struct QueryKey
{
    public Vector3 SourcePosition;           // 12 bytes
    public Vector3 ListenerPosition;         // 12 bytes
    public int LayerMask;                    // 4 bytes
    public ulong IgnoreOriginRootEntityId;   // 8 bytes
    public ulong IgnoreTargetRootEntityId;   // 8 bytes
    public ulong IgnoreOriginBodyEntityId;   // 8 bytes
    public ulong IgnoreTargetBodyEntityId;   // 8 bytes
    // Total: 60 bytes, alignment 4
}
```

### Surgery: Reordered + Padded
```csharp
[StructLayout(LayoutKind.Sequential)]
private struct QueryKey
{
    public Vector3 SourcePosition;           // 12 bytes
    public int LayerMask;                    // 4 bytes → 16-byte boundary
    public Vector3 ListenerPosition;         // 12 bytes
    private int _padding0;                    // 4 bytes → 16-byte boundary
    public ulong IgnoreOriginRootEntityId;   // 8 bytes
    public ulong IgnoreTargetRootEntityId;   // 8 bytes
    public ulong IgnoreOriginBodyEntityId;   // 8 bytes
    public ulong IgnoreTargetBodyEntityId;   // 8 bytes
    // Total: 64 bytes, 8-byte aligned
}
```

---

## Surgery Priority Queue

| Priority | Struct | File | Action | Risk |
|----------|--------|------|--------|------|
| P0 | `HectonVegetationInstanceData` | `InstancedFloraRenderer.cs` | Reorder + pad to 48 bytes | HIGH — GPU instancing break if size changes |
| P1 | `QueryKey` | `AcousticOcclusionUtility.cs` | Reorder fields | LOW — internal struct |
| P2 | `ForcePacket` | `PhysicsApplySystem.cs` | Pad to 40 bytes | LOW — queue internal |
| P3 | `EnclosureKey` | `AcousticOcclusionUtility.cs` | Same treatment as QueryKey | LOW |

---

## Verification Protocol

After each struct surgery:
1. Check `UnsafeUtility.SizeOf<T>()` equals target size
2. Check `UnsafeUtility.AlignOf<T>()` is multiple of 8
3. Run Burst compiler — verify no `Struct size mismatch` errors
4. Test GPU instancing count matches pre-surgery

**STATUS:** PENDING SURGERY — Requires Agent BETA or runtime owner approval.
