#!/usr/bin/env python3
from __future__ import annotations

import contextlib
import io
import json
import sys
import unittest
from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

import AppliedLoreSignalRouteAudit as audit  # noqa: E402
from AppliedLoreSignalRouteAudit import main, validate_signal_route  # noqa: E402


SYNTHETIC_ROOT = Path("__synthetic__")
_active_source_map: dict[Path, str] | None = None


def write_text(path: Path, text: str) -> None:
    if _active_source_map is not None:
        _active_source_map[path.relative_to(SYNTHETIC_ROOT)] = text
        return

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


@contextlib.contextmanager
def synthetic_signal_route_sources(**kwargs: object) -> Path:
    global _active_source_map

    sources: dict[Path, str] = {}
    previous_source_map = _active_source_map
    _active_source_map = sources
    try:
        write_signal_route_sources(SYNTHETIC_ROOT, **kwargs)
    finally:
        _active_source_map = previous_source_map

    original_read_text = audit.read_text

    def read_synthetic_text(root: Path, relative_path: Path, issues: list[str]) -> str:
        text = sources.get(relative_path)
        if text is None:
            issues.append(f"missing synthetic source file: {relative_path.as_posix()}")
            return ""
        return text

    audit.read_text = read_synthetic_text
    try:
        yield SYNTHETIC_ROOT
    finally:
        audit.read_text = original_read_text


def write_signal_route_sources(
    root: Path,
    *,
    include_preview_drop_visibility: bool = True,
    append_event_before_state: bool = False,
    load_clear_after_replay: bool = False,
    include_marker_revision_fallback: bool = True,
    map_clears_stale_marker_slots: bool = True,
    hud_revision_before_copy: bool = True,
) -> None:
    write_text(
        root / "Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs",
        """
public static class H8AppliedLoreRuntime
{
    private static int s_appliedLoreSignalPushDropCount;
    public static bool TryRaisePacketUnlocked(uint packetHash) { return TryRaisePacketUnlockedCore(packetHash, default, 0u, 0); }
    public static bool TryRaisePacketUnlockedAt(uint packetHash)
    {
        bool hasFiniteAup = AbsoluteUniversePosition.IsFinite(in positionAup);
        byte loreFlags = hasFiniteAup ? (byte)(flags | LoreFragmentScannedSignal.FlagHasAup | LoreFragmentScannedSignal.FlagPairedScanComplete) : flags;
        bool raised = TryRaisePacketUnlockedCore(packetHash, in positionAup, resolvedSourceId, loreFlags);
        ScanCompleteSignal scanSignal = new ScanCompleteSignal { EntryHash = packetHash, ScanId = packetHash };
        return SignalBus<ScanCompleteSignal>.TryPushTracked(in scanSignal, ref s_appliedLoreSignalPushDropCount) || raised;
    }
    private static bool TryRaisePacketUnlockedCore(uint packetHash, in AbsoluteUniversePosition positionAup, uint sourceId, byte flags)
    {
        if (packetHash == 0u) return false;
        LoreFragmentScannedSignal signal = new LoreFragmentScannedSignal
        {
            Hash = packetHash,
            Frame = SystemDispatcher.CurrentFrameId,
            SourceId = sourceId != 0u ? sourceId : UnlockSourceId
        };
        return SignalBus<LoreFragmentScannedSignal>.TryPushTracked(in signal, ref s_appliedLoreSignalPushDropCount);
    }
}
""",
    )
    write_text(
        root / "Assets/_Project/Scripts/Gameplay/MessageTerminal.cs",
        """
public sealed class MessageTerminal
{
    private static int s_x001MessageTerminalSignalPushDropCount;
    private const uint AppliedLoreTerminalSourceHash = 1u;
    private uint ResolveActiveAppliedLoreLocaleHash() { return 1u; }
    private void PublishAppliedLorePacketUnlock()
    {
        if (appliedLorePacketHash == 0u) return;
        AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
        H8AppliedLoreRuntime.TryRaisePacketUnlockedAt(appliedLorePacketHash, in aup, AppliedLoreTerminalSourceHash);
        PublishAppliedLoreTerminalPreview();
    }
    private void PublishAppliedLoreTerminalPreview()
    {
        if (appliedLorePacketHash == 0u) return;
        AppliedLoreTerminalPreviewSignal signal = new AppliedLoreTerminalPreviewSignal
        {
            PacketHash = appliedLorePacketHash,
            LocaleHash = ResolveActiveAppliedLoreLocaleHash(),
            TerminalHash = terminalOsPreviewHash,
            Frame = SystemDispatcher.CurrentFrameId,
            TerminalIndex = terminalOsPreviewIndex,
            SourceHash = AppliedLoreTerminalSourceHash,
            Surface = (byte)terminalOsPreviewSurface,
            Flags = AppliedLoreTerminalPreviewSignal.FlagHasTerminalHash
        };
        SignalBus<AppliedLoreTerminalPreviewSignal>.TryPushTracked(in signal, ref s_x001MessageTerminalSignalPushDropCount);
    }
}
""",
    )
    drop_line = "SignalBus<AppliedLoreTerminalPreviewSignal>.DroppedLastFlush" if include_preview_drop_visibility else "0"
    write_text(
        root / "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs",
        f"""
public sealed class TerminalOsRuntime
{{
    private static void ConfigureSignalLanes()
    {{
        SignalBus<AppliedLoreTerminalPreviewSignal>.Configure(
            AppliedLoreTerminalPreviewSignal.ExpectedCapacity,
            AppliedLoreTerminalPreviewSignal.MaxFrameSignals,
            AppliedLoreTerminalPreviewSignal.LowTierFrameSignals,
            AppliedLoreTerminalPreviewSignal.LaneHash);
        SignalBus<AppliedLoreTerminalPreviewSignal>.EnsureInitialized();
    }}
    private uint ConsumeAppliedLoreTerminalPreviewSignals()
    {{
        uint faultFlags = {drop_line} > 0 ? FaultAppliedLorePreviewDrop : 0u;
        ReadOnlySpan<AppliedLoreTerminalPreviewSignal> signals = SignalBus<AppliedLoreTerminalPreviewSignal>.GetFrameSnapshot();
        AppliedLoreTerminalPreviewSignal signal = signals[0];
        if (signal.PacketHash == 0u) faultFlags |= FaultAppliedLorePreviewMiss;
        ApplyTerminalAppliedLoreLine(0, 0, 0, 0, ResolveAppliedLorePreviewSurface(0));
        return faultFlags;
    }}
    private static bool TryGetTerminalPreviewAppliedLoreUtf8(uint packetHash, uint localeHash, H8AppliedLoreSurface surface, out ReadOnlySpan<byte> utf8Bytes)
    {{
        uint resolvedLocale = H8AppliedLoreRuntime.DefaultLocaleHash;
        if (H8AppliedLoreRuntime.TryGetUtf8(packetHash, resolvedLocale, surface, out utf8Bytes) && IsTerminalPreviewAsciiCompatible(utf8Bytes)) return true;
        if (H8AppliedLoreRuntime.TryGetUtf8(packetHash, H8AppliedLoreRuntime.DefaultLocaleHash, surface, out ReadOnlySpan<byte> fallbackUtf8) && fallbackUtf8.Length > 0) return true;
        return false;
    }}
}}
""",
    )
    write_text(
        root / "Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs",
        """
public sealed class PDAEncyclopediaStreamer
{
    private void ConsumeScanSignals()
    {
        if (SignalBus<ScanCompleteSignal>.DroppedLastFlush > 0 || SignalBus<LoreFragmentScannedSignal>.DroppedLastFlush > 0) MarkTransientFault(1);
        ReadOnlySpan<ScanCompleteSignal> scanSignals = SignalBus<ScanCompleteSignal>.GetFrameSnapshot();
        ReadOnlySpan<LoreFragmentScannedSignal> loreSignals = SignalBus<LoreFragmentScannedSignal>.GetFrameSnapshot();
        if (!TryResolveLorePayloadForUnlock(scanSignals[0].EntryHash)) RejectLoreHash(scanSignals[0].EntryHash);
        UnlockEntry(scanSignals[0].EntryHash, default, 0u, 0u, false);
        PublishLoreUnlockHaptic();
        if ((loreSignals[0].Flags & LoreFragmentScannedSignal.FlagPairedScanComplete) != 0 && HasPairedScanComplete(scanSignals, in loreSignals[0])) return;
        TryCaptureSignalAup(in loreSignals[0], out PdaAup48 aup);
    }
    private void ReplayPersistedPdaLogEvents()
    {
        if (!UIStateStore.IsInitialized) return;
        if (!UIStateStore.TryGetPDALogEventHash(0, out uint eventHash)) return;
        if (!TryResolveLorePayloadForUnlock(eventHash)) RejectLoreHash(eventHash);
        UnlockEntry(eventHash, default, PdaLogReplaySourceId, 0u, false);
        TryQueuePdaLogEventSelection(eventHash);
    }
    private void ResetPdaLogEventReplay()
    {
        _pdaLogEventReplayCursor = 0;
        _pdaLogReplayObservedVersion = 0u;
        _pdaLogReplayObservedCount = 0u;
        _pdaLogReplayObservedLatestHash = 0u;
        _pdaLogEventReplayComplete = false;
    }
    private bool TryResolveLorePayloadForUnlock(uint hash)
    {
        TryGetAppliedLoreUtf8(hash, H8AppliedLoreSurface.InGameWiki, out _);
        TryGetH8lrUtf8(hash, out _);
        _babelStore.FetchUtf8(hash);
        return TryGetMockUtf8(hash, out _);
    }
}
""",
    )
    append_order = (
        """
        PDAEvents.TryRaiseLogbookChanged(1, unchecked((uint)originHash));
        UIStateStore.AppendPDALogEventHash(unchecked((uint)originHash), playTimeSeconds);
"""
        if append_event_before_state
        else
        """
        UIStateStore.AppendPDALogEventHash(unchecked((uint)originHash), playTimeSeconds);
        PDAEvents.TryRaiseLogbookChanged(1, unchecked((uint)originHash));
"""
    )
    load_order = (
        """
        AppendLoadedEntry(entry);
        UIStateStore.AppendPDALogEventHash(unchecked((uint)originHash), playTimeSeconds);
        UIStateStore.ClearPDALogEventHistory();
        PDAEvents.TryRaiseLogbookChanged(1, unchecked((uint)originHash));
"""
        if load_clear_after_replay
        else
        """
        UIStateStore.ClearPDALogEventHistory();
        AppendLoadedEntry(entry);
        UIStateStore.AppendPDALogEventHash(unchecked((uint)originHash), playTimeSeconds);
        PDAEvents.TryRaiseLogbookChanged(1, unchecked((uint)originHash));
"""
    )
    write_text(
        root / "Assets/_Project/Scripts/PDA/PDALogbookManager.cs",
        """
public sealed class PDALogbookManager : ISaveable
{
    public int SavePriority => 205;
    public int LoadPriority => 205;
    private void TryRegisterWithSaveManager() {}
    private void UnregisterFromSaveManager() {}
    private bool TryAppendEntry(int originHash, float playTimeSeconds)
    {
__APPEND_ORDER__
        RefreshLogbookSignalPumpRegistration();
        return true;
    }
    public void PopulateSaveData(SaveData data)
    {
        data.pdaLogbook.entryCount = entryCount;
        data.pdaLogbook.nextSequence = nextSequence;
        data.pdaLogbook.entries[0].playTimeSeconds = playTimeSeconds;
        data.pdaLogbook.entries[0].originHash = originHash;
        data.pdaLogbook.seenOriginHashes[0] = originHash;
    }
    public void LoadFromSaveData(SaveData data)
    {
__LOAD_ORDER__
        RebindOwnerSubscriptions();
        RefreshLogbookSignalPumpRegistration();
    }
    public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
    {
        if (serviceSlot == GlobalRegistryServiceSlot.Save)
        {
            UnregisterFromSaveManager();
            _saveService = currentService as ISaveService;
            TryRegisterWithSaveManager();
        }
    }
}
""".replace("__APPEND_ORDER__", append_order).replace("__LOAD_ORDER__", load_order),
    )
    write_text(
        root / "Assets/_Project/Scripts/UI/PDADataLogTab.cs",
        """
public sealed class PDADataLogTab
{
    public void OnPDAEvent(in PDAEventPayload payload)
    {
        if ((PDAEventType)payload.EventType == PDAEventType.LogbookChanged)
            HandleEventSourcedLogbookChanged(payload.LogEventHashID);
    }
    private void HandleEventSourcedLogbookChanged(uint eventHash)
    {
        if (eventHash != 0u) _latestSimulationLogHash = eventHash;
        if (UIStateStore.TryGetPDALogEvent(0, out uint latestHash, out float timestampSeconds))
        {
            _latestSimulationLogHash = latestHash;
            _latestSimulationLogTimestamp = timestampSeconds;
        }
        _dirty = true;
    }
    private void RefreshEventSourcedLogStateFromUIStore()
    {
        if (!UIStateStore.IsInitialized) return;
        UIStateData pdaState = UIStateStore.GetPDAState();
        if (pdaState.LogEntryCount == 0u || pdaState.LatestLogEventHash == 0u) return;
        if (UIStateStore.TryGetPDALogEvent(0, out uint latestHash, out float timestampSeconds))
        {
            _latestSimulationLogHash = latestHash;
            _latestSimulationLogTimestamp = timestampSeconds;
        }
        else
        {
            _latestSimulationLogHash = pdaState.LatestLogEventHash;
        }
        _dirty = true;
        _visualLateFrameDirty = true;
    }
}
""",
    )
    if include_marker_revision_fallback:
        marker_registry_source = """
public sealed class PDAMarkerRegistry : ISaveable
{
    private uint _revision;
    private int _markerCount;
    public uint Revision => _revision;
    public void TryRegisterWithSaveManager() { _saveService = GlobalRegistryServiceSlot.Save; }
    public void TryRegisterService()
    {
        PDAMarkerRegistry registeredRuntime = Hecton8.Core.GlobalRegistry.PDAMarkers;
        Hecton8.Core.GlobalRegistry.RegisterPDAMarkerRuntime(this);
        Hecton8.Core.GlobalRegistry.UnregisterPDAMarkerRuntime(this);
    }
    public bool TryCreateMarker()
    {
        MarkerRecord record = default;
        CommitMarkerRevision(record.markerHashId);
        return true;
    }
    private bool TryCreateOrUpdateMarker()
    {
        MarkerRecord existing = default;
        CommitMarkerRevision(existing.markerHashId);
        MarkerRecord record = default;
        CommitMarkerRevision(record.markerHashId);
        return true;
    }
    public bool RemoveMarker()
    {
        MarkerRecord removedRecord = default;
        CommitMarkerRevision(removedRecord.markerHashId);
        return true;
    }
    public bool UpdateMarkerPosition()
    {
        MarkerRecord record = default;
        CommitMarkerRevision(record.markerHashId);
        return true;
    }
    public bool SetMarkerHudVisibility()
    {
        MarkerRecord record = default;
        CommitMarkerRevision(record.markerHashId);
        return true;
    }
    public void LoadFromSaveData()
    {
        CommitMarkerRevision(0u);
    }
    public void OnOriginShift()
    {
        CommitMarkerRevision(0u);
    }
    private void CommitMarkerRevision(uint markerHashId)
    {
        unchecked
        {
            _revision++;
            if (_revision == 0u)
                _revision = 1u;
        }
        if (Application.isPlaying)
            Hecton8.UI.PDAEvents.TryRaiseMarkerChanged(markerHashId, _markerCount);
    }
}
"""
    else:
        marker_registry_source = """
public sealed class PDAMarkerRegistry : ISaveable
{
    private int _markerCount;
    public bool TryCreateMarker()
    {
        Hecton8.UI.PDAEvents.TryRaiseMarkerChanged(1u, _markerCount);
        return true;
    }
}
"""
    write_text(root / "Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs", marker_registry_source)

    stale_slot_clear_call = (
        "        ClearMarkerVisualSlotsNotInSnapshot(markerCount);\n"
        if map_clears_stale_marker_slots
        else ""
    )
    stale_slot_helpers = (
        """
    private void ClearMarkerVisualSlotsNotInSnapshot(int markerCount)
    {
        if (!SnapshotContainsMarker(1u, markerCount))
            ClearMarkerVisual(0);
    }
    private bool SnapshotContainsMarker(uint markerHashId, int markerCount)
    {
        return markerHashId != 0u && markerCount > 0;
    }
"""
        if map_clears_stale_marker_slots
        else ""
    )
    write_text(
        root / "Assets/_Project/Scripts/UI/PDAMapTab.cs",
        f"""
public sealed class PDAMapTab
{{
    private uint _observedMarkerRevision = uint.MaxValue;
    private object _markerUpdateSnapshots;
    public void LateFrameTick()
    {{
        RefreshMarkerRevisionFallback(force: false);
        ProcessPendingMarkerUpdates(4);
    }}
    public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
    {{
        if (serviceSlot == GlobalRegistryServiceSlot.PDAMarkerRuntime)
        {{
            _observedMarkerRevision = uint.MaxValue;
            RefreshMarkerRevisionFallback(force: true);
        }}
    }}
    private void EnqueueAllMarkerUpdates()
    {{
        PDAMarkerRegistry markerRegistry = ResolveMarkerRegistry();
        int markerCount = markerRegistry.CopyMarkers(_markerUpdateSnapshots, hudOnly: false);
        ClearPendingMarkerUpdates();
{stale_slot_clear_call}        EnqueueMarkerUpdate(1u);
    }}
    private void RefreshMarkerRevisionFallback(bool force)
    {{
        PDAMarkerRegistry markerRegistry = ResolveMarkerRegistry();
        uint revision = markerRegistry != null ? markerRegistry.Revision : 0u;
        if (!force && _observedMarkerRevision == revision)
            return;
        _observedMarkerRevision = revision;
        EnqueueAllMarkerUpdates();
    }}
{stale_slot_helpers}
}}
""",
    )

    sample_order = (
        """
        RefreshMarkerRevisionFallback(force: false);
        if (_markersDirty)
            _markerCount = markerRegistry.CopyMarkers(_markerBuffer, hudOnly: true);
"""
        if hud_revision_before_copy
        else
        """
        if (_markersDirty)
            _markerCount = markerRegistry.CopyMarkers(_markerBuffer, hudOnly: true);
        RefreshMarkerRevisionFallback(force: false);
"""
    )
    write_text(
        root / "Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs",
        f"""
public sealed class PDAMarkerHUDElement
{{
    private uint _observedMarkerRevision = uint.MaxValue;
    private bool _markersDirty;
    private int _markerCount;
    private object _markerBuffer;
    private PDAMarkerRegistry _cachedMarkerRegistry;
    private void OnEnable()
    {{
        _observedMarkerRevision = uint.MaxValue;
        RefreshMarkerRevisionFallback(force: true);
        _markersDirty = true;
    }}
    private void SampleMarkerDisplay(float deltaTime)
    {{
        PDAMarkerRegistry markerRegistry = _cachedMarkerRegistry;
        if (markerRegistry == null)
        {{
            _observedMarkerRevision = 0u;
            HideAllDisplays();
            return;
        }}
{sample_order}    }}
    public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
    {{
        if (serviceSlot == GlobalRegistryServiceSlot.PDAMarkerRuntime)
        {{
            _cachedMarkerRegistry = currentService as PDAMarkerRegistry;
            _observedMarkerRevision = uint.MaxValue;
            _markersDirty = true;
        }}
    }}
    private void RefreshMarkerRevisionFallback(bool force)
    {{
        PDAMarkerRegistry markerRegistry = _cachedMarkerRegistry;
        uint revision = markerRegistry != null ? markerRegistry.Revision : 0u;
        if (!force && _observedMarkerRevision == revision)
            return;
        _observedMarkerRevision = revision;
        _markersDirty = true;
    }}
}}
""",
    )


class AppliedLoreSignalRouteAuditTests(unittest.TestCase):
    def test_clean_signal_route_sources_pass(self) -> None:
        with synthetic_signal_route_sources() as root:
            stats = validate_signal_route(root)

        self.assertTrue(stats.clean)
        self.assertEqual(stats.checked_files, 9)
        self.assertEqual(stats.checked_methods, 31)

    def test_missing_terminal_preview_drop_visibility_fails(self) -> None:
        with synthetic_signal_route_sources(include_preview_drop_visibility=False) as root:
            stats = validate_signal_route(root)

        self.assertFalse(stats.clean)
        self.assertTrue(any("DroppedLastFlush" in issue for issue in stats.issues))

    def test_logbook_append_event_before_state_fails(self) -> None:
        with synthetic_signal_route_sources(append_event_before_state=True) as root:
            stats = validate_signal_route(root)

        self.assertFalse(stats.clean)
        self.assertTrue(any("TryAppendEntry" in issue and "out of order" in issue for issue in stats.issues))

    def test_logbook_load_clear_after_replay_fails(self) -> None:
        with synthetic_signal_route_sources(load_clear_after_replay=True) as root:
            stats = validate_signal_route(root)

        self.assertFalse(stats.clean)
        self.assertTrue(any("LoadFromSaveData" in issue and "out of order" in issue for issue in stats.issues))

    def test_marker_registry_without_revision_owner_fails(self) -> None:
        with synthetic_signal_route_sources(include_marker_revision_fallback=False) as root:
            stats = validate_signal_route(root)

        self.assertFalse(stats.clean)
        self.assertTrue(any("PDAMarkerRegistry" in issue and "Revision" in issue for issue in stats.issues))

    def test_map_full_marker_snapshot_without_stale_slot_clear_fails(self) -> None:
        with synthetic_signal_route_sources(map_clears_stale_marker_slots=False) as root:
            stats = validate_signal_route(root)

        self.assertFalse(stats.clean)
        self.assertTrue(any("EnqueueAllMarkerUpdates" in issue and "ClearMarkerVisualSlotsNotInSnapshot" in issue for issue in stats.issues))

    def test_hud_marker_copy_before_revision_fallback_fails(self) -> None:
        with synthetic_signal_route_sources(hud_revision_before_copy=False) as root:
            stats = validate_signal_route(root)

        self.assertFalse(stats.clean)
        self.assertTrue(any("SampleMarkerDisplay" in issue and "out of order" in issue for issue in stats.issues))

    def test_cli_json_preserves_failure_exit_code(self) -> None:
        with synthetic_signal_route_sources(include_preview_drop_visibility=False) as root:
            stdout = io.StringIO()
            stderr = io.StringIO()

            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", str(root), "--json"])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stderr.getvalue(), "")
        payload = json.loads(stdout.getvalue())
        self.assertFalse(payload["clean"])
        self.assertTrue(payload["issues"])


if __name__ == "__main__":
    unittest.main()
