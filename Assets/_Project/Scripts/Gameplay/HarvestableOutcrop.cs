// ============================================================================
// HECTON-8 — HarvestableOutcrop.cs
// Breakable rock node (Subnautica-style Limestone/Outcrop).
//
// ARCHITECTURE:
//   • Standalone prop — no dependencies on managers beyond SpatialAudioManager.
//   • Hit-based health system (e.g., 3 hits to break).
//   • Implements ICuttable for Knife/LaserCutter integration.
//   • Optional IInteractable for direct pickup (configurable).
//
// ZERO GC:
//   • No Update() — event-driven via TakeDamage/ApplyCutDamage.
//   • Cached Transform, AudioSource, ParticleSystem.
//   • Pre-cached interaction text string.
//   • MaterialPropertyBlock for hit flash VFX.
//
// USAGE:
//   1. Place on rock mesh GameObject.
//   2. Assign lootPrefab (ItemData or raw prefab).
//   3. Assign hit/break audio clips and particle effects.
//   4. Configure health (default 3 hits).
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Breakable resource outcrop (Limestone, Sandstone, etc.).
    /// Takes hits from tools, plays VFX/audio, spawns loot on destruction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HarvestableOutcrop : MonoBehaviour, ICuttable, IInteractable
    {
        private const string DefaultInteractText = "Break Rock";

        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        private static readonly int _FlashColorID = Shader.PropertyToID("_FlashColor");
        private static readonly int _FlashIntensityID = Shader.PropertyToID("_FlashIntensity");

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — HEALTH
        // ══════════════════════════════════════════════════════════

        [Header("── Health ────────────────────────────────────")]
        [Tooltip("Number of hits required to break the outcrop.")]
        [SerializeField, Range(1, 10)] private int hitsToBreak = 3;

        [Tooltip("Damage per hit (for tools that deal variable damage).")]
        [SerializeField] private float damagePerHit = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LOOT
        // ══════════════════════════════════════════════════════════

        [Header("── Loot ──────────────────────────────────────")]
        [Tooltip("Possible loot prefabs. One is randomly selected on break.")]
        [SerializeField] private GameObject[] lootPrefabs;

        [Tooltip("Minimum loot items to spawn.")]
        [SerializeField, Range(0, 5)] private int minLootCount = 1;

        [Tooltip("Maximum loot items to spawn.")]
        [SerializeField, Range(1, 10)] private int maxLootCount = 2;

        [Tooltip("Radius around outcrop to scatter loot.")]
        [SerializeField] private float lootScatterRadius = 0.5f;

        [Tooltip("Upward force applied to loot on spawn.")]
        [SerializeField] private float lootUpwardForce = 2f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Sound played on each hit.")]
        [SerializeField] private AudioClip hitSound;

        [Tooltip("Sound played when outcrop breaks.")]
        [SerializeField] private AudioClip breakSound;

        [Tooltip("Volume for hit sound.")]
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.8f;

        [Tooltip("Volume for break sound.")]
        [SerializeField, Range(0f, 1f)] private float breakVolume = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VFX
        // ══════════════════════════════════════════════════════════

        [Header("── VFX ────────────────────────────────────────")]
        [Tooltip("Particle prefab for hit effect (rock dust). Spawned via ObjectPoolManager.")]
        [SerializeField] private GameObject hitParticlePrefab;

        [Tooltip("Particle prefab for break effect. Spawned via ObjectPoolManager.")]
        [SerializeField] private GameObject breakParticlePrefab;

        [Tooltip("Renderer for hit flash effect.")]
        [SerializeField] private Renderer targetRenderer;

        [Tooltip("Duration of hit flash in seconds.")]
        [SerializeField] private float flashDuration = 0.1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — INTERACTION
        // ══════════════════════════════════════════════════════════

        [Header("── Interaction ───────────────────────────────")]
        [Tooltip("Enable IInteractable for direct pickup (no tool required).")]
        [SerializeField] private bool allowDirectInteract;

        [Tooltip("Interaction text shown in HUD.")]
        [SerializeField] private string interactText = DefaultInteractText;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private float _currentHealth;
        private bool _isBroken;
        private bool _flashActive;
        private float _flashTimer;

        /// <summary>
        /// Cached MaterialPropertyBlock for hit flash VFX.
        /// Allocated once in Awake — zero GC in hot path.
        /// </summary>
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — hit flash VFX — owner: HarvestableOutcrop

        /// <summary>
        /// Pre-cached interaction text to avoid runtime allocations.
        /// </summary>
        private string _cachedInteractText;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Current health (hits remaining).</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>Is the outcrop broken?</summary>
        public bool IsBroken => _isBroken;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;

            // COLD ALLOC: MaterialPropertyBlock — hit flash VFX
            _mpb = new MaterialPropertyBlock();

            // Auto-find renderer if not assigned
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            RebuildLocalizedTextCache();

            ResetState();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            RebuildLocalizedTextCache();
            ResetState();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        // ══════════════════════════════════════════════════════════
        //  ICuttable — KNIFE / LASER CUTTER INTEGRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by LaserCutter or KnifeTool when hitting this object.
        /// Applies damage and triggers hit VFX/audio.
        /// </summary>
        /// <param name="damage">Damage amount (typically damagePerSecond × deltaTime).</param>
        /// <param name="hitPoint">World position of the hit.</param>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            TakeDamage(damage, hitPoint);
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable — DIRECT INTERACTION (OPTIONAL)
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
            if (!allowDirectInteract) return;
            if (_isBroken) return;

            // Apply one hit worth of damage
            TakeDamage(damagePerHit, _transform.position);
        }

        string IInteractable.GetInteractText()
        {
            return allowDirectInteract ? _cachedInteractText : null;
        }

        private void RebuildLocalizedTextCache()
        {
            if (!string.IsNullOrWhiteSpace(interactText) &&
                !string.Equals(interactText, DefaultInteractText, System.StringComparison.Ordinal))
            {
                _cachedInteractText = interactText;
                return;
            }

            _cachedInteractText = ResolveLocalized(LocalizationKeys.INTERACT_BREAK_ROCK, DefaultInteractText);
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager == null)
                return fallback;

            string localized = manager.Get(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }

        // ══════════════════════════════════════════════════════════
        //  DAMAGE SYSTEM
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Public method for tools to deal damage.
        /// </summary>
        /// <param name="damage">Damage amount.</param>
        /// <param name="hitPoint">World position of the hit for VFX.</param>
        public void TakeDamage(float damage, Vector3 hitPoint)
        {
            if (_isBroken) return;
            if (damage <= 0f) return;

            _currentHealth -= damage;

            // Play hit effects
            PlayHitEffects(hitPoint);

            // Check for break
            if (_currentHealth <= 0f)
            {
                Break();
            }
        }

        /// <summary>
        /// Overload for simple hit-based damage (uses default damagePerHit).
        /// </summary>
        public void TakeHit(Vector3 hitPoint)
        {
            TakeDamage(damagePerHit, hitPoint);
        }

        // ══════════════════════════════════════════════════════════
        //  VFX / AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlayHitEffects(Vector3 hitPoint)
        {
            // ── Audio ──
            if (hitSound != null && SpatialAudioManager.TryGetInstance(out var audio))
            {
                audio.PlayAtPoint(hitSound, hitPoint, hitVolume);
            }

            // ── Particles (pooled) ──
            if (hitParticlePrefab != null)
            {
                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool != null)
                {
                    GameObject pooled = pool.Spawn(hitParticlePrefab, hitPoint, Quaternion.identity);
                    // ParticleSystem will auto-despawn if configured with StopAction.Destroy
                }
            }

            // ── Hit Flash (MaterialPropertyBlock) ──
            if (targetRenderer != null && !_flashActive)
            {
                _flashActive = true;
                _flashTimer = flashDuration;

                targetRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(_FlashColorID, new Color(1f, 0.9f, 0.7f, 1f)); // Warm white
                _mpb.SetFloat(_FlashIntensityID, 0.5f);
                targetRenderer.SetPropertyBlock(_mpb);
            }
        }

        private void PlayBreakEffects()
        {
            Vector3 pos = _transform.position;

            // ── Audio ──
            if (breakSound != null && SpatialAudioManager.TryGetInstance(out var audio))
            {
                audio.PlayAtPoint(breakSound, pos, breakVolume);
            }

            // ── Particles (pooled) ──
            if (breakParticlePrefab != null)
            {
                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool != null)
                {
                    GameObject pooled = pool.Spawn(breakParticlePrefab, pos, Quaternion.identity);
                    // ParticleSystem will auto-despawn if configured with StopAction.Destroy
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  BREAK / LOOT
        // ══════════════════════════════════════════════════════════

        private void Break()
        {
            if (_isBroken) return;
            _isBroken = true;

            // Play break effects
            PlayBreakEffects();

            // Spawn loot
            SpawnLoot();

            // Disable mesh and collider
            DisableVisuals();
        }

        private void SpawnLoot()
        {
            if (lootPrefabs == null || lootPrefabs.Length == 0) return;

            int count = Random.Range(minLootCount, maxLootCount + 1);
            Vector3 origin = _transform.position;

            for (int i = 0; i < count; i++)
            {
                // Random loot selection
                GameObject prefab = lootPrefabs[Random.Range(0, lootPrefabs.Length)];
                if (prefab == null) continue;

                // Random position within scatter radius
                Vector2 offset2D = Random.insideUnitCircle * lootScatterRadius;
                Vector3 spawnPos = origin + new Vector3(offset2D.x, 0.1f, offset2D.y);

                // Spawn via ObjectPoolManager if available, else Instantiate
                GameObject loot;
                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool != null)
                {
                    loot = pool.Spawn(prefab, spawnPos, Random.rotation);
                }
                else
                {
                    loot = Instantiate(prefab, spawnPos, Random.rotation);
                }

                if (loot == null) continue;

                // Apply upward force if has Rigidbody
                if (loot.TryGetComponent(out Rigidbody rb))
                {
                    rb.AddForce(Vector3.up * lootUpwardForce, ForceMode.Impulse);
                }
            }
        }

        private void DisableVisuals()
        {
            // Disable renderer
            if (targetRenderer != null)
            {
                targetRenderer.enabled = false;
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
        //  STATE RESET
        // ══════════════════════════════════════════════════════════

        private void ResetState()
        {
            _currentHealth = hitsToBreak;
            _isBroken = false;
            _flashActive = false;
            _flashTimer = 0f;

            // Reset flash on material
            if (targetRenderer != null && _mpb != null)
            {
                targetRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_FlashIntensityID, 0f);
                targetRenderer.SetPropertyBlock(_mpb);
            }

            // Re-enable visuals
            if (targetRenderer != null)
            {
                targetRenderer.enabled = true;
            }

            if (TryGetComponent(out Collider col))
            {
                col.enabled = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (minLootCount > maxLootCount)
            {
                minLootCount = maxLootCount;
            }

            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw loot scatter radius
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, lootScatterRadius);
        }
#endif
    }
}
