using System;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physics.Vehicles
{
    public static class VehicleDamageConstants
    {
        public const int DefaultGridWidth = 16;
        public const int DefaultGridHeight = 6;
        public const int DefaultGridDepth = 8;
        public const int DefaultCellCount = DefaultGridWidth * DefaultGridHeight * DefaultGridDepth;
        public const int MaxDamageSignals = 128;
        public const int MaxMockDamageSignals = 32;
        public const int TelemetryCapacity = 300;
        public const int CsvScratchBytes = 64 * 1024;
        public const int JobBatchSize = 32;

        public const uint ComponentHull = 0x6EA478B6u; // fnv1a("hull")
        public const uint ComponentEngine = 0xEE05D83Bu; // fnv1a("engine")
        public const uint ComponentBallast = 0x16368F10u; // fnv1a("ballast")
        public const uint ComponentSensors = 0x5FD70E98u; // fnv1a("sensors")
        public const uint ComponentPower = 0xF54F2346u; // fnv1a("power")

        public const uint ComponentAliasSensor = 0x83B6367Bu; // fnv1a("sensor")
        public const uint ComponentAliasSonar = 0xCC21B794u; // fnv1a("sonar")
        public const uint ComponentAliasEngines = 0xFB337958u; // fnv1a("engines")
        public const uint ComponentAliasReactor = 0x8B99E7E1u; // fnv1a("reactor")
        public const uint ComponentAliasBattery = 0xFD6A0C8Eu; // fnv1a("battery")

        public const uint CellFlagOuterHull = 1u << 0;
        public const uint CellFlagFlooded = 1u << 1;
        public const uint CellFlagBurning = 1u << 2;
        public const uint CellFlagDestroyed = 1u << 3;
        public const uint CellFlagFlammable = 1u << 4;
        public const uint CellFlagSensorCritical = 1u << 5;
        public const uint CellFlagEngineCritical = 1u << 6;
        public const uint CellFlagBallastCritical = 1u << 7;

        public const uint StateFlagInitialized = 1u << 0;
        public const uint StateFlagFatalNan = 1u << 1;
        public const uint StateFlagHasBreach = 1u << 2;
        public const uint StateFlagHasFire = 1u << 3;
        public const uint StateFlagCsvLayout = 1u << 4;

        public const uint DamageFlagMapped = 1u << 0;
        public const uint DamageFlagExplosive = 1u << 1;
        public const uint DamageFlagFiniteAup = 1u << 2;
        public const uint DamageTypeExplosiveMask = 1u << 0;

        public const byte HazardFire = 1;
        public const byte HazardFlood = 2;
        public const byte HazardDestroyed = 3;

        public const uint SourceHashMock = 0x56534D4Bu; // VSMK
        public const uint SourceHashCsv = 0x56435356u; // VCSV
        public const uint SourceHashRuntime = 0x56524447u; // VRDG
        public const uint SourceHashEditor = 0x56454454u; // VEDT

        public const uint TuningFlagCsvLayout = 1u << 0;
        public const uint TuningFlagRuntimeSerialized = 1u << 1;
        public const uint TuningFlagEditorOverride = 1u << 2;

        public const BufferID GridWriteBuffer = (BufferID)71640;
        public const BufferID GridReadBuffer = (BufferID)71641;
        public const BufferID SignalBuffer = (BufferID)71642;
        public const BufferID MockSignalBuffer = (BufferID)71643;
        public const BufferID StateWriteBuffer = (BufferID)71644;
        public const BufferID StateReadBuffer = (BufferID)71645;
        public const BufferID TuningBuffer = (BufferID)71646;
        public const BufferID TelemetryRingBuffer = (BufferID)71647;
        public const BufferID TelemetryCursorBuffer = (BufferID)71648;
        public const BufferID CsvScratchBuffer = (BufferID)71649;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VehicleGridCellDTO
    {
        [FieldOffset(0)] public float Integrity01;
        [FieldOffset(4)] public uint ComponentHash;
        [FieldOffset(8)] public uint StatusFlags;
        [FieldOffset(12)] public float ArmorValue;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct VehicleDamageSignalDTO
    {
        [FieldOffset(0)] public double3 ImpactAup;
        [FieldOffset(24)] public float3 Direction;
        [FieldOffset(36)] public float Magnitude;
        [FieldOffset(40)] public uint DamageType;
        [FieldOffset(44)] public uint TargetHash;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public ushort SourceId;
        [FieldOffset(58)] public ushort TargetId;
        [FieldOffset(60)] public byte Channel;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public byte IntegrityDelta;
        [FieldOffset(63)] public byte Reserved0;
        [FieldOffset(64)] public float RadiusMeters;
        [FieldOffset(68)] public float Falloff;
        [FieldOffset(72)] public float ArmorPierce;
        [FieldOffset(76)] public float3 LocalPoint;
        [FieldOffset(88)] public int GridIndex;
        [FieldOffset(92)] public uint MappedFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VehicleDamageStateDTO
    {
        [FieldOffset(0)] public float MaxThrustScalar;
        [FieldOffset(4)] public float BuoyancyScalar;
        [FieldOffset(8)] public float SensorScalar;
        [FieldOffset(12)] public float DragScalar;
        [FieldOffset(16)] public float FloodWaterMassKg;
        [FieldOffset(20)] public float IngressRateKgPerSecond;
        [FieldOffset(24)] public float FireSeverity01;
        [FieldOffset(28)] public float StructuralIntegrity01;
        [FieldOffset(32)] public uint ActiveBreaches;
        [FieldOffset(36)] public uint BurningCells;
        [FieldOffset(40)] public uint DestroyedCells;
        [FieldOffset(44)] public uint DamagedCells;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint SignalCount;
        [FieldOffset(64)] public double3 LastImpactAup;
        [FieldOffset(88)] public float3 LastImpactLocal;
        [FieldOffset(100)] public float TotalDamage01;
        [FieldOffset(104)] public float EstimatedCostUs;
        [FieldOffset(108)] public float QualityWeight;
        [FieldOffset(112)] public uint Reserved0;
        [FieldOffset(116)] public uint Reserved1;
        [FieldOffset(120)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct VehicleDamageTuningDTO
    {
        [FieldOffset(0)] public int GridWidth;
        [FieldOffset(4)] public int GridHeight;
        [FieldOffset(8)] public int GridDepth;
        [FieldOffset(12)] public float CellSizeMeters;
        [FieldOffset(16)] public float3 GridCenterLocal;
        [FieldOffset(28)] public float3 GridSizeLocal;
        [FieldOffset(40)] public float BaseArmor;
        [FieldOffset(44)] public float DirectDamageScale;
        [FieldOffset(48)] public float ExplosiveRadiusMeters;
        [FieldOffset(52)] public float ExplosionFalloff;
        [FieldOffset(56)] public float IngressKgPerSecond;
        [FieldOffset(60)] public float FireChance01;
        [FieldOffset(64)] public float SensorPenaltyWeight;
        [FieldOffset(68)] public float DragPenaltyWeight;
        [FieldOffset(72)] public float FloodMassLimitKg;
        [FieldOffset(76)] public uint SourceHash;
        [FieldOffset(80)] public float EngineMinimumScalar;
        [FieldOffset(84)] public float BallastMinimumScalar;
        [FieldOffset(88)] public float SensorMinimumScalar;
        [FieldOffset(92)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VehicleDamageTelemetryEntry
    {
        [FieldOffset(0)] public double3 RootAup;
        [FieldOffset(24)] public double3 LastImpactAup;
        [FieldOffset(48)] public float3 LastImpactLocal;
        [FieldOffset(60)] public float StructuralIntegrity01;
        [FieldOffset(64)] public float MaxThrustScalar;
        [FieldOffset(68)] public float BuoyancyScalar;
        [FieldOffset(72)] public float FloodWaterMassKg;
        [FieldOffset(76)] public float IngressRateKgPerSecond;
        [FieldOffset(80)] public float FireSeverity01;
        [FieldOffset(84)] public float EstimatedCostUs;
        [FieldOffset(88)] public uint Frame;
        [FieldOffset(92)] public uint StateHash;
        [FieldOffset(96)] public uint Flags;
        [FieldOffset(100)] public uint ActiveBreaches;
        [FieldOffset(104)] public uint BurningCells;
        [FieldOffset(108)] public uint DestroyedCells;
        [FieldOffset(112)] public uint DamagedCells;
        [FieldOffset(116)] public uint SignalCount;
        [FieldOffset(120)] public float TotalDamage01;
        [FieldOffset(124)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct VehicleHazardSignal : ISignal
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Severity01;
        [FieldOffset(16)] public uint ComponentHash;
        [FieldOffset(20)] public uint StatusFlags;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint VehicleHash;
        [FieldOffset(32)] public byte HazardType;
        [FieldOffset(33)] public byte Flags;
        [FieldOffset(34)] public ushort CellIndex;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    public static unsafe class VehicleDamageAccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref VehicleGridCellDTO GetCellRef(
            IDataVault vault,
            ref VaultBufferHandle<VehicleGridCellDTO> handle,
            int index)
        {
            void* pointer = handle.ResolvePointer(vault);
            if (pointer == null || (uint)index >= (uint)handle.Length)
                FatalMemoryException.ThrowStaleVaultHandle();

            return ref UnsafeUtility.ArrayElementAsRef<VehicleGridCellDTO>(pointer, index);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static class VehicleDamageLayoutValidator
    {
        public static bool ValidateVehicleGridCellLayout(out string error)
        {
            error = null;
            if (UnsafeUtility.SizeOf<VehicleGridCellDTO>() != 16)
            {
                error = "VehicleGridCellDTO size must be 16 bytes.";
                return false;
            }

            if (!ValidateOffset(nameof(VehicleGridCellDTO.Integrity01), 0, out error)) return false;
            if (!ValidateOffset(nameof(VehicleGridCellDTO.ComponentHash), 4, out error)) return false;
            if (!ValidateOffset(nameof(VehicleGridCellDTO.StatusFlags), 8, out error)) return false;
            if (!ValidateOffset(nameof(VehicleGridCellDTO.ArmorValue), 12, out error)) return false;
            return true;
        }

        private static bool ValidateOffset(string fieldName, int expected, out string error)
        {
            error = null;
            FieldInfo field = typeof(VehicleGridCellDTO).GetField(fieldName);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            error = fieldName + " offset expected " + expected + " observed " + observed + ".";
            return false;
        }
    }
#endif

    public static class VehicleComponentLayoutCsvParser
    {
        public static int Apply(ReadOnlySpan<byte> csv, NativeArray<VehicleGridCellDTO> cells, int width, int height, int depth)
        {
            if (!cells.IsCreated || width <= 0 || height <= 0 || depth <= 0)
                return 0;

            int applied = 0;
            int start = 0;
            while (start < csv.Length)
            {
                int end = start;
                while (end < csv.Length && csv[end] != (byte)'\n')
                    end++;

                ReadOnlySpan<byte> line = TrimAscii(csv.Slice(start, end - start));
                start = end + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!TryReadField(ref line, out ReadOnlySpan<byte> xField) ||
                    !TryReadField(ref line, out ReadOnlySpan<byte> yField) ||
                    !TryReadField(ref line, out ReadOnlySpan<byte> zField) ||
                    !TryReadField(ref line, out ReadOnlySpan<byte> componentField) ||
                    !TryReadField(ref line, out ReadOnlySpan<byte> armorField))
                {
                    continue;
                }

                if (!TryParseInt(xField, out int x) ||
                    !TryParseInt(yField, out int y) ||
                    !TryParseInt(zField, out int z) ||
                    !TryParseFloat(armorField, out float armor))
                {
                    continue;
                }

                if ((uint)x >= (uint)width || (uint)y >= (uint)height || (uint)z >= (uint)depth)
                    continue;

                uint parsedFlags = 0u;
                bool hasParsedFlags = false;
                if (TryReadField(ref line, out ReadOnlySpan<byte> flagsField))
                    hasParsedFlags = TryParseUInt(flagsField, out parsedFlags);

                int index = x + (y * width) + (z * width * height);
                if ((uint)index >= (uint)cells.Length)
                    continue;

                VehicleGridCellDTO cell = cells[index];
                uint componentHash = ResolveComponentHash(componentField);
                uint statusFlags = cell.StatusFlags | ResolveComponentFlags(componentHash);
                if (hasParsedFlags)
                    statusFlags |= parsedFlags;

                cell.ComponentHash = componentHash;
                cell.ArmorValue = math.max(0.01f, armor);
                cell.StatusFlags = statusFlags;
                cells[index] = cell;
                applied++;
            }

            return applied;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);

                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveComponentHash(ReadOnlySpan<byte> bytes)
        {
            uint hash = Fnv1A(bytes);
            if (hash == VehicleDamageConstants.ComponentAliasSensor ||
                hash == VehicleDamageConstants.ComponentAliasSonar)
            {
                return VehicleDamageConstants.ComponentSensors;
            }

            if (hash == VehicleDamageConstants.ComponentAliasEngines)
                return VehicleDamageConstants.ComponentEngine;

            if (hash == VehicleDamageConstants.ComponentAliasReactor ||
                hash == VehicleDamageConstants.ComponentAliasBattery)
            {
                return VehicleDamageConstants.ComponentPower;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveComponentFlags(uint componentHash)
        {
            if (componentHash == VehicleDamageConstants.ComponentEngine)
                return VehicleDamageConstants.CellFlagEngineCritical | VehicleDamageConstants.CellFlagFlammable;
            if (componentHash == VehicleDamageConstants.ComponentBallast)
                return VehicleDamageConstants.CellFlagBallastCritical;
            if (componentHash == VehicleDamageConstants.ComponentSensors)
                return VehicleDamageConstants.CellFlagSensorCritical;
            if (componentHash == VehicleDamageConstants.ComponentPower)
                return VehicleDamageConstants.CellFlagFlammable;
            return 0u;
        }

        private static bool TryReadField(ref ReadOnlySpan<byte> line, out ReadOnlySpan<byte> field)
        {
            field = default;
            if (line.Length == 0)
                return false;

            int comma = 0;
            while (comma < line.Length && line[comma] != (byte)',')
                comma++;

            field = TrimAscii(line.Slice(0, comma));
            line = comma < line.Length ? line.Slice(comma + 1) : ReadOnlySpan<byte>.Empty;
            return true;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start < value.Length && IsTrim(value[start]))
                start++;
            while (end >= start && IsTrim(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTrim(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
        }

        private static bool TryParseInt(ReadOnlySpan<byte> bytes, out int value)
        {
            value = 0;
            bytes = TrimAscii(bytes);
            if (bytes.Length == 0)
                return false;

            int sign = 1;
            int index = 0;
            if (bytes[0] == (byte)'-')
            {
                sign = -1;
                index = 1;
            }

            int result = 0;
            for (; index < bytes.Length; index++)
            {
                byte digit = bytes[index];
                if (digit < (byte)'0' || digit > (byte)'9')
                    return false;

                result = (result * 10) + (digit - (byte)'0');
            }

            value = result * sign;
            return true;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> bytes, out uint value)
        {
            value = 0u;
            bytes = TrimAscii(bytes);
            if (bytes.Length == 0)
                return false;

            uint result = 0u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte digit = bytes[i];
                if (digit < (byte)'0' || digit > (byte)'9')
                    return false;

                result = (result * 10u) + (uint)(digit - (byte)'0');
            }

            value = result;
            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            bytes = TrimAscii(bytes);
            if (bytes.Length == 0)
                return false;

            int sign = 1;
            int index = 0;
            if (bytes[0] == (byte)'-')
            {
                sign = -1;
                index = 1;
            }

            float result = 0f;
            bool hasDigit = false;
            for (; index < bytes.Length && bytes[index] != (byte)'.'; index++)
            {
                byte digit = bytes[index];
                if (digit < (byte)'0' || digit > (byte)'9')
                    return false;

                hasDigit = true;
                result = (result * 10f) + (digit - (byte)'0');
            }

            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                for (; index < bytes.Length; index++)
                {
                    byte digit = bytes[index];
                    if (digit < (byte)'0' || digit > (byte)'9')
                        return false;

                    hasDigit = true;
                    result += (digit - (byte)'0') * place;
                    place *= 0.1f;
                }
            }

            value = result * sign;
            return hasDigit;
        }
    }
}
