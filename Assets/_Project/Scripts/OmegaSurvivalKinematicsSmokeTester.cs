// ============================================================================
// HECTON-8 - OmegaSurvivalKinematicsSmokeTester.cs
// Batch-safe deterministic audit for survival/kinematics hardening.
// ============================================================================

using Hecton8.Gameplay;
using Hecton8.Core.Contracts;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text;
#endif

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Omega Survival Kinematics Smoke Tester")]
    public sealed class OmegaSurvivalKinematicsSmokeTester : MonoBehaviour
    {
        private const int FailureThermalShock = 1 << 0;
        private const int FailureNitrogenMath = 1 << 1;
        private const int FailureHealthMath = 1 << 2;
        private const int FailureAirPocket = 1 << 3;
        private const int FailureBrineInversion = 1 << 4;
        private const int FailureTractorSnap = 1 << 5;
        private const int FailureCriticalStamina = 1 << 6;

        [Header("Execution")]
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool verboseLogging;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private int _debugLastFailureMask;
        [SerializeField] private int _debugLastCheckCount;
        [SerializeField] private float _debugThermalDamagePerSecond;
        [SerializeField] private float _debugNitrogenDelta;
        [SerializeField] private float _debugBrineSinkMultiplier;
#pragma warning restore CS0414

        public int LastFailureMask => _debugLastFailureMask;
        public bool LastPass => _debugLastPass;

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        [ContextMenu("Run Omega Survival Kinematics Smoke Pass")]
        public void RunSmokePass()
        {
            _debugLastFailureMask = RunDeterministicMathChecks(
                out _debugLastCheckCount,
                out _debugThermalDamagePerSecond,
                out _debugNitrogenDelta,
                out _debugBrineSinkMultiplier);
            _debugLastPass = _debugLastFailureMask == 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogging)
            {
                if (_debugLastPass)
                    Debug.Log("[OmegaSurvivalKinematicsSmoke] PASS checks=" + _debugLastCheckCount);
                else
                    Debug.LogError("[OmegaSurvivalKinematicsSmoke] FAIL mask=" + _debugLastFailureMask);
            }
#endif
        }

        public static int RunDeterministicMathChecks()
        {
            return RunDeterministicMathChecks(out _, out _, out _, out _);
        }

#if UNITY_EDITOR
        public static void RunBatchModeSmokeTest()
        {
            int failureMask = RunDeterministicMathChecks(
                out int checkCount,
                out float thermalDamagePerSecond,
                out float nitrogenBuildUpDelta,
                out float brineSinkMultiplier);
            string json = BuildBatchJson(
                failureMask,
                checkCount,
                thermalDamagePerSecond,
                nitrogenBuildUpDelta,
                brineSinkMultiplier);
            WriteBatchJson(json);
            Debug.Log("[OmegaSurvivalKinematicsSmoke] " + json);

            if (failureMask != 0)
            {
                throw new System.InvalidOperationException(
                    "Omega survival kinematics smoke failed. FailureMask=" + failureMask);
            }
        }

        private static string BuildBatchJson(
            int failureMask,
            int checkCount,
            float thermalDamagePerSecond,
            float nitrogenBuildUpDelta,
            float brineSinkMultiplier)
        {
            // COLD ALLOC: StringBuilder[384] - editor batch smoke JSON report - owner: OmegaSurvivalKinematicsSmokeTester
            var builder = new StringBuilder(384);
            builder.Append('{')
                .Append("\"status\":\"").Append(failureMask == 0 ? "PASS" : "FAIL").Append("\",")
                .Append("\"checks\":").Append(checkCount).Append(',')
                .Append("\"failureMask\":").Append(failureMask).Append(',')
                .Append("\"thermalDps\":").Append(thermalDamagePerSecond.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append("\"nitrogenDelta\":").Append(nitrogenBuildUpDelta.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append("\"brineSinkMultiplier\":").Append(brineSinkMultiplier.ToString("R", CultureInfo.InvariantCulture))
                .Append('}');
            return builder.ToString();
        }

        private static void WriteBatchJson(string json)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(projectRoot, "CodexArtifacts");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "omega-survival-kinematics-smoke.json"), json);
        }
#endif

        public static int RunDeterministicMathChecks(
            out int checkCount,
            out float thermalDamagePerSecond,
            out float nitrogenBuildUpDelta,
            out float brineSinkMultiplier)
        {
            int failureMask = 0;
            checkCount = 0;

            thermalDamagePerSecond = SomaticSurvivalMath.ResolveThermalShockDamagePerSecond(120f, 2f, 1f);
            float shieldedThermalDamage = SomaticSurvivalMath.ResolveThermalShockDamagePerSecond(120f, 2f, 0.35f);
            float freezingDamage = SomaticSurvivalMath.ResolveThermalShockDamagePerSecond(-12f, 2f, 1f);
            float thermalSample = SomaticSurvivalMath.ResolveExternalThermalShockTemperature(20f, 120f);
            float freezingThermalSample = SomaticSurvivalMath.ResolveExternalThermalShockTemperature(20f, -12f);
            float fallbackSample = SomaticSurvivalMath.ResolveExternalThermalShockTemperature(20f, float.NegativeInfinity);
            if (!CheckNear(thermalDamagePerSecond, 13.5f, ref checkCount) ||
                !CheckNear(shieldedThermalDamage, 4.725f, ref checkCount) ||
                !CheckTrue(freezingDamage > 0f, ref checkCount) ||
                !CheckNear(thermalSample, 120f, ref checkCount) ||
                !CheckNear(freezingThermalSample, -12f, ref checkCount) ||
                !CheckNear(fallbackSample, 20f, ref checkCount))
            {
                failureMask |= FailureThermalShock;
            }

            nitrogenBuildUpDelta = SomaticSurvivalMath.ResolveNitrogenBuildUpDelta(7f, 800f, 0.5f);
            float nitrogenSafeDelta = SomaticSurvivalMath.ResolveNitrogenBuildUpDelta(5f, 800f, 1f);
            float narcosisMid = SomaticSurvivalMath.ResolveNitrogenNarcosis01(125f);
            float staminaPenalty = SomaticSurvivalMath.ResolveNitrogenStaminaMultiplier(101f);
            float vomitSeverity = SomaticSurvivalMath.ResolveDecompressionVomitSeverity01(155f);
            if (!CheckNear(nitrogenBuildUpDelta, 12f, ref checkCount) ||
                !CheckNear(nitrogenSafeDelta, 0f, ref checkCount) ||
                !CheckNear(narcosisMid, 0.5f, ref checkCount) ||
                !CheckNear(staminaPenalty, 0.8f, ref checkCount) ||
                !CheckNear(vomitSeverity, 0.5f, ref checkCount))
            {
                failureMask |= FailureNitrogenMath;
            }

            float fatigueScale = SomaticSurvivalMath.ResolveRadiationFatigueScale(100f);
            float regenMultiplier = SomaticSurvivalMath.ResolveNaturalHealthRegenerationMultiplier(1f);
            float toxicityDamage = SomaticSurvivalMath.ResolveNutritionalToxicityDamagePerSecond(1f, 2f);
            float frostMid = SomaticSurvivalMath.ResolveHypothermiaFrostIntensity01(31.5f);
            bool punctureMajor = SomaticSurvivalMath.ShouldForceSuitPunctureBleeding(31f, 100f);
            bool punctureMinor = SomaticSurvivalMath.ShouldForceSuitPunctureBleeding(29f, 100f);
            bool survivalGraceLethal = HectonPlayerHealth.ShouldActivateSurvivalGrace(50f, 100f, 60f, false, 0f);
            bool survivalGraceNonLethal = HectonPlayerHealth.ShouldActivateSurvivalGrace(50f, 100f, 10f, false, 0f);
            bool survivalGraceTooLow = HectonPlayerHealth.ShouldActivateSurvivalGrace(10f, 100f, 20f, false, 0f);
            if (!CheckNear(fatigueScale, 0.65f, ref checkCount) ||
                !CheckNear(regenMultiplier, 0.35f, ref checkCount) ||
                !CheckNear(toxicityDamage, 0.9f, ref checkCount) ||
                !CheckNear(frostMid, 0.5f, ref checkCount) ||
                !CheckTrue(punctureMajor, ref checkCount) ||
                !CheckTrue(!punctureMinor, ref checkCount) ||
                !CheckTrue(survivalGraceLethal, ref checkCount) ||
                !CheckTrue(!survivalGraceNonLethal, ref checkCount) ||
                !CheckTrue(!survivalGraceTooLow, ref checkCount))
            {
                failureMask |= FailureHealthMath;
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
            if (!CheckTrue(flaggedAirPocket, ref checkCount) ||
                !CheckTrue(sampledAirPocket, ref checkCount) ||
                !CheckTrue(oxygenRefillFraction >= 0.999f, ref checkCount) ||
                !CheckTrue(!rejectedAirPocket, ref checkCount))
            {
                failureMask |= FailureAirPocket;
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
            if (!CheckTrue(brineSinkMultiplier < 0f, ref checkCount) ||
                !CheckTrue(invertedVelocity.y > 0f, ref checkCount) ||
                !CheckNear(thrusterVelocity.y, -2f, ref checkCount))
            {
                failureMask |= FailureBrineInversion;
            }

            if (!CheckTrue(PropulsionTool.ShouldBreakTractorTetherByTowAngle(Vector3.forward, Vector3.back), ref checkCount) ||
                !CheckTrue(!PropulsionTool.ShouldBreakTractorTetherByTowAngle(Vector3.forward, Vector3.forward), ref checkCount))
            {
                failureMask |= FailureTractorSnap;
            }

            if (!CheckTrue(HectonPlayerMovement.ShouldTriggerCriticalStaminaFailure(1.5f, 0.09f), ref checkCount) ||
                !CheckTrue(!HectonPlayerMovement.ShouldTriggerCriticalStaminaFailure(1.5f, 0.10f), ref checkCount) ||
                !CheckTrue(!HectonPlayerMovement.ShouldTriggerCriticalStaminaFailure(1.49f, 0.09f), ref checkCount))
            {
                failureMask |= FailureCriticalStamina;
            }

            return failureMask;
        }

        private static bool CheckNear(float actual, float expected, ref int checkCount)
        {
            checkCount++;
            return math.abs(actual - expected) <= 0.0001f;
        }

        private static bool CheckTrue(bool condition, ref int checkCount)
        {
            checkCount++;
            return condition;
        }
    }
}
