// ============================================================================
// HECTON-8 — PowerNode.cs
// Компонент энергоподключения на каждом модуле базы.
//
// ОТВЕТСТВЕННОСТИ:
//   1. При спавне — чтение базового потребления из BuildableData.
//   2. При спавне — поиск соседних PowerNode (OverlapSphereNonAlloc).
//   3. Создание/вступление в PowerGrid (или объединение сетей).
//   4. Сбор всех IPowerComponent на своём GameObject.
//   5. При деспавне — выход из PowerGrid (с проверкой связности).
//   6. Реализация IPowerComponent для базового потребления модуля.
//
// БАЗОВОЕ ПОТРЕБЛЕНИЕ (Data-Driven):
//   PowerNode сам реализует IPowerComponent.
//   При OnSpawn() читает BuildableData через ModuleMarker:
//     BuildableData.powerRating → PowerRating (положит. или отрицат.)
//     BuildableData.powerPriority → PowerPriority
//
//   Это БАЗОВОЕ потребление модуля (стены, освещение, вентиляция).
//   Дополнительные потребители (Fabricator, LifeSupport) добавляют
//   свои IPowerComponent поверх базового.
//
// АРХИТЕКТУРА:
//   • IPoolable — корректная работа с ObjectPoolManager.
//   • IPowerComponent — базовое потребление из BuildableData.
//   • OverlapSphereNonAlloc — zero GC поиск соседей.
//   • _components — кэш всех IPowerComponent на этом объекте.
//   • _neighbors — кэш соседних PowerNode (прямые связи).
//
// ZERO GC:
//   • Static Collider[] буфер для OverlapSphere — одна аллокация.
//   • List<IPowerComponent> заполняется GetComponents — zero GC.
//   • List<PowerNode> _neighbors — pre-allocated.
//   • ReferenceEquals для проверки дубликатов — zero GC.
//
// НАСТРОЙКА ПРЕФАБА:
//   1. Повесить PowerNode на finalPrefab модуля базы.
//   2. Установить connectionRadius (чуть больше snap-сетки).
//   3. ModuleMarker должен быть настроен с BuildableData.
//   4. BuildableData должна содержать powerRating и powerPriority.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Power
{
    [DisallowMultipleComponent]
    public sealed class PowerNode : MonoBehaviour, IPoolable, IPowerComponent
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Connection ────────────────────────────────")]
        [Tooltip("Радиус поиска соседних модулей (метры). " +
                 "Должен быть чуть больше размера snap-сетки. " +
                 "Рекомендация: размер модуля × 1.1")]
        [SerializeField] private float connectionRadius = 5f;

        [Tooltip("Слои, на которых ищутся соседние PowerNode.")]
        [SerializeField] private LayerMask connectionMask = ~0;

        [Header("── Fallback (если нет ModuleMarker) ──────────")]
        [Tooltip("Базовое потребление если ModuleMarker отсутствует. " +
                 "Отрицательное = потребляет, положительное = генерирует.")]
        [SerializeField] private float fallbackPowerRating;

        [Tooltip("Приоритет отключения если ModuleMarker отсутствует.")]
        [Range(0, 100)]
        [SerializeField] private int fallbackPowerPriority = 50;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Сеть, к которой принадлежит этот узел.</summary>
        private PowerGrid _grid;

        /// <summary>
        /// Кэш всех IPowerComponent на этом GameObject.
        /// Включает сам PowerNode (он тоже IPowerComponent).
        /// Заполняется при OnSpawn через GetComponents.
        /// </summary>
        private List<IPowerComponent> _components;

        /// <summary>
        /// Соседние PowerNode (прямые физические связи).
        /// Используется для BFS при проверке связности.
        /// </summary>
        private List<PowerNode> _neighbors;

        /// <summary>Базовое потребление из BuildableData.</summary>
        private float _basePowerRating;

        /// <summary>Приоритет из BuildableData.</summary>
        private int _basePowerPriority;

        /// <summary>Текущее состояние питания.</summary>
        private bool _hasPower = true;

        /// <summary>
        /// Статический буфер для OverlapSphereNonAlloc.
        /// 32 коллайдера — достаточно для любого модуля.
        /// Shared: только один PowerNode спавнится за кадр.
        /// </summary>
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Сеть этого узла. null если не подключён.</summary>
        public PowerGrid Grid => _grid;

        /// <summary>
        /// Все IPowerComponent на этом модуле.
        /// Используется PowerGrid.UpdateBalance() для подсчёта.
        /// Read-only рекомендуется.
        /// </summary>
        public List<IPowerComponent> Components => _components;

        /// <summary>
        /// Прямые соседи (подключённые физически).
        /// Используется PowerGridManager.CheckAndSplitGrid() для BFS.
        /// </summary>
        public List<PowerNode> Neighbors => _neighbors;

        /// <summary>
        /// Устанавливает ссылку на сеть.
        /// Вызывается PowerGrid при Add/Remove/Merge.
        /// </summary>
        public void SetGrid(PowerGrid grid)
        {
            _grid = grid;
        }

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent — БАЗОВОЕ ПОТРЕБЛЕНИЕ МОДУЛЯ
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Базовое энергопотребление модуля (из BuildableData).
        ///
        /// Это потребление самого модуля (корпус, освещение, вентиляция).
        /// Дополнительные потребители (Fabricator и т.д.) имеют
        /// свои IPowerComponent с отдельным PowerRating.
        ///
        /// Примеры:
        ///   • Коридор: 0 (пассивный, только проводит энергию)
        ///   • Жилая комната: -30 (базовое освещение)
        ///   • Солнечная панель: +200 (генерация)
        ///   • Реактор: +500 (генерация)
        /// </summary>
        public float PowerRating => _basePowerRating;

        /// <summary>Приоритет отключения базового потребления.</summary>
        public int PowerPriority => _basePowerPriority;

        /// <summary>Текущее состояние питания (кэшированное).</summary>
        public bool HasPower => _hasPower;

        /// <summary>
        /// Уведомление об изменении питания.
        /// Для базового потребления (PowerNode) — просто кэшируем.
        /// Компоненты (Fabricator и т.д.) получают свои уведомления
        /// через свой IPowerComponent.OnPowerStatusChanged.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;

            // Будущее: отключение/включение базового освещения,
            // вентиляции, звуков модуля
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _components = new List<IPowerComponent>(4);
            _neighbors  = new List<PowerNode>(6);
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable — ЖИЗНЕННЫЙ ЦИКЛ ПУЛА
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается ObjectPoolManager после SetActive(true).
        ///
        /// Порядок:
        ///   1. Читаем BuildableData через ModuleMarker.
        ///   2. Собираем все IPowerComponent на объекте.
        ///   3. Ищем соседние PowerNode.
        ///   4. Подключаемся к сети (или создаём новую).
        /// </summary>
        public void OnSpawn()
        {
            _hasPower = true;

            // ── 1. Читаем данные из BuildableData ──
            ReadBuildableData();

            // ── 2. Собираем IPowerComponent ──
            _components.Clear();
            GetComponents(_components); // zero GC, fills list

            // ── 3. Ищем соседей и подключаемся к сети ──
            _neighbors.Clear();
            FindAndConnectNeighbors();
        }

        /// <summary>
        /// Вызывается ObjectPoolManager перед SetActive(false).
        ///
        /// Порядок:
        ///   1. Отключаемся от сети.
        ///   2. Убираем себя из списков соседей.
        ///   3. Проверяем связность оставшейся сети.
        ///   4. Очищаем кэши.
        /// </summary>
        public void OnDespawn()
        {
            DisconnectFromGrid();
            RemoveSelfFromNeighbors();

            _neighbors.Clear();
            _components.Clear();
            _grid = null;
            _hasPower = true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DATA-DRIVEN INITIALIZATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Читает powerRating и powerPriority из BuildableData
        /// через ModuleMarker на этом объекте.
        ///
        /// Если ModuleMarker отсутствует — используются fallback значения
        /// из Inspector.
        ///
        /// Примечание: Требует что ModuleMarker имеет публичное свойство
        /// Data типа BuildableData. Если его нет — добавьте:
        ///   public BuildableData Data => _buildableData;
        /// </summary>
        private void ReadBuildableData()
        {
            _basePowerRating   = fallbackPowerRating;
            _basePowerPriority = fallbackPowerPriority;

            if (TryGetComponent(out ModuleMarker marker))
            {
                BuildableData data = marker.Data;
                if (data != null)
                {
                    _basePowerRating   = data.powerRating;
                    _basePowerPriority = data.powerPriority;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — NEIGHBOR DISCOVERY & GRID CONNECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ищет соседние PowerNode через OverlapSphereNonAlloc.
        /// Подключается к существующей сети или создаёт новую.
        /// При обнаружении соседей из разных сетей — объединяет.
        ///
        /// ZERO GC: static Collider[] buffer, TryGetComponent,
        /// ReferenceEquals для проверки дубликатов.
        ///
        /// СЦЕНАРИЙ ОБЪЕДИНЕНИЯ:
        ///   Игрок ставит коридор между двумя независимыми комнатами.
        ///   Коридор находит соседей из GridA и GridB.
        ///   → MergeGrids(GridA, GridB) → одна общая сеть.
        /// </summary>
        private void FindAndConnectNeighbors()
        {
            int overlapCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                transform.position,
                connectionRadius,
                OverlapBuffer,
                connectionMask,
                QueryTriggerInteraction.Ignore);

            PowerGrid targetGrid = null;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null) continue;
                if (ReferenceEquals(col.gameObject, gameObject)) continue;

                if (!col.TryGetComponent(out PowerNode neighbor)) continue;
                if (ReferenceEquals(neighbor, this)) continue;

                // ── Регистрируем как соседа (двусторонняя связь) ──
                if (!ContainsRef(_neighbors, neighbor))
                    _neighbors.Add(neighbor);

                if (!ContainsRef(neighbor._neighbors, this))
                    neighbor._neighbors.Add(this);

                // ── Сетевая логика ──
                if (neighbor._grid != null)
                {
                    if (targetGrid == null)
                    {
                        // Первый найденный сосед с сетью → присоединяемся
                        targetGrid = neighbor._grid;
                    }
                    else if (!ReferenceEquals(targetGrid, neighbor._grid))
                    {
                        // Сосед из ДРУГОЙ сети → объединяем!
                        targetGrid = PowerGridManager.MergeGrids(
                            targetGrid, neighbor._grid);
                    }
                }
            }

            // ── Подключение к сети ──
            if (targetGrid != null)
            {
                // Присоединяемся к найденной сети
                targetGrid.AddNode(this);
                _grid = targetGrid;
            }
            else
            {
                // Нет соседей с сетью → создаём свою
                _grid = PowerGridManager.CreateGrid(this);
            }
        }

        /// <summary>
        /// Отключается от текущей сети.
        /// Проверяет связность оставшихся узлов.
        /// Если сеть распалась — разделяет.
        /// </summary>
        private void DisconnectFromGrid()
        {
            if (_grid == null) return;

            PowerGrid oldGrid = _grid;
            oldGrid.RemoveNode(this);

            // ── Проверка связности ──
            if (oldGrid.NodeCount > 1)
            {
                // Сеть могла распасться — проверяем BFS
                PowerGridManager.CheckAndSplitGrid(oldGrid);
            }
            else if (oldGrid.NodeCount == 0)
            {
                // Сеть пуста — удаляем
                PowerGridManager.DestroyGrid(oldGrid);
            }
            // Если NodeCount == 1 — сеть из одного узла, связна по определению

            _grid = null;
        }

        /// <summary>
        /// Убирает себя из списков соседей всех подключённых узлов.
        /// Вызывается при деспавне.
        /// </summary>
        private void RemoveSelfFromNeighbors()
        {
            int count = _neighbors.Count;
            for (int i = 0; i < count; i++)
            {
                PowerNode neighbor = _neighbors[i];
                if (neighbor == null) continue;

                RemoveRef(neighbor._neighbors, this);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — COLLECTION HELPERS (Zero GC)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверка наличия по ссылке. Zero GC.
        /// O(n) — но списки соседей обычно 1-6 элементов.
        /// </summary>
        private static bool ContainsRef<T>(List<T> list, T item) where T : class
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(list[i], item))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Удаление по ссылке. Обычный RemoveAt (не swap — сохраняем порядок).
        /// </summary>
        private static void RemoveRef<T>(List<T> list, T item) where T : class
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(list[i], item))
                {
                    list.RemoveAt(i);
                    return;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            if (connectionRadius < 0.5f) connectionRadius = 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            // ── Радиус подключения ──
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.12f);
            Gizmos.DrawWireSphere(transform.position, connectionRadius);

            if (!Application.isPlaying) return;

            // ── Связи с соседями ──
            if (_neighbors != null)
            {
                int count = _neighbors.Count;
                for (int i = 0; i < count; i++)
                {
                    PowerNode neighbor = _neighbors[i];
                    if (neighbor == null) continue;

                    // Цвет зависит от состояния питания
                    Gizmos.color = (_hasPower && neighbor._hasPower)
                        ? Color.green
                        : Color.red;

                    Gizmos.DrawLine(transform.position, neighbor.transform.position);
                }
            }

            // ── Информация о сети ──
            if (_grid != null)
            {
                string info = $"Grid #{_grid.Id}\n" +
                              $"Nodes: {_grid.NodeCount}\n" +
                              $"Gen: {_grid.TotalGeneration:F0}W\n" +
                              $"Con: {_grid.TotalConsumption:F0}W\n" +
                              $"Bal: {_grid.Balance:F0}W";

                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    info,
                    new GUIStyle
                    {
                        fontSize = 10,
                        normal =
                        {
                            textColor = _grid.HasPowerDeficit
                                ? Color.red
                                : Color.green
                        }
                    });
            }
        }
#endif
    }
}
