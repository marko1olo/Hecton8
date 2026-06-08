import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreTextIntegrityAudit import collect_production_packets, parse_packet_globs


class TestAppliedLoreTextIntegrityAudit(unittest.TestCase):
    def test_production_packet_collection_respects_packet_globs(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet_root = root / "Docs" / "Lore" / "AppliedContent" / "production_packets"
            packet_root.mkdir(parents=True)
            (packet_root / "P001_KEEP.production.md").write_text("keep", encoding="utf-8")
            (packet_root / "P999_SKIP.production.md").write_text("skip", encoding="utf-8")

            paths = collect_production_packets(root, parse_packet_globs("P001*"))

            self.assertEqual([path.name for path in paths], ["P001_KEEP.production.md"])


if __name__ == "__main__":
    unittest.main()
