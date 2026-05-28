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

Problem: `ContentTieredGroupPolicy` used hard VRAM branches: `<=2048 MB` forced low visual budget and `>4096 MB` unlocked overkill. That creates binary platform behavior and ignores `GlobalQualityWeight`, XR pressure, and runtime thermal/load-shed state.
Solution: Added continuous `ResolveRuntimeVisualBudgetWeight01()` combining `HomeostasisBrain.GlobalQualityWeight`, smoothed graphics-memory capacity, XR ceiling, and content-tier ceiling. Visual budget fields now derive from weighted lerps; overkill download requires weighted threshold instead of raw VRAM.
Rejected Alternatives: Keeping raw `SystemInfo.graphicsMemorySize` forks or adding a new platform service. Standard Unity quality tiers were rejected because they are authoring labels and do not represent runtime pressure.
Scalability potential: Low keeps 1D LUT/triangle/dot-product dear-lie features with 512 particles and 8 raymarch steps; middle/high smoothly add silt, salt, hull dents, raymarch and POM budget; ultra reaches 16K particles/64 steps/16 POM taps when the scalar permits.
Hardware Impact: MX350/Quest-like budgets cannot accidentally download overkill content; high-end PCs can still spend budget on richer content when `GlobalQualityWeight` and hardware capacity agree. Estimated hot-path GC change: 0 B; CPU delta expected below 1 us per policy call.

Problem: `WorldChunkResidencyManager.ResolvePredictiveVramAbortState()` returned `false` for any GPU reporting more than 2048 MB. That means predictive streaming ignored VRAM pressure on high-end PCs, Steam Deck-like shared memory if misreported, and any future platform with pressure above the baseline.
Solution: Removed the hard skip. Abort threshold now scales from MX350 survival threshold to a capped visual-overkill ceiling through `ResolveSmoothGlobalQualityWeight01()`. Shared-memory devices use `HardwareTierDetector.RecommendedVramBudgetBytes`. Resume threshold uses proportional hysteresis instead of a fixed 1.4 GB floor.
Rejected Alternatives: Applying the MX350 1.6 GB threshold to every GPU. That would protect weak devices but punish high-end visual residency. Also rejected disabling predictive streaming globally under pressure; scoped only predictive requests.
Scalability potential: Weak/shared-memory devices abort predictive loads early; middle devices expand modestly; high/ultra allow longer predictive residency up to 4 GB while retaining pressure hysteresis.
Hardware Impact: i3/MX350 keeps 1.6 GB abort / 1.4 GB resume behavior; top-tier can keep more streamed chunks before abort. Estimated saved hitch risk: avoids uncontrolled predictive loads under pressure; exact microseconds pending profiler/player proof.

Problem: Verification compile is required, but host CPU was measured at 100%, and project law forbids `dotnet build` when CPU exceeds 50% or another compiler is active.
Solution: Did not launch build. Ran static platform proof audit and source-pattern checks. Marked compile/runtime status `PENDING VERIFICATION`.
Rejected Alternatives: Forcing a build under load or claiming Unity readiness from static scans.
Scalability potential: No runtime change.
Hardware Impact: No compile contention added to the shared machine.
