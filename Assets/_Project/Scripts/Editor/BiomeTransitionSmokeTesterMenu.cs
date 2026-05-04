using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor and batchmode entrypoint for biome transition smoke validation.
    /// </summary>
    public static class BiomeTransitionSmokeTesterMenu
    {
        /// <summary>
        /// Runs the headless biome transition smoke test and exits Unity with failure code in batchmode.
        /// </summary>
        [MenuItem("Hecton8/World/Run Biome Transition Smoke Test")]
        public static void RunBiomeTransitionSmokeTest()
        {
            bool passed = BiomeTransitionSmokeTester.RunHeadlessSmokeTest(
                out float fogDensity,
                out float absorption,
                out uint packedInfluence);

            if (passed)
            {
                Debug.Log(
                    $"[BiomeTransitionSmokeTester] PASS density={fogDensity:F4} absorption={absorption:F4} packed=0x{packedInfluence:X8}");
            }
            else
            {
                Debug.LogError(
                    $"[BiomeTransitionSmokeTester] FAIL density={fogDensity:F4} absorption={absorption:F4} packed=0x{packedInfluence:X8}");
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
