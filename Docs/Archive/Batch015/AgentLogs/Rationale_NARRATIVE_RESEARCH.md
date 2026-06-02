# NARRATIVE_RESEARCH Rationale

Status: APPLIEDLORE DATA MONOLITH VERIFIED; SCENE TERMINALOS INTEGRATED; WORLD SCENE CORE COUNTERS ADDED; UNITY APEX RE-RUN PENDING

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

Problem: User stated the remaining questions require joint reasoning with pros/cons, not simple answers.
Solution: Create `Docs/Lore/Decision_Workbench.md` with option matrices for Black Keel ownership, Aegir moon naming/geology approach, blue debt behavior, false endings, and personal motive. Keep recommendations as working bias, not final lock.
Rejected Alternatives: Forcing immediate canon choices; leaving all reasoning only in chat; over-locking personal backstory before user control.
Scalability potential: Low tier uses compact codex and contract variants. Middle adds source-specific terminal evidence. High adds route/moon visual detail and false-ending sequences. Ultra adds richer orbital consequence presentation without changing decision truth.
Hardware Impact: Documentation-only now, 0 us/frame. Future implementation must use static data, quest flags, authored windows, and event-driven endings.

Problem: User approved continuing the deeper synthesis and requested full development of these branches.
Solution: Promote hybrid Black Keel ownership, detailed Aegir moon catalog, blue debt containment/signal behavior, minimum false-ending families, and player motive arc into working lore locks. Add dedicated encyclopedia entries and update binding docs.
Rejected Alternatives: Treating Black Keel as purely neutral or purely Deep Reach; live N-body moon simulation; blue debt as supernatural artifact; false endings as fake fail screens; player as explicit ex-Deep-Reach from the start.
Scalability potential: Low tier exposes through compact PDA, route windows, scanner records, and terminal text. Middle adds moon maps, contract variants, and sample behavior states. High adds orbital/sky staging, richer false-ending sequences, and source-specific evidence chains. Ultra adds spectacle and sensory density only.
Hardware Impact: Documentation-only now, 0 us/frame. Future runtime must use authored ephemeris tables, deterministic quest flags, static localization, and event-triggered state changes.

Problem: User clarified that lore files are not bureaucracy; they must become hooks for future site/wiki/in-game notes and support a broad language roster.
Solution: Add `Lore_Localization_Model.md` and update the lore content system/template so every mature article has Article ID, Loc Namespace, canon owner, runtime layer, content targets, source voices, spoiler level, unlock route, and localization status. Map user labels to production locale IDs.
Rejected Alternatives: Loose prose articles; runtime language names based on chat shorthand; translating LocIDs; treating website/wiki copy as separate canon.
Scalability potential: Low tier reads compact pre-baked codex/scanner strings. Middle adds terminal and dossier variants. High adds source-voice variants and website/archive exports. Ultra adds richer VO/transcript/visual delivery while using the same stable content IDs.
Hardware Impact: Documentation-only now, 0 us/frame. Future runtime impact should remain static hash lookup and streamed Narrative pools; no string-key dictionary access, runtime translation generation, or hot-path layout discovery.

Problem: User rejected over-focus on locale naming/legal abstraction and demanded practical world material while preserving Zero-GC/DoD mandates.
Solution: Add gameable world packets and a multilingual content architecture that separates human authoring files from runtime data. The intended path is build-time bake to static packet IDs, LocID hashes, enums, offsets, unlock flags, seed tags, and string-pool records.
Rejected Alternatives: Runtime lore interpreter; markdown parsing during play; generating story text at runtime; scene scanning for lore; making seeds or language change canon truth.
Scalability potential: Low tier shows short scanner/codex records. Middle adds terminal and field-note variants. High adds VO, richer UI, and environmental dressing. Ultra adds dense archive/website/VO layers. The same packet IDs and truth stay fixed.
Hardware Impact: Documentation-only now, 0 us/frame. Future runtime cost should be event-driven lookups and bounded UI rendering; no hot allocations from text construction or content discovery.

Problem: Approved lore still needed usable game-world units for designers, UI, scanner/codex, localization, and endings.
Solution: Add concrete content packs, source text bank, POI/unlock matrix, HECTON-8 field atlas, human/ship/Aegir texture docs, resource gameplay catalog, and final payload map.
Rejected Alternatives: Treating lore as encyclopedia-only; burying first-hour content inside large prose; making resources generic crafting loot; making final choice an abstract morality selector.
Scalability potential: Low tier can use core packet strings and sparse POIs. Middle adds more source fragments and alternate POI variants. High adds VO and stronger environmental dressing. Ultra adds sky detail, dense scan variants, richer archives, and visual overkill while preserving the same IDs/unlocks.
Hardware Impact: Documentation-only now, 0 us/frame. Future runtime should load compact packet tables, unlock flags, and localized string pools; seed variation controls placement/context, not truth.

Problem: User clarified that source docs are insufficient; content must exist as directly usable game/wiki/site/localization material, not just internal planning.
Solution: Create `AppliedContent` with RS001 First Descent: multilingual packet JSON for P001-P005, EN/RU in-game wiki pages, EN/RU external site pages, image briefs, and release manifest. Validate JSON files.
Rejected Alternatives: One sample only; internal docs only; relying on future runtime interpreter; leaving translations implied.
Scalability potential: Low tier uses scanner/wiki strings from packets. Middle adds terminal/audio text. High adds image briefs and richer external site material. Ultra adds additional localized long-form pages and VO variants using the same packet IDs.
Hardware Impact: Authoring/export only, 0 us/frame. Future game build should bake packet JSON into static records and localized string pools.

Problem: RS001 proved the applied content shape, but mid/deep descent and ending structure were still not release-facing.
Solution: Add RS002 Deepening Descent with structured packets P006-P010, all target locale fields, EN/RU in-game wiki pages, EN/RU external site pages, and release image brief. Update AppliedContent, implementation notes, and article index.
Rejected Alternatives: Making only one sample packet; keeping P006-P010 in planning docs; using runtime markdown or translation generation; leaving final payload logic as abstract ending prose.
Scalability potential: Low tier can ship scanner/wiki strings and sparse thumbnails. Middle adds terminals and route POIs. High adds VO subtitles, image cards, and richer site copy. Ultra adds dense art variants and source-voice archives without changing packet IDs or gameplay truth.
Hardware Impact: Authoring/export only, 0 us/frame. Future runtime should use baked packet IDs, LocID hashes, localized string-pool offsets, unlock flags, and asset references.

Problem: Hand-authored multilingual JSON is fragile and a broken comma or quote would block future bake/import.
Solution: Validate all applied packet JSON and release manifests through `python -m json.tool` after RS002 additions.
Rejected Alternatives: Treating markdown readability as enough; validating only new files; waiting for a future importer to catch syntax errors.
Scalability potential: Validation has no runtime tier impact. It protects the content bake path that will feed static packet tables and string pools.
Hardware Impact: 0 us/frame. Editor/build-time validation only.

Problem: The applied content covered first descent and deep descent, but the broader human-space hard-sci-fi context was still mostly source texture rather than release-facing content.
Solution: Add RS003 Human Space / Aegir Route with packets P011-P015, all target locale fields, EN/RU in-game wiki pages, EN/RU external site pages, release manifest, and image brief. Focus on object-level domain marks, relay delay, no-FTL ship classes, Aegir windows, and Black Keel ledger logic.
Rejected Alternatives: Turning setting context into a lore lecture; leaving ships and domains as internal docs; using FTL or instant rescue; making Black Keel a personal hero ship.
Scalability potential: Low tier uses compact scanner/wiki strings and static route-window UI. Middle adds terminal records and timed sky states. High adds VO subtitles, richer map plates, and carrier interface cards. Ultra adds dense transit archive visuals, orbital sky detail, and image variants without changing packet IDs or truth.
Hardware Impact: Authoring/export only, 0 us/frame. Future runtime should use baked packet IDs, LocID hashes, route-window IDs, and string-pool offsets.

Problem: A full AppliedContent sanity pass showed RS001 was syntactically valid but structurally older than RS002/RS003: no manifest `packet_sources` and P001 used specific field names without standard surface aliases.
Solution: Normalize RS001 by adding `packet_sources` and standard P001 `scanner`, `terminal`, and `audio` aliases while preserving the original detailed fields. Validate 3 manifests / 15 packets and all JSON files.
Rejected Alternatives: Keeping validation per-release only; forcing a future importer to special-case P001; deleting the detailed P001 fields and losing richer source text.
Scalability potential: No presentation-tier impact. This stabilizes the future bake path for Low/Middle/High/Ultra because every release set now exposes the same required surface contract.
Hardware Impact: 0 us/frame. Build/import hygiene only.

Problem: AppliedContent existed as release-facing authoring material, but gameplay still could not consume it through DataMonolith, PDA, scanner, or terminal routes.
Solution: Add a build-time importer from AppliedContent manifests to a DataMonolith CSV, generate stable packet/locale hash constants, add `H8AppliedLorePacketRecord` as a 128-byte explicit-layout DTO, bake section `27`, and expose zero-allocation lookup through `H8StaticDataArena` / `H8AppliedLoreRuntime`.
Rejected Alternatives: Runtime JSON parsing, runtime markdown parsing, UI-owned lore dictionaries, one CSV maintained by hand, or deferred localization generation in gameplay.
Scalability potential: Low uses scanner title and compact PDA text. Middle adds terminal/audio surfaces. High adds richer PDA/wiki pages and more authored packets. Ultra adds more VO/image/site surfaces while preserving the same packet hashes, locale hashes, offsets, and unlock truth.
Hardware Impact: Runtime lookup is a bounded binary search over current 225 records plus a UTF-8 span slice; expected under 1 us per event/UI request on low-end silicon. No per-frame lore interpreter, no string allocation, no scene search.

Problem: Static localization enumeration would have exposed the same AppliedContent packet hash once per locale, creating duplicate title aliases for generic hash lookup.
Solution: Keep generic static localization alias enumeration to the default `en_US` title while PDA/runtime consumers resolve explicit `LocaleHash` records.
Rejected Alternatives: Returning 15 title aliases with identical key hashes; adding locale-specific key mutation that would break stable packet identity.
Scalability potential: More locales can be added without multiplying generic alias ambiguity. Locale-specific surfaces remain direct packet+locale records.
Hardware Impact: Saves duplicate alias iteration and keeps lookup deterministic. Runtime frame cost remains event-driven only.

Problem: Terminal delivery has two different surfaces: full localized terminal text and very short diegetic TerminalOS screen text.
Solution: `MessageTerminal` exposes span-copy APIs for full title/terminal/audio surfaces; `TerminalOsRuntime.TrySetTerminalAppliedLoreLine` writes a bounded 30-byte UTF-8 preview into `FixedString32Bytes` terminal state.
Rejected Alternatives: Forcing long localized prose into the 32-byte TerminalOS DTO; building strings for terminal UI; changing TerminalOS ABI for this lore pass.
Scalability potential: Low uses short preview lines; Middle/High/Ultra can show the full terminal body through dedicated UI/subtitle surfaces using the same baked packet record.
Hardware Impact: Preview write is explicit/event-driven and bounded by `MaxFixedStringPayloadBytes`; full text copy writes caller-provided spans only.

Problem: Unity project verification is polluted by unrelated editor/runtime errors and long-running Editor dotnet work.
Solution: Verify this task by importer run, generated CSV sanity, DataMonolith binary section parse, targeted script validation where the validator is reliable, and Unity Console absence of C# errors from this change. Record unrelated Burst entrypoint/editor-readiness errors as a full-runtime blocker.
Rejected Alternatives: Claiming whole-project runtime green; launching another external dotnet build while Unity Editor dotnet was already active; editing unrelated rendering/bootstrap files from the narrative/data domain.
Scalability potential: No content-tier impact. The route is ready for more packets once unrelated runtime blockers are handled by their owners.
Hardware Impact: No runtime cost from verification. Baked blob size is 1,290,688 bytes after section 27.

Problem: AppliedContent records were readable from DataMonolith, but there was still no reliable gameplay unlock ingress from POI/scanner/terminal authoring to PDA selection.
Solution: Add `H8AppliedLoreRuntime.TryRaisePacketUnlocked` as the single first-party packet unlock route over `LoreFragmentScannedSignal`. Connect direct `NarrativeDiscovery`, native AUP POI presentation, staged `ScannableFragment`, and `MessageTerminal` access to this route.
Rejected Alternatives: Having PDA subscribe to all `NarrativeEvents.DiscoveryMade` payloads and accidentally unlock/select non-article discovery hashes; adding UI-owned dictionaries; using runtime string packet ids; scanning scene components for content.
Scalability potential: Low tier receives compact PDA/wiki unlocks from the same event. Middle adds staged scan articles and terminal bodies. High adds VO/subtitles and richer PDA presentation. Ultra adds more packet surfaces and visual dressing without changing hash ownership or DTO layout.
Hardware Impact: 0 us/frame. Runtime work happens only on interaction/scan threshold/POI trigger and is one bounded signal push plus the existing PDA unlock bit set. Expected cost is below 1 us per event on low-end silicon; no per-frame interpreter or content polling.

Problem: AUP narrative POIs bypass `NarrativeDiscovery.Interact`, so direct component fields alone would miss auto-triggered discoveries.
Solution: Reuse the existing spare/pad slot in `NarrativeSpatialTriggerAuthoring` and `NarrativePoiPresentationDTO` as `AppliedLoreHash`, preserving struct sizes while carrying packet identity through the native POI trigger registry.
Rejected Alternatives: Expanding DTO sizes without need; looking up MonoBehaviours during POI dispatch; publishing a second string-based narrative event; requiring every AUP POI to also be a direct interactable.
Scalability potential: Low tier uses one hash in the POI presentation DTO. Middle/High/Ultra can add richer sky/waypoint/card presentation after the same trigger without changing truth ownership.
Hardware Impact: No layout growth and no scene search. Added cost is one uint copy during cold POI registry rebuild and one event push on trigger.

Problem: Runtime AppliedContent integration had targeted validation, but no single offline proof that CSV, generated constants, source-route symbols, and `static_data.h8bin` agreed byte-for-byte.
Solution: Add `Tools/AppliedLoreRuntimeAudit.py` to parse the authoring CSV, generated constants, DataMonolith header/directory/section table, AppliedLore section, localization UTF-8 pool, and required route symbols from importer through PDA/scanner/terminal/POI consumers. The audit verifies packet+locale sort order, hashes, record indices, surface masks, bounds, NUL terminators, exact text bytes, and that critical source route symbols still exist.
Rejected Alternatives: Re-running Unity refresh while another agent owns the Editor; accepting section count as proof; validating only packet/locale pairs; writing a managed runtime diagnostic path; relying on manual grep after every content bake.
Scalability potential: Low tier gets the same baked compact surfaces. Middle/High/Ultra can add more packets, VO/site/wiki surfaces, and image references while the audit keeps packet identity and string-pool slices deterministic.
Hardware Impact: 0 us/frame. Offline tool only. It protects the under-1 us/event runtime lookup route by catching bake drift before gameplay.

Problem: The AppliedLore route exists in code and blob data, but actual scene/prefab serialized packet assignments could still be absent.
Solution: Extend the offline audit to count non-zero serialized `appliedLore...Hash` / `AppliedLoreHash` fields in `.unity`, `.prefab`, and `.asset` files. Current result is `scene_bindings=0`, so this is a real authoring gap, not a runtime route gap.
Rejected Alternatives: Silently assuming components are assigned; editing Unity scene assets while another agent owns the Editor; making the audit fail before any concrete scene binding pass exists.
Scalability potential: Low tier can bind one packet per critical POI. Middle can bind staged scan packets. High/Ultra can add terminal/VO/presentation variants without changing packet identity.
Hardware Impact: 0 us/frame. Serialized bindings add no polling; they only provide packet hashes consumed by existing event-triggered routes.

Problem: Scene binding needs concrete packet hashes, but Unity inspector assignment cannot rely on chat memory or generated C# constants alone.
Solution: Add `Docs/Lore/AppliedContent/binding_maps/RS001_RS003_runtime_binding_map.csv` with all 15 packet IDs, hex hashes, decimal uint hashes, component fields, suggested POI targets, and unlock moments. Extend the audit to validate the map against baked packet IDs and FNV hashes.
Rejected Alternatives: Manual hash copying from `H8AppliedLoreHashes.cs`; scene assignment without a source map; unvalidated spreadsheet drift.
Scalability potential: Low tier can bind one packet per critical location. Middle can add staged scan fields. High/Ultra can expand the same map format with VO/image/presentation references.
Hardware Impact: 0 us/frame. This is authoring source only and does not create runtime lookup or polling.

Problem: AppliedContent packets were baked and bindable, but the story flow still lacked a machine-checkable evidence chain. Prose page order cannot safely drive replayable discovery, partial endings, or PDA/wiki linking.
Solution: Add `Docs/Lore/AppliedContent/graphs/RS001_RS003_evidence_graph.csv` and validate it in `Tools/AppliedLoreRuntimeAudit.py`. The graph links every baked packet to arcs, depth bands, route moments, prereqs, next leads, evidence types, truth claims, player decisions, spoiler tiers, and primary display surfaces.
Rejected Alternatives: Runtime quest/evidence graph parsing; deriving sequence from markdown; adding managed dictionaries in gameplay; editing Unity scene bindings while another agent owns the Editor.
Scalability potential: Low tier can expose only the primary surface and one next lead. Middle can add optional fragments and alternate POI placement. High can add source-voice variants and richer PDA crosslinks. Ultra can add VO/image/site layers and denser evidence presentation while preserving packet IDs and truth graph.
Hardware Impact: 0 us/frame. This is offline authoring validation. Future runtime should bake graph-derived flags/route gates into static data and fire unlocks by existing signals; no per-frame graph walk or scene search.

Problem: AppliedContent packet JSON contained localized strings for all target locales, but publication folders only had EN/RU markdown pages. The user needs actual in-game wiki and external-site fragments ready across the language roster, not just future translation intent.
Solution: Add `Tools/AppliedLorePageExporter.py` to generate missing localized markdown pages from packet JSON while preserving hand-authored pages unless `--overwrite` is explicit. Extend the audit to require page coverage for every baked packet/locale pair.
Rejected Alternatives: Manual copy/paste for 390 missing pages; overwriting EN/RU authored pages; treating packet JSON as enough for external publication; runtime markdown parsing in the game.
Scalability potential: Low tier ships only baked DataMonolith packet strings. Middle uses exported wiki/site fragments. High adds richer hand-authored overrides. Ultra adds VO/image/site variants; all keep packet IDs and locale IDs stable.
Hardware Impact: 0 us/frame. The exporter is offline. Runtime still reads DataMonolith UTF-8 slices by packet hash, locale hash, and surface enum.

Problem: The evidence graph proved packet-to-packet truth flow, but it did not yet express player-facing route beats, depth gating, replay variation, or false-exit pressure as a checked source artifact.
Solution: Add `Docs/Lore/AppliedContent/route_cards/RS001_RS003_route_cards.csv` with 9 route cards covering all 15 baked packets. Each card ties packet groups to phase, depth bounds, required packets, display surface, world-object hint, player question, truth payload, replay axis, and ending-pressure category. Extend `Tools/AppliedLoreRuntimeAudit.py` to validate route-card coverage and references.
Rejected Alternatives: More prose-only scenario notes; deriving route beats from wiki pages; runtime CSV parsing; adding a managed quest graph; changing DataMonolith schema while Unity is concurrently active.
Scalability potential: Low tier can show one compact route hint and one PDA packet per card. Middle can use multiple POI variants per card. High can add VO, images, and richer PDA crosslinks. Ultra can add dense route-window visuals and false-exit presentation while keeping the same packet hashes and route ids.
Hardware Impact: 0 us/frame in this pass. Future runtime route bake should use fixed records and event/UI-triggered lookup only, expected below 1 us per query on low-end silicon.

Problem: Localized packet pages existed, but wiki/site folders had no generated localized indexes. That makes the content less usable for publication and manual review even though the packet pages are present.
Solution: Extend `Tools/AppliedLorePageExporter.py` to write localized `INDEX.md` files for every target locale under both `in_game_wiki` and `external_site`, using localized packet titles from packet JSON. Extend `Tools/AppliedLoreRuntimeAudit.py` to validate that every index exists and references every baked packet id.
Rejected Alternatives: Hand-maintaining 30 index pages; putting index logic in runtime UI; leaving external publication structure as loose files.
Scalability potential: Low tier ignores markdown and reads static DataMonolith records. Middle can expose generated indexes in a debug/content browser. High can hand-polish selected locale indexes. Ultra can add art/VO crosslinks while packet ids remain stable.
Hardware Impact: 0 us/frame. Offline export only; runtime still reads baked packet records and string-pool slices.

Problem: Route cards were checked authoring data, but a future compiler would still need either to parse writer-facing CSV/prose or rely on hand-copied route hashes.
Solution: Add `Tools/AppliedLoreRouteCardExporter.py` and generate `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv` with route, phase, packet, prerequisite, surface-mask, and ending-pressure hashes. Extend the audit to compare the export back to route-card authoring rows and baked packet ids.
Rejected Alternatives: Direct runtime parsing of route-card CSV; hand-maintained hash tables; changing `static_data.h8bin` under concurrent Unity Editor work; adding a managed runtime route interpreter.
Scalability potential: Low tier can bake compact route rows for simple PDA/quest linking. Middle can add optional POI variants. High/Ultra can layer richer presentation and route-window visuals using the same route ids and packet hashes.
Hardware Impact: 0 us/frame now. Future baked route lookup should stay a fixed-record query, not a string/prose parser.

Problem: Route-card source data existed, but route cards were not proven as active runtime static data inside `static_data.h8bin`.
Solution: Add section `H8DataSectionId.AppliedLoreRoutes` (`28`) and `H8AppliedLoreRouteRecord` as a 128-byte explicit-layout DTO. Connect `applied_lore_route_cards.csv` to `H8DataMonolithCompiler`, sort by `RouteCardHash`, assign `RecordIndex`, validate packet/prerequisite references against baked AppliedLore packets, append the route section to the blob, and expose lookup through `H8StaticDataArena` plus `H8AppliedLoreRuntime`.
Rejected Alternatives: Runtime route-card CSV parsing; managed route dictionaries; storing route-card prose in gameplay state; changing packet truth per locale; hand-maintained route hash arrays.
Scalability potential: Low tier can query one compact route card for PDA/terminal hints. Middle can expose alternate POI variants. High and Ultra can add VO, route-window visuals, orbital/corporate pressure presentation, and richer site/wiki crosslinks using the same route hashes and packet hashes.
Hardware Impact: 0 us/frame. Route lookup is event/UI-triggered fixed-record access over 9 route records. Expected cost remains below 1 us/query on low-end silicon; no per-frame route graph walk, no string parser, no scene search.

Problem: A static-data section change without blob-level proof would be indistinguishable from source-only integration.
Solution: Extend `Tools/AppliedLoreRuntimeAudit.py` to require section `28`, validate 128-byte route records, compare route hashes, phase hashes, depth bounds, surface masks, ending-pressure hashes, packet hash slots, prerequisite slots, record indices, unused slot zeros, and reserved padding zeros against the source-data export.
Rejected Alternatives: Trusting `dotnet build` alone; checking only row count; accepting source CSV as proof; relying on Unity Editor while another agent and active `dotnet` are running.
Scalability potential: Audit remains offline and scales with packet/route count. Runtime still consumes immutable records and localized packet text from the monolith.
Hardware Impact: 0 us/frame. Current audited blob: `blob_bytes=1291840`, `applied_records=225`, `applied_routes=9`, `scene_bindings=0`.

Problem: Route-card and audit tooling still assumed a single RS001-RS003 authoring source, which would drop future release-set route cards, graphs, or binding rows from validation/export.
Solution: Convert route-card export and audit validation to glob all `*_route_cards.csv`, `*_evidence_graph.csv`, and `*_runtime_binding_map.csv` files. Add duplicate route/packet coverage checks across the combined source set.
Rejected Alternatives: Adding RS004/RS005 special cases; merging all release rows into one growing CSV; letting the compiler see fewer routes than the lore authoring layer.
Scalability potential: Low tier still reads compact baked route records. Middle/High/Ultra can add more packets, route cards, VO, and publication surfaces per release set without changing runtime lookup ownership.
Hardware Impact: 0 us/frame. Offline source/export cost only. Runtime remains fixed-record lookup after bake.

Problem: Aegir system physics, HECTON-8 geology/resources/ecology, carrier custody, capsule stranding, current Deep Reach pressure, false material exit, and player motive were strong lore decisions but not yet usable by PDA/scanner/terminal/wiki/site routes.
Solution: Add RS004 P016-P020 and RS005 P021-P025 as AppliedContent packets with 15 locales, required surfaces, manifests, image briefs, evidence graphs, route cards, and binding maps. Regenerate packet CSV, hash constants, publication pages, indexes, and route-card source rows.
Rejected Alternatives: More internal markdown, one-language notes, runtime translation generation, runtime markdown interpretation, or unbound encyclopedia prose.
Scalability potential: Low tier can expose scanner/terminal/wiki strings and one route card per beat. Middle adds staged scan surfaces and more POI placements. High adds VO subtitles/image cards. Ultra adds dense orbital/corporate/abyss presentation using the same packet hashes and route ids.
Hardware Impact: 0 us/frame until content is unlocked. After bake, lookups remain event/UI-triggered binary searches over fixed 128-byte records and UTF-8 string-pool spans; expected cost remains under 1 us per request on low-end silicon at this content scale.

Problem: Full audit correctly fails after RS004/RS005 because the source CSV advanced to 375 rows while `static_data.h8bin` still contains 225 packet records and 9 route records.
Solution: Add `--source-only` audit mode to verify authoring/export/page/route consistency without claiming blob readiness, and block the DataMonolith bake until CPU/dotnet contention clears per project rules.
Rejected Alternatives: Running `dotnet` or the DataMonolith bake while CPU is above 50% and Unity `dotnet` is active; hiding stale blob mismatch; editing Unity scene YAML while another agent owns Unity work.
Scalability potential: Source-only validation keeps release-set authoring moving while runtime truth remains fail-closed until a fresh monolith bake proves 375 packet rows and 19 route records.
Hardware Impact: 0 us/frame. Current source-only proof: `packets=25 locales=15 rows=375 binding_map_rows=25 graph_rows=25 route_cards=19 route_source_rows=19 wiki_pages=375 site_pages=375 index_pages=30 scene_bindings=0`. Full runtime blob update remains pending.

Problem: Humanity scale, Barnard Yards origin, no-FTL rescue math, Seed Ship doctrine, and relay debt were locked or strongly implied in lore docs, but not available as runtime-bakeable, localized player-facing content.
Solution: Add RS006 P026-P030 as AppliedContent packets with manifests, localized surfaces for all 15 target locales, image briefs, evidence graph, route cards, and binding map. Regenerate AppliedLore packet CSV, route-card CSV, generated hash constants, wiki/site pages, and localized indexes.
Rejected Alternatives: More internal prose only; one-language notes; leaving broad setting context as website-only; runtime translation generation; runtime markdown parsing; using a live orbital/interstellar simulation to explain delayed rescue.
Scalability potential: Low tier can expose compact scanner/terminal/wiki text and route-card hints. Middle can add alternate POI placement and staged scan unlocks. High can add VO, image cards, richer PDA crosslinks, and stronger terminal presentation. Ultra can add orbital/relay/corporate sensory overload while preserving the same packet hashes, route ids, and truth state.
Hardware Impact: 0 us/frame until content unlock. After bake, runtime still performs event/UI-triggered fixed-record lookups and UTF-8 string-pool span reads. Current source-only proof after RS006: `packets=30 locales=15 rows=450 binding_map_rows=30 graph_rows=30 route_cards=24 route_source_rows=24 wiki_pages=450 site_pages=450 index_pages=30 scene_bindings=0`.

Problem: Full DataMonolith audit must remain fail-closed after RS006 because the active blob still contains 225 AppliedLore packet records while source now contains 450 rows.
Solution: Run and record the full audit failure: `AppliedLore count mismatch: blob=225 csv=450`. Keep bake blocked until CPU is below threshold and no active compiler/dotnet contention exists.
Rejected Alternatives: Claiming runtime readiness from source-only audit; launching `dotnet` while CPU was 52% and Unity `dotnet` was active; mutating scene/prefab YAML while another Unity agent is active.
Scalability potential: The source layer can keep growing through release-set CSV/JSON and offline audits; runtime truth remains stale by design until a new blob proves the exact packet/route counts.
Hardware Impact: 0 us/frame. No runtime binary changed in this pass.

Problem: Depth progression, shallow contrast, cable reef merge, repair fauna, and bottom factory-temple threshold were still mostly planning lore while the player-facing runtime content layer needed concrete packets.
Solution: Add RS007 P031-P035 with all 15 locales, scanner/field/terminal/audio/wiki/site surfaces, image brief, evidence graph, route cards, and binding map. Regenerate AppliedLore packet CSV, route-card CSV, generated hash constants, wiki/site pages, and localized indexes.
Rejected Alternatives: More internal markdown; runtime translation generation; runtime markdown parsing; explaining depth gates through UI-only level locks; simulating full ecology/pressure truth for lore delivery.
Scalability potential: Low tier exposes compact scanner/terminal/PDA text and strong silhouette image cards. Middle adds alternate POI placement and route-card variants. High adds VO, richer scanner cards, and fauna/cable reef art. Ultra adds dense silt, repair tissue, pressure audio, and factory-threshold spectacle without changing packet hashes or gameplay truth.
Hardware Impact: 0 us/frame until content unlock. After bake, runtime remains event/UI-triggered fixed-record lookup and UTF-8 string-pool span reads. Current source-only proof after RS007: `packets=35 locales=15 rows=525 binding_map_rows=35 graph_rows=35 route_cards=29 route_source_rows=29 wiki_pages=525 site_pages=525 index_pages=30 scene_bindings=0`.

Problem: Full DataMonolith audit must remain fail-closed after RS007 because the active blob still contains 225 AppliedLore packet records while source now contains 525 rows.
Solution: Run and record the full audit failure: `AppliedLore count mismatch: blob=225 csv=525`. Keep bake blocked until CPU is below threshold.
Rejected Alternatives: Claiming runtime readiness from source-only audit; launching DataMonolith bake while CPU samples were 72.6/80.4/66.0%; mutating Unity scene/prefab YAML while scene ownership is unclear.
Scalability potential: Source content can continue through checked release sets, but runtime truth remains stale until a new monolith proves 525 packet rows and 29 route rows.
Hardware Impact: 0 us/frame. No runtime binary changed in this pass.

Problem: Escape, partial endings, material false success, present-tense Deep Reach pressure, and the final Atlas ethical question were still too much in planning notes and not enough in runtime-bakeable content.
Solution: Add RS008 with P036-P040: return-vector window, coward exit chain, material payout ledger, Deep Reach cleanse order, and Atlas final argument. Each packet has all 15 locales, scanner/field/terminal/audio/wiki/site surfaces, image brief, evidence graph, route cards, and binding map.
Rejected Alternatives: Treating early exit as fake game-over; making material payout a pure menu consequence; leaving Deep Reach as only historical villain; making Atlas final choice a boss-core cliché or ownership-only question.
Scalability potential: Low tier exposes compact terminal/PDA text and one ending-pressure route. Middle adds alternate repair/sample/order placement. High adds VO, image cards, richer terminal presentation, and post-ending dossier. Ultra adds orbital timing spectacle, corporate proxy voice layering, and factory-temple choice presentation without changing packet hashes or route truth.
Hardware Impact: 0 us/frame until content unlock. After bake, runtime remains event/UI-triggered fixed-record lookup and UTF-8 string-pool span reads. Current source-only proof after RS008: `packets=40 locales=15 rows=600 binding_map_rows=40 graph_rows=40 route_cards=34 route_source_rows=34 wiki_pages=600 site_pages=600 index_pages=30`.

Problem: First RS008 route-card export failed because two draft prerequisite IDs did not match the actual packet table.
Solution: Replace `P004_BLUE_DEBT_SAMPLE` with `P004_BLUE_DEBT` and `P024_MATERIAL_EXIT_PROTOCOL` with `P024_FALSE_EXIT_MATERIAL`, then rerun route-card export and full source-only audit.
Rejected Alternatives: Adding duplicate alias packets; weakening audit checks for unknown references; letting route cards drift from packet identity.
Scalability potential: Packet identity remains one fact, one owner, one route. Future releases can add aliases only as explicit packets or route metadata, not silent names.
Hardware Impact: 0 us/frame. This is offline authoring validation only; it prevents broken route hashes from entering the monolith.

Problem: The active blob changed under concurrent work: the full audit now reports `blob=525 csv=600`, proving RS007 packet rows are in the blob while RS008 source is not.
Solution: Record this as observed state, not as this agent's bake. Keep RS008 fail-closed until a fresh DataMonolith bake proves 600 packet rows and 34 route rows.
Rejected Alternatives: Claiming RS008 runtime readiness from source-only audit; claiming credit for a bake not run by this agent; launching a bake while an active `dotnet` process exists.
Scalability potential: The audit remains the authority under multi-agent concurrency. Source can grow safely; runtime readiness requires exact blob count and route-record proof.
Hardware Impact: 0 us/frame. No runtime binary was rebuilt by this agent in this pass.

Problem: Drowned colony layout existed as content-pack intent, but not as release-grade runtime/site/wiki content. Without it, the player's personal motive and colony-human texture stayed too abstract.
Solution: Add RS009 P041-P045 as AppliedContent packets: worker locker row, pressure bunk routine, shift-board route holds, medical lock delay, and black-box name stack. Each packet has 15 locales, scanner/field/terminal/audio/wiki/site surfaces, image brief, evidence graph, route card, and binding-map row. Add `HECTON8_Colony_Layout.md` as a curated encyclopedia bridge.
Rejected Alternatives: More internal markdown; corpse-focused colony horror; single example packet; one-language content; runtime markdown/JSON interpretation; runtime translation generation.
Scalability potential: Low tier exposes compact scanner/terminal/PDA text and one strong prop per route. Middle adds alternate room placement and seed-varying readable names. High adds VO/image cards and denser terminal replay. Ultra adds flooded module spectacle, layered audio, name-stack broadcast presentation, and richer archive UI without changing packet hashes or route truth.
Hardware Impact: 0 us/frame until unlock. After bake, runtime remains event/UI-triggered fixed-record lookup and UTF-8 string-pool span reads. Source proof: `packets=45 locales=15 rows=675 binding_map_rows=45 graph_rows=45 route_cards=39 route_source_rows=39 wiki_pages=675 site_pages=675 index_pages=30`.

Problem: RS009 source expansion needed runtime proof, but launching `dotnet build` or manual bake would violate project resource rules under concurrent work.
Solution: Run only existing offline Python importer/exporters/audits. The full AppliedLore audit now passes against the active `static_data.h8bin`: `applied_records=675`, `applied_routes=39`, `blob_bytes=1652736`, `localization_bytes=497790`. This proves the active blob is current for RS009 without requiring a manual build from this agent.
Rejected Alternatives: Claiming source-only readiness; running `dotnet build`; editing scenes to force bindings; ignoring active multi-agent changes to the blob.
Scalability potential: Exact blob audit remains the authority. More release sets can append packet/route rows while runtime consumers stay unchanged.
Hardware Impact: 0 us/frame. Runtime data is baked; lookup cost remains bounded fixed-record access. Authoring gap remains `scene_bindings=0`.

Problem: User rejected slow marketing/social work and redirected the task back to lore integration. Existing AppliedContent was runtime-verified through RS009, but the next useful narrative work needed to move toward concrete gameplay-facing pressure, machinery, and return-route surfaces instead of more abstract setting prose.
Solution: Add RS010 `PRESSURE_MACHINERY_RETURN_ROUTE` with P046-P050: pump-room handshake, hatch seal ledger, cable splice scar, sonar return route, and salvage tool custody. Regenerate AppliedLore source CSV, route-card source CSV, generated packet constants, localized wiki/site pages, and indexes. Validate source-only and run full audit to prove the active blob is stale rather than silently claiming runtime readiness.
Rejected Alternatives: More marketing posts; broad lore essays; runtime markdown/JSON parsing; runtime translation generation; scene/prefab YAML mutation under concurrent Unity work; claiming RS010 DataMonolith readiness before the blob contains 750 records and 44 routes.
Scalability potential: Low tier can expose compact scanner/terminal/PDA strings and one strong prop per route. Middle adds alternate POI placement and seed-varying prop order. High adds VO/image cards, richer terminal presentation, sonar overlays, and wet machinery panels. Ultra adds dense pump-room VFX, pressure audio, route-degradation overlays, and salvage custody presentation without changing packet hashes or route truth.
Hardware Impact: 0 us/frame until unlock. Runtime remains event/UI-triggered fixed-record lookup after bake. Source-only proof: `packets=50 rows=750 route_cards=44 scene_bindings=0`. Full audit currently fails correctly: `AppliedLore count mismatch: blob=675 csv=750`. Bake was not launched: CPU preflight returned `cpu_avg=99.9` with active Unity `dotnet.exe` PID `16784`.

Problem: The audit still reports `scene_bindings=0`, and the next assignment pass would be slow and risky if it starts by searching the whole project in Unity. RS010 needs concrete binding candidates, but raw `.prefab`/`.unity` edits are not acceptable under the current project rules.
Solution: Add `RS010_scene_binding_targets.csv` as a Unity-safe worklist. Map P046-P050 to first-party candidates: service pump module/proxy, airlock hatch data, service scar strip/proxy, sonar amplifier/beacon assets, and salvage sampler/tool custody targets. Keep it outside `*_runtime_binding_map.csv` so the audit does not treat candidate rows as serialized bindings.
Rejected Alternatives: Raw YAML mutation; binding to `Player.prefab` sonar tuning; using third-party generic door/locker assets as primary targets; claiming worklist rows as scene bindings; creating a new terminal prefab without a Unity-safe authoring pass.
Scalability potential: Low tier gets one readable prop per route. Middle can add alternate placements. High can add terminal/image/VO variants. Ultra can add dense machinery/sonar/custody dressing while keeping the same packet hashes and event-triggered unlock route.
Hardware Impact: 0 us/frame. This is authoring backlog only. Source-only audit remains green with `scene_bindings=0`; actual runtime binding proof requires serialized non-zero packet assignments and rerun audit.

Problem: AppliedLore had runtime fields and concrete target backlog rows, but no safe Unity-facing bridge to assign hashes. Manual YAML mutation would violate prefab/scene mutation rules and risk corrupting other agents' Unity work.
Solution: Add `H8AppliedLoreBindingCatalogWindow` as an Editor-only authoring facade. It reads all `*_runtime_binding_map.csv`, enriches rows from `*_scene_binding_targets.csv`, shows packet IDs/hashes/Unity-safe target candidates, assigns hashes through `SerializedObject` to `NarrativeDiscovery`, `ScannableFragment`, and `MessageTerminal`, and validates loaded scene/project prefab bindings against `applied_lore_packets.csv`. Extend `AppliedLoreRuntimeAudit.py` source-route checks so the bridge is mandatory evidence, and validate `*_scene_binding_targets.csv` packet IDs, hashes, components, fields, target cells, and Unity-safe actions.
Rejected Alternatives: Raw `.unity`/`.prefab` YAML edits; runtime CSV/markdown parsing; treating candidate backlog rows as serialized bindings; adding a new runtime route before actual object placement; launching `dotnet build` while CPU stayed 88-94% with `dotnet` PID 16784 active.
Scalability potential: Low tier sees no runtime cost and uses the same compact packet unlocks. Middle can place more route variants. High can add VO/image/terminal variants. Ultra can add dense pressure machinery and sonar presentation without changing packet hashes, route cards, DTO layout, or save truth.
Hardware Impact: 0 us/frame. New code is Editor-only. Runtime remains event/UI-triggered DataMonolith lookup. Current full audit proves RS010 active blob state: `packets=50 rows=750 target_backlog_rows=5 applied_records=750 applied_routes=44 scene_bindings=0`.

Problem: The catalog exposed target rows, but still required manual selection per prefab. Unity MCP was unavailable (`no_unity_session`) and raw YAML was rejected, so the next useful step had to prepare a Unity API batch path without pretending bindings were already serialized.
Solution: Extend the Editor bridge with `Apply Applied Lore Target Backlog To Prefabs`. The applier reads validated target rows, opens prefab candidates with `PrefabUtility.LoadPrefabContents`, selects existing supported components or the first renderer object for safe `ScannableFragment` rows, writes packet hashes through `SerializedObject`, saves changed prefabs through `PrefabUtility.SaveAsPrefabAsset`, and skips manual `MessageTerminal`/`NarrativeDiscovery` cases rather than inventing terminal objects.
Rejected Alternatives: Raw prefab YAML edits; adding `MessageTerminal` to arbitrary construction/tool prefabs without a terminal mesh/status renderer intent; treating ScriptableObject data assets as world POIs; forcing Unity operations while MCP reports no session.
Scalability potential: Low tier remains one compact evidence prop per route. Middle can add multiple prefab placements. High can add terminal/VO/image variants. Ultra can layer dense machinery/sonar effects on the same packet hashes after binding.
Hardware Impact: 0 us/frame. Editor-only apply step. Runtime route stays event-triggered fixed-record lookup. Current proof remains `scene_bindings=0` because the Unity menu has not run.

Problem: The binding target backlog covered only RS010, leaving P001-P045 as runtime-baked content with no concrete Unity assignment worklist. This would force the next Unity pass back into manual project search and would make the audit's `target_backlog_rows=5` misleadingly narrow.
Solution: Add `RS001_RS009_scene_binding_targets.csv` with all P001-P045 mapped to existing first-party prefab/data-asset candidates. Harden `AppliedLoreRuntimeAudit.py` so it validates every candidate path and reports `target_backlog_rows`, `target_prefab_candidate_rows`, `target_auto_prefab_rows`, `target_manual_rows`, and `target_candidate_paths`.
Rejected Alternatives: Treating runtime binding maps as enough; adding fake terminal prefabs; auto-adding `MessageTerminal` or `NarrativeDiscovery` to arbitrary prefabs; relaxing audit to accept missing future candidate paths.
Scalability potential: Low tier receives compact scanner/terminal/PDA unlocks on physical props. Middle can seed alternate placements. High can add VO/image cards and denser terminal variants. Ultra can layer pressure machinery, sonar overlays, ecology spectacle, and final-zone presentation without changing packet hashes, route cards, DTO layout, or save truth.
Hardware Impact: 0 us/frame. Source/full audit proof: `packets=50 rows=750 applied_records=750 applied_routes=44 target_backlog_rows=50 target_auto_prefab_rows=16 target_manual_rows=34 target_candidate_paths=200 scene_bindings=0`. No runtime parser, no hot polling, no YAML mutation.

Problem: Unity MCP automation was blocked by a modal `Applied Lore Bindings` dialog, and the validator still scanned every project prefab. The validator also passed `BindingValidationReport` by value, so its counters could remain zero even after real bindings existed.
Solution: Make static menu methods log-only, keep dialogs only in the manual EditorWindow buttons, pass `BindingValidationReport` by `ref`, and validate only backlog candidate prefabs. Add audit guards against project-wide prefab scans and modal automation menus.
Rejected Alternatives: Relying on Windows focus/OK to clear automation; keeping `AssetDatabase.FindAssets("t:Prefab")` in MCP paths; leaving struct report counters by value.
Scalability potential: Low tier runtime is unchanged. Middle/High/Ultra authoring can add more candidate prefabs without making validation scan unrelated art/content assets.
Hardware Impact: 0 us/frame. Editor validation now reported `prefabs=47` and `bound_fields=16`; runtime still uses event/UI-triggered DataMonolith lookups only.

Problem: First safe prefab apply pass wrote 16 row applications but only 13 final serialized fields because several rows shared the same prefab and ScannableFragment stage field; later rows overwrote earlier non-zero packet hashes.
Solution: Change the applier to reject non-zero different packet hashes and try the next existing prefab candidate. Rerun the Unity menu after refresh. The proof is Unity validation `known_hashes=50 scene_components=0 prefabs=47 prefab_components=14 bound_fields=16 unknown_hashes=0`; later audit counter work reclassified these as prefab authoring bindings, not scene placement.
Rejected Alternatives: Allowing last-write-wins hash overwrite; adding multiple `ScannableFragment` components despite `[DisallowMultipleComponent]`; auto-inventing terminal/POI objects for manual rows; raw YAML semantic edits.
Scalability potential: Low tier gets compact scan unlocks on real props. Middle can seed alternate candidate prefabs. High/Ultra can add richer presentation to the same packet hashes without changing DTO/save truth.
Hardware Impact: 0 us/frame. Fourteen prefabs now carry 16 non-zero AppliedLore scan-stage fields; remaining `target_manual_rows=34` are intentionally left for real terminal/POI authoring.

Problem: The 34 remaining AppliedLore rows were still named only as "manual", which is not actionable enough for scene authors and would invite fake broad prefab binding.
Solution: Add `RS001_RS010_manual_binding_policy.csv` with explicit policy split: 27 terminal-anchor rows and 7 visually marked `NarrativeDiscovery` prop rows. Extend audit output with manual-policy counters and validate packet hashes, component/field consistency, required placement rule, and terminal template path.
Rejected Alternatives: Counting manual rows as acceptable debt without ownership; binding them to generic route-power or ruin-family proxies; creating generic `NarrativeDiscovery` templates that would unlock meaningless discovery IDs.
Scalability potential: Low tier uses compact terminal/PDA text on physical props. Middle adds alternate terminal placements. High adds VO/image cards. Ultra adds richer panel VFX and scene dressing without changing packet hashes, DTO layout, or save identity.
Hardware Impact: 0 us/frame. Policy is editor/source data only; runtime still reads baked AppliedLore records through fixed DataMonolith lookup.

Problem: Terminal rows need physical interactable objects, but no real terminal prefab existed in the current project scan.
Solution: Add `Hecton8/Lore/Create Applied Lore Terminal Anchor Prefab` and create `Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab` through Unity API. The template has a `MessageTerminal`, renderer material/mesh, `statusLightRenderer`, and zero `appliedLorePacketHash` so it cannot falsely unlock content until assigned.
Rejected Alternatives: Raw prefab YAML authoring; runtime terminal factory; assigning terminal hashes to beacon/relay/tool prefabs without a terminal surface.
Scalability potential: Low tier gets a simple physical panel. Middle can place variants. High/Ultra can upgrade material, screen RT, glow, audio, and damage states while preserving the same terminal hash route.
Hardware Impact: 0 us/frame. Prefab is static asset data. Interaction remains event-driven and terminal text uses existing AppliedLore UTF-8/string-pool route.

Problem: The source now contains a generator for 27 terminal-policy prefabs, but Unity MCP did not execute the new menu path during this pass.
Solution: Keep the generator source and audit support, but make generated terminal prefabs optional until the Unity API menu can run. Audit reports `manual_terminal_prefab_rows=0` honestly instead of claiming prefab generation.
Rejected Alternatives: Generating 27 prefab YAML/meta files from shell; failing the full AppliedLore audit on Unity transport state; hiding the failed menu execution.
Scalability potential: Once the generator runs, all terminal policy rows become concrete prefabs ready for authored scene placement. Until then the anchor template and policy prevent false placement.
Hardware Impact: 0 us/frame. Editor transport blocker only; runtime blob remains green with `applied_records=750` and `applied_routes=44`.

Problem: The terminal-policy generator was source-complete but unproven, and the old audit counter would have mislabeled generated prefab hashes as scene placement.
Solution: Run `Hecton8/Lore/Generate Applied Lore Terminal Policy Prefabs` through Unity API after menu registration recovered. Generate 27 terminal prefabs and split audit counters into `scene_bindings`, `prefab_bindings`, `asset_bindings`, and `authoring_bindings`. Update validator source to include generated terminal prefabs without project-wide prefab scans; leave Unity validator re-proof pending until the Editor reload state is stable.
Rejected Alternatives: Shell-generated prefab YAML/meta files; leaving `scene_bindings=43` as a misleading success metric; placing terminal hashes into generic proxy prefabs; running `dotnet build` while Unity `dotnet.exe` is active.
Scalability potential: Low tier gets compact physical terminal panels. Middle can seed terminal variants per route. High adds VO/image-card presentation. Ultra can add richer screen glow, damaged panel VFX, and audio layering on the same packet hashes without changing DTO layout or save identity.
Hardware Impact: 0 us/frame. Generated prefabs are static assets. Runtime content still resolves by event/UI-triggered DataMonolith packet hash lookup. Verified audit: `scene_bindings=0 prefab_bindings=43 asset_bindings=0 authoring_bindings=43`. Unity validator source is updated, but the compiled validator still needs a clean reload proof.

Problem: Manual AppliedLore rows had prefab assets and policy, but no deterministic scene placement route. Without a route, the next step would either raw-edit `.unity` YAML or require scene authors to place 34 objects manually with no auditable plan.
Solution: Add `RS001_RS010_scene_placement_plan.csv` and an Editor-only menu `Hecton8/Lore/Apply Applied Lore Scene Placement Plan`. The route refuses unloaded scenes, instantiates only planned source prefabs, creates a dedicated placement root, assigns hashes through `SerializedObject`, configures `NarrativeDiscovery` display/discovery fields, marks/saves loaded scenes, and keeps automation menu calls log-only. The audit now reports `scene_placement_serialized_rows` so future scene saves must prove exact plan-row coverage, not just any non-zero scene hash.
Rejected Alternatives: Auto-opening `02_HECTON_WORLD` under concurrent Unity work; shell-mutating scene YAML; treating generated terminal prefabs as final world placement; runtime terminal factories; broad scene/prefab scans.
Scalability potential: Low tier sees the same compact physical evidence objects. Middle can move or duplicate placement rows by seed/POI pass. High can add VO, images, terminal glow, and richer prop dressing. Ultra can layer dense scene dressing and diegetic screens on the same packet hashes without changing DTO layout, save identity, or authority route.
Hardware Impact: 0 us/frame. This pass added only Editor authoring and offline audit checks. Runtime remains event/UI-triggered DataMonolith lookup. Scene mutation was not executed because Unity is on `00_BOOTSTRAP`, not `02_HECTON_WORLD`, and the Console has unrelated `CelestialRuntimeSnapshot` compile errors in `OrbitalSkyEphemerisDrift1601EditTests.cs`; current proof after concurrent RS011 source/blob update is `packets=55`, `applied_records=825`, `applied_routes=49`, `scene_bindings=0`, `scene_placement_serialized_rows=0`, `prefab_bindings=43`, `authoring_bindings=43`, `placement_plan_rows=34`.

Problem: Terminal/scanner presentation was allowed to leak managed UnityEvent listener work into simulation/scanner hot paths, and the project lacked a reusable narrative-domain Apex verifier for future checks.
Solution: Queue terminal events and scanner events into fixed byte masks and flush them only from `LateFrameTick`. Add `H8NarrativeApexVerifier`, an Editor-only Roslyn AST scanner that parses source in memory, checks hot roots for scene/service lookup and presentation calls, and checks DataVault/GPU write-lock finally patterns. Validation used `validate_script` and AppliedLore audits, not `dotnet build`.
Rejected Alternatives: Direct UnityEvent dispatch from `Tick`/`OnScan`; per-event managed lists; binary proof dumps; JSON reports; forced `dotnet build` while CPU was 99-100% and `dotnet/csc` were active; fixing the unrelated `DeepReachStationModuleLibrary` compile error from another domain.
Scalability potential: Low tier keeps scanner and terminal listener work out of simulation cadence. Middle/High can attach richer UI/audio listeners without disturbing state ticks. Ultra can layer terminal glow, PDA panels, VO, and scan spectacle while preserving event-driven DataMonolith truth and fixed DTO/save routes.
Hardware Impact: 0 us/frame for new verifier; scanner/terminal events remain zero-GC fixed-mask state transfer. Changed scripts pass `validate_script` with zero diagnostics. AppliedLore full audit remains green with `packets=55`, `applied_records=825`, `applied_routes=49`, `prefab_bindings=43`, `scene_bindings=0`.

Problem: First Apex verifier execution exposed real phase violations: `TerminalOsRuntime` bound material textures/floats/buffers from `SlowTick` rebuild paths, and `ScannableFragment` could still route hot scanner progress through `GlobalRegistry.TryRegisterLateFrameTickable` after a previous unregister.
Solution: Make `ScannableFragment` late-frame registration lifecycle-cold in `OnEnable`, with hot scan paths only writing fixed pending flags. Move TerminalOS material/global texture/buffer binding to the existing `LateFrameTick` visual-sync phase; resource creation/upload paths only set `_bindingsDirty`.
Rejected Alternatives: Keeping event-driven late-frame self-registration; allowing `SlowTick` to perform renderer binding because it is infrequent; adding a new dispatcher/scheduler abstraction for one fragment lane; running `dotnet build` after Unity validation already passed and one compiler process remained active.
Scalability potential: Low tier avoids managed listener and registry mutation work during scan progression. Middle/High can keep richer terminal materials and decryption puzzle presentation without simulation phase leaks. Ultra can bind denser buffers and visual overlays from visual-sync only.
Hardware Impact: 0 managed allocations/frame. Apex verifier proof: `files_parsed=9`, `hot_roots=14`, `methods_from_roots=1603`, `dependency_findings=0`, `phase_findings=0`, `lock_findings=0`, `gpu_locks_checked=6`, `fatal_findings=0`, `build_invocations=0`.

Problem: PDA AppliedLore presentation still owned a parallel packet-record lookup path, including default-locale fallback and direct `H8StaticDataArena` access. That violates the "one fact -> one route" rule and makes future locale/runtime fixes drift across two call sites.
Solution: Extend `H8AppliedLoreRuntime` with `TryFindPacket`, an `in H8AppliedLorePacketRecord` UTF-8 overload, and `GetPacketRecords`. Route `PDAEncyclopediaStreamer` through that facade for active entry text, title lookup, and metadata seeding. Remove the redundant `SignalBus<LoreFragmentScannedSignal>.EnsureInitialized` before `TryPushTracked`.
Rejected Alternatives: A new managed lore service; runtime CSV/markdown interpretation; keeping direct PDA static-arena lookups; stopping Unity Play Mode to force scene placement while another agent may own the current session.
Scalability potential: Low tier keeps compact scanner/terminal/PDA text on the same fixed string-pool route. Middle/High can add richer PDA panels and terminal listeners without changing packet lookup ownership. Ultra can add dense wiki/site/VO presentation while the packet hash and locale fallback stay centralized.
Hardware Impact: 0 allocations/frame. Per unlock, one redundant signal-lane initialization branch is removed. Validation: `validate_script` passed for `H8AppliedLoreRuntime.cs` and `PDAEncyclopediaStreamer.cs`; AppliedLore full audit passed with `applied_records=825`, `applied_routes=49`; Apex verifier passed with `dependency_findings=0`, `phase_findings=0`, `lock_findings=0`, `build_invocations=0`.

Problem: AppliedLore route cards were baked, but PDA metadata did not expose route position, prerequisites, or depth span. Future UI code could also bypass the facade and read AppliedLore sections directly from `H8StaticDataArena`, recreating route drift.
Solution: Add route-card metadata to `PDAEncyclopediaStreamer.WriteMeta` through `H8AppliedLoreRuntime.TryFindRouteForPacket` and `TryResolveRoutePacketOrdinal`. Extend the Apex verifier so direct AppliedLore `H8StaticDataArena` calls outside `H8AppliedLoreRuntime` are reported as route-bypass findings.
Rejected Alternatives: A separate managed route service; runtime CSV or markdown lookup; per-frame route scanning; relying on human review to detect source-route drift.
Scalability potential: Low tier gets compact route context in PDA. Middle/High can expose richer route filters and evidence chains from the same route records. Ultra can add denser PDA/terminal presentation without changing packet hash, locale fallback, DTO layout, or save truth.
Hardware Impact: 0 allocations/frame. Metadata is written only on PDA entry begin/update, using fixed buffers and baked route records. Verification passed with `dependency_findings=0`, `phase_findings=0`, `lock_findings=0`, `fatal_findings=0`, `build_invocations=0`, and no AppliedLore route-bypass findings.

Problem: Out-of-order AppliedLore packets were correctly markable as encrypted, but an already-found encrypted packet would remain locked until rescanned after its prerequisite was found. That is a design tax on the player and creates false friction in long evidence routes.
Solution: Gate `PDAEncyclopediaStreamer.UnlockEntry` on baked route prerequisites and add a bounded encrypted-dependent promotion sweep after each successful unlock. The sweep scans the fixed 256-slot metadata buffer, promotes entries whose prerequisite hashes are now unlocked, clears encrypted flags, and updates PDA masks/revisions without a managed graph.
Rejected Alternatives: Forcing a second scan of the same object; building a managed dependency graph service; per-frame polling of prerequisite state; letting UI display unlocked text while the bitmask still says locked.
Scalability potential: Low tier gets deterministic encrypted/decrypted lore order with compact PDA text. Middle/High can show better route filters and evidence chains from the same flags. Ultra can add richer diegetic PDA/terminal presentation without changing hash truth, DTO layout, or save identity.
Hardware Impact: 0 allocations/frame. Promotion runs only on scan/unlock events, bounded to five passes over 256 metadata slots. Full/source AppliedLore audits passed with `applied_records=825`, `applied_routes=49`; `dotnet build` was rejected under CPU 97% with active `dotnet.exe`.

Problem: Route prerequisite correctness was only partially protected. Self-references were caught by local code paths, but full source graphs and route cards needed cycle detection before they reached runtime.
Solution: Extend `AppliedLoreRuntimeAudit.py` with prerequisite-chain cycle validation for source evidence graphs and route-card dependencies, and keep AppliedLore arena access centralized through `H8AppliedLoreRuntime` using a project candidate scan in `H8NarrativeApexVerifier`.
Rejected Alternatives: Validating only JSON/CSV syntax; trusting writers to avoid dependency loops; allowing PDA or terminal code to read AppliedLore sections directly from `H8StaticDataArena`.
Scalability potential: Low/Middle/High/Ultra all share the same acyclic evidence graph. Presentation density can scale, but the player cannot enter an impossible route due to cyclic prerequisites.
Hardware Impact: Editor/import audit only. Runtime uses fixed switch reads for prerequisites and PDA bitmask checks; no runtime parser, no allocation-heavy dependency container.

Problem: PDA still carried its own AppliedLore UTF-8 to UTF-16 title decoder after AppliedLore text routing had been centralized. That creates a second text surface implementation and a future drift point for CJK/RTL/error handling.
Solution: Add record-based `H8AppliedLoreRuntime.TryWriteSurfaceUtf16` / `TryWriteTitleUtf16` overloads and route PDA title writes through them while preserving the cached `H8AppliedLorePacketRecord`.
Rejected Alternatives: Managed string construction; a new PDA-owned text service; direct `H8StaticDataArena` reads from UI; a second lookup by packet hash after PDA already cached the record.
Scalability potential: Low tier receives compact PDA titles through the same string-pool route. Middle/High/Ultra can add richer PDA panels and localized surfaces without duplicating decode behavior.
Hardware Impact: 0 allocations/frame. The work occurs only on UI text write. Idle runtime cost remains 0 us/frame.

Problem: The Apex verifier was invocation-focused and could miss two important classes: fully qualified `Hecton8.Core.GlobalRegistry.*` access and assignment syntax like `label.text = value` in non-visual hot roots.
Solution: Extend the Roslyn AST scan to flag `*.GlobalRegistry` member access and direct `.text` / `.richText` assignments outside `LateFrameTick`.
Rejected Alternatives: Regex scanning source text; relying on previous verifier output; treating assignment syntax as harmless because it is not an invocation.
Scalability potential: Low/Middle/High/Ultra all benefit from stronger phase proof. More UI/audio layers can be added later without allowing presentation work back into simulation roots.
Hardware Impact: Editor-only. Unity verifier proof after the change: `dependency_findings=0`, `phase_findings=0`, `lock_findings=0`, `fatal_findings=0`, `build_invocations=0`, `analysis=RoslynAST_in_memory`.

Problem: TerminalOS physical preview lines use `FixedString32Bytes` and an ASCII-oriented SDF/GPU lane. Non-Latin AppliedLore titles were being reduced to question marks, which is unacceptable for a multilingual release even if the full text UI is localized.
Solution: Resolve the requested locale first, then fall back to `en_US` only for the small physical preview when the localized UTF-8 contains bytes outside printable ASCII. Full PDA and terminal text remain localized through the AppliedLore facade.
Rejected Alternatives: Expanding the terminal DTO/glyph atlas in this pass; runtime transliteration; allocating managed preview strings; accepting `????` as a localized terminal preview.
Scalability potential: Low tier gets clean machine-readable physical previews. Middle/High can later add richer localized TMP surfaces. Ultra can add Unicode-capable diegetic screens without changing packet hashes or DataMonolith ownership.
Hardware Impact: 0 us/frame idle. Worst case adds one event-triggered baked string-pool fallback lookup for non-default locales with non-ASCII preview text.

Problem: AppliedLore unlocks could reach PDA text, but terminal/POI/scannable hash unlocks did not consistently carry AUP into a world-impact route. That made ecological storytelling depend on separate POI presentation code and left terminal lore unable to affect biome tint/audio interference.
Solution: Add `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt` and `TryRaiseScanCompleteWorldImpact`. The facade now emits `LoreFragmentScannedSignal` plus spatial `ScanCompleteSignal` when an AUP exists, maps selected packet hashes to existing `BiomeChangedSignal`, `ToolAcousticSignal.StateDataGhost`, and cached `ISpatialAudioNarrativeRadioSink.SetNarrativeRadioInterference` from `HectonNarrativeDirector.LateFrameTick`. Extend `H8NarrativeApexVerifier` so future external direct `TryRaisePacketUnlocked` calls are reported.
Rejected Alternatives: Enlarging the 32-byte `LoreFragmentScannedSignal`; adding a new signal lane without route-card proof; runtime audio-cue string lookup; scene searches; forcing terminal/POI logic into PDA code.
Scalability potential: Low tier receives cheap hash-driven tint/audio interference. Middle can add per-biome scatter listeners. High can bind actual AudioLogData cue hashes when DataMonolith supports cue ids. Ultra can layer richer ghost audio/visual effects while packet hash, locale, and save truth remain stable.
Hardware Impact: 0 us/frame idle. Event path emits fixed structs only; no managed strings, no lists, no new scene references. Build was not run because `dotnet.exe` PID 20608 is active.

Problem: The expanded Apex verifier correctly flagged `ScannerDataMiningRouter.TryWriteVaultSettings`: the method had one DataVault write lock and a `finally`, but the lock acquisition call itself was outside the lexical `try` block. That shape is fragile because future edits between acquire and try could reintroduce a deadlock vector.
Solution: Move `TryAcquireWriteLock` into the `try` block, add a single `lockAcquired` boolean, and release only from `finally` when acquisition succeeded. This preserves one lock, one owner id, one buffer, one release route.
Rejected Alternatives: Weakening `H8NarrativeApexVerifier`, allowing "nearby finally" as proof, adding nested locks, or using `using`/managed wrappers over DataVault ownership.
Scalability potential: Low/Middle/High/Ultra all get the same deterministic lock shape. Higher presentation density can depend on scanner settings writes without inheriting a deadlock-prone route.
Hardware Impact: 0 us/frame. The method is not a per-frame scanner tick; it is a cold/settings write path. Apex proof after the change: `dependency_findings=0`, `phase_findings=0`, `lock_findings=0`, `fatal_findings=0`, `build_invocations=0`, `analysis=RoslynAST_in_memory`.

Problem: `DataArchaeologyRuntime` still owned a direct `SignalBus<LoreFragmentScannedSignal>` write. That created two first-party owners for lore commit semantics: archaeology code and `H8AppliedLoreRuntime`.
Solution: Route archaeology scan completion through `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt`. Add an optional `reconKind` byte to preserve scanner classification in the existing `ScanCompleteSignal`, and extend `H8NarrativeApexVerifier` to reject direct `LoreFragmentScannedSignal` pushes outside the facade.
Rejected Alternatives: Leaving the direct signal writer because it happened to be zero-GC; adding a second archaeology-specific facade; dropping `ReconKind` for scannable discoveries; widening `LoreFragmentScannedSignal`.
Scalability potential: Low tier keeps one compact packet-hash route. Middle/High can add richer archaeology UI/audio without duplicating signal ownership. Ultra can layer ghost audio/world impact on the same scan completion path.
Hardware Impact: 0 us/frame. Scan completion remains event-triggered fixed-struct signal traffic. Verification: direct source grep shows only `H8AppliedLoreRuntime` writes `LoreFragmentScannedSignal`; Apex verifier remains clean.

Problem: AppliedLore manual-policy rows were source/prefab-ready but not placed in `02_HECTON_WORLD`; previous attempts were blocked by Play Mode or unsafe scene ownership.
Solution: After Play Mode ended and `00_BOOTSTRAP` was clean, load `02_HECTON_WORLD` through Unity scene API and run `Hecton8/Lore/Apply Applied Lore Scene Placement Plan`. Verify the scene root and component hashes through Unity SerializedObject inspection, not by raw YAML mutation.
Rejected Alternatives: Editing `.unity` YAML by shell; stopping another agent's Play Mode session; claiming prefab rows as scene content without instantiated objects; failing because terminal hashes are prefab-inherited instead of scene-overridden.
Scalability potential: Low tier gets static terminal/discovery anchors. Middle/High can move or dress these authored anchors. Ultra can layer terminal glow, VO, images, and prop dressing while packet hash ownership stays in prefab/scene fields.
Hardware Impact: 0 us/frame idle. Runtime work is unchanged: player interaction/scan reads fixed packet hashes and resolves DataMonolith text on demand. Unity proof: root children 34, terminal hashes 27/27, discovery hashes 7/7, scene saved clean.

Problem: The audit only counted direct scene-serialized hash fields. After placement, all 34 objects existed, but only 7 discovery rows had direct scene overrides; 27 terminal hashes were inherited from generated terminal prefabs. That made the audit output look incomplete.
Solution: Add `scene_placement_covered_rows`, requiring object presence in the scene and hash proof from either scene override or source prefab. Keep `scene_placement_serialized_rows` unchanged so the distinction remains explicit.
Rejected Alternatives: Inflating `scene_placement_serialized_rows`; duplicating terminal hashes as scene overrides; changing prefab data just to satisfy a counter; leaving future agents with ambiguous audit output.
Scalability potential: Low/Middle/High/Ultra all get clearer authoring proof without touching runtime. Terminal prefab variants can keep inherited hash ownership while still proving scene coverage.
Hardware Impact: 0 us/frame. Offline audit only. Verified `scene_placement_covered_rows=34`.

Problem: Scene terminals had AppliedLore packet hashes, but no decoupled route from `MessageTerminal` interaction into the physical TerminalOS preview buffer. The result was incomplete runtime integration: PDA/wiki could unlock, but diegetic terminal screens could not reflect the same packet without direct coupling or manual UI scripting.
Solution: Add a 32-byte `AppliedLoreTerminalPreviewSignal`, register its SignalBus contract, publish it from `MessageTerminal` only on interaction, and consume it from `TerminalOsRuntime.LateFrameTick` after job finalization. The preview text comes from baked DataMonolith UTF-8 slices; non-ASCII localized previews fall back to `en_US` only for the tiny `FixedString32Bytes` physical screen lane.
Rejected Alternatives: Gameplay calling TerminalOS directly; runtime scene search by terminal object; managed string transfer through signals; widening terminal DTOs; rendering full CJK/RTL on the low-byte preview lane in this pass.
Scalability potential: Low tier gets a cheap ASCII machine-preview. Middle/High can add richer localized TMP terminal panels on the same packet hash. Ultra can layer VO, glow, and diegetic images while keeping packet hash, locale, and DataMonolith ownership stable.
Hardware Impact: 0 us/frame idle. Interaction emits one fixed struct; visual sync consumes a bounded signal snapshot. Source/full audits report `scene_terminal_preview_rows=27`. Apex verifier reports `dependency_findings=0`, `phase_findings=0`, `lock_findings=0`, `fatal_findings=0`, `build_invocations=0`.

Problem: Adding `SignalBusRuntime.cs` to the Apex verifier exposed one strict lock-proof issue and one verifier heuristic bug. The frame snapshot write lock was logically released, but acquire sat outside the lexical `try`; the verifier also misclassified `ReleaseWriteLock` as a second write-lock acquire because the text contains `CoreDataVault` and `WriteLock`.
Solution: Move `SignalBus<T>.TryAcquireFrameSnapshotWriteLock` acquisition inside the `try` body with `lockAcquired` and `ownershipTransferred` guards. Update `H8NarrativeApexVerifier.IsDataVaultWriteLock` so `ReleaseWriteLock` is not counted as an acquire.
Rejected Alternatives: Removing `SignalBusRuntime.cs` from verifier coverage; weakening the lock rule; accepting stale false-positive Apex output; adding nested locks or managed wrappers.
Scalability potential: All tiers retain one lock owner and one release route for signal frame snapshots. Higher visual density can rely on SignalBus snapshots without adding deadlock ambiguity.
Hardware Impact: 0 us/frame. This is source-shape and editor-proof hardening. Unity verifier after script refresh: `files_parsed=13`, `lock_findings=0`, `fatal_findings=0`, `analysis=RoslynAST_in_memory`.

Problem: TerminalOS preview signals carried both terminal index and terminal hash, but the consumer trusted a valid index before checking hash. A stale scene override could write an AppliedLore line to the wrong physical terminal while still passing the previous broad audit.
Solution: Make the preview writer hash-authoritative when a hash is supplied. `TrySetTerminalAppliedLoreLine` now opens the terminal state buffer first, validates the index/hash pair, falls back to a bounded hash scan, and only then resolves the baked AppliedLore text. The audit now parses actual index/hash pairs from the scene and rejects duplicate indices or mismatched hashes.
Rejected Alternatives: Runtime scene lookup, managed hash maps, trusting serialized index, broad scene-wide scalar checks, widening DTOs, or adding a direct MessageTerminal-to-TerminalOS dependency.
Scalability potential: Low tier keeps cheap ASCII physical previews. Middle/High can add richer localized terminal panels on the same packet hash. Ultra can add image/VO/glow layers without changing packet hashes, TerminalOS DTO layout, or DataMonolith ownership.
Hardware Impact: 0 us/frame idle. Interaction-frame fallback is a bounded scan over `_terminalCount` only when hash/index drift exists. Validation: `TerminalOsRuntime.cs` standard parse clean; source/full AppliedLore audits report `scene_terminal_preview_rows=27`; Apex verifier reports zero dependency, phase, lock, and fatal findings with `build_invocations=0`.

Problem: Source search showed no `TerminalOsRuntime` script GUID in scenes or prefabs. The signal bridge was architecturally valid but could be invisible in actual world content if no scene-owned TerminalOS runtime exists.
Solution: Extend the existing AppliedLore scene placement editor route to create/configure a single `TerminalOsRuntime` under `__APPLIED_LORE_SCENE_PLACEMENT`. The editor pass binds placed terminal renderers/transforms by serialized preview index and assigns existing TerminalOS compute, panel mesh, SDF atlas texture, and array-panel material assets.
Rejected Alternatives: Runtime scene search, adding `TerminalOsRuntime` references to `MessageTerminal`, one runtime per terminal prefab, raw scene YAML edits, killing or restarting Unity, or launching a second Unity editor while another agent may own the session.
Scalability potential: Low tier uses the same compact terminal texture-array lane. Middle/High can bind richer terminal materials and more placed screens. Ultra can add denser terminal panels and visual effects without changing the packet hash route or gameplay ownership.
Hardware Impact: Runtime cost remains event-driven and visual-sync owned. Scene application is pending because Unity MCP command processing became unstable: additive scene load was rejected as play-mode-blocked and subsequent commands disconnected, with Unity PID 1740 non-responding. No unsafe scene mutation was made.

Problem: The new TerminalOS runtime editor authoring route populated `terminalRenderers` from the first renderer found under each `MessageTerminal`. Generated terminal anchors already serialize `statusLightRenderer`; ignoring that field can bind the wrong child mesh if a prefab gains decorative or collision renderers. It also did not expose null renderer slots in the placement report.
Solution: Read `MessageTerminal.statusLightRenderer` through `SerializedObject` when building TerminalOS runtime arrays, with child-renderer discovery only as a legacy fallback. Use the explicit generic `GetComponentsInChildren<MessageTerminal>` overload for the terminal query. Add `terminal_os_missing_renderers` to the scene placement report.
Rejected Alternatives: Duplicating renderer references as scene overrides, adding runtime renderer search, coupling `MessageTerminal` directly to `TerminalOsRuntime`, raw scene YAML edits, or accepting a green menu report with missing terminal visual surfaces.
Scalability potential: Low tier keeps one compact physical terminal surface per preview row. Middle/High/Ultra can add richer prop dressing under terminal prefabs without accidentally stealing the TerminalOS material target.
Hardware Impact: Editor-only. Runtime remains one fixed `AppliedLoreTerminalPreviewSignal` per interaction and visual-sync-owned TerminalOS state writes. AppliedLore source/full audits passed; Unity scene application is still blocked because MCP reports no active Unity session.

Problem: The audits proved placed terminal preview rows, but did not explicitly show whether the scene-owned `TerminalOsRuntime` consumer actually exists after the new editor authoring route runs.
Solution: Add `scene_terminal_os_runtime_rows` to `Tools/AppliedLoreRuntimeAudit.py`. The counter requires the planned terminal scene to contain `__APPLIED_LORE_TERMINAL_OS_RUNTIME` and the `TerminalOsRuntime` script GUID; if the object name exists without the script GUID, the audit fails.
Rejected Alternatives: Treating source/editor code as scene integration proof; raw scene YAML scan with no script identity check; failing the whole audit before Unity MCP can safely apply the route.
Scalability potential: Low/Middle/High/Ultra all use the same scene-owned TerminalOS consumer. Visual density can scale after the scene object exists; packet hash ownership stays unchanged.
Hardware Impact: Offline audit only. Runtime cost 0. Current value is correctly `scene_terminal_os_runtime_rows=0` because Unity scene application is still pending.

Problem: The source audit had visibility for scene proof, but did not force the editor authoring route to keep the TerminalOS runtime creation/binding symbols alive.
Solution: Extend `Tools/AppliedLoreRuntimeAudit.py` source-route validation with exact TerminalOS runtime authoring symbols: runtime object name, scene creation method, array binding method, explicit renderer field, missing-renderer report token, compute asset path, and runtime terminal counter accumulation.
Rejected Alternatives: Trusting manual code review; accepting a green audit from placement-only symbols; failing the audit on `scene_terminal_os_runtime_rows=0` before the Unity scene can be safely mutated.
Scalability potential: Low/Middle/High/Ultra all depend on the same scene-owned TerminalOS consumer. Later richer terminal screens can scale material quality and density without changing the hash/signal route.
Hardware Impact: Offline audit only. Runtime cost 0. Verification used `py_compile` and AppliedLore source/full audits; no `dotnet build` was launched. Unity script refresh timed out and MCP lost the active instance, so scene write remains blocked.

Problem: The TerminalOS runtime scene consumer was implemented in the editor route but not yet present in `02_HECTON_WORLD`.
Solution: After Unity MCP exposed an active instance again, load `02_HECTON_WORLD` through Unity scene API and run the existing AppliedLore placement menu. Verify with Unity log output and independent full AppliedLore audit.
Rejected Alternatives: Raw `.unity` editing; killing/restarting Unity; running `dotnet build`; adding runtime factories or hot scene searches to cover a missing authoring step.
Scalability potential: Low tier gets one compact scene-owned TerminalOS runtime for 27 terminal surfaces. Middle/High/Ultra can scale TerminalOS material quality, CRT effects, and richer localized overlays without changing `MessageTerminal` gameplay ownership or the fixed preview signal lane.
Hardware Impact: 0 us/frame idle. Runtime remains interaction-event signal publish plus `LateFrameTick` visual consumption. Unity Apex verifier reports zero dependency, phase, lock, and fatal findings with `build_invocations=0`.

Problem: `scene_terminal_os_runtime_rows=1` proved object/script presence but not that the renderer and transform arrays were populated for every planned terminal.
Solution: Extend the AppliedLore audit to extract the serialized `TerminalOsRuntime` block and count `terminalRenderers`/`terminalTransforms` entries, rejecting null references and slot-count drift against the scene placement plan.
Rejected Alternatives: Relying on Unity menu logs only; adding runtime self-healing arrays; running Play Mode for a static serialized invariant.
Scalability potential: Low tier gets correct renderer/transform slots for compact physical previews. Middle/High/Ultra can increase terminal material richness without changing scene proof criteria.
Hardware Impact: Offline audit only. Runtime cost 0. Source/full audits now prove 27 renderer slots and 27 transform slots.

Problem: Python audit proved TerminalOS scene wiring, but the C# Narrative Apex verifier did not yet fail on a missing or drifted scene-owned TerminalOS consumer. That left the primary Unity-side proof tool weaker than the offline audit.
Solution: Add a scene binding pass to `H8NarrativeApexVerifier`: it reads the placement plan, requires 27 `MessageTerminal` rows, requires `__APPLIED_LORE_TERMINAL_OS_RUNTIME` and the `TerminalOsRuntime` script GUID in `02_HECTON_WORLD`, counts non-null `terminalRenderers` and `terminalTransforms`, and prints `terminal_os_*` counters in the Apex console report. The first regex form used `Singleline` and could over-count renderer refs by swallowing the transform array; it was corrected to line-bounded matching and verified against the real scene block as 27/27/nulls=0. Strengthen `Tools/AppliedLoreRuntimeAudit.py` further by matching the 27 scene slots to prefab instance renderer/transform file IDs.
Rejected Alternatives: Runtime array self-healing, Play Mode probing for static serialized data, raw YAML mutation, `dotnet build`, or leaving the C# verifier count-blind.
Scalability potential: Low tier gets the same compact physical terminal route. Middle/High/Ultra can increase terminal material richness and overlay density without changing packet hashes, signal payload layout, or scene ownership.
Hardware Impact: 0 us/frame. Verification is editor/offline only. Current proof: source/full AppliedLore audits report `scene_terminal_os_runtime_verified_slots=27`; local Roslyn parse of the 14-file Apex scope reports zero syntax errors. Unity Console proof for the new fields is pending because MCP editor ping is not answering and `dotnet.exe` PID 27796 is active.

Problem: `02_HECTON_WORLD.unity` is binary in `HEAD` (`33756552` bytes) and text YAML in the working tree (`694575` bytes). The current scene contains AppliedLore/TerminalOS bindings, but the size/type drift means a terminal-only green audit could hide a damaged or reduced world scene. Offline `HEAD` byte scans show historic `Crest` and `MapMagic` strings; the working scene has a MapMagic runtime marker and terrain, but zero `Crest` text markers.
Solution: Add minimal world-scene counters to both `Tools/AppliedLoreRuntimeAudit.py` and `H8NarrativeApexVerifier`. The gates require the target scene to be text-readable and contain scene roots, MapMagic, Terrain, and TerrainCollider. The tools print byte size, root count, core marker counts, Crest marker count, available ocean prefab assets, and scene refs to those prefabs so future reports expose the scene shape instead of only terminal wiring.
Rejected Alternatives: Reverting `02_HECTON_WORLD.unity` without user approval; raw editing Unity YAML; parsing Unity's binary scene format; adding runtime scene searches/self-healing; making Crest a hard failure without a current scene-authority document proving exact Crest presence is mandatory in this serialized scene.
Scalability potential: Low tier still uses one compact terrain/MapMagic-backed world lane plus diegetic TerminalOS previews. Middle/High/Ultra can add richer ocean/Crest visuals, terminal overlays, and environmental evidence without changing AppliedLore packet hashes or signal payload layout.
Hardware Impact: 0 us/frame. Verification is offline/editor-only. Current source/full audits report `scene_world_roots=6`, `scene_world_mapmagic_rows=1`, `scene_world_terrain_rows=1`, `scene_world_terrain_collider_rows=1`, `scene_world_crest_markers=0`, `scene_world_ocean_prefab_assets=2`, and `scene_world_ocean_prefab_refs=0`; zero Crest/ocean prefab scene refs are visible risk counters, not cleared ocean proof.

Problem: The Apex lock verifier accepted any ancestor `try/finally` around a DataVault write-lock acquire. That proves structured control flow, but not that the finally actually releases the write lock. It also used method-wide write-lock counts, which is stricter than the real invariant and weaker at proving simultaneous ownership.
Solution: Change `H8NarrativeApexVerifier` to require `ReleaseWriteLock` inside the direct DataVault lock finally, reject multiple write-lock acquires inside the same try/finally scope, verify SignalBus owner-write wrapper calls are immediately followed by caller-side `try/finally` with `ReleaseFrameSnapshotOwnerWrite`, require `UnlockBufferAfterWrite` inside GPU mapped-buffer finalizers, and raise `hot_direct_job_complete` for direct `.Complete()` calls reached from hot roots.
Rejected Alternatives: Trusting shape-only finally detection; using method-wide write-lock count as the mathematical proof; relying on grep for direct job completions; running `dotnet build` while `dotnet.exe` is already active; mutating runtime lock code without a failing invariant.
Scalability potential: Low/Middle/High/Ultra all keep the same DataVault and GPU upload runtime paths. The change only strengthens editor/source proof so richer terminal visuals can scale without hiding lock drift.
Hardware Impact: 0 us/frame. Editor-only verifier hardening. Current proof: AppliedLore source/full audits pass, and local in-memory Roslyn parse reports `ROSLYN_PARSE_SCOPE_OK files=14 errors=0`. Unity Console proof is pending because the editor session is shared/unstable.

Problem: The Apex verifier covered hot dependency lookups, presentation phase drift, lock flattening, and direct job completion, but Zero-GC proof for the Narrative/AppliedLore hot-root path still depended on external grep. That is weaker than an integrated AST gate.
Solution: Add hot-root managed allocation detection to `H8NarrativeApexVerifier`: obvious managed collection/string/array allocations, interpolated strings, string formatting/concat/join, LINQ, and `ToString` now raise `hot_managed_allocation` and increment `zero_gc_findings`. Struct DTO creation remains allowed to avoid false-positive blocking of fixed native writes.
Rejected Alternatives: Flagging every `new` expression; relying on grep; adding runtime allocation probes; running a build while CPU/dotnet are busy.
Scalability potential: Low/Middle/High/Ultra keep the same runtime; the proof tool now prevents later richer UI/terminal work from sneaking managed allocation into hot roots.
Hardware Impact: 0 us/frame. Editor-only AST proof. Local Roslyn parse remains clean across the 14-file Apex scope; source AppliedLore audit remains green.

Problem: AppliedLore terminal preview routing was valid in source, but the Apex verifier only proved it indirectly through general dependency/phase/scene checks. A future edit could add a second publisher, destructive `TryConsumeFrame`, or move consumption out of `LateFrameTick` without a dedicated route finding.
Solution: Add `ScanAppliedLoreTerminalPreviewRoute` to `H8NarrativeApexVerifier`. It verifies the fixed 32-byte signal contract, SignalBus capacity/low-tier table, exactly one `MessageTerminal` publisher, exactly one `TerminalOsRuntime` `GetFrameSnapshot` reader, exactly one `LateFrameTick` consumer call, and zero `TryConsumeFrame` uses for this lane.
Rejected Alternatives: Trusting generic scans; scanning only text with no method ownership; widening the signal DTO; adding runtime self-checks.
Scalability potential: Low tier keeps one compact ASCII preview lane. Middle/High/Ultra can add richer panels, VO, glow, and localized overlays while the packet hash route remains one fixed SignalBus lane.
Hardware Impact: 0 us/frame. Editor-only route proof. Roslyn parse remains clean; AppliedLore source audit remains green.

Problem: `TerminalOsRuntime.DecryptionBlackBoxDumpWriter` had background-thread primitives but did not actually start a writer thread. On a decryption fault, `TryEnqueue` could mark a dump as accepted while the payload stayed pending until `Dispose`, which is too late for crash evidence and still leaves misleading thread/orphan-process surface in the source.
Solution: Remove `AutoResetEvent`, `Thread`, and `WriterLoop` from the writer. Keep the fixed managed mirror array as cold owner state. `TryEnqueue` now copies telemetry under `_gate`, releases the lock, and synchronously calls `DrainPending()` only on the fault path. `Dispose` still drains any pending record. Add `ScanTerminalOsBlackBoxDumpRoute` to `H8NarrativeApexVerifier` so thread primitives and missing synchronous drain are hard findings.
Rejected Alternatives: Starting a real background writer; keeping a no-op thread stub; doing dump file writes from normal frames; adding runtime scene/profiler probes.
Scalability potential: Low/Middle/High/Ultra all keep zero normal-frame cost. Higher tiers can add richer TerminalOS/decryption telemetry fields later, but the dump writer must remain explicit fault-path work with no persistent unmanaged thread.
Hardware Impact: 0 us/frame in normal gameplay. On low-end hardware a fault dump may block briefly by design; that is acceptable because it preserves crash evidence and avoids orphan thread/process risk.

Problem: The de-orphaned TerminalOS decryption dump route still had a false-success contract: `TryEnqueue` returned true after calling `DrainPending()` even if `NativeFaultDumpWriter.TryWriteAll` failed. That could mark `_decryptionBlackBoxDumped=true` without a real dump artifact. The same route also held `_gate` across file I/O, which is unnecessary lock exposure even though it is not a DataVault write lock.
Solution: Make `DrainPending` and `WritePendingUnsafe` return bool. `TryEnqueue` now returns the synchronous write result. Pending records are copied under `_gate` into a cold `_writeRecords` buffer, then disk I/O happens after `_gate` is released. Extend `H8NarrativeApexVerifier` with `terminal_os_dump_return_drains`, `terminal_os_dump_bool_drains`, and `terminal_os_dump_bool_writes` counters so the route fails if it regresses to enqueue-only success.
Rejected Alternatives: Keeping enqueue success as dump success; adding a retry thread; writing from normal frames; allocating per-dump managed containers; weakening the black-box proof when Unity MCP is unavailable.
Scalability potential: Low tier keeps deterministic no-thread fault dumps with one bounded copy. Middle/High/Ultra can add richer telemetry fields later, but dump acceptance must remain tied to actual write success and cannot become a persistent background service.
Hardware Impact: 0 us/frame normal gameplay. Fault path adds one fixed 300-entry mirror copy and one disk write. No `dotnet build` was run because `dotnet.exe` PID 21320 was active; Unity MCP console access failed at `127.0.0.1:8088/mcp`.

Problem: Decryption dump backpressure telemetry used `TerminalUnlockedSignal.LaneHash` as context. That makes dump-writer failure evidence look like an unlock-lane issue and weakens postmortem triage.
Solution: Add fixed `DecryptionDumpContextHash` (`TDDW`) and publish `DecryptionDumpBackpressureHash` with that context. Extend `H8NarrativeApexVerifier` to require one `TryDumpDecryptionBlackBox` warning call using the dedicated dump context hash.
Rejected Alternatives: Runtime string hashing for context; reusing `TerminalUnlockedSignal.LaneHash`; ignoring context drift because it does not change gameplay state.
Scalability potential: Low/Middle/High/Ultra all keep the same zero-GC warning payload. Higher tiers can add richer telemetry consumers without misrouting dump failures into unlock-lane analysis.
Hardware Impact: 0 us/frame. The warning is emitted only on dump backpressure. `dotnet build` was not run because `dotnet.exe` PID 20256 is active; `py_compile` was not counted because Python atomic `.pyc` writes are returning `PermissionError` in the current workspace.

Problem: The verifier knew `TryEnqueue` returned the real dump write result, but it still did not prove that `WritePendingUnsafe` stayed outside the `_gate` critical section. External grep was carrying that part of the lock-flattening proof.
Solution: Add `terminal_os_dump_gate_locks` and `terminal_os_dump_writes_after_lock` to `H8NarrativeApexVerifier`. `DrainPending` must have one lock scope and exactly one `WritePendingUnsafe` call outside any `lock` ancestor, or the verifier raises `terminal_os_dump_gate_lock_drift`.
Rejected Alternatives: Trusting one-off source scans; moving disk I/O back under `_gate`; adding a retry/background writer; changing DataVault contracts for a private dump writer gate.
Scalability potential: Low tier keeps no-thread deterministic crash dumps. Middle/High/Ultra can expand telemetry payload richness later, but the write path stays fault-only and outside the managed gate lock.
Hardware Impact: 0 us/frame. Editor-only proof. Source/full AppliedLore audits pass; brace scan reports balanced source; direct drain proof reports one gate lock and one post-lock write. `dotnet build` was not run because `dotnet.exe` PID 25932 is active and Unity is running managed callbacks.

Problem: The AppliedLore terminal preview lane had proof for one publisher, one snapshot reader, and LateFrame consumption, but it did not explicitly reject future direct calls into `TerminalOsRuntime.TrySetTerminalAppliedLoreLine` from another runtime file. That would bypass the fixed SignalBus lane and weaken phase ownership.
Solution: Add `terminal_preview_external_writers` to `H8NarrativeApexVerifier`. The verifier scans the fixed runtime Apex scope and fails if `TrySetTerminalAppliedLoreLine(` appears outside `TerminalOsRuntime.cs`. The route remains `MessageTerminal` publisher -> fixed 32-byte signal -> `TerminalOsRuntime.LateFrameTick` visual sync writer.
Rejected Alternatives: Direct references from gameplay terminals to the visual runtime; runtime scene search/self-healing; making `TrySetTerminalAppliedLoreLine` private without first proving no editor/tooling need for the public preview seam.
Scalability potential: Low tier keeps compact ASCII preview updates. Middle/High/Ultra can add richer terminal panels, localized overlays, glow, and VO triggers without changing the SignalBus route or writer phase.
Hardware Impact: 0 us/frame. Editor-only proof. Source/full AppliedLore audits pass; brace scan reports balanced source; direct runtime-scope scan reports `terminal_preview_external_writers_runtime_scope=0`. `dotnet build` was not run because CPU hit 82% and `dotnet.exe` PID 25932 is active.

Problem: Unity-side Apex AST verification found a real zero-GC violation: `Tick -> CompletePlayback -> UpdatePendingMessage -> EnsureWfcOutpostReadMessageSet` could reach `new HashSet<string>(readMessageCapacity)` if the read-message set was missing. The normal lifecycle creates the set in `Awake`, but the hot-root call graph still contained a managed allocation fallback.
Solution: Split the allocation into `EnsureWfcOutpostReadMessageSetCold()` and keep it on `Awake`/cold API paths. Change `UpdatePendingMessage()` to read the cached `HashSet<string>` reference and treat a missing set as "not yet cached" without allocating. Unity Apex now reports `zero_gc_findings=0` and `fatal_findings=0`.
Rejected Alternatives: Ignoring the verifier because normal Unity lifecycle usually calls `Awake`; adding an attribute/comment suppression; allocating lazily from the first playback completion; replacing the set with a bigger refactor of message IDs in this pass.
Scalability potential: Low tier avoids a terminal playback allocation spike. Middle/High/Ultra can add richer terminal presentation and audio/event density without letting terminal state repair allocate from a simulation tick.
Hardware Impact: Removes one possible managed allocation from the playback completion path. Normal-frame cost remains bounded to cached lookups; no DataMonolith layout, save identity, or SignalBus payload change.

Problem: The Apex zero-GC scanner caught explicit managed allocations, but `Array.Resize` is also allocation/growth and was not a named hot invocation guard. MessageTerminal still contains cold `System.Array.Resize` calls for authoring/API paths, so the verifier needed to prove those calls are not reachable from `Tick`, `FixedUpdate`, `LateFrameTick`, or `Execute`.
Solution: Extend `IsHotManagedAllocationInvocation` to flag `Resize`, `Array.Resize`, and `System.Array.Resize` from hot-root call graphs. Re-run the Unity Apex menu after clearing stale Console history; fresh result is green with zero dependency, phase, lock, direct job completion, and zero-GC findings.
Rejected Alternatives: Relying on `new[]` syntax only; adding runtime allocation probes; removing cold authoring resize paths in a broad refactor; running `dotnet build` while `dotnet.exe` PID 15688 is active.
Scalability potential: Low tier keeps terminal playback and preview routes allocation-free during normal frames. Middle/High/Ultra can increase terminal content density, audio triggers, and visual overlays without allowing hidden array growth from hot roots.
Hardware Impact: 0 us/frame. Editor-only verifier hardening. `validate_script` reports zero diagnostics for touched C# files; AppliedLore source/full audits remain green with 825 localized rows, 49 routes, and TerminalOS slots 27/27/27.

Problem: The TerminalOS preview writer was protected against external calls by the Apex verifier, but the method itself was still public. That leaves a bad future API: another system could compile against the public writer before the verifier catches it, and the name `TrySetTerminalAppliedLoreLine` implied an ordinary safe setter instead of a visual-sync owner write.
Solution: Remove the public `TrySetTerminalAppliedLoreLine` surface and keep the writer private as `ApplyTerminalAppliedLoreLine`. The only caller is `ConsumeAppliedLoreTerminalPreviewSignals`, reached from `TerminalOsRuntime.LateFrameTick` after reading the immutable `SignalBus<AppliedLoreTerminalPreviewSignal>` snapshot. Rename AppliedLore publisher helpers to `PublishAppliedLorePacketUnlock` and `PublishAppliedLoreTerminalPreview` so side-effect methods are not disguised as pure `Try*` accessors. Extend `H8NarrativeApexVerifier` with `terminal_preview_public_writers` and update `Tools/AppliedLoreRuntimeAudit.py` source-route tokens. Update `Implementation_Notes.md` to document the private-writer SignalBus route.
Rejected Alternatives: Keeping the public writer and relying on reviewer discipline; direct `MessageTerminal` to `TerminalOsRuntime` coupling; runtime lookup/self-healing; changing the fixed 32-byte signal DTO; adding a new markdown/prose interpreter path.
Scalability potential: Low tier keeps one compact physical terminal preview with bounded text writes. Middle/High/Ultra can add richer CRT materials, VO/subtitle surfaces, glow, and localized overlays while the gameplay side still publishes only the fixed hash-based signal.
Hardware Impact: 0 us/frame. Normal route remains event publish plus `LateFrameTick` visual sync. Source/full AppliedLore audits pass with 825 localized rows and 49 route records. Local in-memory Roslyn AST parse over the 14-file Apex source scope reports zero syntax errors, zero direct hot dependency hits, and zero direct hot job-complete hits. Unity live Apex rerun is pending because the previous MCP session disappeared and the replacement Unity process is still `Opening project...` with Unity-owned `dotnet.exe` processes active; no `dotnet build` was launched.
