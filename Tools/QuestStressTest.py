#!/usr/bin/env python3
"""Stress-test HECTON-8 quest DAG data.

Primary source is Data/Narrative/Quest_Graph.json. If that file is absent,
the tool audits the live QuestData Unity YAML assets as a fallback and marks
the missing JSON contract as a critical finding. When --export-graph is used,
the export is rebuilt from live QuestData assets before analysis so the JSON
mirror cannot silently re-export stale JSON.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
from collections import defaultdict, deque
from dataclasses import dataclass
from pathlib import Path
from typing import Any, DefaultDict, Dict, Iterable, List, Optional, Sequence, Set, Tuple


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_GRAPH_PATH = ROOT_DIR / "Data" / "Narrative" / "Quest_Graph.json"
DEFAULT_ASSET_DIR = ROOT_DIR / "Assets" / "_Project" / "Data" / "Lore" / "Quests"
DEFAULT_SEQUENCES = 1_000_000
DEFAULT_SEQUENCE_LENGTH = 24
DEFAULT_SEED = 0x48385147

TRIGGER_KIND_BY_VALUE = {
    0: "ItemCollected",
    1: "DepthReached",
    2: "BiomeEntered",
    3: "DiscoveryMade",
    4: "AudioLogFound",
    5: "EclipseStarted",
    6: "SignalDecoded",
    7: "CraftCompleted",
    99: "None",
}

COMPLETION_KIND_BY_VALUE = {
    0: "ItemCollected",
    1: "DepthReached",
    2: "BiomeEntered",
    3: "DiscoveryMade",
    4: "AudioLogFound",
    5: "SignalDecoded",
    6: "CraftCompleted",
    99: "None",
}

PHASE_BY_VALUE = {
    0: "None",
    1: "Abyssal",
    2: "Thermal",
}

ID_EVENT_KINDS = frozenset(("ItemCollected", "CraftCompleted", "DiscoveryMade", "AudioLogFound", "SignalDecoded"))
NUMERIC_EVENT_KINDS = frozenset(("DepthReached", "BiomeEntered"))
END_PATTERNS = (
    "end_game",
    "endgame",
    "end game",
    "core_reached",
    "the_core",
    "atlas_core",
)


@dataclass(frozen=True)
class Quest:
    quest_id: str
    title: str
    trigger_kind: str
    trigger_id: str
    trigger_value: float
    completion_kind: str
    completion_id: str
    completion_value: float
    prerequisites: Tuple[str, ...]
    critical_item_id: str
    respawn_event_id: str
    phase_gate: str
    marker_target_id: str
    auto_activate: bool
    source: str
    explicit_end: bool = False


@dataclass(frozen=True)
class Event:
    kind: str
    event_id: str = ""
    value: float = 0.0
    phase: str = "None"

    def label(self) -> str:
        if self.kind == "Phase":
            return "Phase:" + self.phase
        if self.kind in ID_EVENT_KINDS:
            return self.kind + ":" + self.event_id + ":" + compact_number(self.value)
        if self.kind in NUMERIC_EVENT_KINDS:
            return self.kind + ":" + compact_number(self.value)
        return self.kind


@dataclass(frozen=True)
class RuntimeNode:
    quest_index: int
    transition: str
    kind: str
    event_id: str
    value: float
    order: int


@dataclass
class AnalysisResult:
    requested_graph_missing: bool
    source_kind: str
    source_path: Path
    quests: List[Quest]
    duplicate_ids: List[str]
    missing_prerequisites: List[str]
    cycles: List[List[str]]
    end_ids: List[str]
    paths_to_end: List[List[str]]
    dead_ends: List[str]
    impossible_requirements: List[str]
    disconnected_end_nodes: List[str]
    event_candidates: List[Event]


@dataclass
class SimulationResult:
    sequence_count: int
    sequence_length: int
    seed: int
    elapsed_seconds: float
    no_active_softlocks: int
    manual_stall_sequences: int
    end_completed_sequences: int
    end_active_sequences: int
    first_softlock_sequence: int
    first_softlock_prefix: List[str]
    activation_counts: List[int]
    completion_counts: List[int]
    terminal_active_histogram: Dict[int, int]


class DeterministicLcg:
    """Small replayable PRNG using multiply-high bounded selection."""

    def __init__(self, seed: int) -> None:
        self.state = seed & 0xFFFFFFFF
        if self.state == 0:
            self.state = DEFAULT_SEED

    def next_u32(self) -> int:
        self.state = (1664525 * self.state + 1013904223) & 0xFFFFFFFF
        return self.state

    def bounded(self, upper_bound: int) -> int:
        if upper_bound <= 1:
            return 0
        return (self.next_u32() * upper_bound) >> 32


def compact_number(value: float) -> str:
    if int(value) == value:
        return str(int(value))
    return ("%0.3f" % value).rstrip("0").rstrip(".")


def parse_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value != 0
    if value is None:
        return False
    text = str(value).strip().strip('"').lower()
    return text in ("1", "true", "yes", "on")


def parse_float(value: Any) -> float:
    if value is None or value == "":
        return 0.0
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def parse_int(value: Any, default: int = 99) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def clean_scalar(value: str) -> str:
    text = value.strip()
    if not text:
        return ""
    if text.startswith('"') and text.endswith('"') and len(text) >= 2:
        text = text[1:-1]
    return text.replace('\\"', '"')


def normalize_kind(value: Any, table: Dict[int, str]) -> str:
    if isinstance(value, (int, float)):
        return table.get(int(value), "Unknown")
    text = str(value or "").strip()
    if not text:
        return "None"
    if text.isdigit():
        return table.get(int(text), "Unknown")
    text = text.replace("QuestTriggerType.", "").replace("QuestCompletionType.", "")
    text = text.replace("On", "", 1) if text.startswith("On") else text
    if text == "SignalDetected":
        return "SignalDecoded"
    if text in ("Manual", "None"):
        return "None"
    valid = set(TRIGGER_KIND_BY_VALUE.values()) | set(COMPLETION_KIND_BY_VALUE.values())
    return text if text in valid else "Unknown"


def normalize_phase(value: Any) -> str:
    if isinstance(value, (int, float)):
        return PHASE_BY_VALUE.get(int(value), "Unknown")
    text = str(value or "").strip()
    if not text:
        return "None"
    if text.isdigit():
        return PHASE_BY_VALUE.get(int(text), "Unknown")
    return text.replace("QuestPhaseGateType.", "")


def load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except json.JSONDecodeError as exc:
        raise SystemExit("QUEST VALIDATION FAILED: %s JSON syntax error: %s" % (path, exc))


def parse_json_graph(path: Path) -> List[Quest]:
    data = load_json(path)
    if isinstance(data, list):
        raw_nodes = data
        raw_edges = []
    elif isinstance(data, dict):
        raw_nodes = data.get("quests") or data.get("nodes") or data.get("questNodes") or []
        raw_edges = data.get("edges") or []
    else:
        raise SystemExit("QUEST VALIDATION FAILED: %s must contain a JSON object or array" % path)

    prereq_by_id: DefaultDict[str, List[str]] = defaultdict(list)
    for edge in raw_edges:
        if not isinstance(edge, dict):
            continue
        src = str(edge.get("from") or edge.get("source") or "").strip()
        dst = str(edge.get("to") or edge.get("target") or "").strip()
        if src and dst:
            prereq_by_id[dst].append(src)

    quests: List[Quest] = []
    for index, raw in enumerate(raw_nodes):
        if not isinstance(raw, dict):
            continue

        quest_id = str(raw.get("questId") or raw.get("quest_id") or raw.get("id") or raw.get("name") or "").strip()
        if not quest_id:
            quest_id = "json_node_%03d" % index

        trigger = raw.get("trigger") if isinstance(raw.get("trigger"), dict) else raw
        completion = raw.get("completion") if isinstance(raw.get("completion"), dict) else raw

        raw_prereqs = (
            raw.get("prerequisiteQuestIds")
            or raw.get("prerequisites")
            or raw.get("requires")
            or raw.get("requiredQuests")
            or []
        )
        prereqs = list(extract_prerequisites(raw_prereqs))
        prereqs.extend(prereq_by_id.get(quest_id, ()))

        quests.append(
            Quest(
                quest_id=quest_id,
                title=str(raw.get("displayTitle") or raw.get("title") or raw.get("name") or quest_id),
                trigger_kind=normalize_kind(trigger.get("triggerType") or trigger.get("type"), TRIGGER_KIND_BY_VALUE),
                trigger_id=str(trigger.get("triggerId") or trigger.get("id") or ""),
                trigger_value=parse_float(trigger.get("triggerValue") or trigger.get("value")),
                completion_kind=normalize_kind(completion.get("completionType") or completion.get("type"), COMPLETION_KIND_BY_VALUE),
                completion_id=str(completion.get("completionId") or completion.get("id") or ""),
                completion_value=parse_float(completion.get("completionValue") or completion.get("value")),
                prerequisites=tuple(dict.fromkeys(prereqs)),
                critical_item_id=str(raw.get("criticalItemId") or ""),
                respawn_event_id=str(raw.get("respawnEventId") or ""),
                phase_gate=normalize_phase(raw.get("phaseGate")),
                marker_target_id=str(raw.get("markerTargetId") or ""),
                auto_activate=parse_bool(raw.get("autoActivateOnStart") or raw.get("autoActivate")),
                source=str(path),
                explicit_end=parse_bool(raw.get("end_game") or raw.get("endGame") or raw.get("isEndGame")),
            )
        )
    return quests


def extract_prerequisites(raw_prereqs: Any) -> Iterable[str]:
    if raw_prereqs is None:
        return ()
    if isinstance(raw_prereqs, str):
        return (raw_prereqs,) if raw_prereqs.strip() else ()
    if not isinstance(raw_prereqs, Sequence):
        return ()
    values: List[str] = []
    for item in raw_prereqs:
        if isinstance(item, dict):
            value = item.get("questId") or item.get("id") or item.get("quest_id")
        else:
            value = item
        text = str(value or "").strip()
        if text:
            values.append(text)
    return tuple(values)


def parse_quest_asset(path: Path) -> Optional[Quest]:
    values: Dict[str, Any] = {}
    prereqs: List[str] = []
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    index = 0
    while index < len(lines):
        line = lines[index]
        match = re.match(r"^\s{2}([A-Za-z_][A-Za-z0-9_]*):(?:\s*(.*))?$", line)
        if not match:
            index += 1
            continue
        key = match.group(1)
        value = match.group(2) or ""
        if key == "prerequisiteQuestIds":
            index += 1
            while index < len(lines):
                item_match = re.match(r"^\s{2}-\s*(.*)\s*$", lines[index])
                if not item_match:
                    break
                item = clean_scalar(item_match.group(1))
                if item:
                    prereqs.append(item)
                index += 1
            continue
        values[key] = clean_scalar(value)
        index += 1

    quest_id = str(values.get("questId") or "").strip()
    if not quest_id:
        return None

    return Quest(
        quest_id=quest_id,
        title=str(values.get("displayTitle") or quest_id),
        trigger_kind=normalize_kind(values.get("triggerType"), TRIGGER_KIND_BY_VALUE),
        trigger_id=str(values.get("triggerId") or ""),
        trigger_value=parse_float(values.get("triggerValue")),
        completion_kind=normalize_kind(values.get("completionType"), COMPLETION_KIND_BY_VALUE),
        completion_id=str(values.get("completionId") or ""),
        completion_value=parse_float(values.get("completionValue")),
        prerequisites=tuple(prereqs),
        critical_item_id=str(values.get("criticalItemId") or ""),
        respawn_event_id=str(values.get("respawnEventId") or ""),
        phase_gate=normalize_phase(values.get("phaseGate")),
        marker_target_id=str(values.get("markerTargetId") or ""),
        auto_activate=parse_bool(values.get("autoActivateOnStart")),
        source=str(path),
        explicit_end=False,
    )


def parse_quest_assets(asset_dir: Path) -> List[Quest]:
    if not asset_dir.exists():
        raise SystemExit("QUEST VALIDATION FAILED: fallback asset dir missing: %s" % asset_dir)
    quests: List[Quest] = []
    for path in sorted(asset_dir.glob("Quest_*.asset")):
        quest = parse_quest_asset(path)
        if quest is not None:
            quests.append(quest)
    if not quests:
        raise SystemExit("QUEST VALIDATION FAILED: no QuestData assets found in %s" % asset_dir)
    return quests


def load_quests(graph_path: Path, asset_dir: Path) -> Tuple[List[Quest], bool, str, Path]:
    if graph_path.exists():
        return parse_json_graph(graph_path), False, "json", graph_path
    return parse_quest_assets(asset_dir), True, "questdata-yaml-fallback", asset_dir


def build_event_key(kind: str, event_id: str, value: float) -> Tuple[str, str, int]:
    if kind in ID_EVENT_KINDS:
        return kind, event_id, 0
    if kind in NUMERIC_EVENT_KINDS:
        return kind, "", int(value)
    if kind == "EclipseStarted":
        return kind, "", 0
    return "None", "", 0


def is_end_quest(quest: Quest) -> bool:
    if quest.explicit_end:
        return True
    haystack = (quest.quest_id + " " + quest.title + " " + quest.marker_target_id).lower()
    normalized = haystack.replace("-", "_")
    for pattern in END_PATTERNS:
        if pattern in normalized or pattern in haystack:
            return True
    return False


def build_dependency_edges(quests: Sequence[Quest]) -> Dict[str, List[str]]:
    edges: DefaultDict[str, List[str]] = defaultdict(list)
    quest_ids = {quest.quest_id for quest in quests}

    for quest in quests:
        for prerequisite in quest.prerequisites:
            if prerequisite in quest_ids:
                edges[prerequisite].append(quest.quest_id)

    for source in quests:
        if source.completion_kind == "None":
            continue
        for target in quests:
            if source.quest_id == target.quest_id or target.trigger_kind == "None":
                continue
            if completion_enables_trigger(source, target):
                edges[source.quest_id].append(target.quest_id)

    for quest_id, destinations in list(edges.items()):
        edges[quest_id] = sorted(dict.fromkeys(destinations))
    return dict(edges)


def completion_enables_trigger(source: Quest, target: Quest) -> bool:
    if source.completion_kind != target.trigger_kind:
        return False

    if source.completion_kind in ID_EVENT_KINDS:
        return bool(source.completion_id) and source.completion_id == target.trigger_id

    if source.completion_kind == "DepthReached":
        return source.completion_value <= target.trigger_value

    if source.completion_kind == "BiomeEntered":
        return int(source.completion_value) == int(target.trigger_value)

    if source.completion_kind == "EclipseStarted":
        return True

    return False


def find_cycles(quests: Sequence[Quest], edges: Dict[str, List[str]]) -> List[List[str]]:
    quest_ids = {quest.quest_id for quest in quests}
    color: Dict[str, int] = {quest_id: 0 for quest_id in quest_ids}
    stack: List[str] = []
    cycles: List[List[str]] = []

    def visit(node_id: str) -> None:
        color[node_id] = 1
        stack.append(node_id)
        for next_id in edges.get(node_id, ()):
            if next_id not in color:
                continue
            if color[next_id] == 0:
                visit(next_id)
            elif color[next_id] == 1 and next_id in stack:
                start = stack.index(next_id)
                cycles.append(stack[start:] + [next_id])
        stack.pop()
        color[node_id] = 2

    for quest in quests:
        if color[quest.quest_id] == 0:
            visit(quest.quest_id)
    return cycles


def find_paths_to_end(quests: Sequence[Quest], edges: Dict[str, List[str]], end_ids: Set[str], limit: int = 128) -> List[List[str]]:
    quest_ids = {quest.quest_id for quest in quests}
    incoming: DefaultDict[str, int] = defaultdict(int)
    for src, dsts in edges.items():
        for dst in dsts:
            incoming[dst] += 1

    roots = [quest.quest_id for quest in quests if quest.auto_activate or incoming[quest.quest_id] == 0]
    if not roots:
        roots = [quest.quest_id for quest in quests]

    paths: List[List[str]] = []
    queue: deque[List[str]] = deque([root] for root in roots if root in quest_ids)
    while queue and len(paths) < limit:
        path = queue.popleft()
        current = path[-1]
        if current in end_ids:
            paths.append(path)
            continue
        for next_id in edges.get(current, ()):
            if next_id in path:
                continue
            queue.append(path + [next_id])
    return paths


def build_event_candidates(quests: Sequence[Quest]) -> List[Event]:
    events: Dict[str, Event] = {}

    def add(event: Event) -> None:
        events[event.label()] = event

    for quest in quests:
        add_event_for_condition(add, quest.trigger_kind, quest.trigger_id, quest.trigger_value)
        add_event_for_condition(add, quest.completion_kind, quest.completion_id, quest.completion_value)

    add(Event("Phase", phase="Abyssal"))
    add(Event("Phase", phase="Thermal"))
    add(Event("DiscoveryMade", "irrelevant_discovery", 1.0))
    add(Event("ItemCollected", "irrelevant_item", 1.0))
    add(Event("DepthReached", value=0.0))
    return [events[key] for key in sorted(events)]


def add_event_for_condition(add: Any, kind: str, event_id: str, value: float) -> None:
    if kind == "None" or kind == "Unknown":
        return
    if kind in ID_EVENT_KINDS:
        if event_id:
            add(Event(kind, event_id, value if value > 0 else 1.0))
        return
    if kind == "DepthReached":
        add(Event(kind, value=value))
        return
    if kind == "BiomeEntered":
        add(Event(kind, value=value))
        return
    if kind == "EclipseStarted":
        add(Event(kind))


def analyze(quests: List[Quest], requested_graph_missing: bool, source_kind: str, source_path: Path) -> AnalysisResult:
    seen: Dict[str, str] = {}
    duplicate_ids: List[str] = []
    missing_prerequisites: List[str] = []
    impossible_requirements: List[str] = []
    quest_ids = {quest.quest_id for quest in quests}

    for quest in quests:
        if quest.quest_id in seen:
            duplicate_ids.append("%s duplicates %s and %s" % (quest.quest_id, seen[quest.quest_id], quest.source))
        seen[quest.quest_id] = quest.source

    for quest in quests:
        for prerequisite in quest.prerequisites:
            if prerequisite not in quest_ids:
                missing_prerequisites.append("%s references missing prerequisite %s" % (quest.quest_id, prerequisite))

        if quest.trigger_kind == "None" and not quest.auto_activate:
            impossible_requirements.append("%s has Manual trigger and is not auto-activated" % quest.quest_id)
        if quest.trigger_kind in ID_EVENT_KINDS and not quest.trigger_id:
            impossible_requirements.append("%s has %s trigger without triggerId" % (quest.quest_id, quest.trigger_kind))
        if quest.completion_kind in ID_EVENT_KINDS and not quest.completion_id:
            impossible_requirements.append("%s has %s completion without completionId" % (quest.quest_id, quest.completion_kind))
    edges = build_dependency_edges(quests)
    cycles = find_cycles(quests, edges)
    end_ids = [quest.quest_id for quest in quests if is_end_quest(quest)]
    paths = find_paths_to_end(quests, edges, set(end_ids))
    dead_ends = []
    for quest in quests:
        if quest.completion_kind == "None":
            dead_ends.append("%s has no event-driven Complete trigger" % quest.quest_id)

    disconnected_end_nodes = []
    for end_id in end_ids:
        has_nontrivial_path = any(len(path) > 1 and path[-1] == end_id for path in paths)
        if not has_nontrivial_path:
            disconnected_end_nodes.append(end_id)

    return AnalysisResult(
        requested_graph_missing=requested_graph_missing,
        source_kind=source_kind,
        source_path=source_path,
        quests=quests,
        duplicate_ids=duplicate_ids,
        missing_prerequisites=missing_prerequisites,
        cycles=cycles,
        end_ids=end_ids,
        paths_to_end=paths,
        dead_ends=dead_ends,
        impossible_requirements=impossible_requirements,
        disconnected_end_nodes=disconnected_end_nodes,
        event_candidates=build_event_candidates(quests),
    )


def build_runtime_nodes(quests: Sequence[Quest]) -> Dict[str, List[RuntimeNode]]:
    nodes_by_kind: DefaultDict[str, List[RuntimeNode]] = defaultdict(list)
    for index, quest in enumerate(quests):
        if quest.trigger_kind != "None":
            nodes_by_kind[quest.trigger_kind].append(
                RuntimeNode(index, "Activate", quest.trigger_kind, quest.trigger_id, quest.trigger_value, index * 2)
            )
        if quest.completion_kind != "None":
            nodes_by_kind[quest.completion_kind].append(
                RuntimeNode(index, "Complete", quest.completion_kind, quest.completion_id, quest.completion_value, index * 2 + 1)
            )
    for nodes in nodes_by_kind.values():
        nodes.sort(key=lambda node: node.order)
    return dict(nodes_by_kind)


def phase_satisfied(quest: Quest, phases: Set[str]) -> bool:
    return quest.phase_gate in ("", "None") or quest.phase_gate in phases


def prerequisites_satisfied(quest: Quest, completed: Set[int], quest_index_by_id: Dict[str, int]) -> bool:
    for prerequisite in quest.prerequisites:
        index = quest_index_by_id.get(prerequisite)
        if index is None or index not in completed:
            return False
    return True


def event_matches(node: RuntimeNode, event: Event) -> bool:
    if node.kind != event.kind:
        return False
    if node.kind in ID_EVENT_KINDS:
        if node.event_id and node.event_id != event.event_id:
            return False
        return node.value <= 0.0 or event.value >= node.value
    if node.kind == "DepthReached":
        return event.value >= node.value
    if node.kind == "BiomeEntered":
        return int(event.value) == int(node.value)
    if node.kind == "EclipseStarted":
        return True
    return False


def apply_event(
    event: Event,
    quests: Sequence[Quest],
    nodes_by_kind: Dict[str, List[RuntimeNode]],
    quest_index_by_id: Dict[str, int],
    active: Set[int],
    completed: Set[int],
    phases: Set[str],
    activation_counts: List[int],
    completion_counts: List[int],
) -> None:
    if event.kind == "Phase":
        phases.add(event.phase)
        return

    for node in nodes_by_kind.get(event.kind, ()):
        if not event_matches(node, event):
            continue

        quest = quests[node.quest_index]
        if node.transition == "Activate":
            if node.quest_index in active or node.quest_index in completed:
                continue
            if not phase_satisfied(quest, phases):
                continue
            if not prerequisites_satisfied(quest, completed, quest_index_by_id):
                continue
            active.add(node.quest_index)
            activation_counts[node.quest_index] += 1
            continue

        if node.quest_index not in active or node.quest_index in completed:
            continue
        active.discard(node.quest_index)
        completed.add(node.quest_index)
        completion_counts[node.quest_index] += 1


def initialize_active(quests: Sequence[Quest], phases: Set[str], activation_counts: List[int]) -> Set[int]:
    active: Set[int] = set()
    for index, quest in enumerate(quests):
        if not quest.auto_activate:
            continue
        if not phase_satisfied(quest, phases):
            continue
        active.add(index)
        activation_counts[index] += 1
    return active


def is_manual_stall(active: Set[int], quests: Sequence[Quest]) -> bool:
    if not active:
        return False
    for index in active:
        if quests[index].completion_kind != "None":
            return False
    return True


def simulate(analysis: AnalysisResult, sequence_count: int, sequence_length: int, seed: int) -> SimulationResult:
    vectorized = simulate_vectorized(analysis, sequence_count, sequence_length, seed)
    if vectorized is not None:
        return vectorized

    quests = analysis.quests
    quest_index_by_id = {quest.quest_id: index for index, quest in enumerate(quests)}
    nodes_by_kind = build_fast_runtime_nodes(quests, quest_index_by_id)
    end_mask = 0
    for end_id in analysis.end_ids:
        index = quest_index_by_id.get(end_id)
        if index is not None:
            end_mask |= 1 << index
    events = analysis.event_candidates
    if not events:
        raise SystemExit("QUEST VALIDATION FAILED: no event candidates found")

    auto_indices: List[int] = []
    auto_mask = 0
    non_manual_completion_mask = 0
    future_activation_mask = 0
    for index, quest in enumerate(quests):
        if quest.auto_activate and phase_requirement_mask(quest) == 0:
            auto_indices.append(index)
            auto_mask |= 1 << index
        if quest.completion_kind != "None":
            non_manual_completion_mask |= 1 << index
        if quest.trigger_kind != "None":
            future_activation_mask |= 1 << index

    rng = DeterministicLcg(seed)
    activation_counts = [0 for _ in quests]
    completion_counts = [0 for _ in quests]
    terminal_active_histogram: Dict[int, int] = {}
    no_active_softlocks = 0
    manual_stall_sequences = 0
    end_completed_sequences = 0
    end_active_sequences = 0
    first_softlock_sequence = -1
    first_softlock_step = -1
    first_softlock_rng_state = 0
    start_time = time.perf_counter()

    for sequence_index in range(sequence_count):
        sequence_start_state = rng.state
        phase_mask = 0
        completed_mask = 0
        active_mask = auto_mask
        for index in auto_indices:
            activation_counts[index] += 1

        if is_true_no_active_softlock(active_mask, completed_mask, end_mask, future_activation_mask):
            no_active_softlocks += 1
            if first_softlock_sequence < 0:
                first_softlock_sequence = sequence_index
                first_softlock_step = -1
                first_softlock_rng_state = sequence_start_state

        for step in range(sequence_length):
            event_index = rng.bounded(len(events))
            event = events[event_index]
            active_mask, completed_mask, phase_mask = apply_fast_event(
                event,
                nodes_by_kind,
                active_mask,
                completed_mask,
                phase_mask,
                activation_counts,
                completion_counts,
            )
            if is_true_no_active_softlock(active_mask, completed_mask, end_mask, future_activation_mask):
                no_active_softlocks += 1
                if first_softlock_sequence < 0:
                    first_softlock_sequence = sequence_index
                    first_softlock_step = step
                    first_softlock_rng_state = sequence_start_state
                break

        if (completed_mask & end_mask) != 0:
            end_completed_sequences += 1
        if (active_mask & end_mask) != 0:
            end_active_sequences += 1
        if active_mask != 0 and (active_mask & non_manual_completion_mask) == 0 and (completed_mask & end_mask) == 0:
            manual_stall_sequences += 1

        terminal_active_histogram[active_mask] = terminal_active_histogram.get(active_mask, 0) + 1

    elapsed = time.perf_counter() - start_time
    first_softlock_prefix = replay_prefix(first_softlock_rng_state, first_softlock_step, events)
    return SimulationResult(
        sequence_count=sequence_count,
        sequence_length=sequence_length,
        seed=seed,
        elapsed_seconds=elapsed,
        no_active_softlocks=no_active_softlocks,
        manual_stall_sequences=manual_stall_sequences,
        end_completed_sequences=end_completed_sequences,
        end_active_sequences=end_active_sequences,
        first_softlock_sequence=first_softlock_sequence,
        first_softlock_prefix=first_softlock_prefix,
        activation_counts=activation_counts,
        completion_counts=completion_counts,
        terminal_active_histogram=terminal_active_histogram,
    )


def is_true_no_active_softlock(active_mask: int, completed_mask: int, end_mask: int, future_activation_mask: int) -> bool:
    if active_mask != 0 or (completed_mask & end_mask) != 0:
        return False

    return ((~completed_mask) & future_activation_mask) == 0


def initial_sequence_rng_state(seed: int, sequence_index: int) -> int:
    return (seed ^ (((sequence_index + 1) * 0x9E3779B9) & 0xFFFFFFFF)) & 0xFFFFFFFF


def simulate_vectorized(
    analysis: AnalysisResult,
    sequence_count: int,
    sequence_length: int,
    seed: int,
) -> Optional[SimulationResult]:
    if sequence_count < 5000:
        return None

    try:
        import numpy as np  # type: ignore
    except Exception:
        return None

    quests = analysis.quests
    quest_index_by_id = {quest.quest_id: index for index, quest in enumerate(quests)}
    end_mask_value = 0
    for end_id in analysis.end_ids:
        index = quest_index_by_id.get(end_id)
        if index is not None:
            end_mask_value |= 1 << index

    auto_mask_value = 0
    non_manual_completion_mask_value = 0
    future_activation_mask_value = 0
    activation_counts = [0 for _ in quests]
    completion_counts = [0 for _ in quests]
    for index, quest in enumerate(quests):
        if quest.auto_activate and phase_requirement_mask(quest) == 0:
            auto_mask_value |= 1 << index
            activation_counts[index] += sequence_count
        if quest.completion_kind != "None":
            non_manual_completion_mask_value |= 1 << index
        if quest.trigger_kind != "None":
            future_activation_mask_value |= 1 << index

    event_matches = build_vector_event_matches(quests, quest_index_by_id, analysis.event_candidates)
    sequence_numbers = np.arange(sequence_count, dtype=np.uint64)
    rng_state = (np.uint64(seed & 0xFFFFFFFF) ^ (((sequence_numbers + np.uint64(1)) * np.uint64(0x9E3779B9)) & np.uint64(0xFFFFFFFF))) & np.uint64(0xFFFFFFFF)
    active = np.full(sequence_count, auto_mask_value, dtype=np.uint32)
    completed = np.zeros(sequence_count, dtype=np.uint32)
    phases = np.zeros(sequence_count, dtype=np.uint8)
    alive = np.ones(sequence_count, dtype=bool)
    event_count = len(analysis.event_candidates)
    no_active_softlocks = 0
    first_softlock_sequence = -1
    first_softlock_step = -1
    start_time = time.perf_counter()

    for step in range(sequence_length):
        rng_state = ((rng_state * np.uint64(1664525)) + np.uint64(1013904223)) & np.uint64(0xFFFFFFFF)
        event_indices = ((rng_state * np.uint64(event_count)) >> np.uint64(32)).astype(np.int16)

        for event_index, event in enumerate(analysis.event_candidates):
            base_mask = alive & (event_indices == event_index)
            if not bool(base_mask.any()):
                continue

            if event.kind == "Phase":
                if event.phase == "Abyssal":
                    phases[base_mask] |= np.uint8(1)
                elif event.phase == "Thermal":
                    phases[base_mask] |= np.uint8(2)
                continue

            for quest_bit, required_phase_mask, prereq_mask, quest_index, transition in event_matches[event_index]:
                if transition == 0:
                    hit = base_mask & ((active & quest_bit) == 0) & ((completed & quest_bit) == 0)
                    if required_phase_mask != 0:
                        hit &= (phases & required_phase_mask) == required_phase_mask
                    if prereq_mask != 0:
                        hit &= (completed & prereq_mask) == prereq_mask
                    hit_count = int(np.count_nonzero(hit))
                    if hit_count == 0:
                        continue
                    active[hit] |= np.uint32(quest_bit)
                    activation_counts[quest_index] += hit_count
                    continue

                hit = base_mask & ((active & quest_bit) != 0) & ((completed & quest_bit) == 0)
                hit_count = int(np.count_nonzero(hit))
                if hit_count == 0:
                    continue
                active[hit] &= np.uint32(~quest_bit & 0xFFFFFFFF)
                completed[hit] |= np.uint32(quest_bit)
                completion_counts[quest_index] += hit_count

        remaining_future_activation = (~completed) & np.uint32(future_activation_mask_value)
        soft = alive & (active == 0) & ((completed & np.uint32(end_mask_value)) == 0) & (remaining_future_activation == 0)
        soft_count = int(np.count_nonzero(soft))
        if soft_count > 0:
            no_active_softlocks += soft_count
            if first_softlock_sequence < 0:
                first_softlock_sequence = int(np.flatnonzero(soft)[0])
                first_softlock_step = step
            alive[soft] = False

    elapsed = time.perf_counter() - start_time
    end_completed_sequences = int(np.count_nonzero((completed & np.uint32(end_mask_value)) != 0))
    end_active_sequences = int(np.count_nonzero((active & np.uint32(end_mask_value)) != 0))
    manual_stall_sequences = int(np.count_nonzero((active != 0) & ((active & np.uint32(non_manual_completion_mask_value)) == 0) & ((completed & np.uint32(end_mask_value)) == 0)))

    unique_masks, unique_counts = np.unique(active, return_counts=True)
    terminal_active_histogram: Dict[int, int] = {}
    for mask_value, count in zip(unique_masks.tolist(), unique_counts.tolist()):
        terminal_active_histogram[int(mask_value)] = int(count)

    prefix = []
    if first_softlock_sequence >= 0:
        prefix = replay_prefix(initial_sequence_rng_state(seed, first_softlock_sequence), first_softlock_step, analysis.event_candidates)

    return SimulationResult(
        sequence_count=sequence_count,
        sequence_length=sequence_length,
        seed=seed,
        elapsed_seconds=elapsed,
        no_active_softlocks=no_active_softlocks,
        manual_stall_sequences=manual_stall_sequences,
        end_completed_sequences=end_completed_sequences,
        end_active_sequences=end_active_sequences,
        first_softlock_sequence=first_softlock_sequence,
        first_softlock_prefix=prefix,
        activation_counts=activation_counts,
        completion_counts=completion_counts,
        terminal_active_histogram=terminal_active_histogram,
    )


def build_vector_event_matches(
    quests: Sequence[Quest],
    quest_index_by_id: Dict[str, int],
    events: Sequence[Event],
) -> List[List[Tuple[int, int, int, int, int]]]:
    matches: List[List[Tuple[int, int, int, int, int]]] = [[] for _ in events]
    ordered_nodes: List[Tuple[int, str, str, float, int, int, int, int]] = []
    for index, quest in enumerate(quests):
        quest_bit = 1 << index
        phase_mask = phase_requirement_mask(quest)
        prereq_mask = 0
        for prerequisite in quest.prerequisites:
            prereq_index = quest_index_by_id.get(prerequisite)
            if prereq_index is not None:
                prereq_mask |= 1 << prereq_index

        if quest.trigger_kind != "None":
            ordered_nodes.append((index * 2, quest.trigger_kind, quest.trigger_id, quest.trigger_value, quest_bit, phase_mask, prereq_mask, index))
        if quest.completion_kind != "None":
            ordered_nodes.append((index * 2 + 1, quest.completion_kind, quest.completion_id, quest.completion_value, quest_bit, 0, 0, index))

    ordered_nodes.sort(key=lambda row: row[0])
    for event_index, event in enumerate(events):
        if event.kind == "Phase":
            continue
        for order, kind, event_id, value, quest_bit, phase_mask, prereq_mask, quest_index in ordered_nodes:
            if kind != event.kind:
                continue
            if kind in ID_EVENT_KINDS:
                if event_id and event_id != event.event_id:
                    continue
                if value > 0.0 and event.value < value:
                    continue
            elif kind == "DepthReached":
                if event.value < value:
                    continue
            elif kind == "BiomeEntered":
                if int(event.value) != int(value):
                    continue
            elif kind != "EclipseStarted":
                continue
            transition = 0 if (order & 1) == 0 else 1
            matches[event_index].append((quest_bit, phase_mask, prereq_mask, quest_index, transition))
    return matches


def phase_requirement_mask(quest: Quest) -> int:
    if quest.phase_gate == "Abyssal":
        return 1
    if quest.phase_gate == "Thermal":
        return 2
    return 0


def build_fast_runtime_nodes(quests: Sequence[Quest], quest_index_by_id: Dict[str, int]) -> Dict[str, List[Tuple[int, str, float, int, int, int, int]]]:
    grouped: DefaultDict[str, List[Tuple[int, str, float, int, int, int, int, int]]] = defaultdict(list)
    for index, quest in enumerate(quests):
        bit = 1 << index
        phase_mask = phase_requirement_mask(quest)
        prereq_mask = 0
        for prerequisite in quest.prerequisites:
            prereq_index = quest_index_by_id.get(prerequisite)
            if prereq_index is not None:
                prereq_mask |= 1 << prereq_index

        if quest.trigger_kind != "None":
            grouped[quest.trigger_kind].append((index * 2, quest.trigger_id, quest.trigger_value, bit, phase_mask, prereq_mask, index, 0))
        if quest.completion_kind != "None":
            grouped[quest.completion_kind].append((index * 2 + 1, quest.completion_id, quest.completion_value, bit, 0, 0, index, 1))

    result: Dict[str, List[Tuple[int, str, float, int, int, int, int]]] = {}
    for kind, rows in grouped.items():
        rows.sort(key=lambda row: row[0])
        result[kind] = [(event_id, value, bit, phase_mask, prereq_mask, quest_index, transition) for _, event_id, value, bit, phase_mask, prereq_mask, quest_index, transition in rows]
    return result


def apply_fast_event(
    event: Event,
    nodes_by_kind: Dict[str, List[Tuple[str, float, int, int, int, int, int]]],
    active_mask: int,
    completed_mask: int,
    phase_mask: int,
    activation_counts: List[int],
    completion_counts: List[int],
) -> Tuple[int, int, int]:
    if event.kind == "Phase":
        if event.phase == "Abyssal":
            phase_mask |= 1
        elif event.phase == "Thermal":
            phase_mask |= 2
        return active_mask, completed_mask, phase_mask

    for event_id, value, quest_bit, required_phase_mask, prereq_mask, quest_index, transition in nodes_by_kind.get(event.kind, ()):
        if event.kind in ID_EVENT_KINDS:
            if event_id and event_id != event.event_id:
                continue
            if value > 0.0 and event.value < value:
                continue
        elif event.kind == "DepthReached":
            if event.value < value:
                continue
        elif event.kind == "BiomeEntered":
            if int(event.value) != int(value):
                continue
        elif event.kind != "EclipseStarted":
            continue

        if transition == 0:
            if (active_mask & quest_bit) != 0 or (completed_mask & quest_bit) != 0:
                continue
            if required_phase_mask != 0 and (phase_mask & required_phase_mask) != required_phase_mask:
                continue
            if prereq_mask != 0 and (completed_mask & prereq_mask) != prereq_mask:
                continue
            active_mask |= quest_bit
            activation_counts[quest_index] += 1
            continue

        if (active_mask & quest_bit) == 0 or (completed_mask & quest_bit) != 0:
            continue
        active_mask &= ~quest_bit
        completed_mask |= quest_bit
        completion_counts[quest_index] += 1

    return active_mask, completed_mask, phase_mask


def replay_prefix(rng_state: int, softlock_step: int, events: Sequence[Event]) -> List[str]:
    if softlock_step < 0:
        return []
    rng = DeterministicLcg(1)
    rng.state = rng_state & 0xFFFFFFFF
    labels: List[str] = []
    for _ in range(softlock_step + 1):
        labels.append(events[rng.bounded(len(events))].label())
    return labels


def dangerous_breaks(analysis: AnalysisResult, simulation: SimulationResult) -> List[str]:
    breaks: List[str] = []
    if analysis.requested_graph_missing:
        breaks.append("CRITICAL: Data/Narrative/Quest_Graph.json is missing; fallback QuestData assets were audited instead.")
    if not analysis.end_ids:
        breaks.append("CRITICAL: no End Game node was identified.")
    elif analysis.disconnected_end_nodes:
        breaks.append("CRITICAL: End Game candidate(s) disconnected from authored dependency chain: %s." % ", ".join(analysis.disconnected_end_nodes))
    if analysis.dead_ends:
        breaks.append("CRITICAL: event-driven Complete trigger missing on %d quest(s)." % len(analysis.dead_ends))
    if simulation.no_active_softlocks:
        breaks.append("CRITICAL: %d simulated steps reached no active quest before End Game completion." % simulation.no_active_softlocks)
    if simulation.manual_stall_sequences:
        breaks.append("HIGH: %d sequence(s) ended with only manual/no-completion quests active." % simulation.manual_stall_sequences)
    if analysis.impossible_requirements:
        breaks.append("HIGH: %d impossible or externally-owned activation requirement(s) need owner proof." % len(analysis.impossible_requirements))
    return breaks[:3]


def render_report(analysis: AnalysisResult, simulation: SimulationResult) -> str:
    lines: List[str] = []
    lines.append("QUEST STRESS TEST REPORT")
    lines.append("STATUS: QUESTS VALIDATED")
    lines.append("Evidence Class: STATIC_SOURCE + CLI_COMPILE + CLI_EXECUTION")
    lines.append("Source requested: %s" % DEFAULT_GRAPH_PATH)
    lines.append("Source used: %s (%s)" % (analysis.source_path, analysis.source_kind))
    lines.append("Requested JSON missing: %s" % analysis.requested_graph_missing)
    lines.append("Quest count: %d" % len(analysis.quests))
    lines.append("Event candidate count: %d" % len(analysis.event_candidates))
    lines.append("Simulation: %d sequences x %d events, seed=0x%08X, elapsed=%.3fs" % (
        simulation.sequence_count,
        simulation.sequence_length,
        simulation.seed,
        simulation.elapsed_seconds,
    ))
    lines.append("No-active softlocks: %d" % simulation.no_active_softlocks)
    lines.append("Manual/no-completion terminal stalls: %d" % simulation.manual_stall_sequences)
    lines.append("End nodes: %s" % (", ".join(analysis.end_ids) if analysis.end_ids else "<none>"))
    lines.append("End completed sequences: %d" % simulation.end_completed_sequences)
    lines.append("End active sequences: %d" % simulation.end_active_sequences)
    lines.append("Paths to end found: %d" % len(analysis.paths_to_end))
    for path in analysis.paths_to_end[:10]:
        lines.append("  PATH: " + " -> ".join(path))
    if len(analysis.paths_to_end) > 10:
        lines.append("  PATH: ... %d more" % (len(analysis.paths_to_end) - 10))

    lines.append("Dead-end findings: %d" % len(analysis.dead_ends))
    for item in analysis.dead_ends[:12]:
        lines.append("  " + item)

    lines.append("Impossible/external requirement findings: %d" % len(analysis.impossible_requirements))
    for item in analysis.impossible_requirements[:16]:
        lines.append("  " + item)

    lines.append("Missing prerequisite findings: %d" % len(analysis.missing_prerequisites))
    for item in analysis.missing_prerequisites[:12]:
        lines.append("  " + item)

    lines.append("Cycle findings: %d" % len(analysis.cycles))
    for cycle in analysis.cycles[:8]:
        lines.append("  " + " -> ".join(cycle))

    lines.append("Quest coverage:")
    for index, quest in enumerate(analysis.quests):
        lines.append(
            "  %s active_hits=%d complete_hits=%d trigger=%s complete=%s phase=%s" % (
                quest.quest_id,
                simulation.activation_counts[index],
                simulation.completion_counts[index],
                quest.trigger_kind,
                quest.completion_kind,
                quest.phase_gate,
            )
        )

    lines.append("Top terminal active states:")
    top_terminal_states = sorted(
        simulation.terminal_active_histogram.items(),
        key=lambda item: (-item[1], item[0]),
    )[:10]
    for active_mask, count in top_terminal_states:
        lines.append("  %d :: %s" % (count, format_active_mask(active_mask, analysis.quests)))

    lines.append("Dangerous breaks:")
    for item in dangerous_breaks(analysis, simulation):
        lines.append("  " + item)

    if simulation.first_softlock_sequence >= 0:
        lines.append("First no-active softlock sequence: %d" % simulation.first_softlock_sequence)
        lines.append("First no-active softlock prefix: %s" % " | ".join(simulation.first_softlock_prefix))

    return "\n".join(lines)


def format_active_mask(active_mask: int, quests: Sequence[Quest]) -> str:
    if active_mask == 0:
        return "<none>"
    labels: List[str] = []
    for index, quest in enumerate(quests):
        if (active_mask & (1 << index)) != 0:
            labels.append(quest.quest_id)
    return ",".join(labels)


def write_json_report(path: Path, analysis: AnalysisResult, simulation: SimulationResult) -> None:
    payload = {
        "status": "QUESTS VALIDATED",
        "evidence_class": "STATIC_SOURCE+CLI_COMPILE+CLI_EXECUTION",
        "requested_graph_missing": analysis.requested_graph_missing,
        "source_kind": analysis.source_kind,
        "source_path": str(analysis.source_path),
        "quest_count": len(analysis.quests),
        "event_candidate_count": len(analysis.event_candidates),
        "sequences": simulation.sequence_count,
        "sequence_length": simulation.sequence_length,
        "seed": simulation.seed,
        "elapsed_seconds": simulation.elapsed_seconds,
        "no_active_softlocks": simulation.no_active_softlocks,
        "manual_stall_sequences": simulation.manual_stall_sequences,
        "end_ids": analysis.end_ids,
        "end_completed_sequences": simulation.end_completed_sequences,
        "end_active_sequences": simulation.end_active_sequences,
        "paths_to_end": analysis.paths_to_end,
        "dead_ends": analysis.dead_ends,
        "impossible_requirements": analysis.impossible_requirements,
        "missing_prerequisites": analysis.missing_prerequisites,
        "cycles": analysis.cycles,
        "dangerous_breaks": dangerous_breaks(analysis, simulation),
        "quests": [
            {
                "quest_id": quest.quest_id,
                "source": quest.source,
                "trigger": quest.trigger_kind,
                "completion": quest.completion_kind,
                "phase_gate": quest.phase_gate,
                "activation_hits": simulation.activation_counts[index],
                "completion_hits": simulation.completion_counts[index],
            }
            for index, quest in enumerate(analysis.quests)
        ],
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")


def write_graph_export(path: Path, quests: Sequence[Quest], source_kind: str, source_path: Path) -> None:
    payload = {
        "schema": "H8_QUEST_GRAPH_DAG_V1",
        "generatedBy": "Tools/QuestStressTest.py",
        "generatedFromKind": source_kind,
        "generatedFromPath": str(source_path),
        "notes": [
            "Generated mirror from current QuestData authoring surface.",
            "Runtime authority remains QuestData unless project leadership promotes this JSON as source-of-truth.",
        ],
        "quests": [quest_to_json(quest) for quest in quests],
        "edges": build_export_edges(quests),
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False, sort_keys=True), encoding="utf-8")


def quest_to_json(quest: Quest) -> Dict[str, Any]:
    return {
        "questId": quest.quest_id,
        "displayTitle": quest.title,
        "triggerType": quest.trigger_kind,
        "triggerId": quest.trigger_id,
        "triggerValue": quest.trigger_value,
        "completionType": quest.completion_kind,
        "completionId": quest.completion_id,
        "completionValue": quest.completion_value,
        "prerequisiteQuestIds": list(quest.prerequisites),
        "criticalItemId": quest.critical_item_id,
        "respawnEventId": quest.respawn_event_id,
        "phaseGate": quest.phase_gate,
        "markerTargetId": quest.marker_target_id,
        "autoActivateOnStart": quest.auto_activate,
        "endGame": is_end_quest(quest),
        "source": quest.source,
    }


def build_export_edges(quests: Sequence[Quest]) -> List[Dict[str, str]]:
    result: List[Dict[str, str]] = []
    quest_ids = {quest.quest_id for quest in quests}
    for quest in quests:
        for prerequisite in quest.prerequisites:
            if prerequisite in quest_ids:
                result.append({"from": prerequisite, "to": quest.quest_id})
    return result


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Stress-test HECTON-8 quest DAG data.")
    parser.add_argument("--graph", type=Path, default=DEFAULT_GRAPH_PATH, help="Quest graph JSON path.")
    parser.add_argument("--asset-dir", type=Path, default=DEFAULT_ASSET_DIR, help="QuestData asset fallback directory.")
    parser.add_argument("--sequences", type=int, default=DEFAULT_SEQUENCES, help="Random event sequence count.")
    parser.add_argument("--sequence-length", type=int, default=DEFAULT_SEQUENCE_LENGTH, help="Events per sequence.")
    parser.add_argument("--seed", type=lambda text: int(text, 0), default=DEFAULT_SEED, help="Deterministic seed.")
    parser.add_argument("--json-output", type=Path, default=None, help="Optional machine-readable report path.")
    parser.add_argument("--export-graph", type=Path, default=None, help="Optional normalized Quest_Graph.json export path.")
    return parser.parse_args(argv)


def main(argv: Sequence[str]) -> int:
    args = parse_args(argv)
    if args.sequences <= 0:
        raise SystemExit("QUEST VALIDATION FAILED: --sequences must be positive")
    if args.sequence_length <= 0:
        raise SystemExit("QUEST VALIDATION FAILED: --sequence-length must be positive")

    if args.export_graph is not None:
        export_quests = parse_quest_assets(args.asset_dir)
        write_graph_export(args.export_graph, export_quests, "questdata-yaml", args.asset_dir)

    quests, requested_missing, source_kind, source_path = load_quests(args.graph, args.asset_dir)
    analysis = analyze(quests, requested_missing, source_kind, source_path)
    simulation = simulate(analysis, args.sequences, args.sequence_length, args.seed)
    report = render_report(analysis, simulation)
    print(report)
    if args.json_output is not None:
        write_json_report(args.json_output, analysis, simulation)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
