# HECTON-8 DATA DICTIONARY — DOD Struct Reference
Date: 2026-04-28
Status: REFERENCE


## Current-State Addendum (2026-04-29)

This file is a dated structural reference snapshot, not a fresh surgery-ready authority.

Important boundary:

- some layout notes here are still useful as orientation
- they were not all revalidated against a fresh full struct-by-struct source reread in this pass
- any real struct surgery must re-read the live owner files first and verify `UnsafeUtility.SizeOf<T>()` / `AlignOf<T>()` against the current code, not against this document alone

Use this file as a reference map, not as blind implementation instructions.

**Версия:** 2026-04-28 | **Исторический статус на момент скана:** ETA VERIFIED

---

## 📋 КРИТИЧЕСКИЕ STRUCTS — AARCH (Absolute AUP)

### AbsoluteUniversePosition

**Файл:** `World/PersistentWorldRegistry.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
internal struct AbsoluteUniversePosition {
    public long gridX;    // 8 bytes — int64 grid coordinate
    public long gridY;    // 8 bytes
    public long gridZ;    // 8 bytes
    public float localX;   // 4 bytes — local offset within cell
    public float localY;  // 4 bytes
    public float localZ;  // 4 bytes
}
```

**Размер:** 36 bytes ⚠️ NOT 16-byte aligned — требует padding

---

### AbsoluteUniversePositionBlit128

**Файл:** `World/PersistentWorldRegistry.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 16, Size = 48)]
internal struct AbsoluteUniversePositionBlit128 {
    public float4 cellOrigin;     // 16 bytes
    public float4 localOffset;   // 16 bytes
    public float4 orientation;   // 16 bytes — quaternion encoding
}
```

**Размер:** 48 bytes ✅ 16-byte aligned

---

## 📋 PHYSICS STRUCTS

### ForcePacket

**Файл:** `PhysicsApplySystem.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct ForcePacket {
    public float3 position;      // 12 bytes
    public float3 impulse;       // 12 bytes
    public float torque;        // 4 bytes
    public ForcePacketPayload payload; // enum (4 bytes)
    public uint targetEntity;    // 4 bytes
}
```

**Размер:** ~36 bytes ✅ Burst-compatible

---

### SplashEvent

**Файл:** `SubmarineFluidDynamics.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct SplashEvent {
    public float3 position;      // 12 bytes
    public float3 velocity;     // 12 bytes
    public float massKg;       // 4 bytes
    public float timestamp;      // 4 bytes
}
```

**Размер:** 32 bytes ✅ Burst-compatible

---

### CompartmentState

**Файл:** `SubmarineFluidDynamics.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
private struct CompartmentState {
    public float volumeM3;         // 4 bytes
    public float floodedVolumeM3;  // 4 bytes
    public float airMassKg;         // 4 bytes
    public float o2Kg;            // 4 bytes
    public float co2Kg;           // 4 bytes
    public float temperatureK;     // 4 bytes
    public float pressureKPa;        // 4 bytes
}
```

**Размер:** 32 bytes ✅ Burst-compatible, Pack = 4

---

## 📋 AI / COGNITION STRUCTS

### CognitionCore

**Файл:** `Fauna/PredatorCognitionDomain.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
internal struct CognitionCore {
    public fixed byte Data[64];   // 64 bytes — tightly packed
}
```

**Размер:** 64 bytes ✅ Cache-aligned

---

### PackedCognitionOutput

**Файл:** `Fauna/PredatorCognitionDomain.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 40)]
internal struct PackedCognitionOutput {
    public float3 decisionForce;     // 12 bytes
    public float3 attentionVector;   // 12 bytes
    public float urgency;            // 4 bytes
    public float confidence;        // 4 bytes
    public uint stateFlags;          // 4 bytes
    public uint targetEntityID;      // 4 bytes
}
```

**Размер:** 40 bytes ⚠️ NOT 16-byte aligned

---

## 📋 SAVE / PERSISTENCE STRUCTS

### SaveFileHeader

**Файл:** `SaveBinaryStorage.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = CurrentHeaderSize)]
internal struct SaveFileHeader {
    public ulong magic;                  // 8 bytes — 'H8SAV000'
    public uint version;                 // 4 bytes
    public uint headerSize;               // 4 bytes
    public long creationTimestamp;        // 8 bytes
    public long playTimeSeconds;        // 8 bytes
    public fixed byte sceneName[64];    // 64 bytes
    public float3 playerPosition;       // 12 bytes
    public uint worldSeed;            // 4 bytes
    public uint checksum;            // 4 bytes
    public uint headerChecksum;       // 4 bytes
}
```

**Размер:** ~120 bytes ✅ Pack = 1 for binary serialization

---

### DeltaCell

**Файл:** `SaveBinaryStorage.cs`

```csharp
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct DeltaCell {
    public int x;                // 4 bytes
    public int y;                // 4 bytes
    public int oldValue;          // 4 bytes
    public int newValue;         // 4 bytes
    public uint timestamp;       // 4 bytes
}
```

**Размер:** 20 bytes ⚠️ NOT 16-byte aligned

---

## 📋 WORLD / VEGETATION STRUCTS

### HectonVegetationInstanceData

**Файл:** `World/HectonIndirectVegetationContracts.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct HectonVegetationInstanceData {
    public float4x4 worldMatrix;      // 64 bytes — matrix
    public float4 colorParams;        // 16 bytes — wind/tint
    public float4 windData;          // 16 bytes — motion
    public uint instanceID;          // 4 bytes
    public uint variationSeed;         // 4 bytes
}
```

**Размер:** ~104 bytes ✅ Used in GPU instancing

---

### FloraInteractionPointGpuData

**Файл:** `World/FloraInteractionManager.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
private struct FloraInteractionPointGpuData {
    public float3 position;        // 12 bytes
    public float interaction;      // 4 bytes
    public float3 normal;        // 12 bytes
    public float padding;        // 4 bytes
}
```

**Размер:** 32 bytes ✅ Burst-compatible

---

## 📋 BOIDS / ECOSYSTEM STRUCTS

### BoidData

**Файл:** `World/SargassumMicroFaunaBoids.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
internal struct BoidData {
    public float3 position;      // 12 bytes
    public uint entityID;        // 4 bytes
    public float3 velocity;      // 12 bytes
    public uint flags;          // 4 bytes
}
```

**Размер:** 32 bytes ✅ Pack = 4, cache-aligned

---

### SimulationFrameConstants

**Файл:** `World/SargassumMicroFaunaBoids.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 640)]
private struct SimulationFrameConstants {
    public float4 userParams0;      // 16 bytes
    public float4 userParams1;      // 16 bytes
    // ... 39 more float4 fields
}
```

**Размер:** 640 bytes ⚠️ Large constant buffer

---

## 📋 SUBMARINE STRUCTS

### AtmosphereStepJob

**Файл:** `SubmarineAtmosphereSystem.cs`

```csharp
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
[StructLayout(LayoutKind.Sequential)]
private struct AtmosphereStepJob : IJob {
    public NativeArray<float> o2Front;
    public NativeArray<float> o2Back;
    public NativeArray<float> co2Front;
    public NativeArray<float> co2Back;
    public NativeArray<float> pressureFront;
    public NativeArray<float> pressureBack;
    public NativeArray<float> temperatureFront;
    public NativeArray<float> temperatureBack;
    // ... more arrays
}
```

**Размер:** Variable (handles NativeArrays)

---

## 📋 INTERACTION STRUCTS

### InteractionPacket

**Файл:** `Interaction/EquipmentInteractionContracts.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct InteractionPacket {
    public uint sourceEntityID;        // 4 bytes
    public uint targetEntityID;    // 4 bytes
    public float3 hitPosition;      // 12 bytes
    public float3 hitNormal;        // 12 bytes
    public InteractionPhase phase; // enum (4 bytes)
    public InteractionToolFlags flags; // enum (4 bytes)
}
```

**Размер:** 40 bytes ⚠️ NOT 16-byte aligned

---

## 📋 PLAYER STATE STRUCTS

### PlayerMovementRuntimeState

**Файл:** `Core/PlayerRuntimeContext.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct PlayerMovementRuntimeState {
    public float3 position;        // 12 bytes
    public float3 velocity;         // 12 bytes
    public quaternion rotation;    // 16 bytes
    public float swimSpeed;     // 4 bytes
    public float verticalThrust; // 4 bytes
    public uint groundFlags;    // 4 bytes
}
```

**Размер:** 52 bytes ⚠️ NOT 16-byte aligned — needs padding

---

### PlayerSurvivalRuntimeState

**Файл:** `Core/PlayerRuntimeContext.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct PlayerSurvivalRuntimeState {
    public float oxygenPercent;      // 4 bytes
    public float energyPercent;      // 4 bytes
    public float integrityPercent; // 4 bytes
    public float temperaturePercent; // 4 bytes
    public float depthMeters;     // 4 bytes
    public float weightKg;       // 4 bytes
    public uint hazardFlags;       // 4 bytes
}
```

**Размер:** 28 bytes ⚠️ NOT 16-byte aligned

---

## 📋 OPTIMIZATION ALERTS

### ❌ NOT 16-BYTE ALIGNED (Require Fix):

| Struct | Current Size | Required Size |
|--------|------------|--------------|
| `AbsoluteUniversePosition` | 36 | 48 (padding) |
| `PlayerMovementRuntimeState` | 52 | 64 (padding) |
| `PlayerSurvivalRuntimeState` | 28 | 32 (padding) |
| `InteractionPacket` | 40 | 48 (padding) |
| `PackedCognitionOutput` | 40 | 48 (padding) |
| `DeltaCell` | 20 | 32 (padding) |

---

## 📋 QUANTIZATION STRUCTS

### SByte3

**Файл:** `World/ChunkLocalOffsetQuantization.cs`

```csharp
internal struct SByte3
{
    public sbyte X;   // 1 byte
    public sbyte Y;   // 1 byte
    public sbyte Z;   // 1 byte
}
```

**Размер:** 3 bytes ✅ Pack = 1 (sbyte alignment = 1)  
**Назначение:** Мёртвый мусор (трупы рыб, осколки) хранится как offset от центра чанка вместо float3.  
**Экономия:** 12 bytes → 3 bytes (−75%).

---

### QuantizedLocalOffset

**Файл:** `World/ChunkLocalOffsetQuantization.cs`

```csharp
internal struct QuantizedLocalOffset
{
    public SByte3 Packed;   // 3 bytes
}
```

**Размер:** 3 bytes ✅ Pack = 1  
**Назначение:** Wrapper для типизированного массива квантованных смещений в Job-системе сброса мусора.  
**Alignment:** 1 byte (no padding).

---

## 📋 BURST JOB STRUCTS

### BuoyancyJob

**Файл:** `HectonFluidEngine.cs`

```csharp
[BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
[StructLayout(LayoutKind.Sequential)]
public struct BuoyancyJob : IJobParallelFor {
    [ReadOnly] public NativeArray<BuoyancyParams> inputParams;
    [ReadOnly] public NativeArray<float3> queryPositions;
    [WriteOnly] public NativeArray<float> resultHeights;
    public float dt;
}
```

---

### WaveQueryJob

**Файл:** `HectonFluidEngine.cs`

```csharp
[BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
[StructLayout(LayoutKind.Sequential)]
public struct WaveQueryJob : IJobParallelFor {
    [ReadOnly] public float3 position;
    public float time;
    // ... wave parameters
}
```

---

**Historical Scan Status:** ETA VERIFIED — 11 critical structs documented, 6 alignment violations flagged
