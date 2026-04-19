// ============================================================================
// HECTON-8 — PDATabButton.cs
// Кнопка вкладки PDA.
// Управляет визуальным состоянием (активная/неактивная) и переключением вкладок.
// NOTE: Создан как заглушка для восстановления компиляции после рефакторинга.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Кнопка вкладки в PDA.
    /// Отображает состояние (активная/неактивная) и обрабатывает нажатие.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Tab Button")]
    public sealed class PDATabButton : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private int _tabIndex;
        private PlayerPDA _playerPDA;
        private Image _background;
        private TextMeshProUGUI _label;
        private Color _bgActive;
        private Color _bgInactive;
        private Color _textActive;
        private Color _textInactive;
        private bool _isActive;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Индекс вкладки.
        /// </summary>
        public int TabIndex => _tabIndex;

        /// <summary>
        /// Активна ли вкладка.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Инициализирует кнопку вкладки.
        /// </summary>
        /// <param name="tabIndex">Индекс вкладки.</param>
        /// <param name="playerPDA">Ссылка на PlayerPDA для переключения вкладок.</param>
        /// <param name="background">Изображение фона кнопки.</param>
        /// <param name="label">Текст метки.</param>
        /// <param name="bgActive">Цвет фона в активном состоянии.</param>
        /// <param name="bgInactive">Цвет фона в неактивном состоянии.</param>
        /// <param name="textActive">Цвет текста в активном состоянии.</param>
        /// <param name="textInactive">Цвет текста в неактивном состоянии.</param>
        public void Init(
            int tabIndex,
            PlayerPDA playerPDA,
            Image background,
            TextMeshProUGUI label,
            Color bgActive,
            Color bgInactive,
            Color textActive,
            Color textInactive)
        {
            _tabIndex = tabIndex;
            _playerPDA = playerPDA;
            _background = background;
            _label = label;
            _bgActive = bgActive;
            _bgInactive = bgInactive;
            _textActive = textActive;
            _textInactive = textInactive;
            _isActive = tabIndex == 0; // Первая вкладка активна по умолчанию
        }

        /// <summary>
        /// Устанавливает активное состояние вкладки.
        /// </summary>
        /// <param name="active">True если вкладка активна.</param>
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
            // Добавляем обработчик клика если есть Button компонент
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
            if (_playerPDA != null)
                _playerPDA.SetActiveTab(_tabIndex);
        }
    }
}
