using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;
using InteractionSignalPayload = Hecton8.Interaction.InteractionSignal;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Breakable resource outcrop that converts tool damage into debris and yield.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HarvestableOutcrop : MonoBehaviour, ICuttable, IInteractable, IInteractionSignalConsumer, ILocalizationLanguageChangedListener
    {
        private const string DefaultInteractText = "Break Rock";
        private const float MinimumToolPower = 0.05f;

        [Header("Health")]
        [SerializeField, Range(1, 10)]
        [Tooltip("Number of hits required to destroy the outcrop.")]
        private int hitsToBreak = 3;

        [SerializeField, Min(0.1f)]
        [Tooltip("Fallback damage used for direct interaction and non-signal callers.")]
        private float damagePerHit = 1f;

        [Header("Yield")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Authored density multiplier used by Yield = DrillPower * RockDensity.")]
        private float rockDensity = 2f;

        [SerializeField, Range(0, 8)]
        [Tooltip("Minimum discrete item quantity yielded when the outcrop collapses.")]
        private int minLootCount = 1;

        [SerializeField, Range(1, 16)]
        [Tooltip("Maximum discrete item quantity yielded when the outcrop collapses.")]
        private int maxLootCount = 3;

        [SerializeField]
        [Tooltip("Preferred item definitions for yield routing.")]
        private ItemData[] lootItems;

        [SerializeField]
        [Tooltip("Legacy fallback world prefabs used only to resolve authored ItemData.")]
        private GameObject[] lootPrefabs;

        [Header("Debris")]
        [SerializeField]
        [Tooltip("Pre-baked Voronoi chunk profile handed to the runtime debris manager on collapse.")]
        private OrganicDebrisProfile debrisProfile;

        [Header("Audio")]
        [SerializeField]
        [Tooltip("Sound played on each impact.")]
        private AudioClip hitSound;

        [SerializeField]
        [Tooltip("Sound played when the outcrop collapses.")]
        private AudioClip breakSound;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Impact sound volume.")]
        private float hitVolume = 0.8f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Collapse sound volume.")]
        private float breakVolume = 1f;

        [Header("VFX")]
        [SerializeField]
        [Tooltip("Pooled impact particle prefab.")]
        private GameObject hitParticlePrefab;

        [SerializeField]
        [Tooltip("Pooled collapse particle prefab.")]
        private GameObject breakParticlePrefab;

        [SerializeField]
        [Tooltip("Primary intact renderer used by simple authored setups.")]
        private Renderer targetRenderer;

        [Header("Interaction")]
        [SerializeField]
        [Tooltip("Enable direct interaction as a fallback one-hit strike.")]
        private bool allowDirectInteract;

        [SerializeField]
        [Tooltip("Interaction text shown in the HUD.")]
        private string interactText = DefaultInteractText;

        private Transform _cachedTransform;
        private Renderer[] _cachedRenderers;
        private Collider[] _cachedColliders;
        private ItemData[] _resolvedLootItems;
        private string _cachedInteractText = DefaultInteractText;
        private float _currentHealth;
        private bool _isBroken;

        /// <summary>
        /// Current health remaining before collapse.
        /// </summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>
        /// True once the outcrop has collapsed.
        /// </summary>
        public bool IsBroken => _isBroken;

        private void Awake()
        {
            _cachedTransform = transform;

            if (targetRenderer == null)
                targetRenderer = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);

            // COLD ALLOC: Renderer[n] - intact renderer cache for collapse toggles - owner: HarvestableOutcrop
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            // COLD ALLOC: Collider[n] - intact collider cache for collapse toggles - owner: HarvestableOutcrop
            _cachedColliders = GetComponentsInChildren<Collider>(true);

            RebuildLocalizedTextCache();
            RebuildLootCache();
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

        /// <inheritdoc />
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            TakeDamage(damage, hitPoint, 1f, ResolveFallbackNormal(hitPoint));
        }

        /// <inheritdoc />
        public void ApplyInteractionSignal(in Hecton8.Interaction.InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            if (signal.PowerDelivered <= 0f)
                return;

            Vector3 hitNormal = new Vector3(signal.HitNormal.x, signal.HitNormal.y, signal.HitNormal.z);
            TakeDamage(signal.PowerDelivered, runtimeHitPoint, signal.Source.Power, hitNormal);
        }

        /// <inheritdoc />
        void IInteractable.OnHoverStart()
        {
        }

        /// <inheritdoc />
        void IInteractable.OnHoverEnd()
        {
        }

        /// <inheritdoc />
        void IInteractable.Interact(Transform interactor)
        {
            if (!allowDirectInteract || _isBroken)
                return;

            TakeDamage(damagePerHit, _cachedTransform.position, 1f, Vector3.up);
        }

        /// <inheritdoc />
        string IInteractable.GetInteractText()
        {
            return allowDirectInteract ? _cachedInteractText : null;
        }

        /// <summary>
        /// Public damage entry point used by non-signal callers.
        /// </summary>
        /// <param name="damage">Damage amount to apply.</param>
        /// <param name="hitPoint">Runtime-space impact point.</param>
        public void TakeDamage(float damage, Vector3 hitPoint)
        {
            TakeDamage(damage, hitPoint, 1f, ResolveFallbackNormal(hitPoint));
        }

        /// <summary>
        /// Public one-hit helper.
        /// </summary>
        /// <param name="hitPoint">Runtime-space impact point.</param>
        public void TakeHit(Vector3 hitPoint)
        {
            TakeDamage(damagePerHit, hitPoint, 1f, ResolveFallbackNormal(hitPoint));
        }

        private void TakeDamage(float damage, Vector3 hitPoint, float toolPower, Vector3 hitNormal)
        {
            if (_isBroken || damage <= 0f)
                return;

            _currentHealth -= damage;
            PlayHitEffects(hitPoint);

            if (_currentHealth <= 0f)
                Break(hitPoint, hitNormal, toolPower);
        }

        private void Break(Vector3 hitPoint, Vector3 hitNormal, float toolPower)
        {
            if (_isBroken)
                return;

            _isBroken = true;
            PlayBreakEffects();
            DisableIntactState();
            DispatchDebris(hitPoint, hitNormal, toolPower);
            DispatchYield(toolPower, hitPoint);
        }

        private void DispatchDebris(Vector3 hitPoint, Vector3 hitNormal, float toolPower)
        {
            IDebrisService debrisService = GlobalRegistry.Debris;
            if (debrisService == null || !debrisService.IsInitialized || debrisProfile == null || !debrisProfile.IsValid)
                return;

            uint seed = unchecked((uint)EntityId.ToULong(GetEntityId())) ^ (uint)(Time.frameCount + 1);
            debrisService.SpawnBurst(
                debrisProfile,
                _cachedTransform.position,
                _cachedTransform.rotation,
                hitPoint,
                hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : ResolveFallbackNormal(hitPoint),
                Mathf.Max(MinimumToolPower, toolPower),
                seed);
        }

        private void DispatchYield(float toolPower, Vector3 dropPoint)
        {
            if (_resolvedLootItems == null || _resolvedLootItems.Length == 0)
                return;

            ItemData item = ResolveYieldItem(toolPower);
            if (item == null)
                return;

            int quantity = Mathf.CeilToInt(Mathf.Max(MinimumToolPower, toolPower) * Mathf.Max(rockDensity, 0.1f));
            quantity = Mathf.Max(Mathf.Max(1, minLootCount), quantity);
            quantity = Mathf.Min(Mathf.Max(quantity, 1), Mathf.Max(1, maxLootCount));
            if (quantity <= 0)
                return;

            PlayerInventory playerInventory = Hecton8.Core.GlobalRegistry.PlayerInventoryRuntime;
            int rejectedQuantity = quantity;
            if (playerInventory != null)
            {
                int itemHashId = LocHash.Compute(item.PersistentId);
                Transform inventoryTransform = playerInventory.transform;
                PlayerInventory.ScavengeAttemptResult result = playerInventory.ScavengeAttempt(itemHashId, quantity, inventoryTransform);
                if (result.AnyAdded)
                {
                    InteractionEvents.RaiseItemCollected(item, result.AddedQuantity, inventoryTransform);
                    bool hasInteractorPosition = inventoryTransform != null;
                    ulong interactorEntityId = hasInteractorPosition ? EntityId.ToULong(inventoryTransform.GetEntityId()) : 0ul;
                    Vector3 interactorPosition = hasInteractorPosition ? inventoryTransform.position : Vector3.zero;
                    HectonEventBus.Publish(new ItemCollectedEvent(
                        item,
                        itemHashId,
                        result.AddedQuantity,
                        interactorEntityId,
                        interactorPosition,
                        hasInteractorPosition));
                }

                if (result.IsSuccess)
                    return;

                rejectedQuantity = result.RejectedQuantity;
            }

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null && rejectedQuantity > 0)
                registry.TryRegisterDroppedItem(item, rejectedQuantity, dropPoint);
        }

        private ItemData ResolveYieldItem(float toolPower)
        {
            if (_resolvedLootItems == null || _resolvedLootItems.Length == 0)
                return null;

            uint seed = unchecked((uint)EntityId.ToULong(GetEntityId())) ^ (uint)Mathf.CeilToInt(toolPower * 100f);
            int index = (int)(seed % (uint)_resolvedLootItems.Length);
            return _resolvedLootItems[index];
        }

        private void DisableIntactState()
        {
            if (_cachedRenderers != null)
            {
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    Renderer renderer = _cachedRenderers[i];
                    if (renderer != null)
                        renderer.enabled = false;
                }
            }

            if (_cachedColliders != null)
            {
                for (int i = 0; i < _cachedColliders.Length; i++)
                {
                    Collider collider = _cachedColliders[i];
                    if (collider != null)
                        collider.enabled = false;
                }
            }

            enabled = false;
        }

        private void PlayHitEffects(Vector3 hitPoint)
        {
            if (hitSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
                audio.PlayAtPoint(hitSound, hitPoint, hitVolume);

            if (hitParticlePrefab != null)
            {
                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool != null)
                    pool.Spawn(hitParticlePrefab, hitPoint, Quaternion.identity);
            }
        }

        private void PlayBreakEffects()
        {
            Vector3 position = _cachedTransform.position;
            if (breakSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
                audio.PlayAtPoint(breakSound, position, breakVolume);

            if (breakParticlePrefab != null)
            {
                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool != null)
                    pool.Spawn(breakParticlePrefab, position, Quaternion.identity);
            }
        }

        private void ResetState()
        {
            _currentHealth = Mathf.Max(1, hitsToBreak);
            _isBroken = false;

            if (_cachedRenderers != null)
            {
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    Renderer renderer = _cachedRenderers[i];
                    if (renderer != null)
                        renderer.enabled = true;
                }
            }

            if (_cachedColliders != null)
            {
                for (int i = 0; i < _cachedColliders.Length; i++)
                {
                    Collider collider = _cachedColliders[i];
                    if (collider != null)
                        collider.enabled = true;
                }
            }
        }

        private void RebuildLootCache()
        {
            int authoredCount = lootItems != null ? lootItems.Length : 0;
            int legacyCount = lootPrefabs != null ? lootPrefabs.Length : 0;
            int capacity = Mathf.Max(1, Mathf.Max(authoredCount, legacyCount));

            // COLD ALLOC: ItemData[n] - resolved yield item cache - owner: HarvestableOutcrop
            _resolvedLootItems = new ItemData[capacity];
            int writeIndex = 0;

            if (lootItems != null)
            {
                for (int i = 0; i < lootItems.Length; i++)
                {
                    ItemData item = lootItems[i];
                    if (item == null)
                        continue;

                    _resolvedLootItems[writeIndex++] = item;
                }
            }

            if (writeIndex == 0 && lootPrefabs != null)
            {
                for (int i = 0; i < lootPrefabs.Length; i++)
                {
                    GameObject prefab = lootPrefabs[i];
                    if (prefab == null)
                        continue;

                    ItemData item = ResolveItemFromPrefab(prefab);
                    if (item == null)
                        continue;

                    _resolvedLootItems[writeIndex++] = item;
                }
            }

            if (writeIndex == _resolvedLootItems.Length)
                return;

            if (writeIndex <= 0)
            {
                _resolvedLootItems = System.Array.Empty<ItemData>();
                return;
            }

            // COLD ALLOC: ItemData[n] - compacted resolved yield cache - owner: HarvestableOutcrop
            ItemData[] compacted = new ItemData[writeIndex];
            System.Array.Copy(_resolvedLootItems, compacted, writeIndex);
            _resolvedLootItems = compacted;
        }

        private static ItemData ResolveItemFromPrefab(GameObject prefab)
        {
            if (prefab == null)
                return null;

            HectonItem hectonItem = prefab.GetComponent<HectonItem>();
            if (hectonItem != null && hectonItem.Data != null)
                return hectonItem.Data;

            PickupItem pickupItem = prefab.GetComponent<PickupItem>();
            return pickupItem != null ? pickupItem.ItemData : null;
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
            if (manager == null)
                return fallback;

            string localized = manager.Get(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }

        private Vector3 ResolveFallbackNormal(Vector3 hitPoint)
        {
            Vector3 normal = hitPoint - _cachedTransform.position;
            return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (hitsToBreak < 1)
                hitsToBreak = 1;

            if (minLootCount > maxLootCount)
                minLootCount = maxLootCount;

            RebuildLocalizedTextCache();
            RebuildLootCache();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
#endif
    }
}

