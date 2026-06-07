// ============================================================================
// HECTON-8 — SealedDoor.cs
// Laser-cuttable sealed door for wrecks and restricted areas.
//
// ARCHITECTURE:
//   • Standalone prop — driven by LaserCutter hit cadence (no Update).
//   • Progress-based cutting system.
//   • UnityEvents for progress UI and door opening.
//   • ICuttable integration for LaserCutter tool.
//
// ZERO GC:
//   • Tool-hit cadence drives cutting; no Update(), no allocations.
//   • Cached Transform, Renderer, Collider.
//   • State machine with enum (no coroutines).
//   • Progress callbacks and renderer updates are coalesced to fixed thresholds.
//
// USAGE:
//   1. Place on door GameObject with mesh and collider.
//   2. Configure requiredCuttingTime (seconds of laser required).
//   3. Connect OnProgressChanged to UI progress bar.
//   4. Connect OnDoorOpened to animation or game logic.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State machine states for door lifecycle.
    /// </summary>
    public enum DoorState
    {
        Sealed,      // Waiting to be cut
        Cutting,     // Currently being cut
        Opened,      // Door opened
        Locked       // Cannot be cut (optional)
    }

    /// <summary>
    /// Sealed door that requires laser cutting to open.
    /// Implements ICuttable for LaserCutter integration.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SealedDoor : MonoBehaviour, ICuttable, ILateFrameTickable, IWfcDoorLaserCutTarget, IGlobalRegistryHotSwapListener
    {
        private static int s_x001SealedDoorSignalPushDropCount;
        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        private static readonly int _ProgressID = Shader.PropertyToID("_CutProgress");
        private static readonly int _GlowColorID = Shader.PropertyToID("_CutGlowColor");
        private const float ProgressPublishEpsilon = 0.01f;
        private const uint WfcOutpostDoorSourceHash = 0x57464344u; // WFCD
        private const byte WfcDoorOpenFlag = (byte)WfcOutpostCellStateFlags.DoorOpen;
        private const byte WfcDoorUnlockedFlag = (byte)WfcOutpostCellStateFlags.DoorUnlocked;
        private const byte WfcDoorPowerOnFlag = (byte)WfcOutpostCellStateFlags.PowerOn;
        private const uint DoorVfxSpeciesHash = 0x53444F52u; // SDOR
        private const uint DoorSparkSpeciesHash = 0x53445350u; // SDSP

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CUTTING
        // ══════════════════════════════════════════════════════════

        [Header("── Cutting ────────────────────────────────────")]
        [Tooltip("Total cutting time required in seconds.")]
        [SerializeField, Range(0.5f, 30f)] private float requiredCuttingTime = 4f;

        [Tooltip("Can the door be cut? Set to false for permanently sealed doors.")]
        [SerializeField] private bool canBeCut = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VISUALS
        // ══════════════════════════════════════════════════════════

        [Header("── Visuals ─────────────────────────────────────")]
        [Tooltip("Renderer for progress material effect.")]
        [SerializeField] private Renderer doorRenderer;

        [Tooltip("Color of the cutting glow effect.")]
        [SerializeField] private Color cutGlowColor = new Color(1f, 0.5f, 0f); // Orange

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played while cutting.")]
        [SerializeField] private AudioClip cuttingLoopSound;

        [Tooltip("Sound played when door opens.")]
        [SerializeField] private AudioClip openSound;

        [Tooltip("Volume for cutting sound.")]
        [SerializeField, Range(0f, 1f)] private float cuttingVolume = 0.7f;

        [Tooltip("Volume for open sound.")]
        [SerializeField, Range(0f, 1f)] private float openVolume = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — ANIMATION
        // ══════════════════════════════════════════════════════════

        [Header("── Animation ───────────────────────────────────")]
        [Tooltip("Animator for door opening animation.")]
        [SerializeField] private Animator animator;

        [Tooltip("Animation trigger name for opening.")]
        [SerializeField] private string openTriggerName = "Open";

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Events ──────────────────────────────────────")]
        [Tooltip("Invoked when cutting progress changes. Parameter: normalized progress (0-1).")]
        [SerializeField] private UnityEvent<float> OnProgressChanged;

        [Tooltip("Invoked when the door is fully cut and opens.")]
        [SerializeField] private UnityEvent OnDoorOpened;

        [Tooltip("Invoked when cutting starts.")]
        [SerializeField] private UnityEvent OnCuttingStarted;

        [Tooltip("Invoked when cutting stops before completion.")]
        [SerializeField] private UnityEvent OnCuttingStopped;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Collider _collider;
        private DoorState _state = DoorState.Sealed;
        private float _currentProgress;
        private float _lastPublishedProgress = -1f;
        private float _lastVisualProgress = -1f;
        private bool _isBeingCut;
        private ulong _wfcOutpostSectorHash;
        private ushort _wfcOutpostCellIndex;
        private byte _wfcOutpostFlags;
        private bool _wfcOutpostPersistenceConfigured;
        private bool _wfcOutpostLaserUnlocked;
        private IAudioService _cachedAudioService;
        private AbsoluteUniversePosition _cachedRuntimeOriginAup;
        private bool _hasCachedRuntimeOriginAup;
        private bool _lateFrameRegistered;
        private bool _registeredHotSwap;
        private bool _pendingProgressVisualDirty;
        private bool _pendingProgressVisualReset;
        private bool _pendingCuttingVfx;
        private bool _pendingOpenedVfx;
        private bool _pendingCuttingAudio;
        private bool _pendingOpenAudio;
        private bool _pendingOpenAnimatorTrigger;
        private float _pendingProgressVisualValue;

        /// <summary>
        /// Cached MaterialPropertyBlock for progress VFX.
        /// Allocated once in Awake — zero GC in hot path.
        /// </summary>
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — cut progress VFX — owner: SealedDoor

        /// <summary>
        /// Cached animator hash for open trigger.
        /// </summary>
        private int _openTriggerHash;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC WFC API
        // ══════════════════════════════════════════════════════════

        public void ConfigureWfcOutpostPersistence(ulong sectorHash, ushort cellIndex, byte initialFlags)
        {
            if (sectorHash == 0UL || cellIndex >= WfcOutpostPersistenceConstants.CellCount)
            {
                ClearWfcOutpostPersistence();
                ResetState();
                return;
            }

            ResetState();
            _wfcOutpostSectorHash = sectorHash;
            _wfcOutpostCellIndex = cellIndex;
            _wfcOutpostFlags = (byte)(initialFlags & WfcOutpostPersistenceConstants.MutableFlagMask);
            _wfcOutpostPersistenceConfigured = true;
            _wfcOutpostLaserUnlocked =
                (_wfcOutpostFlags & WfcDoorUnlockedFlag) != 0 &&
                (_wfcOutpostFlags & WfcDoorPowerOnFlag) == 0;
            ApplyWfcOutpostFlagsToDoor(_wfcOutpostFlags);
        }

        public void ClearWfcOutpostPersistence()
        {
            _wfcOutpostPersistenceConfigured = false;
            _wfcOutpostSectorHash = 0UL;
            _wfcOutpostCellIndex = 0;
            _wfcOutpostFlags = 0;
            _wfcOutpostLaserUnlocked = false;
        }

        public void ApplyWfcOutpostPowerState(bool poweredAndUnlocked, uint frame)
        {
            if (!_wfcOutpostPersistenceConfigured)
            {
                if (poweredAndUnlocked)
                    Unlock();
                else if (_state != DoorState.Opened)
                    Lock();
                return;
            }

            byte nextFlags = _wfcOutpostFlags;
            if (poweredAndUnlocked)
                nextFlags = (byte)(nextFlags | WfcDoorUnlockedFlag | WfcDoorPowerOnFlag);
            else
            {
                nextFlags = (byte)(nextFlags & ~WfcDoorPowerOnFlag);
                if (!_wfcOutpostLaserUnlocked)
                    nextFlags = (byte)(nextFlags & ~WfcDoorUnlockedFlag);
            }

            if (((_wfcOutpostFlags ^ nextFlags) & WfcOutpostPersistenceConstants.MutableFlagMask) != 0)
                ApplyWfcOutpostFlagsToDoor(nextFlags);

            SetWfcOutpostFlags(nextFlags, frame);
        }

        public bool TryGetWfcOutpostCell(out ulong sectorHash, out ushort cellIndex, out byte flags)
        {
            sectorHash = _wfcOutpostSectorHash;
            cellIndex = _wfcOutpostCellIndex;
            flags = _wfcOutpostFlags;
            return _wfcOutpostPersistenceConfigured &&
                   sectorHash != 0UL &&
                   cellIndex < WfcOutpostPersistenceConstants.CellCount;
        }

        public void ApplyWfcOutpostLaserCutProgress(float progress01, uint frame)
        {
            if (!_wfcOutpostPersistenceConfigured || _state == DoorState.Opened)
                return;

            float clampedProgress = Mathf.Clamp01(progress01);
            _currentProgress = requiredCuttingTime * clampedProgress;

            if (clampedProgress > 0f && clampedProgress < 1f && !_isBeingCut)
                StartCutting();

            if (clampedProgress >= 1f)
            {
                PublishProgress(1f, true);
                StopCutting();
                _wfcOutpostLaserUnlocked = true;
                byte unlockedFlags = (byte)(_wfcOutpostFlags | WfcDoorUnlockedFlag);
                SetWfcOutpostFlags(unlockedFlags, frame);
                ApplyWfcOutpostFlagsToDoor(unlockedFlags);
                if (_state == DoorState.Cutting)
                    _state = DoorState.Sealed;
                return;
            }

            PublishProgress(clampedProgress, false);
        }

        bool IWfcDoorLaserCutTarget.TryReadWfcDoorLaserCutState(out WfcDoorLaserCutReadSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetWfcOutpostCell(out ulong sectorHash, out ushort cellIndex, out byte flags))
                return false;

            snapshot.SectorHash = sectorHash;
            snapshot.CellIndex = cellIndex;
            snapshot.CurrentFlags = flags;
            return true;
        }

        void IWfcDoorLaserCutTarget.ApplyWfcDoorLaserCutProgress(float progress01, uint frame)
        {
            ApplyWfcOutpostLaserCutProgress(progress01, frame);
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            TryGetComponent(out _collider);
            LaserCutterTargetRegistry.RegisterDoor(this, _collider);
            _openTriggerHash = Animator.StringToHash(string.IsNullOrEmpty(openTriggerName) ? "Open" : openTriggerName);
            CacheColdDependencies();
            RefreshCachedRuntimeOriginAup();

            // COLD ALLOC: MaterialPropertyBlock — progress VFX
            _mpb = new MaterialPropertyBlock();

            // Auto-find renderer if not assigned
            if (doorRenderer == null)
                TryResolveOwnedComponent(transform, out doorRenderer);

            // Auto-find animator if not assigned
            if (animator == null)
                TryResolveOwnedComponent(transform, out animator);

            ResetState();
        }

        private void OnEnable()
        {
            CacheColdDependencies();
            RefreshCachedRuntimeOriginAup();
            TryRegisterHotSwapListener();
            LaserCutterTargetRegistry.RegisterDoor(this, _collider);
            InteractableRegistry.RegisterTree(this);
            ResetState();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            LaserCutterTargetRegistry.UnregisterDoor(this, _collider);
            StopLateFrameTicking();
            TryUnregisterHotSwapListener();
            ClearWfcOutpostPersistence();
            ClearColdDependencies();
            _cachedRuntimeOriginAup = default;
            _hasCachedRuntimeOriginAup = false;
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            LaserCutterTargetRegistry.UnregisterDoor(this, _collider);
            StopLateFrameTicking();
            TryUnregisterHotSwapListener();
            ClearColdDependencies();
            _cachedRuntimeOriginAup = default;
            _hasCachedRuntimeOriginAup = false;
        }

        // ══════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                bool shouldRestoreLateFrame = _lateFrameRegistered || HasPendingLateFrameWork();
                StopLateFrameTicking();
                if (shouldRestoreLateFrame && currentService != null && isActiveAndEnabled)
                    StartLateFrameTicking();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                CacheAudioService(currentService as IAudioService);
        }

        // ══════════════════════════════════════════════════════════
        //  ICuttable — LASER CUTTER INTEGRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by LaserCutter when hitting this door.
        /// Applies cutting progress based on damage amount.
        /// </summary>
        /// <param name="damage">Damage amount (typically damagePerSecond × deltaTime).</param>
        /// <param name="hitPoint">World position of the hit.</param>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            // Convert damage to cutting time
            // Assuming damage is per-second rate, damage × deltaTime = progress
            ApplyCutting(damage, hitPoint);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CUTTING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Applies cutting progress. Called by tools every frame while cutting.
        /// </summary>
        /// <param name="amount">Cutting progress amount (typically deltaTime).</param>
        /// <param name="hitPoint">World position of the laser hit.</param>
        public void ApplyCutting(float amount, Vector3 hitPoint)
        {
            if (!canBeCut) return;
            if (_state == DoorState.Opened) return;
            if (_state == DoorState.Locked) return;
            if (amount <= 0f) return;

            // Start cutting if first hit
            if (_state == DoorState.Sealed && !_isBeingCut)
            {
                StartCutting();
            }

            // Add progress
            _currentProgress += amount;

            // Check for completion
            if (_currentProgress >= requiredCuttingTime)
            {
                PublishProgress(1f, true);
                OpenDoor();
                return;
            }

            PublishProgress(ReadProgressNormalized(), false);
        }

        /// <summary>
        /// Overload without hit point (uses door center).
        /// </summary>
        /// <param name="amount">Cutting progress amount.</param>
        public void ApplyCutting(float amount)
        {
            ApplyCutting(amount, _transform.position);
        }

        /// <summary>
        /// Stops the cutting process (called when tool stops hitting door).
        /// </summary>
        public void StopCutting()
        {
            if (!_isBeingCut) return;

            _isBeingCut = false;

            // Fire stopped event
            OnCuttingStopped?.Invoke();

        }

        private float ReadProgressNormalized()
        {
            return requiredCuttingTime > 0f ? _currentProgress / requiredCuttingTime : 0f;
        }

        /// <summary>
        /// Resets the door to sealed state (for testing or special gameplay).
        /// </summary>
        public void ResetDoor()
        {
            _wfcOutpostLaserUnlocked = false;
            ResetState();
            SetWfcOutpostFlags(0, ResolveCurrentFrameId());
        }

        /// <summary>
        /// Locks the door so it cannot be cut.
        /// </summary>
        public void Lock()
        {
            if (_state == DoorState.Opened)
                return;

            _state = DoorState.Locked;
            _wfcOutpostLaserUnlocked = false;
            StopCutting();
            SetWfcOutpostFlags((byte)(_wfcOutpostFlags & ~WfcDoorUnlockedFlag), ResolveCurrentFrameId());
        }

        /// <summary>
        /// Unlocks the door so it can be cut.
        /// </summary>
        public void Unlock()
        {
            if (_state == DoorState.Locked)
            {
                _state = DoorState.Sealed;
                SetWfcOutpostFlags((byte)(_wfcOutpostFlags | WfcDoorUnlockedFlag), ResolveCurrentFrameId());
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════════════

        private void StartCutting()
        {
            _state = DoorState.Cutting;
            _isBeingCut = true;

            QueueDoorGpuVfx(DebrisSpawnSignal.DebrisKindSparks);

            QueueDoorAudio(cutting: true);

            // Fire started event
            OnCuttingStarted?.Invoke();

            // No dispatcher registration: cutting is driven by tool-hit cadence and Tick is intentionally empty.
        }

        private void OpenDoor()
        {
            _state = DoorState.Opened;
            _isBeingCut = false;

            QueueDoorGpuVfx(DebrisSpawnSignal.DebrisKindRockShard);

            QueueDoorAudio(cutting: false);
            QueueOpenAnimatorTrigger();

            // Disable collider
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            // Optionally disable renderer (if no animation)
            // doorRenderer.enabled = false;

            SetWfcOutpostFlags((byte)(_wfcOutpostFlags | WfcDoorOpenFlag), ResolveCurrentFrameId());

            // Fire opened event
            OnDoorOpened?.Invoke();

        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private void PublishProgress(float progressNormalized, bool force)
        {
            float clampedProgress = Mathf.Clamp01(progressNormalized);
            if (force || ShouldPublishProgress(clampedProgress, _lastVisualProgress))
            {
                QueueProgressVisuals(clampedProgress);
                _lastVisualProgress = clampedProgress;
            }

            if (force || ShouldPublishProgress(clampedProgress, _lastPublishedProgress))
            {
                _lastPublishedProgress = clampedProgress;
                OnProgressChanged?.Invoke(clampedProgress);
            }
        }

        private static bool ShouldPublishProgress(float currentProgress, float lastProgress)
        {
            return lastProgress < 0f ||
                   currentProgress <= 0f ||
                   currentProgress >= 1f ||
                   Mathf.Abs(currentProgress - lastProgress) >= ProgressPublishEpsilon;
        }

        private void QueueProgressVisuals(float progressNormalized)
        {
            _pendingProgressVisualValue = math.saturate(progressNormalized);
            _pendingProgressVisualDirty = true;
            _pendingProgressVisualReset = false;
            StartLateFrameTicking();
        }

        private void UpdateProgressVisuals(float progressNormalized)
        {
            if (doorRenderer == null) return;
            if (_mpb == null) return;

            // Update shader properties
            doorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_ProgressID, progressNormalized);
            _mpb.SetColor(_GlowColorID, cutGlowColor);
            doorRenderer.SetPropertyBlock(_mpb);
        }

        private void ResetProgressVisuals()
        {
            _pendingProgressVisualValue = 0f;
            _pendingProgressVisualDirty = true;
            _pendingProgressVisualReset = true;
            _lastVisualProgress = 0f;
            StartLateFrameTicking();
        }

        private void ApplyProgressVisualsReset()
        {
            if (doorRenderer == null) return;
            if (_mpb == null) return;

            doorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_ProgressID, 0f);
            doorRenderer.SetPropertyBlock(_mpb);
        }

        public void LateFrameTick()
        {
            if (_pendingProgressVisualDirty)
            {
                _pendingProgressVisualDirty = false;
                if (_pendingProgressVisualReset)
                    ApplyProgressVisualsReset();
                else
                    UpdateProgressVisuals(_pendingProgressVisualValue);
                _pendingProgressVisualReset = false;
            }

            if (_pendingCuttingVfx)
            {
                _pendingCuttingVfx = false;
                PublishDoorGpuVfx(0.55f, DebrisSpawnSignal.DebrisKindSparks);
            }

            if (_pendingOpenedVfx)
            {
                _pendingOpenedVfx = false;
                PublishDoorGpuVfx(1f, DebrisSpawnSignal.DebrisKindRockShard);
            }

            IAudioService audio = ResolveAudioService();
            if (_pendingCuttingAudio)
            {
                _pendingCuttingAudio = false;
                if (cuttingLoopSound != null && audio != null)
                    audio.PlayAtPoint(cuttingLoopSound, _transform.position, cuttingVolume);
            }

            if (_pendingOpenAudio)
            {
                _pendingOpenAudio = false;
                if (openSound != null && audio != null)
                    audio.PlayAtPoint(openSound, _transform.position, openVolume);
            }

            if (_pendingOpenAnimatorTrigger)
            {
                _pendingOpenAnimatorTrigger = false;
                if (animator != null)
                    animator.SetTrigger(_openTriggerHash);
            }

            StopLateFrameTicking();
        }

        private void QueueDoorGpuVfx(byte debrisKind)
        {
            if (debrisKind == DebrisSpawnSignal.DebrisKindSparks)
                _pendingCuttingVfx = true;
            else
                _pendingOpenedVfx = true;
            StartLateFrameTicking();
        }

        private void QueueDoorAudio(bool cutting)
        {
            if (cutting)
                _pendingCuttingAudio = true;
            else
                _pendingOpenAudio = true;
            StartLateFrameTicking();
        }

        private void QueueOpenAnimatorTrigger()
        {
            _pendingOpenAnimatorTrigger = true;
            StartLateFrameTicking();
        }

        private void StartLateFrameTicking()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void StopLateFrameTicking()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
        }

        private bool HasPendingLateFrameWork()
        {
            return _pendingProgressVisualDirty
                || _pendingCuttingVfx
                || _pendingOpenedVfx
                || _pendingCuttingAudio
                || _pendingOpenAudio
                || _pendingOpenAnimatorTrigger;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        // ══════════════════════════════════════════════════════════
        //  STATE RESET
        // ══════════════════════════════════════════════════════════

        private void ResetState()
        {
            _state = canBeCut ? DoorState.Sealed : DoorState.Locked;
            _currentProgress = 0f;
            _lastPublishedProgress = -1f;
            _lastVisualProgress = -1f;
            _isBeingCut = false;

            // Reset visuals
            ResetProgressVisuals();

            // Re-enable collider
            if (_collider != null)
            {
                _collider.enabled = true;
            }

            // Reset animator
            if (animator != null)
            {
                animator.Rebind();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════

        private void PublishDoorGpuVfx(float intensity01, byte debrisKind)
        {
            if (_transform == null)
                return;

            float intensity = math.saturate(intensity01);
            if (!RefreshCachedRuntimeOriginAup() ||
                !TryResolveDoorAup(_transform.position, in _cachedRuntimeOriginAup, out double3 centerAup))
                return;

            float quality = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            float maxQuantity = debrisKind == DebrisSpawnSignal.DebrisKindSparks ? 96f : 48f;
            ushort quantity = (ushort)math.clamp((int)math.round(math.lerp(0f, maxQuantity, quality) * intensity), 0, ushort.MaxValue);
            uint source = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId()));
            DebrisSpawnSignal debris = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(centerAup),
                SpeciesHash = debrisKind == DebrisSpawnSignal.DebrisKindSparks ? DoorSparkSpeciesHash : DoorVfxSpeciesHash,
                SourceEntityId = source,
                Intensity01 = intensity,
                DebrisKind = debrisKind,
                Flags = (byte)(debrisKind == DebrisSpawnSignal.DebrisKindSparks
                    ? DebrisSpawnSignal.FlagToolSparks | DebrisSpawnSignal.FlagComputeShard
                    : DebrisSpawnSignal.FlagComputeShard),
                Quantity = quantity
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in debris, ref s_x001SealedDoorSignalPushDropCount);
        }

        private static bool TryResolveDoorAup(Vector3 runtimePosition, in AbsoluteUniversePosition originAup, out double3 doorAup)
        {
            doorAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            doorAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(doorAup));
        }

        private bool RefreshCachedRuntimeOriginAup()
        {
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
            {
                _cachedRuntimeOriginAup = default;
                _hasCachedRuntimeOriginAup = false;
                return false;
            }

            _cachedRuntimeOriginAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            _hasCachedRuntimeOriginAup = _cachedRuntimeOriginAup.IsFinite();
            return _hasCachedRuntimeOriginAup;
        }

        private void ApplyWfcOutpostFlagsToDoor(byte flags)
        {
            flags = (byte)(flags & WfcOutpostPersistenceConstants.MutableFlagMask);
            if ((flags & WfcDoorOpenFlag) != 0)
            {
                ApplyOpenedStateFromPersistence();
                return;
            }

            if ((flags & WfcDoorUnlockedFlag) != 0)
            {
                if (_state == DoorState.Locked)
                    _state = DoorState.Sealed;
            }
            else if (_state != DoorState.Opened)
            {
                _state = DoorState.Locked;
                StopCutting();
            }

            if (_collider != null)
                _collider.enabled = true;
        }

        private void ApplyOpenedStateFromPersistence()
        {
            _state = DoorState.Opened;
            _isBeingCut = false;
            _currentProgress = requiredCuttingTime;
            _lastPublishedProgress = 1f;
            _lastVisualProgress = 1f;

            QueueProgressVisuals(1f);

            QueueOpenAnimatorTrigger();

            if (_collider != null)
                _collider.enabled = false;
        }

        private void SetWfcOutpostFlags(byte flags, uint frame)
        {
            byte previous = _wfcOutpostFlags;
            byte current = (byte)(flags & WfcOutpostPersistenceConstants.MutableFlagMask);
            _wfcOutpostFlags = current;
            PublishWfcOutpostFlags(previous, current, frame);
        }

        private void PublishWfcOutpostFlags(byte previous, byte current, uint frame)
        {
            if (!_wfcOutpostPersistenceConfigured)
                return;

            previous = (byte)(previous & WfcOutpostPersistenceConstants.MutableFlagMask);
            current = (byte)(current & WfcOutpostPersistenceConstants.MutableFlagMask);
            if (previous == current)
                return;

            WfcOutpostStateChangedSignal signal = new WfcOutpostStateChangedSignal
            {
                SectorHash = _wfcOutpostSectorHash,
                CellIndex = _wfcOutpostCellIndex,
                PreviousFlags = previous,
                CurrentFlags = current,
                Frame = frame,
                SourceHash = WfcOutpostDoorSourceHash,
                Flags = 0
            };
            SignalBus<WfcOutpostStateChangedSignal>.TryPushTracked(in signal, ref s_x001SealedDoorSignalPushDropCount);
        }

        private static uint ResolveCurrentFrameId()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame != 0u ? frame : 1u;
        }

        private void CacheColdDependencies()
        {
            CacheAudioService(GlobalRegistry.Audio);
        }

        private void ClearColdDependencies()
        {
            _cachedAudioService = null;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsInitialized)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static bool TryResolveOwnedComponent<T>(Transform root, out T component) where T : Component
        {
            component = null;
            if (root == null)
                return false;

            if (root.TryGetComponent(out component))
                return true;

            for (int i = 0; i < root.childCount; i++)
            {
                if (TryResolveOwnedComponent(root.GetChild(i), out component))
                    return true;
            }

            return false;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (requiredCuttingTime <= 0f)
            {
                requiredCuttingTime = 1f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw door bounds
            Gizmos.color = _state == DoorState.Opened
                ? new Color(0f, 1f, 0f, 0.3f)
                : new Color(1f, 0.5f, 0f, 0.3f);

            if (_collider != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                if (_collider is BoxCollider box)
                {
                    Gizmos.DrawWireCube(box.center, box.size);
                }
            }
        }
#endif
    }
}

