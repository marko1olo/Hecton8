# Binary Payload Integration Ledger

Date: 2026-06-01
Status: PENDING VERIFICATION
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / STATIC_SOURCE / STATIC_FILESYSTEM

## Current Source Constants

| Fact | Value |
|---|---:|
| Save version | `0x000B` |
| Save header | `56` bytes |
| Legacy save header | `44` bytes |
| SignalBus registry lane capacity | `512` |
| H8DM header | `64` bytes |
| H8DM directory | `64` bytes |
| Data Monolith payload bytes | `7457664` |

Payload path: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
2026-06-01 static filesystem check confirms the payload exists at this path. Payload bytes above are the 2026-08-05 measurement, mtime 2026-06-07; the 2026-06-01 check recorded `1804864`. This is still not import, boot, checksum, player, or save/load proof.

## Active Boundary

- Active C# source owns DTO sizes, offsets, BufferIDs, and route ownership.
- Full pre-compression ledger: `../_Archive/Architecture_X_012_APEX_2026-05-23/BINARY_PAYLOAD_INTEGRATION_LEDGER.full.md`.
- Machine-readable extracted payload inventory: `../Reports/BINARY_PAYLOAD_LEDGER_CONCISION_X_012.json`.
- Extracted payload boundary records: `288`.
- Runtime readiness remains `PENDING VERIFICATION` without Unity import, Play Mode, profiler, GCMonitor, Memory Profiler, and player-build artifacts.

## Contract Rules

- No archived report text is active doctrine.
- No static source read proves runtime readiness.
- `GlobalQualityWeight` may scale cost, cadence, and presentation richness only.
- Quality scaling must not change DTO layout, BufferID identity, save identity, rollback identity, or authority route.
- New payload facts must enter source first, then the JSON inventory, then this index.
