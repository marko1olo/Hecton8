import unittest
from pathlib import Path

class TestAmplifyImpostorTransform(unittest.TestCase):
    def test_temporary_solution_removed(self):
        filepath = Path("Assets/AmplifyImpostors/Plugins/Scripts/AmplifyImpostor.cs")
        with open(filepath, 'r') as f:
            content = f.read()

        self.assertNotIn("TODO: remove this temporary solution", content)
        self.assertNotIn("CopyTransform", content)
        self.assertNotIn("PasteTransform", content)

if __name__ == '__main__':
    unittest.main()
