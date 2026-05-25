// ============================================================================
// HECTON-8 - AtlasSignalSystem.cs
// Atlas-6 signal pulse system.
//
// LORE (Block Z):
//   Scavenger rumor: "There is a signal on Hecton-8 that repeats every 11:23."
//   The 11:23 rhythm reads as the machine iterating every colony rescue branch.
//   The closer the player gets to the core, the clearer the signal content becomes:
//   not words, but emotional payload: despair, hope, and collapse.
//
// MECHANICS:
//   - Pulse every 683 seconds.
//   - Signal strength = 1 - (dist / maxSignalRange).
//   - Scanner receives usable bearing only after late identity-stage lock.
//   - Quest handoff goes through discovery-chain, not early raw detection.
//   - Integrates with HectonDirectorAI narrative beat.
//
// ZERO GC:
//   - ISlowTickable - timer without per-frame polling.
//   - No new/LINQ in the hot path.
//   - Shader.SetGlobalFloat publishes bioluminescent response strength.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Hecton.Localization;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class AtlasSignalSystem : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, IAtlasSignalReadModel, IGlobalRegistryHotSwapListener
    {
        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Signal Parameters ----------------------")]
        [Tooltip("Pulse period in seconds. 683 equals 11 minutes 23 seconds.")]
        [SerializeField] private float pulsePeriodSeconds = 683f;

        [Tooltip("Maximum signal detection range in meters.")]
        [SerializeField] private float maxSignalRange = 8000f;

        [Tooltip("Atlas-6 core position in world coordinates.")]
        [SerializeField] private Vector3 atlasCorePosWorld = new Vector3(0f, -5000f, 0f);

        [Tooltip("Minimum signal strength required for scanner detection.")]
        [SerializeField, Range(0f, 1f)] private float detectionThreshold = 0.05f;

        [Header("-- Late Manifestation ---------------------")]
        [Tooltip("Atlas stays dormant until the first-hour spine has already handed the player to deeper route/module play.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToManifest = FirstHourMilestone.FirstModule;

        [Tooltip("Before full manifestation, Atlas may leak only a weak rhythmic ghost-beat once the player has already proven deeper commitment.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToGhostManifest = FirstHourMilestone.FirstCraft;

        [Tooltip("Depth where the first rhythmic Atlas beat can cut through the water.")]
        [SerializeField] private float revealStage1Depth = 180f;

        [Tooltip("Depth where the rhythm stops reading as noise and starts reading as pattern.")]
        [SerializeField] private float revealStage2Depth = 450f;

        [Tooltip("Depth where the signal starts yielding content fragments instead of pure rhythm.")]
        [SerializeField] private float revealStage3Depth = 1200f;

        [Tooltip("Depth where the carrier becomes stable enough for a true late-game lock on the source.")]
        [SerializeField] private float revealStage4Depth = 2600f;

        [Header("-- Shader Integration --------------------")]
        [Tooltip("Publish signal strength to the shader for bioluminescent response.")]
        [SerializeField] private bool publishToShader = true;

        [Header("Encrypted Log Unlocks")]
        [SerializeField] private string stage2EncryptedLogId = "captain_last_broadcast";
        [SerializeField] private string stage3EncryptedLogId = "atlas6_terminal_sector3";
        [SerializeField] private string stage4EncryptedLogId = "biologist_samples";

        // ----------------------------------------------------------
        //  SERVICE AUTHORITY
        // ----------------------------------------------------------

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        private HectonPlayerMovement _playerMovement;
        private AbsoluteUniversePosition _atlasCoreAup;
        private Vector3 _atlasCoreAupSource;
        private float _pulseTimer;
        private float _currentStrength;
        private float _lastPublishedStrength;
        private int _currentStrengthBand;
        private bool _signalEverDetected;
        private int _maxRevealStageUnlocked;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _saveRegistered;
        private bool _ghostManifestationAnnounced;
        private bool _identityDiscoverySynchronized;
        private bool _fullDecodeDiscoverySynchronized;
        private bool _stage2LogQueued;
        private bool _stage3LogQueued;
        private bool _stage4LogQueued;
        private bool _atlasCoreAupCached;
        private bool _lateFrameRegistered;
        private bool _pendingShaderStrengthDirty;
        private float _pendingShaderStrength;
        private uint _stage2EncryptedLogHash;
        private uint _stage3EncryptedLogHash;
        private uint _stage4EncryptedLogHash;
        private uint _stage2EncryptedLogDiscoveryHash;
        private uint _stage3EncryptedLogDiscoveryHash;
        private uint _stage4EncryptedLogDiscoveryHash;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IFirstHourReadModel _firstHourDirector;
        private INarrativeDiscoveryReadModel _narrativeDiscoveryReadModel;
        private IAudioLogRuntime _audioLogs;
        private ILocalizationTextReadModel _localization;
        private ISaveService _saveService;

        private const int FormalDetectionRevealStage = 2;
        private const int IdentityRevealStage = 3;
        private const int FullDecodeRevealStage = 4;
        private const double SlowTickBudgetMilliseconds = 0.2d;
        private const float AtlasRevealPingDurationSeconds = 0.09f;
        private const float AtlasRevealPingTransmission01 = 0.72f;
        private const float AtlasRevealPingLowPassCutoffHz = 4200f;
        private const string SignalIdentityDiscoveryId = "atlas6_signal_identified";
        private const string SignalFullyDecodedDiscoveryId = "atlas6_signal_fully_decoded";
        private const string SignalFirstDetectedLog = "[AtlasSignal] Signal first detected.";
        private const string SignalPulseLog = "[AtlasSignal] Pulse emitted.";
        private const string SignalDecodedLog = "[AtlasSignal] Signal decoded.";
        private const string RevealStageUnlockedLog = "[AtlasSignal] Reveal stage unlocked.";
        private static readonly uint _signalIdentityDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalIdentityDiscoveryId);
        private static readonly uint _signalFullyDecodedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalFullyDecodedDiscoveryId);
        private static readonly uint _signalFullyDecodedMessageHash = AtlasSignalEvents.ComputeMessageHash(SignalFullyDecodedDiscoveryId);
        private static readonly uint _AudioLogRuntimeMissingWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.AudioLogRuntimeMissing"));
        private static readonly uint _EncryptedLogFallbackWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.EncryptedLogFallback"));
        private static readonly uint _DuplicateRuntimeWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.DuplicateRuntime"));
        private static readonly uint _SlowTickBudgetWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.SlowTickBudgetExceeded"));
        private static readonly uint _AtlasSignalContextHash = unchecked((uint)LocHash.Compute("AtlasSignalSystem"));

        private static readonly int _ShaderSignalStrength =
            Shader.PropertyToID("_AtlasSignalStrength");

        // Throttle log - static field outside the hot path.
        private static float _nextSignalLogTime;

        private const float StrengthEpsilon = 0.01f;

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        public float CurrentStrength => _currentStrength;
        public int CurrentStrengthBand => _currentStrengthBand;
        public bool IsDetected =>
            _maxRevealStageUnlocked >= FormalDetectionRevealStage &&
            _currentStrength >= detectionThreshold;
        public float CurrentAtlasSignalStrength01 =>
            math.saturate(math.select(0f, _currentStrength, math.isfinite(_currentStrength)));
        public int CurrentAtlasSignalRevealStage => math.max(0, _maxRevealStageUnlocked);
        public bool IsAtlasSignalDetected => IsDetected;
        public Vector3 AtlasCorePosition => atlasCorePosWorld;

        public AbsoluteUniversePosition AtlasCoreAup => ResolveAtlasCoreAup();
        public int CurrentRevealStage => _maxRevealStageUnlocked;

        public bool TryReadAtlasSignalCoreAup(out AbsoluteUniversePosition coreAup)
        {
            coreAup = ResolveAtlasCoreAup();
            return coreAup.IsFinite();
        }

        public bool TryReadAtlasSignalSnapshot(
            in AbsoluteUniversePosition observerAup,
            out AtlasSignalReadSnapshot snapshot)
        {
            snapshot = default;
            if (!observerAup.IsFinite())
                return false;

            AbsoluteUniversePosition coreAup = ResolveAtlasCoreAup();
            if (!coreAup.IsFinite())
                return false;

            float strength = math.saturate(math.select(0f, _currentStrength, math.isfinite(_currentStrength)));
            int revealStage = math.max(0, _maxRevealStageUnlocked);
            Vector3 direction = SignalStrengthSystem.CalculateDirectionToCore(in observerAup, in coreAup);
            float3 directionToCore = new float3(direction.x, direction.y, direction.z);
            if (!math.all(math.isfinite(directionToCore)))
                directionToCore = new float3(0f, -1f, 0f);

            uint flags = 0u;
            if (revealStage >= FormalDetectionRevealStage && strength >= detectionThreshold)
                flags |= AtlasSignalReadSnapshot.IsDetectedFlag;
            if (revealStage >= IdentityRevealStage)
                flags |= AtlasSignalReadSnapshot.HasNavigationFlag;

            snapshot.DirectionToCore = directionToCore;
            snapshot.Strength01 = strength;
            snapshot.RevealStage = revealStage;
            snapshot.StrengthBand = math.max(0, _currentStrengthBand);
            snapshot.Flags = flags;
            return true;
        }

        /// <summary>
        /// Direction from the current player position to the Atlas-6 core.
        /// Used by the scanner for navigation.
        /// </summary>
        public Vector3 DirectionToCore
        {
            get
            {
                if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                    return Vector3.down;

                AbsoluteUniversePosition coreAup = ResolveAtlasCoreAup();
                return SignalStrengthSystem.CalculateDirectionToCore(in playerAup, in coreAup);
            }
        }

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public int SavePriority => 8;
        public int LoadPriority => 8;

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void OnEnable()
        {
            CacheEncryptedLogHashes();
            CacheRuntimeDependencies();
            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegister();
            TryRegisterLateFrame();
            TryRegisterSaveParticipant();

            ResolvePlayer();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterService();
            TryUnregisterSaveParticipant();

            TryUnregisterHotSwapListener();
            ClearRuntimeDependencies();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterService();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            ClearRuntimeDependencies();

        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        public void SlowTick()
        {
            long solveStartTicks = Stopwatch.GetTimestamp();
            try
            {
                SlowTickCore();
            }
            finally
            {
                PublishSlowTickBudgetIfNeeded(solveStartTicks);
            }
        }

        private void SlowTickCore()
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                ClearLiveSignalState();
                return;
            }

            _pulseTimer += 0.5f; // SlowTick ~0.5s

            AbsoluteUniversePosition coreAup = ResolveAtlasCoreAup();
            float rawStrength = CalculateRawStrength(in playerAup, in coreAup);
            int previousRevealStage = _maxRevealStageUnlocked;
            int desiredRevealStage = ResolveDesiredRevealStage(ResolveCurrentDepthMeters(in playerAup));
            if (desiredRevealStage > _maxRevealStageUnlocked)
                _maxRevealStageUnlocked = desiredRevealStage;

            float newStrength = math.min(rawStrength, ResolveRevealStrengthCap(_maxRevealStageUnlocked));
            _currentStrengthBand = math.min(
                SignalStrengthSystem.StrengthToBand(newStrength),
                math.clamp(_maxRevealStageUnlocked, 0, FullDecodeRevealStage));

            // Publikuem izmenenie sily
            if (math.abs(newStrength - _lastPublishedStrength) > StrengthEpsilon)
            {
                _currentStrength = newStrength;
                _lastPublishedStrength = newStrength;
                AtlasSignalEvents.TryRaiseStrengthChanged(newStrength);

                // Pervoe obnaruzhenie
                if (!_signalEverDetected &&
                    newStrength >= detectionThreshold &&
                    _maxRevealStageUnlocked >= FormalDetectionRevealStage)
                {
                    _signalEverDetected = true;
                    AtlasSignalEvents.TryRaiseDetected(atlasCorePosWorld);
                    LogSignalFirstDetected();
                }

                // Sheyder
                if (publishToShader)
                    QueueShaderStrength(newStrength);
            }

            if (_maxRevealStageUnlocked > previousRevealStage)
                HandleRevealStageUnlocked(_maxRevealStageUnlocked, newStrength);

            TryEnsureIdentityDiscoveryPublished();

            // Puls
            if (_maxRevealStageUnlocked <= 0)
                return;

            if (_pulseTimer < pulsePeriodSeconds)
                return;

            _pulseTimer = 0f;
            float pulseIntensity = _currentStrength;
            AtlasSignalEvents.TryRaisePulse(pulseIntensity);

            LogSignalPulse();
        }

        private void ClearLiveSignalState()
        {
            bool hadLiveStrength =
                math.abs(_currentStrength) > StrengthEpsilon ||
                math.abs(_lastPublishedStrength) > StrengthEpsilon ||
                _currentStrengthBand != 0;

            if (!hadLiveStrength)
                return;

            _currentStrength = 0f;
            _lastPublishedStrength = 0f;
            _currentStrengthBand = 0;
            AtlasSignalEvents.TryRaiseStrengthChanged(0f);

            if (publishToShader)
                QueueShaderStrength(0f);
        }

        public void LateFrameTick()
        {
            if (!_pendingShaderStrengthDirty)
                return;

            _pendingShaderStrengthDirty = false;
            Shader.SetGlobalFloat(_ShaderSignalStrength, _pendingShaderStrength);
        }

        // ----------------------------------------------------------
        //  PUBLIC API
        // ----------------------------------------------------------

        /// <summary>
        /// Vyzyvaetsya kogda igrok dostigaet yadra i rasshifrovyvaet signal.
        /// </summary>
        public void DecodeSignal(string messageId)
        {
            DecodeSignal(AtlasSignalEvents.ComputeMessageHash(messageId));
        }

        public void DecodeSignal(uint messageHash)
        {
            if (messageHash == 0u)
                return;

            AtlasSignalEvents.TryRaiseDecoded(messageHash);
            if (messageHash == _signalFullyDecodedMessageHash)
            {
                if (_maxRevealStageUnlocked < FullDecodeRevealStage)
                    _maxRevealStageUnlocked = FullDecodeRevealStage;

                TryEnsureFullDecodeDiscoveryPublished();
            }

            LogSignalDecoded();
        }

        // ----------------------------------------------------------
        //  PRIVATE
        // ----------------------------------------------------------

        private void ResolvePlayer()
        {
            _playerMovement = null;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement == null)
            {
                ResolvePlayer();
                if (_playerMovement == null)
                {
                    playerAup = default;
                    return false;
                }
            }

            playerAup = _playerMovement.CurrentAup;
            return true;
        }

        private AbsoluteUniversePosition ResolveAtlasCoreAup()
        {
            if (!_atlasCoreAupCached || _atlasCoreAupSource != atlasCorePosWorld)
            {
                _atlasCoreAupSource = atlasCorePosWorld;
                double3 atlasCoreAup = new double3(atlasCorePosWorld.x, atlasCorePosWorld.y, atlasCorePosWorld.z);
                _atlasCoreAup = math.all(math.isfinite(atlasCoreAup))
                    ? AbsoluteUniversePosition.FromAbsolutePosition(atlasCoreAup)
                    : default;
                _atlasCoreAupCached = true;
            }

            return _atlasCoreAup;
        }

        private float ResolveCurrentDepthMeters(in AbsoluteUniversePosition playerAup)
        {
            BiomeMatrixDirector biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            if (biomeMatrixDirector != null)
                return biomeMatrixDirector.CurrentDepthMeters;

            double absoluteY = playerAup.ToAbsoluteDouble3().y;
            return math.max(0f, (float)-absoluteY);
        }

        private float CalculateRawStrength(in AbsoluteUniversePosition playerAup, in AbsoluteUniversePosition coreAup)
        {
            return SignalStrengthSystem.CalculateStrength(in playerAup, in coreAup, maxSignalRange);
        }

        private int ResolveDesiredRevealStage(float currentDepthMeters)
        {
            if (CanManifestAtlas())
            {
                if (currentDepthMeters >= revealStage4Depth)
                    return 4;

                if (currentDepthMeters >= revealStage3Depth)
                    return 3;

                if (currentDepthMeters >= revealStage2Depth)
                    return 2;

                if (currentDepthMeters >= revealStage1Depth)
                    return 1;

                return 0;
            }

            if (CanManifestGhostBeat() && currentDepthMeters >= revealStage1Depth)
                return 1;

            return 0;
        }

        private float ResolveRevealStrengthCap(int revealStage)
        {
            return revealStage switch
            {
                1 => 0.08f,
                2 => 0.34f,
                3 => 0.78f,
                4 => 1f,
                _ => 0f
            };
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
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

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            _lateFrameRegistered = false;
        }

        private void QueueShaderStrength(float strength01)
        {
            _pendingShaderStrength = math.isfinite(strength01)
                ? math.saturate(strength01)
                : 0f;
            _pendingShaderStrengthDirty = true;
            TryRegisterLateFrame();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.AtlasSignal != null && !ReferenceEquals(GlobalRegistry.AtlasSignal, this))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_DuplicateRuntimeWarningHash, _AtlasSignalContextHash, 1f);
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterAtlasSignalRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AtlasSignal, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.AtlasSignal, this))
                GlobalRegistry.UnregisterAtlasSignalRuntime(this);

            _serviceRegistered = false;
        }

        private void CacheRuntimeDependencies()
        {
            _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
            _firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHourReadModel;
            _narrativeDiscoveryReadModel = GlobalRegistry.NarrativeDiscoveryReadModel;
            _audioLogs = GlobalRegistry.AudioLogRuntime;
            _localization = Hecton8.Core.GlobalRegistry.LocalizationText;
            _saveService = Hecton8.Core.GlobalRegistry.Save;
        }

        private void ClearRuntimeDependencies()
        {
            _playerRuntimeContext = null;
            _playerMovement = null;
            _firstHourDirector = null;
            _narrativeDiscoveryReadModel = null;
            _audioLogs = null;
            _localization = null;
            _saveService = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered)
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    ResolvePlayer();
                    break;
                case GlobalRegistryServiceSlot.FirstHourRuntime:
                    _firstHourDirector = currentService as IFirstHourReadModel;
                    break;
                case GlobalRegistryServiceSlot.NarrativeDirectorRuntime:
                    _narrativeDiscoveryReadModel = currentService as INarrativeDiscoveryReadModel;
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    _audioLogs = currentService as IAudioLogRuntime;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    if (_saveService != null)
                    {
                        _saveService.Register(this);
                        _saveRegistered = true;
                    }
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService != null)
                    {
                        TryRegister();
                        TryRegisterLateFrame();
                    }
                    break;
            }
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered)
                return;

            if (_saveService == null)
                _saveService = Hecton8.Core.GlobalRegistry.Save;
            if (_saveService == null)
                return;

            _saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _saveService = null;
            _saveRegistered = false;
        }

        private bool CanManifestAtlas()
        {
            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToManifest);
        }

        private bool CanManifestGhostBeat()
        {
            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector == null)
                return false;

            if (firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToManifest))
                return false;

            return firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToGhostManifest);
        }

        private void HandleRevealStageUnlocked(int revealStage, float manifestedStrength)
        {
            if (manifestedStrength <= 0f)
                return;

            _pulseTimer = 0f;
            AtlasSignalEvents.TryRaisePulse(manifestedStrength);
            ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                atlasCorePosWorld,
                math.saturate(manifestedStrength),
                AtlasRevealPingDurationSeconds,
                AtlasRevealPingTransmission01,
                AtlasRevealPingLowPassCutoffHz,
                ProceduralAudioPingKind.Sonar);

            switch (revealStage)
            {
                case 1:
                    if (!CanManifestAtlas() && !_ghostManifestationAnnounced)
                    {
                        _ghostManifestationAnnounced = true;
                    }
                    break;

                case 2:
                    if (!_signalEverDetected && manifestedStrength >= detectionThreshold)
                    {
                        _signalEverDetected = true;
                        AtlasSignalEvents.TryRaiseDetected(atlasCorePosWorld);
                    }

                    TryQueueEncryptedLog(2);
                    NotificationEvents.TryPushInfo(ResolveLocalizedSpan(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_2,
                        "WEAK RHYTHMIC PATTERN CONFIRMED. CONTACT STILL UNSTABLE."));
                    break;

                case 3:
                    TryEnsureIdentityDiscoveryPublished();
                    TryQueueEncryptedLog(3);
                    NotificationEvents.TryPushWarning(ResolveLocalizedSpan(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_3,
                        "THE SIGNAL IS STARTING TO RETURN CONTENT FRAGMENTS. DEPTH IS CLEANING THE BEARING."));
                    break;

                case 4:
                    TryEnsureFullDecodeDiscoveryPublished();
                    TryQueueEncryptedLog(4);
                    NotificationEvents.TryPushWarning(ResolveLocalizedSpan(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_4,
                        "CARRIER STABLE. THE SIGNAL CAN NOW BE DRIVEN ALL THE WAY TO THE SOURCE."));
                    break;
            }

            LogRevealStageUnlocked();
        }

        private void TryEnsureIdentityDiscoveryPublished()
        {
            if (_identityDiscoverySynchronized || _maxRevealStageUnlocked < IdentityRevealStage)
                return;

            INarrativeDiscoveryReadModel narrativeDiscovery = _narrativeDiscoveryReadModel;
            if (narrativeDiscovery == null)
                return;

            if (!narrativeDiscovery.HasDiscovery(_signalIdentityDiscoveryHash))
                NarrativeEvents.TryRaiseDiscoveryMade(_signalIdentityDiscoveryHash);

            _identityDiscoverySynchronized = true;
        }

        private void TryEnsureFullDecodeDiscoveryPublished()
        {
            if (_fullDecodeDiscoverySynchronized || _maxRevealStageUnlocked < FullDecodeRevealStage)
                return;

            INarrativeDiscoveryReadModel narrativeDiscovery = _narrativeDiscoveryReadModel;
            if (narrativeDiscovery != null && narrativeDiscovery.HasDiscovery(_signalFullyDecodedDiscoveryHash))
            {
                _fullDecodeDiscoverySynchronized = true;
                return;
            }

            NarrativeEvents.TryRaiseDiscoveryMade(_signalFullyDecodedDiscoveryHash);
            _fullDecodeDiscoverySynchronized = true;
        }

        private void TryQueueEncryptedLog(int revealStage)
        {
            uint logHash;
            uint fallbackLogHash;
            switch (revealStage)
            {
                case 2:
                    if (_stage2LogQueued)
                        return;
                    _stage2LogQueued = true;
                    logHash = _stage2EncryptedLogHash;
                    fallbackLogHash = _stage2EncryptedLogDiscoveryHash;
                    break;

                case 3:
                    if (_stage3LogQueued)
                        return;
                    _stage3LogQueued = true;
                    logHash = _stage3EncryptedLogHash;
                    fallbackLogHash = _stage3EncryptedLogDiscoveryHash;
                    break;

                case 4:
                    if (_stage4LogQueued)
                        return;
                    _stage4LogQueued = true;
                    logHash = _stage4EncryptedLogHash;
                    fallbackLogHash = _stage4EncryptedLogDiscoveryHash;
                    break;

                default:
                    return;
            }

            if (logHash == 0u)
                return;

            IAudioLogRuntime audioLogs = _audioLogs;
            if (audioLogs != null)
            {
                if (audioLogs.TryPlayAudioLogByHash(logHash))
                    return;

                if ((audioLogs.GetRecoveredEncryptedAudioLogBits(logHash) & 0xFu) != 0xFu)
                    return;
            }

            GlobalTelemetryBus.PublishPerformanceWarning(
                audioLogs == null ? _AudioLogRuntimeMissingWarningHash : _EncryptedLogFallbackWarningHash,
                _AtlasSignalContextHash,
                revealStage);
            NarrativeEvents.TryRaiseDiscoveryMade(fallbackLogHash);
        }

        private void CacheEncryptedLogHashes()
        {
            _stage2EncryptedLogHash = QuestFlagHashKernel.ComputeStableHash(stage2EncryptedLogId);
            _stage3EncryptedLogHash = QuestFlagHashKernel.ComputeStableHash(stage3EncryptedLogId);
            _stage4EncryptedLogHash = QuestFlagHashKernel.ComputeStableHash(stage4EncryptedLogId);
            _stage2EncryptedLogDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(stage2EncryptedLogId);
            _stage3EncryptedLogDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(stage3EncryptedLogId);
            _stage4EncryptedLogDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(stage4EncryptedLogId);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalFirstDetected()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log(SignalFirstDetectedLog);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalPulse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.time < _nextSignalLogTime)
                return;

            _nextSignalLogTime = Time.time + 5f;
            H8Debug.Log(SignalPulseLog);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalDecoded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log(SignalDecodedLog);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogRevealStageUnlocked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log(RevealStageUnlockedLog);
#endif
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _localization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private static void PublishSlowTickBudgetIfNeeded(long solveStartTicks)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - solveStartTicks;
            double elapsedMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= SlowTickBudgetMilliseconds)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _SlowTickBudgetWarningHash,
                _AtlasSignalContextHash,
                (float)elapsedMilliseconds);
        }

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.atlasSignalDetected = _signalEverDetected;
            data.atlasSignalPulseTimer = _pulseTimer;
            data.atlasSignalRevealStage = _maxRevealStageUnlocked;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _signalEverDetected = data.atlasSignalDetected;
            _pulseTimer = data.atlasSignalPulseTimer;
            _maxRevealStageUnlocked = math.clamp(data.atlasSignalRevealStage, 0, 4);
            _ghostManifestationAnnounced = _maxRevealStageUnlocked > 0 && !_signalEverDetected;
            _identityDiscoverySynchronized = _maxRevealStageUnlocked >= IdentityRevealStage;
            _fullDecodeDiscoverySynchronized = _maxRevealStageUnlocked >= FullDecodeRevealStage;
            if (_signalEverDetected && _maxRevealStageUnlocked < FormalDetectionRevealStage)
                _maxRevealStageUnlocked = FormalDetectionRevealStage;

            if (_maxRevealStageUnlocked >= FormalDetectionRevealStage)
                _signalEverDetected = true;

            _stage2LogQueued = _maxRevealStageUnlocked >= 2;
            _stage3LogQueued = _maxRevealStageUnlocked >= 3;
            _stage4LogQueued = _maxRevealStageUnlocked >= 4;
        }
    }

    internal static class SignalStrengthSystem
    {
        private const float StrengthBandOneThreshold = 0.001f;
        private const float StrengthBandTwoThreshold = 0.25f;
        private const float StrengthBandThreeThreshold = 0.5f;
        private const float StrengthBandFourThreshold = 0.75f;

        public static float CalculateStrength(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup,
            float maxRangeMeters)
        {
            double safeRange = math.max(0.001f, maxRangeMeters);
            double safeRangeSq = safeRange * safeRange;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
            if (distanceSq >= safeRangeSq)
                return 0f;

            return math.saturate((float)(1d - distanceSq / safeRangeSq));
        }

        public static int CalculateStrengthBand(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup,
            float maxRangeMeters)
        {
            return StrengthToBand(CalculateStrength(in playerAup, in coreAup, maxRangeMeters));
        }

        public static int StrengthToBand(float strength01)
        {
            float strength = math.saturate(strength01);
            if (strength < StrengthBandOneThreshold)
                return 0;
            if (strength < StrengthBandTwoThreshold)
                return 1;
            if (strength < StrengthBandThreeThreshold)
                return 2;
            if (strength < StrengthBandFourThreshold)
                return 3;

            return 4;
        }

        public static double CalculateDistanceSqMeters(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup)
        {
            return AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
        }

        public static Vector3 CalculateDirectionToCore(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup)
        {
            double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in coreAup, in playerAup);
            double lengthSq = math.lengthsq(delta);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001d)
                return Vector3.down;

            double ax = math.abs(delta.x);
            double ay = math.abs(delta.y);
            double az = math.abs(delta.z);
            double maxAxis = math.max(ax, math.max(ay, az));
            double minAxis = math.min(ax, math.min(ay, az));
            double midAxis = ax + ay + az - maxAxis - minAxis;
            double approximateLength = maxAxis + (midAxis * 0.5d) + (minAxis * 0.25d);
            double invLength = math.rcp(math.max(approximateLength, 0.000001d));
            return new Vector3(
                (float)(delta.x * invLength),
                (float)(delta.y * invLength),
                (float)(delta.z * invLength));
        }
    }
}
