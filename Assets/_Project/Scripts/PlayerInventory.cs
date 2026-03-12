// ============================================================================
// HECTON-8 — PlayerInventory.cs
// MonoBehaviour-обёртка над InventoryGrid.
// Вешается на корневой GameObject игрока.
//
// Ответственности:
//   1. Создаёт InventoryGrid при инициализации.
//   2. Слушает InteractionEvents.OnItemCollected и размещает предметы.
//   3. Отслеживает общий вес и синхронизирует с HectonSurvivalSystem.
//   4. ISaveable: сохраняет/загружает содержимое сетки инвентаря.
//
// НЕ содержит UI-логику — только данные и бизнес-правила.
// ============================================================================

namespace Hecton8.Inventory
{
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.SaveSystem;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour, ISaveable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Grid Settings ─────────────────────────────")]
        [Tooltip("Количество колонок сетки инвентаря")]
        [SerializeField] private int columns = 8;
        [Tooltip("Количество строк сетки инвентаря")]
        [SerializeField] private int rows    = 6;

        [Header("── References ────────────────────────────────")]
        [Tooltip("Ссылка на систему выживания для синхронизации веса (опционально)")]
        [SerializeField] private HectonSurvivalSystem survival;

        [Header("── Save System ───────────────────────────────")]
        [Tooltip("Каталог всех предметов для поиска по ID при загрузке")]
        [SerializeField] private ItemCatalog itemCatalog;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private InventoryGrid _grid;

        // ══════════════════════════════════════════════════════════
        //  SAVE HELPERS (pre-allocated, reused)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вспомогательный массив для отслеживания multi-cell предметов
        /// при сохранении. Аллоцируется один раз, очищается через Array.Clear.
        /// </summary>
        private bool[,] _saveDrawn;
        private int     _saveDrawnCols;
        private int     _saveDrawnRows;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Суммарный вес всех предметов в инвентаре (кг).</summary>
        public float TotalWeight { get; private set; }

        /// <summary>
        /// Прямой доступ к сетке для UI и других систем.
        /// Только чтение рекомендуется — мутация через PlayerInventory.
        /// </summary>
        public InventoryGrid Grid => _grid;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _grid = new InventoryGrid(columns, rows);
        }

        private void OnEnable()
        {
            InteractionEvents.OnItemCollected += HandleItemCollected;

            // ── Регистрация в SaveManager ──
            SaveManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            InteractionEvents.OnItemCollected -= HandleItemCollected;

            // ── Отписка от SaveManager ──
            SaveManager.Instance?.Unregister(this);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC METHODS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Удаляет предмет из сетки и пересчитывает вес.
        /// Вызывается UI-системой при drop/discard.
        /// </summary>
        /// <param name="item">Данные предмета для пересчёта веса.</param>
        /// <param name="x">Колонка якорной ячейки.</param>
        /// <param name="y">Строка якорной ячейки.</param>
        public void RemoveItem(ItemData item, int x, int y)
        {
            _grid.RemoveItem(x, y, item.width, item.height);
            TotalWeight -= item.weight;

            if (TotalWeight < 0f) TotalWeight = 0f;

            if (survival != null)
                survival.SetWeight(TotalWeight);
        }

        /// <summary>
        /// Прибавляет вес напрямую (без прохода через RemoveItem).
        /// Используется Fabricator при возврате ингредиентов / добавлении результата.
        /// </summary>
        public void AddWeight(float amount)
        {
            TotalWeight += amount;
            if (TotalWeight < 0f) TotalWeight = 0f;

            if (survival != null)
                survival.SetWeight(TotalWeight);
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable — SAVE / LOAD
        // ══════════════════════════════════════════════════════════

        /// <summary>Inventory загружается после Player stats.</summary>
        public int SavePriority => 20;
        public int LoadPriority => 20;

        /// <summary>
        /// Сохраняет содержимое сетки инвентаря.
        /// Сканирует InventoryGrid, записывает якорные ячейки.
        ///
        /// Используем bool[,] для отслеживания multi-cell предметов
        /// (аналогично HectonInventoryUI._drawn).
        /// </summary>
        public void PopulateSaveData(SaveData data)
        {
            InventoryGrid grid = _grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

            ref InventoryDTO dto = ref data.inventory;
            dto.EnsureCapacity();
            dto.gridColumns  = cols;
            dto.gridRows     = rows;
            dto.totalWeight  = TotalWeight;

            // ── Отслеживание multi-cell (pre-allocated) ──
            EnsureSaveDrawn(cols, rows);
            System.Array.Clear(_saveDrawn, 0, _saveDrawn.Length);

            int cellIndex = 0;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    if (_saveDrawn[x, y]) continue;

                    ItemData item = grid.GetCell(x, y);
                    if (item == null) continue;

                    // Помечаем все ячейки предмета
                    int endX = Mathf.Min(x + item.width, cols);
                    int endY = Mathf.Min(y + item.height, rows);
                    for (int iy = y; iy < endY; iy++)
                        for (int ix = x; ix < endX; ix++)
                            _saveDrawn[ix, iy] = true;

                    // Записываем якорную ячейку
                    if (cellIndex < InventoryDTO.MaxCells)
                    {
                        dto.cells[cellIndex] = new InventoryCellDTO
                        {
                            x      = x,
                            y      = y,
                            itemId = item.name // ScriptableObject.name = asset filename
                        };
                        cellIndex++;
                    }
                }
            }

            dto.cellCount = cellIndex;
        }

        /// <summary>
        /// Восстанавливает инвентарь из SaveData.
        /// Очищает текущую сетку, затем размещает предметы по координатам.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            if (itemCatalog == null)
            {
                Debug.LogError("[PlayerInventory] ItemCatalog not assigned! Cannot load inventory.");
                return;
            }

            InventoryDTO dto = data.inventory;

            // ── Очистка текущего инвентаря ──
            _grid.Clear();
            TotalWeight = 0f;

            if (dto.cells == null || dto.cellCount <= 0) return;

            // ── Восстановление предметов ──
            int count = Mathf.Min(dto.cellCount, dto.cells.Length);

            for (int i = 0; i < count; i++)
            {
                InventoryCellDTO cell = dto.cells[i];

                // ── Поиск ItemData по ID ──
                ItemData item = itemCatalog.FindById(cell.itemId);
                if (item == null)
                {
                    Debug.LogWarning(
                        $"[PlayerInventory] Item '{cell.itemId}' not found in catalog. Skipping.");
                    continue;
                }

                // ── Размещение по сохранённым координатам ──
                if (_grid.CheckFit(cell.x, cell.y, item.width, item.height))
                {
                    _grid.PlaceAt(item, cell.x, cell.y);
                    TotalWeight += item.weight;
                }
                else
                {
                    // Fallback: автопоиск свободного места
                    int px, py;
                    if (_grid.TryAddItem(item, out px, out py))
                    {
                        TotalWeight += item.weight;
                        Debug.LogWarning(
                            $"[PlayerInventory] '{cell.itemId}' repositioned " +
                            $"from ({cell.x},{cell.y}) to ({px},{py}).");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[PlayerInventory] No space for '{cell.itemId}'. Lost.");
                    }
                }
            }

            // ── Синхронизация веса с survival ──
            if (survival != null)
                survival.SetWeight(TotalWeight);
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLER
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обработчик глобального события «предмет подобран».
        /// В цикле пытается разместить quantity единиц предмета.
        /// При неудаче — прекращает попытки (если один не влез,
        /// следующий того же размера тоже не влезет).
        /// </summary>
        private void HandleItemCollected(ItemData item, int quantity, Transform interactor)
        {
            if (item == null) return;

            for (int i = 0; i < quantity; i++)
            {
                int placedX, placedY;

                if (_grid.TryAddItem(item, out placedX, out placedY))
                {
                    TotalWeight += item.weight;

                    if (survival != null)
                        survival.SetWeight(TotalWeight);
                }
                else
                {
                    Debug.LogWarning(
                        $"[PlayerInventory] Инвентарь полон! " +
                        $"Не удалось разместить: {item.itemName} " +
                        $"({item.width}×{item.height}), " +
                        $"осталось {quantity - i} шт.");
                    break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SAVE HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Lazy init / resize массива для отслеживания multi-cell
        /// предметов при сохранении. Аллокация только при смене
        /// размеров сетки (т.е. практически никогда).
        /// </summary>
        private void EnsureSaveDrawn(int cols, int rows)
        {
            if (_saveDrawn != null && _saveDrawnCols == cols && _saveDrawnRows == rows) return;
            _saveDrawn     = new bool[cols, rows];
            _saveDrawnCols = cols;
            _saveDrawnRows = rows;
        }
    }
}