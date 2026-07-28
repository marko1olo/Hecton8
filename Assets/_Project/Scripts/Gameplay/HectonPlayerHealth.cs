// ============================================================================
// HECTON-8 - HectonPlayerHealth.cs
// Player health system with damage, healing, and hazard effects.
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Audio;
using Hecton8.Interaction;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Hecton.Localization;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Player health component managing HP, damage, healing, and environmental effects.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Player Health")]
    public sealed class HectonPlayerHealth : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, IDamageReceiver, ICombatHitProfileSource, ICombatPushbackBodySource, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HectonPlayerHealthSignalPushDropCount;
        private const float MinimumRuntimeMaxHealth = 1f;
        private const float HealthSlowTickDeltaSeconds = 0.1f;
        private const float SurvivalGraceEligibilityThresholdNormalized = 0.10f;
        private const float SurvivalGraceHealthFloor = 0.01f;
        private const float SurvivalGraceInvulnerabilitySeconds = 0.5f;
        private const float SurvivalGraceLockoutSeconds = 8f;
        private const float RadiationFatigueMinimumScale = 0.65f;
        private const float RadiationFatigueScalePerSecond = 0.005f;
        private const float RadiationFatigueCriticalExposureSeconds =
            (1f - RadiationFatigueMinimumScale) / RadiationFatigueScalePerSecond;
        private const float GillsOxygenCapacityMultiplier = 1.25f;
        private const float BioluminescentPredatorVisibilityScale = 2f;
        private const float NutritionalToxicityRegenFloor = 0.35f;
        private const float HealingReversalToxicityThreshold01 = 0.7f;
        private const float VitalWarningHealthThreshold01 = 0.20f;
        private const float VitalWarningHealthReleaseThreshold01 = 0.28f;
        private const uint HealthRespawnDamageHash = 0x484C5448u; // HLTH
        private const float CriticalRadiationAdvisoryThresholdSeconds = 90f;
        private const float RadiationAdvisoryStageOneExposure01 = 0.30f;
        private const float RadiationAdvisoryStageTwoExposure01 = 0.70f;
        private const float LeviathanTraumaDamageThreshold01 = 0.40f;
        private const uint ShinobuPhysiologySourceHash = PhysiologyStateSignal.SourceShinobuPhysiology;
        private const byte ShinobuGasToxicitySignalCause = PhysiologyStateSignal.CauseGasToxicity;
        private const uint ShinobuGasStatusMask = PhysiologyStateSignal.GasStatusMask;
        private const uint GasPhysiologyBridgeHoldFrames = 12u;
        private const string RadiationFatigueDiscoveryId = "radiation_fatigue_advisory_30";
        private const string RadiationCriticalDiscoveryId = "radiation_critical_advisory";
        private const string LeviathanTraumaDiscoveryId = "leviathan_trauma_voice_log";
        private const string MutationDetectedMessage = "MUTATION DETECTED";
        private const string SurvivalGraceNotification = "CARDIAC OVERRIDE";
        private const string RadiationFatigueFallbackMessage = "CRITICAL ADVISORY // RADIATION LOAD 30 PERCENT";
        private const string RadiationCriticalFallbackMessage = "CRITICAL ADVISORY // RADIATION LOAD 70 PERCENT - RAD-SHIELD REQUIRED";
        private static readonly uint _radiationFatigueDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(RadiationFatigueDiscoveryId);
        private static readonly uint _radiationCriticalDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(RadiationCriticalDiscoveryId);
        private static readonly uint _leviathanTraumaDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(LeviathanTraumaDiscoveryId);
        private static readonly uint _mutationDetectedMessageHash = NotificationEvents.ComputeMessageHash(MutationDetectedMessage);
        private static readonly uint _MutationDetectedNotificationMissWarningHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.MutationDetectedNotificationMiss"));
        private static readonly uint _SurvivalGraceNotificationMissWarningHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.SurvivalGraceNotificationMiss"));
        private static readonly uint _RadiationAdvisoryNotificationMissWarningHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.RadiationAdvisoryNotificationMiss"));
        private static readonly uint _PlayerSignalEventLaneDropWarningHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.PlayerSignalEventLaneDrop"));
        private static readonly uint _PlayerHealthNotificationContextHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.Notification"));
        private static readonly uint _PlayerSignalEventLaneContextHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.PlayerSignalEventLane"));
        private static readonly uint _SurvivalGraceNotificationContextHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.SurvivalGraceNotification"));
        private static readonly uint _RadiationAdvisoryNotificationContextHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.RadiationAdvisoryNotification"));
        private static readonly uint _RadiationAdvisoryTraumaSignalContextHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.RadiationAdvisoryTraumaSignal"));
        private static readonly uint _VitalWarningTraumaSignalContextHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.VitalWarningTraumaSignal"));
        private static readonly uint _DamageFeedbackTraumaSignalContextHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.DamageFeedbackTraumaSignal"));
        private static readonly uint _LeviathanTraumaSignalContextHash = unchecked((uint)LocHash.Compute("HectonPlayerHealth.LeviathanTraumaSignal"));
        private static readonly char[] s_radiationFatigueMessage =
        {
            'C','R','I','T','I','C','A','L',' ','A','D','V','I','S','O','R','Y',' ','/','/',' ',
            'R','A','D','I','A','T','I','O','N',' ','L','O','A','D',' ','3','0',' ','P','E','R','C','E','N','T'
        };
        private static readonly char[] s_radiationCriticalMessage =
        {
            'C','R','I','T','I','C','A','L',' ','A','D','V','I','S','O','R','Y',' ','/','/',' ',
            'R','A','D','I','A','T','I','O','N',' ','L','O','A','D',' ','7','0',' ','P','E','R','C','E','N','T',' ','-',' ',
            'R','A','D','-','S','H','I','E','L','D',' ','R','E','Q','U','I','R','E','D'
        };
        // COLD ALLOC: MutationThreshold[2] - fallback mutation thresholds when no authored profile is assigned - owner: HectonPlayerHealth
        private static readonly HazardMutationProfile.MutationThreshold[] s_fallbackMutationThresholds =
        {
            new HazardMutationProfile.MutationThreshold
            {
                DisplayName = "BIOLUMINESCENT SKIN",
                ExposureThresholdSeconds = 120f,
                MutationBit = HazardMutationProfile.BioluminescentSkinBit
            },
            new HazardMutationProfile.MutationThreshold
            {
                DisplayName = "GILLS",
                ExposureThresholdSeconds = 180f,
                MutationBit = HazardMutationProfile.GillsBit
            }
        };

        /// <summary>Maximum health points.</summary>
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        /// <summary>Current health points.</summary>
        [SerializeField, ReadOnly] private float currentHealth;

        /// <summary>Invulnerability duration after taking damage.</summary>
        [SerializeField] private float invulnerabilityTime = 1f;

        [Header("Trauma Recovery")]
        [SerializeField] private AudioClip survivalGraceHeartbeatClip;
        [SerializeField, Range(0f, 1f)] private float survivalGraceHeartbeatVolume = 1f;
        [SerializeField] private HazardMutationProfile hazardMutationProfile;
        private bool _vitalWarningSignalIssued;
        private bool _lastDamageTriggeredRespawnReconciliation;
        private uint _pendingRespawnReconciliationSequence;
        private uint _lastAppliedRespawnReconciliationSequence;

        /// <summary>Gets the current health value.</summary>
        public float CurrentHealth => ResolveSafeRuntimeHealth(currentHealth, ResolveSafeRuntimeMaxHealth(maxHealth));

        /// <summary>Gets the maximum health value.</summary>
        public float MaxHealth => ResolveSafeRuntimeMaxHealth(maxHealth);

        /// <summary>Gets the health percentage (0-1).</summary>
        public float HealthPercent
        {
            get
            {
                float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
                return ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth) * math.rcp(runtimeMaxHealth);
            }
        }

        /// <summary>Current forward vector used by directional armor checks.</summary>
        public Vector3 CombatForward => transform.forward;

        /// <summary>Current body height used by local-space critical-hit fakes.</summary>
        public float CombatHeight => _combatHitCollider != null
            ? Mathf.Max(0.0001f, _combatHitCollider.bounds.size.y)
            : Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));

        /// <summary>Cached body used by the combat runtime for deferred physical pushback.</summary>
        public Rigidbody CombatPushbackBody => _combatBody;

        /// <summary>Composite panic/stress scalar from health loss and hazardous exposure.</summary>
        public float Stress
        {
            get
            {
                float healthStress = ResolveUnit01(1f - HealthPercent);
                float radiationStress = ResolveUnit01(_radiationExposureSeconds / Mathf.Max(1f, CriticalRadiationAdvisoryThresholdSeconds));
                float toxicityStress = ResolvePoisonStatus01();
                float gasStress = ResolveUnit01(_gasPhysiologyStress01);
                float pressureStress = _survivalSystem != null ? ResolveUnit01(_survivalSystem.PressureExposureSeverity01) : 0f;
                float thermalStress = _survivalSystem != null ? ResolveUnit01(_survivalSystem.ThermalStressSeverity01) : 0f;
                return ResolveUnit01(Mathf.Max(
                    healthStress,
                    Mathf.Max(radiationStress, Mathf.Max(toxicityStress, Mathf.Max(gasStress, Mathf.Max(pressureStress, thermalStress))))));
            }
        }

        /// <summary>Composite panic/stress scalar from health loss and hazardous exposure.</summary>
        public float Stress01 => Stress;

        /// <summary>Gets whether the player is alive.</summary>
        public bool IsAlive => CurrentHealth > 0f;

        internal bool RespawnReconciliationPending => _pendingRespawnReconciliationSequence != 0u;

        /// <summary>Gets whether the player is currently invulnerable.</summary>
        public bool IsInvulnerable => ResolveExpiryRemainingSeconds(_invulnerabilityExpiresAt, ResolveUnscaledNowSeconds()) > 0f;

        /// <summary>Current cumulative radiation-fatigue exposure in seconds.</summary>
        public float RadiationExposureSeconds => ResolveNonNegativeRuntimeValue(_radiationExposureSeconds);

        /// <summary>Normalized cumulative radiation exposure used by visor degradation shaders.</summary>
        public float RadiationExposure => ResolveUnit01(_radiationExposureSeconds / Mathf.Max(1f, RadiationFatigueCriticalExposureSeconds));

        /// <summary>Permanent mutation bitmask unlocked by radiation exposure.</summary>
        public uint MutationFlags => _mutationFlags;

        public int MutationDetectedNotificationMissCount => _mutationDetectedNotificationMissCount;

        public int SurvivalGraceNotificationMissCount => _survivalGraceNotificationMissCount;

        public int RadiationAdvisoryNotificationMissCount => _radiationAdvisoryNotificationMissCount;

        public int PlayerSignalEventLaneDropCount => _playerSignalEventLaneDropCount;

        /// <summary>Predator detection multiplier applied by mutation state.</summary>
        public float PredatorVisibilityScale => HasMutation(HazardMutationProfile.BioluminescentSkinBit)
            ? BioluminescentPredatorVisibilityScale
            : 1f;

        /// <summary>Runtime natural HP regeneration multiplier after food toxicity suppression.</summary>
        public float NaturalHealthRegenerationMultiplier => ResolveNaturalHealthRegenerationMultiplier(BloodToxicity01);
        /// <summary>Composite blood toxicity scalar used by medical item effects.</summary>
        public float BloodToxicity01 => Mathf.Max(
            Mathf.Max(ResolvePoisonStatus01(), RadiationExposure),
            ResolveUnit01(_gasPhysiologyToxicity01));

        /// <summary>Latest gas physiology stress scalar received from the physiology signal lane.</summary>
        public float GasPhysiologyStress01 => ResolveUnit01(_gasPhysiologyStress01);

        /// <summary>True when mutation state removes the practical need for a flashlight.</summary>
        public bool FlashlightBypassActive => HasMutation(HazardMutationProfile.BioluminescentSkinBit);

        internal void ApplyRadiationExposure(float exposureSeconds)
        {
            _radiationExposureSeconds = Mathf.Max(
                ResolveNonNegativeRuntimeValue(_radiationExposureSeconds),
                ResolveNonNegativeRuntimeValue(exposureSeconds));
            ApplyRadiationExposureExact(_radiationExposureSeconds);
        }

        internal void SetRadiationExposure(float exposureSeconds)
        {
            _radiationExposureSeconds = ResolveNonNegativeRuntimeValue(exposureSeconds);
            ApplyRadiationExposureExact(_radiationExposureSeconds);
        }

        private void ApplyRadiationExposureExact(float exposureSeconds)
        {
            float safeExposureSeconds = ResolveNonNegativeRuntimeValue(exposureSeconds);
            float fatigueScale = ResolveRadiationFatigueScale(safeExposureSeconds);
            SetRuntimeMaxHealthScaleInternal(fatigueScale);
            EvaluateMutationThresholds();
            TryIssueRadiationAdvisories();
        }

        internal static float ResolveRadiationFatigueScale(float exposureSeconds)
        {
            return SomaticSurvivalMath.ResolveRadiationFatigueScale(ResolveNonNegativeRuntimeValue(exposureSeconds));
        }

        internal static bool ShouldActivateSurvivalGrace(
            float currentHealth,
            float maximumHealth,
            float incomingDamage,
            bool ignoreInvulnerability,
            float lockoutTimer)
        {
            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maximumHealth);
            float runtimeHealth = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);
            float safeIncomingDamage = ResolveNonNegativeRuntimeValue(incomingDamage);

            if (ignoreInvulnerability ||
                lockoutTimer > 0f ||
                safeIncomingDamage < runtimeHealth)
            {
                return false;
            }

            float healthPercent = runtimeHealth * math.rcp(runtimeMaxHealth);
            return healthPercent > SurvivalGraceEligibilityThresholdNormalized;
        }

        internal void ClearRadiationFatigue()
        {
            _radiationExposureSeconds = 0f;
            SetRuntimeMaxHealthScaleInternal(1f);
        }

        private void TryIssueRadiationAdvisories()
        {
            float exposure01 = RadiationExposure;
            TryIssueRadiationAdvisory(
                exposure01,
                RadiationAdvisoryStageOneExposure01,
                ref _radiationFatigueAdvisoryIssued,
                _radiationFatigueDiscoveryHash,
                0.72f,
                0.22f,
                s_radiationFatigueMessage,
                RadiationFatigueFallbackMessage,
                false);

            TryIssueRadiationAdvisory(
                exposure01,
                RadiationAdvisoryStageTwoExposure01,
                ref _radiationCriticalAdvisoryIssued,
                _radiationCriticalDiscoveryHash,
                1f,
                0.3f,
                s_radiationCriticalMessage,
                RadiationCriticalFallbackMessage,
                true);
        }

        private void TryIssueRadiationAdvisory(
            float exposure01,
            float threshold01,
            ref bool issued,
            uint discoveryHash,
            float glitchIntensity,
            float glitchDuration,
            char[] message,
            string fallbackMessage,
            bool blocksNarrativeQueue)
        {
            if (issued || exposure01 < threshold01)
                return;

            issued = true;

            NarrativeEvents.TryRaiseDiscoveryMade(discoveryHash);

            if (blocksNarrativeQueue)
            {
                IAudioLogRuntime audioLogs = ResolveAudioLogSystem();
                if (audioLogs != null)
                    audioLogs.NotifyAtmosphericWarningStarted(glitchDuration);
            }

            TryRaiseTraumaHudSignal(
                new TraumaHudSignal(glitchIntensity, glitchDuration, 1f, Mathf.Clamp01(HealthPercent), true),
                _RadiationAdvisoryTraumaSignalContextHash ^ discoveryHash);
            ShowRadiationAdvisory(message, fallbackMessage, discoveryHash);
        }

        private void ShowRadiationAdvisory(char[] message, string fallbackMessage, uint discoveryHash)
        {
            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                TryPushRadiationAdvisoryFallbackNotification(fallbackMessage.AsSpan(), discoveryHash);
                return;
            }

            try
            {
                FixedCharBuffer buffer = new FixedCharBuffer(lease.Buffer);
                buffer.Append(message);
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                    notification.ShowCritical(in buffer);
                else
                    TryPushRadiationAdvisoryFallbackNotification(fallbackMessage.AsSpan(), discoveryHash);
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
        }

        private void TryPushRadiationAdvisoryFallbackNotification(ReadOnlySpan<char> message, uint discoveryHash)
        {
            if (NotificationEvents.TryPushCritical(message))
                return;

            ReportRadiationAdvisoryNotificationMiss(discoveryHash);
        }

        private void ReportRadiationAdvisoryNotificationMiss(uint discoveryHash)
        {
            _radiationAdvisoryNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _RadiationAdvisoryNotificationMissWarningHash,
                _PlayerHealthNotificationContextHash ^ _RadiationAdvisoryNotificationContextHash ^ discoveryHash,
                math.max(1, _radiationAdvisoryNotificationMissCount));
        }

        private void TryRaiseTraumaHudSignal(in TraumaHudSignal signal, uint contextHash)
        {
            if (PlayerSignalEvents.TryRaiseTraumaHudSignal(in signal))
                return;

            ReportPlayerSignalEventLaneDropIfBackpressured(contextHash);
        }

        private void ReportPlayerSignalEventLaneDropIfBackpressured(uint contextHash)
        {
            if (PlayerSignalEvents.PendingCount <= 0)
                return;

            _playerSignalEventLaneDropCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _PlayerSignalEventLaneDropWarningHash,
                _PlayerSignalEventLaneContextHash ^ contextHash,
                math.max(1, _playerSignalEventLaneDropCount));
        }

        internal void ApplyNutritionalToxicity(float severity01, float durationSeconds)
        {
            float clampedSeverity = ResolveUnit01(severity01);
            float clampedDuration = ResolveNonNegativeRuntimeValue(durationSeconds);
            if (clampedSeverity <= 0f || clampedDuration <= 0f)
                return;

            if (_combatDamageTargetId == 0)
                _combatDamageTargetId = CombatDamageRuntime.ResolveTargetId(gameObject);

            if (_combatDamageTargetId != 0 && CombatDamageRuntime.IsTargetRegistered(_combatDamageTargetId))
            {
                bool queued = CombatDamageRuntime.TryQueueStatusEffect(
                    _combatDamageTargetId,
                    CombatStatusBits.Poisoned64,
                    clampedDuration,
                    DamageSourceIds.EnvironmentHazard,
                    clampedSeverity);
                if (queued)
                    MarkCombatStatusReadModel(CombatStatusBits.Poisoned64);
            }
        }

        internal static float ResolveNaturalHealthRegenerationMultiplier(float toxicitySeverity01)
        {
            return SomaticSurvivalMath.ResolveNaturalHealthRegenerationMultiplier(ResolveUnit01(toxicitySeverity01));
        }

        private float ResolvePoisonStatus01()
        {
            return HasCachedCombatStatusEffect(CombatStatusBits.Poisoned64)
                ? 1f
                : 0f;
        }

        internal bool HasMutation(uint mutationBit)
        {
            return (_mutationFlags & mutationBit) != 0u;
        }

        private void SetRuntimeMaxHealthScaleInternal(float scale)
        {
            float safeBaseMaxHealth = ResolveSafeRuntimeMaxHealth(_baseMaxHealth);
            float minScale = MinimumRuntimeMaxHealth / safeBaseMaxHealth;
            float safeScale = math.isfinite(scale) ? scale : 1f;
            float clampedScale = Mathf.Clamp(safeScale, minScale, 1f);
            float nextMaxHealth = ResolveSafeRuntimeMaxHealth(safeBaseMaxHealth * clampedScale);
            float nextCurrentHealth = ResolveSafeRuntimeHealth(currentHealth, nextMaxHealth);
            if (Mathf.Approximately(_runtimeMaxHealthScale, clampedScale) &&
                Mathf.Approximately(maxHealth, nextMaxHealth) &&
                Mathf.Approximately(currentHealth, nextCurrentHealth))
            {
                return;
            }

            _baseMaxHealth = safeBaseMaxHealth;
            _runtimeMaxHealthScale = clampedScale;
            maxHealth = nextMaxHealth;
            currentHealth = nextCurrentHealth;
            MarkCombatDamageSyncDirty();
        }

        // Private state
        private double _invulnerabilityExpiresAt;
        private double _survivalGraceLockoutExpiresAt;
        private bool _isInitialized;
        private bool _registeredToSlowTickManager;
        private bool _registeredToLateFrameTick;
        private float _baseMaxHealth = 100f;
        private float _runtimeMaxHealthScale = 1f;
        private float _radiationExposureSeconds;
        private float _gasPhysiologyStress01;
        private float _gasPhysiologyToxicity01;
        private ulong _cachedCombatStatusMask;
        private int _lastGasPhysiologySequence;
        private uint _lastGasPhysiologyFrame;
        private bool _hasCachedCombatStatusMask;
        private uint _mutationFlags;
        private int _mutationDetectedNotificationMissCount;
        private int _survivalGraceNotificationMissCount;
        private int _radiationAdvisoryNotificationMissCount;
        private int _playerSignalEventLaneDropCount;
        private bool _radiationFatigueAdvisoryIssued;
        private bool _radiationCriticalAdvisoryIssued;
        private bool _leviathanTraumaAdvisoryIssued;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private Collider _combatHitCollider;
        private Rigidbody _combatBody;
        private Vector3 _lastKnownRuntimePosition;
        private int _combatDamageTargetId;
        private bool _combatDamageRegistered;
        private bool _combatDamageSyncDirty;
        private bool _interactionTargetRegistered;
        private bool _hotSwapRegistered;
        private bool _pendingSurvivalGraceHeartbeatPulse;
        private bool _pendingLeviathanTraumaRoar;
        private Vector3 _pendingLeviathanTraumaRoarPosition;
        private bool _saveRegistered;
        private IAudioService _audioService;
        private IAudioLogRuntime _audioLogs;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            s_x001HectonPlayerHealthSignalPushDropCount = 0;
        }

        /// <summary>
        /// Installs the player health owner on the bootstrap-published player root when the authored
        /// prefab does not already carry one.
        /// </summary>
        /// <param name="playerRoot">Bootstrap-published player root.</param>
        internal static void EnsureOnPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            if (playerRoot.TryGetComponent(out HectonPlayerHealth _))
                return;

            // Unguarded on purpose - see the rationale block at
            // PlayerRuntimeContextService.SyncPlayerContextColdInternal, which owns the call order and
            // the inactive-player-root argument. Short form: this is the player's only damage/injury
            // owner, the only ISaveable that writes SaveData.playerStats.health (:1287), and the only
            // CombatEntityKind.Player target handed to CombatDamageRuntime (:1531). An editor-only
            // guard would ship a player build with no health model at all.
            playerRoot.AddComponent<HectonPlayerHealth>(); // COLD ALLOC: HectonPlayerHealth[1] - player damage/injury/toxicity owner install on the bootstrap-published player root - owner: PlayerRuntimeContextService
        }

        /// <summary>Initializes the health system.</summary>
        private void Awake()
        {
            if (!_isInitialized)
            {
                _baseMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
                maxHealth = _baseMaxHealth;
                currentHealth = maxHealth;
                NotificationEvents.RegisterMessage(MutationDetectedMessage.AsSpan());
                TryGetComponent(out _survivalSystem);
                TryGetComponent(out _playerMovement);
                TryGetComponent(out _combatHitCollider);
                TryGetComponent(out _combatBody);
                _combatDamageTargetId = CombatDamageRuntime.ResolveTargetId(gameObject);
                CacheRegistryServicesCold();
                ApplyMutationRuntimeEffects();
                _isInitialized = true;
            }
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            TryRegisterSaveParticipant();
            TryRegisterToSlowTickManager();
            TryRegisterCombatDamageTarget();
        }

        private void Start()
        {
            TryRegisterToSlowTickManager();
            TryRegisterCombatDamageTarget();
        }

        private void OnDisable()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterCombatDamageTarget();
            TryUnregisterFromSlowTickManager();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
            _pendingSurvivalGraceHeartbeatPulse = false;
            _pendingLeviathanTraumaRoar = false;
            ClearPendingRespawnReconciliation();
            ClearPlayerHealthNotificationDiagnostics();
            ClearPlayerHealthSignalDiagnostics();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterCombatDamageTarget();
            TryUnregisterFromSlowTickManager();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
            _pendingSurvivalGraceHeartbeatPulse = false;
            _pendingLeviathanTraumaRoar = false;
            ClearPendingRespawnReconciliation();
            ClearPlayerHealthNotificationDiagnostics();
            ClearPlayerHealthSignalDiagnostics();
            ClearCachedRegistryServices();
        }

        /// <summary>Updates low-frequency status and physiology bridge timers.</summary>
        public void SlowTick()
        {
            ConsumeCommittedRespawnReconciliationSignals();
            TryRegisterCombatDamageTarget();
            TryFlushCombatDamageSync();
            RefreshCombatStatusMaskCache();
            UpdateGasPhysiologyBridge(HealthSlowTickDeltaSeconds);
        }

        public void LateFrameTick()
        {
            ConsumeCommittedRespawnReconciliationSignals();
            FlushQueuedPresentationFeedback();
        }

        /// <summary>Applies damage to the player.</summary>
        /// <param name="damage">Amount of damage to apply.</param>
        /// <param name="ignoreInvulnerability">Whether to ignore invulnerability frames.</param>
        /// <returns>True if damage was applied, false if blocked by invulnerability.</returns>
        public bool TakeDamage(float damage, bool ignoreInvulnerability = false)
        {
            _lastDamageTriggeredRespawnReconciliation = false;

            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            currentHealth = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);
            if (currentHealth <= 0f || (!ignoreInvulnerability && IsInvulnerable))
                return false;

            float appliedDamage = ResolveNonNegativeRuntimeValue(damage);
            bool graceTriggered = TryActivateSurvivalGrace(appliedDamage, ignoreInvulnerability, out float clampedDamage);
            if (graceTriggered)
                appliedDamage = clampedDamage;

            currentHealth = Mathf.Max(0, currentHealth - appliedDamage);

            if (!ignoreInvulnerability && !graceTriggered)
                ExtendInvulnerability(invulnerabilityTime);

            if (currentHealth <= 0)
            {
                PublishDeath();

                _lastDamageTriggeredRespawnReconciliation = TryApplyRespawnReconciliation(HealthRespawnDamageHash);
                return true;
            }

            MarkCombatDamageSyncDirty();
            TryIssueVitalWarningSignal();

            return true;
        }

        public bool TakeLeviathanDamage(float damage)
        {
            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            float previousHealth = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);
            bool applied = TakeDamage(damage);
            if (!applied)
                return false;

            if (_lastDamageTriggeredRespawnReconciliation)
                return true;

            TryIssueLeviathanTraumaAdvisory(previousHealth - currentHealth);
            return true;
        }

        /// <summary>Heals the player.</summary>
        /// <param name="amount">Amount of healing to apply.</param>
        /// <returns>Actual amount healed.</returns>
        public float Heal(float amount)
        {
            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            currentHealth = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);
            if (currentHealth <= 0f) return 0;

            float positiveAmount = ResolveNonNegativeRuntimeValue(amount);
            if (BloodToxicity01 >= HealingReversalToxicityThreshold01 && positiveAmount > 0f)
                return 0f;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(runtimeMaxHealth, currentHealth + positiveAmount);
            float actualHeal = currentHealth - previousHealth;

            if (actualHeal > 0)
            {
                MarkCombatDamageSyncDirty();
                RefreshVitalWarningSignalReset();
            }

            return actualHeal;
        }

        private void TryIssueVitalWarningSignal()
        {
            if (_vitalWarningSignalIssued || HealthPercent > VitalWarningHealthThreshold01)
                return;

            _vitalWarningSignalIssued = true;
            TryRaiseTraumaHudSignal(
                new TraumaHudSignal(1f, 0.85f, 1f, Mathf.Clamp01(HealthPercent), true),
                _VitalWarningTraumaSignalContextHash);
            VitalWarningSignal signal = default;
            signal.WarningHash = VocalWarningHashes.OxygenLow;
            signal.SourceId = 0u;
            signal.Vital01 = math.saturate(1f - HealthPercent);
            signal.Severity01 = math.saturate(1f - HealthPercent);
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            signal.Priority = (byte)VocalWarningId.OxygenLow;
            signal.Flags = 0;
            SignalBus<VitalWarningSignal>.TryPushTracked(in signal, ref s_x001HectonPlayerHealthSignalPushDropCount);
        }

        private void RefreshVitalWarningSignalReset()
        {
            if (HealthPercent >= VitalWarningHealthReleaseThreshold01)
                _vitalWarningSignalIssued = false;
        }

        /// <summary>Kills the player instantly.</summary>
        public void Kill()
        {
            _lastDamageTriggeredRespawnReconciliation = false;

            if (!IsAlive) return;

            currentHealth = 0;
            PublishDeath();
            TryApplyRespawnReconciliation(HealthRespawnDamageHash);
        }

        private void PublishDeath()
        {
            MarkCombatDamageSyncDirty();
        }

        /// <summary>Resets health to maximum.</summary>
        public void FullHeal()
        {
            Heal(ResolveSafeRuntimeMaxHealth(maxHealth));
        }

        private bool TryActivateSurvivalGrace(float incomingDamage, bool ignoreInvulnerability, out float clampedDamage)
        {
            clampedDamage = incomingDamage;
            double now = ResolveUnscaledNowSeconds();
            float lockoutRemaining = ResolveExpiryRemainingSeconds(_survivalGraceLockoutExpiresAt, now);
            if (!ShouldActivateSurvivalGrace(
                    currentHealth,
                    maxHealth,
                    incomingDamage,
                    ignoreInvulnerability,
                    lockoutRemaining))
            {
                return false;
            }

            clampedDamage = Mathf.Max(0f, currentHealth - SurvivalGraceHealthFloor);
            ExtendInvulnerability(SurvivalGraceInvulnerabilitySeconds, now);
            _survivalGraceLockoutExpiresAt = ResolveExpirySeconds(now, SurvivalGraceLockoutSeconds);
            PlaySurvivalGraceHeartbeatPulse();
            TryPushSurvivalGraceNotification();
            return true;
        }

        private void TryPushSurvivalGraceNotification()
        {
            if (NotificationEvents.TryPushCritical(SurvivalGraceNotification.AsSpan()))
                return;

            ReportSurvivalGraceNotificationMiss();
        }

        private void ReportSurvivalGraceNotificationMiss()
        {
            _survivalGraceNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _SurvivalGraceNotificationMissWarningHash,
                _PlayerHealthNotificationContextHash ^ _SurvivalGraceNotificationContextHash,
                math.max(1, _survivalGraceNotificationMissCount));
        }

        private void ExtendInvulnerability(float durationSeconds)
        {
            ExtendInvulnerability(durationSeconds, ResolveUnscaledNowSeconds());
        }

        private void ExtendInvulnerability(float durationSeconds, double now)
        {
            double expiry = ResolveExpirySeconds(now, durationSeconds);
            if (expiry > _invulnerabilityExpiresAt)
                _invulnerabilityExpiresAt = expiry;
        }

        private static double ResolveExpirySeconds(double now, float durationSeconds)
        {
            float duration = math.isfinite(durationSeconds) ? Mathf.Max(0f, durationSeconds) : 0f;
            return now + duration;
        }

        private static float ResolveExpiryRemainingSeconds(double expirySeconds, double now)
        {
            if (!IsFiniteDouble(expirySeconds) || !IsFiniteDouble(now) || expirySeconds <= now)
                return 0f;

            double remaining = expirySeconds - now;
            return remaining >= float.MaxValue ? float.MaxValue : (float)remaining;
        }

        private static double ResolveUnscaledNowSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            return IsFiniteDouble(now) && now > 0d ? now : 0d;
        }

        private static bool IsFiniteDouble(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void UpdateGasPhysiologyBridge(float deltaTime)
        {
            float stress01 = 0f;
            float toxicity01 = 0f;
            bool anyGasSignal = false;
            int sequence = SignalBus<PhysiologyStateSignal>.SnapshotGeneration;
            uint currentFrame = TimeSliceScheduler.CurrentFrameId;
            uint latestGasFrame = _lastGasPhysiologyFrame;
            var signals = SignalBus<PhysiologyStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysiologyStateSignal signal = signals[i];
                if (signal.SourceHash != ShinobuPhysiologySourceHash)
                    continue;

                bool gasSignal = signal.Cause == ShinobuGasToxicitySignalCause ||
                                 (signal.StatusFlags & ShinobuGasStatusMask) != 0u;
                if (!gasSignal)
                {
                    stress01 = Mathf.Max(stress01, ResolveUnit01(signal.PlayerStress01));
                    continue;
                }

                anyGasSignal = true;
                latestGasFrame = signal.Frame != 0u ? signal.Frame : currentFrame;
                float signalStress01 = ResolveUnit01(signal.PlayerStress01);
                float narcosis01 = ResolveUnit01(signal.Narcosis01);
                stress01 = Mathf.Max(stress01, Mathf.Max(signalStress01, narcosis01));
                toxicity01 = Mathf.Max(toxicity01, signalStress01);
            }

            if (anyGasSignal)
            {
                _gasPhysiologyStress01 = stress01;
                _gasPhysiologyToxicity01 = toxicity01;
                _lastGasPhysiologySequence = sequence;
                _lastGasPhysiologyFrame = latestGasFrame != 0u ? latestGasFrame : currentFrame;
                return;
            }

            if (IsRecentGasPhysiologyFrame(_lastGasPhysiologyFrame, currentFrame))
            {
                _lastGasPhysiologySequence = sequence;
                return;
            }

            if (_lastGasPhysiologySequence == sequence)
                return;

            float decay = ResolveNonNegativeRuntimeValue(deltaTime) * 0.5f;
            _gasPhysiologyStress01 = Mathf.MoveTowards(_gasPhysiologyStress01, 0f, decay);
            _gasPhysiologyToxicity01 = Mathf.MoveTowards(_gasPhysiologyToxicity01, 0f, decay);
            _lastGasPhysiologySequence = sequence;
        }

        private void RefreshCombatStatusMaskCache()
        {
            if (_combatDamageTargetId == 0)
                _combatDamageTargetId = CombatDamageRuntime.ResolveTargetId(gameObject);

            if (_combatDamageTargetId == 0)
            {
                _cachedCombatStatusMask = 0UL;
                _hasCachedCombatStatusMask = false;
                return;
            }

            if (!CombatDamageRuntime.TryGetStatusEffectMask(_combatDamageTargetId, out ulong activeMask))
                return;

            _cachedCombatStatusMask = activeMask & CombatStatusBits.KnownRuntimeMask64;
            _hasCachedCombatStatusMask = true;
        }

        private void MarkCombatStatusReadModel(ulong statusMask)
        {
            _cachedCombatStatusMask |= statusMask & CombatStatusBits.KnownRuntimeMask64;
            _hasCachedCombatStatusMask = true;
        }

        private bool HasCachedCombatStatusEffect(ulong statusMask)
        {
            return _hasCachedCombatStatusMask && (_cachedCombatStatusMask & statusMask) != 0UL;
        }

        private static bool IsRecentGasPhysiologyFrame(uint signalFrame, uint currentFrame)
        {
            if (signalFrame == 0u || currentFrame == 0u)
                return false;

            uint delta = currentFrame >= signalFrame
                ? currentFrame - signalFrame
                : uint.MaxValue - signalFrame + currentFrame + 1u;
            return delta <= GasPhysiologyBridgeHoldFrames;
        }

        private void PlaySurvivalGraceHeartbeatPulse()
        {
            if (ResolveAudioService() == null || survivalGraceHeartbeatClip == null)
                return;

            _pendingSurvivalGraceHeartbeatPulse = true;
            TryRegisterLateFrameTickable();
        }

        private void FlushQueuedPresentationFeedback()
        {
            if (!_pendingSurvivalGraceHeartbeatPulse && !_pendingLeviathanTraumaRoar)
            {
                if (_pendingRespawnReconciliationSequence == 0u)
                    TryUnregisterLateFrameTickable();
                return;
            }

            bool heartbeat = _pendingSurvivalGraceHeartbeatPulse;
            bool leviathanRoar = _pendingLeviathanTraumaRoar;
            Vector3 leviathanRoarPosition = _pendingLeviathanTraumaRoarPosition;
            _pendingSurvivalGraceHeartbeatPulse = false;
            _pendingLeviathanTraumaRoar = false;

            IAudioService audioService = ResolveAudioService();
            if (heartbeat && audioService != null && survivalGraceHeartbeatClip != null)
                audioService.PlayStatic2D(survivalGraceHeartbeatClip, survivalGraceHeartbeatVolume);

            if (leviathanRoar)
            {
                ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                    leviathanRoarPosition,
                    1f,
                    0.6f,
                    1f,
                    260f,
                    ProceduralAudioPingKind.LeviathanRoar);
            }

            if (_pendingRespawnReconciliationSequence == 0u)
                TryUnregisterLateFrameTickable();
        }

        public void ReceiveDamage(in DamagePacket packet)
        {
            if (packet.Channel != DamageChannel.Integrity || packet.Magnitude <= 0f)
                return;

            if (TryApplyAuthoritativeCombatDamagePacket(in packet, out float authoritativeDamage))
            {
                if (_lastDamageTriggeredRespawnReconciliation)
                    return;

                PublishDamageFeedback(in packet, authoritativeDamage);
                return;
            }

            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            float previousHealth = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);
            bool applied = TakeDamage(packet.Magnitude);
            if (!applied)
            {
                MarkCombatDamageSyncDirty();
                return;
            }

            if (_lastDamageTriggeredRespawnReconciliation)
                return;

            float appliedDamage = Mathf.Max(0f, previousHealth - currentHealth);
            PublishDamageFeedback(in packet, appliedDamage);
        }

        private bool TryApplyAuthoritativeCombatDamagePacket(in DamagePacket packet, out float appliedDamage)
        {
            appliedDamage = 0f;

            if (!math.isfinite(packet.PreviousValue) ||
                !math.isfinite(packet.NextValue) ||
                packet.PreviousValue <= packet.NextValue)
            {
                return false;
            }

            _lastDamageTriggeredRespawnReconciliation = false;

            float safeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            float previousHealth = ResolveSafeRuntimeHealth(currentHealth, safeMaxHealth);
            float packetNextHealth = math.clamp(packet.NextValue, 0f, safeMaxHealth);
            currentHealth = math.min(previousHealth, packetNextHealth);
            appliedDamage = Mathf.Max(0f, previousHealth - currentHealth);

            if (currentHealth <= 0f)
            {
                PublishDeath();

                _lastDamageTriggeredRespawnReconciliation = TryApplyRespawnReconciliation(HealthRespawnDamageHash);
                return true;
            }

            TryIssueVitalWarningSignal();
            return true;
        }

        private void PublishDamageFeedback(in DamagePacket packet, float appliedDamage)
        {
            if (packet.SourceId == DamageSourceIds.FaunaLeviathanBite)
                TryIssueLeviathanTraumaAdvisory(appliedDamage);

            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            float severity01 = Mathf.Clamp01(ResolveNonNegativeRuntimeValue(appliedDamage) * math.rcp(runtimeMaxHealth));
            TryRaiseTraumaHudSignal(
                new TraumaHudSignal(
                    Mathf.Clamp01(severity01 * 2f),
                    severity01,
                    1f,
                    Mathf.Clamp01(HealthPercent),
                    false),
                _DamageFeedbackTraumaSignalContextHash ^ unchecked((uint)packet.SourceId));
        }

        private void TryIssueLeviathanTraumaAdvisory(float appliedDamage)
        {
            float safeAppliedDamage = ResolveNonNegativeRuntimeValue(appliedDamage);
            if (_leviathanTraumaAdvisoryIssued ||
                safeAppliedDamage < ResolveSafeRuntimeMaxHealth(maxHealth) * LeviathanTraumaDamageThreshold01)
            {
                return;
            }

            _leviathanTraumaAdvisoryIssued = true;
            NarrativeEvents.TryRaiseDiscoveryMade(_leviathanTraumaDiscoveryHash);
            _pendingLeviathanTraumaRoarPosition = CapturePlayerRuntimePositionForPresentation();
            _pendingLeviathanTraumaRoar = true;
            TryRegisterLateFrameTickable();
            TryRaiseTraumaHudSignal(
                new TraumaHudSignal(1f, 0.7f, 1f, Mathf.Clamp01(HealthPercent), true),
                _LeviathanTraumaSignalContextHash);
        }

        private void EvaluateMutationThresholds()
        {
            float radiationExposureSeconds = RadiationExposureSeconds;
            HazardMutationProfile profile = hazardMutationProfile;
            HazardMutationProfile.MutationThreshold[] thresholds = profile != null && profile.Mutations != null && profile.Mutations.Length > 0
                ? profile.Mutations
                : s_fallbackMutationThresholds;
            for (int i = 0; i < thresholds.Length; i++)
            {
                HazardMutationProfile.MutationThreshold threshold = thresholds[i];
                if (threshold.MutationBit == 0u || radiationExposureSeconds < threshold.ExposureThresholdSeconds)
                    continue;

                if ((_mutationFlags & threshold.MutationBit) != 0u)
                    continue;

                _mutationFlags |= threshold.MutationBit;
                ApplyMutationRuntimeEffects();
                TryPushMutationDetectedNotification();
            }
        }

        private void TryPushMutationDetectedNotification()
        {
            if (NotificationEvents.TryPushRegisteredWarning(_mutationDetectedMessageHash))
                return;

            ReportMutationDetectedNotificationMiss();
        }

        private void ReportMutationDetectedNotificationMiss()
        {
            _mutationDetectedNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _MutationDetectedNotificationMissWarningHash,
                _PlayerHealthNotificationContextHash,
                math.max(1, _mutationDetectedNotificationMissCount));
        }

        private void ClearPlayerHealthNotificationDiagnostics()
        {
            _mutationDetectedNotificationMissCount = 0;
            _survivalGraceNotificationMissCount = 0;
            _radiationAdvisoryNotificationMissCount = 0;
        }

        private void ClearPlayerHealthSignalDiagnostics()
        {
            _playerSignalEventLaneDropCount = 0;
        }

        private static float ResolveSafeRuntimeMaxHealth(float configuredMaxHealth)
        {
            return math.isfinite(configuredMaxHealth) && configuredMaxHealth >= MinimumRuntimeMaxHealth
                ? configuredMaxHealth
                : MinimumRuntimeMaxHealth;
        }

        private static float ResolveSafeRuntimeHealth(float runtimeHealth, float runtimeMaxHealth)
        {
            return math.isfinite(runtimeHealth)
                ? Mathf.Clamp(runtimeHealth, 0f, runtimeMaxHealth)
                : runtimeMaxHealth;
        }

        private static float ResolveNonNegativeRuntimeValue(float value)
        {
            return math.isfinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static float ResolveUnit01(float value)
        {
            return math.isfinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private void ApplyMutationRuntimeEffects()
        {
            if (_survivalSystem == null)
                return;

            float oxygenCapacityMultiplier = HasMutation(HazardMutationProfile.GillsBit)
                ? GillsOxygenCapacityMultiplier
                : 1f;
            _survivalSystem.SetRuntimeOxygenCapacityMultiplier(oxygenCapacityMultiplier);
        }

        private Vector3 CapturePlayerRuntimePositionForPresentation()
        {
            if (TryResolveActivePlayerAup(out AbsoluteUniversePosition activeAup, out bool hasRuntimeContext))
            {
                if (TryResolveRuntimePositionFromAup(in activeAup, out Vector3 runtimePosition))
                {
                    _lastKnownRuntimePosition = runtimePosition;
                    return _lastKnownRuntimePosition;
                }
            }

            if (hasRuntimeContext)
                return _lastKnownRuntimePosition;

            if (_playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;
                if (TryResolveRuntimePositionFromAup(in currentAup, out Vector3 runtimePosition))
                {
                    _lastKnownRuntimePosition = runtimePosition;
                    return _lastKnownRuntimePosition;
                }
            }

            return _lastKnownRuntimePosition;
        }

        internal bool TryResolveRespawnDeathAup(out double3 deathAup)
        {
            deathAup = default;

            if (TryResolveActivePlayerAup(out AbsoluteUniversePosition activeAup, out bool hasRuntimeContext))
            {
                deathAup = activeAup.ToAbsoluteDouble3();
                return math.all(math.isfinite(deathAup));
            }

            if (hasRuntimeContext)
                return false;

            if (_playerMovement != null)
            {
                var currentAup = _playerMovement.CurrentAup;
                if (math.isfinite(currentAup.LocalX) &&
                    math.isfinite(currentAup.LocalY) &&
                    math.isfinite(currentAup.LocalZ))
                {
                    deathAup = currentAup.ToAbsoluteDouble3();
                    return math.all(math.isfinite(deathAup));
                }
            }

            return false;
        }

        private static bool TryResolveActivePlayerAup(
            out AbsoluteUniversePosition playerAup,
            out bool hasRuntimeContext)
        {
            playerAup = default;
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            hasRuntimeContext = runtimeContext != null;
            if (!hasRuntimeContext)
                return false;

            if (runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                snapshot.Aup.IsFinite())
            {
                playerAup = snapshot.Aup;
                return true;
            }

            if (!runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite())
            {
                return false;
            }

            playerAup = movementState.PredictedAup;
            return true;
        }

        private static bool TryResolveRuntimePositionFromAup(
            in AbsoluteUniversePosition playerAup,
            out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!playerAup.IsFinite())
                return false;

            float3 runtimePosition3 = playerAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtimePosition3)))
                return false;

            runtimePosition.x = runtimePosition3.x;
            runtimePosition.y = runtimePosition3.y;
            runtimePosition.z = runtimePosition3.z;
            return true;
        }

        internal void ApplyRespawnReconciliationHealth(float normalizedHealth)
        {
            float safeHealth01 = Mathf.Clamp01(math.isfinite(normalizedHealth) ? normalizedHealth : 1f);
            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            currentHealth = Mathf.Max(1f, runtimeMaxHealth * safeHealth01);
            currentHealth = Mathf.Min(currentHealth, runtimeMaxHealth);
            ExtendInvulnerability(SurvivalGraceInvulnerabilitySeconds);
            _survivalGraceLockoutExpiresAt = 0d;
            _vitalWarningSignalIssued = false;
            _leviathanTraumaAdvisoryIssued = false;
            MarkCombatDamageSyncDirty();
            RefreshVitalWarningSignalReset();
        }

        private bool TryApplyRespawnReconciliation(uint damageHash)
        {
            uint playerHash = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId()));
            bool hasDeathAup = TryResolveRespawnDeathAup(out double3 deathAup);
            if (!hasDeathAup)
                deathAup = MissingRespawnDeathAup();

            bool accepted = PlayerDeathReconciliationBridge.RequestRespawn(deathAup, damageHash, playerHash, out uint sequence);
            if (accepted)
            {
                _pendingRespawnReconciliationSequence = sequence;
                TryRegisterLateFrameTickable();
                return true;
            }

            return false;
        }

        private void ConsumeCommittedRespawnReconciliationSignals()
        {
            uint pendingSequence = _pendingRespawnReconciliationSequence;
            if (pendingSequence == 0u || pendingSequence == _lastAppliedRespawnReconciliationSequence)
                return;

            ReadOnlySpan<PlayerRespawnSignal> signals = SignalBus<PlayerRespawnSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return;

            uint playerHash = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId()));
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerRespawnSignal signal = signals[i];
                if (!PlayerDeathReconciliationBridge.IsAcceptedCommittedRespawnSignal(in signal, pendingSequence, playerHash))
                    continue;

                ApplyRespawnReconciliationHealth(1f);
                _lastAppliedRespawnReconciliationSequence = pendingSequence;
                _pendingRespawnReconciliationSequence = 0u;
                return;
            }
        }

        private void ClearPendingRespawnReconciliation()
        {
            _pendingRespawnReconciliationSequence = 0u;
        }

        private static double3 MissingRespawnDeathAup()
        {
            double3 missing = default;
            missing.x = double.NaN;
            missing.y = double.NaN;
            missing.z = double.NaN;
            return missing;
        }

        #region ISaveable Implementation

        /// <summary>Save priority for health data.</summary>
        public int SavePriority => 100; // Save after survival so this owner supplies only playerStats.health.

        /// <summary>Load priority for health data.</summary>
        public int LoadPriority => 100; // Load after survival, then refresh combat/UI warning state from persisted HP.

        /// <summary>Populates save data with current health state.</summary>
        /// <param name="data">The save data container to populate.</param>
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            ref PlayerStatsDTO dto = ref data.playerStats;
            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            dto.health = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);
        }

        /// <summary>Loads health state from save data.</summary>
        /// <param name="data">The save data container to load from.</param>
        public void LoadFromSaveData(SaveData data)
        {
            ClearPlayerHealthNotificationDiagnostics();
            ClearPlayerHealthSignalDiagnostics();
            ClearPendingRespawnReconciliation();
            if (data == null)
                return;

            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            currentHealth = ResolveSafeRuntimeHealth(data.playerStats.health, runtimeMaxHealth);
            MarkCombatDamageSyncDirty();
            RefreshVitalWarningSignalReset();
            TryIssueVitalWarningSignal();
        }

        private void CacheRegistryServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime);
        }

        private void ClearCachedRegistryServices()
        {
            _audioService = null;
            _audioLogs = null;
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

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            OnRegistryServiceReplaced(serviceSlot, previousService, currentService);
        }

        private void OnRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    CacheAudioLogSystem(currentService as IAudioLogRuntime);
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    if (isActiveAndEnabled)
                        TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterFromSlowTickManager();
                    TryUnregisterLateFrameTickable();
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegisterToSlowTickManager();
                        if (_pendingSurvivalGraceHeartbeatPulse ||
                            _pendingLeviathanTraumaRoar ||
                            _pendingRespawnReconciliationSequence != 0u)
                        {
                            TryRegisterLateFrameTickable();
                        }
                    }
                    break;
            }
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
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

        private void TryRegisterToSlowTickManager()
        {
            if (_registeredToSlowTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToSlowTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterFromSlowTickManager()
        {
            if (!_registeredToSlowTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            _registeredToSlowTickManager = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredToLateFrameTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredToLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredToLateFrameTick = false;
        }

        private void TryRegisterCombatDamageTarget()
        {
            if (!Application.isPlaying)
                return;

            TryRegisterInteractionTargetTree();

            if (_combatDamageRegistered)
                return;

            if (_combatDamageTargetId == 0)
                _combatDamageTargetId = CombatDamageRuntime.ResolveTargetId(gameObject);

            _combatDamageRegistered = CombatDamageRuntime.RegisterTarget(
                _combatDamageTargetId,
                this,
                currentHealth,
                maxHealth,
                CombatEntityKind.Player,
                CombatArmorClass.Suit,
                0f,
                0f);
            _combatDamageSyncDirty = !_combatDamageRegistered;
        }

        private void TryUnregisterCombatDamageTarget()
        {
            TryUnregisterInteractionTargetTree();

            if (!_combatDamageRegistered)
                return;

            CombatDamageRuntime.UnregisterTarget(_combatDamageTargetId, this);
            _combatDamageRegistered = false;
            _combatDamageSyncDirty = false;
            _cachedCombatStatusMask = 0UL;
            _hasCachedCombatStatusMask = false;
        }

        private void TryRegisterInteractionTargetTree()
        {
            if (_interactionTargetRegistered || !Application.isPlaying)
                return;

            InteractableRegistry.RegisterTree(this);
            _interactionTargetRegistered = true;
        }

        private void TryUnregisterInteractionTargetTree()
        {
            if (!_interactionTargetRegistered)
                return;

            InteractableRegistry.InvalidateTree(this);
            _interactionTargetRegistered = false;
        }

        private void MarkCombatDamageSyncDirty()
        {
            if (!_combatDamageRegistered)
                return;

            _combatDamageSyncDirty = !TrySyncCombatDamageTargetHealth();
        }

        private void TryFlushCombatDamageSync()
        {
            if (!_combatDamageRegistered || !_combatDamageSyncDirty)
                return;

            _combatDamageSyncDirty = !TrySyncCombatDamageTargetHealth();
        }

        private bool TrySyncCombatDamageTargetHealth()
        {
            float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);
            float runtimeHealth = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);
            return CombatDamageRuntime.SyncTargetHealth(_combatDamageTargetId, runtimeHealth, runtimeMaxHealth);
        }

        #endregion
    }
}
