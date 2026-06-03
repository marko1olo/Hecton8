#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Bakers
{
    public sealed class WreckageTextureBaker : EditorWindow
    {
        private const string MenuRoot = "HECTON-8/Bakers/1727/";
        private const string ComputeShaderPath = "Assets/_Project/Art/Shaders/Include/WreckageCarbonizationBaker1727.compute";
        private const string DefaultOutputFolder = "Assets/_Project/Art/Textures/Wreckage";
        private const int MinimumAlbedoSize = 1024;
        private const int MaximumAlbedoSize = 4096;
        private const int MinimumMraoSize = 512;
        private const int MaximumMraoSize = 2048;
        private const int MinimumGeneratedCurvatureSize = 64;
        private const int MaximumGeneratedCurvatureSize = 1024;
        private const int DryRunSize = 64;
        private const long MaxEncodedPngBytes = 128L * 1024L * 1024L;
        private const ulong ComputeKernelThreadLimit = 1024UL;

        private static readonly int s_outputId = Shader.PropertyToID("_WreckageOutput");
        private static readonly int s_curvatureMapId = Shader.PropertyToID("_WreckageCurvatureMap");
        private static readonly int s_textureSizeId = Shader.PropertyToID("_WreckageTextureSize");
        private static readonly int s_seedId = Shader.PropertyToID("_WreckageSeed");
        private static readonly int s_blastParamsId = Shader.PropertyToID("_WreckageBlastParams");
        private static readonly int s_layerParamsId = Shader.PropertyToID("_WreckageLayerParams");
        private static readonly int s_patternParamsId = Shader.PropertyToID("_WreckagePatternParams");

        private Mesh _sourceMesh;
        private string _assetName = "agent1717_wreckage_hull";
        private string _outputFolder = DefaultOutputFolder;
        private ComputeShader _computeOverride;
        private Texture2D _curvatureMap;
        private int _seed = 1727001;
        private float _globalQualityWeight = 0.70f;
        private Vector2 _explosionCenterUv = new Vector2(0.47f, 0.54f);
        private float _blastRadiusUv = 0.34f;
        private float _sootStrength = 0.86f;
        private float _scrapeStrength = 0.72f;
        private float _paintSurvival = 0.34f;
        private float _thermalHaloStrength = 0.78f;
        private string _lastStatus = "Idle.";

        private enum TextureRole
        {
            Albedo,
            Mrao
        }

        private struct BakeSettings
        {
            public Mesh SourceMesh;
            public string AssetName;
            public string OutputFolder;
            public ComputeShader ComputeOverride;
            public Texture2D CurvatureMap;
            public uint Seed;
            public float GlobalQualityWeight;
            public Vector2 ExplosionCenterUv;
            public float BlastRadiusUv;
            public float SootStrength;
            public float ScrapeStrength;
            public float PaintSurvival;
            public float ThermalHaloStrength;

            public static BakeSettings Default()
            {
                return new BakeSettings
                {
                    SourceMesh = null,
                    AssetName = "agent1717_wreckage_hull",
                    OutputFolder = DefaultOutputFolder,
                    ComputeOverride = null,
                    CurvatureMap = null,
                    Seed = 1727001u,
                    GlobalQualityWeight = 0.70f,
                    ExplosionCenterUv = new Vector2(0.47f, 0.54f),
                    BlastRadiusUv = 0.34f,
                    SootStrength = 0.86f,
                    ScrapeStrength = 0.72f,
                    PaintSurvival = 0.34f,
                    ThermalHaloStrength = 0.78f
                };
            }
        }

        private readonly struct BakeResult
        {
            public readonly string AlbedoPath;
            public readonly string MraoPath;
            public readonly int AlbedoSize;
            public readonly int MraoSize;
            public readonly double DurationMs;
            public readonly double DurationMicroseconds;
            public readonly BakeValidation AlbedoValidation;
            public readonly BakeValidation MraoValidation;

            public BakeResult(
                string albedoPath,
                string mraoPath,
                int albedoSize,
                int mraoSize,
                double durationMs,
                BakeValidation albedoValidation,
                BakeValidation mraoValidation)
            {
                AlbedoPath = albedoPath;
                MraoPath = mraoPath;
                AlbedoSize = albedoSize;
                MraoSize = mraoSize;
                DurationMs = durationMs;
                DurationMicroseconds = durationMs * 1000.0d;
                AlbedoValidation = albedoValidation;
                MraoValidation = mraoValidation;
            }
        }

        private readonly struct BakeValidation
        {
            public readonly long ExpectedPixelCount;
            public readonly long ActualPixelCount;
            public readonly byte RMin;
            public readonly byte RMax;
            public readonly byte GMin;
            public readonly byte GMax;
            public readonly byte BMin;
            public readonly byte BMax;
            public readonly byte AMin;
            public readonly byte AMax;
            public readonly int DeepCarbonMetalPixelCount;

            public BakeValidation(
                long expectedPixelCount,
                long actualPixelCount,
                byte rMin,
                byte rMax,
                byte gMin,
                byte gMax,
                byte bMin,
                byte bMax,
                byte aMin,
                byte aMax,
                int deepCarbonMetalPixelCount)
            {
                ExpectedPixelCount = expectedPixelCount;
                ActualPixelCount = actualPixelCount;
                RMin = rMin;
                RMax = rMax;
                GMin = gMin;
                GMax = gMax;
                BMin = bMin;
                BMax = bMax;
                AMin = aMin;
                AMax = aMax;
                DeepCarbonMetalPixelCount = deepCarbonMetalPixelCount;
            }
        }

        [MenuItem(MenuRoot + "Open Wreckage Burn Baker", false, 1727)]
        private static void Open()
        {
            WreckageTextureBaker window = GetWindow<WreckageTextureBaker>();
            window.titleContent = new GUIContent("Burn Baker 1727");
            window.minSize = new Vector2(500f, 460f);
        }

        [MenuItem(MenuRoot + "Bake Default Wreckage Burn Atlases", false, 1728)]
        private static void BakeDefaultMenu()
        {
            BakeSettings settings = BakeSettings.Default();
            if (TryBake(settings, out BakeResult result, out string failure))
            {
                Debug.Log("[WreckageTextureBaker1727] Baked albedo=" + result.AlbedoPath +
                          " mrao=" + result.MraoPath +
                          " us=" + result.DurationMicroseconds.ToString("0.0", CultureInfo.InvariantCulture));
            }
            else
            {
                Debug.LogError("[WreckageTextureBaker1727] " + failure);
            }
        }

        [MenuItem(MenuRoot + "Dry Run 64px Validator", false, 1729)]
        private static void DryRunMenu()
        {
            BakeSettings settings = BakeSettings.Default();
            if (TryDryRun(settings, out string summary, out string failure))
                Debug.Log("[WreckageTextureBaker1727] " + summary);
            else
                Debug.LogError("[WreckageTextureBaker1727] " + failure);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Wreckage Burn, Carbonization, Scrape Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _sourceMesh = (Mesh)EditorGUILayout.ObjectField("Agent 1717 Mesh", _sourceMesh, typeof(Mesh), false);
            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _computeOverride = (ComputeShader)EditorGUILayout.ObjectField("Compute Override", _computeOverride, typeof(ComputeShader), false);
            _curvatureMap = (Texture2D)EditorGUILayout.ObjectField("Curvature Map", _curvatureMap, typeof(Texture2D), false);
            _seed = EditorGUILayout.IntField("Seed", Mathf.Max(1, _seed));
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _explosionCenterUv = EditorGUILayout.Vector2Field("Explosion Center UV", _explosionCenterUv);
            _blastRadiusUv = EditorGUILayout.Slider("Blast Radius UV", _blastRadiusUv, 0.05f, 0.72f);
            _sootStrength = EditorGUILayout.Slider("Deep Soot", _sootStrength, 0f, 1f);
            _scrapeStrength = EditorGUILayout.Slider("Jagged Scrapes", _scrapeStrength, 0f, 1f);
            _paintSurvival = EditorGUILayout.Slider("Paint Survival", _paintSurvival, 0f, 1f);
            _thermalHaloStrength = EditorGUILayout.Slider("Thermal Halos", _thermalHaloStrength, 0f, 1f);

            EditorGUILayout.Space(6f);
            int albedoSize = ResolveTextureSize(MinimumAlbedoSize, MaximumAlbedoSize, _globalQualityWeight);
            int mraoSize = ResolveTextureSize(MinimumMraoSize, MaximumMraoSize, _globalQualityWeight);
            EditorGUILayout.LabelField("Albedo", albedoSize + " x " + albedoSize + " sRGB");
            EditorGUILayout.LabelField("MRAO", mraoSize + " x " + mraoSize + " linear BC7/ASTC");

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Wreckage Burn Atlases", GUILayout.Height(32f)))
                BakeFromWindow();

            if (GUILayout.Button("Dry Run Validator", GUILayout.Height(26f)))
                DryRunFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = BuildSettingsFromWindow();
            if (TryBake(settings, out BakeResult result, out string failure))
            {
                _lastStatus = "Baked " + result.AlbedoPath +
                              " | " + result.MraoPath +
                              " | us=" + result.DurationMicroseconds.ToString("0.0", CultureInfo.InvariantCulture);
            }
            else
            {
                _lastStatus = "Bake failed: " + failure;
            }
        }

        private void DryRunFromWindow()
        {
            BakeSettings settings = BuildSettingsFromWindow();
            if (TryDryRun(settings, out string summary, out string failure))
                _lastStatus = summary;
            else
                _lastStatus = "Dry run failed: " + failure;
        }

        private BakeSettings BuildSettingsFromWindow()
        {
            BakeSettings settings = BakeSettings.Default();
            settings.SourceMesh = _sourceMesh;
            settings.AssetName = _assetName;
            settings.OutputFolder = _outputFolder;
            settings.ComputeOverride = _computeOverride;
            settings.CurvatureMap = _curvatureMap;
            settings.Seed = unchecked((uint)Mathf.Max(1, _seed));
            settings.GlobalQualityWeight = _globalQualityWeight;
            settings.ExplosionCenterUv = new Vector2(
                Mathf.Repeat(_explosionCenterUv.x, 1f),
                Mathf.Repeat(_explosionCenterUv.y, 1f));
            settings.BlastRadiusUv = _blastRadiusUv;
            settings.SootStrength = _sootStrength;
            settings.ScrapeStrength = _scrapeStrength;
            settings.PaintSurvival = _paintSurvival;
            settings.ThermalHaloStrength = _thermalHaloStrength;
            return settings;
        }

        private static bool TryBake(BakeSettings settings, out BakeResult result, out string failure)
        {
            result = default;
            failure = string.Empty;

            if (!ValidateUnmanagedLayouts(out failure))
                return false;

            if (!ValidateSourceMesh(settings.SourceMesh, out failure))
                return false;

            if (!TryResolveCompute(settings, out ComputeShader compute, out failure))
                return false;

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(settings.OutputFolder, out string normalizedFolder, out failure))
                return false;

            string safeName = ResolveSafeAssetName(settings);
            int albedoSize = ResolveTextureSize(MinimumAlbedoSize, MaximumAlbedoSize, settings.GlobalQualityWeight);
            int mraoSize = ResolveTextureSize(MinimumMraoSize, MaximumMraoSize, settings.GlobalQualityWeight);
            string albedoPath = normalizedFolder + "/TX_Wreckage_Burn_" + safeName + "_Albedo.png";
            string mraoPath = normalizedFolder + "/TX_Wreckage_Burn_" + safeName + "_MRAO.png";

            if (!ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(albedoPath, mraoPath, out ProceduralTextureBaker.AssetFileRollbackSnapshot[] rollback, out failure))
                return false;

            Stopwatch stopwatch = Stopwatch.StartNew();
            Texture2D generatedCurvature = null;
            try
            {
                int curvatureSize = ResolveGeneratedCurvatureSize(settings.GlobalQualityWeight, mraoSize);
                if (!TryPrepareCurvatureMap(settings, curvatureSize, out BakeSettings dispatchSettings, out generatedCurvature, out failure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                if (!TryDispatchWriteImport(compute, "GenerateWreckageBurnAlbedo", dispatchSettings, albedoSize, TextureRole.Albedo, albedoPath, out BakeValidation albedoValidation, out failure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                if (!TryDispatchWriteImport(compute, "GenerateWreckageBurnMrao", dispatchSettings, mraoSize, TextureRole.Mrao, mraoPath, out BakeValidation mraoValidation, out failure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                if (!ProceduralTextureBaker.TryFinalizeAssetDatabase("wreckage burn bake 1727", out failure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                stopwatch.Stop();
                result = new BakeResult(albedoPath, mraoPath, albedoSize, mraoSize, stopwatch.Elapsed.TotalMilliseconds, albedoValidation, mraoValidation);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                stopwatch.Stop();
                failure = ex.GetType().Name + ": " + ex.Message;
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                return false;
            }
            finally
            {
                if (generatedCurvature != null)
                    Object.DestroyImmediate(generatedCurvature);
            }
        }

        private static bool TryDryRun(BakeSettings settings, out string summary, out string failure)
        {
            summary = string.Empty;
            failure = string.Empty;

            if (!ValidateUnmanagedLayouts(out failure))
                return false;

            if (!ValidateSourceMesh(settings.SourceMesh, out failure))
                return false;

            if (!TryResolveCompute(settings, out ComputeShader compute, out failure))
                return false;

            Texture2D albedo = null;
            Texture2D mrao = null;
            Texture2D generatedCurvature = null;
            try
            {
                if (!TryPrepareCurvatureMap(settings, DryRunSize, out BakeSettings dispatchSettings, out generatedCurvature, out failure))
                    return false;

                if (!TryRenderTexture(compute, "GenerateWreckageBurnAlbedo", dispatchSettings, DryRunSize, TextureRole.Albedo, out albedo, out BakeValidation albedoValidation, out failure))
                    return false;

                if (!TryRenderTexture(compute, "GenerateWreckageBurnMrao", dispatchSettings, DryRunSize, TextureRole.Mrao, out mrao, out BakeValidation mraoValidation, out failure))
                    return false;

                summary = "Dry run " + DryRunSize.ToString(CultureInfo.InvariantCulture) +
                          "px passed. albedoPixels=" + albedoValidation.ActualPixelCount.ToString(CultureInfo.InvariantCulture) +
                          " mraoPixels=" + mraoValidation.ActualPixelCount.ToString(CultureInfo.InvariantCulture) +
                          " carbonA=" + mraoValidation.AMin.ToString(CultureInfo.InvariantCulture) +
                          "-" + mraoValidation.AMax.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            finally
            {
                if (albedo != null)
                    Object.DestroyImmediate(albedo);
                if (mrao != null)
                    Object.DestroyImmediate(mrao);
                if (generatedCurvature != null)
                    Object.DestroyImmediate(generatedCurvature);
            }
        }

        private static bool TryResolveCompute(BakeSettings settings, out ComputeShader compute, out string failure)
        {
            compute = null;
            failure = string.Empty;
            if (!SystemInfo.supportsComputeShaders)
            {
                failure = "compute shaders are unsupported";
                return false;
            }

            compute = settings.ComputeOverride != null
                ? settings.ComputeOverride
                : LoadDefaultComputeShader();
            if (compute == null)
            {
                failure = "missing compute shader at " + ComputeShaderPath;
                return false;
            }

            if (!TryValidateKernelContract(compute, "GenerateWreckageBurnAlbedo", out failure))
                return false;

            return TryValidateKernelContract(compute, "GenerateWreckageBurnMrao", out failure);
        }

        private static ComputeShader LoadDefaultComputeShader()
        {
            AssetDatabase.ImportAsset(ComputeShaderPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderPath);
        }

        private static bool TryPrepareCurvatureMap(
            BakeSettings settings,
            int textureSize,
            out BakeSettings dispatchSettings,
            out Texture2D generatedCurvature,
            out string failure)
        {
            dispatchSettings = settings;
            generatedCurvature = null;
            failure = string.Empty;

            if (settings.CurvatureMap != null || settings.SourceMesh == null)
                return true;

            int curvatureSize = ResolveGeneratedCurvatureSize(settings.GlobalQualityWeight, textureSize);
            if (!TryBakeMeshCurvatureTexture(settings.SourceMesh, curvatureSize, out generatedCurvature, out failure))
                return false;

            dispatchSettings.CurvatureMap = generatedCurvature;
            return true;
        }

        private static bool TryBakeMeshCurvatureTexture(Mesh mesh, int textureSize, out Texture2D curvatureTexture, out string failure)
        {
            curvatureTexture = null;
            failure = string.Empty;

            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                int[] triangles = mesh.triangles;

                if (vertices == null || uvs == null || triangles == null ||
                    vertices.Length == 0 || uvs.Length != vertices.Length || triangles.Length < 3)
                {
                    failure = "source mesh CPU streams are incomplete for curvature bake";
                    return false;
                }

                bool hasNormals = normals != null && normals.Length == vertices.Length;
                int pixelCount = textureSize * textureSize;
                Color32[] pixels = new Color32[pixelCount];
                Bounds bounds = mesh.bounds;
                float meshScale = Mathf.Max(bounds.size.magnitude, 0.001f);

                for (int triangleIndex = 0; triangleIndex + 2 < triangles.Length; triangleIndex += 3)
                {
                    int i0 = triangles[triangleIndex];
                    int i1 = triangles[triangleIndex + 1];
                    int i2 = triangles[triangleIndex + 2];
                    if (!HasValidMeshIndex(i0, vertices.Length) ||
                        !HasValidMeshIndex(i1, vertices.Length) ||
                        !HasValidMeshIndex(i2, vertices.Length))
                    {
                        continue;
                    }

                    Vector3 p0 = vertices[i0];
                    Vector3 p1 = vertices[i1];
                    Vector3 p2 = vertices[i2];
                    Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
                    if (faceNormal.sqrMagnitude <= 0.0000001f)
                        continue;

                    faceNormal.Normalize();
                    DrawCurvatureEdge(pixels, textureSize, uvs[i0], uvs[i1], ResolveEdgeStress(p0, p1, hasNormals ? normals[i0] : faceNormal, hasNormals ? normals[i1] : faceNormal, faceNormal, hasNormals, meshScale));
                    DrawCurvatureEdge(pixels, textureSize, uvs[i1], uvs[i2], ResolveEdgeStress(p1, p2, hasNormals ? normals[i1] : faceNormal, hasNormals ? normals[i2] : faceNormal, faceNormal, hasNormals, meshScale));
                    DrawCurvatureEdge(pixels, textureSize, uvs[i2], uvs[i0], ResolveEdgeStress(p2, p0, hasNormals ? normals[i2] : faceNormal, hasNormals ? normals[i0] : faceNormal, faceNormal, hasNormals, meshScale));
                }

                SpreadCurvaturePixels(pixels, textureSize);
                if (!HasUsableCurvaturePixels(pixels))
                    return true;

                curvatureTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, true)
                {
                    name = "TX_WreckageBurn1727_MeshCurvature_" + mesh.name,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
                curvatureTexture.SetPixels32(pixels);
                curvatureTexture.Apply(false, true);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "mesh curvature bake failed for " + mesh.name + ": " + ex.GetType().Name + ": " + ex.Message;
                if (curvatureTexture != null)
                {
                    Object.DestroyImmediate(curvatureTexture);
                    curvatureTexture = null;
                }

                return false;
            }
        }

        private static bool HasValidMeshIndex(int index, int vertexCount)
        {
            return index >= 0 && index < vertexCount;
        }

        private static byte ResolveEdgeStress(
            Vector3 a,
            Vector3 b,
            Vector3 normalA,
            Vector3 normalB,
            Vector3 faceNormal,
            bool hasNormals,
            float meshScale)
        {
            float normalBreak = 0f;
            float faceBreak = 0f;
            if (hasNormals && normalA.sqrMagnitude > 0.000001f && normalB.sqrMagnitude > 0.000001f)
            {
                Vector3 na = normalA.normalized;
                Vector3 nb = normalB.normalized;
                normalBreak = 1f - Mathf.Clamp01(Vector3.Dot(na, nb));
                faceBreak = Mathf.Max(
                    1f - Mathf.Clamp01(Vector3.Dot(na, faceNormal)),
                    1f - Mathf.Clamp01(Vector3.Dot(nb, faceNormal)));
            }

            float edgeLength = (b - a).magnitude;
            float lengthBreak = Mathf.Clamp01(edgeLength / Mathf.Max(0.001f, meshScale * 0.075f));
            float stress = normalBreak * 0.66f + faceBreak * 0.42f + lengthBreak * 0.10f;
            if (stress < 0.18f)
                return 0;

            return (byte)Mathf.Clamp(Mathf.RoundToInt(stress * 255f), 32, 255);
        }

        private static bool HasUsableCurvaturePixels(Color32[] pixels)
        {
            if (pixels == null)
                return false;

            int markedPixels = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r <= 24)
                    continue;

                markedPixels++;
                if (markedPixels >= 16)
                    return true;
            }

            return false;
        }

        private static void DrawCurvatureEdge(Color32[] pixels, int textureSize, Vector2 uvA, Vector2 uvB, byte strength)
        {
            if (strength == 0 || pixels == null || textureSize <= 0)
                return;

            Vector2 a = Repeat01(uvA);
            Vector2 b = Repeat01(uvB);
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            if (dx > 0.5f)
                b.x -= 1f;
            else if (dx < -0.5f)
                b.x += 1f;

            if (dy > 0.5f)
                b.y -= 1f;
            else if (dy < -0.5f)
                b.y += 1f;

            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) * textureSize));
            for (int step = 0; step <= steps; step++)
            {
                float t = (float)step / steps;
                int x = WrapTexel(Mathf.FloorToInt(Mathf.Lerp(a.x, b.x, t) * textureSize), textureSize);
                int y = WrapTexel(Mathf.FloorToInt(Mathf.Lerp(a.y, b.y, t) * textureSize), textureSize);
                StampCurvature(pixels, textureSize, x, y, strength);
            }
        }

        private static Vector2 Repeat01(Vector2 uv)
        {
            return new Vector2(Mathf.Repeat(uv.x, 1f), Mathf.Repeat(uv.y, 1f));
        }

        private static int WrapTexel(int value, int textureSize)
        {
            int wrapped = value % textureSize;
            return wrapped < 0 ? wrapped + textureSize : wrapped;
        }

        private static void StampCurvature(Color32[] pixels, int textureSize, int centerX, int centerY, byte strength)
        {
            for (int y = -1; y <= 1; y++)
            {
                int py = WrapTexel(centerY + y, textureSize);
                for (int x = -1; x <= 1; x++)
                {
                    int px = WrapTexel(centerX + x, textureSize);
                    int falloff = (Mathf.Abs(x) + Mathf.Abs(y)) * 34;
                    int value = Mathf.Max(0, strength - falloff);
                    if (value <= 0)
                        continue;

                    int index = py * textureSize + px;
                    if (value <= pixels[index].r)
                        continue;

                    byte v = (byte)value;
                    pixels[index] = new Color32(v, v, v, 255);
                }
            }
        }

        private static void SpreadCurvaturePixels(Color32[] pixels, int textureSize)
        {
            if (pixels == null || pixels.Length == 0)
                return;

            Color32[] source = new Color32[pixels.Length];
            Array.Copy(pixels, source, pixels.Length);
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    int index = y * textureSize + x;
                    int best = source[index].r;
                    best = Mathf.Max(best, source[y * textureSize + WrapTexel(x - 1, textureSize)].r - 28);
                    best = Mathf.Max(best, source[y * textureSize + WrapTexel(x + 1, textureSize)].r - 28);
                    best = Mathf.Max(best, source[WrapTexel(y - 1, textureSize) * textureSize + x].r - 28);
                    best = Mathf.Max(best, source[WrapTexel(y + 1, textureSize) * textureSize + x].r - 28);
                    byte v = (byte)Mathf.Clamp(best, 0, 255);
                    pixels[index] = new Color32(v, v, v, 255);
                }
            }
        }

        private static bool TryDispatchWriteImport(
            ComputeShader compute,
            string kernelName,
            BakeSettings settings,
            int textureSize,
            TextureRole role,
            string assetPath,
            out BakeValidation validation,
            out string failure)
        {
            validation = default;
            failure = string.Empty;
            Texture2D staging = null;
            try
            {
                if (!TryRenderTexture(compute, kernelName, settings, textureSize, role, out staging, out validation, out failure))
                    return false;

                byte[] png = ImageConversion.EncodeToPNG(staging);
                if (png == null || png.Length == 0)
                {
                    failure = "EncodeToPNG returned no bytes for " + assetPath;
                    return false;
                }

                if (png.LongLength > MaxEncodedPngBytes)
                {
                    failure = "encoded PNG byte ceiling exceeded for " + assetPath;
                    return false;
                }

                if (!ProceduralTextureBaker.TryWriteBytesAtomic(assetPath, png, out failure))
                    return false;

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                return ProceduralTextureBaker.TryEnforceTextureImportSettings(
                    assetPath,
                    role == TextureRole.Albedo,
                    false,
                    TextureWrapMode.Repeat,
                    FilterMode.Trilinear,
                    textureSize,
                    TextureImporterFormat.BC7,
                    true,
                    role == TextureRole.Albedo ? 4 : 2,
                    out failure);
            }
            finally
            {
                if (staging != null)
                    Object.DestroyImmediate(staging);
            }
        }

        private static bool TryRenderTexture(
            ComputeShader compute,
            string kernelName,
            BakeSettings settings,
            int textureSize,
            TextureRole role,
            out Texture2D staging,
            out BakeValidation validation,
            out string failure)
        {
            staging = null;
            validation = default;
            failure = string.Empty;

            if (!TryResolveKernel(compute, kernelName, out int kernel, out failure))
                return false;

            compute.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out _);
            if (!ProceduralTextureBaker.TryResolveDispatchGroups(textureSize, groupSizeX, groupSizeY, out int groupsX, out int groupsY, out failure))
                return false;

            RenderTexture rt = null;
            RenderTexture previous = RenderTexture.active;
            bool keepStaging = false;
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.ARGB32, 0)
                {
                    enableRandomWrite = true,
                    mipCount = 1,
                    msaaSamples = 1,
                    sRGB = role == TextureRole.Albedo
                };

                rt = new RenderTexture(descriptor)
                {
                    name = "RT_WreckageBurn1727_" + kernelName,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };

                if (!rt.Create())
                {
                    failure = "RenderTexture allocation failed for " + kernelName + " at " + textureSize.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                float q = Mathf.Clamp01(settings.GlobalQualityWeight);
                Texture2D curvature = settings.CurvatureMap != null ? settings.CurvatureMap : Texture2D.blackTexture;
                compute.SetTexture(kernel, s_outputId, rt);
                compute.SetTexture(kernel, s_curvatureMapId, curvature);
                compute.SetVector(s_textureSizeId, new Vector4(textureSize, textureSize, settings.CurvatureMap != null ? 1f : 0f, role == TextureRole.Mrao ? 1f : 0f));
                compute.SetInt(s_seedId, unchecked((int)(settings.Seed == 0u ? 1u : settings.Seed)));
                compute.SetVector(s_blastParamsId, new Vector4(
                    Mathf.Repeat(settings.ExplosionCenterUv.x, 1f),
                    Mathf.Repeat(settings.ExplosionCenterUv.y, 1f),
                    Mathf.Clamp(settings.BlastRadiusUv, 0.05f, 0.72f),
                    Mathf.Lerp(0.035f, 0.135f, q)));
                compute.SetVector(s_layerParamsId, new Vector4(
                    Mathf.Clamp01(settings.SootStrength),
                    Mathf.Clamp01(settings.ScrapeStrength),
                    Mathf.Clamp01(settings.PaintSurvival),
                    q));
                compute.SetVector(s_patternParamsId, new Vector4(
                    Mathf.Lerp(0.12f, 0.70f, q),
                    Mathf.Lerp(32f, 160f, q),
                    Mathf.Lerp(0.36f, 0.90f, Mathf.Clamp01(settings.ScrapeStrength)),
                    Mathf.Clamp01(settings.ThermalHaloStrength)));

                compute.Dispatch(kernel, groupsX, groupsY, 1);
                RenderTexture.active = rt;
                staging = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, role == TextureRole.Mrao);
                staging.ReadPixels(new Rect(0f, 0f, textureSize, textureSize), 0, 0, false);
                staging.Apply(false, false);

                Color32[] pixels = staging.GetPixels32();
                if (!ValidatePixels(pixels, textureSize, role, out validation, out failure))
                    return false;

                keepStaging = true;
                return true;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null)
                    Object.DestroyImmediate(rt);
                if (!keepStaging && staging != null)
                {
                    Object.DestroyImmediate(staging);
                    staging = null;
                }
            }
        }

        private static bool ValidatePixels(Color32[] pixels, int textureSize, TextureRole role, out BakeValidation validation, out string failure)
        {
            validation = default;
            failure = string.Empty;

            long expected = (long)textureSize * textureSize;
            long actual = pixels == null ? 0L : pixels.LongLength;
            if (actual != expected)
            {
                failure = "pixel count mismatch expected=" + expected.ToString(CultureInfo.InvariantCulture) +
                          " actual=" + actual.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            byte rMin = byte.MaxValue;
            byte rMax = 0;
            byte gMin = byte.MaxValue;
            byte gMax = 0;
            byte bMin = byte.MaxValue;
            byte bMax = 0;
            byte aMin = byte.MaxValue;
            byte aMax = 0;
            int deepCarbonMetalPixels = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.r < rMin)
                    rMin = pixel.r;
                if (pixel.r > rMax)
                    rMax = pixel.r;
                if (pixel.g < gMin)
                    gMin = pixel.g;
                if (pixel.g > gMax)
                    gMax = pixel.g;
                if (pixel.b < bMin)
                    bMin = pixel.b;
                if (pixel.b > bMax)
                    bMax = pixel.b;
                if (pixel.a < aMin)
                    aMin = pixel.a;
                if (pixel.a > aMax)
                    aMax = pixel.a;

                if (role == TextureRole.Mrao && pixel.a > 220 && pixel.r > 180)
                    deepCarbonMetalPixels++;
            }

            validation = new BakeValidation(expected, actual, rMin, rMax, gMin, gMax, bMin, bMax, aMin, aMax, deepCarbonMetalPixels);
            if (role == TextureRole.Albedo)
            {
                int redRange = rMax - rMin;
                int greenRange = gMax - gMin;
                int blueRange = bMax - bMin;
                if (redRange + greenRange + blueRange < 48)
                {
                    failure = "albedo variation is flat; rgbRangeSum=" + (redRange + greenRange + blueRange).ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                if (aMin < 250)
                {
                    failure = "albedo alpha must remain opaque; minA=" + aMin.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                return ValidateSeamContinuity(pixels, textureSize, role, out failure);
            }

            if (rMax < 112)
            {
                failure = "MRAO metallic channel never exposes heat-stripped steel; max=" + rMax.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (gMax - gMin < 32)
            {
                failure = "MRAO roughness channel is flat; min=" + gMin.ToString(CultureInfo.InvariantCulture) +
                          " max=" + gMax.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (bMax - bMin < 14)
            {
                failure = "MRAO AO channel is flat; min=" + bMin.ToString(CultureInfo.InvariantCulture) +
                          " max=" + bMax.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (aMax < 128)
            {
                failure = "MRAO carbonization alpha never reaches deep burn; max=" + aMax.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            int invalidCeiling = Mathf.Max(1, (int)(expected / 96L));
            if (deepCarbonMetalPixels > invalidCeiling)
            {
                failure = "MRAO violates carbonized-steel contract; deepCarbonMetalPixels=" +
                          deepCarbonMetalPixels.ToString(CultureInfo.InvariantCulture) +
                          " ceiling=" + invalidCeiling.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return ValidateSeamContinuity(pixels, textureSize, role, out failure);
        }

        private static bool ValidateSeamContinuity(Color32[] pixels, int textureSize, TextureRole role, out string failure)
        {
            failure = string.Empty;
            if (pixels == null || textureSize <= 1)
                return true;

            int tolerance = role == TextureRole.Albedo ? 72 : 48;
            int allowedFailures = Mathf.Max(1, textureSize >> 4);
            int failedEdges = 0;
            int maxDelta = 0;
            int last = textureSize - 1;
            for (int i = 0; i < textureSize; i++)
            {
                int horizontal = ResolveSeamDelta(pixels[i * textureSize], pixels[i * textureSize + last], role);
                if (horizontal > maxDelta)
                    maxDelta = horizontal;
                if (horizontal > tolerance)
                    failedEdges++;

                int vertical = ResolveSeamDelta(pixels[i], pixels[last * textureSize + i], role);
                if (vertical > maxDelta)
                    maxDelta = vertical;
                if (vertical > tolerance)
                    failedEdges++;
            }

            if (failedEdges <= allowedFailures)
                return true;

            failure = (role == TextureRole.Albedo ? "albedo" : "MRAO") +
                      " repeat seam exceeds tolerance; failedEdges=" + failedEdges.ToString(CultureInfo.InvariantCulture) +
                      " allowed=" + allowedFailures.ToString(CultureInfo.InvariantCulture) +
                      " maxDelta=" + maxDelta.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        private static int ResolveSeamDelta(Color32 a, Color32 b, TextureRole role)
        {
            int max = Mathf.Abs(a.r - b.r);
            int g = Mathf.Abs(a.g - b.g);
            if (g > max)
                max = g;
            int blue = Mathf.Abs(a.b - b.b);
            if (blue > max)
                max = blue;
            if (role == TextureRole.Mrao)
            {
                int alpha = Mathf.Abs(a.a - b.a);
                if (alpha > max)
                    max = alpha;
            }

            return max;
        }

        private static bool TryValidateKernelContract(ComputeShader compute, string kernelName, out string failure)
        {
            if (!TryResolveKernel(compute, kernelName, out int kernel, out failure))
                return false;

            compute.GetKernelThreadGroupSizes(kernel, out uint threadX, out uint threadY, out uint threadZ);
            ulong totalThreads = (ulong)threadX * threadY * threadZ;
            if (threadX == 0u || threadY == 0u || threadZ == 0u || threadZ != 1u || totalThreads > ComputeKernelThreadLimit)
            {
                failure = "compute kernel thread contract failed for " + kernelName +
                          "; x=" + threadX.ToString(CultureInfo.InvariantCulture) +
                          " y=" + threadY.ToString(CultureInfo.InvariantCulture) +
                          " z=" + threadZ.ToString(CultureInfo.InvariantCulture) +
                          " total=" + totalThreads.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        private static bool TryResolveKernel(ComputeShader compute, string kernelName, out int kernel, out string failure)
        {
            kernel = -1;
            failure = string.Empty;
            try
            {
                if (!compute.HasKernel(kernelName))
                {
                    failure = "missing compute kernel " + kernelName;
                    return false;
                }

                kernel = compute.FindKernel(kernelName);
                return kernel >= 0;
            }
            catch (Exception ex) when (ex is UnityException || ex is ArgumentException || ex is InvalidOperationException)
            {
                failure = "compute kernel resolve failed for " + kernelName + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static int ResolveTextureSize(int minimum, int maximum, float globalQualityWeight)
        {
            float q = Mathf.Clamp01(globalQualityWeight);
            float continuous = Mathf.Lerp(minimum, maximum, q * q);
            return Mathf.Clamp(RoundUpPowerOfTwo(Mathf.CeilToInt(continuous)), minimum, maximum);
        }

        private static int ResolveGeneratedCurvatureSize(float globalQualityWeight, int targetTextureSize)
        {
            int qualitySize = ResolveTextureSize(MinimumGeneratedCurvatureSize, MaximumGeneratedCurvatureSize, globalQualityWeight);
            return Mathf.Clamp(Mathf.Min(qualitySize, targetTextureSize), MinimumGeneratedCurvatureSize, MaximumGeneratedCurvatureSize);
        }

        private static int RoundUpPowerOfTwo(int value)
        {
            int v = Mathf.Max(1, value);
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        private static string ResolveSafeAssetName(BakeSettings settings)
        {
            string raw = !string.IsNullOrWhiteSpace(settings.AssetName)
                ? settings.AssetName
                : settings.SourceMesh != null ? settings.SourceMesh.name : string.Empty;
            string safe = ProceduralTextureBaker.SanitizeAssetNameForPath(raw);
            if (!string.IsNullOrEmpty(safe))
                return safe;

            return "wreckage_burn_" + (settings.Seed == 0u ? 1u : settings.Seed).ToString("X8", CultureInfo.InvariantCulture);
        }

        private static bool ValidateSourceMesh(Mesh mesh, out string failure)
        {
            failure = string.Empty;
            if (mesh == null)
                return true;

            if (mesh.vertexCount <= 0)
            {
                failure = "source mesh has no vertices";
                return false;
            }

            if (!mesh.HasVertexAttribute(VertexAttribute.Position))
            {
                failure = "source mesh has no position stream";
                return false;
            }

            if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                failure = "source mesh has no UV0 stream for deterministic burn atlas projection";
                return false;
            }

            return true;
        }

        private static bool ValidateUnmanagedLayouts(out string failure)
        {
            failure = string.Empty;
            int validationSize = UnsafeUtility.SizeOf<BakeValidation>();
            if ((validationSize & 7) == 0)
                return true;

            failure = "BakeValidation size must be 8-byte aligned; size=" + validationSize.ToString(CultureInfo.InvariantCulture);
            return false;
        }

    }
}
#endif
