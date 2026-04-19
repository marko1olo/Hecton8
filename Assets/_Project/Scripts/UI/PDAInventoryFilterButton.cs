// ============================================================================
// HECTON-8 — PDAInventoryFilterButton.cs
// Кнопка фильтра инвентаря в PDA.
// Управляет визуальным состоянием и фильтрацией предметов.
// NOTE: Создан как заглушка для восстановления компиляции после рефакторинга.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Кнопка фильтра инвентаря в PDA.
    /// Отображает состояние (активная/неактивная) и применяет фильтр при нажатии.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Inventory Filter Button")]
    public sealed class PDAInventoryFilterButton : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private PDAInventoryTab _inventoryTab;
        private InventoryViewFilter _filter;
        private Image _background;
        private TextMeshProUGUI _label;
        private Color _bgActive;
        private Color _bgInactive;
        private Color _textActive;
        private Color _textInactive;
        private bool _isActive;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Фильтр, который применяет эта кнопка.
        /// </summary>
        internal InventoryViewFilter Filter => _filter;

        /// <summary>
        /// Активен ли фильтр.
        /// </summary>
        public bool IsActive => _isActive;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Инициализирует кнопку фильтра.
        /// </summary>
        /// <param name="inventoryTab">Ссылка на вкладку инвентаря.</param>
        /// <param name="filter">Фильтр, который применяет кнопка.</param>
        /// <param name="background">Изображение фона кнопки.</param>
        /// <param name="label">Текст метки.</param>
        /// <param name="bgActive">Цвет фона в активном состоянии.</param>
        /// <param name="bgInactive">Цвет фона в неактивном состоянии.</param>
        /// <param name="textActive">Цвет текста в активном состоянии.</param>
        /// <param name="textInactive">Цвет текста в неактивном состоянии.</param>
        internal void Init(
            PDAInventoryTab inventoryTab,
            InventoryViewFilter filter,
            Image background,
            TextMeshProUGUI label,
            Color bgActive,
            Color bgInactive,
            Color textActive,
            Color textInactive)
        {
            _inventoryTab = inventoryTab;
            _filter = filter;
            _background = background;
            _label = label;
            _bgActive = bgActive;
            _bgInactive = bgInactive;
            _textActive = textActive;
            _textInactive = textInactive;
        }

        /// <summary>
        /// Устанавливает активное состояние фильтра.
        /// </summary>
        /// <param name="active">True если фильтр активен.</param>
        public void SetActive(bool active)
        {
            _isActive = active;
            UpdateVisuals();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════

        private void UpdateVisuals()
        {
            if (_background != null)
                _background.color = _isActive ? _bgActive : _bgInactive;

            if (_label != null)
                _label.color = _isActive ? _textActive : _textInactive;
        }

        // ══════════════════════════════════════════════════════════
        //  UNITY CALLBACKS
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            var button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            var button = GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (_inventoryTab != null)
                _inventoryTab.SetFilter(_filter);
        }
    }
}
