import csv
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import TARGET_LOCALES, fnv1a32
from AppliedLorePacketCoverageAudit import AppliedLoreCoverageError, audit_selected_packets
from AppliedLorePageExporter import PUBLICATION_INDEX_HEADERS, publication_surface_rows
from AppliedLoreRouteCardExporter import INPUT_HEADERS as ROUTE_CARD_HEADERS
from AppliedLoreRouteCardExporter import export_route_cards


def localized_row():
    return {
        "title": "Title",
        "scanner": "Scanner text",
        "terminal": "Terminal text",
        "audio": "Audio text",
        "in_game_wiki": "Wiki body",
        "external_site": "Site body",
    }


def write_csv(path: Path, headers: tuple[str, ...], rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def make_repo(root: Path) -> None:
    base = root / "Docs" / "Lore" / "AppliedContent"
    packet = {
        "packet_id": "P_TEST_COVERAGE",
        "release_set_id": "RS_TEST",
        "article_id": "test.coverage",
        "unlock_id": "unlock.test",
        "surface_mask": 48,
        "localized": {locale: localized_row() for locale in TARGET_LOCALES},
    }
    packet_dir = base / "packets"
    packet_dir.mkdir(parents=True, exist_ok=True)
    packet_path = packet_dir / "RS_TEST.packets.json"
    packet_path.write_text(
        json.dumps({"release_set_id": "RS_TEST", "packets": [packet]}),
        encoding="utf-8",
    )

    packet_rows = []
    for locale in TARGET_LOCALES:
        packet_rows.append(
            {
                "packet_id": "P_TEST_COVERAGE",
                "locale": locale,
                "release_set_id": "RS_TEST",
                "article_id": "test.coverage",
                "unlock_id": "unlock.test",
                "surface_mask": "48",
                "title": "Title",
                "scanner": "Scanner text",
                "terminal": "Terminal text",
                "audio": "Audio text",
                "in_game_wiki": "Wiki body",
                "external_site": "Site body",
                "field_note": "",
                "poi_tags": "",
                "biome_tags": "",
                "flags": "0",
            }
        )
    write_csv(
        root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv",
        (
            "packet_id",
            "locale",
            "release_set_id",
            "article_id",
            "unlock_id",
            "surface_mask",
            "title",
            "scanner",
            "terminal",
            "audio",
            "in_game_wiki",
            "external_site",
            "field_note",
            "poi_tags",
            "biome_tags",
            "flags",
        ),
        packet_rows,
    )

    publication_packet = dict(packet)
    publication_packet["_source_path"] = str(packet_path.resolve())
    publication_rows = publication_surface_rows(base, [publication_packet])
    write_csv(base / "Publication_Surface_Index.csv", PUBLICATION_INDEX_HEADERS, publication_rows)
    for row in publication_rows:
        page_path = base / row["page_path"]
        page_path.parent.mkdir(parents=True, exist_ok=True)
        page_path.write_text("---\npacket_id: P_TEST_COVERAGE\n---\nBody\n", encoding="utf-8")

    packet_hash = fnv1a32("P_TEST_COVERAGE")
    write_csv(
        base / "binding_maps" / "RS_TEST_runtime_binding_map.csv",
        (
            "packet_id",
            "packet_hash_hex",
            "packet_hash_uint",
            "release_set",
            "primary_component",
            "primary_field",
            "secondary_component",
            "secondary_field",
            "suggested_world_target",
            "unlock_moment",
            "notes",
        ),
        [
            {
                "packet_id": "P_TEST_COVERAGE",
                "packet_hash_hex": f"0x{packet_hash:08X}",
                "packet_hash_uint": str(packet_hash),
                "release_set": "RS_TEST",
                "primary_component": "NarrativeDiscovery",
                "primary_field": "appliedLorePacketHash",
                "secondary_component": "",
                "secondary_field": "",
                "suggested_world_target": "poi.test",
                "unlock_moment": "test",
                "notes": "test",
            }
        ],
    )
    write_csv(
        base / "graphs" / "RS_TEST_evidence_graph.csv",
        (
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
        ),
        [
            {
                "packet_id": "P_TEST_COVERAGE",
                "arc_id": "test_arc",
                "depth_band": "0-10m",
                "route_moment": "test_moment",
                "prereq_packet_ids": "",
                "next_packet_ids": "",
                "evidence_type": "test proof",
                "truth_claim": "Test claim.",
                "player_decision": "Test question?",
                "spoiler_tier": "0",
                "primary_surface": "in_game_wiki",
            }
        ],
    )
    write_csv(
        base / "route_cards" / "RS_TEST_route_cards.csv",
        ROUTE_CARD_HEADERS,
        [
            {
                "route_card_id": "RC_TEST_COVERAGE",
                "phase_id": "test_phase",
                "depth_min_m": "0",
                "depth_max_m": "10",
                "packet_ids": "P_TEST_COVERAGE",
                "required_packet_ids": "",
                "primary_surface": "in_game_wiki",
                "world_object_hint": "poi.test",
                "player_question": "Test question?",
                "truth_payload": "Test claim.",
                "replay_axis": "test_axis",
                "ending_pressure": "none",
            }
        ],
    )
    export_route_cards(root)


class TestAppliedLorePacketCoverageAudit(unittest.TestCase):
    def test_complete_packet_coverage_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_repo(root)

            stats = audit_selected_packets(root, ("P_TEST_COVERAGE",))

            self.assertEqual(stats.target_packets, 1)
            self.assertEqual(stats.baked_rows, len(TARGET_LOCALES))
            self.assertEqual(stats.publication_rows, len(TARGET_LOCALES) * 2)
            self.assertEqual(stats.binding_rows, 1)
            self.assertEqual(stats.graph_rows, 1)
            self.assertEqual(stats.route_cards, 1)
            self.assertEqual(stats.route_source_rows, 1)

    def test_missing_binding_map_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_repo(root)
            (root / "Docs" / "Lore" / "AppliedContent" / "binding_maps" / "RS_TEST_runtime_binding_map.csv").unlink()

            with self.assertRaises(AppliedLoreCoverageError):
                audit_selected_packets(root, ("P_TEST_COVERAGE",))


if __name__ == "__main__":
    unittest.main()
