# Rationale_DOC_AUDIT

Agent ID: DOC_AUDIT
Domain: Documentation + Project Reality Audit
Status: PENDING VERIFICATION

Previous rationale history is archived under `Docs/Archive/Batch004/AgentLogs/Rationale_DOC_AUDIT.md`.

## Decision 021 - PDA Must Fail Closed When The Physical Shell Is Missing

Problem: Static prefab evidence still shows `Player.prefab` `PlayerPDA` with null `pdaPanel`, null `pdaCanvasGroup`, and null tabs, while static scene/prefab scans did not prove `DiegeticPDAController` placement. Before R22, `PlayerPDA.Open()` could set `IsOpen=true`, switch to UI input, release the cursor, request PDA depth of field, play audio, and raise PDA events even if no visible PDA shell had configured the panel. That is a first-hour input trap risk.

Solution: Keep the existing diegetic bridge architecture but make the backend fail closed. `PlayerPDA.Open()` now requires a renderable shell: non-null `pdaPanel` and at least one resolved tab. If the bridge has not configured the shell, open returns before mutating global PDA state or input maps. Input-map switches are also guarded against missing/uninitialized `GlobalRegistry.Input`. `ContentSanityValidator` now validates `Player.prefab` for `PlayerPDA` headless-open risk and exposes `PlayerPdaHeadlessOpenRisk` / `PlayerPdaBridgeWarnings` summary counters.

Rejected Alternatives: Creating a raw YAML PDA shell inside `Player.prefab` was rejected because hand-authoring a complex UI hierarchy without Unity import is corruption-prone and likely to fight concurrent UI agents. Adding another runtime auto-installer that instantiates UI was rejected because it would hide scene/prefab ownership instead of proving the physical shell. Leaving the issue documented only was rejected because a headless input lock is a player-facing failure mode.

Scalability potential: Low = on MX350, PDA open cannot capture controls into an invisible UI; missing shell is a clear validator/runtime error instead of a soft lock. Middle = editor validation makes prefab regressions visible before route tests. High = once the shell is mounted, richer PDA tabs can be measured without confusing backend state with presentation. Ultra = visual overkill should go into the physical tablet, phosphor/render-texture presentation, scanner visualization, and diegetic input after the shell route is proven.

Hardware Impact: Runtime guard runs only on PDA open/close input paths, not per frame; expected hot-path impact is 0 us/frame. Validator is editor-only. No Unity compile/import/PlayMode/profiler proof was run, so runtime cost remains PENDING VERIFICATION.
