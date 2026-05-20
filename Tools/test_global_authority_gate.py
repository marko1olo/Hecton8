import json
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import GlobalAuthorityGate as gate  # noqa: E402


class GlobalAuthorityGateTests(unittest.TestCase):
    def _write_h8_memory(self, source: Path, duplicate: bool = False) -> Path:
        h8memory = source / "Core" / "Memory" / "H8Memory.cs"
        h8memory.parent.mkdir(parents=True)
        beta_value = 10 if duplicate else 11
        h8memory.write_text(
            "namespace Hecton8.Core { public static class H8Memory {\n"
            "public enum BufferID : int\n"
            "{\n"
            "    Alpha = 10,\n"
            f"    Beta = {beta_value},\n"
            "}\n"
            "} }\n",
            encoding="utf-8",
        )
        return h8memory

    def test_hard_failures_detect_registry_get_and_duplicate_buffer_id(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_global_gate_", dir=TOOLS_ROOT) as temp_dir:
            source = Path(temp_dir) / "Assets" / "_Project" / "Scripts"
            h8memory = self._write_h8_memory(source, duplicate=True)
            gameplay = source / "Gameplay" / "Owner.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "GlobalRegistry.Get<IThing>();\n"
                "SignalBus<FooSignal>.TryPush(default);\n"
                "var id = (BufferID)10;\n",
                encoding="utf-8",
            )

            payload = gate.build_payload(source, h8memory)
            failures = gate.hard_failures(
                payload,
                gate.build_parser().parse_args(
                    ["--source-root", str(source), "--h8-memory", str(h8memory)]
                ),
            )

        self.assertEqual(payload["counts"]["globalRegistryGenericGet"]["matches"], 1)
        self.assertEqual(payload["bufferId"]["duplicateValueCount"], 1)
        self.assertEqual(payload["signalBus"]["suspectProducerTypeCount"], 1)
        self.assertEqual(len(failures), 2)

    def test_json_stdout_writes_no_files_and_reports_clean_hard_gate(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_global_gate_json_", dir=TOOLS_ROOT) as temp_dir:
            source = Path(temp_dir) / "Assets" / "_Project" / "Scripts"
            h8memory = self._write_h8_memory(source, duplicate=False)
            gameplay = source / "Gameplay" / "Owner.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "SignalBus<FooSignal>.Configure(16, default);\n"
                "SignalBus<FooSignal>.TryPush(default);\n",
                encoding="utf-8",
            )
            args = gate.build_parser().parse_args(
                ["--source-root", str(source), "--h8-memory", str(h8memory), "--json"]
            )
            output = StringIO()
            with redirect_stdout(output):
                exit_code = gate.run(args)
            parsed = json.loads(output.getvalue())

        self.assertEqual(exit_code, 0)
        self.assertEqual(parsed["bufferId"]["duplicateValueCount"], 0)
        self.assertEqual(parsed["signalBus"]["suspectProducerTypeCount"], 0)
        self.assertEqual(parsed["failures"], [])

    def test_pack_one_does_not_match_pack_sixteen(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_global_gate_pack_", dir=TOOLS_ROOT) as temp_dir:
            source = Path(temp_dir) / "Assets" / "_Project" / "Scripts"
            h8memory = self._write_h8_memory(source, duplicate=False)
            owner = source / "Core" / "Owner.cs"
            owner.parent.mkdir(parents=True, exist_ok=True)
            owner.write_text(
                "using System.Runtime.InteropServices;\n"
                "[StructLayout(LayoutKind.Explicit, Pack = 16)] struct SafePack {}\n"
                "[StructLayout(LayoutKind.Explicit, Pack = 1)] struct BadPack {}\n",
                encoding="utf-8",
            )

            payload = gate.build_payload(source, h8memory)

        self.assertEqual(payload["counts"]["packOne"]["matches"], 1)


if __name__ == "__main__":
    unittest.main()
