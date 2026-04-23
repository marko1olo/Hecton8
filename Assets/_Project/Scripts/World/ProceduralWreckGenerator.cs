using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Runtime contract for deterministic procedural geometry generators.
    /// </summary>
    public interface IProceduralGenerator : IDisposable
    {
        /// <summary>
        /// Builds a deterministic wreck payload for the requested chunk-space anchor.
        /// </summary>
        /// <param name="chunkAup">Chunk anchor in absolute-universe chunk space.</param>
        /// <param name="seed">Stable seed used to resolve WFC collapse order and module selection.</param>
        /// <returns>Generated wreck payload.</returns>
        WreckageData Generate(int3 chunkAup, uint seed);
    }

    /// <summary>
    /// Declares the merged render tier for a procedural wreck module.
    /// </summary>
    public enum WreckLodTier : byte
    {
        Essential = 0,
        Detail = 1,
        Clutter = 2
    }

    /// <summary>
    /// Serializable authoring record for a single WFC wreck module.
    /// </summary>
    [Serializable]
    public struct ProceduralWreckModuleDefinition
    {
        [Tooltip("North face socket mask.")]
        public ushort NorthSocket;

        [Tooltip("East face socket mask.")]
        public ushort EastSocket;

        [Tooltip("South face socket mask.")]
        public ushort SouthSocket;

        [Tooltip("West face socket mask.")]
        public ushort WestSocket;

        [Tooltip("Top face socket mask.")]
        public ushort TopSocket;

        [Tooltip("Bottom face socket mask.")]
        public ushort BottomSocket;

        [Tooltip("Structural mesh used by the merged render pass. Mesh layout must expose tightly packed Vector3 position, Vector3 normal, and Vector2 UV streams for direct job access.")]
        public Mesh StructuralMesh;

        [Tooltip("Local axis-aligned bounds used for deterministic proxy hull generation and authored occupancy checks.")]
        public Bounds LocalBounds;

        [Tooltip("Render priority used to split the merged result into Essential/Detail/Clutter tiers.")]
        public WreckLodTier DrawCallPriority;

        [Tooltip("True when this module writes geometry into the merged structural mesh.")]
        public bool EmitsGeometry;

        [Tooltip("True when this module writes a lightweight nav proxy hull derived from LocalBounds.")]
        public bool EmitsNavProxy;

        [Tooltip("True when this module acts as the universal contradiction fallback. Slot 0 is forced to behave this way.")]
        public bool UniversalConnector;
    }

    /// <summary>
    /// Runtime output for a generated procedural wreck.
    /// </summary>
    [Serializable]
    public struct WreckageData
    {
        public Mesh CombinedMesh;
        public Mesh EssentialMesh;
        public Mesh DetailMesh;
        public Mesh ClutterMesh;
        public Mesh ProxyMesh;
        public NavMeshData Navigation;
        public AsyncOperation NavigationBuild;
        public Bounds WorldBounds;
        public Vector3 RuntimeOrigin;
        public uint GenerationSeed;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    internal struct WreckGridCell
    {
        public ushort PossibleModuleMask;
        public byte CollapsedModuleId;
        public byte SocketConstraints;
        public float Entropy;
        private uint _reserved0;
        private uint _reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WreckModuleRuntimeDefinition
    {
        public ushort NorthSocket;
        public ushort EastSocket;
        public ushort SouthSocket;
        public ushort WestSocket;
        public ushort TopSocket;
        public ushort BottomSocket;
        public float3 BoundsCenter;
        public float3 BoundsSize;
        public byte DrawCallPriority;
        public byte EmitsGeometry;
        public byte EmitsNavProxy;
        public byte UniversalConnector;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WreckModulePlacement
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 BoundsCenter;
        public float3 BoundsSize;
        public int MortonIndex;
        public byte ModuleId;
        public byte DrawPriority;
        private ushort _reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WreckMergedVertex
    {
        public float3 Position;
        public float3 Normal;
        public float2 UV;
    }

    [BurstCompile]
    internal struct XorShift128State
    {
        public uint State0;
        public uint State1;
        public uint State2;
        public uint State3;

        public XorShift128State(uint seed)
        {
            uint sanitized = seed == 0u ? 0x6E624EB7u : seed;
            State0 = sanitized;
            State1 = sanitized ^ 0x9E3779B9u;
            State2 = sanitized << 13;
            State3 = (sanitized >> 17) ^ 0x85EBCA6Bu;
        }

        public uint NextUInt()
        {
            uint t = State0 ^ (State0 << 11);
            State0 = State1;
            State1 = State2;
            State2 = State3;
            State3 = State3 ^ (State3 >> 19) ^ (t ^ (t >> 8));
            return State3;
        }

        public float NextFloat01()
        {
            return NextUInt() * 2.3283064365386963e-10f;
        }
    }

    [BurstCompile]
    internal struct CopyModuleMeshJob : IJob
    {
        [ReadOnly] public Mesh.MeshData SourceMeshData;
        [NativeDisableParallelForRestriction] public NativeArray<WreckMergedVertex> DestinationVertices;
        [NativeDisableParallelForRestriction] public NativeArray<uint> DestinationIndices;
        public int VertexOffset;
        public int IndexOffset;
        public int PositionStream;
        public int NormalStream;
        public int UvStream;
        public int SubMeshCount;
        public bool HasNormals;
        public bool HasUvs;
        public float4x4 LocalToWreck;
        public quaternion Rotation;

        public void Execute()
        {
            NativeArray<Vector3> sourcePositions = SourceMeshData.GetVertexData<Vector3>(PositionStream);
            NativeArray<Vector3> sourceNormals = HasNormals
                ? SourceMeshData.GetVertexData<Vector3>(NormalStream)
                : default;
            NativeArray<Vector2> sourceUvs = HasUvs
                ? SourceMeshData.GetVertexData<Vector2>(UvStream)
                : default;

            int vertexCount = SourceMeshData.vertexCount;
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                Vector3 sourcePosition = sourcePositions[vertexIndex];
                float3 transformedPosition = math.transform(LocalToWreck, new float3(sourcePosition.x, sourcePosition.y, sourcePosition.z));

                float3 transformedNormal = new float3(0f, 1f, 0f);
                if (HasNormals)
                {
                    Vector3 sourceNormal = sourceNormals[vertexIndex];
                    transformedNormal = math.normalize(math.rotate(Rotation, new float3(sourceNormal.x, sourceNormal.y, sourceNormal.z)));
                }

                float2 uv = float2.zero;
                if (HasUvs)
                {
                    Vector2 sourceUv = sourceUvs[vertexIndex];
                    uv = new float2(sourceUv.x, sourceUv.y);
                }

                DestinationVertices[VertexOffset + vertexIndex] = new WreckMergedVertex
                {
                    Position = transformedPosition,
                    Normal = transformedNormal,
                    UV = uv
                };
            }

            int writeIndex = IndexOffset;
            if (SourceMeshData.indexFormat == IndexFormat.UInt32)
            {
                NativeArray<uint> sourceIndices = SourceMeshData.GetIndexData<uint>();
                for (int subMeshIndex = 0; subMeshIndex < SubMeshCount; subMeshIndex++)
                {
                    SubMeshDescriptor descriptor = SourceMeshData.GetSubMesh(subMeshIndex);
                    int descriptorEnd = descriptor.indexStart + descriptor.indexCount;
                    for (int sourceIndex = descriptor.indexStart; sourceIndex < descriptorEnd; sourceIndex++)
                        DestinationIndices[writeIndex++] = sourceIndices[sourceIndex] + (uint)VertexOffset;
                }
            }
            else
            {
                NativeArray<ushort> sourceIndices = SourceMeshData.GetIndexData<ushort>();
                for (int subMeshIndex = 0; subMeshIndex < SubMeshCount; subMeshIndex++)
                {
                    SubMeshDescriptor descriptor = SourceMeshData.GetSubMesh(subMeshIndex);
                    int descriptorEnd = descriptor.indexStart + descriptor.indexCount;
                    for (int sourceIndex = descriptor.indexStart; sourceIndex < descriptorEnd; sourceIndex++)
                        DestinationIndices[writeIndex++] = (uint)(sourceIndices[sourceIndex] + VertexOffset);
                }
            }
        }
    }

    [BurstCompile]
    internal struct BuildProxyMeshJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<WreckModulePlacement> Placements;
        [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
        [NativeDisableParallelForRestriction] public NativeArray<uint> Indices;

        public void Execute(int index)
        {
            WreckModulePlacement placement = Placements[index];
            float3 halfExtents = placement.BoundsSize * 0.5f;
            float3 localCenter = placement.Position + placement.BoundsCenter;

            int vertexBase = index * 8;
            int indexBase = index * 36;

            float3 p0 = math.rotate(placement.Rotation, new float3(-halfExtents.x, -halfExtents.y, -halfExtents.z)) + localCenter;
            float3 p1 = math.rotate(placement.Rotation, new float3(halfExtents.x, -halfExtents.y, -halfExtents.z)) + localCenter;
            float3 p2 = math.rotate(placement.Rotation, new float3(halfExtents.x, halfExtents.y, -halfExtents.z)) + localCenter;
            float3 p3 = math.rotate(placement.Rotation, new float3(-halfExtents.x, halfExtents.y, -halfExtents.z)) + localCenter;
            float3 p4 = math.rotate(placement.Rotation, new float3(-halfExtents.x, -halfExtents.y, halfExtents.z)) + localCenter;
            float3 p5 = math.rotate(placement.Rotation, new float3(halfExtents.x, -halfExtents.y, halfExtents.z)) + localCenter;
            float3 p6 = math.rotate(placement.Rotation, new float3(halfExtents.x, halfExtents.y, halfExtents.z)) + localCenter;
            float3 p7 = math.rotate(placement.Rotation, new float3(-halfExtents.x, halfExtents.y, halfExtents.z)) + localCenter;

            Positions[vertexBase + 0] = p0;
            Positions[vertexBase + 1] = p1;
            Positions[vertexBase + 2] = p2;
            Positions[vertexBase + 3] = p3;
            Positions[vertexBase + 4] = p4;
            Positions[vertexBase + 5] = p5;
            Positions[vertexBase + 6] = p6;
            Positions[vertexBase + 7] = p7;

            WriteQuad(Indices, indexBase + 0, vertexBase, 0, 2, 1, 3);
            WriteQuad(Indices, indexBase + 6, vertexBase, 4, 5, 6, 7);
            WriteQuad(Indices, indexBase + 12, vertexBase, 0, 1, 5, 4);
            WriteQuad(Indices, indexBase + 18, vertexBase, 1, 2, 6, 5);
            WriteQuad(Indices, indexBase + 24, vertexBase, 2, 3, 7, 6);
            WriteQuad(Indices, indexBase + 30, vertexBase, 3, 0, 4, 7);
        }

        private static void WriteQuad(NativeArray<uint> indices, int targetIndex, int vertexBase, int a, int b, int c, int d)
        {
            indices[targetIndex + 0] = (uint)(vertexBase + a);
            indices[targetIndex + 1] = (uint)(vertexBase + b);
            indices[targetIndex + 2] = (uint)(vertexBase + c);
            indices[targetIndex + 3] = (uint)(vertexBase + a);
            indices[targetIndex + 4] = (uint)(vertexBase + c);
            indices[targetIndex + 5] = (uint)(vertexBase + d);
        }
    }

    /// <summary>
    /// Deterministic wave-function-collapse wreck generator operating in absolute-universe space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralWreckGenerator : MonoBehaviour, IProceduralGenerator
    {
        private const int MaxModuleDefinitions = 16;
        private const byte UncollapsedModuleId = byte.MaxValue;
        private const byte DirectionNorth = 0;
        private const byte DirectionEast = 1;
        private const byte DirectionSouth = 2;
        private const byte DirectionWest = 3;
        private const byte DirectionTop = 4;
        private const byte DirectionBottom = 5;
        private const int ProxyVerticesPerPlacement = 8;
        private const int ProxyIndicesPerPlacement = 36;
        private const uint FallbackSectionSalt = 0xA511E9B3u;

        [Header("WFC")]
        [SerializeField, Range(4, 32)]
        [Tooltip("Power-of-two grid resolution used by the wreck WFC kernel. Stored in Morton order for coherent neighbor traversal.")]
        private int gridResolution = 16;

        [SerializeField, Min(1f)]
        [Tooltip("Edge length for each WFC cell in meters.")]
        private float cellSizeMeters = 6f;

        [SerializeField, Min(1)]
        [Tooltip("Hard ceiling for generated structural placements. Capacity is pre-allocated once and generation truncates safely if exceeded.")]
        private int maxPlacements = 256;

        [SerializeField]
        [Tooltip("Additional deterministic salt mixed into the AUP hash to separate generation revisions.")]
        private uint worldGenerationVersionSalt = 1u;

        [Header("Navigation")]
        [SerializeField]
        [Tooltip("True when a lightweight proxy mesh should be passed to Unity NavMeshBuilder.UpdateNavMeshDataAsync after mesh merge completes.")]
        private bool buildAsyncNavMesh = true;

        [SerializeField, Min(0.1f)]
        [Tooltip("Slim diver radius used for async NavMesh baking.")]
        private float navAgentRadius = 0.3f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Agent height used for async NavMesh baking.")]
        private float navAgentHeight = 1.8f;

        [SerializeField, Range(0f, 60f)]
        [Tooltip("Maximum climbable slope used by async NavMesh baking.")]
        private float navAgentSlope = 45f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum climbable ledge height used by async NavMesh baking.")]
        private float navAgentClimb = 0.4f;

        [Header("Modules")]
        [SerializeField]
        [Tooltip("Authoring definitions for up to 16 WFC modules. Element 0 is reserved as the contradiction-safe universal connector.")]
        private ProceduralWreckModuleDefinition[] moduleDefinitions = Array.Empty<ProceduralWreckModuleDefinition>();

        [Header("Diagnostics")]
        [SerializeField] private uint _debugLastSeed;
        [SerializeField] private int _debugLastPlacementCount;
        [SerializeField] private int _debugLastCellCount;

        private NativeArray<WreckGridCell> _grid;
        private NativeArray<int> _propagationQueue;
        private NativeList<WreckModulePlacement> _allPlacements;
        private NativeList<WreckModulePlacement> _filteredPlacements;
        private NativeArray<WreckModuleRuntimeDefinition> _runtimeDefinitions;
        private List<NavMeshBuildSource> _navMeshSources;
        private Mesh.MeshDataArray[] _readOnlyMeshSnapshots;
        private JobHandle[] _copyHandles;
        private bool _initialized;

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnValidate()
        {
            gridResolution = ClampPowerOfTwo(gridResolution, 4, 32);
            cellSizeMeters = Mathf.Max(1f, cellSizeMeters);
            maxPlacements = Mathf.Max(1, maxPlacements);
            navAgentRadius = Mathf.Max(0.1f, navAgentRadius);
            navAgentHeight = Mathf.Max(0.5f, navAgentHeight);
            navAgentSlope = Mathf.Clamp(navAgentSlope, 0f, 60f);
            navAgentClimb = Mathf.Clamp(navAgentClimb, 0f, 1f);
        }

        /// <summary>
        /// Computes the deterministic AUP-derived seed used by the wreck WFC kernel.
        /// </summary>
        /// <param name="runtimePosition">Runtime position resolved through the floating-origin bridge.</param>
        /// <param name="salt">Optional additional salt.</param>
        /// <returns>Bit-safe deterministic seed.</returns>
        public static uint ComputeGenerationSeed(Vector3 runtimePosition, uint salt = 0u)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return ComputeGenerationSeed(in aup, salt);
        }

        /// <summary>
        /// Generates a wreck around the requested chunk-space anchor.
        /// </summary>
        /// <param name="chunkAup">Absolute-universe chunk anchor.</param>
        /// <param name="seed">Stable caller-provided seed.</param>
        /// <returns>Generated wreck payload.</returns>
        public WreckageData Generate(int3 chunkAup, uint seed)
        {
            Initialize();

            double spanMeters = gridResolution * cellSizeMeters;
            double3 absoluteOrigin = new double3(
                chunkAup.x * spanMeters,
                chunkAup.y * spanMeters,
                chunkAup.z * spanMeters);

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteOrigin);
            float3 runtimeOriginFloat = aup.ToRuntimeFloat3();
            Vector3 runtimeOrigin = new Vector3(runtimeOriginFloat.x, runtimeOriginFloat.y, runtimeOriginFloat.z);
            uint resolvedSeed = seed != 0u
                ? seed
                : ComputeGenerationSeed(in aup, worldGenerationVersionSalt ^ FallbackSectionSalt);

            return GenerateInternal(runtimeOrigin, resolvedSeed);
        }

        /// <summary>
        /// Generates a wreck payload directly from the existing mega-wreck streaming section contract.
        /// </summary>
        /// <param name="section">Published mega-wreck section payload.</param>
        /// <returns>Generated wreck payload.</returns>
        public WreckageData Generate(in HectonMapMagicVegetationBridge.MegaWreckStreamSection section)
        {
            Initialize();
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(section.WorldCenter);
            uint resolvedSeed = ComputeGenerationSeed(in aup, (uint)section.SectionSeed ^ worldGenerationVersionSalt);
            return GenerateInternal(section.WorldCenter, resolvedSeed);
        }

        /// <summary>
        /// Releases all persistent native state owned by this generator.
        /// </summary>
        public void Dispose()
        {
            if (_grid.IsCreated)
                _grid.Dispose();

            if (_propagationQueue.IsCreated)
                _propagationQueue.Dispose();

            if (_allPlacements.IsCreated)
                _allPlacements.Dispose();

            if (_filteredPlacements.IsCreated)
                _filteredPlacements.Dispose();

            if (_runtimeDefinitions.IsCreated)
                _runtimeDefinitions.Dispose();

            _navMeshSources?.Clear();
            _initialized = false;
        }

        private static uint ComputeGenerationSeed(in AbsoluteUniversePosition aup, uint salt)
        {
            double3 absolutePosition = aup.ToAbsoluteDouble3();
            float3 absoluteHashVector = new float3(
                (float)absolutePosition.x,
                (float)absolutePosition.y,
                (float)absolutePosition.z);

            uint hash =
                (math.asuint(absoluteHashVector.x) * 73856093u) ^
                (math.asuint(absoluteHashVector.y) * 19349663u) ^
                (math.asuint(absoluteHashVector.z) * 83492791u);

            return (hash ^ (hash >> 16)) ^ salt;
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            gridResolution = ClampPowerOfTwo(gridResolution, 4, 32);
            int maxCellCount = gridResolution * gridResolution * gridResolution;

            // COLD ALLOC: NativeArray<WreckGridCell>[maxCellCount] - Morton-ordered WFC state grid - owner: ProceduralWreckGenerator
            _grid = new NativeArray<WreckGridCell>(maxCellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<int>[maxCellCount * 6] - deterministic propagation queue scratch - owner: ProceduralWreckGenerator
            _propagationQueue = new NativeArray<int>(maxCellCount * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeList<WreckModulePlacement>[maxPlacements] - merged structural placement list - owner: ProceduralWreckGenerator
            _allPlacements = new NativeList<WreckModulePlacement>(maxPlacements, Allocator.Persistent);
            // COLD ALLOC: NativeList<WreckModulePlacement>[maxPlacements] - per-tier mesh merge filter scratch - owner: ProceduralWreckGenerator
            _filteredPlacements = new NativeList<WreckModulePlacement>(maxPlacements, Allocator.Persistent);
            // COLD ALLOC: NativeArray<WreckModuleRuntimeDefinition>[16] - native WFC module table - owner: ProceduralWreckGenerator
            _runtimeDefinitions = new NativeArray<WreckModuleRuntimeDefinition>(MaxModuleDefinitions, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: List<NavMeshBuildSource>[8] - reusable async NavMesh source list - owner: ProceduralWreckGenerator
            _navMeshSources = new List<NavMeshBuildSource>(8);
            // COLD ALLOC: Mesh.MeshDataArray[maxPlacements] - scheduled read-only source snapshots - owner: ProceduralWreckGenerator
            _readOnlyMeshSnapshots = new Mesh.MeshDataArray[maxPlacements];
            // COLD ALLOC: JobHandle[maxPlacements] - scheduled mesh copy handles - owner: ProceduralWreckGenerator
            _copyHandles = new JobHandle[maxPlacements];

            RefreshRuntimeDefinitions();
            _initialized = true;
        }

        private WreckageData GenerateInternal(Vector3 runtimeOrigin, uint seed)
        {
            RefreshRuntimeDefinitions();

            int moduleCount = ResolveRuntimeModuleCount();
            if (moduleCount <= 0)
                return default;

            int cellCount = gridResolution * gridResolution * gridResolution;
            ushort initialMask = ResolveInitialMask(moduleCount);
            InitializeGrid(cellCount, initialMask);

            XorShift128State rng = new XorShift128State(seed);
            CollapseGrid(cellCount, moduleCount, ref rng);
            BuildPlacements(cellCount);

            Mesh combinedMesh = BuildMergedMesh(_allPlacements);
            Mesh essentialMesh = BuildMergedMeshForTier((byte)WreckLodTier.Essential);
            Mesh detailMesh = BuildMergedMeshForTier((byte)WreckLodTier.Detail);
            Mesh clutterMesh = BuildMergedMeshForTier((byte)WreckLodTier.Clutter);
            Mesh proxyMesh = BuildProxyMesh();

            Bounds localBounds = CalculateLocalBounds(_allPlacements);
            Bounds worldBounds = TranslateBounds(localBounds, runtimeOrigin);

            NavMeshData navMeshData = null;
            AsyncOperation navMeshOperation = null;
            if (buildAsyncNavMesh && proxyMesh != null)
                BuildAsyncNavMesh(runtimeOrigin, worldBounds, proxyMesh, out navMeshData, out navMeshOperation);

            _debugLastSeed = seed;
            _debugLastPlacementCount = _allPlacements.Length;
            _debugLastCellCount = cellCount;

            return new WreckageData
            {
                CombinedMesh = combinedMesh,
                EssentialMesh = essentialMesh,
                DetailMesh = detailMesh,
                ClutterMesh = clutterMesh,
                ProxyMesh = proxyMesh,
                Navigation = navMeshData,
                NavigationBuild = navMeshOperation,
                WorldBounds = worldBounds,
                RuntimeOrigin = runtimeOrigin,
                GenerationSeed = seed
            };
        }

        private void RefreshRuntimeDefinitions()
        {
            for (int i = 0; i < MaxModuleDefinitions; i++)
                _runtimeDefinitions[i] = default;

            int count = math.min(moduleDefinitions != null ? moduleDefinitions.Length : 0, MaxModuleDefinitions);
            for (int i = 0; i < count; i++)
            {
                ProceduralWreckModuleDefinition source = moduleDefinitions[i];
                bool isFallbackConnector = i == 0 || source.UniversalConnector;
                ushort universalMask = 0xFFFF;

                _runtimeDefinitions[i] = new WreckModuleRuntimeDefinition
                {
                    NorthSocket = isFallbackConnector ? universalMask : source.NorthSocket,
                    EastSocket = isFallbackConnector ? universalMask : source.EastSocket,
                    SouthSocket = isFallbackConnector ? universalMask : source.SouthSocket,
                    WestSocket = isFallbackConnector ? universalMask : source.WestSocket,
                    TopSocket = isFallbackConnector ? universalMask : source.TopSocket,
                    BottomSocket = isFallbackConnector ? universalMask : source.BottomSocket,
                    BoundsCenter = new float3(source.LocalBounds.center.x, source.LocalBounds.center.y, source.LocalBounds.center.z),
                    BoundsSize = new float3(source.LocalBounds.size.x, source.LocalBounds.size.y, source.LocalBounds.size.z),
                    DrawCallPriority = (byte)math.clamp((int)source.DrawCallPriority, 0, 2),
                    EmitsGeometry = (byte)((source.EmitsGeometry && source.StructuralMesh != null) ? 1 : 0),
                    EmitsNavProxy = (byte)(source.EmitsNavProxy ? 1 : 0),
                    UniversalConnector = (byte)(isFallbackConnector ? 1 : 0)
                };
            }
        }

        private int ResolveRuntimeModuleCount()
        {
            if (moduleDefinitions == null || moduleDefinitions.Length == 0)
                return 0;

            return math.min(moduleDefinitions.Length, MaxModuleDefinitions);
        }

        private ushort ResolveInitialMask(int moduleCount)
        {
            if (moduleCount >= MaxModuleDefinitions)
                return 0xFFFF;

            return (ushort)((1 << moduleCount) - 1);
        }

        private void InitializeGrid(int cellCount, ushort initialMask)
        {
            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                byte constraints = ResolveBoundaryConstraintMask(mortonIndex);
                ushort constrainedMask = ApplyBoundaryConstraints(initialMask, constraints);
                if (constrainedMask == 0)
                    constrainedMask = 1;

                _grid[mortonIndex] = new WreckGridCell
                {
                    PossibleModuleMask = constrainedMask,
                    CollapsedModuleId = UncollapsedModuleId,
                    SocketConstraints = constraints,
                    Entropy = ComputeEntropy(constrainedMask)
                };
            }
        }

        private void CollapseGrid(int cellCount, int moduleCount, ref XorShift128State rng)
        {
            int collapsedCount = 0;
            while (collapsedCount < cellCount)
            {
                int selectedIndex = SelectNextCell(cellCount, ref rng);
                if (selectedIndex < 0)
                    break;

                WreckGridCell selectedCell = _grid[selectedIndex];
                byte chosenModule = SelectModuleFromMask(selectedCell.PossibleModuleMask, moduleCount, ref rng);
                selectedCell.CollapsedModuleId = chosenModule;
                selectedCell.PossibleModuleMask = (ushort)(1 << chosenModule);
                selectedCell.Entropy = 0f;
                _grid[selectedIndex] = selectedCell;
                collapsedCount++;

                int queueCount = 0;
                _propagationQueue[queueCount++] = selectedIndex;
                PropagateConstraints(moduleCount, ref queueCount);
            }
        }

        private int SelectNextCell(int cellCount, ref XorShift128State rng)
        {
            float bestEntropy = float.MaxValue;
            int bestIndex = -1;

            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                WreckGridCell cell = _grid[mortonIndex];
                if (cell.CollapsedModuleId != UncollapsedModuleId)
                    continue;

                float candidateEntropy = cell.Entropy + ComputeEntropyNoise(mortonIndex, ref rng);
                if (candidateEntropy < bestEntropy)
                {
                    bestEntropy = candidateEntropy;
                    bestIndex = mortonIndex;
                }
            }

            return bestIndex;
        }

        private void PropagateConstraints(int moduleCount, ref int queueCount)
        {
            int queueReadIndex = 0;
            while (queueReadIndex < queueCount)
            {
                int currentIndex = _propagationQueue[queueReadIndex++];
                int3 currentCoord = DecodeMorton3(currentIndex);
                WreckGridCell currentCell = _grid[currentIndex];

                for (byte direction = 0; direction < 6; direction++)
                {
                    if (!TryGetNeighbor(currentCoord, direction, out int neighborIndex))
                        continue;

                    WreckGridCell neighborCell = _grid[neighborIndex];
                    if (neighborCell.CollapsedModuleId != UncollapsedModuleId)
                        continue;

                    ushort compatibleMask = ComputeCompatibleNeighborMask(
                        currentCell.PossibleModuleMask,
                        neighborCell.PossibleModuleMask,
                        direction,
                        moduleCount);

                    compatibleMask = ApplyBoundaryConstraints(compatibleMask, neighborCell.SocketConstraints);
                    if (compatibleMask == 0)
                        compatibleMask = 1;

                    if (compatibleMask == neighborCell.PossibleModuleMask)
                        continue;

                    neighborCell.PossibleModuleMask = compatibleMask;
                    neighborCell.Entropy = ComputeEntropy(compatibleMask);
                    _grid[neighborIndex] = neighborCell;

                    if (queueCount < _propagationQueue.Length)
                        _propagationQueue[queueCount++] = neighborIndex;
                }
            }
        }

        private void BuildPlacements(int cellCount)
        {
            _allPlacements.Clear();

            float halfSpan = (gridResolution * cellSizeMeters) * 0.5f;
            float halfCell = cellSizeMeters * 0.5f;

            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                WreckGridCell cell = _grid[mortonIndex];
                int moduleId = cell.CollapsedModuleId;
                if (moduleId < 0 || moduleId >= moduleDefinitions.Length)
                    continue;

                WreckModuleRuntimeDefinition runtimeDefinition = _runtimeDefinitions[moduleId];
                if (runtimeDefinition.EmitsGeometry == 0 && runtimeDefinition.EmitsNavProxy == 0)
                    continue;

                if (_allPlacements.Length >= maxPlacements)
                    break;

                int3 coord = DecodeMorton3(mortonIndex);
                float3 localPosition = new float3(
                    (-halfSpan + halfCell) + (coord.x * cellSizeMeters),
                    (-halfSpan + halfCell) + (coord.y * cellSizeMeters),
                    (-halfSpan + halfCell) + (coord.z * cellSizeMeters));

                _allPlacements.Add(new WreckModulePlacement
                {
                    Position = localPosition,
                    Rotation = quaternion.identity,
                    BoundsCenter = runtimeDefinition.BoundsCenter,
                    BoundsSize = runtimeDefinition.BoundsSize,
                    MortonIndex = mortonIndex,
                    ModuleId = (byte)moduleId,
                    DrawPriority = runtimeDefinition.DrawCallPriority
                });
            }
        }

        private Mesh BuildMergedMeshForTier(byte tier)
        {
            _filteredPlacements.Clear();
            int placementCount = _allPlacements.Length;
            for (int i = 0; i < placementCount; i++)
            {
                WreckModulePlacement placement = _allPlacements[i];
                if (placement.DrawPriority == tier)
                    _filteredPlacements.Add(placement);
            }

            return BuildMergedMesh(_filteredPlacements);
        }

        private Mesh BuildMergedMesh(NativeList<WreckModulePlacement> placements)
        {
            if (!placements.IsCreated || placements.Length <= 0)
                return null;

            int placementCount = placements.Length;
            int totalVertexCount = 0;
            int totalIndexCount = 0;
            int scheduledJobCount = 0;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                WreckModulePlacement placement = placements[placementIndex];
                Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                if (sourceMesh == null || !ValidateMeshLayout(sourceMesh))
                    continue;

                totalVertexCount += sourceMesh.vertexCount;
                totalIndexCount += ResolveIndexCount(sourceMesh);
                scheduledJobCount++;
            }

            if (totalVertexCount <= 0 || totalIndexCount <= 0 || scheduledJobCount <= 0)
                return null;

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = writableMeshData[0];
            meshData.SetVertexBufferParams(
                totalVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

            NativeArray<WreckMergedVertex> destinationVertices = meshData.GetVertexData<WreckMergedVertex>();
            NativeArray<uint> destinationIndices = meshData.GetIndexData<uint>();

            int vertexOffset = 0;
            int indexOffset = 0;
            JobHandle dependency = default;
            scheduledJobCount = 0;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                WreckModulePlacement placement = placements[placementIndex];
                Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                if (sourceMesh == null || !ValidateMeshLayout(sourceMesh))
                    continue;

                _readOnlyMeshSnapshots[scheduledJobCount] = Mesh.AcquireReadOnlyMeshData(sourceMesh);
                Mesh.MeshData sourceData = _readOnlyMeshSnapshots[scheduledJobCount][0];

                int subMeshCount = sourceData.subMeshCount;
                CopyModuleMeshJob job = new CopyModuleMeshJob
                {
                    SourceMeshData = sourceData,
                    DestinationVertices = destinationVertices,
                    DestinationIndices = destinationIndices,
                    VertexOffset = vertexOffset,
                    IndexOffset = indexOffset,
                    PositionStream = sourceData.GetVertexAttributeStream(VertexAttribute.Position),
                    NormalStream = sourceData.HasVertexAttribute(VertexAttribute.Normal)
                        ? sourceData.GetVertexAttributeStream(VertexAttribute.Normal)
                        : -1,
                    UvStream = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0)
                        ? sourceData.GetVertexAttributeStream(VertexAttribute.TexCoord0)
                        : -1,
                    SubMeshCount = subMeshCount,
                    HasNormals = sourceData.HasVertexAttribute(VertexAttribute.Normal),
                    HasUvs = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0),
                    LocalToWreck = float4x4.TRS(placement.Position, placement.Rotation, new float3(1f)),
                    Rotation = placement.Rotation
                };

                dependency = job.Schedule(dependency);
                _copyHandles[scheduledJobCount] = dependency;
                scheduledJobCount++;

                vertexOffset += sourceMesh.vertexCount;
                indexOffset += ResolveIndexCount(sourceMesh);
            }

            JobHandle.ScheduleBatchedJobs();
            dependency.Complete();

            for (int i = 0; i < scheduledJobCount; i++)
                _readOnlyMeshSnapshots[i].Dispose();

            Bounds localBounds = CalculateLocalBounds(placements);
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles)
            {
                bounds = localBounds,
                vertexCount = totalVertexCount
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);

            Mesh result = new Mesh
            {
                name = $"{nameof(ProceduralWreckGenerator)}_{scheduledJobCount}"
            };
            Mesh.ApplyAndDisposeWritableMeshData(
                writableMeshData,
                result,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
            result.bounds = localBounds;
            return result;
        }

        private Mesh BuildProxyMesh()
        {
            _filteredPlacements.Clear();
            int placementCount = _allPlacements.Length;
            for (int i = 0; i < placementCount; i++)
            {
                WreckModulePlacement placement = _allPlacements[i];
                if (_runtimeDefinitions[placement.ModuleId].EmitsNavProxy != 0)
                    _filteredPlacements.Add(placement);
            }

            if (_filteredPlacements.Length <= 0)
                return null;

            int proxyPlacementCount = _filteredPlacements.Length;
            int totalVertexCount = proxyPlacementCount * ProxyVerticesPerPlacement;
            int totalIndexCount = proxyPlacementCount * ProxyIndicesPerPlacement;

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = writableMeshData[0];
            meshData.SetVertexBufferParams(
                totalVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

            NativeArray<float3> positions = meshData.GetVertexData<float3>();
            NativeArray<uint> indices = meshData.GetIndexData<uint>();

            BuildProxyMeshJob job = new BuildProxyMeshJob
            {
                Placements = _filteredPlacements.AsArray(),
                Positions = positions,
                Indices = indices
            };

            JobHandle handle = job.Schedule(proxyPlacementCount, 32);
            JobHandle.ScheduleBatchedJobs();
            handle.Complete();

            Bounds localBounds = CalculateLocalBounds(_filteredPlacements);
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles)
            {
                bounds = localBounds,
                vertexCount = totalVertexCount
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);

            Mesh result = new Mesh
            {
                name = $"{nameof(ProceduralWreckGenerator)}_Proxy"
            };
            Mesh.ApplyAndDisposeWritableMeshData(
                writableMeshData,
                result,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
            result.bounds = localBounds;
            return result;
        }

        private void BuildAsyncNavMesh(
            Vector3 runtimeOrigin,
            Bounds worldBounds,
            Mesh proxyMesh,
            out NavMeshData navMeshData,
            out AsyncOperation navMeshOperation)
        {
            navMeshData = null;
            navMeshOperation = null;

            if (proxyMesh == null || NavMesh.GetSettingsCount() <= 0)
                return;

            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = navAgentRadius;
            settings.agentHeight = navAgentHeight;
            settings.agentSlope = navAgentSlope;
            settings.agentClimb = navAgentClimb;

            _navMeshSources.Clear();
            _navMeshSources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = proxyMesh,
                transform = Matrix4x4.TRS(runtimeOrigin, Quaternion.identity, Vector3.one),
                area = 0
            });

            navMeshData = new NavMeshData();
            navMeshOperation = NavMeshBuilder.UpdateNavMeshDataAsync(navMeshData, settings, _navMeshSources, worldBounds);
        }

        private Mesh ResolveStructuralMesh(int moduleId)
        {
            if (moduleDefinitions == null || moduleId < 0 || moduleId >= moduleDefinitions.Length)
                return null;

            return moduleDefinitions[moduleId].StructuralMesh;
        }

        private bool ValidateMeshLayout(Mesh sourceMesh)
        {
            using Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(sourceMesh);
            Mesh.MeshData sourceData = meshDataArray[0];

            if (!ValidateAttributeLayout(sourceData, VertexAttribute.Position, UnsafeUtility.SizeOf<Vector3>()))
                return false;

            if (sourceData.HasVertexAttribute(VertexAttribute.Normal) &&
                !ValidateAttributeLayout(sourceData, VertexAttribute.Normal, UnsafeUtility.SizeOf<Vector3>()))
            {
                return false;
            }

            if (sourceData.HasVertexAttribute(VertexAttribute.TexCoord0) &&
                !ValidateAttributeLayout(sourceData, VertexAttribute.TexCoord0, UnsafeUtility.SizeOf<Vector2>()))
            {
                return false;
            }

            return true;
        }

        private static bool ValidateAttributeLayout(Mesh.MeshData sourceData, VertexAttribute attribute, int expectedStride)
        {
            if (!sourceData.HasVertexAttribute(attribute))
                return false;

            int stream = sourceData.GetVertexAttributeStream(attribute);
            if (stream < 0)
                return false;

            return sourceData.GetVertexAttributeOffset(attribute) == 0 &&
                   sourceData.GetVertexBufferStride(stream) == expectedStride;
        }

        private static int ResolveIndexCount(Mesh sourceMesh)
        {
            int total = 0;
            int subMeshCount = sourceMesh.subMeshCount;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                total += (int)sourceMesh.GetIndexCount(subMeshIndex);

            return total;
        }

        private Bounds CalculateLocalBounds(NativeList<WreckModulePlacement> placements)
        {
            if (!placements.IsCreated || placements.Length <= 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            WreckModulePlacement first = placements[0];
            float3 firstCenter = first.Position + first.BoundsCenter;
            Bounds bounds = new Bounds(
                new Vector3(firstCenter.x, firstCenter.y, firstCenter.z),
                new Vector3(first.BoundsSize.x, first.BoundsSize.y, first.BoundsSize.z));
            int count = placements.Length;
            for (int i = 1; i < count; i++)
            {
                WreckModulePlacement placement = placements[i];
                float3 placementCenter = placement.Position + placement.BoundsCenter;
                bounds.Encapsulate(new Bounds(
                    new Vector3(placementCenter.x, placementCenter.y, placementCenter.z),
                    new Vector3(placement.BoundsSize.x, placement.BoundsSize.y, placement.BoundsSize.z)));
            }

            return bounds;
        }

        private static Bounds TranslateBounds(Bounds localBounds, Vector3 runtimeOrigin)
        {
            localBounds.center += runtimeOrigin;
            return localBounds;
        }

        private static float ComputeEntropy(ushort mask)
        {
            int optionCount = math.countbits((uint)mask);
            if (optionCount <= 0)
                return float.MaxValue;

            return math.log2(optionCount);
        }

        private static float ComputeEntropyNoise(int mortonIndex, ref XorShift128State rng)
        {
            uint cellHash = (uint)(mortonIndex * 1103515245 + 12345);
            XorShift128State cellRng = new XorShift128State(rng.State3 ^ cellHash);
            return cellRng.NextFloat01() * 0.001f;
        }

        private byte SelectModuleFromMask(ushort mask, int moduleCount, ref XorShift128State rng)
        {
            int optionCount = math.countbits((uint)mask);
            if (optionCount <= 0)
                return 0;

            int selected = (int)(rng.NextUInt() % (uint)optionCount);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                if ((mask & (1 << moduleIndex)) == 0)
                    continue;

                if (selected == 0)
                    return (byte)moduleIndex;

                selected--;
            }

            return 0;
        }

        private ushort ComputeCompatibleNeighborMask(ushort currentMask, ushort neighborMask, byte direction, int moduleCount)
        {
            ushort compatibleMask = 0;
            byte oppositeDirection = OppositeDirection(direction);

            for (int currentModuleIndex = 0; currentModuleIndex < moduleCount; currentModuleIndex++)
            {
                if ((currentMask & (1 << currentModuleIndex)) == 0)
                    continue;

                ushort currentSocket = ResolveSocket(currentModuleIndex, direction);
                for (int neighborModuleIndex = 0; neighborModuleIndex < moduleCount; neighborModuleIndex++)
                {
                    if ((neighborMask & (1 << neighborModuleIndex)) == 0)
                        continue;

                    ushort neighborSocket = ResolveSocket(neighborModuleIndex, oppositeDirection);
                    if ((currentSocket & neighborSocket) != 0)
                        compatibleMask |= (ushort)(1 << neighborModuleIndex);
                }
            }

            return compatibleMask;
        }

        private ushort ResolveSocket(int moduleIndex, byte direction)
        {
            WreckModuleRuntimeDefinition definition = _runtimeDefinitions[moduleIndex];
            return direction switch
            {
                DirectionNorth => definition.NorthSocket,
                DirectionEast => definition.EastSocket,
                DirectionSouth => definition.SouthSocket,
                DirectionWest => definition.WestSocket,
                DirectionTop => definition.TopSocket,
                DirectionBottom => definition.BottomSocket,
                _ => 0
            };
        }

        private ushort ApplyBoundaryConstraints(ushort mask, byte boundaryConstraints)
        {
            ushort constrainedMask = mask;
            if ((boundaryConstraints & (1 << DirectionNorth)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionNorth);
            if ((boundaryConstraints & (1 << DirectionEast)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionEast);
            if ((boundaryConstraints & (1 << DirectionSouth)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionSouth);
            if ((boundaryConstraints & (1 << DirectionWest)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionWest);
            if ((boundaryConstraints & (1 << DirectionTop)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionTop);
            if ((boundaryConstraints & (1 << DirectionBottom)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionBottom);
            return constrainedMask;
        }

        private ushort FilterBoundary(ushort mask, byte direction)
        {
            ushort filteredMask = 0;
            int moduleCount = ResolveRuntimeModuleCount();
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                if ((mask & (1 << moduleIndex)) == 0)
                    continue;

                if (ResolveSocket(moduleIndex, direction) != 0)
                    filteredMask |= (ushort)(1 << moduleIndex);
            }

            return filteredMask;
        }

        private byte ResolveBoundaryConstraintMask(int mortonIndex)
        {
            int3 coord = DecodeMorton3(mortonIndex);
            byte constraints = 0;
            int boundary = gridResolution - 1;

            if (coord.z >= boundary)
                constraints |= (byte)(1 << DirectionNorth);
            if (coord.x >= boundary)
                constraints |= (byte)(1 << DirectionEast);
            if (coord.z <= 0)
                constraints |= (byte)(1 << DirectionSouth);
            if (coord.x <= 0)
                constraints |= (byte)(1 << DirectionWest);
            if (coord.y >= boundary)
                constraints |= (byte)(1 << DirectionTop);
            if (coord.y <= 0)
                constraints |= (byte)(1 << DirectionBottom);

            return constraints;
        }

        private bool TryGetNeighbor(int3 coord, byte direction, out int neighborIndex)
        {
            int3 offset = direction switch
            {
                DirectionNorth => new int3(0, 0, 1),
                DirectionEast => new int3(1, 0, 0),
                DirectionSouth => new int3(0, 0, -1),
                DirectionWest => new int3(-1, 0, 0),
                DirectionTop => new int3(0, 1, 0),
                DirectionBottom => new int3(0, -1, 0),
                _ => int3.zero
            };

            int3 next = coord + offset;
            if (next.x < 0 || next.y < 0 || next.z < 0 ||
                next.x >= gridResolution || next.y >= gridResolution || next.z >= gridResolution)
            {
                neighborIndex = -1;
                return false;
            }

            neighborIndex = EncodeMorton3(next.x, next.y, next.z);
            return true;
        }

        private static byte OppositeDirection(byte direction)
        {
            return direction switch
            {
                DirectionNorth => DirectionSouth,
                DirectionEast => DirectionWest,
                DirectionSouth => DirectionNorth,
                DirectionWest => DirectionEast,
                DirectionTop => DirectionBottom,
                DirectionBottom => DirectionTop,
                _ => DirectionNorth
            };
        }

        private static int ClampPowerOfTwo(int value, int min, int max)
        {
            int clamped = math.clamp(value, min, max);
            if (Mathf.IsPowerOfTwo(clamped))
                return clamped;

            int nextPower = Mathf.NextPowerOfTwo(clamped);
            if (nextPower > max)
                nextPower >>= 1;
            if (nextPower < min)
                nextPower = min;
            return nextPower;
        }

        private static int EncodeMorton3(int x, int y, int z)
        {
            return Part1By2(x) | (Part1By2(y) << 1) | (Part1By2(z) << 2);
        }

        private static int3 DecodeMorton3(int morton)
        {
            return new int3(Compact1By2(morton), Compact1By2(morton >> 1), Compact1By2(morton >> 2));
        }

        private static int Part1By2(int value)
        {
            uint x = (uint)value & 0x000003FFu;
            x = (x ^ (x << 16)) & 0xFF0000FFu;
            x = (x ^ (x << 8)) & 0x0300F00Fu;
            x = (x ^ (x << 4)) & 0x030C30C3u;
            x = (x ^ (x << 2)) & 0x09249249u;
            return (int)x;
        }

        private static int Compact1By2(int value)
        {
            uint x = (uint)value & 0x09249249u;
            x = (x ^ (x >> 2)) & 0x030C30C3u;
            x = (x ^ (x >> 4)) & 0x0300F00Fu;
            x = (x ^ (x >> 8)) & 0xFF0000FFu;
            x = (x ^ (x >> 16)) & 0x000003FFu;
            return (int)x;
        }
    }
}
