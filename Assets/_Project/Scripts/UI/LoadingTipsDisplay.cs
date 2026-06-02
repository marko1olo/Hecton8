using System;
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
    public sealed class LoadingTipsDisplay : MonoBehaviour, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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
        private uint _tipRandomState;
        private ILocalizationTextReadModel _cachedLocalization;
        private bool _hotSwapListenerRegistered;
        private readonly char[] _tipBuffer = new char[TipBufferCapacity]; // COLD ALLOC: char[256] — loading tip TMP staging buffer — owner: LoadingTipsDisplay

        private static readonly int[] TipKeyHashes = // COLD ALLOC: precomputed localization key hashes for loading tips — owner: LoadingTipsDisplay
        {
            LocHash.Compute(LocalizationKeys.LOADING_TIP_01),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_02),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_03),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_04),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_05),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_06),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_07),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_08),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_09),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_10),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_11),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_12),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_13),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_14),
            LocHash.Compute(LocalizationKeys.LOADING_TIP_15),
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
            CacheRegistryServicesCold();
            if (tipCanvasGroup != null)
                tipCanvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            StartTipCycle();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();

            LocalizationEvents.UnregisterLanguageListener(this);
            StopTipCycle();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        public void StartTipCycle()
        {
            if (_isActive)
                return;

            if (TipKeyHashes.Length == 0)
                return;

            _isActive = true;
            _currentTipIndex = randomOrder ? NextTipIndex(TipKeyHashes.Length) : 0;
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

            float dt = math.max(0f, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
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

        private void ShowTip(int index)
        {
            if (tipText == null || index < 0 || index >= TipKeyHashes.Length)
                return;

            ReadOnlySpan<char> tip = ResolveTipSpan(index);
            int length = CopyTipToBuffer(tip, _tipBuffer);
            tipText.SetCharArray(_tipBuffer, 0, length);
        }

        private ReadOnlySpan<char> ResolveTipSpan(int index)
        {
            ReadOnlySpan<char> fallback = index < DefaultTips.Length
                ? DefaultTips[index].AsSpan()
                : ReadOnlySpan<char>.Empty;

            ILocalizationTextReadModel manager = _cachedLocalization;
            int keyHash = index < TipKeyHashes.Length ? TipKeyHashes[index] : 0;
            return manager != null && keyHash != 0
                ? manager.GetRawSpanOrFallback(keyHash, fallback)
                : fallback;
        }

        private static int CopyTipToBuffer(ReadOnlySpan<char> tip, char[] buffer)
        {
            if (tip.IsEmpty || buffer == null || buffer.Length == 0)
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
            if (TipKeyHashes.Length == 0)
                return;

            if (randomOrder)
            {
                int newIndex = NextTipIndex(TipKeyHashes.Length);
                if (TipKeyHashes.Length > 1)
                {
                    int rerollWatchdog = TipKeyHashes.Length << 1;
                    while (newIndex == _currentTipIndex && rerollWatchdog-- > 0)
                        newIndex = NextTipIndex(TipKeyHashes.Length);

                    if (newIndex == _currentTipIndex)
                        newIndex = (_currentTipIndex + 1) % TipKeyHashes.Length;
                }

                _currentTipIndex = newIndex;
            }
            else
            {
                _currentTipIndex = (_currentTipIndex + 1) % TipKeyHashes.Length;
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
            if (_isActive)
                ShowTip(_currentTipIndex);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.LocalizationRuntime)
                return;

            _cachedLocalization = currentService as ILocalizationTextReadModel;
            if (_isActive)
                ShowTip(_currentTipIndex);
        }

        private void CacheRegistryServicesCold()
        {
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationText;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
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

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
