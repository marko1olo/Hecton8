# Equipment Voxel Weld Surgery Log

Mandates followed:
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `CTRL_Device_Abstraction_Haptics.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `VOX_Voxel_World_Logic_Carving_Persistence.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

## Haptic Queue Integration

```csharp
protected void QueueToolHapticFeedback(float powerDelivered, float ratedPower, byte priority = 1)
{
    ToolHapticsRuntime.EnqueueToolFeedback(powerDelivered, ratedPower, priority);
}
```

```csharp
private void EnqueueBackBuffer(float powerDelivered, float ratedPower, byte priority)
{
    EnsureBuffers();
    if (_backCount >= BufferCapacity)
        return;

    float normalizedPower = ratedPower > 0.0001f
        ? math.saturate(powerDelivered / ratedPower)
        : 0f;
    if (normalizedPower <= 0f)
        return;

    _backBuffer[_backCount++] = new HapticCommand
    {
        LowFreqIntensity = 0f,
        HighFreqIntensity = normalizedPower,
        DurationRemaining = DefaultDurationSeconds,
        DecayRate = DefaultDecayRate,
        Priority = priority,
        MotorMask = RightMotorMask,
        BlendMode = 1
    };
}
```

Runtime constants:
- `HighFreqIntensity = clamp(powerDelivered / ratedPower, 0, 1)`
- `DecayRate = 1.5f`
- `MotorMask = 0b0010`

## Voxel Weld Delta Shape

`VoxelModifiedCell`:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct VoxelModifiedCell
{
    public half Density;
    public byte MaterialId;
    public byte Flags;
    public ushort Reserved;
}
```

Mode legend:
- `Flags & 0x01 == 0` => subtractive carve
- `Flags & 0x01 != 0` => additive weld

Rebuild rule:

```csharp
if ((storedCell.Flags & 0x01) != 0)
{
    smoothDensityValue = math.max(smoothDensityValue, deltaDensity);
    finalDensityValue = math.max(finalDensityValue, deltaDensity);
}
else
{
    smoothDensityValue = math.min(smoothDensityValue, deltaDensity);
    finalDensityValue = math.min(finalDensityValue, deltaDensity);
}
```

## Brownout Rule

Runtime owner: `ModularEquipmentEngine`

```csharp
if (!_wirelessBrownoutActive || !TryGetToolState(toolId, out ToolState state))
    return stats.EfficiencyScalar;

return (state.UpgradeBitmask & (uint)ToolUpgradeBits.WirelessCharging) != 0u
    ? stats.EfficiencyScalar * 0.5f
    : stats.EfficiencyScalar;
```
