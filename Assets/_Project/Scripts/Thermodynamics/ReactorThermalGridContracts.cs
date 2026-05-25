using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Thermodynamics
{
    public static class BaseReactorThermalBufferIds
    {
        public const BufferID States = (BufferID)73642;
        public const BufferID Tuning = (BufferID)73643;
        public const BufferID PowerLedger = (BufferID)73644;
        public const BufferID TelemetryRing = (BufferID)73645;
        public const BufferID TelemetryCursor = (BufferID)73646;
        public const BufferID Visuals = (BufferID)73647;
        public const BufferID DumpLatch = (BufferID)73648;
        public const BufferID Profiles = (BufferID)73649;
        public const BufferID ProfileCount = (BufferID)73650;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseReactorStateDTO
    {
        public const uint FlagActive = 1u << 0;
        public const uint FlagMock = 1u << 1;
        public const uint FlagMeltdown = 1u << 2;
        public const uint FlagScrammed = 1u << 3;
        public const uint FlagNoCoolant = 1u << 4;
        public const uint FlagAtomicAbort = 1u << 5;
        public const uint FlagSignalOverflow = 1u << 29;
        public const uint FlagNonFinite = 1u << 30;

        [FieldOffset(0)] public uint PowerNodeHashID;
        [FieldOffset(4)] public uint FluidRoomHashID;
        [FieldOffset(8)] public float CoreTemperatureCelsius;
        [FieldOffset(12)] public float FuelRemainingScalar;
        [FieldOffset(16)] public float ControlRodInsertion01;
        [FieldOffset(20)] public uint ReactorFlags;
        [FieldOffset(24)] private uint _pad0;
        [FieldOffset(28)] private uint _pad1;
        [FieldOffset(32)] private uint _pad2;
        [FieldOffset(36)] private uint _pad3;
        [FieldOffset(40)] private uint _pad4;
        [FieldOffset(44)] private uint _pad5;
        [FieldOffset(48)] private uint _pad6;
        [FieldOffset(52)] private uint _pad7;
        [FieldOffset(56)] private uint _pad8;
        [FieldOffset(60)] private uint _pad9;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ReactorStateDTO
    {
        public const uint FlagActive = 1u << 0;
        public const uint FlagMock = 1u << 1;
        public const uint FlagMeltdown = 1u << 2;
        public const uint FlagSignalOverflow = 1u << 29;
        public const uint FlagNonFinite = 1u << 30;
        public const uint FlagOutOfGrid = 1u << 31;

        [FieldOffset(0)] public float CurrentCoreTempCelsius;
        [FieldOffset(4)] public float TargetPowerOutputMW;
        [FieldOffset(8)] public float ThermalDissipationRate;
        [FieldOffset(12)] public uint ReactorHashID;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private uint _pad1;
        [FieldOffset(28)] private uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct NuclearReactorThermalTuningDTO
    {
        [FieldOffset(0)] public float BaseFissionHeatJoulesPerSecond;
        [FieldOffset(4)] public float CoreHeatCapacityJoulesPerCelsius;
        [FieldOffset(8)] public float TurbineThermalDrawWatts;
        [FieldOffset(12)] public float LatentHeatJoulesPerLiter;
        [FieldOffset(16)] public float AmbientCoolantTempCelsius;
        [FieldOffset(20)] public float DryCoolantTempCelsius;
        [FieldOffset(24)] public float MeltdownCoreTempCelsius;
        [FieldOffset(28)] public float SafeCoreTempCelsius;
        [FieldOffset(32)] public float MaxBoilOffLitersPerSecond;
        [FieldOffset(36)] public float RadiationIntensityBase;
        [FieldOffset(40)] public float RadiationRadiusMeters;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public float MinTickIntervalSeconds;
        [FieldOffset(52)] public float MaxTickIntervalSeconds;
        [FieldOffset(56)] public int MockRunawayCount;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint SourceHash;
        [FieldOffset(68)] public uint DamageTypeHash;
        [FieldOffset(72)] public int MaxReactors;
        [FieldOffset(76)] public float ThermalLeakToGrid01;
        [FieldOffset(80)] public float CoolantLitersForNominalColdSink;
        [FieldOffset(84)] public uint Flags;
        [FieldOffset(88)] public float VisualOverkillScalar;
        [FieldOffset(92)] public float FuelBurnPerMegawattSecond;
        [FieldOffset(96)] public uint ProfileHash;
        [FieldOffset(100)] private uint _pad0;
        [FieldOffset(104)] private ulong _pad1;
        [FieldOffset(112)] private ulong _pad2;
        [FieldOffset(120)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReactorKinematicStateDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 LinearVelocity;
        [FieldOffset(36)] public uint ReactorHashID;
        [FieldOffset(40)] public uint EntityHashID;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct ReactorThermalTuningDTO
    {
        [FieldOffset(0)] public float BaseDissipationRate;
        [FieldOffset(4)] public float ForcedConvectionMultiplier;
        [FieldOffset(8)] public float MaxConvectionMultiplier;
        [FieldOffset(12)] public float CoreHeatCapacityJoulesPerCelsius;
        [FieldOffset(16)] public float WaterDensityKgPerCubicMeter;
        [FieldOffset(20)] public float WaterHeatCapacityJoulesPerKgC;
        [FieldOffset(24)] public float SafeCoreTempCelsius;
        [FieldOffset(28)] public float MeltdownCoreTempCelsius;
        [FieldOffset(32)] public float GridTemperatureClampCelsius;
        [FieldOffset(36)] public float HeatShimmerMinJoules;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public int MockReactorCount;
        [FieldOffset(48)] public uint ThermalSignalStrideFrames;
        [FieldOffset(52)] public uint MeltdownSignalStrideFrames;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint SourceHash;
        [FieldOffset(68)] public uint DamageTypeHash;
        [FieldOffset(72)] public int MaxReactors;
        [FieldOffset(76)] public float MockPowerMW;
        [FieldOffset(80)] public float MockCoreTempCelsius;
        [FieldOffset(84)] public float MockThermalDissipationRate;
        [FieldOffset(88)] public float VisualOverkillScalar;
        [FieldOffset(92)] public float CellConvectionGain;
        [FieldOffset(96)] public uint ProfileHash;
        [FieldOffset(100)] private uint _pad0;
        [FieldOffset(104)] private ulong _pad1;
        [FieldOffset(112)] private ulong _pad2;
        [FieldOffset(120)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReactorThermalScratchDTO
    {
        [FieldOffset(0)] public float JoulesInjected;
        [FieldOffset(4)] public float CoreCoolingCelsius;
        [FieldOffset(8)] public float CoreTempCelsius;
        [FieldOffset(12)] public float SpeedMetersPerSecond;
        [FieldOffset(16)] public float ConvectiveMultiplier;
        [FieldOffset(20)] public uint CenterCellIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint ReactorHashID;
        [FieldOffset(32)] public uint CellWrites;
        [FieldOffset(36)] public uint ThermalSignalCount;
        [FieldOffset(40)] public uint DamageSignalCount;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct ReactorThermalTelemetryEntry
    {
        [FieldOffset(0)] public double3 HotReactorAup;
        [FieldOffset(24)] public float TotalJoulesInjected;
        [FieldOffset(28)] public float AverageCoreTempCelsius;
        [FieldOffset(32)] public float MaxCoreTempCelsius;
        [FieldOffset(36)] public float MaxSpeedMetersPerSecond;
        [FieldOffset(40)] public float LastInjectionMicroseconds;
        [FieldOffset(44)] public uint ActiveReactorCount;
        [FieldOffset(48)] public uint MeltdownCount;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint StateHash;
        [FieldOffset(64)] public uint HotCellHash;
        [FieldOffset(68)] public uint InjectionCellWrites;
        [FieldOffset(72)] public uint NonFiniteCount;
        [FieldOffset(76)] public uint ThermalSignalCount;
        [FieldOffset(80)] public uint DamageSignalCount;
        [FieldOffset(84)] public uint RingIndex;
        [FieldOffset(88)] public uint HotReactorHashID;
        [FieldOffset(92)] public uint HotEntityHashID;
        [FieldOffset(96)] private ulong _pad0;
        [FieldOffset(104)] private ulong _pad1;
        [FieldOffset(112)] private ulong _pad2;
        [FieldOffset(120)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ReactorThermalProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float BaseDissipationRate;
        [FieldOffset(8)] public float CoreHeatCapacityJoulesPerCelsius;
        [FieldOffset(12)] public float SafeCoreTempCelsius;
        [FieldOffset(16)] public float MeltdownCoreTempCelsius;
        [FieldOffset(20)] public float NominalPowerMW;
        [FieldOffset(24)] public float ForcedConvectionMultiplier;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct NuclearReactorProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float BaseFissionHeatJoulesPerSecond;
        [FieldOffset(8)] public float CoreHeatCapacityJoulesPerCelsius;
        [FieldOffset(12)] public float TurbineThermalDrawWatts;
        [FieldOffset(16)] public float LatentHeatJoulesPerLiter;
        [FieldOffset(20)] public float SafeCoreTempCelsius;
        [FieldOffset(24)] public float MeltdownCoreTempCelsius;
        [FieldOffset(28)] public float RadiationRadiusMeters;
        [FieldOffset(32)] public float MaxBoilOffLitersPerSecond;
        [FieldOffset(36)] public float FuelBurnPerMegawattSecond;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] private uint _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ReactorPowerInjectionDTO
    {
        public const uint FlagMeltdownEnteredThisTick = 1u << 24;
        public const uint FlagMeltdownSignalTick = 1u << 25;
        public const uint FlagCoolantBoiledThisTick = 1u << 26;
        public const uint FlagSignalOverflow = 1u << 27;

        [FieldOffset(0)] public uint PowerNodeHashID;
        [FieldOffset(4)] public float GeneratedWatts;
        [FieldOffset(8)] public float GeneratedWattSeconds;
        [FieldOffset(12)] public float CarnotEfficiency01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint ReactorHashID;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public float BoiledLiters;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReactorThermalVisualDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float CoreTemperatureCelsius;
        [FieldOffset(28)] public float GeneratedMegawatts;
        [FieldOffset(32)] public float CarnotEfficiency01;
        [FieldOffset(36)] public float BoiledLiters;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint ReactorHashID;
        [FieldOffset(48)] public float ControlRodInsertion01;
        [FieldOffset(52)] public float FuelRemainingScalar;
        [FieldOffset(56)] public uint PowerNodeHashID;
        [FieldOffset(60)] public uint FluidRoomHashID;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct NuclearReactorTelemetryEntry
    {
        [FieldOffset(0)] public double3 HotReactorAup;
        [FieldOffset(24)] public float TotalGeneratedWatts;
        [FieldOffset(28)] public float TotalBoiledLiters;
        [FieldOffset(32)] public float AverageCoreTempCelsius;
        [FieldOffset(36)] public float MaxCoreTempCelsius;
        [FieldOffset(40)] public float LastExecutionMicroseconds;
        [FieldOffset(44)] public float AverageCarnotEfficiency01;
        [FieldOffset(48)] public uint ActiveReactorCount;
        [FieldOffset(52)] public uint MeltdownCount;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint StateHash;
        [FieldOffset(68)] public uint PowerNodeHashID;
        [FieldOffset(72)] public uint FluidRoomHashID;
        [FieldOffset(76)] public uint RadiationSignalCount;
        [FieldOffset(80)] public uint BaseCompromiseSignalCount;
        [FieldOffset(84)] public uint RingIndex;
        [FieldOffset(88)] public uint NonFiniteCount;
        [FieldOffset(92)] public uint AtomicAbortCount;
        [FieldOffset(96)] private ulong _pad0;
        [FieldOffset(104)] private ulong _pad1;
        [FieldOffset(112)] private ulong _pad2;
        [FieldOffset(120)] private ulong _pad3;
    }

    public static class ReactorThermalLayoutValidator
    {
        public static bool ValidateBaseReactorLayout()
        {
            return UnsafeUtility.SizeOf<BaseReactorStateDTO>() == 64 &&
                   Marshal.OffsetOf<BaseReactorStateDTO>(nameof(BaseReactorStateDTO.PowerNodeHashID)).ToInt32() == 0 &&
                   Marshal.OffsetOf<BaseReactorStateDTO>(nameof(BaseReactorStateDTO.FluidRoomHashID)).ToInt32() == 4 &&
                   Marshal.OffsetOf<BaseReactorStateDTO>(nameof(BaseReactorStateDTO.CoreTemperatureCelsius)).ToInt32() == 8 &&
                   Marshal.OffsetOf<BaseReactorStateDTO>(nameof(BaseReactorStateDTO.FuelRemainingScalar)).ToInt32() == 12 &&
                   Marshal.OffsetOf<BaseReactorStateDTO>(nameof(BaseReactorStateDTO.ControlRodInsertion01)).ToInt32() == 16 &&
                   Marshal.OffsetOf<BaseReactorStateDTO>(nameof(BaseReactorStateDTO.ReactorFlags)).ToInt32() == 20;
        }

        public static bool ValidateReactorStateLayout()
        {
            return UnsafeUtility.SizeOf<ReactorStateDTO>() == 32 &&
                   Marshal.OffsetOf<ReactorStateDTO>(nameof(ReactorStateDTO.CurrentCoreTempCelsius)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ReactorStateDTO>(nameof(ReactorStateDTO.TargetPowerOutputMW)).ToInt32() == 4 &&
                   Marshal.OffsetOf<ReactorStateDTO>(nameof(ReactorStateDTO.ThermalDissipationRate)).ToInt32() == 8 &&
                   Marshal.OffsetOf<ReactorStateDTO>(nameof(ReactorStateDTO.ReactorHashID)).ToInt32() == 12 &&
                   Marshal.OffsetOf<ReactorStateDTO>(nameof(ReactorStateDTO.Flags)).ToInt32() == 16;
        }

        public static bool ValidateSupportLayouts()
        {
            return UnsafeUtility.SizeOf<ReactorKinematicStateDTO>() == 64 &&
                   Marshal.OffsetOf<ReactorKinematicStateDTO>(nameof(ReactorKinematicStateDTO.Aup)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ReactorKinematicStateDTO>(nameof(ReactorKinematicStateDTO.LinearVelocity)).ToInt32() == 24 &&
                   Marshal.OffsetOf<ReactorKinematicStateDTO>(nameof(ReactorKinematicStateDTO.ReactorHashID)).ToInt32() == 36 &&
                   Marshal.OffsetOf<ReactorKinematicStateDTO>(nameof(ReactorKinematicStateDTO.EntityHashID)).ToInt32() == 40 &&
                   Marshal.OffsetOf<ReactorKinematicStateDTO>(nameof(ReactorKinematicStateDTO.Flags)).ToInt32() == 44 &&
                   UnsafeUtility.SizeOf<ReactorThermalTuningDTO>() == 128 &&
                   Marshal.OffsetOf<ReactorThermalTuningDTO>(nameof(ReactorThermalTuningDTO.BaseDissipationRate)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ReactorThermalTuningDTO>(nameof(ReactorThermalTuningDTO.GlobalQualityWeight)).ToInt32() == 40 &&
                   Marshal.OffsetOf<ReactorThermalTuningDTO>(nameof(ReactorThermalTuningDTO.SourceHash)).ToInt32() == 64 &&
                   Marshal.OffsetOf<ReactorThermalTuningDTO>(nameof(ReactorThermalTuningDTO.ProfileHash)).ToInt32() == 96 &&
                   UnsafeUtility.SizeOf<ReactorThermalScratchDTO>() == 64 &&
                   Marshal.OffsetOf<ReactorThermalScratchDTO>(nameof(ReactorThermalScratchDTO.JoulesInjected)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ReactorThermalScratchDTO>(nameof(ReactorThermalScratchDTO.CenterCellIndex)).ToInt32() == 20 &&
                   Marshal.OffsetOf<ReactorThermalScratchDTO>(nameof(ReactorThermalScratchDTO.StateHash)).ToInt32() == 44 &&
                   UnsafeUtility.SizeOf<ReactorThermalTelemetryEntry>() == 128 &&
                   Marshal.OffsetOf<ReactorThermalTelemetryEntry>(nameof(ReactorThermalTelemetryEntry.HotReactorAup)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ReactorThermalTelemetryEntry>(nameof(ReactorThermalTelemetryEntry.TotalJoulesInjected)).ToInt32() == 24 &&
                   Marshal.OffsetOf<ReactorThermalTelemetryEntry>(nameof(ReactorThermalTelemetryEntry.LastInjectionMicroseconds)).ToInt32() == 40 &&
                   Marshal.OffsetOf<ReactorThermalTelemetryEntry>(nameof(ReactorThermalTelemetryEntry.RingIndex)).ToInt32() == 84 &&
                   Marshal.OffsetOf<ReactorThermalTelemetryEntry>(nameof(ReactorThermalTelemetryEntry.HotReactorHashID)).ToInt32() == 88 &&
                   Marshal.OffsetOf<ReactorThermalTelemetryEntry>(nameof(ReactorThermalTelemetryEntry.HotEntityHashID)).ToInt32() == 92 &&
                   UnsafeUtility.SizeOf<ReactorThermalProfileDTO>() == 32 &&
                   Marshal.OffsetOf<ReactorThermalProfileDTO>(nameof(ReactorThermalProfileDTO.ProfileHash)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ReactorThermalProfileDTO>(nameof(ReactorThermalProfileDTO.ForcedConvectionMultiplier)).ToInt32() == 24 &&
                   Marshal.OffsetOf<ReactorThermalProfileDTO>(nameof(ReactorThermalProfileDTO.Flags)).ToInt32() == 28 &&
                   UnsafeUtility.SizeOf<NuclearReactorThermalTuningDTO>() == 128 &&
                   Marshal.OffsetOf<NuclearReactorThermalTuningDTO>(nameof(NuclearReactorThermalTuningDTO.BaseFissionHeatJoulesPerSecond)).ToInt32() == 0 &&
                   Marshal.OffsetOf<NuclearReactorThermalTuningDTO>(nameof(NuclearReactorThermalTuningDTO.GlobalQualityWeight)).ToInt32() == 44 &&
                   Marshal.OffsetOf<NuclearReactorThermalTuningDTO>(nameof(NuclearReactorThermalTuningDTO.SourceHash)).ToInt32() == 64 &&
                   Marshal.OffsetOf<NuclearReactorThermalTuningDTO>(nameof(NuclearReactorThermalTuningDTO.ProfileHash)).ToInt32() == 96 &&
                   UnsafeUtility.SizeOf<NuclearReactorProfileDTO>() == 64 &&
                   Marshal.OffsetOf<NuclearReactorProfileDTO>(nameof(NuclearReactorProfileDTO.ProfileHash)).ToInt32() == 0 &&
                   Marshal.OffsetOf<NuclearReactorProfileDTO>(nameof(NuclearReactorProfileDTO.FuelBurnPerMegawattSecond)).ToInt32() == 36 &&
                   UnsafeUtility.SizeOf<ReactorPowerInjectionDTO>() == 32 &&
                   Marshal.OffsetOf<ReactorPowerInjectionDTO>(nameof(ReactorPowerInjectionDTO.GeneratedWatts)).ToInt32() == 4 &&
                   UnsafeUtility.SizeOf<ReactorThermalVisualDTO>() == 64 &&
                   Marshal.OffsetOf<ReactorThermalVisualDTO>(nameof(ReactorThermalVisualDTO.Aup)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ReactorThermalVisualDTO>(nameof(ReactorThermalVisualDTO.GeneratedMegawatts)).ToInt32() == 28 &&
                   UnsafeUtility.SizeOf<NuclearReactorTelemetryEntry>() == 128 &&
                   Marshal.OffsetOf<NuclearReactorTelemetryEntry>(nameof(NuclearReactorTelemetryEntry.HotReactorAup)).ToInt32() == 0 &&
                   Marshal.OffsetOf<NuclearReactorTelemetryEntry>(nameof(NuclearReactorTelemetryEntry.LastExecutionMicroseconds)).ToInt32() == 40 &&
                   Marshal.OffsetOf<NuclearReactorTelemetryEntry>(nameof(NuclearReactorTelemetryEntry.RingIndex)).ToInt32() == 84;
        }
    }

    #if UNITY_EDITOR
    public static unsafe class NuclearReactorProfileCsvParser
    {
        public static int Parse(ReadOnlySpan<byte> bytes, NuclearReactorProfileDTO* profiles, int capacity)
        {
            if (profiles == null || capacity <= 0)
                return 0;

            int count = 0;
            int lineStart = 0;
            bool skippedHeader = false;
            for (int i = 0; i <= bytes.Length; i++)
            {
                bool end = i == bytes.Length || bytes[i] == (byte)'\n' || bytes[i] == (byte)'\r';
                if (!end)
                    continue;

                ReadOnlySpan<byte> line = Trim(bytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0)
                    continue;

                if (!skippedHeader)
                {
                    skippedHeader = true;
                    if (StartsWithAscii(line, "name") || StartsWithAscii(line, "profile"))
                        continue;
                }

                if (TryParseProfile(line, out NuclearReactorProfileDTO profile))
                {
                    profiles[count++] = profile;
                    if (count >= capacity)
                        break;
                }
            }

            return count;
        }

        private static bool TryParseProfile(ReadOnlySpan<byte> line, out NuclearReactorProfileDTO profile)
        {
            profile = default;
            ReadOnlySpan<byte> name = Trim(Next(ref line));
            if (name.Length == 0)
                return false;

            profile.ProfileHash = Fnv1A(name);
            profile.BaseFissionHeatJoulesPerSecond = ReadFloat(ref line, 42000000f);
            profile.CoreHeatCapacityJoulesPerCelsius = ReadFloat(ref line, 1250000f);
            profile.TurbineThermalDrawWatts = ReadFloat(ref line, 30000000f);
            profile.LatentHeatJoulesPerLiter = ReadFloat(ref line, 2256000f);
            profile.SafeCoreTempCelsius = ReadFloat(ref line, 1100f);
            profile.MeltdownCoreTempCelsius = ReadFloat(ref line, 2500f);
            profile.RadiationRadiusMeters = ReadFloat(ref line, 120f);
            profile.MaxBoilOffLitersPerSecond = ReadFloat(ref line, 4200f);
            profile.FuelBurnPerMegawattSecond = ReadFloat(ref line, 0.00000025f);
            profile.Flags = 0u;
            return profile.ProfileHash != 0u;
        }

        private static ReadOnlySpan<byte> Next(ref ReadOnlySpan<byte> line)
        {
            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                ReadOnlySpan<byte> value = line;
                line = ReadOnlySpan<byte>.Empty;
                return value;
            }

            ReadOnlySpan<byte> head = line.Slice(0, comma);
            line = line.Slice(comma + 1);
            return head;
        }

        private static float ReadFloat(ref ReadOnlySpan<byte> line, float fallback)
        {
            ReadOnlySpan<byte> token = Trim(Next(ref line));
            return TryParseFloat(token, out float value) ? value : fallback;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float whole = 0f;
            bool any = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                any = true;
                whole = whole * 10f + (token[index] - (byte)'0');
                index++;
            }

            float fraction = 0f;
            float scale = 1f;
            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    any = true;
                    scale *= 0.1f;
                    fraction += (token[index] - (byte)'0') * scale;
                    index++;
                }
            }

            if (!any)
                return false;

            value = (whole + fraction) * sign;
            return math.isfinite(value);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start < bytes.Length && IsSpace(bytes[start]))
                start++;
            while (end >= start && IsSpace(bytes[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : bytes.Slice(start, end - start + 1);
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool StartsWithAscii(ReadOnlySpan<byte> span, string ascii)
        {
            if (span.Length < ascii.Length)
                return false;

            for (int i = 0; i < ascii.Length; i++)
            {
                byte value = span[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                if (value != (byte)ascii[i])
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Fnv1A(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'a' && value <= (byte)'z')
                    value = (byte)(value - 32);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }
    }
    #endif

    #if UNITY_EDITOR
    public static unsafe class ReactorThermalProfileCsvParser
    {
        public static int Parse(ReadOnlySpan<byte> bytes, ReactorThermalProfileDTO* profiles, int capacity)
        {
            if (profiles == null || capacity <= 0)
                return 0;

            int count = 0;
            int lineStart = 0;
            bool skippedHeader = false;
            for (int i = 0; i <= bytes.Length; i++)
            {
                bool end = i == bytes.Length || bytes[i] == (byte)'\n' || bytes[i] == (byte)'\r';
                if (!end)
                    continue;

                ReadOnlySpan<byte> line = Trim(bytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0)
                    continue;

                if (!skippedHeader)
                {
                    skippedHeader = true;
                    if (StartsWithAscii(line, "name") || StartsWithAscii(line, "profile"))
                        continue;
                }

                if (TryParseProfile(line, out ReactorThermalProfileDTO profile))
                {
                    profiles[count++] = profile;
                    if (count >= capacity)
                        break;
                }
            }

            return count;
        }

        private static bool TryParseProfile(ReadOnlySpan<byte> line, out ReactorThermalProfileDTO profile)
        {
            profile = default;
            ReadOnlySpan<byte> name = Trim(Next(ref line));
            if (name.Length == 0)
                return false;

            profile.ProfileHash = Fnv1A(name);
            profile.BaseDissipationRate = ReadFloat(ref line, 0.085f);
            profile.CoreHeatCapacityJoulesPerCelsius = ReadFloat(ref line, 1250000f);
            profile.SafeCoreTempCelsius = ReadFloat(ref line, 760f);
            profile.MeltdownCoreTempCelsius = ReadFloat(ref line, 1850f);
            profile.NominalPowerMW = ReadFloat(ref line, 14f);
            profile.ForcedConvectionMultiplier = ReadFloat(ref line, 0.08f);
            profile.Flags = 0u;
            return profile.ProfileHash != 0u;
        }

        private static ReadOnlySpan<byte> Next(ref ReadOnlySpan<byte> line)
        {
            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                ReadOnlySpan<byte> value = line;
                line = ReadOnlySpan<byte>.Empty;
                return value;
            }

            ReadOnlySpan<byte> head = line.Slice(0, comma);
            line = line.Slice(comma + 1);
            return head;
        }

        private static float ReadFloat(ref ReadOnlySpan<byte> line, float fallback)
        {
            ReadOnlySpan<byte> token = Trim(Next(ref line));
            return TryParseFloat(token, out float value) ? value : fallback;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float whole = 0f;
            bool any = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                any = true;
                whole = whole * 10f + (token[index] - (byte)'0');
                index++;
            }

            float fraction = 0f;
            float scale = 1f;
            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    any = true;
                    scale *= 0.1f;
                    fraction += (token[index] - (byte)'0') * scale;
                    index++;
                }
            }

            if (!any)
                return false;

            value = (whole + fraction) * sign;
            return math.isfinite(value);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start < bytes.Length && IsSpace(bytes[start]))
                start++;
            while (end >= start && IsSpace(bytes[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : bytes.Slice(start, end - start + 1);
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool StartsWithAscii(ReadOnlySpan<byte> span, string ascii)
        {
            if (span.Length < ascii.Length)
                return false;

            for (int i = 0; i < ascii.Length; i++)
            {
                byte value = span[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                if (value != (byte)ascii[i])
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Fnv1A(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'a' && value <= (byte)'z')
                    value = (byte)(value - 32);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }
    }
    #endif
}
