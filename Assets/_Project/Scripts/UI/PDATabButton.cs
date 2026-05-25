// ============================================================================
// HECTON-8 — PDATabButton.cs
// Knopka vkladki PDA.
// Upravlyaet vizualnym sostoyaniem (aktivnaya/neaktivnaya) i pereklyucheniem vkladok.
// NOTE: Sozdan kak zaglushka dlya vosstanovleniya kompilyatsii posle refaktoringa.
// ============================================================================

using TMPro;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Knopka vkladki v PDA.
    /// Otobrazhaet sostoyanie (aktivnaya/neaktivnaya) i obrabatyvaet nazhatie.
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
        private Button _button;
        private UnityAction _cachedClickAction;
        private bool _isActive;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Indeks vkladki.
        /// </summary>
        public int TabIndex => _tabIndex;

        /// <summary>
        /// Aktivna li vkladka.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Initsializiruet knopku vkladki.
        /// </summary>
        /// <param name="tabIndex">Indeks vkladki.</param>
        /// <param name="playerPDA">Ssylka na PlayerPDA dlya pereklyucheniya vkladok.</param>
        /// <param name="background">Izobrazhenie fona knopki.</param>
        /// <param name="label">Tekst metki.</param>
        /// <param name="bgActive">Tsvet fona v aktivnom sostoyanii.</param>
        /// <param name="bgInactive">Tsvet fona v neaktivnom sostoyanii.</param>
        /// <param name="textActive">Tsvet teksta v aktivnom sostoyanii.</param>
        /// <param name="textInactive">Tsvet teksta v neaktivnom sostoyanii.</param>
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
            _isActive = tabIndex == 0; // Pervaya vkladka aktivna po umolchaniyu
        }

        /// <summary>
        /// Ustanavlivaet aktivnoe sostoyanie vkladki.
        /// </summary>
        /// <param name="active">True esli vkladka aktivna.</param>
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
            _cachedClickAction = OnClick; // COLD ALLOC: UnityAction[1] — cached PDA tab click listener — owner: PDATabButton
        }

        private void OnEnable()
        {
            // Dobavlyaem obrabotchik klika esli est Button komponent
            Button button = _button;
            if (button != null)
                button.onClick.AddListener(_cachedClickAction);
        }

        private void OnDisable()
        {
            Button button = _button;
            if (button != null)
                button.onClick.RemoveListener(_cachedClickAction);
        }

        private void OnClick()
        {
            EntityCommand command = EntityCommand.CreateOpenPDATab(_tabIndex);
            ThreadSafeCommandQueue.TryEnqueue(in command);
        }
    }
}
