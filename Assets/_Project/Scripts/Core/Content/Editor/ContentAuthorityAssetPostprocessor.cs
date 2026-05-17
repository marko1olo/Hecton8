using System;
using Hecton8.Core.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Core.Content.Editor
{
    public sealed class ContentAuthorityAssetPostprocessor : AssetPostprocessor
    {
        private const float SmallShadowScaleMeters = 0.2f;
        private const float Lod0ScreenRatio = 1.00f;
        private const float Lod1ScreenRatio = 0.30f;
        private const float Lod2ImpostorScreenRatio = 0.05f;

        private void OnPostprocessModel(GameObject root)
        {
            if (root == null)
                return;

            if (IsFloraAsset(assetPath, root))
                StripMeshColliders(root);

            if (IsEnvironmentAsset(assetPath))
                AutomateLod(root);

            PurgeSmallShadowCasters(root);
        }

        private static bool IsFloraAsset(string path, GameObject root)
        {
            if (!string.IsNullOrEmpty(path) &&
                path.IndexOf("/Flora/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return string.Equals(root.tag, "Aesthetic/Flora", StringComparison.Ordinal);
        }

        private static bool IsEnvironmentAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.IndexOf("/Environment/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("/Wreck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("/Debris", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void StripMeshColliders(GameObject root)
        {
            MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
                UnityEngine.Object.DestroyImmediate(colliders[i], true);
        }

        private static void AutomateLod(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = root.AddComponent<LODGroup>();

            Renderer[] lod0 = CopyRenderers(renderers);
            Renderer[] lod1 = CopyRenderers(renderers);
            Renderer[] lod2 = Array.Empty<Renderer>();
            LOD[] lods =
            {
                new LOD(Lod0ScreenRatio, lod0),
                new LOD(Lod1ScreenRatio, lod1),
                new LOD(Lod2ImpostorScreenRatio, lod2)
            };

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        private static Renderer[] CopyRenderers(Renderer[] source)
        {
            Renderer[] copy = new Renderer[source.Length];
            for (int i = 0; i < source.Length; i++)
                copy[i] = source[i];
            return copy;
        }

        private static void PurgeSmallShadowCasters(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Vector3 scale = renderer.transform.lossyScale;
                float maxAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                if (maxAxis >= SmallShadowScaleMeters)
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    public static class ContentPhysicsProxyBaker
    {
        private const string MeshAssetFolder = "Assets/_Project/Data/Generated/ContentPhysicsProxies";

        [MenuItem("HECTON-8/Content/Bake Selected Physics Proxy")]
        public static void BakeSelected()
        {
            GameObject[] selection = Selection.gameObjects;
            for (int i = 0; i < selection.Length; i++)
                Bake(selection[i]);
        }

        private static void Bake(GameObject root)
        {
            if (root == null)
                return;

            BoxCollider[] boxes = root.GetComponentsInChildren<BoxCollider>(true);
            if (boxes == null || boxes.Length == 0)
                return;

            Bounds bounds = boxes[0].bounds;
            for (int i = 1; i < boxes.Length; i++)
                bounds.Encapsulate(boxes[i].bounds);

            GameObject proxy = new GameObject("GEN_PhysicsProxyHull");
            Undo.RegisterCreatedObjectUndo(proxy, "Bake physics proxy");
            proxy.transform.SetParent(root.transform, true);
            proxy.transform.position = bounds.center;
            proxy.transform.rotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;

            Mesh mesh = BuildBoxHullMesh(bounds.size);
            EnsureMeshAssetFolder();
            string meshPath = AssetDatabase.GenerateUniqueAssetPath(
                MeshAssetFolder + "/" + root.name + "_PhysicsProxyHull.asset");
            AssetDatabase.CreateAsset(mesh, meshPath);

            MeshCollider hull = proxy.AddComponent<MeshCollider>();
            hull.sharedMesh = mesh;
            hull.convex = true;

            for (int i = 0; i < boxes.Length; i++)
                UnityEngine.Object.DestroyImmediate(boxes[i], true);

            EditorUtility.SetDirty(root);
        }

        private static Mesh BuildBoxHullMesh(Vector3 size)
        {
            Vector3 half = size * 0.5f;
            Vector3[] vertices =
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y,  half.z),
                new Vector3(-half.x, -half.y,  half.z),
                new Vector3(-half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y,  half.z),
                new Vector3(-half.x,  half.y,  half.z)
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };

            Mesh mesh = new Mesh
            {
                name = "GEN_PhysicsProxyHull"
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EnsureMeshAssetFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Generated"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Generated");
            if (!AssetDatabase.IsValidFolder(MeshAssetFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Data/Generated", "ContentPhysicsProxies");
        }
    }
}
