# Competitive Gap Analysis: Subnautica 2 vs HECTON-8

Agent: SHINOBU_81
Date: 2026-05-18
Status: RESEARCH REPORT / WEB_REFERENCE + COMMUNITY_SIGNAL + STATIC_DOC / RUNTIME PENDING
Runtime changes: none

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, Steam/public telemetry beyond cited capture, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters, web captures, and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

<!-- DOC_GLOBAL_DOCS_REFRESH:R20_REPORT_SNAPSHOT_BOUNDARY_START -->
## 2026-05-18 R20 Report Snapshot Boundary

This report is a dated research/static-doc snapshot. It does not imply Unity import, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, Steam/public telemetry beyond the cited capture, or runtime proof.

Use current stable authority and the latest DOC_GLOBAL boundary first: `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source, fresh official platform sources, and `Docs/Reports/2026-05-18_DOCUMENTATION_R22_COUNTER_DRIFT_AND_VALIDATION_LOCAL.md`.
<!-- DOC_GLOBAL_DOCS_REFRESH:R20_REPORT_SNAPSHOT_BOUNDARY_END -->

## 2026-05-18 Scope Correction

User correction after this report: HECTON-8 co-op is not currently planned for production. Existing "100km co-op" language from agents is speculative architecture noise unless the user promotes it to an actual roadmap item.

Marketing rule: do not promise co-op, multiplayer, "play with friends", shared bases, or 100km networking. Treat co-op only as optional future R&D / internal architecture pressure test. Competitive analysis may still study SN2's co-op because it is a competitor advantage, but HECTON-8 public positioning is single-player-first.

## Executive Verdict

Subnautica 2 is not collapsing. It is a strong Early Access launch with franchise gravity, co-op, broad storefront reach, and a very positive Steam reception. Steam API/storefront data captured on 2026-05-18 showed roughly 60k all-language reviews, about 92.5% positive, with English at about 94% positive. Treat any internal claim that SN2 is "hated" as false.

The exploitable gaps are specific:

- launch trust damage from EULA/privacy backlash;
- performance and crash variance, especially DX12/shader/driver failure reports;
- no-kill/no-defensive-agency frustration around hostile fauna;
- content thinness and "not scary enough yet" complaints;
- co-op/save/desync anecdotes and official co-op features deferred to the second update;
- missing aspirational large submarine fantasy at launch;
- QoL features such as sprint, pinned recipes, voice chat, revive, trading, and base-builder improvements living on the roadmap, not all in the current build.

HECTON-8 cannot win by imitating a colorful alien-ocean sequel. It can win by being colder, more mechanical, more deterministic, more honest about telemetry, and more scalable on weak hardware.

## Evidence Boundary

Evidence classes used:

- `WEB_REFERENCE_OFFICIAL`: Unknown Worlds, Steam, Xbox.
- `WEB_REFERENCE_PRESS`: PC Gamer, PCGamesN, Creative Bloq, TechRadar, GamesRadar, GamingOnLinux, Beebom.
- `STEAM_API`: Steam review API fetched through PowerShell on 2026-05-18.
- `COMMUNITY_SIGNAL`: Reddit / Steam review samples; useful for pain taxonomy, not statistical truth unless paired with aggregate counts.
- `PUBLIC_FOOTAGE_OBSERVATION`: public trailers/showcase/store footage and stills, not frame-captured local analysis.
- `STATIC_DOC`: current HECTON-8 docs and mandate registry.

No Unity import, Unity Console, Play Mode, profiler, Frame Debugger, GCMonitor, player build, or runtime proof is implied. Microseconds saved by this research pass: `0us measured`.

## Source Ledger

- Unknown Worlds EA release: https://unknownworlds.com/en/news/subnautica-2-early-access-released
- Unknown Worlds roadmap: https://unknownworlds.com/en/news/subnautica-2-early-access-roadmap
- Unknown Worlds gameplay trailer post: https://unknownworlds.com/en/news/subnautica-2-early-access-gameplay-trailer
- Steam store: https://store.steampowered.com/app/1962700/Subnautica_2/
- Steam review API sample: `https://store.steampowered.com/appreviews/1962700?json=1`
- SteamDB app metadata: https://steamdb.info/app/1962700/
- Xbox Game Preview page: https://www.xbox.com/en-us/games/store/subnautica-2-game-preview/9pjpcb188svg
- PC Gamer multiplayer guide: https://www.pcgamer.com/games/survival-crafting/subnautica-2-multiplayer/
- PC Gamer roadmap coverage: https://www.pcgamer.com/games/survival-crafting/subnautica-2-roadmap-updates/
- PC Gamer performance/settings article: https://www.pcgamer.com/hardware/turn-down-these-five-subnautica-2-settings-from-epic-to-high-for-a-massive-frame-rate-boost/
- Creative Bloq UE5/visual analysis: https://www.creativebloq.com/3d/video-game-design/subnautica-2-uses-unreal-engine-5-to-make-the-ocean-feel-psychologically-hostile
- PCGamesN impressions: https://www.pcgamesn.com/subnautica-2/early-access-impressions
- TechRadar impressions: https://www.techradar.com/gaming/as-someone-that-sank-over-100-hours-into-the-first-game-subnautica-2-has-lived-up-to-my-expectations-but-theres-one-crucial-element-i-want-to-see-before-im-totally-happy
- GamesRadar impressions: https://www.gamesradar.com/games/survival/subnautica-2-lays-strong-foundations-for-an-improved-sequel-but-the-depths-of-early-access-remain-murky/
- GamingOnLinux Linux/Steam Deck report: https://www.gamingonlinux.com/2026/05/subnautica-2-is-looking-good-on-desktop-linux-its-okay-on-steam-deck-with-a-caveat/
- Beebom crash/DX12/shader issue report: https://beebom.com/subnautica-2-crashing-lagging-dx12-not-supported-how-to-fix/
- Public YouTube videos checked: `cJIHefNB3Hw`, `oK5Nr1VtJ9Q`, `WS0vTRl_2PQ`, `dCJ1UztYGvU`. YouTube comments were not accessible through available tools; ranked "top liked comments" were not verified.

## Task 01 - YouTube Sentiment Parsing

Hard limit: I could not access YouTube comment bodies, like counts, or ranked ordering. Therefore a "top 20 most-liked comments" list would be fabricated. Rejected.

Checked public video surfaces:

| Source | URL | Evidence |
|---|---|---|
| Official Subnautica First Dive Showcase | https://www.youtube.com/watch?v=cJIHefNB3Hw | Public video metadata / no accessible comments |
| Official Gameplay Reveal Teaser | https://www.youtube.com/watch?v=oK5Nr1VtJ9Q | Public video metadata / no accessible comments |
| Official Teaser Trailer | https://www.youtube.com/watch?v=WS0vTRl_2PQ | Public video metadata / no accessible comments |
| GamersPrey EA Gameplay Demo 4K | https://www.youtube.com/watch?v=dCJ1UztYGvU | Public video metadata / no accessible comments |
| GameSpot trailer mirror | https://www.gamespot.com/videos/subnautica-2-early-access-gameplay-trailer/2300-6466865/ | Public trailer page |

Sentiment ratio:

- Steam aggregate: about `92.5% hype / 7.5% rejection` all-language at capture.
- English Steam aggregate: about `94% hype / 6% rejection` at capture.
- Cross-source human reading including Reddit/negative review skew: `75/25 hype/fear`.
- Reddit/search-snippet-only reading: closer to `60/40`, because performance/EULA/early-access anxiety is overrepresented.

Player fear taxonomy:

1. "Early Access is too thin for $29.99."
2. "Co-op will dilute isolation."
3. "The publisher/EULA/telemetry situation is hostile."
4. "No killing or direct defense makes hostile fish annoying, not scary."
5. "No Cyclops-scale submarine fantasy at launch."
6. "Performance is high requirement / UE5 / shader crash prone."
7. "Content and biomes feel too shallow or too safe for veterans."

## Task 02 - Technical Glitch Forensics

Confirmed public facts:

- Steam itself warns Early Access players may encounter bugs and performance issues.
- Steam lists minimum GPU as GTX 1660 6GB / RX 5500 XT 6GB, above HECTON-8's MX350 target.
- PC Gamer's settings article says Lumen-adjacent settings can cost enough that dropping from Epic to High yields major frame-rate gains.
- Beebom reported DX12 errors, infinite loading screens, shader compilation crashes, heavy frame drops before crashing, and RX 9000 driver issues based on community reports.
- Reddit and Steam review samples show FPS drops, 1% low complaints, UI/menu stutter, framegen stutter, AMD/driver complaints, and some hard crashes.

Unproven or overclaimed:

- Lumen usage is supported by Creative Bloq/PC Gamer public analysis, but not by direct engine capture.
- Nanite use was not proven from accessible official/current sources.
- Unreal replication internals were not proven. Public sources prove online co-op and EOS/SteamDB metadata, not architecture.
- "Shader compilation stutter" as a broad universal issue was not proven. Shader compilation crashes/errors are reported; traversal stutter is present as anecdotes, not a dominant aggregate term.

HECTON-8 exploit:

- Precompile shader variants per tier.
- Ship a visible shader warmup/progress gate, not first-frame hitch roulette.
- Keep Lumen-class dynamic GI as `GlobalQualityWeight` overkill path only; Low/MX350 gets baked AO, depth fog LUT, projected caustics, and emissive anchors.
- Make `0 B/frame hot path` and shader cache determinism a marketing claim only after profiler/GCMonitor artifacts exist.

## Task 03 - Mechanical Pros And Cons Matrix

| SN2 Feature | Player Reception | HECTON-8 Counter-Implementation |
|---|---|---|
| 4-player co-op | Major hype driver; official second update still targets co-op improvements | Do not counter-market with co-op. Internal-only R&D option: local loopback, deterministic state hash, AUP packets, authority split, rollback telemetry before any roadmap claim |
| Base building | Praised as refined, snap-friendly, movable furniture; roadmap still lists base-builder improvements | Pressure-rated modular shells, grid/AUP snapping, flood/structural stress scalars, visual grime layers, no soft sculptural cost in baseline |
| Lumen/dynamic lighting | Strong visuals; performance settings sensitivity and lighting glitch reports exist | Baked AO + LUT fog on Low, half-res SSDO MED+, raymarch/SSGI/SSS only as weighted overkill |
| Tadpole / starter vehicle | Useful but some call it slow/lackluster; missing Cyclops fantasy | One iconic heavy vessel early: hydraulic latency, ballast, flooding compartments, station telemetry, cargo/logistics bridge |
| Biomods/adaptations | Clever but underutilized in press; roadmap improves biomods | Suit adaptation ledger: active/passive masks, hazard/scan/wreck evidence, pressure/radiation/acoustic adaptations |
| Friendly/non-lethal ecology | Some like peaceful tone; many negative reviews want defensive agency | Defensive tools allowed but noisy/costly/ecologically consequential; agency without shooter drift |
| Audio/underwater sound | Press praise; some reviews call horror stingers misused | Sabine RT60, hydrophone threat, granular hull stress, acoustic shadows, warning tones tied to truth not cheap jumps |
| Biomes | Visually strong; complaints of thin/samey EA content exist | Low: silhouette/fog identity. Mid: authored object batches. High/Ultra: reactive silt/flora/fauna, Seed Ship corruption zones |
| Wreck gameplay | Roadmap improvement target | Industrial dungeons: salvage/repair/cutter/scanner route, persistent scars, black-box logs, object-batch payloads |
| Inventory/crafting QoL | Pinned recipes/storage cache are roadmap items; inventory stacking requests visible | Fixed-size pinned recipe ledger, storage-network missing-material query, zero-GC UI buffers, route-critical resource hinting |
| Story/voice logs | Press finds narrative hooks; some negative reviews complain voice-log pacing | Priority arbiter: route-critical logs preempt ambient, survival warnings never blocked, deterministic queue/drop telemetry |
| Save sharing | PC Gamer documents save conversion and cloud key sharing | Stronger target: host migration plan, rolling backup ring, Merkle save diff, shared persistence ownership |
| Performance profile | Works for many; high min spec and crash reports are real | MX350-first content budgets, continuous `GlobalQualityWeight`, static budget gates, no binary quality switches |

## Task 04 - Dear Lie Detector

Observed likely fakes and staging:

- Fish visibility is staged through horizon concealment, silhouettes, murky distance, and player imagination. This is correct. Do not simulate omniscient ecological truth.
- Bubbles, dust, silt, caustic color, and light shafts read as layered presentation. They sell water better than per-bubble physics would.
- Flora density is clustered and composed, not physically honest ecology. Use object batches and impostors.
- Base lighting is likely dynamic enough to be expensive. The player sees mood and scenic windows, not a physically rigorous lighting proof.
- Creature loops appear authored/autonomous but still presentation-heavy. Avoid overfitting to actual AI; the player needs readable stimulus and response.

HECTON-8 Dear Lie target:

- Low: 1D fog LUT, dithered silhouettes, static particle sheets, projected caustics, emissive masks, scalar pressure audio.
- Middle: object-batched wreck/flora clusters, acoustic profile per biome, fog authority records.
- High: reactive silt wakes, sonar silhouettes, biolum pulses, richer wetness masks.
- Ultra: volumetric silt, salt/condensation, hull dents, high-tier POM/raymarch/SSS.

## Task 05 - Reddit / Community Pain Point Mining

Community and Steam pain taxonomy:

| Pain | Evidence Quality | Details |
|---|---|---|
| EULA/privacy distrust | High in negative Steam helpful reviews and term scan | Loudest negative-review driver, not gameplay |
| No defensive agency | High | Negative review term scan: fish/kill terms dominated; players want ways to repel or clear hostile fauna |
| Content thinness | Medium-high | Reviews cite 10-15 hour EA completion, few biomes/vehicles/leviathans |
| Performance/crash variance | Medium-high | DX12/shader crash reports, AMD driver issues, FPS/1% low/stutter posts |
| Co-op save/desync | Medium | Anecdotal desync, save loss, joiner 0 FPS, world persistence questions |
| Base/docking bugs | Medium | Dock placement/docking failures, free glass/build refund bugs |
| Inventory/storage | Medium | Inventory stacking requests, storage-cache/pinned-recipes roadmap |
| Fear dilution | Medium | Night not more dangerous, hostile fauna not scary, death lacks punch |
| Missing Cyclops-scale vehicle | Medium | Press and players explicitly want large submarine fantasy |
| Localization/accessibility | Low-medium | Polish omission and FOV/motion complaints show trust gaps |

P0 targets for SHINOBU agents:

- No hostile EULA posture. Offline/local-first by default. Explicit telemetry controls.
- Defensive agency: stun, repel, decoy, acoustic masking, cutting tools, but violence creates noise/biological consequences.
- Rolling save backup and crash-safe single-player state from day one.
- FOV, sprint, mouse acceleration, pinned recipes, storage queries, and inventory filters as launch requirements.
- At least one aspirational vehicle and three biome silhouettes in public playable slice.

## Task 06 - NASA-Punk Aesthetic Audit

SN2 visual identity:

- Bright alien-ocean adventure.
- Bio-organic curves.
- Clean scenic bases.
- Friendly tropical color return.
- Stylized sci-fi readability.

HECTON-8 rejection:

- Do not become "Subnautica but darker." That is a palette swap.
- Tech-art direction: pressure-bent metal, salt crystals, oxidized amber hazard lights, cable forests, condensation, scratched viewport glass, oil-sheen wetness, dead instrumentation, silt curtains, black-box recorder UI.
- Modular bases must look like pressure vessels, not aquarium hotels.
- Use corrosion and industrial function to distance from SN2's clean/plastic look.

Low/Middle/High/Ultra:

- Low: baked grime, AO, LUT fog, emissive failure lights.
- Middle: wetness masks, cable trays, object-batched debris.
- High: pressure dents, local leaks, reactive silt.
- Ultra: salt growth, volumetric shafts, visor microfractures, procedural hull stress overlays.

## Task 07 - Co-Op Latency / Persistence Report

Known:

- Steam and Xbox prove online co-op and cross-platform multiplayer/co-op.
- Unknown Worlds says players can explore alone or with up to three friends.
- PC Gamer documents hosting, converting singleplayer saves to multiplayer, and sharing saved worlds.
- Official roadmap second update is co-op-focused: HUD signals, base builder, pinned recipes, voice chat/emotes, player trading, revive, customization.
- Community anecdotes report desync, save corruption/loss, joiner performance collapse, and shared-world confusion.

Verdict:

Co-op is SN2's strategic advantage, but HECTON-8 must not counter with a feature it does not currently plan. Treat persistence/state ownership as internal R&D only. Public angle stays single-player-first pressure, machines, isolation, salvage, and systemic failure.

Counter:

- `AUP` over wire, not float world positions.
- Dirty bitmasks and quantized deltas.
- Ring-buffer snapshots and rollback/reconciliation.
- Per-player inventory intent vs world-owned storage.
- Base edits as transaction records.
- Host migration plan or explicit no-host-migration warning.
- If co-op is ever promoted from R&D to roadmap: black-box dump of last 300 network frames: positions, hashes, flags, authority, packet age.

## Task 08 - Vehicle Kinematics Benchmark

SN2 current signal:

- Tadpole/starter vehicle exists and is a launch feature.
- Some players and press want a Cyclops-equivalent large submarine.
- Reddit complaints call the Tadpole slow or lackluster; others like the base/resource loop.
- TechRadar explicitly says the big-sub fantasy has not revealed itself yet and hopes for something better than Below Zero's Seatruck.

HECTON-8 target:

- Stop chasing nimble toy vehicles. Own mechanical weight.
- Exosuit and submarine motion should have hydraulic latency, inertia, ballast response, center-of-mass slosh, and audio/mechanical feedback.
- Use continuous quality scaling: Low gets scalar drag/ballast and camera/audio weight; Ultra gets visual overkill from the same kinematic truth.

Vehicle feel order:

1. Starter scooter: fast traversal, weak pressure safety.
2. Exosuit: heavy, hydraulic, industrial, controlled inconvenience.
3. Submarine: multi-system machine, not just transport. Power, ballast, flooding, sonar, cargo, docking, and damage.

## Task 09 - Biome Density Comparison

SN2 screenshots/trailers sell density through clustered flora, fog, silhouettes, particles, and color staging. This is not proof of high runtime entity truth. It is disciplined composition.

HECTON-8 target density goal, evidence-labeled as `DESIGN_TARGET`, not measured proof:

| Tier | Near-field visual instances per 100m radius | Truthful gameplay entities | Method |
|---|---:|---:|---|
| Low/MX350 | 250-400 impostor/debris/flora instances | 10-25 active gameplay entities | BRG/impostors, sparse silhouettes, fog hiding budget |
| Middle | 800-1200 visual instances | 25-60 active gameplay entities | Object batches, GPU culling, biome fog authority |
| High | 1600-2500 visual instances | 60-120 active gameplay entities | Reactive silt, fauna stimulus, richer material masks |
| Ultra | 3000-4500 visual instances | 120-250 active gameplay entities | Dense overkill dressing, longer LOD residency, volumetric accents |

BRG Scatter goal:

- Beat SN2 perceived density by `2x` through staged clusters and fog composition, not raw GameObject counts.
- Enforce HECTON-8's existing draw/batch/VRAM budgets; no density target is accepted without Frame Debugger and Memory Profiler proof.

## Task 10 - Soundscape Analysis

SN2 strength:

- Press specifically praises underwater sound and visual water cues.
- Public footage/reviews indicate strong calm/terror tone.
- Some player reviews criticize horror stingers as mistimed or too "boss music" when threat is distant.

HECTON-8 counter:

- Do not use generic horror stingers for threat radius.
- Use hydrophone logic: sound arrives before silhouette.
- Sabine RT60 and enclosure index drive reverb.
- Granular synthesis creates hull stress and pressure groans.
- Acoustic radar and sonar pips reveal threat direction but not certainty.
- Creature audio must be tied to stimulus state: hunt, curiosity, occlusion, hull noise, blood, power draw.

Low/Middle/High/Ultra:

- Low: zone-based filtering, scalar pressure creaks, low voice count.
- Middle: cached occlusion, RT60 per compartment/biome, radar pips.
- High: acoustic shadows, hull resonance, directional threat.
- Ultra: convolution/IR-like richness where budget proves it, never default.

## Task 11 - HECTON8 vs SN2 Gap Matrix

### Five Areas Where SN2 Is Superior Right Now

1. Market traction: franchise, 60k Steam review surface, Game Pass/Xbox reach, co-op conversation.
2. Public visual polish: UE5/Lumen-class lighting, colorful biomes, readable trailer composition.
3. Playable co-op exists today; HECTON-8 does not currently plan co-op and must not market it.
4. Base-building UX is publicly praised and player-visible.
5. Early Access cadence is operational: official release, roadmap, feedback tools, live community loop.

### Ten Areas Where HECTON-8 Can Be The Subnautica Killer If Verified

1. Low-end scalability below SN2's GTX 1660 6GB minimum: MX350/2GB VRAM target.
2. Deterministic AUP world: 100km ambition without float drift, if executed.
3. Zero-GC hot paths and explicit memory budgets.
4. Black-box telemetry: no "unknown crash" culture.
5. Pressure/flooding/structural truth: survival as engineering, not aquarium tourism.
6. Acoustic threat system: hydrophone dread, sonar uncertainty, hull resonance.
7. Heavy vehicle identity: ballast, hydraulic latency, compartment failures.
8. Hostile but readable ecology: defensive agency with ecological cost.
9. NASA-punk/noir art direction: corrosion, salt, instruments, industrial wrecks.
10. Local-first trust posture: no telemetry ambiguity, no hostile EULA narrative, rolling save backups.

## Task 12 - Player Loop Refinement: Scarcity/Bounty Curve

SN2 pain signal: resource grind, hard-blocking scarce resources, inventory/storage friction, and samey early loops show up in negative reviews and community posts. HECTON-8 needs scarcity without deadlocks.

Recommended Economy Surgeon curve:

```text
NeedPressure = saturate((RequiredUnits - KnownReachableUnits) / max(RequiredUnits, 1))
RouteFatigue = saturate(RepeatedTripsToSameResource / 5)
DiscoveryBounty = 1 + 0.65 * NewBiomeFactor + 0.35 * FirstScanFactor + 0.25 * HazardRisk
ScarcityMultiplier = 1 + 1.8 * NeedPressure^2 + 0.6 * RouteFatigue
GuaranteedRecovery = CriticalPathResource ? max(1, ceil(RequiredUnits * 0.20)) : 0
SpawnWeight = BaseWeight * DiscoveryBounty / ScarcityMultiplier
RouteHintWeight = NeedPressure^2 * (1 + RouteFatigue)
```

Rules:

- Critical-path resources must never be globally exhaustible without a recovery route.
- Scarcity should create route choice, not save death.
- Bounty must spike after risk: wreck breach, pressure zone, predator route, toxic plume.
- Repeated same-resource trips should trigger alternate route hints or storage-network assistance.
- Co-op multiplies demand by active player count but also increases bounty through role split.

Tier behavior:

- Low: fewer resource visuals, same deterministic availability.
- Middle: richer route hints and biome clusters.
- High: dynamic hazards alter route value.
- Ultra: overkill material/readability effects only; economy truth unchanged.

## Task 13 - Trailer / Footage Audit

Frame-by-frame local capture was not performed. `yt-dlp` was unavailable and YouTube comment/video scraping was not reliable in this environment. Steam page trailer metadata and public press footage/stills were inspected as accessible public references.

Findings:

- No reliable public source checked called out obvious trailer LOD pop-in.
- No reliable frame capture proves texture streaming lag. TechRadar reports only one minor texture glitch after several hours.
- PC Gamer performance/settings article implies heavy lighting/reflection cost around Lumen-class features.
- Public store/trailer footage uses controlled camera staging, short cuts, fog concealment, and dense composed foregrounds. That is valid trailer craft, not proof of open-world runtime consistency.

Agent 35 streaming bar:

- HECTON-8 chunks must crossfade or dither LOD transitions.
- Texture residency must be predicted by velocity and route.
- Far silhouettes can be impostor-only, but no hard billboard pop inside the player trust radius.
- Pop-in proof requires Play Mode route capture plus frame-by-frame screenshots, not docs.

## Strategic Work Orders

P0:

- FOV, sprint, mouse acceleration toggle, pinned recipe ledger, storage query, autosave/rolling backup.
- Shader warmup/cache and crash-safe graphics preset path.
- No public multiplayer claim. If future R&D changes that, require local loopback state hash before any mention.
- One iconic heavy vehicle and one first-hour industrial wreck route.
- Defensive agency against fauna without turning into a shooter.
- Explicit telemetry privacy/offline posture.

P1:

- Biome visual authority records: fog, silt, palette, audio, object-batch budgets.
- Acoustic threat pipeline: RT60, hydrophone radar, occlusion categories.
- Seed Ship corruption as a systemic narrative hook: AUP-stable anomaly field, not trigger volume.
- Tech-art grime pack: salt, rust, pressure dents, condensation, cable forests.

P2:

- Ultra-only visual overkill pack isolated from route truth.
- Trailer-proof capture harness: desktop + mobile/low-tier screenshots, canvas/Frame Debugger checks, no pop-in in first-hour route.

## Self-Reflection Audit

1. Did I rely on SN2 marketing bullet points?
   No. Official/storefront facts were used for release/features. Pain points came from Steam API/reviews, Reddit/community posts, and press impressions. YouTube top-liked comments were not accessible and were not fabricated.

2. Have I identified a specific technical flaw HECTON-8 can exploit?
   Yes. Public evidence supports performance/crash variance around DX12/shader/driver paths and heavy Lumen-class settings cost. HECTON-8 exploit is deterministic shader warmup, MX350-first budgets, and visual-fake-first lighting.

3. Is the HECTON-8 Counter-Tactics list actionable?
   Yes. It maps to existing domains: streaming, BRG scatter, audio DSP, kinematics, UI QoL, economy, tech art, and Seed Ship anomaly. Networking state remains optional R&D, not public positioning.

4. Did I study the Seed Ship concept?
   Yes, from current batch references. Stronger hook than SN2's current sci-fi story surface: a 5km-deep corrupted terraforming vessel whose scalar anomaly field corrupts gravity, radar, fauna aggression, flora toxicity, logs, flow fields, and UI. That is systemic horror through data, not cutscene lore.

5. Is this honest enough to make the Lead Architect uncomfortable?
   Yes. HECTON-8 currently loses to SN2 in shipped product proof, co-op availability, visual polish, and community traction. Co-op is not a planned HECTON-8 public feature, so the answer is not to promise it; the answer is to win a different fantasy. HECTON-8's superiority is conditional until runtime evidence exists.

## Final Verdict

SN2 is a strong target, not an easy victim. It has the audience. It has co-op. It has a polished public surface. It also exposes a predictable weakness: expensive UE5 visual assumptions, launch trust baggage, early-access QoL debt, shallow defensive agency, co-op persistence uncertainty, and missing heavy-machine fantasy.

HECTON-8 should not chase colorful wonder or "Subnautica with bigger co-op." It should ship industrial dread with deterministic systems: pressure, sound, corrosion, heavy vessels, black boxes, local-first trust, and scalable visual overkill bought with fake-first math.
