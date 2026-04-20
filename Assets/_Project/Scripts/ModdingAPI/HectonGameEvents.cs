using Hecton8.Building;
using Hecton8.Gameplay;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Fired after a save slot finished loading and the base game's save pipeline completed successfully.
    /// </summary>
    public sealed class GameLoadedEvent : HectonEvent
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
    public sealed class PlayerSpawnedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a new player-spawn payload.
        /// </summary>
        /// <param name="playerObject">Live player GameObject published by bootstrap.</param>
        public PlayerSpawnedEvent(GameObject playerObject)
        {
            PlayerObject = playerObject;
            PlayerTransform = playerObject != null ? playerObject.transform : null;
        }

        /// <summary>
        /// Live player GameObject published by bootstrap.
        /// </summary>
        public GameObject PlayerObject { get; }

        /// <summary>
        /// Cached player transform for systems that only need the transform contract.
        /// </summary>
        public Transform PlayerTransform { get; }
    }

    /// <summary>
    /// Fired after a crafting job completed and the resulting item entered the game's official crafting flow.
    /// </summary>
    public sealed class ItemCraftedEvent : HectonEvent
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
    public sealed class ItemCollectedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a collected-item payload.
        /// </summary>
        /// <param name="item">Collected item asset.</param>
        /// <param name="quantity">Successfully added quantity.</param>
        /// <param name="interactor">Interactor responsible for the pickup flow.</param>
        public ItemCollectedEvent(ItemData item, int quantity, Transform interactor)
        {
            Item = item;
            Quantity = quantity < 0 ? 0 : quantity;
            Interactor = interactor;
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
        /// Interactor that initiated the pickup flow.
        /// </summary>
        public Transform Interactor { get; }
    }

    /// <summary>
    /// Fired after the game's discovery owner confirms a first-time biome discovery.
    /// </summary>
    public sealed class BiomeDiscoveredEvent : HectonEvent
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
    public sealed class PlayerTakeDamageEvent : HectonCancellableEvent
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
    public sealed class BaseModulePlacedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a placement payload for a newly placed base module.
        /// </summary>
        /// <param name="buildableData">Authoring asset that describes the placed module.</param>
        /// <param name="moduleObject">Live spawned GameObject that entered the construction registry.</param>
        public BaseModulePlacedEvent(BuildableData buildableData, GameObject moduleObject)
        {
            BuildableData = buildableData;
            ModuleObject = moduleObject;
            ModuleTransform = moduleObject != null ? moduleObject.transform : null;
        }

        /// <summary>
        /// Buildable asset that produced the placed module.
        /// </summary>
        public BuildableData BuildableData { get; }

        /// <summary>
        /// Live spawned GameObject registered by the construction owner.
        /// </summary>
        public GameObject ModuleObject { get; }

        /// <summary>
        /// Cached transform for placement-aware mods that only need spatial data.
        /// </summary>
        public Transform ModuleTransform { get; }
    }

    /// <summary>
    /// Fired after the survival owner resolves a fatal state and records the completed death telemetry.
    /// </summary>
    public sealed class PlayerDiedEvent : HectonEvent
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
    public sealed class AchievementUnlockedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a new achievement-unlock payload.
        /// </summary>
        /// <param name="achievementId">Stable internal achievement identifier.</param>
        /// <param name="title">Player-facing achievement title.</param>
        public AchievementUnlockedEvent(string achievementId, string title)
        {
            AchievementId = achievementId ?? string.Empty;
            Title = title ?? string.Empty;
        }

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
    public sealed class PlayerAdvisoryIssuedEvent : HectonEvent
    {
        /// <summary>
        /// Creates a new advisory-issued payload.
        /// </summary>
        /// <param name="advisoryId">Stable advisory identifier.</param>
        /// <param name="message">Player-facing advisory text.</param>
        public PlayerAdvisoryIssuedEvent(string advisoryId, string message)
        {
            AdvisoryId = advisoryId ?? string.Empty;
            Message = message ?? string.Empty;
        }

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
