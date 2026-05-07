# HECTON-8 DATA DICTIONARY â€” DOD Struct Reference
Date: 2026-05-07
Status: PENDING VERIFICATION


## Current-State Addendum (2026-04-29)

This file is a dated structural reference snapshot, not a fresh surgery-ready authority.

Important boundary:

- some layout notes here are still useful as orientation
- they were not all revalidated against a fresh full struct-by-struct source reread in this pass
- any real struct surgery must re-read the live owner files first and verify `UnsafeUtility.SizeOf<T>()` / `AlignOf<T>()` against the current code, not against this document alone

Use this file as a reference map, not as blind implementation instructions.

**Ð’ÐµÑ€ÑÐ¸Ñ:** 2026-04-28 | **Ð˜ÑÑ‚Ð¾Ñ€Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ Ð½Ð° Ð¼Ð¾Ð¼ÐµÐ½Ñ‚ ÑÐºÐ°Ð½Ð°:** ETA VERIFIED

---

## ðŸ“‹ ÐšÐ Ð˜Ð¢Ð˜Ð§Ð•Ð¡ÐšÐ˜Ð• STRUCTS â€” AARCH (Absolute AUP)

### AbsoluteUniversePosition

**Ð¤Ð°Ð¹Ð»:** `World/PersistentWorldRegistry.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
internal struct AbsoluteUniversePosition {
    public long gridX;    // 8 bytes â€” int64 grid coordinate
    public long gridY;    // 8 bytes
    public long gridZ;    // 8 bytes
    public float localX;   // 4 bytes â€” local offset within cell
    public float localY;  // 4 bytes
    public float localZ;  // 4 bytes
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 36 bytes âš ï¸ NOT 16-byte aligned â€” Ñ‚Ñ€ÐµÐ±ÑƒÐµÑ‚ padding

---

### AbsoluteUniversePositionBlit128

**Ð¤Ð°Ð¹Ð»:** `World/PersistentWorldRegistry.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 16, Size = 48)]
internal struct AbsoluteUniversePositionBlit128 {
    public float4 cellOrigin;     // 16 bytes
    public float4 localOffset;   // 16 bytes
    public float4 orientation;   // 16 bytes â€” quaternion encoding
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 48 bytes âœ… 16-byte aligned

---

## ðŸ“‹ PHYSICS STRUCTS

### ForcePacket

**Ð¤Ð°Ð¹Ð»:** `PhysicsApplySystem.cs`

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

**Ð Ð°Ð·Ð¼ÐµÑ€:** ~36 bytes âœ… Burst-compatible

---

### SplashEvent

**Ð¤Ð°Ð¹Ð»:** `SubmarineFluidDynamics.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct SplashEvent {
    public float3 position;      // 12 bytes
    public float3 velocity;     // 12 bytes
    public float massKg;       // 4 bytes
    public float timestamp;      // 4 bytes
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 32 bytes âœ… Burst-compatible

---

### CompartmentState

**Ð¤Ð°Ð¹Ð»:** `SubmarineFluidDynamics.cs`

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

**Ð Ð°Ð·Ð¼ÐµÑ€:** 32 bytes âœ… Burst-compatible, Pack = 4

---

## ðŸ“‹ AI / COGNITION STRUCTS

### CognitionCore

**Ð¤Ð°Ð¹Ð»:** `Fauna/PredatorCognitionDomain.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
internal struct CognitionCore {
    public fixed byte Data[64];   // 64 bytes â€” tightly packed
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 64 bytes âœ… Cache-aligned

---

### PackedCognitionOutput

**Ð¤Ð°Ð¹Ð»:** `Fauna/PredatorCognitionDomain.cs`

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

**Ð Ð°Ð·Ð¼ÐµÑ€:** 40 bytes âš ï¸ NOT 16-byte aligned

---

## ðŸ“‹ SAVE / PERSISTENCE STRUCTS

### SaveFileHeader

**Ð¤Ð°Ð¹Ð»:** `SaveBinaryStorage.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = CurrentHeaderSize)]
internal struct SaveFileHeader {
    public ulong magic;                  // 8 bytes â€” 'H8SAV000'
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

**Ð Ð°Ð·Ð¼ÐµÑ€:** ~120 bytes âœ… Pack = 1 for binary serialization

---

### DeltaCell

**Ð¤Ð°Ð¹Ð»:** `SaveBinaryStorage.cs`

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

**Ð Ð°Ð·Ð¼ÐµÑ€:** 20 bytes âš ï¸ NOT 16-byte aligned

---

## ðŸ“‹ WORLD / VEGETATION STRUCTS

### HectonVegetationInstanceData

**Ð¤Ð°Ð¹Ð»:** `World/HectonIndirectVegetationContracts.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct HectonVegetationInstanceData {
    public float4x4 worldMatrix;      // 64 bytes â€” matrix
    public float4 colorParams;        // 16 bytes â€” wind/tint
    public float4 windData;          // 16 bytes â€” motion
    public uint instanceID;          // 4 bytes
    public uint variationSeed;         // 4 bytes
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** ~104 bytes âœ… Used in GPU instancing

---

### FloraInteractionPointGpuData

**Ð¤Ð°Ð¹Ð»:** `World/FloraInteractionManager.cs`

```csharp
[StructLayout(LayoutKind.Sequential)]
private struct FloraInteractionPointGpuData {
    public float3 position;        // 12 bytes
    public float interaction;      // 4 bytes
    public float3 normal;        // 12 bytes
    public float padding;        // 4 bytes
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 32 bytes âœ… Burst-compatible

---

## ðŸ“‹ BOIDS / ECOSYSTEM STRUCTS

### BoidData

**Ð¤Ð°Ð¹Ð»:** `World/SargassumMicroFaunaBoids.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
internal struct BoidData {
    public float3 position;      // 12 bytes
    public uint entityID;        // 4 bytes
    public float3 velocity;      // 12 bytes
    public uint flags;          // 4 bytes
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 32 bytes âœ… Pack = 4, cache-aligned

---

### SimulationFrameConstants

**Ð¤Ð°Ð¹Ð»:** `World/SargassumMicroFaunaBoids.cs`

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 640)]
private struct SimulationFrameConstants {
    public float4 userParams0;      // 16 bytes
    public float4 userParams1;      // 16 bytes
    // ... 39 more float4 fields
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 640 bytes âš ï¸ Large constant buffer

---

## ðŸ“‹ SUBMARINE STRUCTS

### AtmosphereStepJob

**Ð¤Ð°Ð¹Ð»:** `SubmarineAtmosphereSystem.cs`

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

**Ð Ð°Ð·Ð¼ÐµÑ€:** Variable (handles NativeArrays)

---

## ðŸ“‹ INTERACTION STRUCTS

### InteractionPacket

**Ð¤Ð°Ð¹Ð»:** `Interaction/EquipmentInteractionContracts.cs`

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

**Ð Ð°Ð·Ð¼ÐµÑ€:** 40 bytes âš ï¸ NOT 16-byte aligned

---

## ðŸ“‹ PLAYER STATE STRUCTS

### PlayerMovementRuntimeState

**Ð¤Ð°Ð¹Ð»:** `Core/PlayerRuntimeContext.cs`

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

**Ð Ð°Ð·Ð¼ÐµÑ€:** 52 bytes âš ï¸ NOT 16-byte aligned â€” needs padding

---

### PlayerSurvivalRuntimeState

**Ð¤Ð°Ð¹Ð»:** `Core/PlayerRuntimeContext.cs`

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

**Ð Ð°Ð·Ð¼ÐµÑ€:** 28 bytes âš ï¸ NOT 16-byte aligned

---

## ðŸ“‹ OPTIMIZATION ALERTS

### âŒ NOT 16-BYTE ALIGNED (Require Fix):

| Struct | Current Size | Required Size |
|--------|------------|--------------|
| `AbsoluteUniversePosition` | 36 | 48 (padding) |
| `PlayerMovementRuntimeState` | 52 | 64 (padding) |
| `PlayerSurvivalRuntimeState` | 28 | 32 (padding) |
| `InteractionPacket` | 40 | 48 (padding) |
| `PackedCognitionOutput` | 40 | 48 (padding) |
| `DeltaCell` | 20 | 32 (padding) |

---

## ðŸ“‹ QUANTIZATION STRUCTS

### SByte3

**Ð¤Ð°Ð¹Ð»:** `World/ChunkLocalOffsetQuantization.cs`

```csharp
internal struct SByte3
{
    public sbyte X;   // 1 byte
    public sbyte Y;   // 1 byte
    public sbyte Z;   // 1 byte
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 3 bytes âœ… Pack = 1 (sbyte alignment = 1)
**ÐÐ°Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ðµ:** ÐœÑ‘Ñ€Ñ‚Ð²Ñ‹Ð¹ Ð¼ÑƒÑÐ¾Ñ€ (Ñ‚Ñ€ÑƒÐ¿Ñ‹ Ñ€Ñ‹Ð±, Ð¾ÑÐºÐ¾Ð»ÐºÐ¸) Ñ…Ñ€Ð°Ð½Ð¸Ñ‚ÑÑ ÐºÐ°Ðº offset Ð¾Ñ‚ Ñ†ÐµÐ½Ñ‚Ñ€Ð° Ñ‡Ð°Ð½ÐºÐ° Ð²Ð¼ÐµÑÑ‚Ð¾ float3.
**Ð­ÐºÐ¾Ð½Ð¾Ð¼Ð¸Ñ:** 12 bytes â†’ 3 bytes (âˆ’75%).

---

### QuantizedLocalOffset

**Ð¤Ð°Ð¹Ð»:** `World/ChunkLocalOffsetQuantization.cs`

```csharp
internal struct QuantizedLocalOffset
{
    public SByte3 Packed;   // 3 bytes
}
```

**Ð Ð°Ð·Ð¼ÐµÑ€:** 3 bytes âœ… Pack = 1
**ÐÐ°Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ðµ:** Wrapper Ð´Ð»Ñ Ñ‚Ð¸Ð¿Ð¸Ð·Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ð¾Ð³Ð¾ Ð¼Ð°ÑÑÐ¸Ð²Ð° ÐºÐ²Ð°Ð½Ñ‚Ð¾Ð²Ð°Ð½Ð½Ñ‹Ñ… ÑÐ¼ÐµÑ‰ÐµÐ½Ð¸Ð¹ Ð² Job-ÑÐ¸ÑÑ‚ÐµÐ¼Ðµ ÑÐ±Ñ€Ð¾ÑÐ° Ð¼ÑƒÑÐ¾Ñ€Ð°.
**Alignment:** 1 byte (no padding).

---

## ðŸ“‹ BURST JOB STRUCTS

### BuoyancyJob

**Ð¤Ð°Ð¹Ð»:** `HectonFluidEngine.cs`

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

**Ð¤Ð°Ð¹Ð»:** `HectonFluidEngine.cs`

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

**Historical Scan Status:** ETA VERIFIED â€” 11 critical structs documented, 6 alignment violations flagged
