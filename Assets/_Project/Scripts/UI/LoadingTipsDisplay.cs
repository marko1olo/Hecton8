using Hecton.Localization;
using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Displays rotating gameplay tips during loading screens.
    /// Tips cycle every N seconds with fade transitions.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Loading Tips Display")]
    public sealed class LoadingTipsDisplay : MonoBehaviour, ITickable, IUpdatable
    {
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

        private bool _registered;
        private bool _isActive;
        private int _currentTipIndex;
        private float _tipTimer;
        private float _fadeTimer;
        private bool _isFadingIn;
        private bool _isFadingOut;
        private string[] _tips;

        private static readonly string[] TipKeys = // COLD ALLOC: localization keys for loading tips — owner: LoadingTipsDisplay
        {
            LocalizationKeys.LOADING_TIP_01,
            LocalizationKeys.LOADING_TIP_02,
            LocalizationKeys.LOADING_TIP_03,
            LocalizationKeys.LOADING_TIP_04,
            LocalizationKeys.LOADING_TIP_05,
            LocalizationKeys.LOADING_TIP_06,
            LocalizationKeys.LOADING_TIP_07,
            LocalizationKeys.LOADING_TIP_08,
            LocalizationKeys.LOADING_TIP_09,
            LocalizationKeys.LOADING_TIP_10,
            LocalizationKeys.LOADING_TIP_11,
            LocalizationKeys.LOADING_TIP_12,
            LocalizationKeys.LOADING_TIP_13,
            LocalizationKeys.LOADING_TIP_14,
            LocalizationKeys.LOADING_TIP_15,
        };

        private static readonly string[] DefaultTips = // COLD ALLOC: fallback tips — owner: LoadingTipsDisplay
        {
            "Scan unknown objects to unlock blueprints and research data.",
            "Save frequently before risky dives or major construction changes.",
            "Keep your loadout aligned with cargo before committing to depth.",
            "Repair critical infrastructure before exploring new zones.",
            "Use quick slots (1-4) to arm tools without opening inventory.",
            "PDA (TAB) provides mission logs, blueprints, and scan data.",
            "Fabricators require power and raw materials to craft items.",
            "Oxygen levels drop faster at greater depths, plan your route.",
            "Flashlight battery depletes over time, conserve power in lit areas.",
            "Suit integrity degrades from fauna contact and pressure damage.",
            "Base modules require power grid connection to function.",
            "Scan flora and fauna to complete biological database entries.",
            "Some resources are depth-locked, upgrade the suit before deep dives.",
            "Crafting stations unlock advanced recipes as you progress.",
            "Emergency oxygen stations provide temporary life support.",
        };

        private void Awake()
        {
            LoadTips();
            if (tipCanvasGroup != null)
                tipCanvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            TryRegister();

            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            StartTipCycle();
        }

        private void OnDisable()
        {
            TryUnregister();

            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            StopTipCycle();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

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

        public void Tick(float dt)
        {
            if (!_isActive || tipText == null || tipCanvasGroup == null)
                return;

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

            _tipTimer += dt;
            if (_tipTimer >= tipDuration)
            {
                _isFadingOut = true;
                _fadeTimer = 0f;
            }
        }

        private void LoadTips()
        {
            if (_tips == null || _tips.Length != TipKeys.Length)
            {
                // COLD ALLOC: string[15] — resolved loading tips cache — owner: LoadingTipsDisplay
                _tips = new string[TipKeys.Length];
            }

            LocalizationManager manager = LocalizationManager.Instance;
            for (int i = 0; i < TipKeys.Length; i++)
            {
                string fallback = i < DefaultTips.Length ? DefaultTips[i] : string.Empty;
                _tips[i] = manager != null
                    ? manager.GetOrFallback(manager.CurrentLanguage, TipKeys[i], fallback)
                    : fallback;
            }
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
                int newIndex = Random.Range(0, _tips.Length);
                if (_tips.Length > 1)
                {
                    int rerollWatchdog = _tips.Length << 1;
                    while (newIndex == _currentTipIndex && rerollWatchdog-- > 0)
                        newIndex = Random.Range(0, _tips.Length);

                    if (newIndex == _currentTipIndex)
                        newIndex = (_currentTipIndex + 1) % _tips.Length;
                }

                _currentTipIndex = newIndex;
            }
            else
            {
                _currentTipIndex = (_currentTipIndex + 1) % _tips.Length;
            }

            ShowTip(_currentTipIndex);
            _isFadingIn = true;
            _fadeTimer = 0f;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            LoadTips();

            if (_isActive)
                ShowTip(_currentTipIndex);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
