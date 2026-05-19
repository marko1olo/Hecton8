# Rationale_SHINOBU_137

Status: PENDING VERIFICATION
Agent: SHINOBU_137
Domain: SUBMARINE_OS_TERMINAL_RENDERER

## Prompt Extraction
Problem: Need exact assignment isolation inside multi-agent batch.
Solution: Extracted `<AGENT_PROMPT id="SHINOBU_137">` from `Docs/Tasks/CURRENT_BATCH.md` using PowerShell raw file regex, not truncated MCP reading.
Rejected Alternatives: Reading adjacent prompts or inferring domain from chat text would violate strict parsing and create cross-agent contamination.
Scalability potential: Assignment targets terminal UI that remains cheap on weak devices and buys visual overkill via shader treatment on high-tier hardware.
Hardware Impact: Prevents terminal UI from paying Canvas mesh rebuild cost on i3/MX350; exact savings require Unity profiler evidence.

## Mandate Selection
Problem: Terminal renderer crosses UI, Burst DTO layout, AUP math, native buffers, execution phases, and signal lanes.
Solution: Read eight mandate files: UI diegetic interfaces, UI zero-GC streaming, ARM64 runtime layout, AUP determinism, zero-GC policy, native jobs, execution phases, signal segregation.
Rejected Alternatives: Reading all 80 mandate files would add noise; reading only UI files would miss AUP/native/signal constraints.
Scalability potential: Low/Middle/High/Ultra behavior must be a continuous curve via `GlobalQualityWeight`, not tier if-branches.
Hardware Impact: Mandates prioritize flat native buffers and staggered VISUAL_SYNC uploads, reducing CPU stalls on low-end silicon.

## Mandate Conflict Note
Problem: `UI_Diegetic_Physical_Interfaces.txt` contains legacy wording enforcing World Space Canvas, while SHINOBU_137 batch explicitly orders World Space Canvas eradication.
Solution: Treat current agent prompt as the domain-specific migration order: physical terminals become quads/projected textures with mathematical interaction. Preserve useful math/RT pool/shader constraints from the mandate.
Rejected Alternatives: Keeping World Space Canvas would directly fail Tasks 01/02 and retain rebuild/raycaster cost.
Scalability potential: Quad + RT projection scales from low-res fake to high-tier holographic shader overkill.
Hardware Impact: Removes Canvas rebuild and GraphicRaycaster traversal from terminal hot paths; measured microseconds pending profiler.
