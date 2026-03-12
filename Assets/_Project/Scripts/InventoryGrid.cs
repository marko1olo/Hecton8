// ============================================================================
// HECTON-8 — InventoryGrid.cs
// Ядро математики тетрис-инвентаря.
//
// Чистый C#. Не MonoBehaviour. Zero GC после конструктора.
// Двумерный массив ItemData[columns, rows] — пространственная карта.
// Ячейка == null → свободна. Ячейка != null → занята данным предметом.
// Многоклеточный предмет (w×h) заполняет все покрытые ячейки одной ссылкой.
//
// Соглашение о координатах:
//   x — колонка (0 = левая),  растёт вправо
//   y — строка  (0 = верхняя), растёт вниз
//   Сканирование: сверху-вниз, слева-направо (как чтение текста)
// ============================================================================

namespace Hecton8.Inventory
{
    using Hecton8.Items;

    public sealed class InventoryGrid
    {
        // ══════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════

        private readonly int _columns;
        private readonly int _rows;
        private readonly ItemData[,] _grid;   // [x, y]

        private int _occupiedCells;           // быстрая проверка «полон ли»

        // ══════════════════════════════════════════════════════════
        //  READ-ONLY ACCESSORS
        // ══════════════════════════════════════════════════════════

        public int  Columns       => _columns;
        public int  Rows          => _rows;
        public int  TotalCells    => _columns * _rows;
        public int  OccupiedCells => _occupiedCells;
        public int  FreeCells     => TotalCells - _occupiedCells;
        public bool IsFull        => _occupiedCells >= TotalCells;

        // ══════════════════════════════════════════════════════════
        //  CONSTRUCTOR — единственная аллокация за жизнь объекта
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Создаёт пустую сетку заданного размера.
        /// Вся память выделяется здесь и больше никогда.
        /// </summary>
        /// <param name="columns">Количество колонок (ширина сетки).</param>
        /// <param name="rows">Количество строк (высота сетки).</param>
        public InventoryGrid(int columns, int rows)
        {
            _columns       = columns;
            _rows          = rows;
            _grid          = new ItemData[columns, rows];
            _occupiedCells = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  TryAddItem — автоматический поиск свободного места
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ищет первое свободное место для предмета (скан: сверху-вниз,
        /// слева-направо). Если нашлось — размещает и возвращает true.
        ///
        /// Zero GC: никаких аллокаций. Только вложенные for-циклы.
        /// </summary>
        /// <param name="item">Данные предмета (обязательно width ≥ 1, height ≥ 1).</param>
        /// <param name="placedX">Колонка, в которую был помещён предмет (-1 при неудаче).</param>
        /// <param name="placedY">Строка, в которую был помещён предмет (-1 при неудаче).</param>
        /// <returns>true — предмет размещён; false — нет свободного места.</returns>
        public bool TryAddItem(ItemData item, out int placedX, out int placedY)
        {
            int iw = item.width;
            int ih = item.height;

            // Быстрый отказ: предмет больше сетки или нет свободных ячеек
            if (iw > _columns || ih > _rows || _occupiedCells >= TotalCells)
            {
                placedX = -1;
                placedY = -1;
                return false;
            }

            // Пределы сканирования: нет смысла проверять позиции,
            // где предмет гарантированно выходит за границу
            int maxX = _columns - iw;
            int maxY = _rows    - ih;

            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    // Ранний выход: если якорная ячейка занята — следующая колонка
                    if (_grid[x, y] != null) continue;

                    if (CheckFitInternal(x, y, iw, ih))
                    {
                        PlaceItem(item, x, y, iw, ih);
                        placedX = x;
                        placedY = y;
                        return true;
                    }
                }
            }

            placedX = -1;
            placedY = -1;
            return false;
        }

        // ══════════════════════════════════════════════════════════
        //  CheckFit — проверка вместимости (публичная)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет, помещается ли предмет размером width×height
        /// в позицию (startX, startY) без выхода за границы и коллизий.
        ///
        /// Вызывается из TryAddItem, а также может использоваться
        /// будущей UI-системой для drag-and-drop превью.
        /// </summary>
        public bool CheckFit(int startX, int startY, int width, int height)
        {
            // Bounds check (включая отрицательные координаты)
            if (startX < 0 || startY < 0) return false;
            if (startX + width > _columns) return false;
            if (startY + height > _rows)   return false;

            return CheckFitInternal(startX, startY, width, height);
        }

        // ══════════════════════════════════════════════════════════
        //  RemoveItem — очистка ячеек
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Очищает прямоугольник ячеек (x, y, width, height).
        /// Безопасно кламписует к границам сетки.
        /// Уменьшает счётчик занятых ячеек корректно (только реально занятые).
        /// </summary>
        /// <param name="x">Левая колонка.</param>
        /// <param name="y">Верхняя строка.</param>
        /// <param name="width">Ширина очищаемой области.</param>
        /// <param name="height">Высота очищаемой области.</param>
        public void RemoveItem(int x, int y, int width, int height)
        {
            // Clamping для защиты от невалидных вызовов
            int x0 = x < 0 ? 0 : x;
            int y0 = y < 0 ? 0 : y;
            int x1 = x + width;
            int y1 = y + height;
            if (x1 > _columns) x1 = _columns;
            if (y1 > _rows)    y1 = _rows;

            for (int ix = x0; ix < x1; ix++)
            {
                for (int iy = y0; iy < y1; iy++)
                {
                    if (_grid[ix, iy] != null)
                    {
                        _grid[ix, iy] = null;
                        _occupiedCells--;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  GetCell — чтение одной ячейки
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает ItemData в ячейке (x, y) или null,
        /// если ячейка пуста или координаты за пределами сетки.
        /// Безопасен для вызова с любыми координатами.
        /// </summary>
        public ItemData GetCell(int x, int y)
        {
            if ((uint)x >= (uint)_columns || (uint)y >= (uint)_rows)
                return null;

            return _grid[x, y];
        }

        // ══════════════════════════════════════════════════════════
        //  PlaceAt — ручное размещение (для drag-and-drop UI)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Размещает предмет в заданную позицию БЕЗ автопоиска.
        /// Предварительно вызовите CheckFit() для валидации.
        /// Если CheckFit вернул false, вызов PlaceAt повредит данные.
        /// </summary>
        /// <returns>true если размещение выполнено.</returns>
        public bool PlaceAt(ItemData item, int x, int y)
        {
            int iw = item.width;
            int ih = item.height;

            if (!CheckFit(x, y, iw, ih))
                return false;

            PlaceItem(item, x, y, iw, ih);
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  Clear — полный сброс
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Очищает всю сетку. Zero GC — обнуляет существующий массив.
        /// </summary>
        public void Clear()
        {
            System.Array.Clear(_grid, 0, _grid.Length);
            _occupiedCells = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — внутренние хелперы
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Быстрая проверка коллизий БЕЗ проверки границ (bounds
        /// уже проверены вызывающим кодом или гарантированы математикой
        /// цикла в TryAddItem).
        /// </summary>
        private bool CheckFitInternal(int startX, int startY, int w, int h)
        {
            int endX = startX + w;
            int endY = startY + h;

            for (int ix = startX; ix < endX; ix++)
                for (int iy = startY; iy < endY; iy++)
                    if (_grid[ix, iy] != null)
                        return false;

            return true;
        }

        /// <summary>
        /// Записывает ссылку на ItemData во все ячейки прямоугольника.
        /// Обновляет счётчик занятых ячеек.
        /// </summary>
        private void PlaceItem(ItemData item, int x, int y, int w, int h)
        {
            int endX = x + w;
            int endY = y + h;

            for (int ix = x; ix < endX; ix++)
                for (int iy = y; iy < endY; iy++)
                    _grid[ix, iy] = item;

            _occupiedCells += w * h;
        }
    }
}