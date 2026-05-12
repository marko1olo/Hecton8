# WORLD_RESOURCE_SPAWNER Status

Status: PENDING VERIFICATION
Domain: WORLD GENERATION & TERRAIN / Geological Node Spawner
Prompt source: `Docs/Tasks/CURRENT_BATCH.md` `<AGENT_PROMPT id="WORLD_RESOURCE_SPAWNER">`
Primary objective count: 18

## Preflight
- [x] Prompt extracted by CLI | Justification: batch protocol requires exact XML isolation before architecture decisions; attribute-aware regex captured the full prompt after the id-only regex failed | Alternative rejected: IDE tab or basic reader because neighboring prompts can contaminate scope | Estimate: 1700 us
- [x] Status/rationale hygiene checked | Justification: missing files mean no stale batch data; fresh state can be initialized without archive wipe | Alternative rejected: appending to unrelated logs | Estimate: 900 us
- [x] Relevant mandates selected | Justification: ore spawning touches deterministic RNG, AUP, MapMagic projection, SoA resource data, native jobs, zero-GC hot paths, save deltas, and crash telemetry | Alternative rejected: loading the full registry and diluting decisions | Estimate: 3200 us

## Loop 1 - Tasks 1-5
- [ ] Task 1 - Singleton eradication | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 2 - Signal migration | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 3 - ASMDEF isolation | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 4 - Residency audit | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 5 - LCG determinism | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Compile verification after Loop 1 | Result: PENDING

## Loop 2 - Tasks 6-10
- [ ] Task 6 - SoA geology data | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 7 - MapMagic projection | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 8 - Slope rejection | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 9 - Biome restriction | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 10 - BRG instancing | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Compile verification after Loop 2 | Result: PENDING

## Loop 3 - Tasks 11-15
- [ ] Task 11 - Proximity hydration | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 12 - Depletion bitmask | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 13 - Mining yield signal | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 14 - Origin shift sync | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 15 - RLE save delta | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Compile verification after Loop 3 | Result: PENDING

## Loop 4 - Tasks 16-18
- [ ] Task 16 - Math LOD | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 17 - No coroutines / SlowTick job | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Task 18 - Telemetry | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Compile verification after Loop 4 | Result: PENDING

## Loop 5 - Recursive Re-Verification
- [ ] Re-read prompt after core tasks | Result: PENDING
- [ ] Squared-distance hydration audit | Result: PENDING
- [ ] ASMDEF direct-core reference audit | Result: PENDING
- [ ] Self-read missed-work pass | Result: PENDING
- [ ] Polish mandate execution | Result: PENDING
