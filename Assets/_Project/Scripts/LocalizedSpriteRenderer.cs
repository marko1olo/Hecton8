using System;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// One localized sprite override for a specific language.
    /// </summary>
    [Serializable]
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
    public sealed class LocalizedSpriteRenderer : MonoBehaviour, ILocalizationLanguageChangedListener
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

        private void Awake()
        {
            ResolveRenderer();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            ApplyCurrentSprite();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
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
            GameLanguage language = Hecton8.Core.GlobalRegistry.Localization != null
                ? Hecton8.Core.GlobalRegistry.Localization.CurrentLanguage
                : GameLanguage.English;

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
    }
}
