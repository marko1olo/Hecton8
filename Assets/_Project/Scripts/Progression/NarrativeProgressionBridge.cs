using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.PDA;
using Hecton8.Quest;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Progression
{
    /// <summary>
    /// Player-owned narrative bridge for event-only progression reactions.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Progression/Narrative Progression Bridge")]
    public sealed class NarrativeProgressionBridge : MonoBehaviour, IBiomeMatrixEventListener, IScanEventListener, IBaseIntegrityEventListener
    {
        private const string ExitLifePodDiscoveryId = "first_hour_exit_lifepod";
        private const string AtlasSignalDiscoveryId = "atlas6_signal_identified";
        private const string AtlasSignalQuestId = "quest_atlas_signal_detected";
        private const string AtlasMarkerId = "narrative_atlas_signal_source";
        private const string AtlasMarkerTitle = "ENCRYPTED SIGNAL SOURCE";
        private const string HullFailureDiscoveryId = "hull_failure_voice_log";
        private const string HullFailureLogId = "captain_last_broadcast";

        private static readonly char[] s_newArchiveDataMessage =
        {
            'N','E','W',' ','D','A','T','A',' ','A','D','D','E','D',' ','T','O',' ','A','R','C','H','I','V','E'
        };

        private bool _exitLifePodIssued;
        private bool _atlasMarkerPublished;
        private bool _hullFailureIssued;
        private int _lastBiomeMatrixId = int.MinValue;

        private void OnEnable()
        {
            BiomeMatrixEvents.Register(this);
            ScanEvents.Register(this);
            BaseIntegrityEvents.Register(this);
        }

        private void OnDisable()
        {
            BaseIntegrityEvents.Unregister(this);
            ScanEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            BaseIntegrityEvents.Unregister(this);
            ScanEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
        }

        public void OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return;

            if (!_exitLifePodIssued)
            {
                _exitLifePodIssued = true;
                NarrativeEvents.RaiseDiscoveryMade(ExitLifePodDiscoveryId);
            }

            int biomeId = profile.matrixIndex;
            if (biomeId == _lastBiomeMatrixId)
                return;

            _lastBiomeMatrixId = biomeId;
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

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            if (audioLogs != null)
                audioLogs.TryPlayLogById(HullFailureLogId);
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
            if (narrativeDirector != null && narrativeDirector.HasDiscovery(AtlasSignalDiscoveryId))
                return true;

            QuestManager questManager = GlobalRegistry.Quest;
            return questManager != null &&
                   (questManager.IsActive(AtlasSignalQuestId) || questManager.IsCompleted(AtlasSignalQuestId));
        }
    }
}
