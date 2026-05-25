#!/usr/bin/env python3
import json
import math
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PAGER = ROOT / "Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs"
PROCESSOR = ROOT / "Assets/_Project/Scripts/VoxelDeltaProcessor.cs"
COMPRESSION = ROOT / "Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs"
REPORT = ROOT / "Docs/Reports/VOXEL_PAGING_OPTIMIZATION_REPORT_1312.json"

DIRECTORY_SLOTS = 252
DIRECTORY_BYTES = 4096
DIRECTORY_HEADER_BYTES = 64
DIRECTORY_ENTRY_BYTES = 16
PAGER_PAYLOAD_BYTES = (256 * 1024) - 64
VOXEL_CELLS = 32 * 32 * 32
RLE_HEADER_BYTES = 32
RLE_RUN_BYTES = 8
DENSE_FALLBACK_BYTES = (VOXEL_CELLS // 8) + (VOXEL_CELLS * (2 + 1 + 1))


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def extract_method(source: str, signature: str) -> str:
    start = source.find(signature)
    if start < 0:
        return ""
    brace = source.find("{", start)
    if brace < 0:
        return ""
    depth = 0
    for index in range(brace, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[start:index + 1]
    return ""


def resolve_slot(sector_hash: int) -> int:
    mixed = sector_hash & 0xFFFFFFFFFFFFFFFF
    mixed ^= mixed >> 33
    mixed = (mixed * 0xFF51AFD7ED558CCD) & 0xFFFFFFFFFFFFFFFF
    mixed ^= mixed >> 33
    return mixed % DIRECTORY_SLOTS


def next_u64(value: int) -> int:
    value ^= value >> 12
    value &= 0xFFFFFFFFFFFFFFFF
    value ^= (value << 25) & 0xFFFFFFFFFFFFFFFF
    value ^= value >> 27
    return (value * 0x2545F4914F6CDD1D) & 0xFFFFFFFFFFFFFFFF


def fuzzer(samples: int = 10000) -> dict:
    counts = [0] * DIRECTORY_SLOTS
    value = 0x1312D17EC70B5EED
    for _ in range(samples):
        value = next_u64(value)
        counts[resolve_slot(value)] += 1
    unique = sum(1 for count in counts if count > 0)
    mean = samples / DIRECTORY_SLOTS
    variance = sum((count - mean) * (count - mean) for count in counts) / DIRECTORY_SLOTS
    return {
        "samples": samples,
        "uniqueSlots": unique,
        "allSlotsReachable": unique == DIRECTORY_SLOTS,
        "slotCollisionFreePossible": False,
        "slotCollisionFreeReason": "10000 sector hashes cannot map injectively into 252 directory slots.",
        "minBucket": min(counts),
        "maxBucket": max(counts),
        "stdDevBucket": math.sqrt(variance),
    }


def main() -> int:
    pager = read(PAGER)
    processor = read(PROCESSOR)
    compression = read(COMPRESSION)

    resolve_directory_slot = extract_method(pager, "private static int ResolveDirectorySlot")
    ensure_compaction = extract_method(processor, "private void EnsureCompactionScratchBuffers")
    ensure_snapshot = extract_method(processor, "private void EnsureNativeSnapshotScratchBuffer")

    old_reachable_slots = len({value & (DIRECTORY_SLOTS - 1) for value in range(512)})
    dead_slots = DIRECTORY_SLOTS - old_reachable_slots
    max_safe_runs = (PAGER_PAYLOAD_BYTES - RLE_HEADER_BYTES) // RLE_RUN_BYTES
    checkerboard_rle_bytes = RLE_HEADER_BYTES + (VOXEL_CELLS * RLE_RUN_BYTES)
    checkerboard_overflow_bytes = checkerboard_rle_bytes - PAGER_PAYLOAD_BYTES

    checks = {
        "directoryModuloPresent": "% (ulong)DirectorySlotCount" in resolve_directory_slot,
        "directoryMaskRemoved": "DirectorySlotCount - 1" not in resolve_directory_slot and "& (ulong)" not in resolve_directory_slot,
        "directoryPageStillFits": DIRECTORY_HEADER_BYTES + (DIRECTORY_SLOTS * DIRECTORY_ENTRY_BYTES) == DIRECTORY_BYTES - 0,
        "denseFallbackFlagPresent": "HeaderFlagDenseFallback" in compression,
        "rleLimitPresent": "MaxVoxelDeltaRleRunsPerWalPayload" in compression,
        "fatalRleOverflowClearedByFallback": "flags = (flags & ~HeaderFlagFatal) | HeaderFlagDenseFallback" in compression,
        "denseFallbackPayloadConstant135168": DENSE_FALLBACK_BYTES == 135168 and "VoxelDeltaDenseFallbackPayloadBytes" in compression,
        "processorPagerRunGuardPresent": "MaxSparseDeltaRunsPerPagerPayload" in processor and "sparseRunCount > MaxSparseDeltaRunsPerPagerPayload" in processor,
        "compactionScratchVaultBacked": "EnsureGenerationHandle" in ensure_compaction and "new NativeArray" not in ensure_compaction,
        "nativeSnapshotScratchVaultBacked": "EnsureGenerationHandle<byte>" in ensure_snapshot and "new NativeArray" not in ensure_snapshot,
        "dumpPath1312": (
            "Dump_1312_VoxelPaging.bin" in pager and
            "VoxelPagingBlackBoxDumpRelativePath1312" in processor and
            "WriteBlackBoxDumpFile(VoxelPagingBlackBoxDumpRelativePath1312" in processor and
            "Dump_1312_VoxelPaging.bin" in compression
        ),
        "ownerDump1304CompatibilityRouteIsSecondary": (
            "Dump_1304" not in pager and
            "Dump_1304" not in compression and
            processor.count("Dump_1304_Voxel.bin") == 1 and
            processor.find("WriteBlackBoxDumpFile(VoxelPagingBlackBoxDumpRelativePath1312") <
            processor.find("WriteBlackBoxDumpFile(VoxelBlackBoxDumpRelativePath")
        ),
        "agent1312LayoutValidatorPresent": "ValidateAgent1312PrivateLayouts" in processor,
        "telemetryCarriesDirectorySlot": "DirectorySlot" in pager and "Metrics" in pager and "PagerTelemetryEntry" in pager,
        "mockWriteJobPresent": "GenerateMockWorldPageWriteJob" in pager and "IJobParallelFor" in pager,
        "aupDoubleEvidence": "double3" in processor and "double distanceSq = math.lengthsq(delta)" in processor,
    }

    fuzz = fuzzer()
    checks["fuzzerAllSlotsReachableAt10000"] = fuzz["allSlotsReachable"]

    report = {
        "agent": "1312",
        "scanner": "OOP_VoxelPaging_Scanner",
        "success": all(checks.values()),
        "checks": checks,
        "math": {
            "directoryBytes": DIRECTORY_BYTES,
            "directoryHeaderBytes": DIRECTORY_HEADER_BYTES,
            "directoryEntryBytes": DIRECTORY_ENTRY_BYTES,
            "directorySlotCount": DIRECTORY_SLOTS,
            "oldReachableSlotsUsingMask251": old_reachable_slots,
            "oldDeadSlots": dead_slots,
            "oldDeadSlotPercent": dead_slots / DIRECTORY_SLOTS,
            "oldPairCollisionProbability": 1.0 / old_reachable_slots,
            "newPairCollisionProbability": 1.0 / DIRECTORY_SLOTS,
            "maxSafeRleRuns": max_safe_runs,
            "checkerboardRuns": VOXEL_CELLS,
            "checkerboardRleBytes": checkerboard_rle_bytes,
            "pagerPayloadBytes": PAGER_PAYLOAD_BYTES,
            "checkerboardOverflowBytes": checkerboard_overflow_bytes,
            "denseFallbackBytes": DENSE_FALLBACK_BYTES,
        },
        "fuzzer": fuzz,
        "sourceFiles": [
            str(PAGER.relative_to(ROOT)),
            str(PROCESSOR.relative_to(ROOT)),
            str(COMPRESSION.relative_to(ROOT)),
        ],
    }

    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    print(str(REPORT))
    print("success=" + str(report["success"]).lower())
    return 0 if report["success"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
