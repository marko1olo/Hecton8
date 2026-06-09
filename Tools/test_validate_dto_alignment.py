import unittest
from pathlib import Path
import tempfile
import sys
import os

sys.path.insert(0, str(Path(__file__).parent))
import ValidateDtoAlignment

class TestValidateDtoAlignment(unittest.TestCase):
    def test_valid_dto(self):
        with tempfile.NamedTemporaryFile(mode='w', suffix='.cs', delete=False) as f:
            f.write("""
            [StructLayout(LayoutKind.Explicit, Size = 16)]
            public struct ValidDTO {
                [FieldOffset(0)] public int Id;
                [FieldOffset(4)] public int Val;
                [FieldOffset(8)] public long BigVal;
            }
            """)
            filepath = Path(f.name)

        structs = ValidateDtoAlignment.parse_cs_file(filepath)
        self.assertEqual(len(structs), 1)
        errors = ValidateDtoAlignment.validate_struct(structs[0])
        self.assertEqual(len(errors), 0)
        os.unlink(filepath)

    def test_invalid_bool(self):
        with tempfile.NamedTemporaryFile(mode='w', suffix='.cs', delete=False) as f:
            f.write("""
            [StructLayout(LayoutKind.Explicit, Size = 8)]
            public struct BadBoolDTO {
                [FieldOffset(0)] public bool Flag;
            }
            """)
            filepath = Path(f.name)

        structs = ValidateDtoAlignment.parse_cs_file(filepath)
        errors = ValidateDtoAlignment.validate_struct(structs[0])
        self.assertTrue(any("uses raw 'bool'" in e for e in errors))
        os.unlink(filepath)

    def test_invalid_size(self):
        with tempfile.NamedTemporaryFile(mode='w', suffix='.cs', delete=False) as f:
            f.write("""
            [StructLayout(LayoutKind.Explicit, Size = 12)]
            public struct BadSizeDTO {
                [FieldOffset(0)] public int Id;
                [FieldOffset(4)] public int Val;
                [FieldOffset(8)] public int Val2;
            }
            """)
            filepath = Path(f.name)

        structs = ValidateDtoAlignment.parse_cs_file(filepath)
        errors = ValidateDtoAlignment.validate_struct(structs[0])
        self.assertTrue(any("not a multiple of 8" in e for e in errors))
        os.unlink(filepath)

if __name__ == '__main__':
    unittest.main()
