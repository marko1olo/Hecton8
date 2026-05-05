using Hecton8.Debugging;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class AutomationSmokeTestRunner
    {
        public static void RunBatchExtractorStorageRouteSmokePass()
        {
            bool passed = AutomationSmokeTester.RunExtractorFillsStorageSmoke(out int routedNode, out int depositedUnits);
            Debug.Log(
                "[AutomationSmokeTester] ExtractorStorageRouteSmoke pass=" +
                passed +
                " routed=" +
                routedNode +
                " deposited=" +
                depositedUnits);

            if (!passed)
                EditorApplication.Exit(1);
        }

        public static void RunBatchCraftingRuntimeSmokePass()
        {
            bool passed = CraftingRuntimeSmokeTester.RunAsyncQueueSmoke(
                1f,
                1f,
                out float pausedProgress,
                out float firstPoweredProgress,
                out float completedProgress);
            int pausedProgressMilli = Mathf.RoundToInt(pausedProgress * 1000f);
            int firstPoweredProgressMilli = Mathf.RoundToInt(firstPoweredProgress * 1000f);
            int completedProgressMilli = Mathf.RoundToInt(completedProgress * 1000f);

            Debug.Log(
                "{\"CraftingRuntimeSmokeTester\":{\"pass\":" +
                (passed ? "true" : "false") +
                ",\"pausedProgressMilli\":" +
                pausedProgressMilli +
                ",\"firstPoweredProgressMilli\":" +
                firstPoweredProgressMilli +
                ",\"completedProgressMilli\":" +
                completedProgressMilli +
                "}}");

            if (!passed)
                EditorApplication.Exit(1);
        }

        public static void RunBatchOmegaAutomationSmokePass()
        {
            AutomationOmegaSmokeResult result = AutomationOmegaSmokeTester.RunLogisticsRouteStressSmoke();
            Debug.Log(
                "{\"AutomationOmegaSmokeTester\":{\"pass\":" +
                (result.Passed ? "true" : "false") +
                ",\"nodes\":" +
                result.NodeCount +
                ",\"edges\":" +
                result.EdgeCount +
                ",\"routedNode\":" +
                result.RoutedNode +
                ",\"expectedStorageNode\":" +
                result.ExpectedStorageNode +
                ",\"noStorageRouteNode\":" +
                result.NoStorageRouteNode +
                ",\"invalidStartRouteNode\":" +
                result.InvalidStartRouteNode +
                "}}");

            if (!result.Passed)
                EditorApplication.Exit(1);
        }
    }
}
