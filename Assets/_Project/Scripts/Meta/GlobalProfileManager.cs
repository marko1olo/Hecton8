using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Items;
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
    public sealed class GlobalProfileManager : MonoBehaviour, ISlowTickable, IUpdatable, IProfileService, IGlobalRegistryHotSwapListener
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
            public readonly string Id;
            public readonly string Title;
            public readonly int ExplorerPoints;

            public AchievementRewardDefinition(string id, string title, int explorerPoints)
            {
                IdHash = QuestFlagHashKernel.ComputeStableHash(id);
                Id = id;
                Title = title;
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
            new AchievementRewardDefinition("achievement.swim.250", "FIELD DIVER", 10),
            new AchievementRewardDefinition("achievement.swim.1000", "ABYSS RUNNER", 25),
            new AchievementRewardDefinition("achievement.craft.10", "FABRICATOR HAND", 10),
            new AchievementRewardDefinition("achievement.craft.50", "SYSTEMS ENGINEER", 25),
            new AchievementRewardDefinition("achievement.biome.5", "CHARTED WATER", 15),
            new AchievementRewardDefinition("achievement.biome.12", "WORLD MEMORY", 30),
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
        private IPlayerRuntimeContext _playerRuntimeContext;
        private GlobalProfileData _profile = new GlobalProfileData();
        private bool _registeredToTick;
        private bool _registeredToUpdate;
        private bool _registeredProfileService;
        private bool _registeredHotSwapListener;
        private bool _runtimeOwnerAborted;
        private bool _dirty;
        private float _flushTimer;
        private float _nextLongestLifeRecordThreshold = LongestLifeRecordStepSeconds;
        private uint _lastCraftingCompletedSequence;
        private uint _survivalSignalSourceId;
        private int _lastSurvivalDeathSignalSequence;
        private uint _lastProgressionMetaSequence;
        private uint _lastItemLifecycleSequence;
        private uint _lastSessionLifecycleSequence;
        private int _currentRunBiomeDiscoveries;
        private ISaveService _saveService;

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
            profile = LoadProfileFromDiskCold();
            return profile != null;
        }

        private void Awake()
        {
            if (!TryRegisterProfileService())
                return;

            LoadProfile();
        }

        private void OnEnable()
        {
            if (!TryRegisterProfileService())
                return;

            TryRegisterHotSwapListener();
            CacheRegistryOwnersCold();
            ResolveOwnersCold();
            TryRegisterWithTickManager();
            TryRegisterWithUpdateDispatcher();
            SyncCraftingSignalBaseline();
            RebindOwnerSubscriptions();
        }

        private void Start()
        {
            if (!TryRegisterProfileService())
                return;

            TryRegisterHotSwapListener();
            CacheRegistryOwnersCold();
            ResolveOwnersCold();
            TryRegisterWithTickManager();
            TryRegisterWithUpdateDispatcher();
            RebindOwnerSubscriptions();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            FlushCurrentRunRecords();
            FlushIfDirtyCold();
            UnbindOwnerSubscriptions();
            ClearRuntimeOwnerCaches();
            UnregisterFromUpdateDispatcher();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            TryUnregisterProfileService();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            FlushCurrentRunRecords();
            FlushIfDirtyCold();
            UnbindOwnerSubscriptions();
            ClearRuntimeOwnerCaches();
            UnregisterFromUpdateDispatcher();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            TryUnregisterProfileService();
        }

        private void OnApplicationQuit()
        {
            if (_runtimeOwnerAborted)
                return;

            FlushCurrentRunRecords();
            FlushIfDirtyCold();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (_runtimeOwnerAborted)
                return;

            if (!pauseStatus)
                return;

            FlushCurrentRunRecords();
            FlushIfDirtyCold();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                return;

            FlushCurrentRunRecords();
            FlushIfDirtyCold();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            ProcessCraftingCompletions();

            if (!ResolveOwnersHot())
                return;

            TrackCurrentRunRecords();

            if (_dirty)
                _flushTimer = Mathf.Min(_flushTimer + 0.5f, FlushIntervalSeconds);
        }

        public void Tick(float deltaTime)
        {
            ProcessSessionLifecycleSignals();
            ConsumeSurvivalDeathSignal();
            ProcessProgressionMetaSignals();
            ProcessItemLifecycleSignals();
        }

        private void ProcessSessionLifecycleSignals()
        {
            global::System.ReadOnlySpan<SessionLifecycleSignal> signals = SignalBus<SessionLifecycleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SessionLifecycleSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastSessionLifecycleSequence))
                    continue;

                _lastSessionLifecycleSequence = signal.Sequence;
                if (signal.Kind == SessionLifecycleSignal.KindGameLoaded)
                    HandleGameLoaded();
            }
        }

        private void ProcessProgressionMetaSignals()
        {
            global::System.ReadOnlySpan<ProgressionMetaSignal> signals = SignalBus<ProgressionMetaSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ProgressionMetaSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastProgressionMetaSequence))
                    continue;

                _lastProgressionMetaSequence = signal.Sequence;
                switch (signal.Kind)
                {
                    case ProgressionMetaSignal.KindAchievementUnlocked:
                        ProcessAchievementUnlocked(signal.EventHash);
                        break;
                    case ProgressionMetaSignal.KindBiomeDiscovered:
                        HandleBiomeDiscovered(unchecked((int)signal.ContextHash));
                        break;
                }
            }
        }

        private void ProcessAchievementUnlocked(uint achievementHash)
        {
            if (achievementHash == 0u)
                return;

            TryResolveAchievementReward(achievementHash, out AchievementRewardDefinition definition);
            string achievementId = definition.Id ?? string.Empty;
            string title = definition.Title ?? string.Empty;

            float unlockTimeSeconds = ResolveCurrentRunElapsedSeconds();
            UpdateFastestAchievementRecord(achievementHash, achievementId, title, unlockTimeSeconds);

            if (_globalUnlockedAchievementHashes.Add(achievementHash))
            {
                _profile.explorerPoints += ResolveExplorerPointReward(achievementHash);
                StoreUnlockedAchievementId(achievementId);
                MarkDirty();
            }
        }

        private void HandleGameLoaded()
        {
            RebindOwnerSubscriptions();

            HectonDiscoveryManager discoveryManager = _discoveryManager;
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

        private void ApplyDeathRecord(in SurvivalDeathRecord deathRecord)
        {
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

        private void ProcessCraftingCompletions()
        {
            uint currentSequence = CraftingSignalRoute.LatestCompletedUnitCount;
            uint delta = currentSequence - _lastCraftingCompletedSequence;
            if (delta == 0u)
                return;

            _lastCraftingCompletedSequence = currentSequence;
            int amount = delta > (uint)int.MaxValue ? int.MaxValue : (int)delta;
            AdvanceMarathonProgress(MarathonMetric.CraftedItems, amount);
        }

        private void ProcessItemLifecycleSignals()
        {
            global::System.ReadOnlySpan<ItemLifecycleSignal> signals = SignalBus<ItemLifecycleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ItemLifecycleSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastItemLifecycleSequence))
                    continue;

                _lastItemLifecycleSequence = signal.Sequence;
                int quantity = Mathf.Max(0, signal.Quantity);
                if (quantity <= 0)
                    continue;

                if (signal.Action == ItemLifecycleSignal.ActionCollected &&
                    signal.ResourceFamily == (byte)ResourceFamily.StructuralMetal)
                {
                    AdvanceMarathonProgress(MarathonMetric.StructuralMetalCollected, quantity);
                }
                else if (signal.Action == ItemLifecycleSignal.ActionRecycled)
                {
                    AdvanceMarathonProgress(MarathonMetric.RecycledItems, quantity);
                }
            }
        }

        private static bool IsNewerSequence(uint candidate, uint lastProcessed)
        {
            return candidate != 0u && (lastProcessed == 0u || unchecked((int)(candidate - lastProcessed)) > 0);
        }

        private void SyncCraftingSignalBaseline()
        {
            _lastCraftingCompletedSequence = CraftingSignalRoute.LatestCompletedUnitCount;
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwnersCold();

            if (_survivalSystem != null)
                _nextLongestLifeRecordThreshold = Mathf.Max(_profile.longestLifeWithoutDeathSeconds + LongestLifeRecordStepSeconds, LongestLifeRecordStepSeconds);

            RefreshSurvivalSignalBinding();
        }

        private void UnbindOwnerSubscriptions()
        {
            _survivalSystem = null;
            _discoveryManager = null;
            _survivalSignalSourceId = 0u;
            _lastSurvivalDeathSignalSequence = 0;
        }

        private void ClearRuntimeOwnerCaches()
        {
            _playerRuntimeContext = null;
            _survivalSystem = null;
            _discoveryManager = null;
            _survivalSignalSourceId = 0u;
            _lastSurvivalDeathSignalSequence = 0;
        }

        private bool ResolveOwnersHot()
        {
            if (_discoveryManager != null)
                _currentRunBiomeDiscoveries = _discoveryManager.TotalDiscovered;

            return _survivalSystem != null || _discoveryManager != null;
        }

        private bool ResolveOwnersCold()
        {
            IPlayerRuntimeContext playerRuntime = _playerRuntimeContext;
            if (_survivalSystem == null && playerRuntime != null)
                _survivalSystem = playerRuntime.SurvivalSystem;

            if (_discoveryManager == null)
                _discoveryManager = GlobalRegistry.Discovery;

            if (!IsSaveServiceUsable(_saveService))
                _saveService = GlobalRegistry.Save;

            RefreshSurvivalSignalBinding();
            return ResolveOwnersHot();
        }

        private void CacheRegistryOwnersCold()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
            if (_discoveryManager == null)
                _discoveryManager = GlobalRegistry.Discovery;

            if (!IsSaveServiceUsable(_saveService))
                _saveService = GlobalRegistry.Save;
        }

        private void RefreshSurvivalSignalBinding()
        {
            uint sourceId = ResolveSurvivalSignalSourceId(_survivalSystem);
            if (_survivalSignalSourceId == sourceId)
                return;

            _survivalSignalSourceId = sourceId;
            _lastSurvivalDeathSignalSequence = sourceId != 0u &&
                                               SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out _, out int sequence)
                ? sequence
                : 0;
        }

        private void ConsumeSurvivalDeathSignal()
        {
            uint sourceId = _survivalSignalSourceId;
            if (sourceId == 0u)
                return;

            if (!SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out SurvivalVitalsChangedSignal signal, out int sequence))
                return;

            if (sequence == _lastSurvivalDeathSignalSequence)
                return;

            _lastSurvivalDeathSignalSequence = sequence;
            if (signal.SourceId != sourceId ||
                (signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u ||
                _survivalSystem == null ||
                !_survivalSystem.TryGetLastDeathRecord(out SurvivalDeathRecord deathRecord))
            {
                return;
            }

            ApplyDeathRecord(in deathRecord);
        }

        private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
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
            _profile = LoadProfileFromDiskCold();
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
            if (string.IsNullOrWhiteSpace(achievementId))
                return;

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
            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (IsSaveServiceUsable(saveService))
                return Mathf.Max(0f, saveService.CurrentPlayTimeSeconds);

            return Mathf.Max(0f, (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds);
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DiscoveryRuntime:
                    _discoveryManager = currentService as HectonDiscoveryManager;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    _saveService = currentService as ISaveService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    _survivalSystem = _playerRuntimeContext != null ? _playerRuntimeContext.SurvivalSystem : null;
                    RefreshSurvivalSignalBinding();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                    {
                        UnregisterFromUpdateDispatcher();
                        UnregisterFromTickManager();
                    }
                    else
                    {
                        TryRegisterWithTickManager();
                        TryRegisterWithUpdateDispatcher();
                    }
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private static int ResolveExplorerPointReward(uint achievementHash)
        {
            if (achievementHash == 0u)
                return 10;

            return TryResolveAchievementReward(achievementHash, out AchievementRewardDefinition definition)
                ? definition.ExplorerPoints
                : 10;
        }

        private static bool TryResolveAchievementReward(uint achievementHash, out AchievementRewardDefinition definition)
        {
            for (int i = 0; i < _achievementRewards.Length; i++)
            {
                if (_achievementRewards[i].IdHash == achievementHash)
                {
                    definition = _achievementRewards[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private void MarkDirty()
        {
            _dirty = true;
            ProfileChanged?.Invoke();
        }

        private void FlushIfDirtyCold()
        {
            if (!_dirty || _profile == null)
                return;

            if (TryWriteProfileCold(_profile))
            {
                _dirty = false;
                _flushTimer = 0f;
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[GlobalProfileManager] Failed to flush profile.json. Dirty state retained.");
#endif
        }

        private static bool TryWriteProfileCold(GlobalProfileData profile)
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
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json); // COLD ALLOC: profile.json UTF-8 write buffer - infrequent meta-profile flush - owner: GlobalProfileManager
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(jsonBytes, 0, jsonBytes.Length);
                    stream.Flush(true);
                }
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);

                if (!AsyncWriteManager.TryGetFileLength(tempPath, out long tempProfileBytes, out string tempLengthError))
                    throw new IOException(string.IsNullOrEmpty(tempLengthError) ? "Global profile temp file length could not be resolved before promotion." : tempLengthError);

                if (tempProfileBytes != jsonBytes.LongLength)
                    throw new IOException("Global profile temp file length changed before promotion.");

                if (!AsyncWriteManager.FlushCriticalSavePath(tempPath, tempProfileBytes, out string tempFlushError))
                    throw new IOException(string.IsNullOrEmpty(tempFlushError) ? "Global profile temp critical flush failed before promotion." : tempFlushError);

                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                AsyncWriteManager.InvalidateCachedReadWindows(path);
                if (File.Exists(path))
                    File.Replace(tempPath, path, null, true);
                else
                    File.Move(tempPath, path);
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                AsyncWriteManager.InvalidateCachedReadWindows(path);

                if (!AsyncWriteManager.TryGetFileLength(path, out long promotedProfileBytes, out string lengthError))
                    throw new IOException(string.IsNullOrEmpty(lengthError) ? "Global profile file length could not be resolved after promotion." : lengthError);

                if (promotedProfileBytes != jsonBytes.LongLength)
                    throw new IOException("Global profile file length changed during promotion.");

                if (!AsyncWriteManager.FlushCriticalSavePath(path, promotedProfileBytes, out string flushError))
                    throw new IOException(string.IsNullOrEmpty(flushError) ? "Global profile critical flush failed after promotion." : flushError);

                return true;
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning($"[GlobalProfileManager] Failed to write profile file '{path}': {ex.Message}");
#endif
                try
                {
                    DeleteProfileTempBestEffort(tempPath);
                }
                catch
                {
                    // Best-effort temp cleanup only.
                }

                return false;
            }
        }

        private static void DeleteProfileTempBestEffort(string tempPath)
        {
            if (string.IsNullOrEmpty(tempPath))
                return;

            AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
            }
        }

        private static GlobalProfileData LoadProfileFromDiskCold()
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
                Hecton8.Core.H8Debug.LogWarning($"[GlobalProfileManager] Failed to load profile file '{path}': {ex.Message}");
#endif
                return new GlobalProfileData();
            }
        }

        private static string GetProfileFilePath()
        {
            return HectonPersistentPathPolicy.CombineFile(Path.Combine(ProfileDirectoryName, ProfileFileName));
        }

        private static string GetProfileTempFilePath()
        {
            return HectonPersistentPathPolicy.CombineFile(Path.Combine(ProfileDirectoryName, ProfileTempFileName));
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);

            _registeredToTick = false;
        }

        private void TryRegisterWithUpdateDispatcher()
        {
            if (_registeredToUpdate || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void UnregisterFromUpdateDispatcher()
        {
            if (!_registeredToUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredToUpdate = false;
        }

        private bool TryRegisterProfileService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_registeredProfileService || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IProfileService registered = GlobalRegistry.Profile;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                GlobalProfileManager staleManager = registered as GlobalProfileManager;
                if (ReferenceEquals(staleManager, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return false;
                }

                GlobalRegistry.UnregisterProfileService(registered);
                staleManager._registeredProfileService = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterProfileService(this);
            _registeredProfileService = ReferenceEquals(GlobalRegistry.Profile, this);
            _runtimeOwnerAborted = !_registeredProfileService;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return _registeredProfileService;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            IProfileService registered = GlobalRegistry.Profile;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsProfileRuntimeUsable(registered))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            GlobalProfileManager staleManager = registered as GlobalProfileManager;
            if (!ReferenceEquals(staleManager, null))
            {
                GlobalRegistry.UnregisterProfileService(registered);
                staleManager._registeredProfileService = false;
            }

            return false;
        }

        private static bool IsProfileRuntimeUsable(IProfileService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            GlobalProfileManager manager = service as GlobalProfileManager;
            return ReferenceEquals(manager, null) ||
                   (manager != null &&
                    manager._registeredProfileService &&
                    manager.isActiveAndEnabled &&
                    !manager._runtimeOwnerAborted);
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
