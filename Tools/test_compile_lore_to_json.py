import unittest
import sys
import os
from pathlib import Path
import tempfile

# Add root directory to sys.path to import compile_lore_to_json
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import compile_lore_to_json

class TestCompileLoreToJson(unittest.TestCase):
    def test_parse_markdown_to_json_missing_packet_structure(self):
        """Test parsing when the file doesn't have the expected '## Packet ' markers"""
        with tempfile.NamedTemporaryFile(mode='w', delete=False, suffix='.md') as f:
            f.write("Just some random text\nNo packets here\n")
            filepath = f.name

        try:
            result = compile_lore_to_json.parse_markdown_to_json(filepath)
            self.assertEqual(result, [])
        finally:
            os.remove(filepath)

    def test_parse_markdown_to_json_with_valid_packet(self):
        """Test parsing with a valid packet structure"""
        with tempfile.NamedTemporaryFile(mode='w', delete=False, suffix='.md') as f:
            f.write("""# Header

## Packet P_TEST_01
- **Title**: Test Packet
- **Author**: Test Author
### Content Surface
This is the test content.
""")
            filepath = f.name

        try:
            result = compile_lore_to_json.parse_markdown_to_json(filepath)
            self.assertEqual(len(result), 1)
            packet = result[0]
            self.assertEqual(packet["packet_id"], "P_TEST_01")
            self.assertEqual(packet["metadata"]["title"], "Test Packet")
            self.assertEqual(packet["metadata"]["author"], "Test Author")
            self.assertEqual(packet["surfaces"]["content_surface"], "This is the test content.")
        finally:
            os.remove(filepath)

if __name__ == '__main__':
    unittest.main()
