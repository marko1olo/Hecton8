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
            if (vault == null)
                return false;

            int capacity = math.clamp(requiredSlots, 1, AlphaLeviathanStalkConstants.MaxLeviathanSlots);
            if (vault.IsAllocationLocked)
                return TryResolveExisting(vault, capacity, out buffers);

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
            if (vault == null)
                return false;

            int capacity = math.clamp(requiredSlots, 1, AlphaLeviathanStalkConstants.MaxLeviathanSlots);
            if (vault.IsAllocationLocked)
                return TryResolveExistingHandles(vault, capacity, out handles);

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

        private static bool TryResolveExisting(IDataVault vault, int requiredSlots, out AlphaLeviathanVaultBuffers buffers)
        {
            buffers = default;
            if (!vault.TryGetBuffer(BufferID.AlphaLeviathanCognitionState, out buffers.States) ||
                !vault.TryGetBuffer(BufferID.AlphaLeviathanSensoryStimulus, out buffers.SensoryStimuli) ||
                !vault.TryGetBuffer(BufferID.AlphaLeviathanSteeringOutput, out buffers.SteeringOutputs) ||
                !vault.TryGetBuffer(BufferID.AlphaLeviathanTelemetryRing, out buffers.TelemetryRing) ||
                !vault.TryGetBuffer(BufferID.AlphaLeviathanTelemetryCursor, out buffers.TelemetryCursor))
            {
                buffers = default;
                return false;
            }

            if (!HasRequiredCapacity(in buffers, requiredSlots))
            {
                buffers = default;
                return false;
            }

            return true;
        }

        private static bool TryResolveExistingHandles(IDataVault vault, int requiredSlots, out AlphaLeviathanVaultHandles handles)
        {
            handles = default;
            if (!vault.TryGetBufferHandle(BufferID.AlphaLeviathanCognitionState, out handles.States) ||
                !vault.TryGetBufferHandle(BufferID.AlphaLeviathanSensoryStimulus, out handles.SensoryStimuli) ||
                !vault.TryGetBufferHandle(BufferID.AlphaLeviathanSteeringOutput, out handles.SteeringOutputs) ||
                !vault.TryGetBufferHandle(BufferID.AlphaLeviathanTelemetryRing, out handles.TelemetryRing) ||
                !vault.TryGetBufferHandle(BufferID.AlphaLeviathanTelemetryCursor, out handles.TelemetryCursor))
            {
                handles = default;
                return false;
            }

            AlphaLeviathanVaultHandles resolved = handles;
            if (!TryResolveViews(vault, ref resolved, out AlphaLeviathanVaultBuffers buffers) ||
                !HasRequiredCapacity(in buffers, requiredSlots))
            {
                handles = default;
                return false;
            }

            handles = resolved;
            return true;
        }

        private static bool HasRequiredCapacity(in AlphaLeviathanVaultBuffers buffers, int requiredSlots)
        {
            return buffers.IsCreated &&
                   buffers.States.Length >= requiredSlots &&
                   buffers.SensoryStimuli.Length >= requiredSlots &&
                   buffers.SteeringOutputs.Length >= requiredSlots &&
                   buffers.TelemetryRing.Length >= AlphaLeviathanStalkConstants.TelemetryCapacity &&
                   buffers.TelemetryCursor.Length > 0;
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
        /// Computes the safe row count for scheduling <see cref="LeviathanStalkJob"/> from resolved DataVault views.
        /// </summary>
        /// <param name="buffers">Resolved DataVault buffer views.</param>
        /// <returns>Maximum safe schedule length across state, sensory, output, and telemetry views.</returns>
        public static int GetScheduleLength(in AlphaLeviathanVaultBuffers buffers)
        {
            if (!buffers.IsCreated)
                return 0;

            int telemetrySlots = buffers.TelemetryRing.Length / AlphaLeviathanStalkConstants.TelemetryFrames;
            int length = math.min(buffers.States.Length, buffers.SensoryStimuli.Length);
            length = math.min(length, buffers.SteeringOutputs.Length);
            length = math.min(length, telemetrySlots);
            length = math.min(length, AlphaLeviathanStalkConstants.MaxLeviathanSlots);
            return math.max(0, length);
        }

        /// <summary>
        /// Resolves handles and computes the safe schedule row count without exposing long-lived raw views.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="handles">Cached handles. Generations are refreshed on success.</param>
        /// <param name="scheduleLength">Maximum safe schedule length across required views.</param>
        /// <returns>True when views resolve and at least one row can be scheduled.</returns>
        public static bool TryGetScheduleLength(
            IDataVault vault,
            ref AlphaLeviathanVaultHandles handles,
            out int scheduleLength)
        {
            scheduleLength = 0;
            if (!TryResolveViews(vault, ref handles, out AlphaLeviathanVaultBuffers buffers))
                return false;

            scheduleLength = GetScheduleLength(in buffers);
            return scheduleLength > 0;
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
        /// Creates the canonical stalk job and safe schedule length from DataVault-owned buffer views.
        /// </summary>
        /// <param name="buffers">Resolved DataVault buffer views.</param>
        /// <param name="frame">Global simulation frame written into telemetry.</param>
        /// <param name="job">Configured job when the views can schedule at least one row.</param>
        /// <param name="scheduleLength">Maximum safe row count for the owner-side job schedule call.</param>
        /// <returns>True when the job and schedule length were configured from current DataVault views.</returns>
        public static bool TryCreateStalkJob(
            in AlphaLeviathanVaultBuffers buffers,
            uint frame,
            out LeviathanStalkJob job,
            out int scheduleLength)
        {
            job = default;
            scheduleLength = GetScheduleLength(in buffers);
            if (scheduleLength <= 0)
                return false;

            job = CreateStalkJob(in buffers, frame);
            return true;
        }

        /// <summary>
        /// Creates the canonical stalk job from generation-checked DataVault handles.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="handles">Cached handles. Generations are refreshed on success.</param>
        /// <param name="frame">Global simulation frame written into telemetry.</param>
        /// <param name="job">Configured job when every handle resolved to a current view.</param>
        /// <returns>True when the job was configured from current DataVault views.</returns>
        public static bool TryCreateStalkJob(
            IDataVault vault,
            ref AlphaLeviathanVaultHandles handles,
            uint frame,
            out LeviathanStalkJob job)
        {
            int scheduleLength;
            return TryCreateStalkJob(vault, ref handles, frame, out job, out scheduleLength);
        }

        /// <summary>
        /// Creates the canonical stalk job and safe schedule length from generation-checked DataVault handles.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="handles">Cached handles. Generations are refreshed on success.</param>
        /// <param name="frame">Global simulation frame written into telemetry.</param>
        /// <param name="job">Configured job when every handle resolved to a current view.</param>
        /// <param name="scheduleLength">Maximum safe row count for the owner-side job schedule call.</param>
        /// <returns>True when the job and schedule length were configured from current DataVault views.</returns>
        public static bool TryCreateStalkJob(
            IDataVault vault,
            ref AlphaLeviathanVaultHandles handles,
            uint frame,
            out LeviathanStalkJob job,
            out int scheduleLength)
        {
            job = default;
            scheduleLength = 0;
            if (!TryResolveViews(vault, ref handles, out AlphaLeviathanVaultBuffers buffers))
                return false;

            return TryCreateStalkJob(in buffers, frame, out job, out scheduleLength);
        }

        /// <summary>
        /// Records the latest telemetry frame cursor after the stalk job completes.
        /// </summary>
        /// <param name="buffers">Resolved DataVault buffer views.</param>
        /// <param name="frame">Global simulation frame that was written into telemetry.</param>
        /// <returns>True when the cursor was recorded.</returns>
        public static bool TryRecordTelemetryHeartbeat(AlphaLeviathanVaultBuffers buffers, uint frame)
        {
            if (GetScheduleLength(in buffers) <= 0 || buffers.TelemetryCursor.Length <= 0)
                return false;

            buffers.TelemetryCursor[0] = (int)(frame % AlphaLeviathanStalkConstants.TelemetryFrames);
            return true;
        }

        /// <summary>
        /// Records the latest telemetry frame cursor through generation-checked handles.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="handles">Cached handles. Generations are refreshed on success.</param>
        /// <param name="frame">Global simulation frame that was written into telemetry.</param>
        /// <returns>True when the cursor was recorded.</returns>
        public static bool TryRecordTelemetryHeartbeat(
            IDataVault vault,
            ref AlphaLeviathanVaultHandles handles,
            uint frame)
        {
            if (!TryResolveViews(vault, ref handles, out AlphaLeviathanVaultBuffers buffers))
                return false;

            return TryRecordTelemetryHeartbeat(buffers, frame);
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
            string tempPath = path + ".tmp";

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                TryDeleteFile(tempPath);

                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
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

                return TryPromoteDump(tempPath, path);
            }
            catch (IOException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (ArgumentException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (NotSupportedException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
        }

        /// <summary>
        /// Cold crash-path dump using generation-checked DataVault handles instead of cached raw views.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="handles">Cached handles. Generations are refreshed on success.</param>
        /// <param name="projectRoot">Project root path. Pass `C:\hades\Hecton8` from the owner.</param>
        /// <returns>True when a binary dump was written.</returns>
        public static bool TryDumpBlackBox(
            IDataVault vault,
            ref AlphaLeviathanVaultHandles handles,
            string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out AlphaLeviathanVaultBuffers buffers))
                return false;

            return TryDumpBlackBox(in buffers, projectRoot);
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

        /// <summary>
        /// Cold fault path that scans only the frame written by the most recent stalk job.
        /// </summary>
        /// <param name="buffers">Resolved DataVault buffer views.</param>
        /// <param name="frame">Global simulation frame that was written into telemetry.</param>
        /// <param name="projectRoot">Project root path. Pass `C:\hades\Hecton8` from the owner.</param>
        /// <returns>True when the current frame contains a fault and a binary dump was written.</returns>
        public static bool TryDumpBlackBoxOnFrameFault(in AlphaLeviathanVaultBuffers buffers, uint frame, string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            int telemetryFrame = (int)(frame % AlphaLeviathanStalkConstants.TelemetryFrames);
            int frameStart = telemetryFrame * AlphaLeviathanStalkConstants.MaxLeviathanSlots;
            if ((uint)frameStart >= (uint)buffers.TelemetryRing.Length)
                return false;

            int frameEnd = math.min(frameStart + AlphaLeviathanStalkConstants.MaxLeviathanSlots, buffers.TelemetryRing.Length);
            for (int i = frameStart; i < frameEnd; i++)
            {
                AlphaLeviathanTelemetryEntry entry = buffers.TelemetryRing[i];
                if (entry.Frame == frame && (entry.Flags & AlphaLeviathanTelemetryFlags.Fault) != 0)
                    return TryDumpBlackBox(in buffers, projectRoot);
            }

            return false;
        }

        /// <summary>
        /// Cold fault path that resolves handles and scans only the frame written by the most recent stalk job.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="handles">Cached handles. Generations are refreshed on success.</param>
        /// <param name="frame">Global simulation frame that was written into telemetry.</param>
        /// <param name="projectRoot">Project root path. Pass `C:\hades\Hecton8` from the owner.</param>
        /// <returns>True when the current frame contains a fault and a binary dump was written.</returns>
        public static bool TryDumpBlackBoxOnFrameFault(
            IDataVault vault,
            ref AlphaLeviathanVaultHandles handles,
            uint frame,
            string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out AlphaLeviathanVaultBuffers buffers))
                return false;

            return TryDumpBlackBoxOnFrameFault(in buffers, frame, projectRoot);
        }

        /// <summary>
        /// Cold fault path using generation-checked DataVault handles instead of cached raw views.
        /// </summary>
        /// <param name="vault">GlobalDataVault service cached by the caller outside hot paths.</param>
        /// <param name="handles">Cached handles. Generations are refreshed on success.</param>
        /// <param name="projectRoot">Project root path. Pass `C:\hades\Hecton8` from the owner.</param>
        /// <returns>True when a fault was detected and a binary dump was written.</returns>
        public static bool TryDumpBlackBoxOnFault(
            IDataVault vault,
            ref AlphaLeviathanVaultHandles handles,
            string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out AlphaLeviathanVaultBuffers buffers))
                return false;

            return TryDumpBlackBoxOnFault(in buffers, projectRoot);
        }

        private static int ResolveTelemetryCursor(in AlphaLeviathanVaultBuffers buffers)
        {
            int cursor = 0;
            if (buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0)
                cursor = math.clamp(buffers.TelemetryCursor[0], 0, AlphaLeviathanStalkConstants.TelemetryFrames - 1);

            uint latestFrame = 0u;
            bool hasLatestFrame = false;
            for (int i = 0; i < buffers.TelemetryRing.Length; i++)
            {
                AlphaLeviathanTelemetryEntry entry = buffers.TelemetryRing[i];
                if (entry.StateHash != 0u && (!hasLatestFrame || IsFrameNewerOrEqual(entry.Frame, latestFrame)))
                {
                    latestFrame = entry.Frame;
                    cursor = (int)(latestFrame % AlphaLeviathanStalkConstants.TelemetryFrames);
                    hasLatestFrame = true;
                }
            }

            return cursor;
        }

        private static bool IsFrameNewerOrEqual(uint candidateFrame, uint currentFrame)
        {
            return candidateFrame == currentFrame || unchecked(candidateFrame - currentFrame) < 0x80000000u;
        }

        private static bool TryPromoteDump(string tempPath, string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);

                return true;
            }
            catch (IOException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (ArgumentException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (NotSupportedException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}
