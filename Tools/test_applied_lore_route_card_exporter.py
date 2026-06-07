import csv
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreRouteCardExporter import (
    INPUT_HEADERS,
    check_route_card_export,
    export_route_cards,
    route_card_export_current,
    route_card_output_path,
)


def write_csv(path: Path, headers: tuple[str, ...], rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def make_repo(root: Path) -> None:
    packet_csv = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
    write_csv(packet_csv, ("packet_id",), [{"packet_id": "P_TEST_ROUTE"}, {"packet_id": "P_REQ_ROUTE"}])
    write_csv(
        root / "Docs" / "Lore" / "AppliedContent" / "route_cards" / "RS_TEST_route_cards.csv",
        INPUT_HEADERS,
        [
            {
                "route_card_id": "RC_TEST_ROUTE",
                "phase_id": "test_phase",
                "depth_min_m": "0",
                "depth_max_m": "10",
                "packet_ids": "P_TEST_ROUTE",
                "required_packet_ids": "P_REQ_ROUTE",
                "primary_surface": "terminal",
                "world_object_hint": "poi.test",
                "player_question": "What proves the route?",
                "truth_payload": "The route has one owner and one prerequisite.",
                "replay_axis": "test_axis",
                "ending_pressure": "truth",
            }
        ],
    )


class TestAppliedLoreRouteCardExporter(unittest.TestCase):
    def test_dry_run_validates_without_writing(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_repo(root)

            count = export_route_cards(root, dry_run=True)

            self.assertEqual(count, 1)
            self.assertFalse(route_card_output_path(root).exists())

    def test_export_then_check_current(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_repo(root)

            self.assertEqual(export_route_cards(root), 1)
            self.assertEqual(check_route_card_export(root), 1)
            self.assertEqual(route_card_export_current(root), (1, True))

    def test_check_rejects_stale_export(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_repo(root)
            output_path = route_card_output_path(root)
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_text("stale\n", encoding="utf-8")

            with self.assertRaises(ValueError):
                check_route_card_export(root)


if __name__ == "__main__":
    unittest.main()
