using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Meta
{
    /// <summary>
    /// Global progression owner that persists hall-of-fame records, marathon goals, meta currency,
    /// and permanent upgrades outside slot saves.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6500)]
    [AddComponentMenu("Hecton8/Meta/Global Profile Manager")]
    public sealed class GlobalProfileManager : MonoBehaviour, ISlowTickable, IProfileService
    {
        private enum MarathonMetric : byte
        {
            StructuralMetalCollected = 0,
            CraftedItems = 1,
            DiscoveredBiomes = 2,
            RecycledItems = 3
        }

        private readonly struct AchievementRewardDefinition
        {
            public readonly uint IdHash;
            public readonly int ExplorerPoints;

            public AchievementRewardDefinition(string id, int explorerPoints)
            {
                IdHash = QuestFlagHashKernel.ComputeStableHash(id);
                ExplorerPoints = explorerPoints;
            }
        }

        private readonly struct MarathonGoalDefinition
        {
            public readonly string Id;
            public readonly uint IdHash;
            public readonly string Title;
            public readonly MarathonMetric Metric;
            public readonly int Target;
            public readonly int RewardExplorerPoints;

            public MarathonGoalDefinition(string id, string title, MarathonMetric metric, int target, int rewardExplorerPoints)
            {
                Id = id;
                IdHash = QuestFlagHashKernel.ComputeStableHash(id);
                Title = title;
                Metric = metric;
                Target = target;
                RewardExplorerPoints = rewardExplorerPoints;
            }
        }

        private const string ProfileDirectoryName = "Meta";
        private const string ProfileFileName = "profile.json";
        private const string ProfileTempFileName = "profile.json.tmp";
        private const float FlushIntervalSeconds = 15f;
        private const float LongestLifeRecordStepSeconds = 60f;
        private const float MaxDepthRecordEpsilon = 0.25f;

        // COLD ALLOC: AchievementRewardDefinition[6] - fixed explorer-point rewards for first-time internal achievements - owner: GlobalProfileManager
        private static readonly AchievementRewardDefinition[] _achievementRewards =
        {
            new AchievementRewardDefinition("achievement.swim.250", 10),
            new AchievementRewardDefinition("achievement.swim.1000", 25),
            new AchievementRewardDefinition("achievement.craft.10", 10),
            new AchievementRewardDefinition("achievement.craft.50", 25),
            new AchievementRewardDefinition("achievement.biome.5", 15),
            new AchievementRewardDefinition("achievement.biome.12", 30),
        };

        // COLD ALLOC: MarathonGoalDefinition[4] - fixed all-time meta retention goals - owner: GlobalProfileManager
        private static readonly MarathonGoalDefinition[] _marathonDefinitions =
        {
            new MarathonGoalDefinition(
                "marathon.collect.structural_metal.10000",
                "Collect 10,000 structural metal",
                MarathonMetric.StructuralMetalCollected,
                10000,
                150),
            new MarathonGoalDefinition(
                "marathon.craft.items.500",
                "Craft 500 items",
                MarathonMetric.CraftedItems,
                500,
                100),
            new MarathonGoalDefinition(
                "marathon.discover.biomes.100",
                "Discover 100 biomes",
                MarathonMetric.DiscoveredBiomes,
                100,
                125),
            new MarathonGoalDefinition(
                "marathon.recycle.items.500",
                "Waste-not manufacturing",
                MarathonMetric.RecycledItems,
                500,
                175),
        };

        // COLD ALLOC: HashSet<uint>[64] - global unlocked achievement hash lookup - owner: GlobalProfileManager
        private readonly HashSet<uint> _globalUnlockedAchievementHashes = new HashSet<uint>(GlobalProfileData.MaxUnlockedAchievements);
        private HectonSurvivalSystem _survivalSystem;
        private HectonDiscoveryManager _discoveryManager;
        private GlobalProfileData _profile = new GlobalProfileData();
        private HectonEventSubscription _achievementUnlockedSubscription;
        private HectonEventSubscription _gameLoadedSubscription;
        private HectonEventSubscription _playerDiedSubscription;
        private HectonEventSubscription _itemCollectedSubscription;
        private HectonEventSubscription _itemCraftedSubscription;
        private HectonEventSubscription _itemRecycledSubscription;
        private bool _registeredToTick;
        private bool _registeredProfileService;
        private bool _dirty;
        private float _flushTimer;
        private float _nextLongestLifeRecordThreshold = LongestLifeRecordStepSeconds;
        private int _currentRunBiomeDiscoveries;

        /// <summary>
        /// Raised after the global profile data changes.
        /// </summary>
        public event Action ProfileChanged;

        /// <summary>
        /// Current meta currency balance.
        /// </summary>
        public int ExplorerPoints => _profile != null ? _profile.explorerPoints : 0;

        /// <summary>
        /// Deepest recorded depth across all time.
        /// </summary>
        public float MaxDepthMeters => _profile != null ? _profile.maxDepthMeters : 0f;

        /// <summary>
        /// Longest recorded life duration across all time.
        /// </summary>
        public float LongestLifeWithoutDeathSeconds => _profile != null ? _profile.longestLifeWithoutDeathSeconds : 0f;

        /// <summary>
        /// Highest biome discovery count reached within a single run.
        /// </summary>
        public int HighestBiomeDiscoveriesInSingleRun => _profile != null ? _profile.highestBiomeDiscoveriesInSingleRun : 0;

        /// <summary>
        /// Returns true when the achievement is unlocked in the global profile showcase.
        /// </summary>
        /// <param name="achievementId">Stable achievement identifier.</param>
        public bool HasUnlockedAchievement(string achievementId)
        {
            uint achievementHash = QuestFlagHashKernel.ComputeStableHash(achievementId);
            return achievementHash != 0u && _globalUnlockedAchievementHashes.Contains(achievementHash);
        }

        /// <summary>
        /// Returns the currently owned level for a permanent meta upgrade.
        /// </summary>
        /// <param name="upgradeId">Stable permanent-upgrade identifier.</param>
        public int GetUpgradeLevel(string upgradeId)
        {
            return ResolveUpgradeLevel(upgradeId);
        }

        /// <summary>
        /// Attempts to purchase the next level of a permanent meta upgrade.
        /// </summary>
        /// <param name="upgradeId">Stable permanent-upgrade identifier.</param>
        /// <param name="error">Human-readable rejection reason when the purchase fails.</param>
        public bool TryPurchaseUpgrade(string upgradeId, out string error)
        {
            error = null;

            if (!MetaUpgradeRegistry.TryGetDefinition(upgradeId, out MetaUpgradeRegistry.MetaUpgradeDefinition definition))
            {
                error = "Unknown meta upgrade.";
                return false;
            }

            int currentLevel = ResolveUpgradeLevel(definition.IdHash);
            if (currentLevel >= definition.MaxLevel)
            {
                error = "Upgrade is already at max level.";
                return false;
            }

            int cost = definition.GetCostForNextLevel(currentLevel);
            if (_profile.explorerPoints < cost)
            {
                error = "Not enough Explorer Points.";
                return false;
            }

            _profile.explorerPoints -= cost;
            SetUpgradeLevel(definition.IdHash, definition.Id, currentLevel + 1);
            MarkDirty();
            return true;
        }

        /// <summary>
        /// Returns the live profile payload.
        /// This is intended for cold-path read-only consumers such as runtime injectors and menu presenters.
        /// </summary>
        public GlobalProfileData GetSnapshot()
        {
            return _profile;
        }

        /// <summary>
        /// Attempts to load the global profile from disk without requiring a live manager instance.
        /// </summary>
        /// <param name="profile">Loaded profile or a repaired empty profile when the file is absent.</param>
        public static bool TryLoadSnapshot(out GlobalProfileData profile)
        {
            profile = LoadProfileFromDisk();
            return profile != null;
        }

        private void Awake()
        {
            IProfileService registered = GlobalRegistry.Profile;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            TryRegisterProfileService();
            LoadProfile();
        }

        private void OnEnable()
        {
            TryRegisterProfileService();
            TryRegisterWithTickManager();
            SubscribeToEventBus();
            RebindOwnerSubscriptions();
        }

        private void Start()
        {
            TryRegisterWithTickManager();
            RebindOwnerSubscriptions();
        }

        private void OnDisable()
        {
            FlushCurrentRunRecords();
            FlushIfDirty();
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            TryUnregisterProfileService();
        }

        private void OnDestroy()
        {
            FlushCurrentRunRecords();
            FlushIfDirty();
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            TryUnregisterProfileService();
        }

        private void OnApplicationQuit()
        {
            FlushCurrentRunRecords();
            FlushIfDirty();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!ResolveOwnersHot())
                return;

            TrackCurrentRunRecords();

            _flushTimer += 0.5f;
            if (_dirty && _flushTimer >= FlushIntervalSeconds)
                FlushIfDirty();
        }

        private void HandleAchievementUnlocked(AchievementUnlockedEvent achievementUnlockedEvent)
        {
            if (achievementUnlockedEvent == null || achievementUnlockedEvent.AchievementHash == 0u)
                return;

            float unlockTimeSeconds = ResolveCurrentRunElapsedSeconds();
            UpdateFastestAchievementRecord(achievementUnlockedEvent.AchievementHash, achievementUnlockedEvent.AchievementId, achievementUnlockedEvent.Title, unlockTimeSeconds);

            if (_globalUnlockedAchievementHashes.Add(achievementUnlockedEvent.AchievementHash))
            {
                _profile.explorerPoints += ResolveExplorerPointReward(achievementUnlockedEvent.AchievementHash);
                StoreUnlockedAchievementId(achievementUnlockedEvent.AchievementId);
                MarkDirty();
            }
        }

        private void HandleGameLoaded(GameLoadedEvent gameLoadedEvent)
        {
            RebindOwnerSubscriptions();

            HectonDiscoveryManager discoveryManager = GlobalRegistry.Discovery;
            _currentRunBiomeDiscoveries = discoveryManager != null ? discoveryManager.TotalDiscovered : 0;
            if (_currentRunBiomeDiscoveries > _profile.highestBiomeDiscoveriesInSingleRun)
            {
                _profile.highestBiomeDiscoveriesInSingleRun = _currentRunBiomeDiscoveries;
                MarkDirty();
            }
        }

        private void HandleBiomeDiscovered(int biomeId)
        {
            _currentRunBiomeDiscoveries++;
            _profile.totalBiomesDiscoveredAllTime = Mathf.Max(0, _profile.totalBiomesDiscoveredAllTime + 1);
            if (_currentRunBiomeDiscoveries > _profile.highestBiomeDiscoveriesInSingleRun)
                _profile.highestBiomeDiscoveriesInSingleRun = _currentRunBiomeDiscoveries;

            AdvanceMarathonProgress(MarathonMetric.DiscoveredBiomes, 1);
            MarkDirty();
        }

        private void HandlePlayerDied(PlayerDiedEvent playerDiedEvent)
        {
            if (playerDiedEvent == null)
                return;

            SurvivalDeathRecord deathRecord = playerDiedEvent.DeathRecord;
            if (deathRecord.LifeDurationSeconds > _profile.longestLifeWithoutDeathSeconds)
            {
                _profile.longestLifeWithoutDeathSeconds = (float)deathRecord.LifeDurationSeconds;
                _nextLongestLifeRecordThreshold = (float)(deathRecord.LifeDurationSeconds + LongestLifeRecordStepSeconds);
                MarkDirty();
            }

            if (deathRecord.PeakDepthMeters > _profile.maxDepthMeters)
            {
                _profile.maxDepthMeters = (float)deathRecord.PeakDepthMeters;
                MarkDirty();
            }
        }

        private void HandleItemCollected(ItemCollectedEvent itemCollectedEvent)
        {
            if (itemCollectedEvent == null || itemCollectedEvent.Item == null || itemCollectedEvent.Quantity <= 0)
                return;

            if (itemCollectedEvent.Item.resourceFamily != ResourceFamily.StructuralMetal)
                return;

            AdvanceMarathonProgress(MarathonMetric.StructuralMetalCollected, itemCollectedEvent.Quantity);
        }

        private void HandleItemCrafted(ItemCraftedEvent itemCraftedEvent)
        {
            if (itemCraftedEvent == null || itemCraftedEvent.Item == null)
                return;

            AdvanceMarathonProgress(MarathonMetric.CraftedItems, 1);
        }

        private void HandleItemRecycled(ItemRecycledEvent itemRecycledEvent)
        {
            if (itemRecycledEvent == null || itemRecycledEvent.Quantity <= 0)
                return;

            AdvanceMarathonProgress(MarathonMetric.RecycledItems, itemRecycledEvent.Quantity);
        }

        private void SubscribeToEventBus()
        {
            if (_achievementUnlockedSubscription == null)
                _achievementUnlockedSubscription = HectonEventBus.Subscribe<AchievementUnlockedEvent>(HandleAchievementUnlocked, "meta.profile");

            if (_gameLoadedSubscription == null)
                _gameLoadedSubscription = HectonEventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded, "meta.profile");

            if (_playerDiedSubscription == null)
                _playerDiedSubscription = HectonEventBus.Subscribe<PlayerDiedEvent>(HandlePlayerDied, "meta.profile");

            if (_itemCollectedSubscription == null)
                _itemCollectedSubscription = HectonEventBus.Subscribe<ItemCollectedEvent>(HandleItemCollected, "meta.profile");

            if (_itemCraftedSubscription == null)
                _itemCraftedSubscription = HectonEventBus.Subscribe<ItemCraftedEvent>(HandleItemCrafted, "meta.profile");

            if (_itemRecycledSubscription == null)
                _itemRecycledSubscription = HectonEventBus.Subscribe<ItemRecycledEvent>(HandleItemRecycled, "meta.profile");
        }

        private void UnsubscribeFromEventBus()
        {
            _achievementUnlockedSubscription?.Dispose();
            _achievementUnlockedSubscription = null;
            _gameLoadedSubscription?.Dispose();
            _gameLoadedSubscription = null;
            _playerDiedSubscription?.Dispose();
            _playerDiedSubscription = null;
            _itemCollectedSubscription?.Dispose();
            _itemCollectedSubscription = null;
            _itemCraftedSubscription?.Dispose();
            _itemCraftedSubscription = null;
            _itemRecycledSubscription?.Dispose();
            _itemRecycledSubscription = null;
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwnersCold();

            if (_survivalSystem != null)
                _nextLongestLifeRecordThreshold = Mathf.Max(_profile.longestLifeWithoutDeathSeconds + LongestLifeRecordStepSeconds, LongestLifeRecordStepSeconds);
        }

        private void UnbindOwnerSubscriptions()
        {
            if (_discoveryManager != null)
                _discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

            _discoveryManager = null;
        }

        private bool ResolveOwnersHot()
        {
            HectonDiscoveryManager discoveryManager = GlobalRegistry.Discovery;
            if (discoveryManager != null && !ReferenceEquals(_discoveryManager, discoveryManager))
            {
                if (_discoveryManager != null)
                    _discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

                _discoveryManager = discoveryManager;
                _discoveryManager.OnBiomeDiscovered += HandleBiomeDiscovered;
            }

            if (_discoveryManager != null)
                _currentRunBiomeDiscoveries = _discoveryManager.TotalDiscovered;

            return _survivalSystem != null || _discoveryManager != null;
        }

        private bool ResolveOwnersCold()
        {
            GameObject playerObject = GameBootstrapper.CurrentPlayerObject;
            if (_survivalSystem == null && playerObject != null)
                playerObject.TryGetComponent(out _survivalSystem);

            return ResolveOwnersHot();
        }

        private void TrackCurrentRunRecords()
        {
            if (_survivalSystem == null)
                return;

            float depth = _survivalSystem.Depth;
            if (depth > _profile.maxDepthMeters + MaxDepthRecordEpsilon)
            {
                _profile.maxDepthMeters = depth;
                MarkDirty();
            }

            double currentLifeDuration = _survivalSystem.CurrentLifeDurationSeconds;
            if (currentLifeDuration > _profile.longestLifeWithoutDeathSeconds && currentLifeDuration >= _nextLongestLifeRecordThreshold)
            {
                _profile.longestLifeWithoutDeathSeconds = (float)currentLifeDuration;
                _nextLongestLifeRecordThreshold = (float)(currentLifeDuration + LongestLifeRecordStepSeconds);
                MarkDirty();
            }
        }

        private void FlushCurrentRunRecords()
        {
            bool changed = false;

            if (_survivalSystem != null)
            {
                float depth = _survivalSystem.Depth;
                if (depth > _profile.maxDepthMeters)
                {
                    _profile.maxDepthMeters = depth;
                    changed = true;
                }

                double currentLifeDuration = _survivalSystem.CurrentLifeDurationSeconds;
                if (currentLifeDuration > _profile.longestLifeWithoutDeathSeconds)
                {
                    _profile.longestLifeWithoutDeathSeconds = (float)currentLifeDuration;
                    changed = true;
                }
            }

            if (_currentRunBiomeDiscoveries > _profile.highestBiomeDiscoveriesInSingleRun)
            {
                _profile.highestBiomeDiscoveriesInSingleRun = _currentRunBiomeDiscoveries;
                changed = true;
            }

            if (changed)
                MarkDirty();
        }

        private void LoadProfile()
        {
            _profile = LoadProfileFromDisk();
            if (_profile == null)
                _profile = new GlobalProfileData();

            _profile.EnsureCapacity();
            _profile.version = GlobalProfileData.CurrentVersion;
            _profile.unlockedAchievementCount = Mathf.Clamp(_profile.unlockedAchievementCount, 0, GlobalProfileData.MaxUnlockedAchievements);
            _profile.fastestAchievementRecordCount = Mathf.Clamp(_profile.fastestAchievementRecordCount, 0, GlobalProfileData.MaxFastestAchievementRecords);
            _profile.purchasedUpgradeCount = Mathf.Clamp(_profile.purchasedUpgradeCount, 0, GlobalProfileData.MaxPurchasedUpgrades);
            _profile.marathonGoalCount = Mathf.Clamp(_profile.marathonGoalCount, 0, GlobalProfileData.MaxMarathonGoals);
            NormalizeUpgradeRecordHashes();
            SynchronizeUpgradeRecords();
            SynchronizeMarathonGoalRecords();
            RebuildUnlockedAchievementLookup();
            NormalizeFastestAchievementHashes();
            _nextLongestLifeRecordThreshold = Mathf.Max(_profile.longestLifeWithoutDeathSeconds + LongestLifeRecordStepSeconds, LongestLifeRecordStepSeconds);
        }

        private void SynchronizeUpgradeRecords()
        {
            _profile.EnsureCapacity();
            for (int i = 0; i < MetaUpgradeRegistry.DefinitionCount; i++)
            {
                MetaUpgradeRegistry.MetaUpgradeDefinition definition = MetaUpgradeRegistry.GetDefinition(i);
                EnsureUpgradeRecordIndex(definition.IdHash, definition.Id);
            }
        }

        private void SynchronizeMarathonGoalRecords()
        {
            _profile.EnsureCapacity();
            for (int i = 0; i < _marathonDefinitions.Length; i++)
            {
                EnsureMarathonRecordIndex(_marathonDefinitions[i]);
            }
        }

        private void RebuildUnlockedAchievementLookup()
        {
            _globalUnlockedAchievementHashes.Clear();
            int count = Mathf.Clamp(_profile.unlockedAchievementCount, 0, _profile.unlockedAchievementIds != null ? _profile.unlockedAchievementIds.Length : 0);
            for (int i = 0; i < count; i++)
            {
                string achievementId = _profile.unlockedAchievementIds[i];
                uint achievementHash = QuestFlagHashKernel.ComputeStableHash(achievementId);
                if (achievementHash != 0u)
                    _globalUnlockedAchievementHashes.Add(achievementHash);
            }
        }

        private void StoreUnlockedAchievementId(string achievementId)
        {
            _profile.EnsureCapacity();
            int count = Mathf.Clamp(_profile.unlockedAchievementCount, 0, GlobalProfileData.MaxUnlockedAchievements);
            if (count >= GlobalProfileData.MaxUnlockedAchievements)
                return;

            _profile.unlockedAchievementIds[count] = achievementId;
            _profile.unlockedAchievementCount = count + 1;
        }

        private void NormalizeFastestAchievementHashes()
        {
            int count = Mathf.Clamp(_profile.fastestAchievementRecordCount, 0, GlobalProfileData.MaxFastestAchievementRecords);
            for (int i = 0; i < count; i++)
            {
                FastestAchievementRecord record = _profile.fastestAchievementRecords[i];
                if (record.achievementHash != 0u)
                    continue;

                record.achievementHash = QuestFlagHashKernel.ComputeStableHash(record.achievementId);
                _profile.fastestAchievementRecords[i] = record;
            }
        }

        private void UpdateFastestAchievementRecord(uint achievementHash, string achievementId, string title, float unlockTimeSeconds)
        {
            if (achievementHash == 0u)
                return;

            _profile.EnsureCapacity();
            int count = Mathf.Clamp(_profile.fastestAchievementRecordCount, 0, GlobalProfileData.MaxFastestAchievementRecords);
            for (int i = 0; i < count; i++)
            {
                FastestAchievementRecord record = _profile.fastestAchievementRecords[i];
                uint recordHash = record.achievementHash != 0u ? record.achievementHash : QuestFlagHashKernel.ComputeStableHash(record.achievementId);
                if (recordHash != achievementHash)
                    continue;

                if (record.achievementHash == 0u)
                    record.achievementHash = recordHash;

                if (unlockTimeSeconds < record.bestTimeSeconds || record.bestTimeSeconds <= 0f)
                {
                    record.bestTimeSeconds = unlockTimeSeconds;
                    record.title = string.IsNullOrWhiteSpace(title) ? record.title : title;
                    _profile.fastestAchievementRecords[i] = record;
                    MarkDirty();
                }

                return;
            }

            if (count >= GlobalProfileData.MaxFastestAchievementRecords)
                return;

            _profile.fastestAchievementRecords[count] = new FastestAchievementRecord
            {
                achievementId = achievementId,
                achievementHash = achievementHash,
                title = title ?? string.Empty,
                bestTimeSeconds = unlockTimeSeconds
            };
            _profile.fastestAchievementRecordCount = count + 1;
            MarkDirty();
        }

        private void AdvanceMarathonProgress(MarathonMetric metric, int amount)
        {
            if (amount <= 0)
                return;

            for (int i = 0; i < _marathonDefinitions.Length; i++)
            {
                MarathonGoalDefinition definition = _marathonDefinitions[i];
                if (definition.Metric != metric)
                    continue;

                int recordIndex = EnsureMarathonRecordIndex(definition);
                if (recordIndex < 0)
                    continue;

                MarathonGoalProgressRecord record = _profile.marathonGoals[recordIndex];
                int clampedTarget = Mathf.Max(1, definition.Target);
                int nextProgress = record.progress + amount;
                if (nextProgress > clampedTarget)
                    nextProgress = clampedTarget;

                if (nextProgress == record.progress && record.claimed)
                    continue;

                record.progress = nextProgress;
                if (!record.claimed && record.progress >= clampedTarget)
                {
                    record.claimed = true;
                    _profile.explorerPoints += Mathf.Max(0, definition.RewardExplorerPoints);
                }

                _profile.marathonGoals[recordIndex] = record;
                MarkDirty();
            }
        }

        private int ResolveUpgradeLevel(string upgradeId)
        {
            uint upgradeHash = QuestFlagHashKernel.ComputeStableHash(upgradeId);
            return ResolveUpgradeLevel(upgradeHash);
        }

        private int ResolveUpgradeLevel(uint upgradeHash)
        {
            int index = FindUpgradeRecordIndex(upgradeHash);
            if (index < 0)
                return 0;

            return Mathf.Max(0, _profile.purchasedUpgradeLevels[index].level);
        }

        private void SetUpgradeLevel(uint upgradeHash, string upgradeId, int level)
        {
            int index = EnsureUpgradeRecordIndex(upgradeHash, upgradeId);
            if (index < 0)
                return;

            MetaUpgradeLevelRecord record = _profile.purchasedUpgradeLevels[index];
            record.upgradeHash = upgradeHash;
            record.upgradeId = upgradeId;
            record.level = Mathf.Max(0, level);
            _profile.purchasedUpgradeLevels[index] = record;
        }

        private void NormalizeUpgradeRecordHashes()
        {
            if (_profile == null || _profile.purchasedUpgradeLevels == null)
                return;

            int count = Mathf.Clamp(_profile.purchasedUpgradeCount, 0, _profile.purchasedUpgradeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                MetaUpgradeLevelRecord record = _profile.purchasedUpgradeLevels[i];
                if (record.upgradeHash != 0u)
                    continue;

                record.upgradeHash = QuestFlagHashKernel.ComputeStableHash(record.upgradeId);
                _profile.purchasedUpgradeLevels[i] = record;
            }
        }

        private int FindUpgradeRecordIndex(uint upgradeHash)
        {
            if (upgradeHash == 0u || _profile == null || _profile.purchasedUpgradeLevels == null)
                return -1;

            int count = Mathf.Clamp(_profile.purchasedUpgradeCount, 0, _profile.purchasedUpgradeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                MetaUpgradeLevelRecord record = _profile.purchasedUpgradeLevels[i];
                uint recordHash = record.upgradeHash != 0u ? record.upgradeHash : QuestFlagHashKernel.ComputeStableHash(record.upgradeId);
                if (recordHash == upgradeHash)
                {
                    if (record.upgradeHash == 0u)
                    {
                        record.upgradeHash = recordHash;
                        _profile.purchasedUpgradeLevels[i] = record;
                    }

                    return i;
                }
            }

            return -1;
        }

        private int EnsureUpgradeRecordIndex(uint upgradeHash, string upgradeId)
        {
            if (upgradeHash == 0u || string.IsNullOrWhiteSpace(upgradeId))
                return -1;

            _profile.EnsureCapacity();

            int existingIndex = FindUpgradeRecordIndex(upgradeHash);
            if (existingIndex >= 0)
                return existingIndex;

            int count = Mathf.Clamp(_profile.purchasedUpgradeCount, 0, GlobalProfileData.MaxPurchasedUpgrades);
            if (count >= GlobalProfileData.MaxPurchasedUpgrades)
                return -1;

            _profile.purchasedUpgradeLevels[count] = new MetaUpgradeLevelRecord
            {
                upgradeId = upgradeId,
                upgradeHash = upgradeHash,
                level = 0
            };
            _profile.purchasedUpgradeCount = count + 1;
            return count;
        }

        private int EnsureMarathonRecordIndex(MarathonGoalDefinition definition)
        {
            _profile.EnsureCapacity();

            int count = Mathf.Clamp(_profile.marathonGoalCount, 0, _profile.marathonGoals.Length);
            for (int i = 0; i < count; i++)
            {
                MarathonGoalProgressRecord existing = _profile.marathonGoals[i];
                uint existingHash = existing.goalHash != 0u ? existing.goalHash : QuestFlagHashKernel.ComputeStableHash(existing.goalId);
                if (existingHash != definition.IdHash)
                    continue;

                bool changed = false;
                if (existing.goalHash == 0u)
                {
                    existing.goalHash = existingHash;
                    changed = true;
                }

                if (!ReferenceEquals(existing.title, definition.Title))
                {
                    existing.title = definition.Title;
                    changed = true;
                }

                if (existing.target != definition.Target)
                {
                    existing.target = definition.Target;
                    if (existing.progress > existing.target)
                        existing.progress = existing.target;
                    changed = true;
                }

                if (existing.rewardExplorerPoints != definition.RewardExplorerPoints)
                {
                    existing.rewardExplorerPoints = definition.RewardExplorerPoints;
                    changed = true;
                }

                if (changed)
                    _profile.marathonGoals[i] = existing;

                return i;
            }

            if (count >= GlobalProfileData.MaxMarathonGoals)
                return -1;

            _profile.marathonGoals[count] = new MarathonGoalProgressRecord
            {
                goalId = definition.Id,
                goalHash = definition.IdHash,
                title = definition.Title,
                progress = 0,
                target = definition.Target,
                rewardExplorerPoints = definition.RewardExplorerPoints,
                claimed = false
            };
            _profile.marathonGoalCount = count + 1;
            return count;
        }

        private float ResolveCurrentRunElapsedSeconds()
        {
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null)
                return Mathf.Max(0f, saveManager.CurrentPlayTimeSeconds);

            return Mathf.Max(0f, Time.realtimeSinceStartup);
        }

        private static int ResolveExplorerPointReward(uint achievementHash)
        {
            if (achievementHash == 0u)
                return 10;

            for (int i = 0; i < _achievementRewards.Length; i++)
            {
                if (_achievementRewards[i].IdHash == achievementHash)
                    return _achievementRewards[i].ExplorerPoints;
            }

            return 10;
        }

        private void MarkDirty()
        {
            _dirty = true;
            ProfileChanged?.Invoke();
        }

        private void FlushIfDirty()
        {
            if (!_dirty || _profile == null)
                return;

            if (TryWriteProfile(_profile))
            {
                _dirty = false;
                _flushTimer = 0f;
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[GlobalProfileManager] Failed to flush profile.json. Dirty state retained.");
#endif
        }

        private static bool TryWriteProfile(GlobalProfileData profile)
        {
            if (profile == null)
                return false;

            string path = GetProfileFilePath();
            string tempPath = GetProfileTempFilePath();
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(tempPath))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                profile.EnsureCapacity();
                profile.version = GlobalProfileData.CurrentVersion;
                string json = JsonUtility.ToJson(profile, true);
                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tempPath, path);
                return true;
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[GlobalProfileManager] Failed to write profile file '{path}': {ex.Message}");
#endif
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort temp cleanup only.
                }

                return false;
            }
        }

        private static GlobalProfileData LoadProfileFromDisk()
        {
            string path = GetProfileFilePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new GlobalProfileData();

            try
            {
                string json = File.ReadAllText(path);
                GlobalProfileData profile = string.IsNullOrWhiteSpace(json)
                    ? new GlobalProfileData()
                    : JsonUtility.FromJson<GlobalProfileData>(json);

                if (profile == null)
                    profile = new GlobalProfileData();

                profile.EnsureCapacity();
                profile.version = GlobalProfileData.CurrentVersion;
                return profile;
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[GlobalProfileManager] Failed to load profile file '{path}': {ex.Message}");
#endif
                return new GlobalProfileData();
            }
        }

        private static string GetProfileFilePath()
        {
            return Path.Combine(Application.persistentDataPath, ProfileDirectoryName, ProfileFileName);
        }

        private static string GetProfileTempFilePath()
        {
            return Path.Combine(Application.persistentDataPath, ProfileDirectoryName, ProfileTempFileName);
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registeredToTick = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);

            _registeredToTick = false;
        }

        private void TryRegisterProfileService()
        {
            if (_registeredProfileService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterProfileService(this);
            _registeredProfileService = ReferenceEquals(GlobalRegistry.Profile, this);
        }

        private void TryUnregisterProfileService()
        {
            if (!_registeredProfileService)
                return;

            if (ReferenceEquals(GlobalRegistry.Profile, this))
                GlobalRegistry.UnregisterProfileService(this);

            _registeredProfileService = false;
        }
    }
}
