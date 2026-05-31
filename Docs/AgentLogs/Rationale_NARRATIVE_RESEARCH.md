# NARRATIVE_RESEARCH Rationale

Status: PENDING VERIFICATION

Problem: User requested story/scenario understanding without a batch XML prompt or explicit agent ID.
Solution: Use local ID `NARRATIVE_RESEARCH`; treat work as read-only narrative research. No code edits, no API changes, no scene changes.
Rejected Alternatives: Guessing story from memory; reading archived logs as authority; inventing a new batch assignment.
Scalability potential: Narrative beats must map to existing zero-GC delivery surfaces: PDA, terminals, scan records, environmental POIs, audio warnings. Low/Middle/High/Ultra should scale presentation density, not gameplay truth.
Hardware Impact: Research-only. Runtime impact 0 us/frame. Future narrative delivery must avoid hot-path managed allocation and use existing data/streaming routes.

Problem: Active lore and Russian drafts contain overlapping but not identical player identity assumptions.
Solution: Keep the conflict visible for user control. Candidate identities: independent Marauder/salvage engineer, corporate expendable/convict asset, former Deep Reach employee, or fake-credential researcher. This choice changes PDA voice, orders, guilt, and final agency.
Rejected Alternatives: Silently merge all identities into one vague protagonist; choosing corporate identity without approval.
Scalability potential: Identity affects authored text and routing only. Low/Middle/High/Ultra can vary evidence density, VO/audio layering, and optional terminal detail.
Hardware Impact: 0 us/frame if compiled into static data and delivered by existing PDA/terminal/audio routes.

Problem: Atlas-6 motivation has two compatible but unordered explanations: protect Seed ecosystem and recreate dead humans/colony from available materials.
Solution: Treat protection as primary directive outcome and reconstruction as Atlas' damaged method until user confirms canon hierarchy.
Rejected Alternatives: Turning Atlas into a simple hostile AI; turning biomech drones into generic monsters.
Scalability potential: Low tier can show this through short scan/PDA lines; high/ultra can add layered audio, visual traces, and multi-stage environmental evidence.
Hardware Impact: Narrative presentation only. Use static lore hashes and event-triggered delivery; no runtime simulation requirement.

Problem: First 20 minute production route is Copper Wire V0 while wider drafts describe a 2-hour organic descent arc.
Solution: Preserve Copper Wire as the proof lane and map only minimal story evidence onto it: broken capsule, island/shallows, oxygen pressure, copper sample, first terminal/error, first unanswered Atlas signal.
Rejected Alternatives: Expanding V0 into scanner/repair/deep module work before route blockers are proven.
Scalability potential: Presentation scales by cadence, compact text, audio density, fog/lighting, not by changing item truth or quest state.
Hardware Impact: 0 us/frame for research. Future cost must stay event-driven and under 0.1 ms suspicion threshold.

Problem: User clarified the project must take tens of hours to reach Atlas-6 and remain replayable.
Solution: Treat Atlas-6 as the gravitational center of exploration, not as a short final mission. Use stable truth with procedural geology, loot, creatures, routes, evidence order, ecological conditions, and partial endings.
Rejected Alternatives: Linear campaign pacing; fixed evidence checklist; boss-rush structure; randomized lore truth.
Scalability potential: Low/Middle/High/Ultra scale density of evidence, biome effects, audio layers, POI variants, and visual overkill while preserving gameplay truth and save identity.
Hardware Impact: Research-only 0 us/frame. Future systems must be data-driven and event-triggered; no hot string routing or scene polling.

Problem: User prefers exploration over combat or truth-map progression.
Solution: Make ocean research, flora/fauna scanning, abandoned-module traversal, depth access, and survival engineering the main loop. Drones and Deep Reach revelations apply pressure and meaning.
Rejected Alternatives: Drone war as main campaign; document hunt as primary loop.
Scalability potential: Low tier uses compact scans and simple environmental deltas; high/ultra adds richer creature behavior, acoustic layers, and biome spectacle.
Hardware Impact: Future narrative cost should be amortized through scan/POI events and static data lookup.

Problem: Ship/escape state was unresolved and user requested source-aware thinking.
Solution: Scan active prologue, ending, relay, and architecture surfaces. Treat arrival as already supported by the `01_ORBIT` prologue route. Treat escape as unimplemented/open. Recommend living-but-unavailable orbital ship because it preserves prologue continuity while preventing easy exit.
Rejected Alternatives: Declaring the ship dead without proof; declaring it a safe hub; inventing a finished escape system; basing design on empty `_PROLOGUE_CONTENT` placeholders.
Scalability potential: Escape chain can scale by route complexity, signal clarity, audio/visual response, and relay density without changing gameplay truth.
Hardware Impact: Research-only 0 us/frame. Future escape state should be quest/relay/data driven with typed signals, not hot polling.

Problem: User wants possible material ending without full truth, but not satisfying enough to replace the deep game.
Solution: Classify early material extraction as false/partial ending or consequence state. It can pay out, show hints, and pull the player back by contract escalation, guilt, Deep Reach pressure, or Atlas signal exposure.
Rejected Alternatives: Hard-forbid early exit; let early exit be fully satisfying; make endings pure final-menu choices.
Scalability potential: Low tier can use terminal/briefing text; high/ultra can add orbital sequence, corporate voice, external sector reaction, and changed world-state return.
Hardware Impact: Presentation only if event-driven. No continuous runtime cost required.

Problem: Project year and technology level were unclear; user recalled 2190 while drafts said 2170.
Solution: Preserve evidence: Seed Program 2090s, catastrophe archive 2147, draft present 2170, Xenon-Ω strategic value in 2170. Recommend 2190 as candidate present because it gives 43 years after 2147 and better supports "decades-dead" colony, myth, decay, and replayable exploration.
Rejected Alternatives: Silently overwriting dates; treating draft 2170 as immutable; making the setting too far future and losing NASA-punk constraints.
Scalability potential: Timeline choice affects authored data/text only. Presentation density can scale through environment, audio, and UI without changing tech truth.
Hardware Impact: 0 us/frame research-only.

Problem: Ship ownership has no locked canon and impacts escape, Deep Reach pressure, and player independence.
Solution: Recommend hybrid: independent/debt-bound Marauder contractor deployed by automated or skeleton-crew salvage carrier under shell contract. Carrier parks around Aegir/high transfer orbit; damaged descent capsule blocks return; rare comm windows preserve pressure without easy rescue.
Rejected Alternatives: Rich player-owned starship without economic explanation; dead ship that removes ongoing pressure; fully responsive safe ship that breaks survival; full NPC crew focus.
Scalability potential: Low tier uses text/audio comm windows; high/ultra adds orbital visuals, signal corruption, and return/extraction scenes.
Hardware Impact: Future implementation can be static/event-driven. No continuous orbital simulation required outside existing prologue presentation.

Problem: User locked 2190, hybrid salvage carrier, layered escape blockers, dirty Atlas-6 repair escalation, replay-by-seed, and hard sci-fi requirements.
Solution: Promote these from working notes into `Docs/Lore/Lore_Bible.md`; preserve decision trail in `Docs/Lore/Narrative_Crystallization.md`; add a compact hard-sci-fi taste rule to `TASTE.md`.
Rejected Alternatives: Keeping locks only in chat; overloading `TASTE.md` with lore timeline; finalizing false-ending structure before user chooses the emotional contract.
Scalability potential: Low tier can express orbital mechanics through text, timing UI, fixed sky states, comm windows, and route blockers. Middle adds better orbital visuals and signal artifacts. High adds richer sky timing, Aegir eclipse/radiation presentation, and capsule damage staging. Ultra adds sensory overkill without changing truth: orbital debris, high-fidelity entry effects, carrier silhouette, and layered comm corruption.
Hardware Impact: Documentation-only now, 0 us/frame. Future implementation should use authored ephemeris/state tables, event-triggered comm windows, and visual fakes instead of continuous N-body simulation.

Problem: User wants hard sci-fi with real technologies, astrophysics, celestial mechanics, system location, and travel justification.
Solution: Inspect existing Aegir/orbit/reentry/tide surfaces and record three location options in `Narrative_Crystallization`: outer Solar gas-giant system, nearby extrasolar system, or rogue/captured planetary system. Recommend outer Solar gas-giant system as current safest hard-sci-fi baseline.
Rejected Alternatives: Committing to FTL; inventing a new astronomy layer detached from existing Aegirium/Aegir localization; promising real-time N-body gameplay; ignoring existing orbital/tide code.
Scalability potential: Low tier uses authored window tables, static sky states, compact comm text, and simple tide/eclipses. Middle adds stronger sky presentation and orbital UI. High adds radiation/eclipses/entry VFX detail. Ultra adds carrier silhouette, debris, layered comm corruption, and richer Aegir sky without changing timing truth.
Hardware Impact: Documentation-only now, 0 us/frame. Future route should use cold-authored ephemeris data and existing celestial/tide DTOs. Continuous orbital mechanics belongs in tools/offline tables, not per-frame gameplay unless proven under budget.

Problem: User rejected the Solar System framing and requested future-lore synthesis.
Solution: Mark Solar-frontier Aegir as rejected, promote non-Solar/extrasolar Aegir direction, and define the hard-sci-fi consequence: no live Earth HQ, no instant rescue, no FTL. Deep Reach pressure must come from local proxy AI, delayed instructions, old logs, carrier automation, and in-system relays.
Rejected Alternatives: Keeping the previous Solar recommendation; making Aegir extrasolar but still allowing real-time core-world conversation; solving distance with magic FTL; making the carrier a casual personal ship.
Scalability potential: Low tier uses delayed packet text, local proxy voice, and simple comm-window gates. Middle adds carrier state UI and Aegir sky timing. High adds local relay/eclipses/radiation presentation. Ultra adds richer carrier/orbit/entry sequences and signal corruption, without changing the no-FTL truth.
Hardware Impact: Documentation-only now, 0 us/frame. Future implementation remains table/event driven: delayed-message queues, local proxy states, and authored ephemeris windows.

Problem: User locked no FTL and rejected brown-dwarf / extreme-darkness framing while asking for distance, ship types, and preflight history.
Solution: Set hard locks in `Lore_Bible`: no FTL/ansible/reactionless rescue, standard slow ships, normal yellow/orange/red dwarf host, no darkness-first premise. Record working 5-7 ly / 5.2 ly candidate and ship taxonomy in `Narrative_Crystallization`.
Rejected Alternatives: Brown dwarf host; starless rogue system; instant command from human core; vague "advanced ships" without transit consequences; making darkness come from the star instead of the ocean and pressure.
Scalability potential: Low tier uses short terminal facts, carrier proxy voice, and simple timing windows. Middle adds route planning and sky cues. High adds detailed Aegir eclipses/radiation/entry visuals. Ultra adds richer carrier, transit, and orbital presentation while keeping the same delayed-communication truth.
Hardware Impact: Documentation-only now, 0 us/frame. Future systems should consume preauthored light-delay, transfer-window, and carrier-state data.

Problem: User asked to fit Aegir to a real star system with a gas giant in or near the habitable zone and expand ship lore.
Solution: Check NASA/NExScI data for eps Eri b, GJ 876 b/c, HD 28185 b, and 47 UMa b. Record that no perfect nearby candidate satisfies all constraints. Recommend Epsilon Eridani/Ran as production anchor with HECTON-8 as a fictional moon of a known gas giant, while keeping HD 28185 and 47 UMa as HZ inspirations.
Rejected Alternatives: Forcing HD 28185 despite 128 ly distance; using GJ 876 without accepting red-dwarf compact-system consequences; claiming eps Eri b is a clean habitable-zone giant; using a purely fictional star while user asked for a real system.
Scalability potential: Low tier exposes star/cargo facts through PDA and ship logs. Middle adds route maps and carrier class records. High adds orbital/entry/sky detail. Ultra adds transit archive visuals, Aegir sky detail, and richer carrier failure presentation.
Hardware Impact: Documentation-only now, 0 us/frame. Future implementation remains static data, route tables, and authored logs.

Problem: User supplied Go2Starss as an additional hard-sci-fi source for interstellar travel.
Solution: Use it as a propulsion/tone source for beam-sail probes, microwave sail infrastructure, braking difficulty, radiation shielding mass, and domain-growth thinking. Keep NASA/NExScI as authority for current star/planet parameters.
Rejected Alternatives: Treating the site as current exoplanet catalog authority; ignoring it because it is old; copying one exact drive concept as final canon before user approval.
Scalability potential: Low tier uses ship logs and terminal route facts. Middle adds route maps and old mission archives. High adds beam-station, transit, and carrier visuals. Ultra adds layered historical telemetry and orbital infrastructure spectacle without changing travel truth.
Hardware Impact: Documentation-only now, 0 us/frame. Future integration should be static lore/data, not simulated interstellar propulsion.

Problem: User requested that fixed lore, implementation direction, open questions, and encyclopedia/article material be preserved in separate files.
Solution: Create `Canon_Locks.md`, `Implementation_Notes.md`, `Open_Questions.md`, and initial `Encyclopedia/` entries for Aegir, HECTON-8, interstellar travel, ship classes, Deep Reach, Marauders, and Atlas-6.
Rejected Alternatives: Keeping decisions only in chat; inflating `Lore_Bible.md` further; mixing spoiler writer notes with future player-facing text without labels.
Scalability potential: Low tier can use short PDA entries. Middle can add terminal variants. High can add Deep Reach internal articles and Marauder annotations. Ultra can add layered archives, route telemetry, and dossier variants while preserving one canon source.
Hardware Impact: Documentation-only now, 0 us/frame. Future use should bake articles into static localization/content data.

Problem: User clarified that Aegir is not humanity's first extrasolar star/planet/claim and that other domains existed before it.
Solution: Reframe Aegir as a later corporate frontier node inside an already-expanded human sphere. Add human domain structure and remove first-colony myth pressure from Aegir/HECTON-8.
Rejected Alternatives: Keeping Aegir as the first extrasolar miracle; making the setting too empty; implying Deep Reach built Aegir route without prior interstellar infrastructure.
Scalability potential: Low tier exposes this through short route/PDA facts. Middle adds domain maps and claim records. High adds relay/depot/corporate logistics archives. Ultra adds historic transit telemetry and broader human-space context without changing gameplay truth.
Hardware Impact: Documentation-only now, 0 us/frame. Future implementation should remain static data/articles and route-state records.

Problem: User approved the sparse mature frontier direction and requested massive lore growth.
Solution: Plant lore spores as separate topic files: Relay Spine, Corporate Claims, Dead Claims, Salvage Economy, Aegir Route, Xenon-Omega, Seed Program, plus a Lore Roadmap. Update Deep Reach as interdomain operator and codify sparse mature frontier in the lore bible.
Rejected Alternatives: Building one monolithic lore document; making the setting dense enough to break isolation; adding factions before route/claim economics are clear.
Scalability potential: Low tier can expose only compact PDA records. Middle can add domain maps and claim contracts. High can add source-specific article variants. Ultra can add route telemetry and multi-layer archive chains without changing canon.
Hardware Impact: Documentation-only now, 0 us/frame. Future content should remain static/baked and event-triggered.

Problem: User approved concrete decisions for domain count, player origin, Deep Reach age, Aegir public profile, and Xenon-Omega function.
Solution: Lock six named domains, set player origin to Barnard Yards/frontier salvage belt, make Deep Reach older than Aegir, set Aegir as specialist-known/poluzabyt, and define Xenon-Omega as a corporate codename for pressure-grown xenon-rich lattice/process used in computation, high-energy containment, and Atlas-compatible infrastructure.
Rejected Alternatives: Earth/Sol tourist origin; literal isotope; dense map with dozens of domains; making Aegir universally famous; leaving Deep Reach as local Aegir-born company.
Scalability potential: Low tier uses compact codex/domain references. Middle adds claim maps. High adds source-specific variants. Ultra adds route archives and domain history without changing player-facing core.
Hardware Impact: Documentation-only now, 0 us/frame. Future integration remains static data/localization.

Problem: User requested lore be tied together for in-game encyclopedia/articles and external website use, not just stored as notes.
Solution: Create a binding layer: content system, crosslink graph, codex delivery map, website publication map, article template, article index. Add key connector articles for player origin, salvage carrier, depth bands, and Deep Reach liability doctrine.
Rejected Alternatives: Writing more standalone lore without unlock/source/spoiler mapping; mixing website-safe text with spoiler truth; relying on chat memory.
Scalability potential: Low tier uses short codex entries. Middle adds source variants. High adds website/internal versions. Ultra adds route telemetry and multi-stage evidence chains while sharing one canon graph.
Hardware Impact: Documentation-only now, 0 us/frame. Future implementation should bake content into static data/localization and unlock via events.

Problem: User approved Atlas directive, carrier direction, Deep Reach 2147 cover direction, multi-moon Aegir, and requested Marauder slang plus broader HECTON-8 resources/ecology.
Solution: Lock Atlas as weighted continuity logic, `Black Keel` as automated claim-tender, Deep Reach's 2147 lie as layered cascade/quarantine/log corruption/authorization failure, Aegir as multi-moon system, and Xenon-Omega slang as `blue debt` / `pressure glass` / `XO continuity substrate`. Add resource and flora/fauna atlas entries.
Rejected Alternatives: Evil-AI motive; one magic ore; single-moon Aegir framing; luxury personal ship; one theatrical Deep Reach lie; runtime N-body implications from lore.
Scalability potential: Low tier delivers compact codex/scanner/terminal records and staged visual bodies. Middle adds route/moon maps and resource variants. High adds source-specific records, richer Aegir sky presentation, and ecology variants. Ultra adds orbital spectacle, dense scan variants, and Atlas-altered material presentation without changing truth.
Hardware Impact: Documentation-only now, 0 us/frame. Future delivery must use static localization/data, deterministic seed tables, authored ephemeris/window records, and visual fakes before simulation.
