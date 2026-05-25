using Hecton8.AtlasSignal;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.PDA;
using Hecton8.Quest;
using Hecton8.UI;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Progression
{
    /// <summary>
    /// Player-owned narrative bridge for event-only progression reactions.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Progression/Narrative Progression Bridge")]
    public sealed class NarrativeProgressionBridge : MonoBehaviour, IBiomeMatrixEventListener, IScanEventListener, IBaseIntegrityEventListener, IBaseAirlockEventListener
    {
        private const string ExitLifePodDiscoveryId = "first_hour_exit_lifepod";
        private const string AtlasSignalDiscoveryId = "atlas6_signal_identified";
        private const string AtlasSignalQuestId = "quest_atlas_signal_detected";
        private const string AtlasMarkerId = "narrative_atlas_signal_source";
        private const string AtlasMarkerTitle = "ENCRYPTED SIGNAL SOURCE";
        private const string HullFailureDiscoveryId = "hull_failure_voice_log";
        private const string HullFailureLogId = "captain_last_broadcast";
        private const int MaxBiomeMarkerRules = 32;
        private static readonly uint _atlasSignalDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(AtlasSignalDiscoveryId);
        private static readonly uint _atlasSignalQuestHash = QuestFlagHashKernel.ComputeStableHash(AtlasSignalQuestId);
        private static readonly uint _atlasMarkerHash = QuestFlagHashKernel.ComputeStableHash(AtlasMarkerId);
        private static readonly uint _atlasMarkerTitleHash = QuestFlagHashKernel.ComputeStableHash(AtlasMarkerTitle);
        private static readonly uint _exitLifePodDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(ExitLifePodDiscoveryId);
        private static readonly uint _hullFailureDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(HullFailureDiscoveryId);
        private static readonly uint _hullFailureLogHash = QuestFlagHashKernel.ComputeStableHash(HullFailureLogId);

        [Header("First Hour AUP Gate")]
        [SerializeField] private Vector3 lifePodExitReferenceWorld = Vector3.zero;
        [SerializeField, Min(0f)] private float lifePodExitMinimumDistanceMeters = 0f;
        [SerializeField] private uint lifePodAirlockHashId;
        [SerializeField] private bool requireWetLifePodExit = true;

        [Header("Biome Marker Discovery")]
        [SerializeField] private BiomeMarkerRule[] biomeMarkerRules = new BiomeMarkerRule[0];

#pragma warning disable 0649 // Unity serialization assigns marker authoring fields.
        [System.Serializable]
        private sealed class BiomeMarkerRule
        {
            public int biomeId;
            public string requiredQuestId;
            public string requiredDiscoveryId;
            public string markerId;
            public string title;
            public Vector3 markerWorldPosition;
            public MarkerIconType iconType = MarkerIconType.Objective;
            public bool visibleOnHud = true;
            [System.NonSerialized] public uint requiredQuestHash;
            [System.NonSerialized] public uint requiredDiscoveryHash;
            [System.NonSerialized] public uint markerHashId;
            [System.NonSerialized] public uint titleHashId;
        }
#pragma warning restore 0649

        private static readonly char[] s_newArchiveDataMessage =
        {
            'N','E','W',' ','D','A','T','A',' ','A','D','D','E','D',' ','T','O',' ','A','R','C','H','I','V','E'
        };

        private bool _exitLifePodIssued;
        private bool _atlasMarkerPublished;
        private bool _hullFailureIssued;
        private uint _revealedBiomeMarkerMask;
        private int _lastBiomeMatrixId = int.MinValue;
        // COLD ALLOC: int[32] - biome marker rule index buckets for event-time sector lookup - owner: NarrativeProgressionBridge
        private readonly int[] _biomeRuleIndices = new int[MaxBiomeMarkerRules];
        // COLD ALLOC: int[32] - biome IDs owning marker-rule buckets - owner: NarrativeProgressionBridge
        private readonly int[] _biomeRuleBucketBiomeIds = new int[MaxBiomeMarkerRules];
        // COLD ALLOC: int[32] - bucket start offsets into _biomeRuleIndices - owner: NarrativeProgressionBridge
        private readonly int[] _biomeRuleBucketStarts = new int[MaxBiomeMarkerRules];
        // COLD ALLOC: int[32] - bucket rule counts - owner: NarrativeProgressionBridge
        private readonly int[] _biomeRuleBucketCounts = new int[MaxBiomeMarkerRules];
        // COLD ALLOC: int[32] - transient boot-time bucket fill offsets - owner: NarrativeProgressionBridge
        private readonly int[] _biomeRuleBucketWriteOffsets = new int[MaxBiomeMarkerRules];
        private int _biomeRuleBucketCount;
        private bool _biomeRuleHashesCached;

        private void Awake()
        {
            CacheRuleHashes();
        }

        private void OnEnable()
        {
            EnsureRuleHashesCached();
            BiomeMatrixEvents.Register(this);
            BaseAirlockEvents.Register(this);
            ScanEvents.Register(this);
            BaseIntegrityEvents.Register(this);
        }

        private void OnDisable()
        {
            BaseIntegrityEvents.Unregister(this);
            ScanEvents.Unregister(this);
            BaseAirlockEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            BaseIntegrityEvents.Unregister(this);
            ScanEvents.Unregister(this);
            BaseAirlockEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
        }

        public void OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return;

            int biomeId = profile.matrixIndex;
            if (biomeId == _lastBiomeMatrixId)
                return;

            _lastBiomeMatrixId = biomeId;
            TryPublishBiomeMarkers(biomeId);
            TryPublishAtlasMarker(profile);
        }

        public void OnDepthTierChanged(int depthTier, float depthMeters)
        {
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            if ((ScanEventType)payload.EventType != ScanEventType.EntryDiscovered)
                return;

            if (!IsSpeciesScan(in payload))
                return;

            ShowNewArchiveDataMilestone();
        }

        public void OnBaseIntegrityEvent(in BaseIntegrityEventPayload payload)
        {
            if ((BaseIntegrityEventType)payload.EventType != BaseIntegrityEventType.Breached || _hullFailureIssued)
                return;

            _hullFailureIssued = true;
            NarrativeEvents.TryRaiseDiscoveryMade(_hullFailureDiscoveryHash);
            ProceduralAudioEvents.TryRaiseStructuralStressTriggered(ResolvePlayerRuntimePosition(), 1f, 0.72f);

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            if (audioLogs != null)
            {
                audioLogs.NotifyAtmosphericWarningStarted(0.7f);
                audioLogs.TryPlayLogByHash(_hullFailureLogHash);
            }
        }

        public void OnBaseAirlockEvent(in BaseAirlockEventPayload payload)
        {
            if (_exitLifePodIssued)
                return;

            if (BaseAirlockEventPayload.GetEventType(payload.StatusFlags) != BaseAirlockEventType.EnvironmentChanged)
                return;

            if (lifePodAirlockHashId != 0u && payload.AirlockHashId != lifePodAirlockHashId)
                return;

            if (requireWetLifePodExit && BaseAirlockEventPayload.IsDry(payload.StatusFlags))
                return;

            TryIssueExitLifePodDiscoveryFromAup();
        }

        private static bool IsSpeciesScan(in ScanEventPayload payload)
        {
            return (ScanEntryKind)payload.EntryKind == ScanEntryKind.Scannable && payload.EntryHash != 0u;
        }

        private static void ShowNewArchiveDataMilestone()
        {
            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                FixedCharBuffer buffer = new FixedCharBuffer(lease.Buffer);
                buffer.Append(s_newArchiveDataMessage);
                ToolHitUtility.ShowInfo(in buffer);
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
        }

        private bool TryIssueExitLifePodDiscoveryFromAup()
        {
            if (_exitLifePodIssued)
                return false;

            if (lifePodExitMinimumDistanceMeters > 0f)
            {
                if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                    return false;

                if (!TryResolveAupFromRuntimeOrigin(lifePodExitReferenceWorld, out AbsoluteUniversePosition podAup))
                    return false;

                double requiredDistanceSq = (double)lifePodExitMinimumDistanceMeters * lifePodExitMinimumDistanceMeters;
                if (AbsoluteUniversePosition.DistanceSq(in playerAup, in podAup) < requiredDistanceSq)
                    return false;
            }

            _exitLifePodIssued = true;
            NarrativeEvents.TryRaiseDiscoveryMade(_exitLifePodDiscoveryHash);
            return true;
        }

        private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerMovement != null)
            {
                playerAup = playerContext.PlayerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private static Vector3 ResolvePlayerRuntimePosition()
        {
            if (TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                var runtime = playerAup.ToRuntimeFloat3();
                return new Vector3(runtime.x, runtime.y, runtime.z);
            }

            return Vector3.zero;
        }

        private void TryPublishAtlasMarker(HectonBiomeMatrixProfile profile)
        {
            if (_atlasMarkerPublished || profile.depthTier < 2)
                return;

            if (!HasAtlasLorePrerequisite())
                return;

            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
            AtlasSignalSystem atlasSignal = GlobalRegistry.AtlasSignal;
            if (markerRegistry == null || atlasSignal == null)
                return;

            AbsoluteUniversePosition atlasCoreAup = atlasSignal.AtlasCoreAup;
            if (markerRegistry.TryCreateOrUpdateMarker(
                    _atlasMarkerHash,
                    in atlasCoreAup,
                    MarkerIconType.Objective,
                    _atlasMarkerTitleHash,
                    AtlasMarkerTitle,
                    out _))
            {
                _atlasMarkerPublished = true;
            }
        }

        private static bool HasAtlasLorePrerequisite()
        {
            INarrativeDiscoveryReadModel narrativeDiscovery = GlobalRegistry.NarrativeDiscoveryReadModel;
            if (narrativeDiscovery != null && narrativeDiscovery.HasDiscovery(_atlasSignalDiscoveryHash))
                return true;

            IQuestSystem questSystem = GlobalRegistry.QuestSystem;
            return questSystem != null &&
                   (questSystem.IsActive(_atlasSignalQuestHash) || questSystem.IsCompleted(_atlasSignalQuestHash));
        }

        private void TryPublishBiomeMarkers(int biomeId)
        {
            if (biomeMarkerRules == null || biomeMarkerRules.Length == 0)
                return;

            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
            if (markerRegistry == null)
                return;

            if (!TryResolveBiomeRuleBucket(biomeId, out int bucketStart, out int bucketCount))
                return;

            for (int bucketOffset = 0; bucketOffset < bucketCount; bucketOffset++)
            {
                int i = _biomeRuleIndices[bucketStart + bucketOffset];
                uint ruleMask = 1u << i;
                if ((_revealedBiomeMarkerMask & ruleMask) != 0u)
                    continue;

                BiomeMarkerRule rule = biomeMarkerRules[i];
                if (rule == null || rule.markerHashId == 0u)
                    continue;

                if (!HasMarkerPrerequisite(rule))
                    continue;

                if (!markerRegistry.TryCreateOrUpdateMarker(
                        rule.markerHashId,
                        rule.markerWorldPosition,
                        rule.iconType,
                        rule.titleHashId,
                        rule.title,
                        rule.visibleOnHud,
                        out _))
                {
                    continue;
                }

                _revealedBiomeMarkerMask |= ruleMask;
            }
        }

        private static bool HasMarkerPrerequisite(BiomeMarkerRule rule)
        {
            if (rule.requiredDiscoveryHash != 0u)
            {
                INarrativeDiscoveryReadModel narrativeDiscovery = GlobalRegistry.NarrativeDiscoveryReadModel;
                if (narrativeDiscovery == null || !narrativeDiscovery.HasDiscovery(rule.requiredDiscoveryHash))
                    return false;
            }

            if (rule.requiredQuestHash != 0u)
            {
                IQuestSystem questSystem = GlobalRegistry.QuestSystem;
                if (questSystem == null ||
                    (!questSystem.IsActive(rule.requiredQuestHash) && !questSystem.IsCompleted(rule.requiredQuestHash)))
                {
                    return false;
                }
            }

            return true;
        }

        private void CacheRuleHashes()
        {
            if (biomeMarkerRules == null || biomeMarkerRules.Length == 0)
            {
                BuildBiomeRuleBuckets();
                _biomeRuleHashesCached = true;
                return;
            }

            for (int i = 0; i < biomeMarkerRules.Length; i++)
            {
                BiomeMarkerRule rule = biomeMarkerRules[i];
                if (rule == null)
                    continue;

                rule.requiredQuestHash = QuestFlagHashKernel.ComputeStableHash(rule.requiredQuestId);
                rule.requiredDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(rule.requiredDiscoveryId);
                rule.markerHashId = QuestFlagHashKernel.ComputeStableHash(rule.markerId);
                rule.titleHashId = QuestFlagHashKernel.ComputeStableHash(rule.title);
            }

            BuildBiomeRuleBuckets();
            _biomeRuleHashesCached = true;
        }

        private void EnsureRuleHashesCached()
        {
            if (_biomeRuleHashesCached)
                return;

            CacheRuleHashes();
        }

        private bool TryResolveBiomeRuleBucket(int biomeId, out int bucketStart, out int bucketCount)
        {
            for (int i = 0; i < _biomeRuleBucketCount; i++)
            {
                if (_biomeRuleBucketBiomeIds[i] != biomeId)
                    continue;

                bucketStart = _biomeRuleBucketStarts[i];
                bucketCount = _biomeRuleBucketCounts[i];
                return bucketCount > 0;
            }

            bucketStart = 0;
            bucketCount = 0;
            return false;
        }

        private void BuildBiomeRuleBuckets()
        {
            _biomeRuleBucketCount = 0;
            for (int i = 0; i < MaxBiomeMarkerRules; i++)
            {
                _biomeRuleBucketBiomeIds[i] = 0;
                _biomeRuleBucketStarts[i] = 0;
                _biomeRuleBucketCounts[i] = 0;
                _biomeRuleBucketWriteOffsets[i] = 0;
                _biomeRuleIndices[i] = 0;
            }

            if (biomeMarkerRules == null || biomeMarkerRules.Length == 0)
                return;

            int ruleCount = math.min(biomeMarkerRules.Length, MaxBiomeMarkerRules);
            for (int i = 0; i < ruleCount; i++)
            {
                BiomeMarkerRule rule = biomeMarkerRules[i];
                if (rule == null || rule.markerHashId == 0u)
                    continue;

                int bucketIndex = ResolveOrCreateBiomeBucket(rule.biomeId);
                if (bucketIndex < 0)
                    continue;

                _biomeRuleBucketCounts[bucketIndex]++;
            }

            int cursor = 0;
            for (int i = 0; i < _biomeRuleBucketCount; i++)
            {
                _biomeRuleBucketStarts[i] = cursor;
                cursor += _biomeRuleBucketCounts[i];
                _biomeRuleBucketWriteOffsets[i] = 0;
            }

            for (int i = 0; i < ruleCount; i++)
            {
                BiomeMarkerRule rule = biomeMarkerRules[i];
                if (rule == null || rule.markerHashId == 0u)
                    continue;

                int bucketIndex = ResolveBiomeBucket(rule.biomeId);
                if (bucketIndex < 0)
                    continue;

                int writeIndex = _biomeRuleBucketStarts[bucketIndex] + _biomeRuleBucketWriteOffsets[bucketIndex];
                if ((uint)writeIndex >= (uint)_biomeRuleIndices.Length)
                    continue;

                _biomeRuleIndices[writeIndex] = i;
                _biomeRuleBucketWriteOffsets[bucketIndex]++;
            }
        }

        private int ResolveOrCreateBiomeBucket(int biomeId)
        {
            int bucketIndex = ResolveBiomeBucket(biomeId);
            if (bucketIndex >= 0)
                return bucketIndex;

            if (_biomeRuleBucketCount >= MaxBiomeMarkerRules)
                return -1;

            bucketIndex = _biomeRuleBucketCount;
            _biomeRuleBucketBiomeIds[bucketIndex] = biomeId;
            _biomeRuleBucketCount++;
            return bucketIndex;
        }

        private int ResolveBiomeBucket(int biomeId)
        {
            for (int i = 0; i < _biomeRuleBucketCount; i++)
            {
                if (_biomeRuleBucketBiomeIds[i] == biomeId)
                    return i;
            }

            return -1;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _biomeRuleHashesCached = false;
            CacheRuleHashes();
        }
#endif
    }
}
