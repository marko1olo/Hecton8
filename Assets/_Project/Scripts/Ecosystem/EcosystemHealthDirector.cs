using System.Collections.Generic;
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
    public sealed class EcosystemHealthDirector : MonoBehaviour, ISlowTickable, ISaveable
    {
        private const float InfectionActivationDebt = 20f;
        private const float InfectionFullDebt = 140f;

        private static EcosystemHealthDirector _instance;

        // COLD ALLOC: HashSet<long>[64] - infected chunk registry persisted in local save - owner: EcosystemHealthDirector
        private readonly HashSet<long> _infectedChunkKeys = new HashSet<long>(EcosystemStateDTO.MaxInfectedZones);
        // COLD ALLOC: Dictionary<long,float>[64] - infected chunk severity lookup - owner: EcosystemHealthDirector
        private readonly Dictionary<long, float> _severityByChunkKey = new Dictionary<long, float>(EcosystemStateDTO.MaxInfectedZones);
        // COLD ALLOC: long[16384] - explored PDA chunk copy buffer for infection-zone selection - owner: EcosystemHealthDirector
        private readonly long[] _exploredChunkBuffer = new long[ExplorationMapDTO.MaxExploredChunks];
        private bool _registeredToTick;
        private bool _serviceRegistered;

        /// <summary>Active runtime owner while the gameplay scene is loaded.</summary>
        public static EcosystemHealthDirector Instance => _instance;

        /// <inheritdoc />
        public int SavePriority => 42;

        /// <inheritdoc />
        public int LoadPriority => 42;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            TryRegisterService();
            TryRegisterToTickManager();
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Register(this);
        }

        private void Start()
        {
            TryRegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            TryUnregisterService();
            if (_instance == this)
                _instance = null;
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            float infectionPressure = ResolveInfectionPressure01();
            int targetZoneCount = Mathf.Clamp(Mathf.CeilToInt(infectionPressure * EcosystemStateDTO.MaxInfectedZones * 0.25f), 0, EcosystemStateDTO.MaxInfectedZones);

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
            return _infectedChunkKeys.Contains(PackChunkKey(chunkCoordinate));
        }

        /// <summary>
        /// Applies infection state to one freshly spawned fauna instance.
        /// </summary>
        public void ConfigureSpawnedFauna(FaunaBrain faunaBrain, CreatureArchetypeData archetype, WorldChunkCoordinate chunkCoordinate)
        {
            if (faunaBrain == null)
                return;

            long chunkKey = PackChunkKey(chunkCoordinate);
            bool infected = _infectedChunkKeys.Contains(chunkKey);
            float severity = infected && _severityByChunkKey.TryGetValue(chunkKey, out float storedSeverity)
                ? storedSeverity
                : 0f;

            faunaBrain.SetInfectedState(infected, severity);
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.ecosystemState.EnsureCapacity();

            int writeIndex = 0;
            HashSet<long>.Enumerator enumerator = _infectedChunkKeys.GetEnumerator();
            while (enumerator.MoveNext() && writeIndex < EcosystemStateDTO.MaxInfectedZones)
            {
                long chunkKey = enumerator.Current;
                data.ecosystemState.infectedChunkKeys[writeIndex] = chunkKey;
                data.ecosystemState.infectedSeverities[writeIndex] =
                    _severityByChunkKey.TryGetValue(chunkKey, out float severity)
                        ? Mathf.Clamp01(severity)
                        : 0f;
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
            _infectedChunkKeys.Clear();
            _severityByChunkKey.Clear();

            if (data == null)
                return;

            EcosystemStateDTO dto = data.ecosystemState;
            int count = Mathf.Clamp(dto.infectedZoneCount, 0, dto.infectedChunkKeys != null ? dto.infectedChunkKeys.Length : 0);
            for (int i = 0; i < count; i++)
            {
                long chunkKey = dto.infectedChunkKeys[i];
                _infectedChunkKeys.Add(chunkKey);
                _severityByChunkKey[chunkKey] = dto.infectedSeverities != null && i < dto.infectedSeverities.Length
                    ? Mathf.Clamp01(dto.infectedSeverities[i])
                    : 0f;
            }
        }

        private void EnsureZoneBudget(int targetZoneCount, float infectionPressure)
        {
            PlayerExplorationTracker tracker = GlobalRegistry.PlayerExploration;
            if (tracker == null)
                return;

            int exploredCount = tracker.CopyExploredChunkKeys(_exploredChunkBuffer);
            if (exploredCount <= 0)
                return;

            FaunaGeneticsManager geneticsManager = GlobalRegistry.FaunaGenetics;
            int seed = geneticsManager != null ? geneticsManager.WorldSeed : 0;
            int dayIndex;
            float dayTimeHours;
            float playTimeSeconds;
            PDAClockUtility.CaptureStamp(out dayIndex, out dayTimeHours, out playTimeSeconds);

            int startIndex = Mathf.Abs(seed ^ dayIndex ^ Mathf.FloorToInt(playTimeSeconds)) % exploredCount;
            for (int search = 0; search < exploredCount && _infectedChunkKeys.Count < targetZoneCount; search++)
            {
                int index = startIndex + search;
                if (index >= exploredCount)
                    index -= exploredCount;

                long chunkKey = _exploredChunkBuffer[index];
                if (_infectedChunkKeys.Contains(chunkKey))
                    continue;

                _infectedChunkKeys.Add(chunkKey);
                _severityByChunkKey[chunkKey] = Mathf.Clamp01(infectionPressure);
            }

            for (int i = 0; i < exploredCount; i++)
            {
                long chunkKey = _exploredChunkBuffer[i];
                if (_severityByChunkKey.ContainsKey(chunkKey))
                    _severityByChunkKey[chunkKey] = Mathf.Clamp01(infectionPressure);
            }
        }

        private void TrimZones(int targetZoneCount)
        {
            if (_infectedChunkKeys.Count <= targetZoneCount)
                return;

            HashSet<long>.Enumerator enumerator = _infectedChunkKeys.GetEnumerator();
            while (_infectedChunkKeys.Count > targetZoneCount && enumerator.MoveNext())
            {
                long chunkKey = enumerator.Current;
                _infectedChunkKeys.Remove(chunkKey);
                _severityByChunkKey.Remove(chunkKey);
                enumerator = _infectedChunkKeys.GetEnumerator();
            }
        }

        private void ClearAllZones()
        {
            if (_infectedChunkKeys.Count == 0 && _severityByChunkKey.Count == 0)
                return;

            _infectedChunkKeys.Clear();
            _severityByChunkKey.Clear();
        }

        private float ResolveInfectionPressure01()
        {
            EnvironmentalStrainManager environmentalStrainManager = GlobalRegistry.EnvironmentalStrain;
            if (environmentalStrainManager == null)
                return 0f;

            float weightedDebt = environmentalStrainManager.MicroplasticStrain * 1.5f + environmentalStrainManager.GeneralPollution * 0.35f;
            if (weightedDebt <= InfectionActivationDebt)
                return 0f;

            return Mathf.Clamp01((weightedDebt - InfectionActivationDebt) / Mathf.Max(1f, InfectionFullDebt - InfectionActivationDebt));
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterEcosystemHealthRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.EcosystemHealth, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterEcosystemHealthRuntime(this);
            _serviceRegistered = false;
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
