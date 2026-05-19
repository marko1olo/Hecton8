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
        [FieldOffset(48)] public uint Reserved0;
        [FieldOffset(52)] public uint Reserved1;
        [FieldOffset(56)] public uint Reserved2;
        [FieldOffset(60)] public uint Reserved3;

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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
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
        [FieldOffset(60)] public uint Reserved0;
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

    internal static class MesofaunaBehaviorConstants
    {
        internal const int StateDtoSizeBytes = 64;
        internal const int TargetDtoSizeBytes = 64;
        internal const int VisualSyncDtoSizeBytes = 32;
        internal const int TelemetryEntrySizeBytes = 64;
        internal const int TuningDtoSizeBytes = 32;
        internal const int TelemetryCapacity = 300;
        internal const int TargetSpatialHashBucketCount = 1024;
        internal const int StateIdle = 0;
        internal const int StateSearch = 1;
        internal const int StateHunt = 2;
        internal const int StateFlee = 3;
        internal const int StateTrackScent = 4;
        internal const uint TelemetryContextHash = 0x4D455346u; // MESF
        internal const uint DumpFailureTelemetryHash = 0x4D44464Cu; // MDFL
        internal const uint DumpReasonFaultHash = 0x4D464C54u; // MFLT

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
                   OffsetOf<MesofaunaStateDTO>(nameof(MesofaunaStateDTO.Reserved0)) == 48 &&
                   UnsafeUtility.SizeOf<MesofaunaTargetDTO>() == TargetDtoSizeBytes &&
                   UnsafeUtility.SizeOf<MesofaunaVisualSyncDTO>() == VisualSyncDtoSizeBytes &&
                   UnsafeUtility.SizeOf<MesofaunaTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<MesofaunaTuningDTO>() == TuningDtoSizeBytes;
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
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct InitializeMesofaunaStateJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<MesofaunaStateDTO> States;
        [NativeDisableParallelForRestriction] public NativeArray<MesofaunaTargetDTO> MockTargets;
        [NativeDisableParallelForRestriction] public NativeArray<MesofaunaVisualSyncDTO> VisualSync;
        [NativeDisableParallelForRestriction] public NativeArray<MesofaunaTelemetryEntry> TelemetryRing;
        [NativeDisableParallelForRestriction] public NativeArray<MesofaunaTuningDTO> Tuning;
        [NativeDisableParallelForRestriction] public NativeArray<int> TargetHashBucketHeads;
        [NativeDisableParallelForRestriction] public NativeArray<int> TargetHashNext;
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
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMesofaunaMockTargetsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> ActiveSlots;
        [ReadOnly] public NativeArray<CognitionInput> Inputs;
        [NativeDisableParallelForRestriction] public NativeArray<MesofaunaTargetDTO> MockTargets;
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
                float angle = ((hash & 1023u) * 0.006135923151f) + (FrameId * 0.01171875f);
                float radius = math.lerp(6f, 18f, q) + ((slot & 7) * 0.5f);
                float3 offset = new float3(math.cos(angle) * radius, ((slot & 3) - 1.5f) * 0.75f, math.sin(angle) * radius);
                position = input.Position + offset;
                velocity = ResolveDirection(new float3(-offset.z, 0f, offset.x), input.Forward) * math.lerp(1.0f, 2.75f, q);
                threatClass = 3;
            }

            if (!IsFinite(position))
                position = float3.zero;
            if (!IsFinite(velocity))
                velocity = float3.zero;

            target.AUP_Position = new double3(position.x, position.y, position.z) + input.FloatingOriginOffset;
            target.Velocity = velocity;
            target.TargetHashID = Hash(slot, input.SpeciesId, 0x54524754u);
            target.SpeciesHash = unchecked((uint)input.SpeciesId);
            target.Flags = 1;
            target.ThreatClass = threatClass;
            target.RadiusMeters = math.max(0.5f, input.AttackRange);
            MockTargets[slot] = target;
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float3 ResolveDirection(float3 direction, float3 fallback)
        {
            if (!IsFinite(direction) || math.lengthsq(direction) <= 0.0001f)
                direction = fallback;
            if (!IsFinite(direction) || math.lengthsq(direction) <= 0.0001f)
                return new float3(0f, 0f, 1f);
            return direction * math.rsqrt(math.max(math.lengthsq(direction), 0.0001f));
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
    internal unsafe struct MesofaunaBehaviorJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> ActiveSlots;
        [ReadOnly] public NativeArray<CognitionInput> Inputs;
        [ReadOnly] public NativeArray<MesofaunaTargetDTO> MockTargets;
        [NativeDisableParallelForRestriction] public NativeArray<MesofaunaStateDTO> States;
        [NativeDisableParallelForRestriction] public NativeArray<MesofaunaVisualSyncDTO> VisualSync;
        [NativeDisableParallelForRestriction] public NativeArray<PackedCognitionOutput> Outputs;
        [NativeDisableParallelForRestriction] public NativeArray<byte> ChosenStates;
        [ReadOnly] public NativeArray<int> TargetHashBucketHeads;
        [ReadOnly] public NativeArray<int> TargetHashNext;
        [ReadOnly] public NativeArray<byte> ThreatVoxelGrid;
        [ReadOnly] public NativeArray<ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint> ChemicalBreadcrumbs;
        public int3 ThreatVoxelDimensions;
        public float3 ThreatVoxelOrigin;
        public float3 ThreatVoxelCellSize;
        public byte ThreatVoxelSolidThreshold;
        public int ThreatVoxelUsesSignedDistanceEncoding;
        public int ChemicalBreadcrumbCount;
        public float ChemicalBreadcrumbFollowStepMeters;
        public float3 SwarmBoundsMin;
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
            state.AUP_Position = RuntimeToAup(input.Position, input.FloatingOriginOffset);
            state.AggressionScalar = math.saturate(input.AggressionWeight);

            int stride = math.clamp(SliceModulo, 1, 10);
            bool continuityOnly = ((FrameId + slot) % stride) != 0;
            if (continuityOnly)
            {
                ApplyContinuity(slot, in input, ref state);
                return;
            }

            float quality = MesofaunaBehaviorConstants.Smooth01(GlobalQualityWeight);
            float visionRadius = math.max(4f, VisionRadiusMeters);
            float visionRadiusSq = visionRadius * visionRadius;
            float3 fallbackForward = ResolveDirection(input.Forward, new float3(0f, 0f, 1f));
            float3 desiredDirection = fallbackForward;
            float3 targetPosition = input.Position + fallbackForward;
            float3 targetVelocity = float3.zero;
            uint targetHash = state.TargetHashID;
            float scentSignal = 0f;
            float targetDistanceSq = float.MaxValue;
            byte nextState = state.CurrentState;

            bool canFlee = (input.Flags & (int)CognitionInputFlags.CanFlee) != 0;
            float fleeThreshold = math.clamp(input.FleeHealthThreshold > 0f ? input.FleeHealthThreshold : 0.25f, 0.05f, 1f);
            bool lowHealth = input.HealthNormalized > 0f && input.HealthNormalized <= fleeThreshold;
            bool hardFear = input.FearPressure01 >= math.lerp(0.62f, 0.42f, quality);
            if (canFlee && (lowHealth || hardFear))
            {
                nextState = MesofaunaBehaviorConstants.StateFlee;
                targetPosition = ResolveThreatPosition(in input, fallbackForward);
                desiredDirection = ResolveDirection(input.Position - targetPosition, fallbackForward);
                targetHash = Hash(slot, input.SpeciesId, 0x464C4545u);
            }
            else if (TryResolveDirectTarget(in input, visionRadiusSq, out targetPosition, out targetVelocity, out targetHash))
            {
                nextState = MesofaunaBehaviorConstants.StateHunt;
                targetDistanceSq = math.lengthsq(targetPosition - input.Position);
                desiredDirection = ResolveInterceptDirection(in state, input.Position, targetPosition, targetVelocity, in input, quality, fallbackForward);
            }
            else if (TryAcquireSpatialHashTarget(slot, in input, visionRadiusSq, fallbackForward, out targetPosition, out targetVelocity, out targetHash))
            {
                nextState = MesofaunaBehaviorConstants.StateHunt;
                targetDistanceSq = math.lengthsq(targetPosition - input.Position);
                desiredDirection = ResolveInterceptDirection(in state, input.Position, targetPosition, targetVelocity, in input, quality, fallbackForward);
            }
            else if (TryAcquireScent(in input, quality, out float3 scentDirection, out scentSignal, out targetHash))
            {
                nextState = MesofaunaBehaviorConstants.StateTrackScent;
                desiredDirection = scentDirection;
            }
            else
            {
                nextState = state.StateTimerTicks > math.max(1, Tuning.IdleToSearchTicks)
                    ? (byte)MesofaunaBehaviorConstants.StateSearch
                    : (byte)MesofaunaBehaviorConstants.StateIdle;
                desiredDirection = ResolveSearchDirection(slot, in input, quality, fallbackForward);
                targetHash = Hash(slot, input.SpeciesId, 0x53454152u);
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

            float speed = ResolveSpeedScalar(nextState, in input, quality);
            state.TargetHashID = targetHash;
            state.Velocity = desiredDirection * speed * math.max(0.5f, Tuning.BaseSpeedMetersPerSecond);
            bool shouldAttack = nextState == MesofaunaBehaviorConstants.StateHunt &&
                                input.AttackRange > 0f &&
                                targetDistanceSq <= input.AttackRange * input.AttackRange;
            WriteVisualAndOutput(slot, in input, ref state, desiredDirection, speed, scentSignal, obstaclePressure, targetHash, shouldAttack);
        }

        private void ApplyContinuity(int slot, in CognitionInput input, ref MesofaunaStateDTO state)
        {
            float3 direction = ResolveDirection(state.Velocity, ResolveDirection(input.Forward, new float3(0f, 0f, 1f)));
            float speed = math.clamp(math.length(state.Velocity) * math.rcp(math.max(0.5f, Tuning.BaseSpeedMetersPerSecond)), 0.45f, 1.35f);
            if (state.CurrentState == MesofaunaBehaviorConstants.StateIdle)
                speed = math.min(speed, 0.55f);
            if (state.StateTimerTicks < ushort.MaxValue)
                state.StateTimerTicks++;
            WriteVisualAndOutput(slot, in input, ref state, direction, speed, 0f, 0f, state.TargetHashID, false);
        }

        private bool TryResolveDirectTarget(
            in CognitionInput input,
            float visionRadiusSq,
            out float3 targetPosition,
            out float3 targetVelocity,
            out uint targetHash)
        {
            targetPosition = default;
            targetVelocity = default;
            targetHash = 0u;
            bool hasPrey = (input.Flags & (int)CognitionInputFlags.HasPreyTarget) != 0 && IsFinite(input.PreyPosition);
            bool hasPlayer = (input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0 && IsFinite(input.PlayerPosition);
            if (hasPrey)
            {
                targetPosition = input.PreyPosition;
                targetVelocity = input.PackTargetVelocity;
                targetHash = HashFloat3(targetPosition, 0x50524559u);
            }
            else if (hasPlayer && input.AggressionWeight >= 0.28f)
            {
                targetPosition = input.PlayerPosition;
                targetVelocity = input.PlayerVelocity;
                targetHash = HashFloat3(targetPosition, 0x504C5952u);
            }
            else
            {
                return false;
            }

            float3 toTarget = targetPosition - input.Position;
            if (!IsFinite(toTarget) || math.lengthsq(toTarget) > visionRadiusSq)
                return false;

            return true;
        }

        private bool TryAcquireSpatialHashTarget(
            int slot,
            in CognitionInput input,
            float visionRadiusSq,
            float3 fallbackForward,
            out float3 targetPosition,
            out float3 targetVelocity,
            out uint targetHash)
        {
            targetPosition = default;
            targetVelocity = default;
            targetHash = 0u;
            if (!TargetHashBucketHeads.IsCreated || !TargetHashNext.IsCreated || !MockTargets.IsCreated)
                return false;

            int mask = TargetHashBucketMask <= 0 ? MesofaunaBehaviorConstants.TargetSpatialHashBucketCount - 1 : TargetHashBucketMask;
            int3 center = ResolveBucket(input.Position, SwarmBoundsMin, 8f);
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
                                if ((target.Flags & 1) != 0)
                                {
                                    float3 delta = AupDeltaToFloat3(target.AUP_Position - RuntimeToAup(input.Position, input.FloatingOriginOffset));
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
            targetPosition = input.Position + AupDeltaToFloat3(best.AUP_Position - RuntimeToAup(input.Position, input.FloatingOriginOffset));
            targetVelocity = best.Velocity;
            targetHash = best.TargetHashID;
            return IsFinite(targetPosition);
        }

        private bool TryAcquireScent(
            in CognitionInput input,
            float quality,
            out float3 scentDirection,
            out float signal01,
            out uint targetHash)
        {
            scentDirection = default;
            signal01 = 0f;
            targetHash = 0u;
            if (!ChemicalBreadcrumbs.IsCreated || ChemicalBreadcrumbCount <= 0)
                return false;

            double3 query = RuntimeToAup(input.Position, input.FloatingOriginOffset);
            int count = math.min(ChemicalBreadcrumbCount, ChemicalBreadcrumbs.Length);
            float range = math.max(4f, ChemicalBreadcrumbFollowStepMeters * math.lerp(1.15f, 2.75f, quality));
            float rangeSq = range * range;
            float bestScore = 0f;
            float3 bestDirection = default;
            uint bestHash = 0u;
            for (int i = 0; i < count; i++)
            {
                ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint waypoint = ChemicalBreadcrumbs[i];
                float3 delta = AupDeltaToFloat3(waypoint.AbsolutePositionDouble - query);
                float distanceSq = math.lengthsq(delta);
                if (distanceSq <= 0.01f || distanceSq > rangeSq)
                    continue;

                float channel = math.max(waypoint.Channels.x, waypoint.Channels.y * 0.5f);
                float fearPenalty = waypoint.Channels.z * 0.25f;
                float score = math.saturate((channel - fearPenalty) * math.rcp(32f)) * (1f - math.saturate(distanceSq * math.rcp(rangeSq)));
                score *= math.max(0.1f, Tuning.ScentSensitivity);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestDirection = ResolveDirection(delta, input.Forward);
                bestHash = HashFloat3(waypoint.RuntimePosition, 0x53434E54u);
            }

            if (bestScore <= math.lerp(0.12f, 0.04f, quality))
                return false;

            scentDirection = bestDirection;
            signal01 = math.saturate(bestScore);
            targetHash = bestHash;
            return true;
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
            float3 cell = (probe - ThreatVoxelOrigin) / math.max(ThreatVoxelCellSize, new float3(0.001f));
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
            pressure01 = ThreatVoxelUsesSignedDistanceEncoding != 0
                ? math.saturate((center - 96f) * math.rcp(96f))
                : math.saturate(center * (1f / 255f));
            return true;
        }

        private float3 ResolveInterceptDirection(
            in MesofaunaStateDTO state,
            float3 runtimePosition,
            float3 targetPosition,
            float3 targetVelocity,
            in CognitionInput input,
            float quality,
            float3 fallbackForward)
        {
            double3 targetAup = RuntimeToAup(targetPosition, input.FloatingOriginOffset);
            float3 delta = AupDeltaToFloat3(targetAup - state.AUP_Position);
            float distance = math.sqrt(math.max(math.lengthsq(delta), 0.0001f));
            float leadSeconds = math.clamp(distance * math.rcp(math.max(1f, Tuning.BaseSpeedMetersPerSecond * math.lerp(0.8f, 1.35f, quality))), 0.05f, math.lerp(0.25f, 1.1f, quality));
            float3 predicted = targetPosition + (targetVelocity * leadSeconds);
            return ResolveDirection(predicted - runtimePosition, fallbackForward);
        }

        private float3 ResolveThreatPosition(in CognitionInput input, float3 fallbackForward)
        {
            if ((input.Flags & (int)CognitionInputFlags.HasThreatTarget) != 0 && IsFinite(input.ThreatPosition))
                return input.ThreatPosition;
            if ((input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0 && IsFinite(input.PlayerPosition))
                return input.PlayerPosition;
            return input.Position - fallbackForward;
        }

        private float3 ResolveSearchDirection(int slot, in CognitionInput input, float quality, float3 fallbackForward)
        {
            uint hash = Hash(slot, input.SpeciesId, 0x57524D52u);
            float phase = ((hash & 4095u) * 0.0015339808f) + (FrameId * math.lerp(0.015625f, 0.03125f, quality));
            float3 lateral = new float3(math.sin(phase), math.sin(phase * 0.37f) * 0.25f, math.cos(phase));
            return ResolveDirection((fallbackForward * math.lerp(0.35f, 0.72f, quality)) + lateral, fallbackForward);
        }

        private float ResolveSpeedScalar(byte state, in CognitionInput input, float quality)
        {
            float baseSpeed = state switch
            {
                MesofaunaBehaviorConstants.StateHunt => math.lerp(0.95f, 1.32f, quality),
                MesofaunaBehaviorConstants.StateFlee => math.lerp(1.05f, 1.45f, quality),
                MesofaunaBehaviorConstants.StateTrackScent => math.lerp(0.62f, 0.9f, quality),
                MesofaunaBehaviorConstants.StateSearch => math.lerp(0.52f, 0.82f, quality),
                _ => 0.35f
            };
            float hungerBoost = math.saturate(input.AggressionWeight) * 0.12f;
            return math.clamp(baseSpeed + hungerBoost, 0.2f, 1.55f);
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
            bool shouldAttack)
        {
            desiredDirection = ResolveDirection(desiredDirection, input.Forward);
            MesofaunaVisualSyncDTO visual = default;
            visual.DesiredVelocity = desiredDirection * math.max(0.5f, Tuning.BaseSpeedMetersPerSecond) * speedScalar;
            visual.SpeedScalar = speedScalar;
            visual.CurrentState = state.CurrentState;
            visual.PreviousState = state.PreviousState;
            visual.TargetHashID = targetHash;
            visual.ScentSignal01 = math.saturate(scentSignal01);
            visual.ObstaclePressure01 = math.saturate(obstaclePressure01);
            visual.Flags = (ushort)(state.CurrentState == MesofaunaBehaviorConstants.StateHunt ? 1 : 0);
            if ((uint)slot < (uint)VisualSync.Length)
                VisualSync[slot] = visual;

            PackedCognitionOutput output = Outputs[slot];
            output.DesiredDirection = desiredDirection;
            output.ForceMultiplier = math.clamp(speedScalar, 0.25f, 1.5f);
            output.SpeedMultiplier = math.clamp(speedScalar, 0.25f, 1.5f);
            output.TurnMultiplier = math.lerp(0.85f, 1.25f, MesofaunaBehaviorConstants.Smooth01(GlobalQualityWeight));
            output.LegacyState = MapLegacyState(state.CurrentState);
            output.StateMask = 0u;
            output.OutputFlags &= (uint)CognitionOutputFlags.RetinalBlind;
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
            if (!IsFinite(direction) || math.lengthsq(direction) <= 0.0001f)
                direction = fallback;
            if (!IsFinite(direction) || math.lengthsq(direction) <= 0.0001f)
                return new float3(0f, 0f, 1f);
            return direction * math.rsqrt(math.max(math.lengthsq(direction), 0.0001f));
        }
    }
}
