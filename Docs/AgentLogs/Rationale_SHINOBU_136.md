# SHINOBU_136 Rationale

Status: PENDING VERIFICATION

## Decision 0 - Prompt Isolation
Problem: Batch file contains neighboring agent prompts with overlapping animation terms.
Solution: Extracted only `<AGENT_PROMPT id="SHINOBU_136">` through `</AGENT_PROMPT>` using CLI regex and discarded neighboring tasks.
Rejected Alternatives: Basic contextual reading of CURRENT_BATCH.md; it exposes adjacent SHINOBU_135/137 content and can corrupt architecture decisions.
Scalability potential: Keeps implementation scoped to player/humanoid kinetic animation; no cross-domain work that creates unnecessary runtime surface.
Hardware Impact: No runtime cost. Prevents accidental systems that would tax i3/MX350.

## Decision 1 - Fresh Disk Memory
Problem: Status and rationale files were missing for the current batch.
Solution: Created fresh `Docs/Tasks/Status_SHINOBU_136.md` and `Docs/AgentLogs/Rationale_SHINOBU_136.md` before code edits.
Rejected Alternatives: Chat-only state; violates anti-amnesia and reporting protocol.
Scalability potential: No runtime path affected.
Hardware Impact: No runtime impact.

## Decision 2 - Mandate Selection
Problem: SHINOBU_136 spans animation, IK, native memory, AUP, phase scheduling, DTO alignment, and crash telemetry.
Solution: Read 8 mandates before code: contextual IK, FABRIK/ground snapping, ARM64 layout, AUP determinism, execution phases, zero-GC, native memory/jobs, and blackbox telemetry.
Rejected Alternatives: Reading every registry file; excessive context without improving this domain. Reading only animation files; misses Vault, AUP, and telemetry acceptance gates.
Scalability potential: Low/Middle/High/Ultra path will use continuous GlobalQualityWeight for IK iteration count, cadence, and optional secondary motion.
Hardware Impact: Expected low-end gain comes from replacing Animator graph/object traversal with flat Burst math and striding; estimate remains PENDING until code and profiler evidence exist.

## Decision 3 - Animator Mandate Conflict Resolution
Problem: ANIM_Contextual_Physical_IK contains an older PlayableGraph/Animator integration option, while SHINOBU_136 explicitly requires total Animator eradication and direct matrix output to Vault.
Solution: Treat the batch prompt and AGENTS.md Animator removal requirement as dominant for this task. Use Burst jobs over flat DTOs and Vault/GraphicsBuffer matrix output, not Animator stream handles.
Rejected Alternatives: Keeping Animator as a hidden writeback surface; it preserves the black box and violates the user directive.
Scalability potential: Flat matrix output scales from minimal bone sets on weak devices to extra secondary bones/finger chains on Ultra without switching pipelines.
Hardware Impact: Removes Animator evaluation and graph traversal from player/humanoid runtime path; microsecond savings PENDING static and runtime verification.
