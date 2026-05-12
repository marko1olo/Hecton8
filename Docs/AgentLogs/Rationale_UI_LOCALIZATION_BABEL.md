# Rationale_UI_LOCALIZATION_BABEL

Status: PENDING VERIFICATION
Agent: UX_ENGINEER
Prompt ID: UI_LOCALIZATION_BABEL

## Mandate Ingestion

Problem: Localization task spans UI, registry, native data, jobs, font swap, and AUP world text.
Solution: Loaded only relevant mandates: Babel localization, UI zero-GC streaming, GlobalRegistry DI, zero-GC policy, native memory/jobs, crash telemetry, and AUP origin safety.
Rejected Alternatives: Bulk loading all .agents-skills would add irrelevant physics/rendering noise and increase risk of cross-domain edits.
Scalability potential: Low uses static baked glyph atlases and raw byte spans; Middle adds staged font swap; High/Ultra can spend saved CPU/GC on richer glyph sets and visual text decay without hot-path allocation.
Hardware Impact: Expected low-end gain on i3/MX350 is prevention of language-switch GC spikes and UI refresh heap churn; exact microseconds pending profiler/build evidence.

## Decision 0 - Batch Memory

Problem: Context compression and parallel agents make chat memory unreliable.
Solution: Created Status_UI_LOCALIZATION_BABEL.md and Rationale_UI_LOCALIZATION_BABEL.md as disk-backed state before code edits.
Rejected Alternatives: Chat-only checklist; rejected because batch protocol says CTO reads disk logs, not chat.
Scalability potential: Persistent state supports at least five iterative loops without architectural drift.
Hardware Impact: No runtime impact; process safety only.

