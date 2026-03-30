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
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay
{
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
        /// Коэффициент возврата ресурсов при деконструкции.
        /// 0.8 = 80% ресурсов возвращается.
        /// </summary>
        private const float REFUND_RATIO = 0.8f;

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
            set => currentIntegrity = Mathf.Clamp(value, 0f, maxIntegrity);
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

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Базовое энергопотребление модуля.
        /// Источник: BuildableData.powerRating → fallback.
        /// </summary>
        public float PowerRating => _basePowerRating;

        public int PowerPriority => powerPriority;

        public bool HasPower => _hasPower;

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

            SetLightsEnabled(hasPower);

            if (!hasPower)
            {
                StopDrain();
            }
            else
            {
                TryStartDrain();
            }
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

            currentIntegrity = Mathf.Clamp(currentIntegrity, 0f, maxIntegrity);
            _wasFlooded = isFlooded;
            _isDeconstructing = false;

            RefreshVisualStateImmediate();
            TryStartDrain();
        }

        public void OnDespawn()
        {
            StopDrain();
            SetLeakActive(false);
            SetFloodedVisual(false);
            SetLightsEnabled(true);

            _isDeconstructing = false;
            _trackedPlayerSurvival = null;

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
            // ── Life Support: O2 refill ──
            // Conditions: player inside + powered + not flooded.
            // Runs BEFORE power check so we can skip everything else
            // if power is off, but still need the player-inside check
            // to be evaluated every tick.
            if (_trackedPlayerSurvival != null && _hasPower && !isFlooded)
            {
                _trackedPlayerSurvival.RefillOxygen(oxygenRefillRate * SLOW_TICK_DT);
            }
            if (!_hasPower)
                return;

            if (passiveRecoveryRate > 0f &&
                currentIntegrity > 0f &&
                currentIntegrity < maxIntegrity)
            {
                Repair(passiveRecoveryRate * SLOW_TICK_DT);
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

            _wasFlooded = isFlooded;
        }

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((ISlowTickable)this);
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((ISlowTickable)this);

            ReleaseAllTrackedObjects();
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
            if (other.CompareTag("Player") && _trackedPlayerSurvival == null)
            {
                other.TryGetComponent(out _trackedPlayerSurvival);
            }

            // ── Interior Zone: BuoyancyObject tracking ──
            if (!other.TryGetComponent(out BuoyancyObject buoyancy))
                return;

            int key = other.GetInstanceID();

            if (_trackedObjects.ContainsKey(key))
                return;

            _trackedObjects[key] = buoyancy;
            UpdateTrackedDiagnostics();

            if (!isFlooded)
            {
                buoyancy.EnterDryZone();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;

            // ── Life Support: detect player exit ──
            if (other.CompareTag("Player"))
            {
                _trackedPlayerSurvival = null;
            }
            // v3.0: Notify HUD of module exit  | is this fragment located right? analyze
            if (other.CompareTag("Player"))
            {
                ModuleStatusEvents.NotifyExit(this);
            }
            // ── Interior Zone: BuoyancyObject tracking ──
            int key = other.GetInstanceID();

            if (_trackedObjects.TryGetValue(key, out BuoyancyObject buoyancy))
            {
                _trackedObjects.Remove(key);
                UpdateTrackedDiagnostics();

                if (buoyancy != null && !isFlooded)
                {
                    buoyancy.ExitDryZone();
                }
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
                Breach();
            }
            else
            {
                SetLeakActive(true);
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
            if (currentIntegrity >= maxIntegrity && !isFlooded) return;

            currentIntegrity += amount;
            if (currentIntegrity > maxIntegrity)
                currentIntegrity = maxIntegrity;

            if (currentIntegrity >= maxIntegrity)
            {
                currentIntegrity = maxIntegrity;
                SetLeakActive(false);
                TryStartDrain();
            }
            else
            {
                SetLeakActive(true);
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
            PlaySpatialSfx(floodClip);
        }

        /// <summary>
        /// Принудительное завершение осушения. Сбрасывает drain state и визуал.
        /// </summary>
        public void ForceDrainComplete()
        {
            isFlooded = false;
            StopDrain();
            SetFloodedVisual(false);
            SyncTrackedObjectsFloodState();
        }

        /// <summary>
        /// Полный сброс визуального состояния модуля по текущим данным.
        /// Вызывается ConstructionManager после загрузки сохранения.
        /// </summary>
        public void RefreshAfterLoad()
        {
            currentIntegrity = Mathf.Clamp(currentIntegrity, 0f, maxIntegrity);
            _wasFlooded = isFlooded;
            RefreshVisualStateImmediate();
            SyncTrackedObjectsFloodState();
            TryStartDrain();
        }

        /// <summary>
        /// Устанавливает состояние модуля при загрузке сохранения.
        /// Вызывается ConstructionManager.LoadFromSaveData().
        /// </summary>
        public void SetState(float integrity, bool flooded)
        {
            currentIntegrity = Mathf.Clamp(integrity, 0f, maxIntegrity);
            isFlooded = flooded;
            _wasFlooded = flooded;
            RefreshVisualStateImmediate();
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

        private void Breach()
        {
            isFlooded = true;
            _isDraining = false;
            _drainTimer = 0f;

            SetLeakActive(true);
            SetFloodedVisual(true);
            SyncTrackedObjectsFloodState();
            PlaySpatialSfx(floodClip);
        }

        private void TryStartDrain()
        {
            if (!_hasPower) return;
            if (!isFlooded) return;
            if (currentIntegrity < maxIntegrity) return;

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
            if (currentIntegrity < maxIntegrity && currentIntegrity > 0f)
                SetLeakActive(true);
            else if (currentIntegrity >= maxIntegrity)
                SetLeakActive(false);

            SetFloodedVisual(isFlooded);
            SetLightsEnabled(_hasPower);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INTERIOR ZONE SYNC
        // ══════════════════════════════════════════════════════════

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
            if (currentIntegrity > maxIntegrity) currentIntegrity = maxIntegrity;
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
