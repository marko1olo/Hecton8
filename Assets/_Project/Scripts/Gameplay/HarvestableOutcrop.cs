using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.World;
using System;
using Unity.Mathematics;
using UnityEngine;
using InteractionSignalPayload = Hecton8.Interaction.InteractionSignal;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Breakable resource outcrop that converts tool damage into debris and yield.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HarvestableOutcrop : MonoBehaviour, ICuttable, IInteractable, IInteractableTextProvider, IInteractionSignalConsumer, ILocalizationLanguageChangedListener, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const string DefaultInteractText = "Break Rock";
        private const float MinimumToolPower = 0.05f;
        private const uint OutcropShardSpeciesHash = 0xC0DEFACEu;

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
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;
        private float _currentHealth;
        private bool _isBroken;
        private IPlayerInventoryService _playerInventoryService;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private IAudioService _audioService;
        private IObjectPoolService _objectPool;
        private ILocalizationTextReadModel _localizationManager;
        private bool _hotSwapListenerRegistered;
        private bool _lateFrameRegistered;
        private bool _pendingHitAudio;
        private bool _pendingBreakAudio;
        private bool _pendingHitParticle;
        private bool _pendingBreakParticle;
        private bool _pendingRendererStateDirty;
        private bool _pendingRendererEnabled;
        private bool _pendingDisableComponent;
        private bool _pendingDebrisSignal;
        private Vector3 _pendingHitPosition;
        private Vector3 _pendingBreakPosition;
        private DebrisSpawnSignal _pendingDebris;

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

            CacheRegistryServicesCold();
            RebuildLocalizedTextCache();
            RebuildLootCache();
            ResetState();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildLocalizedTextCache();
            ResetState();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            StopLateFrameTicking();
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
            return allowDirectInteract ? ResolveLegacyConfigured(interactText, DefaultInteractText) : null;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            ReadOnlySpan<char> source = allowDirectInteract
                ? _cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength)
                : ReadOnlySpan<char>.Empty;
            return InteractableTextCopy.TryCopy(source, destination, out length);
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
            QueueBreakEffects();
            QueueIntactRendererState(false);
            DisableIntactColliders();
            DispatchDebris(hitPoint, hitNormal, toolPower);
            DispatchYield(toolPower, hitPoint);
            QueueComponentDisable();
        }

        private void DispatchDebris(Vector3 hitPoint, Vector3 hitNormal, float toolPower)
        {
            if (!TryNormalize(hitNormal, out Vector3 normalizedHitNormal))
                normalizedHitNormal = ResolveFallbackNormal(hitPoint);

            float power01 = math.saturate(math.max(MinimumToolPower, toolPower));
            uint seed = unchecked((uint)EntityId.ToULong(GetEntityId())) ^ (uint)(SystemDispatcher.CurrentFrameIndex + 1);
            if (!TryResolveAupFromRuntimeOrigin(hitPoint + normalizedHitNormal * 0.04f, out AbsoluteUniversePosition positionAup))
                return;

            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = positionAup,
                SpeciesHash = OutcropShardSpeciesHash,
                SourceEntityId = seed == 0u ? 1u : seed,
                Intensity01 = power01,
                DebrisKind = DebrisSpawnSignal.DebrisKindRockShard,
                Flags = DebrisSpawnSignal.FlagComputeShard,
                Quantity = (ushort)math.clamp(8 + (int)(power01 * 48f), 8, 64)
            };
            _pendingDebris = signal;
            _pendingDebrisSignal = true;
            StartLateFrameTicking();
        }

        private void DispatchYield(float toolPower, Vector3 dropPoint)
        {
            if (_resolvedLootItems == null || _resolvedLootItems.Length == 0)
                return;

            ItemData item = ResolveYieldItem(toolPower);
            if (item == null)
                return;

            int quantity = (int)math.ceil(math.max(MinimumToolPower, toolPower) * math.max(rockDensity, 0.1f));
            quantity = math.max(math.max(1, minLootCount), quantity);
            quantity = math.min(math.max(quantity, 1), math.max(1, maxLootCount));
            if (quantity <= 0)
                return;

            IPlayerInventoryService playerInventoryService = _playerInventoryService;
            PlayerInventory playerInventory = playerInventoryService != null ? playerInventoryService.Inventory : null;
            int rejectedQuantity = quantity;
            if (playerInventory != null)
            {
                int itemHashId = LocHash.Compute(item.PersistentId);
                Transform inventoryTransform = playerInventory.transform;
                PlayerInventory.ScavengeAttemptResult result = playerInventory.ScavengeAttempt(itemHashId, quantity, inventoryTransform);
                if (result.AnyAdded)
                {
                    InteractionEvents.TryRaiseItemCollected(item, result.AddedQuantity, inventoryTransform);
                    bool hasInteractorPosition = inventoryTransform != null;
                    ulong interactorEntityId = hasInteractorPosition ? EntityId.ToULong(inventoryTransform.GetEntityId()) : 0ul;
                    Vector3 interactorPosition = hasInteractorPosition ? inventoryTransform.position : Vector3.zero;
                    PublishItemAcquiredSignal(itemHashId, result.AddedQuantity, hasInteractorPosition ? interactorPosition : dropPoint);
                    ItemLifecycleSignalRoute.TryPublishCollected(
                        item,
                        result.AddedQuantity,
                        interactorEntityId,
                        interactorPosition,
                        hasInteractorPosition);
                }

                if (result.IsSuccess)
                    return;

                rejectedQuantity = result.RejectedQuantity;
            }

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null && rejectedQuantity > 0)
                registry.TryRegisterDroppedItem(item, rejectedQuantity, dropPoint);
        }

        private ItemData ResolveYieldItem(float toolPower)
        {
            if (_resolvedLootItems == null || _resolvedLootItems.Length == 0)
                return null;

            uint seed = unchecked((uint)EntityId.ToULong(GetEntityId())) ^ (uint)(int)math.ceil(toolPower * 100f);
            int index = (int)(seed % (uint)_resolvedLootItems.Length);
            return _resolvedLootItems[index];
        }

        private void DisableIntactState()
        {
            QueueIntactRendererState(false);
            DisableIntactColliders();
            QueueComponentDisable();
        }

        private void DisableIntactColliders()
        {
            if (_cachedColliders == null)
                return;

            for (int i = 0; i < _cachedColliders.Length; i++)
            {
                Collider collider = _cachedColliders[i];
                if (collider != null)
                    collider.enabled = false;
            }
        }

        private void QueueIntactRendererState(bool enabledState)
        {
            _pendingRendererEnabled = enabledState;
            _pendingRendererStateDirty = true;
            StartLateFrameTicking();
        }

        private void ApplyIntactRendererState(bool enabledState)
        {
            if (_cachedRenderers != null)
            {
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    Renderer renderer = _cachedRenderers[i];
                    if (renderer != null)
                        renderer.enabled = enabledState;
                }
            }
        }

        private void PlayHitEffects(Vector3 hitPoint)
        {
            _pendingHitPosition = hitPoint;
            _pendingHitAudio = hitSound != null;
            _pendingHitParticle = hitParticlePrefab != null;
            StartLateFrameTicking();
        }

        private void QueueBreakEffects()
        {
            Vector3 position = _cachedTransform.position;
            _pendingBreakPosition = position;
            _pendingBreakAudio = breakSound != null;
            _pendingBreakParticle = breakParticlePrefab != null;
            StartLateFrameTicking();
        }

        public void LateFrameTick()
        {
            if (_pendingRendererStateDirty)
            {
                _pendingRendererStateDirty = false;
                ApplyIntactRendererState(_pendingRendererEnabled);
            }

            IAudioService audio = _audioService;
            IObjectPoolService pool = _objectPool;

            if (_pendingHitAudio)
            {
                _pendingHitAudio = false;
                if (hitSound != null && audio != null)
                    audio.PlayAtPoint(hitSound, _pendingHitPosition, hitVolume);
            }

            if (_pendingHitParticle)
            {
                _pendingHitParticle = false;
                if (hitParticlePrefab != null && pool != null)
                    pool.Spawn(hitParticlePrefab, _pendingHitPosition, Quaternion.identity);
            }

            if (_pendingBreakAudio)
            {
                _pendingBreakAudio = false;
                if (breakSound != null && audio != null)
                    audio.PlayAtPoint(breakSound, _pendingBreakPosition, breakVolume);
            }

            if (_pendingBreakParticle)
            {
                _pendingBreakParticle = false;
                if (breakParticlePrefab != null && pool != null)
                    pool.Spawn(breakParticlePrefab, _pendingBreakPosition, Quaternion.identity);
            }

            if (_pendingDebrisSignal)
            {
                _pendingDebrisSignal = false;
                SignalBus<DebrisSpawnSignal>.TryPush(in _pendingDebris);
                _pendingDebris = default;
            }

            if (_pendingDisableComponent)
            {
                _pendingDisableComponent = false;
                enabled = false;
            }

            StopLateFrameTicking();
        }

        private void QueueComponentDisable()
        {
            _pendingDisableComponent = true;
            StartLateFrameTicking();
        }

        private void StartLateFrameTicking()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void StopLateFrameTicking()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
        }

        private void ResetState()
        {
            _currentHealth = math.max(1, hitsToBreak);
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
            int capacity = math.max(1, math.max(authoredCount, legacyCount));

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
            _cachedInteractTextLength = InteractableTextCopy.CopyConfiguredOrLocalizedTruncated(
                interactText,
                DefaultInteractText,
                LocalizationKeys.INTERACT_BREAK_ROCK,
                _localizationManager,
                _cachedInteractTextBuffer);
        }

        private static string ResolveLegacyConfigured(string configuredText, string defaultText)
        {
            return !string.IsNullOrWhiteSpace(configuredText) &&
                   !string.Equals(configuredText, defaultText, StringComparison.Ordinal)
                ? configuredText
                : defaultText;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventoryService = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    _objectPool = currentService as IObjectPoolService;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    RebuildLocalizedTextCache();
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _playerInventoryService = GlobalRegistry.PlayerInventory;
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _audioService = GlobalRegistry.Audio;
            _objectPool = GlobalRegistry.ObjectPoolService;
            _localizationManager = GlobalRegistry.LocalizationText;
        }

        private static void PublishItemAcquiredSignal(int itemHashId, int quantity, Vector3 runtimePosition)
        {
            if (itemHashId == 0 || quantity <= 0)
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                return;

            ItemAcquiredSignal signal = new ItemAcquiredSignal
            {
                PositionAup = positionAup,
                ItemHash = unchecked((uint)itemHashId),
                OreHash = unchecked((uint)itemHashId),
                Quantity = (ushort)math.min(quantity, (int)ushort.MaxValue),
                SourceKind = ItemAcquiredSignalSourceKinds.HarvestableOutcrop,
                Flags = 0,
                Frame = unchecked((uint)SystemDispatcher.CurrentFrameIndex)
            };
            SignalBus<ItemAcquiredSignal>.TryPush(in signal);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private Vector3 ResolveFallbackNormal(Vector3 hitPoint)
        {
            Vector3 normal = hitPoint - _cachedTransform.position;
            return TryNormalize(normal, out Vector3 normalized)
                ? normalized
                : Vector3.up;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.0001f)
            {
                normalized = default;
                return false;
            }

            normalized = value * math.rsqrt(lengthSq);
            return true;
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

