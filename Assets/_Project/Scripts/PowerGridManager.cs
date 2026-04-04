// ============================================================================
// HECTON-8 — PowerGridManager.cs
// Глобальный менеджер всех энергетических сетей.
//
// ОТВЕТСТВЕННОСТИ:
//   1. Реестр всех PowerGrid в игре.
//   2. Периодический пересчёт баланса (ISlowTickable).
//   3. Фабрика: создание, уничтожение, объединение, разделение сетей.
//   4. BFS для проверки связности при удалении модуля.
//
// АРХИТЕКТУРА:
//   • Singleton MonoBehaviour (DontDestroyOnLoad).
//   • ISlowTickable — UpdateBalance раз в ~0.5-1 секунду.
//   • Статические методы для операций над сетями.
//   • Вызывается из PowerNode при спавне/деспавне модулей.
//
// СЕТЕВАЯ ТОПОЛОГИЯ:
//   • Каждый PowerNode принадлежит ровно одной PowerGrid.
//   • При размещении соединительного модуля:
//     - Если соседи из разных сетей → MergeGrids (объединение).
//     - Если сосед в одной сети → AddNode (присоединение).
//     - Если нет соседей → CreateGrid (новая сеть).
//   • При удалении модуля:
//     - RemoveNode из сети.
//     - CheckAndSplitGrid: BFS проверяет связность.
//     - Если сеть распалась → выделение в новые сети.
//
// ZERO GC:
//   • List<PowerGrid> — pre-allocated.
//   • BFS: кэшированные static Queue + HashSet.
//   • Swap-remove для удаления пустых сетей.
//   • SlowTick: for-цикл, no LINQ, no foreach (для List).
//
// ПОТОКОБЕЗОПАСНОСТЬ: нет. Вызывать только из Main Thread.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Power
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5500)]
    public sealed class PowerGridManager : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static PowerGridManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            if (_allGrids != null)
                _allGrids.Clear();
        }

        public static PowerGridManager Instance
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
        [Tooltip("Начальная ёмкость списка сетей.")]
        [SerializeField] private int initialGridCapacity = 16;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int   _debugGridCount;
        [SerializeField] private int   _debugTotalNodes;
        [SerializeField] private float _debugTotalGeneration;
        [SerializeField] private float _debugTotalConsumption;
        [SerializeField] private int   _debugDeficitGrids;

        // ══════════════════════════════════════════════════════════
        //  STORAGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Все энергетические сети в игре.
        /// Pre-allocated List. Swap-remove для удаления.
        /// </summary>
        private static List<PowerGrid> _allGrids;

        // ══════════════════════════════════════════════════════════
        //  BFS CACHE — static, переиспользуется
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Кэшированная очередь для BFS проверки связности.
        /// Static — один экземпляр на весь проект.
        /// Clear() перед каждым использованием.
        /// </summary>
        private static readonly Queue<PowerNode> _bfsQueue =
            new Queue<PowerNode>(64);

        /// <summary>
        /// Кэшированный HashSet посещённых узлов для BFS.
        /// </summary>
        private static readonly HashSet<PowerNode> _bfsVisited =
            new HashSet<PowerNode>(64);

        /// <summary>
        /// Кэшированный список узлов для переноса при разделении сети.
        /// Предотвращает аллокацию List при каждом split.
        /// </summary>
        private static readonly List<PowerNode> _splitBuffer =
            new List<PowerNode>(32);

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

            // ── Initialize ──
            if (_allGrids == null)
                _allGrids = new List<PowerGrid>(initialGridCapacity);
        }

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((ISlowTickable)this);
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((ISlowTickable)this);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — PERIODIC BALANCE UPDATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается GameTickManager каждые ~0.5-1 секунду.
        ///
        /// Обновляет баланс каждой сети.
        /// Удаляет пустые сети (модули были деспавнены).
        ///
        /// ZERO GC: for-цикл по List с индексом, swap-remove.
        /// </summary>
        public void SlowTick()
        {
            if (_allGrids == null) return;

            int gridCount = _allGrids.Count;
            float totalGen  = 0f;
            float totalCon  = 0f;
            int totalNodes  = 0;
            int deficitCount = 0;

            for (int i = gridCount - 1; i >= 0; i--)
            {
                PowerGrid grid = _allGrids[i];

                // ── Удаление пустых сетей ──
                if (grid == null || grid.NodeCount == 0)
                {
                    SwapRemoveAt(i);
                    continue;
                }

                // ── Пересчёт баланса ──
                grid.UpdateBalance();

                totalGen   += grid.TotalGeneration;
                totalCon   += grid.TotalConsumption;
                totalNodes += grid.NodeCount;

                if (grid.HasPowerDeficit)
                    deficitCount++;
            }

            UpdateDiagnostics(totalGen, totalCon, totalNodes, deficitCount);
        }

        // ══════════════════════════════════════════════════════════
        //  STATIC API — GRID FACTORY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Создаёт новую энергосеть с одним узлом.
        /// Вызывается из PowerNode.OnSpawn когда нет соседних сетей.
        /// </summary>
        /// <param name="initialNode">Первый узел новой сети.</param>
        /// <returns>Созданная PowerGrid.</returns>
        public static PowerGrid CreateGrid(PowerNode initialNode)
        {
            EnsureStorage();

            PowerGrid grid = new PowerGrid();
            grid.AddNode(initialNode);
            _allGrids.Add(grid);

            return grid;
        }

        /// <summary>
        /// Удаляет пустую сеть из реестра.
        /// Безопасно при отсутствии сети (no-op).
        /// </summary>
        public static void DestroyGrid(PowerGrid grid)
        {
            if (grid == null || _allGrids == null) return;

            int count = _allGrids.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_allGrids[i], grid))
                {
                    SwapRemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Объединяет две сети в одну.
        ///
        /// Стратегия: бóльшая сеть поглощает меньшую.
        /// Это минимизирует количество операций SetGrid
        /// (меньше узлов перепривязываются).
        ///
        /// Поглощённая сеть удаляется из реестра.
        ///
        /// Вызывается из PowerNode.OnSpawn когда найдены соседи
        /// из разных сетей (соединительный коридор).
        /// </summary>
        /// <param name="a">Первая сеть.</param>
        /// <param name="b">Вторая сеть.</param>
        /// <returns>Результирующая (объединённая) сеть.</returns>
        public static PowerGrid MergeGrids(PowerGrid a, PowerGrid b)
        {
            if (a == null) return b;
            if (b == null) return a;
            if (ReferenceEquals(a, b)) return a;

            // Большая поглощает меньшую
            PowerGrid larger, smaller;
            if (a.NodeCount >= b.NodeCount)
            {
                larger  = a;
                smaller = b;
            }
            else
            {
                larger  = b;
                smaller = a;
            }

            larger.AbsorbAll(smaller);
            DestroyGrid(smaller);

            return larger;
        }

        /// <summary>
        /// Проверяет связность сети после удаления узла.
        /// Если сеть распалась на несвязные компоненты —
        /// разделяет на отдельные сети.
        ///
        /// Алгоритм: BFS от произвольного узла.
        /// Если не все узлы достигнуты → недостигнутые выделяются
        /// в новую сеть. Рекурсивно проверяется новая сеть
        /// (могла распасться на 3+ компонента).
        ///
        /// Вызывается из PowerNode.OnDespawn после RemoveNode.
        ///
        /// ZERO GC:
        ///   • _bfsQueue, _bfsVisited, _splitBuffer — static кэши.
        ///   • Clear() не аллоцирует.
        ///   • Единственная аллокация: new PowerGrid (при реальном split).
        /// </summary>
        public static void CheckAndSplitGrid(PowerGrid grid)
        {
            if (grid == null) return;
            if (grid.NodeCount <= 1) return; // 0 или 1 узел — всегда связная

            _bfsQueue.Clear();
            _bfsVisited.Clear();

            // ── Берём стартовый узел ──
            PowerNode startNode = null;
            foreach (PowerNode n in grid.Nodes)
            {
                if (n != null)
                {
                    startNode = n;
                    break;
                }
            }

            if (startNode == null) return;

            // ── BFS ──
            _bfsQueue.Enqueue(startNode);
            _bfsVisited.Add(startNode);

            while (_bfsQueue.Count > 0)
            {
                PowerNode current = _bfsQueue.Dequeue();
                List<PowerNode> neighbors = current.Neighbors;

                if (neighbors == null) continue;

                int neighborCount = neighbors.Count;
                for (int i = 0; i < neighborCount; i++)
                {
                    PowerNode neighbor = neighbors[i];

                    if (neighbor == null) continue;

                    // Сосед должен быть в ТОЙ ЖЕ сети
                    if (!grid.Nodes.Contains(neighbor)) continue;

                    // Уже посещён
                    if (_bfsVisited.Contains(neighbor)) continue;

                    _bfsVisited.Add(neighbor);
                    _bfsQueue.Enqueue(neighbor);
                }
            }

            // ── Все узлы достигнуты? ──
            if (_bfsVisited.Count == grid.NodeCount)
                return; // Сеть связна — ничего делать не надо

            // ══════════════════════════════════════════════════════
            //  СЕТЬ РАСПАЛАСЬ — выделяем недостигнутые в новую сеть
            // ══════════════════════════════════════════════════════

            _splitBuffer.Clear();

            // Собираем узлы для переноса
            // (нельзя модифицировать HashSet во время итерации)
            foreach (PowerNode node in grid.Nodes)
            {
                if (node != null && !_bfsVisited.Contains(node))
                    _splitBuffer.Add(node);
            }

            if (_splitBuffer.Count == 0) return;

            // ── Создаём новую сеть ──
            EnsureStorage();
            PowerGrid newGrid = new PowerGrid(_splitBuffer.Count);
            _allGrids.Add(newGrid);

            // ── Переносим узлы ──
            int moveCount = _splitBuffer.Count;
            for (int i = 0; i < moveCount; i++)
            {
                PowerNode node = _splitBuffer[i];
                grid.RemoveNode(node);
                newGrid.AddNode(node);
            }

            _splitBuffer.Clear();

            // ── Рекурсивная проверка новой сети ──
            // (могла распасться на 3+ компонента при удалении
            // "перекрёстного" узла)
            if (newGrid.NodeCount > 1)
            {
                CheckAndSplitGrid(newGrid);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество активных сетей.</summary>
        public int GridCount => _allGrids != null ? _allGrids.Count : 0;

        /// <summary>Общая генерация по всем сетям (Вт).</summary>
        public float TotalGeneration => _debugTotalGeneration;

        /// <summary>Общее потребление по всем сетям (Вт).</summary>
        public float TotalConsumption => _debugTotalConsumption;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Гарантирует инициализацию статического хранилища.
        /// Вызывается из static методов (могут быть вызваны до Awake).
        /// </summary>
        private static void EnsureStorage()
        {
            if (_allGrids == null)
                _allGrids = new List<PowerGrid>(16);
        }

        /// <summary>
        /// Swap-remove из списка сетей. O(1).
        /// Порядок сетей не важен.
        /// </summary>
        private static void SwapRemoveAt(int index)
        {
            int last = _allGrids.Count - 1;
            if (index < last)
                _allGrids[index] = _allGrids[last];
            _allGrids.RemoveAt(last);
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(float gen, float con, int nodes, int deficits)
        {
            _debugGridCount       = _allGrids != null ? _allGrids.Count : 0;
            _debugTotalNodes      = nodes;
            _debugTotalGeneration = gen;
            _debugTotalConsumption = con;
            _debugDeficitGrids    = deficits;
        }
    }
}
