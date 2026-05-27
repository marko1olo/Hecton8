#if UNITY_EDITOR
using System.IO;
using Hecton8.Rendering.WaterOptics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Rendering.WaterOptics.Editor
{
    internal static class WaterOpticsRendererFeatureInstaller
    {
        private const string MenuPath = "Hecton8/Rendering/Water Optics/Install Telemetry Render Feature";
        private const string RendererFeaturesPropertyPath = "m_RendererFeatures";
        private const string RendererFeatureMapPropertyPath = "m_RendererFeatureMap";
        private const int RendererAssetPathCount = 4;
        private const string PcRendererAssetPath = "Assets/_Project/Data/PC_Renderer.asset";
        private const string PcHighRendererAssetPath = "Assets/_Project/Data/PC_High_Renderer.asset";
        private const string MobileRendererAssetPath = "Assets/_Project/Data/Mobile_Renderer.asset";
        private const string QuestVrRendererAssetPath = "Assets/_Project/Data/Quest_VR_Renderer.asset";
        private const string RuntimeScriptGuid = "26500000000000000000000000000004";
        private const string ProjectAssetRoot = "Assets/_Project";

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

            bool changed = false;
            for (int rendererIndex = 0; rendererIndex < RendererAssetPathCount; rendererIndex++)
            {
                string rendererAssetPath = GetRendererAssetPath(rendererIndex);
                if (!ShouldValidateRendererAsset(buildTarget, rendererAssetPath))
                    continue;

                changed |= EnsureRendererFeature(rendererAssetPath);
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
            for (int rendererIndex = 0; rendererIndex < RendererAssetPathCount; rendererIndex++)
            {
                string rendererAssetPath = GetRendererAssetPath(rendererIndex);
                if (!ShouldValidateRendererAsset(buildTarget, rendererAssetPath))
                    continue;

                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);
                if (rendererData == null)
                {
                    failure = string.Concat("[13KRA] Missing renderer asset: ", rendererAssetPath);
                    return false;
                }

                HectonWaterOpticsTelemetryFeature feature = FindFeatureSubAsset(rendererAssetPath);
                if (feature == null)
                {
                    failure = string.Concat("[13KRA] Renderer asset has no water optics telemetry feature: ", rendererAssetPath);
                    return false;
                }

                if (!VerifyFeatureReference(rendererData, feature))
                {
                    failure = string.Concat("[13KRA] Renderer feature reference/map is not serialized: ", rendererAssetPath);
                    return false;
                }

                if (!VerifyFeatureSettings(feature))
                {
                    failure = string.Concat("[13KRA] Telemetry feature settings are not development-safe: ", rendererAssetPath);
                    return false;
                }
            }

            return VerifyRuntimeOwnerAuthored(out failure);
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

        private static bool ShouldValidateRendererAsset(BuildTarget buildTarget, string rendererAssetPath)
        {
            if (buildTarget == BuildTarget.NoTarget)
                return true;

            if (buildTarget == BuildTarget.Android)
                return rendererAssetPath == MobileRendererAssetPath ||
                       rendererAssetPath == QuestVrRendererAssetPath;

            if (buildTarget == BuildTarget.iOS)
                return rendererAssetPath == MobileRendererAssetPath;

            if (IsStandaloneTarget(buildTarget))
                return rendererAssetPath == PcRendererAssetPath ||
                       rendererAssetPath == PcHighRendererAssetPath;

            return true;
        }

        private static bool IsStandaloneTarget(BuildTarget buildTarget)
        {
            return buildTarget == BuildTarget.StandaloneWindows ||
                   buildTarget == BuildTarget.StandaloneWindows64 ||
                   buildTarget == BuildTarget.StandaloneOSX ||
                   buildTarget == BuildTarget.StandaloneLinux64;
        }

        private static bool EnsureRendererFeature(string rendererAssetPath)
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);
            if (rendererData == null)
                return false;

            HectonWaterOpticsTelemetryFeature feature = FindFeatureSubAsset(rendererAssetPath);
            bool changed = false;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<HectonWaterOpticsTelemetryFeature>();
                feature.name = nameof(HectonWaterOpticsTelemetryFeature);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                changed = true;
            }

            changed |= EnsureFeatureReference(rendererData, feature);
            changed |= EnsureFeatureSettings(feature);
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

        private static HectonWaterOpticsTelemetryFeature FindFeatureSubAsset(string rendererAssetPath)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(rendererAssetPath);
            for (int subAssetIndex = 0; subAssetIndex < subAssets.Length; subAssetIndex++)
            {
                if (subAssets[subAssetIndex] is HectonWaterOpticsTelemetryFeature feature)
                    return feature;
            }

            return null;
        }

        private static bool EnsureFeatureReference(
            UniversalRendererData rendererData,
            HectonWaterOpticsTelemetryFeature feature)
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
            HectonWaterOpticsTelemetryFeature feature)
        {
            bool changed = false;
            bool keptTarget = false;
            for (int featureIndex = rendererFeaturesProperty.arraySize - 1; featureIndex >= 0; featureIndex--)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                HectonWaterOpticsTelemetryFeature opticsFeature = featureProperty.objectReferenceValue as HectonWaterOpticsTelemetryFeature;
                if (opticsFeature == null)
                    continue;

                if (!keptTarget && opticsFeature == feature)
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
            HectonWaterOpticsTelemetryFeature feature)
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
                if (featureProperty.objectReferenceValue is HectonWaterOpticsTelemetryFeature)
                    count++;
            }

            return count;
        }

        private static int FindFeatureReferenceIndex(
            SerializedProperty rendererFeaturesProperty,
            HectonWaterOpticsTelemetryFeature feature)
        {
            for (int featureIndex = 0; featureIndex < rendererFeaturesProperty.arraySize; featureIndex++)
            {
                SerializedProperty featureProperty = rendererFeaturesProperty.GetArrayElementAtIndex(featureIndex);
                if (featureProperty.objectReferenceValue == feature)
                    return featureIndex;
            }

            return -1;
        }

        private static bool VerifyFeatureSettings(HectonWaterOpticsTelemetryFeature feature)
        {
            if (!feature.isActive)
                return false;

            SerializedObject serializedFeature = new SerializedObject(feature);
            SerializedProperty injectionProperty = serializedFeature.FindProperty("settings.injectionPoint");
            SerializedProperty markerProperty = serializedFeature.FindProperty("settings.enableCommandBufferMarker");
            return injectionProperty != null &&
                   injectionProperty.intValue == (int)RenderPassEvent.AfterRenderingOpaques &&
                   markerProperty != null &&
                   !markerProperty.boolValue;
        }

        private static bool VerifyRuntimeOwnerAuthored(out string failure)
        {
            failure = null;
            if (HasRuntimeOwnerInAuthoredAssets("t:Scene") || HasRuntimeOwnerInAuthoredAssets("t:Prefab"))
                return true;

            failure = string.Concat(
                "[13KRA] WaterOpticsRuntime owner is not authored in _Project scenes/prefabs. Use menu '",
                WaterOpticsRuntimeOwnerInstaller.MenuPath,
                "' or add it to an explicit bootstrap-owned prefab; hidden runtime self-spawn is forbidden.");
            return false;
        }

        private static bool HasRuntimeOwnerInAuthoredAssets(string filter)
        {
            string needle = string.Concat("guid: ", RuntimeScriptGuid);
            string[] assetGuids = AssetDatabase.FindAssets(filter, new[] { ProjectAssetRoot });
            for (int assetIndex = 0; assetIndex < assetGuids.Length; assetIndex++)
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGuids[assetIndex]);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                if (FileContains(path, needle))
                    return true;
            }

            return false;
        }

        private static bool FileContains(string path, string needle)
        {
            foreach (string line in File.ReadLines(path))
            {
                if (line.IndexOf(needle, System.StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static bool EnsureFeatureSettings(HectonWaterOpticsTelemetryFeature feature)
        {
            SerializedObject serializedFeature = new SerializedObject(feature);
            bool changed = false;
            changed |= EnsureInt(serializedFeature.FindProperty("settings.injectionPoint"), (int)RenderPassEvent.AfterRenderingOpaques);
            changed |= EnsureBool(serializedFeature.FindProperty("settings.enableCommandBufferMarker"), false);
            if (!changed)
                return false;

            serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool EnsureInt(SerializedProperty property, int expected)
        {
            if (property == null || property.intValue == expected)
                return false;

            property.intValue = expected;
            return true;
        }

        private static bool EnsureBool(SerializedProperty property, bool expected)
        {
            if (property == null || property.boolValue == expected)
                return false;

            property.boolValue = expected;
            return true;
        }
    }

    internal sealed class WaterOpticsRendererFeatureBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -265;

        public void OnPreprocessBuild(BuildReport report)
        {
            WaterOpticsRendererFeatureInstaller.InstallRequiredFeatures(report.summary.platform);
            if (!WaterOpticsRendererFeatureInstaller.VerifyRequiredFeatures(report.summary.platform, out string failure))
                throw new BuildFailedException(failure);
        }
    }
}
#endif
