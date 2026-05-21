#!/usr/bin/env python3
"""Static scan for managed voice/dialogue AudioClip routes.

This is a proof artifact for SHINOBU_260. It does not fail on music/SFX clips;
it separates voice-dialogue suspects from unrelated acoustic pools.
"""

from __future__ import annotations

import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
SCRIPT_ROOT = ROOT / "Assets" / "_Project" / "Scripts"
REPORT = ROOT / "Docs" / "Reports" / "AUDIO_OPTIMIZATION_REPORT.json"

DIRECTOR_VOICE_HINTS = re.compile(r"(PlayVoiceLine|VocalCue|VocalWarningSystem|protagonist|dialogue|VO_)", re.IGNORECASE)
CLIP_PATTERNS = [
    re.compile(r"\bAudioClip\b"),
    re.compile(r"\bAudioSource\b"),
    re.compile(r"PlayVoiceLine\s*\("),
    re.compile(r"PlayOneShot\s*\("),
]


def scan() -> dict[str, object]:
    voice_hits: list[dict[str, object]] = []
    all_hits: list[dict[str, object]] = []
    for path in sorted(SCRIPT_ROOT.rglob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        rel = path.relative_to(ROOT).as_posix()
        for line_no, line in enumerate(text.splitlines(), 1):
            if not any(p.search(line) for p in CLIP_PATTERNS):
                continue
            hit = {"file": rel, "line": line_no, "text": line.strip()[:240]}
            all_hits.append(hit)
            if DIRECTOR_VOICE_HINTS.search(rel) or DIRECTOR_VOICE_HINTS.search(line):
                voice_hits.append(hit)
    return {
        "agent": "SHINOBU_260",
        "managedAudioAssetsEradicated": len(voice_hits) == 0,
        "directorVoiceManagedAudioClipReferences": voice_hits,
        "allManagedAudioReferencesScanned": len(all_hits),
        "note": "Music, footsteps, atmospheric hallucination whispers, SFX, and third-party acoustic pools are not SHINOBU_260 director/protagonist voice ownership unless listed under directorVoiceManagedAudioClipReferences.",
    }


def main() -> int:
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    payload = scan()
    REPORT.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"[AudioClip_Reference_Scanner] wrote {REPORT}")
    print(f"[AudioClip_Reference_Scanner] director/protagonist voice suspects: {len(payload['directorVoiceManagedAudioClipReferences'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
