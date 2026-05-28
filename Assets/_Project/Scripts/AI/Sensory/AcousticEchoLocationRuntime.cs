using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI.Sensory
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
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
        [FieldOffset(124)] private uint _pad1;
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
                float intensity = math.saturate(volume * math.max(0.05f, transmission));
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

    public static class AcousticEchoLocationRuntime
    {
        private const float Pi = 3.14159265358979323846f;
        private const float TwoPi = 6.28318530717958647692f;
        private const float HalfPi = 1.57079632679489661923f;
        private const float InvTwoPi = 0.15915494309189533577f;

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

        private const string DumpRelativePath = "Docs/AgentLogs/Dump_13AI.bin";
        private const float MovementVelocityToVolume = 0.025f;
        private const int MaxQueuedEchoTaps = MaxEchoTapsPerFrame;

        private static readonly AcousticEchoHotSwapBridge s_hotSwapBridge = new AcousticEchoHotSwapBridge(); // COLD ALLOC: AcousticEchoHotSwapBridge[1] - static acoustic echo DataVault rebind listener - owner: AcousticEchoLocationRuntime
        private static IDataVault _dataVault;
        private static VaultGenerationHandle<EchoTap> _frameTapsHandle;
        private static VaultGenerationHandle<EchoTap> _pendingTapsHandle;
        private static VaultGenerationHandle<AcousticEchoTrailState> _jobResultHandle;
        private static VaultGenerationHandle<AcousticEchoBlackBoxEntry> _blackBoxHandle;
        private static JobHandle _trackingHandle;
        private static AcousticEchoTrailState _trailState;
        private static int _trackingScheduled;
        private static int _initialized;
        private static int _lastRefreshFrame = int.MinValue;
        private static int _lastBlackBoxFrame = int.MinValue;
        private static int _blackBoxCursor;
        private static int _blackBoxDumped;
        private static int _queuedEchoTapCount;
        private static int _pendingProducerFault;
        private static AbsoluteUniversePosition _pendingProducerFaultAup;
        private static int _hotSwapRegistered;
        private static uint _sequence;
        private static byte _cachedQualityWeightByte;

        public static uint AcousticHuntsTriggered => _trailState.AcousticHuntsTriggered;

        public static void EnsureInitialized()
        {
            TryRegisterHotSwapListener();
            if (_initialized != 0)
            {
                EnsureVaultBuffers();
                return;
            }

            _cachedQualityWeightByte = ResolveQualityWeightByte();
            EnsureVaultBuffers();
            _initialized = 1;
        }

        public static void Dispose()
        {
            TryUnregisterHotSwapListener();
            if (_initialized == 0)
                return;

            if (_trackingScheduled != 0)
            {
                DispatcherJobFence.TryComplete(ref _trackingHandle, forceComplete: true);
                _trackingScheduled = 0;
            }

            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = null;
            _trailState = default;
            _lastRefreshFrame = int.MinValue;
            _lastBlackBoxFrame = int.MinValue;
            _blackBoxCursor = 0;
            _blackBoxDumped = 0;
            _queuedEchoTapCount = 0;
            _pendingProducerFault = 0;
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

            if (!TryResolvePendingTapsNoAcquire(out NativeArray<EchoTap> pendingTaps))
                return false;

            if (!pendingTaps.IsCreated || _queuedEchoTapCount >= pendingTaps.Length)
                return false;

            pendingTaps[_queuedEchoTapCount] = tap;
            _queuedEchoTapCount++;
            return true;
        }

        private static bool EnsureVaultBuffers()
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
                EnsureVaultBuffers();
                return;
            }

            CompleteTrackingFenceForVaultRelease();
            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = nextVault;
            _trailState = default;
            _lastRefreshFrame = int.MinValue;
            _lastBlackBoxFrame = int.MinValue;
            _blackBoxCursor = 0;
            _blackBoxDumped = 0;
            _queuedEchoTapCount = 0;
            _pendingProducerFault = 0;
            _pendingProducerFaultAup = default;
            _sequence = 0u;

            if (_initialized != 0 && _dataVault != null)
                EnsureVaultBuffers();
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
            }
        }

        private static void ClearVaultHandles()
        {
            _frameTapsHandle = default;
            _pendingTapsHandle = default;
            _jobResultHandle = default;
            _blackBoxHandle = default;
        }

        private static void CompleteTrackingFenceForVaultRelease()
        {
            if (_trackingScheduled == 0)
                return;

            DispatcherJobFence.TryComplete(ref _trackingHandle, forceComplete: true);
            _trackingScheduled = 0;
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

        private static bool EnsureFrameViews(
            out NativeArray<EchoTap> frameTaps,
            out NativeArray<AcousticEchoTrailState> jobResult)
        {
            frameTaps = default;
            jobResult = default;

            if (!EnsureVaultBuffers())
                return false;

            IDataVault vault = _dataVault;
            if (TryResolveVaultBuffer(in _frameTapsHandle, BufferID.AcousticEchoFrameTaps, MaxEchoTapsPerFrame, out frameTaps) &&
                TryResolveVaultBuffer(in _jobResultHandle, BufferID.AcousticEchoTrailState, 1, out jobResult))
            {
                return true;
            }

            CompleteTrackingFenceForVaultRelease();
            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            if (!EnsureVaultBuffers())
                return false;

            return TryResolveVaultBuffer(in _frameTapsHandle, BufferID.AcousticEchoFrameTaps, MaxEchoTapsPerFrame, out frameTaps) &&
                   TryResolveVaultBuffer(in _jobResultHandle, BufferID.AcousticEchoTrailState, 1, out jobResult);
        }

        private static bool EnsureBlackBox(out NativeArray<AcousticEchoBlackBoxEntry> blackBox)
        {
            blackBox = default;
            if (!EnsureVaultBuffers())
                return false;

            if (TryResolveVaultBuffer(in _blackBoxHandle, BufferID.AcousticEchoBlackBox, BlackBoxFrameCount, out blackBox))
                return true;

            CompleteTrackingFenceForVaultRelease();
            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            if (!EnsureVaultBuffers())
                return false;

            return TryResolveVaultBuffer(in _blackBoxHandle, BufferID.AcousticEchoBlackBox, BlackBoxFrameCount, out blackBox);
        }

        private static bool EnsurePendingTaps(out NativeArray<EchoTap> pendingTaps)
        {
            pendingTaps = default;
            return EnsureVaultBuffers() &&
                   TryResolveVaultBuffer(in _pendingTapsHandle, BufferID.AcousticEchoPendingTaps, MaxQueuedEchoTaps, out pendingTaps);
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

            if (!EnsureFrameViews(out NativeArray<EchoTap> frameTaps, out NativeArray<AcousticEchoTrailState> jobResult))
            {
                DropEchoTapQueue();
                return;
            }

            if (_trackingScheduled != 0)
            {
                if (_trackingHandle.IsCompleted)
                {
                    if (!DispatcherJobFence.TryFinalizeCompleted(ref _trackingHandle))
                        return;

                    _trailState = jobResult[0];
                    if ((_trailState.Flags & FlagActiveTrail) != 0 && !IsFiniteAup(in _trailState.InvestigateAup))
                        WriteFaultBlackBox(frame, in _trailState.InvestigateAup);
                    _trackingScheduled = 0;
                }
                else
                {
                    _lastRefreshFrame = frame;
                    WriteHeartbeatBlackBox(frame, currentTime);
                    return;
                }
            }

            _cachedQualityWeightByte = ResolveQualityWeightByte();
            int tapCount = DrainEchoTapQueue(frameTaps, frame, currentTime);
            tapCount = AppendMovementSignals(frameTaps, tapCount, frame, currentTime);
            tapCount = AppendAcousticPingSignals(frameTaps, tapCount, frame, currentTime);

            jobResult[0] = _trailState;
            _trackingHandle = new EchoTrackingJob
            {
                Taps = frameTaps,
                Result = jobResult,
                Previous = _trailState,
                CurrentTime = currentTime,
                TapCount = tapCount,
                SilenceTimeoutSeconds = SilenceTimeoutSeconds
            }.Schedule();
            _trackingScheduled = 1;
            _lastRefreshFrame = frame;
            WriteHeartbeatBlackBox(frame, currentTime);
        }

        private static int DrainEchoTapQueue(NativeArray<EchoTap> frameTaps, int frame, float currentTime)
        {
            int count = 0;
            int limit = math.min(MaxEchoTapsPerFrame, frameTaps.IsCreated ? frameTaps.Length : 0);
            if (!EnsurePendingTaps(out NativeArray<EchoTap> pendingTaps))
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
            int limit = math.min(signals.Length, math.min(MaxEchoTapsPerFrame, frameTaps.Length) - count);
            for (int i = 0; i < limit; i++)
            {
                ref readonly MovementAcousticSignal signal = ref signals[i];
                if (!IsFiniteAup(in signal.PositionAup) ||
                    !math.isfinite(signal.Volume) ||
                    !math.isfinite(signal.VelocitySq))
                {
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
            if (!EnsurePendingTaps(out NativeArray<EchoTap> pendingTaps))
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

        private static float SinPolynomial7(float angle)
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
            if (!EnsureBlackBox(out NativeArray<AcousticEchoBlackBoxEntry> blackBox))
                return;

            int index = _blackBoxCursor % blackBox.Length;
            blackBox[index] = new AcousticEchoBlackBoxEntry
            {
                Frame = frame,
                AcousticHuntsTriggered = state.AcousticHuntsTriggered,
                SourceId = state.SourceId,
                Sequence = state.Sequence,
                Intensity01 = state.Intensity01,
                LastHeardTime = state.LastHeardTime,
                SilenceSeconds = silenceSeconds,
                Flags = state.Flags,
                PortalGridX = state.InvestigateAup.GridX,
                PortalGridY = state.InvestigateAup.GridY,
                PortalGridZ = state.InvestigateAup.GridZ,
                PortalLocal = new float3(state.InvestigateAup.LocalX, state.InvestigateAup.LocalY, state.InvestigateAup.LocalZ),
                StateHash = HashState(in state)
            };
            _blackBoxCursor = (index + 1) % blackBox.Length;
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
            if (!EnsureBlackBox(out NativeArray<AcousticEchoBlackBoxEntry> blackBox))
                return;

            try
            {
                string dumpPath = ResolveDumpPath();
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(blackBox.Length);
                    writer.Write(_blackBoxCursor);
                    for (int i = 0; i < blackBox.Length; i++)
                    {
                        AcousticEchoBlackBoxEntry entry = blackBox[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.AcousticHuntsTriggered);
                        writer.Write(entry.SourceId);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.Intensity01);
                        writer.Write(entry.LastHeardTime);
                        writer.Write(entry.SilenceSeconds);
                        writer.Write(entry.Flags);
                        writer.Write(entry.PortalGridX);
                        writer.Write(entry.PortalGridY);
                        writer.Write(entry.PortalGridZ);
                        writer.Write(entry.PortalLocal.x);
                        writer.Write(entry.PortalLocal.y);
                        writer.Write(entry.PortalLocal.z);
                        writer.Write(entry.StateHash);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static string ResolveDumpPath()
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
                return Path.GetFullPath(Path.Combine(dataPath, "..", DumpRelativePath));

            return Path.GetFullPath(DumpRelativePath);
        }
    }
}
