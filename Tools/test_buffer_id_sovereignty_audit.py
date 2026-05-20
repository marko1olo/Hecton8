import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import BufferIDSovereigntyAudit as audit  # noqa: E402


class BufferIDSovereigntyAuditTests(unittest.TestCase):
    def test_detects_duplicate_enum_values_and_local_casts(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_bufferid_audit_", dir=TOOLS_ROOT) as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            h8memory = source / "Core" / "Memory" / "H8Memory.cs"
            gameplay = source / "Gameplay" / "CastOwner.cs"
            h8memory.parent.mkdir(parents=True)
            gameplay.parent.mkdir(parents=True)

            h8memory.write_text(
                "namespace Hecton8.Core { public enum BufferID : int\n"
                "{\n"
                "    Alpha = 10,\n"
                "    Beta = 10,\n"
                "    Gamma,\n"
                "}\n"
                "}\n",
                encoding="utf-8",
            )
            gameplay.write_text(
                "var id = (BufferID)10;\n"
                "var id2 = ( BufferID ) 0x0B;\n",
                encoding="utf-8",
            )

            entries = audit.parse_buffer_enum(h8memory)
            enum_by_value = {}
            for entry in entries:
                enum_by_value.setdefault(entry.value, []).append(entry.name)

            duplicates = audit.group_duplicates(entries)
            casts = audit.scan_buffer_casts(
                source,
                {value: tuple(names) for value, names in enum_by_value.items()},
                h8memory,
            )
            payload = audit.build_payload(entries, casts, h8memory)

            self.assertEqual(len(entries), 3)
            self.assertIn(10, duplicates)
            self.assertEqual(payload["duplicateValueCount"], 1)
            self.assertEqual(payload["localNumericCastCount"], 2)
            self.assertEqual(casts[0].enum_names, ("Alpha", "Beta"))
            self.assertEqual(casts[1].enum_names, ("Gamma",))

    def test_json_report_round_trip(self) -> None:
        payload = {
            "schema": audit.AUDIT_SCHEMA,
            "bufferEnumPath": "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
            "bufferIdCount": 1,
            "duplicateValueCount": 0,
            "duplicateEntries": [],
            "localNumericCastCount": 0,
            "localNumericCastFileCount": 0,
            "localNumericCasts": [],
        }

        with tempfile.TemporaryDirectory(prefix="h8_bufferid_json_", dir=TOOLS_ROOT) as temp_dir:
            path = Path(temp_dir) / "audit.json"
            audit.write_json(path, payload)
            loaded = json.loads(path.read_text(encoding="utf-8"))

        self.assertEqual(loaded["schema"], audit.AUDIT_SCHEMA)
        self.assertEqual(loaded["bufferIdCount"], 1)


if __name__ == "__main__":
    unittest.main()
