# Rationale: OCEAN_CHEMISTRY_ENGINEER

Domain: ENVIRONMENT_ENGINEER / Environment.Fluids  
Target: i3 / MX350 / Unity 6  
Status: PENDING VERIFICATION

## Mandate Basis

The implementation must replace trigger-driven brine with math-plane evaluation. Runtime checks must be allocation-free and compatible with absolute-universe coordinates. Rendering must use shader/post-process fakes rather than extra water meshes. Cross-domain coupling must use GlobalRegistry interfaces or EventBus signals.

## Decisions

### Bootstrap Memory

Problem: The assigned brine prompt is embedded inside a shared batch file with neighboring agent prompts. Mixing directives would create architectural drift.

Solution: Extracted only the OCEAN_CHEMISTRY_ENGINEER XML block from Docs/Tasks/CURRENT_BATCH.md and created this rationale plus Status_OCEAN_CHEMISTRY_ENGINEER.md before code edits.

Rejected Alternatives: Reading the full batch through memory or MCP view was rejected because it risks truncation and neighboring-prompt bleed.

Scalability potential: Low tier keeps behavior deterministic through scalar plane checks; high tier can spend saved cycles on stronger fog/audio treatment.

Hardware Impact: Prevents wasted engineering on irrelevant systems; runtime gain is not measurable, but prevents cross-domain churn on i3/MX350.

