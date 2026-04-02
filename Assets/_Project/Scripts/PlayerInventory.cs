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
    using System;
    using Hecton8.Gameplay;
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
        /// <summary>
        /// Количество предметов в стеке для каждой якорной ячейки.
        /// Индекс = y * columns + x. Не-якорные ячейки = 0.
        /// </summary>
        private int[] _stackCounts;
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

        /// <summary>
        /// Fired whenever inventory contents change and UI must refresh.
        /// Event is raised only after a completed mutation of the grid.
        /// </summary>
        public event Action InventoryChanged;

        // DEPRECATED: use InventoryEvents.OnInventoryFull
        // public event Action<ItemData> InventoryFull;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _grid = new InventoryGrid(columns, rows);
            _stackCounts = new int[columns * rows];
            _sortBuffer = new ItemPlacement[columns * rows];
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
            int idx = AnchorIndex(x, y);
            int count = _stackCounts[idx];
            if (count < 1) count = 1;

            _grid.RemoveItem(x, y, item.width, item.height);
            _stackCounts[idx] = 0;
            TotalWeight -= item.weight * count;

            if (TotalWeight < 0f) TotalWeight = 0f;
            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
        }
        /// <summary>
        /// Удаляет одну единицу из стека. Если стек становится пуст —
        /// очищает ячейки. Возвращает удалённый ItemData (для спавна в мир).
        /// </summary>
        public ItemData RemoveOneItem(int anchorX, int anchorY)
        {
            ItemData item = _grid.GetCell(anchorX, anchorY);
            if (item == null) return null;

            int idx = AnchorIndex(anchorX, anchorY);
            int count = _stackCounts[idx];

            if (count > 1)
            {
                _stackCounts[idx]--;
            }
            else
            {
                _grid.RemoveItem(anchorX, anchorY, item.width, item.height);
                _stackCounts[idx] = 0;
            }

            TotalWeight -= item.weight;
            if (TotalWeight < 0f) TotalWeight = 0f;
            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
            return item;
        }

        /// <summary>
        /// Потребляет одну единицу предмета из стека.
        /// Применяет эффект через HectonSurvivalSystem.
        /// Возвращает true если предмет был потреблён.
        /// </summary>
        public bool ConsumeOneItem(int anchorX, int anchorY)
        {
            ItemData item = _grid.GetCell(anchorX, anchorY);
            if (item == null || !item.isConsumable) return false;

            // Применяем эффекты
            if (survival != null)
            {
                if (item.oxygenRestore > 0f)    survival.RefillOxygen(item.oxygenRestore);
                if (item.energyRestore > 0f)    survival.RechargeEnergy(item.energyRestore);
                if (item.integrityRestore > 0f) survival.Repair(item.integrityRestore);
            }

            // Удаляем единицу
            RemoveOneItem(anchorX, anchorY);
            return true;
        }

        /// <summary>Количество предметов в стеке по координатам якоря.</summary>
        public int GetStackCount(int anchorX, int anchorY)
        {
            if (_stackCounts == null) return 0;
            int idx = AnchorIndex(anchorX, anchorY);
            if (idx < 0 || idx >= _stackCounts.Length) return 0;
            return _stackCounts[idx];
        }

        /// <summary>
        /// Суммарное количество единиц предмета по всем стекам.
        /// Используется крафтом для проверки «есть ли 3 титана».
        /// </summary>
        public int CountTotal(ItemData item)
        {
            if (item == null || _grid == null) return 0;

            int total = 0;
            int cols = _grid.Columns;
            int rws = _grid.Rows;

            for (int y = 0; y < rws; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    ItemData cell = _grid.GetCell(x, y);
                    if (!ReferenceEquals(cell, item)) continue;

                    if (x > 0 && ReferenceEquals(_grid.GetCell(x - 1, y), item)) continue;
                    if (y > 0 && ReferenceEquals(_grid.GetCell(x, y - 1), item)) continue;

                    int idx = AnchorIndex(x, y);
                    total += Mathf.Max(1, _stackCounts[idx]);
                }
            }

            return total;
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

        /// <summary>
        /// Returns true if at least one anchor instance of the item is present in the grid.
        /// Safe for UI / hotbar availability checks.
        /// </summary>
        public bool ContainsItem(ItemData item)
        {
            return CountAnchors(item) > 0;
        }

        /// <summary>
        /// Programmatically adds one or more items into the inventory using the
        /// same stacking/placement rules as world pickup flow.
        /// Returns true only if the full quantity was added.
        /// </summary>
        public bool TryAddItem(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0)
                return false;

            bool allAdded = true;

            for (int i = 0; i < quantity; i++)
            {
                if (item.stackable && TryStackItem(item))
                {
                    TotalWeight += item.weight;
                    continue;
                }

                int placedX;
                int placedY;
                if (_grid.TryAddItem(item, out placedX, out placedY))
                {
                    _stackCounts[AnchorIndex(placedX, placedY)] = 1;
                    TotalWeight += item.weight;
                }
                else
                {
                    allAdded = false;
                    InventoryEvents.NotifyInventoryFull(item);
                    break;
                }
            }

            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
            return allAdded;
        }

        /// <summary>
        /// Removes an exact quantity of one item type across all anchor stacks.
        /// Returns false and performs no mutation when the inventory does not
        /// contain the full requested quantity.
        /// </summary>
        public bool TryRemoveQuantity(ItemData item, int quantity)
        {
            if (item == null || quantity <= 0)
                return false;

            if (CountTotal(item) < quantity)
                return false;

            int remaining = quantity;
            int cols = _grid.Columns;
            int rows = _grid.Rows;

            for (int y = 0; y < rows && remaining > 0; y++)
            {
                for (int x = 0; x < cols && remaining > 0; x++)
                {
                    ItemData cell = _grid.GetCell(x, y);
                    if (!ReferenceEquals(cell, item))
                        continue;

                    if (x > 0 && ReferenceEquals(_grid.GetCell(x - 1, y), item))
                        continue;
                    if (y > 0 && ReferenceEquals(_grid.GetCell(x, y - 1), item))
                        continue;

                    int idx = AnchorIndex(x, y);
                    int stackCount = Mathf.Max(1, _stackCounts[idx]);
                    int take = Mathf.Min(stackCount, remaining);

                    if (take >= stackCount)
                    {
                        _grid.RemoveItem(x, y, item.width, item.height);
                        _stackCounts[idx] = 0;
                    }
                    else
                    {
                        _stackCounts[idx] = stackCount - take;
                    }

                    TotalWeight -= item.weight * take;
                    remaining -= take;
                }
            }

            if (TotalWeight < 0f)
                TotalWeight = 0f;

            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
            return true;
        }

        /// <summary>
        /// Counts top-left anchor placements of a specific item.
        /// Multi-cell items are counted once.
        /// </summary>
        public int CountAnchors(ItemData item)
        {
            if (item == null || _grid == null)
                return 0;

            int count = 0;
            int cols = _grid.Columns;
            int rows = _grid.Rows;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    ItemData cell = _grid.GetCell(x, y);
                    if (!ReferenceEquals(cell, item))
                        continue;

                    if (x > 0 && ReferenceEquals(_grid.GetCell(x - 1, y), item))
                        continue;

                    if (y > 0 && ReferenceEquals(_grid.GetCell(x, y - 1), item))
                        continue;

                    count++;
                }
            }

            return count;
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
                            x          = x,
                            y          = y,
                            itemId     = item.name,
                            stackCount = _stackCounts[y * cols + x]
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
            System.Array.Clear(_stackCounts, 0, _stackCounts.Length);
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
                    _grid.PlaceAt(item, cell.x, cell.y); //это надо или нет? я не понял нейронка не сказала точно. подумай надо или нет!
                    // После успешного _grid.CheckFit / PlaceAt:
                    _grid.PlaceAt(item, cell.x, cell.y); 
                    
                    int loadedCount = cell.stackCount > 0 ? cell.stackCount : 1;
                    _stackCounts[AnchorIndex(cell.x, cell.y)] = loadedCount;
                    TotalWeight += item.weight * loadedCount;
                }
                else
                {
                    // Fallback: автопоиск свободного места
                    int px, py;
                    if (_grid.TryAddItem(item, out px, out py))
                    {
                        int loadedCount = cell.stackCount > 0 ? cell.stackCount : 1;
                        _stackCounts[AnchorIndex(px, py)] = loadedCount;
                        TotalWeight += item.weight * loadedCount;
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

            NotifyInventoryChanged();
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
            if (item == null)
                return;

            bool allAdded = TryAddItem(item, quantity);
            if (!allAdded)
            {
                Debug.LogWarning(
                    $"[PlayerInventory] Инвентарь полон! " +
                    $"Не удалось полностью разместить: {item.itemName} " +
                    $"({item.width}×{item.height}).");
            }
        }
        /// <summary>
        /// Ищет существующий неполный стек того же предмета.
        /// Если найден — инкрементирует count и возвращает true.
        /// </summary>
        private bool TryStackItem(ItemData item)
        {
            int cols = _grid.Columns;
            int rows = _grid.Rows;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    ItemData cell = _grid.GetCell(x, y);
                    if (!ReferenceEquals(cell, item)) continue;

                    // Проверяем что это якорная ячейка
                    if (x > 0 && ReferenceEquals(_grid.GetCell(x - 1, y), item)) continue;
                    if (y > 0 && ReferenceEquals(_grid.GetCell(x, y - 1), item)) continue;

                    int idx = AnchorIndex(x, y);
                    if (_stackCounts[idx] < item.maxStack)
                    {
                        _stackCounts[idx]++;
                        return true;
                    }
                }
            }

            return false;
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
        /// <summary>
        /// Сортирует инвентарь: собирает все предметы, сортирует по
        /// категории → имени → весу, заново размещает. Zero-alloc
        /// кроме одноразового массива.
        /// </summary>
        public void SortInventory()
        {
            // Собираем все размещения
            int count = GetPlacements(_sortBuffer);
            if (count <= 0) return;

            // Собираем данные для сортировки
            if (_sortEntries == null || _sortEntries.Length < count)
                _sortEntries = new SortEntry[count];

            for (int i = 0; i < count; i++)
            {
                _sortEntries[i] = new SortEntry
                {
                    item = _sortBuffer[i].item,
                    stackCount = _sortBuffer[i].stackCount
                };
            }

            // Сортируем
            System.Array.Sort(_sortEntries, 0, count, SortEntryComparer.Instance);

            // Очищаем сетку
            _grid.Clear();
            System.Array.Clear(_stackCounts, 0, _stackCounts.Length);
            TotalWeight = 0f;

            // Заново размещаем
            for (int i = 0; i < count; i++)
            {
                SortEntry entry = _sortEntries[i];
                int px, py;
                if (_grid.TryAddItem(entry.item, out px, out py))
                {
                    _stackCounts[AnchorIndex(px, py)] = entry.stackCount;
                    TotalWeight += entry.item.weight * entry.stackCount;
                }
            }

            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
        }

        private struct SortEntry
        {
            public ItemData item;
            public int stackCount;
        }

        private sealed class SortEntryComparer : System.Collections.Generic.IComparer<SortEntry>
        {
            public static readonly SortEntryComparer Instance = new SortEntryComparer();

            public int Compare(SortEntry a, SortEntry b)
            {
                // По категории
                int cat = ((int)a.item.category).CompareTo((int)b.item.category);
                if (cat != 0) return cat;

                // По имени
                int name = string.Compare(a.item.itemName, b.item.itemName,
                    System.StringComparison.Ordinal);
                if (name != 0) return name;

                // По весу (тяжёлые первыми)
                return b.item.weight.CompareTo(a.item.weight);
            }
        }

        private SortEntry[] _sortEntries;
        private PlayerInventory.ItemPlacement[] _sortBuffer;

        private int AnchorIndex(int x, int y) => y * _grid.Columns + x;
        private void NotifyInventoryChanged()
        {
            InventoryChanged?.Invoke();
        }

        // ══════════════════════════════════════════════════════════
        //  SNAPSHOT FOR UI (zero-mutation read)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Описывает один предмет, размещённый в сетке.
        /// Координаты — якорная (верхняя-левая) ячейка.
        /// </summary>
        public struct ItemPlacement
        {
            public ItemData item;
            public int x;
            public int y;
            public int stackCount;
        }

        /// <summary>
        /// Заполняет буфер якорными размещениями предметов.
        /// Не мутирует сетку. Zero GC если буфер достаточного размера.
        /// </summary>
        /// <param name="buffer">Pre-allocated массив. Рекомендуемый размер: columns * rows.</param>
        /// <returns>Количество записанных элементов.</returns>
        public int GetPlacements(ItemPlacement[] buffer)
        {
            if (buffer == null || _grid == null) return 0;

            int cols = _grid.Columns;
            int rws = _grid.Rows;

            EnsureSaveDrawn(cols, rws);
            System.Array.Clear(_saveDrawn, 0, _saveDrawn.Length);

            int count = 0;

            for (int y = 0; y < rws; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    if (_saveDrawn[x, y]) continue;

                    ItemData item = _grid.GetCell(x, y);
                    if (item == null) continue;

                    int endX = Mathf.Min(x + item.width, cols);
                    int endY = Mathf.Min(y + item.height, rws);
                    for (int iy = y; iy < endY; iy++)
                        for (int ix = x; ix < endX; ix++)
                            _saveDrawn[ix, iy] = true;

                    if (count < buffer.Length)
                    {
                        int idx = AnchorIndex(x, y);
                        buffer[count++] = new ItemPlacement
                        {
                            item = item,
                            x = x,
                            y = y,
                            stackCount = Mathf.Max(1, _stackCounts[idx])
                        };
                    }
                }
            }

            return count;
        }
    }
}
