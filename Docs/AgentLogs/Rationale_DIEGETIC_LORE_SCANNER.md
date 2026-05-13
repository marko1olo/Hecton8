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

## Loop 2 - Tasks 6-10

Problem: Scanner needed target forgiveness without pixel-accurate ray work.
Solution: Used the highest forward dot product under 15m as the candidate. This is the intended lie: near-crosshair selection, not exact collider picking.
Rejected Alternatives: screen-space projection plus collider tests; multi-ray cone cast; broad managed raycast fan.
Scalability potential: Low tier keeps the same cheap candidate. High/Ultra can spend saved budget on richer RT glyph noise while preserving identical target authority.
Hardware Impact: Expected low-end gain is one Burst linear scan over native arrays plus at most one occlusion command, replacing continuous ray work.

Problem: Scanner decryption UI had to write to the physical tool screen without heap strings.
Solution: The tool RT controller now consumes `ScannerToolActiveSignal`, resolves the lore title by hash, writes into fixed `char[]` staging buffers with `Span<char>`, and scrambles unrevealed characters. Low tier writes percentage only.
Rejected Alternatives: TMP `.text`, managed formatted strings, per-frame `StringBuilder`, and UI `Update()`.
Scalability potential: MX350 gets percentage-only. Mid gets title scramble. High/Ultra can use higher refresh/noise density on the same buffer path.
Hardware Impact: Expected low-end gain is zero managed allocation during active scanner display; exact microseconds PENDING VERIFICATION.

Problem: Lore completion must unlock systems and update campaign without coupling scanner to narrative implementation.
Solution: Completion path publishes `LoreFragmentScannedSignal` and an existing `ProgressionEventSignal` for MetaCampaignService. DataArchaeology remains the commit authority.
Rejected Alternatives: direct MetaCampaignService method call; UI-side unlock; managed UnityEvent chain.
Scalability potential: Additional lore consumers can subscribe to signal lanes without touching scanner code.
Hardware Impact: Completion-only signal traffic; no frame cost during scanning.

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

## Loop 3 - Tasks 11-15

Problem: Scanner targeting must survive floating origin and AUP shifts.
Solution: Lore targets are stored as `AbsoluteUniversePosition` in DataVault and compared to the camera with `AbsoluteUniversePosition.ToCameraRelativeFloat3` inside the candidate job.
Rejected Alternatives: Treating `transform.position` as authoritative range data; caching world-space vectors across shifts.
Scalability potential: Low/Middle/High/Ultra share the same stable authority path; visual layers can scale independently from coordinate precision.
Hardware Impact: One AUP-relative conversion per registered lore node during resample; expected cost remains below the removed broad ray work, exact microseconds PENDING VERIFICATION.

Problem: Low-tier hardware cannot spend budget on scanner glyph theater.
Solution: Low/Unknown/MX350 paths bypass title scrambling and write percentage-only text through `ZeroGCFormatter.FastIntToChars`.
Rejected Alternatives: One uniform scramble path across all tiers; managed formatted percentage strings.
Scalability potential: Low uses numeric status. Middle uses deterministic title scramble. High/Ultra can raise decode density and visual noise while preserving the same zero-GC staging buffers.
Hardware Impact: Expected i3/MX350 gain is removal of per-character scramble work on low tier; exact microseconds PENDING VERIFICATION.

Problem: Scanner acquisition and UI presentation needed deterministic phase separation.
Solution: Acquisition runs in `IFastTickable` on the Player lane; scanner state publication runs through `ILateFrameTickable` on the UI lane; tool RT display consumes signals in the existing dispatcher path.
Rejected Alternatives: Unity `Update()` loops in scanner/UI; direct UI calls from acquisition.
Scalability potential: Simulation remains stable while UI refresh rate/effects scale by tier.
Hardware Impact: Phase split prevents UI work from feeding back into acquisition; exact profiler numbers unavailable.

Problem: Required compile proof for `Span<char>` no boxing is unavailable.
Solution: Static audit confirms scanner/UI span writes use stackalloc or fixed char arrays and TMP `SetCharArray`; project compile is dependency-blocked before scanner proof can be produced.
Rejected Alternatives: Marking Task 15 complete without compiler evidence; replacing span path with strings to appease unverifiable build state.
Scalability potential: Once build infrastructure is restored, this path should validate without changing runtime design.
Hardware Impact: No runtime impact from the blocked proof itself.
