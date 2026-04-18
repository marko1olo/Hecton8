// ============================================================================
// HECTON-8 — PowerGrid.cs
// Энергетическая сеть — чистый C# класс (не MonoBehaviour).
//
// ОТВЕТСТВЕННОСТИ:
//   1. Хранение узлов (PowerNode) одной связной сети.
//   2. Подсчёт энергетического баланса (генерация vs потребление).
//   3. Приоритетное отключение потребителей при дефиците.
//   4. Поглощение другой сети при объединении (merge).
//
// АЛГОРИТМ БАЛАНСА:
//   1. Собрать все IPowerComponent из всех PowerNode.
//   2. Разделить на генераторы (rating > 0) и потребителей (rating < 0).
//   3. Суммировать генерацию и потребление.
//   4. Если генерация >= потребление → все включены.
//   5. Если генерация < потребление → приоритетное отключение:
//      a. Сортировать потребителей по PowerPriority DESC (высокие первые).
//      b. Включать потребителей пока хватает мощности.
//      c. Остальных отключать.
//
// ZERO GC:
//   • HashSet<PowerNode> — pre-allocated, Add/Remove = amortized O(1).
//   • List<IPowerComponent> — кэшированы, Clear() не аллоцирует.
//   • Sort с кэшированным static Comparison<T> — zero GC.
//   • Вызовы OnPowerStatusChanged — direct method call, no boxing.
//
// ПОТОКОБЕЗОПАСНОСТЬ: нет. Вызывать только из Main Thread.
// ============================================================================

using System.Collections.Generic;

namespace Hecton8.Power
{
    /// <summary>
    /// Одна связная энергетическая сеть.
    /// Содержит узлы, подсчитывает баланс, управляет отключением.
    /// Не MonoBehaviour — чистые данные + логика.
    /// </summary>
    public sealed class PowerGrid
    {
        // ══════════════════════════════════════════════════════════
        //  IDENTITY
        // ══════════════════════════════════════════════════════════

        /// <summary>Уникальный ID сети (для отладки и логирования).</summary>
        public readonly int Id;

        private static int _nextId;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _nextId = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  STORAGE — узлы сети
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Все узлы (PowerNode) в этой сети.
        /// HashSet: O(1) Add, Remove, Contains. Pre-allocated.
        /// </summary>
        private readonly HashSet<PowerNode> _nodes;

        // ══════════════════════════════════════════════════════════
        //  CACHED LISTS — переиспользуются в UpdateBalance
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Кэш потребителей (PowerRating &lt; 0).
        /// Заполняется в UpdateBalance, Clear() в начале.
        /// Clear() не аллоцирует — обнуляет Count, массив остаётся.
        /// </summary>
        private readonly List<IPowerComponent> _consumers;

        // ══════════════════════════════════════════════════════════
        //  BALANCE STATE
        // ══════════════════════════════════════════════════════════

        private float _totalGeneration;
        private float _totalConsumption;
        private float _balance;
        private bool  _hasPowerDeficit;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество узлов в сети.</summary>
        public int NodeCount => _nodes.Count;

        /// <summary>Суммарная генерация (Вт). Всегда ≥ 0.</summary>
        public float TotalGeneration => _totalGeneration;

        /// <summary>Суммарное потребление (Вт, положительное значение). Всегда ≥ 0.</summary>
        public float TotalConsumption => _totalConsumption;

        /// <summary>Баланс (генерация − потребление). Отрицательный = дефицит.</summary>
        public float Balance => _balance;

        /// <summary>true если текущий баланс &lt; 0.</summary>
        public bool HasPowerDeficit => _hasPowerDeficit;

        /// <summary>Read-only доступ к узлам (для BFS в PowerGridManager).</summary>
        public HashSet<PowerNode> Nodes => _nodes;

        // ══════════════════════════════════════════════════════════
        //  POWER CONSUMPTION API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Потребляет указанное количество энергии из накопленного баланса.
        /// Используется для разовых операций (крафт, зарядка).
        /// Уменьшает _totalGeneration на указанное значение.
        /// </summary>
        /// <param name="amount">Количество энергии для потребления (Вт·ч).</param>
        public void ConsumePower(float amount)
        {
            if (amount <= 0f) return;
            _totalGeneration = System.Math.Max(0f, _totalGeneration - amount);
            _balance = _totalGeneration - _totalConsumption;
        }

        // ══════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Создаёт новую энергетическую сеть.
        /// </summary>
        /// <param name="initialCapacity">Начальная ёмкость HashSet.</param>
        public PowerGrid(int initialCapacity = 16)
        {
            Id = _nextId++;
            _nodes     = new HashSet<PowerNode>(initialCapacity);
            _consumers = new List<IPowerComponent>(16);
        }

        // ══════════════════════════════════════════════════════════
        //  NODE MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Добавляет узел в сеть. Устанавливает обратную ссылку.
        /// Дубликаты игнорируются (HashSet).
        /// </summary>
        public void AddNode(PowerNode node)
        {
            if (node == null) return;
            if (!_nodes.Add(node) && ReferenceEquals(node.Grid, this))
                return;

            node.SetGrid(this);
        }

        /// <summary>
        /// Удаляет узел из сети. Сбрасывает обратную ссылку.
        /// Безопасно при отсутствии узла (no-op).
        /// </summary>
        public void RemoveNode(PowerNode node)
        {
            if (node == null) return;
            _nodes.Remove(node);

            if (ReferenceEquals(node.Grid, this))
                node.SetGrid(null);
        }

        /// <summary>
        /// Поглощает все узлы из другой сети.
        /// Вызывается при объединении сетей (MergeGrids).
        /// Другая сеть остаётся пустой после вызова.
        /// </summary>
        public void AbsorbAll(PowerGrid other)
        {
            if (other == null) return;
            if (ReferenceEquals(other, this)) return;
            if (other._nodes == null || other._nodes.Count == 0)
            {
                other._nodes?.Clear();
                return;
            }

            foreach (PowerNode node in other._nodes)
            {
                if (node == null) continue;
                _nodes.Add(node);
                node.SetGrid(this);
            }

            other._nodes.Clear();
        }

        // ══════════════════════════════════════════════════════════
        //  BALANCE CALCULATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Пересчитывает энергетический баланс всей сети.
        ///
        /// Алгоритм:
        ///   1. Итерируем все узлы → все IPowerComponent.
        ///   2. Суммируем генерацию (rating > 0) и потребление (rating &lt; 0).
        ///   3. Если баланс ≥ 0 → включаем всех потребителей.
        ///   4. Если баланс &lt; 0 → приоритетное отключение.
        ///
        /// Вызывается PowerGridManager.SlowTick() раз в ~0.5-1с.
        ///
        /// ZERO GC:
        ///   • _consumers.Clear() — zero alloc.
        ///   • Sort с кэшированным Comparison — zero alloc.
        ///   • for-цикл по List — zero alloc.
        ///   • HashSet foreach — struct enumerator, zero alloc.
        /// </summary>
        public void UpdateBalance()
        {
            _consumers.Clear();
            _totalGeneration  = 0f;
            _totalConsumption = 0f;

            // ════════════════════════════════════════════════════
            //  1. СБОР КОМПОНЕНТОВ ИЗ ВСЕХ УЗЛОВ
            // ════════════════════════════════════════════════════

            foreach (PowerNode node in _nodes)
            {
                if (node == null) continue;

                List<IPowerComponent> comps = node.Components;
                if (comps == null) continue;

                int compCount = comps.Count;

                for (int i = 0; i < compCount; i++)
                {
                    IPowerComponent comp = comps[i];
                    if (comp == null) continue;

                    float rating = comp.PowerRating;

                    if (rating > 0f)
                    {
                        // Генератор
                        _totalGeneration += rating;
                    }
                    else if (rating < 0f)
                    {
                        // Потребитель
                        _totalConsumption += -rating; // Сохраняем как положительное
                        _consumers.Add(comp);
                    }
                    // rating == 0: пассивный — игнорируем
                }
            }

            _balance = _totalGeneration - _totalConsumption;

            // ════════════════════════════════════════════════════
            //  2. РАСПРЕДЕЛЕНИЕ ЭНЕРГИИ
            // ════════════════════════════════════════════════════

            if (_balance >= 0f)
            {
                // Энергии хватает — включить всех
                _hasPowerDeficit = false;
                PowerOnAll();
            }
            else
            {
                // Дефицит — приоритетное отключение
                _hasPowerDeficit = true;
                PerformPriorityShutdown();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — POWER ON ALL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Включает питание всем потребителям.
        /// OnPowerStatusChanged вызывается ТОЛЬКО если статус изменился.
        /// </summary>
        private void PowerOnAll()
        {
            int count = _consumers.Count;
            for (int i = 0; i < count; i++)
            {
                IPowerComponent consumer = _consumers[i];
                if (!consumer.HasPower)
                    consumer.OnPowerStatusChanged(true);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — PRIORITY SHUTDOWN
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Приоритетное отключение потребителей при дефиците.
        ///
        /// Алгоритм:
        ///   1. Сортировать потребителей по PowerPriority DESC.
        ///      Высокий приоритет (100 = роскошь) → отключить ПЕРВЫМ.
        ///      Низкий приоритет (0 = критический) → отключить ПОСЛЕДНИМ.
        ///   2. Пройти по списку, распределяя мощность.
        ///   3. Потребителей, на которых хватает мощности → включить.
        ///   4. Остальных → отключить.
        ///
        /// Сортировка: in-place List.Sort с кэшированным Comparison.
        /// Zero GC (delegate не аллоцируется повторно).
        /// </summary>
        private void PerformPriorityShutdown()
        {
            // ── Сортировка: роскошь (100) первыми, критические (0) последними ──
            _consumers.Sort(PriorityCompareDescending);

            float remainingPower = _totalGeneration;

            int count = _consumers.Count;

            for (int i = count - 1; i >= 0; i--)
            {
                // Обходим от КОНЦА (низкий приоритет = критический = включаем первым)
                IPowerComponent consumer = _consumers[i];
                float demand = -consumer.PowerRating; // Положительное значение

                if (remainingPower >= demand)
                {
                    // Мощности хватает — включаем
                    remainingPower -= demand;

                    if (!consumer.HasPower)
                        consumer.OnPowerStatusChanged(true);
                }
                else
                {
                    // Не хватает — отключаем
                    if (consumer.HasPower)
                        consumer.OnPowerStatusChanged(false);
                }
            }
        }

        /// <summary>
        /// Кэшированный компаратор для сортировки потребителей.
        /// Сортировка по убыванию приоритета:
        ///   100 (роскошь) → 50 (обычный) → 0 (критический).
        ///
        /// Static readonly delegate — одна аллокация при загрузке класса.
        /// </summary>
        private static readonly System.Comparison<IPowerComponent> PriorityCompareDescending =
            (a, b) => b.PowerPriority.CompareTo(a.PowerPriority);
    }
}
