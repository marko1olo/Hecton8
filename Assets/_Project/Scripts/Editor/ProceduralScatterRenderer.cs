using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Hecton8.Editor
{
    public static class ProceduralScatterRenderer
    {
        private static Mesh _stalactiteMesh;
        private static Material _caveGlowMaterial;
        
        public static void GenerateAndLogScatter(UnityEngine.Terrain[] terrains)
        {
            int kelpCount = 0;
            int coralCount = 0;
            int ventCount = 0;
            int caveAnomaliesCount = 0;

            if (_stalactiteMesh == null)
            {
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _stalactiteMesh = cyl.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(cyl);
            }
            if (_caveGlowMaterial == null)
            {
                _caveGlowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _caveGlowMaterial.SetColor("_BaseColor", new Color(0.1f, 0.5f, 1.0f));
                _caveGlowMaterial.SetColor("_EmissionColor", new Color(0.2f, 1.0f, 2.0f) * 3f);
                _caveGlowMaterial.EnableKeyword("_EMISSION");
                _caveGlowMaterial.enableInstancing = true;
            }

            Shader floraShader = Shader.Find("Hecton8/URP/ProceduralFlora");
            Material kelpMat = null;
            Material coralMat = null;
            if (floraShader != null)
            {
                kelpMat = new Material(floraShader);
                kelpMat.SetColor("_BaseColor", new Color(0.1f, 0.4f, 0.1f));
                kelpMat.SetColor("_TipColor", new Color(0.3f, 0.8f, 0.2f));
                kelpMat.SetFloat("_HeightScale", 10f);
                kelpMat.enableInstancing = true;

                coralMat = new Material(floraShader);
                coralMat.SetColor("_BaseColor", new Color(0.8f, 0.2f, 0.4f));
                coralMat.SetColor("_TipColor", new Color(1.0f, 0.5f, 0.8f));
                coralMat.SetColor("_EmissionColor", new Color(0.5f, 0.1f, 0.3f));
                coralMat.SetFloat("_HeightScale", 5f);
                coralMat.enableInstancing = true;
            }

            GameObject kelpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_kelp_patch_dense.prefab");
            GameObject coralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_coral_branching.prefab");
            GameObject ventPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_pocket_hazard__vent.prefab");

            if (kelpPrefab == null) Debug.LogWarning("[SCATTER] Kelp prefab not found!");
            if (coralPrefab == null) Debug.LogWarning("[SCATTER] Coral prefab not found!");
            if (ventPrefab == null) Debug.LogWarning("[SCATTER] Vent prefab not found!");

            GameObject scatterRoot = new GameObject("TRT_ScatterRoot");

            float minY = float.MaxValue;
            float maxY = float.MinValue;

            List<Matrix4x4> anomalyInstances = new List<Matrix4x4>();

            // Diagnostics: track weight distributions per layer
            int totalSamples = 0;
            float[] layerWeightSum = null;
            float[] layerWeightMax = null;
            int layerCount = 0;

            foreach (var t in terrains)
            {
                if (t.terrainData == null || t.terrainData.alphamapTextureCount == 0)
                {
                    Debug.Log($"[SCATTER DIAG] Terrain '{t.name}' skipped: no alphamap data");
                    continue;
                }

                int res = t.terrainData.alphamapResolution;
                float[,,] alphaMaps = t.terrainData.GetAlphamaps(0, 0, res, res);
                int layers = t.terrainData.alphamapLayers;

                // Log terrain layer names once
                if (layerCount == 0)
                {
                    layerCount = layers;
                    layerWeightSum = new float[layers];
                    layerWeightMax = new float[layers];
                    Debug.Log($"[SCATTER DIAG] Terrain has {layers} splatmap layers, alphamapRes={res}");
                    var terrainLayers = t.terrainData.terrainLayers;
                    for (int i = 0; i < Mathf.Min(layers, terrainLayers != null ? terrainLayers.Length : 0); i++)
                    {
                        string layerName = terrainLayers[i] != null ? terrainLayers[i].name : "null";
                        Debug.Log($"[SCATTER DIAG] Layer[{i}] = '{layerName}'");
                    }
                }

                int step = 16; 
                for (int y = 0; y < res; y += step)
                {
                    for (int x = 0; x < res; x += step)
                    {
                        totalSamples++;
                        float normX = x * 1.0f / (res - 1);
                        float normY = y * 1.0f / (res - 1);

                        // Track all layer weights for diagnostics
                        for (int l = 0; l < layers && l < layerWeightSum.Length; l++)
                        {
                            float w = alphaMaps[y, x, l];
                            layerWeightSum[l] += w;
                            if (w > layerWeightMax[l]) layerWeightMax[l] = w;
                        }

                        // Find dominant layer and its weight
                        int dominantLayer = 0;
                        float dominantWeight = 0f;
                        for (int l = 0; l < layers; l++)
                        {
                            float w = alphaMaps[y, x, l];
                            if (w > dominantWeight)
                            {
                                dominantWeight = w;
                                dominantLayer = l;
                            }
                        }

                        Vector3 worldPos = t.transform.position + new Vector3(normX * t.terrainData.size.x, 0, normY * t.terrainData.size.z);
                        worldPos.y = t.transform.position.y + t.SampleHeight(worldPos);

                        if (worldPos.y < minY) minY = worldPos.y;
                        if (worldPos.y > maxY) maxY = worldPos.y;

                        float noise = Mathf.PerlinNoise(worldPos.x * 0.1f, worldPos.z * 0.1f);
                        float noise2 = Mathf.PerlinNoise(worldPos.x * 0.03f + 500f, worldPos.z * 0.03f + 500f);

                        // Spawn logic based on dominant layer + noise, not hardcoded layer indices
                        // Cave anomalies: low noise regions with high dominant weight (GPU instanced)
                        if (dominantWeight > 0.5f && noise < 0.15f && noise2 > 0.6f)
                        {
                            Vector3 pos = worldPos + new Vector3(0, 5f, 0);
                            Vector3 scale = new Vector3(
                                Random.Range(1.5f, 3f),
                                Random.Range(6f, 14f),
                                Random.Range(1.5f, 3f));
                            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f)), scale);
                            anomalyInstances.Add(matrix);
                            caveAnomaliesCount++;
                            continue;
                        }

                        // Vents: high noise, any dominant layer
                        if (noise > 0.82f && noise2 < 0.3f)
                        {
                            if (ventPrefab != null)
                            {
                                var go = (GameObject)PrefabUtility.InstantiatePrefab(ventPrefab);
                                go.transform.position = worldPos;
                                go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                                go.transform.localScale = Vector3.one * Random.Range(0.8f, 1.5f);
                                go.transform.SetParent(scatterRoot.transform);
                                ventCount++;
                            }
                            continue;
                        }

                        // Kelp: mid-noise band
                        if (noise > 0.5f && noise < 0.65f && dominantWeight > 0.4f)
                        {
                            if (kelpPrefab != null)
                            {
                                var go = (GameObject)PrefabUtility.InstantiatePrefab(kelpPrefab);
                                go.transform.position = worldPos;
                                go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                                go.transform.localScale = Vector3.one * Random.Range(3.0f, 8.0f);
                                if (kelpMat != null)
                                {
                                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
                                        mr.sharedMaterial = kelpMat;
                                }
                                go.transform.SetParent(scatterRoot.transform);
                                kelpCount++;
                            }
                            continue;
                        }

                        // Coral: different noise band
                        if (noise > 0.3f && noise < 0.5f && dominantWeight > 0.3f && noise2 > 0.4f)
                        {
                            if (coralPrefab != null)
                            {
                                var go = (GameObject)PrefabUtility.InstantiatePrefab(coralPrefab);
                                go.transform.position = worldPos;
                                go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                                go.transform.localScale = Vector3.one * Random.Range(2.0f, 6.0f);
                                if (coralMat != null)
                                {
                                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
                                        mr.sharedMaterial = coralMat;
                                }
                                go.transform.SetParent(scatterRoot.transform);
                                coralCount++;
                            }
                            continue;
                        }
                    }
                }
            }

            if (anomalyInstances.Count > 0)
            {
                var instancer = scatterRoot.AddComponent<Hecton8.World.CaveAnomalyInstancedRenderer>();
                instancer.mesh = _stalactiteMesh;
                instancer.material = _caveGlowMaterial;
                instancer.SetInstances(anomalyInstances);
            }

            // Diagnostics output
            Debug.Log($"[SCATTER VALIDATION] Terrain Height Range: {minY:F1} to {maxY:F1}");
            Debug.Log($"[SCATTER VALIDATION] Total samples: {totalSamples}");
            if (layerWeightSum != null)
            {
                for (int l = 0; l < layerCount; l++)
                {
                    float avg = totalSamples > 0 ? layerWeightSum[l] / totalSamples : 0f;
                    Debug.Log($"[SCATTER DIAG] Layer[{l}]: avgWeight={avg:F4}, maxWeight={layerWeightMax[l]:F4}");
                }
            }
            Debug.Log($"[SCATTER VALIDATION] Kelp count: {kelpCount}, Coral count: {coralCount}, Vents count: {ventCount}, Cave Anomalies count: {caveAnomaliesCount}");

            int totalSpawned = kelpCount + coralCount + ventCount + caveAnomaliesCount;
            if (totalSpawned == 0)
            {
                Debug.LogError("[SCATTER VALIDATION] FAILED! Zero objects spawned.");
            }
            else
            {
                Debug.Log($"[SCATTER VALIDATION] SUCCESS! Total spawned: {totalSpawned}");
            }
        }
    }
}
