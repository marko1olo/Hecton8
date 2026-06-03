#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Bakers
{
    public sealed class GeologicalStrataBaker1724 : EditorWindow
    {
        private const string MenuRoot = "HECTON-8/Bakers/1724/";
        private const string ComputePath = "Assets/_Project/Art/Shaders/Include/GeologicalStrataBaker1724.compute";
        private const string DefaultOutputFolder = "Assets/_Project/Art/Textures/Geology";
        private const string FirstPartyOutputRoot = "Assets/_Project/";
        private const int MinimumAlbedoResolution = 1024;
        private const int MaximumAlbedoResolution = 4096;
        private const int MinimumMraoResolution = 512;
        private const int MaximumMraoResolution = 2048;
        private const int ThreadGroupSize = 8;
        private const float MinimumOreIntensity = 0.05f;
        private const float MinimumSedimentStrength = 0.05f;
        private static readonly int ParamsId = Shader.PropertyToID("_GeologyBakeParams1724");
        private static readonly int AlbedoOutputId = Shader.PropertyToID("_GeologyAlbedoOutput");
        private static readonly int MraoOutputId = Shader.PropertyToID("_GeologyMraoOutput");
        private static readonly int OutputModeId = Shader.PropertyToID("_GeologyOutputMode1724");
        private static readonly int MaterialAlbedoMapId = Shader.PropertyToID("_GeologyStrataAlbedoMap");
        private static readonly int MaterialMraoMapId = Shader.PropertyToID("_GeologyStrataMraoMap");
        private static readonly int MaterialBlendId = Shader.PropertyToID("_GeologyStrataBlend");
        private static readonly int MaterialWorldOriginAupId = Shader.PropertyToID("_GeologyWorldOriginAup");
        private static readonly int MaterialTileMetersId = Shader.PropertyToID("_GeologyTileMeters");
        private static readonly GeologyBakeParams1724[] s_paramsPayload = new GeologyBakeParams1724[1];

        private static RenderTexture s_transientAlbedo;
        private static RenderTexture s_transientMrao;
        private static GraphicsBuffer s_transientParams;

        [SerializeField] private string assetName = "abyssal_strata";
        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private ComputeShader strataCompute;
        [SerializeField] private Mesh sourceRockMesh;
        [SerializeField] private Material targetMaterial;
        [SerializeField, Range(0f, 1f)] private float globalQualityWeight = 0.72f;
        [SerializeField] private Vector3 worldOriginAup = new Vector3(0f, -800f, 0f);
        [SerializeField] private Vector2 tileMeters = new Vector2(64f, 95f);
        [SerializeField, Range(0.5f, 48f)] private float strataPeriodMeters = 9.5f;
        [SerializeField, Range(0f, 12f)] private float warpMeters = 3.25f;
        [SerializeField, Range(2f, 72f)] private float fractureDensity = 18f;
        [SerializeField, Range(MinimumOreIntensity, 1f)] private float oreIntensity = 0.78f;
        [SerializeField, Range(MinimumSedimentStrength, 1f)] private float sedimentStrength = 0.66f;
        [SerializeField] private uint seed = 1724u;
        private string lastStatus = "Idle.";

        [MenuItem(MenuRoot + "Open Geological Strata Baker", false, 1724)]
        private static void Open()
        {
            GeologicalStrataBaker1724 window = GetWindow<GeologicalStrataBaker1724>();
            window.titleContent = new GUIContent("Geology Baker 1724");
            window.minSize = new Vector2(500f, 470f);
            if (window.strataCompute == null)
                window.strataCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        }

        [MenuItem(MenuRoot + "Bake Default Abyssal Strata", false, 1725)]
        private static void BakeDefaultMenu()
        {
            BakeSettings settings = BakeSettings.Default();
            if (TryBake(settings, out BakeResult result))
            {
                Debug.Log("[GeologicalStrataBaker1724] Baked geology textures: " +
                          result.AlbedoPath +
                          " | " +
                          result.MraoPath +
                          " | us=" +
                          result.ElapsedMicroseconds.ToString("0.0", CultureInfo.InvariantCulture));
            }
        }

        private void OnEnable()
        {
            if (strataCompute == null)
                strataCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        }

        private void OnDisable()
        {
            ReleaseTransientGpuState();
            strataCompute = null;
        }

        private void OnDestroy()
        {
            ReleaseTransientGpuState();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Offline Geological Strata and Mineral Vein Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            assetName = EditorGUILayout.TextField("Asset Name", assetName);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            strataCompute = (ComputeShader)EditorGUILayout.ObjectField("Compute", strataCompute, typeof(ComputeShader), false);
            sourceRockMesh = (Mesh)EditorGUILayout.ObjectField("Source Rock Mesh", sourceRockMesh, typeof(Mesh), false);
            targetMaterial = (Material)EditorGUILayout.ObjectField("Target Material", targetMaterial, typeof(Material), false);
            globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", globalQualityWeight, 0f, 1f);
            worldOriginAup = EditorGUILayout.Vector3Field("AUP Origin", worldOriginAup);
            tileMeters = EditorGUILayout.Vector2Field("Tile Meters X/Y", tileMeters);
            strataPeriodMeters = EditorGUILayout.Slider("Strata Period Meters", strataPeriodMeters, 0.5f, 48f);
            warpMeters = EditorGUILayout.Slider("Warp Meters", warpMeters, 0f, 12f);
            fractureDensity = EditorGUILayout.Slider("Fracture Density", fractureDensity, 2f, 72f);
            oreIntensity = EditorGUILayout.Slider("Ore Intensity", oreIntensity, MinimumOreIntensity, 1f);
            sedimentStrength = EditorGUILayout.Slider("Sediment Strength", sedimentStrength, MinimumSedimentStrength, 1f);
            seed = (uint)Mathf.Max(1, EditorGUILayout.IntField("Seed", unchecked((int)seed)));

            ResolvedDimensions dimensions = ResolveDimensions(globalQualityWeight);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Albedo", dimensions.AlbedoResolution.ToString(CultureInfo.InvariantCulture) + " px");
            EditorGUILayout.LabelField("Packed MRAO", dimensions.MraoResolution.ToString(CultureInfo.InvariantCulture) + " px");

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Geological Texture Set", GUILayout.Height(32f)))
                BakeFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(lastStatus, MessageType.Info);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = new BakeSettings(
                assetName,
                outputFolder,
                strataCompute,
                sourceRockMesh,
                targetMaterial,
                globalQualityWeight,
                worldOriginAup,
                tileMeters,
                strataPeriodMeters,
                warpMeters,
                fractureDensity,
                oreIntensity,
                sedimentStrength,
                seed);

            if (TryBake(settings, out BakeResult result))
            {
                lastStatus = "Baked " +
                             result.AlbedoPath +
                             " | " +
                             result.MraoPath +
                             " | us=" +
                             result.ElapsedMicroseconds.ToString("0.0", CultureInfo.InvariantCulture);
                return;
            }

            lastStatus = "Bake failed. Check Console.";
        }

        public static bool TryBake(in BakeSettings requestedSettings, out BakeResult result)
        {
            result = default;
            BakeSettings settings = requestedSettings.Sanitize();
            ResolvedDimensions dimensions = ResolveDimensions(settings.GlobalQualityWeight);

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError("[GeologicalStrataBaker1724] Compute shaders unsupported on this editor device.");
                return false;
            }

            ComputeShader compute = settings.StrataCompute != null
                ? settings.StrataCompute
                : AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            if (compute == null)
            {
                Debug.LogError("[GeologicalStrataBaker1724] Missing compute shader at " + ComputePath);
                return false;
            }

            if (!compute.HasKernel("CSBakeGeology1724"))
            {
                Debug.LogError("[GeologicalStrataBaker1724] Missing CSBakeGeology1724 kernel.");
                return false;
            }

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(settings.OutputFolder, out string safeOutputFolder, out string folderFailure))
            {
                Debug.LogError("[GeologicalStrataBaker1724] Output folder invalid: " + folderFailure);
                return false;
            }

            if (!safeOutputFolder.StartsWith(FirstPartyOutputRoot, StringComparison.Ordinal))
            {
                Debug.LogError("[GeologicalStrataBaker1724] Output folder must stay under " + FirstPartyOutputRoot + ": " + safeOutputFolder);
                return false;
            }

            string safeName = ProceduralTextureBaker.SanitizeAssetNameForPath(settings.AssetName);
            if (string.IsNullOrEmpty(safeName))
                safeName = "abyssal_strata";

            string albedoPath = safeOutputFolder + "/TX_Geology_" + safeName + "_Albedo.png";
            string mraoPath = safeOutputFolder + "/TX_Geology_" + safeName + "_MRAO.png";
            if (!TryCaptureMaterialBindingSnapshot(settings.TargetMaterial, out MaterialBindingSnapshot materialSnapshot, out string materialContractFailure))
            {
                Debug.LogError("[GeologicalStrataBaker1724] Material contract invalid: " + materialContractFailure);
                return false;
            }

            if (!ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(albedoPath, mraoPath, out ProceduralTextureBaker.AssetFileRollbackSnapshot[] rollback, out string rollbackFailure))
            {
                Debug.LogError("[GeologicalStrataBaker1724] Output rollback capture failed: " + rollbackFailure);
                return false;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            Texture2D albedoTexture = null;
            Texture2D mraoTexture = null;
            string validationSummary = string.Empty;
            bool materialApplied = false;

            try
            {
                ReleaseTransientGpuState();
                int kernel = compute.FindKernel("CSBakeGeology1724");
                s_transientAlbedo = CreateTarget(dimensions.AlbedoResolution, GraphicsFormat.R8G8B8A8_UNorm, "H8_1724_GeologyAlbedo");
                s_transientMrao = CreateTarget(dimensions.MraoResolution, GraphicsFormat.R8G8B8A8_UNorm, "H8_1724_GeologyMRAO");
                GeologyBakeParams1724 parameters = settings.ToParams();
                s_transientParams = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, ResolveGeologyParamsStride());
                s_paramsPayload[0] = parameters;
                s_transientParams.SetData(s_paramsPayload);

                compute.SetBuffer(kernel, ParamsId, s_transientParams);
                compute.SetTexture(kernel, AlbedoOutputId, s_transientAlbedo);
                compute.SetTexture(kernel, MraoOutputId, s_transientMrao);

                DispatchOutput(compute, kernel, dimensions.AlbedoResolution, 0);
                DispatchOutput(compute, kernel, dimensions.MraoResolution, 1);

                albedoTexture = ReadRenderTexture(s_transientAlbedo, TextureFormat.RGBA32, true);
                mraoTexture = ReadRenderTexture(s_transientMrao, TextureFormat.RGBA32, true);

                bool albedoPixelCountValid = ValidatePixelCount(albedoTexture, dimensions.AlbedoResolution, out string albedoPixelFailure);
                string mraoPixelFailure = string.Empty;
                bool mraoPixelCountValid = albedoPixelCountValid &&
                                           ValidatePixelCount(mraoTexture, dimensions.MraoResolution, out mraoPixelFailure);
                if (!albedoPixelCountValid || !mraoPixelCountValid)
                {
                    Debug.LogError("[GeologicalStrataBaker1724] Pixel count validation failed: " + albedoPixelFailure + mraoPixelFailure);
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                if (!ValidateMraoTexture(mraoTexture, dimensions.MraoResolution, out validationSummary))
                {
                    Debug.LogError("[GeologicalStrataBaker1724] PBR mask validation violation detected! " + validationSummary);
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                byte[] albedoPng = ImageConversion.EncodeToPNG(albedoTexture);
                byte[] mraoPng = ImageConversion.EncodeToPNG(mraoTexture);
                if (albedoPng == null || albedoPng.Length == 0 || mraoPng == null || mraoPng.Length == 0)
                {
                    Debug.LogError("[GeologicalStrataBaker1724] PNG encode failed.");
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                bool albedoWritten = ProceduralTextureBaker.TryWriteBytesAtomic(albedoPath, albedoPng, out string albedoWriteFailure);
                string mraoWriteFailure = string.Empty;
                bool mraoWritten = albedoWritten &&
                                   ProceduralTextureBaker.TryWriteBytesAtomic(mraoPath, mraoPng, out mraoWriteFailure);
                if (!albedoWritten || !mraoWritten)
                {
                    Debug.LogError("[GeologicalStrataBaker1724] Texture write failed: " + albedoWriteFailure + mraoWriteFailure);
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                bool albedoImported = ProceduralTextureBaker.TryEnforceTextureImportSettings(albedoPath, true, false, TextureWrapMode.Repeat, FilterMode.Bilinear, dimensions.AlbedoResolution, TextureImporterFormat.BC7, out string albedoImportFailure);
                string mraoImportFailure = string.Empty;
                bool mraoImported = albedoImported &&
                                    ProceduralTextureBaker.TryEnforceTextureImportSettings(mraoPath, false, false, TextureWrapMode.Repeat, FilterMode.Bilinear, dimensions.MraoResolution, TextureImporterFormat.BC7, out mraoImportFailure);
                if (!albedoImported || !mraoImported)
                {
                    Debug.LogError("[GeologicalStrataBaker1724] Import settings failed: " + albedoImportFailure + mraoImportFailure);
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                materialApplied = materialSnapshot.Captured;
                if (!TryApplyMaterialBindings(in materialSnapshot, albedoPath, mraoPath, in settings, out string materialFailure))
                {
                    Debug.LogError("[GeologicalStrataBaker1724] Material binding failed: " + materialFailure);
                    if (materialApplied)
                        RestoreMaterialBindings(in materialSnapshot);
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                if (!ProceduralTextureBaker.TryFinalizeAssetDatabase("geological strata bake 1724", out string finalizeFailure))
                {
                    Debug.LogError("[GeologicalStrataBaker1724] AssetDatabase finalize failed: " + finalizeFailure);
                    if (materialApplied)
                        RestoreMaterialBindings(in materialSnapshot);
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                stopwatch.Stop();
                result = new BakeResult(
                    albedoPath,
                    mraoPath,
                    dimensions.AlbedoResolution,
                    dimensions.MraoResolution,
                    stopwatch.Elapsed.TotalMilliseconds * 1000.0,
                    validationSummary);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                Debug.LogError("[GeologicalStrataBaker1724] Bake failed: " + ex.GetType().Name + ": " + ex.Message);
                if (materialApplied)
                    RestoreMaterialBindings(in materialSnapshot);
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                return false;
            }
            finally
            {
                if (albedoTexture != null)
                    DestroyImmediate(albedoTexture);
                if (mraoTexture != null)
                    DestroyImmediate(mraoTexture);
                ReleaseTransientGpuState();
            }
        }

        private static RenderTexture CreateTarget(int size, GraphicsFormat format, string name)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(size, size, format, 0)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                msaaSamples = 1,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            RenderTexture target = new RenderTexture(descriptor)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            if (!target.Create())
                throw new InvalidOperationException("RenderTexture allocation failed for " + name + " at " + size.ToString(CultureInfo.InvariantCulture));
            return target;
        }

        private static Texture2D ReadRenderTexture(RenderTexture source, TextureFormat format, bool linear)
        {
            Texture2D texture = new Texture2D(source.width, source.height, format, true, linear)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                texture.Apply(true, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            return texture;
        }

        private static int ResolveGeologyParamsStride()
        {
            int stride = UnsafeUtility.SizeOf<GeologyBakeParams1724>();
            if ((stride & 7) != 0)
                throw new InvalidOperationException("GeologyBakeParams1724 stride is not 8-byte aligned.");

            return stride;
        }

        private static void DispatchOutput(ComputeShader compute, int kernel, int resolution, int outputMode)
        {
            compute.SetInt(OutputModeId, outputMode);
            compute.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out _);
            int groupsX = Mathf.CeilToInt(resolution / (float)Mathf.Max(1, (int)groupSizeX));
            int groupsY = Mathf.CeilToInt(resolution / (float)Mathf.Max(1, (int)groupSizeY));
            if (groupsX <= 0 || groupsY <= 0)
                throw new InvalidOperationException("Invalid compute dispatch shape for resolution " + resolution.ToString(CultureInfo.InvariantCulture));
            compute.Dispatch(kernel, groupsX, groupsY, 1);
        }

        private static bool ValidatePixelCount(Texture2D texture, int expectedResolution, out string failure)
        {
            failure = string.Empty;
            if (texture == null)
            {
                failure = "texture null; ";
                return false;
            }

            long expected = (long)expectedResolution * expectedResolution;
            long actual = (long)texture.width * texture.height;
            if (actual == expected)
                return true;

            failure = "expectedPixels=" + expected.ToString(CultureInfo.InvariantCulture) +
                      " actualPixels=" + actual.ToString(CultureInfo.InvariantCulture) + "; ";
            return false;
        }

        private static bool ValidateMraoTexture(Texture2D texture, int expectedResolution, out string summary)
        {
            summary = string.Empty;
            if (!ValidatePixelCount(texture, expectedResolution, out string pixelFailure))
            {
                summary = pixelFailure;
                return false;
            }

            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            int metallicPixels = 0;
            int sedimentPixels = 0;
            int hostSmoothViolations = 0;
            int oreRoughViolations = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                float metallic = p.r * (1f / 255f);
                float roughness = p.g * (1f / 255f);
                float sediment = p.a * (1f / 255f);

                if (metallic > 0.15f)
                    metallicPixels++;
                if (sediment > 0.08f)
                    sedimentPixels++;
                if (metallic > 0.35f && roughness > 0.72f)
                    oreRoughViolations++;
                if (metallic < 0.03f && roughness < 0.35f)
                    hostSmoothViolations++;
            }

            summary = "pixels=" + pixels.Length.ToString(CultureInfo.InvariantCulture) +
                      " metallicPixels=" + metallicPixels.ToString(CultureInfo.InvariantCulture) +
                      " sedimentPixels=" + sedimentPixels.ToString(CultureInfo.InvariantCulture) +
                      " hostSmoothViolations=" + hostSmoothViolations.ToString(CultureInfo.InvariantCulture) +
                      " oreRoughViolations=" + oreRoughViolations.ToString(CultureInfo.InvariantCulture);

            return metallicPixels > 0 &&
                   sedimentPixels > 0 &&
                   hostSmoothViolations == 0 &&
                   oreRoughViolations == 0;
        }

        private static bool TryCaptureMaterialBindingSnapshot(Material material, out MaterialBindingSnapshot snapshot, out string failure)
        {
            failure = string.Empty;
            snapshot = default;
            if (material == null)
                return true;

            if (!material.HasProperty(MaterialAlbedoMapId) ||
                !material.HasProperty(MaterialMraoMapId) ||
                !material.HasProperty(MaterialBlendId) ||
                !material.HasProperty(MaterialWorldOriginAupId) ||
                !material.HasProperty(MaterialTileMetersId))
            {
                failure = "target material does not expose the baked geology shader contract";
                return false;
            }

            snapshot = new MaterialBindingSnapshot(
                material,
                material.GetTexture(MaterialAlbedoMapId),
                material.GetTexture(MaterialMraoMapId),
                material.GetFloat(MaterialBlendId),
                material.GetVector(MaterialWorldOriginAupId),
                material.GetVector(MaterialTileMetersId));
            return true;
        }

        private static bool TryApplyMaterialBindings(in MaterialBindingSnapshot snapshot, string albedoPath, string mraoPath, in BakeSettings settings, out string failure)
        {
            failure = string.Empty;
            if (!snapshot.Captured)
                return true;

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D mrao = AssetDatabase.LoadAssetAtPath<Texture2D>(mraoPath);
            if (albedo == null || mrao == null)
            {
                failure = "imported texture assets are not loadable";
                return false;
            }

            Undo.RecordObject(snapshot.Material, "Apply geology bake 1724");
            snapshot.Material.SetTexture(MaterialAlbedoMapId, albedo);
            snapshot.Material.SetTexture(MaterialMraoMapId, mrao);
            snapshot.Material.SetFloat(MaterialBlendId, 1f);
            snapshot.Material.SetVector(MaterialWorldOriginAupId, new Vector4(settings.WorldOriginAup.x, settings.WorldOriginAup.y, settings.WorldOriginAup.z, 0f));
            snapshot.Material.SetVector(MaterialTileMetersId, new Vector4(settings.TileMeters.x, settings.TileMeters.y, 0f, 0f));
            EditorUtility.SetDirty(snapshot.Material);
            return true;
        }

        private static void RestoreMaterialBindings(in MaterialBindingSnapshot snapshot)
        {
            if (!snapshot.Captured || snapshot.Material == null)
                return;

            snapshot.Material.SetTexture(MaterialAlbedoMapId, snapshot.Albedo);
            snapshot.Material.SetTexture(MaterialMraoMapId, snapshot.Mrao);
            snapshot.Material.SetFloat(MaterialBlendId, snapshot.Blend);
            snapshot.Material.SetVector(MaterialWorldOriginAupId, snapshot.WorldOriginAup);
            snapshot.Material.SetVector(MaterialTileMetersId, snapshot.TileMeters);
            EditorUtility.SetDirty(snapshot.Material);
        }

        private static ResolvedDimensions ResolveDimensions(float qualityWeight)
        {
            float q = Mathf.Clamp01(qualityWeight);
            float smooth = q * q * (3f - 2f * q);
            int albedo = RoundToStep(Mathf.Lerp(MinimumAlbedoResolution, MaximumAlbedoResolution, smooth), 256);
            int mrao = RoundToStep(Mathf.Lerp(MinimumMraoResolution, MaximumMraoResolution, smooth), 128);
            return new ResolvedDimensions(
                Mathf.Clamp(albedo, MinimumAlbedoResolution, MaximumAlbedoResolution),
                Mathf.Clamp(mrao, MinimumMraoResolution, MaximumMraoResolution));
        }

        private static int RoundToStep(float value, int step)
        {
            return Mathf.Max(step, Mathf.RoundToInt(value / step) * step);
        }

        private static float FiniteOrZero(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static void ReleaseTransientGpuState()
        {
            if (s_transientParams != null)
            {
                s_transientParams.Release();
                s_transientParams = null;
            }

            if (s_transientAlbedo != null)
            {
                s_transientAlbedo.Release();
                DestroyImmediate(s_transientAlbedo);
                s_transientAlbedo = null;
            }

            if (s_transientMrao != null)
            {
                s_transientMrao.Release();
                DestroyImmediate(s_transientMrao);
                s_transientMrao = null;
            }
        }

        private readonly struct MaterialBindingSnapshot
        {
            public readonly bool Captured;
            public readonly Material Material;
            public readonly Texture Albedo;
            public readonly Texture Mrao;
            public readonly float Blend;
            public readonly Vector4 WorldOriginAup;
            public readonly Vector4 TileMeters;

            public MaterialBindingSnapshot(Material material, Texture albedo, Texture mrao, float blend, Vector4 worldOriginAup, Vector4 tileMeters)
            {
                Captured = material != null;
                Material = material;
                Albedo = albedo;
                Mrao = mrao;
                Blend = blend;
                WorldOriginAup = worldOriginAup;
                TileMeters = tileMeters;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GeologyBakeParams1724
        {
            public Vector4 WorldOriginSeed;
            public Vector4 TileQuality;
            public Vector4 WarpFractureOreSediment;
        }

        public readonly struct ResolvedDimensions
        {
            public readonly int AlbedoResolution;
            public readonly int MraoResolution;

            public ResolvedDimensions(int albedoResolution, int mraoResolution)
            {
                AlbedoResolution = albedoResolution;
                MraoResolution = mraoResolution;
            }
        }

        public readonly struct BakeResult
        {
            public readonly string AlbedoPath;
            public readonly string MraoPath;
            public readonly int AlbedoResolution;
            public readonly int MraoResolution;
            public readonly double ElapsedMicroseconds;
            public readonly string ValidationSummary;

            public BakeResult(string albedoPath, string mraoPath, int albedoResolution, int mraoResolution, double elapsedMicroseconds, string validationSummary)
            {
                AlbedoPath = albedoPath;
                MraoPath = mraoPath;
                AlbedoResolution = albedoResolution;
                MraoResolution = mraoResolution;
                ElapsedMicroseconds = elapsedMicroseconds;
                ValidationSummary = validationSummary;
            }
        }

        public readonly struct BakeSettings
        {
            public readonly string AssetName;
            public readonly string OutputFolder;
            public readonly ComputeShader StrataCompute;
            public readonly Mesh SourceRockMesh;
            public readonly Material TargetMaterial;
            public readonly float GlobalQualityWeight;
            public readonly Vector3 WorldOriginAup;
            public readonly Vector2 TileMeters;
            public readonly float StrataPeriodMeters;
            public readonly float WarpMeters;
            public readonly float FractureDensity;
            public readonly float OreIntensity;
            public readonly float SedimentStrength;
            public readonly uint Seed;

            public BakeSettings(
                string assetName,
                string outputFolder,
                ComputeShader strataCompute,
                Mesh sourceRockMesh,
                Material targetMaterial,
                float globalQualityWeight,
                Vector3 worldOriginAup,
                Vector2 tileMeters,
                float strataPeriodMeters,
                float warpMeters,
                float fractureDensity,
                float oreIntensity,
                float sedimentStrength,
                uint seed)
            {
                AssetName = assetName;
                OutputFolder = outputFolder;
                StrataCompute = strataCompute;
                SourceRockMesh = sourceRockMesh;
                TargetMaterial = targetMaterial;
                GlobalQualityWeight = globalQualityWeight;
                WorldOriginAup = worldOriginAup;
                TileMeters = tileMeters;
                StrataPeriodMeters = strataPeriodMeters;
                WarpMeters = warpMeters;
                FractureDensity = fractureDensity;
                OreIntensity = oreIntensity;
                SedimentStrength = sedimentStrength;
                Seed = seed;
            }

            public static BakeSettings Default()
            {
                return new BakeSettings(
                    "abyssal_strata",
                    DefaultOutputFolder,
                    AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath),
                    null,
                    null,
                    0.72f,
                    new Vector3(0f, -800f, 0f),
                    new Vector2(64f, 95f),
                    9.5f,
                    3.25f,
                    18f,
                    0.78f,
                    0.66f,
                    1724u);
            }

            public BakeSettings Sanitize()
            {
                float resolvedStrataPeriod = Mathf.Clamp(FiniteOrZero(StrataPeriodMeters), 0.5f, 48f);
                Vector2 resolvedTileMeters = new Vector2(
                    Mathf.Clamp(FiniteOrZero(TileMeters.x), 1f, 512f),
                    Mathf.Clamp(FiniteOrZero(TileMeters.y), 1f, 512f));
                if (SourceRockMesh != null)
                {
                    Bounds bounds = SourceRockMesh.bounds;
                    resolvedTileMeters.x = Mathf.Max(resolvedTileMeters.x, Mathf.Max(1f, bounds.size.x));
                    resolvedTileMeters.y = Mathf.Max(resolvedTileMeters.y, Mathf.Max(1f, bounds.size.y));
                }

                int maxStrataMultiples = Mathf.Max(1, Mathf.FloorToInt(512f / resolvedStrataPeriod));
                int strataMultiples = Mathf.Clamp(Mathf.CeilToInt(resolvedTileMeters.y / resolvedStrataPeriod), 1, maxStrataMultiples);
                resolvedTileMeters.y = strataMultiples * resolvedStrataPeriod;

                return new BakeSettings(
                    AssetName,
                    string.IsNullOrWhiteSpace(OutputFolder) ? DefaultOutputFolder : OutputFolder,
                    StrataCompute,
                    SourceRockMesh,
                    TargetMaterial,
                    Mathf.Clamp01(FiniteOrZero(GlobalQualityWeight)),
                    new Vector3(
                        FiniteOrZero(WorldOriginAup.x),
                        FiniteOrZero(WorldOriginAup.y),
                        FiniteOrZero(WorldOriginAup.z)),
                    resolvedTileMeters,
                    resolvedStrataPeriod,
                    Mathf.Clamp(FiniteOrZero(WarpMeters), 0f, 12f),
                    Mathf.Clamp(FiniteOrZero(FractureDensity), 2f, 72f),
                    Mathf.Clamp(FiniteOrZero(OreIntensity), MinimumOreIntensity, 1f),
                    Mathf.Clamp(FiniteOrZero(SedimentStrength), MinimumSedimentStrength, 1f),
                    Seed == 0u ? 1724u : Seed);
            }

            public GeologyBakeParams1724 ToParams()
            {
                return new GeologyBakeParams1724
                {
                    WorldOriginSeed = new Vector4(WorldOriginAup.x, WorldOriginAup.y, WorldOriginAup.z, Seed),
                    TileQuality = new Vector4(TileMeters.x, TileMeters.y, GlobalQualityWeight, StrataPeriodMeters),
                    WarpFractureOreSediment = new Vector4(WarpMeters, FractureDensity, OreIntensity, SedimentStrength)
                };
            }
        }
    }
}
#endif
