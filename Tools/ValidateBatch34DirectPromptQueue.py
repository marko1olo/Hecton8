#!/usr/bin/env python3
"""Validate Batch34 direct service prompt queue files."""

from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BATCH_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Prompts/Batch34"
PARTS = (
    (
        BATCH_ROOT / "3403_TEXTURE_SOURCE_EXPANSION_DIRECT_PART1_25_20260608.md",
        tuple(range(3401, 3426)),
    ),
    (
        BATCH_ROOT / "3404_TEXTURE_SOURCE_EXPANSION_DIRECT_PART2_25_20260608.md",
        tuple(range(3426, 3451)),
    ),
)
INSTRUCTION_PATH = "Docs/GeneratedAssets/Gemini/Prompts/Batch34/3402_TEXTURE_SERVICE_AGENT_INSTRUCTIONS_20260608.md"
SOURCE_PACK_PATH = "Docs/GeneratedAssets/Gemini/Prompts/Batch34/3401_TEXTURE_SOURCE_EXPANSION_PROMPT_PACK_20260608.md"
REQUIRED_NEGATIVE_TOKENS = (
    "no readable text",
    "no labels",
    "no logo",
    "no watermark-like decorative mark",
    "no mobile-game icon composition",
    "no baked directional light",
    "no cropped atlas islands",
)


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def negative_block(text: str) -> str:
    marker = "Global negative prompt when the service supports a separate negative field:"
    marker_index = text.find(marker)
    if marker_index < 0:
        return ""
    match = re.search(r"```text\s*(.*?)\s*```", text[marker_index:], re.DOTALL)
    return "" if match is None else match.group(1).strip()


def validate_part(path: Path, expected_ids: tuple[int, ...], errors: list[str]) -> None:
    if not path.exists():
        errors.append(f"missing direct prompt file: {display(path)}")
        return

    text = path.read_text(encoding="utf-8-sig")
    ids = [int(match) for match in re.findall(r"(?m)^## B34-(\d{4}) - .+$", text)]
    expected = list(expected_ids)
    if ids != expected:
        errors.append(f"{display(path)} ids mismatch: expected={expected[0]}-{expected[-1]} actual={ids[:1]}..{ids[-1:]}")

    if len(ids) != 25:
        errors.append(f"{display(path)} must contain exactly 25 jobs; found={len(ids)}")
    if len(ids) != len(set(ids)):
        errors.append(f"{display(path)} contains duplicate job ids")

    prompt_blocks = re.findall(r"(?m)^Prompt:\s*\n\s*```text\s*\n", text)
    if len(prompt_blocks) != 25:
        errors.append(f"{display(path)} must contain exactly 25 job prompt blocks; found={len(prompt_blocks)}")

    block = negative_block(text).lower()
    if not block:
        errors.append(f"{display(path)} has empty global negative prompt")
    for token in REQUIRED_NEGATIVE_TOKENS:
        if token not in block:
            errors.append(f"{display(path)} global negative prompt missing token: {token}")

    if INSTRUCTION_PATH not in text:
        errors.append(f"{display(path)} missing service-agent instruction link")
    if SOURCE_PACK_PATH not in text:
        errors.append(f"{display(path)} missing source-pack link")
    if "DIRECT_SERVICE_SUBMISSION_QUEUE" not in text:
        errors.append(f"{display(path)} missing DIRECT_SERVICE_SUBMISSION_QUEUE status")


def main() -> int:
    errors: list[str] = []
    all_ids: list[int] = []
    for path, expected_ids in PARTS:
        validate_part(path, expected_ids, errors)
        if path.exists():
            text = path.read_text(encoding="utf-8-sig")
            all_ids.extend(int(match) for match in re.findall(r"(?m)^## B34-(\d{4}) - .+$", text))

    if len(all_ids) != len(set(all_ids)):
        errors.append("direct prompt queue contains duplicate ids across files")
    if all_ids and (min(all_ids), max(all_ids), len(all_ids)) != (3401, 3450, 50):
        errors.append(f"direct prompt queue must cover B34-3401..B34-3450 exactly; found count={len(all_ids)}")

    print("BATCH34_DIRECT_PROMPT_QUEUE_VALIDATOR")
    for path, _expected_ids in PARTS:
        print(f"part={display(path)}")
    print(f"jobs={len(all_ids)}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
