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
        private const uint BrineGeneratorContextHash = 0x414E4252u;

        [Header("Rendering")]
        [SerializeField] private Material brineMaterial;

        [Header("Hazard")]
        [SerializeField] private float colliderDepthMeters = 4f;
        [SerializeField] private float hazardIntensity = 1f;
        [SerializeField] private float hazardVisorGlitchBias = 1f;
        [SerializeField] private int hazardIdBase = 870000;

        // COLD ALLOC: List<ActiveBrinePool>[32] — spawned brine pool bookkeeping — owner: HectonBrinePoolMeshGenerator
        private readonly List<ActiveBrinePool> _activePools = new List<ActiveBrinePool>(32);

        private Transform _poolRoot;

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

                    GameObject poolObject = CreatePoolObject(poolBounds, safeCellSize, runtimeOrigin);
                    int hazardId = hazardIdBase + poolBounds.BasinId;
                    RegisterBrineHazard(poolObject.transform.position, poolBounds, safeCellSize, hazardId);

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
        }

        private void EnsureRoot()
        {
            if (_poolRoot != null)
                return;

            Transform existing = transform.Find("Generated Brine Pools");
            if (existing != null)
            {
                _poolRoot = existing;
                return;
            }

            // COLD ALLOC: GameObject[1] — brine pool container — owner: HectonBrinePoolMeshGenerator
            var rootObject = new GameObject("Generated Brine Pools");
            rootObject.transform.SetParent(transform, false);
            _poolRoot = rootObject.transform;
        }

        private GameObject CreatePoolObject(
            AnomalyBrinePoolBounds poolBounds,
            float cellSizeMeters,
            Vector3 runtimeOrigin)
        {
            float minWorldX = poolBounds.MinX * cellSizeMeters;
            float maxWorldX = (poolBounds.MaxX + 1) * cellSizeMeters;
            float minWorldZ = poolBounds.MinZ * cellSizeMeters;
            float maxWorldZ = (poolBounds.MaxZ + 1) * cellSizeMeters;
            float sizeX = math.max(cellSizeMeters, maxWorldX - minWorldX);
            float sizeZ = math.max(cellSizeMeters, maxWorldZ - minWorldZ);
            Vector3 center = runtimeOrigin + new Vector3((minWorldX + maxWorldX) * 0.5f, poolBounds.LipHeight, (minWorldZ + maxWorldZ) * 0.5f);

            // COLD ALLOC: GameObject[1] — generated brine pool mesh and hazard — owner: HectonBrinePoolMeshGenerator
            var poolObject = new GameObject($"BrinePool_{poolBounds.BasinId:000}");
            poolObject.transform.SetParent(_poolRoot, false);
            poolObject.transform.position = center;

            MeshFilter meshFilter = poolObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = poolObject.AddComponent<MeshRenderer>();
            if (brineMaterial != null)
                meshRenderer.sharedMaterial = brineMaterial;

            meshFilter.sharedMesh = CreateFlatPoolMesh(sizeX, sizeZ, poolBounds.MaskedCount);

            BoxCollider collider = poolObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, -colliderDepthMeters * 0.5f, 0f);
            collider.size = new Vector3(sizeX, math.max(0.01f, colliderDepthMeters), sizeZ);
            poolObject.AddComponent<ToxinHazard>();
            return poolObject;
        }

        private Mesh CreateFlatPoolMesh(float sizeX, float sizeZ, int maskedCount)
        {
            // COLD ALLOC: Mesh[1] — generated brine surface quad — owner: HectonBrinePoolMeshGenerator
            var mesh = new Mesh
            {
                name = $"BrinePoolMesh_{maskedCount}"
            };

            float halfX = sizeX * 0.5f;
            float halfZ = sizeZ * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-halfX, 0f, -halfZ),
                new Vector3(halfX, 0f, -halfZ),
                new Vector3(-halfX, 0f, halfZ),
                new Vector3(halfX, 0f, halfZ)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.normals = new[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void RegisterBrineHazard(Vector3 runtimeCenter, AnomalyBrinePoolBounds poolBounds, float cellSizeMeters, int hazardId)
        {
            float sizeX = math.max(cellSizeMeters, (poolBounds.MaxX - poolBounds.MinX + 1) * cellSizeMeters);
            float sizeZ = math.max(cellSizeMeters, (poolBounds.MaxZ - poolBounds.MinZ + 1) * cellSizeMeters);
            float radius = math.sqrt(sizeX * sizeX + sizeZ * sizeZ) * 0.5f;
            HectonHazardManager.Register(hazardId, runtimeCenter, hazardIntensity, radius, HazardType.Toxicity, hazardVisorGlitchBias);
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
