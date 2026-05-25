// ============================================================================
// HECTON-8 - SurvivalKinematicsSmokeTester.cs
// Dev-only smoke coverage for survival, hydrodynamic, haptic-adjacent, and KCC
// math gates used by the somatic dread protocol.
// ============================================================================

using Hecton8.Atmosphere;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Tools;
using Hecton8.UI;
using NASAPunk.Visor;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    /// <summary>
    /// Runs deterministic, allocation-free math checks for the survival and kinematics protocol.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Survival Kinematics Smoke Tester")]
    public sealed class SurvivalKinematicsSmokeTester : MonoBehaviour
    {
        private const int FailureRadiationFatigue = 1 << 0;
        private const int FailureSurvivalGrace = 1 << 1;
        private const int FailureKccProjection = 1 << 2;
        private const int FailureReducedMass = 1 << 3;
        private const int FailureDepthDrag = 1 << 4;
        private const int FailureEncumbrance = 1 << 5;
        private const int FailureThermalShock = 1 << 6;
        private const int FailureCriticalHaptics = 1 << 7;
        private const int FailureStressOxygen = 1 << 8;
        private const int FailurePressureCrush = 1 << 9;
        private const int FailureBrownoutSiphon = 1 << 10;
        private const int FailureNitrogenNarcosis = 1 << 11;

        [Header("Execution")]
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool verboseLogging;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private int _debugLastFailureMask;
        [SerializeField] private Vector3 _debugProjectedVelocity;
        [SerializeField] private float _debugReducedMass;
        [SerializeField] private float _debugDepthDragCoefficient;
#pragma warning restore CS0414

        /// <summary>Last deterministic smoke result bitmask. Zero means pass.</summary>
        public int LastFailureMask => _debugLastFailureMask;

        /// <summary>True when the last deterministic smoke pass found no math regressions.</summary>
        public bool LastPass => _debugLastPass;

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        [ContextMenu("Run Survival Kinematics Smoke Pass")]
        public void RunSmokePass()
        {
            int failureMask = RunDeterministicMathChecks(out Vector3 projectedVelocity, out float reducedMass, out float depthDragCoefficient);
            _debugProjectedVelocity = projectedVelocity;
            _debugReducedMass = reducedMass;
            _debugDepthDragCoefficient = depthDragCoefficient;
            _debugLastFailureMask = failureMask;
            _debugLastPass = failureMask == 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogging)
            {
                if (_debugLastPass)
                    Hecton8.Core.H8Debug.Log("[SurvivalKinematicsSmoke] PASS", this);
                else
                    Hecton8.Core.H8Debug.LogError("[SurvivalKinematicsSmoke] FAIL", this);
            }
#endif
        }

        internal static int RunDeterministicMathChecks(
            out Vector3 projectedVelocity,
            out float reducedMass,
            out float depthDragCoefficient)
        {
            int failureMask = 0;

            float fatigueScale = HectonPlayerHealth.ResolveRadiationFatigueScale(100f);
            if (math.abs(fatigueScale - 0.65f) > 0.0001f)
                failureMask |= FailureRadiationFatigue;

            bool survivalGrace = HectonPlayerHealth.ShouldActivateSurvivalGrace(25f, 100f, 30f, false, 0f);
            bool lowHealthGrace = HectonPlayerHealth.ShouldActivateSurvivalGrace(10f, 100f, 30f, false, 0f);
            if (!survivalGrace || lowHealthGrace)
                failureMask |= FailureSurvivalGrace;

            projectedVelocity = HectonPlayerMotor.ProjectVelocityOnCollisionPlane(new Vector3(3f, 0f, 4f), Vector3.forward);
            if (math.abs(projectedVelocity.z) > 0.0001f || math.abs(projectedVelocity.x - 3f) > 0.0001f)
                failureMask |= FailureKccProjection;

            reducedMass = HectonContactJob.ResolveReducedMass(20f, 30f, false);
            if (math.abs(reducedMass - 12f) > 0.0001f)
                failureMask |= FailureReducedMass;

            depthDragCoefficient = PlayerSwimMotor.ResolveDepthDragAdd(900f, 20f, 150f, 0.8f);
            if (math.abs(depthDragCoefficient - 0.8f) > 0.0001f)
                failureMask |= FailureDepthDrag;

            bool criticalLoad = HectonPlayerMovement.IsCriticalInventoryLoad(150f, 100f);
            Vector3 clampedSwimForce = HectonPlayerMovement.ResolveCriticalEncumbranceSwimForce(new Vector3(1f, 5f, 0f), criticalLoad);
            if (!criticalLoad || math.abs(clampedSwimForce.y) > 0.0001f)
                failureMask |= FailureEncumbrance;

            byte criticalHapticMask = SuitHUDV4CanvasOverlay.ResolveCriticalHapticMask(0.14f, 1f, 1f);
            if ((criticalHapticMask & 1) == 0 || ToolHapticsRuntime.PriorityCritical != 3)
                failureMask |= FailureCriticalHaptics;

            float stressOxygenMultiplier = HectonSurvivalSystem.ResolveHeartrateOxygenMultiplier(1f);
            if (math.abs(stressOxygenMultiplier - 1.6487213f) > 0.0001f ||
                math.abs(HectonSurvivalSystem.ResolveHeartrateOxygenMultiplier(0f) - 1f) > 0.0001f)
            {
                failureMask |= FailureStressOxygen;
            }

            bool crushGlass = PlayerInventory.IsDepthPressureFragileResource((byte)ItemAudioMaterialId.Glass, ResourceFamily.None);
            bool crushElectronics = PlayerInventory.IsDepthPressureFragileResource((byte)ItemAudioMaterialId.Metal, ResourceFamily.ElectronicsMetal);
            bool shallowCrush = PlayerInventory.ShouldApplyDepthPressureCrush(1500f, false);
            bool protectedCrush = PlayerInventory.ShouldApplyDepthPressureCrush(2500f, true);
            float crushDamageMilli = PlayerInventory.ResolveDepthPressureCrushDamageMilli(2500f);
            if (!crushGlass ||
                !crushElectronics ||
                shallowCrush ||
                protectedCrush ||
                math.abs(crushDamageMilli - 40f) > 0.0001f)
            {
                failureMask |= FailurePressureCrush;
            }

            bool brownoutSiphon = SubmarineAtmosphereSystem.ShouldSiphonOxygenDuringBrownout(0.39f, 0.40f, true);
            bool thresholdSiphon = SubmarineAtmosphereSystem.ShouldSiphonOxygenDuringBrownout(0.40f, 0.40f, true);
            bool unoccupiedSiphon = SubmarineAtmosphereSystem.ShouldSiphonOxygenDuringBrownout(0.1f, 0.40f, false);
            float brownoutRate = SubmarineAtmosphereSystem.ResolveBrownoutOxygenConsumptionRate(0.0001f, 0.0008f);
            if (!brownoutSiphon ||
                thresholdSiphon ||
                unoccupiedSiphon ||
                math.abs(brownoutRate - 0.0008f) > 0.0001f)
            {
                failureMask |= FailureBrownoutSiphon;
            }

            if (!VisorHUDController.ShouldTriggerThermalShockBiosRecovery(85f, 20f, 80f, 80f) ||
                !VisorHUDController.ShouldTriggerThermalShockBiosRecovery(170f, 85f, 80f, 80f) ||
                VisorHUDController.ShouldTriggerThermalShockBiosRecovery(79f, 20f, 80f, 80f))
            {
                failureMask |= FailureThermalShock;
            }

            float nitrogenBuildDelta = HectonSurvivalSystem.ResolveNitrogenBuildUpDelta(10f, 800f, 2f);
            float shallowNitrogenBuildDelta = HectonSurvivalSystem.ResolveNitrogenBuildUpDelta(10f, 399f, 2f);
            float slowNitrogenBuildDelta = HectonSurvivalSystem.ResolveNitrogenBuildUpDelta(5f, 800f, 2f);
            float nitrogenNarcosis01 = HectonSurvivalSystem.ResolveNitrogenNarcosis01(125f);
            float nitrogenStaminaPenalty = HectonSurvivalSystem.ResolveNitrogenStaminaMultiplier(101f);
            float nitrogenSafeStamina = HectonSurvivalSystem.ResolveNitrogenStaminaMultiplier(100f);
            float nitrogenLoad = HectonSurvivalSystem.ResolveNitrogenTissueLoad(1f, 6f, 0.5f);
            bool bendsUnsafe = HectonSurvivalSystem.ShouldApplyBendsDamage(10.1f, 11.1f);
            bool bendsSafeLoad = HectonSurvivalSystem.ShouldApplyBendsDamage(10.1f, 10.9f);
            float pressureNarcosis01 = HectonSurvivalSystem.ResolvePressureNarcosis01(23.5f);
            float coldNutritionMultiplier = HectonSurvivalSystem.ResolveColdNutritionDrainMultiplier(8f, 20f, 12f);
            float nitrogenVisorTarget = VisorHUDController.ResolveHypoxiaNarcosisTarget(1f, 0.15f, 0.35f);
            if (math.abs(nitrogenBuildDelta) > 0.0001f ||
                shallowNitrogenBuildDelta > 0.0001f ||
                slowNitrogenBuildDelta > 0.0001f ||
                math.abs(nitrogenNarcosis01) > 0.0001f ||
                math.abs(nitrogenStaminaPenalty - 1f) > 0.0001f ||
                math.abs(nitrogenSafeStamina - 1f) > 0.0001f ||
                math.abs(nitrogenLoad - 1f) > 0.0001f ||
                bendsUnsafe ||
                bendsSafeLoad ||
                math.abs(pressureNarcosis01) > 0.0001f ||
                math.abs(coldNutritionMultiplier - 2f) > 0.0001f ||
                math.abs(nitrogenVisorTarget - 0.35f) > 0.0001f)
            {
                failureMask |= FailureNitrogenNarcosis;
            }

            return failureMask;
        }
    }
}
