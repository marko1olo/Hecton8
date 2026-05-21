#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    internal static class PlatformPortabilityRouteRepairer
    {
        private const string WireAndroidQuestXrMenuPath = "HECTON-8/Platform/Wire Android Quest XR Routes";

        [MenuItem(WireAndroidQuestXrMenuPath, priority = 419)]
        private static void WireAndroidQuestXrRoutesFromMenu()
        {
            WireAndroidQuestXrRoutesForCi();
        }

        public static void WireAndroidQuestXrRoutesForCi()
        {
            QuestVulkanRenderPipelineConfigurator.ConfigureQuestAssetsForCi();
            QuestVulkanRenderPipelineConfigurator.WireQuestAndroidQualityRouteForCi();
            XrPlatformReadinessValidator.WireAndroidOpenXrProviderRouteForCi();
            XrPlatformReadinessValidator.ValidateAndroidXrReadinessForCi();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[HFI_AUDIT] Android Quest XR route repair executed: Quest URP quality route + Android OpenXR provider route.");
        }
    }
}
#endif
