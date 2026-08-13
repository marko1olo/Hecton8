import unittest

from unittest.mock import patch
import importlib.util
import sys
from pathlib import Path

# Dynamically load the script
script_path = Path(__file__).parent / "OptimizeBatch34AlphaPngSources.py"
spec = importlib.util.spec_from_file_location("OptimizeBatch34AlphaPngSources", script_path)
opt = importlib.util.module_from_spec(spec)
sys.modules["OptimizeBatch34AlphaPngSources"] = opt
spec.loader.exec_module(opt)

class TestOptimizeBatch34AlphaPngSources(unittest.TestCase):
    def test_framework(self):
        self.assertTrue(hasattr(opt, 'display'))

    def test_display(self):
        in_project = opt.ROOT / "some" / "relative" / "path.png"
        self.assertEqual(opt.display(in_project), "some/relative/path.png")

        out_project = Path("/tmp/outside/project.png")
        self.assertEqual(opt.display(out_project), str(out_project))

    def test_project_path(self):
        abs_str = "C:/tmp/absolute/path.png" if sys.platform == "win32" else "/tmp/absolute/path.png"
        abs_path = Path(abs_str)
        self.assertEqual(opt.project_path(abs_str), abs_path)

        rel_path = "Assets/some/path.png"
        self.assertEqual(opt.project_path(rel_path), opt.ROOT / rel_path)

    def test_load_json(self):
        import tempfile
        import json

        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8-sig", delete=False) as f:
            f.write(json.dumps({"test": "value"}))
            temp_path = Path(f.name)

        try:
            data = opt.load_json(temp_path)
            self.assertEqual(data, {"test": "value"})
        finally:
            temp_path.unlink()

    @patch.object(opt, 'ALPHA_MANIFEST')
    @patch.object(opt, 'PADDED_MANIFEST')
    @patch.object(opt, 'load_json')
    def test_iter_targets(self, mock_load_json, mock_padded_manifest, mock_alpha_manifest):
        mock_alpha_manifest.exists.return_value = True
        mock_padded_manifest.exists.return_value = True

        mock_load_json.side_effect = [
            {
                "entries": [
                    {"id": "alpha_1", "alphaCandidate": "Assets/alpha1.png"},
                    {"id": "alpha_2"}, # Missing alphaCandidate
                    {"alphaCandidate": "Assets/alpha2.png"}, # Missing id
                ]
            },
            {
                "entries": [
                    {"id": "padded_1", "paddedAtlas": "Assets/padded1.png"},
                    {"id": "padded_2"}, # Missing paddedAtlas
                    {"paddedAtlas": "Assets/padded2.png"}, # Missing id
                ]
            }
        ]

        targets = opt.iter_targets()

        self.assertEqual(len(targets), 2)

        self.assertEqual(targets[0][0], "alpha_1")
        self.assertEqual(targets[0][1], opt.ROOT / "Assets/alpha1.png")

        self.assertEqual(targets[1][0], "padded_1:padded")
        self.assertEqual(targets[1][1], opt.ROOT / "Assets/padded1.png")

    @patch.object(opt, 'ALPHA_MANIFEST')
    @patch.object(opt, 'PADDED_MANIFEST')
    def test_iter_targets_missing_manifests(self, mock_padded_manifest, mock_alpha_manifest):
        mock_alpha_manifest.exists.return_value = False
        mock_padded_manifest.exists.return_value = False

        targets = opt.iter_targets()
        self.assertEqual(len(targets), 0)



    def test_optimize_png_missing(self):
        missing_path = Path("/tmp/does_not_exist_xyz.png")
        if missing_path.exists():
            missing_path.unlink()
        status, before, after = opt.optimize_png("entry1", missing_path)
        self.assertEqual(status, "missing")
        self.assertEqual(before, 0)
        self.assertEqual(after, 0)

    def test_optimize_png_skipped_non_png(self):
        import tempfile
        with tempfile.NamedTemporaryFile(suffix=".jpg", delete=False) as f:
            f.write(b"fake data")
            temp_path = Path(f.name)

        try:
            status, before, after = opt.optimize_png("entry1", temp_path)
            self.assertEqual(status, "skipped-non-png")
            self.assertEqual(before, 9)
            self.assertEqual(after, 9)
        finally:
            temp_path.unlink()

    @patch('OptimizeBatch34AlphaPngSources.Image.open')
    @patch('OptimizeBatch34AlphaPngSources.Path.stat')
    @patch('OptimizeBatch34AlphaPngSources.Path.unlink')
    @patch('OptimizeBatch34AlphaPngSources.os.replace')
    def test_optimize_png_kept(self, mock_replace, mock_unlink, mock_stat, mock_open):
        existing_path = Path("/tmp/exists.png")

        with patch.object(Path, 'exists', return_value=True):
            # Mock stat().st_size
            mock_stat_result = unittest.mock.MagicMock()
            mock_stat_result.st_size = 100
            mock_stat.return_value = mock_stat_result

            # Setup image mock
            mock_img = unittest.mock.MagicMock()
            mock_img_rgba = unittest.mock.MagicMock()
            mock_img.convert.return_value = mock_img_rgba
            mock_open.return_value.__enter__.return_value = mock_img

            # First st_size call is before (100). Second is after (100 -> kept).
            status, before, after = opt.optimize_png("entry1", existing_path)

            self.assertEqual(status, "kept")
            self.assertEqual(before, 100)
            self.assertEqual(after, 100)
            mock_img_rgba.save.assert_called_once()
            mock_replace.assert_not_called()

    @patch('OptimizeBatch34AlphaPngSources.Image.open')
    @patch('OptimizeBatch34AlphaPngSources.ImageChops.difference')
    @patch('OptimizeBatch34AlphaPngSources.Path.stat')
    @patch('OptimizeBatch34AlphaPngSources.Path.unlink')
    @patch('OptimizeBatch34AlphaPngSources.os.replace')
    def test_optimize_png_rejected_pixel_diff(self, mock_replace, mock_unlink, mock_stat, mock_diff, mock_open):
        existing_path = Path("/tmp/exists.png")

        with patch.object(Path, 'exists', return_value=True):
            # Mock stat().st_size: before=100, after=50
            mock_stat_before = unittest.mock.MagicMock(); mock_stat_before.st_size = 100
            mock_stat_after = unittest.mock.MagicMock(); mock_stat_after.st_size = 50
            mock_stat.side_effect = [mock_stat_before, mock_stat_after]

            # Setup image mock
            mock_img = unittest.mock.MagicMock()
            mock_img_rgba = unittest.mock.MagicMock()
            mock_img_rgba.size = (10, 10)
            mock_img.convert.return_value = mock_img_rgba

            # When opening original and tmp
            mock_open.return_value.__enter__.return_value = mock_img

            # Mock difference to return something indicating a diff (getbbox() is not None)
            mock_diff_result = unittest.mock.MagicMock()
            mock_diff_result.getbbox.return_value = (0, 0, 10, 10)
            mock_diff.return_value = mock_diff_result

            status, before, after = opt.optimize_png("entry1", existing_path)

            self.assertEqual(status, "rejected-pixel-diff")
            self.assertEqual(before, 100)
            self.assertEqual(after, 100) # Returns before, before on rejection
            mock_replace.assert_not_called()

    @patch('OptimizeBatch34AlphaPngSources.Image.open')
    @patch('OptimizeBatch34AlphaPngSources.ImageChops.difference')
    @patch('OptimizeBatch34AlphaPngSources.Path.stat')
    @patch('OptimizeBatch34AlphaPngSources.Path.unlink')
    @patch('OptimizeBatch34AlphaPngSources.os.replace')
    def test_optimize_png_optimized(self, mock_replace, mock_unlink, mock_stat, mock_diff, mock_open):
        existing_path = Path("/tmp/exists.png")

        with patch.object(Path, 'exists', return_value=True):
            # Mock stat().st_size: before=100, after=50
            mock_stat_before = unittest.mock.MagicMock(); mock_stat_before.st_size = 100
            mock_stat_after = unittest.mock.MagicMock(); mock_stat_after.st_size = 50
            mock_stat.side_effect = [mock_stat_before, mock_stat_after]

            # Setup image mock
            mock_img = unittest.mock.MagicMock()
            mock_img_rgba = unittest.mock.MagicMock()
            mock_img_rgba.size = (10, 10)
            mock_img.convert.return_value = mock_img_rgba

            mock_open.return_value.__enter__.return_value = mock_img

            # Mock difference to return None (no pixel diff)
            mock_diff_result = unittest.mock.MagicMock()
            mock_diff_result.getbbox.return_value = None
            mock_diff.return_value = mock_diff_result

            status, before, after = opt.optimize_png("entry1", existing_path)

            self.assertEqual(status, "optimized")
            self.assertEqual(before, 100)
            self.assertEqual(after, 50)
            mock_replace.assert_called_once()


    @patch.object(opt, 'iter_targets')
    @patch.object(opt, 'optimize_png')
    @patch('builtins.print')
    def test_main_all_success_and_skipped(self, mock_print, mock_optimize_png, mock_iter_targets):
        mock_iter_targets.return_value = [
            ("target_opt", Path("opt.png")),
            ("target_kept", Path("kept.png")),
            ("target_skip", Path("skip.jpg"))
        ]

        # return status, before, after
        mock_optimize_png.side_effect = [
            ("optimized", 2048, 1024),
            ("kept", 1024, 1024),
            ("skipped-non-png", 512, 512)
        ]

        result = opt.main()
        self.assertEqual(result, 0)
        self.assertTrue(mock_print.called)

    @patch.object(opt, 'iter_targets')
    @patch.object(opt, 'optimize_png')
    @patch('builtins.print')
    def test_main_with_missing(self, mock_print, mock_optimize_png, mock_iter_targets):
        mock_iter_targets.return_value = [
            ("target_opt", Path("opt.png")),
            ("target_miss", Path("miss.png"))
        ]

        mock_optimize_png.side_effect = [
            ("optimized", 2048, 1024),
            ("missing", 0, 0)
        ]

        result = opt.main()
        self.assertEqual(result, 1) # Should return 1 because there is a missing file
        self.assertTrue(mock_print.called)

if __name__ == '__main__':
    sys.path.insert(0, str(Path(__file__).parent))
    unittest.main()
