using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using System.Reflection;
#endif
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.Vehicles
{
    public static class SubmarineBallastConstants
    {
        public const int TankCount = 4;
        public const int TelemetryCapacity = 300;
        public const int CsvScratchBytes = 32768;
        public const int ProfileCapacity = 64;
        public const int TankBytes = 32;
        public const int CommandBytes = 32;
        public const int FluidSampleBytes = 160;
        public const int ForcePacketBytes = 128;
        public const int TelemetryBytes = 64;
        public const int TuningBytes = 64;
        public const int ProfileBytes = 64;
        public const float Gravity = 9.80665f;
        public const float LitersPerCubicMeter = 1000f;
        public const float CubicMetersPerLiter = 0.001f;
        public const float DefaultWaterDensityKgPerM3 = 1025f;
        public const float AirDensityKgPerM3AtOneAtm = 1.225f;
        public const float AtmosphericPressureAtm = 1f;
        public const float SeaWaterAtmPerMeter = 0.1005f;
        public const float Epsilon = 0.0001f;
        public const float FaultMicros = 500f;
        public const float SampleBudgetHysteresisSeconds = 2.5f;
        public const uint SourceHash = 0x53333333u;
        public const uint CommandFlagFlood = 1u << 0;
        public const uint CommandFlagBlow = 1u << 1;
        public const uint CommandFlagPumpDenied = 1u << 2;
        public const uint TankFlagFlooding = 1u << 0;
        public const uint TankFlagBlowing = 1u << 1;
        public const uint TankFlagInitialized = 1u << 2;
        public const uint TankFlagPressureBlocked = 1u << 3;
        public const uint TankFlagSignalDrop = 1u << 4;
        public const uint TankFlagNonFinite = 1u << 31;
        public const uint SampleFlagMockFluid = 1u << 0;
        public const uint ForceFlagValid = 1u << 0;
        public const uint ForceFlagPressureBlocked = 1u << 1;
        public const uint ForceFlagMockFluid = 1u << 2;
        public const uint ForceFlagTimingProxy = 1u << 3;
        public const uint ForceFlagSignalDrop = 1u << 4;
        public const uint ForceFlagNonFinite = 1u << 31;
        public const byte MovementModePneumaticHiss = 13;
    }

    public static class SubmarineBallastBufferIds
    {
        public const BufferID Tanks = BufferID.Shinobu333BallastTanks;
        public const BufferID Commands = BufferID.Shinobu333BallastCommands;
        public const BufferID FluidSamples = BufferID.Shinobu333BallastFluidSamples;
        public const BufferID ForcePackets = BufferID.Shinobu333BallastForcePackets;
        public const BufferID TelemetryRing = BufferID.Shinobu333BallastTelemetryRing;
        public const BufferID Profiles = BufferID.Shinobu333BallastProfiles;
        public const BufferID Tuning = BufferID.Shinobu333BallastTuning;
        public const BufferID CsvScratch = BufferID.Shinobu333BallastCsvScratch;
    }

    [StructLayout(LayoutKind.Explicit, Size = SubmarineBallastConstants.TankBytes)]
    public struct BallastTankDTO
    {
        [FieldOffset(0)] public float TankVolumeLiters;
        [FieldOffset(4)] public float CurrentWaterLiters;
        [FieldOffset(8)] public float CompressedAirPressureATM;
        [FieldOffset(12)] public uint InputStateFlags;
        [FieldOffset(16)] public float PumpRateLitersPerSecond;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private uint _pad1;
        [FieldOffset(28)] private uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = SubmarineBallastConstants.CommandBytes)]
    public struct BallastTankCommandDTO
    {
        [FieldOffset(0)] public float TargetWaterLiters;
        [FieldOffset(4)] public float FloodRateLitersPerSecond;
        [FieldOffset(8)] public float BlowRateLitersPerSecond;
        [FieldOffset(12)] public float CompressedAirPressureATM;
        [FieldOffset(16)] public uint CommandFlags;
        [FieldOffset(20)] public uint TargetEntityHash;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public int TankIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = SubmarineBallastConstants.FluidSampleBytes)]
    public struct SubmarineBallastFluidSampleDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition HullPositionAup;
        [FieldOffset(48)] public double3 HullAup;
        [FieldOffset(72)] public double3 OceanSurfaceAup;
        [FieldOffset(96)] public float3 HullVelocity;
        [FieldOffset(108)] public float HullHeightMeters;
        [FieldOffset(112)] public float HullVolumeCubicMeters;
        [FieldOffset(116)] public float FluidDensityKgPerM3;
        [FieldOffset(120)] public float AmbientPressureATM;
        [FieldOffset(124)] public float GlobalQualityWeight;
        [FieldOffset(128)] public float SimulationDeltaTime;
        [FieldOffset(132)] public uint TargetEntityHash;
        [FieldOffset(136)] public uint Frame;
        [FieldOffset(140)] public uint Flags;
        [FieldOffset(144)] public float SurfaceSwellMeters;
        [FieldOffset(148)] public int ActiveSampleBudget;
        [FieldOffset(152)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = SubmarineBallastConstants.ForcePacketBytes)]
    public struct SubmarineBallastForcePacketDTO
    {
        [FieldOffset(0)] public double3 HullAup;
        [FieldOffset(24)] public float3 NetForce;
        [FieldOffset(36)] public float3 BuoyantForce;
        [FieldOffset(48)] public float3 BallastGravityForce;
        [FieldOffset(60)] public float SubmergedRatio;
        [FieldOffset(64)] public float TotalWaterLiters;
        [FieldOffset(68)] public float TotalCompressedAirMassKg;
        [FieldOffset(72)] public float AmbientPressureATM;
        [FieldOffset(76)] public float FluidDensityKgPerM3;
        [FieldOffset(80)] public float DisplacedVolumeCubicMeters;
        [FieldOffset(84)] public uint TargetEntityHash;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public int ActiveSamples;
        [FieldOffset(96)] public uint Frame;
        [FieldOffset(100)] public float ComputeMicros;
        [FieldOffset(104)] public float3 LocalizedSurfaceDelta;
        [FieldOffset(116)] public uint StateHash;
        [FieldOffset(120)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SubmarineBallastConstants.TelemetryBytes)]
    public struct SubmarineBallastTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint StateHash;
        [FieldOffset(12)] public float NetForceY;
        [FieldOffset(16)] public float BuoyantForceY;
        [FieldOffset(20)] public float BallastGravityForceY;
        [FieldOffset(24)] public float WaterLiters;
        [FieldOffset(28)] public float CompressedAirMassKg;
        [FieldOffset(32)] public float AmbientPressureATM;
        [FieldOffset(36)] public float DisplacedVolumeCubicMeters;
        [FieldOffset(40)] public float SubmergedRatio;
        [FieldOffset(44)] public float ComputeMicros;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public int ActiveSamples;
        [FieldOffset(56)] public uint TargetEntityHash;
        [FieldOffset(60)] public uint RingCursor;
    }

    [StructLayout(LayoutKind.Explicit, Size = SubmarineBallastConstants.TuningBytes)]
    public struct SubmarineBallastTuningDTO
    {
        [FieldOffset(0)] public float HullVolumeCubicMeters;
        [FieldOffset(4)] public float HullHeightMeters;
        [FieldOffset(8)] public float MaxTankLiters;
        [FieldOffset(12)] public float PumpRateLitersPerSecond;
        [FieldOffset(16)] public float BlowRateLitersPerSecond;
        [FieldOffset(20)] public float AirBankPressureATM;
        [FieldOffset(24)] public float FluidDensityKgPerM3;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint SourceHash;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float LastNetForceY;
        [FieldOffset(48)] public float LastWaterLiters;
        [FieldOffset(52)] public float LastAmbientPressureATM;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SubmarineBallastConstants.ProfileBytes)]
    public struct SubmarineBallastProfileDTO
    {
        [FieldOffset(0)] public uint VehicleHash;
        [FieldOffset(4)] public float HullVolumeCubicMeters;
        [FieldOffset(8)] public float HullHeightMeters;
        [FieldOffset(12)] public float TankVolumeLiters;
        [FieldOffset(16)] public float PumpRateLitersPerSecond;
        [FieldOffset(20)] public float BlowRateLitersPerSecond;
        [FieldOffset(24)] public float AirBankPressureATM;
        [FieldOffset(28)] public float FluidDensityKgPerM3;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint RowIndex;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    public static class SubmarineBallastLayout
    {
        private static readonly bool s_valid = ValidateInternal();

        public static bool Validate()
        {
            return s_valid;
        }

        public static bool ValidateTankLayout()
        {
#if UNITY_EDITOR
            return UnsafeUtility.SizeOf<BallastTankDTO>() == SubmarineBallastConstants.TankBytes &&
                   OffsetOf<BallastTankDTO>(nameof(BallastTankDTO.TankVolumeLiters)) == 0 &&
                   OffsetOf<BallastTankDTO>(nameof(BallastTankDTO.CurrentWaterLiters)) == 4 &&
                   OffsetOf<BallastTankDTO>(nameof(BallastTankDTO.CompressedAirPressureATM)) == 8 &&
                   OffsetOf<BallastTankDTO>(nameof(BallastTankDTO.InputStateFlags)) == 12 &&
                   OffsetOf<BallastTankDTO>(nameof(BallastTankDTO.PumpRateLitersPerSecond)) == 16 &&
                   OffsetOf<BallastTankDTO>("_pad0") == 20 &&
                   OffsetOf<BallastTankDTO>("_pad1") == 24 &&
                   OffsetOf<BallastTankDTO>("_pad2") == 28;
#else
            return UnsafeUtility.SizeOf<BallastTankDTO>() == SubmarineBallastConstants.TankBytes;
#endif
        }

        private static bool ValidateInternal()
        {
            return ValidateTankLayout() &&
                   UnsafeUtility.SizeOf<BallastTankCommandDTO>() == SubmarineBallastConstants.CommandBytes &&
                   UnsafeUtility.SizeOf<SubmarineBallastFluidSampleDTO>() == SubmarineBallastConstants.FluidSampleBytes &&
                   UnsafeUtility.SizeOf<SubmarineBallastForcePacketDTO>() == SubmarineBallastConstants.ForcePacketBytes &&
                   UnsafeUtility.SizeOf<SubmarineBallastTelemetryEntry>() == SubmarineBallastConstants.TelemetryBytes &&
                   UnsafeUtility.SizeOf<SubmarineBallastTuningDTO>() == SubmarineBallastConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<SubmarineBallastProfileDTO>() == SubmarineBallastConstants.ProfileBytes
#if UNITY_EDITOR
                    &&
                    OffsetOf<SubmarineBallastFluidSampleDTO>(nameof(SubmarineBallastFluidSampleDTO.ActiveSampleBudget)) == 148 &&
                    OffsetOf<SubmarineBallastForcePacketDTO>(nameof(SubmarineBallastForcePacketDTO.HullAup)) == 0 &&
                    OffsetOf<SubmarineBallastForcePacketDTO>(nameof(SubmarineBallastForcePacketDTO.NetForce)) == 24 &&
                    OffsetOf<SubmarineBallastForcePacketDTO>(nameof(SubmarineBallastForcePacketDTO.TargetEntityHash)) == 84 &&
                    OffsetOf<SubmarineBallastTelemetryEntry>(nameof(SubmarineBallastTelemetryEntry.ComputeMicros)) == 44 &&
                    OffsetOf<SubmarineBallastTuningDTO>(nameof(SubmarineBallastTuningDTO.SourceHash)) == 32
#endif
                    ;
        }

#if UNITY_EDITOR
        private static int OffsetOf<T>(string fieldName)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif
    }

    #if UNITY_EDITOR
    public static class SubmarineBallastCsvParser
    {
        public static int ParseProfiles(ReadOnlySpan<byte> csv, NativeArray<SubmarineBallastProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0 || csv.Length <= 0)
                return 0;

            int row = 0;
            int index = 0;
            while (index < csv.Length && row < profiles.Length)
            {
                int lineStart = index;
                while (index < csv.Length && csv[index] != (byte)'\n' && csv[index] != (byte)'\r')
                    index++;

                ReadOnlySpan<byte> line = csv.Slice(lineStart, index - lineStart);
                while (index < csv.Length && (csv[index] == (byte)'\n' || csv[index] == (byte)'\r'))
                    index++;

                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (TryParseProfile(line, (uint)row, out SubmarineBallastProfileDTO profile))
                    profiles[row++] = profile;
            }

            return row;
        }

        private static bool TryParseProfile(ReadOnlySpan<byte> line, uint rowIndex, out SubmarineBallastProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            if (!ReadField(line, ref cursor, out ReadOnlySpan<byte> name) || name.Length == 0)
                return false;

            profile.VehicleHash = Fnv1A(name);
            if (!ReadFloatField(line, ref cursor, out profile.HullVolumeCubicMeters))
                return false;
            if (!ReadFloatField(line, ref cursor, out profile.HullHeightMeters))
                return false;
            if (!ReadFloatField(line, ref cursor, out profile.TankVolumeLiters))
                return false;
            if (!ReadFloatField(line, ref cursor, out profile.PumpRateLitersPerSecond))
                return false;
            if (!ReadFloatField(line, ref cursor, out profile.BlowRateLitersPerSecond))
                return false;
            if (!ReadFloatField(line, ref cursor, out profile.AirBankPressureATM))
                return false;

            profile.FluidDensityKgPerM3 = SubmarineBallastConstants.DefaultWaterDensityKgPerM3;
            if (ReadFloatField(line, ref cursor, out float density) && density > 0f)
                profile.FluidDensityKgPerM3 = density;
            profile.Flags = 1u;
            profile.RowIndex = rowIndex;
            return profile.VehicleHash != 0u;
        }

        private static bool ReadFloatField(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            if (!ReadField(line, ref cursor, out ReadOnlySpan<byte> field))
                return false;

            value = ParseFloat(field);
            return math.isfinite(value);
        }

        private static bool ReadField(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> field)
        {
            field = default;
            if (cursor > line.Length)
                return false;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            while (start < end && (line[start] == (byte)' ' || line[start] == (byte)'\t'))
                start++;
            while (end > start && (line[end - 1] == (byte)' ' || line[end - 1] == (byte)'\t'))
                end--;

            field = line.Slice(start, end - start);
            return true;
        }

        private static float ParseFloat(ReadOnlySpan<byte> field)
        {
            if (field.Length == 0)
                return 0f;

            int index = 0;
            float sign = 1f;
            if (field[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (field[index] == (byte)'+')
            {
                index++;
            }

            double value = 0d;
            while (index < field.Length && field[index] >= (byte)'0' && field[index] <= (byte)'9')
            {
                value = (value * 10d) + (field[index] - (byte)'0');
                index++;
            }

            if (index < field.Length && field[index] == (byte)'.')
            {
                index++;
                double scale = 0.1d;
                while (index < field.Length && field[index] >= (byte)'0' && field[index] <= (byte)'9')
                {
                    value += (field[index] - (byte)'0') * scale;
                    scale *= 0.1d;
                    index++;
                }
            }

            return (float)(value * sign);
        }

        private static uint Fnv1A(ReadOnlySpan<byte> bytes)
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
    }
    #endif

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockFluidDisplacementJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SubmarineBallastFluidSampleDTO> FluidSamples;
        public uint Frame;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (!FluidSamples.IsCreated || (uint)index >= (uint)FluidSamples.Length)
                return;

            SubmarineBallastFluidSampleDTO sample = FluidSamples[index];
            float q = SaturateFinite(GlobalQualityWeight, 1f);
            uint phaseSeed = Frame + ((uint)index * 73u);
            float phase = (phaseSeed & 1023u) * (1f / 1024f);
            float triangle = 1f - math.abs((phase * 4f) - 2f);
            float swell = triangle * 10f;
            double baseDepth = math.max(0d, sample.OceanSurfaceAup.y - sample.HullAup.y);
            double depth = math.max(0d, baseDepth + swell);
            sample.SurfaceSwellMeters = swell;
            sample.OceanSurfaceAup.y = sample.HullAup.y + depth;
            sample.FluidDensityKgPerM3 = math.lerp(1015f, 1065f, q) + (triangle * 6f);
            sample.AmbientPressureATM = SubmarineBallastConstants.AtmosphericPressureAtm +
                                        ((float)depth * SubmarineBallastConstants.SeaWaterAtmPerMeter);
            sample.GlobalQualityWeight = q;
            sample.Flags |= SubmarineBallastConstants.SampleFlagMockFluid;
            FluidSamples[index] = sample;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SaturateFinite(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateBallastTanksJob : IJobParallelFor
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<BallastTankDTO> Tanks;
        [ReadOnly, NoAlias] public NativeArray<BallastTankCommandDTO> Commands;
        [ReadOnly, NoAlias] public NativeArray<SubmarineBallastFluidSampleDTO> FluidSamples;
        // Safety proof 1/3: SignalBus owns the underlying NativeQueue lifetime and frame capacity. This job receives
        // only the ParallelWriter facade after the lane is initialized by the owner, and it never stores the writer,
        // resizes the lane, reads from the lane, or opens SignalBus metadata from worker code.
        //
        // Safety proof 2/3: each Execute index owns one ballast tank row. Hiss emission is optional and sparse;
        // payload contents are value-only MovementAcousticSignal fields sanitized from the current sample/command.
        // The queue is the only shared write target, and NativeQueue.ParallelWriter is the approved concurrent path.
        //
        // Safety proof 3/3: if the SignalBus lane is missing, the owner prevents scheduling by initializing the lane
        // in cold registration. Failure degrades to no acoustic presentation, not gameplay truth corruption; tank
        // liters, pressure flags, and force packets remain Vault-owned deterministic rows.
        [NoAlias, NativeDisableContainerSafetyRestriction] public NativeQueue<MovementAcousticSignal>.ParallelWriter AcousticWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> AcousticWriterBudget;
        public float DeltaTime;
        public uint Frame;
        public byte EmitAcousticSignals;

        public void Execute(int index)
        {
            if (!Tanks.IsCreated ||
                !Commands.IsCreated ||
                (uint)index >= (uint)Tanks.Length ||
                (uint)index >= (uint)Commands.Length)
            {
                return;
            }

            BallastTankDTO* tanks = (BallastTankDTO*)Tanks.GetUnsafePtr();
            ref BallastTankDTO tank = ref UnsafeUtility.AsRef<BallastTankDTO>(tanks + index);
            BallastTankCommandDTO command = Commands[index];
            SubmarineBallastFluidSampleDTO sample = FluidSamples.IsCreated && FluidSamples.Length > 0
                ? FluidSamples[0]
                : default;

            float dt = math.clamp(SafeFinite(DeltaTime, 0.02f), 0.0001f, 0.2f);
            float volume = math.max(0f, SafeFinite(tank.TankVolumeLiters, 0f));
            float current = math.clamp(SafeFinite(tank.CurrentWaterLiters, 0f), 0f, volume);
            float target = math.clamp(SafeFinite(command.TargetWaterLiters, current), 0f, volume);
            float pressure = math.max(SubmarineBallastConstants.AtmosphericPressureAtm, SafeFinite(command.CompressedAirPressureATM, tank.CompressedAirPressureATM));
            float ambientPressure = math.max(SubmarineBallastConstants.AtmosphericPressureAtm, SafeFinite(sample.AmbientPressureATM, SubmarineBallastConstants.AtmosphericPressureAtm));
            uint flags = SubmarineBallastConstants.TankFlagInitialized;
            bool commandFlood = (command.CommandFlags & SubmarineBallastConstants.CommandFlagFlood) != 0u;
            bool commandBlow = (command.CommandFlags & SubmarineBallastConstants.CommandFlagBlow) != 0u;
            float before = current;

            if (commandFlood && target > current)
            {
                float rate = math.max(0f, SafeFinite(command.FloodRateLitersPerSecond, tank.PumpRateLitersPerSecond));
                current = math.min(target, current + (rate * dt));
                flags |= SubmarineBallastConstants.TankFlagFlooding;
                tank.PumpRateLitersPerSecond = rate;
            }
            else if (commandBlow && target < current)
            {
                if (pressure > ambientPressure)
                {
                    float rate = math.max(0f, SafeFinite(command.BlowRateLitersPerSecond, tank.PumpRateLitersPerSecond));
                    current = math.max(target, current - (rate * dt));
                    flags |= SubmarineBallastConstants.TankFlagBlowing;
                    tank.PumpRateLitersPerSecond = rate;
                }
                else
                {
                    flags |= SubmarineBallastConstants.TankFlagPressureBlocked;
                }
            }

            bool finite = math.isfinite(volume) &
                          math.isfinite(current) &
                          math.isfinite(pressure) &
                          math.isfinite(ambientPressure);
            flags |= math.select(0u, SubmarineBallastConstants.TankFlagNonFinite, !finite);
            tank.TankVolumeLiters = finite ? volume : 0f;
            tank.CurrentWaterLiters = finite ? current : 0f;
            tank.CompressedAirPressureATM = finite ? pressure : SubmarineBallastConstants.AtmosphericPressureAtm;
            tank.InputStateFlags = flags;

            float releasedLiters = math.max(0f, before - current);
            bool emit = EmitAcousticSignals != 0 &&
                        releasedLiters > 0.001f &&
                        ((Frame + (uint)index) & 7u) == 0u &&
                        (flags & SubmarineBallastConstants.TankFlagBlowing) != 0u;
            if (emit)
            {
                MovementAcousticSignal signal = default;
                signal.PositionAup = sample.HullPositionAup;
                signal.Volume = math.saturate((pressure - ambientPressure) * 0.05f) * math.saturate(releasedLiters * 0.05f);
                signal.VelocitySq = pressure * pressure;
                signal.SourceId = command.TargetEntityHash != 0u ? command.TargetEntityHash : SubmarineBallastConstants.SourceHash;
                signal.LocomotionMode = SubmarineBallastConstants.MovementModePneumaticHiss;
                signal.SurfaceMode = (byte)math.clamp(index, 0, 255);
                signal.Flags = 1;
                if (!SignalBus<MovementAcousticSignal>.TryEnqueueBounded(AcousticWriter, AcousticWriterBudget, signal))
                {
                    flags |= SubmarineBallastConstants.TankFlagSignalDrop;
                    tank.InputStateFlags = flags;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateBuoyancyForceJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<BallastTankDTO> Tanks;
        [ReadOnly, NoAlias] public NativeArray<SubmarineBallastFluidSampleDTO> FluidSamples;
        [NoAlias] public NativeArray<SubmarineBallastForcePacketDTO> ForcePackets;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<SubmarineBallastTelemetryEntry> TelemetryRing;
        public int TankCount;
        public uint Frame;

        public void Execute(int index)
        {
            if (!Tanks.IsCreated ||
                !FluidSamples.IsCreated ||
                !ForcePackets.IsCreated ||
                (uint)index >= (uint)ForcePackets.Length ||
                FluidSamples.Length <= 0)
            {
                return;
            }

            SubmarineBallastFluidSampleDTO sample = FluidSamples[math.min(index, FluidSamples.Length - 1)];
            int tankCount = math.min(math.max(0, TankCount), Tanks.Length);
            double3 hullAup = SafeFinite(sample.HullAup, double3.zero);
            double3 surfaceAup = SafeFinite(sample.OceanSurfaceAup, hullAup);
            double3 surfaceDelta = surfaceAup - hullAup;
            float depthMeters = math.max(0f, SafeFinite((float)surfaceDelta.y, 0f));
            float hullHeight = math.max(SubmarineBallastConstants.Epsilon, SafeFinite(sample.HullHeightMeters, 1f));
            float hullVolume = math.max(0f, SafeFinite(sample.HullVolumeCubicMeters, 0f));
            float density = math.max(SubmarineBallastConstants.Epsilon, SafeFinite(sample.FluidDensityKgPerM3, SubmarineBallastConstants.DefaultWaterDensityKgPerM3));
            float ambientPressure = math.max(SubmarineBallastConstants.AtmosphericPressureAtm, SafeFinite(sample.AmbientPressureATM, SubmarineBallastConstants.AtmosphericPressureAtm));
            float quality = math.saturate(SafeFinite(sample.GlobalQualityWeight, 1f));
            int qualitySamples = math.clamp((int)math.round(math.lerp(1f, 4f, math.smoothstep(0f, 1f, quality))), 1, 4);
            int requestedSamples = math.clamp(sample.ActiveSampleBudget, 0, 4);
            int activeSamples = math.select(qualitySamples, requestedSamples, requestedSamples > 0);
            float halfHeight = hullHeight * 0.5f;
            float center = math.saturate((depthMeters + halfHeight) * math.rcp(hullHeight));
            float bow = math.saturate((depthMeters + sample.SurfaceSwellMeters * 0.35f + halfHeight) * math.rcp(hullHeight));
            float stern = math.saturate((depthMeters - sample.SurfaceSwellMeters * 0.35f + halfHeight) * math.rcp(hullHeight));
            float beam = math.saturate((depthMeters + sample.SurfaceSwellMeters * 0.125f + halfHeight) * math.rcp(hullHeight));
            float submerged = center;
            submerged += math.select(0f, bow, activeSamples >= 2);
            submerged += math.select(0f, stern, activeSamples >= 3);
            submerged += math.select(0f, beam, activeSamples >= 4);
            submerged *= math.rcp((float)activeSamples);

            float totalWaterLiters = 0f;
            float totalAirMassKg = 0f;
            uint flags = 0u;
            for (int i = 0; i < tankCount; i++)
            {
                BallastTankDTO tank = Tanks[i];
                float tankVolume = math.max(0f, SafeFinite(tank.TankVolumeLiters, 0f));
                float waterLiters = math.clamp(SafeFinite(tank.CurrentWaterLiters, 0f), 0f, tankVolume);
                float airPressure = math.max(SubmarineBallastConstants.AtmosphericPressureAtm, SafeFinite(tank.CompressedAirPressureATM, 1f));
                float airVolumeM3 = math.max(0f, (tankVolume - waterLiters) * SubmarineBallastConstants.CubicMetersPerLiter);
                totalWaterLiters += waterLiters;
                totalAirMassKg += airVolumeM3 * SubmarineBallastConstants.AirDensityKgPerM3AtOneAtm * airPressure;
                flags |= math.select(0u, SubmarineBallastConstants.ForceFlagPressureBlocked, (tank.InputStateFlags & SubmarineBallastConstants.TankFlagPressureBlocked) != 0u);
                flags |= math.select(0u, SubmarineBallastConstants.ForceFlagSignalDrop, (tank.InputStateFlags & SubmarineBallastConstants.TankFlagSignalDrop) != 0u);
                flags |= math.select(0u, SubmarineBallastConstants.ForceFlagNonFinite, (tank.InputStateFlags & SubmarineBallastConstants.TankFlagNonFinite) != 0u);
            }

            float displacedVolume = hullVolume * submerged;
            float buoyantY = displacedVolume * density * SubmarineBallastConstants.Gravity;
            float waterMassKg = totalWaterLiters * SubmarineBallastConstants.CubicMetersPerLiter * density;
            float ballastGravityY = -(waterMassKg + totalAirMassKg) * SubmarineBallastConstants.Gravity;
            float netY = buoyantY + ballastGravityY;
            float3 netForce = new float3(0f, netY, 0f);
            bool finite = math.all(math.isfinite(netForce)) &
                          math.isfinite(buoyantY) &
                          math.isfinite(ballastGravityY) &
                          math.isfinite(displacedVolume) &
                          math.isfinite(totalWaterLiters) &
                          math.isfinite(totalAirMassKg) &
                          math.isfinite(ambientPressure);
            flags |= math.select(SubmarineBallastConstants.ForceFlagValid, SubmarineBallastConstants.ForceFlagNonFinite, !finite);
            flags |= math.select(0u, SubmarineBallastConstants.ForceFlagMockFluid, (sample.Flags & SubmarineBallastConstants.SampleFlagMockFluid) != 0u);
            if (!finite)
            {
                netForce = float3.zero;
                buoyantY = 0f;
                ballastGravityY = 0f;
                displacedVolume = 0f;
                totalWaterLiters = 0f;
                totalAirMassKg = 0f;
                submerged = 0f;
            }

            uint hash = BuildStateHash(netForce.y, buoyantY, ballastGravityY, totalWaterLiters, totalAirMassKg, ambientPressure, flags);
            SubmarineBallastForcePacketDTO packet = default;
            packet.HullAup = hullAup;
            packet.NetForce = netForce;
            packet.BuoyantForce = new float3(0f, buoyantY, 0f);
            packet.BallastGravityForce = new float3(0f, ballastGravityY, 0f);
            packet.SubmergedRatio = submerged;
            packet.TotalWaterLiters = totalWaterLiters;
            packet.TotalCompressedAirMassKg = totalAirMassKg;
            packet.AmbientPressureATM = ambientPressure;
            packet.FluidDensityKgPerM3 = density;
            packet.DisplacedVolumeCubicMeters = displacedVolume;
            packet.TargetEntityHash = sample.TargetEntityHash;
            packet.Flags = flags;
            packet.ActiveSamples = activeSamples;
            packet.Frame = Frame;
            packet.ComputeMicros = 0f;
            packet.LocalizedSurfaceDelta = new float3((float)surfaceDelta.x, (float)surfaceDelta.y, (float)surfaceDelta.z);
            packet.StateHash = hash;
            ForcePackets[index] = packet;

            if (TelemetryRing.IsCreated && TelemetryRing.Length > 0)
            {
                int telemetryIndex = (int)(Frame % (uint)TelemetryRing.Length);
                TelemetryRing[telemetryIndex] = new SubmarineBallastTelemetryEntry
                {
                    Frame = Frame,
                    Flags = flags,
                    StateHash = hash,
                    NetForceY = netForce.y,
                    BuoyantForceY = buoyantY,
                    BallastGravityForceY = ballastGravityY,
                    WaterLiters = totalWaterLiters,
                    CompressedAirMassKg = totalAirMassKg,
                    AmbientPressureATM = ambientPressure,
                    DisplacedVolumeCubicMeters = displacedVolume,
                    SubmergedRatio = submerged,
                    ComputeMicros = 0f,
                    GlobalQualityWeight = quality,
                    ActiveSamples = activeSamples,
                    TargetEntityHash = sample.TargetEntityHash,
                    RingCursor = (uint)telemetryIndex
                };
            }
        }

        private static uint BuildStateHash(float netY, float buoyantY, float ballastY, float liters, float airMass, float pressure, uint flags)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(netY));
            hash = Mix(hash, math.asuint(buoyantY));
            hash = Mix(hash, math.asuint(ballastY));
            hash = Mix(hash, math.asuint(liters));
            hash = Mix(hash, math.asuint(airMass));
            hash = Mix(hash, math.asuint(pressure));
            return Mix(hash, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 SafeFinite(double3 value, double3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }
    }
}
