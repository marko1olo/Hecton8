// ============================================================================
// HECTON-8 — HectonPlayerHealth.cs
// Player health system with damage, healing, and hazard effects.
// ============================================================================

using Hecton8.Core;
using Hecton8.SaveSystem;
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
        /// <summary>Maximum health points.</summary>
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        /// <summary>Current health points.</summary>
        [SerializeField, ReadOnly] private float currentHealth;

        /// <summary>Invulnerability duration after taking damage.</summary>
        [SerializeField] private float invulnerabilityTime = 1f;

        /// <summary>Event fired when health changes.</summary>
        public event System.Action<float, float> OnHealthChanged;

        /// <summary>Event fired when player dies.</summary>
        public event System.Action OnDeath;

        /// <summary>Event fired when damage is taken.</summary>
        public event System.Action<float> OnDamageTaken;

        /// <summary>Event fired when healing occurs.</summary>
        public event System.Action<float> OnHealed;

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

        // Private state
        private float _invulnerabilityTimer;
        private bool _isInitialized;
        private bool _registeredToTickManager;

        /// <summary>Initializes the health system.</summary>
        private void Awake()
        {
            if (!_isInitialized)
            {
                currentHealth = maxHealth;
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
            if (_invulnerabilityTimer <= 0f)
                return;

            _invulnerabilityTimer -= deltaTime;
            if (_invulnerabilityTimer < 0f)
                _invulnerabilityTimer = 0f;
        }

        /// <summary>Applies damage to the player.</summary>
        /// <param name="damage">Amount of damage to apply.</param>
        /// <param name="ignoreInvulnerability">Whether to ignore invulnerability frames.</param>
        /// <returns>True if damage was applied, false if blocked by invulnerability.</returns>
        public bool TakeDamage(float damage, bool ignoreInvulnerability = false)
        {
            if (!IsAlive || (!ignoreInvulnerability && IsInvulnerable))
                return false;

            float oldHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - damage);

            if (!ignoreInvulnerability)
                _invulnerabilityTimer = invulnerabilityTime;

            OnHealthChanged?.Invoke(oldHealth, currentHealth);
            OnDamageTaken?.Invoke(damage);

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

        /// <summary>Handles player death.</summary>
        private void Die()
        {
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
            if (_registeredToTickManager)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredToTickManager = true;
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
