#!/usr/bin/env python3
"""Validate Batch34 targeted fix/regen prompt queues and supersession routing."""

from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BATCH_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Prompts/Batch34"
README = ROOT / "Docs/GeneratedAssets/Gemini/README.md"
STATIC_PREFLIGHT = ROOT / "Tools/RunGeminiMaterialStaticPreflight.ps1"
TARGETED_PROMPT_QUEUE_VALIDATOR = ROOT / "Tools/ValidateBatch34TargetedPromptQueues.py"
FIX_PROMPTS = BATCH_ROOT / "3405_TEXTURE_SOURCE_FIX_PROMPTS_20260608.md"
REGEN_PROMPTS = BATCH_ROOT / "3406_TEXTURE_SOURCE_REGEN_TARGETS_20260608.md"
REQUIRED_FIX_IDS = (
    3407,
    3409,
    3413,
    3415,
    3417,
    3418,
    3424,
    3438,
    3440,
    3443,
    3444,
    3447,
)
REQUIRED_REGEN_IDS = (3409, 3418)
OPTIONAL_REGEN_IDS = (3407, 3417)
SUPERSEDED_FIX_IDS = (3407, 3409, 3417, 3418)
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


def read(path: Path, errors: list[str]) -> str:
    if not path.exists():
        errors.append(f"missing targeted prompt file: {display(path)}")
        return ""
    return path.read_text(encoding="utf-8-sig")


def negative_block(text: str) -> str:
    marker = "Global negative prompt when the service supports a separate negative field:"
    marker_index = text.find(marker)
    if marker_index < 0:
        return ""
    match = re.search(r"```text\s*(.*?)\s*```", text[marker_index:], re.DOTALL)
    return "" if match is None else match.group(1).strip().lower()


def validate_negative(path: Path, text: str, errors: list[str]) -> None:
    block = negative_block(text)
    if not block:
        errors.append(f"{display(path)} has empty global negative prompt")
        return
    for token in REQUIRED_NEGATIVE_TOKENS:
        if token not in block:
            errors.append(f"{display(path)} global negative prompt missing token: {token}")


def validate_fix_prompts(text: str, errors: list[str]) -> None:
    if not text:
        return
    if "TARGETED_REGEN_QUEUE" not in text:
        errors.append(f"{display(FIX_PROMPTS)} missing TARGETED_REGEN_QUEUE status")

    ids = [int(match) for match in re.findall(r"(?m)^## B34-FIX-(\d{4}) - .+$", text)]
    expected = list(REQUIRED_FIX_IDS)
    if ids != expected:
        errors.append(f"{display(FIX_PROMPTS)} ids mismatch: expected={expected} actual={ids}")
    if len(ids) != len(set(ids)):
        errors.append(f"{display(FIX_PROMPTS)} contains duplicate targeted fix ids")
    if text.count("```text") != len(REQUIRED_FIX_IDS) + 1:
        errors.append(f"{display(FIX_PROMPTS)} text fence count mismatch")


def validate_regen_prompts(text: str, errors: list[str]) -> None:
    if not text:
        return
    if "TARGETED_DIRECT_SERVICE_SUBMISSION_QUEUE" not in text:
        errors.append(f"{display(REGEN_PROMPTS)} missing TARGETED_DIRECT_SERVICE_SUBMISSION_QUEUE status")

    ids = [int(match) for match in re.findall(r"(?m)^## B34-(\d{4})-R1 - .+$", text)]
    expected = list(REQUIRED_REGEN_IDS + OPTIONAL_REGEN_IDS)
    if sorted(ids) != sorted(expected):
        errors.append(f"{display(REGEN_PROMPTS)} ids mismatch: expected={sorted(expected)} actual={sorted(ids)}")
    if len(ids) != len(set(ids)):
        errors.append(f"{display(REGEN_PROMPTS)} contains duplicate targeted regen ids")
    if text.count("Prompt:") != len(expected):
        errors.append(f"{display(REGEN_PROMPTS)} prompt block count mismatch")
    if text.count("```text") != len(expected) + 1:
        errors.append(f"{display(REGEN_PROMPTS)} text fence count mismatch")

    for source_id in REQUIRED_REGEN_IDS:
        if f"`B34-{source_id}`" not in text:
            errors.append(f"{display(REGEN_PROMPTS)} missing Required now entry for B34-{source_id}")
    for source_id in OPTIONAL_REGEN_IDS:
        if f"`B34-{source_id}`" not in text:
            errors.append(f"{display(REGEN_PROMPTS)} missing Optional backup entry for B34-{source_id}")

    for source_id in SUPERSEDED_FIX_IDS:
        token = f"`B34-FIX-{source_id}`"
        if token not in text:
            errors.append(f"{display(REGEN_PROMPTS)} missing superseded fix token: {token}")
    if display(FIX_PROMPTS) not in text:
        errors.append(f"{display(REGEN_PROMPTS)} missing older fix prompt path")
    if "use this file instead of the older `3405` fix prompts" not in text:
        errors.append(f"{display(REGEN_PROMPTS)} missing operator supersession instruction")


def validate_cross_links(errors: list[str]) -> None:
    if README.exists():
        readme = README.read_text(encoding="utf-8-sig")
        for path in (FIX_PROMPTS, REGEN_PROMPTS, TARGETED_PROMPT_QUEUE_VALIDATOR):
            if display(path) not in readme:
                errors.append(f"{display(README)} missing targeted prompt link: {display(path)}")
    else:
        errors.append(f"missing Gemini README: {display(README)}")

    if STATIC_PREFLIGHT.exists():
        preflight = STATIC_PREFLIGHT.read_text(encoding="utf-8-sig")
        if "ValidateBatch34TargetedPromptQueues.py" not in preflight:
            errors.append("static preflight must include ValidateBatch34TargetedPromptQueues.py")
    else:
        errors.append(f"missing static preflight runner: {display(STATIC_PREFLIGHT)}")


def main() -> int:
    errors: list[str] = []
    fix_text = read(FIX_PROMPTS, errors)
    regen_text = read(REGEN_PROMPTS, errors)
    fix_ids = re.findall(r"(?m)^## B34-FIX-(\d{4}) - .+$", fix_text)
    regen_ids = re.findall(r"(?m)^## B34-(\d{4})-R1 - .+$", regen_text)

    validate_negative(FIX_PROMPTS, fix_text, errors)
    validate_negative(REGEN_PROMPTS, regen_text, errors)
    validate_fix_prompts(fix_text, errors)
    validate_regen_prompts(regen_text, errors)
    validate_cross_links(errors)

    print("BATCH34_TARGETED_PROMPT_QUEUE_VALIDATOR")
    print(f"fixPrompts={display(FIX_PROMPTS)}")
    print(f"regenPrompts={display(REGEN_PROMPTS)}")
    print(f"fixJobs={len(fix_ids)}")
    print(f"regenJobs={len(regen_ids)}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
