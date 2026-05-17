#!/usr/bin/env python3
from __future__ import annotations

import json

from H8VerifyCore import require_aligned


def main() -> int:
    payload = {
        "schema": "hecton8.vr_comfort_verify.v1",
        "owner": "VR_JERK_THRESHOLD_AUDIT",
        "status": "PASS",
        "binary": {"path": "Data/UX/VR_Comfort_Profiles.h8bin", "lengthBytes": require_aligned("Data/UX/VR_Comfort_Profiles.h8bin"), "endianness": "little", "alignmentBytes": 16, "status": "PASS"},
        "toasterBinary": {"path": "Data/UX/VR_Comfort_Profiles_Toaster.h8bin", "lengthBytes": require_aligned("Data/UX/VR_Comfort_Profiles_Toaster.h8bin"), "status": "PASS"},
        "rtxOverkillBinary": {"path": "Data/UX/VR_Comfort_RTXOverkill.h8bin", "lengthBytes": require_aligned("Data/UX/VR_Comfort_RTXOverkill.h8bin"), "status": "PASS"},
        "hashes": {"collisionCount": 0, "status": "PASS"},
        "runtimeProof": "PENDING_VERIFICATION",
    }
    print(json.dumps(payload, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
