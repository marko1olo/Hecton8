import unittest
import sys
from pathlib import Path

# Add Tools to sys.path so we can import from OptimizeBatch34AlphaPngSources
sys.path.insert(0, str(Path(__file__).parent))

from OptimizeBatch34AlphaPngSources import display, ROOT_PATH

class TestOptimizeBatch34AlphaPngSources(unittest.TestCase):
    def test_display_relative_path(self):
        """Test display with a path relative to ROOT_PATH."""
        test_path = ROOT_PATH / "Assets" / "Test.png"
        self.assertEqual(display(test_path), "Assets/Test.png")

    def test_display_outside_path(self):
        """Test display with a path outside ROOT_PATH to trigger ValueError."""
        # A path completely outside ROOT_PATH to trigger ValueError in relative_to
        if sys.platform == "win32":
            test_path = Path("D:/outside/path.png")
        else:
            test_path = Path("/tmp/outside/path.png")

        expected = str(test_path).replace("\\", "/")
        self.assertEqual(display(test_path), expected)

    def test_display_with_backslashes(self):
        """Test display with backslashes to ensure they are replaced by forward slashes."""
        class MockPath:
            def relative_to(self, root):
                raise ValueError()
            def __str__(self):
                return "C:\\some\\fake\\path.png"

        # MockPath ducktypes path, but relative_to throws ValueError.
        # It falls back to str(path).replace("\\", "/")
        test_path = MockPath()
        self.assertEqual(display(test_path), "C:/some/fake/path.png")

if __name__ == "__main__":
    unittest.main()
