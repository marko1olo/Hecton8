#!/usr/bin/env python3
"""Spectrum verifier for HECTON-8 baked blue noise assets."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from GenerateBlueNoise import repository_root, verify_assets, write_json


def parse_args() -> argparse.Namespace:
    root = repository_root()
    parser = argparse.ArgumentParser(description="Verify HECTON-8 baked noise assets.")
    parser.add_argument("--noise", type=Path, default=root / "Data" / "Textures" / "BlueNoise_RGBA.png")
    parser.add_argument("--flow", type=Path, default=root / "Data" / "Textures" / "AbyssalFlowField_LowTier_RGBA.png")
    parser.add_argument("--metrics", type=Path, default=root / "Data" / "Textures" / "NoiseBakeMetrics.verify.json")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    metrics = verify_assets(args.noise.resolve(), args.flow.resolve())
    write_json(args.metrics.resolve(), metrics)
    print(json.dumps(metrics, indent=2, sort_keys=True))
    return 0 if metrics["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
