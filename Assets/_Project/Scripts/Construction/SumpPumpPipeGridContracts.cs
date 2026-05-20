using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    /// <summary>
    /// Stable capacities and quantization constants for the SHINOBU_222 drainage solver.
    /// </summary>
    public static class SumpPumpPipeGridConstants
    {
        public const int MaxPumpNodes = 1000;
        public const int MaxPipeEdges = 2500;
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
        public const int CounterSolverIterations = 8;
        public const int CounterTopologyVersion = 9;
        public const int CounterCount = 16;
        public const float DefaultBasePipeConductance = 0.08f;
        public const float DefaultMaxPumpRateM3PerSecond = 0.36f;
        public const float DefaultPumpPowerDrawWatts = 180f;
        public const float DefaultJacobiSmoothingFactor = 0.82f;
        public const float DefaultVisualFlowGain = 3.5f;
        public const float DefaultMassQuantumM3 = 0.001f;
        public const int MaxQuantizedDrainUnitsPerPump = 1 << 24;
        public const double AupCellSizeMeters = Hecton8.Core.Contracts.HectonPhysicsContract.AupSectorSizeMetersDouble;
        public const uint FnvOffset = 2166136261u;
        public const uint FnvPrime = 16777619u;
    }

    /// <summary>
    /// SHINOBU_222 owner-local Vault buffer IDs. Kept out of the central BufferID enum to avoid compile-wall churn.
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
    }

    /// <summary>
    /// ARM64-aligned pump node state. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PumpNodeDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float IngressRate;
        [FieldOffset(8)] public float MaxPumpRate;
        [FieldOffset(12)] public float CurrentEvacuationRate;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float PowerDraw;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }

    /// <summary>
    /// Flat pipe connection input and per-edge output. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PipeEdgeDTO
    {
        [FieldOffset(0)] public int SourceNodeIndex;
        [FieldOffset(4)] public int DestinationNodeIndex;
        [FieldOffset(8)] public float Conductance;
        [FieldOffset(12)] public float CurrentFlow;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float PowerPotential;
        [FieldOffset(24)] public float FractionalRemainderM3;
        [FieldOffset(28)] public float DownhillScalar;
        [FieldOffset(32)] public uint EdgeHash;
        [FieldOffset(36)] public uint SourceNodeHash;
        [FieldOffset(40)] public uint DestinationNodeHash;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
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
    /// Vault-backed drainage tuning. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DrainageTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float BasePipeConductance;
        [FieldOffset(8)] public float PumpPowerDraw;
        [FieldOffset(12)] public float JacobiSmoothingFactor;
        [FieldOffset(16)] public float DeltaTimeSeconds;
        [FieldOffset(20)] public float LastEvacuatedM3;
        [FieldOffset(24)] public float MaxPumpRateScale;
        [FieldOffset(28)] public float VisualFlowGain;
        [FieldOffset(32)] public float MassQuantumM3;
        [FieldOffset(36)] public uint FrameIndex;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public ushort NodeCount;
        [FieldOffset(46)] public ushort EdgeCount;
        [FieldOffset(48)] public ushort SolverIterations;
        [FieldOffset(50)] public ushort ActivePumpCount;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
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
        public static bool ValidatePumpNodeLayout()
        {
            return UnsafeUtility.SizeOf<PumpNodeDTO>() == 32 &&
                   OffsetOf<PumpNodeDTO>(nameof(PumpNodeDTO.NodeHash)) == 0 &&
                   OffsetOf<PumpNodeDTO>(nameof(PumpNodeDTO.IngressRate)) == 4 &&
                   OffsetOf<PumpNodeDTO>(nameof(PumpNodeDTO.MaxPumpRate)) == 8 &&
                   OffsetOf<PumpNodeDTO>(nameof(PumpNodeDTO.CurrentEvacuationRate)) == 12 &&
                   OffsetOf<PumpNodeDTO>(nameof(PumpNodeDTO.Flags)) == 16 &&
                   OffsetOf<PumpNodeDTO>(nameof(PumpNodeDTO.PowerDraw)) == 20 &&
                   OffsetOf<PumpNodeDTO>(nameof(PumpNodeDTO._pad0)) == 24 &&
                   OffsetOf<PumpNodeDTO>(nameof(PumpNodeDTO._pad7)) == 31;
        }

        public static bool ValidatePipeEdgeLayout()
        {
            return UnsafeUtility.SizeOf<PipeEdgeDTO>() == 64 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.SourceNodeIndex)) == 0 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.DestinationNodeIndex)) == 4 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.Conductance)) == 8 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.CurrentFlow)) == 12 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.Flags)) == 16 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.PowerPotential)) == 20 &&
                   OffsetOf<PipeEdgeDTO>(nameof(PipeEdgeDTO.DownhillScalar)) == 28;
        }

        public static bool ValidateRoomDrainLockLayout()
        {
            return UnsafeUtility.SizeOf<DrainageRoomDrainLock64>() == 64 &&
                   OffsetOf<DrainageRoomDrainLock64>(nameof(DrainageRoomDrainLock64.LockState)) == 0 &&
                   OffsetOf<DrainageRoomDrainLock64>(nameof(DrainageRoomDrainLock64.Pad0)) == 8 &&
                   OffsetOf<DrainageRoomDrainLock64>(nameof(DrainageRoomDrainLock64.Pad6)) == 56;
        }

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

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return UnsafeUtility.GetFieldOffset(typeof(T).GetField(fieldName));
        }

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
    }
}
