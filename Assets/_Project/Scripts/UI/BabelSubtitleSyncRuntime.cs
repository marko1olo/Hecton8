using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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
        [FieldOffset(20)] public uint SourceHash;
        [FieldOffset(24)] private byte _pad0;
        [FieldOffset(25)] private byte _pad1;
        [FieldOffset(26)] private byte _pad2;
        [FieldOffset(27)] private byte _pad3;
        [FieldOffset(28)] private byte _pad4;
        [FieldOffset(29)] private byte _pad5;
        [FieldOffset(30)] private byte _pad6;
        [FieldOffset(31)] private byte _pad7;
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
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    public enum UIOptimizationFailureCode : uint
    {
        None = 0u,
        MissingLocalizationHash = 1u,
        TextBufferOverflow = 2u,
        FormatterOverflow = 3u,
        InvalidTextState = 4u
    }

    /// <summary>
    /// Fixed-size UI text failure telemetry frame. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct UIOptimizationTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint AudioFrameClock;
        [FieldOffset(8)] public uint TokenHash;
        [FieldOffset(12)] public UIOptimizationFailureCode FailureCode;
        [FieldOffset(16)] public int RequestedCharacters;
        [FieldOffset(20)] public int RenderedCharacters;
        [FieldOffset(24)] public int BufferCapacity;
        [FieldOffset(28)] public uint CueSignalCount;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint BufferIdTelemetry;
        [FieldOffset(44)] public uint WriteSequence;
        [FieldOffset(48)] public uint LastTokenHash;
        [FieldOffset(52)] public uint MissingTokenHashCount;
        [FieldOffset(56)] public uint DroppedCueCount;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    public static unsafe class BabelSubtitleSyncRuntime
    {

        private static int s_x001DirectSignalPushDropCount_BabelSubtitleSyncRuntime;

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
        private const uint SubtitleCueLaneHash = SubtitleCueSignal.LaneHash; // SUC1
        private const uint SubtitleCueDropWarningHash = 0x53544452u; // STDR.
        private const uint SubtitleCueDropContextHash = 0x53544344u; // STCD.
        private const uint SubtitleCueAcquireContextHash = 0x53544151u; // STAQ.
        private const uint SubtitleCueRegisterContextHash = 0x53545247u; // STRG.
        private const uint SubtitleCueOverwriteContextHash = 0x53544F57u; // STOW.
        private const uint SubtitleCueSignalOverflowContextHash = 0x53544F56u; // STOV.
        private const uint SubtitleCuePublishSignalDropContextHash = 0x53545044u; // STPD.
        private const int DefaultSampleRate = 48000;
        private const float SlowDecodeDumpThresholdMs = 0.5f;
        private const BufferID SubtitleCueStateBufferId = BufferID.BabelSubtitleSyncRuntime_SubtitleCueStateBufferId;
        private const BufferID SubtitleCueTelemetryBufferId = BufferID.BabelSubtitleSyncRuntime_SubtitleCueTelemetryBufferId;
        private const BufferID UIOptimizationTelemetryBufferId = BufferID.BabelSubtitleSyncRuntime_UIOptimizationTelemetryBufferId;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1335_BabelSubtitleSync.bin";
        private const string UIOptimizationDumpRelativePath = "Docs/AgentLogs/Dump_1423.bin";
        private static readonly ulong CueStateMutationGuardMask = SubtitleMutationGuardBit(SubtitleCueStateBufferId);
        private static readonly ulong TelemetryMutationGuardMask = SubtitleMutationGuardBit(SubtitleCueTelemetryBufferId);
        private static readonly ulong UIOptimizationTelemetryMutationGuardMask = SubtitleMutationGuardBit(UIOptimizationTelemetryBufferId);

        private static readonly DispatcherBridge s_dispatcherBridge = new DispatcherBridge();
        private static IDataVault s_vault;
        private static IDataVault s_activeMutationGuardVault;
        private static BufferID s_activeMutationBufferId;
        private static ulong s_activeMutationGuardMask;
        private static VaultGenerationHandle<SubtitleCueDTO> s_cueHandle;
        private static VaultGenerationHandle<LocalizationTelemetryEntry> s_telemetryHandle;
        private static VaultGenerationHandle<UIOptimizationTelemetryEntry> s_uiOptimizationTelemetryHandle;
        private static int s_telemetryCursor;
        private static int s_uiOptimizationTelemetryCursor;
        private static int s_nextCueSlot;
        private static int s_activeCueCount;
        private static int s_cueSignalCountThisFrame;
        private static int s_droppedCueCount;
        private static int s_lastCueDropTelemetryFrame = -1;
        private static int s_decodedCharactersThisFrame;
        private static int s_missingTokenHashesThisFrame;
        private static uint s_lastTokenHash;
        private static uint s_audioFrameClock;
        private static int s_sampleRate = DefaultSampleRate;
        private static uint s_lastPreparedFrame;
        private static uint s_fallbackPresentationFrame;
        private static uint s_uiOptimizationWriteSequence;
        private static int s_editorAudioFrameOffset;
        private static bool s_initialized;
        private static bool s_signalBusInitialized;
        private static bool s_dispatcherRegistered;
        private static bool s_layoutValidationAttempted;
        private static bool s_layoutValid;

        public static uint CurrentAudioFrame => s_audioFrameClock;
        public static uint CurrentPresentationFrame => s_lastPreparedFrame;
        public static int CurrentSampleRate => math.max(1, s_sampleRate);
        public static int ActiveCueCount => s_activeCueCount;
        public static int DroppedCueCount => Volatile.Read(ref s_droppedCueCount);
        public static int SignalPushDropCount => Volatile.Read(ref s_x001DirectSignalPushDropCount_BabelSubtitleSyncRuntime);
        public static int EditorAudioFrameOffset => s_editorAudioFrameOffset;
        public static bool RollbackStateExcluded => true;
        public static bool LayoutValid => EnsureSubtitleLayoutValid();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseAllSubtitleMutationBuffers();
            ReleaseSubtitleBuffers(s_vault);
            s_vault = null;
            s_activeMutationGuardVault = null;
            s_activeMutationBufferId = default;
            s_activeMutationGuardMask = 0ul;
            s_cueHandle = default;
            s_telemetryHandle = default;
            s_uiOptimizationTelemetryHandle = default;
            s_telemetryCursor = 0;
            s_uiOptimizationTelemetryCursor = 0;
            ResetCueRuntimeStateForVaultRebind();
            s_audioFrameClock = 0u;
            s_sampleRate = DefaultSampleRate;
            s_editorAudioFrameOffset = 0;
            s_initialized = false;
            s_signalBusInitialized = false;
            s_dispatcherRegistered = false;
            s_layoutValidationAttempted = false;
            s_layoutValid = false;
        }

        public static void BindDataVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(s_vault, vault))
                return;

            ReleaseAllSubtitleMutationBuffers();
            ReleaseSubtitleBuffers(s_vault);
            s_activeMutationGuardVault = null;
            s_activeMutationBufferId = default;
            s_activeMutationGuardMask = 0ul;
            s_vault = vault;
            ResetCueRuntimeStateForVaultRebind();
            s_initialized = false;
        }

        private static void ResetCueRuntimeStateForVaultRebind()
        {
            s_nextCueSlot = 0;
            s_activeCueCount = 0;
            s_cueSignalCountThisFrame = 0;
            Volatile.Write(ref s_x001DirectSignalPushDropCount_BabelSubtitleSyncRuntime, 0);
            Volatile.Write(ref s_droppedCueCount, 0);
            Volatile.Write(ref s_lastCueDropTelemetryFrame, -1);
            s_decodedCharactersThisFrame = 0;
            s_missingTokenHashesThisFrame = 0;
            s_lastTokenHash = 0u;
            s_lastPreparedFrame = 0u;
            s_fallbackPresentationFrame = 0u;
            s_uiOptimizationWriteSequence = 0u;
        }

        public static bool EnsureInitialized()
        {
            if (!EnsureSubtitleLayoutValid())
                return false;

            EnsureSignalBusInitializedCold();

            IDataVault vault = s_vault;
            if (vault == null)
                return false;

            if (s_initialized &&
                ReferenceEquals(s_vault, vault) &&
                TryReadOnlyCueBuffer(out _) &&
                TryReadOnlyTelemetryBuffer(out _) &&
                TryReadOnlyUIOptimizationTelemetryBuffer(out _))
            {
                TryRegisterDispatcher();
                return true;
            }

            s_vault = vault;
            s_cueHandle = vault.EnsureGenerationHandle<SubtitleCueDTO>(
                SubtitleCueStateBufferId,
                MaxSubtitleCueCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            s_telemetryHandle = vault.EnsureGenerationHandle<LocalizationTelemetryEntry>(
                SubtitleCueTelemetryBufferId,
                TelemetryFrameCapacity,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);
            s_uiOptimizationTelemetryHandle = vault.EnsureGenerationHandle<UIOptimizationTelemetryEntry>(
                UIOptimizationTelemetryBufferId,
                TelemetryFrameCapacity,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);

            if (!TryReadOnlyCueBuffer(out _) ||
                !TryReadOnlyTelemetryBuffer(out _) ||
                !TryReadOnlyUIOptimizationTelemetryBuffer(out _))
            {
                s_initialized = false;
                return false;
            }

            if (!TryAcquireCueMutationBuffer(out NativeArray<SubtitleCueDTO> cues))
            {
                s_initialized = false;
                return false;
            }

            try
            {
                ClearSubtitleCueFlagsPhase clearPhase = default;
                clearPhase.Cues = (SubtitleCueDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(cues);
                clearPhase.CueCount = MaxSubtitleCueCount;
                // Cold first-use sanitation must finish before runtime signal ingestion sees the buffer.
                for (int i = 0; i < MaxSubtitleCueCount; i++)
                    clearPhase.Execute(i);
            }
            finally
            {
                ReleaseCueMutationBuffer();
            }

            s_nextCueSlot = 0;
            s_activeCueCount = 0;
            s_initialized = true;
            TryRegisterDispatcher();
            return true;
        }

        private static void EnsureSignalBusInitializedCold()
        {
            if (s_signalBusInitialized)
                return;

            SignalBus<SubtitleCueSignal>.Configure(
                SubtitleCueSignal.ExpectedCapacity,
                maxFrameSignals: SubtitleCueSignal.MaxFrameSignals,
                lowTierFrameSignals: SubtitleCueSignal.LowTierFrameSignals,
                laneHash: SubtitleCueLaneHash);
            SignalBus<SubtitleCueSignal>.EnsureInitialized();
            s_signalBusInitialized = true;
        }

        public static void PreparePresentationFrame()
        {
            if (!EnsureInitialized())
                return;

            if (!TryCompletePendingCueEvaluation())
                return;
            ResolveAudioClock();
            uint frame = ResolvePresentationFrameId();
            if (s_lastPreparedFrame == frame)
                return;

            s_lastPreparedFrame = frame;
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
            if (!TryCompletePendingCueEvaluation() || !TryAcquireCueMutationBuffer(out NativeArray<SubtitleCueDTO> cues))
                return false;

            try
            {
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
            finally
            {
                ReleaseCueMutationBuffer();
            }
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

            EnsureSignalBusInitializedCold();

            SubtitleCueSignal signal = default;
            signal.TokenHash = tokenHash;
            signal.StartAudioFrame = startAudioFrame;
            signal.DurationMilliseconds = SecondsToMilliseconds(durationSeconds);
            signal.Priority = priority;
            signal.Flags = PackSignalFlags(flags);
            signal.SourceHash = SystemHash;
            if (SignalBus<SubtitleCueSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_BabelSubtitleSyncRuntime))
                return true;

            RecordCueDrop(tokenHash, SystemHash, SubtitleCuePublishSignalDropContextHash);
            return false;
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
            if (!s_initialized ||
                s_vault == null ||
                !TryReadOnlyTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry>.ReadOnly telemetry))
            {
                return false;
            }

            int index = s_telemetryCursor - 1;
            if (index < 0)
                index = TelemetryFrameCapacity - 1;

            if ((uint)index >= (uint)telemetry.Length)
                return false;

            entry = telemetry[index];
            return true;
        }

        public static bool TryGetLatestUIOptimizationTelemetry(out UIOptimizationTelemetryEntry entry)
        {
            entry = default;
            if (!s_initialized ||
                s_vault == null ||
                !TryReadOnlyUIOptimizationTelemetryBuffer(out NativeArray<UIOptimizationTelemetryEntry>.ReadOnly telemetry))
            {
                return false;
            }

            int index = s_uiOptimizationTelemetryCursor - 1;
            if (index < 0)
                index = TelemetryFrameCapacity - 1;

            if ((uint)index >= (uint)telemetry.Length)
                return false;

            entry = telemetry[index];
            return true;
        }

        public static void RecordUIOptimizationFailure(
            uint tokenHash,
            UIOptimizationFailureCode failureCode,
            int requestedCharacters,
            int renderedCharacters,
            int bufferCapacity,
            uint flags = 0u)
        {
            if (failureCode == UIOptimizationFailureCode.None || !s_initialized || s_vault == null)
                return;

            if (!TryAcquireUIOptimizationTelemetryMutationBuffer(out NativeArray<UIOptimizationTelemetryEntry> telemetry))
                return;

            try
            {
                int slot = s_uiOptimizationTelemetryCursor;
                s_uiOptimizationTelemetryCursor++;
                if (s_uiOptimizationTelemetryCursor >= TelemetryFrameCapacity)
                    s_uiOptimizationTelemetryCursor = 0;

                UIOptimizationTelemetryEntry entry = default;
                entry.Frame = s_lastPreparedFrame != 0u ? s_lastPreparedFrame : ResolvePresentationFrameId();
                entry.AudioFrameClock = s_audioFrameClock;
                entry.TokenHash = tokenHash;
                entry.FailureCode = failureCode;
                entry.RequestedCharacters = math.max(0, requestedCharacters);
                entry.RenderedCharacters = math.max(0, renderedCharacters);
                entry.BufferCapacity = math.max(0, bufferCapacity);
                entry.CueSignalCount = (uint)math.max(0, s_cueSignalCountThisFrame);
                entry.GlobalQualityWeight = ResolveGlobalQualityWeight();
                entry.Flags = flags;
                entry.BufferIdTelemetry = (uint)UIOptimizationTelemetryBufferId;
                entry.WriteSequence = ++s_uiOptimizationWriteSequence;
                entry.LastTokenHash = s_lastTokenHash;
                entry.MissingTokenHashCount = (uint)math.max(0, s_missingTokenHashesThisFrame);
                entry.DroppedCueCount = (uint)math.max(0, Volatile.Read(ref s_droppedCueCount));
                telemetry[slot] = entry;
            }
            finally
            {
                ReleaseUIOptimizationTelemetryMutationBuffer();
            }

            if (failureCode == UIOptimizationFailureCode.InvalidTextState)
                DumpUIOptimizationTelemetry();
        }

        public static void DumpUIOptimizationTelemetry()
        {
            if (!TryReadOnlyUIOptimizationTelemetryBuffer(out NativeArray<UIOptimizationTelemetryEntry>.ReadOnly telemetry))
                return;

            int entryCount = telemetry.Length;
            int entrySize = UnsafeUtility.SizeOf<UIOptimizationTelemetryEntry>();
            telemetry = default;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, UIOptimizationDumpRelativePath);
                WriteUIOptimizationDump(dumpPath, entryCount, entrySize);
            }
            catch (IOException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (UnauthorizedAccessException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (ObjectDisposedException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (InvalidOperationException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (ArgumentException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (NotSupportedException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
        }

        public static bool TryGetCue(int index, out SubtitleCueDTO cue)
        {
            cue = default;
            if (!s_initialized ||
                s_vault == null ||
                !TryReadOnlyCueBuffer(out NativeArray<SubtitleCueDTO>.ReadOnly cues) ||
                (uint)index >= (uint)cues.Length)
            {
                return false;
            }

            cue = cues[index];
            return true;
        }

        private static bool TryReadOnlyCueBuffer(out NativeArray<SubtitleCueDTO>.ReadOnly cues)
        {
            cues = default;
            if (s_vault == null ||
                s_vault.IsCompactionFenceActive ||
                !IsSubtitleVaultHandle(in s_cueHandle, SubtitleCueStateBufferId))
                return false;

            if (!s_vault.TryReadOnlyHandle(in s_cueHandle, out cues) ||
                s_vault.IsCompactionFenceActive ||
                cues.Length < MaxSubtitleCueCount)
            {
                cues = default;
                return false;
            }

            return true;
        }

        private static bool TryReadOnlyTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            if (s_vault == null ||
                s_vault.IsCompactionFenceActive ||
                !IsSubtitleVaultHandle(in s_telemetryHandle, SubtitleCueTelemetryBufferId))
                return false;

            if (!s_vault.TryReadOnlyHandle(in s_telemetryHandle, out telemetry) ||
                s_vault.IsCompactionFenceActive ||
                telemetry.Length < TelemetryFrameCapacity)
            {
                telemetry = default;
                return false;
            }

            return true;
        }

        private static bool TryReadOnlyUIOptimizationTelemetryBuffer(out NativeArray<UIOptimizationTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            if (s_vault == null ||
                s_vault.IsCompactionFenceActive ||
                !IsSubtitleVaultHandle(in s_uiOptimizationTelemetryHandle, UIOptimizationTelemetryBufferId))
                return false;

            if (!s_vault.TryReadOnlyHandle(in s_uiOptimizationTelemetryHandle, out telemetry) ||
                s_vault.IsCompactionFenceActive ||
                telemetry.Length < TelemetryFrameCapacity)
            {
                telemetry = default;
                return false;
            }

            return true;
        }

        private static bool TryAcquireCueMutationBuffer(out NativeArray<SubtitleCueDTO> cues)
        {
            return TryAcquireSubtitleMutationBuffer(
                in s_cueHandle,
                SubtitleCueStateBufferId,
                MaxSubtitleCueCount,
                CueStateMutationGuardMask,
                out cues);
        }

        private static bool TryAcquireTelemetryMutationBuffer(out NativeArray<LocalizationTelemetryEntry> telemetry)
        {
            return TryAcquireSubtitleMutationBuffer(
                in s_telemetryHandle,
                SubtitleCueTelemetryBufferId,
                TelemetryFrameCapacity,
                TelemetryMutationGuardMask,
                out telemetry);
        }

        private static bool TryAcquireUIOptimizationTelemetryMutationBuffer(out NativeArray<UIOptimizationTelemetryEntry> telemetry)
        {
            return TryAcquireSubtitleMutationBuffer(
                in s_uiOptimizationTelemetryHandle,
                UIOptimizationTelemetryBufferId,
                TelemetryFrameCapacity,
                UIOptimizationTelemetryMutationGuardMask,
                out telemetry);
        }

        private static bool TryAcquireSubtitleMutationBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            ulong mutationGuardMask,
            out NativeArray<T> buffer) where T : unmanaged
        {
            buffer = default;
            IDataVault vault = s_vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsSubtitleVaultHandle(in handle, bufferId) ||
                mutationGuardMask == 0ul ||
                s_activeMutationGuardMask != 0ul ||
                !vault.TryAcquireMutationGuard(mutationGuardMask))
            {
                return false;
            }

            bool releaseOnExit = true;
            bool writeLockHeld = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.UI, out buffer))
                {
                    buffer = default;
                    return false;
                }

                writeLockHeld = true;
                if (vault.IsCompactionFenceActive ||
                    !buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    buffer = default;
                    return false;
                }

                StoreSubtitleMutationVault(mutationGuardMask, bufferId, vault);
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                {
                    if (writeLockHeld)
                        vault.ReleaseWriteLock(in handle, SystemID.UI);

                    vault.ReleaseMutationGuard(mutationGuardMask);
                }
            }
        }

        private static void ReleaseCueMutationBuffer()
        {
            ReleaseSubtitleMutationBuffer(CueStateMutationGuardMask);
        }

        private static void ReleaseTelemetryMutationBuffer()
        {
            ReleaseSubtitleMutationBuffer(TelemetryMutationGuardMask);
        }

        private static void ReleaseUIOptimizationTelemetryMutationBuffer()
        {
            ReleaseSubtitleMutationBuffer(UIOptimizationTelemetryMutationGuardMask);
        }

        private static void ReleaseSubtitleMutationBuffer(ulong mutationGuardMask)
        {
            IDataVault vault = TakeSubtitleMutationVault(mutationGuardMask, out BufferID bufferId);
            if (vault != null && mutationGuardMask != 0ul)
            {
                ReleaseSubtitleWriteLock(vault, bufferId);
                vault.ReleaseMutationGuard(mutationGuardMask);
            }
        }

        private static void ReleaseAllSubtitleMutationBuffers()
        {
            ulong mutationGuardMask = s_activeMutationGuardMask;
            IDataVault vault = TakeSubtitleMutationVault(mutationGuardMask, out BufferID bufferId);
            if (vault != null && mutationGuardMask != 0ul)
            {
                ReleaseSubtitleWriteLock(vault, bufferId);
                vault.ReleaseMutationGuard(mutationGuardMask);
            }
        }

        private static void StoreSubtitleMutationVault(ulong mutationGuardMask, BufferID bufferId, IDataVault vault)
        {
            s_activeMutationGuardVault = vault;
            s_activeMutationBufferId = bufferId;
            s_activeMutationGuardMask = mutationGuardMask;
        }

        private static IDataVault TakeSubtitleMutationVault(ulong mutationGuardMask, out BufferID bufferId)
        {
            bufferId = default;
            if (mutationGuardMask == 0ul || mutationGuardMask != s_activeMutationGuardMask)
                return null;

            IDataVault vault = s_activeMutationGuardVault;
            bufferId = s_activeMutationBufferId;
            s_activeMutationGuardVault = null;
            s_activeMutationBufferId = default;
            s_activeMutationGuardMask = 0ul;
            return vault;
        }

        private static void ReleaseSubtitleWriteLock(IDataVault vault, BufferID bufferId)
        {
            if (vault == null)
                return;

            if (bufferId == SubtitleCueStateBufferId && IsSubtitleVaultHandle(in s_cueHandle, SubtitleCueStateBufferId))
            {
                vault.ReleaseWriteLock(in s_cueHandle, SystemID.UI);
                return;
            }

            if (bufferId == SubtitleCueTelemetryBufferId && IsSubtitleVaultHandle(in s_telemetryHandle, SubtitleCueTelemetryBufferId))
            {
                vault.ReleaseWriteLock(in s_telemetryHandle, SystemID.UI);
                return;
            }

            if (bufferId == UIOptimizationTelemetryBufferId &&
                IsSubtitleVaultHandle(in s_uiOptimizationTelemetryHandle, UIOptimizationTelemetryBufferId))
            {
                vault.ReleaseWriteLock(in s_uiOptimizationTelemetryHandle, SystemID.UI);
            }
        }

        private static bool IsSubtitleVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u;
        }

        private static ulong SubtitleMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static void ReleaseSubtitleBuffers(IDataVault vault)
        {
            ReleaseVaultBuffer(vault, ref s_cueHandle, SubtitleCueStateBufferId);
            ReleaseVaultBuffer(vault, ref s_telemetryHandle, SubtitleCueTelemetryBufferId);
            ReleaseVaultBuffer(vault, ref s_uiOptimizationTelemetryHandle, UIOptimizationTelemetryBufferId);
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : unmanaged
        {
            if (vault != null && IsSubtitleVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
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
            uint elapsedFrames = unchecked(s_audioFrameClock - startAudioFrame);
            return elapsedFrames / (float)math.max(1, s_sampleRate);
        }

        public static float ResolveCurrentAudioTimeSeconds()
        {
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
            float3 delta = AupPrecisionMath.LocalDeltaFloat3Clamped(
                sourceAup.ToAbsoluteDouble3(),
                cameraAup.ToAbsoluteDouble3(),
                AupPrecisionMath.DefaultMaxLocalCastMeters,
                float3.zero);
            if (!math.all(math.isfinite(delta)) || math.lengthsq(delta) <= 0.0001f)
            {
                arrow = '\0';
                return false;
            }

            float3 dir = math.normalize(delta);
            float rightDot = math.dot(dir, math.float3(cameraRight.x, cameraRight.y, cameraRight.z));
            float forwardDot = math.dot(dir, math.float3(cameraForward.x, cameraForward.y, cameraForward.z));
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
                   OffsetOf<SubtitleCueDTO>(nameof(SubtitleCueDTO.SourceHash)) == 20 &&
                   OffsetOf<SubtitleCueDTO>("_pad0") == 24 &&
                   OffsetOf<SubtitleCueDTO>("_pad7") == 31 &&
                   UnsafeUtility.SizeOf<SubtitleCueSignal>() == 64 &&
                   OffsetOf<SubtitleCueSignal>(nameof(SubtitleCueSignal.TokenHash)) == 0 &&
                   OffsetOf<SubtitleCueSignal>(nameof(SubtitleCueSignal.SourceHash)) == 4 &&
                   OffsetOf<SubtitleCueSignal>(nameof(SubtitleCueSignal.StartAudioFrame)) == 8 &&
                   OffsetOf<SubtitleCueSignal>(nameof(SubtitleCueSignal.AudioFrameLatency)) == 12 &&
                   OffsetOf<SubtitleCueSignal>(nameof(SubtitleCueSignal.DurationMilliseconds)) == 16 &&
                   OffsetOf<SubtitleCueSignal>(nameof(SubtitleCueSignal.Priority)) == 18 &&
                   OffsetOf<SubtitleCueSignal>(nameof(SubtitleCueSignal.Flags)) == 19 &&
                   OffsetOf<SubtitleCueSignal>("_pad0") == 20 &&
                   OffsetOf<SubtitleCueSignal>("_pad43") == 63 &&
                   UnsafeUtility.SizeOf<LocalizationTelemetryEntry>() == 64 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.Frame)) == 0 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.AudioFrameClock)) == 4 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.ActiveSubtitleCount)) == 8 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.DecodedCharacterCount)) == 12 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.Utf8DecodeMilliseconds)) == 16 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.MissingTokenHashCount)) == 20 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.LastTokenHash)) == 24 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.CueSignalCount)) == 28 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.GlobalQualityWeight)) == 32 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.Flags)) == 36 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.DroppedCueCount)) == 40 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.LayoutAuditHash)) == 44 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.BufferIdCueState)) == 48 &&
                   OffsetOf<LocalizationTelemetryEntry>(nameof(LocalizationTelemetryEntry.BufferIdTelemetry)) == 52 &&
                   OffsetOf<LocalizationTelemetryEntry>("_pad0") == 56 &&
                   OffsetOf<LocalizationTelemetryEntry>("_pad7") == 63 &&
                   UnsafeUtility.SizeOf<UIOptimizationTelemetryEntry>() == 64 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.Frame)) == 0 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.AudioFrameClock)) == 4 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.TokenHash)) == 8 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.FailureCode)) == 12 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.RequestedCharacters)) == 16 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.RenderedCharacters)) == 20 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.BufferCapacity)) == 24 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.CueSignalCount)) == 28 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.GlobalQualityWeight)) == 32 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.Flags)) == 36 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.BufferIdTelemetry)) == 40 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.WriteSequence)) == 44 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.LastTokenHash)) == 48 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.MissingTokenHashCount)) == 52 &&
                   OffsetOf<UIOptimizationTelemetryEntry>(nameof(UIOptimizationTelemetryEntry.DroppedCueCount)) == 56 &&
                   OffsetOf<UIOptimizationTelemetryEntry>("_pad0") == 60 &&
                   OffsetOf<UIOptimizationTelemetryEntry>("_pad3") == 63;
        }

        private static bool EnsureSubtitleLayoutValid()
        {
            if (s_layoutValidationAttempted)
                return s_layoutValid;

            s_layoutValid = ValidateSubtitleCueLayout();
            s_layoutValidationAttempted = true;
            return s_layoutValid;
        }

        private static bool RegisterCue(uint tokenHash, uint startAudioFrame, float durationSeconds, uint flags, uint sourceHash = 0u)
        {
            if (!TryCompletePendingCueEvaluation() || !TryAcquireCueMutationBuffer(out NativeArray<SubtitleCueDTO> cues))
            {
                RecordCueDrop(tokenHash, sourceHash, SubtitleCueAcquireContextHash);
                return false;
            }

            try
            {
                int slot = FindCueSlot(cues, tokenHash, sourceHash);
                if ((uint)slot >= (uint)cues.Length)
                {
                    RecordCueDrop(tokenHash, sourceHash, SubtitleCueRegisterContextHash);
                    return false;
                }

                SubtitleCueDTO cue = default;
                cue.TokenHash = tokenHash;
                cue.DisplayDuration = math.max(0.05f, math.select(0.05f, durationSeconds, math.isfinite(durationSeconds)));
                cue.StartAudioFrame = startAudioFrame;
                cue.CurrentProgress = 0f;
                cue.Flags = flags | FlagActive | FlagVisualOnlyNoRollback;
                cue.SourceHash = sourceHash;
                cues[slot] = cue;
                return true;
            }
            finally
            {
                ReleaseCueMutationBuffer();
            }
        }

        private static int FindCueSlot(NativeArray<SubtitleCueDTO> cues, uint tokenHash, uint sourceHash)
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
            RecordCueDrop(tokenHash, sourceHash, SubtitleCueOverwriteContextHash);
            return overwriteSlot;
        }

        private static void DrainCueSignals()
        {
            ReadOnlySpan<SubtitleCueSignal> signals = SignalBus<SubtitleCueSignal>.GetFrameSnapshot();
            int count = math.min(signals.Length, MaxSubtitleCueCount);
            if (signals.Length > count)
                RecordCueDrop(0u, 0u, SubtitleCueSignalOverflowContextHash, signals.Length - count);

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
                uint startAudioFrame = signal.StartAudioFrame != 0u ? signal.StartAudioFrame : s_audioFrameClock;
                RegisterCue(signal.TokenHash, startAudioFrame, duration, flags, signal.SourceHash);
                s_cueSignalCountThisFrame++;
            }
        }

        private static void RecordCueDrop(uint tokenHash, uint sourceHash, uint contextHash)
        {
            RecordCueDrop(tokenHash, sourceHash, contextHash, 1);
        }

        private static void RecordCueDrop(uint tokenHash, uint sourceHash, uint contextHash, int droppedCount)
        {
            if (droppedCount <= 0)
                return;

            int droppedTotal = AddDroppedCueCountSaturated(droppedCount);

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (Interlocked.Exchange(ref s_lastCueDropTelemetryFrame, frame) == frame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                SubtitleCueDropWarningHash,
                SubtitleCueDropContextHash ^ contextHash ^ tokenHash ^ sourceHash,
                math.max(1, droppedTotal));
        }

        private static int AddDroppedCueCountSaturated(int droppedCount)
        {
            while (true)
            {
                int current = Volatile.Read(ref s_droppedCueCount);
                int updated = current > int.MaxValue - droppedCount
                    ? int.MaxValue
                    : current + droppedCount;
                if (Interlocked.CompareExchange(ref s_droppedCueCount, updated, current) == current)
                    return updated;
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
            if (!TryAcquireCueMutationBuffer(out NativeArray<SubtitleCueDTO> cues))
            {
                s_activeCueCount = 0;
                return dependsOn;
            }

            try
            {
                int count = math.min(MaxSubtitleCueCount, cues.Length);
                if (count <= 0)
                    return dependsOn;

                EvaluateSubtitleCuesPhase phase = default;
                phase.Cues = (SubtitleCueDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(cues);
                phase.CueCount = count;
                phase.AudioFrameClock = s_audioFrameClock;
                phase.SampleRate = (uint)math.max(1, s_sampleRate);
                int active = 0;
                for (int i = 0; i < count; i++)
                {
                    phase.Execute(i);
                    if ((cues[i].Flags & FlagActive) != 0u)
                        active++;
                }

                s_activeCueCount = active;
                return dependsOn;
            }
            finally
            {
                ReleaseCueMutationBuffer();
            }
        }

        private static bool TryCompletePendingCueEvaluation()
        {
            return true;
        }

        private static void RefreshActiveCueCount()
        {
            if (!TryReadOnlyCueBuffer(out NativeArray<SubtitleCueDTO>.ReadOnly cues))
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

        private static uint ResolvePresentationFrameId()
        {
            uint frame = SystemDispatcher.ReadPublishedDispatcherFrameId();
            if (frame != 0u)
                return frame;

            if (s_audioFrameClock != 0u)
                return s_audioFrameClock;

            s_fallbackPresentationFrame++;
            if (s_fallbackPresentationFrame == 0u)
                s_fallbackPresentationFrame = 1u;
            return s_fallbackPresentationFrame;
        }

        private static ushort SecondsToMilliseconds(float seconds)
        {
            float safe = math.max(0.001f, math.select(0.001f, seconds, math.isfinite(seconds)));
            return (ushort)math.clamp((int)math.round(safe * 1000f), 1, ushort.MaxValue);
        }

        private static void WriteFrameTelemetry(float decodeMilliseconds)
        {
            if (!TryAcquireTelemetryMutationBuffer(out NativeArray<LocalizationTelemetryEntry> telemetry))
                return;

            try
            {
                int slot = s_telemetryCursor;
                s_telemetryCursor++;
                if (s_telemetryCursor >= TelemetryFrameCapacity)
                    s_telemetryCursor = 0;

                LocalizationTelemetryEntry entry = default;
                entry.Frame = s_lastPreparedFrame;
                entry.AudioFrameClock = s_audioFrameClock;
                entry.ActiveSubtitleCount = (uint)math.max(0, s_activeCueCount);
                entry.DecodedCharacterCount = (uint)math.max(0, s_decodedCharactersThisFrame);
                entry.Utf8DecodeMilliseconds = decodeMilliseconds;
                entry.MissingTokenHashCount = (uint)math.max(0, s_missingTokenHashesThisFrame);
                entry.LastTokenHash = s_lastTokenHash;
                entry.CueSignalCount = (uint)math.max(0, s_cueSignalCountThisFrame);
                entry.GlobalQualityWeight = ResolveGlobalQualityWeight();
                entry.Flags = s_layoutValid ? 0u : FlagFault;
                entry.DroppedCueCount = (uint)math.max(0, Volatile.Read(ref s_droppedCueCount));
                entry.LayoutAuditHash = 0x15015032u;
                entry.BufferIdCueState = (uint)SubtitleCueStateBufferId;
                entry.BufferIdTelemetry = (uint)SubtitleCueTelemetryBufferId;
                telemetry[slot] = entry;
            }
            finally
            {
                ReleaseTelemetryMutationBuffer();
            }
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
            if (!TryReadOnlyTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry>.ReadOnly telemetry))
                return;

            int entryCount = telemetry.Length;
            int entrySize = UnsafeUtility.SizeOf<LocalizationTelemetryEntry>();
            telemetry = default;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
                WriteDump(dumpPath, entryCount, entrySize);
            }
            catch (IOException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (UnauthorizedAccessException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (ObjectDisposedException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (InvalidOperationException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (ArgumentException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
            catch (NotSupportedException)
            {
                // Crash-path telemetry must not cascade into another failure.
            }
        }

        private static void WriteDump(string path, int entryCount, int entrySize)
        {
            int count = math.min(entryCount, TelemetryFrameCapacity);
            if (count <= 0 ||
                !TryReadOnlyTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry>.ReadOnly telemetry) ||
                count > telemetry.Length)
                return;

            byte* source = (byte*)telemetry.GetUnsafeReadOnlyPtr();
            int byteCount = count * entrySize;
            NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(source, byteCount), byteCount);
        }

        private static bool TryReadTelemetryRow(int index, out LocalizationTelemetryEntry row)
        {
            row = default;
            if ((uint)index >= TelemetryFrameCapacity ||
                !TryReadOnlyTelemetryBuffer(out NativeArray<LocalizationTelemetryEntry>.ReadOnly telemetry) ||
                (uint)index >= (uint)telemetry.Length)
            {
                return false;
            }

            row = telemetry[index];
            return true;
        }

        private static void WriteUIOptimizationDump(string path, int entryCount, int entrySize)
        {
            int count = math.min(entryCount, TelemetryFrameCapacity);
            if (count <= 0 ||
                !TryReadOnlyUIOptimizationTelemetryBuffer(out NativeArray<UIOptimizationTelemetryEntry>.ReadOnly telemetry) ||
                count > telemetry.Length)
                return;

            byte* source = (byte*)telemetry.GetUnsafeReadOnlyPtr();
            int byteCount = count * entrySize;
            NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(source, byteCount), byteCount);
        }

        private static bool TryReadUIOptimizationTelemetryRow(int index, out UIOptimizationTelemetryEntry row)
        {
            row = default;
            if ((uint)index >= TelemetryFrameCapacity ||
                !TryReadOnlyUIOptimizationTelemetryBuffer(out NativeArray<UIOptimizationTelemetryEntry>.ReadOnly telemetry) ||
                (uint)index >= (uint)telemetry.Length)
            {
                return false;
            }

            row = telemetry[index];
            return true;
        }

        private static int OffsetOf<T>(string fieldName) where T : unmanaged
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        private static void TryRegisterDispatcher()
        {
            if (s_dispatcherRegistered || !Application.isPlaying)
                return;

            s_dispatcherRegistered = SystemDispatcher.Register(s_dispatcherBridge);
        }

        private ref struct EvaluateSubtitleCuesPhase
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Cues points to a GlobalDataVault-owned SubtitleCueDTO buffer requested by SubtitleCueStateBufferId.
            // The phase receives CueCount bounded by the allocated NativeArray length and never touches memory outside
            // [0, CueCount). Unity safety cannot express this stack-only pointer phase without the attribute.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Copying each DTO into a temporary value and writing back was rejected because the prompt explicitly
            // requires direct in-memory CurrentProgress updates through UnsafeUtility.AsRef. The DTO is explicit,
            // blittable, and 32 bytes, so the pointer stride is deterministic on ARM64.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Each parallel index owns exactly one cue slot. There is no cross-index aliasing, and the only mutable
            // fields are CurrentProgress and Flags inside that index's DTO.
            [NativeDisableUnsafePtrRestriction] public SubtitleCueDTO* Cues;
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

        private ref struct ClearSubtitleCueFlagsPhase
        {
            [NativeDisableUnsafePtrRestriction] public SubtitleCueDTO* Cues;
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
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.VisualSync;
            public byte GetBucketId() => 0;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                TryCompletePendingCueEvaluation();
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                PreparePresentationFrame();
                TryCompletePendingCueEvaluation();
            }
        }
    }
}
