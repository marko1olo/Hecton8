#!/usr/bin/env python3
"""Validate file and directory references in the HECTON-8 architecture atlas."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


DEFAULT_ATLAS = Path("Docs") / "DEPENDENCY_GRAPH.md"

PATH_PREFIXES = (
    "AGENTS.md",
    ".agents-skills/",
    "Assets/",
    "Docs/",
    "Packages/",
    "ProjectSettings/",
    "Tools/",
)

MARKDOWN_LINK_RE = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
INLINE_CODE_RE = re.compile(r"`([^`]+)`")
PLAIN_PATH_RE = re.compile(
    r"(?<![\w./-])(?:AGENTS\.md|\.agents-skills/|Assets/|Docs/|Packages/|ProjectSettings/|Tools/)"
    r"[^\s|),;:\"'<>]+"
)


def normalize_reference(raw: str) -> str | None:
    ref = raw.strip()
    if not ref:
        return None

    if ref.startswith("<") and ref.endswith(">"):
        ref = ref[1:-1].strip()

    lower = ref.lower()
    if (
        lower.startswith("http://")
        or lower.startswith("https://")
        or lower.startswith("mailto:")
        or lower.startswith("#")
    ):
        return None

    if "#" in ref:
        ref = ref.split("#", 1)[0]

    if ":" in ref:
        before, after = ref.rsplit(":", 1)
        if after.isdigit():
            ref = before

    ref = ref.replace("\\", "/").strip()
    ref = ref.rstrip(".,;)")

    if not ref or "*" in ref or "..." in ref or " " in ref and not any(ref.startswith(p) for p in PATH_PREFIXES):
        return None

    if not any(ref.startswith(prefix) for prefix in PATH_PREFIXES):
        return None

    return ref


def collect_references(text: str) -> dict[str, set[int]]:
    refs: dict[str, set[int]] = {}

    def add(raw: str, line_no: int) -> None:
        normalized = normalize_reference(raw)
        if normalized is None:
            return
        refs.setdefault(normalized, set()).add(line_no)

    for line_no, line in enumerate(text.splitlines(), start=1):
        for match in MARKDOWN_LINK_RE.finditer(line):
            add(match.group(1), line_no)
        for match in INLINE_CODE_RE.finditer(line):
            add(match.group(1), line_no)
        plain_line = INLINE_CODE_RE.sub("", line)
        for match in PLAIN_PATH_RE.finditer(plain_line):
            add(match.group(0), line_no)

    return refs


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--atlas",
        default=str(DEFAULT_ATLAS),
        help="Atlas markdown file to validate.",
    )
    parser.add_argument(
        "--root",
        default=".",
        help="Repository root. Defaults to current directory.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    atlas = (root / args.atlas).resolve()
    if not atlas.exists():
        print(f"ATLAS_CHECK_FAIL missing_atlas={atlas}", file=sys.stderr)
        return 2

    text = atlas.read_text(encoding="utf-8")
    refs = collect_references(text)
    missing: list[tuple[str, list[int]]] = []

    for ref, lines in sorted(refs.items()):
        target = (root / ref).resolve()
        try:
            target.relative_to(root)
        except ValueError:
            missing.append((ref, sorted(lines)))
            continue

        if not target.exists():
            missing.append((ref, sorted(lines)))

    if missing:
        print(f"ATLAS_CHECK_FAIL references={len(refs)} missing={len(missing)}")
        for ref, lines in missing[:100]:
            rendered_lines = ",".join(str(line) for line in lines[:8])
            print(f"MISSING {ref} lines={rendered_lines}")
        if len(missing) > 100:
            print(f"MISSING_TRUNCATED remaining={len(missing) - 100}")
        return 1

    print(f"ATLAS_CHECK_PASS references={len(refs)} atlas={atlas}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
