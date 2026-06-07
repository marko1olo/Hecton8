from __future__ import annotations

import shutil
import unittest
import uuid
from pathlib import Path
from unittest.mock import patch


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
DEFAULT_TEMP_ROOT = REPO_ROOT / ".tmp" / "python_unittest"


class ProjectLocalTemporaryDirectory:
    """TemporaryDirectory-compatible helper that avoids Windows tempfile ACL traps."""

    def __init__(
        self,
        suffix: str | None = None,
        prefix: str | None = None,
        dir: str | Path | None = None,
        ignore_cleanup_errors: bool = True,
        *,
        delete: bool = True,
    ) -> None:
        self._suffix = suffix or ""
        self._prefix = prefix or "tmp"
        self._root = Path(dir) if dir is not None else DEFAULT_TEMP_ROOT
        self._delete = delete
        self._ignore_cleanup_errors = ignore_cleanup_errors
        self.name: str | None = None
        self._ensure_created()

    def __enter__(self) -> str:
        return self._ensure_created()

    def __exit__(self, exc_type, exc, tb) -> None:
        self.cleanup()

    def _ensure_created(self) -> str:
        if self.name is not None:
            return self.name

        self._root.mkdir(parents=True, exist_ok=True)
        for _ in range(100):
            candidate = self._root / f"{self._prefix}{uuid.uuid4().hex}{self._suffix}"
            try:
                candidate.mkdir()
            except FileExistsError:
                continue
            self.name = str(candidate)
            return self.name

        raise RuntimeError(f"Unable to allocate a unique test temp directory under {self._root}")

    def cleanup(self) -> None:
        if self.name is None or not self._delete:
            return

        path = self.name
        self.name = None
        shutil.rmtree(path, ignore_errors=self._ignore_cleanup_errors)


def project_local_tempdir_factory(suite_name: str):
    temp_root = DEFAULT_TEMP_ROOT / suite_name
    temp_root.mkdir(parents=True, exist_ok=True)

    def factory(*args, **kwargs):
        kwargs.setdefault("dir", temp_root)
        kwargs.setdefault("ignore_cleanup_errors", True)
        return ProjectLocalTemporaryDirectory(*args, **kwargs)

    return factory


class ProjectLocalTemporaryDirectoryTests(unittest.TestCase):
    def test_cleanup_honors_strict_error_mode(self) -> None:
        temp_dir = DEFAULT_TEMP_ROOT / "test_local_temp_helper_tests"
        temp = ProjectLocalTemporaryDirectory(
            dir=temp_dir,
            ignore_cleanup_errors=False,
        )
        path = temp.name

        try:
            with patch("shutil.rmtree") as rmtree:
                temp.cleanup()

            rmtree.assert_called_once_with(path, ignore_errors=False)
            self.assertIsNone(temp.name)
        finally:
            if path is not None:
                shutil.rmtree(path, ignore_errors=True)
