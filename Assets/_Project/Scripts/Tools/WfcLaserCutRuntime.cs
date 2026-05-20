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
    internal static class WfcLaserCutRuntime
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

        private static IDataVault _dataVault;
        private static VaultGenerationHandle<float> _cutProgressHandle;
        private static VaultGenerationHandle<WfcLaserCutTelemetryEntry> _blackBoxHandle;
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
                    out NativeArray<float> cutProgress01,
                    out NativeArray<WfcLaserCutTelemetryEntry> blackBox) ||
                !IsFinite(cutOriginAup) ||
                !IsFinite(hitAup) ||
                !IsFinite(runtimeHitPoint))
            {
                return false;
            }

            RefreshActiveGridFromSignals(cutProgress01);
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
                blackBox);

            PublishShaderClipGlobals(runtimeHitPoint, progress01, safeHeat, systemStress01);
            PublishReactiveFeedback(hitAup, toolHash, sectorHash, cellIndex, progress01, safePower, safeHeat, systemStress01, frame);
            door.ApplyWfcOutpostLaserCutProgress(progress01, frame);

            if (completed && !alreadyUnlocked)
                _doorsCutCount++;

            return true;
        }

        private static bool TryResolveBuffers(
            out NativeArray<float> cutProgress01,
            out NativeArray<WfcLaserCutTelemetryEntry> blackBox)
        {
            cutProgress01 = default;
            blackBox = default;

            IDataVault vault = GlobalRegistry.DataVault;
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

            if (!TryResolveOrAcquireVaultBuffer(
                    vault,
                    BufferID.WfcDoorCutProgress01,
                    WfcOutpostGridConstants.MaxCellCount,
                    ref _cutProgressHandle,
                    out cutProgress01))
            {
                return false;
            }

            if (!TryResolveOrAcquireVaultBuffer(
                    vault,
                    BufferID.WfcLaserCutBlackBox,
                    BlackBoxFrameCount,
                    ref _blackBoxHandle,
                    out blackBox))
            {
                return false;
            }

            return cutProgress01.IsCreated &&
                   cutProgress01.Length >= WfcOutpostGridConstants.MaxCellCount &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxFrameCount;
        }

        private static bool TryResolveOrAcquireVaultBuffer<T>(
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

            VaultGenerationHandle<T> acquired = vault.GetGenerationHandle<T>(
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
        }

        private static void ClearProgress(NativeArray<float> cutProgress01)
        {
            if (!cutProgress01.IsCreated || cutProgress01.Length <= 0)
                return;

            int count = math.min(cutProgress01.Length, WfcOutpostGridConstants.MaxCellCount);
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
            float quality01 = Clamp01Finite(HomeostasisBrain.GlobalQualityWeight);
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

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static void DumpBlackBox(NativeArray<WfcLaserCutTelemetryEntry> blackBox)
        {
            if (!blackBox.IsCreated || blackBox.Length <= 0)
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
                    writer.Write(blackBox.Length);
                    writer.Write(_blackBoxCursor);
                    writer.Write(_doorsCutCount);
                    for (int i = 0; i < blackBox.Length; i++)
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
