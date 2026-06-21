import sys
import unittest
from pathlib import Path
from unittest.mock import patch, mock_open

TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import OOP_SignalSpsc_Scanner as scanner

class TestOOPSignalSpscScanner(unittest.TestCase):

    def test_rel(self):
        # ROOT is two levels up from OOP_SignalSpsc_Scanner.py
        root_path = scanner.ROOT
        test_path = root_path / "Assets" / "_Project" / "Scripts" / "Test.cs"
        expected = "Assets/_Project/Scripts/Test.cs"
        self.assertEqual(scanner.rel(test_path), expected)

    @patch('pathlib.Path.read_text')
    def test_read_lines(self, mock_read_text):
        mock_read_text.return_value = "Line 1\nLine 2\nLine 3"
        path = Path("dummy_path")
        lines = scanner.read_lines(path)
        self.assertEqual(lines, ["Line 1", "Line 2", "Line 3"])
        mock_read_text.assert_called_once_with(encoding="utf-8-sig", errors="replace")

    def test_field_size_bytes(self):
        self.assertEqual(scanner.field_size_bytes("long"), 8)
        self.assertEqual(scanner.field_size_bytes("ulong"), 8)
        self.assertEqual(scanner.field_size_bytes("double"), 8)
        self.assertEqual(scanner.field_size_bytes("IntPtr"), 8)
        self.assertEqual(scanner.field_size_bytes("UIntPtr"), 8)
        self.assertEqual(scanner.field_size_bytes("int"), 4)
        self.assertEqual(scanner.field_size_bytes("uint"), 4)
        self.assertEqual(scanner.field_size_bytes("float"), 4)
        self.assertEqual(scanner.field_size_bytes("short"), 2)
        self.assertEqual(scanner.field_size_bytes("ushort"), 2)
        self.assertEqual(scanner.field_size_bytes("byte"), 1)
        self.assertEqual(scanner.field_size_bytes("sbyte"), 1)
        self.assertEqual(scanner.field_size_bytes("delegate*<void>"), 8)
        self.assertEqual(scanner.field_size_bytes("delegate* unmanaged<void>"), 8)
        self.assertEqual(scanner.field_size_bytes("long*"), 8)
        self.assertEqual(scanner.field_size_bytes("UnknownType"), 0)

    def test_field_order_violations(self):
        fields_valid = [
            {"file": "dummy.cs", "line": 1, "name": "f1", "type": "long"}, # 8 bytes
            {"file": "dummy.cs", "line": 2, "name": "f2", "type": "int"},  # 4 bytes
            {"file": "dummy.cs", "line": 3, "name": "f3", "type": "byte"}, # 1 byte
        ]
        self.assertEqual(scanner.field_order_violations(fields_valid), [])

        fields_invalid = [
            {"file": "dummy.cs", "line": 1, "name": "f1", "type": "int"},  # 4 bytes
            {"file": "dummy.cs", "line": 2, "name": "f2", "type": "long"}, # 8 bytes (violation!)
            {"file": "dummy.cs", "line": 3, "name": "f3", "type": "byte"}, # 1 byte
        ]
        violations = scanner.field_order_violations(fields_invalid)
        self.assertEqual(len(violations), 1)
        self.assertEqual(violations[0]["field"], "f2")
        self.assertEqual(violations[0]["reason"], "larger field appears after smaller field")

    @patch('OOP_SignalSpsc_Scanner.read_lines')
    def test_detect_struct_size(self, mock_read_lines):
        mock_read_lines.return_value = [
            "[StructLayout(LayoutKind.Explicit, Size = 128)]",
            "public struct MyStruct {",
            "}"
        ]
        size = scanner.detect_struct_size(Path("dummy"), "MyStruct")
        self.assertEqual(size, 128)

        mock_read_lines.return_value = [
            "public struct MyStruct {"
        ]
        size = scanner.detect_struct_size(Path("dummy"), "MyStruct")
        self.assertIsNone(size)

    @patch('OOP_SignalSpsc_Scanner.read_lines')
    def test_parse_field_offsets(self, mock_read_lines):
        mock_read_lines.return_value = [
            "public struct MyStruct {",
            "    [FieldOffset(0)] public long Head;",
            "    [FieldOffset(64)] private readonly long Tail;",
            "    [FieldOffset(16)] internal int Count;",
            "}"
        ]
        test_path = scanner.ROOT / "dummy.cs"
        fields = scanner.parse_field_offsets(test_path, "MyStruct")
        self.assertEqual(len(fields), 3)
        self.assertEqual(fields[0]["name"], "Head")
        self.assertEqual(fields[0]["offset"], 0)
        self.assertEqual(fields[0]["type"], "long")
        self.assertEqual(fields[1]["name"], "Count")
        self.assertEqual(fields[1]["offset"], 16)
        self.assertEqual(fields[1]["type"], "int")
        self.assertEqual(fields[2]["name"], "Tail")
        self.assertEqual(fields[2]["offset"], 64)
        self.assertEqual(fields[2]["type"], "long")
        # Ensure ordered by offset
        self.assertEqual([f["offset"] for f in fields], [0, 16, 64])

    @patch('pathlib.Path.exists')
    @patch('pathlib.Path.read_text')
    def test_load_asmdef_missing(self, mock_read_text, mock_exists):
        mock_exists.return_value = False
        test_path = scanner.ROOT / "missing.asmdef"
        result = scanner.load_asmdef(test_path)
        self.assertTrue(result.get("missing"))
        self.assertEqual(result.get("file"), "missing.asmdef")

    @patch('pathlib.Path.exists')
    @patch('pathlib.Path.read_text')
    def test_load_asmdef_exists(self, mock_read_text, mock_exists):
        mock_exists.return_value = True
        mock_read_text.return_value = '{"name": "Hecton", "allowUnsafeCode": true}'
        test_path = scanner.ROOT / "Hecton.asmdef"
        result = scanner.load_asmdef(test_path)
        self.assertEqual(result.get("name"), "Hecton")
        self.assertTrue(result.get("allowUnsafeCode"))
        self.assertEqual(result.get("file"), "Hecton.asmdef")

    def test_classify_new_expression(self):
        hit = {"file": "dummy.cs", "line": 10, "text": "new SignalLaneDispatch[10]"}
        result = scanner.classify_new_expression(hit)
        self.assertEqual(result["classification"], "cold_static_registry_array")

        hit = {"file": "dummy.cs", "line": 10, "text": "new SignalLaneDispatch()"}
        result = scanner.classify_new_expression(hit)
        self.assertEqual(result["classification"], "value_type_dispatch_record")

        hit = {"file": "dummy.cs", "line": 10, "text": "new RandomType()"}
        result = scanner.classify_new_expression(hit)
        self.assertEqual(result["classification"], "unknown")

    def test_cursor_alignment_ok(self):
        struct_map_ok = {
            "declaredSizeBytes": 128,
            "fields": [
                {"name": "Head", "offset": 0, "type": "long"},
                {"name": "Tail", "offset": 64, "type": "long"},
            ]
        }
        self.assertTrue(scanner.cursor_alignment_ok(struct_map_ok))

        struct_map_bad_size = {
            "declaredSizeBytes": 64,
            "fields": [
                {"name": "Head", "offset": 0, "type": "long"},
                {"name": "Tail", "offset": 64, "type": "long"},
            ]
        }
        self.assertFalse(scanner.cursor_alignment_ok(struct_map_bad_size))

        struct_map_bad_type = {
            "declaredSizeBytes": 128,
            "fields": [
                {"name": "Head", "offset": 0, "type": "int"},
                {"name": "Tail", "offset": 64, "type": "long"},
            ]
        }
        self.assertFalse(scanner.cursor_alignment_ok(struct_map_bad_type))

    @patch('pathlib.Path.exists')
    def test_classify_status_green(self, mock_exists):
        mock_exists.return_value = True
        token_hits = {key: [] for key in scanner.TOKEN_PATTERNS}
        callsite_hits = {}
        signalbus_nativequeue_writer_hits = []
        maps = {
            "SignalRingCursorState": {
                "declaredSizeBytes": 128,
                "multipleOf8": True,
                "fieldOrderViolations": [],
                "fields": [
                    {"name": "Head", "offset": 0, "type": "long"},
                    {"name": "Tail", "offset": 64, "type": "long"}
                ]
            }
        }
        dump_path_hits = {"requested_dump_path": [{"line": 1}]}
        phase_hits = {
            "dispatcher_pre_sim_flush": [],
            "registry_pre_sim_flush": [],
            "dispatcher_post_sim_clear": [],
            "snapshot_clear_delegate": [],
            "dispatcher_post_sim_flush": [{"line": 1}],
            "registry_post_sim_flush": [{"line": 1}],
        }
        fail_closed_keys = [
            "registration_gate_compare_exchange",
            "registration_gate_release",
            "registration_returns_bool",
            "registered_latch_from_result",
            "registration_overflow_log_once",
            "spsc_partial_allocation_cleanup",
            "mpsc_partial_allocation_cleanup",
            "failed_ring_check",
            "ring_dispose_on_failure",
            "frame_snapshot_release_on_failure",
            "async_dump_request",
            "ring_clear_drop_to_tail",
            "fuzzer_allocation_fail_closed",
            "dispatch_storage_guard",
            "dispatch_length_clamp",
            "writer_sanitize_before_budget",
            "writer_corrupt_drop",
        ]
        fail_closed_hits = {key: [{"line": 1}] for key in fail_closed_keys}
        fail_closed_hits["ring_clear_tail_reset"] = []
        fail_closed_hits["ring_clear_ticket_loop"] = []

        status, reasons = scanner.classify_status(
            token_hits, callsite_hits, signalbus_nativequeue_writer_hits,
            maps, dump_path_hits, phase_hits, fail_closed_hits
        )
        self.assertEqual(status, "GREEN_STATIC_ONLY")
        self.assertEqual(len(reasons), 1)

    @patch('pathlib.Path.exists')
    def test_classify_status_red_cursor(self, mock_exists):
        mock_exists.return_value = True
        token_hits = {key: [] for key in scanner.TOKEN_PATTERNS}
        callsite_hits = {}
        signalbus_nativequeue_writer_hits = []
        maps = {
            "SignalRingCursorState": {
                "declaredSizeBytes": 64, # Bad size
                "multipleOf8": True,
                "fieldOrderViolations": [],
                "fields": []
            }
        }
        dump_path_hits = {"requested_dump_path": [{"line": 1}]}
        phase_hits = {
            "dispatcher_pre_sim_flush": [],
            "registry_pre_sim_flush": [],
            "dispatcher_post_sim_clear": [],
            "snapshot_clear_delegate": [],
            "dispatcher_post_sim_flush": [{"line": 1}],
            "registry_post_sim_flush": [{"line": 1}],
        }
        fail_closed_keys = [
            "registration_gate_compare_exchange",
            "registration_gate_release",
            "registration_returns_bool",
            "registered_latch_from_result",
            "registration_overflow_log_once",
            "spsc_partial_allocation_cleanup",
            "mpsc_partial_allocation_cleanup",
            "failed_ring_check",
            "ring_dispose_on_failure",
            "frame_snapshot_release_on_failure",
            "async_dump_request",
            "ring_clear_drop_to_tail",
            "fuzzer_allocation_fail_closed",
            "dispatch_storage_guard",
            "dispatch_length_clamp",
            "writer_sanitize_before_budget",
            "writer_corrupt_drop",
        ]
        fail_closed_hits = {key: [{"line": 1}] for key in fail_closed_keys}
        fail_closed_hits["ring_clear_tail_reset"] = []
        fail_closed_hits["ring_clear_ticket_loop"] = []

        status, reasons = scanner.classify_status(
            token_hits, callsite_hits, signalbus_nativequeue_writer_hits,
            maps, dump_path_hits, phase_hits, fail_closed_hits
        )
        self.assertEqual(status, "RED")
        self.assertTrue(any("cursor layout is not" in r for r in reasons))

    @patch('OOP_SignalSpsc_Scanner.read_lines')
    @patch('pathlib.Path.exists')
    def test_scan_lines(self, mock_exists, mock_read_lines):
        mock_exists.return_value = True
        mock_read_lines.return_value = [
            "public void MyMethod() {",
            "    var writer = new NativeQueue<int>.ParallelWriter();",
            "    Debug.Log(\"test\");",
            "}"
        ]

        patterns = {
            "native_queue_writer": scanner.TOKEN_PATTERNS["native_queue_writer"],
            "debug_log": scanner.TOKEN_PATTERNS["debug_log"],
        }

        test_path = scanner.ROOT / "dummy.cs"
        hits = scanner.scan_lines([test_path], patterns)

        self.assertEqual(len(hits["native_queue_writer"]), 1)
        self.assertEqual(hits["native_queue_writer"][0]["line"], 2)

        self.assertEqual(len(hits["debug_log"]), 1)
        self.assertEqual(hits["debug_log"][0]["line"], 3)

    @patch('OOP_SignalSpsc_Scanner.scan_lines')
    @patch('pathlib.Path.rglob')
    def test_scan_callsites(self, mock_rglob, mock_scan_lines):
        mock_rglob.return_value = [scanner.ROOT / "dummy.cs"]
        mock_scan_lines.return_value = {"dummy_pattern": []}

        result = scanner.scan_callsites()

        self.assertEqual(result, {"dummy_pattern": []})
        mock_rglob.assert_called_once_with("*.cs")
        mock_scan_lines.assert_called_once()
        self.assertEqual(mock_scan_lines.call_args[0][1], scanner.CALLSITE_PATTERNS)

    @patch('OOP_SignalSpsc_Scanner.read_lines')
    @patch('pathlib.Path.rglob')
    def test_scan_signalbus_nativequeue_writer_intersections(self, mock_rglob, mock_read_lines):
        mock_rglob.return_value = [scanner.ROOT / "dummy.cs"]
        mock_read_lines.return_value = [
            "SignalBus<MyPayload>.TryEnqueueBounded()",
            "NativeQueue<MyPayload>.ParallelWriter writer;",
            "NativeQueue<OtherPayload>.ParallelWriter other_writer;"
        ]

        hits = scanner.scan_signalbus_nativequeue_writer_intersections()

        self.assertEqual(len(hits), 1)
        self.assertEqual(hits[0]["type"], "MyPayload")
        self.assertEqual(hits[0]["line"], 2)

if __name__ == '__main__':
    unittest.main()
