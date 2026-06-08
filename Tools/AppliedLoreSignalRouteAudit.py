#!/usr/bin/env python3
"""Static proof for AppliedLore/PDA runtime signal producer/consumer routes."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from pathlib import Path


RUNTIME_PATH = Path("Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs")
MESSAGE_TERMINAL_PATH = Path("Assets/_Project/Scripts/Gameplay/MessageTerminal.cs")
TERMINAL_OS_PATH = Path("Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs")
PDA_STREAMER_PATH = Path("Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs")
PDA_LOGBOOK_PATH = Path("Assets/_Project/Scripts/PDA/PDALogbookManager.cs")
PDA_DATA_LOG_TAB_PATH = Path("Assets/_Project/Scripts/UI/PDADataLogTab.cs")
PDA_MARKER_REGISTRY_PATH = Path("Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs")
PDA_MAP_TAB_PATH = Path("Assets/_Project/Scripts/UI/PDAMapTab.cs")
PDA_MARKER_HUD_PATH = Path("Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs")


class AppliedLoreSignalRouteError(RuntimeError):
    """Raised when the AppliedLore runtime route is not wired end-to-end."""


@dataclass(frozen=True)
class SignalRouteStats:
    checked_files: int
    checked_methods: int
    issues: tuple[str, ...]

    @property
    def clean(self) -> bool:
        return not self.issues


def read_text(root: Path, relative_path: Path, issues: list[str]) -> str:
    path = root / relative_path
    if not path.exists():
        issues.append(f"missing source file: {relative_path.as_posix()}")
        return ""
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        issues.append(f"source file is not UTF-8: {relative_path.as_posix()}: {exc}")
        return ""


def extract_method_body(source: str, method_name: str) -> str | None:
    bodies = extract_method_bodies(source, method_name)
    return bodies[0] if bodies else None


def extract_method_bodies(source: str, method_name: str) -> tuple[str, ...]:
    bodies: list[str] = []
    search_index = 0
    while True:
        method_index = source.find(method_name, search_index)
        if method_index < 0:
            return tuple(bodies)

        line_start = source.rfind("\n", 0, method_index) + 1
        prefix = source[line_start:method_index].strip()
        search_index = method_index + len(method_name)
        if not any(modifier in prefix.split() for modifier in ("private", "public", "protected", "internal")):
            continue

        paren_index = source.find("(", method_index + len(method_name))
        if paren_index < 0:
            continue

        depth = 0
        close_paren_index = -1
        for index in range(paren_index, len(source)):
            char = source[index]
            if char == "(":
                depth += 1
            elif char == ")":
                depth -= 1
                if depth == 0:
                    close_paren_index = index
                    break
        if close_paren_index < 0:
            continue

        body_start = close_paren_index + 1
        while body_start < len(source) and source[body_start].isspace():
            body_start += 1

        if body_start < len(source) and source[body_start] == "{":
            open_index = body_start
        else:
            continue

        depth = 0
        for index in range(open_index, len(source)):
            char = source[index]
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    bodies.append(source[open_index + 1 : index])
                    search_index = index + 1
                    break


def require_symbols(
    owner: str,
    text: str,
    symbols: tuple[str, ...],
    issues: list[str],
) -> None:
    for symbol in symbols:
        if symbol not in text:
            issues.append(f"{owner}: missing {symbol!r}")


def require_symbol_order(
    owner: str,
    text: str,
    symbols: tuple[str, ...],
    issues: list[str],
) -> None:
    cursor = 0
    for symbol in symbols:
        index = text.find(symbol, cursor)
        if index < 0:
            issues.append(f"{owner}: {symbol!r} is missing or out of order")
            return
        cursor = index + len(symbol)


def require_method_symbols(
    relative_path: Path,
    source: str,
    method_name: str,
    symbols: tuple[str, ...],
    issues: list[str],
) -> bool:
    body = extract_method_body(source, method_name)
    owner = f"{relative_path.as_posix()}::{method_name}"
    if body is None:
        issues.append(f"{owner}: method body missing")
        return False
    require_symbols(owner, body, symbols, issues)
    return True


def require_any_method_symbols(
    relative_path: Path,
    source: str,
    method_name: str,
    symbols: tuple[str, ...],
    issues: list[str],
) -> bool:
    bodies = extract_method_bodies(source, method_name)
    owner = f"{relative_path.as_posix()}::{method_name}"
    if not bodies:
        issues.append(f"{owner}: method body missing")
        return False

    for body in bodies:
        if all(symbol in body for symbol in symbols):
            return True

    joined_symbols = ", ".join(repr(symbol) for symbol in symbols)
    issues.append(f"{owner}: no overload contains {joined_symbols}")
    return False


def validate_runtime_producer(source: str, issues: list[str]) -> int:
    require_symbols(
        RUNTIME_PATH.as_posix(),
        source,
        (
            "private static int s_appliedLoreSignalPushDropCount",
            "public static bool TryRaisePacketUnlocked(",
            "public static bool TryRaisePacketUnlockedAt(",
        ),
        issues,
    )
    checked = 0
    if require_method_symbols(
        RUNTIME_PATH,
        source,
        "TryRaisePacketUnlockedCore",
        (
            "packetHash == 0u",
            "LoreFragmentScannedSignal",
            "Hash = packetHash",
            "Frame = SystemDispatcher.CurrentFrameId",
            "SourceId = sourceId != 0u ? sourceId : UnlockSourceId",
            "SignalBus<LoreFragmentScannedSignal>.TryPushTracked",
            "ref s_appliedLoreSignalPushDropCount",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        RUNTIME_PATH,
        source,
        "TryRaisePacketUnlockedAt",
        (
            "AbsoluteUniversePosition.IsFinite",
            "LoreFragmentScannedSignal.FlagHasAup",
            "LoreFragmentScannedSignal.FlagPairedScanComplete",
            "ScanCompleteSignal",
            "EntryHash = packetHash",
            "ScanId = packetHash",
            "SignalBus<ScanCompleteSignal>.TryPushTracked",
            "|| raised",
        ),
        issues,
    ):
        checked += 1
    return checked


def validate_message_terminal_producer(source: str, issues: list[str]) -> int:
    require_symbols(
        MESSAGE_TERMINAL_PATH.as_posix(),
        source,
        (
            "private static int s_x001MessageTerminalSignalPushDropCount",
            "AppliedLoreTerminalSourceHash",
            "ResolveActiveAppliedLoreLocaleHash",
        ),
        issues,
    )
    checked = 0
    if require_method_symbols(
        MESSAGE_TERMINAL_PATH,
        source,
        "PublishAppliedLorePacketUnlock",
        (
            "appliedLorePacketHash == 0u",
            "AbsoluteUniversePosition.FromRuntimePosition",
            "H8AppliedLoreRuntime.TryRaisePacketUnlockedAt",
            "AppliedLoreTerminalSourceHash",
            "PublishAppliedLoreTerminalPreview",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        MESSAGE_TERMINAL_PATH,
        source,
        "PublishAppliedLoreTerminalPreview",
        (
            "appliedLorePacketHash == 0u",
            "AppliedLoreTerminalPreviewSignal",
            "PacketHash = appliedLorePacketHash",
            "LocaleHash = ResolveActiveAppliedLoreLocaleHash()",
            "TerminalHash = terminalOsPreviewHash",
            "Frame = SystemDispatcher.CurrentFrameId",
            "TerminalIndex = terminalOsPreviewIndex",
            "SourceHash = AppliedLoreTerminalSourceHash",
            "Surface = (byte)terminalOsPreviewSurface",
            "FlagHasTerminalHash",
            "SignalBus<AppliedLoreTerminalPreviewSignal>.TryPushTracked",
            "ref s_x001MessageTerminalSignalPushDropCount",
        ),
        issues,
    ):
        checked += 1
    return checked


def validate_terminal_os_consumer(source: str, issues: list[str]) -> int:
    checked = 0
    if require_method_symbols(
        TERMINAL_OS_PATH,
        source,
        "ConfigureSignalLanes",
        (
            "SignalBus<AppliedLoreTerminalPreviewSignal>.Configure",
            "AppliedLoreTerminalPreviewSignal.ExpectedCapacity",
            "AppliedLoreTerminalPreviewSignal.MaxFrameSignals",
            "AppliedLoreTerminalPreviewSignal.LowTierFrameSignals",
            "AppliedLoreTerminalPreviewSignal.LaneHash",
            "SignalBus<AppliedLoreTerminalPreviewSignal>.EnsureInitialized",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        TERMINAL_OS_PATH,
        source,
        "ConsumeAppliedLoreTerminalPreviewSignals",
        (
            "SignalBus<AppliedLoreTerminalPreviewSignal>.DroppedLastFlush",
            "FaultAppliedLorePreviewDrop",
            "SignalBus<AppliedLoreTerminalPreviewSignal>.GetFrameSnapshot",
            "signal.PacketHash == 0u",
            "FaultAppliedLorePreviewMiss",
            "ApplyTerminalAppliedLoreLine",
            "ResolveAppliedLorePreviewSurface",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        TERMINAL_OS_PATH,
        source,
        "TryGetTerminalPreviewAppliedLoreUtf8",
        (
            "H8AppliedLoreRuntime.DefaultLocaleHash",
            "H8AppliedLoreRuntime.TryGetUtf8(packetHash, resolvedLocale, surface",
            "IsTerminalPreviewAsciiCompatible",
            "H8AppliedLoreRuntime.TryGetUtf8(",
            "fallbackUtf8.Length > 0",
        ),
        issues,
    ):
        checked += 1
    return checked


def validate_pda_streamer_consumer(source: str, issues: list[str]) -> int:
    checked = 0
    if require_method_symbols(
        PDA_STREAMER_PATH,
        source,
        "ConsumeScanSignals",
        (
            "SignalBus<ScanCompleteSignal>.DroppedLastFlush",
            "SignalBus<LoreFragmentScannedSignal>.DroppedLastFlush",
            "SignalBus<ScanCompleteSignal>.GetFrameSnapshot",
            "SignalBus<LoreFragmentScannedSignal>.GetFrameSnapshot",
            "TryResolveLorePayloadForUnlock",
            "RejectLoreHash",
            "UnlockEntry",
            "PublishLoreUnlockHaptic",
            "FlagPairedScanComplete",
            "HasPairedScanComplete",
            "TryCaptureSignalAup",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        PDA_STREAMER_PATH,
        source,
        "ReplayPersistedPdaLogEvents",
        (
            "UIStateStore.IsInitialized",
            "TryGetPDALogEventHash",
            "TryResolveLorePayloadForUnlock",
            "RejectLoreHash",
            "UnlockEntry",
            "PdaLogReplaySourceId",
            "TryQueuePdaLogEventSelection",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        PDA_STREAMER_PATH,
        source,
        "ResetPdaLogEventReplay",
        (
            "_pdaLogEventReplayCursor = 0",
            "_pdaLogReplayObservedVersion = 0u",
            "_pdaLogReplayObservedCount = 0u",
            "_pdaLogReplayObservedLatestHash = 0u",
            "_pdaLogEventReplayComplete = false",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        PDA_STREAMER_PATH,
        source,
        "TryResolveLorePayloadForUnlock",
        (
            "TryGetAppliedLoreUtf8",
            "H8AppliedLoreSurface.InGameWiki",
            "TryGetH8lrUtf8",
            "_babelStore.FetchUtf8",
            "TryGetMockUtf8",
        ),
        issues,
    ):
        checked += 1
    return checked


def validate_pda_logbook_persistence_owner(source: str, issues: list[str]) -> int:
    require_symbols(
        PDA_LOGBOOK_PATH.as_posix(),
        source,
        (
            "ISaveable",
            "SavePriority",
            "LoadPriority",
            "TryRegisterWithSaveManager",
            "UnregisterFromSaveManager",
            "GlobalRegistryServiceSlot.Save",
        ),
        issues,
    )
    checked = 0
    append_body = extract_method_body(source, "TryAppendEntry")
    if append_body is None:
        issues.append(f"{PDA_LOGBOOK_PATH.as_posix()}::TryAppendEntry: method body missing")
    else:
        require_symbols(
            f"{PDA_LOGBOOK_PATH.as_posix()}::TryAppendEntry",
            append_body,
            (
                "UIStateStore.AppendPDALogEventHash",
                "unchecked((uint)originHash)",
                "playTimeSeconds",
                "PDAEvents.TryRaiseLogbookChanged",
                "RefreshLogbookSignalPumpRegistration",
            ),
            issues,
        )
        require_symbol_order(
            f"{PDA_LOGBOOK_PATH.as_posix()}::TryAppendEntry",
            append_body,
            (
                "UIStateStore.AppendPDALogEventHash",
                "PDAEvents.TryRaiseLogbookChanged",
                "RefreshLogbookSignalPumpRegistration",
            ),
            issues,
        )
        checked += 1
    if require_method_symbols(
        PDA_LOGBOOK_PATH,
        source,
        "PopulateSaveData",
        (
            "data.pdaLogbook",
            "entryCount",
            "nextSequence",
            "playTimeSeconds",
            "originHash",
            "seenOriginHashes",
        ),
        issues,
    ):
        checked += 1
    load_body = extract_method_body(source, "LoadFromSaveData")
    if load_body is None:
        issues.append(f"{PDA_LOGBOOK_PATH.as_posix()}::LoadFromSaveData: method body missing")
    else:
        require_symbols(
            f"{PDA_LOGBOOK_PATH.as_posix()}::LoadFromSaveData",
            load_body,
            (
                "UIStateStore.ClearPDALogEventHistory",
                "AppendLoadedEntry",
                "UIStateStore.AppendPDALogEventHash",
                "PDAEvents.TryRaiseLogbookChanged",
                "RebindOwnerSubscriptions",
                "RefreshLogbookSignalPumpRegistration",
            ),
            issues,
        )
        require_symbol_order(
            f"{PDA_LOGBOOK_PATH.as_posix()}::LoadFromSaveData",
            load_body,
            (
                "UIStateStore.ClearPDALogEventHistory",
                "AppendLoadedEntry",
                "UIStateStore.AppendPDALogEventHash",
                "PDAEvents.TryRaiseLogbookChanged",
                "RebindOwnerSubscriptions",
                "RefreshLogbookSignalPumpRegistration",
            ),
            issues,
        )
        checked += 1
    if require_method_symbols(
        PDA_LOGBOOK_PATH,
        source,
        "OnGlobalRegistryServiceReplaced",
        (
            "GlobalRegistryServiceSlot.Save",
            "UnregisterFromSaveManager",
            "_saveService = currentService as ISaveService",
            "TryRegisterWithSaveManager",
        ),
        issues,
    ):
        checked += 1
    return checked


def validate_pda_data_log_ui_consumer(source: str, issues: list[str]) -> int:
    checked = 0
    if require_method_symbols(
        PDA_DATA_LOG_TAB_PATH,
        source,
        "OnPDAEvent",
        (
            "PDAEventType.LogbookChanged",
            "HandleEventSourcedLogbookChanged",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        PDA_DATA_LOG_TAB_PATH,
        source,
        "HandleEventSourcedLogbookChanged",
        (
            "eventHash != 0u",
            "UIStateStore.TryGetPDALogEvent(0, out uint latestHash, out float timestampSeconds)",
            "_latestSimulationLogHash = latestHash",
            "_latestSimulationLogTimestamp = timestampSeconds",
            "_dirty = true",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        PDA_DATA_LOG_TAB_PATH,
        source,
        "RefreshEventSourcedLogStateFromUIStore",
        (
            "UIStateStore.IsInitialized",
            "UIStateStore.GetPDAState",
            "pdaState.LogEntryCount",
            "pdaState.LatestLogEventHash",
            "UIStateStore.TryGetPDALogEvent(0, out uint latestHash, out float timestampSeconds)",
            "_latestSimulationLogHash = pdaState.LatestLogEventHash",
            "_dirty = true",
            "_visualLateFrameDirty = true",
        ),
        issues,
    ):
        checked += 1
    return checked


def validate_pda_marker_registry_owner(source: str, issues: list[str]) -> int:
    owner = PDA_MARKER_REGISTRY_PATH.as_posix()
    require_symbols(
        owner,
        source,
        (
            "ISaveable",
            "GlobalRegistryServiceSlot.Save",
            "Hecton8.Core.GlobalRegistry.PDAMarkers",
            "Hecton8.Core.GlobalRegistry.RegisterPDAMarkerRuntime(this)",
            "Hecton8.Core.GlobalRegistry.UnregisterPDAMarkerRuntime(this)",
            "public uint Revision => _revision",
            "private void CommitMarkerRevision(uint markerHashId)",
            "CommitMarkerRevision(existing.markerHashId)",
            "CommitMarkerRevision(record.markerHashId)",
            "CommitMarkerRevision(removedRecord.markerHashId)",
        ),
        issues,
    )
    event_raise_count = source.count("TryRaiseMarkerChanged")
    if event_raise_count != 1:
        issues.append(
            f"{owner}: MarkerChanged must be raised only by CommitMarkerRevision "
            f"(found {event_raise_count})"
        )

    checked = 0
    if require_method_symbols(
        PDA_MARKER_REGISTRY_PATH,
        source,
        "CommitMarkerRevision",
        (
            "_revision++",
            "_revision == 0u",
            "Application.isPlaying",
            "Hecton8.UI.PDAEvents.TryRaiseMarkerChanged(markerHashId, _markerCount)",
        ),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        PDA_MARKER_REGISTRY_PATH,
        source,
        "TryCreateMarker",
        ("CommitMarkerRevision(record.markerHashId)",),
        issues,
    ):
        checked += 1
    if require_any_method_symbols(
        PDA_MARKER_REGISTRY_PATH,
        source,
        "RemoveMarker",
        ("CommitMarkerRevision(removedRecord.markerHashId)",),
        issues,
    ):
        checked += 1
    if require_any_method_symbols(
        PDA_MARKER_REGISTRY_PATH,
        source,
        "UpdateMarkerPosition",
        ("CommitMarkerRevision(record.markerHashId)",),
        issues,
    ):
        checked += 1
    if require_any_method_symbols(
        PDA_MARKER_REGISTRY_PATH,
        source,
        "SetMarkerHudVisibility",
        ("CommitMarkerRevision(record.markerHashId)",),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        PDA_MARKER_REGISTRY_PATH,
        source,
        "LoadFromSaveData",
        ("CommitMarkerRevision(0u)",),
        issues,
    ):
        checked += 1
    if require_method_symbols(
        PDA_MARKER_REGISTRY_PATH,
        source,
        "OnOriginShift",
        ("CommitMarkerRevision(0u)",),
        issues,
    ):
        checked += 1
    return checked


def validate_pda_map_marker_consumer(source: str, issues: list[str]) -> int:
    owner = PDA_MAP_TAB_PATH.as_posix()
    require_symbols(
        owner,
        source,
        (
            "_observedMarkerRevision",
            "RefreshMarkerRevisionFallback",
            "ClearMarkerVisualSlotsNotInSnapshot",
            "SnapshotContainsMarker",
            "ResolveMarkerRegistry",
        ),
        issues,
    )

    checked = 0
    late_body = extract_method_body(source, "LateFrameTick")
    if late_body is None:
        issues.append(f"{owner}::LateFrameTick: method body missing")
    else:
        require_symbol_order(
            f"{owner}::LateFrameTick",
            late_body,
            (
                "RefreshMarkerRevisionFallback(force: false)",
                "ProcessPendingMarkerUpdates",
            ),
            issues,
        )
        checked += 1

    enqueue_body = extract_method_body(source, "EnqueueAllMarkerUpdates")
    if enqueue_body is None:
        issues.append(f"{owner}::EnqueueAllMarkerUpdates: method body missing")
    else:
        require_symbol_order(
            f"{owner}::EnqueueAllMarkerUpdates",
            enqueue_body,
            (
                "CopyMarkers(_markerUpdateSnapshots, hudOnly: false)",
                "ClearPendingMarkerUpdates",
                "ClearMarkerVisualSlotsNotInSnapshot(markerCount)",
                "EnqueueMarkerUpdate",
            ),
            issues,
        )
        checked += 1

    if require_method_symbols(
        PDA_MAP_TAB_PATH,
        source,
        "OnGlobalRegistryServiceReplaced",
        (
            "GlobalRegistryServiceSlot.PDAMarkerRuntime",
            "_observedMarkerRevision = uint.MaxValue",
            "RefreshMarkerRevisionFallback(force: true)",
        ),
        issues,
    ):
        checked += 1
    return checked


def validate_pda_marker_hud_consumer(source: str, issues: list[str]) -> int:
    owner = PDA_MARKER_HUD_PATH.as_posix()
    require_symbols(
        owner,
        source,
        (
            "_observedMarkerRevision",
            "RefreshMarkerRevisionFallback",
            "markerRegistry.CopyMarkers(_markerBuffer, hudOnly: true)",
        ),
        issues,
    )

    checked = 0
    if require_method_symbols(
        PDA_MARKER_HUD_PATH,
        source,
        "OnEnable",
        (
            "_observedMarkerRevision = uint.MaxValue",
            "RefreshMarkerRevisionFallback(force: true)",
        ),
        issues,
    ):
        checked += 1

    sample_body = extract_method_body(source, "SampleMarkerDisplay")
    if sample_body is None:
        issues.append(f"{owner}::SampleMarkerDisplay: method body missing")
    else:
        require_symbol_order(
            f"{owner}::SampleMarkerDisplay",
            sample_body,
            (
                "RefreshMarkerRevisionFallback(force: false)",
                "markerRegistry.CopyMarkers(_markerBuffer, hudOnly: true)",
            ),
            issues,
        )
        checked += 1

    if require_method_symbols(
        PDA_MARKER_HUD_PATH,
        source,
        "OnGlobalRegistryServiceReplaced",
        (
            "GlobalRegistryServiceSlot.PDAMarkerRuntime",
            "_observedMarkerRevision = uint.MaxValue",
            "_markersDirty = true",
        ),
        issues,
    ):
        checked += 1
    return checked


def validate_signal_route(root: Path, max_issues: int = 80) -> SignalRouteStats:
    root = root.resolve()
    issues: list[str] = []
    sources = {
        RUNTIME_PATH: read_text(root, RUNTIME_PATH, issues),
        MESSAGE_TERMINAL_PATH: read_text(root, MESSAGE_TERMINAL_PATH, issues),
        TERMINAL_OS_PATH: read_text(root, TERMINAL_OS_PATH, issues),
        PDA_STREAMER_PATH: read_text(root, PDA_STREAMER_PATH, issues),
        PDA_LOGBOOK_PATH: read_text(root, PDA_LOGBOOK_PATH, issues),
        PDA_DATA_LOG_TAB_PATH: read_text(root, PDA_DATA_LOG_TAB_PATH, issues),
        PDA_MARKER_REGISTRY_PATH: read_text(root, PDA_MARKER_REGISTRY_PATH, issues),
        PDA_MAP_TAB_PATH: read_text(root, PDA_MAP_TAB_PATH, issues),
        PDA_MARKER_HUD_PATH: read_text(root, PDA_MARKER_HUD_PATH, issues),
    }

    checked_methods = 0
    if sources[RUNTIME_PATH]:
        checked_methods += validate_runtime_producer(sources[RUNTIME_PATH], issues)
    if sources[MESSAGE_TERMINAL_PATH]:
        checked_methods += validate_message_terminal_producer(sources[MESSAGE_TERMINAL_PATH], issues)
    if sources[TERMINAL_OS_PATH]:
        checked_methods += validate_terminal_os_consumer(sources[TERMINAL_OS_PATH], issues)
    if sources[PDA_STREAMER_PATH]:
        checked_methods += validate_pda_streamer_consumer(sources[PDA_STREAMER_PATH], issues)
    if sources[PDA_LOGBOOK_PATH]:
        checked_methods += validate_pda_logbook_persistence_owner(sources[PDA_LOGBOOK_PATH], issues)
    if sources[PDA_DATA_LOG_TAB_PATH]:
        checked_methods += validate_pda_data_log_ui_consumer(sources[PDA_DATA_LOG_TAB_PATH], issues)
    if sources[PDA_MARKER_REGISTRY_PATH]:
        checked_methods += validate_pda_marker_registry_owner(sources[PDA_MARKER_REGISTRY_PATH], issues)
    if sources[PDA_MAP_TAB_PATH]:
        checked_methods += validate_pda_map_marker_consumer(sources[PDA_MAP_TAB_PATH], issues)
    if sources[PDA_MARKER_HUD_PATH]:
        checked_methods += validate_pda_marker_hud_consumer(sources[PDA_MARKER_HUD_PATH], issues)

    if len(issues) > max_issues:
        issues = issues[:max_issues] + [f"... {len(issues) - max_issues} more issues"]

    return SignalRouteStats(
        checked_files=sum(1 for text in sources.values() if text),
        checked_methods=checked_methods,
        issues=tuple(issues),
    )


def stats_to_payload(stats: SignalRouteStats) -> dict[str, object]:
    return {
        "clean": stats.clean,
        "checked_files": stats.checked_files,
        "checked_methods": stats.checked_methods,
        "issues": list(stats.issues),
    }


def render_stats(stats: SignalRouteStats) -> str:
    if stats.clean:
        return (
            "AppliedLore signal route OK: "
            f"checked_files={stats.checked_files} checked_methods={stats.checked_methods}"
        )
    lines = [
        "AppliedLore signal route failed: "
        f"checked_files={stats.checked_files} checked_methods={stats.checked_methods} "
        f"issues={len(stats.issues)}"
    ]
    lines.extend(f"- {issue}" for issue in stats.issues)
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Hecton8 repository root")
    parser.add_argument("--max-issues", type=int, default=80)
    parser.add_argument("--json", action="store_true", help="Write a machine-readable payload to stdout")
    args = parser.parse_args(argv)

    stats = validate_signal_route(Path(args.root), max_issues=max(args.max_issues, 0))
    if args.json:
        print(json.dumps(stats_to_payload(stats), ensure_ascii=False, indent=2))
    elif stats.clean:
        print(render_stats(stats))
    else:
        print(render_stats(stats), file=sys.stderr)
    return 0 if stats.clean else 1


if __name__ == "__main__":
    raise SystemExit(main())
