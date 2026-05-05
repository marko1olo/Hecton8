using Hecton8.Dev;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class FaunaRuntimeSmokeTesterRunner
    {
        public static void RunHeadlessAupDriftAssertion()
        {
            bool passed = FaunaRuntimeSmokeTester.RunHeadlessAupDriftAssertion(out double distanceErrorMeters);
            if (passed)
            {
                Debug.Log("FaunaRuntimeSmokeTester headless AUP drift assertion passed. distanceErrorMeters=" + distanceErrorMeters.ToString("R"));
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError("FaunaRuntimeSmokeTester headless AUP drift assertion failed. distanceErrorMeters=" + distanceErrorMeters.ToString("R"));
            EditorApplication.Exit(1);
        }
    }
}
