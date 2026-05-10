// ============================================================================
// HECTON-8 — PDAInventoryFilterButton.cs
// Knopka filtra inventarya v PDA.
// Upravlyaet vizualnym sostoyaniem i filtratsiey predmetov.
// NOTE: Sozdan kak zaglushka dlya vosstanovleniya kompilyatsii posle refaktoringa.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Knopka filtra inventarya v PDA.
    /// Otobrazhaet sostoyanie (aktivnaya/neaktivnaya) i primenyaet filtr pri nazhatii.
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
        private Button _button;
        private UnityAction _clickAction;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Filtr, kotoryy primenyaet eta knopka.
        /// </summary>
        internal InventoryViewFilter Filter => _filter;

        /// <summary>
        /// Aktiven li filtr.
        /// </summary>
        public bool IsActive => _isActive;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Initsializiruet knopku filtra.
        /// </summary>
        /// <param name="inventoryTab">Ssylka na vkladku inventarya.</param>
        /// <param name="filter">Filtr, kotoryy primenyaet knopka.</param>
        /// <param name="background">Izobrazhenie fona knopki.</param>
        /// <param name="label">Tekst metki.</param>
        /// <param name="bgActive">Tsvet fona v aktivnom sostoyanii.</param>
        /// <param name="bgInactive">Tsvet fona v neaktivnom sostoyanii.</param>
        /// <param name="textActive">Tsvet teksta v aktivnom sostoyanii.</param>
        /// <param name="textInactive">Tsvet teksta v neaktivnom sostoyanii.</param>
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
        /// Ustanavlivaet aktivnoe sostoyanie filtra.
        /// </summary>
        /// <param name="active">True esli filtr aktiven.</param>
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

        private void Awake()
        {
            TryGetComponent(out _button);
            _clickAction = OnClick; // COLD ALLOC: UnityAction[1] - cached PDA inventory filter click listener - owner: PDAInventoryFilterButton
        }

        private void OnEnable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(_clickAction);
                _button.onClick.AddListener(_clickAction);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(_clickAction);
        }

        private void OnClick()
        {
            if (_inventoryTab != null)
                _inventoryTab.SetFilter(_filter);
        }
    }
}
