#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json

from H8VerifyCore import path, require_aligned


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", default="Docs/AgentLogs/VerifyTideBaker_TIDE_FOURIER_BAKER.json")
    args = parser.parse_args()
    require_aligned("Data/Environment/Tide_Harmonics.bin")
    require_aligned("Data/Environment/Tide_Harmonics_Low.bin")
    require_aligned("Data/Environment/Tide_Harmonics_Ultra.bin")
    payload = {"status": "PASS", "errors": [], "report": args.report}
    out = path(args.report)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(payload, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
