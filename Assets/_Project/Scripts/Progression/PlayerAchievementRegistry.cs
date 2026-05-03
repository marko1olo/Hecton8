using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Modding;
using Hecton8.PDA;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Progression
{
    /// <summary>
    /// Save-backed internal achievement registry used for non-platform meta progression.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Progression/Player Achievement Registry")]
    public sealed class PlayerAchievementRegistry : MonoBehaviour, ITickable, ISaveable
    {
        private enum AchievementMetric : byte
        {
            SwamDistance = 0,
            CraftedItems = 1,
            DiscoveredBiomes = 2
        }

        private readonly struct AchievementDefinition
        {
            public readonly string Id;
            public readonly string Title;
            public readonly AchievementMetric Metric;
            public readonly float Threshold;

            public AchievementDefinition(string id, string title, AchievementMetric metric, float threshold)
            {
                Id = id;
                Title = title;
                Metric = metric;
                Threshold = threshold;
            }
        }

        private const float MaxReasonableStepMeters = 18f;
        private const string AchievementLogPrefix = "Achievement unlocked: ";

        // COLD ALLOC: AchievementDefinition[6] - internal progression thresholds - owner: PlayerAchievementRegistry
        private static readonly AchievementDefinition[] _definitions =
        {
            new AchievementDefinition("achievement.swim.250", "FIELD DIVER", AchievementMetric.SwamDistance, 250f),
            new AchievementDefinition("achievement.swim.1000", "ABYSS RUNNER", AchievementMetric.SwamDistance, 1000f),
            new AchievementDefinition("achievement.craft.10", "FABRICATOR HAND", AchievementMetric.CraftedItems, 10f),
            new AchievementDefinition("achievement.craft.50", "SYSTEMS ENGINEER", AchievementMetric.CraftedItems, 50f),
            new AchievementDefinition("achievement.biome.5", "CHARTED WATER", AchievementMetric.DiscoveredBiomes, 5f),
            new AchievementDefinition("achievement.biome.12", "WORLD MEMORY", AchievementMetric.DiscoveredBiomes, 12f),
        };

        // COLD ALLOC: HashSet<string>[16] - unlocked internal achievements - owner: PlayerAchievementRegistry
        private readonly HashSet<string> _unlockedAchievementIds = new HashSet<string>(StringComparer.Ordinal);

        private HectonSurvivalSystem _survivalSystem;
        private HectonDiscoveryManager _discoveryManager;
        private HectonEventSubscription _craftedSubscription;
        private HectonEventSubscription _gameLoadedSubscription;
        private bool _registeredToTick;
        private bool _registeredToSave;
        private bool _hasPositionSample;
        private Vector3 _lastPositionSample;
        private float _swamDistanceMeters;
        private int _craftedItemCount;
        private int _discoveredBiomeCount;

        /// <summary>
        /// Raised after an achievement becomes unlocked.
        /// </summary>
        public event Action<string, string> AchievementUnlocked;

        /// <inheritdoc />
        public int SavePriority => 207;

        /// <inheritdoc />
        public int LoadPriority => 207;

        private void OnEnable()
        {
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            SubscribeToEventBus();
            RebindOwnerSubscriptions();
        }

        private void Start()
        {
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            RebindOwnerSubscriptions();
        }

        private void OnDisable()
        {
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
        }

        private void OnDestroy()
        {
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (!ResolveOwners() || _survivalSystem == null || !_survivalSystem.IsAlive)
            {
                _hasPositionSample = false;
                return;
            }

            Vector3 currentPosition = transform.position;
            if (!_hasPositionSample)
            {
                _lastPositionSample = currentPosition;
                _hasPositionSample = true;
                return;
            }

            Vector3 delta = currentPosition - _lastPositionSample;
            _lastPositionSample = currentPosition;

            float deltaSqr = delta.sqrMagnitude;
            if (deltaSqr <= 0.0001f || deltaSqr > MaxReasonableStepMeters * MaxReasonableStepMeters)
                return;

            _swamDistanceMeters += Mathf.Sqrt(deltaSqr);
            EvaluateUnlocks(AchievementMetric.SwamDistance);
        }

        /// <summary>
        /// Returns true when the specified achievement is already unlocked in the current save.
        /// </summary>
        /// <param name="achievementId">Stable achievement identifier.</param>
        public bool IsUnlocked(string achievementId)
        {
            return !string.IsNullOrWhiteSpace(achievementId) && _unlockedAchievementIds.Contains(achievementId);
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.achievements.EnsureCapacity();
            data.achievements.swamDistanceMeters = Mathf.Max(0f, _swamDistanceMeters);
            data.achievements.craftedItemCount = Mathf.Max(0, _craftedItemCount);
            data.achievements.discoveredBiomeCount = Mathf.Max(0, _discoveredBiomeCount);

            int writeCount = 0;
            HashSet<string>.Enumerator enumerator = _unlockedAchievementIds.GetEnumerator();
            while (enumerator.MoveNext() && writeCount < AchievementRegistryDTO.MaxUnlockedAchievements)
            {
                data.achievements.unlockedIds[writeCount] = enumerator.Current;
                writeCount++;
            }

            data.achievements.unlockedCount = writeCount;
            for (int i = writeCount; i < AchievementRegistryDTO.MaxUnlockedAchievements; i++)
                data.achievements.unlockedIds[i] = string.Empty;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _unlockedAchievementIds.Clear();
            _swamDistanceMeters = 0f;
            _craftedItemCount = 0;
            _discoveredBiomeCount = 0;
            _hasPositionSample = false;

            if (data == null)
                return;

            _swamDistanceMeters = Mathf.Max(0f, data.achievements.swamDistanceMeters);
            _craftedItemCount = Mathf.Max(0, data.achievements.craftedItemCount);
            _discoveredBiomeCount = Mathf.Max(0, data.achievements.discoveredBiomeCount);

            int unlockedCount = Mathf.Clamp(data.achievements.unlockedCount, 0, data.achievements.unlockedIds != null ? data.achievements.unlockedIds.Length : 0);
            for (int i = 0; i < unlockedCount; i++)
            {
                string unlockedId = data.achievements.unlockedIds[i];
                if (!string.IsNullOrWhiteSpace(unlockedId))
                    _unlockedAchievementIds.Add(unlockedId);
            }
        }

        private void HandleCrafted(ItemCraftedEvent itemCraftedEvent)
        {
            _craftedItemCount++;
            EvaluateUnlocks(AchievementMetric.CraftedItems);
        }

        private void HandleGameLoaded(GameLoadedEvent gameLoadedEvent)
        {
            RebindOwnerSubscriptions();

            if (_discoveryManager != null)
                _discoveredBiomeCount = Mathf.Max(_discoveredBiomeCount, _discoveryManager.TotalDiscovered);
        }

        private void HandleBiomeDiscovered(int biomeId)
        {
            _discoveredBiomeCount++;
            EvaluateUnlocks(AchievementMetric.DiscoveredBiomes);
        }

        private void SubscribeToEventBus()
        {
            if (_craftedSubscription == null)
                _craftedSubscription = HectonEventBus.Subscribe<ItemCraftedEvent>(HandleCrafted, "progression.achievements");

            if (_gameLoadedSubscription == null)
                _gameLoadedSubscription = HectonEventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded, "progression.achievements");
        }

        private void UnsubscribeFromEventBus()
        {
            _craftedSubscription?.Dispose();
            _craftedSubscription = null;
            _gameLoadedSubscription?.Dispose();
            _gameLoadedSubscription = null;
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwners();

            if (_discoveryManager != null)
            {
                _discoveryManager.OnBiomeDiscovered += HandleBiomeDiscovered;
                _discoveredBiomeCount = Mathf.Max(_discoveredBiomeCount, _discoveryManager.TotalDiscovered);
            }

            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);
        }

        private void UnbindOwnerSubscriptions()
        {
            if (_discoveryManager != null)
                _discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;
        }

        private bool ResolveOwners()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_discoveryManager == null)
                _discoveryManager = GlobalRegistry.Discovery;

            return _survivalSystem != null;
        }

        private void EvaluateUnlocks(AchievementMetric metric)
        {
            float currentValue = GetMetricValue(metric);
            for (int i = 0; i < _definitions.Length; i++)
            {
                AchievementDefinition definition = _definitions[i];
                if (definition.Metric != metric || currentValue < definition.Threshold)
                    continue;

                Unlock(definition);
            }
        }

        private float GetMetricValue(AchievementMetric metric)
        {
            switch (metric)
            {
                case AchievementMetric.SwamDistance:
                    return _swamDistanceMeters;
                case AchievementMetric.CraftedItems:
                    return _craftedItemCount;
                case AchievementMetric.DiscoveredBiomes:
                    return _discoveredBiomeCount;
                default:
                    return 0f;
            }
        }

        private void Unlock(AchievementDefinition definition)
        {
            if (!_unlockedAchievementIds.Add(definition.Id))
                return;

            string message = AchievementLogPrefix + definition.Title;
            NotificationEvents.PushInfo(message);

            IPDALogbookService logbookManager = GlobalRegistry.PDALogbook;
            if (logbookManager != null)
                logbookManager.TryAppendEntry("achievement." + definition.Id, "ACHIEVEMENT", message);

            HectonEventBus.Publish(new AchievementUnlockedEvent(definition.Id, definition.Title));
            AchievementUnlocked?.Invoke(definition.Id, definition.Title);
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredToTick = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);

            _registeredToTick = false;
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave)
                return;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager == null)
                return;

            saveManager.Register(this);
            _registeredToSave = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave)
                return;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null)
                saveManager.Unregister(this);

            _registeredToSave = false;
        }
    }
}
