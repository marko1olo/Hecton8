using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Creates brine pool meshes and hazard volumes from closed basin records.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/World/Brine Pool Mesh Generator")]
    public sealed class HectonBrinePoolMeshGenerator : MonoBehaviour
    {
        private const string NativeMemoryOwner = nameof(HectonBrinePoolMeshGenerator);
        private const string BoundsLabel = "brinePoolBounds";
        private const uint InvalidInputWarningHash = 0x414E4249u;
        private const uint EmptyBoundsWarningHash = 0x414E4245u;
        private const uint PoolCapWarningHash = 0x414E4250u;
        private const uint BrineGeneratorContextHash = 0x414E4252u;
        private const int MaxGeneratedBrinePools = 32;
        private const string BrineToxicityLayerName = "BrineToxicity";
        private const string BrinePoolObjectName = "BrinePool";
        private static readonly int BrineToxicityLayer = LayerMask.NameToLayer(BrineToxicityLayerName);

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

        // COLD ALLOC: List<ActiveBrinePool>[32] — spawned brine pool bookkeeping — owner: HectonBrinePoolMeshGenerator
        private readonly List<ActiveBrinePool> _activePools = new List<ActiveBrinePool>(MaxGeneratedBrinePools);

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
            ClearBrinePools();

            int cellCount = math.max(1, width) * math.max(1, height);
            if (basinMask.Length < cellCount || basinRecords.Length < cellCount)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(InvalidInputWarningHash, BrineGeneratorContextHash, cellCount);
                return 0;
            }

            EnsureRoot();
            NativeArray<AnomalyBrinePoolBounds> bounds = default;
            try
            {
                // COLD ALLOC: NativeArray<AnomalyBrinePoolBounds>[cellCount] — brine mesh bake bounds — owner: HectonBrinePoolMeshGenerator
                bounds = new NativeArray<AnomalyBrinePoolBounds>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(bounds, NativeMemoryOwner, BoundsLabel, NativeAllocationLifetime.TempJob);

                var boundsJob = new ResolveBrinePoolBoundsJob
                {
                    BasinMask = basinMask,
                    BasinRecords = basinRecords,
                    Bounds = bounds,
                    Width = math.max(1, width),
                    Height = math.max(1, height)
                };

                JobHandle handle = boundsJob.Schedule(cellCount, 64);
                // COLD SYNC JOB: brine mesh baking must resolve bounds before GameObject creation; not a frame tick path.
                handle.Complete();

                int created = 0;
                float safeCellSize = math.max(0.001f, cellSizeMeters);
                for (int i = 0; i < bounds.Length; i++)
                {
                    AnomalyBrinePoolBounds poolBounds = bounds[i];
                    if (poolBounds.Valid == 0)
                        continue;

                    if (created >= MaxGeneratedBrinePools)
                    {
                        GlobalTelemetryBus.PublishPerformanceWarning(PoolCapWarningHash, BrineGeneratorContextHash, created);
                        break;
                    }

                    GameObject poolObject = CreatePoolObject(poolBounds, safeCellSize, runtimeOrigin, out Vector3 runtimeCenter);
                    int hazardId = hazardIdBase + poolBounds.BasinId;
                    RegisterBrineHazard(runtimeCenter, poolBounds, safeCellSize, hazardId);

                    _activePools.Add(new ActiveBrinePool
                    {
                        GameObject = poolObject,
                        HazardId = hazardId
                    });
                    created++;
                }

                if (created == 0)
                    GlobalTelemetryBus.PublishPerformanceWarning(EmptyBoundsWarningHash, BrineGeneratorContextHash, cellCount);

                return created;
            }
            finally
            {
                DisposeTracked(ref bounds);
            }
        }

        /// <summary>
        /// Removes spawned pool objects and unregisters their hazards.
        /// </summary>
        public void ClearBrinePools()
        {
            for (int i = 0; i < _activePools.Count; i++)
            {
                ActiveBrinePool pool = _activePools[i];
                HectonHazardManager.Unregister(pool.HazardId);
                if (pool.GameObject == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(pool.GameObject);
                else
                    DestroyImmediate(pool.GameObject);
            }

            _activePools.Clear();
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

            Transform existing = transform.Find("Generated Brine Pools");
            if (existing != null)
            {
                existing.gameObject.layer = ResolveBrinePhysicsLayer();
                _poolRoot = existing;
                return;
            }

            // COLD ALLOC: GameObject[1] — brine pool container — owner: HectonBrinePoolMeshGenerator
            var rootObject = new GameObject("Generated Brine Pools");
            rootObject.transform.SetParent(transform, false);
            rootObject.layer = ResolveBrinePhysicsLayer();
            _poolRoot = rootObject.transform;
        }

        private GameObject CreatePoolObject(
            AnomalyBrinePoolBounds poolBounds,
            float cellSizeMeters,
            Vector3 runtimeOrigin,
            out Vector3 runtimeCenter)
        {
            float minWorldX = poolBounds.MinX * cellSizeMeters;
            float maxWorldX = (poolBounds.MaxX + 1) * cellSizeMeters;
            float minWorldZ = poolBounds.MinZ * cellSizeMeters;
            float maxWorldZ = (poolBounds.MaxZ + 1) * cellSizeMeters;
            float sizeX = math.max(cellSizeMeters, maxWorldX - minWorldX);
            float sizeZ = math.max(cellSizeMeters, maxWorldZ - minWorldZ);
            Vector3 center = runtimeOrigin + new Vector3((minWorldX + maxWorldX) * 0.5f, poolBounds.LipHeight, (minWorldZ + maxWorldZ) * 0.5f);
            runtimeCenter = center;

            // COLD ALLOC: GameObject[1] — generated brine pool mesh and hazard — owner: HectonBrinePoolMeshGenerator
            var poolObject = new GameObject(BrinePoolObjectName);
            poolObject.transform.SetParent(_poolRoot, false);
            poolObject.transform.position = center;
            poolObject.transform.localScale = new Vector3(sizeX, 1f, sizeZ);
            poolObject.layer = ResolveBrinePhysicsLayer();

            MeshFilter meshFilter = poolObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = poolObject.AddComponent<MeshRenderer>();
            if (brineMaterial != null)
                meshRenderer.sharedMaterial = brineMaterial;

            Mesh poolMesh = EnsureSharedPoolMesh();
            meshFilter.sharedMesh = poolMesh;
            CreateFogVolume(poolObject.transform, poolMesh);

            BoxCollider collider = poolObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, -colliderDepthMeters * 0.5f, 0f);
            collider.size = new Vector3(1f, math.max(0.01f, colliderDepthMeters), 1f);
            poolObject.AddComponent<ToxinHazard>();
            return poolObject;
        }

        private void CreateFogVolume(Transform poolTransform, Mesh poolMesh)
        {
            if (brineFogMaterial == null || poolMesh == null)
                return;

            // COLD ALLOC: GameObject[1] — generated brine fog render proxy — owner: HectonBrinePoolMeshGenerator
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

            // COLD ALLOC: Mesh[1] — generated brine surface quad — owner: HectonBrinePoolMeshGenerator
            var mesh = new Mesh
            {
                name = "BrinePoolUnitQuadMesh"
            };

            // COLD ALLOC: Vector3[4] - one-time shared brine quad vertices - owner: HectonBrinePoolMeshGenerator
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f)
            };
            // COLD ALLOC: Vector2[4] - one-time shared brine quad uvs - owner: HectonBrinePoolMeshGenerator
            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            // COLD ALLOC: Vector3[4] - one-time shared brine quad normals - owner: HectonBrinePoolMeshGenerator
            Vector3[] normals =
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };
            // COLD ALLOC: int[6] - one-time shared brine quad indices - owner: HectonBrinePoolMeshGenerator
            int[] triangles = { 0, 2, 1, 1, 2, 3 };
            mesh.SetVertices(vertices);
            mesh.uv = uvs;
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
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

        private void RegisterBrineHazard(Vector3 runtimeCenter, AnomalyBrinePoolBounds poolBounds, float cellSizeMeters, int hazardId)
        {
            float sizeX = math.max(cellSizeMeters, (poolBounds.MaxX - poolBounds.MinX + 1) * cellSizeMeters);
            float sizeZ = math.max(cellSizeMeters, (poolBounds.MaxZ - poolBounds.MinZ + 1) * cellSizeMeters);
            float radius = math.sqrt(sizeX * sizeX + sizeZ * sizeZ) * 0.5f;
            HectonHazardManager.Register(hazardId, runtimeCenter, hazardIntensity, radius, HazardType.Toxicity, hazardVisorGlitchBias);
        }

        private static int ResolveBrinePhysicsLayer()
        {
            return BrineToxicityLayer >= 0 ? BrineToxicityLayer : HectonLayerMasks.TriggerZone;
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private struct ActiveBrinePool
        {
            public GameObject GameObject;
            public int HazardId;
        }
    }
}
