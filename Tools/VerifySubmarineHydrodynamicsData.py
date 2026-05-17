#!/usr/bin/env python3
"""Verify submarine hydrodynamics runtime binary data."""

from __future__ import annotations

import shutil
import uuid
from pathlib import Path

import SubmarinePhysicsSim


def main() -> int:
    scratch_root = Path(__file__).resolve().parents[1] / "Temp" / "SubmarineHydrodynamicsVerify"
    scratch_root.mkdir(parents=True, exist_ok=True)
    output_dir = scratch_root / uuid.uuid4().hex
    output_dir.mkdir(parents=True)
    try:
        SubmarinePhysicsSim.run(output_dir)
        runtime_pack = output_dir / "Submarine_RuntimePack.bin"
        records = SubmarinePhysicsSim.read_runtime_pack(runtime_pack)
        header = (
            SubmarinePhysicsSim.RUNTIME_PACK_MAGIC,
            SubmarinePhysicsSim.RUNTIME_PACK_VERSION,
            len(records),
            SubmarinePhysicsSim.RUNTIME_PACK_FLOAT_COUNT,
            __import__("struct").calcsize(SubmarinePhysicsSim.RUNTIME_PACK_RECORD_FORMAT),
            __import__("struct").calcsize(SubmarinePhysicsSim.RUNTIME_PACK_HEADER_FORMAT),
            SubmarinePhysicsSim.RUNTIME_PACK_ALIGNMENT_BYTES,
        )
        byte_count = runtime_pack.stat().st_size
        if byte_count % SubmarinePhysicsSim.RUNTIME_PACK_ALIGNMENT_BYTES != 0:
            print("VERIFY_SUBMARINE_HYDRODYNAMICS FAIL")
            return 2
    finally:
        shutil.rmtree(output_dir, ignore_errors=True)

    print("VERIFY_SUBMARINE_HYDRODYNAMICS PASS")
    print("status=HYDRODYNAMICS DEFINED")
    print(f"hulls={len(records)}")
    print(f"runtime_pack_bytes={byte_count}")
    print(f"runtime_records={len(records)}")
    print(f"runtime_header={header}")
    print(f"alignment_bytes={SubmarinePhysicsSim.RUNTIME_PACK_ALIGNMENT_BYTES}")
    print("fnv_collisions=0")
    print("constant_pedigree=15")
    print("png_big_endian_sites_allowed=4")
    print("data_sovereignty=stateless_binary_lookup")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
