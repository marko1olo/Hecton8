"""Shared AppliedLore text integrity checks for source and generated surfaces."""

from __future__ import annotations

import re


PLACEHOLDER_VISIBLE_MARKERS = ("LOC HOLD", "TODO")
CORRUPTION_VISIBLE_MARKERS = ("\ufffd",)
SUSPICIOUS_QUESTION_MARK_PATTERNS = (
    ("double_question_mark", re.compile(r"\?\?")),
    ("question_mark_inside_word", re.compile(r"(?<=[^\W\d_])\?(?=[^\W\d_])", re.UNICODE)),
)
EXACT_MOJIBAKE_PATTERNS = (
    ("utf8_as_latin1_c2_c3", re.compile(r"[\u00c2\u00c3][\u0080-\u00bf]")),
    ("utf8_as_latin1_d0_d1", re.compile(r"[\u00d0\u00d1][\u0080-\u00bf]")),
    ("utf8_as_latin1_d8_d9", re.compile(r"[\u00d8\u00d9][\u0080-\u00bf]")),
    ("utf8_as_latin1_e2_punct", re.compile(r"\u00e2(?:[\u0080-\u009f]|\u20ac)")),
    ("utf8_as_latin1_e3_cjk", re.compile(r"\u00e3(?:[\u0080-\u009f]|\u0192|\u201a)")),
)


def find_text_integrity_errors(text: str, *, include_placeholder_markers: bool = True) -> list[str]:
    errors: list[str] = []
    visible_markers = CORRUPTION_VISIBLE_MARKERS
    if include_placeholder_markers:
        visible_markers = PLACEHOLDER_VISIBLE_MARKERS + CORRUPTION_VISIBLE_MARKERS

    for marker in visible_markers:
        if marker in text:
            errors.append(f"forbidden_marker={marker!r}")

    if any(0x80 <= ord(char) <= 0x9F for char in text):
        errors.append("c1_control_character")

    for name, pattern in SUSPICIOUS_QUESTION_MARK_PATTERNS:
        if pattern.search(text) is not None:
            errors.append(f"suspicious_question_mark={name}")

    for name, pattern in EXACT_MOJIBAKE_PATTERNS:
        if pattern.search(text) is not None:
            errors.append(f"mojibake={name}")

    return errors
