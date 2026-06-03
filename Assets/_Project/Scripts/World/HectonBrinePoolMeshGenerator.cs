using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Hecton8.World
{
    /// <summary>
    /// Creates brine pool meshes and hazard volumes from closed basin records.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/World/Brine Pool Mesh Generator")]
    public sealed class HectonBrinePoolMeshGenerator : MonoBehaviour
    {
        [Header("Authored Runtime")]
        [Tooltip("Player builds consume this pre-baked brine pool hierarchy. Runtime mesh/object generation is editor-only.")]
        [SerializeField] private GameObject _authoredBrinePoolPrefab;

        [Tooltip("Optional Addressables reference to the pre-baked brine pool prefab/fog/collider hierarchy.")]
        [SerializeField] private AssetReferenceGameObject _authoredBrinePoolAddressable;

        public GameObject AuthoredBrinePoolPrefab => _authoredBrinePoolPrefab;
        public AssetReferenceGameObject AuthoredBrinePoolAddressable => _authoredBrinePoolAddressable;
        public bool HasAuthoredBrinePoolReference => _authoredBrinePoolPrefab != null || HasValidAddressableReference(_authoredBrinePoolAddressable);

        private static bool HasValidAddressableReference(AssetReferenceGameObject reference)
        {
            return reference != null && reference.RuntimeKeyIsValid();
        }

#if UNITY_EDITOR
        private const uint InvalidInputWarningHash = 0x414E4249u;
        private const uint EmptyBoundsWarningHash = 0x414E4245u;
        private const uint PoolCapWarningHash = 0x414E4250u;
        private const uint DuplicateHazardWarningHash = 0x414E4244u;
        private const uint RuntimeBuildRejectedWarningHash = 0x414E4255u;
        private const uint BrineGeneratorContextHash = 0x414E4252u;
        private const int MaxGeneratedBrinePools = 32;
        private const string GeneratedBrinePoolsRootName = "Generated Brine Pools";
        private const string BrinePoolObjectName = "BrinePool";
        private const float BrineSurfaceNormalTile = 64f;
        private const int BrineSurfaceSegmentCount = 32;
        private const float BrineSurfaceStepCos = 0.98078528f;
        private const float BrineSurfaceStepSin = 0.19509032f;

        [Header("Rendering")]
        [Tooltip("Material assigned to generated flat brine pool surfaces.")]
        [SerializeField] private Material brineMaterial;

        [Tooltip("Optional dense glowing fog material assigned to a coplanar child mesh under each pool.")]
        [SerializeField] private Material brineFogMaterial;

        [Header("Hazard")]
        [Tooltip("Trigger collider depth below the generated brine surface, in meters.")]
        [SerializeField] private float colliderDepthMeters = 4f;

        [Tooltip("Toxicity hazard intensity sent to HectonHazardManager.")]
        [SerializeField] private float hazardIntensity = 1f;

        [Tooltip("Visor glitch bias sent to HectonHazardManager for brine toxicity.")]
        [SerializeField] private float hazardVisorGlitchBias = 1f;

        [Tooltip("Stable id base added to generated basin ids for hazard registration.")]
        [SerializeField] private int hazardIdBase = 870000;

        // COLD ALLOC: ActiveBrinePool[32] - spawned brine pool bookkeeping - owner: HectonBrinePoolMeshGenerator
        private readonly ActiveBrinePool[] _activePools = new ActiveBrinePool[MaxGeneratedBrinePools];
        private int _activePoolCount;

        private Transform _poolRoot;
        private Mesh _sharedPoolMesh;

        /// <summary>
        /// Builds brine pool meshes for valid basin records and registers toxic hazard zones.
        /// </summary>
        public int BuildBrinePools(
            NativeArray<byte> basinMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            int width,
            int height,
            float cellSizeMeters,
            Vector3 runtimeOrigin)
        {
            if (Application.isPlaying)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(RuntimeBuildRejectedWarningHash, BrineGeneratorContextHash, 0);
                return 0;
            }

            if (!TryResolveCellCount(width, height, out int cellCount) ||
                !basinMask.IsCreated ||
                !basinRecords.IsCreated ||
                !math.isfinite(cellSizeMeters) ||
                !math.isfinite(colliderDepthMeters) ||
                !math.isfinite(hazardIntensity) ||
                !math.isfinite(hazardVisorGlitchBias) ||
                !IsFiniteRuntimePosition(runtimeOrigin) ||
                cellSizeMeters <= 0f ||
                colliderDepthMeters <= 0f ||
                hazardIntensity <= 0f ||
                hazardVisorGlitchBias < 0f ||
                basinMask.Length < cellCount ||
                basinRecords.Length < cellCount)
            {
                ClearBrinePools();
                GlobalTelemetryBus.PublishPerformanceWarning(InvalidInputWarningHash, BrineGeneratorContextHash, cellCount);
                return 0;
            }

            ClearBrinePools();
            EnsureRoot();
            int created = 0;
            float safeCellSize = math.max(0.001f, cellSizeMeters);
            for (int i = 0; i < cellCount; i++)
            {
                if (!TryResolvePoolBoundsFromRecord(
                        basinRecords[i],
                        basinMask,
                        width,
                        height,
                        out AnomalyBrinePoolBounds poolBounds))
                {
                    continue;
                }

                if (created >= MaxGeneratedBrinePools)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(PoolCapWarningHash, BrineGeneratorContextHash, created);
                    break;
                }

                if (!TryResolveHazardId(poolBounds.BasinId, out int hazardId))
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(InvalidInputWarningHash, BrineGeneratorContextHash, poolBounds.BasinId);
                    continue;
                }

                if (IsTrackedHazardId(hazardId))
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(DuplicateHazardWarningHash, BrineGeneratorContextHash, hazardId);
                    continue;
                }

                if (!TryResolvePoolCenterAup(poolBounds, safeCellSize, runtimeOrigin, out AbsoluteUniversePosition poolCenterAup))
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(InvalidInputWarningHash, BrineGeneratorContextHash, poolBounds.BasinId);
                    continue;
                }

                AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
                if (!originAup.IsFinite())
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(InvalidInputWarningHash, BrineGeneratorContextHash, poolBounds.BasinId);
                    continue;
                }

                float3 localCenter = AUPMath.ResolveCameraRelative(in poolCenterAup, in originAup);
                Vector3 runtimeCenter = new Vector3(localCenter.x, localCenter.y, localCenter.z);
                GameObject poolObject = CreatePoolObject(poolBounds, safeCellSize, runtimeCenter);
                if (!TryRegisterBrineHazard(in poolCenterAup, poolBounds, safeCellSize, hazardId))
                {
                    DestroyPoolObject(poolObject);
                    GlobalTelemetryBus.PublishPerformanceWarning(InvalidInputWarningHash, BrineGeneratorContextHash, hazardId);
                    continue;
                }

                _activePools[_activePoolCount] = new ActiveBrinePool
                {
                    GameObject = poolObject,
                    HazardId = hazardId
                };
                _activePoolCount++;
                created++;
            }

            if (created == 0)
                GlobalTelemetryBus.PublishPerformanceWarning(EmptyBoundsWarningHash, BrineGeneratorContextHash, cellCount);

            return created;
        }

        private static bool TryResolveCellCount(int width, int height, out int cellCount)
        {
            cellCount = 0;
            if (width <= 0 || height <= 0)
                return false;

            long total = (long)width * height;
            if (total > int.MaxValue)
                return false;

            cellCount = (int)total;
            return true;
        }

        private bool TryResolveHazardId(int basinId, out int hazardId)
        {
            hazardId = 0;
            long resolved = (long)hazardIdBase + basinId;
            if (resolved <= 0L || resolved > int.MaxValue)
                return false;

            hazardId = (int)resolved;
            return true;
        }

        /// <summary>
        /// Removes spawned pool objects and unregisters their hazards.
        /// </summary>
        public void ClearBrinePools()
        {
            BindExistingRootIfPresent();
            DestroyUntrackedPoolChildren();

            for (int i = 0; i < _activePoolCount; i++)
            {
                ActiveBrinePool pool = _activePools[i];
                HectonBrineToxicMudGrid.UnregisterCell(pool.HazardId);
                HectonHazardManager.Unregister(pool.HazardId);
                if (pool.GameObject == null)
                    continue;

                DestroyPoolObject(pool.GameObject);
            }

            ClearActivePoolState();
        }

        private static void DestroyPoolObject(GameObject poolObject)
        {
            if (poolObject == null)
                return;

            if (Application.isPlaying)
                Destroy(poolObject);
            else
                DestroyImmediate(poolObject);
        }

        private void OnDestroy()
        {
            ClearBrinePools();
            DestroySharedPoolMesh();
        }

        private void EnsureRoot()
        {
            if (_poolRoot != null)
                return;

            BindExistingRootIfPresent();
            if (_poolRoot != null)
                return;

            // COLD ALLOC: GameObject[1] - brine pool container - owner: HectonBrinePoolMeshGenerator
            var rootObject = new GameObject(GeneratedBrinePoolsRootName);
            rootObject.transform.SetParent(transform, false);
            rootObject.layer = ResolveBrinePhysicsLayer();
            _poolRoot = rootObject.transform;
        }

        private void BindExistingRootIfPresent()
        {
            if (_poolRoot != null)
                return;

            Transform existing = transform.Find(GeneratedBrinePoolsRootName);
            if (existing != null)
            {
                existing.gameObject.layer = ResolveBrinePhysicsLayer();
                _poolRoot = existing;
                return;
            }
        }

        private void DestroyUntrackedPoolChildren()
        {
            if (_poolRoot == null)
                return;

            for (int i = _poolRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _poolRoot.GetChild(i);
                if (child == null || IsTrackedPoolObject(child.gameObject))
                    continue;

                DestroyPoolObject(child.gameObject);
            }
        }

        private bool IsTrackedPoolObject(GameObject poolObject)
        {
            for (int i = 0; i < _activePoolCount; i++)
            {
                if (_activePools[i].GameObject == poolObject)
                    return true;
            }

            return false;
        }

        private bool IsTrackedHazardId(int hazardId)
        {
            for (int i = 0; i < _activePoolCount; i++)
            {
                if (_activePools[i].HazardId == hazardId)
                    return true;
            }

            return false;
        }

        private void ClearActivePoolState()
        {
            for (int i = 0; i < _activePoolCount; i++)
                _activePools[i] = default;

            _activePoolCount = 0;
        }

        private GameObject CreatePoolObject(
            AnomalyBrinePoolBounds poolBounds,
            float cellSizeMeters,
            Vector3 runtimeCenter)
        {
            float minWorldX = poolBounds.MinX * cellSizeMeters;
            float maxWorldX = (poolBounds.MaxX + 1) * cellSizeMeters;
            float minWorldZ = poolBounds.MinZ * cellSizeMeters;
            float maxWorldZ = (poolBounds.MaxZ + 1) * cellSizeMeters;
            float sizeX = math.max(cellSizeMeters, maxWorldX - minWorldX);
            float sizeZ = math.max(cellSizeMeters, maxWorldZ - minWorldZ);

            // COLD ALLOC: GameObject[1] - generated brine pool hazard anchor - owner: HectonBrinePoolMeshGenerator
            var poolObject = new GameObject(BrinePoolObjectName);
            poolObject.transform.SetParent(_poolRoot, false);
            poolObject.transform.position = runtimeCenter;
            poolObject.transform.localScale = new Vector3(sizeX, 1f, sizeZ);
            poolObject.layer = ResolveBrinePhysicsLayer();

            float safeColliderDepth = math.max(0.001f, colliderDepthMeters);
            BoxCollider collider = poolObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, safeColliderDepth * -0.5f, 0f);
            collider.size = new Vector3(1f, safeColliderDepth, 1f);
            collider.isTrigger = true;
            // Cinematic hazard fake: the brine surface/fog are global shader planes; this disabled trigger only satisfies ToxinHazard's collider contract.
            collider.enabled = false;
            poolObject.AddComponent<ToxinHazard>();
            return poolObject;
        }

        private static bool TryResolvePoolCenterAup(
            AnomalyBrinePoolBounds poolBounds,
            float cellSizeMeters,
            Vector3 runtimeOrigin,
            out AbsoluteUniversePosition poolCenterAup)
        {
            poolCenterAup = default;
            double safeCellSize = math.max(0.001d, (double)cellSizeMeters);
            double minWorldX = poolBounds.MinX * safeCellSize;
            double maxWorldX = (poolBounds.MaxX + 1) * safeCellSize;
            double minWorldZ = poolBounds.MinZ * safeCellSize;
            double maxWorldZ = (poolBounds.MaxZ + 1) * safeCellSize;
            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!runtimeOriginAup.IsFinite())
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.OffsetMeters(
                in runtimeOriginAup,
                new double3(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z));
            if (!originAup.IsFinite())
                return false;

            double3 originAbsolute = originAup.ToAbsoluteDouble3();
            poolCenterAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                originAbsolute.x + (minWorldX + maxWorldX) * 0.5d,
                originAbsolute.y + poolBounds.LipHeight,
                originAbsolute.z + (minWorldZ + maxWorldZ) * 0.5d));
            return poolCenterAup.IsFinite();
        }

        private static bool TryResolvePoolBoundsFromRecord(
            AnomalyBasinRecord record,
            NativeArray<byte> basinMask,
            int width,
            int height,
            out AnomalyBrinePoolBounds poolBounds)
        {
            poolBounds = default;
            if (record.Valid == 0 ||
                record.BasinId <= 0 ||
                record.CellCount <= 0 ||
                record.MinX < 0 ||
                record.MinZ < 0 ||
                record.MaxX >= width ||
                record.MaxZ >= height ||
                record.MinX > record.MaxX ||
                record.MinZ > record.MaxZ ||
                !math.isfinite(record.DeepestHeight) ||
                !math.isfinite(record.LipHeight) ||
                record.LipHeight <= record.DeepestHeight)
                return false;

            int maskedCount = 0;
            int minX = width;
            int minZ = height;
            int maxX = 0;
            int maxZ = 0;
            for (int z = record.MinZ; z <= record.MaxZ; z++)
            {
                int rowOffset = z * width;
                for (int x = record.MinX; x <= record.MaxX; x++)
                {
                    if (basinMask[rowOffset + x] == 0)
                        continue;

                    minX = math.min(minX, x);
                    minZ = math.min(minZ, z);
                    maxX = math.max(maxX, x);
                    maxZ = math.max(maxZ, z);
                    maskedCount++;
                }
            }

            if (maskedCount <= 0)
                return false;

            if (maskedCount != record.CellCount)
                return false;

            poolBounds = new AnomalyBrinePoolBounds
            {
                BasinId = record.BasinId,
                MinX = minX,
                MinZ = minZ,
                MaxX = maxX,
                MaxZ = maxZ,
                MaskedCount = maskedCount,
                LipHeight = record.LipHeight,
                Valid = 1
            };
            return true;
        }

        private void CreateFogVolume(Transform poolTransform, Mesh poolMesh)
        {
            if (brineFogMaterial == null || poolMesh == null)
                return;

            // COLD ALLOC: GameObject[1] - generated brine fog render proxy - owner: HectonBrinePoolMeshGenerator
            var fogObject = new GameObject("BrinePoolFog");
            fogObject.transform.SetParent(poolTransform, false);
            fogObject.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            fogObject.layer = ResolveBrinePhysicsLayer();

            MeshFilter meshFilter = fogObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = fogObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = poolMesh;
            meshRenderer.sharedMaterial = brineFogMaterial;
        }

        private Mesh EnsureSharedPoolMesh()
        {
            if (_sharedPoolMesh != null)
                return _sharedPoolMesh;

            // COLD ALLOC: Mesh[1] - generated brine surface ellipse - owner: HectonBrinePoolMeshGenerator
            var mesh = new Mesh
            {
                name = "BrinePoolUnitEllipseMesh"
            };

            int vertexCount = BrineSurfaceSegmentCount + 1;
            int indexCount = BrineSurfaceSegmentCount * 3;
            // COLD ALLOC: Vector3[33] - one-time shared brine ellipse vertices - owner: HectonBrinePoolMeshGenerator
            Vector3[] vertices = new Vector3[vertexCount];
            // COLD ALLOC: Vector2[33] - one-time shared brine ellipse uvs - owner: HectonBrinePoolMeshGenerator
            Vector2[] uvs = new Vector2[vertexCount];
            // COLD ALLOC: Vector3[33] - one-time shared brine ellipse normals - owner: HectonBrinePoolMeshGenerator
            Vector3[] normals = new Vector3[vertexCount];
            // COLD ALLOC: int[96] - one-time shared brine ellipse indices - owner: HectonBrinePoolMeshGenerator
            int[] triangles = new int[indexCount];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(BrineSurfaceNormalTile * 0.5f, BrineSurfaceNormalTile * 0.5f);
            normals[0] = Vector3.up;
            float x = 0.5f;
            float z = 0f;
            for (int i = 0; i < BrineSurfaceSegmentCount; i++)
            {
                int vertexIndex = i + 1;
                vertices[vertexIndex] = new Vector3(x, 0f, z);
                uvs[vertexIndex] = new Vector2(
                    (x + 0.5f) * BrineSurfaceNormalTile,
                    (z + 0.5f) * BrineSurfaceNormalTile);
                normals[vertexIndex] = Vector3.up;
                float nextX = (x * BrineSurfaceStepCos) - (z * BrineSurfaceStepSin);
                z = (x * BrineSurfaceStepSin) + (z * BrineSurfaceStepCos);
                x = nextX;
            }

            for (int i = 0; i < BrineSurfaceSegmentCount; i++)
            {
                int triangleIndex = i * 3;
                int current = i + 1;
                int next = ((i + 1) % BrineSurfaceSegmentCount) + 1;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = next;
                triangles[triangleIndex + 2] = current;
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            _sharedPoolMesh = mesh;
            return mesh;
        }

        private void DestroySharedPoolMesh()
        {
            if (_sharedPoolMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(_sharedPoolMesh);
            else
                DestroyImmediate(_sharedPoolMesh);

            _sharedPoolMesh = null;
        }

        private bool TryRegisterBrineHazard(in AbsoluteUniversePosition centerAup, AnomalyBrinePoolBounds poolBounds, float cellSizeMeters, int hazardId)
        {
            float sizeX = math.max(cellSizeMeters, (poolBounds.MaxX - poolBounds.MinX + 1) * cellSizeMeters);
            float sizeZ = math.max(cellSizeMeters, (poolBounds.MaxZ - poolBounds.MinZ + 1) * cellSizeMeters);
            if (hazardId <= 0 ||
                cellSizeMeters <= 0f ||
                colliderDepthMeters <= 0f ||
                hazardIntensity <= 0f ||
                hazardVisorGlitchBias < 0f ||
                !math.isfinite(sizeX) ||
                !math.isfinite(sizeZ) ||
                !math.isfinite(poolBounds.LipHeight) ||
                !math.isfinite(colliderDepthMeters))
                return false;

            float radius = math.max(math.max(sizeX, sizeZ) * 0.5f, math.max(0.001f, colliderDepthMeters));
            HectonBrineToxicMudGrid.RegisterCell(hazardId, in centerAup, sizeX, sizeZ, colliderDepthMeters);
            if (!HectonBrineToxicMudGrid.IsRegisteredCell(hazardId))
                return false;

            if (!HectonHazardManager.Register(hazardId, in centerAup, hazardIntensity, radius, HazardType.Toxicity, hazardVisorGlitchBias))
            {
                HectonBrineToxicMudGrid.UnregisterCell(hazardId);
                return false;
            }

            return true;
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            return math.isfinite(position.x) &&
                   math.isfinite(position.y) &&
                   math.isfinite(position.z);
        }

        private static int ResolveBrinePhysicsLayer()
        {
            return HectonLayerMasks.BrineToxicity;
        }

        private struct ActiveBrinePool
        {
            public GameObject GameObject;
            public int HazardId;
        }
#endif
    }
}
