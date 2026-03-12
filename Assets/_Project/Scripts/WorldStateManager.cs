// ============================================================================
// HECTON-8 — WorldStateManager.cs
// Менеджер состояния игрового мира.
//
// Singleton, ISaveable (Priority 50).
//
// Отслеживает уничтоженные/собранные объекты (ResourceNode),
// чтобы они не появлялись заново после загрузки.
//
// АРХИТЕКТУРА:
//   • HashSet<string> для O(1) проверки IsNodeDepleted.
//   • При сохранении: HashSet → string[] (ConstructionDTO-стиль).
//   • При загрузке: string[] → HashSet + деактивация узлов в сцене.
//   • FindObjectsByType вызывается ОДИН РАЗ при загрузке (не per-frame).
//
// ZERO GC в рантайме:
//   • RegisterDepletedNode: HashSet.Add — amortized O(1), 0 GC (если capacity достаточна).
//   • IsNodeDepleted: HashSet.Contains — O(1), 0 GC.
//   • Per-frame нет никаких вызовов (нет ITickable).
//
// POOL SAFETY (v2.1):
//   ApplyToScene ИГНОРИРУЕТ объекты с PoolItemMarker.
//   Динамические ресурсы (из ObjectPoolManager) проверяют свой стейт
//   самостоятельно при спавне через ResourceNode.OnEnable →
//   WorldStateManager.IsNodeDepleted. ApplyToScene управляет только
//   статически размещёнными в сцене объектами.
//   Это предотвращает массовую активацию пустых болванок пула.
//
// ИНТЕГРАЦИЯ:
//   • ResourceNode вызывает RegisterDepletedNode(uniqueId) при разрушении.
//   • SaveManager вызывает PopulateSaveData / LoadFromSaveData.
//   • ResourceNode.OnEnable проверяет IsNodeDepleted для самодеактивации.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.Scavenging;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6000)]
    public sealed class WorldStateManager : MonoBehaviour, ISaveable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static WorldStateManager _instance;

        public static WorldStateManager Instance
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

        [Header("── Settings ──────────────────────────────────")]
        [Tooltip("Начальная ёмкость HashSet. Увеличь для больших миров.")]
        [SerializeField] private int initialCapacity = 128;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugDepletedCount;

        // ══════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Множество уникальных ID уничтоженных/собранных объектов.
        /// HashSet: O(1) Add, Contains, Remove. Pre-allocated.
        /// </summary>
        private HashSet<string> _depletedNodeIds;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество уничтоженных узлов.</summary>
        public int DepletedCount => _depletedNodeIds != null ? _depletedNodeIds.Count : 0;

        /// <summary>
        /// Проверяет, был ли узел с данным ID уже уничтожен/собран.
        /// O(1), Zero GC.
        ///
        /// Вызывается ResourceNode.OnEnable для самодеактивации
        /// при загрузке сцены / респавне.
        /// </summary>
        /// <param name="uniqueId">Уникальный ID узла.</param>
        /// <returns>true если узел уже уничтожен.</returns>
        public bool IsNodeDepleted(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return false;
            if (_depletedNodeIds == null) return false;

            return _depletedNodeIds.Contains(uniqueId);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — REGISTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Регистрирует узел как уничтоженный.
        /// Вызывается из ResourceNode.TakeDamage() при health ≤ 0.
        ///
        /// O(1) amortized. Дубликаты игнорируются (HashSet).
        /// </summary>
        /// <param name="uniqueId">Уникальный ID узла.</param>
        public void RegisterDepletedNode(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return;
            if (_depletedNodeIds == null) return;

            _depletedNodeIds.Add(uniqueId);

            UpdateDiagnostics();
        }

        /// <summary>
        /// Снимает пометку "уничтожен" с узла.
        /// Используется для респавна ресурсов (таймер, событие, читы).
        /// </summary>
        /// <param name="uniqueId">Уникальный ID узла.</param>
        public void UnregisterDepletedNode(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return;
            if (_depletedNodeIds == null) return;

            _depletedNodeIds.Remove(uniqueId);

            UpdateDiagnostics();
        }

        /// <summary>
        /// Очищает все записи. Все узлы снова считаются "живыми".
        /// Используется при New Game.
        /// </summary>
        public void ClearAll()
        {
            _depletedNodeIds?.Clear();

            UpdateDiagnostics();
        }

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

            // ── Pre-allocate HashSet ──
            _depletedNodeIds = new HashSet<string>(initialCapacity);
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
        //  ISaveable — SAVE / LOAD (Priority 50)
        // ══════════════════════════════════════════════════════════

        /// <summary>World state загружается после Inventory, до Construction.</summary>
        public int SavePriority => 50;
        public int LoadPriority => 50;

        /// <summary>
        /// Записывает HashSet уничтоженных узлов в WorldStateDTO.
        ///
        /// HashSet → string[] (pre-allocated через EnsureCapacity).
        /// Проверка на MaxNodes: если узлов больше — обрезаем с Warning.
        /// </summary>
        public void PopulateSaveData(SaveData data)
        {
            ref WorldStateDTO dto = ref data.worldState;
            dto.EnsureCapacity();

            int index = 0;

            foreach (string id in _depletedNodeIds)
            {
                if (index >= WorldStateDTO.MaxNodes)
                {
                    Debug.LogWarning(
                        $"[WorldStateManager] Max depleted nodes ({WorldStateDTO.MaxNodes}) reached. " +
                        $"Truncating: {_depletedNodeIds.Count - index} nodes not saved.");
                    break;
                }

                dto.depletedNodeIds[index] = id;
                index++;
            }

            dto.depletedCount = index;
        }

        /// <summary>
        /// Восстанавливает состояние мира из SaveData.
        ///
        /// Порядок:
        ///   1. Очистить текущий HashSet.
        ///   2. Загрузить ID из dto.
        ///   3. Найти ВСЕ ResourceNode в сцене (FindObjectsByType — один раз).
        ///   4. Деактивировать те, чей uniqueId есть в HashSet.
        ///
        /// FindObjectsByType вызывается ОДИН РАЗ при загрузке — допустимо.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            WorldStateDTO dto = data.worldState;

            // ── 1. Очистка ──
            _depletedNodeIds.Clear();

            // ── 2. Загрузка ID ──
            if (dto.depletedNodeIds != null && dto.depletedCount > 0)
            {
                int count = Mathf.Min(dto.depletedCount, dto.depletedNodeIds.Length);

                for (int i = 0; i < count; i++)
                {
                    string id = dto.depletedNodeIds[i];
                    if (!string.IsNullOrEmpty(id))
                        _depletedNodeIds.Add(id);
                }
            }

            // ── 3. Применение к сцене ──
            ApplyToScene();

            UpdateDiagnostics();

            Debug.Log(
                $"[WorldStateManager] Loaded {_depletedNodeIds.Count} depleted nodes.");
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC — APPLY TO SCENE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Находит все ResourceNode в сцене и деактивирует уничтоженные.
        ///
        /// Вызывается:
        ///   • LoadFromSaveData() — после загрузки сейва.
        ///   • Можно вызвать вручную после загрузки новой сцены
        ///     (если узлы спавнятся позже SaveManager.LoadGame).
        ///
        /// FindObjectsByType с includeInactive:
        ///   Ищет ВСЕ узлы, включая уже деактивированные,
        ///   чтобы корректно обработать повторные загрузки.
        ///
        /// POOL SAFETY:
        ///   Объекты с PoolItemMarker (принадлежащие ObjectPoolManager)
        ///   ИГНОРИРУЮТСЯ. Динамические ресурсы из пула проверяют свой
        ///   depleted-стейт самостоятельно при спавне через
        ///   ResourceNode.OnEnable → IsNodeDepleted.
        ///   Это предотвращает массовую ошибочную активацию
        ///   предсозданных болванок пула.
        ///
        /// ОДНОРАЗОВАЯ операция при загрузке. Не per-frame.
        /// </summary>
        public void ApplyToScene()
        {
            if (_depletedNodeIds.Count == 0) return;

            // FindObjectsByType — Unity 2022.3+
            // Flags: включаем неактивные объекты
            ResourceNode[] allNodes = FindObjectsByType<ResourceNode>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int deactivated = 0;

            for (int i = 0, len = allNodes.Length; i < len; i++)
            {
                ResourceNode node = allNodes[i];
                if (node == null) continue;

                // ИГНОРИРУЕМ объекты пула. Они динамические и сами
                // проверяют стейт при спавне через ResourceNode.OnEnable.
                // Без этой проверки ApplyToScene активирует все предсозданные
                // болванки ObjectPoolManager, вызывая массовый визуальный баг.
                if (node.TryGetComponent<ObjectPoolManager.PoolItemMarker>(out _))
                    continue;

                string nodeId = node.UniqueId;
                if (string.IsNullOrEmpty(nodeId)) continue;

                if (_depletedNodeIds.Contains(nodeId))
                {
                    if (node.gameObject.activeSelf)
                    {
                        node.gameObject.SetActive(false);
                        deactivated++;
                    }
                }
                else
                {
                    // Узел НЕ в списке уничтоженных — убедиться что активен
                    // (мог быть деактивирован предыдущей загрузкой)
                    if (!node.gameObject.activeSelf)
                    {
                        node.gameObject.SetActive(true);
                    }
                }
            }

            if (deactivated > 0)
            {
                Debug.Log(
                    $"[WorldStateManager] Deactivated {deactivated} depleted nodes in scene.");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugDepletedCount = _depletedNodeIds != null ? _depletedNodeIds.Count : 0;
        }
    }
}