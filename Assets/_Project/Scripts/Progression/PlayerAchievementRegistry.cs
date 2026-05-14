using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Modding;
using Hecton8.PDA;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Progression
{
    /// <summary>
    /// Save-backed internal achievement registry used for non-platform meta progression.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Progression/Player Achievement Registry")]
    public sealed class PlayerAchievementRegistry : MonoBehaviour, ITickable, ISlowTickable, ISaveable
    {
        private enum AchievementMetric : byte
        {
            SwamDistance = 0,
            CraftedItems = 1,
            DiscoveredBiomes = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct AchievementDefinition
        {
            public readonly uint IdHash;
            public readonly string Id;
            public readonly string Title;
            public readonly AchievementMetric Metric;
            public readonly float Threshold;

            public AchievementDefinition(string id, string title, AchievementMetric metric, float threshold)
            {
                IdHash = QuestFlagHashKernel.ComputeStableHash(id);
                Id = id;
                Title = title;
                Metric = metric;
                Threshold = threshold;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct AchievementRuntimeDefinition
        {
            public readonly uint IdHash;
            public readonly float Threshold;
            public readonly AchievementMetric Metric;

            public AchievementRuntimeDefinition(AchievementDefinition definition)
            {
                IdHash = definition.IdHash;
                Threshold = definition.Threshold;
                Metric = definition.Metric;
            }
        }

        private const double MinReasonableStepMetersSq = 0.0001d;
        private const double MaxReasonableStepMetersSq = 18d * 18d;
        private const string AchievementLogPrefix = "Achievement unlocked: ";
        private const string AchievementLogbookPrefix = "achievement.";
        private const int AchievementLogbookCategoryHash = unchecked((int)0x7E122BEA);
        private const int TelemetryCooldownFrames = 30;
        private const uint AchievementUnlockedHashOverflowWarningHash = 0x4143484Fu;
        private const uint AchievementPendingQueueOverflowWarningHash = 0x41435051u;
        private const uint AchievementNotificationMissWarningHash = 0x41434E4Du;
        private const uint AchievementContextHash = 0x41434858u;

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

        // COLD ALLOC: AchievementRuntimeDefinition[definitions.Length] - string-free hot metric threshold table - owner: PlayerAchievementRegistry
        private static readonly AchievementRuntimeDefinition[] _runtimeDefinitions = BuildRuntimeDefinitions();

        // COLD ALLOC: uint[32] - unlocked internal achievement hashes - owner: PlayerAchievementRegistry
        private readonly uint[] _unlockedAchievementHashes = new uint[AchievementRegistryDTO.MaxUnlockedAchievements];
        // COLD ALLOC: uint[6] - pre-registered achievement notification hashes - owner: PlayerAchievementRegistry
        private readonly uint[] _achievementNotificationHashes = new uint[_definitions.Length];
        // COLD ALLOC: int[6] - prehashed logbook entry origin hashes - owner: PlayerAchievementRegistry
        private readonly int[] _achievementLogOriginHashes = new int[_definitions.Length];
        // COLD ALLOC: int[6] - prehashed logbook message hashes - owner: PlayerAchievementRegistry
        private readonly int[] _achievementLogMessageHashes = new int[_definitions.Length];
        // COLD ALLOC: int[6] - pending achievement side-effect queue drained in SlowTick - owner: PlayerAchievementRegistry
        private readonly int[] _pendingUnlockDefinitionIndices = new int[_definitions.Length];

        private HectonSurvivalSystem _survivalSystem;
        private HectonDiscoveryManager _discoveryManager;
        private HectonPlayerMovement _playerMovement;
        private HectonEventSubscription _gameLoadedSubscription;
        private bool _registeredToTick;
        private bool _registeredToSlowTick;
        private bool _registeredToSave;
        private bool _achievementPresentationCached;
        private bool _hasAupSample;
        private AbsoluteUniversePosition _lastAupSample;
        private int _unlockedAchievementCount;
        private int _pendingUnlockCount;
        private int _droppedUnlockedHashCount;
        private int _droppedPendingUnlockCount;
        private int _achievementNotificationMissCount;
        private int _lastUnlockedHashOverflowTelemetryFrame;
        private int _lastPendingUnlockOverflowTelemetryFrame;
        private int _lastAchievementNotificationMissTelemetryFrame;
        private uint _lastCraftingCompletedSequence;
        private float _swamDistanceMeters;
        private int _craftedItemCount;
        private int _discoveredBiomeCount;

        /// <inheritdoc />
        public int SavePriority => 207;

        /// <inheritdoc />
        public int LoadPriority => 207;

        /// <summary>
        /// Number of achievement hashes dropped because the fixed save/runtime buffer was full.
        /// </summary>
        public int DroppedUnlockedHashCount => _droppedUnlockedHashCount;

        /// <summary>
        /// Number of achievement side effects dropped because the fixed pending queue was full.
        /// </summary>
        public int DroppedPendingUnlockCount => _droppedPendingUnlockCount;

        /// <summary>
        /// Number of achievement notifications that could not be resolved after cache repair.
        /// </summary>
        public int AchievementNotificationMissCount => _achievementNotificationMissCount;

        private static AchievementRuntimeDefinition[] BuildRuntimeDefinitions()
        {
            AchievementRuntimeDefinition[] runtimeDefinitions = new AchievementRuntimeDefinition[_definitions.Length];
            for (int i = 0; i < _definitions.Length; i++)
                runtimeDefinitions[i] = new AchievementRuntimeDefinition(_definitions[i]);

            return runtimeDefinitions;
        }

        private void Awake()
        {
            CacheAchievementPresentation();
            ResolveOwnersCold();
        }

        private void OnEnable()
        {
            CacheAchievementPresentation();
            ResolveOwnersCold();
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            SyncCraftingSignalBaseline();
            SubscribeToEventBus();
            RebindOwnerSubscriptions();
        }

        private void Start()
        {
            CacheAchievementPresentation();
            ResolveOwnersCold();
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            RebindOwnerSubscriptions();
        }

        private void OnDisable()
        {
            DrainPendingUnlocks();
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
            ProcessCraftingCompletions();

            if (!ResolveOwnersHot() ||
                _survivalSystem == null ||
                !_survivalSystem.IsAlive ||
                !TryResolvePlayerAup(out AbsoluteUniversePosition currentAup))
            {
                _hasAupSample = false;
                return;
            }

            if (!_hasAupSample)
            {
                _lastAupSample = currentAup;
                _hasAupSample = true;
                return;
            }

            AbsoluteUniversePosition previousAup = _lastAupSample;
            double deltaSq = AbsoluteUniversePosition.DistanceSq(in currentAup, in previousAup);
            _lastAupSample = currentAup;

            if (deltaSq <= MinReasonableStepMetersSq || deltaSq > MaxReasonableStepMetersSq)
                return;

            _swamDistanceMeters += ApproximateAupStepMeters(in currentAup, in previousAup);
            EvaluateUnlocks(AchievementMetric.SwamDistance);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            RefreshDiscoveryBindingCold();
            RefreshDiscoveredBiomeTotalCold();
            DrainPendingUnlocks();
        }

        /// <summary>
        /// Returns true when the specified achievement is already unlocked in the current save.
        /// </summary>
        /// <param name="achievementId">Stable achievement identifier.</param>
        public bool IsUnlocked(string achievementId)
        {
            uint achievementHash = QuestFlagHashKernel.ComputeStableHash(achievementId);
            return IsUnlocked(achievementHash);
        }

        public bool IsUnlocked(uint achievementHash)
        {
            return achievementHash != 0u && ContainsUnlockedHash(achievementHash);
        }

        private static float ApproximateAupStepMeters(in AbsoluteUniversePosition currentAup, in AbsoluteUniversePosition previousAup)
        {
            const double MidAxisWeight = 0.375d;
            const double MinAxisWeight = 0.125d;
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double dx = (((double)currentAup.GridX - previousAup.GridX) * cellSize) + currentAup.LocalX - previousAup.LocalX;
            double dy = (((double)currentAup.GridY - previousAup.GridY) * cellSize) + currentAup.LocalY - previousAup.LocalY;
            double dz = (((double)currentAup.GridZ - previousAup.GridZ) * cellSize) + currentAup.LocalZ - previousAup.LocalZ;
            double ax = math.abs(dx);
            double ay = math.abs(dy);
            double az = math.abs(dz);
            double maxAxis = math.max(ax, math.max(ay, az));
            double minAxis = math.min(ax, math.min(ay, az));
            double midAxis = ax + ay + az - maxAxis - minAxis;
            return (float)(maxAxis + (midAxis * MidAxisWeight) + (minAxis * MinAxisWeight));
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.achievements.EnsureCapacity();
            data.achievements.swamDistanceMeters = math.max(0f, _swamDistanceMeters);
            data.achievements.craftedItemCount = math.max(0, _craftedItemCount);
            data.achievements.discoveredBiomeCount = math.max(0, _discoveredBiomeCount);

            int writeCount = 0;
            for (int i = 0; i < _definitions.Length && writeCount < AchievementRegistryDTO.MaxUnlockedAchievements; i++)
            {
                AchievementDefinition definition = _definitions[i];
                if (definition.IdHash == 0u || !ContainsUnlockedHash(definition.IdHash))
                    continue;

                data.achievements.unlockedIds[writeCount] = definition.Id;
                writeCount++;
            }

            data.achievements.unlockedCount = writeCount;
            for (int i = writeCount; i < AchievementRegistryDTO.MaxUnlockedAchievements; i++)
                data.achievements.unlockedIds[i] = string.Empty;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            ClearUnlockedHashes();
            _swamDistanceMeters = 0f;
            _craftedItemCount = 0;
            _discoveredBiomeCount = 0;
            _hasAupSample = false;

            if (data == null)
            {
                SyncCraftingSignalBaseline();
                return;
            }

            _swamDistanceMeters = math.max(0f, data.achievements.swamDistanceMeters);
            _craftedItemCount = math.max(0, data.achievements.craftedItemCount);
            _discoveredBiomeCount = math.max(0, data.achievements.discoveredBiomeCount);

            int unlockedCount = math.clamp(data.achievements.unlockedCount, 0, data.achievements.unlockedIds != null ? data.achievements.unlockedIds.Length : 0);
            for (int i = 0; i < unlockedCount; i++)
            {
                string unlockedId = data.achievements.unlockedIds[i];
                uint unlockedHash = QuestFlagHashKernel.ComputeStableHash(unlockedId);
                if (unlockedHash != 0u)
                    TryAddUnlockedHash(unlockedHash);
            }

            SyncCraftingSignalBaseline();
        }

        private void ProcessCraftingCompletions()
        {
            uint currentSequence = GlobalSignals.LatestCraftingCompletedUnitCount;
            uint delta = currentSequence - _lastCraftingCompletedSequence;
            if (delta == 0u)
                return;

            _lastCraftingCompletedSequence = currentSequence;
            int currentCount = math.max(0, _craftedItemCount);
            uint maxDelta = unchecked((uint)(int.MaxValue - currentCount));
            _craftedItemCount = delta >= maxDelta ? int.MaxValue : currentCount + (int)delta;
            EvaluateUnlocks(AchievementMetric.CraftedItems);
        }

        private void SyncCraftingSignalBaseline()
        {
            _lastCraftingCompletedSequence = GlobalSignals.LatestCraftingCompletedUnitCount;
        }

        private void HandleGameLoaded(GameLoadedEvent gameLoadedEvent)
        {
            RebindOwnerSubscriptions();
            RefreshDiscoveredBiomeTotalCold();
            SyncCraftingSignalBaseline();
        }

        private void HandleBiomeDiscovered(int biomeId)
        {
            _discoveredBiomeCount++;
            EvaluateUnlocks(AchievementMetric.DiscoveredBiomes);
        }

        private void SubscribeToEventBus()
        {
            if (_gameLoadedSubscription == null)
                _gameLoadedSubscription = HectonEventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded, "progression.achievements");
        }

        private void UnsubscribeFromEventBus()
        {
            _gameLoadedSubscription?.Dispose();
            _gameLoadedSubscription = null;
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwnersCold();
        }

        private void UnbindOwnerSubscriptions()
        {
            if (_discoveryManager != null)
                _discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

            _discoveryManager = null;
        }

        private bool ResolveOwnersHot()
        {
            if (_playerMovement == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _playerMovement = playerContext.PlayerMovement;
            }

            return _survivalSystem != null;
        }

        private void ResolveOwnersCold()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_playerMovement == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _playerMovement = playerContext.PlayerMovement;

                if (_playerMovement == null)
                    TryGetComponent(out _playerMovement);
            }

            ResolveOwnersHot();
            RefreshDiscoveryBindingCold();
        }

        private void RefreshDiscoveryBindingCold()
        {
            HectonDiscoveryManager discoveryManager = GlobalRegistry.Discovery;
            if (ReferenceEquals(_discoveryManager, discoveryManager))
                return;

            if (_discoveryManager != null)
                _discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

            _discoveryManager = discoveryManager;
            if (_discoveryManager != null)
                _discoveryManager.OnBiomeDiscovered += HandleBiomeDiscovered;
        }

        private void RefreshDiscoveredBiomeTotalCold()
        {
            if (_discoveryManager == null)
                return;

            int discoveredBiomeCount = _discoveryManager.TotalDiscovered;
            if (discoveredBiomeCount <= _discoveredBiomeCount)
                return;

            _discoveredBiomeCount = discoveredBiomeCount;
            EvaluateUnlocks(AchievementMetric.DiscoveredBiomes);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _playerMovement = playerContext.PlayerMovement;
            }

            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private void EvaluateUnlocks(AchievementMetric metric)
        {
            float currentValue = GetMetricValue(metric);
            for (int i = 0; i < _runtimeDefinitions.Length; i++)
            {
                AchievementRuntimeDefinition definition = _runtimeDefinitions[i];
                if (definition.Metric != metric || currentValue < definition.Threshold)
                    continue;

                QueueUnlock(i, definition.IdHash);
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

        private void QueueUnlock(int definitionIndex, uint achievementHash)
        {
            if (achievementHash == 0u || (uint)definitionIndex >= (uint)_pendingUnlockDefinitionIndices.Length)
                return;

            if (ContainsUnlockedHash(achievementHash))
                return;

            for (int i = 0; i < _pendingUnlockCount; i++)
            {
                if (_pendingUnlockDefinitionIndices[i] == definitionIndex)
                    return;
            }

            if (_pendingUnlockCount >= _pendingUnlockDefinitionIndices.Length)
            {
                ReportPendingUnlockQueueOverflow(achievementHash);
                return;
            }

            if (!TryAddUnlockedHash(achievementHash))
                return;

            _pendingUnlockDefinitionIndices[_pendingUnlockCount++] = definitionIndex;
        }

        private bool ContainsUnlockedHash(uint achievementHash)
        {
            for (int i = 0; i < _unlockedAchievementCount; i++)
            {
                if (_unlockedAchievementHashes[i] == achievementHash)
                    return true;
            }

            return false;
        }

        private bool TryAddUnlockedHash(uint achievementHash)
        {
            if (achievementHash == 0u || ContainsUnlockedHash(achievementHash))
                return false;

            if (_unlockedAchievementCount >= _unlockedAchievementHashes.Length)
            {
                ReportUnlockedHashCapacityOverflow(achievementHash);
                return false;
            }

            _unlockedAchievementHashes[_unlockedAchievementCount++] = achievementHash;
            return true;
        }

        private void ClearUnlockedHashes()
        {
            for (int i = 0; i < _unlockedAchievementCount; i++)
                _unlockedAchievementHashes[i] = 0u;

            _unlockedAchievementCount = 0;
        }

        private void DrainPendingUnlocks()
        {
            int count = _pendingUnlockCount;
            if (count <= 0)
                return;

            _pendingUnlockCount = 0;
            for (int i = 0; i < count; i++)
            {
                int definitionIndex = _pendingUnlockDefinitionIndices[i];
                _pendingUnlockDefinitionIndices[i] = 0;
                if ((uint)definitionIndex >= (uint)_definitions.Length)
                    continue;

                AchievementDefinition definition = _definitions[definitionIndex];
                DispatchUnlockSideEffects(definitionIndex, in definition);
            }
        }

        private void DispatchUnlockSideEffects(int definitionIndex, in AchievementDefinition definition)
        {
            TryPushAchievementNotification(definitionIndex, definition.IdHash);

            IPDALogbookService logbookManager = GlobalRegistry.PDALogbook;
            if (logbookManager != null && (uint)definitionIndex < (uint)_achievementLogOriginHashes.Length)
            {
                int originHash = _achievementLogOriginHashes[definitionIndex];
                int messageHash = _achievementLogMessageHashes[definitionIndex];
                if (originHash != 0 && messageHash != 0)
                    logbookManager.TryAppendEntry(originHash, AchievementLogbookCategoryHash, messageHash);
            }

            HectonEventBus.Publish(new AchievementUnlockedEvent(definition.IdHash, definition.Id, definition.Title));
        }

        private void TryPushAchievementNotification(int definitionIndex, uint achievementHash)
        {
            if ((uint)definitionIndex >= (uint)_achievementNotificationHashes.Length)
                return;

            uint notificationHash = _achievementNotificationHashes[definitionIndex];
            if (notificationHash != 0u && NotificationEvents.TryResolveMessage(notificationHash, out _))
            {
                NotificationEvents.PushRegisteredInfo(notificationHash);
                return;
            }

            RefreshAchievementPresentation();
            notificationHash = _achievementNotificationHashes[definitionIndex];
            if (notificationHash != 0u && NotificationEvents.TryResolveMessage(notificationHash, out _))
            {
                NotificationEvents.PushRegisteredInfo(notificationHash);
                return;
            }

            ReportAchievementNotificationMiss(achievementHash);
        }

        private void CacheAchievementPresentation()
        {
            if (_achievementPresentationCached)
                return;

            for (int i = 0; i < _definitions.Length; i++)
            {
                AchievementDefinition definition = _definitions[i];
                string message = AchievementLogPrefix + definition.Title;
                _achievementLogOriginHashes[i] = LocHash.Compute(AchievementLogbookPrefix + definition.Id);
                _achievementLogMessageHashes[i] = LocHash.Compute(message);
                _achievementNotificationHashes[i] = NotificationEvents.RegisterMessage(message);
            }

            _achievementPresentationCached = true;
        }

        private void RefreshAchievementPresentation()
        {
            for (int i = 0; i < _achievementNotificationHashes.Length; i++)
                _achievementNotificationHashes[i] = 0u;

            for (int i = 0; i < _achievementLogOriginHashes.Length; i++)
                _achievementLogOriginHashes[i] = 0;

            for (int i = 0; i < _achievementLogMessageHashes.Length; i++)
                _achievementLogMessageHashes[i] = 0;

            _achievementPresentationCached = false;
            CacheAchievementPresentation();
        }

        private void ReportUnlockedHashCapacityOverflow(uint achievementHash)
        {
            _droppedUnlockedHashCount++;

            int frame = Time.frameCount;
            if (frame < _lastUnlockedHashOverflowTelemetryFrame)
                return;

            _lastUnlockedHashOverflowTelemetryFrame = frame + TelemetryCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                AchievementUnlockedHashOverflowWarningHash,
                achievementHash != 0u ? achievementHash : AchievementContextHash,
                _droppedUnlockedHashCount);
        }

        private void ReportPendingUnlockQueueOverflow(uint achievementHash)
        {
            _droppedPendingUnlockCount++;

            int frame = Time.frameCount;
            if (frame < _lastPendingUnlockOverflowTelemetryFrame)
                return;

            _lastPendingUnlockOverflowTelemetryFrame = frame + TelemetryCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                AchievementPendingQueueOverflowWarningHash,
                achievementHash != 0u ? achievementHash : AchievementContextHash,
                _droppedPendingUnlockCount);
        }

        private void ReportAchievementNotificationMiss(uint achievementHash)
        {
            _achievementNotificationMissCount++;

            int frame = Time.frameCount;
            if (frame < _lastAchievementNotificationMissTelemetryFrame)
                return;

            _lastAchievementNotificationMissTelemetryFrame = frame + TelemetryCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                AchievementNotificationMissWarningHash,
                achievementHash != 0u ? achievementHash : AchievementContextHash,
                _achievementNotificationMissCount);
        }

        private void TryRegisterWithTickManager()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredToTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
                _registeredToTick = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredToSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
                _registeredToSlowTick = GlobalRegistry.SlowTickables.Contains(this);
            }
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredToTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredToTick = false;
            }

            if (_registeredToSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredToSlowTick = false;
            }
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
