// ============================================================================
// HECTON-8 - AtlasSignalDecoder.cs
// Atlas-6 signal decoder.
//
// LORE:
//   The closer the player gets to the core, the clearer the carrier becomes:
//   not clean words, but an emotional pattern: despair, hope, then madness.
//   The 11:23 rhythm is the colony rescue-solver loop.
//
// MECHANICS:
//   - Three signal-strength phases.
//   - Full strength opens the final decode window and quest handoff.
//
// ZERO GC:
//   - ISlowTickable checks phase every 0.5 seconds.
//   - Phase text is pre-cached.
// ============================================================================

using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class AtlasSignalDecoder : MonoBehaviour, ISlowTickable, IAtlasSignalEventListener, IGlobalRegistryHotSwapListener
    {
        private const int MaximumSynchronizedPhase = 3;
        private const float SlowTickDeltaSeconds = 0.5f;
        private const string CoreMessageId = "atlas6_core_message";
        private const string FullyDecodedDiscoveryId = "atlas6_signal_fully_decoded";
        private static readonly uint _coreMessageHash = AtlasSignalEvents.ComputeMessageHash(CoreMessageId);
        private static readonly uint _fullyDecodedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(FullyDecodedDiscoveryId);

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("Thresholds")]
        [SerializeField, Range(0f, 1f)] private float phase1Threshold = 0.05f;
        [SerializeField, Range(0f, 1f)] private float phase2Threshold = 0.30f;
        [SerializeField, Range(0f, 1f)] private float phase3Threshold = 0.70f;
        [SerializeField, Range(0f, 1f)] private float fullDecodeThreshold = 0.95f;

        [Header("First-Hour Gate")]
        [Tooltip("Do not decode or surface Atlas phases before the first-hour spine reaches module-route play.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToDecode = FirstHourMilestone.FirstModule;

        [Header("Decode Progress")]
        [Tooltip("Progress added per second while the decode window is open.")]
        [SerializeField, Range(0.01f, 2f)] private float unpackSpeed = 0.2f;

        [Header("Spectrogram Gate")]
        [SerializeField] private bool requireSpectrogramWaveMatch = true;
        [SerializeField, Range(10f, 2000f)] private float targetCarrierFrequencyHz = 113f;
        [SerializeField, Range(0f, 1f)] private float targetCarrierPhase01 = 0.375f;
        [SerializeField, Range(0.01f, 240f)] private float frequencyToleranceHz = 18f;
        [SerializeField, Range(0.001f, 0.5f)] private float phaseTolerance01 = 0.075f;
        [SerializeField, Range(0f, 1f)] private float waveMatchUnlockThreshold01 = 0.92f;

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        private int  _currentPhase = 0;
        private bool _fullyDecoded;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _atlasSignalEventRegistered;
        private bool _hotSwapRegistered;
        private bool _runtimeOwnerAborted;
        private IAtlasSignalReadModel _atlasSignal;
        private IFirstHourReadModel _firstHourDirector;
        private bool _decodeWindowOpen;
        private float _decodeProgress;
        private float _submittedCarrierFrequencyHz;
        private float _submittedCarrierPhase01;
        private float _waveMatch01;

        // Pre-cached phase messages - zero GC
        private static readonly string[] PhaseMessages =
        {
            string.Empty,
            "UNKNOWN SIGNAL - RHYTHMIC PATTERN - PERIOD: 11:23",
            "UNSTABLE EMOTIONAL PATTERN: DESPAIR - HOPE - MADNESS",
            "ATLAS-6 - SOLUTION SEARCH - 847 DAYS - COLONY DEAD - SEED PROGRAM ACTIVE",
            "ATLAS-6 - DECODE COMPLETE - SOURCE: DEPTH -5000M - CORE ACTIVE"
        };

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        public int CurrentPhase => _currentPhase;
        public bool IsFullyDecoded => _fullyDecoded;
        internal bool IsDecodeWindowOpen => _decodeWindowOpen;
        internal float CurrentDecodeProgress => Sanitize01(_decodeProgress);
        public float CurrentWaveMatch01 => Sanitize01(_waveMatch01);
        public float TargetCarrierFrequencyHz => SanitizeFrequencyHz(targetCarrierFrequencyHz);
        public float TargetCarrierPhase01 => SanitizePhase01(targetCarrierPhase01);
        public bool IsSpectrogramWaveMatched => !requireSpectrogramWaveMatch || Sanitize01(_waveMatch01) >= ResolveWaveMatchUnlockThreshold01();
        public string CurrentMessage => _currentPhase < PhaseMessages.Length
            ? PhaseMessages[_currentPhase]
            : string.Empty;

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void OnEnable()
        {
            if (!TryRegisterToGlobalRegistry())
                return;

            TryRegister();
            CacheAtlasSignalCold();
            TryRegisterHotSwapListener();

            TryRegisterAtlasSignalEvents();
            TrySynchronizePhaseFromSignal();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterFromGlobalRegistry();
            TryUnregisterAtlasSignalEvents();
            TryUnregisterHotSwapListener();
            _atlasSignal = null;
            _firstHourDirector = null;
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterFromGlobalRegistry();
            TryUnregisterAtlasSignalEvents();
            TryUnregisterHotSwapListener();
            _atlasSignal = null;
            _firstHourDirector = null;
        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        public void SlowTick()
        {
            if (_runtimeOwnerAborted) return;
            if (_fullyDecoded) return;

            IAtlasSignalReadModel sys = _atlasSignal;
            if (sys == null) return;
            if (!CanDecodeSignal(sys)) return;

            SynchronizePhaseFromSignal(sys);

            float strength = sys.CurrentAtlasSignalStrength01;
            int newPhase = CalculatePhase(strength);
            if (newPhase >= 4)
            {
                _decodeWindowOpen = true;
                newPhase = 3;
            }

            if (_decodeWindowOpen && AdvanceDecodeProgress(SlowTickDeltaSeconds))
                return;

            if (newPhase <= _currentPhase) return;

            _currentPhase = newPhase;
            LogPhaseAdvanced(newPhase);
        }

        // ----------------------------------------------------------
        //  PRIVATE
        // ----------------------------------------------------------

        private int CalculatePhase(float strength)
        {
            float safeStrength = Sanitize01(strength);
            float phase1 = ResolveThreshold01(phase1Threshold, 0.05f);
            float phase2 = math.max(phase1, ResolveThreshold01(phase2Threshold, 0.30f));
            float phase3 = math.max(phase2, ResolveThreshold01(phase3Threshold, 0.70f));
            float fullDecode = math.max(phase3, ResolveThreshold01(fullDecodeThreshold, 0.95f));

            if (safeStrength >= fullDecode) return 4;
            if (safeStrength >= phase3)      return 3;
            if (safeStrength >= phase2)      return 2;
            if (safeStrength >= phase1)      return 1;
            return 0;
        }

        private void TryRegister()
        {
            if (_runtimeOwnerAborted || _registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.AtlasSignalRuntime)
                _atlasSignal = currentService as IAtlasSignalReadModel;
            else if (serviceSlot == GlobalRegistryServiceSlot.FirstHourRuntime)
                _firstHourDirector = currentService as IFirstHourReadModel;
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
            }
        }

        private void CacheAtlasSignalCold()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_atlasSignal == null)
                _atlasSignal = Hecton8.Core.GlobalRegistry.AtlasSignalReadModel;

            if (_firstHourDirector == null)
                _firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHourReadModel;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)
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

        private bool TryRegisterToGlobalRegistry()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            AtlasSignalDecoder registeredRuntime = GlobalRegistry.AtlasSignalDecoder;
            if (IsAtlasSignalDecoderRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            if (!ReferenceEquals(registeredRuntime, null) && !ReferenceEquals(registeredRuntime, this))
                GlobalRegistry.UnregisterAtlasSignalDecoderRuntime(registeredRuntime);

            GlobalRegistry.RegisterAtlasSignalDecoderRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AtlasSignalDecoder, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            return _serviceRegistered;
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterAtlasSignalDecoderRuntime(this);
            _serviceRegistered = false;
        }

        private void TryRegisterAtlasSignalEvents()
        {
            if (_runtimeOwnerAborted || _atlasSignalEventRegistered)
                return;

            AtlasSignalEvents.Register(this);
            _atlasSignalEventRegistered = AtlasSignalEvents.IsRegistered(this);
        }

        private void TryUnregisterAtlasSignalEvents()
        {
            if (!_atlasSignalEventRegistered)
                return;

            AtlasSignalEvents.Unregister(this);
            _atlasSignalEventRegistered = false;
        }

        public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if ((AtlasSignalEventType)payload.EventType == AtlasSignalEventType.Pulse)
                HandleSignalPulse(payload.SignalStrength);
        }

        private void HandleSignalPulse(float intensity)
        {
            if (_runtimeOwnerAborted)
                return;

            // Signal pulse accelerates decode; check phase immediately.
            if (_fullyDecoded) return;

            IAtlasSignalReadModel sys = _atlasSignal;
            if (sys == null) return;
            if (!CanDecodeSignal(sys)) return;

            SynchronizePhaseFromSignal(sys);

            int newPhase = CalculatePhase(sys.CurrentAtlasSignalStrength01);
            if (newPhase >= 4)
            {
                _decodeWindowOpen = true;
                newPhase = 3;
            }

            if (newPhase > _currentPhase)
            {
                _currentPhase = newPhase;
                LogPhaseAdvanced(newPhase);
            }
        }

        private void TrySynchronizePhaseFromSignal()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_fullyDecoded)
                return;

            IAtlasSignalReadModel sys = _atlasSignal;
            if (sys == null)
                return;

            if (!CanDecodeSignal(sys))
                return;

            SynchronizePhaseFromSignal(sys);
        }

        private void SynchronizePhaseFromSignal(IAtlasSignalReadModel sys)
        {
            if (_runtimeOwnerAborted || sys == null || _fullyDecoded)
                return;

            int synchronizedPhase = math.min(MaximumSynchronizedPhase, CalculatePhase(sys.CurrentAtlasSignalStrength01));
            if (synchronizedPhase > _currentPhase)
                _currentPhase = synchronizedPhase;
            _decodeWindowOpen = CalculatePhase(sys.CurrentAtlasSignalStrength01) >= 4;
        }

        private bool CanDecodeSignal(IAtlasSignalReadModel sys)
        {
            if (_runtimeOwnerAborted || sys == null || sys.CurrentAtlasSignalRevealStage <= 0)
                return false;

            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToDecode);
        }

        internal bool TryAdvanceDecode(float dt)
        {
            if (_runtimeOwnerAborted)
                return false;

            return _decodeWindowOpen && AdvanceDecodeProgress(dt);
        }

        public float SubmitWaveMatch(float carrierFrequencyHz, float carrierPhase01)
        {
            if (_runtimeOwnerAborted)
                return 0f;

            _submittedCarrierFrequencyHz = SanitizeFrequencyHz(carrierFrequencyHz);
            _submittedCarrierPhase01 = SanitizePhase01(carrierPhase01);
            _waveMatch01 = SignalBeaconMath.EvaluateSineWaveMatch(
                SanitizeFrequencyHz(targetCarrierFrequencyHz),
                SanitizePhase01(targetCarrierPhase01),
                _submittedCarrierFrequencyHz,
                _submittedCarrierPhase01,
                SanitizePositive(frequencyToleranceHz, 0.001f),
                SanitizePositive(phaseTolerance01, 0.001f));
            _waveMatch01 = Sanitize01(_waveMatch01);

            return _waveMatch01;
        }

        private bool AdvanceDecodeProgress(float dt)
        {
            if (_runtimeOwnerAborted || _fullyDecoded || !_decodeWindowOpen)
                return false;

            float unlockThreshold01 = ResolveWaveMatchUnlockThreshold01();
            float safeWaveMatch01 = Sanitize01(_waveMatch01);
            if (requireSpectrogramWaveMatch && safeWaveMatch01 < unlockThreshold01)
                return false;

            float matchScale = requireSpectrogramWaveMatch ? math.max(unlockThreshold01, safeWaveMatch01) : 1f;
            float safeUnpackSpeed = SanitizePositive(unpackSpeed, 0f);
            float safeDeltaTime = SanitizePositive(dt, 0f);
            _decodeProgress = Sanitize01(_decodeProgress + (safeUnpackSpeed * safeDeltaTime * matchScale));
            if (_decodeProgress < 1f)
                return false;

            CompleteDecode();
            return true;
        }

        private void CompleteDecode()
        {
            if (_runtimeOwnerAborted || _fullyDecoded)
                return;

            _fullyDecoded = true;
            _currentPhase = 4;
            _decodeProgress = 1f;
            _decodeWindowOpen = false;
            AtlasSignalEvents.TryRaiseDecoded(_coreMessageHash);
            NarrativeEvents.TryRaiseDiscoveryMade(_fullyDecodedDiscoveryHash);
            LogSignalFullyDecoded();
        }

        private float ResolveWaveMatchUnlockThreshold01()
        {
            return ResolveThreshold01(waveMatchUnlockThreshold01, 0.92f);
        }

        private static float ResolveThreshold01(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : math.saturate(fallback);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizePhase01(float value)
        {
            return math.isfinite(value) ? math.frac(value) : 0f;
        }

        private static float SanitizeFrequencyHz(float value)
        {
            return math.isfinite(value) ? math.max(0.001f, value) : 0.001f;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : math.max(0f, fallback);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            AtlasSignalDecoder registeredRuntime = GlobalRegistry.AtlasSignalDecoder;
            if (ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsAtlasSignalDecoderRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            if (!ReferenceEquals(registeredRuntime, null))
                GlobalRegistry.UnregisterAtlasSignalDecoderRuntime(registeredRuntime);

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            TryUnregister();
            TryUnregisterAtlasSignalEvents();
            TryUnregisterHotSwapListener();
            _runtimeOwnerAborted = true;
            _registered = false;
            _serviceRegistered = false;
            _atlasSignalEventRegistered = false;
            _hotSwapRegistered = false;
            _atlasSignal = null;
            _firstHourDirector = null;
            enabled = false;
            Destroy(gameObject);
        }


        /// <summary>
        /// Resolve-or-create the sole AtlasSignalDecoder runtime owner for
        /// GlobalRegistry.AtlasSignalDecoder (Atlas-6 phase decode / quest handoff).
        /// Script GUID bca3aaf40fff8ea459345f06a7f5592b has ZERO live scene/prefab hits.
        /// HectonLoreSystemsRoot.SetupAllSystems is editor ContextMenu-only.
        /// No Ensure existed; OnEnable only registers when already present.
        /// Phase/quest consumers hit permanent null without this path.
        /// </summary>
        public static AtlasSignalDecoder EnsureRuntimeInstance()
        {
            AtlasSignalDecoder registered = GlobalRegistry.AtlasSignalDecoder;
            if (IsAtlasSignalDecoderRuntimeUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterAtlasSignalDecoderRuntime(registered);
                registered._serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: zero authored scene/prefab hits for this owner.
            GameObject runtimeRoot = new GameObject("[AtlasSignalDecoder]"); // COLD ALLOC
            return runtimeRoot.AddComponent<AtlasSignalDecoder>();
        }

        private static bool IsAtlasSignalDecoderRuntimeUsable(AtlasSignalDecoder decoder)
        {
            return !ReferenceEquals(decoder, null) &&
                   decoder != null &&
                   decoder._serviceRegistered &&
                   decoder.isActiveAndEnabled &&
                   !decoder._runtimeOwnerAborted;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalFullyDecoded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[AtlasDecoder] Signal fully decoded. Atlas-6 core message received.");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPhaseAdvanced(int phase)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            switch (phase)
            {
                case 1:
                    H8Debug.Log("[AtlasDecoder] Phase 1: UNKNOWN SIGNAL - RHYTHMIC PATTERN.");
                    break;
                case 2:
                    H8Debug.Log("[AtlasDecoder] Phase 2: UNSTABLE EMOTIONAL PATTERN.");
                    break;
                case 3:
                    H8Debug.Log("[AtlasDecoder] Phase 3: ATLAS-6 IDENTITY LOCK.");
                    break;
                case 4:
                    H8Debug.Log("[AtlasDecoder] Phase 4: DECODE COMPLETE.");
                    break;
                default:
                    H8Debug.Log("[AtlasDecoder] Phase advanced.");
                    break;
            }
#endif
        }
    }
}
