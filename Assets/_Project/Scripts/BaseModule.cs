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
        private bool _isDraining;
        private float _drainTimer;
        private float _basePowerRating;
        private bool _tickRegistered;

        private ModuleMarker _moduleMarker;

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
        private bool _airReserveWarningLatched;
        private bool _airReserveDepletedLatched;
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
            get => currentIntegrity;
            set => currentIntegrity = Mathf.Clamp(value, 0f, GetRepairIntegrityCap());
        }

        /// <summary>
        /// Флаг затопления. ConstructionManager записывает сюда
        /// значение при загрузке сохранения.
        /// </summary>
        public bool IsFlooded
        {
            get => isFlooded;
            set => isFlooded = value;
        }

        /// <summary>Целостность упала до нуля — модуль пробит.</summary>
        public bool IsBreached => currentIntegrity <= 0f;

        /// <summary>Идёт ли сейчас откачка воды.</summary>
        public bool IsDraining => _isDraining;

        /// <summary>Идёт ли деконструкция (защита от повторных вызовов).</summary>
        public bool IsDeconstructing => _isDeconstructing;

        /// <summary>Текущий каскадный аварийный статус модуля.</summary>
        public BaseModuleFailureMode CurrentFailureMode => failureMode;

        /// <summary>Модуль находится в аварийном каскадном состоянии.</summary>
        public bool HasCascadeFailure => failureMode != BaseModuleFailureMode.None;

        /// <summary>Current repair ceiling after accumulated material fatigue.</summary>
        public float MaxRecoverableIntegrity => GetRepairIntegrityCap();
        /// <summary>Normalized breathable reserve available for dry-zone life support.</summary>
        public float AirReserveNormalized => breathableReserveCapacity > 0.01f ? Mathf.Clamp01(breathableReserve / breathableReserveCapacity) : 1f;
        /// <summary>True when the player is currently inside this module's interior volume.</summary>
        public bool IsPlayerInsideInterior => _trackedPlayerSurvival != null;
        /// <summary>True when breathable reserve has degraded into a stale-air window.</summary>
        public bool IsAirQualityLow => AirReserveNormalized <= staleAirThreshold;

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Базовое энергопотребление модуля.
        /// Источник: BuildableData.powerRating → fallback.
        /// </summary>
        public float PowerRating => _basePowerRating;

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

            SetLightsEnabled(HasOperationalPower);

            if (!HasOperationalPower)
            {
                StopDrain();
            }
            else
            {
                TryStartDrain();
            }

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
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public void OnSpawn()
        {
            CacheReferences();
            ReadBuildablePower();

            EnsureRepairIntegrityCapInitialized();
            InitializeBreathableReserveCold();
            currentIntegrity = Mathf.Clamp(currentIntegrity, 0f, GetRepairIntegrityCap());
            _wasFlooded = isFlooded;
            _isDeconstructing = false;

            RefreshVisualStateImmediate();
            ResyncInteriorOccupants(true);
            TryStartDrain();
            SyncSpatialRole();
        }

        public void OnDespawn()
        {
            NotifyModuleExitIfNeeded();
            StopDrain();
            SetLeakActive(false);
            SetFloodedVisual(false);
            SetLightsEnabled(true);

            _isDeconstructing = false;
            _trackedPlayerSurvival = null;
            failureMode = BaseModuleFailureMode.None;
            maxRecoverableIntegrity = maxIntegrity;
            breathableReserve = breathableReserveCapacity;
            _airReserveWarningLatched = false;
            _airReserveDepletedLatched = false;
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
                failureMode == BaseModuleFailureMode.None &&
                currentIntegrity > 0f &&
                currentIntegrity < repairCap)
            {
                Repair(passiveRecoveryRate * SLOW_TICK_DT);
            }

            // Пассивная деградация — лор: давление, время, глубина
            if (passiveDegradationRate > 0f && currentIntegrity > 0f)
            {
                float degradation = passiveDegradationRate * SLOW_TICK_DT;

                // Глубина > 500м — усиленная деградация от давления
                if (_trackedPlayerSurvival != null && _trackedPlayerSurvival.Depth > 500f)
                    degradation *= depthDegradationMultiplier;

                ApplyDamage(degradation);
            }

            if (!_isDraining)
                return;

            _drainTimer += SLOW_TICK_DT;

            float progress = drainDuration > 0.01f
                ? _drainTimer / drainDuration
                : 1f;

            if (progress >= 1f)
            {
                ForceDrainComplete();
                progress = 1f;
            }

            _debugIsDraining = _isDraining;
            _debugDrainProgress = progress > 1f ? 1f : progress;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            CacheReferences();
            ReadBuildablePower();
            ValidateInteriorTrigger();
            InitializeBreathableReserveCold();

            _wasFlooded = isFlooded;
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

                if (buoyancy != null && !isFlooded)
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
            if (amount <= 0f) return;
            if (currentIntegrity <= 0f) return;

            currentIntegrity -= amount;
            if (currentIntegrity < 0f)
                currentIntegrity = 0f;

            if (currentIntegrity <= 0f)
            {
                TriggerCascadeFailure();
            }
            else
            {
                SetLeakActive(ShouldLeakBeActive());
            }

            StopDrain();
            RefreshVisualStateImmediate();
        }

        /// <summary>
        /// Ремонтирует модуль.
        /// Если целостность полностью восстановлена и есть питание —
        /// начинается откачка воды.
        /// </summary>
        public void Repair(float amount)
        {
            if (amount <= 0f) return;
            float repairCap = GetRepairIntegrityCap();
            if (currentIntegrity >= repairCap && !isFlooded) return;

            currentIntegrity += amount;
            if (currentIntegrity > repairCap)
                currentIntegrity = repairCap;

            if (currentIntegrity >= repairCap)
            {
                currentIntegrity = repairCap;
                if (failureMode == BaseModuleFailureMode.Fire ||
                    failureMode == BaseModuleFailureMode.ShortCircuit)
                {
                    ClearCascadeFailure();
                }

                SetLeakActive(ShouldLeakBeActive());
                TryStartDrain();
            }
            else
            {
                SetLeakActive(ShouldLeakBeActive());
            }

            RefreshVisualStateImmediate();
        }

        /// <summary>
        /// Принудительное затопление. Останавливает drain, активирует визуал.
        /// </summary>
        public void ForceFlood()
        {
            isFlooded = true;
            StopDrain();
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
            isFlooded = false;
            StopDrain();
            if (failureMode == BaseModuleFailureMode.OxygenLeak)
                ClearCascadeFailure();
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
            EnsureRepairIntegrityCapInitialized();
            currentIntegrity = Mathf.Clamp(currentIntegrity, 0f, GetRepairIntegrityCap());
            breathableReserveCapacity = Mathf.Max(1f, breathableReserveCapacity);
            breathableReserve = Mathf.Clamp(breathableReserve, 0f, breathableReserveCapacity);
            _wasFlooded = isFlooded;
            _airReserveWarningLatched = IsAirQualityLow;
            _airReserveDepletedLatched = breathableReserve <= 0f;
            RefreshVisualStateImmediate();
            SyncTrackedObjectsFloodState();
            ResyncInteriorOccupants(true);
            TryStartDrain();
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
            breathableReserveCapacity = Mathf.Max(1f, breathableReserveCapacity);
            maxRecoverableIntegrity = Mathf.Clamp(repairIntegrityCap, maxIntegrity * minimumRecoverableIntegrityRatio, maxIntegrity);
            currentIntegrity = Mathf.Clamp(integrity, 0f, GetRepairIntegrityCap());
            breathableReserve = Mathf.Clamp01(airReserveNormalized) * breathableReserveCapacity;
            isFlooded = flooded;
            _wasFlooded = flooded;
            failureMode = cascadeFailure;
            _airReserveWarningLatched = IsAirQualityLow;
            _airReserveDepletedLatched = breathableReserve <= 0f;
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
                Vector3 dropPosition = transform.position + Vector3.up * 0.5f;

                InventoryGrid grid = playerInventory != null ? playerInventory.Grid : null;
                ObjectPoolManager pool = ObjectPoolManager.Instance;

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
                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool != null)
                    pool.Despawn(gameObject);
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

            // Небольшой случайный разброс, чтобы предметы не стакались
            Vector3 offset;
            offset.x = UnityEngine.Random.Range(-0.4f, 0.4f);
            offset.y = UnityEngine.Random.Range(0f, 0.3f);
            offset.z = UnityEngine.Random.Range(-0.4f, 0.4f);

            GameObject itemGO = pool.Spawn(worldItemPrefab, position + offset, Quaternion.identity);

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
            float minimumCap = maxIntegrity * minimumRecoverableIntegrityRatio;
            if (minimumCap < 1f)
                minimumCap = 1f;

            return Mathf.Clamp(maxRecoverableIntegrity, minimumCap, maxIntegrity);
        }

        private void EnsureRepairIntegrityCapInitialized()
        {
            if (maxRecoverableIntegrity <= 0f)
                maxRecoverableIntegrity = maxIntegrity;

            maxRecoverableIntegrity = GetRepairIntegrityCap();
        }

        private void InitializeBreathableReserveCold()
        {
            breathableReserveCapacity = Mathf.Max(1f, breathableReserveCapacity);

            if (breathableReserve <= 0f)
                breathableReserve = breathableReserveCapacity;

            breathableReserve = Mathf.Clamp(breathableReserve, 0f, breathableReserveCapacity);
            _airReserveWarningLatched = IsAirQualityLow;
            _airReserveDepletedLatched = breathableReserve <= 0f;
        }

        private void ApplyMaterialFatigue()
        {
            EnsureRepairIntegrityCapInitialized();

            if (repairWearPerCascade <= 0f)
                return;

            float minimumCap = maxIntegrity * minimumRecoverableIntegrityRatio;
            if (minimumCap < 1f)
                minimumCap = 1f;

            maxRecoverableIntegrity -= repairWearPerCascade;
            if (maxRecoverableIntegrity < minimumCap)
                maxRecoverableIntegrity = minimumCap;
        }

        private bool HasOperationalPower => _hasPower && failureMode != BaseModuleFailureMode.ShortCircuit;

        private void TriggerCascadeFailure()
        {
            ApplyMaterialFatigue();
            failureMode = ResolveCascadeFailureMode();
            _isDraining = false;
            _drainTimer = 0f;

            switch (failureMode)
            {
                case BaseModuleFailureMode.Fire:
                    isFlooded = false;
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("MODULE FIRE", "Compartment ignition risk. Repair before occupancy.");
                    NotificationEvents.PushWarning("BASE MODULE FIRE // SERVICE NOW");
                    break;
                case BaseModuleFailureMode.ShortCircuit:
                    isFlooded = true;
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("SHORT CIRCUIT", "Compartment flooded and pumps offline until hull service completes.");
                    NotificationEvents.PushWarning("BASE SHORT CIRCUIT // POWER LOCKOUT");
                    break;
                default:
                    failureMode = BaseModuleFailureMode.OxygenLeak;
                    isFlooded = true;
                    PlaySpatialSfx(floodClip);
                    RecordCascadeFailure("OXYGEN LEAK", "Compartment seal lost. Oxygen-safe shelter compromised.");
                    NotificationEvents.PushWarning("BASE OXYGEN LEAK // COMPARTMENT BREACHED");
                    break;
            }

            SetLeakActive(ShouldLeakBeActive());
            SetFloodedVisual(isFlooded);
            SyncTrackedObjectsFloodState();
            SetLightsEnabled(HasOperationalPower);
            SyncSpatialRole();
        }

        private void TryStartDrain()
        {
            if (!HasOperationalPower) return;
            if (!isFlooded) return;
            if (currentIntegrity < GetRepairIntegrityCap()) return;

            _isDraining = true;
            if (_drainTimer <= 0f)
                PlaySpatialSfx(drainClip);
        }

        private void StopDrain()
        {
            _isDraining = false;
            _drainTimer = 0f;
            _debugIsDraining = false;
            _debugDrainProgress = 0f;
        }

        private void RefreshVisualStateImmediate()
        {
            SetLeakActive(ShouldLeakBeActive());

            SetFloodedVisual(isFlooded);
            SetLightsEnabled(HasOperationalPower);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INTERIOR ZONE SYNC
        // ══════════════════════════════════════════════════════════

        private bool ShouldLeakBeActive()
        {
            if (failureMode == BaseModuleFailureMode.OxygenLeak ||
                failureMode == BaseModuleFailureMode.Fire)
            {
                return true;
            }

            return currentIntegrity < GetRepairIntegrityCap() && currentIntegrity > 0f;
        }

        private void ApplyCascadeFailureEffects()
        {
            if (_trackedPlayerSurvival == null)
                return;

            switch (failureMode)
            {
                case BaseModuleFailureMode.OxygenLeak:
                    if (oxygenLeakDrainRate > 0f)
                        _trackedPlayerSurvival.DrainOxygen(oxygenLeakDrainRate * SLOW_TICK_DT);
                    break;
                case BaseModuleFailureMode.Fire:
                    if (fireSuitDamageRate > 0f)
                        _trackedPlayerSurvival.TakeDamage(fireSuitDamageRate * SLOW_TICK_DT);
                    if (fireSuitEnergyDrainRate > 0f)
                        _trackedPlayerSurvival.DrainEnergy(fireSuitEnergyDrainRate * SLOW_TICK_DT);
                    break;
            }
        }

        private void UpdateLifeSupport(float dt)
        {
            bool dryCompartment = !isFlooded && failureMode != BaseModuleFailureMode.Fire;

            if (dryCompartment && HasOperationalPower && airRecycleRate > 0f && breathableReserve < breathableReserveCapacity)
            {
                breathableReserve += airRecycleRate * dt;
                if (breathableReserve > breathableReserveCapacity)
                    breathableReserve = breathableReserveCapacity;
            }

            if (_trackedPlayerSurvival == null || !dryCompartment)
            {
                TrackAirReserveStateTransitions();
                return;
            }

            if (occupiedAirDrainRate > 0f)
            {
                breathableReserve -= occupiedAirDrainRate * dt;
                if (breathableReserve < 0f)
                    breathableReserve = 0f;
            }

            if (breathableReserve > 0f)
            {
                float refillScale = ResolveAirRefillScale();
                if (refillScale > 0f && oxygenRefillRate > 0f)
                    _trackedPlayerSurvival.RefillOxygen(oxygenRefillRate * refillScale * dt);
            }
            else if (staleAirSuitDrainRate > 0f)
            {
                _trackedPlayerSurvival.DrainOxygen(staleAirSuitDrainRate * dt);
            }

            TrackAirReserveStateTransitions();
        }

        private float ResolveAirRefillScale()
        {
            float airQuality = AirReserveNormalized;
            if (airQuality >= staleAirThreshold)
                return 1f;

            if (airQuality <= 0f || staleAirThreshold <= 0.01f)
                return staleAirMinRefillScale;

            return Mathf.Lerp(staleAirMinRefillScale, 1f, airQuality / staleAirThreshold);
        }

        private void TrackAirReserveStateTransitions()
        {
            bool airQualityLow = IsAirQualityLow;
            if (airQualityLow && !_airReserveWarningLatched)
            {
                _airReserveWarningLatched = true;
                RecordCascadeFailure("AIR SCRUBBERS SATURATED", BuildAirReserveSummary());
            }
            else if (!airQualityLow && _airReserveWarningLatched && AirReserveNormalized > staleAirThreshold + 0.15f)
            {
                _airReserveWarningLatched = false;
            }

            bool depleted = breathableReserve <= 0f;
            if (depleted && !_airReserveDepletedLatched)
            {
                _airReserveDepletedLatched = true;
                RecordCascadeFailure("BREATHABLE RESERVE EXHAUSTED", "Dry shelter air has collapsed into stale reserve. Occupants must evacuate or restore scrubber support.");
            }
            else if (!depleted && _airReserveDepletedLatched && AirReserveNormalized > 0.2f)
            {
                _airReserveDepletedLatched = false;
            }
        }

        private string BuildAirReserveSummary()
        {
            return string.Format(
                "Breathable reserve down to {0:0}% inside the dry shelter loop. Scrubber support is no longer keeping pace with occupancy.",
                AirReserveNormalized * 100f);
        }

        private void ClearCascadeFailure()
        {
            failureMode = BaseModuleFailureMode.None;
            SyncSpatialRole();
        }

        private BaseModuleFailureMode ResolveCascadeFailureMode()
        {
            int hash = 17;
            string prefabId = _moduleMarker != null ? _moduleMarker.PrefabId : string.Empty;

            if (!string.IsNullOrEmpty(prefabId))
            {
                int length = prefabId.Length;
                for (int i = 0; i < length; i++)
                    hash = hash * 31 + prefabId[i];
            }

            Vector3 position = transform.position;
            hash = hash * 31 + Mathf.RoundToInt(position.x * 10f);
            hash = hash * 31 + Mathf.RoundToInt(position.y * 10f);
            hash = hash * 31 + Mathf.RoundToInt(position.z * 10f);

            int resolved = Mathf.Abs(hash % 3);
            if (!_hasPower && resolved == 2)
                resolved = 0;

            switch (resolved)
            {
                case 1:
                    return BaseModuleFailureMode.Fire;
                case 2:
                    return BaseModuleFailureMode.ShortCircuit;
                default:
                    return BaseModuleFailureMode.OxygenLeak;
            }
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
            if (failureMode == BaseModuleFailureMode.ShortCircuit ||
                failureMode == BaseModuleFailureMode.OxygenLeak ||
                isFlooded)
            {
                return FieldTargetRole.ServiceFlooded;
            }

            if (failureMode == BaseModuleFailureMode.Fire)
                return FieldTargetRole.ServiceDamaged;

            if (currentIntegrity < GetRepairIntegrityCap())
                return FieldTargetRole.ServiceDamaged;

            return FieldTargetRole.Generic;
        }

        private void SyncTrackedObjectsFloodState()
        {
            if (isFlooded == _wasFlooded)
                return;

            _wasFlooded = isFlooded;

            if (_trackedObjects.Count == 0)
                return;

            _keysToRemove.Clear();

            foreach (KeyValuePair<int, BuoyancyObject> kvp in _trackedObjects)
            {
                BuoyancyObject buoyancy = kvp.Value;

                if (buoyancy == null)
                {
                    _keysToRemove.Add(kvp.Key);
                    continue;
                }

                if (isFlooded)
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

            foreach (KeyValuePair<int, BuoyancyObject> kvp in _trackedObjects)
            {
                BuoyancyObject buoyancy = kvp.Value;

                if (buoyancy != null && !isFlooded)
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

            if (!isFlooded)
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
            if (airRecycleRate < 0f) airRecycleRate = 0f;
            if (occupiedAirDrainRate < 0f) occupiedAirDrainRate = 0f;
            if (staleAirSuitDrainRate < 0f) staleAirSuitDrainRate = 0f;
            if (drainDuration < 0.1f) drainDuration = 0.1f;
        }

        private void OnDrawGizmosSelected()
        {
            if (interiorTrigger != null)
            {
                Gizmos.color = isFlooded
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
