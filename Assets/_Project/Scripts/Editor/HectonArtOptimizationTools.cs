#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Editor-only production tools for mesh proxies, normal-map repairs, vertex-color bakes, pivot audits, and atlas generation.
    /// </summary>
    internal static class HectonArtOptimizationTools
    {
        private const string ShadowProxyMenuPath = "Hecton/Art Optimization/Generate Shadow Proxies For Selected Prefabs";
        private const string NormalFlipMenuPath = "Hecton/Art Optimization/Flip Tagged DirectX Normal Green Channels";
        private const string VertexBakeMenuPath = "Hecton/Art Optimization/Bake Selected Vertex AO Wear";
        private const string PivotAuditMenuPath = "Hecton/Validation/Asset Pipeline/Audit Scatter Bottom Pivots";
        private const string AtlasMenuPath = "Hecton/Art Optimization/Pack Selected Coral Atlas And Remap Prefabs";
        private const string ShadowProxyFolder = "Assets/_Project/Art/Generated/ShadowProxies";
        private const string VertexBakeFolder = "Assets/_Project/Art/Generated/VertexColorBakes";
        private const string AtlasFolder = "Assets/_Project/Art/Generated/Atlases";
        private const string ArtRoot = "Assets/_Project/Art";
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const int ShadowProxyTriangleBudget = 200;
        private const int AtlasSize = 2048;
        private const float PivotToleranceMeters = 0.01f;
        private const int AtlasGridColumns = 8;
        private const int AtlasGridRows = 8;
        private const int AtlasCellSize = AtlasSize / AtlasGridColumns;
        private const int AtlasGridCapacity = AtlasGridColumns * AtlasGridRows;
        private const int AtlasInputCap = AtlasGridCapacity;

        [MenuItem(ShadowProxyMenuPath, priority = 220)]
        private static void GenerateShadowProxiesForSelection()
        {
            int processed = 0;
            UnityEngine.Object[] selection = Selection.objects;
            for (int i = 0; i < selection.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selection[i]);
                if (!IsPrefabPath(path))
                    continue;

                if (GenerateShadowProxyForPrefab(path))
                    processed++;
            }

            Debug.Log("[HectonArtOptimizationTools] Shadow proxy generation processed prefabs=" + processed + ".");
        }

        [MenuItem(NormalFlipMenuPath, priority = 221)]
        private static void FlipTaggedDirectXNormals()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            int scanned = 0;
            int flipped = 0;
            int skippedUntagged = 0;

            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !HectonTextureImportDictator.IsNormalMap(path, importer))
                    continue;

                scanned++;
                if (!IsDirectXTaggedNormal(path))
                {
                    skippedUntagged++;
                    continue;
                }

                if (FlipNormalGreenChannel(path, importer))
                    flipped++;
            }

            Debug.Log(
                "[HectonArtOptimizationTools] Normal green-channel scan: scanned=" + scanned +
                ", flippedTaggedDirectX=" + flipped +
                ", skippedNoObjectiveTag=" + skippedUntagged + ".");
        }

        [MenuItem(VertexBakeMenuPath, priority = 222)]
        private static void BakeSelectedVertexAoWear()
        {
            int bakedMeshes = 0;
            UnityEngine.Object[] selection = Selection.objects;
            for (int i = 0; i < selection.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selection[i]);
                if (IsPrefabPath(path))
                {
                    bakedMeshes += BakePrefabVertexColors(path);
                    continue;
                }

                Mesh mesh = selection[i] as Mesh;
                if (mesh != null)
                {
                    Mesh baked = BuildVertexColorBake(mesh, mesh.name + "_AO_Wear");
                    if (baked != null)
                    {
                        EnsureFolder(VertexBakeFolder);
                        string meshPath = AssetDatabase.GenerateUniqueAssetPath(VertexBakeFolder + "/" + HectonEditorMeshUtility.SanitizeAssetToken(baked.name) + ".asset");
                        AssetDatabase.CreateAsset(baked, meshPath);
                        bakedMeshes++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[HectonArtOptimizationTools] Vertex AO/wear bake produced meshes=" + bakedMeshes + ".");
        }

        [MenuItem(PivotAuditMenuPath, priority = 223)]
        private static void AuditScatterBottomPivots()
        {
            List<string> violations = new List<string>(128);
            CollectScatterBottomPivotViolations(violations);

            for (int i = 0; i < Mathf.Min(violations.Count, 96); i++)
                Debug.LogWarning("[HectonArtOptimizationTools] Pivot violation: " + violations[i]);

            Debug.Log("[HectonArtOptimizationTools] Scatter bottom-pivot audit violations=" + violations.Count + ".");
        }

        internal static void CollectScatterBottomPivotViolations(List<string> violations)
        {
            if (violations == null)
                return;

            ScanPivotRoot(ArtRoot, violations);
            ScanPivotRoot(PrefabRoot + "/Nature", violations);
        }

        [MenuItem(AtlasMenuPath, priority = 224)]
        private static void PackSelectedCoralAtlasAndRemapPrefabs()
        {
            List<string> texturePaths = new List<string>(AtlasInputCap);
            List<string> prefabPaths = new List<string>(64);

            UnityEngine.Object[] selection = Selection.objects;
            for (int i = 0; i < selection.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selection[i]);
                Texture2D texture = selection[i] as Texture2D;
                if (texture != null && texturePaths.Count < AtlasInputCap)
                {
                    if (!string.IsNullOrEmpty(path))
                        texturePaths.Add(path);
                    continue;
                }

                if (IsPrefabPath(path))
                    prefabPaths.Add(path);
            }

            if (texturePaths.Count <= 0 || prefabPaths.Count <= 0)
            {
                Debug.LogError("[HectonArtOptimizationTools] Select coral textures and the prefabs/meshes that use them before running atlas automation.");
                return;
            }

            string atlasPath = BuildTextureAtlas(texturePaths, out Rect[] rects);
            if (string.IsNullOrEmpty(atlasPath) || rects == null || rects.Length != texturePaths.Count)
                return;

            Material atlasMaterial = CreateAtlasMaterial(atlasPath);
            Dictionary<string, Rect> textureRectLookup = new Dictionary<string, Rect>(texturePaths.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < texturePaths.Count; i++)
                textureRectLookup[texturePaths[i]] = rects[i];

            int remappedMeshes = 0;
            for (int i = 0; i < prefabPaths.Count; i++)
                remappedMeshes += RemapPrefabUvsToAtlas(prefabPaths[i], textureRectLookup, atlasMaterial);

            AssetDatabase.SaveAssets();
            Debug.Log("[HectonArtOptimizationTools] Atlas=" + atlasPath + ", textures=" + texturePaths.Count + ", remappedMeshes=" + remappedMeshes + ".");
        }

        internal static bool GenerateShadowProxyForPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                return false;

            bool changed = false;
            try
            {
                RemoveExistingShadowProxies(root.transform);

                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
                int totalTriangles = 0;
                int validFilterCount = 0;
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    if (!IsRenderableSourceFilter(filter))
                        continue;

                    totalTriangles += HectonEditorMeshUtility.CountTriangles(filter.sharedMesh);
                    validFilterCount++;
                }

                if (totalTriangles <= 0 || validFilterCount <= 0)
                    return false;

                EnsureFolder(ShadowProxyFolder);
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    if (!IsRenderableSourceFilter(filter))
                        continue;

                    MeshRenderer sourceRenderer = filter.GetComponent<MeshRenderer>();
                    sourceRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    int sourceTriangles = HectonEditorMeshUtility.CountTriangles(filter.sharedMesh);
                    int meshBudget = Mathf.Max(1, Mathf.RoundToInt(ShadowProxyTriangleBudget * (sourceTriangles / (float)totalTriangles)));
                    Mesh proxyMesh = HectonEditorMeshUtility.BuildDecimatedMesh(
                        filter.sharedMesh,
                        1f,
                        meshBudget,
                        filter.sharedMesh.name + "_ShadowProxy");
                    if (proxyMesh == null)
                        continue;

                    string meshPath = AssetDatabase.GenerateUniqueAssetPath(
                        ShadowProxyFolder + "/" +
                        HectonEditorMeshUtility.SanitizeAssetToken(Path.GetFileNameWithoutExtension(prefabPath)) + "_" +
                        HectonEditorMeshUtility.SanitizeAssetToken(filter.sharedMesh.name) + "_ShadowProxy.asset");
                    AssetDatabase.CreateAsset(proxyMesh, meshPath);

                    GameObject proxyObject = new GameObject("__ShadowProxy_" + i);
                    Transform proxyTransform = proxyObject.transform;
                    proxyTransform.SetParent(filter.transform, false);
                    proxyTransform.localPosition = Vector3.zero;
                    proxyTransform.localRotation = Quaternion.identity;
                    proxyTransform.localScale = Vector3.one;

                    MeshFilter proxyFilter = proxyObject.AddComponent<MeshFilter>();
                    proxyFilter.sharedMesh = proxyMesh;
                    MeshRenderer proxyRenderer = proxyObject.AddComponent<MeshRenderer>();
                    proxyRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                    proxyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    proxyRenderer.receiveShadows = false;
                    proxyRenderer.lightProbeUsage = LightProbeUsage.Off;
                    proxyRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    changed = true;
                }

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }

        private static bool FlipNormalGreenChannel(string path, TextureImporter importer)
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (source == null)
                return false;

            int width = source.width;
            int height = source.height;
            RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);

            try
            {
                Graphics.Blit(source, temp);
                RenderTexture.active = temp;
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);

                NativeArray<Color32> pixels = readable.GetRawTextureData<Color32>();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    pixel.g = (byte)(255 - pixel.g);
                    pixels[i] = pixel;
                }

                readable.Apply(false, false);

                string writePath = path;
                if (!writePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    writePath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, ".GFlip.png"));

                File.WriteAllBytes(writePath, readable.EncodeToPNG());
                AssetDatabase.ImportAsset(writePath, ImportAssetOptions.ForceUpdate);
                TextureImporter outputImporter = AssetImporter.GetAtPath(writePath) as TextureImporter;
                if (outputImporter != null)
                {
                    HectonTextureImportDictator.ApplyImportPolicy(outputImporter, writePath);
                    outputImporter.SaveAndReimport();
                }

                return true;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static int BakePrefabVertexColors(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                return 0;

            int bakedCount = 0;
            try
            {
                EnsureFolder(VertexBakeFolder);
                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    if (filter == null || filter.sharedMesh == null)
                        continue;

                    Mesh baked = BuildVertexColorBake(filter.sharedMesh, filter.sharedMesh.name + "_AO_Wear");
                    if (baked == null)
                        continue;

                    string meshPath = AssetDatabase.GenerateUniqueAssetPath(
                        VertexBakeFolder + "/" +
                        HectonEditorMeshUtility.SanitizeAssetToken(Path.GetFileNameWithoutExtension(prefabPath)) + "_" +
                        HectonEditorMeshUtility.SanitizeAssetToken(baked.name) + ".asset");
                    AssetDatabase.CreateAsset(baked, meshPath);
                    filter.sharedMesh = baked;
                    bakedCount++;
                }

                if (bakedCount > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return bakedCount;
        }

        private static Mesh BuildVertexColorBake(Mesh source, string meshName)
        {
            if (source == null)
                return null;

            Mesh mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = meshName;
            List<Vector3> vertices = new List<Vector3>(mesh.vertexCount);
            List<Vector3> normals = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            if (normals.Count != vertices.Count)
            {
                mesh.RecalculateNormals();
                normals.Clear();
                mesh.GetNormals(normals);
            }

            Bounds bounds = mesh.bounds;
            float invHeight = bounds.size.y > 0.0001f ? 1f / bounds.size.y : 1f;
            List<Color32> colors = new List<Color32>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                float y01 = Mathf.Clamp01((vertices[i].y - bounds.min.y) * invHeight);
                float up = Mathf.Clamp01(normals[i].y);
                float side = 1f - Mathf.Abs(normals[i].y);
                byte ao = (byte)Mathf.RoundToInt(Mathf.Lerp(140f, 255f, y01) * Mathf.Lerp(0.8f, 1f, up));
                byte wear = (byte)Mathf.RoundToInt(255f * up * up * Mathf.SmoothStep(0.2f, 1f, y01));
                byte cavity = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(side * (1f - y01)));
                colors.Add(new Color32(ao, wear, cavity, 255));
            }

            mesh.SetColors(colors);
            return mesh;
        }

        private static void ScanPivotRoot(string root, List<string> violations)
        {
            if (!AssetDatabase.IsValidFolder(root))
                return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsScatterPath(path))
                    continue;

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                if (contents == null)
                    continue;

                try
                {
                    Bounds bounds = HectonEditorMeshUtility.CalculateLocalRendererBounds(contents, out bool hasBounds);
                    if (hasBounds && Mathf.Abs(bounds.min.y) > PivotToleranceMeters)
                        violations.Add(path + " | bounds.min.y=" + bounds.min.y.ToString("F4"));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static string BuildTextureAtlas(List<string> texturePaths, out Rect[] rects)
        {
            rects = null;
            if (texturePaths == null || texturePaths.Count <= 0 || texturePaths.Count > AtlasGridCapacity)
            {
                Debug.LogError("[HectonArtOptimizationTools] Atlas input count exceeds fixed 8x8 grid capacity.");
                return null;
            }

            Texture2D atlas = null;
            try
            {
                atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, true, false);
                NativeArray<Color32> atlasPixels = atlas.GetRawTextureData<Color32>();
                Color32 clear = new Color32(0, 0, 0, 0);
                for (int pixelIndex = 0; pixelIndex < atlasPixels.Length; pixelIndex++)
                    atlasPixels[pixelIndex] = clear;

                rects = new Rect[texturePaths.Count];
                for (int i = 0; i < texturePaths.Count; i++)
                {
                    int column = i % AtlasGridColumns;
                    int row = i / AtlasGridColumns;
                    int x = column * AtlasCellSize;
                    int y = row * AtlasCellSize;
                    rects[i] = new Rect(
                        x / (float)AtlasSize,
                        y / (float)AtlasSize,
                        AtlasCellSize / (float)AtlasSize,
                        AtlasCellSize / (float)AtlasSize);
                    CopyTextureToAtlasCell(texturePaths[i], x, y, atlasPixels);
                }

                atlas.Apply(true, false);

                EnsureFolder(AtlasFolder);
                string atlasPath = AssetDatabase.GenerateUniqueAssetPath(AtlasFolder + "/TX_ATLAS_Coral_2048.png");
                File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());

                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
                TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = true;
                    importer.isReadable = false;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.maxTextureSize = AtlasSize;
                    TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                    standalone.overridden = true;
                    standalone.format = TextureImporterFormat.BC7;
                    standalone.maxTextureSize = AtlasSize;
                    standalone.textureCompression = TextureImporterCompression.Compressed;
                    standalone.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(standalone);
                    importer.SaveAndReimport();
                }

                return atlasPath;
            }
            finally
            {
                if (atlas != null)
                    UnityEngine.Object.DestroyImmediate(atlas);
            }
        }

        private static void CopyTextureToAtlasCell(string sourcePath, int targetX, int targetY, NativeArray<Color32> atlasPixels)
        {
            Texture2D source = null;
            Texture2D readable = null;
            try
            {
                source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                if (source == null)
                    throw new InvalidOperationException("[HectonArtOptimizationTools] Missing atlas source texture: " + sourcePath);

                readable = CaptureReadableTexture(source, AtlasCellSize, AtlasCellSize);
                NativeArray<Color32> sourcePixels = readable.GetRawTextureData<Color32>();
                for (int y = 0; y < AtlasCellSize; y++)
                {
                    int sourceRow = y * AtlasCellSize;
                    int targetRow = (targetY + y) * AtlasSize + targetX;
                    for (int x = 0; x < AtlasCellSize; x++)
                        atlasPixels[targetRow + x] = sourcePixels[sourceRow + x];
                }
            }
            finally
            {
                if (readable != null)
                    UnityEngine.Object.DestroyImmediate(readable);
                if (source != null)
                    Resources.UnloadAsset(source);
            }
        }

        private static int RemapPrefabUvsToAtlas(string prefabPath, Dictionary<string, Rect> textureRectLookup, Material atlasMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                return 0;

            int remapped = 0;
            try
            {
                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    MeshRenderer renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
                    if (filter == null || renderer == null || filter.sharedMesh == null)
                        continue;

                    if (!TryResolveAtlasRect(renderer.sharedMaterials, textureRectLookup, out Rect rect, out int materialIndex))
                        continue;

                    Mesh remappedMesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    remappedMesh.name = filter.sharedMesh.name + "_AtlasUV";
                    List<Vector2> uv = new List<Vector2>(remappedMesh.vertexCount);
                    remappedMesh.GetUVs(0, uv);
                    if (uv.Count != remappedMesh.vertexCount)
                    {
                        UnityEngine.Object.DestroyImmediate(remappedMesh);
                        continue;
                    }

                    for (int uvIndex = 0; uvIndex < uv.Count; uvIndex++)
                        uv[uvIndex] = new Vector2(rect.x + uv[uvIndex].x * rect.width, rect.y + uv[uvIndex].y * rect.height);

                    remappedMesh.SetUVs(0, uv);
                    string meshPath = AssetDatabase.GenerateUniqueAssetPath(
                        AtlasFolder + "/" +
                        HectonEditorMeshUtility.SanitizeAssetToken(Path.GetFileNameWithoutExtension(prefabPath)) + "_" +
                        HectonEditorMeshUtility.SanitizeAssetToken(remappedMesh.name) + ".asset");
                    AssetDatabase.CreateAsset(remappedMesh, meshPath);
                    filter.sharedMesh = remappedMesh;

                    Material[] materials = renderer.sharedMaterials;
                    if (materialIndex >= 0 && materialIndex < materials.Length)
                    {
                        materials[materialIndex] = atlasMaterial;
                        renderer.sharedMaterials = materials;
                    }

                    remapped++;
                }

                if (remapped > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return remapped;
        }

        private static Material CreateAtlasMaterial(string atlasPath)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader)
            {
                name = "MAT_CoralAtlas_2048"
            };
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", atlas);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", atlas);

            EnsureFolder(AtlasFolder);
            string materialPath = AssetDatabase.GenerateUniqueAssetPath(AtlasFolder + "/MAT_CoralAtlas_2048.mat");
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        private static Texture2D CaptureReadableTexture(Texture texture)
        {
            return CaptureReadableTexture(texture, Mathf.Max(1, texture.width), Mathf.Max(1, texture.height));
        }

        private static Texture2D CaptureReadableTexture(Texture texture, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

            try
            {
                Graphics.Blit(texture, temp);
                RenderTexture.active = temp;
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
            }
        }

        private static bool TryResolveAtlasRect(Material[] materials, Dictionary<string, Rect> textureRectLookup, out Rect rect, out int materialIndex)
        {
            rect = default;
            materialIndex = -1;
            if (materials == null)
                return false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                Texture texture = null;
                if (material.HasProperty("_BaseMap"))
                    texture = material.GetTexture("_BaseMap");
                if (texture == null && material.HasProperty("_MainTex"))
                    texture = material.GetTexture("_MainTex");
                if (texture == null)
                    continue;

                string texturePath = AssetDatabase.GetAssetPath(texture);
                if (textureRectLookup.TryGetValue(texturePath, out rect))
                {
                    materialIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static bool IsRenderableSourceFilter(MeshFilter filter)
        {
            if (filter == null || filter.sharedMesh == null)
                return false;

            if (filter.gameObject.name.StartsWith("__ShadowProxy_", StringComparison.Ordinal))
                return false;

            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            return renderer != null && renderer.enabled;
        }

        private static void RemoveExistingShadowProxies(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                RemoveExistingShadowProxies(child);
                if (child.name.StartsWith("__ShadowProxy_", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool IsDirectXTaggedNormal(string path)
        {
            string lowerPath = path.Replace('\\', '/').ToLowerInvariant();
            return lowerPath.Contains("directx") ||
                   lowerPath.Contains("_dx") ||
                   lowerPath.Contains("/dx_") ||
                   lowerPath.Contains("y-") ||
                   lowerPath.Contains("green_inverted") ||
                   lowerPath.Contains("green-inverted");
        }

        private static bool IsPrefabPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsScatterPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string lowerPath = path.Replace('\\', '/').ToLowerInvariant();
            return lowerPath.Contains("scatter") ||
                   lowerPath.Contains("rock") ||
                   lowerPath.Contains("flora") ||
                   lowerPath.Contains("coral") ||
                   lowerPath.Contains("kelp") ||
                   lowerPath.Contains("debris") ||
                   lowerPath.Contains("nature");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = path.Substring(0, slash);
            string folder = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }
    }

    internal sealed class HectonScatterPivotBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1800;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> violations = new List<string>(128);
            HectonArtOptimizationTools.CollectScatterBottomPivotViolations(violations);
            if (violations.Count <= 0)
                return;

            string firstViolation = violations[0];
            throw new BuildFailedException(
                "[HectonScatterPivotBuildGuard] Scatter prop bottom-pivot fatal error. " +
                "Violations=" + violations.Count + ". First=" + firstViolation);
        }
    }
}
#endif
