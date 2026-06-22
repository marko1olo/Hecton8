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

            GameObject kelpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Ecosystem/Kelp/PFB_KelpForest_VariantA.prefab");
            GameObject coralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Ecosystem/Corals/PFB_CoralShelf_VariantA.prefab");
            GameObject ventPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Ecosystem/Abyss/PFB_AbyssVent_VariantA.prefab");

            if (kelpPrefab == null) Debug.LogWarning("[SCATTER] Kelp prefab not found!");
            if (coralPrefab == null) Debug.LogWarning("[SCATTER] Coral prefab not found!");
            if (ventPrefab == null) Debug.LogWarning("[SCATTER] Vent prefab not found!");

            GameObject scatterRoot = new GameObject("TRT_ScatterRoot");

            foreach (var t in terrains)
            {
                if (t.terrainData == null || t.terrainData.alphamapTextureCount == 0) continue;

                int res = t.terrainData.alphamapResolution;
                float[,,] alphaMaps = t.terrainData.GetAlphamaps(0, 0, res, res);
                int layers = t.terrainData.alphamapLayers;

                int step = 16; 
                for (int y = 0; y < res; y += step)
                {
                    for (int x = 0; x < res; x += step)
                    {
                        float normX = x * 1.0f / (res - 1);
                        float normY = y * 1.0f / (res - 1);

                        float rockWeight = layers > 0 ? alphaMaps[y, x, 0] : 0f;
                        float sandWeight = layers > 1 ? alphaMaps[y, x, 1] : 0f;
                        float mudWeight  = layers > 2 ? alphaMaps[y, x, 2] : 0f;

                        Vector3 worldPos = t.transform.position + new Vector3(normX * t.terrainData.size.x, 0, normY * t.terrainData.size.z);
                        worldPos.y = t.transform.position.y + t.SampleHeight(worldPos);

                        float noise = Mathf.PerlinNoise(worldPos.x * 0.1f, worldPos.z * 0.1f);

                        // Fallbacks based on arbitrary heights and weights
                        if (worldPos.y < -200f && rockWeight > 0.6f && noise < 0.2f)
                        {
                            var go = new GameObject("CaveAnomaly");
                            go.transform.position = worldPos + new Vector3(0, 5f, 0);
                            go.transform.localScale = new Vector3(2f, 10f, 2f);
                            go.transform.SetParent(scatterRoot.transform);
                            var mf = go.AddComponent<MeshFilter>();
                            var mr = go.AddComponent<MeshRenderer>();
                            mf.sharedMesh = _stalactiteMesh;
                            mr.sharedMaterial = _caveGlowMaterial;
                            caveAnomaliesCount++;
                            continue;
                        }

                        if (worldPos.y < -150f && noise > 0.8f)
                        {
                            if (ventPrefab != null)
                            {
                                var go = (GameObject)PrefabUtility.InstantiatePrefab(ventPrefab);
                                go.transform.position = worldPos;
                                go.transform.SetParent(scatterRoot.transform);
                                ventCount++;
                            }
                            continue;
                        }

                        if (worldPos.y > -100f && sandWeight > 0.4f && noise > 0.5f)
                        {
                            if (kelpPrefab != null)
                            {
                                var go = (GameObject)PrefabUtility.InstantiatePrefab(kelpPrefab);
                                go.transform.position = worldPos;
                                go.transform.SetParent(scatterRoot.transform);
                                kelpCount++;
                            }
                            continue;
                        }

                        if (rockWeight > 0.5f && noise > 0.4f && noise < 0.7f)
                        {
                            if (coralPrefab != null)
                            {
                                var go = (GameObject)PrefabUtility.InstantiatePrefab(coralPrefab);
                                go.transform.position = worldPos;
                                go.transform.SetParent(scatterRoot.transform);
                                coralCount++;
                            }
                            continue;
                        }
                    }
                }
            }

            Debug.Log($"[SCATTER VALIDATION] Kelp count: {kelpCount}, Coral count: {coralCount}, Vents count: {ventCount}, Cave Anomalies count: {caveAnomaliesCount}");

            if (kelpCount == 0 && coralCount == 0 && ventCount == 0 && caveAnomaliesCount == 0)
            {
                Debug.LogError("[SCATTER VALIDATION] FAILED! Zero objects spawned.");
            }
        }
    }
}
