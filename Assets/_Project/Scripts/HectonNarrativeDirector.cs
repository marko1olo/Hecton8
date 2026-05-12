// ============================================================================
// HECTON-8 - HectonNarrativeDirector.cs
// Stateless narrative tracker for discoveries and depth-tier milestones.
// ============================================================================

using System.Collections.Generic;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.World;
using UnityEngine;
using Hecton8.Systems.AI;
using Hecton8.Interaction;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public sealed class HectonNarrativeDirector : MonoBehaviour, ISaveable, ISlowTickable, INarrativeEventListener, INarrativePointOfInterestListener, IDirectorAIEventListener
    {
        private struct NarrativeNode
        {
            public uint DiscoveryHash;
            public ushort Reserved0;
            public ushort Reserved1;
        }

        [Header("Settings")]
#pragma warning disable CS0414 // slowTickRate reserved for future SlowTick throttling
        [SerializeField, UnityEngine.Range(1, 10)] private int slowTickRate = 2;
#pragma warning restore CS0414

        [Header("State")]
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private List<string> discoveredIds = new List<string>(); // COLD ALLOC: 64 for [N] discoveries (reasonable number of narrative discoveries)
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private int currentDepthTier;
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private ulong narrativeAupTriggeredMask;

        private const float SlowTickDeltaSeconds = 0.5f;
        private const float HighTierAupScanIntervalSeconds = 0.5f;
        private const float DefaultAupScanIntervalSeconds = 1f;
        private const float LowTierAupScanIntervalSeconds = 2f;

        // Runtime-only collection for active POIs in the world.
        // COLD ALLOC: 64 for initial capacity (reasonable number of POIs per scene).
        private readonly List<NarrativeDiscovery> _activePOIs = new List<NarrativeDiscovery>(64);
        private readonly HashSet<string> _discoveredIdLookup = new HashSet<string>(64, System.StringComparer.Ordinal);
        private readonly HashSet<uint> _discoveredHashLookup = new HashSet<uint>(64);
        private Unity.Collections.NativeHashMap<uint, NarrativeNode> _narrativeNodesByHash;

        private Transform _playerTransform;
        private bool _registeredNarrativeRuntime;
        private bool _registeredSlowTick;
        private float _aupScanAccumulator = LowTierAupScanIntervalSeconds;
        private bool _aupScannerDisabledForCurrentPoiSet;

        // Flag: Director zaprosil redkuyu nahodku v tekuschem tike
        // Chitaetsya v diagnostike i buduschey sisteme spavna
#pragma warning disable CS0414
        private bool _rareDiscoveryRequested;
#pragma warning restore CS0414

        public int SavePriority => 5;
        public int LoadPriority => 5;


        public void PopulateSaveData(SaveData data)
        {
            data.narrativeDepthTier = currentDepthTier;
            data.narrativeAupTriggeredMask = narrativeAupTriggeredMask;
            data.narrativeDiscoveryCount = Mathf.Min(discoveredIds.Count, SaveData.MaxNarrativeDiscoveries);
            
            if (data.narrativeDiscoveryIds == null || data.narrativeDiscoveryIds.Length != SaveData.MaxNarrativeDiscoveries)
            {
                data.narrativeDiscoveryIds = new string[SaveData.MaxNarrativeDiscoveries];
            }

            for (int i = 0; i < data.narrativeDiscoveryCount; i++)
            {
                data.narrativeDiscoveryIds[i] = discoveredIds[i];
            }
        }


        public void LoadFromSaveData(SaveData data)
        {
            currentDepthTier = data.narrativeDepthTier;
            narrativeAupTriggeredMask = data.narrativeAupTriggeredMask;
            _aupScannerDisabledForCurrentPoiSet = false;
            _aupScanAccumulator = LowTierAupScanIntervalSeconds;
            discoveredIds.Clear();

            if (data.narrativeDiscoveryIds != null)
            {
                int count = Mathf.Min(data.narrativeDiscoveryCount, data.narrativeDiscoveryIds.Length);
                for (int i = 0; i < count; i++)
                {
                    string discoveryId = data.narrativeDiscoveryIds[i];
                    if (!string.IsNullOrWhiteSpace(discoveryId))
                        discoveredIds.Add(discoveryId);
                }
            }

            RebuildDiscoveryLookup();
            
            // Re-sync world state from loaded data
            NarrativeEvents.RaiseDepthTierReached(currentDepthTier);
        }

        private void Awake()
        {
            HectonNarrativeDirector activeRuntime = GlobalRegistry.NarrativeDirector;
            if (activeRuntime != null && activeRuntime != this)
            {
                Destroy(gameObject);
                return;
            }

            _narrativeNodesByHash = new Unity.Collections.NativeHashMap<uint, NarrativeNode>(128, Unity.Collections.Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,NarrativeNode>[128] - narrative hash lookup for discovery routing - owner: HectonNarrativeDirector
            NativeMemorySentinel.RegisterNativeHashMap(_narrativeNodesByHash, nameof(HectonNarrativeDirector), nameof(_narrativeNodesByHash), NativeAllocationLifetime.Scene);
            RebuildDiscoveryLookup();
        }

        private void OnEnable()
        {
            TryRegister();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);

            NarrativeEvents.Register(this);
            NarrativeEvents.RegisterPointOfInterestListener(this);
            DirectorAIEvents.Register(this);

            ResolvePlayerTransform();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);

            NarrativeEvents.Unregister(this);
            NarrativeEvents.UnregisterPointOfInterestListener(this);
            DirectorAIEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            DirectorAIEvents.Unregister(this);

            if (_narrativeNodesByHash.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeHashMap(nameof(HectonNarrativeDirector), nameof(_narrativeNodesByHash));
                _narrativeNodesByHash.Dispose();
            }
        }

        private void TryRegister()
        {
            if (!_registeredNarrativeRuntime)
            {
                GlobalRegistry.RegisterNarrativeDirectorRuntime(this);
                _registeredNarrativeRuntime = ReferenceEquals(GlobalRegistry.NarrativeDirector, this);
            }

            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (_registeredNarrativeRuntime)
            {
                GlobalRegistry.UnregisterNarrativeDirectorRuntime(this);
                _registeredNarrativeRuntime = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }
        }

        public NarrativeDiscovery GetNearestUndiscoveredPOI(Vector3 center, float maxDistance)
        {
            NarrativeDiscovery nearest = null;
            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromRuntimePosition(center);
            double minSqrDist = (double)maxDistance * maxDistance;

            for (int i = 0; i < _activePOIs.Count; i++)
            {
                var poi = _activePOIs[i];
                if (poi == null) continue;

                // Check if already discovered
                uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(poi.DiscoveryId);
                if (discoveryHash != 0u && _discoveredHashLookup.Contains(discoveryHash))
                    continue;

                AbsoluteUniversePosition poiAup = AbsoluteUniversePosition.FromRuntimePosition(poi.transform.position);
                double sqrDist = DistanceSqAup(in poiAup, in centerAup);
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = poi;
                }
            }

            return nearest;
        }

        public void SlowTick()
        {
            if (_playerTransform == null && !ResolvePlayerTransform())
                return;

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
            if (ShouldScanAupNarrativeTriggers() &&
                !ScanAupNarrativeTriggers(in playerAup))
            {
                _aupScannerDisabledForCurrentPoiSet = true;
            }

            float depth = Mathf.Max(0f, (float)-playerAup.ToAbsoluteDouble3().y);
            int newTier = CalculateDepthTier(depth);

            if (newTier <= currentDepthTier)
                return;

            currentDepthTier = newTier;
            NarrativeEvents.RaiseDepthTierReached(currentDepthTier);

            LogDepthTierReached();
        }

        private bool ScanAupNarrativeTriggers(in AbsoluteUniversePosition playerAup)
        {
            if (_activePOIs.Count <= 0)
                return false;

            bool hasPendingTrigger = false;
            for (int i = _activePOIs.Count - 1; i >= 0; i--)
            {
                NarrativeDiscovery poi = _activePOIs[i];
                if (poi == null)
                    continue;

                if (!poi.TryGetAupTrigger(
                        out int bitIndex,
                        out double radiusSq,
                        out AbsoluteUniversePosition poiAup,
                        out uint discoveryHash))
                {
                    continue;
                }

                ulong triggerBit = 1UL << bitIndex;
                if ((narrativeAupTriggeredMask & triggerBit) != 0UL)
                    continue;

                if (_discoveredHashLookup.Contains(discoveryHash))
                {
                    narrativeAupTriggeredMask |= triggerBit;
                    continue;
                }

                if (!IsWithinAupNarrativeTriggerRange(in playerAup, in poiAup, radiusSq))
                {
                    hasPendingTrigger = true;
                    continue;
                }

                narrativeAupTriggeredMask |= triggerBit;
                NarrativeEvents.RaiseDiscoveryMade(discoveryHash);

                LoreDatabaseManager loreDatabase = GlobalRegistry.LoreDatabase;
                if (loreDatabase != null)
                    loreDatabase.TryUnlockByHash(LoreDatabaseManager.ComputeLoreHash(poi.DiscoveryId));
            }

            return hasPendingTrigger;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDepthTierReached()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Narrative] New depth tier reached.");
#endif
        }

        public bool HasDiscovery(string id)
        {
            uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(id);
            if (discoveryHash != 0u)
                return _discoveredHashLookup.Contains(discoveryHash);

            return !string.IsNullOrWhiteSpace(id) && _discoveredIdLookup.Contains(id);
        }

        public bool HasDiscovery(uint discoveryHash)
        {
            return discoveryHash != 0u && _discoveredHashLookup.Contains(discoveryHash);
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)
                return;

            HandleDiscovery(payload.DiscoveryHash);
        }

        public void OnNarrativePointOfInterestRegistered(NarrativeDiscovery poi)
        {
            if (poi == null)
                return;

            if (!_activePOIs.Contains(poi))
            {
                _activePOIs.Add(poi);
                _aupScannerDisabledForCurrentPoiSet = false;
                _aupScanAccumulator = LowTierAupScanIntervalSeconds;
            }

            RegisterNarrativeNode(poi.DiscoveryId);
        }

        public void OnNarrativePointOfInterestDisposed(NarrativeDiscovery poi)
        {
            if (poi == null)
                return;

            _activePOIs.Remove(poi);
            _aupScannerDisabledForCurrentPoiSet = false;
        }

        private bool ShouldScanAupNarrativeTriggers()
        {
            if (_aupScannerDisabledForCurrentPoiSet || _activePOIs.Count <= 0)
                return false;

            _aupScanAccumulator += SlowTickDeltaSeconds;
            float scanInterval = ResolveAupScanIntervalSeconds();
            if (_aupScanAccumulator < scanInterval)
                return false;

            _aupScanAccumulator = 0f;
            return true;
        }

        private static float ResolveAupScanIntervalSeconds()
        {
            switch (GlobalRegistry.ScalabilityTier)
            {
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return HighTierAupScanIntervalSeconds;

                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return LowTierAupScanIntervalSeconds;

                default:
                    return DefaultAupScanIntervalSeconds;
            }
        }

        private static bool IsWithinAupNarrativeTriggerRange(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition poiAup,
            double radiusSq)
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350)
            {
                double3 delta = poiAup.ToAbsoluteDouble3() - playerAup.ToAbsoluteDouble3();
                double dominantAxis = math.cmax(math.abs(delta));
                return dominantAxis * dominantAxis <= radiusSq;
            }

            return DistanceSqAup(in playerAup, in poiAup) <= radiusSq;
        }

        private int CalculateDepthTier(float depth)
        {
            if (depth >= 1000f)
                return 4;

            if (depth >= 300f)
                return 3;

            if (depth >= 100f)
                return 2;

            if (depth >= 0f)
                return 1;

            return 0;
        }

        private static double DistanceSqAup(
            in AbsoluteUniversePosition a,
            in AbsoluteUniversePosition b)
        {
            double3 origin = a.ToAbsoluteDouble3();
            double3 target = b.ToAbsoluteDouble3();
            return math.distancesq(origin, target);
        }

        private void HandleDiscovery(uint discoveryHash)
        {
            if (discoveryHash == 0u)
                return;

            if (_narrativeNodesByHash.IsCreated && !_narrativeNodesByHash.ContainsKey(discoveryHash))
            {
                if (NarrativeEvents.TryResolveDiscoveryId(discoveryHash, out string unresolvedId))
                    RegisterNarrativeNode(unresolvedId);
            }

            if (!_discoveredHashLookup.Add(discoveryHash))
                return;

            if (!NarrativeEvents.TryResolveDiscoveryId(discoveryHash, out string id) ||
                string.IsNullOrWhiteSpace(id) ||
                !_discoveredIdLookup.Add(id))
            {
                return;
            }

            discoveredIds.Add(id);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Discovery Logged: {id}");
#endif
        }

        private void HandleRareDiscoveryRequest(Vector3 hintPosition)
        {
            _rareDiscoveryRequested = true;

            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
            if (firstHourDirector != null &&
                !firstHourDirector.IsMilestoneComplete(FirstHourMilestone.FirstModule))
            {
                return;
            }

            AtlasSignalSystem atlasSignalSystem = Hecton8.Core.GlobalRegistry.AtlasSignal;
            bool rebroadcastedPulse = CanRebroadcastAtlasRareDiscoveryPulse(atlasSignalSystem);
            if (rebroadcastedPulse)
                AtlasSignalEvents.RaisePulse(atlasSignalSystem.CurrentStrength);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Rare Discovery requested near {hintPosition}. " +
                      $"Atlas-6 signal {(rebroadcastedPulse ? "pulse triggered" : "pulse skipped")} (strength: " +
                      $"{(Hecton8.Core.GlobalRegistry.AtlasSignal != null ? Hecton8.Core.GlobalRegistry.AtlasSignal.CurrentStrength : 0f):F2}).");
#endif
        }

        void IDirectorAIEventListener.OnDirectorSpawnHordeRequested(Vector3 position)
        {
        }

        void IDirectorAIEventListener.OnDirectorEquipmentGlitchRequested(float intensity)
        {
        }

        void IDirectorAIEventListener.OnDirectorRareDiscoveryRequested(Vector3 position)
        {
            HandleRareDiscoveryRequest(position);
        }

        void IDirectorAIEventListener.OnDirectorWeatherShiftRequested(float intensity)
        {
        }

        void IDirectorAIEventListener.OnDirectorMissionTriggerRequested(Vector3 position)
        {
        }

        void IDirectorAIEventListener.OnDirectorPredatorPressureChanged(bool pressureEnabled)
        {
        }

        void IDirectorAIEventListener.OnDirectorThreatSpike(Vector3 position, float intensity)
        {
        }

        private static bool CanRebroadcastAtlasRareDiscoveryPulse(AtlasSignalSystem atlasSignalSystem)
        {
            return atlasSignalSystem != null &&
                   atlasSignalSystem.CurrentRevealStage >= 3 &&
                   atlasSignalSystem.CurrentStrength > 0f;
        }

        private bool ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return true;

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            _playerTransform = playerTransform;
            return true;
        }

        private void RebuildDiscoveryLookup()
        {
            _discoveredIdLookup.Clear();
            _discoveredHashLookup.Clear();
            if (_narrativeNodesByHash.IsCreated)
                _narrativeNodesByHash.Clear();
            if (discoveredIds == null)
                return;

            int writeIndex = 0;
            for (int i = 0; i < discoveredIds.Count; i++)
            {
                string discoveryId = discoveredIds[i];
                if (string.IsNullOrWhiteSpace(discoveryId) || !_discoveredIdLookup.Add(discoveryId))
                    continue;

                uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(discoveryId);
                if (discoveryHash != 0u)
                {
                    _discoveredHashLookup.Add(discoveryHash);
                    RegisterNarrativeNode(discoveryId);
                }

                if (writeIndex != i)
                    discoveredIds[writeIndex] = discoveryId;

                writeIndex++;
            }

            if (writeIndex < discoveredIds.Count)
                discoveredIds.RemoveRange(writeIndex, discoveredIds.Count - writeIndex);
        }

        private void RegisterNarrativeNode(string discoveryId)
        {
            if (!_narrativeNodesByHash.IsCreated)
                return;

            uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(discoveryId);
            if (discoveryHash == 0u || _narrativeNodesByHash.ContainsKey(discoveryHash))
                return;

            _narrativeNodesByHash.TryAdd(discoveryHash, new NarrativeNode
            {
                DiscoveryHash = discoveryHash,
                Reserved0 = 0,
                Reserved1 = 0
            });
        }
    }
}
