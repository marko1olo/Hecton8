using Hecton8.AI;
using Hecton8.Core;
using Hecton8.PDA;
using Hecton8.SaveSystem;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Slow-tick owner for pollution-driven infection zones and infected-fauna spawn configuration.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6230)]
    [AddComponentMenu("Hecton8/Ecosystem/Ecosystem Health Director")]
    public sealed class EcosystemHealthDirector : MonoBehaviour, ISlowTickable, ISaveable, IGlobalRegistryHotSwapListener
    {
        private const float InfectionActivationDebt = 20f;
        private const float InfectionFullDebt = 140f;

        // COLD ALLOC: long[64] - fixed infection-zone registry; avoids managed hash buckets in slow-tick/read paths - owner: EcosystemHealthDirector
        private readonly long[] _infectedChunkKeys = new long[EcosystemStateDTO.MaxInfectedZones];
        // COLD ALLOC: float[64] - severity mirror aligned by index with _infectedChunkKeys - owner: EcosystemHealthDirector
        private readonly float[] _infectedSeverities = new float[EcosystemStateDTO.MaxInfectedZones];
        // COLD ALLOC: long[16384] - explored PDA chunk copy buffer for infection-zone selection - owner: EcosystemHealthDirector
        private readonly long[] _exploredChunkBuffer = new long[ExplorationMapDTO.MaxExploredChunks];
        private int _infectedZoneCount;
        private bool _registeredToTick;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _duplicateServiceSuppressed;
        private IPlayerExplorationChunkReadModel _playerExploration;
        private IFaunaWorldSeedReadModel _faunaGenetics;
        private IEnvironmentalStrainReadModel _environmentalStrain;
        private ISaveService _saveService;
        private static EcosystemHealthDirector s_activeRuntime;

        /// <inheritdoc />
        public int SavePriority => 42;

        /// <inheritdoc />
        public int LoadPriority => 42;

        private void Awake()
        {
            TryAbortForUsableExistingRuntime();
        }

        private void OnEnable()
        {
            if (_duplicateServiceSuppressed)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (_duplicateServiceSuppressed)
                return;

            CacheRuntimeDependencies();
            CacheSaveServiceCold();
            TryRegisterHotSwapListener();
            TryRegisterToTickManager();
            _saveService?.Register(this);
        }

        private void Start()
        {
            if (_duplicateServiceSuppressed)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            CacheRuntimeDependencies();
            TryRegisterHotSwapListener();
            TryRegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            _saveService?.Unregister(this);
            _saveService = null;
            ClearRuntimeDependencies();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            _saveService?.Unregister(this);
            _saveService = null;
            ClearRuntimeDependencies();
            TryUnregisterService();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            float infectionPressure = SaturateFinite01(ResolveInfectionPressure01());
            int targetZoneCount = Mathf.Clamp(
                Mathf.CeilToInt(infectionPressure * EcosystemStateDTO.MaxInfectedZones * 0.25f),
                0,
                EcosystemStateDTO.MaxInfectedZones);

            if (targetZoneCount <= 0)
            {
                ClearAllZones();
                return;
            }

            TrimZones(targetZoneCount);
            EnsureZoneBudget(targetZoneCount, infectionPressure);
        }

        /// <summary>
        /// Returns true when the specified world chunk is currently infected.
        /// </summary>
        public bool IsChunkInfected(WorldChunkCoordinate chunkCoordinate)
        {
            return FindZoneIndex(PackChunkKey(chunkCoordinate)) >= 0;
        }

        /// <summary>
        /// Applies infection state to one freshly spawned fauna instance.
        /// </summary>
        public void ConfigureSpawnedFauna(FaunaBrain faunaBrain, CreatureArchetypeData archetype, WorldChunkCoordinate chunkCoordinate)
        {
            if (faunaBrain == null)
                return;

            long chunkKey = PackChunkKey(chunkCoordinate);
            bool infected = TryGetZoneSeverity(chunkKey, out float severity);

            faunaBrain.SetInfectedState(infected, severity);
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.ecosystemState.EnsureCapacity();

            int writeIndex = 0;
            int count = Mathf.Clamp(_infectedZoneCount, 0, EcosystemStateDTO.MaxInfectedZones);
            for (int i = 0; i < count && writeIndex < EcosystemStateDTO.MaxInfectedZones; i++)
            {
                data.ecosystemState.infectedChunkKeys[writeIndex] = _infectedChunkKeys[i];
                data.ecosystemState.infectedSeverities[writeIndex] = SaturateFinite01(_infectedSeverities[i]);
                writeIndex++;
            }

            data.ecosystemState.infectedZoneCount = writeIndex;
            for (int i = writeIndex; i < EcosystemStateDTO.MaxInfectedZones; i++)
            {
                data.ecosystemState.infectedChunkKeys[i] = 0L;
                data.ecosystemState.infectedSeverities[i] = 0f;
            }
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            ClearAllZones();

            if (data == null)
                return;

            EcosystemStateDTO dto = data.ecosystemState;
            int sourceCapacity = dto.infectedChunkKeys != null
                ? Mathf.Min(dto.infectedChunkKeys.Length, EcosystemStateDTO.MaxInfectedZones)
                : 0;
            int count = Mathf.Clamp(dto.infectedZoneCount, 0, sourceCapacity);
            for (int i = 0; i < count; i++)
            {
                long chunkKey = dto.infectedChunkKeys[i];
                float severity = dto.infectedSeverities != null && i < dto.infectedSeverities.Length
                    ? SaturateFinite01(dto.infectedSeverities[i])
                    : 0f;
                TryUpsertZone(chunkKey, severity);
            }
        }

        private void EnsureZoneBudget(int targetZoneCount, float infectionPressure)
        {
            int target = Mathf.Clamp(targetZoneCount, 0, EcosystemStateDTO.MaxInfectedZones);
            float safeInfectionPressure = SaturateFinite01(infectionPressure);
            if (_infectedZoneCount >= target)
                return;

            IPlayerExplorationChunkReadModel tracker = _playerExploration;
            if (tracker == null)
                return;

            int exploredCount = tracker.CopyExploredChunkKeys(_exploredChunkBuffer);
            exploredCount = Mathf.Clamp(exploredCount, 0, _exploredChunkBuffer.Length);
            if (exploredCount <= 0)
                return;

            IFaunaWorldSeedReadModel geneticsManager = _faunaGenetics;
            int seed = geneticsManager != null ? geneticsManager.WorldSeed : 0;
            int dayIndex;
            float dayTimeHours;
            float playTimeSeconds;
            PDAClockUtility.CaptureStamp(out dayIndex, out dayTimeHours, out playTimeSeconds);

            bool playTimeFinite = !float.IsNaN(playTimeSeconds) && !float.IsInfinity(playTimeSeconds);
            int playTimeBucket = playTimeFinite ? Mathf.FloorToInt(Mathf.Clamp(playTimeSeconds, 0f, 2147483000f)) : 0;
            uint startSeed = unchecked((uint)seed ^ (uint)dayIndex ^ (uint)playTimeBucket);
            int startIndex = (int)(startSeed % (uint)exploredCount);
            for (int search = 0; search < exploredCount && _infectedZoneCount < target; search++)
            {
                int index = startIndex + search;
                if (index >= exploredCount)
                    index -= exploredCount;

                long chunkKey = _exploredChunkBuffer[index];
                if (FindZoneIndex(chunkKey) >= 0)
                    continue;

                TryUpsertZone(chunkKey, safeInfectionPressure);
            }

            for (int i = 0; i < exploredCount; i++)
            {
                long chunkKey = _exploredChunkBuffer[i];
                int zoneIndex = FindZoneIndex(chunkKey);
                if (zoneIndex >= 0)
                    _infectedSeverities[zoneIndex] = safeInfectionPressure;
            }
        }

        private void TrimZones(int targetZoneCount)
        {
            if (_infectedZoneCount <= targetZoneCount)
                return;

            int target = Mathf.Clamp(targetZoneCount, 0, EcosystemStateDTO.MaxInfectedZones);
            while (_infectedZoneCount > target)
            {
                _infectedZoneCount--;
                _infectedChunkKeys[_infectedZoneCount] = 0L;
                _infectedSeverities[_infectedZoneCount] = 0f;
            }
        }

        private void ClearAllZones()
        {
            if (_infectedZoneCount == 0)
                return;

            for (int i = 0; i < _infectedZoneCount; i++)
            {
                _infectedChunkKeys[i] = 0L;
                _infectedSeverities[i] = 0f;
            }

            _infectedZoneCount = 0;
        }

        private bool TryGetZoneSeverity(long chunkKey, out float severity)
        {
            int index = FindZoneIndex(chunkKey);
            if (index < 0)
            {
                severity = 0f;
                return false;
            }

            severity = SaturateFinite01(_infectedSeverities[index]);
            return true;
        }

        private bool TryUpsertZone(long chunkKey, float severity)
        {
            int index = FindZoneIndex(chunkKey);
            float safeSeverity = SaturateFinite01(severity);
            if (index >= 0)
            {
                _infectedSeverities[index] = safeSeverity;
                return true;
            }

            if (_infectedZoneCount >= EcosystemStateDTO.MaxInfectedZones)
                return false;

            _infectedChunkKeys[_infectedZoneCount] = chunkKey;
            _infectedSeverities[_infectedZoneCount] = safeSeverity;
            _infectedZoneCount++;
            return true;
        }

        private int FindZoneIndex(long chunkKey)
        {
            int count = Mathf.Clamp(_infectedZoneCount, 0, EcosystemStateDTO.MaxInfectedZones);
            for (int i = 0; i < count; i++)
            {
                if (_infectedChunkKeys[i] == chunkKey)
                    return i;
            }

            return -1;
        }

        private float ResolveInfectionPressure01()
        {
            IEnvironmentalStrainReadModel environmentalStrainManager = _environmentalStrain;
            if (environmentalStrainManager == null)
                return 0f;

            float microplasticStrain = NonNegativeFiniteOrZero(environmentalStrainManager.MicroplasticStrain);
            float generalPollution = NonNegativeFiniteOrZero(environmentalStrainManager.GeneralPollution);
            float weightedDebt = microplasticStrain * 1.5f + generalPollution * 0.35f;
            if (!IsFinite(weightedDebt))
                return 0f;

            if (weightedDebt <= InfectionActivationDebt)
                return 0f;

            float denominator = InfectionFullDebt - InfectionActivationDebt;
            if (!IsFinite(denominator) || denominator <= 0.0001f)
                return 0f;

            return SaturateFinite01((weightedDebt - InfectionActivationDebt) / denominator);
        }

        private static float NonNegativeFiniteOrZero(float value)
        {
            return IsFinite(value) && value > 0f ? value : 0f;
        }

        private static float SaturateFinite01(float value)
        {
            if (!IsFinite(value))
                return 0f;

            return Mathf.Clamp01(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = false;
        }

        private void CacheRuntimeDependencies()
        {
            _playerExploration = GlobalRegistry.PlayerExplorationReadModel;
            _faunaGenetics = GlobalRegistry.FaunaWorldSeed;
            _environmentalStrain = GlobalRegistry.EnvironmentalStrainReadModel;
        }

        private void CacheSaveServiceCold()
        {
            _saveService = GlobalRegistry.Save;
        }

        private void ClearRuntimeDependencies()
        {
            _playerExploration = null;
            _faunaGenetics = null;
            _environmentalStrain = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerExplorationRuntime:
                    _playerExploration = currentService as IPlayerExplorationChunkReadModel;
                    break;
                case GlobalRegistryServiceSlot.FaunaGeneticsRuntime:
                    _faunaGenetics = currentService as IFaunaWorldSeedReadModel;
                    break;
                case GlobalRegistryServiceSlot.EnvironmentalStrainRuntime:
                    _environmentalStrain = currentService as IEnvironmentalStrainReadModel;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    if (Application.isPlaying && previousService is ISaveService previousSave)
                        previousSave.Unregister(this);

                    _saveService = currentService as ISaveService;

                    if (Application.isPlaying && _saveService != null && isActiveAndEnabled && !_duplicateServiceSuppressed)
                        _saveService.Register(this);
                    break;
            }
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterEcosystemHealthRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.EcosystemHealth, this);
            if (_serviceRegistered)
                s_activeRuntime = this;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            EcosystemHealthDirector active = s_activeRuntime;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsEcosystemHealthRuntimeUsable(active))
                {
                    SuppressDuplicateService();
                    return true;
                }

                if (ReferenceEquals(s_activeRuntime, active))
                    s_activeRuntime = null;
                if (ReferenceEquals(GlobalRegistry.EcosystemHealth, active))
                    GlobalRegistry.UnregisterEcosystemHealthRuntime(active);
            }

            EcosystemHealthDirector registered = GlobalRegistry.EcosystemHealth;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsEcosystemHealthRuntimeUsable(registered))
            {
                s_activeRuntime = registered;
                SuppressDuplicateService();
                return true;
            }

            if (ReferenceEquals(s_activeRuntime, registered))
                s_activeRuntime = null;
            GlobalRegistry.UnregisterEcosystemHealthRuntime(registered);
            return false;
        }

        private static bool IsEcosystemHealthRuntimeUsable(EcosystemHealthDirector director)
        {
            return director != null &&
                   director._serviceRegistered &&
                   !director._duplicateServiceSuppressed &&
                   director.isActiveAndEnabled;
        }

        private void SuppressDuplicateService()
        {
            _duplicateServiceSuppressed = true;
            _serviceRegistered = false;
            _registeredToTick = false;
            enabled = false;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterEcosystemHealthRuntime(this);
            _serviceRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        private static long PackChunkKey(WorldChunkCoordinate chunkCoordinate)
        {
            unchecked
            {
                return ((long)(uint)chunkCoordinate.x << 32) | (uint)chunkCoordinate.z;
            }
        }
    }
}
