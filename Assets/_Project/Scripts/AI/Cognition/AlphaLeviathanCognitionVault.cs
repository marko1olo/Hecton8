using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Resolved DataVault views for Alpha Leviathan cognition.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AlphaLeviathanVaultBuffers
    {
        public NativeArray<AlphaLeviathanCognitionState> States;
        public NativeArray<AlphaLeviathanSensoryStimulus> SensoryStimuli;
        public NativeArray<AlphaLeviathanSteeringOutput> SteeringOutputs;
        public NativeArray<AlphaLeviathanTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;

        /// <summary>
        /// True when every required DataVault buffer view was resolved.
        /// </summary>
        public readonly bool IsCreated =>
            States.IsCreated &&
            SensoryStimuli.IsCreated &&
            SteeringOutputs.IsCreated &&
            TelemetryRing.IsCreated &&
            TelemetryCursor.IsCreated;
    }

    /// <summary>
    /// Generation-checked DataVault handles for Alpha Leviathan cognition.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 120)]
    public struct AlphaLeviathanVaultHandles
    {
        public VaultBufferHandle<AlphaLeviathanCognitionState> States;
        public VaultBufferHandle<AlphaLeviathanSensoryStimulus> SensoryStimuli;
        public VaultBufferHandle<AlphaLeviathanSteeringOutput> SteeringOutputs;
        public VaultBufferHandle<AlphaLeviathanTelemetryEntry> TelemetryRing;
        public VaultBufferHandle<int> TelemetryCursor;

        /// <summary>
        /// True when every required DataVault handle was resolved from the vault.
        /// </summary>
        public readonly bool IsCreated =>
            States.IsCreated &&
            SensoryStimuli.IsCreated &&
            SteeringOutputs.IsCreated &&
            TelemetryRing.IsCreated &&
            TelemetryCursor.IsCreated;
    }

    /// <summary>
    /// Cold-path bridge that moves Alpha Leviathan stalking truth into GlobalDataVault buffers.
    /// </summary>
    public static class AlphaLeviathanCognitionVault
    {
        private const uint DumpMagic = 0x5053444Cu;
        private const int DumpVersion = 1;
        private const string AgentDumpFileName = "Dump_PREDATOR_STALK_DIRECTOR.bin";

        /// <summary>
        /// Resolves all persistent buffers required by the stalking solver.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="requiredSlots">Requested predator slot capacity.</param>
        /// <param name="buffers">Resolved DataVault buffer views.</param>
        /// <returns>True when every buffer is available.</returns>
        public static bool TryResolve(IDataVault vault, int requiredSlots, out AlphaLeviathanVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            int capacity = math.clamp(requiredSlots, 1, AlphaLeviathanStalkConstants.MaxLeviathanSlots);
            buffers.States = vault.GetBuffer<AlphaLeviathanCognitionState>(
                BufferID.AlphaLeviathanCognitionState,
                capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            buffers.SensoryStimuli = vault.GetBuffer<AlphaLeviathanSensoryStimulus>(
                BufferID.AlphaLeviathanSensoryStimulus,
                capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            buffers.SteeringOutputs = vault.GetBuffer<AlphaLeviathanSteeringOutput>(
                BufferID.AlphaLeviathanSteeringOutput,
                capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            buffers.TelemetryRing = vault.GetBuffer<AlphaLeviathanTelemetryEntry>(
                BufferID.AlphaLeviathanTelemetryRing,
                AlphaLeviathanStalkConstants.TelemetryCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            buffers.TelemetryCursor = vault.GetBuffer<int>(
                BufferID.AlphaLeviathanTelemetryCursor,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            return buffers.IsCreated;
        }

        /// <summary>
        /// Resolves generation-checked handles for every persistent stalking buffer.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="requiredSlots">Requested predator slot capacity.</param>
        /// <param name="handles">Resolved DataVault handles.</param>
        /// <returns>True when every handle is available.</returns>
        public static bool TryResolveHandles(IDataVault vault, int requiredSlots, out AlphaLeviathanVaultHandles handles)
        {
            handles = default;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            int capacity = math.clamp(requiredSlots, 1, AlphaLeviathanStalkConstants.MaxLeviathanSlots);
            handles.States = vault.GetBufferHandle<AlphaLeviathanCognitionState>(
                BufferID.AlphaLeviathanCognitionState,
                capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.SensoryStimuli = vault.GetBufferHandle<AlphaLeviathanSensoryStimulus>(
                BufferID.AlphaLeviathanSensoryStimulus,
                capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.SteeringOutputs = vault.GetBufferHandle<AlphaLeviathanSteeringOutput>(
                BufferID.AlphaLeviathanSteeringOutput,
                capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = vault.GetBufferHandle<AlphaLeviathanTelemetryEntry>(
                BufferID.AlphaLeviathanTelemetryRing,
                AlphaLeviathanStalkConstants.TelemetryCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.GetBufferHandle<int>(
                BufferID.AlphaLeviathanTelemetryCursor,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            return handles.IsCreated;
        }

        /// <summary>
        /// Resolves transient NativeArray views from cached generation-checked handles.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="handles">Cached handles. Generations are refreshed on success.</param>
        /// <param name="buffers">Resolved transient DataVault buffer views.</param>
        /// <returns>True when every handle resolved to a current view.</returns>
        public static bool TryResolveViews(IDataVault vault, ref AlphaLeviathanVaultHandles handles, out AlphaLeviathanVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated)
                return false;

            buffers.States = handles.States.Resolve(vault);
            buffers.SensoryStimuli = handles.SensoryStimuli.Resolve(vault);
            buffers.SteeringOutputs = handles.SteeringOutputs.Resolve(vault);
            buffers.TelemetryRing = handles.TelemetryRing.Resolve(vault);
            buffers.TelemetryCursor = handles.TelemetryCursor.Resolve(vault);
            return buffers.IsCreated;
        }

        /// <summary>
        /// Creates the canonical stalk job from DataVault-owned buffer views.
        /// </summary>
        /// <param name="buffers">Resolved DataVault buffer views.</param>
        /// <param name="frame">Global simulation frame written into telemetry.</param>
        /// <returns>Configured job. The returned job is invalid when <paramref name="buffers"/> is not created.</returns>
        public static LeviathanStalkJob CreateStalkJob(in AlphaLeviathanVaultBuffers buffers, uint frame)
        {
            return new LeviathanStalkJob
            {
                States = buffers.States,
                SensoryStimuli = buffers.SensoryStimuli,
                SteeringOutputs = buffers.SteeringOutputs,
                TelemetryRing = buffers.TelemetryRing,
                Frame = frame
            };
        }

        /// <summary>
        /// Cold crash-path dump of the Alpha Leviathan telemetry ring.
        /// </summary>
        /// <param name="buffers">Resolved DataVault buffer views.</param>
        /// <param name="projectRoot">Project root path. Pass `C:\hades\Hecton8` from the owner.</param>
        /// <returns>True when a binary dump was written.</returns>
        public static bool TryDumpBlackBox(in AlphaLeviathanVaultBuffers buffers, string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            string root = string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
            string path = Path.Combine(root, "Docs", "AgentLogs", AgentDumpFileName);

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(DumpMagic);
                    writer.Write(DumpVersion);
                    writer.Write(AlphaLeviathanStalkConstants.TelemetryFrames);
                    writer.Write(AlphaLeviathanStalkConstants.MaxLeviathanSlots);
                    writer.Write(buffers.TelemetryRing.Length);
                    int cursor = ResolveTelemetryCursor(in buffers);
                    writer.Write(cursor);

                    for (int i = 0; i < buffers.TelemetryRing.Length; i++)
                    {
                        AlphaLeviathanTelemetryEntry entry = buffers.TelemetryRing[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.Slot);
                        writer.Write(entry.Phase);
                        writer.Write(entry.Flags);
                        writer.Write(entry.DistanceToPlayerMeters);
                        writer.Write(entry.FogRingDistanceMeters);
                        writer.Write(entry.Position.x);
                        writer.Write(entry.Position.y);
                        writer.Write(entry.Position.z);
                        writer.Write(entry.PlayerPosition.x);
                        writer.Write(entry.PlayerPosition.y);
                        writer.Write(entry.PlayerPosition.z);
                        writer.Write(entry.DesiredDirection.x);
                        writer.Write(entry.DesiredDirection.y);
                        writer.Write(entry.DesiredDirection.z);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.LeviathanAgressivity01);
                        writer.Write(entry.Reserved1);
                    }
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Cold fault path used by the owner after the Burst job has written telemetry flags.
        /// </summary>
        /// <param name="buffers">Resolved DataVault buffer views.</param>
        /// <param name="projectRoot">Project root path. Pass `C:\hades\Hecton8` from the owner.</param>
        /// <returns>True when a fault was detected and a binary dump was written.</returns>
        public static bool TryDumpBlackBoxOnFault(in AlphaLeviathanVaultBuffers buffers, string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            for (int i = 0; i < buffers.TelemetryRing.Length; i++)
            {
                AlphaLeviathanTelemetryEntry entry = buffers.TelemetryRing[i];
                if ((entry.Flags & AlphaLeviathanTelemetryFlags.Fault) != 0)
                    return TryDumpBlackBox(in buffers, projectRoot);
            }

            return false;
        }

        private static int ResolveTelemetryCursor(in AlphaLeviathanVaultBuffers buffers)
        {
            int cursor = 0;
            if (buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0)
                cursor = math.clamp(buffers.TelemetryCursor[0], 0, AlphaLeviathanStalkConstants.TelemetryFrames - 1);

            uint latestFrame = 0u;
            for (int i = 0; i < buffers.TelemetryRing.Length; i++)
            {
                AlphaLeviathanTelemetryEntry entry = buffers.TelemetryRing[i];
                if (entry.Frame >= latestFrame)
                {
                    latestFrame = entry.Frame;
                    cursor = (int)(latestFrame % AlphaLeviathanStalkConstants.TelemetryFrames);
                }
            }

            return cursor;
        }
    }
}
