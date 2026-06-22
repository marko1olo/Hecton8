import unittest
from OOP_VoxelPaging_Scanner import next_u64

class TestOOPVoxelPagingScanner(unittest.TestCase):
    def test_next_u64(self):
        # Testing input 0
        val1 = next_u64(0)
        self.assertEqual(val1, 16294208416658607535)

        # Testing input 0x9E3779B97F4A7C15
        val2 = next_u64(0x9E3779B97F4A7C15)
        self.assertEqual(val2, 7960286522194355700)

        # Testing max uint64 boundary
        val3 = next_u64(0xFFFFFFFFFFFFFFFF)
        self.assertEqual(val3, 16490336266968443936)

        # Verify result bounds
        self.assertIsInstance(val1, int)
        self.assertTrue(0 <= val1 <= 0xFFFFFFFFFFFFFFFF)
        self.assertIsInstance(val2, int)
        self.assertTrue(0 <= val2 <= 0xFFFFFFFFFFFFFFFF)
        self.assertIsInstance(val3, int)
        self.assertTrue(0 <= val3 <= 0xFFFFFFFFFFFFFFFF)

if __name__ == '__main__':
    unittest.main()
