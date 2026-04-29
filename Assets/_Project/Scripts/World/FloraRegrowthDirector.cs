using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Scavenging;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Delayed regrowth owner for harvested kelp and sargassum flora instances.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-119)]
    public sealed class FloraRegrowthDirector : MonoBehaviour, ITickable, ISlowTickable
    {
        private const float RegrowthDelaySeconds = 4f * 60f * 60f;
        private const float RegrowthDurationSeconds = 90f;
        private const int DefaultTrackedRegrowthCapacity = 2048;
        private const float SeedFlightDurationSeconds = 60f;
        private const float SeedSproutDelaySeconds = 2f * 60f * 60f;
        private const float SeedSinkVelocityMetersPerSecond = 0.06f;
        private const float SeedFlowScale = 0.72f;
        private const float SeedSlopeSampleDistance = 1.25f;
        private const float MaximumSeedSlopeDegrees = 30f;
        private const int SeedsPerSargassumCluster = 3;
        private const byte StateWaiting = 0;
        private const byte StateActive = 1;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct FloraRegrowthState
        {
            public uint InstanceUid;
            public float3 RuntimePosition;
            public float EligiblePlayTime;
            public float RegrowthStartPlayTime;
            public byte State;
            public byte SeenThisScan;
            public ushort Reserved0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct SeedFlightState
        {
            public uint SeedInstanceUid;
            public ulong TemplateHash;
            public float3 Position;
            public float ElapsedSeconds;
            public byte Landed;
            public byte Reserved0;
            public ushort Reserved1;
        }

        [SerializeField]
        [Tooltip("Runtime owner that mutates streamed flora metadata and harvest health state.")]
        private DestructibleOrganicManager destructibleOrganicManager;

        [SerializeField]
        [Tooltip("MapMagic vegetation bridge that owns abyssal flow and terrain-cache queries.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        private NativeList<PersistentWorldDeltaRecord> _destroyedFloraScratch;
        private NativeList<PersistentWorldDeltaRecord> _pendingSeedScratch;
        private NativeList<FloraRegrowthState> _regrowthStates;
        private NativeHashMap<uint, int> _stateIndexByInstanceUid;
        private NativeList<SeedFlightState> _seedFlightStates;
        private NativeHashMap<uint, int> _seedFlightIndexByUid;
        private NativeHashMap<uint, byte> _seedEmissionByDestroyedUid;
        private float _lastSeedPlayTime;
        private bool _tickRegistered;
        private bool _slowTickRegistered;

        private void Awake()
        {
            if (destructibleOrganicManager == null)
                destructibleOrganicManager = GetComponent<DestructibleOrganicManager>();

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            _destroyedFloraScratch = new NativeList<PersistentWorldDeltaRecord>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[2048] - destroyed flora scan scratch for regrowth eligibility - owner: FloraRegrowthDirector
            _pendingSeedScratch = new NativeList<PersistentWorldDeltaRecord>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[2048] - pending flora seed scan scratch for delayed sprout updates - owner: FloraRegrowthDirector
            _regrowthStates = new NativeList<FloraRegrowthState>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeList<FloraRegrowthState>[2048] - active and pending flora regrowth states - owner: FloraRegrowthDirector
            _stateIndexByInstanceUid = new NativeHashMap<uint, int>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[2048] - regrowth state lookup keyed by flora uid - owner: FloraRegrowthDirector
            _seedFlightStates = new NativeList<SeedFlightState>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeList<SeedFlightState>[2048] - active organic seed trajectories - owner: FloraRegrowthDirector
            _seedFlightIndexByUid = new NativeHashMap<uint, int>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[2048] - seed trajectory lookup keyed by landed seed uid - owner: FloraRegrowthDirector
            _seedEmissionByDestroyedUid = new NativeHashMap<uint, byte>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,byte>[2048] - destroyed flora seed-emission gate keyed by source flora uid - owner: FloraRegrowthDirector
            _lastSeedPlayTime = GetCurrentPlayTimeSeconds();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_tickRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = true;
            }

            if (!_slowTickRegistered)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }
        }

        private void OnDestroy()
        {
            if (_destroyedFloraScratch.IsCreated)
                _destroyedFloraScratch.Dispose();

            if (_pendingSeedScratch.IsCreated)
                _pendingSeedScratch.Dispose();

            if (_regrowthStates.IsCreated)
                _regrowthStates.Dispose();

            if (_stateIndexByInstanceUid.IsCreated)
                _stateIndexByInstanceUid.Dispose();

            if (_seedFlightStates.IsCreated)
                _seedFlightStates.Dispose();

            if (_seedFlightIndexByUid.IsCreated)
                _seedFlightIndexByUid.Dispose();

            if (_seedEmissionByDestroyedUid.IsCreated)
                _seedEmissionByDestroyedUid.Dispose();
        }

        /// <summary>
        /// Advances active regrowth blends for already-eligible flora records.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_regrowthStates.IsCreated || !_stateIndexByInstanceUid.IsCreated || destructibleOrganicManager == null)
                return;

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            float currentPlayTime = GetCurrentPlayTimeSeconds();
            UpdateSeedFlights(deltaTime);
            for (int i = _regrowthStates.Length - 1; i >= 0; i--)
            {
                FloraRegrowthState state = _regrowthStates[i];
                if (state.State != StateActive)
                    continue;

                float progress01 = math.saturate((currentPlayTime - state.RegrowthStartPlayTime) / RegrowthDurationSeconds);
                destructibleOrganicManager.TrySetRegrowthProgress(
                    state.InstanceUid,
                    new Vector3(state.RuntimePosition.x, state.RuntimePosition.y, state.RuntimePosition.z),
                    progress01);

                if (progress01 < 1f)
                    continue;

                registry?.TryClearDestroyedFlora(state.InstanceUid);
                RemoveStateAtSwapBack(i);
            }
        }

        /// <summary>
        /// Scans persistent flora-destruction tombstones and starts delayed regrowth once the time gate opens.
        /// </summary>
        public void SlowTick()
        {
            if (!_destroyedFloraScratch.IsCreated || !_regrowthStates.IsCreated || !_stateIndexByInstanceUid.IsCreated)
                return;

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null || destructibleOrganicManager == null)
                return;

            float currentPlayTime = GetCurrentPlayTimeSeconds();
            UpdatePendingSeedTimers(registry, currentPlayTime);
            for (int i = 0; i < _regrowthStates.Length; i++)
            {
                FloraRegrowthState state = _regrowthStates[i];
                state.SeenThisScan = 0;
                _regrowthStates[i] = state;
            }

            _destroyedFloraScratch.Clear();
            registry.CopyDestroyedFloraDeltas(_destroyedFloraScratch);
            for (int i = 0; i < _destroyedFloraScratch.Length; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = _destroyedFloraScratch[i];
                if (deltaRecord.InstanceUid == 0u ||
                    !destructibleOrganicManager.IsMaterialClassRegrowable(deltaRecord.ItemPersistentIdHash))
                {
                    continue;
                }

                TryEmitSargassumSeeds(deltaRecord);

                Vector3 runtimePosition = ToRuntimePosition(deltaRecord.UnpackPosition(registry.ChunkSizeMeters));
                if (_stateIndexByInstanceUid.TryGetValue(deltaRecord.InstanceUid, out int stateIndex))
                {
                    FloraRegrowthState existing = _regrowthStates[stateIndex];
                    existing.RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                    existing.SeenThisScan = 1;
                    if (existing.State == StateWaiting && currentPlayTime >= existing.EligiblePlayTime)
                    {
                        existing.State = StateActive;
                        existing.RegrowthStartPlayTime = currentPlayTime;
                    }

                    _regrowthStates[stateIndex] = existing;
                    continue;
                }

                if (_regrowthStates.Length >= _regrowthStates.Capacity)
                    break;

                FloraRegrowthState newState = new FloraRegrowthState
                {
                    InstanceUid = deltaRecord.InstanceUid,
                    RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                    EligiblePlayTime = currentPlayTime + RegrowthDelaySeconds,
                    RegrowthStartPlayTime = 0f,
                    State = StateWaiting,
                    SeenThisScan = 1,
                    Reserved0 = 0
                };

                _stateIndexByInstanceUid.TryAdd(newState.InstanceUid, _regrowthStates.Length);
                _regrowthStates.AddNoResize(newState);
            }

            for (int i = _regrowthStates.Length - 1; i >= 0; i--)
            {
                FloraRegrowthState state = _regrowthStates[i];
                if (state.State == StateWaiting && state.SeenThisScan == 0)
                    RemoveStateAtSwapBack(i);
            }
        }

        private void UpdateSeedFlights(float deltaTime)
        {
            if (!_seedFlightStates.IsCreated || _seedFlightStates.Length <= 0)
                return;

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null)
                return;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            for (int i = _seedFlightStates.Length - 1; i >= 0; i--)
            {
                SeedFlightState state = _seedFlightStates[i];
                if (state.Landed != 0)
                    continue;

                Vector3 seedPosition = new Vector3(state.Position.x, state.Position.y, state.Position.z);
                Vector3 sampledFlow = Vector3.zero;
                if (vegetationBridge != null)
                {
                    vegetationBridge.TrySampleAbyssalFlow(seedPosition, out sampledFlow);
                    sampledFlow = vegetationBridge.ApplyAbyssalFlowNoise(sampledFlow, seedPosition);
                }

                Vector3 step = (sampledFlow * SeedFlowScale) + (Vector3.down * SeedSinkVelocityMetersPerSecond);
                seedPosition += step * Mathf.Max(0f, deltaTime);
                state.Position = new float3(seedPosition.x, seedPosition.y, seedPosition.z);
                state.ElapsedSeconds += Mathf.Max(0f, deltaTime);

                if (state.ElapsedSeconds < SeedFlightDurationSeconds)
                {
                    _seedFlightStates[i] = state;
                    continue;
                }

                if (TryLandSeed(state, registry, out SeedFlightState landedState))
                {
                    _seedFlightStates[i] = landedState;
                }

                RemoveSeedFlightAtSwapBack(i);
            }
        }

        private bool TryLandSeed(SeedFlightState state, PersistentWorldRegistry registry, out SeedFlightState landedState)
        {
            landedState = state;
            landedState.Landed = 1;
            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            Vector3 landingPosition = new Vector3(state.Position.x, state.Position.y, state.Position.z);
            if (vegetationBridge == null)
                return false;

            if (vegetationBridge.TryGetCachedTerrainHeight(landingPosition.x, landingPosition.z, out float terrainHeight))
                landingPosition.y = terrainHeight + 0.08f;

            if (!vegetationBridge.TrySampleTerrainSlopeDegrees(landingPosition, SeedSlopeSampleDistance, out float slopeDegrees) ||
                slopeDegrees > MaximumSeedSlopeDegrees)
            {
                return false;
            }

            registry.TryRegisterPendingFloraSeed(
                state.TemplateHash,
                state.SeedInstanceUid,
                landingPosition,
                (ushort)SeedSproutDelaySeconds);
            landedState.Position = new float3(landingPosition.x, landingPosition.y, landingPosition.z);
            return true;
        }

        private void TryEmitSargassumSeeds(PersistentWorldDeltaRecord deltaRecord)
        {
            if (!_seedFlightStates.IsCreated ||
                !_seedFlightIndexByUid.IsCreated ||
                !_seedEmissionByDestroyedUid.IsCreated ||
                !destructibleOrganicManager.IsTemplateMaterialClass(deltaRecord.ItemPersistentIdHash, HarvestableTemplate.MaterialClass.Sargassum))
            {
                return;
            }

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null ||
                _seedEmissionByDestroyedUid.ContainsKey(deltaRecord.InstanceUid))
            {
                return;
            }

            Vector3 basePosition = ToRuntimePosition(deltaRecord.UnpackPosition(PersistentWorldRegistry.Instance.ChunkSizeMeters));
            for (int seedIndex = 0; seedIndex < SeedsPerSargassumCluster; seedIndex++)
            {
                uint seedUid = deltaRecord.InstanceUid ^ (uint)((seedIndex + 1) * 0x9E3779B9u);
                if (_seedFlightIndexByUid.ContainsKey(seedUid) || _seedFlightStates.Length >= _seedFlightStates.Capacity)
                    continue;

                Vector3 lateralOffset = ResolveSeedLateralOffset(seedUid);
                SeedFlightState state = new SeedFlightState
                {
                    SeedInstanceUid = seedUid,
                    TemplateHash = deltaRecord.ItemPersistentIdHash,
                    Position = new float3(basePosition.x + lateralOffset.x, basePosition.y + lateralOffset.y, basePosition.z + lateralOffset.z),
                    ElapsedSeconds = 0f,
                    Landed = 0,
                    Reserved0 = 0,
                    Reserved1 = 0
                };

                _seedFlightIndexByUid.TryAdd(seedUid, _seedFlightStates.Length);
                _seedFlightStates.AddNoResize(state);
            }

            _seedEmissionByDestroyedUid.TryAdd(deltaRecord.InstanceUid, 1);
        }

        private void UpdatePendingSeedTimers(PersistentWorldRegistry registry, float currentPlayTime)
        {
            if (!_pendingSeedScratch.IsCreated)
                return;

            float playDelta = Mathf.Max(0f, currentPlayTime - _lastSeedPlayTime);
            _lastSeedPlayTime = currentPlayTime;

            _pendingSeedScratch.Clear();
            registry.CopyPendingFloraSeedDeltas(_pendingSeedScratch);
            ushort elapsedSeconds = (ushort)Mathf.Clamp(Mathf.RoundToInt(playDelta), 0, ushort.MaxValue);
            if (elapsedSeconds == 0)
                return;

            for (int i = 0; i < _pendingSeedScratch.Length; i++)
            {
                PersistentWorldDeltaRecord seedRecord = _pendingSeedScratch[i];
                if (!seedRecord.IsFloraSeedPending)
                    continue;

                int remainingSeconds = Mathf.Max(0, seedRecord.Quantity - elapsedSeconds);
                if (remainingSeconds > 0)
                {
                    registry.TryUpdatePendingFloraSeed(seedRecord.InstanceUid, (ushort)remainingSeconds);
                    continue;
                }

                registry.TryMarkPendingFloraSeedReady(seedRecord.InstanceUid);
            }
        }

        private void RemoveSeedFlightAtSwapBack(int index)
        {
            if (!_seedFlightStates.IsCreated || !_seedFlightIndexByUid.IsCreated || index < 0 || index >= _seedFlightStates.Length)
                return;

            SeedFlightState removed = _seedFlightStates[index];
            int lastIndex = _seedFlightStates.Length - 1;
            SeedFlightState last = _seedFlightStates[lastIndex];
            _seedFlightStates.RemoveAtSwapBack(index);
            _seedFlightIndexByUid.Remove(removed.SeedInstanceUid);

            if (index < lastIndex)
            {
                _seedFlightIndexByUid.Remove(last.SeedInstanceUid);
                _seedFlightIndexByUid.TryAdd(last.SeedInstanceUid, index);
            }
        }

        private static Vector3 ResolveSeedLateralOffset(uint seedUid)
        {
            uint state = seedUid != 0u ? seedUid : 0x91E10DA5u;
            float angle = NextSeed01(ref state) * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(NextSeed01(ref state)) * 1.65f;
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Lerp(0.12f, 0.45f, NextSeed01(ref state)), Mathf.Sin(angle) * radius);
        }

        private static float NextSeed01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private void RemoveStateAtSwapBack(int index)
        {
            if (!_regrowthStates.IsCreated || !_stateIndexByInstanceUid.IsCreated || index < 0 || index >= _regrowthStates.Length)
                return;

            FloraRegrowthState removed = _regrowthStates[index];
            int lastIndex = _regrowthStates.Length - 1;
            FloraRegrowthState last = _regrowthStates[lastIndex];
            _regrowthStates.RemoveAtSwapBack(index);
            _stateIndexByInstanceUid.Remove(removed.InstanceUid);
            if (_seedEmissionByDestroyedUid.IsCreated)
                _seedEmissionByDestroyedUid.Remove(removed.InstanceUid);

            if (index < lastIndex)
            {
                _stateIndexByInstanceUid.Remove(last.InstanceUid);
                _stateIndexByInstanceUid.TryAdd(last.InstanceUid, index);
            }
        }

        private static float GetCurrentPlayTimeSeconds()
        {
            return GlobalRegistry.Save != null
                ? GlobalRegistry.Save.CurrentPlayTimeSeconds
                : Time.realtimeSinceStartup;
        }

        private static Vector3 ToRuntimePosition(AbsoluteUniversePosition position)
        {
            float3 runtimePosition = position.ToRuntimeFloat3();
            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }
    }
}
