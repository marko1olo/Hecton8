# Rationale_17-A

## Decision 001: Treat Direct Prompt As Source

Problem: The user requested Agent `17-A`, but `Docs/Tasks/CURRENT_BATCH.md` contains numeric `<AGENT_PROMPT>` ids `1701-1730` and no `17-A` tag.
Solution: Use the direct user prompt as the operative source and record that batch extraction was attempted.
Rejected Alternatives: Hijacking `1729` would cross owner boundaries; ignoring the prompt would leave no actionable assignment.
Scalability potential: Process-only decision. Low/middle/high/ultra hardware unchanged.
Hardware Impact: 0 us runtime; prevents wasted work on wrong code.

## Decision 002: Bounded Verifier Instead Of Infinite Runtime Loop

Problem: The request describes an endless hour-by-hour loop that edits files and rebuilds monoliths indefinitely.
Solution: Implement a deterministic cold verifier loop that can process selected packages or all packages, emit artifacts, and be re-run by CLI/CI.
Rejected Alternatives: An uncontrolled infinite agent loop would monopolize the workstation, collide with 20+ agents, and risk repeated rebuilds over dirty shared files.
Scalability potential: Low devices consume baked results only; middle/high/ultra can run richer editor/reporting passes without runtime cost.
Hardware Impact: 0 us runtime; editor/CLI work stays off gameplay frames.

## Decision 003: Use Static 720p Bounds Model Before Unity Capture

Problem: Unity/TMP exact glyph metrics require editor scene context and screenshots, but a first pass needs fast package triage across 15 locales.
Solution: Use a conservative cold static width estimator per script family, report evidence class as `STATIC_SOURCE`, and reserve runtime/UI capture claims as `PENDING_VERIFICATION`.
Rejected Alternatives: Claiming clipping proof from static text alone violates `QA_Evidence_Text_Filter_Audit`.
Scalability potential: Low lane gets short high-contrast text; middle/high/ultra may add richer screen treatment without changing string truth.
Hardware Impact: Estimated runtime saving is 0 us because this is offline triage; expected avoided UI rebuild/debug cost is process-only.

## Decision 004: Use AppliedLoreImporter Locale Set As Disk Authority

Problem: Runtime localization enum exposes more languages than the applied-lore disk pipeline currently ships in `Docs/Lore/AppliedContent/in_game_wiki`.
Solution: Bind verifier locale coverage to the 15-locale `TARGET_LOCALES` route already used by `Tools/AppliedLoreImporter.py`.
Rejected Alternatives: Inventing missing locale folders would create false coverage; using runtime enum alone would report non-actionable missing data.
Scalability potential: Low/middle/high/ultra devices read one baked route. Editor tooling can expand when source locale ownership exists.
Hardware Impact: 0 us gameplay; prevents false runtime fallback churn.

## Decision 005: Mechanical RS056 Text Fixes Only

Problem: RS056 had deterministic overrun from repeated draft prefixes and verbose review-lock labels, but most strings are native-review placeholders rather than final translations.
Solution: Rewrite only mechanically safe text: `Draft XX localization pending native pass.` to `XX LOC HOLD:` and long lock labels to short gate labels.
Rejected Alternatives: Blind truncation of thousands of localized prose strings would destroy meaning and tone; runtime font scaling would hide source debt.
Scalability potential: Low devices get shorter terminal/HUD strings; middle/high/ultra can spend saved space on richer diegetic treatment without changing text truth.
Hardware Impact: 0 us gameplay; expected saved UI layout cost is offline/content-only.

## Decision 006: Block Monolith Bake Under CPU Gate

Problem: The prompt requires monolith rebuild after source text edits, but project rules forbid launching .NET build work while system CPU is over 50%.
Solution: Regenerate applied-lore CSV/C# hash artifacts and run hash collision checks, then block the .NET bake with CPU evidence instead of violating the workstation gate.
Rejected Alternatives: Running `dotnet` at 55-60% CPU risks colliding with other agents and breaks explicit project law.
Scalability potential: Low/middle/high/ultra runtime unchanged until the next safe bake window; source artifacts are ready for bake.
Hardware Impact: Avoided unpredictable build contention on low-end silicon; measured gate samples were 60% and 55% CPU, compiler processes absent.

## Decision 007: Report Global Backlog, Do Not Auto-Fix It

Problem: Full applied-lore audit found 62,602 static issue flags across 48,300 surface checks, dominated by draft prefixes and long prose.
Solution: Fix one bounded package, emit full JSON/CSV triage, and leave larger content backlog as explicit evidence.
Rejected Alternatives: Mass automated shortening across all lore would corrupt narrative ownership; repeating rebuilds in a loop would be process theater.
Scalability potential: Low lane needs progressive packet-by-packet reductions; middle/high/ultra can add runtime capture proof once source debt is reduced.
Hardware Impact: 0 us gameplay; future savings require actual UI capture/profiler proof.

## Decision 008: RS081 Tight-Surface Rewrite Scope

Problem: RS081 worker dossiers still produced 174 modeled line-overflow rows after draft-prefix shortening, concentrated in `title`, `scanner`, and `terminal` surfaces.
Solution: Rewrite only tight-surface fields with compact dossier labels and hard terminal prose. Leave audio/wiki/site prose intact because those surfaces stopped clipping after prefix reduction.
Rejected Alternatives: Global truncation would erase worker identity; runtime font shrinking would push source debt into HUD code; translating every placeholder locale would fake native review.
Scalability potential: Low devices get one-line titles and two-line terminal text; middle/high/ultra can present richer surrounding panels without changing content authority.
Hardware Impact: 0 us gameplay; expected benefit is avoided layout overflow, not measured frame-time savings.

## Decision 009: Monolith Bake Blocked By Active Dotnet

Problem: The second cycle reached the monolith bake step, but workstation CPU was 100% and an existing `dotnet` process was active.
Solution: Stop at regenerated source/CSV/C# hash artifacts and record the unchanged monolith SHA-256. Do not start another .NET bake.
Rejected Alternatives: Parallel bake under active `dotnet` violates project law and risks corrupting shared generated artifacts.
Scalability potential: Runtime tiers unchanged until a safe bake window. Source data is ready for the next bake.
Hardware Impact: Avoided build contention on low-end silicon; no gameplay runtime effect.

## Decision 010: RS082 Artifact Memo Rewrite Scope

Problem: RS082 artifact memos retained 148 modeled issue rows after draft-prefix reduction, entirely in `title` and `terminal` surfaces.
Solution: Compress artifact titles to one-line labels and terminal prose to hard liability statements. Preserve scanner/wiki/audio/site fields because they were not the remaining clipping source.
Rejected Alternatives: Rewriting every locale as if native review was complete would fake localization quality; shrinking fonts at runtime would hide data debt.
Scalability potential: Low devices get short proof labels; middle/high/ultra can use richer terminal shells around the same baked strings.
Hardware Impact: 0 us gameplay; benefit is static clipping removal only.

## Decision 011: Cycle 3 Bake Blocked By Dual Dotnet

Problem: Cycle 3 reached the bake gate while CPU was 100% and two `dotnet` processes were active.
Solution: Stop at regenerated lore/hash artifacts and record unchanged monolith checksum.
Rejected Alternatives: Starting another bake would violate the explicit compiler gate and risk generated artifact contention.
Scalability potential: Runtime tiers unchanged until safe bake window; data is ready for bake.
Hardware Impact: Avoided build contention; no gameplay runtime effect.

## Decision 012: RS085 Ephemeris Public Band Rewrite Scope

Problem: RS085 retained 213 modeled issue rows after draft-prefix reduction, concentrated in `title`, `scanner`, and `terminal` surfaces.
Solution: Compress astronomy route text to short public-band labels, two-line scanner summaries, and terminal-safe ephemeris rules. Leave external site and field notes untouched because they were not the clipping source.
Rejected Alternatives: Rewriting non-flagged long-form prose wastes authority and risks tone drift; runtime font shrinking hides source debt.
Scalability potential: Low devices get hard route labels; middle/high/ultra can layer richer maps around the same compact strings.
Hardware Impact: 0 us gameplay; static clipping removal only.

## Decision 013: Cycle 4 Bake Blocked By Active Dotnet

Problem: Cycle 4 reached the bake gate while CPU was 63% and a `dotnet` process was active.
Solution: Stop at regenerated lore/hash artifacts and record unchanged monolith checksum.
Rejected Alternatives: Starting a .NET bake above the CPU gate violates project law and risks competing with another agent.
Scalability potential: Runtime tiers unchanged until safe bake window; data is ready for bake.
Hardware Impact: Avoided build contention; no gameplay runtime effect.
