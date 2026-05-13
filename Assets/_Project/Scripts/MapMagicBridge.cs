using System;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public interface IMapMagicBiomeEventListener
    {
        void OnMapMagicBiomeChanged(int biomeId);
    }

    public static class MapMagicBiomeEvents
    {
        private const int ExpectedPendingBiomeEventCapacity = 8;

        private static readonly RegistryBucket<IMapMagicBiomeEventListener> _listeners = new RegistryBucket<IMapMagicBiomeEventListener>(8);
        private static NativeQueue<int> _pendingBiomeIds;
        private static NativeQueue<int> _nextFrameBiomeIds;
        private static int _pendingBiomeIdCount;
        private static int _nextFrameBiomeIdCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingBiomeIdCount + _nextFrameBiomeIdCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingBiomeIds.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(MapMagicBiomeEvents), nameof(_pendingBiomeIds));
                _pendingBiomeIds.Dispose();
                _pendingBiomeIds = default;
            }

            if (_nextFrameBiomeIds.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(MapMagicBiomeEvents), nameof(_nextFrameBiomeIds));
                _nextFrameBiomeIds.Dispose();
                _nextFrameBiomeIds = default;
            }

            _pendingBiomeIdCount = 0;
            _nextFrameBiomeIdCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

        public static void Register(IMapMagicBiomeEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IMapMagicBiomeEventListener listener)
        {
            if (listener != null && _listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void RaiseBiomeChanged(int biomeId)
        {
            EnsureInitialized();
            if (_pendingBiomeIdCount + _nextFrameBiomeIdCount >= ExpectedPendingBiomeEventCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameBiomeIds.Enqueue(biomeId);
                _nextFrameBiomeIdCount++;
                return;
            }

            _pendingBiomeIds.Enqueue(biomeId);
            _pendingBiomeIdCount++;
        }

        public static void FlushPending()
        {
            if (!_pendingBiomeIds.IsCreated)
                return;

            PromoteNextFrameBiomeIdsIfFrontEmpty();
            int scanBudget = _pendingBiomeIdCount > 0 ? _pendingBiomeIdCount : ExpectedPendingBiomeEventCapacity;
            while (scanBudget > 0 && !_pendingBiomeIds.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingBiomeIds.TryDequeue(out int biomeId))
                    break;

                if (_pendingBiomeIdCount > 0)
                    _pendingBiomeIdCount--;

                scanBudget--;
                IMapMagicBiomeEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IMapMagicBiomeEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnMapMagicBiomeChanged(biomeId);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingBiomeIds.IsEmpty())
            {
                _pendingBiomeIdCount = 0;
                PromoteNextFrameBiomeIdsIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingBiomeIds.IsCreated)
            {
                _pendingBiomeIds = new NativeQueue<int>(Allocator.Persistent); // COLD ALLOC: NativeQueue<int>[8] - deferred MapMagic biome events flushed by SystemDispatcher - owner: MapMagicBiomeEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingBiomeIds,
                    ExpectedPendingBiomeEventCapacity,
                    nameof(MapMagicBiomeEvents),
                    nameof(_pendingBiomeIds),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingBiomeIds, ExpectedPendingBiomeEventCapacity);
            }

            if (!_nextFrameBiomeIds.IsCreated)
            {
                _nextFrameBiomeIds = new NativeQueue<int>(Allocator.Persistent); // COLD ALLOC: NativeQueue<int>[8] - next-frame MapMagic biome event lane prevents same-frame reentrant dispatch - owner: MapMagicBiomeEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameBiomeIds,
                    ExpectedPendingBiomeEventCapacity,
                    nameof(MapMagicBiomeEvents),
                    nameof(_nextFrameBiomeIds),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameBiomeIds, ExpectedPendingBiomeEventCapacity);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void PromoteNextFrameBiomeIdsIfFrontEmpty()
        {
            if (!_pendingBiomeIds.IsCreated ||
                !_nextFrameBiomeIds.IsCreated ||
                !_pendingBiomeIds.IsEmpty() ||
                _nextFrameBiomeIdCount <= 0)
            {
                return;
            }

            NativeQueue<int> swap = _pendingBiomeIds;
            _pendingBiomeIds = _nextFrameBiomeIds;
            _nextFrameBiomeIds = swap;
            _pendingBiomeIdCount = _nextFrameBiomeIdCount;
            _nextFrameBiomeIdCount = 0;
        }
    }

    public readonly struct MapMagicTerrainTileSnapshot
    {
        public MapMagicTerrainTileSnapshot(MapMagicBridge provider, int tileX, int tileZ, Terrain terrain)
        {
            Provider = provider;
            TileX = tileX;
            TileZ = tileZ;
            Terrain = terrain;
        }

        public MapMagicBridge Provider { get; }
        public int TileX { get; }
        public int TileZ { get; }
        public Terrain Terrain { get; }

        public bool IsValid => Terrain != null && Terrain.terrainData != null;
    }

    public interface IMapMagicTerrainTileEventListener
    {
        void OnMapMagicTerrainTileApplied(in MapMagicTerrainTileSnapshot snapshot);

        void OnMapMagicTerrainTileMoved(in MapMagicTerrainTileSnapshot snapshot);
    }

    public static class MapMagicTerrainTileEvents
    {
        private static readonly RegistryBucket<IMapMagicTerrainTileEventListener> _listeners =
            new RegistryBucket<IMapMagicTerrainTileEventListener>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _listeners.Clear();
        }

        public static void Register(IMapMagicTerrainTileEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IMapMagicTerrainTileEventListener listener)
        {
            if (listener != null && _listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void RaiseTileApplied(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!snapshot.IsValid)
                return;

            Terrain terrain = snapshot.Terrain;
            TerrainData terrainData = terrain.terrainData;
            TerrainChunkGeneratedSignal signal = new TerrainChunkGeneratedSignal
            {
                ChunkX = snapshot.TileX,
                ChunkZ = snapshot.TileZ,
                TerrainEntityHash = unchecked((uint)EntityId.ToULong(terrain.GetEntityId())),
                HeightmapResolution = terrainData.heightmapResolution,
                CacheRevision = 0,
                TerrainPosition = (float3)terrain.transform.position,
                TerrainSize = (float3)terrainData.size,
                Frame = (uint)Time.frameCount,
                Flags = 1
            };
            TerrainChunkGeneratedEvents.TryPublish(in signal);

            IMapMagicTerrainTileEventListener[] rawArray = _listeners.RawArray;
            int count = _listeners.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                IMapMagicTerrainTileEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnMapMagicTerrainTileApplied(in snapshot);
            }
        }

        public static void RaiseTileMoved(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!snapshot.IsValid)
                return;

            IMapMagicTerrainTileEventListener[] rawArray = _listeners.RawArray;
            int count = _listeners.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                IMapMagicTerrainTileEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnMapMagicTerrainTileMoved(in snapshot);
            }
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public abstract class MapMagicBridge : MonoBehaviour, ISlowTickable, ITerrainProvider
    {
        private const int BiomeMatrixLayerCount = 108;
        private const string TectonicSpineFamilyId = "biome.family.tectonic_spine";

        public readonly struct QuantizedHeightmapPayload
        {
            public QuantizedHeightmapPayload(
                NativeArray<ushort> heightSamples,
                Vector3 terrainPosition,
                Vector3 terrainSize,
                int heightmapResolution,
                int cacheRevision)
            {
                HeightSamples = heightSamples;
                TerrainPosition = terrainPosition;
                TerrainSize = terrainSize;
                HeightmapResolution = heightmapResolution;
                CacheRevision = cacheRevision;
            }

            public NativeArray<ushort> HeightSamples { get; }
            public Vector3 TerrainPosition { get; }
            public Vector3 TerrainSize { get; }
            public int HeightmapResolution { get; }
            public int CacheRevision { get; }

            public bool IsValid =>
                HeightSamples.IsCreated &&
                HeightmapResolution > 1 &&
                HeightSamples.Length >= HeightmapResolution * HeightmapResolution;
        }

        public static MapMagicBridge Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return null;
#endif
                return GlobalRegistry.MapMagic;
            }
        }

        public abstract float WaterSurfaceLevel { get; }
        public abstract bool IsAvailable { get; }
        public abstract Component RuntimeMapMagicObject { get; }
        public abstract bool SandboxProceduralTerrainOnly { get; }
        public abstract bool SandboxUseBiomeMatrixAlphamapLayers { get; }
        public abstract bool EnableSandboxThermalWeathering { get; }
        public abstract float SandboxThermalWeatheringStrength { get; }
        public abstract float SandboxThermalWeatheringTalusAngleDegrees { get; }
        public abstract bool EnableSandboxTectonicSpineDisplacement { get; }
        public abstract float SandboxTectonicSpineStrength { get; }
        public abstract float SandboxTectonicSpineFrequency { get; }
        public abstract float SandboxTectonicSpineRidgeSharpness { get; }
        public abstract uint SandboxTectonicSpineSeed { get; }
        public abstract bool EnableSandboxFakeCliffOverhangOffsets { get; }
        public abstract int CurrentBiomeID { get; }

        public abstract void SlowTick();
        public abstract bool TryGetHeight(float x, float z, out float height);
        public abstract bool TryGetNormal(float x, float z, float sampleDistance, out Vector3 normal);
        public abstract bool TryGetHeightAUP(Vector3 absoluteUniversePosition, out float height);
        public abstract bool TryGetNormalAUP(Vector3 absoluteUniversePosition, float sampleDistance, out Vector3 normal);

        public virtual float GetHeightAt(float3 aup)
        {
            return TryGetHeightAUP(new Vector3(aup.x, aup.y, aup.z), out float height) ? height : 0f;
        }

        public abstract bool TryGetActiveQuantizedHeightmapPayload(out QuantizedHeightmapPayload payload);
        public abstract bool TryGetQuantizedHeightmapPayload(float x, float z, out QuantizedHeightmapPayload payload);
        public abstract bool TryGetQuantizedHeightmapPayloadAUP(Vector3 absoluteUniversePosition, out QuantizedHeightmapPayload payload);
        public abstract bool TryGetTerrainSplatColorAUP(Vector3 absoluteUniversePosition, out Color color, out float confidence);
        public abstract bool TryGetTerrainSplatColor(float x, float z, out Color color, out float confidence);
        public abstract float SampleHeightAUP(Vector3 absoluteUniversePosition, float fallbackHeight = 0f);
        public abstract float GetHeight(float x, float z);
        public abstract bool TryResolveTerrainAt(float x, float z, out Terrain terrain);
        public abstract int CopyResolvedTerrainsTo(Terrain[] destination);
        public abstract int CopyTerrainTileSnapshotsTo(MapMagicTerrainTileSnapshot[] destination);
        public abstract bool IsUnderwater(float x, float y, float z);
        public abstract bool IsValidSpawnPoint(float x, float y, float z, out float bottomHeight);
        public abstract bool TryGetBiomeIndex(float x, float z, out int biomeIndex);
        public abstract bool TryGetMatrixBiomeId(float x, float z, out int matrixBiomeId);
        public abstract bool TryGetMatrixBiomeId(float x, float z, out int matrixBiomeId, out int alphamapLayer);
        public abstract bool TryGetMatrixBiomeInfluence(
            float x,
            float z,
            out int primaryBiomeId,
            out int secondaryBiomeId,
            out byte blend255,
            out int primaryAlphamapLayer,
            out int secondaryAlphamapLayer);
        public abstract bool TryGetMatrixBiomeId(
            float x,
            float z,
            HectonBiomeMatrixCatalog catalog,
            out int matrixBiomeId,
            out int alphamapLayer);
        public abstract int GetBiomeIndex(float x, float z);
        public abstract int GetCurrentBiome(float3 position);
        public abstract void SetPlayerTransform(Transform player);
        public abstract void SetMapMagicObject(UnityEngine.Object target);
        public abstract void SetWaterSurfaceLevel(float y);
        public abstract void SetSandboxProceduralTerrainOnly(bool enabled);
        public abstract void SetSandboxBiomeMatrixAlphamapLayers(bool enabled);
        public abstract JobHandle ScheduleSandboxThermalWeatheringPostProcess(
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float cellSizeMeters,
            float heightScaleMeters,
            JobHandle dependency = default);
        public abstract JobHandle ScheduleSandboxTectonicSpineDisplacementPostProcess(
            HectonBiomeMatrixProfile biomeProfile,
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float2 worldOriginXZ,
            float cellSizeMeters,
            JobHandle dependency = default);
        public abstract JobHandle ScheduleSandboxTectonicSpineDisplacementPostProcess(
            bool isTectonicSpineBiome,
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float2 worldOriginXZ,
            float cellSizeMeters,
            JobHandle dependency = default);
        public abstract JobHandle ScheduleSandboxFakeCliffOverhangOffsets(
            NativeArray<float> heights01,
            NativeArray<float2> horizontalOffsetsMeters,
            int width,
            int height,
            float cellSizeMeters,
            float heightScaleMeters,
            JobHandle dependency = default);
        public abstract bool SetRuntimeObjectsPerFrame(int objectsPerFrame);
        public abstract bool ConfigureRuntimeTerrainStreaming(
            bool draftsInPlaymode,
            int mainRange,
            int draftRange,
            int draftResolutionValue);
        public abstract bool ApplyRuntimeTerrainQuality(
            int pixelError,
            int baseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod);
        public abstract void MaintainRuntimeTerrainDetailLevels(
            int mainRange,
            int teardownRange,
            int mainPixelError,
            int mainBaseMapDistance,
            int draftPixelError,
            int draftBaseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod);

        public static bool TryResolveBiomeMatrixAlphamapLayer(int matrixBiomeId, out int alphamapLayer)
        {
            alphamapLayer = -1;
            if (matrixBiomeId < 1 || matrixBiomeId > BiomeMatrixLayerCount)
                return false;

            alphamapLayer = matrixBiomeId - 1;
            return true;
        }

        public static bool IsTectonicSpineMatrixBiome(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return false;

            if (IsTectonicSpineFamilyId(profile.familyId))
                return true;

            HectonBiomeFamilyProfile familyProfile = profile.familyProfile;
            return familyProfile != null && IsTectonicSpineFamilyId(familyProfile.familyId);
        }

        public static JobHandle ScheduleBrineBasinLipRidgeOverlay(
            NativeArray<byte> basinMask,
            NativeArray<float> lipOffsetMeters,
            int width,
            int height,
            int falloffCells,
            float lipHeightMeters,
            JobHandle dependency = default)
        {
            if (!basinMask.IsCreated ||
                !lipOffsetMeters.IsCreated ||
                width <= 2 ||
                height <= 2)
            {
                return dependency;
            }

            int cellCount = width * height;
            if (basinMask.Length < cellCount || lipOffsetMeters.Length < cellCount)
                return dependency;

            var job = new BrineBasinLipRidgeOverlayJob
            {
                BasinMask = basinMask,
                LipOffsetMeters = lipOffsetMeters,
                Width = width,
                Height = height,
                FalloffCells = math.max(1, falloffCells),
                LipHeightMeters = math.max(0f, lipHeightMeters)
            };

            int batchCount = math.max(1, math.min(64, cellCount / 16));
            return job.Schedule(cellCount, batchCount, dependency);
        }

        private static bool IsTectonicSpineFamilyId(string familyId)
        {
            return string.Equals(familyId, TectonicSpineFamilyId, StringComparison.OrdinalIgnoreCase);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BrineBasinLipRidgeOverlayJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> BasinMask;
            public NativeArray<float> LipOffsetMeters;
            public int Width;
            public int Height;
            public int FalloffCells;
            public float LipHeightMeters;

            public void Execute(int index)
            {
                if (BasinMask[index] != 0)
                {
                    LipOffsetMeters[index] = 0f;
                    return;
                }

                int x = index % Width;
                int z = index / Width;
                int radius = math.max(1, FalloffCells);
                float radiusSq = math.max(1f, radius * radius);
                float best = 0f;
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int nz = z + dz;
                    if ((uint)nz >= (uint)Height)
                        continue;

                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        int nx = x + dx;
                        if ((uint)nx >= (uint)Width)
                            continue;

                        int neighbor = nx + nz * Width;
                        if (BasinMask[neighbor] == 0)
                            continue;

                        float distanceSq = (dx * dx) + (dz * dz);
                        float ridge = 1f - math.saturate((distanceSq - 1f) / radiusSq);
                        best = math.max(best, ridge);
                    }
                }

                LipOffsetMeters[index] = best * LipHeightMeters;
            }
        }
    }
}
