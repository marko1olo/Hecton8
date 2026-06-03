#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Hecton8.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Bakers
{
    public sealed class CausticOpticsBaker1719 : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/_Project/Art/Textures/Lighting";
        private const string MenuRoot = "HECTON-8/Bakers/1719/";
        private const int RendererAssetPathCount = 4;
        private const string PcRendererAssetPath = "Assets/_Project/Data/PC_Renderer.asset";
        private const string PcHighRendererAssetPath = "Assets/_Project/Data/PC_High_Renderer.asset";
        private const string MobileRendererAssetPath = "Assets/_Project/Data/Mobile_Renderer.asset";
        private const string QuestVrRendererAssetPath = "Assets/_Project/Data/Quest_VR_Renderer.asset";
        private const int MinimumGrid = 4;
        private const int MaximumGrid = 8;
        private const int MinimumFrameSize = 256;
        private const int MaximumFrameSize = 512;
        private const int WaterlineMaskSize = 256;
        private const int JobBatchSize = 128;
        private const long MaxEncodedPngBytes = 192L * 1024L * 1024L;
        private const float TwoPi = 6.2831853071795864769f;
        private const float MinimumDepthMeters = 0.5f;
        private const float MaximumDepthMeters = 80f;
        private const float MinimumTileMeters = 4f;
        private const float MaximumTileMeters = 256f;
        private const float EnergyTarget = 0.50f;
        private const float EnergyWarnDelta = 0.24f;
        private const float DefaultWaterlineWorldMinY = -2f;
        private const float DefaultWaterlineWorldMaxY = 2f;

        private string _assetName = "default";
        private string _outputFolder = DefaultOutputFolder;
        private float _globalQualityWeight = 0.75f;
        private float _tileMeters = 32f;
        private float _receiverDepthMeters = 14f;
        private float _dispersionStrength = 0.85f;
        private float _causticContrast = 1.35f;
        private float _lightSkewX = 0.18f;
        private float _lightSkewZ = 0.08f;
        private float _waterlineNormalized = 0.5f;
        private Texture2D _lightCookieForSelection = null;
        private string _lastStatus = "Idle.";

        [MenuItem(MenuRoot + "Open Caustic Optics Baker", false, 1719)]
        private static void Open()
        {
            CausticOpticsBaker1719 window = GetWindow<CausticOpticsBaker1719>();
            window.titleContent = new GUIContent("Caustic Optics 1719");
            window.minSize = new Vector2(440f, 420f);
        }

        [MenuItem(MenuRoot + "Bake Default Caustic Flipbook", false, 1720)]
        private static void BakeDefaultMenu()
        {
            BakeSettings settings = BakeSettings.Default();
            if (TryBake(settings, out BakeResult result))
                Debug.Log("[CausticBaker1719] Baked caustic flipbook: " + result.FlipbookPath + " | cookie: " + result.LightCookiePath);
        }

        [MenuItem(MenuRoot + "Bake Default And Bind Renderers", false, 1721)]
        private static void BakeDefaultAndBindMenu()
        {
            BakeSettings settings = BakeSettings.Default();
            if (TryBakeAndBind(settings, out BakeResult result, out int boundFeatureCount))
            {
                Debug.Log("[CausticBaker1719] Baked and bound caustic flipbook: " +
                          result.FlipbookPath +
                          " | renderer features=" +
                          boundFeatureCount.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Offline Caustic Projection Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _tileMeters = EditorGUILayout.Slider("Tile Meters", _tileMeters, MinimumTileMeters, MaximumTileMeters);
            _receiverDepthMeters = EditorGUILayout.Slider("Receiver Depth Meters", _receiverDepthMeters, MinimumDepthMeters, MaximumDepthMeters);
            _dispersionStrength = EditorGUILayout.Slider("Spectral Dispersion", _dispersionStrength, 0f, 1.5f);
            _causticContrast = EditorGUILayout.Slider("Caustic Contrast", _causticContrast, 0.25f, 3f);
            _lightSkewX = EditorGUILayout.Slider("Light Skew X", _lightSkewX, -0.75f, 0.75f);
            _lightSkewZ = EditorGUILayout.Slider("Light Skew Z", _lightSkewZ, -0.75f, 0.75f);
            _waterlineNormalized = EditorGUILayout.Slider("Waterline Mask Y", _waterlineNormalized, 0f, 1f);

            EditorGUILayout.Space(6f);
            ResolvedBakeDimensions dimensions = ResolveDimensions(_globalQualityWeight);
            EditorGUILayout.LabelField("Atlas", dimensions.AtlasSize + " x " + dimensions.AtlasSize);
            EditorGUILayout.LabelField("Grid", dimensions.GridColumns + " x " + dimensions.GridColumns + " frames");
            EditorGUILayout.LabelField("Frame Size", dimensions.FrameSize + " px");
            EditorGUILayout.LabelField("Spectral Weight", ResolveSpectralWeight(_globalQualityWeight, _dispersionStrength).ToString("0.000", CultureInfo.InvariantCulture));

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Flipbook", GUILayout.Height(32f)))
                BakeFromWindow();

            if (GUILayout.Button("Bake Flipbook And Bind Renderers", GUILayout.Height(32f)))
                BakeAndBindFromWindow();

            EditorGUILayout.Space(8f);
            _lightCookieForSelection = (Texture2D)EditorGUILayout.ObjectField("Light Cookie", _lightCookieForSelection, typeof(Texture2D), false);
            if (GUILayout.Button("Assign Cookie To Selected Lights", GUILayout.Height(28f)))
                AssignCookieToSelectedLightsFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = BakeSettings.Default();
            settings.AssetName = _assetName;
            settings.OutputFolder = _outputFolder;
            settings.GlobalQualityWeight = _globalQualityWeight;
            settings.TileMeters = _tileMeters;
            settings.ReceiverDepthMeters = _receiverDepthMeters;
            settings.DispersionStrength = _dispersionStrength;
            settings.CausticContrast = _causticContrast;
            settings.LightSkewX = _lightSkewX;
            settings.LightSkewZ = _lightSkewZ;
            settings.WaterlineNormalized = _waterlineNormalized;

            if (TryBake(settings, out BakeResult result))
            {
                _lightCookieForSelection = AssetDatabase.LoadAssetAtPath<Texture2D>(result.LightCookiePath);
                _lastStatus = "Baked " + result.FlipbookPath + " | cookie=" + result.LightCookiePath + " | avg=" + result.AverageBrightness.ToString("0.000", CultureInfo.InvariantCulture);
            }
            else
            {
                _lastStatus = "Bake failed. Check Console.";
            }
        }

        private void BakeAndBindFromWindow()
        {
            BakeSettings settings = BakeSettings.Default();
            settings.AssetName = _assetName;
            settings.OutputFolder = _outputFolder;
            settings.GlobalQualityWeight = _globalQualityWeight;
            settings.TileMeters = _tileMeters;
            settings.ReceiverDepthMeters = _receiverDepthMeters;
            settings.DispersionStrength = _dispersionStrength;
            settings.CausticContrast = _causticContrast;
            settings.LightSkewX = _lightSkewX;
            settings.LightSkewZ = _lightSkewZ;
            settings.WaterlineNormalized = _waterlineNormalized;

            if (TryBakeAndBind(settings, out BakeResult result, out int boundFeatureCount))
            {
                _lightCookieForSelection = AssetDatabase.LoadAssetAtPath<Texture2D>(result.LightCookiePath);
                _lastStatus = "Baked and bound " +
                              result.FlipbookPath +
                              " | features=" +
                              boundFeatureCount.ToString(CultureInfo.InvariantCulture) +
                              " | avg=" +
                              result.AverageBrightness.ToString("0.000", CultureInfo.InvariantCulture);
            }
            else
            {
                _lastStatus = "Bake/bind failed. Check Console.";
            }
        }

        private void AssignCookieToSelectedLightsFromWindow()
        {
            if (TryAssignLightCookieToSelectedLights(_lightCookieForSelection, out int assignedCount, out string failure))
            {
                _lastStatus = "Assigned caustic Light.cookie to selected lights: " + assignedCount.ToString(CultureInfo.InvariantCulture);
                return;
            }

            _lastStatus = "Cookie assignment failed: " + failure;
        }

        public static bool TryBake(in BakeSettings requestedSettings, out BakeResult result)
        {
            result = default;
            BakeSettings settings = SanitizeSettings(requestedSettings);
            ResolvedBakeDimensions dimensions = ResolveDimensions(settings.GlobalQualityWeight);
            if (!ValidateUnmanagedLayouts(out string layoutFailure))
            {
                Debug.LogError("[CausticBaker1719] " + layoutFailure);
                return false;
            }

            string safeName = ProceduralTextureBaker.SanitizeAssetNameForPath(settings.AssetName);
            if (string.IsNullOrEmpty(safeName))
                safeName = "default";

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(settings.OutputFolder, out string outputFolder, out string folderFailure))
            {
                Debug.LogError("[CausticBaker1719] Output folder invalid: " + folderFailure);
                return false;
            }

            int atlasPixelCount = dimensions.AtlasSize * dimensions.AtlasSize;
            string flipbookPath = outputFolder + "/TX_CausticFlipbook_" + safeName + ".png";
            string lightCookiePath = outputFolder + "/TX_CausticLightCookie_" + safeName + ".png";
            string maskPath = outputFolder + "/TX_CausticWaterlineMask_" + safeName + ".png";
            if (!ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(flipbookPath, lightCookiePath, maskPath, out ProceduralTextureBaker.AssetFileRollbackSnapshot[] rollbackSnapshots, out string rollbackFailure))
            {
                Debug.LogError("[CausticBaker1719] Rollback snapshot failed: " + rollbackFailure);
                return false;
            }

            NativeArray<Color32> pixels = default;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                pixels = new NativeArray<Color32>(atlasPixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                CausticBakeJob job = CreateBakeJob(settings, dimensions, pixels);
                JobHandle handle = job.Schedule(atlasPixelCount, JobBatchSize);
                // Editor-only MenuItem bake. This blocking sync never enters runtime Tick/LateFrameTick phases.
                handle.Complete();
                stopwatch.Stop();

                if (!ValidatePixels(pixels, dimensions.AtlasSize, out BakeValidation validation))
                {
                    Debug.LogError("[CausticBaker1719] Pixel validation failed: " + validation.Failure);
                    return false;
                }

                if (!TryWriteTextureAsset(flipbookPath, pixels, dimensions.AtlasSize, dimensions.AtlasSize, true, false, out string writeFailure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);
                    Debug.LogError("[CausticBaker1719] Flipbook write failed: " + writeFailure);
                    return false;
                }

                if (!TryWriteLightCookieFrame(lightCookiePath, pixels, in dimensions, out string cookieWriteFailure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);
                    Debug.LogError("[CausticBaker1719] Light cookie write failed: " + cookieWriteFailure);
                    return false;
                }

                if (!TryWriteWaterlineMask(maskPath, settings.WaterlineNormalized, out string maskFailure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);
                    Debug.LogError("[CausticBaker1719] Waterline mask write failed: " + maskFailure);
                    return false;
                }

                if (!TryConfigureCausticImporter(flipbookPath, dimensions.AtlasSize, out string flipbookImportFailure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);
                    Debug.LogError("[CausticBaker1719] Flipbook import failed: " + flipbookImportFailure);
                    return false;
                }

                if (!TryConfigureLightCookieImporter(lightCookiePath, dimensions.FrameSize, out string cookieImportFailure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);
                    Debug.LogError("[CausticBaker1719] Light cookie import failed: " + cookieImportFailure);
                    return false;
                }

                if (!TryConfigureWaterlineMaskImporter(maskPath, out string maskImportFailure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);
                    Debug.LogError("[CausticBaker1719] Waterline mask import failed: " + maskImportFailure);
                    return false;
                }

                if (!ProceduralTextureBaker.TryFinalizeAssetDatabase("1719 caustic optics bake", out string finalizeFailure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);
                    Debug.LogError("[CausticBaker1719] " + finalizeFailure);
                    return false;
                }

                result = new BakeResult(
                    true,
                    flipbookPath,
                    lightCookiePath,
                    maskPath,
                    dimensions.AtlasSize,
                    dimensions.GridColumns,
                    dimensions.FrameSize,
                    dimensions.FrameCount,
                    validation.AverageBrightness,
                    validation.MinChannel,
                    validation.MaxChannel,
                    stopwatch.Elapsed.TotalMilliseconds * 1000.0);

                if (math.abs(validation.AverageBrightness - EnergyTarget) > EnergyWarnDelta)
                {
                    Debug.LogWarning("[CausticBaker1719] Energy warning: average brightness " +
                                     validation.AverageBrightness.ToString("0.000", CultureInfo.InvariantCulture) +
                                     " is outside target envelope around " +
                                     EnergyTarget.ToString("0.000", CultureInfo.InvariantCulture));
                }

                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                stopwatch.Stop();
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);
                Debug.LogError("[CausticBaker1719] Bake exception: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                if (pixels.IsCreated)
                    pixels.Dispose();
                EditorUtility.ClearProgressBar();
            }
        }

        public static bool TryBakeAndBind(in BakeSettings requestedSettings, out BakeResult result, out int boundFeatureCount)
        {
            boundFeatureCount = 0;
            if (!TryBake(requestedSettings, out result))
                return false;

            if (TryBindBakeToDeferredCaustics(result, out boundFeatureCount, out string bindFailure))
                return true;

            Debug.LogError("[CausticBaker1719] Renderer bind failed: " + bindFailure);
            return false;
        }

        public static bool TryBindBakeToDeferredCaustics(in BakeResult result, out int boundFeatureCount, out string failure)
        {
            boundFeatureCount = 0;
            failure = null;
            if (!result.Success)
            {
                failure = "BakeResult is not marked successful.";
                return false;
            }

            Texture2D flipbookAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(result.FlipbookPath);
            if (flipbookAtlas == null)
            {
                failure = "Missing caustic flipbook atlas at " + result.FlipbookPath;
                return false;
            }

            Texture2D waterlineMask = AssetDatabase.LoadAssetAtPath<Texture2D>(result.WaterlineMaskPath);
            if (waterlineMask == null)
            {
                failure = "Missing caustic waterline mask at " + result.WaterlineMaskPath;
                return false;
            }

            bool changed = false;
            for (int rendererIndex = 0; rendererIndex < RendererAssetPathCount; rendererIndex++)
            {
                string rendererAssetPath = GetRendererAssetPath(rendererIndex);
                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);
                if (rendererData == null)
                {
                    failure = "Missing URP renderer asset: " + rendererAssetPath;
                    return false;
                }

                HectonDeferredCausticsFeature feature = FindDeferredCausticsFeature(rendererData);
                if (feature == null)
                {
                    failure = "Renderer asset has no HectonDeferredCausticsFeature: " + rendererAssetPath;
                    return false;
                }

                if (!TryApplyBakedCausticSettings(feature, flipbookAtlas, waterlineMask, result, out bool featureChanged, out string featureFailure))
                {
                    failure = rendererAssetPath + ": " + featureFailure;
                    return false;
                }

                if (!feature.isActive)
                {
                    feature.SetActive(true);
                    featureChanged = true;
                }

                boundFeatureCount++;
                if (!featureChanged)
                    continue;

                feature.Create();
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();

            return true;
        }

        public static bool TryAssignLightCookieToSelectedLights(Texture2D lightCookie, out int assignedCount, out string failure)
        {
            assignedCount = 0;
            failure = null;
            if (lightCookie == null)
            {
                failure = "No caustic light cookie texture is assigned.";
                return false;
            }

            Light[] selectedLights = Selection.GetFiltered<Light>(SelectionMode.Editable);
            if (selectedLights == null || selectedLights.Length == 0)
            {
                failure = "Select one or more Light components before assigning the cookie.";
                return false;
            }

            for (int i = 0; i < selectedLights.Length; i++)
            {
                Light selectedLight = selectedLights[i];
                if (selectedLight == null)
                    continue;
                if (!CanUseTwoDimensionalCausticCookie(selectedLight.type))
                    continue;

                if (selectedLight.cookie != lightCookie)
                {
                    selectedLight.cookie = lightCookie;
                    EditorUtility.SetDirty(selectedLight);
                    if (selectedLight.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(selectedLight.gameObject.scene);
                }

                assignedCount++;
            }

            if (assignedCount <= 0)
            {
                failure = "Selection did not contain editable Directional or Spot Light components.";
                return false;
            }

            return true;
        }

        private static bool CanUseTwoDimensionalCausticCookie(LightType lightType)
        {
            return lightType == LightType.Directional || lightType == LightType.Spot;
        }

        private static HectonDeferredCausticsFeature FindDeferredCausticsFeature(UniversalRendererData rendererData)
        {
            if (rendererData == null)
                return null;

            var rendererFeatures = rendererData.rendererFeatures;
            for (int featureIndex = 0; featureIndex < rendererFeatures.Count; featureIndex++)
            {
                if (rendererFeatures[featureIndex] is HectonDeferredCausticsFeature feature)
                    return feature;
            }

            return null;
        }

        private static bool TryApplyBakedCausticSettings(
            HectonDeferredCausticsFeature feature,
            Texture2D flipbookAtlas,
            Texture2D waterlineMask,
            in BakeResult result,
            out bool changed,
            out string failure)
        {
            changed = false;
            failure = null;
            SerializedObject serializedFeature = new SerializedObject(feature);
            if (!TrySetObject(serializedFeature, "settings.causticFlipbookAtlas", flipbookAtlas, out bool propertyChanged))
            {
                failure = "Missing serialized property settings.causticFlipbookAtlas";
                return false;
            }

            changed |= propertyChanged;
            if (!TrySetObject(serializedFeature, "settings.waterlineMask", waterlineMask, out propertyChanged))
            {
                failure = "Missing serialized property settings.waterlineMask";
                return false;
            }

            changed |= propertyChanged;
            if (!TrySetFloat(serializedFeature, "settings.bakedAtlasWeight", 1f, out propertyChanged))
            {
                failure = "Missing serialized property settings.bakedAtlasWeight";
                return false;
            }

            changed |= propertyChanged;
            if (!TrySetFloat(serializedFeature, "settings.waterlineMaskWeight", 1f, out propertyChanged))
            {
                failure = "Missing serialized property settings.waterlineMaskWeight";
                return false;
            }

            changed |= propertyChanged;
            if (!TrySetInt(serializedFeature, "settings.flipbookColumns", math.max(1, result.GridColumns), out propertyChanged))
            {
                failure = "Missing serialized property settings.flipbookColumns";
                return false;
            }

            changed |= propertyChanged;
            int flipbookRows = math.max(1, result.FrameCount / math.max(1, result.GridColumns));
            if (!TrySetInt(serializedFeature, "settings.flipbookRows", flipbookRows, out propertyChanged))
            {
                failure = "Missing serialized property settings.flipbookRows";
                return false;
            }

            changed |= propertyChanged;
            if (!TrySetInt(serializedFeature, "settings.flipbookFrames", math.max(1, result.FrameCount), out propertyChanged))
            {
                failure = "Missing serialized property settings.flipbookFrames";
                return false;
            }

            changed |= propertyChanged;
            if (!TrySetFloat(serializedFeature, "settings.waterlineWorldMinY", DefaultWaterlineWorldMinY, out propertyChanged))
            {
                failure = "Missing serialized property settings.waterlineWorldMinY";
                return false;
            }

            changed |= propertyChanged;
            if (!TrySetFloat(serializedFeature, "settings.waterlineWorldMaxY", DefaultWaterlineWorldMaxY, out propertyChanged))
            {
                failure = "Missing serialized property settings.waterlineWorldMaxY";
                return false;
            }

            changed |= propertyChanged;
            if (!changed)
                return true;

            serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TrySetObject(SerializedObject serializedObject, string propertyPath, UnityEngine.Object value, out bool changed)
        {
            changed = false;
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
                return false;

            if (property.objectReferenceValue == value)
                return true;

            property.objectReferenceValue = value;
            changed = true;
            return true;
        }

        private static bool TrySetInt(SerializedObject serializedObject, string propertyPath, int value, out bool changed)
        {
            changed = false;
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
                return false;

            if (property.intValue == value)
                return true;

            property.intValue = value;
            changed = true;
            return true;
        }

        private static bool TrySetFloat(SerializedObject serializedObject, string propertyPath, float value, out bool changed)
        {
            changed = false;
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
                return false;

            if (Mathf.Approximately(property.floatValue, value))
                return true;

            property.floatValue = value;
            changed = true;
            return true;
        }

        private static string GetRendererAssetPath(int rendererIndex)
        {
            switch (rendererIndex)
            {
                case 0:
                    return PcRendererAssetPath;
                case 1:
                    return PcHighRendererAssetPath;
                case 2:
                    return MobileRendererAssetPath;
                case 3:
                    return QuestVrRendererAssetPath;
                default:
                    return PcRendererAssetPath;
            }
        }

        private static CausticBakeJob CreateBakeJob(in BakeSettings settings, in ResolvedBakeDimensions dimensions, NativeArray<Color32> pixels)
        {
            float quality = math.saturate(settings.GlobalQualityWeight);
            float contrast = math.max(0.05f, settings.CausticContrast);
            float depth = math.clamp(settings.ReceiverDepthMeters, MinimumDepthMeters, MaximumDepthMeters);
            float tileMeters = math.clamp(settings.TileMeters, MinimumTileMeters, MaximumTileMeters);
            float3 lightDirection = math.normalize(new float3(settings.LightSkewX, -1f, settings.LightSkewZ));
            float spectralWeight = ResolveSpectralWeight(quality, settings.DispersionStrength);
            float waveScale = math.lerp(0.45f, 1.0f, quality);

            CausticBakeJob job;
            job.Output = pixels;
            job.AtlasSize = dimensions.AtlasSize;
            job.FrameSize = dimensions.FrameSize;
            job.GridColumns = dimensions.GridColumns;
            job.FrameCount = dimensions.FrameCount;
            job.TileMeters = tileMeters;
            job.ReceiverDepthMeters = depth;
            job.LightDirection = lightDirection;
            job.SpectralWeight = spectralWeight;
            job.CausticContrast = contrast;
            job.QualityWeight = quality;
            job.Wave0 = new float4(1f, 0f, 0.072f * waveScale, 1f);
            job.Wave1 = new float4(0f, 1f, 0.053f * waveScale, -1f);
            job.Wave2 = new float4(2f, 1f, 0.034f * waveScale, 2f);
            job.Wave3 = new float4(-1f, 2f, 0.026f * waveScale, -2f);
            job.WavePhase = new float4(0.17f, 1.91f, 3.11f, 4.29f);
            return job;
        }

        private static bool ValidatePixels(NativeArray<Color32> pixels, int atlasSize, out BakeValidation validation)
        {
            validation = default;
            int expected = atlasSize * atlasSize;
            if (!pixels.IsCreated)
            {
                validation = BakeValidation.Fail(0, expected, 0, 0f, 0f, 0f, "NativeArray not created.");
                return false;
            }

            if (pixels.Length != expected)
            {
                validation = BakeValidation.Fail(pixels.Length, expected, 0, 0f, 0f, 0f, "Pixel count mismatch.");
                return false;
            }

            double sum = 0.0;
            float minChannel = 1f;
            float maxChannel = 0f;
            int nonFinite = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                float r = p.r * (1f / 255f);
                float g = p.g * (1f / 255f);
                float b = p.b * (1f / 255f);
                if (!math.isfinite(r) || !math.isfinite(g) || !math.isfinite(b))
                    nonFinite++;

                minChannel = Mathf.Min(minChannel, Mathf.Min(r, Mathf.Min(g, b)));
                maxChannel = Mathf.Max(maxChannel, Mathf.Max(r, Mathf.Max(g, b)));
                sum += (r + g + b) * (1.0 / 3.0);
            }

            float average = (float)(sum / pixels.Length);
            validation = new BakeValidation(pixels.Length, expected, nonFinite, average, minChannel, maxChannel, string.Empty);
            return nonFinite == 0;
        }

        private static bool TryWriteTextureAsset(string assetPath, NativeArray<Color32> pixels, int width, int height, bool mipChain, bool linear, out string failure)
        {
            failure = string.Empty;
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain, linear)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                    wrapMode = mipChain ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                    filterMode = mipChain ? FilterMode.Trilinear : FilterMode.Bilinear,
                    anisoLevel = mipChain ? 2 : 1
                };
                texture.SetPixelData(pixels, 0);
                texture.Apply(updateMipmaps: mipChain, makeNoLongerReadable: false);
                byte[] encoded = ImageConversion.EncodeToPNG(texture);
                if (encoded == null || encoded.Length == 0)
                {
                    failure = "EncodeToPNG returned no bytes.";
                    return false;
                }

                if (encoded.LongLength > MaxEncodedPngBytes)
                {
                    failure = "encoded PNG exceeds byte ceiling: " + encoded.LongLength.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                return ProceduralTextureBaker.TryWriteBytesAtomic(assetPath, encoded, out failure);
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool TryWriteWaterlineMask(string assetPath, float waterlineNormalized, out string failure)
        {
            failure = string.Empty;
            NativeArray<Color32> pixels = default;
            try
            {
                float waterline = Mathf.Clamp01(waterlineNormalized);
                pixels = new NativeArray<Color32>(WaterlineMaskSize * WaterlineMaskSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                float feather = 2f / WaterlineMaskSize;
                byte waterlineByte = (byte)Mathf.RoundToInt(waterline * 255f);
                byte featherByte = (byte)Mathf.RoundToInt(feather * 255f);
                for (int y = 0; y < WaterlineMaskSize; y++)
                {
                    float v = WaterlineMaskSize <= 1 ? 0f : y / (float)(WaterlineMaskSize - 1);
                    float mask = 1f - Mathf.SmoothStep(waterline - feather, waterline + feather, v);
                    byte maskByte = (byte)Mathf.Clamp(Mathf.RoundToInt(mask * 255f), 0, 255);
                    int row = y * WaterlineMaskSize;
                    for (int x = 0; x < WaterlineMaskSize; x++)
                        pixels[row + x] = new Color32(maskByte, waterlineByte, featherByte, 255);
                }

                return TryWriteTextureAsset(assetPath, pixels, WaterlineMaskSize, WaterlineMaskSize, false, true, out failure);
            }
            finally
            {
                if (pixels.IsCreated)
                    pixels.Dispose();
            }
        }

        private static bool TryWriteLightCookieFrame(string assetPath, NativeArray<Color32> atlasPixels, in ResolvedBakeDimensions dimensions, out string failure)
        {
            failure = string.Empty;
            if (!atlasPixels.IsCreated ||
                dimensions.AtlasSize <= 0 ||
                dimensions.FrameSize <= 0 ||
                atlasPixels.Length != dimensions.AtlasSize * dimensions.AtlasSize)
            {
                failure = "Invalid atlas pixels for light cookie extraction.";
                return false;
            }

            NativeArray<Color32> cookiePixels = default;
            try
            {
                int cookiePixelCount = dimensions.FrameSize * dimensions.FrameSize;
                cookiePixels = new NativeArray<Color32>(cookiePixelCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int y = 0; y < dimensions.FrameSize; y++)
                {
                    int srcRow = y * dimensions.AtlasSize;
                    int dstRow = y * dimensions.FrameSize;
                    for (int x = 0; x < dimensions.FrameSize; x++)
                        cookiePixels[dstRow + x] = atlasPixels[srcRow + x];
                }

                return TryWriteTextureAsset(assetPath, cookiePixels, dimensions.FrameSize, dimensions.FrameSize, true, false, out failure);
            }
            finally
            {
                if (cookiePixels.IsCreated)
                    cookiePixels.Dispose();
            }
        }

        private static bool TryConfigureCausticImporter(string assetPath, int maxTextureSize, out string failure)
        {
            return TryConfigureTextureImporter(
                assetPath,
                TextureImporterType.Default,
                true,
                true,
                TextureWrapMode.Repeat,
                FilterMode.Trilinear,
                TextureImporterCompression.CompressedHQ,
                Mathf.Clamp(maxTextureSize, 1024, 4096),
                TextureImporterFormat.BC7,
                TextureImporterFormat.ASTC_6x6,
                out failure);
        }

        private static bool TryConfigureLightCookieImporter(string assetPath, int maxTextureSize, out string failure)
        {
            return TryConfigureTextureImporter(
                assetPath,
                TextureImporterType.Cookie,
                true,
                true,
                TextureWrapMode.Repeat,
                FilterMode.Trilinear,
                TextureImporterCompression.CompressedHQ,
                Mathf.Clamp(maxTextureSize, MinimumFrameSize, 1024),
                TextureImporterFormat.BC7,
                TextureImporterFormat.ASTC_6x6,
                out failure);
        }

        private static bool TryConfigureWaterlineMaskImporter(string assetPath, out string failure)
        {
            return TryConfigureTextureImporter(
                assetPath,
                TextureImporterType.Default,
                false,
                false,
                TextureWrapMode.Clamp,
                FilterMode.Bilinear,
                TextureImporterCompression.Compressed,
                WaterlineMaskSize,
                TextureImporterFormat.BC4,
                TextureImporterFormat.ASTC_6x6,
                out failure);
        }

        private static bool TryConfigureTextureImporter(
            string assetPath,
            TextureImporterType textureType,
            bool srgb,
            bool mipmaps,
            TextureWrapMode wrapMode,
            FilterMode filterMode,
            TextureImporterCompression compression,
            int maxTextureSize,
            TextureImporterFormat desktopFormat,
            TextureImporterFormat mobileFormat,
            out string failure)
        {
            failure = string.Empty;
            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    failure = "TextureImporter missing for " + assetPath;
                    return false;
                }

                importer.textureType = textureType;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = srgb;
                importer.mipmapEnabled = mipmaps;
                importer.wrapMode = wrapMode;
                importer.filterMode = filterMode;
                importer.textureCompression = compression;
                importer.crunchedCompression = false;
                importer.isReadable = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = maxTextureSize;
                importer.SetPlatformTextureSettings(CreatePlatformSettings("Standalone", maxTextureSize, desktopFormat));
                importer.SetPlatformTextureSettings(CreatePlatformSettings("Windows Store Apps", maxTextureSize, desktopFormat));
                importer.SetPlatformTextureSettings(CreatePlatformSettings("Android", maxTextureSize, mobileFormat));
                importer.SetPlatformTextureSettings(CreatePlatformSettings("iPhone", maxTextureSize, mobileFormat));
                importer.SaveAndReimport();
                return ValidateTextureImporterSettings(
                    importer,
                    textureType,
                    srgb,
                    mipmaps,
                    wrapMode,
                    filterMode,
                    maxTextureSize,
                    desktopFormat,
                    mobileFormat,
                    out failure);
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool ValidateTextureImporterSettings(
            TextureImporter importer,
            TextureImporterType textureType,
            bool srgb,
            bool mipmaps,
            TextureWrapMode wrapMode,
            FilterMode filterMode,
            int maxTextureSize,
            TextureImporterFormat desktopFormat,
            TextureImporterFormat mobileFormat,
            out string failure)
        {
            failure = string.Empty;
            if (importer.textureType != textureType)
            {
                failure = "TextureImporter type mismatch.";
                return false;
            }

            if (importer.textureShape != TextureImporterShape.Texture2D)
            {
                failure = "TextureImporter shape mismatch.";
                return false;
            }

            if (importer.sRGBTexture != srgb)
            {
                failure = "TextureImporter sRGB mismatch.";
                return false;
            }

            if (importer.mipmapEnabled != mipmaps)
            {
                failure = "TextureImporter mipmap mismatch.";
                return false;
            }

            if (importer.wrapMode != wrapMode)
            {
                failure = "TextureImporter wrap mode mismatch.";
                return false;
            }

            if (importer.filterMode != filterMode)
            {
                failure = "TextureImporter filter mode mismatch.";
                return false;
            }

            if (importer.isReadable)
            {
                failure = "TextureImporter readability mismatch.";
                return false;
            }

            if (importer.maxTextureSize != maxTextureSize)
            {
                failure = "TextureImporter max texture size mismatch.";
                return false;
            }

            if (!ValidatePlatformSettings(importer, "Standalone", maxTextureSize, desktopFormat, out failure))
                return false;

            return ValidatePlatformSettings(importer, "Android", maxTextureSize, mobileFormat, out failure);
        }

        private static bool ValidatePlatformSettings(
            TextureImporter importer,
            string platform,
            int maxTextureSize,
            TextureImporterFormat format,
            out string failure)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            if (!settings.overridden)
            {
                failure = "TextureImporter platform override missing: " + platform;
                return false;
            }

            if (settings.maxTextureSize != maxTextureSize)
            {
                failure = "TextureImporter platform max size mismatch: " + platform;
                return false;
            }

            if (settings.format != format)
            {
                failure = "TextureImporter platform format mismatch: " + platform;
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static TextureImporterPlatformSettings CreatePlatformSettings(string platform, int maxTextureSize, TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = new TextureImporterPlatformSettings();
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = maxTextureSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.allowsAlphaSplitting = false;
            return settings;
        }

        private static BakeSettings SanitizeSettings(in BakeSettings settings)
        {
            BakeSettings sanitized = settings;
            sanitized.AssetName = string.IsNullOrEmpty(settings.AssetName) ? "default" : settings.AssetName;
            sanitized.OutputFolder = string.IsNullOrEmpty(settings.OutputFolder) ? DefaultOutputFolder : settings.OutputFolder;
            sanitized.GlobalQualityWeight = Mathf.Clamp01(settings.GlobalQualityWeight);
            sanitized.TileMeters = Mathf.Clamp(settings.TileMeters, MinimumTileMeters, MaximumTileMeters);
            sanitized.ReceiverDepthMeters = Mathf.Clamp(settings.ReceiverDepthMeters, MinimumDepthMeters, MaximumDepthMeters);
            sanitized.DispersionStrength = Mathf.Clamp(settings.DispersionStrength, 0f, 1.5f);
            sanitized.CausticContrast = Mathf.Clamp(settings.CausticContrast, 0.25f, 3f);
            sanitized.LightSkewX = Mathf.Clamp(settings.LightSkewX, -0.75f, 0.75f);
            sanitized.LightSkewZ = Mathf.Clamp(settings.LightSkewZ, -0.75f, 0.75f);
            sanitized.WaterlineNormalized = Mathf.Clamp01(settings.WaterlineNormalized);
            return sanitized;
        }

        private static ResolvedBakeDimensions ResolveDimensions(float globalQualityWeight)
        {
            float q = Mathf.Clamp01(globalQualityWeight);
            int grid = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(MinimumGrid, MaximumGrid, q)), MinimumGrid, MaximumGrid);
            int frameSize = Mathf.Clamp(RoundUpToMultipleOf4(Mathf.RoundToInt(Mathf.Lerp(MinimumFrameSize, MaximumFrameSize, q))), MinimumFrameSize, MaximumFrameSize);
            int atlasSize = grid * frameSize;
            return new ResolvedBakeDimensions(atlasSize, frameSize, grid, grid * grid);
        }

        private static int RoundUpToMultipleOf4(int value)
        {
            return (value + 3) & ~3;
        }

        private static float ResolveSpectralWeight(float globalQualityWeight, float dispersionStrength)
        {
            float q = math.saturate(globalQualityWeight);
            float enabled = math.saturate((q - 0.18f) / 0.82f);
            return math.saturate(enabled * enabled * math.max(0f, dispersionStrength));
        }

        private static bool ValidateUnmanagedLayouts(out string failure)
        {
            failure = string.Empty;
            int jobBytes = UnsafeUtility.SizeOf<CausticBakeJob>();
            int dimensionsBytes = UnsafeUtility.SizeOf<ResolvedBakeDimensions>();
            if ((jobBytes & 7) != 0)
            {
                failure = "CausticBakeJob size is not 8-byte aligned: " + jobBytes.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if ((dimensionsBytes & 7) != 0)
            {
                failure = "ResolvedBakeDimensions size is not 8-byte aligned: " + dimensionsBytes.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct CausticBakeJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<Color32> Output;
            public int AtlasSize;
            public int FrameSize;
            public int GridColumns;
            public int FrameCount;
            public float TileMeters;
            public float ReceiverDepthMeters;
            public float3 LightDirection;
            public float SpectralWeight;
            public float CausticContrast;
            public float QualityWeight;
            public float4 Wave0;
            public float4 Wave1;
            public float4 Wave2;
            public float4 Wave3;
            public float4 WavePhase;

            public void Execute(int index)
            {
                int y = index / AtlasSize;
                int x = index - y * AtlasSize;
                int frameX = math.min(x / FrameSize, GridColumns - 1);
                int frameY = math.min(y / FrameSize, GridColumns - 1);
                int localX = x - frameX * FrameSize;
                int localY = y - frameY * FrameSize;
                int frameIndex = math.min(frameY * GridColumns + frameX, FrameCount - 1);

                float denom = math.max(1f, FrameSize - 1f);
                float2 uv = new float2(localX / denom, localY / denom);
                float time01 = frameIndex / (float)math.max(1, FrameCount);
                float2 world = uv * TileMeters;
                float texelMeters = TileMeters / math.max(1f, FrameSize);
                float spectralOffset = SpectralWeight * texelMeters * 0.75f;

                float red = EvaluateSpectral(world, time01, 1.331f, spectralOffset);
                float green = EvaluateSpectral(world, time01, 1.337f, 0f);
                float blue = EvaluateSpectral(world, time01, 1.343f, -spectralOffset);
                Output[index] = new Color32(Quantize(red), Quantize(green), Quantize(blue), 255);
            }

            private float EvaluateSpectral(float2 world, float time01, float ior, float spectralOffsetMeters)
            {
                float2 slope = EvaluateSlope(world, time01);
                float slopeLenSq = math.lengthsq(slope);
                float2 direction = math.select(new float2(1f, 0f), math.normalize(slope), slopeLenSq > 0.000001f);
                float2 shifted = WrapWorld(world + direction * spectralOffsetMeters);
                float2 projected = ProjectRefracted(shifted, time01, ior);
                float step = math.max(0.015625f, TileMeters / math.max(64f, FrameSize));
                float2 projectedX = ProjectRefracted(WrapWorld(shifted + new float2(step, 0f)), time01, ior);
                float2 projectedY = ProjectRefracted(WrapWorld(shifted + new float2(0f, step)), time01, ior);
                float2 dx = ShortestTileDelta(projectedX, projected, TileMeters);
                float2 dy = ShortestTileDelta(projectedY, projected, TileMeters);
                float determinant = math.abs(dx.x * dy.y - dx.y * dy.x) / math.max(0.000001f, step * step);
                float focus = math.rcp(0.18f + determinant);
                float band = math.saturate(focus * 0.58f);
                float sharpened = math.pow(band, math.lerp(1.55f, 0.82f, QualityWeight));
                float shimmer = 0.82f + 0.18f * math.sin(TwoPi * (projected.x * 0.071f + projected.y * 0.043f + time01));
                float energy = math.lerp(0.38f, 0.58f, math.saturate(sharpened * CausticContrast));
                return math.saturate(energy * shimmer);
            }

            private float2 ProjectRefracted(float2 world, float time01, float ior)
            {
                float3 normal = EvaluateNormal(world, time01);
                float eta = math.rcp(math.max(1.0001f, ior));
                float3 refracted = RefractSafe(math.normalize(LightDirection), normal, eta);
                float y = math.min(-0.0001f, refracted.y);
                float travel = ReceiverDepthMeters / -y;
                return WrapWorld(world + refracted.xz * travel);
            }

            private float3 RefractSafe(float3 incident, float3 normal, float eta)
            {
                float cosi = math.dot(normal, incident);
                float k = 1f - eta * eta * (1f - cosi * cosi);
                bool valid = math.isfinite(k) && k > 0.000001f;
                float root = math.sqrt(math.max(k, 0.000001f));
                float3 refracted = eta * incident - (eta * cosi + root) * normal;
                float lenSq = math.lengthsq(refracted);
                bool finite = math.isfinite(lenSq) && lenSq > 0.000001f;
                return math.select(new float3(0f, -1f, 0f), refracted * math.rsqrt(math.max(lenSq, 0.000001f)), valid & finite);
            }

            private float3 EvaluateNormal(float2 world, float time01)
            {
                float2 slope = EvaluateSlope(world, time01);
                float3 normal = new float3(-slope.x, 1f, -slope.y);
                float lenSq = math.lengthsq(normal);
                return math.select(new float3(0f, 1f, 0f), normal * math.rsqrt(math.max(lenSq, 0.000001f)), math.isfinite(lenSq) && lenSq > 0.000001f);
            }

            private float2 EvaluateSlope(float2 world, float time01)
            {
                float2 slope = new float2(0f, 0f);
                AddWaveSlope(ref slope, Wave0, WavePhase.x, world, time01);
                AddWaveSlope(ref slope, Wave1, WavePhase.y, world, time01);
                AddWaveSlope(ref slope, Wave2, WavePhase.z, world, time01);
                AddWaveSlope(ref slope, Wave3, WavePhase.w, world, time01);
                return slope;
            }

            private void AddWaveSlope(ref float2 slope, float4 wave, float phaseOffset, float2 world, float time01)
            {
                float2 lattice = wave.xy;
                float amplitude = wave.z;
                float cycles = wave.w;
                float phase = TwoPi * ((lattice.x * world.x + lattice.y * world.y) / math.max(0.0001f, TileMeters) + cycles * time01) + phaseOffset;
                float c = math.cos(phase);
                float2 k = TwoPi * lattice / math.max(0.0001f, TileMeters);
                slope += amplitude * k * c;
            }

            private float2 WrapWorld(float2 world)
            {
                float tile = math.max(0.0001f, TileMeters);
                float x = world.x - math.floor(world.x / tile) * tile;
                float y = world.y - math.floor(world.y / tile) * tile;
                return new float2(x, y);
            }

            private static float2 ShortestTileDelta(float2 a, float2 b, float tile)
            {
                float2 d = a - b;
                d.x -= math.round(d.x / tile) * tile;
                d.y -= math.round(d.y / tile) * tile;
                return d;
            }

            private static byte Quantize(float value)
            {
                int v = (int)math.round(math.saturate(value) * 255f);
                return (byte)math.clamp(v, 0, 255);
            }
        }

        public struct BakeSettings
        {
            public string AssetName;
            public string OutputFolder;
            public float GlobalQualityWeight;
            public float TileMeters;
            public float ReceiverDepthMeters;
            public float DispersionStrength;
            public float CausticContrast;
            public float LightSkewX;
            public float LightSkewZ;
            public float WaterlineNormalized;

            public static BakeSettings Default()
            {
                BakeSettings settings;
                settings.AssetName = "default";
                settings.OutputFolder = DefaultOutputFolder;
                settings.GlobalQualityWeight = 0.75f;
                settings.TileMeters = 32f;
                settings.ReceiverDepthMeters = 14f;
                settings.DispersionStrength = 0.85f;
                settings.CausticContrast = 1.35f;
                settings.LightSkewX = 0.18f;
                settings.LightSkewZ = 0.08f;
                settings.WaterlineNormalized = 0.5f;
                return settings;
            }
        }

        private readonly struct ResolvedBakeDimensions
        {
            public readonly int AtlasSize;
            public readonly int FrameSize;
            public readonly int GridColumns;
            public readonly int FrameCount;

            public ResolvedBakeDimensions(int atlasSize, int frameSize, int gridColumns, int frameCount)
            {
                AtlasSize = atlasSize;
                FrameSize = frameSize;
                GridColumns = gridColumns;
                FrameCount = frameCount;
            }
        }

        private readonly struct BakeValidation
        {
            public readonly int PixelCount;
            public readonly int ExpectedPixelCount;
            public readonly int NonFiniteCount;
            public readonly float AverageBrightness;
            public readonly float MinChannel;
            public readonly float MaxChannel;
            public readonly string Failure;

            public BakeValidation(int pixelCount, int expectedPixelCount, int nonFiniteCount, float averageBrightness, float minChannel, float maxChannel, string failure)
            {
                PixelCount = pixelCount;
                ExpectedPixelCount = expectedPixelCount;
                NonFiniteCount = nonFiniteCount;
                AverageBrightness = averageBrightness;
                MinChannel = minChannel;
                MaxChannel = maxChannel;
                Failure = failure;
            }

            public static BakeValidation Fail(int pixelCount, int expectedPixelCount, int nonFiniteCount, float averageBrightness, float minChannel, float maxChannel, string failure)
            {
                return new BakeValidation(pixelCount, expectedPixelCount, nonFiniteCount, averageBrightness, minChannel, maxChannel, failure);
            }
        }

        public readonly struct BakeResult
        {
            public readonly bool Success;
            public readonly string FlipbookPath;
            public readonly string LightCookiePath;
            public readonly string WaterlineMaskPath;
            public readonly int AtlasSize;
            public readonly int GridColumns;
            public readonly int FrameSize;
            public readonly int FrameCount;
            public readonly float AverageBrightness;
            public readonly float MinChannel;
            public readonly float MaxChannel;
            public readonly double BakeMicroseconds;

            public BakeResult(
                bool success,
                string flipbookPath,
                string lightCookiePath,
                string waterlineMaskPath,
                int atlasSize,
                int gridColumns,
                int frameSize,
                int frameCount,
                float averageBrightness,
                float minChannel,
                float maxChannel,
                double bakeMicroseconds)
            {
                Success = success;
                FlipbookPath = flipbookPath;
                LightCookiePath = lightCookiePath;
                WaterlineMaskPath = waterlineMaskPath;
                AtlasSize = atlasSize;
                GridColumns = gridColumns;
                FrameSize = frameSize;
                FrameCount = frameCount;
                AverageBrightness = averageBrightness;
                MinChannel = minChannel;
                MaxChannel = maxChannel;
                BakeMicroseconds = bakeMicroseconds;
            }
        }
    }
}
#endif
