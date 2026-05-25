// ============================================================================
// HECTON-8 - HectonNarrativeDirector.cs
// Stateless narrative tracker for discoveries and depth-tier milestones.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
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
    public sealed partial class HectonNarrativeDirector : MonoBehaviour, ISaveable, ISlowTickable, IUpdatable, ILateFrameTickable, INarrativeEventListener, INarrativePointOfInterestListener, IDirectorAIEventListener, ISpatialTriggerSystem, IOriginShiftListener, IGlobalRegistryHotSwapListener, INarrativeDiscoveryReadModel, IDisposable
    {
        private static int s_x001HectonNarrativeDirectorSignalPushDropCount;
        [Header("Settings")]
#pragma warning disable CS0414 // slowTickRate reserved for future SlowTick throttling
        [SerializeField, UnityEngine.Range(1, 10)] private int slowTickRate = 2;
#pragma warning restore CS0414

        [Header("State")]
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private List<string> discoveredIds = new List<string>(SaveData.MaxNarrativeDiscoveries); // COLD ALLOC: bounded save discovery ids - owner: HectonNarrativeDirector
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private int currentDepthTier;
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private ulong narrativeAupTriggeredMask;

        private const float VisualOverkillAupScanIntervalSeconds = 0.05f;
        private const float SurvivalAupScanIntervalSeconds = 0.25f;
        private const int NativePoiCapacity = 64;
        private const int NarrativeWaypointIdBase = 0x4E545247;
        private const float NarrativeFocusDefaultDurationSeconds = 3.5f;
        private const float NarrativeFocusSubtitleFadeDistanceSq = 1600f;
        private const byte SaveStateOperationSnapshot = 0;
        private const byte SaveStateOperationTriggered = 1;
        private const string NarrativeWaypointLabel = "Objective";
        private static readonly Color NarrativeWaypointColor = new Color(0.28f, 0.86f, 1f, 1f);
        private static readonly uint _blackBoxFaultWarningHash = NarrativeEvents.ComputeDiscoveryHash("NarrativeTrigger.BlackBoxFault");
        private static readonly uint _blackBoxFaultContextHash = NarrativeEvents.ComputeDiscoveryHash("NARRATIVE_TRIGGER_SYS");
        private static readonly uint _registryOverflowWarningHash = NarrativeEvents.ComputeDiscoveryHash("NarrativeTrigger.RegistryOverflow");

        // COLD ALLOC: string[64] - authored POI ids for hash-to-id narrative event resolution - owner: HectonNarrativeDirector
        private readonly string[] _poiDiscoveryIds = new string[NativePoiCapacity];
        // Runtime-only collection for active POIs in the world.
        // COLD ALLOC: 64 for initial capacity (reasonable number of POIs per scene).
        private readonly List<NarrativeDiscovery> _activePOIs = new List<NarrativeDiscovery>(64);
        private readonly HashSet<string> _discoveredIdLookup = new HashSet<string>(SaveData.MaxNarrativeDiscoveries, System.StringComparer.Ordinal);
        private readonly HashSet<uint> _discoveredHashLookup = new HashSet<uint>(SaveData.MaxNarrativeDiscoveries);

        private Transform _playerTransform;
        private bool _registeredNarrativeRuntime;
        private bool _registeredSpatialTriggerRuntime;
        private bool _registeredSlowTick;
        private bool _registeredHotSwapListener;
        private bool _saveRegistered;
        private ISaveService _saveService;
        private float _aupScanAccumulator = SurvivalAupScanIntervalSeconds;
        private bool _aupScannerDisabledForCurrentPoiSet;
        private int _poiCount;
        private int _spatialTriggerTickCount;
        private uint _lastBiomeHash;
        private AbsoluteUniversePosition _lastScheduledPlayerAup;
        private float3 _lastScheduledPlayerRuntime;
        private bool _hasLastScheduledPlayerPose;

        // Flag: Director zaprosil redkuyu nahodku v tekuschem tike
        // Chitaetsya v diagnostike i buduschey sisteme spavna
#pragma warning disable CS0414
        private bool _rareDiscoveryRequested;
#pragma warning restore CS0414

        public int SavePriority => 5;
        public int LoadPriority => 5;
        public int RegisteredPoiCount => _poiCount;
        public ulong PoiStateMask => narrativeAupTriggeredMask;
        int ISystem.TickCount => _spatialTriggerTickCount;

        public bool TryGetPoiStateMask(out ulong stateMask)
        {
            stateMask = narrativeAupTriggeredMask;
            return true;
        }

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

            for (int i = data.narrativeDiscoveryCount; i < SaveData.MaxNarrativeDiscoveries; i++)
            {
                data.narrativeDiscoveryIds[i] = null;
            }
        }


        public void LoadFromSaveData(SaveData data)
        {
            currentDepthTier = data.narrativeDepthTier;
            narrativeAupTriggeredMask = data.narrativeAupTriggeredMask;
            _aupScannerDisabledForCurrentPoiSet = false;
            _aupScanAccumulator = ResolveAupScanIntervalSeconds();
            discoveredIds.Clear();

            if (data.narrativeDiscoveryIds != null)
            {
                int count = Mathf.Min(
                    Mathf.Min(data.narrativeDiscoveryCount, data.narrativeDiscoveryIds.Length),
                    SaveData.MaxNarrativeDiscoveries);
                for (int i = 0; i < count; i++)
                {
                    string discoveryId = data.narrativeDiscoveryIds[i];
                    if (!string.IsNullOrWhiteSpace(discoveryId))
                        discoveredIds.Add(discoveryId);
                }
            }

            RebuildDiscoveryLookup();
            ApplySavedPoiStateToNativeRegistry();
            PublishPoiStateSignal(0u, -1, SaveStateOperationSnapshot);
            
            // Re-sync world state from loaded data
            NarrativeEvents.TryRaiseDepthTierReached(currentDepthTier);
        }

        private void Awake()
        {
            HectonNarrativeDirector activeRuntime = GlobalRegistry.NarrativeDirector;
            if (activeRuntime != null && activeRuntime != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeAupNarrativePoiVaultStorage();
            RebuildDiscoveryLookup();
        }

        private void OnEnable()
        {
            TryRegister();
            TryRegisterHotSwapListener();
            _saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
            TryRegisterSaveParticipant();

            NarrativeEvents.Register(this);
            NarrativeEvents.RegisterPointOfInterestListener(this);
            DirectorAIEvents.Register(this);
            HectonFloatingOrigin.RegisterListener(this);

            ResolvePlayerTransform();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();

            NarrativeEvents.Unregister(this);
            NarrativeEvents.UnregisterPointOfInterestListener(this);
            DirectorAIEvents.Unregister(this);
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            DirectorAIEvents.Unregister(this);
            HectonFloatingOrigin.UnregisterListener(this);

            DisposeAupNarrativePoiVaultStorage();
        }

        private void TryRegister()
        {
            if (!_registeredNarrativeRuntime)
            {
                GlobalRegistry.RegisterNarrativeDirectorRuntime(this);
                _registeredNarrativeRuntime = ReferenceEquals(GlobalRegistry.NarrativeDirector, this);
            }

            if (!_registeredSpatialTriggerRuntime)
            {
                GlobalRegistry.RegisterSpatialTriggerSystem(this);
                _registeredSpatialTriggerRuntime = ReferenceEquals(GlobalRegistry.SpatialTriggerSystem, this);
            }

            TryRegisterAupNarrativePoiUpdate();
            TryRegisterAupNarrativePoiLateFrame();

            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (_registeredNarrativeRuntime)
            {
                GlobalRegistry.UnregisterNarrativeDirectorRuntime(this);
                _registeredNarrativeRuntime = false;
            }

            if (_registeredSpatialTriggerRuntime)
            {
                GlobalRegistry.UnregisterSpatialTriggerSystem(this);
                _registeredSpatialTriggerRuntime = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }

            TryUnregisterAupNarrativePoiUpdate();
            TryUnregisterAupNarrativePoiLateFrame();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null || !isActiveAndEnabled)
                        return;

                    TryUnregisterAupNarrativePoiUpdate();
                    TryUnregisterAupNarrativePoiLateFrame();
                    TryRegisterAupNarrativePoiUpdate();
                    TryRegisterAupNarrativePoiLateFrame();
                    if (_registeredSlowTick)
                    {
                        GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                        _registeredSlowTick = false;
                    }

                    TryRegister();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    DisposeAupNarrativePoiVaultStorage();
                    if (currentService != null)
                    {
                        InitializeAupNarrativePoiVaultStorage();
                        RebuildNativePoiRegistry();
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

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_saveService == null)
                _saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;

            if (_saveService == null)
                return;

            _saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _saveRegistered = false;
        }

        public NarrativeDiscovery GetNearestUndiscoveredPOI(Vector3 center, float maxDistance)
        {
            NarrativeDiscovery nearest = null;
            if (!TryResolveRuntimeAup(center, out AbsoluteUniversePosition centerAup))
                return null;

            double minSqrDist = (double)maxDistance * maxDistance;

            for (int i = 0; i < _activePOIs.Count; i++)
            {
                var poi = _activePOIs[i];
                if (poi == null) continue;

                // Check if already discovered
                uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(poi.DiscoveryId);
                if (discoveryHash != 0u && _discoveredHashLookup.Contains(discoveryHash))
                    continue;

                AbsoluteUniversePosition poiAup = poi.CachedAup;
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
            _spatialTriggerTickCount = unchecked(_spatialTriggerTickCount + 1);

            if (_playerTransform == null && !ResolvePlayerTransform())
                return;

            Vector3 playerPosition = _playerTransform.position;
            float3 playerRuntime = new float3(playerPosition.x, playerPosition.y, playerPosition.z);
            if (!math.all(math.isfinite(playerRuntime)))
            {
                if (EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                    DumpAupNarrativePoiTelemetry(buffers);
                GlobalTelemetryBus.PublishPerformanceWarning(_blackBoxFaultWarningHash, _blackBoxFaultContextHash, 1f);
                return;
            }

            if (!TryResolveRuntimeAup(playerPosition, out AbsoluteUniversePosition playerAup))
            {
                if (EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                    DumpAupNarrativePoiTelemetry(buffers);
                GlobalTelemetryBus.PublishPerformanceWarning(_blackBoxFaultWarningHash, _blackBoxFaultContextHash, 1f);
                return;
            }

            CompleteAupNarrativePoiJobIfReady();

            float depth = Mathf.Max(0f, (float)-playerAup.ToAbsoluteDouble3().y);
            int newTier = CalculateDepthTier(depth);

            if (newTier <= currentDepthTier)
                return;

            currentDepthTier = newTier;
            NarrativeEvents.TryRaiseDepthTierReached(currentDepthTier);

            LogDepthTierReached();
        }

        public void Dispose()
        {
            DisposeAupNarrativePoiVaultStorage();
        }

        private void RebuildNativePoiRegistry()
        {
            CompleteAupNarrativePoiJob(forceComplete: true);
            _poiCount = 0;
            Array.Clear(_poiDiscoveryIds, 0, _poiDiscoveryIds.Length);
            int validSpatialPoiCount = 0;
            int discoveryIdCount = 0;

            for (int i = 0; i < _activePOIs.Count; i++)
            {
                NarrativeDiscovery poi = _activePOIs[i];
                if (poi == null || !poi.TryGetSpatialTrigger(out NarrativeSpatialTriggerAuthoring trigger))
                    continue;

                validSpatialPoiCount++;
                if (discoveryIdCount >= NativePoiCapacity)
                    continue;

                _poiDiscoveryIds[discoveryIdCount] = poi.DiscoveryId;
                discoveryIdCount++;
            }

            _poiCount = validSpatialPoiCount;
            if (validSpatialPoiCount > NativePoiCapacity)
                GlobalTelemetryBus.PublishPerformanceWarning(_registryOverflowWarningHash, _blackBoxFaultContextHash, validSpatialPoiCount);

            RebuildAupNarrativePoiVaultRegistry();
            _aupScannerDisabledForCurrentPoiSet = !HasPendingNativePoi();
        }

        private void ApplySavedPoiStateToNativeRegistry()
        {
            CompleteAupNarrativePoiJob(forceComplete: true);
            SyncAupNarrativePoiVaultStateFromMask();
            _aupScannerDisabledForCurrentPoiSet = !HasPendingNativePoi();
        }

        private bool HasPendingNativePoi()
        {
            return HasPendingAupNarrativePoi();
        }

        private void CompleteAupNarrativeJobForRegistryMutation()
        {
            CompleteAupNarrativePoiJob(forceComplete: true);
        }

        private void PublishBiomeSignal(in NarrativePoiDTO dto, in NarrativePoiPresentationDTO presentation, uint poiHash)
        {
            uint biomeHash = presentation.BiomeHash;
            if (biomeHash == 0u || biomeHash == _lastBiomeHash)
                return;

            BiomeChangedSignal signal = new BiomeChangedSignal
            {
                PositionAup = CreateAupFromPoiDto(in dto),
                PreviousBiomeHash = _lastBiomeHash,
                CurrentBiomeHash = biomeHash,
                PoiHash = poiHash,
                Frame = SystemDispatcher.CurrentFrameId
            };
            _lastBiomeHash = biomeHash;
            SignalBus<BiomeChangedSignal>.TryPushTracked(in signal, ref s_x001HectonNarrativeDirectorSignalPushDropCount);
        }

        private void PublishSoundscapeSignal(in NarrativePoiDTO dto, in NarrativePoiPresentationDTO presentation, uint poiHash)
        {
            uint profileHash = presentation.SoundscapeHash;
            if (profileHash == 0u)
                return;

            SoundscapeProfileSignal signal = new SoundscapeProfileSignal
            {
                PositionAup = CreateAupFromPoiDto(in dto),
                ProfileHash = profileHash,
                PoiHash = poiHash,
                Intensity01 = 1f,
                Frame = SystemDispatcher.CurrentFrameId
            };
            SignalBus<SoundscapeProfileSignal>.TryPushTracked(in signal, ref s_x001HectonNarrativeDirectorSignalPushDropCount);
        }

        private void PublishNarrativeFocusSignal(in NarrativePoiDTO dto, in NarrativePoiPresentationDTO presentation, uint poiHash)
        {
            uint subtitleHash = presentation.LoreHash != 0u ? presentation.LoreHash : poiHash;
            NarrativeFocusSignal signal = new NarrativeFocusSignal
            {
                TargetAup = CreateAupFromPoiDto(in dto),
                FocusHash = poiHash,
                SubtitleHash = subtitleHash,
                Intensity01 = 1f,
                DurationSeconds = NarrativeFocusDefaultDurationSeconds,
                SubtitleFadeDistanceSq = NarrativeFocusSubtitleFadeDistanceSq,
                Frame = SystemDispatcher.CurrentFrameId,
                Flags = (byte)(NarrativeFocusSignal.FlagArtifactTarget | NarrativeFocusSignal.FlagWorldSubtitle),
                BoneTarget = 0
            };
            SignalBus<NarrativeFocusSignal>.TryPushTracked(in signal, ref s_x001HectonNarrativeDirectorSignalPushDropCount);
        }

        private void PublishHudWaypointSignal(in NarrativePoiDTO dto, in NarrativePoiPresentationDTO presentation, uint poiHash)
        {
            byte flags = (byte)(presentation.Flags & 0xFFu);
            if ((flags & (byte)NarrativeSpatialTriggerFlags.HudBreadcrumb) == 0)
                return;

            uint questHash = presentation.QuestHash;
            if (!IsQuestActiveForHud(questHash))
                return;

            AbsoluteUniversePosition poiAup = CreateAupFromPoiDto(in dto);
            NarrativeHudWaypointSignal signal = new NarrativeHudWaypointSignal
            {
                PositionAup = poiAup,
                PoiHash = poiHash,
                QuestHash = questHash,
                Frame = SystemDispatcher.CurrentFrameId,
                Priority = 1,
                Flags = flags
            };
            SignalBus<NarrativeHudWaypointSignal>.TryPushTracked(in signal, ref s_x001HectonNarrativeDirectorSignalPushDropCount);

            IARWaypointService waypoints = GlobalRegistry.ARWaypoints;
            if (waypoints != null && waypoints.IsInitialized)
            {
                float3 runtime = poiAup.ToRuntimeFloat3();
                waypoints.SetWaypoint(
                    NarrativeWaypointIdBase ^ unchecked((int)poiHash),
                    new Vector3(runtime.x, runtime.y, runtime.z),
                    NarrativeWaypointLabel,
                    NarrativeWaypointColor);
            }
        }

        private bool IsQuestActiveForHud(uint questHash)
        {
            if (questHash == 0u)
                return true;

            IQuestSystem questSystem = GlobalRegistry.QuestSystem;
            if (questSystem == null || !questSystem.IsInitialized)
                return false;

            return questSystem.TryGetQuestIdByHash(questHash, out string questId) &&
                   questSystem.IsActive(questId);
        }

        private void PublishPoiStateSignal(uint poiHash, int poiIndex, byte operation, byte flags = 0)
        {
            NarrativePoiStateSignal signal = new NarrativePoiStateSignal
            {
                StateMask = narrativeAupTriggeredMask,
                PoiHash = poiHash,
                Frame = SystemDispatcher.CurrentFrameId,
                PoiIndex = poiIndex >= 0 ? (ushort)poiIndex : (ushort)0,
                Operation = operation,
                Flags = flags
            };
            SignalBus<NarrativePoiStateSignal>.TryPushTracked(in signal, ref s_x001HectonNarrativeDirectorSignalPushDropCount);
        }

        private static AbsoluteUniversePosition CreateAupFromPoiDto(in NarrativePoiDTO dto)
        {
            return AbsoluteUniversePosition.FromAbsolutePosition(dto.PoiAUP);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            CompleteAupNarrativePoiJobIfReady();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDepthTierReached()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Narrative] New depth tier reached.");
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

            CompleteAupNarrativeJobForRegistryMutation();

            if (!_activePOIs.Contains(poi))
            {
                _activePOIs.Add(poi);
                _aupScannerDisabledForCurrentPoiSet = false;
                _aupScanAccumulator = ResolveAupScanIntervalSeconds();
            }

            RebuildNativePoiRegistry();
        }

        public void OnNarrativePointOfInterestDisposed(NarrativeDiscovery poi)
        {
            if (poi == null)
                return;

            CompleteAupNarrativeJobForRegistryMutation();
            _activePOIs.Remove(poi);
            _aupScannerDisabledForCurrentPoiSet = false;
            RebuildNativePoiRegistry();
        }

        private bool ShouldScanAupNarrativeTriggers(float deltaSeconds)
        {
            if (_aupScannerDisabledForCurrentPoiSet || _poiCount <= 0)
                return false;

            float safeDelta = math.select(0f, math.max(0f, deltaSeconds), math.isfinite(deltaSeconds));
            _aupScanAccumulator = math.min(_aupScanAccumulator + safeDelta, SurvivalAupScanIntervalSeconds);
            float scanInterval = ResolveAupScanIntervalSeconds();
            if (_aupScanAccumulator < scanInterval)
                return false;

            _aupScanAccumulator = 0f;
            return true;
        }

        private static float ResolveAupScanIntervalSeconds()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.select(0f, quality, math.isfinite(quality)));
            float smooth = quality * quality * (3f - 2f * quality);
            return math.lerp(VisualOverkillAupScanIntervalSeconds, SurvivalAupScanIntervalSeconds, 1f - smooth);
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
            return AupPrecisionMath.DistanceSqSafeDouble(target, origin);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private void HandleDiscovery(uint discoveryHash)
        {
            if (discoveryHash == 0u)
                return;

            NarrativeEvents.TryResolveDiscoveryId(discoveryHash, out string id);
            RecordDiscoveryIdentity(discoveryHash, id);
        }

        private void RecordDiscoveryIdentity(uint discoveryHash, string discoveryId)
        {
            if (discoveryHash == 0u)
                return;

            if (!_discoveredHashLookup.Contains(discoveryHash) &&
                _discoveredHashLookup.Count >= SaveData.MaxNarrativeDiscoveries)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _registryOverflowWarningHash,
                    _blackBoxFaultContextHash ^ discoveryHash,
                    SaveData.MaxNarrativeDiscoveries);
                return;
            }

            if (_discoveredHashLookup.Add(discoveryHash))
                MarkNativePoiTriggeredByHash(discoveryHash);

            if (string.IsNullOrWhiteSpace(discoveryId) || _discoveredIdLookup.Contains(discoveryId))
                return;

            if (discoveredIds.Count >= SaveData.MaxNarrativeDiscoveries)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _registryOverflowWarningHash,
                    _blackBoxFaultContextHash ^ discoveryHash,
                    SaveData.MaxNarrativeDiscoveries);
                return;
            }

            _discoveredIdLookup.Add(discoveryId);
            discoveredIds.Add(discoveryId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Narrative] Discovery logged.");
#endif
        }

        private void HandleRareDiscoveryRequest(Vector3 hintPosition)
        {
            _rareDiscoveryRequested = true;

            IFirstHourReadModel firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHourReadModel;
            if (firstHourDirector != null &&
                !firstHourDirector.IsFirstHourMilestoneComplete((int)FirstHourMilestone.FirstModule))
            {
                return;
            }

            AtlasSignalSystem atlasSignalSystem = Hecton8.Core.GlobalRegistry.AtlasSignal;
            bool rebroadcastedPulse = CanRebroadcastAtlasRareDiscoveryPulse(atlasSignalSystem);
            if (rebroadcastedPulse)
                AtlasSignalEvents.TryRaisePulse(atlasSignalSystem.CurrentStrength);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Narrative] Rare discovery requested.");
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
            if (discoveredIds == null)
                return;

            int writeIndex = 0;
            for (int i = 0; i < discoveredIds.Count; i++)
            {
                string discoveryId = discoveredIds[i];
                if (string.IsNullOrWhiteSpace(discoveryId) || !_discoveredIdLookup.Add(discoveryId))
                    continue;

                if (writeIndex >= SaveData.MaxNarrativeDiscoveries)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _registryOverflowWarningHash,
                        _blackBoxFaultContextHash,
                        discoveredIds.Count);
                    break;
                }

                uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(discoveryId);
                if (discoveryHash != 0u)
                {
                    _discoveredHashLookup.Add(discoveryHash);
                }

                if (writeIndex != i)
                    discoveredIds[writeIndex] = discoveryId;

                writeIndex++;
            }

            if (writeIndex < discoveredIds.Count)
                discoveredIds.RemoveRange(writeIndex, discoveredIds.Count - writeIndex);

            SyncAupNarrativePoiVaultStateFromMask();
        }

        private void MarkNativePoiTriggeredByHash(uint discoveryHash)
        {
            MarkAupNarrativePoiTriggeredByHash(discoveryHash);
        }
    }
}
