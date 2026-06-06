import importlib.util
from pathlib import Path
import sys
import unittest


SCRIPT_PATH = Path(__file__).resolve().parent / "CompileWallX003Audit.py"
SPEC = importlib.util.spec_from_file_location("compile_wall_x003_audit", SCRIPT_PATH)
compile_wall = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = compile_wall
SPEC.loader.exec_module(compile_wall)


def scan_source(text):
    source = compile_wall.SourceFile(
        path=Path("Assets/_Project/Scripts/Audio/FakeNativeBridge.cs"),
        assembly="Hecton8.Audio",
        domain="Hecton8.Audio",
        editor=False,
        text=text,
    )
    return compile_wall.concrete_cast_scan([source])


class CompileWallX003AuditTests(unittest.TestCase):
    def test_ignores_native_pointer_handle_casts(self):
        findings = scan_source(
            "IntPtr frames = (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(buffer);\n"
            "UIntPtr state = (UIntPtr)statePtr;\n"
        )

        self.assertEqual([], findings)

    def test_ignores_standard_exception_filters(self):
        findings = scan_source(
            "catch (Exception exception) when (\n"
            "    exception is IOException ||\n"
            "    exception is UnauthorizedAccessException ||\n"
            "    exception is NotSupportedException ||\n"
            "    exception is ArgumentException)\n"
        )

        self.assertEqual([], findings)

    def test_ignores_generic_type_parameter_casts(self):
        findings = scan_source(
            "T component = source as T;\n"
            "if (component is T typedComponent) return typedComponent;\n"
        )

        self.assertEqual([], findings)

    def test_keeps_real_player_concrete_cast(self):
        findings = scan_source(
            "IPlayerRuntimeContext context = GlobalRegistry.RegisteredPlayer;\n"
            "PlayerRuntimeContextService runtime = context as PlayerRuntimeContextService;\n"
        )

        self.assertEqual(1, len(findings))
        self.assertEqual("PlayerRuntimeContextService", findings[0]["type"])
        self.assertTrue(findings[0]["directPlayerCoupling"])


if __name__ == "__main__":
    unittest.main()
