import tempfile
import os
from pathlib import Path
import unittest

from OOP_VoxelPaging_Scanner import read, ScanError

class TestOOPVoxelPagingScanner(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.temp_dir_path = Path(self.temp_dir.name)

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_read_valid_utf8(self):
        # Create a temporary file with valid utf-8 content
        test_file = self.temp_dir_path / "valid.txt"
        content = "Hello, World! 🚀 Voxel Paging!"
        test_file.write_text(content, encoding="utf-8")

        # Verify read function
        result = read(test_file)
        self.assertEqual(result, content)

    def test_read_invalid_utf8(self):
        # Create a temporary file with invalid utf-8 content
        test_file = self.temp_dir_path / "invalid.bin"
        # Write some non-utf8 bytes
        with open(test_file, "wb") as f:
            f.write(b'\xff\xfe\x00\x00')

        # Verify read function raises an error
        with self.assertRaises(UnicodeDecodeError):
            read(test_file)

if __name__ == '__main__':
    unittest.main()
