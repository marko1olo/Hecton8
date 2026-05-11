// ============================================================================
// HECTON-8 - HectonPlayerHealth.cs
// Player health system with damage, healing, and hazard effects.
// ============================================================================

using Hecton8.Core;
using Hecton8.Audio;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Sirenix.OdinInspector;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Player health component managing HP, damage, healing, and environmental effects.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Player Health")]
    public sealed class HectonPlayerHealth : MonoBehaviour, ISaveable, ITickable, IUpdatable, IDamageReceiver
    {
        private const float MinimumRuntimeMaxHealth = 1f;
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
        private const float CriticalRadiationAdvisoryThresholdSeconds = 90f;
        private const float RadiationAdvisoryStageOneExposure01 = 0.30f;
        private const float RadiationAdvisoryStageTwoExposure01 = 0.70f;
        private const float LeviathanTraumaDamageThreshold01 = 0.40f;
        private const string RadiationFatigueDiscoveryId = "radiation_fatigue_advisory_30";
        private const string RadiationCriticalDiscoveryId = "radiation_critical_advisory";
        private const string LeviathanTraumaDiscoveryId = "leviathan_trauma_voice_log";
        private const string MutationDetectedMessage = "MUTATION DETECTED";
        private const string RadiationFatigueFallbackMessage = "CRITICAL ADVISORY // RADIATION LOAD 30 PERCENT";
        private const string RadiationCriticalFallbackMessage = "CRITICAL ADVISORY // RADIATION LOAD 70 PERCENT - RAD-SHIELD REQUIRED";
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

        /// <summary>Event fired when health changes.</summary>
        public event System.Action<float, float> OnHealthChanged;

        /// <summary>Event fired when player dies.</summary>
        public event System.Action OnDeath;

        /// <summary>Event fired when damage is taken.</summary>
        public event System.Action<float> OnDamageTaken;

        /// <summary>Event fired when healing occurs.</summary>
        public event System.Action<float> OnHealed;

        /// <summary>Event fired when mutation flags change.</summary>
        public event System.Action<uint> OnMutationFlagsChanged;

        /// <summary>Gets the current health value.</summary>
        public float CurrentHealth => currentHealth;

        /// <summary>Gets the maximum health value.</summary>
        public float MaxHealth => maxHealth;

        /// <summary>Gets the health percentage (0-1).</summary>
        public float HealthPercent => currentHealth / Mathf.Max(0.0001f, maxHealth);

        /// <summary>Composite panic/stress scalar from health loss and hazardous exposure.</summary>
        public float Stress
        {
            get
            {
                float healthStress = Mathf.Clamp01(1f - HealthPercent);
                float radiationStress = Mathf.Clamp01(_radiationExposureSeconds / Mathf.Max(1f, CriticalRadiationAdvisoryThresholdSeconds));
                float toxicityStress = Mathf.Clamp01(_nutritionalToxicitySeverity01);
                float pressureStress = _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.PressureExposureSeverity01) : 0f;
                float thermalStress = _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.ThermalStressSeverity01) : 0f;
                return Mathf.Clamp01(Mathf.Max(
                    healthStress,
                    Mathf.Max(radiationStress, Mathf.Max(toxicityStress, Mathf.Max(pressureStress, thermalStress)))));
            }
        }

        /// <summary>Composite panic/stress scalar from health loss and hazardous exposure.</summary>
        public float Stress01 => Stress;

        /// <summary>Gets whether the player is alive.</summary>
        public bool IsAlive => currentHealth > 0;

        /// <summary>Gets whether the player is currently invulnerable.</summary>
        public bool IsInvulnerable => _invulnerabilityTimer > 0;

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
        public float NaturalHealthRegenerationMultiplier => ResolveNaturalHealthRegenerationMultiplier(_nutritionalToxicitySeverity01);

        /// <summary>True when mutation state removes the practical need for a flashlight.</summary>
        public bool FlashlightBypassActive => HasMutation(HazardMutationProfile.BioluminescentSkinBit);

        internal void ApplyRadiationExposure(float exposureSeconds)
        {
            _radiationExposureSeconds = Mathf.Max(_radiationExposureSeconds, Mathf.Max(0f, exposureSeconds));
            float fatigueScale = ResolveRadiationFatigueScale(_radiationExposureSeconds);
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
                RadiationFatigueDiscoveryId,
                0.72f,
                0.22f,
                s_radiationFatigueMessage,
                RadiationFatigueFallbackMessage,
                false);

            TryIssueRadiationAdvisory(
                exposure01,
                RadiationAdvisoryStageTwoExposure01,
                ref _radiationCriticalAdvisoryIssued,
                RadiationCriticalDiscoveryId,
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
            string discoveryId,
            float glitchIntensity,
            float glitchDuration,
            char[] message,
            string fallbackMessage,
            bool blocksNarrativeQueue)
        {
            if (issued || exposure01 < threshold01)
                return;

            issued = true;

            NarrativeEvents.RaiseDiscoveryMade(discoveryId);

            if (blocksNarrativeQueue)
            {
                AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
                if (audioLogs != null)
                    audioLogs.NotifyAtmosphericWarningStarted(glitchDuration);
            }

            PlayerSignalEvents.RaiseTraumaHudSignal(new TraumaHudSignal(glitchIntensity, glitchDuration, 1f, Mathf.Clamp01(HealthPercent), true));
            ShowRadiationAdvisory(message, fallbackMessage);
        }

        private static void ShowRadiationAdvisory(char[] message, string fallbackMessage)
        {
            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                NotificationEvents.PushCritical(fallbackMessage);
                return;
            }

            try
            {
                FixedCharBuffer buffer = new FixedCharBuffer(lease.Buffer);
                buffer.Append(message);
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                    notification.ShowCritical(in buffer);
                else
                    NotificationEvents.PushCritical(fallbackMessage);
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

            _nutritionalToxicitySeverity01 = Mathf.Max(_nutritionalToxicitySeverity01, clampedSeverity);
            _nutritionalToxicityTimer = Mathf.Max(_nutritionalToxicityTimer, clampedDuration);
        }

        internal static float ResolveNaturalHealthRegenerationMultiplier(float toxicitySeverity01)
        {
            return SomaticSurvivalMath.ResolveNaturalHealthRegenerationMultiplier(toxicitySeverity01);
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

            float previousHealth = currentHealth;
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(previousHealth, currentHealth);
            MarkCombatDamageSyncDirty();
        }

        // Private state
        private float _invulnerabilityTimer;
        private float _survivalGraceLockoutTimer;
        private bool _isInitialized;
        private bool _registeredToTickManager;
        private float _baseMaxHealth = 100f;
        private float _runtimeMaxHealthScale = 1f;
        private float _radiationExposureSeconds;
        private float _nutritionalToxicityTimer;
        private float _nutritionalToxicitySeverity01;
        private uint _mutationFlags;
        private bool _radiationFatigueAdvisoryIssued;
        private bool _radiationCriticalAdvisoryIssued;
        private bool _leviathanTraumaAdvisoryIssued;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private Vector3 _lastKnownRuntimePosition;
        private int _combatDamageTargetId;
        private bool _combatDamageRegistered;
        private bool _combatDamageSyncDirty;

        /// <summary>Initializes the health system.</summary>
        private void Awake()
        {
            if (!_isInitialized)
            {
                _baseMaxHealth = Mathf.Max(MinimumRuntimeMaxHealth, maxHealth);
                maxHealth = _baseMaxHealth;
                currentHealth = maxHealth;
                NotificationEvents.RegisterMessage(MutationDetectedMessage);
                TryGetComponent(out _survivalSystem);
                TryGetComponent(out _playerMovement);
                _combatDamageTargetId = CombatDamageRuntime.ResolveTargetId(gameObject);
                ApplyMutationRuntimeEffects();
                _isInitialized = true;
            }
        }

        private void OnEnable()
        {
            TryRegisterToTickManager();
            TryRegisterCombatDamageTarget();
        }

        private void Start()
        {
            TryRegisterToTickManager();
            TryRegisterCombatDamageTarget();
        }

        private void OnDisable()
        {
            TryUnregisterCombatDamageTarget();
            TryUnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            TryUnregisterCombatDamageTarget();
            TryUnregisterFromTickManager();
        }

        /// <summary>Updates invulnerability timer.</summary>
        public void Tick(float deltaTime)
        {
            TryRegisterCombatDamageTarget();
            TryFlushCombatDamageSync();

            if (_survivalGraceLockoutTimer > 0f)
            {
                _survivalGraceLockoutTimer -= deltaTime;
                if (_survivalGraceLockoutTimer < 0f)
                    _survivalGraceLockoutTimer = 0f;
            }

            if (_invulnerabilityTimer > 0f)
            {
                _invulnerabilityTimer -= deltaTime;
                if (_invulnerabilityTimer < 0f)
                    _invulnerabilityTimer = 0f;
            }

            UpdateNutritionalToxicity(deltaTime);
        }

        /// <summary>Applies damage to the player.</summary>
        /// <param name="damage">Amount of damage to apply.</param>
        /// <param name="ignoreInvulnerability">Whether to ignore invulnerability frames.</param>
        /// <returns>True if damage was applied, false if blocked by invulnerability.</returns>
        public bool TakeDamage(float damage, bool ignoreInvulnerability = false)
        {
            if (!IsAlive || (!ignoreInvulnerability && IsInvulnerable))
                return false;

            float appliedDamage = Mathf.Max(0f, damage);
            bool graceTriggered = TryActivateSurvivalGrace(appliedDamage, ignoreInvulnerability, out float clampedDamage);
            if (graceTriggered)
                appliedDamage = clampedDamage;

            float oldHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - appliedDamage);

            if (!ignoreInvulnerability && !graceTriggered && _invulnerabilityTimer < invulnerabilityTime)
                _invulnerabilityTimer = invulnerabilityTime;

            OnHealthChanged?.Invoke(oldHealth, currentHealth);
            OnDamageTaken?.Invoke(appliedDamage);
            MarkCombatDamageSyncDirty();

            if (currentHealth <= 0)
            {
                Die();
            }

            return true;
        }

        public bool TakeLeviathanDamage(float damage)
        {
            float previousHealth = currentHealth;
            bool applied = TakeDamage(damage);
            if (!applied)
                return false;

            TryIssueLeviathanTraumaAdvisory(previousHealth - currentHealth);
            return true;
        }

        /// <summary>Heals the player.</summary>
        /// <param name="amount">Amount of healing to apply.</param>
        /// <returns>Actual amount healed.</returns>
        public float Heal(float amount)
        {
            if (!IsAlive) return 0;

            float oldHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            float actualHeal = currentHealth - oldHealth;

            if (actualHeal > 0)
            {
                OnHealthChanged?.Invoke(oldHealth, currentHealth);
                OnHealed?.Invoke(actualHeal);
                MarkCombatDamageSyncDirty();
            }

            return actualHeal;
        }

        /// <summary>Kills the player instantly.</summary>
        public void Kill()
        {
            if (!IsAlive) return;

            float oldHealth = currentHealth;
            currentHealth = 0;

            OnHealthChanged?.Invoke(oldHealth, currentHealth);
            MarkCombatDamageSyncDirty();
            Die();
        }

        /// <summary>Resets health to maximum.</summary>
        public void FullHeal()
        {
            Heal(maxHealth);
        }

        private bool TryActivateSurvivalGrace(float incomingDamage, bool ignoreInvulnerability, out float clampedDamage)
        {
            clampedDamage = incomingDamage;
            if (!ShouldActivateSurvivalGrace(
                    currentHealth,
                    maxHealth,
                    incomingDamage,
                    ignoreInvulnerability,
                    _survivalGraceLockoutTimer))
            {
                return false;
            }

            clampedDamage = Mathf.Max(0f, currentHealth - SurvivalGraceHealthFloor);
            _invulnerabilityTimer = Mathf.Max(_invulnerabilityTimer, SurvivalGraceInvulnerabilitySeconds);
            _survivalGraceLockoutTimer = SurvivalGraceLockoutSeconds;
            PlaySurvivalGraceHeartbeatPulse();
            NotificationEvents.PushCritical("CARDIAC OVERRIDE");
            return true;
        }

        private void UpdateNutritionalToxicity(float deltaTime)
        {
            if (_nutritionalToxicityTimer <= 0f)
                return;

            _nutritionalToxicityTimer = Mathf.Max(0f, _nutritionalToxicityTimer - Mathf.Max(0f, deltaTime));
            if (_nutritionalToxicityTimer > 0f)
                return;

            _nutritionalToxicitySeverity01 = 0f;
        }

        private void PlaySurvivalGraceHeartbeatPulse()
        {
            IAudioService audioService = GlobalRegistry.Audio;
            if (audioService == null || survivalGraceHeartbeatClip == null)
                return;

            audioService.PlayStatic2D(survivalGraceHeartbeatClip, survivalGraceHeartbeatVolume);
        }

        public void ReceiveDamage(in DamagePacket packet)
        {
            if (packet.Channel != DamageChannel.Integrity || packet.Magnitude <= 0f)
                return;

            float previousHealth = currentHealth;
            bool applied = TakeDamage(packet.Magnitude);
            if (!applied)
            {
                MarkCombatDamageSyncDirty();
                return;
            }

            float appliedDamage = Mathf.Max(0f, previousHealth - currentHealth);
            if (packet.SourceId == DamageSourceIds.FaunaLeviathanBite)
                TryIssueLeviathanTraumaAdvisory(appliedDamage);

            float severity01 = Mathf.Clamp01(appliedDamage * math.rcp(Mathf.Max(MinimumRuntimeMaxHealth, maxHealth)));
            PlayerSignalEvents.RaiseTraumaHudSignal(new TraumaHudSignal(
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
            NarrativeEvents.RaiseDiscoveryMade(LeviathanTraumaDiscoveryId);
            ProceduralAudioEvents.RaiseAudioPingTriggered(
                ResolvePlayerRuntimePosition(),
                1f,
                0.6f,
                1f,
                260f,
                ProceduralAudioPingKind.LeviathanRoar);
            PlayerSignalEvents.RaiseTraumaHudSignal(new TraumaHudSignal(1f, 0.7f, 1f, Mathf.Clamp01(HealthPercent), true));
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
                OnMutationFlagsChanged?.Invoke(_mutationFlags);
                NotificationEvents.PushRegisteredWarning(_mutationDetectedMessageHash);
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

        private Vector3 ResolvePlayerRuntimePosition()
        {
            if (_playerMovement == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _playerMovement = playerContext.PlayerMovement;
            }

            if (_playerMovement != null)
            {
                float3 runtimePosition = _playerMovement.CurrentAup.ToRuntimeFloat3();
                _lastKnownRuntimePosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                return _lastKnownRuntimePosition;
            }

            return _lastKnownRuntimePosition;
        }

        /// <summary>Handles player death.</summary>
        private void Die()
        {
            GlobalTelemetryBus.PublishPlayerDeath(ResolvePlayerRuntimePosition());
            OnDeath?.Invoke();
            // TODO: Trigger death sequence, respawn, etc.
            LogPlayerDied();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlayerDied()
        {
            Debug.Log("Player died");
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

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredToTickManager = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredToTickManager = false;
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
