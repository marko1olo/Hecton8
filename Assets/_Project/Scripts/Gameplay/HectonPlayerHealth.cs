// ============================================================================
// HECTON-8 - HectonPlayerHealth.cs
// Player health system with damage, healing, and hazard effects.
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Audio;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Player health component managing HP, damage, healing, and environmental effects.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Player Health")]
    public sealed class HectonPlayerHealth : MonoBehaviour, ISaveable, ISlowTickable, IDamageReceiver, ICombatHitProfileSource, ICombatPushbackBodySource, IGlobalRegistryHotSwapListener
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
        private const string RadiationFatigueFallbackMessage = "CRITICAL ADVISORY // RADIATION LOAD 30 PERCENT";
        private const string RadiationCriticalFallbackMessage = "CRITICAL ADVISORY // RADIATION LOAD 70 PERCENT - RAD-SHIELD REQUIRED";
        private static readonly uint _radiationFatigueDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(RadiationFatigueDiscoveryId);
        private static readonly uint _radiationCriticalDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(RadiationCriticalDiscoveryId);
        private static readonly uint _leviathanTraumaDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(LeviathanTraumaDiscoveryId);
        private static readonly uint _mutationDetectedMessageHash = NotificationEvents.ComputeMessageHash(MutationDetectedMessage);
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

        /// <summary>Gets the current health value.</summary>
        public float CurrentHealth => currentHealth;

        /// <summary>Gets the maximum health value.</summary>
        public float MaxHealth => maxHealth;

        /// <summary>Gets the health percentage (0-1).</summary>
        public float HealthPercent => currentHealth / Mathf.Max(0.0001f, maxHealth);

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
                float healthStress = Mathf.Clamp01(1f - HealthPercent);
                float radiationStress = Mathf.Clamp01(_radiationExposureSeconds / Mathf.Max(1f, CriticalRadiationAdvisoryThresholdSeconds));
                float toxicityStress = ResolvePoisonStatus01();
                float gasStress = Mathf.Clamp01(_gasPhysiologyStress01);
                float pressureStress = _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.PressureExposureSeverity01) : 0f;
                float thermalStress = _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.ThermalStressSeverity01) : 0f;
                return Mathf.Clamp01(Mathf.Max(
                    healthStress,
                    Mathf.Max(radiationStress, Mathf.Max(toxicityStress, Mathf.Max(gasStress, Mathf.Max(pressureStress, thermalStress))))));
            }
        }

        /// <summary>Composite panic/stress scalar from health loss and hazardous exposure.</summary>
        public float Stress01 => Stress;

        /// <summary>Gets whether the player is alive.</summary>
        public bool IsAlive => currentHealth > 0;

        /// <summary>Gets whether the player is currently invulnerable.</summary>
        public bool IsInvulnerable => ResolveExpiryRemainingSeconds(_invulnerabilityExpiresAt, ResolveUnscaledNowSeconds()) > 0f;

        /// <summary>Current cumulative radiation-fatigue exposure in seconds.</summary>
        public float RadiationExposureSeconds => _radiationExposureSeconds;

        /// <summary>Normalized cumulative radiation exposure used by visor degradation shaders.</summary>
        public float RadiationExposure => Mathf.Clamp01(_radiationExposureSeconds / Mathf.Max(1f, RadiationFatigueCriticalExposureSeconds));

        /// <summary>Permanent mutation bitmask unlocked by radiation exposure.</summary>
        public uint MutationFlags => _mutationFlags;

        /// <summary>Predator detection multiplier applied by mutation state.</summary>
        public float PredatorVisibilityScale => HasMutation(HazardMutationProfile.BioluminescentSkinBit)
            ? BioluminescentPredatorVisibilityScale
            : 1f;

        /// <summary>Runtime natural HP regeneration multiplier after food toxicity suppression.</summary>
        public float NaturalHealthRegenerationMultiplier => ResolveNaturalHealthRegenerationMultiplier(BloodToxicity01);
        /// <summary>Composite blood toxicity scalar used by medical item effects.</summary>
        public float BloodToxicity01 => Mathf.Clamp01(Mathf.Max(Mathf.Max(ResolvePoisonStatus01(), RadiationExposure), _gasPhysiologyToxicity01));

        /// <summary>Latest gas physiology stress scalar received from the physiology signal lane.</summary>
        public float GasPhysiologyStress01 => Mathf.Clamp01(_gasPhysiologyStress01);

        /// <summary>True when mutation state removes the practical need for a flashlight.</summary>
        public bool FlashlightBypassActive => HasMutation(HazardMutationProfile.BioluminescentSkinBit);

        internal void ApplyRadiationExposure(float exposureSeconds)
        {
            _radiationExposureSeconds = Mathf.Max(_radiationExposureSeconds, Mathf.Max(0f, exposureSeconds));
            ApplyRadiationExposureExact(_radiationExposureSeconds);
        }

        internal void SetRadiationExposure(float exposureSeconds)
        {
            _radiationExposureSeconds = Mathf.Max(0f, exposureSeconds);
            ApplyRadiationExposureExact(_radiationExposureSeconds);
        }

        private void ApplyRadiationExposureExact(float exposureSeconds)
        {
            float fatigueScale = ResolveRadiationFatigueScale(exposureSeconds);
            SetRuntimeMaxHealthScaleInternal(fatigueScale);
            EvaluateMutationThresholds();
            TryIssueRadiationAdvisories();
        }

        internal static float ResolveRadiationFatigueScale(float exposureSeconds)
        {
            return SomaticSurvivalMath.ResolveRadiationFatigueScale(exposureSeconds);
        }

        internal static bool ShouldActivateSurvivalGrace(
            float currentHealth,
            float maximumHealth,
            float incomingDamage,
            bool ignoreInvulnerability,
            float lockoutTimer)
        {
            if (ignoreInvulnerability ||
                lockoutTimer > 0f ||
                incomingDamage < currentHealth)
            {
                return false;
            }

            float healthPercent = currentHealth / Mathf.Max(0.0001f, maximumHealth);
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
                if (_audioLogs != null)
                    _audioLogs.NotifyAtmosphericWarningStarted(glitchDuration);
            }

            PlayerSignalEvents.TryRaiseTraumaHudSignal(new TraumaHudSignal(glitchIntensity, glitchDuration, 1f, Mathf.Clamp01(HealthPercent), true));
            ShowRadiationAdvisory(message, fallbackMessage);
        }

        private static void ShowRadiationAdvisory(char[] message, string fallbackMessage)
        {
            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                NotificationEvents.TryPushCritical(fallbackMessage.AsSpan());
                return;
            }

            try
            {
                FixedCharBuffer buffer = new FixedCharBuffer(lease.Buffer);
                buffer.Append(message);
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                    notification.ShowCritical(in buffer);
                else
                    NotificationEvents.TryPushCritical(fallbackMessage.AsSpan());
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
        }

        internal void ApplyNutritionalToxicity(float severity01, float durationSeconds)
        {
            float clampedSeverity = Mathf.Clamp01(severity01);
            float clampedDuration = Mathf.Max(0f, durationSeconds);
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
            return SomaticSurvivalMath.ResolveNaturalHealthRegenerationMultiplier(toxicitySeverity01);
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
            float minScale = MinimumRuntimeMaxHealth / Mathf.Max(MinimumRuntimeMaxHealth, _baseMaxHealth);
            float clampedScale = Mathf.Clamp(scale, minScale, 1f);
            float nextMaxHealth = Mathf.Max(MinimumRuntimeMaxHealth, _baseMaxHealth * clampedScale);
            if (Mathf.Approximately(_runtimeMaxHealthScale, clampedScale) && Mathf.Approximately(maxHealth, nextMaxHealth))
                return;

            _runtimeMaxHealthScale = clampedScale;
            maxHealth = nextMaxHealth;
            if (currentHealth <= maxHealth)
            {
                MarkCombatDamageSyncDirty();
                return;
            }

            currentHealth = maxHealth;
            MarkCombatDamageSyncDirty();
        }

        // Private state
        private double _invulnerabilityExpiresAt;
        private double _survivalGraceLockoutExpiresAt;
        private bool _isInitialized;
        private bool _registeredToSlowTickManager;
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
        private bool _hotSwapRegistered;
        private IAudioService _audioService;
        private IAudioLogRuntime _audioLogs;

        /// <summary>Initializes the health system.</summary>
        private void Awake()
        {
            if (!_isInitialized)
            {
                _baseMaxHealth = Mathf.Max(MinimumRuntimeMaxHealth, maxHealth);
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
            TryUnregisterCombatDamageTarget();
            TryUnregisterFromSlowTickManager();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterCombatDamageTarget();
            TryUnregisterFromSlowTickManager();
            TryUnregisterHotSwapListener();
        }

        /// <summary>Updates low-frequency status and physiology bridge timers.</summary>
        public void SlowTick()
        {
            TryRegisterCombatDamageTarget();
            TryFlushCombatDamageSync();
            RefreshCombatStatusMaskCache();
            UpdateGasPhysiologyBridge(HealthSlowTickDeltaSeconds);
        }

        /// <summary>Applies damage to the player.</summary>
        /// <param name="damage">Amount of damage to apply.</param>
        /// <param name="ignoreInvulnerability">Whether to ignore invulnerability frames.</param>
        /// <returns>True if damage was applied, false if blocked by invulnerability.</returns>
        public bool TakeDamage(float damage, bool ignoreInvulnerability = false)
        {
            _lastDamageTriggeredRespawnReconciliation = false;

            if (!IsAlive || (!ignoreInvulnerability && IsInvulnerable))
                return false;

            float appliedDamage = Mathf.Max(0f, damage);
            bool graceTriggered = TryActivateSurvivalGrace(appliedDamage, ignoreInvulnerability, out float clampedDamage);
            if (graceTriggered)
                appliedDamage = clampedDamage;

            currentHealth = Mathf.Max(0, currentHealth - appliedDamage);

            if (!ignoreInvulnerability && !graceTriggered)
                ExtendInvulnerability(invulnerabilityTime);

            if (currentHealth <= 0)
            {
                PublishDeath();

                if (TryApplyRespawnReconciliation(HealthRespawnDamageHash))
                {
                    _lastDamageTriggeredRespawnReconciliation = true;
                    return true;
                }

                ApplyRespawnReconciliationHealth(1f);
                _lastDamageTriggeredRespawnReconciliation = true;
                return true;
            }

            MarkCombatDamageSyncDirty();
            TryIssueVitalWarningSignal();

            return true;
        }

        public bool TakeLeviathanDamage(float damage)
        {
            float previousHealth = currentHealth;
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
            if (!IsAlive) return 0;

            float positiveAmount = Mathf.Max(0f, amount);
            if (BloodToxicity01 >= HealingReversalToxicityThreshold01 && positiveAmount > 0f)
                return 0f;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + positiveAmount);
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
            PlayerSignalEvents.TryRaiseTraumaHudSignal(new TraumaHudSignal(1f, 0.85f, 1f, Mathf.Clamp01(HealthPercent), true));
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

            if (TryApplyRespawnReconciliation(HealthRespawnDamageHash))
                return;

            ApplyRespawnReconciliationHealth(1f);
        }

        private void PublishDeath()
        {
            MarkCombatDamageSyncDirty();
        }

        /// <summary>Resets health to maximum.</summary>
        public void FullHeal()
        {
            Heal(maxHealth);
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
            NotificationEvents.TryPushCritical("CARDIAC OVERRIDE".AsSpan());
            return true;
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
            double now = Time.unscaledTime;
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
                    stress01 = Mathf.Max(stress01, Mathf.Clamp01(signal.PlayerStress01));
                    continue;
                }

                anyGasSignal = true;
                latestGasFrame = signal.Frame != 0u ? signal.Frame : currentFrame;
                stress01 = Mathf.Max(stress01, Mathf.Clamp01(Mathf.Max(signal.PlayerStress01, signal.Narcosis01)));
                toxicity01 = Mathf.Max(toxicity01, Mathf.Clamp01(signal.PlayerStress01));
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

            float decay = Mathf.Max(0f, deltaTime) * 0.5f;
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
            if (_audioService == null || survivalGraceHeartbeatClip == null)
                return;

            _audioService.PlayStatic2D(survivalGraceHeartbeatClip, survivalGraceHeartbeatVolume);
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

            float previousHealth = currentHealth;
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

            float previousHealth = currentHealth;
            float safeMaxHealth = Mathf.Max(MinimumRuntimeMaxHealth, maxHealth);
            float packetNextHealth = math.clamp(packet.NextValue, 0f, safeMaxHealth);
            currentHealth = math.min(previousHealth, packetNextHealth);
            appliedDamage = Mathf.Max(0f, previousHealth - currentHealth);

            if (currentHealth <= 0f)
            {
                PublishDeath();

                if (TryApplyRespawnReconciliation(HealthRespawnDamageHash))
                {
                    _lastDamageTriggeredRespawnReconciliation = true;
                    return true;
                }

                ApplyRespawnReconciliationHealth(1f);
                _lastDamageTriggeredRespawnReconciliation = true;
                return true;
            }

            TryIssueVitalWarningSignal();
            return true;
        }

        private void PublishDamageFeedback(in DamagePacket packet, float appliedDamage)
        {
            if (packet.SourceId == DamageSourceIds.FaunaLeviathanBite)
                TryIssueLeviathanTraumaAdvisory(appliedDamage);

            float severity01 = Mathf.Clamp01(appliedDamage * math.rcp(Mathf.Max(MinimumRuntimeMaxHealth, maxHealth)));
            PlayerSignalEvents.TryRaiseTraumaHudSignal(new TraumaHudSignal(
                Mathf.Clamp01(severity01 * 2f),
                severity01,
                1f,
                Mathf.Clamp01(HealthPercent),
                false));
        }

        private void TryIssueLeviathanTraumaAdvisory(float appliedDamage)
        {
            if (_leviathanTraumaAdvisoryIssued ||
                appliedDamage < Mathf.Max(MinimumRuntimeMaxHealth, maxHealth) * LeviathanTraumaDamageThreshold01)
            {
                return;
            }

            _leviathanTraumaAdvisoryIssued = true;
            NarrativeEvents.TryRaiseDiscoveryMade(_leviathanTraumaDiscoveryHash);
            ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                CapturePlayerRuntimePositionForPresentation(),
                1f,
                0.6f,
                1f,
                260f,
                ProceduralAudioPingKind.LeviathanRoar);
            PlayerSignalEvents.TryRaiseTraumaHudSignal(new TraumaHudSignal(1f, 0.7f, 1f, Mathf.Clamp01(HealthPercent), true));
        }

        private void EvaluateMutationThresholds()
        {
            HazardMutationProfile profile = hazardMutationProfile;
            HazardMutationProfile.MutationThreshold[] thresholds = profile != null && profile.Mutations != null && profile.Mutations.Length > 0
                ? profile.Mutations
                : s_fallbackMutationThresholds;
            for (int i = 0; i < thresholds.Length; i++)
            {
                HazardMutationProfile.MutationThreshold threshold = thresholds[i];
                if (threshold.MutationBit == 0u || _radiationExposureSeconds < threshold.ExposureThresholdSeconds)
                    continue;

                if ((_mutationFlags & threshold.MutationBit) != 0u)
                    continue;

                _mutationFlags |= threshold.MutationBit;
                ApplyMutationRuntimeEffects();
                NotificationEvents.TryPushRegisteredWarning(_mutationDetectedMessageHash);
            }
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
            if (_playerMovement != null)
            {
                float3 runtimePosition = _playerMovement.CurrentAup.ToRuntimeFloat3();
                if (!math.all(math.isfinite(runtimePosition)))
                    return _lastKnownRuntimePosition;

                Vector3 resolved = default;
                resolved.x = runtimePosition.x;
                resolved.y = runtimePosition.y;
                resolved.z = runtimePosition.z;
                _lastKnownRuntimePosition = resolved;
                return _lastKnownRuntimePosition;
            }

            return _lastKnownRuntimePosition;
        }

        internal bool TryResolveRespawnDeathAup(out double3 deathAup)
        {
            deathAup = default;

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

        internal void ApplyRespawnReconciliationHealth(float normalizedHealth)
        {
            float safeHealth01 = Mathf.Clamp01(math.isfinite(normalizedHealth) ? normalizedHealth : 1f);
            currentHealth = Mathf.Max(1f, Mathf.Max(MinimumRuntimeMaxHealth, maxHealth) * safeHealth01);
            currentHealth = Mathf.Min(currentHealth, maxHealth);
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
            if (!TryResolveRespawnDeathAup(out double3 deathAup))
                deathAup = MissingRespawnDeathAup();

            bool reconciled = PlayerDeathReconciliationBridge.RequestRespawn(deathAup, damageHash, playerHash);
            if (reconciled)
            {
                ApplyRespawnReconciliationHealth(1f);
                return true;
            }

            return false;
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
        public int SavePriority => 100; // High priority - save health early

        /// <summary>Load priority for health data.</summary>
        public int LoadPriority => 100; // Load health early

        /// <summary>Populates save data with current health state.</summary>
        /// <param name="data">The save data container to populate.</param>
        public void PopulateSaveData(SaveData data)
        {
            // Legacy component: SaveData currently has no dedicated player-health DTO.
            // Keep interface compliance compile-safe without inventing a new persistence path here.
        }

        /// <summary>Loads health state from save data.</summary>
        /// <param name="data">The save data container to load from.</param>
        public void LoadFromSaveData(SaveData data)
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            MarkCombatDamageSyncDirty();
        }

        private void CacheRegistryServicesCold()
        {
            _audioService = Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance;
            _audioLogs = GlobalRegistry.AudioLogRuntime;
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
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    _audioLogs = currentService as IAudioLogRuntime;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredToSlowTickManager = false;
                    if (currentService != null)
                    {
                        TryRegisterToSlowTickManager();
                    }
                    break;
            }
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

        private void TryRegisterCombatDamageTarget()
        {
            if (_combatDamageRegistered || !Application.isPlaying)
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
            if (!_combatDamageRegistered)
                return;

            CombatDamageRuntime.UnregisterTarget(_combatDamageTargetId, this);
            _combatDamageRegistered = false;
            _combatDamageSyncDirty = false;
            _cachedCombatStatusMask = 0UL;
            _hasCachedCombatStatusMask = false;
        }

        private void MarkCombatDamageSyncDirty()
        {
            if (!_combatDamageRegistered)
                return;

            _combatDamageSyncDirty = !CombatDamageRuntime.SyncTargetHealth(_combatDamageTargetId, currentHealth, maxHealth);
        }

        private void TryFlushCombatDamageSync()
        {
            if (!_combatDamageRegistered || !_combatDamageSyncDirty)
                return;

            _combatDamageSyncDirty = !CombatDamageRuntime.SyncTargetHealth(_combatDamageTargetId, currentHealth, maxHealth);
        }

        #endregion
    }
}
