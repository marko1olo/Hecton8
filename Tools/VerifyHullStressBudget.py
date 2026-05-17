#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json

from H8VerifyCore import path, require, require_aligned


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-report", default="Data/Habitat/HabitatPressureBudget_Verification.json")
    parser.add_argument("--economy-json", default="")
    args = parser.parse_args()
    size = require_aligned("Data/Habitat/HabitatPressureBudget.h8bin")
    report = {"status": "PASS", "binaryBytes": size, "alignment": 16, "runtimeProof": "PENDING_VERIFICATION"}
    out = path(args.write_report)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"verification_report={out}\nstatus=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
