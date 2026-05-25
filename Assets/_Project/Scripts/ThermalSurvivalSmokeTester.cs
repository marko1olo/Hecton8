// ============================================================================
// HECTON-8 - ThermalSurvivalSmokeTester.cs
// Dev-only deterministic checks for thermal shock, nutrition toxicity, air
// pockets, decompression sickness, brine inversion, and tractor snap gates.
// ============================================================================

using Hecton.Localization;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton8.Items;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Thermal Survival Smoke Tester")]
    public sealed class ThermalSurvivalSmokeTester : MonoBehaviour
    {
        private const int FailureThermalShock = 1 << 0;
        private const int FailureAirPocket = 1 << 1;
        private const int FailureNutritionalToxicity = 1 << 2;
        private const int FailureDecompressionVomit = 1 << 3;
        private const int FailureSuitPuncture = 1 << 4;
        private const int FailureBrineInversion = 1 << 5;
        private const int FailureCriticalStamina = 1 << 6;
        private const int FailureTractorSnap = 1 << 7;
        private const int FailureHypothermiaFrost = 1 << 8;
        private const int FailureHealthRegenToxicity = 1 << 9;

        [Header("Execution")]
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool verboseLogging;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private int _debugLastFailureMask;
        [SerializeField] private float _debugThermalDamagePerSecond;
        [SerializeField] private float _debugBrineSinkMultiplier;
#pragma warning restore CS0414

        public int LastFailureMask => _debugLastFailureMask;
        public bool LastPass => _debugLastPass;

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        [ContextMenu("Run Thermal Survival Smoke Pass")]
        public void RunSmokePass()
        {
            _debugLastFailureMask = RunDeterministicMathChecks(
                out _debugThermalDamagePerSecond,
                out _debugBrineSinkMultiplier);
            _debugLastPass = _debugLastFailureMask == 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogging)
            {
                if (_debugLastPass)
                    Hecton8.Core.H8Debug.Log("[ThermalSurvivalSmoke] PASS");
                else
                    Debug.LogError("[ThermalSurvivalSmoke] FAIL mask=" + _debugLastFailureMask);
            }
#endif
        }

        public static int RunDeterministicMathChecks()
        {
            return RunDeterministicMathChecks(out _, out _);
        }

#if UNITY_EDITOR
        public static void RunBatchModeSmokeTest()
        {
            int failureMask = RunDeterministicMathChecks(
                out float thermalDamagePerSecond,
                out float brineSinkMultiplier);
            if (failureMask != 0)
            {
                throw new System.InvalidOperationException(
                    "Thermal survival smoke failed. FailureMask=" + failureMask);
            }

            Hecton8.Core.H8Debug.Log(
                "[ThermalSurvivalSmoke] PASS thermalDps=" + thermalDamagePerSecond +
                " brineSinkMultiplier=" + brineSinkMultiplier);
        }

#endif

        public static int RunDeterministicMathChecks(
            out float thermalDamagePerSecond,
            out float brineSinkMultiplier)
        {
            int failureMask = 0;

            thermalDamagePerSecond = HectonSurvivalSystem.ResolveThermalShockDamagePerSecond(120f, 2f, 1f);
            float shieldedThermalDamage = HectonSurvivalSystem.ResolveThermalShockDamagePerSecond(120f, 2f, 0.35f);
            float freezingDamage = HectonSurvivalSystem.ResolveThermalShockDamagePerSecond(-12f, 2f, 1f);
            float thermalSample = HectonSurvivalSystem.ResolveExternalThermalShockTemperature(20f, 120f);
            if (thermalDamagePerSecond <= 0f ||
                shieldedThermalDamage >= thermalDamagePerSecond ||
                freezingDamage <= 0f ||
                math.abs(thermalSample - 120f) > 0.0001f)
            {
                failureMask |= FailureThermalShock;
            }

            global::HectonVoxelEngine.ClearAirPocketRegistry();
            bool flaggedAirPocket = global::HectonVoxelEngine.TryFlagAirPocketFromCeilingConcavity(
                Vector3.zero,
                Vector3.one,
                -0.8f,
                0.85f,
                0.5f,
                1f,
                out int airPocketHandle);
            bool sampledAirPocket = global::HectonVoxelEngine.TrySampleAirPocket(Vector3.zero, out float oxygenRefillFraction);
            bool rejectedAirPocket = global::HectonVoxelEngine.IsCeilingConcavityAirPocketCandidate(0.2f, 0.85f, 0.5f);
            global::HectonVoxelEngine.UnregisterAirPocket(airPocketHandle);
            if (!flaggedAirPocket || !sampledAirPocket || oxygenRefillFraction < 0.999f || rejectedAirPocket)
                failureMask |= FailureAirPocket;

            int membraneHash = LocHash.Compute("Data_MembraneTissue");
            bool membraneToxic = HectonSurvivalSystem.ShouldApplyNutritionalToxicityOnConsume(membraneHash);
            float toxicityDamage = HectonSurvivalSystem.ResolveNutritionalToxicityDamagePerSecond(1f, 2f);
            if (!membraneToxic || math.abs(toxicityDamage - 0.9f) > 0.0001f)
                failureMask |= FailureNutritionalToxicity;

            float vomitSeverity = HectonSurvivalSystem.ResolveDecompressionVomitSeverity01(155f);
            float safeVomitSeverity = HectonSurvivalSystem.ResolveDecompressionVomitSeverity01(150f);
            if (math.abs(vomitSeverity) > 0.0001f || safeVomitSeverity > 0.0001f)
                failureMask |= FailureDecompressionVomit;

            if (!HectonSurvivalSystem.ShouldForceSuitPunctureBleeding(31f, 100f) ||
                HectonSurvivalSystem.ShouldForceSuitPunctureBleeding(29f, 100f))
            {
                failureMask |= FailureSuitPuncture;
            }

            brineSinkMultiplier = HectonPlayerMotor.ResolveHeavyBrineSinkMultiplier(1250f, HectonPhysicsContract.WaterDensityKgPerCubicMeterConst);
            Vector3 invertedVelocity = HectonPlayerMotor.ResolveBuoyancyInversionVelocity(
                new Vector3(0f, -2f, 0f),
                true,
                false,
                brineSinkMultiplier);
            Vector3 thrusterVelocity = HectonPlayerMotor.ResolveBuoyancyInversionVelocity(
                new Vector3(0f, -2f, 0f),
                true,
                true,
                brineSinkMultiplier);
            if (brineSinkMultiplier >= 0f || invertedVelocity.y <= 0f || math.abs(thrusterVelocity.y + 2f) > 0.0001f)
                failureMask |= FailureBrineInversion;

            if (!HectonPlayerMovement.ShouldTriggerCriticalStaminaFailure(1.5f, 0.09f) ||
                HectonPlayerMovement.ShouldTriggerCriticalStaminaFailure(1.5f, 0.10f) ||
                HectonPlayerMovement.ShouldTriggerCriticalStaminaFailure(1.49f, 0.09f))
            {
                failureMask |= FailureCriticalStamina;
            }

            if (!PropulsionTool.ShouldBreakTractorTetherByTowAngle(Vector3.forward, Vector3.back) ||
                PropulsionTool.ShouldBreakTractorTetherByTowAngle(Vector3.forward, Vector3.forward))
            {
                failureMask |= FailureTractorSnap;
            }

            float frostMid = HectonSurvivalSystem.ResolveHypothermiaFrostIntensity01(31.5f);
            float frostSafe = HectonSurvivalSystem.ResolveHypothermiaFrostIntensity01(36f);
            float frostFull = HectonSurvivalSystem.ResolveHypothermiaFrostIntensity01(28f);
            if (math.abs(frostMid - 0.5f) > 0.0001f || frostSafe > 0.0001f || math.abs(frostFull - 1f) > 0.0001f)
                failureMask |= FailureHypothermiaFrost;

            float regenMultiplier = HectonPlayerHealth.ResolveNaturalHealthRegenerationMultiplier(1f);
            if (regenMultiplier >= 1f || math.abs(regenMultiplier - 0.35f) > 0.0001f)
                failureMask |= FailureHealthRegenToxicity;

            return failureMask;
        }
    }
}
