// ============================================================================
// HECTON-8 - BaseModule.cs
// Base controller for underwater base modules.
//
// RESPONSIBILITIES:
//   1. Stores module integrity at runtime.
//   2. Maintains legacy flood presentation state; CSR flood truth is owned by HabitatFluidIncursionDirector.
//   3. Implements IPowerComponent for base power consumption.
//   4. Implements IPoolable for ObjectPoolManager compatibility.
//   5. Implements ISlowTickable for centralized slow ticking.
//   6. Implements ICuttable for LaserCutter compatibility through ApplyDamage.
//   7. Tracks player interior occupancy for life support.
//   8. Handles deconstruction refund and module destruction handoff.
//
// DECONSTRUCTION:
//   - Deconstruct(PlayerInventory) is called by LaserCutter after teardown
//     progress completes.
//   - Resources refund at 50 percent.
//   - Overflow resources spawn as HectonItem instances through ObjectPoolManager.
//   - ConstructionManager.DestroyModule() owns final destruction.
//
// INTERIOR DRY ZONE:
//   - A trigger BoxCollider defines the module interior for player life-support entry/exit only.
//   - Buoyancy dry-zone suppression and interior water planes are rejected; CSR flood truth is scalar Vault data.
//
// SAVE:
//   Module state is not self-serialized.
//   ConstructionManager reads CurrentIntegrity / IsFlooded during base save and
//   writes them back during load.
//
// STATES:
//   - Healthy: currentIntegrity == maxIntegrity, not flooded.
//   - Damaged: currentIntegrity < maxIntegrity, leak VFX active.
//   - Breached: currentIntegrity <= 0, flooded = true.
//   - Draining: flooded && hasPower && integrity == maxIntegrity.
//
// POWER:
//   - Base consumption comes from BuildableData.powerRating.
//   - Without power, pumps stop, lights turn off, and repair stalls.
//   - With power and full integrity, water drains.
//
// ZERO GC:
//   - No Update / FixedUpdate; all repeated logic runs through ISlowTickable.
//   - OnPowerStatusChanged toggles lights without per-frame polling.
//   - GetComponents is not called in hot paths.
//   - Dictionaries are preallocated; key lookups avoid boxing.
//   - OnTriggerStay is not used.
//   - Deconstruct uses for loops, TryAddItem, and no LINQ.
//   - No static collections leak across scene changes.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Caves;
using Hecton8.Construction;
using Hecton8.Crafting;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Economy;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Interaction;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Hecton8.Gameplay
{
    public struct BaseModuleSaveState
    {
        public float Integrity;
        public bool Flooded;
        public BaseModuleFailureMode CascadeFailure;
        public float RepairIntegrityCap;
        public float AirReserveNormalized;
        public float Co2Normalized;
        public float FloodedReefFloodSeconds;
        public bool InteriorReefInfestationActive;
    }

    public enum BaseModuleFailureMode : byte
    {
        None = 0,
        OxygenLeak = 1,
        Fire = 2,
        ShortCircuit = 3
    }

    public enum BaseModuleIntegrityState : byte
    {
        Pristine = 0,
        Flooded = 1,
        Ruptured = 2,
        Abandoned = 3
    }

    [DisallowMultipleComponent]
    public sealed class BaseModule : MonoBehaviour, IPowerComponent, IContinuousPowerComponent, IPoolable, ISlowTickable, IFixedTickable, IUpdatable, ILateFrameTickable, ICuttable, Hecton8.Core.Contracts.IPhysicsImpactMaterialProvider, IElectromagneticPulseEventListener, Hecton8.Interaction.IKinematicRepairTarget, Hecton8.Interaction.IRepairableModuleTarget, IGlobalRegistryHotSwapListener
    {
        private static int s_x001BaseModuleSignalPushDropCount;
        // COLD ALLOC: List<BaseModule>[64] - active runtime habitat module registry for cold-path environment scans - owner: BaseModule
        private static readonly List<BaseModule> s_activeModules = new List<BaseModule>(64);
        private const int ModuleWaterLevelShaderCapacity = 64;
        private const float BrownoutShaderStateEpsilon = 0.001f;
        private const float AupRadiusLogicThresholdMeters = 50f;
        private const float RepairHandHalfSpanMeters = 0.18f;
        private const float RepairHandVerticalBiasMeters = 0.05f;
        private static readonly int s_ModuleAmbienceDataId = Shader.PropertyToID("_HectonModuleAmbienceDataBuffer");
        private static readonly int s_ModuleWaterLevelsId = Shader.PropertyToID("_HectonModuleWaterLevelsBuffer");
        private static readonly int s_ModuleWaterLevelCountId = Shader.PropertyToID("_ModuleWaterLevelCount");
        private static readonly int s_BaseVoltageId = Shader.PropertyToID("_BaseVoltage");
        private static readonly int s_BaseVoltageFlickerSpeedId = Shader.PropertyToID("_BaseVoltageFlickerSpeed");
        private static readonly int s_BaseVoltageMinimumId = Shader.PropertyToID("_BaseVoltageMinimum");
        private static readonly int s_BaseBrownoutEmergencyColorId = Shader.PropertyToID("_BaseBrownoutEmergencyColor");
        // COLD ALLOC: Vector4[256] - global module center/radius upload scratch - owner: BaseModule
        private static readonly Vector4[] s_moduleAmbienceData = new Vector4[ModuleWaterLevelShaderCapacity];
        // COLD ALLOC: Vector4[256] - global module water/flicker upload scratch - owner: BaseModule
        private static readonly Vector4[] s_moduleFloodAndFlickerData = new Vector4[ModuleWaterLevelShaderCapacity];
        private static GraphicsBuffer s_moduleAmbienceDataBufferA;
        private static GraphicsBuffer s_moduleAmbienceDataBufferB;
        private static GraphicsBuffer s_moduleFloodAndFlickerDataBufferA;
        private static GraphicsBuffer s_moduleFloodAndFlickerDataBufferB;
        private static int s_moduleWaterLevelUploadIndex;
        private static int s_lastModuleWaterLevelUploadFrame = -1;
        private static bool s_moduleWaterLevelShaderDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveModuleRegistry()
        {
            ReleaseModuleWaterLevelBuffers();
            s_activeModules.Clear();
            s_lastModuleWaterLevelUploadFrame = -1;
            s_moduleWaterLevelShaderDirty = false;
            for (int i = 0; i < ModuleWaterLevelShaderCapacity; i++)
            {
                s_moduleAmbienceData[i] = Vector4.zero;
                s_moduleFloodAndFlickerData[i] = new Vector4(0f, 0f, 1f, 0f);
            }

            Shader.SetGlobalInt(s_ModuleWaterLevelCountId, 0);
            Shader.SetGlobalFloat(s_BaseVoltageId, 1f);
            Shader.SetGlobalFloat(s_BaseVoltageFlickerSpeedId, 19f);
            Shader.SetGlobalFloat(s_BaseVoltageMinimumId, 0.04f);
            Shader.SetGlobalColor(s_BaseBrownoutEmergencyColorId, new Color(1f, 0.13f, 0.06f, 1f));
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorLifecycleHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseModuleWaterLevelBuffers;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseModuleWaterLevelBuffers;
            UnityEditor.EditorApplication.quitting -= ReleaseModuleWaterLevelBuffers;
            UnityEditor.EditorApplication.quitting += ReleaseModuleWaterLevelBuffers;
        }
#endif

        private static void EnsureModuleWaterLevelBuffers()
        {
            if (s_moduleAmbienceDataBufferA != null &&
                s_moduleAmbienceDataBufferB != null &&
                s_moduleFloodAndFlickerDataBufferA != null &&
                s_moduleFloodAndFlickerDataBufferB != null)
            {
                return;
            }

            ReleaseModuleWaterLevelBuffers();
            s_moduleAmbienceDataBufferA = CreateModuleWaterLevelBuffer(); // COLD ALLOC: GraphicsBuffer[64 float4] A - module ambience shader upload - owner: BaseModule
            s_moduleAmbienceDataBufferB = CreateModuleWaterLevelBuffer(); // COLD ALLOC: GraphicsBuffer[64 float4] B - module ambience shader upload - owner: BaseModule
            s_moduleFloodAndFlickerDataBufferA = CreateModuleWaterLevelBuffer(); // COLD ALLOC: GraphicsBuffer[64 float4] A - module water/flicker shader upload - owner: BaseModule
            s_moduleFloodAndFlickerDataBufferB = CreateModuleWaterLevelBuffer(); // COLD ALLOC: GraphicsBuffer[64 float4] B - module water/flicker shader upload - owner: BaseModule
            s_moduleWaterLevelUploadIndex = 0;
        }

        private static void ReleaseModuleWaterLevelBuffers()
        {
            ReleaseModuleWaterLevelBuffer(ref s_moduleAmbienceDataBufferA);
            ReleaseModuleWaterLevelBuffer(ref s_moduleAmbienceDataBufferB);
            ReleaseModuleWaterLevelBuffer(ref s_moduleFloodAndFlickerDataBufferA);
            ReleaseModuleWaterLevelBuffer(ref s_moduleFloodAndFlickerDataBufferB);
            s_moduleWaterLevelUploadIndex = 0;
        }

        private static void UploadModuleWaterLevelBuffers()
        {
            GraphicsBuffer ambienceWrite = s_moduleWaterLevelUploadIndex == 0 ? s_moduleAmbienceDataBufferA : s_moduleAmbienceDataBufferB;
            GraphicsBuffer floodWrite = s_moduleWaterLevelUploadIndex == 0 ? s_moduleFloodAndFlickerDataBufferA : s_moduleFloodAndFlickerDataBufferB;
            if (ambienceWrite == null || floodWrite == null)
                return;

            UploadModuleWaterLevelArray(ambienceWrite, s_moduleAmbienceData);
            UploadModuleWaterLevelArray(floodWrite, s_moduleFloodAndFlickerData);
            Shader.SetGlobalBuffer(s_ModuleAmbienceDataId, ambienceWrite);
            Shader.SetGlobalBuffer(s_ModuleWaterLevelsId, floodWrite);
            s_moduleWaterLevelUploadIndex ^= 1;
        }

        private static GraphicsBuffer CreateModuleWaterLevelBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                ModuleWaterLevelShaderCapacity,
                sizeof(float) * 4);
        }

        private static void ReleaseModuleWaterLevelBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void UploadModuleWaterLevelArray(GraphicsBuffer buffer, Vector4[] source)
        {
            NativeArray<Vector4> mapped = buffer.LockBufferForWrite<Vector4>(0, ModuleWaterLevelShaderCapacity);
            try
            {
                for (int i = 0; i < ModuleWaterLevelShaderCapacity; i++)
                    mapped[i] = source[i];
            }
            finally
            {
                buffer.UnlockBufferAfterWrite<Vector4>(ModuleWaterLevelShaderCapacity);
            }
        }

        private static void QueueActiveModuleWaterLevelsShaderUpload(bool force = false)
        {
            if (force)
                s_lastModuleWaterLevelUploadFrame = -1;

            s_moduleWaterLevelShaderDirty = true;
        }
        // ==========================================================
        //  CONSTANTS
        // ==========================================================

        /// <summary>
        /// Fiksirovannaya delta medlennogo tika (sekundy).
        /// GameTickManager vyzyvaet SlowTick() s etim intervalom.
        /// </summary>
        private const float SLOW_TICK_DT = 0.5f;

        /// <summary>
        /// Maksimum kollayderov, pereschityvaemyh pri holodnoy sinhronizatsii interior zone.
        /// </summary>
        private const int INTERIOR_OVERLAP_CAPACITY = 32;
        private const float SeawaterDensityKilogramsPerCubicMeter = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        private const float GravityAccelerationMetersPerSecondSquared = HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
        private const float MinimumMassKilograms = 1f;
        private const float BuoyancyMassUpdateThresholdKilograms = 0.5f;
        private const float DefaultMaximumHydroStructuralLoadNewtons = 500000f;
        private const float DefaultBulkheadFailureWaterMassKilograms = 18000f;
        private const float DefaultBulkheadStressRatePerSecond = 0.035f;
        private const float DefaultBulkheadStressRecoveryPerSecond = 0.01f;
        private const float SurfacePressureKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float DefaultDeepCompressionStartDepthMeters = 3000f;
        private const float DefaultDeepCompressionFullPressureKPa = 60000f;
        private const float DefaultMaximumDeepCompressionAxisLoss = 0.001f;
        private const float DefaultJointShearCompressionDeltaThreshold = 0.15f;
        private const float DefaultJointShearDamagePerSecondAtFullDelta = 0.02f;
        private const float DefaultJointShearStressRecoveryPerSecond = 0.08f;
        private const float DefaultJointShearGroanCooldownSeconds = 4f;
        private const float DefaultHullCondensationStartDepthMeters = 2000f;
        private const float CinematicLeakFullDepthMeters = 4000f;
        private const float CinematicLeakFullDepthMetersInv = 1f / CinematicLeakFullDepthMeters;
        private const float CinematicLeakBaseIntensity01 = 0.12f;
        private const uint FastSqrtApproximationBias = 0x1FC00000u;
        private const float AirPocketMinimumRemainingVolume01 = 0.05f;
        private const float AirPocketCrackPressureAtm = 3f;
        private const float AirPocketCrackPressureInvRange = 0.25f;
        private const float FloodFireSuppressionThreshold01 = 0.2f;
        private const float FloodShortCircuitThreshold01 = 0.5f;
        private const float FloodShortCircuitBaseChance01 = 0.18f;
        private const float FloodShortCircuitHashToUnit01 = 1f / 16777215f;
        private const uint FloodShortCircuitHashSalt = 0xA53A9B5Du;
        private const float DefaultHullCondensationFullDepthMeters = 5000f;
        private const float DefaultLowIntegrityGroanNoiseFrequency = 0.19f;
        private const float DefaultLowIntegrityGroanNoiseThreshold = 0.58f;
        private const float DefaultBrownoutEmergencyTransitionSeconds = 0.5f;
        private const float DefaultBreachVortexDurationSeconds = 5f;
        private const float DefaultBreachVortexReferenceMassKilograms = 80f;
        private const float DefaultBreachVortexMaximumAccelerationMetersPerSecondSquared = 45f;
        private const float DefaultBreachVortexRadiusPaddingMeters = 1.25f;
        private const float DefaultImplosionDepthThresholdMeters = 2000f;
        private const float DefaultImplosionImpulseRadiusMeters = 30f;
        private const float DefaultImplosionMaximumImpulseNewtonSeconds = 65000f;
        private const float DefaultLocalGravityHoldSeconds = 0.75f;
        private const float DefaultInGameDaySeconds = 3600f;
        private const float DefaultFloodedReefActivationDays = 3f;
        private const int FloodedReefFaunaAnchorSalt = unchecked((int)0x52EF0A11);
        private const int ParasiteSporeHazardThreshold = 5;
        private const float ParasiteAttachedPowerConsumptionScalar = 1.2f;
        private const int MinimumFloodedReefProxyPoolReserve = 10;
        private const string FloodedReefFaunaFamilyId = "fauna.family.reef_small";
        private const string FoundationPersistentId = "Build_Foundation_Platform";
        private const string PylonPersistentId = "Build_Utility_Pylon";
        private const string AirlockPersistentId = "Build_Airlock_Hatch";
        private const string LegacyAirlockPersistentId = "base.module.airlock";
        private const int DefaultHabitatBaseId = 0;
        private static readonly uint s_baseCascadeNotificationMissWarningHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("BaseModule.CascadeNotificationMiss"));
        private static readonly uint s_baseCascadeNotificationContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("BaseModule.CascadeNotification"));

        /// <summary>
        /// Koeffitsient vozvrata resursov pri dekonstruktsii.
        /// 0.8 = 80% resursov vozvraschaetsya.
        /// </summary>
        private const int RefundDivisor = 2;

        /// <summary>
        /// Canonical child name for module-local leak particle owner.
        /// Used as a cold-path fallback when serialized reference is missing.
        /// </summary>
        private const string LeakVfxChildName = "LeakVfx";
        private const string InteriorCaveWeedChildName = "Cave-Weed";
        private const string InteriorBarnaclesChildName = "Barnacles";

        // ==========================================================
        //  INSPECTOR
        // ==========================================================

        [Header("Integrity")]
        [Tooltip("Maximum module integrity.")]
        [SerializeField] private float maxIntegrity = 100f;

        [Tooltip("Starting module integrity.")]
        [SerializeField] private float currentIntegrity = 100f;
        [Tooltip("Optional immutable template that owns abandoned-module integrity authoring and VFX socket coordinates.")]
        [SerializeField] private BaseModuleTemplate moduleTemplate;

        [Tooltip("Whether the module starts flooded. Usually false.")]
        [SerializeField] private bool isFlooded;

        [Header("Anchor / Unmoored Physics")]
        [Tooltip("Explicit authoring fallback for modules that must count as seafloor anchors in habitat traversal.")]
        [SerializeField] private bool isStructuralAnchor;

        [Tooltip("Explicit authoring fallback for modules that must obey emergency bulkhead lockdown.")]
        [SerializeField] private bool isEmergencyAirlock;

        [Tooltip("Dry structural mass routed into unmoored buoyancy evaluation in kilograms.")]
        [SerializeField, Min(1f)] private float structuralDryMassKilograms = 14000f;

        [Tooltip("Displacement volume used by unmoored buoyancy evaluation in cubic meters.")]
        [SerializeField, Min(0.1f)] private float buoyancyDisplacementVolumeCubicMeters = 18f;

        [Tooltip("Absolute cap applied to unmoored buoyancy acceleration in meters per second squared.")]
        [SerializeField, Min(0.1f)] private float maximumUnmooredAccelerationMetersPerSecondSquared = 24f;

        [Tooltip("Maximum local-space visual lean bias toward the breach while the room floods.")]
        [FormerlySerializedAs("maximumCenterOfMassShiftMeters")]
        [SerializeField, Min(0.01f)] private float maximumFloodVisualLeanBiasMeters = 0.85f;

        [Tooltip("Blend time constant used when leaning the visual root toward the flooding breach.")]
        [FormerlySerializedAs("centerOfMassShiftTauSeconds")]
        [SerializeField, Min(0.01f)] private float floodVisualLeanTauSeconds = 1.2f;

        [Tooltip("Flooded unmoored modules crossing this external depth get an additional crushing sink acceleration.")]
        [SerializeField, Min(1f)] private float hullCrushDepthMeters = 4000f;

        [Tooltip("Absolute cap for graph-driven hydro-structural downward load queued into an unmoored module body.")]
        [SerializeField, Min(1f)] private float maximumHydroStructuralLoadNewtons = DefaultMaximumHydroStructuralLoadNewtons;

        [Header("Abyssal Pressure Compression")]
        [Tooltip("Optional visual root scaled by abyssal pressure. Leave null to keep colliders and socket transforms authoritative.")]
        [SerializeField] private Transform pressureCompressionVisualRoot;

        [Tooltip("Depth in meters where deep-sea pressure begins reducing room volume.")]
        [SerializeField, Min(0f)] private float deepCompressionStartDepthMeters = DefaultDeepCompressionStartDepthMeters;

        [Tooltip("Hydrostatic pressure in kPa where the authored maximum compression is reached.")]
        [SerializeField, Min(1f)] private float deepCompressionFullPressureKPa = DefaultDeepCompressionFullPressureKPa;

        [Tooltip("Maximum X/Y visual axis loss at full crush pressure. 0.001 = one millimeter per meter.")]
        [SerializeField, Range(0f, 0.01f)] private float maximumDeepCompressionAxisLoss = DefaultMaximumDeepCompressionAxisLoss;

        [Tooltip("Normalized compression-alpha delta across a graph edge before joint shear starts damaging both modules.")]
        [SerializeField, Range(0f, 1f)] private float jointShearCompressionDeltaThreshold = DefaultJointShearCompressionDeltaThreshold;

        [Tooltip("Integrity fraction consumed per second when the edge compression delta reaches the maximum possible mismatch.")]
        [SerializeField, Min(0f)] private float jointShearDamagePerSecondAtFullDelta = DefaultJointShearDamagePerSecondAtFullDelta;

        [Tooltip("Normalized joint shear stress recovered per second when no pressure mismatch overload is present.")]
        [SerializeField, Min(0f)] private float jointShearStressRecoveryPerSecond = DefaultJointShearStressRecoveryPerSecond;

        [Tooltip("Minimum seconds between structural groan audio events emitted by this module under joint shear.")]
        [SerializeField, Min(0.1f)] private float jointShearGroanCooldownSeconds = DefaultJointShearGroanCooldownSeconds;

        [Tooltip("External depth in meters where interior hull condensation starts becoming visible.")]
        [SerializeField, Min(0f)] private float hullCondensationStartDepthMeters = DefaultHullCondensationStartDepthMeters;

        [Tooltip("External depth in meters where interior hull condensation reaches full shader strength.")]
        [SerializeField, Min(1f)] private float hullCondensationFullDepthMeters = DefaultHullCondensationFullDepthMeters;

        [Tooltip("Integrity fraction below which deterministic pressure-creak events can fire.")]
        [SerializeField, Range(0.01f, 1f)] private float lowIntegrityGroanThreshold01 = 0.5f;

        [Tooltip("Low-frequency noise speed for non-periodic rupture creak intervals.")]
        [SerializeField, Min(0.01f)] private float lowIntegrityGroanNoiseFrequency = DefaultLowIntegrityGroanNoiseFrequency;

        [Tooltip("Noise threshold crossed by damaged rooms before a structural creak event is queued.")]
        [SerializeField, Range(-1f, 1f)] private float lowIntegrityGroanNoiseThreshold = DefaultLowIntegrityGroanNoiseThreshold;

        [Tooltip("Minimum stress payload sent to the procedural structural creak renderer.")]
        [SerializeField, Range(0f, 1f)] private float lowIntegrityGroanStressFloor = 0.62f;

        [Tooltip("Lowest pitch multiplier for damaged-room metallic creaks.")]
        [SerializeField, Min(0.1f)] private float lowIntegrityGroanPitchMin = 0.52f;

        [Tooltip("Highest pitch multiplier for damaged-room metallic creaks.")]
        [SerializeField, Min(0.1f)] private float lowIntegrityGroanPitchMax = 0.78f;

        [Header("Breach Vortex")]
        [Tooltip("Duration in seconds for the transient depressurization vortex emitted on module breach.")]
        [SerializeField, Min(0f)] private float breachVortexDurationSeconds = DefaultBreachVortexDurationSeconds;

        [Tooltip("Reference mass used to convert pressure force into vortex acceleration.")]
        [SerializeField, Min(1f)] private float breachVortexReferenceMassKilograms = DefaultBreachVortexReferenceMassKilograms;

        [Tooltip("Maximum acceleration applied to loose bodies by the breach vortex.")]
        [SerializeField, Min(0f)] private float breachVortexMaximumAccelerationMetersPerSecondSquared = DefaultBreachVortexMaximumAccelerationMetersPerSecondSquared;

        [Tooltip("Extra radius added around the interior trigger bounds for vortex spatial-hash queries.")]
        [SerializeField, Min(0f)] private float breachVortexRadiusPaddingMeters = DefaultBreachVortexRadiusPaddingMeters;

        [Header("Catastrophic Implosion")]
        [Tooltip("External depth where an abandoned submerged module implodes instead of remaining as static wreckage.")]
        [SerializeField, Min(0f)] private float implosionDepthThresholdMeters = DefaultImplosionDepthThresholdMeters;

        [Tooltip("Radius in meters affected by the one-shot implosion impulse.")]
        [SerializeField, Min(0.5f)] private float implosionImpulseRadiusMeters = DefaultImplosionImpulseRadiusMeters;

        [Tooltip("Maximum impulse routed to loose entities by catastrophic implosion.")]
        [SerializeField, Min(0f)] private float implosionMaximumImpulseNewtonSeconds = DefaultImplosionMaximumImpulseNewtonSeconds;

        [Header("Local Gravity Anomaly")]
        [Tooltip("When enabled, players inside this module receive a local gravity vector request from the interior volume.")]
        [SerializeField] private bool localGravityAnomalyEnabled;

        [Tooltip("Module-local direction used by relic gravity anomaly volumes.")]
        [SerializeField] private Vector3 localGravityDirection = Vector3.down;

        [Tooltip("Acceleration magnitude in meters per second squared for local module gravity.")]
        [SerializeField, Min(0f)] private float localGravityAccelerationMetersPerSecondSquared = GravityAccelerationMetersPerSecondSquared;

        [Tooltip("How long each slow-tick gravity request remains authoritative on the player controller.")]
        [SerializeField, Min(0.1f)] private float localGravityHoldSeconds = DefaultLocalGravityHoldSeconds;

        [Header("Flood / Drain")]
        [Tooltip("Seconds required to fully drain the module.")]
        [SerializeField] private float drainDuration = 8f;
        [SerializeField] private float floodPumpEnergyCost = 65f;

        [Tooltip("Passive integrity recovery rate in units per second. Zero disables it.")]
        [SerializeField] private float passiveRecoveryRate = 0f;

        [Tooltip("Passive integrity degradation rate in units per second. " +
                 "Lore: approximately 0.1 percent per game day. Depth above 500m multiplies by depthDegradationMultiplier.")]
        [SerializeField] private float passiveDegradationRate = 0.001f;

        [Tooltip("Integrity degradation multiplier applied below 500m depth for hull pressure stress.")]
        [SerializeField, UnityEngine.Range(1f, 5f)] private float depthDegradationMultiplier = 2f;

        [Header("Cascade Failures")]
        [Tooltip("Current cascade failure. None means nominal; all other modes require service recovery.")]
        [SerializeField] private BaseModuleFailureMode failureMode;
        [Tooltip("Permanent integrity lost after each cascade failure.")]
        [SerializeField] private float repairWearPerCascade = 12f;
        [Tooltip("Lowest fraction of original integrity that repairs are allowed to restore.")]
        [SerializeField, Range(0.1f, 1f)] private float minimumRecoverableIntegrityRatio = 0.45f;
        [Tooltip("Current repair ceiling for this module. Repeated failures reduce it until the module is rebuilt.")]
        [SerializeField] private float maxRecoverableIntegrity = 100f;

        [Header("Fluid Incursion")]
        [Tooltip("Current seawater volume retained by this module interior.")]
        [SerializeField, Min(0f)] private float waterVolumeM3;

        [Tooltip("Effective breach hole area used by the pressure leak kernel.")]
        [SerializeField, Min(0.0001f)] private float breachHoleAreaSquareMeters = 0.08f;

        [Tooltip("Pressure-to-flow normalization for Volume += sqrt(dP) * holeArea * dt.")]
        [SerializeField, Min(0f)] private float breachPressureFlowCoefficient = 0.001f;

        [Tooltip("Hydrostatic pressure delta that immediately floods a newly ruptured room.")]
        [SerializeField, Min(0f)] private float explosiveFloodPressureDeltaKPa = 650f;

        [Tooltip("Permanent fatigue cycles accumulated after flood-to-dry or depressurization events.")]
        [SerializeField, Min(0)] private int fatigueDamage;

        [Tooltip("Opposing flood-water mass that a sealed airlock can hold before bulkhead stress reaches full-rate accumulation.")]
        [SerializeField, Min(1f)] private float bulkheadFailureWaterMassKilograms = DefaultBulkheadFailureWaterMassKilograms;

        [Tooltip("Normalized bulkhead stress accumulated per second when opposing flood mass equals the failure mass.")]
        [SerializeField, Min(0.001f)] private float bulkheadStressRatePerSecond = DefaultBulkheadStressRatePerSecond;

        [Tooltip("Normalized bulkhead stress recovered per second when the airlock is no longer holding back flood pressure.")]
        [SerializeField, Min(0f)] private float bulkheadStressRecoveryPerSecond = DefaultBulkheadStressRecoveryPerSecond;

        [Tooltip("Player suit oxygen drain rate inside a failing module.")]
        [SerializeField] private float oxygenLeakDrainRate = 10f;

        [Tooltip("Player suit damage rate inside a burning module.")]
        [SerializeField] private float fireSuitDamageRate = 12f;

        [Tooltip("Player suit energy drain rate inside a burning module.")]
        [SerializeField] private float fireSuitEnergyDrainRate = 6f;

        [Header("Interior Zone")]
        [Tooltip("Trigger BoxCollider covering the module interior. " +
                 "Used only for player life-support occupancy; flood water truth is owned by HabitatFluidIncursionDirector.")]
        [SerializeField] private BoxCollider interiorTrigger;

        [Tooltip("Authored interior wall surfaces that swap to a dedicated condensation material when hot air meets cold hull.")]
        [SerializeField] private BaseModuleCondensationSurface[] condensationSurfaces = Array.Empty<BaseModuleCondensationSurface>();

        [Header("Flooded Reef")]
        [SerializeField, Min(0f)]
        [Tooltip("Continuous flooded in-game days before the room latches interior reef growth.")]
        private float floodedReefActivationDays = DefaultFloodedReefActivationDays;

        [SerializeField]
        [Tooltip("Optional preauthored Cave-Weed interior growth root toggled when flooded reef growth latches.")]
        private GameObject interiorCaveWeed;

        [SerializeField]
        [Tooltip("Optional preauthored Barnacles interior growth root toggled when flooded reef growth latches.")]
        private GameObject interiorBarnacles;

        [SerializeField]
        [Tooltip("Impact-audio material exposed by this module after flooded reef growth latches.")]
        private byte floodedReefAudioMaterialId = (byte)ItemAudioMaterialId.Organic;

        [SerializeField]
        [Tooltip("Optional continuous toxic spore VFX toggled when parasite count exceeds the hazard threshold.")]
        private ParticleSystem parasiteSporeVfx;

        [SerializeField, Min(0.1f)]
        [Tooltip("Toxicity hazard radius used by overgrown parasite spore rooms.")]
        private float parasiteSporeHazardRadius = 3.2f;

        [Header("Deconstruction")]
        [Tooltip("Prefab mirovogo predmeta (HectonItem) dlya spavna resursov, " +
                 "kotorye ne pomestilis v inventar. " +
                 "Dolzhen imet HectonItem + physics pickup runtime body.")]
        [SerializeField] private GameObject worldItemPrefab;

        [Tooltip("Optional renderer whose shared material is swapped while this module is targeted for deconstruction.")]
        [SerializeField] private Renderer deconstructionGhostRenderer;

        [Tooltip("Shared red wireframe/glitch material used for deconstruction targeting. No runtime material clone is created.")]
        [SerializeField] private Material deconstructionGhostMaterial;

        [Tooltip("Optional preauthored red wireframe/glitch visual toggled while the module is targeted for deconstruction.")]
        [SerializeField] private GameObject deconstructionGhostVisual;

        [Header("Visual References")]
        [Tooltip("Obekt vody vnutri modulya. Aktiven, kogda modul zatoplen.")]
        [SerializeField] private GameObject waterVolume;

        [Tooltip("Optional water-surface proxy transform driven by room flood fill. Only the local Y value is animated.")]
        [SerializeField] private Transform floodSurfacePlane;

        [Tooltip("Fallback local-space Y range for the water-surface proxy when the interior trigger cannot provide bounds.")]
        [SerializeField] private Vector2 floodSurfaceLocalYRange = new Vector2(-1.25f, 1.25f);

        [Tooltip("Effekt puzyrkov / utechki pri povrezhdenii.")]
        [SerializeField] private ParticleSystem leakVfx;

        [Tooltip("Vnutrennie istochniki sveta. Vyklyuchayutsya pri otsutstvii pitaniya.")]
        [SerializeField] private Light[] interiorLights;

        [Header("Brownout Ambience")]
        [Tooltip("Voltage ratio below which room lights enter deterministic brownout flicker.")]
        [SerializeField, Range(0.01f, 1f)] private float brownoutActivationVoltageRatio = 0.80f;

        [Tooltip("Deterministic noise frequency used by low-voltage light flicker.")]
        [SerializeField, Min(0.1f)] private float brownoutFlickerSpeed = 19f;

        [Tooltip("Minimum shader emission fraction during brownout flicker.")]
        [SerializeField, Range(0f, 1f)] private float brownoutMinimumLightIntensityRatio = 0.04f;

        [Tooltip("Emergency tint reference for shader-driven brownout surfaces. PointLight mutation is forbidden.")]
        [SerializeField] private Color brownoutEmergencyEmissionColor = new Color(1f, 0.13f, 0.06f, 1f);

        [Tooltip("Seconds required for white interior lighting to transition into emergency red.")]
        [SerializeField, Min(0.05f)] private float brownoutEmergencyTransitionSeconds = DefaultBrownoutEmergencyTransitionSeconds;

        [Tooltip("Lokalnyy Volume dlya tumana / postprotsessa zatopleniya.")]
        [SerializeField] private Volume floodedLocalVolume;

        [Tooltip("Optional camera/probe transform used to enable flooded screen-space distortion only while below the water plane.")]
        [SerializeField] private Transform floodDistortionProbe;

        [Header("Audio (optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip leakLoop;
        [SerializeField] private AudioClip floodClip;
        [SerializeField] private AudioClip drainClip;
        [SerializeField] private AudioClip deconstructClip;
        [Tooltip("Optional authored 40Hz-style scrubber bed source. Pitch and volume are driven without allocation.")]
        [SerializeField] private AudioSource oxygenScrubberHumSource;
        [SerializeField] private AudioClip oxygenScrubberHumLoop;
        [SerializeField, Range(0f, 1f)] private float oxygenScrubberHumVolume = 0.18f;
        [SerializeField, Min(0.01f)] private float oxygenScrubberHumPoweredPitch = 1f;
        [SerializeField, Min(0.01f)] private float oxygenScrubberHumFailPitch = 0.2f;
        [SerializeField, Min(0.1f)] private float oxygenScrubberHumFailFadeSeconds = 3f;
        [Header("Life Support")]
        [Tooltip("Oxygen refill rate (units per second) when player is inside,\n" +
                 "module is powered, and not flooded.\n" +
                 "15 = full O2 tank (~100 units) refilled in ~7 seconds.")]
        [SerializeField] private float oxygenRefillRate = 15f;
        [Tooltip("Maximum breathable reserve stored inside the module for dry-zone life support.")]
        [SerializeField] private float breathableReserveCapacity = 120f;
        [Tooltip("Current breathable reserve. Older authored modules initialize this to full on first runtime spawn.")]
        [SerializeField] private float breathableReserve = 120f;
        [Tooltip("How quickly powered scrubbers rebuild breathable reserve while the compartment stays serviceable.")]
        [SerializeField] private float airRecycleRate = 6f;
        [Tooltip("Seconds of scrubber operation supplied by one Data_CarbonFilter item.")]
        [SerializeField, Min(1f)] private float carbonFilterConsumptionIntervalSeconds = 30f;
        [Tooltip("Breathable reserve consumed each second while an occupant is using this module as dry shelter.")]
        [SerializeField] private float occupiedAirDrainRate = 9f;
        [Tooltip("Air-quality threshold below which dry shelter becomes a short-lived stale-air pocket.")]
        [SerializeField, Range(0.05f, 0.8f)] private float staleAirThreshold = 0.25f;
        [Tooltip("Minimum refill fraction while stale air still exists but the scrubber loop is near saturation.")]
        [SerializeField, Range(0f, 1f)] private float staleAirMinRefillScale = 0.2f;
        [Tooltip("Suit oxygen lost per second when breathable reserve is fully exhausted inside an otherwise dry module.")]
        [SerializeField] private float staleAirSuitDrainRate = 3f;
        [Tooltip("Maximum CO2 load the dry-air loop can tolerate before mechanical regeneration locks out.")]
        [SerializeField] private float co2Capacity = 100f;
        [Tooltip("Current accumulated CO2 inside this module.")]
        [SerializeField] private float co2Level;
        [Tooltip("CO2 generated each second while an occupant uses this module as breathable shelter.")]
        [SerializeField] private float co2GenerationRate = 5f;
        [Tooltip("CO2 threshold beyond which power alone can no longer restore breathable reserve.")]
        [SerializeField] private float co2CriticalThreshold = 75f;
        [Header("Power Fallback")]
        [Tooltip("Fallback power draw, esli BuildableData / ModuleMarker otsutstvuyut.")]
        [SerializeField] private float fallbackPowerRating = -10f;

        [Tooltip("Prioritet otklyucheniya pomp/osvescheniya modulya.")]
        [Range(0, 100)]
        [SerializeField] private int powerPriority = 50;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugIsDraining;
        [SerializeField] private float _debugDrainProgress;
        [SerializeField] private int _debugTrackedObjectCount;

        // ==========================================================
        //  RUNTIME STATE
        // ==========================================================

        private bool _hasPower = true;
        private bool _ambientLightsBrownedOut;
        private bool _updatableRegistered;
        private bool _pendingInteriorLightsEnabled;
        private bool _hasPendingInteriorLightsEnabled;
        private float _ambientVoltageSupplyRatio = 1f;
        private float _brownoutNoiseSeed;
        private float _currentBrownoutFlicker01 = 1f;
        private float _brownoutTransition01;
        private float _brownoutTransitionTarget01;
        private float _ruptureGroanNoisePhase;
        private float _ruptureGroanPreviousNoise = -1f;
        private float _oxygenHum01;
        private float _oxygenHumTarget01;
        private bool _oxygenHumActive;
        private bool _oxygenHumSourceConfigured;
        private AudioSource _configuredOxygenHumSource;
        private AudioClip _configuredOxygenHumClip;
        private float _basePowerRating;
        private float _parasitePowerDrainWatts;
        private float _parasiteRootPowerDrainWatts;
        private float _cultivationScrubberPowerDrainWatts;
        private float _cultivationLightingPowerCreditWatts;
        private float _parasiteAddedMassKilograms;
        private float _parasiteThermalInsulation01;
        private float _parasiteBioReactorOverheatMultiplier = 1f;
        private float _parasiteInfectionLevel;
        private float _parasiteRootInfectionLevel;
        private int _attachedParasiteCount;
        private float _floodedReefFloodSeconds;
        private bool _interiorReefInfestationActive;
        private bool _tickRegistered;
        private bool _fixedTickRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _isUnmoored;
        private bool _pendingLeakVisualDirty;
        private bool _pendingLeakActive;
        private bool _pendingFloodVisualDirty;
        private AudioClip _pendingSpatialSfx0;
        private AudioClip _pendingSpatialSfx1;
        private AudioClip _pendingSpatialSfx2;
        private AudioClip _pendingSpatialSfx3;
        private byte _pendingSpatialSfxCount;
        private bool _pendingOxygenHumVisualDirty;
        private bool _pendingPressureVisualScaleDirty;
        private bool _pendingPressureVisualRotationDirty;
        private bool _pendingInteriorReefVisualDirty;
        private bool _pendingParasiteSporeVfxDirty;
        private bool _pendingParasiteSporeVfxActive;
        private Vector3 _pendingPressureVisualScale = Vector3.one;
        private Quaternion _pendingPressureVisualRotation = Quaternion.identity;
        private Quaternion _pressureCompressionVisualRotationState = Quaternion.identity;
        private bool _pendingInteriorReefVisualActive;

        private ModuleMarker _moduleMarker;
        private HabitatIntegrityManager _habitatIntegrityManager;
        private ISubmarineAtmosphereRoomMutationSink _submarineAtmosphereSystem;
        private IAtmosphereReadModel _atmosphereRuntime;
        private IPlayerInventoryService _cachedPlayerInventoryService;
        private Hecton8.Core.IAudioService _cachedAudioService;
        private ISpatialAudioSfxMixerRouteReadModel _cachedSpatialAudioSfxRoute;
        private IObjectPoolService _cachedObjectPool;
        private IPlayerRuntimeContext _cachedPlayerRuntime;
        private IPhysicsService _cachedPhysicsService;
        private ConstructionManager _constructionManager;
        private bool _empListenerRegistered;
        private PowerNode _powerNode;
        private HectonVoxelVolume _voxelVolume;
        private bool _breachLatched;
        private Rigidbody _moduleRigidbody;
        private int _cachedAtmosphereRoomIndex = -1;
        private Vector3 _breachCenterOfMassTargetLocal;
        private bool _hasBreachCenterOfMassTarget;
        private float _defaultBodyMass;
        private float _defaultLinearDamping;
        private float _defaultAngularDamping;
        private Vector3 _defaultFloodSurfaceLocalPosition;
        private float _cachedFloodCapacityM3;
        private float _inverseFloodCapacityM3;
        private float _cachedFloodLevel01;
        private float _bulkheadFloodStress01;
        private float _queuedHydroStructuralLoadNewtons;
        private float _queuedHydroStructuralLoadRemainingSeconds;
        private Vector3 _queuedHydroStructuralLoadPointWorld;
        private bool _bulkheadFailureLatched;
        private bool _ruptureCascadeFailureQueued;
        private bool _emergencyBulkheadLockedDown;
        private bool _implosionTriggered;
        private bool _defaultBodyIsKinematic;
        private bool _defaultBodyUseGravity;
        private CollisionDetectionMode _defaultCollisionDetectionMode;
        private RigidbodyInterpolation _defaultInterpolation;
        private bool _moduleBodyDefaultsCaptured;
        private bool _floodSurfaceDefaultsCaptured;
        private Vector3 _defaultPressureCompressionVisualScale = Vector3.one;
        private Quaternion _defaultPressureCompressionVisualRotation = Quaternion.identity;
        private bool _pressureCompressionDefaultsCaptured;
        private float _pressureCompressionAxisScale = 1f;
        private float _pressureCompressionVolumeScale = 1f;
        private float _pressureCompressionDepthMeters;
        private float _jointShearStress01;
        private float _jointShearGroanCooldownRemainingSeconds;
        private bool _detachedAsDebris;
        private bool _carbonFilterAvailable = true;
        private bool _condensationActive;
        private float _carbonFilterTimerSeconds;

        /// <summary>
        /// Previous flooded state used for flood-state edge detection.
        /// Initialized from isFlooded in OnSpawn/Awake.
        /// </summary>
        private bool _wasFlooded;

        /// <summary>
        /// Guard against repeated Deconstruct calls, including future multiplayer overlap.
        /// </summary>
        private bool _isDeconstructing;
        private bool _deconstructionPreviewActive;
        private Material _deconstructionPreviewOriginalMaterial;
        // Life Support State

        /// <summary>
        /// Cached reference to player's survival system.
        /// Set when player enters interior trigger, cleared on exit.
        /// Null = player is not inside this module.
        /// </summary>
        private HectonSurvivalSystem _trackedPlayerSurvival;
        private IPlayerMovementEnvironmentSink _trackedPlayerMovement;
        private IPlayerHypoxiaPresentationSink _trackedPlayerHypoxiaPresentation;
        private readonly ModuleIntegrityComponent _integrityComponent = new ModuleIntegrityComponent();
        private readonly ModuleLifeSupportComponent _lifeSupportComponent = new ModuleLifeSupportComponent();
        private FixedCharBuffer _fieldOperationSummaryBuffer = new FixedCharBuffer(320);
        private int _cascadeNotificationMissCount;
        // SHINOBU_330: interior trigger remains life-support occupancy only.
        // Flood water and dry-zone physics truth is routed through HabitatFluidIncursionDirector and SignalBus.
        // COLD ALLOC: List<BaseAirlock>[2] - cached owned airlock controllers for emergency lockdown fan-out - owner: BaseModule
        private readonly List<BaseAirlock> _airlockBuffer = new List<BaseAirlock>(2);
        // COLD ALLOC: List<SealedDoor>[2] - cached owned sealed bulkhead doors for quarantine locking - owner: BaseModule
        private readonly List<SealedDoor> _sealedDoorBuffer = new List<SealedDoor>(2);

        // COLD ALLOC: Collider[32] - resync interior occupants on enable/load/spawn - owner: BaseModule
        private readonly Collider[] _interiorOverlapBuffer = new Collider[INTERIOR_OVERLAP_CAPACITY];
        [SerializeField] private float _debugSolarEmpBlackoutSeconds;
        private float _solarEmpBlackoutRemainingSeconds;

        // ==========================================================
        //  PUBLIC PROPERTIES - ConstructionManager save/load
        // ==========================================================

        /// <summary>Maximum integrity, read-only.</summary>
        public float MaxIntegrity => maxIntegrity;
        internal static int ActiveModuleCount => s_activeModules.Count;
        internal static BaseModule GetActiveModuleAt(int index)
        {
            return index >= 0 && index < s_activeModules.Count ? s_activeModules[index] : null;
        }

        /// <summary>
        /// Tekuschaya tselostnost. ConstructionManager zapisyvaet syuda
        /// znachenie pri zagruzke sohraneniya.
        /// </summary>
        public float CurrentIntegrity
        {
            get => _integrityComponent.CurrentIntegrity;
            set => _integrityComponent.SetCurrentIntegrity(value);
        }

        /// <summary>
        /// Flag zatopleniya. ConstructionManager zapisyvaet syuda
        /// znachenie pri zagruzke sohraneniya.
        /// </summary>
        public bool IsFlooded
        {
            get => _integrityComponent.IsFlooded;
            set
            {
                _integrityComponent.SetFlooded(value);
                SyncWaterVolumeToFloodFlag(value);
            }
        }

        /// <summary>Integrity reached zero; module is breached.</summary>
        public bool IsBreached => _integrityComponent.CurrentIntegrity <= 0f;

        /// <summary>Water drain is currently active.</summary>
        public bool IsDraining => _integrityComponent.IsDraining;

        /// <summary>Deconstruction is currently active; blocks repeated calls.</summary>
        public bool IsDeconstructing => _isDeconstructing;

        /// <summary>Tekuschiy kaskadnyy avariynyy status modulya.</summary>
        public BaseModuleFailureMode CurrentFailureMode => _integrityComponent.FailureMode;

        /// <summary>Modul nahoditsya v avariynom kaskadnom sostoyanii.</summary>
        public bool HasCascadeFailure => _integrityComponent.FailureMode != BaseModuleFailureMode.None;
        public int CascadeNotificationMissCount => _cascadeNotificationMissCount;

        /// <summary>Current repair ceiling after accumulated material fatigue.</summary>
        public float MaxRecoverableIntegrity => _integrityComponent.MaxRecoverableIntegrity;
        /// <summary>Estimated catastrophic repair cycles remaining before the module reaches its minimum recoverable ceiling. -1 means the cap is not authored.</summary>
        public int RemainingRepairCycles => _integrityComponent.ResolveRemainingRepairCycles();
        /// <summary>Optional immutable template that owns abandoned-module integrity authoring and VFX sockets.</summary>
        public BaseModuleTemplate ModuleTemplate => moduleTemplate;

        internal int CachedModuleHashId
        {
            get
            {
                if (_moduleMarker != null && _moduleMarker.Data != null)
                    return _moduleMarker.Data.ModuleHashId;

                // Route through the template's own resolver rather than re-deriving the hash here.
                // Hashing PersistentId locally is a second derivation of the same identity, and it
                // disagrees with the baked templateHashId that HabitatConstructionManager,
                // HabitatGraphManager and BaseModuleCatalogRuntime resolve for the same module - so
                // the telemetry hash below would attribute a warning to a module id nothing else uses.
                return moduleTemplate != null
                    ? moduleTemplate.ResolvePersistentHashId()
                    : 0;
            }
        }

        internal string CachedModuleDisplayName
        {
            get
            {
                if (_moduleMarker != null &&
                    _moduleMarker.Data != null &&
                    !string.IsNullOrWhiteSpace(_moduleMarker.Data.moduleName))
                {
                    return _moduleMarker.Data.moduleName;
                }

                return null;
            }
        }

        /// <summary>Discrete integrity state derived from flood, breach, and abandonment thresholds.</summary>
        public BaseModuleIntegrityState IntegrityState
        {
            get
            {
                if (IsBreached)
                    return BaseModuleIntegrityState.Ruptured;

                if (_integrityComponent.IsFlooded)
                    return BaseModuleIntegrityState.Flooded;

                float normalizedIntegrity = IntegrityStateNormalized;
                if (normalizedIntegrity <= 0.4f || _integrityComponent.FailureMode != BaseModuleFailureMode.None)
                    return BaseModuleIntegrityState.Abandoned;

                return BaseModuleIntegrityState.Pristine;
            }
        }
        /// <summary>Normalized module integrity in the [0..1] range.</summary>
        public float IntegrityStateNormalized => _integrityComponent.MaxIntegrity > 0.01f
            ? Mathf.Clamp01(_integrityComponent.CurrentIntegrity / _integrityComponent.MaxIntegrity)
            : 0f;
        /// <summary>Normalized breathable reserve available for dry-zone life support.</summary>
        public float AirReserveNormalized => _lifeSupportComponent.AirReserveNormalized;
        /// <summary>Normalized room flood fill currently driving local module visuals.</summary>
        public float FloodLevel01 => _cachedFloodLevel01;
        public float WaterVolumeM3 => float.IsFinite(waterVolumeM3)
            ? Mathf.Clamp(waterVolumeM3, 0f, ResolveFloodCapacityM3())
            : 0f;
        public int FatigueDamage => Mathf.Max(0, fatigueDamage);
        /// <summary>Normalized cumulative pressure stress on sealed airlock bulkheads.</summary>
        public float BulkheadFloodStress01 => _bulkheadFloodStress01;
        /// <summary>Impact audio material exposed to the global physics impact router.</summary>
        public byte ImpactAudioMaterialId => _interiorReefInfestationActive ? floodedReefAudioMaterialId : (byte)ItemAudioMaterialId.Metal;
        /// <summary>True when the player is currently inside this module's interior volume.</summary>
        public bool IsPlayerInsideInterior => _trackedPlayerSurvival != null;
        /// <summary>True when breathable reserve has degraded into a stale-air window.</summary>
        public bool IsAirQualityLow => _lifeSupportComponent.IsAirQualityLow;
        /// <summary>Normalized CO2 saturation inside the module loop.</summary>
        public float Co2Normalized => _lifeSupportComponent.Co2Normalized;
        /// <summary>Active power supply ratio for this module's current grid connection.</summary>
        public float PowerSupplyRatio
        {
            get
            {
                if (!HasOperationalPower)
                    return 0f;

                return CachedPowerSupplyRatio;
            }
        }
        /// <summary>True when CO2 saturation has reached the life-support lockout threshold.</summary>
        public bool IsCo2Critical => _lifeSupportComponent.IsCo2Critical;
        /// <summary>True when CO2 saturation has crossed the toxic dry-room threshold.</summary>
        public bool IsCo2Toxic => _lifeSupportComponent.IsCo2Toxic;
        /// <summary>Normalized dry-room toxicity hazard intensity derived from local CO2 saturation.</summary>
        public float Co2ToxicHazardIntensity => _lifeSupportComponent.ToxicHazardIntensity;
        /// <summary>Continuous flooded-time accumulator used by construction save/load.</summary>
        public float FloodedReefFloodSeconds => _floodedReefFloodSeconds;
        /// <summary>True after flooded interior reef growth has latched for this module.</summary>
        public bool InteriorReefInfestationActive => _interiorReefInfestationActive;
        /// <summary>True when the habitat graph has cut this module off from every anchor.</summary>
        public bool IsUnmoored => _isUnmoored;
        /// <summary>True after catastrophic pressure implosion has latched for this module.</summary>
        public bool HasImploded => _implosionTriggered;
        public bool IsDetachedDebris => _detachedAsDebris;
        public bool CondensationActive => _condensationActive;
        internal float BreathableReserve => _lifeSupportComponent.BreathableReserve;
        internal float BreathableReserveCapacity => _lifeSupportComponent.BreathableReserveCapacity;
        internal float PressureCompressionVolumeScale => _pressureCompressionVolumeScale;
        internal float PressureCompressionDepthMeters => _pressureCompressionDepthMeters;
        internal float PressureCompressionAlpha01
        {
            get
            {
                float maximumAxisLoss = Mathf.Max(0.000001f, Mathf.Clamp(maximumDeepCompressionAxisLoss, 0f, 0.01f));
                float axisLoss = 1f - _pressureCompressionAxisScale;
                return Mathf.Clamp01(axisLoss / maximumAxisLoss);
            }
        }
        internal float JointShearStress01 => _jointShearStress01;
        internal bool IsEmergencyBulkheadLockedDown => _emergencyBulkheadLockedDown;
        internal bool IsGraphBreachIngressSource => IsBreached ||
                                                    IntegrityState == BaseModuleIntegrityState.Ruptured ||
                                                    _breachLatched;
        internal int AttachedParasiteCount => _attachedParasiteCount;
        internal float ParasiteRootPowerDrainWatts => _parasiteRootPowerDrainWatts;
        internal float ParasiteAddedMassKilograms => _parasiteAddedMassKilograms;
        internal float ParasiteThermalInsulation01 => _parasiteThermalInsulation01;
        internal float ParasiteBioReactorOverheatMultiplier => _parasiteBioReactorOverheatMultiplier;
        internal float PowerRatingForHabitatGraph => StaticDebuffedPowerRating;
        internal PowerGrid CachedPowerGrid => _powerNode != null ? _powerNode.Grid : null;

        private float StaticDebuffedPowerRating
        {
            get
            {
                if (_interiorReefInfestationActive)
                    return 0f;

                float generationWatts = Mathf.Max(0f, _basePowerRating) + _cultivationLightingPowerCreditWatts;
                float consumptionWatts = Mathf.Max(0f, -_basePowerRating) +
                                         ResolveFloodPumpPowerDraw() +
                                         _parasitePowerDrainWatts +
                                         _cultivationScrubberPowerDrainWatts;

                if (HasAttachedParasitePowerDebuff())
                    consumptionWatts *= ParasiteAttachedPowerConsumptionScalar;

                return generationWatts - consumptionWatts;
            }
        }

        private bool HasAttachedParasitePowerDebuff()
        {
            return _attachedParasiteCount > 0 ||
                   _parasiteInfectionLevel > 0.001f ||
                   _parasiteRootInfectionLevel > 0.001f ||
                   _parasiteRootPowerDrainWatts > 0.01f;
        }

        internal float CachedPowerSupplyRatio
        {
            get
            {
                if (!HasOperationalPower)
                    return 0f;

                PowerGrid grid = CachedPowerGrid;
                return grid != null ? Mathf.Clamp01(grid.SupplyRatio) : (_hasPower ? 1f : 0f);
            }
        }
        internal HectonVoxelVolume CachedVoxelVolume => _voxelVolume;

        // ----------------------------------------------------------
        //  IPowerComponent
        // ----------------------------------------------------------

        /// <summary>
        /// Base module power draw.
        /// Source: BuildableData.powerRating, then fallback.
        /// </summary>
        public float PowerRating => StaticDebuffedPowerRating;

        public int PowerPriority => powerPriority;

        public bool HasPower => HasOperationalPower;

        public float Voltage01 => Clamp01Finite(_ambientVoltageSupplyRatio, 1f);

        public void OnVoltageChanged(float voltage01)
        {
            float sanitizedVoltage = Clamp01Finite(voltage01, 1f);
            bool brownedOut = sanitizedVoltage < Clamp01Finite(brownoutActivationVoltageRatio, 0.80f);
            SetAmbientPowerVisualState(brownedOut, sanitizedVoltage);
        }

        /// <summary>
        /// Reacts to power status changes from PowerGrid:
        ///   - Lights enable or disable.
        ///   - Drain starts or stops.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            if (_hasPower == hasPower)
                return;

            _hasPower = hasPower;
            _debugHasPower = hasPower;

            QueueLightsEnabled(ShouldLightsBeEnabled());

            if (!HasOperationalPower)
            {
                _integrityComponent.StopDrain();
            }
            else
            {
                _integrityComponent.TryStartDrain(_hasPower);
            }

            UpdateDrainDiagnostics();
            SyncSpatialRole();
            UpdateOxygenScrubberHumTarget();
            AdvanceBrownoutShaderState(0f);
            QueueActiveModuleWaterLevelsShaderUpload(true);
            UpdateAmbienceTickRegistration();
        }

        // ----------------------------------------------------------
        //  ICuttable
        // ----------------------------------------------------------

        /// <summary>
        /// ICuttable bridge into ApplyDamage.
        /// Allows LaserCutter to damage base modules.
        /// hitPoint is retained for localized damage.
        /// </summary>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            ApplyDamageInternal(damage, true, transform.InverseTransformPoint(hitPoint));
        }

        // ----------------------------------------------------------
        //  IPoolable
        // ----------------------------------------------------------

        public void OnSpawn()
        {
            CacheReferences();
            ReadBuildablePower();
            ConfigureRuntimeComponentsFromSerializedState();
            InitializeAmbienceNoiseSeed();
            ResetOxygenScrubberHumRuntime(true);
            ConfigureOxygenScrubberHumSource();
            _isDeconstructing = false;
            _ambientLightsBrownedOut = false;
            _ambientVoltageSupplyRatio = 1f;
            _currentBrownoutFlicker01 = 1f;
            _brownoutTransition01 = 0f;
            _brownoutTransitionTarget01 = 0f;
            _ruptureGroanNoisePhase = 0f;
            _ruptureGroanPreviousNoise = -1f;
            _breachLatched = IsBreached;
            _cachedAtmosphereRoomIndex = -1;
            _hasBreachCenterOfMassTarget = false;
            _cachedFloodLevel01 = 0f;
            _bulkheadFloodStress01 = 0f;
            _jointShearStress01 = 0f;
            _jointShearGroanCooldownRemainingSeconds = 0f;
            _bulkheadFailureLatched = false;
            _ruptureCascadeFailureQueued = false;
            _emergencyBulkheadLockedDown = false;
            _implosionTriggered = false;
            _trackedPlayerMovement = null;
            _trackedPlayerHypoxiaPresentation = null;
            _cultivationScrubberPowerDrainWatts = 0f;
            _cultivationLightingPowerCreditWatts = 0f;
            _parasiteAddedMassKilograms = 0f;
            _parasiteThermalInsulation01 = 0f;
            _parasiteBioReactorOverheatMultiplier = 1f;
            _floodedReefFloodSeconds = Mathf.Max(0f, _floodedReefFloodSeconds);
            _solarEmpBlackoutRemainingSeconds = 0f;
            _debugSolarEmpBlackoutSeconds = 0f;
            ClearQueuedHydroStructuralLoad();
            ApplyDeepSeaCompressionState(true);

            RefreshVisualStateImmediate();
            SetInteriorReefVisualActive(_interiorReefInfestationActive);
            ApplyCondensationVisualState(_condensationActive);
            InteractableRegistry.RegisterTree(this);
            if (_interiorReefInfestationActive)
                RegisterFloodedReefFaunaAnchor();
            ResyncInteriorOccupants(true);
            _integrityComponent.TryStartDrain(_hasPower);
            UpdateDrainDiagnostics();
            SyncSpatialRole();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
            BaseDegradationSystem.SynchronizeParasiteSporeHazard(this);
            if (_isUnmoored)
            {
                EnableUnmooredPhysics();
                TryRegisterFixedTick();
            }
        }

        public void OnDespawn()
        {
            InteractableRegistry.InvalidateTree(this);
            NotifyModuleExitIfNeeded();
            StopDrain();
            SetLeakActive(false);
            SetFloodedVisual(false);
            ResetOxygenScrubberHumRuntime(true);
            _ambientLightsBrownedOut = false;
            _brownoutTransition01 = 0f;
            _brownoutTransitionTarget01 = 0f;
            _ruptureGroanNoisePhase = 0f;
            _ruptureGroanPreviousNoise = -1f;
            SetDeconstructionPreview(false);
            QueueLightsEnabled(true);

            _isDeconstructing = false;
            _breachLatched = false;
            _cachedAtmosphereRoomIndex = -1;
            _hasBreachCenterOfMassTarget = false;
            _cachedFloodLevel01 = 0f;
            _bulkheadFloodStress01 = 0f;
            _jointShearStress01 = 0f;
            _jointShearGroanCooldownRemainingSeconds = 0f;
            _bulkheadFailureLatched = false;
            _ruptureCascadeFailureQueued = false;
            _emergencyBulkheadLockedDown = false;
            _implosionTriggered = false;
            ClearQueuedHydroStructuralLoad();
            _trackedPlayerSurvival = null;
            _trackedPlayerMovement = null;
            _trackedPlayerHypoxiaPresentation = null;
            _lifeSupportComponent.ClearTrackedSurvivalCold();
            _parasitePowerDrainWatts = 0f;
            _parasiteRootPowerDrainWatts = 0f;
            _solarEmpBlackoutRemainingSeconds = 0f;
            _debugSolarEmpBlackoutSeconds = 0f;
            _parasiteAddedMassKilograms = 0f;
            _parasiteThermalInsulation01 = 0f;
            _parasiteBioReactorOverheatMultiplier = 1f;
            _parasiteInfectionLevel = 0f;
            _parasiteRootInfectionLevel = 0f;
            _attachedParasiteCount = 0;
            _cultivationScrubberPowerDrainWatts = 0f;
            _cultivationLightingPowerCreditWatts = 0f;
            _floodedReefFloodSeconds = 0f;
            UnregisterFloodedReefFaunaAnchor();
            _interiorReefInfestationActive = false;
            waterVolumeM3 = 0f;
            fatigueDamage = 0;
            _detachedAsDebris = false;
            _carbonFilterAvailable = true;
            _carbonFilterTimerSeconds = 0f;
            _condensationActive = false;
            ApplyCondensationVisualState(false);
            SetInteriorReefVisualActive(false);
            ResetPressureCompressionVisualState();
            FlushInteriorReefVisualState();
            FlushPressureCompressionVisualState();
            _integrityComponent.ResetForDespawn();
            _lifeSupportComponent.ResetForDespawn();
            TryUnregisterFixedTick();
            DisableUnmooredPhysics();
            SyncSpatialRole();
            BaseDegradationSystem.ClearIntegrityState(this);
            BaseDegradationSystem.ClearParasiteSporeHazard(this);
            Hecton8.Construction.BaseDegradationSystem.ClearParasiteStructuralState(this);
            BaseDegradationSystem.ClearPressureCompressionState(this);

            ReleaseAllTrackedObjects();
        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        /// <summary>
        /// Central slow tick from GameTickManager.
        /// Runs passive repair when powered and advances water drain.
        /// No power means no operational work.
        /// </summary>
        public void SlowTick()
        {
            UpdateInteriorOccupancyFromPlayerRuntime();
            ApplyCascadeFailureEffects();
            ApplyDeepSeaCompressionState(false);
            UpdateLifeSupport(SLOW_TICK_DT);
            UpdateFloodVisualStateImmediate();
            UpdateFloodedReefGrowth(SLOW_TICK_DT);
            TryApplyFloodShortCircuit();
            ApplyLocalGravityAnomalyRequest();
            EvaluateCatastrophicImplosion();
            AdvanceSolarEmpBlackout(SLOW_TICK_DT);
            EvaluateRuptureGroanAudio(SLOW_TICK_DT);
            if (!HasOperationalPower)
                return;

            float repairCap = GetRepairIntegrityCap();

            if (passiveRecoveryRate > 0f &&
                _integrityComponent.FailureMode == BaseModuleFailureMode.None &&
                _integrityComponent.CurrentIntegrity > 0f &&
                _integrityComponent.CurrentIntegrity < repairCap)
            {
                Repair(passiveRecoveryRate * SLOW_TICK_DT);
            }

            // Passive degradation: pressure, time, depth.
            if (passiveDegradationRate > 0f && _integrityComponent.CurrentIntegrity > 0f)
            {
                float degradation = passiveDegradationRate * SLOW_TICK_DT;

                // Depth above 500m increases pressure degradation.
                if (_trackedPlayerSurvival != null && _trackedPlayerSurvival.Depth > 500f)
                    degradation *= depthDegradationMultiplier;

                ApplyDamage(degradation);
            }

            if (!_integrityComponent.IsDraining)
                return;

            if (_integrityComponent.AdvanceDrain(SLOW_TICK_DT))
                ForceDrainComplete();
            UpdateDrainDiagnostics();
        }

        /// <summary>
        /// Fixed-step unmoored buoyancy and flooding tilt evaluation.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_isUnmoored || fixedDeltaTime <= 0f)
                return;

            if (_moduleRigidbody == null)
                return;

            ApplyQueuedHydroStructuralLoad(fixedDeltaTime);

            float floodFill01 = ResolveUnmooredFloodFillNormalized();
            float displacementVolume = ResolveBuoyancyDisplacementVolumeCubicMeters();
            float dryMass = ResolveDryMassKilograms();
            float parasiteMass = ResolveParasiteAddedMassKilograms();
            float effectiveMass = math.max(
                MinimumMassKilograms,
                dryMass + parasiteMass + (floodFill01 * displacementVolume * SeawaterDensityKilogramsPerCubicMeter));
            if (math.abs(_moduleRigidbody.mass - effectiveMass) >= BuoyancyMassUpdateThresholdKilograms)
                _moduleRigidbody.mass = effectiveMass;

            float retainedAirMassEquivalent = displacementVolume * (1f - floodFill01) * SeawaterDensityKilogramsPerCubicMeter;
            float netAccelerationY = ((retainedAirMassEquivalent - effectiveMass) / effectiveMass) * GravityAccelerationMetersPerSecondSquared;
            float maximumAcceleration = ResolveMaximumUnmooredAccelerationMetersPerSecondSquared();
            float externalDepthMeters = ResolveExternalDepthMeters();
            if (floodFill01 > 0.5f && externalDepthMeters > hullCrushDepthMeters)
            {
                float crushRatio = math.saturate((externalDepthMeters - hullCrushDepthMeters) / 1000f);
                netAccelerationY -= maximumAcceleration * crushRatio;
            }

            netAccelerationY = math.clamp(netAccelerationY, -maximumAcceleration, maximumAcceleration);
            if (math.abs(netAccelerationY) > 0.0001f)
            {
                _cachedPhysicsService?.QueueForceAtPosition(
                    _moduleRigidbody,
                    Vector3.up * netAccelerationY,
                    ResolveModuleFallbackWorldPosition(),
                    ForceMode.Acceleration);
            }

            ApplyFloodVisualLean(fixedDeltaTime, floodFill01);
        }

        /// <summary>
        /// Frame-rate ambience only. Registered while brownout flicker or scrubber hum fade is active.
        /// </summary>
        public void Tick(float deltaTime)
        {
            float dt = math.max(0f, deltaTime);
            bool shaderStateChanged = false;
            if (ShouldAdvanceBrownoutShaderState())
                shaderStateChanged = AdvanceBrownoutShaderState(dt);

            UpdateOxygenScrubberHum(dt);
            if (shaderStateChanged)
                QueueActiveModuleWaterLevelsShaderUpload(true);

            UpdateAmbienceTickRegistration();
        }

        public void LateFrameTick()
        {
            FlushPendingSpatialSfx();
            FlushLeakVisualState();
            FlushFloodVisualState();
            FlushOxygenScrubberHumVisualState();
            FlushPressureCompressionVisualState();
            FlushInteriorReefVisualState();
            FlushParasiteSporeVfxState();
            FlushPendingInteriorLightState();
            if (s_moduleWaterLevelShaderDirty)
            {
                PublishActiveModuleWaterLevelsToShader(true);
                s_moduleWaterLevelShaderDirty = false;
            }
        }

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            CacheReferences();
            CacheRegistryServicesCold();
            TryRouteAudioSourceToSfxGroup(audioSource);
            ReadBuildablePower();
            ValidateInteriorTrigger();
            ConfigureRuntimeComponentsFromSerializedState();
            if (isFlooded && waterVolumeM3 <= 0.0001f)
                SetWaterVolumeM3(ResolveFloodCapacityM3());

            _wasFlooded = _integrityComponent.IsFlooded;
            CacheOwnedBulkheadComponents();
            CaptureModuleRigidbodyDefaults();
            CaptureFloodSurfaceDefaults();
            CapturePressureCompressionDefaults();
            InitializeAmbienceNoiseSeed();
            ConfigureOxygenScrubberHumSource();
            ApplyCondensationVisualState(_condensationActive);
        }

        private void OnEnable()
        {
            if (!s_activeModules.Contains(this))
                s_activeModules.Add(this);

            CacheRegistryServicesCold();
            TryRouteAudioSourceToSfxGroup(audioSource);
            TryRouteAudioSourceToSfxGroup(oxygenScrubberHumSource);
            TryRegisterHotSwapListener();
            LaserCutterTargetRegistry.RegisterModuleTree(this);
            InteractableRegistry.RegisterTree(this);
            TryRegisterElectromagneticPulseListener();
            TryRegister();
            TryRegisterLateFrameTick();
            ResyncInteriorOccupants(true);
            BaseDegradationSystem.SynchronizeIntegrityState(this);
            if (_isUnmoored)
            {
                EnableUnmooredPhysics();
                TryRegisterFixedTick();
            }

            ApplyDeepSeaCompressionState(true);
            UpdateFloodVisualStateImmediate();
            SetInteriorReefVisualActive(_interiorReefInfestationActive);
            ApplyCondensationVisualState(_condensationActive);
            UpdateOxygenScrubberHumTarget();
            UpdateAmbienceTickRegistration();
            PublishActiveModuleWaterLevelsToShader(true);
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            LaserCutterTargetRegistry.UnregisterModuleTree(this);
            TryUnregisterHotSwapListener();
            TryUnregisterElectromagneticPulseListener();
            ClearCachedRegistryServices();
            TryUnregisterUpdatable();
            TryUnregisterLateFrameTick();
            ResetBrownoutShaderState();
            ResetOxygenScrubberHumRuntime(false);
            s_activeModules.Remove(this);
            TryUnregister();
            TryUnregisterFixedTick();
            BaseDegradationSystem.ClearIntegrityState(this);
            BaseDegradationSystem.ClearParasiteSporeHazard(this);
            Hecton8.Construction.BaseDegradationSystem.ClearParasiteStructuralState(this);
            BaseDegradationSystem.ClearPressureCompressionState(this);

            NotifyModuleExitIfNeeded();
            ReleaseAllTrackedObjects();
            _cachedFloodLevel01 = 0f;
            ClearQueuedHydroStructuralLoad();
            ClearCascadeNotificationDiagnostics();
            PublishActiveModuleWaterLevelsToShader(true);
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            LaserCutterTargetRegistry.UnregisterModuleTree(this);
            TryUnregisterHotSwapListener();
            TryUnregisterElectromagneticPulseListener();
            ClearCachedRegistryServices();
            TryUnregisterUpdatable();
            TryUnregisterLateFrameTick();
            ResetBrownoutShaderState();
            ResetOxygenScrubberHumRuntime(true);
            s_activeModules.Remove(this);
            TryUnregister();
            TryUnregisterFixedTick();
            BaseDegradationSystem.ClearIntegrityState(this);
            BaseDegradationSystem.ClearParasiteSporeHazard(this);
            Hecton8.Construction.BaseDegradationSystem.ClearParasiteStructuralState(this);
            BaseDegradationSystem.ClearPressureCompressionState(this);
            ClearCascadeNotificationDiagnostics();
            PublishActiveModuleWaterLevelsToShader(true);
        }

        // ----------------------------------------------------------
        //  INTERIOR ZONE � TRIGGER CALLBACKS
        // ----------------------------------------------------------

        private void UpdateInteriorOccupancyFromPlayerRuntime()
        {
            IPlayerRuntimeContext playerRuntime = _cachedPlayerRuntime;
            if (playerRuntime == null)
                return;

            Vector3 playerPosition;
            Transform playerTransform = null;
            if (playerRuntime.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose))
            {
                float3 runtimePosition = pose.RuntimePosition;
                playerPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            }
            else
            {
                playerTransform = playerRuntime.PlayerTransform;
                if (playerTransform == null)
                    return;

                playerPosition = playerTransform.position;
            }

            if (!TryContainsInteriorRuntimePoint(playerPosition))
            {
                NotifyModuleExitIfNeeded();
                return;
            }

            if (_trackedPlayerSurvival != null)
                return;

            HectonSurvivalSystem survival = playerRuntime.SurvivalSystem;
            if (survival == null)
                return;

            playerTransform ??= playerRuntime.PlayerTransform;
            TrackPlayerFromRuntime(playerRuntime, survival, playerTransform, true);
        }

        private void HandleInteriorTriggerEnterLegacyDisabled(Collider other)
        {
            if (other == null) return;

            // -- Life Support: detect player entry --
            // CompareTag is zero GC (no string allocation).
            // Player occupancy remains here; water authority is scalar CSR/Vault state.
            TryTrackPlayer(other, true);
        }

        private void HandleInteriorTriggerExitLegacyDisabled(Collider other)
        {
            if (other == null) return;

            // -- Life Support: detect player exit --
            bool trackedPlayerExited = IsTrackedPlayerCollider(other);
            if (trackedPlayerExited)
            {
                _trackedPlayerSurvival = null;
                _trackedPlayerMovement = null;
                _trackedPlayerHypoxiaPresentation = null;
                _lifeSupportComponent.ClearTrackedSurvivalCold();
            }

            if (trackedPlayerExited)
            {
                ResyncInteriorOccupants(false);
                if (_trackedPlayerSurvival == null)
                {
                    ModuleStatusEvents.TryNotifyExit(this);
                    PublishPlayerBaseTransitionSignal(false);
                }
            }
        }

        // ----------------------------------------------------------
        //  PUBLIC GAMEPLAY API
        // ----------------------------------------------------------

        /// <summary>
        /// Nanosit uron modulyu.
        /// Pri dostizhenii 0 � modul probit i zataplivaetsya.
        /// </summary>
        public void ApplyDamage(float amount)
        {
            ApplyDamageInternal(amount, false, Vector3.zero);
        }

        private void ApplyDamageInternal(float amount, bool hasBreachLocalPointOverride, Vector3 breachLocalPointOverride)
        {
            float previousIntegrityNormalized = _integrityComponent.MaxIntegrity > 0.01f
                ? Mathf.Clamp01(_integrityComponent.CurrentIntegrity / _integrityComponent.MaxIntegrity)
                : 0f;
            bool wasBreached = _integrityComponent.CurrentIntegrity <= 0f;
            ModuleDamageOutcome outcome = _integrityComponent.ApplyDamage(amount);
            if (outcome == ModuleDamageOutcome.None)
                return;

            float nextIntegrityNormalized = _integrityComponent.MaxIntegrity > 0.01f
                ? Mathf.Clamp01(_integrityComponent.CurrentIntegrity / _integrityComponent.MaxIntegrity)
                : 0f;

            if (outcome == ModuleDamageOutcome.Catastrophic)
            {
                TriggerCascadeFailure();
            }
            else
            {
                SetLeakActive(ShouldLeakBeActive());
            }

            if (_habitatIntegrityManager != null)
            {
                uint damageType = _integrityComponent.FailureMode == BaseModuleFailureMode.Fire
                    ? (uint)DamageTypeMask.Thermal
                    : (uint)DamageTypeMask.Pressure;
                HabitatDamageSignal signal = default;
                signal.magnitude = Mathf.Max(0f, amount);
                signal.localPoint = hasBreachLocalPointOverride
                    ? new Unity.Mathematics.float3(breachLocalPointOverride.x, breachLocalPointOverride.y, breachLocalPointOverride.z)
                    : new Unity.Mathematics.float3(0f, 0f, 0f);
                signal.damageType = damageType;
                signal.integrityDelta = (byte)Mathf.Clamp(
                    Mathf.RoundToInt(Mathf.Abs(nextIntegrityNormalized - previousIntegrityNormalized) * byte.MaxValue),
                    0,
                    byte.MaxValue);
                signal.sourceID = DamageSourceIds.HabitatIntegrity;

                _habitatIntegrityManager.DispatchIntegrityChanged(previousIntegrityNormalized, nextIntegrityNormalized, signal);
                _habitatIntegrityManager.DispatchClarityChanged(
                    0f,
                    Mathf.Clamp01(Mathf.Max(Mathf.Abs(nextIntegrityNormalized - previousIntegrityNormalized), amount * 0.01f)),
                    signal);

                if (outcome == ModuleDamageOutcome.Catastrophic)
                    _habitatIntegrityManager.DispatchTraumaThresholdCrossed(TraumaLevel.Catastrophic);
                else if (nextIntegrityNormalized < 0.4f)
                    _habitatIntegrityManager.DispatchTraumaThresholdCrossed(TraumaLevel.Critical);
                else if (nextIntegrityNormalized < 0.65f)
                    _habitatIntegrityManager.DispatchTraumaThresholdCrossed(TraumaLevel.Significant);
                else
                    _habitatIntegrityManager.DispatchTraumaThresholdCrossed(TraumaLevel.Minor);
            }

            if (!wasBreached && _integrityComponent.CurrentIntegrity <= 0f)
            {
                Vector3 resolvedBreachLocalPoint = hasBreachLocalPointOverride
                    ? breachLocalPointOverride
                    : ResolveDefaultBreachLocalPoint();
                _breachCenterOfMassTargetLocal = resolvedBreachLocalPoint;
                _hasBreachCenterOfMassTarget = true;
                HandleIntegrityCollapse(resolvedBreachLocalPoint);
            }

            _integrityComponent.StopDrain();
            UpdateDrainDiagnostics();
            RefreshVisualStateImmediate();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        /// <summary>
        /// Remontiruet modul.
        /// Esli tselostnost polnostyu vosstanovlena i est pitanie �
        /// nachinaetsya otkachka vody.
        /// </summary>
        public void Repair(float amount)
        {
            ModuleRepairOutcome outcome = _integrityComponent.Repair(amount);
            if (outcome == ModuleRepairOutcome.None)
                return;

            if (outcome == ModuleRepairOutcome.FullyRestored)
            {
                BaseModuleFailureMode currentFailureMode = _integrityComponent.FailureMode;
                if (currentFailureMode == BaseModuleFailureMode.Fire ||
                    currentFailureMode == BaseModuleFailureMode.ShortCircuit)
                {
                    ClearCascadeFailure();
                }

                SetLeakActive(ShouldLeakBeActive());
                _integrityComponent.TryStartDrain(_hasPower);
            }
            else
            {
                SetLeakActive(ShouldLeakBeActive());
            }

            UpdateDrainDiagnostics();
            RefreshVisualStateImmediate();
            if (_integrityComponent.CurrentIntegrity > 0f)
            {
                _breachLatched = false;
                NotifyEmergencyLockdownStateChanged();
            }
            BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        bool Hecton8.Interaction.IRepairableModuleTarget.TryReadRepairState(out Hecton8.Interaction.ModuleRepairReadSnapshot snapshot)
        {
            snapshot = default;
            snapshot.CurrentIntegrity = _integrityComponent.CurrentIntegrity;
            snapshot.MaxIntegrity = _integrityComponent.MaxIntegrity;
            uint flags = 0u;
            if (_integrityComponent.IsFlooded)
                flags |= Hecton8.Interaction.ModuleRepairReadSnapshot.FlagFlooded;
            if (_integrityComponent.IsDraining)
                flags |= Hecton8.Interaction.ModuleRepairReadSnapshot.FlagDraining;
            if (HasOperationalPower)
                flags |= Hecton8.Interaction.ModuleRepairReadSnapshot.FlagHasPower;

            snapshot.Flags = flags;
            return true;
        }

        void Hecton8.Interaction.IRepairableModuleTarget.ApplyRepair(float amount)
        {
            Repair(amount);
        }

        /// <summary>
        /// Prinuditelnoe zatoplenie. Ostanavlivaet drain, aktiviruet vizual.
        /// </summary>
        public void ForceFlood()
        {
            bool wasFlooded = _integrityComponent.IsFlooded;
            SetWaterVolumeM3(ResolveFloodCapacityM3());
            _integrityComponent.ForceFlood();
            UpdateDrainDiagnostics();
            SetFloodedVisual(true);
            SyncTrackedObjectsFloodState();
            SyncSpatialRole();
            bool shortCircuitTriggered = TryApplyFloodShortCircuit();
            if (!shortCircuitTriggered)
                PlaySpatialSfx(floodClip);
            if (!wasFlooded && !shortCircuitTriggered)
                NotifyEmergencyLockdownStateChanged();
            if (!shortCircuitTriggered)
                BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        internal void ForceFloodFromBulkheadOverride(Vector3 breachWorldPoint)
        {
            Vector3 localBreachPoint = SetBreachVisualAnchor(breachWorldPoint);
            ForceFlood();
            TriggerBreachDepressurizationVortex(localBreachPoint);
        }

        internal Vector3 SetBreachVisualAnchor(Vector3 breachWorldPoint)
        {
            Vector3 localBreachPoint = IsFiniteVector(breachWorldPoint)
                ? transform.InverseTransformPoint(breachWorldPoint)
                : ResolveDefaultBreachLocalPoint();
            _breachCenterOfMassTargetLocal = localBreachPoint;
            _hasBreachCenterOfMassTarget = true;
            return localBreachPoint;
        }

        /// <summary>
        /// Prinuditelnoe zavershenie osusheniya. Sbrasyvaet drain state i vizual.
        /// </summary>
        public void ForceDrainComplete()
        {
            bool wasFlooded = _integrityComponent.IsFlooded || waterVolumeM3 > 0.001f;
            SetWaterVolumeM3(0f);
            _integrityComponent.ForceDrainComplete(clearOxygenLeakFailure: true);
            if (wasFlooded)
                RegisterFloodDryFatigueCycle();
            ResetBulkheadFloodStress();
            UpdateDrainDiagnostics();
            SetFloodedVisual(false);
            SyncTrackedObjectsFloodState();
            SyncSpatialRole();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        /// <summary>
        /// Reduces sealed-bulkhead flood stress from repair drones or manual override tools.
        /// </summary>
        /// <param name="normalizedAmount">Normalized stress amount to remove from the [0..1] bulkhead stress accumulator.</param>
        public void RepairBulkheadStress(float normalizedAmount)
        {
            if (normalizedAmount <= 0f || !float.IsFinite(normalizedAmount))
                return;

            float previousStress = _bulkheadFloodStress01;
            _bulkheadFloodStress01 = Mathf.Max(0f, _bulkheadFloodStress01 - normalizedAmount);
            if (_bulkheadFloodStress01 <= 0f && !_integrityComponent.IsFlooded && !IsBreached)
                _bulkheadFailureLatched = false;
            if (Mathf.Abs(previousStress - _bulkheadFloodStress01) > 0.0001f)
                BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        internal float ResolveFloodWaterVolumeCubicMeters()
        {
            if (waterVolumeM3 > 0.0001f)
                return Mathf.Min(waterVolumeM3, ResolveFloodCapacityM3());

            float floodFill01 = ResolveRuntimeFloodLevel01();
            if (floodFill01 <= 0f)
                return 0f;

            float displacementVolume = ResolveBuoyancyDisplacementVolumeCubicMeters();
            return Mathf.Max(0f, displacementVolume * floodFill01);
        }

        internal float DrainWaterVolumeM3(float requestedVolumeM3)
        {
            if (requestedVolumeM3 <= 0f || !float.IsFinite(requestedVolumeM3) || waterVolumeM3 <= 0f)
                return 0f;

            bool wasFlooded = _integrityComponent.IsFlooded;
            float drained = Mathf.Min(waterVolumeM3, requestedVolumeM3);
            SetWaterVolumeM3(waterVolumeM3 - drained);
            if (waterVolumeM3 <= 0.001f)
            {
                _integrityComponent.ForceDrainComplete(clearOxygenLeakFailure: true);
                if (wasFlooded)
                    RegisterFloodDryFatigueCycle();
                ResetBulkheadFloodStress();
                SetFloodedVisual(false);
                SyncTrackedObjectsFloodState();
                SyncSpatialRole();
                NotifyEmergencyLockdownStateChanged();
                BaseDegradationSystem.SynchronizeIntegrityState(this);
            }
            else
            {
                _integrityComponent.ForceFlood();
                UpdateFloodVisualStateImmediate();
            }

            UpdateDrainDiagnostics();
            return drained;
        }

        internal float AddWaterVolumeM3(float requestedVolumeM3)
        {
            return AddWaterVolumeM3Internal(requestedVolumeM3, false, 0f);
        }

        internal float ApplyGraphPressureIngress(float deltaTime, float pressureRootKPa)
        {
            if (deltaTime <= 0f ||
                pressureRootKPa <= 0f ||
                !float.IsFinite(deltaTime) ||
                !float.IsFinite(pressureRootKPa) ||
                !IsGraphBreachIngressSource)
            {
                return 0f;
            }

            float deltaVolumeM3 = CalculatePressureDrivenIngressVolumeDeltaM3(
                pressureRootKPa,
                ResolveLeakHoleAreaSquareMeters(),
                deltaTime,
                breachPressureFlowCoefficient);
            if (deltaVolumeM3 <= 0f)
                return 0f;

            return AddWaterVolumeM3Internal(deltaVolumeM3, true, ResolveExternalDepthMeters());
        }

        internal float ResolveGraphBoyleAirPocketPressureAtm(float thermalPressureScale)
        {
            float capacityM3 = ResolveFloodCapacityM3();
            if (capacityM3 <= 0.001f || !float.IsFinite(capacityM3))
                return 1f;

            float remainingAirVolumeM3 = math.max(
                capacityM3 * AirPocketMinimumRemainingVolume01,
                capacityM3 - math.min(capacityM3, WaterVolumeM3));
            float pressureAtm = capacityM3 * math.rcp(remainingAirVolumeM3);
            if (!math.isfinite(pressureAtm))
                pressureAtm = 1f;

            float thermalScale = math.isfinite(thermalPressureScale)
                ? math.clamp(thermalPressureScale, 0.5f, 1.35f)
                : 1f;
            return math.max(1f, pressureAtm * thermalScale);
        }

        internal bool ApplyGraphAirPocketCompressionStress(float pressureAtm, float deltaTime)
        {
            if (pressureAtm <= AirPocketCrackPressureAtm ||
                deltaTime <= 0f ||
                !float.IsFinite(pressureAtm) ||
                !float.IsFinite(deltaTime))
            {
                return false;
            }

            float compressionDelta01 = math.saturate((pressureAtm - AirPocketCrackPressureAtm) * AirPocketCrackPressureInvRange);
            bool stressApplied = ApplyJointShearStress(compressionDelta01, deltaTime);
            if (stressApplied && TryConsumeJointShearGroanCooldown())
                EmitHullBreachJet(ResolveDefaultBreachLocalPoint(), pressureAtm - 1f);

            return stressApplied;
        }

        internal bool TryExtinguishFloodedFire()
        {
            return TryApplyFloodFireSuppression();
        }

        internal float ResolveFloodWaterMassKilograms()
        {
            float floodVolumeCubicMeters = ResolveFloodWaterVolumeCubicMeters();
            if (floodVolumeCubicMeters <= 0f)
                return 0f;

            float floodMassKilograms = floodVolumeCubicMeters * SeawaterDensityKilogramsPerCubicMeter;
            return float.IsFinite(floodMassKilograms) ? Mathf.Max(0f, floodMassKilograms) : 0f;
        }

        internal float ResolveFloodCapacityM3()
        {
            if (_cachedFloodCapacityM3 <= 0.001f || !float.IsFinite(_cachedFloodCapacityM3))
                RefreshFloodCapacityCache();

            return _cachedFloodCapacityM3;
        }

        private float ResolveInverseFloodCapacityM3()
        {
            if (_inverseFloodCapacityM3 <= 0f || !float.IsFinite(_inverseFloodCapacityM3))
                RefreshFloodCapacityCache();

            return _inverseFloodCapacityM3;
        }

        private void RefreshFloodCapacityCache()
        {
            _cachedFloodCapacityM3 = Mathf.Max(0.001f, ResolveBuoyancyDisplacementVolumeCubicMeters());
            _inverseFloodCapacityM3 = math.rcp(_cachedFloodCapacityM3);
            if (!float.IsFinite(waterVolumeM3) || waterVolumeM3 < 0f)
                waterVolumeM3 = 0f;
            else if (waterVolumeM3 > _cachedFloodCapacityM3)
                waterVolumeM3 = _cachedFloodCapacityM3;
        }

        internal float ResolveExternalPressureDeltaKPa()
        {
            return Mathf.Max(0f, ResolveHydrostaticPressureKPa(ResolveExternalDepthMeters()) - SurfacePressureKPa);
        }

        internal bool SetCarbonFilterAvailable(bool available)
        {
            if (_carbonFilterAvailable == available)
                return false;

            _carbonFilterAvailable = available;
            UpdateOxygenScrubberHumTarget();
            UpdateAmbienceTickRegistration();
            return true;
        }

        internal void UpdateCarbonFilterLogistics(float deltaTime, int itemHashId)
        {
            if (deltaTime <= 0f || !float.IsFinite(deltaTime))
                return;

            if (_integrityComponent.IsFlooded || _integrityComponent.FailureMode == BaseModuleFailureMode.Fire)
            {
                SetCarbonFilterAvailable(false);
                _carbonFilterTimerSeconds = 0f;
                return;
            }

            if (!HasOperationalPower)
            {
                SetCarbonFilterAvailable(false);
                return;
            }

            _carbonFilterTimerSeconds -= deltaTime;
            if (_carbonFilterAvailable && _carbonFilterTimerSeconds > 0f)
                return;

            PowerGrid grid = CachedPowerGrid;
            bool consumed = itemHashId != 0 && BaseLogisticsNetwork.TryConsumeAccessibleItem(grid, itemHashId, 1);
            SetCarbonFilterAvailable(consumed);
            _carbonFilterTimerSeconds = consumed
                ? Mathf.Max(1f, carbonFilterConsumptionIntervalSeconds)
                : Mathf.Min(5f, Mathf.Max(1f, carbonFilterConsumptionIntervalSeconds));
        }

        internal void SetCondensationState(bool active)
        {
            if (_condensationActive == active)
                return;

            _condensationActive = active;
            ApplyCondensationVisualState(active);
            BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        private void ApplyCondensationVisualState(bool active)
        {
            if (condensationSurfaces == null)
                return;

            int count = condensationSurfaces.Length;
            for (int i = 0; i < count; i++)
            {
                BaseModuleCondensationSurface surface = condensationSurfaces[i];
                if (surface != null)
                    surface.ApplyCondensation(active);
            }
        }

        internal float ResolveParasiteAddedMassKilograms()
        {
            return float.IsFinite(_parasiteAddedMassKilograms)
                ? Mathf.Max(0f, _parasiteAddedMassKilograms)
                : 0f;
        }

        internal void QueueHydroStructuralLoad(float floodWaterMassKilograms, Vector3 applicationWorldPoint, float durationSeconds)
        {
            if (!_isUnmoored ||
                floodWaterMassKilograms <= 0f ||
                durationSeconds <= 0f ||
                !float.IsFinite(floodWaterMassKilograms) ||
                !float.IsFinite(durationSeconds))
            {
                return;
            }

            if (!IsFiniteVector(applicationWorldPoint))
                applicationWorldPoint = ResolveModuleFallbackWorldPosition();

            float forceNewtons = floodWaterMassKilograms * GravityAccelerationMetersPerSecondSquared;
            forceNewtons = Mathf.Min(ResolveMaximumHydroStructuralLoadNewtons(), forceNewtons);
            if (forceNewtons <= 0f || !float.IsFinite(forceNewtons))
                return;

            _queuedHydroStructuralLoadNewtons = Mathf.Max(_queuedHydroStructuralLoadNewtons, forceNewtons);
            _queuedHydroStructuralLoadRemainingSeconds = Mathf.Max(_queuedHydroStructuralLoadRemainingSeconds, durationSeconds);
            _queuedHydroStructuralLoadPointWorld = applicationWorldPoint;
        }

        internal void DecayBulkheadFloodStress(float deltaTime)
        {
            if (deltaTime <= 0f || !float.IsFinite(deltaTime) || _bulkheadFloodStress01 <= 0f)
                return;

            if (_integrityComponent.IsFlooded || IsBreached)
                return;

            float recovery = ResolveBulkheadStressRecoveryPerSecond() * deltaTime;
            if (recovery <= 0f || !float.IsFinite(recovery))
                return;

            float previousStress = _bulkheadFloodStress01;
            _bulkheadFloodStress01 = Mathf.Max(0f, _bulkheadFloodStress01 - recovery);
            if (Mathf.Abs(previousStress - _bulkheadFloodStress01) > 0.0001f)
                BaseDegradationSystem.SynchronizeIntegrityState(this);
            if (_bulkheadFloodStress01 <= 0f)
                _bulkheadFailureLatched = false;
        }

        internal bool AccumulateBulkheadFloodStress(float opposingFloodWaterMassKilograms, float deltaTime)
        {
            if (opposingFloodWaterMassKilograms <= 0f ||
                deltaTime <= 0f ||
                !float.IsFinite(opposingFloodWaterMassKilograms) ||
                !float.IsFinite(deltaTime))
            {
                return false;
            }

            if (!_emergencyBulkheadLockedDown ||
                !ResolveEmergencyAirlockRole() ||
                _integrityComponent.IsFlooded ||
                IsBreached)
            {
                return false;
            }

            float failureMassKilograms = ResolveBulkheadFailureWaterMassKilograms();
            if (failureMassKilograms <= 0f)
                return false;

            float overloadRatio = Mathf.Clamp(opposingFloodWaterMassKilograms / failureMassKilograms, 0f, 4f);
            float stressDelta = overloadRatio * ResolveBulkheadStressRatePerSecond() * deltaTime;
            if (stressDelta <= 0f || !float.IsFinite(stressDelta))
                return false;

            float previousStress = _bulkheadFloodStress01;
            _bulkheadFloodStress01 = Mathf.Clamp01(_bulkheadFloodStress01 + stressDelta);
            if (Mathf.Abs(previousStress - _bulkheadFloodStress01) > 0.0001f)
                BaseDegradationSystem.SynchronizeIntegrityState(this);
            if (_bulkheadFloodStress01 < 1f || _bulkheadFailureLatched)
                return false;

            _bulkheadFailureLatched = true;
            ForceFlood();
            return true;
        }

        internal void DecayJointShearStress(float deltaTime)
        {
            if (deltaTime <= 0f || !float.IsFinite(deltaTime))
                return;

            if (_jointShearGroanCooldownRemainingSeconds > 0f)
                _jointShearGroanCooldownRemainingSeconds = Mathf.Max(0f, _jointShearGroanCooldownRemainingSeconds - deltaTime);

            if (_jointShearStress01 <= 0f)
                return;

            float recovery = Mathf.Max(0f, jointShearStressRecoveryPerSecond) * deltaTime;
            if (recovery <= 0f || !float.IsFinite(recovery))
                return;

            _jointShearStress01 = Mathf.Max(0f, _jointShearStress01 - recovery);
        }

        internal bool TryConsumeJointShearGroanCooldown()
        {
            if (_jointShearGroanCooldownRemainingSeconds > 0f)
                return false;

            _jointShearGroanCooldownRemainingSeconds = Mathf.Max(0.1f, jointShearGroanCooldownSeconds);
            return true;
        }

        internal bool ApplyJointShearStress(float compressionDelta01, float deltaTime)
        {
            if (compressionDelta01 <= 0f ||
                deltaTime <= 0f ||
                !float.IsFinite(compressionDelta01) ||
                !float.IsFinite(deltaTime) ||
                IsBreached)
            {
                return false;
            }

            float threshold = Mathf.Clamp01(jointShearCompressionDeltaThreshold);
            if (compressionDelta01 <= threshold)
                return false;

            float overload01 = Mathf.Clamp01((compressionDelta01 - threshold) / Mathf.Max(0.0001f, 1f - threshold));
            _jointShearStress01 = Mathf.Clamp01(Mathf.Max(_jointShearStress01, overload01));

            float damageFraction = Mathf.Max(0f, jointShearDamagePerSecondAtFullDelta) * overload01 * deltaTime;
            float damageAmount = damageFraction * Mathf.Max(1f, _integrityComponent.MaxIntegrity);
            if (damageAmount <= 0f || !float.IsFinite(damageAmount))
                return true;

            ApplyDamage(damageAmount);
            return true;
        }

        private void EvaluateRuptureGroanAudio(float deltaTime)
        {
            if (deltaTime <= 0f || !float.IsFinite(deltaTime) || _integrityComponent.CurrentIntegrity <= 0f)
                return;

            float threshold01 = math.saturate(lowIntegrityGroanThreshold01);
            float integrity01 = IntegrityStateNormalized;
            if (integrity01 >= threshold01)
            {
                _ruptureGroanPreviousNoise = -1f;
                return;
            }

            _ruptureGroanNoisePhase += math.max(0f, deltaTime) * math.max(0.01f, lowIntegrityGroanNoiseFrequency);
            float noiseValue = noise.snoise(new float2(_ruptureGroanNoisePhase, _brownoutNoiseSeed + 31.73f));
            float threshold = math.clamp(lowIntegrityGroanNoiseThreshold, -1f, 1f);
            bool crossedThreshold = noiseValue >= threshold && _ruptureGroanPreviousNoise < threshold;
            _ruptureGroanPreviousNoise = noiseValue;
            if (!crossedThreshold)
                return;

            float damage01 = math.saturate((threshold01 - integrity01) / math.max(0.0001f, threshold01));
            float stress01 = math.saturate(math.max(lowIntegrityGroanStressFloor, damage01));
            float pitchNoise = noise.snoise(new float2(_ruptureGroanNoisePhase * 3.17f, _brownoutNoiseSeed + 91.41f));
            float pitch01 = math.saturate(pitchNoise * 0.5f + 0.5f);
            float pitchMin = math.max(0.1f, lowIntegrityGroanPitchMin);
            float pitchMax = math.max(pitchMin, lowIntegrityGroanPitchMax);
            float pitch = math.lerp(pitchMin, pitchMax, pitch01) * math.lerp(1f, 0.82f, damage01);

            ResolveModuleAmbienceBounds(out Vector3 centerWS, out _);
            ProceduralAudioEvents.TryRaiseStructuralStressTriggered(centerWS, stress01, pitch);
        }

        internal void ApplyRuptureCascadeStress(float stressMultiplier01)
        {
            if (stressMultiplier01 <= 0f ||
                !float.IsFinite(stressMultiplier01) ||
                IsBreached)
            {
                return;
            }

            float previousStress = _jointShearStress01;
            _jointShearStress01 = Mathf.Clamp01(_jointShearStress01 + stressMultiplier01);
            if (previousStress < 1f && _jointShearStress01 >= 1f)
                _ruptureCascadeFailureQueued = true;
        }

        internal bool TryConsumePendingRuptureCascadeFailure()
        {
            if (!_ruptureCascadeFailureQueued)
                return false;

            _ruptureCascadeFailureQueued = false;
            if (IsBreached)
                return false;

            float ruptureDamage = Mathf.Max(1f, CurrentIntegrity);
            ApplyDamage(ruptureDamage);
            return true;
        }

        internal float ResolveThermalSurfaceAreaSquareMeters()
        {
            Vector3 size = moduleTemplate != null ? moduleTemplate.ProxyBoundsSize : Vector3.zero;
            if (size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f)
            {
                size = interiorTrigger != null
                    ? interiorTrigger.bounds.size
                    : new Vector3(4f, 4f, 4f);
            }

            float area = 2f * ((size.x * size.y) + (size.x * size.z) + (size.y * size.z));
            return float.IsFinite(area) ? Mathf.Max(0.1f, area) : 1f;
        }

        /// <summary>
        /// Applies a normalized integrity state authored by abandoned-module generation.
        /// </summary>
        public void SetIntegrityState(float integrityState)
        {
            ConfigureRuntimeComponentsFromSerializedState();
            bool wasBreached = _integrityComponent.CurrentIntegrity <= 0f;

            float normalizedIntegrity = Mathf.Clamp01(integrityState);
            float resolvedIntegrity = normalizedIntegrity * Mathf.Max(0f, _integrityComponent.MaxIntegrity);
            _integrityComponent.SetCurrentIntegrity(resolvedIntegrity);

            if (moduleTemplate != null)
            {
                if (normalizedIntegrity <= moduleTemplate.FloodedBelowIntegrityState)
                    _integrityComponent.SetFlooded(true);
                else
                    _integrityComponent.SetFlooded(false);

                if (normalizedIntegrity <= moduleTemplate.OxygenOfflineBelowIntegrityState)
                    _lifeSupportComponent.CollapseBreathableReserve();
            }

            SyncWaterVolumeToFloodFlag(_integrityComponent.IsFlooded);

            if (!_integrityComponent.IsFlooded)
                ResetBulkheadFloodStress();

            UpdateDrainDiagnostics();
            RefreshVisualStateImmediate();
            SyncTrackedObjectsFloodState();
            SyncSpatialRole();

            if (!wasBreached && normalizedIntegrity <= 0f)
            {
                _breachCenterOfMassTargetLocal = ResolveDefaultBreachLocalPoint();
                _hasBreachCenterOfMassTarget = true;
                HandleIntegrityCollapse(ResolveDefaultBreachLocalPoint());
            }
            else if (normalizedIntegrity > 0f)
            {
                _breachLatched = false;
                NotifyEmergencyLockdownStateChanged();
            }

            BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        /// <summary>
        /// Applies a discrete integrity-state preset for authored pristine modules or world-gen wreck variants.
        /// </summary>
        public void SetIntegrityState(BaseModuleIntegrityState integrityState)
        {
            float normalizedIntegrity;
            bool flooded;

            switch (integrityState)
            {
                case BaseModuleIntegrityState.Ruptured:
                    normalizedIntegrity = 0f;
                    flooded = true;
                    break;

                case BaseModuleIntegrityState.Flooded:
                    normalizedIntegrity = moduleTemplate != null
                        ? Mathf.Min(0.95f, moduleTemplate.FloodedBelowIntegrityState)
                        : 0.4f;
                    flooded = true;
                    break;

                case BaseModuleIntegrityState.Abandoned:
                    normalizedIntegrity = moduleTemplate != null
                        ? Mathf.Min(0.4f, Mathf.Clamp01(moduleTemplate.DefaultIntegrityState))
                        : 0.2f;
                    flooded = moduleTemplate == null || normalizedIntegrity <= moduleTemplate.FloodedBelowIntegrityState;
                    break;

                default:
                    normalizedIntegrity = 1f;
                    flooded = false;
                    break;
            }

            SetState(new BaseModuleSaveState
            {
                Integrity = normalizedIntegrity * Mathf.Max(0f, _integrityComponent.MaxIntegrity),
                Flooded = flooded,
                CascadeFailure = BaseModuleFailureMode.None,
                RepairIntegrityCap = maxIntegrity,
                AirReserveNormalized = integrityState == BaseModuleIntegrityState.Pristine ? 1f : Mathf.Clamp01(normalizedIntegrity),
                Co2Normalized = 0f,
                FloodedReefFloodSeconds = 0f,
                InteriorReefInfestationActive = false
            });
        }

        /// <summary>
        /// Polnyy sbros vizualnogo sostoyaniya modulya po tekuschim dannym.
        /// Vyzyvaetsya ConstructionManager posle zagruzki sohraneniya.
        /// </summary>
        public void RefreshAfterLoad()
        {
            ConfigureRuntimeComponentsFromSerializedState();
            RefreshVisualStateImmediate();
            SetInteriorReefVisualActive(_interiorReefInfestationActive);
            SyncTrackedObjectsFloodState();
            ResyncInteriorOccupants(true);
            _integrityComponent.TryStartDrain(_hasPower);
            UpdateDrainDiagnostics();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
            RefreshOwnedAirlockPressurizationSnapshots();
        }

        /// <summary>
        /// Restores module state from save, including flooded reef maturation state.
        /// </summary>
        public void SetState(in BaseModuleSaveState state)
        {
            ConfigureRuntimeComponentsFromSerializedState();
            _integrityComponent.RestoreState(state.Integrity, state.Flooded, state.CascadeFailure, state.RepairIntegrityCap);
            SyncWaterVolumeToFloodFlag(state.Flooded);
            _lifeSupportComponent.RestoreState(state.AirReserveNormalized, state.Co2Normalized);
            _floodedReefFloodSeconds = Mathf.Max(0f, state.FloodedReefFloodSeconds);
            _interiorReefInfestationActive = state.InteriorReefInfestationActive;
            SetInteriorReefVisualActive(_interiorReefInfestationActive);
            if (_interiorReefInfestationActive)
                RegisterFloodedReefFaunaAnchor();
            else
                UnregisterFloodedReefFaunaAnchor();
            if (!state.Flooded)
                ResetBulkheadFloodStress();
            RefreshVisualStateImmediate();
            SyncTrackedObjectsFloodState();
            SyncSpatialRole();
            _breachLatched = _integrityComponent.CurrentIntegrity <= 0f;
            if (!_breachLatched)
                _hasBreachCenterOfMassTarget = false;
            NotifyEmergencyLockdownStateChanged();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        // ----------------------------------------------------------
        //  PUBLIC API � DECONSTRUCTION
        // ----------------------------------------------------------

        /// <summary>
        /// Razbiraet modul, vozvraschaya resursy igroku.
        ///
        /// Poryadok:
        ///   1. Poluchit buildCost iz ModuleMarker.Data.
        ///   2. Dlya kazhdogo resursa: refund = floor(amount / 2).
        ///   3. Popytka dobavit v PlayerInventory.Grid.
        ///   4. Esli inventar polon � spavn HectonItem v mir cherez ObjectPoolManager.
        ///   5. Osvobozhdenie dry zone (ReleaseAllTrackedObjects).
        ///   6. ConstructionManager.DestroyModule(gameObject).
        ///
        /// ZERO GC:
        ///   � for-tsikly po List, bez LINQ.
        ///   � TryAddItem vozvraschaet bool, bez allokatsiy.
        ///   � ObjectPoolManager.Spawn � zero GC (pre-warmed pool).
        ///
        /// ZASchITA:
        ///   � _isDeconstructing predotvraschaet povtornyy vyzov.
        ///   � Null-safe: esli ModuleMarker/Data/buildCost otsutstvuyut �
        ///     modul unichtozhaetsya bez vozvrata resursov (s Warning).
        /// </summary>
        /// <param name="playerInventory">
        /// Inventar igroka dlya vozvrata resursov.
        /// Null dopustim � vse resursy budut spavneny v mir.
        /// </param>
        public void Deconstruct(PlayerInventory playerInventory)
        {
            if (_isDeconstructing)
                return;

            EnqueueLegacyDeconstructionRequest();
        }

        /// <summary>
        /// Proveryaet, mozhno li dekonstruirovat etot modul.
        /// Ispolzuetsya LaserCutter dlya validatsii pered nachalom razbora.
        /// </summary>
        public bool CanDeconstruct()
        {
            if (_isDeconstructing) return false;

            // Buduschee: zapret dekonstruktsii pri zatoplenii,
            // nalichii podklyuchennyh moduley, pitanii i t.d.
            return true;
        }

        internal bool TryBeginAuthoritativeDeconstruction()
        {
            if (_isDeconstructing)
                return false;

            _isDeconstructing = true;
            SetDeconstructionPreview(false);
            PlaySpatialSfx(deconstructClip);
            return true;
        }

        internal void CancelAuthoritativeDeconstruction()
        {
            _isDeconstructing = false;
        }

        internal void PrepareForDeconstructionPoolReturn()
        {
            SetDeconstructionPreview(false);
            StopDrain();
            SetLeakActive(false);
            SetFloodedVisual(false);
            waterVolumeM3 = 0f;
            ReleaseAllTrackedObjects();
        }

        internal bool CanEjectHostedContentsForDeconstruction(PlayerInventory playerInventory, IObjectPoolService pool)
        {
            return CanEjectHostedModuleContents(playerInventory, pool, ResolveHostedContentsDropPosition());
        }

        internal bool EjectHostedContentsForDeconstruction(PlayerInventory playerInventory, IObjectPoolService pool)
        {
            Vector3 dropPosition = ResolveHostedContentsDropPosition();
            return EjectHostedModuleContents(playerInventory, pool, ref dropPosition);
        }

        internal void SetDeconstructionPreview(bool enabled)
        {
            if (_deconstructionPreviewActive == enabled)
                return;

            _deconstructionPreviewActive = enabled;
            if (deconstructionGhostVisual != null && deconstructionGhostVisual.activeSelf != enabled)
                deconstructionGhostVisual.SetActive(enabled);

            if (deconstructionGhostRenderer == null || deconstructionGhostMaterial == null)
                return;

            if (enabled)
            {
                _deconstructionPreviewOriginalMaterial = deconstructionGhostRenderer.sharedMaterial;
                deconstructionGhostRenderer.sharedMaterial = deconstructionGhostMaterial;
            }
            else if (_deconstructionPreviewOriginalMaterial != null)
            {
                deconstructionGhostRenderer.sharedMaterial = _deconstructionPreviewOriginalMaterial;
                _deconstructionPreviewOriginalMaterial = null;
            }
        }

        private void EnqueueLegacyDeconstructionRequest()
        {
            IHabitatDeconstructionSystem deconstructionSystem = GlobalRegistry.HabitatDeconstruction;
            if (deconstructionSystem == null || !deconstructionSystem.IsInitialized)
                return;

            Vector3 modulePosition = transform.position;
            if (!TryResolveAupFromRuntimeOrigin(modulePosition, out AbsoluteUniversePosition targetAup) ||
                !TryResolveAupFromRuntimeOrigin(modulePosition + Vector3.up, out AbsoluteUniversePosition rayOriginAup))
            {
                return;
            }

            DeconstructRequestSignal request = new DeconstructRequestSignal
            {
                TargetAup = targetAup,
                RayOriginAup = rayOriginAup,
                TargetEntityId = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId())),
                RequesterEntityId = 0u,
                MaxDistance = 0f,
                RayDirection = new float3(0f, -1f, 0f),
                Frame = SystemDispatcher.CurrentFrameId,
                ToolKind = 0,
                Flags = 1
            };

            deconstructionSystem.EnqueueDeconstruction(in request);
        }

        internal bool CanDropItemQuantityToInventoryOrWorld(
            int itemHashId,
            int quantity,
            PlayerInventory playerInventory,
            IObjectPoolService pool)
        {
            return CanDropItemQuantityToInventoryOrWorld(
                itemHashId,
                quantity,
                playerInventory,
                pool,
                ResolveHostedContentsDropPosition());
        }

        internal bool CanDropItemQuantityToInventoryOrWorld(
            int itemHashId,
            int quantity,
            PlayerInventory playerInventory,
            IObjectPoolService pool,
            Vector3 dropPosition)
        {
            if (itemHashId == 0 || quantity <= 0)
                return false;

            ItemCatalog itemCatalog = ResolveItemCatalog(playerInventory);
            if (itemCatalog == null)
                return false;

            ItemData itemData = itemCatalog.FindByHash(itemHashId);
            if (itemData == null)
                return false;

            if (playerInventory != null &&
                playerInventory.CanAcceptItemQuantity(itemHashId, quantity))
            {
                return true;
            }

            if (!IsFiniteRuntimePosition(dropPosition))
                return false;

            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            if (persistentWorldRegistry != null &&
                persistentWorldRegistry.CanRegisterDroppedItem(itemData, quantity, dropPosition))
            {
                return true;
            }

            return false;
        }

        internal bool CanSpawnPooledWorldItemFallback(
            int itemHashId,
            PlayerInventory playerInventory,
            IObjectPoolService pool,
            Vector3 position)
        {
            if (itemHashId == 0 || pool == null || !IsFiniteRuntimePosition(position))
                return false;

            ItemCatalog itemCatalog = ResolveItemCatalog(playerInventory);
            return itemCatalog != null &&
                   itemCatalog.FindByHash(itemHashId) != null &&
                   worldItemPrefab != null &&
                   worldItemPrefab.TryGetComponent(out HectonItem _);
        }

        internal int DropItemQuantityToInventoryOrWorld(
            int itemHashId,
            int quantity,
            PlayerInventory playerInventory,
            IObjectPoolService pool,
            ref Vector3 dropPosition)
        {
            if (itemHashId == 0 || quantity <= 0)
                return 0;

            int delivered = 0;
            InventoryGrid targetGrid = playerInventory != null ? playerInventory.Grid : null;
            bool persistentDropUnavailable = false;
            for (int i = 0; i < quantity; i++)
            {
                bool addedToInventory = false;
                if (playerInventory != null &&
                    targetGrid != null &&
                    playerInventory.TryAddItem(itemHashId, 1))
                    addedToInventory = true;

                if (addedToInventory)
                {
                    delivered++;
                    continue;
                }

                int remainingQuantity = quantity - delivered;
                if (!persistentDropUnavailable &&
                    TryRegisterPersistentDroppedItemQuantity(itemHashId, remainingQuantity, dropPosition, playerInventory))
                {
                    delivered += remainingQuantity;
                    break;
                }

                persistentDropUnavailable = true;
                if (!SpawnPooledWorldItem(itemHashId, dropPosition, pool, playerInventory))
                    break;

                delivered++;
                dropPosition.x += 0.3f;
            }

            return delivered;
        }

        // ----------------------------------------------------------
        //  PRIVATE � WORLD ITEM SPAWN
        // ----------------------------------------------------------

        /// <summary>
        /// Spavnit resurs kak fizicheskiy predmet v mire.
        ///
        /// Pattern:
        ///   1. Esli worldItemPrefab naznachen ? Spawn cherez ObjectPoolManager.
        ///   2. Spavnennyy HectonItem initsializiruetsya po hashId cherez ItemCatalog.
        ///   3. Esli worldItemPrefab == null ? resurs poteryan (s Warning).
        ///
        /// Razdelenie otvetstvennostey:
        ///   BaseModule NE znaet pro konkretnyy vizual predmeta.
        ///   worldItemPrefab � generic konteyner s HectonItem + Rigidbody.
        ///   Katalozhnye dannye na HectonItem ustanavlivayutsya programmno.
        ///
        /// Buduschee: esli nuzhna vizualnaya differentsiatsiya (raznye modeli
        /// dlya titana vs stekla), worldItemPrefab mozhet byt zamenen
        /// na per-resource world prefab, esli poyavitsya otdelnyy vizualnyy vladelets.
        /// </summary>
        // Persistent registry path: stores the remaining stack as one record quantity.
        private bool TryRegisterPersistentDroppedItemQuantity(
            int itemHashId,
            int quantity,
            Vector3 position,
            PlayerInventory playerInventory)
        {
            if (itemHashId == 0 || quantity <= 0)
                return false;

            ItemCatalog itemCatalog = ResolveItemCatalog(playerInventory);
            if (itemCatalog == null)
                return false;

            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            return persistentWorldRegistry != null &&
                   persistentWorldRegistry.TryRegisterDroppedItem(itemHashId, itemCatalog, quantity, position);
        }

        // Pooled visual fallback: spawns one initialized world item when persistence cannot accept the drop.
        private bool SpawnPooledWorldItem(int itemHashId, Vector3 position, IObjectPoolService pool, PlayerInventory playerInventory)
        {
            if (itemHashId == 0)
                return false;

            if (!IsFiniteRuntimePosition(position))
                return false;

            ItemCatalog itemCatalog = ResolveItemCatalog(playerInventory);
            if (itemCatalog == null)
                return false;

            if (worldItemPrefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    $"[BaseModule] worldItemPrefab not assigned on '{gameObject.name}'. " +
                    $"Resource hash '{itemHashId}' dropped on the ground but has no world prefab. Lost.",
                    this);
#endif
                return false;
            }

            if (pool == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    "[BaseModule] ObjectPoolManager not available. " +
                    $"Resource hash '{itemHashId}' lost.");
#endif
                return false;
            }

            GameObject itemGO = pool.Spawn(worldItemPrefab, position, Quaternion.identity);

            if (itemGO == null)
                return false;

            // -- Initsializatsiya HectonItem dannymi --
            // HectonItem na worldItemPrefab initsializiruetsya hashId cherez ItemCatalog.
            // Bazovyy modul ne tyanet asset-ssylki v logiku vozvrata resursov.
            //
            // ARHITEKTURNOE REShENIE:
            // Vizualnyy/world seam ostaetsya vnutri HectonItem.
            // Eto chische, chem refleksiya, i sohranyaet Zero-GC.
            if (itemGO.TryGetComponent(out HectonItem hectonItem))
            {
                if (hectonItem.SetItemByHash(itemCatalog, itemHashId, 1))
                    return true;
            }

            DespawnWithPoolOrDeactivate(itemGO, pool);
            return false;
        }

        private ItemCatalog ResolveItemCatalog(PlayerInventory playerInventory)
        {
            if (playerInventory != null && playerInventory.ItemCatalog != null)
                return playerInventory.ItemCatalog;

            IPlayerInventoryService inventoryService = _cachedPlayerInventoryService;
            PlayerInventory inventoryInstance = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            return inventoryInstance != null ? inventoryInstance.ItemCatalog : null;
        }

        private Vector3 ResolveHostedContentsDropPosition()
        {
            return transform.position + Vector3.up * 0.5f;
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            return math.isfinite(position.x) &&
                   math.isfinite(position.y) &&
                   math.isfinite(position.z);
        }

        // ----------------------------------------------------------
        //  PRIVATE � CORE STATE LOGIC
        // ----------------------------------------------------------

        private float GetRepairIntegrityCap()
        {
            return _integrityComponent.GetRepairIntegrityCap();
        }

        private float ResolveFloodPumpPowerDraw()
        {
            return _integrityComponent.IsDraining
                ? Mathf.Max(0f, floodPumpEnergyCost)
                : 0f;
        }

        private bool CanEjectHostedModuleContents(PlayerInventory playerInventory, IObjectPoolService pool, Vector3 dropPosition)
        {
            if (TryGetComponent(out MaintenanceStationModule maintenanceStation) &&
                maintenanceStation.TryPeekSlottedToolHashForDeconstruct(out int slottedToolHashId) &&
                !CanDropItemQuantityToInventoryOrWorld(slottedToolHashId, 1, playerInventory, pool, dropPosition))
            {
                return false;
            }

            if (TryGetComponent(out DeepDrillModule drillModule) &&
                !drillModule.CanEjectBufferedOutput(this, playerInventory, pool, dropPosition))
            {
                return false;
            }

            if (TryGetComponent(out LogisticsSorterModule sorterModule) &&
                !sorterModule.CanEjectBufferedContents(this, playerInventory, pool, dropPosition))
            {
                return false;
            }

            if (TryGetComponent(out ResourceRecyclerModule recyclerModule) &&
                !recyclerModule.CanEjectBufferedContents(this, playerInventory, pool, dropPosition))
            {
                return false;
            }

            if (TryGetComponent(out Fabricator fabricator) &&
                !fabricator.CanEjectPendingCraftOutput(playerInventory, dropPosition))
            {
                return false;
            }

            if (TryGetComponent(out CultivationManager cultivationManager) &&
                !cultivationManager.CanEjectCultivationContents(this, playerInventory, dropPosition))
            {
                return false;
            }

            if (TryGetComponent(out StorageCrate storageCrate) &&
                !storageCrate.CanEjectContainedContents(this, playerInventory, pool, dropPosition))
            {
                return false;
            }

            if (TryGetComponent(out LogisticsPipeNode pipeNode) &&
                pipeNode.TryPeekInFlightCargoHashForDeconstruct(out int pipeItemHashId, out int pipeAmount) &&
                !CanDropItemQuantityToInventoryOrWorld(pipeItemHashId, pipeAmount, playerInventory, pool, dropPosition))
            {
                return false;
            }

            return true;
        }

        private bool EjectHostedModuleContents(PlayerInventory playerInventory, IObjectPoolService pool, ref Vector3 dropPosition)
        {
            bool allDelivered = true;
            if (TryGetComponent(out MaintenanceStationModule maintenanceStation) &&
                maintenanceStation.TryExtractSlottedToolHashForDeconstruct(out int slottedToolHashId))
            {
                allDelivered &= DropItemQuantityToInventoryOrWorld(slottedToolHashId, 1, playerInventory, pool, ref dropPosition) == 1;
            }

            if (TryGetComponent(out DeepDrillModule drillModule))
                allDelivered &= drillModule.EjectBufferedOutput(this, playerInventory, pool, ref dropPosition);

            if (TryGetComponent(out LogisticsSorterModule sorterModule))
                allDelivered &= sorterModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);

            if (TryGetComponent(out ResourceRecyclerModule recyclerModule))
                allDelivered &= recyclerModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);

            if (TryGetComponent(out Fabricator fabricator))
                allDelivered &= fabricator.EjectPendingCraftOutput(playerInventory, ref dropPosition);

            if (TryGetComponent(out CultivationManager cultivationManager))
                allDelivered &= cultivationManager.EjectCultivationContents(this, playerInventory, ref dropPosition);

            if (TryGetComponent(out StorageCrate storageCrate))
                allDelivered &= storageCrate.EjectContainedContents(this, playerInventory, pool, ref dropPosition);

            if (TryGetComponent(out LogisticsPipeNode pipeNode) &&
                pipeNode.TryExtractInFlightCargoHashForDeconstruct(out int pipeItemHashId, out int pipeAmount))
            {
                allDelivered &= DropItemQuantityToInventoryOrWorld(pipeItemHashId, pipeAmount, playerInventory, pool, ref dropPosition) == pipeAmount;
            }

            return allDelivered;
        }

        private void EnsureRepairIntegrityCapInitialized()
        {
            ConfigureRuntimeComponentsFromSerializedState();
        }

        private void InitializeBreathableReserveCold()
        {
            ConfigureRuntimeComponentsFromSerializedState();
        }

        private void ApplyMaterialFatigue()
        {
            _integrityComponent.ApplyMaterialFatigue();
        }

        internal void SetAmbientLightsBrownout(bool brownedOut)
        {
            SetAmbientPowerVisualState(brownedOut, brownedOut ? Mathf.Min(_ambientVoltageSupplyRatio, 0.5f) : 1f);
        }

        internal void SetAmbientPowerVisualState(bool brownedOut, float voltageSupplyRatio)
        {
            _brownoutTransitionTarget01 = brownedOut ? 1f : 0f;
            float sanitizedVoltageRatio = Mathf.Clamp01(float.IsFinite(voltageSupplyRatio) ? voltageSupplyRatio : 1f);
            if (_ambientLightsBrownedOut == brownedOut)
            {
                _ambientVoltageSupplyRatio = sanitizedVoltageRatio;
                AdvanceBrownoutShaderState(0f);
                QueueActiveModuleWaterLevelsShaderUpload(true);
                UpdateAmbienceTickRegistration();
                return;
            }

            _ambientLightsBrownedOut = brownedOut;
            _ambientVoltageSupplyRatio = sanitizedVoltageRatio;
            QueueLightsEnabled(ShouldLightsBeEnabled());
            AdvanceBrownoutShaderState(0f);
            QueueActiveModuleWaterLevelsShaderUpload(true);
            UpdateAmbienceTickRegistration();
        }

        private bool HasOperationalPower => _solarEmpBlackoutRemainingSeconds <= 0.0001f &&
                                            _integrityComponent.HasOperationalPower(_hasPower);
        private bool ShouldLightsBeEnabled() => HasOperationalPower && !_ambientLightsBrownedOut;

        public void OnElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent)
        {
            if ((pulseEvent.DamageType & (uint)DamageTypeMask.Emp) == 0u ||
                pulseEvent.DurationSeconds <= 0f ||
                pulseEvent.RadiusMeters <= 0f)
            {
                return;
            }

            float radius = pulseEvent.RadiusMeters;
            float radiusSq = radius * radius;
            Vector3 moduleProbePosition = ResolveAtmosphereRoomProbeWorldPosition();
            if (radius > AupRadiusLogicThresholdMeters && radius < 250000f)
            {
                if (!TryResolveAupFromRuntimeOrigin(moduleProbePosition, out AbsoluteUniversePosition moduleAup) ||
                    !TryResolveAupFromRuntimeOrigin(pulseEvent.RuntimePosition, out AbsoluteUniversePosition pulseAup))
                {
                    return;
                }

                if (AbsoluteUniversePosition.DistanceSq(in moduleAup, in pulseAup) > (double)radiusSq)
                    return;
            }
            else if (radius <= AupRadiusLogicThresholdMeters)
            {
                Vector3 delta = moduleProbePosition - pulseEvent.RuntimePosition;
                if (delta.sqrMagnitude > radiusSq)
                    return;
            }

            _solarEmpBlackoutRemainingSeconds = Mathf.Max(
                _solarEmpBlackoutRemainingSeconds,
                pulseEvent.DurationSeconds);
            _debugSolarEmpBlackoutSeconds = _solarEmpBlackoutRemainingSeconds;
            _integrityComponent.StopDrain();
            QueueLightsEnabled(false);
            UpdateDrainDiagnostics();
            SyncSpatialRole();
            UpdateOxygenScrubberHumTarget();
            UpdateAmbienceTickRegistration();
        }

        private void AdvanceSolarEmpBlackout(float deltaTime)
        {
            if (_solarEmpBlackoutRemainingSeconds <= 0f)
            {
                _debugSolarEmpBlackoutSeconds = 0f;
                return;
            }

            _solarEmpBlackoutRemainingSeconds = math.max(0f, _solarEmpBlackoutRemainingSeconds - math.max(0f, deltaTime));
            _debugSolarEmpBlackoutSeconds = _solarEmpBlackoutRemainingSeconds;
            if (_solarEmpBlackoutRemainingSeconds > 0f)
                return;

            QueueLightsEnabled(ShouldLightsBeEnabled());
            if (HasOperationalPower)
                _integrityComponent.TryStartDrain(_hasPower);
            UpdateDrainDiagnostics();
            SyncSpatialRole();
            UpdateOxygenScrubberHumTarget();
            UpdateAmbienceTickRegistration();
        }

        private void TriggerCascadeFailure()
        {
            TriggerCascadeFailure(ResolveCascadeFailureMode());
        }

        private void TriggerCascadeFailure(BaseModuleFailureMode failureMode)
        {
            _integrityComponent.TriggerCascadeFailure(failureMode);
            UpdateDrainDiagnostics();

            switch (_integrityComponent.FailureMode)
            {
                case BaseModuleFailureMode.Fire:
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("MODULE FIRE", "Compartment ignition risk. Repair before occupancy.");
                    TryPushCascadeFailureNotification("BASE MODULE FIRE // SERVICE NOW".AsSpan(), BaseModuleFailureMode.Fire);
                    break;
                case BaseModuleFailureMode.ShortCircuit:
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("SHORT CIRCUIT", "Compartment flooded and pumps offline until hull service completes.");
                    TryPushCascadeFailureNotification("BASE SHORT CIRCUIT // POWER LOCKOUT".AsSpan(), BaseModuleFailureMode.ShortCircuit);
                    break;
                default:
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("OXYGEN LEAK", "Compartment seal lost. Oxygen-safe shelter compromised.");
                    TryPushCascadeFailureNotification("BASE OXYGEN LEAK // COMPARTMENT BREACHED".AsSpan(), BaseModuleFailureMode.OxygenLeak);
                    break;
            }

            SetLeakActive(ShouldLeakBeActive());
            SetFloodedVisual(_integrityComponent.IsFlooded);
            SyncTrackedObjectsFloodState();
            QueueLightsEnabled(ShouldLightsBeEnabled());
            SyncSpatialRole();
        }

        private void TryPushCascadeFailureNotification(ReadOnlySpan<char> message, BaseModuleFailureMode failureMode)
        {
            if (NotificationEvents.TryPushWarning(message))
                return;

            ReportCascadeFailureNotificationMiss(failureMode);
        }

        private void ReportCascadeFailureNotificationMiss(BaseModuleFailureMode failureMode)
        {
            _cascadeNotificationMissCount++;
            uint moduleHash = unchecked((uint)CachedModuleHashId);
            uint contextHash = s_baseCascadeNotificationContextHash ^ moduleHash ^ (uint)failureMode;
            GlobalTelemetryBus.PublishPerformanceWarning(
                s_baseCascadeNotificationMissWarningHash,
                contextHash,
                math.max(1, _cascadeNotificationMissCount));
        }

        private void ClearCascadeNotificationDiagnostics()
        {
            _cascadeNotificationMissCount = 0;
        }

        private void TryStartDrain()
        {
            _integrityComponent.TryStartDrain(_hasPower);
            UpdateDrainDiagnostics();
        }

        private void StopDrain()
        {
            _integrityComponent.StopDrain();
            UpdateDrainDiagnostics();
        }

        private void RefreshVisualStateImmediate()
        {
            SetLeakActive(ShouldLeakBeActive());

            SetFloodedVisual(_integrityComponent.IsFlooded);
            QueueLightsEnabled(ShouldLightsBeEnabled());
        }

        // ----------------------------------------------------------
        //  PRIVATE � INTERIOR ZONE SYNC
        // ----------------------------------------------------------

        private bool ShouldLeakBeActive()
        {
            return _integrityComponent.ShouldLeakBeActive();
        }

        private void ApplyCascadeFailureEffects()
        {
            _lifeSupportComponent.ApplyCascadeFailureEffects(
                _trackedPlayerSurvival,
                _integrityComponent.FailureMode,
                oxygenLeakDrainRate,
                fireSuitDamageRate,
                fireSuitEnergyDrainRate,
                SLOW_TICK_DT);
        }

        private void ApplyLocalGravityAnomalyRequest()
        {
            if (!localGravityAnomalyEnabled || _trackedPlayerMovement == null)
                return;

            float acceleration = Mathf.Max(0f, localGravityAccelerationMetersPerSecondSquared);
            if (acceleration <= 0f)
                return;

            Vector3 localDirection = localGravityDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantLocalAxis(localGravityDirection)
                : Vector3.down;
            Vector3 worldGravity = transform.TransformDirection(localDirection) * acceleration;
            if (IsFiniteVector(worldGravity))
                _trackedPlayerMovement.RequestLocalGravityOverride(worldGravity, Mathf.Max(0.1f, localGravityHoldSeconds));
        }

        private void UpdateLifeSupport(float dt)
        {
            bool scrubberOperational = HasOperationalPower && _carbonFilterAvailable;
            ModuleLifeSupportSignals signals = _lifeSupportComponent.Tick(
                dt,
                !_integrityComponent.IsFlooded && _integrityComponent.FailureMode != BaseModuleFailureMode.Fire,
                scrubberOperational,
                CachedPowerSupplyRatio,
                _trackedPlayerSurvival);

            HandleLifeSupportSignals(signals);
            UpdateOxygenScrubberHumTarget();
            UpdateAmbienceTickRegistration();
        }

        private float ResolveAirRefillScale()
        {
            return IsAirQualityLow ? staleAirMinRefillScale : 1f;
        }

        private void TrackAirReserveStateTransitions()
        {
            bool scrubberOperational = HasOperationalPower && _carbonFilterAvailable;
            HandleLifeSupportSignals(_lifeSupportComponent.Tick(
                0f,
                !_integrityComponent.IsFlooded && _integrityComponent.FailureMode != BaseModuleFailureMode.Fire,
                scrubberOperational,
                CachedPowerSupplyRatio,
                _trackedPlayerSurvival));
        }

        private void ClearCascadeFailure()
        {
            _integrityComponent.ClearCascadeFailure();
            SyncSpatialRole();
        }

        private BaseModuleFailureMode ResolveCascadeFailureMode()
        {
            string prefabId = _moduleMarker != null ? _moduleMarker.PrefabId : string.Empty;
            return _integrityComponent.ResolveCascadeFailureMode(prefabId, ResolveAtmosphereRoomProbeWorldPosition(), _hasPower);
        }

        private void RecordCascadeFailure(string title, string summary)
        {
            string source = _moduleMarker != null && _moduleMarker.Data != null
                ? _moduleMarker.Data.moduleName
                : "BASE";
            FieldOperationLogSystem.RecordOperation(source, title, summary, "WARN");
        }

        private void RecordCascadeFailure(string title, in FixedCharBuffer summary)
        {
            string source = _moduleMarker != null && _moduleMarker.Data != null
                ? _moduleMarker.Data.moduleName
                : "BASE";
            FieldOperationLogSystem.RecordOperation(source, title, in summary, "WARN");
        }

        private void SyncSpatialRole()
        {
            if (_moduleMarker == null)
                return;

            _moduleMarker.SetSpatialRole(ResolveSpatialRole());
        }

        private FieldTargetRole ResolveSpatialRole()
        {
            if (_integrityComponent.FailureMode == BaseModuleFailureMode.ShortCircuit ||
                _integrityComponent.FailureMode == BaseModuleFailureMode.OxygenLeak ||
                _integrityComponent.IsFlooded)
            {
                return FieldTargetRole.ServiceFlooded;
            }

            if (_integrityComponent.FailureMode == BaseModuleFailureMode.Fire)
                return FieldTargetRole.ServiceDamaged;

            if (_integrityComponent.CurrentIntegrity < _integrityComponent.MaxRecoverableIntegrity)
                return FieldTargetRole.ServiceDamaged;

            return FieldTargetRole.Generic;
        }

        private void SyncTrackedObjectsFloodState()
        {
            _wasFlooded = _integrityComponent.IsFlooded;
            UpdateTrackedDiagnostics();
        }

        private void ReleaseAllTrackedObjects()
        {
            UpdateTrackedDiagnostics();
        }

        // ----------------------------------------------------------
        //  PRIVATE � VISUALS
        // ----------------------------------------------------------

        private void ResyncInteriorOccupants(bool notifyPlayerEnter)
        {
            if (_trackedPlayerSurvival != null)
                return;

            IPlayerRuntimeContext playerContext = _cachedPlayerRuntime;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return;

            if (!TryContainsInteriorRuntimePoint(playerTransform.position))
                return;

            TryTrackPlayer(playerTransform, notifyPlayerEnter);
        }

        private void SetLeakActive(bool active)
        {
            _pendingLeakActive = active;
            _pendingLeakVisualDirty = true;
            TryRegisterLateFrameTick();
        }

        private void FlushLeakVisualState()
        {
            if (leakVfx == null) return;

            if (!_pendingLeakVisualDirty)
                return;

            _pendingLeakVisualDirty = false;
            bool active = _pendingLeakActive;

            if (active)
            {
                if (!leakVfx.isPlaying)
                    leakVfx.Play();
            }
            else
            {
                if (leakVfx.isPlaying)
                    leakVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (audioSource != null && leakLoop != null)
            {
                TryRouteAudioSourceToSfxGroup(audioSource);
                if (active)
                {
                    if (audioSource.clip != leakLoop || !audioSource.isPlaying)
                    {
                        audioSource.clip = leakLoop;
                        audioSource.loop = true;
                        audioSource.Play();
                    }
                }
                else
                {
                    if (audioSource.clip == leakLoop && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                        audioSource.loop = false;
                        audioSource.clip = null;
                    }
                }
            }
        }

        private void SetFloodedVisual(bool flooded)
        {
            UpdateFloodVisualState(flooded ? ResolveRuntimeFloodLevel01() : 0f);
        }

        private void UpdateFloodVisualStateImmediate()
        {
            UpdateFloodVisualState(ResolveRuntimeFloodLevel01());
        }

        private void UpdateFloodVisualState(float floodLevel01)
        {
            float sanitizedFloodLevel01 = Mathf.Clamp01(floodLevel01);
            _cachedFloodLevel01 = sanitizedFloodLevel01;
            _pendingFloodVisualDirty = true;
            QueueActiveModuleWaterLevelsShaderUpload(true);
            TryRegisterLateFrameTick();
        }

        private void FlushFloodVisualState()
        {
            if (!_pendingFloodVisualDirty)
                return;

            _pendingFloodVisualDirty = false;
            bool floodVisible = _cachedFloodLevel01 > 0.001f;
            if (waterVolume != null && waterVolume.activeSelf)
                waterVolume.SetActive(false);

            UpdateFloodDistortionVolume(floodVisible);
        }

        private void UpdateFloodDistortionVolume(bool floodVisible)
        {
            if (floodedLocalVolume == null)
                return;

            bool distortionVisible = floodVisible && IsFloodDistortionProbeSubmerged();
            if (floodedLocalVolume.enabled != distortionVisible)
                floodedLocalVolume.enabled = distortionVisible;
        }

        private bool IsFloodDistortionProbeSubmerged()
        {
            Transform probe = floodDistortionProbe;
            if (probe == null && _trackedPlayerSurvival != null)
                probe = _trackedPlayerSurvival.transform;

            if (probe == null)
                return false;

            return probe.position.y < ResolveFloodSurfaceWorldY();
        }

        private float ResolveFloodSurfaceWorldY()
        {
            if (floodSurfacePlane != null)
                return floodSurfacePlane.position.y;

            float localY = math.lerp(ResolveFloodSurfaceMinimumLocalY(), ResolveFloodSurfaceMaximumLocalY(), _cachedFloodLevel01);
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            return localToWorld.m13 + (localToWorld.m11 * localY);
        }

        private static void PublishActiveModuleWaterLevelsToShader(bool force = false)
        {
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            if (!force && s_lastModuleWaterLevelUploadFrame == currentFrame)
                return;

            s_lastModuleWaterLevelUploadFrame = currentFrame;
            int moduleCount = Mathf.Min(s_activeModules.Count, ModuleWaterLevelShaderCapacity);
            float baseVoltage01 = 1f;
            float baseFlickerSpeed = 19f;
            float baseVoltageMinimum = 0.04f;
            Color baseEmergencyColor = new Color(1f, 0.13f, 0.06f, 1f);
            bool hasGlobalModuleSettings = false;
            for (int i = 0; i < moduleCount; i++)
            {
                BaseModule module = s_activeModules[i];
                if (module == null)
                {
                    s_moduleAmbienceData[i] = Vector4.zero;
                    s_moduleFloodAndFlickerData[i] = new Vector4(0f, 0f, 1f, 0f);
                    continue;
                }

                module.ResolveModuleAmbienceBounds(out Vector3 centerWS, out float radiusMeters);
                float moduleVoltage01 = Clamp01Finite(module._currentBrownoutFlicker01, 1f);
                baseVoltage01 = Mathf.Min(baseVoltage01, moduleVoltage01);
                if (!hasGlobalModuleSettings)
                {
                    baseFlickerSpeed = MaxFinite(0.1f, module.brownoutFlickerSpeed, 19f);
                    baseVoltageMinimum = Clamp01Finite(module.brownoutMinimumLightIntensityRatio, 0.04f);
                    baseEmergencyColor = ResolveFiniteColor(module.brownoutEmergencyEmissionColor, baseEmergencyColor);
                    hasGlobalModuleSettings = true;
                }

                s_moduleAmbienceData[i] = new Vector4(centerWS.x, centerWS.y, centerWS.z, radiusMeters);
                s_moduleFloodAndFlickerData[i] = new Vector4(
                    module.ResolveFloodSurfaceWorldY(),
                    Mathf.Clamp01(module._cachedFloodLevel01),
                    moduleVoltage01,
                    module.ResolveHullCondensationDepth01());
            }

            for (int i = moduleCount; i < ModuleWaterLevelShaderCapacity; i++)
            {
                s_moduleAmbienceData[i] = Vector4.zero;
                s_moduleFloodAndFlickerData[i] = new Vector4(0f, 0f, 1f, 0f);
            }

            EnsureModuleWaterLevelBuffers();
            UploadModuleWaterLevelBuffers();
            Shader.SetGlobalInt(s_ModuleWaterLevelCountId, moduleCount);
            Shader.SetGlobalFloat(s_BaseVoltageId, baseVoltage01);
            Shader.SetGlobalFloat(s_BaseVoltageFlickerSpeedId, baseFlickerSpeed);
            Shader.SetGlobalFloat(s_BaseVoltageMinimumId, baseVoltageMinimum);
            Shader.SetGlobalColor(s_BaseBrownoutEmergencyColorId, baseEmergencyColor);
            s_moduleWaterLevelShaderDirty = false;
        }

        private float ResolveHullCondensationDepth01()
        {
            float startDepth = MaxFinite(0f, hullCondensationStartDepthMeters, 0f);
            float fullDepth = MaxFinite(startDepth + 1f, hullCondensationFullDepthMeters, startDepth + 1f);
            float depthMeters = _pressureCompressionDepthMeters > 0.25f && float.IsFinite(_pressureCompressionDepthMeters)
                ? _pressureCompressionDepthMeters
                : ResolveExternalDepthMeters();
            return Mathf.Clamp01((depthMeters - startDepth) / (fullDepth - startDepth));
        }

        private void ResolveModuleAmbienceBounds(out Vector3 centerWS, out float radiusMeters)
        {
            if (TryGetInteriorOverlapQuery(out centerWS, out Vector3 halfExtents, out _))
            {
                if (!IsFiniteVector(centerWS))
                    centerWS = ResolveInteriorHazardWorldPosition();

                float maxExtent = math.max(
                    MaxFinite(0f, halfExtents.x, 0f),
                    math.max(MaxFinite(0f, halfExtents.y, 0f), MaxFinite(0f, halfExtents.z, 0f)));
                radiusMeters = math.max(0.5f, (maxExtent * 1.75f) + 0.25f);
                return;
            }

            centerWS = ResolveInteriorHazardWorldPosition();
            float volumeRadius = 1f + (math.saturate((ResolveBuoyancyDisplacementVolumeCubicMeters() - 1f) * 0.008f) * 4f);
            radiusMeters = math.max(2f, volumeRadius * 1.75f);
        }

        private void ApplyDeepSeaCompressionState(bool force)
        {
            float depthMeters = ResolveExternalDepthMeters();
            float axisScale = ResolvePressureCompressionAxisScale(depthMeters);
            float volumeScale = Mathf.Clamp(axisScale * axisScale, 0.1f, 1f);

            if (!force &&
                Mathf.Abs(_pressureCompressionAxisScale - axisScale) <= 0.00001f &&
                Mathf.Abs(_pressureCompressionVolumeScale - volumeScale) <= 0.00001f &&
                Mathf.Abs(_pressureCompressionDepthMeters - depthMeters) <= 0.25f)
            {
                return;
            }

            _pressureCompressionAxisScale = axisScale;
            _pressureCompressionVolumeScale = volumeScale;
            _pressureCompressionDepthMeters = depthMeters;
            RefreshFloodCapacityCache();
            _lifeSupportComponent.ApplyPressureCompressionScale(volumeScale);
            ApplyPressureCompressionVisualScale(axisScale);

            BaseDegradationSystem.SynchronizePressureCompression(
                this,
                Matrix4x4.Scale(new Vector3(axisScale, axisScale, 1f)),
                volumeScale,
                depthMeters);
        }

        private float ResolvePressureCompressionAxisScale(float depthMeters)
        {
            float safeDepthMeters = MaxFinite(0f, depthMeters, 0f);
            float startDepthMeters = MaxFinite(0f, deepCompressionStartDepthMeters, 0f);
            if (safeDepthMeters <= startDepthMeters)
                return 1f;

            float hydrostaticPressureKPa = ResolveHydrostaticPressureKPa(safeDepthMeters);
            float startPressureKPa = ResolveHydrostaticPressureKPa(startDepthMeters);
            float pressureRangeKPa = MaxFinite(1f, deepCompressionFullPressureKPa - startPressureKPa, 1f);
            float compression01 = Mathf.Clamp01((hydrostaticPressureKPa - startPressureKPa) / pressureRangeKPa);
            float axisLoss = float.IsFinite(maximumDeepCompressionAxisLoss)
                ? Mathf.Clamp(maximumDeepCompressionAxisLoss, 0f, 0.01f)
                : 0f;
            return 1f - (compression01 * axisLoss);
        }

        private static float ResolveHydrostaticPressureKPa(float depthMeters)
        {
            return SurfacePressureKPa + (MaxFinite(0f, depthMeters, 0f) * SeawaterDensityKilogramsPerCubicMeter * GravityAccelerationMetersPerSecondSquared * 0.001f);
        }

        private void ApplyPressureCompressionVisualScale(float axisScale)
        {
            if (pressureCompressionVisualRoot == null)
                return;

            CapturePressureCompressionDefaults();
            Vector3 nextScale = _defaultPressureCompressionVisualScale;
            nextScale.x *= axisScale;
            nextScale.y *= axisScale;
            QueuePressureCompressionVisualScale(nextScale);
        }

        private void ResetPressureCompressionVisualState()
        {
            _pressureCompressionAxisScale = 1f;
            _pressureCompressionVolumeScale = 1f;
            _pressureCompressionDepthMeters = 0f;
            RefreshFloodCapacityCache();
            _lifeSupportComponent.ApplyPressureCompressionScale(1f);

            if (pressureCompressionVisualRoot == null)
                return;

            CapturePressureCompressionDefaults();
            QueuePressureCompressionVisualScale(_defaultPressureCompressionVisualScale);
            QueuePressureCompressionVisualRotation(_defaultPressureCompressionVisualRotation);
        }

        private void QueuePressureCompressionVisualScale(Vector3 localScale)
        {
            if (!IsFiniteVector(localScale))
                return;

            _pendingPressureVisualScale = localScale;
            _pendingPressureVisualScaleDirty = true;
            TryRegisterLateFrameTick();
        }

        private void QueuePressureCompressionVisualRotation(Quaternion localRotation)
        {
            if (!IsFiniteQuaternion(localRotation))
                localRotation = _defaultPressureCompressionVisualRotation;

            _pressureCompressionVisualRotationState = localRotation;
            _pendingPressureVisualRotation = localRotation;
            _pendingPressureVisualRotationDirty = true;
            TryRegisterLateFrameTick();
        }

        private void FlushPressureCompressionVisualState()
        {
            if (pressureCompressionVisualRoot == null)
            {
                _pendingPressureVisualScaleDirty = false;
                _pendingPressureVisualRotationDirty = false;
                return;
            }

            if (_pendingPressureVisualScaleDirty)
            {
                _pendingPressureVisualScaleDirty = false;
                pressureCompressionVisualRoot.localScale = _pendingPressureVisualScale;
            }

            if (_pendingPressureVisualRotationDirty)
            {
                _pendingPressureVisualRotationDirty = false;
                pressureCompressionVisualRoot.localRotation = _pendingPressureVisualRotation;
            }
        }

        private void ApplyFluidIncursion(float deltaTime)
        {
            if (deltaTime <= 0f || !float.IsFinite(deltaTime))
                return;

            if (!IsBreached && IntegrityState != BaseModuleIntegrityState.Ruptured && !_breachLatched)
                return;

            float capacityM3 = ResolveFloodCapacityM3();
            if (waterVolumeM3 >= capacityM3)
                return;

            float pressureRootKPa = ResolveIngressPressureRootApprox(ResolveExternalPressureDeltaKPa());
            ApplyGraphPressureIngress(deltaTime, pressureRootKPa);
        }

        internal static float CalculateIngressVolumeDeltaM3(
            float depthMeters,
            float holeAreaSquareMeters,
            float deltaTime,
            float pressureFlowCoefficient)
        {
            float safeDepthMeters = float.IsFinite(depthMeters) ? Mathf.Max(0f, depthMeters) : 0f;
            float safeHoleArea = float.IsFinite(holeAreaSquareMeters) ? Mathf.Max(0f, holeAreaSquareMeters) : 0f;
            float safeDeltaTime = float.IsFinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;
            float safeFlowCoefficient = float.IsFinite(pressureFlowCoefficient) ? Mathf.Max(0f, pressureFlowCoefficient) : 0f;
            if (safeDepthMeters <= 0f || safeHoleArea <= 0f || safeDeltaTime <= 0f || safeFlowCoefficient <= 0f)
                return 0f;

            float pressureDeltaKPa = math.max(0f, ResolveHydrostaticPressureKPa(safeDepthMeters) - SurfacePressureKPa);
            float volumeDelta = CalculatePressureDrivenIngressVolumeDeltaM3(
                ResolveIngressPressureRootApprox(pressureDeltaKPa),
                safeHoleArea,
                safeDeltaTime,
                safeFlowCoefficient);
            return float.IsFinite(volumeDelta) ? Mathf.Max(0f, volumeDelta) : 0f;
        }

        internal static float CalculatePressureDrivenIngressVolumeDeltaM3(
            float pressureRootKPa,
            float holeAreaSquareMeters,
            float deltaTime,
            float pressureFlowCoefficient)
        {
            float safePressureRoot = float.IsFinite(pressureRootKPa) ? Mathf.Max(0f, pressureRootKPa) : 0f;
            float safeHoleArea = float.IsFinite(holeAreaSquareMeters) ? Mathf.Max(0f, holeAreaSquareMeters) : 0f;
            float safeDeltaTime = float.IsFinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;
            float safeFlowCoefficient = float.IsFinite(pressureFlowCoefficient) ? Mathf.Max(0f, pressureFlowCoefficient) : 0f;
            float volumeDelta = safePressureRoot * safeHoleArea * safeDeltaTime * safeFlowCoefficient;
            return float.IsFinite(volumeDelta) ? Mathf.Max(0f, volumeDelta) : 0f;
        }

        private static float ResolveIngressPressureRootApprox(float pressureDeltaKPa)
        {
            if (pressureDeltaKPa <= 0f || !float.IsFinite(pressureDeltaKPa))
                return 0f;

            float root = math.asfloat((math.asint(pressureDeltaKPa) >> 1) + (int)FastSqrtApproximationBias);
            return root > 0f && math.isfinite(root)
                ? 0.5f * (root + (pressureDeltaKPa * math.rcp(root)))
                : 0f;
        }

        private float ResolveLeakHoleAreaSquareMeters()
        {
            return Mathf.Max(0.0001f, moduleTemplate != null
                ? Mathf.Max(moduleTemplate.BreachAreaSquareMeters, breachHoleAreaSquareMeters)
                : breachHoleAreaSquareMeters);
        }

        private void EmitPressureIncursionVisuals(float deltaVolumeM3, float depthMeters)
        {
            if (deltaVolumeM3 <= 0f || depthMeters <= 0f)
                return;

            Vector3 localLeakPoint = _hasBreachCenterOfMassTarget
                ? _breachCenterOfMassTargetLocal
                : ResolveDefaultBreachLocalPoint();
            float pressureVisualScale = ResolveCinematicLeakSprayScale01(depthMeters, deltaVolumeM3);
            RegisterInstancedPressureSpray(localLeakPoint, pressureVisualScale);
        }

        internal static float ResolveCinematicLeakSprayScale01(float depthMeters, float floodDeltaM3)
        {
            if (depthMeters <= 0f && floodDeltaM3 <= 0f)
                return 0f;

            float depth01 = Mathf.Clamp01(depthMeters * CinematicLeakFullDepthMetersInv);
            float stagedDepth01 = depth01 * depth01 * (3f - (2f * depth01));
            float burst01 = Mathf.Clamp01(floodDeltaM3 * 0.35f);
            return Mathf.Clamp01(CinematicLeakBaseIntensity01 + (stagedDepth01 * 0.78f) + (burst01 * 0.22f));
        }

        private void SetWaterVolumeM3(float nextVolumeM3)
        {
            float capacityM3 = ResolveFloodCapacityM3();
            waterVolumeM3 = Mathf.Clamp(
                float.IsFinite(nextVolumeM3) ? nextVolumeM3 : 0f,
                0f,
                capacityM3);
        }

        private void SyncWaterVolumeToFloodFlag(bool flooded)
        {
            float capacityM3 = ResolveFloodCapacityM3();
            if (flooded)
            {
                if (waterVolumeM3 <= 0.0001f ||
                    !float.IsFinite(waterVolumeM3) ||
                    waterVolumeM3 > capacityM3)
                {
                    SetWaterVolumeM3(capacityM3);
                }
                return;
            }

            if (waterVolumeM3 > 0f || !float.IsFinite(waterVolumeM3))
                SetWaterVolumeM3(0f);
        }

        private float AddWaterVolumeM3Internal(float requestedVolumeM3, bool emitIncursionVisuals, float depthMeters)
        {
            if (requestedVolumeM3 <= 0f || !float.IsFinite(requestedVolumeM3))
                return 0f;

            float capacityM3 = ResolveFloodCapacityM3();
            if (waterVolumeM3 >= capacityM3)
                return 0f;

            bool wasFlooded = _integrityComponent.IsFlooded;
            float previousVolumeM3 = waterVolumeM3;
            SetWaterVolumeM3(math.min(capacityM3, waterVolumeM3 + requestedVolumeM3));
            float addedVolumeM3 = waterVolumeM3 - previousVolumeM3;
            if (addedVolumeM3 <= 0f)
                return 0f;

            if (emitIncursionVisuals)
                EmitPressureIncursionVisuals(addedVolumeM3, depthMeters);

            _integrityComponent.ForceFlood();
            UpdateFloodVisualStateImmediate();
            bool fireSuppressed = TryApplyFloodFireSuppression();
            if (!wasFlooded || fireSuppressed)
            {
                SyncTrackedObjectsFloodState();
                SyncSpatialRole();
                NotifyEmergencyLockdownStateChanged();
                BaseDegradationSystem.SynchronizeIntegrityState(this);
            }

            TryApplyFloodShortCircuit();
            return addedVolumeM3;
        }

        private bool TryApplyFloodFireSuppression()
        {
            if (_integrityComponent.FailureMode != BaseModuleFailureMode.Fire ||
                ResolveRuntimeFloodLevel01() < FloodFireSuppressionThreshold01)
            {
                return false;
            }

            ClearCascadeFailure();
            SetLeakActive(ShouldLeakBeActive());
            return true;
        }

        private bool TryApplyFloodShortCircuit()
        {
            if (_integrityComponent.FailureMode != BaseModuleFailureMode.None ||
                !_integrityComponent.IsFlooded ||
                !HasOperationalPower)
            {
                return false;
            }

            float floodLevel01 = ResolveRuntimeFloodLevel01();
            if (floodLevel01 < FloodShortCircuitThreshold01)
                return false;

            float hazard01 = math.saturate(
                (floodLevel01 - FloodShortCircuitThreshold01) *
                math.rcp(math.max(0.0001f, 1f - FloodShortCircuitThreshold01)));
            float tripChance01 = math.saturate(
                FloodShortCircuitBaseChance01 +
                hazard01 * (1f - FloodShortCircuitBaseChance01));
            if (ResolveFloodShortCircuitRoll01() > tripChance01)
                return false;

            TriggerCascadeFailure(BaseModuleFailureMode.ShortCircuit);

            if (_powerNode != null)
                _powerNode.SetShortCircuited(true);

            TryMarkPowerGridDirty();
            NotifyEmergencyLockdownStateChanged();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
            return true;
        }

        private float ResolveFloodShortCircuitRoll01()
        {
            ulong entityId = EntityId.ToULong(GetEntityId());
            uint hash = unchecked((uint)entityId ^ (uint)(entityId >> 32) ^ FloodShortCircuitHashSalt);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * FloodShortCircuitHashToUnit01;
        }

        private void RegisterFloodDryFatigueCycle()
        {
            fatigueDamage = Mathf.Max(0, fatigueDamage + 1);
            float fatigueCapRatio = Mathf.Max(0f, 1f - (fatigueDamage * 0.02f));
            float fatigueCap = Mathf.Max(0f, _integrityComponent.MaxIntegrity * fatigueCapRatio);
            _integrityComponent.ClampRepairIntegrityCap(fatigueCap);
            maxRecoverableIntegrity = _integrityComponent.MaxRecoverableIntegrity;
        }

        private float ResolveRuntimeFloodLevel01()
        {
            if (waterVolumeM3 > 0.0001f)
                return Mathf.Clamp01(waterVolumeM3 * ResolveInverseFloodCapacityM3());

            if (_integrityComponent.IsFlooded)
            {
                if (TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem) &&
                    atmosphereSystem != null)
                {
                    if (TryResolveCachedAtmosphereRoomIndex(atmosphereSystem, out int cachedRoomIndex))
                        return atmosphereSystem.ResolveRoomFloodFillNormalized(cachedRoomIndex);

                    if (atmosphereSystem.TryResolveRoomFloodFillNormalized(ResolveAtmosphereRoomProbeWorldPosition(), out int roomIndex, out float floodFill01))
                    {
                        if (!_isUnmoored)
                            _cachedAtmosphereRoomIndex = roomIndex;
                        return floodFill01;
                    }
                }

                return 1f;
            }

            _cachedAtmosphereRoomIndex = -1;
            return 0f;
        }

        private void CaptureFloodSurfaceDefaults()
        {
            if (_floodSurfaceDefaultsCaptured || floodSurfacePlane == null)
                return;

            _defaultFloodSurfaceLocalPosition = floodSurfacePlane.localPosition;
            _floodSurfaceDefaultsCaptured = true;
        }

        private void CapturePressureCompressionDefaults()
        {
            if (_pressureCompressionDefaultsCaptured || pressureCompressionVisualRoot == null)
                return;

            _defaultPressureCompressionVisualScale = pressureCompressionVisualRoot.localScale;
            _defaultPressureCompressionVisualRotation = pressureCompressionVisualRoot.localRotation;
            _pressureCompressionVisualRotationState = _defaultPressureCompressionVisualRotation;
            _pressureCompressionDefaultsCaptured = true;
        }

        private float ResolveFloodSurfaceMinimumLocalY()
        {
            if (!TryResolveInteriorFloodBounds(out float minimumLocalY, out _))
                return floodSurfaceLocalYRange.x;

            return minimumLocalY;
        }

        private float ResolveFloodSurfaceMaximumLocalY()
        {
            if (!TryResolveInteriorFloodBounds(out _, out float maximumLocalY))
                return floodSurfaceLocalYRange.y;

            return maximumLocalY;
        }

        private bool TryResolveInteriorFloodBounds(out float minimumLocalY, out float maximumLocalY)
        {
            minimumLocalY = floodSurfaceLocalYRange.x;
            maximumLocalY = floodSurfaceLocalYRange.y;

            if (interiorTrigger == null)
                return false;

            Transform triggerTransform = interiorTrigger.transform;
            Vector3 localCenter = transform.InverseTransformPoint(triggerTransform.TransformPoint(interiorTrigger.center));
            Vector3 lossyScale = triggerTransform.lossyScale;
            float halfHeight = interiorTrigger.size.y * 0.5f * math.abs(lossyScale.y);
            minimumLocalY = localCenter.y - halfHeight;
            maximumLocalY = localCenter.y + halfHeight;
            return maximumLocalY > minimumLocalY;
        }

        private void SetLightsEnabled(bool enabled)
        {
            if (interiorLights == null || interiorLights.Length == 0)
                return;

            int count = interiorLights.Length;
            for (int i = 0; i < count; i++)
            {
                Light l = interiorLights[i];
                if (l == null)
                    continue;

                if (l.shadows != LightShadows.None)
                    l.shadows = LightShadows.None;

                if (l.enabled != enabled)
                    l.enabled = enabled;
            }
        }

        private void QueueLightsEnabled(bool enabled)
        {
            _pendingInteriorLightsEnabled = enabled;
            _hasPendingInteriorLightsEnabled = true;
            if (Application.isPlaying)
                TryRegisterLateFrameTick();
        }

        private void FlushPendingInteriorLightState()
        {
            if (!_hasPendingInteriorLightsEnabled)
                return;

            _hasPendingInteriorLightsEnabled = false;
            SetLightsEnabled(_pendingInteriorLightsEnabled);
        }

        private void InitializeAmbienceNoiseSeed()
        {
            ulong entitySeed = EntityId.ToULong(GetEntityId());
            _brownoutNoiseSeed = (float)(entitySeed & 0xFFFFu) * 0.01731f;
        }

        private bool IsBrownoutFlickerActive()
        {
            return HasOperationalPower &&
                   _ambientLightsBrownedOut &&
                   _ambientVoltageSupplyRatio < Clamp01Finite(brownoutActivationVoltageRatio, 0.80f);
        }

        private bool ShouldAdvanceBrownoutShaderState()
        {
            return IsBrownoutFlickerActive() ||
                   Mathf.Abs(_brownoutTransition01 - _brownoutTransitionTarget01) > BrownoutShaderStateEpsilon ||
                   Mathf.Abs(_currentBrownoutFlicker01 - ResolveBrownoutShaderVoltage01()) > BrownoutShaderStateEpsilon;
        }

        private bool AdvanceBrownoutShaderState(float dt)
        {
            float previousTransition01 = _brownoutTransition01;
            float previousVoltage01 = _currentBrownoutFlicker01;
            AdvanceBrownoutTransition(dt);
            _currentBrownoutFlicker01 = ResolveBrownoutShaderVoltage01();
            return Mathf.Abs(previousTransition01 - _brownoutTransition01) > BrownoutShaderStateEpsilon ||
                   Mathf.Abs(previousVoltage01 - _currentBrownoutFlicker01) > BrownoutShaderStateEpsilon;
        }

        private float ResolveBrownoutShaderVoltage01()
        {
            if (!HasOperationalPower)
                return 0f;

            if (!_ambientLightsBrownedOut)
                return math.lerp(1f, 0f, math.saturate(_brownoutTransition01));

            float activationVoltage = MaxFinite(0.01f, brownoutActivationVoltageRatio, 0.80f);
            float voltage01 = Clamp01Finite(_ambientVoltageSupplyRatio / activationVoltage, 1f);
            return math.lerp(1f, Mathf.Max(Clamp01Finite(brownoutMinimumLightIntensityRatio, 0.04f), voltage01), math.saturate(_brownoutTransition01));
        }

        private void AdvanceBrownoutTransition(float dt)
        {
            float transitionSeconds = MaxFinite(0.05f, brownoutEmergencyTransitionSeconds, 0.5f);
            float safeDeltaTime = float.IsFinite(dt) ? Mathf.Max(0f, dt) : 0f;
            float transitionStep = math.saturate(safeDeltaTime / transitionSeconds);
            _brownoutTransition01 = math.lerp(_brownoutTransition01, _brownoutTransitionTarget01, transitionStep);
            if (Mathf.Abs(_brownoutTransition01 - _brownoutTransitionTarget01) <= BrownoutShaderStateEpsilon)
                _brownoutTransition01 = _brownoutTransitionTarget01;
        }

        private void ResetBrownoutShaderState()
        {
            _currentBrownoutFlicker01 = 1f;
            _brownoutTransition01 = 0f;
            _brownoutTransitionTarget01 = 0f;
            QueueActiveModuleWaterLevelsShaderUpload(true);
        }

        private void ConfigureOxygenScrubberHumSource()
        {
            if (oxygenScrubberHumSource == null)
            {
                _oxygenHumSourceConfigured = false;
                _configuredOxygenHumSource = null;
                _configuredOxygenHumClip = null;
                return;
            }

            if (_oxygenHumSourceConfigured &&
                ReferenceEquals(_configuredOxygenHumSource, oxygenScrubberHumSource) &&
                ReferenceEquals(_configuredOxygenHumClip, oxygenScrubberHumLoop))
            {
                return;
            }

            if (oxygenScrubberHumLoop != null && oxygenScrubberHumSource.clip != oxygenScrubberHumLoop)
                oxygenScrubberHumSource.clip = oxygenScrubberHumLoop;
            TryRouteAudioSourceToSfxGroup(oxygenScrubberHumSource);
            oxygenScrubberHumSource.loop = true;
            oxygenScrubberHumSource.playOnAwake = false;
            oxygenScrubberHumSource.volume = 0f;
            oxygenScrubberHumSource.pitch = ResolveOxygenScrubberHumFailPitch();
            _configuredOxygenHumSource = oxygenScrubberHumSource;
            _configuredOxygenHumClip = oxygenScrubberHumLoop;
            _oxygenHumSourceConfigured = true;
        }

        private void TryRouteAudioSourceToSfxGroup(AudioSource source)
        {
            if (source == null || source.outputAudioMixerGroup != null)
                return;

            ISpatialAudioSfxMixerRouteReadModel spatialAudioRoute = ResolveSpatialAudioSfxRoute();
            if (spatialAudioRoute != null)
                source.outputAudioMixerGroup = spatialAudioRoute.SfxGroup;
        }

        private void ResetOxygenScrubberHumRuntime(bool invalidateConfiguration)
        {
            _oxygenHum01 = 0f;
            _oxygenHumTarget01 = 0f;
            _oxygenHumActive = false;

            AudioSource source = oxygenScrubberHumSource;
            if (source != null)
            {
                if (source.isPlaying)
                    source.Stop();
                source.volume = 0f;
                source.pitch = ResolveOxygenScrubberHumFailPitch();
            }

            if (!invalidateConfiguration)
                return;

            _oxygenHumSourceConfigured = false;
            _configuredOxygenHumSource = null;
            _configuredOxygenHumClip = null;
        }

        private void UpdateOxygenScrubberHumTarget()
        {
            _oxygenHumTarget01 = HasOperationalPower &&
                                 _carbonFilterAvailable &&
                                 !_integrityComponent.IsFlooded
                ? 1f
                : 0f;
        }

        private void UpdateOxygenScrubberHum(float dt)
        {
            float fadeSeconds = _oxygenHumTarget01 > _oxygenHum01
                ? 0.25f
                : math.max(0.1f, oxygenScrubberHumFailFadeSeconds);
            float alpha = dt > 0f ? dt / (fadeSeconds + dt) : 1f;
            _oxygenHum01 = math.lerp(_oxygenHum01, _oxygenHumTarget01, alpha);
            _pendingOxygenHumVisualDirty = true;
            TryRegisterLateFrameTick();
        }

        private void FlushOxygenScrubberHumVisualState()
        {
            if (!_pendingOxygenHumVisualDirty)
                return;

            _pendingOxygenHumVisualDirty = false;
            if (oxygenScrubberHumSource == null)
                return;

            if (!_oxygenHumSourceConfigured ||
                !ReferenceEquals(_configuredOxygenHumSource, oxygenScrubberHumSource) ||
                !ReferenceEquals(_configuredOxygenHumClip, oxygenScrubberHumLoop))
            {
                ConfigureOxygenScrubberHumSource();
            }

            if (_oxygenHum01 <= 0.001f)
            {
                if (oxygenScrubberHumSource.isPlaying)
                    oxygenScrubberHumSource.Stop();
                oxygenScrubberHumSource.volume = 0f;
                _oxygenHumActive = false;
                return;
            }

            if (!oxygenScrubberHumSource.isPlaying)
                oxygenScrubberHumSource.Play();

            oxygenScrubberHumSource.volume = oxygenScrubberHumVolume * _oxygenHum01;
            oxygenScrubberHumSource.pitch = math.lerp(
                ResolveOxygenScrubberHumFailPitch(),
                math.max(0.01f, oxygenScrubberHumPoweredPitch),
                _oxygenHum01);
            _oxygenHumActive = true;
        }

        private float ResolveOxygenScrubberHumFailPitch()
        {
            return math.clamp(oxygenScrubberHumFailPitch, 0.2f, math.max(0.2f, oxygenScrubberHumPoweredPitch));
        }

        /// <summary>
        /// Odnorazovyy SFX u modulya cherez SpatialAudioManager (pul 3D). Lup utechki po-prezhnemu na <see cref="audioSource"/>.
        /// </summary>
        private void PlaySpatialSfx(AudioClip clip)
        {
            if (clip == null)
                return;

            QueueSpatialSfx(clip);
        }

        private void QueueSpatialSfx(AudioClip clip)
        {
            switch (_pendingSpatialSfxCount)
            {
                case 0:
                    _pendingSpatialSfx0 = clip;
                    break;
                case 1:
                    _pendingSpatialSfx1 = clip;
                    break;
                case 2:
                    _pendingSpatialSfx2 = clip;
                    break;
                case 3:
                    _pendingSpatialSfx3 = clip;
                    break;
                default:
                    return;
            }

            _pendingSpatialSfxCount++;
            TryRegisterLateFrameTick();
        }

        private void FlushPendingSpatialSfx()
        {
            if (_pendingSpatialSfxCount == 0)
                return;

            Hecton8.Core.IAudioService sam = ResolveAudioService();
            Vector3 position = ResolveInteriorHazardWorldPosition();
            byte count = _pendingSpatialSfxCount;
            AudioClip clip0 = _pendingSpatialSfx0;
            AudioClip clip1 = _pendingSpatialSfx1;
            AudioClip clip2 = _pendingSpatialSfx2;
            AudioClip clip3 = _pendingSpatialSfx3;
            _pendingSpatialSfx0 = null;
            _pendingSpatialSfx1 = null;
            _pendingSpatialSfx2 = null;
            _pendingSpatialSfx3 = null;
            _pendingSpatialSfxCount = 0;

            if (sam == null)
                return;

            if (count > 0 && clip0 != null)
                sam.PlayAtPoint(clip0, position);
            if (count > 1 && clip1 != null)
                sam.PlayAtPoint(clip1, position);
            if (count > 2 && clip2 != null)
                sam.PlayAtPoint(clip2, position);
            if (count > 3 && clip3 != null)
                sam.PlayAtPoint(clip3, position);
        }

        // ----------------------------------------------------------
        //  PRIVATE � DATA HELPERS
        // ----------------------------------------------------------

        private void CacheReferences()
        {
            if (_moduleMarker == null)
                TryGetComponent(out _moduleMarker);

            if (_moduleRigidbody == null)
                TryGetComponent(out _moduleRigidbody);

            if (leakVfx == null)
                ResolveLeakVfxReference();

            if (parasiteSporeVfx == null)
                ResolveParasiteSporeVfxReference();

            if (_habitatIntegrityManager == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!TryGetComponent(out _habitatIntegrityManager))
                    _habitatIntegrityManager = gameObject.AddComponent<HabitatIntegrityManager>();
#else
                TryGetComponent(out _habitatIntegrityManager);
#endif
            }

            if (_submarineAtmosphereSystem == null || !_submarineAtmosphereSystem.IsAtmosphereRuntimeActive)
                _submarineAtmosphereSystem = ComponentReferenceUtility.ResolveParentService<ISubmarineAtmosphereRoomMutationSink>(this);

            if (_powerNode == null)
                TryGetComponent(out _powerNode);

            if (_voxelVolume == null)
                TryResolveComponentInSelfOrParents(transform, out _voxelVolume);

            if (interiorTrigger == null)
                interiorTrigger = ComponentReferenceUtility.ResolveOwnedComponent<BoxCollider>(transform);

            CacheOwnedBulkheadComponents();
            CaptureModuleRigidbodyDefaults();
            CaptureFloodSurfaceDefaults();
        }

        private void CacheOwnedBulkheadComponents()
        {
            _airlockBuffer.Clear();
            GetComponentsInChildren(true, _airlockBuffer);
            _sealedDoorBuffer.Clear();
            GetComponentsInChildren(true, _sealedDoorBuffer);
        }

        private string ResolvePersistentId(ModuleMarker markerOverride)
        {
            ModuleMarker marker = markerOverride != null ? markerOverride : _moduleMarker;
            if (marker != null && !string.IsNullOrEmpty(marker.PrefabId))
                return marker.PrefabId;

            if (marker != null && marker.Data != null)
                return marker.Data.PersistentId;

            return string.Empty;
        }

        private bool EnsureUnmooredRigidbody()
        {
            if (_moduleRigidbody == null)
            {
                TryGetComponent(out _moduleRigidbody);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_moduleRigidbody == null)
                    _moduleRigidbody = gameObject.AddComponent<Rigidbody>();
#endif
            }

            if (_moduleRigidbody == null)
                return false;

            CaptureModuleRigidbodyDefaults();
            return true;
        }

        private void CaptureModuleRigidbodyDefaults()
        {
            if (_moduleBodyDefaultsCaptured)
                return;

            if (_moduleRigidbody == null)
                return;

            _defaultBodyMass = _moduleRigidbody.mass;
            _defaultLinearDamping = _moduleRigidbody.linearDamping;
            _defaultAngularDamping = _moduleRigidbody.angularDamping;
            _defaultBodyIsKinematic = _moduleRigidbody.isKinematic;
            _defaultBodyUseGravity = _moduleRigidbody.useGravity;
            _defaultCollisionDetectionMode = _moduleRigidbody.collisionDetectionMode;
            _defaultInterpolation = _moduleRigidbody.interpolation;
            _moduleBodyDefaultsCaptured = true;
        }

        private void EnableUnmooredPhysics()
        {
            if (!EnsureUnmooredRigidbody())
                return;

            _moduleRigidbody.isKinematic = false;
            _moduleRigidbody.useGravity = false;
            _moduleRigidbody.mass = Mathf.Max(MinimumMassKilograms, ResolveDryMassKilograms());
            _moduleRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void DisableUnmooredPhysics()
        {
            if (_moduleRigidbody == null || !_moduleBodyDefaultsCaptured)
                return;

            _moduleRigidbody.mass = _defaultBodyMass;
            _moduleRigidbody.linearDamping = _defaultLinearDamping;
            _moduleRigidbody.angularDamping = _defaultAngularDamping;
            _moduleRigidbody.useGravity = _defaultBodyUseGravity;
            _moduleRigidbody.isKinematic = _defaultBodyIsKinematic;
            _moduleRigidbody.collisionDetectionMode = _defaultCollisionDetectionMode;
            _moduleRigidbody.interpolation = _defaultInterpolation;
        }

        private void ResolveLeakVfxReference()
        {
            Transform leakTransform = transform.Find(LeakVfxChildName);
            if (leakTransform == null)
            {
                Transform lod0Transform = transform.Find("LOD0");
                if (lod0Transform != null)
                    leakTransform = lod0Transform.Find(LeakVfxChildName);
            }

            if (leakTransform != null)
                leakTransform.TryGetComponent(out leakVfx);
        }

        private void ResolveParasiteSporeVfxReference()
        {
            Transform sporeTransform = transform.Find("ParasiteSporeVfx");
            if (sporeTransform == null)
            {
                Transform lod0Transform = transform.Find("LOD0");
                if (lod0Transform != null)
                    sporeTransform = lod0Transform.Find("ParasiteSporeVfx");
            }

            if (sporeTransform != null)
                sporeTransform.TryGetComponent(out parasiteSporeVfx);
        }

        private Vector3 ResolveInteriorHazardWorldPosition()
        {
            if (interiorTrigger == null)
                return ResolveModuleFallbackWorldPosition();

            return interiorTrigger.transform.TransformPoint(interiorTrigger.center);
        }

        private Vector3 ResolveAtmosphereRoomProbeWorldPosition()
        {
            return TryGetInteriorAabbBounds(out Vector3 worldCenter, out _)
                ? worldCenter
                : ResolveInteriorHazardWorldPosition();
        }

        private int ResolveAtmosphereRoomIndex(ISubmarineAtmosphereRoomReadModel atmosphereSystem)
        {
            if (atmosphereSystem == null)
                return -1;

            if (TryResolveCachedAtmosphereRoomIndex(atmosphereSystem, out int cachedRoomIndex))
                return cachedRoomIndex;

            _cachedAtmosphereRoomIndex = -1;
            int roomIndex = atmosphereSystem.ResolveNearestRoomIndexForWorldPosition(ResolveAtmosphereRoomProbeWorldPosition());
            if (roomIndex >= 0 && !_isUnmoored)
                _cachedAtmosphereRoomIndex = roomIndex;

            return roomIndex;
        }

        private bool TryResolveCachedAtmosphereRoomIndex(ISubmarineAtmosphereRoomReadModel atmosphereSystem, out int roomIndex)
        {
            roomIndex = _cachedAtmosphereRoomIndex;
            if (_isUnmoored || atmosphereSystem == null || roomIndex < 0 || roomIndex >= atmosphereSystem.RoomCount)
            {
                _cachedAtmosphereRoomIndex = -1;
                roomIndex = -1;
                return false;
            }

            return true;
        }

        private Vector3 ResolveModuleFallbackWorldPosition()
        {
            return _moduleRigidbody != null
                ? _moduleRigidbody.worldCenterOfMass
                : transform.position;
        }

        private static Vector3 ResolveDominantLocalAxis(Vector3 localDirection)
        {
            float absX = math.abs(localDirection.x);
            float absY = math.abs(localDirection.y);
            float absZ = math.abs(localDirection.z);
            if ((absX + absY + absZ) <= 0.0001f)
                return Vector3.back;

            if (absX >= absY && absX >= absZ)
                return localDirection.x >= 0f ? Vector3.right : Vector3.left;

            if (absY >= absZ)
                return localDirection.y >= 0f ? Vector3.up : Vector3.down;

            return localDirection.z >= 0f ? Vector3.forward : Vector3.back;
        }

        private bool TryGetInteriorOverlapQuery(out Vector3 worldCenter, out Vector3 halfExtents, out Quaternion worldRotation)
        {
            worldCenter = default;
            halfExtents = default;
            worldRotation = Quaternion.identity;

            if (interiorTrigger == null)
                return false;

            Transform triggerTransform = interiorTrigger.transform;
            Vector3 lossyScale = triggerTransform.lossyScale;
            Vector3 absoluteScale = new Vector3(
                math.abs(lossyScale.x),
                math.abs(lossyScale.y),
                math.abs(lossyScale.z));

            worldCenter = triggerTransform.TransformPoint(interiorTrigger.center);
            halfExtents = Vector3.Scale(interiorTrigger.size * 0.5f, absoluteScale);
            worldRotation = triggerTransform.rotation;
            return halfExtents.x > 0f && halfExtents.y > 0f && halfExtents.z > 0f;
        }

        private float ResolveUnmooredFloodFillNormalized()
        {
            if (TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem) && atmosphereSystem != null)
            {
                if (TryResolveCachedAtmosphereRoomIndex(atmosphereSystem, out int cachedRoomIndex))
                    return atmosphereSystem.ResolveRoomFloodFillNormalized(cachedRoomIndex);

                if (atmosphereSystem.TryResolveRoomFloodFillNormalized(ResolveAtmosphereRoomProbeWorldPosition(), out int roomIndex, out float floodFill01))
                {
                    if (!_isUnmoored)
                        _cachedAtmosphereRoomIndex = roomIndex;
                    return floodFill01;
                }
            }

            _cachedAtmosphereRoomIndex = -1;
            return _integrityComponent.IsFlooded ? 1f : 0f;
        }

        private float ResolveExternalDepthMeters()
        {
            if (TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem) && atmosphereSystem != null)
                return atmosphereSystem.ResolveExternalDepthMeters();

            IAtmosphereReadModel atmosphereManager = _atmosphereRuntime;
            if (atmosphereManager != null)
                return ResolveExternalDepthMetersAup(atmosphereManager.SeaLevelY);

            return 0f;
        }

        private float ResolveExternalDepthMetersAup(float seaLevelRuntimeY)
        {
            Vector3 moduleRuntimePosition = ResolveAtmosphereRoomProbeWorldPosition();
            if (!TryResolveAupFromRuntimeOrigin(moduleRuntimePosition, out AbsoluteUniversePosition moduleAup))
                return 0f;

            moduleRuntimePosition.y = seaLevelRuntimeY;
            if (!TryResolveAupFromRuntimeOrigin(moduleRuntimePosition, out AbsoluteUniversePosition seaLevelAup))
                return 0f;

            double depthMeters = AbsoluteUniversePosition.DeltaMetersClamped(in seaLevelAup, in moduleAup).y;
            if (!math.isfinite(depthMeters) || depthMeters <= 0d)
                return 0f;

            return (float)math.min(depthMeters, (double)float.MaxValue);
        }

        private float ResolveDryMassKilograms()
        {
            if (moduleTemplate != null)
                return Mathf.Max(MinimumMassKilograms, moduleTemplate.StructuralDryMassKilograms);

            return Mathf.Max(MinimumMassKilograms, structuralDryMassKilograms);
        }

        private float ResolveBuoyancyDisplacementVolumeCubicMeters()
        {
            float volumeScale = float.IsFinite(_pressureCompressionVolumeScale)
                ? Mathf.Clamp(_pressureCompressionVolumeScale, 0.1f, 1f)
                : 1f;
            if (moduleTemplate != null)
                return MaxFinite(0.1f, moduleTemplate.BuoyancyDisplacementVolumeCubicMeters * volumeScale, 0.1f);

            return MaxFinite(0.1f, buoyancyDisplacementVolumeCubicMeters * volumeScale, 0.1f);
        }

        private float ResolveMaximumUnmooredAccelerationMetersPerSecondSquared()
        {
            if (moduleTemplate != null)
                return Mathf.Max(0.1f, moduleTemplate.MaximumUnmooredAccelerationMetersPerSecondSquared);

            return Mathf.Max(0.1f, maximumUnmooredAccelerationMetersPerSecondSquared);
        }

        private float ResolveMaximumHydroStructuralLoadNewtons()
        {
            return Mathf.Max(1f, maximumHydroStructuralLoadNewtons);
        }

        private float ResolveBulkheadFailureWaterMassKilograms()
        {
            float authoredFailureMass = Mathf.Max(1f, bulkheadFailureWaterMassKilograms);
            if (moduleTemplate == null)
                return authoredFailureMass;

            float yieldMass = Mathf.Max(1f, moduleTemplate.ModuleYieldStrengthNewtons / GravityAccelerationMetersPerSecondSquared);
            return Mathf.Max(authoredFailureMass, yieldMass);
        }

        private float ResolveBulkheadStressRatePerSecond()
        {
            return Mathf.Max(0.001f, bulkheadStressRatePerSecond);
        }

        private float ResolveBulkheadStressRecoveryPerSecond()
        {
            return Mathf.Max(0f, bulkheadStressRecoveryPerSecond);
        }

        private void ResetBulkheadFloodStress()
        {
            _bulkheadFloodStress01 = 0f;
            _jointShearStress01 = 0f;
            _bulkheadFailureLatched = false;
            _ruptureCascadeFailureQueued = false;
        }

        private void ClearQueuedHydroStructuralLoad()
        {
            _queuedHydroStructuralLoadNewtons = 0f;
            _queuedHydroStructuralLoadRemainingSeconds = 0f;
            _queuedHydroStructuralLoadPointWorld = Vector3.zero;
        }

        private void ApplyQueuedHydroStructuralLoad(float fixedDeltaTime)
        {
            if (_queuedHydroStructuralLoadRemainingSeconds <= 0f)
                return;

            if (_moduleRigidbody == null || _moduleRigidbody.isKinematic)
            {
                ClearQueuedHydroStructuralLoad();
                return;
            }

            float forceNewtons = Mathf.Min(ResolveMaximumHydroStructuralLoadNewtons(), _queuedHydroStructuralLoadNewtons);
            if (forceNewtons > 0f && float.IsFinite(forceNewtons))
            {
                _cachedPhysicsService?.QueueForceAtPosition(
                    _moduleRigidbody,
                    Vector3.down * forceNewtons,
                    _queuedHydroStructuralLoadPointWorld,
                    ForceMode.Force);
            }

            _queuedHydroStructuralLoadRemainingSeconds = Mathf.Max(0f, _queuedHydroStructuralLoadRemainingSeconds - fixedDeltaTime);
            if (_queuedHydroStructuralLoadRemainingSeconds <= 0f)
                ClearQueuedHydroStructuralLoad();
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return math.all(math.isfinite(aup.ToAbsoluteDouble3()));
        }

        private static float Clamp01Finite(float value, float fallback)
        {
            return Mathf.Clamp01(float.IsFinite(value) ? value : fallback);
        }

        private static float MaxFinite(float minimum, float value, float fallback)
        {
            return Mathf.Max(minimum, float.IsFinite(value) ? value : fallback);
        }

        private static Color ResolveFiniteColor(Color value, Color fallback)
        {
            return float.IsFinite(value.r) &&
                   float.IsFinite(value.g) &&
                   float.IsFinite(value.b) &&
                   float.IsFinite(value.a)
                ? value
                : fallback;
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private float ResolveMaximumFloodVisualLeanBiasMeters()
        {
            if (moduleTemplate != null)
                return Mathf.Max(0.01f, moduleTemplate.MaximumCenterOfMassShiftMeters);

            return Mathf.Max(0.01f, maximumFloodVisualLeanBiasMeters);
        }

        private float ResolveFloodVisualLeanTauSeconds()
        {
            if (moduleTemplate != null)
                return Mathf.Max(0.01f, moduleTemplate.CenterOfMassShiftTauSeconds);

            return Mathf.Max(0.01f, floodVisualLeanTauSeconds);
        }

        private void ApplyFloodVisualLean(float fixedDeltaTime, float floodFill01)
        {
            if (pressureCompressionVisualRoot == null || fixedDeltaTime <= 0f)
                return;

            CapturePressureCompressionDefaults();

            Vector3 localFloodBias = _hasBreachCenterOfMassTarget
                ? _breachCenterOfMassTargetLocal
                : ResolveDefaultBreachLocalPoint();
            localFloodBias.y = 0f;

            if (!IsFiniteVector(localFloodBias) || localFloodBias.sqrMagnitude <= 0.000001f || floodFill01 <= 0.001f)
            {
                BlendPressureVisualRootRotation(_defaultPressureCompressionVisualRotation, fixedDeltaTime);
                return;
            }

            float maxShift = ResolveMaximumFloodVisualLeanBiasMeters();
            if (localFloodBias.sqrMagnitude > (maxShift * maxShift))
            {
                float planarMax = math.max(math.abs(localFloodBias.x), math.abs(localFloodBias.z));
                float scale = maxShift / math.max(0.0001f, planarMax);
                localFloodBias.x *= scale;
                localFloodBias.z *= scale;
            }

            float normalizedX = Mathf.Clamp(localFloodBias.x / maxShift, -1f, 1f);
            float normalizedZ = Mathf.Clamp(localFloodBias.z / maxShift, -1f, 1f);
            float maxTiltDegrees = Mathf.Clamp(maxShift * 10f, 1f, 8f);
            Quaternion targetRotation = _defaultPressureCompressionVisualRotation *
                                        Quaternion.Euler(
                                            Mathf.Clamp(-normalizedZ * maxTiltDegrees * floodFill01, -maxTiltDegrees, maxTiltDegrees),
                                            0f,
                                            Mathf.Clamp(normalizedX * maxTiltDegrees * floodFill01, -maxTiltDegrees, maxTiltDegrees));

            BlendPressureVisualRootRotation(targetRotation, fixedDeltaTime);
        }

        private void BlendPressureVisualRootRotation(Quaternion targetRotation, float fixedDeltaTime)
        {
            if (pressureCompressionVisualRoot == null)
                return;

            if (!IsFiniteQuaternion(targetRotation))
            {
                QueuePressureCompressionVisualRotation(_defaultPressureCompressionVisualRotation);
                return;
            }

            float tauSeconds = ResolveFloodVisualLeanTauSeconds();
            float alpha = ResolveOneMinusExpPade(fixedDeltaTime / tauSeconds);
            QueuePressureCompressionVisualRotation(Quaternion.Lerp(
                _pressureCompressionVisualRotationState,
                targetRotation,
                alpha));
        }

        private static float ResolveOneMinusExpPade(float normalizedStep)
        {
            float x = float.IsFinite(normalizedStep) ? Mathf.Max(0f, normalizedStep) : 0f;
            float numerator = x * (6f + x);
            float denominator = 6f + (4f * x) + (x * x);
            return Mathf.Clamp01(numerator / Mathf.Max(denominator, 0.0001f));
        }

        private void NotifyEmergencyLockdownStateChanged()
        {
            ConstructionManager manager = _constructionManager;
            if (manager != null)
                manager.NotifyModuleEmergencyStateChanged(this);
        }

        private void TryTrackPlayer(Collider other, bool notifyEnter)
        {
            if (_trackedPlayerSurvival != null)
                return;

            if (!other.CompareTag("Player"))
                return;

            if (!TryResolveComponentInSelfOrParents(other.transform, out HectonSurvivalSystem resolvedSurvival))
                return;

            TrackPlayer(resolvedSurvival, other.transform, other, notifyEnter);
        }

        private void TryTrackPlayer(Transform playerTransform, bool notifyEnter)
        {
            if (_trackedPlayerSurvival != null || playerTransform == null)
                return;

            if (!TryResolveComponentInSelfOrParents(playerTransform, out HectonSurvivalSystem resolvedSurvival))
                return;

            TrackPlayer(resolvedSurvival, playerTransform, null, notifyEnter);
        }

        private void TrackPlayer(
            HectonSurvivalSystem resolvedSurvival,
            Transform playerTransform,
            Collider playerCollider,
            bool notifyEnter)
        {
            _trackedPlayerSurvival = resolvedSurvival;
            _lifeSupportComponent.BindTrackedSurvivalCold(resolvedSurvival);
            _trackedPlayerMovement = ResolvePlayerMovementEnvironmentSink(playerCollider, playerTransform);
            _trackedPlayerHypoxiaPresentation = ResolvePlayerHypoxiaPresentationSink(playerCollider, playerTransform);
            if (notifyEnter)
            {
                ModuleStatusEvents.TryNotifyEnter(this);
                PublishPlayerBaseTransitionSignal(true);
            }
        }

        private void TrackPlayerFromRuntime(
            IPlayerRuntimeContext playerRuntime,
            HectonSurvivalSystem resolvedSurvival,
            Transform playerTransform,
            bool notifyEnter)
        {
            _trackedPlayerSurvival = resolvedSurvival;
            _lifeSupportComponent.BindTrackedSurvivalCold(resolvedSurvival);
            HectonPlayerMovement movement = playerRuntime != null ? playerRuntime.PlayerMovement : null;
            _trackedPlayerMovement = movement;
            _trackedPlayerHypoxiaPresentation = movement;
            if (notifyEnter)
            {
                ModuleStatusEvents.TryNotifyEnter(this);
                PublishPlayerBaseTransitionSignal(true);
            }
        }

        private static IPlayerMovementEnvironmentSink ResolvePlayerMovementEnvironmentSink(Collider playerCollider, Transform playerTransform)
        {
            IPlayerMovementContracts registryContracts = Hecton8.Core.GlobalRegistry.PlayerMovementContracts;
            if (registryContracts != null)
                return registryContracts;

            if (playerCollider != null &&
                TryResolveComponentInSelfOrParents(playerCollider.transform, out IPlayerMovementEnvironmentSink colliderSink))
            {
                return colliderSink;
            }

            return playerTransform != null &&
                   TryResolveComponentInSelfOrParents(playerTransform, out IPlayerMovementEnvironmentSink transformSink)
                ? transformSink
                : null;
        }

        private static IPlayerHypoxiaPresentationSink ResolvePlayerHypoxiaPresentationSink(Collider playerCollider, Transform playerTransform)
        {
            if (playerCollider != null &&
                TryResolveComponentInSelfOrParents(playerCollider.transform, out IPlayerHypoxiaPresentationSink colliderSink))
            {
                return colliderSink;
            }

            return playerTransform != null &&
                   TryResolveComponentInSelfOrParents(playerTransform, out IPlayerHypoxiaPresentationSink transformSink)
                ? transformSink
                : null;
        }

        private bool IsTrackedPlayerCollider(Collider other)
        {
            if (_trackedPlayerSurvival == null || !other.CompareTag("Player"))
                return false;

            return TryResolveComponentInSelfOrParents(other.transform, out HectonSurvivalSystem resolvedSurvival) &&
                   ReferenceEquals(_trackedPlayerSurvival, resolvedSurvival);
        }

        private static bool TryResolveComponentInSelfOrParents<T>(Transform start, out T component)
        {
            component = default;

            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out component))
                    return true;
            }

            return false;
        }

        private void NotifyModuleExitIfNeeded()
        {
            if (_trackedPlayerSurvival == null)
                return;

            _trackedPlayerSurvival = null;
            _trackedPlayerMovement = null;
            _trackedPlayerHypoxiaPresentation = null;
            _lifeSupportComponent.ClearTrackedSurvivalCold();
            ModuleStatusEvents.TryNotifyExit(this);
            PublishPlayerBaseTransitionSignal(false);
        }

        private void PublishPlayerBaseTransitionSignal(bool playerInside)
        {
            if (!Application.isPlaying)
                return;

            int roomId = -1;
            TryResolveHostAtmosphereRoomIndex(out roomId);

            Vector3 center = ResolveAtmosphereRoomProbeWorldPosition();
            if (!IsFiniteVector(center))
                center = transform.position;
            if (!IsFiniteVector(center))
                return;

            if (!TryResolveAupFromRuntimeOrigin(center, out AbsoluteUniversePosition centerAup))
                return;

            uint frame = SystemDispatcher.CurrentFrameId;
            if (playerInside)
            {
                PlayerBaseEnterSignal signal = new PlayerBaseEnterSignal
                {
                    BaseCenterAup = centerAup,
                    BaseId = DefaultHabitatBaseId,
                    RoomId = roomId,
                    Frame = frame,
                    Flags = PlayerBaseEnterSignal.DirectPlayerInsideFlag
                };
                SignalBus<PlayerBaseEnterSignal>.TryPushTracked(in signal, ref s_x001BaseModuleSignalPushDropCount);
                return;
            }

            PlayerBaseExitSignal exitSignal = new PlayerBaseExitSignal
            {
                BaseCenterAup = centerAup,
                BaseId = DefaultHabitatBaseId,
                RoomId = roomId,
                Frame = frame,
                Flags = PlayerBaseExitSignal.DirectPlayerOutsideFlag
            };
            SignalBus<PlayerBaseExitSignal>.TryPushTracked(in exitSignal, ref s_x001BaseModuleSignalPushDropCount);
        }

        private void ReadBuildablePower()
        {
            if (_moduleMarker != null && _moduleMarker.Data != null)
            {
                _basePowerRating = _moduleMarker.Data.powerRating;
                powerPriority    = _moduleMarker.Data.powerPriority;
                if (_moduleMarker.Data.ModuleTemplate != null)
                    moduleTemplate = _moduleMarker.Data.ModuleTemplate;
            }
            else
            {
                _basePowerRating = fallbackPowerRating;
            }
        }

        internal void ApplyBuildableTemplate(BuildableData data, BoxCollider runtimeInteriorTrigger = null)
        {
            if (data != null)
            {
                moduleTemplate = data.ModuleTemplate;
                _basePowerRating = data.powerRating;
                powerPriority = data.powerPriority;
            }

            if (runtimeInteriorTrigger != null)
                interiorTrigger = runtimeInteriorTrigger;

            ConfigureRuntimeComponentsFromSerializedState();
            RefreshVisualStateImmediate();
        }

        private void ValidateInteriorTrigger()
        {
            if (interiorTrigger != null)
            {
                if (!interiorTrigger.isTrigger)
                {
                    interiorTrigger.isTrigger = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(
                        $"[BaseModule] interiorTrigger on '{gameObject.name}' was not set as Trigger. " +
                        "Fixed automatically.", this);
#endif
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Hecton8.Core.H8Debug.LogWarning(
                    $"[BaseModule] '{gameObject.name}' has no interiorTrigger assigned. " +
                    "Interior Zone (Dry Zone) will not function.", this);
            }
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateTrackedDiagnostics()
        {
            _debugTrackedObjectCount = 0;
        }

        internal bool ResolveStructuralAnchorRole(ModuleMarker markerOverride = null)
        {
            if (moduleTemplate != null && moduleTemplate.IsStructuralAnchor)
                return true;

            if (isStructuralAnchor)
                return true;

            string persistentId = ResolvePersistentId(markerOverride);
            return string.Equals(persistentId, FoundationPersistentId, StringComparison.Ordinal) ||
                   string.Equals(persistentId, PylonPersistentId, StringComparison.Ordinal);
        }

        internal bool ResolveEmergencyAirlockRole(ModuleMarker markerOverride = null)
        {
            if (moduleTemplate != null && moduleTemplate.IsEmergencyAirlock)
                return true;

            if (isEmergencyAirlock)
                return true;

            if (_airlockBuffer.Count > 0)
                return true;

            string persistentId = ResolvePersistentId(markerOverride);
            return string.Equals(persistentId, AirlockPersistentId, StringComparison.Ordinal) ||
                   string.Equals(persistentId, LegacyAirlockPersistentId, StringComparison.Ordinal);
        }

        internal void SetAnchoredState(bool anchored)
        {
            if (_detachedAsDebris && anchored)
                return;

            bool nextUnmoored = !anchored;
            if (_isUnmoored == nextUnmoored)
                return;

            _isUnmoored = nextUnmoored;
            if (_isUnmoored)
            {
                EnableUnmooredPhysics();
                TryRegisterFixedTick();
                return;
            }

            TryUnregisterFixedTick();
            DisableUnmooredPhysics();
        }

        internal bool TryDetachAsSinkingDebris()
        {
            if (_detachedAsDebris)
                return false;

            _detachedAsDebris = true;
            _isUnmoored = true;
            ReleaseAllTrackedObjects();
            if (EnsureUnmooredRigidbody())
            {
                _moduleRigidbody.isKinematic = false;
                _moduleRigidbody.useGravity = true;
                _moduleRigidbody.mass = Mathf.Max(
                    MinimumMassKilograms,
                    ResolveDryMassKilograms() + ResolveFloodWaterMassKilograms());
                _moduleRigidbody.linearDamping = Mathf.Max(_moduleRigidbody.linearDamping, 0.1f);
                _moduleRigidbody.angularDamping = Mathf.Max(_moduleRigidbody.angularDamping, 0.1f);
                _moduleRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                _moduleRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            TryRegisterFixedTick();
            ConstructionManager manager = _constructionManager;
            if (manager != null)
                manager.NotifyModuleDetachedAsDebris(this);
            BaseDegradationSystem.SynchronizeIntegrityState(this);
            return true;
        }

        internal void ApplyConstructedWeldSnap(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!IsFiniteVector(targetPosition) || !IsFiniteQuaternion(targetRotation))
                return;

            CacheReferences();
            _isUnmoored = false;
            TryUnregisterFixedTick();
            ClearQueuedHydroStructuralLoad();

            if (_moduleRigidbody == null)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            CaptureModuleRigidbodyDefaults();
            IPhysicsService physicsService = _cachedPhysicsService ?? Hecton8.Core.GlobalRegistry.Physics;
            if (physicsService == null || !physicsService.ApplyKinematicWeldSnap(_moduleRigidbody, targetPosition, targetRotation))
                transform.SetPositionAndRotation(targetPosition, targetRotation);

            _defaultBodyIsKinematic = true;
            _defaultBodyUseGravity = false;
            _defaultCollisionDetectionMode = _moduleRigidbody.collisionDetectionMode;
            _defaultInterpolation = _moduleRigidbody.interpolation;
            _moduleBodyDefaultsCaptured = true;
        }

        public bool TryResolveRepairSnapPoints(
            Vector3 runtimeHitPoint,
            out AbsoluteUniversePosition leftHandAup,
            out AbsoluteUniversePosition rightHandAup,
            out Quaternion toolRotation)
        {
            leftHandAup = default;
            rightHandAup = default;
            toolRotation = Quaternion.identity;
            if (!IsFiniteVector(runtimeHitPoint))
                return false;

            Transform moduleTransform = transform;
            Vector3 right = moduleTransform.right;
            Vector3 up = moduleTransform.up;
            Vector3 forward = moduleTransform.forward;
            if (!IsFiniteVector(right) || right.sqrMagnitude <= 0.000001f)
                right = Vector3.right;

            if (!IsFiniteVector(up) || up.sqrMagnitude <= 0.000001f)
                up = Vector3.up;

            if (!IsFiniteVector(forward) || forward.sqrMagnitude <= 0.000001f)
                forward = Vector3.forward;

            Vector3 handCenter = runtimeHitPoint + up * RepairHandVerticalBiasMeters;
            Vector3 leftRuntime = handCenter - right * RepairHandHalfSpanMeters;
            Vector3 rightRuntime = handCenter + right * RepairHandHalfSpanMeters;
            if (!TryResolveAupFromRuntimeOrigin(leftRuntime, out leftHandAup) ||
                !TryResolveAupFromRuntimeOrigin(rightRuntime, out rightHandAup))
            {
                leftHandAup = default;
                rightHandAup = default;
                return false;
            }

            toolRotation = Quaternion.LookRotation(forward, up);
            return IsFiniteQuaternion(toolRotation);
        }

        public bool TryResolveKinematicRepairSnap(
            in Hecton8.Interaction.KinematicRepairTargetProbe probe,
            out Hecton8.Interaction.KinematicRepairSnapPoint snapPoint)
        {
            snapPoint = default;
            float3 runtimeHit = probe.HitAup.ToRuntimeFloat3();
            Vector3 runtimeHitPoint = new Vector3(runtimeHit.x, runtimeHit.y, runtimeHit.z);
            if (!TryResolveRepairSnapPoints(
                    runtimeHitPoint,
                    out AbsoluteUniversePosition leftHandAup,
                    out AbsoluteUniversePosition rightHandAup,
                    out Quaternion toolRotation))
            {
                return false;
            }

            float3 leftRuntime = leftHandAup.ToRuntimeFloat3();
            float3 rightRuntime = rightHandAup.ToRuntimeFloat3();
            Vector3 runtimeAnchor = new Vector3(
                (leftRuntime.x + rightRuntime.x) * 0.5f,
                (leftRuntime.y + rightRuntime.y) * 0.5f,
                (leftRuntime.z + rightRuntime.z) * 0.5f);
            if (!IsFiniteVector(runtimeAnchor))
                runtimeAnchor = runtimeHitPoint;

            Vector3 surfaceNormal = IsFiniteVector(probe.HitNormal) && probe.HitNormal.sqrMagnitude > 0.000001f
                ? probe.HitNormal
                : toolRotation * Vector3.forward;
            if (!TryResolveAupFromRuntimeOrigin(runtimeAnchor, out AbsoluteUniversePosition anchorAup))
                return false;

            snapPoint = new Hecton8.Interaction.KinematicRepairSnapPoint
            {
                AnchorAup = anchorAup,
                LeftHandAup = leftHandAup,
                RightHandAup = rightHandAup,
                RuntimePosition = runtimeAnchor,
                SurfaceNormal = surfaceNormal,
                ToolRotation = toolRotation,
                HitDistance = math.max(0f, probe.HitDistance),
                Blend = 1f,
                ColliderInstanceId = probe.ColliderInstanceId
            };
            return true;
        }

        internal void SetEmergencyBulkheadLockdown(bool lockedDown)
        {
            SetEmergencyBulkheadLockdown(lockedDown, false);
        }

        internal void SetEmergencyBulkheadLockdown(bool lockedDown, bool blockManualOverride)
        {
            if (!ResolveEmergencyAirlockRole())
                return;

            _emergencyBulkheadLockedDown = lockedDown;
            CacheOwnedBulkheadComponents();
            for (int i = 0; i < _airlockBuffer.Count; i++)
            {
                BaseAirlock airlock = _airlockBuffer[i];
                if (airlock != null)
                {
                    airlock.SetEmergencyLockdown(lockedDown);
                    airlock.SetEmergencyLockdownOverrideBlocked(lockedDown && blockManualOverride);
                }
            }

            for (int i = 0; i < _sealedDoorBuffer.Count; i++)
            {
                SealedDoor sealedDoor = _sealedDoorBuffer[i];
                if (sealedDoor == null)
                    continue;

                if (lockedDown)
                    sealedDoor.Lock();
                else
                    sealedDoor.Unlock();
            }
        }

        private void RefreshOwnedAirlockPressurizationSnapshots()
        {
            CacheOwnedBulkheadComponents();
            for (int i = 0; i < _airlockBuffer.Count; i++)
            {
                BaseAirlock airlock = _airlockBuffer[i];
                if (airlock != null)
                    airlock.RequestPressurizationSnapshotRefresh();
            }
        }

        private void TryRegister()
        {
            if (_tickRegistered)
                return;
            if (!Application.isPlaying)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void UpdateAmbienceTickRegistration()
        {
            if (ShouldRunAmbienceTick())
                TryRegisterUpdatable();
            else
                TryUnregisterUpdatable();
        }

        private bool ShouldRunAmbienceTick()
        {
            return math.abs(_brownoutTransition01 - _brownoutTransitionTarget01) > 0.001f ||
                   math.abs(_oxygenHum01 - _oxygenHumTarget01) > 0.001f ||
                   _oxygenHumActive;
        }

        private void TryRegisterUpdatable()
        {
            if (_updatableRegistered)
                return;
            if (!Application.isPlaying)
                return;

            _updatableRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_updatableRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _updatableRegistered = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered)
                return;
            if (!Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
        }

        private void TryRegisterFixedTick()
        {
            if (_fixedTickRegistered || !_isUnmoored)
                return;
            if (!Application.isPlaying)
                return;

            _fixedTickRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
        }

        private void CacheRegistryServicesCold()
        {
            _atmosphereRuntime = Hecton8.Core.GlobalRegistry.AtmosphereReadModel;
            _cachedPlayerInventoryService = Hecton8.Core.GlobalRegistry.PlayerInventory;
            CacheAudioService(Hecton8.Core.GlobalRegistry.Audio);
            CacheObjectPoolService(null);
            _cachedPlayerRuntime = Hecton8.Core.GlobalRegistry.Player;
            _cachedPhysicsService = Hecton8.Core.GlobalRegistry.Physics;
            _constructionManager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            if (!ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate))
            {
                _cachedObjectPool = null;
                return;
            }

            _cachedObjectPool = candidate;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _cachedObjectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _cachedObjectPool = resolved;
                pool = resolved;
                return true;
            }

            _cachedObjectPool = null;
            pool = null;
            return false;
        }

        private static void DespawnWithPoolOrDeactivate(GameObject instance, IObjectPoolService preferredPool)
        {
            ObjectPoolManager.DespawnOrDeactivate(instance, preferredPool);
        }

        private void CacheAudioService(Hecton8.Core.IAudioService audioService)
        {
            if (!IsAudioServiceUsable(audioService))
            {
                ClearCachedAudioService();
                return;
            }

            _cachedAudioService = audioService;
            _cachedSpatialAudioSfxRoute = audioService as ISpatialAudioSfxMixerRouteReadModel;
        }

        private Hecton8.Core.IAudioService ResolveAudioService()
        {
            Hecton8.Core.IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            ClearCachedAudioService();
            return null;
        }

        private ISpatialAudioSfxMixerRouteReadModel ResolveSpatialAudioSfxRoute()
        {
            return ResolveAudioService() != null ? _cachedSpatialAudioSfxRoute : null;
        }

        private void ClearCachedAudioService()
        {
            _cachedAudioService = null;
            _cachedSpatialAudioSfxRoute = null;
        }

        private static bool IsAudioServiceUsable(Hecton8.Core.IAudioService audioService)
        {
            if (audioService == null || !audioService.IsInitialized)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void ClearCachedRegistryServices()
        {
            _atmosphereRuntime = null;
            _cachedPlayerInventoryService = null;
            ClearCachedAudioService();
            _cachedObjectPool = null;
            _cachedPlayerRuntime = null;
            _cachedPhysicsService = null;
            _constructionManager = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryRegisterElectromagneticPulseListener()
        {
            if (_empListenerRegistered)
                return;

            IPhysicsService physicsService = _cachedPhysicsService;
            if (physicsService == null)
                return;

            physicsService.RegisterElectromagneticPulseListener(this);
            _empListenerRegistered = true;
        }

        private void TryUnregisterElectromagneticPulseListener()
        {
            if (!_empListenerRegistered)
                return;

            _cachedPhysicsService?.UnregisterElectromagneticPulseListener(this);
            _empListenerRegistered = false;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.AtmosphereRuntime)
                _atmosphereRuntime = currentService as IAtmosphereReadModel;
            else if (serviceSlot == GlobalRegistryServiceSlot.PlayerInventory)
                _cachedPlayerInventoryService = currentService as IPlayerInventoryService;
            else if (serviceSlot == GlobalRegistryServiceSlot.Audio)
            {
                CacheAudioService(currentService as Hecton8.Core.IAudioService);
                TryRouteAudioSourceToSfxGroup(audioSource);
                TryRouteAudioSourceToSfxGroup(oxygenScrubberHumSource);
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)
            {
                CacheObjectPoolService(currentService as ObjectPoolManager);
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerRuntime = currentService as IPlayerRuntimeContext;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                if (_empListenerRegistered && previousService is IPhysicsService previousPhysicsService)
                    previousPhysicsService.UnregisterElectromagneticPulseListener(this);

                _empListenerRegistered = false;
                _cachedPhysicsService = currentService as IPhysicsService;
                if (isActiveAndEnabled)
                    TryRegisterElectromagneticPulseListener();
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Logistics)
            {
                _constructionManager = currentService as ConstructionManager;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RefreshOwnedAirlockPressurizationSnapshots();
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null)
            {
                RefreshOwnedAirlockPressurizationSnapshots();
            }
        }

        private void TryUnregisterFixedTick()
        {
            if (!_fixedTickRegistered)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _fixedTickRegistered = false;
        }

        /// <summary>
        /// Allows botanical modules to chemically pull CO2 out of this module's dry-air loop.
        /// </summary>
        public void ApplyBotanyScrub(float amount)
        {
            _lifeSupportComponent.ScrubCo2(amount);
        }

        /// <summary>
        /// Allows cultivation modules to inject additional oxygen into the owning room atmosphere.
        /// </summary>
        public void ApplyCultivationOxygen(float oxygenUnits)
        {
            if (oxygenUnits <= 0f || !TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem) || atmosphereSystem == null)
                return;

            int roomIndex = ResolveAtmosphereRoomIndex(atmosphereSystem);
            if (roomIndex < 0)
                return;

            atmosphereSystem.InjectOxygenUnits(roomIndex, oxygenUnits);
        }

        /// <summary>
        /// Allows cultivation modules to compute mature oxygen-producing slot output without exposing storage.
        /// </summary>
        public void ApplyCultivationOxygen(CultivationManager cultivationManager, float oxygenUnitsPerMaturePlant)
        {
            if (cultivationManager == null || oxygenUnitsPerMaturePlant <= 0f)
                return;

            ApplyCultivationOxygen(cultivationManager.CalculateMatureOxygenUnits(oxygenUnitsPerMaturePlant));
        }

        internal float ParasiteInfectionLevel => _parasiteInfectionLevel;

        internal bool SetParasiteInfestation(float powerDrainWatts, float infectionLevel)
        {
            return SetParasiteInfestation(powerDrainWatts, infectionLevel, 0f, 0f, 0);
        }

        internal bool SetParasiteInfestation(float powerDrainWatts, float infectionLevel, float rootPowerDrainWatts, float rootInfectionLevel)
        {
            return SetParasiteInfestation(powerDrainWatts, infectionLevel, rootPowerDrainWatts, rootInfectionLevel, 0);
        }

        internal bool SetParasiteInfestation(float powerDrainWatts, float infectionLevel, float rootPowerDrainWatts, float rootInfectionLevel, int attachedParasiteCount)
        {
            float sanitizedDrain = Mathf.Max(0f, powerDrainWatts);
            float sanitizedInfection = Mathf.Clamp01(infectionLevel);
            float sanitizedRootDrain = Mathf.Max(0f, rootPowerDrainWatts);
            float sanitizedRootInfection = Mathf.Clamp01(rootInfectionLevel);
            int sanitizedParasiteCount = Mathf.Max(0, attachedParasiteCount);
            if (Mathf.Abs(_parasitePowerDrainWatts - sanitizedDrain) <= 0.01f &&
                Mathf.Abs(_parasiteInfectionLevel - sanitizedInfection) <= 0.001f &&
                Mathf.Abs(_parasiteRootPowerDrainWatts - sanitizedRootDrain) <= 0.01f &&
                Mathf.Abs(_parasiteRootInfectionLevel - sanitizedRootInfection) <= 0.001f &&
                _attachedParasiteCount == sanitizedParasiteCount)
            {
                return false;
            }

            bool sporeStateChanged = _attachedParasiteCount != sanitizedParasiteCount ||
                                     Mathf.Abs(_parasiteInfectionLevel - sanitizedInfection) > 0.001f ||
                                     Mathf.Abs(_parasiteRootInfectionLevel - sanitizedRootInfection) > 0.001f;
            _parasitePowerDrainWatts = sanitizedDrain;
            _parasiteInfectionLevel = sanitizedInfection;
            _parasiteRootPowerDrainWatts = sanitizedRootDrain;
            _parasiteRootInfectionLevel = sanitizedRootInfection;
            _attachedParasiteCount = sanitizedParasiteCount;
            TryMarkPowerGridDirty();
            if (sporeStateChanged)
            {
                BaseDegradationSystem.SynchronizeParasiteSporeHazard(this);
                BaseDegradationSystem.SynchronizeIntegrityState(this);
            }
            return true;
        }

        internal bool SetParasiteStructuralEffects(float addedMassKilograms, float thermalInsulation01, float bioReactorOverheatMultiplier)
        {
            float sanitizedMass = Mathf.Max(0f, addedMassKilograms);
            float sanitizedInsulation = Mathf.Clamp01(thermalInsulation01);
            float sanitizedOverheatMultiplier = Mathf.Max(1f, bioReactorOverheatMultiplier);
            if (Mathf.Abs(_parasiteAddedMassKilograms - sanitizedMass) <= 0.1f &&
                Mathf.Abs(_parasiteThermalInsulation01 - sanitizedInsulation) <= 0.001f &&
                Mathf.Abs(_parasiteBioReactorOverheatMultiplier - sanitizedOverheatMultiplier) <= 0.001f)
            {
                return false;
            }

            _parasiteAddedMassKilograms = sanitizedMass;
            _parasiteThermalInsulation01 = sanitizedInsulation;
            _parasiteBioReactorOverheatMultiplier = sanitizedOverheatMultiplier;
            return true;
        }

        internal bool SetCultivationScrubberLoad(float powerDrainWatts)
        {
            float sanitizedDrain = Mathf.Max(0f, powerDrainWatts);
            if (Mathf.Abs(_cultivationScrubberPowerDrainWatts - sanitizedDrain) <= 0.01f)
                return false;

            _cultivationScrubberPowerDrainWatts = sanitizedDrain;
            TryMarkPowerGridDirty();
            return true;
        }

        internal bool SetCultivationLightingPowerCredit(float powerCreditWatts)
        {
            float sanitizedCredit = Mathf.Max(0f, powerCreditWatts);
            if (Mathf.Abs(_cultivationLightingPowerCreditWatts - sanitizedCredit) <= 0.01f)
                return false;

            _cultivationLightingPowerCreditWatts = sanitizedCredit;
            TryMarkPowerGridDirty();
            return true;
        }

        internal void EmitCultivationRotIntoFloodWater(float intensity, float co2Amplifier)
        {
            if (intensity <= 0f || !_integrityComponent.IsFlooded)
                return;

            Vector3 center = ResolveBotanyAnchorWorldPosition();
            if (TryGetInteriorHazardBounds(out Vector3 worldCenter, out _))
                center = worldCenter;

            float clampedIntensity = Mathf.Clamp01(intensity);
            ChemicalInfluenceGrid.QueueToxicityBurst(center, clampedIntensity);
            ApplyFloodExposure(Mathf.Clamp01(clampedIntensity * 0.025f), Mathf.Max(0f, co2Amplifier));
        }

        internal bool TryGetParasiteSporeHazard(out Vector3 position, out float radius, out float intensity)
        {
            position = ResolveInteriorHazardWorldPosition();
            radius = Mathf.Max(0.1f, parasiteSporeHazardRadius);
            intensity = 0f;
            if (_attachedParasiteCount <= ParasiteSporeHazardThreshold)
                return false;

            float overgrowth01 = Mathf.Clamp01((_attachedParasiteCount - ParasiteSporeHazardThreshold) * 0.2f);
            float infection01 = Mathf.Clamp01(Mathf.Max(_parasiteInfectionLevel, _parasiteRootInfectionLevel));
            intensity = Mathf.Clamp01(0.35f + infection01 * 0.45f + overgrowth01 * 0.2f);
            return intensity > 0.001f;
        }

        internal void SetParasiteSporeVfxActive(bool active)
        {
            _pendingParasiteSporeVfxActive = active;
            _pendingParasiteSporeVfxDirty = true;
            TryRegisterLateFrameTick();
        }

        private void FlushParasiteSporeVfxState()
        {
            if (parasiteSporeVfx == null)
                return;

            if (!_pendingParasiteSporeVfxDirty)
                return;

            _pendingParasiteSporeVfxDirty = false;
            bool active = _pendingParasiteSporeVfxActive;
            if (active)
            {
                parasiteSporeVfx.transform.position = ResolveInteriorHazardWorldPosition();
                if (!parasiteSporeVfx.isPlaying)
                    parasiteSporeVfx.Play(true);
                return;
            }

            if (parasiteSporeVfx.isPlaying)
                parasiteSporeVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        internal Vector3 ResolveBotanyAnchorWorldPosition()
        {
            if (TryGetDegradationSockets(out BaseModuleTemplate.VfxSocket[] sockets) && sockets != null && sockets.Length > 0)
            {
                var localPosition = sockets[0].LocalPosition;
                return transform.TransformPoint(new Vector3(localPosition.x, localPosition.y, localPosition.z));
            }

            return ResolveInteriorHazardWorldPosition();
        }

        internal float ResolveHostRoomTemperatureCelsius()
        {
            if (!TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem) ||
                atmosphereSystem == null)
            {
                return 0f;
            }

            int roomIndex = ResolveAtmosphereRoomIndex(atmosphereSystem);
            return roomIndex >= 0
                ? atmosphereSystem.GetRoomTemperatureCelsius(roomIndex)
                : 0f;
        }

        internal bool TryResolveHostAtmosphereRoomIndex(out int roomIndex)
        {
            roomIndex = -1;
            if (!TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem) ||
                atmosphereSystem == null)
            {
                return false;
            }

            roomIndex = ResolveAtmosphereRoomIndex(atmosphereSystem);
            return roomIndex >= 0;
        }

        internal bool TryInjectHostRoomTemperatureDeltaCelsius(float deltaCelsius)
        {
            if (!(deltaCelsius > 0f) || !float.IsFinite(deltaCelsius))
                return false;

            if (!TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem) || atmosphereSystem == null)
                return false;

            int roomIndex = ResolveAtmosphereRoomIndex(atmosphereSystem);
            if (roomIndex < 0)
                return false;

            atmosphereSystem.InjectRoomTemperatureDeltaCelsius(roomIndex, deltaCelsius);
            return true;
        }

        internal bool TryGetHostedBioReactor(out BioReactor reactor)
        {
            if (TryGetComponent(out reactor))
                return reactor != null;

            reactor = ComponentReferenceUtility.ResolveOwnedComponent<BioReactor>(transform);
            return reactor != null;
        }

        private void TryMarkPowerGridDirty()
        {
            if (_powerNode == null || _powerNode.Grid == null)
                return;

            _powerNode.Grid.MarkDirty();
        }

        internal void ApplyFloodExposure(float normalizedFloodDelta, float co2Amplifier)
        {
            _lifeSupportComponent.ApplyFloodExposure(normalizedFloodDelta, co2Amplifier);
        }

        private void UpdateFloodedReefGrowth(float deltaTime)
        {
            if (!_integrityComponent.IsFlooded)
            {
                if (!_interiorReefInfestationActive)
                    _floodedReefFloodSeconds = 0f;

                return;
            }

            if (_interiorReefInfestationActive)
            {
                RegisterFloodedReefFaunaAnchor();
                return;
            }

            _floodedReefFloodSeconds += Mathf.Max(0f, deltaTime);
            if (_floodedReefFloodSeconds < ResolveFloodedReefActivationSeconds())
                return;

            _interiorReefInfestationActive = true;
            SetInteriorReefVisualActive(true);
            TryMarkPowerGridDirty();
            RegisterFloodedReefFaunaAnchor();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        private void RegisterFloodedReefFaunaAnchor()
        {
            WorldFaunaSpawnRegistry registry = WorldFaunaSpawnRegistry.ActiveRuntimeInstance;
            if (registry == null)
                return;

            Vector3 anchorPosition = ResolveInteriorHazardWorldPosition();
            float anchorRadius = parasiteSporeHazardRadius;
            if (TryGetInteriorHazardBounds(out Vector3 interiorCenter, out float interiorRadius))
            {
                anchorPosition = interiorCenter;
                anchorRadius = interiorRadius;
            }

            registry.RegisterRuntimeReefAnchor(
                ResolveFloodedReefFaunaAnchorKey(),
                anchorPosition,
                Mathf.Max(4f, anchorRadius),
                FloodedReefFaunaFamilyId);
        }

        private void UnregisterFloodedReefFaunaAnchor()
        {
            WorldFaunaSpawnRegistry registry = WorldFaunaSpawnRegistry.ActiveRuntimeInstance;
            if (registry == null)
                return;

            registry.UnregisterRuntimeReefAnchor(ResolveFloodedReefFaunaAnchorKey());
        }

        private long ResolveFloodedReefFaunaAnchorKey()
        {
            return unchecked((long)EntityId.ToULong(GetEntityId()) ^ FloodedReefFaunaAnchorSalt);
        }

        private bool SetInteriorReefVisualActive(bool active)
        {
            if (active)
            {
                ResolveInteriorReefProxyReferences();
                if (!HasInteriorReefProxyPoolReserve())
                    return false;
            }

            _pendingInteriorReefVisualActive = active;
            _pendingInteriorReefVisualDirty = true;
            TryRegisterLateFrameTick();
            return true;
        }

        private void FlushInteriorReefVisualState()
        {
            if (!_pendingInteriorReefVisualDirty)
                return;

            _pendingInteriorReefVisualDirty = false;
            ApplyInteriorReefVisualActiveImmediate(_pendingInteriorReefVisualActive);
        }

        private void ApplyInteriorReefVisualActiveImmediate(bool active)
        {
            if (interiorCaveWeed != null && interiorCaveWeed.activeSelf != active)
                interiorCaveWeed.SetActive(active);

            if (interiorBarnacles != null && interiorBarnacles.activeSelf != active)
                interiorBarnacles.SetActive(active);
        }

        private void ResolveInteriorReefProxyReferences()
        {
            if (interiorCaveWeed == null)
                interiorCaveWeed = ResolveInteriorProxyChild(InteriorCaveWeedChildName);

            if (interiorBarnacles == null)
                interiorBarnacles = ResolveInteriorProxyChild(InteriorBarnaclesChildName);
        }

        private GameObject ResolveInteriorProxyChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                Transform lod0Transform = transform.Find("LOD0");
                if (lod0Transform != null)
                    child = lod0Transform.Find(childName);
            }

            return child != null ? child.gameObject : null;
        }

        private bool HasInteriorReefProxyPoolReserve()
        {
            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return true;

            return HasProxyPoolReserve(interiorCaveWeed, pool) &&
                   HasProxyPoolReserve(interiorBarnacles, pool);
        }

        private static bool HasProxyPoolReserve(GameObject proxy, IObjectPoolService pool)
        {
            if (proxy == null || pool == null)
                return true;

            if (!pool.TryGetAvailableCountForPooledInstance(proxy, out int availableCount))
                return true;

            return availableCount >= MinimumFloodedReefProxyPoolReserve;
        }

        private float ResolveFloodedReefActivationSeconds()
        {
            IAtmosphereReadModel atmosphereManager = _atmosphereRuntime;
            float daySeconds = atmosphereManager != null
                ? Mathf.Max(1f, atmosphereManager.CycleDuration)
                : DefaultInGameDaySeconds;
            return Mathf.Max(0f, floodedReefActivationDays) * daySeconds;
        }

        internal bool ClampRepairIntegrityCap(float repairIntegrityCap)
        {
            bool changed = _integrityComponent.ClampRepairIntegrityCap(repairIntegrityCap);
            if (!changed)
                return false;

            UpdateDrainDiagnostics();
            RefreshVisualStateImmediate();
            return true;
        }

        internal bool RestoreRepairIntegrityCap(float amount)
        {
            return _integrityComponent.RestoreRepairIntegrityCap(amount);
        }

        internal bool TryGetDegradationSockets(out BaseModuleTemplate.VfxSocket[] sockets)
        {
            if (moduleTemplate == null)
            {
                sockets = null;
                return false;
            }

            sockets = moduleTemplate.VfxSockets;
            return sockets != null && sockets.Length > 0;
        }

        internal void EmitIntegritySocketVfx(Unity.Mathematics.float3 localPoint, BaseModuleVfxSocketType socketType, float integrityState)
        {
            float integrityDeficit = 1f - Mathf.Clamp01(integrityState);
            float pressureDelta = socketType switch
            {
                BaseModuleVfxSocketType.Spark => math.lerp(1.5f, 3.5f, integrityDeficit),
                BaseModuleVfxSocketType.Vent => math.lerp(2.5f, 5.5f, integrityDeficit),
                _ => math.lerp(3f, 6f, integrityDeficit)
            };

            EmitHullBreachJet(new Vector3(localPoint.x, localPoint.y, localPoint.z), pressureDelta);
        }

        internal void EmitHullBreachJet(Vector3 localPoint, float pressureDelta)
        {
            float burst01 = Mathf.Clamp01(pressureDelta * 0.25f);
            RegisterInstancedPressureSpray(localPoint, burst01);
        }

        private void RegisterInstancedPressureSpray(Vector3 localPoint, float intensity01)
        {
            IFluidDecalPresentationSink fluidDecals = GlobalRegistry.FluidDecalPresentation;
            if (fluidDecals == null)
                return;

            Vector3 worldPoint = transform.TransformPoint(localPoint);
            Vector3 worldDirection = transform.TransformDirection(ResolveDominantLocalAxis(-localPoint));
            fluidDecals.RegisterPressureSpray(worldPoint, worldDirection, math.saturate(intensity01));
        }

        internal bool TryGetInteriorHazardBounds(out Vector3 worldCenter, out float radius)
        {
            if (!TryGetInteriorOverlapQuery(out worldCenter, out Vector3 halfExtents, out _))
            {
                radius = 0f;
                return false;
            }

            float maxExtent = math.max(halfExtents.x, math.max(halfExtents.y, halfExtents.z));
            radius = maxExtent * 1.75f;
            return radius > 0.01f;
        }

        internal bool TryGetInteriorAabbBounds(out Vector3 worldCenter, out Vector3 halfExtents)
        {
            if (!TryGetInteriorOverlapQuery(out worldCenter, out Vector3 orientedHalfExtents, out Quaternion worldRotation))
            {
                halfExtents = Vector3.zero;
                return false;
            }

            Vector3 right = worldRotation * Vector3.right;
            Vector3 up = worldRotation * Vector3.up;
            Vector3 forward = worldRotation * Vector3.forward;
            halfExtents = new Vector3(
                (math.abs(right.x) * orientedHalfExtents.x) + (math.abs(up.x) * orientedHalfExtents.y) + (math.abs(forward.x) * orientedHalfExtents.z),
                (math.abs(right.y) * orientedHalfExtents.x) + (math.abs(up.y) * orientedHalfExtents.y) + (math.abs(forward.y) * orientedHalfExtents.z),
                (math.abs(right.z) * orientedHalfExtents.x) + (math.abs(up.z) * orientedHalfExtents.y) + (math.abs(forward.z) * orientedHalfExtents.z));
            return halfExtents.x > 0f && halfExtents.y > 0f && halfExtents.z > 0f;
        }

        internal bool TryContainsInteriorRuntimePoint(Vector3 runtimePosition)
        {
            if (!math.all(math.isfinite((float3)runtimePosition)))
                return false;

            if (!TryGetInteriorOverlapQuery(out Vector3 worldCenter, out Vector3 halfExtents, out Quaternion worldRotation))
                return false;

            Vector3 localDelta = Quaternion.Inverse(worldRotation) * (runtimePosition - worldCenter);
            const float containmentPaddingMeters = 0.05f;
            return math.abs(localDelta.x) <= halfExtents.x + containmentPaddingMeters &&
                   math.abs(localDelta.y) <= halfExtents.y + containmentPaddingMeters &&
                   math.abs(localDelta.z) <= halfExtents.z + containmentPaddingMeters;
        }

        internal bool TryBuildRoomWaterlineSnapshot(
            int roomId,
            float fill01,
            uint sequence,
            out HabitatRoomWaterlineSnapshot snapshot)
        {
            snapshot = default;
            float safeFill01 = math.saturate(math.isfinite(fill01) ? fill01 : 0f);
            float minimumLocalY = ResolveFloodSurfaceMinimumLocalY();
            float maximumLocalY = ResolveFloodSurfaceMaximumLocalY();
            if (!math.isfinite(minimumLocalY) || !math.isfinite(maximumLocalY) || maximumLocalY <= minimumLocalY)
                return false;

            float localSurfaceY = math.lerp(minimumLocalY, maximumLocalY, safeFill01);
            Vector3 floorWorld = transform.TransformPoint(new Vector3(0f, minimumLocalY, 0f));
            Vector3 ceilingWorld = transform.TransformPoint(new Vector3(0f, maximumLocalY, 0f));
            Vector3 surfaceWorld = transform.TransformPoint(new Vector3(0f, localSurfaceY, 0f));
            if (!math.isfinite(floorWorld.y) || !math.isfinite(ceilingWorld.y) || !math.isfinite(surfaceWorld.y))
                return false;

            float waterVolume = WaterVolumeM3;
            if (waterVolume <= 0f && safeFill01 > 0f)
                waterVolume = ResolveFloodCapacityM3() * safeFill01;

            byte flags = 0;
            if (IsBreached)
                flags |= HabitatRoomWaterlineSnapshot.FlagBreached;
            if (safeFill01 > 0.001f || IsFlooded)
                flags |= HabitatRoomWaterlineSnapshot.FlagFlooded;
            if (HasPower)
                flags |= HabitatRoomWaterlineSnapshot.FlagPowered;
            if (safeFill01 >= 0.8f)
                flags |= HabitatRoomWaterlineSnapshot.FlagOxygenDisabled;

            snapshot = new HabitatRoomWaterlineSnapshot(
                roomId,
                safeFill01,
                surfaceWorld.y,
                math.min(floorWorld.y, ceilingWorld.y),
                math.max(floorWorld.y, ceilingWorld.y),
                math.max(0f, waterVolume),
                sequence,
                flags);
            return snapshot.IsValid;
        }

        private void EvaluateCatastrophicImplosion()
        {
            bool abandonedFailure = IntegrityState == BaseModuleIntegrityState.Abandoned ||
                                    (!IsBreached && IntegrityStateNormalized <= 0.4f);
            if (_implosionTriggered ||
                !abandonedFailure ||
                ResolveExternalDepthMeters() < ResolveImplosionDepthThresholdMeters())
            {
                return;
            }

            TriggerCatastrophicImplosion();
        }

        private void TriggerCatastrophicImplosion()
        {
            if (_implosionTriggered)
                return;

            _implosionTriggered = true;
            ForceFlood();

            Vector3 roomCenter = ResolveInteriorHazardWorldPosition();
            float influenceRadius = ResolveImplosionImpulseRadiusMeters();
            if (TryGetInteriorHazardBounds(out Vector3 resolvedCenter, out float resolvedRadius))
            {
                roomCenter = resolvedCenter;
                influenceRadius = Mathf.Max(influenceRadius, resolvedRadius);
            }

            float rawImpulse = ResolveCinematicImplosionImpulseNewtonSeconds();
            if (rawImpulse > 0f && float.IsFinite(rawImpulse))
            {
                _cachedPhysicsService?.QueueImplosionImpulse(
                    roomCenter,
                    influenceRadius,
                    rawImpulse,
                    ResolveImplosionMaximumImpulseNewtonSeconds());
            }

            SetAnchoredState(false);
            NotifyModuleImploded();
            NotifyEmergencyLockdownStateChanged();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
        }

        private void HandleIntegrityCollapse(Vector3 localBreachPoint)
        {
            if (_breachLatched)
                return;

            _breachLatched = true;
            _breachCenterOfMassTargetLocal = localBreachPoint;
            _hasBreachCenterOfMassTarget = true;
            RegisterFloodDryFatigueCycle();
            if (ResolveExternalPressureDeltaKPa() >= Mathf.Max(0f, explosiveFloodPressureDeltaKPa))
                ForceFlood();
            TriggerBreachDepressurizationVortex(localBreachPoint);
            if (!TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem) || atmosphereSystem == null)
            {
                NotifyEmergencyLockdownStateChanged();
                return;
            }

            atmosphereSystem.HandleExternalModuleBreach(
                transform.TransformPoint(localBreachPoint),
                ResolveBreachAreaSquareMeters());
            NotifyEmergencyLockdownStateChanged();
        }

        private void NotifyModuleImploded()
        {
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            if (floraInteractionManager != null)
                floraInteractionManager.KillAttachedParasites(this);

            ConstructionManager manager = _constructionManager;
            if (manager != null)
                manager.NotifyModuleImploded(this);
        }

        private void TriggerBreachDepressurizationVortex(Vector3 localBreachPoint)
        {
            if (breachVortexDurationSeconds <= 0f || breachVortexMaximumAccelerationMetersPerSecondSquared <= 0f)
                return;

            Vector3 breachWorldPosition = transform.TransformPoint(localBreachPoint);
            Vector3 roomCenter = ResolveInteriorHazardWorldPosition();
            float influenceRadius = 3f;
            if (TryGetInteriorHazardBounds(out Vector3 resolvedCenter, out float resolvedRadius))
            {
                roomCenter = resolvedCenter;
                influenceRadius = resolvedRadius;
            }

            influenceRadius = Mathf.Max(0.5f, influenceRadius + Mathf.Max(0f, breachVortexRadiusPaddingMeters));
            float baseAcceleration = ResolveCinematicBreachVortexAcceleration();
            if (baseAcceleration <= 0.0001f || !float.IsFinite(baseAcceleration))
                return;

            _cachedPhysicsService?.QueueDepressurizationVortex(
                roomCenter,
                breachWorldPosition,
                influenceRadius,
                baseAcceleration,
                breachVortexMaximumAccelerationMetersPerSecondSquared,
                breachVortexDurationSeconds);
        }

        private float ResolveCinematicImplosionImpulseNewtonSeconds()
        {
            float cap = ResolveImplosionMaximumImpulseNewtonSeconds();
            if (cap <= 0f)
                return 0f;

            float depthThreat01 = ResolveCinematicDepthThreat01(ResolveExternalDepthMeters());
            return math.lerp(cap * 0.35f, cap, depthThreat01);
        }

        private float ResolveCinematicBreachVortexAcceleration()
        {
            float cap = Mathf.Max(0f, breachVortexMaximumAccelerationMetersPerSecondSquared);
            if (cap <= 0f)
                return 0f;

            float depthThreat01 = ResolveCinematicDepthThreat01(ResolveExternalDepthMeters());
            return math.lerp(cap * 0.25f, cap * 0.85f, depthThreat01);
        }

        private float ResolveCinematicDepthThreat01(float depthMeters)
        {
            float startDepth = ResolveImplosionDepthThresholdMeters();
            float fullDepth = Mathf.Max(startDepth + 1f, CinematicLeakFullDepthMeters);
            float depth01 = Mathf.Clamp01((depthMeters - startDepth) / (fullDepth - startDepth));
            return depth01 * depth01 * (3f - (2f * depth01));
        }

        private bool TryResolveSubmarineAtmosphereSystem(out ISubmarineAtmosphereRoomMutationSink atmosphereSystem)
        {
            atmosphereSystem = _submarineAtmosphereSystem;
            return atmosphereSystem != null && atmosphereSystem.IsAtmosphereRuntimeActive;
        }

        private float ResolveBreachAreaSquareMeters()
        {
            if (moduleTemplate != null)
                return Mathf.Max(0.05f, moduleTemplate.BreachAreaSquareMeters);

            return 1.2f;
        }

        private float ResolveImplosionDepthThresholdMeters()
        {
            return Mathf.Max(0f, implosionDepthThresholdMeters);
        }

        private float ResolveImplosionImpulseRadiusMeters()
        {
            return Mathf.Max(0.5f, implosionImpulseRadiusMeters);
        }

        private float ResolveImplosionMaximumImpulseNewtonSeconds()
        {
            return Mathf.Max(0f, implosionMaximumImpulseNewtonSeconds);
        }

        private Vector3 ResolveDefaultBreachLocalPoint()
        {
            if (TryGetDegradationSockets(out BaseModuleTemplate.VfxSocket[] sockets))
            {
                BaseModuleTemplate.VfxSocket socket = sockets[0];
                return new Vector3(socket.LocalPosition.x, socket.LocalPosition.y, socket.LocalPosition.z);
            }

            return Vector3.zero;
        }

        private void ConfigureRuntimeComponentsFromSerializedState()
        {
            _integrityComponent.Configure(
                maxIntegrity,
                currentIntegrity,
                isFlooded,
                drainDuration,
                repairWearPerCascade,
                minimumRecoverableIntegrityRatio,
                maxRecoverableIntegrity,
                failureMode);

            _lifeSupportComponent.Configure(
                oxygenRefillRate,
                breathableReserveCapacity,
                breathableReserve,
                airRecycleRate,
                occupiedAirDrainRate,
                staleAirThreshold,
                staleAirMinRefillScale,
                staleAirSuitDrainRate,
                co2Capacity,
                co2Level,
                co2GenerationRate,
                co2CriticalThreshold);
            _lifeSupportComponent.ApplyPressureCompressionScale(_pressureCompressionVolumeScale);
            RefreshFloodCapacityCache();
        }

        private void UpdateDrainDiagnostics()
        {
            _debugIsDraining = _integrityComponent.IsDraining;
            _debugDrainProgress = _integrityComponent.DrainProgress;
        }

        private void HandleLifeSupportSignals(ModuleLifeSupportSignals signals)
        {
            if (signals.AirQualityWarningRaised != 0)
            {
                _fieldOperationSummaryBuffer.Clear();
                if (_lifeSupportComponent.TryBuildAirReserveSummary(ref _fieldOperationSummaryBuffer))
                    RecordCascadeFailure("AIR SCRUBBERS SATURATED", in _fieldOperationSummaryBuffer);
            }

            if (signals.AirReserveDepletedRaised != 0)
            {
                RecordCascadeFailure(
                    "BREATHABLE RESERVE EXHAUSTED",
                    "Dry shelter air has collapsed into stale reserve. Occupants must evacuate or restore scrubber support.");
            }

            if (signals.Co2CriticalRaised != 0)
            {
                _fieldOperationSummaryBuffer.Clear();
                if (_lifeSupportComponent.TryBuildCo2CriticalSummary(ref _fieldOperationSummaryBuffer))
                    RecordCascadeFailure("CO2 SCRUBBER LOCKOUT", in _fieldOperationSummaryBuffer);
            }

            if (signals.Co2HypoxiaRaised != 0)
                TriggerCo2HypoxiaDistortion();
        }

        private void TriggerCo2HypoxiaDistortion()
        {
            if (_trackedPlayerHypoxiaPresentation == null)
                return;

            float intensity = Mathf.InverseLerp(0.8f, 1f, Co2Normalized);
            _trackedPlayerHypoxiaPresentation.RequestHypoxiaVisorDistortion(Mathf.Clamp01(intensity), 0.45f, 2.5f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            if (maxIntegrity < 1f) maxIntegrity = 1f;
            if (currentIntegrity < 0f) currentIntegrity = 0f;
            if (minimumRecoverableIntegrityRatio < 0.1f) minimumRecoverableIntegrityRatio = 0.1f;
            if (minimumRecoverableIntegrityRatio > 1f) minimumRecoverableIntegrityRatio = 1f;
            if (maxRecoverableIntegrity <= 0f) maxRecoverableIntegrity = maxIntegrity;
            if (maxRecoverableIntegrity > maxIntegrity) maxRecoverableIntegrity = maxIntegrity;
            if (currentIntegrity > maxRecoverableIntegrity) currentIntegrity = maxRecoverableIntegrity;
            if (breathableReserveCapacity < 1f) breathableReserveCapacity = 1f;
            if (breathableReserve < 0f) breathableReserve = 0f;
            if (breathableReserve > breathableReserveCapacity) breathableReserve = breathableReserveCapacity;
            if (co2Capacity < 1f) co2Capacity = 1f;
            if (co2Level < 0f) co2Level = 0f;
            if (co2Level > co2Capacity) co2Level = co2Capacity;
            if (co2GenerationRate < 0f) co2GenerationRate = 0f;
            if (co2CriticalThreshold < 1f) co2CriticalThreshold = 1f;
            if (co2CriticalThreshold > co2Capacity) co2CriticalThreshold = co2Capacity;
            if (airRecycleRate < 0f) airRecycleRate = 0f;
            if (occupiedAirDrainRate < 0f) occupiedAirDrainRate = 0f;
            if (staleAirSuitDrainRate < 0f) staleAirSuitDrainRate = 0f;
            if (drainDuration < 0.1f) drainDuration = 0.1f;
            if (structuralDryMassKilograms < 1f) structuralDryMassKilograms = 1f;
            if (buoyancyDisplacementVolumeCubicMeters < 0.1f) buoyancyDisplacementVolumeCubicMeters = 0.1f;
            if (maximumUnmooredAccelerationMetersPerSecondSquared < 0.1f) maximumUnmooredAccelerationMetersPerSecondSquared = 0.1f;
            if (maximumFloodVisualLeanBiasMeters < 0.01f) maximumFloodVisualLeanBiasMeters = 0.01f;
            if (floodVisualLeanTauSeconds < 0.01f) floodVisualLeanTauSeconds = 0.01f;
            if (hullCrushDepthMeters < 1f) hullCrushDepthMeters = 1f;
            if (maximumHydroStructuralLoadNewtons < 1f) maximumHydroStructuralLoadNewtons = 1f;
            if (deepCompressionStartDepthMeters < 0f) deepCompressionStartDepthMeters = 0f;
            if (deepCompressionFullPressureKPa < 1f) deepCompressionFullPressureKPa = 1f;
            if (maximumDeepCompressionAxisLoss < 0f) maximumDeepCompressionAxisLoss = 0f;
            if (maximumDeepCompressionAxisLoss > 0.01f) maximumDeepCompressionAxisLoss = 0.01f;
            if (hullCondensationStartDepthMeters < DefaultHullCondensationStartDepthMeters)
                hullCondensationStartDepthMeters = DefaultHullCondensationStartDepthMeters;
            if (hullCondensationFullDepthMeters < hullCondensationStartDepthMeters + 1f)
                hullCondensationFullDepthMeters = hullCondensationStartDepthMeters + 1f;
            if (lowIntegrityGroanThreshold01 < 0.01f) lowIntegrityGroanThreshold01 = 0.01f;
            if (lowIntegrityGroanThreshold01 > 1f) lowIntegrityGroanThreshold01 = 1f;
            if (lowIntegrityGroanNoiseFrequency < 0.01f) lowIntegrityGroanNoiseFrequency = 0.01f;
            if (lowIntegrityGroanNoiseThreshold < -1f) lowIntegrityGroanNoiseThreshold = -1f;
            if (lowIntegrityGroanNoiseThreshold > 1f) lowIntegrityGroanNoiseThreshold = 1f;
            if (lowIntegrityGroanStressFloor < 0f) lowIntegrityGroanStressFloor = 0f;
            if (lowIntegrityGroanStressFloor > 1f) lowIntegrityGroanStressFloor = 1f;
            if (lowIntegrityGroanPitchMin < 0.1f) lowIntegrityGroanPitchMin = 0.1f;
            if (lowIntegrityGroanPitchMax < lowIntegrityGroanPitchMin) lowIntegrityGroanPitchMax = lowIntegrityGroanPitchMin;
            if (brownoutEmergencyTransitionSeconds < 0.05f) brownoutEmergencyTransitionSeconds = 0.05f;
            if (oxygenScrubberHumFailPitch < 0.2f) oxygenScrubberHumFailPitch = 0.2f;
            if (oxygenScrubberHumPoweredPitch < oxygenScrubberHumFailPitch) oxygenScrubberHumPoweredPitch = oxygenScrubberHumFailPitch;
            if (breachVortexDurationSeconds < 0f) breachVortexDurationSeconds = 0f;
            if (breachVortexReferenceMassKilograms < 1f) breachVortexReferenceMassKilograms = 1f;
            if (breachVortexMaximumAccelerationMetersPerSecondSquared < 0f) breachVortexMaximumAccelerationMetersPerSecondSquared = 0f;
            if (breachVortexRadiusPaddingMeters < 0f) breachVortexRadiusPaddingMeters = 0f;
            if (implosionDepthThresholdMeters < 0f) implosionDepthThresholdMeters = 0f;
            if (implosionImpulseRadiusMeters < 0.5f) implosionImpulseRadiusMeters = 0.5f;
            if (implosionMaximumImpulseNewtonSeconds < 0f) implosionMaximumImpulseNewtonSeconds = 0f;
            if (localGravityAccelerationMetersPerSecondSquared < 0f) localGravityAccelerationMetersPerSecondSquared = 0f;
            if (localGravityHoldSeconds < 0.1f) localGravityHoldSeconds = 0.1f;
            if (bulkheadFailureWaterMassKilograms < 1f) bulkheadFailureWaterMassKilograms = 1f;
            if (bulkheadStressRatePerSecond < 0.001f) bulkheadStressRatePerSecond = 0.001f;
            if (bulkheadStressRecoveryPerSecond < 0f) bulkheadStressRecoveryPerSecond = 0f;
            if (floodedReefActivationDays < 0f) floodedReefActivationDays = 0f;
            if (floodedReefAudioMaterialId > (byte)ItemAudioMaterialId.Glass)
                floodedReefAudioMaterialId = (byte)ItemAudioMaterialId.Organic;
            if (parasiteSporeHazardRadius < 0.1f) parasiteSporeHazardRadius = 0.1f;
        }

        private void OnDrawGizmosSelected()
        {
            if (interiorTrigger != null)
            {
                Gizmos.color = _integrityComponent.IsFlooded
                    ? new Color(0f, 0.3f, 1f, 0.15f)
                    : new Color(0f, 1f, 0.3f, 0.15f);

                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = interiorTrigger.transform.localToWorldMatrix;
                Gizmos.DrawCube(interiorTrigger.center, interiorTrigger.size);
                Gizmos.DrawWireCube(interiorTrigger.center, interiorTrigger.size);
                Gizmos.matrix = oldMatrix;
            }
        }
#endif
    }
}
