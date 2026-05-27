namespace Hecton8.Tools
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Memory;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.World;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Data-oriented WFC sealed-door cut state. Gameplay truth is one float per WFC cell; visuals read globals.
    /// </summary>
    internal static class WfcLaserCutRuntime
    {
        private static int s_x001WfcLaserCutRuntimeSignalPushDropCount;
        private const int BlackBoxFrameCount = 300;
        private const uint BlackBoxDumpMagic = 0x5746434Cu; // WFCL
        private const uint BlackBoxDumpVersion = 1u;
        private const int BlackBoxDumpHeaderBytes = 32;
        private const int WfcTelemetryEntrySizeBytes = 96;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_225.bin";
        private const uint SourceHash = 0x544C5352u; // TLSR
        private const uint SparkSpeciesHash = 0x4C53504Bu; // LSPK
        private const byte TelemetryFlagCompleted = 1 << 0;
        private const byte TelemetryFlagAlreadyUnlocked = 1 << 1;
        private const byte TelemetryFlagStressReduced = 1 << 2;
        private const float StressSparkDropThreshold = 0.7f;
        private const float StressSparkScale = 0.35f;
        private const float BaseClipRadiusMeters = 0.055f;
        private const float MaxClipRadiusMeters = 0.42f;
        private const float HapticPulseSeconds = 0.045f;

        private static readonly int _CutSphereWsId = Shader.PropertyToID("_WfcLaserCutSphereWS");
        private static readonly int _CutProgressId = Shader.PropertyToID("_WfcLaserCutProgress01");
        private static readonly int _CutHeatId = Shader.PropertyToID("_WfcLaserCutHeat01");
        private static readonly int _CutMoltenId = Shader.PropertyToID("_WfcLaserCutMolten01");
        private static readonly int _CutOverkillId = Shader.PropertyToID("_WfcLaserCutOverkill01");

        private static IDataVault _dataVault;
        private static VaultGenerationHandle<float> _cutProgressHandle;
        private static VaultGenerationHandle<WfcLaserCutTelemetryEntry> _blackBoxHandle;
        private static ulong _activeSectorHash;
        private static uint _activeGridHandle;
        private static uint _activeGenerationSequence;
        private static ushort _activeCellCount;
        private static uint _blackBoxCursor;
        private static uint _doorsCutCount;
        private static float _latestSystemStress01;
        private static Vector4 _pendingCutSphereWs;
        private static float _pendingCutProgress01;
        private static float _pendingCutHeat01;
        private static float _pendingCutMolten01;
        private static float _pendingCutOverkill01;
        private static bool _cutShaderGlobalsDirty;

        public static void RefreshOwnerPhaseContext()
        {
            if (!ReadBoundBuffers(
                    out NativeArray<float> cutProgress01,
                    out _))
            {
                return;
            }

            RefreshActiveGridFromSignals(cutProgress01);
            RefreshSystemStressFromSignals();
        }

        public static bool EnsureInitialized(IDataVault vault)
        {
            if (vault == null)
            {
                ReleaseVaultHandles(_dataVault);
                ClearVaultHandles();
                _dataVault = null;
                return false;
            }

            if (!ReferenceEquals(_dataVault, vault))
            {
                ReleaseVaultHandles(_dataVault);
                ClearVaultHandles();
                _dataVault = vault;
            }

            return BindOrAcquireVaultBuffer(
                       vault,
                       BufferID.WfcDoorCutProgress01,
                       WfcOutpostPersistenceConstants.CellCount,
                       ref _cutProgressHandle,
                       out NativeArray<float> cutProgress01) &&
                   BindOrAcquireVaultBuffer(
                       vault,
                       BufferID.WfcLaserCutBlackBox,
                       BlackBoxFrameCount,
                       ref _blackBoxHandle,
                       out NativeArray<WfcLaserCutTelemetryEntry> blackBox) &&
                   cutProgress01.IsCreated &&
                   cutProgress01.Length >= WfcOutpostPersistenceConstants.CellCount &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxFrameCount;
        }

        public static bool TryApplyDoorCut(
            ulong sectorHash,
            ushort cellIndex,
            byte currentFlags,
            uint toolHash,
            double3 cutOriginAup,
            double3 hitAup,
            Vector3 runtimeHitPoint,
            float progressDelta01,
            float cutterPower01,
            float heat01,
            out float progress01,
            out bool completed,
            out uint frame)
        {
            progress01 = 0f;
            completed = false;
            frame = 0u;

            if (!ReadBoundBuffers(
                    out NativeArray<float> cutProgress01,
                    out NativeArray<WfcLaserCutTelemetryEntry> blackBox) ||
                !IsFinite(cutOriginAup) ||
                !IsFinite(hitAup) ||
                !IsFinite(runtimeHitPoint))
            {
                return false;
            }

            if (!IsKnownSealedDoorCell(sectorHash, cellIndex))
                return false;

            frame = ResolveCurrentFrameId();
            float safePower = Clamp01Finite(cutterPower01);
            float safeHeat = Clamp01Finite(heat01);
            float safeDelta = ClampFiniteNonNegative(progressDelta01);
            bool alreadyUnlocked = (currentFlags & (byte)WfcOutpostCellStateFlags.DoorUnlocked) != 0;
            int progressIndex = cellIndex;
            float previousProgress = alreadyUnlocked ? 1f : Clamp01Finite(cutProgress01[progressIndex]);
            progress01 = alreadyUnlocked ? 1f : Clamp01Finite(previousProgress + safeDelta);
            cutProgress01[progressIndex] = progress01;
            completed = progress01 >= 1f;

            float systemStress01 = Clamp01Finite(_latestSystemStress01);
            byte telemetryFlags = 0;
            if (completed)
                telemetryFlags |= TelemetryFlagCompleted;
            if (alreadyUnlocked)
                telemetryFlags |= TelemetryFlagAlreadyUnlocked;
            if (systemStress01 > StressSparkDropThreshold)
                telemetryFlags |= TelemetryFlagStressReduced;

            RecordTelemetry(
                cutOriginAup,
                hitAup,
                sectorHash,
                cellIndex,
                progress01,
                safeDelta,
                safePower,
                safeHeat,
                systemStress01,
                frame,
                toolHash,
                telemetryFlags,
                blackBox);

            QueueShaderClipGlobals(runtimeHitPoint, progress01, safeHeat, systemStress01);
            PublishReactiveFeedback(hitAup, toolHash, sectorHash, cellIndex, progress01, safePower, safeHeat, systemStress01, frame);

            if (completed && !alreadyUnlocked)
                _doorsCutCount++;

            return true;
        }

        private static bool ReadBoundBuffers(
            out NativeArray<float> cutProgress01,
            out NativeArray<WfcLaserCutTelemetryEntry> blackBox)
        {
            cutProgress01 = default;
            blackBox = default;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!ReadBoundVaultBuffer(
                    vault,
                    WfcOutpostPersistenceConstants.CellCount,
                    ref _cutProgressHandle,
                    out cutProgress01))
            {
                return false;
            }

            if (!ReadBoundVaultBuffer(
                    vault,
                    BlackBoxFrameCount,
                    ref _blackBoxHandle,
                    out blackBox))
            {
                return false;
            }

            return cutProgress01.IsCreated &&
                   cutProgress01.Length >= WfcOutpostPersistenceConstants.CellCount &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxFrameCount;
        }

        private static bool ReadBoundVaultBuffer<T>(
            IDataVault vault,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsVaultHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool BindOrAcquireVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null)
                return false;

            if (IsVaultHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (IsVaultHandleCreated(in handle))
            {
                vault.ReleaseBuffer(in handle);
                handle = default;
            }

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayTools,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                return false;
            }

            handle = acquired;
            return true;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ClearVaultHandles()
        {
            _cutProgressHandle = default;
            _blackBoxHandle = default;
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _cutProgressHandle);
            ReleaseVaultHandle(vault, ref _blackBoxHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!IsVaultHandleCreated(in handle))
                return;

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private static void RefreshActiveGridFromSignals(NativeArray<float> cutProgress01)
        {
            ReadOnlySpan<WfcOutpostGeneratedSignal> signals = SignalBus<WfcOutpostGeneratedSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return;

            WfcOutpostGeneratedSignal latest = default;
            bool hasLatest = false;
            for (int i = 0; i < signals.Length; i++)
            {
                WfcOutpostGeneratedSignal signal = signals[i];
                if (signal.SectorHash == 0UL || signal.GridHandle == 0u)
                    continue;

                if (!hasLatest || signal.Frame >= latest.Frame)
                {
                    latest = signal;
                    hasLatest = true;
                }
            }

            if (!hasLatest ||
                (_activeSectorHash == latest.SectorHash &&
                 _activeGridHandle == latest.GridHandle &&
                 _activeGenerationSequence == latest.GenerationSequence))
            {
                return;
            }

            ClearProgress(cutProgress01);
            _activeSectorHash = latest.SectorHash;
            _activeGridHandle = latest.GridHandle;
            _activeGenerationSequence = latest.GenerationSequence;
            _activeCellCount = (ushort)math.min((int)latest.CellCount, WfcOutpostPersistenceConstants.CellCount);
        }

        private static void ClearProgress(NativeArray<float> cutProgress01)
        {
            if (!cutProgress01.IsCreated || cutProgress01.Length <= 0)
                return;

            int count = math.min(cutProgress01.Length, WfcOutpostPersistenceConstants.CellCount);
            for (int i = 0; i < count; i++)
                cutProgress01[i] = 0f;
        }

        private static bool IsKnownSealedDoorCell(ulong sectorHash, ushort cellIndex)
        {
            if (cellIndex >= WfcOutpostPersistenceConstants.CellCount)
                return false;

            if (_activeGridHandle == 0u || _activeSectorHash != sectorHash)
                return true;

            return _activeCellCount == 0 || cellIndex < _activeCellCount;
        }

        private static void RefreshSystemStressFromSignals()
        {
            ReadOnlySpan<SystemHealthIndexSignal> signals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return;

            float stress01 = 0f;
            for (int i = 0; i < signals.Length; i++)
            {
                SystemHealthIndexSignal signal = signals[i];
                float pressure = Clamp01Finite(signal.Pressure01);
                float healthStress = 1f - Clamp01Finite(signal.Health01);
                float stateStress = signal.State == SystemHealthIndexSignal.StateCritical ? 1f :
                    signal.State == SystemHealthIndexSignal.StateWarning ? 0.72f : 0f;
                stress01 = math.max(stress01, math.max(pressure, math.max(healthStress, stateStress)));
            }

            _latestSystemStress01 = Clamp01Finite(stress01);
        }

        public static void FlushVisualSync()
        {
            if (!_cutShaderGlobalsDirty)
                return;

            _cutShaderGlobalsDirty = false;
            Shader.SetGlobalVector(_CutSphereWsId, _pendingCutSphereWs);
            Shader.SetGlobalFloat(_CutProgressId, _pendingCutProgress01);
            Shader.SetGlobalFloat(_CutHeatId, _pendingCutHeat01);
            Shader.SetGlobalFloat(_CutMoltenId, _pendingCutMolten01);
            Shader.SetGlobalFloat(_CutOverkillId, _pendingCutOverkill01);
        }

        private static void QueueShaderClipGlobals(Vector3 runtimeHitPoint, float progress01, float heat01, float systemStress01)
        {
            if (!IsFinite(runtimeHitPoint))
                return;

            float safeProgress01 = Clamp01Finite(progress01);
            float safeHeat01 = Clamp01Finite(heat01);
            float radius = math.lerp(BaseClipRadiusMeters, MaxClipRadiusMeters, safeProgress01);
            _pendingCutSphereWs = new Vector4(runtimeHitPoint.x, runtimeHitPoint.y, runtimeHitPoint.z, radius);
            _pendingCutProgress01 = safeProgress01;
            _pendingCutHeat01 = safeHeat01;
            _pendingCutMolten01 = Clamp01Finite(safeHeat01 * (0.35f + safeProgress01));
            _pendingCutOverkill01 = ResolveVisualOverkill01(systemStress01);
            _cutShaderGlobalsDirty = true;
        }

        private static float ResolveVisualOverkill01(float systemStress01)
        {
            float quality01 = Clamp01Finite(SignalBusRegistry.GlobalQualityWeight01);
            float stressHeadroom01 = math.saturate((StressSparkDropThreshold - Clamp01Finite(systemStress01)) * math.rcp(StressSparkDropThreshold));
            float overkillCurve = SmoothStep01((quality01 - 0.35f) * math.rcp(0.65f));
            return Clamp01Finite(overkillCurve * stressHeadroom01);
        }

        private static void PublishReactiveFeedback(
            double3 hitAup,
            uint toolHash,
            ulong sectorHash,
            ushort cellIndex,
            float progress01,
            float cutterPower01,
            float heat01,
            float systemStress01,
            uint frame)
        {
            if (!IsFinite(hitAup))
                return;

            uint targetHash = ComposeDoorTargetHash(sectorHash, cellIndex);
            float safeProgress01 = Clamp01Finite(progress01);
            float safePower01 = Clamp01Finite(cutterPower01);
            float safeHeat01 = Clamp01Finite(heat01);
            float safeStress01 = Clamp01Finite(systemStress01);
            float stressScale = safeStress01 > StressSparkDropThreshold ? StressSparkScale : 1f;
            float sparkIntensity = Clamp01Finite((0.28f + safePower01 * 0.72f) * stressScale);
            ushort sparkQuantity = (ushort)math.clamp((int)math.round(6f * stressScale), 1, 16);

            DebrisSpawnSignal debris = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(hitAup),
                SpeciesHash = SparkSpeciesHash,
                SourceEntityId = toolHash != 0u ? toolHash : SourceHash,
                Intensity01 = sparkIntensity,
                DebrisKind = DebrisSpawnSignal.DebrisKindSparks,
                Flags = DebrisSpawnSignal.FlagToolSparks,
                Quantity = sparkQuantity
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in debris, ref s_x001WfcLaserCutRuntimeSignalPushDropCount);

            ToolAcousticSignal acoustic = new ToolAcousticSignal
            {
                ToolHash = toolHash != 0u ? toolHash : SourceHash,
                TargetHash = targetHash,
                Progress01 = safeProgress01,
                PitchScale = math.lerp(0.92f, 1.32f, safeHeat01),
                Intensity01 = safePower01,
                Frame = frame,
                State = ToolAcousticSignal.StateLaserLoop,
                Flags = ToolAcousticSignal.FlagLooping
            };
            SignalBus<ToolAcousticSignal>.TryPushTracked(in acoustic, ref s_x001WfcLaserCutRuntimeSignalPushDropCount);

            HapticRequest haptic = new HapticRequest
            {
                Intensity01 = Clamp01Finite(safePower01 * (0.35f + safeHeat01 * 0.65f)),
                DurationSeconds = HapticPulseSeconds,
                Frequency01 = Clamp01Finite(0.62f + safeHeat01 * 0.38f),
                SourceHash = toolHash != 0u ? toolHash : SourceHash,
                Frame = frame,
                Channel = HapticRequest.ChannelMicroVibration,
                Flags = HapticRequest.FlagMicroVibration
            };
            SignalBus<HapticRequest>.TryPushTracked(in haptic, ref s_x001WfcLaserCutRuntimeSignalPushDropCount);
        }

        private static uint ComposeDoorTargetHash(ulong sectorHash, ushort cellIndex)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)sectorHash) * 16777619u;
            hash = (hash ^ (uint)(sectorHash >> 32)) * 16777619u;
            hash = (hash ^ cellIndex) * 16777619u;
            return hash != 0u ? hash : 1u;
        }

        private static void RecordTelemetry(
            double3 cutOriginAup,
            double3 hitAup,
            ulong sectorHash,
            ushort cellIndex,
            float progress01,
            float progressDelta01,
            float cutterPower01,
            float heat01,
            float systemStress01,
            uint frame,
            uint toolHash,
            byte flags,
            NativeArray<WfcLaserCutTelemetryEntry> blackBox)
        {
            if (!blackBox.IsCreated || blackBox.Length <= 0)
                return;

            bool valid = IsFinite(cutOriginAup) &&
                         IsFinite(hitAup) &&
                         math.isfinite(progress01) &&
                         math.isfinite(progressDelta01) &&
                         math.isfinite(cutterPower01) &&
                         math.isfinite(heat01) &&
                         math.isfinite(systemStress01);

            int index = (int)(_blackBoxCursor % (uint)blackBox.Length);
            blackBox[index] = new WfcLaserCutTelemetryEntry
            {
                CutOriginAup = cutOriginAup,
                HitAup = hitAup,
                SectorHash = sectorHash,
                Frame = frame,
                ToolHash = toolHash,
                Progress01 = Clamp01Finite(progress01),
                ProgressDelta01 = ClampFiniteNonNegative(progressDelta01),
                CutterPower01 = Clamp01Finite(cutterPower01),
                Heat01 = Clamp01Finite(heat01),
                SystemStress01 = Clamp01Finite(systemStress01),
                DoorsCutCount = _doorsCutCount,
                CellIndex = cellIndex,
                Flags = flags
            };
            _blackBoxCursor++;

            if (!valid)
                DumpBlackBox(blackBox);
        }

        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static float Clamp01Finite(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ClampFiniteNonNegative(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static uint ResolveCurrentFrameId()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame != 0u ? frame : 1u;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static unsafe void DumpBlackBox(NativeArray<WfcLaserCutTelemetryEntry> blackBox)
        {
            if (!blackBox.IsCreated || blackBox.Length <= 0)
                return;

            try
            {
                int entrySize = UnsafeUtility.SizeOf<WfcLaserCutTelemetryEntry>();
                if (entrySize != WfcTelemetryEntrySizeBytes)
                    return;

                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(root, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                    return;

                Directory.CreateDirectory(directory);

                int entryCount = math.min(blackBox.Length, BlackBoxFrameCount);
                if (entryCount <= 0)
                    return;

                int cursor = (int)(_blackBoxCursor % (uint)entryCount);
                int payloadBytes = entryCount * entrySize;
                Span<byte> header = stackalloc byte[BlackBoxDumpHeaderBytes];
                WriteUIntLittleEndian(header.Slice(0, 4), BlackBoxDumpMagic);
                WriteUIntLittleEndian(header.Slice(4, 4), BlackBoxDumpVersion);
                WriteUIntLittleEndian(header.Slice(8, 4), ResolveCurrentFrameId());
                WriteUIntLittleEndian(header.Slice(12, 4), (uint)entryCount);
                WriteUIntLittleEndian(header.Slice(16, 4), (uint)entrySize);
                WriteUIntLittleEndian(header.Slice(20, 4), (uint)cursor);
                WriteUIntLittleEndian(header.Slice(24, 4), _doorsCutCount);
                WriteUIntLittleEndian(header.Slice(28, 4), (uint)payloadBytes);

                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(header);
                    WriteTelemetryBlock(stream, source, cursor, entryCount - cursor, entrySize);
                    WriteTelemetryBlock(stream, source, 0, cursor, entrySize);
                    stream.Flush(true);
                }
            }
            catch (Exception exception)
            {
                _ = exception;
                GlobalTelemetryBus.PublishUnityLogFault(SourceHash, 0u, 1u);
            }
        }

        private static unsafe void WriteTelemetryBlock(FileStream stream, byte* source, int start, int count, int entrySize)
        {
            if (count <= 0)
                return;

            stream.Write(new ReadOnlySpan<byte>(source + start * entrySize, count * entrySize));
        }

        private static void WriteUIntLittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal struct WfcLaserCutTelemetryEntry
    {
        [FieldOffset(0)]
        public double3 CutOriginAup;

        [FieldOffset(24)]
        public double3 HitAup;

        [FieldOffset(48)]
        public ulong SectorHash;

        [FieldOffset(56)]
        public uint Frame;

        [FieldOffset(60)]
        public uint ToolHash;

        [FieldOffset(64)]
        public float Progress01;

        [FieldOffset(68)]
        public float ProgressDelta01;

        [FieldOffset(72)]
        public float CutterPower01;

        [FieldOffset(76)]
        public float Heat01;

        [FieldOffset(80)]
        public float SystemStress01;

        [FieldOffset(84)]
        public uint DoorsCutCount;

        [FieldOffset(88)]
        public ushort CellIndex;

        [FieldOffset(90)]
        public byte Flags;

        [FieldOffset(91)]
        public byte Reserved;

        [FieldOffset(92)]
        public uint ReservedPadding;
    }
}
