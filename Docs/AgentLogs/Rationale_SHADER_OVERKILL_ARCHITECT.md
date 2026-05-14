# Rationale_SHADER_OVERKILL_ARCHITECT

Status: PENDING VERIFICATION
Agent: SHADER_OVERKILL_ARCHITECT

## Decision 001 - Active Dependency Logs Missing
Problem: The prompt mandates reading `Docs/AgentLogs/Rationale_CAUSTICS_PROJECTION.md` and `Docs/AgentLogs/Rationale_MATERIAL_DECAY.md`, but both active files are absent.
Solution: Record the absence as an evidence gap, inspect current shader/C# implementation sources, and avoid claiming inherited proof from missing logs.
Rejected Alternatives: Reading archived batch logs as authority was rejected because current AGENTS hygiene forbids stale-batch log use unless explicitly current; treating absent logs as read was rejected as fake reporting.
Scalability potential: Low/Middle/High/Ultra remain shader-tier driven; missing logs do not block implementing tier gates, but they block claiming historical runtime proof.
Hardware Impact: Estimated runtime gain from this decision is 0 us; it prevents incorrect dependency assumptions on i3/MX350.

## Decision 002 - Mandate Set
Problem: The shader library crosses SRP batching, AUP precision, Resident Drawer, GraphicsBuffer instance data, dither transparency, caustics, and zero-GC C# property IDs.
Solution: Read `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `REND_DescriptorBinding_Reality_Check`, `REND_GPU_Sovereignty`, `MATH_AUP_Determinism_Sync`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, and `OPT_Zero_GC_Policy_AllocFree_Mandate`.
Rejected Alternatives: Generic URP shader implementation was rejected because the project requires specific AUP and SRP/Resident Drawer constraints; adding a render pass was rejected because the prompt asks for shader core library first.
Scalability potential: Low disables POM/caustics/bending; Middle enables cheaper caustic/detail; High enables POM/caustics/bending; Ultra increases visual overkill through stricter samples and richer emission without changing CPU path.
Hardware Impact: Estimated low-end gain versus fragmented shader/material path is 30-120 us CPU SetPass overhead pending Frame Debugger proof; shader GPU cost remains tier-gated.
