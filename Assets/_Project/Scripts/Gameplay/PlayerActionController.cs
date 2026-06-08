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
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using CoreAudioEvent = Hecton8.Core.AudioEvent;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// ÐšÐ¾Ð½Ñ‚Ñ€Ð¾Ð»Ð»ÐµÑ€ Ð¾Ñ‚Ð»Ð¾Ð¶ÐµÐ½Ð½Ñ‹Ñ… Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ð¹ Ð¸Ð³Ñ€Ð¾ÐºÐ°.
    /// Ð£Ð¿Ñ€Ð°Ð²Ð»ÑÐµÑ‚ Ñ‚Ð°Ð¹Ð¼ÐµÑ€Ð¾Ð¼, Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°Ð½Ð¸ÑÐ¼Ð¸ Ð¸ Ð·Ð°Ð²ÐµÑ€ÑˆÐµÐ½Ð¸ÐµÐ¼ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9920)]
    public sealed class PlayerActionController : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IPlayerActionInterruptSink, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static int s_x001PlayerActionControllerSignalPushDropCount;
        private const float TwoPi = 6.28318530718f;
        private const uint KccVelocityInterruptMaxAgeFrames = 12u;
        private const byte ActionAudioClipNone = 0;
        private const byte ActionAudioClipEating = 1;
        private const byte ActionAudioClipHealing = 2;
        private const byte ActionAudioClipCancel = 3;
        private const byte ActionAudioClipItemUseSound = 4;
        private const byte ActionCameraBobCommandNone = 0;
        private const byte ActionCameraBobCommandApply = 1;
        private const byte ActionCameraBobCommandClear = 2;

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ActionAudioRequest
        {
            [FieldOffset(0)] public Vector3 Position;
            [FieldOffset(12)] public uint EventId;
            [FieldOffset(16)] public uint ItemHash;
            [FieldOffset(20)] public byte ClipKind;
            [FieldOffset(21)] public byte Dirty;
            [FieldOffset(22)] public ushort Reserved0;
            [FieldOffset(24)] public uint Reserved1;
            [FieldOffset(28)] public uint Reserved2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ActionCameraBobRequest
        {
            [FieldOffset(0)] public float Intensity;
            [FieldOffset(4)] public float Frequency;
            [FieldOffset(8)] public byte Command;
            [FieldOffset(9)] public byte Reserved0;
            [FieldOffset(10)] public ushort Reserved1;
            [FieldOffset(12)] public uint Reserved2;
        }

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
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _isInitialized;
        private bool _runtimeOwnerAborted;
        private float _cameraBobPhase;
        private ActionAudioRequest _pendingActionAudio;
        private ActionCameraBobRequest _pendingActionCameraBob;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CACHED REFERENCES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private HectonPlayerMovement _playerMovement;
        private PlayerToolManager _toolManager;
        private HectonSurvivalSystem _survivalSystem;
        private Transform _cachedTransform;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPlayerInventoryService _playerInventoryService;
        private IAudioService _audioService;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Ð˜Ð´Ñ‘Ñ‚ Ð»Ð¸ ÑÐµÐ¹Ñ‡Ð°Ñ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ðµ.</summary>
        public bool IsActionInProgress => _state == ActionState.InProgress;

        public bool IsInitialized =>
            !_runtimeOwnerAborted &&
            _isInitialized &&
            _serviceRegistered &&
            isActiveAndEnabled &&
            ReferenceEquals(GlobalRegistry.PlayerActions, this);

        public ServiceHeartbeatState HeartbeatState => IsInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        public bool IsServiceReady => IsInitialized;

        /// <summary>Ð¢ÐµÐºÑƒÑ‰Ð¸Ð¹ Ð¿Ñ€Ð¾Ð³Ñ€ÐµÑÑ (0-1).</summary>
        public float Progress => ResolveProgress01();

        /// <summary>ÐÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ Ð¿Ñ€ÐµÐ´Ð¼ÐµÑ‚ (null ÐµÑÐ»Ð¸ Ð½ÐµÑ‚ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ).</summary>
        public ItemData ActiveItem => _activeItem;

        internal static PlayerActionController ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
            s_x001PlayerActionControllerSignalPushDropCount = 0;
        }

        public static PlayerActionController EnsureRuntimeInstance()
        {
            PlayerActionController runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            GameObject runtimeRoot = new GameObject("[PlayerActionController]"); // COLD ALLOC: GameObject[1] - bootstrap-owned delayed player action/audio service root - owner: PlayerActionController
            return runtimeRoot.AddComponent<PlayerActionController>();
        }

        public void InitializeService()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            if (!TryRegisterService())
                return;

            _isInitialized = true;
            TryRegisterHotSwap();
            TryRegister();
            CacheRegistryServicesCold();
        }

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
            RefreshPlayerOwnedReferencesCold();

            if (item == null) return false;
            if (_state == ActionState.InProgress) return false;
            if (!CanUseInventoryAnchor(anchorX, anchorY, item)) return false;
            if (!CanApplyConsumableEffects(item)) return false;

            if (item.UseDuration <= 0f)
            {
                // ÐœÐ³Ð½Ð¾Ð²ÐµÐ½Ð½Ð¾Ðµ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ - ÑƒÐ´Ð°Ð»ÑÐµÐ¼ Ð¸Ð· Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€Ñ ÐµÑÐ»Ð¸ ÐµÑÑ‚ÑŒ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹
                if (HasInventoryAnchor(anchorX, anchorY) && !TryRemoveItemFromInventory(anchorX, anchorY, item))
                    return false;

                ConsumableItem.TryConsumeWithoutAudio(item, _survivalSystem);
                PlayCompletionSound(item);
                PublishActionCompleted(item, anchorX, anchorY);
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
            QueueActionCameraBobClear();

            PlayCancelSound();
            PublishActionCancelled(cancelledItem, cancelledProgress, PlayerActionCancelledSignal.ReasonGeneric);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            _cachedTransform = transform;

            // ÐšÑÑˆÐ¸Ñ€ÑƒÐµÐ¼ ÑÑÑ‹Ð»ÐºÐ¸
            TryGetComponent(out _playerMovement);
            TryGetComponent(out _toolManager);
            CacheRegistryServicesCold();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            // ÐšÑÑˆÐ¸Ñ€ÑƒÐµÐ¼ SurvivalSystem
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            ConsumableItem.BindSurvivalSystemCold(_survivalSystem);
            if (!TryRegisterService())
                return;

            _isInitialized = true;
            TryRegister();
            TryRegisterHotSwap();
            CacheRegistryServicesCold();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
            {
                if (ReferenceEquals(ActiveRuntimeInstance, this))
                    ActiveRuntimeInstance = null;

                return;
            }

            if (_state == ActionState.InProgress)
                CancelAction();

            TryUnregisterHotSwap();
            TryUnregister();
            TryUnregisterService();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    RefreshPlayerOwnedReferencesCold();
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    CachePlayerInventoryService(currentService as IPlayerInventoryService);
                    RefreshPlayerOwnedReferencesCold();
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister(clearQueuedPresentation: false);
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
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

        public void LateFrameTick()
        {
            FlushQueuedActionCameraBob();
            FlushQueuedActionAudio();
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
            // Ð¡Ð¸Ð½ÑƒÑÐ¾Ð¸Ð´Ð°Ð»ÑŒÐ½Ð¾Ðµ Ð¿Ð¾ÐºÐ°Ñ‡Ð¸Ð²Ð°Ð½Ð¸Ðµ Ñ Ð·Ð°Ñ‚ÑƒÑ…Ð°Ð½Ð¸ÐµÐ¼ Ðº ÐºÐ¾Ð½Ñ†Ñƒ Ð´ÐµÐ¹ÑÑ‚Ð²Ð¸Ñ
            _cameraBobPhase += deltaTime * actionCameraBobFrequency * TwoPi;

            float progress = ResolveProgress01();
            float fadeOut = 1f - (progress * progress); // ÐšÐ²Ð°Ð´Ñ€Ð°Ñ‚Ð¸Ñ‡Ð½Ð¾Ðµ Ð·Ð°Ñ‚ÑƒÑ…Ð°Ð½Ð¸Ðµ

            // Ð ÐµÐ³Ð¸ÑÑ‚Ñ€Ð¸Ñ€ÑƒÐµÐ¼ Ð¼Ð¸ÐºÑ€Ð¾-bob ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€ Ñ Ð·Ð°Ñ‚ÑƒÑ…Ð°ÑŽÑ‰ÐµÐ¹ Ð¸Ð½Ñ‚ÐµÐ½ÑÐ¸Ð²Ð½Ð¾ÑÑ‚ÑŒÑŽ
            float intensity = actionCameraBobIntensity * fadeOut;
            QueueActionCameraBob(intensity, actionCameraBobFrequency);
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
                Frame = SystemDispatcher.CurrentFrameId,
                ActiveToolSlot = PackActiveToolSlot(_lastToolSlotIndex),
                ActionKind = ResolveActionKind(item),
                Flags = item != null ? PlayerActionProgressSignal.FlagHasItem : (byte)0
            };

            SignalBus<PlayerActionProgressSignal>.TryPushTracked(in signal, ref s_x001PlayerActionControllerSignalPushDropCount);
        }

        private void PublishActionCompleted(ItemData item, int anchorX, int anchorY)
        {
            byte flags = item != null ? PlayerActionCompletedSignal.FlagHasItem : (byte)0;
            if (anchorX >= 0 && anchorY >= 0)
                flags |= PlayerActionCompletedSignal.FlagInventoryAnchorValid;

            PlayerActionCompletedSignal signal = new PlayerActionCompletedSignal
            {
                ItemHash = ResolveItemHash(item),
                Frame = SystemDispatcher.CurrentFrameId,
                InventoryAnchorX = PackInventoryAnchor(anchorX),
                InventoryAnchorY = PackInventoryAnchor(anchorY),
                ActionKind = ResolveActionKind(item),
                Flags = flags
            };

            SignalBus<PlayerActionCompletedSignal>.TryPushTracked(in signal, ref s_x001PlayerActionControllerSignalPushDropCount);
        }

        private void PublishActionCancelled(ItemData item, float progress01, byte reason)
        {
            PlayerActionCancelledSignal signal = new PlayerActionCancelledSignal
            {
                ItemHash = ResolveItemHash(item),
                Frame = SystemDispatcher.CurrentFrameId,
                Progress01 = math.saturate(progress01),
                ActionKind = ResolveActionKind(item),
                Reason = reason,
                Flags = item != null ? PlayerActionCancelledSignal.FlagHasItem : (byte)0
            };

            SignalBus<PlayerActionCancelledSignal>.TryPushTracked(in signal, ref s_x001PlayerActionControllerSignalPushDropCount);
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
            if (TryResolveKccVelocity(out Vector3 velocity))
            {
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

        private static bool TryResolveKccVelocity(out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) || signal.Sequence == 0u)
                return false;

            uint currentFrame = SystemDispatcher.CurrentFrameId;
            uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
            if (currentFrame != 0u &&
                signalFrame != 0u &&
                (signalFrame > currentFrame || currentFrame - signalFrame > KccVelocityInterruptMaxAgeFrames))
            {
                return false;
            }

            float3 value = signal.Velocity;
            if (!math.all(math.isfinite(value)))
                return false;

            velocity = new Vector3(value.x, value.y, value.z);
            return true;
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
            QueueActionCameraBobClear();

            // â”€â”€ ATOMIC: Remove item from inventory ONLY on completion â”€â”€
            if (completedItem != null)
            {
                RefreshPlayerOwnedReferencesCold();

                if (!CanApplyConsumableEffects(completedItem))
                {
                    PublishActionCancelled(completedItem, 1f, PlayerActionCancelledSignal.ReasonGeneric);
                    return;
                }

                if (HasInventoryAnchor(anchorX, anchorY) && !TryRemoveItemFromInventory(anchorX, anchorY, completedItem))
                {
                    PublishActionCancelled(completedItem, 1f, PlayerActionCancelledSignal.ReasonGeneric);
                    return;
                }

                ConsumableItem.TryConsumeWithoutAudio(completedItem, _survivalSystem);
                PlayCompletionSound(completedItem);
            }

            PublishActionCompleted(completedItem, anchorX, anchorY);
        }

        /// <summary>
        /// Removes one item from inventory at the specified position.
        /// Called only on successful action completion (atomicity).
        /// </summary>
        private bool TryRemoveItemFromInventory(int anchorX, int anchorY, ItemData expectedItem)
        {
            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            if (inventory == null)
                return false;

            int expectedHash = expectedItem != null ? expectedItem.PersistentHashId : 0;
            if (expectedHash != 0 && inventory.GetItemHashAt(anchorX, anchorY) != expectedHash)
                return false;

            int removedHash = inventory.RemoveOneItem(anchorX, anchorY);
            return removedHash != 0 && (expectedHash == 0 || removedHash == expectedHash);
        }

        private bool CanUseInventoryAnchor(int anchorX, int anchorY, ItemData expectedItem)
        {
            if (!HasInventoryAnchor(anchorX, anchorY))
                return true;

            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            if (inventory == null)
                return false;

            int expectedHash = expectedItem != null ? expectedItem.PersistentHashId : 0;
            return expectedHash == 0 || inventory.GetItemHashAt(anchorX, anchorY) == expectedHash;
        }

        private bool CanApplyConsumableEffects(ItemData item)
        {
            return item == null ||
                   !item.isConsumable ||
                   !ConsumableItem.HasAnyEffect(item) ||
                   _survivalSystem != null;
        }

        private static bool HasInventoryAnchor(int anchorX, int anchorY)
        {
            return anchorX >= 0 && anchorY >= 0;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” AUDIO
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void PlayCompletionSound(ItemData item)
        {
            if (item == null) return;

            AudioClip clip = null;
            byte clipKind = ActionAudioClipNone;
            uint eventId = item.UseAudioEventId;
            uint itemHash = ResolveItemHash(item);

            // ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÑÐµÐ¼ Ñ‚Ð¸Ð¿ Ð·Ð²ÑƒÐºÐ° Ð¿Ð¾ ÑÑ„Ñ„ÐµÐºÑ‚Ð°Ð¼ Ð¿Ñ€ÐµÐ´Ð¼ÐµÑ‚Ð°
            if (item.integrityRestore > 0f)
            {
                clip = healingSound;
                clipKind = ActionAudioClipHealing;
            }
            else if (item.hungerRestore > 0f || item.thirstRestore > 0f)
            {
                clip = eatingSound;
                clipKind = ActionAudioClipEating;
            }
            else if (item.useSound != null)
            {
                clip = item.useSound;
                clipKind = ActionAudioClipItemUseSound;
            }

            if (clip == null && eventId == 0u) return;

            if (ResolveAudioService() != null && _cachedTransform != null)
                QueueActionAudio(clipKind, eventId, itemHash, _cachedTransform.position);
        }

        private void QueueActionCameraBob(float intensity, float frequency)
        {
            if (intensity <= 0f)
                return;

            _pendingActionCameraBob.Intensity = intensity;
            _pendingActionCameraBob.Frequency = frequency;
            _pendingActionCameraBob.Command = ActionCameraBobCommandApply;
            _pendingActionCameraBob.Reserved0 = 0;
            _pendingActionCameraBob.Reserved1 = 0;
            _pendingActionCameraBob.Reserved2 = 0u;
        }

        private void QueueActionCameraBobClear()
        {
            _pendingActionCameraBob.Intensity = 0f;
            _pendingActionCameraBob.Frequency = 0f;
            _pendingActionCameraBob.Command = ActionCameraBobCommandClear;
            _pendingActionCameraBob.Reserved0 = 0;
            _pendingActionCameraBob.Reserved1 = 0;
            _pendingActionCameraBob.Reserved2 = 0u;
        }

        private void FlushQueuedActionCameraBob()
        {
            if (_pendingActionCameraBob.Command == ActionCameraBobCommandNone)
                return;

            ActionCameraBobRequest request = _pendingActionCameraBob;
            _pendingActionCameraBob = default;

            CameraJuiceProcessor processor = cameraJuiceProcessor;
            if (processor == null)
                return;

            if (request.Command == ActionCameraBobCommandApply)
                processor.RegisterActionBob(ResolveActionCameraBobPresentationIntensity(request.Intensity), request.Frequency);
            else if (request.Command == ActionCameraBobCommandClear)
                processor.ClearActionBob();
        }

        private static float ResolveActionCameraBobPresentationIntensity(float intensity)
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.isfinite(quality) ? quality : 1f);
            return intensity * math.lerp(0.65f, 1.15f, quality);
        }

        private void PlayCancelSound()
        {
            if (cancelSound == null) return;

            if (ResolveAudioService() != null && _cachedTransform != null)
                QueueActionAudio(ActionAudioClipCancel, 0u, 0u, _cachedTransform.position);
        }

        private void QueueActionAudio(byte clipKind, uint eventId, uint itemHash, Vector3 position)
        {
            _pendingActionAudio.Position = position;
            _pendingActionAudio.EventId = eventId;
            _pendingActionAudio.ItemHash = itemHash;
            _pendingActionAudio.ClipKind = clipKind;
            _pendingActionAudio.Dirty = 1;
            _pendingActionAudio.Reserved0 = 0;
            _pendingActionAudio.Reserved1 = 0u;
            _pendingActionAudio.Reserved2 = 0u;
        }

        private void FlushQueuedActionAudio()
        {
            if (_pendingActionAudio.Dirty == 0)
                return;

            ActionAudioRequest request = _pendingActionAudio;
            _pendingActionAudio = default;

            IAudioService audioService = ResolveAudioService();
            if (audioService == null)
                return;

            float volume = ResolveActionAudioPresentationVolume();
            if (request.EventId != 0u && audioService.IsAudioRuntimeReady)
            {
                CoreAudioEvent audioEvent = new CoreAudioEvent(request.EventId, request.Position, volume, 1f);
                if (audioService.QueueAudioEvent(in audioEvent))
                    return;
            }

            AudioClip clip = ResolveActionAudioClip(in request);
            if (clip != null)
                audioService.PlayAtPoint(clip, request.Position, volume, 1f);
        }

        private void ClearQueuedActionAudio()
        {
            _pendingActionAudio = default;
            _pendingActionCameraBob = default;
        }

        private AudioClip ResolveActionAudioClip(in ActionAudioRequest request)
        {
            switch (request.ClipKind)
            {
                case ActionAudioClipEating:
                    return eatingSound;
                case ActionAudioClipHealing:
                    return healingSound;
                case ActionAudioClipCancel:
                    return cancelSound;
                case ActionAudioClipItemUseSound:
                    return ResolveItemUseSound(request.ItemHash);
                default:
                    return null;
            }
        }

        private AudioClip ResolveItemUseSound(uint itemHash)
        {
            if (itemHash == 0u)
                return null;

            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            if (inventory == null || inventory.ItemCatalog == null || inventory.ItemCatalog.HasLookupAmbiguity)
                return null;

            ItemData item = inventory.ItemCatalog.FindByHash(unchecked((int)itemHash));
            return item != null ? item.useSound : null;
        }

        private static float ResolveActionAudioPresentationVolume()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.isfinite(quality) ? quality : 1f);
            return math.lerp(0.75f, 1f, quality);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” REGISTRATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregister(bool clearQueuedPresentation = true)
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }

            if (clearQueuedPresentation)
                ClearQueuedActionAudio();
        }

        private bool EnsureSingletonOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            PlayerActionController activeRuntime = ActiveRuntimeInstance;
            if (!ReferenceEquals(activeRuntime, null) && !ReferenceEquals(activeRuntime, this))
            {
                if (IsPlayerActionRuntimeUsable(activeRuntime))
                {
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                ActiveRuntimeInstance = null;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            ActiveRuntimeInstance = this;
            return true;
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_state == ActionState.InProgress)
                CancelAction();

            TryUnregisterHotSwap();
            TryUnregister();
            TryUnregisterService();
            _isInitialized = false;
            _playerRuntimeContext = null;
            _playerInventoryService = null;
            _audioService = null;
            _playerMovement = null;
            _toolManager = null;
            _survivalSystem = null;
            _cachedTransform = null;
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered)
            {
                if (ReferenceEquals(GlobalRegistry.PlayerActions, this))
                    return true;

                _serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterPlayerActionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PlayerActions, this);
            return _serviceRegistered;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (_runtimeOwnerAborted)
                return true;

            PlayerActionController registered = GlobalRegistry.PlayerActions;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsPlayerActionRuntimeUsable(registered))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            GlobalRegistry.UnregisterPlayerActionRuntime(registered);
            return false;
        }

        private static bool IsPlayerActionRuntimeUsable(PlayerActionController controller)
        {
            return controller != null &&
                   !controller._runtimeOwnerAborted &&
                   controller._serviceRegistered &&
                   controller.isActiveAndEnabled &&
                   ReferenceEquals(GlobalRegistry.PlayerActions, controller);
        }

        private static PlayerActionController ResolveUsableRuntime()
        {
            PlayerActionController runtime = ActiveRuntimeInstance;
            if (IsPlayerActionRuntimeUsable(runtime))
                return runtime;

            PlayerActionController registered = GlobalRegistry.PlayerActions;
            if (IsPlayerActionRuntimeUsable(registered))
            {
                ActiveRuntimeInstance = registered;
                return registered;
            }

            ActiveRuntimeInstance = null;
            return null;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwap();
            TryUnregister();
            TryUnregisterService();
            _isInitialized = false;
            _runtimeOwnerAborted = true;
            _playerRuntimeContext = null;
            _playerInventoryService = null;
            _audioService = null;

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            if (Application.isPlaying)
                Destroy(this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPlayerActionRuntime(this);
            _serviceRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player ?? PlayerRuntimeContextService.ActiveRuntimeContext);
            CachePlayerInventoryService(GlobalRegistry.PlayerInventory);
            CacheAudioService(GlobalRegistry.Audio);
            RefreshPlayerOwnedReferencesCold();
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            _playerRuntimeContext = IsPlayerRuntimeContextUsable(playerRuntimeContext) ? playerRuntimeContext : null;
        }

        private void CachePlayerInventoryService(IPlayerInventoryService playerInventoryService)
        {
            _playerInventoryService = playerInventoryService != null && playerInventoryService.IsInitialized
                ? playerInventoryService
                : null;
        }

        private void RefreshPlayerOwnedReferencesCold()
        {
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (!IsPlayerRuntimeContextUsable(playerRuntimeContext))
            {
                CachePlayerRuntimeContext(GlobalRegistry.Player ?? PlayerRuntimeContextService.ActiveRuntimeContext);
                playerRuntimeContext = _playerRuntimeContext;
            }

            if (IsPlayerRuntimeContextUsable(playerRuntimeContext))
            {
                _cachedTransform = playerRuntimeContext.PlayerTransform != null ? playerRuntimeContext.PlayerTransform : _cachedTransform;
                _playerMovement = playerRuntimeContext.PlayerMovement;
                _toolManager = playerRuntimeContext.ToolManager;
                _survivalSystem = playerRuntimeContext.SurvivalSystem;
            }
            else
            {
                ClearPlayerOwnedReferences();
            }

            IPlayerInventoryService inventoryService = _playerInventoryService;
            if (inventoryService == null || !inventoryService.IsInitialized)
            {
                CachePlayerInventoryService(GlobalRegistry.PlayerInventory);
                inventoryService = _playerInventoryService;
            }

            if (inventoryService != null)
            {
                if (_toolManager == null)
                    _toolManager = inventoryService.ToolManager;
            }

            if (_cachedTransform == null)
                _cachedTransform = transform;

            ConsumableItem.BindSurvivalSystemCold(_survivalSystem);
        }

        private static bool IsPlayerRuntimeContextUsable(IPlayerRuntimeContext playerRuntimeContext)
        {
            return playerRuntimeContext != null &&
                   playerRuntimeContext.IsInitialized &&
                   playerRuntimeContext.PlayerObject != null &&
                   playerRuntimeContext.PlayerTransform != null;
        }

        private void ClearPlayerOwnedReferences()
        {
            _playerMovement = null;
            _toolManager = null;
            _survivalSystem = null;
            _cachedTransform = transform;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}
