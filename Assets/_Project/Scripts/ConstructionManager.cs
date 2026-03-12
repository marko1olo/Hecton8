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
    public sealed class ConstructionManager : MonoBehaviour, ISaveable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static ConstructionManager _instance;

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

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugModuleCount;

        // ══════════════════════════════════════════════════════════
        //  REGISTRY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Реестр всех построенных модулей.
        /// Pre-allocated. Swap-remove для O(1) удаления.
        /// </summary>
        private List<GameObject> _spawnedModules;

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
        }

        private void OnEnable()
        {
            SaveManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            SaveManager.Instance?.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
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
                    moduleDto.isFlooded = baseModule.IsFlooded;
                }
                else
                {
                    moduleDto.integrity = DefaultIntegrity;
                    moduleDto.isFlooded = DefaultIsFlooded;
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

            ConstructionDTO dto = data.construction;

            // ── 1. Удаляем текущую базу ──
            ClearAllModules();

            // ── Guard: пустые данные ──
            if (dto.modules == null || dto.moduleCount <= 0)
            {
                Debug.Log("[ConstructionManager] No construction data to load.");
                return;
            }

            // ── 2. Респавн модулей из сейва ──
            ObjectPoolManager pool = ObjectPoolManager.Instance;
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
                GameObject module;
                if (pool != null)
                    module = pool.Spawn(prefab, pos, rot);
                else
                    module = Instantiate(prefab, pos, rot);

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

                    baseModule.SetState(loadedIntegrity, moduleDto.isFlooded);
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugModuleCount = _spawnedModules.Count;
        }
    }
}