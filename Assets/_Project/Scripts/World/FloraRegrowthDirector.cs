using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
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
    public sealed class FloraRegrowthDirector : MonoBehaviour, ITickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
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

        [SerializeField]
        [Tooltip("Runtime owner that mutates streamed flora metadata and harvest health state.")]
        private DestructibleOrganicManager destructibleOrganicManager;

        [SerializeField]
        [Tooltip("MapMagic vegetation bridge that owns abyssal flow and terrain-cache queries.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        private readonly PersistentWorldDeltaRecord[] _destroyedFloraScratch = new PersistentWorldDeltaRecord[DefaultTrackedRegrowthCapacity];
        private readonly PersistentWorldDeltaRecord[] _pendingSeedScratch = new PersistentWorldDeltaRecord[DefaultTrackedRegrowthCapacity];
        private readonly FloraRegrowthState[] _regrowthStates = new FloraRegrowthState[DefaultTrackedRegrowthCapacity];
        private int _regrowthStateCount;
        private readonly SeedFlightState[] _seedFlightStates = new SeedFlightState[DefaultTrackedRegrowthCapacity];
        private int _seedFlightStateCount;
        private readonly uint[] _seedEmissionDestroyedUids = new uint[DefaultTrackedRegrowthCapacity];
        private int _seedEmissionDestroyedUidCount;
        private readonly FloraMaturationState[] _maturationStates = new FloraMaturationState[DefaultTrackedRegrowthCapacity];
        private int _maturationStateCount;
        private readonly SymbioticFungalNodeState[] _symbioticFungalNodes = new SymbioticFungalNodeState[MaxSymbioticFungalNodes];
        private readonly SymbioticFungalBuffState[] _symbioticFungalBuffs = new SymbioticFungalBuffState[MaxSymbioticFungalNodes];
        private int _symbioticFungalNodeCount;
        private int _symbioticFungalBuffCount;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private ISaveService _saveService;
        private float _lunarResonanceExpirePlayTimeSeconds;
        private float _lunarResonanceGrowthMultiplier = 1f;
        private float _nextFloraGrowthFrostTickPlayTime;
        private float _lastSeedPlayTime;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _originShiftRegistered;
        private bool _hotSwapRegistered;

        private void Awake()
        {
            ResolveLocalComponentReferences();
            CacheRegistryServicesCold();

            _regrowthStateCount = 0;
            _seedFlightStateCount = 0;
            _maturationStateCount = 0;
            _symbioticFungalNodeCount = 0;
            _symbioticFungalBuffCount = 0;
            _lastSeedPlayTime = GetCurrentPlayTimeSeconds();
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

            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }

            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }

            TryUnregisterHotSwapListener();
            _regrowthStateCount = 0;
            _seedFlightStateCount = 0;
            _seedEmissionDestroyedUidCount = 0;
            _symbioticFungalNodeCount = 0;
            _symbioticFungalBuffCount = 0;
            _maturationStateCount = 0;
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
                _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_slowTickRegistered)
            {
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
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

                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterDispatcherLanes();
                    break;
            }
        }

        private void SyncMaturationStates(PersistentWorldRegistry registry, float currentPlayTime)
        {
            if (registry == null ||
                destructibleOrganicManager == null ||
                vegetationBridge == null)
            {
                return;
            }

            for (int i = 0; i < _maturationStateCount; i++)
            {
                FloraMaturationState state = _maturationStates[i];
                state.SeenThisScan = 0;
                _maturationStates[i] = state;
            }

            _symbioticFungalNodeCount = 0;

            SyncMaturationStatesForPayload(registry, currentPlayTime, underwater: false);
            SyncMaturationStatesForPayload(registry, currentPlayTime, underwater: true);

            for (int i = _maturationStateCount - 1; i >= 0; i--)
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

            NativeArray<int>.ReadOnly semanticTypes;
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

                if (TryFindMaturationStateIndex(instanceUid, out int existingIndex))
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

                if (_maturationStateCount >= _maturationStates.Length)
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
                _maturationStates[_maturationStateCount] = newState;
                _maturationStateCount++;
                TryRegisterSymbioticFungalNode(in newState);
            }
        }

        private void EvaluateMaturationStates(float currentPlayTime)
        {
            if (_maturationStateCount <= 0 ||
                destructibleOrganicManager == null)
            {
                return;
            }

            PruneExpiredSymbioticBuffs(currentPlayTime);
            float lunarGrowthMultiplier = ResolveLunarResonanceGrowthMultiplier(currentPlayTime);
            for (int i = 0; i < _maturationStateCount; i++)
            {
                FloraMaturationState state = _maturationStates[i];
                EvaluateMaturationState(in state, currentPlayTime, lunarGrowthMultiplier, out FloraMaturationResult result);
                ApplyMaturationResult(in result);
            }
        }

        private void EvaluateMaturationState(
            in FloraMaturationState state,
            float currentPlayTime,
            float lunarGrowthMultiplier,
            out FloraMaturationResult result)
        {
            float durationSeconds = math.max(1f, state.GrowthDurationSeconds);
            float ageSeconds = math.max(0f, currentPlayTime - state.SpawnPlayTimeSeconds);
            float growthRateMultiplier = ResolveSymbioticGrowthMultiplier(state.InstanceUid, currentPlayTime) *
                                         math.max(1f, lunarGrowthMultiplier) *
                                         math.max(1f, state.RadiationGrowthMultiplier);
            float progress01 = math.saturate((ageSeconds / durationSeconds) * growthRateMultiplier);
            result = new FloraMaturationResult
            {
                InstanceUid = state.InstanceUid,
                Progress01 = progress01,
                GrowthMultiplier = ResolveLightStarvationGrowthMultiplier(in state, progress01),
                ScaleMultiplier = ResolveMaturationMultiplier(progress01),
                ResourceYieldMultiplier = progress01,
                _pad0 = 0u
            };
        }

        private static float ResolveMaturationMultiplier(float progress01)
        {
            float clampedProgress = math.saturate(progress01);
            float smoothProgress = clampedProgress * clampedProgress * (3f - (2f * clampedProgress));
            return math.lerp(0.1f, 1f, smoothProgress);
        }

        private float ResolveSymbioticGrowthMultiplier(uint instanceUid, float currentPlayTime)
        {
            if (instanceUid == 0u || _symbioticFungalBuffCount <= 0)
                return 1f;

            float multiplier = 1f;
            for (int i = 0; i < _symbioticFungalBuffCount; i++)
            {
                SymbioticFungalBuffState buff = _symbioticFungalBuffs[i];
                if (buff.InstanceUid != instanceUid || currentPlayTime > buff.ExpirePlayTimeSeconds)
                    continue;

                multiplier = math.max(multiplier, math.max(1f, buff.GrowthMultiplier));
            }

            return multiplier;
        }

        private static float ResolveLightStarvationGrowthMultiplier(in FloraMaturationState state, float progress01)
        {
            if (state.ExternalShadeOcclusion01 <= 0.01f || IsCanopyType(state.TypeId))
                return progress01;

            return -math.saturate(math.max(state.ExternalShadeOcclusion01, LightStarvationStrength));
        }

        private static bool IsCanopyType(int typeId)
        {
            return typeId == (int)HectonVegetationInstanceType.Sargassum ||
                   typeId == (int)HectonVegetationInstanceType.GiantKelp;
        }

        private float ResolveMigratorySargassumShadeOcclusion(Vector3 runtimePosition)
        {
            WorldProceduralScatterDirector scatterDirector = null;
            if (!WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref scatterDirector) ||
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
            if (_symbioticFungalNodeCount >= _symbioticFungalNodes.Length ||
                !IsSymbioticFungalTemplateHash(state.TemplateHash))
            {
                return;
            }

            _symbioticFungalNodes[_symbioticFungalNodeCount] = new SymbioticFungalNodeState
            {
                InstanceUid = state.InstanceUid,
                TemplateHash = state.TemplateHash,
                RuntimePosition = state.RuntimePosition,
                Active = 1,
                Reserved0 = 0,
                Reserved1 = 0,
                _pad0 = 0u
            };
            _symbioticFungalNodeCount++;
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
                _symbioticFungalNodeCount <= 0)
            {
                return false;
            }

            float3 rootPosition = float3.zero;
            bool foundRoot = false;
            for (int i = 0; i < _symbioticFungalNodeCount; i++)
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
            if (radiusMeters <= 0f || _symbioticFungalNodeCount <= 0)
                return false;

            float3 origin = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            float bestDistanceSq = radiusMeters * radiusMeters;
            float3 rootPosition = float3.zero;
            bool foundRoot = false;
            for (int i = 0; i < _symbioticFungalNodeCount; i++)
            {
                SymbioticFungalNodeState node = _symbioticFungalNodes[i];
                if (node.Active == 0)
                    continue;

                float3 nodeDelta = origin - node.RuntimePosition;
                float distanceSq = math.lengthsq(nodeDelta);
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
            if (_symbioticFungalNodeCount <= 0)
                return false;

            float currentPlayTime = GetCurrentPlayTimeSeconds();
            float expireTime = currentPlayTime + durationSeconds;
            float radiusSq = SymbioticFungalRootRadiusMeters * SymbioticFungalRootRadiusMeters;
            int appliedCount = 0;
            PruneExpiredSymbioticBuffs(currentPlayTime);
            for (int i = 0; i < _symbioticFungalNodeCount; i++)
            {
                SymbioticFungalNodeState node = _symbioticFungalNodes[i];
                if (node.Active == 0 || node.InstanceUid == 0u)
                    continue;

                float3 rootDelta = rootPosition - node.RuntimePosition;
                if (math.lengthsq(rootDelta) > radiusSq)
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

        private void UpsertSymbioticFungalBuff(SymbioticFungalBuffState buff)
        {
            for (int i = 0; i < _symbioticFungalBuffCount; i++)
            {
                if (_symbioticFungalBuffs[i].InstanceUid != buff.InstanceUid)
                    continue;

                _symbioticFungalBuffs[i] = buff;
                return;
            }

            if (_symbioticFungalBuffCount >= _symbioticFungalBuffs.Length)
                return;

            _symbioticFungalBuffs[_symbioticFungalBuffCount] = buff;
            _symbioticFungalBuffCount++;
        }

        private void PruneExpiredSymbioticBuffs(float currentPlayTime)
        {
            if (_symbioticFungalBuffCount <= 0)
                return;

            for (int i = _symbioticFungalBuffCount - 1; i >= 0; i--)
            {
                SymbioticFungalBuffState buff = _symbioticFungalBuffs[i];
                if (buff.InstanceUid == 0u || currentPlayTime > buff.ExpirePlayTimeSeconds)
                    RemoveSymbioticFungalBuffAtSwapBack(i);
            }
        }

        private void RemoveSymbioticFungalBuffAtSwapBack(int index)
        {
            if (index < 0 || index >= _symbioticFungalBuffCount)
                return;

            int lastIndex = _symbioticFungalBuffCount - 1;
            _symbioticFungalBuffs[index] = _symbioticFungalBuffs[lastIndex];
            _symbioticFungalBuffs[lastIndex] = default;
            _symbioticFungalBuffCount = lastIndex;
        }

        private void ApplyMaturationResult(in FloraMaturationResult result)
        {
            if (result.InstanceUid == 0u || destructibleOrganicManager == null)
                return;

            if (result.GrowthMultiplier < -0.0001f)
            {
                destructibleOrganicManager.TryApplyLightStarvation(result.InstanceUid, -result.GrowthMultiplier);
                return;
            }

            destructibleOrganicManager.TrySetMaturationProgress(
                result.InstanceUid,
                result.Progress01,
                result.ScaleMultiplier,
                result.ResourceYieldMultiplier);
        }

        private void RemoveMaturationStateAtSwapBack(int index)
        {
            if (index < 0 || index >= _maturationStateCount)
                return;

            int lastIndex = _maturationStateCount - 1;
            _maturationStates[index] = _maturationStates[lastIndex];
            _maturationStates[lastIndex] = default;
            _maturationStateCount = lastIndex;
        }

        private bool TryFindMaturationStateIndex(uint instanceUid, out int index)
        {
            for (int i = 0; i < _maturationStateCount; i++)
            {
                if (_maturationStates[i].InstanceUid != instanceUid)
                    continue;

                index = i;
                return true;
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// Advances active regrowth blends for already-eligible flora records.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_regrowthStateCount <= 0 || destructibleOrganicManager == null)
                return;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            float currentPlayTime = GetCurrentPlayTimeSeconds();
            UpdateSeedFlights(deltaTime);
            for (int i = _regrowthStateCount - 1; i >= 0; i--)
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
        /// Re-bases cached runtime flora positions after the floating-origin system shifts the scene.
        /// </summary>
        /// <param name="shiftData">Origin-shift event emitted by <see cref="HectonFloatingOrigin"/>.</param>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            float3 runtimeOffset = new float3(-shiftOffset.x, -shiftOffset.y, -shiftOffset.z);
            ApplyOriginShiftToCachedFloraState(runtimeOffset);
        }

        /// <summary>
        /// Scans persistent flora-destruction tombstones and starts delayed regrowth once the time gate opens.
        /// </summary>
        public void SlowTick()
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null || destructibleOrganicManager == null)
                return;

            float currentPlayTime = GetCurrentPlayTimeSeconds();
            if (currentPlayTime < _nextFloraGrowthFrostTickPlayTime)
                return;

            _nextFloraGrowthFrostTickPlayTime = currentPlayTime + FloraGrowthFrostTickIntervalSeconds;
            UpdatePendingSeedTimers(registry, currentPlayTime);
            for (int i = 0; i < _regrowthStateCount; i++)
            {
                FloraRegrowthState state = _regrowthStates[i];
                state.SeenThisScan = 0;
                _regrowthStates[i] = state;
            }

            int destroyedFloraCount = registry.CopyDestroyedFloraDeltas(_destroyedFloraScratch, _destroyedFloraScratch.Length);
            for (int i = 0; i < destroyedFloraCount; i++)
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
                if (TryFindRegrowthStateIndex(deltaRecord.InstanceUid, out int stateIndex))
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

                if (_regrowthStateCount >= _regrowthStates.Length)
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

                _regrowthStates[_regrowthStateCount] = newState;
                _regrowthStateCount++;
            }

            int floraOverrideCount = registry.CopyFloraStateOverrideDeltas(_destroyedFloraScratch, _destroyedFloraScratch.Length);
            for (int i = 0; i < floraOverrideCount; i++)
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
                if (TryFindRegrowthStateIndex(deltaRecord.InstanceUid, out int stateIndex))
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

                if (_regrowthStateCount >= _regrowthStates.Length)
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

                _regrowthStates[_regrowthStateCount] = bareState;
                _regrowthStateCount++;
            }

            for (int i = _regrowthStateCount - 1; i >= 0; i--)
            {
                FloraRegrowthState state = _regrowthStates[i];
                if (state.State == StateWaiting && state.SeenThisScan == 0)
                    RemoveStateAtSwapBack(i);
            }

            SyncMaturationStates(registry, currentPlayTime);
            EvaluateMaturationStates(currentPlayTime);
        }

        private void UpdateSeedFlights(float deltaTime)
        {
            if (_seedFlightStateCount <= 0)
                return;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null)
                return;

            for (int i = _seedFlightStateCount - 1; i >= 0; i--)
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
            if (!destructibleOrganicManager.IsTemplateMaterialClass(deltaRecord.ItemPersistentIdHash, HarvestableTemplate.MaterialClass.Sargassum))
            {
                return;
            }

            if (vegetationBridge == null ||
                ContainsSeedEmissionUid(deltaRecord.InstanceUid))
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
                if (_seedFlightStateCount >= _seedFlightStates.Length ||
                    TryFindSeedFlightIndex(seedUid, out _))
                {
                    continue;
                }

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

                _seedFlightStates[_seedFlightStateCount] = state;
                _seedFlightStateCount++;
            }

            TryAddSeedEmissionUid(deltaRecord.InstanceUid);
        }

        private void UpdatePendingSeedTimers(PersistentWorldRegistry registry, float currentPlayTime)
        {
            float playDelta = Mathf.Max(0f, currentPlayTime - _lastSeedPlayTime);
            _lastSeedPlayTime = currentPlayTime;

            int pendingSeedCount = registry.CopyPendingFloraSeedDeltas(_pendingSeedScratch, _pendingSeedScratch.Length);
            ushort elapsedSeconds = (ushort)Mathf.Clamp(Mathf.RoundToInt(playDelta), 0, ushort.MaxValue);
            if (elapsedSeconds == 0)
                return;

            for (int i = 0; i < pendingSeedCount; i++)
            {
                PersistentWorldDeltaRecord seedRecord = _pendingSeedScratch[i];
                if (!PersistentWorldDeltaRecord.IsFloraSeedPending(in seedRecord))
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
            if (_regrowthStateCount > 0)
            {
                for (int i = 0; i < _regrowthStateCount; i++)
                {
                    FloraRegrowthState state = _regrowthStates[i];
                    state.RuntimePosition += runtimeOffset;
                    _regrowthStates[i] = state;
                }
            }

            if (_seedFlightStateCount > 0)
            {
                for (int i = 0; i < _seedFlightStateCount; i++)
                {
                    SeedFlightState state = _seedFlightStates[i];
                    state.Position += runtimeOffset;
                    _seedFlightStates[i] = state;
                }
            }

            if (_maturationStateCount > 0)
            {
                for (int i = 0; i < _maturationStateCount; i++)
                {
                    FloraMaturationState state = _maturationStates[i];
                    state.RuntimePosition += runtimeOffset;
                    _maturationStates[i] = state;
                }
            }

            if (_symbioticFungalNodeCount > 0)
            {
                for (int i = 0; i < _symbioticFungalNodeCount; i++)
                {
                    SymbioticFungalNodeState state = _symbioticFungalNodes[i];
                    state.RuntimePosition += runtimeOffset;
                    _symbioticFungalNodes[i] = state;
                }
            }
        }

        private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private void RemoveSeedFlightAtSwapBack(int index)
        {
            if (index < 0 || index >= _seedFlightStateCount)
                return;

            int lastIndex = _seedFlightStateCount - 1;
            _seedFlightStates[index] = _seedFlightStates[lastIndex];
            _seedFlightStates[lastIndex] = default;
            _seedFlightStateCount = lastIndex;
        }

        private bool TryFindSeedFlightIndex(uint seedUid, out int index)
        {
            for (int i = 0; i < _seedFlightStateCount; i++)
            {
                if (_seedFlightStates[i].SeedInstanceUid != seedUid)
                    continue;

                index = i;
                return true;
            }

            index = -1;
            return false;
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
            if (index < 0 || index >= _regrowthStateCount)
                return;

            FloraRegrowthState removed = _regrowthStates[index];
            int lastIndex = _regrowthStateCount - 1;
            _regrowthStates[index] = _regrowthStates[lastIndex];
            _regrowthStates[lastIndex] = default;
            _regrowthStateCount = lastIndex;
            RemoveSeedEmissionUid(removed.InstanceUid);
        }

        private bool TryFindRegrowthStateIndex(uint instanceUid, out int index)
        {
            for (int i = 0; i < _regrowthStateCount; i++)
            {
                if (_regrowthStates[i].InstanceUid != instanceUid)
                    continue;

                index = i;
                return true;
            }

            index = -1;
            return false;
        }

        private bool ContainsSeedEmissionUid(uint instanceUid)
        {
            if (instanceUid == 0u)
                return false;

            for (int i = 0; i < _seedEmissionDestroyedUidCount; i++)
            {
                if (_seedEmissionDestroyedUids[i] == instanceUid)
                    return true;
            }

            return false;
        }

        private void TryAddSeedEmissionUid(uint instanceUid)
        {
            if (instanceUid == 0u || ContainsSeedEmissionUid(instanceUid))
                return;

            int count = _seedEmissionDestroyedUidCount;
            if (count >= _seedEmissionDestroyedUids.Length)
                return;

            _seedEmissionDestroyedUids[count] = instanceUid;
            _seedEmissionDestroyedUidCount = count + 1;
        }

        private void RemoveSeedEmissionUid(uint instanceUid)
        {
            if (instanceUid == 0u)
                return;

            for (int i = 0; i < _seedEmissionDestroyedUidCount; i++)
            {
                if (_seedEmissionDestroyedUids[i] != instanceUid)
                    continue;

                int lastIndex = _seedEmissionDestroyedUidCount - 1;
                _seedEmissionDestroyedUids[i] = _seedEmissionDestroyedUids[lastIndex];
                _seedEmissionDestroyedUids[lastIndex] = 0u;
                _seedEmissionDestroyedUidCount = lastIndex;
                return;
            }
        }

        private float GetCurrentPlayTimeSeconds()
        {
            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            return IsSaveServiceUsable(saveService)
                ? saveService.CurrentPlayTimeSeconds
                : (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private static Vector3 ToRuntimePosition(AbsoluteUniversePosition position)
        {
            if (!position.IsFinite())
                return Vector3.zero;

            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            float3 runtimePosition = AUPMath.ResolveCameraRelative(in position, in runtimeOriginAup);
            if (!math.all(math.isfinite(runtimePosition)))
                return Vector3.zero;

            float rx = runtimePosition.x == 0f ? 0.0f : runtimePosition.x;
            float ry = runtimePosition.y == 0f ? 0.0f : runtimePosition.y;
            float rz = runtimePosition.z == 0f ? 0.0f : runtimePosition.z;
            return new Vector3(rx, ry, rz);
        }
    }
}
