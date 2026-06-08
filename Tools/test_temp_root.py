from __future__ import annotations

import contextlib
import shutil
import uuid
from pathlib import Path


@contextlib.contextmanager
def temporary_directory():
    parent = Path(__file__).resolve().parents[1] / ".tmp" / "python-tests"
    parent.mkdir(parents=True, exist_ok=True)
    root = parent / f"tmp_{uuid.uuid4().hex}"
    root.mkdir()
    try:
        yield str(root)
    finally:
        shutil.rmtree(root, ignore_errors=True)
