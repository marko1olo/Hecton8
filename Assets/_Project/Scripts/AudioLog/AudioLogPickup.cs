// ============================================================================
// HECTON-8 — AudioLogPickup.cs
// Интерактивный объект в мире — аудиодневник колонии.
// Реализует IInteractable. При взаимодействии: обнаруживает + воспроизводит лог.
//
// Лор: датапады Chen_M, записи капитана, аудиозаписи в терминалах.
// ============================================================================

using Hecton8.Core;
using Hecton8.Interaction;
using UnityEngine;

namespace Hecton8.Narrative
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AudioLogPickup : MonoBehaviour, IInteractable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Audio Log ───────────────────────────────")]
        [Tooltip("Данные аудиодневника.")]
        [SerializeField] private AudioLogData logData;

        [Tooltip("Текст подсказки взаимодействия.")]
        [SerializeField] private string interactVerb = "Воспроизвести запись";

        [Header("── Behaviour ───────────────────────────────")]
        [Tooltip("Деактивировать объект после первого взаимодействия.")]
        [SerializeField] private bool deactivateAfterPickup = false;

        [Tooltip("Подсветка при наведении.")]
        [SerializeField] private GameObject highlightObject;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private string _cachedInteractText;
        private bool _alreadyDiscovered;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            BuildCache();

            // Проверяем уже обнаружен ли лог
            if (logData != null && AudioLogSystem.Instance != null)
            {
                _alreadyDiscovered = AudioLogSystem.Instance.IsDiscovered(logData.logId);

                if (_alreadyDiscovered && deactivateAfterPickup)
                    gameObject.SetActive(false);
            }
        }

        private void BuildCache()
        {
            if (logData != null)
                _cachedInteractText = $"{interactVerb}: {logData.displayTitle}";
            else
                _cachedInteractText = interactVerb;
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

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

            if (deactivateAfterPickup)
                gameObject.SetActive(false);
        }

        public string GetInteractText() => _cachedInteractText;

#if UNITY_EDITOR
        private void OnValidate()
        {
            BuildCache();
        }
#endif
    }
}
