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
using Hecton8.Core.Contracts;
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
    public sealed class AtlasSignalSystem : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, IAtlasSignalReadModel, IAtlasSignalDecodeSink, IGlobalRegistryHotSwapListener
    {
        private const float DefaultPulsePeriodSeconds = 683f;
        private const float DefaultDetectionThreshold = 0.05f;

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Signal Parameters ----------------------")]
        [Tooltip("Pulse period in seconds. 683 equals 11 minutes 23 seconds.")]
        [SerializeField] private float pulsePeriodSeconds = DefaultPulsePeriodSeconds;

        [Tooltip("Maximum signal detection range in meters.")]
        [SerializeField] private float maxSignalRange = 8000f;

        [Tooltip("Atlas-6 core position in world coordinates.")]
        [SerializeField] private Vector3 atlasCorePosWorld = new Vector3(0f, -5000f, 0f);

        [Tooltip("Minimum signal strength required for scanner detection.")]
        [SerializeField, Range(0f, 1f)] private float detectionThreshold = DefaultDetectionThreshold;

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
        private bool _runtimeOwnerAborted;
        private bool _hotSwapRegistered;
        private bool _saveRegistered;
        private bool _ghostManifestationAnnounced;
        private bool _identityDiscoverySynchronized;
        private bool _fullDecodeDiscoverySynchronized;
        private bool _stage2LogQueued;
        private bool _stage3LogQueued;
        private bool _stage4LogQueued;
        private bool _atlasCoreAupCached;
        private bool _atlasCoreAupValid;
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
        private ISaveService _registeredSaveService;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private int _revealNotificationMissCount;

        private const int FormalDetectionRevealStage = 2;
        private const int IdentityRevealStage = 3;
        private const int FullDecodeRevealStage = 4;
        private const double SlowTickBudgetMilliseconds = 0.2d;
        private const double DefaultSeaLevelAupY = 14.02d;
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
        private static readonly uint _RevealNotificationMissWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.RevealNotificationMiss"));
        private static readonly uint _RevealNotificationContextHash = unchecked((uint)LocHash.Compute("AtlasSignal.RevealNotification"));

        private static readonly int _ShaderSignalStrength =
            Shader.PropertyToID("_AtlasSignalStrength");

        // Throttle log - static field outside the hot path.
        private static float _nextSignalLogTime;

        private const float StrengthEpsilon = 0.01f;

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        public float CurrentStrength => _runtimeOwnerAborted ? 0f : CurrentAtlasSignalStrength01;
        public int CurrentStrengthBand => _runtimeOwnerAborted ? 0 : math.clamp(_currentStrengthBand, 0, FullDecodeRevealStage);
        public bool IsDetected =>
            !_runtimeOwnerAborted &&
            _maxRevealStageUnlocked >= FormalDetectionRevealStage &&
            CurrentAtlasSignalStrength01 >= ResolveDetectionThreshold();
        public float CurrentAtlasSignalStrength01 =>
            _runtimeOwnerAborted ? 0f : math.saturate(math.select(0f, _currentStrength, math.isfinite(_currentStrength)));
        public int CurrentAtlasSignalRevealStage => _runtimeOwnerAborted ? 0 : SanitizeRevealStage(_maxRevealStageUnlocked);
        public bool IsAtlasSignalDetected => IsDetected;
        public Vector3 AtlasCorePosition => atlasCorePosWorld;

        public AbsoluteUniversePosition AtlasCoreAup => _runtimeOwnerAborted ? default : ResolveAtlasCoreAup();
        public int CurrentRevealStage => _runtimeOwnerAborted ? 0 : SanitizeRevealStage(_maxRevealStageUnlocked);
        public int RevealNotificationMissCount => _revealNotificationMissCount;

        public bool TryReadAtlasSignalCoreAup(out AbsoluteUniversePosition coreAup)
        {
            if (_runtimeOwnerAborted)
            {
                coreAup = default;
                return false;
            }

            return TryResolveAtlasCoreAup(out coreAup);
        }

        public bool TryReadAtlasSignalSnapshot(
            in AbsoluteUniversePosition observerAup,
            out AtlasSignalReadSnapshot snapshot)
        {
            snapshot = default;
            if (_runtimeOwnerAborted || !observerAup.IsFinite())
                return false;

            if (!TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup))
                return false;

            float strength = math.saturate(math.select(0f, _currentStrength, math.isfinite(_currentStrength)));
            int revealStage = SanitizeRevealStage(_maxRevealStageUnlocked);
            float detectionThreshold01 = ResolveDetectionThreshold();
            Vector3 direction = SignalStrengthSystem.CalculateDirectionToCore(in observerAup, in coreAup);
            float3 directionToCore = new float3(direction.x, direction.y, direction.z);
            if (!math.all(math.isfinite(directionToCore)))
                directionToCore = new float3(0f, -1f, 0f);

            uint flags = 0u;
            if (revealStage >= FormalDetectionRevealStage && strength >= detectionThreshold01)
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
                if (_runtimeOwnerAborted)
                    return Vector3.down;

                if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                    return Vector3.down;

                if (!TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup))
                    return Vector3.down;

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
            if (!TryRegisterService())
                return;

            CacheEncryptedLogHashes();
            CacheRuntimeDependencies();
            TryRegisterHotSwapListener();
            TryRegister();
            TryRegisterLateFrame();
            TryRegisterSaveParticipant();

            ResolvePlayer();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterService();
            TryUnregisterSaveParticipant();

            TryUnregisterHotSwapListener();
            ClearRuntimeDependencies();
            ClearRevealNotificationDiagnostics();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterService();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            ClearRuntimeDependencies();
            ClearRevealNotificationDiagnostics();

        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        public void SlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

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
            if (_runtimeOwnerAborted)
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                ClearLiveSignalState();
                return;
            }

            _pulseTimer = math.isfinite(_pulseTimer)
                ? _pulseTimer + 0.5f
                : 0f; // SlowTick ~0.5s

            if (!TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup))
            {
                ClearLiveSignalState();
                return;
            }

            float rawStrength = CalculateRawStrength(in playerAup, in coreAup);
            int previousRevealStage = _maxRevealStageUnlocked;
            int desiredRevealStage = ResolveDesiredRevealStage(ResolveCurrentDepthMeters(in playerAup));
            if (desiredRevealStage > _maxRevealStageUnlocked)
                _maxRevealStageUnlocked = desiredRevealStage;

            float newStrength = math.min(rawStrength, ResolveRevealStrengthCap(_maxRevealStageUnlocked));
            float detectionThreshold01 = ResolveDetectionThreshold();
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
                    newStrength >= detectionThreshold01 &&
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

            if (_pulseTimer < ResolvePulsePeriodSeconds())
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

        private static int SanitizeRevealStage(int revealStage)
        {
            return math.clamp(revealStage, 0, FullDecodeRevealStage);
        }

        private float ResolvePulsePeriodSeconds()
        {
            return math.isfinite(pulsePeriodSeconds) && pulsePeriodSeconds > 0f
                ? pulsePeriodSeconds
                : DefaultPulsePeriodSeconds;
        }

        private float ResolveDetectionThreshold()
        {
            return math.isfinite(detectionThreshold) &&
                   detectionThreshold >= 0f &&
                   detectionThreshold <= 1f
                ? detectionThreshold
                : DefaultDetectionThreshold;
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted || !_pendingShaderStrengthDirty)
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
            if (_runtimeOwnerAborted)
                return;

            DecodeSignal(AtlasSignalEvents.ComputeMessageHash(messageId));
        }

        public void DecodeSignal(uint messageHash)
        {
            if (_runtimeOwnerAborted || messageHash == 0u)
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
            if (_runtimeOwnerAborted)
                return;

            _playerMovement = null;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_runtimeOwnerAborted)
            {
                playerAup = default;
                return false;
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                ResolvePlayer();

            playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite())
            {
                playerAup = default;
                return false;
            }

            playerAup = movementState.PredictedAup;
            return true;
        }

        private AbsoluteUniversePosition ResolveAtlasCoreAup()
        {
            if (!_atlasCoreAupCached || _atlasCoreAupSource != atlasCorePosWorld)
            {
                _atlasCoreAupSource = atlasCorePosWorld;
                double3 atlasCoreAup = new double3(atlasCorePosWorld.x, atlasCorePosWorld.y, atlasCorePosWorld.z);
                _atlasCoreAupValid = math.all(math.isfinite(atlasCoreAup));
                _atlasCoreAup = _atlasCoreAupValid
                    ? AbsoluteUniversePosition.FromAbsolutePosition(atlasCoreAup)
                    : default;
                _atlasCoreAupCached = true;
            }

            return _atlasCoreAup;
        }

        private bool TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup)
        {
            if (_runtimeOwnerAborted)
            {
                coreAup = default;
                return false;
            }

            coreAup = ResolveAtlasCoreAup();
            return _atlasCoreAupValid && coreAup.IsFinite();
        }

        private float ResolveCurrentDepthMeters(in AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (playerContext == null)
            {
                HectonPlayerMovement playerMovement = _playerMovement;
                if (playerMovement != null && math.isfinite(playerMovement.CurrentDepth))
                    return math.max(0f, playerMovement.CurrentDepth);
            }

            BiomeMatrixDirector biomeMatrixDirector = null;
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            if (biomeMatrixDirector != null &&
                biomeMatrixDirector.isActiveAndEnabled &&
                math.isfinite(biomeMatrixDirector.CurrentDepthMeters))
            {
                return math.max(0f, biomeMatrixDirector.CurrentDepthMeters);
            }

            double absoluteY = playerAup.ToAbsoluteDouble3().y;
            return math.max(0f, (float)(ResolveCurrentSeaLevelAupY() - absoluteY));
        }

        private double ResolveCurrentSeaLevelAupY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveSeaLevelAupY(oceanKinematics.SeaLevel, out double seaLevelAupY))
            {
                return seaLevelAupY;
            }

            return DefaultSeaLevelAupY;
        }

        private static bool TryResolveSeaLevelAupY(float candidateSeaLevelY, out double seaLevelAupY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelAupY = candidateSeaLevelY;
                return true;
            }

            seaLevelAupY = DefaultSeaLevelAupY;
            return false;
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

        private void TryRegisterLateFrame()
        {
            if (_runtimeOwnerAborted || _lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
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
            if (_runtimeOwnerAborted)
                return;

            _pendingShaderStrength = math.isfinite(strength01)
                ? math.saturate(strength01)
                : 0f;
            _pendingShaderStrengthDirty = true;
            TryRegisterLateFrame();
        }

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            AtlasSignalSystem registeredRuntime = GlobalRegistry.AtlasSignal;
            if (IsAtlasSignalRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            if (!ReferenceEquals(registeredRuntime, null) && !ReferenceEquals(registeredRuntime, this))
                GlobalRegistry.UnregisterAtlasSignalRuntime(registeredRuntime);

            GlobalRegistry.RegisterAtlasSignalRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AtlasSignal, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.AtlasSignal, this))
                GlobalRegistry.UnregisterAtlasSignalRuntime(this);

            _serviceRegistered = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            AtlasSignalSystem registeredRuntime = GlobalRegistry.AtlasSignal;
            if (ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsAtlasSignalRuntimeUsable(registeredRuntime))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_DuplicateRuntimeWarningHash, _AtlasSignalContextHash, 1f);
                AbortDuplicateRuntimeOwner();
                return true;
            }

            if (!ReferenceEquals(registeredRuntime, null))
                GlobalRegistry.UnregisterAtlasSignalRuntime(registeredRuntime);

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.AtlasSignal, this))
                GlobalRegistry.UnregisterAtlasSignalRuntime(this);

            ClearRuntimeDependencies();
            ClearRevealNotificationDiagnostics();
            _runtimeOwnerAborted = true;
            _registered = false;
            _serviceRegistered = false;
            _hotSwapRegistered = false;
            _saveRegistered = false;
            _lateFrameRegistered = false;
            _pendingShaderStrengthDirty = false;
            _pendingShaderStrength = 0f;
            enabled = false;
            Destroy(gameObject);
        }


        /// <summary>
        /// Resolve-or-create the sole AtlasSignalSystem runtime owner for
        /// GlobalRegistry.AtlasSignal (Atlas-6 pulse / reveal read-model).
        /// Script GUID a9addf4847ba6d64396043aeeec51fb3 has ZERO live scene/prefab hits.
        /// HectonLoreSystemsRoot.SetupAllSystems is editor ContextMenu-only.
        /// No Ensure existed; OnEnable only registers when already present.
        /// AudioLog, decoder, and discovery consumers hit permanent null without this path.
        /// </summary>
        public static AtlasSignalSystem EnsureRuntimeInstance()
        {
            AtlasSignalSystem registered = GlobalRegistry.AtlasSignal;
            if (IsAtlasSignalRuntimeUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterAtlasSignalRuntime(registered);
                registered._serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: zero authored scene/prefab hits for this owner.
            GameObject runtimeRoot = new GameObject("[AtlasSignalSystem]"); // COLD ALLOC
            return runtimeRoot.AddComponent<AtlasSignalSystem>();
        }

        private static bool IsAtlasSignalRuntimeUsable(AtlasSignalSystem system)
        {
            return !ReferenceEquals(system, null) &&
                   system != null &&
                   system._serviceRegistered &&
                   system.isActiveAndEnabled &&
                   !system._runtimeOwnerAborted;
        }

        private void CacheRuntimeDependencies()
        {
            if (_runtimeOwnerAborted)
                return;

            _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
            _firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHourReadModel;
            _narrativeDiscoveryReadModel = GlobalRegistry.NarrativeDiscoveryReadModel;
            CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime);
            _localization = Hecton8.Core.GlobalRegistry.LocalizationText;
            _saveService = Hecton8.Core.GlobalRegistry.Save;
            _oceanKinematicsService = Hecton8.Core.GlobalRegistry.OceanKinematics;
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
            _oceanKinematicsService = null;
        }

        private void CacheAudioLogSystem(IAudioLogRuntime audioLogSystem)
        {
            _audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null;
        }

        private IAudioLogRuntime ResolveAudioLogSystem()
        {
            IAudioLogRuntime audioLogSystem = _audioLogs;
            if (IsAudioLogRuntimeUsable(audioLogSystem))
                return audioLogSystem;

            _audioLogs = null;
            return null;
        }

        private static bool IsAudioLogRuntimeUsable(IAudioLogRuntime audioLogSystem)
        {
            if (audioLogSystem == null || !audioLogSystem.IsAudioLogRuntimeReady)
                return false;

            if (audioLogSystem is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

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
                    CacheAudioLogSystem(currentService as IAudioLogRuntime);
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    TryUnregisterLateFrame();
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
            if (_runtimeOwnerAborted || _saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = Hecton8.Core.GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveService = null;
            _saveRegistered = false;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private bool CanManifestAtlas()
        {
            if (_runtimeOwnerAborted)
                return false;

            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToManifest);
        }

        private bool CanManifestGhostBeat()
        {
            if (_runtimeOwnerAborted)
                return false;

            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector == null)
                return false;

            if (firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToManifest))
                return false;

            return firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToGhostManifest);
        }

        private void HandleRevealStageUnlocked(int revealStage, float manifestedStrength)
        {
            if (_runtimeOwnerAborted || manifestedStrength <= 0f)
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
                    if (!_signalEverDetected && manifestedStrength >= ResolveDetectionThreshold())
                    {
                        _signalEverDetected = true;
                        AtlasSignalEvents.TryRaiseDetected(atlasCorePosWorld);
                    }

                    TryQueueEncryptedLog(2);
                    TryPushRevealNotification(
                        ResolveLocalizedSpan(
                            LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_2,
                            "WEAK RHYTHMIC PATTERN CONFIRMED. CONTACT STILL UNSTABLE."),
                        warning: false,
                        revealStage);
                    break;

                case 3:
                    TryEnsureIdentityDiscoveryPublished();
                    TryQueueEncryptedLog(3);
                    TryPushRevealNotification(
                        ResolveLocalizedSpan(
                            LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_3,
                            "THE SIGNAL IS STARTING TO RETURN CONTENT FRAGMENTS. DEPTH IS CLEANING THE BEARING."),
                        warning: true,
                        revealStage);
                    break;

                case 4:
                    TryEnsureFullDecodeDiscoveryPublished();
                    TryQueueEncryptedLog(4);
                    TryPushRevealNotification(
                        ResolveLocalizedSpan(
                            LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_4,
                            "CARRIER STABLE. THE SIGNAL CAN NOW BE DRIVEN ALL THE WAY TO THE SOURCE."),
                        warning: true,
                        revealStage);
                    break;
            }

            LogRevealStageUnlocked();
        }

        private void TryPushRevealNotification(ReadOnlySpan<char> message, bool warning, int revealStage)
        {
            bool pushed = warning
                ? NotificationEvents.TryPushWarning(message)
                : NotificationEvents.TryPushInfo(message);
            if (pushed)
                return;

            ReportRevealNotificationMiss(revealStage);
        }

        private void ReportRevealNotificationMiss(int revealStage)
        {
            _revealNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _RevealNotificationMissWarningHash,
                _AtlasSignalContextHash ^ _RevealNotificationContextHash ^ unchecked((uint)revealStage),
                math.max(1, _revealNotificationMissCount));
        }

        private void ClearRevealNotificationDiagnostics()
        {
            _revealNotificationMissCount = 0;
        }

        private void TryEnsureIdentityDiscoveryPublished()
        {
            if (_runtimeOwnerAborted || _identityDiscoverySynchronized || _maxRevealStageUnlocked < IdentityRevealStage)
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
            if (_runtimeOwnerAborted || _fullDecodeDiscoverySynchronized || _maxRevealStageUnlocked < FullDecodeRevealStage)
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
            if (_runtimeOwnerAborted)
                return;

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

            IAudioLogRuntime audioLogs = ResolveAudioLogSystem();
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
            if (_runtimeOwnerAborted)
                return;

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
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextSignalLogTime)
                return;

            _nextSignalLogTime = now + 5f;
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
            if (_runtimeOwnerAborted)
                return fallback.AsSpan();

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
            if (_runtimeOwnerAborted || data == null) return;
            data.atlasSignalDetected = _signalEverDetected;
            data.atlasSignalPulseTimer = math.isfinite(_pulseTimer) ? math.max(0f, _pulseTimer) : 0f;
            data.atlasSignalRevealStage = math.clamp(_maxRevealStageUnlocked, 0, FullDecodeRevealStage);
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearRevealNotificationDiagnostics();
            if (_runtimeOwnerAborted || data == null) return;
            _signalEverDetected = data.atlasSignalDetected;
            _pulseTimer = math.isfinite(data.atlasSignalPulseTimer)
                ? math.max(0f, data.atlasSignalPulseTimer)
                : 0f;
            _maxRevealStageUnlocked = math.clamp(data.atlasSignalRevealStage, 0, FullDecodeRevealStage);
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
            if (!math.isfinite(maxRangeMeters) || maxRangeMeters <= 0f)
                return 0f;

            double safeRange = math.max(0.001f, maxRangeMeters);
            double safeRangeSq = safeRange * safeRange;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
            if (!math.isfinite(distanceSq) || distanceSq >= safeRangeSq)
                return 0f;

            float strength = (float)(1d - distanceSq / safeRangeSq);
            return math.isfinite(strength) ? math.saturate(strength) : 0f;
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
            if (!math.isfinite(strength01))
                return 0;

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
