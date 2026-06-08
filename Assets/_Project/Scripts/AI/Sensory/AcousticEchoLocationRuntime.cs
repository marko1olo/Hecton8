using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI.Sensory
{
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct EchoTap
    {
        [FieldOffset(0)] public AbsoluteUniversePosition SourceAup;
        [FieldOffset(48)] public AbsoluteUniversePosition PortalAup;
        [FieldOffset(96)] public float Volume01;
        [FieldOffset(100)] public float Transmission01;
        [FieldOffset(104)] public float DelaySeconds;
        [FieldOffset(108)] public float LastHeardTime;
        [FieldOffset(112)] public uint SourceId;
        [FieldOffset(116)] public uint Sequence;
        [FieldOffset(120)] public byte Flags;
        [FieldOffset(121)] public byte QualityWeightByte;
        [FieldOffset(122)] private ushort _pad0;
        [FieldOffset(124)] public float EchoAmplitude;
        [FieldOffset(128)] public float EchoFrequency;
        [FieldOffset(132)] public uint IsGhostBlip;
        [FieldOffset(136)] public float LifetimeSeconds;
        [FieldOffset(140)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct AcousticEchoHuntResult
    {
        [FieldOffset(0)] public AbsoluteUniversePosition InvestigateAup;
        [FieldOffset(48)] public AbsoluteUniversePosition SourceAup;
        [FieldOffset(96)] public float3 RuntimePosition;
        [FieldOffset(108)] public float Intensity01;
        [FieldOffset(112)] public float LastHeardTime;
        [FieldOffset(116)] public float SilenceSeconds;
        [FieldOffset(120)] public float HeadSweep01;
        [FieldOffset(124)] public uint SourceId;
        [FieldOffset(128)] public uint Sequence;
        [FieldOffset(132)] public byte Flags;
        [FieldOffset(133)] public byte QualityWeightByte;
        [FieldOffset(134)] private ushort _pad0;
        [FieldOffset(136)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AcousticEchoTrailState
    {
        [FieldOffset(0)] public AbsoluteUniversePosition InvestigateAup;
        [FieldOffset(48)] public AbsoluteUniversePosition SourceAup;
        [FieldOffset(96)] public float Intensity01;
        [FieldOffset(100)] public float LastHeardTime;
        [FieldOffset(104)] public uint SourceId;
        [FieldOffset(108)] public uint Sequence;
        [FieldOffset(112)] public uint AcousticHuntsTriggered;
        [FieldOffset(116)] public byte Flags;
        [FieldOffset(117)] public byte QualityWeightByte;
        [FieldOffset(118)] private ushort _pad0;
        [FieldOffset(120)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct AcousticEchoBlackBoxEntry
    {
        [FieldOffset(0)] public long PortalGridX;
        [FieldOffset(8)] public long PortalGridY;
        [FieldOffset(16)] public long PortalGridZ;
        [FieldOffset(24)] public float3 PortalLocal;
        [FieldOffset(36)] public float Intensity01;
        [FieldOffset(40)] public float LastHeardTime;
        [FieldOffset(44)] public float SilenceSeconds;
        [FieldOffset(48)] public int Frame;
        [FieldOffset(52)] public uint AcousticHuntsTriggered;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public uint Sequence;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public uint StateHash;
        [FieldOffset(72)] private ulong _pad0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EchoTrackingJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<EchoTap> Taps;
        [NoAlias] public NativeArray<AcousticEchoTrailState> Result;
        public AcousticEchoTrailState Previous;
        public float CurrentTime;
        public int TapCount;
        public float SilenceTimeoutSeconds;

        public void Execute()
        {
            if (!Result.IsCreated || Result.Length == 0)
                return;

            AcousticEchoTrailState state = Previous;
            float bestScore = -1f;
            EchoTap bestTap = default;
            int limit = math.min(math.max(0, TapCount), Taps.IsCreated ? Taps.Length : 0);
            for (int i = 0; i < limit; i++)
            {
                EchoTap tap = Taps[i];
                if (!IsFiniteAup(in tap.PortalAup) ||
                    !IsFiniteAup(in tap.SourceAup) ||
                    !math.isfinite(tap.Volume01) ||
                    !math.isfinite(tap.Transmission01) ||
                    !math.isfinite(tap.DelaySeconds) ||
                    !math.isfinite(tap.LastHeardTime))
                {
                    continue;
                }

                float volume = math.saturate(tap.Volume01);
                float transmission = math.saturate(tap.Transmission01);
                float truthMask = math.select(1f, 0f, tap.IsGhostBlip != 0u);
                float intensity = math.saturate(volume * math.max(0.05f, transmission) * truthMask);
                if (intensity <= 0.0001f)
                    continue;

                float age = math.max(0f, CurrentTime - tap.LastHeardTime);
                if (age > SilenceTimeoutSeconds)
                    continue;

                if (intensity > bestScore ||
                    (math.abs(intensity - bestScore) <= 0.0001f && tap.Sequence > bestTap.Sequence))
                {
                    bestScore = intensity;
                    bestTap = tap;
                }
            }

            if (bestScore >= 0f)
            {
                state.InvestigateAup = bestTap.PortalAup;
                state.SourceAup = bestTap.SourceAup;
                state.Intensity01 = bestScore;
                state.LastHeardTime = bestTap.LastHeardTime;
                state.SourceId = bestTap.SourceId;
                state.Sequence = bestTap.Sequence;
                state.AcousticHuntsTriggered = Previous.AcousticHuntsTriggered == uint.MaxValue
                    ? uint.MaxValue
                    : Previous.AcousticHuntsTriggered + 1u;
                state.Flags = (byte)(bestTap.Flags | AcousticEchoLocationRuntime.FlagActiveTrail);
                state.QualityWeightByte = bestTap.QualityWeightByte;
            }
            else
            {
                float silenceSeconds = CurrentTime - state.LastHeardTime;
                if (silenceSeconds >= SilenceTimeoutSeconds || !math.isfinite(silenceSeconds))
                {
                    state.Intensity01 = 0f;
                    state.Flags = AcousticEchoLocationRuntime.FlagSilenceLost;
                }
                else
                {
                    float retained = 1f - math.saturate(silenceSeconds * math.rcp(math.max(0.001f, SilenceTimeoutSeconds)));
                    state.Intensity01 = math.saturate(state.Intensity01 * retained);
                    state.Flags = (byte)(state.Flags | AcousticEchoLocationRuntime.FlagActiveTrail);
                }
            }

            Result[0] = state;
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateAcousticPingsJob : IJob
    {
        [NoAlias] public NativeArray<EchoTap> Taps;
        public AbsoluteUniversePosition PlayerAup;
        public float3 PlayerForward;
        public float3 FlashlightForward;
        public float FlashlightConeCos;
        public float FlashlightActive01;
        public float StressLevel01;
        public float CurrentFrequency;
        public float PredatorFrequency;
        public float CurrentTime;
        public float GlobalQualityWeight;
        public int StartIndex;
        public int Capacity;
        public int Frame;
        public uint Seed;
        public byte QualityWeightByte;

        public void Execute()
        {
            if (!Taps.IsCreated)
                return;

            int capacity = math.min(math.max(0, Capacity), Taps.Length);
            int start = math.clamp(StartIndex, 0, capacity);
            for (int i = start; i < capacity; i++)
                Taps[i] = default;

            if (!PlayerAup.IsFinite())
                return;

            int available = capacity - start;
            if (available <= 0)
                return;

            float quality = math.saturate(math.select(0f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            int qualityBudget = math.clamp((int)math.round(math.lerp(3f, 25f, quality)), 3, 25);
            int ghostCount = math.min(available, qualityBudget);

            float stress01 = math.saturate(math.select(0f, StressLevel01, math.isfinite(StressLevel01)));
            float stressMultiplier = math.saturate((stress01 - 0.75f) * 4f);
            float currentFrequency = math.clamp(
                math.select(-999f, CurrentFrequency, math.isfinite(CurrentFrequency)),
                -999f,
                12f);
            float predatorFrequency = math.clamp(
                math.select(AcousticEchoLocationRuntime.DefaultGhostPredatorFrequency, PredatorFrequency, math.isfinite(PredatorFrequency)),
                0.1f,
                12f);
            float currentTime = math.max(0f, math.select(0f, CurrentTime, math.isfinite(CurrentTime)));
            float tunedMask = math.step(math.abs(predatorFrequency - currentFrequency), 0.05f);
            float frequencyMultiplier = 1f - tunedMask;

            float3 forward = SafeNormalize(
                new float3(PlayerForward.x, 0f, PlayerForward.z),
                new float3(0f, 0f, 1f));
            float3 rear = -forward;
            float3 right = new float3(forward.z, 0f, -forward.x);
            float3 flashlightForward = SafeNormalize(FlashlightForward, forward);
            float flashlightActive = math.saturate(math.select(0f, FlashlightActive01, math.isfinite(FlashlightActive01)));
            float flashlightConeCos = math.clamp(math.select(1.1f, FlashlightConeCos, math.isfinite(FlashlightConeCos)), -1f, 1.1f);

            for (int i = 0; i < ghostCount; i++)
            {
                uint state = Seed ^ ((uint)i * 747796405u) ^ (uint)math.max(0, Frame);
                float lateral01 = NextRandom01(ref state);
                float distance01 = NextRandom01(ref state);
                float vertical01 = NextRandom01(ref state);
                float angle = (lateral01 - 0.5f) * AcousticEchoLocationRuntime.Pi;
                float sin = AcousticEchoLocationRuntime.SinPolynomial7(angle);
                float cos = AcousticEchoLocationRuntime.SinPolynomial7(angle + AcousticEchoLocationRuntime.HalfPi);
                float3 direction = SafeNormalize(
                    rear * math.abs(cos) + right * sin + new float3(0f, (vertical01 - 0.5f) * 0.18f, 0f),
                    rear);
                float distance = math.lerp(14f, 78f, distance01);
                float dot = math.dot(flashlightForward, direction);
                float illuminatedMask = math.step(flashlightConeCos, dot) * flashlightActive;
                float approachPulse = 0.7f + 0.3f * AcousticEchoLocationRuntime.SinPolynomial7(currentTime * 7.0f + i * 1.6180339f);
                float amplitude = math.saturate(stressMultiplier * frequencyMultiplier * (1f - illuminatedMask) * approachPulse);
                AbsoluteUniversePosition blipAup = AbsoluteUniversePosition.OffsetMeters(in PlayerAup, (double3)(direction * distance));

                EchoTap tap = default;
                tap.SourceAup = blipAup;
                tap.PortalAup = blipAup;
                tap.Volume01 = amplitude;
                tap.Transmission01 = 1f;
                tap.DelaySeconds = 0f;
                tap.LastHeardTime = currentTime;
                tap.SourceId = AcousticEchoLocationRuntime.GhostBlipSourceHash ^ (uint)i;
                tap.Sequence = ((uint)math.max(0, Frame) << 16) ^ (Seed + (uint)i + 1u);
                tap.Flags = AcousticEchoLocationRuntime.FlagNoisemakerCandidate;
                tap.QualityWeightByte = QualityWeightByte;
                tap.EchoAmplitude = amplitude;
                tap.EchoFrequency = predatorFrequency;
                tap.IsGhostBlip = 1u;
                tap.LifetimeSeconds = math.lerp(0.75f, 2.25f, stressMultiplier);
                Taps[start + i] = tap;
            }
        }

        private static float NextRandom01(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) * (1f / 16777215f);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lenSq = math.dot(value, value);
            float valid = math.select(0f, 1f, math.isfinite(lenSq) & (lenSq > 0.000001f));
            float invLen = math.rsqrt(math.max(0.000001f, lenSq));
            return math.select(fallback, value * invLen, valid > 0f);
        }

    }

    public static class AcousticEchoLocationRuntime
    {
        public const float Pi = 3.14159265358979323846f;
        public const float TwoPi = 6.28318530717958647692f;
        public const float HalfPi = 1.57079632679489661923f;
        public const float InvTwoPi = 0.15915494309189533577f;
        public const float DefaultGhostPredatorFrequency = 4.75f;
        public const uint GhostBlipSourceHash = 0x47484F53u;

        public const byte FlagActiveTrail = 1 << 0;
        public const byte FlagPortalBreadcrumb = 1 << 1;
        public const byte FlagMovementBreadcrumb = 1 << 2;
        public const byte FlagPingBreadcrumb = 1 << 3;
        public const byte FlagNoisemakerCandidate = 1 << 4;
        public const byte FlagMinimumQualityDirectNode = 1 << 5;
        public const byte FlagDspEchoTap = 1 << 6;
        public const byte FlagSilenceLost = 1 << 7;
        public const byte PortalEmissionFlagStationaryEmitter = 1 << 4;

        public const int MaxEchoTapsPerFrame = 32;
        public const int BlackBoxFrameCount = 300;
        public const float SilenceTimeoutSeconds = 5f;

        private const float MovementVelocityToVolume = 0.025f;
        private const int MaxQueuedEchoTaps = MaxEchoTapsPerFrame;
        private const int GhostHapticCooldownFrames = 24;
        private const int TuningHapticCooldownFrames = 30;
        private const int MovementSignalCapacityWarningCooldownFrames = 90;
        private const int ExternalHandleRefreshCooldownFrames = 30;
        private const uint MovementSignalCapacityWarningHash = 0x41454D4Fu;
        private const uint AcousticEchoContextHash = 0x41454348u;
        private const int MutationGuardBitMask = 31;
        private const ulong AcousticFrameTapGuardBit = 1UL << (((int)BufferID.AcousticEchoFrameTaps) & MutationGuardBitMask);
        private const ulong AcousticPendingTapGuardBit = 1UL << (((int)BufferID.AcousticEchoPendingTaps) & MutationGuardBitMask);
        private const ulong AcousticTrailStateGuardBit = 1UL << (((int)BufferID.AcousticEchoTrailState) & MutationGuardBitMask);
        private const ulong AcousticBlackBoxGuardBit = 1UL << (((int)BufferID.AcousticEchoBlackBox) & MutationGuardBitMask);
        private const ulong TrackingMutationGuardMask =
            AcousticFrameTapGuardBit |
            AcousticPendingTapGuardBit |
            AcousticTrailStateGuardBit |
            AcousticBlackBoxGuardBit;
        private const ulong PendingTapMutationGuardMask = AcousticPendingTapGuardBit;
        private const ulong BlackBoxMutationGuardMask = AcousticBlackBoxGuardBit;

        private static readonly AcousticEchoHotSwapBridge s_hotSwapBridge = new AcousticEchoHotSwapBridge(); // COLD ALLOC: AcousticEchoHotSwapBridge[1] - static acoustic echo DataVault rebind listener - owner: AcousticEchoLocationRuntime
        private static IDataVault _dataVault;
        private static IDataVault _pendingDataVaultRebind;
        private static VaultGenerationHandle<EchoTap> _frameTapsHandle;
        private static VaultGenerationHandle<EchoTap> _pendingTapsHandle;
        private static VaultGenerationHandle<AcousticEchoTrailState> _jobResultHandle;
        private static VaultGenerationHandle<AcousticEchoBlackBoxEntry> _blackBoxHandle;
        private static VaultGenerationHandle<DecryptionPuzzleDTO> _decryptionPuzzleHandle;
        private static VaultGenerationHandle<DecryptionKnobInputDTO> _decryptionKnobInputHandle;
        private static JobHandle _trackingHandle;
        private static AcousticEchoTrailState _trailState;
        private static IPlayerRuntimeContext _playerContext;
        private static int _trackingScheduled;
        private static int _pendingDataVaultRebindValid;
        private static int _initialized;
        private static int _lastRefreshFrame = int.MinValue;
        private static int _lastBlackBoxFrame = int.MinValue;
        private static int _lastGhostHapticFrame = int.MinValue;
        private static int _lastTuningHapticFrame = int.MinValue;
        private static int _lastExternalHandleRefreshFrame = int.MinValue;
        private static int _lastPlayerStressSignalSequence;
        private static int _lastPlayerStressSignalSeenFrame = int.MinValue;
        private static int _nextMovementSignalCapacityWarningFrame = int.MinValue;
        private static int _blackBoxCursor;
        private static int _blackBoxDumped;
        private static int _initializationAttempted;
        private static int _queuedEchoTapCount;
        private static int _pendingProducerFault;
        private static AbsoluteUniversePosition _pendingProducerFaultAup;
        private static IDataVault _trackingMutationGuardVault;
        private static ulong _trackingMutationGuardMask;

        private struct GhostBlipContext
        {
            public AbsoluteUniversePosition PlayerAup;
            public float3 PlayerForward;
            public float3 FlashlightForward;
            public float FlashlightConeCos;
            public float FlashlightActive01;
            public float Stress01;
            public float CurrentFrequency;
            public float PredatorFrequency;
            public float TunedMask01;
            public float StressMultiplier01;
        }
        private static int _hotSwapRegistered;
        private static uint _sequence;
        private static byte _cachedQualityWeightByte;

        public static uint AcousticHuntsTriggered => _trailState.AcousticHuntsTriggered;

        public static bool TryRunStaticSelfAudit(out uint failureMask)
        {
            failureMask = 0u;
            if (UnsafeUtility.SizeOf<EchoTap>() != 144)
                failureMask |= 1u << 0;
            if ((UnsafeUtility.SizeOf<EchoTap>() & 15) != 0)
                failureMask |= 1u << 1;
            if (UnsafeUtility.SizeOf<AcousticEchoTrailState>() != 128)
                failureMask |= 1u << 2;
            if ((UnsafeUtility.SizeOf<AcousticEchoTrailState>() & 7) != 0)
                failureMask |= 1u << 3;
            if (UnsafeUtility.SizeOf<AcousticEchoHuntResult>() != 144)
                failureMask |= 1u << 4;
            if ((UnsafeUtility.SizeOf<AcousticEchoHuntResult>() & 7) != 0)
                failureMask |= 1u << 5;
            if (UnsafeUtility.SizeOf<AcousticEchoBlackBoxEntry>() != 80)
                failureMask |= 1u << 6;
            if ((UnsafeUtility.SizeOf<AcousticEchoBlackBoxEntry>() & 7) != 0)
                failureMask |= 1u << 7;
            if (UnsafeUtility.SizeOf<DecryptionPuzzleDTO>() != 32)
                failureMask |= 1u << 8;
            if (UnsafeUtility.SizeOf<DecryptionKnobInputDTO>() != 64)
                failureMask |= 1u << 9;
            if (UnsafeUtility.SizeOf<HapticPulseSignal>() != 16)
                failureMask |= 1u << 10;
            return failureMask == 0u;
        }

        public static void EnsureInitialized()
        {
            TryRegisterHotSwapListener();
            if (_initialized != 0)
                return;

            if (_initializationAttempted != 0)
                return;

            _initializationAttempted = 1;
            SignalCorridorRuntime.EnsureHapticPulseSignalLaneInitialized();
            _cachedQualityWeightByte = ResolveQualityWeightByte();
            EnsureBootstrapPlayerContext();
            if (OpenOrAcquireVaultBuffersForOwnerRoute())
                _initialized = 1;
        }

        public static void Dispose()
        {
            TryUnregisterHotSwapListener();

            if (_trackingScheduled != 0)
            {
                try
                {
                    DispatcherJobFence.TryComplete(ref _trackingHandle, forceComplete: true);
                }
                finally
                {
                    _trackingScheduled = 0;
                    ReleaseTrackingMutationGuard();
                }
            }

            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = null;
            _pendingDataVaultRebind = null;
            _playerContext = null;
            _trailState = default;
            _lastRefreshFrame = int.MinValue;
            _lastBlackBoxFrame = int.MinValue;
            _lastGhostHapticFrame = int.MinValue;
            _lastTuningHapticFrame = int.MinValue;
            _lastExternalHandleRefreshFrame = int.MinValue;
            _lastPlayerStressSignalSequence = 0;
            _lastPlayerStressSignalSeenFrame = int.MinValue;
            _nextMovementSignalCapacityWarningFrame = int.MinValue;
            _blackBoxCursor = 0;
            _blackBoxDumped = 0;
            _initializationAttempted = 0;
            _queuedEchoTapCount = 0;
            _pendingProducerFault = 0;
            _pendingDataVaultRebindValid = 0;
            _pendingProducerFaultAup = default;
            _sequence = 0u;
            _initialized = 0;
        }

        public static bool TryEnqueueEchoTap(in EchoTap tap)
        {
            if (!IsValidTap(in tap))
            {
                RecordPendingProducerFault(in tap.PortalAup);
                return false;
            }

            if (_initialized == 0 || _queuedEchoTapCount >= MaxQueuedEchoTaps)
                return false;

            IDataVault guardVault;
            if (!TryAcquireAcousticMutationGuard(_dataVault, PendingTapMutationGuardMask, out guardVault))
                return false;

            try
            {
                if (!TryResolvePendingTapsNoAcquire(out NativeArray<EchoTap> pendingTaps) ||
                    !pendingTaps.IsCreated ||
                    _queuedEchoTapCount >= pendingTaps.Length)
                {
                    return false;
                }

                pendingTaps[_queuedEchoTapCount] = tap;
                _queuedEchoTapCount++;
                return true;
            }
            finally
            {
                ReleaseAcousticMutationGuard(guardVault, PendingTapMutationGuardMask);
            }
        }

        private static bool OpenOrAcquireVaultBuffersForOwnerRoute()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsAcousticVaultHandle(in _frameTapsHandle, BufferID.AcousticEchoFrameTaps) ||
                !IsAcousticVaultHandle(in _pendingTapsHandle, BufferID.AcousticEchoPendingTaps) ||
                !IsAcousticVaultHandle(in _jobResultHandle, BufferID.AcousticEchoTrailState) ||
                !IsAcousticVaultHandle(in _blackBoxHandle, BufferID.AcousticEchoBlackBox))
            {
                EnsureBootstrapVault();
                vault = _dataVault;
            }

            if (vault == null)
                return false;

            return EnsureVaultBuffer(
                       vault,
                       BufferID.AcousticEchoFrameTaps,
                       MaxEchoTapsPerFrame,
                       ref _frameTapsHandle,
                       out _) &&
                   EnsureVaultBuffer(
                       vault,
                       BufferID.AcousticEchoPendingTaps,
                       MaxQueuedEchoTaps,
                       ref _pendingTapsHandle,
                       out _) &&
                   EnsureVaultBuffer(
                       vault,
                       BufferID.AcousticEchoTrailState,
                       1,
                       ref _jobResultHandle,
                       out _) &&
                   EnsureVaultBuffer(
                       vault,
                       BufferID.AcousticEchoBlackBox,
                       BlackBoxFrameCount,
                       ref _blackBoxHandle,
                       out _);
        }

        private static void EnsureBootstrapVault()
        {
            if (_dataVault != null)
                return;

            // DataVault can be published after early sensory initialization; retry only while unbound.
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
        }

        private static void RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
            {
                if (_trackingScheduled != 0)
                    return;

                if (_initializationAttempted != 0 && OpenOrAcquireVaultBuffersForOwnerRoute())
                    _initialized = 1;
                return;
            }

            if (_trackingScheduled != 0)
            {
                _pendingDataVaultRebind = nextVault;
                _pendingDataVaultRebindValid = 1;
                return;
            }

            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = nextVault;
            _pendingDataVaultRebind = null;
            _pendingDataVaultRebindValid = 0;
            _trailState = default;
            _lastRefreshFrame = int.MinValue;
            _lastBlackBoxFrame = int.MinValue;
            _nextMovementSignalCapacityWarningFrame = int.MinValue;
            _blackBoxCursor = 0;
            _blackBoxDumped = 0;
            _initialized = 0;
            _queuedEchoTapCount = 0;
            _pendingProducerFault = 0;
            _pendingProducerFaultAup = default;
            _sequence = 0u;

            if (_initializationAttempted != 0 && _dataVault != null && OpenOrAcquireVaultBuffersForOwnerRoute())
                _initialized = 1;
        }

        private static void ApplyPendingDataVaultRebindIfIdle()
        {
            if (_pendingDataVaultRebindValid == 0 || _trackingScheduled != 0)
                return;

            IDataVault pendingVault = _pendingDataVaultRebind;
            _pendingDataVaultRebind = null;
            _pendingDataVaultRebindValid = 0;
            RebindDataVaultForLifecycle(pendingVault);
        }

        private static void EnsureBootstrapPlayerContext()
        {
            if (_playerContext != null)
                return;

            RebindPlayerContextForLifecycle(GlobalRegistry.Player);
        }

        private static void RebindPlayerContextForLifecycle(IPlayerRuntimeContext nextContext)
        {
            _playerContext = nextContext;
        }

        private static void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered != 0 || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(s_hotSwapBridge) ? 1 : 0;
        }

        private static void TryUnregisterHotSwapListener()
        {
            if (_hotSwapRegistered == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(s_hotSwapBridge);
            _hotSwapRegistered = 0;
        }

        private sealed class AcousticEchoHotSwapBridge : IGlobalRegistryHotSwapListener
        {
            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                    RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);
                else if (serviceSlot == GlobalRegistryServiceSlot.Player)
                    RebindPlayerContextForLifecycle(currentService as IPlayerRuntimeContext);
            }
        }

        private static void ClearVaultHandles()
        {
            _frameTapsHandle = default;
            _pendingTapsHandle = default;
            _jobResultHandle = default;
            _blackBoxHandle = default;
            _decryptionPuzzleHandle = default;
            _decryptionKnobInputHandle = default;
        }

        private static bool TryAcquireTrackingMutationGuard()
        {
            if (_trackingMutationGuardVault != null)
                return false;

            IDataVault vault = _dataVault;
            if (!TryAcquireAcousticMutationGuard(vault, TrackingMutationGuardMask, out IDataVault guardVault))
                return false;

            _trackingMutationGuardVault = guardVault;
            _trackingMutationGuardMask = TrackingMutationGuardMask;
            return true;
        }

        private static void ReleaseTrackingMutationGuard()
        {
            IDataVault vault = _trackingMutationGuardVault;
            ulong mask = _trackingMutationGuardMask;
            if (vault == null)
                return;

            _trackingMutationGuardVault = null;
            _trackingMutationGuardMask = 0UL;
            ReleaseAcousticMutationGuard(vault, mask);
        }

        private static bool TryAcquireAcousticMutationGuard(
            IDataVault vault,
            ulong mask,
            out IDataVault guardVault)
        {
            guardVault = null;
            if (vault == null ||
                mask == 0UL ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(mask))
            {
                return false;
            }

            guardVault = vault;
            return true;
        }

        private static void ReleaseAcousticMutationGuard(IDataVault vault, ulong mask)
        {
            vault?.ReleaseMutationGuard(mask);
        }

        private static bool TryResolveVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsAcousticVaultHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool EnsureVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null)
                return false;

            if (IsAcousticVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (IsAcousticVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);
            handle = default;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AISensory,
                NativeArrayOptions.ClearMemory);
            if (!IsAcousticVaultHandle(in acquired, bufferId) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                if (IsAcousticVaultHandle(in acquired, bufferId))
                    vault.ReleaseBuffer(in acquired);
                return false;
            }

            handle = acquired;
            return true;
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, BufferID.AcousticEchoFrameTaps, ref _frameTapsHandle);
            ReleaseVaultHandle(vault, BufferID.AcousticEchoPendingTaps, ref _pendingTapsHandle);
            ReleaseVaultHandle(vault, BufferID.AcousticEchoTrailState, ref _jobResultHandle);
            ReleaseVaultHandle(vault, BufferID.AcousticEchoBlackBox, ref _blackBoxHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, BufferID expectedBufferId, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsAcousticVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsAcousticVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.AISensory &&
                   handle.Generation != 0u;
        }

        private static bool IsExternalUiVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u;
        }

        private static void RefreshExternalReadHandles(IDataVault vault, int frame)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            bool hasPuzzleHandle = IsExternalUiVaultHandle(in _decryptionPuzzleHandle, BufferID.TerminalDecryptionPuzzles);
            bool hasKnobHandle = IsExternalUiVaultHandle(in _decryptionKnobInputHandle, BufferID.TerminalDecryptionKnobInput);
            if (hasPuzzleHandle && hasKnobHandle)
                return;

            if (_lastExternalHandleRefreshFrame != int.MinValue &&
                frame >= _lastExternalHandleRefreshFrame &&
                frame - _lastExternalHandleRefreshFrame < ExternalHandleRefreshCooldownFrames)
            {
                return;
            }

            _lastExternalHandleRefreshFrame = frame;
            if (!hasPuzzleHandle)
                vault.TryGetGenerationHandle(BufferID.TerminalDecryptionPuzzles, out _decryptionPuzzleHandle);
            if (!hasKnobHandle)
                vault.TryGetGenerationHandle(BufferID.TerminalDecryptionKnobInput, out _decryptionKnobInputHandle);
        }

        private static bool TryResolveFrameViewsNoAcquire(
            out NativeArray<EchoTap> frameTaps,
            out NativeArray<AcousticEchoTrailState> jobResult)
        {
            frameTaps = default;
            jobResult = default;

            return TryResolveVaultBuffer(in _frameTapsHandle, BufferID.AcousticEchoFrameTaps, MaxEchoTapsPerFrame, out frameTaps) &&
                   TryResolveVaultBuffer(in _jobResultHandle, BufferID.AcousticEchoTrailState, 1, out jobResult);
        }

        private static bool EnsureBlackBox(out NativeArray<AcousticEchoBlackBoxEntry> blackBox)
        {
            blackBox = default;
            if (!OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            if (TryResolveVaultBuffer(in _blackBoxHandle, BufferID.AcousticEchoBlackBox, BlackBoxFrameCount, out blackBox))
                return true;

            if (_trackingScheduled != 0)
                return false;

            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            if (!OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            return TryResolveVaultBuffer(in _blackBoxHandle, BufferID.AcousticEchoBlackBox, BlackBoxFrameCount, out blackBox);
        }

        private static bool TryResolveBlackBoxNoAcquire(out NativeArray<AcousticEchoBlackBoxEntry> blackBox)
        {
            blackBox = default;
            return TryResolveVaultBuffer(in _blackBoxHandle, BufferID.AcousticEchoBlackBox, BlackBoxFrameCount, out blackBox);
        }

        private static bool TryReadOnlyBlackBox(out NativeArray<AcousticEchoBlackBoxEntry>.ReadOnly blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsAcousticVaultHandle(in _blackBoxHandle, BufferID.AcousticEchoBlackBox) &&
                   vault.TryReadOnlyHandle(in _blackBoxHandle, out blackBox) &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxFrameCount;
        }

        private static bool TryResolvePendingTapsNoAcquire(out NativeArray<EchoTap> pendingTaps)
        {
            pendingTaps = default;
            return _initialized != 0 &&
                   _dataVault != null &&
                   IsAcousticVaultHandle(in _pendingTapsHandle, BufferID.AcousticEchoPendingTaps) &&
                   TryResolveVaultBuffer(in _pendingTapsHandle, BufferID.AcousticEchoPendingTaps, MaxQueuedEchoTaps, out pendingTaps);
        }

        public static bool TryEnqueuePortalEcho(
            in AbsoluteUniversePosition sourceAup,
            in AcousticAup lastPortalAup,
            byte pathFound,
            byte usedPortalPath,
            float transmission01,
            float delaySeconds,
            float volume01,
            uint sourceId,
            int frame,
            float currentTime,
            byte qualityWeightByte,
            byte extraFlags = 0)
        {
            if (pathFound == 0 ||
                usedPortalPath == 0 ||
                !AcousticAup.IsFinite(in lastPortalAup) ||
                !math.isfinite(transmission01) ||
                !math.isfinite(delaySeconds))
            {
                return false;
            }

            EchoTap tap = default;
            tap.SourceAup = sourceAup;
            tap.PortalAup = ToAbsoluteUniversePosition(in lastPortalAup);
            tap.Volume01 = math.saturate(volume01);
            tap.Transmission01 = math.saturate(transmission01);
            tap.DelaySeconds = math.max(0f, delaySeconds);
            tap.LastHeardTime = math.max(0f, currentTime - tap.DelaySeconds);
            tap.SourceId = sourceId;
            tap.Sequence = NextSequence(frame);
            tap.Flags = (byte)(FlagPortalBreadcrumb | FlagDspEchoTap | extraFlags);
            tap.QualityWeightByte = qualityWeightByte;
            return TryEnqueueEchoTap(in tap);
        }

        public static bool TryPublishPortalPropagationEcho(
            in AcousticAup emissionSourceAup,
            in AcousticAup lastPortalAup,
            byte pathFound,
            byte usedPortalPath,
            float volume01,
            float transmission01,
            float delaySeconds,
            uint eventId,
            byte emissionFlags,
            int frame,
            float currentTime,
            byte qualityWeightByte)
        {
            AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in emissionSourceAup);
            byte flags = (emissionFlags & PortalEmissionFlagStationaryEmitter) != 0
                ? FlagNoisemakerCandidate
                : (byte)0;
            return TryEnqueuePortalEcho(
                in sourceAup,
                in lastPortalAup,
                pathFound,
                usedPortalPath,
                transmission01,
                delaySeconds,
                volume01,
                eventId,
                frame,
                currentTime,
                qualityWeightByte,
                flags);
        }

        public static void TickOwnerFrame(int frame, float currentTime)
        {
            EnsureInitialized();
            RefreshForFrame(frame, currentTime);
        }

        public static bool TryUpdatePredatorEcho(
            int frame,
            in AbsoluteUniversePosition predatorAup,
            float currentTime,
            out AcousticEchoHuntResult result)
        {
            result = default;
            if (_initialized == 0)
                return false;

            AcousticEchoTrailState state = _trailState;
            float silenceSeconds = currentTime - state.LastHeardTime;
            if ((state.Flags & FlagActiveTrail) == 0 ||
                silenceSeconds >= SilenceTimeoutSeconds ||
                !math.isfinite(silenceSeconds) ||
                !IsFiniteAup(in state.InvestigateAup))
            {
                return false;
            }

            float3 runtimePosition = state.InvestigateAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtimePosition)))
            {
                return false;
            }

            result.InvestigateAup = state.InvestigateAup;
            result.SourceAup = state.SourceAup;
            result.RuntimePosition = runtimePosition;
            result.Intensity01 = math.saturate(state.Intensity01);
            result.LastHeardTime = state.LastHeardTime;
            result.SilenceSeconds = math.max(0f, silenceSeconds);
            result.HeadSweep01 = ResolveHeadSweep01(in predatorAup, in state, currentTime);
            result.SourceId = state.SourceId;
            result.Sequence = state.Sequence;
            result.Flags = state.Flags;
            result.QualityWeightByte = state.QualityWeightByte;
            return result.Intensity01 > 0.0001f;
        }

        public static bool TryHydrateFromAcousticEchoTaps(
            NativeArray<Hecton8.Core.Contracts.AcousticEchoTap>.ReadOnly echoTaps,
            int tapCount,
            int frame,
            float currentTime,
            byte qualityWeightByte)
        {
            if (_initialized == 0 || !echoTaps.IsCreated)
                return false;

            int safeCount = math.clamp(tapCount, 0, echoTaps.IsCreated ? math.min(MaxEchoTapsPerFrame, echoTaps.Length) : 0);
            bool any = false;
            for (int i = 0; i < safeCount; i++)
            {
                Hecton8.Core.Contracts.AcousticEchoTap echoTap = echoTaps[i];
                if (!AcousticAup.IsFinite(in echoTap.SourceAup) ||
                    !math.isfinite(echoTap.DelaySeconds) ||
                    !math.isfinite(echoTap.Volume01) ||
                    !math.isfinite(echoTap.Magnitude))
                {
                    continue;
                }

                AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in echoTap.SourceAup);
                EchoTap tap = default;
                tap.SourceAup = sourceAup;
                tap.PortalAup = sourceAup;
                tap.Volume01 = math.saturate(echoTap.Volume01);
                tap.Transmission01 = math.saturate(echoTap.Magnitude);
                tap.DelaySeconds = math.max(0f, echoTap.DelaySeconds);
                tap.LastHeardTime = math.max(0f, currentTime - tap.DelaySeconds);
                uint resolvedSourceId = echoTap.SourceId != 0u ? echoTap.SourceId : echoTap.SoundHash;
                tap.SourceId = resolvedSourceId != 0u ? resolvedSourceId : 1u;
                tap.Sequence = NextSequence(frame);
                tap.Flags = (byte)(FlagDspEchoTap | FlagMinimumQualityDirectNode);
                tap.QualityWeightByte = qualityWeightByte;
                any |= TryEnqueueEchoTap(in tap);
            }

            return any;
        }

        private static bool TryBuildGhostBlipContext(int frame, out GhostBlipContext context)
        {
            context = default;
            if (!SignalBus<PlayerStressSignal>.TryGetLatest(out PlayerStressSignal stress, out int stressSequence) ||
                !math.isfinite(stress.Stress01))
            {
                return false;
            }

            if (stressSequence != _lastPlayerStressSignalSequence || frame < _lastPlayerStressSignalSeenFrame)
            {
                _lastPlayerStressSignalSequence = stressSequence;
                _lastPlayerStressSignalSeenFrame = frame;
            }
            else if (_lastPlayerStressSignalSeenFrame != int.MinValue &&
                     frame - _lastPlayerStressSignalSeenFrame > 8)
            {
                return false;
            }

            float stress01 = math.saturate(stress.Stress01);
            float stressMultiplier = math.saturate((stress01 - 0.75f) * 4f);
            if (stressMultiplier <= 0.0001f)
                return false;

            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext == null ||
                !playerContext.IsInitialized ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) ||
                !pose.Aup.IsFinite())
            {
                return false;
            }

            ResolveDecryptionFrequencies(frame, out float currentFrequency, out float predatorFrequency, out float tunedMask);
            ResolveFlashlightContext(playerContext, pose.Forward, out float3 flashlightForward, out float flashlightConeCos, out float flashlightActive);

            context.PlayerAup = pose.Aup;
            context.PlayerForward = SanitizeDirection(pose.Forward, new float3(0f, 0f, 1f));
            context.FlashlightForward = flashlightForward;
            context.FlashlightConeCos = flashlightConeCos;
            context.FlashlightActive01 = flashlightActive;
            context.Stress01 = stress01;
            context.CurrentFrequency = currentFrequency;
            context.PredatorFrequency = predatorFrequency;
            context.TunedMask01 = tunedMask;
            context.StressMultiplier01 = stressMultiplier;
            return true;
        }

        private static void ResolveFlashlightContext(
            IPlayerRuntimeContext playerContext,
            float3 poseForward,
            out float3 flashlightForward,
            out float flashlightConeCos,
            out float flashlightActive)
        {
            flashlightForward = SanitizeDirection(poseForward, new float3(0f, 0f, 1f));
            flashlightConeCos = 1.1f;
            flashlightActive = 0f;

            Hecton8.Gameplay.PlayerFlashlight flashlight = playerContext.Flashlight;
            if (flashlight == null || !flashlight.IsBeamPresentationActive)
                return;

            if (playerContext.TryGetLookRuntimeState(out PlayerLookState lookState) &&
                math.all(math.isfinite(lookState.AimForward)) &&
                math.lengthsq(lookState.AimForward) > 0.000001f)
            {
                flashlightForward = SanitizeDirection(lookState.AimForward, flashlightForward);
            }

            float spotAngle = math.clamp(
                math.select(42f, flashlight.PresentationSpotAngle, math.isfinite(flashlight.PresentationSpotAngle)),
                1f,
                179f);
            flashlightConeCos = math.cos(math.radians(spotAngle * 0.5f));
            flashlightActive = 1f;
        }

        private static void ResolveDecryptionFrequencies(int frame, out float currentFrequency, out float predatorFrequency, out float tunedMask)
        {
            currentFrequency = -999f;
            predatorFrequency = DefaultGhostPredatorFrequency;
            tunedMask = 0f;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            RefreshExternalReadHandles(vault, frame);
            if (vault.IsCompactionFenceActive)
                return;

            bool resolvedPuzzleFrequency = false;
            if (IsExternalUiVaultHandle(in _decryptionPuzzleHandle, BufferID.TerminalDecryptionPuzzles) &&
                vault.TryReadOnlyHandle(in _decryptionPuzzleHandle, out NativeArray<DecryptionPuzzleDTO>.ReadOnly puzzles) &&
                puzzles.IsCreated &&
                puzzles.Length > 0)
            {
                DecryptionPuzzleDTO puzzle = puzzles[0];
                currentFrequency = math.clamp(
                    math.select(currentFrequency, puzzle.PlayerFrequency, math.isfinite(puzzle.PlayerFrequency)),
                    -999f,
                    12f);
                predatorFrequency = math.clamp(
                    math.select(predatorFrequency, puzzle.TargetFrequency, math.isfinite(puzzle.TargetFrequency)),
                    0.1f,
                    12f);
                resolvedPuzzleFrequency = math.isfinite(puzzle.PlayerFrequency);
            }

            if (IsExternalUiVaultHandle(in _decryptionKnobInputHandle, BufferID.TerminalDecryptionKnobInput) &&
                vault.TryReadOnlyHandle(in _decryptionKnobInputHandle, out NativeArray<DecryptionKnobInputDTO>.ReadOnly inputs) &&
                inputs.IsCreated &&
                inputs.Length > 0)
            {
                DecryptionKnobInputDTO input = inputs[0];
                float frequencyDelta = math.select(0f, input.FrequencyDelta, math.isfinite(input.FrequencyDelta));
                float inputActive = math.select(0f, 1f, (input.Flags != 0u) | (math.abs(frequencyDelta) > 0.0001f));
                float knobPreviewFrequency = math.clamp(3.25f + frequencyDelta, 0.1f, 12f);
                currentFrequency = math.select(currentFrequency, knobPreviewFrequency, !resolvedPuzzleFrequency && inputActive > 0f);
            }

            tunedMask = math.step(math.abs(predatorFrequency - currentFrequency), 0.05f);
        }

        private static float3 SanitizeDirection(float3 direction, float3 fallback)
        {
            float lengthSq = math.dot(direction, direction);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return direction * math.rsqrt(lengthSq);
        }

        private static void PublishGhostHaptics(int frame, in GhostBlipContext context)
        {
            float unsuppressedAmplitude = math.saturate(context.StressMultiplier01 * (1f - context.TunedMask01));
            if (unsuppressedAmplitude > 0.65f &&
                (_lastGhostHapticFrame == int.MinValue ||
                 frame - _lastGhostHapticFrame >= GhostHapticCooldownFrames))
            {
                HapticPulseSignal pulse = new HapticPulseSignal
                {
                    LowFrequencyMotor01 = 0.15f,
                    HighFrequencyMotor01 = math.saturate(unsuppressedAmplitude),
                    DurationSeconds = 0.075f,
                    PriorityFlags = HapticPulseSignal.PriorityTool
                };
                if (SignalBus<HapticPulseSignal>.TryPush(in pulse))
                    _lastGhostHapticFrame = frame;
            }

            if (context.TunedMask01 > 0.5f &&
                (_lastTuningHapticFrame == int.MinValue ||
                 frame - _lastTuningHapticFrame >= TuningHapticCooldownFrames))
            {
                HapticPulseSignal pulse = new HapticPulseSignal
                {
                    LowFrequencyMotor01 = 0.42f,
                    HighFrequencyMotor01 = 0.08f,
                    DurationSeconds = 0.18f,
                    PriorityFlags = HapticPulseSignal.PriorityTool
                };
                if (SignalBus<HapticPulseSignal>.TryPush(in pulse))
                    _lastTuningHapticFrame = frame;
            }
        }

        private static void RefreshForFrame(int frame, float currentTime)
        {
            if (_lastRefreshFrame == frame)
                return;

            if (!math.isfinite(currentTime))
            {
                WriteFaultBlackBox(frame, in _trailState.InvestigateAup);
                DropEchoTapQueue();
                return;
            }

            currentTime = math.max(0f, currentTime);
            DrainPendingProducerFault(frame);

            if (_trackingScheduled != 0)
            {
                if (_trackingHandle.IsCompleted)
                {
                    if (!DispatcherJobFence.TryFinalizeCompleted(ref _trackingHandle))
                        return;

                    try
                    {
                        if (TryResolveVaultBuffer(
                                in _jobResultHandle,
                                BufferID.AcousticEchoTrailState,
                                1,
                                out NativeArray<AcousticEchoTrailState> jobResult))
                        {
                            _trailState = jobResult[0];
                            if ((_trailState.Flags & FlagActiveTrail) != 0 && !IsFiniteAup(in _trailState.InvestigateAup))
                                WriteFaultBlackBox(frame, in _trailState.InvestigateAup);
                        }
                    }
                    finally
                    {
                        _trackingScheduled = 0;
                        ReleaseTrackingMutationGuard();
                    }
                }
                else
                {
                    _lastRefreshFrame = frame;
                    WriteHeartbeatBlackBox(frame, currentTime);
                    return;
                }
            }

            ApplyPendingDataVaultRebindIfIdle();
            if (_initialized == 0)
                return;

            if (!TryAcquireTrackingMutationGuard())
                return;

            bool scheduled = false;
            try
            {
                if (!TryResolveFrameViewsNoAcquire(out NativeArray<EchoTap> frameTaps, out NativeArray<AcousticEchoTrailState> jobResult))
                {
                    DropEchoTapQueueUnderAcquiredGuard();
                    return;
                }

                _cachedQualityWeightByte = ResolveQualityWeightByte();
                int tapCount = DrainEchoTapQueue(frameTaps, frame, currentTime);
                tapCount = AppendMovementSignals(frameTaps, tapCount, frame, currentTime);
                tapCount = AppendAcousticPingSignals(frameTaps, tapCount, frame, currentTime);
                JobHandle dependency = default;
                int trackingTapCount = tapCount;
                if (tapCount < MaxEchoTapsPerFrame &&
                    tapCount < frameTaps.Length &&
                    TryBuildGhostBlipContext(frame, out GhostBlipContext ghostContext))
                {
                    PublishGhostHaptics(frame, in ghostContext);
                    dependency = new GenerateAcousticPingsJob
                    {
                        Taps = frameTaps,
                        PlayerAup = ghostContext.PlayerAup,
                        PlayerForward = ghostContext.PlayerForward,
                        FlashlightForward = ghostContext.FlashlightForward,
                        FlashlightConeCos = ghostContext.FlashlightConeCos,
                        FlashlightActive01 = ghostContext.FlashlightActive01,
                        StressLevel01 = ghostContext.Stress01,
                        CurrentFrequency = ghostContext.CurrentFrequency,
                        PredatorFrequency = ghostContext.PredatorFrequency,
                        CurrentTime = currentTime,
                        GlobalQualityWeight = DecodeQualityWeightByte(_cachedQualityWeightByte),
                        StartIndex = tapCount,
                        Capacity = math.min(MaxEchoTapsPerFrame, frameTaps.Length),
                        Frame = frame,
                        Seed = NextSequence(frame),
                        QualityWeightByte = _cachedQualityWeightByte
                    }.Schedule();
                    trackingTapCount = math.min(MaxEchoTapsPerFrame, frameTaps.Length);
                }

                jobResult[0] = _trailState;
                _trackingHandle = new EchoTrackingJob
                {
                    Taps = frameTaps,
                    Result = jobResult,
                    Previous = _trailState,
                    CurrentTime = currentTime,
                    TapCount = trackingTapCount,
                    SilenceTimeoutSeconds = SilenceTimeoutSeconds
                }.Schedule(dependency);
                _trackingScheduled = 1;
                scheduled = true;
                _lastRefreshFrame = frame;
                WriteHeartbeatBlackBox(frame, currentTime);
            }
            finally
            {
                if (!scheduled)
                    ReleaseTrackingMutationGuard();
            }
        }

        private static int DrainEchoTapQueue(NativeArray<EchoTap> frameTaps, int frame, float currentTime)
        {
            int count = 0;
            int limit = math.min(MaxEchoTapsPerFrame, frameTaps.IsCreated ? frameTaps.Length : 0);
            if (!TryResolvePendingTapsNoAcquire(out NativeArray<EchoTap> pendingTaps))
            {
                _queuedEchoTapCount = 0;
                return count;
            }

            int pendingCount = math.min(_queuedEchoTapCount, pendingTaps.IsCreated ? pendingTaps.Length : 0);
            for (int i = 0; i < pendingCount && count < limit; i++)
            {
                EchoTap tap = pendingTaps[i];
                if (!IsValidTap(in tap))
                {
                    WriteFaultBlackBox(frame, in tap.PortalAup);
                    continue;
                }

                if (tap.LastHeardTime <= 0f || !math.isfinite(tap.LastHeardTime))
                    tap.LastHeardTime = currentTime;

                frameTaps[count++] = tap;
            }

            for (int i = 0; i < pendingCount; i++)
            {
                pendingTaps[i] = default;
            }

            _queuedEchoTapCount = 0;
            return count;
        }

        private static int AppendMovementSignals(NativeArray<EchoTap> frameTaps, int count, int frame, float currentTime)
        {
            ReadOnlySpan<MovementAcousticSignal> signals = SignalBus<MovementAcousticSignal>.GetFrameSnapshot();
            int remainingCapacity = math.min(MaxEchoTapsPerFrame, frameTaps.Length) - count;
            if (signals.Length > math.max(0, remainingCapacity))
                PublishMovementSignalCapacityWarning(frame, signals.Length, remainingCapacity);

            int limit = math.min(signals.Length, math.max(0, remainingCapacity));
            for (int i = 0; i < limit; i++)
            {
                ref readonly MovementAcousticSignal signal = ref signals[i];
                if (!IsFiniteAup(in signal.PositionAup) ||
                    !math.isfinite(signal.Volume) ||
                    !math.isfinite(signal.VelocitySq))
                {
                    WriteFaultBlackBox(frame, in signal.PositionAup);
                    continue;
                }

                float movement01 = math.saturate(math.max(signal.Volume, signal.VelocitySq * MovementVelocityToVolume));
                if (movement01 <= 0.01f)
                    continue;

                EchoTap tap = default;
                tap.SourceAup = signal.PositionAup;
                tap.PortalAup = signal.PositionAup;
                tap.Volume01 = movement01;
                tap.Transmission01 = 1f;
                tap.DelaySeconds = 0f;
                tap.LastHeardTime = currentTime;
                tap.SourceId = signal.SourceId;
                tap.Sequence = NextSequence(frame);
                tap.Flags = (byte)(FlagMovementBreadcrumb | FlagMinimumQualityDirectNode);
                tap.QualityWeightByte = _cachedQualityWeightByte;
                frameTaps[count++] = tap;
                if (count >= MaxEchoTapsPerFrame || count >= frameTaps.Length)
                    break;
            }

            return count;
        }

        private static void PublishMovementSignalCapacityWarning(int frame, int observedCount, int remainingCapacity)
        {
            if (frame < _nextMovementSignalCapacityWarningFrame)
                return;

            int droppedCount = math.max(0, observedCount - math.max(0, remainingCapacity));
            if (droppedCount <= 0)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                MovementSignalCapacityWarningHash,
                AcousticEchoContextHash,
                droppedCount);
            _nextMovementSignalCapacityWarningFrame = frame + MovementSignalCapacityWarningCooldownFrames;
        }

        private static int AppendAcousticPingSignals(NativeArray<EchoTap> frameTaps, int count, int frame, float currentTime)
        {
            ReadOnlySpan<AcousticPingSignal> signals = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            int limit = math.min(signals.Length, math.min(MaxEchoTapsPerFrame, frameTaps.Length) - count);
            for (int i = 0; i < limit; i++)
            {
                ref readonly AcousticPingSignal signal = ref signals[i];
                if (!IsFiniteAup(in signal.PositionAup) ||
                    !math.isfinite(signal.Intensity01) ||
                    signal.Intensity01 <= 0.01f ||
                    signal.Channel == AcousticPingSignal.ChannelLeviathanRoar ||
                    (signal.Flags & AcousticPingSignal.FlagLeviathanRoar) != 0)
                {
                    continue;
                }

                EchoTap tap = default;
                tap.SourceAup = signal.PositionAup;
                tap.PortalAup = signal.PositionAup;
                tap.Volume01 = math.saturate(signal.Intensity01);
                tap.Transmission01 = 1f;
                tap.DelaySeconds = 0f;
                tap.LastHeardTime = currentTime;
                tap.SourceId = signal.SourceId;
                tap.Sequence = NextSequence(frame);
                tap.Flags = (byte)(FlagPingBreadcrumb | FlagMinimumQualityDirectNode);
                if ((signal.Flags & AcousticPingSignal.FlagActiveSonar) != 0 ||
                    signal.Channel == AcousticPingSignal.ChannelActiveSonar)
                {
                    tap.Flags |= FlagNoisemakerCandidate;
                }

                tap.QualityWeightByte = _cachedQualityWeightByte;
                frameTaps[count++] = tap;
                if (count >= MaxEchoTapsPerFrame || count >= frameTaps.Length)
                    break;
            }

            return count;
        }

        private static float ResolveHeadSweep01(
            in AbsoluteUniversePosition predatorAup,
            in AcousticEchoTrailState state,
            float currentTime)
        {
            if (state.Intensity01 <= 0.01f ||
                !IsFiniteAup(in predatorAup))
            {
                return 0f;
            }

            double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in state.InvestigateAup, in predatorAup);
            if (!math.all(math.isfinite(delta)))
                return 0f;

            double distanceSq = math.csum(delta * delta);
            if (!math.isfinite(distanceSq))
                return 0f;

            float quality01 = DecodeQualityWeightByte(state.QualityWeightByte);
            float qualityCurve = SmoothStep01(math.saturate((quality01 - 0.12f) * math.rcp(0.88f)));
            float distance01 = math.saturate(1f - (float)(distanceSq * math.rcp(1600.0)));
            return SinPolynomial7(currentTime * 4.65f) * math.saturate(state.Intensity01 * (0.45f + distance01)) * qualityCurve;
        }

        private static void DropEchoTapQueue()
        {
            if (!TryAcquireAcousticMutationGuard(_dataVault, PendingTapMutationGuardMask, out IDataVault guardVault))
            {
                _queuedEchoTapCount = 0;
                return;
            }

            try
            {
                DropEchoTapQueueUnderAcquiredGuard();
            }
            finally
            {
                ReleaseAcousticMutationGuard(guardVault, PendingTapMutationGuardMask);
            }
        }

        private static void DropEchoTapQueueUnderAcquiredGuard()
        {
            if (!TryResolvePendingTapsNoAcquire(out NativeArray<EchoTap> pendingTaps))
            {
                _queuedEchoTapCount = 0;
                return;
            }

            int pendingCount = math.min(_queuedEchoTapCount, pendingTaps.IsCreated ? pendingTaps.Length : 0);
            for (int i = 0; i < pendingCount; i++)
            {
                pendingTaps[i] = default;
            }

            _queuedEchoTapCount = 0;
        }

        private static uint NextSequence(int frame)
        {
            _sequence++;
            if (_sequence == 0u)
                _sequence = 1u;

            return ((uint)math.max(0, frame) << 16) ^ _sequence;
        }

        public static byte EncodeQualityWeightByte(float qualityWeight01)
        {
            float safe = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            return (byte)math.clamp((int)math.round(safe * 255f), 0, 255);
        }

        private static float DecodeQualityWeightByte(byte qualityWeightByte)
        {
            return qualityWeightByte * (1f / 255f);
        }

        private static byte ResolveQualityWeightByte()
        {
            return EncodeQualityWeightByte(HomeostasisBrain.GlobalQualityWeight);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        internal static float SinPolynomial7(float angle)
        {
            float x = angle - TwoPi * math.floor((angle + Pi) * InvTwoPi);
            x = math.select(x, Pi - x, x > HalfPi);
            x = math.select(x, -Pi - x, x < -HalfPi);
            float x2 = x * x;
            return x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.000198409f)));
        }

        private static AbsoluteUniversePosition ToAbsoluteUniversePosition(in AcousticAup aup)
        {
            return new AbsoluteUniversePosition
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.Local.x,
                LocalY = aup.Local.y,
                LocalZ = aup.Local.z
            };
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        private static bool IsValidTap(in EchoTap tap)
        {
            return IsFiniteAup(in tap.PortalAup) &&
                   IsFiniteAup(in tap.SourceAup) &&
                   math.isfinite(tap.Volume01) &&
                   math.isfinite(tap.Transmission01) &&
                   math.isfinite(tap.DelaySeconds) &&
                   math.isfinite(tap.LastHeardTime);
        }

        private static void RecordPendingProducerFault(in AbsoluteUniversePosition faultAup)
        {
            _pendingProducerFaultAup = faultAup;
            _pendingProducerFault = 1;
        }

        private static void DrainPendingProducerFault(int frame)
        {
            if (_pendingProducerFault == 0)
                return;

            AbsoluteUniversePosition faultAup = _pendingProducerFaultAup;
            _pendingProducerFault = 0;
            WriteFaultBlackBox(frame, in faultAup);
        }

        private static uint HashState(in AcousticEchoTrailState state)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)state.InvestigateAup.GridX) * 16777619u;
            hash = (hash ^ (uint)(state.InvestigateAup.GridX >> 32)) * 16777619u;
            hash = (hash ^ (uint)state.InvestigateAup.GridY) * 16777619u;
            hash = (hash ^ (uint)(state.InvestigateAup.GridY >> 32)) * 16777619u;
            hash = (hash ^ (uint)state.InvestigateAup.GridZ) * 16777619u;
            hash = (hash ^ (uint)(state.InvestigateAup.GridZ >> 32)) * 16777619u;
            hash = (hash ^ state.Sequence) * 16777619u;
            hash = (hash ^ state.SourceId) * 16777619u;
            return hash;
        }

        private static void WriteHeartbeatBlackBox(int frame, float currentTime)
        {
            AcousticEchoTrailState state = _trailState;
            float silenceSeconds = currentTime - state.LastHeardTime;
            if (!math.isfinite(silenceSeconds))
                silenceSeconds = SilenceTimeoutSeconds;

            WriteBlackBoxOnce(frame, in state, math.max(0f, silenceSeconds));
        }

        private static void WriteBlackBoxOnce(int frame, in AcousticEchoTrailState state, float silenceSeconds)
        {
            if (_lastBlackBoxFrame == frame)
                return;

            WriteBlackBox(frame, in state, silenceSeconds);
            _lastBlackBoxFrame = frame;
        }

        private static void WriteBlackBox(int frame, in AcousticEchoTrailState state, float silenceSeconds)
        {
            IDataVault guardVault = null;
            bool releaseGuard = false;
            if (_trackingMutationGuardVault == null)
            {
                if (!TryAcquireAcousticMutationGuard(_dataVault, BlackBoxMutationGuardMask, out guardVault))
                    return;

                releaseGuard = true;
            }

            try
            {
                if (!TryResolveBlackBoxNoAcquire(out NativeArray<AcousticEchoBlackBoxEntry> blackBox))
                    return;

                int index = _blackBoxCursor % blackBox.Length;
                AcousticEchoBlackBoxEntry entry = default;
                entry.Frame = frame;
                entry.AcousticHuntsTriggered = state.AcousticHuntsTriggered;
                entry.SourceId = state.SourceId;
                entry.Sequence = state.Sequence;
                entry.Intensity01 = state.Intensity01;
                entry.LastHeardTime = state.LastHeardTime;
                entry.SilenceSeconds = silenceSeconds;
                entry.Flags = state.Flags;
                entry.PortalGridX = state.InvestigateAup.GridX;
                entry.PortalGridY = state.InvestigateAup.GridY;
                entry.PortalGridZ = state.InvestigateAup.GridZ;
                entry.PortalLocal.x = state.InvestigateAup.LocalX;
                entry.PortalLocal.y = state.InvestigateAup.LocalY;
                entry.PortalLocal.z = state.InvestigateAup.LocalZ;
                entry.StateHash = HashState(in state);
                blackBox[index] = entry;
                _blackBoxCursor = (index + 1) % blackBox.Length;
            }
            finally
            {
                if (releaseGuard)
                    ReleaseAcousticMutationGuard(guardVault, BlackBoxMutationGuardMask);
            }
        }

        private static void WriteFaultBlackBox(int frame, in AbsoluteUniversePosition faultAup)
        {
            AcousticEchoTrailState state = _trailState;
            state.InvestigateAup = faultAup;
            state.Flags = FlagSilenceLost;
            WriteBlackBox(frame, in state, 0f);
            _lastBlackBoxFrame = frame;
            if (_blackBoxDumped == 0)
            {
                _blackBoxDumped = 1;
                DumpBlackBox();
            }
        }

        private static void DumpBlackBox()
        {
            if (!TryReadOnlyBlackBox(out NativeArray<AcousticEchoBlackBoxEntry>.ReadOnly blackBox))
                return;

            _ = blackBox.Length;
        }
    }
}
