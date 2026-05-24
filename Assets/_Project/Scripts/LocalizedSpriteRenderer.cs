using System;
using Hecton8.Core;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// One localized sprite override for a specific language.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct LocalizedSpriteVariant
    {
        [Tooltip("Language for this sprite variant.")]
        [SerializeField] private GameLanguage language;

        [Tooltip("Sprite override for the selected language.")]
        [SerializeField] private Sprite sprite;

        /// <summary>
        /// Language bound to this sprite variant.
        /// </summary>
        public GameLanguage Language => language;

        /// <summary>
        /// Sprite override payload.
        /// </summary>
        public Sprite Sprite => sprite;
    }

    /// <summary>
    /// Event-driven sprite localization for world signs, posters, decals, and warning art.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Hecton/Localization/Localized Sprite Renderer")]
    public sealed class LocalizedSpriteRenderer : MonoBehaviour, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        [Header("References")]
        [Tooltip("Target SpriteRenderer. Defaults to the component on the same GameObject.")]
        [SerializeField] private SpriteRenderer targetRenderer;

        [Header("Localization")]
        [Tooltip("Default sprite used when no localized override exists.")]
        [SerializeField] private Sprite defaultSprite;

        [Tooltip("Optional language-specific sprite overrides.")]
        [SerializeField] private LocalizedSpriteVariant[] variants;

        private Sprite _appliedSprite;
        private LocalizationManager _localization;
        private bool _hotSwapRegistered;

        private void Awake()
        {
            ResolveRenderer();
            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            ApplyCurrentSprite();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveRenderer();
            if (!Application.isPlaying)
                ApplyCurrentSprite();
        }
#endif

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            ApplyCurrentSprite();
        }

        private void ApplyCurrentSprite()
        {
            if (targetRenderer == null)
                return;

            Sprite resolved = ResolveSpriteForCurrentLanguage();
            if (_appliedSprite == resolved)
                return;

            targetRenderer.sprite = resolved;
            _appliedSprite = resolved;
        }

        private Sprite ResolveSpriteForCurrentLanguage()
        {
            LocalizationManager manager = _localization;
            GameLanguage language = manager != null ? manager.CurrentLanguage : GameLanguage.English;

            if (variants != null)
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    if (variants[i].Language == language && variants[i].Sprite != null)
                        return variants[i].Sprite;
                }
            }

            if (defaultSprite != null)
                return defaultSprite;

            return targetRenderer != null ? targetRenderer.sprite : null;
        }

        private void ResolveRenderer()
        {
            if (targetRenderer == null)
                TryGetComponent(out targetRenderer);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.LocalizationRuntime)
                return;

            _localization = currentService as LocalizationManager;
            ApplyCurrentSprite();
        }

        private void CacheRegistryServicesCold()
        {
            _localization = LocalizationManager.ActiveRuntimeInstance;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}
