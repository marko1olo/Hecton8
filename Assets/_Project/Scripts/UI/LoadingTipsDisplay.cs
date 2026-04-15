using TMPro;
using UnityEngine;
using Hecton8.Core;
using Hecton.Localization;

namespace Hecton8.UI
{
    /// <summary>
    /// Displays rotating gameplay tips during loading screens (Subnautica-style).
    /// Tips cycle every N seconds with smooth fade transitions.
    /// Zero-GC: ITickable, cached strings, no LINQ.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Loading Tips Display")]
    public sealed class LoadingTipsDisplay : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== UI REFERENCES ===")]
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private CanvasGroup tipCanvasGroup;

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Time to display each tip (seconds)")]
        private float tipDuration = 5f;

        [SerializeField, Tooltip("Fade in/out duration (seconds)")]
        private float fadeDuration = 0.5f;

        [SerializeField, Tooltip("Show tips in random order")]
        private bool randomOrder = true;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private bool _registered;
        private bool _isActive;
        private int _currentTipIndex;
        private float _tipTimer;
        private float _fadeTimer;
        private bool _isFadingIn;
        private bool _isFadingOut;
        private string[] _tips; // COLD ALLOC: loading tips cache

        private static readonly string[] DefaultTips = // COLD ALLOC: fallback tips
        {
            "Scan unknown objects to unlock blueprints and research data.",
            "Save frequently before risky dives or major construction changes.",
            "Keep your loadout aligned with cargo before committing to depth.",
            "Repair critical infrastructure before exploring new zones.",
            "Use quick slots (1-4) to arm tools without opening inventory.",
            "PDA (TAB) provides mission logs, blueprints, and scan data.",
            "Fabricators require power and raw materials to craft items.",
            "Oxygen levels drop faster at greater depths — plan your route.",
            "Flashlight battery depletes over time — conserve power in lit areas.",
            "Suit integrity degrades from fauna contact and pressure damage.",
            "Base modules require power grid connection to function.",
            "Scan flora and fauna to complete biological database entries.",
            "Some resources are depth-locked — upgrade suit before deep dives.",
            "Crafting stations unlock advanced recipes as you progress.",
            "Emergency oxygen stations provide temporary life support.",
        };

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            LoadTips();
            if (tipCanvasGroup != null)
                tipCanvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }

            StartTipCycle();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            StopTipCycle();
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        public void StartTipCycle()
        {
            if (_isActive)
                return;

            if (_tips == null || _tips.Length == 0)
                LoadTips();

            if (_tips == null || _tips.Length == 0)
                return;

            _isActive = true;
            _currentTipIndex = randomOrder ? Random.Range(0, _tips.Length) : 0;
            _tipTimer = 0f;
            _fadeTimer = 0f;
            _isFadingIn = true;
            _isFadingOut = false;

            ShowTip(_currentTipIndex);
        }

        public void StopTipCycle()
        {
            _isActive = false;
            _isFadingIn = false;
            _isFadingOut = false;

            if (tipCanvasGroup != null)
                tipCanvasGroup.alpha = 0f;
        }

        // ══════════════════════════════════════════════════════════
        // ITICKABLE
        // ══════════════════════════════════════════════════════════

        public void Tick(float dt)
        {
            if (!_isActive || tipText == null || tipCanvasGroup == null)
                return;

            // Handle fade in
            if (_isFadingIn)
            {
                _fadeTimer += dt;
                float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
                tipCanvasGroup.alpha = t;

                if (t >= 1f)
                {
                    _isFadingIn = false;
                    _fadeTimer = 0f;
                    _tipTimer = 0f;
                }

                return;
            }

            // Handle fade out
            if (_isFadingOut)
            {
                _fadeTimer += dt;
                float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
                tipCanvasGroup.alpha = 1f - t;

                if (t >= 1f)
                {
                    _isFadingOut = false;
                    _fadeTimer = 0f;
                    NextTip();
                }

                return;
            }

            // Handle tip display duration
            _tipTimer += dt;
            if (_tipTimer >= tipDuration)
            {
                _isFadingOut = true;
                _fadeTimer = 0f;
            }
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void LoadTips()
        {
            // Localized loading tips are not implemented yet.
            // Keep the runtime path deterministic and avoid dependency on localization startup order.
            _tips = DefaultTips;
        }

        private void ShowTip(int index)
        {
            if (tipText == null || _tips == null || index < 0 || index >= _tips.Length)
                return;

            tipText.SetText(_tips[index]);
        }

        private void NextTip()
        {
            if (_tips == null || _tips.Length == 0)
                return;

            if (randomOrder)
            {
                // Random tip (avoid repeating same tip)
                int newIndex = Random.Range(0, _tips.Length);
                if (_tips.Length > 1)
                {
                    while (newIndex == _currentTipIndex)
                        newIndex = Random.Range(0, _tips.Length);
                }
                _currentTipIndex = newIndex;
            }
            else
            {
                // Sequential tips
                _currentTipIndex = (_currentTipIndex + 1) % _tips.Length;
            }

            ShowTip(_currentTipIndex);
            _isFadingIn = true;
            _fadeTimer = 0f;
        }
    }
}
