#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from datetime import datetime, timezone

import Decal_Projector_Inquisition as base


TRAUMA_ROUTE_PATTERNS = (
    re.compile(r"AddComponent\s*<\s*(?:Image|RawImage|DecalProjector)\s*>.*(?:blood|crack|wound|trauma|decal)", re.IGNORECASE),
    re.compile(r"new\s+GameObject\s*\([^)]*(?:blood|crack|wound|trauma|decal)", re.IGNORECASE),
    re.compile(r"Instantiate\s*\([^;]*(?:blood|crack|wound|trauma|decal)", re.IGNORECASE),
)


def main() -> int:
    files, skipped_large = base.iter_text_assets()
    candidate_files = base.collect_candidate_paths(files)
    token_hits: list[dict[str, object]] = []
    active_violations: list[dict[str, object]] = []
    active_renderer_features: list[dict[str, object]] = []
    inactive_renderer_feature_count = 0

    for path in candidate_files:
        text = base.read_text(path)
        active, inactive = base.scan_active_renderer_features(path, text)
        active_renderer_features.extend(active)
        inactive_renderer_feature_count += inactive

        for line_index, line in enumerate(text.splitlines(), start=1):
            if "DecalProjector" in line:
                token_hits.append({"path": base.relative(path), "line": line_index, "text": line.strip()[:180]})

            if any(pattern.search(line) for pattern in base.ACTIVE_DECAL_PATTERNS) or any(pattern.search(line) for pattern in TRAUMA_ROUTE_PATTERNS):
                active_violations.append({"path": base.relative(path), "line": line_index, "text": line.strip()[:180]})

    passed = len(active_violations) == 0 and len(active_renderer_features) == 0
    entry = {
        "agentId": "SHINOBU_325",
        "scanner": "Trauma_Projector_Inquisition",
        "timestampUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "evidenceClass": "STATIC_SOURCE_TARGETED",
        "scannedAssetCount": len(files),
        "candidateAssetCount": len(candidate_files),
        "skippedLargeAssetCount": skipped_large,
        "decalProjectorTokenHits": len(token_hits),
        "activeGameObjectTraumaDecalViolations": len(active_violations),
        "activeDecalRendererFeatureViolations": len(active_renderer_features),
        "inactiveUrpDecalRendererFeatures": inactive_renderer_feature_count,
        "oopTraumaDecalsPurged": passed,
        "status": "PASS" if passed else "FAIL",
        "tokenHits": token_hits,
        "activeViolations": active_violations,
        "activeRendererFeatures": active_renderer_features,
        "notes": "Inactive URP DecalRendererFeature assets are reported but not counted as active routes. Runtime trauma uses TraumaDecalDTO through _GlobalVisorTrauma in a RenderGraph fullscreen pass.",
    }

    if base.REPORT_PATH.exists():
        try:
            report = json.loads(base.REPORT_PATH.read_text(encoding="utf-8-sig"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            report = {}
    else:
        report = {}

    report["shinobu_325_screen_space_trauma_decal_resolver"] = entry
    base.REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    base.REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=False) + "\n", encoding="utf-8")
    print(json.dumps(entry, indent=2, sort_keys=True))
    return 0 if passed else 2


if __name__ == "__main__":
    raise SystemExit(main())
