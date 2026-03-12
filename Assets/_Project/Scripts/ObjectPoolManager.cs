// ============================================================================
// HECTON-8 — ObjectPoolManager.cs
// Глобальная система Object Pooling.
//
// Singleton, DontDestroyOnLoad. Один менеджер на всю игру.
// Пулирует любые префабы: лут, VFX, снаряды, врагов.
//
// ZERO GC В РАНТАЙМЕ:
//   • Dictionary<int, Pool> — аллокация при первом Warmup/Spawn.
//   • Queue<GameObject> — аллокация при Warmup. Dequeue/Enqueue — 0 GC.
//   • TryGetComponent — 0 GC.
//   • Никаких строковых аллокаций в горячих путях (Spawn/Despawn).
//   • Expand Warning использует #if UNITY_EDITOR или кэшированные строки.
//   • DespawnTimer — "полностью укомплектованный префаб":
//     компонент заранее добавлен на префаб, в рантайме только enabled/disabled.
//     Никаких AddComponent в горячих путях.
//
// ПОТОКОБЕЗОПАСНОСТЬ: не гарантирована. Вызывать только из Main Thread.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)] // Раньше большинства систем, после TickManager
    public sealed class ObjectPoolManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static ObjectPoolManager _instance;

        /// <summary>Глобальный доступ к пулу объектов.</summary>
        public static ObjectPoolManager Instance
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

        [Header("── Warmup Presets ────────────────────────────")]
        [Tooltip("Автоматический прогрев при старте сцены. " +
                "Добавь сюда часто используемые префабы.")]
        [SerializeField] private WarmupEntry[] warmupPresets;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugPoolCount;
        [SerializeField] private int _debugTotalPooled;
        [SerializeField] private int _debugTotalExpands;

        // ══════════════════════════════════════════════════════════
        //  WARMUP PRESET — сериализуемая структура для Inspector
        // ══════════════════════════════════════════════════════════

        [System.Serializable]
        private struct WarmupEntry
        {
            [Tooltip("Префаб для пулирования")]
            public GameObject prefab;

            [Tooltip("Количество предсозданных экземпляров")]
            public int count;
        }

        // ══════════════════════════════════════════════════════════
        //  STORAGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Основное хранилище пулов.
        /// Key = prefab.GetInstanceID() — стабильный, уникальный, int.
        /// Value = Pool (очередь + контейнер + метаданные).
        /// </summary>
        private Dictionary<int, Pool> _pools;

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

            // ── Инициализация словаря ──
            // Начальная ёмкость 32 — покрывает большинство игр.
            // При превышении — одна переаллокация (допустимо, это Awake).
            _pools = new Dictionary<int, Pool>(32);
        }

        private void Start()
        {
            // ── Автопрогрев из Inspector ──
            if (warmupPresets != null)
            {
                for (int i = 0; i < warmupPresets.Length; i++)
                {
                    WarmupEntry entry = warmupPresets[i];
                    if (entry.prefab != null && entry.count > 0)
                        Warmup(entry.prefab, entry.count);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — WARMUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Предсоздаёт count экземпляров префаба в пуле.
        /// Вызывай при загрузке сцены (Awake/Start/Loading Screen).
        ///
        /// Объекты создаются отключенными и складываются в
        /// дочерний контейнер для чистоты Hierarchy.
        ///
        /// Безопасно вызывать повторно — добавит ещё count объектов.
        /// </summary>
        /// <param name="prefab">Оригинальный префаб (не экземпляр!).</param>
        /// <param name="count">Сколько объектов предсоздать.</param>
        public void Warmup(GameObject prefab, int count)
        {
            if (prefab == null)
            {
                Debug.LogError("[ObjectPoolManager] Warmup: prefab is null!");
                return;
            }

            if (count <= 0) return;

            int id = prefab.GetInstanceID();

            if (!_pools.TryGetValue(id, out Pool pool))
            {
                pool = CreatePool(prefab, id);
            }

            for (int i = 0; i < count; i++)
            {
                GameObject obj = InstantiatePooled(prefab, id, pool);
                obj.SetActive(false);
                pool.available.Enqueue(obj);
            }

            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — SPAWN
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Извлекает объект из пула, устанавливает позицию/поворот,
        /// активирует и вызывает OnSpawn().
        ///
        /// Если пул пуст — автоматически расширяется (expand) с Warning.
        /// Если пул для этого префаба не существует — создаётся.
        ///
        /// ZERO GC: Queue.Dequeue + TryGetComponent — обе 0 B.
        /// Единственная возможная аллокация — Instantiate при расширении.
        /// </summary>
        /// <param name="prefab">Оригинальный префаб.</param>
        /// <param name="position">Мировая позиция.</param>
        /// <param name="rotation">Мировой поворот.</param>
        /// <returns>Активированный экземпляр из пула.</returns>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogError("[ObjectPoolManager] Spawn: prefab is null!");
                return null;
            }

            int id = prefab.GetInstanceID();

            // ── Получаем или создаём пул ──
            if (!_pools.TryGetValue(id, out Pool pool))
            {
                pool = CreatePool(prefab, id);
                WarnExpand(prefab, "Pool created on-demand. Call Warmup() at load time!");
            }

            // ── Расширяем, если пуст ──
            if (pool.available.Count == 0)
            {
                ExpandPool(pool, prefab, id);
            }

            // ── Извлекаем ──
            GameObject instance = pool.available.Dequeue();

            // ── Защита от уничтоженных объектов (edge case) ──
            // Объект мог быть уничтожен внешним кодом через Destroy().
            // Unity перегружает == null для destroyed объектов.
            if (instance == null)
            {
                // Рекурсивно пробуем следующий в очереди.
                // В худшем случае — создаст новый через ExpandPool.
                return Spawn(prefab, position, rotation);
            }

            // ── Отцепляем от контейнера пула ──
            instance.transform.SetParent(null, false);

            // ── Устанавливаем трансформ ──
            instance.transform.SetPositionAndRotation(position, rotation);

            // ── Активируем ──
            instance.SetActive(true);

            // ── Уведомляем IPoolable ──
            NotifySpawn(instance);

            return instance;
        }

        /// <summary>
        /// Перегрузка Spawn с дефолтным поворотом (identity).
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position)
        {
            return Spawn(prefab, position, Quaternion.identity);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — DESPAWN
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает экземпляр в пул.
        /// Вызывает OnDespawn(), деактивирует, перемещает в контейнер.
        ///
        /// ZERO GC: TryGetComponent + Queue.Enqueue — обе 0 B.
        /// </summary>
        /// <param name="instance">Экземпляр для возврата в пул.</param>
        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            // ── Получаем маркер для определения «родного» пула ──
            if (!instance.TryGetComponent(out PoolItemMarker marker))
            {
                // Объект не из пула — безопасно уничтожаем
                Debug.LogWarning(
                    $"[ObjectPoolManager] Despawn: '{instance.name}' has no PoolItemMarker. " +
                    "Destroying instead. Was it spawned via pool?");
                Destroy(instance);
                return;
            }

            int prefabId = marker.PrefabId;

            // ── Проверяем существование пула ──
            if (!_pools.TryGetValue(prefabId, out Pool pool))
            {
                // Пул был уничтожен (смена сцены?) — уничтожаем объект
                Debug.LogWarning(
                    $"[ObjectPoolManager] Despawn: Pool for '{instance.name}' not found. Destroying.");
                Destroy(instance);
                return;
            }

            // ── Уведомляем IPoolable ПЕРЕД деактивацией ──
            NotifyDespawn(instance);

            // ── Деактивируем ──
            instance.SetActive(false);

            // ── Возвращаем в контейнер пула (для чистоты Hierarchy) ──
            instance.transform.SetParent(pool.container, false);

            // ── Сбрасываем трансформ (опционально, но гигиенично) ──
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // ── Возвращаем в очередь ──
            pool.available.Enqueue(instance);
        }

        /// <summary>
        /// Отложенный Despawn через указанное время.
        /// Удобно для VFX, снарядов, временных эффектов.
        ///
        /// ZERO GC: TryGetComponent — 0 B. Никаких AddComponent в рантайме.
        /// Компонент DespawnTimer просто активируется (enabled = true).
        ///
        /// ВАЖНО: На префабе, который должен поддерживать отложенный деспавн,
        /// должен быть заранее добавлен компонент DespawnTimer.
        /// В рантайме он будет просто активироваться.
        /// Если компонент отсутствует — объект деспавнится немедленно с предупреждением.
        /// </summary>
        /// <param name="instance">Экземпляр для возврата.</param>
        /// <param name="delay">Задержка в секундах.</param>
        public void Despawn(GameObject instance, float delay)
        {
            if (instance == null) return;

            if (delay <= 0f)
            {
                Despawn(instance);
                return;
            }

            // ── Паттерн "полностью укомплектованного префаба" ──
            // Компонент DespawnTimer должен быть заранее добавлен на префаб.
            // Никаких AddComponent в рантайме — zero GC, zero CPU spike.
            if (instance.TryGetComponent(out DespawnTimer timer))
            {
                timer.StartTimer(delay);
            }
            else
            {
                Debug.LogWarning(
                    $"[ObjectPoolManager] Prefab '{instance.name}' is missing a DespawnTimer component. " +
                    "Despawning immediately. Add the component to the prefab to enable delayed despawn.");
                Despawn(instance);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество доступных объектов в пуле для данного префаба.</summary>
        public int GetAvailableCount(GameObject prefab)
        {
            if (prefab == null) return 0;
            int id = prefab.GetInstanceID();
            return _pools.TryGetValue(id, out Pool pool) ? pool.available.Count : 0;
        }

        /// <summary>Существует ли пул для данного префаба.</summary>
        public bool HasPool(GameObject prefab)
        {
            if (prefab == null) return false;
            return _pools.ContainsKey(prefab.GetInstanceID());
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CLEANUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Уничтожает все объекты в пуле для данного префаба.
        /// Вызывай при смене сцены, если объекты не нужны.
        /// </summary>
        public void ClearPool(GameObject prefab)
        {
            if (prefab == null) return;
            int id = prefab.GetInstanceID();

            if (!_pools.TryGetValue(id, out Pool pool)) return;

            while (pool.available.Count > 0)
            {
                GameObject obj = pool.available.Dequeue();
                if (obj != null)
                    Destroy(obj);
            }

            if (pool.container != null)
                Destroy(pool.container.gameObject);

            _pools.Remove(id);
            UpdateDiagnostics();
        }

        /// <summary>Уничтожает ВСЕ пулы. Используй при полном рестарте.</summary>
        public void ClearAllPools()
        {
            foreach (var kvp in _pools)
            {
                Pool pool = kvp.Value;

                while (pool.available.Count > 0)
                {
                    GameObject obj = pool.available.Dequeue();
                    if (obj != null)
                        Destroy(obj);
                }

                if (pool.container != null)
                    Destroy(pool.container.gameObject);
            }

            _pools.Clear();
            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — POOL MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Создаёт новый пул и контейнер в Hierarchy.
        /// Одна аллокация Dictionary entry + один GameObject контейнер.
        /// </summary>
        private Pool CreatePool(GameObject prefab, int prefabId)
        {
            // ── Контейнер для чистоты Hierarchy ──
            GameObject containerGO = new GameObject($"[Pool] {prefab.name}");
            containerGO.transform.SetParent(transform, false);

            Pool pool = new Pool
            {
                available = new Queue<GameObject>(32),
                container = containerGO.transform,
                prefab    = prefab,
                prefabId  = prefabId
            };

            _pools.Add(prefabId, pool);

            return pool;
        }

        /// <summary>
        /// Расширяет пул на один объект. Аллокация Instantiate неизбежна,
        /// но это fallback — при правильном Warmup никогда не вызывается.
        /// </summary>
        private void ExpandPool(Pool pool, GameObject prefab, int prefabId)
        {
            WarnExpand(prefab, "Pool exhausted, expanding by 1.");

#if UNITY_EDITOR
            _debugTotalExpands++;
#endif

            GameObject obj = InstantiatePooled(prefab, prefabId, pool);
            obj.SetActive(false);
            pool.available.Enqueue(obj);
        }

        /// <summary>
        /// Создаёт экземпляр префаба и настраивает маркер.
        /// Единственное место, где вызывается Instantiate.
        /// </summary>
        private GameObject InstantiatePooled(GameObject prefab, int prefabId, Pool pool)
        {
            GameObject obj = Instantiate(prefab, pool.container);
            obj.SetActive(false);

            // ── Добавляем маркер для обратной связи Despawn → Pool ──
            if (!obj.TryGetComponent(out PoolItemMarker marker))
            {
                marker = obj.AddComponent<PoolItemMarker>();
            }

            marker.Initialize(prefabId);

            return obj;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — IPoolable NOTIFICATIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Уведомляет все IPoolable-компоненты на объекте о спавне.
        /// TryGetComponent — zero GC. Для множественных IPoolable
        /// используем кэшированный список.
        /// </summary>
        private static readonly List<IPoolable> _poolableCache = new List<IPoolable>(8);

        private static void NotifySpawn(GameObject instance)
        {
            instance.GetComponents(_poolableCache); // fills list, zero GC
            int count = _poolableCache.Count;
            for (int i = 0; i < count; i++)
                _poolableCache[i].OnSpawn();
            _poolableCache.Clear();
        }

        private static void NotifyDespawn(GameObject instance)
        {
            instance.GetComponents(_poolableCache);
            int count = _poolableCache.Count;
            for (int i = 0; i < count; i++)
                _poolableCache[i].OnDespawn();
            _poolableCache.Clear();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugPoolCount = _pools.Count;
            int total = 0;
            foreach (var kvp in _pools)
                total += kvp.Value.available.Count;
            _debugTotalPooled = total;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void WarnExpand(GameObject prefab, string reason)
        {
            Debug.LogWarning(
                $"[ObjectPoolManager] '{prefab.name}': {reason} " +
                "Consider increasing Warmup count.");
        }

        // ══════════════════════════════════════════════════════════
        //  NESTED TYPES
        // ══════════════════════════════════════════════════════════

        // ──────────────────────────────────────────────────────────
        //  Pool — внутренняя структура данных пула
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Данные одного пула. Struct-like class для Dictionary value.
        /// Не MonoBehaviour — чистые данные.
        /// </summary>
        private sealed class Pool
        {
            /// <summary>Очередь доступных (отключенных) объектов. FIFO.</summary>
            public Queue<GameObject> available;

            /// <summary>Transform контейнера в Hierarchy (для чистоты).</summary>
            public Transform container;

            /// <summary>Оригинальный префаб (для расширения пула).</summary>
            public GameObject prefab;

            /// <summary>GetInstanceID() оригинального префаба.</summary>
            public int prefabId;
        }

        // ──────────────────────────────────────────────────────────
        //  PoolItemMarker — компонент-маркер на экземплярах
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Лёгкий компонент-маркер. Добавляется автоматически
        /// при первом Instantiate через пул.
        ///
        /// Единственная задача: хранить ID оригинального префаба,
        /// чтобы Despawn() мог вернуть объект в правильную очередь.
        ///
        /// Нет Update, нет аллокаций, нет логики — только int.
        /// </summary>
        [DisallowMultipleComponent]
        [AddComponentMenu("")] // Скрываем из меню Add Component
        public sealed class PoolItemMarker : MonoBehaviour
        {
            private int _prefabId;
            private bool _initialized;

            /// <summary>ID префаба-прародителя.</summary>
            public int PrefabId => _prefabId;

            /// <summary>
            /// Инициализация маркера. Вызывается один раз при создании.
            /// Повторные вызовы игнорируются (защита от переинициализации).
            /// </summary>
            public void Initialize(int prefabId)
            {
                if (_initialized) return;
                _prefabId    = prefabId;
                _initialized = true;
            }
        }

        // ──────────────────────────────────────────────────────────
        //  DespawnTimer — компонент отложенного Despawn
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Таймер для отложенного возврата в пул.
        /// Использует ручной таймер вместо корутины (zero GC).
        ///
        /// ВАЖНО: На префабе, который должен поддерживать отложенный деспавн,
        /// должен быть заранее добавлен компонент DespawnTimer.
        /// В рантайме он будет просто активироваться (enabled = true).
        /// Никаких AddComponent в горячих путях — zero GC, zero CPU spike.
        ///
        /// Рабочий процесс:
        /// 1. Добавь DespawnTimer на префаб в редакторе.
        /// 2. Сними галочку enabled (или оставь — OnDisable сбросит при Despawn).
        /// 3. При вызове Despawn(instance, delay) компонент активируется автоматически.
        /// 4. По истечении таймера — объект возвращается в пул, компонент деактивируется.
        /// </summary>
        [DisallowMultipleComponent]
        [AddComponentMenu("")]
        public sealed class DespawnTimer : MonoBehaviour
        {
            private float _timer;
            private bool  _active;

            /// <summary>
            /// Запускает обратный отсчёт.
            /// Активирует компонент (enabled = true), чтобы Update() вызывался.
            /// </summary>
            public void StartTimer(float delay)
            {
                _timer   = delay;
                _active  = true;
                enabled  = true;
            }

            private void Update()
            {
                if (!_active) return;

                _timer -= Time.deltaTime;

                if (_timer <= 0f)
                {
                    _active = false;

                    // ── Безопасный паттерн доступа к синглтону ──
                    // Кэшируем ссылку в локальную переменную, чтобы избежать
                    // NullReferenceException, если менеджер уничтожен при смене сцены.
                    ObjectPoolManager pool = ObjectPoolManager.Instance;
                    if (pool != null)
                    {
                        pool.Despawn(gameObject);
                    }

                    // ── Деактивируем компонент до следующего использования ──
                    // Это останавливает вызовы Update() и экономит CPU.
                    // Располагаем после Despawn, т.к. Despawn деактивирует GameObject,
                    // что вызовет OnDisable, но enabled = false здесь гарантирует
                    // чистое состояние даже если Despawn не вызвался (pool == null).
                    enabled = false;
                }
            }

            private void OnDisable()
            {
                // Сброс при деактивации (Despawn, внешнее отключение, смена сцены).
                // Гарантирует чистое состояние для следующего цикла Spawn → Despawn.
                _active = false;
            }
        }
    }
}