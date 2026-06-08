#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class CodexVisualRouteAssetAudit
    {
        private const string ReportPath = "Docs/AgentLogs/CODEX_VISUAL_ROUTE_ASSET_AUDIT_20260608.txt";

        private static readonly string[] MaterialPaths =
        {
            "Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat",
            "Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat",
            "Assets/_Project/Art/Materials/World/MAT_H8AegirGasGiantReal_1428.mat",
            "Assets/_Project/Art/Materials/World/MAT_SurfaceGasGiant_1428.mat",
            "Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat",
            "Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat",
            "Assets/_Project/Art/Materials/Mat_HectonSky.mat",
            "Assets/_Project/Art/Materials/Mat_GasGiant.mat",
            "Assets/_Project/_Archive/Mat_Ocean.mat"
        };

        private static readonly string[] PrefabPaths =
        {
            "Assets/_Project/Prefabs/GasGiant_Aegir.prefab",
            "Assets/_Project/_PROLOGUE_CONTENT/Prefabs/GasGiant_Aegir.prefab",
            "Assets/_Project/_PROLOGUE_CONTENT/Prefabs/Hecton8_Surface.prefab",
            "Assets/_Project/Prefabs/Hecton Ocean.prefab",
            "Assets/_Project/Prefabs/Ocean_Crest.prefab",
            "Assets/_Project/Prefabs/Sky_System.prefab"
        };

        public static void AuditAndExit()
        {
            int exitCode = 0;
            try
            {
                string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

                StringBuilder builder = new StringBuilder(32 * 1024);
                builder.AppendLine("status=STATIC_ASSET_AUDIT");
                builder.AppendLine("date=2026-06-08");
                builder.AppendLine("evidenceClass=UNITY_EDITOR_ASSETDATABASE_STATIC");
                builder.AppendLine();
                builder.AppendLine("[materials]");
                for (int i = 0; i < MaterialPaths.Length; i++)
                    AppendMaterial(builder, MaterialPaths[i]);

                builder.AppendLine();
                builder.AppendLine("[prefabs]");
                for (int i = 0; i < PrefabPaths.Length; i++)
                    AppendPrefab(builder, PrefabPaths[i]);

                File.WriteAllText(absolutePath, builder.ToString(), Encoding.UTF8);
                Debug.Log("[CodexVisualRouteAssetAudit] Wrote " + absolutePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        private static void AppendMaterial(StringBuilder builder, string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            builder.AppendLine("material=" + path);
            if (material == null)
            {
                builder.AppendLine("  missing=true");
                return;
            }

            builder.AppendLine("  name=" + material.name);
            builder.AppendLine("  shader=" + (material.shader != null ? material.shader.name : "<missing>"));
            builder.AppendLine("  renderQueue=" + material.renderQueue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string[] textureNames = material.GetTexturePropertyNames();
            for (int i = 0; i < textureNames.Length; i++)
            {
                string property = textureNames[i];
                Texture texture = material.GetTexture(property);
                if (texture == null)
                    continue;

                string texturePath = AssetDatabase.GetAssetPath(texture);
                builder.Append("  texture=");
                builder.Append(property);
                builder.Append(" path=");
                builder.Append(texturePath);
                builder.Append(" type=");
                builder.Append(texture.GetType().Name);
                builder.Append(" size=");
                builder.Append(texture.width.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Append("x");
                builder.Append(texture.height.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine();
            }
        }

        private static void AppendPrefab(StringBuilder builder, string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            builder.AppendLine("prefab=" + path);
            if (prefab == null)
            {
                builder.AppendLine("  missing=true");
                return;
            }

            AppendTransform(builder, prefab.transform, "  ");
        }

        private static void AppendTransform(StringBuilder builder, Transform transform, string indent)
        {
            builder.Append(indent);
            builder.Append("go=");
            builder.Append(transform.name);
            builder.Append(" active=");
            builder.Append(transform.gameObject.activeSelf ? "true" : "false");
            builder.Append(" localPos=");
            AppendVector(builder, transform.localPosition);
            builder.Append(" localScale=");
            AppendVector(builder, transform.localScale);
            builder.AppendLine();

            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer != null)
            {
                builder.Append(indent);
                builder.Append("  renderer=");
                builder.Append(renderer.GetType().Name);
                builder.Append(" enabled=");
                builder.Append(renderer.enabled ? "true" : "false");
                builder.Append(" mats=");
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (i > 0)
                        builder.Append("|");
                    Material mat = materials[i];
                    builder.Append(mat != null ? AssetDatabase.GetAssetPath(mat) : "<null>");
                }
                builder.AppendLine();
            }

            MeshFilter meshFilter = transform.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                Mesh mesh = meshFilter.sharedMesh;
                builder.Append(indent);
                builder.Append("  mesh=");
                builder.Append(mesh != null ? AssetDatabase.GetAssetPath(mesh) : "<null>");
                if (mesh != null)
                {
                    builder.Append(" name=");
                    builder.Append(mesh.name);
                    builder.Append(" verts=");
                    builder.Append(mesh.vertexCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.Append(" tris=");
                    builder.Append((mesh.triangles.Length / 3).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                builder.AppendLine();
            }

            MonoBehaviour[] behaviours = transform.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                builder.Append(indent);
                builder.Append("  component=");
                builder.Append(behaviour != null ? behaviour.GetType().FullName : "<missing-script>");
                builder.AppendLine();
            }

            for (int i = 0; i < transform.childCount; i++)
                AppendTransform(builder, transform.GetChild(i), indent + "  ");
        }

        private static void AppendVector(StringBuilder builder, Vector3 vector)
        {
            builder.Append("(");
            builder.Append(vector.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",");
            builder.Append(vector.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",");
            builder.Append(vector.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(")");
        }
    }
}
#endif
