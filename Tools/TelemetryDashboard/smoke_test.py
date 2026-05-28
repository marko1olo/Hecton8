from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True

import server


SMOKE_ROOT = server.PROJECT_ROOT / "Temp" / "CodexValidation" / "BLACKBOX_TELEMETRY_VISUALIZER_SMOKE"


def main() -> int:
    root = SMOKE_ROOT
    root.mkdir(parents=True, exist_ok=True)

    generic = server.GENERIC_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, server.GENERIC_BLACKBOX_ENTRY.size)
    generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        10, 3, 0.010, 1.5, 7.25, 512.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        11, 3, 0.020, 1.5, 7.25, 513.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    (root / "Dump_PLAYER_KINEMATICS.bin").write_bytes(generic)
    h8dump_dir = root / "persistent_copy"
    h8dump_dir.mkdir(exist_ok=True)
    h8dump_path = h8dump_dir / "BLACKBOX_CRASH.h8dump"
    h8dump_path.write_bytes(generic)
    assert server.parse_dump_file(h8dump_path)["type"] == "generic_blackbox"

    defrag = server.DEFRAG_ENTRY_PACK1.pack(1, 2, 3, 100, 64, 16, 32, 0, 0.25, 1, 5, 1, 0, 0)
    (root / "Dump_A_MEMORY_DEFRAGMENTATION_OVERSEER.bin").write_bytes(defrag)

    thermal = server.THERMAL_HEADER.pack(7, 0)
    thermal += server.THERMAL_ENTRY_MANUAL.pack(100, 7, 3, 430, 2, 77, 1, 3, 9)
    (root / "Dump_THERMAL_THROTTLING_DIRECTOR.bin").write_bytes(thermal)

    biomass = server.BIOMASS_HEADER.pack(server.BIOMASS_MAGIC, 1, server.BIOMASS_ENTRY.size, 0, 300)
    biomass += server.BIOMASS_ENTRY.pack(12, 99, 4, 0, 8.0, 5.0, 3.0, 0.4)
    (root / "Dump_ECOLOGICAL_BIOMASS_ENGINE.bin").write_bytes(biomass)

    macro = server.BIOMASS_HEADER.pack(server.MACRO_SWARM_MAGIC, 1, server.MACRO_SWARM_ENTRY.size, 0, 300)
    macro += server.MACRO_SWARM_ENTRY.pack(22, 101, 3, 2, 9.5, 1, 0, 0)
    (root / "Dump_SWARM_MACRO_MIGRATION_DIRECTOR.bin").write_bytes(macro)

    mutation = server.BIOMASS_HEADER.pack(server.FAUNA_MUTATION_MAGIC, 1, server.FAUNA_MUTATION_ENTRY.size, 0, 300)
    mutation += server.FAUNA_MUTATION_ENTRY.pack(23, 102, 7, 4, 3, 5, 0.25, 0.5, 0.75, 0, 0)
    (root / "Dump_ECOLOGY_MUTATION_DIRECTOR.bin").write_bytes(mutation)

    live = server.LIVE_TELEMETRY_ENTRY.pack(
        server.LIVE_TELEMETRY_MAGIC,
        2,
        server.LIVE_TELEMETRY_ENTRY.size,
        333,
        12,
        64,
        17.25,
        0.016,
        2048.0,
        3.5,
        6.25,
        0x123,
        0x40,
        0x77,
        9,
        331,
    )
    (root / "runtime_telemetry.bin").write_bytes(live)
    live_parsed = server.parse_live_telemetry(root / "runtime_telemetry.bin", live)
    assert live_parsed["entrySize"] == 64
    assert live_parsed["latest"]["latencyMs"] == 3.5
    assert live_parsed["latest"]["gpuFrameTimeMs"] == 6.25
    assert live_parsed["latest"]["systemMask"] == 0x123
    legacy_live = server.LIVE_TELEMETRY_ENTRY_V1.pack(server.LIVE_TELEMETRY_MAGIC, 1, 333, 12, 64, 17.25, 0.016, 2048.0)
    legacy_parsed = server.parse_live_telemetry(root / "legacy_runtime_telemetry.bin", legacy_live)
    assert legacy_parsed["entrySize"] == server.LIVE_TELEMETRY_ENTRY_V1.size
    assert "legacy_v1_32_byte_record" in legacy_parsed["warnings"]

    headless = server.HEADLESS_HEADER.pack(server.HEADLESS_MAGIC, 1, server.HEADLESS_ENTRY.size, 1)
    headless += server.HEADLESS_ENTRY.pack(200, 4, 55, 1, 2, 3, 0.1, 0.2, 0.3, 6.0, 2.0, 128.0, 0)
    assert server.parse_headless_blackbox(root / "Dump_HEADLESS.bin", headless)["latest"]["predator"] == 2.0
    assert len(server.cap_entries([{"i": i} for i in range(server.MAX_DUMP_ENTRIES + 1)])) == server.MAX_DUMP_ENTRIES

    memory_text = root / "Dump_CORE_DATA_VAULT_WARDEN.txt"
    memory_text.write_text(
        "H8MEMORY_ALLOCATION_TABLE\n"
        "TotalBytes=256\n"
        "ActiveAllocationCount=1\n"
        "Index=0 Ptr=4096 Bytes=64 Owner=1 Allocator=4 Flags=1\n",
        encoding="utf-8",
    )
    assert server.parse_h8memory_text(memory_text)["memoryMap"][-1]["state"] == "free"

    csv_path = root / "QA_Endurance_Log.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["frame", "avgFps", "PreyBiomass", "PredatorBiomass", "HardwareThermalSeverity", "BatteryPercent"],
        )
        writer.writeheader()
        writer.writerow(
            {
                "frame": "1",
                "avgFps": "60",
                "PreyBiomass": "4.5",
                "PredatorBiomass": "1.2",
                "HardwareThermalSeverity": "2",
                "BatteryPercent": "66",
            }
        )
    assert round(server.parse_csv_file(csv_path, "QA_Endurance_Log.csv")["frameSeries"][0]["frameTimeMs"], 3) == 16.667

    hphi_path = root / "HECTON_PHI_REPORT.md"
    hphi_path.write_text("Date: 2026-05-13\nH-Phi_static = 0.973 * 0.996 * 0.001 * 0.535 = 0.00062\n", encoding="utf-8")
    assert server.parse_hphi_report(hphi_path)["value"] == 0.00062

    missing_logs = root / "MissingAgentLogs"
    old_logs = server.AGENT_LOGS
    server.AGENT_LOGS = missing_logs
    try:
        missing_data = server.collect_dumps()
    finally:
        server.AGENT_LOGS = old_logs
    assert missing_data["files"] == []
    assert not missing_logs.exists()

    server.AGENT_LOGS = root
    original_parse_dump_file = server.parse_dump_file

    def fail_player_dump(path: Path) -> dict[str, object]:
        if path.name == "Dump_PLAYER_KINEMATICS.bin":
            raise ValueError("forced parser failure")
        return original_parse_dump_file(path)

    server.parse_dump_file = fail_player_dump
    try:
        fault_data = server.collect_dumps()
    finally:
        server.parse_dump_file = original_parse_dump_file
        server.AGENT_LOGS = old_logs
    assert any(
        file["name"] == "Dump_PLAYER_KINEMATICS.bin" and file["type"] == "parse_failed"
        for file in fault_data["files"]
    )
    assert any(file["type"] == "live_telemetry" for file in fault_data["files"])

    server.AGENT_LOGS = root
    try:
        dump_data = server.collect_dumps()
    finally:
        server.AGENT_LOGS = old_logs

    parsed_types = {file["type"] for file in dump_data["files"]}
    assert "macro_swarm" in parsed_types
    assert "fauna_mutation" in parsed_types
    assert "live_telemetry" in parsed_types
    assert len(dump_data["frameSeries"]) >= 3
    assert any(point["jitterMs"] == 10.0 for point in dump_data["frameSeries"])
    assert any(point["source"] == "runtime_telemetry.bin" for point in dump_data["frameSeries"])
    assert dump_data["latestThermal"]["batteryPercent"] == 77
    assert dump_data["ecologySeries"]
    assert dump_data["memoryMaps"]
    assert dump_data["memoryMaps"][0]["name"] == "Dump_CORE_DATA_VAULT_WARDEN.txt"
    assert dump_data["memoryMaps"][0]["estimated"] is False

    index_text = (Path(__file__).with_name("index.html")).read_text(encoding="utf-8")
    for required in (
        "function normalizeSummary",
        "function normalizeMemoryMap",
        "function objectArray",
        "if (!response.ok)",
    ):
        assert required in index_text
    for forbidden in ("innerHTML", "eval(", "new Function", "document.write", "console.log", "debugger"):
        assert forbidden not in index_text

    degraded = server.build_degraded_summary(RuntimeError("forced failure"))
    assert degraded["status"] == "DASHBOARD DEGRADED"
    assert degraded["csv"]["sources"] == []
    assert degraded["dumps"]["files"] == []
    assert degraded["errors"][0]["type"] == "RuntimeError"

    original_build_summary = server.build_summary

    def raise_summary() -> dict[str, object]:
        raise RuntimeError("forced route failure")

    server.build_summary = raise_summary
    try:
        response = server.api_summary()
    finally:
        server.build_summary = original_build_summary
    routed_payload = json.loads(response.body)
    assert response.status_code == 200
    assert routed_payload["status"] == "DASHBOARD DEGRADED"
    assert routed_payload["errors"][0]["type"] == "RuntimeError"
    assert response.headers["cache-control"] == "no-store, max-age=0"
    assert response.headers["pragma"] == "no-cache"
    assert response.headers["x-content-type-options"] == "nosniff"

    assert server.index().headers["cache-control"] == "no-store, max-age=0"
    health_response = server.api_health()
    health_payload = json.loads(health_response.body)
    assert health_payload["status"] == "ok"
    assert health_response.headers["x-content-type-options"] == "nosniff"

    print("telemetry dashboard smoke ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
