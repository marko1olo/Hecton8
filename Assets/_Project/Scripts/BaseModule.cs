// ============================================================================
// HECTON-8 — BaseModule.cs
// Базовый контроллер модуля подводной базы.
//
// ОТВЕТСТВЕННОСТИ:
//   1. Хранит целостность модуля (integrity) в рантайме.
//   2. Управляет затоплением (flood) и осушением (drain).
//   3. Реализует IPowerComponent для базового энергопотребления.
//   4. Реализует IPoolable для совместимости с ObjectPoolManager.
//   5. Реализует ISlowTickable для централизованного тика через GameTickManager.
//   6. Реализует ICuttable для совместимости с LaserCutter (→ ApplyDamage).
//   7. Управляет Interior Zone (Сухая Зона) — подавляет водную физику
//      для объектов внутри незатопленного модуля.
//   8. Деконструкция (Deconstruct) — возврат ресурсов и уничтожение модуля.
//
// ДЕКОНСТРУКЦИЯ:
//   • Deconstruct(PlayerInventory) вызывается из LaserCutter при завершении
//     прогресса разбора (режим R+ЛКМ).
//   • Ресурсы возвращаются с коэффициентом REFUND_RATIO (80% по умолчанию).
//   • Если инвентарь полон — ресурс спавнится как HectonItem в мир
//     через ObjectPoolManager.
//   • После раздачи ресурсов вызывается ConstructionManager.DestroyModule().
//
// СУХИЕ ЗОНЫ (Interior Zone):
//   • BoxCollider (Trigger) на дочернем объекте или этом же GO охватывает
//     внутреннее пространство модуля.
//   • OnTriggerEnter: если модуль не затоплен → BuoyancyObject.EnterDryZone()
//   • OnTriggerExit: BuoyancyObject.ExitDryZone()
//   • При смене isFlooded: синхронизация всех отслеживаемых объектов.
//   • Кэширование через Dictionary<ulong, BuoyancyObject> по EntityId —
//     zero GetComponent в OnTriggerStay (Stay не используется вовсе).
//
// СОХРАНЕНИЕ:
//   Модуль НЕ сохраняет себя самостоятельно.
//   ConstructionManager читает публичные свойства CurrentIntegrity / IsFlooded
//   при сериализации базы и записывает их обратно при загрузке.
//
// СОСТОЯНИЯ:
//   • Healthy      : currentIntegrity == maxIntegrity, not flooded
//   • Damaged      : currentIntegrity < maxIntegrity, leak VFX active
//   • Breached     : currentIntegrity <= 0 → flooded = true
//   • Draining     : flooded && hasPower && integrity == maxIntegrity
//
// ЭНЕРГОСИСТЕМА:
//   • Базовое потребление берётся из BuildableData.powerRating.
//   • Если питания нет — помпы не работают, освещение гаснет, ремонт стоит.
//   • Если питание есть и модуль цел — вода откачивается.
//
// ZERO GC:
//   • Нет Update / FixedUpdate — вся логика через ISlowTickable.
//   • OnPowerStatusChanged включает/выключает свет без per-frame polling.
//   • GetComponents в горячем пути не вызываются.
//   • Dictionary — pre-allocated capacity, no boxing (int keys).
//   • OnTriggerStay не используется — только Enter/Exit.
//   • Deconstruct: for-циклы, TryAddItem, zero LINQ.
//   • Статические коллекции отсутствуют — нет утечек памяти при смене сцен.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Caves;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Hecton8.Gameplay
{
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
    public sealed class BaseModule : MonoBehaviour, IPowerComponent, IPoolable, ISlowTickable, IFixedTickable, IUpdatable, ICuttable, IPhysicsImpactMaterialProvider, IElectromagneticPulseEventListener
    {
        // COLD ALLOC: List<BaseModule>[64] - active runtime habitat module registry for cold-path environment scans - owner: BaseModule
        private static readonly List<BaseModule> s_activeModules = new List<BaseModule>(64);
        private const int ModuleWaterLevelShaderCapacity = 64;
        private const float BrownoutShaderStateEpsilon = 0.001f;
        private const float AupRadiusLogicThresholdMeters = 50f;
        private static readonly int s_ModuleAmbienceDataId = Shader.PropertyToID("_ModuleAmbienceData");
        private static readonly int s_ModuleWaterLevelsId = Shader.PropertyToID("_ModuleWaterLevels");
        private static readonly int s_ModuleWaterLevelCountId = Shader.PropertyToID("_ModuleWaterLevelCount");
        private static readonly int s_BaseVoltageId = Shader.PropertyToID("_BaseVoltage");
        private static readonly int s_BaseVoltageFlickerSpeedId = Shader.PropertyToID("_BaseVoltageFlickerSpeed");
        private static readonly int s_BaseVoltageMinimumId = Shader.PropertyToID("_BaseVoltageMinimum");
        private static readonly int s_BaseBrownoutEmergencyColorId = Shader.PropertyToID("_BaseBrownoutEmergencyColor");
        // COLD ALLOC: Vector4[256] — global module center/radius upload scratch — owner: BaseModule
        private static readonly Vector4[] s_moduleAmbienceData = new Vector4[ModuleWaterLevelShaderCapacity];
        // COLD ALLOC: Vector4[256] — global module water/flicker upload scratch — owner: BaseModule
        private static readonly Vector4[] s_moduleFloodAndFlickerData = new Vector4[ModuleWaterLevelShaderCapacity];
        private static int s_lastModuleWaterLevelUploadFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveModuleRegistry()
        {
            s_activeModules.Clear();
            s_lastModuleWaterLevelUploadFrame = -1;
            for (int i = 0; i < ModuleWaterLevelShaderCapacity; i++)
            {
                s_moduleAmbienceData[i] = Vector4.zero;
                s_moduleFloodAndFlickerData[i] = new Vector4(0f, 0f, 1f, 0f);
            }

            Shader.SetGlobalFloat(s_BaseVoltageId, 1f);
            Shader.SetGlobalFloat(s_BaseVoltageFlickerSpeedId, 19f);
            Shader.SetGlobalFloat(s_BaseVoltageMinimumId, 0.04f);
            Shader.SetGlobalColor(s_BaseBrownoutEmergencyColorId, new Color(1f, 0.13f, 0.06f, 1f));
        }
        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Фиксированная дельта медленного тика (секунды).
        /// GameTickManager вызывает SlowTick() с этим интервалом.
        /// </summary>
        private const float SLOW_TICK_DT = 0.5f;

        /// <summary>
        /// Начальная ёмкость словаря отслеживаемых объектов.
        /// Типичный модуль содержит 0–16 плавучих объектов одновременно.
        /// </summary>
        private const int TRACKED_INITIAL_CAPACITY = 16;

        /// <summary>
        /// Максимум коллайдеров, пересчитываемых при холодной синхронизации interior zone.
        /// </summary>
        private const int INTERIOR_OVERLAP_CAPACITY = 32;
        private const float SeawaterDensityKilogramsPerCubicMeter = 1025f;
        private const float GravityAccelerationMetersPerSecondSquared = 9.81f;
        private const float MinimumMassKilograms = 1f;
        private const float BuoyancyMassUpdateThresholdKilograms = 0.5f;
        private const float DefaultMaximumHydroStructuralLoadNewtons = 500000f;
        private const float DefaultBulkheadFailureWaterMassKilograms = 18000f;
        private const float DefaultBulkheadStressRatePerSecond = 0.035f;
        private const float DefaultBulkheadStressRecoveryPerSecond = 0.01f;
        private const float SurfacePressureKPa = 101.325f;
        private const float DefaultDeepCompressionStartDepthMeters = 3000f;
        private const float DefaultDeepCompressionFullPressureKPa = 60000f;
        private const float DefaultMaximumDeepCompressionAxisLoss = 0.001f;
        private const float DefaultJointShearCompressionDeltaThreshold = 0.15f;
        private const float DefaultJointShearDamagePerSecondAtFullDelta = 0.02f;
        private const float DefaultJointShearStressRecoveryPerSecond = 0.08f;
        private const float DefaultJointShearGroanCooldownSeconds = 4f;
        private const float DefaultHullCondensationStartDepthMeters = 2000f;
        private const float CinematicLeakFullDepthMeters = 4000f;
        private const float CinematicLeakBaseIntensity01 = 0.12f;
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

        /// <summary>
        /// Коэффициент возврата ресурсов при деконструкции.
        /// 0.8 = 80% ресурсов возвращается.
        /// </summary>
        private const float REFUND_RATIO = 0.8f;

        /// <summary>
        /// Canonical child name for module-local leak particle owner.
        /// Used as a cold-path fallback when serialized reference is missing.
        /// </summary>
        private const string LeakVfxChildName = "LeakVfx";
        private const string InteriorCaveWeedChildName = "Cave-Weed";
        private const string InteriorBarnaclesChildName = "Barnacles";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Integrity ─────────────────────────────────")]
        [Tooltip("Максимальная целостность модуля.")]
        [SerializeField] private float maxIntegrity = 100f;

        [Tooltip("Текущая целостность модуля на старте.")]
        [SerializeField] private float currentIntegrity = 100f;
        [Tooltip("Optional immutable template that owns abandoned-module integrity authoring and VFX socket coordinates.")]
        [SerializeField] private BaseModuleTemplate moduleTemplate;

        [Tooltip("Модуль затоплен на старте? Обычно false.")]
        [SerializeField] private bool isFlooded;

        [Header("── Anchor / Unmoored Physics ──────────────────")]
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

        [Header("── Flood / Drain ─────────────────────────────")]
        [Tooltip("Сколько секунд требуется на полную откачку воды.")]
        [SerializeField] private float drainDuration = 8f;
        [SerializeField] private float floodPumpEnergyCost = 65f;

        [Tooltip("Скорость пассивного восстановления целостности (единиц/сек). 0 = отключено.")]
        [SerializeField] private float passiveRecoveryRate = 0f;

        [Tooltip("Скорость пассивной деградации целостности (единиц/сек). " +
                 "Лор: ~0.1% в игровой день. При глубине > 500м — умножается на depthDegradationMultiplier.")]
        [SerializeField] private float passiveDegradationRate = 0.001f;

        [Tooltip("Множитель деградации на глубине > 500м (давление на корпус).")]
        [SerializeField, UnityEngine.Range(1f, 5f)] private float depthDegradationMultiplier = 2f;

        [Header("── Cascade Failures ──────────────────────────────")]
        [Tooltip("Текущий каскадный отказ модуля. None = штатно, остальные требуют сервисного восстановления.")]
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

        [Tooltip("Скорость утечки кислорода из скафандра игрока внутри аварийного модуля.")]
        [SerializeField] private float oxygenLeakDrainRate = 10f;

        [Tooltip("Урон скафандру игрока внутри горящего модуля.")]
        [SerializeField] private float fireSuitDamageRate = 12f;

        [Tooltip("Сжигаемая пожаром энергия костюма игрока внутри модуля.")]
        [SerializeField] private float fireSuitEnergyDrainRate = 6f;

        [Header("── Interior Zone (Dry Zone) ──────────────────")]
        [Tooltip("BoxCollider (Trigger), охватывающий внутреннее пространство модуля. " +
                 "Объекты с BuoyancyObject внутри этого триггера не испытывают водных сил, " +
                 "пока модуль не затоплен. Назначь вручную или создай автоматически.")]
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

        [Header("── Deconstruction ────────────────────────────")]
        [Tooltip("Префаб мирового предмета (HectonItem) для спавна ресурсов, " +
                 "которые не поместились в инвентарь. " +
                 "Должен иметь HectonItem + BuoyancyObject + Rigidbody.")]
        [SerializeField] private GameObject worldItemPrefab;

        [Header("── Visual References ─────────────────────────")]
        [Tooltip("Объект воды внутри модуля. Активен, когда модуль затоплен.")]
        [SerializeField] private GameObject waterVolume;

        [Tooltip("Optional water-surface proxy transform driven by room flood fill. Only the local Y value is animated.")]
        [SerializeField] private Transform floodSurfacePlane;

        [Tooltip("Fallback local-space Y range for the water-surface proxy when the interior trigger cannot provide bounds.")]
        [SerializeField] private Vector2 floodSurfaceLocalYRange = new Vector2(-1.25f, 1.25f);

        [Tooltip("Эффект пузырьков / утечки при повреждении.")]
        [SerializeField] private ParticleSystem leakVfx;

        [Tooltip("Внутренние источники света. Выключаются при отсутствии питания.")]
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

        [Tooltip("Локальный Volume для тумана / постпроцесса затопления.")]
        [SerializeField] private Volume floodedLocalVolume;

        [Tooltip("Optional camera/probe transform used to enable flooded screen-space distortion only while below the water plane.")]
        [SerializeField] private Transform floodDistortionProbe;

        [Header("── Audio (optional) ──────────────────────────")]
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
        [Header("── Life Support ──────────────────────────────")]
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
        [Header("── Power Fallback ────────────────────────────")]
        [Tooltip("Fallback power draw, если BuildableData / ModuleMarker отсутствуют.")]
        [SerializeField] private float fallbackPowerRating = -10f;

        [Tooltip("Приоритет отключения помп/освещения модуля.")]
        [Range(0, 100)]
        [SerializeField] private int powerPriority = 50;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugIsDraining;
        [SerializeField] private float _debugDrainProgress;
        [SerializeField] private int _debugTrackedObjectCount;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private bool _hasPower = true;
        private bool _ambientLightsBrownedOut;
        private bool _updatableRegistered;
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
        private bool _isUnmoored;

        private ModuleMarker _moduleMarker;
        private HabitatIntegrityManager _habitatIntegrityManager;
        private SubmarineAtmosphereSystem _submarineAtmosphereSystem;
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
        /// Предыдущее состояние isFlooded, используемое для определения
        /// момента смены состояния затопления (edge detection).
        /// Инициализируется в OnSpawn/Awake значением isFlooded.
        /// </summary>
        private bool _wasFlooded;

        /// <summary>
        /// Защита от повторного вызова Deconstruct (например, два игрока
        /// одновременно разбирают модуль в будущем мультиплеере).
        /// </summary>
        private bool _isDeconstructing;
        // ── Life Support State ──

        /// <summary>
        /// Cached reference to player's survival system.
        /// Set when player enters interior trigger, cleared on exit.
        /// Null = player is not inside this module.
        /// </summary>
        private HectonSurvivalSystem _trackedPlayerSurvival;
        private HectonPlayerMovement _trackedPlayerMovement;
        private readonly ModuleIntegrityComponent _integrityComponent = new ModuleIntegrityComponent();
        private readonly ModuleLifeSupportComponent _lifeSupportComponent = new ModuleLifeSupportComponent();
        // ══════════════════════════════════════════════════════════
        //  INTERIOR ZONE — TRACKED OBJECTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Словарь отслеживаемых BuoyancyObject внутри Interior Zone.
        /// Key: Collider.GetEntityId() (не GameObject — т.к. триггер видит Collider).
        /// Value: кэшированный BuoyancyObject.
        /// </summary>
        private readonly Dictionary<ulong, BuoyancyObject> _trackedObjects
            = new Dictionary<ulong, BuoyancyObject>(TRACKED_INITIAL_CAPACITY);

        /// <summary>
        /// Временный список InstanceID для безопасного удаления из словаря
        /// во время итерации (при синхронизации состояния затопления).
        /// Pre-allocated, zero GC.
        /// </summary>
        private readonly List<ulong> _keysToRemove = new List<ulong>(TRACKED_INITIAL_CAPACITY);
        // COLD ALLOC: List<BaseAirlock>[2] — cached owned airlock controllers for emergency lockdown fan-out — owner: BaseModule
        private readonly List<BaseAirlock> _airlockBuffer = new List<BaseAirlock>(2);
        // COLD ALLOC: List<SealedDoor>[2] — cached owned sealed bulkhead doors for quarantine locking — owner: BaseModule
        private readonly List<SealedDoor> _sealedDoorBuffer = new List<SealedDoor>(2);

        // COLD ALLOC: Collider[32] — resync interior occupants on enable/load/spawn — owner: BaseModule
        private readonly Collider[] _interiorOverlapBuffer = new Collider[INTERIOR_OVERLAP_CAPACITY];
        [SerializeField] private float _debugSolarEmpBlackoutSeconds;
        private float _solarEmpBlackoutRemainingSeconds;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES — для ConstructionManager save/load
        // ══════════════════════════════════════════════════════════

        /// <summary>Максимальная целостность (read-only).</summary>
        public float MaxIntegrity => maxIntegrity;
        internal static int ActiveModuleCount => s_activeModules.Count;
        internal static BaseModule GetActiveModuleAt(int index)
        {
            return index >= 0 && index < s_activeModules.Count ? s_activeModules[index] : null;
        }

        /// <summary>
        /// Текущая целостность. ConstructionManager записывает сюда
        /// значение при загрузке сохранения.
        /// </summary>
        public float CurrentIntegrity
        {
            get => _integrityComponent.CurrentIntegrity;
            set => _integrityComponent.SetCurrentIntegrity(value);
        }

        /// <summary>
        /// Флаг затопления. ConstructionManager записывает сюда
        /// значение при загрузке сохранения.
        /// </summary>
        public bool IsFlooded
        {
            get => _integrityComponent.IsFlooded;
            set => _integrityComponent.SetFlooded(value);
        }

        /// <summary>Целостность упала до нуля — модуль пробит.</summary>
        public bool IsBreached => _integrityComponent.CurrentIntegrity <= 0f;

        /// <summary>Идёт ли сейчас откачка воды.</summary>
        public bool IsDraining => _integrityComponent.IsDraining;

        /// <summary>Идёт ли деконструкция (защита от повторных вызовов).</summary>
        public bool IsDeconstructing => _isDeconstructing;

        /// <summary>Текущий каскадный аварийный статус модуля.</summary>
        public BaseModuleFailureMode CurrentFailureMode => _integrityComponent.FailureMode;

        /// <summary>Модуль находится в аварийном каскадном состоянии.</summary>
        public bool HasCascadeFailure => _integrityComponent.FailureMode != BaseModuleFailureMode.None;

        /// <summary>Current repair ceiling after accumulated material fatigue.</summary>
        public float MaxRecoverableIntegrity => _integrityComponent.MaxRecoverableIntegrity;
        /// <summary>Estimated catastrophic repair cycles remaining before the module reaches its minimum recoverable ceiling. -1 means the cap is not authored.</summary>
        public int RemainingRepairCycles => _integrityComponent.ResolveRemainingRepairCycles();
        /// <summary>Optional immutable template that owns abandoned-module integrity authoring and VFX sockets.</summary>
        public BaseModuleTemplate ModuleTemplate => moduleTemplate;
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
        public float WaterVolumeM3 => Mathf.Max(0f, waterVolumeM3);
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

                if (_powerNode == null)
                    TryGetComponent(out _powerNode);

                if (_powerNode != null && _powerNode.Grid != null)
                    return Mathf.Clamp01(_powerNode.Grid.SupplyRatio);

                return _hasPower ? 1f : 0f;
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

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Базовое энергопотребление модуля.
        /// Источник: BuildableData.powerRating → fallback.
        /// </summary>
        public float PowerRating => StaticDebuffedPowerRating;

        public int PowerPriority => powerPriority;

        public bool HasPower => HasOperationalPower;

        /// <summary>
        /// Реакция на изменение статуса питания от PowerGrid:
        ///   • Свет включается / выключается.
        ///   • Drain запускается / останавливается.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            if (_hasPower == hasPower)
                return;

            _hasPower = hasPower;
            _debugHasPower = hasPower;

            SetLightsEnabled(ShouldLightsBeEnabled());

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
            PublishActiveModuleWaterLevelsToShader(true);
            UpdateAmbienceTickRegistration();
        }

        // ══════════════════════════════════════════════════════════
        //  ICuttable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Реализация ICuttable — делегирует в ApplyDamage.
        /// Позволяет LaserCutter резать модули базы.
        /// hitPoint может использоваться для локализации повреждений в будущем.
        /// </summary>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            ApplyDamageInternal(damage, true, transform.InverseTransformPoint(hitPoint));
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public void OnSpawn()
        {
            CacheReferences();
            ReadBuildablePower();
            ConfigureRuntimeComponentsFromSerializedState();
            InitializeAmbienceNoiseSeed();
            ConfigureOxygenScrubberHumSource();
            _isDeconstructing = false;
            _ambientLightsBrownedOut = false;
            _ambientVoltageSupplyRatio = 1f;
            _currentBrownoutFlicker01 = 1f;
            _brownoutTransition01 = 0f;
            _brownoutTransitionTarget01 = 0f;
            _ruptureGroanNoisePhase = 0f;
            _ruptureGroanPreviousNoise = -1f;
            _oxygenHum01 = 0f;
            _oxygenHumTarget01 = 0f;
            _oxygenHumActive = false;
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
            NotifyModuleExitIfNeeded();
            StopDrain();
            SetLeakActive(false);
            SetFloodedVisual(false);
            _ambientLightsBrownedOut = false;
            _brownoutTransition01 = 0f;
            _brownoutTransitionTarget01 = 0f;
            _ruptureGroanNoisePhase = 0f;
            _ruptureGroanPreviousNoise = -1f;
            SetLightsEnabled(true);

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

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Централизованный медленный тик от GameTickManager.
        /// Выполняет:
        ///   1. Пассивный ремонт (если есть питание и integrity > 0).
        ///   2. Прогресс откачки воды (drain timer).
        /// Без питания — никаких операций не происходит.
        /// </summary>
        public void SlowTick()
        {
            ApplyFluidIncursion(SLOW_TICK_DT);
            ApplyCascadeFailureEffects();
            ApplyDeepSeaCompressionState(false);
            UpdateLifeSupport(SLOW_TICK_DT);
            UpdateFloodVisualStateImmediate();
            UpdateFloodedReefGrowth(SLOW_TICK_DT);
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

            // Пассивная деградация — лор: давление, время, глубина
            if (passiveDegradationRate > 0f && _integrityComponent.CurrentIntegrity > 0f)
            {
                float degradation = passiveDegradationRate * SLOW_TICK_DT;

                // Глубина > 500м — усиленная деградация от давления
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

            if (!EnsureUnmooredRigidbody())
                return;

            ApplyQueuedHydroStructuralLoad(fixedDeltaTime);

            float floodFill01 = ResolveUnmooredFloodFillNormalized();
            float displacementVolume = ResolveBuoyancyDisplacementVolumeCubicMeters();
            float dryMass = ResolveDryMassKilograms();
            float parasiteMass = ResolveParasiteAddedMassKilograms();
            float effectiveMass = Mathf.Max(
                MinimumMassKilograms,
                dryMass + parasiteMass + (floodFill01 * displacementVolume * SeawaterDensityKilogramsPerCubicMeter));
            if (Mathf.Abs(_moduleRigidbody.mass - effectiveMass) >= BuoyancyMassUpdateThresholdKilograms)
                _moduleRigidbody.mass = effectiveMass;

            float retainedAirMassEquivalent = displacementVolume * (1f - floodFill01) * SeawaterDensityKilogramsPerCubicMeter;
            float netAccelerationY = ((retainedAirMassEquivalent - effectiveMass) / effectiveMass) * GravityAccelerationMetersPerSecondSquared;
            float maximumAcceleration = ResolveMaximumUnmooredAccelerationMetersPerSecondSquared();
            float externalDepthMeters = ResolveExternalDepthMeters();
            if (floodFill01 > 0.5f && externalDepthMeters > hullCrushDepthMeters)
            {
                float crushRatio = Mathf.Clamp01((externalDepthMeters - hullCrushDepthMeters) / 1000f);
                netAccelerationY -= maximumAcceleration * crushRatio;
            }

            netAccelerationY = Mathf.Clamp(netAccelerationY, -maximumAcceleration, maximumAcceleration);
            if (Mathf.Abs(netAccelerationY) > 0.0001f)
            {
                PhysicsForceRouter.QueueForceAtPosition(
                    _moduleRigidbody,
                    Vector3.up * netAccelerationY,
                    transform.position,
                    ForceMode.Acceleration);
            }

            ApplyFloodVisualLean(fixedDeltaTime, floodFill01);
        }

        /// <summary>
        /// Frame-rate ambience only. Registered while brownout flicker or scrubber hum fade is active.
        /// </summary>
        public void Tick(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            bool shaderStateChanged = false;
            if (ShouldAdvanceBrownoutShaderState())
                shaderStateChanged = AdvanceBrownoutShaderState(dt);

            UpdateOxygenScrubberHum(dt);
            if (shaderStateChanged)
                PublishActiveModuleWaterLevelsToShader(true);

            UpdateAmbienceTickRegistration();
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            CacheReferences();
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

            PhysicsEventBus.Register(this);
            TryRegister();
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
            TryUnregisterUpdatable();
            ResetBrownoutShaderState();
            s_activeModules.Remove(this);
            PhysicsEventBus.Unregister(this);
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
            PublishActiveModuleWaterLevelsToShader(true);
        }

        private void OnDestroy()
        {
            TryUnregisterUpdatable();
            ResetBrownoutShaderState();
            s_activeModules.Remove(this);
            PhysicsEventBus.Unregister(this);
            TryUnregister();
            TryUnregisterFixedTick();
            BaseDegradationSystem.ClearIntegrityState(this);
            BaseDegradationSystem.ClearParasiteSporeHazard(this);
            Hecton8.Construction.BaseDegradationSystem.ClearParasiteStructuralState(this);
            BaseDegradationSystem.ClearPressureCompressionState(this);
            PublishActiveModuleWaterLevelsToShader(true);
        }

        // ══════════════════════════════════════════════════════════
        //  INTERIOR ZONE — TRIGGER CALLBACKS
        // ══════════════════════════════════════════════════════════

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            // ── Life Support: detect player entry ──
            // CompareTag is zero GC (no string allocation).
            // Player check runs BEFORE BuoyancyObject check —
            // player may or may not have BuoyancyObject,
            // but life support must work regardless.
            TryTrackPlayer(other, true);

            // ── Interior Zone: BuoyancyObject tracking ──
            if (!other.TryGetComponent(out BuoyancyObject buoyancy))
                return;

            TrackBuoyancyObject(other, buoyancy);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;

            // ── Life Support: detect player exit ──
            bool trackedPlayerExited = IsTrackedPlayerCollider(other);
            if (trackedPlayerExited)
                _trackedPlayerSurvival = null;
            // ── Interior Zone: BuoyancyObject tracking ──
            ulong key = ResolveColliderRuntimeId(other);

            if (_trackedObjects.TryGetValue(key, out BuoyancyObject buoyancy))
            {
                _trackedObjects.Remove(key);
                UpdateTrackedDiagnostics();

                if (buoyancy != null && !_integrityComponent.IsFlooded)
                {
                    buoyancy.ExitDryZone();
                }
            }

            if (trackedPlayerExited)
            {
                ResyncInteriorOccupants(false);
                if (_trackedPlayerSurvival == null)
                    ModuleStatusEvents.NotifyExit(this);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC GAMEPLAY API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Наносит урон модулю.
        /// При достижении 0 — модуль пробит и затапливается.
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
                DamageSignal signal = default;
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
        /// Ремонтирует модуль.
        /// Если целостность полностью восстановлена и есть питание —
        /// начинается откачка воды.
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

        /// <summary>
        /// Принудительное затопление. Останавливает drain, активирует визуал.
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
            PlaySpatialSfx(floodClip);
            if (!wasFlooded)
                NotifyEmergencyLockdownStateChanged();
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
        /// Принудительное завершение осушения. Сбрасывает drain state и визуал.
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
            return Mathf.Max(0.001f, ResolveBuoyancyDisplacementVolumeCubicMeters());
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
                applicationWorldPoint = transform.position;

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

            float threshold01 = Mathf.Clamp01(lowIntegrityGroanThreshold01);
            float integrity01 = IntegrityStateNormalized;
            if (integrity01 >= threshold01)
            {
                _ruptureGroanPreviousNoise = -1f;
                return;
            }

            _ruptureGroanNoisePhase += Mathf.Max(0f, deltaTime) * Mathf.Max(0.01f, lowIntegrityGroanNoiseFrequency);
            float noiseValue = noise.snoise(new float2(_ruptureGroanNoisePhase, _brownoutNoiseSeed + 31.73f));
            float threshold = Mathf.Clamp(lowIntegrityGroanNoiseThreshold, -1f, 1f);
            bool crossedThreshold = noiseValue >= threshold && _ruptureGroanPreviousNoise < threshold;
            _ruptureGroanPreviousNoise = noiseValue;
            if (!crossedThreshold)
                return;

            float damage01 = Mathf.Clamp01((threshold01 - integrity01) / Mathf.Max(0.0001f, threshold01));
            float stress01 = Mathf.Clamp01(Mathf.Max(lowIntegrityGroanStressFloor, damage01));
            float pitchNoise = noise.snoise(new float2(_ruptureGroanNoisePhase * 3.17f, _brownoutNoiseSeed + 91.41f));
            float pitch01 = Mathf.Clamp01(pitchNoise * 0.5f + 0.5f);
            float pitchMin = Mathf.Max(0.1f, lowIntegrityGroanPitchMin);
            float pitchMax = Mathf.Max(pitchMin, lowIntegrityGroanPitchMax);
            float pitch = Mathf.Lerp(pitchMin, pitchMax, pitch01) * Mathf.Lerp(1f, 0.82f, damage01);

            ResolveModuleAmbienceBounds(out Vector3 centerWS, out _);
            ProceduralAudioEvents.RaiseStructuralStressTriggered(centerWS, stress01, pitch);
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

            SetState(
                normalizedIntegrity * Mathf.Max(0f, _integrityComponent.MaxIntegrity),
                flooded,
                BaseModuleFailureMode.None,
                maxIntegrity,
                integrityState == BaseModuleIntegrityState.Pristine ? 1f : Mathf.Clamp01(normalizedIntegrity),
                0f);
        }

        /// <summary>
        /// Полный сброс визуального состояния модуля по текущим данным.
        /// Вызывается ConstructionManager после загрузки сохранения.
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
        }

        /// <summary>
        /// Устанавливает состояние модуля при загрузке сохранения.
        /// Вызывается ConstructionManager.LoadFromSaveData().
        /// </summary>
        public void SetState(float integrity, bool flooded)
        {
            SetState(integrity, flooded, BaseModuleFailureMode.None, maxIntegrity);
        }

        /// <summary>
        /// Устанавливает состояние модуля при загрузке сохранения, включая аварийный статус.
        /// </summary>
        public void SetState(float integrity, bool flooded, BaseModuleFailureMode cascadeFailure)
        {
            SetState(integrity, flooded, cascadeFailure, maxIntegrity);
        }

        /// <summary>
        /// Restores module state from save, including the reduced repair ceiling caused by previous failures.
        /// </summary>
        public void SetState(float integrity, bool flooded, BaseModuleFailureMode cascadeFailure, float repairIntegrityCap)
        {
            SetState(integrity, flooded, cascadeFailure, repairIntegrityCap, 1f);
        }

        /// <summary>
        /// Restores module state from save, including reduced repair ceiling and breathable reserve state.
        /// </summary>
        public void SetState(float integrity, bool flooded, BaseModuleFailureMode cascadeFailure, float repairIntegrityCap, float airReserveNormalized)
        {
            SetState(integrity, flooded, cascadeFailure, repairIntegrityCap, airReserveNormalized, 0f);
        }

        /// <summary>
        /// Restores module state from save, including reduced repair ceiling, breathable reserve, and CO2 saturation.
        /// </summary>
        public void SetState(float integrity, bool flooded, BaseModuleFailureMode cascadeFailure, float repairIntegrityCap, float airReserveNormalized, float co2Normalized)
        {
            SetState(
                integrity,
                flooded,
                cascadeFailure,
                repairIntegrityCap,
                airReserveNormalized,
                co2Normalized,
                0f,
                false);
        }

        /// <summary>
        /// Restores module state from save, including flooded reef maturation state.
        /// </summary>
        public void SetState(
            float integrity,
            bool flooded,
            BaseModuleFailureMode cascadeFailure,
            float repairIntegrityCap,
            float airReserveNormalized,
            float co2Normalized,
            float floodedReefFloodSeconds,
            bool interiorReefInfestationActive)
        {
            ConfigureRuntimeComponentsFromSerializedState();
            _integrityComponent.RestoreState(integrity, flooded, cascadeFailure, repairIntegrityCap);
            _lifeSupportComponent.RestoreState(airReserveNormalized, co2Normalized);
            _floodedReefFloodSeconds = Mathf.Max(0f, floodedReefFloodSeconds);
            _interiorReefInfestationActive = interiorReefInfestationActive;
            SetInteriorReefVisualActive(_interiorReefInfestationActive);
            if (_interiorReefInfestationActive)
                RegisterFloodedReefFaunaAnchor();
            else
                UnregisterFloodedReefFaunaAnchor();
            if (!flooded)
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

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — DECONSTRUCTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Разбирает модуль, возвращая ресурсы игроку.
        ///
        /// Порядок:
        ///   1. Получить buildCost из ModuleMarker.Data.
        ///   2. Для каждого ресурса: refund = floor(amount * REFUND_RATIO).
        ///   3. Попытка добавить в PlayerInventory.Grid.
        ///   4. Если инвентарь полон — спавн HectonItem в мир через ObjectPoolManager.
        ///   5. Освобождение dry zone (ReleaseAllTrackedObjects).
        ///   6. ConstructionManager.DestroyModule(gameObject).
        ///
        /// ZERO GC:
        ///   • for-циклы по List, без LINQ.
        ///   • TryAddItem возвращает bool, без аллокаций.
        ///   • ObjectPoolManager.Spawn — zero GC (pre-warmed pool).
        ///
        /// ЗАЩИТА:
        ///   • _isDeconstructing предотвращает повторный вызов.
        ///   • Null-safe: если ModuleMarker/Data/buildCost отсутствуют —
        ///     модуль уничтожается без возврата ресурсов (с Warning).
        /// </summary>
        /// <param name="playerInventory">
        /// Инвентарь игрока для возврата ресурсов.
        /// Null допустим — все ресурсы будут спавнены в мир.
        /// </param>
        public void Deconstruct(PlayerInventory playerInventory)
        {
            // ── Guard: повторный вызов ──
            if (_isDeconstructing)
                return;

            _isDeconstructing = true;

            // ── Audio ──
            PlaySpatialSfx(deconstructClip);

            Vector3 dropPosition = transform.position + Vector3.up * 0.5f;
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            InventoryGrid grid = playerInventory != null ? playerInventory.Grid : null;
            EjectHostedModuleContents(playerInventory, pool, ref dropPosition);

            // ── Получение данных о стоимости ──
            BuildableData buildData = _moduleMarker != null ? _moduleMarker.Data : null;
            List<InventoryCost> buildCost = buildData != null ? buildData.buildCost : null;

            if (buildCost == null || buildCost.Count == 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[BaseModule] Deconstruct: '{gameObject.name}' has no buildCost data. " +
                    "Destroying without resource refund.", this);
#endif
            }
            else
            {
                // ── Позиция для спавна выпавших предметов ──
                // Немного выше центра модуля, чтобы предметы не застревали в полу
                int costCount = buildCost.Count;
                for (int c = 0; c < costCount; c++)
                {
                    InventoryCost cost = buildCost[c];
                    if (cost == null || cost.item == null)
                        continue;

                    // ── Расчёт возврата ──
                    int refundAmount = Mathf.FloorToInt(cost.amount * REFUND_RATIO);
                    if (refundAmount <= 0)
                        continue;

                    for (int i = 0; i < refundAmount; i++)
                    {
                        bool addedToInventory = false;

                        // ── Попытка добавить в инвентарь ──
                        int itemHashId = cost.item != null
                            ? Hecton.Localization.LocHash.Compute(cost.item.PersistentId)
                            : 0;
                        if (playerInventory != null &&
                            grid != null &&
                            itemHashId != 0 &&
                            playerInventory.TryAddItem(itemHashId, 1))
                            addedToInventory = true;

                        // ── Fallback: спавн в мир ──
                        if (!addedToInventory)
                        {
                            SpawnWorldItem(itemHashId, dropPosition, pool, playerInventory);

                            // Смещаем позицию для следующего предмета,
                            // чтобы они не стакались в одной точке
                            dropPosition.x += 0.3f;
                        }
                    }
                }
            }

            // ── Освобождение dry zone ──
            ReleaseAllTrackedObjects();

            // ── Уничтожение модуля через ConstructionManager ──
            ConstructionManager cm = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
            if (cm != null)
            {
                cm.DestroyModule(gameObject);
            }
            else
            {
                // Fallback: если ConstructionManager недоступен
                ObjectPoolManager fallbackPool = GlobalRegistry.ObjectPool;
                if (fallbackPool != null)
                    fallbackPool.Despawn(gameObject);
                else
                    Destroy(gameObject);
            }
        }

        /// <summary>
        /// Проверяет, можно ли деконструировать этот модуль.
        /// Используется LaserCutter для валидации перед началом разбора.
        /// </summary>
        public bool CanDeconstruct()
        {
            if (_isDeconstructing) return false;

            // Будущее: запрет деконструкции при затоплении,
            // наличии подключённых модулей, питании и т.д.
            return true;
        }

        internal void DropItemQuantityToInventoryOrWorld(
            int itemHashId,
            int quantity,
            PlayerInventory playerInventory,
            ObjectPoolManager pool,
            ref Vector3 dropPosition)
        {
            if (itemHashId == 0 || quantity <= 0)
                return;

            InventoryGrid targetGrid = playerInventory != null ? playerInventory.Grid : null;
            for (int i = 0; i < quantity; i++)
            {
                bool addedToInventory = false;
                if (playerInventory != null &&
                    targetGrid != null &&
                    playerInventory.TryAddItem(itemHashId, 1))
                    addedToInventory = true;

                if (addedToInventory)
                    continue;

                SpawnWorldItem(itemHashId, dropPosition, pool, playerInventory);
                dropPosition.x += 0.3f;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — WORLD ITEM SPAWN
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Спавнит ресурс как физический предмет в мире.
        ///
        /// Паттерн:
        ///   1. Если worldItemPrefab назначен → Spawn через ObjectPoolManager.
        ///   2. Спавненный HectonItem инициализируется по hashId через ItemCatalog.
        ///   3. Если worldItemPrefab == null → ресурс потерян (с Warning).
        ///
        /// Разделение ответственностей:
        ///   BaseModule НЕ знает про конкретный визуал предмета.
        ///   worldItemPrefab — generic контейнер с HectonItem + Rigidbody.
        ///   Каталожные данные на HectonItem устанавливаются программно.
        ///
        /// Будущее: если нужна визуальная дифференциация (разные модели
        /// для титана vs стекла), worldItemPrefab может быть заменён
        /// на per-resource world prefab, если появится отдельный визуальный владелец.
        /// </summary>
        private void SpawnWorldItem(int itemHashId, Vector3 position, ObjectPoolManager pool, PlayerInventory playerInventory)
        {
            if (itemHashId == 0)
                return;

            ItemCatalog itemCatalog = ResolveItemCatalog(playerInventory);
            if (itemCatalog == null)
                return;

            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            if (persistentWorldRegistry != null &&
                persistentWorldRegistry.TryRegisterDroppedItem(itemHashId, itemCatalog, 1, position))
            {
                return;
            }

            if (worldItemPrefab == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[BaseModule] worldItemPrefab not assigned on '{gameObject.name}'. " +
                    $"Resource hash '{itemHashId}' dropped on the ground but has no world prefab. Lost.",
                    this);
#endif
                return;
            }

            if (pool == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    "[BaseModule] ObjectPoolManager not available. " +
                    $"Resource hash '{itemHashId}' lost.");
#endif
                return;
            }

            GameObject itemGO = pool.Spawn(worldItemPrefab, position, Quaternion.identity);

            if (itemGO == null)
                return;

            // ── Инициализация HectonItem данными ──
            // HectonItem на worldItemPrefab инициализируется hashId через ItemCatalog.
            // Базовый модуль не тянет asset-ссылки в логику возврата ресурсов.
            //
            // АРХИТЕКТУРНОЕ РЕШЕНИЕ:
            // Визуальный/world seam остаётся внутри HectonItem.
            // Это чище, чем рефлексия, и сохраняет Zero-GC.
            if (itemGO.TryGetComponent(out HectonItem hectonItem))
            {
                hectonItem.SetItemByHash(itemCatalog, itemHashId, 1);
            }
        }

        private static ItemCatalog ResolveItemCatalog(PlayerInventory playerInventory)
        {
            if (playerInventory != null && playerInventory.ItemCatalog != null)
                return playerInventory.ItemCatalog;

            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            PlayerInventory inventoryInstance = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            return inventoryInstance != null ? inventoryInstance.ItemCatalog : null;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — CORE STATE LOGIC
        // ══════════════════════════════════════════════════════════

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

        private void EjectHostedModuleContents(PlayerInventory playerInventory, ObjectPoolManager pool, ref Vector3 dropPosition)
        {
            if (TryGetComponent(out MaintenanceStationModule maintenanceStation) &&
                maintenanceStation.TryExtractSlottedToolHashForDeconstruct(out int slottedToolHashId))
            {
                DropItemQuantityToInventoryOrWorld(slottedToolHashId, 1, playerInventory, pool, ref dropPosition);
            }

            if (TryGetComponent(out DeepDrillModule drillModule))
                drillModule.EjectBufferedOutput(this, playerInventory, pool, ref dropPosition);

            if (TryGetComponent(out LogisticsSorterModule sorterModule))
                sorterModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);

            if (TryGetComponent(out LogisticsPipeNode pipeNode) &&
                pipeNode.TryExtractInFlightCargoHashForDeconstruct(out int pipeItemHashId, out int pipeAmount))
            {
                DropItemQuantityToInventoryOrWorld(pipeItemHashId, pipeAmount, playerInventory, pool, ref dropPosition);
            }
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
                PublishActiveModuleWaterLevelsToShader(true);
                UpdateAmbienceTickRegistration();
                return;
            }

            _ambientLightsBrownedOut = brownedOut;
            _ambientVoltageSupplyRatio = sanitizedVoltageRatio;
            SetLightsEnabled(ShouldLightsBeEnabled());
            AdvanceBrownoutShaderState(0f);
            PublishActiveModuleWaterLevelsToShader(true);
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
            if (radius > AupRadiusLogicThresholdMeters && radius < 250000f)
            {
                AbsoluteUniversePosition moduleAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
                AbsoluteUniversePosition pulseAup = AbsoluteUniversePosition.FromRuntimePosition(pulseEvent.RuntimePosition);
                if (AbsoluteUniversePosition.DistanceSq(in moduleAup, in pulseAup) > (double)radiusSq)
                    return;
            }
            else if (radius <= AupRadiusLogicThresholdMeters)
            {
                Vector3 delta = transform.position - pulseEvent.RuntimePosition;
                if (delta.sqrMagnitude > radiusSq)
                    return;
            }

            _solarEmpBlackoutRemainingSeconds = Mathf.Max(
                _solarEmpBlackoutRemainingSeconds,
                pulseEvent.DurationSeconds);
            _debugSolarEmpBlackoutSeconds = _solarEmpBlackoutRemainingSeconds;
            _integrityComponent.StopDrain();
            SetLightsEnabled(false);
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

            _solarEmpBlackoutRemainingSeconds = Mathf.Max(0f, _solarEmpBlackoutRemainingSeconds - Mathf.Max(0f, deltaTime));
            _debugSolarEmpBlackoutSeconds = _solarEmpBlackoutRemainingSeconds;
            if (_solarEmpBlackoutRemainingSeconds > 0f)
                return;

            SetLightsEnabled(ShouldLightsBeEnabled());
            if (HasOperationalPower)
                _integrityComponent.TryStartDrain(_hasPower);
            UpdateDrainDiagnostics();
            SyncSpatialRole();
            UpdateOxygenScrubberHumTarget();
            UpdateAmbienceTickRegistration();
        }

        private void TriggerCascadeFailure()
        {
            _integrityComponent.TriggerCascadeFailure(ResolveCascadeFailureMode());
            UpdateDrainDiagnostics();

            switch (_integrityComponent.FailureMode)
            {
                case BaseModuleFailureMode.Fire:
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("MODULE FIRE", "Compartment ignition risk. Repair before occupancy.");
                    NotificationEvents.PushWarning("BASE MODULE FIRE // SERVICE NOW");
                    break;
                case BaseModuleFailureMode.ShortCircuit:
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("SHORT CIRCUIT", "Compartment flooded and pumps offline until hull service completes.");
                    NotificationEvents.PushWarning("BASE SHORT CIRCUIT // POWER LOCKOUT");
                    break;
                default:
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("OXYGEN LEAK", "Compartment seal lost. Oxygen-safe shelter compromised.");
                    NotificationEvents.PushWarning("BASE OXYGEN LEAK // COMPARTMENT BREACHED");
                    break;
            }

            SetLeakActive(ShouldLeakBeActive());
            SetFloodedVisual(_integrityComponent.IsFlooded);
            SyncTrackedObjectsFloodState();
            SetLightsEnabled(ShouldLightsBeEnabled());
            SyncSpatialRole();
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
            SetLightsEnabled(ShouldLightsBeEnabled());
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INTERIOR ZONE SYNC
        // ══════════════════════════════════════════════════════════

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
                ? localGravityDirection.normalized
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
                PowerSupplyRatio,
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
                PowerSupplyRatio,
                _trackedPlayerSurvival));
        }

        private string BuildAirReserveSummary()
        {
            return _lifeSupportComponent.BuildAirReserveSummary();
        }

        private void ClearCascadeFailure()
        {
            _integrityComponent.ClearCascadeFailure();
            SyncSpatialRole();
        }

        private BaseModuleFailureMode ResolveCascadeFailureMode()
        {
            string prefabId = _moduleMarker != null ? _moduleMarker.PrefabId : string.Empty;
            return _integrityComponent.ResolveCascadeFailureMode(prefabId, transform.position, _hasPower);
        }

        private void RecordCascadeFailure(string title, string summary)
        {
            string source = _moduleMarker != null && _moduleMarker.Data != null
                ? _moduleMarker.Data.moduleName
                : "BASE";
            FieldOperationLogSystem.RecordOperation(source, title, summary, "WARN");
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
            bool isFloodedNow = _integrityComponent.IsFlooded;
            if (isFloodedNow == _wasFlooded)
                return;

            _wasFlooded = isFloodedNow;

            if (_trackedObjects.Count == 0)
                return;

            _keysToRemove.Clear();

            Dictionary<ulong, BuoyancyObject>.Enumerator enumerator = _trackedObjects.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<ulong, BuoyancyObject> kvp = enumerator.Current;
                BuoyancyObject buoyancy = kvp.Value;

                if (buoyancy == null)
                {
                    _keysToRemove.Add(kvp.Key);
                    continue;
                }

                if (isFloodedNow)
                    buoyancy.ExitDryZone();
                else
                    buoyancy.EnterDryZone();
            }

            for (int i = 0, count = _keysToRemove.Count; i < count; i++)
            {
                _trackedObjects.Remove(_keysToRemove[i]);
            }

            _keysToRemove.Clear();
            UpdateTrackedDiagnostics();
        }

        private void ReleaseAllTrackedObjects()
        {
            if (_trackedObjects.Count == 0)
                return;

            Dictionary<ulong, BuoyancyObject>.Enumerator enumerator = _trackedObjects.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<ulong, BuoyancyObject> kvp = enumerator.Current;
                BuoyancyObject buoyancy = kvp.Value;

                if (buoyancy != null && !_integrityComponent.IsFlooded)
                {
                    buoyancy.ExitDryZone();
                }
            }

            _trackedObjects.Clear();
            UpdateTrackedDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VISUALS
        // ══════════════════════════════════════════════════════════

        private void ResyncInteriorOccupants(bool notifyPlayerEnter)
        {
            if (!TryGetInteriorOverlapQuery(out Vector3 worldCenter, out Vector3 halfExtents, out Quaternion worldRotation))
                return;

            int overlapCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                worldCenter,
                halfExtents,
                _interiorOverlapBuffer,
                worldRotation,
                HectonLayerMasks.DefaultRaycastLayerMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < overlapCount; i++)
            {
                Collider overlap = _interiorOverlapBuffer[i];
                _interiorOverlapBuffer[i] = null;

                if (overlap == null || ReferenceEquals(overlap, interiorTrigger))
                    continue;

                TryTrackPlayer(overlap, notifyPlayerEnter);

                if (overlap.TryGetComponent(out BuoyancyObject buoyancy))
                    TrackBuoyancyObject(overlap, buoyancy);
            }
        }

        private void SetLeakActive(bool active)
        {
            if (leakVfx == null) return;

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
            bool floodVisible = sanitizedFloodLevel01 > 0.001f;

            if (waterVolume != null && waterVolume.activeSelf != floodVisible)
                waterVolume.SetActive(floodVisible);

            if (floodSurfacePlane != null)
            {
                CaptureFloodSurfaceDefaults();
                Vector3 nextLocalPosition = _defaultFloodSurfaceLocalPosition;
                nextLocalPosition.y = Mathf.Lerp(ResolveFloodSurfaceMinimumLocalY(), ResolveFloodSurfaceMaximumLocalY(), sanitizedFloodLevel01);
                floodSurfacePlane.localPosition = nextLocalPosition;
            }

            UpdateFloodDistortionVolume(floodVisible);
            PublishActiveModuleWaterLevelsToShader(true);
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

            float localY = Mathf.Lerp(ResolveFloodSurfaceMinimumLocalY(), ResolveFloodSurfaceMaximumLocalY(), _cachedFloodLevel01);
            return transform.TransformPoint(new Vector3(0f, localY, 0f)).y;
        }

        private static void PublishActiveModuleWaterLevelsToShader(bool force = false)
        {
            int currentFrame = Time.frameCount;
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
                float moduleVoltage01 = Mathf.Clamp01(module._currentBrownoutFlicker01);
                baseVoltage01 = Mathf.Min(baseVoltage01, moduleVoltage01);
                if (!hasGlobalModuleSettings)
                {
                    baseFlickerSpeed = Mathf.Max(0.1f, module.brownoutFlickerSpeed);
                    baseVoltageMinimum = Mathf.Clamp01(module.brownoutMinimumLightIntensityRatio);
                    baseEmergencyColor = module.brownoutEmergencyEmissionColor;
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

            Shader.SetGlobalVectorArray(s_ModuleAmbienceDataId, s_moduleAmbienceData);
            Shader.SetGlobalVectorArray(s_ModuleWaterLevelsId, s_moduleFloodAndFlickerData);
            Shader.SetGlobalInt(s_ModuleWaterLevelCountId, moduleCount);
            Shader.SetGlobalFloat(s_BaseVoltageId, baseVoltage01);
            Shader.SetGlobalFloat(s_BaseVoltageFlickerSpeedId, baseFlickerSpeed);
            Shader.SetGlobalFloat(s_BaseVoltageMinimumId, baseVoltageMinimum);
            Shader.SetGlobalColor(s_BaseBrownoutEmergencyColorId, baseEmergencyColor);
        }

        private float ResolveHullCondensationDepth01()
        {
            float startDepth = Mathf.Max(0f, hullCondensationStartDepthMeters);
            float fullDepth = Mathf.Max(startDepth + 1f, hullCondensationFullDepthMeters);
            float depthMeters = _pressureCompressionDepthMeters > 0.25f
                ? _pressureCompressionDepthMeters
                : ResolveExternalDepthMeters();
            return Mathf.Clamp01((depthMeters - startDepth) / (fullDepth - startDepth));
        }

        private void ResolveModuleAmbienceBounds(out Vector3 centerWS, out float radiusMeters)
        {
            if (TryGetInteriorOverlapQuery(out centerWS, out Vector3 halfExtents, out _))
            {
                radiusMeters = Mathf.Max(0.5f, halfExtents.magnitude + 0.25f);
                return;
            }

            centerWS = transform.position;
            float volumeRadius = Mathf.Pow(Mathf.Max(1f, ResolveBuoyancyDisplacementVolumeCubicMeters()), 0.33333334f);
            radiusMeters = Mathf.Max(2f, volumeRadius * 1.75f);
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
            float startDepthMeters = Mathf.Max(0f, deepCompressionStartDepthMeters);
            if (depthMeters <= startDepthMeters)
                return 1f;

            float hydrostaticPressureKPa = ResolveHydrostaticPressureKPa(depthMeters);
            float startPressureKPa = ResolveHydrostaticPressureKPa(startDepthMeters);
            float pressureRangeKPa = Mathf.Max(1f, deepCompressionFullPressureKPa - startPressureKPa);
            float compression01 = Mathf.Clamp01((hydrostaticPressureKPa - startPressureKPa) / pressureRangeKPa);
            return 1f - (compression01 * Mathf.Clamp(maximumDeepCompressionAxisLoss, 0f, 0.01f));
        }

        private static float ResolveHydrostaticPressureKPa(float depthMeters)
        {
            return SurfacePressureKPa + (Mathf.Max(0f, depthMeters) * SeawaterDensityKilogramsPerCubicMeter * GravityAccelerationMetersPerSecondSquared * 0.001f);
        }

        private void ApplyPressureCompressionVisualScale(float axisScale)
        {
            if (pressureCompressionVisualRoot == null)
                return;

            CapturePressureCompressionDefaults();
            Vector3 nextScale = _defaultPressureCompressionVisualScale;
            nextScale.x *= axisScale;
            nextScale.y *= axisScale;
            pressureCompressionVisualRoot.localScale = nextScale;
        }

        private void ResetPressureCompressionVisualState()
        {
            _pressureCompressionAxisScale = 1f;
            _pressureCompressionVolumeScale = 1f;
            _pressureCompressionDepthMeters = 0f;
            _lifeSupportComponent.ApplyPressureCompressionScale(1f);

            if (pressureCompressionVisualRoot == null)
                return;

            CapturePressureCompressionDefaults();
            pressureCompressionVisualRoot.localScale = _defaultPressureCompressionVisualScale;
            pressureCompressionVisualRoot.localRotation = _defaultPressureCompressionVisualRotation;
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

            float depthMeters = ResolveExternalDepthMeters();
            float deltaVolumeM3 = CalculateIngressVolumeDeltaM3(
                depthMeters,
                ResolveLeakHoleAreaSquareMeters(),
                deltaTime,
                breachPressureFlowCoefficient);
            if (deltaVolumeM3 <= 0f)
                return;

            bool wasFlooded = _integrityComponent.IsFlooded;
            SetWaterVolumeM3(Mathf.Min(capacityM3, waterVolumeM3 + deltaVolumeM3));
            EmitPressureIncursionVisuals(deltaVolumeM3, depthMeters);
            _integrityComponent.ForceFlood();
            UpdateFloodVisualStateImmediate();
            if (!wasFlooded)
            {
                SyncTrackedObjectsFloodState();
                SyncSpatialRole();
                NotifyEmergencyLockdownStateChanged();
                BaseDegradationSystem.SynchronizeIntegrityState(this);
            }
        }

        internal static float CalculateIngressVolumeDeltaM3(
            float depthMeters,
            float holeAreaSquareMeters,
            float deltaTime,
            float pressureFlowCoefficient)
        {
            if (depthMeters <= 0f || holeAreaSquareMeters <= 0f || deltaTime <= 0f || pressureFlowCoefficient <= 0f)
                return 0f;

            float pressureDeltaPa = SeawaterDensityKilogramsPerCubicMeter *
                                    GravityAccelerationMetersPerSecondSquared *
                                    Mathf.Max(0f, depthMeters);
            float volumeDelta = Mathf.Sqrt(Mathf.Max(0f, pressureDeltaPa)) *
                                holeAreaSquareMeters *
                                deltaTime *
                                pressureFlowCoefficient;
            return float.IsFinite(volumeDelta) ? Mathf.Max(0f, volumeDelta) : 0f;
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

            float depth01 = Mathf.Clamp01(depthMeters / CinematicLeakFullDepthMeters);
            float stagedDepth01 = depth01 * depth01 * (3f - (2f * depth01));
            float burst01 = Mathf.Clamp01(floodDeltaM3 * 0.35f);
            return Mathf.Clamp01(CinematicLeakBaseIntensity01 + (stagedDepth01 * 0.78f) + (burst01 * 0.22f));
        }

        private void SetWaterVolumeM3(float nextVolumeM3)
        {
            waterVolumeM3 = Mathf.Clamp(
                float.IsFinite(nextVolumeM3) ? nextVolumeM3 : 0f,
                0f,
                ResolveFloodCapacityM3());
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
                return Mathf.Clamp01(waterVolumeM3 / ResolveFloodCapacityM3());

            if (_integrityComponent.IsFlooded)
            {
                if (TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem) &&
                    atmosphereSystem != null)
                {
                    if (_cachedAtmosphereRoomIndex >= 0)
                        return atmosphereSystem.ResolveRoomFloodFillNormalized(_cachedAtmosphereRoomIndex);

                    if (atmosphereSystem.TryResolveRoomFloodFillNormalized(transform.position, out int roomIndex, out float floodFill01))
                    {
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
            float halfHeight = interiorTrigger.size.y * 0.5f * Mathf.Abs(lossyScale.y);
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
                if (l != null && l.enabled != enabled)
                    l.enabled = enabled;
            }
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
                   _ambientVoltageSupplyRatio < Mathf.Clamp01(brownoutActivationVoltageRatio);
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
                return Mathf.Lerp(1f, 0f, Mathf.Clamp01(_brownoutTransition01));

            float voltage01 = Mathf.Clamp01(_ambientVoltageSupplyRatio / Mathf.Max(0.01f, brownoutActivationVoltageRatio));
            return Mathf.Lerp(1f, Mathf.Max(Mathf.Clamp01(brownoutMinimumLightIntensityRatio), voltage01), Mathf.Clamp01(_brownoutTransition01));
        }

        private void AdvanceBrownoutTransition(float dt)
        {
            float transitionSeconds = Mathf.Max(0.05f, brownoutEmergencyTransitionSeconds);
            float transitionStep = math.saturate(Mathf.Max(0f, dt) / transitionSeconds);
            _brownoutTransition01 = math.lerp(_brownoutTransition01, _brownoutTransitionTarget01, transitionStep);
            if (Mathf.Abs(_brownoutTransition01 - _brownoutTransitionTarget01) <= BrownoutShaderStateEpsilon)
                _brownoutTransition01 = _brownoutTransitionTarget01;
        }

        private void ResetBrownoutShaderState()
        {
            _currentBrownoutFlicker01 = 1f;
            _brownoutTransition01 = 0f;
            _brownoutTransitionTarget01 = 0f;
            PublishActiveModuleWaterLevelsToShader(true);
        }

        private void ConfigureOxygenScrubberHumSource()
        {
            if (oxygenScrubberHumSource == null)
                return;

            if (oxygenScrubberHumLoop != null && oxygenScrubberHumSource.clip != oxygenScrubberHumLoop)
                oxygenScrubberHumSource.clip = oxygenScrubberHumLoop;
            oxygenScrubberHumSource.loop = true;
            oxygenScrubberHumSource.playOnAwake = false;
            oxygenScrubberHumSource.volume = 0f;
            oxygenScrubberHumSource.pitch = ResolveOxygenScrubberHumFailPitch();
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
            if (oxygenScrubberHumSource == null)
                return;

            ConfigureOxygenScrubberHumSource();
            float fadeSeconds = _oxygenHumTarget01 > _oxygenHum01
                ? 0.25f
                : Mathf.Max(0.1f, oxygenScrubberHumFailFadeSeconds);
            float alpha = dt > 0f ? 1f - Mathf.Exp(-dt / fadeSeconds) : 1f;
            _oxygenHum01 = Mathf.Lerp(_oxygenHum01, _oxygenHumTarget01, alpha);

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
            oxygenScrubberHumSource.pitch = Mathf.Lerp(
                ResolveOxygenScrubberHumFailPitch(),
                Mathf.Max(0.01f, oxygenScrubberHumPoweredPitch),
                _oxygenHum01);
            _oxygenHumActive = true;
        }

        private float ResolveOxygenScrubberHumFailPitch()
        {
            return Mathf.Clamp(oxygenScrubberHumFailPitch, 0.2f, Mathf.Max(0.2f, oxygenScrubberHumPoweredPitch));
        }

        /// <summary>
        /// Одноразовый SFX у модуля через SpatialAudioManager (пул 3D). Луп утечки по-прежнему на <see cref="audioSource"/>.
        /// </summary>
        private void PlaySpatialSfx(AudioClip clip)
        {
            if (clip == null)
                return;

            Hecton8.Core.IAudioService sam = Hecton8.Core.GlobalRegistry.Audio;
            if (sam != null)
                sam.PlayAtPoint(clip, transform.position);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DATA HELPERS
        // ══════════════════════════════════════════════════════════

        private void CacheReferences()
        {
            if (_moduleMarker == null)
                TryGetComponent(out _moduleMarker);

            if (_moduleRigidbody == null)
                TryGetComponent(out _moduleRigidbody);

            if (leakVfx == null)
                ResolveLeakVfxReference();

            if (_habitatIntegrityManager == null)
            {
                if (!TryGetComponent(out _habitatIntegrityManager))
                    _habitatIntegrityManager = gameObject.AddComponent<HabitatIntegrityManager>();
            }

            if (_submarineAtmosphereSystem == null)
                _submarineAtmosphereSystem = GetComponentInParent<SubmarineAtmosphereSystem>();

            if (_powerNode == null)
                TryGetComponent(out _powerNode);

            if (_voxelVolume == null && !TryGetComponent(out _voxelVolume))
                _voxelVolume = GetComponentInParent<HectonVoxelVolume>();

            if (interiorTrigger == null)
                interiorTrigger = GetComponentInChildren<BoxCollider>(true);

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
            if (_moduleRigidbody == null && !TryGetComponent(out _moduleRigidbody))
                _moduleRigidbody = gameObject.AddComponent<Rigidbody>();

            CaptureModuleRigidbodyDefaults();
            return _moduleRigidbody != null;
        }

        private void CaptureModuleRigidbodyDefaults()
        {
            if (_moduleBodyDefaultsCaptured)
                return;

            if (_moduleRigidbody == null && !TryGetComponent(out _moduleRigidbody))
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
                return transform.position;

            return interiorTrigger.transform.TransformPoint(interiorTrigger.center);
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
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z));

            worldCenter = triggerTransform.TransformPoint(interiorTrigger.center);
            halfExtents = Vector3.Scale(interiorTrigger.size * 0.5f, absoluteScale);
            worldRotation = triggerTransform.rotation;
            return halfExtents.x > 0f && halfExtents.y > 0f && halfExtents.z > 0f;
        }

        private float ResolveUnmooredFloodFillNormalized()
        {
            if (TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem) && atmosphereSystem != null)
            {
                if (_cachedAtmosphereRoomIndex >= 0)
                    return atmosphereSystem.ResolveRoomFloodFillNormalized(_cachedAtmosphereRoomIndex);

                if (atmosphereSystem.TryResolveRoomFloodFillNormalized(transform.position, out int roomIndex, out float floodFill01))
                {
                    _cachedAtmosphereRoomIndex = roomIndex;
                    return floodFill01;
                }
            }

            _cachedAtmosphereRoomIndex = -1;
            return _integrityComponent.IsFlooded ? 1f : 0f;
        }

        private float ResolveExternalDepthMeters()
        {
            if (TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem) && atmosphereSystem != null)
                return atmosphereSystem.ResolveExternalDepthMeters();

            HectonAtmosphereManager atmosphereManager = Hecton8.Core.GlobalRegistry.Atmosphere;
            if (atmosphereManager != null)
                return ResolveExternalDepthMetersAup(atmosphereManager.SeaLevelY);

            return 0f;
        }

        private float ResolveExternalDepthMetersAup(float seaLevelRuntimeY)
        {
            Vector3 moduleRuntimePosition = transform.position;
            AbsoluteUniversePosition moduleAup = AbsoluteUniversePosition.FromRuntimePosition(moduleRuntimePosition);
            moduleRuntimePosition.y = seaLevelRuntimeY;
            AbsoluteUniversePosition seaLevelAup = AbsoluteUniversePosition.FromRuntimePosition(moduleRuntimePosition);
            double depthMeters = seaLevelAup.ToAbsoluteDouble3().y - moduleAup.ToAbsoluteDouble3().y;
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
            float volumeScale = Mathf.Clamp(_pressureCompressionVolumeScale, 0.1f, 1f);
            if (moduleTemplate != null)
                return Mathf.Max(0.1f, moduleTemplate.BuoyancyDisplacementVolumeCubicMeters * volumeScale);

            return Mathf.Max(0.1f, buoyancyDisplacementVolumeCubicMeters * volumeScale);
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
                PhysicsForceRouter.QueueForceAtPosition(
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
                localFloodBias = localFloodBias.normalized * maxShift;

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
                pressureCompressionVisualRoot.localRotation = _defaultPressureCompressionVisualRotation;
                return;
            }

            float tauSeconds = ResolveFloodVisualLeanTauSeconds();
            float alpha = 1f - Mathf.Exp(-fixedDeltaTime / tauSeconds);
            pressureCompressionVisualRoot.localRotation = Quaternion.Slerp(
                pressureCompressionVisualRoot.localRotation,
                targetRotation,
                alpha);
        }

        private void NotifyEmergencyLockdownStateChanged()
        {
            ConstructionManager manager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
            if (manager != null)
                manager.NotifyModuleEmergencyStateChanged(this);
        }

        private void TryTrackPlayer(Collider other, bool notifyEnter)
        {
            if (_trackedPlayerSurvival != null)
                return;

            if (!other.CompareTag("Player"))
                return;

            HectonSurvivalSystem resolvedSurvival = other.GetComponentInParent<HectonSurvivalSystem>();
            if (resolvedSurvival == null)
                return;

            _trackedPlayerSurvival = resolvedSurvival;
            _trackedPlayerMovement = other.GetComponentInParent<HectonPlayerMovement>();
            if (notifyEnter)
                ModuleStatusEvents.NotifyEnter(this);
        }

        private bool IsTrackedPlayerCollider(Collider other)
        {
            if (_trackedPlayerSurvival == null || !other.CompareTag("Player"))
                return false;

            HectonSurvivalSystem resolvedSurvival = other.GetComponentInParent<HectonSurvivalSystem>();
            return ReferenceEquals(_trackedPlayerSurvival, resolvedSurvival);
        }

        private void TrackBuoyancyObject(Collider other, BuoyancyObject buoyancy)
        {
            ulong key = ResolveColliderRuntimeId(other);

            if (_trackedObjects.ContainsKey(key))
                return;

            _trackedObjects[key] = buoyancy;
            UpdateTrackedDiagnostics();

            if (!_integrityComponent.IsFlooded)
                buoyancy.EnterDryZone();
        }

        private static ulong ResolveColliderRuntimeId(Collider collider)
        {
            return collider != null
                ? EntityId.ToULong(collider.GetEntityId())
                : 0UL;
        }

        private void NotifyModuleExitIfNeeded()
        {
            if (_trackedPlayerSurvival == null)
                return;

            _trackedPlayerSurvival = null;
            _trackedPlayerMovement = null;
            ModuleStatusEvents.NotifyExit(this);
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
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[BaseModule] interiorTrigger on '{gameObject.name}' was not set as Trigger. " +
                        "Fixed automatically.", this);
#endif
                }
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogWarning(
                    $"[BaseModule] '{gameObject.name}' has no interiorTrigger assigned. " +
                    "Interior Zone (Dry Zone) will not function.", this);
            }
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateTrackedDiagnostics()
        {
            _debugTrackedObjectCount = _trackedObjects.Count;
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
            ConstructionManager manager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
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
            if (!PhysicsForceRouter.ApplyKinematicWeldSnap(_moduleRigidbody, targetPosition, targetRotation))
                transform.SetPositionAndRotation(targetPosition, targetRotation);

            _defaultBodyIsKinematic = true;
            _defaultBodyUseGravity = false;
            _defaultCollisionDetectionMode = _moduleRigidbody.collisionDetectionMode;
            _defaultInterpolation = _moduleRigidbody.interpolation;
            _moduleBodyDefaultsCaptured = true;
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

        private void TryRegister()
        {
            if (_tickRegistered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _tickRegistered = GlobalRegistry.SlowTickables.Contains(this);
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
            return Mathf.Abs(_brownoutTransition01 - _brownoutTransitionTarget01) > 0.001f ||
                   Mathf.Abs(_oxygenHum01 - _oxygenHumTarget01) > 0.001f ||
                   _oxygenHumActive;
        }

        private void TryRegisterUpdatable()
        {
            if (_updatableRegistered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _updatableRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_updatableRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _updatableRegistered = false;
        }

        private void TryRegisterFixedTick()
        {
            if (_fixedTickRegistered || !_isUnmoored)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _fixedTickRegistered = GlobalRegistry.FixedTickables.Contains(this);
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
            if (oxygenUnits <= 0f || !TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem) || atmosphereSystem == null)
                return;

            int roomIndex = atmosphereSystem.ResolveNearestRoomIndexForWorldPosition(transform.position);
            if (roomIndex < 0)
                return;

            atmosphereSystem.InjectOxygenUnits(roomIndex, oxygenUnits);
        }

        /// <summary>
        /// Allows the atmosphere owner to sum mature oxygen-producing cultivation slots directly from the native slot lane.
        /// </summary>
        public void ApplyCultivationOxygen(CultivationManager cultivationManager, float oxygenUnitsPerMaturePlant)
        {
            if (cultivationManager == null ||
                oxygenUnitsPerMaturePlant <= 0f ||
                !TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem) ||
                atmosphereSystem == null)
            {
                return;
            }

            int roomIndex = atmosphereSystem.ResolveNearestRoomIndexForWorldPosition(transform.position);
            if (roomIndex < 0)
                return;

            atmosphereSystem.InjectCultivationOxygenFromSlots(
                roomIndex,
                cultivationManager.SlotStateReadOnly,
                oxygenUnitsPerMaturePlant);
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
            if (parasiteSporeVfx == null)
                ResolveParasiteSporeVfxReference();

            if (parasiteSporeVfx == null)
                return;

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

            return transform.position;
        }

        internal float ResolveHostRoomTemperatureCelsius()
        {
            if (!TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem) ||
                atmosphereSystem == null)
            {
                return 0f;
            }

            int roomIndex = atmosphereSystem.ResolveNearestRoomIndexForWorldPosition(transform.position);
            return roomIndex >= 0
                ? atmosphereSystem.GetRoomTemperatureCelsius(roomIndex)
                : 0f;
        }

        internal bool TryInjectHostRoomTemperatureDeltaCelsius(float deltaCelsius)
        {
            if (!(deltaCelsius > 0f) || !float.IsFinite(deltaCelsius))
                return false;

            if (!TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem) || atmosphereSystem == null)
                return false;

            int roomIndex = atmosphereSystem.ResolveNearestRoomIndexForWorldPosition(transform.position);
            if (roomIndex < 0)
                return false;

            atmosphereSystem.InjectRoomTemperatureDeltaCelsius(roomIndex, deltaCelsius);
            return true;
        }

        internal bool TryGetHostedBioReactor(out BioReactor reactor)
        {
            if (TryGetComponent(out reactor))
                return reactor != null;

            reactor = GetComponentInChildren<BioReactor>();
            return reactor != null;
        }

        private void TryMarkPowerGridDirty()
        {
            if (_powerNode == null)
                TryGetComponent(out _powerNode);

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

            if (interiorCaveWeed != null && interiorCaveWeed.activeSelf != active)
                interiorCaveWeed.SetActive(active);

            if (interiorBarnacles != null && interiorBarnacles.activeSelf != active)
                interiorBarnacles.SetActive(active);

            return true;
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
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
                return true;

            return HasProxyPoolReserve(interiorCaveWeed, pool) &&
                   HasProxyPoolReserve(interiorBarnacles, pool);
        }

        private static bool HasProxyPoolReserve(GameObject proxy, ObjectPoolManager pool)
        {
            if (proxy == null || pool == null)
                return true;

            if (!proxy.TryGetComponent(out ObjectPoolManager.PoolItemMarker marker))
                return true;

            return pool.GetAvailableCountByPrefabId(marker.PrefabId) >= MinimumFloodedReefProxyPoolReserve;
        }

        private float ResolveFloodedReefActivationSeconds()
        {
            HectonAtmosphereManager atmosphereManager = Hecton8.Core.GlobalRegistry.Atmosphere;
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
                BaseModuleVfxSocketType.Spark => Mathf.Lerp(1.5f, 3.5f, integrityDeficit),
                BaseModuleVfxSocketType.Vent => Mathf.Lerp(2.5f, 5.5f, integrityDeficit),
                _ => Mathf.Lerp(3f, 6f, integrityDeficit)
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
            AbyssalFluidDecalManager fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals == null)
                return;

            Vector3 worldPoint = transform.TransformPoint(localPoint);
            Vector3 worldDirection = transform.position - worldPoint;
            if (worldDirection.sqrMagnitude < 0.0001f)
                worldDirection = -transform.forward;

            worldDirection.Normalize();
            fluidDecals.RegisterPressureSpray(worldPoint, worldDirection, Mathf.Clamp01(intensity01));
        }

        internal bool TryGetInteriorHazardBounds(out Vector3 worldCenter, out float radius)
        {
            if (!TryGetInteriorOverlapQuery(out worldCenter, out Vector3 halfExtents, out _))
            {
                radius = 0f;
                return false;
            }

            radius = halfExtents.magnitude;
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
                (Mathf.Abs(right.x) * orientedHalfExtents.x) + (Mathf.Abs(up.x) * orientedHalfExtents.y) + (Mathf.Abs(forward.x) * orientedHalfExtents.z),
                (Mathf.Abs(right.y) * orientedHalfExtents.x) + (Mathf.Abs(up.y) * orientedHalfExtents.y) + (Mathf.Abs(forward.y) * orientedHalfExtents.z),
                (Mathf.Abs(right.z) * orientedHalfExtents.x) + (Mathf.Abs(up.z) * orientedHalfExtents.y) + (Mathf.Abs(forward.z) * orientedHalfExtents.z));
            return halfExtents.x > 0f && halfExtents.y > 0f && halfExtents.z > 0f;
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

            Vector3 roomCenter = transform.position;
            float influenceRadius = ResolveImplosionImpulseRadiusMeters();
            if (TryGetInteriorHazardBounds(out Vector3 resolvedCenter, out float resolvedRadius))
            {
                roomCenter = resolvedCenter;
                influenceRadius = Mathf.Max(influenceRadius, resolvedRadius);
            }

            float rawImpulse = ResolveCinematicImplosionImpulseNewtonSeconds();
            if (rawImpulse > 0f && float.IsFinite(rawImpulse))
            {
                PhysicsApplySystem.TriggerImplosionImpulse(
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
            if (!TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem) || atmosphereSystem == null)
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

            ConstructionManager manager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
            if (manager != null)
                manager.NotifyModuleImploded(this);
        }

        private void TriggerBreachDepressurizationVortex(Vector3 localBreachPoint)
        {
            if (breachVortexDurationSeconds <= 0f || breachVortexMaximumAccelerationMetersPerSecondSquared <= 0f)
                return;

            Vector3 breachWorldPosition = transform.TransformPoint(localBreachPoint);
            Vector3 roomCenter = transform.position;
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

            PhysicsApplySystem.TriggerDepressurizationVortex(
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
            return Mathf.Lerp(cap * 0.35f, cap, depthThreat01);
        }

        private float ResolveCinematicBreachVortexAcceleration()
        {
            float cap = Mathf.Max(0f, breachVortexMaximumAccelerationMetersPerSecondSquared);
            if (cap <= 0f)
                return 0f;

            float depthThreat01 = ResolveCinematicDepthThreat01(ResolveExternalDepthMeters());
            return Mathf.Lerp(cap * 0.25f, cap * 0.85f, depthThreat01);
        }

        private float ResolveCinematicDepthThreat01(float depthMeters)
        {
            float startDepth = ResolveImplosionDepthThresholdMeters();
            float fullDepth = Mathf.Max(startDepth + 1f, CinematicLeakFullDepthMeters);
            float depth01 = Mathf.Clamp01((depthMeters - startDepth) / (fullDepth - startDepth));
            return depth01 * depth01 * (3f - (2f * depth01));
        }

        private bool TryResolveSubmarineAtmosphereSystem(out SubmarineAtmosphereSystem atmosphereSystem)
        {
            if (_submarineAtmosphereSystem == null || !_submarineAtmosphereSystem.isActiveAndEnabled)
                _submarineAtmosphereSystem = GetComponentInParent<SubmarineAtmosphereSystem>();

            atmosphereSystem = _submarineAtmosphereSystem;
            return atmosphereSystem != null && atmosphereSystem.isActiveAndEnabled;
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
        }

        private void UpdateDrainDiagnostics()
        {
            _debugIsDraining = _integrityComponent.IsDraining;
            _debugDrainProgress = _integrityComponent.DrainProgress;
        }

        private void HandleLifeSupportSignals(ModuleLifeSupportSignals signals)
        {
            if (signals.AirQualityWarningRaised)
                RecordCascadeFailure("AIR SCRUBBERS SATURATED", _lifeSupportComponent.BuildAirReserveSummary());

            if (signals.AirReserveDepletedRaised)
            {
                RecordCascadeFailure(
                    "BREATHABLE RESERVE EXHAUSTED",
                    "Dry shelter air has collapsed into stale reserve. Occupants must evacuate or restore scrubber support.");
            }

            if (signals.Co2CriticalRaised)
                RecordCascadeFailure("CO2 SCRUBBER LOCKOUT", _lifeSupportComponent.BuildCo2CriticalSummary());

            if (signals.Co2HypoxiaRaised)
                TriggerCo2HypoxiaDistortion();
        }

        private void TriggerCo2HypoxiaDistortion()
        {
            if (_trackedPlayerMovement == null)
                return;

            float intensity = Mathf.InverseLerp(0.8f, 1f, Co2Normalized);
            _trackedPlayerMovement.TriggerHypoxiaVisorDistortion(Mathf.Clamp01(intensity), 0.45f, 2.5f);
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
