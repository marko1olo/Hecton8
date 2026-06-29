

import unittest
import sys
import tempfile
import os
import shutil
from pathlib import Path
from unittest.mock import patch

# Add Tools directory to path to import OOP_VoxelPaging_Scanner
sys.path.insert(0, str(Path(__file__).resolve().parent))

import OOP_VoxelPaging_Scanner as scanner

class TestOOPVoxelPagingScanner(unittest.TestCase):
    def test_read(self):
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', delete=False) as f:
            f.write("Hello World! 🌍")
            temp_path = Path(f.name)

        try:
            content = scanner.read(temp_path)
            self.assertEqual(content, "Hello World! 🌍")
        finally:
            os.unlink(temp_path)

    def test_extract_method_success(self):
        source = """
public class MyClass {
    private static int ResolveDirectorySlot(ulong hash) {
        if (hash == 0) {
            return 0;
        }
        return 1;
    }

    public void OtherMethod() {
    }
}
"""
        signature = "private static int ResolveDirectorySlot"
        expected = """private static int ResolveDirectorySlot(ulong hash) {
        if (hash == 0) {
            return 0;
        }
        return 1;
    }"""
        extracted = scanner.extract_method(source, signature)
        self.assertEqual(extracted, expected)

    def test_extract_method_not_found(self):
        source = "public void Foo() {}"
        signature = "void Bar"
        self.assertEqual(scanner.extract_method(source, signature), "")

    def test_extract_method_no_open_brace(self):
        source = "public abstract void Foo();"
        signature = "void Foo"
        self.assertEqual(scanner.extract_method(source, signature), "")

    def test_extract_method_no_close_brace(self):
        source = "public void Foo() { int x = 1; "
        signature = "void Foo"
        self.assertEqual(scanner.extract_method(source, signature), "")

    def test_next_u64(self):
        # Test consistency with specific seed
        val1 = scanner.next_u64(0x1312D17EC70B5EED)
        self.assertIsInstance(val1, int)
        self.assertEqual(val1, scanner.next_u64(0x1312D17EC70B5EED))

    def test_resolve_slot(self):
        # Should stay bounded by DIRECTORY_SLOTS (252)
        val1 = scanner.resolve_slot(0x1312D17EC70B5EED)
        self.assertGreaterEqual(val1, 0)
        self.assertLess(val1, scanner.DIRECTORY_SLOTS)

    def test_fuzzer(self):
        result = scanner.fuzzer(samples=100)
        self.assertIn("samples", result)
        self.assertEqual(result["samples"], 100)
        self.assertIn("uniqueSlots", result)
        self.assertIn("allSlotsReachable", result)
        self.assertIn("minBucket", result)
        self.assertIn("maxBucket", result)
        self.assertIn("stdDevBucket", result)

    @patch('OOP_VoxelPaging_Scanner.read')
    @patch('OOP_VoxelPaging_Scanner.REPORT')
    def test_main_success(self, mock_report, mock_read):
        # Setup mock report path
        temp_dir = tempfile.mkdtemp()
        mock_report_path = Path(temp_dir) / "report.json"
        mock_report.parent = mock_report_path.parent
        mock_report.write_text = mock_report_path.write_text

        # Setup mock read payload to satisfy all checks
        pager_content = """
private static int ResolveDirectorySlot(ulong hash) {
    return (int)(hash % (ulong)DirectorySlotCount); // check: directoryModuloPresent, directoryMaskRemoved
}
Dump_1312_VoxelPaging.bin
DirectorySlot
Metrics
PagerTelemetryEntry
GenerateMockWorldPageWriteJob
IJobParallelFor
"""
        processor_content = """
private void EnsureCompactionScratchBuffers() {
    EnsureGenerationHandle<byte>();
}
private void EnsureNativeSnapshotScratchBuffer() {
    EnsureGenerationHandle<byte>();
}
VoxelPagingBlackBoxDumpRelativePath1312
WriteBlackBoxDumpFile(VoxelPagingBlackBoxDumpRelativePath1312
WriteBlackBoxDumpFile(VoxelBlackBoxDumpRelativePath
Dump_1304_Voxel.bin
ValidateAgent1312PrivateLayouts
double3
double distanceSq = math.lengthsq(delta)
MaxSparseDeltaRunsPerPagerPayload
sparseRunCount > MaxSparseDeltaRunsPerPagerPayload
"""
        compression_content = """
HeaderFlagDenseFallback
MaxVoxelDeltaRleRunsPerWalPayload
flags = (flags & ~HeaderFlagFatal) | HeaderFlagDenseFallback
VoxelDeltaDenseFallbackPayloadBytes
Dump_1312_VoxelPaging.bin
"""

        def mock_read_side_effect(path):
            if path == scanner.PAGER:
                return pager_content
            if path == scanner.PROCESSOR:
                return processor_content
            if path == scanner.COMPRESSION:
                return compression_content
            return ""

        mock_read.side_effect = mock_read_side_effect

        # Adjust DIRECTORY_BYTES to satisfy directoryPageStillFits check without mutating module if possible
        original_directory_bytes = scanner.DIRECTORY_BYTES
        scanner.DIRECTORY_BYTES = scanner.DIRECTORY_HEADER_BYTES + (scanner.DIRECTORY_SLOTS * scanner.DIRECTORY_ENTRY_BYTES)

        original_dense_fallback = scanner.DENSE_FALLBACK_BYTES
        scanner.DENSE_FALLBACK_BYTES = 135168

        # We also need to mock print or capture it to prevent polluting test output
        with patch('builtins.print') as mock_print:
            try:
                result = scanner.main()
                self.assertEqual(result, 0)
            finally:
                scanner.DIRECTORY_BYTES = original_directory_bytes
                scanner.DENSE_FALLBACK_BYTES = original_dense_fallback
                shutil.rmtree(temp_dir)

    @patch('OOP_VoxelPaging_Scanner.read')
    @patch('OOP_VoxelPaging_Scanner.REPORT')
    def test_main_failure(self, mock_report, mock_read):
        temp_dir = tempfile.mkdtemp()
        mock_report_path = Path(temp_dir) / "report.json"
        mock_report.parent = mock_report_path.parent
        mock_report.write_text = mock_report_path.write_text

        # Empty payloads will definitely fail the checks
        mock_read.return_value = ""

        with patch('builtins.print') as mock_print:
            try:
                result = scanner.main()
                self.assertEqual(result, 2)
            finally:
                shutil.rmtree(temp_dir)



if __name__ == '__main__':
    unittest.main()
