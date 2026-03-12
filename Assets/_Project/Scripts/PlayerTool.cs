// ============================================================================
// HECTON-8 — PlayerTool.cs
// Абстрактный базовый класс для всех инструментов игрока.
//
// Ответственности:
//   1. Определяет жизненный цикл инструмента: Equip → Use → Unequip.
//   2. Реализует IPoolable для совместимости с ObjectPoolManager.
//   3. Хранит ссылку на ItemData для связи с инвентарной системой.
//
// НЕ содержит Update/FixedUpdate — логика вызывается через
// PlayerToolManager.Tick() → UsePrimary()/UseSecondary().
//
// НАСЛЕДНИКИ: LaserCutter, Scanner, Builder и т.д.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Items;
    using UnityEngine;

    /// <summary>
    /// Базовый класс для всех инструментов, которые игрок
    /// может держать в руках. Управляется через <see cref="PlayerToolManager"/>.
    /// </summary>
    public abstract class PlayerTool : MonoBehaviour, IPoolable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Tool Identity ─────────────────────────────")]
        [Tooltip("Ссылка на ItemData этого инструмента. " +
                 "Используется для проверки наличия в инвентаре.")]
        [SerializeField] private ItemData _toolData;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Данные предмета инструмента (ScriptableObject).
        /// Используется <see cref="PlayerToolManager"/> для проверки
        /// наличия инструмента в <see cref="Hecton8.Inventory.PlayerInventory"/>.
        /// </summary>
        public ItemData ToolData => _toolData;

        /// <summary>
        /// Экипирован ли инструмент в данный момент.
        /// Устанавливается <see cref="PlayerToolManager"/> при Equip/Unequip.
        /// </summary>
        public bool IsEquipped { get; private set; }

        // ══════════════════════════════════════════════════════════
        //  IPoolable — POOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается ObjectPoolManager при извлечении из пула.
        /// Сбрасывает состояние инструмента к начальному.
        /// Наследники могут переопределить для своей инициализации,
        /// но ОБЯЗАНЫ вызвать base.OnSpawn().
        /// </summary>
        public virtual void OnSpawn()
        {
            IsEquipped = false;
        }

        /// <summary>
        /// Вызывается ObjectPoolManager при возврате в пул.
        /// Гарантирует, что инструмент корректно отключён.
        /// Наследники могут переопределить для очистки,
        /// но ОБЯЗАНЫ вызвать base.OnDespawn().
        /// </summary>
        public virtual void OnDespawn()
        {
            // Если инструмент ещё экипирован — корректно снимаем
            if (IsEquipped)
            {
                OnUnequip();
            }

            IsEquipped = false;
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE — вызывается PlayerToolManager
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается при экипировке инструмента в руки игрока.
        /// Используй для: включения визуальных элементов,
        /// активации компонентов, запуска idle-анимации.
        /// </summary>
        public virtual void OnEquip()
        {
            IsEquipped = true;
        }

        /// <summary>
        /// Вызывается при снятии инструмента из рук.
        /// Используй для: отключения VFX, остановки звуков,
        /// сброса состояния использования.
        /// </summary>
        public virtual void OnUnequip()
        {
            IsEquipped = false;
        }

        /// <summary>
        /// Основное действие инструмента (ЛКМ / Fire1).
        /// Вызывается каждый кадр, пока зажата кнопка.
        /// Пример: стрельба лазером, сканирование, размещение блока.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime — для frame-independent логики.</param>
        public virtual void UsePrimary(float deltaTime) { }

        /// <summary>
        /// Альтернативное действие инструмента (ПКМ / Fire2).
        /// Вызывается каждый кадр, пока зажата кнопка.
        /// Пример: альт-режим резака, зум сканера, поворот блока.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime — для frame-independent логики.</param>
        public virtual void UseSecondary(float deltaTime) { }

        /// <summary>
        /// Вызывается каждый кадр через PlayerToolManager.Tick(),
        /// независимо от нажатия кнопок. Используй для: idle-анимации,
        /// покачивания, обновления UI индикаторов инструмента.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public virtual void ToolTick(float deltaTime) { }
    }
}