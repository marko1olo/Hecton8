using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using Hecton8.World;
using System.Runtime.InteropServices;

namespace Hecton8.Editor
{
    public static class ProceduralScatterRenderer
    {
        private static Material _caveGlowMaterial;
        
        private class ScatterBatch
        {
            public Mesh mesh;
            public Material material;
            public BatchRendererGroup brg;
            public BatchID batchId;
            public BatchMeshID meshId;
            public BatchMaterialID materialId;
            public GraphicsBuffer batchHandleBuffer;

            public GraphicsBuffer allInstancesBuffer;
            public GraphicsBuffer visibleInstancesBuffer;
            public GraphicsBuffer indirectArgsBuffer;
            
            public int totalCount;
            public int[] countReadback = new int[1];

            public void Release()
            {
                if (brg != null)
                {
                    if (batchId != default) brg.RemoveBatch(batchId);
                    if (meshId != default) brg.UnregisterMesh(meshId);
                    if (materialId != default) brg.UnregisterMaterial(materialId);
                    brg.Dispose();
                    brg = null;
                }
                if (batchHandleBuffer != null) { batchHandleBuffer.Release(); batchHandleBuffer = null; }
                if (allInstancesBuffer != null) { allInstancesBuffer.Release(); allInstancesBuffer = null; }
                if (visibleInstancesBuffer != null) { visibleInstancesBuffer.Release(); visibleInstancesBuffer = null; }
                if (indirectArgsBuffer != null) { indirectArgsBuffer.Release(); indirectArgsBuffer = null; }
            }
        }
        
        private static List<ScatterBatch> _batches = new List<ScatterBatch>();
        private static ComputeShader _cullShader;

        public static void Cleanup()
        {
            foreach (var b in _batches) b.Release();
            _batches.Clear();
        }

        public static Dictionary<string, Vector3> RepresentativeInstancesByPrefab = new Dictionary<string, Vector3>();

        public static void GenerateAndLogScatter(UnityEngine.Terrain[] terrains, string artifactDir = "Logs/")
        {
            RepresentativeInstancesByPrefab.Clear();
            Cleanup();

            if (_caveGlowMaterial == null)
            {
                _caveGlowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/MAT_CaveAnomaly.mat");
                if (_caveGlowMaterial == null)
                {
                    _caveGlowMaterial = new Material(Shader.Find("Hecton8/URP/ProceduralFlora"));
                    AssetDatabase.CreateAsset(_caveGlowMaterial, "Assets/_Project/Art/Materials/Terrain/MAT_CaveAnomaly.mat");
                }
                _caveGlowMaterial.shader = Shader.Find("Hecton8/URP/ProceduralFlora");
                var cAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/albedo___family.coral.brittle.png");
                var cNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/normal___family.coral.brittle.png");
                if (cAlbedo != null) _caveGlowMaterial.SetTexture("_BaseMap", cAlbedo);
                if (cNormal != null) _caveGlowMaterial.SetTexture("_NormalMap", cNormal);
                _caveGlowMaterial.SetColor("_BaseColor", new Color(0.1f, 0.5f, 1.0f));
                _caveGlowMaterial.SetFloat("_SwaySpeed", 1.0f);
                _caveGlowMaterial.SetFloat("_SwayAmount", 0.1f);
                _caveGlowMaterial.SetFloat("_HeightScale", 5.0f);
                _caveGlowMaterial.EnableKeyword("_ALPHATEST_ON");
                _caveGlowMaterial.renderQueue = 2450;
                _caveGlowMaterial.SetColor("_EmissionColor", new Color(0.0f, 1.0f, 1.0f) * 30.0f);
                _caveGlowMaterial.enableInstancing = true;
                EditorUtility.SetDirty(_caveGlowMaterial);
            }

            _cullShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/_Project/Art/Shaders/InstanceCulling.compute");

            string[] coralTokens = {
                "family_coral_brittle__lace", "family_coral_brittle__crown", "family_coral_brittle__thicket",
                "family_coral_brittle__halo", "family_coral_brittle__candelabra", "family_coral_brittle__wreath",
                "family_coral_brittle__cathedral"
            };
            List<GameObject> coralPrefabs = new List<GameObject>();
            foreach (var token in coralTokens) {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/GeneratedEcosystem/{token}_Prefab.prefab");
                if (p != null) coralPrefabs.Add(p);
            }

            string[] seaweedTokens = {
                "family_kelp_tall__stalk", "family_kelp_tall__lean", "family_kelp_tall__ribbon",
                "family_kelp_tall__lamina", "family_kelp_tall__rope", "family_kelp_tall__banner",
                "family_kelp_tall__lance"
            };
            List<GameObject> kelpPrefabs = new List<GameObject>();
            foreach (var token in seaweedTokens) {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/GeneratedEcosystem/{token}_Prefab.prefab");
                if (p != null) kelpPrefabs.Add(p);
            }

            GameObject ventPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_pocket_hazard__vent.prefab");

            Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>> instancesByMeshMat = new Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>>();

            void AddInstance(GameObject prefab, Vector3 pos, Quaternion rot, Vector3 scale)
            {
                if (prefab == null) return;

                if (!RepresentativeInstancesByPrefab.ContainsKey(prefab.name))
                    RepresentativeInstancesByPrefab[prefab.name] = pos;

                var mf = prefab.GetComponentInChildren<MeshFilter>();
                var mr = prefab.GetComponentInChildren<MeshRenderer>();
                if (mf == null || mr == null || mf.sharedMesh == null || mr.sharedMaterial == null) return;
                
                var mesh = mf.sharedMesh;
                var mat = mr.sharedMaterial;
                if (prefab.name.Contains("CaveAnomaly")) mat = _caveGlowMaterial;
                
                if (!instancesByMeshMat.ContainsKey(mesh)) instancesByMeshMat[mesh] = new Dictionary<Material, List<Matrix4x4>>();
                if (!instancesByMeshMat[mesh].ContainsKey(mat)) instancesByMeshMat[mesh][mat] = new List<Matrix4x4>();

                instancesByMeshMat[mesh][mat].Add(Matrix4x4.TRS(pos, rot, scale));
            }

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            int totalSamples = 0;

            foreach (var t in terrains)
            {
                if (t.terrainData == null) continue;

                int res = 512;
                
                

                int step = 3; 
                for (int y = 0; y < res; y += step)
                {
                    for (int x = 0; x < res; x += step)
                    {
                        totalSamples++;
                        float normX = x * 1.0f / (res - 1);
                        float normY = y * 1.0f / (res - 1);

                        Vector3 normal = t.terrainData.GetInterpolatedNormal(normX, normY);
                        float angle = Vector3.Angle(Vector3.up, normal);

                        float dominantWeight = 1.0f; int dominantLayer = 0;

                        Vector3 worldPos = t.transform.position + new Vector3(normX * t.terrainData.size.x, 0, normY * t.terrainData.size.z);
                        worldPos.y = t.transform.position.y + t.SampleHeight(worldPos);

                        if (worldPos.y < minY) minY = worldPos.y;
                        if (worldPos.y > maxY) maxY = worldPos.y;

                        float noise = Mathf.PerlinNoise(worldPos.x * 0.1f, worldPos.z * 0.1f);
                        float noise2 = Mathf.PerlinNoise(worldPos.x * 0.03f + 500f, worldPos.z * 0.03f + 500f);
                        float clusterNoise = Mathf.PerlinNoise(worldPos.x * 0.01f + 100f, worldPos.z * 0.01f + 100f);

                        // Cave anomalies
                        if (dominantWeight > 0.5f && noise < 0.3f && noise2 > 0.4f)
                        {
                            Vector3 pos = worldPos + new Vector3(0, UnityEngine.Random.Range(2f, 8f), 0);
                            Vector3 scale = new Vector3(UnityEngine.Random.Range(1.5f, 5.0f), UnityEngine.Random.Range(1.5f, 5.0f), UnityEngine.Random.Range(1.5f, 5.0f));
                            Quaternion rot = Quaternion.Euler(UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-15f, 15f));
                            if (coralPrefabs.Count > 0)
                            {
                                var pref = coralPrefabs[UnityEngine.Random.Range(0, coralPrefabs.Count)];
                                var fakePrefab = new GameObject("CaveAnomaly");
                                fakePrefab.AddComponent<MeshFilter>().sharedMesh = pref.GetComponentInChildren<MeshFilter>().sharedMesh;
                                fakePrefab.AddComponent<MeshRenderer>().sharedMaterial = _caveGlowMaterial;
                                AddInstance(fakePrefab, pos, rot, scale);
                                GameObject.DestroyImmediate(fakePrefab);
                            }
                            continue;
                        }

                        // Vents
                        if (noise > 0.7f && noise2 < 0.4f)
                        {
                            AddInstance(ventPrefab, worldPos, Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0), Vector3.one * UnityEngine.Random.Range(0.1f, 0.3f));
                            continue;
                        }

                        // KELP FOREST
                        if (kelpPrefabs.Count > 0 && angle < 18f && dominantLayer == 0 && clusterNoise > 0.15f)
                        {
                            var pref = kelpPrefabs[UnityEngine.Random.Range(0, kelpPrefabs.Count)];
                            Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, normal);
                            AddInstance(pref, worldPos, slopeRot * Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0), Vector3.one * UnityEngine.Random.Range(0.1f, 0.3f));
                            continue;
                        }

                        // CORAL REEFS
                        if (coralPrefabs.Count > 0 && angle > 25f && clusterNoise < 0.6f && noise2 > 0.3f)
                        {
                            var pref = coralPrefabs[UnityEngine.Random.Range(0, coralPrefabs.Count)];
                            Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, normal);
                            AddInstance(pref, worldPos, slopeRot * Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0), Vector3.one * UnityEngine.Random.Range(0.1f, 0.3f));
                            continue;
                        }
                    }
                }
            }

            int totalSpawned = 0;
            foreach (var kvp in instancesByMeshMat)
            {
                foreach (var matKvp in kvp.Value)
                {
                    totalSpawned += matKvp.Value.Count;
                    CreateBatch(kvp.Key, matKvp.Key, matKvp.Value);
                }
            }

            Debug.Log($"[SCATTER VALIDATION] Terrain Height Range: {minY:F1} to {maxY:F1}");
            System.IO.File.WriteAllText(artifactDir + "RenderLog.txt", $"[SCATTER] Total BRG Instances: {totalSpawned}\n");
            
            if (totalSpawned == 0) Debug.LogError("[SCATTER VALIDATION] FAILED! Zero objects spawned.");
            else Debug.Log($"[SCATTER VALIDATION] SUCCESS! Total spawned BRG instances: {totalSpawned}");

            // Hook rendering
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        public static Dictionary<string, Vector3> RepresentativeInstances = new Dictionary<string, Vector3>();

        private static void CreateBatch(Mesh mesh, Material material, List<Matrix4x4> instances)
        {
            if (instances.Count == 0) return;

            if (!RepresentativeInstances.ContainsKey(material.name))
                RepresentativeInstances[material.name] = instances[0].GetPosition();

            ScatterBatch b = new ScatterBatch();
            b.mesh = mesh;
            b.material = material;
            b.totalCount = instances.Count;
            
            b.brg = new BatchRendererGroup(OnPerformCulling, System.IntPtr.Zero);
            b.meshId = b.brg.RegisterMesh(mesh);
            b.materialId = b.brg.RegisterMaterial(material);

            b.allInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, instances.Count, 64);
            b.allInstancesBuffer.SetData(instances);

            b.visibleInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured, instances.Count, 64);
            b.indirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
            b.batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer();

            var metadata = new NativeArray<MetadataValue>(1, Allocator.Temp);
            metadata[0] = new MetadataValue
            {
                NameID = Shader.PropertyToID("unity_ObjectToWorld"),
                Value = 0x80000000 // BRG flag to read from buffer
            };
            b.batchId = b.brg.AddBatch(metadata, b.batchHandleBuffer.bufferHandle);
            metadata.Dispose();

            b.brg.SetBatchBuffer(b.batchId, b.visibleInstancesBuffer.bufferHandle);
            
            // Set unbounded bounds for simplicity in test
            b.brg.SetGlobalBounds(new Bounds(Vector3.zero, new Vector3(100000, 100000, 100000)));

            _batches.Add(b);
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (_batches.Count == 0 || _cullShader == null) return;

            int kernel = _cullShader.FindKernel("CullInstances");
            
            GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);

            Texture3D voxelSdf = null;
            Vector3 voxelSdfOrigin = Vector3.zero;
            Vector3 voxelSdfInvSize = Vector3.one;

            if (HectonCaveVoxelLightingVolume.ActiveRuntimeInstance != null && 
                HectonCaveVoxelLightingVolume.ActiveRuntimeInstance.TryGetPublishedGpuSdfPayload(out var tex, out var w2l, out var hr, out var inv))
            {
                voxelSdf = tex;
                voxelSdfOrigin = w2l.inverse.MultiplyPoint3x4(Vector3.zero) - new Vector3(hr.x, hr.y, hr.z);
                voxelSdfInvSize = new Vector3(inv.x, inv.y, inv.z) * 0.5f; 
            }
            else
            {
                // Dummy fallback
                voxelSdf = new Texture3D(1, 1, 1, TextureFormat.R8, false);
                voxelSdf.SetPixelData(new byte[] { 255 }, 0);
                voxelSdf.Apply();
            }

            CommandBuffer cmd = CommandBufferPool.Get("Scatter BRG Culling");
            
            foreach (var b in _batches)
            {
                cmd.SetBufferCounterValue(b.visibleInstancesBuffer, 0);

                cmd.SetComputeBufferParam(_cullShader, kernel, "_HectonAllInstances", b.allInstancesBuffer);
                cmd.SetComputeBufferParam(_cullShader, kernel, "_HectonVisibleInstances", b.visibleInstancesBuffer);
                
                cmd.SetComputeIntParam(_cullShader, "_HectonInstanceCount", b.totalCount);
                cmd.SetComputeVectorParam(_cullShader, "_HectonCameraPosition", cam.transform.position);
                cmd.SetComputeVectorParam(_cullShader, "_HectonCameraForward", cam.transform.forward);
                
                cmd.SetComputeVectorParam(_cullShader, "_HectonFrustumPlane0", new Vector4(_frustumPlanes[0].normal.x, _frustumPlanes[0].normal.y, _frustumPlanes[0].normal.z, _frustumPlanes[0].distance));
                cmd.SetComputeVectorParam(_cullShader, "_HectonFrustumPlane1", new Vector4(_frustumPlanes[1].normal.x, _frustumPlanes[1].normal.y, _frustumPlanes[1].normal.z, _frustumPlanes[1].distance));
                cmd.SetComputeVectorParam(_cullShader, "_HectonFrustumPlane2", new Vector4(_frustumPlanes[2].normal.x, _frustumPlanes[2].normal.y, _frustumPlanes[2].normal.z, _frustumPlanes[2].distance));
                cmd.SetComputeVectorParam(_cullShader, "_HectonFrustumPlane3", new Vector4(_frustumPlanes[3].normal.x, _frustumPlanes[3].normal.y, _frustumPlanes[3].normal.z, _frustumPlanes[3].distance));
                cmd.SetComputeVectorParam(_cullShader, "_HectonFrustumPlane4", new Vector4(_frustumPlanes[4].normal.x, _frustumPlanes[4].normal.y, _frustumPlanes[4].normal.z, _frustumPlanes[4].distance));
                cmd.SetComputeVectorParam(_cullShader, "_HectonFrustumPlane5", new Vector4(_frustumPlanes[5].normal.x, _frustumPlanes[5].normal.y, _frustumPlanes[5].normal.z, _frustumPlanes[5].distance));
                
                cmd.SetComputeFloatParam(_cullShader, "_HectonBoundsRadius", 20.0f);
                cmd.SetComputeFloatParam(_cullShader, "_HectonCullDistanceMeters", 5000.0f);
                cmd.SetComputeIntParam(_cullShader, "_HectonCullingFlags", 1); // 1 = HECTON_FLAG_VOXEL_SDF_CULL
                
                cmd.SetComputeTextureParam(_cullShader, kernel, "_HectonVoxelSdfTexture3D", voxelSdf);
                cmd.SetComputeVectorParam(_cullShader, "_HectonVoxelSdfOrigin", voxelSdfOrigin);
                cmd.SetComputeVectorParam(_cullShader, "_HectonVoxelSdfInvSize", voxelSdfInvSize);
                
                int groups = Mathf.CeilToInt(b.totalCount / 64.0f);
                cmd.DispatchCompute(_cullShader, kernel, groups, 1, 1);
                
                // Copy count to args
                cmd.CopyCounterValue(b.visibleInstancesBuffer, b.indirectArgsBuffer, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            context.Submit(); // We MUST submit compute to GPU immediately so we can read back synchronously

            // Synchronous readback
            foreach (var b in _batches)
            {
                b.indirectArgsBuffer.GetData(b.countReadback, 0, 0, 1);
            }
            
            if (voxelSdf.width == 1) GameObject.DestroyImmediate(voxelSdf);
        }

        private static Plane[] _frustumPlanes = new Plane[6];

        private static Unity.Jobs.JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            System.IntPtr userContext)
        {
            // Find which batch corresponds to this BRG
            ScatterBatch b = null;
            foreach (var batch in _batches)
            {
                if (batch.brg == rendererGroup)
                {
                    b = batch;
                    break;
                }
            }

            if (b == null || b.countReadback[0] == 0)
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(cullingOutput, HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            HectonBatchRendererGroupUtility.WriteAllVisibleSingleDrawOutput(
                cullingOutput,
                b.countReadback[0],
                b.batchId,
                b.meshId,
                b.materialId,
                0, // Default layer
                0, // submesh 0
                ShadowCastingMode.Off,
                false,
                MotionVectorGenerationMode.Camera);

            return default;
        }
    }
}
