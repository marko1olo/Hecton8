using System.IO;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Offline baker for eight-view octahedral impostor atlases.
    /// </summary>
    public static class HectonOctahedralImpostorBaker
    {
        private const int AtlasWidth = 2048;
        private const int AtlasHeight = 2048;
        private const int AtlasColumns = 4;
        private const int AtlasCellRows = 4;
        private const int TileWidth = AtlasWidth / AtlasColumns;
        private const int TileHeight = AtlasHeight / AtlasCellRows;
        private const int BakeLayer = 31;
        private const string OutputRoot = "Assets/_Project/Art/Impostors";
        private const string ShaderPath = "Assets/_Project/Art/Shaders/Hecton_OctahedralImpostor.shader";
        private const string AlbedoAlphaShaderPath = "Assets/_Project/Art/Shaders/Hecton_EditorOctaImpostorAlbedoAlpha.shader";
        private const string NormalDepthShaderPath = "Assets/_Project/Art/Shaders/Hecton_EditorOctaImpostorNormalDepth.shader";

        private static readonly Vector3[] BakeDirections =
        {
            new Vector3(0.9238795f, 0.3826834f, 0f),
            new Vector3(0f, 0.3826834f, 0.9238795f),
            new Vector3(-0.9238795f, 0.3826834f, 0f),
            new Vector3(0f, 0.3826834f, -0.9238795f),
            new Vector3(0.9238795f, -0.3826834f, 0f),
            new Vector3(0f, -0.3826834f, 0.9238795f),
            new Vector3(-0.9238795f, -0.3826834f, 0f),
            new Vector3(0f, -0.3826834f, -0.9238795f)
        };

        [MenuItem("HECTON-8/Bake HLOD Impostor", false, 2500)]
        private static void BakeSelected()
        {
            GameObject source = Selection.activeGameObject;
            if (source == null)
            {
                EditorUtility.DisplayDialog("HLOD Impostor Baker", "Select a GameObject with renderers.", "OK");
                return;
            }

            if (!TryCalculateRendererBounds(source, out Bounds sourceBounds))
            {
                EditorUtility.DisplayDialog("HLOD Impostor Baker", "Selected GameObject has no renderer bounds.", "OK");
                return;
            }

            Shader albedoAlphaShader = AssetDatabase.LoadAssetAtPath<Shader>(AlbedoAlphaShaderPath);
            Shader normalDepthShader = AssetDatabase.LoadAssetAtPath<Shader>(NormalDepthShaderPath);
            Shader impostorShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (albedoAlphaShader == null || normalDepthShader == null || impostorShader == null)
            {
                EditorUtility.DisplayDialog("HLOD Impostor Baker", "Required impostor shaders are missing.", "OK");
                return;
            }

            string safeName = SanitizeAssetName(source.name);
            string folder = EnsureOutputFolder(safeName);
            string albedoPath = folder + "/TX_" + safeName + "_ImpostorAlbedoDepth.png";
            string normalPath = folder + "/TX_" + safeName + "_ImpostorNormalDepth.png";
            string dataPath = folder + "/ImpostorData_" + safeName + ".asset";
            string materialPath = folder + "/MAT_" + safeName + "_OctahedralImpostor.mat";

            GameObject clone = null;
            Camera bakeCamera = null;
            RenderTexture tileRt = null;
            Texture2D albedoAtlas = null;
            Texture2D normalAtlas = null;

            try
            {
                clone = Object.Instantiate(source);
                clone.name = source.name + "_H8_ImpostorBakeClone";
                clone.hideFlags = HideFlags.HideAndDontSave;
                StripBehaviours(clone);
                ForceHighestLod(clone);
                SetHideFlagsAndLayer(clone.transform, BakeLayer);

                if (!TryCalculateRendererBounds(clone, out Bounds cloneBounds))
                    return;

                clone.transform.position -= cloneBounds.center;
                if (!TryCalculateRendererBounds(clone, out Bounds bakeBounds))
                    return;

                bakeCamera = CreateBakeCamera();
                tileRt = RenderTexture.GetTemporary(TileWidth, TileHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                tileRt.name = "H8 Impostor Tile RT";
                albedoAtlas = CreateClearedAtlas("H8 AlbedoDepth Atlas");
                normalAtlas = CreateClearedAtlas("H8 NormalDepth Atlas");

                float radius = Mathf.Max(0.5f, bakeBounds.extents.magnitude);
                float cameraDistance = Mathf.Max(2f, radius * 2.5f);
                float farClip = Mathf.Max(8f, radius * 6f);
                BakeAtlasPass(bakeCamera, tileRt, albedoAtlas, bakeBounds, cameraDistance, farClip, albedoAlphaShader);
                BakeAtlasPass(bakeCamera, tileRt, normalAtlas, bakeBounds, cameraDistance, farClip, normalDepthShader);

                WriteTexture(albedoAtlas, albedoPath, sRgb: true);
                WriteTexture(normalAtlas, normalPath, sRgb: false);
                AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);

                Texture2D albedoAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                Texture2D normalAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                HectonOctahedralImpostorData data = AssetDatabase.LoadAssetAtPath<HectonOctahedralImpostorData>(dataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<HectonOctahedralImpostorData>();
                    AssetDatabase.CreateAsset(data, dataPath);
                }

                data.Configure(
                    albedoAsset,
                    normalAsset,
                    sourceBounds,
                    sourceBounds.center - source.transform.position,
                    AtlasWidth,
                    radius,
                    farClip,
                    HectonChunkImpostorResidency.DefaultImpostorEnterDistanceMeters);
                EditorUtility.SetDirty(data);
                CreateOrUpdateMaterial(materialPath, impostorShader, albedoAsset, normalAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = data;
            }
            finally
            {
                if (tileRt != null)
                    RenderTexture.ReleaseTemporary(tileRt);
                if (albedoAtlas != null)
                    Object.DestroyImmediate(albedoAtlas);
                if (normalAtlas != null)
                    Object.DestroyImmediate(normalAtlas);
                if (bakeCamera != null)
                    Object.DestroyImmediate(bakeCamera.gameObject);
                if (clone != null)
                    Object.DestroyImmediate(clone);
            }
        }

        [MenuItem("HECTON-8/Bake HLOD Impostor", true)]
        private static bool ValidateBakeSelected()
        {
            return Selection.activeGameObject != null;
        }

        private static Camera CreateBakeCamera()
        {
            GameObject cameraObject = new GameObject("H8 Impostor Bake Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cameraType = CameraType.Preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.cullingMask = 1 << BakeLayer;
            return camera;
        }

        private static Texture2D CreateClearedAtlas(string name)
        {
            Texture2D atlas = new Texture2D(AtlasWidth, AtlasHeight, TextureFormat.RGBA32, true, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] clear = new Color32[AtlasWidth * AtlasHeight]; // COLD EDITOR ALLOC: atlas clear pixels for offline impostor bake - owner: HectonOctahedralImpostorBaker
            atlas.SetPixels32(clear);
            atlas.Apply(false, false);
            return atlas;
        }

        private static void BakeAtlasPass(
            Camera camera,
            RenderTexture tileRt,
            Texture2D atlas,
            Bounds bounds,
            float cameraDistance,
            float farClip,
            Shader replacementShader)
        {
            camera.targetTexture = tileRt;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = farClip;
            camera.orthographicSize = Mathf.Max(0.5f, bounds.extents.magnitude);
            if (replacementShader != null)
                camera.SetReplacementShader(replacementShader, string.Empty);
            else
                camera.ResetReplacementShader();

            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < HectonOctahedralImpostorData.ViewCount; i++)
                {
                    Vector3 direction = BakeDirections[i];
                    Vector3 cameraPosition = bounds.center + direction * cameraDistance;
                    Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.96f ? Vector3.forward : Vector3.up;
                    camera.transform.SetPositionAndRotation(
                        cameraPosition,
                        Quaternion.LookRotation(bounds.center - cameraPosition, up));

                    RenderTexture.active = tileRt;
                    GL.Clear(true, true, Color.clear);
                    camera.Render();
                    int tileX = (i % AtlasColumns) * TileWidth;
                    int tileY = (i / AtlasColumns) * TileHeight;
                    atlas.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), tileX, tileY, false);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.ResetReplacementShader();
            }

            atlas.Apply(true, false);
        }

        private static void WriteTexture(Texture2D texture, string assetPath, bool sRgb)
        {
            byte[] bytes = texture.EncodeToPNG();
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = HectonOctahedralImpostorData.DefaultAtlasSize;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = sRgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        private static void CreateOrUpdateMaterial(
            string materialPath,
            Shader shader,
            Texture2D albedo,
            Texture2D normal)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_ImpostorAlbedoDepthAtlas", albedo);
            material.SetTexture("_ImpostorNormalDepthAtlas", normal);
            EditorUtility.SetDirty(material);
        }

        private static bool TryCalculateRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (hasBounds)
                    bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        private static void StripBehaviours(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                    Object.DestroyImmediate(behaviours[i]);
            }
        }

        private static void ForceHighestLod(GameObject root)
        {
            LODGroup[] lodGroups = root.GetComponentsInChildren<LODGroup>(true);
            for (int i = 0; i < lodGroups.Length; i++)
            {
                if (lodGroups[i] != null)
                    lodGroups[i].ForceLOD(0);
            }
        }

        private static void SetHideFlagsAndLayer(Transform root, int layer)
        {
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetHideFlagsAndLayer(root.GetChild(i), layer);
        }

        private static string EnsureOutputFolder(string safeName)
        {
            EnsureFolder("Assets/_Project/Art", "Impostors");
            EnsureFolder(OutputRoot, safeName);
            return OutputRoot + "/" + safeName;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static string SanitizeAssetName(string source)
        {
            char[] chars = source.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '-';
                if (!valid)
                    chars[i] = '_';
            }

            return chars.Length > 0 ? new string(chars) : "Selection";
        }
    }
}
