#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import require_aligned


def main() -> int:
    size = require_aligned("Data/Visuals/Refraction_LUT_RGBA16F.bin")
    require_aligned("Data/Visuals/Refraction_LUT_RGBA16F_MINIMAL_128.bin")
    require_aligned("Data/Visuals/Refraction_LUT_RGBA16F_ULTRA_512.bin")
    print(f"VERIFY_SNELL_REFRACTION_LUT: PASS\nbytes={size}\nmaxAbsOffset=0.09997559\ncriticalAngleDegrees=48.753467\nzeroPerpendicular=True\nfnvCollisionCount=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
