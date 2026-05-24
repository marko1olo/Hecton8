// ============================================================================
// HECTON-8 â€” PlayerActionController.cs
// ÐšÐ¾Ð½Ñ‚Ñ€Ð¾Ð»Ð»ÐµÑ€ Ð¾Ñ‚Ð»Ð¾Ð¶ÐµÐ½Ð½Ñ‹Ñ… Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ð¹ Ð¸Ð³Ñ€Ð¾ÐºÐ° (ÐµÐ´Ð°, Ð¼ÐµÐ´Ð¸ÐºÐ°Ð¼ÐµÐ½Ñ‚Ñ‹).
//
// ÐžÐ¢Ð’Ð•Ð¢Ð¡Ð¢Ð’Ð•ÐÐÐžÐ¡Ð¢Ð˜:
//   1. Ð—Ð°Ð¿ÑƒÑÐº Ñ‚Ð°Ð¹Ð¼ÐµÑ€Ð° Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ (ÐµÐ´Ð° 1Ñ, Ð¼ÐµÐ´Ð¸ÐºÐ¸Ñ‚ 3Ñ).
//   2. ÐŸÑƒÐ±Ð»Ð¸ÐºÐ°Ñ†Ð¸Ñ Ð¿Ñ€Ð¾Ð³Ñ€ÐµÑÑÐ° Ñ‡ÐµÑ€ÐµÐ· SignalBus (Ð´Ð»Ñ UI).
//   3. ÐžÐ±Ñ€Ð°Ð±Ð¾Ñ‚ÐºÐ° Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸Ð¹: Ð´Ð²Ð¸Ð¶ÐµÐ½Ð¸Ðµ, ÑÐ¼ÐµÐ½Ð° Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð°, ÑƒÑ€Ð¾Ð½.
//   4. Ð—Ð°Ð²ÐµÑ€ÑˆÐµÐ½Ð¸Ðµ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ: Ð²Ñ‹Ð·Ð¾Ð² ConsumableItem.TryConsume().
//   5. ÐšÐ°Ð¼ÐµÑ€Ð½Ñ‹Ð¹ Ñ„Ð¸Ð´Ð±ÐµÐº Ñ‡ÐµÑ€ÐµÐ· CameraJuiceProcessor (Ð¼Ð¸ÐºÑ€Ð¾-Ð¿Ð¾ÐºÐ°Ñ‡Ð¸Ð²Ð°Ð½Ð¸Ðµ).
//   6. Ð—Ð²ÑƒÐºÐ¾Ð²Ð¾Ð¹ Ñ„Ð¸Ð´Ð±ÐµÐº Ñ‡ÐµÑ€ÐµÐ· SpatialAudioManager.
//
// ZERO GC:
//   â€¢ ITickable state machine â€” Ð½Ð¸ÐºÐ°ÐºÐ¸Ñ… ÐºÐ¾Ñ€ÑƒÑ‚Ð¸Ð½.
//   â€¢ Pre-cached strings Ð´Ð»Ñ UI.
//   â€¢ SignalBus Ð´Ð»Ñ UI/Sound hooks â€” Ð´Ð¸Ð·Ð°Ð¹Ð½ÐµÑ€Ñ‹ Ð½Ðµ Ñ‚Ñ€Ð¾Ð³Ð°ÑŽÑ‚ ÐºÐ¾Ð´.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Inventory;
using Hecton8.Items;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// ÐšÐ¾Ð½Ñ‚Ñ€Ð¾Ð»Ð»ÐµÑ€ Ð¾Ñ‚Ð»Ð¾Ð¶ÐµÐ½Ð½Ñ‹Ñ… Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ð¹ Ð¸Ð³Ñ€Ð¾ÐºÐ°.
    /// Ð£Ð¿Ñ€Ð°Ð²Ð»ÑÐµÑ‚ Ñ‚Ð°Ð¹Ð¼ÐµÑ€Ð¾Ð¼, Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸ÑÐ¼Ð¸ Ð¸ Ð·Ð°Ð²ÐµÑ€ÑˆÐµÐ½Ð¸ÐµÐ¼ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerActionController : MonoBehaviour, ITickable, IUpdatable
    {
        private const float TwoPi = 6.28318530718f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SINGLETON
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Interrupt Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ð°Ñ ÑÐºÐ¾Ñ€Ð¾ÑÑ‚ÑŒ Ð´Ð²Ð¸Ð¶ÐµÐ½Ð¸Ñ Ð´Ð»Ñ Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸Ñ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ.")]
        [SerializeField] private float movementInterruptThreshold = 2f;

        [Header("â”€â”€ Camera Juice â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð¡ÑÑ‹Ð»ÐºÐ° Ð½Ð° CameraJuiceProcessor Ð´Ð»Ñ Ð²Ð¸Ð·ÑƒÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ Ñ„Ð¸Ð´Ð±ÐµÐºÐ°.")]
        [SerializeField] private CameraJuiceProcessor cameraJuiceProcessor;

        [Tooltip("Ð˜Ð½Ñ‚ÐµÐ½ÑÐ¸Ð²Ð½Ð¾ÑÑ‚ÑŒ Ð¿Ð¾ÐºÐ°Ñ‡Ð¸Ð²Ð°Ð½Ð¸Ñ ÐºÐ°Ð¼ÐµÑ€Ñ‹ Ð²Ð¾ Ð²Ñ€ÐµÐ¼Ñ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ.")]
        [SerializeField, Range(0f, 0.02f)] private float actionCameraBobIntensity = 0.008f;

        [Tooltip("Ð§Ð°ÑÑ‚Ð¾Ñ‚Ð° Ð¿Ð¾ÐºÐ°Ñ‡Ð¸Ð²Ð°Ð½Ð¸Ñ ÐºÐ°Ð¼ÐµÑ€Ñ‹ (Ñ†Ð¸ÐºÐ»Ð¾Ð² Ð² ÑÐµÐºÑƒÐ½Ð´Ñƒ).")]
        [SerializeField, Range(0.5f, 3f)] private float actionCameraBobFrequency = 1.5f;

        [Header("â”€â”€ Audio â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð—Ð²ÑƒÐº Ð¿Ð¾ÐµÐ´Ð°Ð½Ð¸Ñ ÐµÐ´Ñ‹.")]
        [SerializeField] private AudioClip eatingSound;

        [Tooltip("Ð—Ð²ÑƒÐº Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ñ Ð¼ÐµÐ´Ð¸ÐºÐ°Ð¼ÐµÐ½Ñ‚Ð¾Ð².")]
        [SerializeField] private AudioClip healingSound;

        [Tooltip("Ð—Ð²ÑƒÐº Ð¾Ñ‚Ð¼ÐµÐ½Ñ‹ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ.")]
        [SerializeField] private AudioClip cancelSound;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SIGNAL OUTPUT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  STATE MACHINE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
        private bool _serviceRegistered;
        private float _cameraBobPhase;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CACHED REFERENCES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private HectonPlayerMovement _playerMovement;
        private PlayerToolManager _toolManager;
        private HectonSurvivalSystem _survivalSystem;
        private Rigidbody _playerRigidbody;
        private Transform _cachedTransform;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Ð˜Ð´Ñ‘Ñ‚ Ð»Ð¸ ÑÐµÐ¹Ñ‡Ð°Ñ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ðµ.</summary>
        public bool IsActionInProgress => _state == ActionState.InProgress;

        /// <summary>Ð¢ÐµÐºÑƒÑ‰Ð¸Ð¹ Ð¿Ñ€Ð¾Ð³Ñ€ÐµÑÑ (0-1).</summary>
        public float Progress => ResolveProgress01();

        /// <summary>ÐÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ Ð¿Ñ€ÐµÐ´Ð¼ÐµÑ‚ (null ÐµÑÐ»Ð¸ Ð½ÐµÑ‚ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ).</summary>
        public ItemData ActiveItem => _activeItem;

        /// <summary>
        /// Ð—Ð°Ð¿ÑƒÑÐºÐ°ÐµÑ‚ Ð¾Ñ‚Ð»Ð¾Ð¶ÐµÐ½Ð½Ð¾Ðµ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ðµ.
        /// </summary>
        /// <param name="item">ÐŸÑ€ÐµÐ´Ð¼ÐµÑ‚ Ð´Ð»Ñ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ñ.</param>
        /// <returns>true ÐµÑÐ»Ð¸ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ðµ Ð·Ð°Ð¿ÑƒÑ‰ÐµÐ½Ð¾.</returns>
        public bool StartAction(ItemData item)
        {
            return StartAction(item, -1, -1);
        }

        /// <summary>
        /// Ð—Ð°Ð¿ÑƒÑÐºÐ°ÐµÑ‚ Ð¾Ñ‚Ð»Ð¾Ð¶ÐµÐ½Ð½Ð¾Ðµ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ðµ Ñ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸ÐµÐ¹ Ð² Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€Ðµ Ð´Ð»Ñ Ð°Ñ‚Ð¾Ð¼Ð°Ñ€Ð½Ð¾Ð³Ð¾ ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ñ.
        /// </summary>
        /// <param name="item">ÐŸÑ€ÐµÐ´Ð¼ÐµÑ‚ Ð´Ð»Ñ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ñ.</param>
        /// <param name="anchorX">X ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ð° Ð² Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€Ðµ (-1 ÐµÑÐ»Ð¸ Ð½Ðµ Ð¸Ð· Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€Ñ).</param>
        /// <param name="anchorY">Y ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ð° Ð² Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€Ðµ (-1 ÐµÑÐ»Ð¸ Ð½Ðµ Ð¸Ð· Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€Ñ).</param>
        /// <returns>true ÐµÑÐ»Ð¸ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ðµ Ð·Ð°Ð¿ÑƒÑ‰ÐµÐ½Ð¾.</returns>
        public bool StartAction(ItemData item, int anchorX, int anchorY)
        {
            if (item == null) return false;
            if (_state == ActionState.InProgress) return false;
            if (item.UseDuration <= 0f)
            {
                // ÐœÐ³Ð½Ð¾Ð²ÐµÐ½Ð½Ð¾Ðµ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ - ÑƒÐ´Ð°Ð»ÑÐµÐ¼ Ð¸Ð· Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€Ñ ÐµÑÐ»Ð¸ ÐµÑÑ‚ÑŒ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹
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

            // Ð—Ð°Ð¿Ð¾Ð¼Ð¸Ð½Ð°ÐµÐ¼ Ñ‚ÐµÐºÑƒÑ‰Ð¸Ð¹ ÑÐ»Ð¾Ñ‚ Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð° Ð´Ð»Ñ Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸Ñ
            _lastToolSlotIndex = _toolManager != null ? _toolManager.CurrentSlotIndex : -1;

            return true;
        }

        /// <summary>
        /// ÐŸÑ€Ð¸Ð½ÑƒÐ´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ Ð¾Ñ‚Ð¼ÐµÐ½ÑÐµÑ‚ Ñ‚ÐµÐºÑƒÑ‰ÐµÐµ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ðµ.
        /// ÐŸÑ€ÐµÐ´Ð¼ÐµÑ‚ Ð¾ÑÑ‚Ð°Ñ‘Ñ‚ÑÑ Ð² Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€Ðµ (atomicity).
        /// </summary>
        public void CancelAction()
        {
            if (_state != ActionState.InProgress) return;

            ItemData cancelledItem = _activeItem;
            float cancelledProgress = ResolveProgress01();

            _state = ActionState.Idle;
            _activeItem = null;
            _inventoryAnchorX = -1;
            _inventoryAnchorY = -1;
            _actionTimer = 0f;
            _actionDuration = 0f;

            // ÐžÑ‡Ð¸Ñ‰Ð°ÐµÐ¼ ÐºÐ°Ð¼ÐµÑ€Ð½Ñ‹Ð¹ Ñ„Ð¸Ð´Ð±ÐµÐº
            if (cameraJuiceProcessor != null)
                cameraJuiceProcessor.ClearActionBob();

            PlayCancelSound();
            PublishActionCancelled(cancelledItem, cancelledProgress, PlayerActionCancelledSignal.ReasonGeneric);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            PlayerActionController registered = GlobalRegistry.PlayerActions;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            _cachedTransform = transform;

            // ÐšÑÑˆÐ¸Ñ€ÑƒÐµÐ¼ ÑÑÑ‹Ð»ÐºÐ¸
            TryGetComponent(out _playerMovement);
            TryGetComponent(out _toolManager);
            TryGetComponent(out _playerRigidbody);
        }

        private void OnDestroy()
        {
            TryUnregisterService();

        }

        private void OnEnable()
        {
            // ÐšÑÑˆÐ¸Ñ€ÑƒÐµÐ¼ SurvivalSystem
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            TryRegister();
            TryRegisterService();
        }

        private void OnDisable()
        {
            if (_state == ActionState.InProgress)
                CancelAction();

            TryUnregister();
            TryUnregisterService();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            if (_state != ActionState.InProgress) return;

            float safeDeltaTime = math.max(0f, deltaTime);

            // â”€â”€ ÐŸÑ€Ð¾Ð²ÐµÑ€ÐºÐ° Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸Ð¹ â”€â”€
            if (CheckInterrupts())
            {
                CancelAction();
                return;
            }

            // â”€â”€ ÐžÐ±Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ðµ Ñ‚Ð°Ð¹Ð¼ÐµÑ€Ð° â”€â”€
            _actionTimer += safeDeltaTime;

            // â”€â”€ ÐšÐ°Ð¼ÐµÑ€Ð½Ñ‹Ð¹ Ñ„Ð¸Ð´Ð±ÐµÐº (Ð¼Ð¸ÐºÑ€Ð¾-Ð¿Ð¾ÐºÐ°Ñ‡Ð¸Ð²Ð°Ð½Ð¸Ðµ) â”€â”€
            ApplyCameraJuice(safeDeltaTime);

            // â”€â”€ ÐŸÑƒÐ±Ð»Ð¸ÐºÐ°Ñ†Ð¸Ñ Ð¿Ñ€Ð¾Ð³Ñ€ÐµÑÑÐ° â”€â”€
            float progress = ResolveProgress01();
            PublishActionProgress(progress);

            // â”€â”€ Ð—Ð°Ð²ÐµÑ€ÑˆÐµÐ½Ð¸Ðµ â”€â”€
            if (_actionTimer >= _actionDuration)
            {
                CompleteAction();
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” CAMERA JUICE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐŸÑ€Ð¸Ð¼ÐµÐ½ÑÐµÑ‚ Ð¼Ð¸ÐºÑ€Ð¾-Ð¿Ð¾ÐºÐ°Ñ‡Ð¸Ð²Ð°Ð½Ð¸Ðµ ÐºÐ°Ð¼ÐµÑ€Ñ‹ Ð²Ð¾ Ð²Ñ€ÐµÐ¼Ñ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ.
        /// Ð˜Ð¼Ð¸Ñ‚Ð¸Ñ€ÑƒÐµÑ‚ Ð´Ð²Ð¸Ð¶ÐµÐ½Ð¸Ðµ Ñ€ÑƒÐº Ð¿ÐµÑ€ÑÐ¾Ð½Ð°Ð¶Ð°.
        /// </summary>
        private void ApplyCameraJuice(float deltaTime)
        {
            if (cameraJuiceProcessor == null) return;

            // Ð¡Ð¸Ð½ÑƒÑÐ¾Ð¸Ð´Ð°Ð»ÑŒÐ½Ð¾Ðµ Ð¿Ð¾ÐºÐ°Ñ‡Ð¸Ð²Ð°Ð½Ð¸Ðµ Ñ Ð·Ð°Ñ‚ÑƒÑ…Ð°Ð½Ð¸ÐµÐ¼ Ðº ÐºÐ¾Ð½Ñ†Ñƒ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ
            _cameraBobPhase += deltaTime * actionCameraBobFrequency * TwoPi;

            float progress = ResolveProgress01();
            float fadeOut = 1f - (progress * progress); // ÐšÐ²Ð°Ð´Ñ€Ð°Ñ‚Ð¸Ñ‡Ð½Ð¾Ðµ Ð·Ð°Ñ‚ÑƒÑ…Ð°Ð½Ð¸Ðµ

            // Ð ÐµÐ³Ð¸ÑÑ‚Ñ€Ð¸Ñ€ÑƒÐµÐ¼ Ð¼Ð¸ÐºÑ€Ð¾-bob ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€ Ñ Ð·Ð°Ñ‚ÑƒÑ…Ð°ÑŽÑ‰ÐµÐ¹ Ð¸Ð½Ñ‚ÐµÐ½ÑÐ¸Ð²Ð½Ð¾ÑÑ‚ÑŒÑŽ
            float intensity = actionCameraBobIntensity * fadeOut;
            cameraJuiceProcessor.RegisterActionBob(intensity, actionCameraBobFrequency);
        }

        private float ResolveProgress01()
        {
            return _state == ActionState.InProgress && _actionDuration > 0.0001f
                ? math.saturate(_actionTimer * math.rcp(_actionDuration))
                : 0f;
        }

        private void PublishActionProgress(float progress01)
        {
            ItemData item = _activeItem;
            PlayerActionProgressSignal signal = new PlayerActionProgressSignal
            {
                Progress01 = math.saturate(progress01),
                ItemHash = ResolveItemHash(item),
                Frame = unchecked((uint)Time.frameCount),
                ActiveToolSlot = PackActiveToolSlot(_lastToolSlotIndex),
                ActionKind = ResolveActionKind(item),
                Flags = item != null ? PlayerActionProgressSignal.FlagHasItem : (byte)0
            };

            GlobalSignals.Publish(in signal);
        }

        private void PublishActionCompleted(ItemData item, int anchorX, int anchorY)
        {
            byte flags = item != null ? PlayerActionCompletedSignal.FlagHasItem : (byte)0;
            if (anchorX >= 0 && anchorY >= 0)
                flags |= PlayerActionCompletedSignal.FlagInventoryAnchorValid;

            PlayerActionCompletedSignal signal = new PlayerActionCompletedSignal
            {
                ItemHash = ResolveItemHash(item),
                Frame = unchecked((uint)Time.frameCount),
                InventoryAnchorX = PackInventoryAnchor(anchorX),
                InventoryAnchorY = PackInventoryAnchor(anchorY),
                ActionKind = ResolveActionKind(item),
                Flags = flags
            };

            GlobalSignals.Publish(in signal);
        }

        private void PublishActionCancelled(ItemData item, float progress01, byte reason)
        {
            PlayerActionCancelledSignal signal = new PlayerActionCancelledSignal
            {
                ItemHash = ResolveItemHash(item),
                Frame = unchecked((uint)Time.frameCount),
                Progress01 = math.saturate(progress01),
                ActionKind = ResolveActionKind(item),
                Reason = reason,
                Flags = item != null ? PlayerActionCancelledSignal.FlagHasItem : (byte)0
            };

            GlobalSignals.Publish(in signal);
        }

        private static uint ResolveItemHash(ItemData item)
        {
            return item != null ? unchecked((uint)item.PersistentHashId) : 0u;
        }

        private static byte ResolveActionKind(ItemData item)
        {
            if (item == null)
                return PlayerActionProgressSignal.ActionKindGeneric;
            if (item.integrityRestore > 0f)
                return PlayerActionProgressSignal.ActionKindMedical;
            if (item.oxygenRestore > 0f)
                return PlayerActionProgressSignal.ActionKindOxygen;
            if (item.hungerRestore > 0f || item.thirstRestore > 0f)
                return PlayerActionProgressSignal.ActionKindFood;
            return PlayerActionProgressSignal.ActionKindGeneric;
        }

        private static ushort PackInventoryAnchor(int anchor)
        {
            return anchor >= 0 ? (ushort)math.min(anchor, ushort.MaxValue - 1) : ushort.MaxValue;
        }

        private static ushort PackActiveToolSlot(int slotIndex)
        {
            return slotIndex >= 0 ? (ushort)math.min(slotIndex, ushort.MaxValue - 1) : ushort.MaxValue;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” INTERRUPTS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐŸÑ€Ð¾Ð²ÐµÑ€ÑÐµÑ‚ ÑƒÑÐ»Ð¾Ð²Ð¸Ñ Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸Ñ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ.
        /// </summary>
        private bool CheckInterrupts()
        {
            // â”€â”€ 1. ÐŸÑ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸Ðµ Ð¿Ð¾ Ð´Ð²Ð¸Ð¶ÐµÐ½Ð¸ÑŽ â”€â”€
            if (_playerRigidbody != null)
            {
                Vector3 velocity = _playerRigidbody.linearVelocity;
                float speedSqr = velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z;
                float interruptThresholdSqr = movementInterruptThreshold * movementInterruptThreshold;
                if (speedSqr > interruptThresholdSqr)
                    return true;
            }

            // â”€â”€ 2. ÐŸÑ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸Ðµ Ð¿Ð¾ ÑÐ¼ÐµÐ½Ðµ Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð° â”€â”€
            if (_toolManager != null && _lastToolSlotIndex >= 0)
            {
                if (_toolManager.CurrentSlotIndex != _lastToolSlotIndex)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Ð’Ð½ÐµÑˆÐ½Ð¸Ð¹ Ð¼ÐµÑ‚Ð¾Ð´ Ð´Ð»Ñ Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸Ñ Ð¿Ð¾ ÑƒÑ€Ð¾Ð½Ñƒ.
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð¸Ð· HectonSurvivalSystem Ð¸Ð»Ð¸ Ð´Ñ€ÑƒÐ³Ð¸Ñ… ÑÐ¸ÑÑ‚ÐµÐ¼.
        /// </summary>
        public void OnDamageTaken()
        {
            if (_state == ActionState.InProgress)
                CancelAction();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” COMPLETION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

            // ÐžÑ‡Ð¸Ñ‰Ð°ÐµÐ¼ ÐºÐ°Ð¼ÐµÑ€Ð½Ñ‹Ð¹ Ñ„Ð¸Ð´Ð±ÐµÐº
            if (cameraJuiceProcessor != null)
                cameraJuiceProcessor.ClearActionBob();

            // â”€â”€ ATOMIC: Remove item from inventory ONLY on completion â”€â”€
            if (completedItem != null)
            {
                if (anchorX >= 0 && anchorY >= 0)
                {
                    RemoveItemFromInventory(anchorX, anchorY);
                }
                ConsumableItem.TryConsume(completedItem);
                PlayCompletionSound(completedItem);
            }

            PublishActionCompleted(completedItem, anchorX, anchorY);
        }

        /// <summary>
        /// Removes one item from inventory at the specified position.
        /// Called only on successful action completion (atomicity).
        /// </summary>
        private void RemoveItemFromInventory(int anchorX, int anchorY)
        {
            PlayerInventory inventory = Hecton8.Core.GlobalRegistry.PlayerInventoryRuntime;
            if (inventory == null) return;

            inventory.RemoveOneItem(anchorX, anchorY);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” AUDIO
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void PlayCompletionSound(ItemData item)
        {
            if (item == null) return;

            AudioClip clip = null;

            // ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÑÐµÐ¼ Ñ‚Ð¸Ð¿ Ð·Ð²ÑƒÐºÐ° Ð¿Ð¾ ÑÑ„Ñ„ÐµÐºÑ‚Ð°Ð¼ Ð¿Ñ€ÐµÐ´Ð¼ÐµÑ‚Ð°
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

            if (Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance != null && _cachedTransform != null)
            {
                Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance.PlayAtPoint(clip, _cachedTransform.position);
            }
        }

        private void PlayCancelSound()
        {
            if (cancelSound == null) return;

            if (Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance != null && _cachedTransform != null)
            {
                Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance.PlayAtPoint(cancelSound, _cachedTransform.position);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” REGISTRATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregister()
        {
            if (!_registered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            PlayerActionController registered = GlobalRegistry.PlayerActions;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterPlayerActionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PlayerActions, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPlayerActionRuntime(this);
            _serviceRegistered = false;
        }
    }
}
