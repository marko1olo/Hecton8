using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.World;
using Hecton.Localization;
using System;
using System.Collections.Generic;
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
        private static int s_x001HarvestableOutcropSignalPushDropCount;
        private static int s_YieldDeliveryBlockedCount;
        private const string DefaultInteractText = "Break Rock";
        private const float MinimumToolPower = 0.05f;
        private const uint OutcropShardSpeciesHash = 0xC0DEFACEu;
        private const int HarvestScarRegistryCapacity = 1024;
        private static readonly uint s_YieldDeliveryBlockedWarningHash =
            unchecked((uint)LocHash.Compute("HarvestableOutcrop.YieldDeliveryBlocked"));
        private static readonly uint s_YieldDeliveryContextHash =
            unchecked((uint)LocHash.Compute("HarvestableOutcrop.YieldDelivery"));

        // COLD ALLOC: RegistryBucket<HarvestableOutcrop>[1024] - live outcrops re-checked against persisted harvest scars after a save load - owner: HarvestableOutcrop
        private static readonly RegistryBucket<HarvestableOutcrop> s_liveOutcrops =
            new RegistryBucket<HarvestableOutcrop>(HarvestScarRegistryCapacity);

        private static readonly HarvestScarLoadListener s_harvestScarLoadListener = new HarvestScarLoadListener();
        private static bool s_harvestScarLoadListenerRegistered;

        /// <summary>
        /// Re-applies persisted harvest scars once a save load has restored world depletion state.
        /// Scene props enable before <see cref="WorldStateManager"/> restores its payload, so the
        /// enable-time scar check alone would always read an empty depletion set on a fresh load.
        /// </summary>
        private sealed class HarvestScarLoadListener : Hecton8.SaveSystem.ISaveEventListener
        {
            public void OnSaveEvent(in Hecton8.SaveSystem.SaveEventPayload payload)
            {
                if (payload.Type != Hecton8.SaveSystem.SaveEventType.LoadCompleted)
                    return;

                ApplyPersistedHarvestScarsToLiveOutcrops();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHarvestScarStaticState()
        {
            s_liveOutcrops.Clear();
            s_harvestScarLoadListenerRegistered = false;
            s_YieldDeliveryBlockedCount = 0;
        }

        private static void ApplyPersistedHarvestScarsToLiveOutcrops()
        {
            for (int i = s_liveOutcrops.Count - 1; i >= 0; i--)
            {
                HarvestableOutcrop outcrop = s_liveOutcrops.GetAt(i);
                if (outcrop == null)
                    continue;

                outcrop.TryApplyPersistedHarvestScar();
            }
        }

        private static void RegisterLiveOutcrop(HarvestableOutcrop outcrop)
        {
            s_liveOutcrops.TryRegister(outcrop);

            if (s_harvestScarLoadListenerRegistered || s_liveOutcrops.Count <= 0)
                return;

            Hecton8.SaveSystem.SaveEvents.Register(s_harvestScarLoadListener);
            s_harvestScarLoadListenerRegistered = true;
        }

        private static void UnregisterLiveOutcrop(HarvestableOutcrop outcrop)
        {
            s_liveOutcrops.TryUnregister(outcrop);

            if (!s_harvestScarLoadListenerRegistered || s_liveOutcrops.Count > 0)
                return;

            Hecton8.SaveSystem.SaveEvents.Unregister(s_harvestScarLoadListener);
            s_harvestScarLoadListenerRegistered = false;
        }

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
        // COLD ALLOC: List<Renderer> - reusable child renderer cache for collapse toggles - owner: HarvestableOutcrop
        private readonly List<Renderer> _cachedRenderers = new List<Renderer>(8);
        // COLD ALLOC: List<Collider> - reusable child collider cache for collapse toggles - owner: HarvestableOutcrop
        private readonly List<Collider> _cachedColliders = new List<Collider>(8);
        private ItemData[] _resolvedLootItems;
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;
        private float _currentHealth;
        private bool _isBroken;
        private IPlayerInventoryService _playerInventoryService;
        private IPersistentDroppedItemRegistry _persistentWorldRegistry;
        private PersistentWorldRegistry _persistentWorldScarRegistry;
        private WorldStateManager _worldStateManager;
        private AbsoluteUniversePosition _persistentScarAup;
        private bool _hasPersistentScarAup;
        private ulong _persistentScarTombstoneId;
        private string _persistentScarLabel;
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

            _cachedRenderers.Clear();
            GetComponentsInChildren<Renderer>(true, _cachedRenderers);
            _cachedColliders.Clear();
            GetComponentsInChildren<Collider>(true, _cachedColliders);

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
            RegisterLiveOutcrop(this);
            TryApplyPersistedHarvestScar();
        }

        private void OnDisable()
        {
            UnregisterLiveOutcrop(this);
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            StopLateFrameTicking();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        private void OnDestroy()
        {
            UnregisterLiveOutcrop(this);
            InteractableRegistry.InvalidateTree(this);
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
        void IInteractable.Interact(Transform interactor)
        {
            if (!allowDirectInteract || _isBroken)
                return;

            TakeDamage(damagePerHit, _cachedTransform.position, 1f, Vector3.up);
        }

        /// <inheritdoc />
        string IInteractable.GetInteractText()
        {
            return allowDirectInteract && !_isBroken ? ResolveLegacyConfigured(interactText, DefaultInteractText) : null;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            ReadOnlySpan<char> source = allowDirectInteract && !_isBroken
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

            if (!CanDispatchYield(toolPower, hitPoint))
            {
                _currentHealth = math.max(_currentHealth, MinimumToolPower);
                return;
            }

            _isBroken = true;
            RefreshPersistentScarIdentity();
            RegisterPersistentHarvestScar();
            QueueBreakEffects();
            QueueIntactRendererState(false);
            DisableIntactColliders();
            DispatchDebris(hitPoint, hitNormal, toolPower);
            DispatchYield(toolPower, hitPoint);
            QueueComponentDisable();
        }

        /// <summary>
        /// Resolves the outcrop's stable world-scar identity from its absolute universe position.
        /// Shares the ResourceNode tombstone id space so one consumed node maps to one persisted
        /// scar regardless of which world-object component owns it.
        /// </summary>
        private void RefreshPersistentScarIdentity()
        {
            Vector3 runtimePosition = _cachedTransform != null ? _cachedTransform.position : transform.position;
            _hasPersistentScarAup = TryResolveAupFromRuntimeOrigin(runtimePosition, out _persistentScarAup);
            ulong tombstoneId = _hasPersistentScarAup
                ? PersistentWorldRegistry.ComputeResourceNodeTombstoneId(in _persistentScarAup)
                : 0UL;

            if (tombstoneId == _persistentScarTombstoneId)
                return;

            _persistentScarTombstoneId = tombstoneId;
            _persistentScarLabel = null;
        }

        private string ResolvePersistentScarLabel()
        {
            if (_persistentScarLabel != null)
                return _persistentScarLabel;

            if (_persistentScarTombstoneId == 0UL)
                return null;

            // COLD ALLOC: string[30] - stable world-scar label written into the WorldStateManager save payload - owner: HarvestableOutcrop
            _persistentScarLabel = PersistentWorldRegistry.FormatResourceNodeTombstoneId(_persistentScarTombstoneId);
            return _persistentScarLabel;
        }

        /// <summary>
        /// True when this outcrop was already harvested in a previous session or earlier in this one.
        /// </summary>
        private bool IsPersistentlyHarvested()
        {
            if (_persistentScarTombstoneId == 0UL)
                return false;

            PersistentWorldRegistry scarRegistry = _persistentWorldScarRegistry;
            if (scarRegistry != null && scarRegistry.IsResourceNodeTombstoned(_persistentScarTombstoneId))
                return true;

            WorldStateManager worldState = _worldStateManager;
            if (worldState == null || worldState.DepletedCount <= 0)
                return false;

            string scarLabel = ResolvePersistentScarLabel();
            return !string.IsNullOrEmpty(scarLabel) && worldState.IsNodeDepleted(scarLabel);
        }

        /// <summary>
        /// Records the collapse as a persistent world scar on both the native tombstone route and the
        /// save-payload depletion set, so the node stays consumed across quit/reload.
        /// </summary>
        private void RegisterPersistentHarvestScar()
        {
            if (_persistentScarTombstoneId == 0UL)
                return;

            PersistentWorldRegistry scarRegistry = _persistentWorldScarRegistry;
            if (scarRegistry != null && _hasPersistentScarAup)
                scarRegistry.TryRegisterDestroyedResourceNode(_persistentScarTombstoneId, in _persistentScarAup);

            WorldStateManager worldState = _worldStateManager;
            if (worldState == null)
                return;

            string scarLabel = ResolvePersistentScarLabel();
            if (!string.IsNullOrEmpty(scarLabel))
                worldState.RegisterDepletedNode(scarLabel);
        }

        /// <summary>
        /// Restores the collapsed state for an outcrop that was already harvested, without replaying
        /// the break audio, debris, or yield of a fresh collapse.
        /// </summary>
        private void TryApplyPersistedHarvestScar()
        {
            if (_isBroken || !isActiveAndEnabled)
                return;

            RefreshPersistentScarIdentity();
            if (!IsPersistentlyHarvested())
                return;

            _isBroken = true;
            _currentHealth = 0f;
            ApplyIntactRendererState(false);
            DisableIntactColliders();
            InteractableRegistry.InvalidateTree(this);
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
            if (!TryResolveYield(toolPower, out ItemData item, out int quantity))
                return;

            IPlayerInventoryService playerInventoryService = _playerInventoryService;
            PlayerInventory playerInventory = playerInventoryService != null ? playerInventoryService.Inventory : null;
            int rejectedQuantity = quantity;
            if (playerInventory != null)
            {
                int itemHashId = ItemData.ResolvePersistentHashId(item);
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

            IPersistentDroppedItemRegistry registry = _persistentWorldRegistry;
            if (registry != null && rejectedQuantity > 0)
                registry.TryRegisterDroppedItem(item, rejectedQuantity, dropPoint);
        }

        private bool CanDispatchYield(float toolPower, Vector3 dropPoint)
        {
            if (!TryResolveYield(toolPower, out ItemData item, out int quantity))
                return true;

            int itemHashId = ItemData.ResolvePersistentHashId(item);
            if (itemHashId == 0)
            {
                ReportYieldDeliveryBlocked(itemHashId, quantity);
                return false;
            }

            IPlayerInventoryService playerInventoryService = _playerInventoryService;
            PlayerInventory playerInventory = playerInventoryService != null ? playerInventoryService.Inventory : null;
            if (playerInventory != null &&
                playerInventory.CanAcceptItemQuantity(itemHashId, quantity))
            {
                return true;
            }

            IPersistentDroppedItemRegistry registry = _persistentWorldRegistry;
            if (registry != null &&
                registry.CanRegisterDroppedItem(item, quantity, dropPoint))
            {
                return true;
            }

            ReportYieldDeliveryBlocked(itemHashId, quantity);
            return false;
        }

        private bool TryResolveYield(float toolPower, out ItemData item, out int quantity)
        {
            item = null;
            quantity = 0;
            if (_resolvedLootItems == null || _resolvedLootItems.Length == 0)
                return false;

            item = ResolveYieldItem(toolPower);
            if (item == null)
                return false;

            quantity = (int)math.ceil(math.max(MinimumToolPower, toolPower) * math.max(rockDensity, 0.1f));
            quantity = math.max(math.max(1, minLootCount), quantity);
            quantity = math.min(math.max(quantity, 1), math.max(1, maxLootCount));
            return quantity > 0;
        }

        private static void ReportYieldDeliveryBlocked(int itemHashId, int quantity)
        {
            s_YieldDeliveryBlockedCount++;
            uint contextHash = s_YieldDeliveryContextHash ^ unchecked((uint)itemHashId);
            GlobalTelemetryBus.PublishPerformanceWarning(
                s_YieldDeliveryBlockedWarningHash,
                contextHash,
                math.max(1, math.max(quantity, s_YieldDeliveryBlockedCount)));
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
            for (int i = 0; i < _cachedColliders.Count; i++)
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
            for (int i = 0; i < _cachedRenderers.Count; i++)
            {
                Renderer renderer = _cachedRenderers[i];
                if (renderer != null)
                    renderer.enabled = enabledState;
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
            if (!HasPendingLateFrameWork())
                return;

            if (_pendingRendererStateDirty)
            {
                _pendingRendererStateDirty = false;
                ApplyIntactRendererState(_pendingRendererEnabled);
            }

            IAudioService audio = ResolveAudioService();
            TryResolveCachedObjectPool(out IObjectPoolService pool);

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
                SignalBus<DebrisSpawnSignal>.TryPushTracked(in _pendingDebris, ref s_x001HarvestableOutcropSignalPushDropCount);
                _pendingDebris = default;
            }

            if (_pendingDisableComponent)
            {
                _pendingDisableComponent = false;
                enabled = false;
            }
        }

        private bool HasPendingLateFrameWork()
        {
            return _pendingRendererStateDirty ||
                   _pendingHitAudio ||
                   _pendingHitParticle ||
                   _pendingBreakAudio ||
                   _pendingBreakParticle ||
                   _pendingDebrisSignal ||
                   _pendingDisableComponent;
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

            for (int i = 0; i < _cachedRenderers.Count; i++)
            {
                Renderer renderer = _cachedRenderers[i];
                if (renderer != null)
                    renderer.enabled = true;
            }

            for (int i = 0; i < _cachedColliders.Count; i++)
            {
                Collider collider = _cachedColliders[i];
                if (collider != null)
                    collider.enabled = true;
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

                    ItemData item = CaptureItemFromPrefabCold(prefab);
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

        private static ItemData CaptureItemFromPrefabCold(GameObject prefab)
        {
            if (prefab == null)
                return null;

            prefab.TryGetComponent(out HectonItem hectonItem);
            if (hectonItem != null && hectonItem.Data != null)
                return hectonItem.Data;

            prefab.TryGetComponent(out PickupItem pickupItem);
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
                    _persistentWorldRegistry = currentService as IPersistentDroppedItemRegistry;
                    _persistentWorldScarRegistry = currentService as PersistentWorldRegistry;
                    TryApplyPersistedHarvestScar();
                    break;
                case GlobalRegistryServiceSlot.WorldStateRuntime:
                    _worldStateManager = currentService as WorldStateManager;
                    TryApplyPersistedHarvestScar();
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
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
            _persistentWorldRegistry = GlobalRegistry.PersistentDroppedItems;
            _persistentWorldScarRegistry = GlobalRegistry.PersistentWorldRegistry;
            _worldStateManager = GlobalRegistry.WorldState;
            CacheAudioService(GlobalRegistry.Audio);
            CacheObjectPoolService(null);
            _localizationManager = GlobalRegistry.LocalizationText;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPool = pool;
                return;
            }

            _objectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPool = resolved;
                pool = resolved;
                return true;
            }

            _objectPool = null;
            pool = null;
            return false;
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
                Frame = SystemDispatcher.CurrentFrameId
            };
            SignalBus<ItemAcquiredSignal>.TryPushTracked(in signal, ref s_x001HarvestableOutcropSignalPushDropCount);
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

