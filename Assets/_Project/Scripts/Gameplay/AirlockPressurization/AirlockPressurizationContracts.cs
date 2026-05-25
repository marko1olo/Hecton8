// ============================================================================
// HECTON-8 - AirlockPressurizationContracts.cs
// SHINOBU_338 unmanaged airlock pressure, water, gas, collision, and telemetry contracts.
// ============================================================================

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Gameplay.AirlockPressurization
{
    public static class AirlockPressurizationConstants
    {
        public const int MaxActiveAirlocks = 50;
        public const int TelemetryFrameCount = 300;
        public const int MaxHardwareProfiles = 32;
        public const int SignalScratchCapacity = MaxActiveAirlocks;
        public const float LitersPerCubicMeter = 1000f;
        public const float CubicMetersPerLiter = 0.001f;
        public const float MinimumPressureAtm = 0.05f;
        public const float SurfacePressureAtm = 1f;
        public const float AtmospherePerTenMeters = 1f;
        public const float MinimumDenominator = 0.0001f;
        public const float WaterEqualizedLiters = 5f;
        public const float PressureEqualizedAtm = 0.03f;
        public const float DefaultPumpEvacuationSpeedLps = 260f;
        public const float DefaultEqualizationCurveExponent = 1.75f;
        public const float DefaultPowerDrawWatts = 1400f;
        public const float DefaultBreachAreaM2 = 0.18f;
        public const float DefaultDischargeCoefficient = 0.62f;
        public const float ViolentPressureDeltaAtm = 4f;
        public const float CatastrophicWaterRatio = 0.92f;
        public const float CatastrophicStressScaleAtm = 48f;
        public const uint AgentHash = 0x53333338u;
        public const uint HeavyPumpHash = 0x48504D50u;
        public const uint ViolentBubblesHash = 0x56425542u;
        public const uint CondensationFogHash = 0x43464F47u;
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_338.bin";
    }

    public static class AirlockPressurizationBufferIds
    {
        public const BufferID AirlockStates = (BufferID)73380;
        public const BufferID Tuning = (BufferID)73381;
        public const BufferID DoorPoses = (BufferID)73382;
        public const BufferID ExchangeIndices = (BufferID)73383;
        public const BufferID EvaluationResults = (BufferID)73384;
        public const BufferID BulkheadIntents = (BufferID)73385;
        public const BufferID VfxSignals = (BufferID)73386;
        public const BufferID AcousticSignals = (BufferID)73387;
        public const BufferID TelemetryRing = (BufferID)73388;
        public const BufferID TelemetryCursor = (BufferID)73389;
        public const BufferID HardwareProfiles = (BufferID)73390;
        public const BufferID DebugGizmos = (BufferID)73391;
        public const BufferID DumpRequested = (BufferID)73392;
    }

    public static class AirlockCycleFlags
    {
        public const uint None = 0u;
        public const uint Pumping = 1u << 0;
        public const uint Equalizing = 1u << 1;
        public const uint InnerOpen = 1u << 2;
        public const uint OuterOpen = 1u << 3;
        public const uint ForceInnerOpen = 1u << 4;
        public const uint ForceOuterOpen = 1u << 5;
        public const uint CollisionBlocked = 1u << 6;
        public const uint CatastrophicFlood = 1u << 7;
        public const uint AcousticPump = 1u << 8;
        public const uint ViolentBubbles = 1u << 9;
        public const uint CondensationFog = 1u << 10;
        public const uint AtomicAbort = 1u << 11;
        public const uint MockGenerated = 1u << 12;
        public const uint NonFinite = 1u << 31;
    }

    public static class AirlockDoorPoseFlags
    {
        public const uint None = 0u;
        public const uint Valid = 1u << 0;
        public const uint OuterFaceSubmerged = 1u << 1;
        public const uint CameraRelativeVfxResolved = 1u << 2;
        public const uint NonFinite = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AirlockStateDTO
    {
        [FieldOffset(0)] public uint InnerRoomHashID;
        [FieldOffset(4)] public uint OuterRoomHashID;
        [FieldOffset(8)] public float CurrentWaterVolumeLiters;
        [FieldOffset(12)] public float CurrentPressureATM;
        [FieldOffset(16)] public uint CycleStateFlags;
        [FieldOffset(20)] public float CycleTimer;
        [FieldOffset(24)] private uint _pad0;
        [FieldOffset(28)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AirlockTuningDTO
    {
        [FieldOffset(0)] public float PumpEvacuationSpeedLps;
        [FieldOffset(4)] public float MaxWaterVolumeLiters;
        [FieldOffset(8)] public float ChamberVolumeLiters;
        [FieldOffset(12)] public float EqualizationCurveExponent;
        [FieldOffset(16)] public float PowerDrawWatts;
        [FieldOffset(20)] public float AvailablePower01;
        [FieldOffset(24)] public float ExternalDepthMeters;
        [FieldOffset(28)] public float BreachAreaM2;
        [FieldOffset(32)] public float DischargeCoefficient;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float PressureEqualizedAtm;
        [FieldOffset(44)] public float WaterEqualizedLiters;
        [FieldOffset(48)] public float ExternalPressureAtm;
        [FieldOffset(52)] public float RoomPressureAtm;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct AirlockDoorPoseDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition DoorAup;
        [FieldOffset(48)] public float3 DoorNormal;
        [FieldOffset(60)] public float WidthMeters;
        [FieldOffset(64)] public float HeightMeters;
        [FieldOffset(68)] public uint DoorHashID;
        [FieldOffset(72)] public uint EdgeHashID;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public float ExternalDepthMeters;
        [FieldOffset(84)] public float HeadMeters;
        [FieldOffset(88)] public uint Frame;
        [FieldOffset(92)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AirlockExchangeIndexDTO
    {
        [FieldOffset(0)] public int FluidCompartmentIndex;
        [FieldOffset(4)] public int AtmosphereCellIndex;
        [FieldOffset(8)] public int OwnerIndex;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AirlockEvaluationResultDTO
    {
        [FieldOffset(0)] public float WaterDeltaLiters;
        [FieldOffset(4)] public float PressureDeltaAtm;
        [FieldOffset(8)] public float TargetPressureAtm;
        [FieldOffset(12)] public float VfxIntensity01;
        [FieldOffset(16)] public float PumpMovedLiters;
        [FieldOffset(20)] public float TorricelliMovedLiters;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint EffectHash;
        [FieldOffset(40)] public float GasExchange01;
        [FieldOffset(44)] public float StressSpike01;
        [FieldOffset(48)] public uint EdgeHashID;
        [FieldOffset(52)] public uint InnerRoomHashID;
        [FieldOffset(56)] public uint OuterRoomHashID;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AirlockTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveCycles;
        [FieldOffset(8)] public float TotalWaterDisplacedLiters;
        [FieldOffset(12)] public float MaxPressureDeltaAtm;
        [FieldOffset(16)] public uint SolverWallMicroseconds;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint LastStateHash;
        [FieldOffset(28)] public uint ForcedFloodEvents;
        [FieldOffset(32)] public uint CollisionBlockedCount;
        [FieldOffset(36)] public float MaxWaterVolumeLiters;
        [FieldOffset(40)] public float MinPressureAtm;
        [FieldOffset(44)] public float TickIntervalSeconds;
        [FieldOffset(48)] public uint VfxSignals;
        [FieldOffset(52)] public uint AcousticSignals;
        [FieldOffset(56)] public uint NonFiniteCount;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AirlockHardwareProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float ChamberVolumeLiters;
        [FieldOffset(8)] public float MaxWaterVolumeLiters;
        [FieldOffset(12)] public float PumpEvacuationSpeedLps;
        [FieldOffset(16)] public float EqualizationCurveExponent;
        [FieldOffset(20)] public float PowerDrawWatts;
        [FieldOffset(24)] public float BreachAreaM2;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AirlockDebugGizmoDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition DoorAup;
        [FieldOffset(48)] public float CurrentWaterVolumeLiters;
        [FieldOffset(52)] public float MaxWaterVolumeLiters;
        [FieldOffset(56)] public float CurrentPressureAtm;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AirlockSelfAuditResultDTO
    {
        [FieldOffset(0)] public uint Flags;
        [FieldOffset(4)] public uint AirlockStateSize;
        [FieldOffset(8)] public uint TuningSize;
        [FieldOffset(12)] public uint DoorPoseSize;
        [FieldOffset(16)] public uint EvaluationResultSize;
        [FieldOffset(20)] public uint TelemetrySize;
        [FieldOffset(24)] public uint OffsetInnerRoom;
        [FieldOffset(28)] public uint OffsetOuterRoom;
        [FieldOffset(32)] public uint OffsetWaterLiters;
        [FieldOffset(36)] public uint OffsetPressureAtm;
        [FieldOffset(40)] public uint OffsetFlags;
        [FieldOffset(44)] public uint OffsetTimer;
        [FieldOffset(48)] public uint BufferStart;
        [FieldOffset(52)] public uint BufferEnd;
        [FieldOffset(56)] public uint FailureMask;
        [FieldOffset(60)] public uint Reserved0;
    }

    public static class AirlockPressurizationMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTickInterval(float globalQualityWeight)
        {
            float quality = math.saturate(FiniteOr(globalQualityWeight, 0.5f));
            return math.lerp(0.016f, 0.1f, 1f - quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveExternalPressureAtm(float depthMeters)
        {
            float depth = math.max(0f, FiniteOr(depthMeters, 0f));
            return AirlockPressurizationConstants.SurfacePressureAtm +
                   depth * (AirlockPressurizationConstants.AtmospherePerTenMeters * 0.1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproximateSqrtPositive(float value)
        {
            float safe = math.max(value, 0f);
            return safe > 0f ? safe * math.rsqrt(safe) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTorricelliLitersPerSecond(float breachAreaM2, float headMeters, float dischargeCoefficient)
        {
            float area = math.max(0f, FiniteOr(breachAreaM2, AirlockPressurizationConstants.DefaultBreachAreaM2));
            float head = math.max(0f, FiniteOr(headMeters, 0f));
            float coefficient = math.clamp(
                FiniteOr(dischargeCoefficient, AirlockPressurizationConstants.DefaultDischargeCoefficient),
                0.05f,
                1f);
            float velocity = ApproximateSqrtPositive(2f * HectonPhysicsContract.GravityMetersPerSecondSquaredConst * head);
            return coefficient * area * velocity * AirlockPressurizationConstants.LitersPerCubicMeter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyNonLinearEqualization(float currentAtm, float targetAtm, float exponent, float dt, float chamberVolumeLiters)
        {
            float current = math.max(AirlockPressurizationConstants.MinimumPressureAtm, FiniteOr(currentAtm, AirlockPressurizationConstants.SurfacePressureAtm));
            float target = math.max(AirlockPressurizationConstants.MinimumPressureAtm, FiniteOr(targetAtm, AirlockPressurizationConstants.SurfacePressureAtm));
            float delta = target - current;
            float absDelta = math.abs(delta);
            if (absDelta <= 0.00001f)
                return target;

            float safeVolume = math.max(1f, FiniteOr(chamberVolumeLiters, 1f));
            float curve = math.max(0.25f, FiniteOr(exponent, AirlockPressurizationConstants.DefaultEqualizationCurveExponent));
            float normalizedDelta = math.saturate(absDelta / math.max(1f, target));
            float speed = (0.65f + normalizedDelta * curve) * math.rsqrt(safeVolume * 0.001f + 1f);
            float step = math.min(absDelta, absDelta * math.saturate(speed * math.max(0f, dt)));
            return current + math.sign(delta) * step;
        }

        public static float EstimateEqualizationDurationSeconds(
            float airlockVolumeM3,
            float equalizationFlowM3PerSqrtKPaSecond,
            float pressureDeltaAtm,
            float maximumEqualizationSeconds)
        {
            float volume = math.max(0.1f, FiniteOr(airlockVolumeM3, 18f));
            float flow = math.max(0.01f, FiniteOr(equalizationFlowM3PerSqrtKPaSecond, 1.35f));
            float deltaKPa = math.abs(FiniteOr(pressureDeltaAtm, 1f)) * HectonSurvivalContract.KPaPerAtmosphere;
            float rootDelta = ApproximateSqrtPositive(math.max(1f, deltaKPa));
            float seconds = volume / math.max(AirlockPressurizationConstants.MinimumDenominator, flow * rootDelta);
            return math.clamp(seconds, 0.25f, math.max(0.25f, FiniteOr(maximumEqualizationSeconds, 18f)));
        }

        public static float EstimateLegacyFacadeCycleSeconds(
            float airlockVolumeM3,
            float equalizationFlowM3PerSqrtKPaSecond,
            float externalDepthMeters,
            float maximumEqualizationSeconds)
        {
            float externalPressure = ResolveExternalPressureAtm(externalDepthMeters);
            return EstimateEqualizationDurationSeconds(
                airlockVolumeM3,
                equalizationFlowM3PerSqrtKPaSecond,
                externalPressure - AirlockPressurizationConstants.SurfacePressureAtm,
                maximumEqualizationSeconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint state, uint value)
        {
            return (state ^ value) * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashFloat(uint state, float value)
        {
            return Hash(state, math.asuint(FiniteOr(value, 0f)));
        }
    }

    /// <summary>
    /// False-sharing-isolated cadence state kept by the owner between dispatcher frames.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AirlockPressurizationScheduleState
    {
        [FieldOffset(0)] public float TickAccumulatorSeconds;
        [FieldOffset(4)] public float LastAdmittedDeltaSeconds;
        [FieldOffset(8)] public float LastTickIntervalSeconds;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public uint LastFrame;
        [FieldOffset(20)] public uint ScheduledFrameCount;
        [FieldOffset(24)] public uint SkippedFrameCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] private uint _pad0;
        [FieldOffset(36)] private uint _pad1;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    public static class AirlockPressurizationLayoutValidator
    {
        public const int AirlockStateOffsetInnerRoom = 0;
        public const int AirlockStateOffsetOuterRoom = 4;
        public const int AirlockStateOffsetWaterLiters = 8;
        public const int AirlockStateOffsetPressureAtm = 12;
        public const int AirlockStateOffsetFlags = 16;
        public const int AirlockStateOffsetTimer = 20;

        public static bool ValidateAirlockStateLayout()
        {
            return UnsafeUtility.SizeOf<AirlockStateDTO>() == 32 &&
                   AirlockStateOffsetInnerRoom == 0 &&
                   AirlockStateOffsetOuterRoom == 4 &&
                   AirlockStateOffsetWaterLiters == 8 &&
                   AirlockStateOffsetPressureAtm == 12 &&
                   AirlockStateOffsetFlags == 16 &&
                   AirlockStateOffsetTimer == 20;
        }

        public static bool ValidateAllRuntimeLayouts()
        {
            return ValidateAirlockStateLayout() &&
                   UnsafeUtility.SizeOf<AirlockTuningDTO>() == 64 &&
                   UnsafeUtility.SizeOf<AirlockDoorPoseDTO>() == 96 &&
                   UnsafeUtility.SizeOf<AirlockExchangeIndexDTO>() == 16 &&
                   UnsafeUtility.SizeOf<AirlockEvaluationResultDTO>() == 64 &&
                   UnsafeUtility.SizeOf<AirlockTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<AirlockHardwareProfileDTO>() == 32 &&
                   UnsafeUtility.SizeOf<AirlockDebugGizmoDTO>() == 64 &&
                   UnsafeUtility.SizeOf<AirlockPressurizationScheduleState>() == 64;
        }

    }

    public static class AirlockPressurizationSelfAudit
    {
        public static bool TryRun(out AirlockSelfAuditResultDTO result)
        {
            result = new AirlockSelfAuditResultDTO
            {
                AirlockStateSize = (uint)UnsafeUtility.SizeOf<AirlockStateDTO>(),
                TuningSize = (uint)UnsafeUtility.SizeOf<AirlockTuningDTO>(),
                DoorPoseSize = (uint)UnsafeUtility.SizeOf<AirlockDoorPoseDTO>(),
                EvaluationResultSize = (uint)UnsafeUtility.SizeOf<AirlockEvaluationResultDTO>(),
                TelemetrySize = (uint)UnsafeUtility.SizeOf<AirlockTelemetryEntry>(),
                OffsetInnerRoom = AirlockPressurizationLayoutValidator.AirlockStateOffsetInnerRoom,
                OffsetOuterRoom = AirlockPressurizationLayoutValidator.AirlockStateOffsetOuterRoom,
                OffsetWaterLiters = AirlockPressurizationLayoutValidator.AirlockStateOffsetWaterLiters,
                OffsetPressureAtm = AirlockPressurizationLayoutValidator.AirlockStateOffsetPressureAtm,
                OffsetFlags = AirlockPressurizationLayoutValidator.AirlockStateOffsetFlags,
                OffsetTimer = AirlockPressurizationLayoutValidator.AirlockStateOffsetTimer,
                BufferStart = (uint)AirlockPressurizationBufferIds.AirlockStates,
                BufferEnd = (uint)AirlockPressurizationBufferIds.DumpRequested
            };

            uint failure = 0u;
            if (!AirlockPressurizationLayoutValidator.ValidateAirlockStateLayout())
                failure |= 1u << 0;
            if (!AirlockPressurizationLayoutValidator.ValidateAllRuntimeLayouts())
                failure |= 1u << 1;
            if (AirlockPressurizationMath.ResolveTickInterval(0f) <= AirlockPressurizationMath.ResolveTickInterval(1f))
                failure |= 1u << 2;
            if (AirlockPressurizationMath.ResolveTorricelliLitersPerSecond(0.18f, 30f, 0.62f) <= 0f)
                failure |= 1u << 3;

            result.FailureMask = failure;
            result.Flags = failure == 0u ? 1u : 0u;
            return failure == 0u;
        }
    }

    public static unsafe class AirlockTelemetryDumper
    {
        public static bool TryDump(NativeArray<AirlockTelemetryEntry> telemetry, string path = AirlockPressurizationConstants.DumpPath)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Docs/AgentLogs");
            int count = math.min(telemetry.Length, AirlockPressurizationConstants.TelemetryFrameCount);
            int bytes = count * UnsafeUtility.SizeOf<AirlockTelemetryEntry>();
            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                stream.Write(new ReadOnlySpan<byte>(source, bytes));
            }

            return true;
        }
    }

    public static class AirlockPressurizationIntentFlush
    {
        public static void PushBulkheadIntents(NativeArray<BulkheadContainmentIntentDTO> intents, int intentCount)
        {
            if (!intents.IsCreated)
                return;

            int safeCount = math.min(math.max(0, intentCount), intents.Length);
            for (int i = 0; i < safeCount; i++)
            {
                BulkheadContainmentIntentDTO intent = intents[i];
                if ((intent.Flags & BulkheadContainmentIntentFlags.Valid) == 0u ||
                    (intent.Flags & BulkheadContainmentIntentFlags.NonFinite) != 0u ||
                    intent.EdgeHashID == 0u)
                {
                    intents[i] = default;
                    continue;
                }

                bool locked = (intent.Flags & BulkheadContainmentIntentFlags.Locked) != 0u;
                bool published = BulkheadContainmentIntentBus.TryWriteAirlockBulkheadIntent(
                    intent.EdgeHashID,
                    locked,
                    intent.CenterAup,
                    intent.Normal,
                    intent.WidthMeters,
                    intent.HeightMeters,
                    intent.ParentIntegrity01,
                    intent.SiblingNodeHash,
                    intent.Frame);
                if (published)
                    intents[i] = default;
            }
        }
    }

    public static class AirlockPressurizationSignalFlush
    {
        private static int s_x001AirlockPressurizationContractsSignalPushDropCount;
        public static void PushFrameSignals(
            NativeArray<BubbleSpawnSignal> vfxSignals,
            NativeArray<MovementAcousticSignal> acousticSignals,
            int signalCount)
        {
            int count = math.max(0, signalCount);
            if (vfxSignals.IsCreated)
            {
                int safe = math.min(count, vfxSignals.Length);
                for (int i = 0; i < safe; i++)
                {
                    BubbleSpawnSignal signal = vfxSignals[i];
                    vfxSignals[i] = default;
                    if (signal.Frame != 0u)
                        SignalBus<BubbleSpawnSignal>.TryPushTracked(in signal, ref s_x001AirlockPressurizationContractsSignalPushDropCount);
                }
            }

            if (!acousticSignals.IsCreated)
                return;

            int acousticCount = math.min(count, acousticSignals.Length);
            for (int i = 0; i < acousticCount; i++)
            {
                MovementAcousticSignal signal = acousticSignals[i];
                acousticSignals[i] = default;
                if (signal.SourceId != 0u)
                    SignalBus<MovementAcousticSignal>.TryPushTracked(in signal, ref s_x001AirlockPressurizationContractsSignalPushDropCount);
            }
        }
    }
}
