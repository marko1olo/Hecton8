using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Scavenging;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;

namespace Hecton8.World
{
    /// <summary>
    /// Delayed regrowth owner for harvested kelp and sargassum flora instances.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-119)]
    public sealed class FloraRegrowthDirector : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const float RegrowthDelaySeconds = 4f * 60f * 60f;
        private const float DefaultRegrowthDurationSeconds = 90f;
        private const float FloraGrowthFrostTickIntervalSeconds = 10f;
        private const int DefaultTrackedRegrowthCapacity = 2048;
        private const float SeedFlightDurationSeconds = 60f;
        private const float SeedSproutDelaySeconds = 2f * 60f * 60f;
        private const float SeedSinkVelocityMetersPerSecond = 0.06f;
        private const float SeedFlowScale = 0.72f;
        private const float SeedSlopeSampleDistance = 1.25f;
        private const float MinimumSeedNormalY = 0.8660254f;
        private const int SeedsPerSargassumCluster = 3;
        private const float CanopyShadowRadiusMeters = 6f;
        private const float CanopyVerticalMinMeters = 1.25f;
        private const float CanopyVerticalMaxMeters = 28f;
        private const float CanopyMinHeightScale = 0.45f;
        private const float LightStarvationStrength = 0.85f;
        private const int MaxSymbioticFungalNodes = 128;
        private const float SymbioticFungalRootRadiusMeters = 15f;
        private const float DefaultSymbioticGrowthMultiplier = 2f;
        private const float DefaultSymbioticBuffDurationSeconds = 900f;
        private const ulong FungalStalkTemplateHash = 0xFD5A46CCUL;
        private const ulong AcidShroomTemplateHash = 0xB796CF49UL;
        private const ulong BlindcapTemplateHash = 0x1FB3740AUL;
        private const byte StateWaiting = 0;
        private const byte StateActive = 1;
        private const string NativeMemoryOwner = nameof(FloraRegrowthDirector);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct FloraRegrowthState
        {
            [FieldOffset(32)]
            public uint InstanceUid;
            [FieldOffset(0)]
            public ulong TemplateHash;
            [FieldOffset(8)]
            public float3 RuntimePosition;
            [FieldOffset(20)]
            public float EligiblePlayTime;
            [FieldOffset(24)]
            public float RegrowthStartPlayTime;
            [FieldOffset(28)]
            public float RegrowthDurationSeconds;
            [FieldOffset(36)]
            public byte State;
            [FieldOffset(37)]
            public byte SeenThisScan;
            [FieldOffset(38)]
            public ushort Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct SeedFlightState
        {
            [FieldOffset(24)]
            public uint SeedInstanceUid;
            [FieldOffset(0)]
            public ulong TemplateHash;
            [FieldOffset(8)]
            public float3 Position;
            [FieldOffset(20)]
            public float ElapsedSeconds;
            [FieldOffset(28)]
            public byte Landed;
            [FieldOffset(29)]
            public byte Reserved0;
            [FieldOffset(30)]
            public ushort Reserved1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 56)]
        private struct FloraMaturationState
        {
            [FieldOffset(44)]
            public uint InstanceUid;
            [FieldOffset(0)]
            public ulong TemplateHash;
            [FieldOffset(8)]
            public float3 RuntimePosition;
            [FieldOffset(20)]
            public float SpawnPlayTimeSeconds;
            [FieldOffset(24)]
            public float GrowthDurationSeconds;
            [FieldOffset(28)]
            public float HeightScale;
            [FieldOffset(32)]
            public float WidthScale;
            [FieldOffset(36)]
            public float ExternalShadeOcclusion01;
            [FieldOffset(40)]
            public float RadiationGrowthMultiplier;
            [FieldOffset(48)]
            public int TypeId;
            [FieldOffset(52)]
            public byte SeenThisScan;
            [FieldOffset(53)]
            public byte Reserved0;
            [FieldOffset(54)]
            public ushort Reserved1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct FloraMaturationResult
        {
            [FieldOffset(0)]
            public uint InstanceUid;
            [FieldOffset(4)]
            public float Progress01;
            [FieldOffset(8)]
            public float GrowthMultiplier;
            [FieldOffset(12)]
            public float ScaleMultiplier;
            [FieldOffset(16)]
            public float ResourceYieldMultiplier;
            [FieldOffset(20)]
            public uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct SymbioticFungalNodeState
        {
            [FieldOffset(20)]
            public uint InstanceUid;
            [FieldOffset(0)]
            public ulong TemplateHash;
            [FieldOffset(8)]
            public float3 RuntimePosition;
            [FieldOffset(24)]
            public byte Active;
            [FieldOffset(25)]
            public byte Reserved0;
            [FieldOffset(26)]
            public ushort Reserved1;
            [FieldOffset(28)]
            public uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct SymbioticFungalBuffState
        {
            [FieldOffset(0)]
            public uint InstanceUid;
            [FieldOffset(4)]
            public float ExpirePlayTimeSeconds;
            [FieldOffset(8)]
            public float GrowthMultiplier;
            [FieldOffset(12)]
            public float Reserved0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateMaturationJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<FloraMaturationState> States;
            [ReadOnly, NoAlias] public NativeArray<SymbioticFungalBuffState> SymbioticBuffs;
            public float CurrentPlayTimeSeconds;
            public float CanopyShadowRadiusMeters;
            public float CanopyVerticalMinMeters;
            public float CanopyVerticalMaxMeters;
            public float CanopyMinHeightScale;
            public float LightStarvationStrength;
            public float LunarResonanceGrowthMultiplier;
            [WriteOnly, NoAlias] public NativeArray<FloraMaturationResult> Results;

            public void Execute(int index)
            {
                if (!States.IsCreated ||
                    !Results.IsCreated ||
                    index < 0 ||
                    index >= States.Length ||
                    index >= Results.Length)
                {
                    return;
                }

                FloraMaturationState state = States[index];
                float durationSeconds = math.max(1f, state.GrowthDurationSeconds);
                float ageSeconds = math.max(0f, CurrentPlayTimeSeconds - state.SpawnPlayTimeSeconds);
                float growthRateMultiplier = ResolveSymbioticGrowthMultiplier(state.InstanceUid) *
                                             math.max(1f, LunarResonanceGrowthMultiplier) *
                                             math.max(1f, state.RadiationGrowthMultiplier);
                float progress01 = math.saturate((ageSeconds / durationSeconds) * growthRateMultiplier);
                float maturationMultiplier = ResolveMaturationMultiplier(progress01);
                float growthMultiplier = ResolveLightStarvationGrowthMultiplier(index, state, progress01);
                Results[index] = new FloraMaturationResult
                {
                    InstanceUid = state.InstanceUid,
                    Progress01 = progress01,
                    GrowthMultiplier = growthMultiplier,
                    ScaleMultiplier = maturationMultiplier,
                    ResourceYieldMultiplier = progress01
                };
            }

            private static float ResolveMaturationMultiplier(float progress01)
            {
                float clampedProgress = math.saturate(progress01);
                float smoothProgress = clampedProgress * clampedProgress * (3f - (2f * clampedProgress));
                return math.lerp(0.1f, 1f, smoothProgress);
            }

            private float ResolveSymbioticGrowthMultiplier(uint instanceUid)
            {
                if (instanceUid == 0u || !SymbioticBuffs.IsCreated)
                    return 1f;

                float multiplier = 1f;
                for (int i = 0; i < SymbioticBuffs.Length; i++)
                {
                    SymbioticFungalBuffState buff = SymbioticBuffs[i];
                    if (buff.InstanceUid != instanceUid || CurrentPlayTimeSeconds > buff.ExpirePlayTimeSeconds)
                        continue;

                    multiplier = math.max(multiplier, math.max(1f, buff.GrowthMultiplier));
                }

                return multiplier;
            }

            private float ResolveLightStarvationGrowthMultiplier(int index, FloraMaturationState undergrowth, float progress01)
            {
                if (undergrowth.ExternalShadeOcclusion01 > 0.01f && !IsCanopyType(undergrowth.TypeId))
                    return -math.saturate(math.max(undergrowth.ExternalShadeOcclusion01, LightStarvationStrength));

                if (undergrowth.TypeId != (int)HectonVegetationInstanceType.Grass ||
                    math.abs(undergrowth.HeightScale) <= 0.0001f)
                {
                    return progress01;
                }

                float bestOcclusion01 = 0f;
                float verticalRange = math.max(0.001f, CanopyVerticalMaxMeters - CanopyVerticalMinMeters);
                for (int canopyIndex = 0; canopyIndex < States.Length; canopyIndex++)
                {
                    if (canopyIndex == index)
                        continue;

                    FloraMaturationState canopy = States[canopyIndex];
                    if (!IsCanopyType(canopy.TypeId) ||
                        math.abs(canopy.HeightScale) < CanopyMinHeightScale)
                    {
                        continue;
                    }

                    float3 delta = canopy.RuntimePosition - undergrowth.RuntimePosition;
                    float verticalDelta = delta.y;
                    if (verticalDelta < CanopyVerticalMinMeters || verticalDelta > CanopyVerticalMaxMeters)
                        continue;

                    float radius = math.max(0.25f, CanopyShadowRadiusMeters * math.max(1f, math.abs(canopy.WidthScale)));
                    float radiusSq = radius * radius;
                    float planarDistanceSq = (delta.x * delta.x) + (delta.z * delta.z);
                    if (planarDistanceSq > radiusSq)
                        continue;

                    float planarOcclusion01 = 1f - math.saturate(planarDistanceSq / radiusSq);
                    float verticalOcclusion01 = 1f - math.saturate((verticalDelta - CanopyVerticalMinMeters) / verticalRange);
                    float heightOcclusion01 = math.saturate((math.abs(canopy.HeightScale) - CanopyMinHeightScale) / math.max(0.001f, 1f - CanopyMinHeightScale));
                    float occlusion01 = planarOcclusion01 *
                                         math.max(0.25f, verticalOcclusion01) *
                                         math.max(0.25f, heightOcclusion01);
                    bestOcclusion01 = math.max(bestOcclusion01, occlusion01);
                }

                return bestOcclusion01 > 0.0001f
                    ? -math.saturate(bestOcclusion01 * math.max(0.01f, LightStarvationStrength))
                    : progress01;
            }

            private static bool IsCanopyType(int typeId)
            {
                return typeId == (int)HectonVegetationInstanceType.Sargassum ||
                       typeId == (int)HectonVegetationInstanceType.GiantKelp;
            }
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
        private NativeList<FloraMaturationState> _maturationStates;
        private NativeHashMap<uint, int> _maturationIndexByInstanceUid;
        private NativeArray<FloraMaturationResult> _maturationResults;
        private NativeList<SymbioticFungalNodeState> _symbioticFungalNodes;
        private NativeList<SymbioticFungalBuffState> _symbioticFungalBuffs;
        private JobHandle _maturationJobHandle;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private ISaveService _saveService;
        private bool _maturationJobScheduled;
        private float _lunarResonanceExpirePlayTimeSeconds;
        private float _lunarResonanceGrowthMultiplier = 1f;
        private float _nextFloraGrowthFrostTickPlayTime;
        private float _lastSeedPlayTime;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _originShiftRegistered;
        private bool _hotSwapRegistered;

        private void Awake()
        {
            ResolveLocalComponentReferences();
            CacheRegistryServicesCold();

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
            _maturationStates = new NativeList<FloraMaturationState>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeList<FloraMaturationState>[2048] - live flora maturation state lane keyed by deterministic flora uid - owner: FloraRegrowthDirector
            _maturationIndexByInstanceUid = new NativeHashMap<uint, int>(
                DefaultTrackedRegrowthCapacity,
                Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[2048] - maturation state lookup keyed by deterministic flora uid - owner: FloraRegrowthDirector
            _symbioticFungalNodes = new NativeList<SymbioticFungalNodeState>(
                MaxSymbioticFungalNodes,
                Allocator.Persistent); // COLD ALLOC: NativeList<SymbioticFungalNodeState>[128] - bounded fungal root-radius nodes - owner: FloraRegrowthDirector
            _symbioticFungalBuffs = new NativeList<SymbioticFungalBuffState>(
                MaxSymbioticFungalNodes,
                Allocator.Persistent); // COLD ALLOC: NativeList<SymbioticFungalBuffState>[128] - active fungal growth buffs consumed by maturation Burst job - owner: FloraRegrowthDirector
            _lastSeedPlayTime = GetCurrentPlayTimeSeconds();
            RegisterNativeMemorySentinel();
        }

        private void ResolveLocalComponentReferences()
        {
            if (destructibleOrganicManager == null)
                TryGetComponent(out destructibleOrganicManager);

            if (vegetationBridge == null)
                TryGetComponent(out vegetationBridge);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ResolveLocalComponentReferences();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();

            TryRegisterDispatcherLanes();

            if (!_originShiftRegistered)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _originShiftRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
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

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }

            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }

            TryUnregisterHotSwapListener();
            JobHandle disposeHandle = _maturationJobScheduled ? _maturationJobHandle : default;
            DisposeNativeList(ref _destroyedFloraScratch, nameof(_destroyedFloraScratch));
            DisposeNativeList(ref _pendingSeedScratch, nameof(_pendingSeedScratch));
            DisposeNativeList(ref _regrowthStates, nameof(_regrowthStates));
            DisposeNativeHashMap(ref _stateIndexByInstanceUid, nameof(_stateIndexByInstanceUid));
            DisposeNativeList(ref _seedFlightStates, nameof(_seedFlightStates));
            DisposeNativeHashMap(ref _seedFlightIndexByUid, nameof(_seedFlightIndexByUid));
            DisposeNativeHashMap(ref _seedEmissionByDestroyedUid, nameof(_seedEmissionByDestroyedUid));
            DisposeNativeArray(ref _maturationResults, disposeHandle, _maturationJobScheduled);
            DisposeNativeList(ref _maturationStates, nameof(_maturationStates), disposeHandle, _maturationJobScheduled);
            DisposeNativeHashMap(ref _maturationIndexByInstanceUid, nameof(_maturationIndexByInstanceUid), disposeHandle, _maturationJobScheduled);
            DisposeNativeList(ref _symbioticFungalBuffs, nameof(_symbioticFungalBuffs), disposeHandle, _maturationJobScheduled);
            DisposeNativeList(ref _symbioticFungalNodes, nameof(_symbioticFungalNodes));
        }

        private void CacheRegistryServicesCold()
        {
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _saveService = GlobalRegistry.Save;
        }

        private void TryRegisterDispatcherLanes()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_tickRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_slowTickRegistered)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = GlobalRegistry.SlowTickables.Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }
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
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    _saveService = currentService as ISaveService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryRegisterDispatcherLanes();
                    break;
            }
        }

        private void SyncMaturationStates(PersistentWorldRegistry registry, float currentPlayTime)
        {
            if (registry == null ||
                destructibleOrganicManager == null ||
                vegetationBridge == null ||
                !_maturationStates.IsCreated ||
                !_maturationIndexByInstanceUid.IsCreated)
            {
                return;
            }

            for (int i = 0; i < _maturationStates.Length; i++)
            {
                FloraMaturationState state = _maturationStates[i];
                state.SeenThisScan = 0;
                _maturationStates[i] = state;
            }

            if (_symbioticFungalNodes.IsCreated)
                _symbioticFungalNodes.Clear();

            SyncMaturationStatesForPayload(registry, currentPlayTime, underwater: false);
            SyncMaturationStatesForPayload(registry, currentPlayTime, underwater: true);

            for (int i = _maturationStates.Length - 1; i >= 0; i--)
            {
                if (_maturationStates[i].SeenThisScan == 0)
                    RemoveMaturationStateAtSwapBack(i);
            }
        }

        private void SyncMaturationStatesForPayload(PersistentWorldRegistry registry, float currentPlayTime, bool underwater)
        {
            if (vegetationBridge == null || destructibleOrganicManager == null)
                return;

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
            if (!hasPayload || count <= 0)
                return;

            NativeArray<int> semanticTypes;
            int semanticCount;
            bool hasSemanticPayload = underwater
                ? vegetationBridge.TryGetActiveUnderwaterSemanticPayload(out semanticTypes, out _, out semanticCount)
                : vegetationBridge.TryGetActiveSurfaceSemanticPayload(out semanticTypes, out _, out semanticCount);
            if (!hasSemanticPayload)
                return;

            int upperBound = math.min(count, math.min(matrices.Length, math.min(metadata.Length, math.min(types.Length, math.min(semanticTypes.Length, semanticCount)))));
            for (int i = 0; i < upperBound; i++)
            {
                if (!destructibleOrganicManager.TryResolveFloraGrowthDescriptor(
                        matrices[i],
                        metadata[i],
                        types[i],
                        semanticTypes[i],
                        out uint instanceUid,
                        out ulong templateHash,
                        out float growthTimeSeconds))
                {
                    continue;
                }

                if (metadata[i].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    math.abs(metadata[i].HeightScale) <= 0.0001f)
                {
                    continue;
                }

                Vector3 runtimePosition = ExtractTranslation(matrices[i]);
                float externalShadeOcclusion01 = ResolveMigratorySargassumShadeOcclusion(runtimePosition);
                float radiationGrowthMultiplier = ResolveRadiationGrowthMultiplier(runtimePosition);
                float spawnPlayTimeSeconds;
                if (!registry.TryGetFloraSpawnTimestamp(instanceUid, out spawnPlayTimeSeconds))
                {
                    spawnPlayTimeSeconds = Mathf.Max(0f, currentPlayTime - Mathf.Max(1f, growthTimeSeconds));
                    registry.TryRegisterFloraSpawnTimestamp(instanceUid, runtimePosition, spawnPlayTimeSeconds);
                }

                if (_maturationIndexByInstanceUid.TryGetValue(instanceUid, out int existingIndex))
                {
                    FloraMaturationState state = _maturationStates[existingIndex];
                    state.TemplateHash = templateHash;
                    state.RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                    state.SpawnPlayTimeSeconds = spawnPlayTimeSeconds;
                    state.GrowthDurationSeconds = Mathf.Max(1f, growthTimeSeconds);
                    state.HeightScale = Mathf.Abs(metadata[i].HeightScale);
                    state.WidthScale = Mathf.Abs(metadata[i].WidthScale);
                    state.ExternalShadeOcclusion01 = externalShadeOcclusion01;
                    state.RadiationGrowthMultiplier = radiationGrowthMultiplier;
                    state.TypeId = types[i];
                    state.SeenThisScan = 1;
                    _maturationStates[existingIndex] = state;
                    TryRegisterSymbioticFungalNode(in state);
                    continue;
                }

                if (_maturationStates.Length >= _maturationStates.Capacity)
                    break;

                FloraMaturationState newState = new FloraMaturationState
                {
                    InstanceUid = instanceUid,
                    TemplateHash = templateHash,
                    RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                    SpawnPlayTimeSeconds = spawnPlayTimeSeconds,
                    GrowthDurationSeconds = Mathf.Max(1f, growthTimeSeconds),
                    HeightScale = Mathf.Abs(metadata[i].HeightScale),
                    WidthScale = Mathf.Abs(metadata[i].WidthScale),
                    ExternalShadeOcclusion01 = externalShadeOcclusion01,
                    RadiationGrowthMultiplier = radiationGrowthMultiplier,
                    TypeId = types[i],
                    SeenThisScan = 1,
                    Reserved0 = 0,
                    Reserved1 = 0
                };
                _maturationIndexByInstanceUid.TryAdd(instanceUid, _maturationStates.Length);
                _maturationStates.AddNoResize(newState);
                TryRegisterSymbioticFungalNode(in newState);
            }
        }

        private void ScheduleMaturationJob(float currentPlayTime)
        {
            if (_maturationJobScheduled || !_maturationStates.IsCreated || _maturationStates.Length <= 0)
                return;

            EnsureMaturationResultCapacity(_maturationStates.Length);
            if (!_maturationResults.IsCreated || _maturationResults.Length < _maturationStates.Length)
                return;

            PruneExpiredSymbioticBuffs(currentPlayTime);
            _maturationJobHandle = new EvaluateMaturationJob
            {
                States = _maturationStates.AsArray(),
                SymbioticBuffs = _symbioticFungalBuffs.IsCreated ? _symbioticFungalBuffs.AsArray() : default,
                CurrentPlayTimeSeconds = currentPlayTime,
                CanopyShadowRadiusMeters = CanopyShadowRadiusMeters,
                CanopyVerticalMinMeters = CanopyVerticalMinMeters,
                CanopyVerticalMaxMeters = CanopyVerticalMaxMeters,
                CanopyMinHeightScale = CanopyMinHeightScale,
                LightStarvationStrength = LightStarvationStrength,
                LunarResonanceGrowthMultiplier = ResolveLunarResonanceGrowthMultiplier(currentPlayTime),
                Results = _maturationResults
            }.Schedule(_maturationStates.Length, 32);
            _maturationJobScheduled = true;
        }

        private float ResolveMigratorySargassumShadeOcclusion(Vector3 runtimePosition)
        {
            WorldProceduralScatterDirector scatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;
            if (scatterDirector == null ||
                !scatterDirector.TryEvaluateMigratorySargassumShade(runtimePosition, out float occlusion01))
            {
                return 0f;
            }

            return Mathf.Clamp01(occlusion01);
        }

        private static float ResolveRadiationGrowthMultiplier(Vector3 runtimePosition)
        {
            float radiation01 = HectonHazardManager.GetHazardIntensity(runtimePosition, HazardType.Radiation);
            return radiation01 > 0.0001f ? 3f : 1f;
        }

        private void TryRegisterSymbioticFungalNode(in FloraMaturationState state)
        {
            if (!_symbioticFungalNodes.IsCreated ||
                _symbioticFungalNodes.Length >= _symbioticFungalNodes.Capacity ||
                !IsSymbioticFungalTemplateHash(state.TemplateHash))
            {
                return;
            }

            _symbioticFungalNodes.AddNoResize(new SymbioticFungalNodeState
            {
                InstanceUid = state.InstanceUid,
                TemplateHash = state.TemplateHash,
                RuntimePosition = state.RuntimePosition,
                Active = 1,
                Reserved0 = 0,
                Reserved1 = 0
            });
        }

        private static bool IsSymbioticFungalTemplateHash(ulong templateHash)
        {
            return templateHash == FungalStalkTemplateHash ||
                   templateHash == AcidShroomTemplateHash ||
                   templateHash == BlindcapTemplateHash;
        }

        /// <summary>
        /// Applies a simple root-radius fungal growth buff around the fertilized node.
        /// </summary>
        /// <param name="instanceUid">Stable flora instance uid of the fertilized fungal node.</param>
        /// <param name="growthMultiplier">Maturation-rate multiplier applied to connected fungal nodes.</param>
        /// <param name="durationSeconds">Buff duration in play-time seconds.</param>
        /// <returns>True when at least one fungal node received a radius buff.</returns>
        public bool TryApplySymbioticFungalFertilizer(uint instanceUid, float growthMultiplier = DefaultSymbioticGrowthMultiplier, float durationSeconds = DefaultSymbioticBuffDurationSeconds)
        {
            if (instanceUid == 0u ||
                !_symbioticFungalNodes.IsCreated ||
                _symbioticFungalNodes.Length <= 0)
            {
                return false;
            }

            float3 rootPosition = float3.zero;
            bool foundRoot = false;
            for (int i = 0; i < _symbioticFungalNodes.Length; i++)
            {
                SymbioticFungalNodeState node = _symbioticFungalNodes[i];
                if (node.Active == 0 || node.InstanceUid != instanceUid)
                    continue;

                rootPosition = node.RuntimePosition;
                foundRoot = true;
                break;
            }

            if (!foundRoot)
                return false;

            return ApplySymbioticRadiusBuff(
                rootPosition,
                Mathf.Max(1f, growthMultiplier),
                Mathf.Max(0.1f, durationSeconds));
        }

        /// <summary>
        /// Resolves the nearest fungal root node in a runtime radius and applies a fixed 15 m growth buff.
        /// </summary>
        /// <param name="runtimePosition">Runtime-space fertilization point.</param>
        /// <param name="radiusMeters">Search radius for the nearest fungal node.</param>
        /// <param name="growthMultiplier">Maturation-rate multiplier applied to connected fungal nodes.</param>
        /// <param name="durationSeconds">Buff duration in play-time seconds.</param>
        /// <returns>True when at least one fungal node received a radius buff.</returns>
        public bool TryApplySymbioticFungalFertilizer(Vector3 runtimePosition, float radiusMeters, float growthMultiplier = DefaultSymbioticGrowthMultiplier, float durationSeconds = DefaultSymbioticBuffDurationSeconds)
        {
            if (radiusMeters <= 0f || !_symbioticFungalNodes.IsCreated || _symbioticFungalNodes.Length <= 0)
                return false;

            float3 origin = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            float bestDistanceSq = radiusMeters * radiusMeters;
            float3 rootPosition = float3.zero;
            bool foundRoot = false;
            for (int i = 0; i < _symbioticFungalNodes.Length; i++)
            {
                SymbioticFungalNodeState node = _symbioticFungalNodes[i];
                if (node.Active == 0)
                    continue;

                float distanceSq = math.distancesq(origin, node.RuntimePosition);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                rootPosition = node.RuntimePosition;
                foundRoot = true;
            }

            return foundRoot &&
                   ApplySymbioticRadiusBuff(rootPosition, Mathf.Max(1f, growthMultiplier), Mathf.Max(0.1f, durationSeconds));
        }

        public void ApplyLunarResonance(float growthMultiplier, float durationSeconds)
        {
            if (growthMultiplier <= 1f ||
                durationSeconds <= 0f ||
                !float.IsFinite(growthMultiplier) ||
                !float.IsFinite(durationSeconds))
            {
                return;
            }

            float currentPlayTime = GetCurrentPlayTimeSeconds();
            _lunarResonanceGrowthMultiplier = Mathf.Max(_lunarResonanceGrowthMultiplier, growthMultiplier);
            _lunarResonanceExpirePlayTimeSeconds = Mathf.Max(
                _lunarResonanceExpirePlayTimeSeconds,
                currentPlayTime + durationSeconds);
        }

        private float ResolveLunarResonanceGrowthMultiplier(float currentPlayTime)
        {
            if (currentPlayTime > _lunarResonanceExpirePlayTimeSeconds)
            {
                _lunarResonanceGrowthMultiplier = 1f;
                return 1f;
            }

            return Mathf.Max(1f, _lunarResonanceGrowthMultiplier);
        }

        private bool ApplySymbioticRadiusBuff(float3 rootPosition, float growthMultiplier, float durationSeconds)
        {
            if (!_symbioticFungalNodes.IsCreated || !_symbioticFungalBuffs.IsCreated)
                return false;

            float currentPlayTime = GetCurrentPlayTimeSeconds();
            float expireTime = currentPlayTime + durationSeconds;
            float radiusSq = SymbioticFungalRootRadiusMeters * SymbioticFungalRootRadiusMeters;
            int appliedCount = 0;
            PruneExpiredSymbioticBuffs(currentPlayTime);
            for (int i = 0; i < _symbioticFungalNodes.Length; i++)
            {
                SymbioticFungalNodeState node = _symbioticFungalNodes[i];
                if (node.Active == 0 || node.InstanceUid == 0u)
                    continue;

                if (math.distancesq(rootPosition, node.RuntimePosition) > radiusSq)
                    continue;

                UpsertSymbioticFungalBuff(new SymbioticFungalBuffState
                {
                    InstanceUid = node.InstanceUid,
                    ExpirePlayTimeSeconds = expireTime,
                    GrowthMultiplier = growthMultiplier,
                    Reserved0 = 0f
                });
                appliedCount++;
            }

            return appliedCount > 0;
        }

        private static bool TryCompleteVegetationJob(ref JobHandle handle, bool forceComplete)
        {
            return DispatcherJobSwap.TryComplete(ref handle, forceComplete);
        }

        private void UpsertSymbioticFungalBuff(SymbioticFungalBuffState buff)
        {
            for (int i = 0; i < _symbioticFungalBuffs.Length; i++)
            {
                if (_symbioticFungalBuffs[i].InstanceUid != buff.InstanceUid)
                    continue;

                _symbioticFungalBuffs[i] = buff;
                return;
            }

            if (_symbioticFungalBuffs.Length < _symbioticFungalBuffs.Capacity)
                _symbioticFungalBuffs.AddNoResize(buff);
        }

        private void PruneExpiredSymbioticBuffs(float currentPlayTime)
        {
            if (!_symbioticFungalBuffs.IsCreated)
                return;

            for (int i = _symbioticFungalBuffs.Length - 1; i >= 0; i--)
            {
                SymbioticFungalBuffState buff = _symbioticFungalBuffs[i];
                if (buff.InstanceUid == 0u || currentPlayTime > buff.ExpirePlayTimeSeconds)
                    _symbioticFungalBuffs.RemoveAtSwapBack(i);
            }
        }

        private void CompleteMaturationJobIfNeeded(bool forceComplete = false)
        {
            if (!_maturationJobScheduled)
                return;

            if (!TryCompleteVegetationJob(ref _maturationJobHandle, forceComplete))
                return;

            _maturationJobScheduled = false;

            if (destructibleOrganicManager == null || !_maturationResults.IsCreated || !_maturationStates.IsCreated)
                return;

            int resultCount = math.min(_maturationStates.Length, _maturationResults.Length);
            for (int i = 0; i < resultCount; i++)
            {
                FloraMaturationResult result = _maturationResults[i];
                if (result.InstanceUid == 0u)
                    continue;

                if (result.GrowthMultiplier < -0.0001f)
                {
                    destructibleOrganicManager.TryApplyLightStarvation(result.InstanceUid, -result.GrowthMultiplier);
                    continue;
                }

                destructibleOrganicManager.TrySetMaturationProgress(
                    result.InstanceUid,
                    result.Progress01,
                    result.ScaleMultiplier,
                    result.ResourceYieldMultiplier);
            }
        }

        private void EnsureMaturationResultCapacity(int requiredCount)
        {
            if (requiredCount <= 0)
                return;

            if (_maturationResults.IsCreated && _maturationResults.Length >= requiredCount)
                return;

            if (_maturationResults.IsCreated)
                DisposeNativeArray(ref _maturationResults);

            _maturationResults = new NativeArray<FloraMaturationResult>(
                requiredCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<FloraMaturationResult>[requiredCount] - burst maturation result lane for slow-tick flora growth application - owner: FloraRegrowthDirector
            NativeMemorySentinel.RegisterNativeArray(_maturationResults, NativeMemoryOwner, nameof(_maturationResults), NativeMemoryLifetime);
        }

        private void RemoveMaturationStateAtSwapBack(int index)
        {
            if (!_maturationStates.IsCreated || !_maturationIndexByInstanceUid.IsCreated || index < 0 || index >= _maturationStates.Length)
                return;

            FloraMaturationState removed = _maturationStates[index];
            int lastIndex = _maturationStates.Length - 1;
            FloraMaturationState last = _maturationStates[lastIndex];
            _maturationStates.RemoveAtSwapBack(index);
            _maturationIndexByInstanceUid.Remove(removed.InstanceUid);

            if (index < lastIndex)
            {
                _maturationIndexByInstanceUid.Remove(last.InstanceUid);
                _maturationIndexByInstanceUid.TryAdd(last.InstanceUid, index);
            }
        }

        /// <summary>
        /// Advances active regrowth blends for already-eligible flora records.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_regrowthStates.IsCreated || !_stateIndexByInstanceUid.IsCreated || destructibleOrganicManager == null)
                return;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            float currentPlayTime = GetCurrentPlayTimeSeconds();
            UpdateSeedFlights(deltaTime);
            for (int i = _regrowthStates.Length - 1; i >= 0; i--)
            {
                FloraRegrowthState state = _regrowthStates[i];
                if (state.State != StateActive)
                    continue;

                float durationSeconds = Mathf.Max(1f, state.RegrowthDurationSeconds > 0f ? state.RegrowthDurationSeconds : DefaultRegrowthDurationSeconds);
                float progress01 = math.saturate((currentPlayTime - state.RegrowthStartPlayTime) / durationSeconds);
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
        /// Recovers completed flora maturation jobs inside the dispatcher late-frame swap window.
        /// </summary>
        public void LateFrameTick()
        {
            CompleteMaturationJobIfNeeded();
        }

        /// <summary>
        /// Re-bases cached runtime flora positions after the floating-origin system shifts the scene.
        /// </summary>
        /// <param name="shiftData">Origin-shift event emitted by <see cref="HectonFloatingOrigin"/>.</param>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.000001f)
                return;

            CompleteMaturationJobIfNeeded(forceComplete: true);
            float3 runtimeOffset = new float3(-shiftData.ShiftOffset.x, -shiftData.ShiftOffset.y, -shiftData.ShiftOffset.z);
            ApplyOriginShiftToCachedFloraState(runtimeOffset);
        }

        /// <summary>
        /// Scans persistent flora-destruction tombstones and starts delayed regrowth once the time gate opens.
        /// </summary>
        public void SlowTick()
        {
            if (!_destroyedFloraScratch.IsCreated || !_regrowthStates.IsCreated || !_stateIndexByInstanceUid.IsCreated)
                return;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null || destructibleOrganicManager == null)
                return;

            if (_maturationJobScheduled)
                return;

            float currentPlayTime = GetCurrentPlayTimeSeconds();
            if (currentPlayTime < _nextFloraGrowthFrostTickPlayTime)
                return;

            _nextFloraGrowthFrostTickPlayTime = currentPlayTime + FloraGrowthFrostTickIntervalSeconds;
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
                if (deltaRecord.InstanceUid == 0u)
                    continue;

                if (!destructibleOrganicManager.HasTemplatePersistentIdHash(deltaRecord.ItemPersistentIdHash))
                {
                    registry.TryClearDestroyedFlora(deltaRecord.InstanceUid);
                    continue;
                }

                if (!destructibleOrganicManager.IsMaterialClassRegrowable(deltaRecord.ItemPersistentIdHash))
                {
                    continue;
                }

                TryEmitSargassumSeeds(deltaRecord);

                Vector3 runtimePosition = ToRuntimePosition(deltaRecord.UnpackPosition(registry.ChunkSizeMeters));
                if (_stateIndexByInstanceUid.TryGetValue(deltaRecord.InstanceUid, out int stateIndex))
                {
                    FloraRegrowthState existing = _regrowthStates[stateIndex];
                    existing.TemplateHash = deltaRecord.ItemPersistentIdHash;
                    existing.RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                    existing.RegrowthDurationSeconds = destructibleOrganicManager.ResolveGrowthTimeSeconds(deltaRecord.ItemPersistentIdHash);
                    existing.SeenThisScan = 1;
                    if (existing.State == StateWaiting && currentPlayTime >= existing.EligiblePlayTime)
                    {
                        existing.State = StateActive;
                        existing.RegrowthStartPlayTime = currentPlayTime;
                        registry.TryRegisterFloraSpawnTimestamp(
                            existing.InstanceUid,
                            runtimePosition,
                            currentPlayTime);
                    }

                    _regrowthStates[stateIndex] = existing;
                    continue;
                }

                if (_regrowthStates.Length >= _regrowthStates.Capacity)
                    break;

                FloraRegrowthState newState = new FloraRegrowthState
                {
                    InstanceUid = deltaRecord.InstanceUid,
                    TemplateHash = deltaRecord.ItemPersistentIdHash,
                    RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                    EligiblePlayTime = currentPlayTime + RegrowthDelaySeconds,
                    RegrowthStartPlayTime = 0f,
                    RegrowthDurationSeconds = destructibleOrganicManager.ResolveGrowthTimeSeconds(deltaRecord.ItemPersistentIdHash),
                    State = StateWaiting,
                    SeenThisScan = 1,
                    Reserved0 = 0
                };

                _stateIndexByInstanceUid.TryAdd(newState.InstanceUid, _regrowthStates.Length);
                _regrowthStates.AddNoResize(newState);
            }

            _destroyedFloraScratch.Clear();
            registry.CopyFloraStateOverrideDeltas(_destroyedFloraScratch);
            for (int i = 0; i < _destroyedFloraScratch.Length; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = _destroyedFloraScratch[i];
                if (deltaRecord.InstanceUid == 0u)
                    continue;

                if (!destructibleOrganicManager.HasTemplatePersistentIdHash(deltaRecord.ItemPersistentIdHash))
                {
                    registry.TryClearFloraStateOverride(deltaRecord.InstanceUid);
                    continue;
                }

                if (!destructibleOrganicManager.IsMaterialClassRegrowable(deltaRecord.ItemPersistentIdHash))
                {
                    continue;
                }

                PersistentWorldRegistry.UnpackFloraStateOverride(deltaRecord.Quantity, out float normalizedHealth, out byte persistedHarvestState);
                if (!destructibleOrganicManager.IsBareHarvestState(persistedHarvestState))
                    continue;

                float durationSeconds = destructibleOrganicManager.ResolveGrowthTimeSeconds(deltaRecord.ItemPersistentIdHash);
                float regrowthProgress = math.saturate(normalizedHealth);
                float regrowthStartPlayTime = currentPlayTime - (Mathf.Max(1f, durationSeconds) * regrowthProgress);
                Vector3 runtimePosition = ToRuntimePosition(deltaRecord.UnpackPosition(registry.ChunkSizeMeters));
                if (_stateIndexByInstanceUid.TryGetValue(deltaRecord.InstanceUid, out int stateIndex))
                {
                    FloraRegrowthState existing = _regrowthStates[stateIndex];
                    existing.TemplateHash = deltaRecord.ItemPersistentIdHash;
                    existing.RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                    existing.RegrowthDurationSeconds = durationSeconds;
                    existing.State = StateActive;
                    existing.RegrowthStartPlayTime = regrowthStartPlayTime;
                    existing.EligiblePlayTime = currentPlayTime;
                    existing.SeenThisScan = 1;
                    _regrowthStates[stateIndex] = existing;
                    continue;
                }

                if (_regrowthStates.Length >= _regrowthStates.Capacity)
                    break;

                FloraRegrowthState bareState = new FloraRegrowthState
                {
                    InstanceUid = deltaRecord.InstanceUid,
                    TemplateHash = deltaRecord.ItemPersistentIdHash,
                    RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                    EligiblePlayTime = currentPlayTime,
                    RegrowthStartPlayTime = regrowthStartPlayTime,
                    RegrowthDurationSeconds = durationSeconds,
                    State = StateActive,
                    SeenThisScan = 1,
                    Reserved0 = 0
                };

                _stateIndexByInstanceUid.TryAdd(bareState.InstanceUid, _regrowthStates.Length);
                _regrowthStates.AddNoResize(bareState);
            }

            for (int i = _regrowthStates.Length - 1; i >= 0; i--)
            {
                FloraRegrowthState state = _regrowthStates[i];
                if (state.State == StateWaiting && state.SeenThisScan == 0)
                    RemoveStateAtSwapBack(i);
            }

            SyncMaturationStates(registry, currentPlayTime);
            ScheduleMaturationJob(currentPlayTime);
        }

        private void UpdateSeedFlights(float deltaTime)
        {
            if (!_seedFlightStates.IsCreated || _seedFlightStates.Length <= 0)
                return;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null)
                return;

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

            Vector3 landingPosition = new Vector3(state.Position.x, state.Position.y, state.Position.z);
            if (vegetationBridge == null)
                return false;

            if (vegetationBridge.TryGetCachedTerrainHeight(landingPosition.x, landingPosition.z, out float terrainHeight))
                landingPosition.y = terrainHeight + 0.08f;

            if (!vegetationBridge.TryPassTerrainNormalYThreshold(landingPosition, SeedSlopeSampleDistance, MinimumSeedNormalY))
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

            if (vegetationBridge == null ||
                _seedEmissionByDestroyedUid.ContainsKey(deltaRecord.InstanceUid))
            {
                return;
            }

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null)
                return;

            Vector3 basePosition = ToRuntimePosition(deltaRecord.UnpackPosition(registry.ChunkSizeMeters));
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

        private void ApplyOriginShiftToCachedFloraState(float3 runtimeOffset)
        {
            if (_regrowthStates.IsCreated)
            {
                for (int i = 0; i < _regrowthStates.Length; i++)
                {
                    FloraRegrowthState state = _regrowthStates[i];
                    state.RuntimePosition += runtimeOffset;
                    _regrowthStates[i] = state;
                }
            }

            if (_seedFlightStates.IsCreated)
            {
                for (int i = 0; i < _seedFlightStates.Length; i++)
                {
                    SeedFlightState state = _seedFlightStates[i];
                    state.Position += runtimeOffset;
                    _seedFlightStates[i] = state;
                }
            }

            if (_maturationStates.IsCreated)
            {
                for (int i = 0; i < _maturationStates.Length; i++)
                {
                    FloraMaturationState state = _maturationStates[i];
                    state.RuntimePosition += runtimeOffset;
                    _maturationStates[i] = state;
                }
            }

            if (_symbioticFungalNodes.IsCreated)
            {
                for (int i = 0; i < _symbioticFungalNodes.Length; i++)
                {
                    SymbioticFungalNodeState state = _symbioticFungalNodes[i];
                    state.RuntimePosition += runtimeOffset;
                    _symbioticFungalNodes[i] = state;
                }
            }
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeList(_destroyedFloraScratch, NativeMemoryOwner, nameof(_destroyedFloraScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_pendingSeedScratch, NativeMemoryOwner, nameof(_pendingSeedScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_regrowthStates, NativeMemoryOwner, nameof(_regrowthStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_stateIndexByInstanceUid, NativeMemoryOwner, nameof(_stateIndexByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_seedFlightStates, NativeMemoryOwner, nameof(_seedFlightStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_seedFlightIndexByUid, NativeMemoryOwner, nameof(_seedFlightIndexByUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_seedEmissionByDestroyedUid, NativeMemoryOwner, nameof(_seedEmissionByDestroyedUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_maturationStates, NativeMemoryOwner, nameof(_maturationStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_maturationIndexByInstanceUid, NativeMemoryOwner, nameof(_maturationIndexByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_symbioticFungalNodes, NativeMemoryOwner, nameof(_symbioticFungalNodes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_symbioticFungalBuffs, NativeMemoryOwner, nameof(_symbioticFungalBuffs), NativeMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency = default, bool deferDisposal = false) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (deferDisposal)
                array.Dispose(dependency);
            else
                array.Dispose();
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, string label, JobHandle dependency = default, bool deferDisposal = false) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            if (deferDisposal)
                list.Dispose(dependency);
            else
                list.Dispose();
            list = default;
        }

        private static void DisposeNativeHashMap<TKey, TValue>(ref NativeHashMap<TKey, TValue> map, string label, JobHandle dependency = default, bool deferDisposal = false)
            where TKey : unmanaged, System.IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeHashMap(NativeMemoryOwner, label);
            if (deferDisposal)
                map.Dispose(dependency);
            else
                map.Dispose();
            map = default;
        }

        private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
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
            float2 direction = ResolveSeedOctantDirection((int)(NextSeed01(ref state) * 7.999f));
            float radius = NextSeed01(ref state) * 1.65f;
            return new Vector3(direction.x * radius, 0.12f + (0.33f * NextSeed01(ref state)), direction.y * radius);
        }

        private static float2 ResolveSeedOctantDirection(int sector)
        {
            switch (sector & 7)
            {
                case 0:
                    return new float2(1f, 0f);
                case 1:
                    return new float2(0.70710677f, 0.70710677f);
                case 2:
                    return new float2(0f, 1f);
                case 3:
                    return new float2(-0.70710677f, 0.70710677f);
                case 4:
                    return new float2(-1f, 0f);
                case 5:
                    return new float2(-0.70710677f, -0.70710677f);
                case 6:
                    return new float2(0f, -1f);
                default:
                    return new float2(0.70710677f, -0.70710677f);
            }
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

        private float GetCurrentPlayTimeSeconds()
        {
            return _saveService != null
                ? _saveService.CurrentPlayTimeSeconds
                : Time.realtimeSinceStartup;
        }

        private static Vector3 ToRuntimePosition(AbsoluteUniversePosition position)
        {
            float3 runtimePosition = position.ToRuntimeFloat3();
            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }
    }
}
