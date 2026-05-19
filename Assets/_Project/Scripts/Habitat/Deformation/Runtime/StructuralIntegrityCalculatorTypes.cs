using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Habitat.Deformation
{
    public static class StructuralIntegrityConstants
    {
        public const int MaxNodeCapacity = 4096;
        public const int MaxEdgeCapacity = MaxNodeCapacity * 4;
        public const int TelemetryFrameCapacity = 300;
        public const int MaterialStrengthCapacity = 32;
        public const int CsvScratchBytes = 16 * 1024;
        public const uint AgentHash = 0x73313135u; // s115
        public const uint DefaultBaseHash = 0x53313135u; // S115
        public const uint SignalLaneHash = 0x53494331u; // SIC1
        public const uint DumpMagic = 0x53494344u; // SICD
        public const uint DumpVersion = 1u;

        public const uint StateFlagAnchor = 1u << 0;
        public const uint StateFlagCollapsed = 1u << 1;
        public const uint StateFlagLeakEmitted = 1u << 2;
        public const uint StateFlagWarn80Emitted = 1u << 3;
        public const uint StateFlagWarn90Emitted = 1u << 4;
        public const uint StateFlagNonFinite = 1u << 31;

        public const byte EdgeFlagSevered = 1 << 0;

        public const uint TelemetryFlagNonFinite = 1u << 0;
        public const uint TelemetryFlagMassCollapse = 1u << 1;
        public const uint TelemetryFlagSdfFallback = 1u << 2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public unsafe struct IntegrityStateDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float BaseStrength;
        [FieldOffset(8)] public float CurrentStress;
        [FieldOffset(12)] public float AppliedPressure;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float BucklingScalar;
        [FieldOffset(24)] private byte _pad0;
        [FieldOffset(25)] private byte _pad1;
        [FieldOffset(26)] private byte _pad2;
        [FieldOffset(27)] private byte _pad3;
        [FieldOffset(28)] private byte _pad4;
        [FieldOffset(29)] private byte _pad5;
        [FieldOffset(30)] private byte _pad6;
        [FieldOffset(31)] private byte _pad7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref IntegrityStateDTO AsRef(NativeArray<IntegrityStateDTO> states, int index)
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(states);
            return ref UnsafeUtility.AsRef<IntegrityStateDTO>((byte*)basePtr + (index * UnsafeUtility.SizeOf<IntegrityStateDTO>()));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct StructuralTuningDTO
    {
        [FieldOffset(0)] public double3 SeaLevelAup;
        [FieldOffset(24)] public double3 SdfOriginAup;
        [FieldOffset(48)] public float BasePressureKPa;
        [FieldOffset(52)] public float PressureGradientKPaPerMeter;
        [FieldOffset(56)] public float PressureToStressScale;
        [FieldOffset(60)] public float MaterialStrengthFactor;
        [FieldOffset(64)] public float BucklingStart01;
        [FieldOffset(68)] public float BucklingVisualIntensity;
        [FieldOffset(72)] public float SupportDamping;
        [FieldOffset(76)] public float CollapseStress01;
        [FieldOffset(80)] public float GlobalQualityWeight;
        [FieldOffset(84)] public float SdfMetersPerVoxel;
        [FieldOffset(88)] public float SdfRangeMeters;
        [FieldOffset(92)] public int ActiveNodeCount;
    }

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
                   UnsafeUtility.SizeOf<BaseIntegrityEventPayload>() == 64;
#if UNITY_EDITOR
            return sizeValid &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.NodeHash)) == 0 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.BaseStrength)) == 4 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.CurrentStress)) == 8 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.AppliedPressure)) == 12 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.Flags)) == 16 &&
                   Offset<IntegrityStateDTO>(nameof(IntegrityStateDTO.BucklingScalar)) == 20;
#else
            return sizeValid;
#endif
        }

#if UNITY_EDITOR
        private static int Offset<T>(string fieldName)
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct StructuralIntegrityClearJob : IJob
    {
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [NoAlias] public NativeArray<double3> NodeAups;
        [NoAlias] public NativeArray<int> CsrOffsets;
        [NoAlias] public NativeArray<int> CsrDestinations;
        [NoAlias] public NativeArray<byte> EdgeFlags;
        [NoAlias] public NativeArray<StructuralTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;

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
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [NoAlias] public NativeArray<double3> NodeAups;
        [NoAlias] public NativeArray<int> CsrOffsets;
        [NoAlias] public NativeArray<int> CsrDestinations;
        [NoAlias] public NativeArray<byte> EdgeFlags;
        [ReadOnly] [NoAlias] public NativeArray<StructuralMaterialStrengthEntry> Materials;

        public int NodeCount;
        public uint BaseHash;
        public double3 SeaLevelAup;
        public uint GlassHash;
        public uint TitaniumHash;
        public uint PlasteelHash;

        public void Execute()
        {
            int safeNodeCount = math.clamp(NodeCount, 1, math.min(States.Length, math.min(NodeAups.Length, CsrOffsets.Length - 1)));
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
            float depthMeters = (float)math.max(0d, depthDelta.y);
            float pressure = tuning.BasePressureKPa + depthMeters * tuning.PressureGradientKPaPerMeter;
            state.AppliedPressure = math.isfinite(pressure) ? math.max(0f, pressure) : 0f;
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

            if (VoxelSdfTexture3D.IsCreated && SdfDimension > 1 && VoxelSdfTexture3D.Length >= SdfDimension * SdfDimension * SdfDimension)
            {
                StructuralTuningDTO tuning = Tuning[0];
                float cellSize = math.max(0.01f, FiniteOr(tuning.SdfMetersPerVoxel, 1f));
                float range = math.max(0.01f, FiniteOr(tuning.SdfRangeMeters, 8f));
                double3 relativeD = NodeAups[index] - tuning.SdfOriginAup;
                float3 relative = new float3((float)relativeD.x, (float)relativeD.y, (float)relativeD.z);
                int3 voxel = (int3)math.floor(relative / cellSize + SdfDimension * 0.5f);
                int3 maxVoxel = new int3(SdfDimension - 1);
                voxel = math.clamp(voxel, int3.zero, maxVoxel);
                float nearestDistance = SampleSdfMeters(voxel, maxVoxel, range);
                float quality = math.saturate(FiniteOr(tuning.GlobalQualityWeight, 1f));
                float curvedQuality = quality * quality * (3f - 2f * quality);
                float highTapWeight = curvedQuality * math.step(0.3f, quality);
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
            if ((uint)index >= (uint)ActiveNodeCount)
                return;

            StructuralTuningDTO tuning = Tuning[0];
            ref IntegrityStateDTO state = ref IntegrityStateDTO.AsRef(States, index);
            uint flags = state.Flags;
            if ((flags & StructuralIntegrityConstants.StateFlagCollapsed) != 0)
            {
                state.CurrentStress = math.max(1f, state.CurrentStress);
                state.BucklingScalar = 1f;
                return;
            }

            int start = CsrOffsets[index];
            int end = CsrOffsets[index + 1];
            start = math.clamp(start, 0, CsrDestinations.Length);
            end = math.clamp(end, start, CsrDestinations.Length);

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
    internal struct StructuralCollapseSignalJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<double3> NodeAups;
        [ReadOnly] [NoAlias] public NativeArray<StructuralTuningDTO> Tuning;
        [WriteOnly] [NoAlias] public NativeQueue<BaseIntegrityEventPayload>.ParallelWriter IntegrityEvents;
        [WriteOnly] [NoAlias] public NativeQueue<FluidIncursionSignal>.ParallelWriter FluidEvents;
        [WriteOnly] [NoAlias] public NativeQueue<BaseModuleCompromisedSignal>.ParallelWriter CompromisedEvents;
        public int ActiveNodeCount;
        public uint Frame;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveNodeCount)
                return;

            StructuralTuningDTO tuning = Tuning[0];
            ref IntegrityStateDTO state = ref IntegrityStateDTO.AsRef(States, index);
            float stress = math.isfinite(state.CurrentStress) ? math.max(0f, state.CurrentStress) : 1f;
            uint flags = state.Flags;
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
                state.BucklingScalar = math.max(state.BucklingScalar, 1f);
                severity = BaseIntegrityEventPayload.SeverityCollapse;
            }

            if (severity != 0)
            {
                BaseIntegrityEventPayload evt = new BaseIntegrityEventPayload
                {
                    NodeAup = BuildAup(NodeAups[index]),
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
                    LeakAup = BuildAup(NodeAups[index]),
                    CompartmentId = state.NodeHash,
                    FloodLevel01 = 1f,
                    FlowRate01 = math.saturate(stress),
                    Flags = 1
                };
                BaseModuleCompromisedSignal compromised = new BaseModuleCompromisedSignal
                {
                    ModuleCenter = ToFloat3(NodeAups[index] - tuning.SeaLevelAup),
                    Stress01 = math.saturate(stress),
                    PeakStress01 = math.saturate(stress),
                    DepthMeters = (float)math.max(0d, tuning.SeaLevelAup.y - NodeAups[index].y),
                    NodeId = state.NodeHash,
                    ModuleHash = StructuralIntegrityConstants.DefaultBaseHash,
                    Frame = Frame,
                    Sequence = (uint)index,
                    SourceId = (ushort)(StructuralIntegrityConstants.AgentHash & 0xFFFFu),
                    Flags = BaseModuleCompromisedSignal.MaxDeformationFlag,
                    StressIndex = (byte)math.min(255, index),
                    QualityTier = ResolveSignalProfileByte(tuning.GlobalQualityWeight)
                };
                FluidEvents.Enqueue(flood);
                CompromisedEvents.Enqueue(compromised);
            }

            state.Flags = flags;
        }

        private static byte ResolveSignalProfileByte(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            int profile = (int)math.step(0.5f, q);
            return (byte)math.clamp(profile, (int)ScalabilityTierProfiles.LowMx350, (int)ScalabilityTierProfiles.HighRtx);
        }

        private static AbsoluteUniversePosition BuildAup(double3 absolute)
        {
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            long gridX = (long)math.floor(absolute.x / cellSize);
            long gridY = (long)math.floor(absolute.y / cellSize);
            long gridZ = (long)math.floor(absolute.z / cellSize);
            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolute.x - gridX * cellSize),
                LocalY = (float)(absolute.y - gridY * cellSize),
                LocalZ = (float)(absolute.z - gridZ * cellSize)
            };
        }

        private static float3 ToFloat3(double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
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
            if ((uint)index >= (uint)ActiveNodeCount)
                return;

            int start = math.clamp(CsrOffsets[index], 0, EdgeFlags.Length);
            int end = math.clamp(CsrOffsets[index + 1], start, EdgeFlags.Length);
            bool sourceCollapsed = (States[index].Flags & StructuralIntegrityConstants.StateFlagCollapsed) != 0;
            for (int edge = start; edge < end; edge++)
            {
                bool destinationCollapsed = false;
                if (!sourceCollapsed && edge < CsrDestinations.Length)
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
            if (!Telemetry.IsCreated || Telemetry.Length == 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length == 0)
                return;

            StructuralTuningDTO tuning = Tuning[0];
            int safeCount = math.clamp(ActiveNodeCount, 0, States.Length);
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
                if (!math.isfinite(state.CurrentStress) ||
                    !math.isfinite(state.AppliedPressure) ||
                    !math.isfinite(state.BucklingScalar))
                {
                    flags |= StructuralIntegrityConstants.TelemetryFlagNonFinite;
                }

                if ((state.Flags & StructuralIntegrityConstants.StateFlagNonFinite) != 0)
                    flags |= StructuralIntegrityConstants.TelemetryFlagNonFinite;
                if ((state.Flags & StructuralIntegrityConstants.StateFlagCollapsed) != 0)
                    collapsedCount++;
                if (state.CurrentStress >= 0.8f)
                    criticalCount++;
                if (state.CurrentStress > maxStress)
                {
                    maxStress = state.CurrentStress;
                    weakestHash = state.NodeHash;
                    weakestBuckle = state.BucklingScalar;
                }

                maxPressure = math.max(maxPressure, state.AppliedPressure);
                stateHash = Hash(stateHash, state.NodeHash);
                stateHash = Hash(stateHash, math.asuint(state.CurrentStress));
                stateHash = Hash(stateHash, math.asuint(state.AppliedPressure));
                stateHash = Hash(stateHash, state.Flags);
            }

            if (safeCount > 0 && collapsedCount * 2 >= safeCount)
                flags |= StructuralIntegrityConstants.TelemetryFlagMassCollapse;

            int cursor = TelemetryCursor[0];
            int slot = math.abs(cursor) % StructuralIntegrityConstants.TelemetryFrameCapacity;
            int edgeCount = safeCount >= 0 && safeCount < CsrOffsets.Length ? CsrOffsets[safeCount] : 0;
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
            TelemetryCursor[0] = (cursor + 1) % StructuralIntegrityConstants.TelemetryFrameCapacity;
        }

        private static uint Hash(uint hash, uint value)
        {
            hash = (hash ^ value) * 16777619u;
            return hash;
        }
    }
}
