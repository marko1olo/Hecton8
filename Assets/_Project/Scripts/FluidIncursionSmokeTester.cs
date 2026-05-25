// ============================================================================
// HECTON-8 - FluidIncursionSmokeTester.cs
// Dev-only smoke for pressure leak volume versus powered pump drainage.
// ============================================================================

using System.Globalization;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Fluid Incursion Smoke Tester")]
    public sealed class FluidIncursionSmokeTester : MonoBehaviour
    {
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool verboseLogging;
        [SerializeField, Min(0f)] private float depthMeters = 900f;
        [SerializeField, Min(0.0001f)] private float holeAreaSquareMeters = 0.08f;
        [SerializeField, Min(0f)] private float pressureFlowCoefficient = 0.001f;
        [SerializeField, Min(0f)] private float pumpRateM3PerSecond = 2.4f;
        [SerializeField, Min(1)] private int tickCount = 20;
        [SerializeField, Min(0.01f)] private float tickDeltaSeconds = 0.5f;

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runOnStart)
                RunSmokePass();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            depthMeters = math.max(0f, depthMeters);
            holeAreaSquareMeters = math.max(0.0001f, holeAreaSquareMeters);
            pressureFlowCoefficient = math.max(0f, pressureFlowCoefficient);
            pumpRateM3PerSecond = math.max(0f, pumpRateM3PerSecond);
            tickCount = math.max(1, tickCount);
            tickDeltaSeconds = math.max(0.01f, tickDeltaSeconds);
        }
#endif

        [ContextMenu("Run Fluid Incursion Smoke Pass")]
        public void RunSmokePass()
        {
            bool passed = EvaluateSmokePass(out float leakOnlyVolume, out float pumpedVolume);
            if (!passed)
            {
                Hecton8.Core.H8Debug.LogError(
                    "[FluidIncursionSmoke] FAIL leakOnly=" +
                    leakOnlyVolume.ToString("F4", CultureInfo.InvariantCulture) +
                    "m3 pumped=" +
                    pumpedVolume.ToString("F4", CultureInfo.InvariantCulture) +
                    "m3",
                    this);
                return;
            }

            if (verboseLogging)
            {
                Hecton8.Core.H8Debug.Log(
                    "[FluidIncursionSmoke] PASS leakOnly=" +
                    leakOnlyVolume.ToString("F4", CultureInfo.InvariantCulture) +
                    "m3 pumped=" +
                    pumpedVolume.ToString("F4", CultureInfo.InvariantCulture) +
                    "m3",
                    this);
            }
        }

        public bool EvaluateSmokePass(out float leakOnlyVolume, out float pumpedVolume)
        {
            leakOnlyVolume = 0f;
            pumpedVolume = 0f;
            for (int i = 0; i < tickCount; i++)
            {
                float leakDelta = BaseModule.CalculateIngressVolumeDeltaM3(
                    depthMeters,
                    holeAreaSquareMeters,
                    tickDeltaSeconds,
                    pressureFlowCoefficient);
                float pumpDelta = WaterPumpModule.CalculatePumpDrainVolumeM3(
                    pumpRateM3PerSecond,
                    1f,
                    tickDeltaSeconds);

                leakOnlyVolume += leakDelta;
                pumpedVolume = math.max(0f, pumpedVolume + leakDelta - pumpDelta);
            }

            bool leakProducesWater = leakOnlyVolume > 0.0001f;
            bool pumpBeatsLeak = pumpedVolume + 0.0001f < leakOnlyVolume;
            return leakProducesWater && pumpBeatsLeak;
        }
    }
}
