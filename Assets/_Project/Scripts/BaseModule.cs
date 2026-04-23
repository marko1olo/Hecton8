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

using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.Power;
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

    [DisallowMultipleComponent]
    public sealed class BaseModule : MonoBehaviour, IPowerComponent, IPoolable, ISlowTickable, ICuttable
    {
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

        [Tooltip("Модуль затоплен на старте? Обычно false.")]
        [SerializeField] private bool isFlooded;

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
        private bool _tickRegistered;

        private ModuleMarker _moduleMarker;
        private HabitatIntegrityManager _habitatIntegrityManager;

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
        /// Key: Collider.GetInstanceID() (не GameObject — т.к. триггер видит Collider).
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

        // COLD ALLOC: Collider[32] — resync interior occupants on enable/load/spawn — owner: BaseModule
        private readonly Collider[] _interiorOverlapBuffer = new Collider[INTERIOR_OVERLAP_CAPACITY];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES — для ConstructionManager save/load
        // ══════════════════════════════════════════════════════════

        /// <summary>Максимальная целостность (read-only).</summary>
        public float MaxIntegrity => maxIntegrity;

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
        public int RemainingRepairCycles => ResolveRemainingRepairCycles();
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
        internal float BreathableReserve => _lifeSupportComponent.BreathableReserve;
        internal float BreathableReserveCapacity => _lifeSupportComponent.BreathableReserveCapacity;

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Базовое энергопотребление модуля.
        /// Источник: BuildableData.powerRating → fallback.
        /// </summary>
        public float PowerRating => _basePowerRating - ResolveFloodPumpPowerDraw();

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
            ApplyDamage(damage);

            if (_habitatIntegrityManager != null && _integrityComponent.CurrentIntegrity <= 0f)
                _habitatIntegrityManager.NotifyHullBreach(transform.InverseTransformPoint(hitPoint));
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

            RefreshVisualStateImmediate();
            ResyncInteriorOccupants(true);
            _integrityComponent.TryStartDrain(_hasPower);
            UpdateDrainDiagnostics();
            SyncSpatialRole();
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
            _trackedPlayerSurvival = null;
            _integrityComponent.ResetForDespawn();
            _lifeSupportComponent.ResetForDespawn();
            SyncSpatialRole();

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
        }

        private void OnEnable()
        {
            TryRegister();
            ResyncInteriorOccupants(true);
        }

        private void OnDisable()
        {
            TryUnregister();

            NotifyModuleExitIfNeeded();
            ReleaseAllTrackedObjects();
        }

        private void OnDestroy()
        {
            TryUnregister();
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
            #pragma warning disable CS0618
            int key = other.GetInstanceID();
            #pragma warning restore CS0618

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
            float previousIntegrityNormalized = _integrityComponent.MaxIntegrity > 0.01f
                ? Mathf.Clamp01(_integrityComponent.CurrentIntegrity / _integrityComponent.MaxIntegrity)
                : 0f;
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
                signal.localPoint = new Unity.Mathematics.float3(0f, 0f, 0f);
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

            _integrityComponent.StopDrain();
            UpdateDrainDiagnostics();
            RefreshVisualStateImmediate();
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
            SyncSpatialRole();
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
                        if (grid != null)
                        {
                            int px, py;
                            if (grid.TryAddItem(cost.item, out px, out py))
                            {
                                playerInventory.AddWeight(cost.item.weight);
                                addedToInventory = true;
                            }
                        }

                        // ── Fallback: спавн в мир ──
                        if (!addedToInventory)
                        {
                            SpawnWorldItem(cost.item, dropPosition, pool);

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
            ItemData itemData,
            int quantity,
            PlayerInventory playerInventory,
            ObjectPoolManager pool,
            ref Vector3 dropPosition)
        {
            if (itemData == null || quantity <= 0)
                return;

            InventoryGrid targetGrid = playerInventory != null ? playerInventory.Grid : null;
            for (int i = 0; i < quantity; i++)
            {
                bool addedToInventory = false;
                if (targetGrid != null)
                {
                    int px;
                    int py;
                    if (targetGrid.TryAddItem(itemData, out px, out py))
                    {
                        playerInventory.AddWeight(itemData.weight);
                        addedToInventory = true;
                    }
                }

                if (addedToInventory)
                    continue;

                SpawnWorldItem(itemData, dropPosition, pool);
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
        ///   2. Спавненный HectonItem инициализируется данными ItemData.
        ///   3. Если worldItemPrefab == null → ресурс потерян (с Warning).
        ///
        /// Разделение ответственностей:
        ///   BaseModule НЕ знает про конкретный визуал предмета.
        ///   worldItemPrefab — generic контейнер с HectonItem + Rigidbody.
        ///   ItemData на HectonItem устанавливается программно.
        ///
        /// Будущее: если нужна визуальная дифференциация (разные модели
        /// для титана vs стекла), worldItemPrefab может быть заменён
        /// на ItemData.worldPrefab per-resource.
        /// </summary>
        private void SpawnWorldItem(ItemData itemData, Vector3 position, ObjectPoolManager pool)
        {
            if (itemData == null)
                return;

            // Небольшой случайный разброс, чтобы предметы не стакались
            Vector3 offset;
            offset.x = UnityEngine.Random.Range(-0.4f, 0.4f);
            offset.y = UnityEngine.Random.Range(0f, 0.3f);
            offset.z = UnityEngine.Random.Range(-0.4f, 0.4f);

            Vector3 spawnPosition = position + offset;
            PersistentWorldRegistry persistentWorldRegistry = PersistentWorldRegistry.Instance;
            if (persistentWorldRegistry != null &&
                itemData.worldPrefab != null &&
                persistentWorldRegistry.TryRegisterDroppedItem(itemData, 1, spawnPosition))
            {
                return;
            }

            if (worldItemPrefab == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[BaseModule] worldItemPrefab not assigned on '{gameObject.name}'. " +
                    $"Resource '{itemData.itemName}' dropped on the ground but has no world prefab. Lost.",
                    this);
#endif
                return;
            }

            if (pool == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    "[BaseModule] ObjectPoolManager not available. " +
                    $"Resource '{itemData.itemName}' lost.");
#endif
                return;
            }

            GameObject itemGO = pool.Spawn(worldItemPrefab, spawnPosition, Quaternion.identity);

            if (itemGO == null)
                return;

            // ── Инициализация HectonItem данными ──
            // HectonItem на worldItemPrefab должен иметь сериализованное поле itemData.
            // Однако itemData — [SerializeField] private. Для программной установки
            // используем рефлексию-бесплатный подход: HectonItem.SetItemData(ItemData, int).
            // Если такой метод не существует — предмет будет иметь пустые данные.
            //
            // АРХИТЕКТУРНОЕ РЕШЕНИЕ:
            // Мы добавляем public метод SetItemData в HectonItem (см. комментарий ниже).
            // Это чище, чем рефлексия, и сохраняет Zero-GC.
            if (itemGO.TryGetComponent(out HectonItem hectonItem))
            {
                hectonItem.SetItemData(itemData, 1);
            }
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
                maintenanceStation.TryExtractSlottedToolForDeconstruct(out ItemData slottedTool))
            {
                DropItemQuantityToInventoryOrWorld(slottedTool, 1, playerInventory, pool, ref dropPosition);
            }

            if (TryGetComponent(out DeepDrillModule drillModule))
                drillModule.EjectBufferedOutput(this, playerInventory, pool, ref dropPosition);

            if (TryGetComponent(out LogisticsSorterModule sorterModule))
                sorterModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);

            if (TryGetComponent(out LogisticsPipeNode pipeNode) &&
                pipeNode.TryExtractInFlightCargoForDeconstruct(out ItemData pipeItem, out int pipeAmount))
            {
                DropItemQuantityToInventoryOrWorld(pipeItem, pipeAmount, playerInventory, pool, ref dropPosition);
            }
        }

        private void EnsureRepairIntegrityCapInitialized()
        {
            ConfigureRuntimeComponentsFromSerializedState();
        }

        private int ResolveRemainingRepairCycles()
        {
            return _integrityComponent.ResolveRemainingRepairCycles();
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

            SpatialAudioManager sam = SpatialAudioManager.Instance;
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

            if (leakVfx == null)
                ResolveLeakVfxReference();

            if (_habitatIntegrityManager == null)
            {
                if (!TryGetComponent(out _habitatIntegrityManager))
                    _habitatIntegrityManager = gameObject.AddComponent<HabitatIntegrityManager>();
            }
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
            #pragma warning disable CS0618
            int key = other.GetInstanceID();
            #pragma warning restore CS0618

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
            }
            else
            {
                _basePowerRating = fallbackPowerRating;
            }
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

        private void TryRegister()
        {
            if (_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ISlowTickable)this);
            _tickRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ISlowTickable)this);

            _tickRegistered = false;
        }

        /// <summary>
        /// Allows botanical modules to chemically pull CO2 out of this module's dry-air loop.
        /// </summary>
        public void ApplyBotanyScrub(float amount)
        {
            _lifeSupportComponent.ScrubCo2(amount);
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
