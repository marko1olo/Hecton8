using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Hash-addressed subtitle cue signal. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SubtitleCueSignal : ISignal
    {
        [FieldOffset(0)] public uint TokenHash;
        [FieldOffset(4)] public uint StartAudioFrame;
        [FieldOffset(8)] public ushort DurationMilliseconds;
        [FieldOffset(10)] public byte Priority;
        [FieldOffset(11)] public byte Flags;
        [FieldOffset(12)] public uint SourceHash;
    }
}

namespace Hecton8.UI
{
    /// <summary>
    /// Vault-backed subtitle cue synchronized by audio sample frames.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SubtitleCueDTO
    {
        [FieldOffset(0)] public uint TokenHash;
        [FieldOffset(4)] public float DisplayDuration;
        [FieldOffset(8)] public uint StartAudioFrame;
        [FieldOffset(12)] public float CurrentProgress;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public byte _pad0;
        [FieldOffset(21)] public byte _pad1;
        [FieldOffset(22)] public byte _pad2;
        [FieldOffset(23)] public byte _pad3;
        [FieldOffset(24)] public byte _pad4;
        [FieldOffset(25)] public byte _pad5;
        [FieldOffset(26)] public byte _pad6;
        [FieldOffset(27)] public byte _pad7;
        [FieldOffset(28)] public byte _pad8;
        [FieldOffset(29)] public byte _pad9;
        [FieldOffset(30)] public byte _pad10;
        [FieldOffset(31)] public byte _pad11;
    }

    /// <summary>
    /// Fixed-size subtitle/localization telemetry frame. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LocalizationTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint AudioFrameClock;
        [FieldOffset(8)] public uint ActiveSubtitleCount;
        [FieldOffset(12)] public uint DecodedCharacterCount;
        [FieldOffset(16)] public float Utf8DecodeMilliseconds;
        [FieldOffset(20)] public uint MissingTokenHashCount;
        [FieldOffset(24)] public uint LastTokenHash;
        [FieldOffset(28)] public uint CueSignalCount;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint DroppedCueCount;
        [FieldOffset(44)] public uint LayoutAuditHash;
        [FieldOffset(48)] public uint BufferIdCueState;
        [FieldOffset(52)] public uint BufferIdTelemetry;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    public static unsafe class BabelSubtitleSyncRuntime
    {
        public const int MaxSubtitleCueCount = 64;
        public const int TelemetryFrameCapacity = 300;
        public const uint FlagActive = 1u << 0;
        public const uint FlagVisible = 1u << 1;
        public const uint FlagExpired = 1u << 2;
        public const uint FlagPresented = 1u << 3;
        public const uint FlagInterrupt = 1u << 4;
        public const uint FlagVisualOnlyNoRollback = 1u << 5;
        public const uint FlagDirectionLeft = 1u << 8;
        public const uint FlagDirectionRight = 1u << 9;
        public const uint FlagDirectionBehind = 1u << 10;
        public const uint FlagFault = 1u << 31;

        private const uint SystemHash = 0xBA150150u;
        private const uint SubtitleCueLaneHash = 0x53554331u; // SUC1
        private const int DefaultSampleRate = 48000;
        private const float SlowDecodeDumpThresholdMs = 0.5f;
        private const BufferID SubtitleCueStateBufferId = (BufferID)15070550;
        private const BufferID SubtitleCueTelemetryBufferId = (BufferID)15070551;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_BABEL_SURGEON.bin";
        private const string DumpAgentRelativePath = "Docs/AgentLogs/Dump_SHINOBU_150.bin";

        private static readonly DispatcherBridge s_dispatcherBridge = new DispatcherBridge();
        private static IDataVault s_vault;
        private static VaultBufferHandle<SubtitleCueDTO> s_cueHandle;
        private static VaultBufferHandle<LocalizationTelemetryEntry> s_telemetryHandle;
        private static JobHandle s_pendingCueEvaluationHandle;
        private static int s_telemetryCursor;
        private static int s_nextCueSlot;
        private static int s_activeCueCount;
        private static int s_cueSignalCountThisFrame;
        private static int s_droppedCueCount;
        private static int s_decodedCharactersThisFrame;
        private static int s_missingTokenHashesThisFrame;
        private static uint s_lastTokenHash;
        private static uint s_audioFrameClock;
        private static int s_sampleRate = DefaultSampleRate;
        private static int s_lastPreparedFrame = -1;
        private static int s_editorAudioFrameOffset;
        private static bool s_initialized;
        private static bool s_dispatcherRegistered;
        private static bool s_layoutValid;
        private static bool s_pendingCueEvaluationActive;

        public static uint CurrentAudioFrame => s_audioFrameClock;
        public static int CurrentSampleRate => math.max(1, s_sampleRate);
        public static int ActiveCueCount => s_activeCueCount;
        public static int EditorAudioFrameOffset => s_editorAudioFrameOffset;
        public static bool RollbackStateExcluded => true;
        public static bool LayoutValid => s_layoutValid || ValidateSubtitleCueLayout();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_vault = null;
            s_cueHandle = default;
            s_telemetryHandle = default;
            s_pendingCueEvaluationHandle = default;
            s_telemetryCursor = 0;
            s_nextCueSlot = 0;
            s_activeCueCount = 0;
            s_cueSignalCountThisFrame = 0;
            s_droppedCueCount = 0;
            s_decodedCharactersThisFrame = 0;
            s_missingTokenHashesThisFrame = 0;
            s_lastTokenHash = 0u;
            s_audioFrameClock = 0u;
            s_sampleRate = DefaultSampleRate;
            s_lastPreparedFrame = -1;
            s_editorAudioFrameOffset = 0;
            s_initialized = false;
            s_dispatcherRegistered = false;
            s_layoutValid = false;
            s_pendingCueEvaluationActive = false;
        }

        public static bool EnsureInitialized()
        {
            s_layoutValid = ValidateSubtitleCueLayout();
            if (!s_layoutValid)
                return false;

            SignalBus<SubtitleCueSignal>.Configure(32, maxFrameSignals: 64, lowTierFrameSignals: 8, laneHash: SubtitleCueLaneHash);
            SignalBus<SubtitleCueSignal>.EnsureInitialized();

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (s_initialized &&
                ReferenceEquals(s_vault, vault) &&
                TryResolveCueBuffer(out _) &&
                TryResolveTelemetryBuffer(out _))
            {
                TryRegisterDispatcher();
                return true;
            }

            s_vault = vault;
            s_cueHandle = vault.GetBufferHandle<SubtitleCueDTO>(
                SubtitleCueStateBufferId,
                MaxSubtitleCueCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            s_telemetryHandle = vault.GetBufferHandle<LocalizationTelemetryEntry>(
                SubtitleCueTelemetryBufferId,
                TelemetryFrameCapacity,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);

            if (!TryResolveCueBuffer(out NativeArray<SubtitleCueDTO> cues) ||
                !TryResolveTelemetryBuffer(out _))
            {
                s_initialized = false;
                return false;
            }

            ClearSubtitleCueFlagsJob clearJob = new ClearSubtitleCueFlagsJob
            {
                Cues = (SubtitleCueDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(cues),
                CueCount = MaxSubtitleCueCount
            };
            // COLD SYNC JOB: first-use cue sanitation must finish before runtime signal ingestion sees the buffer.
            for (int i = 0; i < MaxSubtitleCueCount; i++)
                clearJob.Execute(i);
            s_nextCueSlot = 0;
            s_activeCueCount = 0;
            s_initialized = true;
            TryRegisterDispatcher();
            return true;
        }

        public static void PreparePresentationFrame()
        {
            if (!EnsureInitialized())
                return;

            if (!TryCompletePendingCueEvaluation())
                return;
            int frame = Time.frameCount;
            if (s_lastPreparedFrame == frame)
                return;

            s_lastPreparedFrame = frame;
            ResolveAudioClock();
            DrainCueSignals();
            ScheduleCueEvaluation(default);
            WriteFrameTelemetry(0f);
            s_decodedCharactersThisFrame = 0;
            s_missingTokenHashesThisFrame = 0;
            s_cueSignalCountThisFrame = 0;
        }

        public static bool TryConsumeReadyCue(out SubtitleCueDTO cue)
        {
            cue = default;
            if (!EnsureInitialized())
                return false;

            PreparePresentationFrame();
            if (!TryCompletePendingCueEvaluation() || !TryResolveCueBuffer(out NativeArray<SubtitleCueDTO> cues))
                return false;

            for (int i = 0; i < math.min(MaxSubtitleCueCount, cues.Length); i++)
            {
                SubtitleCueDTO candidate = cues[i];
                uint required = FlagActive | FlagVisible;
                if ((candidate.Flags & required) != required ||
                    (candidate.Flags & FlagPresented) != 0u ||
                    candidate.TokenHash == 0u)
                {
                    continue;
                }

                candidate.Flags |= FlagPresented;
                cues[i] = candidate;
                cue = candidate;
                return true;
            }

            return false;
        }

        public static bool TryRegisterImmediateCue(uint tokenHash, float durationSeconds, uint flags)
        {
            if (!EnsureInitialized() || tokenHash == 0u)
                return false;

            ResolveAudioClock();
            return RegisterCue(tokenHash, s_audioFrameClock, durationSeconds, flags | FlagVisible | FlagPresented);
        }

        public static bool PublishCue(uint tokenHash, uint startAudioFrame, float durationSeconds, uint flags, byte priority = 0)
        {
            if (tokenHash == 0u)
                return false;

            SubtitleCueSignal signal = default;
            signal.TokenHash = tokenHash;
            signal.StartAudioFrame = startAudioFrame;
            signal.DurationMilliseconds = SecondsToMilliseconds(durationSeconds);
            signal.Priority = priority;
            signal.Flags = PackSignalFlags(flags);
            return SignalBus<SubtitleCueSignal>.TryPush(in signal);
        }

        public static void RecordDecode(uint tokenHash, int decodedCharacters, bool missingTokenHash, float utf8DecodeMilliseconds)
        {
            if (!EnsureInitialized())
                return;

            s_lastTokenHash = tokenHash;
            s_decodedCharactersThisFrame += math.max(0, decodedCharacters);
            if (missingTokenHash)
                s_missingTokenHashesThisFrame++;

            WriteFrameTelemetry(math.max(0f, utf8DecodeMilliseconds));
            if (utf8DecodeMilliseconds > SlowDecodeDumpThresholdMs || missingTokenHash)
                DumpTelemetry();
        }

        public static bool TryGetLatestTelemetry(out LocalizationTelemetryEntry entry)
        {
            entry = default;
            if (!EnsureInitialized() || !TryResolveTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry> telemetry))
                return false;

            int index = s_telemetryCursor - 1;
            if (index < 0)
                index = TelemetryFrameCapacity - 1;

            if ((uint)index >= (uint)telemetry.Length)
                return false;

            entry = telemetry[index];
            return true;
        }

        public static bool TryGetCue(int index, out SubtitleCueDTO cue)
        {
            cue = default;
            if (!EnsureInitialized() ||
                !TryCompletePendingCueEvaluation() ||
                !TryResolveCueBuffer(out NativeArray<SubtitleCueDTO> cues) ||
                (uint)index >= (uint)cues.Length)
            {
                return false;
            }

            cue = cues[index];
            return true;
        }

        private static bool TryResolveCueBuffer(out NativeArray<SubtitleCueDTO> cues)
        {
            cues = default;
            if (s_vault == null || !s_cueHandle.IsCreated)
                return false;

            cues = s_cueHandle.Resolve(s_vault);
            return cues.IsCreated && cues.Length >= MaxSubtitleCueCount;
        }

        private static bool TryResolveTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry> telemetry)
        {
            telemetry = default;
            if (s_vault == null || !s_telemetryHandle.IsCreated)
                return false;

            telemetry = s_telemetryHandle.Resolve(s_vault);
            return telemetry.IsCreated && telemetry.Length >= TelemetryFrameCapacity;
        }

        public static void SetEditorAudioFrameOffset(int offsetFrames)
        {
            s_editorAudioFrameOffset = offsetFrames;
            ResolveAudioClock();
        }

        public static uint ResolveDurationFrames(float durationSeconds)
        {
            int sampleRate = math.max(1, s_sampleRate);
            float safeSeconds = math.max(0.001f, math.select(0.001f, durationSeconds, math.isfinite(durationSeconds)));
            double frames = safeSeconds * sampleRate;
            if (frames >= uint.MaxValue)
                return uint.MaxValue;

            return (uint)math.max(1, (int)math.round((float)frames));
        }

        public static float ResolveElapsedSecondsSince(uint startAudioFrame)
        {
            ResolveAudioClock();
            uint elapsedFrames = unchecked(s_audioFrameClock - startAudioFrame);
            return elapsedFrames / (float)math.max(1, s_sampleRate);
        }

        public static float ResolveCurrentAudioTimeSeconds()
        {
            ResolveAudioClock();
            return s_audioFrameClock / (float)math.max(1, s_sampleRate);
        }

        public static int ResolveCanvasDirtyBudget(int pendingCount)
        {
            if (pendingCount <= 0)
                return 0;

            float quality = ResolveGlobalQualityWeight();
            float smooth = quality * quality * (3f - (2f * quality));
            float budget = math.lerp(2f, 18f, smooth);
            return math.clamp((int)math.ceil(budget), 1, math.max(1, pendingCount));
        }

        public static bool TryResolveCueArrow(uint flags, out char arrow)
        {
            if ((flags & FlagDirectionLeft) != 0u)
            {
                arrow = '\u2190';
                return true;
            }

            if ((flags & FlagDirectionRight) != 0u)
            {
                arrow = '\u2192';
                return true;
            }

            if ((flags & FlagDirectionBehind) != 0u)
            {
                arrow = '\u2193';
                return true;
            }

            arrow = '\0';
            return false;
        }

        public static bool TryResolveAupDirectionalArrow(
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition cameraAup,
            Vector3 cameraRight,
            Vector3 cameraForward,
            out char arrow)
        {
            float3 delta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in cameraAup);
            if (!math.all(math.isfinite(delta)) || math.lengthsq(delta) <= 0.0001f)
            {
                arrow = '\0';
                return false;
            }

            float3 dir = math.normalize(delta);
            float rightDot = math.dot(dir, new float3(cameraRight.x, cameraRight.y, cameraRight.z));
            float forwardDot = math.dot(dir, new float3(cameraForward.x, cameraForward.y, cameraForward.z));
            if (forwardDot < -0.45f && math.abs(forwardDot) >= math.abs(rightDot))
            {
                arrow = '\u2193';
                return true;
            }

            arrow = rightDot < 0f ? '\u2190' : '\u2192';
            return true;
        }

        public static bool ValidateSubtitleCueLayout()
        {
            return UnsafeUtility.SizeOf<SubtitleCueDTO>() == 32 &&
                   OffsetOf<SubtitleCueDTO>(nameof(SubtitleCueDTO.TokenHash)) == 0 &&
                   OffsetOf<SubtitleCueDTO>(nameof(SubtitleCueDTO.DisplayDuration)) == 4 &&
                   OffsetOf<SubtitleCueDTO>(nameof(SubtitleCueDTO.StartAudioFrame)) == 8 &&
                   OffsetOf<SubtitleCueDTO>(nameof(SubtitleCueDTO.CurrentProgress)) == 12 &&
                   OffsetOf<SubtitleCueDTO>(nameof(SubtitleCueDTO.Flags)) == 16 &&
                   OffsetOf<SubtitleCueDTO>(nameof(SubtitleCueDTO._pad0)) == 20 &&
                   OffsetOf<SubtitleCueDTO>(nameof(SubtitleCueDTO._pad11)) == 31 &&
                   UnsafeUtility.SizeOf<SubtitleCueSignal>() == 16 &&
                   UnsafeUtility.SizeOf<LocalizationTelemetryEntry>() == 64;
        }

        private static bool RegisterCue(uint tokenHash, uint startAudioFrame, float durationSeconds, uint flags)
        {
            if (!TryCompletePendingCueEvaluation() || !TryResolveCueBuffer(out NativeArray<SubtitleCueDTO> cues))
                return false;

            int slot = FindCueSlot(cues);
            if ((uint)slot >= (uint)cues.Length)
            {
                s_droppedCueCount++;
                return false;
            }

            SubtitleCueDTO cue = default;
            cue.TokenHash = tokenHash;
            cue.DisplayDuration = math.max(0.05f, math.select(0.05f, durationSeconds, math.isfinite(durationSeconds)));
            cue.StartAudioFrame = startAudioFrame;
            cue.CurrentProgress = 0f;
            cue.Flags = flags | FlagActive | FlagVisualOnlyNoRollback;
            cues[slot] = cue;
            return true;
        }

        private static int FindCueSlot(NativeArray<SubtitleCueDTO> cues)
        {
            int count = math.min(MaxSubtitleCueCount, cues.Length);
            for (int i = 0; i < count; i++)
            {
                int slot = s_nextCueSlot + i;
                if (slot >= count)
                    slot -= count;

                SubtitleCueDTO cue = cues[slot];
                if ((cue.Flags & FlagActive) == 0u)
                {
                    s_nextCueSlot = slot + 1;
                    if (s_nextCueSlot >= count)
                        s_nextCueSlot = 0;
                    return slot;
                }
            }

            int overwriteSlot = s_nextCueSlot;
            s_nextCueSlot++;
            if (s_nextCueSlot >= count)
                s_nextCueSlot = 0;
            s_droppedCueCount++;
            return overwriteSlot;
        }

        private static void DrainCueSignals()
        {
            ReadOnlySpan<SubtitleCueSignal> signals = SignalBus<SubtitleCueSignal>.GetFrameSnapshot();
            int count = math.min(signals.Length, MaxSubtitleCueCount);
            for (int i = 0; i < count; i++)
            {
                SubtitleCueSignal signal = signals[i];
                if (signal.TokenHash == 0u)
                    continue;

                uint flags = TranslateSignalFlags(signal.Flags);
                if (signal.Priority >= 200)
                    flags |= FlagInterrupt;

                float duration = signal.DurationMilliseconds > 0
                    ? signal.DurationMilliseconds * 0.001f
                    : 3.25f;
                RegisterCue(signal.TokenHash, signal.StartAudioFrame, duration, flags);
                s_cueSignalCountThisFrame++;
            }
        }

        private static uint TranslateSignalFlags(byte flags)
        {
            uint result = 0u;
            if ((flags & 1) != 0)
                result |= FlagInterrupt;
            if ((flags & 2) != 0)
                result |= FlagDirectionLeft;
            if ((flags & 4) != 0)
                result |= FlagDirectionRight;
            if ((flags & 8) != 0)
                result |= FlagDirectionBehind;
            return result;
        }

        private static byte PackSignalFlags(uint flags)
        {
            byte packed = 0;
            if ((flags & FlagInterrupt) != 0u)
                packed |= 1;
            if ((flags & FlagDirectionLeft) != 0u)
                packed |= 2;
            if ((flags & FlagDirectionRight) != 0u)
                packed |= 4;
            if ((flags & FlagDirectionBehind) != 0u)
                packed |= 8;
            return packed;
        }

        private static JobHandle ScheduleCueEvaluation(JobHandle dependsOn)
        {
            if (s_pendingCueEvaluationActive)
                return JobHandle.CombineDependencies(dependsOn, s_pendingCueEvaluationHandle);

            if (!TryResolveCueBuffer(out NativeArray<SubtitleCueDTO> cues))
            {
                return dependsOn;
            }

            int count = math.min(MaxSubtitleCueCount, cues.Length);
            if (count <= 0)
                return dependsOn;

            EvaluateSubtitleCuesJob job = new EvaluateSubtitleCuesJob
            {
                Cues = (SubtitleCueDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(cues),
                CueCount = count,
                AudioFrameClock = s_audioFrameClock,
                SampleRate = (uint)math.max(1, s_sampleRate)
            };
            s_pendingCueEvaluationHandle = job.Schedule(count, 32, dependsOn);
            s_pendingCueEvaluationActive = true;
            return s_pendingCueEvaluationHandle;
        }

        private static bool TryCompletePendingCueEvaluation()
        {
            if (!s_pendingCueEvaluationActive)
                return true;

            if (!s_pendingCueEvaluationHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref s_pendingCueEvaluationHandle))
                return false;

            s_pendingCueEvaluationActive = false;
            RefreshActiveCueCount();
            return true;
        }

        private static void RefreshActiveCueCount()
        {
            if (!TryResolveCueBuffer(out NativeArray<SubtitleCueDTO> cues))
            {
                s_activeCueCount = 0;
                return;
            }

            int active = 0;
            int count = math.min(MaxSubtitleCueCount, cues.Length);
            for (int i = 0; i < count; i++)
            {
                if ((cues[i].Flags & FlagActive) != 0u)
                    active++;
            }

            s_activeCueCount = active;
        }

        private static void ResolveAudioClock()
        {
            int rate = AudioSettings.outputSampleRate;
            s_sampleRate = rate > 0 ? rate : DefaultSampleRate;
            double dsp = AudioSettings.dspTime;
            if (double.IsNaN(dsp) || double.IsInfinity(dsp) || dsp < 0.0)
                dsp = 0.0;

            double rawFrame = (dsp * s_sampleRate) + s_editorAudioFrameOffset;
            if (rawFrame <= 0.0)
            {
                s_audioFrameClock = 0u;
                return;
            }

            if (rawFrame >= uint.MaxValue)
            {
                s_audioFrameClock = uint.MaxValue;
                return;
            }

            s_audioFrameClock = (uint)rawFrame;
        }

        private static ushort SecondsToMilliseconds(float seconds)
        {
            float safe = math.max(0.001f, math.select(0.001f, seconds, math.isfinite(seconds)));
            return (ushort)math.clamp((int)math.round(safe * 1000f), 1, ushort.MaxValue);
        }

        private static void WriteFrameTelemetry(float decodeMilliseconds)
        {
            if (!TryResolveTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry> telemetry) || telemetry.Length <= 0)
                return;

            int slot = s_telemetryCursor;
            s_telemetryCursor++;
            if (s_telemetryCursor >= TelemetryFrameCapacity)
                s_telemetryCursor = 0;

            telemetry[slot] = new LocalizationTelemetryEntry
            {
                Frame = (uint)math.max(0, Time.frameCount),
                AudioFrameClock = s_audioFrameClock,
                ActiveSubtitleCount = (uint)math.max(0, s_activeCueCount),
                DecodedCharacterCount = (uint)math.max(0, s_decodedCharactersThisFrame),
                Utf8DecodeMilliseconds = decodeMilliseconds,
                MissingTokenHashCount = (uint)math.max(0, s_missingTokenHashesThisFrame),
                LastTokenHash = s_lastTokenHash,
                CueSignalCount = (uint)math.max(0, s_cueSignalCountThisFrame),
                GlobalQualityWeight = ResolveGlobalQualityWeight(),
                Flags = s_layoutValid ? 0u : FlagFault,
                DroppedCueCount = (uint)math.max(0, s_droppedCueCount),
                LayoutAuditHash = 0x15015032u,
                BufferIdCueState = (uint)SubtitleCueStateBufferId,
                BufferIdTelemetry = (uint)SubtitleCueTelemetryBufferId
            };
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight))
                return math.saturate(weight);

            return math.saturate(SignalBusRegistry.GlobalQualityWeight01);
        }

        private static void DumpTelemetry()
        {
            if (!TryResolveTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry> telemetry))
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
                string agentDumpPath = Path.Combine(projectRoot, DumpAgentRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));
                int byteCount = UnsafeUtility.SizeOf<LocalizationTelemetryEntry>() * telemetry.Length;
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                WriteDump(dumpPath, source, byteCount);
                WriteDump(agentDumpPath, source, byteCount);
            }
            catch (Exception)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
        }

        private static void WriteDump(string path, byte* source, int byteCount)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(new ReadOnlySpan<byte>(source, byteCount));
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        private static void TryRegisterDispatcher()
        {
            if (s_dispatcherRegistered || !Application.isPlaying)
                return;

            s_dispatcherRegistered = GlobalRegistry.TryRegisterDispatcherSystem(s_dispatcherBridge);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct EvaluateSubtitleCuesJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Cues points to a GlobalDataVault-owned SubtitleCueDTO buffer requested by SubtitleCueStateBufferId.
            // The job receives CueCount bounded by the allocated NativeArray length and never touches memory outside
            // [0, CueCount). Unity safety cannot express the ref-in-place requirement from the batch prompt.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Copying each DTO into a temporary value and writing back was rejected because the prompt explicitly
            // requires direct in-memory CurrentProgress updates through UnsafeUtility.AsRef. The DTO is explicit,
            // blittable, and 32 bytes, so the pointer stride is deterministic on ARM64.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Each parallel index owns exactly one cue slot. There is no cross-index aliasing, and the only mutable
            // fields are CurrentProgress and Flags inside that index's DTO.
            [NoAlias, NativeDisableUnsafePtrRestriction] public SubtitleCueDTO* Cues;
            public int CueCount;
            public uint AudioFrameClock;
            public uint SampleRate;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)CueCount || Cues == null)
                    return;

                ref SubtitleCueDTO cue = ref UnsafeUtility.AsRef<SubtitleCueDTO>(Cues + index);
                uint flags = cue.Flags;
                if ((flags & FlagActive) == 0u)
                    return;

                int elapsedFrames = unchecked((int)(AudioFrameClock - cue.StartAudioFrame));
                if (elapsedFrames < 0)
                {
                    cue.CurrentProgress = 0f;
                    cue.Flags = (flags & ~FlagVisible) | FlagActive | FlagVisualOnlyNoRollback;
                    return;
                }

                uint durationFrames = (uint)math.max(1, (int)math.round(math.max(0.001f, cue.DisplayDuration) * math.max(1u, SampleRate)));
                float progress = math.saturate(elapsedFrames / (float)durationFrames);
                cue.CurrentProgress = progress;
                if ((uint)elapsedFrames >= durationFrames)
                {
                    cue.Flags = (flags & ~(FlagActive | FlagVisible)) | FlagExpired | FlagVisualOnlyNoRollback;
                    return;
                }

                cue.Flags = flags | FlagActive | FlagVisible | FlagVisualOnlyNoRollback;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearSubtitleCueFlagsJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public SubtitleCueDTO* Cues;
            public int CueCount;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)CueCount || Cues == null)
                    return;

                ref SubtitleCueDTO cue = ref UnsafeUtility.AsRef<SubtitleCueDTO>(Cues + index);
                cue.Flags = 0u;
                cue.CurrentProgress = 0f;
            }
        }

        private sealed class DispatcherBridge : IDispatcherSystem
        {
            public uint GetSystemIdHash() => SystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PreSimulation;
            public byte GetBucketId() => 0;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
                PreparePresentationFrame();
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return ScheduleCueEvaluation(dependsOn);
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                TryCompletePendingCueEvaluation();
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                TryCompletePendingCueEvaluation();
            }
        }
    }
}
