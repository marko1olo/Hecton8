# 14MU Rationale

Date: 2026-05-28
Status: PENDING VERIFICATION

Problem: `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="14MU">`; blindly reading nearby `14xx` prompts would contaminate the architecture decision stream.
Solution: Use the explicit user assignment as the active directive, keep `14MU` status/rationale/log files, and ignore neighboring batch prompts.
Rejected Alternatives: Mapping `14MU` to `1404`, `1406`, or `1427` by guess. That would violate strict parsing and could mutate the wrong domain.
Scalability potential: Keeps platform-adaptation decisions tied to the current user domain instead of one narrow mobile/XR batch role.
Hardware Impact: No runtime change; prevents wrong code paths from being edited under i3/MX350, Steam Deck, Quest, or console assumptions.

Problem: Platform domain has broad targets, but `PLATFORM_PORTABILITY_PROOF_LADDER.md` explicitly orders proof: Windows Editor/player and Copper Wire V0 first, then MX350, then Linux/Steam Deck, macOS, XR, Quest/PICO, consoles.
Solution: Treat platform work as blocker removal and static-risk reduction unless fresh device/player artifacts exist. Any "ready" wording stays `PENDING VERIFICATION`.
Rejected Alternatives: Making serialized package/settings presence equivalent to readiness. That ignores shader, native plugin, input, storage, thermal, profiler, GC, and player-launch proof.
Scalability potential: Low/Middle/High/Ultra remain supported as continuous weight bands; platform labels select endpoints or proof lanes, not separate gameplay truth.
Hardware Impact: Prevents spending weak-device budget on unproven XR/VRS claims; MX350 path remains render scale, fakes, culling, mip pressure, and continuous quality response.

Problem: Binary quality/platform branches risk stutter, visual popping, and divergent gameplay behavior across PC, Deck, VR, and console.
Solution: Use `HomeostasisBrain.GlobalQualityWeight` as source scalar. Hardware labels may choose curve endpoints; runtime fidelity, cadence, capacity, and presentation cost scale continuously with hysteresis.
Rejected Alternatives: `if (isLowEnd)`, `if (isQuest)`, or `QualitySettings.GetQualityLevel()` as gameplay/runtime truth. Standard Unity quality levels are authoring labels, not hot runtime authority.
Scalability potential: Weak devices keep silhouettes, fog LUTs, route cues, pressure audio, readable instruments; high/ultra spend saved cycles on silt wakes, wetness, longer LOD residency, richer material response.
Hardware Impact: i3/MX350 avoids frame spikes and shader bloat; strong PCs/PCVR get additive presentation without save/DTO/authority drift.
