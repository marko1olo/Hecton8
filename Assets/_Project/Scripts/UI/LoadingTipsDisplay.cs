using Hecton.Localization;
using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Displays rotating gameplay tips during loading screens.
    /// Tips cycle every N seconds with fade transitions.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Loading Tips Display")]
    public sealed class LoadingTipsDisplay : MonoBehaviour, ILateFrameTickable, ILocalizationLanguageChangedListener
    {
        private const int TipBufferCapacity = 256;

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
        private uint _tipRandomState;
        private readonly char[] _tipBuffer = new char[TipBufferCapacity]; // COLD ALLOC: char[256] — loading tip TMP staging buffer — owner: LoadingTipsDisplay

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
            _tipRandomState = MixSeed(unchecked((uint)EntityId.ToULong(GetEntityId())));
            LoadTips();
            if (tipCanvasGroup != null)
                tipCanvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            StartTipCycle();
        }

        private void OnDisable()
        {
            TryUnregister();

            LocalizationEvents.UnregisterLanguageListener(this);
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
            _currentTipIndex = randomOrder ? NextTipIndex(_tips.Length) : 0;
            _tipTimer = 0f;
            _fadeTimer = 0f;
            _isFadingIn = true;
            _isFadingOut = false;

            ShowTip(_currentTipIndex);
            RefreshTickRegistration();
        }

        public void StopTipCycle()
        {
            _isActive = false;
            _isFadingIn = false;
            _isFadingOut = false;
            _tipTimer = 0f;
            _fadeTimer = 0f;

            if (tipCanvasGroup != null)
                tipCanvasGroup.alpha = 0f;

            if (tipText != null)
                tipText.SetCharArray(_tipBuffer, 0, 0);

            RefreshTickRegistration();
        }

        public void LateFrameTick()
        {
            if (!_isActive || tipText == null || tipCanvasGroup == null)
                return;

            float dt = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            if (_isFadingIn)
            {
                _fadeTimer += dt;
                float t = ResolveFadeT();
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
                float t = ResolveFadeT();
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

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
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

            int length = CopyTipToBuffer(_tips[index], _tipBuffer);
            tipText.SetCharArray(_tipBuffer, 0, length);
        }

        private static int CopyTipToBuffer(string tip, char[] buffer)
        {
            if (string.IsNullOrEmpty(tip) || buffer == null || buffer.Length == 0)
                return 0;

            int length = math.min(tip.Length, buffer.Length);
            bool truncated = tip.Length > buffer.Length && length >= 3;
            int copyLength = truncated ? length - 3 : length;
            for (int i = 0; i < copyLength; i++)
                buffer[i] = tip[i];

            if (truncated)
            {
                buffer[length - 3] = '.';
                buffer[length - 2] = '.';
                buffer[length - 1] = '.';
            }

            return length;
        }

        private void NextTip()
        {
            if (_tips == null || _tips.Length == 0)
                return;

            if (randomOrder)
            {
                int newIndex = NextTipIndex(_tips.Length);
                if (_tips.Length > 1)
                {
                    int rerollWatchdog = _tips.Length << 1;
                    while (newIndex == _currentTipIndex && rerollWatchdog-- > 0)
                        newIndex = NextTipIndex(_tips.Length);

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

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

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

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void RefreshTickRegistration()
        {
            if (_isActive)
            {
                TryRegister();
                return;
            }

            TryUnregister();
        }

        private float ResolveFadeT()
        {
            float safeDuration = math.max(0.0001f, fadeDuration);
            return math.saturate(_fadeTimer / safeDuration);
        }

        private int NextTipIndex(int length)
        {
            if (length <= 1)
                return 0;

            uint state = _tipRandomState;
            if (state == 0u)
                state = 0xA341316Cu;

            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _tipRandomState = state != 0u ? state : 0x9E3779B9u;
            return (int)(_tipRandomState % (uint)length);
        }

        private static uint MixSeed(uint seed)
        {
            unchecked
            {
                seed ^= 0x9E3779B9u;
                seed ^= seed >> 16;
                seed *= 0x7FEB352Du;
                seed ^= seed >> 15;
                seed *= 0x846CA68Bu;
                seed ^= seed >> 16;
                return seed != 0u ? seed : 0xA341316Cu;
            }
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
