#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SIM_PATH = ROOT / "Tools" / "AiPathSim.py"
TUNING_PATH = ROOT / "Data" / "AI" / "Navigation_Tuning.json"


def load_sim_module():
    spec = importlib.util.spec_from_file_location("ai_path_sim", SIM_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load simulator module from {SIM_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def main() -> int:
    sim = load_sim_module()
    try:
        data = json.loads(TUNING_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print("AI NAV VERIFY FAILED")
        print(f"- invalid tuning JSON: {exc}")
        return 1

    valid, errors = sim.validate_export(data)
    if not valid:
        print("AI NAV VERIFY FAILED")
        for error in errors:
            print(f"- {error}")
        return 1

    binary_cache = data["binaryCache"]
    header_audit = binary_cache["headerAudit"]
    hash_audit = binary_cache["hashAudit"]
    performance = data["performanceModel"]
    math_audit = data["mathAudit"]["hardScience"]
    toaster = data["toasterData"]
    rtx = data["rtxOverkillData"]

    print("AI NAV VERIFY PASSED")
    print(f"json={TUNING_PATH.relative_to(ROOT).as_posix()}")
    print(
        "binary="
        f"{binary_cache['path']} bytes={binary_cache['fileBytes']} records={binary_cache['recordCount']}"
    )
    print(f"manifest={binary_cache['manifestPath']}")
    print(
        "layout="
        f"header={binary_cache['headerStruct']} record={binary_cache['recordStruct']} "
        f"alignment={binary_cache['alignmentBytes']} endian={binary_cache['endianness']}"
    )
    print(
        "crc="
        f"header={header_audit['headerCrc32']} payload={header_audit['payloadCrc32']} "
        f"semantic={header_audit['semanticHashCrc32']} reservedZero={header_audit['reservedZero']}"
    )
    print(
        "hash="
        f"algorithm={hash_audit['algorithm']} records={hash_audit['recordCount']} "
        f"fnvCollisions={hash_audit['collisionCount']} sorted={hash_audit['sortedHashes']}"
    )
    print(
        "performance="
        f"predators={performance['predators']} cadenceHz={performance['cadenceHz']} "
        f"samplesPerSecond={performance['samplesPerSecond']} "
        f"scalarOpsFrameHigh={performance['estimatedScalarOpsPerFrameHigh']}"
    )
    print(
        "math="
        f"flowBasis={math_audit['flowBoostResistance']['basis']} "
        f"sdfBasis={math_audit['obstacleRepulsion']['basis']}"
    )
    print(
        "scalability="
        f"toaster={toaster['profile']} rtx={rtx['profile']} "
        f"extraFields={len(rtx['extraDataFields'])}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
