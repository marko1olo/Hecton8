using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal unsafe struct MesofaunaStateDTO
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public uint TargetHashID;
        [FieldOffset(40)] public byte CurrentState;
        [FieldOffset(41)] public byte PreviousState;
        [FieldOffset(42)] public ushort StateTimerTicks;
        [FieldOffset(44)] public float AggressionScalar;
        [FieldOffset(48)] public byte _pad0;
        [FieldOffset(49)] public byte _pad1;
        [FieldOffset(50)] public byte _pad2;
        [FieldOffset(51)] public byte _pad3;
        [FieldOffset(52)] public byte _pad4;
        [FieldOffset(53)] public byte _pad5;
        [FieldOffset(54)] public byte _pad6;
        [FieldOffset(55)] public byte _pad7;
        [FieldOffset(56)] public byte _pad8;
        [FieldOffset(57)] public byte _pad9;
        [FieldOffset(58)] public byte _pad10;
        [FieldOffset(59)] public byte _pad11;
        [FieldOffset(60)] public byte _pad12;
        [FieldOffset(61)] public byte _pad13;
        [FieldOffset(62)] public byte _pad14;
        [FieldOffset(63)] public byte _pad15;

        public static ref MesofaunaStateDTO AsMutableRef(void* ptr)
        {
            return ref UnsafeUtility.AsRef<MesofaunaStateDTO>(ptr);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct MesofaunaTargetDTO
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public uint TargetHashID;
        [FieldOffset(40)] public uint SpeciesHash;
        [FieldOffset(44)] public byte Flags;
        [FieldOffset(45)] public byte ThreatClass;
        [FieldOffset(46)] public ushort Reserved0;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public uint Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct MesofaunaVisualSyncDTO
    {
        [FieldOffset(0)] public float3 DesiredVelocity;
        [FieldOffset(12)] public float SpeedScalar;
        [FieldOffset(16)] public byte CurrentState;
        [FieldOffset(17)] public byte PreviousState;
        [FieldOffset(18)] public ushort Flags;
        [FieldOffset(20)] public uint TargetHashID;
        [FieldOffset(24)] public float ScentSignal01;
        [FieldOffset(28)] public float ObstaclePressure01;
        [FieldOffset(32)] public double3 TargetAup;
        [FieldOffset(56)] public float TargetDistanceMeters;
        [FieldOffset(60)] public uint TargetFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct MesofaunaTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public ushort ActivePredators;
        [FieldOffset(6)] public ushort HuntingPredators;
        [FieldOffset(8)] public float AvgSpatialHashQueryMicroseconds;
        [FieldOffset(12)] public float FsmMicroseconds;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public byte SliceModulo;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] public ushort NonFiniteFallbackCount;
        [FieldOffset(24)] public uint StateHash;
        [FieldOffset(28)] public uint TargetHash;
        [FieldOffset(32)] public double3 ProbeAup;
        [FieldOffset(56)] public uint DumpReasonHash;
        [FieldOffset(60)] public ushort FleeingPredators;
        [FieldOffset(62)] public ushort Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct MesofaunaTuningDTO
    {
        [FieldOffset(0)] public float VisionRadiusLow;
        [FieldOffset(4)] public float VisionRadiusUltra;
        [FieldOffset(8)] public float ScentSensitivity;
        [FieldOffset(12)] public float BaseSpeedMetersPerSecond;
        [FieldOffset(16)] public ushort IdleToSearchTicks;
        [FieldOffset(18)] public ushort SearchToIdleTicks;
        [FieldOffset(20)] public float StateTimeoutSeconds;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public uint Flags;

        public static MesofaunaTuningDTO CreateDefault(float qualityWeight)
        {
            MesofaunaTuningDTO tuning = default;
            tuning.VisionRadiusLow = 22f;
            tuning.VisionRadiusUltra = 104f;
            tuning.ScentSensitivity = 1.0f;
            tuning.BaseSpeedMetersPerSecond = 6.0f;
            tuning.IdleToSearchTicks = 8;
            tuning.SearchToIdleTicks = 120;
            tuning.StateTimeoutSeconds = 4.5f;
            tuning.GlobalQualityWeight = math.saturate(qualityWeight);
            tuning.Flags = 1u;
            return tuning;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct MesofaunaSpeciesProfileDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float AggressionMultiplier;
        [FieldOffset(8)] public float SpeedMultiplier;
        [FieldOffset(12)] public float ScentSensitivityMultiplier;
        [FieldOffset(16)] public float VisionRadiusMultiplier;
        [FieldOffset(20)] public float HuntBias;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] public byte _pad0;
        [FieldOffset(26)] public byte _pad1;
        [FieldOffset(27)] public byte _pad2;
        [FieldOffset(28)] public byte _pad3;
        [FieldOffset(29)] public byte _pad4;
        [FieldOffset(30)] public byte _pad5;
        [FieldOffset(31)] public byte _pad6;

        public static MesofaunaSpeciesProfileDTO CreateDefault(uint speciesHash)
        {
            MesofaunaSpeciesProfileDTO profile = default;
            profile.SpeciesHash = speciesHash;
            profile.AggressionMultiplier = 1f;
            profile.SpeedMultiplier = 1f;
            profile.ScentSensitivityMultiplier = 1f;
            profile.VisionRadiusMultiplier = 1f;
            profile.HuntBias = 1f;
            profile.Flags = 1;
            return profile;
        }
    }

    internal static class MesofaunaBehaviorConstants
    {
        internal const int StateDtoSizeBytes = 64;
        internal const int TargetDtoSizeBytes = 64;
        internal const int VisualSyncDtoSizeBytes = 64;
        internal const int TelemetryEntrySizeBytes = 64;
        internal const int TuningDtoSizeBytes = 32;
        internal const int SpeciesProfileDtoSizeBytes = 32;
        internal const int TelemetryCapacity = 300;
        internal const int SpeciesProfileCapacity = 64;
        internal const int CsvScratchBytes = 4096;
        internal const int TargetSpatialHashBucketCount = 1024;
        internal const int StateIdle = 0;
        internal const int StateSearch = 1;
        internal const int StateHunt = 2;
        internal const int StateFlee = 3;
        internal const int StateTrackScent = 4;
        internal const byte TargetFlagValid = 1;
        internal const ushort VisualFlagHunt = 1;
        internal const uint VisualTargetFlagValid = 1u;
        internal const byte TelemetryFlagFault = 1;
        internal const byte TelemetryFlagOverBudget = 2;
        internal const uint TelemetryContextHash = 0x4D455346u; // MESF
        internal const uint DumpFailureTelemetryHash = 0x4D44464Cu; // MDFL
        internal const uint DumpReasonFaultHash = 0x4D464C54u; // MFLT
        internal const uint DumpReasonOverBudgetHash = 0x4D425544u; // MBUD
        internal const float DirectionLengthSqEpsilon = 0.0001f;
        internal const double InvAupSectorMeters = 0.00390625d;
        internal const float InvThree = 0.3333333333333333f;
        internal const float InvThirtyTwo = 0.03125f;
        internal const float InvByteMax = 0.00392156862745098f;

        internal static bool ValidateLayout()
        {
            return UnsafeUtility.SizeOf<MesofaunaStateDTO>() == StateDtoSizeBytes &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO.AUP_Position)) == 0 &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO.Velocity)) == 24 &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO.TargetHashID)) == 36 &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO.CurrentState)) == 40 &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO.PreviousState)) == 41 &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO.StateTimerTicks)) == 42 &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO.AggressionScalar)) == 44 &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO._pad0)) == 48 &&
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO._pad15)) == 63 &&
                   UnsafeUtility.SizeOf<MesofaunaTargetDTO>() == TargetDtoSizeBytes &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.AUP_Position)) == 0 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.Velocity)) == 24 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.TargetHashID)) == 36 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.SpeciesHash)) == 40 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.Flags)) == 44 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.ThreatClass)) == 45 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.Reserved0)) == 46 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.RadiusMeters)) == 48 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.Reserved1)) == 52 &&
                   OffsetOf<MesofaunaTargetDTO>(nameof(MesofaunaTargetDTO.Reserved2)) == 56 &&
                   UnsafeUtility.SizeOf<MesofaunaVisualSyncDTO>() == VisualSyncDtoSizeBytes &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.DesiredVelocity)) == 0 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.SpeedScalar)) == 12 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.CurrentState)) == 16 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.PreviousState)) == 17 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.Flags)) == 18 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.TargetHashID)) == 20 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.ScentSignal01)) == 24 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.ObstaclePressure01)) == 28 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.TargetAup)) == 32 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.TargetDistanceMeters)) == 56 &&
                   OffsetOf<MesofaunaVisualSyncDTO>(nameof(MesofaunaVisualSyncDTO.TargetFlags)) == 60 &&
                   UnsafeUtility.SizeOf<MesofaunaTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.Frame)) == 0 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.ActivePredators)) == 4 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.HuntingPredators)) == 6 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.AvgSpatialHashQueryMicroseconds)) == 8 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.FsmMicroseconds)) == 12 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.GlobalQualityWeight)) == 16 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.SliceModulo)) == 20 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.Flags)) == 21 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.NonFiniteFallbackCount)) == 22 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.StateHash)) == 24 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.TargetHash)) == 28 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.ProbeAup)) == 32 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.DumpReasonHash)) == 56 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.FleeingPredators)) == 60 &&
                   OffsetOf<MesofaunaTelemetryEntry>(nameof(MesofaunaTelemetryEntry.Reserved0)) == 62 &&
                   UnsafeUtility.SizeOf<MesofaunaTuningDTO>() == TuningDtoSizeBytes &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.VisionRadiusLow)) == 0 &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.VisionRadiusUltra)) == 4 &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.ScentSensitivity)) == 8 &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.BaseSpeedMetersPerSecond)) == 12 &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.IdleToSearchTicks)) == 16 &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.SearchToIdleTicks)) == 18 &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.StateTimeoutSeconds)) == 20 &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.GlobalQualityWeight)) == 24 &&
                   OffsetOf<MesofaunaTuningDTO>(nameof(MesofaunaTuningDTO.Flags)) == 28 &&
                   UnsafeUtility.SizeOf<MesofaunaSpeciesProfileDTO>() == SpeciesProfileDtoSizeBytes &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO.SpeciesHash)) == 0 &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO.AggressionMultiplier)) == 4 &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO.SpeedMultiplier)) == 8 &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO.ScentSensitivityMultiplier)) == 12 &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO.VisionRadiusMultiplier)) == 16 &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO.HuntBias)) == 20 &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO.Flags)) == 24 &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO._pad0)) == 25 &&
                   OffsetOf<MesofaunaSpeciesProfileDTO>(nameof(MesofaunaSpeciesProfileDTO._pad6)) == 31;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }

        internal static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }

        internal static float FastLengthFromSq(float lengthSq, float minLengthSq)
        {
            if (!math.isfinite(lengthSq))
                return 0f;

            float safeLengthSq = math.max(lengthSq, minLengthSq);
            return safeLengthSq > 0f ? safeLengthSq * math.rsqrt(safeLengthSq) : 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct InitializeMesofaunaStateJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaStateDTO> States;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaTargetDTO> MockTargets;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaVisualSyncDTO> VisualSync;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaTelemetryEntry> TelemetryRing;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaTuningDTO> Tuning;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaSpeciesProfileDTO> SpeciesProfiles;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> SpeciesProfileCount;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> CsvScratch;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TargetHashBucketHeads;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TargetHashNext;
        public MesofaunaTuningDTO DefaultTuning;

        public void Execute(int index)
        {
            if (States.IsCreated && index < States.Length)
            {
                MesofaunaStateDTO state = default;
                state.CurrentState = MesofaunaBehaviorConstants.StateIdle;
                States[index] = state;
            }

            if (MockTargets.IsCreated && index < MockTargets.Length)
                MockTargets[index] = default;

            if (VisualSync.IsCreated && index < VisualSync.Length)
                VisualSync[index] = default;

            if (TelemetryRing.IsCreated && index < TelemetryRing.Length)
                TelemetryRing[index] = default;

            if (TargetHashBucketHeads.IsCreated && index < TargetHashBucketHeads.Length)
                TargetHashBucketHeads[index] = -1;

            if (TargetHashNext.IsCreated && index < TargetHashNext.Length)
                TargetHashNext[index] = -1;

            if (index == 0 && Tuning.IsCreated && Tuning.Length > 0)
                Tuning[0] = DefaultTuning;

            if (SpeciesProfiles.IsCreated && index < SpeciesProfiles.Length)
                SpeciesProfiles[index] = default;

            if (index == 0 && SpeciesProfileCount.IsCreated && SpeciesProfileCount.Length > 0)
                SpeciesProfileCount[0] = 0;

            if (CsvScratch.IsCreated && index < CsvScratch.Length)
                CsvScratch[index] = 0;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMesofaunaMockTargetsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<int> ActiveSlots;
        [ReadOnly, NoAlias] public NativeArray<CognitionInput> Inputs;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaTargetDTO> MockTargets;
        public int FrameId;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            int slot = ActiveSlots[index];
            if ((uint)slot >= (uint)MockTargets.Length || (uint)slot >= (uint)Inputs.Length)
                return;

            CognitionInput input = Inputs[slot];
            MesofaunaTargetDTO target = default;
            if ((input.Flags & (int)CognitionInputFlags.Active) == 0)
            {
                MockTargets[slot] = target;
                return;
            }

            float3 position = input.Position;
            float3 velocity = input.Velocity;
            byte threatClass = 0;
            if ((input.Flags & (int)CognitionInputFlags.HasPreyTarget) != 0 && IsFinite(input.PreyPosition))
            {
                position = input.PreyPosition;
                velocity = input.PackTargetVelocity;
                threatClass = 1;
            }
            else if ((input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0 && IsFinite(input.PlayerPosition))
            {
                position = input.PlayerPosition;
                velocity = input.PlayerVelocity;
                threatClass = 2;
            }
            else
            {
                float q = MesofaunaBehaviorConstants.Smooth01(GlobalQualityWeight);
                uint hash = Hash(slot, input.SpeciesId, 0x4D4F434Bu);
                double3 selfAup = RuntimeToAup(input.Position, input.FloatingOriginOffset);
                uint sectorHash = ResolveAupSectorHash(selfAup);
                Unity.Mathematics.Random random = CreateDeterministicRandom(sectorHash, (uint)math.max(1, FrameId), hash);
                float angleJitter = random.NextFloat(-0.0125f, 0.0125f);
                float radiusJitter = random.NextFloat(-0.35f, 0.35f);
                float verticalJitter = random.NextFloat(-0.2f, 0.2f);
                float angle = ((hash & 1023u) * 0.006135923151f) + (FrameId * 0.01171875f) + angleJitter;
                float radius = math.lerp(6f, 18f, q) + ((slot & 7) * 0.5f) + radiusJitter;
                Hecton8.Core.MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                float3 offset = new float3(cos * radius, (((slot & 3) - 1.5f) * 0.75f) + verticalJitter, sin * radius);
                position = input.Position + offset;
                velocity = ResolveDirection(new float3(-offset.z, 0f, offset.x), input.Forward) * math.lerp(1.0f, 2.75f, q);
                threatClass = 3;
            }

            if (!IsFinite(position))
                position = float3.zero;
            if (!IsFinite(velocity))
                velocity = float3.zero;

            double3 targetAup = RuntimeToAup(position, input.FloatingOriginOffset);
            if (threatClass == 1)
                targetAup = ResolveAupOrRuntime(in input.PackTargetAup, position, input.FloatingOriginOffset);
            else if (threatClass == 2)
                targetAup = ResolveAupOrRuntime(in input.PlayerTargetAup, position, input.FloatingOriginOffset);

            target.AUP_Position = targetAup;
            target.Velocity = velocity;
            target.TargetHashID = Hash(slot, input.SpeciesId, 0x54524754u);
            target.SpeciesHash = unchecked((uint)input.SpeciesId);
            target.Flags = MesofaunaBehaviorConstants.TargetFlagValid;
            target.ThreatClass = threatClass;
            target.RadiusMeters = math.max(0.5f, input.AttackRange);
            MockTargets[slot] = target;
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static double3 RuntimeToAup(float3 runtimePosition, double3 floatingOriginOffset)
        {
            if (!IsFinite(runtimePosition))
                runtimePosition = float3.zero;
            if (!math.all(math.isfinite(floatingOriginOffset)))
                floatingOriginOffset = double3.zero;
            return new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + floatingOriginOffset;
        }

        private static double3 ResolveAupOrRuntime(in AbsoluteUniversePositionBlit128 aup, float3 runtimePosition, double3 floatingOriginOffset)
        {
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double3 absolute = new double3(
                (aup.GridX * cellSize) + aup.Local.x,
                (aup.GridY * cellSize) + aup.Local.y,
                (aup.GridZ * cellSize) + aup.Local.z);
            if (math.all(math.isfinite(absolute)) &&
                (math.any(absolute != double3.zero) || math.all(runtimePosition == float3.zero)))
            {
                return absolute;
            }

            return RuntimeToAup(runtimePosition, floatingOriginOffset);
        }

        private static Unity.Mathematics.Random CreateDeterministicRandom(uint sectorHash, uint simulationFrame, uint salt)
        {
            unchecked
            {
                uint seed = sectorHash ^ (simulationFrame * 747796405u) ^ salt ^ 0x9E3779B9u;
                seed ^= seed >> 16;
                seed *= 2246822519u;
                seed ^= seed >> 13;
                seed *= 3266489917u;
                seed ^= seed >> 16;
                return new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
            }
        }

        private static uint ResolveAupSectorHash(double3 aup)
        {
            if (!math.all(math.isfinite(aup)))
                return 0x4D534543u; // MSEC
            int3 sector = new int3(
                (int)math.floor(aup.x * MesofaunaBehaviorConstants.InvAupSectorMeters),
                (int)math.floor(aup.y * MesofaunaBehaviorConstants.InvAupSectorMeters),
                (int)math.floor(aup.z * MesofaunaBehaviorConstants.InvAupSectorMeters));
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)sector.x) * 16777619u;
                hash = (hash ^ (uint)sector.y) * 16777619u;
                hash = (hash ^ (uint)sector.z) * 16777619u;
                return hash == 0u ? 0x4D534543u : hash;
            }
        }

        private static float3 ResolveDirection(float3 direction, float3 fallback)
        {
            float lengthSq = IsFinite(direction) ? math.lengthsq(direction) : 0f;
            if (!math.isfinite(lengthSq) || lengthSq <= MesofaunaBehaviorConstants.DirectionLengthSqEpsilon)
            {
                direction = fallback;
                lengthSq = IsFinite(direction) ? math.lengthsq(direction) : 0f;
            }

            if (!math.isfinite(lengthSq) || lengthSq <= MesofaunaBehaviorConstants.DirectionLengthSqEpsilon)
                return new float3(0f, 0f, 1f);
            return direction * math.rsqrt(math.max(lengthSq, MesofaunaBehaviorConstants.DirectionLengthSqEpsilon));
        }

        private static uint Hash(int slot, int speciesId, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)slot) * 16777619u;
                hash = (hash ^ (uint)speciesId) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                return hash == 0u ? salt : hash;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildMesofaunaTargetSpatialHashJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<int> ActiveSlots;
        [ReadOnly, NoAlias] public NativeArray<CognitionInput> Inputs;
        [ReadOnly, NoAlias] public NativeArray<MesofaunaTargetDTO> MockTargets;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TargetHashBucketHeads;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TargetHashNext;
        public int ActiveSlotCount;
        public float3 SwarmBoundsMin;
        public float CellSizeMeters;
        public int BucketMask;

        public void Execute()
        {
            if (!TargetHashBucketHeads.IsCreated || !TargetHashNext.IsCreated)
                return;

            for (int i = 0; i < TargetHashBucketHeads.Length; i++)
                TargetHashBucketHeads[i] = -1;
            for (int i = 0; i < TargetHashNext.Length; i++)
                TargetHashNext[i] = -1;

            if (!ActiveSlots.IsCreated || !Inputs.IsCreated || !MockTargets.IsCreated)
                return;

            int safeCount = math.min(math.max(0, ActiveSlotCount), ActiveSlots.Length);
            int mask = BucketMask <= 0 ? TargetHashBucketHeads.Length - 1 : BucketMask;
            float cellSize = math.max(0.001f, CellSizeMeters);
            for (int i = 0; i < safeCount; i++)
            {
                int slot = ActiveSlots[i];
                if ((uint)slot >= (uint)Inputs.Length ||
                    (uint)slot >= (uint)MockTargets.Length ||
                    (uint)slot >= (uint)TargetHashNext.Length)
                    continue;

                CognitionInput input = Inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0)
                    continue;

                MesofaunaTargetDTO target = MockTargets[slot];
                if ((target.Flags & MesofaunaBehaviorConstants.TargetFlagValid) == 0 ||
                    !math.all(math.isfinite(target.AUP_Position)))
                {
                    continue;
                }

                float3 targetPosition = AupToRuntimePosition(target.AUP_Position, input.FloatingOriginOffset);
                if (!math.all(math.isfinite(targetPosition)))
                    continue;

                int3 bucket = ResolveBucket(targetPosition, SwarmBoundsMin, cellSize);
                int bucketIndex = HashBucket(bucket) & mask;
                if ((uint)bucketIndex >= (uint)TargetHashBucketHeads.Length)
                    continue;

                TargetHashNext[slot] = TargetHashBucketHeads[bucketIndex];
                TargetHashBucketHeads[bucketIndex] = slot;
            }
        }

        private static int3 ResolveBucket(float3 position, float3 boundsMin, float cellSize)
        {
            float inv = math.rcp(math.max(0.001f, cellSize));
            float3 local = math.max(position - boundsMin, float3.zero);
            return new int3((int)math.floor(local.x * inv), (int)math.floor(local.y * inv), (int)math.floor(local.z * inv));
        }

        private static int HashBucket(int3 bucket)
        {
            unchecked
            {
                return (int)(((uint)bucket.x * 73856093u) ^ ((uint)bucket.y * 19349663u) ^ ((uint)bucket.z * 83492791u));
            }
        }

        private static float3 AupToRuntimePosition(double3 aup, double3 floatingOriginOffset)
        {
            if (!math.all(math.isfinite(aup)))
                return float3.zero;
            if (!math.all(math.isfinite(floatingOriginOffset)))
                floatingOriginOffset = double3.zero;
            double3 delta = aup - floatingOriginOffset;
            if (!math.all(math.isfinite(delta)))
                return float3.zero;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct MesofaunaBehaviorJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<int> ActiveSlots;
        [ReadOnly, NoAlias] public NativeArray<CognitionInput> Inputs;
        [ReadOnly, NoAlias] public NativeArray<CognitionControl> Controls;
        [ReadOnly, NoAlias] public NativeArray<MesofaunaTargetDTO> MockTargets;
        [ReadOnly, NoAlias] public NativeArray<MesofaunaSpeciesProfileDTO> SpeciesProfiles;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaStateDTO> States;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaVisualSyncDTO> VisualSync;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<PackedCognitionOutput> Outputs;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> ChosenStates;
        [ReadOnly, NoAlias] public NativeArray<int> TargetHashBucketHeads;
        [ReadOnly, NoAlias] public NativeArray<int> TargetHashNext;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly ThreatVoxelGrid;
        [ReadOnly, NoAlias] public NativeArray<ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint>.ReadOnly ChemicalBreadcrumbs;
        public int3 ThreatVoxelDimensions;
        public float3 ThreatVoxelOrigin;
        public float3 ThreatVoxelCellSize;
        public byte ThreatVoxelSolidThreshold;
        public int ThreatVoxelUsesSignedDistanceEncoding;
        public int ChemicalBreadcrumbCount;
        public float ChemicalBreadcrumbFollowStepMeters;
        public float3 SwarmBoundsMin;
        public float TargetHashCellSizeMeters;
        public int TargetHashBucketMask;
        public int FrameId;
        public int SliceModulo;
        public float GlobalQualityWeight;
        public float VisionRadiusMeters;
        public MesofaunaTuningDTO Tuning;

        public void Execute(int index)
        {
            int slot = ActiveSlots[index];
            if ((uint)slot >= (uint)Inputs.Length ||
                (uint)slot >= (uint)States.Length ||
                (uint)slot >= (uint)Outputs.Length)
            {
                return;
            }

            CognitionInput input = Inputs[slot];
            if ((input.Flags & (int)CognitionInputFlags.Active) == 0 ||
                (input.Flags & (int)CognitionInputFlags.PredatorRole) == 0 ||
                (input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) != 0 ||
                (input.Flags & (int)CognitionInputFlags.IsApexPredator) != 0)
            {
                return;
            }

            MesofaunaStateDTO* statePtr = (MesofaunaStateDTO*)States.GetUnsafePtr() + slot;
            ref MesofaunaStateDTO state = ref MesofaunaStateDTO.AsMutableRef(statePtr);
            MesofaunaSpeciesProfileDTO speciesProfile = ResolveSpeciesProfile(input.SpeciesId);
            float aggressionMultiplier = ResolveProfileScalar(speciesProfile.AggressionMultiplier);
            float speedMultiplier = ResolveProfileScalar(speciesProfile.SpeedMultiplier);
            float scentMultiplier = ResolveProfileScalar(speciesProfile.ScentSensitivityMultiplier);
            float visionMultiplier = math.clamp(ResolveProfileScalar(speciesProfile.VisionRadiusMultiplier), 0.25f, 3f);
            state.AUP_Position = RuntimeToAup(input.Position, input.FloatingOriginOffset);
            state.AggressionScalar = math.saturate(input.AggressionWeight * aggressionMultiplier);

            int stride = math.clamp(SliceModulo, 1, 10);
            bool continuityOnly = ((FrameId + slot) % stride) != 0;
            if (continuityOnly)
            {
                ApplyContinuity(slot, in input, ref state);
                return;
            }

            float quality = MesofaunaBehaviorConstants.Smooth01(GlobalQualityWeight);
            int stateTimeoutTicks = ResolveStateTimeoutTicks(quality);
            int idleToSearchTicks = math.clamp(math.min((int)Tuning.IdleToSearchTicks, stateTimeoutTicks), 1, ushort.MaxValue);
            int searchToIdleTicks = math.clamp(math.min((int)Tuning.SearchToIdleTicks, stateTimeoutTicks), 1, ushort.MaxValue);
            float visionRadius = math.max(4f, VisionRadiusMeters * visionMultiplier);
            float visionRadiusSq = visionRadius * visionRadius;
            float3 fallbackForward = ResolveDirection(input.Forward, new float3(0f, 0f, 1f));
            float3 desiredDirection = fallbackForward;
            float3 targetPosition = input.Position + fallbackForward;
            float3 targetVelocity = float3.zero;
            double3 targetAup = RuntimeToAup(targetPosition, input.FloatingOriginOffset);
            uint targetHash = state.TargetHashID;
            float scentSignal = 0f;
            float targetDistanceSq = float.MaxValue;
            byte nextState = state.CurrentState;

            bool canFlee = (input.Flags & (int)CognitionInputFlags.CanFlee) != 0;
            float fleeThreshold = math.clamp(input.FleeHealthThreshold > 0f ? input.FleeHealthThreshold : 0.25f, 0.05f, 1f);
            bool lowHealth = input.HealthNormalized > 0f && input.HealthNormalized <= fleeThreshold;
            bool hardFear = input.FearPressure01 >= math.lerp(0.62f, 0.42f, quality);
            bool mustFlee = canFlee && (lowHealth || hardFear);
            switch (state.CurrentState)
            {
                case MesofaunaBehaviorConstants.StateFlee:
                {
                    bool keepFleeing = mustFlee || state.StateTimerTicks <= math.max(4, searchToIdleTicks >> 1);
                    if (keepFleeing)
                    {
                        nextState = MesofaunaBehaviorConstants.StateFlee;
                        targetPosition = ResolveThreatPosition(slot, in input, fallbackForward);
                        targetAup = RuntimeToAup(targetPosition, input.FloatingOriginOffset);
                        desiredDirection = ResolveDirection(input.Position - targetPosition, fallbackForward);
                        targetHash = ResolveFleeTargetHash(slot, in input, state.TargetHashID);
                        break;
                    }

                    if (TryResolveDirectTarget(in input, visionRadiusSq, out targetPosition, out targetVelocity, out targetHash, out targetAup) ||
                        TryAcquireSpatialHashTarget(slot, in input, visionRadiusSq, fallbackForward, out targetPosition, out targetVelocity, out targetHash, out targetAup))
                    {
                        nextState = MesofaunaBehaviorConstants.StateHunt;
                        targetDistanceSq = math.lengthsq(targetPosition - input.Position);
                        desiredDirection = ResolveInterceptDirection(in state, targetAup, targetVelocity, quality, fallbackForward);
                    }
                    else if (TryAcquireScent(in input, quality, scentMultiplier, out targetPosition, out targetAup, out float3 scentDirection, out scentSignal, out targetHash))
                    {
                        nextState = MesofaunaBehaviorConstants.StateTrackScent;
                        desiredDirection = scentDirection;
                    }
                    else
                    {
                        nextState = MesofaunaBehaviorConstants.StateSearch;
                        desiredDirection = ResolveSearchDirection(slot, in input, quality, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x53454152u);
                    }

                    break;
                }
                case MesofaunaBehaviorConstants.StateHunt:
                {
                    if (mustFlee)
                    {
                        nextState = MesofaunaBehaviorConstants.StateFlee;
                        targetPosition = ResolveThreatPosition(slot, in input, fallbackForward);
                        targetAup = RuntimeToAup(targetPosition, input.FloatingOriginOffset);
                        desiredDirection = ResolveDirection(input.Position - targetPosition, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x464C4545u);
                    }
                    else if (TryResolveDirectTarget(in input, visionRadiusSq, out targetPosition, out targetVelocity, out targetHash, out targetAup) ||
                             TryAcquireSpatialHashTarget(slot, in input, visionRadiusSq, fallbackForward, out targetPosition, out targetVelocity, out targetHash, out targetAup))
                    {
                        nextState = MesofaunaBehaviorConstants.StateHunt;
                        targetDistanceSq = math.lengthsq(targetPosition - input.Position);
                        desiredDirection = ResolveInterceptDirection(in state, targetAup, targetVelocity, quality, fallbackForward);
                    }
                    else if (TryAcquireScent(in input, quality, scentMultiplier, out targetPosition, out targetAup, out float3 scentDirection, out scentSignal, out targetHash))
                    {
                        nextState = MesofaunaBehaviorConstants.StateTrackScent;
                        desiredDirection = scentDirection;
                    }
                    else
                    {
                        nextState = MesofaunaBehaviorConstants.StateSearch;
                        desiredDirection = ResolveSearchDirection(slot, in input, quality, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x53454152u);
                    }

                    break;
                }
                case MesofaunaBehaviorConstants.StateTrackScent:
                {
                    if (mustFlee)
                    {
                        nextState = MesofaunaBehaviorConstants.StateFlee;
                        targetPosition = ResolveThreatPosition(slot, in input, fallbackForward);
                        targetAup = RuntimeToAup(targetPosition, input.FloatingOriginOffset);
                        desiredDirection = ResolveDirection(input.Position - targetPosition, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x464C4545u);
                    }
                    else if (TryResolveDirectTarget(in input, visionRadiusSq, out targetPosition, out targetVelocity, out targetHash, out targetAup) ||
                             TryAcquireSpatialHashTarget(slot, in input, visionRadiusSq, fallbackForward, out targetPosition, out targetVelocity, out targetHash, out targetAup))
                    {
                        nextState = MesofaunaBehaviorConstants.StateHunt;
                        targetDistanceSq = math.lengthsq(targetPosition - input.Position);
                        desiredDirection = ResolveInterceptDirection(in state, targetAup, targetVelocity, quality, fallbackForward);
                    }
                    else if (TryAcquireScent(in input, quality, scentMultiplier, out targetPosition, out targetAup, out float3 scentDirection, out scentSignal, out targetHash))
                    {
                        nextState = MesofaunaBehaviorConstants.StateTrackScent;
                        desiredDirection = scentDirection;
                    }
                    else
                    {
                        nextState = MesofaunaBehaviorConstants.StateSearch;
                        desiredDirection = ResolveSearchDirection(slot, in input, quality, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x53454152u);
                    }

                    break;
                }
                case MesofaunaBehaviorConstants.StateSearch:
                {
                    if (mustFlee)
                    {
                        nextState = MesofaunaBehaviorConstants.StateFlee;
                        targetPosition = ResolveThreatPosition(slot, in input, fallbackForward);
                        targetAup = RuntimeToAup(targetPosition, input.FloatingOriginOffset);
                        desiredDirection = ResolveDirection(input.Position - targetPosition, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x464C4545u);
                    }
                    else if (TryResolveDirectTarget(in input, visionRadiusSq, out targetPosition, out targetVelocity, out targetHash, out targetAup) ||
                             TryAcquireSpatialHashTarget(slot, in input, visionRadiusSq, fallbackForward, out targetPosition, out targetVelocity, out targetHash, out targetAup))
                    {
                        nextState = MesofaunaBehaviorConstants.StateHunt;
                        targetDistanceSq = math.lengthsq(targetPosition - input.Position);
                        desiredDirection = ResolveInterceptDirection(in state, targetAup, targetVelocity, quality, fallbackForward);
                    }
                    else if (TryAcquireScent(in input, quality, scentMultiplier, out targetPosition, out targetAup, out float3 scentDirection, out scentSignal, out targetHash))
                    {
                        nextState = MesofaunaBehaviorConstants.StateTrackScent;
                        desiredDirection = scentDirection;
                    }
                    else
                    {
                        nextState = state.StateTimerTicks > searchToIdleTicks
                            ? (byte)MesofaunaBehaviorConstants.StateIdle
                            : (byte)MesofaunaBehaviorConstants.StateSearch;
                        desiredDirection = ResolveSearchDirection(slot, in input, quality, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x53454152u);
                    }

                    break;
                }
                default:
                {
                    if (mustFlee)
                    {
                        nextState = MesofaunaBehaviorConstants.StateFlee;
                        targetPosition = ResolveThreatPosition(slot, in input, fallbackForward);
                        targetAup = RuntimeToAup(targetPosition, input.FloatingOriginOffset);
                        desiredDirection = ResolveDirection(input.Position - targetPosition, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x464C4545u);
                    }
                    else if (state.StateTimerTicks >= idleToSearchTicks)
                    {
                        nextState = MesofaunaBehaviorConstants.StateSearch;
                        desiredDirection = ResolveSearchDirection(slot, in input, quality, fallbackForward);
                        targetHash = Hash(slot, input.SpeciesId, 0x53454152u);
                    }
                    else
                    {
                        nextState = MesofaunaBehaviorConstants.StateIdle;
                        desiredDirection = fallbackForward;
                        targetHash = Hash(slot, input.SpeciesId, 0x49444C45u);
                    }

                    break;
                }
            }

            float obstaclePressure = 0f;
            if (TryResolveObstacleRepulsion(input.Position, desiredDirection, quality, out float3 repulsion, out obstaclePressure))
                desiredDirection = ResolveDirection(math.lerp(desiredDirection, repulsion, math.saturate(obstaclePressure)), desiredDirection);

            if (nextState != state.CurrentState)
            {
                state.PreviousState = state.CurrentState;
                state.CurrentState = nextState;
                state.StateTimerTicks = 0;
            }
            else if (state.StateTimerTicks < ushort.MaxValue)
            {
                state.StateTimerTicks++;
            }

            float speed = ResolveSpeedScalar(nextState, in input, quality, aggressionMultiplier, speedMultiplier);
            state.TargetHashID = targetHash;
            state.Velocity = desiredDirection * speed * math.max(0.5f, Tuning.BaseSpeedMetersPerSecond);
            bool shouldAttack = nextState == MesofaunaBehaviorConstants.StateHunt &&
                                input.AttackRange > 0f &&
                                targetDistanceSq <= input.AttackRange * input.AttackRange;
            bool hasTargetAup = nextState == MesofaunaBehaviorConstants.StateHunt ||
                                nextState == MesofaunaBehaviorConstants.StateFlee ||
                                nextState == MesofaunaBehaviorConstants.StateTrackScent;
            WriteVisualAndOutput(slot, in input, ref state, desiredDirection, speed, scentSignal, obstaclePressure, targetHash, targetAup, hasTargetAup, shouldAttack);
        }

        private void ApplyContinuity(int slot, in CognitionInput input, ref MesofaunaStateDTO state)
        {
            float3 direction = ResolveDirection(state.Velocity, ResolveDirection(input.Forward, new float3(0f, 0f, 1f)));
            float speed = math.clamp(
                MesofaunaBehaviorConstants.FastLengthFromSq(math.lengthsq(state.Velocity), 0f) *
                math.rcp(math.max(0.5f, Tuning.BaseSpeedMetersPerSecond)),
                0.45f,
                1.35f);
            if (state.CurrentState == MesofaunaBehaviorConstants.StateIdle)
                speed = math.min(speed, 0.55f);
            if (state.StateTimerTicks < ushort.MaxValue)
                state.StateTimerTicks++;
            double3 targetAup = state.AUP_Position;
            bool hasTargetAup = false;
            if (VisualSync.IsCreated && (uint)slot < (uint)VisualSync.Length)
            {
                MesofaunaVisualSyncDTO previous = VisualSync[slot];
                if ((previous.TargetFlags & MesofaunaBehaviorConstants.VisualTargetFlagValid) != 0 && math.all(math.isfinite(previous.TargetAup)))
                {
                    targetAup = previous.TargetAup;
                    hasTargetAup = true;
                }
            }

            WriteVisualAndOutput(slot, in input, ref state, direction, speed, 0f, 0f, state.TargetHashID, targetAup, hasTargetAup, false);
        }

        private bool TryResolveDirectTarget(
            in CognitionInput input,
            float visionRadiusSq,
            out float3 targetPosition,
            out float3 targetVelocity,
            out uint targetHash,
            out double3 targetAup)
        {
            targetPosition = default;
            targetVelocity = default;
            targetHash = 0u;
            targetAup = default;
            bool hasPrey = (input.Flags & (int)CognitionInputFlags.HasPreyTarget) != 0 && IsFinite(input.PreyPosition);
            bool hasPlayer = (input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0 && IsFinite(input.PlayerPosition);
            bool selectedPlayer = false;
            if (hasPrey)
            {
                targetPosition = input.PreyPosition;
                targetVelocity = input.PackTargetVelocity;
            }
            else if (hasPlayer && input.AggressionWeight >= 0.28f)
            {
                targetPosition = input.PlayerPosition;
                targetVelocity = input.PlayerVelocity;
                selectedPlayer = true;
            }
            else
            {
                return false;
            }

            double3 selfAup = RuntimeToAup(input.Position, input.FloatingOriginOffset);
            targetAup = selectedPlayer
                ? ResolveAupOrRuntime(in input.PlayerTargetAup, targetPosition, input.FloatingOriginOffset)
                : ResolveAupOrRuntime(in input.PackTargetAup, targetPosition, input.FloatingOriginOffset);

            float3 toTarget = AupDeltaToFloat3(targetAup - selfAup);
            if (!IsFinite(toTarget) || math.lengthsq(toTarget) > visionRadiusSq)
                return false;

            targetPosition = input.Position + toTarget;
            targetHash = HashFloat3(toTarget, selectedPlayer ? 0x504C5952u : 0x50524559u);
            return true;
        }

        private bool TryAcquireSpatialHashTarget(
            int slot,
            in CognitionInput input,
            float visionRadiusSq,
            float3 fallbackForward,
            out float3 targetPosition,
            out float3 targetVelocity,
            out uint targetHash,
            out double3 targetAup)
        {
            targetPosition = default;
            targetVelocity = default;
            targetHash = 0u;
            targetAup = default;
            if (!TargetHashBucketHeads.IsCreated || !TargetHashNext.IsCreated || !MockTargets.IsCreated)
                return false;

            int mask = TargetHashBucketMask <= 0 ? MesofaunaBehaviorConstants.TargetSpatialHashBucketCount - 1 : TargetHashBucketMask;
            int3 center = ResolveBucket(input.Position, SwarmBoundsMin, TargetHashCellSizeMeters);
            double3 selfAup = RuntimeToAup(input.Position, input.FloatingOriginOffset);
            float bestSq = visionRadiusSq;
            int bestSlot = -1;
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int bucketIndex = HashBucket(center + new int3(x, y, z)) & mask;
                        if ((uint)bucketIndex >= (uint)TargetHashBucketHeads.Length)
                            continue;

                        int candidate = TargetHashBucketHeads[bucketIndex];
                        int guard = 0;
                        while (candidate >= 0 && guard++ < 64)
                        {
                            if (candidate != slot &&
                                (uint)candidate < (uint)Inputs.Length &&
                                (uint)candidate < (uint)MockTargets.Length)
                            {
                                MesofaunaTargetDTO target = MockTargets[candidate];
                                if ((target.Flags & MesofaunaBehaviorConstants.TargetFlagValid) != 0)
                                {
                                    float3 delta = AupDeltaToFloat3(target.AUP_Position - selfAup);
                                    float distanceSq = math.lengthsq(delta);
                                    if (distanceSq > 0.25f && distanceSq < bestSq)
                                    {
                                        float3 direction = delta * math.rsqrt(math.max(distanceSq, 0.0001f));
                                        float cone = math.dot(ResolveDirection(input.Forward, fallbackForward), direction);
                                        if (cone > -0.25f)
                                        {
                                            bestSq = distanceSq;
                                            bestSlot = candidate;
                                        }
                                    }
                                }
                            }

                            candidate = (uint)candidate < (uint)TargetHashNext.Length ? TargetHashNext[candidate] : -1;
                        }
                    }
                }
            }

            if (bestSlot < 0)
                return false;

            MesofaunaTargetDTO best = MockTargets[bestSlot];
            targetPosition = input.Position + AupDeltaToFloat3(best.AUP_Position - selfAup);
            targetVelocity = best.Velocity;
            targetHash = best.TargetHashID;
            targetAup = best.AUP_Position;
            return IsFinite(targetPosition) && math.all(math.isfinite(targetAup));
        }

        private bool TryAcquireScent(
            in CognitionInput input,
            float quality,
            float scentMultiplier,
            out float3 targetPosition,
            out double3 targetAup,
            out float3 scentDirection,
            out float signal01,
            out uint targetHash)
        {
            targetPosition = default;
            targetAup = default;
            scentDirection = default;
            signal01 = 0f;
            targetHash = 0u;
            if (!ChemicalBreadcrumbs.IsCreated || ChemicalBreadcrumbCount <= 0)
                return false;

            double3 query = RuntimeToAup(input.Position, input.FloatingOriginOffset);
            int count = math.min(ChemicalBreadcrumbCount, ChemicalBreadcrumbs.Length);
            float bestScore = 0f;
            float3 bestDirection = default;
            float3 bestTargetPosition = default;
            double3 bestTargetAup = default;
            float3 gradient = default;
            uint bestHash = 0u;
            for (int i = 0; i < count; i++)
            {
                ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint waypoint = ChemicalBreadcrumbs[i];
                if (input.CurrentTime > 0f && waypoint.ExpiresAt > 0f && waypoint.ExpiresAt <= input.CurrentTime)
                    continue;

                float radius = math.max(1f, waypoint.RadiusMeters);
                float followRange = math.max(4f, ChemicalBreadcrumbFollowStepMeters * math.lerp(1.15f, 2.75f, quality));
                float effectiveRange = math.max(radius, followRange) * math.max(0.25f, scentMultiplier);
                float rangeSq = effectiveRange * effectiveRange;
                double3 waypointAup = RuntimeToAup(waypoint.RuntimePosition, input.FloatingOriginOffset);
                float3 delta = AupDeltaToFloat3(waypointAup - query);
                float distanceSq = math.lengthsq(delta);
                if (distanceSq <= 0.01f || distanceSq > rangeSq)
                    continue;

                float falloff = MesofaunaBehaviorConstants.Smooth01(1f - math.saturate(distanceSq * math.rcp(math.max(rangeSq, 0.0001f))));
                float channel = math.max(waypoint.Channels.x, waypoint.Channels.y * 0.5f);
                float fearPenalty = waypoint.Channels.z * 0.25f;
                float score = math.saturate((channel - fearPenalty) * math.rcp(32f)) * falloff;
                score *= math.max(0.1f, Tuning.ScentSensitivity * scentMultiplier);
                float3 direction = ResolveDirection(delta, input.Forward);
                gradient += direction * score;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestDirection = direction;
                bestTargetPosition = input.Position + delta;
                bestTargetAup = waypointAup;
                bestHash = HashFloat3(delta, 0x53434E54u);
            }

            if (bestScore <= math.lerp(0.12f, 0.04f, quality))
                return false;

            targetPosition = bestTargetPosition;
            targetAup = bestTargetAup;
            scentDirection = ResolveDirection(gradient, bestDirection);
            signal01 = math.saturate(bestScore);
            targetHash = bestHash;
            return math.all(math.isfinite(targetAup));
        }

        private bool TryResolveObstacleRepulsion(float3 position, float3 desiredDirection, float quality, out float3 repulsion, out float pressure01)
        {
            repulsion = default;
            pressure01 = 0f;
            if (!ThreatVoxelGrid.IsCreated ||
                ThreatVoxelDimensions.x <= 1 ||
                ThreatVoxelDimensions.y <= 1 ||
                ThreatVoxelDimensions.z <= 1)
            {
                return false;
            }

            float probeDistance = math.lerp(1.75f, 4.5f, quality);
            float3 probe = position + (ResolveDirection(desiredDirection, new float3(0f, 0f, 1f)) * probeDistance);
            float3 cell = (probe - ThreatVoxelOrigin) * math.rcp(math.max(ThreatVoxelCellSize, new float3(0.001f)));
            int3 c = new int3((int)math.floor(cell.x), (int)math.floor(cell.y), (int)math.floor(cell.z));
            if (!InVoxelBounds(c))
                return false;

            byte center = ReadVoxel(c);
            if (!IsSolidOrNear(center))
                return false;

            float gx = SignedVoxel(c + new int3(1, 0, 0)) - SignedVoxel(c + new int3(-1, 0, 0));
            float gy = SignedVoxel(c + new int3(0, 1, 0)) - SignedVoxel(c + new int3(0, -1, 0));
            float gz = SignedVoxel(c + new int3(0, 0, 1)) - SignedVoxel(c + new int3(0, 0, -1));
            float3 gradient = new float3(gx, gy, gz);
            repulsion = ResolveDirection(-gradient, -desiredDirection);
            float meanCellMeters = math.max(0.1f, (ThreatVoxelCellSize.x + ThreatVoxelCellSize.y + ThreatVoxelCellSize.z) * MesofaunaBehaviorConstants.InvThree);
            float sdfDistance = ThreatVoxelUsesSignedDistanceEncoding != 0
                ? math.max(0.1f, math.abs(center - 128f) * meanCellMeters * MesofaunaBehaviorConstants.InvThirtyTwo)
                : math.max(0.1f, (1f - math.saturate(center * MesofaunaBehaviorConstants.InvByteMax)) * probeDistance);
            float reciprocalPressure = math.rcp(math.max(0.1f, sdfDistance));
            pressure01 = math.saturate(reciprocalPressure * math.lerp(0.2f, 0.45f, quality));
            return true;
        }

        private float3 ResolveInterceptDirection(
            in MesofaunaStateDTO state,
            double3 targetAup,
            float3 targetVelocity,
            float quality,
            float3 fallbackForward)
        {
            float3 delta = AupDeltaToFloat3(targetAup - state.AUP_Position);
            float distance = MesofaunaBehaviorConstants.FastLengthFromSq(math.lengthsq(delta), MesofaunaBehaviorConstants.DirectionLengthSqEpsilon);
            float leadSeconds = math.clamp(distance * math.rcp(math.max(1f, Tuning.BaseSpeedMetersPerSecond * math.lerp(0.8f, 1.35f, quality))), 0.05f, math.lerp(0.25f, 1.1f, quality));
            float3 predictedLocalDelta = delta + (targetVelocity * leadSeconds);
            return ResolveDirection(predictedLocalDelta, fallbackForward);
        }

        private float3 ResolveThreatPosition(int slot, in CognitionInput input, float3 fallbackForward)
        {
            if (Controls.IsCreated && (uint)slot < (uint)Controls.Length)
            {
                CognitionControl control = Controls[slot];
                if ((control.Flags & (int)CognitionControlFlags.HasOverrideThreatPosition) != 0 &&
                    control.OverrideUntilTime > input.CurrentTime &&
                    IsFinite(control.OverrideThreatPosition))
                {
                    return control.OverrideThreatPosition;
                }
            }

            if ((input.Flags & (int)CognitionInputFlags.HasThreatTarget) != 0 && IsFinite(input.ThreatPosition))
                return input.ThreatPosition;
            if ((input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0 && IsFinite(input.PlayerPosition))
                return input.PlayerPosition;
            return input.Position - fallbackForward;
        }

        private static uint ResolveFleeTargetHash(int slot, in CognitionInput input, uint currentTargetHash)
        {
            return currentTargetHash != 0u
                ? currentTargetHash
                : Hash(slot, input.SpeciesId, 0x464C4545u);
        }

        private float3 ResolveSearchDirection(int slot, in CognitionInput input, float quality, float3 fallbackForward)
        {
            uint hash = Hash(slot, input.SpeciesId, 0x57524D52u);
            float phase = ((hash & 4095u) * 0.0015339808f) + (FrameId * math.lerp(0.015625f, 0.03125f, quality));
            ApproxSinCosBhaskara(phase, out float sin, out float cos);
            float3 lateral = new float3(sin, ApproxSinBhaskara(phase * 0.37f) * 0.25f, cos);
            return ResolveDirection((fallbackForward * math.lerp(0.35f, 0.72f, quality)) + lateral, fallbackForward);
        }

        private static void ApproxSinCosBhaskara(float radians, out float sin, out float cos)
        {
            sin = ApproxSinBhaskara(radians);
            cos = ApproxSinBhaskara(radians + 1.57079632679f);
        }

        private static float ApproxSinBhaskara(float radians)
        {
            float safe = math.select(radians, 0f, !math.isfinite(radians));
            float cycle = safe * 0.15915494309189535f;
            float wrapped = cycle - math.floor(cycle);
            float x = wrapped * 6.28318530718f;
            bool secondHalf = x > math.PI;
            float mirrored = math.select(x, 6.28318530718f - x, secondHalf);
            float sign = math.select(1f, -1f, secondHalf);
            float shape = mirrored * (math.PI - mirrored);
            float denominator = math.max(0.0001f, (5f * math.PI * math.PI) - (4f * shape));
            return math.clamp(sign * (16f * shape) * math.rcp(denominator), -1f, 1f);
        }

        private float ResolveSpeedScalar(byte state, in CognitionInput input, float quality, float aggressionMultiplier, float speedMultiplier)
        {
            float baseSpeed = state switch
            {
                MesofaunaBehaviorConstants.StateHunt => math.lerp(0.95f, 1.32f, quality),
                MesofaunaBehaviorConstants.StateFlee => math.lerp(1.05f, 1.45f, quality),
                MesofaunaBehaviorConstants.StateTrackScent => math.lerp(0.62f, 0.9f, quality),
                MesofaunaBehaviorConstants.StateSearch => math.lerp(0.52f, 0.82f, quality),
                _ => 0.35f
            };
            float hungerBoost = math.saturate(input.AggressionWeight * aggressionMultiplier) * 0.12f;
            return math.clamp((baseSpeed + hungerBoost) * speedMultiplier, 0.2f, 1.85f);
        }

        private int ResolveStateTimeoutTicks(float quality)
        {
            float seconds = math.clamp(
                math.isfinite(Tuning.StateTimeoutSeconds) ? Tuning.StateTimeoutSeconds : 4.5f,
                0.1f,
                60f);
            float logicalTicksPerSecond = math.lerp(5f, 60f, MesofaunaBehaviorConstants.Smooth01(quality));
            return math.clamp((int)math.round(seconds * logicalTicksPerSecond), 1, ushort.MaxValue);
        }

        private MesofaunaSpeciesProfileDTO ResolveSpeciesProfile(int speciesId)
        {
            if (!SpeciesProfiles.IsCreated || SpeciesProfiles.Length <= 0 || speciesId == 0)
                return MesofaunaSpeciesProfileDTO.CreateDefault(0u);

            uint speciesHash = unchecked((uint)speciesId);
            uint mask = (uint)(SpeciesProfiles.Length - 1);
            int probes = math.min(SpeciesProfiles.Length, MesofaunaBehaviorConstants.SpeciesProfileCapacity);
            for (int probe = 0; probe < probes; probe++)
            {
                int profileIndex = (int)((speciesHash + (uint)probe) & mask);
                MesofaunaSpeciesProfileDTO profile = SpeciesProfiles[profileIndex];
                if (profile.SpeciesHash == speciesHash && (profile.Flags & 1) != 0)
                    return profile;

                if (profile.SpeciesHash == 0u && profile.Flags == 0)
                    break;
            }

            return MesofaunaSpeciesProfileDTO.CreateDefault(speciesHash);
        }

        private static float ResolveProfileScalar(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 1f;
        }

        private void WriteVisualAndOutput(
            int slot,
            in CognitionInput input,
            ref MesofaunaStateDTO state,
            float3 desiredDirection,
            float speedScalar,
            float scentSignal01,
            float obstaclePressure01,
            uint targetHash,
            double3 targetAup,
            bool hasTargetAup,
            bool shouldAttack)
        {
            desiredDirection = ResolveDirection(desiredDirection, input.Forward);
            bool validTargetAup = hasTargetAup && math.all(math.isfinite(targetAup));
            MesofaunaVisualSyncDTO visual = default;
            visual.DesiredVelocity = desiredDirection * math.max(0.5f, Tuning.BaseSpeedMetersPerSecond) * speedScalar;
            visual.SpeedScalar = speedScalar;
            visual.CurrentState = state.CurrentState;
            visual.PreviousState = state.PreviousState;
            visual.TargetHashID = targetHash;
            visual.ScentSignal01 = math.saturate(scentSignal01);
            visual.ObstaclePressure01 = math.saturate(obstaclePressure01);
            visual.Flags = state.CurrentState == MesofaunaBehaviorConstants.StateHunt
                ? MesofaunaBehaviorConstants.VisualFlagHunt
                : (ushort)0;
            visual.TargetAup = validTargetAup ? targetAup : state.AUP_Position;
            visual.TargetDistanceMeters = validTargetAup
                ? MesofaunaBehaviorConstants.FastLengthFromSq(math.lengthsq(AupDeltaToFloat3(visual.TargetAup - state.AUP_Position)), 0f)
                : 0f;
            visual.TargetFlags = validTargetAup ? MesofaunaBehaviorConstants.VisualTargetFlagValid : 0u;
            if ((uint)slot < (uint)VisualSync.Length)
                VisualSync[slot] = visual;

            PackedCognitionOutput output = Outputs[slot];
            output.DesiredDirection = desiredDirection;
            output.ForceMultiplier = math.clamp(speedScalar, 0.25f, 1.5f);
            output.SpeedMultiplier = math.clamp(speedScalar, 0.25f, 1.5f);
            output.TurnMultiplier = math.lerp(0.85f, 1.25f, MesofaunaBehaviorConstants.Smooth01(GlobalQualityWeight));
            output.LegacyState = MapLegacyState(state.CurrentState);
            output.StateMask = 0u;
            output.OutputFlags &= (uint)(CognitionOutputFlags.RetinalBlind | CognitionOutputFlags.EcoHeadless);
            if (state.CurrentState == MesofaunaBehaviorConstants.StateHunt)
            {
                output.OutputFlags |= (uint)CognitionOutputFlags.EmitThreatPulse;
                if (shouldAttack)
                    output.OutputFlags |= (uint)CognitionOutputFlags.ShouldAttack;
            }
            Outputs[slot] = output;
            if ((uint)slot < (uint)ChosenStates.Length)
                ChosenStates[slot] = state.CurrentState;
        }

        private static int MapLegacyState(byte state)
        {
            return state switch
            {
                MesofaunaBehaviorConstants.StateHunt => (int)FaunaBrain.AIState.Aggressive,
                MesofaunaBehaviorConstants.StateFlee => (int)FaunaBrain.AIState.Retreat,
                MesofaunaBehaviorConstants.StateTrackScent => (int)FaunaBrain.AIState.Investigate,
                MesofaunaBehaviorConstants.StateIdle => (int)FaunaBrain.AIState.Idle,
                _ => (int)FaunaBrain.AIState.Wander
            };
        }

        private byte ReadVoxel(int3 coord)
        {
            if (!InVoxelBounds(coord))
                return 0;

            int index = coord.x + (coord.y * ThreatVoxelDimensions.x) + (coord.z * ThreatVoxelDimensions.x * ThreatVoxelDimensions.y);
            return (uint)index < (uint)ThreatVoxelGrid.Length ? ThreatVoxelGrid[index] : (byte)0;
        }

        private float SignedVoxel(int3 coord)
        {
            if (!InVoxelBounds(coord))
                return 0f;
            return ReadVoxel(coord) - 128f;
        }

        private bool IsSolidOrNear(byte value)
        {
            return ThreatVoxelUsesSignedDistanceEncoding != 0
                ? value >= 96
                : value >= ThreatVoxelSolidThreshold;
        }

        private bool InVoxelBounds(int3 coord)
        {
            return coord.x >= 0 && coord.y >= 0 && coord.z >= 0 &&
                   coord.x < ThreatVoxelDimensions.x &&
                   coord.y < ThreatVoxelDimensions.y &&
                   coord.z < ThreatVoxelDimensions.z;
        }

        private static double3 RuntimeToAup(float3 runtimePosition, double3 floatingOriginOffset)
        {
            if (!IsFinite(runtimePosition))
                runtimePosition = float3.zero;
            if (!math.all(math.isfinite(floatingOriginOffset)))
                floatingOriginOffset = double3.zero;
            return new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + floatingOriginOffset;
        }

        private static float3 AupDeltaToFloat3(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static double3 ResolveAupOrRuntime(in AbsoluteUniversePositionBlit128 aup, float3 runtimePosition, double3 floatingOriginOffset)
        {
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double3 absolute = new double3(
                (aup.GridX * cellSize) + aup.Local.x,
                (aup.GridY * cellSize) + aup.Local.y,
                (aup.GridZ * cellSize) + aup.Local.z);
            if (math.all(math.isfinite(absolute)) &&
                (math.any(absolute != double3.zero) || math.all(runtimePosition == float3.zero)))
            {
                return absolute;
            }

            return RuntimeToAup(runtimePosition, floatingOriginOffset);
        }

        private static int3 ResolveBucket(float3 position, float3 boundsMin, float cellSize)
        {
            float inv = math.rcp(math.max(0.001f, cellSize));
            float3 local = math.max(position - boundsMin, float3.zero);
            return new int3((int)math.floor(local.x * inv), (int)math.floor(local.y * inv), (int)math.floor(local.z * inv));
        }

        private static int HashBucket(int3 bucket)
        {
            unchecked
            {
                return (int)(((uint)bucket.x * 73856093u) ^ ((uint)bucket.y * 19349663u) ^ ((uint)bucket.z * 83492791u));
            }
        }

        private static uint Hash(int slot, int speciesId, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)slot) * 16777619u;
                hash = (hash ^ (uint)speciesId) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                return hash == 0u ? salt : hash;
            }
        }

        private static uint HashFloat3(float3 value, uint salt)
        {
            unchecked
            {
                uint hash = salt == 0u ? 2166136261u : salt;
                hash = (hash ^ math.asuint(value.x)) * 16777619u;
                hash = (hash ^ math.asuint(value.y)) * 16777619u;
                hash = (hash ^ math.asuint(value.z)) * 16777619u;
                return hash == 0u ? salt : hash;
            }
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float3 ResolveDirection(float3 direction, float3 fallback)
        {
            float lengthSq = IsFinite(direction) ? math.lengthsq(direction) : 0f;
            if (!math.isfinite(lengthSq) || lengthSq <= MesofaunaBehaviorConstants.DirectionLengthSqEpsilon)
            {
                direction = fallback;
                lengthSq = IsFinite(direction) ? math.lengthsq(direction) : 0f;
            }

            if (!math.isfinite(lengthSq) || lengthSq <= MesofaunaBehaviorConstants.DirectionLengthSqEpsilon)
                return new float3(0f, 0f, 1f);
            return direction * math.rsqrt(math.max(lengthSq, MesofaunaBehaviorConstants.DirectionLengthSqEpsilon));
        }
    }
}
