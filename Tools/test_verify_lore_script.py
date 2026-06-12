#!/usr/bin/env python3
"""Tests for verify_lore.py."""

import os
import sys
from pathlib import Path
import pytest

# Add the repository root to sys.path so we can import verify_lore.py
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import verify_lore


def test_check_file_happy_path(tmp_path):
    """Test that a file with enough words and no banned phrases passes without issues."""
    test_file = tmp_path / "valid_file.md"
    content = "This is a completely valid file. " * 50  # Ensure > 200 words
    test_file.write_text(content, encoding="utf-8")

    issues = verify_lore.check_file(str(test_file))
    assert not issues


def test_check_file_banned_phrases(tmp_path):
    """Test that banned phrases are properly detected."""
    test_file = tmp_path / "banned_file.md"
    content = ("This is a completely valid file. " * 50) + " Let's delve into this tapestry."
    test_file.write_text(content, encoding="utf-8")

    issues = verify_lore.check_file(str(test_file))
    assert len(issues) == 2
    assert "Contains banned phrase: 'delve'" in issues
    assert "Contains banned phrase: 'tapestry'" in issues


def test_check_file_too_short(tmp_path):
    """Test that files with fewer than 200 words are flagged."""
    test_file = tmp_path / "short_file.md"
    content = "This file is too short."
    test_file.write_text(content, encoding="utf-8")

    issues = verify_lore.check_file(str(test_file))
    assert len(issues) == 1
    assert "File too short: 5 words." in issues


def test_check_file_missing_factory_sections(tmp_path):
    """Test that a FACTORY_20260606.md file requires specific headers."""
    test_file = tmp_path / "SOME_FACTORY_20260606.md"
    # Make it long enough so it doesn't fail the length check
    content = "This is a completely valid file but missing headers. " * 50
    test_file.write_text(content, encoding="utf-8")

    issues = verify_lore.check_file(str(test_file))
    assert len(issues) == 3
    assert "Missing In-game codex entry" in issues
    assert "Missing Scanner short" in issues
    assert "Missing Terminal surface" in issues


def test_check_file_with_factory_sections(tmp_path):
    """Test that a FACTORY_20260606.md file passes when headers are present."""
    test_file = tmp_path / "SOME_FACTORY_20260606.md"
    # Make it long enough so it doesn't fail the length check
    content = (
        "### In-game codex entry\n"
        "### Scanner short\n"
        "### Terminal/memo/document surface\n"
        + "This is a completely valid file. " * 50
    )
    test_file.write_text(content, encoding="utf-8")

    issues = verify_lore.check_file(str(test_file))
    assert not issues


def test_check_file_oserror_handling(tmp_path):
    """Test that OSError (e.g. FileNotFoundError) is properly caught and reported."""
    non_existent_file = tmp_path / "does_not_exist.md"

    issues = verify_lore.check_file(str(non_existent_file))
    assert len(issues) == 1
    assert "File error:" in issues[0]
    assert "No such file or directory" in issues[0]
