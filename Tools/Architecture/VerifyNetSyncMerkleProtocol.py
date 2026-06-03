#!/usr/bin/env python3
"""Static verifier for the HECTON-8 co-op Merkle state delta protocol."""

from __future__ import annotations

import re
import struct
import sys
import os
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
TOOLS_ROOT = ROOT / "Tools"
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import NetJitterSim as net_jitter  # noqa: E402

DOC = ROOT / "Docs" / "ARCHITECTURE" / "COOP_MERKLE_STATE_DELTA_PROTOCOL.md"
ATLAS = ROOT / "Docs" / "PROJECT_ATLAS.md"
JITTER_REPORT = ROOT / "Docs" / "AgentLogs" / "NetJitterSim_NET_SYNC_MERKLE_ARCHITECT.json"

DATAGRAM_CEILING = 1200
EXPECTED_DOMAIN_COUNT = 85
EXPECTED_VAULT_BUFFER_FAMILIES = 7
EXPECTED_SIGNAL_LANES = 4
HEADER_CRC16_OFFSET = 62
HEADER_CRC16_SIZE = 2
HEADER_CRC16_POLY = 0x1021
HEADER_CRC16_INIT = 0xFFFF
HEADER_CRC16_XOROUT = 0x0000
REQUIRED_JITTER_CONFIG = {
    "clients": 4,
    "latency_ms": 200,
    "jitter_ms": 80,
    "loss_bps": 800,
    "ticks": 600,
    "input_delay_ticks": 12,
    "rollback_ticks": 96,
    "redundancy": 24,
    "seed": 1313817649,
}

STRUCTS = {
    "H8NetMerkleFrameHeader64": ("<IHHIIIIQQQHHHHHBBBBH", 64),
    "H8NetMerkleNodeRecord32": ("<HHIQQIHBB", 32),
    "H8NetLeafDeltaRecord64": ("<QIHBBQQQQIHHIBBH", 64),
    "H8NetRepairRequestRecord32": ("<QQQIHBB", 32),
    "H8NetTelemetryEntry64": ("<IIQQQQQHHHHII", 64),
    "H8NetVisualOverkillRecord64": ("<QQIIIHHHBBIQQQ", 64),
}

PROTOCOL_LABELS = (
    "H8NET_LEAF_V1",
    "H8NET_NODE_V1",
    "H8NET_ROOT_V1",
    "RootSeal",
    "NodeProbe",
    "LeafDelta",
    "RepairRequest",
    "FullSectorWindow",
    "TelemetryEcho",
    "VisualOverkill",
    "NetSyncRootSealSignal",
    "NetSyncRepairRequestSignal",
    "NetSyncDeltaAppliedSignal",
    "NetSyncDesyncSignal",
    "NetSyncLeafKeyFront",
    "NetSyncLeafHashFront",
    "NetSyncLeafHashBack",
    "NetSyncNodeFront",
    "NetSyncNodeBack",
    "NetSyncPacketStaging",
    "NetSyncTelemetryRing",
    "H8NetVisualOverkillRecord64",
)

FORBIDDEN_STERILE_TERMS = (
    "hyperspace",
    "nanite cloud",
    "warp drive",
    "clean sci-fi",
)

IGNORED_BINARY_DIRS = (
    ".git",
    ".codex-artifacts",
    ".codex-build",
    ".codex",
    "Library",
    "Temp",
    "obj",
    "bin",
    "Build",
    "Builds",
    "Logs",
    "UserSettings",
)


def fnv1a64(text: str) -> int:
    value = 0xCBF29CE484222325
    for byte in text.encode("utf-8"):
        value ^= byte
        value = (value * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return value


def crc16_ccitt_false(data: bytes) -> int:
    crc = HEADER_CRC16_INIT
    for byte in data:
        crc ^= byte << 8
        for _ in range(8):
            if crc & 0x8000:
                crc = ((crc << 1) ^ HEADER_CRC16_POLY) & 0xFFFF
            else:
                crc = (crc << 1) & 0xFFFF
    return crc ^ HEADER_CRC16_XOROUT


def fail(message: str) -> None:
    print(f"FAIL: {message}")
    raise SystemExit(1)


def read_text(path: Path) -> str:
    if not path.exists():
        fail(f"missing required file: {path.relative_to(ROOT)}")
    return path.read_text(encoding="utf-8")


def verify_structs() -> int:
    for name, (fmt, expected_size) in STRUCTS.items():
        if not fmt.startswith("<"):
            fail(f"{name} is not explicitly little-endian: {fmt}")
        size = struct.calcsize(fmt)
        if size != expected_size:
            fail(f"{name} size {size} != expected {expected_size}")
        if size % 16 != 0:
            fail(f"{name} size {size} is not 16-byte aligned")

    header_fmt = STRUCTS["H8NetMerkleFrameHeader64"][0]
    header = struct.pack(
        header_fmt,
        0x4D4E3848,
        1,
        64,
        0x12345678,
        1,
        20,
        19,
        0x0102030405060708,
        0x1112131415161718,
        0x2122232425262728,
        0,
        0,
        0,
        0,
        1,
        0,
        1,
        1,
        0,
        0,
    )
    if header[:4] != b"H8NM":
        fail(f"header magic byte order invalid: {header[:4]!r}")
    header_without_crc = bytearray(header)
    header_without_crc[HEADER_CRC16_OFFSET:HEADER_CRC16_OFFSET + HEADER_CRC16_SIZE] = b"\x00\x00"
    header_crc = crc16_ccitt_false(bytes(header_without_crc))
    if header_crc == 0:
        fail("header CRC sample unexpectedly zero")
    header_with_crc = bytearray(header_without_crc)
    struct.pack_into("<H", header_with_crc, HEADER_CRC16_OFFSET, header_crc)
    unpacked_crc = struct.unpack_from("<H", header_with_crc, HEADER_CRC16_OFFSET)[0]
    if unpacked_crc != header_crc:
        fail("header CRC little-endian write/read mismatch")
    verify_crc = bytearray(header_with_crc)
    verify_crc[HEADER_CRC16_OFFSET:HEADER_CRC16_OFFSET + HEADER_CRC16_SIZE] = b"\x00\x00"
    if crc16_ccitt_false(bytes(verify_crc)) != header_crc:
        fail("header CRC verification mismatch")
    return header_crc


def verify_packet_fits() -> None:
    header = STRUCTS["H8NetMerkleFrameHeader64"][1]
    node = STRUCTS["H8NetMerkleNodeRecord32"][1]
    leaf = STRUCTS["H8NetLeafDeltaRecord64"][1]
    repair = STRUCTS["H8NetRepairRequestRecord32"][1]
    telemetry = STRUCTS["H8NetTelemetryEntry64"][1]

    examples = {
        "RootSeal": header,
        "NodeProbe": header + (32 * node),
        "LeafDelta": header + (8 * leaf) + 512,
        "RepairRequest": header + (32 * repair),
        "TelemetryEcho": header + (16 * telemetry),
        "VisualOverkill": header + (16 * STRUCTS["H8NetVisualOverkillRecord64"][1]),
    }

    for name, byte_count in examples.items():
        if byte_count > DATAGRAM_CEILING:
            fail(f"{name} packet fit {byte_count} exceeds {DATAGRAM_CEILING}")
        if byte_count % 16 != 0:
            fail(f"{name} packet fit {byte_count} is not 16-byte aligned")


def parse_domain_labels() -> list[str]:
    text = read_text(ATLAS)
    by_id: dict[int, str] = {}
    pattern = re.compile(r"^\|\s*([1-9]\d?)\s*\|[^|]*\|\s*`([^`]+)`\s*\|", re.M)
    for match in pattern.finditer(text):
        domain_id = int(match.group(1))
        if not (1 <= domain_id <= EXPECTED_DOMAIN_COUNT):
            continue
        label = " ".join(match.group(2).strip().split())
        if label and domain_id not in by_id:
            by_id[domain_id] = label

    missing = [i for i in range(1, EXPECTED_DOMAIN_COUNT + 1) if i not in by_id]
    if missing:
        fail(f"PROJECT_ATLAS.md domain map missing ids: {missing}")

    return [by_id[i] for i in range(1, EXPECTED_DOMAIN_COUNT + 1)]


def verify_fnv_collisions(labels: list[str]) -> None:
    hashes: dict[int, str] = {}
    for label in labels:
        value = fnv1a64(label)
        previous = hashes.get(value)
        if previous is not None:
            fail(f"FNV-1a64 collision: {previous!r} and {label!r} -> 0x{value:016X}")
        hashes[value] = label


def iter_binary_payloads() -> list[Path]:
    payloads: list[Path] = []
    ignored = set(IGNORED_BINARY_DIRS)
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [name for name in dirnames if name not in ignored]
        current = Path(dirpath)
        for filename in filenames:
            if filename.endswith(".bin") or filename.endswith(".h8bin"):
                payloads.append(current / filename)
    return sorted(payloads)


def verify_binary_payload_alignment() -> int:
    payloads = iter_binary_payloads()
    for path in payloads:
        size = path.stat().st_size
        if size % 16 != 0:
            fail(f"binary payload is not 16-byte aligned: {path.relative_to(ROOT)} size={size}")
    return len(payloads)


def build_required_jitter_args() -> object:
    return type(
        "Args",
        (),
        {
            "latency_ms": REQUIRED_JITTER_CONFIG["latency_ms"],
            "jitter_ms": REQUIRED_JITTER_CONFIG["jitter_ms"],
            "loss_bps": REQUIRED_JITTER_CONFIG["loss_bps"],
            "tick_ms": 20,
            "ticks": REQUIRED_JITTER_CONFIG["ticks"],
            "clients": REQUIRED_JITTER_CONFIG["clients"],
            "input_delay_ticks": REQUIRED_JITTER_CONFIG["input_delay_ticks"],
            "rollback_ticks": REQUIRED_JITTER_CONFIG["rollback_ticks"],
            "redundancy": REQUIRED_JITTER_CONFIG["redundancy"],
            "seed": REQUIRED_JITTER_CONFIG["seed"],
            "report": JITTER_REPORT,
        },
    )()


def jitter_report_is_current(report: dict[str, object]) -> bool:
    config = report.get("config")
    if not isinstance(config, dict):
        return False
    for key, expected in REQUIRED_JITTER_CONFIG.items():
        if config.get(key) != expected:
            return False
    return report.get("status") == "NETWORK PROTOCOL READY"


def load_or_rebuild_jitter_report() -> dict[str, object]:
    if JITTER_REPORT.exists():
        try:
            report = json.loads(JITTER_REPORT.read_text(encoding="utf-8"))
            if jitter_report_is_current(report):
                return report
        except json.JSONDecodeError:
            pass

    report = net_jitter.simulate(build_required_jitter_args())
    JITTER_REPORT.parent.mkdir(parents=True, exist_ok=True)
    JITTER_REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return report


def verify_jitter_report() -> dict[str, object]:
    report = load_or_rebuild_jitter_report()
    if report.get("status") != "NETWORK PROTOCOL READY":
        fail(f"jitter simulation status is not ready: {report.get('status')}")

    config = report.get("config")
    network = report.get("network")
    rollback = report.get("rollback")
    verification = report.get("verification")
    if not isinstance(config, dict) or not isinstance(network, dict) or not isinstance(rollback, dict) or not isinstance(verification, dict):
        fail("jitter simulation report shape is invalid")

    for key, expected in REQUIRED_JITTER_CONFIG.items():
        if config.get(key) != expected:
            fail(f"jitter simulation config {key}={config.get(key)} != {expected}")

    if verification.get("master_state_hash_mismatches") != 0:
        fail("jitter simulation master state hash mismatch")
    if verification.get("input_ring_mismatches") != 0:
        fail("jitter simulation input ring mismatch")
    if verification.get("missing_actual_inputs") != 0:
        fail("jitter simulation missing actual inputs")
    float_audit = verification.get("float_hash_audit")
    if not isinstance(float_audit, dict) or float_audit.get("status") != "PASS":
        fail("jitter simulation float hash audit failed")
    if rollback.get("too_old_corrections") != 0:
        fail("jitter simulation rollback window too small")
    if int(rollback.get("max_depth_ticks", 0)) > int(config.get("rollback_ticks", 0)):
        fail("jitter simulation rollback depth exceeds window")
    if int(network.get("lost_packets", 0)) <= 0:
        fail("jitter simulation did not exercise packet loss")
    if int(network.get("delivered_packets", 0)) <= 0:
        fail("jitter simulation delivered no packets")

    return report


def verify_doc_contract(doc: str) -> None:
    required_terms = (
        "STATUS: STATIC DESIGN VERIFIED / RUNTIME PENDING",
        "AUP",
        "GlobalDataVault",
        "SignalBus",
        "0 B",
        "little-endian",
        "16-byte aligned",
        "XXH3_128",
        "FNV-1a",
        "CRC-16/CCITT-FALSE",
        "0x1021",
        "0xFFFF",
        "offset `62..63`",
        "85-domain",
        "H8NetVisualOverkillRecord64",
        "NetJitterSim_NET_SYNC_MERKLE_ARCHITECT.json",
        "NETWORK PROTOCOL READY",
        "Data Sovereignty",
    )
    for term in required_terms:
        if term not in doc:
            fail(f"document missing required term: {term}")

    lowered = doc.lower()
    for term in FORBIDDEN_STERILE_TERMS:
        if term in lowered:
            fail(f"forbidden sterile term present: {term}")

    if ".json" in lowered or "json" in lowered:
        runtime_json_sentence = "Runtime JSON is forbidden."
        if runtime_json_sentence not in doc:
            fail("JSON appears without explicit runtime ban")


def verify_atlas() -> None:
    atlas = read_text(ATLAS)
    if "85 Identified Domains" not in atlas:
        fail("PROJECT_ATLAS.md does not expose the 85-domain map")
    if "DataSovereignty" not in atlas or "SignalBus" not in atlas:
        fail("PROJECT_ATLAS.md missing H-Phi/DataSovereignty signal boundary")


def verify_hphi_model(doc: str) -> None:
    vault_match = re.search(r"design-level protocol model is `(\d+)` DataVault buffer families", doc)
    signal_match = re.search(r"`(\d+)` future typed signal lanes", doc)
    if not vault_match or int(vault_match.group(1)) != EXPECTED_VAULT_BUFFER_FAMILIES:
        fail("DataVault buffer-family H-Phi model mismatch")
    if not signal_match or int(signal_match.group(1)) != EXPECTED_SIGNAL_LANES:
        fail("typed signal-lane H-Phi model mismatch")
    forbidden_negative_terms = (
        "`0` direct concrete cross-domain references",
        "`0` hot registry polls",
        "`0` runtime JSON paths",
    )
    for term in forbidden_negative_terms:
        if term not in doc:
            fail(f"H-Phi negative counter missing: {term}")


def main() -> int:
    doc = read_text(DOC)
    domain_labels = parse_domain_labels()
    header_crc = verify_structs()
    verify_packet_fits()
    verify_fnv_collisions(list(PROTOCOL_LABELS) + domain_labels)
    binary_payloads = verify_binary_payload_alignment()
    jitter_report = verify_jitter_report()
    verify_doc_contract(doc)
    verify_atlas()
    verify_hphi_model(doc)

    print("NET_SYNC_MERKLE_PROTOCOL_VERIFY=PASS")
    print(f"STRUCT_COUNT={len(STRUCTS)}")
    print(f"DOMAIN_LABELS={len(domain_labels)}")
    print(f"FNV_LABELS={len(PROTOCOL_LABELS) + len(domain_labels)}")
    print(f"BINARY_PAYLOADS_ALIGNED={binary_payloads}")
    print(f"DATAGRAM_CEILING={DATAGRAM_CEILING}")
    print(f"HEADER_CRC16_SAMPLE=0x{header_crc:04X}")
    print(f"JITTER_SIM_STATUS={jitter_report['status']}")
    print(f"JITTER_SIM_LOST_PACKETS={jitter_report['network']['lost_packets']}")
    print(f"JITTER_SIM_ROLLBACK_MAX_DEPTH={jitter_report['rollback']['max_depth_ticks']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
