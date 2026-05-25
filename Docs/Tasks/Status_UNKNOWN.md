# Status UNKNOWN - 13XX NativeArray / Native Ownership Audit

Date: 2026-05-25
Agent: UNKNOWN
Domain: audit only / no source-code mutation
Evidence class: STATIC_SOURCE_ONLY

- [x] Read current AGENTS/TASTE context | DOD: checked project rules before writing report artifacts. Alternative rejected: chat-only answer without disk trail. Estimate: 150 us.
- [x] Checked prior local memory files | DOD: verified `Docs/Tasks/Status_UNKNOWN.md` and `Docs/AgentLogs/Rationale_UNKNOWN.md` were missing before this task. Alternative rejected: pretending prior local state existed. Estimate: 80 us.
- [x] Extracted old NativeArray/native ownership complaints from 13XX logs and reports | DOD: reviewed `LOG_1300..1314`, `Rationale_13xx`, `Status_13xx`, and targeted `Docs/Reports` artifacts. Alternative rejected: generic NativeArray explanation. Estimate: 900 us.
- [x] Cross-checked reports against current source | DOD: verified representative source lines for 1300, 1301, 1302, 1304, 1305, 1310, 1313, 1314. Alternative rejected: trusting agent reports without source grep. Estimate: 1400 us.
- [x] Wrote detailed log artifact | DOD: created `Docs/AgentLogs/LOG_UNKNOWN.md` with old complaint, proof, residual per agent. Alternative rejected: final chat only. Estimate: 300 us.
- [x] Promoted audit into Docs/Reports | DOD: added markdown and JSON report artifacts for integrator consumption. Alternative rejected: leaving proof only in agent log. Estimate: 400 us.
- [x] Rechecked newer 13XX artifacts after user requested current state | DOD: parsed `VAULT_NATIVE_ALIAS_LEDGER_1303_WHOLE_SCRIPTS.json`, refreshed 1305/1313/1314/1310 facts, and updated report artifacts. Alternative rejected: leaving stale 1305 pointer-field claim in the report. Estimate: 650 us.
- [x] Wrote full `Assets/_Project/Scripts` folder map | DOD: generated markdown directory map, machine JSON, and TSV every-file index. Alternative rejected: dumping thousands of rows into chat. Estimate: 900 us.
- [x] Calculated forbidden native burndown speed and quality | DOD: compared only full-project zero-parse-failure ledgers, filtered latest residuals by `forbidden_persistent_native_alias_candidate`, and wrote `FORBIDDEN_NATIVE_BURNDOWN_RATE_QUALITY_UNKNOWN.*`. Alternative rejected: comparing scoped one-file zero reports against full ledgers. Estimate: 700 us.
- [x] Full fresh Roslyn native alias ledger rerun | DOD: CPU gate was 16.5%, no dotnet/csc process was shown, ran prebuilt `VaultNativeAliasRoslynAudit.exe` without rebuild and wrote `VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json`. Alternative rejected: trusting stale `X_000` 00:47 ledger that contradicted current source lines. Estimate: 0 runtime us; offline static tool only.

Current audit verdict:
- Scoped fixes are real in 1300, 1301, 1304, 1310, 1313, and 1314.
- Project-wide native ownership is not clean.
- Latest comparable full ledger: `1770` forbidden persistent candidates, `358` forbidden MonoBehaviour candidates, `0` parse failures, `2421` scanned files.
- Current measured late-window rate: persistent `1778 -> 1770` over `1.146h` = `6.98/hour`; MonoBehaviour `358 -> 358` = `0/hour`.
- Scripts map snapshot: 326 directories, 5579 files, 2420 C# files, 1765753 C# lines, 162 asmdefs. Source files changed again after the snapshot because other agents are active.
- Biggest proven residuals: `HectonVoxelEngine.cs` 124, `VegetationMemoryPool.cs` 75, `PlayerInventory.cs` 63, `DestructibleOrganicManager.cs` 50, `LogisticsNetworkGraph.cs` 50. 1305 world residency field-level candidates are now 0; terrain pager lifetime lock/unsafe alias proof debt remains.
- Policy answer: do not remove every `NativeArray`; remove or reclassify every forbidden persistent candidate. Transient job fields, method-local DataVault views, stack-only views, core memory authority, and editor validators may stay with proof.

## Build Repair Pass - 2026-05-26

- [x] Guarded build launch | DOD: checked CPU and compiler/dotnet process state before launching build; used `/m:1 /nr:false /p:UseSharedCompilation=false` to avoid persistent build workers. Alternative rejected: blind parallel build under active multi-agent load. Estimate: 0 runtime us; build-only.
- [x] Captured failing build proof | DOD: wrote `Docs/Reports/BUILD_UNKNOWN_20260526_010802.log`; first pass failed with `75` errors and `5` warnings. Alternative rejected: fixing from memory without archived compiler output. Estimate: 0 runtime us; evidence-only.
- [x] Fixed compile errors in source, not by suppressing diagnostics | DOD: repaired missing namespace imports, stale writer API calls, unassigned locals/out parameters, invalid address-of out parameter, stale native drone transaction state paths, private padding initialization, span index width, property-by-in usage, and unavailable shader tag dependency. Alternative rejected: warning suppression or broad conditional exclusion. Estimate: 0 runtime us claimed.
- [x] Fixed remaining warnings | DOD: removed CS8909, CS0618, CS0168, CS1058/MSB9008 build warnings and preserved the missing `Hecton8.Input.csproj` reference only when the project file exists. Alternative rejected: accepting a warning-clean failure as "good enough". Estimate: 0 runtime us claimed.
- [x] Final guarded build clean | DOD: `Docs/Reports/BUILD_UNKNOWN_20260526_012406.log` contains `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines 66-68. Alternative rejected: reporting after an intermediate warning build. Estimate: 0 runtime us; proof-only.
- [x] Build repair report written | DOD: added `Docs/Reports/BUILD_REPAIR_UNKNOWN_20260526.md` and `.json` with exact commands, logs, touched files, and residual caveats. Alternative rejected: chat-only build report. Estimate: 0 runtime us.
- [x] Fresh build recheck after user challenge | DOD: CPU gate `2%`, compiler process count `0`, ran full `Hecton8.slnx` build and wrote `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_013709.log`; exit `0`, `0 Warning(s)`, `0 Error(s)`. Alternative rejected: saying "yes" from the older log. Estimate: 0 runtime us; proof-only.
- [x] Root/stable docs compile-boundary refresh | DOD: updated root and stable docs that still cited old EXTERNAL_CODEX compile walls; wrote `Docs/Reports/ROOT_DOCS_BUILD_EVIDENCE_REFRESH_UNKNOWN_20260526.md/.json`. Alternative rejected: inflating docs with broad audit prose. Estimate: 0 runtime us.
- [x] Documentation gates rechecked and repaired | DOD: `python Tools/VerifyDocStructure.py` returned `pass=true`; `python Tools/OOP_Doc_Scanner.py` returned `finalPass=true`. Fixed two untagged fences, two over-threshold architecture lines, and UTF-8 BOM policy for 92 active docs from the validator report. Alternative rejected: hiding red doc gates behind the build pass. Estimate: 0 runtime us.
- [x] Second build/root-doc recheck after repeated challenge | DOD: CPU gate `5%`, compiler process count `0`, reran full `Hecton8.slnx` build and wrote `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_020504.log`; exit `0`, `0 Warning(s)`, `0 Error(s)`. Root/stable docs now cite this latest log. Alternative rejected: relying on the `013709` recheck after new user challenge. Estimate: 0 runtime us; proof-only.
