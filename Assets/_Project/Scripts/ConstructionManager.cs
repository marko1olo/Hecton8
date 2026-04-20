// ============================================================================
// HECTON-8 — ConstructionManager.cs
// Менеджер построенных модулей базы.
//
// Singleton, ISaveable (Priority 90 — последний при загрузке).
//
// Ведёт реестр всех построенных модулей. При сохранении записывает
// ID + трансформ + динамическое состояние (integrity, isFlooded)
// каждого модуля. При загрузке — удаляет старые через пул и
// спавнит новые из сейва с восстановлением состояния.
//
// ZERO GC в рантайме:
//   • Register/Unregister: O(1) проверка, no LINQ.
//   • List<GameObject> pre-allocated с запасом.
//   • Swap-remove для O(1) удаления.
//   • PopulateSaveData: for-циклы, TryGetComponent.
//
// ИНТЕГРАЦИЯ:
//   • PlayerBuilder вызывает RegisterModule() после размещения.
//   • ClearAllModules() вызывается в LoadFromSaveData перед респавном.
//   • ObjectPoolManager для Spawn/Despawn модулей.
//   • BaseModule: integrity и isFlooded сохраняются/восстанавливаются здесь.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class ConstructionManager : MonoBehaviour, ISaveable, ISlowTickable
    {
        private const float SlowTickDeltaTime = 0.5f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static ConstructionManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static ConstructionManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Catalog ───────────────────────────────────")]
        [Tooltip("Каталог всех строительных модулей. " +
                 "Нужен для поиска префабов по ID при загрузке.")]
        [SerializeField] private ModuleCatalog catalog;

        [Header("── Settings ──────────────────────────────────")]
        [Tooltip("Начальная ёмкость списка модулей. " +
                 "Увеличь для больших баз.")]
        [SerializeField] private int initialCapacity = 64;

        [Header("Ambient Accidents")]
        [Tooltip("Разрешает редкие сервисные аварии на уже размещённых модулях базы.")]
        [SerializeField] private bool enableAmbientAccidents = true;
        [Tooltip("Интервал между cold-path проверками на случайную сервисную аварию.")]
        [SerializeField] private float ambientAccidentCheckInterval = 90f;
        [Tooltip("Базовый шанс аварии на одну cold-path проверку. Финальный шанс умножается на risk score кандидата.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentBaseChance = 0.25f;
        [Tooltip("Минимальный risk score, при котором модуль считается аварийным кандидатом.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentMinRisk = 0.2f;
        [Tooltip("Порог integrity, ниже которого модуль считается изношенным для accident scheduler.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentIntegrityThreshold = 0.8f;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugModuleCount;
        [Tooltip("Runtime timer until the next ambient accident evaluation.")]
        [SerializeField] private float _debugAmbientAccidentTimer;

        // ══════════════════════════════════════════════════════════
        //  REGISTRY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Реестр всех построенных модулей.
        /// Pre-allocated. Swap-remove для O(1) удаления.
        /// </summary>
        private List<GameObject> _spawnedModules;
        private bool _tickRegistered;
        private float _ambientAccidentTimer;
        private int _ambientAccidentCursor;

        // ══════════════════════════════════════════════════════════
        //  CONSTANTS — DEFAULT MODULE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Дефолтная целостность для модулей без BaseModule (опоры и т.п.)
        /// и для миграции старых сейвов (v1 → v2).
        /// </summary>
        private const float DefaultIntegrity = 100f;

        /// <summary>Дефолтное состояние затопления.</summary>
        private const bool  DefaultIsFlooded = false;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество построенных модулей.</summary>
        public int ModuleCount => _spawnedModules != null ? _spawnedModules.Count : 0;

        /// <summary>Read-only доступ к списку модулей (для UI, minimap).</summary>
        public IReadOnlyList<GameObject> SpawnedModules => _spawnedModules;

        /// <summary>Read-only доступ к каталогу модулей для build tools/UI.</summary>
        public ModuleCatalog Catalog => catalog;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton ──
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // ── Pre-allocate ──
            _spawnedModules = new List<GameObject>(initialCapacity);
            _ambientAccidentTimer = 0f;
        }

        private void OnEnable()
        {
            TryRegister();
            SaveManager.Instance?.Register(this);
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            SaveManager.Instance?.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (_instance == this)
                _instance = null;
        }

        public void SlowTick()
        {
            if (!enableAmbientAccidents || ambientAccidentCheckInterval <= 0f)
                return;

            _ambientAccidentTimer += SlowTickDeltaTime;
            _debugAmbientAccidentTimer = _ambientAccidentTimer;

            if (_ambientAccidentTimer < ambientAccidentCheckInterval)
                return;

            _ambientAccidentTimer = 0f;
            _debugAmbientAccidentTimer = 0f;

            TryTriggerAmbientAccident();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — REGISTER / UNREGISTER
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Регистрирует построенный модуль в реестре.
        ///
        /// Вызывается:
        ///   • PlayerBuilder.TryPlaceModule() после успешного размещения
        ///   • LoadFromSaveData() при загрузке
        ///
        /// Автоматически добавляет ModuleMarker, если отсутствует.
        /// Дубликаты игнорируются (ReferenceEquals проверка).
        /// </summary>
        /// <param name="module">GameObject финального модуля.</param>
        public void RegisterModule(GameObject module)
        {
            if (module == null) return;

            // ── Проверка дубликатов ──
            if (ContainsRef(module)) return;

            // ── Добавляем в реестр ──
            _spawnedModules.Add(module);

            UpdateDiagnostics();
        }

        /// <summary>
        /// Регистрирует модуль с привязкой к BuildableData.
        /// Автоматически настраивает ModuleMarker.
        ///
        /// Предпочтительный метод: гарантирует наличие маркера.
        /// </summary>
        /// <param name="module">GameObject финального модуля.</param>
        /// <param name="data">BuildableData для привязки.</param>
        public void RegisterModule(GameObject module, BuildableData data)
        {
            if (module == null) return;

            // ── Гарантируем наличие ModuleMarker ──
            if (!module.TryGetComponent(out ModuleMarker marker))
            {
                marker = module.AddComponent<ModuleMarker>();
            }

            // ── Инициализируем маркер, если data предоставлена ──
            if (data != null)
                marker.Initialize(data);

            RegisterModule(module);
        }

        /// <summary>
        /// Удаляет модуль из реестра. НЕ деспавнит его.
        /// Используй для деконструкции: Unregister + Pool.Despawn.
        ///
        /// Swap-remove: O(1).
        /// </summary>
        public void UnregisterModule(GameObject module)
        {
            if (module == null) return;

            SwapRemove(module);

            UpdateDiagnostics();
        }

        /// <summary>
        /// Удаляет из реестра И деспавнит через пул.
        /// Используется при деконструкции модулей.
        /// </summary>
        public void DestroyModule(GameObject module)
        {
            if (module == null) return;

            UnregisterModule(module);

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
                pool.Despawn(module);
            else
                Destroy(module);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CLEAR ALL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Деспавнит ВСЕ модули через пул и очищает реестр.
        ///
        /// Вызывается:
        ///   • LoadFromSaveData() перед респавном из сейва
        ///   • New Game (если нужно начать с чистого мира)
        ///
        /// Итерация обратным циклом: безопасно при Despawn,
        /// который может вызвать OnDisable на модулях.
        /// </summary>
        public void ClearAllModules()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;

            // ── Обратный цикл: безопасно при модификации списка ──
            for (int i = _spawnedModules.Count - 1; i >= 0; i--)
            {
                GameObject module = _spawnedModules[i];

                if (module == null) continue; // уже уничтожен

                if (pool != null)
                    pool.Despawn(module);
                else
                    Destroy(module);
            }

            _spawnedModules.Clear();

            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable — SAVE / LOAD (Priority 90)
        // ══════════════════════════════════════════════════════════

        /// <summary>Construction загружается ПОСЛЕДНЕЙ (зависит от мира).</summary>
        public int SavePriority => 90;
        public int LoadPriority => 90;

        /// <summary>
        /// Записывает все построенные модули в ConstructionDTO.
        ///
        /// Для каждого модуля:
        ///   1. Получает ModuleMarker → PrefabId
        ///   2. Читает transform.position и rotation
        ///   3. Читает BaseModule.CurrentIntegrity и IsFlooded (если есть)
        ///   4. Записывает в dto.modules[]
        ///
        /// Модули без ModuleMarker — пропускаются с Warning.
        /// Модули без BaseModule — записываются с дефолтными значениями
        /// (100% HP, не затоплен). Это корректно для пассивных модулей (опоры).
        /// </summary>
        public void PopulateSaveData(SaveData data)
        {
            ref ConstructionDTO dto = ref data.construction;
            dto.EnsureCapacity();

            int moduleIndex = 0;
            int count = _spawnedModules.Count;

            for (int i = 0; i < count; i++)
            {
                GameObject module = _spawnedModules[i];

                // ── Guard: destroyed reference ──
                if (module == null) continue;

                // ── Guard: missing marker ──
                if (!module.TryGetComponent(out ModuleMarker marker))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{module.name}' has no ModuleMarker. " +
                        "Skipping save for this module.");
                    continue;
                }

                // ── Guard: empty ID ──
                string prefabId = marker.PrefabId;
                if (string.IsNullOrEmpty(prefabId))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{module.name}' has empty PrefabId. " +
                        "Skipping.");
                    continue;
                }

                // ── Guard: capacity ──
                if (moduleIndex >= ConstructionDTO.MaxModules)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Max modules ({ConstructionDTO.MaxModules}) reached. " +
                        $"Truncating save: {count - moduleIndex} modules not saved.");
                    break;
                }

                // ── Serialize transform ──
                Transform t = module.transform;
                ModuleDTO moduleDto = new ModuleDTO();
                moduleDto.prefabId = prefabId;
                moduleDto.SetPosition(t.position);
                moduleDto.SetRotation(t.rotation);

                // ── Serialize dynamic state ──
                // Пассивные модули (опоры, декор) не имеют BaseModule.
                // Для них пишем дефолтные значения — при загрузке они корректны.
                if (module.TryGetComponent(out BaseModule baseModule))
                {
                    moduleDto.integrity = baseModule.CurrentIntegrity;
                    moduleDto.repairIntegrityCap = baseModule.MaxRecoverableIntegrity;
                    moduleDto.airReserveNormalized = baseModule.AirReserveNormalized;
                    moduleDto.isFlooded = baseModule.IsFlooded;
                    moduleDto.failureMode = (byte)baseModule.CurrentFailureMode;
                }
                else
                {
                    moduleDto.integrity = DefaultIntegrity;
                    moduleDto.repairIntegrityCap = DefaultIntegrity;
                    moduleDto.airReserveNormalized = 1f;
                    moduleDto.isFlooded = DefaultIsFlooded;
                    moduleDto.failureMode = (byte)BaseModuleFailureMode.None;
                }

                dto.modules[moduleIndex] = moduleDto;
                moduleIndex++;
            }

            dto.moduleCount = moduleIndex;
        }

        /// <summary>
        /// Восстанавливает построенные модули из ConstructionDTO.
        ///
        /// Порядок:
        ///   1. ClearAllModules() — удалить текущую базу
        ///   2. Для каждого ModuleDTO:
        ///      a. Найти префаб через ModuleCatalog
        ///      b. Spawn через ObjectPoolManager
        ///      c. Восстановить динамическое состояние (integrity, isFlooded)
        ///         ДО первого SlowTick (синхронно, в том же кадре)
        ///      d. RegisterModule (с привязкой BuildableData)
        ///
        /// Миграция v1 → v2: если integrity == 0f (дефолт для float при
        /// десериализации старого сейва без этого поля), трактуем как 100%.
        ///
        /// При ошибках: модуль пропускается, игра не крашится.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            // ── Валидация ──
            if (catalog == null)
            {
                Debug.LogError(
                    "[ConstructionManager] ModuleCatalog not assigned! " +
                    "Cannot load construction data.");
                return;
            }

            if (catalog.HasLookupAmbiguity)
            {
                Debug.LogError(
                    "[ConstructionManager] ModuleCatalog has ambiguous ID aliases. " +
                    $"Construction load aborted: {catalog.LookupAmbiguitySummary}");
                return;
            }

            ConstructionDTO dto = data.construction;

            // ── 1. Удаляем текущую базу ──

            // ── Guard: пустые данные ──
            if (dto.modules == null || dto.moduleCount <= 0)
            {
                ClearAllModules();
                Debug.Log("[ConstructionManager] No construction data to load.");
                return;
            }

            // ── 2. Респавн модулей из сейва ──
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                Debug.LogError(
                    "[ConstructionManager] ObjectPoolManager unavailable. " +
                    "Construction load aborted before world teardown.");
                return;
            }

            ClearAllModules();
            int count = Mathf.Min(dto.moduleCount, dto.modules.Length);
            int loadedCount   = 0;
            int skippedCount  = 0;

            for (int i = 0; i < count; i++)
            {
                ModuleDTO moduleDto = dto.modules[i];

                // ── Поиск префаба ──
                if (string.IsNullOrEmpty(moduleDto.prefabId))
                {
                    skippedCount++;
                    continue;
                }

                BuildableData buildData = catalog.FindDataById(moduleDto.prefabId);
                if (buildData == null)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{moduleDto.prefabId}' " +
                        "not found in catalog. Skipping.");
                    skippedCount++;
                    continue;
                }

                GameObject prefab = buildData.finalPrefab;
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{moduleDto.prefabId}' " +
                        "has no finalPrefab. Skipping.");
                    skippedCount++;
                    continue;
                }

                // ── Валидация позиции ──
                Vector3    pos = moduleDto.GetPosition();
                Quaternion rot = moduleDto.GetRotation();

                if (float.IsNaN(pos.x) || float.IsInfinity(pos.x) ||
                    float.IsNaN(pos.y) || float.IsInfinity(pos.y) ||
                    float.IsNaN(pos.z) || float.IsInfinity(pos.z))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{moduleDto.prefabId}' " +
                        "has invalid position. Skipping.");
                    skippedCount++;
                    continue;
                }

                // Нормализация quaternion (защита от float-дрифта в сейве)
                if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f)
                    rot = Quaternion.identity;
                else
                    rot.Normalize();

                // ── Spawn ──
                GameObject module = pool.Spawn(prefab, pos, rot);

                if (module == null)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Failed to spawn '{moduleDto.prefabId}'.");
                    skippedCount++;
                    continue;
                }

                // ── Restore dynamic state ──
                // ВАЖНО: выполняется синхронно, ДО первого SlowTick.
                // BaseModule.OnEnable() регистрирует SlowTick, но первый
                // вызов произойдёт только в следующем интервале таймера.
                // К этому моменту состояние уже будет установлено.
                if (module.TryGetComponent(out BaseModule baseModule))
                {
                    // Миграция v1 → v2: integrity == 0f означает,
                    // что поле не существовало в старом сейве.
                    // Трактуем как «полное здоровье».
                    float loadedIntegrity = moduleDto.integrity;
                    if (loadedIntegrity <= 0f)
                        loadedIntegrity = DefaultIntegrity;

                    float loadedRepairCap = moduleDto.repairIntegrityCap;
                    if (loadedRepairCap <= 0f)
                        loadedRepairCap = baseModule.MaxIntegrity;

                    float loadedAirReserveNormalized = data.version >= 28
                        ? Mathf.Clamp01(moduleDto.airReserveNormalized)
                        : 1f;

                    baseModule.SetState(
                        loadedIntegrity,
                        moduleDto.isFlooded,
                        (BaseModuleFailureMode)moduleDto.failureMode,
                        loadedRepairCap,
                        loadedAirReserveNormalized);
                }

                // ── Register с привязкой к BuildableData ──
                RegisterModule(module, buildData);
                loadedCount++;
            }

            Debug.Log(
                $"[ConstructionManager] Loaded {loadedCount} modules" +
                (skippedCount > 0 ? $", skipped {skippedCount}." : "."));

            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — COLLECTION HELPERS (Zero GC)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет наличие модуля по ссылке. O(n), но вызывается
        /// только при Register (редко). Zero GC.
        /// </summary>
        private bool ContainsRef(GameObject module)
        {
            int count = _spawnedModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_spawnedModules[i], module))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Swap-remove: O(1) удаление без сдвига массива.
        /// Порядок модулей не гарантирован (допустимо для этой системы).
        /// </summary>
        private void SwapRemove(GameObject module)
        {
            int count = _spawnedModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_spawnedModules[i], module))
                {
                    int last = count - 1;
                    _spawnedModules[i] = _spawnedModules[last];
                    _spawnedModules.RemoveAt(last);
                    return;
                }
            }
        }

        /// <summary>
        /// Очищает null-ссылки из списка (защита от Destroy извне).
        /// Вызывается перед Save для гарантии целостности.
        /// </summary>
        private void PurgeNullEntries()
        {
            for (int i = _spawnedModules.Count - 1; i >= 0; i--)
            {
                if (_spawnedModules[i] == null)
                {
                    int last = _spawnedModules.Count - 1;
                    _spawnedModules[i] = _spawnedModules[last];
                    _spawnedModules.RemoveAt(last);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

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

        private void TryTriggerAmbientAccident()
        {
            PurgeNullEntries();

            int count = _spawnedModules.Count;
            if (count <= 0)
                return;

            BaseModule candidate = null;
            float bestRisk = ambientAccidentMinRisk;
            int startIndex = _ambientAccidentCursor % count;

            for (int offset = 0; offset < count; offset++)
            {
                int index = (startIndex + offset) % count;
                GameObject moduleObject = _spawnedModules[index];
                if (moduleObject == null || !moduleObject.TryGetComponent(out BaseModule module))
                    continue;

                if (!TryEvaluateAmbientAccidentRisk(module, out float risk))
                    continue;

                if (risk <= bestRisk)
                    continue;

                bestRisk = risk;
                candidate = module;
                _ambientAccidentCursor = index + 1;
            }

            if (candidate == null)
                return;

            float accidentChance = Mathf.Clamp01(ambientAccidentBaseChance * bestRisk);
            if (UnityEngine.Random.value > accidentChance)
                return;

            TriggerAmbientAccident(candidate, bestRisk);
        }

        private bool TryEvaluateAmbientAccidentRisk(BaseModule module, out float risk)
        {
            risk = 0f;

            if (module == null)
                return false;

            if (module.HasCascadeFailure || module.CurrentIntegrity <= 0f || module.MaxIntegrity <= 0f)
                return false;

            float integrity01 = module.CurrentIntegrity / module.MaxIntegrity;
            if (integrity01 >= 0.999f && module.HasPower && !module.IsFlooded)
                return false;

            risk = 1f - integrity01;

            if (integrity01 <= ambientAccidentIntegrityThreshold)
                risk += 0.25f;

            if (!module.HasPower)
                risk += 0.2f;

            if (module.IsFlooded)
                risk += 0.35f;

            return risk >= ambientAccidentMinRisk;
        }

        private static void TriggerAmbientAccident(BaseModule module, float risk)
        {
            if (module == null)
                return;

            string source = ResolveModuleSource(module);
            string summary = BuildAmbientAccidentSummary(module, risk);
            FieldOperationLogSystem.RecordOperation(source, "SERVICE ACCIDENT", summary, "WARN");

            module.ApplyDamage(module.CurrentIntegrity + 1f);
        }

        private static string ResolveModuleSource(BaseModule module)
        {
            if (module != null && module.TryGetComponent(out ModuleMarker marker) && marker.Data != null)
            {
                string moduleName = marker.Data.moduleName;
                if (!string.IsNullOrWhiteSpace(moduleName))
                    return moduleName;
            }

            return "BASE";
        }

        private static string BuildAmbientAccidentSummary(BaseModule module, float risk)
        {
            if (module == null)
                return "Neglected service hardware destabilized and rolled into a cascade failure.";

            float integrity01 = module.MaxIntegrity > 0f
                ? module.CurrentIntegrity / module.MaxIntegrity
                : 0f;

            string condition;
            if (module.IsFlooded)
                condition = "Residual flooding was left unresolved.";
            else if (!module.HasPower)
                condition = "Power loss left pumps and service recovery offline.";
            else
                condition = "Hull fatigue crossed the unattended maintenance margin.";

            return $"Integrity {integrity01:0%}. {condition} Risk {Mathf.Clamp01(risk):0%} converted into a live compartment incident.";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugModuleCount = _spawnedModules.Count;
        }
    }
}
