# HECTON-8 Telemetry Dashboard

FastAPI dashboard for telemetry files under `Docs/AgentLogs`.

## Run

Windows:

```bat
Tools\TelemetryDashboard\start_dashboard.bat
```

POSIX:

```sh
sh Tools/TelemetryDashboard/start_dashboard.sh
```

Open:

```text
http://127.0.0.1:8000
```

## Smoke Test

```bat
cd Tools\TelemetryDashboard
python -B smoke_test.py
```

Expected output:

```text
telemetry dashboard smoke ok
```

## Data Sources

- `Docs/AgentLogs/QA_Endurance_Log.csv`
- `Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv`
- `Docs/AgentLogs/Dump_*.bin`
- `Docs/AgentLogs/Dump_*.txt`
- `Docs/AgentLogs/*.h8dump`
- `Docs/AgentLogs/runtime_telemetry.bin`
- `Docs/Reports/HECTON_PHI_REPORT.md`

The dashboard runs without Unity. Missing files are reported as missing instead of failing startup.
CSV rows and dump entry arrays returned by `/api/summary` are capped to the latest 600 samples. Parser `latest` fields still point to the newest decoded record.

## Parser Contracts

- Generic crash blackbox: `HECTON8` little-endian header, `uint entryCount`, `uint structSize`, 64-byte entries. These entries are also used as a fallback frame-time source when QA CSV is absent.
- Job-admission blackbox: `HECTON8` little-endian 32-byte header with version, entry count, 64-byte entry size, cursor, and frame sequence; v2 decodes admission reason flags, while v1 remains legacy starvation/non-finite flags.
- Data Vault defrag: raw `MemoryDefragTelemetryEntry` ring from `GlobalDataVault`; supports 64-byte pack-1 and 72-byte aligned variants.
- Thermal throttling: `uint sequence`, `uint cursor`, then manual little-endian thermal records from `HardwareThermalService`.
- Ecology biomass: magic `HECSMB8`, entry count, entry size, oldest index, capacity, then 32-byte biomass entries.
- Macro-swarm migration: magic `HECOSWM`, entry count, entry size, oldest index, capacity, then 32-byte macro-swarm entries.
- Fauna mutation: magic `HECOGUM`, entry count, entry size, oldest index, capacity, then 48-byte mutation entries.
- Headless QA blackbox: magic `0x48385142`, entry count, entry size, cursor, then 64-byte entries.
- Crash live telemetry: magic `TELM`; v2 uses a 64-byte record with record size, frame, active chunk count, GC bytes, CPU frame ms, delta time, reserved memory MB, latency ms, GPU frame ms, system mask, error flags, velocity pack, AUP shift sequence, and last origin-shift frame. Legacy v1 32-byte records remain readable with a warning.

Memory-map selection prefers source H8Memory allocation tables over fully estimated defrag summaries when both exist. Defrag-only maps remain labeled as estimated.

Evidence class remains `FILE_IO + STATIC_SOURCE`. Unity Play Mode, Profiler, and GCMonitor are not asserted by this tool.
