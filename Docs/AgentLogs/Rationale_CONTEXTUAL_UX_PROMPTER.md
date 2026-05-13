# CONTEXTUAL_UX_PROMPTER Rationale

Status: PENDING VERIFICATION

## Decision 0: Architecture Entry
Problem: Existing implementation unknown; diegetic input prompts must not create a singleton or Canvas tutorial overlay.
Solution: Inspect project contracts first, then implement through UI-domain files with GlobalRegistry/EventBus boundaries and fixed buffers.
Rejected Alternatives: A screen-space Canvas tooltip, a TooltipManager.Instance singleton, or direct dependency on player controller concrete classes; all violate local mandates and parallel-agent isolation.
Scalability potential: Low uses one prompted target with instant fade and minimal math; Middle adds dither fade; High adds richer atlas glyph support; Ultra can spend saved CPU on denser visual effects without changing gameplay authority.
Hardware Impact: Expected hot-path target under 100 us on i3/MX350 by using fixed arrays, cached references, and no per-frame string or dictionary work. Measured proof absent.
