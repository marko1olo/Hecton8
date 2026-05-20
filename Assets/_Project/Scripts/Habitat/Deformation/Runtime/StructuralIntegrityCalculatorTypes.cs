using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
// The AUP type is compiled by Hecton8.Core.asmdef even though its namespace is Hecton8.World.
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Habitat.Deformation
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct StructuralTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public float MaxPressureKPa;
        [FieldOffset(12)] public float MaxStress01;
        [FieldOffset(16)] public int ActiveNodeCount;
        [FieldOffset(20)] public int EdgeCount;
        [FieldOffset(24)] public int CriticalNodeCount;
        [FieldOffset(28)] public int CollapsedNodeCount;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public int FramesBetweenUpdates;
        [FieldOffset(40)] public float EstimatedMicroseconds;
        [FieldOffset(44)] public uint FaultFlags;
        [FieldOffset(48)] public uint WeakestNodeHash;
        [FieldOffset(52)] public float WeakestBucklingScalar;
        [FieldOffset(56)] public uint BaseHash;
        [FieldOffset(60)] public uint Sequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct StructuralMaterialStrengthEntry
    {
        [FieldOffset(0)] public uint MaterialHash;
        [FieldOffset(4)] public float BaseStrength;
        [FieldOffset(8)] public float BucklingStart01;
        [FieldOffset(12)] public float PressureScale;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StructuralTelemetryDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint EntrySize;
        [FieldOffset(16)] public int EntryCount;
        [FieldOffset(20)] public int Cursor;
        [FieldOffset(24)] public uint FaultFlags;
        [FieldOffset(28)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseIntegrityEventPayload : ISignal
    {
        public const byte SeverityWarn80 = 1;
        public const byte SeverityWarn90 = 2;
        public const byte SeverityCollapse = 3;

        [FieldOffset(0)] public AbsoluteUniversePosition NodeAup;
        [FieldOffset(48)] public uint NodeHash;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public float Stress01;
        [FieldOffset(60)] public byte Severity;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public ushort SourceId;
    }

    public static class StructuralIntegrityLayout
    {
        public static bool Validate()
        {
            bool sizeValid =
                   UnsafeUtility.SizeOf<IntegrityStateDTO>() == 32 &&
                   UnsafeUtility.SizeOf<StructuralTuningDTO>() == 96 &&
                   UnsafeUtility.SizeOf<StructuralTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<StructuralMaterialStrengthEntry>() == 16 &&
                   UnsafeUtility.SizeOf<StructuralTelemetryDumpHeader>() == 32 &&
                   UnsafeUtility.SizeOf<BaseIntegrityEventPayload>() == 64 &&
                   UnsafeUtility.SizeOf<AbsoluteUniversePosition>() == 48;
#if UNITY_EDITOR
            return sizeValid &&
                   ValidateIntegrityStateOffsets() &&
                   ValidateTuningOffsets() &&
                   ValidateTelemetryOffsets() &&
                   ValidateMaterialOffsets() &&
                   ValidateDumpHeaderOffsets() &&
                   ValidateEventPayloadOffsets() &&
                   ValidateAupOffsets();
#else
            return sizeValid;
#endif
        }

#if UNITY_EDITOR
        private static bool ValidateIntegrityStateOffsets()
        {
            return Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.NodeHash)) == 0 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.BaseStrength)) == 4 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.CurrentStress)) == 8 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.AppliedPressure)) == 12 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.Flags)) == 16 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.BucklingScalar)) == 20 &&
                   Offset<IntegrityStateDTO>("_pad0") == 24 &&
                   Offset<IntegrityStateDTO>("_pad7") == 31;
        }

        private static bool ValidateTuningOffsets()
        {
            return Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.SeaLevelAup)) == 0 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.SdfOriginAup)) == 24 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.BasePressureKPa)) == 48 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.PressureGradientKPaPerMeter)) == 52 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.PressureToStressScale)) == 56 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.MaterialStrengthFactor)) == 60 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.BucklingStart01)) == 64 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.BucklingVisualIntensity)) == 68 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.SupportDamping)) == 72 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.CollapseStress01)) == 76 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.GlobalQualityWeight)) == 80 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.SdfMetersPerVoxel)) == 84 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.SdfRangeMeters)) == 88 &&
                   Offset<StructuralTuningDTO>(nameof(StructuralTuningDTO.ActiveNodeCount)) == 92;
        }

        private static bool ValidateTelemetryOffsets()
        {
            return Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.Frame)) == 0 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.StateHash)) == 4 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.MaxPressureKPa)) == 8 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.MaxStress01)) == 12 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.ActiveNodeCount)) == 16 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.EdgeCount)) == 20 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.CriticalNodeCount)) == 24 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.CollapsedNodeCount)) == 28 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.GlobalQualityWeight)) == 32 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.FramesBetweenUpdates)) == 36 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.EstimatedMicroseconds)) == 40 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.FaultFlags)) == 44 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.WeakestNodeHash)) == 48 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.WeakestBucklingScalar)) == 52 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.BaseHash)) == 56 &&
                   Offset<StructuralTelemetryEntry>(nameof(StructuralTelemetryEntry.Sequence)) == 60;
        }

        private static bool ValidateMaterialOffsets()
        {
            return Offset<StructuralMaterialStrengthEntry>(nameof(StructuralMaterialStrengthEntry.MaterialHash)) == 0 &&
                   Offset<StructuralMaterialStrengthEntry>(nameof(StructuralMaterialStrengthEntry.BaseStrength)) == 4 &&
                   Offset<StructuralMaterialStrengthEntry>(nameof(StructuralMaterialStrengthEntry.BucklingStart01)) == 8 &&
                   Offset<StructuralMaterialStrengthEntry>(nameof(StructuralMaterialStrengthEntry.PressureScale)) == 12;
        }

        private static bool ValidateDumpHeaderOffsets()
        {
            return Offset<StructuralTelemetryDumpHeader>(nameof(StructuralTelemetryDumpHeader.Magic)) == 0 &&
                   Offset<StructuralTelemetryDumpHeader>(nameof(StructuralTelemetryDumpHeader.Version)) == 4 &&
                   Offset<StructuralTelemetryDumpHeader>(nameof(StructuralTelemetryDumpHeader.Frame)) == 8 &&
                   Offset<StructuralTelemetryDumpHeader>(nameof(StructuralTelemetryDumpHeader.EntrySize)) == 12 &&
                   Offset<StructuralTelemetryDumpHeader>(nameof(StructuralTelemetryDumpHeader.EntryCount)) == 16 &&
                   Offset<StructuralTelemetryDumpHeader>(nameof(StructuralTelemetryDumpHeader.Cursor)) == 20 &&
                   Offset<StructuralTelemetryDumpHeader>(nameof(StructuralTelemetryDumpHeader.FaultFlags)) == 24 &&
                   Offset<StructuralTelemetryDumpHeader>(nameof(StructuralTelemetryDumpHeader.StateHash)) == 28;
        }

        private static bool ValidateEventPayloadOffsets()
        {
            return Offset<BaseIntegrityEventPayload>(nameof(BaseIntegrityEventPayload.NodeAup)) == 0 &&
                   Offset<BaseIntegrityEventPayload>(nameof(BaseIntegrityEventPayload.NodeHash)) == 48 &&
                   Offset<BaseIntegrityEventPayload>(nameof(BaseIntegrityEventPayload.Frame)) == 52 &&
                   Offset<BaseIntegrityEventPayload>(nameof(BaseIntegrityEventPayload.Stress01)) == 56 &&
                   Offset<BaseIntegrityEventPayload>(nameof(BaseIntegrityEventPayload.Severity)) == 60 &&
                   Offset<BaseIntegrityEventPayload>(nameof(BaseIntegrityEventPayload.Flags)) == 61 &&
                   Offset<BaseIntegrityEventPayload>(nameof(BaseIntegrityEventPayload.SourceId)) == 62;
        }

        private static bool ValidateAupOffsets()
        {
            return Offset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridX)) == 0 &&
                   Offset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridY)) == 8 &&
                   Offset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridZ)) == 16 &&
                   Offset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalX)) == 24 &&
                   Offset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalY)) == 28 &&
                   Offset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalZ)) == 32 &&
                   Offset<AbsoluteUniversePosition>("_pad0") == 36 &&
                   Offset<AbsoluteUniversePosition>("_pad1") == 40;
        }

        private static int Offset<T>(string fieldName)
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct StructuralIntegrityClearJob : IJob
    {
        [WriteOnly] [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [WriteOnly] [NoAlias] public NativeArray<double3> NodeAups;
        [WriteOnly] [NoAlias] public NativeArray<int> CsrOffsets;
        [WriteOnly] [NoAlias] public NativeArray<int> CsrDestinations;
        [WriteOnly] [NoAlias] public NativeArray<byte> EdgeFlags;
        [WriteOnly] [NoAlias] public NativeArray<StructuralTelemetryEntry> Telemetry;
        [WriteOnly] [NoAlias] public NativeArray<int> TelemetryCursor;

        public void Execute()
        {
            Clear(States);
            Clear(NodeAups);
            Clear(CsrOffsets);
            Clear(CsrDestinations);
            Clear(EdgeFlags);
            Clear(Telemetry);
            Clear(TelemetryCursor);
        }

        private static void Clear<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated || array.Length == 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(ptr, (long)array.Length * UnsafeUtility.SizeOf<T>());
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockStructuralStressJob : IJob
    {
        [WriteOnly] [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [WriteOnly] [NoAlias] public NativeArray<double3> NodeAups;
        [WriteOnly] [NoAlias] public NativeArray<int> CsrOffsets;
        [WriteOnly] [NoAlias] public NativeArray<int> CsrDestinations;
        [WriteOnly] [NoAlias] public NativeArray<byte> EdgeFlags;
        [ReadOnly] [NoAlias] public NativeArray<StructuralMaterialStrengthEntry> Materials;

        public int NodeCount;
        public uint BaseHash;
        public double3 SeaLevelAup;
        public uint GlassHash;
        public uint TitaniumHash;
        public uint PlasteelHash;

        public void Execute()
        {
            int maxNodeCount = math.min(States.Length, math.min(NodeAups.Length, math.max(0, CsrOffsets.Length - 1)));
            int edgeCapacity = math.min(CsrDestinations.Length, EdgeFlags.Length);
            for (int i = 0; i < States.Length; i++)
                States[i] = default;
            for (int i = 0; i < NodeAups.Length; i++)
                NodeAups[i] = double3.zero;
            for (int i = 0; i < CsrOffsets.Length; i++)
                CsrOffsets[i] = 0;
            for (int i = 0; i < edgeCapacity; i++)
            {
                CsrDestinations[i] = 0;
                EdgeFlags[i] = 0;
            }

            if (maxNodeCount <= 0)
                return;

            int safeNodeCount = math.clamp(NodeCount, 1, maxNodeCount);
            int gridWidth = math.max(1, (int)math.ceil(math.sqrt((float)safeNodeCount)));
            int edgeCursor = 0;
            for (int i = 0; i < safeNodeCount; i++)
            {
                int x = i % gridWidth;
                int z = i / gridWidth;
                int materialSlot = i % 3;
                uint materialHash = materialSlot == 0 ? GlassHash : (materialSlot == 1 ? TitaniumHash : PlasteelHash);
                StructuralMaterialStrengthEntry material = ResolveMaterial(materialHash, materialSlot);
                float baseStrength = math.max(1f, material.BaseStrength);
                uint nodeHash = math.hash(new uint3((uint)i + 1u, BaseHash, materialHash));
                if (nodeHash == 0u)
                    nodeHash = (uint)i + 1u;

                CsrOffsets[i] = edgeCursor;
                double depthMeters = 90d + (z * 2.5d) + ((i & 15) * 0.75d);
                NodeAups[i] = SeaLevelAup + new double3((x - gridWidth * 0.5d) * 6d, -depthMeters, z * 6d);
                States[i] = new IntegrityStateDTO
                {
                    NodeHash = nodeHash,
                    BaseStrength = baseStrength,
                    CurrentStress = 0f,
                    AppliedPressure = 0f,
                    Flags = (i < gridWidth || (i % 17) == 0) ? StructuralIntegrityConstants.StateFlagAnchor : 0u,
                    BucklingScalar = 0f
                };

                AddEdge(i, i + 1, x + 1 < gridWidth && i + 1 < safeNodeCount, ref edgeCursor, edgeCapacity);
                AddEdge(i, i - 1, x > 0, ref edgeCursor, edgeCapacity);
                AddEdge(i, i + gridWidth, i + gridWidth < safeNodeCount, ref edgeCursor, edgeCapacity);
                AddEdge(i, i - gridWidth, z > 0, ref edgeCursor, edgeCapacity);
            }

            CsrOffsets[safeNodeCount] = edgeCursor;
            for (int i = safeNodeCount + 1; i < CsrOffsets.Length; i++)
                CsrOffsets[i] = edgeCursor;
        }

        private void AddEdge(int source, int destination, bool valid, ref int edgeCursor, int edgeCapacity)
        {
            if (!valid || edgeCursor >= edgeCapacity || destination < 0)
                return;

            CsrDestinations[edgeCursor] = destination;
            EdgeFlags[edgeCursor] = 0;
            edgeCursor++;
        }

        private StructuralMaterialStrengthEntry ResolveMaterial(uint hash, int fallbackSlot)
        {
            if (Materials.IsCreated && Materials.Length > 0)
            {
                int start = (int)(hash % (uint)Materials.Length);
                for (int probe = 0; probe < Materials.Length; probe++)
                {
                    StructuralMaterialStrengthEntry entry = Materials[WrapIndex(start + probe, Materials.Length)];
                    if (entry.MaterialHash == hash && math.isfinite(entry.BaseStrength) && entry.BaseStrength > 0f)
                        return entry;
                    if (entry.MaterialHash == 0u)
                        break;
                }
            }

            if (fallbackSlot == 0)
                return new StructuralMaterialStrengthEntry { MaterialHash = hash, BaseStrength = 420f, BucklingStart01 = 0.55f, PressureScale = 1.15f };
            if (fallbackSlot == 1)
                return new StructuralMaterialStrengthEntry { MaterialHash = hash, BaseStrength = 1220f, BucklingStart01 = 0.72f, PressureScale = 1f };
            return new StructuralMaterialStrengthEntry { MaterialHash = hash, BaseStrength = 2100f, BucklingStart01 = 0.82f, PressureScale = 0.85f };
        }

        private static int WrapIndex(int value, int length)
        {
            return (length & (length - 1)) == 0 ? (value & (length - 1)) : (value % length);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct StructuralDepthPressureJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<double3> NodeAups;
        [ReadOnly] [NoAlias] public NativeArray<StructuralTuningDTO> Tuning;
        public int ActiveNodeCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveNodeCount)
                return;

            StructuralTuningDTO tuning = Tuning[0];
            ref IntegrityStateDTO state = ref IntegrityStateDTO.AsRef(States, index);
            double3 depthDelta = tuning.SeaLevelAup - NodeAups[index];
            if (!math.all(math.isfinite(depthDelta)))
            {
                state.AppliedPressure = 0f;
                state.CurrentStress = 1f;
                state.BucklingScalar = 1f;
                state.Flags |= StructuralIntegrityConstants.StateFlagNonFinite;
                return;
            }

            const double maxStructuralDepthMeters = 1000000d;
            double rawDepthMeters = math.max(0d, depthDelta.y);
            if (!math.isfinite(rawDepthMeters) || rawDepthMeters > maxStructuralDepthMeters)
            {
                state.AppliedPressure = 0f;
                state.CurrentStress = 1f;
                state.BucklingScalar = 1f;
                state.Flags |= StructuralIntegrityConstants.StateFlagNonFinite;
                return;
            }

            float depthMeters = (float)rawDepthMeters;
            float basePressure = math.isfinite(tuning.BasePressureKPa) ? tuning.BasePressureKPa : 0f;
            float pressureGradient = math.isfinite(tuning.PressureGradientKPaPerMeter) ? tuning.PressureGradientKPaPerMeter : 0f;
            float pressure = basePressure + depthMeters * pressureGradient;
            if (!math.isfinite(pressure))
            {
                state.AppliedPressure = 0f;
                state.CurrentStress = 1f;
                state.BucklingScalar = 1f;
                state.Flags |= StructuralIntegrityConstants.StateFlagNonFinite;
                return;
            }

            state.AppliedPressure = math.max(0f, pressure);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct StructuralMaterialStrengthApplyJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<StructuralMaterialStrengthEntry> Materials;
        public int ActiveNodeCount;
        public uint GlassHash;
        public uint TitaniumHash;
        public uint PlasteelHash;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveNodeCount)
                return;

            int materialSlot = index % 3;
            uint materialHash = materialSlot == 0 ? GlassHash : (materialSlot == 1 ? TitaniumHash : PlasteelHash);
            StructuralMaterialStrengthEntry material = ResolveMaterial(materialHash);
            if (!math.isfinite(material.BaseStrength) || material.BaseStrength <= 0f)
                return;

            ref IntegrityStateDTO state = ref IntegrityStateDTO.AsRef(States, index);
            state.BaseStrength = material.BaseStrength;
        }

        private StructuralMaterialStrengthEntry ResolveMaterial(uint hash)
        {
            if (!Materials.IsCreated || Materials.Length == 0)
                return default;

            int start = (int)(hash % (uint)Materials.Length);
            for (int probe = 0; probe < Materials.Length; probe++)
            {
                StructuralMaterialStrengthEntry entry = Materials[WrapIndex(start + probe, Materials.Length)];
                if (entry.MaterialHash == hash)
                    return entry;
                if (entry.MaterialHash == 0u)
                    break;
            }

            return default;
        }

        private static int WrapIndex(int value, int length)
        {
            return (length & (length - 1)) == 0 ? (value & (length - 1)) : (value % length);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct StructuralSdfAnchorJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<double3> NodeAups;
        [ReadOnly] [NoAlias] public NativeArray<byte> VoxelSdfTexture3D;
        [ReadOnly] [NoAlias] public NativeArray<StructuralTuningDTO> Tuning;
        public int ActiveNodeCount;
        public int SdfDimension;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveNodeCount)
                return;

            ref IntegrityStateDTO state = ref IntegrityStateDTO.AsRef(States, index);
            uint flags = state.Flags & ~StructuralIntegrityConstants.StateFlagAnchor;
            bool anchored = false;

            long voxelCount = (long)SdfDimension * SdfDimension * SdfDimension;
            if (VoxelSdfTexture3D.IsCreated && SdfDimension > 1 && VoxelSdfTexture3D.Length >= voxelCount)
            {
                StructuralTuningDTO tuning = Tuning[0];
                float cellSize = math.max(0.01f, FiniteOr(tuning.SdfMetersPerVoxel, 1f));
                float range = math.max(0.01f, FiniteOr(tuning.SdfRangeMeters, 8f));
                double3 relativeD = NodeAups[index] - tuning.SdfOriginAup;
                if (!math.all(math.isfinite(relativeD)))
                {
                    state.CurrentStress = 1f;
                    state.BucklingScalar = 1f;
                    state.Flags = flags | StructuralIntegrityConstants.StateFlagNonFinite;
                    return;
                }

                double halfExtentMeters = math.min(math.max((double)SdfDimension * (double)cellSize * 0.5d, (double)cellSize), 1000000d);
                relativeD = math.clamp(relativeD, new double3(-halfExtentMeters), new double3(halfExtentMeters));
                float3 relative = new float3((float)relativeD.x, (float)relativeD.y, (float)relativeD.z);
                if (!math.all(math.isfinite(relative)))
                {
                    state.CurrentStress = 1f;
                    state.BucklingScalar = 1f;
                    state.Flags = flags | StructuralIntegrityConstants.StateFlagNonFinite;
                    return;
                }

                int3 voxel = (int3)math.floor(relative / cellSize + SdfDimension * 0.5f);
                int3 maxVoxel = new int3(SdfDimension - 1);
                voxel = math.clamp(voxel, int3.zero, maxVoxel);
                float nearestDistance = SampleSdfMeters(voxel, maxVoxel, range);
                float quality = math.saturate(FiniteOr(tuning.GlobalQualityWeight, 1f));
                float curvedQuality = quality * quality * (3f - 2f * quality);
                float highTapWeight = curvedQuality * math.smoothstep(0.25f, 0.75f, quality);
                float signedDistance = nearestDistance;
                if (highTapWeight > 0f)
                {
                    float crossTap =
                        nearestDistance * 2f +
                        SampleSdfMeters(voxel + new int3(1, 0, 0), maxVoxel, range) +
                        SampleSdfMeters(voxel + new int3(-1, 0, 0), maxVoxel, range) +
                        SampleSdfMeters(voxel + new int3(0, 1, 0), maxVoxel, range) +
                        SampleSdfMeters(voxel + new int3(0, -1, 0), maxVoxel, range) +
                        SampleSdfMeters(voxel + new int3(0, 0, 1), maxVoxel, range) +
                        SampleSdfMeters(voxel + new int3(0, 0, -1), maxVoxel, range);
                    signedDistance = math.lerp(nearestDistance, crossTap * 0.125f, highTapWeight);
                }

                anchored = signedDistance <= 0.5f;
            }
            else
            {
                anchored = (index & 15) == 0 || index < 16;
            }

            if (anchored)
                flags |= StructuralIntegrityConstants.StateFlagAnchor;
            state.Flags = flags;
        }

        private float SampleSdfMeters(int3 voxel, int3 maxVoxel, float range)
        {
            int3 v = math.clamp(voxel, int3.zero, maxVoxel);
            int sdfIndex = v.x + v.y * SdfDimension + v.z * SdfDimension * SdfDimension;
            float encoded = VoxelSdfTexture3D[sdfIndex] * (1f / 255f);
            return ((encoded * 2f) - 1f) * range;
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct StructuralGraphStressJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<int> CsrOffsets;
        [ReadOnly] [NoAlias] public NativeArray<int> CsrDestinations;
        [ReadOnly] [NoAlias] public NativeArray<byte> EdgeFlags;
        [ReadOnly] [NoAlias] public NativeArray<StructuralTuningDTO> Tuning;
        public int ActiveNodeCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveNodeCount || index + 1 >= CsrOffsets.Length)
                return;

            StructuralTuningDTO tuning = Tuning[0];
            ref IntegrityStateDTO state = ref IntegrityStateDTO.AsRef(States, index);
            uint flags = state.Flags;
            if ((flags & StructuralIntegrityConstants.StateFlagCollapsed) != 0)
            {
                float priorStress = math.isfinite(state.CurrentStress) ? state.CurrentStress : 1f;
                state.CurrentStress = math.max(1f, priorStress);
                state.BucklingScalar = 1f;
                return;
            }

            int edgeLimit = math.min(CsrDestinations.Length, EdgeFlags.Length);
            int start = CsrOffsets[index];
            int end = CsrOffsets[index + 1];
            start = math.clamp(start, 0, edgeLimit);
            end = math.clamp(end, start, edgeLimit);

            float support = (flags & StructuralIntegrityConstants.StateFlagAnchor) != 0 ? 2f : 0f;
            float collapsedNeighborLoad = 0f;
            int activeEdges = 0;
            for (int edge = start; edge < end; edge++)
            {
                if ((EdgeFlags[edge] & StructuralIntegrityConstants.EdgeFlagSevered) != 0)
                    continue;

                int destination = CsrDestinations[edge];
                if ((uint)destination >= (uint)ActiveNodeCount)
                    continue;

                IntegrityStateDTO neighbor = States[destination];
                activeEdges++;
                if ((neighbor.Flags & StructuralIntegrityConstants.StateFlagCollapsed) != 0)
                {
                    collapsedNeighborLoad += 0.18f;
                    continue;
                }

                if ((neighbor.Flags & StructuralIntegrityConstants.StateFlagAnchor) != 0)
                    support += 1f;
            }

            float baseStrengthRaw = FiniteOr(state.BaseStrength, 1f);
            float materialFactor = math.max(0.01f, FiniteOr(tuning.MaterialStrengthFactor, 1f));
            float supportDamping = math.max(0f, FiniteOr(tuning.SupportDamping, 0.45f));
            float pressureScale = math.max(0f, FiniteOr(tuning.PressureToStressScale, 1f));
            float pressure = math.max(0f, FiniteOr(state.AppliedPressure, 0f));
            float baseStrength = math.max(0.001f, baseStrengthRaw * materialFactor);
            float supportFactor = math.rcp(math.max(0.0001f, 1f + support * supportDamping));
            float pressureStress = pressure * pressureScale * supportFactor;
            float edgeStress = activeEdges > 0 ? activeEdges * 0.0125f : 0.035f;
            float stress = pressureStress / baseStrength + edgeStress + collapsedNeighborLoad;
            if (!math.isfinite(stress))
            {
                state.Flags = flags | StructuralIntegrityConstants.StateFlagNonFinite;
                state.CurrentStress = 1f;
                state.BucklingScalar = 1f;
                return;
            }

            float clampedStress = math.max(0f, stress);
            float buckleStart = math.saturate(FiniteOr(tuning.BucklingStart01, 0.72f));
            float buckleRange = math.max(0.0001f, 1f - buckleStart);
            float quality = math.saturate(FiniteOr(tuning.GlobalQualityWeight, 1f));
            float qualityCurve = quality * quality * (3f - 2f * quality);
            float visualScale = math.lerp(0.35f, 1.25f, qualityCurve);
            float buckling = math.saturate((clampedStress - buckleStart) / buckleRange) *
                             math.max(0f, FiniteOr(tuning.BucklingVisualIntensity, 1f)) *
                             visualScale;
            state.CurrentStress = clampedStress;
            state.BucklingScalar = math.isfinite(buckling) ? buckling : 1f;
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct StructuralCollapseSignalJob : IJob
    {
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<double3> NodeAups;
        [ReadOnly] [NoAlias] public NativeArray<StructuralTuningDTO> Tuning;
        [WriteOnly] [NoAlias] public NativeQueue<BaseIntegrityEventPayload>.ParallelWriter IntegrityEvents;
        [WriteOnly] [NoAlias] public NativeQueue<FluidIncursionSignal>.ParallelWriter FluidEvents;
        [WriteOnly] [NoAlias] public NativeQueue<BaseModuleCompromisedSignal>.ParallelWriter CompromisedEvents;
        public int ActiveNodeCount;
        public uint Frame;

        public void Execute()
        {
            if (!States.IsCreated || !NodeAups.IsCreated || !Tuning.IsCreated || Tuning.Length == 0)
                return;

            int safeCount = math.clamp(ActiveNodeCount, 0, math.min(States.Length, NodeAups.Length));
            for (int index = 0; index < safeCount; index++)
                ExecuteNode(index);
        }

        private void ExecuteNode(int index)
        {
            StructuralTuningDTO tuning = Tuning[0];
            ref IntegrityStateDTO state = ref IntegrityStateDTO.AsRef(States, index);
            float stress = math.isfinite(state.CurrentStress) ? math.max(0f, state.CurrentStress) : 1f;
            uint flags = state.Flags;
            double3 rawNodeAup = NodeAups[index];
            bool nodeAupFinite = math.all(math.isfinite(rawNodeAup));
            if (!nodeAupFinite)
            {
                flags |= StructuralIntegrityConstants.StateFlagNonFinite;
                stress = 1f;
            }

            double3 nodeAup = nodeAupFinite ? rawNodeAup : double3.zero;
            double3 relativeToSea = SafeDouble3(nodeAup - tuning.SeaLevelAup);
            byte severity = 0;

            if (stress >= 0.8f && (flags & StructuralIntegrityConstants.StateFlagWarn80Emitted) == 0)
            {
                flags |= StructuralIntegrityConstants.StateFlagWarn80Emitted;
                severity = BaseIntegrityEventPayload.SeverityWarn80;
            }

            if (stress >= 0.9f && (flags & StructuralIntegrityConstants.StateFlagWarn90Emitted) == 0)
            {
                flags |= StructuralIntegrityConstants.StateFlagWarn90Emitted;
                severity = BaseIntegrityEventPayload.SeverityWarn90;
            }

            bool collapsedNow = stress >= math.max(0.01f, tuning.CollapseStress01) || (flags & StructuralIntegrityConstants.StateFlagNonFinite) != 0;
            if (collapsedNow && (flags & StructuralIntegrityConstants.StateFlagCollapsed) == 0)
            {
                flags |= StructuralIntegrityConstants.StateFlagCollapsed;
                float priorBuckling = math.isfinite(state.BucklingScalar) ? state.BucklingScalar : 0f;
                state.BucklingScalar = math.max(priorBuckling, 1f);
                severity = BaseIntegrityEventPayload.SeverityCollapse;
            }

            if (severity != 0)
            {
                BaseIntegrityEventPayload evt = new BaseIntegrityEventPayload
                {
                    NodeAup = BuildAup(nodeAup),
                    NodeHash = state.NodeHash,
                    Frame = Frame,
                    Stress01 = math.saturate(stress),
                    Severity = severity,
                    Flags = (byte)(collapsedNow ? 1 : 0),
                    SourceId = (ushort)(StructuralIntegrityConstants.AgentHash & 0xFFFFu)
                };
                IntegrityEvents.Enqueue(evt);
            }

            if (stress >= 0.95f && (flags & StructuralIntegrityConstants.StateFlagLeakEmitted) == 0)
            {
                flags |= StructuralIntegrityConstants.StateFlagLeakEmitted;
                FluidIncursionSignal flood = new FluidIncursionSignal
                {
                    LeakAup = BuildAup(nodeAup),
                    CompartmentId = state.NodeHash,
                    FloodLevel01 = 1f,
                    FlowRate01 = math.saturate(stress),
                    Flags = 1
                };
                BaseModuleCompromisedSignal compromised = new BaseModuleCompromisedSignal
                {
                    ModuleCenter = ToFiniteFloat3(relativeToSea),
                    Stress01 = math.saturate(stress),
                    PeakStress01 = math.saturate(stress),
                    DepthMeters = SafePositiveSignalFloat(tuning.SeaLevelAup.y - nodeAup.y),
                    NodeId = state.NodeHash,
                    ModuleHash = StructuralIntegrityConstants.DefaultBaseHash,
                    Frame = Frame,
                    Sequence = (uint)index,
                    SourceId = (ushort)(StructuralIntegrityConstants.AgentHash & 0xFFFFu),
                    Flags = BaseModuleCompromisedSignal.MaxDeformationFlag,
                    StressIndex = (byte)math.min(255, index),
                    QualityTier = ResolveSignalQualityByte(tuning.GlobalQualityWeight)
                };
                FluidEvents.Enqueue(flood);
                CompromisedEvents.Enqueue(compromised);
            }

            state.Flags = flags;
        }

        private static byte ResolveSignalQualityByte(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            int quantized = (int)math.round(q * 255f);
            return (byte)math.clamp(quantized, 0, 255);
        }

        private static AbsoluteUniversePosition BuildAup(double3 absolute)
        {
            absolute = SafeDouble3(absolute);
            double cellSize = math.max(1d, (double)AbsoluteUniversePosition.CellSizeMeters);
            const double gridClamp = 1000000000d;
            double gridXD = math.clamp(math.floor(absolute.x / cellSize), -gridClamp, gridClamp);
            double gridYD = math.clamp(math.floor(absolute.y / cellSize), -gridClamp, gridClamp);
            double gridZD = math.clamp(math.floor(absolute.z / cellSize), -gridClamp, gridClamp);
            long gridX = (long)gridXD;
            long gridY = (long)gridYD;
            long gridZ = (long)gridZD;
            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = SafeSignalFloat(absolute.x - gridX * cellSize),
                LocalY = SafeSignalFloat(absolute.y - gridY * cellSize),
                LocalZ = SafeSignalFloat(absolute.z - gridZ * cellSize)
            };
        }

        private static float3 ToFiniteFloat3(double3 value)
        {
            value = SafeDouble3(value);
            return new float3(SafeSignalFloat(value.x), SafeSignalFloat(value.y), SafeSignalFloat(value.z));
        }

        private static double3 SafeDouble3(double3 value)
        {
            return math.all(math.isfinite(value)) ? value : double3.zero;
        }

        private static double SafeFinite(double value, double fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float SafeSignalFloat(double value)
        {
            const double signalClampMeters = 1000000d;
            double safe = SafeFinite(value, 0d);
            return (float)math.clamp(safe, -signalClampMeters, signalClampMeters);
        }

        private static float SafePositiveSignalFloat(double value)
        {
            const double signalClampMeters = 1000000d;
            double safe = SafeFinite(value, 0d);
            return (float)math.clamp(safe, 0d, signalClampMeters);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct StructuralEdgeSeverJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<int> CsrOffsets;
        [ReadOnly] [NoAlias] public NativeArray<int> CsrDestinations;
        [NoAlias] public NativeArray<byte> EdgeFlags;
        public int ActiveNodeCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveNodeCount || index + 1 >= CsrOffsets.Length)
                return;

            int edgeLimit = math.min(EdgeFlags.Length, CsrDestinations.Length);
            int start = math.clamp(CsrOffsets[index], 0, edgeLimit);
            int end = math.clamp(CsrOffsets[index + 1], start, edgeLimit);
            bool sourceCollapsed = (States[index].Flags & StructuralIntegrityConstants.StateFlagCollapsed) != 0;
            for (int edge = start; edge < end; edge++)
            {
                bool destinationCollapsed = false;
                if (!sourceCollapsed)
                {
                    int destination = CsrDestinations[edge];
                    destinationCollapsed = (uint)destination < (uint)ActiveNodeCount &&
                                           (States[destination].Flags & StructuralIntegrityConstants.StateFlagCollapsed) != 0;
                }

                if (sourceCollapsed || destinationCollapsed)
                    EdgeFlags[edge] = (byte)(EdgeFlags[edge] | StructuralIntegrityConstants.EdgeFlagSevered);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct StructuralTelemetryJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<int> CsrOffsets;
        [ReadOnly] [NoAlias] public NativeArray<StructuralTuningDTO> Tuning;
        [NoAlias] public NativeArray<StructuralTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public int ActiveNodeCount;
        public uint Frame;
        public int FramesBetweenUpdates;
        public float EstimatedMicroseconds;
        public uint BaseHash;
        public int SdfFallback;

        public void Execute()
        {
            if (!Telemetry.IsCreated || Telemetry.Length == 0 ||
                !TelemetryCursor.IsCreated || TelemetryCursor.Length == 0 ||
                !States.IsCreated || !CsrOffsets.IsCreated ||
                !Tuning.IsCreated || Tuning.Length == 0)
                return;

            StructuralTuningDTO tuning = Tuning[0];
            int maxNodeCount = math.min(States.Length, math.max(0, CsrOffsets.Length - 1));
            int safeCount = math.clamp(ActiveNodeCount, 0, maxNodeCount);
            float maxPressure = 0f;
            float maxStress = 0f;
            float weakestBuckle = 0f;
            int criticalCount = 0;
            int collapsedCount = 0;
            uint weakestHash = 0u;
            uint stateHash = 2166136261u;
            uint flags = SdfFallback != 0 ? StructuralIntegrityConstants.TelemetryFlagSdfFallback : 0u;

            for (int i = 0; i < safeCount; i++)
            {
                IntegrityStateDTO state = States[i];
                bool nonFiniteState = !math.isfinite(state.CurrentStress) ||
                                      !math.isfinite(state.AppliedPressure) ||
                                      !math.isfinite(state.BucklingScalar);
                float stress = math.isfinite(state.CurrentStress) ? state.CurrentStress : 1f;
                float pressure = math.isfinite(state.AppliedPressure) ? state.AppliedPressure : 0f;
                float buckling = math.isfinite(state.BucklingScalar) ? state.BucklingScalar : 1f;
                if (nonFiniteState)
                {
                    flags |= StructuralIntegrityConstants.TelemetryFlagNonFinite;
                }

                if ((state.Flags & StructuralIntegrityConstants.StateFlagNonFinite) != 0)
                    flags |= StructuralIntegrityConstants.TelemetryFlagNonFinite;
                if ((state.Flags & StructuralIntegrityConstants.StateFlagCollapsed) != 0)
                    collapsedCount++;
                if (stress >= 0.8f)
                    criticalCount++;
                if (stress > maxStress)
                {
                    maxStress = stress;
                    weakestHash = state.NodeHash;
                    weakestBuckle = buckling;
                }

                maxPressure = math.max(maxPressure, pressure);
                stateHash = Hash(stateHash, state.NodeHash);
                stateHash = Hash(stateHash, math.asuint(stress));
                stateHash = Hash(stateHash, math.asuint(pressure));
                stateHash = Hash(stateHash, math.asuint(buckling));
                stateHash = Hash(stateHash, state.Flags);
            }

            if (safeCount > 0 && collapsedCount * 2 >= safeCount)
                flags |= StructuralIntegrityConstants.TelemetryFlagMassCollapse;

            int capacity = math.min(Telemetry.Length, StructuralIntegrityConstants.TelemetryFrameCapacity);
            int cursor = TelemetryCursor[0];
            if (cursor < 0)
                cursor = 0;
            cursor %= capacity;
            int slot = cursor;
            int edgeCount = safeCount >= 0 && safeCount < CsrOffsets.Length ? math.max(0, CsrOffsets[safeCount]) : 0;
            Telemetry[slot] = new StructuralTelemetryEntry
            {
                Frame = Frame,
                StateHash = stateHash,
                MaxPressureKPa = maxPressure,
                MaxStress01 = maxStress,
                ActiveNodeCount = safeCount,
                EdgeCount = edgeCount,
                CriticalNodeCount = criticalCount,
                CollapsedNodeCount = collapsedCount,
                GlobalQualityWeight = tuning.GlobalQualityWeight,
                FramesBetweenUpdates = FramesBetweenUpdates,
                EstimatedMicroseconds = EstimatedMicroseconds,
                FaultFlags = flags,
                WeakestNodeHash = weakestHash,
                WeakestBucklingScalar = weakestBuckle,
                BaseHash = BaseHash,
                Sequence = (uint)cursor
            };
            TelemetryCursor[0] = (cursor + 1) % capacity;
        }

        private static uint Hash(uint hash, uint value)
        {
            hash = (hash ^ value) * 16777619u;
            return hash;
        }
    }
}
