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
    public sealed class AudioLogPickup : MonoBehaviour, IInteractable, ILocalizationLanguageChangedListener
    {
        private const int MaxRegisteredPickupTemplates = 64;
        private const string DefaultPlaybackVerbRu = "Воспроизвести запись";
        private const string DefaultPlaybackVerbEn = "Play Log";
        private const string DefaultTextVerbRu = "Открыть запись";
        private const string DefaultTextVerbEn = "Open Log";
        private const string DefaultArchiveVerbRu = "Открыть архив";
        private const string DefaultArchiveVerbEn = "Open Archive";

        // COLD ALLOC: RegistryBucket<AudioLogPickup>[64] - active pickup templates for procedural lore lookup - owner: AudioLogPickup
        private static readonly RegistryBucket<AudioLogPickup> _registeredPickupTemplates = new RegistryBucket<AudioLogPickup>(MaxRegisteredPickupTemplates);

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
        private bool _pickupTemplateRegistered;

        internal static int RegisteredPickupTemplateCount => _registeredPickupTemplates.Count;

        private void OnEnable()
        {
            TryRegisterPickupTemplate();
            LocalizationEvents.RegisterLanguageListener(this);
            _alreadyDiscovered = false;

            if (logData != null && Hecton8.Core.GlobalRegistry.AudioLogs != null)
            {
                _alreadyDiscovered = Hecton8.Core.GlobalRegistry.AudioLogs.IsDiscovered(logData.logId);

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
            TryUnregisterPickupTemplate();
            LocalizationEvents.UnregisterLanguageListener(this);

            if (highlightObject != null)
                highlightObject.SetActive(false);
        }

        private void OnDestroy()
        {
            TryUnregisterPickupTemplate();
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

            AudioLogSystem system = Hecton8.Core.GlobalRegistry.AudioLogs;
            if (system == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[AudioLogPickup] Hecton8.Core.GlobalRegistry.AudioLogs is null.");
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

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


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
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
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

        /// <summary>
        /// Resolves the first active audio-log pickup template without scene search.
        /// </summary>
        /// <param name="template">Resolved pickup template.</param>
        /// <returns>True when an active template is registered.</returns>
        internal static bool TryGetRegisteredTemplate(out AudioLogPickup template)
        {
            AudioLogPickup[] rawArray = _registeredPickupTemplates.RawArray;
            int registeredCount = _registeredPickupTemplates.Count;
            for (int i = 0; i < registeredCount; i++)
            {
                AudioLogPickup pickup = rawArray[i];
                if (pickup == null || !pickup.isActiveAndEnabled || pickup.gameObject == null)
                    continue;

                template = pickup;
                return true;
            }

            template = null;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPickupTemplateRegistry()
        {
            _registeredPickupTemplates.Clear();
        }

        private void TryRegisterPickupTemplate()
        {
            if (_pickupTemplateRegistered)
                return;

            _pickupTemplateRegistered = _registeredPickupTemplates.TryRegister(this);
        }

        private void TryUnregisterPickupTemplate()
        {
            if (!_pickupTemplateRegistered)
                return;

            _registeredPickupTemplates.Unregister(this);
            _pickupTemplateRegistered = false;
        }
    }
}
