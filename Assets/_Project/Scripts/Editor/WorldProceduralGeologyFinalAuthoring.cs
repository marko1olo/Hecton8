// ============================================================================
// HECTON-8 — WorldProceduralGeologyFinalAuthoring.cs
// Editor authoring: generiruet production prefabs dlya vsey osnovnoy geologii.
//
// NOVYY PUT (v2):
//   Vmesto ruchnogo kitbash iz Forest_Rock_Shelf / Nordic_Beach_Rock —
//   vyzyvaet WorldGenerativeGeologyMeshBuilder, sohranyaet mesh assets,
//   sobiraet prefab s LODGroup i triplanarnym materialom.
//
// STARYE PREFAB PATHS NE MENYaYuTSYa — family assets prodolzhayut ssylatsya
// na te zhe puti, no vnutri teper realnaya protsedurnaya geometriya.
//
// Menyu: Hecton/Authoring/Rebuild Procedural Geology Finals
// ============================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Hecton8.Editor.ColliderOptimization1716;
using Hecton8.World;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralGeologyFinalAuthoring
    {
        private const string FinalPrefabFolder  = "Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals";
        private const string MeshOutputFolder   = "Assets/_Project/Art/Meshes/WorldProceduralGeology";
        private const string RockMaterialName   = "mat_Rock_Shared";
        private static readonly string[] RockMaterialSearchFolders = { "Assets/_Project/Art/Models/Rocks" };

        // ── Menyu ─────────────────────────────────────────────────

        [MenuItem("Hecton/Authoring/Rebuild Procedural Geology Finals", priority = 179)]
        public static void RebuildGeologyFinals()
        {
            EnsureFolder(FinalPrefabFolder);
            EnsureFolder(MeshOutputFolder);

            Material mat = ResolveGeologyRockMaterial();
            if (mat == null)
            {
                Debug.LogError($"[GeologyFinalAuthoring] Required material is missing: {RockMaterialName}");
                return;
            }

            if (!mat.enableInstancing)
            {
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }

            int built = 0;

            // ── 10 variantov melkih kamney ────────────────────────
            for (int i = 0; i < 10; i++)
            {
                string prefabPath = $"{FinalPrefabFolder}/PFB_Geo_RockFloor_{i:D2}.prefab";
                string meshFolder = $"{MeshOutputFolder}/RockSmallFloor";
                EnsureFolder(meshFolder);
                if (BuildGeologyPrefab(GeologyArchetype.RockFloor, i * 1337 + 42, 1f,
                    prefabPath, meshFolder, $"RockFloor_{i:D2}", mat,
                    new Vector3(1.2f, 0.7f, 1.0f)))
                    built++;
            }

            // ── 8 variantov srednih klasterov ─────────────────────
            for (int i = 0; i < 10; i++)
            {
                string prefabPath = $"{FinalPrefabFolder}/PFB_Geo_RockCluster_{i:D2}.prefab";
                string meshFolder = $"{MeshOutputFolder}/RockClusterMedium";
                EnsureFolder(meshFolder);
                if (BuildGeologyPrefab(GeologyArchetype.RockCluster, i * 2741 + 137, 1f,
                    prefabPath, meshFolder, $"RockCluster_{i:D2}", mat,
                    new Vector3(5f, 3f, 5f)))
                    built++;
            }

            // ── 6 variantov shelf / cliff ─────────────────────────
            for (int i = 0; i < 8; i++)
            {
                string prefabPath = $"{FinalPrefabFolder}/PFB_Geo_RockShelf_{i:D2}.prefab";
                string meshFolder = $"{MeshOutputFolder}/RockShelfLarge";
                EnsureFolder(meshFolder);
                if (BuildGeologyPrefab(GeologyArchetype.RockShelf, i * 3571 + 211, 1f,
                    prefabPath, meshFolder, $"RockShelf_{i:D2}", mat,
                    new Vector3(12f, 6f, 6f)))
                    built++;
            }

            // ── 6 variantov bolshih arok ──────────────────────────
            for (int i = 0; i < 6; i++)
            {
                string prefabPath = $"{FinalPrefabFolder}/PFB_Geo_RockArch_{i:D2}.prefab";
                string meshFolder = $"{MeshOutputFolder}/RockArchLarge";
                EnsureFolder(meshFolder);
                if (BuildGeologyPrefab(GeologyArchetype.RockArch, i * 4127 + 317, 1f,
                    prefabPath, meshFolder, $"RockArch_{i:D2}", mat,
                    new Vector3(18f, 12f, 6f)))
                    built++;
            }

            // ── Obratnaya sovmestimost: staryy put arki ──────────
            {
                string legacyPath = $"{FinalPrefabFolder}/PFB_Geo_RockArch_Large.prefab";
                string meshFolder = $"{MeshOutputFolder}/RockArchLarge";
                BuildGeologyPrefab(GeologyArchetype.RockArch, 4127 + 317, 1f,
                    legacyPath, meshFolder, "RockArch_Legacy", mat,
                    new Vector3(18f, 8f, 10f));
            }

            // ── 5 variantov cave entrances ────────────────────────
            for (int i = 0; i < 6; i++)
            {
                string prefabPath = $"{FinalPrefabFolder}/PFB_Geo_CaveEntrance_{i:D2}.prefab";
                string meshFolder = $"{MeshOutputFolder}/CaveEntrance";
                EnsureFolder(meshFolder);
                if (BuildGeologyPrefab(GeologyArchetype.CaveEntrance, i * 5003 + 419, 1f,
                    prefabPath, meshFolder, $"CaveEntrance_{i:D2}", mat,
                    new Vector3(14f, 10f, 8f)))
                    built++;
            }

            // ── Obratnaya sovmestimost: staryy put cave entrance ─
            {
                string legacyPath = $"{FinalPrefabFolder}/PFB_Geo_Cave_Entrance.prefab";
                string meshFolder = $"{MeshOutputFolder}/CaveEntrance";
                BuildGeologyPrefab(GeologyArchetype.CaveEntrance, 5003 + 419, 1f,
                    legacyPath, meshFolder, "CaveEntrance_Legacy", mat,
                    new Vector3(17f, 9f, 14f));
            }

            // ── 5 variantov landmark spires ───────────────────────
            for (int i = 0; i < 6; i++)
            {
                string prefabPath = $"{FinalPrefabFolder}/PFB_Geo_LandmarkSpire_{i:D2}.prefab";
                string meshFolder = $"{MeshOutputFolder}/LandmarkSpire";
                EnsureFolder(meshFolder);
                if (BuildGeologyPrefab(GeologyArchetype.LandmarkSpire, i * 6271 + 523, 1f,
                    prefabPath, meshFolder, $"LandmarkSpire_{i:D2}", mat,
                    new Vector3(8f, 20f, 8f)))
                    built++;
            }

            // ── Obratnaya sovmestimost: staryy put spire ─────────
            {
                string legacyPath = $"{FinalPrefabFolder}/PFB_Geo_Landmark_Spire.prefab";
                string meshFolder = $"{MeshOutputFolder}/LandmarkSpire";
                BuildGeologyPrefab(GeologyArchetype.LandmarkSpire, 6271 + 523, 1f,
                    legacyPath, meshFolder, "LandmarkSpire_Legacy", mat,
                    new Vector3(10f, 22f, 10f));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GeologyFinalAuthoring] Built {built} geology final prefabs. " +
                      $"Mesh assets saved to {MeshOutputFolder}.");
        }

        // ── Core builder ──────────────────────────────────────────

        private static Material ResolveGeologyRockMaterial()
        {
            string[] guids = AssetDatabase.FindAssets($"{RockMaterialName} t:Material", RockMaterialSearchFolders);
            if (guids == null || guids.Length == 0)
                return null;

            Array.Sort(guids, StringComparer.Ordinal);
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        }

        private static bool BuildGeologyPrefab(
            GeologyArchetype archetype,
            int seed,
            float scale,
            string prefabPath,
            string meshFolder,
            string meshBaseName,
            Material mat,
            Vector3 colliderSize)
        {
            // 1. Generiruem mesh bundle
            GeologyMeshBundle bundle = WorldGenerativeGeologyMeshBuilder.Build(archetype, seed, scale);
            if (bundle == null || bundle.Lod0 == null)
            {
                Debug.LogWarning($"[GeologyFinalAuthoring] Builder returned null for {meshBaseName}.");
                return false;
            }

            // 2. Sohranyaem mesh assets
            bundle.Lod0     = SaveMesh(bundle.Lod0,     $"{meshFolder}/{meshBaseName}_LOD0.asset");
            bundle.Lod1     = SaveMesh(bundle.Lod1,     $"{meshFolder}/{meshBaseName}_LOD1.asset");
            bundle.Lod2     = SaveMesh(bundle.Lod2,     $"{meshFolder}/{meshBaseName}_LOD2.asset");
            bundle.Collider = SaveMesh(bundle.Collider, $"{meshFolder}/{meshBaseName}_COL.asset");

            // 3. Sobiraem prefab
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                // BoxCollider — uproschennyy, ne iz LOD0
                if (archetype == GeologyArchetype.RockFloor || archetype == GeologyArchetype.RockCluster)
                {
                    BoxCollider col = root.AddComponent<BoxCollider>();
                    col.size = colliderSize;
                    col.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
                }
                else
                {
                    GameObject colliderRoot = new GameObject("COL_CompoundProxy_1716");
                    colliderRoot.transform.SetParent(root.transform, false);
                    BoxCollider col = colliderRoot.AddComponent<BoxCollider>();
                    col.size = colliderSize;
                    col.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
                }

                // LOD chain
                LOD[] lods = new LOD[3];

                lods[0] = BuildLodLevel(root.transform, "LOD0", bundle.Lod0, mat, 0.6f);
                lods[1] = BuildLodLevel(root.transform, "LOD1", bundle.Lod1, mat, 0.15f);
                lods[2] = BuildLodLevel(root.transform, "LOD2", bundle.Lod2, mat, 0.04f);

                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();

                // Sohranyaem prefab
                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed before save: " + colliderFailure);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved != null && !ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath, out colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed after save: " + colliderFailure);

                return saved != null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static LOD BuildLodLevel(
            Transform parent, string name, Mesh mesh, Material mat, float threshold)
        {
            GameObject lodGO = new GameObject(name);
            lodGO.transform.SetParent(parent, false);

            MeshFilter mf = lodGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = lodGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            return new LOD(threshold, new Renderer[] { mr });
        }

        private static Mesh SaveMesh(Mesh mesh, string assetPath)
        {
            if (mesh == null) return null;
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            mesh.name = assetName;

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                // Obnovlyaem suschestvuyuschiy asset
                existing.Clear();
                List<Vector3> vertices = new List<Vector3>(mesh.vertexCount);
                List<int> triangles = new List<int>((int)mesh.GetIndexCount(0));
                mesh.GetVertices(vertices);
                mesh.GetTriangles(triangles, 0);
                existing.SetVertices(vertices);
                existing.SetTriangles(triangles, 0);
                existing.RecalculateNormals();
                existing.RecalculateBounds();
                existing.Optimize();
                existing.name = assetName;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        // ── Folder utility ────────────────────────────────────────

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string name   = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;

            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
