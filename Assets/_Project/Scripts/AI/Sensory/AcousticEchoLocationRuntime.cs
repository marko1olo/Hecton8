using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Audio;
using Hecton8.Audio.Propagation;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Sensory
{
    [StructLayout(LayoutKind.Sequential)]
    public struct EchoTap
    {
        public AbsoluteUniversePosition SourceAup;
        public AbsoluteUniversePosition PortalAup;
        public float Volume01;
        public float Transmission01;
        public float DelaySeconds;
        public float LastHeardTime;
        public uint SourceId;
        public uint Sequence;
        public byte Flags;
        public byte QualityTier;
        private ushort _reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AcousticEchoHuntResult
    {
        public AbsoluteUniversePosition InvestigateAup;
        public AbsoluteUniversePosition SourceAup;
        public float3 RuntimePosition;
        public float Intensity01;
        public float LastHeardTime;
        public float SilenceSeconds;
        public float HeadSweep01;
        public uint SourceId;
        public uint Sequence;
        public byte Flags;
        public byte QualityTier;
        private ushort _reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AcousticEchoTrailState
    {
        public AbsoluteUniversePosition InvestigateAup;
        public AbsoluteUniversePosition SourceAup;
        public float Intensity01;
        public float LastHeardTime;
        public uint SourceId;
        public uint Sequence;
        public uint AcousticHuntsTriggered;
        public byte Flags;
        public byte QualityTier;
        private ushort _reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AcousticEchoBlackBoxEntry
    {
        public int Frame;
        public uint AcousticHuntsTriggered;
        public uint SourceId;
        public uint Sequence;
        public float Intensity01;
        public float LastHeardTime;
        public float SilenceSeconds;
        public uint Flags;
        public long PortalGridX;
        public long PortalGridY;
        public long PortalGridZ;
        public float3 PortalLocal;
        public uint StateHash;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct EchoTrackingJob : IJob
    {
        [ReadOnly] public NativeArray<EchoTap> Taps;
        public NativeArray<AcousticEchoTrailState> Result;
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
                state.AcousticHuntsTriggered = Previous.AcousticHuntsTriggered + 1u;
                state.Flags = (byte)(bestTap.Flags | AcousticEchoLocationRuntime.FlagActiveTrail);
                state.QualityTier = bestTap.QualityTier;
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
        public const byte FlagActiveTrail = 1 << 0;
        public const byte FlagPortalBreadcrumb = 1 << 1;
        public const byte FlagMovementBreadcrumb = 1 << 2;
        public const byte FlagPingBreadcrumb = 1 << 3;
        public const byte FlagNoisemakerCandidate = 1 << 4;
        public const byte FlagLowTierDirectNode = 1 << 5;
        public const byte FlagDspEchoTap = 1 << 6;
        public const byte FlagSilenceLost = 1 << 7;

        public const int MaxEchoTapsPerFrame = 32;
        public const int BlackBoxFrameCount = 300;
        public const float SilenceTimeoutSeconds = 5f;

        private const string NativeOwner = "ACOUSTIC_ECHO_LOCATION_AI";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_ACOUSTIC_ECHO_LOCATION_AI.bin";
        private const float MovementVelocityToVolume = 0.025f;

        private static NativeQueue<EchoTap> _echoTapQueue;
        private static NativeArray<EchoTap> _frameTaps;
        private static NativeArray<AcousticEchoTrailState> _jobResult;
        private static NativeArray<AcousticEchoBlackBoxEntry> _blackBox;
        private static JobHandle _trackingHandle;
        private static AcousticEchoTrailState _trailState;
        private static int _trackingScheduled;
        private static int _initialized;
        private static int _lastRefreshFrame = int.MinValue;
        private static int _blackBoxCursor;
        private static uint _sequence;
        private static byte _cachedQualityTier;

        public static uint AcousticHuntsTriggered => _trailState.AcousticHuntsTriggered;

        public static NativeQueue<EchoTap>.ParallelWriter EchoTapWriter
        {
            get
            {
                EnsureInitialized();
                return _echoTapQueue.AsParallelWriter();
            }
        }

        public static void EnsureInitialized()
        {
            if (_initialized != 0)
                return;

            _echoTapQueue = new NativeQueue<EchoTap>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EchoTap>[64] - acoustic echo tap producer bridge - owner: ACOUSTIC_ECHO_LOCATION_AI
            _frameTaps = new NativeArray<EchoTap>(MaxEchoTapsPerFrame, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<EchoTap>[32] - bounded echo tracking frame slab - owner: ACOUSTIC_ECHO_LOCATION_AI
            _jobResult = new NativeArray<AcousticEchoTrailState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AcousticEchoTrailState>[1] - Burst echo tracking result - owner: ACOUSTIC_ECHO_LOCATION_AI
            _blackBox = new NativeArray<AcousticEchoBlackBoxEntry>(BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AcousticEchoBlackBoxEntry>[300] - acoustic hunt black box - owner: ACOUSTIC_ECHO_LOCATION_AI
            NativeMemorySentinel.RegisterNativeQueue(_echoTapQueue, 64, NativeOwner, nameof(_echoTapQueue), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_frameTaps, NativeOwner, nameof(_frameTaps), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobResult, NativeOwner, nameof(_jobResult), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_blackBox, NativeOwner, nameof(_blackBox), NativeAllocationLifetime.Session);
            _cachedQualityTier = ResolveQualityTier();
            _initialized = 1;
        }

        public static void Dispose()
        {
            if (_initialized == 0)
                return;

            if (_trackingScheduled != 0)
            {
                _trackingHandle.Complete();
                _trackingScheduled = 0;
            }

            if (_echoTapQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeOwner, nameof(_echoTapQueue));
                _echoTapQueue.Dispose();
                _echoTapQueue = default;
            }

            DisposeArray(ref _frameTaps);
            DisposeArray(ref _jobResult);
            DisposeArray(ref _blackBox);
            _trailState = default;
            _lastRefreshFrame = int.MinValue;
            _blackBoxCursor = 0;
            _sequence = 0u;
            _initialized = 0;
        }

        public static bool TryEnqueueEchoTap(in EchoTap tap)
        {
            EnsureInitialized();
            if (!IsFiniteAup(in tap.PortalAup) ||
                !IsFiniteAup(in tap.SourceAup) ||
                !math.isfinite(tap.Volume01) ||
                !math.isfinite(tap.Transmission01) ||
                !math.isfinite(tap.LastHeardTime))
            {
                WriteFaultBlackBox(0, in tap.PortalAup);
                return false;
            }

            _echoTapQueue.Enqueue(tap);
            return true;
        }

        public static bool TryEnqueuePortalEcho(
            in AbsoluteUniversePosition sourceAup,
            in AcousticPathResult pathResult,
            float volume01,
            uint sourceId,
            int frame,
            float currentTime,
            byte qualityTier,
            byte extraFlags = 0)
        {
            if (pathResult.Status != AcousticPathStatus.PathFound ||
                pathResult.UsedPortalPath == 0 ||
                !AcousticAup.IsFinite(in pathResult.LastPortalAup) ||
                !math.isfinite(pathResult.Transmission01) ||
                !math.isfinite(pathResult.DelaySeconds))
            {
                return false;
            }

            EchoTap tap = default;
            tap.SourceAup = sourceAup;
            tap.PortalAup = ToAbsoluteUniversePosition(in pathResult.LastPortalAup);
            tap.Volume01 = math.saturate(volume01);
            tap.Transmission01 = math.saturate(pathResult.Transmission01);
            tap.DelaySeconds = math.max(0f, pathResult.DelaySeconds);
            tap.LastHeardTime = math.max(0f, currentTime - tap.DelaySeconds);
            tap.SourceId = sourceId;
            tap.Sequence = NextSequence(frame);
            tap.Flags = (byte)(FlagPortalBreadcrumb | FlagDspEchoTap | extraFlags);
            tap.QualityTier = qualityTier;
            return TryEnqueueEchoTap(in tap);
        }

        public static bool TryPublishPortalPropagationEcho(
            in SoundEmissionSignal emission,
            in AcousticPathResult pathResult,
            int frame,
            float currentTime,
            byte qualityTier)
        {
            AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in emission.SourceAup);
            byte flags = (emission.Flags & AcousticPortalFlags.StationaryEmitter) != 0
                ? FlagNoisemakerCandidate
                : (byte)0;
            return TryEnqueuePortalEcho(
                in sourceAup,
                in pathResult,
                emission.Volume,
                emission.EventID,
                frame,
                currentTime,
                qualityTier,
                flags);
        }

        public static bool TryResolvePredatorEcho(
            int frame,
            in AbsoluteUniversePosition predatorAup,
            float currentTime,
            out AcousticEchoHuntResult result)
        {
            EnsureInitialized();
            RefreshForFrame(frame, currentTime);
            result = default;

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
                WriteFaultBlackBox(frame, in state.InvestigateAup);
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
            result.QualityTier = state.QualityTier;
            WriteBlackBox(frame, in state, result.SilenceSeconds);
            return result.Intensity01 > 0.0001f;
        }

        public static bool TryHydrateFromSonarEchoTaps(
            NativeArray<SonarEchoTap>.ReadOnly sonarTaps,
            int tapCount,
            in AbsoluteUniversePosition sourceAup,
            float volume01,
            uint sourceId,
            int frame,
            float currentTime,
            byte qualityTier)
        {
            EnsureInitialized();
            int safeCount = math.clamp(tapCount, 0, sonarTaps.IsCreated ? math.min(MaxEchoTapsPerFrame, sonarTaps.Length) : 0);
            bool any = false;
            for (int i = 0; i < safeCount; i++)
            {
                SonarEchoTap sonarTap = sonarTaps[i];
                if (!math.isfinite(sonarTap.DelaySeconds) ||
                    !math.isfinite(sonarTap.Attenuation))
                {
                    continue;
                }

                EchoTap tap = default;
                tap.SourceAup = sourceAup;
                tap.PortalAup = sourceAup;
                tap.Volume01 = math.saturate(volume01);
                tap.Transmission01 = math.saturate(sonarTap.Attenuation);
                tap.DelaySeconds = math.max(0f, sonarTap.DelaySeconds);
                tap.LastHeardTime = math.max(0f, currentTime - tap.DelaySeconds);
                tap.SourceId = sourceId;
                tap.Sequence = NextSequence(frame);
                tap.Flags = (byte)(FlagDspEchoTap | FlagLowTierDirectNode);
                tap.QualityTier = qualityTier;
                any |= TryEnqueueEchoTap(in tap);
            }

            return any;
        }

        private static void RefreshForFrame(int frame, float currentTime)
        {
            if (_lastRefreshFrame == frame)
                return;

            if (_trackingScheduled != 0)
            {
                if (_trackingHandle.IsCompleted)
                {
                    _trackingHandle.Complete();
                    _trailState = _jobResult[0];
                    _trackingScheduled = 0;
                }
                else
                {
                    return;
                }
            }

            _cachedQualityTier = ResolveQualityTier();
            int tapCount = DrainEchoTapQueue(currentTime);
            tapCount = AppendMovementSignals(tapCount, frame, currentTime);
            tapCount = AppendAcousticPingSignals(tapCount, frame, currentTime);

            _jobResult[0] = _trailState;
            _trackingHandle = new EchoTrackingJob
            {
                Taps = _frameTaps,
                Result = _jobResult,
                Previous = _trailState,
                CurrentTime = currentTime,
                TapCount = tapCount,
                SilenceTimeoutSeconds = SilenceTimeoutSeconds
            }.Schedule();
            _trackingScheduled = 1;
            _lastRefreshFrame = frame;
        }

        private static int DrainEchoTapQueue(float currentTime)
        {
            int count = 0;
            while (count < MaxEchoTapsPerFrame && _echoTapQueue.TryDequeue(out EchoTap tap))
            {
                if (tap.LastHeardTime <= 0f || !math.isfinite(tap.LastHeardTime))
                    tap.LastHeardTime = currentTime;

                _frameTaps[count++] = tap;
            }

            while (_echoTapQueue.Count > MaxEchoTapsPerFrame)
            {
                if (!_echoTapQueue.TryDequeue(out _))
                    break;
            }

            return count;
        }

        private static int AppendMovementSignals(int count, int frame, float currentTime)
        {
            ReadOnlySpan<MovementAcousticSignal> signals = SignalBus<MovementAcousticSignal>.GetFrameSnapshot();
            int limit = math.min(signals.Length, MaxEchoTapsPerFrame - count);
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
                tap.Flags = (byte)(FlagMovementBreadcrumb | FlagLowTierDirectNode);
                tap.QualityTier = _cachedQualityTier;
                _frameTaps[count++] = tap;
                if (count >= MaxEchoTapsPerFrame)
                    break;
            }

            return count;
        }

        private static int AppendAcousticPingSignals(int count, int frame, float currentTime)
        {
            ReadOnlySpan<AcousticPingSignal> signals = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            int limit = math.min(signals.Length, MaxEchoTapsPerFrame - count);
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
                tap.Flags = (byte)(FlagPingBreadcrumb | FlagLowTierDirectNode);
                if ((signal.Flags & AcousticPingSignal.FlagActiveSonar) != 0 ||
                    signal.Channel == AcousticPingSignal.ChannelActiveSonar)
                {
                    tap.Flags |= FlagNoisemakerCandidate;
                }

                tap.QualityTier = _cachedQualityTier;
                _frameTaps[count++] = tap;
                if (count >= MaxEchoTapsPerFrame)
                    break;
            }

            return count;
        }

        private static float ResolveHeadSweep01(
            in AbsoluteUniversePosition predatorAup,
            in AcousticEchoTrailState state,
            float currentTime)
        {
            if (state.QualityTier == ScalabilityTierProfiles.LowMx350 ||
                state.Intensity01 <= 0.01f ||
                !IsFiniteAup(in predatorAup))
            {
                return 0f;
            }

            double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in state.InvestigateAup, in predatorAup);
            double distanceSq = math.csum(delta * delta);
            float distance01 = math.saturate(1f - (float)(distanceSq * math.rcp(1600.0)));
            return math.sin(currentTime * 4.65f) * math.saturate(state.Intensity01 * (0.45f + distance01));
        }

        private static uint NextSequence(int frame)
        {
            _sequence++;
            if (_sequence == 0u)
                _sequence = 1u;

            return ((uint)math.max(0, frame) << 16) ^ _sequence;
        }

        private static byte ResolveQualityTier()
        {
            return ScalabilityTierProfiles.Normalize(GlobalRegistry.ScalabilityTierProfileByte);
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

        private static void WriteBlackBox(int frame, in AcousticEchoTrailState state, float silenceSeconds)
        {
            if (!_blackBox.IsCreated || _blackBox.Length == 0)
                return;

            int index = _blackBoxCursor % _blackBox.Length;
            _blackBox[index] = new AcousticEchoBlackBoxEntry
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
            _blackBoxCursor = (index + 1) % _blackBox.Length;
        }

        private static void WriteFaultBlackBox(int frame, in AbsoluteUniversePosition faultAup)
        {
            AcousticEchoTrailState state = _trailState;
            state.InvestigateAup = faultAup;
            state.Flags = FlagSilenceLost;
            WriteBlackBox(frame, in state, 0f);
            DumpBlackBox();
        }

        private static void DumpBlackBox()
        {
            if (!_blackBox.IsCreated)
                return;

            try
            {
                string dumpPath = Path.GetFullPath(DumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(_blackBox.Length);
                    writer.Write(_blackBoxCursor);
                    for (int i = 0; i < _blackBox.Length; i++)
                    {
                        AcousticEchoBlackBoxEntry entry = _blackBox[i];
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

        private static void DisposeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }
    }
}
