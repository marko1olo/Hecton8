// ============================================================================
// HECTON-8 — ScannableFragment.cs
// Debris on the ocean floor that the player scans to unlock tech.
//
// ARCHITECTURE:
//   • Standalone prop — implements IInteractable for scanner detection.
//   • Progress-based scanning system.
//   • MaterialPropertyBlock for scan glow effect.
//   • UnityEvents for scan completion.
//
// ZERO GC:
//   • No Update loop — event-driven via OnScan().
//   • Cached Transform, Renderer.
//   • MaterialPropertyBlock for VFX.
//   • Pre-cached interaction text.
//
// USAGE:
//   1. Place on debris GameObject with mesh.
//   2. Configure scan time and unlock data.
//   3. Connect OnScanComplete to tech unlock system.
//   4. Scanner tool calls OnScan(progressDelta) each frame.
// ============================================================================

using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Data;
using Hecton8.Interaction;
using Hecton8.Narrative;
using Hecton8.World;
using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State machine states for fragment scanning.
    /// </summary>
    public enum FragmentState
    {
        Scannable,   // Ready to be scanned
        Scanning,    // Currently being scanned
        Completed,   // Scan finished, fragment disabled
        Locked       // Cannot be scanned
    }

    /// <summary>
    /// Scannable debris fragment that unlocks tech when scanned.
    /// Implements IInteractable for scanner tool integration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScannableFragment : MonoBehaviour, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        private static readonly int _ScanProgressID = Shader.PropertyToID("_ScanProgress");
        private static readonly int _ScanGlowColorID = Shader.PropertyToID("_ScanGlowColor");
        private static readonly int _ScanPulseID = Shader.PropertyToID("_ScanPulse");
        private const byte QuarterLoreStageBit = 1 << 0;
        private const byte HalfLoreStageBit = 1 << 1;
        private const byte FinalLoreStageBit = 1 << 2;
        private const byte PendingScanEventStarted = 1 << 0;
        private const byte PendingScanEventStopped = 1 << 1;
        private const byte PendingScanEventComplete = 1 << 2;
        private const byte PendingScanEventProgress = 1 << 3;
        private const string DefaultResearchCategory = "Research";
        private const string DefaultResearchSummary = "Scientific scan archived to the suit research ledger.";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SCANNING
        // ══════════════════════════════════════════════════════════

        [Header("── Scanning ────────────────────────────────────")]
        [Tooltip("Total scan time required in seconds.")]
        [SerializeField, Range(0.5f, 30f)] private float scanTime = 3f;

        [Tooltip("Can this fragment be scanned?")]
        [SerializeField] private bool canBeScanned = true;

        [Tooltip("ID of the tech/blueprint this fragment unlocks.")]
        [SerializeField] private string unlockId = "unknown_tech";

        [Tooltip("Optional scientific research contract that owns scan duration, lore unlock stages, and reward hashes.")]
        [SerializeField] private ResearchDataTemplate researchData;

        [Header("── Applied Lore ────────────────────────────────")]
        [Tooltip("Optional AppliedContent packet unlocked when scan progress crosses 25%.")]
        [SerializeField] private uint appliedLoreQuarterPacketHash;

        [Tooltip("Optional AppliedContent packet unlocked when scan progress crosses 50%.")]
        [SerializeField] private uint appliedLoreHalfPacketHash;

        [Tooltip("Optional AppliedContent packet unlocked when scan completes.")]
        [SerializeField] private uint appliedLoreFinalPacketHash;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VISUALS
        // ══════════════════════════════════════════════════════════

        [Header("── Visuals ─────────────────────────────────────")]
        [Tooltip("Renderer for scan glow effect.")]
        [SerializeField] private Renderer fragmentRenderer;

        [Tooltip("Color of the scan glow effect.")]
        [SerializeField] private Color scanGlowColor = new Color(0.3f, 0.8f, 1f); // Cyan

        [Tooltip("Particle system for scan completion.")]
        [SerializeField] private ParticleSystem completeParticles;

        [Tooltip("Vibration amplitude during scan.")]
        [SerializeField, Range(0f, 0.1f)] private float vibrationAmplitude = 0.02f;

        [Tooltip("Vibration frequency during scan.")]
        [SerializeField, Range(0f, 20f)] private float vibrationFrequency = 5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played during scanning.")]
        [SerializeField] private AudioClip scanningSound;

        [Tooltip("Sound played when scan completes.")]
        [SerializeField] private AudioClip completeSound;

        [Tooltip("Volume for scan sounds.")]
        [SerializeField, Range(0f, 1f)] private float scanVolume = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — INTERACTION
        // ══════════════════════════════════════════════════════════

        [Header("── Interaction ──────────────────────────────────")]
        [Tooltip("Interaction text shown in HUD.")]
        [SerializeField] private string interactText = DefaultInteractText;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Events ──────────────────────────────────────")]
        private const string DefaultInteractText = "Scan Fragment";

        [Tooltip("Invoked when scan progress changes. Parameter: normalized progress (0-1).")]
        [SerializeField] private UnityEvent<float> OnProgressChanged;

        [Tooltip("Invoked when scan completes. Parameter: unlock ID.")]
        [SerializeField] private UnityEvent<string> OnScanComplete;

        [Tooltip("Invoked when scanning starts.")]
        [SerializeField] private UnityEvent OnScanStarted;

        [Tooltip("Invoked when scanning stops before completion.")]
        [SerializeField] private UnityEvent OnScanStopped;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Collider _collider;
        private FragmentState _state = FragmentState.Scannable;
        private float _currentProgress;
        private bool _isScanning;
        private bool _scanRenderRegistered;
        private uint _scanRenderAddedFlags;
        private int _spatialHandle;
        private byte _appliedLoreStagesMask;
        private float _scanPulsePhase;
        private IAudioService _audioService;
        private ILocalizationTextReadModel _localization;
        private LoreDatabaseManager _loreDatabase;
        private bool _hotSwapRegistered;
        private bool _lateFrameRegistered;
        private bool _pendingScanVisualDirty;
        private bool _pendingScanVisualReset;
        private bool _pendingScanProxyActive;
        private bool _pendingScanProxyDirty;
        private bool _pendingScanningAudio;
        private bool _pendingCompleteAudio;
        private bool _pendingCompleteParticles;
        private bool _pendingFragmentDisable;
        private bool _pendingRendererEnableDirty;
        private bool _pendingRendererEnabled;
        private byte _pendingScanEventMask;
        private float _pendingScanVisualProgress;
        private float _pendingScanVisualPulse;
        private float _pendingProgressEventValue;
        private Vector3 _pendingCompleteParticlePosition;
        private Mesh _cachedSharedMesh;
        private string _pendingCompleteEventUnlockId;

        /// <summary>
        /// Cached MaterialPropertyBlock for scan VFX.
        /// Allocated once in Awake — zero GC in hot path.
        /// </summary>
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — scan progress VFX — owner: ScannableFragment

        /// <summary>
        /// Pre-cached interaction text to avoid runtime allocations.
        /// </summary>
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;
        private uint _discoveryHash;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Current state of the fragment.</summary>
        public FragmentState State => _state;

        /// <summary>Current scan progress (0 to scanTime).</summary>
        public float CurrentProgress => _currentProgress;

        /// <summary>Normalized progress (0 to 1).</summary>
        public float ProgressNormalized
        {
            get
            {
                float duration = ResolveScanDuration();
                return duration > 0f ? _currentProgress / duration : 0f;
            }
        }

        /// <summary>Is the fragment fully scanned?</summary>
        public bool IsCompleted => _state == FragmentState.Completed;

        /// <summary>Can the fragment be scanned?</summary>
        public bool CanBeScanned => canBeScanned && _state == FragmentState.Scannable;

        /// <summary>ID of the tech this fragment unlocks.</summary>
        public string UnlockId => unlockId;

        /// <summary>Optional authored research contract for this fragment.</summary>
        public ResearchDataTemplate ResearchData => researchData;

        /// <summary>Reward item hash used for visor hologram lookup.</summary>
        public int RewardItemHash => researchData != null ? researchData.RewardItemHash : 0;

        /// <summary>Proxy mesh index resolved from the authored research reward hash.</summary>
        public int HologramProxyMeshIndex => researchData != null ? researchData.HologramProxyMeshIndex : -1;

        /// <summary>Stable uint hash used by archaeology and encyclopedia unlocks.</summary>
        public uint DiscoveryHash => _discoveryHash;

        /// <summary>Resolved scan duration in seconds.</summary>
        public float ScanDurationSeconds => researchData != null ? researchData.ScanDuration : Mathf.Max(0.5f, scanTime);

        /// <summary>Cold-cached source mesh used by archaeology reconstruction.</summary>
        public Mesh CachedSharedMesh => _cachedSharedMesh;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            _collider = ComponentReferenceUtility.ResolveOwnedComponent<Collider>(transform);
            RefreshDiscoveryHash();

            // COLD ALLOC: MaterialPropertyBlock[1] - scan progress VFX - owner: ScannableFragment
            _mpb = new MaterialPropertyBlock();

            // Auto-find renderer if not assigned
            if (fragmentRenderer == null)
            {
                fragmentRenderer = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);
            }

            CacheSharedMeshCold();
            CacheRegistryServicesCold();
            RebuildLocalizedTextCache();

            ResetState();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            RegisterLateFrameTickingCold();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            RefreshDiscoveryHash();
            CacheSharedMeshCold();
            RebuildLocalizedTextCache();
            ResetState();
            RegisterSpatialContact();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            UnregisterSpatialContact();
            ClearQueuedLateFrameWork();
            StopLateFrameTicking();
            UnregisterScanRenderProxy();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            UnregisterSpatialContact();
            ClearQueuedLateFrameWork();
            StopLateFrameTicking();
        }

        private void RegisterSpatialContact()
        {
            if (_spatialHandle != 0)
                return;

            _spatialHandle = WorldSpatialHashGrid.RegisterScannable(this);
        }

        private void UnregisterSpatialContact()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

        void IInteractable.OnHoverStart()
        {
            // Could trigger highlight effect here
        }

        void IInteractable.OnHoverEnd()
        {
            // Could disable highlight effect here
        }

        void IInteractable.Interact(Transform interactor)
        {
            // Scanner tool handles interaction via OnScan()
            // This is for direct interaction if needed
        }

        string IInteractable.GetInteractText()
        {
            return CanBeScanned ? ResolveLegacyConfigured(interactText, DefaultInteractText) : null;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            ReadOnlySpan<char> source = CanBeScanned
                ? _cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength)
                : ReadOnlySpan<char>.Empty;
            return InteractableTextCopy.TryCopy(source, destination, out length);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — SCANNING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by ScannerTool each frame while scanning.
        /// Adds progress and updates visuals.
        /// </summary>
        /// <param name="progressDelta">Progress amount (typically deltaTime).</param>
        public void OnScan(float progressDelta)
        {
            if (!canBeScanned) return;
            if (_state == FragmentState.Completed) return;
            if (_state == FragmentState.Locked) return;
            if (progressDelta <= 0f) return;

            // Start scanning if first call
            if (!_isScanning)
            {
                StartScanning();
            }

            float previousProgressNormalized = ProgressNormalized;

            // Add progress
            float safeProgressDelta = math.max(0f, progressDelta);
            float scanDuration = ResolveScanDuration();
            _currentProgress = math.min(_currentProgress + safeProgressDelta, scanDuration);

            float currentProgressNormalized = ProgressNormalized;
            TryUnlockLoreStages(previousProgressNormalized, currentProgressNormalized);

            // Update visuals
            QueueScanVisuals(currentProgressNormalized, AdvanceScanPulse(safeProgressDelta));

            QueueProgressChangedEvent(currentProgressNormalized);

            // Check for completion
            if (_currentProgress >= scanDuration)
            {
                CompleteScan();
            }
        }

        /// <summary>
        /// Stops the scanning process (called when scanner stops hitting fragment).
        /// </summary>
        public void StopScanning()
        {
            if (!_isScanning) return;

            _isScanning = false;
            _state = FragmentState.Scannable;
            _scanPulsePhase = 0f;
            QueueScanRenderProxyState(false);

            // Reset visuals
            QueueScanVisualReset();

            QueueScanStoppedEvent();
        }

        /// <summary>
        /// Resets the fragment to scannable state (for testing or special gameplay).
        /// </summary>
        public void ResetFragment()
        {
            ResetState();
        }

        internal void RestoreProgressNormalized(float progress01)
        {
            if (_state == FragmentState.Completed || _state == FragmentState.Locked)
                return;

            float previousProgressNormalized = ProgressNormalized;
            float duration = ResolveScanDuration();
            float restoredProgress = math.saturate(progress01) * duration;
            if (restoredProgress <= _currentProgress)
                return;

            _currentProgress = math.min(restoredProgress, duration);
            TryUnlockLoreStages(previousProgressNormalized, ProgressNormalized);
            QueueScanVisuals(ProgressNormalized, AdvanceScanPulse(0f));
            QueueProgressChangedEvent(ProgressNormalized);
        }

        /// <summary>
        /// Locks the fragment so it cannot be scanned.
        /// </summary>
        public void Lock()
        {
            StopScanning();
            _state = FragmentState.Locked;
        }

        /// <summary>
        /// Unlocks the fragment so it can be scanned.
        /// </summary>
        public void Unlock()
        {
            if (_state == FragmentState.Locked)
            {
                _state = FragmentState.Scannable;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════════════

        private void StartScanning()
        {
            _state = FragmentState.Scanning;
            _isScanning = true;
            QueueScanRenderProxyState(true);

            QueueScanAudio(complete: false);

            QueueScanStartedEvent();
        }

        private void CompleteScan()
        {
            _state = FragmentState.Completed;
            _isScanning = false;
            _scanPulsePhase = 0f;
            QueueScanRenderProxyState(false);

            QueueCompletePresentation();

            QueueScanCompleteEvent(unlockId);
            EmitResearchDiscoveryEvent();

            QueueProgressChangedEvent(1f);

            // Disable the fragment
            QueueFragmentDisable();
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private float AdvanceScanPulse(float deltaTime)
        {
            if (vibrationAmplitude <= 0f)
                return 0f;

            _scanPulsePhase = math.frac(_scanPulsePhase + math.max(0.01f, vibrationFrequency) * deltaTime);
            return 1f - math.abs((_scanPulsePhase * 2f) - 1f);
        }

        private void QueueScanVisuals(float progress, float pulse)
        {
            _pendingScanVisualProgress = math.saturate(progress);
            _pendingScanVisualPulse = math.saturate(pulse);
            _pendingScanVisualDirty = true;
            _pendingScanVisualReset = false;
            StartLateFrameTicking();
        }

        private void UpdateScanVisuals(float progress, float pulse = 0f)
        {
            // Update shader properties
            if (fragmentRenderer != null && _mpb != null)
            {
                fragmentRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_ScanProgressID, progress);
                _mpb.SetColor(_ScanGlowColorID, scanGlowColor);
                _mpb.SetFloat(_ScanPulseID, pulse);

                fragmentRenderer.SetPropertyBlock(_mpb);
            }
        }

        private void ResetScanVisuals()
        {
            _pendingScanVisualProgress = 0f;
            _pendingScanVisualPulse = 0f;
            _pendingScanVisualDirty = true;
            _pendingScanVisualReset = true;
            StartLateFrameTicking();
        }

        private void QueueScanVisualReset()
        {
            ResetScanVisuals();
        }

        private void ApplyScanVisualReset()
        {
            if (fragmentRenderer == null || _mpb == null) return;

            fragmentRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_ScanProgressID, 0f);
            _mpb.SetFloat(_ScanPulseID, 0f);
            fragmentRenderer.SetPropertyBlock(_mpb);
        }

        public void LateFrameTick()
        {
            if (!HasPendingLateFrameWork())
                return;

            if (_pendingScanProxyDirty)
            {
                _pendingScanProxyDirty = false;
                if (_pendingScanProxyActive)
                    RegisterScanRenderProxy();
                else
                    UnregisterScanRenderProxy();
            }

            if (_pendingScanVisualDirty)
            {
                _pendingScanVisualDirty = false;
                if (_pendingScanVisualReset)
                    ApplyScanVisualReset();
                else
                    UpdateScanVisuals(_pendingScanVisualProgress, _pendingScanVisualPulse);
                _pendingScanVisualReset = false;
            }

            if (_pendingRendererEnableDirty)
            {
                _pendingRendererEnableDirty = false;
                if (fragmentRenderer != null)
                    fragmentRenderer.enabled = _pendingRendererEnabled;
            }

            IAudioService audio = ResolveAudioService();
            if (_pendingScanningAudio)
            {
                _pendingScanningAudio = false;
                if (scanningSound != null && audio != null)
                    audio.PlayAtPoint(scanningSound, _transform.position, scanVolume);
            }

            if (_pendingCompleteParticles)
            {
                _pendingCompleteParticles = false;
                if (completeParticles != null)
                {
                    completeParticles.transform.position = _pendingCompleteParticlePosition;
                    completeParticles.Play();
                }
            }

            if (_pendingCompleteAudio)
            {
                _pendingCompleteAudio = false;
                if (completeSound != null && audio != null)
                    audio.PlayAtPoint(completeSound, _transform.position, scanVolume);
            }

            bool disableFragment = _pendingFragmentDisable;
            _pendingFragmentDisable = false;

            FlushQueuedScanEvents();

            if (disableFragment)
            {
                DisableFragment();
                return;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE RESET
        // ══════════════════════════════════════════════════════════

        private void ResetState()
        {
            ClearQueuedLateFrameWork();
            QueueScanRenderProxyState(false);
            _state = canBeScanned ? FragmentState.Scannable : FragmentState.Locked;
            _currentProgress = 0f;
            _isScanning = false;
            _appliedLoreStagesMask = 0;
            _scanPulsePhase = 0f;

            // Reset visuals
            QueueScanVisualReset();

            // Re-enable renderer
            QueueRendererEnabled(true);
        }

        private void QueueScanRenderProxyState(bool active)
        {
            _pendingScanProxyActive = active;
            _pendingScanProxyDirty = true;
            StartLateFrameTicking();
        }

        private void QueueScanAudio(bool complete)
        {
            if (complete)
                _pendingCompleteAudio = true;
            else
                _pendingScanningAudio = true;
            StartLateFrameTicking();
        }

        private void QueueCompletePresentation()
        {
            _pendingCompleteParticlePosition = _transform != null ? _transform.position : transform.position;
            _pendingCompleteParticles = true;
            QueueScanAudio(complete: true);
        }

        private void QueueFragmentDisable()
        {
            _pendingFragmentDisable = true;
            StartLateFrameTicking();
        }

        private void QueueRendererEnabled(bool enabled)
        {
            _pendingRendererEnabled = enabled;
            _pendingRendererEnableDirty = true;
            StartLateFrameTicking();
        }

        private bool HasPendingLateFrameWork()
        {
            return _pendingScanProxyDirty ||
                   _pendingScanVisualDirty ||
                   _pendingRendererEnableDirty ||
                   _pendingScanningAudio ||
                   _pendingCompleteAudio ||
                   _pendingCompleteParticles ||
                   _pendingFragmentDisable ||
                   _pendingScanEventMask != 0;
        }

        private void QueueScanStartedEvent()
        {
            _pendingScanEventMask |= PendingScanEventStarted;
        }

        private void QueueScanStoppedEvent()
        {
            _pendingScanEventMask |= PendingScanEventStopped;
        }

        private void QueueScanCompleteEvent(string completedUnlockId)
        {
            _pendingCompleteEventUnlockId = completedUnlockId ?? string.Empty;
            _pendingScanEventMask |= PendingScanEventComplete;
        }

        private void QueueProgressChangedEvent(float normalizedProgress)
        {
            _pendingProgressEventValue = math.saturate(normalizedProgress);
            _pendingScanEventMask |= PendingScanEventProgress;
        }

        private void FlushQueuedScanEvents()
        {
            byte mask = _pendingScanEventMask;
            if (mask == 0)
                return;

            float progress = _pendingProgressEventValue;
            string completedUnlockId = _pendingCompleteEventUnlockId;
            _pendingScanEventMask = 0;
            _pendingProgressEventValue = 0f;
            _pendingCompleteEventUnlockId = null;

            if ((mask & PendingScanEventStarted) != 0)
                OnScanStarted?.Invoke();
            if ((mask & PendingScanEventProgress) != 0)
                OnProgressChanged?.Invoke(progress);
            if ((mask & PendingScanEventComplete) != 0)
                OnScanComplete?.Invoke(completedUnlockId ?? string.Empty);
            if ((mask & PendingScanEventStopped) != 0)
                OnScanStopped?.Invoke();
        }

        private void ClearQueuedLateFrameWork()
        {
            _pendingScanProxyActive = false;
            _pendingScanProxyDirty = false;
            _pendingScanVisualProgress = 0f;
            _pendingScanVisualPulse = 0f;
            _pendingScanVisualDirty = false;
            _pendingScanVisualReset = false;
            _pendingRendererEnableDirty = false;
            _pendingScanningAudio = false;
            _pendingCompleteAudio = false;
            _pendingCompleteParticles = false;
            _pendingFragmentDisable = false;
            _pendingCompleteParticlePosition = default;
            _pendingScanEventMask = 0;
            _pendingProgressEventValue = 0f;
            _pendingCompleteEventUnlockId = null;
        }

        private void StartLateFrameTicking()
        {
            // Registration is lifecycle-cold in OnEnable. Hot scan/event paths only set fixed pending fields.
        }

        private void RegisterLateFrameTickingCold()
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

        private void RegisterScanRenderProxy()
        {
            if (_scanRenderRegistered || fragmentRenderer == null)
                return;

            const uint requestedFlags = HectonScanRenderFlags.IsScanned | HectonScanRenderFlags.Environment;
            bool alreadyRegistered = HectonScanRenderRegistry.TryGetFlags(fragmentRenderer, out uint existingFlags);
            _scanRenderAddedFlags = requestedFlags & ~existingFlags;
            _scanRenderRegistered = alreadyRegistered
                ? HectonScanRenderRegistry.SetFlags(fragmentRenderer, requestedFlags, true)
                : HectonScanRenderRegistry.Register(fragmentRenderer, requestedFlags);
        }

        private void UnregisterScanRenderProxy()
        {
            if (!_scanRenderRegistered)
                return;

            if (fragmentRenderer != null && _scanRenderAddedFlags != HectonScanRenderFlags.None)
                HectonScanRenderRegistry.SetFlags(fragmentRenderer, _scanRenderAddedFlags, false);

            _scanRenderAddedFlags = HectonScanRenderFlags.None;
            _scanRenderRegistered = false;
        }

        private void DisableFragment()
        {
            // Disable renderer
            if (fragmentRenderer != null)
            {
                fragmentRenderer.enabled = false;
            }

            // Disable collider
            if (_collider != null)
                _collider.enabled = false;

            // Disable this script
            this.enabled = false;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (scanTime <= 0f)
            {
                scanTime = 1f;
            }

            RefreshDiscoveryHash();
            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw scan indicator
            Gizmos.color = _state == FragmentState.Completed
                ? new Color(0f, 1f, 0.5f, 0.3f)
                : new Color(0.3f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
#endif

        private void RebuildLocalizedTextCache()
        {
            _cachedInteractTextLength = InteractableTextCopy.CopyConfiguredOrLocalizedTruncated(
                interactText,
                DefaultInteractText,
                LocalizationKeys.INTERACT_SCAN_FRAGMENT,
                _localization,
                _cachedInteractTextBuffer);
        }

        private bool HasCustomInteractText()
        {
            return !string.IsNullOrWhiteSpace(interactText) &&
                   !string.Equals(interactText, DefaultInteractText, System.StringComparison.Ordinal);
        }

        private static string ResolveLegacyConfigured(string configuredText, string defaultText)
        {
            return !string.IsNullOrWhiteSpace(configuredText) &&
                   !string.Equals(configuredText, defaultText, StringComparison.Ordinal)
                ? configuredText
                : defaultText;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    RebuildLocalizedTextCache();
                    break;
                case GlobalRegistryServiceSlot.LoreDatabaseRuntime:
                    _loreDatabase = currentService as LoreDatabaseManager;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            _localization = GlobalRegistry.LocalizationText;
            _loreDatabase = GlobalRegistry.LoreDatabase;
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

        private void CacheSharedMeshCold()
        {
            if (_cachedSharedMesh != null)
                return;

            if (fragmentRenderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
            {
                _cachedSharedMesh = skinnedMeshRenderer.sharedMesh;
                return;
            }

            MeshFilter meshFilter = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<MeshFilter>(transform);
            if (meshFilter != null)
                _cachedSharedMesh = meshFilter.sharedMesh;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void EmitResearchDiscoveryEvent()
        {
            if (_discoveryHash != 0u)
            {
                ScanEvents.TryRaiseEntryDiscovered(_discoveryHash, 0u, 0u, 0u, ScanEntryKind.Scannable);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string entryId = !string.IsNullOrWhiteSpace(unlockId)
                ? unlockId.Trim()
                : researchData != null && researchData.RewardItemHash != 0
                    ? "research." + researchData.RewardItemHash
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(entryId))
                return;

            ReadOnlySpan<char> title = HasCustomInteractText()
                ? interactText.AsSpan()
                : DefaultInteractText.AsSpan();
            uint entryHash = ScanEvents.ComputeEntryHash(entryId);
            uint titleHash = title.IsEmpty ? 0u : unchecked((uint)LocHash.Compute(title));
            uint categoryHash = unchecked((uint)LocHash.Compute(DefaultResearchCategory));
            uint summaryHash = unchecked((uint)LocHash.Compute(DefaultResearchSummary));
            ScanEvents.TryRaiseEntryDiscovered(
                entryHash,
                titleHash,
                categoryHash,
                summaryHash,
                ScanEntryKind.Scannable);
#endif
        }

        private float ResolveScanDuration()
        {
            return researchData != null ? researchData.ScanDuration : Mathf.Max(0.5f, scanTime);
        }

        private void RefreshDiscoveryHash()
        {
            if (researchData != null && researchData.DiscoveryHash != 0u)
            {
                _discoveryHash = researchData.DiscoveryHash;
                return;
            }

            if (researchData != null && researchData.RewardItemHash != 0)
            {
                _discoveryHash = unchecked((uint)researchData.RewardItemHash);
                return;
            }

            _discoveryHash = string.IsNullOrWhiteSpace(unlockId)
                ? 0u
                : H8DataHash.ComputeFnv1A32(unlockId);
        }

        private void TryUnlockLoreStages(float previousProgressNormalized, float currentProgressNormalized)
        {
            bool hasResearchLoreStages = researchData != null && _loreDatabase != null;
            bool hasAppliedLoreStages = appliedLoreQuarterPacketHash != 0u ||
                                        appliedLoreHalfPacketHash != 0u ||
                                        appliedLoreFinalPacketHash != 0u;
            if (!hasResearchLoreStages && !hasAppliedLoreStages)
                return;

            TryUnlockLoreStage(previousProgressNormalized, currentProgressNormalized, 0.25f, QuarterLoreStageBit, 0);
            TryUnlockLoreStage(previousProgressNormalized, currentProgressNormalized, 0.5f, HalfLoreStageBit, 1);
            TryUnlockLoreStage(previousProgressNormalized, currentProgressNormalized, 1f, FinalLoreStageBit, 2);
        }

        private void TryUnlockLoreStage(
            float previousProgressNormalized,
            float currentProgressNormalized,
            float threshold,
            byte stageBit,
            int stageIndex)
        {
            if ((_appliedLoreStagesMask & stageBit) != 0 ||
                previousProgressNormalized >= threshold ||
                currentProgressNormalized < threshold)
            {
                return;
            }

            _appliedLoreStagesMask |= stageBit;
            if (researchData != null &&
                researchData.TryGetLoreUnlockMask(stageIndex, out ulong packedBits) &&
                packedBits != 0UL)
            {
                _loreDatabase?.UnlockByPackedBits(packedBits);
            }

            uint packetHash = ResolveAppliedLoreStagePacketHash(stageIndex);
            if (packetHash != 0u)
            {
                uint sourceId = _discoveryHash != 0u ? _discoveryHash : H8AppliedLoreRuntime.UnlockSourceId;
                AbsoluteUniversePosition aup = _transform != null
                    ? AbsoluteUniversePosition.FromRuntimePosition(_transform.position)
                    : AbsoluteUniversePosition.Invalid();
                H8AppliedLoreRuntime.TryRaisePacketUnlockedAt(
                    packetHash,
                    in aup,
                    sourceId,
                    0,
                    (byte)ScanEntryKind.Scannable);
            }
        }

        private uint ResolveAppliedLoreStagePacketHash(int stageIndex)
        {
            switch (stageIndex)
            {
                case 0:
                    return appliedLoreQuarterPacketHash;
                case 1:
                    return appliedLoreHalfPacketHash;
                case 2:
                    return appliedLoreFinalPacketHash;
                default:
                    return 0u;
            }
        }
    }
}

