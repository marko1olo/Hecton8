// ============================================================================
// HECTON-8 — NarrativeDiscovery.cs
// Компонент для лорных объектов (черные ящики, КПК, обломки).
// Позволяет "открывать" лор через механику взаимодействия.
// Опционально воспроизводит AudioLog при взаимодействии.
// ============================================================================

using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    public sealed class NarrativeDiscovery : MonoBehaviour, IInteractable
    {
        [Header("── Discovery ─────────────────────────────────")]
        [Tooltip("Уникальный ID открытия (для сохранения и триггеров)")]
        [SerializeField] private string discoveryId;
        
        [Tooltip("Текст подсказки: 'Забрать КПК', 'Изучить бортовой самописец'")]
        [SerializeField] private string interactVerb = "Изучить";
        
        [Tooltip("Название объекта (для лога)")]
        [SerializeField] private string displayName = "Объект";

        [Header("── Audio Log (опционально) ───────────────────")]
        [Tooltip("Если назначен — воспроизводит аудиодневник при взаимодействии.")]
        [SerializeField] private AudioLogData linkedAudioLog;

        [Header("── Settings ──────────────────────────────────")]
        [SerializeField] private bool disableAfterDiscovery = true;
        [SerializeField] private GameObject highlightObject;

        private string _cachedInteractText;

        public string DiscoveryId => discoveryId;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private bool _registeredLifecycle;

        private void OnEnable()
        {
            RebuildCache();
            
            // Register for AI Director guidance
            NarrativeEvents.RaiseNarrativePOIRegistered(this);
            _registeredLifecycle = true;

            // Если уже открыто — отключаем (если настройка активна)
            if (disableAfterDiscovery && HectonNarrativeDirector.Instance != null)
            {
                if (HectonNarrativeDirector.Instance.HasDiscovery(discoveryId))
                {
                    gameObject.SetActive(false);
                }
            }
        }

        private void OnDisable()
        {
            if (_registeredLifecycle)
            {
                NarrativeEvents.RaiseNarrativePOIDisposed(this);
                _registeredLifecycle = false;
            }
        }

        private void RebuildCache()
        {
            _cachedInteractText = $"{interactVerb} {displayName}";
        }

        // ══════════════════════════════════════════════════════════
        //  IINTERACTABLE
        // ══════════════════════════════════════════════════════════

        public void OnHoverStart()
        {
            if (highlightObject != null) highlightObject.SetActive(true);
        }

        public void OnHoverEnd()
        {
            if (highlightObject != null) highlightObject.SetActive(false);
        }

        public void Interact(Transform interactor)
        {
            if (HectonNarrativeDirector.Instance != null && HectonNarrativeDirector.Instance.HasDiscovery(discoveryId))
            {
                // Уже открыто — но аудиолог можно переслушать
                if (linkedAudioLog != null && AudioLogSystem.Instance != null)
                    AudioLogSystem.Instance.PlayLog(linkedAudioLog);

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Narrative] '{discoveryId}' already discovered.");
                #endif
                return;
            }

            // Оповещаем систему
            NarrativeEvents.RaiseDiscoveryMade(discoveryId);

            // Воспроизводим аудиолог если назначен
            if (linkedAudioLog != null && AudioLogSystem.Instance != null)
                AudioLogSystem.Instance.PlayLog(linkedAudioLog);

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Discovery made: {discoveryId} ({displayName})");
            #endif

            if (disableAfterDiscovery)
            {
                gameObject.SetActive(false);
            }
        }

        public string GetInteractText() => _cachedInteractText;

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(discoveryId))
            {
                discoveryId = gameObject.name.ToLower().Replace(" ", "_");
            }
            RebuildCache();
        }
        #endif
    }
}
