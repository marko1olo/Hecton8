// ============================================================================
// HECTON-8 — AudioLogPickup.cs
// Интерактивный объект в мире — аудиодневник колонии.
// ============================================================================

using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Interaction;
using UnityEngine;

namespace Hecton8.Narrative
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AudioLogPickup : MonoBehaviour, IInteractable
    {
        private const string DefaultPlaybackVerbRu = "Воспроизвести запись";
        private const string DefaultPlaybackVerbEn = "Play Log";
        private const string DefaultTextVerbRu = "Открыть запись";
        private const string DefaultTextVerbEn = "Open Log";
        private const string DefaultArchiveVerbRu = "Открыть архив";
        private const string DefaultArchiveVerbEn = "Open Archive";

        [Header("── Audio Log ───────────────────────────────")]
        [Tooltip("Данные аудиодневника.")]
        [SerializeField] private AudioLogData logData;

        [Tooltip("Текст подсказки взаимодействия.")]
        [SerializeField] private string interactVerb = DefaultPlaybackVerbRu;

        [Header("── Behaviour ───────────────────────────────")]
        [Tooltip("Деактивировать объект после первого взаимодействия.")]
        [SerializeField] private bool deactivateAfterPickup;

        [Tooltip("Подсветка при наведении.")]
        [SerializeField] private GameObject highlightObject;

        private string _cachedInteractText;
        private bool _alreadyDiscovered;

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            _alreadyDiscovered = false;

            if (logData != null && AudioLogSystem.Instance != null)
            {
                _alreadyDiscovered = AudioLogSystem.Instance.IsDiscovered(logData.logId);

                if (_alreadyDiscovered && deactivateAfterPickup)
                {
                    BuildCache();
                    gameObject.SetActive(false);
                    return;
                }
            }

            BuildCache();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;

            if (highlightObject != null)
                highlightObject.SetActive(false);
        }

        private void BuildCache()
        {
            if (logData == null)
            {
                _cachedInteractText = ResolveInteractVerb();
                return;
            }

            string title = logData.DisplayTitleOrFallback;
            string resolvedVerb = ResolveInteractVerb();
            if (_alreadyDiscovered)
            {
                _cachedInteractText = resolvedVerb + ": " + title + " " +
                                      ResolveLocalized(LocalizationKeys.INTERACT_REPLAY_SUFFIX, "(Replay)");
                return;
            }

            _cachedInteractText = resolvedVerb + ": " + title;
        }

        private string ResolveInteractVerb()
        {
            if (HasCustomInteractVerb())
                return interactVerb;

            if (logData == null)
                return ResolveLocalized(LocalizationKeys.INTERACT_PLAY_LOG, DefaultPlaybackVerbEn);

            if (logData.IsTextOnlyPlayback)
                return ResolveLocalized(LocalizationKeys.INTERACT_OPEN_LOG, DefaultTextVerbEn);

            if (!logData.HasPlaybackPayload && logData.HasVisibleContent)
                return ResolveLocalized(LocalizationKeys.INTERACT_OPEN_ARCHIVE, DefaultArchiveVerbEn);

            return ResolveLocalized(LocalizationKeys.INTERACT_PLAY_LOG, DefaultPlaybackVerbEn);
        }

        public void OnHoverStart()
        {
            if (highlightObject != null)
                highlightObject.SetActive(true);
        }

        public void OnHoverEnd()
        {
            if (highlightObject != null)
                highlightObject.SetActive(false);
        }

        public void Interact(Transform interactor)
        {
            if (logData == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[AudioLogPickup] No AudioLogData assigned on {name}.");
#endif
                return;
            }

            AudioLogSystem system = AudioLogSystem.Instance;
            if (system == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[AudioLogPickup] AudioLogSystem.Instance is null.");
#endif
                return;
            }

            system.PlayLog(logData);
            _alreadyDiscovered = true;
            BuildCache();

            if (deactivateAfterPickup)
                gameObject.SetActive(false);
        }

        public string GetInteractText() => _cachedInteractText;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(interactVerb))
                interactVerb = DefaultPlaybackVerbRu;

            BuildCache();
        }
#endif

        private void HandleLanguageChanged(GameLanguage language)
        {
            BuildCache();
        }

        private bool HasCustomInteractVerb()
        {
            if (string.IsNullOrWhiteSpace(interactVerb))
                return false;

            return !IsLegacyDefaultVerb(interactVerb);
        }

        private static bool IsLegacyDefaultVerb(string value)
        {
            return string.Equals(value, DefaultPlaybackVerbRu, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultPlaybackVerbEn, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultTextVerbRu, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultTextVerbEn, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultArchiveVerbRu, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultArchiveVerbEn, System.StringComparison.Ordinal);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        internal void ConfigureRecoveryPickup(AudioLogData data, bool deactivateAfterUse)
        {
            logData = data;
            interactVerb = string.Empty;
            deactivateAfterPickup = deactivateAfterUse;
            highlightObject = null;
            _alreadyDiscovered = false;
            BuildCache();
        }
    }
}
