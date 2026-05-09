using System.Runtime.InteropServices;
using System;
using Hecton8.Audio;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Scavenging;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Zero-allocation hand IK snap target resolved from the active indirect-flora lanes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FloraHarvestInteractionPoint
    {
        public readonly uint InstanceUid;
        public readonly AbsoluteUniversePosition AnchorAup;
        public readonly Vector3 RuntimePosition;
        public readonly Vector3 SurfaceNormal;
        public readonly HarvestableTemplate.MaterialClass MaterialClass;
        public readonly int TemplateIndex;
        public readonly float BlendWeight;

        public FloraHarvestInteractionPoint(
            uint instanceUid,
            AbsoluteUniversePosition anchorAup,
            Vector3 runtimePosition,
            Vector3 surfaceNormal,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            float blendWeight)
        {
            InstanceUid = instanceUid;
            AnchorAup = anchorAup;
            RuntimePosition = runtimePosition;
            SurfaceNormal = surfaceNormal;
            MaterialClass = materialClass;
            TemplateIndex = templateIndex;
            BlendWeight = blendWeight;
        }
    }

    /// <summary>
    /// Runtime owner for indirect-flora harvest health, destruction, debris, and yield routing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)] // Manager order must stay ahead of gameplay consumers that read/wire destruction state.
    public sealed class DestructibleOrganicManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener
    {
        private static DestructibleOrganicManager _activeRuntimeInstance;

        private const int DefaultTrackedDestroyedCapacity = 2048;
        private const int DefaultTrackedHealthCapacity = 4096;
        private const int DefaultPendingYieldCapacity = 1024;
        private const int DefaultDropBufferCapacity = 256;
        private const int MaxOrganicDropRecordsPerFrame = 256;
        private const float HiddenInstanceWorldY = -100000f;
        private const float MinimumSearchRadius = 0.8f;
        private const float KelpRadiusBias = 0.65f;
        private const float OrganicBurstVelocityScale = 3f;
        private const float OrganicWiltDurationSeconds = 0.85f;
        private const float OrganicDecompositionDurationSeconds = 10f * 60f;
        private const float MinimumDecomposedHeightScale = 0.05f;
        private const float MinimumDecomposedWidthScale = 0.12f;
        private const float HarvestStatePartialThreshold01 = 0.999f;
        private const float HarvestStateBareThreshold01 = 0.3f;
        private const float MatureSporeGrowthThreshold01 = 0.999f;
        private const float MinimumSporePulseFrequencyHz = 0.01f;
        private const float SporePulsePeakPhase01 = 0.25f;
        private const float SporeShaderPhasePositionX = 0.07f;
        private const float SporeShaderPhasePositionZ = 0.05f;
        private const float InvTwoPi = 0.15915494309189535f;
        private const float SoftBareHealthFloor01 = 0.05f;
        private const float LightStarvationDamagePerSlowTick01 = 0.035f;
        private const float LightStarvationDeathHealth01 = 0.015f;
        private const float AllelopathicBareHealth01 = 0.08f;
        private const float AllelopathicDeathThreshold01 = 0.85f;
        private const float OvergrowthUntouchedSeconds = 3f * 24f * 60f * 60f;
        private const float OvergrowthExpansionMeters = 2f;
        private const int OvergrowthScanBudgetPerSlowTick = 64;
        private const float TitanRootMoundRadiusMeters = 5f;
        private const float TitanRootMoundStrengthMeters = 2.25f;
        private const float TitanRootMoundMatureThreshold01 = 0.999f;
        private const byte FloraRuntimeFlagHasParasite = (byte)HectonVegetationRuntimeFlags.Parasite;
        private const byte FloraRuntimeFlagDead = 1 << 6;
        private const int DefaultCorpseNodeCapacity = 96;
        private const float DefaultCorpseBloodIntensity = 6f;
        private const float CorpseDiseaseActivationSeconds = 120f;
        private const float CorpseDiseaseRadiusMeters = 22f;
        private const float CorpseDiseaseSeverity = 1f;
        private const int MaterialClassCount = 5;
        private const string NativeMemoryOwner = nameof(DestructibleOrganicManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        private enum HarvestState : byte
        {
            Pristine = 0,
            PartiallyHarvested = 1,
            Bare = 2,
            Dead = 3
        }

        private struct CorpseResourceNodeRecord
        {
            public uint NodeId;
            public int SpeciesId;
            public AbsoluteUniversePosition PositionAup;
            public Vector3 Position;
            public float InitialUnits;
            public float RemainingUnits;
            public float BloodIntensity;
            public float SpawnTime;
            public float ExpireTime;
            public byte Active;
        }

        private readonly struct SporeAcousticEvent
        {
            public readonly AbsoluteUniversePosition PositionAup;
            public readonly Vector3 RuntimePosition;
            public readonly AudioClip Clip;
            public readonly float PulseFrequencyHz;
            public readonly float Volume;
            public readonly float Pitch;
            public readonly float SimulationTimeSeconds;
            public readonly float PhaseOffset01;

            public SporeAcousticEvent(
                AbsoluteUniversePosition positionAup,
                Vector3 runtimePosition,
                AudioClip clip,
                float pulseFrequencyHz,
                float volume,
                float pitch,
                float simulationTimeSeconds,
                float phaseOffset01)
            {
                PositionAup = positionAup;
                RuntimePosition = runtimePosition;
                Clip = clip;
                PulseFrequencyHz = pulseFrequencyHz;
                Volume = volume;
                Pitch = pitch;
                SimulationTimeSeconds = simulationTimeSeconds;
                PhaseOffset01 = phaseOffset01;
            }
        }

        private void EvaluateAggressiveOvergrowth(float currentTime)
        {
            EvaluateAggressiveOvergrowthInLane(false, currentTime, ref _surfaceOvergrowthScanCursor);
            EvaluateAggressiveOvergrowthInLane(true, currentTime, ref _underwaterOvergrowthScanCursor);
        }

        private void EvaluateAggressiveOvergrowthInLane(bool underwater, float currentTime, ref int cursor)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int> semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                !_lastOrganicTouchTimeByInstanceUid.IsCreated ||
                !_overgrownByInstanceUid.IsCreated ||
                count <= 0)
            {
                cursor = 0;
                return;
            }

            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length)))));
            if (safeCount <= 0)
            {
                cursor = 0;
                return;
            }

            cursor = math.clamp(cursor, 0, safeCount - 1);
            int checks = math.min(OvergrowthScanBudgetPerSlowTick, safeCount);
            for (int step = 0; step < checks; step++)
            {
                int activeIndex = (cursor + step) % safeCount;
                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u ||
                    (float)health[activeIndex] <= 0.0001f ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    _overgrownByInstanceUid.ContainsKey(instanceUid))
                {
                    continue;
                }

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
                if (!IsConsumableFloraMaterialClass(materialClass))
                    continue;

                if (!_lastOrganicTouchTimeByInstanceUid.TryGetValue(instanceUid, out float lastTouchTime))
                {
                    _lastOrganicTouchTimeByInstanceUid.TryAdd(instanceUid, currentTime);
                    continue;
                }

                if (currentTime - lastTouchTime < OvergrowthUntouchedSeconds)
                    continue;

                if (!TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out float3 navObstacleCenter, out float3 navObstacleExtents))
                    continue;

                VoxelDynamicNavGridRuntime.EnqueueDynamicObstacleGrowth(
                    navObstacleCenter,
                    navObstacleExtents,
                    OvergrowthExpansionMeters);
                _overgrownByInstanceUid.TryAdd(instanceUid, 1);
                TryApplyTitanRootMound(underwater, activeIndex, instanceUid);
            }

            cursor = (cursor + checks) % safeCount;
        }

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Authoritative indirect-flora bridge that owns the streamed native instance payloads.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Optional flora interaction manager used to publish localized tool-impact bend bursts.")]
        private FloraInteractionManager floraInteractionManager;

        [Header("Templates")]
        [SerializeField]
        [Tooltip("Authored harvest templates resolved by material class.")]
        private HarvestableTemplate[] harvestTemplates;

        [Header("Debris")]
        [SerializeField]
        [Tooltip("Burst debris profile used for kelp-family destruction.")]
        private OrganicDebrisProfile kelpDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for coral-family destruction.")]
        private OrganicDebrisProfile coralDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for metallic outcrop destruction.")]
        private OrganicDebrisProfile titaniumDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for surface sargassum destruction.")]
        private OrganicDebrisProfile sargassumDebrisProfile;

        [Header("Harvest Query")]
        [SerializeField, Min(MinimumSearchRadius)]
        [Tooltip("Base world-space radius used when resolving a tool hit against the active indirect flora arrays.")]
        private float hitSearchRadius = 1.25f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Extra world-space radius added when resolving tall kelp silhouettes.")]
        private float kelpHeightTolerance = 0.4f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Radius of the published flora-interaction burst when a tool hits a harvestable instance.")]
        private float interactionBurstRadius = 1.4f;

        [Header("Allelopathy")]
        [SerializeField, Min(1f)]
        [Tooltip("Planar kelp-density cell radius used when evaluating overcrowding-driven allelopathic coral suppression.")]
        private float allelopathicCellRadius = 14f;

        [SerializeField, Min(1)]
        [Tooltip("Maximum macro-kelp count treated as a full cell when evaluating the 95% overcrowding threshold.")]
        private int allelopathicKelpCapacity = 20;

        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Normalized kelp occupancy threshold above which competing coral in the same cell is forced into decomposition.")]
        private float allelopathicThreshold01 = 0.95f;

        [Header("Harvest Audio")]
        [SerializeField]
        [Tooltip("Organic-impact clip used when a soft flora harvest transition occurs.")]
        private AudioClip organicHarvestClip;

        [SerializeField]
        [Tooltip("Brittle snap/crack clip used when coral-like flora changes harvest state.")]
        private AudioClip brittleHarvestClip;

        [SerializeField]
        [Tooltip("Fibrous tear clip used when kelp- or vine-like flora changes harvest state.")]
        private AudioClip fibrousHarvestClip;

        [SerializeField]
        [Tooltip("Metallic fallback clip used when a flora template routes through the metallic acoustic lane.")]
        private AudioClip metallicHarvestClip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base harvest audio volume applied to partial state changes before state-specific scaling.")]
        private float harvestAudioBaseVolume = 0.72f;

        [Header("Spore Acoustics")]
        [SerializeField]
        [Tooltip("Fallback hostile spore pulse clip used when a mature spore flora template has no authored clip.")]
        private AudioClip sporeAcousticFallbackClip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fallback volume for mature spore acoustic pulses when the flora template leaves volume at zero.")]
        private float sporeAcousticFallbackVolume = 0.65f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Lower cadence guard for mature spore acoustic pulses. Actual cadence remains locked to 1 / PulseFrequency unless clamped by this value.")]
        private float sporeAcousticMinimumIntervalSeconds = 0.2f;

        [SerializeField, Min(1)]
        [Tooltip("Maximum active flora instances checked per lane each Tick for mature spore acoustic cadence. This keeps large fields bounded.")]
        private int matureSporeAcousticScanBudgetPerTick = 64;

        private NativeArray<uint> _surfaceInstanceUids;
        private NativeArray<uint> _underwaterInstanceUids;
        private NativeArray<byte> _surfaceMaterialClasses;
        private NativeArray<byte> _underwaterMaterialClasses;
        private NativeArray<Unity.Mathematics.half> _surfaceHealth;
        private NativeArray<Unity.Mathematics.half> _underwaterHealth;
        private NativeHashMap<uint, Unity.Mathematics.half> _healthByInstanceUid;
        private NativeHashMap<uint, byte> _destroyedByInstanceUid;
        private NativeHashMap<uint, float> _pendingWiltEndTimeByInstanceUid;
        private NativeHashMap<uint, float> _damageVisualProgressByInstanceUid;
        private NativeHashMap<uint, float> _decompositionStartTimeByInstanceUid;
        private NativeHashMap<uint, float> _regrowthProgressByInstanceUid;
        private NativeHashMap<uint, float3> _regrowthPositionByInstanceUid;
        private NativeHashMap<uint, Unity.Mathematics.half> _maturationScaleByInstanceUid;
        private NativeHashMap<uint, Unity.Mathematics.half> _maturationYieldByInstanceUid;
        private NativeHashMap<uint, float> _nextSporeAcousticTimeByInstanceUid;
        private NativeHashMap<uint, float2> _baseScaleByInstanceUid;
        private NativeHashMap<uint, byte> _runtimeFlagsByInstanceUid;
        private NativeHashMap<uint, float> _lastOrganicTouchTimeByInstanceUid;
        private NativeHashMap<uint, byte> _overgrownByInstanceUid;
        private NativeHashMap<uint, byte> _rootMoundAppliedByInstanceUid;
        private NativeList<PersistentWorldDeltaRecord> _destroyedFloraScratch;
        private NativeList<PersistentWorldDeltaRecord> _floraStateOverrideScratch;
        private NativeHashMap<uint, Unity.Mathematics.half> _persistedHealth01ByInstanceUid;
        private NativeHashMap<uint, Unity.Mathematics.half> _persistedHeightScale01ByInstanceUid;
        private NativeList<DestroyedOrganicEvent> _pendingYieldEvents;
        private NativeArray<DestroyedOrganicEvent> _yieldJobInput;
        private DropBuffer _dropBuffer;
        private NativeArray<HarvestableTemplate.RuntimeDescriptor> _templateDescriptors;
        private NativeArray<HarvestableTemplate.LootRuntimeEntry> _lootEntries;
        private NativeArray<EntropyYieldMaterialLutEntry> _yieldMaterialLut;
        private NativeArray<Vector3> _dropDebugScratch;
        private JobHandle _yieldJobHandle;
        private int _scheduledYieldCount;
        private int _deferredYieldScheduleFrame = -1;
        private int _surfaceRevision = -1;
        private int _underwaterRevision = -1;
        private int _surfaceCount;
        private int _underwaterCount;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameTickRegistered;
        private bool _originShiftListenerRegistered;
        private bool _yieldScheduled;

        private NativeArray<Matrix4x4> _surfaceMatrices;
        private NativeArray<HectonVegetationInstanceData> _surfaceMetadata;
        private NativeArray<int> _surfaceTypes;
        private NativeArray<int> _surfaceSemanticTypes;
        private NativeArray<Matrix4x4> _underwaterMatrices;
        private NativeArray<HectonVegetationInstanceData> _underwaterMetadata;
        private NativeArray<int> _underwaterTypes;
        private NativeArray<int> _underwaterSemanticTypes;

        private int[] _templateIndexByMaterialClass;
        private int[] _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();
        private HarvestableTemplate[] _descriptorHarvestTemplates = Array.Empty<HarvestableTemplate>();
        private byte[] _floraCategoryByDescriptorIndex = Array.Empty<byte>();
        private byte[] _audioMaterialByDescriptorIndex = Array.Empty<byte>();
        private float[] _growthTimeSecondsByDescriptorIndex = Array.Empty<float>();
        private byte[] _sporeAcousticEmitterByDescriptorIndex = Array.Empty<byte>();
        private AudioClip[] _sporeAcousticClipByDescriptorIndex = Array.Empty<AudioClip>();
        private float[] _sporePulseFrequencyByDescriptorIndex = Array.Empty<float>();
        private float[] _sporeAcousticVolumeByDescriptorIndex = Array.Empty<float>();
        private int _surfaceMatureSporeScanCursor;
        private int _underwaterMatureSporeScanCursor;
        private int _surfaceOvergrowthScanCursor;
        private int _underwaterOvergrowthScanCursor;
        // COLD ALLOC: CorpseResourceNodeRecord[96] - bounded ecological corpse-resource nodes used by scavenger AI and blood-scent routing - owner: DestructibleOrganicManager
        private CorpseResourceNodeRecord[] _corpseResourceNodes = Array.Empty<CorpseResourceNodeRecord>();
        private int _corpseResourceNodeCount;

        /// <summary>Currently enabled runtime organic entropy owner.</summary>
        public static DestructibleOrganicManager ActiveRuntimeInstance => _activeRuntimeInstance;

        internal bool RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits)
        {
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            return RegisterCorpseResourceNode(in positionAup, worldPosition, speciesId, capacityUnits);
        }

        internal bool RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits)
        {
            Vector3 runtimePosition = positionAup.ToRuntimeFloat3();
            return RegisterCorpseResourceNode(in positionAup, runtimePosition, speciesId, capacityUnits);
        }

        private bool RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, Vector3 worldPosition, int speciesId, float capacityUnits)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodes.Length == 0 || capacityUnits <= 0f)
                return false;

            int writeIndex = -1;
            for (int i = 0; i < _corpseResourceNodes.Length; i++)
            {
                if (_corpseResourceNodes[i].Active != 0)
                    continue;

                writeIndex = i;
                break;
            }

            if (writeIndex < 0)
                writeIndex = FindWeakestCorpseNodeIndex();

            if (writeIndex < 0)
                return false;

            float initialUnits = Mathf.Max(0.25f, capacityUnits);
            CorpseResourceNodeRecord record = new CorpseResourceNodeRecord
            {
                NodeId = (uint)(PersistentWorldRegistry.ComputeResourceNodeTombstoneId(in positionAup) & uint.MaxValue),
                SpeciesId = speciesId,
                PositionAup = positionAup,
                Position = worldPosition,
                InitialUnits = initialUnits,
                RemainingUnits = initialUnits,
                BloodIntensity = DefaultCorpseBloodIntensity,
                SpawnTime = Time.time,
                ExpireTime = Time.time + OrganicDecompositionDurationSeconds,
                Active = 1
            };
            _corpseResourceNodes[writeIndex] = record;
            if (writeIndex >= _corpseResourceNodeCount)
                _corpseResourceNodeCount = writeIndex + 1;

            ChemicalInfluenceGrid.QueueBloodScent(worldPosition, record.BloodIntensity);
            return true;
        }

        internal bool TryResolveNearestCorpseResourceNode(Vector3 worldPosition, float searchRadius, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            AbsoluteUniversePosition queryAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            return TryResolveNearestCorpseResourceNode(in queryAup, searchRadius, out corpsePosition, out corpseNodeId);
        }

        internal bool TryResolveNearestCorpseResourceNode(in AbsoluteUniversePosition queryAup, float searchRadius, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return false;

            double bestDistanceSq = (double)searchRadius * searchRadius;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.RemainingUnits <= 0f)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                corpsePosition = record.Position;
                corpseNodeId = record.NodeId;
            }

            return corpseNodeId != 0u;
        }

        internal bool TryConsumeCorpseResourceNode(uint corpseNodeId, float consumeUnits)
        {
            if (corpseNodeId == 0u || consumeUnits <= 0f || _corpseResourceNodes == null)
                return false;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.NodeId != corpseNodeId)
                    continue;

                record.RemainingUnits = Mathf.Max(0f, record.RemainingUnits - consumeUnits);
                if (record.RemainingUnits <= 0.001f)
                {
                    record.Active = 0;
                    record.RemainingUnits = 0f;
                }
                else
                {
                    record.BloodIntensity = math.lerp(0.35f, DefaultCorpseBloodIntensity, ResolveCorpseCapacityFraction01(in record));
                }

                _corpseResourceNodes[i] = record;
                TrimTrailingCorpseNodes();
                return true;
            }

            return false;
        }

        internal float ResolveCorpseSpawnInfluence01(Vector3 worldPosition, float searchRadius)
        {
            AbsoluteUniversePosition queryAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            return ResolveCorpseSpawnInfluence01(in queryAup, searchRadius);
        }

        internal float ResolveCorpseSpawnInfluence01(in AbsoluteUniversePosition queryAup, float searchRadius)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0 || searchRadius <= 0f)
                return 0f;

            double maxDistanceSq = (double)searchRadius * searchRadius;
            float bestInfluence01 = 0f;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.RemainingUnits <= 0f)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > maxDistanceSq)
                    continue;

                float distance01 = 1f - math.saturate((float)(distanceSq / maxDistanceSq));
                float mass01 = ResolveCorpseCapacityFraction01(in record);
                float influence01 = distance01 * mass01;
                if (influence01 > bestInfluence01)
                    bestInfluence01 = influence01;
            }

            return bestInfluence01;
        }

        internal bool TryResolveCorpseDiseaseExposure(
            in AbsoluteUniversePosition queryAup,
            float currentTimeSeconds,
            out float severity01,
            out Vector3 sourcePosition)
        {
            severity01 = 0f;
            sourcePosition = default;
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return false;

            double radiusSq = (double)CorpseDiseaseRadiusMeters * CorpseDiseaseRadiusMeters;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 ||
                    record.RemainingUnits <= 0f ||
                    currentTimeSeconds - record.SpawnTime < CorpseDiseaseActivationSeconds)
                {
                    continue;
                }

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > radiusSq)
                    continue;

                float distance01 = 1f - math.saturate((float)(distanceSq / radiusSq));
                float mass01 = ResolveCorpseCapacityFraction01(in record);
                severity01 = Mathf.Max(severity01, distance01 * mass01 * CorpseDiseaseSeverity);
                sourcePosition = record.Position;
            }

            return severity01 > 0.001f;
        }

        private void Awake()
        {
            _activeRuntimeInstance = this;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (floraInteractionManager == null)
                floraInteractionManager = GetComponent<FloraInteractionManager>();

            hitSearchRadius = Mathf.Max(MinimumSearchRadius, hitSearchRadius);
            kelpHeightTolerance = Mathf.Max(0.05f, kelpHeightTolerance);
            interactionBurstRadius = Mathf.Max(0.05f, interactionBurstRadius);
            allelopathicCellRadius = Mathf.Max(1f, allelopathicCellRadius);
            allelopathicKelpCapacity = Mathf.Max(1, allelopathicKelpCapacity);
            allelopathicThreshold01 = Mathf.Clamp(allelopathicThreshold01, 0.5f, 1f);
            harvestAudioBaseVolume = Mathf.Clamp01(harvestAudioBaseVolume);
            sporeAcousticFallbackVolume = Mathf.Clamp01(sporeAcousticFallbackVolume);
            sporeAcousticMinimumIntervalSeconds = Mathf.Max(0.05f, sporeAcousticMinimumIntervalSeconds);
            matureSporeAcousticScanBudgetPerTick = Mathf.Max(1, matureSporeAcousticScanBudgetPerTick);

            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persistent per-instance harvest health state keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _healthByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,byte>[2048] - persistent destroyed flora tombstone set keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _destroyedByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - active wilt-to-hide timers keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _pendingWiltEndTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - persistent partial-damage wilt progress keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _damageVisualProgressByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - persistent decomposition start time keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _decompositionStartTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - active flora regrowth progress keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _regrowthProgressByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float3>[2048] - flora regrowth position overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _regrowthPositionByInstanceUid = new NativeHashMap<uint, float3>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - live flora maturation scale multipliers keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _maturationScaleByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - live flora maturation resource-yield multipliers keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _maturationYieldByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[4096] - mature spore acoustic cadence keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _nextSporeAcousticTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float2>[4096] - baseline height/width scales keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _baseScaleByInstanceUid = new NativeHashMap<uint, float2>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,byte>[4096] - runtime flora bit-mask flags keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _runtimeFlagsByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[4096] - untouched flora clock keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _lastOrganicTouchTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,byte>[4096] - macro-flora overgrowth obstacle state keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _overgrownByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,byte>[4096] - one-shot Titan root SDF mound state keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _rootMoundAppliedByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[2048] - destroyed flora tombstone restore scratch - owner: DestructibleOrganicManager
            _destroyedFloraScratch = new NativeList<PersistentWorldDeltaRecord>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[4096] - partial flora-state restore scratch - owner: DestructibleOrganicManager
            _floraStateOverrideScratch = new NativeList<PersistentWorldDeltaRecord>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persisted normalized flora health overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _persistedHealth01ByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persisted normalized flora height overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _persistedHeightScale01ByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<DestroyedOrganicEvent>[128] - pending entropy yield event queue - owner: DestructibleOrganicManager
            _pendingYieldEvents = new NativeList<DestroyedOrganicEvent>(DefaultPendingYieldCapacity, Allocator.Persistent);
            _dropBuffer = new DropBuffer(DefaultDropBufferCapacity, Allocator.Persistent);
            // COLD ALLOC: Vector3[1] - bounded debug scratch for future runtime diagnostics - owner: DestructibleOrganicManager
            _dropDebugScratch = new NativeArray<Vector3>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeMemorySentinel();
            // COLD ALLOC: CorpseResourceNodeRecord[96] - bounded ecological corpse-resource nodes used by scavenger AI and blood-scent routing - owner: DestructibleOrganicManager
            _corpseResourceNodes = new CorpseResourceNodeRecord[DefaultCorpseNodeCapacity];
            _corpseResourceNodeCount = 0;

            BuildTemplateCaches();
            BuildYieldMaterialLut();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            RegisterOriginShiftListener();

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

            if (!_lateFrameTickRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameTickRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }

            SyncDestroyedFloraFromPersistence();
            SyncFloraStateOverridesFromPersistence();
            BuildFloraTemplateHarvestMap();
            RefreshActiveCachesIfNeeded(force: true);
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

            if (_lateFrameTickRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameTickRegistered = false;
            }

            CompleteYieldJobIfNeeded();
            UnregisterOriginShiftListener();
        }

        private void OnDestroy()
        {
            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;

            CompleteYieldJobIfNeeded();
            UnregisterOriginShiftListener();
            DisposeNativeArray(ref _surfaceInstanceUids);
            DisposeNativeArray(ref _underwaterInstanceUids);
            DisposeNativeArray(ref _surfaceMaterialClasses);
            DisposeNativeArray(ref _underwaterMaterialClasses);
            DisposeNativeArray(ref _surfaceHealth);
            DisposeNativeArray(ref _underwaterHealth);
            DisposeNativeArray(ref _yieldJobInput);
            DisposeNativeArray(ref _templateDescriptors);
            DisposeNativeArray(ref _lootEntries);
            DisposeNativeArray(ref _yieldMaterialLut);
            DisposeNativeArray(ref _dropDebugScratch);

            if (_dropBuffer.IsCreated)
                _dropBuffer.Dispose();

            DisposeNativeHashMap(ref _healthByInstanceUid, nameof(_healthByInstanceUid));
            DisposeNativeHashMap(ref _destroyedByInstanceUid, nameof(_destroyedByInstanceUid));
            DisposeNativeHashMap(ref _persistedHealth01ByInstanceUid, nameof(_persistedHealth01ByInstanceUid));
            DisposeNativeHashMap(ref _persistedHeightScale01ByInstanceUid, nameof(_persistedHeightScale01ByInstanceUid));
            DisposeNativeHashMap(ref _pendingWiltEndTimeByInstanceUid, nameof(_pendingWiltEndTimeByInstanceUid));
            DisposeNativeHashMap(ref _damageVisualProgressByInstanceUid, nameof(_damageVisualProgressByInstanceUid));
            DisposeNativeHashMap(ref _maturationScaleByInstanceUid, nameof(_maturationScaleByInstanceUid));
            DisposeNativeHashMap(ref _maturationYieldByInstanceUid, nameof(_maturationYieldByInstanceUid));
            DisposeNativeHashMap(ref _nextSporeAcousticTimeByInstanceUid, nameof(_nextSporeAcousticTimeByInstanceUid));
            DisposeNativeHashMap(ref _decompositionStartTimeByInstanceUid, nameof(_decompositionStartTimeByInstanceUid));
            DisposeNativeHashMap(ref _regrowthProgressByInstanceUid, nameof(_regrowthProgressByInstanceUid));
            DisposeNativeHashMap(ref _regrowthPositionByInstanceUid, nameof(_regrowthPositionByInstanceUid));
            DisposeNativeHashMap(ref _baseScaleByInstanceUid, nameof(_baseScaleByInstanceUid));
            DisposeNativeHashMap(ref _runtimeFlagsByInstanceUid, nameof(_runtimeFlagsByInstanceUid));
            DisposeNativeHashMap(ref _lastOrganicTouchTimeByInstanceUid, nameof(_lastOrganicTouchTimeByInstanceUid));
            DisposeNativeHashMap(ref _overgrownByInstanceUid, nameof(_overgrownByInstanceUid));
            DisposeNativeHashMap(ref _rootMoundAppliedByInstanceUid, nameof(_rootMoundAppliedByInstanceUid));
            DisposeNativeList(ref _destroyedFloraScratch, nameof(_destroyedFloraScratch));
            DisposeNativeList(ref _floraStateOverrideScratch, nameof(_floraStateOverrideScratch));
            DisposeNativeList(ref _pendingYieldEvents, nameof(_pendingYieldEvents));
        }

        /// <summary>
        /// Rebuilds live corpse attractor runtime caches from authoritative Absolute Universe Positions after a committed origin shift.
        /// </summary>
        /// <param name="shiftData">Committed floating-origin shift data.</param>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return;

            float3 committedOriginOffset = new float3(
                shiftData.NewTotalOffset.x,
                shiftData.NewTotalOffset.y,
                shiftData.NewTotalOffset.z);

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0)
                    continue;

                float3 runtimePosition = AUPMath.ToRuntimeFloat3(in record.PositionAup, committedOriginOffset);
                record.Position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                _corpseResourceNodes[i] = record;
            }
        }

        private void RegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void UnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        /// <summary>
        /// Processes pending entropy jobs and drop routing.
        /// </summary>
        public void Tick(float deltaTime)
        {
            float currentTime = Time.time;
            RefreshActiveCachesIfNeeded(force: false);
            UpdateDecompositionVisuals(currentTime);
            UpdateRegrowthVisuals();
            UpdateMatureSporeAcoustics(currentTime);
            UpdateDamageVisuals(currentTime);
            UpdateWiltInstances(currentTime);
            bool dropBufferDrained = !_yieldScheduled && DrainDropBuffer();
            if (dropBufferDrained && _deferredYieldScheduleFrame < 0)
                ScheduleYieldJobIfNeeded();

            VoxelDynamicNavGridRuntime.SchedulePendingDynamicObstacleUpdates();
        }

        public void LateFrameTick()
        {
            CompleteYieldJobIfNeeded(force: false);
            if (_yieldScheduled)
                return;

            bool dropBufferDrained = DrainDropBuffer();
            if (dropBufferDrained &&
                _deferredYieldScheduleFrame >= 0 &&
                Time.frameCount >= _deferredYieldScheduleFrame)
            {
                _deferredYieldScheduleFrame = -1;
                ScheduleYieldJobIfNeeded();
                VoxelDynamicNavGridRuntime.SchedulePendingDynamicObstacleUpdates();
            }
        }

        /// <summary>
        /// Restores destroyed flora tombstones from persistence and re-applies active suppression after world paging.
        /// </summary>
        public void SlowTick()
        {
            SyncDestroyedFloraFromPersistence();
            SyncFloraStateOverridesFromPersistence();
            RefreshActiveCachesIfNeeded(force: true);
            RefreshCorpseResourceNodes(Time.time);
            EvaluateAllelopathicRelease();
            EvaluateAggressiveOvergrowth(Time.time);
        }

        /// <summary>
        /// Applies one tool hit against the nearest active harvestable indirect-flora instance.
        /// </summary>
        public bool TryApplyToolHit(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask)
        {
            if (deliveredDamage <= 0f || vegetationBridge == null || _templateDescriptors.Length <= 0)
                return false;

            RefreshActiveCachesIfNeeded(force: false);
            if (!TryResolveNearestHarvestTarget(
                hitPoint,
                Mathf.Max(hitSearchRadius, interactionBurstRadius),
                toolCapabilityMask,
                out bool underwater,
                out int activeIndex,
                out uint instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out Matrix4x4 instanceMatrix,
                out Vector3 instancePosition))
            {
                return false;
            }

            if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid))
                return false;

            float baseHealth = Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth);
            float currentHealth = GetLaneHealth(underwater, activeIndex);
            float toolResistance = math.max(0.01f, _templateDescriptors[templateIndex].ToolResistance);
            float nextHealth = Mathf.Max(0f, currentHealth - (deliveredDamage / toolResistance));
            float previousNormalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            float previousHeightScale = ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, previousNormalizedHealth);
            HarvestState previousHarvestState = ResolveHarvestState(templateIndex, baseHealth, currentHealth, previousHeightScale);
            float nextHeightScale = ResolveNormalizedHeightScale(templateIndex, baseHealth, nextHealth);
            HarvestState nextHarvestState = nextHealth > 0.0001f
                ? ResolveHarvestState(templateIndex, baseHealth, nextHealth, nextHeightScale)
                : HarvestState.Dead;
            if (ShouldDetonateDefensiveSporeBurst(templateIndex, toolCapabilityMask))
            {
                floraInteractionManager?.RegisterDefensiveSporeBurst(instancePosition, Mathf.Max(0.35f, normalizedPower));
                nextHealth = 0f;
                nextHeightScale = ResolveNormalizedHeightScale(templateIndex, baseHealth, nextHealth);
                nextHarvestState = HarvestState.Dead;
            }

            SetLaneHealth(underwater, activeIndex, nextHealth);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)nextHealth);
            MarkOrganicTouched(instanceUid, Time.time);

            PublishExternalInteraction(hitPoint, direction * Mathf.Max(0.25f, normalizedPower * OrganicBurstVelocityScale), interactionBurstRadius);
            bool harvestStateChanged = previousHarvestState != nextHarvestState;
            ApplyDamageVisualState(instanceUid, underwater, activeIndex, templateIndex, baseHealth, nextHealth, nextHeightScale, harvestStateChanged, Time.time);
            if (harvestStateChanged)
                DispatchHarvestAudioTransition(instanceUid, templateIndex, previousHarvestState, nextHarvestState, instancePosition);

            if (nextHealth > 0.0001f)
            {
                PersistFloraStateOverride(instanceUid, templateIndex, instancePosition, underwater, activeIndex, baseHealth, nextHealth);
                return true;
            }

            DestroyResolvedInstance(
                underwater,
                activeIndex,
                instanceUid,
                materialClass,
                templateIndex,
                instanceMatrix,
                instancePosition,
                hitPoint,
                hitNormal,
                normalizedPower);
            return true;
        }

        /// <summary>
        /// Consumes one nearby flora instance without spawning debris or loot, using the passive decomposition/tombstone path.
        /// </summary>
        internal bool TryConsumeFloraAtPosition(Vector3 worldPosition, float searchRadius, out uint instanceUid)
        {
            instanceUid = 0u;
            if (vegetationBridge == null || _templateDescriptors.Length <= 0)
                return false;

            RefreshActiveCachesIfNeeded(force: false);
            if (!TryResolveNearestHarvestTarget(
                worldPosition,
                Mathf.Max(MinimumSearchRadius, searchRadius),
                0u,
                out bool underwater,
                out int activeIndex,
                out instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out _,
                out Vector3 instancePosition))
            {
                return false;
            }

            if (materialClass == HarvestableTemplate.MaterialClass.None ||
                (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
            {
                instanceUid = 0u;
                return false;
            }

            byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
            ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
            ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
            PublishExternalInteraction(instancePosition, Vector3.up * 0.15f, interactionBurstRadius);
            return true;
        }

        /// <summary>
        /// Applies non-harvest decomposition to any active indirect flora intersecting a newly placed construction envelope.
        /// </summary>
        internal int ApplyConstructionDecomposition(Vector3 runtimePosition, float radiusMeters)
        {
            if (radiusMeters <= 0f)
                return 0;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null)
                return 0;

            RefreshActiveCachesIfNeeded(force: false);
            Vector3 universePosition = HectonMapMagicVegetationBridge.ToUniverseSpace(runtimePosition);
            float radiusSq = radiusMeters * radiusMeters;
            int decomposedCount = 0;
            decomposedCount += ApplyConstructionDecompositionInLane(false, universePosition, radiusSq);
            decomposedCount += ApplyConstructionDecompositionInLane(true, universePosition, radiusSq);
            return decomposedCount;
        }

        /// <summary>
        /// Instantly tombstones active consumable flora inside a persistent chemical dead zone.
        /// </summary>
        internal int ApplyDefoliantDeadZone(Vector3 runtimePosition, float radiusMeters)
        {
            if (radiusMeters <= 0f)
                return 0;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null)
                return 0;

            RefreshActiveCachesIfNeeded(force: false);
            Vector3 universePosition = HectonMapMagicVegetationBridge.ToUniverseSpace(runtimePosition);
            float radiusSq = radiusMeters * radiusMeters;
            int killedCount = 0;
            killedCount += ApplyDefoliantDeadZoneInLane(false, universePosition, radiusSq);
            killedCount += ApplyDefoliantDeadZoneInLane(true, universePosition, radiusSq);
            return killedCount;
        }

        private bool ShouldDetonateDefensiveSporeBurst(int templateIndex, uint toolCapabilityMask)
        {
            if (floraInteractionManager == null || templateIndex < 0)
                return false;

            uint burstTriggerMask = (uint)FloraDataTemplate.VulnerabilityMask.Cut | (uint)FloraDataTemplate.VulnerabilityMask.Drill;
            return (toolCapabilityMask & burstTriggerMask) != 0u &&
                   floraInteractionManager.IsDefensiveSporeBurstTemplateIndex(templateIndex);
        }

        private void EvaluateAllelopathicRelease()
        {
            NativeArray<Matrix4x4> matrices = _underwaterMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = _underwaterMetadata;
            NativeArray<int> types = _underwaterTypes;
            NativeArray<int> semanticTypes = _underwaterSemanticTypes;
            NativeArray<uint> instanceUids = _underwaterInstanceUids;
            NativeArray<byte> materialClasses = _underwaterMaterialClasses;
            int count = _underwaterCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                count <= 0)
            {
                return;
            }

            float radiusSq = allelopathicCellRadius * allelopathicCellRadius;
            int overcrowdingThreshold = Mathf.Max(1, Mathf.CeilToInt(allelopathicKelpCapacity * allelopathicThreshold01));
            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, materialClasses.Length))));

            for (int coralIndex = 0; coralIndex < safeCount; coralIndex++)
            {
                uint instanceUid = instanceUids[coralIndex];
                if (instanceUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    metadata[coralIndex].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f)
                {
                    continue;
                }

                HectonMapMagicVegetationBridge.VegetationSemanticType semanticType =
                    (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticTypes[coralIndex];
                if (!HectonMapMagicVegetationBridge.IsColonyCoralSemanticType(semanticType))
                    continue;

                Vector3 coralPosition = ExtractTranslation(matrices[coralIndex]);
                int kelpCount = 0;
                for (int kelpIndex = 0; kelpIndex < safeCount; kelpIndex++)
                {
                    if (types[kelpIndex] != (int)HectonVegetationInstanceType.GiantKelp ||
                        metadata[kelpIndex].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                        math.abs(metadata[kelpIndex].HeightScale) <= 0.0001f)
                    {
                        continue;
                    }

                    Vector3 kelpPosition = ExtractTranslation(matrices[kelpIndex]);
                    Vector2 planarDelta = new Vector2(kelpPosition.x - coralPosition.x, kelpPosition.z - coralPosition.z);
                    if (planarDelta.sqrMagnitude > radiusSq)
                        continue;

                    kelpCount++;
                    if (kelpCount < overcrowdingThreshold)
                        continue;

                    HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[coralIndex];
                    int templateIndex = ResolveTemplateIndex(metadata[coralIndex], materialClass);
                    ApplyPassiveDecomposition(true, coralIndex, instanceUid, materialClass, templateIndex, coralPosition);
                    break;
                }
            }
        }

        private void BuildTemplateCaches()
        {
            // COLD ALLOC: int[MaterialClassCount] - material-class to template-index lookup table - owner: DestructibleOrganicManager
            _templateIndexByMaterialClass = new int[MaterialClassCount];
            for (int i = 0; i < _templateIndexByMaterialClass.Length; i++)
                _templateIndexByMaterialClass[i] = -1;

            FloraDataTemplate[] floraTemplates = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            bool hasFloraTemplates = floraTemplates != null && floraTemplates.Length > 0;
            int validTemplateCount = 0;
            int totalLootEntries = 0;

            if (hasFloraTemplates)
            {
                for (int i = 0; i < floraTemplates.Length; i++)
                {
                    FloraDataTemplate floraTemplate = floraTemplates[i];
                    HarvestableTemplate template = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                    if (floraTemplate == null || template == null)
                        continue;

                    validTemplateCount++;
                    totalLootEntries += CountTemplateLootEntries(template);
                }
            }
            else if (harvestTemplates != null)
            {
                for (int i = 0; i < harvestTemplates.Length; i++)
                {
                    HarvestableTemplate template = harvestTemplates[i];
                    if (template == null)
                        continue;

                    validTemplateCount++;
                    totalLootEntries += CountTemplateLootEntries(template);
                }
            }

            DisposeNativeArray(ref _templateDescriptors);
            DisposeNativeArray(ref _lootEntries);
            _templateDescriptors = new NativeArray<HarvestableTemplate.RuntimeDescriptor>(
                math.max(1, validTemplateCount),
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: RuntimeDescriptor[templateCount] - flora-resolved harvest runtime table - owner: DestructibleOrganicManager
            _lootEntries = new NativeArray<HarvestableTemplate.LootRuntimeEntry>(
                math.max(1, totalLootEntries),
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: LootRuntimeEntry[totalLootEntries] - flattened harvest loot runtime table - owner: DestructibleOrganicManager
            NativeMemorySentinel.RegisterNativeArray(_templateDescriptors, NativeMemoryOwner, nameof(_templateDescriptors), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_lootEntries, NativeMemoryOwner, nameof(_lootEntries), NativeMemoryLifetime);
            _descriptorHarvestTemplates = new HarvestableTemplate[math.max(1, validTemplateCount)]; // COLD ALLOC: HarvestableTemplate[templateCount] - descriptor-to-authoring lookup for flora template harvest routing - owner: DestructibleOrganicManager
            _floraCategoryByDescriptorIndex = new byte[math.max(1, validTemplateCount)]; // COLD ALLOC: byte[templateCount] - flora-category cache used by harvest-state thresholds - owner: DestructibleOrganicManager
            _audioMaterialByDescriptorIndex = new byte[math.max(1, validTemplateCount)]; // COLD ALLOC: byte[templateCount] - flora audio-material routing cache used by harvest-state audio dispatch - owner: DestructibleOrganicManager
            _growthTimeSecondsByDescriptorIndex = new float[math.max(1, validTemplateCount)]; // COLD ALLOC: float[templateCount] - authored flora growth durations - owner: DestructibleOrganicManager
            _sporeAcousticEmitterByDescriptorIndex = new byte[math.max(1, validTemplateCount)]; // COLD ALLOC: byte[templateCount] - mature spore acoustic emitter flags - owner: DestructibleOrganicManager
            _sporeAcousticClipByDescriptorIndex = new AudioClip[math.max(1, validTemplateCount)]; // COLD ALLOC: AudioClip[templateCount] - mature spore acoustic clip refs - owner: DestructibleOrganicManager
            _sporePulseFrequencyByDescriptorIndex = new float[math.max(1, validTemplateCount)]; // COLD ALLOC: float[templateCount] - mature spore pulse cadence copied from VAT authoring - owner: DestructibleOrganicManager
            _sporeAcousticVolumeByDescriptorIndex = new float[math.max(1, validTemplateCount)]; // COLD ALLOC: float[templateCount] - mature spore acoustic volume per descriptor - owner: DestructibleOrganicManager
            _harvestDescriptorIndexByFloraTemplateIndex = hasFloraTemplates
                ? new int[floraTemplates.Length]
                : Array.Empty<int>(); // COLD ALLOC: int[floraTemplates.Length] - flora-template to descriptor mapping - owner: DestructibleOrganicManager

            if (hasFloraTemplates)
            {
                for (int i = 0; i < _harvestDescriptorIndexByFloraTemplateIndex.Length; i++)
                    _harvestDescriptorIndexByFloraTemplateIndex[i] = -1;
            }

            if (validTemplateCount <= 0)
                return;

            int descriptorWriteIndex = 0;
            int lootWriteIndex = 0;
            NativeList<HarvestableTemplate.LootRuntimeEntry> lootScratch =
                new NativeList<HarvestableTemplate.LootRuntimeEntry>(math.max(1, totalLootEntries), Allocator.Temp);
            try
            {
                if (hasFloraTemplates)
                {
                    for (int i = 0; i < floraTemplates.Length; i++)
                    {
                        FloraDataTemplate floraTemplate = floraTemplates[i];
                        HarvestableTemplate template = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                        if (floraTemplate == null || template == null)
                            continue;

                        int lootStartIndex = lootWriteIndex;
                        lootScratch.Clear();
                        template.CopyLootTableNonAlloc(lootScratch);
                        for (int lootIndex = 0; lootIndex < lootScratch.Length && lootWriteIndex < _lootEntries.Length; lootIndex++)
                        {
                            _lootEntries[lootWriteIndex] = lootScratch[lootIndex];
                            lootWriteIndex++;
                        }

                        if (descriptorWriteIndex >= _templateDescriptors.Length)
                            continue;

                        HarvestableTemplate.RuntimeDescriptor descriptor = template.BuildRuntimeDescriptor(lootStartIndex);
                        FloraDataTemplate.RuntimeDescriptor floraRuntimeDescriptor = floraTemplate.BuildRuntimeDescriptor();
                        descriptor.StableHashId = floraRuntimeDescriptor.StableHashId;
                        descriptor.BaseHealth = floraTemplate.MaxHealth;
                        _templateDescriptors[descriptorWriteIndex] = descriptor;
                        _descriptorHarvestTemplates[descriptorWriteIndex] = template;
                        _floraCategoryByDescriptorIndex[descriptorWriteIndex] = (byte)floraTemplate.Category;
                        _audioMaterialByDescriptorIndex[descriptorWriteIndex] = floraTemplate.AudioMaterialID;
                        _growthTimeSecondsByDescriptorIndex[descriptorWriteIndex] = floraTemplate.GrowthTimeSeconds;
                        _sporeAcousticEmitterByDescriptorIndex[descriptorWriteIndex] = floraTemplate.EmitsMatureSporeAcoustic ? (byte)1 : (byte)0;
                        _sporeAcousticClipByDescriptorIndex[descriptorWriteIndex] = floraTemplate.MatureSporeAcousticClip;
                        _sporePulseFrequencyByDescriptorIndex[descriptorWriteIndex] = floraTemplate.PulseFrequency;
                        _sporeAcousticVolumeByDescriptorIndex[descriptorWriteIndex] = floraTemplate.MatureSporeAcousticVolume;
                        _harvestDescriptorIndexByFloraTemplateIndex[i] = descriptorWriteIndex;

                        int materialIndex = descriptor.MaterialClassId;
                        if ((uint)materialIndex < (uint)_templateIndexByMaterialClass.Length && _templateIndexByMaterialClass[materialIndex] < 0)
                            _templateIndexByMaterialClass[materialIndex] = descriptorWriteIndex;

                        descriptorWriteIndex++;
                    }
                }
                else if (harvestTemplates != null)
                {
                    for (int i = 0; i < harvestTemplates.Length; i++)
                    {
                        HarvestableTemplate template = harvestTemplates[i];
                        if (template == null)
                            continue;

                        int lootStartIndex = lootWriteIndex;
                        lootScratch.Clear();
                        template.CopyLootTableNonAlloc(lootScratch);
                        for (int lootIndex = 0; lootIndex < lootScratch.Length && lootWriteIndex < _lootEntries.Length; lootIndex++)
                        {
                            _lootEntries[lootWriteIndex] = lootScratch[lootIndex];
                            lootWriteIndex++;
                        }

                        if (descriptorWriteIndex >= _templateDescriptors.Length)
                            continue;

                        HarvestableTemplate.RuntimeDescriptor descriptor = template.BuildRuntimeDescriptor(lootStartIndex);
                        _templateDescriptors[descriptorWriteIndex] = descriptor;
                        _descriptorHarvestTemplates[descriptorWriteIndex] = template;
                        _floraCategoryByDescriptorIndex[descriptorWriteIndex] = (byte)InferCategoryFromMaterialClass(template.TemplateMaterialClass);
                        _growthTimeSecondsByDescriptorIndex[descriptorWriteIndex] = 480f;

                        int materialIndex = descriptor.MaterialClassId;
                        if ((uint)materialIndex < (uint)_templateIndexByMaterialClass.Length && _templateIndexByMaterialClass[materialIndex] < 0)
                            _templateIndexByMaterialClass[materialIndex] = descriptorWriteIndex;

                        descriptorWriteIndex++;
                    }
                }
            }
            finally
            {
                if (lootScratch.IsCreated)
                    lootScratch.Dispose();
            }
        }

        private int ApplyConstructionDecompositionInLane(bool underwater, Vector3 centerUniversePosition, float radiusSq)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int> semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                count <= 0)
            {
                return 0;
            }

            int decomposedCount = 0;
            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, materialClasses.Length))));
            for (int i = 0; i < safeCount; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (materialClass == HarvestableTemplate.MaterialClass.None)
                    continue;

                if (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                float distanceSq = ResolveConstructionDistanceSq(centerUniversePosition, rootPosition, metadata[i], types[i]);
                if (distanceSq > radiusSq)
                    continue;

                int templateIndex = ResolveTemplateIndex(metadata[i], materialClass);
                ApplyPassiveDecomposition(underwater, i, instanceUid, materialClass, templateIndex, rootPosition);
                decomposedCount++;
            }

            return decomposedCount;
        }

        private int ApplyDefoliantDeadZoneInLane(bool underwater, Vector3 centerUniversePosition, float radiusSq)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int> semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                count <= 0)
            {
                return 0;
            }

            int killedCount = 0;
            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, materialClasses.Length))));
            for (int i = 0; i < safeCount; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (!IsConsumableFloraMaterialClass(materialClass))
                    continue;

                if (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                if ((rootPosition - centerUniversePosition).sqrMagnitude > radiusSq)
                    continue;

                int templateIndex = ResolveTemplateIndex(metadata[i], materialClass);
                ApplyPassiveDecomposition(underwater, i, instanceUid, materialClass, templateIndex, rootPosition);
                PrimeDecompositionState(instanceUid, Time.time - OrganicDecompositionDurationSeconds);
                ApplyDecompositionToLaneInstance(underwater, i, instanceUid, 1f);
                killedCount++;
            }

            return killedCount;
        }

        private void BuildFloraTemplateHarvestMap()
        {
            if (_harvestDescriptorIndexByFloraTemplateIndex != null && _harvestDescriptorIndexByFloraTemplateIndex.Length > 0)
                return;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            FloraDataTemplate[] floraTemplateAssets = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            if (floraTemplateAssets == null || floraTemplateAssets.Length == 0)
            {
                _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();
                return;
            }

            // COLD ALLOC: int[floraTemplateAssets.Length] - flora-template to harvest-descriptor lookup for instance-specific loot routing - owner: DestructibleOrganicManager
            int[] mapping = new int[floraTemplateAssets.Length];
            for (int i = 0; i < mapping.Length; i++)
                mapping[i] = -1;

            for (int i = 0; i < floraTemplateAssets.Length; i++)
            {
                FloraDataTemplate floraTemplate = floraTemplateAssets[i];
                HarvestableTemplate harvestTemplate = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                if (harvestTemplate == null || _descriptorHarvestTemplates == null)
                    continue;

                for (int descriptorIndex = 0; descriptorIndex < _descriptorHarvestTemplates.Length; descriptorIndex++)
                {
                    if (_descriptorHarvestTemplates[descriptorIndex] != harvestTemplate)
                        continue;

                    mapping[i] = descriptorIndex;
                    break;
                }
            }

            _harvestDescriptorIndexByFloraTemplateIndex = mapping;
        }

        private static FloraDataTemplate.FloraCategory InferCategoryFromMaterialClass(HarvestableTemplate.MaterialClass materialClass)
        {
            switch (materialClass)
            {
                case HarvestableTemplate.MaterialClass.Kelp:
                    return FloraDataTemplate.FloraCategory.HarvestableKelp;
                case HarvestableTemplate.MaterialClass.Coral:
                case HarvestableTemplate.MaterialClass.TitaniumOutcrop:
                    return FloraDataTemplate.FloraCategory.HardCoral;
                case HarvestableTemplate.MaterialClass.Sargassum:
                    return FloraDataTemplate.FloraCategory.GiantSargassum;
                default:
                    return FloraDataTemplate.FloraCategory.MicroGrass;
            }
        }

        private void BuildYieldMaterialLut()
        {
            DisposeNativeArray(ref _yieldMaterialLut);
            _yieldMaterialLut = new NativeArray<EntropyYieldMaterialLutEntry>(
                math.max(1, MaterialClassCount),
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: EntropyYieldMaterialLutEntry[MaterialClassCount] - deterministic density/unit-mass lookup for burst flora yield - owner: DestructibleOrganicManager
            NativeMemorySentinel.RegisterNativeArray(_yieldMaterialLut, NativeMemoryOwner, nameof(_yieldMaterialLut), NativeMemoryLifetime);

            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.None, 1000f, 1f, 0.5f, 0f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Kelp, 460f, 1.2f, 0.58f, 0.08f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Coral, 1320f, 2.5f, 0.65f, 0.16f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.TitaniumOutcrop, 4480f, 4.5f, 0.78f, 0.22f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Sargassum, 310f, 1.0f, 0.52f, 0.05f);
        }

        private void WriteYieldMaterialLut(
            HarvestableTemplate.MaterialClass materialClass,
            float densityKgPerM3,
            float unitItemMassKg,
            float minimumRecovery,
            float qualityBias)
        {
            if (!_yieldMaterialLut.IsCreated)
                return;

            int materialIndex = (int)materialClass;
            if (materialIndex < 0 || materialIndex >= _yieldMaterialLut.Length)
                return;

            _yieldMaterialLut[materialIndex] = new EntropyYieldMaterialLutEntry
            {
                DensityKgPerM3 = Mathf.Max(0.01f, densityKgPerM3),
                UnitItemMassKg = Mathf.Max(0.01f, unitItemMassKg),
                MinimumRecovery = Mathf.Clamp01(minimumRecovery),
                QualityBias = Mathf.Clamp01(qualityBias)
            };
        }

        private static int CountTemplateLootEntries(HarvestableTemplate template)
        {
            NativeList<HarvestableTemplate.LootRuntimeEntry> scratch =
                new NativeList<HarvestableTemplate.LootRuntimeEntry>(32, Allocator.Temp);
            try
            {
                return template != null ? template.CopyLootTableNonAlloc(scratch) : 0;
            }
            finally
            {
                if (scratch.IsCreated)
                    scratch.Dispose();
            }
        }

        private void RefreshActiveCachesIfNeeded(bool force)
        {
            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null)
                return;

            if (force || _surfaceRevision != vegetationBridge.ActiveSurfaceAggregateRevision)
                SyncLane(false);

            if (force || _underwaterRevision != vegetationBridge.ActiveUnderwaterAggregateRevision)
                SyncLane(true);
        }

        private void SyncLane(bool underwater)
        {
            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            NativeArray<int> semanticTypes;
            int count;
            int semanticCount;
            bool hasNativePayload;
            bool hasSemanticPayload;
            if (underwater)
            {
                hasNativePayload = vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count);
                hasSemanticPayload = vegetationBridge.TryGetActiveUnderwaterSemanticPayload(out semanticTypes, out _, out semanticCount);
            }
            else
            {
                hasNativePayload = vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
                hasSemanticPayload = vegetationBridge.TryGetActiveSurfaceSemanticPayload(out semanticTypes, out _, out semanticCount);
            }

            if (!hasNativePayload || !hasSemanticPayload || count <= 0 || semanticCount < count)
            {
                if (underwater)
                {
                    _underwaterMatrices = default;
                    _underwaterMetadata = default;
                    _underwaterTypes = default;
                    _underwaterSemanticTypes = default;
                    _underwaterCount = 0;
                    _underwaterRevision = vegetationBridge.ActiveUnderwaterAggregateRevision;
                }
                else
                {
                    _surfaceMatrices = default;
                    _surfaceMetadata = default;
                    _surfaceTypes = default;
                    _surfaceSemanticTypes = default;
                    _surfaceCount = 0;
                    _surfaceRevision = vegetationBridge.ActiveSurfaceAggregateRevision;
                }

                return;
            }

            NativeArray<uint> instanceUids = underwater
                ? EnsureLaneCapacity(ref _underwaterInstanceUids, count, nameof(_underwaterInstanceUids))
                : EnsureLaneCapacity(ref _surfaceInstanceUids, count, nameof(_surfaceInstanceUids));
            NativeArray<byte> materialClasses = underwater
                ? EnsureLaneCapacity(ref _underwaterMaterialClasses, count, nameof(_underwaterMaterialClasses))
                : EnsureLaneCapacity(ref _surfaceMaterialClasses, count, nameof(_surfaceMaterialClasses));
            NativeArray<Unity.Mathematics.half> health = underwater
                ? EnsureLaneCapacity(ref _underwaterHealth, count, nameof(_underwaterHealth))
                : EnsureLaneCapacity(ref _surfaceHealth, count, nameof(_surfaceHealth));
            float currentTime = Time.time;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = ComputeStableInstanceUid(matrices[i], metadata[i], types[i], semanticTypes[i]);
                HarvestableTemplate.MaterialClass fallbackMaterialClass = ResolveMaterialClass(types[i], semanticTypes[i]);
                int templateIndex = ResolveTemplateIndex(metadata[i], fallbackMaterialClass);
                HarvestableTemplate.MaterialClass materialClass = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                    ? (HarvestableTemplate.MaterialClass)_templateDescriptors[templateIndex].MaterialClassId
                    : fallbackMaterialClass;
                instanceUids[i] = instanceUid;
                materialClasses[i] = (byte)materialClass;
                CacheBaseScale(instanceUid, metadata[i]);
                PrimeUntouchedClock(instanceUid, currentTime);
                byte runtimeFlags = ResolveRuntimeFlags(instanceUid, materialClass, semanticTypes[i], metadata[i].RuntimeFlags);
                ApplyRuntimeFlags(ref metadata, i, runtimeFlags);
                SetRuntimeState(ref metadata, i, HectonVegetationInstanceData.RuntimeStateIdle);
                float defaultHealth = templateIndex >= 0 ? _templateDescriptors[templateIndex].BaseHealth : 0f;
                float resolvedHealth = defaultHealth;
                bool hasPersistedFloraState = TryResolvePersistedFloraState(instanceUid, out float persistedHealth01, out float persistedHeightScale01);
                bool isDestroyed = _destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid);
                bool isDefoliantSuppressed = !isDestroyed &&
                                             IsConsumableFloraMaterialClass(materialClass) &&
                                             ChemicalInfluenceGrid.IsInsidePermanentDefoliantDeadZoneAbsolute(ExtractTranslation(matrices[i]));
                if (isDefoliantSuppressed)
                {
                    RegisterDefoliantDestroyedInstance(instanceUid, templateIndex, ExtractTranslation(matrices[i]));
                    ApplyRuntimeFlags(ref metadata, i, MarkDeadRuntimeFlag(instanceUid));
                    isDestroyed = true;
                }
                float regrowthProgress = 0f;
                bool isRegrowing = _regrowthProgressByInstanceUid.IsCreated &&
                                   _regrowthProgressByInstanceUid.TryGetValue(instanceUid, out regrowthProgress);
                if (_healthByInstanceUid.IsCreated && _healthByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half savedHealth))
                    resolvedHealth = math.max(0f, (float)savedHealth);
                else if (hasPersistedFloraState && templateIndex >= 0)
                    resolvedHealth = Mathf.Max(0f, defaultHealth * Mathf.Clamp01(persistedHealth01));

                if (_healthByInstanceUid.IsCreated)
                {
                    _healthByInstanceUid.Remove(instanceUid);
                    _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)resolvedHealth);
                }

                health[i] = (Unity.Mathematics.half)resolvedHealth;
                if (isRegrowing)
                {
                    if (_damageVisualProgressByInstanceUid.IsCreated)
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);

                    ApplyRegrowthVisualToLaneInstance(underwater, i, instanceUid, regrowthProgress);
                }
                else if (isDestroyed || resolvedHealth <= 0.0001f)
                {
                    if (_damageVisualProgressByInstanceUid.IsCreated)
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);
                    float entropy01 = ResolveOrPrimeDecompositionProgress(instanceUid, currentTime);
                    ApplyDecompositionMetadata(ref metadata, i, instanceUid, entropy01);
                }
                else if (templateIndex >= 0 && resolvedHealth < defaultHealth)
                {
                    float damage01 = ResolveDamageProgress(defaultHealth, resolvedHealth);
                    UpdateDamageProgressCache(instanceUid, damage01);
                    float normalizedHeightScale = hasPersistedFloraState
                        ? Mathf.Clamp01(persistedHeightScale01)
                        : ResolveNormalizedHeightScale(templateIndex, defaultHealth, resolvedHealth);
                    ApplyPersistedDamageMetadata(ref metadata, i, instanceUid, templateIndex, persistedHealth01, normalizedHeightScale, damage01, currentTime);
                }
                else if (_damageVisualProgressByInstanceUid.IsCreated)
                {
                    _damageVisualProgressByInstanceUid.Remove(instanceUid);
                }

                if (!isRegrowing && !isDestroyed && resolvedHealth > 0.0001f)
                {
                    float maturationScale = ResolveMaturationScaleMultiplier(instanceUid);
                    if (maturationScale < 0.9999f)
                        ApplyMaturationVisualToLaneInstance(underwater, i, instanceUid, maturationScale);
                }
            }

            if (underwater)
            {
                _underwaterMatrices = matrices;
                _underwaterMetadata = metadata;
                _underwaterTypes = types;
                _underwaterSemanticTypes = semanticTypes;
                _underwaterCount = count;
                _underwaterRevision = vegetationBridge.ActiveUnderwaterAggregateRevision;
            }
            else
            {
                _surfaceMatrices = matrices;
                _surfaceMetadata = metadata;
                _surfaceTypes = types;
                _surfaceSemanticTypes = semanticTypes;
                _surfaceCount = count;
                _surfaceRevision = vegetationBridge.ActiveSurfaceAggregateRevision;
            }
        }

        private void SyncDestroyedFloraFromPersistence()
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null || !_destroyedFloraScratch.IsCreated || !_destroyedByInstanceUid.IsCreated)
                return;

            _destroyedByInstanceUid.Clear();
            _destroyedFloraScratch.Clear();
            registry.CopyDestroyedFloraDeltas(_destroyedFloraScratch);
            for (int i = 0; i < _destroyedFloraScratch.Length; i++)
            {
                PersistentWorldDeltaRecord record = _destroyedFloraScratch[i];
                if (record.InstanceUid == 0u)
                    continue;

                if (ResolveDescriptorIndexByPersistentIdHash(record.ItemPersistentIdHash) < 0)
                {
                    registry.TryClearDestroyedFlora(record.InstanceUid);
                    continue;
                }

                if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(record.InstanceUid))
                    continue;

                _destroyedByInstanceUid.TryAdd(record.InstanceUid, 1);
                ClearOrganicLifecycleState(record.InstanceUid);
                PrimeDecompositionState(record.InstanceUid, Time.time - OrganicDecompositionDurationSeconds);
                _healthByInstanceUid.Remove(record.InstanceUid);
                _healthByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)0f);
            }
        }

        private void SyncFloraStateOverridesFromPersistence()
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null ||
                !_floraStateOverrideScratch.IsCreated ||
                !_persistedHealth01ByInstanceUid.IsCreated ||
                !_persistedHeightScale01ByInstanceUid.IsCreated)
            {
                return;
            }

            _floraStateOverrideScratch.Clear();
            _persistedHealth01ByInstanceUid.Clear();
            _persistedHeightScale01ByInstanceUid.Clear();
            registry.CopyFloraStateOverrideDeltas(_floraStateOverrideScratch);
            for (int i = 0; i < _floraStateOverrideScratch.Length; i++)
            {
                PersistentWorldDeltaRecord record = _floraStateOverrideScratch[i];
                if (record.InstanceUid == 0u)
                    continue;

                if ((_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(record.InstanceUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(record.InstanceUid)))
                {
                    continue;
                }

                int descriptorIndex = ResolveDescriptorIndexByPersistentIdHash(record.ItemPersistentIdHash);
                if (descriptorIndex < 0)
                {
                    registry.TryClearFloraStateOverride(record.InstanceUid);
                    continue;
                }

                PersistentWorldRegistry.UnpackFloraStateOverride(record.Quantity, out float persistedHealth01, out byte persistedHarvestState);
                float normalizedHealth = math.saturate(persistedHealth01);
                float normalizedHeightScale = ResolveNormalizedHeightScaleFromHarvestState(
                    descriptorIndex,
                    normalizedHealth,
                    ResolvePersistedHarvestState(persistedHarvestState));
                _persistedHealth01ByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)normalizedHealth);
                _persistedHeightScale01ByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)math.saturate(normalizedHeightScale));
            }
        }

        private void CompleteYieldJobIfNeeded(bool force = true)
        {
            if (!_yieldScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _yieldJobHandle, force))
                return;

            _yieldScheduled = false;
            _scheduledYieldCount = 0;
        }

        private void ScheduleYieldJobIfNeeded()
        {
            if (_yieldScheduled ||
                !_pendingYieldEvents.IsCreated ||
                _pendingYieldEvents.Length <= 0 ||
                !_dropBuffer.IsCreated ||
                !_dropBuffer.IsEmpty ||
                !_yieldMaterialLut.IsCreated)
                return;

            int pendingCount = _pendingYieldEvents.Length;
            int eventCount = math.min(pendingCount, math.min(_dropBuffer.Capacity, MaxOrganicDropRecordsPerFrame));
            EnsureNativeCapacity(ref _yieldJobInput, eventCount, nameof(_yieldJobInput));
            for (int i = 0; i < eventCount; i++)
            {
                _yieldJobInput[i] = _pendingYieldEvents[i];
            }

            VoxelDynamicNavGridRuntime.EnqueueDestroyedOrganicEvents(_yieldJobInput, eventCount);

            int remainderCount = pendingCount - eventCount;
            if (remainderCount > 0)
            {
                for (int i = 0; i < remainderCount; i++)
                    _pendingYieldEvents[i] = _pendingYieldEvents[eventCount + i];

                _pendingYieldEvents.ResizeUninitialized(remainderCount);
                _deferredYieldScheduleFrame = math.max(_deferredYieldScheduleFrame, Time.frameCount + 1);
            }
            else
            {
                _pendingYieldEvents.Clear();
                _deferredYieldScheduleFrame = -1;
            }

            _scheduledYieldCount = eventCount;
            _yieldJobHandle = new EntropyYieldJob
            {
                Events = _yieldJobInput,
                TemplateDescriptors = _templateDescriptors,
                LootEntries = _lootEntries,
                MaterialLut = _yieldMaterialLut,
                DropWriter = _dropBuffer.AsParallelWriter(),
                EventCount = eventCount
            }.Schedule(eventCount, 8);
            _yieldScheduled = true;
        }

        private bool DrainDropBuffer()
        {
            if (!_dropBuffer.IsCreated)
                return true;

            PlayerInventory playerInventory = GlobalRegistry.PlayerInventory != null
                ? GlobalRegistry.PlayerInventory.Inventory
                : null;
            Hecton8.SaveSystem.ItemCatalog itemCatalog = playerInventory != null ? playerInventory.ItemCatalog : null;
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            int remainingBudget = math.min(_dropBuffer.Capacity, MaxOrganicDropRecordsPerFrame);
            while (remainingBudget-- > 0 && _dropBuffer.TryDequeue(out ItemDropData drop))
            {
                if (drop.ItemHashId == 0 || drop.Quantity == 0)
                    continue;

                int rejectedQuantity = drop.Quantity;
                if (playerInventory != null)
                {
                    PlayerInventory.ScavengeAttemptResult result =
                        playerInventory.ScavengeAttempt(drop.ItemHashId, drop.Quantity, playerInventory.transform);
                    rejectedQuantity = result.RejectedQuantity;
                }

                if (rejectedQuantity > 0 && registry != null && itemCatalog != null)
                {
                    Vector3 runtimePosition = new Vector3(drop.Position.x, drop.Position.y, drop.Position.z);
                    registry.TryRegisterDroppedItem(drop.ItemHashId, itemCatalog, rejectedQuantity, runtimePosition);
                }
            }

            return _dropBuffer.IsEmpty;
        }

        private void RefreshCorpseResourceNodes(float currentTime)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0)
                    continue;

                if (record.RemainingUnits <= 0f || currentTime >= record.ExpireTime)
                {
                    record.Active = 0;
                    record.RemainingUnits = 0f;
                    _corpseResourceNodes[i] = record;
                    continue;
                }

                float normalizedDecay = ResolveCorpseCapacityFraction01(in record);
                float bloodIntensity = math.lerp(0.35f, record.BloodIntensity, normalizedDecay);
                ChemicalInfluenceGrid.QueueBloodScent(record.Position, bloodIntensity);
            }

            TrimTrailingCorpseNodes();
        }

        private static float ResolveCorpseCapacityFraction01(in CorpseResourceNodeRecord record)
        {
            float initialUnits = record.InitialUnits > 0f ? record.InitialUnits : record.RemainingUnits;
            return initialUnits > 0f
                ? Mathf.Clamp01(record.RemainingUnits / initialUnits)
                : 0f;
        }

        private int FindWeakestCorpseNodeIndex()
        {
            if (_corpseResourceNodes == null || _corpseResourceNodes.Length == 0)
                return -1;

            int weakestIndex = -1;
            float weakestScore = float.MaxValue;
            for (int i = 0; i < _corpseResourceNodes.Length; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                float score = record.Active == 0 ? float.MinValue : record.RemainingUnits;
                if (score >= weakestScore)
                    continue;

                weakestScore = score;
                weakestIndex = i;
            }

            return weakestIndex;
        }

        private void TrimTrailingCorpseNodes()
        {
            while (_corpseResourceNodeCount > 0 && _corpseResourceNodes[_corpseResourceNodeCount - 1].Active == 0)
                _corpseResourceNodeCount--;
        }

        internal bool TryResolveNearestConsumableFlora(Vector3 runtimePosition, float searchRadius, out Vector3 floraPosition, out uint instanceUid)
        {
            floraPosition = Vector3.zero;
            instanceUid = 0u;

            float bestDistanceSq = searchRadius * searchRadius;
            bool found = TryResolveNearestConsumableFloraInLane(runtimePosition, true, ref bestDistanceSq, ref floraPosition, ref instanceUid);
            if (TryResolveNearestConsumableFloraInLane(runtimePosition, false, ref bestDistanceSq, ref floraPosition, ref instanceUid))
                found = true;

            return found;
        }

        public bool TryResolveNearestHarvestInteractionPoint(
            Vector3 handRuntimePosition,
            float searchRadius,
            uint toolCapabilityMask,
            out FloraHarvestInteractionPoint interactionPoint)
        {
            interactionPoint = default;
            if (vegetationBridge == null)
                vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (searchRadius <= 0f || vegetationBridge == null || _templateDescriptors.Length <= 0)
                return false;

            RefreshActiveCachesIfNeeded(force: false);
            if (!TryResolveNearestHarvestTarget(
                handRuntimePosition,
                Mathf.Max(MinimumSearchRadius, searchRadius),
                toolCapabilityMask,
                out bool underwater,
                out int activeIndex,
                out uint instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out _,
                out Vector3 instancePosition))
            {
                return false;
            }

            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return false;
            }

            Vector3 snapPosition = ResolveHarvestSnapPosition(
                handRuntimePosition,
                instancePosition,
                metadata[activeIndex],
                types[activeIndex]);
            Vector3 normal = handRuntimePosition - snapPosition;
            normal = NormalizeVector3Fast(normal, Vector3.up);

            interactionPoint = new FloraHarvestInteractionPoint(
                instanceUid,
                AbsoluteUniversePosition.FromRuntimePosition(snapPosition),
                snapPosition,
                normal,
                materialClass,
                templateIndex,
                1f);
            return true;
        }

        internal int CollectNearestConsumableFlora(
            Vector3 runtimePosition,
            float searchRadius,
            uint[] instanceUids,
            Vector3[] positions)
        {
            if (instanceUids == null || positions == null)
                return 0;

            int capacity = math.min(instanceUids.Length, positions.Length);
            if (capacity <= 0)
                return 0;

            for (int i = 0; i < capacity; i++)
            {
                instanceUids[i] = 0u;
                positions[i] = Vector3.zero;
            }

            Span<float> bestDistanceSq = stackalloc float[4];
            int boundedCapacity = math.min(capacity, 4);
            for (int i = 0; i < boundedCapacity; i++)
                bestDistanceSq[i] = float.MaxValue;

            int collectedCount = 0;
            CollectNearestConsumableFloraInLane(runtimePosition, searchRadius, true, instanceUids, positions, bestDistanceSq, boundedCapacity, ref collectedCount);
            CollectNearestConsumableFloraInLane(runtimePosition, searchRadius, false, instanceUids, positions, bestDistanceSq, boundedCapacity, ref collectedCount);
            return collectedCount;
        }

        internal bool AreTrackedFloraDestroyed(uint[] instanceUids, int trackedCount)
        {
            if (instanceUids == null || trackedCount <= 0)
                return false;

            if (!_destroyedByInstanceUid.IsCreated)
                return false;

            int upperBound = math.min(trackedCount, instanceUids.Length);
            bool hasTrackedInstance = false;
            for (int i = 0; i < upperBound; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u)
                    continue;

                hasTrackedInstance = true;
                if (!_destroyedByInstanceUid.ContainsKey(instanceUid))
                    return false;
            }

            return hasTrackedInstance;
        }

        internal bool TryConsumeFlora(uint instanceUid)
        {
            if (instanceUid == 0u || !TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
                return false;

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !materialClasses.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= count ||
                activeIndex >= matrices.Length ||
                activeIndex >= materialClasses.Length)
            {
                return false;
            }

            HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
            if (!IsConsumableFloraMaterialClass(materialClass))
                return false;

            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
            return true;
        }

        private bool TryResolveNearestHarvestTarget(
            Vector3 hitPoint,
            float searchRadius,
            uint toolCapabilityMask,
            out bool underwater,
            out int activeIndex,
            out uint instanceUid,
            out HarvestableTemplate.MaterialClass materialClass,
            out int templateIndex,
            out Matrix4x4 instanceMatrix,
            out Vector3 instancePosition)
        {
            underwater = false;
            activeIndex = -1;
            instanceUid = 0u;
            materialClass = HarvestableTemplate.MaterialClass.None;
            templateIndex = -1;
            instanceMatrix = Matrix4x4.identity;
            instancePosition = Vector3.zero;

            float bestDistanceSq = float.MaxValue;
            if (TryResolveNearestHarvestTargetInLane(
                hitPoint,
                searchRadius,
                toolCapabilityMask,
                true,
                ref bestDistanceSq,
                ref activeIndex,
                ref instanceUid,
                ref materialClass,
                ref templateIndex,
                ref instanceMatrix,
                ref instancePosition))
            {
                underwater = true;
            }

            int surfaceIndex = -1;
            uint surfaceUid = 0u;
            HarvestableTemplate.MaterialClass surfaceMaterial = HarvestableTemplate.MaterialClass.None;
            int surfaceTemplateIndex = -1;
            Matrix4x4 surfaceMatrix = Matrix4x4.identity;
            Vector3 surfacePosition = Vector3.zero;
            if (TryResolveNearestHarvestTargetInLane(
                hitPoint,
                searchRadius,
                toolCapabilityMask,
                false,
                ref bestDistanceSq,
                ref surfaceIndex,
                ref surfaceUid,
                ref surfaceMaterial,
                ref surfaceTemplateIndex,
                ref surfaceMatrix,
                ref surfacePosition))
            {
                underwater = false;
                activeIndex = surfaceIndex;
                instanceUid = surfaceUid;
                materialClass = surfaceMaterial;
                templateIndex = surfaceTemplateIndex;
                instanceMatrix = surfaceMatrix;
                instancePosition = surfacePosition;
            }

            return activeIndex >= 0 && instanceUid != 0u && templateIndex >= 0;
        }

        private static Vector3 ResolveHarvestSnapPosition(
            Vector3 handRuntimePosition,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = math.lerp(10f, 20f, math.saturate(math.abs(metadata.HeightScale)));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                return ClosestPointOnSegment(rootPosition, top, handRuntimePosition);
            }

            float height01 = math.saturate(math.abs(metadata.HeightScale));
            float verticalBias = vegetationType == HectonVegetationInstanceType.Sargassum
                ? math.lerp(0.18f, 0.85f, height01)
                : math.lerp(0.12f, 0.65f, height01);
            return rootPosition + Vector3.up * verticalBias;
        }

        private bool TryResolveNearestConsumableFloraInLane(
            Vector3 runtimePosition,
            bool underwater,
            ref float bestDistanceSq,
            ref Vector3 bestPosition,
            ref uint bestInstanceUid)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                count <= 0)
            {
                return false;
            }

            bool found = false;
            int upperBound = math.min(count, math.min(matrices.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length))));
            for (int i = 0; i < upperBound; i++)
            {
                uint candidateUid = instanceUids[i];
                if (candidateUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(candidateUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(candidateUid)))
                {
                    continue;
                }

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (!IsConsumableFloraMaterialClass(materialClass) || (float)health[i] <= 0.0001f)
                    continue;

                Vector3 candidatePosition = ExtractTranslation(matrices[i]);
                float distanceSq = (candidatePosition - runtimePosition).sqrMagnitude;
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestPosition = candidatePosition;
                bestInstanceUid = candidateUid;
                found = true;
            }

            return found;
        }

        private void CollectNearestConsumableFloraInLane(
            Vector3 runtimePosition,
            float searchRadius,
            bool underwater,
            uint[] bestInstanceUids,
            Vector3[] bestPositions,
            Span<float> bestDistanceSq,
            int capacity,
            ref int collectedCount)
        {
            if (capacity <= 0)
                return;

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                count <= 0)
            {
                return;
            }

            float searchRadiusSq = math.max(0.0001f, searchRadius * searchRadius);
            int upperBound = math.min(count, math.min(matrices.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length))));
            for (int i = 0; i < upperBound; i++)
            {
                uint candidateUid = instanceUids[i];
                if (candidateUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(candidateUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(candidateUid)))
                {
                    continue;
                }

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (!IsConsumableFloraMaterialClass(materialClass) || (float)health[i] <= 0.0001f)
                    continue;

                Vector3 candidatePosition = ExtractTranslation(matrices[i]);
                float distanceSq = (candidatePosition - runtimePosition).sqrMagnitude;
                if (distanceSq > searchRadiusSq)
                    continue;

                TryInsertConsumableCandidate(
                    candidateUid,
                    candidatePosition,
                    distanceSq,
                    bestInstanceUids,
                    bestPositions,
                    bestDistanceSq,
                    capacity,
                    ref collectedCount);
            }
        }

        private static void TryInsertConsumableCandidate(
            uint candidateUid,
            Vector3 candidatePosition,
            float distanceSq,
            uint[] bestInstanceUids,
            Vector3[] bestPositions,
            Span<float> bestDistanceSq,
            int capacity,
            ref int collectedCount)
        {
            if (candidateUid == 0u || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
            {
                if (bestInstanceUids[i] == candidateUid)
                    return;
            }

            int insertIndex = -1;
            for (int i = 0; i < capacity; i++)
            {
                if (bestInstanceUids[i] == 0u || distanceSq < bestDistanceSq[i])
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < 0)
                return;

            for (int i = capacity - 1; i > insertIndex; i--)
            {
                bestInstanceUids[i] = bestInstanceUids[i - 1];
                bestPositions[i] = bestPositions[i - 1];
                bestDistanceSq[i] = bestDistanceSq[i - 1];
            }

            bestInstanceUids[insertIndex] = candidateUid;
            bestPositions[insertIndex] = candidatePosition;
            bestDistanceSq[insertIndex] = distanceSq;
            collectedCount = math.min(collectedCount + 1, capacity);
        }

        private bool TryResolveNearestHarvestTargetInLane(
            Vector3 hitPoint,
            float searchRadius,
            uint toolCapabilityMask,
            bool underwater,
            ref float bestDistanceSq,
            ref int bestIndex,
            ref uint bestUid,
            ref HarvestableTemplate.MaterialClass bestMaterialClass,
            ref int bestTemplateIndex,
            ref Matrix4x4 bestMatrix,
            ref Vector3 bestPosition)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated || !metadata.IsCreated || !types.IsCreated || !instanceUids.IsCreated || !materialClasses.IsCreated || !health.IsCreated || count <= 0)
                return false;

            float searchRadiusSq = searchRadius * searchRadius;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || (float)health[i] <= 0.0001f)
                    continue;

                HectonVegetationInstanceData instanceMetadata = metadata[i];
                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                int templateIndex = ResolveTemplateIndex(instanceMetadata, materialClass);
                if (materialClass == HarvestableTemplate.MaterialClass.None || templateIndex < 0)
                    continue;

                if (!IsToolCompatible(instanceMetadata, toolCapabilityMask))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                float distanceSq = ResolveHarvestDistanceSq(hitPoint, rootPosition, instanceMetadata, types[i], searchRadiusSq, kelpHeightTolerance);
                if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
                    continue;

                found = true;
                bestDistanceSq = distanceSq;
                bestIndex = i;
                bestUid = instanceUid;
                bestMaterialClass = materialClass;
                bestTemplateIndex = templateIndex;
                bestMatrix = matrices[i];
                bestPosition = rootPosition;
            }

            return found;
        }

        private void DestroyResolvedInstance(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Matrix4x4 instanceMatrix,
            Vector3 instancePosition,
            Vector3 hitPoint,
            Vector3 hitNormal,
            float normalizedPower)
        {
            if (!_destroyedByInstanceUid.IsCreated)
                return;

            if (_destroyedByInstanceUid.ContainsKey(instanceUid))
                return;

            bool hasNavObstacleBounds = TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out float3 navObstacleCenter, out float3 navObstacleExtents);
            byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
            ClearOrganicLifecycleState(instanceUid);

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);
            PrimeDecompositionState(instanceUid, Time.time);
            SetLaneHealth(underwater, activeIndex, 0f);
            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

            ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
            ApplyDecompositionToLaneInstance(underwater, activeIndex, instanceUid, 0f);

            PublishExternalInteraction(instancePosition, NormalizeVector3Fast(hitNormal, Vector3.up) * (normalizedPower * OrganicBurstVelocityScale), interactionBurstRadius * 1.25f);
            SpawnDebris(materialClass, instanceMatrix, instancePosition, hitPoint, hitNormal, normalizedPower, instanceUid);
            QueueYieldEvent(
                instancePosition,
                normalizedPower,
                instanceUid,
                templateIndex,
                materialClass,
                ResolveParentMassKg(underwater, activeIndex, materialClass, templateIndex),
                1f,
                hasNavObstacleBounds ? navObstacleCenter : float3.zero,
                hasNavObstacleBounds ? navObstacleExtents : float3.zero);

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);
        }

        private void ApplyPassiveDecomposition(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Vector3 instancePosition)
        {
            if (!_destroyedByInstanceUid.IsCreated || instanceUid == 0u || _destroyedByInstanceUid.ContainsKey(instanceUid))
                return;

            bool hasNavObstacleBounds = TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out float3 navObstacleCenter, out float3 navObstacleExtents);
            byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
            ClearOrganicLifecycleState(instanceUid);

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);
            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
            if (_regrowthProgressByInstanceUid.IsCreated)
                _regrowthProgressByInstanceUid.Remove(instanceUid);
            if (_regrowthPositionByInstanceUid.IsCreated)
                _regrowthPositionByInstanceUid.Remove(instanceUid);

            PrimeDecompositionState(instanceUid, Time.time);
            SetLaneHealth(underwater, activeIndex, 0f);
            ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
            ApplyDecompositionToLaneInstance(underwater, activeIndex, instanceUid, 0f);

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);

            QueueYieldEvent(
                instancePosition,
                0.1f,
                instanceUid,
                -1,
                materialClass,
                0.05f,
                0f,
                hasNavObstacleBounds ? navObstacleCenter : float3.zero,
                hasNavObstacleBounds ? navObstacleExtents : float3.zero);
        }

        private void SpawnDebris(
            HarvestableTemplate.MaterialClass materialClass,
            Matrix4x4 instanceMatrix,
            Vector3 instancePosition,
            Vector3 hitPoint,
            Vector3 hitNormal,
            float normalizedPower,
            uint instanceUid)
        {
            IDebrisService debrisService = GlobalRegistry.Debris;
            if (debrisService == null || !debrisService.IsInitialized)
                return;

            OrganicDebrisProfile profile = ResolveDebrisProfile(materialClass);
            if (profile == null || !profile.IsValid)
                return;

            Vector3 fallbackNormal = NormalizeVector3Fast(hitNormal, Vector3.up);
            debrisService.SpawnBurst(
                profile,
                instancePosition,
                instanceMatrix.rotation,
                hitPoint,
                fallbackNormal,
                Mathf.Max(0.1f, normalizedPower),
                instanceUid ^ 0x7F4A7C15u);
        }

        private void QueueYieldEvent(
            Vector3 instancePosition,
            float normalizedPower,
            uint instanceUid,
            int templateIndex,
            HarvestableTemplate.MaterialClass materialClass,
            float parentMassKg,
            float damage01,
            float3 navObstacleCenter,
            float3 navObstacleExtents)
        {
            if (!_pendingYieldEvents.IsCreated || _pendingYieldEvents.Length >= _pendingYieldEvents.Capacity)
                return;

            _pendingYieldEvents.AddNoResize(new DestroyedOrganicEvent
            {
                Position = new float3(instancePosition.x, instancePosition.y, instancePosition.z),
                NavObstacleCenter = navObstacleCenter,
                NavObstacleExtents = navObstacleExtents,
                ToolPower = Mathf.Max(0.1f, normalizedPower),
                ParentMassKg = Mathf.Max(0.05f, parentMassKg),
                Damage01 = Mathf.Clamp01(damage01),
                InstanceUid = instanceUid,
                TemplateIndex = templateIndex,
                MaterialClassId = (int)materialClass
            });
        }

        private void PublishExternalInteraction(Vector3 positionWS, Vector3 velocityWS, float radius)
        {
            if (floraInteractionManager == null)
                floraInteractionManager = GetComponent<FloraInteractionManager>();

            floraInteractionManager?.RegisterExternalInteraction(positionWS, velocityWS, radius);
        }

        private OrganicDebrisProfile ResolveDebrisProfile(HarvestableTemplate.MaterialClass materialClass)
        {
            return materialClass switch
            {
                HarvestableTemplate.MaterialClass.Kelp => kelpDebrisProfile,
                HarvestableTemplate.MaterialClass.Coral => coralDebrisProfile,
                HarvestableTemplate.MaterialClass.TitaniumOutcrop => titaniumDebrisProfile,
                HarvestableTemplate.MaterialClass.Sargassum => sargassumDebrisProfile,
                _ => null
            };
        }

        private int ResolveTemplateIndex(HarvestableTemplate.MaterialClass materialClass)
        {
            int materialIndex = (int)materialClass;
            if (_templateIndexByMaterialClass == null || materialIndex < 0 || materialIndex >= _templateIndexByMaterialClass.Length)
                return -1;

            return _templateIndexByMaterialClass[materialIndex];
        }

        private int ResolveTemplateIndex(HectonVegetationInstanceData metadata, HarvestableTemplate.MaterialClass fallbackMaterialClass)
        {
            int floraTemplateIndex = Mathf.RoundToInt(metadata.TemplateIndex);
            if (_harvestDescriptorIndexByFloraTemplateIndex != null &&
                floraTemplateIndex >= 0 &&
                floraTemplateIndex < _harvestDescriptorIndexByFloraTemplateIndex.Length)
            {
                int mappedDescriptorIndex = _harvestDescriptorIndexByFloraTemplateIndex[floraTemplateIndex];
                if (mappedDescriptorIndex >= 0)
                    return mappedDescriptorIndex;
            }

            return ResolveTemplateIndex(fallbackMaterialClass);
        }

        private bool IsToolCompatible(HectonVegetationInstanceData metadata, uint toolCapabilityMask)
        {
            if (toolCapabilityMask == 0u || vegetationBridge == null)
                return true;

            int floraTemplateIndex = Mathf.RoundToInt(metadata.TemplateIndex);
            if (!vegetationBridge.TryGetFloraTemplateRuntimeDescriptor(floraTemplateIndex, out FloraDataTemplate.RuntimeDescriptor descriptor))
                return true;

            return descriptor.VulnerabilityMask == 0u || (descriptor.VulnerabilityMask & toolCapabilityMask) != 0u;
        }

        private void CacheBaseScale(uint instanceUid, HectonVegetationInstanceData metadata)
        {
            if (!_baseScaleByInstanceUid.IsCreated || instanceUid == 0u || _baseScaleByInstanceUid.ContainsKey(instanceUid))
                return;

            _baseScaleByInstanceUid.TryAdd(
                instanceUid,
                new float2(
                    Mathf.Max(MinimumDecomposedHeightScale, Mathf.Abs(metadata.HeightScale)),
                    Mathf.Max(MinimumDecomposedWidthScale, Mathf.Clamp01(Mathf.Abs(metadata.WidthScale)))));
        }

        private void PrimeUntouchedClock(uint instanceUid, float currentTime)
        {
            if (instanceUid == 0u || !_lastOrganicTouchTimeByInstanceUid.IsCreated || _lastOrganicTouchTimeByInstanceUid.ContainsKey(instanceUid))
                return;

            _lastOrganicTouchTimeByInstanceUid.TryAdd(instanceUid, currentTime);
        }

        private void MarkOrganicTouched(uint instanceUid, float currentTime)
        {
            if (instanceUid == 0u || !_lastOrganicTouchTimeByInstanceUid.IsCreated)
                return;

            _lastOrganicTouchTimeByInstanceUid.Remove(instanceUid);
            _lastOrganicTouchTimeByInstanceUid.TryAdd(instanceUid, currentTime);
            if (_overgrownByInstanceUid.IsCreated)
                _overgrownByInstanceUid.Remove(instanceUid);
        }

        private void ClearOrganicLifecycleState(uint instanceUid)
        {
            if (instanceUid == 0u)
                return;

            if (_lastOrganicTouchTimeByInstanceUid.IsCreated)
                _lastOrganicTouchTimeByInstanceUid.Remove(instanceUid);
            if (_overgrownByInstanceUid.IsCreated)
                _overgrownByInstanceUid.Remove(instanceUid);
            if (_rootMoundAppliedByInstanceUid.IsCreated)
                _rootMoundAppliedByInstanceUid.Remove(instanceUid);
        }

        private void RegisterDefoliantDestroyedInstance(uint instanceUid, int templateIndex, Vector3 instancePosition)
        {
            if (instanceUid == 0u || !_destroyedByInstanceUid.IsCreated)
                return;

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            PrimeDecompositionState(instanceUid, Time.time - OrganicDecompositionDurationSeconds);
            ClearOrganicLifecycleState(instanceUid);

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);
        }

        private byte ResolveRuntimeFlags(uint instanceUid, HarvestableTemplate.MaterialClass materialClass, int semanticType, float existingRuntimeFlags)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(existingRuntimeFlags);

            if (_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte existingFlags))
                return existingFlags;

            byte resolvedFlags = HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(existingRuntimeFlags);
            bool parasiteEligible = materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                                    materialClass == HarvestableTemplate.MaterialClass.Sargassum;
            if (parasiteEligible)
            {
                uint parasiteHash = instanceUid ^ (uint)(semanticType + 17) * 2246822519u;
                if ((parasiteHash & 0x0Fu) <= 1u)
                    resolvedFlags |= FloraRuntimeFlagHasParasite;
            }

            _runtimeFlagsByInstanceUid.TryAdd(instanceUid, resolvedFlags);
            return resolvedFlags;
        }

        private static void ApplyRuntimeFlags(ref NativeArray<HectonVegetationInstanceData> metadata, int activeIndex, byte runtimeFlags)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData flaggedMetadata = metadata[activeIndex];
            flaggedMetadata.RuntimeFlags = HectonVegetationRuntimeFlagEncoding.WithRuntimeFlags(flaggedMetadata.RuntimeFlags, runtimeFlags);
            metadata[activeIndex] = flaggedMetadata;
        }

        private void ApplyRuntimeFlagsToLaneInstance(bool underwater, int activeIndex, byte runtimeFlags)
        {
            if (underwater)
                ApplyRuntimeFlags(ref _underwaterMetadata, activeIndex, runtimeFlags);
            else
                ApplyRuntimeFlags(ref _surfaceMetadata, activeIndex, runtimeFlags);
        }

        private byte MarkDeadRuntimeFlag(uint instanceUid)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return 0;

            byte runtimeFlags = 0;
            _runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out runtimeFlags);
            runtimeFlags |= FloraRuntimeFlagDead;
            _runtimeFlagsByInstanceUid.Remove(instanceUid);
            _runtimeFlagsByInstanceUid.TryAdd(instanceUid, runtimeFlags);
            return runtimeFlags;
        }

        private void ClearDeadRuntimeFlag(uint instanceUid)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            if (!_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte runtimeFlags))
                return;

            runtimeFlags &= unchecked((byte)~FloraRuntimeFlagDead);
            _runtimeFlagsByInstanceUid.Remove(instanceUid);
            _runtimeFlagsByInstanceUid.TryAdd(instanceUid, runtimeFlags);
        }

        private bool TryResolveNavObstacleForLaneInstance(bool underwater, int activeIndex, out float3 center, out float3 extents)
        {
            center = float3.zero;
            extents = float3.zero;
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int> semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length ||
                activeIndex >= semanticTypes.Length)
            {
                return false;
            }

            return VoxelDynamicNavGridRuntime.TryResolveMacroFloraObstacleWorldBounds(
                matrices[activeIndex],
                metadata[activeIndex],
                types[activeIndex],
                semanticTypes[activeIndex],
                out center,
                out extents);
        }

        private static void SetRuntimeState(ref NativeArray<HectonVegetationInstanceData> metadata, int activeIndex, float runtimeState)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData stateMetadata = metadata[activeIndex];
            stateMetadata.RuntimeState = runtimeState;
            metadata[activeIndex] = stateMetadata;
        }

        private void PrimeDecompositionState(uint instanceUid, float decompositionStartTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            _decompositionStartTimeByInstanceUid.Remove(instanceUid);
            _decompositionStartTimeByInstanceUid.TryAdd(instanceUid, decompositionStartTime);
        }

        private float ResolveOrPrimeDecompositionProgress(uint instanceUid, float currentTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || instanceUid == 0u)
                return 1f;

            if (!_decompositionStartTimeByInstanceUid.TryGetValue(instanceUid, out float startTime))
            {
                startTime = currentTime - OrganicDecompositionDurationSeconds;
                _decompositionStartTimeByInstanceUid.TryAdd(instanceUid, startTime);
            }

            return math.saturate((currentTime - startTime) / OrganicDecompositionDurationSeconds);
        }

        private void ApplyDecompositionToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float entropy01)
        {
            if (underwater)
                ApplyDecompositionMetadata(ref _underwaterMetadata, activeIndex, instanceUid, entropy01);
            else
                ApplyDecompositionMetadata(ref _surfaceMetadata, activeIndex, instanceUid, entropy01);
        }

        internal bool TryEvaluateParasiteExposure(Vector3 runtimePosition, out float exposure01)
        {
            exposure01 = 0f;
            float bestExposure = 0f;
            EvaluateParasiteExposureInLane(runtimePosition, false, ref bestExposure);
            EvaluateParasiteExposureInLane(runtimePosition, true, ref bestExposure);
            exposure01 = Mathf.Clamp01(bestExposure);
            return exposure01 > 0.0001f;
        }

        private void ApplyDamageVisualState(
            uint instanceUid,
            bool underwater,
            int activeIndex,
            int templateIndex,
            float baseHealth,
            float currentHealth,
            float transitionHeightScale,
            bool harvestStateChanged,
            float currentTime)
        {
            float damage01 = ResolveDamageProgress(baseHealth, currentHealth);
            UpdateDamageProgressCache(instanceUid, damage01);
            if (damage01 <= 0.0001f && !harvestStateChanged)
                return;

            float normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            float normalizedHeightScale = harvestStateChanged
                ? math.saturate(transitionHeightScale)
                : ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, normalizedHealth);
            ApplyDamageToLaneInstance(underwater, activeIndex, instanceUid, templateIndex, normalizedHealth, damage01, normalizedHeightScale, currentTime);
        }

        private void UpdateDamageProgressCache(uint instanceUid, float damage01)
        {
            if (!_damageVisualProgressByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            _damageVisualProgressByInstanceUid.Remove(instanceUid);
            if (damage01 > 0.0001f)
                _damageVisualProgressByInstanceUid.TryAdd(instanceUid, damage01);
        }

        private static float ResolveDamageProgress(float baseHealth, float currentHealth)
        {
            float normalizedHealth = currentHealth / math.max(0.0001f, baseHealth);
            return math.saturate((0.5f - normalizedHealth) * 2f);
        }

        private HarvestState ResolveHarvestState(int templateIndex, float baseHealth, float currentHealth, float normalizedHeightScale)
        {
            if (currentHealth <= 0.0001f || normalizedHeightScale <= 0.0001f)
                return HarvestState.Dead;

            float normalizedHealth = currentHealth / math.max(0.0001f, baseHealth);
            if (normalizedHealth >= HarvestStatePartialThreshold01 && normalizedHeightScale >= HarvestStatePartialThreshold01)
                return HarvestState.Pristine;

            float bareThreshold = ResolveBareThreshold01(templateIndex);
            return normalizedHealth <= bareThreshold || normalizedHeightScale <= bareThreshold
                ? HarvestState.Bare
                : HarvestState.PartiallyHarvested;
        }

        private float ResolveNormalizedHeightScale(int templateIndex, float baseHealth, float currentHealth)
        {
            float normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            HarvestState state = ResolveHarvestState(templateIndex, baseHealth, currentHealth, normalizedHealth);
            switch (state)
            {
                case HarvestState.Pristine:
                    return 1f;
                case HarvestState.PartiallyHarvested:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolvePartialHeightCeiling01(templateIndex)), MinimumDecomposedHeightScale, 1f);
                case HarvestState.Bare:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)), SoftBareHealthFloor01, 1f);
                default:
                    return MinimumDecomposedHeightScale;
            }
        }

        private float ResolveBareThreshold01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.42f;
                case FloraDataTemplate.FloraCategory.MicroGrass:
                    return 0.22f;
                default:
                    return HarvestStateBareThreshold01;
            }
        }

        private float ResolvePartialHeightCeiling01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    return 0.68f;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    return 0.74f;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.90f;
                default:
                    return 0.82f;
            }
        }

        private float ResolveBareHeightCeiling01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    return 0.18f;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    return 0.24f;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.58f;
                default:
                    return 0.20f;
            }
        }

        private FloraDataTemplate.FloraCategory ResolveDescriptorCategory(int templateIndex)
        {
            if (_floraCategoryByDescriptorIndex == null || templateIndex < 0 || templateIndex >= _floraCategoryByDescriptorIndex.Length)
                return FloraDataTemplate.FloraCategory.MicroGrass;

            return (FloraDataTemplate.FloraCategory)_floraCategoryByDescriptorIndex[templateIndex];
        }

        private static float ResolveHarvestStateRuntimeState(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                case HarvestState.Dead:
                    return HectonVegetationInstanceData.RuntimeStateDying;
                case HarvestState.PartiallyHarvested:
                    return HectonVegetationInstanceData.RuntimeStateAgitated;
                default:
                    return HectonVegetationInstanceData.RuntimeStateIdle;
            }
        }

        private byte ResolveDescriptorAudioMaterialId(int templateIndex)
        {
            if (_audioMaterialByDescriptorIndex == null || templateIndex < 0 || templateIndex >= _audioMaterialByDescriptorIndex.Length)
                return (byte)FloraDataTemplate.AudioMaterialId.Organic;

            return _audioMaterialByDescriptorIndex[templateIndex];
        }

        private float ResolveNormalizedHeightScaleFromHarvestState(int templateIndex, float normalizedHealth, HarvestState harvestState)
        {
            normalizedHealth = Mathf.Clamp01(normalizedHealth);
            switch (harvestState)
            {
                case HarvestState.Pristine:
                    return 1f;
                case HarvestState.PartiallyHarvested:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolvePartialHeightCeiling01(templateIndex)), MinimumDecomposedHeightScale, 1f);
                case HarvestState.Bare:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)), SoftBareHealthFloor01, 1f);
                default:
                    return MinimumDecomposedHeightScale;
            }
        }

        internal bool IsBareHarvestState(byte packedHarvestState)
        {
            return packedHarvestState == (byte)HarvestState.Bare;
        }

        private void DispatchHarvestAudioTransition(
            uint instanceUid,
            int templateIndex,
            HarvestState previousState,
            HarvestState nextState,
            Vector3 instancePosition)
        {
            if (templateIndex < 0 ||
                templateIndex >= _templateDescriptors.Length ||
                instanceUid == 0u ||
                previousState == nextState)
            {
                return;
            }

            AudioClip clip = ResolveHarvestAudioClip(ResolveDescriptorAudioMaterialId(templateIndex), nextState);
            if (clip == null)
                return;

            float volume = ResolveHarvestAudioVolume(nextState);
            float pitch = ResolveHarvestAudioPitch(nextState);
            AbsoluteUniversePosition soundAup = AbsoluteUniversePosition.FromRuntimePosition(instancePosition);
            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager)
            {
                spatialAudioManager.PlayHarvestAtAup(soundAup, clip, volume, pitch);
                return;
            }

            GlobalRegistry.Audio?.PlayAtPoint(clip, instancePosition, volume, pitch);
        }

        private AudioClip ResolveHarvestAudioClip(byte audioMaterialId, HarvestState harvestState)
        {
            switch ((FloraDataTemplate.AudioMaterialId)audioMaterialId)
            {
                case FloraDataTemplate.AudioMaterialId.Brittle:
                    return brittleHarvestClip;
                case FloraDataTemplate.AudioMaterialId.Fibrous:
                    return fibrousHarvestClip != null ? fibrousHarvestClip : organicHarvestClip;
                case FloraDataTemplate.AudioMaterialId.Metallic:
                    return metallicHarvestClip != null ? metallicHarvestClip : brittleHarvestClip;
                default:
                    return harvestState == HarvestState.PartiallyHarvested && fibrousHarvestClip != null
                        ? fibrousHarvestClip
                        : organicHarvestClip;
            }
        }

        private float ResolveHarvestAudioVolume(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                    return Mathf.Clamp01(harvestAudioBaseVolume * 1.15f);
                case HarvestState.Dead:
                    return Mathf.Clamp01(harvestAudioBaseVolume * 1.25f);
                default:
                    return harvestAudioBaseVolume;
            }
        }

        private static float ResolveHarvestAudioPitch(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                    return 0.9f;
                case HarvestState.Dead:
                    return 0.82f;
                default:
                    return 1f;
            }
        }

        private void TryDispatchMatureSporeAcoustic(
            uint instanceUid,
            float progress01,
            bool underwater,
            int activeIndex,
            int templateIndex,
            float currentTime)
        {
            if (instanceUid == 0u || !_nextSporeAcousticTimeByInstanceUid.IsCreated)
                return;

            if (progress01 < MatureSporeGrowthThreshold01)
            {
                _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
                return;
            }

            if (!IsMatureSporeAcousticEmitter(templateIndex))
                return;

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
                return;

            AudioClip clip = ResolveMatureSporeAcousticClip(templateIndex);
            if (clip == null)
                return;

            float pulseFrequency = ResolveMatureSporePulseFrequency(templateIndex);
            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            float phaseOffset01 = ResolveSporeShaderPhaseOffset01(instancePosition);
            if (!_nextSporeAcousticTimeByInstanceUid.TryGetValue(instanceUid, out float nextAllowedTime))
            {
                nextAllowedTime = ResolveNextSporePulseTime(currentTime, pulseFrequency, phaseOffset01);
                _nextSporeAcousticTimeByInstanceUid.TryAdd(instanceUid, nextAllowedTime);
                if (currentTime < nextAllowedTime)
                    return;
            }
            else if (currentTime < nextAllowedTime)
            {
                return;
            }

            float volume = ResolveMatureSporeAcousticVolume(templateIndex);
            float pitch = ResolveMatureSporeAcousticPitch(pulseFrequency);
            AbsoluteUniversePosition soundAup = AbsoluteUniversePosition.FromRuntimePosition(instancePosition);
            SporeAcousticEvent acousticEvent = new SporeAcousticEvent(
                soundAup,
                instancePosition,
                clip,
                pulseFrequency,
                volume,
                pitch,
                nextAllowedTime,
                phaseOffset01);
            DispatchSporeAcousticEvent(in acousticEvent);

            _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
            _nextSporeAcousticTimeByInstanceUid.TryAdd(instanceUid, ResolveNextSporePulseTime(currentTime + 0.0001f, pulseFrequency, phaseOffset01));
        }

        private static void DispatchSporeAcousticEvent(in SporeAcousticEvent acousticEvent)
        {
            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager)
            {
                spatialAudioManager.PlaySporeEmissionAtAup(
                    acousticEvent.PositionAup,
                    acousticEvent.Clip,
                    acousticEvent.PulseFrequencyHz,
                    acousticEvent.SimulationTimeSeconds,
                    acousticEvent.PhaseOffset01,
                    acousticEvent.Volume);
                return;
            }

            GlobalRegistry.Audio?.PlayAtPoint(
                acousticEvent.Clip,
                acousticEvent.RuntimePosition,
                acousticEvent.Volume,
                acousticEvent.Pitch);
        }

        private static float ResolveSporeShaderPhaseOffset01(Vector3 instancePosition)
        {
            return math.frac((instancePosition.x * SporeShaderPhasePositionX + instancePosition.z * SporeShaderPhasePositionZ) * InvTwoPi);
        }

        private static float ResolveNextSporePulseTime(float simulationTimeSeconds, float pulseFrequencyHz, float phaseOffset01)
        {
            float safePulseFrequency = math.max(MinimumSporePulseFrequencyHz, pulseFrequencyHz);
            float currentCycle = simulationTimeSeconds * safePulseFrequency + phaseOffset01 - SporePulsePeakPhase01;
            float nextCycle = math.floor(currentCycle) + 1f;
            return (nextCycle + SporePulsePeakPhase01 - phaseOffset01) / safePulseFrequency;
        }

        private static bool IsMatureGrowth(HectonVegetationInstanceData metadata)
        {
            return metadata.Reserved0 <= 0.0001f || metadata.Reserved0 >= MatureSporeGrowthThreshold01;
        }

        private bool IsMatureSporeAcousticEmitter(int templateIndex)
        {
            return _sporeAcousticEmitterByDescriptorIndex != null &&
                   templateIndex >= 0 &&
                   templateIndex < _sporeAcousticEmitterByDescriptorIndex.Length &&
                   _sporeAcousticEmitterByDescriptorIndex[templateIndex] != 0;
        }

        private AudioClip ResolveMatureSporeAcousticClip(int templateIndex)
        {
            if (_sporeAcousticClipByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporeAcousticClipByDescriptorIndex.Length &&
                _sporeAcousticClipByDescriptorIndex[templateIndex] != null)
            {
                return _sporeAcousticClipByDescriptorIndex[templateIndex];
            }

            return sporeAcousticFallbackClip != null
                ? sporeAcousticFallbackClip
                : ResolveHarvestAudioClip(ResolveDescriptorAudioMaterialId(templateIndex), HarvestState.PartiallyHarvested);
        }

        private float ResolveMatureSporePulseFrequency(int templateIndex)
        {
            if (_sporePulseFrequencyByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporePulseFrequencyByDescriptorIndex.Length)
            {
                return Mathf.Max(MinimumSporePulseFrequencyHz, _sporePulseFrequencyByDescriptorIndex[templateIndex]);
            }

            return 1f;
        }

        private float ResolveMatureSporeAcousticVolume(int templateIndex)
        {
            if (_sporeAcousticVolumeByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporeAcousticVolumeByDescriptorIndex.Length &&
                _sporeAcousticVolumeByDescriptorIndex[templateIndex] > 0.0001f)
            {
                return Mathf.Clamp01(_sporeAcousticVolumeByDescriptorIndex[templateIndex]);
            }

            return sporeAcousticFallbackVolume;
        }

        private static float ResolveMatureSporeAcousticPitch(float pulseFrequency)
        {
            return Mathf.Clamp(pulseFrequency, 0.1f, 3f);
        }

        private bool TryResolvePersistedFloraState(uint instanceUid, out float normalizedHealth, out float normalizedHeightScale)
        {
            normalizedHealth = 1f;
            normalizedHeightScale = 1f;
            if (!_persistedHealth01ByInstanceUid.IsCreated || !_persistedHeightScale01ByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            bool hasHealth = _persistedHealth01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHealth);
            bool hasHeight = _persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHeight);
            if (!hasHealth || !hasHeight)
                return false;

            normalizedHealth = Mathf.Clamp01((float)persistedHealth);
            normalizedHeightScale = Mathf.Clamp01((float)persistedHeight);
            return true;
        }

        private float ResolvePersistedNormalizedHeightScale(uint instanceUid)
        {
            if (!_persistedHeightScale01ByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHeight))
            {
                return 0f;
            }

            return Mathf.Clamp01((float)persistedHeight);
        }

        private float ResolveRuntimeNormalizedHeightScale(uint instanceUid, HectonVegetationInstanceData metadata)
        {
            if (!_baseScaleByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 baseScale))
            {
                return Mathf.Clamp01(Mathf.Abs(metadata.HeightScale));
            }

            return Mathf.Clamp01(Mathf.Abs(metadata.HeightScale) / math.max(0.0001f, baseScale.x));
        }

        private void ClearPersistedFloraStateOverride(uint instanceUid)
        {
            if (_persistedHealth01ByInstanceUid.IsCreated)
                _persistedHealth01ByInstanceUid.Remove(instanceUid);

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
                _persistedHeightScale01ByInstanceUid.Remove(instanceUid);
        }

        private void PersistFloraStateOverride(
            uint instanceUid,
            int templateIndex,
            Vector3 instancePosition,
            bool underwater,
            int activeIndex,
            float baseHealth,
            float currentHealth)
        {
            if (instanceUid == 0u || templateIndex < 0 || templateIndex >= _templateDescriptors.Length)
                return;

            float normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            float normalizedHeightScale = ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, normalizedHealth);
            HarvestState harvestState = ResolveHarvestState(templateIndex, baseHealth, currentHealth, normalizedHeightScale);
            if (PersistentWorldRegistry.IsPristineFloraState(normalizedHealth, normalizedHeightScale))
            {
                GlobalRegistry.PersistentWorldRegistry?.TryClearFloraStateOverride(instanceUid);
                ClearPersistedFloraStateOverride(instanceUid);
                return;
            }

            if (_persistedHealth01ByInstanceUid.IsCreated)
            {
                _persistedHealth01ByInstanceUid.Remove(instanceUid);
                _persistedHealth01ByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)normalizedHealth);
            }

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
            {
                _persistedHeightScale01ByInstanceUid.Remove(instanceUid);
                _persistedHeightScale01ByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)normalizedHeightScale);
            }

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return;

            registry.TryRegisterFloraStateOverride(
                (ulong)(uint)_templateDescriptors[templateIndex].StableHashId,
                instanceUid,
                instancePosition,
                normalizedHealth,
                (byte)harvestState);
        }

        private float ResolveCurrentNormalizedHeightScale(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            float fallbackNormalizedHeightScale)
        {
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return Mathf.Clamp01(fallbackNormalizedHeightScale);

            return ResolveRuntimeNormalizedHeightScale(instanceUid, metadata[activeIndex]);
        }

        private float GetLaneHealth(bool underwater, int activeIndex)
        {
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            return laneHealth.IsCreated && (uint)activeIndex < (uint)laneHealth.Length ? (float)laneHealth[activeIndex] : 0f;
        }

        private void SetLaneHealth(bool underwater, int activeIndex, float value)
        {
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            if (!laneHealth.IsCreated || activeIndex < 0 || activeIndex >= laneHealth.Length)
                return;

            laneHealth[activeIndex] = (Unity.Mathematics.half)Mathf.Max(0f, value);
        }

        private void SuppressActiveInstance(bool underwater, int activeIndex)
        {
            if (underwater)
                SuppressActiveInstance(ref _underwaterMatrices, ref _underwaterMetadata, activeIndex);
            else
                SuppressActiveInstance(ref _surfaceMatrices, ref _surfaceMetadata, activeIndex);
        }

        private void ApplyWiltToLaneInstance(bool underwater, int activeIndex, float wiltEndTime)
        {
            float wiltStartTime = wiltEndTime - OrganicWiltDurationSeconds;
            if (underwater)
                ApplyWiltMetadata(ref _underwaterMetadata, activeIndex, wiltStartTime);
            else
                ApplyWiltMetadata(ref _surfaceMetadata, activeIndex, wiltStartTime);
        }

        private void ApplyDamageToLaneInstance(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            float normalizedHealth,
            float damage01,
            float normalizedHeightScale,
            float currentTime)
        {
            if (underwater)
                ApplyPersistedDamageMetadata(ref _underwaterMetadata, activeIndex, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
            else
                ApplyPersistedDamageMetadata(ref _surfaceMetadata, activeIndex, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
        }

        private void UpdateRegrowthVisuals()
        {
            if (!_regrowthProgressByInstanceUid.IsCreated || _regrowthProgressByInstanceUid.Count <= 0)
                return;

            UpdateRegrowthLane(false);
            UpdateRegrowthLane(true);
        }

        private void UpdateRegrowthLane(bool underwater)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || !_regrowthProgressByInstanceUid.TryGetValue(instanceUid, out float progress01))
                    continue;

                ApplyRegrowthVisualToLaneInstance(underwater, i, instanceUid, progress01);
            }
        }

        private void UpdateMatureSporeAcoustics(float currentTime)
        {
            if (!_nextSporeAcousticTimeByInstanceUid.IsCreated ||
                _sporeAcousticEmitterByDescriptorIndex == null ||
                _sporeAcousticEmitterByDescriptorIndex.Length == 0)
            {
                return;
            }

            UpdateMatureSporeAcousticLane(false, currentTime, ref _surfaceMatureSporeScanCursor);
            UpdateMatureSporeAcousticLane(true, currentTime, ref _underwaterMatureSporeScanCursor);
        }

        private void UpdateMatureSporeAcousticLane(bool underwater, float currentTime, ref int scanCursor)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || !materialClasses.IsCreated || count <= 0)
            {
                scanCursor = 0;
                return;
            }

            int safeCount = math.min(count, math.min(instanceUids.Length, math.min(metadata.Length, materialClasses.Length)));
            if (safeCount <= 0)
            {
                scanCursor = 0;
                return;
            }

            if ((uint)scanCursor >= (uint)safeCount)
                scanCursor = 0;

            int budget = math.min(Mathf.Max(1, matureSporeAcousticScanBudgetPerTick), safeCount);
            for (int checkedCount = 0; checkedCount < budget; checkedCount++)
            {
                int activeIndex = scanCursor;
                scanCursor++;
                if (scanCursor >= safeCount)
                    scanCursor = 0;

                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
                {
                    continue;
                }

                HectonVegetationInstanceData instanceData = metadata[activeIndex];
                if (!IsMatureGrowth(instanceData) ||
                    instanceData.HealthNormalized < MatureSporeGrowthThreshold01 ||
                    instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    math.abs(instanceData.HeightScale) <= 0.0001f)
                {
                    continue;
                }

                int templateIndex = ResolveTemplateIndex(instanceData, (HarvestableTemplate.MaterialClass)materialClasses[activeIndex]);
                if (!IsMatureSporeAcousticEmitter(templateIndex))
                    continue;

                TryDispatchMatureSporeAcoustic(instanceUid, 1f, underwater, activeIndex, templateIndex, currentTime);
            }
        }

        private void UpdateDecompositionVisuals(float currentTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || _decompositionStartTimeByInstanceUid.Count <= 0)
                return;

            UpdateDecompositionLane(false, currentTime);
            UpdateDecompositionLane(true, currentTime);
        }

        private void UpdateDecompositionLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    !_destroyedByInstanceUid.IsCreated ||
                    !_destroyedByInstanceUid.ContainsKey(instanceUid))
                {
                    continue;
                }

                float entropy01 = ResolveOrPrimeDecompositionProgress(instanceUid, currentTime);
                ApplyDecompositionMetadata(ref metadata, i, instanceUid, entropy01);
            }
        }

        private void UpdateDamageVisuals(float currentTime)
        {
            if (!_damageVisualProgressByInstanceUid.IsCreated || _damageVisualProgressByInstanceUid.Count <= 0)
                return;

            UpdateDamageLane(false, currentTime);
            UpdateDamageLane(true, currentTime);
        }

        private void UpdateDamageLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || !materialClasses.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    (_pendingWiltEndTimeByInstanceUid.IsCreated && _pendingWiltEndTimeByInstanceUid.ContainsKey(instanceUid)) ||
                    !_damageVisualProgressByInstanceUid.TryGetValue(instanceUid, out float damage01))
                {
                    continue;
                }

                float normalizedHeightScale = ResolvePersistedNormalizedHeightScale(instanceUid);
                if (normalizedHeightScale <= 0.0001f)
                    normalizedHeightScale = ResolveRuntimeNormalizedHeightScale(instanceUid, metadata[i]);

                int templateIndex = ResolveTemplateIndex(metadata[i], (HarvestableTemplate.MaterialClass)materialClasses[i]);
                float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                    ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                    : 1f;
                float normalizedHealth = _healthByInstanceUid.IsCreated && _healthByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half trackedHealth)
                    ? Mathf.Clamp01((float)trackedHealth / baseHealth)
                    : Mathf.Clamp01(1f - (damage01 * 0.5f));
                ApplyPersistedDamageMetadata(ref metadata, i, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
            }
        }

        private void UpdateWiltInstances(float currentTime)
        {
            if (!_pendingWiltEndTimeByInstanceUid.IsCreated || _pendingWiltEndTimeByInstanceUid.Count <= 0)
                return;

            UpdateWiltLane(false, currentTime);
            UpdateWiltLane(true, currentTime);
        }

        private void UpdateWiltLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || !_pendingWiltEndTimeByInstanceUid.TryGetValue(instanceUid, out float wiltEndTime))
                    continue;

                if (currentTime >= wiltEndTime)
                {
                    SuppressActiveInstance(underwater, i);
                    _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
                    continue;
                }

                ApplyWiltMetadata(ref metadata, i, wiltEndTime - OrganicWiltDurationSeconds);
            }
        }

        private static void SuppressActiveInstance(
            ref NativeArray<Matrix4x4> matrices,
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex)
        {
            if (!matrices.IsCreated || !metadata.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length || activeIndex >= metadata.Length)
                return;

            Matrix4x4 hiddenMatrix = matrices[activeIndex];
            hiddenMatrix.m03 = 0f;
            hiddenMatrix.m13 = HiddenInstanceWorldY;
            hiddenMatrix.m23 = 0f;
            matrices[activeIndex] = hiddenMatrix;

            HectonVegetationInstanceData hiddenMetadata = metadata[activeIndex];
            hiddenMetadata.Type = 0f;
            hiddenMetadata.HeightScale = 0f;
            hiddenMetadata.WidthScale = 0f;
            hiddenMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateIdle;
            hiddenMetadata.RuntimeFlags = 0f;
            hiddenMetadata.HealthNormalized = 0f;
            metadata[activeIndex] = hiddenMetadata;
        }

        private static void ApplyWiltMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            float wiltStartTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData wiltMetadata = metadata[activeIndex];
            wiltMetadata.HeightScale = -Mathf.Max(0.05f, Mathf.Abs(wiltMetadata.HeightScale));
            wiltMetadata.WidthScale = Mathf.Max(0.001f, wiltStartTime);
            wiltMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
            wiltMetadata.HealthNormalized = 0f;
            metadata[activeIndex] = wiltMetadata;
        }

        private void ApplyDecompositionMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            uint instanceUid,
            float entropy01)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : new float2(1f, 1f);
            float smoothEntropy = entropy01 * entropy01 * (3f - (2f * entropy01));
            float decompositionStartTime = 0f;
            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.TryGetValue(instanceUid, out decompositionStartTime);

            HectonVegetationInstanceData decompositionMetadata = metadata[activeIndex];
            decompositionMetadata.HeightScale = -math.lerp(baseScale.x, MinimumDecomposedHeightScale, smoothEntropy);
            decompositionMetadata.WidthScale = -Mathf.Max(0.001f, decompositionStartTime);
            decompositionMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
            decompositionMetadata.HealthNormalized = math.lerp(1f, 0f, smoothEntropy);
            metadata[activeIndex] = decompositionMetadata;
        }

        private static void ApplyDamageMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            float damage01,
            float currentTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length || damage01 <= 0.0001f)
                return;

            HectonVegetationInstanceData damageMetadata = metadata[activeIndex];
            damageMetadata.HeightScale = -Mathf.Max(0.05f, Mathf.Abs(damageMetadata.HeightScale));
            damageMetadata.WidthScale = currentTime - (Mathf.Clamp01(damage01) * OrganicWiltDurationSeconds);
            damageMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateAgitated;
            damageMetadata.HealthNormalized = Mathf.Clamp01(1f - damage01);
            metadata[activeIndex] = damageMetadata;
        }

        private void ApplyPersistedDamageMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            float normalizedHealth,
            float normalizedHeightScale,
            float damage01,
            float currentTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HarvestState harvestState = ResolveHarvestState(
                templateIndex,
                1f,
                Mathf.Clamp01(normalizedHeightScale),
                Mathf.Clamp01(normalizedHeightScale));
            if (_baseScaleByInstanceUid.IsCreated &&
                _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 baseScale))
            {
                float clampedHeight01 = math.saturate(normalizedHeightScale);
                harvestState = ResolveHarvestState(templateIndex, baseScale.x, baseScale.x * clampedHeight01, clampedHeight01);
                HectonVegetationInstanceData damageMetadata = metadata[activeIndex];
                damageMetadata.HeightScale = -Mathf.Max(MinimumDecomposedHeightScale, baseScale.x * clampedHeight01);
                damageMetadata.WidthScale = currentTime - (Mathf.Clamp01(damage01) * OrganicWiltDurationSeconds);
                damageMetadata.RuntimeState = ResolveHarvestStateRuntimeState(harvestState);
                damageMetadata.HealthNormalized = math.saturate(normalizedHealth);
                metadata[activeIndex] = damageMetadata;
                return;
            }

            ApplyDamageMetadata(ref metadata, activeIndex, damage01, currentTime);
            HectonVegetationInstanceData fallbackMetadata = metadata[activeIndex];
            fallbackMetadata.RuntimeState = ResolveHarvestStateRuntimeState(harvestState);
            fallbackMetadata.HealthNormalized = math.saturate(normalizedHealth);
            metadata[activeIndex] = fallbackMetadata;
        }

        private void EvaluateParasiteExposureInLane(Vector3 runtimePosition, bool underwater, ref float bestExposure)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !laneHealth.IsCreated || !matrices.IsCreated || count <= 0 || !_runtimeFlagsByInstanceUid.IsCreated)
                return;

            const float parasiteRadius = 3.25f;
            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (float)laneHealth[i] <= 0.0001f ||
                    !_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte runtimeFlags) ||
                    (runtimeFlags & FloraRuntimeFlagHasParasite) == 0)
                {
                    continue;
                }

                Vector3 delta = ExtractTranslation(matrices[i]) - runtimePosition;
                float distanceSq = delta.sqrMagnitude;
                float radiusSq = parasiteRadius * parasiteRadius;
                if (distanceSq >= radiusSq)
                    continue;

                float exposure = 1f - math.saturate(distanceSq / radiusSq);
                if (exposure > bestExposure)
                    bestExposure = exposure;
            }
        }

        private bool TryApplyTitanRootMound(bool underwater, int activeIndex, uint instanceUid)
        {
            if (instanceUid == 0u ||
                !_rootMoundAppliedByInstanceUid.IsCreated ||
                _rootMoundAppliedByInstanceUid.ContainsKey(instanceUid))
            {
                return false;
            }

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return false;
            }

            if ((HectonVegetationInstanceType)types[activeIndex] != HectonVegetationInstanceType.GiantKelp)
                return false;

            HectonVegetationInstanceData instanceData = metadata[activeIndex];
            if (math.saturate(instanceData.Reserved0) < TitanRootMoundMatureThreshold01 &&
                math.saturate(instanceData.HealthNormalized) < TitanRootMoundMatureThreshold01 &&
                math.saturate(math.abs(instanceData.HeightScale)) < TitanRootMoundMatureThreshold01)
            {
                return false;
            }

            HectonVoxelEngine voxelEngine = HectonVoxelEngine.ActiveRuntimeInstance;
            if (voxelEngine == null)
                return false;

            Vector3 universePosition = ExtractTranslation(matrices[activeIndex]);
            Vector3 runtimePosition = HectonMapMagicVegetationBridge.ToRuntimeSpace(universePosition);
            if (!voxelEngine.TryGetNearestActiveVolume(runtimePosition, out HectonVoxelVolume volume) || volume == null)
                return false;

            volume.ApplyOrganicRootMound(runtimePosition, TitanRootMoundRadiusMeters, TitanRootMoundStrengthMeters);
            _rootMoundAppliedByInstanceUid.TryAdd(instanceUid, 1);
            return true;
        }

        internal bool IsMaterialClassRegrowable(ulong floraPersistentIdHash)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId != floraPersistentIdHash)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)_templateDescriptors[i].MaterialClassId;
                return materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                       materialClass == HarvestableTemplate.MaterialClass.Sargassum;
            }

            return false;
        }

        internal float ResolveGrowthTimeSeconds(ulong floraPersistentIdHash)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId != floraPersistentIdHash)
                    continue;

                if (_growthTimeSecondsByDescriptorIndex != null && i < _growthTimeSecondsByDescriptorIndex.Length)
                    return Mathf.Max(1f, _growthTimeSecondsByDescriptorIndex[i]);

                return 480f;
            }

            return 480f;
        }

        internal bool TryResolveFloraGrowthDescriptor(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType,
            out uint instanceUid,
            out ulong floraPersistentIdHash,
            out float growthTimeSeconds)
        {
            instanceUid = ComputeStableInstanceUid(matrix, metadata, typeId, semanticType);
            floraPersistentIdHash = 0UL;
            growthTimeSeconds = 0f;

            HarvestableTemplate.MaterialClass fallbackMaterialClass = ResolveMaterialClass(typeId, semanticType);
            int templateIndex = ResolveTemplateIndex(metadata, fallbackMaterialClass);
            if (templateIndex < 0 || templateIndex >= _templateDescriptors.Length)
                return false;

            floraPersistentIdHash = (ulong)(uint)_templateDescriptors[templateIndex].StableHashId;
            growthTimeSeconds = _growthTimeSecondsByDescriptorIndex != null && templateIndex < _growthTimeSecondsByDescriptorIndex.Length
                ? Mathf.Max(1f, _growthTimeSecondsByDescriptorIndex[templateIndex])
                : 480f;
            return floraPersistentIdHash != 0UL;
        }

        internal bool TrySetMaturationProgress(uint instanceUid, float progress01)
        {
            float multiplier = EvaluateMaturationScaleMultiplier(progress01);
            return TrySetMaturationProgress(instanceUid, progress01, multiplier, multiplier);
        }

        internal bool TrySetMaturationProgress(uint instanceUid, float progress01, float scaleMultiplier, float resourceYieldMultiplier)
        {
            if (instanceUid == 0u || !_maturationScaleByInstanceUid.IsCreated)
                return false;

            scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.1f, 1f);
            resourceYieldMultiplier = Mathf.Clamp(resourceYieldMultiplier, 0.1f, 1f);
            _maturationScaleByInstanceUid.Remove(instanceUid);
            if (scaleMultiplier < 0.9999f)
                _maturationScaleByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)scaleMultiplier);

            if (_maturationYieldByInstanceUid.IsCreated)
            {
                _maturationYieldByInstanceUid.Remove(instanceUid);
                if (resourceYieldMultiplier < 0.9999f)
                    _maturationYieldByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)resourceYieldMultiplier);
            }

            if (TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                ApplyMaturationVisualToLaneInstance(underwater, activeIndex, instanceUid, scaleMultiplier);
                TryDispatchMatureSporeAcoustic(instanceUid, progress01, underwater, activeIndex, templateIndex, Time.time);
                if (progress01 >= TitanRootMoundMatureThreshold01)
                    TryApplyTitanRootMound(underwater, activeIndex, instanceUid);
            }
            else if (progress01 < MatureSporeGrowthThreshold01 && _nextSporeAcousticTimeByInstanceUid.IsCreated)
            {
                _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
            }

            return true;
        }

        internal bool TryApplyLightStarvation(uint instanceUid, float starvation01)
        {
            if (instanceUid == 0u ||
                !TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex) ||
                (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
            {
                return false;
            }

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            if (!matrices.IsCreated ||
                !materialClasses.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= materialClasses.Length)
            {
                return false;
            }

            HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
            if (materialClass == HarvestableTemplate.MaterialClass.None)
                return false;

            float clampedStarvation01 = Mathf.Clamp01(starvation01);
            float baseHealth = ResolveBaseHealth(templateIndex);
            float currentHealth = GetLaneHealth(underwater, activeIndex);
            if (currentHealth <= 0.0001f)
                return false;

            float nextHealth = Mathf.Max(0f, currentHealth - (baseHealth * LightStarvationDamagePerSlowTick01 * clampedStarvation01));
            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            if (nextHealth <= baseHealth * LightStarvationDeathHealth01)
            {
                ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
                return true;
            }

            float normalizedHealth = Mathf.Clamp01(nextHealth / Mathf.Max(0.0001f, baseHealth));
            float normalizedHeightScale = Mathf.Clamp(
                Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)),
                SoftBareHealthFloor01,
                1f);
            ApplySuppressionState(
                underwater,
                activeIndex,
                instanceUid,
                templateIndex,
                instancePosition,
                baseHealth,
                nextHealth,
                normalizedHealth,
                normalizedHeightScale);
            return true;
        }

        internal bool TryApplyAllelopathicToxinSuppression(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType,
            float toxicity01)
        {
            if (!TryResolveFloraGrowthDescriptor(
                    matrix,
                    metadata,
                    typeId,
                    semanticType,
                    out uint instanceUid,
                    out _,
                    out _) ||
                instanceUid == 0u ||
                !TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex) ||
                (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
            {
                return false;
            }

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            if (!matrices.IsCreated ||
                !materialClasses.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= materialClasses.Length)
            {
                return false;
            }

            HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
            if (materialClass == HarvestableTemplate.MaterialClass.None)
                return false;

            float clampedToxicity01 = Mathf.Clamp01(toxicity01);
            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            if (clampedToxicity01 >= AllelopathicDeathThreshold01)
            {
                ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
                return true;
            }

            float baseHealth = ResolveBaseHealth(templateIndex);
            float normalizedHealth = math.lerp(ResolveBareThreshold01(templateIndex), AllelopathicBareHealth01, clampedToxicity01);
            float normalizedHeightScale = Mathf.Clamp(
                Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)),
                SoftBareHealthFloor01,
                1f);
            float nextHealth = baseHealth * normalizedHealth;
            ApplySuppressionState(
                underwater,
                activeIndex,
                instanceUid,
                templateIndex,
                instancePosition,
                baseHealth,
                nextHealth,
                normalizedHealth,
                normalizedHeightScale);
            return true;
        }

        internal static float EvaluateMaturationScaleMultiplier(float progress01)
        {
            float clampedProgress = math.saturate(progress01);
            float smoothProgress = clampedProgress * clampedProgress * (3f - (2f * clampedProgress));
            return math.lerp(0.1f, 1f, smoothProgress);
        }

        private float ResolveBaseHealth(int templateIndex)
        {
            return templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                : 1f;
        }

        private void ApplySuppressionState(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            Vector3 instancePosition,
            float baseHealth,
            float currentHealth,
            float normalizedHealth,
            float normalizedHeightScale)
        {
            SetLaneHealth(underwater, activeIndex, currentHealth);
            if (_healthByInstanceUid.IsCreated)
            {
                _healthByInstanceUid.Remove(instanceUid);
                _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)Mathf.Max(0f, currentHealth));
            }

            float damage01 = ResolveDamageProgress(baseHealth, currentHealth);
            UpdateDamageProgressCache(instanceUid, damage01);
            ApplyDamageToLaneInstance(
                underwater,
                activeIndex,
                instanceUid,
                templateIndex,
                Mathf.Clamp01(normalizedHealth),
                damage01,
                Mathf.Clamp01(normalizedHeightScale),
                Time.time);
            PersistFloraStateOverride(
                instanceUid,
                templateIndex,
                instancePosition,
                underwater,
                activeIndex,
                baseHealth,
                currentHealth);
        }

        private int ResolveDescriptorIndexByPersistentIdHash(ulong floraPersistentIdHash)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId == floraPersistentIdHash)
                    return i;
            }

            return -1;
        }

        internal bool HasTemplatePersistentIdHash(ulong floraPersistentIdHash)
        {
            return ResolveDescriptorIndexByPersistentIdHash(floraPersistentIdHash) >= 0;
        }

        private static HarvestState ResolvePersistedHarvestState(byte packedHarvestState)
        {
            if (packedHarvestState > (byte)HarvestState.Dead)
                return HarvestState.PartiallyHarvested;

            return (HarvestState)packedHarvestState;
        }

        internal bool IsTemplateMaterialClass(ulong floraPersistentIdHash, HarvestableTemplate.MaterialClass materialClass)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId != floraPersistentIdHash)
                    continue;

                return _templateDescriptors[i].MaterialClassId == (byte)materialClass;
            }

            return false;
        }

        internal bool TrySetRegrowthProgress(uint instanceUid, Vector3 runtimePosition, float progress01)
        {
            if (instanceUid == 0u ||
                !_regrowthProgressByInstanceUid.IsCreated ||
                !_regrowthPositionByInstanceUid.IsCreated)
            {
                return false;
            }

            if (ChemicalInfluenceGrid.IsInsidePermanentDefoliantDeadZone(runtimePosition))
                return false;

            progress01 = math.saturate(progress01);
            if (_destroyedByInstanceUid.IsCreated)
                _destroyedByInstanceUid.Remove(instanceUid);
            ClearDeadRuntimeFlag(instanceUid);
            MarkOrganicTouched(instanceUid, Time.time);

            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);

            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.Remove(instanceUid);

            _regrowthProgressByInstanceUid.Remove(instanceUid);
            _regrowthProgressByInstanceUid.TryAdd(instanceUid, progress01);
            _regrowthPositionByInstanceUid.Remove(instanceUid);
            _regrowthPositionByInstanceUid.TryAdd(instanceUid, new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z));

            if (TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                byte runtimeFlags = 0;
                if (_runtimeFlagsByInstanceUid.IsCreated)
                    _runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out runtimeFlags);

                ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
                ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, progress01);
                float health = ResolveRegrowthHealth(progress01, templateIndex);
                SetLaneHealth(underwater, activeIndex, health);
                _healthByInstanceUid.Remove(instanceUid);
                _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)health);
                if (progress01 >= TitanRootMoundMatureThreshold01)
                    TryApplyTitanRootMound(underwater, activeIndex, instanceUid);
            }

            if (progress01 >= 0.9999f)
                FinalizeRegrowth(instanceUid);

            return true;
        }

        private void FinalizeRegrowth(uint instanceUid)
        {
            if (_regrowthProgressByInstanceUid.IsCreated)
                _regrowthProgressByInstanceUid.Remove(instanceUid);

            if (_regrowthPositionByInstanceUid.IsCreated)
                _regrowthPositionByInstanceUid.Remove(instanceUid);

            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.Remove(instanceUid);
            MarkOrganicTouched(instanceUid, Time.time);

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);

            if (TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                    ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                    : 1f;
                SetLaneHealth(underwater, activeIndex, baseHealth);
                _healthByInstanceUid.Remove(instanceUid);
                _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)baseHealth);
                ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, 1f);
                return;
            }

            _healthByInstanceUid.Remove(instanceUid);
        }

        private float ResolveRegrowthHealth(float progress01, int templateIndex)
        {
            float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                : 1f;
            float smoothProgress = progress01 * progress01 * (3f - (2f * progress01));
            return Mathf.Max(0.05f, math.lerp(baseHealth * 0.1f, baseHealth, smoothProgress));
        }

        private float ResolveMaturationScaleMultiplier(uint instanceUid)
        {
            if (!_maturationScaleByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_maturationScaleByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half storedScale))
            {
                return 1f;
            }

            return Mathf.Clamp((float)storedScale, 0.1f, 1f);
        }

        private float ResolveMaturationYieldMultiplier(uint instanceUid)
        {
            if (!_maturationYieldByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_maturationYieldByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half storedYield))
            {
                return ResolveMaturationScaleMultiplier(instanceUid);
            }

            return Mathf.Clamp((float)storedYield, 0.1f, 1f);
        }

        private void ApplyMaturationVisualToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float scaleMultiplier)
        {
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return;
            }

            float clampedScale = Mathf.Clamp(scaleMultiplier, 0.1f, 1f);
            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : new float2(1f, 1f);
            HectonVegetationInstanceData maturationMetadata = metadata[activeIndex];
            maturationMetadata.Type = types[activeIndex];
            maturationMetadata.HeightScale = baseScale.x * clampedScale;
            maturationMetadata.WidthScale = baseScale.y * clampedScale;
            maturationMetadata.Reserved0 = clampedScale;
            metadata[activeIndex] = maturationMetadata;
        }

        private void ApplyRegrowthVisualToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float progress01)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return;
            }

            if (_regrowthPositionByInstanceUid.IsCreated &&
                _regrowthPositionByInstanceUid.TryGetValue(instanceUid, out float3 regrowthPosition))
            {
                Matrix4x4 visibleMatrix = matrices[activeIndex];
                visibleMatrix.m03 = regrowthPosition.x;
                visibleMatrix.m13 = regrowthPosition.y;
                visibleMatrix.m23 = regrowthPosition.z;
                matrices[activeIndex] = visibleMatrix;
            }

            float smoothProgress = progress01 * progress01 * (3f - (2f * progress01));
            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : new float2(1f, 1f);
            HectonVegetationInstanceData regrowthMetadata = metadata[activeIndex];
            regrowthMetadata.Type = types[activeIndex];
            regrowthMetadata.HeightScale = math.lerp(MinimumDecomposedHeightScale, baseScale.x, smoothProgress);
            regrowthMetadata.WidthScale = math.lerp(MinimumDecomposedWidthScale, baseScale.y, smoothProgress);
            regrowthMetadata.RuntimeState = progress01 >= 0.995f
                ? HectonVegetationInstanceData.RuntimeStateIdle
                : HectonVegetationInstanceData.RuntimeStateAgitated;
            regrowthMetadata.HealthNormalized = Mathf.Clamp01(progress01);
            regrowthMetadata.Reserved0 = Mathf.Clamp01(progress01);
            metadata[activeIndex] = regrowthMetadata;
        }

        private bool TryResolveActiveInstanceByUid(uint instanceUid, out bool underwater, out int activeIndex, out int templateIndex)
        {
            if (TryResolveActiveInstanceByUid(instanceUid, _surfaceInstanceUids, _surfaceCount, _surfaceMaterialClasses, _surfaceMetadata, out activeIndex, out templateIndex))
            {
                underwater = false;
                return true;
            }

            if (TryResolveActiveInstanceByUid(instanceUid, _underwaterInstanceUids, _underwaterCount, _underwaterMaterialClasses, _underwaterMetadata, out activeIndex, out templateIndex))
            {
                underwater = true;
                return true;
            }

            underwater = false;
            activeIndex = -1;
            templateIndex = -1;
            return false;
        }

        private bool TryResolveActiveInstanceByUid(
            uint instanceUid,
            NativeArray<uint> instanceUids,
            int count,
            NativeArray<byte> materialClasses,
            NativeArray<HectonVegetationInstanceData> metadata,
            out int activeIndex,
            out int templateIndex)
        {
            activeIndex = -1;
            templateIndex = -1;
            if (!instanceUids.IsCreated || !materialClasses.IsCreated || !metadata.IsCreated || count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (instanceUids[i] != instanceUid)
                    continue;

                activeIndex = i;
                templateIndex = ResolveTemplateIndex(metadata[i], (HarvestableTemplate.MaterialClass)materialClasses[i]);
                return true;
            }

            return false;
        }

        private float ResolveParentMassKg(
            bool underwater,
            int activeIndex,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex)
        {
            float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                : 1f;
            float height01 = 1f;
            float width01 = 1f;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            if (metadata.IsCreated && activeIndex >= 0 && activeIndex < metadata.Length)
            {
                HectonVegetationInstanceData instanceData = metadata[activeIndex];
                height01 = Mathf.Clamp01(Mathf.Abs(instanceData.HeightScale));
                width01 = Mathf.Clamp01(instanceData.WidthScale);
            }

            float maturationMultiplier = ResolveMaturationYieldMultiplier(_underwaterInstanceUids.IsCreated || _surfaceInstanceUids.IsCreated
                ? (underwater && activeIndex >= 0 && activeIndex < _underwaterCount ? _underwaterInstanceUids[activeIndex] :
                   !underwater && activeIndex >= 0 && activeIndex < _surfaceCount ? _surfaceInstanceUids[activeIndex] :
                   0u)
                : 0u);

            float resolvedMassKg = materialClass switch
            {
                HarvestableTemplate.MaterialClass.Kelp => Mathf.Max(1f, baseHealth * math.lerp(0.28f, 0.52f, height01) * math.lerp(0.9f, 1.15f, width01)),
                HarvestableTemplate.MaterialClass.Coral => Mathf.Max(2f, baseHealth * math.lerp(0.55f, 0.8f, height01)),
                HarvestableTemplate.MaterialClass.TitaniumOutcrop => Mathf.Max(4f, baseHealth * math.lerp(0.82f, 1.08f, height01)),
                HarvestableTemplate.MaterialClass.Sargassum => Mathf.Max(0.75f, baseHealth * math.lerp(0.22f, 0.38f, height01) * math.lerp(0.85f, 1.1f, width01)),
                _ => Mathf.Max(1f, baseHealth * 0.4f)
            };

            return Mathf.Max(0.05f, resolvedMassKg * maturationMultiplier);
        }

        private static HarvestableTemplate.MaterialClass ResolveMaterialClass(int typeId, int semanticType)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            HectonMapMagicVegetationBridge.VegetationSemanticType semantic = (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticType;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp || semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicKelp)
                return HarvestableTemplate.MaterialClass.Kelp;

            if (vegetationType == HectonVegetationInstanceType.Sargassum || semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum)
                return HarvestableTemplate.MaterialClass.Sargassum;

            return HarvestableTemplate.MaterialClass.None;
        }

        private static bool IsConsumableFloraMaterialClass(HarvestableTemplate.MaterialClass materialClass)
        {
            return materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                   materialClass == HarvestableTemplate.MaterialClass.Sargassum;
        }

        private static float ResolveConstructionDistanceSq(
            Vector3 centerUniversePosition,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = math.lerp(10f, 20f, math.saturate(metadata.HeightScale));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                Vector3 closest = ClosestPointOnSegment(rootPosition, top, centerUniversePosition);
                return (closest - centerUniversePosition).sqrMagnitude;
            }

            return (rootPosition - centerUniversePosition).sqrMagnitude;
        }

        private static float ResolveHarvestDistanceSq(
            Vector3 hitPoint,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId,
            float fallbackDistanceSq,
            float heightTolerance)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = math.lerp(10f, 20f, math.saturate(metadata.HeightScale));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                Vector3 closest = ClosestPointOnSegment(rootPosition, top, hitPoint);
                return (closest - hitPoint).sqrMagnitude;
            }

            return Mathf.Min((rootPosition - hitPoint).sqrMagnitude, fallbackDistanceSq + heightTolerance);
        }

        private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 segment = end - start;
            float segmentLengthSq = segment.sqrMagnitude;
            if (segmentLengthSq <= 0.0001f)
                return start;

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSq);
            return start + segment * t;
        }

        private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private static float ResolveFractionalVariation(float encodedVariation)
        {
            return Mathf.Repeat(encodedVariation, 1f);
        }

        internal static uint ComputeStableInstanceUid(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType)
        {
            int x = Mathf.RoundToInt(matrix.m03 * 100f);
            int y = Mathf.RoundToInt(matrix.m13 * 100f);
            int z = Mathf.RoundToInt(matrix.m23 * 100f);
            uint hx = (uint)x * 73856093u;
            uint hy = (uint)y * 19349663u;
            uint hz = (uint)z * 83492791u;
            uint hv = (uint)Mathf.RoundToInt(ResolveFractionalVariation(metadata.Variation) * 10000f) * 2654435761u;
            uint hs = (uint)(semanticType + 1) * 2246822519u;
            uint ht = (uint)(typeId + 1) * 3266489917u;
            uint mixed = hx ^ hy ^ hz ^ hv ^ hs ^ ht;
            return mixed == 0u ? 1u : mixed;
        }

        private static NativeArray<T> EnsureLaneCapacity<T>(ref NativeArray<T> array, int requiredCount, string label) where T : unmanaged
        {
            EnsureNativeCapacity(ref array, requiredCount, label);
            return array;
        }

        private static void EnsureNativeCapacity<T>(ref NativeArray<T> array, int requiredCount, string label) where T : unmanaged
        {
            if (requiredCount <= 0)
                return;

            if (array.IsCreated && array.Length >= requiredCount)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<T>(requiredCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<T>[requiredCount] - resized persistent entropy runtime lane - owner: DestructibleOrganicManager
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeHashMap(_healthByInstanceUid, NativeMemoryOwner, nameof(_healthByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_destroyedByInstanceUid, NativeMemoryOwner, nameof(_destroyedByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_pendingWiltEndTimeByInstanceUid, NativeMemoryOwner, nameof(_pendingWiltEndTimeByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_damageVisualProgressByInstanceUid, NativeMemoryOwner, nameof(_damageVisualProgressByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_decompositionStartTimeByInstanceUid, NativeMemoryOwner, nameof(_decompositionStartTimeByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_regrowthProgressByInstanceUid, NativeMemoryOwner, nameof(_regrowthProgressByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_regrowthPositionByInstanceUid, NativeMemoryOwner, nameof(_regrowthPositionByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_maturationScaleByInstanceUid, NativeMemoryOwner, nameof(_maturationScaleByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_maturationYieldByInstanceUid, NativeMemoryOwner, nameof(_maturationYieldByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_nextSporeAcousticTimeByInstanceUid, NativeMemoryOwner, nameof(_nextSporeAcousticTimeByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_baseScaleByInstanceUid, NativeMemoryOwner, nameof(_baseScaleByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_runtimeFlagsByInstanceUid, NativeMemoryOwner, nameof(_runtimeFlagsByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_lastOrganicTouchTimeByInstanceUid, NativeMemoryOwner, nameof(_lastOrganicTouchTimeByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_overgrownByInstanceUid, NativeMemoryOwner, nameof(_overgrownByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_rootMoundAppliedByInstanceUid, NativeMemoryOwner, nameof(_rootMoundAppliedByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_destroyedFloraScratch, NativeMemoryOwner, nameof(_destroyedFloraScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_floraStateOverrideScratch, NativeMemoryOwner, nameof(_floraStateOverrideScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_persistedHealth01ByInstanceUid, NativeMemoryOwner, nameof(_persistedHealth01ByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_persistedHeightScale01ByInstanceUid, NativeMemoryOwner, nameof(_persistedHeightScale01ByInstanceUid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_pendingYieldEvents, NativeMemoryOwner, nameof(_pendingYieldEvents), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_dropDebugScratch, NativeMemoryOwner, nameof(_dropDebugScratch), NativeMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, string label) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            list.Dispose();
            list = default;
        }

        private static void DisposeNativeHashMap<TKey, TValue>(ref NativeHashMap<TKey, TValue> map, string label)
            where TKey : unmanaged, System.IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeHashMap(NativeMemoryOwner, label);
            map.Dispose();
            map = default;
        }

        private static Vector3 NormalizeVector3Fast(Vector3 vector, Vector3 fallback)
        {
            float magnitudeSq = vector.sqrMagnitude;
            return magnitudeSq > 0.0001f ? vector * math.rsqrt(magnitudeSq) : fallback;
        }
    }
}
