#!/usr/bin/env python3
"""Validate Grand Library source lore articles before publication/export.

Evidence class: STATIC_SOURCE only. This tool does not prove native
localization, runtime placement, Unity import health, DataMonolith, or h8bin
readiness.
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

try:
    from AppliedLoreImporter import TARGET_LOCALES as IMPORTED_TARGET_LOCALES
except ModuleNotFoundError:
    from Tools.AppliedLoreImporter import TARGET_LOCALES as IMPORTED_TARGET_LOCALES


TARGET_LOCALES = tuple(IMPORTED_TARGET_LOCALES)
ENGLISH_LOCALE = "en_US"
GRAND_LIBRARY_DIR = Path("Docs") / "Lore" / "Grand_Library"
APPLIED_PACKETS_DIR = Path("Docs") / "Lore" / "AppliedContent" / "packets"
APPLIED_RELEASE_SETS_DIR = Path("Docs") / "Lore" / "AppliedContent" / "release_sets"
ENGLISH_STATUS_TOKENS = ("source_authority", "source_authority_en_us")
DRAFT_STATUS_TOKENS = (
    "source_draft_pending_native_review",
    "draft_machine_or_llm",
    "blocked_translation_draft",
)
PACKET_REQUIRED_TEXT_FIELDS = ("title", "scanner", "terminal", "audio", "in_game_wiki", "external_site", "field_note")
PACKET_OPTIONAL_TEXT_FIELDS = ("external_site_article",)
PACKET_REFERENCE_PATH_FIELDS = ("external_site_article_path", "in_game_wiki_article_path", "generated_page_path")
PACKET_REFERENCE_SEARCH_ROOTS = (
    Path(""),
    Path("Docs") / "Lore" / "AppliedContent" / "external_site",
    Path("Docs") / "Lore" / "AppliedContent" / "in_game_wiki",
    Path("Docs") / "Lore" / "AppliedContent" / "articles",
)
READY_CLAIM_PATTERNS = tuple(
    re.compile(pattern, re.IGNORECASE | re.MULTILINE)
    for pattern in (
        r"^\s*(runtime_ready|publication_ready|native_localization_ready|native_ready)\s*[:=]\s*true\b",
        r"^\s*localization_status\s*:\s*(native|native_reviewed|publication_ready|runtime_ready|published)\b",
        r"<!--\s*localization_status\s*:\s*(native|native_reviewed|publication_ready|runtime_ready|published)\s*-->",
        r'"\s*status\s*"\s*:\s*"[^"]*(?:runtime_ready|publication_ready|native_localization_ready|native_ready|data_monolith_ready|h8bin_ready|unity_placement_ready)[^"]*"',
    )
)
ANTI_AI_PATTERNS = tuple(
    re.compile(pattern, re.IGNORECASE)
    for pattern in (
        r"\bthis entry explores\b",
        r"\bserves as a reminder\b",
        r"\ba testament to\b",
        r"\bmore than just\b",
        r"\bat its core\b",
        r"\bin a world where\b",
        r"\bunique blend\b",
        r"\brich tapestry\b",
        r"\bdelve(?:s|d|ing)? into\b",
        r"\bboth beautiful and terrifying\b",
        r"\bnot just\b.+\bbut\b",
        r"\bas an ai\b",
    )
)
PLACEHOLDER_PATTERNS = (
    re.compile(r"\bLOC HOLD\b", re.IGNORECASE),
    re.compile(r"\bTODO\b"),
    re.compile(r"\bTBD\b"),
    re.compile(r"\bPLACEHOLDER\b", re.IGNORECASE),
    re.compile(r"\bLOREM IPSUM\b", re.IGNORECASE),
    re.compile(r"\bTK\b"),
)
KNOWN_MOJIBAKE_PATTERNS = (
    ("replacement_char", re.compile("\ufffd")),
    ("question_mark_run", re.compile(r"\?{4,}")),
    ("utf8_as_latin1_c2_c3", re.compile(r"[\u00c2\u00c3][\u0080-\u00bf]")),
    ("utf8_as_latin1_d0_d1", re.compile(r"[\u00d0\u00d1][\u0080-\u00bf]")),
    ("utf8_as_latin1_d8_d9", re.compile(r"[\u00d8\u00d9][\u0080-\u00bf]")),
    ("utf8_as_latin1_e2_punct", re.compile(r"\u00e2(?:[\u0080-\u009f]|\u20ac)")),
    ("utf8_as_latin1_e3_cjk", re.compile(r"\u00e3(?:[\u0080-\u009f]|\u0192|\u201a)")),
)
LOCALE_SUFFIX_PATTERN = re.compile(r"_(?P<locale>[a-z]{2}_[A-Z]{2})\.md$")


class ValidationSetupError(Exception):
    """Raised for invalid CLI setup, not content failures."""


@dataclass
class Counters:
    article_groups: int = 0
    files: int = 0
    inactive_locale_files: int = 0
    packet_bundles: int = 0
    packet_rows: int = 0
    release_manifests: int = 0
    failures: int = 0
    warnings: int = 0


@dataclass
class ValidationResult:
    counters: Counters = field(default_factory=Counters)
    messages: list[str] = field(default_factory=list)

    def fail(self, message: str) -> None:
        self.counters.failures += 1
        self.messages.append(f"FAIL: {message}")

    def warn(self, message: str) -> None:
        self.counters.warnings += 1
        self.messages.append(f"WARN: {message}")


@dataclass(frozen=True)
class ArticleFile:
    base_name: str
    locale: str
    path: Path


def parse_globs(value: str) -> tuple[str, ...]:
    return tuple(part.strip() for part in value.split(",") if part.strip()) or ("*",)


def rel(root: Path, path: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8-sig")
    except UnicodeDecodeError as exc:
        raise ValidationSetupError(f"invalid UTF-8 decode: {path}") from exc


def configure_stdout() -> None:
    """Keep localized validation failures printable on non-UTF-8 Windows consoles."""

    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except AttributeError:
        pass


def split_locale_suffix(path: Path) -> tuple[str, str] | None:
    name = path.name
    for locale in TARGET_LOCALES:
        suffix = f"_{locale}.md"
        if name.endswith(suffix):
            return name[: -len(suffix)], locale
    return None


def inactive_locale_suffix(path: Path) -> str | None:
    match = LOCALE_SUFFIX_PATTERN.search(path.name)
    if not match:
        return None
    locale = match.group("locale")
    if locale in TARGET_LOCALES:
        return None
    return locale


def matches_article_glob(base_name: str, path: Path, patterns: tuple[str, ...]) -> bool:
    return any(
        fnmatch.fnmatchcase(base_name, pattern)
        or fnmatch.fnmatchcase(path.stem, pattern)
        or fnmatch.fnmatchcase(path.name, pattern)
        for pattern in patterns
    )


def matches_path_glob(path: Path, patterns: tuple[str, ...]) -> bool:
    return any(
        fnmatch.fnmatchcase(path.stem, pattern) or fnmatch.fnmatchcase(path.name, pattern)
        for pattern in patterns
    )


def collect_articles(root: Path, article_globs: tuple[str, ...]) -> dict[str, dict[str, Path]]:
    library_root = root / GRAND_LIBRARY_DIR
    if not library_root.exists():
        raise ValidationSetupError(f"missing Grand Library directory: {library_root}")

    groups: dict[str, dict[str, Path]] = {}
    for path in sorted(library_root.glob("*.md"), key=lambda item: item.name.lower()):
        split = split_locale_suffix(path)
        if split is None:
            continue
        base_name, locale = split
        if not matches_article_glob(base_name, path, article_globs):
            continue
        groups.setdefault(base_name, {})[locale] = path

    return groups


def collect_inactive_locale_files(root: Path, article_globs: tuple[str, ...]) -> list[tuple[str, Path]]:
    library_root = root / GRAND_LIBRARY_DIR
    inactive: list[tuple[str, Path]] = []
    for path in sorted(library_root.glob("*.md"), key=lambda item: item.name.lower()):
        locale = inactive_locale_suffix(path)
        if locale is None:
            continue
        base_name = path.name[: -len(f"_{locale}.md")]
        if matches_article_glob(base_name, path, article_globs):
            inactive.append((locale, path))
    return inactive


def collect_packet_bundles(root: Path, packet_globs: tuple[str, ...]) -> list[Path]:
    packets_root = root / APPLIED_PACKETS_DIR
    if not packets_root.exists():
        raise ValidationSetupError(f"missing AppliedContent packets directory: {packets_root}")

    return [
        path
        for path in sorted(packets_root.glob("*.json"), key=lambda item: item.name.lower())
        if matches_path_glob(path, packet_globs)
    ]


def collect_release_manifests(root: Path, manifest_globs: tuple[str, ...]) -> list[Path]:
    manifests_root = root / APPLIED_RELEASE_SETS_DIR
    if not manifests_root.exists():
        raise ValidationSetupError(f"missing AppliedContent release_sets directory: {manifests_root}")

    return [
        path
        for path in sorted(manifests_root.glob("*_manifest.json"), key=lambda item: item.name.lower())
        if matches_path_glob(path, manifest_globs)
    ]


def title_present(text: str) -> bool:
    return bool(re.search(r"(?m)^#\s+\S", text))


def has_any_token(text: str, tokens: tuple[str, ...]) -> bool:
    lowered = text.lower()
    return any(token.lower() in lowered for token in tokens)


def validate_quality_text(result: ValidationResult, location: str, text: str) -> None:
    for label, pattern in KNOWN_MOJIBAKE_PATTERNS:
        if pattern.search(text):
            result.fail(f"{location}: mojibake marker detected: {label}")

    for pattern in ANTI_AI_PATTERNS:
        if pattern.search(text):
            result.fail(f"{location}: anti-AI prose pattern detected: {pattern.pattern}")

    for pattern in PLACEHOLDER_PATTERNS:
        if pattern.search(text):
            result.fail(f"{location}: placeholder token detected: {pattern.pattern}")

    for pattern in READY_CLAIM_PATTERNS:
        if pattern.search(text):
            result.fail(f"{location}: source content claims ready runtime/publication/native status")


def normalized_clone_text(text: str) -> str:
    text = re.sub(r"<!--.*?-->", "", text, flags=re.DOTALL)
    text = re.sub(r"\s+", "", text)
    return text.casefold()


def validate_article_file(
    result: ValidationResult,
    root: Path,
    article: ArticleFile,
    require_status_comment: bool,
) -> None:
    result.counters.files += 1
    location = rel(root, article.path)
    text = read_text(article.path)

    if not title_present(text):
        result.fail(f"{location}: missing markdown title")

    validate_quality_text(result, location, text)

    if not require_status_comment:
        return

    if article.locale == ENGLISH_LOCALE:
        if not has_any_token(text, ENGLISH_STATUS_TOKENS):
            result.fail(f"{location}: en_US source article must carry source_authority status")
        return

    if not has_any_token(text, DRAFT_STATUS_TOKENS):
        result.fail(f"{location}: non-English source article must carry draft localization status")


def validate_packet_runtime_contract(result: ValidationResult, root: Path, path: Path, bundle: dict) -> None:
    location = rel(root, path)
    runtime_contract = bundle.get("runtime_contract", {})
    if not isinstance(runtime_contract, dict):
        result.fail(f"{location}: missing runtime_contract object")
        return

    for key in (
        "runtime_ready",
        "native_localization_ready",
        "data_monolith_ready",
        "h8bin_ready",
        "unity_placement_ready",
        "generated_page_ready",
        "publication_ready",
    ):
        if runtime_contract.get(key) is True:
            result.fail(f"{location}: runtime_contract.{key} must not be true for source packets")


def packet_reference_exists(root: Path, source_value: str) -> bool:
    path = Path(source_value)
    if path.is_absolute():
        return path.exists()

    return any((root / search_root / path).exists() for search_root in PACKET_REFERENCE_SEARCH_ROOTS)


def validate_packet_reference_paths(
    result: ValidationResult,
    root: Path,
    row_location: str,
    row: dict,
) -> None:
    for field_name in PACKET_REFERENCE_PATH_FIELDS:
        if field_name not in row:
            continue

        value = row.get(field_name)
        if not isinstance(value, str) or not value.strip():
            result.fail(f"{row_location}.{field_name}: empty packet reference path")
            continue

        if not packet_reference_exists(root, value.strip()):
            result.fail(f"{row_location}.{field_name}: missing packet reference path: {value}")


def validate_packet_row(result: ValidationResult, root: Path, path: Path, packet_index: int, packet: dict) -> None:
    packet_id = str(packet.get("packet_id") or f"packet[{packet_index}]")
    localized = packet.get("localized")
    location = f"{rel(root, path)}:{packet_id}"
    if not isinstance(localized, dict):
        result.fail(f"{location}: missing localized object")
        return

    missing = [locale for locale in TARGET_LOCALES if locale not in localized]
    if missing:
        result.fail(f"{location}: missing active localized rows: {', '.join(missing)}")

    expected_optional_text_fields = tuple(
        field_name
        for field_name in PACKET_OPTIONAL_TEXT_FIELDS
        if any(
            isinstance(localized.get(locale), dict) and field_name in localized[locale]
            for locale in TARGET_LOCALES
        )
    )
    for locale in TARGET_LOCALES:
        row = localized.get(locale)
        if not isinstance(row, dict):
            continue
        result.counters.packet_rows += 1
        row_location = f"{location}:{locale}"

        status = str(row.get("localization_status") or "")
        if locale == ENGLISH_LOCALE:
            if not has_any_token(status, ("source_authority",)):
                result.fail(f"{row_location}: en_US packet row must carry source_authority status")
        elif not has_any_token(status, DRAFT_STATUS_TOKENS):
            result.fail(f"{row_location}: non-English packet row must carry draft localization status")

        for field_name in PACKET_REQUIRED_TEXT_FIELDS:
            value = row.get(field_name)
            if not isinstance(value, str) or not value.strip():
                result.fail(f"{row_location}.{field_name}: missing packet text")
                continue
            validate_quality_text(result, f"{row_location}.{field_name}", value)

        for field_name in expected_optional_text_fields:
            value = row.get(field_name)
            if not isinstance(value, str) or not value.strip():
                result.fail(f"{row_location}.{field_name}: missing optional packet text used by sibling locales")
                continue
            validate_quality_text(result, f"{row_location}.{field_name}", value)

        validate_packet_reference_paths(result, root, row_location, row)


def validate_packet_bundle(result: ValidationResult, root: Path, path: Path) -> None:
    result.counters.packet_bundles += 1
    location = rel(root, path)
    text = read_text(path)
    validate_quality_text(result, location, text)

    try:
        bundle = json.loads(text)
    except json.JSONDecodeError as exc:
        result.fail(f"{location}: invalid JSON: {exc}")
        return

    if not isinstance(bundle, dict):
        result.fail(f"{location}: packet bundle root must be an object")
        return

    validate_packet_runtime_contract(result, root, path, bundle)
    packets = bundle.get("packets")
    if not isinstance(packets, list) or not packets:
        result.fail(f"{location}: missing packets array")
        return

    for index, packet in enumerate(packets):
        if not isinstance(packet, dict):
            result.fail(f"{location}: packet[{index}] must be an object")
            continue
        validate_packet_row(result, root, path, index, packet)


def validate_release_manifest(result: ValidationResult, root: Path, path: Path) -> None:
    result.counters.release_manifests += 1
    location = rel(root, path)
    text = read_text(path)
    validate_quality_text(result, location, text)

    try:
        manifest = json.loads(text)
    except json.JSONDecodeError as exc:
        result.fail(f"{location}: invalid JSON: {exc}")
        return

    if not isinstance(manifest, dict):
        result.fail(f"{location}: release manifest root must be an object")
        return

    allowed_partial_ready_keys = {"canonical_importer_ready", "generated_page_ready"}
    for key, value in manifest.items():
        if key.endswith("_ready") and value is True and key not in allowed_partial_ready_keys:
            result.fail(f"{location}: {key} must not be true for source release manifests")
    if manifest.get("generated_page_ready") is True and manifest.get("canonical_importer_ready") is not True:
        result.fail(f"{location}: generated_page_ready requires canonical_importer_ready")

    for field_name in ("packet_sources", "article_sources"):
        sources = manifest.get(field_name)
        if not isinstance(sources, list) or not sources:
            result.fail(f"{location}: missing {field_name} array")
            continue
        missing_sources = [source for source in sources if not (root / source).exists()]
        if missing_sources:
            result.fail(f"{location}: missing {field_name} paths: {', '.join(missing_sources)}")

    article_sources = manifest.get("article_sources")
    if isinstance(article_sources, list) and len(article_sources) != len(TARGET_LOCALES):
        result.fail(
            f"{location}: article_sources must cover {len(TARGET_LOCALES)} active locales, found {len(article_sources)}"
        )


def validate_article_group(
    result: ValidationResult,
    root: Path,
    base_name: str,
    localized_paths: dict[str, Path],
    require_status_comment: bool,
) -> None:
    result.counters.article_groups += 1
    missing = [locale for locale in TARGET_LOCALES if locale not in localized_paths]
    if missing:
        result.fail(f"{base_name}: missing active locales: {', '.join(missing)}")

    duplicate_check = len(localized_paths) != len(set(localized_paths))
    if duplicate_check:
        result.fail(f"{base_name}: duplicate locale paths detected")

    english_path = localized_paths.get(ENGLISH_LOCALE)
    english_clone_text = normalized_clone_text(read_text(english_path)) if english_path else ""

    for locale, path in sorted(localized_paths.items(), key=lambda item: TARGET_LOCALES.index(item[0])):
        if locale != ENGLISH_LOCALE and english_clone_text:
            localized_clone_text = normalized_clone_text(read_text(path))
            if localized_clone_text == english_clone_text:
                result.fail(f"{rel(root, path)}: non-English article is an exact clone of en_US authority text")

        validate_article_file(
            result,
            root,
            ArticleFile(base_name=base_name, locale=locale, path=path),
            require_status_comment=require_status_comment,
        )


def run_validation(
    root: Path,
    article_glob: str,
    require_status_comment: bool,
    json_output: bool = False,
    packet_glob: str | None = None,
    release_manifest_glob: str | None = None,
) -> int:
    result = ValidationResult()
    article_globs = parse_globs(article_glob)
    groups = collect_articles(root, article_globs)
    if not groups:
        raise ValidationSetupError(f"no Grand Library articles selected by --article-glob {article_glob!r}")

    for base_name, localized_paths in sorted(groups.items()):
        validate_article_group(result, root, base_name, localized_paths, require_status_comment)

    inactive_locale_files = collect_inactive_locale_files(root, article_globs)
    result.counters.inactive_locale_files += len(inactive_locale_files)
    if inactive_locale_files:
        sample = ", ".join(
            f"{rel(root, path)} ({locale})" for locale, path in inactive_locale_files[:8]
        )
        suffix = "" if len(inactive_locale_files) <= 8 else f", +{len(inactive_locale_files) - 8} more"
        result.warn(
            "inactive Grand Library locale files are outside the active 15-locale roster and are not production coverage: "
            + sample
            + suffix
        )

    if packet_glob:
        packet_globs = parse_globs(packet_glob)
        packet_paths = collect_packet_bundles(root, packet_globs)
        if not packet_paths:
            raise ValidationSetupError(f"no AppliedContent packet bundles selected by --packet-glob {packet_glob!r}")
        for path in packet_paths:
            validate_packet_bundle(result, root, path)

    if release_manifest_glob:
        manifest_globs = parse_globs(release_manifest_glob)
        manifest_paths = collect_release_manifests(root, manifest_globs)
        if not manifest_paths:
            raise ValidationSetupError(
                f"no AppliedContent release manifests selected by --release-manifest-glob {release_manifest_glob!r}"
            )
        for path in manifest_paths:
            validate_release_manifest(result, root, path)

    if json_output:
        print(
            json.dumps(
                {
                    "counts": vars(result.counters),
                    "messages": result.messages,
                },
                ensure_ascii=False,
                indent=2,
            )
        )
    else:
        print("Grand Library lore quality validator")
        print(f"root={root.resolve()}")
        print(f"article_glob={article_glob}")
        if packet_glob:
            print(f"packet_glob={packet_glob}")
        if release_manifest_glob:
            print(f"release_manifest_glob={release_manifest_glob}")
        print(f"require_status_comment={str(require_status_comment).lower()}")
        for message in result.messages:
            print(message)
        print("--- Summary ---")
        for key, value in vars(result.counters).items():
            print(f"{key}: {value}")
        print("FINAL: FAIL" if result.counters.failures else "FINAL: PASS")

    return 1 if result.counters.failures else 0


def main() -> int:
    configure_stdout()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument(
        "--article-glob",
        default="*",
        help="Comma-separated Grand Library base-name/file globs.",
    )
    parser.add_argument(
        "--require-status-comment",
        action="store_true",
        help="Require source_authority/draft localization status tokens in selected articles.",
    )
    parser.add_argument("--json", action="store_true", help="Print machine-readable JSON.")
    parser.add_argument(
        "--packet-glob",
        default=None,
        help="Optional comma-separated AppliedContent packet bundle globs to validate.",
    )
    parser.add_argument(
        "--release-manifest-glob",
        default=None,
        help="Optional comma-separated AppliedContent release-set manifest globs to validate.",
    )
    args = parser.parse_args()

    try:
        return run_validation(
            Path(args.root),
            args.article_glob,
            args.require_status_comment,
            args.json,
            args.packet_glob,
            args.release_manifest_glob,
        )
    except ValidationSetupError as exc:
        print(f"Grand Library lore quality validator FAILED: {exc}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
