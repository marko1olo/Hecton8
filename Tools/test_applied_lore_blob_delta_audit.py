import contextlib
import io
import json
import sys
from test_temp_root import temporary_directory
import unittest
from dataclasses import dataclass
from pathlib import Path
from unittest.mock import patch

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

from AppliedLoreBlobDeltaAudit import (  # noqa: E402
    ArtifactState,
    BlobDeltaStats,
    combined_delta_to_json_payload,
    compute_blob_delta,
    compute_route_delta,
    main,
    render_delta,
    render_route_delta,
    RouteDeltaStats,
)
from AppliedLoreRuntimeAudit import AuditFailure  # noqa: E402


@dataclass(frozen=True)
class FakeRow:
    packet_id: str
    locale: str
    release_set_id: str
    line_number: int
    packet_hash: int
    locale_hash: int

    @property
    def key(self):
        return self.packet_hash, self.locale_hash


@dataclass(frozen=True)
class FakeRecord:
    packet_hash: int
    locale_hash: int
    index: int = 0

    @property
    def key(self):
        return self.packet_hash, self.locale_hash


@dataclass(frozen=True)
class FakeRouteSource:
    route_card_id: str
    route_card_hash: int
    line_number: int


@dataclass(frozen=True)
class FakeRouteRecord:
    route_card_hash: int
    index: int = 0


def row(packet_id: str, locale: str, packet_hash: int, locale_hash: int, line_number: int = 2) -> FakeRow:
    return FakeRow(packet_id, locale, "RS_TEST", line_number, packet_hash, locale_hash)


def record(packet_hash: int, locale_hash: int, index: int = 0) -> FakeRecord:
    return FakeRecord(packet_hash, locale_hash, index)


def route_source(route_card_id: str, route_card_hash: int, line_number: int = 2) -> FakeRouteSource:
    return FakeRouteSource(route_card_id, route_card_hash, line_number)


def route_record(route_card_hash: int, index: int = 0) -> FakeRouteRecord:
    return FakeRouteRecord(route_card_hash, index)


def clean_route_stats() -> RouteDeltaStats:
    return compute_route_delta([route_source("R001", 101)], [route_record(101)])


def current_artifact_state() -> ArtifactState:
    return ArtifactState(
        csv_path="Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv",
        route_csv_path="Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv",
        blob_path="Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin",
        csv_exists=True,
        route_csv_exists=True,
        blob_exists=True,
        csv_mtime_ns=100,
        route_csv_mtime_ns=100,
        blob_mtime_ns=200,
    )


def stale_artifact_state() -> ArtifactState:
    return ArtifactState(
        csv_path="Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv",
        route_csv_path="Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv",
        blob_path="Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin",
        csv_exists=True,
        route_csv_exists=True,
        blob_exists=True,
        csv_mtime_ns=200,
        route_csv_mtime_ns=300,
        blob_mtime_ns=100,
    )


class AppliedLoreBlobDeltaAuditTests(unittest.TestCase):
    def test_compute_blob_delta_accepts_matching_rows(self):
        rows = [row("P001", "en_US", 1, 11), row("P001", "ru_RU", 1, 12)]
        records = [record(1, 11), record(1, 12)]

        stats = compute_blob_delta(rows, records)

        self.assertTrue(stats.is_clean)
        self.assertEqual(stats.csv_rows, 2)
        self.assertEqual(stats.blob_rows, 2)

    def test_compute_blob_delta_groups_missing_locales_by_packet(self):
        rows = [
            row("P001", "en_US", 1, 11, 2),
            row("P001", "ru_RU", 1, 12, 3),
            row("P002", "en_US", 2, 11, 4),
        ]
        records = [record(2, 11)]

        stats = compute_blob_delta(rows, records)

        self.assertFalse(stats.is_clean)
        self.assertEqual(stats.missing_rows, 2)
        self.assertEqual(len(stats.missing_packets), 1)
        self.assertEqual(stats.missing_packets[0].packet_id, "P001")
        self.assertEqual(stats.missing_packets[0].locales, ("en_US", "ru_RU"))
        self.assertIn("missing packet=P001", render_delta(stats))

    def test_compute_blob_delta_reports_extra_and_duplicate_blob_keys(self):
        rows = [row("P001", "en_US", 1, 11)]
        records = [record(1, 11), record(9, 99), record(9, 99)]

        stats = compute_blob_delta(rows, records)

        self.assertEqual(stats.extra_rows, 1)
        self.assertEqual(stats.duplicate_blob_rows, 1)
        rendered = render_delta(stats)
        self.assertIn("extra blob keys count=1", rendered)
        self.assertIn("duplicate blob keys count=1", rendered)

    def test_compute_route_delta_accepts_matching_routes(self):
        stats = compute_route_delta([route_source("R001", 101)], [route_record(101)])

        self.assertTrue(stats.is_clean)
        self.assertEqual(stats.source_rows, 1)
        self.assertEqual(stats.blob_rows, 1)
        self.assertIn("AppliedLore route blob delta OK", render_route_delta(stats))

    def test_compute_route_delta_reports_missing_extra_and_duplicate_routes(self):
        stats = compute_route_delta(
            [route_source("R001", 101), route_source("R002", 102, line_number=3)],
            [route_record(101), route_record(999), route_record(999)],
        )

        self.assertFalse(stats.is_clean)
        self.assertEqual(stats.missing_rows, 1)
        self.assertEqual(stats.extra_rows, 1)
        self.assertEqual(stats.duplicate_blob_rows, 1)
        rendered = render_route_delta(stats)
        self.assertIn("missing route=R002", rendered)
        self.assertIn("extra route hashes count=1", rendered)
        self.assertIn("duplicate route hashes count=1", rendered)

    def test_render_delta_includes_stale_artifact_state(self):
        rows = [row("P001", "en_US", 1, 11)]
        records: list[FakeRecord] = []
        stats = compute_blob_delta(rows, records)
        artifact_state = stale_artifact_state()

        rendered = render_delta(stats, artifact_state=artifact_state)

        self.assertIn("csv_exists=true", rendered)
        self.assertIn("route_csv_exists=true", rendered)
        self.assertIn("blob_exists=true", rendered)
        self.assertIn("blob_older_than_csv=true", rendered)
        self.assertIn("blob_older_than_route_csv=true", rendered)
        self.assertIn("artifact_current=false", rendered)
        self.assertIn("artifact stale_reason=blob_older_than_packet_csv", rendered)
        self.assertIn("artifact stale_reason=blob_older_than_route_csv", rendered)

    def test_clean_keys_still_fail_when_artifact_is_stale(self):
        rows = [row("P001", "en_US", 1, 11)]
        records = [record(1, 11)]
        stats = compute_blob_delta(rows, records)
        route_stats = clean_route_stats()

        payload = combined_delta_to_json_payload(stats, route_stats, stale_artifact_state())
        rendered = render_delta(stats, artifact_state=stale_artifact_state())

        self.assertFalse(payload["clean"])
        self.assertTrue(payload["artifact"]["blob_older_than_csv"])
        self.assertIn("AppliedLore blob delta failed", rendered)
        self.assertIn("missing_rows=0", rendered)

    def test_json_payload_includes_packet_route_and_artifact_deltas(self):
        packet_stats = compute_blob_delta(
            [row("P001", "en_US", 1, 11), row("P001", "ru_RU", 1, 12)],
            [],
        )
        route_stats = compute_route_delta([route_source("R002", 102, line_number=7)], [])
        artifact_state = stale_artifact_state()

        payload = combined_delta_to_json_payload(
            packet_stats,
            route_stats,
            artifact_state,
            max_packets=1,
            max_locales=1,
        )

        self.assertFalse(payload["clean"])
        self.assertTrue(payload["artifact"]["blob_older_than_csv"])
        self.assertEqual(payload["packets"]["missing_rows"], 2)
        self.assertEqual(payload["packets"]["missing_packets"][0]["packet"], "P001")
        self.assertEqual(payload["packets"]["missing_packets"][0]["locales"], ["en_US"])
        self.assertEqual(payload["packets"]["missing_packets"][0]["truncated_locales"], 1)
        self.assertEqual(payload["routes"]["missing_rows"], 1)
        self.assertEqual(payload["routes"]["missing_routes"][0]["route"], "R002")

    def test_cli_returns_zero_for_clean_delta(self):
        rows = [row("P001", "en_US", 1, 11)]
        records = [record(1, 11)]

        with temporary_directory() as tmp:
            stdout = io.StringIO()
            stderr = io.StringIO()
            with patch("AppliedLoreBlobDeltaAudit.load_csv_rows", return_value=rows), patch(
                "AppliedLoreBlobDeltaAudit.load_blob_records",
                return_value=records,
            ), patch("AppliedLoreBlobDeltaAudit.run_route", return_value=clean_route_stats()), patch(
                "AppliedLoreBlobDeltaAudit.collect_artifact_state",
                return_value=current_artifact_state(),
            ), contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", tmp])

        self.assertEqual(exit_code, 0)
        self.assertIn("AppliedLore blob delta OK", stdout.getvalue())
        self.assertIn("AppliedLore route blob delta OK", stdout.getvalue())
        self.assertEqual(stderr.getvalue(), "")

    def test_cli_returns_one_for_missing_delta(self):
        rows = [row("P001", "en_US", 1, 11)]
        records: list[FakeRecord] = []

        with temporary_directory() as tmp:
            stdout = io.StringIO()
            stderr = io.StringIO()
            with patch("AppliedLoreBlobDeltaAudit.load_csv_rows", return_value=rows), patch(
                "AppliedLoreBlobDeltaAudit.load_blob_records",
                return_value=records,
            ), patch("AppliedLoreBlobDeltaAudit.run_route", return_value=clean_route_stats()), contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", tmp])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stdout.getvalue(), "")
        self.assertIn("missing packet=P001", stderr.getvalue())

    def test_cli_json_writes_stdout_and_keeps_failure_exit_code(self):
        rows = [row("P001", "en_US", 1, 11)]
        records: list[FakeRecord] = []

        with temporary_directory() as tmp:
            stdout = io.StringIO()
            stderr = io.StringIO()
            with patch("AppliedLoreBlobDeltaAudit.load_csv_rows", return_value=rows), patch(
                "AppliedLoreBlobDeltaAudit.load_blob_records",
                return_value=records,
            ), patch("AppliedLoreBlobDeltaAudit.run_route", return_value=clean_route_stats()), contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", tmp, "--json", "--max-packets", "1"])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stderr.getvalue(), "")
        payload = json.loads(stdout.getvalue())
        self.assertFalse(payload["clean"])
        self.assertEqual(payload["packets"]["missing_rows"], 1)
        self.assertEqual(payload["packets"]["missing_packets"][0]["packet"], "P001")
        self.assertTrue(payload["routes"]["clean"])

    def test_cli_returns_one_for_parser_failure_without_traceback(self):
        with temporary_directory() as tmp:
            stdout = io.StringIO()
            stderr = io.StringIO()
            with patch("AppliedLoreBlobDeltaAudit.load_csv_rows", side_effect=AuditFailure("bad blob")), (
                contextlib.redirect_stdout(stdout)
            ), contextlib.redirect_stderr(stderr):
                exit_code = main(["--root", tmp])

        self.assertEqual(exit_code, 1)
        self.assertEqual(stdout.getvalue(), "")
        self.assertIn("AppliedLore blob delta failed", stderr.getvalue())
        self.assertIn("bad blob", stderr.getvalue())
        self.assertNotIn("Traceback", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
