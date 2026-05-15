# NET_SYNC_MERKLE_ARCHITECT - BACKEND_ENGINEER

Prompt: Co-Op Determinism Designer
Domain: Backend/Multiplayer Foundation - Lockstep & Reconciliation
Task count: 7 concrete numbered tasks. Prompt header says "15 TITANIUM TASKS"; XML contains 7 concrete tasks.
Status: NETWORK PROTOCOL READY - OFFLINE SIM VERIFIED; UNITY RUNTIME PENDING

Hygiene note:
- [HYGIENE_VIOLATION] `Docs/Tasks/Status_BACKEND_ENGINEER.md` contained prior BACKEND_ENGINEER work at session start.
- User ordered continuation without wiping. Existing BACKEND_ENGINEER evidence is preserved; this prompt uses isolated `Status_NET_SYNC_MERKLE_ARCHITECT.md` state.

Mandates loaded:
- NET_Logistics_Sync_BitPacking_Reconciliation.txt
- MATH_AUP_Determinism_Sync.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Signal_Lane_Segregation.txt
- QA_Evidence_Text_Filter_Audit.txt

## Checklist

- [x] Task 1 - PACKET SCHEMA | DOD: `Docs/Modding/Net_Protocol_v1.md` defines fixed binary envelope, 16-byte `InputStateRecord`, 32-byte `WorldDeltaHeader`, 32-byte `WorldDeltaRecord`, endian, versioning, and reject policy | Alternative rejected: JSON/string RPC/network objects | Estimate: 0 us/frame current; future bounded packet drain only
- [x] Task 2 - MERKLE TREE HASHING | DOD: protocol defines sorted DataVault/AUP/signal/input leaves, integer-only hash projection, root compare, descend-on-diff, and back-buffer apply | Alternative rejected: whole-world hash only, float hash sources | Estimate: 0 us/frame current; future leaf compare under network owner budget
- [x] Task 3 - TICK RECONCILIATION | DOD: protocol defines 50Hz cadence, 256-tick input ring, 128-tick snapshot ring, 64-tick rollback, replay, resync, and 300-entry black box | Alternative rejected: visual-only smoothing as authority | Estimate: 0 us/frame current; future replay cost only on correction
- [x] Task 4 - AUP BIT PACKING | DOD: protocol defines anchor-relative `AupLocal64` signed 21-bit millimeter XYZ fields plus overflow/full-anchor path | Alternative rejected: sending `Vector3` / full `int64x3` on every packet | Estimate: saves 16+ bytes per common position payload versus full anchor
- [x] Task 5 - NETWORK SIMULATOR | DOD: `Tools/NetJitterSim.py` implemented and baseline run passed 200ms latency, 40ms jitter, 5% loss, 600 ticks, 2 clients | Alternative rejected: spreadsheet/manual packet math | Estimate: 0 us/frame current, offline tool only
- [x] Task 6 - MASTER HASH SELF-AUDIT | DOD: simulator validates deterministic replay hash sequence, `InputRingBuffer` equality, missing-input count, and AST float-hash audit across baseline, rollback-stress, and 4-client reports | Alternative rejected: trusting prose rules | Estimate: 0 us/frame current, offline tool only
- [x] Task 7 - DATA EXPORT | DOD: generated `Docs/Modding/Net_Protocol_v1.md` with schemas, Merkle logic, rollback, AUP64 compression, telemetry, scalability, and verification evidence | Alternative rejected: chat-only protocol design | Estimate: 0 us/frame current

## Loop State

Loop 1: prompt extraction, contaminated status preservation, mandate/domain intake - COMPLETE
Loop 2: packet schema, Merkle, rollback, AUP64, simulator implementation - COMPLETE
Loop 3: prompt re-extraction and Tasks 1-5 compile/static verification - COMPLETE
Loop 4: MasterStateHash/InputRingBuffer self-audit, report validation, and 4-client sanity run - COMPLETE
Loop 5: final anti-bloat verification, missing Polish Mandate check, and LOG_BACKEND_ENGINEER append - COMPLETE
Loop 6: regression unittest hardening and generated-cache cleanup - COMPLETE

## Verification Evidence

- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` via CLI regex: `NET_SYNC_MERKLE_ARCHITECT`.
- Existing network runtime ownership scan found AUP/DataVault/state-hash infrastructure, but no source-backed lockstep/Merkle runtime owner. Scope remains docs + offline simulator.
- `python Tools\NetJitterSim.py --latency-ms 200 --jitter-ms 40 --loss-percent 5 --ticks 600 --clients 2 --input-delay-ticks 16 --rollback-ticks 64 --redundancy 16 --report Docs\Reports\NetJitterSim_Report.json` -> `NETWORK PROTOCOL READY`, 78 lost packets, 0 hash mismatches, 0 ring mismatches, float hash audit PASS.
- `python Tools\NetJitterSim.py --latency-ms 200 --jitter-ms 40 --loss-percent 5 --ticks 600 --clients 2 --input-delay-ticks 8 --rollback-ticks 64 --redundancy 16 --report Docs\Reports\NetJitterSim_RollbackStress_Report.json` -> `NETWORK PROTOCOL READY`, 1190 rollback events, max rollback 4 ticks, 0 hash mismatches, 0 ring mismatches, float hash audit PASS.
- Prompt re-extracted after first task group from `Docs/Tasks/CURRENT_BATCH.md`; active block still `NET_SYNC_MERKLE_ARCHITECT`.
- `python -m py_compile Tools\NetJitterSim.py` -> exit 0.
- `git diff --check` on owned NET_SYNC files and reports -> exit 0.
- `python Tools\NetJitterSim.py --latency-ms 200 --jitter-ms 40 --loss-percent 5 --ticks 600 --clients 4 --input-delay-ticks 16 --rollback-ticks 64 --redundancy 16 --report Docs\Reports\NetJitterSim_4Client_Report.json` -> `NETWORK PROTOCOL READY`, 390 lost packets, 0 hash mismatches, 0 ring mismatches, float hash audit PASS.
- PowerShell JSON validation of all three reports -> `NET_JITTER_REPORTS_OK`.
- `Select-String` section/key-term validation of `Docs\Modding\Net_Protocol_v1.md` -> `NET_PROTOCOL_DOC_SECTIONS_OK` and `NET_PROTOCOL_KEY_TERMS_OK`.
- `rg -n "float\(|[0-9]+\.[0-9]+" Tools\NetJitterSim.py` found only the audit violation string, not an executable float call or numeric float literal.
- PowerShell-piped Python AST parse of `Tools\NetJitterSim.py` -> `NET_JITTER_AST_OK`.
- `<POLISH_MANDATE>` extraction from `Docs\Tasks\CURRENT_BATCH.md` -> `POLISH_MANDATE_NOT_FOUND`.
- Final rerun of baseline, rollback-stress, and 4-client simulator reports -> all `NETWORK PROTOCOL READY`.
- File size anti-bloat readback -> `NetJitterSim.py` 21781 bytes, `Net_Protocol_v1.md` 15147 bytes, status 5581 bytes, rationale 7900 bytes.
- Appended final report to `Docs\AgentLogs\LOG_BACKEND_ENGINEER.md`.
- Added `Tools\test_net_jitter_sim.py` regression suite for baseline convergence, rollback correction, 4-client fanout, redundant record clamping, and float-hash crime detection.
- `python -B -m unittest Tools.test_net_jitter_sim` -> 5 tests, OK.
- PowerShell-piped Python AST parse of `Tools\NetJitterSim.py` and `Tools\test_net_jitter_sim.py` under `python -B` -> `NET_SYNC_AST_NO_CACHE_OK`.
- Removed generated NET_SYNC `Tools\__pycache__` bytecode files; follow-up check -> `NET_SYNC_PYCACHE_CLEAN`.
- Final post-hardening `python -B` rerun of baseline, rollback-stress, and 4-client reports -> all `NETWORK PROTOCOL READY`.
- Final `python -B -m unittest Tools.test_net_jitter_sim` -> 5 tests, OK, 15.093 s.
- Final JSON report gate -> `FINAL_NET_REPORTS_OK`.
- Final protocol doc gate -> `FINAL_NET_DOC_OK`.
- Final NET_SYNC bytecode cache gate -> `NET_SYNC_PYCACHE_CLEAN`.
- Final `git diff --check` on owned NET_SYNC files/reports/logs -> exit 0.
