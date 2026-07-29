using UnityEngine;
using UnityEditor;
using System.IO;
using Hecton8.Editor;
using System.Collections.Generic;

namespace Hecton8.Editor
{
    public static class BakeEcosystemMeshes
    {
        [MenuItem("Hecton8/Bake Ecosystem Meshes")]
        public static void BakeAll()
        {
            TryBakeAll();
        }

        public static bool TryBakeAll()
        {
            string exportDir = "Assets/_Project/Prefabs/GeneratedEcosystem";
            if (!AssetDatabase.IsValidFolder(exportDir))
            {
                Directory.CreateDirectory(exportDir);
                // A folder made behind the AssetDatabase's back is not importable until refreshed,
                // and CreateAsset into it silently fails on a first run.
                AssetDatabase.Refresh();
            }

            Material coralMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/MAT_ProceduralCoral.mat");
            if (coralMat == null)
            {
                coralMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(coralMat, "Assets/_Project/Art/Materials/Terrain/MAT_ProceduralCoral.mat");
            }
            coralMat.shader = Shader.Find("Universal Render Pipeline/Lit");
            coralMat.enableInstancing = true;
            coralMat.SetColor("_BaseColor", new Color(0.6f, 0.2f, 0.4f, 1)); // Deep red/purple
            
            // Setup Alpha Test
            coralMat.SetFloat("_AlphaClip", 1.0f);
            coralMat.EnableKeyword("_ALPHATEST_ON");
            coralMat.renderQueue = 2450;
            coralMat.SetColor("_EmissionColor", new Color(0.0f, 0.0f, 0.0f, 1)); 
            
            var cColor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/color___family.coral.brittle.png");
            if (cColor != null) coralMat.SetTexture("_BaseMap", cColor);
            
            var cNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/normal___family.coral.brittle.png");
            if (cNormal != null) coralMat.SetTexture("_BumpMap", cNormal);

            Material kelpMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/MAT_ProceduralKelp.mat");
            if (kelpMat == null)
            {
                kelpMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(kelpMat, "Assets/_Project/Art/Materials/Terrain/MAT_ProceduralKelp.mat");
            }
            kelpMat.shader = Shader.Find("Universal Render Pipeline/Lit");
            kelpMat.enableInstancing = true;
            kelpMat.SetColor("_BaseColor", new Color(0.2f, 0.4f, 0.6f, 1)); // Deep ocean blue-green
            
            kelpMat.SetFloat("_AlphaClip", 1.0f);
            kelpMat.EnableKeyword("_ALPHATEST_ON");
            kelpMat.renderQueue = 2450;
            kelpMat.SetColor("_EmissionColor", new Color(0.0f, 0.0f, 0.0f, 1)); 
            
            var kColor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall/color___family.kelp.tall.png");
            if (kColor != null) kelpMat.SetTexture("_BaseMap", kColor);
            
            var kNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall/normal___family.kelp.tall.png");
            if (kNormal != null) kelpMat.SetTexture("_BumpMap", kNormal);
            // Patch: Mark materials dirty so they save to disk
            EditorUtility.SetDirty(coralMat);
            EditorUtility.SetDirty(kelpMat);
            AssetDatabase.SaveAssets();

            string[] coralTokens = {
                "family_coral_brittle__lace", "family_coral_brittle__crown", "family_coral_brittle__thicket",
                "family_coral_brittle__halo", "family_coral_brittle__candelabra", "family_coral_brittle__wreath",
                "family_coral_brittle__cathedral"
            };

            List<GameObject> bakedCorals = new List<GameObject>();
            foreach (var token in coralTokens)
            {
                if (WorldProceduralCoralMeshBuilder.TryBuild(token, new Vector3(3, 3, 3), 0, out Mesh mesh))
                {
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                    if (mesh.vertexCount == 0)
                    {
                        Debug.LogError($"[BAKE] Coral {token} built an EMPTY mesh (0 vertices). Not saving a hollow prefab.");
                        Object.DestroyImmediate(mesh);
                        continue;
                    }
                    Debug.Log($"[BAKE] Coral {token} vertexCount: {mesh.vertexCount}");
                    GameObject baked = SavePrefab(mesh, token, coralMat, exportDir);
                    if (baked != null) bakedCorals.Add(baked);
                }
                else
                {
                    Debug.LogError($"[BAKE] WorldProceduralCoralMeshBuilder.TryBuild returned false for token '{token}'. Variant spec missing.");
                }
            }

            string[] seaweedTokens = {
                "family_kelp_tall__stalk", "family_kelp_tall__lean", "family_kelp_tall__ribbon",
                "family_kelp_tall__lamina", "family_kelp_tall__rope", "family_kelp_tall__banner",
                "family_kelp_tall__lance"
            };

            List<GameObject> bakedKelp = new List<GameObject>();
            foreach (var token in seaweedTokens)
            {
                if (WorldProceduralSeaweedMeshBuilder.TryBuild(token, new Vector3(2, 6, 2), 0, out Mesh mesh))
                {
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                    if (mesh.vertexCount == 0)
                    {
                        Debug.LogError($"[BAKE] Kelp {token} built an EMPTY mesh (0 vertices). Not saving a hollow prefab.");
                        Object.DestroyImmediate(mesh);
                        continue;
                    }
                    Debug.Log($"[BAKE] Kelp {token} vertexCount: {mesh.vertexCount}");
                    GameObject baked = SavePrefab(mesh, token, kelpMat, exportDir);
                    if (baked != null) bakedKelp.Add(baked);
                }
                else
                {
                    Debug.LogError($"[BAKE] WorldProceduralSeaweedMeshBuilder.TryBuild returned false for token '{token}'. Variant spec missing.");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool complete = bakedCorals.Count == coralTokens.Length && bakedKelp.Count == seaweedTokens.Length;
            if (complete)
            {
                Debug.Log($"[BAKE] Baked {bakedCorals.Count}/{coralTokens.Length} corals and {bakedKelp.Count}/{seaweedTokens.Length} kelps into {exportDir}.");
            }
            else
            {
                Debug.LogError($"[BAKE] INCOMPLETE: baked {bakedCorals.Count}/{coralTokens.Length} corals and " +
                               $"{bakedKelp.Count}/{seaweedTokens.Length} kelps. The GeneratedEcosystem set is partial; " +
                               "any consumer indexing the full token list will miss prefabs.");
            }

            return complete;
        }

        private static GameObject SavePrefab(Mesh mesh, string token, Material mat, string exportDir)
        {
            string meshPath = $"{exportDir}/{token}_Mesh.asset";
            AssetDatabase.CreateAsset(mesh, meshPath);

            GameObject go = new GameObject(token);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            string prefabPath = $"{exportDir}/{token}_Prefab.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out bool saved);
            Object.DestroyImmediate(go);

            if (!saved || prefab == null)
            {
                Debug.LogError($"[BAKE] PrefabUtility.SaveAsPrefabAsset failed for '{prefabPath}'. " +
                               "Verify the export folder is imported and writable.");
                return null;
            }

            return prefab;
        }

        /// <summary>
        /// Batchmode entry point. Exits non-zero when the token set did not bake completely, so a
        /// headless run cannot report success over a partial GeneratedEcosystem folder.
        /// Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod Hecton8.Editor.BakeEcosystemMeshes.ExecuteBatch
        /// </summary>
        public static void ExecuteBatch()
        {
            bool ok = TryBakeAll();
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
