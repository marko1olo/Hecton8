#if UNITY_EDITOR
using Hecton8.Rendering.OceanSinglePass;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Editor
{
    internal static class SinglePassOceanRendererFeatureInstaller
    {
        private const string MenuPath = "Hecton/Rendering/SHINOBU_262/Install Single-Pass Ocean RenderGraph";
        private const string SessionKey = "Hecton8.SinglePassOceanRendererFeatureInstaller.Ran";
        private const string RendererFeaturesPropertyPath = "m_RendererFeatures";
        private const string RendererFeatureMapPropertyPath = "m_RendererFeatureMap";
        private const string RequireDepthTexturePropertyPath = "m_RequireDepthTexture";
        private const string DepthShaderAssetPath = "Assets/_Project/Art/Shaders/Hidden_Hecton_OceanDepthFoam.shader";
        private const string WakeComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_WakeDisplacement.compute";
        private const string ClearWakeKernelName = "ClearWake";
        private const string AccumulateWakeKernelName = "AccumulateWake";

        private static readonly string[] s_RendererAssetPaths =
        {
            "Assets/_Project/Data/PC_Renderer.asset",
            "Assets/_Project/Data/PC_High_Renderer.asset",
            "Assets/_Project/Data/Mobile_Renderer.asset",
            "Assets/_Project/Data/Quest_VR_Renderer.asset"
        };

        private static readonly string[] s_PipelineAssetPaths =
        {
            "Assets/_Project/Data/URP_Low (PC_RPAsset).asset",
            "Assets/_Project/Data/URP_Medium (PC_RPAsset).asset",
            "Assets/_Project/Data/URP_High (PC_RPAsset).asset",
            "Assets/_Project/Data/URP_Quest_VR.asset"
        };

        [InitializeOnLoadMethod]
        private static void QueueInstallAfterReload()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall -= InstallRequiredFeaturesForActiveBuildTarget;
            EditorApplication.delayCall += InstallRequiredFeaturesForActiveBuildTarget;
        }

        [MenuItem(MenuPath)]
        internal static void InstallRequiredFeatures()
        {
            InstallRequiredFeatures(BuildTarget.NoTarget);
        }

        private static void InstallRequiredFeaturesForActiveBuildTarget()
        {
            InstallRequiredFeatures(EditorUserBuildSettings.activeBuildTarget);
        }

        internal static void InstallRequiredFeatures(BuildTarget buildTarget)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                if (buildTarget == BuildTarget.NoTarget)
                {
                    EditorApplication.delayCall -= InstallRequiredFeatures;
                    EditorApplication.delayCall += InstallRequiredFeatures;
                }
                else
                {
                    EditorApplication.delayCall -= InstallRequiredFeaturesForActiveBuildTarget;
                    EditorApplication.delayCall += InstallRequiredFeaturesForActiveBuildTarget;
                }

                return;
            }

            Shader depthShader = AssetDatabase.LoadAssetAtPath<Shader>(DepthShaderAssetPath);
            ComputeShader wakeCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(WakeComputeAssetPath);
            bool changed = false;

            for (int pipelineIndex = 0; pipelineIndex < s_PipelineAssetPaths.Length; pipelineIndex++)
            {
                string pipelineAssetPath = s_PipelineAssetPaths[pipelineIndex];
                if (!ShouldValidatePipelineAsset(buildTarget, pipelineAssetPath))
                    continue;

                changed |= EnsurePipelineDepthTexture(pipelineAssetPath);
            }

            for (int rendererIndex = 0; rendererIndex < s_RendererAssetPaths.Length; rendererIndex++)
            {
                string rendererAssetPath = s_RendererAssetPaths[rendererIndex];
                if (!ShouldValidateRendererAsset(buildTarget, rendererAssetPath))
                    continue;

                changed |= EnsureRendererFeature(rendererAssetPath, depthShader, wakeCompute);
            }

            if (changed)
                AssetDatabase.SaveAssets();
        }

        internal static bool VerifyRequiredFeatures(out string failure)
        {
            return VerifyRequiredFeatures(BuildTarget.NoTarget, out failure);
        }

        internal static bool VerifyRequiredFeatures(BuildTarget buildTarget, out string failure)
        {
            failure = null;
            Shader depthShader = AssetDatabase.LoadAssetAtPath<Shader>(DepthShaderAssetPath);
            if (depthShader == null)
            {
                failure = "[SHINOBU_262] Missing depth foam shader asset: " + DepthShaderAssetPath;
                return false;
            }

            ComputeShader wakeCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(WakeComputeAssetPath);
            if (!VerifyComputeKernels(wakeCompute))
            {
                failure = "[SHINOBU_262] Wake compute shader is missing ClearWake or AccumulateWake.";
                return false;
            }

            for (int pipelineIndex = 0; pipelineIndex < s_PipelineAssetPaths.Length; pipelineIndex++)
            {
                string pipelineAssetPath = s_PipelineAssetPaths[pipelineIndex];
                if (!ShouldValidatePipelineAsset(buildTarget, pipelineAssetPath))
                    continue;

                if (!VerifyPipelineDepthTexture(pipelineAssetPath))
                {
                    failure = "[SHINOBU_262] URP asset disables camera depth texture required by single-pass ocean depth extraction: " + pipelineAssetPath;
                    return false;
                }
            }

            for (int rendererIndex = 0; rendererIndex < s_RendererAssetPaths.Length; rendererIndex++)
            {
                string rendererAssetPath = s_RendererAssetPaths[rendererIndex];
                if (!ShouldValidateRendererAsset(buildTarget, rendererAssetPath))
                    continue;

                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);
                if (rendererData == null)
                {
                    failure = "[SHINOBU_262] Missing renderer asset: " + rendererAssetPath;
                    return false;
                }

                HectonSinglePassOceanFeature feature = FindFeatureSubAsset(rendererAssetPath);
                if (feature == null)
                {
                    failure = "[SHINOBU_262] Renderer asset has no Single-Pass Ocean sub-asset: " + rendererAssetPath;
                    return false;
                }

                if (!VerifyFeatureReference(rendererData, feature))
                {
                    failure = "[SHINOBU_262] Renderer feature reference/map is not serialized: " + rendererAssetPath;
                    return false;
                }

                if (!VerifyFeatureSettings(feature, depthShader, wakeCompute))
                {
                    failure = "[SHINOBU_262] Renderer feature settings are not bound to the ocean RenderGraph route: " + rendererAssetPath;
                    return false;
                }
            }

            return true;
        }

        private static bool VerifyComputeKernels(ComputeShader wakeCompute)
        {
            return wakeCompute != null &&
                   wakeCompute.HasKernel(ClearWakeKernelName) &&
                   wakeCompute.HasKernel(AccumulateWakeKernelName);
        }

        private static bool ShouldValidatePipelineAsset(BuildTarget buildTarget, string pipelineAssetPath)
        {
            if (buildTarget == BuildTarget.NoTarget)
                return true;

            if (buildTarget == BuildTarget.Android)
                return pipelineAssetPath == s_PipelineAssetPaths[0] ||
                       pipelineAssetPath == s_PipelineAssetPaths[3];

            if (buildTarget == BuildTarget.iOS)
                return pipelineAssetPath == s_PipelineAssetPaths[0];

            if (IsStandaloneTarget(buildTarget))
                return pipelineAssetPath == s_PipelineAssetPaths[0] ||
                       pipelineAssetPath == s_PipelineAssetPaths[1] ||
                       pipelineAssetPath == s_PipelineAssetPaths[2];

            return true;
        }

        private static bool ShouldValidateRendererAsset(BuildTarget buildTarget, string rendererAssetPath)
        {
            if (buildTarget == BuildTarget.NoTarget)
                return true;

            if (buildTarget == BuildTarget.Android)
                return rendererAssetPath == s_RendererAssetPaths[2] ||
                       rendererAssetPath == s_RendererAssetPaths[3];

            if (buildTarget == BuildTarget.iOS)
                return rendererAssetPath == s_RendererAssetPaths[2];

            if (IsStandaloneTarget(buildTarget))
                return rendererAssetPath == s_RendererAssetPaths[0] ||
                       rendererAssetPath == s_RendererAssetPaths[1];

            return true;
        }

        private static bool IsStandaloneTarget(BuildTarget buildTarget)
        {
            return buildTarget == BuildTarget.StandaloneWindows ||
                   buildTarget == BuildTarget.StandaloneWindows64 ||
                   buildTarget == BuildTarget.StandaloneOSX ||
                   buildTarget == BuildTarget.StandaloneLinux64;
        }

        private static bool EnsurePipelineDepthTexture(string pipelineAssetPath)
        {
            Object pipelineAsset = AssetDatabase.LoadAssetAtPath<Object>(pipelineAssetPath);
            if (pipelineAsset == null)
                return false;

            SerializedObject serializedPipeline = new SerializedObject(pipelineAsset);
            SerializedProperty depthTextureProperty = serializedPipeline.FindProperty(RequireDepthTexturePropertyPath);
            if (depthTextureProperty == null || depthTextureProperty.boolValue)
                return false;

            depthTextureProperty.boolValue = true;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssetIfDirty(pipelineAsset);
            return true;
        }

        private static bool VerifyPipelineDepthTexture(string pipelineAssetPath)
        {
            Object pipelineAsset = AssetDatabase.LoadAssetAtPath<Object>(pipelineAssetPath);
            if (pipelineAsset == null)
                return false;

            SerializedObject serializedPipeline = new SerializedObject(pipelineAsset);
            SerializedProperty depthTextureProperty = serializedPipeline.FindProperty(RequireDepthTexturePropertyPath);
            return depthTextureProperty != null && depthTextureProperty.boolValue;
        }

        private static bool EnsureRendererFeature(string rendererAssetPath, Shader depthShader, ComputeShader wakeCompute)
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);
            if (rendererData == null)
                return false;

            HectonSinglePassOceanFeature feature = FindFeatureSubAsset(rendererAssetPath);
            bool changed = false;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<HectonSinglePassOceanFeature>();
                feature.name = nameof(HectonSinglePassOceanFeature);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                changed = true;
            }

            changed |= EnsureFeatureReference(rendererData, feature);
            changed |= EnsureFeatureSettings(feature, depthShader, wakeCompute);
            if (!feature.isActive)
            {
                feature.SetActive(true);
                changed = true;
            }

            if (!changed)
                return false;

            feature.Create();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssetIfDirty(rendererData);
            return true;
        }

        private static HectonSinglePassOceanFeature FindFeatureSubAsset(string rendererAssetPath)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(rendererAssetPath);
            for (int subAssetIndex = 0; subAssetIndex < subAssets.Length; subAssetIndex++)
            {
                if (subAssets[subAssetIndex] is HectonSinglePassOceanFeature feature)
                    return feature;
            }

            return null;
        }

        private static bool EnsureFeatureReference(
            UniversalRendererData rendererData,
            HectonSinglePassOceanFeature feature)
        {
            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty rendererFeaturesProperty = serializedRenderer.FindProperty(RendererFeaturesPropertyPath);
            SerializedProperty mapProperty = serializedRenderer.FindProperty(RendererFeatureMapPropertyPath);
            if (rendererFeaturesProperty == null)
                return false;

            bool changed = NormalizeFeatureReferences(rendererFeaturesProperty, feature);
            changed |= RebuildRendererFeatureMap(rendererFeaturesProperty, mapProperty);
            if (!changed)
                return false;

            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool NormalizeFeatureReferences(
            SerializedProperty rendererFeaturesProperty,
            HectonSinglePassOceanFeature feature)
        {
            bool changed = false;
            bool keptTarget = false;
            for (int featureIndex = rendererFeaturesProperty.arraySize - 1; featureIndex >= 0; featureIndex--)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                HectonSinglePassOceanFeature oceanFeature = featureProperty.objectReferenceValue as HectonSinglePassOceanFeature;
                if (oceanFeature == null)
                    continue;

                if (!keptTarget && oceanFeature == feature)
                {
                    keptTarget = true;
                    continue;
                }

                RemoveArrayElement(rendererFeaturesProperty, featureIndex);
                changed = true;
            }

            if (keptTarget)
                return changed;

            int appendIndex = rendererFeaturesProperty.arraySize;
            rendererFeaturesProperty.arraySize++;
            rendererFeaturesProperty.GetArrayElementAtIndex(appendIndex).objectReferenceValue = feature;
            return true;
        }

        private static void RemoveArrayElement(SerializedProperty arrayProperty, int elementIndex)
        {
            int previousSize = arrayProperty.arraySize;
            arrayProperty.DeleteArrayElementAtIndex(elementIndex);
            if (arrayProperty.arraySize == previousSize)
                arrayProperty.DeleteArrayElementAtIndex(elementIndex);
        }

        private static bool RebuildRendererFeatureMap(
            SerializedProperty rendererFeaturesProperty,
            SerializedProperty mapProperty)
        {
            if (mapProperty == null)
                return false;

            bool changed = mapProperty.arraySize != rendererFeaturesProperty.arraySize;
            mapProperty.arraySize = rendererFeaturesProperty.arraySize;
            for (int featureIndex = 0; featureIndex < rendererFeaturesProperty.arraySize; featureIndex++)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                long localId = 0;
                if (featureProperty.objectReferenceValue is ScriptableRendererFeature feature)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out localId);

                SerializedProperty mapEntryProperty = mapProperty.GetArrayElementAtIndex(featureIndex);
                if (mapEntryProperty.longValue == localId)
                    continue;

                mapEntryProperty.longValue = localId;
                changed = true;
            }

            return changed;
        }

        private static bool VerifyFeatureReference(
            UniversalRendererData rendererData,
            HectonSinglePassOceanFeature feature)
        {
            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty rendererFeaturesProperty = serializedRenderer.FindProperty(RendererFeaturesPropertyPath);
            SerializedProperty mapProperty = serializedRenderer.FindProperty(RendererFeatureMapPropertyPath);
            if (rendererFeaturesProperty == null || mapProperty == null)
                return false;

            int featureIndex = FindFeatureReferenceIndex(rendererFeaturesProperty, feature);
            if (featureIndex < 0 ||
                CountFeatureReferences(rendererFeaturesProperty) != 1 ||
                mapProperty.arraySize <= featureIndex ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
            {
                return false;
            }

            return localId != 0 && mapProperty.GetArrayElementAtIndex(featureIndex).longValue == localId;
        }

        private static int CountFeatureReferences(SerializedProperty rendererFeaturesProperty)
        {
            int count = 0;
            for (int featureIndex = 0; featureIndex < rendererFeaturesProperty.arraySize; featureIndex++)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                if (featureProperty.objectReferenceValue is HectonSinglePassOceanFeature)
                    count++;
            }

            return count;
        }

        private static int FindFeatureReferenceIndex(
            SerializedProperty rendererFeaturesProperty,
            HectonSinglePassOceanFeature feature)
        {
            for (int featureIndex = 0; featureIndex < rendererFeaturesProperty.arraySize; featureIndex++)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                if (featureProperty.objectReferenceValue == feature)
                    return featureIndex;
            }

            return -1;
        }

        private static bool VerifyFeatureSettings(
            HectonSinglePassOceanFeature feature,
            Shader depthShader,
            ComputeShader wakeCompute)
        {
            if (!feature.isActive)
                return false;

            SerializedObject serializedFeature = new SerializedObject(feature);
            SerializedProperty depthShaderProperty = serializedFeature.FindProperty("settings.depthFoamShader");
            SerializedProperty wakeComputeProperty = serializedFeature.FindProperty("settings.wakeCompute");
            SerializedProperty injectionProperty = serializedFeature.FindProperty("settings.injectionPoint");
            return depthShaderProperty != null &&
                   depthShaderProperty.objectReferenceValue == depthShader &&
                   wakeComputeProperty != null &&
                   wakeComputeProperty.objectReferenceValue == wakeCompute &&
                   injectionProperty != null &&
                   injectionProperty.intValue == (int)RenderPassEvent.BeforeRenderingTransparents;
        }

        private static bool EnsureFeatureSettings(
            HectonSinglePassOceanFeature feature,
            Shader depthShader,
            ComputeShader wakeCompute)
        {
            SerializedObject serializedFeature = new SerializedObject(feature);
            bool changed = false;
            if (depthShader != null)
                changed |= EnsureObject(serializedFeature.FindProperty("settings.depthFoamShader"), depthShader);
            if (wakeCompute != null)
                changed |= EnsureObject(serializedFeature.FindProperty("settings.wakeCompute"), wakeCompute);

            changed |= EnsureInt(serializedFeature.FindProperty("settings.injectionPoint"), (int)RenderPassEvent.BeforeRenderingTransparents);
            if (!changed)
                return false;

            serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool EnsureObject(SerializedProperty property, Object expected)
        {
            if (property == null || property.objectReferenceValue == expected)
                return false;

            property.objectReferenceValue = expected;
            return true;
        }

        private static bool EnsureInt(SerializedProperty property, int expected)
        {
            if (property == null || property.intValue == expected)
                return false;

            property.intValue = expected;
            return true;
        }
    }

    internal sealed class SinglePassOceanRendererFeatureBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -262;

        public void OnPreprocessBuild(BuildReport report)
        {
            SinglePassOceanRendererFeatureInstaller.InstallRequiredFeatures(report.summary.platform);
            if (!SinglePassOceanRendererFeatureInstaller.VerifyRequiredFeatures(report.summary.platform, out string failure))
                throw new BuildFailedException(failure);
        }
    }
}
#endif
