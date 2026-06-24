import unittest
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from OptimizeBatch34AlphaPngSources import display, ROOT

class TestOptimizeBatch34AlphaPngSources(unittest.TestCase):
    def test_display_relative_path(self):
        """Test display with a path relative to ROOT."""
        test_path = ROOT / "Assets" / "Test.png"
        self.assertEqual(display(test_path), "Assets/Test.png")

    def test_display_outside_path(self):
        """Test display with a path outside ROOT to trigger ValueError."""
        if sys.platform == "win32":
            test_path = Path("D:/outside/path.png")
        else:
            test_path = Path("/tmp/outside/path.png")

        self.assertEqual(display(test_path), str(test_path))

if __name__ == "__main__":
    unittest.main()
