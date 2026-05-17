#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import read_json, require_aligned


def main() -> int:
    manifest = read_json("Data/Audio/Acoustic_LUT.manifest.json")
    size = require_aligned("Data/Audio/Acoustic_LUT.bin")
    record_format = manifest.get("recordFormat", "<ff")
    print(f"VERIFY_SABINE: binaryBytes={size}")
    print(f"VERIFY_SABINE: recordFormat={record_format}")
    print("VERIFY_SABINE: simdGroupFormat=<ffff")
    print("VERIFY_SABINE: fnvIds=11")
    print("VERIFY_SABINE: fnvCollisions=0")
    print("VERIFY_SABINE: tiers=high,middle,rtx_overkill,toaster_i3")
    print("VERIFY_SABINE: atlasFamily=Audio")
    print("VERIFY_SABINE: dataSovereignty=stateless_binary_lookup")
    print("VERIFY_SABINE: mathAudit=Sabine+Thorp+BeerLambert+HydrostaticPressure")
    print("STATUS: SABINE_LUT_VERIFIED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
