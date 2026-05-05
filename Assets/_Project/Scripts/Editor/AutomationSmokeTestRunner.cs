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
                $"[AutomationSmokeTester] ExtractorStorageRouteSmoke pass={passed} routed={routedNode} deposited={depositedUnits}");

            if (!passed)
                EditorApplication.Exit(1);
        }
    }
}
