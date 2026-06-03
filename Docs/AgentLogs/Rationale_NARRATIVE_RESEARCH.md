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

Problem: Human law and public memory were still broad enough to make Aegir either unreachable background or generic corporate frontier.
Solution: Lock split authority through Sol, Centauri, Barnard, Tau Ceti, Luyten and Aegir; define Marauders as a jurisdictional loophole; define evidence status through chain-of-custody, witness hash and relay notary; add RS025 packets.
Rejected Alternatives: Dense space-opera government map; one universal salvage law; public knowing every HECTON-8 detail; Deep Reach launching Aegir as a direct heroic Earth mission.
Scalability potential: Low tier can show compact ledger entries. Middle/High/Ultra can add route maps, custody UI and public-ledger screens using the same packet hashes.
Hardware Impact: 0 us/frame; static content source only.

Problem: Atlas public status and shutdown ethics were unresolved in a way that could collapse the final act into "kill AI or not".
Solution: Lock Atlas public front, insurance/personhood gap, classified weighting layer and public post-2147 memory; keep shutdown intentionally multi-valued by payload/receiver/evidence/ecology; add RS026 packets.
Rejected Alternatives: Legal personhood for Atlas; cartoon evil AI; single correct shutdown answer; pure mystic ocean mind.
Scalability potential: Low tier can present terminal/legal text. High/Ultra can stage competing UI/audio/environmental responses without changing the underlying choices.
Hardware Impact: 0 us/frame; static content source only.

Problem: Early exits needed to be satisfying as real outcomes but unsatisfying as final truth, or players would either ignore them or treat them as fake fail screens.
Solution: Lock material payout, same-seed partial return, corporate capture, quarantine hold and public ledger leak as real partial endings with bitter consequences; add RS027 packets.
Rejected Alternatives: Coward exit as joke; hidden fail state; one material ending only; forcing every player to reach Atlas before any ending.
Scalability potential: Low tier can resolve with text/cards. Higher tiers can add carrier/quarantine/public-ledger presentation around the same static route records.
Hardware Impact: 0 us/frame; static content source only.

Problem: Replay risked becoming power-roguelite progression, which would weaken survival engineering and long-form descent.
Solution: Lock dossier persistence as knowledge, not power; riskier contract seeds alter lien, weather/orbit, custody, sample requirements and evidence order; starting claim variants keep the same ex-Deep-Reach/current-Marauder protagonist; add RS028 packets.
Rejected Alternatives: Permanent oxygen/hull/gun upgrades; random protagonist identity per run before the canon campaign is stable; seed changes without narrative pressure.
Scalability potential: Low tier can show dossier text and contract toggles. High/Ultra can add richer dossier boards, route previews and ending record presentation without changing save authority.
Hardware Impact: 0 us/frame; static content source only.

Problem: Source audit exposed that forcing every new manual packet through `MessageTerminal` would require expanding TerminalOS scene renderer/transform slots from 27 to 42 while another Unity agent is active.
Solution: Route new manual rows through `NarrativeDiscovery` placement backlog and keep existing terminal slots unchanged. Packet terminal surfaces and route cards remain authored; scene placement waits for a deliberate Unity pass.
Rejected Alternatives: Editing active Unity scene during a parallel pass; leaving audit mismatch; creating terminal prefabs that no scene capacity can display.
Scalability potential: Low/Middle/High/Ultra unchanged until placement. Avoids scene conflict and keeps content bake-ready.
Hardware Impact: 0 us/frame; no Unity scene write.

Problem: New RS025-RS028 content needed proof of propagation without claiming a runtime bake.
Solution: Run importer, page exporter, route-card exporter and source-only audit after generation. Final proof: `packets=140`, `locales=15`, `rows=2100`, `graph_rows=140`, `route_cards=134`, `route_source_rows=134`, `wiki_pages=2100`, `site_pages=2100`, `binding_map_rows=140`, `manual_discovery_policy_rows=27`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Rebuilding Unity/DataMonolith during lore-only pass; claiming `static_data.h8bin` integration from source rows alone; hiding TerminalOS mismatch.
Scalability potential: Source rows are ready for next bake. Runtime remains baked static-data lookup only.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Aegir distance and travel time were still broad enough to become either soft-sci-fi convenience or unusable astronomy exposition.
Solution: Lock RS029 around a practical route model: Ran/Aegir is a roughly 10.5 light-year class target; probes and autonomous packets arrive first; heavy Atlas/Seed freight uses staged precursor route economics; human crew rotation is years/debt/custody; local relay windows remain orbit/weather/radiation constrained.
Rejected Alternatives: FTL/ansible rescue; exact ephemeris before table pass; decorative star map with no gameplay pressure; making Black Keel a personal interstellar ship.
Scalability potential: Low tier can show static route plates and packet timestamps. Middle/High/Ultra can layer sky-chart UI, orbital-window animation, packet spool audio and route custody screens on the same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Deep Reach had signatures but not a stable public/shell hierarchy for site/wiki articles, terminal stamps and legal-object writing.
Solution: Lock RS030: Deep Reach Extraterrestrial Development Combine as public name; Aegir Continuity Holdings as dirty shell; Atlas Continuity Office as continuity language owner; Keelmark Loss Desk as loss conversion surface; Recovery Compliance Office as current return-pressure chain.
Rejected Alternatives: Secret mastermind office; dozens of departments; unnamed corporate fog; heroic direct-Sol Deep Reach framing.
Scalability potential: Low tier can present stamps and ledgers. Higher tiers can add layered legal UI, dossier boards, voice filters and animated packet provenance without changing authority routes.
Hardware Impact: 0 us/frame; source content only.

Problem: The first hour had mood and tool names but needed playable sequence objects tied to evidence and future tutorial tasks.
Solution: Lock RS031: Black Keel contract approach under debt/blacklist pressure, drop damage that spends ascent capacity, Shallow Annex P-63 pump room, first sanitized Deep Reach accident packet, and first Atlas repair trace as useful repair around a human object.
Rejected Alternatives: Combat-first opening; generic exposition terminal; family motive; magic crash stranding; universal escape button.
Scalability potential: Weak devices can use simple props, static terminals and pump state. High/Ultra can spend budget on waterline change, crush-frame animation, sealant VFX, shallow beauty pass and subtle repair-life motion.
Hardware Impact: 0 us/frame in this pass; future runtime cost belongs to scene/tutorial implementation.

Problem: Colony humanity risked either anonymous ruins or family melodrama, both weaker than the project's procedural noir.
Solution: Lock RS032: colonists are written through shift crews, job cards, locker names, triage ledgers, route permissions, tool wear and Marauder correction notes. The player learns people through work evidence and procedural harm.
Rejected Alternatives: Lost-relative hook; pure memorial wall; generic worker ghosts; treating colony evidence as only exposition.
Scalability potential: Low tier can show names/cards/lockers. High/Ultra can add object-specific wear, localized name strips, handwriting overlays, audio fragments and seed-varying worker evidence.
Hardware Impact: 0 us/frame; source content only.

Problem: New route cards initially contained stale/fantasy packet IDs from memory (`P035_NO_ANSIBLE_DELAY`, `P058_ESCAPE_CHAIN_ASSEMBLY`, `P060_ATLAS_MAINTENANCE_ECOLOGY`, `P057_GREAT_TIDE_LIABILITY`, `P041_WORKER_LOCKERS`).
Solution: Use the route-card exporter and audit as hard proof, then replace every phantom dependency with real packet IDs: `P101_NO_FTL_ROUTE_ECONOMY`, `P059_ESCAPE_CHAIN_ASSEMBLY`, `P061_MAINTENANCE_ECOLOGY`, `P057_GREAT_TIDE_LIABILITY_CHAIN`, `P041_WORKER_LOCKER_ROW`.
Rejected Alternatives: Letting the audit fail; adding duplicate packets to satisfy bad names; hand-waving dependency mismatch as documentation-only.
Scalability potential: Stable packet graph lets all device tiers share the same unlock/evidence truth while presentation scales independently.
Hardware Impact: 0 us/frame; prevents content-route drift before bake.

Problem: RS029-RS032 needed proof of propagation without claiming runtime bake or Unity placement that was not performed.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=160`, `locales=15`, `rows=2400`, `graph_rows=160`, `route_cards=154`, `route_source_rows=154`, `wiki_pages=2400`, `site_pages=2400`, `manual_policy_rows=74`, `manual_discovery_policy_rows=47`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake while task is lore-only; expanding TerminalOS during parallel Unity work; claiming `static_data.h8bin` integration from source rows alone.
Scalability potential: Source rows and route cards are ready for bake. Runtime remains baked static-data and string-pool lookup, not live JSON/markdown/translation.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Human expansion still had useful major-domain roles but lacked a publication-safe route table, route names, duration bands and lower Deep Reach office labels. That gap would force future wiki/game text to invent names ad hoc.
Solution: Add RS033 packets P161-P165. Lock route-band distance scale, population/authority scale, public route names, transit duration bands, and lower Deep Reach office surfaces: `Contract Continuity Desk`, `Packet Notary Interface`, `Quarantine Review Gate`, `Asset Silence Board`, `Return Action Queue`.
Rejected Alternatives: Exact census/ephemeris dump before numeric table pass; dense space-opera polity list; leaving route names as prose-only.
Scalability potential: Low tier can show static route cards and stamps. Middle/High/Ultra can add animated route boards, packet-delay visualization and legal packet provenance using the same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Colony humanity had method but still lacked a reusable worker-name/job/locker/native-localization protocol. Without this, localization or prop passes could turn names into random memorial text or family melodrama.
Solution: Add RS034 packets P166-P170. Lock seed-safe worker names tied to roles/tasks, pressure job titles, locker prop variants, identity-safe localization handling, and shift crew story seeds.
Rejected Alternatives: Lost-relative hook; fully random worker names with no job function; live-translated personal names; memorial-only writing.
Scalability potential: Low tier can show label strips and job cards. High/Ultra can layer handwriting, prop wear, localized annotations, audio fragments and seed-specific object combinations without changing story truth.
Hardware Impact: 0 us/frame; source content only.

Problem: Resources and escape crafting still had taxonomy but not pressure-band recipe grammar, sample quality classes or vent forge process wording. That would make future recipe/UI text either generic crafting or magic ore.
Solution: Add RS035 packets P171-P175. Lock recipe pressure bands, pressure failure stage classes, blue debt quality classes, vent forge process steps and escape component route grammar.
Rejected Alternatives: Exact numeric balancing during lore pass; a single spooky resource tier; "repair capsule" shortcut; pure ore economy.
Scalability potential: Weak devices can present text, icons and static scanner evidence. Higher tiers can add vent forge animation, pressure lattice VFX and containment UI from the same static route records.
Hardware Impact: 0 us/frame; source content only.

Problem: Replay/dossier persistence was knowledge-not-power, but UI/save/site presentation still needed concrete rules. Without them, future work could drift into power roguelite persistence or unspoiled public pages exposing final payload truth.
Solution: Add RS036 packets P176-P180. Lock dossier selection UI, risk-weight contract cards, ending record fields, save-profile knowledge flags and website/wiki spoiler tiers.
Rejected Alternatives: Permanent oxygen/hull/gear upgrades; hidden ending metadata with no player-facing shape; public wiki exposing Atlas-basin consequences without spoiler gates.
Scalability potential: Low tier can use plain dossier cards. High/Ultra can add animated claim boards, packet replay, ending timeline UI and spoiler-gated web visuals without changing save truth.
Hardware Impact: 0 us/frame; source content only.

Problem: RS033-RS036 needed proof of propagation without claiming runtime bake or scene placement.
Solution: Run importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=180`, `locales=15`, `rows=2700`, `graph_rows=180`, `route_cards=174`, `route_source_rows=174`, `wiki_pages=2700`, `site_pages=2700`, `manual_policy_rows=94`, `manual_discovery_policy_rows=67`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during content-only lore work; expanding TerminalOS while another Unity agent may be active; leaving temporary generator script in repo; claiming source rows are baked runtime data.
Scalability potential: Source rows and route cards are ready for the next bake. Runtime remains baked packet/string-pool lookup only.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Aegir moon names, orbital hazards and public article boundaries were still loose enough for future writing to treat moons as decorative astronomy or freeze bad placeholder names as canon.
Solution: Add RS037 packets P181-P185. Lock moon names as publication labels, moon route functions as canon, HECTON-8 extraction hazards as eclipse/radiation/relay/ice/storm/guidance windows, ephemeris numbers as future table-owned data, and public moon articles as spoiler-gated.
Rejected Alternatives: Exact orbital periods in prose; decorative moon list; hiding Atlas-basin consequences in public wiki pages; renaming moons in a way that breaks route roles.
Scalability potential: Low tier can show static route plates and hazard text. Middle/High/Ultra can add sky-chart UI, window animation, carrier overlays and spoiler-gated public pages without changing packet truth.
Hardware Impact: 0 us/frame; source content only.

Problem: "Who inside Deep Reach knew?" remained a real narrative hole. A single mastermind would be crude, but leaving it open weakens evidence play.
Solution: Add RS038 packets P186-P190. Lock true-cause knowledge tiers across field staff, risk office, Atlas office, evacuation counsel, Keelmark and Recovery Compliance. Define memo fragments, signoff/witness conflict, seeded suboffice personnel and the false public report packet.
Rejected Alternatives: Cartoon villain confession; everyone knew everything; faceless corporate fog; public report as pure fake rather than real physics with culpable weighting removed.
Scalability potential: Weak devices can deliver stamps, memos and room evidence. High/Ultra can add dossier boards, redaction animation, voice fragments and packet provenance around the same static hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: The final question was still a candidate list. That blocks endings, public/wiki spoiler policy and final-room object writing.
Solution: Add RS039 packets P191-P195. Lock the final emotional axis: crime-scene sale, broken guardian preservation, public truth without control, Atlas severance as mercy/murder/liberation/theft, and no-clean-best-ending rule.
Rejected Alternatives: Morally clean best ending; binary kill/spare Atlas; material ending as joke fail state; public truth as automatically pure justice.
Scalability potential: Low tier can present payload cards and ending records. High/Ultra can layer room state, ecology response, payload UI, carrier/legal audio and ending-specific visual residue without changing the route records.
Hardware Impact: 0 us/frame; source content only.

Problem: Resource numbers, recipe costs, stack sizes, risk rewards and localization gates were open. Freezing them in prose would create bad balance and runtime localization drift.
Solution: Add RS040 packets P196-P200. Lock table ownership for resource yields, escape recipe balance bands, risk/reward bands, inventory stack policy and native localization pass requirements. Lore owns category and meaning; DataMonolith tables own numbers.
Rejected Alternatives: Fake exact numbers in lore; arbitrary inventory stack sizes; binary difficulty switches; runtime translation or runtime markdown/json parsing.
Scalability potential: Low tier can use simple tables and static strings. Middle/High/Ultra can scale UI richness, risk presentation and localization polish while preserving baked packet/string-pool truth.
Hardware Impact: 0 us/frame; no runtime code change.

Problem: First RS037 audit failed on stale memory IDs (`P078_AEGIR_MOON_LADDER`, `P080_RELAY_GEOMETRY_HAZARDS`, `P079_HECTON8_ORBITAL_POSITION`, `P120_ATLAS_ALTERED_BIOMECH_RESOURCE`).
Solution: Verify real packet IDs from current packet JSON and replace with `P077_AEGIR_MOON_LADDER`, `P078_INNER_MOON_RELAY_HAZARDS`, `P079_HECTON8_ORBIT_TIDE_GEOMETRY`, and `P120_BIOMETAL_SENSOR_TAGS`.
Rejected Alternatives: Creating duplicate packets to satisfy wrong names; hand-waving graph errors; leaving route-card exporter failure unresolved.
Scalability potential: Stable graph references keep unlock/evidence truth deterministic across all presentation tiers.
Hardware Impact: 0 us/frame; prevents source-route drift before bake.

Problem: RS037-RS040 needed proof of propagation without claiming runtime bake or scene placement.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=200`, `locales=15`, `rows=3000`, `graph_rows=200`, `route_cards=194`, `route_source_rows=194`, `wiki_pages=3000`, `site_pages=3000`, `manual_policy_rows=114`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=87`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during a lore pass; expanding TerminalOS while another Unity agent may be active; claiming source rows are runtime-ready baked data.
Scalability potential: Source rows and route cards are ready for the next bake. Runtime remains baked packet/string-pool lookup only.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Lower Deep Reach personnel were still open. Adding names carelessly would create new villains and dilute the senior liability chain.
Solution: Add RS041 with lower office signatures as procedure stamps: Contract Continuity Desk, Packet Notary Interface, Quarantine Review Gate, Asset Silence Board and Return Action Queue. Each seed name owns a narrow stamp/action, not strategic guilt.
Rejected Alternatives: More masterminds; anonymous office fog; villain monologues; making Deep Reach either fully innocent or cartoonishly intentional.
Scalability potential: Low tier can show stamps and memo lines. High/Ultra can add dossier boards, packet provenance, redaction animation and voice-filter fragments around the same static packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Colony humanity had a protocol but no exact authoring scale. Without a limit, future content could become anonymous ruins or uncontrolled random names.
Solution: Add RS042 and lock 72 worker identities: 24 anchor names plus 48 seed-role identities. Names attach to crew family, route permission, last task and prop variant.
Rejected Alternatives: Family revenge hook; unlimited random names; memorial wall only; translating names into new identities per locale.
Scalability potential: Low tier can display labels, ledgers and locker strips. Middle/High/Ultra can add handwriting, audio, prop wear and seed-varying object combinations without changing story truth.
Hardware Impact: 0 us/frame; source content only.

Problem: Worker evidence prop rules were still broad enough for decorative clutter or lore-dump props.
Solution: Add RS043 with prop evidence kit: locker matrix, triage ledger variants, route permission stamps, Marauder correction marks and audio fragment rules.
Rejected Alternatives: Empty graffiti, generic keepsakes, gore spectacle, free-floating voice logs, prop art with no route/system meaning.
Scalability potential: Weak devices can use static card/scan props. High/Ultra can spend budget on wet glass, wear, parallax, audio damage and subtle animated instrument response while packet truth stays fixed.
Hardware Impact: 0 us/frame; source content only.

Problem: External site, in-game wiki, audio transcript and image publication policy were still split across taste prose and packet notes.
Solution: Add RS044 to lock public article tiers, PDA unlock tiers, audio transcript redaction, art brief release gates and native-language backlog rules.
Rejected Alternatives: Public pages spoiling Atlas-basin payload consequences; unreadable censor style; claiming draft multilingual rows are release localization; decorative generic sci-fi images.
Scalability potential: Same packet IDs support plain low-tier pages and richer high-tier cards/art/audio. Runtime remains baked string-pool lookup, not live translation or markdown parsing.
Hardware Impact: 0 us/frame; source content only.

Problem: First RS041-RS044 route export failed on stale packet IDs from memory.
Solution: Replace `P046_MARAUDER_GRAFFITI_MASKS` with real `P053_MARAUDER_GRAFFITI_MASKS`, and `P176_DOSSIER_SELECTION_UI_RULES` with real `P176_DOSSIER_SELECTION_UI_RULE`.
Rejected Alternatives: Adding duplicate packets to satisfy bad references; hand-waving route-card exporter failure; leaving graph drift.
Scalability potential: Stable dependency graph keeps unlock/evidence route deterministic across all presentation tiers.
Hardware Impact: 0 us/frame; prevents source-route drift before bake.

Problem: RS041-RS044 needed propagation proof without runtime bake or scene placement claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=220`, `locales=15`, `rows=3300`, `graph_rows=220`, `route_cards=214`, `route_source_rows=214`, `wiki_pages=3300`, `site_pages=3300`, `manual_policy_rows=134`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=107`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during content work; expanding TerminalOS while another Unity agent may be active; claiming source rows are baked runtime data.
Scalability potential: Source rows and route cards are ready for next bake. Runtime remains baked packet/string-pool lookup only.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Native HECTON-8 ecology was still vulnerable to becoming a decorative bestiary instead of exploration, hazard and wonder evidence.
Solution: Add RS045 and RS046. Shallow ecology now has photic mats, glass grazers, lantern drifts, shell clamp reefs and predator shadow telegraphs. Brine/abyss ecology now has brine vane forests, density skaters, vent anchor colonies, wide filter-body traces and silt ambusher telegraphs.
Rejected Alternatives: Aquarium entries; combat-only monster catalog; horror-only dark ocean; exact spawn numbers in lore prose.
Scalability potential: Low tier can present silhouettes, static scan cards and cheap particle/audio hints. Middle/High/Ultra can add swarm density, translucency, caustic response, shader variation and rare-behavior spectacle while preserving packet IDs and route truth.
Hardware Impact: 0 us/frame; source content only.

Problem: "Atlas uses the ocean as a repair network" was still abstract. Without concrete mechanisms it reads like mysticism.
Solution: Add RS047. Atlas repair is expressed through conductive biofilm cable skin, acoustic filter-organ relays, shell sealant fracture growth, sensor-tagged fauna and vent micronode nests. The ocean does not speak or reason as a human; Atlas routes industrial repair impulses through biological growth, chemistry, pressure and animal movement.
Rejected Alternatives: Fully sentient ocean; evil AI drone army; pure background biology; clean industrial repair bot network divorced from native life.
Scalability potential: Low tier can use static mesh seams, scan text and audio pings. Middle/High/Ultra can add growth masks, procedural wetness, living cable animation, relay pulses and fauna-carried signal trails without changing gameplay truth ownership.
Hardware Impact: 0 us/frame; source content only.

Problem: The opening escape/hardware chain still needed concrete object classes that explain why the player cannot simply leave and why exploration pushes inward.
Solution: Add RS048. The Black Keel tender is a remote salvage carrier, not a luxury personal ship. The drop capsule has damaged ascent and comms hardware. The P-63 fabricator cannot make sealed flight hardware without authority keys, pressure-safe feedstock and alignment data. Pressure suit grades and sonar pinger route beacons define survival/exploration gates.
Rejected Alternatives: Hand-waved crash lock; arbitrary "find three parts" quest; Deep Reach rescue as simple phone call; instant fabrication of orbital escape gear.
Scalability potential: Low tier can present repair cards, broken modules and beacon breadcrumbs. Middle/High/Ultra can add detailed cockpit damage, diagnostic UI, fabrication animation, orbital-window overlays and pressure-suit visual grades around the same packet graph.
Hardware Impact: 0 us/frame; source content only.

Problem: First RS045-RS048 verification caught source drift: phantom IDs, unsupported route-card ending pressures and pseudo-prefab candidates.
Solution: Correct prereqs to real packet IDs, restrict new route-card ending pressures to supported schema values, and move `poi.*` markers out of prefab candidate columns while using real prefab candidate paths for auditable binding targets.
Rejected Alternatives: Adding duplicate content to satisfy wrong names; weakening the audit; claiming pseudo paths are real Unity candidates.
Scalability potential: Stable source graph keeps runtime bake deterministic and avoids authoring drift across in-game wiki, site pages and scene placement backlog.
Hardware Impact: 0 us/frame; prevents invalid source from reaching bake.

Problem: RS045-RS048 needed propagation proof without pretending source content is already baked runtime data.
Solution: Run importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=240`, `locales=15`, `rows=3600`, `graph_rows=240`, `route_cards=234`, `route_source_rows=234`, `wiki_pages=3600`, `site_pages=3600`, `manual_policy_rows=154`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=127`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; touching scene YAML while another Unity agent may be active; claiming native localization quality from draft multilingual rows; claiming `static_data.h8bin` was rebuilt.
Scalability potential: Source packets, route cards, pages and binding targets are ready for next controlled bake/placement pass. Runtime target remains baked string-pool/blob lookup, not live parsing or translation.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Replay pressure still risked being described as "more random things" instead of usable contract surfaces.
Solution: Add RS049 packets P241-P245 for lien severity, storm windows, sample custody, evidence order and Deep Reach clause weight. These are seed-visible cards that change route pressure, payout urgency, rescue tolerance and discovery order without inherited power.
Rejected Alternatives: Roguelite power carryover; vague replay randomness; changing protagonist identity per seed; tuning exact numeric rewards in prose.
Scalability potential: Low tier can show static contract cards. Middle/High/Ultra can add animated claim boards, storm overlays, custody case state and dossier route previews without changing packet truth.
Hardware Impact: 0 us/frame; source content only.

Problem: The first hour had a spine but still needed micro-script surfaces usable by terminal, scanner, audio and PDA instead of abstract scene notes.
Solution: Add RS050 packets P246-P250. Lock Black Keel approach audio, drop capsule diagnostic readout, P-63 first repair task, sanitized accident packet body and first Atlas repair trace scene.
Rejected Alternatives: Opening lore dump; crash cinematic with no readable systems; friendly helper voice; Atlas monster reveal in the first hour.
Scalability potential: Weak devices can use text/audio/prop cards. High/Ultra can spend budget on cockpit diagnostics, wet module lighting, local VFX and audio filtering around the same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Website/public wiki content needed pillar articles, not only internal lore packets.
Solution: Add RS051 packets P251-P255 for spoiler-tiered HECTON-8, Aegir, Deep Reach, Atlas-6 and blue debt public articles.
Rejected Alternatives: Public pages spoiling final payload consequences; marketing copy detached from packet IDs; internal implementation notes masquerading as public lore.
Scalability potential: Same packet IDs support simple text pages on low-end UI and richer public cards, images and spoiler gates on high-end presentation.
Hardware Impact: 0 us/frame; source content only.

Problem: Multilingual/audio expansion needed hard style locks before native review; otherwise proper nouns, unit labels and terminal voice drift by language.
Solution: Add RS052 packets P256-P260 for proper noun locks, unit/number style, terminal register, audio bark families and RTL/CJK/font risk notes.
Rejected Alternatives: Runtime translation; translating names into new identities; terminal prose as narrator; constant companion chatter; claiming draft multilingual rows are release localization.
Scalability potential: Stable packet IDs and string-pool rows let languages change without save drift. UI richness scales separately through font/layout proof.
Hardware Impact: 0 us/frame; no runtime localization change.

Problem: First RS049-RS052 route export and audit found stale prereq IDs from memory.
Solution: Replace phantom references with real packet IDs: `P021_BLACK_KEEL_CUSTODY`, `P097_RECOVERY_COMPLIANCE_OFFICE`, `P203_QUARANTINE_REVIEW_GATE_SIGNATURES`, `P091_COLLISION_FRACTURED_MOON`, `P146_DEEP_REACH_PUBLIC_COMBINE`, `P147_AEGIR_CONTINUITY_HOLDINGS`, `P171_RECIPE_TIER_PRESSURE_BANDS`, `P216_PUBLIC_SITE_ARTICLE_TIER_RULES`, `P217_IN_GAME_WIKI_UNLOCK_TIER_RULES`, `P218_AUDIO_TRANSCRIPT_CENSOR_RULES`, `P190_FALSE_PUBLIC_REPORT_PACKET`, and `P036_RETURN_VECTOR_WINDOW`.
Rejected Alternatives: Creating duplicates for wrong names; hand-waving route-card exporter errors; loosening the audit.
Scalability potential: Stable dependency graph keeps route unlocks deterministic and safe for DataMonolith bake.
Hardware Impact: 0 us/frame; prevents source-route drift before bake.

Problem: RS049-RS052 needed proof across all export surfaces without runtime claims.
Solution: Run importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=260`, `locales=15`, `rows=3900`, `graph_rows=260`, `route_cards=254`, `route_source_rows=254`, `wiki_pages=3900`, `site_pages=3900`, `manual_policy_rows=174`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=147`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; expanding TerminalOS during a parallel Unity pass; claiming source packets solve numeric tables, final UI or native localization proof.
Scalability potential: Source packets, route cards, pages and binding targets are ready for next controlled bake/placement pass. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Numeric gameplay gaps were still open but could not be honestly solved by narrative prose.
Solution: Add RS053 packets P261-P265 as table-bridge surfaces for resource yields, inventory stacks, escape recipes, contract risk/reward and ending payouts. These packets define row semantics, evidence meaning and UI/site/wiki text while leaving final values to DataMonolith gameplay tables.
Rejected Alternatives: Hardcoding balance numbers in lore packets; claiming exact economy is solved; adding a runtime parser or live tuning bridge.
Scalability potential: Low tier can show simple rows and warnings. Middle/High/Ultra can add richer tables, overlays and animated contract/payout cards without changing packet IDs or gameplay authority.
Hardware Impact: 0 us/frame; source content only.

Problem: Dossier and contract UI copy was not yet packetized enough for PDA/scanner/terminal/wiki/site reuse.
Solution: Add RS054 packets P266-P270 for start screen copy, contract field labels, rumor-family copy, route warnings and ending-record UI copy.
Rejected Alternatives: Writing UI strings as disconnected mockup text; hiding route warnings in code; letting rumor copy spoil coordinates.
Scalability potential: Stable packet IDs let low-end UI use static labels and high-end UI use richer cards/animation/audio while sharing baked strings.
Hardware Impact: 0 us/frame; source content only.

Problem: Ending outcomes needed concrete records, not only thematic ending names.
Solution: Add RS055 packets P271-P275 for material payout, partial return, public ledger, Atlas severance and preserve/quarantine records. Each record carries payload route, receiver, evidence state, ecological consequence and unresolved pressure.
Rejected Alternatives: Fake fail screens; vague moral labels; clean "best ending"; endings without replay/dossier consequences.
Scalability potential: Same record packets can drive plain low-tier ending summaries and high-tier dossier boards, site pages, voice captions and spoiler-gated wiki articles.
Hardware Impact: 0 us/frame; source content only.

Problem: Multilingual release risk was still too vague after glossary/style locks.
Solution: Add RS056 packets P276-P280 for RU native review, CJK review, RTL review, European expansion review and subtitle/audio review. These are production gates, not claims that draft rows are final translations.
Rejected Alternatives: Runtime translation; claiming draft multilingual coverage is release localization; ignoring RTL/CJK/font/subtitle timing proof.
Scalability potential: Stable string-pool rows and packet IDs allow richer per-language UI later without save or route drift.
Hardware Impact: 0 us/frame; source content only.

Problem: First RS053-RS056 route-card verification exposed stale packet names from memory.
Solution: Replace phantom prereqs with real packet IDs: `P099_MARAUDER_DOSSIER_PERSISTENCE`, `P037_COWARD_EXIT_CHAIN`, `P132_PARTIAL_EXIT_SAME_SEED_RETURN`, and `P135_PUBLIC_LEDGER_LEAK_ROUTE`.
Rejected Alternatives: Creating duplicate packets for bad names; weakening route-card validation; allowing graph drift into source data.
Scalability potential: Valid route graphs keep DataMonolith route bake deterministic across devices and presentation tiers.
Hardware Impact: 0 us/frame; prevents invalid source from reaching bake.

Problem: RS053-RS056 needed proof across all export surfaces without runtime or Unity claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=280`, `locales=15`, `rows=4200`, `graph_rows=280`, `route_cards=274`, `route_source_rows=274`, `wiki_pages=4200`, `site_pages=4200`, `manual_policy_rows=194`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=167`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; expanding TerminalOS slots during a parallel Unity pass; claiming exact numeric tables, final UI implementation or native review are done.
Scalability potential: Source packets, route cards, pages and binding targets are ready for next controlled bake/placement/UI pass. Runtime target remains baked string-pool/blob lookup, not live parsing.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Public/site content still risked being separate marketing copy detached from packet IDs and spoiler gates.
Solution: Add RS057 packets P281-P285 for HECTON-8 opening hook, Aegir map copy, Deep Reach accountability copy, Atlas-6 spoiler gate copy and blue debt resource copy.
Rejected Alternatives: Separate website prose without in-game packet source; public Atlas spoilers; Deep Reach as cartoon villain; blue debt as magic ore.
Scalability potential: Low tier can use static site/wiki pages. Middle/High/Ultra can add image cards, animated map captions and spoiler gates while retaining the same packet IDs.
Hardware Impact: 0 us/frame; source content only.

Problem: In-game notes/audio needed concrete artifacts, not generic "add logs" guidance.
Solution: Add RS058 packets P286-P290 for capsule blackbox audio, P-63 work order, worker locker nameplate, Marauder correction note and quarantine relay fragment.
Rejected Alternatives: Tutorial prose, companion narration, family melodrama, omniscient logs or clean early extraction messaging.
Scalability potential: Weak devices can show text/audio captions. High/Ultra can add diegetic prop VFX, localized subtitles and richer terminal presentation without changing lore truth.
Hardware Impact: 0 us/frame; source content only.

Problem: Ecology still needed release-ready specimen cards that teach route logic without becoming a decorative bestiary.
Solution: Add RS059 packets P291-P295 for photic mat, glass grazer, lantern drift, brine vane and sensor-tagged fauna codex cards.
Rejected Alternatives: Aquarium entries, combat-only monster list, mystical ocean mind or vague "life is weird" prose.
Scalability potential: Low tier can use scan cards and silhouettes. Middle/High/Ultra can add behavior animation, shader response and audio cues while retaining same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Final descent needed concrete route/UI fragments that preserve hard-sci-fi pressure and no-clean-ending tone.
Solution: Add RS060 packets P296-P300 for abyssal machine-field warning, Atlas basin pressure gate, factory-temple entry fragment, payload authority last check and no-clean-ending dossier note.
Rejected Alternatives: Boss buildup, fantasy portal, clean best ending, or ending copy that ignores receiver/custody/ecology consequences.
Scalability potential: Same packets can feed low-tier terminal/scanner text and high-end final UI, audio, visual threshold and dossier presentation.
Hardware Impact: 0 us/frame; source content only.

Problem: First RS057-RS060 route-card verification found stale prereq naming.
Solution: Replace `P083_BRINE_CANYON_LADDER` with existing `P083_BRINE_CANYON_ROUTE_LADDER`; confirm `P100_FINAL_CHOICE_PAYLOAD` exists before rerunning export.
Rejected Alternatives: Duplicate packet creation; weakening route-card exporter; hand-editing only the output CSV instead of fixing the generator source.
Scalability potential: Stable route graph keeps unlocks and publication pages deterministic through DataMonolith bake.
Hardware Impact: 0 us/frame; prevents source-route drift before bake.

Problem: RS057-RS060 needed proof across all export surfaces without runtime claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=300`, `locales=15`, `rows=4500`, `graph_rows=300`, `route_cards=294`, `route_source_rows=294`, `wiki_pages=4500`, `site_pages=4500`, `manual_policy_rows=214`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=187`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; expanding TerminalOS slots during a parallel Unity pass; claiming final web composition, image production or UI proof are done.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/placement/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Numeric gameplay values were still unresolved, but writing numbers into lore would corrupt table ownership.
Solution: Add RS061 packets P301-P305 as table handoff contracts. Each packet defines the acceptance shape for resource yield, stack limit, escape recipe cost, contract risk/reward and ending payout rows while leaving exact values to DataMonolith gameplay tables.
Rejected Alternatives: Hardcoding yields, stack sizes, recipe counts, risk weights or payout values in prose; creating a runtime parser bridge; claiming RS061 solves balance.
Scalability potential: Low tier can show static table warnings and short UI labels. Middle/High/Ultra can add richer editor previews, table cards and dossier overlays without changing packet IDs or gameplay authority.
Hardware Impact: 0 us/frame; source content only.

Problem: PDA/scanner/terminal/dossier UI requirements were packetized but not proof-shaped enough for future implementation review.
Solution: Add RS062 packets P306-P310 for PDA codex state proof, scanner stage binding proof, terminal slot proof, dossier ending record proof and localized-overflow proof.
Rejected Alternatives: Claiming Unity UI implementation from source text; allowing runtime markdown/json parsing; treating draft multilingual rows as final localization.
Scalability potential: Weak devices can use static baked labels and short warnings. High/Ultra can add animated panels, richer scan progress and subtitle treatment while preserving baked string-pool keys.
Hardware Impact: 0 us/frame; no runtime UI code touched.

Problem: Public pages could still be assembled as generic marketing despite packet-owned article text.
Solution: Add RS063 packets P311-P315 for public home composition, Aegir system art composition, Deep Reach evidence composition, Atlas spoiler composition and social/dev-note copy boundaries.
Rejected Alternatives: Unsupported demo/platform/release claims; fantasy astronomy; cartoon Deep Reach villain copy; public Atlas payload spoilers; hype-calendar writing.
Scalability potential: Low tier/public wiki can use static pages. High/Ultra presentation can add richer image cards, spoiler toggles and animated maps around the same packet truth.
Hardware Impact: 0 us/frame; source/publication content only.

Problem: The `NarrativeDiscovery` backlog was numerically visible but not prioritized for the next Unity placement pass.
Solution: Add RS064 packets P316-P320 for first-hour placement priority, mid-depth route placement priority, ecology scan placement priority, final descent placement priority and terminal backlog triage.
Rejected Alternatives: Raw-editing Unity YAML; expanding TerminalOS slots during a parallel Unity pass; promoting terminal rows that do not change decision/proof/ending context.
Scalability potential: Placement priority lets low-tier builds focus on core evidence anchors first while high-tier scenes can add denser optional prop/readable layers.
Hardware Impact: 0 us/frame; source placement backlog only.

Problem: RS061-RS064 needed proof across all export surfaces without runtime claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=320`, `locales=15`, `rows=4800`, `graph_rows=320`, `route_cards=314`, `route_source_rows=314`, `wiki_pages=4800`, `site_pages=4800`, `manual_policy_rows=234`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=207`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; claiming UI/device/native localization/publication proof from source packets.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/placement/UI/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Carrier ownership and player start pressure still risked being summarized as "the player's ship" or "Deep Reach owns everything" instead of playable contract evidence.
Solution: Add RS065 packets P321-P325 for Black Keel claim-pool charter, masked Deep Reach beneficiary clause, orbital recovery window protocol, salvage carrier autonomy limits and player lien start card.
Rejected Alternatives: Personal luxury ship, loyal rescue AI, invisible omnipotent Deep Reach ownership, or family-revenge motive.
Scalability potential: Low tier can show static contract/status cards. Middle/High/Ultra can add animated carrier status, orbital-window panels and contract overlays using the same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Present-tense Deep Reach contact needed concrete communication grammar, not villain monologues or constant radio exposition.
Solution: Add RS066 packets P326-P330 for sanitized accident reply, automated legal/insurance ping, coordinate demand, faction-message split and blackout signal decay.
Rejected Alternatives: Companion-style exposition, live omniscient dispatcher, flat villain broadcast or arbitrary comm silence.
Scalability potential: Weak devices can use terse terminal/audio rows; high-end builds can add signal decay UI, waveform treatment and rare voiced windows without changing route truth.
Hardware Impact: 0 us/frame; source content only.

Problem: Atlas repair-network mechanisms needed deeper specificity beyond the existing ecology labels.
Solution: Add RS067 packets P331-P335 for conductive biofilm bus, acoustic filter relay, shell sealant pressure growth, sensor-tagged fauna feedback and vent micronode power nest.
Rejected Alternatives: Talking ocean, magic corruption, boss-monster framing or decorative bestiary.
Scalability potential: Low tier can show scan cards and static props. Middle/High/Ultra can spend saved cycles on animated biofilm pulses, acoustic overlays and vent-node VFX while preserving packet truth.
Hardware Impact: 0 us/frame; source content only.

Problem: False exits were valid but needed after-action records that make replay consequences readable.
Solution: Add RS068 packets P336-P340 for material receipt audit, partial return lien extension, quarantine hold interrogation, corporate coordinate capture and public ledger aftershock.
Rejected Alternatives: Fake fail screens, clean win/lose labels, inherited gear progression or endings without receiver/custody/evidence consequence.
Scalability potential: Same records feed low-tier dossier rows and high-tier ending boards, voice captions, spoiler-gated site pages and replay contract screens.
Hardware Impact: 0 us/frame; source content only.

Problem: Initial RS065-RS068 verification exposed stale packet references from memory.
Solution: Replace stale prereqs with real packet IDs: `P021_BLACK_KEEL_CUSTODY`, `P016_AEGIR_HOST_STAR`, `P201_CONTRACT_CONTINUITY_DESK_SIGNATURES`, `P231_CONDUCTIVE_BIOFILM_CABLE_SKIN`, `P232_ACOUSTIC_FILTER_ORGAN_RELAY`, `P233_SHELL_SEALANT_FRACTURE_GROWTH`, `P234_SENSOR_TAGGED_FAUNA`, and `P235_VENT_MICRONODE_NESTS`.
Rejected Alternatives: Creating duplicate packets to satisfy wrong names; weakening route-card validation; hand-editing only exported source data.
Scalability potential: Valid route graph keeps importer/exporter/bake identity stable across device tiers and publication surfaces.
Hardware Impact: 0 us/frame; prevents source-route drift before bake.

Problem: RS065-RS068 needed proof across all export surfaces without runtime claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=340`, `locales=15`, `rows=5100`, `graph_rows=340`, `route_cards=334`, `route_source_rows=334`, `wiki_pages=5100`, `site_pages=5100`, `manual_policy_rows=254`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=227`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; claiming carrier UI, comms UI, repair-network implementation, ending screens or native localization are complete from source packets.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/placement/UI/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Ship and transit lore could still collapse into vague "future spaceship" language or imply FTL.
Solution: Add RS069 packets P341-P345 for needleprobe survey, beam-sail/pellet-lane transit, seed-ship braking, Black Keel carrier tug stack and bathydrop-interface failure. Transit is infrastructure-heavy no-FTL logistics.
Rejected Alternatives: FTL, private player yacht, loyal rescue ship, hand-waved crash blocker or route lore detached from carrier/escape gameplay.
Scalability potential: Low tier can show static encyclopedia/contract cards. Middle/High/Ultra can add orbital UI, carrier status animation and route diagrams around the same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Aegir needed to feel like a real multi-moon gas-giant system without moving the campaign away from HECTON-8.
Solution: Add RS070 packets P346-P350 for warm dwarf light/radiation, inner relay moon, ice-scatter moon, HECTON-8 mid-orbit tide role and outer dead beacon moon. Other moons become traffic/hazard/relay context, not extra playable scope.
Rejected Alternatives: Brown dwarf darkness, fantasy astronomy, dozens of named moons, or exact ephemeris claims without owned celestial tables.
Scalability potential: Low tier can use static map labels and skybox constraints. Middle/High/Ultra can add animated orbital maps, radiation-window UI and richer system art while preserving the same role grammar.
Hardware Impact: 0 us/frame; source atlas content only.

Problem: HECTON-8 geology and resources needed fieldguide utility, not only atmospheric prose.
Solution: Add RS071 packets P351-P355 for drowned crust strata, brine density ladders, vent forge process, blue debt pressure history and pressure glass/sealant repair routes. Blue debt stays salvage slang for Xenon-Omega-bearing pressure substrate.
Rejected Alternatives: Magic ore, generic loot nodes, purely decorative trenches, talking-ocean explanation or prose-owned numeric yield tuning.
Scalability potential: Low tier can show scan cards and static POI labels. Middle/High/Ultra can add denser scan overlays, geology art, resource VFX and route callouts without changing resource authority.
Hardware Impact: 0 us/frame; source fieldguide content only.

Problem: Colony humanity still needed more daily-life evidence while preserving the user's rejection of family-revenge motivation.
Solution: Add RS072 packets P356-P360 for pressure bunk routines, canteen water ledger, tool certification rituals, worker-community no-family-hook rule and last-normal-day evidence set.
Rejected Alternatives: Parent/spouse/child rescue hook, tourist protagonist, companion melodrama, heroic martyr roster or abstract "colonists had lives" notes.
Scalability potential: Low tier can place static prop/readable cards. Middle/High/Ultra can add richer prop art, audio fragments, localized captions and seed-varied POI arrangements around stable packet IDs.
Hardware Impact: 0 us/frame; source colony evidence only.

Problem: RS069-RS072 needed proof across all export surfaces without runtime claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=360`, `locales=15`, `rows=5400`, `graph_rows=360`, `route_cards=354`, `route_source_rows=354`, `wiki_pages=5400`, `site_pages=5400`, `manual_policy_rows=274`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=247`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; claiming orbital simulation, Unity placement, runtime UI, native localization or final public art are complete from source packets.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/placement/UI/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: The escape chain was directionally locked but still needed concrete component surfaces that explain why the player must go deeper instead of simply leaving.
Solution: Add RS073 packets P361-P365 for acoustic relay spine, pressure seal clamp ring, guidance timing core, ascent energy charge and quarantine/legal handshake assembly. Each packet has route evidence, scanner/terminal/wiki/site text and binding backlog.
Rejected Alternatives: Menu-based rescue, single generic repair kit, personal ship callback, pure recipe prose without physical blockers, or choosing final resource counts in lore.
Scalability potential: Low tier can expose static scan/terminal cards. Middle/High/Ultra can add richer component meshes, pinger UI, orbital timing animation and exit-panel state while keeping the same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: The protagonist lock needed more usable in-game evidence than "former Deep Reach, now Marauder".
Solution: Add RS074 packets P366-P370 for field-systems record, revoked access language, old procedure recognition, debt/blacklist pressure and professional-guilt escalation. Motive remains professional interest becoming personal responsibility, not family revenge.
Rejected Alternatives: lost relative hook, tourist protagonist, command-level mastermind protagonist, or inherited gear-power roguelite framing.
Scalability potential: Low tier can use dossier cards and lockout text. Middle/High/Ultra can add access-denial UI states, old-signature overlays, voice fragments and contract-board variants without changing protagonist identity.
Hardware Impact: 0 us/frame; source content only.

Problem: Deep Reach guilt risked becoming either too evil or too vague after the user's correction that physics really caused the flood.
Solution: Add RS075 packets P371-P375 for Great Tide sensor-margin proof, evacuation queue delay proof, Atlas weighting audit proof, quarantine release delay proof and claim continuity loss conversion proof.
Rejected Alternatives: making Deep Reach solely responsible for the flood physics, making them innocent because the flood was real, or reducing proof to villain monologues.
Scalability potential: Low tier can show static evidence cards. Middle/High/Ultra can add matching-room proof overlays, clock/queue animations, Atlas audit visualization and richer public-ledger boards.
Hardware Impact: 0 us/frame; source content only.

Problem: Final payload choices needed receiver protocols, not only abstract endings.
Solution: Add RS076 packets P376-P380 for coordinate sale, Atlas severance, preserve/quarantine, public ledger and payload withholding. Each choice names receiver, custody meaning, evidence state and unresolved cost.
Rejected Alternatives: clean good/bad ending labels, fake fail screens, one canonical rescue, or final choices with no receiver/custody consequence.
Scalability potential: Low tier can render static ending records. Middle/High/Ultra can add final receiver UI, spoiler-gated site pages, voice captions and dossier after-action boards around the same route cards.
Hardware Impact: 0 us/frame; source content only.

Problem: RS073-RS076 verification exposed one stale route dependency during route export.
Solution: Replace stale `P070_RETURN_VECTOR_WINDOW` with real `P036_RETURN_VECTOR_WINDOW`, regenerate source, rerun importer and route exporter. Final route-card export count became `374`.
Rejected Alternatives: weakening route validation, creating duplicate packets to satisfy a bad ID, or hand-editing only generated route CSV.
Scalability potential: Valid graph references keep future bake/runtime lookup stable across device tiers and publication surfaces.
Hardware Impact: 0 us/frame; source-route correction only.

Problem: RS073-RS076 needed proof across all export surfaces without runtime claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=380`, `locales=15`, `rows=5700`, `graph_rows=380`, `route_cards=374`, `route_source_rows=374`, `wiki_pages=5700`, `site_pages=5700`, `manual_policy_rows=294`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=267`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; claiming ending UI, scene placement, native localization, audio, cinematics or public site assembly are complete from source packets.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/placement/UI/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: The lore base had many facts but still lacked a clean long-campaign act spine that future mission/placement passes can use without inventing pacing from scratch.
Solution: Add RS077 packets P381-P385 for contract approach, photic shelf survival, brine canyon liability, abyssal machine-field repair and Atlas basin payload authority. These are act gates, not rails.
Rejected Alternatives: Writing another internal outline; creating linear mission scripts; claiming runtime mission implementation from prose.
Scalability potential: Low tier can expose static codex/contract/scan cards. Middle/High/Ultra can add richer act transitions, route UI and environmental set pieces from the same packet hashes.
Hardware Impact: 0 us/frame; source content only.

Problem: Major locations were still too easy to describe as vibes instead of physical evidence kits.
Solution: Add RS078 packets P386-P390 for Shallow Annex P-63, cable reef relay yard, brine canyon pump cathedral, evacuation queue terminal and Atlas service basin. Each kit binds route function, evidence, object set and player pressure.
Rejected Alternatives: Generic POI names; pure lore terminals; mystical factory-temple imagery without machine function.
Scalability potential: Low tier can place one readable anchor per kit. Middle/High/Ultra can add dense prop layers, scan stages, audio captions and bespoke art without changing truth ownership.
Hardware Impact: 0 us/frame; placement remains a later Unity API pass.

Problem: Replayability still needed contract families that are more concrete than "seed varies" but do not create power-progression roguelite rules.
Solution: Add RS079 packets P391-P395 for quiet salvage, storm-window rush, high-custody sample, evidence-first charter and Recovery Compliance bait. These vary custody, timing, route pressure, evidence order and receiver pressure only.
Rejected Alternatives: inherited gear, stat meta-progression, alternate protagonists or random truth changes.
Scalability potential: Low tier can show static contract cards. Middle/High/Ultra can add contract-board animation, route warnings and dossier overlays while preserving same seed semantics.
Hardware Impact: 0 us/frame; no gameplay tables changed.

Problem: Public/wiki material needed spoiler-safe article modules that explain the world without leaking final payload outcomes or making unsupported feature claims.
Solution: Add RS080 packets P396-P400 for starting premise, no-FTL route, Aegir moon map, Deep Reach liability and Atlas spoiler gate modules.
Rejected Alternatives: marketing fluff; final-spoiler public pages; dense internal lore dump; claiming release/platform/demo facts.
Scalability potential: Public static pages can use these modules now; higher presentation tiers can add imagery, spoiler modals and animated maps around the same content IDs.
Hardware Impact: 0 us/frame; publication source only.

Problem: RS077-RS080 route export found stale prereq names.
Solution: Replace `P336_MATERIAL_RECEIPT_AUDIT_RECORD` with `P336_MATERIAL_EXIT_RECEIPT_AUDIT`, and `P342_BEAM_SAIL_PELLET_LANE_TRANSIT` with `P342_BEAM_SAIL_AND_PELLET_LANE`; rerun route export to `applied_lore_route_cards=394`.
Rejected Alternatives: weakening route-card validation; duplicating old packet names; hand-editing generated route output only.
Scalability potential: Stable route graph avoids bake/runtime lookup drift across all device tiers.
Hardware Impact: 0 us/frame; source-route correction only.

Problem: RS077-RS080 needed proof across all export surfaces without runtime claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=400`, `locales=15`, `rows=6000`, `graph_rows=400`, `route_cards=394`, `route_source_rows=394`, `wiki_pages=6000`, `site_pages=6000`, `manual_policy_rows=314`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=287`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; claiming missions, UI, scene placement, ending cinematics, native localization or public web assembly are complete from source packets.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/placement/UI/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Colony humanity still needed named anchors that can become props, terminals, audio and wiki articles without violating the user's rejection of family-revenge motive.
Solution: Add RS081 packets P401-P405 for Mara Venn, Juno Kade, Ren Okoye, Sahana Iqbal and Lian Torres. Each worker is preserved through job evidence: pump cadence, relay witnessing, brine mapping, repair triage and vent-forge operation.
Rejected Alternatives: relatives of the protagonist, heroic biographies, abstract "colonists had lives" prose, or generic worker names not tied to physical evidence.
Scalability potential: Low tier can place one prop/card per worker. Middle/High/Ultra can add richer prop clusters, audio fragments, localized dossier pages and seed-varied object pairings around stable packet IDs.
Hardware Impact: 0 us/frame; source content only.

Problem: Deep Reach culpability risked staying too abstract after the physical flood correction.
Solution: Add RS082 packets P406-P410 for Varnek margin acceptance, Arendt Atlas weighting waiver, Haldane quarantine hold, Ibarra loss conversion and Sato-Ren return-action packet. The crime is accepted margin, delayed release and custody pressure under real flood physics.
Rejected Alternatives: cartoon villain monologue, making Deep Reach solely cause the flood, making them innocent because physics flooded the colony, or burying liability in non-interactive lore.
Scalability potential: Low tier can show memo cards/terminals. Middle/High/Ultra can add archive-room props, stamped overlays, signal-window UI and public article diagrams without changing truth ownership.
Hardware Impact: 0 us/frame; source artifact content only.

Problem: Fauna needed systemic encounter language for replay and scanner/PDA use, not only species descriptions.
Solution: Add RS083 packets P411-P415 for predator shadow, glass grazer clearing, lantern drift false-safe, brine vane navigation and sensor-tagged fauna pursuit grammar. Encounters teach route risk, ambiguous safety, density navigation and Atlas feedback misuse.
Rejected Alternatives: boss list, monster gallery, random jump scare table, talking-ocean mysticism or mind-controlled pet framing.
Scalability potential: Low tier can use scanner text and simple spawn/absence rules. Middle/High/Ultra can add richer telegraphs, sonar distortion, ambient fauna VFX and seed-varied encounter composition around the same packet hashes.
Hardware Impact: 0 us/frame; no AI/runtime change in this pass.

Problem: Public/wiki content was becoming large enough to need navigation clusters, not just more pages.
Solution: Add RS084 packets P416-P420 for start-here, system/ships, colony/workers, resources/ecology and spoiler-gated endings hubs. These clusters make publication and in-game codex grouping explicit.
Rejected Alternatives: unordered packet dump, spoiler-heavy public navigation, implementation-report pages masquerading as wiki, or good/bad ending labels.
Scalability potential: Low tier can export static hub pages. Middle/High/Ultra can add image headers, spoiler modals, orbital maps and richer codex filters without changing source ownership.
Hardware Impact: 0 us/frame; publication/codex source only.

Problem: RS081-RS084 route validation exposed stale packet names and an unsupported ending-pressure category.
Solution: Correct `P352_BRINE_DENSITY_LADDER_ROUTES` to `P352_BRINE_CANYON_DENSITY_LADDER_GUIDE`, `P353_VENT_FORGE_PROCESS_CHAIN` to `P353_VENT_FORGE_FIELD_PROCESS_GUIDE`, `P355_PRESSURE_GLASS_SEALANT_ROUTE` to `P355_PRESSURE_GLASS_AND_SEALANT_GUIDE`, `P351_DROWNED_CRUST_STRATA_FIELDGUIDE` to `P351_DROWNED_CRUST_STRATA_GUIDE`, and change RS084 `ending_pressure` from unsupported `spoiler` to valid `truth`.
Rejected Alternatives: weakening exporter/audit validation, duplicating old packet names, or hand-editing only generated route output.
Scalability potential: Valid source graph avoids future DataMonolith bake/runtime lookup drift across all device tiers and publication surfaces.
Hardware Impact: 0 us/frame; source-route correction only.

Problem: RS081-RS084 needed proof across all export surfaces without runtime claims.
Solution: Run generator, importer, page exporter, route-card exporter and source-only audit. Final proof: `packets=420`, `locales=15`, `rows=6300`, `graph_rows=420`, `route_cards=414`, `route_source_rows=414`, `wiki_pages=6300`, `site_pages=6300`, `manual_policy_rows=334`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=307`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; claiming scene placement, runtime UI, native localization, audio, cinematics, ending implementation or public web assembly are complete from source packets.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/placement/UI/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Public hard-sci-fi material still lacked a safe layer between vague "Aegir is far" prose and exact orbital constants that are not table-owned yet.
Solution: Add RS085 packets P421-P425 for Ran/Aegir public distance bands, local Aegir window bands, HECTON-8 moon-ladder bands, Black Keel transfer-orbit bands and the ephemeris table handoff rule.
Rejected Alternatives: Inventing exact periods/inclinations in prose; leaving public articles with no route math pressure; building runtime orbital simulation during a lore pass.
Scalability potential: Low tier can show static route/window cards. Middle/High/Ultra can add animated orbital maps, transfer arcs and recovery-window UI after celestial tables own the exact numbers.
Hardware Impact: 0 us/frame; source content only.

Problem: Resource economy could still be mistaken for generic loot instead of custody, containment, mass and evidence.
Solution: Add RS086 packets P426-P430 for blue debt custody receipts, pressure-glass certificates, brine process lot cards, Atlas lattice contamination tags and Black Keel payout mass ledgers.
Rejected Alternatives: Magic ore labels; generic inventory flavor; choosing final numeric yields in prose; runtime economy implementation in a content pass.
Scalability potential: Low tier can use static receipts and scan cards. Middle/High/Ultra can add ledger UI, lab labels, animated containment warnings and payout overlays from stable packet hashes.
Hardware Impact: 0 us/frame; source artifact content only.

Problem: PDA/scanner/terminal/dossier lore had enough packets but still needed presentation rules so runtime UI work does not invent tone and disclosure order independently.
Solution: Add RS087 packets P431-P435 for evidence tier labels, scanner stage copy escalation, terminal operator surface, dossier ending record layout and localized overflow presentation.
Rejected Alternatives: Claiming UI implementation; leaving copy rules to ad hoc UI text; live translation or runtime markdown parsing.
Scalability potential: Low tier can display concise baked strings. Middle/High/Ultra can add richer filtering, animation, responsive layouts and device-specific presentation while using the same packet rows.
Hardware Impact: 0 us/frame; source UI copy only.

Problem: Audio/transcript surfaces were scattered across packets but not yet structured as article-ready performance seeds.
Solution: Add RS088 packets P436-P440 for Black Keel approach transcript, sanitized Deep Reach packet transcript, worker dossier audio seed, Atlas repair trace transcript and ending record transcript.
Rejected Alternatives: constant companion chatter; villain monologues; worker melodrama; Atlas speaking as a human; claiming routed audio implementation.
Scalability potential: Low tier can expose transcript text only. Middle/High/Ultra can add voice processing, subtitles, diegetic playback and spoiler-gated article embeds after audio/runtime passes.
Hardware Impact: 0 us/frame; source transcript content only.

Problem: Initial RS085-RS088 verification used parallel tool execution where route/page exporters raced stale generated source files.
Solution: Rerun importer -> route exporter -> page exporter -> audit sequentially. Final proof: `packets=440`, `locales=15`, `rows=6600`, `graph_rows=440`, `route_cards=434`, `route_source_rows=434`, `wiki_pages=6600`, `site_pages=6600`, `manual_policy_rows=354`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=327`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: weakening route validation; duplicating packet IDs to satisfy stale CSV; accepting failed audit output as "close enough"; running Unity/DataMonolith bake during lore work.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/placement/UI/audio/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Gameplay-table needs were still known but not actionable enough for a future table owner to build rows without reinterpreting lore.
Solution: Add RS089 packets P441-P445 for resource yield, stack limit, escape recipe, contract risk/reward and ending payout value-band drafts. They define required fields, pressure/custody/receiver bands and player-facing meaning while leaving exact numbers table-owned.
Rejected Alternatives: selecting final yields/costs in prose; leaving "balance later" as an empty note; making generic loot values disconnected from pressure and custody.
Scalability potential: Low tier can consume static table labels and concise UI copy. Middle/High/Ultra can add richer ledger UI, warnings and economy overlays from the same packet IDs after numeric tables own final values.
Hardware Impact: 0 us/frame; source packet and CSV rows only.

Problem: The manual placement backlog kept growing, but scene placement priorities were not explicit enough for a later Unity/editor pass.
Solution: Add RS090 packets P446-P450 for first-hour placement, mid-depth route placement, ecology scan placement, final descent placement and terminal-slot promotion briefs. All rows stay in `NarrativeDiscovery` backlog until Unity tooling places them.
Rejected Alternatives: raw-editing `.unity` YAML in a parallel workspace; expanding TerminalOS slots from a lore pass; leaving placement to ad hoc interpretation.
Scalability potential: Low tier can place sparse proof props. Middle/High/Ultra can scale prop density, scan stages, lighting and terminal richness around the same packet hashes.
Hardware Impact: 0 us/frame in this pass; scene work remains future Unity API work.

Problem: Multilingual source coverage existed, but production blockers for native localization/accessibility were too easy to confuse with completed translation.
Solution: Add RS091 packets P451-P455 for RU encoding/native review, CJK wrap/font proof, RTL numeric/bidi proof, European expansion fit and subtitle/audio timing. Draft non-EN/RU rows remain explicitly uncertified.
Rejected Alternatives: claiming native localization from draft rows; runtime translation; ignoring RTL/CJK/font and subtitle timing until late UI work.
Scalability potential: Low tier gets stable baked strings only after proof. Middle/High/Ultra can add richer typography, subtitles and audio treatment without changing packet IDs or string-pool authority.
Hardware Impact: 0 us/frame; source QA briefs only.

Problem: Public/site/wiki article work had many modules but still needed longform assembly briefs that avoid unsupported runtime/release claims.
Solution: Add RS092 packets P456-P460 for public home, Aegir hard-sci-fi, Deep Reach liability, Atlas spoiler layers and blue debt resource longform briefs. These define article spine, spoiler gates, image use and claim boundaries.
Rejected Alternatives: marketing fluff; public pages that spoil final receiver protocols; promising runtime/UI/release facts not implemented; treating blue debt as magic ore.
Scalability potential: Static site/wiki can render these as article scaffolds now. Higher presentation tiers can add image headers, orbital maps, spoiler modals and resource diagrams without changing lore ownership.
Hardware Impact: 0 us/frame; publication source only.

Problem: RS089-RS092 route validation exposed stale packet IDs in newly generated route cards.
Solution: Correct prereqs to real packet IDs: `P385_ATLAS_BASIN_PAYLOAD_ACT`, `P388_BRINE_CANYON_PUMP_CATHEDRAL_POI_KIT`, `P389_EVACUATION_QUEUE_TERMINAL_POI_KIT`, `P390_ATLAS_SERVICE_BASIN_POI_KIT`, and `P276_RU_NATIVE_REVIEW_LOCK`; rerun generator/importer/exporters/audit.
Rejected Alternatives: weakening route validation, duplicating stale IDs, or editing generated route CSV without fixing source generation.
Scalability potential: Stable route graph prevents DataMonolith/source lookup drift across all device tiers and publication surfaces.
Hardware Impact: 0 us/frame; source-route correction only.

Problem: RS089-RS092 needed proof across all export surfaces without runtime claims.
Solution: Run generator, importer, route-card exporter, page exporter and source-only audit. Final proof: `packets=460`, `locales=15`, `rows=6900`, `graph_rows=460`, `route_cards=454`, `route_source_rows=454`, `wiki_pages=6900`, `site_pages=6900`, `manual_policy_rows=374`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=347`, `scene_terminal_os_runtime_verified_slots=27`.
Rejected Alternatives: Running Unity/DataMonolith bake during lore work; claiming numeric tables, Unity placement, native localization, runtime UI/audio, ending cinematics or website assembly are complete from source packets.
Scalability potential: Source packets, route cards, pages, image briefs and binding targets are ready for controlled bake/table/placement/UI/audio/publication passes. Runtime target remains baked string-pool/blob lookup.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: Narrative/AppliedLore Apex proof could still miss Unity `Update`/`LateUpdate`, hot string `+`, `WaitForCompletion`, and project hygiene drift such as orphan `.meta` files. It also did not expose a local runtime struct layout/8-byte ABI counter for this domain.
Solution: Extend existing `H8NarrativeApexVerifier` instead of creating a parallel scanner. Added `Update`/`LateUpdate` roots, hot string concat detection, extra LINQ/sync completion tokens, runtime `StructLayout` literal-size checks, `Pack=1` rejection, `UnsafeUtility.SizeOf<` reference counter, and `.meta` orphan/missing-source scans.
Rejected Alternatives: standalone scanner class, runtime assertions, `dotnet build`, scene mutation, or another report-only pass.
Scalability potential: Low/Middle/High/Ultra runtime is unchanged. The verifier prevents future hot-path allocation/lookup drift before those defects reach weak devices, while high-tier visual work keeps the same zero-GC authority route.
Hardware Impact: 0 us/frame; editor-only in-memory source scan.

Problem: Existing runtime lock and hot dependency claims needed fresh proof after verifier edits.
Solution: Run scoped static checks: `hot_scope_findings=0`; `MetaCampaignService` direct write-lock helpers each have one acquire, release and `finally`; `.meta` scan reports `orphan_meta_count=0`, `missing_source_meta_count=0`; AppliedLore source audit remains green at `packets=460`, `rows=6900`, `route_cards=454`.
Rejected Alternatives: rewriting cold `TryGetComponent` sites, weakening helper-transfer DataVault pattern, or using build spam as proof.
Scalability potential: Keeps source and content gates stable for future bake/UI/publication work without changing gameplay truth, save identity, DTO layout or SignalBus payloads.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: `H8AppliedLoreWorldImpactRecord` had literal `Size=24` but no first-party cold runtime method proving `UnsafeUtility.SizeOf<T>()` alignment, so future AppliedLore world-impact expansion could drift from ARM64 layout rules without a domain-specific gate.
Solution: Extend the existing `H8AppliedLoreRuntime` facade. Add `SizeBytes=24`, explicit tail padding fields at offsets 17/18/20, and `ValidateRuntimeLayout()` using `UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>()` plus `(bytes & 7)==0`.
Rejected Alternatives: new layout helper class, changing SignalBus payloads, new monolith DTO wrapper, or adding a hot runtime assertion.
Scalability potential: All tiers keep the same 24-byte record. Low/handheld avoids misaligned DTO surprises; high/ultra can attach richer visual responses outside gameplay truth.
Hardware Impact: 0 us/frame; validation method is cold/static.

Problem: `H8NarrativeApexVerifier` had broad runtime struct counters but did not require the AppliedLore world-impact DTO proof specifically, and the editor `.meta` scan allocated full path arrays.
Solution: Bind size constant, padding offsets and `UnsafeUtility.SizeOf<T>()` proof into `ScanAppliedLoreWorldImpactPhaseRoute`; replace `Directory.GetFiles` array scans with `Directory.EnumerateFiles` streaming in `.meta` hygiene.
Rejected Alternatives: report-only proof, standalone scanner, build spam, Unity refresh, or raw scene mutation.
Scalability potential: Prevents lore/runtime drift before it reaches baked content and weak devices; editor proof gets lower peak memory without changing runtime.
Hardware Impact: 0 us/frame; editor-only scan alloc pressure reduced.

Problem: The new C# changes needed evidence without claiming a build or player proof.
Solution: Source checks passed: C# balance OK for both changed files; hot-scope scan `0`; `.meta` scan `0/0`; DataVault write-lock helpers acquire/release/finally with no heavy tokens; AppliedLore audit green at `packets=460`, `rows=6900`, `blob_bytes=8212352`, `route_cards=454`.
Rejected Alternatives: `dotnet build`, Unity compile, DataMonolith bake, native localization certification or public-site completion claims.
Scalability potential: Source and verifier gates remain usable for the next controlled bake/UI/publication pass.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: AppliedLore world-impact layout proof lived in the local runtime facade but was not part of the central DataMonolith layout audit, so a future boot/editor layout check could pass while missing this narrative DTO.
Solution: Add direct `UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>()` and 8-byte alignment checks to `H8DataLayoutAudit.ValidateBlittableSizes()`, then make `H8NarrativeApexVerifier` require those central audit tokens.
Rejected Alternatives: expanding the DTO to 32 bytes solely to match DataMonolith section alignment, adding a wrapper, or moving the DTO into a new subsystem.
Scalability potential: Low/handheld gets the same 24-byte DTO with explicit ARM64-safe alignment; higher tiers can still layer richer visual response outside this compact truth record.
Hardware Impact: 0 us/frame; cold layout audit only.

Problem: Adding `H8DataMonolithTypes.cs` to Apex scope could introduce hidden hot findings or syntax drift, so the scope expansion required proof.
Solution: Run tokenizer balance on all three touched C# files, scoped hot scan including `H8DataMonolithTypes.cs`, `.meta` hygiene, DataVault lock proof and AppliedLore runtime audit. Results: balance OK, `hot_scope_findings=0`, `.meta` `0/0`, `lock_proof_bad=0`, AppliedLore audit green at `packets=460`, `rows=6900`, `blob_bytes=8212352`.
Rejected Alternatives: assuming the new file was harmless, `dotnet build`, Unity compile, or DataMonolith bake claim.
Scalability potential: Apex verifier can now catch layout-route drift before source rows are baked into runtime content.
Hardware Impact: 0 us/frame; no `dotnet build`, no Unity compile.

Problem: AppliedLore UTF8 surface lookup had an `in` public overload but the private selector could still receive `H8AppliedLorePacketRecord` by value, creating an avoidable copy before slicing localized UTF8 bytes.
Solution: Route both public `TryGetUtf8` overloads through `TryGetUtf8FromRecord(in H8AppliedLorePacketRecord record, ...)` and make the existing Apex verifier count the pass-by-ref proof.
Rejected Alternatives: new helper utility, new DTO wrapper, reverting the localized-span route in `H8StaticDataArena`, or touching PDA/terminal UI code outside this narrow data facade.
Scalability potential: Low and handheld tiers avoid needless record copies during lore lookups; high/ultra tiers can add richer presentation without changing string-pool ownership or packet layout.
Hardware Impact: 0 heap bytes; one packet-record copy removed per facade lookup path.

Problem: The C# polish needed proof without competing with active compiler processes.
Solution: Run source-only checks: tokenizer balance OK for three touched C# files, touched runtime files `hot_roots=0 raw_forbidden_tokens=0`, DataVault helper proof acquire/release/finally with `heavy_tokens=0`, `git diff --check` clean, AppliedLore source audit green at `packets=460`, `rows=6900`, `route_cards=454`.
Rejected Alternatives: `dotnet build` while `dotnet`/`csc` processes were already running, Unity compile, scene edit, or DataMonolith bake claim.
Scalability potential: Keeps the verifier route enforceable without adding runtime work.
Hardware Impact: 0 us/frame; validation only.

Problem: Project asset hygiene scan found four source/shader assets without `.meta`, which would let Unity assign unstable GUIDs during import and break deterministic asset identity.
Solution: Add only missing companion `.meta` files for the two `1722/1724` baker C# files and two compute shaders, using unique GUIDs and matching local importer formats.
Rejected Alternatives: editing the untracked baker source files, deleting them, leaving missing meta for Unity to auto-generate, or changing scene/prefab references.
Scalability potential: Stable GUIDs keep editor/baker assets addressable across weak and high-end authoring machines without changing runtime systems.
Hardware Impact: 0 us/frame; asset import identity repair only.

Problem: The meta repair needed proof that it did not create orphan metadata or duplicate GUIDs.
Solution: Run asset hygiene scan and GUID search. Results: `orphan_meta_count=0`, `missing_source_meta_count=0`; each new GUID appears exactly once; AppliedLore source audit remains green at `packets=460`, `rows=6900`, `route_cards=454`.
Rejected Alternatives: `dotnet build` for a meta-only pass, Unity import refresh, or DataMonolith bake claim.
Scalability potential: Keeps Apex hygiene gate clean for later agents and prevents Unity import churn.
Hardware Impact: 0 us/frame; no compile or player runtime work.

Problem: `H8AppliedLoreRuntime` still contained a private UTF8 surface selector after the central `H8StaticDataArena.TryGetAppliedLoreUtf8(in record, ...)` route existed, creating duplicate route logic.
Solution: Remove the facade selector and route both runtime facade overloads directly into `H8StaticDataArena.TryGetAppliedLoreUtf8(in record, surface, out utf8Bytes)`.
Rejected Alternatives: Adding another helper, keeping the duplicate switch, editing another agent's `H8StaticDataArena` implementation, or changing packet/string-pool ownership.
Scalability potential: Low/handheld avoids duplicated facade branch logic; high/ultra can expand presentation without creating a second content owner.
Hardware Impact: 0 heap bytes; one local selector path removed; hot roots remain 0.

Problem: The Apex proof counted `in` usage but did not prove the facade had no duplicate UTF8 selector after the central route was present.
Solution: Extend `H8NarrativeApexVerifier` to include `H8StaticDataArena.cs`, count arena pass-by-ref proof tokens, and reject `case H8AppliedLoreSurface.*`, `TryGetLocalizedUtf8Span(` or `TryGetUtf8FromRecord(` selector leftovers inside `H8AppliedLoreRuntime`.
Rejected Alternatives: Markdown proof, standalone scanner, runtime assertion, or broad gameplay refactor.
Scalability potential: Keeps one fact -> one owner -> one route enforceable before lore content reaches PDA, terminal, wiki and site surfaces.
Hardware Impact: 0 us/frame; editor-only static proof.

Problem: The C# route flattening needed proof without violating compilation throttling.
Solution: Source-only verification: `runtime_arena_utf8_calls=2`, `arena_methods=1`, `arena_in_record_params=1`, `total_pass_by_ref_proofs=4`, `facade_duplicate_selectors=0`; hot-token scan `0`; tokenizer balance OK; `.meta` hygiene `0/0`; DataVault helper proof unchanged; AppliedLore audit green at `packets=460`, `rows=6900`, `route_cards=454`.
Rejected Alternatives: `dotnet build`, Unity compile/import, DataMonolith bake, scene edit, or touching already-dirty `H8StaticDataArena.cs` content.
Scalability potential: The verifier now catches content-route drift early without adding runtime cost on weak devices.
Hardware Impact: 0 us/frame; no compiler process launched.

Problem: `H8NarrativeApexVerifier` checked missing `.meta` only for `.cs`, `.shader` and `.compute`, leaving prefab/material/scene/texture/audio/UI/DataMonolith source identity drift outside the Apex gate.
Solution: Extend the same verifier with one static `SourceMetaRequiredExtensions` table and a single `Directory.EnumerateFiles(assetsRoot, "*", ...)` pass filtered by extension.
Rejected Alternatives: A parallel meta scanner, Unity import refresh, deleting files, relying on Unity to generate GUIDs after the fact, or scanning the tree once per extension.
Scalability potential: Stable asset identity protects low-tier and high-tier builds equally; prefab/material/texture/audio references do not churn across authoring machines.
Hardware Impact: 0 us/frame; editor-only source scan.

Problem: The expanded meta surface needed proof without compile or import side effects.
Solution: Static source/meta scan reports `source_meta_extensions=27`, `source_meta_files_scanned=11908`, `missing_source_meta_files=0`, `meta_files_scanned=13887`, `orphan_meta_files=0`; tokenizer balance OK; AppliedLore audit remains green at `packets=460`, `rows=6900`, `route_cards=454`.
Rejected Alternatives: `dotnet build` while `dotnet` processes were active, Unity compile/import, DataMonolith bake, scene edit, or regenerating lore/source pages.
Scalability potential: Catches Unity identity drift before content bake and runtime surface wiring.
Hardware Impact: 0 us/frame; no compiler process launched.

Problem: `AwaitableDropSequenceDirector.RecordStage` released its DataVault write lock in `finally`, but the lock body still resolved runtime frame/orbital telemetry and cursor math while holding the native black-box buffer.
Solution: Hoist `IPrologueSequenceRuntime`, `CurrentFrame`, sequence, orbital speed, orbital distance, and ring cursor calculation before `TryAcquireWriteLock`; leave only buffer validity check, DTO primitive assignment, one `NativeArray` slot write, cursor store, and `ReleaseWriteLock` in `finally` inside the critical section.
Rejected Alternatives: Adding a new telemetry manager, adding a helper wrapper around DataVault, changing black-box DTO ownership, moving prologue telemetry to managed logs, or deleting the black-box path.
Scalability potential: Low-tier machines get shorter write-lock occupancy and no new allocations; middle/high/ultra keep the same black-box truth route and can add richer prologue presentation outside the lock.
Hardware Impact: 0 us/frame steady state; critical-section work reduced to primitive assignments and one native slot write.

Problem: A one-off grep proof would not stop future agents from moving heavy telemetry back into the DataVault write-lock `try`.
Solution: Add `ScanPrologueBlackBoxDataVaultRoute` to `H8NarrativeApexVerifier`, with counters for write-lock count, release-finally proof, hoisted telemetry proof, and heavy tokens inside the release-finally try block.
Rejected Alternatives: Markdown-only assertion, separate scanner file, broad lock-system refactor, or relying on manual review.
Scalability potential: Editor-only proof protects the prologue route across all device tiers without spending runtime budget.
Hardware Impact: 0 us/frame; verifier-only static AST/source gate.

Problem: The C# change needed evidence without violating compilation throttle.
Solution: Source-only verification: tokenizer balance OK for `AwaitableDropSequenceDirector.cs` and `H8NarrativeApexVerifier.cs`; `prologue_blackbox_write_locks=1`, `prologue_blackbox_release_finally=1`, `prologue_blackbox_hoisted_telemetry=6`, `prologue_blackbox_heavy_inside_lock=0`; prologue hot-token scan found no dependency/LINQ/WaitForCompletion/container allocation; AppliedLore audit green at `packets=460`, `rows=6900`, `route_cards=454`.
Rejected Alternatives: `dotnet build` while `dotnet` PID 47240 was active, Unity compile/import, DataMonolith bake, scene edit, generated lore rewrite, or killing compiler processes without ownership.
Scalability potential: Keeps the source gate and content route clean while avoiding multi-agent workstation stalls.
Hardware Impact: 0 us/frame; no compiler process launched.

Problem: `PDAEncyclopediaStreamer.RecordTelemetry` read the telemetry ring through `TryReadVaultBuffer(in _telemetryHandle, ...)` only to check capacity, then immediately acquired a write lock for the same ring.
Solution: Remove the redundant read-only telemetry ring lookup and validate `telemetry.Length < TelemetryFrameCount` inside the write-lock block before the native slot write.
Rejected Alternatives: Adding a telemetry proxy, adding a second ring, deleting PDA black-box telemetry, changing stream state ownership, or broad UI refactor.
Scalability potential: Low-tier devices avoid one vault read per visible PDA telemetry frame; high/ultra tiers keep the same ring for richer PDA diagnostics and presentation timing.
Hardware Impact: 0 heap bytes; one read-only DataVault lookup removed from the steady visible PDA telemetry path.

Problem: The PDA telemetry route needed a durable regression gate so future edits cannot reintroduce the same redundant read.
Solution: Add `ScanPdaTelemetryVaultRoute` to `H8NarrativeApexVerifier`, requiring exactly two write locks, two release-finally proofs, zero redundant `_telemetryHandle` read-only snapshots in `RecordTelemetry`, one write-lock size proof, one runtime-state fallback read, and streaming-frame snapshot pass proofs.
Rejected Alternatives: One-off grep, markdown-only assertion, standalone scanner file, or runtime assertion.
Scalability potential: Editor-only source gate protects PDA telemetry routing without runtime cost across all quality tiers.
Hardware Impact: 0 us/frame; verifier-only proof.

Problem: Normal PDA streaming frames called `WriteRuntimeState` and then `RecordTelemetry`, which reread runtime state just to recover `UnlockedCount` already loaded by `WriteRuntimeState`.
Solution: Change `WriteRuntimeState` to return `unlockedCountSnapshot` by `out uint`; pass it from `LateFrameTick` into `RecordTelemetry`; keep a fallback runtime-state read for locked/complete telemetry frames.
Rejected Alternatives: Global unlocked-count cache, stale UI field, removing locked/complete telemetry, adding a DTO wrapper, or changing PDA truth ownership.
Scalability potential: Weak devices avoid an extra runtime-state vault read in the common streaming path; stronger devices keep deterministic telemetry for richer diagnostics.
Hardware Impact: 0 heap bytes; one additional DataVault read removed from normal streaming PDA telemetry frames.

Problem: The source patch needed proof without competing with active compiler work.
Solution: Source-only checks: tokenizer balance OK for `PDAEncyclopediaStreamer.cs` and `H8NarrativeApexVerifier.cs`; `lateframe_streaming_snapshot_write_calls=1`, `lateframe_streaming_snapshot_record_calls=1`, `write_runtime_state_out_params=1`, `record_telemetry_write_locks=2`, `record_telemetry_release_finally=2`, `record_telemetry_redundant_readonly=0`, `record_telemetry_runtime_fallback_reads=1`, `record_telemetry_size_proofs=1`, `pda_telemetry_hot_tokens=0`; AppliedLore audit green at `packets=460`, `rows=6900`, `route_cards=454`.
Rejected Alternatives: `dotnet build` while `dotnet` PID 47240 was active, Unity compile/import, DataMonolith bake, generated lore rewrite, scene edit, or killing another agent's process.
Scalability potential: Keeps the runtime route lean while respecting multi-agent compilation throttle.
Hardware Impact: 0 us/frame; no compiler process launched.

Problem: `TerminalOsRuntime.RecordTelemetry` opened the terminal telemetry ring before computing the layout hash, so the telemetry write path overlapped ring access with a read-only screen-command snapshot.
Solution: Compute `layoutHashSnapshot` and terminal count first, then open `_telemetryRingHandle`, guard ring length, write through a clamped telemetry index, and advance from that index.
Rejected Alternatives: New telemetry manager, second ring, DTO wrapper, deleting black-box telemetry, or changing terminal UI truth ownership.
Scalability potential: Low-tier devices avoid unnecessary DataVault buffer overlap in VISUAL_SYNC; middle/high/ultra keep the same black-box diagnostics route.
Hardware Impact: 0 heap bytes; telemetry-ring residency shortened to final slot write.

Problem: `RecordDecryptionTelemetry` opened the decryption telemetry ring before puzzle/terminal snapshot reads and advanced cursor from the raw cursor rather than the clamped write index.
Solution: Read puzzle/terminal snapshots first, open `_decryptionTelemetryRingHandle` only for the final write, and advance `_decryptionTelemetryCursor` from `telemetryIndex`.
Rejected Alternatives: Managed staging queue, changing puzzle state authority, removing decryption telemetry, or broad terminal subsystem refactor.
Scalability potential: Safer telemetry cursor behavior across quality tiers while keeping puzzle truth unchanged.
Hardware Impact: 0 heap bytes; final write path has shorter native-buffer residency.

Problem: The TerminalOS telemetry ordering fix needed a durable source gate.
Solution: Extend `H8NarrativeApexVerifier` with `ScanTerminalOsTelemetryVaultRoute`, proving layout-hash hoist, telemetry ring open-after-snapshot shape, ring length guards, decryption snapshot-before-ring order, and clamped cursor advancement.
Rejected Alternatives: One-off grep, markdown proof, standalone scanner, Unity scene edit, or DataMonolith bake claim.
Scalability potential: Protects terminal/PDA/wiki AppliedLore surface route from future telemetry drift without runtime cost.
Hardware Impact: 0 us/frame; editor/source-only proof.

Problem: The pass needed verification without compiler spam.
Solution: Source-only verification reports token balance OK; `terminal_layout_hoists=2`, `terminal_ring_after_snapshot_tokens=3`, `terminal_ring_length_guards=2`, `decryption_snapshot_before_ring=1`, `decryption_cursor_clamps=2`, `verifier_terminal_gate_tokens=9`, hot forbidden tokens `0`; AppliedLore audit remains green at `packets=460`, `rows=6900`, `route_cards=454`.
Rejected Alternatives: `dotnet build`, Unity compile/import, DataMonolith bake, generated lore rewrite, scene edit, or process kill.
Scalability potential: Keeps proof local and cheap while other agents can continue using the workstation.
Hardware Impact: 0 us/frame; no compiler process launched.

Problem: `RecordTerminalInputTelemetry` in the TerminalOS projection partial composed `projectionFaults` near the terminal input telemetry ring write, leaving more non-write work coupled to the DataVault buffer window than needed.
Solution: Hoist owner/input fault composition before `_terminalInputTelemetryRingHandle` is opened, keep only ring validity, DTO assignment, one native slot write and clamped cursor advance in the ring write path.
Rejected Alternatives: New input telemetry manager, managed queue, changing terminal command authority, deleting input telemetry, or merging unrelated TerminalOS partials.
Scalability potential: Weak devices get shorter native-buffer residency in VISUAL_SYNC; middle/high/ultra keep the same black-box evidence route for richer terminal presentation.
Hardware Impact: 0 heap bytes; no added frame work.

Problem: The TerminalOS verifier initially covered runtime and decryption telemetry but not the terminal input projection partial.
Solution: Add `TerminalOsRuntime_TerminalProjection.cs` to the verifier scope and require `input_faults_before_ring=1` plus `input_cursor_clamps=2` in `ScanTerminalOsTelemetryVaultRoute`.
Rejected Alternatives: Markdown-only claim, separate scanner, broad UI refactor, or runtime assertion.
Scalability potential: Prevents future input telemetry drift without runtime cost.
Hardware Impact: 0 us/frame; editor/source-only gate.

Problem: `PDAEncyclopediaStreamer.WriteBlackBoxDump` serialized the 300-frame black-box dump by calling `TryReadTelemetryDumpEntry` once per row, and that helper re-resolved `_telemetryHandle` through `TryReadVaultBuffer` each time. The path is fault-only, but it made the dump route structurally noisier than the TerminalOS dump route and violated the flat snapshot principle.
Solution: Snapshot `NativeArray<PdaEncyclopediaTelemetryEntry>.ReadOnly telemetrySnapshot` once before dump serialization, read entries directly from that snapshot during the fixed 300-row copy loop, and remove the per-row helper.
Rejected Alternatives: New dump manager, managed staging list, deleting PDA telemetry, moving DataVault ownership, or keeping repeated read resolves because the path is fault-only.
Scalability potential: Weak devices avoid repeated vault checks during a fault dump; middle/high/ultra keep the same PDA black-box data and can layer richer diagnostics without changing the telemetry owner.
Hardware Impact: 0 us/frame steady state; removes up to 300 repeated DataVault read attempts per PDA dump.

Problem: PDA black-box dump payload allocation used raw `new NativeArray<byte>(..., Allocator.Temp, ClearMemory)` and manual `Dispose`, while first-party dump routes use `NativeFaultDumpWriter` for transient payload ownership.
Solution: Route payload creation/disposal through `NativeFaultDumpWriter.CreateTransientPayload` and `DisposeTransientPayload` with `NativeArrayOptions.ClearMemory`, preserving zeroed header padding and adding owner-label tracking.
Rejected Alternatives: Manual Temp allocation, uninitialized payload memory, adding a local allocator wrapper, or changing dump binary layout.
Scalability potential: Keeps fault payload ownership consistent across low-tier and high-tier devices without runtime frame cost.
Hardware Impact: 0 us/frame; fault-only ownership hygiene.

Problem: A source edit alone would not prevent future agents from restoring the per-row vault read or raw payload allocation.
Solution: Extend `H8NarrativeApexVerifier.ScanPdaTelemetryVaultRoute` with PDA dump counters for single telemetry snapshot, per-row reads, transient payload create/dispose, and raw payload allocs.
Rejected Alternatives: One-off grep, markdown-only assertion, standalone scanner, runtime assertion, or Unity build spam.
Scalability potential: Editor/source-only tripwire protects the PDA dump route before it reaches runtime QA.
Hardware Impact: 0 us/frame; verifier-only static gate.

Problem: AppliedLore multilingual packet sources encoded localization review state inside player-visible prose, so strings such as draft/native-review prefixes could leak into DataMonolith CSV, in-game wiki pages, and external site pages.
Solution: Strip known authoring-only localization prefixes in `AppliedLoreImporter` before player-visible CSV fields are emitted, while preserving review state through the existing `flags` column and `H8AppliedLoreHashes.RowFlagDraftLocalization`.
Rejected Alternatives: Leaving markers visible, adding a new localization-state table, changing packet IDs, changing `H8AppliedLorePacketRecord` layout, or pretending incomplete locales are final.
Scalability potential: Low/middle/high/ultra devices all read the same clean baked text; publication/QA can still route draft rows without runtime branching.
Hardware Impact: 0 us/frame; existing 128-byte record layout unchanged.

Problem: Site/wiki consumers needed a direct place to see which locales are complete without parsing body prose or packet JSON by hand.
Solution: Add frontmatter fields `direction`, `localization_status`, and `localization_flags` to generated pages and generate `Localization_Status_Index.md` from source packet JSON.
Rejected Alternatives: JSON report files, binary telemetry dumps, manual wiki notes, or embedding review state in article body text.
Scalability potential: Web/wiki tooling can filter source-ready vs native-pass-pending locales deterministically; RTL routing is explicit for Arabic and Hebrew.
Hardware Impact: Offline export only; runtime cost 0.

Problem: A one-time grep would not protect future exports from reintroducing draft/native-review marker leakage.
Solution: Add `validate_no_visible_localization_markers` to `AppliedLoreRuntimeAudit`, scanning player-visible CSV fields plus generated `in_game_wiki` and `external_site` Markdown before the rest of the route audit.
Rejected Alternatives: Manual QA, separate scanner file, markdown-only proof, or trusting the importer without an audit gate.
Scalability potential: Source-only CI/editor audit catches publication regressions before DataMonolith or site packaging.
Hardware Impact: 0 us/frame; audit-only filesystem scan.

Problem: Generated encyclopedia pages were player-readable but weak as a transport surface for site/wiki tooling because each page lacked release-set, unlock and tag metadata.
Solution: Extend `AppliedLorePageExporter` frontmatter with `release_set_id`, `unlock_id`, `poi_tags` and `biome_tags`, using the same two-tag runtime bridge already emitted to DataMonolith CSV.
Rejected Alternatives: Making site/wiki consumers parse packet JSON, adding a runtime Markdown dependency, adding another unmanaged DTO, or using a hand-maintained navigation document.
Scalability potential: Weak devices keep the same baked game data; external tools and high-tier presentation sites can build richer navigation from static metadata.
Hardware Impact: 0 us/frame; offline Markdown generation only.

Problem: Site/wiki ingestion would still need to scan every Markdown page to build a multilingual navigation model.
Solution: Generate `Publication_Surface_Index.csv` with one deterministic row per surface/locale/packet page, including relative page path, localization status and route metadata.
Rejected Alternatives: JSON report file, manual sitemap, filesystem crawl as build truth, or embedding navigation state inside visible article prose.
Scalability potential: Website, wiki and launcher tooling can ingest one CSV table and filter by locale/surface/status/tag without touching runtime game systems.
Hardware Impact: 0 us/frame; 13,800-row offline publication index.

Problem: New publication metadata could silently drift from the DataMonolith CSV source.
Solution: Extend `AppliedLoreRuntimeAudit` to validate 13,800 page frontmatter records and 13,800 `Publication_Surface_Index.csv` rows against CSV packet rows.
Rejected Alternatives: Counting rows only, one-off grep, manual review, or trusting the exporter.
Scalability potential: Source-only validation catches publication route drift before site/wiki packaging.
Hardware Impact: 0 us/frame; audit-only filesystem scan.

Problem: The multilingual encyclopedia had page and surface metadata, but no compact hard-sci-fi navigation manifest for the five RS084 public/wiki hubs. Site/wiki tooling would still need to infer start-here, system/ships, colony/workers, resources/ecology and endings-spoiler routes by crawling pages or duplicating taxonomy logic.
Solution: Extend `AppliedLorePageExporter` with `Publication_Cluster_Index.csv`, generated from `RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv` and packet JSON. Each row carries surface, locale, direction, cluster id/order, cluster packet id, release/unlock route, spoiler tier, prerequisites, next cluster, page path, localized title, truth payload and route question.
Rejected Alternatives: Hand-maintained wiki map, JSON report file, runtime markdown parsing, new taxonomy table not owned by RS084, or forcing publication tooling to scan 13,800 Markdown files for cluster state.
Scalability potential: Low-tier game clients are unaffected because runtime still reads baked packet rows. Middle/high/ultra publication stacks can build richer localized hard-sci-fi hubs from one 150-row CSV without changing game DTOs or packet identity.
Hardware Impact: 0 us/frame; offline publication bridge only.

Problem: A cluster manifest could silently drift from the RS084 evidence graph or localized DataMonolith CSV source.
Solution: Extend `AppliedLoreRuntimeAudit` to validate `Publication_Cluster_Index.csv` against RS084 graph and CSV rows: header, row count, uniqueness, RTL direction, localization flags/status, route metadata, page existence, spoiler tier, prerequisite/next packet IDs, truth payload and player question.
Rejected Alternatives: Row-count-only proof, trusting exporter output, manual QA, or reporting source health without graph-level validation.
Scalability potential: Source-only audit catches site/wiki navigation drift before website/wiki packaging and before any DataMonolith bake is claimed.
Hardware Impact: 0 us/frame; audit-only filesystem scan.

Problem: `python -B -m py_compile` attempted to write a `.pyc` temp file under `Tools/__pycache__` and hit a permission error, making it unsuitable as syntax evidence for this pass.
Solution: Use in-memory `ast.parse` over `Tools/AppliedLorePageExporter.py` and `Tools/AppliedLoreRuntimeAudit.py`, then regenerate pages and run `AppliedLoreRuntimeAudit --source-only`.
Rejected Alternatives: Escalating only to write `.pyc`, deleting `__pycache__`, running `dotnet build`, Unity import, or pretending the permission failure was a syntax failure.
Scalability potential: Keeps validation CPU/lightweight and avoids compiler spam in a multi-agent workspace.
Hardware Impact: 0 us/frame; no compiler process launched.

Problem: The AppliedLore encyclopedia had generated pages and deterministic CSV manifests, but a human reader still had to open CSV paths or browse locale folders manually. That blocked direct review of the hard-sci-fi content even though the publication data was already present.
Solution: Add one static `reader.html` in `Docs/Lore/AppliedContent` that consumes the existing surface and cluster manifests, then fetches generated Markdown pages by relative path. The reader exposes locale, surface, localization status, search and RS084 cluster navigation without adding runtime game dependencies.
Rejected Alternatives: New generator script, web framework, package install, JSON report, binary telemetry dump, hand-maintained article index, runtime markdown parsing, or changing C# DataMonolith DTOs.
Scalability potential: Weak devices and game builds are unaffected; middle/high/ultra publication surfaces can use the same CSV/page truth for richer websites or wiki shells later.
Hardware Impact: 0 us/frame; local HTTP reader only. Server process is explicit PID 32960 on `127.0.0.1:8788` and can be stopped without touching unrelated Python processes.

Problem: The first public pages exposed game/service snippets as website articles: one-sentence `external_site` text followed by `Scanner`, `Terminal`, `Audio`, and `Field Note` sections. That made the reader technically functional but editorially bad.
Solution: Add `external_site_article` longform bodies to the five RS084 hub packets for `en_US` and `ru_RU`, restore corrupted Russian titles/snippets, and teach `AppliedLorePageExporter` to use the longform body for `external_site` while leaving compact in-game wiki cards intact.
Rejected Alternatives: Blaming the reader, hiding RU by defaulting to EN, hand-editing generated Markdown that would be overwritten, adding a web framework, expanding runtime DTOs, or claiming the whole encyclopedia was prose-polished.
Scalability potential: Game clients stay on compact baked packet data; publication surfaces can now progressively replace stubs with real longform bodies without runtime changes.
Hardware Impact: 0 us/frame; offline source/content and page export only.

Problem: The first longform repair still read like internal guidance: it talked about the player, family-hook rules, evidence-layer intent and "should" structure instead of presenting the world as a readable encyclopedia.
Solution: Rewrite the five RS084 EN/RU public longform bodies as in-world archive/encyclopedia prose, then scan the visible bodies for meta-guidance vocabulary and service headings.
Rejected Alternatives: Defending the text because it contained useful facts, editing only the rendered Markdown, leaving RU as a weaker fallback, or claiming source audit quality equals editorial quality.
Scalability potential: The same `external_site_article` field can now be used as the migration target for future public longform rewrites without touching runtime systems.
Hardware Impact: 0 us/frame; publication content only.

Problem: The worker article was still too compressed, and a PowerShell inline JSON rewrite corrupted Cyrillic longform text into literal `?` characters.
Solution: Move `P418` EN/RU longform article bodies into dedicated markdown source files and make the exporter prefer `external_site_article_path` before inline JSON prose. Rewrite the worker article around ledgers, certifications, shift boards, relay maintenance, triage custody and named workers.
Rejected Alternatives: Keeping long Cyrillic prose inside JSON, editing generated markdown only, accepting mojibake in the local reader, or pretending short hub copy is a lore encyclopedia.
Scalability potential: Long public articles can now live as readable content files while compact packet fields remain available for game/wiki surfaces.
Hardware Impact: 0 us/frame; publication-only source and offline export.
