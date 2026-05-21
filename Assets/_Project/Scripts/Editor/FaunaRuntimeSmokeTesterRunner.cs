using Hecton8.Dev;
using System.Globalization;
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

        public static void RunOmegaHeadlessSmoke()
        {
            bool passed = FaunaRuntimeSmokeTester.RunOmegaHeadlessSmoke(out FaunaRuntimeSmokeTester.OmegaSmokeResult result);
            string json =
                "FAUNA_OMEGA_SMOKE_RESULT {\"passed\":" + Bool(result.Passed) +
                ",\"aupDriftPassed\":" + Bool(result.AupDriftPassed) +
                ",\"aupStressPassed\":" + Bool(result.AupStressPassed) +
                ",\"parasiteAttachPassed\":" + Bool(result.ParasiteAttachPassed) +
                ",\"eggPersistencePassed\":" + Bool(result.EggPersistencePassed) +
                ",\"nativeSentinelBalanced\":" + Bool(result.NativeSentinelBalanced) +
                ",\"aupDriftDistanceErrorMeters\":" + D(result.AupDriftDistanceErrorMeters) +
                ",\"aupStressMaxDistanceErrorMeters\":" + D(result.AupStressMaxDistanceErrorMeters) +
                ",\"parasiteHostHealth\":" + F(result.ParasiteHostHealth) +
                ",\"parasiteHunger01\":" + F(result.ParasiteHunger01) +
                ",\"parasiteMaxDistanceErrorMeters\":" + D(result.ParasiteMaxDistanceErrorMeters) +
                ",\"eggHatchTimeSeconds\":" + F(result.EggHatchTimeSeconds) +
                ",\"nativeSentinelDelta\":" + result.NativeSentinelDelta.ToString(CultureInfo.InvariantCulture) +
                "}";

            if (passed)
            {
                Debug.Log(json);
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError(json);
            EditorApplication.Exit(1);
        }

        private static string Bool(byte value)
        {
            return value != 0 ? "true" : "false";
        }

        private static string D(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string F(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
