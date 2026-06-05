# Orchestrator Subagent Synthesis - 2026-06-05

Status: `CONTROLLER_SYNTHESIS / PENDING UNITY PROOF`
Evidence class: `SUBAGENT_STATIC_AUDIT + STATIC_SOURCE + STATIC_DOC`

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, Addressables mutation, or project-setting mutation was performed by this synthesis.

## Runtime Player/HUD Audit

Detailed player/HUD/movement P0 synthesis is captured in `Docs/Orchestration/PLAYER_HUD_MOVEMENT_P0_SYNTHESIS_20260605.md`.

Source-backed blockers:

- `02_HECTON_WORLD.unity` still has an active scene-local `Player` candidate with enabled `HectonWorldShellController1428`.
- `HectonWorldShellController1428` reads direct input (`Keyboard.current`, `Mouse.current`, legacy `Input.GetKey`, `Input.GetAxisRaw`) and writes player/camera transform.
- `Player.prefab` has production movement/interactions/HUD components, including `HectonPlayerMovement`, `PlayerSwimPresentationController`, interaction, visor pieces, Rigidbody, and CapsuleCollider.
- Static scene evidence does not prove the production `Player.prefab` active in `02_HECTON_WORLD`.
- `InputDispatcher` and explicit-layout `PlayerInputState` exist and look like intended input authority; active route remains unproven.
- `HUD_Internal.prefab` has disabled `SuitHUDScreenCompositor` with latent `forceScreenSpaceOverlay: 1`.
- `Suit_HUD_Canvas.prefab` starts as overlay and `SuitHUDV4CanvasOverlay` attempts projection; active render mode/camera binding remains unproven.
- Two `InteractionUI` classes exist; active prompt route remains unproven.

Required split:

1. Readback Gate Agent: no mutation; prove active player, input, HUD root, canvas render modes, projection camera, and shell survival.
2. Player Authority Repair Agent: bootstrap/spawner/player prefab/scene binding only after readback.
3. Movement Proof Agent: dry walk, surface swim, underwater swim, ascend/descend, camera, collision authority.
4. Input Owner Agent: `InputDispatcher`, `PlayerInputState`, rebinding/device/focus, and removal of active direct polling.
5. HUD/Interaction Agent: visor/world-space projection, no gameplay overlay, prompt route and duplication proof.
6. PDA/Pause/Save UI Agent: only after input/HUD authority is known.
7. Telemetry Agent: 300-frame black-box rings/dumps and GC/profiler markers.
8. Product-Face Proof Agent: final manifest/checksum and pass/fail classification.

## Surface Route Audit

Detailed active/candidate/rejected route classification is captured in `Docs/Orchestration/SURFACE_ROUTE_STATIC_CLASSIFICATION_20260605.md`.

Core conclusion:

- Active saved ocean route is `02_HECTON_WORLD.unity` + `Ocean_Crest.prefab` + `Assets/Crest/Crest/Materials/Ocean.mat`, but current visual quality is rejected.
- `MAT_H8_SurfaceCrestOcean_1428.mat` is candidate-only and must not be blindly assigned.
- `SURFACE_HORIZON_SALT_HAZE_1428` / `H8_TEMP_SurfaceHorizonHazeProbe_1428` is rejected temp cover.
- Active terrain/sky/Aegir/foam routes are visible but below product floor.
- h8_1914 is diagnostic-only and cannot substitute h8_1475 proof.

## Asset/VFX/Audio Audit

P0 candidates:

- VFX DataVault: classify/repair `BiolumPulseSyncRuntime.cs` native ownership debt around `313/330/378/3987`.
- VFX DataVault: classify/repair `HectonMarineSnowRenderer.cs` persistent scratch around `673/674/712`, with allocation sites `1347/2005`.
- Compute particles: keep catalog static parity green; preserve continuous budget/scalability and split/stagger path if Ultra GPU proof rejects 512 groups.
- Audio route: close `MusicDirectorConfig_Global.asset` null `_musicMixerGroup` and `_stingerMixerGroup` blockers through Unity-safe workflow or owned DSP/native bypass.
- Player audio lifecycle: classify/reroute direct `Player.prefab` clip refs for `Underwater Ambient.wav` and `dive_splash.wav`.
- Audio policy: resolve hybrid duration/player-loop exception policy before import edits.
- Addressables: `Assets/AddressableAssetsData` has zero files; prepare one-owner/one-key/one-proof matrix only until Unity gate clears.
- Asset import: block false promotion of water contact, flora proxy materials, music/ambient/player loops, and UI SFX without material/audio/prefab readback.

Audio/Addressables P0 details are captured in `Docs/Orchestration/AUDIO_ADDRESSABLES_P0_SYNTHESIS_20260605.md`.

VFX DataVault P0 details are captured in `Docs/Orchestration/VFX_DATAVAULT_P0_SYNTHESIS_20260605.md`.

P1 candidates:

- Isolate editor/offline VFX scratch from runtime DataVault debt.
- Review `ShinobuPlasmaBeamRuntime.cs:1483` `Allocator.Temp` dump payload as telemetry/fault-path issue.
- Prepare Low/Middle/High/Ultra particle visual proof queue.
- Prove long-bed/stinger audio cadence, cooldown, silence windows, and warning priority.
- Triage long/multichannel/high-rate audio rows before import mutation.
- Prepare P1 Addressables group candidates for sky/Aegir, terrain/geology PBR, UI sprites, geology/flora prefab pools.
- Resolve Batch31 PBR/channel semantics and source-only texture rows before import.

## Rule Routing Front

Useful current docs:

- `Docs/AGENT_AUTHORITY_ROUTING.md` correctly defines start sequence, authority receipt, no-loss split protocol, and tool-specific shims.
- `.agents-skills/README.md` now states `80` mandate files and the `2-8` read rule.
- `Docs/DOC_GOVERNANCE.md` routes no-loss rule changes and generated snapshots.

Popper rule-routing audit completed:

- Static rule-routing audit result is `PASS`.
- Root `AGENTS.md`, `.codexrules/AGENTS.md`, and `.github/agents/AGENTS.md` byte-match. SHA256: `9249FB9CC17DACDA0B373B86840A481C53A17EE09DF28084183B0FF1D92BA15A`.
- `.agent/rules/AGENTS.md` is a thin delegate to root authority.
- `Tools/Docs/TestAgentRuleRouting.py` passed with `AGENT_RULE_ROUTING_CHECK=PASS`, `mandates=80`.
- `Tools/Docs/BuildProjectRootBiblesCombined.py --check` passed.
- Checked active files had no stored mojibake sequences and no replacement characters.
- The ledger embedded BOM marker before the preserved old body is intentional; the routing test depends on `EF BB BF[CORE IDENTITY]`.
- Visible mojibake from plain PowerShell `Get-Content` is display/decoding risk, not proved stored file corruption.

Remaining risk:

- Full mirrored `AGENTS.md` copies are synced now but brittle. One root edit without mirror sync creates divergent law.
- Historical `.agent/rules/*.md` are demoted with `alwaysApply: false`, but still contain generic Unity examples that conflict if a tool ignores metadata: `Update`, `FixedUpdate`, `LateUpdate`, `UnityEngine.Pool`, direct instantiation, `PlayerPrefs`, `GameManager.Instance`, `NetworkBehaviour`, `UnityTest`, and `yield return`.
- Current static routing test checks demotion markers/globs, not all prompt-poisoning content inside historical files.
- Concrete historical body anchors are captured in `Docs/Orchestration/RULE_ROUTING_HISTORICAL_AGENT_RULES_SCAN_20260605.md`.
- No independent pre-split source artifact was found beyond the ledger/self-hash route. This proves current preservation, not external reconstruction of the original monolith.
- No clause-by-clause promotion map from former monolithic law into route bibles/mandates exists. Until that exists, the ledger remains active detail/provenance.
- Do not edit or revert root/mirror AGENTS files from this orchestrator lane without dedicated approval.

## Controller Order

1. Keep build/Unity/profiler blocked while CPU or tooling gate is red.
2. Use runtime readback packet 05 before any player/HUD/movement repair.
3. Use h8_1475 anti-false-proof packet 37 before accepting any scenic capture.
4. Treat `H8VisualProofCapture1912.cs` as diagnostic-only until a no-mutation method is proven.
5. Do not let VFX/audio/Addressables static tables become acceptance claims.
6. Do not edit root rule files until a dedicated rule-routing owner has a no-loss migration plan and user approval.
7. If rule-routing repair is approved, preserve ledger bytes, then add a readable companion or expand tests; do not casually normalize the preserved body.

Final status: `PENDING VERIFICATION`.
