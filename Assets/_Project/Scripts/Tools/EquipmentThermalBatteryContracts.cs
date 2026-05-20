namespace Hecton8.Tools
{
    using System.Runtime.InteropServices;
    using Hecton8.Core.Contracts.Signals;
    using Unity.Mathematics;

    public static class ActiveEquipmentStateFlags
    {
        public const uint Active = 1u << 0;
        public const uint Overheated = 1u << 1;
        public const uint InWater = 1u << 2;
        public const uint GridPowered = 1u << 3;
        public const uint Depleted = 1u << 4;
        public const uint Faulted = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ActiveEquipmentDTO
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public float CurrentBattery;
        [FieldOffset(8)] public float ThermalLoad;
        [FieldOffset(12)] public uint StateFlags;
        [FieldOffset(16)] public float PowerDrawRate;
        [FieldOffset(20)] public float HeatGenerationRate;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EquipmentGridLoadRequest
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public float EnergyWattSeconds;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EquipmentIntegrationCounters
    {
        [FieldOffset(0)] public float BatteryDrainWattSeconds;
        [FieldOffset(4)] public float GridDrawWattSeconds;
        [FieldOffset(8)] public float PeakThermal01;
        [FieldOffset(12)] public uint ActiveCount;
        [FieldOffset(16)] public uint SignalCount;
        [FieldOffset(20)] public uint FaultFlags;
        [FieldOffset(24)] public uint LastFaultToolHashID;
        [FieldOffset(28)] public float WearDrainNormalized;
        [FieldOffset(32)] public ulong Reserved1;
        [FieldOffset(40)] public ulong Reserved2;
        [FieldOffset(48)] public ulong Reserved3;
        [FieldOffset(56)] public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EquipmentTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint TickIndex;
        [FieldOffset(8)] public float BatteryDrainWattSeconds;
        [FieldOffset(12)] public float GridDrawWattSeconds;
        [FieldOffset(16)] public float PeakThermal01;
        [FieldOffset(20)] public uint ActiveToolMask;
        [FieldOffset(24)] public uint SignalCount;
        [FieldOffset(28)] public uint FaultFlags;
        [FieldOffset(32)] public uint LastFaultToolHashID;
        [FieldOffset(36)] public float CpuMicroseconds;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float TickIntervalSeconds;
        [FieldOffset(48)] public int ThermalGridVersion;
        [FieldOffset(52)] public int ThermalGridCellCount;
        [FieldOffset(56)] public uint SnapshotHash;
        [FieldOffset(60)] public float WearDrainNormalized;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EquipmentOverheatSignal : ISignal
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Heat01;
        [FieldOffset(12)] public float AmbientCelsius;
        [FieldOffset(16)] public float Severity01;
        [FieldOffset(20)] public uint StateFlags;
        [FieldOffset(24)] public byte VisualOnly;
        [FieldOffset(25)] public byte Reserved0;
        [FieldOffset(26)] public ushort Reserved1;
        [FieldOffset(28)] public uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolDepletedSignal : ISignal
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Battery01;
        [FieldOffset(12)] public float RequestedPower;
        [FieldOffset(16)] public uint StateFlags;
        [FieldOffset(20)] public byte GridPowered;
        [FieldOffset(21)] public byte Reserved0;
        [FieldOffset(22)] public ushort Reserved1;
        [FieldOffset(24)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EquipmentTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float MinimumTickInterval;
        [FieldOffset(8)] public float MaximumTickInterval;
        [FieldOffset(12)] public float CoolingGain;
        [FieldOffset(16)] public float WaterCoolingMultiplier;
        [FieldOffset(20)] public float AmbientHeatFloorCelsius;
        [FieldOffset(24)] public float AmbientHeatCeilingCelsius;
        [FieldOffset(28)] public uint Flags;

        public static EquipmentTuningDTO CreateDefault(float globalQualityWeight)
        {
            return new EquipmentTuningDTO
            {
                GlobalQualityWeight = math.saturate(globalQualityWeight),
                MinimumTickInterval = 0.016f,
                MaximumTickInterval = 0.2f,
                CoolingGain = 0.82f,
                WaterCoolingMultiplier = 2.75f,
                AmbientHeatFloorCelsius = -2f,
                AmbientHeatCeilingCelsius = 70f,
                Flags = 0u
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EquipmentHardwareSpecDTO
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public float BatteryCapacity;
        [FieldOffset(8)] public float ThermalLimit;
        [FieldOffset(12)] public float PowerDrawRate;
        [FieldOffset(16)] public float HeatGenerationRate;
        [FieldOffset(20)] public float CooldownRate;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EquipmentCsvParseResult
    {
        [FieldOffset(0)] public int ParsedRows;
        [FieldOffset(4)] public int SkippedRows;
        [FieldOffset(8)] public uint LastToolHashID;
        [FieldOffset(12)] public uint FaultFlags;
    }
}
