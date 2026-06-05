#!/usr/bin/env python3
"""Static AppliedContent text integrity validator.

Evidence class: STATIC_SOURCE only. This tool does not prove native
localization, runtime placement, Unity import health, or h8bin readiness.
"""

from __future__ import annotations

import argparse
import fnmatch
import re
from dataclasses import dataclass
from pathlib import Path


ENGLISH_LOCALE = "en_US"
SURFACE_DIRS = ("in_game_wiki", "external_site")
DEFAULT_PACKET_GLOB = "*"
SAMPLE_PACKET_GLOB = "P45[6-9]*,P460*"
DEFAULT_SAMPLE_LIMIT = 8

BROAD_MARKERS = (
    ("U+00C2", "\u00c2"),
    ("U+00C3", "\u00c3"),
    ("U+00D0", "\u00d0"),
    ("U+00D1", "\u00d1"),
    ("U+00D8", "\u00d8"),
    ("U+00E2", "\u00e2"),
    ("U+00E6", "\u00e6"),
    ("U+00EC", "\u00ec"),
    ("U+00D7", "\u00d7"),
)
FAIL_MARKERS = (("U+FFFD", "\ufffd"),)

KNOWN_MOJIBAKE_PATTERNS = (
    ("utf8_as_latin1_c2_c3", re.compile(r"[\u00c2\u00c3][\u0080-\u00bf]")),
    ("utf8_as_latin1_d0_d1", re.compile(r"[\u00d0\u00d1][\u0080-\u00bf]")),
    ("utf8_as_latin1_d8_d9", re.compile(r"[\u00d8\u00d9][\u0080-\u00bf]")),
    ("utf8_as_latin1_e2_punct", re.compile(r"\u00e2(?:[\u0080-\u009f]|\u20ac)")),
    ("utf8_as_latin1_e3_cjk", re.compile(r"\u00e3(?:[\u0080-\u009f]|\u0192|\u201a)")),
)

READY_STATUS_TOKENS = (
    "native",
    "native_reviewed",
    "runtime_ready",
    "publication_ready",
    "publish_ready",
    "published",
    "website_ready",
    "public_ready",
)
DRAFT_STATUS_TOKENS = (
    "draft",
    "pending",
    "blocked_translation_draft",
)


class AuditFailure(Exception):
    """Raised for CLI configuration failures, not content failures."""


@dataclass(frozen=True)
class MarkerScan:
    label: str
    files: int
    broad_counts: dict[str, int]
    fail_counts: dict[str, int]
    exact_mojibake_hits: int
    broad_samples: tuple[str, ...]
    fail_samples: tuple[str, ...]
    exact_mojibake_samples: tuple[str, ...]
    decode_failures: tuple[str, ...]


@dataclass(frozen=True)
class GeneratedPage:
    surface: str
    locale: str
    packet_id: str
    status: str
    title_norm: str
    body_norm: str
    path: Path


@dataclass(frozen=True)
class CloneScan:
    files: int
    baselines: int
    compared: int
    missing_baselines: int
    title_exact: int
    body_exact: int
    both_exact: int
    draft_clone_warnings: int
    ready_clone_failures: int
    unknown_clone_warnings: int
    partial_clone_warnings: int
    samples: tuple[str, ...]
    failure_samples: tuple[str, ...]


def parse_packet_globs(value: str) -> tuple[str, ...]:
    patterns = tuple(part.strip() for part in value.split(",") if part.strip())
    return patterns or (DEFAULT_PACKET_GLOB,)


def relative_path(root: Path, path: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def read_utf8(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        raise AuditFailure(f"invalid UTF-8 decode: {path}") from exc


def collect_production_packets(root: Path) -> list[Path]:
    packet_root = root / "Docs" / "Lore" / "AppliedContent" / "production_packets"
    if not packet_root.exists():
        raise AuditFailure(f"missing production packet directory: {packet_root}")
    return sorted(packet_root.glob("*.md"), key=lambda item: item.name.lower())


def page_matches(path: Path, patterns: tuple[str, ...]) -> bool:
    if patterns == (DEFAULT_PACKET_GLOB,):
        return True
    for pattern in patterns:
        if fnmatch.fnmatchcase(path.name, pattern) or fnmatch.fnmatchcase(path.stem, pattern):
            return True
    return False


def collect_generated_pages(root: Path, packet_globs: tuple[str, ...]) -> list[Path]:
    content_root = root / "Docs" / "Lore" / "AppliedContent"
    pages: list[Path] = []
    for surface in SURFACE_DIRS:
        surface_root = content_root / surface
        if not surface_root.exists():
            continue
        for path in sorted(surface_root.glob("*/*.md"), key=lambda item: str(item).lower()):
            if page_matches(path, packet_globs):
                pages.append(path)
    return pages


def scan_markers(root: Path, label: str, paths: list[Path], sample_limit: int) -> MarkerScan:
    broad_counts = {name: 0 for name, _char in BROAD_MARKERS}
    fail_counts = {name: 0 for name, _char in FAIL_MARKERS}
    broad_samples: list[str] = []
    fail_samples: list[str] = []
    exact_samples: list[str] = []
    decode_failures: list[str] = []
    exact_hits = 0

    for path in paths:
        try:
            text = read_utf8(path)
        except AuditFailure:
            decode_failures.append(relative_path(root, path))
            continue

        rel = relative_path(root, path)
        for name, marker in FAIL_MARKERS:
            count = text.count(marker)
            fail_counts[name] += count
            if count and len(fail_samples) < sample_limit:
                fail_samples.append(f"{rel} {name}={count}")

        for name, marker in BROAD_MARKERS:
            count = text.count(marker)
            broad_counts[name] += count
            if count and len(broad_samples) < sample_limit:
                broad_samples.append(f"{rel} {name}={count}")

        for pattern_name, pattern in KNOWN_MOJIBAKE_PATTERNS:
            count = len(pattern.findall(text))
            exact_hits += count
            if count and len(exact_samples) < sample_limit:
                exact_samples.append(f"{rel} {pattern_name}={count}")

    return MarkerScan(
        label=label,
        files=len(paths),
        broad_counts=broad_counts,
        fail_counts=fail_counts,
        exact_mojibake_hits=exact_hits,
        broad_samples=tuple(broad_samples),
        fail_samples=tuple(fail_samples),
        exact_mojibake_samples=tuple(exact_samples),
        decode_failures=tuple(decode_failures[:sample_limit]),
    )


def parse_frontmatter(text: str) -> tuple[dict[str, str], str]:
    if not text.startswith("---"):
        return {}, text

    match = re.match(r"\A---[ \t]*\r?\n(?P<meta>.*?)\r?\n---[ \t]*(?:\r?\n)?(?P<body>.*)\Z", text, re.DOTALL)
    if match is None:
        return {}, text

    metadata: dict[str, str] = {}
    for line in match.group("meta").splitlines():
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        metadata[key.strip()] = value.strip()
    return metadata, match.group("body")


def strip_generated_comments(text: str) -> str:
    return re.sub(r"<!--.*?-->", "", text, flags=re.DOTALL)


def normalize_text(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()


def page_record(root: Path, path: Path) -> GeneratedPage:
    text = read_utf8(path)
    metadata, body = parse_frontmatter(text)
    body = strip_generated_comments(body)

    relative = path.relative_to(root / "Docs" / "Lore" / "AppliedContent")
    parts = relative.parts
    if len(parts) < 3:
        raise AuditFailure(f"unexpected generated page path shape: {path}")

    surface = parts[0]
    locale = parts[1]
    packet_id = metadata.get("packet_id") or path.stem
    status = metadata.get("localization_status", "")

    title = ""
    body_lines: list[str] = []
    title_seen = False
    for line in body.splitlines():
        if not title_seen:
            match = re.match(r"^#\s+(.+?)\s*$", line)
            if match is not None:
                title = match.group(1)
                title_seen = True
                continue
        body_lines.append(line)

    return GeneratedPage(
        surface=surface,
        locale=locale,
        packet_id=packet_id,
        status=status,
        title_norm=normalize_text(title),
        body_norm=normalize_text("\n".join(body_lines)),
        path=path,
    )


def normalize_status(status: str) -> str:
    return re.sub(r"[^a-z0-9]+", "_", status.lower()).strip("_")


def is_draft_status(status: str) -> bool:
    normalized = normalize_status(status)
    return any(token in normalized for token in DRAFT_STATUS_TOKENS)


def is_ready_status(status: str) -> bool:
    normalized = normalize_status(status)
    if any(token in normalized for token in DRAFT_STATUS_TOKENS):
        return False
    return any(token in normalized for token in READY_STATUS_TOKENS)


def scan_generated_clones(root: Path, page_paths: list[Path], sample_limit: int) -> CloneScan:
    records = [page_record(root, path) for path in page_paths]
    baselines: dict[tuple[str, str], GeneratedPage] = {}
    for record in records:
        if record.locale == ENGLISH_LOCALE:
            baselines[(record.surface, record.packet_id)] = record

    compared = 0
    missing_baselines = 0
    title_exact = 0
    body_exact = 0
    both_exact = 0
    draft_clone_warnings = 0
    ready_clone_failures = 0
    unknown_clone_warnings = 0
    partial_clone_warnings = 0
    samples: list[str] = []
    failure_samples: list[str] = []

    for record in records:
        if record.locale == ENGLISH_LOCALE:
            continue

        baseline = baselines.get((record.surface, record.packet_id))
        if baseline is None:
            missing_baselines += 1
            if len(failure_samples) < sample_limit:
                failure_samples.append(f"{relative_path(root, record.path)} missing_en_US_baseline")
            continue

        compared += 1
        current_title_exact = record.title_norm == baseline.title_norm
        current_body_exact = record.body_norm == baseline.body_norm
        if current_title_exact:
            title_exact += 1
        if current_body_exact:
            body_exact += 1

        rel = relative_path(root, record.path)
        if current_title_exact and current_body_exact:
            both_exact += 1
            if is_ready_status(record.status):
                ready_clone_failures += 1
                if len(failure_samples) < sample_limit:
                    failure_samples.append(f"{rel} status={record.status or 'MISSING'}")
            elif is_draft_status(record.status):
                draft_clone_warnings += 1
                if len(samples) < sample_limit:
                    samples.append(f"{rel} draft_exact_en_clone")
            else:
                unknown_clone_warnings += 1
                if len(samples) < sample_limit:
                    samples.append(f"{rel} status={record.status or 'MISSING'} exact_en_clone")
        elif current_title_exact or current_body_exact:
            partial_clone_warnings += 1
            if len(samples) < sample_limit:
                parts = []
                if current_title_exact:
                    parts.append("title")
                if current_body_exact:
                    parts.append("body")
                samples.append(f"{rel} partial_en_clone={'+'.join(parts)} status={record.status or 'MISSING'}")

    return CloneScan(
        files=len(records),
        baselines=len(baselines),
        compared=compared,
        missing_baselines=missing_baselines,
        title_exact=title_exact,
        body_exact=body_exact,
        both_exact=both_exact,
        draft_clone_warnings=draft_clone_warnings,
        ready_clone_failures=ready_clone_failures,
        unknown_clone_warnings=unknown_clone_warnings,
        partial_clone_warnings=partial_clone_warnings,
        samples=tuple(samples),
        failure_samples=tuple(failure_samples),
    )


def format_counts(counts: dict[str, int]) -> str:
    return " ".join(f"{name}={counts[name]}" for name in sorted(counts))


def print_samples(title: str, samples: tuple[str, ...]) -> None:
    if not samples:
        return
    print(title)
    for sample in samples:
        print(f"  {sample}")


def run(root: Path, packet_globs: tuple[str, ...], sample_limit: int) -> int:
    production_paths = collect_production_packets(root)
    generated_paths = collect_generated_pages(root, packet_globs)

    production_scan = scan_markers(root, "production_packets", production_paths, sample_limit)
    generated_scan = scan_markers(root, "generated_pages", generated_paths, sample_limit)
    clone_scan = scan_generated_clones(root, generated_paths, sample_limit)

    hard_failures = (
        sum(production_scan.fail_counts.values())
        + sum(generated_scan.fail_counts.values())
        + production_scan.exact_mojibake_hits
        + generated_scan.exact_mojibake_hits
        + len(production_scan.decode_failures)
        + len(generated_scan.decode_failures)
        + clone_scan.ready_clone_failures
        + clone_scan.missing_baselines
    )
    warnings = (
        sum(production_scan.broad_counts.values())
        + sum(generated_scan.broad_counts.values())
        + clone_scan.draft_clone_warnings
        + clone_scan.unknown_clone_warnings
        + clone_scan.partial_clone_warnings
    )

    print("AppliedLore text integrity audit")
    print(f"root={root}")
    print(f"packet_glob={','.join(packet_globs)}")
    print(f"production_packets={production_scan.files}")
    print(f"production_fail_markers {format_counts(production_scan.fail_counts)}")
    print(f"production_broad_markers {format_counts(production_scan.broad_counts)}")
    print(f"production_exact_mojibake={production_scan.exact_mojibake_hits}")
    print(f"generated_pages={generated_scan.files}")
    print(f"generated_fail_markers {format_counts(generated_scan.fail_counts)}")
    print(f"generated_broad_markers {format_counts(generated_scan.broad_counts)}")
    print(f"generated_exact_mojibake={generated_scan.exact_mojibake_hits}")
    print(
        "clone_scan "
        f"en_baselines={clone_scan.baselines} "
        f"non_en_compared={clone_scan.compared} "
        f"title_exact={clone_scan.title_exact} "
        f"body_exact={clone_scan.body_exact} "
        f"both_exact={clone_scan.both_exact} "
        f"draft_clone_warnings={clone_scan.draft_clone_warnings} "
        f"unknown_clone_warnings={clone_scan.unknown_clone_warnings} "
        f"partial_clone_warnings={clone_scan.partial_clone_warnings} "
        f"ready_clone_failures={clone_scan.ready_clone_failures} "
        f"missing_en_baselines={clone_scan.missing_baselines}"
    )

    print_samples("fail_samples:", production_scan.fail_samples + generated_scan.fail_samples)
    print_samples("decode_failures:", production_scan.decode_failures + generated_scan.decode_failures)
    print_samples(
        "exact_mojibake_samples:",
        production_scan.exact_mojibake_samples + generated_scan.exact_mojibake_samples,
    )
    print_samples("broad_marker_samples:", production_scan.broad_samples + generated_scan.broad_samples)
    print_samples("clone_warning_samples:", clone_scan.samples)
    print_samples("clone_failure_samples:", clone_scan.failure_samples)

    if hard_failures:
        print("FINAL: FAIL")
        return 1
    if warnings:
        print("FINAL: WARN")
        return 0

    print("FINAL: PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument(
        "--packet-glob",
        default=DEFAULT_PACKET_GLOB,
        help="Comma-separated generated page filename/stem globs. Example: P45[6-9]*,P460*",
    )
    parser.add_argument(
        "--sample-p456-p460",
        action="store_true",
        help="Shortcut for --packet-glob P45[6-9]*,P460*.",
    )
    parser.add_argument("--sample-limit", type=int, default=DEFAULT_SAMPLE_LIMIT, help="Maximum sample paths per section.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    packet_glob = SAMPLE_PACKET_GLOB if args.sample_p456_p460 else args.packet_glob
    try:
        return run(root, parse_packet_globs(packet_glob), max(args.sample_limit, 0))
    except AuditFailure as exc:
        print(f"AppliedLore text integrity audit FAILED: {exc}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
