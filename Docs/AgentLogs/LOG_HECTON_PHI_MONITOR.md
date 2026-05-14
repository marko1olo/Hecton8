# LOG_HECTON_PHI_MONITOR

## 2026-05-14 Current Static H-Phi Rescan And Guardrail Patch

What was wrong:
- The active H-Phi status/log files were absent because Batch005 artifacts were archived.
- `Docs/Reports/HECTON_PHI_REPORT.md` described a 2026-05-13 static snapshot, not the current workspace.
- Current static scan shows the main H-Phi collapse remains Data Sovereignty: `GlobalDataVaultRefs=15` vs `NativeArrayRefs=7445`.
- Several `[BinaryBlittableSafe]` save DTOs were annotated but not asserted in `BinaryLayoutManifest.VerifySaveLayouts()`.

What was done:
- Added `Tools/Architecture/HectonPhiAudit.ps1` with reproducible counters and optional JSON output.
- Recreated active `Status_HECTON_PHI_MONITOR.md`, `Rationale_HECTON_PHI_MONITOR.md`, and this log.
- Added cold-boot layout asserts for already-approved save DTOs: `ExternalScavengerSiteDTO`, `ProceduralGeologySeamStateDTO`, `ProceduralGeologyCaveEntranceDTO`, `ModuleBlitDTO`, `PDAContextualAdvisoryDTO`, `EnvironmentalStrainDTO`, and `ModuleGraphEdgeDTO`.

Cinematic Cheats used:
- None. This pass is architecture metrics and save ABI hardening.

Exact Microseconds saved:
- 0 us measured. Runtime performance proof is absent. Expected hot-path delta is 0 because the code patch executes only in cold-boot layout verification.

Compile Status:
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` succeeded after the patch: 0 warnings, 0 errors, 39.97 s.

Phi Gain:
- Static formula score did not materially change from the guardrail patch; it improves evidence quality, not the vanity ratio. Current source-only H-Phi narrow is `0.000894646`; risk-adjusted is `0.000081597`.

## 2026-05-14 Bool DTO Raw-Blit Removal

What was wrong:
- `ProceduralFaunaStateDTO[]` and `HibernatedFaunaStateDTO[]` were saved through generic raw struct-array blits.
- Both DTOs contain `bool`, so their binary shape depended on runtime bool layout/padding assumptions.

What was done:
- Added explicit `[StructLayout]` sizes to both DTOs.
- Replaced raw `WriteStructArray`/`ReadStructArray` for those arrays with deterministic field codecs and explicit padding bytes.
- Added pre-allocation payload-length checks before reader array allocation.

Cinematic Cheats used:
- None. This is save ABI hardening.

Exact Microseconds saved:
- 0 us measured. Hot path cost is unchanged. Save/load cold path may spend slightly more CPU to buy deterministic layout.

Compile Status:
- Final verification succeeded after dependency rebuild: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`, 0 warnings, 0 errors, 43.96 s.

Phi Gain:
- Static H-Phi narrow moved from `0.000894646` to `0.000896018`. Risk-adjusted moved to `0.000081638`. The real gain is lower ABI risk, not the numeric delta.
