import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
ROOT = TOOLS_ROOT.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAssetOwnerPacketIndex as validator  # noqa: E402
from Tools.test_local_temp import project_local_tempdir_factory  # noqa: E402


TEMP_DIR = project_local_tempdir_factory("asset_owner_packet_index")


class ValidateAssetOwnerPacketIndexTests(unittest.TestCase):
    def test_current_project_index_matches_static_contract(self) -> None:
        rows = validator.validate_asset_owner_packet_index()

        self.assertEqual(37, len(rows))
        self.assertEqual(32, sum(1 for row in rows if row.packet_presence == "PRESENT"))
        self.assertEqual(5, sum(1 for row in rows if row.packet_presence == "OUTPUT_ONLY_NO_PACKET_FILE"))

    def test_line_count_mismatch_rejects_present_packet(self) -> None:
        with TEMP_DIR() as temp_dir:
            root = Path(temp_dir)
            packet = root / "taskslocal/asset_system_20260605/ASSET_OWNER_01_PACKET.md"
            packet.parent.mkdir(parents=True, exist_ok=True)
            packet.write_text("one\nline\n", encoding="utf-8")
            row = _row(packet_file=str(packet.relative_to(root)).replace("\\", "/"), line_count=99)

            with self.assertRaises(SystemExit):
                validator.validate_present_packet(row, root=root)

    def test_output_only_taskfile_rejects_row(self) -> None:
        with TEMP_DIR() as temp_dir:
            tasklocal_root = Path(temp_dir)
            (tasklocal_root / "ASSET_OWNER_29_BAD_PACKET.md").write_text("bad", encoding="utf-8")
            row = _row(
                owner_id="29",
                packet_presence="OUTPUT_ONLY_NO_PACKET_FILE",
                packet_file=validator.OUTPUT_ONLY_PACKET_FILE,
                domain="material_p0_target_table_worker_output",
                title="Owner 29 target-table worker output only",
                line_count=0,
                primary_source_files="Docs/AssetAudit/PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.md/.csv",
                acceptance_blocked_by="No packet file exists; use named target table output",
            )

            with self.assertRaises(SystemExit):
                validator.validate_output_only_packet(row, tasklocal_root=tasklocal_root)

    def test_unknown_completion_status_rejects_row(self) -> None:
        row = _row(status="GREEN")

        with self.assertRaises(SystemExit):
            validator.validate_status_language(row)

    def test_bad_id_sequence_rejects_rows(self) -> None:
        rows = [_row(owner_id="02"), _row(owner_id="01")]

        with self.assertRaises(SystemExit):
            validator.validate_id_sequence(rows)


def _row(**overrides: object) -> validator.OwnerPacketRow:
    values: dict[str, object] = {
        "owner_id": "01",
        "packet_presence": "PRESENT",
        "packet_file": "taskslocal/asset_system_20260605/ASSET_OWNER_01_PACKET.md",
        "domain": "unity_material_readback",
        "title": "Asset Owner 01 - Unity Material Readback",
        "status": "PENDING_VERIFICATION",
        "evidence_class": "STATIC_DOC",
        "line_count": 1,
        "unity_gate_required": "YES",
        "uses_p0_target_tables": "NO",
        "primary_source_files": "SEE_PACKET_SOURCE_EVIDENCE",
        "acceptance_blocked_by": "Unity/runtime/import/visual/audio/profiler/memory proof absent unless packet later produces artifacts",
        "notes": "Task packet file exists; distributable scope only; status is static unless future proof exists",
    }
    values.update(overrides)
    return validator.OwnerPacketRow(**values)


if __name__ == "__main__":
    unittest.main()
