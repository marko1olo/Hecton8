#!/usr/bin/env python3
"""Detect design-document voice inside player-facing HECTON-8 lore text.

WHAT THIS TOOL IS FOR
    `writing.md` Prime Law is "write artifacts, not specifications". The AppliedLore corpus violated it in
    a specific, mechanical way: production design documents were fed to a generator, wrapped in packet
    clothing, exported to `in_game_wiki` / `external_site`, and translated into 15 locales. The result is
    player-facing text that addresses "the player", explains the game's own design intent, and prescribes
    how surfaces should behave.

    The canonical illustration is
    `Docs/Lore/AppliedContent/in_game_wiki/en_US/P217_IN_GAME_WIKI_UNLOCK_TIER_RULES.md`: the rule about
    how the in-game wiki unlocks, published in the in-game wiki, as a thing the player unlocks, with a
    biome and a POI assigned. Its `## Scanner` section reads "The PDA should explain only what the player
    has physically earned." A design instruction rendered as sensor output. `writing.md:794-809` documents
    that exact shape as forbidden.

WHAT THIS TOOL IS NOT FOR
    Acceptance. `writing.md` LLM Style Suppression Law is explicit: "detection is triage only; manual
    redline is acceptance". A clean run here means "no known pattern matched", never "this prose is good".
    Passing this validator is necessary and nowhere near sufficient.

    It also cannot see synonym evasion. `writing.md` AI Phrase Family Quarantine says a banned phrase
    rewritten with synonyms is still banned because it performs the same move. Regex sees the phrase, not
    the move. Detectors here are deliberately grouped into FAMILIES so a reviewer reads the family name and
    goes looking for the move, but a determined paraphrase will slip through.

USAGE
    python -B Tools/ValidateAppliedLoreArtifactVoice.py --glob "Docs/Lore/AppliedContent/in_game_wiki/en_US/*.md"
    python -B Tools/ValidateAppliedLoreArtifactVoice.py --glob "Docs/Lore/Grand_Library/*_en_US.md" --json
    python -B Tools/ValidateAppliedLoreArtifactVoice.py --glob "..." --write-baseline Tools/data/artifact_voice_baseline.json
    python -B Tools/ValidateAppliedLoreArtifactVoice.py --glob "..." --baseline Tools/data/artifact_voice_baseline.json --fail-on-error

    --baseline records existing debt so CI can fail only on NEW violations while the corpus is repaired
    incrementally. Shrinking a baseline is progress; growing one is a regression.
"""

from __future__ import annotations

import argparse
import glob as globmod
import json
import re
import sys
from dataclasses import dataclass, asdict
from pathlib import Path

# --------------------------------------------------------------------------------------- rule families
# Each entry: (family, pattern, authority citation, human explanation)
# Patterns run against the page BODY only. Frontmatter carries machine fields and legitimately contains
# words like "spoiler_tier" that would otherwise trip the production-vocabulary detectors.

HARD: list[tuple[str, str, str, str]] = [
    (
        "addresses_the_player",
        r"\bthe player(?:'s|s)?\b|\bplayers\b",
        "narrative.md 1C / writing.md Anti-AI Prose Ban",
        "player-facing text must not talk about the player; narrative.md bans 'the player learns that...'",
    ),
    (
        "talks_about_the_game",
        r"\bHECTON-8 (?:should|must|works|needs|is at its best)\b|\bthe game(?:'s)?\b|\bgameplay\b|\bonboarding\b|\bworldbuilding\b|\bnoir works\b",
        "writing.md Anti-AI Prose Ban",
        "an in-world document cannot discuss the game it appears in",
    ),
    (
        "prescriptive_design",
        r"\b(?:should|must)\s+(?:explain|reveal|keep|stay|read|render|display|show|teach|feel|remain|convey)\b",
        "writing.md Prime Law (artifacts, not specifications)",
        "prescriptive design voice: instructs how a surface ought to behave",
    ),
    (
        "authoring_prohibition",
        r"\bDo not (?:reveal|use|dump|add|write|include|replace|make)\b",
        "writing.md Prime Law",
        "an authoring prohibition addressed to a writer, not a statement inside the world",
    ),
    (
        "surface_prescription",
        r"\b(?:PDA|codex|wiki|scanner|terminal|UI|HUD)\s+(?:should|must|is a receipt|explains only)\b",
        "writing.md Surface Truth Contract",
        "tells a game surface how to behave instead of being output from it",
    ),
    (
        "production_vocabulary",
        r"\b(?:art brief|image brief|copy deck|UI copy|localization backlog|native review|release gate|unlock tier|spoiler gate|scene brief|placement backlog|authoring note)\b",
        "writing.md AppliedContent Packet Shape",
        "production-pipeline vocabulary in player-facing text",
    ),
    (
        "essay_framing",
        r"\bthis (?:article|entry|section|page|packet)\b[^.\n]{0,40}?\b(?:explores|examines|shows|explains|defines|covers|describes)\b",
        "writing.md AI Phrase Family Quarantine (essay-framing)",
        "tells the reader what the text is doing",
    ),
    (
        "thesis_contrast",
        r"\bnot (?:just|merely|simply|only)\b[^.\n]{2,60}?\bbut\b|\bmore than (?:just|simply)\b|\bat once\b[^.\n]{2,40}?\band\b",
        "writing.md LLM Style Suppression Law (balanced thesis)",
        "banned balanced-reveal shape 'not merely X; it is Y'",
    ),
    (
        "museum_label",
        r"\b(?:serves as|stands as|a testament to|bears witness|testament|symbol of|reminder of|reflection of)\b",
        "writing.md AI Phrase Family Quarantine (museum-label prose)",
        "museum-label prose",
    ),
    (
        "at_its_core",
        r"\bat its core\b|\bin essence\b|\bin a world where\b|\ba delicate balance\b|\ba unique blend\b|\bthe real horror\b",
        "writing.md Anti-AI Prose Ban (hard-ban list)",
        "explicitly hard-banned phrase",
    ),
    (
        "organic_metaphor",
        r"\bone body\b|\bone skin\b|\bone tissue\b|\bcorridor as gut\b|\bwall as organ\b|\bcable blooms\b",
        "writing.md Anti-AI Prose Ban (organic metaphor spam)",
        "organic metaphor outside literal xenobiology",
    ),
    (
        # writing.md permits these words when they are LITERAL and sourced: "'pulse' is allowed for
        # pressure, signal, power, sonar, pump cadence, or biological rhythm with observed evidence".
        # LITERAL_SENSORY_CONTEXT below suppresses the hit when real acoustics/power/biology is present,
        # so a brine-mirror sonar article is not punished for saying "echoes".
        "fake_sensory",
        r"\b(?:whispers|breathes|echoes|hums|sings|hungers)\b",
        "writing.md AI Phrase Family Quarantine (fake sensory fog)",
        "sensory verb with no sound, pressure, power, signal or organism behind it",
    ),
    (
        "concept_as_actor",
        r"\bthe (?:system|ocean|colony|process|factory|debt|sea)\s+(?:remembers|decides|chooses|wants|knows|judges|forgives|demands|rewrites|reclaims)\b",
        "writing.md AI Phrase Family Quarantine (concept-as-actor)",
        "abstraction performing a moral or metaphysical action with no owner",
    ),
]


# A sensory verb is only "fake sensory fog" when nothing real produces the sensation. When the surrounding
# text is actually about acoustics, power, signal, pressure or an organism, the word is literal and
# writing.md Risk Word And Rhythm Firewall explicitly allows it. Checked against the paragraph, not the
# sentence, because the sonar apparatus is usually established a clause or two earlier.
LITERAL_SENSORY_CONTEXT = re.compile(
    r"\b(?:sonar|ping|pinger|acoustic|hydrophone|carrier|transducer|decibel|dB|Hz|kHz|"
    r"frequency|delay|reverberation|density boundary|brine mirror|thermocline|"
    r"pump|compressor|scrubber|valve|turbine|generator|relay|signal|antenna|"
    r"network|packet|beacon|telemetry|inbound|transmission|channel|uplink|"
    r"organism|gill|siphon|colony of|biofilm|vocal|whale|fauna)\b",
    re.IGNORECASE,
)

SENSORY_FAMILY = "fake_sensory"


@dataclass(frozen=True)
class Finding:
    file: str
    line: int
    family: str
    authority: str
    why: str
    text: str

    def key(self) -> str:
        return f"{self.file}|{self.family}|{self.text.strip()[:120]}"


def split_frontmatter(raw: str) -> tuple[int, str]:
    """Return (body_start_line_index, body). Frontmatter is a leading '---' fenced block."""
    lines = raw.splitlines()
    if not lines or lines[0].strip() != "---":
        return 0, raw
    for i in range(1, len(lines)):
        if lines[i].strip() == "---":
            return i + 1, "\n".join(lines[i + 1 :])
    return 0, raw


def strip_code_fences(lines: list[str]) -> list[bool]:
    """Mark lines inside ``` fences. Quoted terminal/manifest blocks are in-world artifacts and are
    exactly what we asked writers to add, so scanning them for 'design voice' produces false hits."""
    inside = False
    flags = []
    for ln in lines:
        if ln.lstrip().startswith("```"):
            inside = not inside
            flags.append(True)
            continue
        flags.append(inside)
    return flags


def scan_text(path: str, raw: str) -> list[Finding]:
    offset, body = split_frontmatter(raw)
    lines = body.splitlines()
    fenced = strip_code_fences(lines)
    out: list[Finding] = []
    for idx, line in enumerate(lines):
        if fenced[idx]:
            continue
        if line.lstrip().startswith("<!--"):
            continue
        # Paragraph window: a literal sonar/power/organism source is often established a line or two above.
        window = "\n".join(lines[max(0, idx - 2) : idx + 3])
        for family, pattern, authority, why in HARD:
            if family == SENSORY_FAMILY and LITERAL_SENSORY_CONTEXT.search(window):
                continue
            for m in re.finditer(pattern, line, re.IGNORECASE):
                out.append(
                    Finding(
                        file=path.replace("\\", "/"),
                        line=offset + idx + 1,
                        family=family,
                        authority=authority,
                        why=why,
                        text=m.group(0),
                    )
                )
    out.extend(scan_structural(path, offset, lines, fenced))
    return out


def scan_structural(path: str, offset: int, lines: list[str], fenced: list[bool]) -> list[Finding]:
    """Shape-level checks that a per-line regex cannot see."""
    out: list[Finding] = []
    p = path.replace("\\", "/")

    # A heading that addresses the player breaks the fiction at document-structure level.
    for idx, line in enumerate(lines):
        if fenced[idx] or not line.lstrip().startswith("#"):
            continue
        if re.search(r"\bplayer\b|\bgameplay\b", line, re.IGNORECASE):
            out.append(
                Finding(p, offset + idx + 1, "heading_addresses_player",
                        "narrative.md 1C",
                        "a section heading in an in-world document addressed to the player",
                        line.strip()))

    prose = [
        (i, ln.strip())
        for i, ln in enumerate(lines)
        if ln.strip() and not fenced[i] and not ln.lstrip().startswith(("#", ">", "-", "*", "|", "<!--"))
    ]

    # Repeated clause rhythm: 3+ consecutive sentences opening "A <noun> <verb>s".
    # writing.md names this: "repeated 'X can be Y' rhythm used to fake discovery".
    for i, (lineno, text) in enumerate(prose):
        sentences = [s.strip() for s in re.split(r"(?<=[.!?])\s+", text) if s.strip()]
        run = 0
        for s in sentences:
            if re.match(r"^A[n]?\s+\w+(?:\s+\w+)?\s+(?:can|is|are|was|were|needs|brings|carries|becomes|makes)\b", s):
                run += 1
                if run >= 3:
                    out.append(
                        Finding(p, offset + lineno + 1, "repeated_clause_rhythm",
                                "writing.md Anti-AI Prose Ban / Risk Word And Rhythm Firewall",
                                f"{run} consecutive sentences share one clause shape, faking discovery",
                                s[:110]))
                    break
            else:
                run = 0

    # Long-surface body with no evidence anchor at all (Paragraph Evidence Firewall).
    body_text = "\n".join(t for _, t in prose)
    if len(body_text) > 700:
        has_id = re.search(r"\b[A-Z]{2,}[-–][A-Z0-9]{1,6}(?:[-–][A-Z0-9]{1,6})?\b", body_text)
        has_qty = re.search(r"\b\d+(?:\.\d+)?\s*(?:m|mm|cm|km|MPa|kPa|bar|kg|t|tonne|percent|%|Hz|K|C|V|A|min|h)\b", body_text)
        has_date = re.search(r"\b(?:19|20|21|22)\d{2}\b", body_text)
        if not (has_id or has_qty or has_date):
            out.append(
                Finding(p, offset + 1, "no_evidence_anchor",
                        "writing.md Paragraph Evidence Firewall",
                        "long body with no document ID, quantity, unit, or date anywhere in it",
                        body_text[:110]))
    return out


def load_baseline(path: str | None) -> set[str]:
    if not path:
        return set()
    f = Path(path)
    if not f.exists():
        return set()
    data = json.loads(f.read_text(encoding="utf-8"))
    return set(data.get("keys", []))


def main() -> int:
    # This corpus is 15 locales including CJK, Arabic and Hebrew. On a Windows machine with a non-UTF-8
    # locale (this project's dev box defaults to cp1251) print() would encode stdout in that codepage and
    # silently corrupt the output - which produced an undecodable --json payload before this line existed.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(description="Detect design-document voice in player-facing lore text.")
    ap.add_argument("--glob", action="append", required=True, help="glob of files to scan (repeatable)")
    ap.add_argument("--json", action="store_true", help="emit JSON")
    ap.add_argument("--baseline", help="known-debt file; only NEW findings are failures")
    ap.add_argument("--write-baseline", help="write current findings as the accepted baseline")
    ap.add_argument("--fail-on-error", action="store_true", help="exit non-zero when findings remain")
    ap.add_argument("--max-print", type=int, default=60, help="max findings to print in text mode")
    args = ap.parse_args()

    files: list[str] = []
    for g in args.glob:
        files.extend(sorted(globmod.glob(g, recursive=True)))
    files = [f for f in files if Path(f).is_file()]

    findings: list[Finding] = []
    for f in files:
        try:
            raw = Path(f).read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as exc:
            print(f"SKIP {f}: {exc}", file=sys.stderr)
            continue
        findings.extend(scan_text(f, raw))

    if args.write_baseline:
        out = Path(args.write_baseline)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(
            json.dumps({"keys": sorted({x.key() for x in findings})}, indent=1, ensure_ascii=False),
            encoding="utf-8",
        )
        print(f"baseline written: {out} ({len({x.key() for x in findings})} accepted findings)")
        return 0

    baseline = load_baseline(args.baseline)
    fresh = [x for x in findings if x.key() not in baseline]

    if args.json:
        print(json.dumps(
            {
                "acceptance_note": "TRIAGE ONLY. writing.md: detection cannot accept prose; manual redline is acceptance.",
                "files_scanned": len(files),
                "findings_total": len(findings),
                "findings_new": len(fresh),
                "by_family": {fam: sum(1 for x in findings if x.family == fam)
                              for fam in sorted({x.family for x in findings})},
                "findings": [asdict(x) for x in fresh],
            },
            indent=1, ensure_ascii=False))
    else:
        print("=" * 78)
        print(" ARTIFACT VOICE CHECK -- TRIAGE ONLY, NOT ACCEPTANCE")
        print(" writing.md: 'detection is triage only; manual redline is acceptance'.")
        print(" A clean run means no known pattern matched. It does not mean the prose is good.")
        print("=" * 78)
        print(f"files scanned      : {len(files)}")
        print(f"findings total     : {len(findings)}")
        if baseline:
            print(f"accepted baseline  : {len(baseline)}")
            print(f"NEW findings       : {len(fresh)}")
        by_fam: dict[str, int] = {}
        for x in findings:
            by_fam[x.family] = by_fam.get(x.family, 0) + 1
        if by_fam:
            print("\nby family:")
            for fam, n in sorted(by_fam.items(), key=lambda kv: -kv[1]):
                print(f"  {n:6}  {fam}")
        worst: dict[str, int] = {}
        for x in findings:
            worst[x.file] = worst.get(x.file, 0) + 1
        if worst:
            print("\nworst files:")
            for fpath, n in sorted(worst.items(), key=lambda kv: -kv[1])[:15]:
                print(f"  {n:4}  {fpath}")
        if fresh:
            print(f"\nfindings{' (new only)' if baseline else ''}:")
            for x in fresh[: args.max_print]:
                print(f"  {x.file}:{x.line}  [{x.family}]")
                print(f"        matched : {x.text.strip()[:100]}")
                print(f"        rule    : {x.authority}")
                print(f"        why     : {x.why}")
            if len(fresh) > args.max_print:
                print(f"  ... {len(fresh) - args.max_print} more (use --json for all)")

    if args.fail_on_error and fresh:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
