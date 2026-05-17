namespace Hecton8.Tools
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Memory;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Gameplay;
    using Hecton8.Logistics.Grid.Contracts;
    using Hecton8.Power;
    using Hecton8.World;
    using Unity.Collections;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Data-oriented WFC sealed-door cut state. Gameplay truth is one float per WFC cell; visuals read globals.
    /// </summary>
    internal static unsafe class WfcLaserCutRuntime
    {
        private const int BlackBoxFrameCount = WfcOutpostGridConstants.TelemetryFrames;
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

        private static VaultBufferHandle<float> _cutProgressHandle;
        private static VaultBufferHandle<WfcLaserCutTelemetryEntry> _blackBoxHandle;
        private static ulong _activeSectorHash;
        private static uint _activeGridHandle;
        private static uint _activeGenerationSequence;
        private static uint _blackBoxCursor;
        private static uint _doorsCutCount;
        private static float _latestSystemStress01;

        public static uint DoorsCutCount => _doorsCutCount;

        public static bool TryApplyDoorCut(
            SealedDoor door,
            uint toolHash,
            double3 cutOriginAup,
            double3 hitAup,
            Vector3 runtimeHitPoint,
            float progressDelta01,
            float cutterPower01,
            float heat01,
            out float progress01,
            out bool completed)
        {
            progress01 = 0f;
            completed = false;

            if (door == null ||
                !door.TryGetWfcOutpostCell(out ulong sectorHash, out ushort cellIndex, out byte currentFlags) ||
                !TryResolveBuffers(
                    out float* cutProgress01,
                    out int cutProgressLength,
                    out WfcLaserCutTelemetryEntry* blackBox,
                    out int blackBoxLength) ||
                !IsFinite(cutOriginAup) ||
                !IsFinite(hitAup) ||
                !IsFinite(runtimeHitPoint))
            {
                return false;
            }

            RefreshActiveGridFromSignals(cutProgress01, cutProgressLength);
            if (!IsKnownSealedDoorCell(sectorHash, cellIndex))
                return false;

            uint frame = unchecked((uint)Time.frameCount);
            float safePower = Clamp01Finite(cutterPower01);
            float safeHeat = Clamp01Finite(heat01);
            float safeDelta = ClampFiniteNonNegative(progressDelta01);
            bool alreadyUnlocked = (currentFlags & (byte)WfcOutpostCellStateFlags.DoorUnlocked) != 0;
            int progressIndex = cellIndex;
            float previousProgress = alreadyUnlocked ? 1f : Clamp01Finite(cutProgress01[progressIndex]);
            progress01 = alreadyUnlocked ? 1f : Clamp01Finite(previousProgress + safeDelta);
            cutProgress01[progressIndex] = progress01;
            completed = progress01 >= 1f;

            float systemStress01 = ResolveSystemStress01();
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
                blackBox,
                blackBoxLength);

            PublishShaderClipGlobals(runtimeHitPoint, progress01, safeHeat, systemStress01);
            PublishReactiveFeedback(hitAup, toolHash, sectorHash, cellIndex, progress01, safePower, safeHeat, systemStress01, frame);
            door.ApplyWfcOutpostLaserCutProgress(progress01, frame);

            if (completed && !alreadyUnlocked)
                _doorsCutCount++;

            return true;
        }

        private static bool TryResolveBuffers(
            out float* cutProgress01,
            out int cutProgressLength,
            out WfcLaserCutTelemetryEntry* blackBox,
            out int blackBoxLength)
        {
            cutProgress01 = null;
            cutProgressLength = 0;
            blackBox = null;
            blackBoxLength = 0;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (!EnsureVaultHandle(
                    vault,
                    ref _cutProgressHandle,
                    BufferID.WfcDoorCutProgress01,
                    WfcOutpostGridConstants.MaxCellCount))
            {
                return false;
            }

            if (!EnsureVaultHandle(
                    vault,
                    ref _blackBoxHandle,
                    BufferID.WfcLaserCutBlackBox,
                    BlackBoxFrameCount))
            {
                return false;
            }

            cutProgress01 = (float*)_cutProgressHandle.ptr;
            cutProgressLength = _cutProgressHandle.Length;
            blackBox = (WfcLaserCutTelemetryEntry*)_blackBoxHandle.ptr;
            blackBoxLength = _blackBoxHandle.Length;
            return cutProgress01 != null &&
                   cutProgressLength >= WfcOutpostGridConstants.MaxCellCount &&
                   blackBox != null &&
                   blackBoxLength >= BlackBoxFrameCount;
        }

        private static bool EnsureVaultHandle<T>(
            IDataVault vault,
            ref VaultBufferHandle<T> handle,
            BufferID bufferId,
            int requiredLength)
            where T : struct
        {
            if (vault == null)
                return false;

            if (!handle.IsCreated ||
                !vault.ResolveBuffer(ref handle) ||
                handle.Length < requiredLength)
            {
                handle = vault.GetBufferHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.GameplayTools,
                    NativeArrayOptions.ClearMemory);
            }

            return handle.IsCreated && handle.ptr != null && handle.Length >= requiredLength;
        }

        private static void RefreshActiveGridFromSignals(float* cutProgress01, int cutProgressLength)
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

            ClearProgress(cutProgress01, cutProgressLength);
            _activeSectorHash = latest.SectorHash;
            _activeGridHandle = latest.GridHandle;
            _activeGenerationSequence = latest.GenerationSequence;
        }

        private static void ClearProgress(float* cutProgress01, int cutProgressLength)
        {
            if (cutProgress01 == null || cutProgressLength <= 0)
                return;

            int count = math.min(cutProgressLength, WfcOutpostGridConstants.MaxCellCount);
            for (int i = 0; i < count; i++)
                cutProgress01[i] = 0f;
        }

        private static bool IsKnownSealedDoorCell(ulong sectorHash, ushort cellIndex)
        {
            if (cellIndex >= WfcOutpostGridConstants.MaxCellCount)
                return false;

            if (_activeGridHandle == 0u || _activeSectorHash != sectorHash)
                return true;

            if (!WfcOutpostGridRegistry.TryGetGrid(_activeGridHandle, out WfcOutpostGridLease lease) ||
                !lease.Cells.IsCreated ||
                cellIndex >= lease.Cells.Length)
            {
                return true;
            }

            return WfcOutpostGridConstants.IsDoorKind(lease.Cells[cellIndex]);
        }

        private static float ResolveSystemStress01()
        {
            ReadOnlySpan<SystemHealthIndexSignal> signals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return math.saturate(_latestSystemStress01);

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
            return Clamp01Finite(_latestSystemStress01);
        }

        private static void PublishShaderClipGlobals(Vector3 runtimeHitPoint, float progress01, float heat01, float systemStress01)
        {
            if (!IsFinite(runtimeHitPoint))
                return;

            float safeProgress01 = Clamp01Finite(progress01);
            float safeHeat01 = Clamp01Finite(heat01);
            float radius = math.lerp(BaseClipRadiusMeters, MaxClipRadiusMeters, safeProgress01);
            Shader.SetGlobalVector(_CutSphereWsId, new Vector4(runtimeHitPoint.x, runtimeHitPoint.y, runtimeHitPoint.z, radius));
            Shader.SetGlobalFloat(_CutProgressId, safeProgress01);
            Shader.SetGlobalFloat(_CutHeatId, safeHeat01);
            Shader.SetGlobalFloat(_CutMoltenId, Clamp01Finite(safeHeat01 * (0.35f + safeProgress01)));
            Shader.SetGlobalFloat(_CutOverkillId, ResolveVisualOverkill01(systemStress01));
        }

        private static float ResolveVisualOverkill01(float systemStress01)
        {
            if (systemStress01 > StressSparkDropThreshold)
                return 0f;

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.Ultra)
                return 1f;
            if (tier == HectonQualityTier.High)
                return 0.7f;
            if (tier == HectonQualityTier.Mid)
                return 0.2f;

            return 0f;
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
            SignalBus<DebrisSpawnSignal>.Push(in debris);

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
            SignalBus<ToolAcousticSignal>.Push(in acoustic);

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
            SignalBus<HapticRequest>.Push(in haptic);
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
            WfcLaserCutTelemetryEntry* blackBox,
            int blackBoxLength)
        {
            if (blackBox == null || blackBoxLength <= 0)
                return;

            bool valid = IsFinite(cutOriginAup) &&
                         IsFinite(hitAup) &&
                         math.isfinite(progress01) &&
                         math.isfinite(progressDelta01) &&
                         math.isfinite(cutterPower01) &&
                         math.isfinite(heat01) &&
                         math.isfinite(systemStress01);

            int index = (int)(_blackBoxCursor % (uint)blackBoxLength);
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
                DumpBlackBox(blackBox, blackBoxLength);
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

        private static void DumpBlackBox(WfcLaserCutTelemetryEntry* blackBox, int blackBoxLength)
        {
            if (blackBox == null || blackBoxLength <= 0)
                return;

            try
            {
                string assetsPath = Application.dataPath;
                DirectoryInfo projectRoot = Directory.GetParent(assetsPath);
                if (projectRoot == null)
                    return;

                string directory = Path.Combine(projectRoot.FullName, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Dump_TOOL_RESAK_SOLVER.bin");
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(blackBoxLength);
                    writer.Write(_blackBoxCursor);
                    writer.Write(_doorsCutCount);
                    for (int i = 0; i < blackBoxLength; i++)
                    {
                        WfcLaserCutTelemetryEntry entry = blackBox[i];
                        writer.Write(entry.CutOriginAup.x);
                        writer.Write(entry.CutOriginAup.y);
                        writer.Write(entry.CutOriginAup.z);
                        writer.Write(entry.HitAup.x);
                        writer.Write(entry.HitAup.y);
                        writer.Write(entry.HitAup.z);
                        writer.Write(entry.SectorHash);
                        writer.Write(entry.Frame);
                        writer.Write(entry.ToolHash);
                        writer.Write(entry.Progress01);
                        writer.Write(entry.ProgressDelta01);
                        writer.Write(entry.CutterPower01);
                        writer.Write(entry.Heat01);
                        writer.Write(entry.SystemStress01);
                        writer.Write(entry.DoorsCutCount);
                        writer.Write(entry.CellIndex);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Reserved);
                        writer.Write(entry.ReservedPadding);
                    }
                }
            }
            catch (Exception exception)
            {
                _ = exception;
                GlobalTelemetryBus.PublishUnityLogFault(SourceHash, 0u, 1u);
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]
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
