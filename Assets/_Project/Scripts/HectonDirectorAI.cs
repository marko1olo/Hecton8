using System;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Visor;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Systems.AI
{
    /// <summary>
    /// Scene-facing compatibility owner for the encounter pacing director.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4500)]
    public sealed class HectonDirectorAI : MonoBehaviour, IUpdatable, IEncounterDirectorService
    {
        public static event Action<Vector3> OnRequestSpawnHorde;
        public static event Action<float> OnRequestEquipmentGlitch;
        public static event Action<Vector3> OnRequestRareDiscovery;
        public static event Action<float> OnRequestWeatherShift;
        public static event Action<Vector3> OnRequestMissionTrigger;
        public static event Action<bool> OnPredatorPressureChanged;

        internal static HectonDirectorAI ActiveRuntimeInstance { get; private set; }

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Authoritative player transform. Resolved from bootstrap when left null.")]
        [SerializeField] private Transform playerTransform;
        [Tooltip("Optional explicit gameplay camera. Resolved from the player hierarchy when left null.")]
        [SerializeField] private Camera playerCamera;
        [Tooltip("Player survival system used to feed the director stress inputs.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [Tooltip("Fauna spawn owner. Resolved from the runtime reference utility when left null.")]
        [SerializeField] private FaunaDirector faunaDirector;
        [Tooltip("Optional authored encounter pacing profile. When assigned, spawn thresholds and critical-health suppression become data-driven.")]
        [SerializeField] private EncounterProfile encounterProfile;
        [Tooltip("Optional authored threat token cost table. When assigned, encounter token costs and simultaneous caps become data-driven.")]
        [SerializeField] private ThreatCostTable threatCostTable;

        [Header("â”€â”€ Event Output â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Deterministic offset radius used for non-spawn director event hints.")]
        [SerializeField, Range(8f, 48f)] private float eventOffsetRadius = 25f;

#if UNITY_EDITOR
        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float _debugStressLevel;
        [SerializeField] private float _debugIntensityLevel;
        [SerializeField] private float _debugTokenBudget;
        [SerializeField] private float _debugAverageFrameTimeMs;
        [SerializeField] private int _debugActiveEnemyCount;
        [SerializeField] private string _debugPhaseName;
#endif

        // COLD ALLOC: EncounterDirector[1] â€” dispatcher-driven encounter kernel â€” owner: HectonDirectorAI
        private readonly EncounterDirector _encounterDirector = new EncounterDirector();
        // COLD ALLOC: Plane[6] â€” reusable frustum plane scratch for zero-allocation camera extraction â€” owner: HectonDirectorAI
        private readonly Plane[] _frustumPlaneScratch = new Plane[EncounterDirector.FrustumPlaneCount];
        // COLD ALLOC: FrameTiming[1] â€” reusable frame-timing sample buffer â€” owner: HectonDirectorAI
        private readonly FrameTiming[] _frameTimingScratch = new FrameTiming[1];
        // COLD ALLOC: float[8] â€” rolling frame-time history for shed hysteresis â€” owner: HectonDirectorAI
        private readonly float[] _frameTimeHistory = new float[8];
        private const float SonarStressDecayPerSecond = 0.18f;

        private HectonPlayerMovement _playerMovement;
        private bool _dispatcherRegistered;
        private float _resolveRetryTimer;
        private float _hunterSquadCooldown;
        private int _frameTimeHistoryCount;
        private int _frameTimeHistoryIndex;
        private Vector3 _previousPlayerPosition;
        private bool _hasPreviousPlayerPosition;
        private float _recentSonarStress;
        private float _externalPeakPressure01;
        private float _externalPeakHoldSeconds;
        private const float HunterSquadHostilityThreshold = 0.8f;
        private const float HunterSquadCooldownSeconds = 9f;
        private const int HunterSquadSize = 2;

        /// <summary>
        /// Current normalized director tension score in the legacy 0..100 presentation range.
        /// </summary>
        public float TensionScore => _encounterDirector.StressLevel * 100f;

        internal int CurrentPhaseIndex => (int)_encounterDirector.CurrentPhase;
        internal float CurrentStress01 => _encounterDirector.StressLevel;
        internal float CurrentIntensity01 => _encounterDirector.IntensityLevel;

        /// <summary>
        /// True while the director is in the Relax phase.
        /// </summary>
        public bool IsRelaxPhase => _encounterDirector.CurrentPhase == EncounterPhase.Relax;

        /// <summary>
        /// True while predator pressure is allowed to escalate.
        /// </summary>
        public bool IsPredatorPressureEnabled => _encounterDirector.CurrentPhase != EncounterPhase.Relax;

        /// <summary>
        /// Human-readable current phase name for legacy diagnostics consumers.
        /// </summary>
        public string CurrentPhaseName => _encounterDirector.CurrentPhaseName;

        /// <summary>
        /// True once the encounter director is registered in the global registry.
        /// </summary>
        public bool IsInitialized => ReferenceEquals(GlobalRegistry.EncounterDirector, this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            _encounterDirector.ApplyAuthoring(encounterProfile, threatCostTable);
            ResolveDependencies(force: true);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            GlobalRegistry.RegisterEncounterDirectorService(this);

            if (!_dispatcherRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
                _dispatcherRegistered = true;
            }

            _encounterDirector.Reset();
            _hasPreviousPlayerPosition = false;
            _recentSonarStress = 0f;
            _externalPeakPressure01 = 0f;
            _externalPeakHoldSeconds = 0f;
            _hunterSquadCooldown = 0f;
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
            PublishPredatorPressure(true);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            GlobalRegistry.UnregisterEncounterDirectorService(this);

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _dispatcherRegistered = false;
            }

            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            _recentSonarStress = 0f;
            _externalPeakPressure01 = 0f;
            _externalPeakHoldSeconds = 0f;
            _hunterSquadCooldown = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _encounterDirector.ApplyAuthoring(encounterProfile, threatCostTable);
        }
#endif

        private void OnDestroy()
        {
            GlobalRegistry.UnregisterEncounterDirectorService(this);

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            _encounterDirector.Dispose();
        }

        /// <summary>
        /// Executes one dispatcher step.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            ResolveDependencies(force: false);
            if (playerTransform == null)
                return;

            FrameTimingManager.CaptureFrameTimings();
            float averageFrameTimeMs = UpdateFrameTimeAverage(deltaTime);

            Vector3 playerPosition = playerTransform.position;
            Vector3 playerVelocity = ResolvePlayerVelocity(playerPosition, deltaTime);
            Vector3 playerForward = ResolvePlayerForward();
            float surfaceWorldY = ResolveSurfaceWorldY(playerPosition);
            float healthNormalized = survivalSystem != null ? Mathf.Clamp01(survivalSystem.IntegrityNormalized) : 1f;
            float oxygenNormalized = survivalSystem != null ? Mathf.Clamp01(survivalSystem.OxygenNormalized) : 1f;
            float sonarStress = UpdateSonarStress(deltaTime);
            float internalStress = ResolveInternalStress(healthNormalized, oxygenNormalized, sonarStress);
            float acousticThreatLevel = 0f;
            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null)
                acousticThreatLevel = Mathf.Clamp01(vegetationBridge.GetThreatLevel(playerPosition));
            acousticThreatLevel = Mathf.Max(acousticThreatLevel, sonarStress);
            ApplyExternalPeakPressure(deltaTime, ref internalStress, ref acousticThreatLevel);
            UpdateHunterSquadPressure(deltaTime);

            if (playerCamera != null)
                GeometryUtility.CalculateFrustumPlanes(playerCamera, _frustumPlaneScratch);
            else
                EncounterDirector.FillFallbackFrustumPlanes(playerPosition, playerForward, _frustumPlaneScratch);

            _encounterDirector.CopyFrustumPlanes(_frustumPlaneScratch);

            EncounterFrameContext frameContext = new EncounterFrameContext
            {
                DeltaTime = deltaTime,
                PlayerPosition = playerPosition,
                PlayerVelocity = playerVelocity,
                PlayerForward = playerForward,
                PlayerHealthNormalized = healthNormalized,
                PlayerOxygenNormalized = oxygenNormalized,
                PlayerInternalStress = internalStress,
                AcousticThreatLevel = acousticThreatLevel,
                PlayerDepth = ResolvePlayerDepth(playerPosition, surfaceWorldY),
                AvgFrameTimeMs = averageFrameTimeMs,
                SurfaceWorldY = surfaceWorldY
            };

            _encounterDirector.Advance(frameContext, faunaDirector, this);

#if UNITY_EDITOR
            _debugStressLevel = _encounterDirector.StressLevel;
            _debugIntensityLevel = _encounterDirector.IntensityLevel;
            _debugTokenBudget = _encounterDirector.TokenBudget;
            _debugAverageFrameTimeMs = averageFrameTimeMs;
            _debugActiveEnemyCount = _encounterDirector.ActiveEnemyCount;
            _debugPhaseName = _encounterDirector.CurrentPhaseName;
#endif
        }

        /// <summary>
        /// Forces the next completed encounter tick into the Peak phase.
        /// </summary>
        public void ForcePeak()
        {
            _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);
        }

        /// <summary>
        /// Resets the runtime encounter state.
        /// </summary>
        public void ResetDirector()
        {
            _encounterDirector.RequestReset();
        }

        /// <summary>
        /// Forces the next completed encounter tick into the Relax phase.
        /// </summary>
        public void ForceRelax()
        {
            _encounterDirector.RequestPhaseOverride(EncounterPhase.Relax);
        }

        /// <summary>
        /// Applies external hostility pressure that extends Peak pacing without allocating auxiliary state machines.
        /// </summary>
        public void ApplyExternalPeakPressure(float pressure01, float holdSeconds)
        {
            float clampedPressure = Mathf.Clamp01(pressure01);
            if (clampedPressure <= 0f || holdSeconds <= 0f)
                return;

            _externalPeakPressure01 = Mathf.Max(_externalPeakPressure01, clampedPressure);
            _externalPeakHoldSeconds = Mathf.Max(_externalPeakHoldSeconds, holdSeconds);
        }

        /// <summary>
        /// Legacy predator registration hook retained for compatibility.
        /// </summary>
        /// <param name="collider">Predator collider.</param>
        public static void RegisterPredator(Collider collider)
        {
        }

        /// <summary>
        /// Legacy predator unregistration hook retained for compatibility.
        /// </summary>
        /// <param name="collider">Predator collider.</param>
        public static void UnregisterPredator(Collider collider)
        {
        }

        /// <summary>
        /// Legacy global predator registration clear retained for compatibility.
        /// </summary>
        public static void ClearAllPredatorRegistrations()
        {
        }

        internal void HandleEncounterPhaseChanged(EncounterPhase previousPhase, EncounterPhase newPhase)
        {
            PublishPredatorPressure(newPhase != EncounterPhase.Relax);

            if (playerTransform == null)
                return;

            uint seed = EncounterDirector.BuildDeterministicSeed(playerTransform.position, _encounterDirector.FrameIndex, (int)newPhase, _encounterDirector.ActiveEnemyCount);
            Vector3 eventPosition = ResolveDeterministicOffsetPosition(playerTransform.position, seed, eventOffsetRadius);

            switch (newPhase)
            {
                case EncounterPhase.Peak:
                    SafeInvoke(OnRequestEquipmentGlitch, Mathf.Lerp(0.35f, 0.85f, _encounterDirector.IntensityLevel));
                    SafeInvoke(OnRequestMissionTrigger, eventPosition);
                    break;

                case EncounterPhase.Decay:
                    SafeInvoke(OnRequestWeatherShift, Mathf.Lerp(0.2f, 0.6f, _encounterDirector.StressLevel));
                    break;

                case EncounterPhase.Relax:
                    SafeInvoke(OnRequestRareDiscovery, eventPosition);
                    break;
            }
        }

        internal void HandleThreatSpawned(EncounterThreatClass threatClass, Vector3 spawnPosition)
        {
            if (threatClass == EncounterThreatClass.Swarm)
                SafeInvoke(OnRequestSpawnHorde, spawnPosition);
        }

        private void ResolveDependencies(bool force)
        {
            if (!force && _resolveRetryTimer > 0f)
            {
                _resolveRetryTimer -= Time.unscaledDeltaTime;
                return;
            }

            _resolveRetryTimer = 1f;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (survivalSystem == null && playerTransform != null)
                playerTransform.TryGetComponent(out survivalSystem);

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (faunaDirector == null)
                WorldRuntimeReferenceUtility.TryResolveFaunaDirector(ref faunaDirector);

            if (playerCamera == null && playerTransform != null)
                playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
        }

        private float UpdateFrameTimeAverage(float deltaTime)
        {
            uint timingCount = FrameTimingManager.GetLatestTimings(1u, _frameTimingScratch);
            float sampleMs = timingCount > 0u ? (float)_frameTimingScratch[0].cpuFrameTime : deltaTime * 1000f;
            if (sampleMs <= 0f)
                sampleMs = deltaTime * 1000f;

            _frameTimeHistory[_frameTimeHistoryIndex] = sampleMs;
            _frameTimeHistoryIndex++;
            if (_frameTimeHistoryIndex >= _frameTimeHistory.Length)
                _frameTimeHistoryIndex = 0;

            if (_frameTimeHistoryCount < _frameTimeHistory.Length)
                _frameTimeHistoryCount++;

            float sum = 0f;
            for (int i = 0; i < _frameTimeHistoryCount; i++)
                sum += _frameTimeHistory[i];

            return _frameTimeHistoryCount > 0 ? sum / _frameTimeHistoryCount : sampleMs;
        }

        private Vector3 ResolvePlayerVelocity(Vector3 playerPosition, float deltaTime)
        {
            Vector3 velocity = Vector3.zero;
            if (_hasPreviousPlayerPosition && TryResolveSafeReciprocal(deltaTime, out float inverseDeltaTime))
                velocity = SanitizeFiniteVector((playerPosition - _previousPlayerPosition) * inverseDeltaTime);

            _previousPlayerPosition = playerPosition;
            _hasPreviousPlayerPosition = true;
            return velocity;
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || Mathf.Abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return float.IsFinite(reciprocal);
        }

        private static Vector3 SanitizeFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z)
                ? value
                : Vector3.zero;
        }

        private Vector3 ResolvePlayerForward()
        {
            if (playerCamera != null)
                return playerCamera.transform.forward;

            if (playerTransform != null)
                return playerTransform.forward;

            return Vector3.forward;
        }

        private float ResolveSurfaceWorldY(Vector3 playerPosition)
        {
            if (survivalSystem == null)
                return 0f;

            return playerPosition.y + survivalSystem.Depth;
        }

        private float ResolvePlayerDepth(Vector3 playerPosition, float surfaceWorldY)
        {
            if (survivalSystem != null)
                return Mathf.Max(0f, survivalSystem.Depth);

            return Mathf.Max(0f, surfaceWorldY - playerPosition.y);
        }

        private float ResolveInternalStress(float healthNormalized, float oxygenNormalized, float sonarStress)
        {
            if (survivalSystem == null)
                return Mathf.Clamp01(Mathf.Max(Mathf.Max(1f - healthNormalized, 1f - oxygenNormalized), sonarStress));

            float pressureStress = Mathf.Clamp01(survivalSystem.PressureExposureSeverity01);
            float thermalStress = Mathf.Clamp01(survivalSystem.ThermalStressSeverity01);
            float healthStress = 1f - healthNormalized;
            float oxygenStress = 1f - oxygenNormalized;
            return Mathf.Clamp01(Mathf.Max(Mathf.Max(pressureStress, thermalStress), Mathf.Max(Mathf.Max(healthStress, oxygenStress), sonarStress)));
        }

        private float UpdateSonarStress(float deltaTime)
        {
            if (_recentSonarStress <= 0f)
                return 0f;

            _recentSonarStress = Mathf.MoveTowards(_recentSonarStress, 0f, SonarStressDecayPerSecond * deltaTime);
            return _recentSonarStress;
        }

        private void HandleSonarPingSent(float intensity)
        {
            float clampedIntensity = Mathf.Clamp01(intensity);
            if (clampedIntensity <= 0f)
                return;

            _recentSonarStress = Mathf.Max(_recentSonarStress, clampedIntensity);
        }

        private void ApplyExternalPeakPressure(float deltaTime, ref float internalStress, ref float acousticThreatLevel)
        {
            if (_externalPeakHoldSeconds <= 0f || _externalPeakPressure01 <= 0f)
                return;

            _externalPeakHoldSeconds = Mathf.Max(0f, _externalPeakHoldSeconds - deltaTime);
            internalStress = Mathf.Max(internalStress, _externalPeakPressure01);
            acousticThreatLevel = Mathf.Max(acousticThreatLevel, _externalPeakPressure01);

            if (_encounterDirector.CurrentPhase != EncounterPhase.Peak)
                _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);

            if (_externalPeakHoldSeconds > 0f)
                return;

            _externalPeakPressure01 = 0f;
        }

        private void UpdateHunterSquadPressure(float deltaTime)
        {
            if (_hunterSquadCooldown > 0f)
                _hunterSquadCooldown = Mathf.Max(0f, _hunterSquadCooldown - deltaTime);

            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector == null || ecosystemDirector.BiomeHostility01 < HunterSquadHostilityThreshold)
                return;

            if (_hunterSquadCooldown > 0f)
                return;

            _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);
            _encounterDirector.RequestForcedSquad(EncounterThreatClass.Stalker, HunterSquadSize);
            _hunterSquadCooldown = HunterSquadCooldownSeconds;
        }

        private void PublishPredatorPressure(bool enabled)
        {
            if (faunaDirector != null)
                faunaDirector.SetPredatorPressure(enabled);

            SafeInvoke(OnPredatorPressureChanged, enabled);
        }

        private Vector3 ResolveDeterministicOffsetPosition(Vector3 origin, uint seed, float radius)
        {
            float angle = EncounterDirector.HashToUnit01(seed ^ 0xA511E9B3u) * (Mathf.PI * 2f);
            float distance = Mathf.Lerp(radius * 0.4f, radius, EncounterDirector.HashToUnit01(seed ^ 0x6C8E9CF5u));
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            return origin + offset;
        }

        private static void SafeInvoke<T>(Action<T> action, T arg)
        {
            if (action == null)
                return;

            try
            {
                action.Invoke(arg);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
