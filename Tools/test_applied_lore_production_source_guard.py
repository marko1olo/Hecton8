import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from io import StringIO

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import TARGET_LOCALES
from AppliedLoreProductionSourceGuard import run_guard


def packet_markdown(packet_id: str, statuses: dict[str, str] | None = None, body_suffix: str = "") -> str:
    statuses = statuses or {}
    rows = []
    for locale in TARGET_LOCALES:
        status = statuses.get(locale)
        if status is None:
            status = "source_authority" if locale == "en_US" else "draft_machine_or_llm"
        rows.append(f"| {locale} | {status} | Scanner text. |")

    return "\n".join(
        [
            f"# {packet_id}",
            "",
            "## Header Metadata",
            "",
            "| Field | Value |",
            "|---|---|",
            f"| Packet ID | {packet_id} |",
            "| Runtime layer | future_import_candidate |",
            "| Content status | source_complete_unimported |",
            "",
            "## Source Brief",
            "",
            "Cold source only. No runtime readiness.",
            "",
            "## Surface Texts",
            "",
            "### Scanner",
            "",
            "Scanner text.",
            "",
            "## Future Integration Notes",
            "",
            "Importer/publication readiness remains false.",
            "",
            "## Locale Rows",
            "",
            "| Locale | Status | Text |",
            "|---|---|---|",
            *rows,
            body_suffix,
        ]
    )


def write_release(root: Path, release_set_id: str, packet_ids: list[str]) -> None:
    base = root / "Docs" / "Lore" / "AppliedContent"
    packet_dir = base / "production_packets"
    release_dir = base / "release_sets"
    packet_dir.mkdir(parents=True, exist_ok=True)
    release_dir.mkdir(parents=True, exist_ok=True)

    sources = []
    for packet_id in packet_ids:
        path = packet_dir / f"{packet_id}.production.md"
        path.write_text(packet_markdown(packet_id), encoding="utf-8")
        sources.append(f"Docs/Lore/AppliedContent/production_packets/{packet_id}.production.md")

    manifest = {
        "schema": "H8.APPLIED_LORE_RELEASE_SET.V0",
        "release_set_id": release_set_id,
        "packet_sources": sources,
        "canonical_importer_ready": False,
        "runtime_ready": False,
        "native_localization_ready": False,
        "data_monolith_ready": False,
        "h8bin_ready": False,
        "unity_placement_ready": False,
        "generated_page_ready": False,
        "publication_ready": False,
    }
    (release_dir / f"{release_set_id}_manifest.json").write_text(json.dumps(manifest), encoding="utf-8")


class TestAppliedLoreProductionSourceGuard(unittest.TestCase):
    def test_valid_release_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_release(root, "RS900_TEST_PRODUCTION_SOURCE", ["P9000_TEST_PACKET"])

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS900_*", "", False)

            self.assertEqual(ret, 0)
            self.assertIn("FINAL: PASS", out.getvalue())

    def test_locale_gap_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_release(root, "RS901_TEST_PRODUCTION_SOURCE", ["P9001_TEST_PACKET"])
            packet_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "production_packets"
                / "P9001_TEST_PACKET.production.md"
            )
            packet_path.write_text(
                packet_markdown("P9001_TEST_PACKET").replace("| ru_RU | draft_machine_or_llm | Scanner text. |\n", ""),
                encoding="utf-8",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS901_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("missing locale rows: ru_RU", out.getvalue())

    def test_ready_manifest_flag_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_release(root, "RS902_TEST_PRODUCTION_SOURCE", ["P9002_TEST_PACKET"])
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS902_TEST_PRODUCTION_SOURCE_manifest.json"
            )
            data = json.loads(manifest_path.read_text(encoding="utf-8"))
            data["runtime_ready"] = True
            manifest_path.write_text(json.dumps(data), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS902_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("runtime_ready must be false", out.getvalue())

    def test_release_number_conflict_fails_selected_release(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_release(root, "RS903_TEST_ONE", ["P9003_TEST_PACKET"])
            write_release(root, "RS903_TEST_TWO", ["P9004_TEST_PACKET"])

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS903_TEST_ONE", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("release number prefix RS903 is reused", out.getvalue())

    def test_unknown_connected_packet_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_release(root, "RS904_TEST_PRODUCTION_SOURCE", ["P9005_TEST_PACKET"])
            packet_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "production_packets"
                / "P9005_TEST_PACKET.production.md"
            )
            packet_path.write_text(
                packet_markdown("P9005_TEST_PACKET").replace(
                    "| Packet ID | P9005_TEST_PACKET |\n",
                    "| Packet ID | P9005_TEST_PACKET |\n"
                    "| Connected packets | P9999_MISSING_PACKET |\n",
                ),
                encoding="utf-8",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS904_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("connected packet id is not present", out.getvalue())


if __name__ == "__main__":
    unittest.main()
