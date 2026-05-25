using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using System.Reflection;
#endif
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    /// <summary>
    /// Stable capacities and quantization constants for the SHINOBU_340 drainage solver.
    /// </summary>
    public static class SumpPumpPipeGridConstants
    {
        public const int MaxPumpNodes = 2000;
        public const int MaxPipeEdges = 6000;
        public const int TelemetryFrameCount = 300;
        public const int MaxPipeProfiles = 128;
        public const int CsvScratchBytes = 16 * 1024;
        public const int CounterFrameDrainedMilliM3 = 0;
        public const int CounterActivePumps = 1;
        public const int CounterNanCount = 2;
        public const int CounterPowerMilliWatts = 3;
        public const int CounterMassErrorMilliM3 = 4;
        public const int CounterNodeCount = 5;
        public const int CounterEdgeCount = 6;
        public const int CounterValidCsrEdges = 7;
        public const int CounterDeltaPassCount = 8;
        public const int CounterTopologyVersion = 9;
        public const int CounterCount = 16;
        public const float DefaultBasePipeConductance = 0.08f;
        public const float DefaultMaxPumpRateM3PerSecond = 0.36f;
        public const float DefaultPumpPowerDrawWatts = 180f;
        public const float DefaultDeltaSmoothingFactor = 0.82f;
        public const float DefaultVisualFlowGain = 3.5f;
        public const float DefaultMassQuantumM3 = 0.001f;
        public const float DefaultGravityAssistScalar = 1.5f;
        public const float DefaultGravityResistanceScalar = 0.5f;
        public const int MaxQuantizedDrainUnitsPerPump = 1 << 24;
        public const double AupCellSizeMeters = Hecton8.Core.Contracts.HectonPhysicsContract.AupSectorSizeMetersDouble;
        public const uint FnvOffset = 2166136261u;
        public const uint FnvPrime = 16777619u;
    }

    /// <summary>
    /// SHINOBU_340 owner-local Vault buffer IDs. Kept out of the central BufferID enum to avoid compile-wall churn.
    /// </summary>
    public static class SumpPumpDrainageBufferIds
    {
        public const BufferID PumpNodes = (BufferID)95820;
        public const BufferID PipeEdges = (BufferID)95821;
        public const BufferID NodeAup = (BufferID)95822;
        public const BufferID PumpRoomIndices = (BufferID)95823;
        public const BufferID CsrOffsets = (BufferID)95824;
        public const BufferID CsrDestinations = (BufferID)95825;
        public const BufferID CsrConductance = (BufferID)95826;
        public const BufferID CsrFlow = (BufferID)95827;
        public const BufferID CsrFlatEdgeIndex = (BufferID)95828;
        public const BufferID CsrWriteCursor = (BufferID)95829;
        public const BufferID PressureFront = (BufferID)95830;
        public const BufferID PressureBack = (BufferID)95831;
        public const BufferID PowerPotential = (BufferID)95832;
        public const BufferID PumpRemainder = (BufferID)95833;
        public const BufferID Tuning = (BufferID)95834;
        public const BufferID TelemetryRing = (BufferID)95835;
        public const BufferID TelemetryCursor = (BufferID)95836;
        public const BufferID Counters = (BufferID)95837;
        public const BufferID PipeProfiles = (BufferID)95838;
        public const BufferID CsvScratch = (BufferID)95839;
        public const BufferID FrameSummary = (BufferID)95840;
        public const BufferID FlowGpu = (BufferID)95841;
        public const BufferID PumpMassError = (BufferID)95842;
        public const BufferID RoomDrainLocks = (BufferID)95843;
        public const BufferID PumpBaseMaxRate = (BufferID)95844;
        public const BufferID PumpPowerNodeHashes = (BufferID)95845;
    }

    /// <summary>
    /// Pump-node flags consumed directly by Burst jobs.
    /// </summary>
    public static class SumpPumpNodeFlags
    {
        public const uint Active = 1u << 0;
        public const uint Pump = 1u << 1;
        public const uint Mock = 1u << 2;
        public const uint PowerStarved = 1u << 3;
        public const uint NonFinite = 1u << 31;
    }

    /// <summary>
    /// Pipe-edge flags consumed directly by Burst jobs.
    /// </summary>
    public static class SumpPipeEdgeFlags
    {
        public const uint Active = 1u << 0;
        public const uint Sealed = 1u << 1;
        public const uint Mock = 1u << 2;
        public const uint DownhillBoosted = 1u << 3;
        public const uint NonFinite = 1u << 31;
    }

    /// <summary>
    /// Drainage telemetry flags written into the fixed 300-frame ring.
    /// </summary>
    public static class SumpDrainageTelemetryFlags
    {
        public const uint None = 0u;
        public const uint NonFinite = 1u << 0;
        public const uint MissingFluidVault = 1u << 1;
        public const uint MissingPowerVault = 1u << 2;
        public const uint DumpedBlackBox = 1u << 3;
        public const uint TopologyInvalid = 1u << 4;
        public const uint SolverOverBudget = 1u << 5;
        public const uint ScheduleWindowTiming = 1u << 6;
        public const uint HeartbeatFrame = 1u << 7;
    }

    /// <summary>ARM64-aligned drainage node state consumed directly by Burst jobs. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DrainageNodeDTO
    {
        [FieldOffset(0)] public uint NodeHashID;
        [FieldOffset(4)] public float HydraulicPressure;
        [FieldOffset(8)] public float MaxPumpRate;
        [FieldOffset(12)] public float CurrentFlow;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private uint _pad1;
        [FieldOffset(28)] private uint _pad2;
    }

    /// <summary>
    /// Flat pipe connection input and per-edge output. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PipeEdgeDTO
    {
        [FieldOffset(0)] public int SourceNodeIndex;
        [FieldOffset(4)] public int DestinationNodeIndex;
        [FieldOffset(8)] public float Conductance;
        [FieldOffset(12)] public float CurrentFlow;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint EdgeHash;
        [FieldOffset(24)] public uint SourceNodeHash;
        [FieldOffset(28)] public uint DestinationNodeHash;
    }

    /// <summary>
    /// Cold CSV profile row mapped by FNV-1a name hash. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PipeProfileDTO
    {
        [FieldOffset(0)] public uint NameHash;
        [FieldOffset(4)] public float PipeConductance;
        [FieldOffset(8)] public float PumpRateM3PerSecond;
        [FieldOffset(12)] public float PumpPowerDrawWatts;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint Reserved0;
        [FieldOffset(24)] public uint Reserved1;
        [FieldOffset(28)] public uint Reserved2;
    }

    /// <summary>
    /// Vault-backed drainage tuning. Size: 80 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct DrainageTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float BasePipeConductance;
        [FieldOffset(8)] public float PumpPowerDraw;
        [FieldOffset(12)] public float DeltaSmoothingFactor;
        [FieldOffset(16)] public float DeltaTimeSeconds;
        [FieldOffset(20)] public float LastEvacuatedM3;
        [FieldOffset(24)] public float MaxPumpRateScale;
        [FieldOffset(28)] public float VisualFlowGain;
        [FieldOffset(32)] public float MassQuantumM3;
        [FieldOffset(36)] public uint FrameIndex;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public ushort NodeCount;
        [FieldOffset(46)] public ushort EdgeCount;
        [FieldOffset(48)] public ushort DeltaPassCount;
        [FieldOffset(50)] public ushort ActivePumpCount;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float MaxPumpThroughputM3PerSecond;
        [FieldOffset(60)] public float GravityAssistScalar;
        [FieldOffset(64)] public float GravityResistanceScalar;
        [FieldOffset(68)] public uint Reserved0;
        [FieldOffset(72)] public uint Reserved1;
        [FieldOffset(76)] public uint Reserved2;
    }

    /// <summary>
    /// Fixed-size Black Box drainage entry. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DrainageTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public float FrameEvacuatedM3;
        [FieldOffset(12)] public float TotalEvacuatedM3;
        [FieldOffset(16)] public float AveragePressure;
        [FieldOffset(20)] public float MaxPressure;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float TotalPowerDrawWatts;
        [FieldOffset(32)] public uint ActivePumpCount;
        [FieldOffset(36)] public uint NanCount;
        [FieldOffset(40)] public uint SolverWallMicroseconds;
        [FieldOffset(44)] public uint NodeCount;
        [FieldOffset(48)] public uint EdgeCount;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint ConservativeMassErrorMilli;
        [FieldOffset(60)] public uint Reserved0;
    }

    /// <summary>
    /// Raw drainage dump header. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DrainageDumpHeader
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public uint EntryCount;
        [FieldOffset(12)] public uint StructSizeBytes;
        [FieldOffset(16)] public uint Version;
        [FieldOffset(20)] public uint Capacity;
        [FieldOffset(24)] public uint WriteCount;
        [FieldOffset(28)] public uint OldestIndex;
        [FieldOffset(32)] public uint RuntimeHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public ulong Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    /// <summary>
    /// GPU-facing edge flow scalar for shader panning. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct DrainagePipeFlowGpuDTO
    {
        [FieldOffset(0)] public float Flow01;
        [FieldOffset(4)] public float PressureDelta01;
        [FieldOffset(8)] public uint EdgeHash;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Per-room lock row padded to a full L1 cache line for parallel pump drains. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DrainageRoomDrainLock64
    {
        [FieldOffset(0)] public int LockState;
        [FieldOffset(4)] public int Reserved0;
        [FieldOffset(8)] public ulong Pad0;
        [FieldOffset(16)] public ulong Pad1;
        [FieldOffset(24)] public ulong Pad2;
        [FieldOffset(32)] public ulong Pad3;
        [FieldOffset(40)] public ulong Pad4;
        [FieldOffset(48)] public ulong Pad5;
        [FieldOffset(56)] public ulong Pad6;
    }

    /// <summary>
    /// Cold layout and CSV validation utilities.
    /// </summary>
    public static class SumpPumpPipeGridValidation
    {
        public static bool ValidateDrainageNodeLayout()
        {
            if (UnsafeUtility.SizeOf<DrainageNodeDTO>() != 32)
                return false;

#if UNITY_EDITOR
            return OffsetOf<DrainageNodeDTO>(nameof(DrainageNodeDTO.NodeHashID)) == 0 &&
                   OffsetOf<DrainageNodeDTO>(nameof(DrainageNodeDTO.HydraulicPressure)) == 4 &&
                   OffsetOf<DrainageNodeDTO>(nameof(DrainageNodeDTO.MaxPumpRate)) == 8 &&
                   OffsetOf<DrainageNodeDTO>(nameof(DrainageNodeDTO.CurrentFlow)) == 12 &&
                   OffsetOf<DrainageNodeDTO>(nameof(DrainageNodeDTO.Flags)) == 16 &&
                   OffsetOf<DrainageNodeDTO>("_pad0") == 20 &&
                   OffsetOf<DrainageNodeDTO>("_pad1") == 24 &&
                   OffsetOf<DrainageNodeDTO>("_pad2") == 28;
#else
            return true;
#endif
        }

        public static bool ValidatePumpNodeLayout()
        {
            return ValidateDrainageNodeLayout();
        }

        public static bool ValidatePipeEdgeLayout()
        {
            if (UnsafeUtility.SizeOf<PipeEdgeDTO>() != 32)
                return false;

#if UNITY_EDITOR
            return
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.SourceNodeIndex)) == 0 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.DestinationNodeIndex)) == 4 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.Conductance)) == 8 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.CurrentFlow)) == 12 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.Flags)) == 16 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.EdgeHash)) == 20 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.SourceNodeHash)) == 24 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.DestinationNodeHash)) == 28;
#else
            return true;
#endif
        }

        public static bool ValidateRoomDrainLockLayout()
        {
            if (UnsafeUtility.SizeOf<DrainageRoomDrainLock64>() != 64)
                return false;

#if UNITY_EDITOR
            return
                   OffsetOf<DrainageRoomDrainLock64>(nameof(DrainageRoomDrainLock64.LockState)) == 0 &&
                   OffsetOf<DrainageRoomDrainLock64>(nameof(DrainageRoomDrainLock64.Pad0)) == 8 &&
                   OffsetOf<DrainageRoomDrainLock64>(nameof(DrainageRoomDrainLock64.Pad6)) == 56;
#else
            return true;
#endif
        }

        public static bool ValidateDrainageTuningLayout()
        {
            if (UnsafeUtility.SizeOf<DrainageTuningDTO>() != 80)
                return false;

#if UNITY_EDITOR
            return
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.GlobalQualityWeight)) == 0 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.BasePipeConductance)) == 4 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.PumpPowerDraw)) == 8 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.DeltaSmoothingFactor)) == 12 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.DeltaTimeSeconds)) == 16 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.LastEvacuatedM3)) == 20 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.MaxPumpRateScale)) == 24 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.VisualFlowGain)) == 28 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.MassQuantumM3)) == 32 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.FrameIndex)) == 36 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.StateHash)) == 40 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.NodeCount)) == 44 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.EdgeCount)) == 46 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.DeltaPassCount)) == 48 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.ActivePumpCount)) == 50 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.Flags)) == 52 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.MaxPumpThroughputM3PerSecond)) == 56 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.GravityAssistScalar)) == 60 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.GravityResistanceScalar)) == 64 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.Reserved0)) == 68 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.Reserved1)) == 72 &&
                   OffsetOf<DrainageTuningDTO>(nameof(DrainageTuningDTO.Reserved2)) == 76;
#else
            return true;
#endif
        }

        public static bool ValidateDrainageDumpHeaderLayout()
        {
            if (UnsafeUtility.SizeOf<DrainageDumpHeader>() != 64)
                return false;

#if UNITY_EDITOR
            return
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.Magic)) == 0 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.EntryCount)) == 8 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.StructSizeBytes)) == 12 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.Version)) == 16 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.Capacity)) == 20 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.WriteCount)) == 24 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.OldestIndex)) == 28 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.RuntimeHash)) == 32 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.Flags)) == 36 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.Reserved0)) == 40 &&
                   OffsetOf<DrainageDumpHeader>(nameof(DrainageDumpHeader.Reserved2)) == 56;
#else
            return true;
#endif
        }

        public static bool ValidatePipeProfileLayout()
        {
            if (UnsafeUtility.SizeOf<PipeProfileDTO>() != 32)
                return false;

#if UNITY_EDITOR
            return OffsetOf<PipeProfileDTO>(nameof(PipeProfileDTO.NameHash)) == 0 &&
                   OffsetOf<PipeProfileDTO>(nameof(PipeProfileDTO.PipeConductance)) == 4 &&
                   OffsetOf<PipeProfileDTO>(nameof(PipeProfileDTO.PumpRateM3PerSecond)) == 8 &&
                   OffsetOf<PipeProfileDTO>(nameof(PipeProfileDTO.PumpPowerDrawWatts)) == 12 &&
                   OffsetOf<PipeProfileDTO>(nameof(PipeProfileDTO.Flags)) == 16 &&
                   OffsetOf<PipeProfileDTO>(nameof(PipeProfileDTO.Reserved0)) == 20 &&
                   OffsetOf<PipeProfileDTO>(nameof(PipeProfileDTO.Reserved1)) == 24 &&
                   OffsetOf<PipeProfileDTO>(nameof(PipeProfileDTO.Reserved2)) == 28;
#else
            return true;
#endif
        }

        public static bool ValidateDrainageTelemetryLayout()
        {
            if (UnsafeUtility.SizeOf<DrainageTelemetryEntry>() != 64)
                return false;

#if UNITY_EDITOR
            return OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.StateHash)) == 4 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.FrameEvacuatedM3)) == 8 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.TotalEvacuatedM3)) == 12 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.AveragePressure)) == 16 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.MaxPressure)) == 20 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.GlobalQualityWeight)) == 24 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.TotalPowerDrawWatts)) == 28 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.ActivePumpCount)) == 32 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.NanCount)) == 36 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.SolverWallMicroseconds)) == 40 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.NodeCount)) == 44 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.EdgeCount)) == 48 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.Flags)) == 52 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.ConservativeMassErrorMilli)) == 56 &&
                   OffsetOf<DrainageTelemetryEntry>(nameof(DrainageTelemetryEntry.Reserved0)) == 60;
#else
            return true;
#endif
        }

        public static bool ValidateDrainagePipeFlowGpuLayout()
        {
            if (UnsafeUtility.SizeOf<DrainagePipeFlowGpuDTO>() != 16)
                return false;

#if UNITY_EDITOR
            return OffsetOf<DrainagePipeFlowGpuDTO>(nameof(DrainagePipeFlowGpuDTO.Flow01)) == 0 &&
                   OffsetOf<DrainagePipeFlowGpuDTO>(nameof(DrainagePipeFlowGpuDTO.PressureDelta01)) == 4 &&
                   OffsetOf<DrainagePipeFlowGpuDTO>(nameof(DrainagePipeFlowGpuDTO.EdgeHash)) == 8 &&
                   OffsetOf<DrainagePipeFlowGpuDTO>(nameof(DrainagePipeFlowGpuDTO.Flags)) == 12;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        public static bool TryParsePipeProfilesCsv(ReadOnlySpan<byte> csv, NativeArray<PipeProfileDTO> profiles, out int profileCount)
        {
            profileCount = 0;
            if (csv.Length <= 0 || !profiles.IsCreated || profiles.Length <= 0)
                return false;

            int cursor = 0;
            SkipLine(csv, ref cursor);
            while (cursor < csv.Length && profileCount < profiles.Length)
            {
                uint nameHash = ParseNameHash(csv, ref cursor);
                if (cursor < csv.Length && csv[cursor] == (byte)',')
                    cursor++;
                bool hasConductance = TryParseFloat(csv, ref cursor, out float conductance);
                if (cursor < csv.Length && csv[cursor] == (byte)',')
                    cursor++;
                bool hasRate = TryParseFloat(csv, ref cursor, out float pumpRate);
                if (cursor < csv.Length && csv[cursor] == (byte)',')
                    cursor++;
                bool hasPower = TryParseFloat(csv, ref cursor, out float powerDraw);
                SkipLine(csv, ref cursor);

                if (nameHash == 0u || !hasConductance || !hasRate || !hasPower)
                    continue;

                profiles[profileCount++] = new PipeProfileDTO
                {
                    NameHash = nameHash,
                    PipeConductance = math.max(0f, conductance),
                    PumpRateM3PerSecond = math.max(0f, pumpRate),
                    PumpPowerDrawWatts = math.max(0f, powerDraw),
                    Flags = 1u
                };
            }

            return profileCount > 0;
        }

        public static bool TryParsePipeProfilesCsv(ReadOnlySpan<byte> csv, Span<PipeProfileDTO> profiles, out int profileCount)
        {
            profileCount = 0;
            if (csv.Length <= 0 || profiles.Length <= 0)
                return false;

            int cursor = 0;
            SkipLine(csv, ref cursor);
            while (cursor < csv.Length && profileCount < profiles.Length)
            {
                uint nameHash = ParseNameHash(csv, ref cursor);
                if (cursor < csv.Length && csv[cursor] == (byte)',')
                    cursor++;
                bool hasConductance = TryParseFloat(csv, ref cursor, out float conductance);
                if (cursor < csv.Length && csv[cursor] == (byte)',')
                    cursor++;
                bool hasRate = TryParseFloat(csv, ref cursor, out float pumpRate);
                if (cursor < csv.Length && csv[cursor] == (byte)',')
                    cursor++;
                bool hasPower = TryParseFloat(csv, ref cursor, out float powerDraw);
                SkipLine(csv, ref cursor);

                if (nameHash == 0u || !hasConductance || !hasRate || !hasPower)
                    continue;

                profiles[profileCount++] = new PipeProfileDTO
                {
                    NameHash = nameHash,
                    PipeConductance = math.max(0f, conductance),
                    PumpRateM3PerSecond = math.max(0f, pumpRate),
                    PumpPowerDrawWatts = math.max(0f, powerDraw),
                    Flags = 1u
                };
            }

            return profileCount > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MixHash(uint hash, uint value)
        {
            return (hash ^ value) * SumpPumpPipeGridConstants.FnvPrime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

#if UNITY_EDITOR
        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif

        private static uint ParseNameHash(ReadOnlySpan<byte> csv, ref int cursor)
        {
            uint hash = SumpPumpPipeGridConstants.FnvOffset;
            bool any = false;
            while (cursor < csv.Length)
            {
                byte b = csv[cursor];
                if (b == (byte)',' || b == (byte)'\n' || b == (byte)'\r')
                    break;

                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b > (byte)' ')
                {
                    hash = (hash ^ b) * SumpPumpPipeGridConstants.FnvPrime;
                    any = true;
                }
                cursor++;
            }

            return any ? hash : 0u;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> csv, ref int cursor, out float value)
        {
            value = 0f;
            while (cursor < csv.Length && csv[cursor] == (byte)' ')
                cursor++;

            float sign = 1f;
            if (cursor < csv.Length && csv[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            bool any = false;
            float whole = 0f;
            while (cursor < csv.Length)
            {
                byte b = csv[cursor];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                whole = (whole * 10f) + (b - (byte)'0');
                any = true;
                cursor++;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (cursor < csv.Length && csv[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < csv.Length)
                {
                    byte b = csv[cursor];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    fraction += (b - (byte)'0') * scale;
                    scale *= 0.1f;
                    any = true;
                    cursor++;
                }
            }

            while (cursor < csv.Length && csv[cursor] != (byte)',' && csv[cursor] != (byte)'\n' && csv[cursor] != (byte)'\r')
                cursor++;

            value = (whole + fraction) * sign;
            return any && math.isfinite(value);
        }

        private static void SkipLine(ReadOnlySpan<byte> csv, ref int cursor)
        {
            while (cursor < csv.Length && csv[cursor] != (byte)'\n')
                cursor++;
            if (cursor < csv.Length && csv[cursor] == (byte)'\n')
                cursor++;
        }
#endif
    }
}
