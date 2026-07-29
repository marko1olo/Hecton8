#!/usr/bin/env python3
"""Derive the AppliedLore crosslink graph from authoring data that already exists.

WHY THIS EXISTS

    The corpus holds 694 packets. The navigation/evidence graph the exporter reads,
    `Docs/Lore/AppliedContent/graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv`, holds FIVE
    rows. Zero packet files carry their own `prereq_packet_ids` / `next_packet_ids`. So 689 packets have no
    edge to anything and the corpus is a pile of isolated pages rather than a network - which is the whole
    complaint `Docs/Lore/Lore_Crosslink_Graph.md` opens with: "make lore articles connect instead of
    becoming isolated wiki pages."

    Nothing needs inventing to fix that. The packets already assert their relationships through authored
    fields: `unlock.primary`, `unlock.secondary`, `unlock.poi_tags`, `unlock.biome_tags` and
    `release_set_id`. Two packets that hang off the same point of interest ARE related, and a packet whose
    `unlock.secondary` names another packet's `unlock.primary` is an explicit, authored dependency. This tool
    surfaces those as graph edges. It writes no prose, invents no lore fact, and adds no relationship the
    authoring data does not already claim.

EDGE KINDS, strongest first

    unlock_chain   packet A's `unlock.secondary` contains packet B's `unlock.primary`. An authored
                   dependency: A expects B to have happened. Directional - B is a prereq of A.
    shared_poi     two packets share a `poi_tags` entry. The strongest undirected signal, because a POI is a
                   physical place a player stands in. Tags shared by more than --max-poi-fanout packets are
                   skipped: at that width the tag is a category, not a location, and it would produce a
                   hairball instead of a route.
    shared_biome   two packets share a `biome_tags` entry. Weaker and much wider, so it is only used to
                   connect a packet that shared_poi left with no edge at all.

WHAT IT WILL NOT DO

    It never drops a hand-authored row. The five existing rows are read first and preserved verbatim,
    including their `truth_claim` and `player_decision` prose, which a generator has no business writing.
    Derived rows are marked in `arc_id` with a `derived_` prefix so a human can always tell which is which.
    `production_metadata` packets are excluded: they are specs, they do not publish, and they are not part of
    any player-facing route.

USAGE

    python -B Tools/AppliedLoreCrosslinkGraphBuilder.py --report        # measure, write nothing
    python -B Tools/AppliedLoreCrosslinkGraphBuilder.py --write
    python -B Tools/AppliedLoreCrosslinkGraphBuilder.py --check         # non-zero if the graph is stale
"""

from __future__ import annotations

import argparse
import csv
import glob
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

GRAPH_HEADERS = (
    "packet_id",
    "arc_id",
    "depth_band",
    "route_moment",
    "prereq_packet_ids",
    "next_packet_ids",
    "evidence_type",
    "truth_claim",
    "player_decision",
    "spoiler_tier",
    "primary_surface",
)

PACKET_GLOB = "Docs/Lore/AppliedContent/packets/*.json"
GRAPH_PATH = Path("Docs/Lore/AppliedContent/graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv")

# A biome is a whole depth band, so biome edges are a fallback only. A POI shared by more packets than this
# is behaving as a category rather than a place; linking all of them would bury the real routes.
DEFAULT_MAX_POI_FANOUT = 12
DEFAULT_MAX_EDGES_PER_PACKET = 4


def load_packets() -> list[dict]:
    out: list[dict] = []
    for path in sorted(glob.glob(PACKET_GLOB)):
        try:
            doc = json.loads(Path(path).read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            print(f"SKIP {path}: {exc}", file=sys.stderr)
            continue
        for packet in doc.get("packets", [doc]):
            if not isinstance(packet, dict) or "packet_id" not in packet:
                continue
            if str(packet.get("content_class", "")) == "production_metadata":
                continue
            unlock = packet.get("unlock") or {}
            if not isinstance(unlock, dict):
                unlock = {}
            surfaces = packet.get("surfaces") or []
            out.append(
                {
                    "packet_id": str(packet["packet_id"]),
                    "release_set_id": str(packet.get("release_set_id", "")),
                    "primary": str(unlock.get("primary", "")),
                    "secondary": [str(s) for s in (unlock.get("secondary") or [])],
                    "poi": [str(t) for t in (unlock.get("poi_tags") or [])],
                    "biome": [str(t) for t in (unlock.get("biome_tags") or [])],
                    "surfaces": [str(s) for s in surfaces],
                }
            )
    return out


def read_authored_rows() -> list[dict]:
    """Hand-authored rows are authority. Read them first so they can never be overwritten."""
    if not GRAPH_PATH.exists():
        return []
    with GRAPH_PATH.open("r", encoding="utf-8", newline="") as handle:
        return [row for row in csv.DictReader(handle) if (row.get("packet_id") or "").strip()]


def derive_edges(packets: list[dict], max_poi_fanout: int, max_edges: int) -> tuple[dict, dict, Counter]:
    by_id = {p["packet_id"]: p for p in packets}
    primary_owner: dict[str, list[str]] = defaultdict(list)
    for p in packets:
        if p["primary"]:
            primary_owner[p["primary"]].append(p["packet_id"])

    prereq: dict[str, list[str]] = defaultdict(list)
    nxt: dict[str, list[str]] = defaultdict(list)
    kinds: Counter = Counter()

    def link(a: str, b: str, kind: str) -> None:
        """b becomes a prereq of a, a becomes a next of b. Idempotent, self-loops refused.

        Only the INCOMING side is capped. Capping `nxt[b]` as well meant a popular anchor filled up and then
        silently blocked every later member of its chain - which left 105 packets isolated despite having
        perfectly good tags. A hub can legitimately precede many things; what must stay bounded is how many
        prerequisites any single packet claims, because that is what a reader has to walk.
        """
        if a == b or not a or not b:
            return
        if len(prereq[a]) >= max_edges:
            return
        if b not in prereq[a]:
            prereq[a].append(b)
        if a not in nxt[b]:
            nxt[b].append(a)
        kinds[kind] += 1

    # 1. Explicit authored dependency: A's unlock.secondary names B's unlock.primary.
    for p in packets:
        for sec in p["secondary"]:
            for owner in primary_owner.get(sec, ()):
                link(p["packet_id"], owner, "unlock_chain")

    # 2. Shared point of interest - a physical place two packets both attach to.
    poi_members: dict[str, list[str]] = defaultdict(list)
    for p in packets:
        for tag in p["poi"]:
            poi_members[tag].append(p["packet_id"])
    for tag, members in sorted(poi_members.items()):
        if not 1 < len(members) <= max_poi_fanout:
            continue
        ordered = sorted(members)
        # Chain rather than clique: a route the reader can walk, not a hairball.
        for left, right in zip(ordered, ordered[1:]):
            link(right, left, "shared_poi")

    # 3. Biome fallback, only for packets left with no edge at all.
    isolated = [p["packet_id"] for p in packets if not prereq[p["packet_id"]] and not nxt[p["packet_id"]]]
    if isolated:
        biome_members: dict[str, list[str]] = defaultdict(list)
        for p in packets:
            for tag in p["biome"]:
                biome_members[tag].append(p["packet_id"])
        isolated_set = set(isolated)
        for tag, members in sorted(biome_members.items()):
            ordered = sorted(members)
            if len(ordered) < 2:
                continue
            anchor = ordered[0]
            for pid in ordered[1:]:
                if pid in isolated_set and not prereq[pid] and not nxt[pid]:
                    link(pid, anchor, "shared_biome")

    return prereq, nxt, kinds, by_id


def build_rows(packets: list[dict], authored: list[dict], prereq, nxt, by_id) -> list[dict]:
    authored_ids = {(r.get("packet_id") or "").strip() for r in authored}
    rows: list[dict] = [dict(r) for r in authored]
    for p in sorted(packets, key=lambda item: item["packet_id"]):
        pid = p["packet_id"]
        if pid in authored_ids:
            continue
        if not prereq[pid] and not nxt[pid]:
            continue
        primary_surface = "external_site" if "external_site" in p["surfaces"] else (
            p["surfaces"][0] if p["surfaces"] else ""
        )
        rows.append(
            {
                "packet_id": pid,
                "arc_id": "derived_" + (p["release_set_id"] or "unassigned"),
                "depth_band": ";".join(p["biome"][:2]),
                "route_moment": p["primary"],
                "prereq_packet_ids": ";".join(prereq[pid]),
                "next_packet_ids": ";".join(nxt[pid]),
                "evidence_type": "derived_from_unlock_and_poi_tags",
                # Deliberately empty. A generator writing a truth_claim or a player_decision would be
                # inventing lore, which is exactly what this tool refuses to do. Hand-authored rows keep
                # theirs; derived rows leave them for a writer.
                "truth_claim": "",
                "player_decision": "",
                "spoiler_tier": "",
                "primary_surface": primary_surface,
            }
        )
    return rows


def write_rows(rows: list[dict]) -> None:
    GRAPH_PATH.parent.mkdir(parents=True, exist_ok=True)
    with GRAPH_PATH.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(GRAPH_HEADERS), lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow({k: row.get(k, "") for k in GRAPH_HEADERS})


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(description="Derive the AppliedLore crosslink graph from authored packet data.")
    ap.add_argument("--write", action="store_true", help="write the graph csv")
    ap.add_argument("--check", action="store_true", help="exit non-zero if the on-disk graph is stale")
    ap.add_argument("--report", action="store_true", help="measure only")
    ap.add_argument("--max-poi-fanout", type=int, default=DEFAULT_MAX_POI_FANOUT)
    ap.add_argument("--max-edges", type=int, default=DEFAULT_MAX_EDGES_PER_PACKET)
    args = ap.parse_args()

    packets = load_packets()
    authored = read_authored_rows()
    prereq, nxt, kinds, by_id = derive_edges(packets, args.max_poi_fanout, args.max_edges)
    rows = build_rows(packets, authored, prereq, nxt, by_id)

    connected = sum(1 for p in packets if prereq[p["packet_id"]] or nxt[p["packet_id"]])
    total_edges = sum(kinds.values())
    print("applied_lore_crosslink_graph")
    print(f"  in-world packets        : {len(packets)}")
    print(f"  hand-authored rows kept: {len(authored)}")
    print(f"  rows total             : {len(rows)}")
    print(f"  packets with an edge   : {connected}  ({100 * connected // max(len(packets), 1)}%)")
    print(f"  packets still isolated : {len(packets) - connected}")
    print(f"  edges derived          : {total_edges}")
    for kind, count in kinds.most_common():
        print(f"      {kind:14} {count}")

    if args.write:
        write_rows(rows)
        print(f"  WROTE {GRAPH_PATH.as_posix()}")
        return 0

    if args.check:
        existing = read_authored_rows()
        if len(existing) < len(rows):
            print(f"  STALE: on disk {len(existing)} rows, derivable {len(rows)}")
            return 1
        print("  graph is current")
        return 0

    return 0


if __name__ == "__main__":
    sys.exit(main())
