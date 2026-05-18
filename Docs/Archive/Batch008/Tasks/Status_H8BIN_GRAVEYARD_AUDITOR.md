# Status_H8BIN_GRAVEYARD_AUDITOR

Date: 2026-05-17 / continued 2026-05-18
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM
Domain: Data/Binary Asset Archaeology
Task count: 4 explicit user deliverables after continuation request

## Mandates Selected

- QA_Evidence_Text_Filter_Audit.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt
- STRM_ModuleDTO_LZ4_Dictionary.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## Assignment Extraction

- [x] CURRENT_BATCH.md checked for `<AGENT_PROMPT id="H8BIN_GRAVEYARD_AUDITOR">` | DOD practice: CLI raw read with regex, no MCP truncation | Rejected: assuming SHINOBU_01 ownership because the user did not provide an ID | Estimate: 450 us
- [x] Domain boundary read | DOD practice: authoritative docs before scan | Rejected: archive-only inference | Estimate: 300 us
- [x] Relevant mandates read | DOD practice: evidence-class and binary/data mandates selected before analysis | Rejected: broad registry bulk-read as primary memory | Estimate: 900 us

## Work Checklist

- [x] Task 01: enumerate every `.h8bin` and adjacent generated binary candidate in active project/archive paths | DOD practice: deterministic inventory across `Data`, `Assets/_Project/Data`, and archive dump path; produced `Docs/AgentLogs/H8BIN_GRAVEYARD_AUDITOR_ReferenceScan.csv` with 47 target rows | Rejected: extension-only scan that would miss `GlitchTable.bytes` and archive dump context | Estimate: 2400 us
- [x] Task 02: map each binary to generator scripts, reports, intended mechanics, runtime references, and dead-weight status | DOD practice: per-file classification written to `Docs/AgentLogs/LOG_H8BIN_GRAVEYARD_AUDITOR.md`; exact-name/stem/guid evidence captured in CSV and notes | Rejected: report-only claims without code/reference grep | Estimate: 11900 us
- [x] Task 03: promote binary audit into stable documentation | DOD practice: created `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and linked it from architecture authority docs | Rejected: leaving the result only in agent logs/CSV | Estimate: 3900 us
- [x] Task 04: recheck integration claims before documenting insertion points | DOD practice: source search for `SpatialAudioManager`, `LutArrayResolver`, `BiolumPulseSyncRuntime`, `StaticDataStore`, `BabelDictionaryStore`, quest DAG, and lore readers | Rejected: treating old log classification as final without reinspection | Estimate: 5200 us
- [x] Task 05: mark safe and unsafe code application points | DOD practice: ledger separates active payloads, candidate payloads, editor/test payloads, script-tool-only payloads, and integration blockers | Rejected: opportunistic runtime wiring in occupied cross-domain systems | Estimate: 4100 us
- [x] Task 06: decide whether to patch code/binaries | DOD practice: no runtime code or generated binary bytes changed because no safe deterministic owner path was proven; documented required owner tasks instead | Rejected: hand-padding `Babel_Dictionary.h8bin` or adding concrete cross-domain references | Estimate: 1600 us
- [x] Iteration 01: raw inventory and extension classification | DOD practice: first-pass file table: 27 `.bin`, 19 `.h8bin`, 1 `.bytes`; separate non-target vendor/editor binary list captured | Rejected: hand-picked sample | Estimate: 1700 us
- [x] Iteration 02: generator/report correlation | DOD practice: basename/stem search across `Tools`, `Data`, `Docs/Reports`, `Docs/Archive/Batch007/AgentLogs`; archive BinaryHygiene RERUN compared to live disk | Rejected: trusting file names only | Estimate: 5200 us
- [x] Iteration 03: main-code usage scan | DOD practice: exact-name and stem search across first-party scripts/tests/prefabs/scenes plus GUID checks for Unity `.h8bin`/`.bytes` assets | Rejected: assuming imported TextAsset means used | Estimate: 6800 us
- [x] Iteration 04: binary header/size fingerprint pass | DOD practice: first 16-byte header, magic, byte length, 16-byte alignment, current `VerifyBinaryHygiene.py` run | Rejected: opening binaries as text | Estimate: 3100 us
- [x] Iteration 05: final dead-weight classification and report append | DOD practice: 47 product/generated rows classified; 19 Bakery verifier-scope binaries separated; final report appended/created at `Docs/AgentLogs/LOG_H8BIN_GRAVEYARD_AUDITOR.md` | Rejected: deletion recommendations without owner proof | Estimate: 4300 us
- [x] Iteration 06: continuation recheck | DOD practice: fresh inventory confirmed 47 target files; fresh hygiene verifier still reports 65 binaries and 16 misalignments | Rejected: stale "46 aligned payloads" doc claim | Estimate: 2100 us
- [x] Iteration 07: stable-doc correction | DOD practice: updated `Docs/ARCHITECTURE/README.md`, `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, and `COOP_MERKLE_STATE_DELTA_PROTOCOL.md` to point at the current ledger | Rejected: conflicting stable docs | Estimate: 2400 us
- [x] Iteration 08: independent source-usage cross-check | DOD practice: explorer delta reviewed; `Water_Extinction_Matrix.bin` retained as active because `LutArrayResolver.EnsureLoadedAndBound` has `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` | Rejected: downgrading Unity runtime-init attribute wiring as "no caller" | Estimate: 1200 us

## Verification

- [x] Reference scan generated | DOD practice: `Docs/AgentLogs/H8BIN_GRAVEYARD_AUDITOR_ReferenceScan.csv` has 47 rows | Rejected: chat-only table | Estimate: 800 us
- [x] Binary hygiene run | DOD practice: `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR.json` executed | Result: failed by current gate, `binaryCount=65`, `misalignedCount=16`; product misalignment is `Data/Balance/Baked/Babel_Dictionary.h8bin`, other 15 are Bakery editor/plugin fixtures | Estimate: 2500 us
- [x] Binary hygiene rerun 2026-05-18 | DOD practice: `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR_RECHECK.json` executed | Result: still failed by current gate, `binaryCount=65`, `misalignedCount=16` | Estimate: 2500 us
- [x] Binary hygiene rerun after explorer reconciliation | DOD practice: `python Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR_RECHECK2.json` executed | Result: still failed by current gate, `binaryCount=65`, `misalignedCount=16` | Estimate: 2500 us
- [x] Compile status | DOD practice: static audit only, no source code changed | Result: compile not run because no code/build-affecting source edits were made | Estimate: 0 us/frame
- [x] Documentation status | DOD practice: stable docs edited only; no Unity YAML, C#, binary payload, or project settings changed | Result: build compile not required for documentation-only changes | Estimate: 0 us/frame
- [x] Polish mandate check | DOD practice: read `Docs/Tasks/CURRENT_BATCH.md` after core checklist completion | Result: no `<POLISH_MANDATE>` tag found for this ad-hoc task | Estimate: 300 us
