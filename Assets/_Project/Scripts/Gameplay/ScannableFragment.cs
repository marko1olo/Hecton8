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
//   • No Update() — event-driven via OnScan().
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
using Hecton8.Interaction;
using Hecton8.Narrative;
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
    public sealed class ScannableFragment : MonoBehaviour, IInteractable, ILocalizationLanguageChangedListener
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
        private FragmentState _state = FragmentState.Scannable;
        private float _currentProgress;
        private bool _isScanning;
        private Vector3 _originalPosition;
        private byte _appliedLoreStagesMask;

        /// <summary>
        /// Cached MaterialPropertyBlock for scan VFX.
        /// Allocated once in Awake — zero GC in hot path.
        /// </summary>
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — scan progress VFX — owner: ScannableFragment

        /// <summary>
        /// Pre-cached interaction text to avoid runtime allocations.
        /// </summary>
        private string _cachedInteractText;

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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;

            // COLD ALLOC: MaterialPropertyBlock — scan VFX
            _mpb = new MaterialPropertyBlock();

            // Auto-find renderer if not assigned
            if (fragmentRenderer == null)
            {
                fragmentRenderer = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);
            }

            RebuildLocalizedTextCache();

            // Store original position for vibration
            _originalPosition = _transform.position;

            ResetState();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildLocalizedTextCache();
            ResetState();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
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
            return CanBeScanned ? _cachedInteractText : null;
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
            float scanDuration = ResolveScanDuration();
            _currentProgress = Mathf.Min(_currentProgress + progressDelta, scanDuration);

            float currentProgressNormalized = ProgressNormalized;
            TryUnlockLoreStages(previousProgressNormalized, currentProgressNormalized);

            // Update visuals
            UpdateScanVisuals();

            // Fire progress event
            OnProgressChanged?.Invoke(currentProgressNormalized);

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

            // Reset vibration
            _transform.position = _originalPosition;

            // Reset visuals
            ResetScanVisuals();

            // Fire stopped event
            OnScanStopped?.Invoke();
        }

        /// <summary>
        /// Resets the fragment to scannable state (for testing or special gameplay).
        /// </summary>
        public void ResetFragment()
        {
            ResetState();
        }

        /// <summary>
        /// Locks the fragment so it cannot be scanned.
        /// </summary>
        public void Lock()
        {
            _state = FragmentState.Locked;
            StopScanning();
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

            // Store original position for vibration
            _originalPosition = _transform.position;

            // Play scanning sound
            if (scanningSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(scanningSound, _transform.position, scanVolume);
            }

            // Fire started event
            OnScanStarted?.Invoke();
        }

        private void CompleteScan()
        {
            _state = FragmentState.Completed;
            _isScanning = false;

            // Reset vibration
            _transform.position = _originalPosition;

            // Play complete particles
            if (completeParticles != null)
            {
                completeParticles.transform.position = _transform.position;
                completeParticles.Play();
            }

            // Play complete sound
            if (completeSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(completeSound, _transform.position, scanVolume);
            }

            // Fire completion event with unlock ID
            OnScanComplete?.Invoke(unlockId);
            EmitResearchDiscoveryEvent();

            // Fire final progress event
            OnProgressChanged?.Invoke(1f);

            // Disable the fragment
            DisableFragment();
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private void UpdateScanVisuals()
        {
            // Update shader properties
            if (fragmentRenderer != null && _mpb != null)
            {
                fragmentRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_ScanProgressID, ProgressNormalized);
                _mpb.SetColor(_ScanGlowColorID, scanGlowColor);

                // Pulse effect
                float pulse = Mathf.Sin(Time.time * vibrationFrequency * 2f) * 0.5f + 0.5f;
                _mpb.SetFloat(_ScanPulseID, pulse);

                fragmentRenderer.SetPropertyBlock(_mpb);
            }

            // Apply vibration
            if (vibrationAmplitude > 0f)
            {
                Vector3 vibration = new Vector3(
                    Mathf.Sin(Time.time * vibrationFrequency) * vibrationAmplitude,
                    Mathf.Cos(Time.time * vibrationFrequency * 1.3f) * vibrationAmplitude,
                    Mathf.Sin(Time.time * vibrationFrequency * 0.7f) * vibrationAmplitude
                );
                _transform.position = _originalPosition + vibration;
            }
        }

        private void ResetScanVisuals()
        {
            if (fragmentRenderer == null || _mpb == null) return;

            fragmentRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_ScanProgressID, 0f);
            _mpb.SetFloat(_ScanPulseID, 0f);
            fragmentRenderer.SetPropertyBlock(_mpb);
        }

        // ══════════════════════════════════════════════════════════
        //  STATE RESET
        // ══════════════════════════════════════════════════════════

        private void ResetState()
        {
            _state = canBeScanned ? FragmentState.Scannable : FragmentState.Locked;
            _currentProgress = 0f;
            _isScanning = false;
            _appliedLoreStagesMask = 0;

            // Reset visuals
            ResetScanVisuals();

            // Reset position
            if (_transform != null)
            {
                _originalPosition = _transform.position;
            }

            // Re-enable renderer
            if (fragmentRenderer != null)
            {
                fragmentRenderer.enabled = true;
            }
        }

        private void DisableFragment()
        {
            // Disable renderer
            if (fragmentRenderer != null)
            {
                fragmentRenderer.enabled = false;
            }

            // Disable collider
            if (TryGetComponent(out Collider col))
            {
                col.enabled = false;
            }

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
            _cachedInteractText = HasCustomInteractText()
                ? interactText
                : ResolveLocalized(LocalizationKeys.INTERACT_SCAN_FRAGMENT, DefaultInteractText);
        }

        private bool HasCustomInteractText()
        {
            return !string.IsNullOrWhiteSpace(interactText) &&
                   !string.Equals(interactText, DefaultInteractText, System.StringComparison.Ordinal);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void EmitResearchDiscoveryEvent()
        {
            string entryId = !string.IsNullOrWhiteSpace(unlockId)
                ? unlockId.Trim()
                : researchData != null && researchData.RewardItemHash != 0
                    ? "research." + researchData.RewardItemHash
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(entryId))
                return;

            string title = HasCustomInteractText()
                ? interactText.Trim().ToUpperInvariant()
                : ResolveLocalized(LocalizationKeys.INTERACT_SCAN_FRAGMENT, DefaultInteractText).ToUpperInvariant();
            ScanEvents.RaiseEntryDiscovered(
                entryId,
                title,
                DefaultResearchCategory,
                DefaultResearchSummary,
                ScanEntryKind.Scannable);
        }

        private float ResolveScanDuration()
        {
            return researchData != null ? researchData.ScanDuration : Mathf.Max(0.5f, scanTime);
        }

        private void TryUnlockLoreStages(float previousProgressNormalized, float currentProgressNormalized)
        {
            if (researchData == null || Hecton8.Core.GlobalRegistry.LoreDatabase == null)
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
                Hecton8.Core.GlobalRegistry.LoreDatabase.UnlockByPackedBits(packedBits);
            }
        }
    }
}

