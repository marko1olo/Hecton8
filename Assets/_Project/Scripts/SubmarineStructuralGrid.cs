using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.VFX;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Hecton8.Physics
{
    /// <summary>
    /// Fixed-step voxelized hull integrity grid with Burst-distributed impact diffusion and double-buffered breach publication.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Submarine Structural Grid")]
    public sealed class SubmarineStructuralGrid : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, ISlowTickable, Hecton8.Gameplay.IDamageSignalReceiver, ISubmarineHullBreachReadModel, ISubmarineDamageControlTarget, ISubmarineRepairRoomResolver, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DirectSignalPushDropCount_SubmarineStructuralGrid;

        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.FixedTick");
        private static readonly ProfilerMarker _damageScheduleProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.Damage.Schedule");
        private static readonly ProfilerMarker _damageConsumeProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.Damage.Consume");
        private static readonly ProfilerMarker _breachRepairProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.BreachRepair");
        private static readonly int _ShaderCrushCenterRadiusId = Shader.PropertyToID("_HectonSubmarineCrushCenterRadius");
        private static readonly int _ShaderCrushDepthParamsId = Shader.PropertyToID("_HectonSubmarineCrushDepthParams");
        private static readonly int _LeakBreachBufferId = Shader.PropertyToID("_BreachBuffer");
        private static readonly int _LeakParticleBufferId = Shader.PropertyToID("_LeakPlumeParticleBuffer");
        private static readonly int _LeakBreachCountId = Shader.PropertyToID("_BreachCount");
        private static readonly int _LeakVisibleBreachCountId = Shader.PropertyToID("_VisibleBreachCount");
        private static readonly int _LeakDeltaTimeId = Shader.PropertyToID("_DeltaTimeSeconds");
        private static readonly int _LeakTimeId = Shader.PropertyToID("_TimeSeconds");
        private static readonly int _LeakParamsId = Shader.PropertyToID("_LeakParams");
        private static readonly int _LeakUseParticleBufferId = Shader.PropertyToID("_UseLeakParticleBuffer");
        private static readonly int _LeakParticleSizeId = Shader.PropertyToID("_LeakPlumeParticleSize");
        private static readonly int _LeakLocalToWorldId = Shader.PropertyToID("_SubmarineLocalToWorld");
        private static readonly int _LeakCameraRightId = Shader.PropertyToID("_CameraRightWS");
        private static readonly int _LeakCameraUpId = Shader.PropertyToID("_CameraUpWS");

        private const int CompartmentCapacity = 8;
        private const int MaxQueuedImpacts = 16;
        private const int MaxActiveBreaches = 64;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const int DeferredBreachAddCapacity = 16;
        private const int MinVisibleBreachLimit = 8;
        private const int LeakPlumeParticleCapacity = MaxActiveBreaches * 4;
        private const int DamageControlTelemetryCapacity = 300;
        private const byte FullIntegrity = byte.MaxValue;
        private const byte UnmappedCompartment = byte.MaxValue;
        private const float DefaultMinimumImpactRadiusMeters = 0.45f;
        private const float DefaultImpactRadiusPerMeterPerSecond = 0.035f;
        private const float DefaultMinimumSigmaMeters = 0.2f;
        private const float DefaultSigmaScale = 0.45f;
        private const float DefaultImpactSpeedToCellDamageScale = 9f;
        private const float DefaultIntegrityByteToCellDamageScale = 1.15f;
        private const float DefaultFatiguePressureThresholdKPa = 150f;
        private const byte DefaultFatigueIntegrityLossPerCycle = 4;
        private const float DefaultCompressionDepthThresholdMeters = 3000f;
        private const float DefaultCompressionFullPressureKPa = 60000f;
        private const float DefaultMaximumVolumeCompressionNormalized = 0.15f;
        private const float RecentImpactSeverityDecayPerSecond = 2.8f;
        private const float BreachMergeRadiusMeters = 0.75f;
        private const float BreachRepairRadiusMeters = 1f;
        private const float DefaultPressureToBreachSeverityScale = 0.00025f;
        private const float DefaultRepairUnitsToBreachSeverityScale = 0.01f;
        private const float DefaultLeakAudioCadenceSeconds = 0.35f;
        private const uint CriticalBreachWarningHash = 0x43524B4Cu;
        private const byte LeakImpactFlags = 0x20;
        private const float CriticalBreachThreshold = 0.9f;
        private const float CriticalBreachWarningCadenceSeconds = 1.5f;
        private const uint DamageControlTelemetryInvalidFlag = 1u;
        private const uint DamageControlTelemetryRepairJobInFlightFlag = 2u;
        private const uint DamageControlTelemetryCompactionFenceFlag = 4u;
        private const uint DamageControlTelemetryWriteLockFailureFlag = 8u;
        private const uint DamageControlTelemetryBufferLockFailureFlag = 16u;
        private const uint DamageControlTelemetryStaleHandleFlag = 32u;
        private const uint DamageControlTelemetryCapacityFailureFlag = 64u;
        private const ushort FailureCodeNone = 0;
        private const ushort FailureCodeInvalidState = 1;
        private const ushort FailureCodeWriteLock = 2;
        private const ushort FailureCodeBufferLock = 3;
        private const ushort FailureCodeCompactionFence = 4;
        private const ushort FailureCodeStaleHandle = 5;
        private const ushort FailureCodeCapacityMismatch = 6;
        private const uint HullDentVisualDamageType = 3u;
        private const uint HullDentVisualSourceHash = 0xD3CA0149u;
        private const float DefaultLeakPlumeParticleSizeMeters = 0.18f;
        private const float DefaultLeakPlumeRenderBoundsPaddingMeters = 4f;
        private const float LeakPlumeClockMaxSeconds = 16777215f;
        private const float Epsilon = 0.0001f;
        private const double AupLocalCastClampMeters = 100000.0;
        private const string LeakPlumeComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_LeakPlume.compute";
        private const string LeakPlumeMaterialAssetPath = "Assets/_Project/Art/Materials/VFX/Mat_LeakPlume.mat";
        private const SystemID VaultOwnerSystemId = SystemID.VehiclesPhysics;
        private const ulong StructuralMutationGuardMask = 1UL << 46;

        private const int LockCellIntegrityFront = 1 << 0;
        private const int LockCellIntegrityBack = 1 << 1;
        private const int LockCellFatigue = 1 << 2;
        private const int LockCellCompartmentIndices = 1 << 3;
        private const int LockHullBreachMaskFront = 1 << 4;
        private const int LockHullBreachMaskBack = 1 << 5;
        private const int LockCompartmentBreachAreasFront = 1 << 6;
        private const int LockCompartmentBreachAreasBack = 1 << 7;
        private const int LockQueuedImpacts = 1 << 8;
        private const int LockScheduledImpacts = 1 << 9;
        private const int LockCompartmentCentroids = 1 << 10;
        private const int LockFatigueCompartmentFlags = 1 << 11;
        private const int LockFatigueIntegrityLossPerCycle = 1 << 12;
        private const int LockFatiguePeakResult = 1 << 13;
        private const int LockBreachSeveritySumResult = 1 << 14;
        private const int LockBreaches = 1 << 15;
        private const int LockStructuralJobMutationGuard = 1 << 30;

        private static class StructuralGridVaultRoute
        {
            public const BufferID CellIntegrityFront = BufferID.SubmarineStructuralGrid_CellIntegrityFront;
            public const BufferID CellIntegrityBack = BufferID.SubmarineStructuralGrid_CellIntegrityBack;
            public const BufferID CellFatigue = BufferID.SubmarineStructuralGrid_CellFatigue;
            public const BufferID CellCompartmentIndices = BufferID.SubmarineStructuralGrid_CellCompartmentIndices;
            public const BufferID HullBreachMaskFront = BufferID.SubmarineStructuralGrid_HullBreachMaskFront;
            public const BufferID HullBreachMaskBack = BufferID.SubmarineStructuralGrid_HullBreachMaskBack;
            public const BufferID CompartmentBreachAreasFront = BufferID.SubmarineStructuralGrid_CompartmentBreachAreasFront;
            public const BufferID CompartmentBreachAreasBack = BufferID.SubmarineStructuralGrid_CompartmentBreachAreasBack;
            public const BufferID QueuedImpacts = BufferID.SubmarineStructuralGrid_QueuedImpacts;
            public const BufferID ScheduledImpacts = BufferID.SubmarineStructuralGrid_ScheduledImpacts;
            public const BufferID CompartmentCentroids = BufferID.SubmarineStructuralGrid_CompartmentCentroids;
            public const BufferID FatigueCompartmentFlags = BufferID.SubmarineStructuralGrid_FatigueCompartmentFlags;
            public const BufferID FatigueIntegrityLossPerCycle = BufferID.SubmarineStructuralGrid_FatigueIntegrityLossPerCycle;
            public const BufferID FatiguePeakResult = BufferID.SubmarineStructuralGrid_FatiguePeakResult;
            public const BufferID BreachSeveritySumResult = BufferID.SubmarineStructuralGrid_BreachSeveritySumResult;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullDamageDiffusionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> InputIntegrity;
            [ReadOnly, NoAlias] public NativeArray<byte> CellCompartmentIndices;
            [ReadOnly, NoAlias] public NativeArray<ImpactCommand> Impacts;

            [NoAlias] public NativeArray<byte> OutputIntegrity;
            [NoAlias] public NativeArray<ulong> OutputBreachMaskWords;
            [NoAlias] public NativeArray<float> OutputCompartmentBreachAreas;

            public int GridWidth;
            public int GridHeight;
            public int GridDepth;
            public int CellCount;
            public int ImpactCount;
            public float3 GridCenterLocal;
            public float3 GridSizeLocal;
            public float CellBreachAreaSquareMeters;

            public void Execute()
            {
                for (int i = 0; i < CellCount; i++)
                    OutputIntegrity[i] = InputIntegrity[i];

                for (int i = 0; i < OutputBreachMaskWords.Length; i++)
                    OutputBreachMaskWords[i] = 0UL;

                for (int i = 0; i < OutputCompartmentBreachAreas.Length; i++)
                    OutputCompartmentBreachAreas[i] = 0f;

                float3 cellSize = new float3(
                    GridWidth > 0 ? GridSizeLocal.x / GridWidth : 0f,
                    GridHeight > 0 ? GridSizeLocal.y / GridHeight : 0f,
                    GridDepth > 0 ? GridSizeLocal.z / GridDepth : 0f);

                float3 gridMin = GridCenterLocal - (GridSizeLocal * 0.5f);

                for (int impactIndex = 0; impactIndex < ImpactCount; impactIndex++)
                {
                    ImpactCommand impact = Impacts[impactIndex];
                    float impactActive = math.select(0f, 1f, impact.DamageBytes > 0 && impact.RadiusMeters > Epsilon);
                    float sigmaMeters = math.max(impact.SigmaMeters, Epsilon);
                    float invTwoSigmaSq = 1f / (2f * sigmaMeters * sigmaMeters);
                    float radiusSq = impact.RadiusMeters * impact.RadiusMeters;

                    int minX = math.max(0, (int)math.floor((impact.LocalPoint.x - impact.RadiusMeters - gridMin.x) / math.max(cellSize.x, Epsilon)));
                    int maxX = math.min(GridWidth - 1, (int)math.floor((impact.LocalPoint.x + impact.RadiusMeters - gridMin.x) / math.max(cellSize.x, Epsilon)));
                    int minY = math.max(0, (int)math.floor((impact.LocalPoint.y - impact.RadiusMeters - gridMin.y) / math.max(cellSize.y, Epsilon)));
                    int maxY = math.min(GridHeight - 1, (int)math.floor((impact.LocalPoint.y + impact.RadiusMeters - gridMin.y) / math.max(cellSize.y, Epsilon)));
                    int minZ = math.max(0, (int)math.floor((impact.LocalPoint.z - impact.RadiusMeters - gridMin.z) / math.max(cellSize.z, Epsilon)));
                    int maxZ = math.min(GridDepth - 1, (int)math.floor((impact.LocalPoint.z + impact.RadiusMeters - gridMin.z) / math.max(cellSize.z, Epsilon)));

                    for (int z = minZ; z <= maxZ; z++)
                    {
                        float localZ = gridMin.z + ((z + 0.5f) * cellSize.z);
                        for (int y = minY; y <= maxY; y++)
                        {
                            float localY = gridMin.y + ((y + 0.5f) * cellSize.y);
                            int yzBase = (z * GridHeight + y) * GridWidth;
                            for (int x = minX; x <= maxX; x++)
                            {
                                float localX = gridMin.x + ((x + 0.5f) * cellSize.x);
                                float3 delta = new float3(localX, localY, localZ) - impact.LocalPoint;
                                float distSq = math.lengthsq(delta);
                                int cellIndex = yzBase + x;
                                int currentIntegrity = OutputIntegrity[cellIndex];
                                float activeCell = impactActive *
                                                   math.select(0f, 1f, distSq <= radiusSq) *
                                                   math.select(0f, 1f, currentIntegrity > 0);
                                float weight = ApproximateExpNegPositive(distSq * invTwoSigmaSq);
                                int damage = (int)math.round(impact.DamageBytes * weight * activeCell);
                                OutputIntegrity[cellIndex] = (byte)math.max(0, currentIntegrity - damage);
                            }
                        }
                    }
                }

                for (int cellIndex = 0; cellIndex < CellCount; cellIndex++)
                {
                    int breached = math.select(0, 1, OutputIntegrity[cellIndex] <= 0);
                    int wordIndex = cellIndex >> 6;
                    int bitIndex = cellIndex & 63;
                    ulong breachBit = (1UL << bitIndex) & unchecked((ulong)-(long)breached);
                    OutputBreachMaskWords[wordIndex] |= breachBit;

                    byte compartmentIndex = CellCompartmentIndices[cellIndex];
                    int safeCompartmentIndex = math.clamp((int)compartmentIndex, 0, OutputCompartmentBreachAreas.Length - 1);
                    float compartmentWrite = CellBreachAreaSquareMeters *
                                             breached *
                                             math.select(0f, 1f, compartmentIndex < OutputCompartmentBreachAreas.Length);
                    OutputCompartmentBreachAreas[safeCompartmentIndex] += compartmentWrite;
                }
            }

            private static float ApproximateExpNegPositive(float x)
            {
                float clamped = math.clamp(x, 0f, 8f);
                float x2 = clamped * clamped;
                float x3 = x2 * clamped;
                float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
                float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
                return math.saturate(numerator / math.max(denominator, Epsilon));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullCompartmentMappingJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float3> CompartmentCentroids;
            [NoAlias] public NativeArray<byte> CellCompartmentIndices;

            public int CompartmentCount;
            public int GridWidth;
            public int GridHeight;
            public int GridDepth;
            public float3 GridCenterLocal;
            public float3 GridSizeLocal;

            public void Execute(int cellIndex)
            {
                int x = cellIndex % GridWidth;
                int yz = cellIndex / GridWidth;
                int y = yz % GridHeight;
                int z = yz / GridHeight;
                float3 gridMin = GridCenterLocal - (GridSizeLocal * 0.5f);
                float3 cellSize = new float3(
                    GridWidth > 0 ? GridSizeLocal.x / GridWidth : 0f,
                    GridHeight > 0 ? GridSizeLocal.y / GridHeight : 0f,
                    GridDepth > 0 ? GridSizeLocal.z / GridDepth : 0f);
                float3 cellLocalPoint = gridMin + (new float3(x + 0.5f, y + 0.5f, z + 0.5f) * cellSize);
                int nearestIndex = UnmappedCompartment;
                float nearestDistanceSq = float.MaxValue;
                int safeCompartmentCount = math.max(0, CompartmentCount);

                for (int compartmentIndex = 0; compartmentIndex < safeCompartmentCount; compartmentIndex++)
                {
                    float distanceSq = math.lengthsq(cellLocalPoint - CompartmentCentroids[compartmentIndex]);
                    bool closer = distanceSq < nearestDistanceSq;
                    nearestDistanceSq = math.select(nearestDistanceSq, distanceSq, closer);
                    nearestIndex = math.select(nearestIndex, compartmentIndex, closer);
                }

                CellCompartmentIndices[cellIndex] = (byte)math.select((int)UnmappedCompartment, nearestIndex, safeCompartmentCount > 0);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullFatigueCompartmentJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> CellCompartmentIndices;
            [NoAlias] public NativeArray<byte> CellIntegrityFront;
            [NoAlias] public NativeArray<byte> CellIntegrityBack;
            [NoAlias] public NativeArray<byte> CellFatigue;
            [NoAlias] public NativeArray<byte> FatigueCompartmentFlags;
            [NoAlias] public NativeArray<float> FatigueIntegrityLossPerCycle;
            [NoAlias] public NativeArray<float> PeakNormalized;

            public int CellCount;

            public void Execute()
            {
                float peak = PeakNormalized[0];
                int lastFlagIndex = math.max(0, FatigueCompartmentFlags.Length - 1);
                for (int cellIndex = 0; cellIndex < CellCount; cellIndex++)
                {
                    byte compartmentIndex = CellCompartmentIndices[cellIndex];
                    int safeCompartmentIndex = math.clamp((int)compartmentIndex, 0, lastFlagIndex);
                    int active = math.select(0, 1, compartmentIndex < FatigueCompartmentFlags.Length) &
                                 math.select(0, 1, FatigueCompartmentFlags[safeCompartmentIndex] != 0);

                    byte fatigue = CellFatigue[cellIndex];
                    int fatigueValue = math.min(byte.MaxValue, fatigue + active);

                    CellFatigue[cellIndex] = (byte)fatigueValue;
                    peak = math.max(peak, fatigueValue / (float)byte.MaxValue);
                    float scaledIntegrityLossPerCycle = math.max(0f, FatigueIntegrityLossPerCycle[safeCompartmentIndex]) * active;
                    int integrityCap = math.max(0, (int)math.floor(FullIntegrity - (fatigueValue * scaledIntegrityLossPerCycle)));
                    byte cappedIntegrity = (byte)integrityCap;
                    byte currentFront = CellIntegrityFront[cellIndex];
                    byte currentBack = CellIntegrityBack[cellIndex];
                    CellIntegrityFront[cellIndex] = (byte)math.select((int)currentFront, math.min((int)currentFront, integrityCap), active != 0);
                    CellIntegrityBack[cellIndex] = (byte)math.select((int)currentBack, math.min((int)currentBack, integrityCap), active != 0);
                }

                for (int i = 0; i < FatigueCompartmentFlags.Length; i++)
                    FatigueCompartmentFlags[i] = 0;

                PeakNormalized[0] = peak;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ImpactCommand
        {
            [FieldOffset(0)]
            public float3 LocalPoint;
            [FieldOffset(12)]
            public float RadiusMeters;
            [FieldOffset(16)]
            public float SigmaMeters;
            [FieldOffset(20)]
            public int DamageBytes;
            [FieldOffset(24)]
            private byte _pad0;
            [FieldOffset(25)]
            private byte _pad1;
            [FieldOffset(26)]
            private byte _pad2;
            [FieldOffset(27)]
            private byte _pad3;
            [FieldOffset(28)]
            private byte _pad4;
            [FieldOffset(29)]
            private byte _pad5;
            [FieldOffset(30)]
            private byte _pad6;
            [FieldOffset(31)]
            private byte _pad7;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BreachRepairJob : IJob
        {
            [NoAlias] public NativeArray<float4> Breaches;
            [NoAlias] public NativeArray<float> SeveritySum;
            public int ActiveCount;
            public float3 LocalHitPoint;
            public float RepairDelta;
            public float RepairRadiusSq;

            public void Execute()
            {
                float sum = 0f;
                int count = math.clamp(ActiveCount, 0, Breaches.Length);
                for (int i = 0; i < count; i++)
                {
                    float4 breach = Breaches[i];
                    float severity = math.max(0f, breach.w);
                    float3 breachDelta = new float3(breach.x, breach.y, breach.z) - LocalHitPoint;
                    float repairActive = math.select(0f, 1f, severity > 0f && math.lengthsq(breachDelta) <= RepairRadiusSq);
                    severity = math.max(0f, severity - (RepairDelta * repairActive));
                    breach.w = severity;
                    Breaches[i] = breach;

                    sum += severity;
                }

                SeveritySum[0] = math.select(0f, sum, math.isfinite(sum));
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct SubmarineStructuralTelemetryEntry
        {
            [FieldOffset(0)]
            public float4 FirstBreachLocalSeverity;
            [FieldOffset(16)]
            public float SeveritySum;
            [FieldOffset(20)]
            public float CpuMicroseconds;
            [FieldOffset(24)]
            public float GpuMicroseconds;
            [FieldOffset(28)]
            public uint Frame;
            [FieldOffset(32)]
            public uint Flags;
            [FieldOffset(36)]
            public uint StateHash;
            [FieldOffset(40)]
            public uint BufferId;
            [FieldOffset(44)]
            public uint Generation;
            [FieldOffset(48)]
            public uint VaultGeneration;
            [FieldOffset(52)]
            public uint Sequence;
            [FieldOffset(56)]
            public ushort ActiveBreachCount;
            [FieldOffset(58)]
            public ushort VisibleBreachCount;
            [FieldOffset(60)]
            public ushort FailureCode;
            [FieldOffset(62)]
            public ushort ConsecutiveFailureCount;
        }

        [Header("Grid Authoring")]
        [Tooltip("Voxel columns along the submarine local X axis.")]
        [SerializeField, Min(1)] private int gridWidth = 16;
        [Tooltip("Voxel rows along the submarine local Y axis.")]
        [SerializeField, Min(1)] private int gridHeight = 6;
        [Tooltip("Voxel slices along the submarine local Z axis.")]
        [SerializeField, Min(1)] private int gridDepth = 8;
        [Tooltip("Center of the structural grid in submarine-local space.")]
        [SerializeField] private Vector3 localGridCenter = Vector3.zero;
        [Tooltip("Size of the structural grid in submarine-local space.")]
        [SerializeField] private Vector3 localGridSize = new Vector3(6f, 2.5f, 14f);
        [Tooltip("When enabled, the grid bounds derive from the cached hull collider at startup.")]
        [SerializeField] private bool deriveGridBoundsFromHullCollider = true;

        [Header("Damage Diffusion")]
        [Tooltip("Minimum spherical damage radius in meters for any structural impact.")]
        [SerializeField, Min(0.05f)] private float minimumImpactRadiusMeters = DefaultMinimumImpactRadiusMeters;
        [Tooltip("Additional damage radius in meters added per impact speed unit.")]
        [SerializeField, Min(0f)] private float impactRadiusPerMeterPerSecond = DefaultImpactRadiusPerMeterPerSecond;
        [Tooltip("Minimum Gaussian sigma in meters.")]
        [SerializeField, Min(0.01f)] private float minimumSigmaMeters = DefaultMinimumSigmaMeters;
        [Tooltip("Sigma scale applied to the resolved impact radius.")]
        [SerializeField, Range(0.1f, 1f)] private float sigmaScale = DefaultSigmaScale;
        [Tooltip("Cell-integrity damage contributed by one meter per second of collision speed.")]
        [SerializeField, Min(0f)] private float impactSpeedToCellDamageScale = DefaultImpactSpeedToCellDamageScale;
        [Tooltip("Cell-integrity damage contributed by one integrity byte from the incoming damage signal.")]
        [SerializeField, Min(0f)] private float integrityByteToCellDamageScale = DefaultIntegrityByteToCellDamageScale;

        [Header("Hull Impact Decals")]
        [Tooltip("Minimum projected dent decal size in meters for heavy impacts.")]
        [SerializeField, Min(0.05f)] private float minimumDentDecalSizeMeters = 0.35f;
        [Tooltip("Additional projected decal size added at full heavy-impact severity.")]
        [SerializeField, Min(0f)] private float dentDecalSizeFromSeverityMeters = 0.95f;
        [Tooltip("Projection depth of the dent decal volume in meters.")]
#pragma warning disable CS0414
        [SerializeField, Min(0.01f)] private float dentDecalProjectionDepthMeters = 0.18f;
#pragma warning restore CS0414
        [Tooltip("Surface-normal offset used to avoid decal z-fighting.")]
        [SerializeField, Min(0f)] private float dentDecalSurfaceOffsetMeters = 0.015f;
        [Tooltip("Lifetime before the pooled decal is returned.")]
#pragma warning disable CS0414
        [SerializeField, Min(0.1f)] private float dentDecalLifetimeSeconds = 4f;
#pragma warning restore CS0414
        [Tooltip("Optional shared material for GPU-instanced visual-only hull impact sparks.")]
        [SerializeField] private Material hullImpactSparkMaterial;
        [Tooltip("Maximum pooled spark particles reserved for submarine hull impacts.")]
        [SerializeField, Min(8)] private int hullImpactSparkMaxParticles = 192;
        [Tooltip("Maximum visual spark burst emitted at full impact severity.")]
        [SerializeField, Range(1, 64)] private int hullImpactSparkMaxBurstCount = 50;

        [Header("References")]
        [Tooltip("Optional authored hull collider used for automatic local bounds fitting.")]
        [SerializeField] private Collider hullCollider;
        [Tooltip("Optional authored submarine fluid owner consuming published breach areas.")]
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;
        [Tooltip("Optional authored atmosphere owner used for pressure-cycle fatigue.")]
        [SerializeField, FormerlySerializedAs("atmosphereSystem")] private MonoBehaviour atmosphereSystemSource;
        [Tooltip("Compute kernel that expands the packed hull breach buffer into GPU leak plume particles.")]
        [SerializeField] private ComputeShader leakPlumeCompute;
        [Tooltip("Material using HECTON/VFX/LeakPlume. Required to draw GPU leak plume points emitted by the compute kernel.")]
        [SerializeField] private Material leakPlumeRenderMaterial;

        [Header("Damage Control")]
        [Tooltip("Pressure-to-severity scalar for packed hull breaches. Local breach logic remains capped at 64 entries.")]
        [SerializeField, Min(0f)] private float pressureToBreachSeverityScale = DefaultPressureToBreachSeverityScale;
        [Tooltip("Repair-tool units converted to breach severity reduction per second.")]
        [SerializeField, Min(0f)] private float repairUnitsToBreachSeverityScale = DefaultRepairUnitsToBreachSeverityScale;
        [Tooltip("Seconds between water-leak impact/audio signals while any breach remains active.")]
        [SerializeField, Min(0.05f)] private float leakAudioCadenceSeconds = DefaultLeakAudioCadenceSeconds;
        [Tooltip("World-space billboard size for each GPU leak plume point.")]
        [SerializeField, Min(0.01f)] private float leakPlumeParticleSizeMeters = DefaultLeakPlumeParticleSizeMeters;
        [Tooltip("Extra world-space render-bound padding around the submarine while leak plumes are active.")]
        [SerializeField, Min(0f)] private float leakPlumeRenderBoundsPaddingMeters = DefaultLeakPlumeRenderBoundsPaddingMeters;

        [Header("Fatigue")]
        [Tooltip("Pressure threshold in kPa that counts as one full pressurization cycle.")]
        [SerializeField, Min(0f)] private float fatiguePressureThresholdKPa = DefaultFatiguePressureThresholdKPa;
        [Tooltip("Permanent integrity bytes lost each time a compartment crosses into the high-pressure band.")]
        [SerializeField, Range(1, 32)] private byte fatigueIntegrityLossPerCycle = DefaultFatigueIntegrityLossPerCycle;

        [Header("Abyssal Compression")]
        [Tooltip("Depth threshold in meters where ambient pressure starts compressing compartment volume.")]
        [SerializeField, Min(0f)] private float compressionDepthThresholdMeters = DefaultCompressionDepthThresholdMeters;
        [Tooltip("Hydrostatic pressure in kPa where maximum hull-volume compression is reached.")]
        [SerializeField, Min(1f)] private float compressionFullPressureKPa = DefaultCompressionFullPressureKPa;
        [Tooltip("Maximum normalized compartment-volume loss applied at full crush pressure.")]
        [SerializeField, Range(0f, 0.5f)] private float maximumVolumeCompressionNormalized = DefaultMaximumVolumeCompressionNormalized;

        [Header("Fake Crush Depth")]
        [Tooltip("Depth where visual-only hull buckling reaches full strength.")]
        [SerializeField, Min(1f)] private float fakeCrushDepthMeters = 4000f;
        [Tooltip("Maximum GPU vertex displacement in meters at full fake crush depth.")]
        [SerializeField, Range(0f, 1f)] private float fakeCrushMaxVertexDisplacementMeters = 0.22f;
        [Tooltip("World-space radius around the submarine center affected by fake crush shader globals.")]
        [SerializeField, Min(0f)] private float fakeCrushEffectRadiusMeters = 18f;
        [Tooltip("Voronoi noise scale used by the hull shader fake crush displacement.")]
        [SerializeField, Min(0.001f)] private float fakeCrushVoronoiScale = 0.18f;

        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _damageReceiverRegistered;
        private bool _damageJobRunning;
        private bool _nativeStateReady;
        private bool _coldSupportsComputeShaders;
        private ISubmarineAtmosphereRoomReadModel _atmosphereSystem;
        private int _queuedImpactCount;
        private int _scheduledImpactCount;
        private int _mappedCompartmentCount;
        private int _activeBreachCount;
        private int _visibleBreachCount;
        private int _pendingMappedCompartmentCount;
        private int _leakPlumeKernelIndex = -1;
        private int _leakPlumeThreadGroupSizeX;
        private int _activeBreachGpuBufferIndex;
        private int _damageControlTelemetryHead;
        private int _deferredBreachAddCount;
        private int _droppedSignalCount;

        private float _cellBreachAreaSquareMeters;
        private float _fatiguePeakNormalized;
        private float _recentImpactSeverityNormalized;
        private float _activeBreachSeveritySum;
        private float _pendingRepairSeverityDelta;
        private float3 _pendingRepairLocalPoint;
        private float _leakAudioTimer;
        private float _leakPlumeClockSeconds;
        private float _pendingLeakPlumeDeltaSeconds;
        private float _pendingFakeCrushDepthMeters;
        private float _criticalBreachWarningTimer;
        private float _debugCompressionScale = 1f;
        private Vector4 _publishedCrushCenterRadius = new Vector4(float.NaN, 0f, 0f, 0f);
        private Vector4 _publishedCrushDepthParams = new Vector4(float.NaN, 0f, 0f, 0f);
        private JobHandle _damageJobHandle;
        private JobHandle _mappingJobHandle;
        private JobHandle _fatigueJobHandle;
        private JobHandle _breachRepairJobHandle;
        private IDamageSignalEmitter _damageEmitter;
        private Transform _cachedTransform;
        private Rigidbody _cachedHullRigidbody;
        private IPlayerRuntimeContext _cachedPlayerRuntime;
        private IFluidDecalPresentationSink _cachedFluidDecals;
        private ParticleSystem _hullImpactSparkParticles;
        private ParticleSystemRenderer _hullImpactSparkRenderer;
        private ParticleSystem.EmitParams _hullImpactSparkEmitParams;
        private readonly HullImpactDecalRequest[] _pendingHullImpactDecals = new HullImpactDecalRequest[MaxQueuedImpacts]; // COLD ALLOC: HullImpactDecalRequest[16] - visual-sync hull impact decal queue - owner: SubmarineStructuralGrid
        private readonly HullImpactSparkRequest[] _pendingHullImpactSparks = new HullImpactSparkRequest[MaxQueuedImpacts]; // COLD ALLOC: HullImpactSparkRequest[16] - visual-sync spark burst queue - owner: SubmarineStructuralGrid
        private int _pendingHullImpactDecalCount;
        private int _pendingHullImpactSparkCount;
        private readonly BreachPressureSprayRequest[] _pendingBreachPressureSprays = new BreachPressureSprayRequest[MaxQueuedImpacts]; // COLD ALLOC: BreachPressureSprayRequest[16] - visual-sync breach spray queue - owner: SubmarineStructuralGrid
        private int _pendingBreachPressureSprayCount;
        private MaterialPropertyBlock _leakPlumeDrawProperties;
        private IDataVault _dataVault;
        private IDataVault _structuralMutationGuardVault;
        private IDataVault _damageJobMutationGuardVault;
        private IDataVault _mappingJobMutationGuardVault;
        private IDataVault _fatigueJobMutationGuardVault;
        private IDataVault _breachRepairJobMutationGuardVault;
        private VaultGenerationHandle<float4> _breachesHandle;
        private VaultGenerationHandle<SubmarineStructuralTelemetryEntry> _damageControlTelemetryHandle;
        private bool _breachRepairJobRunning;
        private bool _pendingRepairQueued;
        private bool _breachGpuDirty;
        private readonly List<MonoBehaviour> _componentSearchBuffer = new List<MonoBehaviour>(4); // COLD ALLOC: List<MonoBehaviour>(4) - local component search scratch for interface-only wiring - owner: SubmarineStructuralGrid

        private VaultGenerationHandle<byte> _cellIntegrityFrontHandle;
        private VaultGenerationHandle<byte> _cellIntegrityBackHandle;
        private VaultGenerationHandle<byte> _cellFatigueHandle;
        private VaultGenerationHandle<byte> _cellCompartmentIndicesHandle;
        private VaultGenerationHandle<ulong> _hullBreachMaskFrontHandle;
        private VaultGenerationHandle<ulong> _hullBreachMaskBackHandle;
        private VaultGenerationHandle<float> _compartmentBreachAreasFrontHandle;
        private VaultGenerationHandle<float> _compartmentBreachAreasBackHandle;
        private VaultGenerationHandle<ImpactCommand> _queuedImpactsHandle;
        private VaultGenerationHandle<ImpactCommand> _scheduledImpactsHandle;
        private VaultGenerationHandle<float3> _compartmentCentroidsHandle;
        private VaultGenerationHandle<byte> _fatigueCompartmentFlagsHandle;
        private VaultGenerationHandle<float> _fatigueIntegrityLossPerCycleHandle;
        private VaultGenerationHandle<float> _fatiguePeakResultHandle;
        private VaultGenerationHandle<float> _breachSeveritySumResultHandle;
        private int _damageJobLockMask;
        private int _mappingJobLockMask;
        private int _fatigueJobLockMask;
        private int _breachRepairJobLockMask;
        private uint _structuralTelemetrySequence;
        private uint _structuralTelemetryFailureCount;
        private readonly SubmarineStructuralTelemetryEntry[] _damageControlTelemetryDumpSnapshot = new SubmarineStructuralTelemetryEntry[DamageControlTelemetryCapacity]; // COLD ALLOC: fixed black-box snapshot retained in memory.
        private int _damageControlTelemetryDumpHead;
        private int _damageControlTelemetryDumpRequested;

        /// <summary>Signals refused by bounded downstream lanes since this runtime was enabled.</summary>
        public int DroppedSignalCount => _droppedSignalCount;
        private readonly float4[] _deferredBreachAdds = new float4[DeferredBreachAddCapacity]; // COLD ALLOC: float4[16] - breach adds deferred while Burst repair owns the NativeArray - owner: SubmarineStructuralGrid
        private GraphicsBuffer _breachGpuBufferA;
        private GraphicsBuffer _breachGpuBufferB;
        private GraphicsBuffer _leakPlumeParticleBuffer;
        private bool _mappingJobRunning;
        private bool _fatigueJobRunning;
        private bool _leakPlumeVisualDirty;
        private bool _leakPlumeGpuResourceRepairRequested = true;
        private bool _fakeCrushDepthVisualDirty;
        // COLD ALLOC: float[8] - previous compartment pressures used to detect fatigue cycles - owner: SubmarineStructuralGrid
        private readonly float[] _previousCompartmentPressuresKPa = new float[CompartmentCapacity];

        private struct HullImpactSparkRequest
        {
            public Vector3 WorldPoint;
            public Vector3 OutwardNormal;
            public float Severity01;
        }

        private struct HullImpactDecalRequest
        {
            public float3 LocalPoint;
            public float3 LocalNormal;
            public Vector3 WorldPoint;
            public Vector3 OutwardNormal;
            public float ImpactSpeed;
            public float Severity01;
            public byte UseLocalSpace;
        }

        private struct BreachPressureSprayRequest
        {
            public float3 LocalPoint;
            public float Severity01;
        }

        /// <inheritdoc />
        public bool IsReady => _nativeStateReady && IsGenerationHandleCreated(in _cellIntegrityFrontHandle);

        /// <inheritdoc />
        public int BreachMaskWordCount
        {
            get
            {
                int breachWordCount = (ResolveCellCount() + 63) >> 6;
                return TryReadVaultBuffer(_dataVault, in _hullBreachMaskFrontHandle, breachWordCount, out NativeArray<ulong>.ReadOnly words)
                    ? words.Length
                    : 0;
            }
        }

        /// <inheritdoc />
        public int ActiveBreachCount => _nativeStateReady && TryReadBreachBuffer(out var breaches)
            ? math.min(_activeBreachCount, breaches.Length)
            : 0;

        public float FatiguePeakNormalized => _fatiguePeakNormalized;
        public float RecentImpactSeverityNormalized => _recentImpactSeverityNormalized;

#if UNITY_EDITOR
        private void OnValidate()
        {
            pressureToBreachSeverityScale = math.max(0f, pressureToBreachSeverityScale);
            repairUnitsToBreachSeverityScale = math.max(0f, repairUnitsToBreachSeverityScale);
            leakAudioCadenceSeconds = math.max(0.05f, leakAudioCadenceSeconds);
            leakPlumeParticleSizeMeters = math.max(0.01f, leakPlumeParticleSizeMeters);
            leakPlumeRenderBoundsPaddingMeters = math.max(0f, leakPlumeRenderBoundsPaddingMeters);
            if (leakPlumeCompute == null)
                leakPlumeCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(LeakPlumeComputeAssetPath);
            if (leakPlumeRenderMaterial == null)
                leakPlumeRenderMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(LeakPlumeMaterialAssetPath);
        }
#endif

        private void Awake()
        {
            CacheGraphicsCapabilitiesCold();
            CacheReferences();
            // COLD ALLOC: MaterialPropertyBlock[1] - procedural leak plume draw properties for RenderPrimitives - owner: SubmarineStructuralGrid
            _leakPlumeDrawProperties = new MaterialPropertyBlock();
            ResolveGridBounds();
            EnsureNativeState();
            SeedStructuralState();
            EnsureDamageControlTelemetryDumpWorker();
            EnsureHullImpactSparkParticles();
        }

        private void OnEnable()
        {
            _droppedSignalCount = 0;
            CacheReferences();
            CacheGlobalRegistryServices();
            ResolveGridBounds();
            EnsureNativeState();
            SeedStructuralState();
            GlobalRegistry.RegisterSubmarineHullBreach(this);
            InteractableRegistry.RegisterTree(this);
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            EnsureDamageControlTelemetryDumpWorker();
            TryRegister();
            TryRegisterDamageReceiver();
            EnsureHullImpactSparkParticles();
        }

        private void OnDisable()
        {
            StopHullImpactSparkParticles();
            TryUnregisterDamageReceiver();
            TryUnregister();
            InteractableRegistry.InvalidateTree(this);
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            ClearGlobalRegistryServiceCache();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
            ResetFakeCrushDepthGlobals();
            ReleaseLeakPlumeGpuResources();
            StopDamageControlTelemetryDumpWorker();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            StopHullImpactSparkParticles();
            TryUnregisterDamageReceiver();
            TryUnregister();
            InteractableRegistry.InvalidateTree(this);
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            ClearGlobalRegistryServiceCache();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
            ResetFakeCrushDepthGlobals();
            ReleaseLeakPlumeGpuResources();
            StopDamageControlTelemetryDumpWorker();
            DisposeNativeStateDeferred();
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            using (_fixedTickProfilerMarker.Auto())
            {
                if (!IsReady)
                    return;

                _recentImpactSeverityNormalized = math.max(
                    0f,
                    _recentImpactSeverityNormalized - math.max(0f, fixedDeltaTime) * RecentImpactSeverityDecayPerSecond);
                if (!EnsureCompartmentMappingReady())
                    return;

                ApplyAbyssalCompression();
                if (!_damageJobRunning)
                    ApplyPressureCycleFatigue();

                if (!_breachRepairJobRunning && _pendingRepairQueued)
                    ScheduleBreachRepairJob();

                if (!_damageJobRunning && !_fatigueJobRunning && _queuedImpactCount > 0)
                    ScheduleDamageJob();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ConsumeCompletedMappingJob();
            ConsumeCompletedFatigueJob();
            ConsumeCompletedDamageJob();
            ConsumeCompletedBreachRepairJob();
            if (_breachRepairJobRunning)
            {
                WriteDamageControlTelemetry(DamageControlTelemetryRepairJobInFlightFlag, false);
                return;
            }

            FlushDeferredBreachAdds();
            CompactInactiveBreaches();
            PushDamageControlCoupling(fixedDeltaTime);
            QueueLeakPlumeVisualSync(fixedDeltaTime);
            WriteDamageControlTelemetry(0u);
        }

        public void LateFrameTick()
        {
            FlushFakeCrushDepthGlobals();
            FlushQueuedHullImpactDecals();
            FlushQueuedHullImpactSparks();
            FlushQueuedBreachScreenSpaceFeedback();
            FlushLeakPlumeVisualSync();
            RenderLeakPlumeParticles();
        }

        public void SlowTick()
        {
            FlushLeakPlumeGpuResourceRepairSlow();
        }

        /// <summary>
        /// Queues a hull-local impact for the next fixed-step diffusion pass.
        /// </summary>
        public void QueueImpactLocal(float3 localPoint, float impactSpeed, byte integrityDelta)
        {
            if (!_nativeStateReady ||
                _queuedImpactCount >= MaxQueuedImpacts ||
                !TryAcquireStructuralMutationGuard())
                return;

            try
            {
                if (!TryResolveStructuralWriteBuffer(in _queuedImpactsHandle, MaxQueuedImpacts, out NativeArray<ImpactCommand> queuedImpacts))
                    return;

                if (_queuedImpactCount >= queuedImpacts.Length)
                    return;

                float radius = math.max(minimumImpactRadiusMeters, impactSpeed * impactRadiusPerMeterPerSecond);
                float sigma = math.max(minimumSigmaMeters, radius * sigmaScale);
                float damageFromImpact = math.max(0f, impactSpeed) * impactSpeedToCellDamageScale;
                float damageFromSignal = integrityDelta * integrityByteToCellDamageScale;
                int damageBytes = (int)math.round(math.clamp(damageFromImpact + damageFromSignal, 1f, FullIntegrity));
                _recentImpactSeverityNormalized = math.max(
                    _recentImpactSeverityNormalized,
                    math.saturate(damageBytes / (float)FullIntegrity));

                ImpactCommand impactCommand = default;
                impactCommand.LocalPoint = localPoint;
                impactCommand.RadiusMeters = radius;
                impactCommand.SigmaMeters = sigma;
                impactCommand.DamageBytes = damageBytes;
                queuedImpacts[_queuedImpactCount++] = impactCommand;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }

        /// <summary>
        /// Queues a hull-impact feedback DTO for the visual-sync lane without touching scene presentation.
        /// </summary>
        public void QueueHullImpactFeedbackLocal(float3 localPoint, float3 localNormal, float impactSpeed, float severity01)
        {
            QueueHullImpactDecalLocal(localPoint, localNormal, impactSpeed, severity01);
        }

        /// <summary>
        /// Queues one visual impact decal in hull-local space without spawning projector objects or mutating hull mesh data.
        /// </summary>
        public void QueueHullImpactDecalLocal(float3 localPoint, float3 localNormal, float impactSpeed, float severity01)
        {
            QueueHullImpactDecalRequest(
                new HullImpactDecalRequest
                {
                    LocalPoint = localPoint,
                    LocalNormal = localNormal,
                    ImpactSpeed = math.max(0f, impactSpeed),
                    Severity01 = math.saturate(severity01),
                    UseLocalSpace = 1
                });
        }

        /// <summary>
        /// Queues a visual-only hull impact decal from an external kinematic sweep without mutating mesh data.
        /// </summary>
        public void QueueHullImpactDecalWorld(Vector3 worldPoint, Vector3 outwardNormal, float impactSpeed, float severity01)
        {
            QueueHullImpactDecalRequest(
                new HullImpactDecalRequest
                {
                    WorldPoint = worldPoint,
                    OutwardNormal = outwardNormal,
                    ImpactSpeed = math.max(0f, impactSpeed),
                    Severity01 = math.saturate(severity01),
                    UseLocalSpace = 0
                });
        }

        internal static float DebugResolveHullImpactDentDecalSize(
            float minimumSizeMeters,
            float sizeFromSeverityMeters,
            float impactSpeed,
            float severity01)
        {
            float safeSeverity = math.saturate(severity01);
            float size = math.max(minimumSizeMeters, minimumSizeMeters + sizeFromSeverityMeters * safeSeverity);
            return size + math.saturate(math.max(0f, impactSpeed) / 30f) * 0.2f;
        }

        private void SpawnHullImpactSparks(Vector3 worldPoint, Vector3 outwardNormal, float severity01)
        {
            if (_hullImpactSparkParticles == null)
                return;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 normal = ResolveSafeDirection(outwardNormal, cachedTransform.up);
            Transform sparkTransform = _hullImpactSparkParticles.transform;
            sparkTransform.SetPositionAndRotation(
                worldPoint + normal * math.max(0f, dentDecalSurfaceOffsetMeters),
                Quaternion.LookRotation(normal, ResolveStableDecalUp(normal)));

            float safeSeverity = math.saturate(severity01);
            int maxBurstCount = math.max(1, hullImpactSparkMaxBurstCount);
            int burstCount = math.clamp((int)math.ceil(math.lerp(6f, maxBurstCount, safeSeverity)), 1, maxBurstCount);

            _hullImpactSparkEmitParams.position = sparkTransform.position;
            _hullImpactSparkEmitParams.velocity = normal * math.lerp(1.5f, 4.5f, safeSeverity);
            _hullImpactSparkEmitParams.startLifetime = math.lerp(0.16f, 0.42f, safeSeverity);
            _hullImpactSparkEmitParams.startSize = math.lerp(0.025f, 0.08f, safeSeverity);
            _hullImpactSparkEmitParams.startColor = LerpColorClamped(new Color(1f, 0.45f, 0.08f, 0.85f), Color.white, safeSeverity);
            _hullImpactSparkParticles.Emit(_hullImpactSparkEmitParams, burstCount);
        }

        private void QueueHullImpactSparks(Vector3 worldPoint, Vector3 outwardNormal, float severity01)
        {
            if (_pendingHullImpactSparkCount >= _pendingHullImpactSparks.Length)
                return;

            _pendingHullImpactSparks[_pendingHullImpactSparkCount++] = new HullImpactSparkRequest
            {
                WorldPoint = worldPoint,
                OutwardNormal = outwardNormal,
                Severity01 = math.saturate(severity01)
            };
        }

        private void QueueHullImpactDecalRequest(in HullImpactDecalRequest request)
        {
            if (_pendingHullImpactDecalCount >= _pendingHullImpactDecals.Length)
                return;

            _pendingHullImpactDecals[_pendingHullImpactDecalCount++] = request;
        }

        private void FlushQueuedHullImpactDecals()
        {
            int count = _pendingHullImpactDecalCount;
            if (count <= 0)
                return;

            _pendingHullImpactDecalCount = 0;
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 fallbackNormal = cachedTransform.up;
            for (int i = 0; i < count; i++)
            {
                HullImpactDecalRequest request = _pendingHullImpactDecals[i];
                _pendingHullImpactDecals[i] = default;

                Vector3 worldPoint;
                Vector3 worldNormal;
                if (request.UseLocalSpace != 0)
                {
                    Vector3 localPoint = new Vector3(request.LocalPoint.x, request.LocalPoint.y, request.LocalPoint.z);
                    Vector3 localNormal = new Vector3(request.LocalNormal.x, request.LocalNormal.y, request.LocalNormal.z);
                    worldPoint = cachedTransform.TransformPoint(localPoint);
                    worldNormal = cachedTransform.TransformDirection(localNormal);
                }
                else
                {
                    worldPoint = request.WorldPoint;
                    worldNormal = request.OutwardNormal;
                }

                Vector3 normal = ResolveSafeDirection(worldNormal, fallbackNormal);
                QueueHullImpactSparks(worldPoint, normal, request.Severity01);
                EnqueueHullImpactDecal(worldPoint, normal, request.ImpactSpeed, request.Severity01);
                TriggerHullImpactCameraShake(request.Severity01, worldPoint, normal);
            }
        }

        private void FlushQueuedHullImpactSparks()
        {
            int count = _pendingHullImpactSparkCount;
            if (count <= 0)
                return;

            _pendingHullImpactSparkCount = 0;
            for (int i = 0; i < count; i++)
            {
                HullImpactSparkRequest request = _pendingHullImpactSparks[i];
                _pendingHullImpactSparks[i] = default;
                SpawnHullImpactSparks(request.WorldPoint, request.OutwardNormal, request.Severity01);
            }
        }

        private bool EnqueueHullImpactDecal(Vector3 worldPoint, Vector3 outwardNormal, float impactSpeed, float severity01)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 normal = ResolveSafeDirection(outwardNormal, cachedTransform.up);
            Vector3 position = worldPoint + normal * math.max(0f, dentDecalSurfaceOffsetMeters);
            float size = DebugResolveHullImpactDentDecalSize(
                math.max(0.05f, minimumDentDecalSizeMeters),
                math.max(0f, dentDecalSizeFromSeverityMeters),
                impactSpeed,
                severity01);
            CombatDamageSignal signal = default;
            float3 direction = default;
            direction.x = -normal.x;
            direction.y = -normal.y;
            direction.z = -normal.z;
            signal.ImpactAup = CombatDamageSignalCodec.FromRuntimePoint(position);
            signal.Direction = direction;
            signal.Magnitude = math.max(size * 18f, impactSpeed * 0.2f);
            signal.DamageType = HullDentVisualDamageType;
            uint targetHash = Hecton8.Core.RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
            signal.TargetHash = targetHash != 0u ? targetHash : 1u;
            signal.SourceHash = HullDentVisualSourceHash;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.SourceId = 0;
            signal.TargetId = 0;
            signal.Channel = 0;
            signal.Flags = CombatDamageSignal.DirectRuntimeFlag | CombatDamageSignal.VisualOnlyFlag;
            signal.IntegrityDelta = (byte)math.clamp(math.round(math.saturate(severity01) * 255f), 0f, 255f);
            bool accepted = SignalBus<CombatDamageSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_SubmarineStructuralGrid);
            if (!accepted)
                IncrementDroppedSignalCount();
            return accepted;
        }

        private void TriggerHullImpactCameraShake(float severity01, Vector3 worldPoint, Vector3 worldNormal)
        {
            if (!CameraJuiceSignals.TryPublishImpact(
                    severity01,
                    worldPoint,
                    -worldNormal,
                    CameraJuiceSignals.SharpKineticImpactProfileHash,
                    1.15f,
                    severity01 >= 0.72f ? CameraJuiceSignals.CriticalPriority : CameraJuiceSignals.HighPriority,
                    0f,
                    1.1f,
                    1.2f,
                    HullDentVisualSourceHash))
            {
                IncrementDroppedSignalCount();
            }
        }

        private void EnsureHullImpactSparkParticles()
        {
            if (_hullImpactSparkParticles != null)
                return;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            GameObject sparkObject = new GameObject("PFX_SubmarineHull_ImpactSparks"); // COLD ALLOC: GameObject[1] - visual-only hull impact particle owner - owner: SubmarineStructuralGrid
            sparkObject.transform.SetParent(cachedTransform, false);
            _hullImpactSparkParticles = sparkObject.AddComponent<ParticleSystem>(); // COLD ALLOC: ParticleSystem[1] - pooled hull impact sparks - owner: SubmarineStructuralGrid
            sparkObject.TryGetComponent(out _hullImpactSparkRenderer);

            ParticleSystem.MainModule main = _hullImpactSparkParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = math.max(8, hullImpactSparkMaxParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.38f, 0.06f, 0.9f),
                new Color(1f, 0.92f, 0.66f, 1f));

            ParticleSystem.EmissionModule emission = _hullImpactSparkParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = _hullImpactSparkParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.035f;
            shape.length = 0.08f;

            ParticleSystem.LightsModule lights = _hullImpactSparkParticles.lights;
            lights.enabled = false;
            ParticleSystem.CollisionModule collision = _hullImpactSparkParticles.collision;
            collision.enabled = false;
            ParticleSystem.TrailModule trails = _hullImpactSparkParticles.trails;
            trails.enabled = false;

            if (_hullImpactSparkRenderer != null)
            {
                _hullImpactSparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                _hullImpactSparkRenderer.lengthScale = 2.4f;
                _hullImpactSparkRenderer.velocityScale = 0.08f;
                _hullImpactSparkRenderer.cameraVelocityScale = 0f;
                _hullImpactSparkRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _hullImpactSparkRenderer.receiveShadows = false;
                _hullImpactSparkRenderer.enableGPUInstancing = true;
                if (hullImpactSparkMaterial != null)
                    _hullImpactSparkRenderer.sharedMaterial = hullImpactSparkMaterial;
            }
        }

        private void StopHullImpactSparkParticles()
        {
            if (_hullImpactSparkParticles == null)
                return;

            _hullImpactSparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private Vector3 ResolveStableDecalUp(Vector3 normal)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 up = Vector3.Cross(normal, cachedTransform.right);
            if (up.sqrMagnitude <= Epsilon)
                up = Vector3.Cross(normal, cachedTransform.forward);

            return ResolveSafeDirection(up, Vector3.up);
        }

        /// <inheritdoc />
        public ulong GetHullBreachMaskWord(int wordIndex)
        {
            int breachWordCount = (ResolveCellCount() + 63) >> 6;
            return TryReadVaultBuffer(_dataVault, in _hullBreachMaskFrontHandle, breachWordCount, out NativeArray<ulong>.ReadOnly words) &&
                   (uint)wordIndex < (uint)words.Length
                ? words[wordIndex]
                : 0UL;
        }

        /// <inheritdoc />
        public float GetCompartmentBreachAreaSquareMeters(int compartmentIndex)
        {
            return TryReadVaultBuffer(_dataVault, in _compartmentBreachAreasFrontHandle, CompartmentCapacity, out NativeArray<float>.ReadOnly areas) &&
                   (uint)compartmentIndex < (uint)areas.Length
                ? areas[compartmentIndex]
                : 0f;
        }

        /// <inheritdoc />
        public bool TryGetActiveBreach(int index, out Vector4 localPointSeverity)
        {
            localPointSeverity = default;
            if (!_nativeStateReady ||
                !TryReadBreachBuffer(out var breaches) ||
                (uint)index >= (uint)math.min(_activeBreachCount, breaches.Length))
            {
                return false;
            }

            float4 breach = breaches[index];
            if (breach.w <= 0f || !math.all(math.isfinite(breach)))
                return false;

            localPointSeverity = new Vector4(breach.x, breach.y, breach.z, breach.w);
            return true;
        }

        /// <inheritdoc />
        public bool TryResolveRepairRoom(Vector3 worldHitPoint, out int roomId)
        {
            roomId = -1;
            if (!_nativeStateReady ||
                _mappingJobRunning ||
                _mappedCompartmentCount <= 0 ||
                !TryReadVaultBuffer(_dataVault, in _compartmentCentroidsHandle, CompartmentCapacity, out NativeArray<float3>.ReadOnly centroids) ||
                !TryResolveLocalPointAup(worldHitPoint, out float3 localPoint))
            {
                return false;
            }

            float bestDistanceSq = float.MaxValue;
            int bestRoomId = -1;
            int count = math.min(_mappedCompartmentCount, centroids.Length);
            for (int compartmentIndex = 0; compartmentIndex < count; compartmentIndex++)
            {
                float distanceSq = math.lengthsq(localPoint - centroids[compartmentIndex]);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestRoomId = compartmentIndex;
            }

            roomId = bestRoomId;
            return bestRoomId >= 0;
        }

        /// <inheritdoc />
        public bool TryQueueRepairHit(Vector3 worldHitPoint, float deltaTime, float repairUnitsPerSecond, float intensity01)
        {
            if (!_nativeStateReady ||
                !TryReadBreachBuffer(out var breaches) ||
                _breachRepairJobRunning ||
                _activeBreachCount <= 0 ||
                !IsFiniteVector(worldHitPoint))
            {
                return false;
            }

            if (!TryResolveLocalPointAup(worldHitPoint, out float3 localPoint))
                return false;

            float repairRadiusSq = BreachRepairRadiusMeters * BreachRepairRadiusMeters;
            int count = math.min(_activeBreachCount, breaches.Length);
            for (int i = 0; i < count; i++)
            {
                float4 breach = breaches[i];
                if (breach.w <= 0f)
                    continue;

                float3 breachDelta = new float3(breach.x, breach.y, breach.z) - localPoint;
                if (math.lengthsq(breachDelta) > repairRadiusSq)
                    continue;

                _pendingRepairLocalPoint = localPoint;
                _pendingRepairSeverityDelta = math.max(0f, deltaTime) *
                                              math.max(0f, repairUnitsPerSecond) *
                                              math.max(0f, repairUnitsToBreachSeverityScale) *
                                              math.max(0.1f, math.saturate(intensity01));
                _pendingRepairQueued = _pendingRepairSeverityDelta > 0f;
                return _pendingRepairQueued;
            }

            return false;
        }

        /// <inheritdoc />
        public void OnIntegrityChanged(float prev, float next, Hecton8.Gameplay.HabitatDamageSignal src)
        {
            float damageDelta = math.max(0f, prev - next);
            if (damageDelta <= 0f)
                return;

            QueueImpactLocal(src.localPoint, math.max(src.magnitude, damageDelta * 10f), src.integrityDelta);
        }

        /// <inheritdoc />
        public void OnPowerChanged(float prev, float next, Hecton8.Gameplay.HabitatDamageSignal src) { }

        /// <inheritdoc />
        public void OnClarityChanged(float prev, float next, Hecton8.Gameplay.HabitatDamageSignal src) { }

        /// <inheritdoc />
        public void OnTraumaThresholdCrossed(TraumaLevel level) { }

        /// <inheritdoc />
        public void OnHullBreach(float3 localPoint, float depth, float pressureDelta)
        {
            QueueImpactLocal(localPoint, math.max(pressureDelta, 1f) * 12f, FullIntegrity);
            AddOrRefreshBreachLocal(localPoint, math.saturate(math.max(pressureDelta, 1f) * pressureToBreachSeverityScale));
        }

        private void AddOrRefreshBreachLocal(float3 localPoint, float severity01)
        {
            if (!_nativeStateReady || !math.all(math.isfinite(localPoint)))
                return;

            float severity = math.saturate(severity01);
            if (severity <= 0f)
                return;

            if (_breachRepairJobRunning)
            {
                QueueDeferredBreachAdd(localPoint, severity);
                return;
            }

            if (!TryAcquireStructuralMutationGuard())
                return;

            try
            {
                if (!TryAcquireBreachWriteBuffer(out NativeArray<float4> breaches))
                    return;

                int count = math.min(_activeBreachCount, breaches.Length);
                float mergeRadiusSq = BreachMergeRadiusMeters * BreachMergeRadiusMeters;
                for (int i = 0; i < count; i++)
                {
                    float4 breach = breaches[i];
                    float3 breachDelta = breach.xyz - localPoint;
                    if (math.lengthsq(breachDelta) > mergeRadiusSq)
                        continue;

                    _activeBreachSeveritySum -= math.max(0f, breach.w);
                    breach.w = math.saturate(math.max(breach.w, severity));
                    _activeBreachSeveritySum += breach.w;
                    breaches[i] = breach;
                    _breachGpuDirty = true;
                    RegisterBreachScreenSpaceFeedback(localPoint, breach.w);
                    return;
                }

                if (count >= breaches.Length)
                    return;

                breaches[count] = math.float4(localPoint, severity);
                _activeBreachCount = count + 1;
                _activeBreachSeveritySum += severity;
                _breachGpuDirty = true;
                RegisterBreachScreenSpaceFeedback(localPoint, severity);
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }

        private void QueueDeferredBreachAdd(float3 localPoint, float severity01)
        {
            if (!math.all(math.isfinite(localPoint)))
                return;

            float severity = math.saturate(severity01);
            if (severity <= 0f)
                return;

            float mergeRadiusSq = BreachMergeRadiusMeters * BreachMergeRadiusMeters;
            for (int i = 0; i < _deferredBreachAddCount; i++)
            {
                float4 deferred = _deferredBreachAdds[i];
                float3 deferredDelta = new float3(deferred.x, deferred.y, deferred.z) - localPoint;
                if (math.lengthsq(deferredDelta) > mergeRadiusSq)
                    continue;

                deferred.w = math.saturate(math.max(deferred.w, severity));
                _deferredBreachAdds[i] = deferred;
                return;
            }

            if (_deferredBreachAddCount < _deferredBreachAdds.Length)
            {
                _deferredBreachAdds[_deferredBreachAddCount] = new float4(localPoint, severity);
                _deferredBreachAddCount++;
                return;
            }

            int weakestIndex = 0;
            float weakestSeverity = _deferredBreachAdds[0].w;
            for (int i = 1; i < _deferredBreachAdds.Length; i++)
            {
                float candidateSeverity = _deferredBreachAdds[i].w;
                if (candidateSeverity >= weakestSeverity)
                    continue;

                weakestSeverity = candidateSeverity;
                weakestIndex = i;
            }

            if (severity > weakestSeverity)
                _deferredBreachAdds[weakestIndex] = new float4(localPoint, severity);
        }

        private void FlushDeferredBreachAdds()
        {
            int count = _deferredBreachAddCount;
            if (count <= 0 || _breachRepairJobRunning)
                return;

            _deferredBreachAddCount = 0;
            for (int i = 0; i < count; i++)
            {
                float4 deferred = _deferredBreachAdds[i];
                _deferredBreachAdds[i] = float4.zero;
                AddOrRefreshBreachLocal(new float3(deferred.x, deferred.y, deferred.z), deferred.w);
            }
        }

        private void RegisterBreachScreenSpaceFeedback(float3 localPoint, float severity01)
        {
            if (!math.all(math.isfinite(localPoint)))
                return;

            BreachPressureSprayRequest request = new BreachPressureSprayRequest
            {
                LocalPoint = localPoint,
                Severity01 = math.saturate(severity01)
            };

            if (_pendingBreachPressureSprayCount < _pendingBreachPressureSprays.Length)
            {
                _pendingBreachPressureSprays[_pendingBreachPressureSprayCount++] = request;
                return;
            }

            int weakestIndex = 0;
            float weakestSeverity = _pendingBreachPressureSprays[0].Severity01;
            for (int i = 1; i < _pendingBreachPressureSprays.Length; i++)
            {
                float severity = _pendingBreachPressureSprays[i].Severity01;
                if (severity >= weakestSeverity)
                    continue;

                weakestSeverity = severity;
                weakestIndex = i;
            }

            if (request.Severity01 > weakestSeverity)
                _pendingBreachPressureSprays[weakestIndex] = request;
        }

        private void FlushQueuedBreachScreenSpaceFeedback()
        {
            int count = _pendingBreachPressureSprayCount;
            if (count <= 0)
                return;

            _pendingBreachPressureSprayCount = 0;
            IFluidDecalPresentationSink fluidDecals = _cachedFluidDecals;
            if (fluidDecals == null)
                return;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 fallbackDirection = -cachedTransform.up;
            for (int i = 0; i < count; i++)
            {
                BreachPressureSprayRequest request = _pendingBreachPressureSprays[i];
                _pendingBreachPressureSprays[i] = default;
                Vector3 local = new Vector3(request.LocalPoint.x, request.LocalPoint.y, request.LocalPoint.z);
                Vector3 worldPoint = cachedTransform.TransformPoint(local);
                Vector3 inward = cachedTransform.position - worldPoint;
                if (inward.sqrMagnitude <= Epsilon)
                    inward = fallbackDirection;

                fluidDecals.RegisterPressureSpray(worldPoint, ResolveSafeDirection(inward, fallbackDirection), request.Severity01);
            }
        }

        private void ScheduleBreachRepairJob()
        {
            if (!_pendingRepairQueued || _activeBreachCount <= 0 || _breachRepairJobLockMask != 0)
            {
                _pendingRepairQueued = false;
                return;
            }

            using (_breachRepairProfilerMarker.Auto())
            {
                NativeArray<float> severitySum;
                if (!TryAcquireStructuralMutationGuard())
                    return;

                try
                {
                    if (!TryResolveStructuralWriteBuffer(in _breachSeveritySumResultHandle, 1, out severitySum))
                        return;

                    severitySum[0] = _activeBreachSeveritySum;
                }
                finally
                {
                    ReleaseStructuralMutationGuard();
                }

                int lockMask = 0;
                IDataVault breachRepairGuardVault = null;
                if (!TryValidateStructuralJobBuffer(in _breachesHandle, LockBreaches, ref lockMask) ||
                    !TryValidateStructuralJobBuffer(in _breachSeveritySumResultHandle, LockBreachSeveritySumResult, ref lockMask) ||
                    !TryAcquireStructuralJobMutationGuard(ref lockMask, out breachRepairGuardVault))
                {
                    UnlockStructuralJobBuffers(lockMask, breachRepairGuardVault);
                    return;
                }

                if (!TryResolveBreachBuffer(out NativeArray<float4> breaches) ||
                    !TryResolveVaultBuffer(_dataVault, in _breachSeveritySumResultHandle, 1, out severitySum))
                {
                    UnlockStructuralJobBuffers(lockMask, breachRepairGuardVault);
                    return;
                }

                _breachRepairJobLockMask = lockMask;
                _breachRepairJobMutationGuardVault = breachRepairGuardVault;
                bool scheduled = false;
                try
                {
                    BreachRepairJob repairJob = new BreachRepairJob
                    {
                        Breaches = breaches,
                        SeveritySum = severitySum,
                        ActiveCount = _activeBreachCount,
                        LocalHitPoint = _pendingRepairLocalPoint,
                        RepairDelta = math.max(0f, _pendingRepairSeverityDelta),
                        RepairRadiusSq = BreachRepairRadiusMeters * BreachRepairRadiusMeters
                    };
                    _breachRepairJobHandle = repairJob.Schedule();
                    _breachRepairJobRunning = true;
                    _pendingRepairQueued = false;
                    H8Memory.RegisterActiveJob(VaultOwnerSystemId, _breachRepairJobHandle);
                    JobHandle.ScheduleBatchedJobs();
                    scheduled = true;
                }
                finally
                {
                    if (!scheduled)
                    {
                        UnlockStructuralJobBuffers(_breachRepairJobLockMask, _breachRepairJobMutationGuardVault);
                        _breachRepairJobLockMask = 0;
                        _breachRepairJobMutationGuardVault = null;
                        _breachRepairJobHandle = default;
                        _breachRepairJobRunning = false;
                    }
                }
            }
        }

        private void ConsumeCompletedBreachRepairJob()
        {
            if (!_breachRepairJobRunning)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _breachRepairJobHandle))
                return;

            try
            {
                _activeBreachSeveritySum = RecalculateBreachSeveritySum();
                _breachGpuDirty = true;
            }
            finally
            {
                _breachRepairJobRunning = false;
                UnlockStructuralJobBuffers(_breachRepairJobLockMask, _breachRepairJobMutationGuardVault);
                _breachRepairJobLockMask = 0;
                _breachRepairJobMutationGuardVault = null;
            }
        }

        private void CompactInactiveBreaches()
        {
            if (_breachRepairJobRunning || !TryAcquireStructuralMutationGuard())
                return;

            try
            {
                if (!TryAcquireBreachWriteBuffer(out NativeArray<float4> breaches))
                    return;

                int count = math.min(_activeBreachCount, breaches.Length);
                float sum = 0f;
                bool compacted = false;
                int i = 0;
                while (i < count)
                {
                    float4 breach = breaches[i];
                    if (breach.w > 0f && math.all(math.isfinite(breach)))
                    {
                        sum += breach.w;
                        i++;
                        continue;
                    }

                    int lastIndex = count - 1;
                    breaches[i] = breaches[lastIndex];
                    breaches[lastIndex] = float4.zero;
                    count--;
                    compacted = true;
                }

                _activeBreachCount = count;
                _activeBreachSeveritySum = math.isfinite(sum) ? sum : 0f;
                if (compacted)
                    _breachGpuDirty = true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }

        private float RecalculateBreachSeveritySum()
        {
            if (!TryReadBreachBuffer(out var breaches))
                return 0f;

            float sum = 0f;
            int count = math.min(_activeBreachCount, breaches.Length);
            for (int i = 0; i < count; i++)
                sum += math.max(0f, breaches[i].w);

            return math.isfinite(sum) ? sum : 0f;
        }

        private void PushDamageControlCoupling(float fixedDeltaTime)
        {
            float severitySum = math.max(0f, _activeBreachSeveritySum);
            if (severitySum <= 0f)
                return;

            float ambientPressureKPa = ResolveAmbientPressureKPa();
            if (fluidDynamics != null)
                fluidDynamics.ApplyDamageControlLeakMass(severitySum * ambientPressureKPa, fixedDeltaTime);

            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            _criticalBreachWarningTimer -= safeDeltaTime;
            if (_criticalBreachWarningTimer <= 0f)
            {
                float peakSeverity = ResolvePeakBreachSeverity();
                if (peakSeverity >= CriticalBreachThreshold)
                {
                    PublishCriticalBreachWarning(peakSeverity);
                    _criticalBreachWarningTimer = CriticalBreachWarningCadenceSeconds;
                }
            }

            _leakAudioTimer -= safeDeltaTime;
            if (_leakAudioTimer > 0f)
                return;

            PublishLeakImpactSignal(severitySum, ambientPressureKPa);
            _leakAudioTimer = math.max(0.05f, leakAudioCadenceSeconds);
        }

        private float ResolveAmbientPressureKPa()
        {
            float depthMeters = fluidDynamics != null ? math.max(0f, fluidDynamics.ExternalDepthMeters) : 0f;
            return (depthMeters * HectonPhysicsContract.HydrostaticPressureKPaPerMeter) + HectonSurvivalContract.KPaPerAtmosphere;
        }

        private bool PublishLeakImpactSignal(float severitySum, float ambientPressureKPa)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            if (!TryResolveAupFromRuntimeOrigin(cachedTransform.position, out double3 absolute))
                return false;

            Rigidbody hullBody = ResolveHullRigidbody();
            ImpactSignal signal = new ImpactSignal
            {
                PointAup = AbsoluteUniversePosition.FromAbsolutePosition(absolute),
                Force = math.max(0f, severitySum * ambientPressureKPa),
                Intensity = math.saturate(severitySum / math.max(1f, MaxActiveBreaches)),
                PrimaryBodyId = hullBody != null ? unchecked((uint)EntityId.ToULong(hullBody.GetEntityId())) : 0u,
                WeightClass = 1,
                PrimaryMaterialId = 0,
                SecondaryMaterialId = 0,
                Flags = LeakImpactFlags
            };
            bool accepted = SignalBus<ImpactSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_SubmarineStructuralGrid);
            if (!accepted)
                IncrementDroppedSignalCount();
            return accepted;
        }

        private float ResolvePeakBreachSeverity()
        {
            if (!TryReadBreachBuffer(out var breaches))
                return 0f;

            float peak = 0f;
            int count = math.min(_activeBreachCount, breaches.Length);
            for (int i = 0; i < count; i++)
                peak = math.max(peak, math.max(0f, breaches[i].w));

            return peak;
        }

        private bool PublishCriticalBreachWarning(float severity01)
        {
            Rigidbody hullBody = ResolveHullRigidbody();
            VocalWarningSignal warning = new VocalWarningSignal
            {
                WarningHash = VocalWarningHashes.HullBreach,
                SourceId = hullBody != null ? unchecked((uint)EntityId.ToULong(hullBody.GetEntityId())) : 0u,
                Severity01 = math.saturate(severity01),
                CooldownSeconds = CriticalBreachWarningCadenceSeconds,
                Priority = (byte)VocalWarningId.HullBreach,
                Flags = VocalWarningSignalFlags.HabitatIntegrityCompromised
            };
            bool accepted = SignalBus<VocalWarningSignal>.TryPushTracked(in warning, ref s_x001DirectSignalPushDropCount_SubmarineStructuralGrid);
            if (!accepted)
                IncrementDroppedSignalCount();
            return accepted;
        }

        private void IncrementDroppedSignalCount()
        {
            if (_droppedSignalCount < 0x3FFFFFFF)
                _droppedSignalCount++;
        }

        private void DispatchLeakPlumeCompute(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime)
                ? math.max(0f, fixedDeltaTime)
                : 0f;
            AdvanceLeakPlumeClock(safeFixedDeltaTime);

            if (!TryResolveBreachBuffer(out var breaches) || leakPlumeCompute == null)
                return;

            if (!HasLeakPlumeGpuResourcesReady())
            {
                QueueLeakPlumeGpuResourceRepair();
                return;
            }

            _visibleBreachCount = ResolveVisibleBreachCount();
            bool uploadedThisFrame = false;
            if (_breachGpuDirty)
            {
                _activeBreachGpuBufferIndex ^= 1;
                GraphicsBuffer uploadBuffer = ResolveWritableBreachGpuBuffer();
                if (uploadBuffer == null)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(uploadBuffer, breaches, math.max(1, _activeBreachCount));
                _breachGpuDirty = false;
                uploadedThisFrame = true;
            }

            GraphicsBuffer breachBuffer = ResolveWritableBreachGpuBuffer();
            if (breachBuffer == null || _leakPlumeParticleBuffer == null)
                return;

            if (_visibleBreachCount <= 0 && !uploadedThisFrame)
            {
                Shader.SetGlobalBuffer(_LeakParticleBufferId, _leakPlumeParticleBuffer);
                Shader.SetGlobalInt(_LeakVisibleBreachCountId, 0);
                return;
            }

            leakPlumeCompute.SetBuffer(_leakPlumeKernelIndex, _LeakBreachBufferId, breachBuffer);
            leakPlumeCompute.SetBuffer(_leakPlumeKernelIndex, _LeakParticleBufferId, _leakPlumeParticleBuffer);
            leakPlumeCompute.SetInt(_LeakBreachCountId, _activeBreachCount);
            leakPlumeCompute.SetInt(_LeakVisibleBreachCountId, _visibleBreachCount);
            leakPlumeCompute.SetFloat(_LeakDeltaTimeId, safeFixedDeltaTime);
            leakPlumeCompute.SetFloat(_LeakTimeId, ResolveLeakPlumeClockSeconds());
            leakPlumeCompute.SetVector(_LeakParamsId, new Vector4(LeakPlumeParticleCapacity, MaxActiveBreaches, 0f, 0f));
            int dispatchGroups = CeilDividePositive(MaxActiveBreaches, _leakPlumeThreadGroupSizeX);
            if (dispatchGroups <= 0)
                return;

            leakPlumeCompute.Dispatch(_leakPlumeKernelIndex, dispatchGroups, 1, 1);
            Shader.SetGlobalBuffer(_LeakParticleBufferId, _leakPlumeParticleBuffer);
            Shader.SetGlobalInt(_LeakVisibleBreachCountId, _visibleBreachCount);
        }

        private void QueueLeakPlumeVisualSync(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime)
                ? math.max(0f, fixedDeltaTime)
                : 0f;
            _pendingLeakPlumeDeltaSeconds = math.min(LeakPlumeClockMaxSeconds, _pendingLeakPlumeDeltaSeconds + safeFixedDeltaTime);
            _leakPlumeVisualDirty = true;
        }

        private void FlushLeakPlumeVisualSync()
        {
            if (!_leakPlumeVisualDirty)
                return;

            float deltaSeconds = _pendingLeakPlumeDeltaSeconds;
            _pendingLeakPlumeDeltaSeconds = 0f;
            _leakPlumeVisualDirty = false;
            DispatchLeakPlumeCompute(deltaSeconds);
        }

        private void AdvanceLeakPlumeClock(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _leakPlumeClockSeconds = math.min(LeakPlumeClockMaxSeconds, _leakPlumeClockSeconds + deltaTime);
        }

        private float ResolveLeakPlumeClockSeconds()
        {
            return _leakPlumeClockSeconds;
        }

        private void RenderLeakPlumeParticles()
        {
            int instanceCount = math.min(math.max(0, _visibleBreachCount) * 4, LeakPlumeParticleCapacity);
            if (instanceCount <= 0 ||
                leakPlumeRenderMaterial == null ||
                _leakPlumeParticleBuffer == null ||
                _leakPlumeDrawProperties == null)
            {
                return;
            }

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Camera viewCamera = ResolvePlayerCamera();
            Transform cameraTransform = viewCamera != null ? viewCamera.transform : null;
            Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            Vector3 cameraUp = cameraTransform != null ? cameraTransform.up : Vector3.up;

            _leakPlumeDrawProperties.Clear();
            _leakPlumeDrawProperties.SetBuffer(_LeakParticleBufferId, _leakPlumeParticleBuffer);
            _leakPlumeDrawProperties.SetFloat(_LeakUseParticleBufferId, 1f);
            _leakPlumeDrawProperties.SetFloat(_LeakParticleSizeId, math.max(0.01f, leakPlumeParticleSizeMeters));
            _leakPlumeDrawProperties.SetMatrix(_LeakLocalToWorldId, cachedTransform.localToWorldMatrix);
            _leakPlumeDrawProperties.SetVector(_LeakCameraRightId, cameraRight);
            _leakPlumeDrawProperties.SetVector(_LeakCameraUpId, cameraUp);

            RenderParams renderParams = new RenderParams(leakPlumeRenderMaterial)
            {
                worldBounds = ResolveLeakPlumeRenderBounds(cachedTransform),
                matProps = _leakPlumeDrawProperties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
                lightProbeUsage = LightProbeUsage.Off,
                camera = viewCamera
            };
            UnityEngine.Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, instanceCount);
        }

        private Bounds ResolveLeakPlumeRenderBounds(Transform cachedTransform)
        {
            Bounds bounds = hullCollider != null
                ? hullCollider.bounds
                : new Bounds(cachedTransform.position, Vector3.one * 4f);
            float padding = math.max(0f, leakPlumeRenderBoundsPaddingMeters + leakPlumeParticleSizeMeters);
            bounds.Expand(padding * 2f);
            return bounds;
        }

        private Camera ResolvePlayerCamera()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerRuntime;
            return playerContext != null ? playerContext.PlayerCamera : null;
        }

        private int ResolveVisibleBreachCount()
        {
            int activeCount = math.clamp(_activeBreachCount, 0, MaxActiveBreaches);
            float quality = ResolveLeakPresentationQuality01();
            float curve = quality * quality * (3f - 2f * quality);
            int visibleBudget = (int)math.round(math.lerp(MinVisibleBreachLimit, MaxActiveBreaches, curve));
            return math.min(activeCount, math.clamp(visibleBudget, MinVisibleBreachLimit, MaxActiveBreaches));
        }

        private static float ResolveLeakPresentationQuality01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private bool EnsureLeakPlumeGpuResources()
        {
            if (leakPlumeCompute == null || !_coldSupportsComputeShaders)
                return false;

            if (_leakPlumeKernelIndex < 0)
            {
                try
                {
                    if (!leakPlumeCompute.HasKernel("CSSpawnLeakParticles"))
                        return false;

                    _leakPlumeKernelIndex = leakPlumeCompute.FindKernel("CSSpawnLeakParticles");
                }
                catch (System.ObjectDisposedException)
                {
                    _leakPlumeKernelIndex = -1;
                    return false;
                }
                catch (System.InvalidOperationException)
                {
                    _leakPlumeKernelIndex = -1;
                    return false;
                }
                catch (System.ArgumentException)
                {
                    _leakPlumeKernelIndex = -1;
                    return false;
                }
                catch (MissingReferenceException)
                {
                    _leakPlumeKernelIndex = -1;
                    return false;
                }
                catch (UnityException)
                {
                    _leakPlumeKernelIndex = -1;
                    return false;
                }

                _leakPlumeThreadGroupSizeX = ResolveKernelThreadGroupSizeX(leakPlumeCompute, _leakPlumeKernelIndex);
            }

            if (_breachGpuBufferA == null)
                _breachGpuBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(MaxActiveBreaches);
            if (_breachGpuBufferB == null)
                _breachGpuBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(MaxActiveBreaches);
            if (_leakPlumeParticleBuffer == null)
                _leakPlumeParticleBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(LeakPlumeParticleCapacity);

            bool ready = _leakPlumeKernelIndex >= 0 &&
                         _breachGpuBufferA != null &&
                         _breachGpuBufferB != null &&
                         _leakPlumeParticleBuffer != null;
            if (ready)
                _leakPlumeGpuResourceRepairRequested = false;

            return ready;
        }

        private bool HasLeakPlumeGpuResourcesReady()
        {
            return leakPlumeCompute != null &&
                   _coldSupportsComputeShaders &&
                   _leakPlumeKernelIndex >= 0 &&
                   _leakPlumeThreadGroupSizeX > 0 &&
                   _breachGpuBufferA != null &&
                   _breachGpuBufferB != null &&
                   _leakPlumeParticleBuffer != null;
        }

        private void QueueLeakPlumeGpuResourceRepair()
        {
            if (leakPlumeCompute != null && _coldSupportsComputeShaders)
                _leakPlumeGpuResourceRepairRequested = true;
        }

        private void FlushLeakPlumeGpuResourceRepairSlow()
        {
            if (!_leakPlumeGpuResourceRepairRequested && HasLeakPlumeGpuResourcesReady())
                return;

            _leakPlumeGpuResourceRepairRequested = false;
            if (!EnsureLeakPlumeGpuResources())
                _leakPlumeGpuResourceRepairRequested = leakPlumeCompute != null && _coldSupportsComputeShaders;
        }

        private GraphicsBuffer ResolveWritableBreachGpuBuffer()
        {
            return (_activeBreachGpuBufferIndex & 1) == 0 ? _breachGpuBufferA : _breachGpuBufferB;
        }

        private void ReleaseLeakPlumeGpuResources()
        {
            ReleaseGraphicsBuffer(ref _breachGpuBufferA);
            ReleaseGraphicsBuffer(ref _breachGpuBufferB);
            ReleaseGraphicsBuffer(ref _leakPlumeParticleBuffer);
            _leakPlumeKernelIndex = -1;
            _leakPlumeThreadGroupSizeX = 0;
            _activeBreachGpuBufferIndex = 0;
            QueueLeakPlumeGpuResourceRepair();
        }

        private int ResolveKernelThreadGroupSizeX(ComputeShader compute, int kernel)
        {
            if (compute == null || kernel < 0 || !_coldSupportsComputeShaders)
                return 0;

            uint sizeX;
            uint sizeY;
            uint sizeZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return 0;

                compute.GetKernelThreadGroupSizes(kernel, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return 0;
            }
            catch (System.InvalidOperationException)
            {
                return 0;
            }
            catch (System.ArgumentException)
            {
                return 0;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                return 0;
            }
            catch (UnityEngine.UnityException)
            {
                return 0;
            }
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u || sizeX > int.MaxValue)
                return 0;

            ulong totalThreads = sizeX * (ulong)sizeY * sizeZ;
            return totalThreads <= PortableMaxComputeThreadsPerGroup ? (int)sizeX : 0;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;
            QueueLeakPlumeGpuResourceRepair();
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void WriteDamageControlTelemetry(uint reasonFlags)
        {
            WriteDamageControlTelemetry(reasonFlags, true, FailureCodeNone);
        }

        private void WriteDamageControlTelemetry(uint reasonFlags, bool allowNativeBreachRead)
        {
            WriteDamageControlTelemetry(reasonFlags, allowNativeBreachRead, FailureCodeNone);
        }

        private void WriteDamageControlTelemetry(uint reasonFlags, bool allowNativeBreachRead, ushort failureCode)
        {
            if (!TryAcquireStructuralWriteBuffer(in _damageControlTelemetryHandle, DamageControlTelemetryCapacity, out NativeArray<SubmarineStructuralTelemetryEntry> telemetry, out IDataVault telemetryWriteVault) ||
                telemetry.Length <= 0)
            {
                if (_structuralTelemetryFailureCount < uint.MaxValue)
                    _structuralTelemetryFailureCount++;
                return;
            }

            bool invalid = false;
            try
            {
                int index = _damageControlTelemetryHead % telemetry.Length;
                float4 first = allowNativeBreachRead &&
                               TryReadBreachBuffer(out var breaches) &&
                               _activeBreachCount > 0
                    ? breaches[0]
                    : float4.zero;
                invalid = allowNativeBreachRead &&
                          _activeBreachCount > 0 &&
                          (!math.all(math.isfinite(first)) || !math.isfinite(_activeBreachSeveritySum));
                uint flags = reasonFlags | (invalid ? DamageControlTelemetryInvalidFlag : 0u);
                ushort resolvedFailureCode = failureCode != FailureCodeNone
                    ? failureCode
                    : (invalid ? FailureCodeInvalidState : FailureCodeNone);
                uint sequence = ++_structuralTelemetrySequence;
                if (resolvedFailureCode != FailureCodeNone && _structuralTelemetryFailureCount < uint.MaxValue)
                    _structuralTelemetryFailureCount++;

                SubmarineStructuralTelemetryEntry entry = new SubmarineStructuralTelemetryEntry
                {
                    FirstBreachLocalSeverity = first,
                    SeveritySum = math.isfinite(_activeBreachSeveritySum) ? _activeBreachSeveritySum : 0f,
                    ActiveBreachCount = (ushort)math.clamp(_activeBreachCount, 0, ushort.MaxValue),
                    VisibleBreachCount = (ushort)math.clamp(_visibleBreachCount, 0, ushort.MaxValue),
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    Flags = flags,
                    StateHash = BuildDamageControlTelemetryHash(first, _activeBreachSeveritySum, _activeBreachCount, flags),
                    BufferId = _breachesHandle.BufferID,
                    Generation = _breachesHandle.Generation,
                    VaultGeneration = _dataVault != null ? _dataVault.VaultGenerationID : 0u,
                    Sequence = sequence,
                    FailureCode = resolvedFailureCode,
                    ConsecutiveFailureCount = (ushort)math.clamp(_structuralTelemetryFailureCount, 0u, ushort.MaxValue)
                };
                telemetry[index] = entry;
                _damageControlTelemetryHead = (_damageControlTelemetryHead + 1) % telemetry.Length;
            }
            finally
            {
                telemetryWriteVault.ReleaseWriteLock(in _damageControlTelemetryHandle, VaultOwnerSystemId);
            }

            if (invalid)
            {
                DumpDamageControlTelemetry();
                _activeBreachSeveritySum = RecalculateBreachSeveritySum();
            }
        }

        private static uint BuildDamageControlTelemetryHash(float4 first, float severitySum, int activeCount, uint flags)
        {
            uint hash = 2166136261u;
            hash = HashDamageControlTelemetry(hash, (uint)math.round(first.x * 1000f));
            hash = HashDamageControlTelemetry(hash, (uint)math.round(first.y * 1000f));
            hash = HashDamageControlTelemetry(hash, (uint)math.round(first.z * 1000f));
            hash = HashDamageControlTelemetry(hash, (uint)math.round(severitySum * 1000f));
            hash = HashDamageControlTelemetry(hash, (uint)math.max(0, activeCount));
            return HashDamageControlTelemetry(hash, flags);
        }

        private static uint HashDamageControlTelemetry(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private void DumpDamageControlTelemetry()
        {
            if (!TryReadVaultBuffer(_dataVault, in _damageControlTelemetryHandle, DamageControlTelemetryCapacity, out NativeArray<SubmarineStructuralTelemetryEntry>.ReadOnly telemetry))
                return;

            if (_damageControlTelemetryDumpRequested != 0)
                return;

            _damageControlTelemetryDumpRequested = 1;
            int count = math.min(telemetry.Length, _damageControlTelemetryDumpSnapshot.Length);
            for (int i = 0; i < count; i++)
                _damageControlTelemetryDumpSnapshot[i] = telemetry[i];

            _damageControlTelemetryDumpHead = _damageControlTelemetryHead;
            _damageControlTelemetryDumpRequested = 0;
        }

        private void EnsureDamageControlTelemetryDumpWorker()
        {
            _damageControlTelemetryDumpRequested = 0;
        }

        private void StopDamageControlTelemetryDumpWorker()
        {
            _damageControlTelemetryDumpRequested = 0;
        }

        private Rigidbody ResolveHullRigidbody()
        {
            if (_cachedHullRigidbody != null)
                return _cachedHullRigidbody;

            if (hullCollider != null && hullCollider.attachedRigidbody != null)
            {
                _cachedHullRigidbody = hullCollider.attachedRigidbody;
                return _cachedHullRigidbody;
            }

            return null;
        }

        private bool TryResolveLocalPointAup(Vector3 worldPoint, out float3 localPoint)
        {
            localPoint = default;
            if (!IsFiniteVector(worldPoint))
                return false;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            if (cachedTransform == null ||
                !IsFiniteVector(cachedTransform.position) ||
                !IsFiniteQuaternion(cachedTransform.rotation))
            {
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(worldPoint, out double3 hitAup) ||
                !TryResolveAupFromRuntimeOrigin(cachedTransform.position, out double3 rootAup))
            {
                return false;
            }

            double3 relativeWorldDouble = hitAup - rootAup;
            if (!math.all(math.isfinite(relativeWorldDouble)))
                return false;

            relativeWorldDouble = math.clamp(
                relativeWorldDouble,
                new double3(-AupLocalCastClampMeters, -AupLocalCastClampMeters, -AupLocalCastClampMeters),
                new double3(AupLocalCastClampMeters, AupLocalCastClampMeters, AupLocalCastClampMeters));
            Vector3 relativeWorld = new Vector3(
                (float)relativeWorldDouble.x,
                (float)relativeWorldDouble.y,
                (float)relativeWorldDouble.z);
            if (!IsFiniteVector(relativeWorld))
                return false;

            Quaternion inverseRotation = ConjugateUnitRotation(cachedTransform.rotation);
            Vector3 localVector = inverseRotation * relativeWorld;
            Vector3 lossyScale = cachedTransform.lossyScale;
            localVector.x /= ResolveSafeScale(lossyScale.x);
            localVector.y /= ResolveSafeScale(lossyScale.y);
            localVector.z /= ResolveSafeScale(lossyScale.z);
            if (!IsFiniteVector(localVector))
                return false;

            localPoint = new float3(localVector.x, localVector.y, localVector.z);
            return math.all(math.isfinite(localPoint));
        }

        private bool TryResolveLocalDirection(Vector3 worldDirection, out float3 localDirection)
        {
            localDirection = default;
            if (!IsFiniteVector(worldDirection))
                return false;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            if (cachedTransform == null || !IsFiniteQuaternion(cachedTransform.rotation))
                return false;

            Quaternion inverseRotation = ConjugateUnitRotation(cachedTransform.rotation);
            Vector3 localVector = inverseRotation * worldDirection;
            if (!IsFiniteVector(localVector))
                return false;

            localDirection = NormalizeSafe(
                new float3(localVector.x, localVector.y, localVector.z),
                new float3(0f, 1f, 0f));
            return math.all(math.isfinite(localDirection));
        }

        private static Vector3 ResolveSafeDirection(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (IsFiniteVector(value) && math.isfinite(lengthSq) && lengthSq > Epsilon)
                return value * math.rsqrt(lengthSq);

            float fallbackLengthSq = fallback.sqrMagnitude;
            if (IsFiniteVector(fallback) && math.isfinite(fallbackLengthSq) && fallbackLengthSq > Epsilon)
                return fallback * math.rsqrt(fallbackLengthSq);

            return Vector3.up;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static Quaternion ConjugateUnitRotation(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, -rotation.y, -rotation.z, rotation.w);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out double3 aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            aup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(aup));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static float ResolveSafeScale(float scale)
        {
            return math.isfinite(scale) && math.abs(scale) > Epsilon ? scale : 1f;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > Epsilon
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static Color LerpColorClamped(Color from, Color to, float t)
        {
            float clampedT = math.saturate(t);
            return new Color(
                math.lerp(from.r, to.r, clampedT),
                math.lerp(from.g, to.g, clampedT),
                math.lerp(from.b, to.b, clampedT),
                math.lerp(from.a, to.a, clampedT));
        }

        private float3 ResolveOutwardHullNormal(float3 localPoint, float3 candidateNormal)
        {
            float3 outward = localPoint - new float3(localGridCenter.x, localGridCenter.y, localGridCenter.z);
            float3 resolvedNormal = NormalizeSafe(candidateNormal, float3.zero);
            if (math.lengthsq(resolvedNormal) <= Epsilon)
                return NormalizeSafe(outward, new float3(0f, 1f, 0f));

            if (math.dot(resolvedNormal, outward) < 0f)
                resolvedNormal = -resolvedNormal;

            return resolvedNormal;
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
            {
                _atmosphereSystem = atmosphereSystemSource as ISubmarineAtmosphereRoomReadModel;
                if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                    _atmosphereSystem = ComponentReferenceUtility.ResolveParentService<ISubmarineAtmosphereRoomReadModel>(this);
            }

            if (hullCollider == null)
                TryGetComponent(out hullCollider);

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            Rigidbody registryBody = submarine != null ? submarine.HullRigidbody : null;
            if (registryBody != null)
                _cachedHullRigidbody = registryBody;
            else if (hullCollider != null && hullCollider.attachedRigidbody != null)
                _cachedHullRigidbody = hullCollider.attachedRigidbody;
            else if (_cachedHullRigidbody == null)
                TryGetComponent(out _cachedHullRigidbody);

            if (_damageEmitter == null)
            {
                _componentSearchBuffer.Clear();
                GetComponents(_componentSearchBuffer);
                for (int i = 0; i < _componentSearchBuffer.Count; i++)
                {
                    MonoBehaviour component = _componentSearchBuffer[i];
                    if (component is IDamageSignalEmitter emitter)
                    {
                        _damageEmitter = emitter;
                        break;
                    }
                }
            }
        }

        private void CacheGlobalRegistryServices()
        {
            _cachedPlayerRuntime = GlobalRegistry.Player;
            _cachedFluidDecals = GlobalRegistry.FluidDecalPresentation;
        }

        private void ClearGlobalRegistryServiceCache()
        {
            _cachedPlayerRuntime = null;
            _cachedFluidDecals = null;
        }

        private void ApplyAbyssalCompression()
        {
            if (fluidDynamics == null)
            {
                PublishFakeCrushDepthGlobals(0f);
                return;
            }

            float depthMeters = math.max(0f, fluidDynamics.ExternalDepthMeters);
            PublishFakeCrushDepthGlobals(depthMeters);
            if (depthMeters <= math.max(0f, compressionDepthThresholdMeters))
            {
                _debugCompressionScale = 1f;
                fluidDynamics.SetCompartmentCompressionScale(1f);
                return;
            }

            float hydrostaticPressureKPa =
                (depthMeters * HectonPhysicsContract.HydrostaticPressureKPaPerMeter) + HectonSurvivalContract.KPaPerAtmosphere;
            float startPressureKPa =
                (math.max(0f, compressionDepthThresholdMeters) * HectonPhysicsContract.HydrostaticPressureKPaPerMeter) + HectonSurvivalContract.KPaPerAtmosphere;
            float pressureRangeKPa = math.max(1f, compressionFullPressureKPa - startPressureKPa);
            float compression01 = math.saturate((hydrostaticPressureKPa - startPressureKPa) / pressureRangeKPa);
            float compressionScale = 1f - (compression01 * math.saturate(maximumVolumeCompressionNormalized));
            _debugCompressionScale = compressionScale;
            fluidDynamics.SetCompartmentCompressionScale(compressionScale);
        }

        private void PublishFakeCrushDepthGlobals(float depthMeters)
        {
            _pendingFakeCrushDepthMeters = math.max(0f, depthMeters);
            _fakeCrushDepthVisualDirty = true;
        }

        private void FlushFakeCrushDepthGlobals()
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 center = cachedTransform.position;
            float depthMeters = _pendingFakeCrushDepthMeters;
            Vector4 centerRadius = new Vector4(
                center.x,
                center.y,
                center.z,
                math.max(0f, fakeCrushEffectRadiusMeters));
            Vector4 depthParams = new Vector4(
                math.max(0f, depthMeters),
                math.max(1f, fakeCrushDepthMeters),
                math.max(0f, fakeCrushMaxVertexDisplacementMeters),
                math.max(0.001f, fakeCrushVoronoiScale));

            if (!_fakeCrushDepthVisualDirty &&
                Approximately(_publishedCrushCenterRadius, centerRadius) &&
                Approximately(_publishedCrushDepthParams, depthParams))
            {
                return;
            }

            _fakeCrushDepthVisualDirty = false;
            Shader.SetGlobalVector(_ShaderCrushCenterRadiusId, centerRadius);
            Shader.SetGlobalVector(_ShaderCrushDepthParamsId, depthParams);
            _publishedCrushCenterRadius = centerRadius;
            _publishedCrushDepthParams = depthParams;
        }

        private void ResetFakeCrushDepthGlobals()
        {
            _fakeCrushDepthVisualDirty = false;
            _pendingFakeCrushDepthMeters = 0f;
            Vector4 zero = Vector4.zero;
            Shader.SetGlobalVector(_ShaderCrushCenterRadiusId, zero);
            Shader.SetGlobalVector(_ShaderCrushDepthParamsId, zero);
            _publishedCrushCenterRadius = new Vector4(float.NaN, 0f, 0f, 0f);
            _publishedCrushDepthParams = new Vector4(float.NaN, 0f, 0f, 0f);
        }

        private static bool Approximately(Vector4 a, Vector4 b)
        {
            const float EpsilonSq = 0.0001f;
            Vector4 delta = a - b;
            return delta.sqrMagnitude <= EpsilonSq;
        }

        private void ResolveGridBounds()
        {
            if (!deriveGridBoundsFromHullCollider || hullCollider == null)
                return;

            Bounds bounds = hullCollider.bounds;
            if (!TryResolveLocalPointAup(bounds.center, out float3 localCenterAup))
                return;

            Vector3 localCenter = new Vector3(localCenterAup.x, localCenterAup.y, localCenterAup.z);
            Vector3 localExtents = transform.InverseTransformVector(bounds.extents);
            localGridCenter = localCenter;
            localGridSize = new Vector3(
                math.max(math.abs(localExtents.x) * 2f, 0.5f),
                math.max(math.abs(localExtents.y) * 2f, 0.5f),
                math.max(math.abs(localExtents.z) * 2f, 0.5f));
        }

        private IDataVault ResolveDataVault()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                return vault;

            vault = GlobalRegistry.DataVault;
            _dataVault = vault;
            return vault;
        }

        private bool EnsureBreachVaultState()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            return EnsureVaultHandle(
                       vault,
                       BufferID.SubmarineStructuralBreaches,
                       MaxActiveBreaches,
                       ref _breachesHandle,
                       NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(
                       vault,
                       BufferID.SubmarineDamageControlBlackBox,
                       DamageControlTelemetryCapacity,
                       ref _damageControlTelemetryHandle,
                       NativeArrayOptions.ClearMemory);
        }

        private bool EnsureStructuralVaultState()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            int cellCount = ResolveCellCount();
            int breachWordCount = (cellCount + 63) >> 6;
            return EnsureVaultHandle(vault, StructuralGridVaultRoute.CellIntegrityFront, cellCount, ref _cellIntegrityFrontHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.CellIntegrityBack, cellCount, ref _cellIntegrityBackHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.CellFatigue, cellCount, ref _cellFatigueHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.CellCompartmentIndices, cellCount, ref _cellCompartmentIndicesHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.HullBreachMaskFront, breachWordCount, ref _hullBreachMaskFrontHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.HullBreachMaskBack, breachWordCount, ref _hullBreachMaskBackHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.CompartmentBreachAreasFront, CompartmentCapacity, ref _compartmentBreachAreasFrontHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.CompartmentBreachAreasBack, CompartmentCapacity, ref _compartmentBreachAreasBackHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.QueuedImpacts, MaxQueuedImpacts, ref _queuedImpactsHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.ScheduledImpacts, MaxQueuedImpacts, ref _scheduledImpactsHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.CompartmentCentroids, CompartmentCapacity, ref _compartmentCentroidsHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.FatigueCompartmentFlags, CompartmentCapacity, ref _fatigueCompartmentFlagsHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.FatigueIntegrityLossPerCycle, CompartmentCapacity, ref _fatigueIntegrityLossPerCycleHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.FatiguePeakResult, 1, ref _fatiguePeakResultHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultHandle(vault, StructuralGridVaultRoute.BreachSeveritySumResult, 1, ref _breachSeveritySumResultHandle, NativeArrayOptions.UninitializedMemory) &&
                   EnsureBreachVaultState();
        }

        private bool EnsureVaultHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            NativeArrayOptions options)
            where T : struct
        {
            if (TryResolveVaultBuffer(vault, in handle, requiredLength, out NativeArray<T> _))
                return true;

            if (!IsVaultOpenForStructuralAccess(vault) || requiredLength <= 0)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;
            }
            else
            {
                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    VaultOwnerSystemId,
                    options);
            }

            return TryResolveVaultBuffer(vault, in handle, requiredLength, out NativeArray<T> _);
        }

        private bool TryResolveBreachBuffer(out NativeArray<float4> breaches)
        {
            breaches = default;
            return TryResolveVaultBuffer(_dataVault, in _breachesHandle, MaxActiveBreaches, out breaches);
        }

        private bool TryReadBreachBuffer(out NativeArray<float4>.ReadOnly breaches)
        {
            return TryReadVaultBuffer(_dataVault, in _breachesHandle, MaxActiveBreaches, out breaches);
        }

        private bool TryAcquireBreachWriteBuffer(out NativeArray<float4> breaches)
        {
            return TryResolveStructuralWriteBuffer(in _breachesHandle, MaxActiveBreaches, out breaches);
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return IsVaultOpenForStructuralAccess(vault) &&
                   requiredLength > 0 &&
                   IsGenerationHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return IsVaultOpenForStructuralAccess(vault) &&
                   requiredLength > 0 &&
                   IsGenerationHandleCreated(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= requiredLength;
        }

        private bool TryResolveStructuralNativeViews()
        {
            int cellCount = ResolveCellCount();
            int breachWordCount = (cellCount + 63) >> 6;
            IDataVault vault = _dataVault;
            return TryResolveVaultBuffer(vault, in _cellIntegrityFrontHandle, cellCount, out NativeArray<byte> _) &&
                   TryResolveVaultBuffer(vault, in _cellIntegrityBackHandle, cellCount, out NativeArray<byte> _) &&
                   TryResolveVaultBuffer(vault, in _cellFatigueHandle, cellCount, out NativeArray<byte> _) &&
                   TryResolveVaultBuffer(vault, in _cellCompartmentIndicesHandle, cellCount, out NativeArray<byte> _) &&
                   TryResolveVaultBuffer(vault, in _hullBreachMaskFrontHandle, breachWordCount, out NativeArray<ulong> _) &&
                   TryResolveVaultBuffer(vault, in _hullBreachMaskBackHandle, breachWordCount, out NativeArray<ulong> _) &&
                   TryResolveVaultBuffer(vault, in _compartmentBreachAreasFrontHandle, CompartmentCapacity, out NativeArray<float> _) &&
                   TryResolveVaultBuffer(vault, in _compartmentBreachAreasBackHandle, CompartmentCapacity, out NativeArray<float> _) &&
                   TryResolveVaultBuffer(vault, in _queuedImpactsHandle, MaxQueuedImpacts, out NativeArray<ImpactCommand> _) &&
                   TryResolveVaultBuffer(vault, in _scheduledImpactsHandle, MaxQueuedImpacts, out NativeArray<ImpactCommand> _) &&
                   TryResolveVaultBuffer(vault, in _compartmentCentroidsHandle, CompartmentCapacity, out NativeArray<float3> _) &&
                   TryResolveVaultBuffer(vault, in _fatigueCompartmentFlagsHandle, CompartmentCapacity, out NativeArray<byte> _) &&
                   TryResolveVaultBuffer(vault, in _fatigueIntegrityLossPerCycleHandle, CompartmentCapacity, out NativeArray<float> _) &&
                   TryResolveVaultBuffer(vault, in _fatiguePeakResultHandle, 1, out NativeArray<float> _) &&
                   TryResolveVaultBuffer(vault, in _breachSeveritySumResultHandle, 1, out NativeArray<float> _);
        }

        private bool TryAcquireStructuralWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer,
            out IDataVault writeVault)
            where T : struct
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _dataVault;
            if (!IsVaultOpenForStructuralAccess(vault))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryCompactionFenceFlag, FailureCodeCompactionFence);
                return false;
            }

            if (!IsGenerationHandleCreated(in handle))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryStaleHandleFlag, FailureCodeStaleHandle);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryWriteLockFailureFlag, FailureCodeWriteLock);
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (!IsVaultOpenForStructuralAccess(vault))
                {
                    RecordStructuralVaultFailure(in handle, DamageControlTelemetryCompactionFenceFlag, FailureCodeCompactionFence);
                    buffer = default;
                    return false;
                }

                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    writeVault = vault;
                    releaseOnFailure = false;
                    return true;
                }

                RecordStructuralVaultFailure(in handle, DamageControlTelemetryCapacityFailureFlag, FailureCodeCapacityMismatch);
                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            }
        }

        private bool TryResolveStructuralWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (!IsVaultOpenForStructuralAccess(vault))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryCompactionFenceFlag, FailureCodeCompactionFence);
                return false;
            }

            if (!IsGenerationHandleCreated(in handle))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryStaleHandleFlag, FailureCodeStaleHandle);
                return false;
            }

            if (!TryResolveVaultBuffer(vault, in handle, requiredLength, out buffer))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryCapacityFailureFlag, FailureCodeCapacityMismatch);
                return false;
            }

            return true;
        }

        private bool TryValidateStructuralJobBuffer<T>(in VaultGenerationHandle<T> handle, int bit, ref int lockMask)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (!IsVaultOpenForStructuralAccess(vault))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryCompactionFenceFlag, FailureCodeCompactionFence);
                return false;
            }

            if (!IsGenerationHandleCreated(in handle))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryStaleHandleFlag, FailureCodeStaleHandle);
                return false;
            }

            if (!IsVaultOpenForStructuralAccess(vault))
            {
                RecordStructuralVaultFailure(in handle, DamageControlTelemetryCompactionFenceFlag, FailureCodeCompactionFence);
                return false;
            }

            lockMask |= bit;
            return true;
        }

        private void UnlockStructuralJobBuffers(int mask, IDataVault guardVault)
        {
            IDataVault vault = guardVault ?? _dataVault;
            if (vault == null || mask == 0 || (mask & LockStructuralJobMutationGuard) == 0)
                return;

            vault.ReleaseMutationGuard(ResolveStructuralJobMutationGuardMask(mask));
        }

        private bool TryAcquireStructuralJobMutationGuard(ref int lockMask, out IDataVault guardVault)
        {
            guardVault = null;
            IDataVault vault = _dataVault;
            if (!IsVaultOpenForStructuralAccess(vault) || lockMask == 0)
                return false;

            ulong guardMask = ResolveStructuralJobMutationGuardMask(lockMask);
            if (guardMask == 0UL || !vault.TryAcquireMutationGuard(guardMask))
            {
                WriteDamageControlTelemetry(DamageControlTelemetryBufferLockFailureFlag, false, FailureCodeBufferLock);
                return false;
            }

            lockMask |= LockStructuralJobMutationGuard;
            guardVault = vault;
            if (IsVaultOpenForStructuralAccess(vault))
                return true;

            UnlockStructuralJobBuffers(lockMask, guardVault);
            lockMask = 0;
            guardVault = null;
            WriteDamageControlTelemetry(DamageControlTelemetryCompactionFenceFlag, false, FailureCodeCompactionFence);
            return false;
        }

        private ulong ResolveStructuralJobMutationGuardMask(int mask)
        {
            ulong guardMask = 0UL;
            if ((mask & LockBreaches) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_breachesHandle.BufferID);
            if ((mask & LockBreachSeveritySumResult) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_breachSeveritySumResultHandle.BufferID);
            if ((mask & LockFatiguePeakResult) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_fatiguePeakResultHandle.BufferID);
            if ((mask & LockFatigueIntegrityLossPerCycle) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_fatigueIntegrityLossPerCycleHandle.BufferID);
            if ((mask & LockFatigueCompartmentFlags) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_fatigueCompartmentFlagsHandle.BufferID);
            if ((mask & LockCompartmentCentroids) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_compartmentCentroidsHandle.BufferID);
            if ((mask & LockScheduledImpacts) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_scheduledImpactsHandle.BufferID);
            if ((mask & LockQueuedImpacts) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_queuedImpactsHandle.BufferID);
            if ((mask & LockCompartmentBreachAreasBack) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_compartmentBreachAreasBackHandle.BufferID);
            if ((mask & LockCompartmentBreachAreasFront) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_compartmentBreachAreasFrontHandle.BufferID);
            if ((mask & LockHullBreachMaskBack) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_hullBreachMaskBackHandle.BufferID);
            if ((mask & LockHullBreachMaskFront) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_hullBreachMaskFrontHandle.BufferID);
            if ((mask & LockCellCompartmentIndices) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_cellCompartmentIndicesHandle.BufferID);
            if ((mask & LockCellFatigue) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_cellFatigueHandle.BufferID);
            if ((mask & LockCellIntegrityBack) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_cellIntegrityBackHandle.BufferID);
            if ((mask & LockCellIntegrityFront) != 0) guardMask |= StructuralJobMutationGuardBit((BufferID)_cellIntegrityFrontHandle.BufferID);
            return guardMask;
        }

        private static ulong StructuralJobMutationGuardBit(BufferID bufferId)
        {
            return bufferId == BufferID.Unknown ? 0UL : 1UL << ((int)bufferId & 31);
        }

        private bool TryAcquireStructuralMutationGuard()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(StructuralMutationGuardMask))
                return false;

            _structuralMutationGuardVault = vault;
            return true;
        }

        private void ReleaseStructuralMutationGuard()
        {
            IDataVault vault = _structuralMutationGuardVault;
            _structuralMutationGuardVault = null;
            vault?.ReleaseMutationGuard(StructuralMutationGuardMask);
        }

        private static bool IsVaultOpenForStructuralAccess(IDataVault vault)
        {
            return vault != null && !vault.IsCompactionFenceActive;
        }

        private void RecordStructuralVaultFailure<T>(
            in VaultGenerationHandle<T> handle,
            uint reasonFlags,
            ushort failureCode)
            where T : struct
        {
            if (_structuralTelemetryFailureCount < uint.MaxValue)
                _structuralTelemetryFailureCount++;

            if (_damageControlTelemetryHandle.BufferID == 0u ||
                handle.BufferID == _damageControlTelemetryHandle.BufferID)
            {
                return;
            }

            WriteDamageControlTelemetry(reasonFlags, false, failureCode);
        }

        private static bool IsGenerationHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   handle.SystemID == unchecked((uint)VaultOwnerSystemId);
        }

        private void EnsureNativeState()
        {
            if (TryResolveStructuralNativeViews())
            {
                _nativeStateReady = true;
                return;
            }

            if (!EnsureStructuralVaultState() || !TryResolveStructuralNativeViews())
                return;

            _nativeStateReady = true;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
            _mappedCompartmentCount = 0;
            _pendingMappedCompartmentCount = 0;
        }

        private void SeedStructuralState()
        {
            if (!TryAcquireStructuralMutationGuard())
                return;

            try
            {
                int cellCount = ResolveCellCount();
                int breachWordCount = (cellCount + 63) >> 6;
                if (!TryResolveStructuralWriteBuffer(in _cellIntegrityFrontHandle, cellCount, out NativeArray<byte> cellIntegrityFront) ||
                    !TryResolveStructuralWriteBuffer(in _cellIntegrityBackHandle, cellCount, out NativeArray<byte> cellIntegrityBack) ||
                    !TryResolveStructuralWriteBuffer(in _cellFatigueHandle, cellCount, out NativeArray<byte> cellFatigue) ||
                    !TryResolveStructuralWriteBuffer(in _cellCompartmentIndicesHandle, cellCount, out NativeArray<byte> cellCompartmentIndices) ||
                    !TryResolveStructuralWriteBuffer(in _hullBreachMaskFrontHandle, breachWordCount, out NativeArray<ulong> hullBreachMaskFront) ||
                    !TryResolveStructuralWriteBuffer(in _hullBreachMaskBackHandle, breachWordCount, out NativeArray<ulong> hullBreachMaskBack) ||
                    !TryResolveStructuralWriteBuffer(in _compartmentBreachAreasFrontHandle, CompartmentCapacity, out NativeArray<float> compartmentBreachAreasFront) ||
                    !TryResolveStructuralWriteBuffer(in _compartmentBreachAreasBackHandle, CompartmentCapacity, out NativeArray<float> compartmentBreachAreasBack) ||
                    !TryResolveStructuralWriteBuffer(in _queuedImpactsHandle, MaxQueuedImpacts, out NativeArray<ImpactCommand> queuedImpacts) ||
                    !TryResolveStructuralWriteBuffer(in _scheduledImpactsHandle, MaxQueuedImpacts, out NativeArray<ImpactCommand> scheduledImpacts) ||
                    !TryResolveStructuralWriteBuffer(in _compartmentCentroidsHandle, CompartmentCapacity, out NativeArray<float3> compartmentCentroids) ||
                    !TryResolveStructuralWriteBuffer(in _fatigueCompartmentFlagsHandle, CompartmentCapacity, out NativeArray<byte> fatigueFlags) ||
                    !TryResolveStructuralWriteBuffer(in _fatigueIntegrityLossPerCycleHandle, CompartmentCapacity, out NativeArray<float> fatigueLossPerCycle) ||
                    !TryResolveStructuralWriteBuffer(in _fatiguePeakResultHandle, 1, out NativeArray<float> fatiguePeakResult) ||
                    !TryResolveStructuralWriteBuffer(in _breachSeveritySumResultHandle, 1, out NativeArray<float> breachSeveritySumResult))
                {
                    return;
                }

                for (int i = 0; i < cellIntegrityFront.Length; i++)
                {
                    cellIntegrityFront[i] = FullIntegrity;
                    cellIntegrityBack[i] = FullIntegrity;
                    cellFatigue[i] = 0;
                    cellCompartmentIndices[i] = UnmappedCompartment;
                }

                for (int i = 0; i < hullBreachMaskFront.Length; i++)
                {
                    hullBreachMaskFront[i] = 0UL;
                    hullBreachMaskBack[i] = 0UL;
                }

                for (int i = 0; i < CompartmentCapacity; i++)
                {
                    compartmentBreachAreasFront[i] = 0f;
                    compartmentBreachAreasBack[i] = 0f;
                    compartmentCentroids[i] = float3.zero;
                    fatigueFlags[i] = 0;
                    fatigueLossPerCycle[i] = 0f;
                }

                for (int i = 0; i < MaxQueuedImpacts; i++)
                {
                    queuedImpacts[i] = default;
                    scheduledImpacts[i] = default;
                }

                _cellBreachAreaSquareMeters = ResolveCellBreachAreaSquareMeters();
                _fatiguePeakNormalized = 0f;
                fatiguePeakResult[0] = 0f;
                breachSeveritySumResult[0] = 0f;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }

            if (TryAcquireStructuralMutationGuard())
            {
                try
                {
                    if (TryAcquireBreachWriteBuffer(out NativeArray<float4> breaches))
                    {
                        for (int i = 0; i < breaches.Length; i++)
                            breaches[i] = float4.zero;
                    }
                }
                finally
                {
                    ReleaseStructuralMutationGuard();
                }
            }

            _recentImpactSeverityNormalized = 0f;
            _activeBreachCount = 0;
            _visibleBreachCount = 0;
            _activeBreachSeveritySum = 0f;
            _pendingRepairQueued = false;
            _breachGpuDirty = true;
            _mappedCompartmentCount = 0;
            EnsureCompartmentMappingReady();
            for (int i = 0; i < CompartmentCapacity; i++)
                _previousCompartmentPressuresKPa[i] = 0f;
        }

        private bool EnsureCompartmentMappingReady()
        {
            if (!IsReady || fluidDynamics == null || fluidDynamics.CompartmentCount <= 0)
                return true;

            if (_mappingJobRunning)
                return false;

            int compartmentCount = math.min(fluidDynamics.CompartmentCount, CompartmentCapacity);
            if (_mappedCompartmentCount == compartmentCount)
                return true;

            NativeArray<float3> compartmentCentroids;
            if (!TryAcquireStructuralMutationGuard())
                return false;

            try
            {
                if (!TryResolveStructuralWriteBuffer(in _compartmentCentroidsHandle, CompartmentCapacity, out compartmentCentroids))
                    return false;

                for (int compartmentIndex = 0; compartmentIndex < compartmentCount; compartmentIndex++)
                    compartmentCentroids[compartmentIndex] = fluidDynamics.GetCompartmentCentroid(compartmentIndex);
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }

            int lockMask = 0;
            IDataVault mappingGuardVault = null;
            if (!TryValidateStructuralJobBuffer(in _compartmentCentroidsHandle, LockCompartmentCentroids, ref lockMask) ||
                !TryValidateStructuralJobBuffer(in _cellCompartmentIndicesHandle, LockCellCompartmentIndices, ref lockMask) ||
                !TryAcquireStructuralJobMutationGuard(ref lockMask, out mappingGuardVault))
            {
                UnlockStructuralJobBuffers(lockMask, mappingGuardVault);
                return false;
            }

            if (!TryResolveVaultBuffer(_dataVault, in _compartmentCentroidsHandle, CompartmentCapacity, out compartmentCentroids) ||
                !TryResolveVaultBuffer(_dataVault, in _cellCompartmentIndicesHandle, ResolveCellCount(), out NativeArray<byte> cellCompartmentIndices))
            {
                UnlockStructuralJobBuffers(lockMask, mappingGuardVault);
                return false;
            }

            _pendingMappedCompartmentCount = compartmentCount;
            _mappingJobLockMask = lockMask;
            _mappingJobMutationGuardVault = mappingGuardVault;
            bool scheduled = false;
            try
            {
                HullCompartmentMappingJob mappingJob = new HullCompartmentMappingJob
                {
                    CompartmentCentroids = compartmentCentroids,
                    CellCompartmentIndices = cellCompartmentIndices,
                    CompartmentCount = compartmentCount,
                    GridWidth = math.max(1, gridWidth),
                    GridHeight = math.max(1, gridHeight),
                    GridDepth = math.max(1, gridDepth),
                    GridCenterLocal = localGridCenter,
                    GridSizeLocal = localGridSize
                };
                _mappingJobHandle = mappingJob.Schedule(cellCompartmentIndices.Length, 64);
                _mappingJobRunning = true;
                H8Memory.RegisterActiveJob(VaultOwnerSystemId, _mappingJobHandle);
                JobHandle.ScheduleBatchedJobs();
                scheduled = true;
                return false;
            }
            finally
            {
                if (!scheduled)
                {
                    UnlockStructuralJobBuffers(_mappingJobLockMask, _mappingJobMutationGuardVault);
                    _mappingJobLockMask = 0;
                    _mappingJobMutationGuardVault = null;
                    _mappingJobHandle = default;
                    _mappingJobRunning = false;
                    _pendingMappedCompartmentCount = 0;
                }
            }
        }

        private void ConsumeCompletedMappingJob()
        {
            if (!_mappingJobRunning)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _mappingJobHandle))
                return;

            try
            {
                _mappedCompartmentCount = _pendingMappedCompartmentCount;
            }
            finally
            {
                _mappingJobRunning = false;
                _pendingMappedCompartmentCount = 0;
                UnlockStructuralJobBuffers(_mappingJobLockMask, _mappingJobMutationGuardVault);
                _mappingJobLockMask = 0;
                _mappingJobMutationGuardVault = null;
            }
        }

        private void ApplyPressureCycleFatigue()
        {
            if (!IsReady ||
                _fatigueJobRunning ||
                _atmosphereSystem == null ||
                !_atmosphereSystem.IsAtmosphereRuntimeActive ||
                fluidDynamics == null)
            {
                return;
            }

            if (!TryAcquireStructuralMutationGuard())
                return;

            bool scheduledAny = false;
            try
            {
                if (!TryResolveStructuralWriteBuffer(in _fatigueCompartmentFlagsHandle, CompartmentCapacity, out NativeArray<byte> fatigueFlags) ||
                    !TryResolveStructuralWriteBuffer(in _fatigueIntegrityLossPerCycleHandle, CompartmentCapacity, out NativeArray<float> fatigueLossPerCycle))
                {
                    return;
                }

                int compartmentCount = math.min(fluidDynamics.CompartmentCount, CompartmentCapacity);
                float thresholdKPa = math.max(0f, fatiguePressureThresholdKPa);
                for (int compartmentIndex = 0; compartmentIndex < compartmentCount; compartmentIndex++)
                {
                    float previousPressure = _previousCompartmentPressuresKPa[compartmentIndex];
                    float currentPressure = _atmosphereSystem.GetRoomPressureKPa(compartmentIndex);
                    _previousCompartmentPressuresKPa[compartmentIndex] = currentPressure;

                    if (previousPressure >= thresholdKPa || currentPressure < thresholdKPa)
                        continue;

                    float thermalMultiplier = _atmosphereSystem.ResolveThermalFatigueMultiplier(compartmentIndex);
                    fatigueFlags[compartmentIndex] = 1;
                    fatigueLossPerCycle[compartmentIndex] = math.max(0f, fatigueIntegrityLossPerCycle * thermalMultiplier);
                    scheduledAny = true;
                }
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }

            if (scheduledAny)
                ScheduleFatigueJob();
        }

        private void ScheduleFatigueJob()
        {
            if (_fatigueJobRunning ||
                !IsReady ||
                _fatigueJobLockMask != 0)
            {
                return;
            }

            if (!TryAcquireStructuralMutationGuard())
                return;

            try
            {
                if (!TryResolveStructuralWriteBuffer(in _fatiguePeakResultHandle, 1, out NativeArray<float> writablePeakResult))
                    return;

                writablePeakResult[0] = _fatiguePeakNormalized;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }

            int lockMask = 0;
            IDataVault fatigueGuardVault = null;
            if (!TryValidateStructuralJobBuffer(in _cellCompartmentIndicesHandle, LockCellCompartmentIndices, ref lockMask) ||
                !TryValidateStructuralJobBuffer(in _cellIntegrityFrontHandle, LockCellIntegrityFront, ref lockMask) ||
                !TryValidateStructuralJobBuffer(in _cellIntegrityBackHandle, LockCellIntegrityBack, ref lockMask) ||
                !TryValidateStructuralJobBuffer(in _cellFatigueHandle, LockCellFatigue, ref lockMask) ||
                !TryValidateStructuralJobBuffer(in _fatigueCompartmentFlagsHandle, LockFatigueCompartmentFlags, ref lockMask) ||
                !TryValidateStructuralJobBuffer(in _fatigueIntegrityLossPerCycleHandle, LockFatigueIntegrityLossPerCycle, ref lockMask) ||
                !TryValidateStructuralJobBuffer(in _fatiguePeakResultHandle, LockFatiguePeakResult, ref lockMask) ||
                !TryAcquireStructuralJobMutationGuard(ref lockMask, out fatigueGuardVault))
            {
                UnlockStructuralJobBuffers(lockMask, fatigueGuardVault);
                return;
            }

            int cellCount = ResolveCellCount();
            if (!TryResolveVaultBuffer(_dataVault, in _cellCompartmentIndicesHandle, cellCount, out NativeArray<byte> cellCompartmentIndices) ||
                !TryResolveVaultBuffer(_dataVault, in _cellIntegrityFrontHandle, cellCount, out NativeArray<byte> cellIntegrityFront) ||
                !TryResolveVaultBuffer(_dataVault, in _cellIntegrityBackHandle, cellCount, out NativeArray<byte> cellIntegrityBack) ||
                !TryResolveVaultBuffer(_dataVault, in _cellFatigueHandle, cellCount, out NativeArray<byte> cellFatigue) ||
                !TryResolveVaultBuffer(_dataVault, in _fatigueCompartmentFlagsHandle, CompartmentCapacity, out NativeArray<byte> fatigueFlags) ||
                !TryResolveVaultBuffer(_dataVault, in _fatigueIntegrityLossPerCycleHandle, CompartmentCapacity, out NativeArray<float> fatigueLossPerCycle) ||
                !TryResolveVaultBuffer(_dataVault, in _fatiguePeakResultHandle, 1, out NativeArray<float> peakResult))
            {
                UnlockStructuralJobBuffers(lockMask, fatigueGuardVault);
                return;
            }

            _fatigueJobLockMask = lockMask;
            _fatigueJobMutationGuardVault = fatigueGuardVault;
            bool scheduled = false;
            try
            {
                HullFatigueCompartmentJob fatigueJob = new HullFatigueCompartmentJob
                {
                    CellCompartmentIndices = cellCompartmentIndices,
                    CellIntegrityFront = cellIntegrityFront,
                    CellIntegrityBack = cellIntegrityBack,
                    CellFatigue = cellFatigue,
                    FatigueCompartmentFlags = fatigueFlags,
                    FatigueIntegrityLossPerCycle = fatigueLossPerCycle,
                    PeakNormalized = peakResult,
                    CellCount = cellIntegrityFront.Length
                };
                _fatigueJobHandle = fatigueJob.Schedule();
                _fatigueJobRunning = true;
                H8Memory.RegisterActiveJob(VaultOwnerSystemId, _fatigueJobHandle);
                JobHandle.ScheduleBatchedJobs();
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                {
                    UnlockStructuralJobBuffers(_fatigueJobLockMask, _fatigueJobMutationGuardVault);
                    _fatigueJobLockMask = 0;
                    _fatigueJobMutationGuardVault = null;
                    _fatigueJobHandle = default;
                    _fatigueJobRunning = false;
                }
            }
        }

        private void ConsumeCompletedFatigueJob()
        {
            if (!_fatigueJobRunning)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _fatigueJobHandle))
                return;

            try
            {
                if (TryResolveVaultBuffer(_dataVault, in _fatiguePeakResultHandle, 1, out NativeArray<float> peakResult))
                    _fatiguePeakNormalized = math.max(_fatiguePeakNormalized, peakResult[0]);
            }
            finally
            {
                _fatigueJobRunning = false;
                UnlockStructuralJobBuffers(_fatigueJobLockMask, _fatigueJobMutationGuardVault);
                _fatigueJobLockMask = 0;
                _fatigueJobMutationGuardVault = null;
            }
        }

        private void ScheduleDamageJob()
        {
            if (_damageJobRunning || _damageJobLockMask != 0 || _queuedImpactCount <= 0 || !IsReady)
                return;

            using (_damageScheduleProfilerMarker.Auto())
            {
                int copiedImpactCount = 0;
                if (!TryAcquireStructuralMutationGuard())
                    return;

                try
                {
                    if (!TryResolveStructuralWriteBuffer(in _queuedImpactsHandle, MaxQueuedImpacts, out NativeArray<ImpactCommand> queuedImpacts) ||
                        !TryResolveStructuralWriteBuffer(in _scheduledImpactsHandle, MaxQueuedImpacts, out NativeArray<ImpactCommand> scheduledImpacts))
                    {
                        return;
                    }

                    copiedImpactCount = math.min(_queuedImpactCount, math.min(queuedImpacts.Length, scheduledImpacts.Length));
                    for (int i = 0; i < copiedImpactCount; i++)
                        scheduledImpacts[i] = queuedImpacts[i];
                }
                finally
                {
                    ReleaseStructuralMutationGuard();
                }

                if (copiedImpactCount <= 0)
                    return;

                int lockMask = 0;
                IDataVault damageGuardVault = null;
                if (!TryValidateStructuralJobBuffer(in _cellIntegrityFrontHandle, LockCellIntegrityFront, ref lockMask) ||
                    !TryValidateStructuralJobBuffer(in _cellCompartmentIndicesHandle, LockCellCompartmentIndices, ref lockMask) ||
                    !TryValidateStructuralJobBuffer(in _scheduledImpactsHandle, LockScheduledImpacts, ref lockMask) ||
                    !TryValidateStructuralJobBuffer(in _cellIntegrityBackHandle, LockCellIntegrityBack, ref lockMask) ||
                    !TryValidateStructuralJobBuffer(in _hullBreachMaskBackHandle, LockHullBreachMaskBack, ref lockMask) ||
                    !TryValidateStructuralJobBuffer(in _compartmentBreachAreasBackHandle, LockCompartmentBreachAreasBack, ref lockMask) ||
                    !TryAcquireStructuralJobMutationGuard(ref lockMask, out damageGuardVault))
                {
                    UnlockStructuralJobBuffers(lockMask, damageGuardVault);
                    return;
                }

                int cellCount = ResolveCellCount();
                int breachWordCount = (cellCount + 63) >> 6;
                if (!TryResolveVaultBuffer(_dataVault, in _cellIntegrityFrontHandle, cellCount, out NativeArray<byte> cellIntegrityFront) ||
                    !TryResolveVaultBuffer(_dataVault, in _cellCompartmentIndicesHandle, cellCount, out NativeArray<byte> cellCompartmentIndices) ||
                    !TryResolveVaultBuffer(_dataVault, in _scheduledImpactsHandle, MaxQueuedImpacts, out NativeArray<ImpactCommand> scheduledImpactsForJob) ||
                    !TryResolveVaultBuffer(_dataVault, in _cellIntegrityBackHandle, cellCount, out NativeArray<byte> cellIntegrityBack) ||
                    !TryResolveVaultBuffer(_dataVault, in _hullBreachMaskBackHandle, breachWordCount, out NativeArray<ulong> hullBreachMaskBack) ||
                    !TryResolveVaultBuffer(_dataVault, in _compartmentBreachAreasBackHandle, CompartmentCapacity, out NativeArray<float> compartmentBreachAreasBack))
                {
                    UnlockStructuralJobBuffers(lockMask, damageGuardVault);
                    return;
                }

                _scheduledImpactCount = copiedImpactCount;
                _queuedImpactCount = 0;
                _damageJobLockMask = lockMask;
                _damageJobMutationGuardVault = damageGuardVault;
                bool scheduled = false;
                try
                {
                    HullDamageDiffusionJob damageJob = new HullDamageDiffusionJob
                    {
                        InputIntegrity = cellIntegrityFront,
                        CellCompartmentIndices = cellCompartmentIndices,
                        Impacts = scheduledImpactsForJob,
                        OutputIntegrity = cellIntegrityBack,
                        OutputBreachMaskWords = hullBreachMaskBack,
                        OutputCompartmentBreachAreas = compartmentBreachAreasBack,
                        GridWidth = gridWidth,
                        GridHeight = gridHeight,
                        GridDepth = gridDepth,
                        CellCount = cellIntegrityFront.Length,
                        ImpactCount = _scheduledImpactCount,
                        GridCenterLocal = localGridCenter,
                        GridSizeLocal = localGridSize,
                        CellBreachAreaSquareMeters = _cellBreachAreaSquareMeters
                    };
                    _damageJobHandle = damageJob.Schedule();
                    _damageJobRunning = true;
                    H8Memory.RegisterActiveJob(VaultOwnerSystemId, _damageJobHandle);
                    JobHandle.ScheduleBatchedJobs();
                    scheduled = true;
                }
                finally
                {
                    if (!scheduled)
                    {
                        UnlockStructuralJobBuffers(_damageJobLockMask, _damageJobMutationGuardVault);
                        _damageJobLockMask = 0;
                        _damageJobMutationGuardVault = null;
                        _damageJobHandle = default;
                        _damageJobRunning = false;
                        _scheduledImpactCount = 0;
                    }
                }
            }
        }

        private void ConsumeCompletedDamageJob()
        {
            if (!_damageJobRunning)
                return;

            using (_damageConsumeProfilerMarker.Auto())
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _damageJobHandle))
                    return;

                try
                {
                    VaultGenerationHandle<byte> integrityFront = _cellIntegrityFrontHandle;
                    _cellIntegrityFrontHandle = _cellIntegrityBackHandle;
                    _cellIntegrityBackHandle = integrityFront;

                    VaultGenerationHandle<ulong> breachMaskFront = _hullBreachMaskFrontHandle;
                    _hullBreachMaskFrontHandle = _hullBreachMaskBackHandle;
                    _hullBreachMaskBackHandle = breachMaskFront;

                    VaultGenerationHandle<float> breachAreaFront = _compartmentBreachAreasFrontHandle;
                    _compartmentBreachAreasFrontHandle = _compartmentBreachAreasBackHandle;
                    _compartmentBreachAreasBackHandle = breachAreaFront;
                }
                finally
                {
                    _damageJobRunning = false;
                    _scheduledImpactCount = 0;
                    UnlockStructuralJobBuffers(_damageJobLockMask, _damageJobMutationGuardVault);
                    _damageJobLockMask = 0;
                    _damageJobMutationGuardVault = null;
                }
            }
        }

        private void TryRegister()
        {
            if ((_registered && _registeredLateFrame && _registeredSlowTick) || !Application.isPlaying)
                return;
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
            {
                bool fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
                bool postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
                _registered = fixedRegistered || postFixedRegistered;
            }

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered && !_registeredLateFrame && !_registeredSlowTick)
                return;

            if (_registered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerRuntime = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime)
            {
                _cachedFluidDecals = currentService as IFluidDecalPresentationSink;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault currentVault = currentService as IDataVault;
                if (ReferenceEquals(_dataVault, currentVault))
                    return;

                DisposeNativeStateDeferred();
                _dataVault = currentVault;
                if (_dataVault == null || !isActiveAndEnabled)
                    return;

                EnsureNativeState();
                SeedStructuralState();
                _breachGpuDirty = true;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
            }
        }

        private void TryRegisterDamageReceiver()
        {
            if (_damageReceiverRegistered || _damageEmitter == null)
                return;

            _damageEmitter.RegisterDamageReceiver(this);
            _damageReceiverRegistered = true;
        }

        private void TryUnregisterDamageReceiver()
        {
            if (!_damageReceiverRegistered || _damageEmitter == null)
                return;

            _damageEmitter.UnregisterDamageReceiver(this);
            _damageReceiverRegistered = false;
        }

        private void DisposeNativeStateDeferred()
        {
            CompleteStructuralJobsForTeardown();
            UnlockStructuralJobBuffers(_damageJobLockMask, _damageJobMutationGuardVault);
            UnlockStructuralJobBuffers(_mappingJobLockMask, _mappingJobMutationGuardVault);
            UnlockStructuralJobBuffers(_fatigueJobLockMask, _fatigueJobMutationGuardVault);
            UnlockStructuralJobBuffers(_breachRepairJobLockMask, _breachRepairJobMutationGuardVault);
            _damageJobLockMask = 0;
            _mappingJobLockMask = 0;
            _fatigueJobLockMask = 0;
            _breachRepairJobLockMask = 0;
            _damageJobMutationGuardVault = null;
            _mappingJobMutationGuardVault = null;
            _fatigueJobMutationGuardVault = null;
            _breachRepairJobMutationGuardVault = null;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _cellIntegrityFrontHandle);
                ReleaseVaultHandle(vault, ref _cellIntegrityBackHandle);
                ReleaseVaultHandle(vault, ref _cellFatigueHandle);
                ReleaseVaultHandle(vault, ref _cellCompartmentIndicesHandle);
                ReleaseVaultHandle(vault, ref _hullBreachMaskFrontHandle);
                ReleaseVaultHandle(vault, ref _hullBreachMaskBackHandle);
                ReleaseVaultHandle(vault, ref _compartmentBreachAreasFrontHandle);
                ReleaseVaultHandle(vault, ref _compartmentBreachAreasBackHandle);
                ReleaseVaultHandle(vault, ref _queuedImpactsHandle);
                ReleaseVaultHandle(vault, ref _scheduledImpactsHandle);
                ReleaseVaultHandle(vault, ref _compartmentCentroidsHandle);
                ReleaseVaultHandle(vault, ref _fatigueCompartmentFlagsHandle);
                ReleaseVaultHandle(vault, ref _fatigueIntegrityLossPerCycleHandle);
                ReleaseVaultHandle(vault, ref _fatiguePeakResultHandle);
                ReleaseVaultHandle(vault, ref _breachSeveritySumResultHandle);
                ReleaseVaultHandle(vault, ref _breachesHandle);
                ReleaseVaultHandle(vault, ref _damageControlTelemetryHandle);
            }

            _dataVault = null;
            _damageJobHandle = default;
            _mappingJobHandle = default;
            _fatigueJobHandle = default;
            _breachRepairJobHandle = default;
            _damageJobRunning = false;
            _mappingJobRunning = false;
            _fatigueJobRunning = false;
            _breachRepairJobRunning = false;
            _nativeStateReady = false;
            _recentImpactSeverityNormalized = 0f;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
            _mappedCompartmentCount = 0;
            _pendingMappedCompartmentCount = 0;
            _deferredBreachAddCount = 0;
            _activeBreachCount = 0;
            _visibleBreachCount = 0;
            _activeBreachSeveritySum = 0f;
            _pendingRepairQueued = false;
            _breachGpuDirty = true;
        }

        private void CompleteStructuralJobsForTeardown()
        {
            if (!_damageJobRunning &&
                !_mappingJobRunning &&
                !_fatigueJobRunning &&
                !_breachRepairJobRunning)
            {
                return;
            }

            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                if (_damageJobRunning)
                {
                    DispatcherJobFence.TryComplete(ref _damageJobHandle, forceComplete: true);
                    _damageJobRunning = false;
                }

                if (_mappingJobRunning)
                {
                    DispatcherJobFence.TryComplete(ref _mappingJobHandle, forceComplete: true);
                    _mappingJobRunning = false;
                }

                if (_fatigueJobRunning)
                {
                    DispatcherJobFence.TryComplete(ref _fatigueJobHandle, forceComplete: true);
                    _fatigueJobRunning = false;
                }

                if (_breachRepairJobRunning)
                {
                    DispatcherJobFence.TryComplete(ref _breachRepairJobHandle, forceComplete: true);
                    _breachRepairJobRunning = false;
                }
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void ValidateStructuralGridMemoryLayoutEditor()
        {
            if (UnsafeUtility.SizeOf<VaultGenerationHandle<byte>>() != 16 ||
                UnsafeUtility.SizeOf<ImpactCommand>() != 32 ||
                OffsetOf<ImpactCommand>(nameof(ImpactCommand.LocalPoint)) != 0 ||
                OffsetOf<ImpactCommand>(nameof(ImpactCommand.RadiusMeters)) != 12 ||
                OffsetOf<ImpactCommand>(nameof(ImpactCommand.SigmaMeters)) != 16 ||
                OffsetOf<ImpactCommand>(nameof(ImpactCommand.DamageBytes)) != 20 ||
                UnsafeUtility.SizeOf<SubmarineStructuralTelemetryEntry>() != 64 ||
                OffsetOf<SubmarineStructuralTelemetryEntry>(nameof(SubmarineStructuralTelemetryEntry.FirstBreachLocalSeverity)) != 0 ||
                OffsetOf<SubmarineStructuralTelemetryEntry>(nameof(SubmarineStructuralTelemetryEntry.SeveritySum)) != 16 ||
                OffsetOf<SubmarineStructuralTelemetryEntry>(nameof(SubmarineStructuralTelemetryEntry.Frame)) != 28 ||
                OffsetOf<SubmarineStructuralTelemetryEntry>(nameof(SubmarineStructuralTelemetryEntry.BufferId)) != 40 ||
                OffsetOf<SubmarineStructuralTelemetryEntry>(nameof(SubmarineStructuralTelemetryEntry.ActiveBreachCount)) != 56 ||
                OffsetOf<SubmarineStructuralTelemetryEntry>(nameof(SubmarineStructuralTelemetryEntry.FailureCode)) != 60 ||
                OffsetOf<SubmarineStructuralTelemetryEntry>(nameof(SubmarineStructuralTelemetryEntry.ConsecutiveFailureCount)) != 62)
            {
                throw new System.InvalidOperationException("SubmarineStructuralGrid memory sovereignty validation failed: DTO stride or field offset changed.");
            }
        }

        private static int OffsetOf<T>(string fieldName)
            where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
#endif

        private int ResolveCellCount()
        {
            int safeWidth = math.max(1, gridWidth);
            int safeHeight = math.max(1, gridHeight);
            int safeDepth = math.max(1, gridDepth);
            return safeWidth * safeHeight * safeDepth;
        }

        private float ResolveCellBreachAreaSquareMeters()
        {
            float safeWidth = math.max(1, gridWidth);
            float safeHeight = math.max(1, gridHeight);
            float safeDepth = math.max(1, gridDepth);
            float cellSizeX = math.max(localGridSize.x / safeWidth, Epsilon);
            float cellSizeY = math.max(localGridSize.y / safeHeight, Epsilon);
            float cellSizeZ = math.max(localGridSize.z / safeDepth, Epsilon);
            float areaXY = cellSizeX * cellSizeY;
            float areaXZ = cellSizeX * cellSizeZ;
            float areaYZ = cellSizeY * cellSizeZ;
            return math.min(areaXY, math.min(areaXZ, areaYZ));
        }

    }
}
