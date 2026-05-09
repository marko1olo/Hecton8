using Hecton8.Building;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Quest;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Fired after a save slot finished loading and the base game's save pipeline completed successfully.
    /// </summary>
    internal sealed class GameLoadedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a new payload for the completed load operation.
        /// </summary>
        /// <param name="slotName">Resolved save slot name that finished loading.</param>
        public GameLoadedEvent(string slotName)
        {
            SlotName = slotName ?? string.Empty;
        }

        /// <summary>
        /// Save slot that produced the current runtime state.
        /// </summary>
        public string SlotName { get; }
    }

    /// <summary>
    /// Fired after bootstrap has a live player object and the runtime world is ready for gameplay code.
    /// </summary>
    internal sealed class PlayerSpawnedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a new player-spawn payload.
        /// </summary>
        /// <param name="playerEntityId">Stable runtime entity identifier for the spawned player root.</param>
        /// <param name="playerPosition">Frame-space player position sampled by the bootstrap boundary.</param>
        internal PlayerSpawnedEvent(ulong playerEntityId, Vector3 playerPosition)
        {
            PlayerEntityId = playerEntityId;
            PlayerPosition = playerPosition;
        }

        /// <summary>
        /// Stable runtime entity identifier for the spawned player root.
        /// </summary>
        internal ulong PlayerEntityId { get; }

        /// <summary>
        /// Frame-space player position sampled at publish time.
        /// </summary>
        internal Vector3 PlayerPosition { get; }
    }

    /// <summary>
    /// Fired after a crafting job completed and the resulting item entered the game's official crafting flow.
    /// </summary>
    internal sealed class ItemCraftedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a crafted-item payload.
        /// </summary>
        /// <param name="item">Result item that completed crafting.</param>
        public ItemCraftedEvent(ItemData item)
        {
            Item = item;
        }

        /// <summary>
        /// Crafted item asset returned by the official crafting system.
        /// </summary>
        public ItemData Item { get; }
    }

    /// <summary>
    /// Fired after the official pickup flow adds one or more world-collected items into player inventory.
    /// </summary>
    internal sealed class ItemCollectedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a collected-item payload.
        /// </summary>
        /// <param name="item">Collected item asset.</param>
        /// <param name="quantity">Successfully added quantity.</param>
        /// <param name="interactorEntityId">Stable runtime entity identifier for the interactor.</param>
        /// <param name="interactorPosition">Frame-space interactor position sampled at publish time.</param>
        /// <param name="hasInteractorPosition">True when <paramref name="interactorPosition"/> is valid.</param>
        internal ItemCollectedEvent(
            ItemData item,
            int quantity,
            ulong interactorEntityId,
            Vector3 interactorPosition,
            bool hasInteractorPosition)
            : this(
                item,
                item != null && !string.IsNullOrWhiteSpace(item.PersistentId)
                    ? Hecton.Localization.LocHash.Compute(item.PersistentId)
                    : 0,
                quantity,
                interactorEntityId,
                interactorPosition,
                hasInteractorPosition)
        {
        }

        /// <summary>
        /// Creates a collected-item payload with an explicit hash identifier.
        /// </summary>
        /// <param name="item">Collected item asset when the caller already has a visual/presentation reference.</param>
        /// <param name="itemHashId">Logic-tier item hash identifier.</param>
        /// <param name="quantity">Successfully added quantity.</param>
        /// <param name="interactorEntityId">Stable runtime entity identifier for the interactor.</param>
        /// <param name="interactorPosition">Frame-space interactor position sampled at publish time.</param>
        /// <param name="hasInteractorPosition">True when <paramref name="interactorPosition"/> is valid.</param>
        internal ItemCollectedEvent(
            ItemData item,
            int itemHashId,
            int quantity,
            ulong interactorEntityId,
            Vector3 interactorPosition,
            bool hasInteractorPosition)
        {
            Item = item;
            ItemHashId = itemHashId;
            Quantity = quantity < 0 ? 0 : quantity;
            InteractorEntityId = interactorEntityId;
            InteractorPosition = interactorPosition;
            HasInteractorPosition = hasInteractorPosition;
        }

        /// <summary>
        /// Collected item asset.
        /// </summary>
        public ItemData Item { get; }

        /// <summary>
        /// Successfully added quantity.
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// Logic-tier hash identifier for the collected item.
        /// </summary>
        public int ItemHashId { get; }

        /// <summary>
        /// Stable runtime entity identifier for the interactor that initiated the pickup flow.
        /// </summary>
        internal ulong InteractorEntityId { get; }

        /// <summary>
        /// Frame-space interactor position sampled when the event was published.
        /// </summary>
        internal Vector3 InteractorPosition { get; }

        /// <summary>
        /// True when the interactor snapshot contains a valid sampled position.
        /// </summary>
        internal bool HasInteractorPosition { get; }
    }

    /// <summary>
    /// Fired after the official recycling owner dismantles one or more inventory items into resource outputs.
    /// </summary>
    internal sealed class ItemRecycledEvent : HectonEvent
    {
        /// <summary>
        /// Creates a recycled-item payload.
        /// </summary>
        /// <param name="item">Source item consumed by the recycling owner.</param>
        /// <param name="quantity">Number of source-item units recycled.</param>
        /// <param name="yieldUnitCount">Total quantity of resource units returned to the player.</param>
        public ItemRecycledEvent(ItemData item, int quantity, int yieldUnitCount)
        {
            Item = item;
            Quantity = quantity < 0 ? 0 : quantity;
            YieldUnitCount = yieldUnitCount < 0 ? 0 : yieldUnitCount;
        }

        /// <summary>
        /// Source item consumed by the recycling owner.
        /// </summary>
        public ItemData Item { get; }

        /// <summary>
        /// Number of source-item units recycled.
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// Total quantity of resource units returned to the player.
        /// </summary>
        public int YieldUnitCount { get; }
    }

    /// <summary>
    /// Fired after the player deliberately removes an inventory item through the supported discard flow.
    /// </summary>
    internal sealed class ItemDiscardedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a discarded-item payload.
        /// </summary>
        /// <param name="item">Discarded item asset.</param>
        /// <param name="quantity">Number of discarded units.</param>
        /// <param name="interactorEntityId">Stable runtime entity identifier for the interactor.</param>
        /// <param name="interactorPosition">Frame-space interactor position sampled at publish time.</param>
        /// <param name="hasInteractorPosition">True when <paramref name="interactorPosition"/> is valid.</param>
        internal ItemDiscardedEvent(
            ItemData item,
            int quantity,
            ulong interactorEntityId,
            Vector3 interactorPosition,
            bool hasInteractorPosition)
        {
            Item = item;
            Quantity = quantity < 0 ? 0 : quantity;
            InteractorEntityId = interactorEntityId;
            InteractorPosition = interactorPosition;
            HasInteractorPosition = hasInteractorPosition;
        }

        /// <summary>
        /// Discarded item asset.
        /// </summary>
        public ItemData Item { get; }

        /// <summary>
        /// Number of discarded units.
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// Stable runtime entity identifier for the interactor that initiated the discard flow.
        /// </summary>
        internal ulong InteractorEntityId { get; }

        /// <summary>
        /// Frame-space interactor position sampled when the event was published.
        /// </summary>
        internal Vector3 InteractorPosition { get; }

        /// <summary>
        /// True when the interactor snapshot contains a valid sampled position.
        /// </summary>
        internal bool HasInteractorPosition { get; }
    }

    /// <summary>
    /// Fired after the game's discovery owner confirms a first-time biome discovery.
    /// </summary>
    internal sealed class BiomeDiscoveredEvent : HectonEvent
    {
        /// <summary>
        /// Creates a biome-discovery payload.
        /// </summary>
        /// <param name="biomeId">Stable biome identifier.</param>
        /// <param name="biomeName">Display name resolved at discovery time.</param>
        public BiomeDiscoveredEvent(int biomeId, string biomeName)
        {
            BiomeId = biomeId;
            BiomeName = biomeName ?? string.Empty;
        }

        /// <summary>
        /// Stable biome identifier.
        /// </summary>
        public int BiomeId { get; }

        /// <summary>
        /// Player-facing biome name resolved by the discovery owner.
        /// </summary>
        public string BiomeName { get; }
    }

    /// <summary>
    /// Fired before <see cref="HectonSurvivalSystem"/> applies suit integrity loss.
    /// Subscribers may cancel the event or reduce the requested damage amount.
    /// </summary>
    internal sealed class PlayerTakeDamageEvent : HectonCancellableEvent
    {
        private float _damageAmount;

        /// <summary>
        /// Creates a new cancellable damage payload.
        /// </summary>
        /// <param name="survivalSystem">Owning survival system that is about to apply damage.</param>
        /// <param name="damageAmount">Requested integrity damage before any mod mutation.</param>
        public PlayerTakeDamageEvent(HectonSurvivalSystem survivalSystem, float damageAmount)
        {
            SurvivalSystem = survivalSystem;
            _damageAmount = damageAmount < 0f ? 0f : damageAmount;
        }

        /// <summary>
        /// Survival owner about to receive the damage mutation.
        /// </summary>
        public HectonSurvivalSystem SurvivalSystem { get; }

        /// <summary>
        /// Mutable damage amount that the game will apply if the event is not cancelled.
        /// Values below zero are clamped to zero.
        /// </summary>
        public float DamageAmount
        {
            get => _damageAmount;
            set => _damageAmount = value < 0f ? 0f : value;
        }
    }

    /// <summary>
    /// Fired after a buildable module was spawned and registered by the construction pipeline.
    /// </summary>
    internal sealed class BaseModulePlacedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a placement payload for a newly placed base module.
        /// </summary>
        /// <param name="buildableData">Authoring asset that describes the placed module.</param>
        /// <param name="moduleEntityId">Stable runtime entity identifier for the placed module.</param>
        /// <param name="modulePosition">Frame-space module position sampled at publish time.</param>
        /// <param name="moduleRotation">Frame-space module rotation sampled at publish time.</param>
        /// <param name="hasModulePose">True when the module pose snapshot is valid.</param>
        internal BaseModulePlacedEvent(
            BuildableData buildableData,
            ulong moduleEntityId,
            Vector3 modulePosition,
            Quaternion moduleRotation,
            bool hasModulePose)
        {
            BuildableData = buildableData;
            ModuleEntityId = moduleEntityId;
            ModulePosition = modulePosition;
            ModuleRotation = moduleRotation;
            HasModulePose = hasModulePose;
        }

        /// <summary>
        /// Buildable asset that produced the placed module.
        /// </summary>
        public BuildableData BuildableData { get; }

        /// <summary>
        /// Stable runtime entity identifier for the placed module.
        /// </summary>
        internal ulong ModuleEntityId { get; }

        /// <summary>
        /// Frame-space module position sampled when the event was published.
        /// </summary>
        internal Vector3 ModulePosition { get; }

        /// <summary>
        /// Frame-space module rotation sampled when the event was published.
        /// </summary>
        internal Quaternion ModuleRotation { get; }

        /// <summary>
        /// True when the module pose snapshot contains valid sampled data.
        /// </summary>
        internal bool HasModulePose { get; }
    }

    /// <summary>
    /// Fired after the survival owner resolves a fatal state and records the completed death telemetry.
    /// </summary>
    internal sealed class PlayerDiedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a death payload for the latest recorded loss.
        /// </summary>
        /// <param name="survivalSystem">Owning survival system that just completed the death flow.</param>
        /// <param name="deathCause">Resolved fatal cause.</param>
        /// <param name="deathRecord">Captured telemetry for the completed life.</param>
        public PlayerDiedEvent(HectonSurvivalSystem survivalSystem, SurvivalDeathCause deathCause, SurvivalDeathRecord deathRecord)
        {
            SurvivalSystem = survivalSystem;
            DeathCause = deathCause;
            DeathRecord = deathRecord;
        }

        /// <summary>
        /// Survival owner that completed the death flow.
        /// </summary>
        public HectonSurvivalSystem SurvivalSystem { get; }

        /// <summary>
        /// Resolved fatal cause for the completed life.
        /// </summary>
        public SurvivalDeathCause DeathCause { get; }

        /// <summary>
        /// Captured death telemetry snapshot.
        /// </summary>
        public SurvivalDeathRecord DeathRecord { get; }
    }

    /// <summary>
    /// Fired after the game's internal progression registry unlocks a persistent achievement.
    /// </summary>
    internal sealed class AchievementUnlockedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a new achievement-unlock payload.
        /// </summary>
        /// <param name="achievementId">Stable internal achievement identifier.</param>
        /// <param name="title">Player-facing achievement title.</param>
        public AchievementUnlockedEvent(string achievementId, string title)
            : this(QuestFlagHashKernel.ComputeStableHash(achievementId), achievementId, title)
        {
        }

        /// <summary>
        /// Creates a new achievement-unlock payload from a pre-hashed runtime identifier.
        /// </summary>
        /// <param name="achievementHash">FNV-1a stable achievement identifier hash.</param>
        /// <param name="achievementId">Stable internal achievement identifier for persistence boundaries.</param>
        /// <param name="title">Player-facing achievement title.</param>
        public AchievementUnlockedEvent(uint achievementHash, string achievementId, string title)
        {
            AchievementHash = achievementHash;
            AchievementId = achievementId ?? string.Empty;
            Title = title ?? string.Empty;
        }

        /// <summary>
        /// FNV-1a stable achievement identifier hash.
        /// </summary>
        public uint AchievementHash { get; }

        /// <summary>
        /// Stable internal achievement identifier.
        /// </summary>
        public string AchievementId { get; }

        /// <summary>
        /// Player-facing achievement title at the time of unlock.
        /// </summary>
        public string Title { get; }
    }

    /// <summary>
    /// Fired after the contextual advisory system pushes a non-repeatable suit/PDA recommendation.
    /// </summary>
    internal sealed class PlayerAdvisoryIssuedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a new advisory-issued payload.
        /// </summary>
        /// <param name="advisoryId">Stable advisory identifier.</param>
        /// <param name="message">Player-facing advisory text.</param>
        public PlayerAdvisoryIssuedEvent(string advisoryId, string message)
            : this(QuestFlagHashKernel.ComputeStableHash(advisoryId), advisoryId, message)
        {
        }

        /// <summary>
        /// Creates a new advisory-issued payload from a pre-hashed runtime identifier.
        /// </summary>
        /// <param name="advisoryHash">FNV-1a stable advisory identifier hash.</param>
        /// <param name="advisoryId">Stable advisory identifier for persistence boundaries.</param>
        /// <param name="message">Player-facing advisory text.</param>
        public PlayerAdvisoryIssuedEvent(uint advisoryHash, string advisoryId, string message)
        {
            AdvisoryHash = advisoryHash;
            AdvisoryId = advisoryId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// FNV-1a stable advisory identifier hash.
        /// </summary>
        public uint AdvisoryHash { get; }

        /// <summary>
        /// Stable advisory identifier.
        /// </summary>
        public string AdvisoryId { get; }

        /// <summary>
        /// Player-facing advisory text.
        /// </summary>
        public string Message { get; }
    }
}
