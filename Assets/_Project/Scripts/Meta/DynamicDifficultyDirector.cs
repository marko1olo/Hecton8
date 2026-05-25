using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Meta
{
    /// <summary>
    /// Slow-tick director that derives hidden difficulty pressure from player pain and mastery signals.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6400)]
    [AddComponentMenu("Hecton8/Meta/Dynamic Difficulty Director")]
    public sealed class DynamicDifficultyDirector : MonoBehaviour, ISlowTickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const float StruggleWindowSeconds = 1800f;
        private const float AdvisoryWindowSeconds = 900f;
        private const float AchievementMomentumWindowSeconds = 1200f;
        private const float BiomeMasteryDamageEpsilon = 0.01f;
        private const int RecentEventBufferCapacity = 16;

        private readonly float[] _deathTimestamps = new float[RecentEventBufferCapacity]; // COLD ALLOC: float[16] - recent death telemetry window - owner: DynamicDifficultyDirector
        private readonly float[] _advisoryTimestamps = new float[RecentEventBufferCapacity]; // COLD ALLOC: float[16] - recent advisory telemetry window - owner: DynamicDifficultyDirector
        private readonly float[] _achievementTimestamps = new float[RecentEventBufferCapacity]; // COLD ALLOC: float[16] - recent achievement telemetry window - owner: DynamicDifficultyDirector
        private HectonSurvivalSystem _survivalSystem;
        private HectonDiscoveryManager _discoveryManager;
        private bool _registeredToTick;
        private bool _registeredToUpdate;
        private bool _registeredService;
        private bool _registeredHotSwapListener;
        private int _deathCount;
        private int _deathWriteIndex;
        private int _advisoryCount;
        private int _advisoryWriteIndex;
        private int _achievementCount;
        private int _achievementWriteIndex;
        private int _biomesSinceDamage;
        private float _lastIntegritySample = -1f;
        private uint _survivalSignalSourceId;
        private int _lastSurvivalDeathSignalSequence;
        private uint _lastProgressionMetaSequence;
        private uint _lastSessionLifecycleSequence;
        private ISaveService _saveService;

        /// <summary>
        /// Current live difficulty modifiers.
        /// </summary>
        public DifficultyModifierData CurrentModifiers { get; private set; } = DifficultyModifierData.Default;

        /// <summary>
        /// Returns the current modifier snapshot or the neutral baseline when the director is unavailable.
        /// </summary>
        public static DifficultyModifierData Current
        {
            get
            {
                DynamicDifficultyDirector runtime = GlobalRegistry.DynamicDifficulty;
                return runtime != null ? runtime.CurrentModifiers : DifficultyModifierData.Default;
            }
        }

        private void Awake()
        {
            CurrentModifiers = DifficultyModifierData.Default;
        }

        private void OnEnable()
        {
            TryRegisterService();
            TryRegisterHotSwapListener();
            ResolveOwnersCold();
            TryRegisterWithTickManager();
            TryRegisterWithUpdateDispatcher();
            RebindOwnerSubscriptions();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            ResolveOwnersCold();
            TryRegisterWithTickManager();
            TryRegisterWithUpdateDispatcher();
            RebindOwnerSubscriptions();
        }

        private void OnDisable()
        {
            UnbindOwnerSubscriptions();
            UnregisterFromUpdateDispatcher();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            UnbindOwnerSubscriptions();
            UnregisterFromUpdateDispatcher();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            ProcessSessionLifecycleSignals();
            ConsumeSurvivalDeathSignal();
            ProcessProgressionMetaSignals();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!ResolveOwnersHot())
                return;

            SampleIntegrityDrop();
            EvaluateModifiers();
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
                        RegisterRecentEvent(_achievementTimestamps, ref _achievementCount, ref _achievementWriteIndex, ResolveTelemetryTimeSeconds());
                        break;
                    case ProgressionMetaSignal.KindAdvisoryIssued:
                        RegisterRecentEvent(_advisoryTimestamps, ref _advisoryCount, ref _advisoryWriteIndex, ResolveTelemetryTimeSeconds());
                        break;
                    case ProgressionMetaSignal.KindBiomeDiscovered:
                        _biomesSinceDamage++;
                        break;
                }
            }
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

        private void HandleGameLoaded()
        {
            RebindOwnerSubscriptions();
            ResetTelemetryWindows();
        }

        private void HandlePlayerDeath()
        {
            RegisterRecentEvent(_deathTimestamps, ref _deathCount, ref _deathWriteIndex, ResolveTelemetryTimeSeconds());
            _biomesSinceDamage = 0;
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwnersCold();

            if (_survivalSystem != null)
                _lastIntegritySample = _survivalSystem.Integrity;

            RefreshSurvivalSignalBinding();
        }

        private void UnbindOwnerSubscriptions()
        {
            _discoveryManager = null;
            _survivalSignalSourceId = 0u;
            _lastSurvivalDeathSignalSequence = 0;
        }

        private bool ResolveOwnersHot()
        {
            return _survivalSystem != null || _discoveryManager != null;
        }

        private bool ResolveOwnersCold()
        {
            GameObject playerObject = GameBootstrapper.CurrentPlayerObject;
            if (_survivalSystem == null && playerObject != null)
                playerObject.TryGetComponent(out _survivalSystem);

            if (_discoveryManager == null)
                _discoveryManager = GlobalRegistry.Discovery;

            if (_saveService == null)
                _saveService = GlobalRegistry.Save;

            RefreshSurvivalSignalBinding();
            return ResolveOwnersHot();
        }

        private void RefreshSurvivalSignalBinding()
        {
            uint sourceId = ResolveSurvivalSignalSourceId(_survivalSystem);
            if (_survivalSignalSourceId == sourceId)
                return;

            _survivalSignalSourceId = sourceId;
            _lastSurvivalDeathSignalSequence = SurvivalSignalRoute.TryGetLatestDeath(out _, out int sequence)
                ? sequence
                : 0;
        }

        private void ConsumeSurvivalDeathSignal()
        {
            uint sourceId = _survivalSignalSourceId;
            if (sourceId == 0u)
                return;

            if (!SurvivalSignalRoute.TryGetLatestDeath(out SurvivalVitalsChangedSignal signal, out int sequence))
                return;

            if (sequence == _lastSurvivalDeathSignalSequence)
                return;

            _lastSurvivalDeathSignalSequence = sequence;
            if (signal.SourceId != sourceId ||
                (signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)
            {
                return;
            }

            HandlePlayerDeath();
        }

        private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
        }

        private void SampleIntegrityDrop()
        {
            if (_survivalSystem == null)
                return;

            float currentIntegrity = _survivalSystem.Integrity;
            if (_lastIntegritySample < 0f)
            {
                _lastIntegritySample = currentIntegrity;
                return;
            }

            if (currentIntegrity < _lastIntegritySample - BiomeMasteryDamageEpsilon)
                _biomesSinceDamage = 0;

            _lastIntegritySample = currentIntegrity;
        }

        private void EvaluateModifiers()
        {
            if (RunModifierController.IsNightmareModeActive)
            {
                DifficultyModifierData nightmareModifiers = new DifficultyModifierData
                {
                    DamageMultiplier = 2f,
                    OxygenDepletionRate = 1f,
                    PredatorAggressionScale = 1.5f
                };

                if (math.abs(CurrentModifiers.DamageMultiplier - nightmareModifiers.DamageMultiplier) < 0.001f &&
                    math.abs(CurrentModifiers.OxygenDepletionRate - nightmareModifiers.OxygenDepletionRate) < 0.001f &&
                    math.abs(CurrentModifiers.PredatorAggressionScale - nightmareModifiers.PredatorAggressionScale) < 0.001f)
                {
                    return;
                }

                CurrentModifiers = nightmareModifiers;
                return;
            }

            float now = ResolveTelemetryTimeSeconds();
            int deathsRecent = CountRecentEvents(_deathTimestamps, _deathCount, now - StruggleWindowSeconds);
            int advisoriesRecent = CountRecentEvents(_advisoryTimestamps, _advisoryCount, now - AdvisoryWindowSeconds);
            int achievementsRecent = CountRecentEvents(_achievementTimestamps, _achievementCount, now - AchievementMomentumWindowSeconds);

            DifficultyModifierData modifiers = DifficultyModifierData.Default;

            if (deathsRecent >= 5)
            {
                modifiers.DamageMultiplier *= 0.9f;
                modifiers.OxygenDepletionRate *= 0.8f;
                modifiers.PredatorAggressionScale *= 0.9f;
            }
            else if (advisoriesRecent >= 3)
            {
                modifiers.DamageMultiplier *= 0.95f;
                modifiers.OxygenDepletionRate *= 0.9f;
            }

            if (_biomesSinceDamage >= 3 && achievementsRecent >= 2)
            {
                modifiers.DamageMultiplier *= 1.05f;
                modifiers.PredatorAggressionScale *= 1.2f;
            }

            modifiers.PredatorAggressionScale *= EnvironmentalStrainManager.CurrentPredatorAggressionScale;

            modifiers.DamageMultiplier = math.clamp(modifiers.DamageMultiplier, 0.75f, 1.35f);
            modifiers.OxygenDepletionRate = math.clamp(modifiers.OxygenDepletionRate, 0.75f, 1.2f);
            modifiers.PredatorAggressionScale = math.clamp(modifiers.PredatorAggressionScale, 0.85f, 1.6f);

            if (math.abs(CurrentModifiers.DamageMultiplier - modifiers.DamageMultiplier) < 0.001f &&
                math.abs(CurrentModifiers.OxygenDepletionRate - modifiers.OxygenDepletionRate) < 0.001f &&
                math.abs(CurrentModifiers.PredatorAggressionScale - modifiers.PredatorAggressionScale) < 0.001f)
            {
                return;
            }

            CurrentModifiers = modifiers;
        }

        private void ResetTelemetryWindows()
        {
            _deathCount = 0;
            _deathWriteIndex = 0;
            _advisoryCount = 0;
            _advisoryWriteIndex = 0;
            _achievementCount = 0;
            _achievementWriteIndex = 0;
            _biomesSinceDamage = 0;
            _lastIntegritySample = _survivalSystem != null ? _survivalSystem.Integrity : -1f;
            CurrentModifiers = DifficultyModifierData.Default;
        }

        private static bool IsNewerSequence(uint candidate, uint lastProcessed)
        {
            return candidate != 0u && (lastProcessed == 0u || unchecked((int)(candidate - lastProcessed)) > 0);
        }

        private static void RegisterRecentEvent(float[] buffer, ref int count, ref int writeIndex, float timestampSeconds)
        {
            if (buffer == null || buffer.Length == 0)
                return;

            buffer[writeIndex] = timestampSeconds;
            writeIndex = (writeIndex + 1) % buffer.Length;
            if (count < buffer.Length)
                count++;
        }

        private static int CountRecentEvents(float[] buffer, int count, float thresholdSeconds)
        {
            if (buffer == null || count <= 0)
                return 0;

            int recentCount = 0;
            int maxCount = count > buffer.Length ? buffer.Length : count;
            for (int i = 0; i < maxCount; i++)
            {
                if (buffer[i] >= thresholdSeconds)
                    recentCount++;
            }

            return recentCount;
        }

        private float ResolveTelemetryTimeSeconds()
        {
            ISaveService saveService = _saveService;
            if (saveService != null)
                return math.max(0f, saveService.CurrentPlayTimeSeconds);

            return math.max(0f, (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds);
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

        private void TryRegisterService()
        {
            if (_registeredService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterDynamicDifficultyRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.DynamicDifficulty, this);
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterDynamicDifficultyRuntime(this);
            _registeredService = false;
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
            if (_registeredToUpdate || !Application.isPlaying)
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
    }
}
