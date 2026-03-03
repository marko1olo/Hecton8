namespace Hecton.Interaction
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Сканирует мир рейкастом из центра камеры каждые <see cref="raycastInterval"/> секунд.
    /// При обнаружении IInteractable — вызывает ховер и публикует события для UI.
    /// При нажатии клавиши — вызывает Interact.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteraction : MonoBehaviour
    {
        // ─────────────────────── Inspector ───────────────────────
        [Header("Raycast")]
        [SerializeField] private float   reachDistance    = 3.5f;
        [SerializeField] private float   raycastInterval  = 0.2f;
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("Input")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("References")]
        [Tooltip("Если пусто — возьмёт Camera.main")]
        [SerializeField] private Camera  playerCamera;

        // ─────────────────────── Events (для UI и других систем) ──
        /// <summary>Найден новый интерактивный объект. Аргумент — текст подсказки.</summary>
        public event Action<string>        OnTargetFound;
        /// <summary>Объект потерян (курсор ушёл или объект уничтожен).</summary>
        public event Action                OnTargetLost;
        /// <summary>Произведено взаимодействие.</summary>
        public event Action<IInteractable> OnInteracted;

        // ─────────────────────── State ───────────────────────────
        private IInteractable _currentTarget;
        private Component     _targetRef;   // для проверки Unity-null
        private float         _timer;

        // ═════════════════════════════════════════════════════════
        #region Lifecycle

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (playerCamera == null)
                Debug.LogError("[PlayerInteraction] Camera не назначена и Camera.main == null.", this);
        }

        private void Update()
        {
            // Если объект уничтожен между кадрами — сбросить
            ValidateTarget();

            // Периодический рейкаст
            _timer += Time.deltaTime;
            if (_timer >= raycastInterval)
            {
                _timer = 0f;
                Scan();
            }

            // Ввод
            if (_currentTarget != null && Input.GetKeyDown(interactKey))
                PerformInteraction();
        }

        private void OnDisable()
        {
            ClearTarget();
        }

        #endregion
        // ═════════════════════════════════════════════════════════
        #region Core Logic

        private void Scan()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, reachDistance,
                                interactionMask, QueryTriggerInteraction.Ignore))
            {
                // Ищем IInteractable на самом объекте и выше по иерархии
                var interactable = hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null)
                {
                    // Тот же объект — ничего не делать
                    if (ReferenceEquals(interactable, _currentTarget))
                        return;

                    // Новый объект — переключить
                    ClearTarget();
                    SetTarget(interactable);
                    return;
                }
            }

            // Ничего не нашли
            if (_currentTarget != null)
                ClearTarget();
        }

        private void SetTarget(IInteractable target)
        {
            _currentTarget = target;
            _targetRef      = target as Component;

            _currentTarget.OnHoverStart();
            OnTargetFound?.Invoke(_currentTarget.GetInteractText());
        }

        private void ClearTarget()
        {
            // Если объект ещё жив — уведомить о конце ховера
            if (_currentTarget != null && _targetRef != null)
                _currentTarget.OnHoverEnd();

            _currentTarget = null;
            _targetRef     = null;
            OnTargetLost?.Invoke();
        }

        private void PerformInteraction()
        {
            var target = _currentTarget;

            OnInteracted?.Invoke(target);
            target.Interact(transform);

            // Объект мог самоуничтожиться в Interact()
            if (_targetRef == null)
                ClearTarget();
        }

        private void ValidateTarget()
        {
            // Component уничтожен, но интерфейс-ссылка ещё жива
            if (_currentTarget != null && _targetRef == null)
                ClearTarget();
        }

        #endregion
        // ═════════════════════════════════════════════════════════
        #region Debug

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (playerCamera == null) return;
            Gizmos.color  = Color.cyan;
            var ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Gizmos.DrawRay(ray.origin, ray.direction * reachDistance);
        }
        #endif

        #endregion
    }
}