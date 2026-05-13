# Rationale - DIEGETIC_LORE_SCANNER

Status: PENDING VERIFICATION

## Intake

Problem: Scanner prompt requires replacing continuous raycasts with spatial lookup while preserving diegetic scanner UI.
Solution: Inspect current scanner, scan event, GlobalDataVault, dispatcher, AUP, and UI render paths before edits. Use existing `ScanEvents`/GlobalRegistry interfaces where possible.
Rejected Alternatives: Direct concrete dependency on narrative/campaign systems before verifying contracts; raw per-frame Physics.Raycast; string/TMP `.text` writes.
Scalability potential: Low uses percentage-only display and one occlusion query after candidate selection. Middle enables bounded scramble. High/Ultra can add denser decryption glyph visuals while keeping authority in same scan candidate path.
Hardware Impact: Expected low-end i3/MX350 gain is removal of continuous scanner raycasts; exact microseconds are PENDING VERIFICATION until profiler/compile evidence exists.

Problem: Status/rationale files were missing at session start.
Solution: Created fresh batch status and rationale files before code edits.
Rejected Alternatives: Reusing stale logs or chat-only tracking.
Scalability potential: File-backed state survives context compression and supports iterative loops.
Hardware Impact: No runtime impact.

## Loop 1 - Tasks 1-5

Problem: Focused scanner path fired a raycast before resolving whether a lore target was even near the reticle.
Solution: Replaced the initial forward raycast with a DataVault-backed lore SOA (`LoreEntityAUPs`, `LoreEntityHashes`) and a Burst dot-product job scheduled from `FastTick`.
Rejected Alternatives: `Physics.Raycast` every resample; managed `FindObjectsOfType`; adding a scanner singleton manager.
Scalability potential: Low/MX350 scans at fixed 1024-node cap with one result slot. Middle/High/Ultra can raise visual decoding density without changing target authority.
Hardware Impact: Expected i3/MX350 win is removal of the old continuous forward raycast per scanner resample; exact microseconds remain PENDING VERIFICATION because Unity compiler/profiler access is blocked.

Problem: Lore completion needed a decoupled signal and campaign progression without direct narrative dependencies.
Solution: Added `LoreFragmentScannedSignal(Hash)` and published it beside `ScanCompleteSignal`; also emits `ProgressionEventSignal` so `MetaCampaignService` consumes through its existing DAG signal lane.
Rejected Alternatives: Direct calls into `MetaCampaignService`; managed UnityEvents; adding another scanner manager.
Scalability potential: SignalBus consumers can fan out to PDA, campaign, telemetry, and UI without scanner knowing those systems.
Hardware Impact: One unmanaged signal push per completed lore scan; no per-frame cost.

Problem: Scanner target metadata had no contract assembly boundary.
Solution: Added `Hecton8.Tools.Scanner.Contracts` with `IScannerLoreTitleReadModel` as the boundary for future scanner UI/read-model extraction; kept current implementation in place to avoid a high-risk assembly move during active multi-agent work.
Rejected Alternatives: Moving `ScannerTool.cs` into a new asmdef immediately, which would drag gameplay, world, UI, inventory, audio, and narrative dependencies into a compile wall.
Scalability potential: Contract assembly allows later extraction of scanner read models without pulling scanner implementation into UI packages.
Hardware Impact: No runtime cost.

Problem: Verification is blocked by project-level dependency state, not by scanner-specific diagnostics.
Solution: Ran generated Core build and Unity refresh. Core build fails before scanner-specific validation on missing assemblies (`Hecton8.Environment.Fluids`, `Hecton8.Audio.Virtualization`, etc.). Unity refresh timed out and console reads return `no_unity_session`.
Rejected Alternatives: Reporting green compile without evidence; reverting scanner changes for unrelated dependency failures.
Scalability potential: None; this is build infrastructure state.
Hardware Impact: No runtime impact.
