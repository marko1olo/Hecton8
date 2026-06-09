import csv
import io
import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import fnv1a32
from AppliedLoreRouteCardExporter import (
    INPUT_HEADERS,
    check_selected_route_card_export,
    check_route_card_export,
    export_route_cards,
    main,
    selected_route_card_export_current,
    route_card_export_current,
    route_card_output_path,
)


def write_csv(path: Path, headers: tuple[str, ...], rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def route_row(**overrides: str) -> dict[str, str]:
    row = {
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
    row.update(overrides)
    return row


def write_route_cards(root: Path, rows: list[dict[str, str]]) -> None:
    write_csv(
        root / "Docs" / "Lore" / "AppliedContent" / "route_cards" / "RS_TEST_route_cards.csv",
        INPUT_HEADERS,
        rows,
    )


def write_route_card(root: Path, row: dict[str, str]) -> None:
    write_route_cards(root, [row])


def make_repo(root: Path) -> None:
    packet_csv = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
    write_csv(packet_csv, ("packet_id",), [{"packet_id": "P_TEST_ROUTE"}, {"packet_id": "P_REQ_ROUTE"}])
    write_route_card(root, route_row())


class TestAppliedLoreRouteCardExporter(unittest.TestCase):
    def test_dry_run_validates_without_writing(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)

            count = export_route_cards(root, dry_run=True)

            self.assertEqual(count, 1)
            self.assertFalse(route_card_output_path(root).exists())

    def test_export_then_check_current(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)

            self.assertEqual(export_route_cards(root), 1)
            self.assertEqual(check_route_card_export(root), 1)
            self.assertEqual(route_card_export_current(root), (1, True))
            self.assertEqual(check_selected_route_card_export(root, "P_TEST_ROUTE"), 1)
            self.assertEqual(selected_route_card_export_current(root, "P_TEST_ROUTE"), (1, True))

    def test_check_rejects_stale_export(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            output_path = route_card_output_path(root)
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_text("stale\n", encoding="utf-8")

            with self.assertRaises(ValueError):
                check_route_card_export(root)
            with self.assertRaises(ValueError):
                check_selected_route_card_export(root, "P_TEST_ROUTE")

    def test_selected_check_ignores_unrelated_broken_route_card(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            self.assertEqual(export_route_cards(root), 1)
            write_csv(
                root / "Docs" / "Lore" / "AppliedContent" / "route_cards" / "RS_UNRELATED_route_cards.csv",
                INPUT_HEADERS,
                [
                    route_row(
                        route_card_id="RC_UNRELATED_BROKEN",
                        packet_ids="P_UNRELATED",
                        required_packet_ids=";".join(f"P_EXTRA_{index}" for index in range(5)),
                    )
                ],
            )

            self.assertEqual(check_selected_route_card_export(root, "P_TEST_ROUTE"), 1)
            with self.assertRaisesRegex(ValueError, "required_packet_ids exceeds capacity"):
                export_route_cards(root, dry_run=True)

    def test_rejects_non_integer_depth(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(depth_min_m="surface"))

            with self.assertRaisesRegex(ValueError, r"RS_TEST_route_cards\.csv:2.*invalid depth_min_m"):
                export_route_cards(root, dry_run=True)

    def test_rejects_inverted_depth_range(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(depth_min_m="20", depth_max_m="10"))

            with self.assertRaisesRegex(ValueError, "invalid depth bounds"):
                export_route_cards(root, dry_run=True)

    def test_rejects_title_as_primary_route_surface(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(primary_surface="title"))

            with self.assertRaisesRegex(ValueError, "unsupported primary_surface"):
                export_route_cards(root, dry_run=True)

    def test_rejects_unsupported_ending_pressure(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(ending_pressure="maybe"))

            with self.assertRaisesRegex(ValueError, "unsupported ending_pressure"):
                export_route_cards(root, dry_run=True)

    def test_accepts_quarantine_hold_ending_pressure(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(ending_pressure="quarantine_hold"))

            count = export_route_cards(root)

            with route_card_output_path(root).open("r", encoding="utf-8", newline="") as handle:
                rows = list(csv.DictReader(handle))
            self.assertEqual(count, 1)
            self.assertEqual(rows[0]["ending_pressure"], "quarantine_hold")
            self.assertEqual(rows[0]["ending_pressure_hash_uint"], str(fnv1a32("quarantine_hold")))

    def test_rejects_empty_packet_refs(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(packet_ids=" ; "))

            with self.assertRaisesRegex(ValueError, "packet_ids is empty"):
                export_route_cards(root, dry_run=True)

    def test_rejects_packet_ref_capacity_overflow(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(packet_ids=";".join(f"P_EXTRA_{index}" for index in range(9))))

            with self.assertRaisesRegex(ValueError, "packet_ids exceeds capacity"):
                export_route_cards(root, dry_run=True)

    def test_rejects_prerequisite_ref_capacity_overflow(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(
                root,
                route_row(required_packet_ids=";".join(f"P_EXTRA_{index}" for index in range(5))),
            )

            with self.assertRaisesRegex(ValueError, "required_packet_ids exceeds capacity"):
                export_route_cards(root, dry_run=True)

    def test_prunes_unknown_packet_refs_from_runtime_export(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_cards(
                root,
                [
                    route_row(route_card_id="RC_UNKNOWN_PRIMARY", packet_ids="P_TEST_ROUTE;P_UNKNOWN_PRIMARY"),
                    route_row(
                        route_card_id="RC_UNKNOWN_REQUIRED",
                        packet_ids="P_REQ_ROUTE",
                        required_packet_ids="P_UNKNOWN_REQUIRED",
                    ),
                ],
            )

            self.assertEqual(export_route_cards(root), 2)
            text = route_card_output_path(root).read_text(encoding="utf-8")
            self.assertIn("RC_UNKNOWN_PRIMARY", text)
            self.assertIn("RC_UNKNOWN_REQUIRED", text)
            self.assertNotIn("P_UNKNOWN_PRIMARY", text)
            self.assertNotIn("P_UNKNOWN_REQUIRED", text)

    def test_skips_draft_route_card_without_baked_owner_packet(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_cards(
                root,
                [
                    route_row(),
                    route_row(
                        route_card_id="RC_DRAFT_ROUTE",
                        packet_ids="P_DRAFT_ROUTE",
                        required_packet_ids="P_DRAFT_PREREQ",
                    ),
                ],
            )

            self.assertEqual(export_route_cards(root), 1)
            text = route_card_output_path(root).read_text(encoding="utf-8")
            self.assertIn("RC_TEST_ROUTE", text)
            self.assertNotIn("RC_DRAFT_ROUTE", text)

    def test_main_dry_run_prunes_unknown_refs_without_traceback(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(packet_ids="P_TEST_ROUTE;P_UNKNOWN_PRIMARY"))
            output = io.StringIO()

            with patch(
                "sys.argv",
                [
                    "AppliedLoreRouteCardExporter.py",
                    "--root",
                    str(root),
                    "--dry-run",
                ],
            ), patch("sys.stdout", output):
                code = main()

            self.assertEqual(code, 0)
            self.assertIn("would_write=1", output.getvalue())
            self.assertNotIn("Traceback", output.getvalue())

    def test_rejects_self_prerequisite(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_card(root, route_row(required_packet_ids="P_TEST_ROUTE"))

            with self.assertRaisesRegex(ValueError, "depends on itself"):
                export_route_cards(root, dry_run=True)

    def test_rejects_prerequisite_cycle(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            make_repo(root)
            write_route_cards(
                root,
                [
                    route_row(
                        route_card_id="RC_TEST_ROUTE_A",
                        packet_ids="P_TEST_ROUTE",
                        required_packet_ids="P_REQ_ROUTE",
                    ),
                    route_row(
                        route_card_id="RC_TEST_ROUTE_B",
                        packet_ids="P_REQ_ROUTE",
                        required_packet_ids="P_TEST_ROUTE",
                    ),
                ],
            )

            with self.assertRaisesRegex(ValueError, "prerequisite cycle"):
                export_route_cards(root, dry_run=True)


if __name__ == "__main__":
    unittest.main()
