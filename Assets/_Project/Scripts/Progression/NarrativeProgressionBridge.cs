using Hecton8.AtlasSignal;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.PDA;
using Hecton8.Quest;
using Hecton8.UI;
using Hecton8.World;
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
        private static readonly uint _atlasSignalDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(AtlasSignalDiscoveryId);
        private static readonly uint _atlasSignalQuestHash = QuestFlagHashKernel.ComputeStableHash(AtlasSignalQuestId);
        private static readonly uint _atlasMarkerHash = QuestFlagHashKernel.ComputeStableHash(AtlasMarkerId);

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

        private void Awake()
        {
            CacheRuleHashes();
        }

        private void OnEnable()
        {
            CacheRuleHashes();
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
            NarrativeEvents.RaiseDiscoveryMade(HullFailureDiscoveryId);
            ProceduralAudioEvents.RaiseStructuralStressTriggered(transform.position, 1f, 0.72f);

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            if (audioLogs != null)
            {
                audioLogs.NotifyAtmosphericWarningStarted(0.7f);
                audioLogs.TryPlayLogById(HullFailureLogId);
            }
        }

        public void OnBaseAirlockEvent(in BaseAirlockEventPayload payload)
        {
            if (_exitLifePodIssued)
                return;

            if ((BaseAirlockEventType)payload.EventType != BaseAirlockEventType.EnvironmentChanged)
                return;

            if (lifePodAirlockHashId != 0u && payload.AirlockHashId != lifePodAirlockHashId)
                return;

            if (requireWetLifePodExit && payload.Dry)
                return;

            TryIssueExitLifePodDiscoveryFromAup();
        }

        private static bool IsSpeciesScan(in ScanEventPayload payload)
        {
            if ((ScanEntryKind)payload.EntryKind != ScanEntryKind.Scannable)
                return false;

            if (!ScanEvents.TryResolveEntryMetadata(payload.EntryHash, out ScanEntryMetadata metadata))
                return false;

            string entryId = metadata.EntryId;
            return !string.IsNullOrEmpty(entryId) &&
                   (entryId.StartsWith("creature.", System.StringComparison.Ordinal) ||
                    entryId.StartsWith("fauna.", System.StringComparison.Ordinal) ||
                    entryId.StartsWith("species.", System.StringComparison.Ordinal));
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
                AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
                AbsoluteUniversePosition podAup = AbsoluteUniversePosition.FromRuntimePosition(lifePodExitReferenceWorld);
                double requiredDistanceSq = (double)lifePodExitMinimumDistanceMeters * lifePodExitMinimumDistanceMeters;
                if (AbsoluteUniversePosition.DistanceSq(in playerAup, in podAup) < requiredDistanceSq)
                    return false;
            }

            _exitLifePodIssued = true;
            NarrativeEvents.RaiseDiscoveryMade(ExitLifePodDiscoveryId);
            return true;
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

            if (markerRegistry.TryCreateOrUpdateMarker(
                    _atlasMarkerHash,
                    AtlasMarkerId,
                    atlasSignal.AtlasCorePosition,
                    MarkerIconType.Objective,
                    AtlasMarkerTitle,
                    out _))
            {
                _atlasMarkerPublished = true;
            }
        }

        private static bool HasAtlasLorePrerequisite()
        {
            HectonNarrativeDirector narrativeDirector = GlobalRegistry.NarrativeDirector;
            if (narrativeDirector != null && narrativeDirector.HasDiscovery(_atlasSignalDiscoveryHash))
                return true;

            QuestManager questManager = GlobalRegistry.Quest;
            return questManager != null &&
                   (questManager.IsActive(_atlasSignalQuestHash) || questManager.IsCompleted(_atlasSignalQuestHash));
        }

        private void TryPublishBiomeMarkers(int biomeId)
        {
            if (biomeMarkerRules == null || biomeMarkerRules.Length == 0)
                return;

            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
            if (markerRegistry == null)
                return;

            int ruleCount = Mathf.Min(biomeMarkerRules.Length, 32);
            for (int i = 0; i < ruleCount; i++)
            {
                uint ruleMask = 1u << i;
                if ((_revealedBiomeMarkerMask & ruleMask) != 0u)
                    continue;

                BiomeMarkerRule rule = biomeMarkerRules[i];
                if (rule == null || rule.biomeId != biomeId || rule.markerHashId == 0u)
                    continue;

                if (!HasMarkerPrerequisite(rule))
                    continue;

                if (!markerRegistry.TryCreateOrUpdateMarker(
                        rule.markerHashId,
                        rule.markerId,
                        rule.markerWorldPosition,
                        rule.iconType,
                        rule.title,
                        out _))
                {
                    continue;
                }

                if (!rule.visibleOnHud)
                    markerRegistry.SetMarkerHudVisibility(rule.markerHashId, false);

                _revealedBiomeMarkerMask |= ruleMask;
            }
        }

        private static bool HasMarkerPrerequisite(BiomeMarkerRule rule)
        {
            if (rule.requiredDiscoveryHash != 0u)
            {
                HectonNarrativeDirector narrativeDirector = GlobalRegistry.NarrativeDirector;
                if (narrativeDirector == null || !narrativeDirector.HasDiscovery(rule.requiredDiscoveryHash))
                    return false;
            }

            if (rule.requiredQuestHash != 0u)
            {
                QuestManager questManager = GlobalRegistry.Quest;
                if (questManager == null ||
                    (!questManager.IsActive(rule.requiredQuestHash) && !questManager.IsCompleted(rule.requiredQuestHash)))
                {
                    return false;
                }
            }

            return true;
        }

        private void CacheRuleHashes()
        {
            if (biomeMarkerRules == null || biomeMarkerRules.Length == 0)
                return;

            for (int i = 0; i < biomeMarkerRules.Length; i++)
            {
                BiomeMarkerRule rule = biomeMarkerRules[i];
                if (rule == null)
                    continue;

                rule.requiredQuestHash = QuestFlagHashKernel.ComputeStableHash(rule.requiredQuestId);
                rule.requiredDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(rule.requiredDiscoveryId);
                rule.markerHashId = QuestFlagHashKernel.ComputeStableHash(rule.markerId);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheRuleHashes();
        }
#endif
    }
}
