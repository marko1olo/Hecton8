using System.Collections.Generic;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Scavenging;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Deterministic environmental-envelope spawner for harvestable resource nodes.
    /// Uses AUP sector quantization, MapMagic seabed queries, cached thermal/slope envelopes,
    /// and voxel-density rejection so resources are placed by conditions instead of biome labels.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4042)]
    public sealed class ResourceDistributionDirector : MonoBehaviour, ISlowTickable
    {
        private const int DefaultSectorSizeMeters = 128;
        private const int DefaultMaxPendingSpawnRequests = 1024;
        private const int DefaultPoolWarmupFloor = 64;
        private const float DefaultSlopeSampleDistanceMeters = 4f;
        private const float DefaultVoxelSolidThreshold = 0.08f;
        private const float DefaultSectorMarginMeters = 2f;
        private const float GhostAlpha = 0.24f;
        private const string RuntimePrefabName = "PFB_RuntimeResourceNode_Generic";

        private sealed class SectorState
        {
            public readonly int2 Coordinates;
            public readonly List<ResourceNode> ActiveNodes;
            public bool SpawnEnvelopeQueued;

            public SectorState(int2 coordinates, int initialCapacity)
            {
                Coordinates = coordinates;
                // COLD ALLOC: List<ResourceNode>[initialCapacity] — live sector resource node registry — owner: ResourceDistributionDirector
                ActiveNodes = new List<ResourceNode>(initialCapacity);
                SpawnEnvelopeQueued = false;
            }
        }

        private struct SpawnRequest
        {
            public long SectorKey;
            public int TemplateIndex;
            public Vector3 RuntimePosition;
            public float YawDegrees;
        }

        [Header("References")]
        [SerializeField]
        [Tooltip("Authored resource-node templates consumed by the environmental-envelope spawner.")]
        private ResourceNodeTemplate[] resourceTemplates;

        [SerializeField]
        [Tooltip("Optional explicit player transform. Runtime falls back to WorldRuntimeReferenceUtility when empty.")]
        private Transform playerTransform;

        [SerializeField]
        [Tooltip("Optional explicit MapMagic bridge. Runtime falls back to WorldRuntimeReferenceUtility when empty.")]
        private MapMagicBridge mapMagicBridge;

        [SerializeField]
        [Tooltip("Optional explicit vegetation bridge. Runtime falls back to WorldRuntimeReferenceUtility when empty.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Optional explicit voxel engine. Runtime falls back to WorldRuntimeReferenceUtility when empty.")]
        private HectonVoxelEngine voxelEngine;

        [Header("Streaming")]
        [SerializeField, Min(32)]
        [Tooltip("AUP sector edge length used by the deterministic resource-node envelope pass.")]
        private int sectorSizeMeters = DefaultSectorSizeMeters;

        [SerializeField, Range(0, 3)]
        [Tooltip("How many sector rings around the player stay resident.")]
        private int sectorRadius = 1;

        [SerializeField, Range(1, 64)]
        [Tooltip("Maximum queued node spawns resolved during one SlowTick.")]
        private int maxSpawnsPerSlowTick = 12;

        [SerializeField, Min(8)]
        [Tooltip("One-time generic node pool warmup floor. Final warmup is the max of this value and computed envelope demand.")]
        private int poolWarmupFloor = DefaultPoolWarmupFloor;

        [Header("Envelope Sampling")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Probe distance used when resolving fallback terrain slope samples.")]
        private float slopeSampleDistanceMeters = DefaultSlopeSampleDistanceMeters;

        [SerializeField, Min(0f)]
        [Tooltip("Rejects samples this close to sector edges to avoid visible seam packing.")]
        private float sectorEdgeMarginMeters = DefaultSectorMarginMeters;

        [SerializeField, Range(0.001f, 1f)]
        [Tooltip("Positive voxel density above this threshold blocks surface placement.")]
        private float voxelSolidThreshold = DefaultVoxelSolidThreshold;

        [Header("Diagnostics")]
        [SerializeField] private int _debugResidentSectorCount;
        [SerializeField] private int _debugActiveNodeCount;
        [SerializeField] private int _debugQueuedSpawnCount;
        [SerializeField] private int _debugLastAcceptedTemplateHash;
        [SerializeField] private Vector2Int _debugPlayerSector;

        // COLD ALLOC: Dictionary<long,SectorState>[32] — resident sector registry keyed by AUP sector hash — owner: ResourceDistributionDirector
        private Dictionary<long, SectorState> _residentSectors;
        // COLD ALLOC: Queue<SpawnRequest>[DefaultMaxPendingSpawnRequests] — deterministic deferred resource spawn queue — owner: ResourceDistributionDirector
        private Queue<SpawnRequest> _pendingSpawns;
        // COLD ALLOC: List<long>[32] — sector eviction scratch list — owner: ResourceDistributionDirector
        private List<long> _sectorEvictionScratch;

        private GameObject _runtimePrefab;
        private Mesh _ghostCubeMesh;
        private Material _ghostMaterial;
        private bool _runtimePoolReady;
        private bool _slowTickRegistered;
        private int _computedPoolWarmupCount;

        private void Awake()
        {
            sectorSizeMeters = math.max(32, sectorSizeMeters);
            maxSpawnsPerSlowTick = math.max(1, maxSpawnsPerSlowTick);
            poolWarmupFloor = math.max(8, poolWarmupFloor);
            slopeSampleDistanceMeters = math.max(0.5f, slopeSampleDistanceMeters);
            sectorEdgeMarginMeters = math.clamp(sectorEdgeMarginMeters, 0f, sectorSizeMeters * 0.25f);
            voxelSolidThreshold = math.clamp(voxelSolidThreshold, 0.001f, 1f);

            // COLD ALLOC: Dictionary<long,SectorState>[32] — resident sector registry keyed by AUP sector hash — owner: ResourceDistributionDirector
            _residentSectors = new Dictionary<long, SectorState>(32);
            // COLD ALLOC: Queue<SpawnRequest>[DefaultMaxPendingSpawnRequests] — deterministic deferred resource spawn queue — owner: ResourceDistributionDirector
            _pendingSpawns = new Queue<SpawnRequest>(DefaultMaxPendingSpawnRequests);
            // COLD ALLOC: List<long>[32] — sector eviction scratch list — owner: ResourceDistributionDirector
            _sectorEvictionScratch = new List<long>(32);

            EnsureRuntimePrefab();
            UpdateDiagnostics(default);
        }

        private void OnEnable()
        {
            if (_slowTickRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _slowTickRegistered = true;
        }

        private void OnDisable()
        {
            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            DespawnAllResidentNodes();
            _residentSectors?.Clear();
            _pendingSpawns?.Clear();
            _runtimePoolReady = false;
            UpdateDiagnostics(default);
        }

        /// <summary>
        /// Slow-tick residency pass. Maintains deterministic resource sectors around the player.
        /// </summary>
        public void SlowTick()
        {
            if (!TryResolveRuntimeDependencies())
                return;

            EnsureRuntimePool();

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
            int2 playerSector = QuantizeSector(in playerAup);
            _debugPlayerSector = new Vector2Int(playerSector.x, playerSector.y);

            RefreshResidentSectors(playerSector);
            ProcessPendingSpawns();
            UpdateDiagnostics(playerSector);
        }

        private bool TryResolveRuntimeDependencies()
        {
            if (resourceTemplates == null || resourceTemplates.Length == 0)
                return false;

            if (!WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform) || playerTransform == null)
                return false;

            if (!WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge) || mapMagicBridge == null)
                return false;

            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
            return true;
        }

        private void EnsureRuntimePrefab()
        {
            if (_runtimePrefab != null)
                return;

            _ghostCubeMesh = CaptureCubeMesh();
            _ghostMaterial = CreateGhostMaterial();

            // COLD ALLOC: GameObject[1] — generic pooled runtime resource-node prefab template — owner: ResourceDistributionDirector
            _runtimePrefab = new GameObject(RuntimePrefabName);
            _runtimePrefab.transform.SetParent(transform, false);
            _runtimePrefab.SetActive(false);

            MeshFilter meshFilter = _runtimePrefab.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = _ghostCubeMesh;

            MeshRenderer meshRenderer = _runtimePrefab.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _ghostMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            BoxCollider boxCollider = _runtimePrefab.AddComponent<BoxCollider>();
            boxCollider.size = Vector3.one;

            SphereCollider sphereCollider = _runtimePrefab.AddComponent<SphereCollider>();
            sphereCollider.enabled = false;
            sphereCollider.radius = 0.5f;

            _runtimePrefab.AddComponent<ResourceNode>();
        }

        private void EnsureRuntimePool()
        {
            if (_runtimePoolReady || _runtimePrefab == null)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            _computedPoolWarmupCount = ComputeRequiredPoolWarmupCount();
            int warmupCount = math.max(poolWarmupFloor, _computedPoolWarmupCount);
            if (!pool.HasPool(_runtimePrefab))
                pool.Warmup(_runtimePrefab, warmupCount);

            _runtimePoolReady = pool.HasPool(_runtimePrefab);
        }

        private int ComputeRequiredPoolWarmupCount()
        {
            if (resourceTemplates == null || resourceTemplates.Length == 0)
                return poolWarmupFloor;

            int sectorsInWindow = (sectorRadius * 2) + 1;
            sectorsInWindow *= sectorsInWindow;

            int perSectorDemand = 0;
            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate template = resourceTemplates[i];
                if (template == null)
                    continue;

                perSectorDemand += math.max(1, template.MaxInstancesPerSector);
            }

            return math.max(poolWarmupFloor, perSectorDemand * sectorsInWindow);
        }

        private void RefreshResidentSectors(int2 playerSector)
        {
            _sectorEvictionScratch.Clear();
            Dictionary<long, SectorState>.Enumerator residentEnumerator = _residentSectors.GetEnumerator();
            while (residentEnumerator.MoveNext())
            {
                SectorState state = residentEnumerator.Current.Value;
                int deltaX = math.abs(state.Coordinates.x - playerSector.x);
                int deltaY = math.abs(state.Coordinates.y - playerSector.y);
                if (deltaX > sectorRadius || deltaY > sectorRadius)
                    _sectorEvictionScratch.Add(residentEnumerator.Current.Key);
                else
                    CompactSectorNodes(state);
            }

            residentEnumerator.Dispose();

            for (int i = 0; i < _sectorEvictionScratch.Count; i++)
                EvictSector(_sectorEvictionScratch[i]);

            for (int z = -sectorRadius; z <= sectorRadius; z++)
            {
                for (int x = -sectorRadius; x <= sectorRadius; x++)
                {
                    int2 sector = new int2(playerSector.x + x, playerSector.y + z);
                    long sectorKey = ComposeSectorKey(sector);
                    if (_residentSectors.TryGetValue(sectorKey, out SectorState existingState))
                    {
                        CompactSectorNodes(existingState);
                        continue;
                    }

                    SectorState state = new SectorState(sector, ComputePerSectorInitialCapacity());
                    _residentSectors.Add(sectorKey, state);
                    EnqueueSectorEnvelope(state, sectorKey);
                }
            }
        }

        private int ComputePerSectorInitialCapacity()
        {
            int capacity = 4;
            if (resourceTemplates == null)
                return capacity;

            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate template = resourceTemplates[i];
                if (template == null)
                    continue;

                capacity += math.max(1, template.MaxInstancesPerSector);
            }

            return capacity;
        }

        private void EnqueueSectorEnvelope(SectorState state, long sectorKey)
        {
            if (state == null || state.SpawnEnvelopeQueued)
                return;

            state.SpawnEnvelopeQueued = true;
            for (int templateIndex = 0; templateIndex < resourceTemplates.Length; templateIndex++)
            {
                ResourceNodeTemplate template = resourceTemplates[templateIndex];
                if (template == null)
                    continue;

                int acceptedForTemplate = 0;
                int candidateBudget = template.CandidateBudgetPerSector;
                for (int candidateIndex = 0; candidateIndex < candidateBudget; candidateIndex++)
                {
                    if (_pendingSpawns.Count >= DefaultMaxPendingSpawnRequests ||
                        acceptedForTemplate >= template.MaxInstancesPerSector)
                    {
                        return;
                    }

                    if (!TryBuildSpawnRequest(state.Coordinates, sectorKey, template, templateIndex, candidateIndex, out SpawnRequest request))
                        continue;

                    _pendingSpawns.Enqueue(request);
                    acceptedForTemplate++;
                }
            }
        }

        private bool TryBuildSpawnRequest(
            int2 sector,
            long sectorKey,
            ResourceNodeTemplate template,
            int templateIndex,
            int candidateIndex,
            out SpawnRequest request)
        {
            request = default;
            uint state = SeedSectorCandidate(sector, template.StableHashId, candidateIndex);

            double absoluteX = (sector.x * (double)sectorSizeMeters) + ResolveSectorOffsetMeters(ref state);
            double absoluteZ = (sector.y * (double)sectorSizeMeters) + ResolveSectorOffsetMeters(ref state);
            Vector3 runtimeProbe = AbsoluteToRuntime(absoluteX, 0d, absoluteZ);
            if (!mapMagicBridge.TryGetHeight(runtimeProbe.x, runtimeProbe.z, out float seabedHeight))
                return false;

            Vector3 runtimePosition = new Vector3(runtimeProbe.x, seabedHeight + template.SpawnOffsetMeters, runtimeProbe.z);
            float waterSurface = mapMagicBridge.WaterSurfaceLevel;
            float depthMeters = math.max(0f, waterSurface - seabedHeight);
            float temperatureCelsius = ResolveTemperature(runtimePosition);
            float slopeDegrees = ResolveSlope(runtimePosition);
            if (!template.MatchesEnvelope(depthMeters, temperatureCelsius, slopeDegrees))
                return false;

            if (Next01(ref state) > template.PlacementProbability)
                return false;

            if (IsBlockedByVoxelSolid(runtimePosition))
                return false;

            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(runtimePosition);
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            request = new SpawnRequest
            {
                SectorKey = sectorKey,
                TemplateIndex = templateIndex,
                RuntimePosition = runtimePosition,
                YawDegrees = Next01(ref state) * 360f
            };
            return true;
        }

        private void ProcessPendingSpawns()
        {
            if (!_runtimePoolReady || _runtimePrefab == null || _pendingSpawns.Count == 0)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            int processedCount = 0;
            while (processedCount < maxSpawnsPerSlowTick && _pendingSpawns.Count > 0)
            {
                SpawnRequest request = _pendingSpawns.Peek();
                if (!_residentSectors.TryGetValue(request.SectorKey, out SectorState sectorState))
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                if ((uint)request.TemplateIndex >= (uint)resourceTemplates.Length)
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                ResourceNodeTemplate template = resourceTemplates[request.TemplateIndex];
                if (template == null)
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                Quaternion rotation = Quaternion.Euler(0f, request.YawDegrees, 0f);
                GameObject instance = pool.Spawn(_runtimePrefab, request.RuntimePosition, rotation);
                if (instance == null)
                    break;

                _pendingSpawns.Dequeue();
                processedCount++;

                if (!instance.TryGetComponent(out ResourceNode node))
                {
                    pool.Despawn(instance);
                    continue;
                }

                node.ApplyRuntimeTemplate(template, _ghostCubeMesh, _ghostMaterial);
                node.RefreshRuntimeSpatialRegistration();
                sectorState.ActiveNodes.Add(node);
                _debugLastAcceptedTemplateHash = template.StableHashId;
            }
        }

        private void CompactSectorNodes(SectorState state)
        {
            if (state == null)
                return;

            List<ResourceNode> nodes = state.ActiveNodes;
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                ResourceNode node = nodes[i];
                if (node == null || !node.gameObject.activeInHierarchy)
                    nodes.RemoveAt(i);
            }
        }

        private void EvictSector(long sectorKey)
        {
            if (!_residentSectors.TryGetValue(sectorKey, out SectorState state))
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            List<ResourceNode> nodes = state.ActiveNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (node == null)
                    continue;

                if (pool != null)
                    pool.Despawn(node.gameObject);
                else
                    node.gameObject.SetActive(false);
            }

            nodes.Clear();
            _residentSectors.Remove(sectorKey);
        }

        private void DespawnAllResidentNodes()
        {
            if (_residentSectors == null || _residentSectors.Count == 0)
                return;

            _sectorEvictionScratch.Clear();
            Dictionary<long, SectorState>.Enumerator enumerator = _residentSectors.GetEnumerator();
            while (enumerator.MoveNext())
                _sectorEvictionScratch.Add(enumerator.Current.Key);
            enumerator.Dispose();

            for (int i = 0; i < _sectorEvictionScratch.Count; i++)
                EvictSector(_sectorEvictionScratch[i]);

            _sectorEvictionScratch.Clear();
        }

        private bool IsBlockedByVoxelSolid(Vector3 runtimePosition)
        {
            if (voxelEngine == null || !voxelEngine.TryGetNearestActiveVolume(runtimePosition, out HectonVoxelVolume volume) || volume == null)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out Bounds localBounds))
                return false;

            Vector3 localPoint = volume.transform.InverseTransformPoint(runtimePosition);
            if (!localBounds.Contains(localPoint))
                return false;

            return volume.TrySampleDensity(runtimePosition, out float density, out float density01) &&
                   (density > 0f || density01 >= voxelSolidThreshold);
        }

        private float ResolveTemperature(Vector3 runtimePosition)
        {
            return vegetationBridge != null
                ? vegetationBridge.GetWaterTemperature(runtimePosition)
                : 0f;
        }

        private float ResolveSlope(Vector3 runtimePosition)
        {
            if (vegetationBridge != null &&
                vegetationBridge.TrySampleTerrainSlopeDegrees(runtimePosition, slopeSampleDistanceMeters, out float vegetationSlope))
            {
                return vegetationSlope;
            }

            float probe = math.max(0.5f, slopeSampleDistanceMeters);
            if (!mapMagicBridge.TryGetHeight(runtimePosition.x + probe, runtimePosition.z, out float heightPosX) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x - probe, runtimePosition.z, out float heightNegX) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x, runtimePosition.z + probe, out float heightPosZ) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x, runtimePosition.z - probe, out float heightNegZ))
            {
                return 0f;
            }

            float gradientX = (heightPosX - heightNegX) / (probe * 2f);
            float gradientZ = (heightPosZ - heightNegZ) / (probe * 2f);
            float gradientMagnitude = math.sqrt((gradientX * gradientX) + (gradientZ * gradientZ));
            return math.degrees(math.atan(gradientMagnitude));
        }

        private int2 QuantizeSector(in AbsoluteUniversePosition position)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            double sectorSize = math.max(1d, sectorSizeMeters);
            return new int2(
                (int)math.floor(absolute.x / sectorSize),
                (int)math.floor(absolute.z / sectorSize));
        }

        private Vector3 AbsoluteToRuntime(double absoluteX, double absoluteY, double absoluteZ)
        {
            AbsoluteUniversePosition candidate = AbsoluteUniversePosition.FromAbsolutePosition(new double3(absoluteX, absoluteY, absoluteZ));
            float3 runtime = candidate.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        private float ResolveSectorOffsetMeters(ref uint state)
        {
            float margin = math.clamp(sectorEdgeMarginMeters, 0f, sectorSizeMeters * 0.45f);
            float usableSpan = math.max(1f, sectorSizeMeters - (margin * 2f));
            return margin + (Next01(ref state) * usableSpan);
        }

        private static long ComposeSectorKey(int2 sector)
        {
            return ((long)sector.x << 32) ^ (uint)sector.y;
        }

        private static uint SeedSectorCandidate(int2 sector, int templateHash, int candidateIndex)
        {
            uint seed = 2166136261u;
            seed = Mix(seed, (uint)sector.x);
            seed = Mix(seed, (uint)sector.y);
            seed = Mix(seed, (uint)templateHash);
            seed = Mix(seed, (uint)candidateIndex);
            return seed != 0u ? seed : 0xA341316Cu;
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            return hash;
        }

        private static float Next01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private Mesh CaptureCubeMesh()
        {
            // COLD ALLOC: GameObject[1] — temporary primitive source used to capture the built-in cube mesh — owner: ResourceDistributionDirector
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshFilter filter = temp.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (Application.isPlaying)
                Destroy(temp);
            else
                DestroyImmediate(temp);

            return mesh;
        }

        private Material CreateGhostMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            // COLD ALLOC: Material[1] — shared ghost placeholder material for meshless resource nodes — owner: ResourceDistributionDirector
            Material material = new Material(shader)
            {
                name = "MAT_Runtime_ResourceGhost"
            };

            Color ghostColor = new Color(1f, 0.15f, 0.1f, GhostAlpha);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", ghostColor);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", ghostColor);

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }

        private void UpdateDiagnostics(int2 playerSector)
        {
            _debugResidentSectorCount = _residentSectors != null ? _residentSectors.Count : 0;
            _debugQueuedSpawnCount = _pendingSpawns != null ? _pendingSpawns.Count : 0;

            int activeNodeCount = 0;
            if (_residentSectors != null)
            {
                Dictionary<long, SectorState>.Enumerator enumerator = _residentSectors.GetEnumerator();
                while (enumerator.MoveNext())
                    activeNodeCount += enumerator.Current.Value.ActiveNodes.Count;
                enumerator.Dispose();
            }

            _debugActiveNodeCount = activeNodeCount;
            _debugPlayerSector = new Vector2Int(playerSector.x, playerSector.y);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sectorSizeMeters = math.max(32, sectorSizeMeters);
            sectorRadius = math.clamp(sectorRadius, 0, 3);
            maxSpawnsPerSlowTick = math.max(1, maxSpawnsPerSlowTick);
            poolWarmupFloor = math.max(8, poolWarmupFloor);
            slopeSampleDistanceMeters = math.max(0.5f, slopeSampleDistanceMeters);
            voxelSolidThreshold = math.clamp(voxelSolidThreshold, 0.001f, 1f);
            sectorEdgeMarginMeters = math.max(0f, sectorEdgeMarginMeters);
        }
#endif
    }
}
