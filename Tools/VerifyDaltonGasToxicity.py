#!/usr/bin/env python3
from __future__ import annotations

import json

from H8VerifyCore import read_json, require, require_aligned


def verify(base_dir=None):
    manifest = read_json("Data/Precomputed/dalton_gas_toxicity_manifest.json") if base_dir is None else json.loads((base_dir / "dalton_gas_toxicity_manifest.json").read_text(encoding="utf-8"))
    binary = "Data/Precomputed/dalton_gas_toxicity.bin"
    size = require_aligned(binary) if base_dir is None else (base_dir / "dalton_gas_toxicity.bin").stat().st_size
    require(str(manifest.get("rowFormat", manifest.get("endianness", "<"))).startswith("<") or manifest.get("endianness") == "<", "Dalton manifest must be little-endian")
    require("Dalton" in str(manifest) or "dalton" in str(manifest), "Dalton basis missing from manifest")
    return {
        "status": "VERIFY_DALTON_GAS_TOXICITY_PASS",
        "binary": binary,
        "bytes": size,
        "aligned16": size % 16 == 0,
        "endianness": "<",
        "fnvCollisionCount": int(manifest.get("fnvCollisionCount", 0)),
        "toasterBytes": require_aligned("Data/Precomputed/dalton_gas_toxicity_toaster.bin") if base_dir is None else 0,
        "overkillBytes": require_aligned("Data/Precomputed/dalton_gas_toxicity_overkill.bin") if base_dir is None else 0,
    }


def main() -> int:
    print(json.dumps(verify(), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
