using System;
using System.IO;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Vault buffer IDs reserved by SHINOBU_61 without mutating the shared BufferID enum.
    /// </summary>
    public static class ApexBrainVaultBufferIds
    {
        public const BufferID ApexState = (BufferID)70609;
        public const BufferID MockPlayerAup = (BufferID)70610;
        public const BufferID AcousticEchoTap = (BufferID)70611;
        public const BufferID Tuning = (BufferID)70612;
        public const BufferID EmergencyStats = (BufferID)70613;
        public const BufferID MockWorldSampler = (BufferID)70614;
        public const BufferID Output = (BufferID)70615;
        public const BufferID ProximitySignal = (BufferID)70616;
        public const BufferID CombatDamageSignal = (BufferID)70617;
        public const BufferID PanicSignal = (BufferID)70618;
        public const BufferID InfluenceNodes = (BufferID)70619;
        public const BufferID TelemetryRing = (BufferID)70626;
        public const BufferID TelemetryCursor = (BufferID)70627;
        public const BufferID CsvScratch = (BufferID)70628;
        public const BufferID AmbushNodeScratch = (BufferID)70629;
    }

    /// <summary>
    /// Generation-checked DataVault handles for the apex brain.
    /// </summary>
    public struct ApexBrainVaultHandles
    {
        public VaultBufferHandle<ApexStateDTO> States;
        public VaultBufferHandle<MockPlayerAUP> MockTargets;
        public VaultBufferHandle<AcousticEchoTap> AcousticTaps;
        public VaultBufferHandle<ApexBrainTuning> Tuning;
        public VaultBufferHandle<ApexEmergencyStats> EmergencyStats;
        public VaultBufferHandle<MockWorldSampler> WorldSampler;
        public VaultBufferHandle<ApexBrainOutputDTO> Outputs;
        public VaultBufferHandle<ApexProximitySignal> ProximitySignals;
        public VaultBufferHandle<MockCombatDamageSignal> CombatDamageSignals;
        public VaultBufferHandle<GlobalPanicSignal> PanicSignals;
        public VaultBufferHandle<ApexInfluenceNode> InfluenceNodes;
        public VaultBufferHandle<float3> AmbushNodeScratch;
        public VaultBufferHandle<ApexTelemetryEntry> TelemetryRing;
        public VaultBufferHandle<int> TelemetryCursor;
        public VaultBufferHandle<byte> CsvScratch;

        public bool IsCreated()
        {
            return States.IsCreated &&
                   MockTargets.IsCreated &&
                   AcousticTaps.IsCreated &&
                   Tuning.IsCreated &&
                   EmergencyStats.IsCreated &&
                   WorldSampler.IsCreated &&
                   Outputs.IsCreated &&
                   ProximitySignals.IsCreated &&
                   CombatDamageSignals.IsCreated &&
                   PanicSignals.IsCreated &&
                   InfluenceNodes.IsCreated &&
                   AmbushNodeScratch.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   CsvScratch.IsCreated;
        }
    }

    /// <summary>
    /// Transient NativeArray views resolved from generation-checked handles.
    /// </summary>
    public struct ApexBrainVaultBuffers
    {
        public NativeArray<ApexStateDTO> States;
        public NativeArray<MockPlayerAUP> MockTargets;
        public NativeArray<AcousticEchoTap> AcousticTaps;
        public NativeArray<ApexBrainTuning> Tuning;
        public NativeArray<ApexEmergencyStats> EmergencyStats;
        public NativeArray<MockWorldSampler> WorldSampler;
        public NativeArray<ApexBrainOutputDTO> Outputs;
        public NativeArray<ApexProximitySignal> ProximitySignals;
        public NativeArray<MockCombatDamageSignal> CombatDamageSignals;
        public NativeArray<GlobalPanicSignal> PanicSignals;
        public NativeArray<ApexInfluenceNode> InfluenceNodes;
        public NativeArray<float3> AmbushNodeScratch;
        public NativeArray<ApexTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<byte> CsvScratch;

        public bool IsCreated()
        {
            return States.IsCreated &&
                   MockTargets.IsCreated &&
                   AcousticTaps.IsCreated &&
                   Tuning.IsCreated &&
                   EmergencyStats.IsCreated &&
                   WorldSampler.IsCreated &&
                   Outputs.IsCreated &&
                   ProximitySignals.IsCreated &&
                   CombatDamageSignals.IsCreated &&
                   PanicSignals.IsCreated &&
                   InfluenceNodes.IsCreated &&
                   AmbushNodeScratch.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   CsvScratch.IsCreated;
        }
    }

    /// <summary>
    /// Cold bridge for DataVault hydration, CSV overrides, job creation, and black-box dumps.
    /// </summary>
    public static class ApexBrainVault
    {
        private const uint DumpMagic = 0x53484E61u;
        private const uint DumpEndianMarker = 0x01020304u;
        private const int DumpVersion = 1;
        private const string DumpFileName = "Dump_SHINOBU_61.bin";
        private const string LegacyDumpFileName = "Dump_LEVIATHAN_CORTEX.bin";
        private const string CsvFileName = "apex_predator_stats.csv";
        private static readonly uint _aggressionMultiplierHash = HashAscii("aggression_multiplier");
        private static readonly uint _acousticSensitivityHash = HashAscii("acoustic_sensitivity");
        private static readonly uint _turnRateHash = HashAscii("turn_rate");
        private static readonly uint _stalkingDistanceHash = HashAscii("stalking_distance");
        private static readonly uint _leviathanSpeedHash = HashAscii("leviathan_speed");
        private static readonly uint _terrorRadiusHash = HashAscii("terror_radius");
        private static readonly uint _biomeAggressionHash = HashAscii("biome_aggression_multiplier");
        private static readonly uint _strikeDistanceHash = HashAscii("strike_distance");
        private static readonly uint _globalQualityHash = HashAscii("global_quality_weight");

        /// <summary>
        /// Resolves or creates all SHINOBU_61 vault handles.
        /// </summary>
        public static bool TryResolve(IDataVault vault, out ApexBrainVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!TryResolveExisting(vault, out handles))
                    return false;

                if (TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers lockedBuffers))
                    GenerateEmergencyMockApexStats(lockedBuffers);

                return handles.IsCreated();
            }

            handles.States = vault.GetBufferHandle<ApexStateDTO>(
                ApexBrainVaultBufferIds.ApexState,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.MockTargets = vault.GetBufferHandle<MockPlayerAUP>(
                ApexBrainVaultBufferIds.MockPlayerAup,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.AcousticTaps = vault.GetBufferHandle<AcousticEchoTap>(
                ApexBrainVaultBufferIds.AcousticEchoTap,
                ApexBrainConstants.MaxAcousticTaps,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.Tuning = vault.GetBufferHandle<ApexBrainTuning>(
                ApexBrainVaultBufferIds.Tuning,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.EmergencyStats = vault.GetBufferHandle<ApexEmergencyStats>(
                ApexBrainVaultBufferIds.EmergencyStats,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.WorldSampler = vault.GetBufferHandle<MockWorldSampler>(
                ApexBrainVaultBufferIds.MockWorldSampler,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.Outputs = vault.GetBufferHandle<ApexBrainOutputDTO>(
                ApexBrainVaultBufferIds.Output,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.ProximitySignals = vault.GetBufferHandle<ApexProximitySignal>(
                ApexBrainVaultBufferIds.ProximitySignal,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.CombatDamageSignals = vault.GetBufferHandle<MockCombatDamageSignal>(
                ApexBrainVaultBufferIds.CombatDamageSignal,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.PanicSignals = vault.GetBufferHandle<GlobalPanicSignal>(
                ApexBrainVaultBufferIds.PanicSignal,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.InfluenceNodes = vault.GetBufferHandle<ApexInfluenceNode>(
                ApexBrainVaultBufferIds.InfluenceNodes,
                ApexBrainConstants.InfluenceNodeCapacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.AmbushNodeScratch = vault.GetBufferHandle<float3>(
                ApexBrainVaultBufferIds.AmbushNodeScratch,
                ApexBrainConstants.InfluenceNodeCapacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.GetBufferHandle<ApexTelemetryEntry>(
                ApexBrainVaultBufferIds.TelemetryRing,
                ApexBrainConstants.TelemetryCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.GetBufferHandle<int>(
                ApexBrainVaultBufferIds.TelemetryCursor,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.CsvScratch = vault.GetBufferHandle<byte>(
                ApexBrainVaultBufferIds.CsvScratch,
                ApexBrainConstants.CsvScratchBytes,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);

            if (!handles.IsCreated())
                return false;

            if (TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers buffers))
                GenerateEmergencyMockApexStats(buffers);

            return true;
        }

        /// <summary>
        /// Resolves transient NativeArray views from handles.
        /// </summary>
        public static bool TryResolveViews(IDataVault vault, ref ApexBrainVaultHandles handles, out ApexBrainVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            buffers.States = handles.States.Resolve(vault);
            buffers.MockTargets = handles.MockTargets.Resolve(vault);
            buffers.AcousticTaps = handles.AcousticTaps.Resolve(vault);
            buffers.Tuning = handles.Tuning.Resolve(vault);
            buffers.EmergencyStats = handles.EmergencyStats.Resolve(vault);
            buffers.WorldSampler = handles.WorldSampler.Resolve(vault);
            buffers.Outputs = handles.Outputs.Resolve(vault);
            buffers.ProximitySignals = handles.ProximitySignals.Resolve(vault);
            buffers.CombatDamageSignals = handles.CombatDamageSignals.Resolve(vault);
            buffers.PanicSignals = handles.PanicSignals.Resolve(vault);
            buffers.InfluenceNodes = handles.InfluenceNodes.Resolve(vault);
            buffers.AmbushNodeScratch = handles.AmbushNodeScratch.Resolve(vault);
            buffers.TelemetryRing = handles.TelemetryRing.Resolve(vault);
            buffers.TelemetryCursor = handles.TelemetryCursor.Resolve(vault);
            buffers.CsvScratch = handles.CsvScratch.Resolve(vault);
            return buffers.IsCreated();
        }

        /// <summary>
        /// Returns a mutable ref to ApexStateDTO in vault memory.
        /// </summary>
        public static ref ApexStateDTO GetStateAsRef(IDataVault vault, ref ApexBrainVaultHandles handles, int index)
        {
            return ref handles.States.GetElementAsRef(vault, index);
        }

        /// <summary>
        /// Resets one spawned apex slot with UnsafeUtility.MemClear.
        /// </summary>
        public static unsafe bool TryClearSpawnSlot(NativeArray<ApexStateDTO> states, int index)
        {
            if (!states.IsCreated || (uint)index >= (uint)states.Length)
                return false;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(states);
            UnsafeUtility.MemClear((byte*)ptr + (index * UnsafeUtility.SizeOf<ApexStateDTO>()), UnsafeUtility.SizeOf<ApexStateDTO>());
            return true;
        }

        /// <summary>
        /// Builds the configured apex brain job and safe schedule length.
        /// </summary>
        public static bool TryCreateJob(
            in ApexBrainVaultBuffers buffers,
            uint frame,
            out ApexBrainJob job,
            out int scheduleLength)
        {
            job = default;
            scheduleLength = GetScheduleLength(in buffers);
            if (scheduleLength <= 0)
                return false;

            job.States = buffers.States;
            job.MockTargets = buffers.MockTargets;
            job.AcousticTaps = buffers.AcousticTaps;
            job.Tuning = buffers.Tuning;
            job.EmergencyStats = buffers.EmergencyStats;
            job.WorldSampler = buffers.WorldSampler;
            job.Outputs = buffers.Outputs;
            job.ProximitySignals = buffers.ProximitySignals;
            job.CombatDamageSignals = buffers.CombatDamageSignals;
            job.PanicSignals = buffers.PanicSignals;
            job.InfluenceNodes = buffers.InfluenceNodes;
            job.AmbushNodeScratch = buffers.AmbushNodeScratch;
            job.TelemetryRing = buffers.TelemetryRing;
            job.TargetCount = buffers.MockTargets.Length;
            job.AcousticTapCount = buffers.AcousticTaps.Length;
            job.Frame = frame;
            return true;
        }

        /// <summary>
        /// Creates a dependency-preserving schedule. Caller owns returned JobHandle and must not Complete mid-frame.
        /// </summary>
        public static bool TrySchedule(
            in ApexBrainVaultBuffers buffers,
            uint frame,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!ShouldEvaluateFrame(in buffers, frame))
                return false;

            if (!TryCreateJob(in buffers, frame, out ApexBrainJob job, out int scheduleLength))
                return false;

            outputDependency = job.Schedule(scheduleLength, 1, inputDependency);
            return true;
        }

        /// <summary>
        /// Creates a dependency-preserving schedule with external SignalBus/NativeQueue writers attached by the owning core bridge.
        /// </summary>
        public static bool TryScheduleWithSignalWriters(
            in ApexBrainVaultBuffers buffers,
            uint frame,
            JobHandle inputDependency,
            NativeQueue<ApexProximitySignal>.ParallelWriter proximityWriter,
            NativeQueue<MockCombatDamageSignal>.ParallelWriter combatWriter,
            NativeQueue<GlobalPanicSignal>.ParallelWriter panicWriter,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!ShouldEvaluateFrame(in buffers, frame))
                return false;

            if (!TryCreateJob(in buffers, frame, out ApexBrainJob job, out int scheduleLength))
                return false;

            AttachSignalWriters(ref job, proximityWriter, combatWriter, panicWriter);
            outputDependency = job.Schedule(scheduleLength, 1, inputDependency);
            return true;
        }

        /// <summary>
        /// Continuous quality gate for scheduler owners: 5 Hz at survival quality, 60 Hz at full quality.
        /// </summary>
        public static bool ShouldEvaluateFrame(in ApexBrainVaultBuffers buffers, uint frame)
        {
            float quality = 1f;
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
                quality = ResolveSchedulingQuality(buffers.Tuning[0].GlobalQualityWeight);

            float qualityCurve = Smooth01(math.saturate((quality - ApexBrainConstants.LowQualityNodeHold) * math.rcp(1f - ApexBrainConstants.LowQualityNodeHold)));
            float updateHz = math.lerp(5f, 60f, qualityCurve);
            uint evaluationsPerWindow = (uint)math.clamp((int)math.round(updateHz), 5, 60);
            uint phase = (frame * evaluationsPerWindow) % 60u;
            return phase < evaluationsPerWindow;
        }

        /// <summary>
        /// Records the last written telemetry frame after the scheduled job is complete.
        /// </summary>
        public static bool TryRecordTelemetryHeartbeat(ApexBrainVaultBuffers buffers, uint frame)
        {
            if (!buffers.TelemetryCursor.IsCreated || buffers.TelemetryCursor.Length <= 0)
                return false;

            buffers.TelemetryCursor[0] = (int)(frame % ApexBrainConstants.TelemetryFrames);
            return true;
        }

        /// <summary>
        /// Records telemetry cursor and dumps black-box data immediately if the completed frame contains a fault row.
        /// </summary>
        public static bool TryRecordTelemetryHeartbeat(ApexBrainVaultBuffers buffers, uint frame, string projectRoot)
        {
            bool recorded = TryRecordTelemetryHeartbeat(buffers, frame);
            if (recorded && !string.IsNullOrEmpty(projectRoot))
                TryDumpBlackBoxOnFrameFault(in buffers, frame, projectRoot);
            return recorded;
        }

        /// <summary>
        /// Attaches external NativeQueue/SignalBus writers without adding a runtime dependency on the Core SignalBus assembly.
        /// </summary>
        public static void AttachSignalWriters(
            ref ApexBrainJob job,
            NativeQueue<ApexProximitySignal>.ParallelWriter proximityWriter,
            NativeQueue<MockCombatDamageSignal>.ParallelWriter combatWriter,
            NativeQueue<GlobalPanicSignal>.ParallelWriter panicWriter)
        {
            job.ProximitySignalWriter = proximityWriter;
            job.CombatDamageSignalWriter = combatWriter;
            job.PanicSignalWriter = panicWriter;
            job.EnableSignalQueueWrites = 1;
        }

        /// <summary>
        /// Reads current tuning from unmanaged vault memory.
        /// </summary>
        public static bool TryGetTuning(IDataVault vault, ref ApexBrainVaultHandles handles, out ApexBrainTuning tuning)
        {
            tuning = default;
            if (!TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            tuning = buffers.Tuning[0];
            return true;
        }

        /// <summary>
        /// Writes current tuning to unmanaged vault memory.
        /// </summary>
        public static bool TrySetTuning(IDataVault vault, ref ApexBrainVaultHandles handles, in ApexBrainTuning tuning)
        {
            if (!TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            buffers.Tuning[0] = SanitizeTuning(in tuning);
            return true;
        }

        /// <summary>
        /// Loads apex_predator_stats.csv into vault scratch and applies zero-allocation key-hash parsing.
        /// </summary>
        public static bool TryLoadCsvOverrides(IDataVault vault, ref ApexBrainVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                buffers.CsvScratch.Length <= 0 ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = TryGetLastWriteTicks(path);
            int length = ReadFileIntoNativeScratch(path, buffers.CsvScratch);
            if (length <= 0)
                return false;

            ApexBrainTuning tuning = buffers.Tuning[0];
            bool changed = TryApplyCsvOverrides(buffers.CsvScratch, length, ref tuning);
            if (changed)
            {
                tuning.LastCsvHash = HashBytes(buffers.CsvScratch, length);
                tuning.CsvReloadVersion++;
                tuning.LastCsvWriteTicks = writeTicks;
                buffers.Tuning[0] = SanitizeTuning(in tuning);
            }

            return changed;
        }

        /// <summary>
        /// Timestamp-gated CSV polling for editor/runtime owners that need human tuning without recompilation.
        /// </summary>
        public static bool TryPollCsvOverrides(IDataVault vault, ref ApexBrainVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = TryGetLastWriteTicks(path);
            if (writeTicks == 0UL || buffers.Tuning[0].LastCsvWriteTicks == writeTicks)
                return false;

            return TryLoadCsvOverrides(vault, ref handles, projectRoot);
        }

        /// <summary>
        /// Parses CSV bytes as key,value rows. The parser hashes ASCII keys and does not allocate.
        /// </summary>
        public static bool TryApplyCsvOverrides(NativeArray<byte> bytes, int length, ref ApexBrainTuning tuning)
        {
            if (!bytes.IsCreated || length <= 0)
                return false;

            bool changed = false;
            int limit = math.min(length, bytes.Length);
            int index = 0;
            while (index < limit)
            {
                SkipWhitespaceAndLineBreaks(bytes, limit, ref index);
                if (index >= limit)
                    break;

                if (bytes[index] == (byte)'#')
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                uint keyHash = 2166136261u;
                int keyLength = 0;
                while (index < limit && bytes[index] != (byte)',' && bytes[index] != (byte)'=' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                {
                    byte c = ToLowerAscii(bytes[index]);
                    if (c > (byte)' ')
                    {
                        keyHash = (keyHash ^ c) * 16777619u;
                        keyLength++;
                    }

                    index++;
                }

                if (index < limit && (bytes[index] == (byte)',' || bytes[index] == (byte)'='))
                    index++;

                if (keyLength > 0 && TryParseFloat(bytes, limit, ref index, out float value))
                    changed |= ApplyCsvValue(keyHash, value, ref tuning);

                SkipLine(bytes, limit, ref index);
            }

            return changed;
        }

        /// <summary>
        /// Writes a binary black-box dump for the last 300 frames.
        /// </summary>
        public static bool TryDumpBlackBox(in ApexBrainVaultBuffers buffers, string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            string root = string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
            string directory = Path.Combine(root, "Docs", "AgentLogs");
            string primary = Path.Combine(directory, DumpFileName);
            string legacy = Path.Combine(directory, LegacyDumpFileName);
            return TryWriteDump(primary, in buffers) & TryWriteDump(legacy, in buffers);
        }

        /// <summary>
        /// Scans one telemetry frame for faults and dumps the ring if needed.
        /// </summary>
        public static bool TryDumpBlackBoxOnFrameFault(in ApexBrainVaultBuffers buffers, uint frame, string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            int frameIndex = (int)(frame % ApexBrainConstants.TelemetryFrames);
            int start = frameIndex * ApexBrainConstants.MaxLeviathans;
            int end = math.min(start + ApexBrainConstants.MaxLeviathans, buffers.TelemetryRing.Length);
            for (int i = start; i < end; i++)
            {
                ApexTelemetryEntry entry = buffers.TelemetryRing[i];
                if (entry.Frame == frame && (entry.Flags & ApexBrainFlags.Fault) != 0)
                    return TryDumpBlackBox(in buffers, projectRoot);
            }

            return false;
        }

        /// <summary>
        /// Validates byte layouts required by the prompt.
        /// </summary>
        public static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<ApexStateDTO>() == 64 &&
                   UnsafeUtility.SizeOf<MockPlayerAUP>() == 128 &&
                   UnsafeUtility.SizeOf<AcousticEchoTap>() == 64 &&
                   UnsafeUtility.SizeOf<MockWorldSampler>() == 64 &&
                   UnsafeUtility.SizeOf<ApexBrainTuning>() == 128 &&
                   UnsafeUtility.SizeOf<ApexEmergencyStats>() == 64 &&
                   UnsafeUtility.SizeOf<ApexInfluenceNode>() == 64 &&
                   UnsafeUtility.SizeOf<ApexBrainOutputDTO>() == 192 &&
                   UnsafeUtility.SizeOf<ApexTelemetryEntry>() == 128 &&
                   UnsafeUtility.SizeOf<ApexProximitySignal>() == 64 &&
                   UnsafeUtility.SizeOf<MockCombatDamageSignal>() == 64 &&
                   UnsafeUtility.SizeOf<GlobalPanicSignal>() == 64;
        }

        public static ApexBrainTuning BuildEmergencyMockTuning()
        {
            return ApexBrainDefaults.BuildEmergencyMockTuning();
        }

        public static ApexEmergencyStats BuildEmergencyMockStats()
        {
            return ApexBrainDefaults.BuildEmergencyMockStats();
        }

        public static MockWorldSampler BuildEmergencyMockWorldSampler()
        {
            return ApexBrainDefaults.BuildEmergencyMockWorldSampler();
        }

        private static bool TryResolveExisting(IDataVault vault, out ApexBrainVaultHandles handles)
        {
            handles = default;
            return vault.TryGetBufferHandle(ApexBrainVaultBufferIds.ApexState, out handles.States) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.MockPlayerAup, out handles.MockTargets) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.AcousticEchoTap, out handles.AcousticTaps) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.Tuning, out handles.Tuning) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.EmergencyStats, out handles.EmergencyStats) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.MockWorldSampler, out handles.WorldSampler) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.Output, out handles.Outputs) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.ProximitySignal, out handles.ProximitySignals) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.CombatDamageSignal, out handles.CombatDamageSignals) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.PanicSignal, out handles.PanicSignals) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.InfluenceNodes, out handles.InfluenceNodes) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.AmbushNodeScratch, out handles.AmbushNodeScratch) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.TelemetryRing, out handles.TelemetryRing) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                   vault.TryGetBufferHandle(ApexBrainVaultBufferIds.CsvScratch, out handles.CsvScratch);
        }

        private static int GetScheduleLength(in ApexBrainVaultBuffers buffers)
        {
            if (!buffers.IsCreated())
                return 0;

            int length = math.min(buffers.States.Length, buffers.MockTargets.Length);
            length = math.min(length, buffers.Outputs.Length);
            length = math.min(length, buffers.ProximitySignals.Length);
            length = math.min(length, buffers.CombatDamageSignals.Length);
            length = math.min(length, buffers.PanicSignals.Length);
            length = math.min(length, buffers.AmbushNodeScratch.Length / ApexBrainConstants.MaxAmbushNodes);
            length = math.min(length, ApexBrainConstants.MaxLeviathans);
            return math.max(0, length);
        }

        private static void GenerateEmergencyMockApexStats(ApexBrainVaultBuffers buffers)
        {
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                ApexBrainTuning tuning = buffers.Tuning[0];
                if (!math.isfinite(tuning.LeviathanSpeed) || tuning.LeviathanSpeed <= 0f)
                {
                    ClearRuntimeRows(in buffers);
                    buffers.Tuning[0] = BuildEmergencyMockTuning();
                }
            }

            if (buffers.EmergencyStats.IsCreated && buffers.EmergencyStats.Length > 0)
                buffers.EmergencyStats[0] = BuildEmergencyMockStats();
            if (buffers.WorldSampler.IsCreated && buffers.WorldSampler.Length > 0)
                buffers.WorldSampler[0] = BuildEmergencyMockWorldSampler();
        }

        private static unsafe void ClearRuntimeRows(in ApexBrainVaultBuffers buffers)
        {
            MemClearArray(buffers.States);
            MemClearArray(buffers.MockTargets);
            MemClearArray(buffers.AcousticTaps);
            MemClearArray(buffers.Outputs);
            MemClearArray(buffers.ProximitySignals);
            MemClearArray(buffers.CombatDamageSignals);
            MemClearArray(buffers.PanicSignals);
            MemClearArray(buffers.InfluenceNodes);
            MemClearArray(buffers.AmbushNodeScratch);
            MemClearArray(buffers.TelemetryRing);
            MemClearArray(buffers.TelemetryCursor);
            MemClearArray(buffers.CsvScratch);
        }

        private static unsafe void MemClearArray<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            long byteCount = (long)UnsafeUtility.SizeOf<T>() * array.Length;
            UnsafeUtility.MemClear(ptr, byteCount);
        }

        private static ApexBrainTuning SanitizeTuning(in ApexBrainTuning input)
        {
            ApexBrainTuning fallback = BuildEmergencyMockTuning();
            ApexBrainTuning tuning = input;
            tuning.AggressionMultiplier = SanitizePositive(tuning.AggressionMultiplier, fallback.AggressionMultiplier);
            tuning.AcousticSensitivity = SanitizePositive(tuning.AcousticSensitivity, fallback.AcousticSensitivity);
            tuning.TurnRate = SanitizePositive(tuning.TurnRate, fallback.TurnRate);
            tuning.StalkingDistance = SanitizePositive(tuning.StalkingDistance, fallback.StalkingDistance);
            tuning.LeviathanSpeed = SanitizePositive(tuning.LeviathanSpeed, fallback.LeviathanSpeed);
            tuning.TerrorRadius = SanitizePositive(tuning.TerrorRadius, fallback.TerrorRadius);
            tuning.BaseDamageMagnitude = SanitizePositive(tuning.BaseDamageMagnitude, fallback.BaseDamageMagnitude);
            tuning.BiomeAggressionMultiplier = SanitizePositive(tuning.BiomeAggressionMultiplier, fallback.BiomeAggressionMultiplier);
            tuning.GlobalQualityWeight = math.saturate(math.select(fallback.GlobalQualityWeight, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            tuning.SimulationTickDelta = SanitizePositive(tuning.SimulationTickDelta, fallback.SimulationTickDelta);
            tuning.StrikeDistance = SanitizePositive(tuning.StrikeDistance, fallback.StrikeDistance);
            tuning.HeadOffsetMeters = SanitizePositive(tuning.HeadOffsetMeters, fallback.HeadOffsetMeters);
            tuning.MidOffsetMeters = SanitizePositive(tuning.MidOffsetMeters, fallback.MidOffsetMeters);
            tuning.TailOffsetMeters = SanitizePositive(tuning.TailOffsetMeters, fallback.TailOffsetMeters);
            tuning.NoiseAggroGain = SanitizePositive(tuning.NoiseAggroGain, fallback.NoiseAggroGain);
            tuning.StaminaRecoveryPerSecond = SanitizePositive(tuning.StaminaRecoveryPerSecond, fallback.StaminaRecoveryPerSecond);
            tuning.StaminaStrikeCost = math.saturate(math.select(fallback.StaminaStrikeCost, tuning.StaminaStrikeCost, math.isfinite(tuning.StaminaStrikeCost)));
            tuning.SweetLieShadowGain = SanitizePositive(tuning.SweetLieShadowGain, fallback.SweetLieShadowGain);
            tuning.SweetLieViewDotThreshold = math.saturate(math.select(fallback.SweetLieViewDotThreshold, tuning.SweetLieViewDotThreshold, math.isfinite(tuning.SweetLieViewDotThreshold)));
            tuning.AmbushNodeRadiusMeters = SanitizePositive(tuning.AmbushNodeRadiusMeters, fallback.AmbushNodeRadiusMeters);
            tuning.VisualOverkillGain = SanitizePositive(tuning.VisualOverkillGain, fallback.VisualOverkillGain);
            tuning.BiteHeadLocalOffset = SanitizePositive(tuning.BiteHeadLocalOffset, fallback.BiteHeadLocalOffset);
            if (tuning.PreferredBiomeHash == 0u)
                tuning.PreferredBiomeHash = fallback.PreferredBiomeHash;
            if (tuning.SourceHash == 0u)
                tuning.SourceHash = fallback.SourceHash;
            return tuning;
        }

        private static bool ApplyCsvValue(uint keyHash, float value, ref ApexBrainTuning tuning)
        {
            if (!math.isfinite(value))
                return false;

            if (keyHash == _aggressionMultiplierHash)
                tuning.AggressionMultiplier = value;
            else if (keyHash == _acousticSensitivityHash)
                tuning.AcousticSensitivity = value;
            else if (keyHash == _turnRateHash)
                tuning.TurnRate = value;
            else if (keyHash == _stalkingDistanceHash)
                tuning.StalkingDistance = value;
            else if (keyHash == _leviathanSpeedHash)
                tuning.LeviathanSpeed = value;
            else if (keyHash == _terrorRadiusHash)
                tuning.TerrorRadius = value;
            else if (keyHash == _biomeAggressionHash)
                tuning.BiomeAggressionMultiplier = value;
            else if (keyHash == _strikeDistanceHash)
                tuning.StrikeDistance = value;
            else if (keyHash == _globalQualityHash)
                tuning.GlobalQualityWeight = math.saturate(value);
            else
                return false;

            return true;
        }

        private static string ResolveCsvPath(string projectRoot)
        {
            string root = string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
            string rootPath = Path.Combine(root, CsvFileName);
            if (File.Exists(rootPath))
                return rootPath;

            string dataPath = Path.Combine(root, "Data", "AI", CsvFileName);
            if (File.Exists(dataPath))
                return dataPath;

            return Path.Combine(root, "Assets", "StreamingAssets", CsvFileName);
        }

        private static int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int total = 0;
                    int max = scratch.Length;
                    while (total < max)
                    {
                        int value = stream.ReadByte();
                        if (value < 0)
                            break;
                        scratch[total++] = (byte)value;
                    }

                    return total;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (NotSupportedException)
            {
                return 0;
            }
        }

        private static bool TryParseFloat(NativeArray<byte> bytes, int limit, ref int index, out float value)
        {
            value = 0f;
            SkipSpaces(bytes, limit, ref index);
            if (index >= limit)
                return false;

            float sign = 1f;
            if (bytes[index] == (byte)'-' || bytes[index] == (byte)'+')
            {
                sign = bytes[index] == (byte)'-' ? -1f : 1f;
                index++;
            }

            float integer = 0f;
            int digitCount = 0;
            while (index < limit && IsDigit(bytes[index]))
            {
                integer = (integer * 10f) + (bytes[index] - (byte)'0');
                index++;
                digitCount++;
            }

            float fraction = 0f;
            float place = 0.1f;
            if (index < limit && bytes[index] == (byte)'.')
            {
                index++;
                while (index < limit && IsDigit(bytes[index]))
                {
                    fraction += (bytes[index] - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                    digitCount++;
                }
            }

            value = (integer + fraction) * sign;
            return digitCount > 0 && math.isfinite(value);
        }

        private static void SkipWhitespaceAndLineBreaks(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && bytes[index] <= (byte)' ')
                index++;
        }

        private static void SkipSpaces(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                index++;
        }

        private static void SkipLine(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;
            while (index < limit && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static byte ToLowerAscii(byte c)
        {
            return c >= (byte)'A' && c <= (byte)'Z' ? (byte)(c + 32) : c;
        }

        private static bool IsDigit(byte c)
        {
            return c >= (byte)'0' && c <= (byte)'9';
        }

        private static uint HashAscii(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ ToLowerAscii((byte)value[i])) * 16777619u;
            return hash;
        }

        private static uint HashBytes(NativeArray<byte> bytes, int length)
        {
            uint hash = 2166136261u;
            int limit = math.min(length, bytes.IsCreated ? bytes.Length : 0);
            for (int i = 0; i < limit; i++)
                hash = (hash ^ bytes[i]) * 16777619u;
            return hash;
        }

        private static ulong TryGetLastWriteTicks(string path)
        {
            try
            {
                return (ulong)File.GetLastWriteTimeUtc(path).Ticks;
            }
            catch (IOException)
            {
                return 0UL;
            }
            catch (UnauthorizedAccessException)
            {
                return 0UL;
            }
            catch (ArgumentException)
            {
                return 0UL;
            }
            catch (NotSupportedException)
            {
                return 0UL;
            }
        }

        private static float ResolveSchedulingQuality(float value)
        {
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }

        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - (2f * value));
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value) & value > ApexBrainConstants.Epsilon);
        }

        private static bool TryWriteDump(string path, in ApexBrainVaultBuffers buffers)
        {
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
                    writer.Write(ToLittleEndianMarker(DumpEndianMarker));
                    writer.Write(DumpVersion);
                    writer.Write(ApexBrainConstants.TelemetryFrames);
                    writer.Write(ApexBrainConstants.MaxLeviathans);
                    writer.Write(buffers.TelemetryRing.Length);
                    int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0 ? buffers.TelemetryCursor[0] : 0;
                    writer.Write(cursor);
                    for (int i = 0; i < buffers.TelemetryRing.Length; i++)
                    {
                        ApexTelemetryEntry entry = buffers.TelemetryRing[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.SpatialHash);
                        writer.Write(entry.AcousticMemoryHash);
                        writer.Write(entry.InterceptLocal.x);
                        writer.Write(entry.InterceptLocal.y);
                        writer.Write(entry.InterceptLocal.z);
                        writer.Write(entry.AggressionLevel);
                        writer.Write(entry.DesiredVelocity.x);
                        writer.Write(entry.DesiredVelocity.y);
                        writer.Write(entry.DesiredVelocity.z);
                        writer.Write(entry.SweetLieLos01);
                        writer.Write(entry.WallRepulsion.x);
                        writer.Write(entry.WallRepulsion.y);
                        writer.Write(entry.WallRepulsion.z);
                        writer.Write(entry.StrikeUtility);
                        writer.Write(entry.UtilityScores.x);
                        writer.Write(entry.UtilityScores.y);
                        writer.Write(entry.UtilityScores.z);
                        writer.Write(entry.UtilityScores.w);
                        writer.Write(entry.TargetHash);
                        writer.Write(entry.BiomeHash);
                        writer.Write(entry.EvaluatedNodeCount);
                        writer.Write(entry.GlobalQualityWeight);
                        writer.Write(entry.ActiveLeviathans);
                        writer.Write(entry.InterceptComputeTimeMs);
                        writer.Write(entry.Slot);
                        writer.Write(entry.Phase);
                        writer.Write(entry.Flags);
                        writer.Write(entry.FaultCode);
                    }
                }

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

        private static uint ToLittleEndianMarker(uint value)
        {
            return BitConverter.IsLittleEndian ? value : ReverseBytes(value);
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }
    }
}
