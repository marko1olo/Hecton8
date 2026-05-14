# Rationale_RESONANCE

Agent: CORE_RESONANCE_ORCHESTRATOR
Domain: SYSTEMS_ARCHITECT / Resonance Orchestration
Status: ACTIVE / PENDING VERIFICATION

## Mandate Set

Selected mandates:

- `ARCH_Execution_Phases.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `CORE_Weather_Abyssal_FlowField_Currents.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`

## Decision 0 - Prompt Source

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain this agent's XML block, but the user supplied the complete `<AGENT_PROMPT id="CORE_RESONANCE_ORCHESTRATOR">` in chat.

Solution: Treat the in-chat XML as the authoritative batch prompt for this run. Record the missing batch-file extraction as evidence instead of fabricating a prompt path.

Rejected Alternatives: Blocking on a missing batch-file block would stall the assignment. Reading neighboring prompts would violate strict parsing.

Scalability potential: Low tier gets no runtime change from this decision. High and Ultra tiers get cleaner architecture work because no unrelated prompt bleeds into the plan.

Hardware Impact: 0 microseconds at runtime. Documentation-only decision.

## Decision 1 - Rationale File Naming

Problem: Global protocol names `Rationale_[YourID].md`, while the XML prompt explicitly requires `Rationale_RESONANCE.md`.

Solution: Use `Rationale_RESONANCE.md` as the canonical rationale file because the task prompt names it directly. Maintain `Status_CORE_RESONANCE_ORCHESTRATOR.md` for the global state-machine protocol. Add `Rationale_CORE_RESONANCE_ORCHESTRATOR.md` as a pointer alias only, so the global anti-amnesia lookup resolves without duplicating journal content.

Rejected Alternatives: Creating only `Rationale_CORE_RESONANCE_ORCHESTRATOR.md` would miss the prompt-specific file. Duplicating full content into two rationale files would increase log drift risk.

Scalability potential: Low/Middle/High/Ultra unaffected at runtime.

Hardware Impact: 0 microseconds at runtime. Documentation-only decision.
