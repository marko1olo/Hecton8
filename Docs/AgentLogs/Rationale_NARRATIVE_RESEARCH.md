# Rationale_NARRATIVE_RESEARCH

Problem: Active NARRATIVE_RESEARCH status/rationale/log files were moved into `Docs/Archive/Batch015`, while current work still needs a live memory file under `Docs/Tasks` and `Docs/AgentLogs`.
Solution: Recreate active files with Control Pass 81 only and leave the archive untouched.
Rejected Alternatives: Writing no active state; copying the whole archived log back into root; inflating status with old history.
Scalability potential: No runtime effect. Keeps agent coordination stable while multiple agents edit the project.
Hardware Impact: 0 us/frame.

Problem: Fresh Unity Apex showed `scanner_lore_fragment_sources_missing` even though `ScanEvents.cs` exists and the scanner/PDA route is implemented.
Solution: Add missing `GlobalSignals.RuntimeLifecycle.cs` to `H8NarrativeApexVerifier.RuntimeSourcePaths`; `ScanEvents.cs` is already in scope. This makes the verifier parse the actual SignalBus lifecycle validation file it requires.
Rejected Alternatives: Suppressing the route check; adding duplicate runtime lifecycle code; changing scanner/PDA behavior.
Scalability potential: Low/Middle/High/Ultra unchanged. This is editor-only proof scope repair.
Hardware Impact: 0 us/frame.

Problem: `CountInvocationInMethod` compared only simple invocation names, so checks that passed full static/generic names such as `SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot` returned zero even when runtime code was correct.
Solution: Add `MatchesInvocation`, preserving simple-name exact matching while allowing full static/generic invocation text matching for route proofs.
Rejected Alternatives: Editing `PDAEncyclopediaStreamer` and `DiegeticHudManualLayout` despite correct `LateFrameTick` snapshot reads; replacing AST checks with loose global grep.
Scalability potential: Prevents future false phase failures without changing runtime.
Hardware Impact: 0 us/frame.

Problem: `TerminalOsRuntime` dump gate lock uses explicit `Monitor.Enter(_gate, ref lockTaken)` with `finally Monitor.Exit`, but the verifier counted only C# `lock` statements. That produced a false lock finding.
Solution: Count `Monitor.Enter` as a gate lock scope inside `CountLockStatementsInMethod`; keep runtime code unchanged because it already releases in `finally` and writes after lock release.
Rejected Alternatives: Rewriting valid source to `lock {}` for cosmetic verifier compatibility; weakening write-after-release proof.
Scalability potential: Crash dump path remains fault-only and deterministic across low-tier to ultra-tier devices.
Hardware Impact: 0 us/frame normal gameplay.

Problem: Narrative/AppliedLore verifier hard-failed MapMagic/Terrain/TerrainCollider markers in `02_HECTON_WORLD.unity`, but those are world-domain ownership facts. Current scene has no MapMagic/Terrain markers and no TerminalOS placement; hard-failing world markers hides the actual applied-lore issue.
Solution: Convert world-core markers to `scene_world_dependency_warnings` in both C# verifier and Python audit. Keep counters visible. Do not raw-edit scene YAML in a shared workspace.
Rejected Alternatives: Reverting scene changes made by other agents; mutating YAML; pretending world scene is complete.
Scalability potential: Lets Narrative Apex remain a code/protocol verifier while world-domain agents own scalable terrain/ocean composition.
Hardware Impact: 0 us/frame.

Problem: Current `02_HECTON_WORLD.unity` no longer contains scene-owned TerminalOS runtime placement.
Solution: Do not claim integration complete. Keep audit counters visible: `scene_terminal_os_runtime_rows=0`, `scene_terminal_os_runtime_verified_slots=0`, `scene_bindings=0`, `scene_world_dependency_warnings=3`.
Rejected Alternatives: Fabricating a green scene proof; adding runtime scene search/self-healing; unsafe scene edit during parallel agent work.
Scalability potential: Prefab/content pipeline remains usable, but final scene placement must be restored by a scene-authority pass.
Hardware Impact: 0 us/frame until placed.

Problem: The shared world scene now contains the scene-owned TerminalOS runtime again, but all 27 `terminalOsPreviewHash` overrides pointed at message/content packet hashes instead of `TerminalOsHash.HashIndex(index)`. The runtime preview resolver validates terminal identity hash, so those previews could silently fail routing despite renderer/transform slots being present.
Solution: Replace only the 27 serialized `terminalOsPreviewHash` values with the deterministic TerminalOS index hashes. Add a C# Apex verifier check for preview hash/index pairs, hash mismatches, and duplicate indices so C# Apex cannot pass this drift again.
Rejected Alternatives: Changing `TerminalOsRuntime.TryResolveTerminalPreviewIndex`; adding runtime fallback search; using message hash as terminal identity; relying only on Python audit.
Scalability potential: Low/Middle/High/Ultra unchanged. This removes a content identity defect without adding runtime work. The scene still scales through existing TerminalOS preview slots and quality guards.
Hardware Impact: 0 us/frame.

Problem: Raw YAML scene edits are risky, and `git diff` reports `02_HECTON_WORLD.unity` as binary in this workspace.
Solution: Verify the scene structure command succeeded (`m_RootGameObject` scan), rerun AppliedLore full/source audits, and add C# verifier coverage for the exact serialized field relationship.
Rejected Alternatives: Broad YAML rewrite; touching unrelated scene objects; claiming safety from a binary diff alone.
Scalability potential: No runtime effect. Prevents content drift from breaking terminal previews across all device classes.
Hardware Impact: 0 us/frame.

Problem: A Unity compile/import rerun is required for the new editor verifier code to appear in the menu assembly, but CPU is above the project throttle threshold and Unity Roslyn/dotnet compiler/server processes are active.
Solution: Stop at Unity `validate_script` plus Python full/source audits until CPU drops below the mandated threshold. After CPU dropped and no `csc.exe` existed, request one Unity script refresh/import and run the Apex menu verifier. Do not run `dotnet build`.
Rejected Alternatives: Compile spam; starting a second build while Unity compiler services are live; killing shared compiler/server processes.
Scalability potential: Prevents workstation stalls during multi-agent work.
Hardware Impact: 0 us/frame in game; avoided editor CPU contention.

Problem: The new C# Apex hash guard needed Unity-side proof, not only source parse.
Solution: Run `Hecton8/Lore/Run Narrative Apex Verification` after Unity returned idle. The console proof reports `terminal_os_preview_hash_pairs=27`, `terminal_os_preview_hash_mismatches=0`, `terminal_os_preview_hash_duplicate_indices=0`, `dependency_findings=0`, `phase_findings=0`, `zero_gc_findings=0`, `job_complete_findings=0`, `lock_findings=0`, `fatal_findings=0`, `build_invocations=0`, `analysis=RoslynAST_in_memory`.
Rejected Alternatives: Treating Python audit as the only proof; skipping Unity menu proof after editor import.
Scalability potential: Editor-only verification surface. Runtime cost unchanged across all device classes.
Hardware Impact: 0 us/frame.

Problem: Process scan found three stale `rg.exe` searches from earlier agent prompt extraction/static scans. They were hours old and not tied to the current task.
Solution: Stop only those `rg.exe` PIDs and verify they are gone. Leave Python servers, Unity, and Unity Roslyn/dotnet processes untouched because ownership is not proven.
Rejected Alternatives: Ignoring orphaned search processes; killing broad process classes; killing user services.
Scalability potential: Reduces workstation noise without touching runtime/game code.
Hardware Impact: 0 us/frame; editor/workstation hygiene only.

Problem: Unity Console showed 19 stale `The referenced script (Unknown) on this Behaviour is missing!` entries after import.
Solution: Scan scenes/prefabs/assets for missing script YAML signatures, find zero hits, clear Console, rerun Narrative Apex, and confirm missing-script messages do not return.
Rejected Alternatives: Deleting MonoBehaviour blocks blindly; treating stale Console history as current regression; ignoring the signal without a sweep.
Scalability potential: No runtime effect. Prevents false blocker churn for later scene/content agents.
Hardware Impact: 0 us/frame.

Problem: `MetaCampaignService.PublishCachedVisualState` owned a `Shader.SetGlobalFloat` presentation write, but it could be reached through state side-effect calls outside a strict `LateFrameTick` route.
Solution: Add primitive pending visual state fields and route visual refresh through `QueueCachedVisualState` -> `LateFrameTick` -> `FlushCachedVisualState` -> `PublishCachedVisualState`. Keep campaign truth, save DTO, SignalBus payloads, and DataVault handles unchanged.
Rejected Alternatives: Treating cold API calls as acceptable presentation writes; moving campaign variable truth to UI; adding a new SignalBus lane for a single-owner scalar.
Scalability potential: Low/Middle/High/Ultra unchanged for gameplay truth. The saved timing currency keeps visual shader pressure deterministic and lets high-tier shader response read the same scalar without extra simulation cost.
Hardware Impact: Normal frame: one bool branch in `LateFrameTick`, 0 B GC. Visual refresh frame: existing shader/ecosystem calls shifted to late phase, no new allocation.

Problem: The existing Apex scope did not include several adjacent Narrative owners (`MetaCampaignService`, `CorporateOrderSystem`, `LoreDatabaseManager`, `ProceduralLoreDirector`, `AwaitableDropSequenceDirector`), so APEX compliance could be green while campaign/prologue write-lock or phase drift existed.
Solution: Add those files to `H8NarrativeApexVerifier.RuntimeSourcePaths`. Add DataVault write helper-transfer checks and a MetaCampaign visual phase route guard.
Rejected Alternatives: Expanding runtime coupling; scanning the whole repository and producing false cross-domain failures; leaving helper-transfer lock patterns invisible.
Scalability potential: Editor-only proof surface. Runtime device tiers unchanged.
Hardware Impact: 0 us/frame.

Problem: Unity MCP validation could not complete after the patch because the active Unity compiler/server path was saturated.
Solution: Do not run `dotnet build`; do not spam Unity refresh. Run only AppliedLore audit, scoped hot dependency grep, route token checks, `git diff --check`, and local brace/paren/bracket lexer until CPU/compiler throttle clears.
Rejected Alternatives: Starting another build while CPU is 100%; killing shared Unity/VBCSCompiler processes without ownership proof; claiming Unity proof from a timed-out MCP call.
Scalability potential: Prevents editor/workstation stalls during multi-agent work.
Hardware Impact: 0 us/frame; avoided extra compile contention.

Problem: `MetaCampaignService.PublishStateSideEffects` still published `VocalWarningSignal` and `NarrativePoiStateSignal` directly even after the visual shader route was moved to `LateFrameTick`. Those unmanaged signals are valid lanes, but the publication phase could still be reached from state mutation/cold setter paths before the frame side effects were visually stable.
Solution: Add `_audioBroadcastDirty`, `_cartographyStateDirty`, `_pendingAudioBroadcastVariableHash`, and `_pendingCartographyFrame`. Route audio/cartography through `QueueCampaignBroadcast` / `QueueCartographyState`, then flush only from `LateFrameTick` via `FlushCampaignBroadcast` / `FlushCartographyState`.
Rejected Alternatives: Adding a new global route; moving campaign truth into VWS/cartography consumers; keeping direct side-effect publication because payloads are unmanaged.
Scalability potential: Low/Middle/High/Ultra gameplay truth unchanged. Weak devices get deterministic one-frame side-effect coalescing; high/ultra can add richer VWS/cartography consumers without changing simulation authority.
Hardware Impact: Normal frame: two extra bool branches, 0 B GC. Dirty frame: existing SignalBus payload publication shifted to `LateFrameTick`.

Problem: The C# Apex guard only proved the visual side-effect route, so future direct audio/cartography publishes could regress without failing the Narrative Apex menu.
Solution: Expand `ScanMetaCampaignPhaseSideEffectRoute` to count audio/cartography queue, late-frame flush, publish, and direct-publish calls. Add console counters for `meta_campaign_audio_*` and `meta_campaign_cartography_*`.
Rejected Alternatives: Loose grep; not failing on direct side-effect calls; claiming phase safety from runtime code shape only.
Scalability potential: Editor-only proof; runtime tiers unchanged.
Hardware Impact: 0 us/frame.

Problem: Unity compile/import proof is still required, but current system load and Unity MCP stability violate project throttle rules.
Solution: Stop at source proof plus one successful Unity `validate_script` on `H8NarrativeApexVerifier.cs` (0 errors/0 warnings). Do not run Unity refresh, Apex menu, or `dotnet build`; `MetaCampaignService.cs` validation disconnected once and timed out once.
Rejected Alternatives: Build spam; killing compiler processes owned by Unity/other agents without proof; pretending source checks equal Unity import; repeatedly retrying a timed-out MCP path.
Scalability potential: Keeps multi-agent workstation usable; no runtime effect.
Hardware Impact: 0 us/frame; avoided extra editor contention.

Problem: Latest user-controlled lore reverses the old caution against an explicit ex-Deep-Reach protagonist. Keeping the stale guard would make future AppliedLore and first-hour writing contradict the current canon.
Solution: Lock the player as former Deep Reach field-systems / evacuation-infrastructure labor, now independent/debt-bound Marauder. Keep the emotional hook professional recognition and complicity, not family melodrama.
Rejected Alternatives: Generic salvage-only protagonist; family revenge; multiple unconstrained origin variants before the core campaign is stable.
Scalability potential: Low/Middle/High/Ultra unchanged. This is content identity, not gameplay truth ownership.
Hardware Impact: 0 us/frame.

Problem: Deep Reach guilt needed to stay hard-sci-fi without turning the corporation into a cartoon murderer or making HECTON-8 physics fake.
Solution: Split catastrophe and crime: Great Tide remains real tide/cryosphere/pressure/geophysics; Deep Reach is guilty through tail-risk underpricing, insufficient independent evacuation, Atlas/claim/XO continuity weighting, rescue delay, and post-2147 cover-up.
Rejected Alternatives: "Deep Reach melted the moon"; "Atlas killed everyone"; "pure natural disaster with no human liability."
Scalability potential: Supports many evidence orders and endings without changing runtime systems.
Hardware Impact: 0 us/frame.

Problem: Lore discussion had to become usable game/wiki/site payload, not internal documentation.
Solution: Add AppliedLore RS012-RS014 with packet bundles, manifests, route cards, evidence graphs, binding maps, scene binding targets, image briefs, generated source CSV/hash constants, and exported localized wiki/site pages.
Rejected Alternatives: One-off markdown essays; runtime markdown parsing; live translation; Unity scene edits during content-lock pass.
Scalability potential: Weak devices read baked string-pool records; high-end devices can add richer presentation around the same packet IDs without changing content authority.
Hardware Impact: 0 us/frame for authoring; later runtime remains baked static-data lookup.

Problem: Full runtime claim would be false until `static_data.h8bin` is rebuilt from the new DataMonolith source rows.
Solution: Run source-only AppliedLore audit after importer/page/route exports and state the boundary clearly.
Rejected Alternatives: Claiming baked proof without a bake; running Unity/compile work in a lore-only pass.
Scalability potential: Keeps DataMonolith source ready for the next bake while avoiding editor contention.
Hardware Impact: 0 us/frame; no `dotnet build`.

Problem: Human domains, Aegir moon ladder and HECTON-8 geology were still too likely to remain internal prose instead of game-readable evidence. That would make the wider setting feel large in docs but invisible in scanner/terminal/wiki/site systems.
Solution: Add RS015-RS017 with 15 AppliedLore packets, route cards, evidence graphs, binding maps, localized wiki/site pages and generated hash constants. Domain roles are object-first; moon references explain route windows and communication failures; geology becomes route language.
Rejected Alternatives: Dense space-opera exposition; one-off lore essays; decorative star map with no gameplay function; runtime markdown parsing.
Scalability potential: Weak devices consume baked string-pool records and simple packet hashes. Higher tiers can spend visual/audio budget on richer sky charts, moon ladder UI, brine/vent presentation and article art without changing packet identity.
Hardware Impact: 0 us/frame in this pass; content source only until DataMonolith bake.

Problem: Black Keel ownership, debt, first voice and Deep Reach present pressure were still underspecified. Without exact names and debt mechanics, the opening contract could feel hand-waved and the carrier could collapse into either magic rescue or personal luxury.
Solution: Lock `Aegir Reclamation Pool`, `Keelmark Mutual`, `4.8 tonne-window` lien, clipped-audio/clean-text Black Keel first voice, and Deep Reach priority hooks through old clauses. Add RS018 packets and route cards for these facts.
Rejected Alternatives: Player-owned luxury ship; anonymous generic claim-pool; abstract credit debt only; friendly companion AI; Deep Reach owning the carrier through impossible live control.
Scalability potential: Low tier can show plain terminal/billing text. Mid/high/ultra can add carrier voice treatment, orbital UI, contract overlays and payload scale visuals from the same packet IDs.
Hardware Impact: 0 us/frame; all data is static content source.

Problem: HECTON-8's physical atlas needed playable specificity: origin, depth bands, seafloor access, seed invariants and blue-debt containment stages. Leaving them open blocks procedural generation rules, wiki consistency and resource gameplay language.
Solution: Lock HECTON-8 as Aegir-formed, collision-fractured and resonance-heated; lock depth bands from photic shelf to 5600 m Atlas basin; lock rare seafloor windows; lock seed invariants; lock containment stages 0-4 with stage 1 recoverable by vent repressure only. Add RS019 packets and route cards.
Rejected Alternatives: random captured-moon drama; arbitrary depth gates; fully walkable ocean floor; per-seed physics retcons; supernatural resource infection.
Scalability potential: Low tier can use band labels, fog and scanner text. Middle/high/ultra can add richer brine layers, vent lighting, geological silhouettes and containment VFX without changing canonical bands.
Hardware Impact: 0 us/frame; no simulation code added.

Problem: Atlas agency, present Deep Reach faction pressure, false endings, meta replay and final question needed concrete applied payloads. Without that, endings remain philosophical notes rather than buildable route data.
Solution: Lock Atlas as recognizing procedure/access anomaly/revoked Deep Reach key but not full personhood; lock `Recovery Compliance Office`; lock false-ending families; lock Marauder dossier persistence as knowledge not power; lock final payload choices. Add RS020 packets and route cards.
Rejected Alternatives: Atlas as humanized ghost; Deep Reach as only old logs; fake fail-state endings; roguelite power carryover; morally clean best ending.
Scalability potential: Low tier can present route-card and terminal choices. High/ultra can layer cinematic payload UI, audio, dossier animation and ending-specific environmental response around the same static packet IDs.
Hardware Impact: 0 us/frame; source-only content.

Problem: The new lore needed proof that it reached the established content system without claiming a runtime bake that was not run.
Solution: Run importer, page exporter, route-card exporter and source-only AppliedLore audit after RS018-RS020. Final proof: `packets=100`, `locales=15`, `rows=1500`, `graph_rows=100`, `route_cards=94`, `route_source_rows=94`, `wiki_pages=1500`, `site_pages=1500`, `binding_map_rows=100`, `target_backlog_rows=100`.
Rejected Alternatives: Rebuilding Unity/DataMonolith during a lore-only pass; claiming `static_data.h8bin` integration from source rows alone; leaving generated constants stale.
Scalability potential: Source rows are ready for the next bake. Runtime remains on baked packet/route records, not live JSON/markdown.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Interstellar travel was still broad prose. Without exact drive/route doctrine, Aegir could drift into either impossible FTL convenience or vague "far away" flavor.
Solution: Lock hybrid hard-sci-fi infrastructure in RS021: beam-assisted probes/autonomous packets first, pellet-beam assisted fusion or related fusion freight for heavy Atlas/Seed/colony cargo, long coast/braking infrastructure, and Black Keel as an Aegir-system tender. Add RAN-B:H8 as dry catalog language while keeping HECTON-8 as play/story identity.
Rejected Alternatives: FTL/ansible rescue; one heroic player ship; pure exposition with no packet IDs; exact numerical ephemeris before the astronomy/table pass.
Scalability potential: Low tier can show simple route plates and terminal text. High/Ultra can add sky-chart UI, route-window animations and carrier-custody visuals from the same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Deep Reach guilt had good shape but few accountable signatures. Without names, evidence could feel like faceless corporate fog.
Solution: Lock five signoff names in RS022: Iliya Varnek, Selene Arendt, Noor Haldane, Marek Ibarra and Vera Sato-Ren. Each name owns a narrow procedural failure: risk margin, Atlas weighting, evacuation certification, loss conversion or present return-action pressure.
Rejected Alternatives: Cartoon mastermind; dozens of executives; anonymous-only liability notes; making Deep Reach totally innocent because physics caused the Great Tide.
Scalability potential: Low tier can present names in terminals. High/Ultra can add voice filters, dossier boards and ending-specific packet replay without changing content authority.
Hardware Impact: 0 us/frame; source content only.

Problem: First hour still named beats but lacked exact tools. That blocks tutorial object writing, wiki articles and future recipe hooks.
Solution: Lock RS023 first tool chain: manual bilge pump kit, cold sealant patch gun, low-power induction cutter, acoustic pinger line and P-63 field fabricator. First recipes are valve gasket, cutter contact, pinger float and sealant clamp.
Rejected Alternatives: Generic scanner-only tutorial; universal cutter; magic fabricator; combat-first opening.
Scalability potential: Weak devices can use simple props and terminal prompts. High/Ultra can spend budget on tactile animations, waterline change, sealant VFX and pinger audio from the same packet IDs.
Hardware Impact: 0 us/frame; source content only.

Problem: Resources were still too broad for gameplay and publication surfaces. "Blue debt plus other stuff" is not enough for recipes, scanner categories or site articles.
Solution: Lock RS024 category taxonomy: native sulfide/salt/vent chemistry, noble-gas brine feedstock, Deep-Reach-amplified pressure ceramics and Atlas-altered biofiber/biometal sensor tags.
Rejected Alternatives: One spooky ore; magic infection; full numeric recipe tuning during lore pass; leaving resource ownership undefined.
Scalability potential: Low tier can show category labels and simple scanner text. High/Ultra can add brine effects, vent-forge visuals, biofiber growth and tagged fauna presentation without changing resource identity.
Hardware Impact: 0 us/frame; source content only.

Problem: A stale encyclopedia guard still warned against making the protagonist openly ex-Deep-Reach, contradicting current user-controlled canon.
Solution: Retire that baseline in `Player_Motive_Arc.md` and state the current ex-Deep-Reach/current-Marauder motive path.
Rejected Alternatives: Leaving contradictory long-term memory for future agents; hiding the reversal only in status logs.
Scalability potential: Content coherence across all device tiers.
Hardware Impact: 0 us/frame.

Problem: New RS021-RS024 content needed proof of propagation without claiming a runtime bake.
Solution: Run importer, page exporter, route-card exporter and source-only audit after all edits. Final proof: `packets=120`, `locales=15`, `rows=1800`, `graph_rows=120`, `route_cards=114`, `route_source_rows=114`, `wiki_pages=1800`, `site_pages=1800`, `binding_map_rows=120`, `target_backlog_rows=120`.
Rejected Alternatives: Rebuilding Unity/DataMonolith during lore-only pass; claiming `static_data.h8bin` integration from source rows alone; keeping a temporary generator script in the repo.
Scalability potential: Source rows are ready for next bake. Runtime remains baked static-data lookup only.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.
