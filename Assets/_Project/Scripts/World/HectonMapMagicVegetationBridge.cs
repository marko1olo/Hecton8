using System;
using System.Collections.Generic;
using Hecton8.Core;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Builds indirect vegetation instance buffers from MapMagic terrain tiles and
    /// binds only the nearest tile set to the external indirect renderers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonMapMagicVegetationBridge : MonoBehaviour, ISlowTickable
    {
        private const string SandLayerName = "L_Sand";
        private const string GreenSandLayerName = "L_sandGreen";
        private const string RockLayerName = "L_Rocks";
        private const string BasaltLayerName = "L_Basalt";
        private const float DefaultWaterLevel = 4900f;
        private const float DefaultUnderwaterDepth = 300f;
        private const int InitialTileCapacity = 32;
        private const int InitialMatrixBuilderCapacity = 512;
        private const long EmptyTileKey = long.MinValue;

        [Header("── References ─────────────────────────────")]
        [SerializeField]
        [Tooltip("Normative MapMagic owner used to filter foreign tile events.")]
        private MapMagicBridge mapMagicBridge;

        [SerializeField]
        [Tooltip("Player transform used to select the nearest loaded terrain tiles.")]
        private Transform playerTransform;

        [SerializeField]
        [Tooltip("Indirect renderer used for surface vegetation above water.")]
        private HectonIndirectVegetationRenderer surfaceRenderer;

        [SerializeField]
        [Tooltip("Indirect renderer used for underwater vegetation below water.")]
        private HectonIndirectVegetationRenderer underwaterRenderer;

        [Header("── Tile Selection ─────────────────────────")]
        [SerializeField, Min(1)]
        [Tooltip("Maximum number of nearest tiles kept in the render buffers at once.")]
        private int maxNearestTiles = 4;

        [Header("── Mask Sampling ───────────────────────────")]
        [SerializeField, Min(4900f)]
        [Tooltip("Project water surface level. Task contract fixes this at 4900.")]
        private float waterLevel = DefaultWaterLevel;

        [SerializeField, Min(1f)]
        [Tooltip("Maximum allowed underwater spawn depth measured from the water level.")]
        private float underwaterDepth = DefaultUnderwaterDepth;

        [SerializeField, Min(1)]
        [Tooltip("Stride in alphamap pixels when generating vegetation candidates per tile.")]
        private int sampleStride = 4;

        [SerializeField, Range(0f, 0.95f)]
        [Tooltip("Normalized jitter fraction applied inside each sampling cell to break the grid.")]
        private float jitterFraction = 0.45f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum combined sand mask required for a vegetation spawn candidate.")]
        private float sandMaskThreshold = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum blocking rock or basalt mask value that rejects the candidate.")]
        private float blockedMaskThreshold = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum terrain normal Y accepted for vegetation placement.")]
        private float minimumNormalY = 0.7f;

        [SerializeField, Min(0f)]
        [Tooltip("Small offset along terrain normal to keep strips above the sampled surface.")]
        private float normalOffset = 0.04f;

        [Header("── Scale Ranges ───────────────────────────")]
        [SerializeField]
        [Tooltip("Uniform scale range used for surface vegetation instances.")]
        private Vector2 surfaceScaleRange = new Vector2(0.7f, 1.15f);

        [SerializeField]
        [Tooltip("Uniform scale range used for underwater vegetation instances.")]
        private Vector2 underwaterScaleRange = new Vector2(1.15f, 2.1f);

        [Header("── Draw Bounds ────────────────────────────")]
        [SerializeField]
        [Tooltip("Extra padding added to the aggregated world bounds before binding them to the renderers.")]
        private Vector3 drawBoundsPadding = new Vector3(6f, 24f, 6f);

        private struct TilePayload
        {
            public Matrix4x4[] SurfaceMatrices;
            public Matrix4x4[] UnderwaterMatrices;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
            public Bounds WorldBounds;

            public bool HasSurface => SurfaceMatrices != null && SurfaceMatrices.Length > 0;
            public bool HasUnderwater => UnderwaterMatrices != null && UnderwaterMatrices.Length > 0;
            public bool HasAny => HasSurface || HasUnderwater;
        }

        private struct LayerIndices
        {
            public int Sand;
            public int GreenSand;
            public int Rock;
            public int Basalt;
        }

        // COLD ALLOC: Dictionary<long, TilePayload>[32] - cached tile vegetation payloads keyed by MapMagic coord - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<long, TilePayload> _tilePayloads = new Dictionary<long, TilePayload>(InitialTileCapacity);
        // COLD ALLOC: List<Matrix4x4>[512] - surface tile matrix staging during tile rebuild - owner: HectonMapMagicVegetationBridge
        private readonly List<Matrix4x4> _surfaceMatrixBuilder = new List<Matrix4x4>(InitialMatrixBuilderCapacity);
        // COLD ALLOC: List<Matrix4x4>[512] - underwater tile matrix staging during tile rebuild - owner: HectonMapMagicVegetationBridge
        private readonly List<Matrix4x4> _underwaterMatrixBuilder = new List<Matrix4x4>(InitialMatrixBuilderCapacity);

        private long[] _selectedTileKeys;
        private long[] _candidateTileKeys;
        private float[] _candidateTileDistances;
        private Matrix4x4[] _surfaceAggregateMatrices = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _underwaterAggregateMatrices = Array.Empty<Matrix4x4>();
        private ComputeBuffer _surfaceInstanceBuffer;
        private ComputeBuffer _underwaterInstanceBuffer;
        private int _selectedTileCount;
        private bool _isRegistered;
        private bool _eventsSubscribed;
        private bool _selectionDirty = true;

        private void Awake()
        {
            maxNearestTiles = Mathf.Max(1, maxNearestTiles);
            sampleStride = Mathf.Max(1, sampleStride);
            underwaterDepth = Mathf.Max(1f, underwaterDepth);
            surfaceScaleRange = NormalizeScaleRange(surfaceScaleRange);
            underwaterScaleRange = NormalizeScaleRange(underwaterScaleRange);

            ResolveRuntimeDependencies();

            // COLD ALLOC: long[maxNearestTiles] - active nearest tile selection cache - owner: HectonMapMagicVegetationBridge
            _selectedTileKeys = new long[maxNearestTiles];
            // COLD ALLOC: long[maxNearestTiles] - nearest tile candidate staging cache - owner: HectonMapMagicVegetationBridge
            _candidateTileKeys = new long[maxNearestTiles];
            // COLD ALLOC: float[maxNearestTiles] - nearest tile distance staging cache - owner: HectonMapMagicVegetationBridge
            _candidateTileDistances = new float[maxNearestTiles];

            InitializeTileKeyCache(_selectedTileKeys);
            InitializeTileKeyCache(_candidateTileKeys);
        }

        private void OnEnable()
        {
            TrySubscribeEvents();
            TryRegister();
            RefreshBindings();
        }

        private void Start()
        {
            TryRegister();
            RefreshBindings();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnsubscribeEvents();
            ClearRendererBindings();
            ReleaseBuffers();
            _selectedTileCount = 0;
            _selectionDirty = true;
            InitializeTileKeyCache(_selectedTileKeys);
            InitializeTileKeyCache(_candidateTileKeys);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnsubscribeEvents();
            ClearRendererBindings();
            ReleaseBuffers();
        }

        /// <summary>
        /// Re-evaluates the nearest loaded tile set and rebinds the indirect buffers when needed.
        /// </summary>
        public void SlowTick()
        {
            ResolveRuntimeDependencies();
            RefreshBindings();
        }

        private void HandleTileApplied(TerrainTile tile, TileData tileData, StopToken stop)
        {
            if (!isActiveAndEnabled || tile == null || tileData == null || tileData.isDraft)
                return;

            if (stop != null && stop.stop)
                return;

            ResolveRuntimeDependencies();

            if (IsForeignTile(tile))
                return;

            Terrain terrain = ResolveMainTerrain(tile);
            if (terrain == null || terrain.terrainData == null)
            {
                RemoveTilePayload(tile.coord.x, tile.coord.z);
                RefreshBindings();
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (!TryResolveLayerIndices(terrainData, out LayerIndices indices))
            {
                RemoveTilePayload(tile.coord.x, tile.coord.z);
                RefreshBindings();
                return;
            }

            TilePayload payload = BuildTilePayload(tile, terrain, terrainData, in indices);
            long tileKey = PackTileCoord(tile.coord.x, tile.coord.z);

            if (payload.HasAny)
                _tilePayloads[tileKey] = payload;
            else
                _tilePayloads.Remove(tileKey);

            _selectionDirty = true;
            RefreshBindings();
        }

        private void HandleTileMoved(TerrainTile tile)
        {
            if (!isActiveAndEnabled || tile == null)
                return;

            ResolveRuntimeDependencies();

            if (IsForeignTile(tile))
                return;

            RemoveTilePayload(tile.coord.x, tile.coord.z);
            RefreshBindings();
        }

        private void RefreshBindings()
        {
            if (_candidateTileKeys == null || _candidateTileDistances == null || _selectedTileKeys == null)
                return;

            if (_tilePayloads.Count == 0)
            {
                if (_selectedTileCount == 0 && !_selectionDirty)
                    return;

                _selectedTileCount = 0;
                _selectionDirty = false;
                InitializeTileKeyCache(_selectedTileKeys);
                InitializeTileKeyCache(_candidateTileKeys);
                ClearRendererBindings();
                ReleaseBuffers();
                return;
            }

            if (playerTransform == null)
            {
                if (_selectedTileCount == 0 && !_selectionDirty)
                    return;

                _selectedTileCount = 0;
                _selectionDirty = false;
                InitializeTileKeyCache(_selectedTileKeys);
                InitializeTileKeyCache(_candidateTileKeys);
                ClearRendererBindings();
                ReleaseBuffers();
                return;
            }

            int candidateCount = SelectNearestTiles(playerTransform.position);
            bool selectionChanged = HasSelectionChanged(candidateCount);
            if (!selectionChanged && !_selectionDirty)
                return;

            CommitNearestSelection(candidateCount);
            RebuildAndBindActiveBuffers();
            _selectionDirty = false;
        }

        private int SelectNearestTiles(Vector3 playerPosition)
        {
            InitializeTileKeyCache(_candidateTileKeys);

            for (int i = 0; i < _candidateTileDistances.Length; i++)
                _candidateTileDistances[i] = float.PositiveInfinity;

            Dictionary<long, TilePayload>.Enumerator enumerator = _tilePayloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, TilePayload> current = enumerator.Current;
                TilePayload currentPayload = current.Value;
                float distanceSqr = GetTileDistanceSqr(playerPosition, in currentPayload);
                InsertNearestTile(current.Key, distanceSqr);
            }

            int count = 0;
            while (count < _candidateTileKeys.Length && _candidateTileKeys[count] != EmptyTileKey)
                count++;

            return count;
        }

        private void RebuildAndBindActiveBuffers()
        {
            int totalSurfaceCount = 0;
            int totalUnderwaterCount = 0;
            bool hasSurfaceBounds = false;
            bool hasUnderwaterBounds = false;
            Bounds surfaceBounds = default;
            Bounds underwaterBounds = default;

            for (int i = 0; i < _selectedTileCount; i++)
            {
                if (!_tilePayloads.TryGetValue(_selectedTileKeys[i], out TilePayload payload))
                    continue;

                if (payload.HasSurface)
                {
                    totalSurfaceCount += payload.SurfaceMatrices.Length;
                    EncapsulateBounds(ref surfaceBounds, ref hasSurfaceBounds, payload.WorldBounds);
                }

                if (payload.HasUnderwater)
                {
                    totalUnderwaterCount += payload.UnderwaterMatrices.Length;
                    EncapsulateBounds(ref underwaterBounds, ref hasUnderwaterBounds, payload.WorldBounds);
                }
            }

            if (hasSurfaceBounds)
                surfaceBounds.Expand(drawBoundsPadding);

            if (hasUnderwaterBounds)
                underwaterBounds.Expand(drawBoundsPadding);

            if (totalSurfaceCount > 0)
            {
                EnsureMatrixCapacity(ref _surfaceAggregateMatrices, totalSurfaceCount);

                int writeIndex = 0;
                for (int i = 0; i < _selectedTileCount; i++)
                {
                    if (!_tilePayloads.TryGetValue(_selectedTileKeys[i], out TilePayload payload) || !payload.HasSurface)
                        continue;

                    Matrix4x4[] matrices = payload.SurfaceMatrices;
                    Array.Copy(matrices, 0, _surfaceAggregateMatrices, writeIndex, matrices.Length);
                    writeIndex += matrices.Length;
                }

                UploadChannel(surfaceRenderer, ref _surfaceInstanceBuffer, _surfaceAggregateMatrices, totalSurfaceCount, surfaceBounds);
            }
            else
            {
                ClearChannel(surfaceRenderer);
                ReleaseBuffer(ref _surfaceInstanceBuffer);
            }

            if (totalUnderwaterCount > 0)
            {
                EnsureMatrixCapacity(ref _underwaterAggregateMatrices, totalUnderwaterCount);

                int writeIndex = 0;
                for (int i = 0; i < _selectedTileCount; i++)
                {
                    if (!_tilePayloads.TryGetValue(_selectedTileKeys[i], out TilePayload payload) || !payload.HasUnderwater)
                        continue;

                    Matrix4x4[] matrices = payload.UnderwaterMatrices;
                    Array.Copy(matrices, 0, _underwaterAggregateMatrices, writeIndex, matrices.Length);
                    writeIndex += matrices.Length;
                }

                UploadChannel(underwaterRenderer, ref _underwaterInstanceBuffer, _underwaterAggregateMatrices, totalUnderwaterCount, underwaterBounds);
            }
            else
            {
                ClearChannel(underwaterRenderer);
                ReleaseBuffer(ref _underwaterInstanceBuffer);
            }
        }

        private TilePayload BuildTilePayload(TerrainTile tile, Terrain terrain, TerrainData terrainData, in LayerIndices indices)
        {
            int alphamapResolution = terrainData.alphamapResolution;
            if (alphamapResolution <= 0)
                return default;

            EnsureBuilderCapacity(alphamapResolution);
            _surfaceMatrixBuilder.Clear();
            _underwaterMatrixBuilder.Clear();

            // COLD ALLOC: float[,,] - terrain alphamap snapshot for one tile rebuild - owner: HectonMapMagicVegetationBridge
            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, alphamapResolution, alphamapResolution);
            Vector3 terrainPosition = terrain.GetPosition();
            Vector3 terrainSize = terrainData.size;
            float normalizedStep = sampleStride / (float)alphamapResolution;
            float jitterAmplitude = normalizedStep * jitterFraction;
            float minimumUnderwaterHeight = waterLevel - underwaterDepth;
            Quaternion alignRotation;

            for (int z = 0; z < alphamapResolution; z += sampleStride)
            {
                for (int x = 0; x < alphamapResolution; x += sampleStride)
                {
                    float sandMask = 0f;

                    if (indices.Sand >= 0)
                        sandMask += alphamaps[z, x, indices.Sand];

                    if (indices.GreenSand >= 0)
                        sandMask += alphamaps[z, x, indices.GreenSand];

                    if (sandMask <= sandMaskThreshold)
                        continue;

                    float blockedMask = 0f;

                    if (indices.Rock >= 0)
                        blockedMask = Mathf.Max(blockedMask, alphamaps[z, x, indices.Rock]);

                    if (indices.Basalt >= 0)
                        blockedMask = Mathf.Max(blockedMask, alphamaps[z, x, indices.Basalt]);

                    if (blockedMask > blockedMaskThreshold)
                        continue;

                    uint seed = BuildSampleSeed(tile.coord.x, tile.coord.z, x, z);
                    float jitterU = (Hash01(seed) * 2f - 1f) * jitterAmplitude;
                    float jitterV = (Hash01(seed ^ 0x9E3779B9u) * 2f - 1f) * jitterAmplitude;
                    float u = Mathf.Clamp01(((x + 0.5f) / alphamapResolution) + jitterU);
                    float v = Mathf.Clamp01(((z + 0.5f) / alphamapResolution) + jitterV);

                    Vector3 normal = terrainData.GetInterpolatedNormal(u, v);
                    if (normal.y < minimumNormalY)
                        continue;

                    float worldY = terrainPosition.y + terrainData.GetInterpolatedHeight(u, v);
                    bool isSurface = worldY > waterLevel;
                    bool isUnderwater = worldY <= waterLevel && worldY >= minimumUnderwaterHeight;

                    if (!isSurface && !isUnderwater)
                        continue;

                    float worldX = terrainPosition.x + terrainSize.x * u;
                    float worldZ = terrainPosition.z + terrainSize.z * v;
                    float yaw = Hash01(seed ^ 0x85EBCA6Bu) * 360f;
                    float scale = isSurface
                        ? Mathf.Lerp(surfaceScaleRange.x, surfaceScaleRange.y, Hash01(seed ^ 0xC2B2AE35u))
                        : Mathf.Lerp(underwaterScaleRange.x, underwaterScaleRange.y, Hash01(seed ^ 0x27D4EB2Fu));
                    Vector3 position = new Vector3(worldX, worldY, worldZ) + normal * normalOffset;
                    alignRotation = Quaternion.FromToRotation(Vector3.up, normal);
                    Quaternion rotation = alignRotation * Quaternion.Euler(0f, yaw, 0f);
                    Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, new Vector3(scale, scale, scale));

                    if (isSurface)
                        _surfaceMatrixBuilder.Add(matrix);
                    else
                        _underwaterMatrixBuilder.Add(matrix);
                }
            }

            TilePayload payload = default;
            payload.SurfaceMatrices = CopyBuilderToArray(_surfaceMatrixBuilder);
            payload.UnderwaterMatrices = CopyBuilderToArray(_underwaterMatrixBuilder);
            payload.MinX = terrainPosition.x;
            payload.MaxX = terrainPosition.x + terrainSize.x;
            payload.MinZ = terrainPosition.z;
            payload.MaxZ = terrainPosition.z + terrainSize.z;
            payload.WorldBounds = CreateTerrainBounds(terrainPosition, terrainSize);
            return payload;
        }

        private static Terrain ResolveMainTerrain(TerrainTile tile)
        {
            if (tile.main != null && tile.main.terrain != null)
                return tile.main.terrain;

            if (tile.ActiveTerrain != null)
                return tile.ActiveTerrain;

            return tile.GetTerrain(isDraft: false);
        }

        private bool TryResolveLayerIndices(TerrainData terrainData, out LayerIndices indices)
        {
            indices = default;
            indices.Sand = -1;
            indices.GreenSand = -1;
            indices.Rock = -1;
            indices.Basalt = -1;

            if (terrainData == null)
                return false;

            TerrainLayer[] terrainLayers = terrainData.terrainLayers;
            if (terrainLayers == null || terrainLayers.Length == 0)
                return false;

            for (int i = 0; i < terrainLayers.Length; i++)
            {
                TerrainLayer layer = terrainLayers[i];
                if (layer == null)
                    continue;

                string layerName = layer.name;
                if (string.Equals(layerName, SandLayerName, StringComparison.Ordinal))
                {
                    indices.Sand = i;
                    continue;
                }

                if (string.Equals(layerName, GreenSandLayerName, StringComparison.Ordinal))
                {
                    indices.GreenSand = i;
                    continue;
                }

                if (string.Equals(layerName, RockLayerName, StringComparison.Ordinal))
                {
                    indices.Rock = i;
                    continue;
                }

                if (string.Equals(layerName, BasaltLayerName, StringComparison.Ordinal))
                    indices.Basalt = i;
            }

            return indices.Sand >= 0 || indices.GreenSand >= 0;
        }

        private void UploadChannel(
            HectonIndirectVegetationRenderer renderer,
            ref ComputeBuffer buffer,
            Matrix4x4[] matrices,
            int count,
            Bounds bounds)
        {
            if (renderer == null)
            {
                ReleaseBuffer(ref buffer);
                return;
            }

            EnsureInstanceBuffer(ref buffer, count);
            if (buffer == null)
            {
                ClearChannel(renderer);
                return;
            }

            buffer.SetData(matrices, 0, 0, count);
            renderer.BindInstanceBuffer(buffer, count);
            renderer.SetDrawBounds(bounds);
        }

        private void EnsureInstanceBuffer(ref ComputeBuffer buffer, int count)
        {
            if (count <= 0)
            {
                ReleaseBuffer(ref buffer);
                return;
            }

            if (buffer != null && buffer.count >= count)
                return;

            ReleaseBuffer(ref buffer);
            // COLD ALLOC: ComputeBuffer[count] - indirect vegetation instance matrix payload - owner: HectonMapMagicVegetationBridge
            buffer = new ComputeBuffer(count, HectonIndirectVegetationRenderer.InstanceMatrixStride, ComputeBufferType.Structured);
        }

        private void EnsureBuilderCapacity(int alphamapResolution)
        {
            int samplesPerAxis = ((alphamapResolution - 1) / sampleStride) + 1;
            int estimatedSamples = samplesPerAxis * samplesPerAxis;

            if (_surfaceMatrixBuilder.Capacity < estimatedSamples)
            {
                // COLD ALLOC: List<Matrix4x4>[estimatedSamples] - surface tile staging growth - owner: HectonMapMagicVegetationBridge
                _surfaceMatrixBuilder.Capacity = estimatedSamples;
            }

            if (_underwaterMatrixBuilder.Capacity < estimatedSamples)
            {
                // COLD ALLOC: List<Matrix4x4>[estimatedSamples] - underwater tile staging growth - owner: HectonMapMagicVegetationBridge
                _underwaterMatrixBuilder.Capacity = estimatedSamples;
            }
        }

        private static Matrix4x4[] CopyBuilderToArray(List<Matrix4x4> source)
        {
            if (source == null || source.Count == 0)
                return null;

            // COLD ALLOC: Matrix4x4[source.Count] - per-tile immutable vegetation payload - owner: HectonMapMagicVegetationBridge
            Matrix4x4[] result = new Matrix4x4[source.Count];
            source.CopyTo(result);
            return result;
        }

        private static Bounds CreateTerrainBounds(Vector3 terrainPosition, Vector3 terrainSize)
        {
            Vector3 center = terrainPosition + terrainSize * 0.5f;
            return new Bounds(center, terrainSize);
        }

        private static void EncapsulateBounds(ref Bounds aggregateBounds, ref bool hasBounds, Bounds tileBounds)
        {
            if (!hasBounds)
            {
                aggregateBounds = tileBounds;
                hasBounds = true;
                return;
            }

            aggregateBounds.Encapsulate(tileBounds.min);
            aggregateBounds.Encapsulate(tileBounds.max);
        }

        private static Vector2 NormalizeScaleRange(Vector2 range)
        {
            float min = Mathf.Max(0.01f, Mathf.Min(range.x, range.y));
            float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2(min, max);
        }

        private static void InitializeTileKeyCache(long[] cache)
        {
            if (cache == null)
                return;

            for (int i = 0; i < cache.Length; i++)
                cache[i] = EmptyTileKey;
        }

        private bool HasSelectionChanged(int candidateCount)
        {
            if (_selectedTileCount != candidateCount)
                return true;

            for (int i = 0; i < candidateCount; i++)
            {
                if (_selectedTileKeys[i] != _candidateTileKeys[i])
                    return true;
            }

            return false;
        }

        private void CommitNearestSelection(int candidateCount)
        {
            for (int i = 0; i < _selectedTileKeys.Length; i++)
                _selectedTileKeys[i] = i < candidateCount ? _candidateTileKeys[i] : EmptyTileKey;

            _selectedTileCount = candidateCount;
        }

        private void InsertNearestTile(long tileKey, float distanceSqr)
        {
            int insertIndex = -1;
            for (int i = 0; i < _candidateTileDistances.Length; i++)
            {
                if (distanceSqr < _candidateTileDistances[i])
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < 0)
                return;

            for (int i = _candidateTileKeys.Length - 1; i > insertIndex; i--)
            {
                _candidateTileKeys[i] = _candidateTileKeys[i - 1];
                _candidateTileDistances[i] = _candidateTileDistances[i - 1];
            }

            _candidateTileKeys[insertIndex] = tileKey;
            _candidateTileDistances[insertIndex] = distanceSqr;
        }

        private static float GetTileDistanceSqr(Vector3 playerPosition, in TilePayload payload)
        {
            float clampedX = Mathf.Clamp(playerPosition.x, payload.MinX, payload.MaxX);
            float clampedZ = Mathf.Clamp(playerPosition.z, payload.MinZ, payload.MaxZ);
            float deltaX = playerPosition.x - clampedX;
            float deltaZ = playerPosition.z - clampedZ;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

        private static long PackTileCoord(int x, int z)
        {
            unchecked
            {
                return ((long)x << 32) ^ (uint)z;
            }
        }

        private void RemoveTilePayload(int x, int z)
        {
            _tilePayloads.Remove(PackTileCoord(x, z));
            _selectionDirty = true;
        }

        private void ResolveRuntimeDependencies()
        {
            if (mapMagicBridge == null)
                mapMagicBridge = MapMagicBridge.Instance;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }

        private bool IsForeignTile(TerrainTile tile)
        {
            if (tile == null)
                return true;

            if (mapMagicBridge == null || mapMagicBridge.RuntimeMapMagicObject == null)
                return false;

            return tile.mapMagic != mapMagicBridge.RuntimeMapMagicObject;
        }

        private void TryRegister()
        {
            if (_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _isRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _isRegistered = false;
        }

        private void TrySubscribeEvents()
        {
            if (_eventsSubscribed)
                return;

            TerrainTile.OnTileApplied += HandleTileApplied;
            TerrainTile.OnTileMoved += HandleTileMoved;
            _eventsSubscribed = true;
        }

        private void TryUnsubscribeEvents()
        {
            if (!_eventsSubscribed)
                return;

            TerrainTile.OnTileApplied -= HandleTileApplied;
            TerrainTile.OnTileMoved -= HandleTileMoved;
            _eventsSubscribed = false;
        }

        private void ClearRendererBindings()
        {
            ClearChannel(surfaceRenderer);
            ClearChannel(underwaterRenderer);
        }

        private static void ClearChannel(HectonIndirectVegetationRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.ClearInstanceBuffer();
            renderer.ClearDrawBoundsOverride();
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _surfaceInstanceBuffer);
            ReleaseBuffer(ref _underwaterInstanceBuffer);
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void EnsureMatrixCapacity(ref Matrix4x4[] matrixCache, int requiredCount)
        {
            if (matrixCache != null && matrixCache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: Matrix4x4[nextCapacity] - aggregated active vegetation upload staging - owner: HectonMapMagicVegetationBridge
            matrixCache = new Matrix4x4[nextCapacity];
        }

        private static uint BuildSampleSeed(int tileX, int tileZ, int sampleX, int sampleZ)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)tileX) * 16777619u;
                hash = (hash ^ (uint)tileZ) * 16777619u;
                hash = (hash ^ (uint)sampleX) * 16777619u;
                hash = (hash ^ (uint)sampleZ) * 16777619u;
                return hash;
            }
        }

        private static float Hash01(uint seed)
        {
            unchecked
            {
                seed ^= seed >> 16;
                seed *= 0x7FEB352Du;
                seed ^= seed >> 15;
                seed *= 0x846CA68Bu;
                seed ^= seed >> 16;
                return (seed & 0x00FFFFFFu) / 16777215f;
            }
        }
    }
}
