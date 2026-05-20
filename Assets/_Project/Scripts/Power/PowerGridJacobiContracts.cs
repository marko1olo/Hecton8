using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Power
{
    public static class PowerGridJacobiConstants
    {
        public const int PowerNodeDtoSizeBytes = 32;
        public const int PowerGridEdgeDtoSizeBytes = 32;
        public const int PowerProfileDtoSizeBytes = 32;
        public const int PowerTelemetryEntrySizeBytes = 64;
        public const int PowerGridCounter64SizeBytes = 64;
        public const int TelemetryFrameCount = 300;
        public const uint NodeFlagActive = 1u << 0;
        public const uint NodeFlagSource = 1u << 1;
        public const uint NodeFlagBattery = 1u << 2;
        public const uint NodeFlagDamaged = 1u << 3;
        public const uint NodeFlagFlooded = 1u << 4;
        public const uint NodeFlagBrownout = 1u << 5;
        public const uint NodeFlagOffline = 1u << 6;
        public const uint EdgeFlagSealed = 1u << 0;
        public const uint EdgeFlagDamaged = 1u << 1;
        public const uint EdgeFlagShortCircuit = 1u << 2;
        public const uint ProfileFlagGenerator = 1u << 0;
        public const uint ProfileFlagBattery = 1u << 1;
        public const uint TelemetryReasonNonFinite = 1u << 0;
        public const uint TelemetryReasonBrownout = 1u << 1;
        public const float MinimumConductance = 0.000001f;
        public const float BrownoutThreshold01 = 0.20f;
    }

    public static class PowerGridBufferIds
    {
        public const BufferID Nodes = (BufferID)70850;
        public const BufferID Edges = (BufferID)70851;
        public const BufferID NodeAup = (BufferID)70852;
        public const BufferID CsrOffsets = (BufferID)70853;
        public const BufferID CsrDestinations = (BufferID)70854;
        public const BufferID CsrConductance = (BufferID)70855;
        public const BufferID CsrFlow = (BufferID)70856;
        public const BufferID PotentialFront = (BufferID)70857;
        public const BufferID PotentialBack = (BufferID)70858;
        public const BufferID DemandRate = (BufferID)70859;
        public const BufferID BatteryRemainderMilli = (BufferID)70860;
        public const BufferID TelemetryRing = (BufferID)70861;
        public const BufferID TelemetryCursor = (BufferID)70862;
        public const BufferID Profiles = (BufferID)70863;
        public const BufferID CsvScratch = (BufferID)70864;
    }

    [StructLayout(LayoutKind.Explicit, Size = PowerGridJacobiConstants.PowerNodeDtoSizeBytes)]
    public struct PowerNodeDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float Potential;
        [FieldOffset(8)] public float MaxCapacity;
        [FieldOffset(12)] public float CurrentStorage;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float InternalResistance;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = PowerGridJacobiConstants.PowerGridEdgeDtoSizeBytes)]
    public struct PowerGridEdgeDTO
    {
        [FieldOffset(0)] public uint SourceNodeHash;
        [FieldOffset(4)] public uint DestinationNodeHash;
        [FieldOffset(8)] public float Conductance;
        [FieldOffset(12)] public float CurrentFlow;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float Capacity;
        [FieldOffset(24)] public int SourceNodeIndex;
        [FieldOffset(28)] public int DestinationNodeIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = PowerGridJacobiConstants.PowerProfileDtoSizeBytes)]
    public struct PowerProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float GeneratorRateWatts;
        [FieldOffset(8)] public float BatteryCapacityWattSeconds;
        [FieldOffset(12)] public float InternalResistance;
        [FieldOffset(16)] public float BaseConductance;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = PowerGridJacobiConstants.PowerTelemetryEntrySizeBytes)]
    public struct PowerTelemetryEntry
    {
        // ABI union aliases are intentional: older black-box readers use TotalLoad/AveragePotential/SolverMicroseconds,
        // while the power grid ledger uses TotalConsumption/Balance/OverloadedCount for the same 64-byte row.
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint ReasonFlags;
        [FieldOffset(12)] public int NodeCount;
        [FieldOffset(16)] public int EdgeCount;
        [FieldOffset(20)] public int RuntimeEdgeCount;
        [FieldOffset(24)] public int SolveStartNode;
        [FieldOffset(28)] public int SolveNodeCount;
        [FieldOffset(32)] public float TotalGeneration;
        [FieldOffset(36)] public float TotalConsumption;
        [FieldOffset(36)] public float TotalLoad;
        [FieldOffset(40)] public float SupplyRatio;
        [FieldOffset(44)] public float Balance;
        [FieldOffset(44)] public float AveragePotential;
        [FieldOffset(48)] public float MinPotential;
        [FieldOffset(52)] public float MaxPotential;
        [FieldOffset(56)] public int BrownoutCount;
        [FieldOffset(60)] public int OverloadedCount;
        [FieldOffset(60)] public int SolverMicroseconds;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PowerEquipmentLoadRequest
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public float EnergyWattSeconds;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PumpPowerRequest
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float EnergyWattSeconds;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = PowerGridJacobiConstants.PowerGridCounter64SizeBytes)]
    public struct PowerGridCounter64
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong Reserved0;
        [FieldOffset(16)] public ulong Reserved1;
        [FieldOffset(24)] public ulong Reserved2;
        [FieldOffset(32)] public ulong Reserved3;
        [FieldOffset(40)] public ulong Reserved4;
        [FieldOffset(48)] public ulong Reserved5;
        [FieldOffset(56)] public ulong Reserved6;
    }

    public struct PowerGridVaultHandles
    {
        public VaultGenerationHandle<PowerNodeDTO> Nodes;
        public VaultGenerationHandle<PowerGridEdgeDTO> Edges;
        public VaultGenerationHandle<double3> NodeAup;
        public VaultGenerationHandle<int> NodeEdgeOffsets;
        public VaultGenerationHandle<int> EdgeDestinations;
        public VaultGenerationHandle<float> EdgeConductance;
        public VaultGenerationHandle<float> EdgeCurrentFlow;
        public VaultGenerationHandle<float> PotentialFront;
        public VaultGenerationHandle<float> PotentialBack;
        public VaultGenerationHandle<float> DemandRate;
        public VaultGenerationHandle<float> BatteryMilliRemainder;
        public VaultGenerationHandle<PowerTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<PowerGridCounter64> TelemetryCursor;
        public VaultGenerationHandle<PowerProfileDTO> Profiles;
        public VaultGenerationHandle<byte> CsvScratch;
    }

#if UNITY_EDITOR
    public static class PowerGridLayoutAudit
    {
        public static bool ValidatePowerNodeDtoLayout()
        {
            return UnsafeUtility.SizeOf<PowerNodeDTO>() == PowerGridJacobiConstants.PowerNodeDtoSizeBytes &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.NodeHash)) == 0 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.Potential)) == 4 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.MaxCapacity)) == 8 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.CurrentStorage)) == 12 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.Flags)) == 16 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.InternalResistance)) == 20 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO._pad0)) == 24 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO._pad7)) == 31;
        }

        public static bool ValidateAllPowerLayouts()
        {
            return ValidatePowerNodeDtoLayout() &&
                   ValidatePowerGridEdgeDtoLayout() &&
                   ValidatePowerProfileDtoLayout() &&
                   ValidatePowerTelemetryEntryLayout() &&
                   ValidatePowerGridCounter64Layout() &&
                   ValidatePowerRequestLayouts();
        }

        public static bool ValidatePowerGridEdgeDtoLayout()
        {
            return UnsafeUtility.SizeOf<PowerGridEdgeDTO>() == PowerGridJacobiConstants.PowerGridEdgeDtoSizeBytes &&
                   OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.SourceNodeHash)) == 0 &&
                   OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.DestinationNodeHash)) == 4 &&
                   OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.Conductance)) == 8 &&
                   OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.CurrentFlow)) == 12 &&
                   OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.Flags)) == 16 &&
                   OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.Capacity)) == 20 &&
                   OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.SourceNodeIndex)) == 24 &&
                   OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.DestinationNodeIndex)) == 28;
        }

        public static bool ValidatePowerProfileDtoLayout()
        {
            return UnsafeUtility.SizeOf<PowerProfileDTO>() == PowerGridJacobiConstants.PowerProfileDtoSizeBytes &&
                   OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.ProfileHash)) == 0 &&
                   OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.GeneratorRateWatts)) == 4 &&
                   OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.BatteryCapacityWattSeconds)) == 8 &&
                   OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.InternalResistance)) == 12 &&
                   OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.BaseConductance)) == 16 &&
                   OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.Flags)) == 20 &&
                   OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.Reserved0)) == 24 &&
                   OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.Reserved1)) == 28;
        }

        public static bool ValidatePowerTelemetryEntryLayout()
        {
            return UnsafeUtility.SizeOf<PowerTelemetryEntry>() == PowerGridJacobiConstants.PowerTelemetryEntrySizeBytes &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.StateHash)) == 4 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.ReasonFlags)) == 8 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.NodeCount)) == 12 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.EdgeCount)) == 16 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.RuntimeEdgeCount)) == 20 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.SolveStartNode)) == 24 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.SolveNodeCount)) == 28 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.TotalGeneration)) == 32 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.TotalConsumption)) == 36 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.TotalLoad)) == 36 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.SupplyRatio)) == 40 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.Balance)) == 44 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.AveragePotential)) == 44 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.MinPotential)) == 48 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.MaxPotential)) == 52 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.BrownoutCount)) == 56 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.OverloadedCount)) == 60 &&
                   OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.SolverMicroseconds)) == 60;
        }

        public static bool ValidatePowerGridCounter64Layout()
        {
            return UnsafeUtility.SizeOf<PowerGridCounter64>() == PowerGridJacobiConstants.PowerGridCounter64SizeBytes &&
                   OffsetOf<PowerGridCounter64>(nameof(PowerGridCounter64.Value)) == 0 &&
                   OffsetOf<PowerGridCounter64>(nameof(PowerGridCounter64.Flags)) == 4 &&
                   OffsetOf<PowerGridCounter64>(nameof(PowerGridCounter64.Reserved0)) == 8 &&
                   OffsetOf<PowerGridCounter64>(nameof(PowerGridCounter64.Reserved6)) == 56;
        }

        public static bool ValidatePowerRequestLayouts()
        {
            return UnsafeUtility.SizeOf<PowerEquipmentLoadRequest>() == 16 &&
                   OffsetOf<PowerEquipmentLoadRequest>(nameof(PowerEquipmentLoadRequest.ToolHashID)) == 0 &&
                   OffsetOf<PowerEquipmentLoadRequest>(nameof(PowerEquipmentLoadRequest.EnergyWattSeconds)) == 4 &&
                   OffsetOf<PowerEquipmentLoadRequest>(nameof(PowerEquipmentLoadRequest.Flags)) == 8 &&
                   OffsetOf<PowerEquipmentLoadRequest>(nameof(PowerEquipmentLoadRequest.Reserved0)) == 12 &&
                   UnsafeUtility.SizeOf<PumpPowerRequest>() == 16 &&
                   OffsetOf<PumpPowerRequest>(nameof(PumpPowerRequest.NodeHash)) == 0 &&
                   OffsetOf<PumpPowerRequest>(nameof(PumpPowerRequest.EnergyWattSeconds)) == 4 &&
                   OffsetOf<PumpPowerRequest>(nameof(PumpPowerRequest.Flags)) == 8 &&
                   OffsetOf<PumpPowerRequest>(nameof(PumpPowerRequest.Reserved0)) == 12;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            var field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
#endif

    public static class PowerGridAupMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToBaseLocalFloat3(double3 nodeAup, double3 baseOriginAup)
        {
            double3 localDelta = nodeAup - baseOriginAup;
            return new float3((float)localDelta.x, (float)localDelta.y, (float)localDelta.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceMeters(double3 aupA, double3 aupB, double3 baseOriginAup)
        {
            float3 localA = ToBaseLocalFloat3(aupA, baseOriginAup);
            float3 localB = ToBaseLocalFloat3(aupB, baseOriginAup);
            float distanceSq = math.lengthsq(localA - localB);
            return distanceSq <= 0.000001f ? 0f : distanceSq * math.rsqrt(math.max(distanceSq, 0.000001f));
        }
    }

    public static class PowerGridVaultRuntime
    {
        public static bool EnsureCoreBuffers(IDataVault vault, int nodeCapacity, int edgeCapacity, out PowerGridVaultHandles handles)
        {
            handles = default;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            int safeNodes = math.max(1, nodeCapacity);
            int safeEdges = math.max(1, edgeCapacity);
            int safeAdjacency = math.max(2, math.min(safeEdges, int.MaxValue / 2) * 2);
            handles.Nodes = vault.GetGenerationHandle<PowerNodeDTO>(PowerGridBufferIds.Nodes, safeNodes, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.Edges = vault.GetGenerationHandle<PowerGridEdgeDTO>(PowerGridBufferIds.Edges, safeEdges, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.NodeAup = vault.GetGenerationHandle<double3>(PowerGridBufferIds.NodeAup, safeNodes, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.NodeEdgeOffsets = vault.GetGenerationHandle<int>(PowerGridBufferIds.CsrOffsets, safeNodes + 1, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.EdgeDestinations = vault.GetGenerationHandle<int>(PowerGridBufferIds.CsrDestinations, safeAdjacency, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.EdgeConductance = vault.GetGenerationHandle<float>(PowerGridBufferIds.CsrConductance, safeAdjacency, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.EdgeCurrentFlow = vault.GetGenerationHandle<float>(PowerGridBufferIds.CsrFlow, safeAdjacency, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.PotentialFront = vault.GetGenerationHandle<float>(PowerGridBufferIds.PotentialFront, safeNodes, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.PotentialBack = vault.GetGenerationHandle<float>(PowerGridBufferIds.PotentialBack, safeNodes, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.DemandRate = vault.GetGenerationHandle<float>(PowerGridBufferIds.DemandRate, safeNodes, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.BatteryMilliRemainder = vault.GetGenerationHandle<float>(PowerGridBufferIds.BatteryRemainderMilli, safeNodes, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = vault.GetGenerationHandle<PowerTelemetryEntry>(PowerGridBufferIds.TelemetryRing, PowerGridJacobiConstants.TelemetryFrameCount, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.GetGenerationHandle<PowerGridCounter64>(PowerGridBufferIds.TelemetryCursor, 1, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.Profiles = vault.GetGenerationHandle<PowerProfileDTO>(PowerGridBufferIds.Profiles, 128, SystemID.Power, NativeArrayOptions.ClearMemory);
            handles.CsvScratch = vault.GetGenerationHandle<byte>(PowerGridBufferIds.CsvScratch, 16 * 1024, SystemID.Power, NativeArrayOptions.ClearMemory);

            bool valid = HasResolvedBuffer(vault, in handles.Nodes, safeNodes) &&
                         HasResolvedBuffer(vault, in handles.Edges, safeEdges) &&
                         HasResolvedBuffer(vault, in handles.NodeAup, safeNodes) &&
                         HasResolvedBuffer(vault, in handles.NodeEdgeOffsets, safeNodes + 1) &&
                         HasResolvedBuffer(vault, in handles.EdgeDestinations, safeAdjacency) &&
                         HasResolvedBuffer(vault, in handles.EdgeConductance, safeAdjacency) &&
                         HasResolvedBuffer(vault, in handles.EdgeCurrentFlow, safeAdjacency) &&
                         HasResolvedBuffer(vault, in handles.PotentialFront, safeNodes) &&
                         HasResolvedBuffer(vault, in handles.PotentialBack, safeNodes) &&
                         HasResolvedBuffer(vault, in handles.DemandRate, safeNodes) &&
                         HasResolvedBuffer(vault, in handles.BatteryMilliRemainder, safeNodes) &&
                         HasResolvedBuffer(vault, in handles.TelemetryRing, PowerGridJacobiConstants.TelemetryFrameCount) &&
                         HasResolvedBuffer(vault, in handles.TelemetryCursor, 1) &&
                         HasResolvedBuffer(vault, in handles.Profiles, 128) &&
                         HasResolvedBuffer(vault, in handles.CsvScratch, 16 * 1024);
            if (!valid)
                ReleaseCoreBuffers(vault, ref handles);

            return valid;
        }

        public static bool ValidateCoreBuffers(IDataVault vault, in PowerGridVaultHandles handles, int nodeCapacity, int edgeCapacity)
        {
            if (vault == null)
                return false;

            int safeNodes = math.max(1, nodeCapacity);
            int safeEdges = math.max(1, edgeCapacity);
            int safeAdjacency = math.max(2, math.min(safeEdges, int.MaxValue / 2) * 2);
            return HasResolvedBuffer(vault, in handles.Nodes, safeNodes) &&
                   HasResolvedBuffer(vault, in handles.Edges, safeEdges) &&
                   HasResolvedBuffer(vault, in handles.NodeAup, safeNodes) &&
                   HasResolvedBuffer(vault, in handles.NodeEdgeOffsets, safeNodes + 1) &&
                   HasResolvedBuffer(vault, in handles.EdgeDestinations, safeAdjacency) &&
                   HasResolvedBuffer(vault, in handles.EdgeConductance, safeAdjacency) &&
                   HasResolvedBuffer(vault, in handles.EdgeCurrentFlow, safeAdjacency) &&
                   HasResolvedBuffer(vault, in handles.PotentialFront, safeNodes) &&
                   HasResolvedBuffer(vault, in handles.PotentialBack, safeNodes) &&
                   HasResolvedBuffer(vault, in handles.DemandRate, safeNodes) &&
                   HasResolvedBuffer(vault, in handles.BatteryMilliRemainder, safeNodes) &&
                   HasResolvedBuffer(vault, in handles.TelemetryRing, PowerGridJacobiConstants.TelemetryFrameCount) &&
                   HasResolvedBuffer(vault, in handles.TelemetryCursor, 1) &&
                   HasResolvedBuffer(vault, in handles.Profiles, 128) &&
                   HasResolvedBuffer(vault, in handles.CsvScratch, 16 * 1024);
        }

        public static void ReleaseCoreBuffers(IDataVault vault, ref PowerGridVaultHandles handles)
        {
            if (vault != null)
            {
                ReleaseBuffer(vault, in handles.Nodes);
                ReleaseBuffer(vault, in handles.Edges);
                ReleaseBuffer(vault, in handles.NodeAup);
                ReleaseBuffer(vault, in handles.NodeEdgeOffsets);
                ReleaseBuffer(vault, in handles.EdgeDestinations);
                ReleaseBuffer(vault, in handles.EdgeConductance);
                ReleaseBuffer(vault, in handles.EdgeCurrentFlow);
                ReleaseBuffer(vault, in handles.PotentialFront);
                ReleaseBuffer(vault, in handles.PotentialBack);
                ReleaseBuffer(vault, in handles.DemandRate);
                ReleaseBuffer(vault, in handles.BatteryMilliRemainder);
                ReleaseBuffer(vault, in handles.TelemetryRing);
                ReleaseBuffer(vault, in handles.TelemetryCursor);
                ReleaseBuffer(vault, in handles.Profiles);
                ReleaseBuffer(vault, in handles.CsvScratch);
            }

            handles = default;
        }

        private static bool HasResolvedBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle, int minLength) where T : struct
        {
            if (handle.BufferID == 0u)
                return false;
            if (!vault.TryResolveHandle(in handle, out NativeArray<T> buffer))
                return false;
            return buffer.IsCreated && buffer.Length >= minLength;
        }

        private static void ReleaseBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID == 0u)
                return;

            vault.ReleaseBuffer(in handle);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildCsrPowerGraphJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<PowerNodeDTO> Nodes;
        [ReadOnly] [NoAlias] public NativeArray<PowerGridEdgeDTO> FlatEdges;
        [NoAlias] public NativeArray<int> NodeEdgeOffsets;
        [NoAlias] public NativeArray<int> EdgeWriteCursor;
        [NoAlias] public NativeArray<int> EdgeDestinations;
        [NoAlias] public NativeArray<float> EdgeConductance;
        [NoAlias] public NativeArray<float> EdgeCurrentFlow;
        public int NodeCount;
        public int EdgeCount;

        public void Execute()
        {
            int nodeCount = math.clamp(NodeCount, 0, math.max(0, Nodes.Length));
            int edgeCount = math.clamp(EdgeCount, 0, math.max(0, FlatEdges.Length));
            int maxAdjacency = math.min(EdgeDestinations.Length, math.min(EdgeConductance.Length, EdgeCurrentFlow.Length));
            if (NodeEdgeOffsets.Length < nodeCount + 1 || EdgeWriteCursor.Length < nodeCount)
                return;

            for (int i = 0; i <= nodeCount; i++)
                NodeEdgeOffsets[i] = 0;
            for (int i = 0; i < nodeCount; i++)
                EdgeWriteCursor[i] = 0;

            int adjacencyCount = 0;
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                PowerGridEdgeDTO edge = FlatEdges[edgeIndex];
                if (!IsTraversable(edge, nodeCount))
                    continue;
                if (adjacencyCount + 2 > maxAdjacency)
                    break;

                NodeEdgeOffsets[edge.SourceNodeIndex + 1]++;
                NodeEdgeOffsets[edge.DestinationNodeIndex + 1]++;
                adjacencyCount += 2;
            }

            int prefix = 0;
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                int degree = NodeEdgeOffsets[nodeIndex + 1];
                NodeEdgeOffsets[nodeIndex] = prefix;
                EdgeWriteCursor[nodeIndex] = prefix;
                prefix += degree;
            }
            NodeEdgeOffsets[nodeCount] = prefix;

            int writtenAdjacency = 0;
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                PowerGridEdgeDTO edge = FlatEdges[edgeIndex];
                if (!IsTraversable(edge, nodeCount))
                    continue;
                if (writtenAdjacency + 2 > maxAdjacency)
                    break;

                float conductance = ResolveConductance(edge);
                int writeA = EdgeWriteCursor[edge.SourceNodeIndex]++;
                int writeB = EdgeWriteCursor[edge.DestinationNodeIndex]++;
                if ((uint)writeA < (uint)maxAdjacency)
                {
                    EdgeDestinations[writeA] = edge.DestinationNodeIndex;
                    EdgeConductance[writeA] = conductance;
                    EdgeCurrentFlow[writeA] = 0f;
                }

                if ((uint)writeB < (uint)maxAdjacency)
                {
                    EdgeDestinations[writeB] = edge.SourceNodeIndex;
                    EdgeConductance[writeB] = conductance;
                    EdgeCurrentFlow[writeB] = 0f;
                }

                writtenAdjacency += 2;
            }
        }

        private bool IsTraversable(in PowerGridEdgeDTO edge, int nodeCount)
        {
            if ((uint)edge.SourceNodeIndex >= (uint)nodeCount ||
                (uint)edge.DestinationNodeIndex >= (uint)nodeCount)
                return false;
            return true;
        }

        private float ResolveConductance(in PowerGridEdgeDTO edge)
        {
            if ((edge.Flags & (PowerGridJacobiConstants.EdgeFlagSealed | PowerGridJacobiConstants.EdgeFlagDamaged | PowerGridJacobiConstants.EdgeFlagShortCircuit)) != 0u)
                return 0f;
            uint sourceFlags = Nodes[edge.SourceNodeIndex].Flags;
            uint destinationFlags = Nodes[edge.DestinationNodeIndex].Flags;
            if (((sourceFlags | destinationFlags) & PowerGridJacobiConstants.NodeFlagDamaged) != 0u)
                return 0f;

            return math.max(0f, math.isfinite(edge.Conductance) ? edge.Conductance : 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct PowerVoltageSolverJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerNodeDTO* NodesPtr;
        [ReadOnly] [NoAlias] public NativeArray<int> NodeEdgeOffsets;
        [ReadOnly] [NoAlias] public NativeArray<int> EdgeDestinations;
        [ReadOnly] [NoAlias] public NativeArray<float> EdgeConductance;
        [ReadOnly] [NoAlias] public NativeArray<float> FrontPotential;
        [ReadOnly] [NoAlias] public NativeArray<float> DemandRate;
        [NoAlias] public NativeArray<float> BackPotential;
        public int NodeCount;
        public float GlobalQualityWeight;
        public float SmoothingFactor;

        public void Execute(int index)
        {
            if (NodesPtr == null ||
                (uint)index >= (uint)NodeCount ||
                (uint)(index + 1) >= (uint)NodeEdgeOffsets.Length ||
                (uint)index >= (uint)FrontPotential.Length ||
                (uint)index >= (uint)BackPotential.Length)
            {
                return;
            }

            ref PowerNodeDTO node = ref UnsafeUtility.AsRef<PowerNodeDTO>(NodesPtr + index);
            uint flags = node.Flags;
            if ((flags & (PowerGridJacobiConstants.NodeFlagOffline | PowerGridJacobiConstants.NodeFlagDamaged)) != 0u)
            {
                node.Potential = 0f;
                BackPotential[index] = 0f;
                return;
            }

            int edgeReadLimit = math.min(EdgeDestinations.Length, EdgeConductance.Length);
            int edgeStart = math.clamp(NodeEdgeOffsets[index], 0, edgeReadLimit);
            int edgeEnd = math.clamp(NodeEdgeOffsets[index + 1], edgeStart, edgeReadLimit);
            float weightedPotential = 0f;
            float conductanceSum = 0f;
            for (int edgeCursor = edgeStart; edgeCursor < edgeEnd; edgeCursor++)
            {
                int destination = EdgeDestinations[edgeCursor];
                if ((uint)destination >= (uint)NodeCount || (uint)destination >= (uint)FrontPotential.Length)
                    continue;

                float conductance = math.max(0f, math.isfinite(EdgeConductance[edgeCursor]) ? EdgeConductance[edgeCursor] : 0f);
                if (conductance <= PowerGridJacobiConstants.MinimumConductance)
                    continue;

                weightedPotential += conductance * Sanitize01(FrontPotential[destination]);
                conductanceSum += conductance;
            }

            float generatorRate = (flags & PowerGridJacobiConstants.NodeFlagSource) != 0u ? 1f : 0f;
            float demandRaw = DemandRate.IsCreated && (uint)index < (uint)DemandRate.Length
                ? DemandRate[index]
                : 0f;
            float demandRate = math.saturate(math.max(0f, math.isfinite(demandRaw) ? demandRaw : 0f));
            float targetPotential = (weightedPotential + generatorRate - demandRate) * math.rcp(math.max(conductanceSum + 1f, 1f));
            float currentPotential = Sanitize01(FrontPotential[index]);
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float smoothingInput = math.isfinite(SmoothingFactor) ? SmoothingFactor : 1f;
            float smoothing = math.clamp(smoothingInput * math.lerp(0.35f, 1f, q), 0.05f, 1f);
            float solvedPotential = currentPotential + (targetPotential - currentPotential) * smoothing;
            solvedPotential = Sanitize01(solvedPotential);

            node.Potential = solvedPotential;
            if (solvedPotential < PowerGridJacobiConstants.BrownoutThreshold01)
                node.Flags = flags | PowerGridJacobiConstants.NodeFlagBrownout;
            else
                node.Flags = flags & ~PowerGridJacobiConstants.NodeFlagBrownout;
            BackPotential[index] = solvedPotential;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct IntegrateBatteryChargeJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerNodeDTO* NodesPtr;
        [ReadOnly] [NoAlias] public NativeArray<int> NodeEdgeOffsets;
        [ReadOnly] [NoAlias] public NativeArray<int> EdgeDestinations;
        [ReadOnly] [NoAlias] public NativeArray<float> EdgeConductance;
        [NoAlias] public NativeArray<float> EdgeCurrentFlow;
        [NoAlias] public NativeArray<float> BatteryMilliRemainder;
        public int NodeCount;
        public float DeltaTimeSeconds;

        public void Execute(int index)
        {
            if (NodesPtr == null ||
                (uint)index >= (uint)NodeCount ||
                (uint)(index + 1) >= (uint)NodeEdgeOffsets.Length)
            {
                return;
            }

            ref PowerNodeDTO node = ref UnsafeUtility.AsRef<PowerNodeDTO>(NodesPtr + index);
            float potential = Sanitize01(node.Potential);
            int edgeReadLimit = math.min(EdgeDestinations.Length, EdgeConductance.Length);
            int edgeStart = math.clamp(NodeEdgeOffsets[index], 0, edgeReadLimit);
            int edgeEnd = math.clamp(NodeEdgeOffsets[index + 1], edgeStart, edgeReadLimit);
            float netCurrentOut = 0f;
            for (int edgeCursor = edgeStart; edgeCursor < edgeEnd; edgeCursor++)
            {
                int destination = EdgeDestinations[edgeCursor];
                if ((uint)destination >= (uint)NodeCount)
                    continue;

                ref PowerNodeDTO destinationNode = ref UnsafeUtility.AsRef<PowerNodeDTO>(NodesPtr + destination);
                float conductance = math.max(0f, math.isfinite(EdgeConductance[edgeCursor]) ? EdgeConductance[edgeCursor] : 0f);
                float current = (potential - Sanitize01(destinationNode.Potential)) * conductance;
                netCurrentOut += current;
                if ((uint)edgeCursor < (uint)EdgeCurrentFlow.Length)
                    EdgeCurrentFlow[edgeCursor] = current;
            }

            if ((node.Flags & PowerGridJacobiConstants.NodeFlagBattery) == 0u)
                return;

            float capacity = math.max(0f, math.isfinite(node.MaxCapacity) ? node.MaxCapacity : 0f);
            if (capacity <= 0f)
            {
                node.CurrentStorage = 0f;
                return;
            }

            float tickDelta = math.max(0f, math.isfinite(DeltaTimeSeconds) ? DeltaTimeSeconds : 0f);
            float carriedRemainder = 0f;
            if ((uint)index < (uint)BatteryMilliRemainder.Length)
                carriedRemainder = math.isfinite(BatteryMilliRemainder[index]) ? BatteryMilliRemainder[index] : 0f;

            float rawMilliWattSeconds = (-netCurrentOut * tickDelta * 1000f) + carriedRemainder;
            int wholeMilliWattSeconds = (int)math.trunc(rawMilliWattSeconds);
            if ((uint)index < (uint)BatteryMilliRemainder.Length)
                BatteryMilliRemainder[index] = rawMilliWattSeconds - wholeMilliWattSeconds;

            float deltaWattSeconds = wholeMilliWattSeconds * 0.001f;
            node.CurrentStorage = math.clamp(node.CurrentStorage + deltaWattSeconds, 0f, capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyEquipmentPowerDrainJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<PowerEquipmentLoadRequest> EquipmentRequests;
        [ReadOnly] [NoAlias] public NativeArray<PumpPowerRequest> PumpRequests;
        [ReadOnly] public NativeParallelHashMap<uint, int> ToolToNode;
        [ReadOnly] public NativeParallelHashMap<uint, int> PumpToNode;
        [NoAlias] public NativeArray<float> DemandRate;
        public float DeltaTimeSeconds;

        public void Execute()
        {
            if (!DemandRate.IsCreated || DemandRate.Length <= 0)
                return;

            for (int i = 0; i < DemandRate.Length; i++)
                DemandRate[i] = 0f;

            float tickDelta = math.max(0.001f, math.isfinite(DeltaTimeSeconds) ? DeltaTimeSeconds : 0.001f);
            float invDt = math.rcp(tickDelta);
            if (EquipmentRequests.IsCreated && ToolToNode.IsCreated)
            {
                for (int i = 0; i < EquipmentRequests.Length; i++)
                {
                    PowerEquipmentLoadRequest request = EquipmentRequests[i];
                    float energyWattSeconds = math.max(0f, math.isfinite(request.EnergyWattSeconds) ? request.EnergyWattSeconds : 0f);
                    if (request.ToolHashID == 0u || energyWattSeconds <= 0f)
                        continue;
                    if (ToolToNode.TryGetValue(request.ToolHashID, out int nodeIndex) && (uint)nodeIndex < (uint)DemandRate.Length)
                        DemandRate[nodeIndex] = math.saturate(SanitizeDemand(DemandRate[nodeIndex]) + energyWattSeconds * invDt);
                }
            }

            if (PumpRequests.IsCreated && PumpToNode.IsCreated)
            {
                for (int i = 0; i < PumpRequests.Length; i++)
                {
                    PumpPowerRequest request = PumpRequests[i];
                    float energyWattSeconds = math.max(0f, math.isfinite(request.EnergyWattSeconds) ? request.EnergyWattSeconds : 0f);
                    if (request.NodeHash == 0u || energyWattSeconds <= 0f)
                        continue;
                    if (PumpToNode.TryGetValue(request.NodeHash, out int nodeIndex) && (uint)nodeIndex < (uint)DemandRate.Length)
                        DemandRate[nodeIndex] = math.saturate(SanitizeDemand(DemandRate[nodeIndex]) + energyWattSeconds * invDt);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeDemand(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordPowerTelemetryJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<PowerNodeDTO> Nodes;
        [ReadOnly] [NoAlias] public NativeArray<float> DemandRate;
        [NoAlias] public NativeArray<PowerTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<PowerGridCounter64> TelemetryCursor;
        public uint FrameIndex;
        public uint ReasonFlags;
        public int NodeCount;
        public int EdgeCount;
        public int RuntimeEdgeCount;
        public int SolveStartNode;
        public int SolveNodeCount;
        public int SolverMicroseconds;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int nodeLimit = math.clamp(NodeCount, 0, Nodes.IsCreated ? Nodes.Length : 0);
            float totalGeneration = 0f;
            float totalLoad = 0f;
            float potentialSum = 0f;
            float minPotential = nodeLimit > 0 ? 1f : 0f;
            float maxPotential = 0f;
            int brownoutCount = 0;
            uint stateHash = 2166136261u;
            uint reasonFlags = ReasonFlags;

            for (int i = 0; i < nodeLimit; i++)
            {
                PowerNodeDTO node = Nodes[i];
                float potential = Sanitize01(node.Potential, ref reasonFlags);
                float capacity = SanitizePositive(node.MaxCapacity, ref reasonFlags);
                float demand = 0f;
                if (DemandRate.IsCreated && (uint)i < (uint)DemandRate.Length)
                    demand = SanitizePositive(DemandRate[i], ref reasonFlags);

                if ((node.Flags & PowerGridJacobiConstants.NodeFlagSource) != 0u)
                    totalGeneration += capacity * potential;

                totalLoad += demand;
                potentialSum += potential;
                minPotential = math.min(minPotential, potential);
                maxPotential = math.max(maxPotential, potential);
                if (potential < PowerGridJacobiConstants.BrownoutThreshold01)
                    brownoutCount++;

                stateHash = Mix(stateHash, node.NodeHash);
                stateHash = Mix(stateHash, math.asuint(potential));
                stateHash = Mix(stateHash, math.asuint(demand));
            }

            if (brownoutCount > 0)
                reasonFlags |= PowerGridJacobiConstants.TelemetryReasonBrownout;

            float averagePotential = nodeLimit > 0
                ? potentialSum * math.rcp(math.max(1, nodeLimit))
                : 0f;
            float supplyRatio = totalLoad > 0.0001f
                ? math.saturate(totalGeneration * math.rcp(math.max(totalLoad, 0.0001f)))
                : 1f;

            PowerTelemetryEntry entry = default;
            entry.FrameIndex = FrameIndex;
            entry.StateHash = stateHash;
            entry.ReasonFlags = reasonFlags;
            entry.NodeCount = nodeLimit;
            entry.EdgeCount = math.max(0, EdgeCount);
            entry.RuntimeEdgeCount = math.max(0, RuntimeEdgeCount);
            entry.SolveStartNode = math.max(0, SolveStartNode);
            entry.SolveNodeCount = math.max(0, SolveNodeCount);
            entry.TotalGeneration = totalGeneration;
            entry.TotalLoad = totalLoad;
            entry.SupplyRatio = supplyRatio;
            entry.AveragePotential = averagePotential;
            entry.MinPotential = minPotential;
            entry.MaxPotential = maxPotential;
            entry.BrownoutCount = brownoutCount;
            entry.SolverMicroseconds = math.max(0, SolverMicroseconds);

            int writeIndex = ResolveWriteIndex(reasonFlags);
            TelemetryRing[writeIndex] = entry;
        }

        private int ResolveWriteIndex(uint finalReasonFlags)
        {
            if (!TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return (int)(FrameIndex % (uint)TelemetryRing.Length);

            PowerGridCounter64 cursor = TelemetryCursor[0];
            int cursorValue = math.max(0, cursor.Value);
            int writeIndex = cursorValue % TelemetryRing.Length;
            cursor.Value = cursorValue == int.MaxValue ? 0 : cursorValue + 1;
            cursor.Flags = finalReasonFlags;
            TelemetryCursor[0] = cursor;
            return writeIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value, ref uint reasonFlags)
        {
            if (math.isfinite(value))
                return math.saturate(value);

            reasonFlags |= PowerGridJacobiConstants.TelemetryReasonNonFinite;
            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, ref uint reasonFlags)
        {
            if (math.isfinite(value))
                return math.max(0f, value);

            reasonFlags |= PowerGridJacobiConstants.TelemetryReasonNonFinite;
            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockPowerNetworkJob : IJob
    {
        [NoAlias] public NativeArray<PowerNodeDTO> Nodes;
        [NoAlias] public NativeArray<PowerGridEdgeDTO> Edges;
        [NoAlias] public NativeArray<double3> NodeAup;
        [NoAlias] public NativeArray<int> Counts;
        public double3 BaseOriginAup;
        public int RequestedNodeCount;
        public int RequestedEdgeCount;

        public void Execute()
        {
            int nodeCapacity = math.min(Nodes.Length, NodeAup.IsCreated ? NodeAup.Length : 0);
            if (nodeCapacity <= 0)
            {
                if (Counts.IsCreated && Counts.Length >= 2)
                {
                    Counts[0] = 0;
                    Counts[1] = 0;
                }
                return;
            }

            int nodeCount = math.clamp(RequestedNodeCount <= 0 ? 1000 : RequestedNodeCount, 1, nodeCapacity);
            int edgeCount = math.clamp(RequestedEdgeCount <= 0 ? 2500 : RequestedEdgeCount, 0, Edges.Length);
            uint seed = 0xA22319D5u;
            for (int i = 0; i < nodeCount; i++)
            {
                bool generator = (i % 251) == 0;
                bool battery = (i % 53) == 0;
                PowerNodeDTO node = default;
                node.NodeHash = HashNode(i);
                node.Potential = generator ? 1f : 0f;
                node.MaxCapacity = battery ? 12000f : 120f;
                node.CurrentStorage = battery ? 6000f : 0f;
                node.Flags = PowerGridJacobiConstants.NodeFlagActive |
                             (generator ? PowerGridJacobiConstants.NodeFlagSource : 0u) |
                             (battery ? PowerGridJacobiConstants.NodeFlagBattery : 0u);
                node.InternalResistance = generator ? 0.08f : 0.35f;
                Nodes[i] = node;

                double x = (i % 40) * 4.0;
                double z = (i / 40) * 4.0;
                NodeAup[i] = BaseOriginAup + new double3(x, 0.0, z);
                _ = PowerGridAupMath.ToBaseLocalFloat3(NodeAup[i], BaseOriginAup);
            }

            for (int i = 0; i < edgeCount; i++)
            {
                int source = i % nodeCount;
                int ringTarget = (source + 1 + (i % 7)) % nodeCount;
                int extraTarget = NextIndex(ref seed, nodeCount);
                int destination = (i & 3) == 0 ? extraTarget : ringTarget;
                if (destination == source)
                    destination = (destination + 1) % nodeCount;

                PowerGridEdgeDTO edge = default;
                edge.SourceNodeHash = HashNode(source);
                edge.DestinationNodeHash = HashNode(destination);
                edge.Conductance = math.lerp(0.18f, 0.92f, (i & 31) * (1f / 31f));
                edge.CurrentFlow = 0f;
                edge.Flags = 0u;
                edge.Capacity = 450f + ((i % 17) * 40f);
                edge.SourceNodeIndex = source;
                edge.DestinationNodeIndex = destination;
                Edges[i] = edge;
            }

            if (Counts.IsCreated && Counts.Length >= 2)
            {
                Counts[0] = nodeCount;
                Counts[1] = edgeCount;
            }
        }

        private static int NextIndex(ref uint seed, int nodeCount)
        {
            seed = (seed * 1664525u) + 1013904223u;
            return nodeCount > 0 ? (int)(seed % (uint)nodeCount) : 0;
        }

        private static uint HashNode(int index)
        {
            uint value = (uint)(index + 1);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }
    }

    public static class PowerProfileCsvParser
    {
        public static bool TryParsePowerProfiles(ReadOnlySpan<byte> csvBytes, NativeArray<PowerProfileDTO> profiles, out int profileCount)
        {
            profileCount = 0;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int lineStart = 0;
            while (lineStart < csvBytes.Length && profileCount < profiles.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csvBytes.Length && csvBytes[lineEnd] != (byte)'\n' && csvBytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, lineEnd - lineStart);
                if (TryParseLine(line, out PowerProfileDTO profile))
                    profiles[profileCount++] = profile;

                lineStart = lineEnd + 1;
                while (lineStart < csvBytes.Length && (csvBytes[lineStart] == (byte)'\n' || csvBytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return true;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out PowerProfileDTO profile)
        {
            profile = default;
            line = Trim(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            ReadOnlySpan<byte> name = NextField(ref line);
            if (name.Length == 0 || IsHeader(name))
                return false;

            profile.ProfileHash = Fnva32(name);
            profile.GeneratorRateWatts = ParseFloat(NextField(ref line));
            profile.BatteryCapacityWattSeconds = ParseFloat(NextField(ref line));
            profile.InternalResistance = math.max(0f, ParseFloat(NextField(ref line)));
            profile.BaseConductance = math.max(0f, ParseFloat(NextField(ref line)));
            profile.Flags = (profile.GeneratorRateWatts > 0f ? PowerGridJacobiConstants.ProfileFlagGenerator : 0u) |
                            (profile.BatteryCapacityWattSeconds > 0f ? PowerGridJacobiConstants.ProfileFlagBattery : 0u);
            return true;
        }

        private static ReadOnlySpan<byte> NextField(ref ReadOnlySpan<byte> line)
        {
            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                ReadOnlySpan<byte> last = Trim(line);
                line = ReadOnlySpan<byte>.Empty;
                return last;
            }

            ReadOnlySpan<byte> field = Trim(line.Slice(0, comma));
            line = line.Slice(comma + 1);
            return field;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsSpace(value[start]))
                start++;
            while (end >= start && IsSpace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool IsHeader(ReadOnlySpan<byte> value)
        {
            return value.Length >= 4 &&
                   ToLower(value[0]) == (byte)'n' &&
                   ToLower(value[1]) == (byte)'a' &&
                   ToLower(value[2]) == (byte)'m' &&
                   ToLower(value[3]) == (byte)'e';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static uint Fnva32(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ ToLower(bytes[i])) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static float ParseFloat(ReadOnlySpan<byte> value)
        {
            value = Trim(value);
            if (value.Length == 0)
                return 0f;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float result = 0f;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                result = (result * 10f) + (value[index] - (byte)'0');
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    result += (value[index] - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                }
            }

            return math.isfinite(result) ? result * sign : 0f;
        }
    }
}
