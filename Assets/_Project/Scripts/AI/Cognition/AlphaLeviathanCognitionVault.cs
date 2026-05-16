using System;
using System.IO;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Resolved DataVault views for Alpha Leviathan cognition.
    /// </summary>
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
                    int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0
                        ? buffers.TelemetryCursor[0]
                        : 0;
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
    }
}
