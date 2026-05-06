using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Meta
{
    /// <summary>
    /// Slow-tick director that derives hidden difficulty pressure from player pain and mastery signals.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6400)]
    [AddComponentMenu("Hecton8/Meta/Dynamic Difficulty Director")]
    public sealed class DynamicDifficultyDirector : MonoBehaviour, ISlowTickable
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
        private HectonEventSubscription _achievementUnlockedSubscription;
        private HectonEventSubscription _advisoryIssuedSubscription;
        private HectonEventSubscription _gameLoadedSubscription;
        private HectonEventSubscription _playerDiedSubscription;
        private bool _registeredToTick;
        private bool _registeredService;
        private int _deathCount;
        private int _deathWriteIndex;
        private int _advisoryCount;
        private int _advisoryWriteIndex;
        private int _achievementCount;
        private int _achievementWriteIndex;
        private int _biomesSinceDamage;
        private float _lastIntegritySample = -1f;

        /// <summary>
        /// Raised after the director produces a new modifier snapshot.
        /// </summary>
        public event Action<DifficultyModifierData> ModifiersChanged;

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
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            TryUnregisterService();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!ResolveOwners())
                return;

            SampleIntegrityDrop();
            EvaluateModifiers();
        }

        private void HandleAchievementUnlocked(AchievementUnlockedEvent achievementUnlockedEvent)
        {
            RegisterRecentEvent(_achievementTimestamps, ref _achievementCount, ref _achievementWriteIndex, ResolveTelemetryTimeSeconds());
        }

        private void HandleAdvisoryIssued(PlayerAdvisoryIssuedEvent advisoryIssuedEvent)
        {
            RegisterRecentEvent(_advisoryTimestamps, ref _advisoryCount, ref _advisoryWriteIndex, ResolveTelemetryTimeSeconds());
        }

        private void HandleBiomeDiscovered(int biomeId)
        {
            _biomesSinceDamage++;
        }

        private void HandleGameLoaded(GameLoadedEvent gameLoadedEvent)
        {
            RebindOwnerSubscriptions();
            ResetTelemetryWindows();
        }

        private void HandlePlayerDied(PlayerDiedEvent playerDiedEvent)
        {
            RegisterRecentEvent(_deathTimestamps, ref _deathCount, ref _deathWriteIndex, ResolveTelemetryTimeSeconds());
            _biomesSinceDamage = 0;
        }

        private void SubscribeToEventBus()
        {
            if (_achievementUnlockedSubscription == null)
                _achievementUnlockedSubscription = HectonEventBus.Subscribe<AchievementUnlockedEvent>(HandleAchievementUnlocked, "meta.difficulty");

            if (_advisoryIssuedSubscription == null)
                _advisoryIssuedSubscription = HectonEventBus.Subscribe<PlayerAdvisoryIssuedEvent>(HandleAdvisoryIssued, "meta.difficulty");

            if (_gameLoadedSubscription == null)
                _gameLoadedSubscription = HectonEventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded, "meta.difficulty");

            if (_playerDiedSubscription == null)
                _playerDiedSubscription = HectonEventBus.Subscribe<PlayerDiedEvent>(HandlePlayerDied, "meta.difficulty");
        }

        private void UnsubscribeFromEventBus()
        {
            _achievementUnlockedSubscription?.Dispose();
            _achievementUnlockedSubscription = null;
            _advisoryIssuedSubscription?.Dispose();
            _advisoryIssuedSubscription = null;
            _gameLoadedSubscription?.Dispose();
            _gameLoadedSubscription = null;
            _playerDiedSubscription?.Dispose();
            _playerDiedSubscription = null;
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwners();

            if (_survivalSystem != null)
                _lastIntegritySample = _survivalSystem.Integrity;
        }

        private void UnbindOwnerSubscriptions()
        {
            if (_discoveryManager != null)
                _discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

            _discoveryManager = null;
        }

        private bool ResolveOwners()
        {
            GameObject playerObject = SceneBootstrap.CurrentPlayerObject;
            if (_survivalSystem == null && playerObject != null)
                playerObject.TryGetComponent(out _survivalSystem);

            HectonDiscoveryManager discoveryManager = GlobalRegistry.Discovery;
            if (discoveryManager != null && !ReferenceEquals(_discoveryManager, discoveryManager))
            {
                _discoveryManager = discoveryManager;
                _discoveryManager.OnBiomeDiscovered += HandleBiomeDiscovered;
            }

            return _survivalSystem != null || discoveryManager != null;
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

                if (Mathf.Abs(CurrentModifiers.DamageMultiplier - nightmareModifiers.DamageMultiplier) < 0.001f &&
                    Mathf.Abs(CurrentModifiers.OxygenDepletionRate - nightmareModifiers.OxygenDepletionRate) < 0.001f &&
                    Mathf.Abs(CurrentModifiers.PredatorAggressionScale - nightmareModifiers.PredatorAggressionScale) < 0.001f)
                {
                    return;
                }

                CurrentModifiers = nightmareModifiers;
                ModifiersChanged?.Invoke(CurrentModifiers);
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

            modifiers.DamageMultiplier = Mathf.Clamp(modifiers.DamageMultiplier, 0.75f, 1.35f);
            modifiers.OxygenDepletionRate = Mathf.Clamp(modifiers.OxygenDepletionRate, 0.75f, 1.2f);
            modifiers.PredatorAggressionScale = Mathf.Clamp(modifiers.PredatorAggressionScale, 0.85f, 1.6f);

            if (Mathf.Abs(CurrentModifiers.DamageMultiplier - modifiers.DamageMultiplier) < 0.001f &&
                Mathf.Abs(CurrentModifiers.OxygenDepletionRate - modifiers.OxygenDepletionRate) < 0.001f &&
                Mathf.Abs(CurrentModifiers.PredatorAggressionScale - modifiers.PredatorAggressionScale) < 0.001f)
            {
                return;
            }

            CurrentModifiers = modifiers;
            ModifiersChanged?.Invoke(CurrentModifiers);
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

        private static float ResolveTelemetryTimeSeconds()
        {
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null)
                return Mathf.Max(0f, saveManager.CurrentPlayTimeSeconds);

            return Mathf.Max(0f, Time.realtimeSinceStartup);
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
    }
}
