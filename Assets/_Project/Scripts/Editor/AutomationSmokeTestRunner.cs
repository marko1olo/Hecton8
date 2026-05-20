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
            bool passed = CraftingRuntimeSmokeTester.RunFabricationVaultSmoke(
                1f,
                1f,
                out float firstMockProgress,
                out float lastMockProgress,
                out float averageMockProgress);
            int firstMockProgressMilli = Mathf.RoundToInt(firstMockProgress * 1000f);
            int lastMockProgressMilli = Mathf.RoundToInt(lastMockProgress * 1000f);
            int averageMockProgressMilli = Mathf.RoundToInt(averageMockProgress * 1000f);

            Debug.Log(
                "{\"CraftingRuntimeSmokeTester\":{\"pass\":" +
                (passed ? "true" : "false") +
                ",\"firstMockProgressMilli\":" +
                firstMockProgressMilli +
                ",\"lastMockProgressMilli\":" +
                lastMockProgressMilli +
                ",\"averageMockProgressMilli\":" +
                averageMockProgressMilli +
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
