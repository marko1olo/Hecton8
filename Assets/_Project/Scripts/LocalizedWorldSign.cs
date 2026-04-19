using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// Event-driven localized text bridge for world-space signs and authored TMP labels.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Localization/Localized World Sign")]
    public sealed class LocalizedWorldSign : MonoBehaviour
    {
        [Header("── References ───────────────────────────────────────────────")]
        [Tooltip("Target TMP text owner. Defaults to TMP_Text on the same GameObject.")]
        [SerializeField] private TMP_Text targetText;

        [Header("── Localization ─────────────────────────────────────────────")]
        [Tooltip("Localization table key resolved through LocalizationManager.")]
        [SerializeField] private string tableKey;

        [Tooltip("Fallback text used when the table key is missing.")]
        [SerializeField] private string fallbackText;

        [Tooltip("For signage that should stay in all-caps regardless of language.")]
        [SerializeField] private bool forceUppercase = true;

        private string _appliedText;

        private void Awake()
        {
            ResolveTargetText();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            RefreshLocalizedText();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveTargetText();
            if (!Application.isPlaying)
                RefreshLocalizedText();
        }
#endif

        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshLocalizedText();
        }

        private void RefreshLocalizedText()
        {
            if (targetText == null)
                return;

            string resolvedText = ResolveLocalizedText();
            if (forceUppercase)
                resolvedText = ZeroGCStringCache.CachedToUpperInvariant(resolvedText);

            if (string.Equals(_appliedText, resolvedText, System.StringComparison.Ordinal))
                return;

            targetText.text = resolvedText;
            _appliedText = resolvedText;
        }

        private string ResolveLocalizedText()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager != null && !string.IsNullOrWhiteSpace(tableKey))
            {
                string fallback = string.IsNullOrWhiteSpace(fallbackText) ? tableKey : fallbackText;
                return manager.GetExpandedOrFallback(manager.CurrentLanguage, tableKey, fallback);
            }

            if (!string.IsNullOrWhiteSpace(fallbackText))
                return fallbackText;

            return string.IsNullOrWhiteSpace(tableKey) ? string.Empty : tableKey;
        }

        private void ResolveTargetText()
        {
            if (targetText == null)
                TryGetComponent(out targetText);
        }
    }
}
