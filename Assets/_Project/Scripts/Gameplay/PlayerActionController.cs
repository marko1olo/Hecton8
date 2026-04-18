// ============================================================================
// HECTON-8 — PlayerActionController.cs
// Контроллер отложенных действий игрока (еда, медикаменты).
//
// ОТВЕТСТВЕННОСТИ:
//   1. Запуск таймера действия (еда 1с, медикит 3с).
//   2. Публикация прогресса через UnityEvent (для UI).
//   3. Обработка прерываний: движение, смена инструмента, урон.
//   4. Завершение действия: вызов ConsumableItem.TryConsume().
//   5. Камерный фидбек через CameraJuiceProcessor (микро-покачивание).
//   6. Звуковой фидбек через SpatialAudioManager.
//
// ZERO GC:
//   • ITickable state machine — никаких корутин.
//   • Pre-cached strings для UI.
//   • UnityEvent для UI/Sound hooks — дизайнеры не трогают код.
// ============================================================================

using System;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Контроллер отложенных действий игрока.
    /// Управляет таймером, прерываниями и завершением действия.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerActionController : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static PlayerActionController _instance;
        /// <summary>Singleton instance for external access (e.g., FloraProjectile interrupt).</summary>
        public static PlayerActionController Instance => _instance;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Interrupt Settings ───────────────────────")]
        [Tooltip("Минимальная скорость движения для прерывания действия.")]
        [SerializeField] private float movementInterruptThreshold = 2f;

        [Header("── Camera Juice ──────────────────────────────")]
        [Tooltip("Ссылка на CameraJuiceProcessor для визуального фидбека.")]
        [SerializeField] private CameraJuiceProcessor cameraJuiceProcessor;

        [Tooltip("Интенсивность покачивания камеры во время действия.")]
        [SerializeField, Range(0f, 0.02f)] private float actionCameraBobIntensity = 0.008f;

        [Tooltip("Частота покачивания камеры (циклов в секунду).")]
        [SerializeField, Range(0.5f, 3f)] private float actionCameraBobFrequency = 1.5f;

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Звук поедания еды.")]
        [SerializeField] private AudioClip eatingSound;

        [Tooltip("Звук использования медикаментов.")]
        [SerializeField] private AudioClip healingSound;

        [Tooltip("Звук отмены действия.")]
        [SerializeField] private AudioClip cancelSound;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>Вызывается каждый кадр с прогрессом 0-1.</summary>
        public event Action<float> OnActionProgress;

        /// <summary>Вызывается при успешном завершении действия.</summary>
        public event Action<ItemData> OnActionCompleted;

        /// <summary>Вызывается при прерывании действия.</summary>
        public event Action OnActionCancelled;

        // ══════════════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════════════

        private enum ActionState
        {
            Idle,
            InProgress
        }

        private ActionState _state = ActionState.Idle;
        private ItemData _activeItem;
        private int _inventoryAnchorX = -1;  // Inventory position for atomic removal
        private int _inventoryAnchorY = -1;
        private float _actionTimer;
        private float _actionDuration;
        private int _lastToolSlotIndex = -1;
        private bool _registered;
        private float _cameraBobPhase;

        // ══════════════════════════════════════════════════════════
        //  CACHED REFERENCES
        // ══════════════════════════════════════════════════════════

        private HectonPlayerMovement _playerMovement;
        private PlayerToolManager _toolManager;
        private HectonSurvivalSystem _survivalSystem;
        private Rigidbody _playerRigidbody;
        private Transform _cachedTransform;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Идёт ли сейчас действие.</summary>
        public bool IsActionInProgress => _state == ActionState.InProgress;

        /// <summary>Текущий прогресс (0-1).</summary>
        public float Progress => _state == ActionState.InProgress && _actionDuration > 0f
            ? Mathf.Clamp01(_actionTimer / _actionDuration)
            : 0f;

        /// <summary>Активный предмет (null если нет действия).</summary>
        public ItemData ActiveItem => _activeItem;

        /// <summary>
        /// Запускает отложенное действие.
        /// </summary>
        /// <param name="item">Предмет для использования.</param>
        /// <returns>true если действие запущено.</returns>
        public bool StartAction(ItemData item)
        {
            return StartAction(item, -1, -1);
        }

        /// <summary>
        /// Запускает отложенное действие с позицией в инвентаре для атомарного удаления.
        /// </summary>
        /// <param name="item">Предмет для использования.</param>
        /// <param name="anchorX">X координата в инвентаре (-1 если не из инвентаря).</param>
        /// <param name="anchorY">Y координата в инвентаре (-1 если не из инвентаря).</param>
        /// <returns>true если действие запущено.</returns>
        public bool StartAction(ItemData item, int anchorX, int anchorY)
        {
            if (item == null) return false;
            if (_state == ActionState.InProgress) return false;
            if (item.UseDuration <= 0f)
            {
                // Мгновенное использование - удаляем из инвентаря если есть координаты
                if (anchorX >= 0 && anchorY >= 0)
                {
                    RemoveItemFromInventory(anchorX, anchorY);
                }
                ConsumableItem.TryConsume(item);
                PlayCompletionSound(item);
                return true;
            }

            _activeItem = item;
            _inventoryAnchorX = anchorX;
            _inventoryAnchorY = anchorY;
            _actionDuration = item.UseDuration;
            _actionTimer = 0f;
            _state = ActionState.InProgress;
            _cameraBobPhase = 0f;

            // Запоминаем текущий слот инструмента для прерывания
            if (_toolManager != null)
                _lastToolSlotIndex = _toolManager.CurrentSlotIndex;

            return true;
        }

        /// <summary>
        /// Принудительно отменяет текущее действие.
        /// Предмет остаётся в инвентаре (atomicity).
        /// </summary>
        public void CancelAction()
        {
            if (_state != ActionState.InProgress) return;

            _state = ActionState.Idle;
            _activeItem = null;
            _inventoryAnchorX = -1;
            _inventoryAnchorY = -1;
            _actionTimer = 0f;
            _actionDuration = 0f;

            // Очищаем камерный фидбек
            if (cameraJuiceProcessor != null)
                cameraJuiceProcessor.ClearActionBob();

            PlayCancelSound();
            OnActionCancelled?.Invoke();
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // Singleton assignment
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _cachedTransform = transform;

            // Кэшируем ссылки
            TryGetComponent(out _playerMovement);
            TryGetComponent(out _toolManager);
            TryGetComponent(out _playerRigidbody);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnEnable()
        {
            // Кэшируем SurvivalSystem
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            TryRegister();
        }

        private void OnDisable()
        {
            if (_state == ActionState.InProgress)
                CancelAction();

            TryUnregister();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (_state != ActionState.InProgress) return;

            // ── Проверка прерываний ──
            if (CheckInterrupts())
            {
                CancelAction();
                return;
            }

            // ── Обновление таймера ──
            _actionTimer += deltaTime;

            // ── Камерный фидбек (микро-покачивание) ──
            ApplyCameraJuice(deltaTime);

            // ── Публикация прогресса ──
            float progress = Mathf.Clamp01(_actionTimer / _actionDuration);
            OnActionProgress?.Invoke(progress);

            // ── Завершение ──
            if (_actionTimer >= _actionDuration)
            {
                CompleteAction();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — CAMERA JUICE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Применяет микро-покачивание камеры во время действия.
        /// Имитирует движение рук персонажа.
        /// </summary>
        private void ApplyCameraJuice(float deltaTime)
        {
            if (cameraJuiceProcessor == null) return;

            // Синусоидальное покачивание с затуханием к концу действия
            _cameraBobPhase += deltaTime * actionCameraBobFrequency * Mathf.PI * 2f;

            float progress = _actionDuration > 0f ? Mathf.Clamp01(_actionTimer / _actionDuration) : 0f;
            float fadeOut = 1f - (progress * progress); // Квадратичное затухание

            // Регистрируем микро-bob каждый кадр с затухающей интенсивностью
            float intensity = actionCameraBobIntensity * fadeOut;
            cameraJuiceProcessor.RegisterActionBob(intensity, actionCameraBobFrequency);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INTERRUPTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет условия прерывания действия.
        /// </summary>
        private bool CheckInterrupts()
        {
            // ── 1. Прерывание по движению ──
            if (_playerRigidbody != null)
            {
                Vector3 velocity = _playerRigidbody.linearVelocity;
                float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z);
                if (speed > movementInterruptThreshold)
                    return true;
            }

            // ── 2. Прерывание по смене инструмента ──
            if (_toolManager != null && _lastToolSlotIndex >= 0)
            {
                if (_toolManager.CurrentSlotIndex != _lastToolSlotIndex)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Внешний метод для прерывания по урону.
        /// Вызывается из HectonSurvivalSystem или других систем.
        /// </summary>
        public void OnDamageTaken()
        {
            if (_state == ActionState.InProgress)
                CancelAction();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — COMPLETION
        // ══════════════════════════════════════════════════════════

        private void CompleteAction()
        {
            ItemData completedItem = _activeItem;
            int anchorX = _inventoryAnchorX;
            int anchorY = _inventoryAnchorY;

            _state = ActionState.Idle;
            _activeItem = null;
            _inventoryAnchorX = -1;
            _inventoryAnchorY = -1;
            _actionTimer = 0f;
            _actionDuration = 0f;

            // Очищаем камерный фидбек
            if (cameraJuiceProcessor != null)
                cameraJuiceProcessor.ClearActionBob();

            // ── ATOMIC: Remove item from inventory ONLY on completion ──
            if (completedItem != null)
            {
                if (anchorX >= 0 && anchorY >= 0)
                {
                    RemoveItemFromInventory(anchorX, anchorY);
                }
                ConsumableItem.TryConsume(completedItem);
                PlayCompletionSound(completedItem);
            }

            OnActionCompleted?.Invoke(completedItem);
        }

        /// <summary>
        /// Removes one item from inventory at the specified position.
        /// Called only on successful action completion (atomicity).
        /// </summary>
        private void RemoveItemFromInventory(int anchorX, int anchorY)
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory == null) return;

            inventory.RemoveOneItem(anchorX, anchorY);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlayCompletionSound(ItemData item)
        {
            if (item == null) return;

            AudioClip clip = null;

            // Определяем тип звука по эффектам предмета
            if (item.integrityRestore > 0f)
            {
                clip = healingSound;
            }
            else if (item.hungerRestore > 0f || item.thirstRestore > 0f)
            {
                clip = eatingSound;
            }
            else if (item.useSound != null)
            {
                clip = item.useSound;
            }

            if (clip == null) return;

            if (SpatialAudioManager.Instance != null && _cachedTransform != null)
            {
                SpatialAudioManager.Instance.PlayAtPoint(clip, _cachedTransform.position);
            }
        }

        private void PlayCancelSound()
        {
            if (cancelSound == null) return;

            if (SpatialAudioManager.Instance != null && _cachedTransform != null)
            {
                SpatialAudioManager.Instance.PlayAtPoint(cancelSound, _cachedTransform.position);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void TryRegister()
        {
            if (_registered) return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null) return;

            tickManager.Register((ITickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered) return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _registered = false;
        }
    }
}
