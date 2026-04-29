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
//   • Кэширование через Dictionary<int, BuoyancyObject> по InstanceID —
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
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Rendering;

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
    public sealed class BaseModule : MonoBehaviour, IPowerComponent, IPoolable, ISlowTickable, IFixedTickable, ICuttable
    {
        // COLD ALLOC: List<BaseModule>[64] - active runtime habitat module registry for cold-path environment scans - owner: BaseModule
        private static readonly List<BaseModule> s_activeModules = new List<BaseModule>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveModuleRegistry()
        {
            s_activeModules.Clear();
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

        [Tooltip("Maximum local-space center-of-mass shift toward the breach while the room floods.")]
        [SerializeField, Min(0.01f)] private float maximumCenterOfMassShiftMeters = 0.85f;

        [Tooltip("Blend time constant used when shifting center of mass toward the flooding breach.")]
        [SerializeField, Min(0.01f)] private float centerOfMassShiftTauSeconds = 1.2f;

        [Tooltip("Per-fixed-step clamp on center-of-mass movement to avoid solver spikes.")]
        [SerializeField, Min(0.001f)] private float maxCenterOfMassShiftPerTickMeters = 0.05f;

        [Tooltip("Flooded unmoored modules crossing this external depth get an additional crushing sink acceleration.")]
        [SerializeField, Min(1f)] private float hullCrushDepthMeters = 4000f;

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

        [Header("── Deconstruction ────────────────────────────")]
        [Tooltip("Префаб мирового предмета (HectonItem) для спавна ресурсов, " +
                 "которые не поместились в инвентарь. " +
                 "Должен иметь HectonItem + BuoyancyObject + Rigidbody.")]
        [SerializeField] private GameObject worldItemPrefab;

        [Header("── Visual References ─────────────────────────")]
        [Tooltip("Объект воды внутри модуля. Активен, когда модуль затоплен.")]
        [SerializeField] private GameObject waterVolume;

        [Tooltip("Эффект пузырьков / утечки при повреждении.")]
        [SerializeField] private ParticleSystem leakVfx;

        [Tooltip("Внутренние источники света. Выключаются при отсутствии питания.")]
        [SerializeField] private Light[] interiorLights;

        [Tooltip("Локальный Volume для тумана / постпроцесса затопления.")]
        [SerializeField] private Volume floodedLocalVolume;

        [Header("── Audio (optional) ──────────────────────────")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip leakLoop;
        [SerializeField] private AudioClip floodClip;
        [SerializeField] private AudioClip drainClip;
        [SerializeField] private AudioClip deconstructClip;
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
        private float _basePowerRating;
        private float _parasitePowerDrainWatts;
        private float _parasiteInfectionLevel;
        private bool _tickRegistered;
        private bool _fixedTickRegistered;
        private bool _isUnmoored;

        private ModuleMarker _moduleMarker;
        private HabitatIntegrityManager _habitatIntegrityManager;
        private SubmarineAtmosphereSystem _submarineAtmosphereSystem;
        private bool _breachLatched;
        private Rigidbody _moduleRigidbody;
        private int _cachedAtmosphereRoomIndex = -1;
        private Vector3 _defaultCenterOfMassLocal;
        private Vector3 _breachCenterOfMassTargetLocal;
        private bool _hasBreachCenterOfMassTarget;
        private float _defaultBodyMass;
        private float _defaultLinearDamping;
        private float _defaultAngularDamping;
        private bool _defaultBodyIsKinematic;
        private bool _defaultBodyUseGravity;
        private CollisionDetectionMode _defaultCollisionDetectionMode;
        private RigidbodyInterpolation _defaultInterpolation;
        private bool _moduleBodyDefaultsCaptured;

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
        private readonly Dictionary<int, BuoyancyObject> _trackedObjects
            = new Dictionary<int, BuoyancyObject>(TRACKED_INITIAL_CAPACITY);

        /// <summary>
        /// Временный список InstanceID для безопасного удаления из словаря
        /// во время итерации (при синхронизации состояния затопления).
        /// Pre-allocated, zero GC.
        /// </summary>
        private readonly List<int> _keysToRemove = new List<int>(TRACKED_INITIAL_CAPACITY);
        // COLD ALLOC: List<BaseAirlock>[2] — cached owned airlock controllers for emergency lockdown fan-out — owner: BaseModule
        private readonly List<BaseAirlock> _airlockBuffer = new List<BaseAirlock>(2);

        // COLD ALLOC: Collider[32] — resync interior occupants on enable/load/spawn — owner: BaseModule
        private readonly Collider[] _interiorOverlapBuffer = new Collider[INTERIOR_OVERLAP_CAPACITY];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES — для ConstructionManager save/load
        // ══════════════════════════════════════════════════════════

        /// <summary>Максимальная целостность (read-only).</summary>
        public float MaxIntegrity => maxIntegrity;
        internal static IReadOnlyList<BaseModule> ActiveModules => s_activeModules;

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
        public BaseModuleIntegrityState IntegrityState => ResolveIntegrityState();
        /// <summary>Normalized module integrity in the [0..1] range.</summary>
        public float IntegrityStateNormalized => _integrityComponent.MaxIntegrity > 0.01f
            ? Mathf.Clamp01(_integrityComponent.CurrentIntegrity / _integrityComponent.MaxIntegrity)
            : 0f;
        /// <summary>Normalized breathable reserve available for dry-zone life support.</summary>
        public float AirReserveNormalized => _lifeSupportComponent.AirReserveNormalized;
        /// <summary>True when the player is currently inside this module's interior volume.</summary>
        public bool IsPlayerInsideInterior => _trackedPlayerSurvival != null;
        /// <summary>True when breathable reserve has degraded into a stale-air window.</summary>
        public bool IsAirQualityLow => _lifeSupportComponent.IsAirQualityLow;
        /// <summary>Normalized CO2 saturation inside the module loop.</summary>
        public float Co2Normalized => _lifeSupportComponent.Co2Normalized;
        /// <summary>True when CO2 saturation has reached the life-support lockout threshold.</summary>
        public bool IsCo2Critical => _lifeSupportComponent.IsCo2Critical;
        /// <summary>True when CO2 saturation has crossed the toxic dry-room threshold.</summary>
        public bool IsCo2Toxic => _lifeSupportComponent.IsCo2Toxic;
        /// <summary>Normalized dry-room toxicity hazard intensity derived from local CO2 saturation.</summary>
        public float Co2ToxicHazardIntensity => _lifeSupportComponent.ToxicHazardIntensity;
        /// <summary>True when the habitat graph has cut this module off from every anchor.</summary>
        public bool IsUnmoored => _isUnmoored;
        internal float BreathableReserve => _lifeSupportComponent.BreathableReserve;
        internal float BreathableReserveCapacity => _lifeSupportComponent.BreathableReserveCapacity;

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Базовое энергопотребление модуля.
        /// Источник: BuildableData.powerRating → fallback.
        /// </summary>
        public float PowerRating => _basePowerRating - ResolveFloodPumpPowerDraw() - _parasitePowerDrainWatts;

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
            _isDeconstructing = false;
            _ambientLightsBrownedOut = false;
            _breachLatched = IsBreached;
            _cachedAtmosphereRoomIndex = -1;
            _hasBreachCenterOfMassTarget = false;

            RefreshVisualStateImmediate();
            ResyncInteriorOccupants(true);
            _integrityComponent.TryStartDrain(_hasPower);
            UpdateDrainDiagnostics();
            SyncSpatialRole();
            BaseDegradationSystem.SynchronizeIntegrityState(this);
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
            SetLightsEnabled(true);

            _isDeconstructing = false;
            _breachLatched = false;
            _cachedAtmosphereRoomIndex = -1;
            _hasBreachCenterOfMassTarget = false;
            _trackedPlayerSurvival = null;
            _integrityComponent.ResetForDespawn();
            _lifeSupportComponent.ResetForDespawn();
            TryUnregisterFixedTick();
            DisableUnmooredPhysics();
            SyncSpatialRole();
            BaseDegradationSystem.ClearIntegrityState(this);

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
            ApplyCascadeFailureEffects();
            UpdateLifeSupport(SLOW_TICK_DT);
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

            float floodFill01 = ResolveUnmooredFloodFillNormalized();
            float displacementVolume = ResolveBuoyancyDisplacementVolumeCubicMeters();
            float dryMass = ResolveDryMassKilograms();
            float effectiveMass = Mathf.Max(
                MinimumMassKilograms,
                dryMass + (floodFill01 * displacementVolume * SeawaterDensityKilogramsPerCubicMeter));
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
                    transform.TransformPoint(_defaultCenterOfMassLocal),
                    ForceMode.Acceleration);
            }

            ApplyFloodWeightedCenterOfMass(fixedDeltaTime, floodFill01);
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

            _wasFlooded = _integrityComponent.IsFlooded;
            CacheAirlockComponents();
            CaptureModuleRigidbodyDefaults();
        }

        private void OnEnable()
        {
            if (!s_activeModules.Contains(this))
                s_activeModules.Add(this);

            TryRegister();
            ResyncInteriorOccupants(true);
            BaseDegradationSystem.SynchronizeIntegrityState(this);
            if (_isUnmoored)
            {
                EnableUnmooredPhysics();
                TryRegisterFixedTick();
            }
        }

        private void OnDisable()
        {
            s_activeModules.Remove(this);
            TryUnregister();
            TryUnregisterFixedTick();
            BaseDegradationSystem.ClearIntegrityState(this);

            NotifyModuleExitIfNeeded();
            ReleaseAllTrackedObjects();
        }

        private void OnDestroy()
        {
            s_activeModules.Remove(this);
            TryUnregister();
            TryUnregisterFixedTick();
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
            int key = unchecked((int)EntityId.ToULong(other.GetEntityId()));

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
            _integrityComponent.ForceFlood();
            UpdateDrainDiagnostics();
            SetFloodedVisual(true);
            SyncTrackedObjectsFloodState();
            SyncSpatialRole();
            PlaySpatialSfx(floodClip);
        }

        /// <summary>
        /// Принудительное завершение осушения. Сбрасывает drain state и визуал.
        /// </summary>
        public void ForceDrainComplete()
        {
            _integrityComponent.ForceDrainComplete(clearOxygenLeakFailure: true);
            UpdateDrainDiagnostics();
            SetFloodedVisual(false);
            SyncTrackedObjectsFloodState();
            SyncSpatialRole();
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
                        ? Mathf.Clamp01(moduleTemplate.DefaultIntegrityState)
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
            ConfigureRuntimeComponentsFromSerializedState();
            _integrityComponent.RestoreState(integrity, flooded, cascadeFailure, repairIntegrityCap);
            _lifeSupportComponent.RestoreState(airReserveNormalized, co2Normalized);
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
            ObjectPoolManager pool = ObjectPoolManager.Instance;
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
            ConstructionManager cm = ConstructionManager.Instance;
            if (cm != null)
            {
                cm.DestroyModule(gameObject);
            }
            else
            {
                // Fallback: если ConstructionManager недоступен
                ObjectPoolManager fallbackPool = ObjectPoolManager.Instance;
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

            PersistentWorldRegistry persistentWorldRegistry = PersistentWorldRegistry.Instance;
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

            PlayerInventory inventoryInstance = PlayerInventory.Instance;
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
            if (_ambientLightsBrownedOut == brownedOut)
                return;

            _ambientLightsBrownedOut = brownedOut;
            SetLightsEnabled(ShouldLightsBeEnabled());
        }

        private bool HasOperationalPower => _integrityComponent.HasOperationalPower(_hasPower);
        private bool ShouldLightsBeEnabled() => HasOperationalPower && !_ambientLightsBrownedOut;

        private BaseModuleIntegrityState ResolveIntegrityState()
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

        private void UpdateLifeSupport(float dt)
        {
            ModuleLifeSupportSignals signals = _lifeSupportComponent.Tick(
                dt,
                !_integrityComponent.IsFlooded && _integrityComponent.FailureMode != BaseModuleFailureMode.Fire,
                HasOperationalPower,
                _trackedPlayerSurvival);

            HandleLifeSupportSignals(signals);
        }

        private float ResolveAirRefillScale()
        {
            return IsAirQualityLow ? staleAirMinRefillScale : 1f;
        }

        private void TrackAirReserveStateTransitions()
        {
            HandleLifeSupportSignals(_lifeSupportComponent.Tick(
                0f,
                !_integrityComponent.IsFlooded && _integrityComponent.FailureMode != BaseModuleFailureMode.Fire,
                HasOperationalPower,
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

            Dictionary<int, BuoyancyObject>.Enumerator enumerator = _trackedObjects.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, BuoyancyObject> kvp = enumerator.Current;
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

            Dictionary<int, BuoyancyObject>.Enumerator enumerator = _trackedObjects.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, BuoyancyObject> kvp = enumerator.Current;
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
                ~0,
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
            if (waterVolume != null && waterVolume.activeSelf != flooded)
                waterVolume.SetActive(flooded);

            if (floodedLocalVolume != null && floodedLocalVolume.enabled != flooded)
                floodedLocalVolume.enabled = flooded;
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

            if (interiorTrigger == null)
                interiorTrigger = GetComponentInChildren<BoxCollider>(true);

            CacheAirlockComponents();
            CaptureModuleRigidbodyDefaults();
        }

        private void CacheAirlockComponents()
        {
            _airlockBuffer.Clear();
            GetComponentsInChildren(true, _airlockBuffer);
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

            _defaultCenterOfMassLocal = _moduleRigidbody.centerOfMass;
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
            _moduleRigidbody.centerOfMass = _defaultCenterOfMassLocal;
        }

        private void DisableUnmooredPhysics()
        {
            if (_moduleRigidbody == null || !_moduleBodyDefaultsCaptured)
                return;

            _moduleRigidbody.centerOfMass = _defaultCenterOfMassLocal;
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

            return 0f;
        }

        private float ResolveDryMassKilograms()
        {
            if (moduleTemplate != null)
                return Mathf.Max(MinimumMassKilograms, moduleTemplate.StructuralDryMassKilograms);

            return Mathf.Max(MinimumMassKilograms, structuralDryMassKilograms);
        }

        private float ResolveBuoyancyDisplacementVolumeCubicMeters()
        {
            if (moduleTemplate != null)
                return Mathf.Max(0.1f, moduleTemplate.BuoyancyDisplacementVolumeCubicMeters);

            return Mathf.Max(0.1f, buoyancyDisplacementVolumeCubicMeters);
        }

        private float ResolveMaximumUnmooredAccelerationMetersPerSecondSquared()
        {
            if (moduleTemplate != null)
                return Mathf.Max(0.1f, moduleTemplate.MaximumUnmooredAccelerationMetersPerSecondSquared);

            return Mathf.Max(0.1f, maximumUnmooredAccelerationMetersPerSecondSquared);
        }

        private float ResolveMaximumCenterOfMassShiftMeters()
        {
            if (moduleTemplate != null)
                return Mathf.Max(0.01f, moduleTemplate.MaximumCenterOfMassShiftMeters);

            return Mathf.Max(0.01f, maximumCenterOfMassShiftMeters);
        }

        private float ResolveCenterOfMassShiftTauSeconds()
        {
            if (moduleTemplate != null)
                return Mathf.Max(0.01f, moduleTemplate.CenterOfMassShiftTauSeconds);

            return Mathf.Max(0.01f, centerOfMassShiftTauSeconds);
        }

        private void ApplyFloodWeightedCenterOfMass(float fixedDeltaTime, float floodFill01)
        {
            if (_moduleRigidbody == null)
                return;

            Vector3 targetCenterOfMass = _defaultCenterOfMassLocal;
            if (_hasBreachCenterOfMassTarget && floodFill01 > 0.001f)
            {
                Vector3 offsetFromCenter = _breachCenterOfMassTargetLocal - _defaultCenterOfMassLocal;
                float maxShift = ResolveMaximumCenterOfMassShiftMeters();
                if (offsetFromCenter.sqrMagnitude > (maxShift * maxShift))
                    offsetFromCenter = offsetFromCenter.normalized * maxShift;

                targetCenterOfMass += offsetFromCenter * floodFill01;
            }

            float tauSeconds = ResolveCenterOfMassShiftTauSeconds();
            float alpha = 1f - Mathf.Exp(-fixedDeltaTime / tauSeconds);
            Vector3 nextCenterOfMass = Vector3.Lerp(_moduleRigidbody.centerOfMass, targetCenterOfMass, alpha);
            Vector3 clampedDelta = nextCenterOfMass - _moduleRigidbody.centerOfMass;
            float maxStep = Mathf.Max(0.001f, maxCenterOfMassShiftPerTickMeters);
            if (clampedDelta.sqrMagnitude > (maxStep * maxStep))
                nextCenterOfMass = _moduleRigidbody.centerOfMass + clampedDelta.normalized * maxStep;

            _moduleRigidbody.centerOfMass = nextCenterOfMass;
        }

        private void NotifyEmergencyLockdownStateChanged()
        {
            ConstructionManager manager = ConstructionManager.Instance;
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
            int key = unchecked((int)EntityId.ToULong(other.GetEntityId()));

            if (_trackedObjects.ContainsKey(key))
                return;

            _trackedObjects[key] = buoyancy;
            UpdateTrackedDiagnostics();

            if (!_integrityComponent.IsFlooded)
                buoyancy.EnterDryZone();
        }

        private void NotifyModuleExitIfNeeded()
        {
            if (_trackedPlayerSurvival == null)
                return;

            _trackedPlayerSurvival = null;
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

        internal void SetEmergencyBulkheadLockdown(bool lockedDown)
        {
            if (!ResolveEmergencyAirlockRole())
                return;

            CacheAirlockComponents();
            for (int i = 0; i < _airlockBuffer.Count; i++)
            {
                BaseAirlock airlock = _airlockBuffer[i];
                if (airlock != null)
                    airlock.SetEmergencyLockdown(lockedDown);
            }
        }

        private void TryRegister()
        {
            if (_tickRegistered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _tickRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryRegisterFixedTick()
        {
            if (_fixedTickRegistered || !_isUnmoored)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _fixedTickRegistered = true;
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

        internal float ParasiteInfectionLevel => _parasiteInfectionLevel;

        internal bool SetParasiteInfestation(float powerDrainWatts, float infectionLevel)
        {
            float sanitizedDrain = Mathf.Max(0f, powerDrainWatts);
            float sanitizedInfection = Mathf.Clamp01(infectionLevel);
            if (Mathf.Abs(_parasitePowerDrainWatts - sanitizedDrain) <= 0.01f &&
                Mathf.Abs(_parasiteInfectionLevel - sanitizedInfection) <= 0.001f)
            {
                return false;
            }

            _parasitePowerDrainWatts = sanitizedDrain;
            _parasiteInfectionLevel = sanitizedInfection;
            TryMarkPowerGridDirty();
            return true;
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
            if (_submarineAtmosphereSystem == null)
                return 0f;

            int roomIndex = _submarineAtmosphereSystem.ResolveNearestRoomIndexForWorldPosition(transform.position);
            return roomIndex >= 0
                ? _submarineAtmosphereSystem.GetRoomTemperatureCelsius(roomIndex)
                : 0f;
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
            if (!TryGetComponent(out PowerNode powerNode) || powerNode.Grid == null)
                return;

            powerNode.Grid.MarkDirty();
        }

        internal void ApplyFloodExposure(float normalizedFloodDelta, float co2Amplifier)
        {
            _lifeSupportComponent.ApplyFloodExposure(normalizedFloodDelta, co2Amplifier);
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
            if (leakVfx == null)
                return;

            float burst01 = Mathf.Clamp01(pressureDelta * 0.25f);
            ParticleSystem.EmitParams emitParams = default;
            ParticleSystem.MainModule main = leakVfx.main;
            bool worldSpace = main.simulationSpace == ParticleSystemSimulationSpace.World;
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            Vector3 worldDirection = transform.position - worldPoint;
            if (worldDirection.sqrMagnitude < 0.0001f)
                worldDirection = -transform.forward;

            worldDirection.Normalize();
            Vector3 burstVelocity = worldDirection * Mathf.Lerp(4f, 18f, burst01);

            emitParams.position = worldSpace ? worldPoint : localPoint;
            emitParams.velocity = worldSpace ? burstVelocity : transform.InverseTransformDirection(burstVelocity);
            emitParams.startSize = Mathf.Lerp(0.05f, 0.18f, burst01);
            emitParams.startLifetime = Mathf.Lerp(0.35f, 1.15f, burst01);
            emitParams.applyShapeToPosition = true;

            leakVfx.Emit(emitParams, Mathf.RoundToInt(Mathf.Lerp(6f, 24f, burst01)));
            if (!leakVfx.isPlaying)
                leakVfx.Play();
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

        private void HandleIntegrityCollapse(Vector3 localBreachPoint)
        {
            if (_breachLatched)
                return;

            _breachLatched = true;
            _breachCenterOfMassTargetLocal = localBreachPoint;
            _hasBreachCenterOfMassTarget = true;
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
            if (maximumCenterOfMassShiftMeters < 0.01f) maximumCenterOfMassShiftMeters = 0.01f;
            if (centerOfMassShiftTauSeconds < 0.01f) centerOfMassShiftTauSeconds = 0.01f;
            if (maxCenterOfMassShiftPerTickMeters < 0.001f) maxCenterOfMassShiftPerTickMeters = 0.001f;
            if (hullCrushDepthMeters < 1f) hullCrushDepthMeters = 1f;
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
