// ============================================================================
// HECTON-8 — HectonPlayerHealth.cs
// Player health system with damage, healing, and hazard effects.
// ============================================================================

using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Sirenix.OdinInspector;
using Conditional = System.Diagnostics.ConditionalAttribute;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Player health component managing HP, damage, healing, and environmental effects.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Player Health")]
    public sealed class HectonPlayerHealth : MonoBehaviour, ISaveable, ITickable, IUpdatable
    {
        private const float MinimumRuntimeMaxHealth = 1f;
        private const float SurvivalGraceEligibilityThresholdNormalized = 0.10f;
        private const float SurvivalGraceHealthFloor = 0.01f;
        private const float SurvivalGraceInvulnerabilitySeconds = 0.5f;
        private const float SurvivalGraceLockoutSeconds = 8f;
        private const float RadiationFatigueMinimumScale = 0.65f;
        private const float RadiationFatigueScalePerSecond = 0.005f;
        private const float GillsOxygenCapacityMultiplier = 1.25f;
        private const float BioluminescentPredatorVisibilityScale = 2f;
        // COLD ALLOC: MutationThreshold[2] — fallback mutation thresholds when no authored profile is assigned — owner: HectonPlayerHealth
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
        public float HealthPercent => currentHealth / maxHealth;

        /// <summary>Gets whether the player is alive.</summary>
        public bool IsAlive => currentHealth > 0;

        /// <summary>Gets whether the player is currently invulnerable.</summary>
        public bool IsInvulnerable => _invulnerabilityTimer > 0;

        /// <summary>Current cumulative radiation-fatigue exposure in seconds.</summary>
        public float RadiationExposureSeconds => _radiationExposureSeconds;

        /// <summary>Permanent mutation bitmask unlocked by radiation exposure.</summary>
        public uint MutationFlags => _mutationFlags;

        /// <summary>Predator detection multiplier applied by mutation state.</summary>
        public float PredatorVisibilityScale => HasMutation(HazardMutationProfile.BioluminescentSkinBit)
            ? BioluminescentPredatorVisibilityScale
            : 1f;

        /// <summary>True when mutation state removes the practical need for a flashlight.</summary>
        public bool FlashlightBypassActive => HasMutation(HazardMutationProfile.BioluminescentSkinBit);

        internal void ApplyRadiationExposure(float exposureSeconds)
        {
            _radiationExposureSeconds = Mathf.Max(_radiationExposureSeconds, Mathf.Max(0f, exposureSeconds));
            float fatigueScale = Mathf.Max(RadiationFatigueMinimumScale, 1f - (_radiationExposureSeconds * RadiationFatigueScalePerSecond));
            SetRuntimeMaxHealthScaleInternal(fatigueScale);
            EvaluateMutationThresholds();
        }

        internal void ClearRadiationFatigue()
        {
            _radiationExposureSeconds = 0f;
            SetRuntimeMaxHealthScaleInternal(1f);
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
                return;

            float previousHealth = currentHealth;
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(previousHealth, currentHealth);
        }

        // Private state
        private float _invulnerabilityTimer;
        private float _survivalGraceLockoutTimer;
        private bool _isInitialized;
        private bool _registeredToTickManager;
        private float _baseMaxHealth = 100f;
        private float _runtimeMaxHealthScale = 1f;
        private float _radiationExposureSeconds;
        private uint _mutationFlags;
        private HectonSurvivalSystem _survivalSystem;

        /// <summary>Initializes the health system.</summary>
        private void Awake()
        {
            if (!_isInitialized)
            {
                _baseMaxHealth = Mathf.Max(MinimumRuntimeMaxHealth, maxHealth);
                maxHealth = _baseMaxHealth;
                currentHealth = maxHealth;
                TryGetComponent(out _survivalSystem);
                ApplyMutationRuntimeEffects();
                _isInitialized = true;
            }
        }

        private void OnEnable()
        {
            TryRegisterToTickManager();
        }

        private void Start()
        {
            TryRegisterToTickManager();
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            TryUnregisterFromTickManager();
        }

        /// <summary>Updates invulnerability timer.</summary>
        public void Tick(float deltaTime)
        {
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

            if (currentHealth <= 0)
            {
                Die();
            }

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
            if (ignoreInvulnerability ||
                _survivalGraceLockoutTimer > 0f ||
                incomingDamage < currentHealth ||
                HealthPercent <= SurvivalGraceEligibilityThresholdNormalized)
            {
                return false;
            }

            clampedDamage = Mathf.Max(0f, currentHealth - SurvivalGraceHealthFloor);
            _invulnerabilityTimer = Mathf.Max(_invulnerabilityTimer, SurvivalGraceInvulnerabilitySeconds);
            _survivalGraceLockoutTimer = SurvivalGraceLockoutSeconds;
            PlaySurvivalGraceHeartbeatPulse();
            NotificationEvents.PushCritical("CARDIAC OVERRIDE // SURVIVAL GRACE");
            return true;
        }

        private void PlaySurvivalGraceHeartbeatPulse()
        {
            IAudioService audioService = GlobalRegistry.Audio;
            if (audioService == null || survivalGraceHeartbeatClip == null)
                return;

            audioService.PlayStatic2D(survivalGraceHeartbeatClip, survivalGraceHeartbeatVolume);
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
                NotificationEvents.PushWarning("MUTATION DETECTED // " + ResolveMutationDisplayName(threshold));
            }
        }

        private void ApplyMutationRuntimeEffects()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_survivalSystem == null)
                return;

            float oxygenCapacityMultiplier = HasMutation(HazardMutationProfile.GillsBit)
                ? GillsOxygenCapacityMultiplier
                : 1f;
            _survivalSystem.SetRuntimeOxygenCapacityMultiplier(oxygenCapacityMultiplier);
        }

        private static string ResolveMutationDisplayName(HazardMutationProfile.MutationThreshold threshold)
        {
            return string.IsNullOrWhiteSpace(threshold.DisplayName)
                ? "UNKNOWN ADAPTATION"
                : threshold.DisplayName.Trim();
        }

        /// <summary>Handles player death.</summary>
        private void Die()
        {
            GlobalTelemetryBus.PublishPlayerDeath(transform.position);
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

        #endregion
    }
}
