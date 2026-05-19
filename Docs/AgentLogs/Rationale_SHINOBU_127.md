# Rationale_SHINOBU_127

Problem: User assigned SHINOBU_127 as ARMOR_PENETRATION_BALLISTICS_EXPERT, but the active `Docs/Tasks/CURRENT_BATCH.md` contains no `<AGENT_PROMPT id="SHINOBU_127">` block. The strict batch protocol requires extracting the exact XML block and using its task count before code.

Solution: Halt implementation and mark the work `[BLOCKED BY DEPENDENCY]`. Status was recorded in `Docs/Tasks/Status_SHINOBU_127.md`; final report will be appended to `Docs/AgentLogs/LOG_SHINOBU_127.md`.

Rejected Alternatives: Inferring a 20-task ballistics scope from archived batch prompts or neighboring agents was rejected. Archived prompts are stale by the batch hygiene rule, and neighboring tasks would contaminate architecture decisions. Coding from the user's paraphrase alone was rejected because the protocol requires exact XML extraction and task-count verification.

Scalability potential: Not implemented. Expected future direction remains math-only Burst AABB trajectory tests, Vault-backed projectile/target DTOs, continuous `GlobalQualityWeight` for substep/ricochet fidelity, and low/middle/high/ultra behavior without binary quality switches.

Hardware Impact: No runtime code changed. Estimated gain remains unverified. Expected target after valid prompt: eliminate projectile `Rigidbody` and hot-path `Physics.Raycast`, replacing them with Burst AABB sweeps and LUT penetration to avoid main-thread PhysX stalls on i3/MX350.

Black Box: Not implemented because no authenticated active task block exists. Future implementation must include a 300-frame fixed-size telemetry ring and dump to `Docs/AgentLogs/Dump_SHINOBU_127.bin` on NaN or invalid ballistic state.

## 2026-05-19 Recheck After Ultra Mandate

Problem: The user supplied an additional `<ULTRA_THINK_POLISH_MANDATE agent_id="[YourID]">`, but it is not the required active `<AGENT_PROMPT id="SHINOBU_127">` block and does not contain the original 20-task matrix. The active `CURRENT_BATCH.md` still has no `SHINOBU_127` prompt after a fresh CLI extraction.

Solution: Keep the task blocked and update status/log evidence. Do not mutate runtime combat/physics/Vault code from inferred requirements. The architecture law says exact XML extraction and task-count verification are mandatory before coding.

Rejected Alternatives: Treating archived `COMBAT_ARMOR_PENETRATION` prompts as current was rejected because batch hygiene forbids old batch logs/prompts unless explicitly ordered. Treating the user's prose as the missing XML was rejected because the mandate itself orders re-reading the original XML assignment and its 20 tasks. Treating neighboring SHINOBU prompts as context was rejected because strict parsing requires deleting non-owned prompt text.

Scalability potential: Future authorized implementation must expose continuous quality weight for ballistic substep count, AABB candidate stride, ricochet resolution richness, telemetry sample cadence, and Dear Lie presentation richness. Low: one swept AABB against coarse local boxes, minimal ricochet. Middle: per-material LUT and limited ricochet. High: additional armor-normal refinement and hydrodynamic drag integration. Ultra: finer deterministic substeps and richer GPU impact/scar data, still no projectile Rigidbody or hot `Physics.Raycast`.

Hardware Impact: No new runtime code. No microsecond savings are claimed. Expected valid scope remains replacing main-thread PhysX projectile/Raycast queries with Burst math over Vault state to reduce i3/MX350 stalls, but proof is pending the real prompt and profiler/GCMonitor data.
