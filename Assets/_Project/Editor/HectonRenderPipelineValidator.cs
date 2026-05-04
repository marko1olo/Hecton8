#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Crest;
using Hecton8.Atmosphere;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Visor;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Enforces URP water/depth requirements and emits hard warnings for renderer features that can destabilize Crest.
    /// </summary>
    internal static class HectonRenderPipelineValidator
    {
        private const string ValidateMenuPath = "Hecton/Validation/Graphics/Run Render Pipeline Validator";
        private const string RepairMenuPath = "Hecton/Validation/Graphics/Repair Render Pipeline Assets";
        private const string LogPrefix = "[HectonRenderPipelineValidator]";
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string BlendedSkyboxMaterialPath = "Assets/_Project/Art/Materials/Mat_HectonSky.mat";
        private const string DaySkyboxMaterialPath = "Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat";
        private const string NightSkyboxMaterialPath = "Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat";
        private const string MeshSkyMaterialPath = "Assets/_Project/Art/Materials/Mat_HectonSky.mat";
        private const string RendererFeaturesPropertyPath = "m_RendererFeatures";
        private const string RendererFeatureMapPropertyPath = "m_RendererFeatureMap";
        private static readonly string[] s_TargetRendererNames = { "PC_Renderer", "PC_High_Renderer", "Mobile_Renderer" };
        private static readonly string[] s_TargetRendererAssetPaths =
        {
            "Assets/_Project/Data/PC_Renderer.asset",
            "Assets/_Project/Data/PC_High_Renderer.asset",
            "Assets/_Project/Data/Mobile_Renderer.asset"
        };
        private static readonly string[] s_TargetUrpAssetPaths =
        {
            "Assets/_Project/Data/URP_Medium (PC_RPAsset).asset",
            "Assets/_Project/Data/URP_Low (PC_RPAsset).asset",
            "Assets/_Project/Data/URP_High (PC_RPAsset).asset",
            "Assets/_Project/Data/Mobile_RPAsset.asset"
        };
        private static readonly string[] s_UrpEditorShaderGraphPaths =
        {
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractive.shadergraph",
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractiveTransparent.shadergraph",
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractiveMasked.shadergraph"
        };
        private static readonly MethodInfo s_ValidateRendererFeaturesMethod =
            typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
        private const string InitialRepairCompletedSessionKey =
            "HectonRenderPipelineValidator.InitialRepairCompleted";
        private static bool s_InitialRepairQueued;

        private static readonly string[] s_DepthMutationPatterns =
        {
            "ClearFlag.Depth",
            "ClearRenderTarget(",
            "activeDepthTexture",
            "AccessFlags.ReadWrite",
            "SetRenderTarget(",
            "SetRenderAttachmentDepth",
            "ConfigureTarget("
        };
        private const string VolumetricFeatureTypeName = "Hecton8.Visor.HectonScooterVolumetricShaftsFeature";
        private const string SsdoFeatureTypeName = "Hecton8.Visor.HectonAbyssalSsdoFeature";
        private const string VisorFluidFeatureTypeName = "Hecton8.Visor.HectonVisorFluidDistortionFeature";

        [InitializeOnLoadMethod]
        private static void QueueInitialRepair()
        {
            if (Application.isBatchMode)
                return;

            if (s_InitialRepairQueued ||
                SessionState.GetBool(InitialRepairCompletedSessionKey, false))
            {
                return;
            }

            s_InitialRepairQueued = true;
            EditorApplication.delayCall += RunInitialRepair;
        }

        [MenuItem(ValidateMenuPath, priority = 191)]
        private static void RunValidationFromMenu()
        {
            Validate(logSummary: true, applyRepairs: false);
        }

        [MenuItem(RepairMenuPath, priority = 192)]
        private static void RunRepairFromMenu()
        {
            Validate(logSummary: true, applyRepairs: true);
        }

        internal static void RunBatchValidation()
        {
            Validate(logSummary: true, applyRepairs: true);
        }

        internal static void RunBatchWorldValidation()
        {
            if (File.Exists(WorldScenePath))
                EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

            Validate(logSummary: true, applyRepairs: true);
            LogOpenSceneDepthCacheAlignment();
        }

        private static void RunInitialRepair()
        {
            s_InitialRepairQueued = false;

            if (Application.isBatchMode)
                return;

            if (IsPlayModeUnsafeForRepairs())
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueInitialRepair();
                return;
            }

            SessionState.SetBool(InitialRepairCompletedSessionKey, true);
            EnsureUrpEditorShaderGraphsImported();
            RepairKnownRenderPipelineAssets();
            Validate(logSummary: false, applyRepairs: true);
        }

        private static void RepairKnownRenderPipelineAssets()
        {
            bool changed = false;

            for (int assetIndex = 0; assetIndex < s_TargetUrpAssetPaths.Length; assetIndex++)
            {
                UniversalRenderPipelineAsset urpAsset =
                    AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(s_TargetUrpAssetPaths[assetIndex]);
                if (urpAsset == null)
                    continue;

                changed |= EnsurePipelineRequirements(urpAsset);
                changed |= EnsureRequiredRendererFeatures(urpAsset);
            }

            for (int assetIndex = 0; assetIndex < s_TargetRendererAssetPaths.Length; assetIndex++)
            {
                UniversalRendererData rendererData =
                    AssetDatabase.LoadAssetAtPath<UniversalRendererData>(s_TargetRendererAssetPaths[assetIndex]);
                if (rendererData == null || !IsManagedRenderer(rendererData))
                    continue;

                changed |= EnsureRequiredRendererAssetState(rendererData);
            }

            if (changed)
                AssetDatabase.SaveAssets();
        }

        private static void Validate(bool logSummary, bool applyRepairs)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (applyRepairs && IsPlayModeUnsafeForRepairs())
                applyRepairs = false;

            if (!IsPlayModeUnsafeForRepairs())
                EnsureUrpEditorShaderGraphsImported();

            UniversalRenderPipelineAsset urpAsset = ResolveActiveUrpAsset();
            if (urpAsset == null)
                return;

            bool assetChanged = false;
            if (applyRepairs)
            {
                assetChanged = EnsurePipelineRequirements(urpAsset);
                assetChanged |= EnsureRequiredRendererFeatures(urpAsset);
            }

            int severeWarningCount = ValidateRendererFeatures(urpAsset);
            int sceneIssueCount = ValidateCrestScene();
            sceneIssueCount += ValidateCelestialSkybox(applyRepairs);

            if (logSummary)
            {
                Debug.Log(
                    $"{LogPrefix} Validation complete. " +
                    $"applyRepairs={applyRepairs}, assetChanged={assetChanged}, " +
                    $"rendererFeatureSevereWarnings={severeWarningCount}, sceneIssues={sceneIssueCount}.");
            }
        }

        private static bool EnsurePipelineRequirements(UniversalRenderPipelineAsset urpAsset)
        {
            bool changed = false;

            if (!urpAsset.supportsCameraDepthTexture)
            {
                urpAsset.supportsCameraDepthTexture = true;
                changed = true;
            }

            if (!urpAsset.supportsCameraOpaqueTexture)
            {
                urpAsset.supportsCameraOpaqueTexture = true;
                changed = true;
            }

            if (urpAsset.msaaSampleCount != 1)
            {
                urpAsset.msaaSampleCount = 1;
                changed = true;
            }

            if (urpAsset.opaqueDownsampling != Downsampling.None)
            {
                Debug.LogWarning(
                    $"{LogPrefix} URP asset '{urpAsset.name}' has Opaque Downsampling set to '{urpAsset.opaqueDownsampling}'. " +
                    "Crest underwater resolves are safer with Downsampling.None.");
            }

            var rendererDataList = urpAsset.rendererDataList;
            for (int rendererIndex = 0; rendererIndex < rendererDataList.Length; rendererIndex++)
            {
                if (rendererDataList[rendererIndex] is not UniversalRendererData rendererData)
                    continue;

                if (rendererData.depthPrimingMode != DepthPrimingMode.Disabled)
                {
                    rendererData.depthPrimingMode = DepthPrimingMode.Disabled;
                    EditorUtility.SetDirty(rendererData);
                    AssetDatabase.SaveAssetIfDirty(rendererData);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(urpAsset);
                AssetDatabase.SaveAssetIfDirty(urpAsset);
            }

            return changed;
        }

        private static bool IsPlayModeUnsafeForRepairs()
        {
            return Application.isPlaying ||
                   EditorApplication.isPlaying ||
                   EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void EnsureUrpEditorShaderGraphsImported()
        {
            for (int i = 0; i < s_UrpEditorShaderGraphPaths.Length; i++)
            {
                string path = s_UrpEditorShaderGraphPaths[i];
                if (AssetDatabase.LoadAssetAtPath<Shader>(path) != null)
                    continue;

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static bool EnsureRequiredRendererFeatures(UniversalRenderPipelineAsset urpAsset)
        {
            bool changed = false;
            var rendererDataList = urpAsset.rendererDataList;
            for (int rendererIndex = 0; rendererIndex < rendererDataList.Length; rendererIndex++)
            {
                if (rendererDataList[rendererIndex] is not UniversalRendererData rendererData ||
                    !IsManagedRenderer(rendererData))
                {
                    continue;
                }

                bool rendererChanged = EnsureRequiredRendererAssetState(rendererData);
                if (!rendererChanged)
                    continue;

                changed = true;
            }

            return changed;
        }

        private static bool EnsureRequiredRendererAssetState(UniversalRendererData rendererData)
        {
            bool rendererChanged = false;
            rendererChanged |= RestoreSerializedRendererFeatures(rendererData);
            rendererChanged |= EnsureRequiredRendererFeature<HectonAbyssalSsdoFeature>(rendererData);
            rendererChanged |= EnsureRequiredRendererFeature<HectonScooterVolumetricShaftsFeature>(rendererData);
            rendererChanged |= EnsureRequiredRendererFeature<HectonVisorFluidDistortionFeature>(rendererData);
            rendererChanged |= EnsureRequiredFeatureState(rendererData, SsdoFeatureTypeName, RenderPassEvent.BeforeRenderingTransparents);
            rendererChanged |= EnsureRequiredFeatureState(rendererData, VolumetricFeatureTypeName, RenderPassEvent.BeforeRenderingTransparents);
            rendererChanged |= EnsureRequiredFeatureState(rendererData, VisorFluidFeatureTypeName, RenderPassEvent.BeforeRenderingPostProcessing);
            if (!rendererChanged)
                return false;

            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssetIfDirty(rendererData);
            return true;
        }

        private static int ValidateRendererFeatures(UniversalRenderPipelineAsset urpAsset)
        {
            int severeWarningCount = 0;
            var rendererDataList = urpAsset.rendererDataList;
            for (int rendererIndex = 0; rendererIndex < rendererDataList.Length; rendererIndex++)
            {
                if (rendererDataList[rendererIndex] is not UniversalRendererData rendererData)
                    continue;

                var rendererFeatures = rendererData.rendererFeatures;
                for (int featureIndex = 0; featureIndex < rendererFeatures.Count; featureIndex++)
                {
                    ScriptableRendererFeature feature = rendererFeatures[featureIndex];
                    if (feature == null || !feature.isActive || !IsCustomFeature(feature))
                        continue;

                    if (!TryResolveInjectionPoint(feature, out RenderPassEvent injectionPoint))
                        continue;

                    severeWarningCount += ValidateFeatureOrdering(rendererData, feature, injectionPoint);

                    if (!TryReadFeatureSource(feature, out string sourcePath, out string sourceText))
                        continue;

                    if (!TryFindDepthMutationPattern(sourceText, out string matchedPattern))
                        continue;

                    if (injectionPoint < RenderPassEvent.BeforeRenderingOpaques)
                    {
                        severeWarningCount++;
                        Debug.LogError(
                            $"{LogPrefix} Severe: renderer '{rendererData.name}' feature '{feature.name}' " +
                            $"({feature.GetType().FullName}) injects at '{injectionPoint}' and source '{sourcePath}' contains '{matchedPattern}'. " +
                            "This can destabilize _CameraDepthTexture before RenderOpaqueForward.");
                    }
                    else if (injectionPoint <= RenderPassEvent.BeforeRenderingTransparents)
                    {
                        Debug.LogWarning(
                            $"{LogPrefix} Renderer '{rendererData.name}' feature '{feature.name}' " +
                            $"({feature.GetType().FullName}) injects at '{injectionPoint}' and source '{sourcePath}' contains '{matchedPattern}'. " +
                            "Review this feature if Crest depth or transparency regresses.");
                    }
                }
            }

            return severeWarningCount;
        }

        private static bool EnsureRequiredRendererFeature<TFeature>(UniversalRendererData rendererData)
            where TFeature : ScriptableRendererFeature
        {
            if (HasRendererFeature<TFeature>(rendererData))
                return false;

            string assetPath = AssetDatabase.GetAssetPath(rendererData);
            TFeature feature = FindFeatureSubAsset<TFeature>(assetPath);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<TFeature>();
                feature.name = typeof(TFeature).Name;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
            }

            if (!AppendRendererFeatureReference(rendererData, feature))
                return false;

            feature.Create();
            EditorUtility.SetDirty(feature);
            return true;
        }

        private static bool RestoreSerializedRendererFeatures(UniversalRendererData rendererData)
        {
            if (rendererData == null)
                return false;

            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty rendererFeaturesProperty = serializedRenderer.FindProperty(RendererFeaturesPropertyPath);
            SerializedProperty mapProperty = serializedRenderer.FindProperty(RendererFeatureMapPropertyPath);
            if (rendererFeaturesProperty == null)
                return false;

            bool rendererChanged = false;
            rendererChanged |= CompactNullRendererFeatureReferences(rendererFeaturesProperty);
            rendererChanged |= RebuildRendererFeatureMap(rendererFeaturesProperty, mapProperty);
            if (!rendererChanged)
                return false;

            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            if (s_ValidateRendererFeaturesMethod != null)
                s_ValidateRendererFeaturesMethod.Invoke(rendererData, null);

            EditorUtility.SetDirty(rendererData);
            return true;
        }

        private static bool CompactNullRendererFeatureReferences(SerializedProperty rendererFeaturesProperty)
        {
            if (rendererFeaturesProperty == null)
                return false;

            bool changed = false;
            for (int featureIndex = rendererFeaturesProperty.arraySize - 1; featureIndex >= 0; featureIndex--)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                if (featureProperty.objectReferenceValue != null)
                    continue;

                int sizeBeforeDelete = rendererFeaturesProperty.arraySize;
                rendererFeaturesProperty.DeleteArrayElementAtIndex(featureIndex);
                if (rendererFeaturesProperty.arraySize == sizeBeforeDelete)
                    rendererFeaturesProperty.DeleteArrayElementAtIndex(featureIndex);

                changed = true;
            }

            return changed;
        }

        private static bool RebuildRendererFeatureMap(
            SerializedProperty rendererFeaturesProperty,
            SerializedProperty mapProperty)
        {
            if (rendererFeaturesProperty == null || mapProperty == null)
                return false;

            bool changed = false;
            if (mapProperty.arraySize != rendererFeaturesProperty.arraySize)
            {
                mapProperty.arraySize = rendererFeaturesProperty.arraySize;
                changed = true;
            }

            for (int featureIndex = 0; featureIndex < rendererFeaturesProperty.arraySize; featureIndex++)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                ScriptableRendererFeature feature = featureProperty.objectReferenceValue as ScriptableRendererFeature;

                long localId = 0L;
                if (feature != null)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out localId);

                SerializedProperty mapEntryProperty = mapProperty.GetArrayElementAtIndex(featureIndex);
                if (mapEntryProperty.longValue == localId)
                    continue;

                mapEntryProperty.longValue = localId;
                changed = true;
            }

            return changed;
        }

        private static bool EnsureRequiredFeatureState(
            UniversalRendererData rendererData,
            string featureTypeName,
            RenderPassEvent expectedInjectionPoint)
        {
            ScriptableRendererFeature feature = FindRendererFeature(rendererData, featureTypeName);
            if (feature == null)
                return false;

            bool changed = false;
            if (!feature.isActive)
            {
                feature.SetActive(true);
                changed = true;
            }

            SerializedObject serializedFeature = new SerializedObject(feature);
            if (TryResolveInjectionPointProperty(serializedFeature, out SerializedProperty injectionPointProperty) &&
                injectionPointProperty.intValue != (int)expectedInjectionPoint)
            {
                injectionPointProperty.intValue = (int)expectedInjectionPoint;
                serializedFeature.ApplyModifiedPropertiesWithoutUndo();
                feature.Create();
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(feature);

            return changed;
        }

        private static int ValidateFeatureOrdering(
            UniversalRendererData rendererData,
            ScriptableRendererFeature feature,
            RenderPassEvent injectionPoint)
        {
            string featureTypeName = feature.GetType().FullName;
            RenderPassEvent expectedInjectionPoint;
            if (string.Equals(featureTypeName, VolumetricFeatureTypeName, StringComparison.Ordinal) ||
                string.Equals(featureTypeName, SsdoFeatureTypeName, StringComparison.Ordinal))
            {
                expectedInjectionPoint = RenderPassEvent.BeforeRenderingTransparents;
            }
            else if (string.Equals(featureTypeName, VisorFluidFeatureTypeName, StringComparison.Ordinal))
            {
                expectedInjectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            }
            else
            {
                return 0;
            }

            if (injectionPoint == expectedInjectionPoint)
                return 0;

            Debug.LogError(
                $"{LogPrefix} Renderer '{rendererData.name}' feature '{feature.name}' " +
                $"({featureTypeName}) is serialized at '{injectionPoint}'. " +
                $"Expected '{expectedInjectionPoint}' so the managed visor/noir stack stays in the validated URP order.");
            return 1;
        }

        private static int ValidateCrestScene()
        {
            int issueCount = 0;
            OceanRenderer[] oceans = Resources.FindObjectsOfTypeAll<OceanRenderer>();
            UnderwaterRenderer[] underwaterRenderers = Resources.FindObjectsOfTypeAll<UnderwaterRenderer>();
            OceanDepthCache[] depthCaches = Resources.FindObjectsOfTypeAll<OceanDepthCache>();
            RegisterSeaFloorDepthInput[] depthInputs = Resources.FindObjectsOfTypeAll<RegisterSeaFloorDepthInput>();
            UniversalAdditionalCameraData[] cameraDataList = Resources.FindObjectsOfTypeAll<UniversalAdditionalCameraData>();

            int activeOceanCount = 0;
            int activeUnderwaterCount = 0;
            int activeDepthCacheCount = 0;
            int activeDepthInputCount = 0;

            for (int i = 0; i < underwaterRenderers.Length; i++)
            {
                if (IsSceneObject(underwaterRenderers[i]))
                    activeUnderwaterCount++;
            }

            for (int i = 0; i < oceans.Length; i++)
            {
                OceanRenderer ocean = oceans[i];
                if (!IsSceneObject(ocean))
                    continue;

                activeOceanCount++;
                ValidateOceanRenderer(ocean, activeUnderwaterCount > 0, ref issueCount);
            }

            for (int i = 0; i < underwaterRenderers.Length; i++)
            {
                UnderwaterRenderer underwater = underwaterRenderers[i];
                if (!IsSceneObject(underwater))
                    continue;

                ValidateUnderwaterCamera(underwater, cameraDataList, ref issueCount);
            }

            for (int i = 0; i < depthCaches.Length; i++)
            {
                OceanDepthCache depthCache = depthCaches[i];
                if (!IsSceneObject(depthCache))
                    continue;

                activeDepthCacheCount++;
                ValidateDepthCache(depthCache, ref issueCount);
            }

            for (int i = 0; i < depthInputs.Length; i++)
            {
                if (IsSceneObject(depthInputs[i]))
                    activeDepthInputCount++;
            }

            if (activeOceanCount > 0)
            {
                for (int i = 0; i < oceans.Length; i++)
                {
                    OceanRenderer ocean = oceans[i];
                    if (!IsSceneObject(ocean) || !ocean.CreateSeaFloorDepthData)
                        continue;

                    if (activeDepthCacheCount == 0 && activeDepthInputCount == 0)
                    {
                        issueCount++;
                        Debug.LogError(
                            $"{LogPrefix} Ocean '{GetHierarchyPath(ocean.transform)}' enables sea-floor depth simulation, " +
                            "but no active OceanDepthCache or RegisterSeaFloorDepthInput exists in the open scene.");
                    }
                }
            }

            return issueCount;
        }

        private static int ValidateCelestialSkybox(bool applyRepairs)
        {
            int issueCount = 0;
            Material blendedSkyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>(BlendedSkyboxMaterialPath);
            Material daySkyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>(DaySkyboxMaterialPath);
            Material nightSkyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>(NightSkyboxMaterialPath);
            Material meshSkyMaterial = AssetDatabase.LoadAssetAtPath<Material>(MeshSkyMaterialPath);
            HectonCelestialEngine[] celestialEngines = Resources.FindObjectsOfTypeAll<HectonCelestialEngine>();

            for (int engineIndex = 0; engineIndex < celestialEngines.Length; engineIndex++)
            {
                HectonCelestialEngine celestialEngine = celestialEngines[engineIndex];
                if (!IsSceneObject(celestialEngine))
                    continue;

                SerializedObject serializedEngine = new SerializedObject(celestialEngine);
                bool engineChanged = false;
                engineChanged |= EnsureMaterialReference(serializedEngine.FindProperty("_skyMaterial"), meshSkyMaterial, applyRepairs);
                engineChanged |= EnsureMaterialReference(serializedEngine.FindProperty("daySkybox"), daySkyboxMaterial, applyRepairs);
                engineChanged |= EnsureMaterialReference(serializedEngine.FindProperty("nightSkybox"), nightSkyboxMaterial, applyRepairs);
                engineChanged |= EnsureMaterialReference(
                    serializedEngine.FindProperty("blendedSkyboxMaterial"),
                    blendedSkyboxMaterial,
                    applyRepairs);

                SerializedProperty blendedSkyboxProperty = serializedEngine.FindProperty("blendedSkyboxMaterial");
                if (blendedSkyboxProperty == null || blendedSkyboxProperty.objectReferenceValue == null)
                {
                    issueCount++;
                    Debug.LogError(
                        $"{LogPrefix} Celestial engine '{GetHierarchyPath(celestialEngine.transform)}' is missing blendedSkyboxMaterial.");
                }

                if (engineChanged && applyRepairs)
                {
                    serializedEngine.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(celestialEngine);
                    EditorSceneManager.MarkSceneDirty(celestialEngine.gameObject.scene);
                }
            }

            Material currentSkybox = AtmosphereDirector.Skybox;
            if (!ReferenceEquals(currentSkybox, meshSkyMaterial))
            {
                issueCount++;
                if (applyRepairs && meshSkyMaterial != null)
                {
                    AtmosphereDirector.SetSkybox(meshSkyMaterial);
                    EditorSceneManager.MarkAllScenesDirty();
                }
                else
                {
                    string currentSkyboxName = currentSkybox != null ? currentSkybox.name : "<null>";
                    Debug.LogError(
                        $"{LogPrefix} RenderSettings.skybox is '{currentSkyboxName}', not Mat_HectonSky.");
                }
            }

            return issueCount;
        }

        private static void ValidateOceanRenderer(OceanRenderer ocean, bool hasUnderwaterRenderer, ref int issueCount)
        {
            Material oceanMaterial = ocean.OceanMaterial;
            if (oceanMaterial == null)
            {
                issueCount++;
                Debug.LogError($"{LogPrefix} Ocean '{GetHierarchyPath(ocean.transform)}' has no material assigned.");
                return;
            }

            if (!hasUnderwaterRenderer)
                return;

            bool underwaterKeywordEnabled = oceanMaterial.IsKeywordEnabled("_UNDERWATER_ON");
            float underwaterToggle = oceanMaterial.HasProperty("_Underwater") ? oceanMaterial.GetFloat("_Underwater") : 0f;
            if (!underwaterKeywordEnabled || underwaterToggle < 0.5f)
            {
                issueCount++;
                Debug.LogError(
                    $"{LogPrefix} Ocean material '{oceanMaterial.name}' is missing underwater support " +
                    $"for ocean '{GetHierarchyPath(ocean.transform)}'. _UNDERWATER_ON={underwaterKeywordEnabled}, _Underwater={underwaterToggle}.");
            }

            if (oceanMaterial.HasProperty("_CullMode") &&
                Mathf.Approximately(oceanMaterial.GetFloat("_CullMode"), (int)CullMode.Back))
            {
                issueCount++;
                Debug.LogError(
                    $"{LogPrefix} Ocean material '{oceanMaterial.name}' is still back-face culled for ocean '{GetHierarchyPath(ocean.transform)}'. " +
                    "This breaks Crest underwater surface rendering.");
            }
        }

        private static void ValidateUnderwaterCamera(
            UnderwaterRenderer underwater,
            UniversalAdditionalCameraData[] cameraDataList,
            ref int issueCount)
        {
            Camera camera = underwater.GetComponent<Camera>();
            if (camera == null)
            {
                issueCount++;
                Debug.LogError($"{LogPrefix} Underwater renderer '{GetHierarchyPath(underwater.transform)}' is missing a Camera component.");
                return;
            }

            UniversalAdditionalCameraData matchingData = null;
            for (int cameraIndex = 0; cameraIndex < cameraDataList.Length; cameraIndex++)
            {
                UniversalAdditionalCameraData cameraData = cameraDataList[cameraIndex];
                if (!IsSceneObject(cameraData) || cameraData.gameObject != underwater.gameObject)
                    continue;

                matchingData = cameraData;
                break;
            }

            if (matchingData == null)
            {
                issueCount++;
                Debug.LogError(
                    $"{LogPrefix} Underwater camera '{GetHierarchyPath(underwater.transform)}' is missing UniversalAdditionalCameraData.");
                return;
            }

            if (matchingData.requiresDepthOption != CameraOverrideOption.On)
            {
                issueCount++;
                Debug.LogError(
                    $"{LogPrefix} Underwater camera '{GetHierarchyPath(underwater.transform)}' does not force depth texture generation. " +
                    $"requiresDepthOption={matchingData.requiresDepthOption}.");
            }

            if (matchingData.requiresColorOption != CameraOverrideOption.On)
            {
                issueCount++;
                Debug.LogError(
                    $"{LogPrefix} Underwater camera '{GetHierarchyPath(underwater.transform)}' does not force opaque texture copying. " +
                    $"requiresColorOption={matchingData.requiresColorOption}.");
            }
        }

        private static void ValidateDepthCache(OceanDepthCache depthCache, ref int issueCount)
        {
            SerializedObject serializedDepthCache = new SerializedObject(depthCache);
            SerializedProperty layersProperty = serializedDepthCache.FindProperty("_layers");
            SerializedProperty resolutionProperty = serializedDepthCache.FindProperty("_resolution");

            if (layersProperty != null && layersProperty.intValue == 0)
            {
                issueCount++;
                Debug.LogError(
                    $"{LogPrefix} OceanDepthCache '{GetHierarchyPath(depthCache.transform)}' has no layers assigned for capture.");
            }

            if (resolutionProperty != null && resolutionProperty.intValue < 64)
            {
                issueCount++;
                Debug.LogWarning(
                    $"{LogPrefix} OceanDepthCache '{GetHierarchyPath(depthCache.transform)}' resolution is {resolutionProperty.intValue}. " +
                    "Very low cache resolution can make water read as transparent or unstable near shore.");
            }

            Vector3 scale = depthCache.transform.lossyScale;
            if (!Mathf.Approximately(scale.x, scale.z) || !Mathf.Approximately(scale.y, 1f))
            {
                issueCount++;
                Debug.LogError(
                    $"{LogPrefix} OceanDepthCache '{GetHierarchyPath(depthCache.transform)}' has invalid scale {scale}. " +
                    "Crest requires uniform X/Z and Y=1 for stable sea-floor depth capture.");
            }
        }

        private static bool TryResolveInjectionPoint(ScriptableRendererFeature feature, out RenderPassEvent injectionPoint)
        {
            SerializedObject serializedFeature = new SerializedObject(feature);
            if (TryResolveInjectionPointProperty(serializedFeature, out SerializedProperty property))
            {
                injectionPoint = (RenderPassEvent)property.intValue;
                return true;
            }

            injectionPoint = default;
            return false;
        }

        private static bool TryResolveInjectionPointProperty(
            SerializedObject serializedObject,
            out SerializedProperty property)
        {
            property = serializedObject.FindProperty("settings.injectionPoint");
            if (property != null)
                return true;

            property = serializedObject.FindProperty("m_Settings.injectionPoint");
            if (property != null)
                return true;

            property = serializedObject.FindProperty("injectionPoint");
            return property != null;
        }

        private static bool AppendRendererFeatureReference(UniversalRendererData rendererData, ScriptableRendererFeature feature)
        {
            if (rendererData == null || feature == null)
                return false;

            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty rendererFeaturesProperty = serializedRenderer.FindProperty(RendererFeaturesPropertyPath);
            if (rendererFeaturesProperty == null)
                return false;

            if (ContainsRendererFeatureReference(rendererFeaturesProperty, feature))
                return false;

            int featureIndex = rendererFeaturesProperty.arraySize;
            rendererFeaturesProperty.arraySize++;
            rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex).objectReferenceValue = feature;

            SerializedProperty mapProperty = serializedRenderer.FindProperty(RendererFeatureMapPropertyPath);
            if (mapProperty != null)
            {
                long localId = 0;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out localId);
                mapProperty.arraySize = rendererFeaturesProperty.arraySize;
                mapProperty.GetArrayElementAtIndex(featureIndex).longValue = localId;
            }

            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TryReadFeatureSource(
            ScriptableRendererFeature feature,
            out string sourcePath,
            out string sourceText)
        {
            MonoScript script = MonoScript.FromScriptableObject(feature);
            if (script == null)
            {
                sourcePath = null;
                sourceText = null;
                return false;
            }

            sourcePath = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                sourceText = null;
                return false;
            }

            sourceText = File.ReadAllText(sourcePath);
            return true;
        }

        private static bool TryFindDepthMutationPattern(string sourceText, out string matchedPattern)
        {
            for (int patternIndex = 0; patternIndex < s_DepthMutationPatterns.Length; patternIndex++)
            {
                string pattern = s_DepthMutationPatterns[patternIndex];
                if (!sourceText.Contains(pattern, StringComparison.Ordinal))
                    continue;

                matchedPattern = pattern;
                return true;
            }

            matchedPattern = null;
            return false;
        }

        private static bool HasRendererFeature<TFeature>(UniversalRendererData rendererData)
            where TFeature : ScriptableRendererFeature
        {
            var rendererFeatures = rendererData.rendererFeatures;
            for (int featureIndex = 0; featureIndex < rendererFeatures.Count; featureIndex++)
            {
                if (rendererFeatures[featureIndex] is TFeature)
                    return true;
            }

            return false;
        }

        private static TFeature FindFeatureSubAsset<TFeature>(string assetPath)
            where TFeature : ScriptableRendererFeature
        {
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int subAssetIndex = 0; subAssetIndex < subAssets.Length; subAssetIndex++)
            {
                if (subAssets[subAssetIndex] is TFeature feature)
                    return feature;
            }

            return null;
        }

        private static bool HasNullRendererFeatures(UniversalRendererData rendererData)
        {
            for (int featureIndex = 0; featureIndex < rendererData.rendererFeatures.Count; featureIndex++)
            {
                if (rendererData.rendererFeatures[featureIndex] == null)
                    return true;
            }

            return false;
        }

        private static bool HasRendererFeatureMapMismatch(UniversalRendererData rendererData)
        {
            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty rendererFeaturesProperty = serializedRenderer.FindProperty(RendererFeaturesPropertyPath);
            SerializedProperty mapProperty = serializedRenderer.FindProperty(RendererFeatureMapPropertyPath);
            return rendererFeaturesProperty != null &&
                   mapProperty != null &&
                   rendererFeaturesProperty.arraySize != mapProperty.arraySize;
        }

        private static bool ContainsRendererFeatureReference(
            SerializedProperty rendererFeaturesProperty,
            ScriptableRendererFeature feature)
        {
            if (rendererFeaturesProperty == null || feature == null)
                return false;

            for (int featureIndex = 0; featureIndex < rendererFeaturesProperty.arraySize; featureIndex++)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                if (featureProperty.objectReferenceValue == feature)
                    return true;
            }

            return false;
        }

        private static bool EnsureMaterialReference(
            SerializedProperty property,
            Material material,
            bool applyRepairs)
        {
            if (!applyRepairs || property == null || property.objectReferenceValue != null || material == null)
                return false;

            property.objectReferenceValue = material;
            return true;
        }

        private static ScriptableRendererFeature FindRendererFeature(UniversalRendererData rendererData, string featureTypeName)
        {
            var rendererFeatures = rendererData.rendererFeatures;
            for (int featureIndex = 0; featureIndex < rendererFeatures.Count; featureIndex++)
            {
                ScriptableRendererFeature feature = rendererFeatures[featureIndex];
                if (feature == null)
                    continue;

                if (string.Equals(feature.GetType().FullName, featureTypeName, StringComparison.Ordinal))
                    return feature;
            }

            return null;
        }

        private static bool IsCustomFeature(ScriptableRendererFeature feature)
        {
            Type featureType = feature.GetType();
            string featureNamespace = featureType.Namespace;
            if (string.IsNullOrEmpty(featureNamespace))
                return true;

            return !featureNamespace.StartsWith("UnityEngine.Rendering.Universal", StringComparison.Ordinal);
        }

        private static bool IsManagedRenderer(UniversalRendererData rendererData)
        {
            for (int rendererNameIndex = 0; rendererNameIndex < s_TargetRendererNames.Length; rendererNameIndex++)
            {
                if (string.Equals(rendererData.name, s_TargetRendererNames[rendererNameIndex], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsSceneObject(UnityEngine.Object obj)
        {
            if (obj == null || EditorUtility.IsPersistent(obj))
                return false;

            if (obj is Component component)
                return component.gameObject.scene.IsValid();

            if (obj is GameObject gameObject)
                return gameObject.scene.IsValid();

            return false;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static UniversalRenderPipelineAsset ResolveActiveUrpAsset()
        {
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset qualityUrpAsset)
                return qualityUrpAsset;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentUrpAsset)
                return currentUrpAsset;

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultUrpAsset)
                return defaultUrpAsset;

            return UniversalRenderPipeline.asset;
        }

        private static void LogOpenSceneDepthCacheAlignment()
        {
            HectonCrestOceanDepthCacheBootstrap bootstrap = FindSceneObject<HectonCrestOceanDepthCacheBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError($"{LogPrefix} Open-scene depth-cache audit failed: no HectonCrestOceanDepthCacheBootstrap was found.");
                return;
            }

            Terrain[] terrains = Resources.FindObjectsOfTypeAll<Terrain>();
            Vector3 runtimeMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 runtimeMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 absoluteMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 absoluteMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int terrainCount = 0;

            for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
            {
                Terrain terrain = terrains[terrainIndex];
                if (!IsSceneObject(terrain) || terrain.terrainData == null)
                    continue;

                Vector3 runtimeTerrainMin = terrain.transform.position;
                Vector3 runtimeTerrainMax = runtimeTerrainMin + terrain.terrainData.size;
                Vector3 absoluteTerrainMin = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeTerrainMin);
                Vector3 absoluteTerrainMax = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeTerrainMax);

                runtimeMin = Vector3.Min(runtimeMin, runtimeTerrainMin);
                runtimeMax = Vector3.Max(runtimeMax, runtimeTerrainMax);
                absoluteMin = Vector3.Min(absoluteMin, absoluteTerrainMin);
                absoluteMax = Vector3.Max(absoluteMax, absoluteTerrainMax);
                terrainCount++;
            }

            if (terrainCount == 0)
            {
                Debug.LogError($"{LogPrefix} Open-scene depth-cache audit failed: terrainCount=0 in '{SceneManager.GetActiveScene().name}'.");
                return;
            }

            float terrainBoundsPadding = 64f;
            SerializedObject serializedBootstrap = new SerializedObject(bootstrap);
            SerializedProperty terrainBoundsPaddingProperty = serializedBootstrap.FindProperty("terrainBoundsPadding");
            if (terrainBoundsPaddingProperty != null)
                terrainBoundsPadding = terrainBoundsPaddingProperty.floatValue;

            OceanRenderer oceanRenderer = bootstrap.GetComponent<OceanRenderer>();
            MapMagicBridge mapMagicBridge = FindSceneObject<MapMagicBridge>();
            float runtimeWaterLevel = ResolveAuditWaterLevel(bootstrap.transform.position.y, oceanRenderer, mapMagicBridge);
            float absoluteWaterLevel = runtimeWaterLevel + HectonFloatingOrigin.CurrentTotalOffset.y;
            float paddedAbsoluteMinX = absoluteMin.x - terrainBoundsPadding;
            float paddedAbsoluteMaxX = absoluteMax.x + terrainBoundsPadding;
            float paddedAbsoluteMinZ = absoluteMin.z - terrainBoundsPadding;
            float paddedAbsoluteMaxZ = absoluteMax.z + terrainBoundsPadding;
            float coverageSize = Mathf.Max(256f, Mathf.Max(paddedAbsoluteMaxX - paddedAbsoluteMinX, paddedAbsoluteMaxZ - paddedAbsoluteMinZ));
            float halfCoverageSize = coverageSize * 0.5f;
            Vector3 computedCacheCenterAUP = new Vector3(
                (paddedAbsoluteMinX + paddedAbsoluteMaxX) * 0.5f,
                absoluteWaterLevel,
                (paddedAbsoluteMinZ + paddedAbsoluteMaxZ) * 0.5f);
            Vector3 computedCacheCenterWS = HectonFloatingOrigin.ToRuntimePosition(computedCacheCenterAUP);
            Vector3 computedCacheMinWS = computedCacheCenterWS - new Vector3(halfCoverageSize, 0f, halfCoverageSize);
            Vector3 computedCacheMaxWS = computedCacheCenterWS + new Vector3(halfCoverageSize, 0f, halfCoverageSize);
            Vector3 computedCacheMinAUP = computedCacheCenterAUP - new Vector3(halfCoverageSize, 0f, halfCoverageSize);
            Vector3 computedCacheMaxAUP = computedCacheCenterAUP + new Vector3(halfCoverageSize, 0f, halfCoverageSize);
            bool coversTerrainWS =
                computedCacheMinWS.x <= runtimeMin.x &&
                computedCacheMaxWS.x >= runtimeMax.x &&
                computedCacheMinWS.z <= runtimeMin.z &&
                computedCacheMaxWS.z >= runtimeMax.z;
            bool coversTerrainAUP =
                computedCacheMinAUP.x <= absoluteMin.x &&
                computedCacheMaxAUP.x >= absoluteMax.x &&
                computedCacheMinAUP.z <= absoluteMin.z &&
                computedCacheMaxAUP.z >= absoluteMax.z;

            Debug.Log(
                $"{LogPrefix} DepthCacheAlignment scene={SceneManager.GetActiveScene().name} terrainCount={terrainCount} " +
                $"terrainMinWS={runtimeMin:F3} terrainMaxWS={runtimeMax:F3} " +
                $"terrainMinAUP={absoluteMin:F3} terrainMaxAUP={absoluteMax:F3} " +
                $"cacheMinWS={computedCacheMinWS:F3} cacheMaxWS={computedCacheMaxWS:F3} " +
                $"cacheMinAUP={computedCacheMinAUP:F3} cacheMaxAUP={computedCacheMaxAUP:F3} " +
                $"waterLevelWS={runtimeWaterLevel:F3} waterLevelAUP={absoluteWaterLevel:F3} " +
                $"coverageSize={coverageSize:F3} coversTerrainWS={coversTerrainWS} coversTerrainAUP={coversTerrainAUP}");
        }

        private static float ResolveAuditWaterLevel(float fallbackWaterLevel, OceanRenderer oceanRenderer, MapMagicBridge mapMagicBridge)
        {
            if (oceanRenderer != null && oceanRenderer.Root != null)
            {
                float rootWaterLevel = oceanRenderer.Root.position.y;
                if (!float.IsNaN(rootWaterLevel) && !float.IsInfinity(rootWaterLevel))
                    return rootWaterLevel;
            }

            if (mapMagicBridge != null)
            {
                float bridgedWaterLevel = mapMagicBridge.WaterSurfaceLevel;
                if (!float.IsNaN(bridgedWaterLevel) && !float.IsInfinity(bridgedWaterLevel))
                    return bridgedWaterLevel;
            }

            return fallbackWaterLevel;
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
            {
                if (IsSceneObject(objects[objectIndex]))
                    return objects[objectIndex];
            }

            return null;
        }
    }
}
#endif
