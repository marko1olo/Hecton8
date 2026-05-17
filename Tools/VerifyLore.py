#!/usr/bin/env python3
"""Verify and inspect the raw HECTON-8 H8LR encyclopedia blob."""

from __future__ import annotations

import LorePacker

from LorePacker import (  # noqa: F401
    ALIGNMENT,
    DEFAULT_BLOB,
    DEFAULT_MANIFEST,
    DEFAULT_SOURCE_DIR,
    HEADER_STRUCT,
    MAGIC,
    RECORD_STRUCT,
    VERSION,
    LoreRecord,
    SourceEntry,
    bake_blob,
    build_manifest,
    compute_fnv1a32,
    fnv1a32_ascii_lower,
    format_hash,
    load_current_blob_and_manifest,
    load_source_entries,
    parse_blob,
    repo_relative,
    verify_entries_against_blob,
    verify_manifest,
)


def main(argv: list[str] | None = None) -> int:
    return LorePacker.main(argv)


if __name__ == "__main__":
    raise SystemExit(main())
