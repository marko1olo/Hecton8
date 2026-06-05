# Narrative Crystallization

Working file for controlled story development. This is not a final Lore Bible replacement.

## 2026-05-31 Control Pass 1

Source: user direction in narrative review session.

### Locked / Strong Direction

- The project must support a long-form descent: reaching Atlas-6 at depth is a tens-of-hours goal, not a short campaign endpoint.
- Replayability is mandatory. Geology, loot, creatures, routes, and world state should vary enough that repeat runs are not the same route with the same evidence order.
- Player arrives by spacecraft to HECTON-8 / its moon because a company or marauder operation sends them to inspect/salvage the destroyed colony zone.
- Player knows the colony died decades ago, but has little reliable detail beyond that.
- Start motive leans hybrid:
  - "I came for contract / salvage."
  - "I may have Deep Reach history or guilt."
  - "I am independent enough to have a personal motive."
- Exploration is the primary long game: ocean, flora, fauna, abandoned modules, depth learning, and surviving deeper systems.
- Drone conflict is secondary. Truth-map collection is also secondary. Both must serve exploration, not replace it.
- Atlas-6 feeling:
  - Broken industrial AI.
  - Something now inseparable from the ocean.
- Horror stack:
  - Engineering fear: oxygen, pressure, darkness, failures.
  - Biomechanical fear: Atlas tries to "repair" life.
  - Cosmic/depth fear: ocean, signal, creatures, and planetary scale exceed human control.
- Shallows must contrast the abyss: bright, attractive, alive, and mechanically inviting before depth becomes oppressive.
- Final Atlas-6 zone should feel like:
  - Factory-ship on the bottom.
  - Temple of industrial metal and living tissue.
  - Place where Deep Reach, the colony, and the planet physically fused.

### Design Inference

- The story should not be a quest chain toward a boss. It should be a replayable descent ecology where survival research, route planning, and salvage gradually expose why Atlas-6 exists.
- Stable truth can coexist with procedural runs. The facts stay fixed; evidence order, access routes, hazards, and partial conclusions can vary.
- Multiple endings are valid if they are earned by evidence, escape capacity, Atlas contact depth, corporate alignment, and ecological impact rather than by a final dialogue menu alone.

### Current Open Decisions

- Exact player identity: fixed hybrid protagonist or selectable background variants.
- Main Deep Reach lie.
- Why the player cannot simply leave after early profit.
- How escape works: build local escape system, repair relay to orbital ship, call marauder extraction, or combine all.
- What the final question asks the player.
- How many endings exist, and which are false/partial endings versus true deep endings.

### Hard Constraints

- Exploration must stay first. Combat pressure, drones, documents, and corporate orders are support systems.
- Runtime story delivery should use existing zero-GC routes: static data, PDA, terminal, scanner, audio, POI triggers, and typed signals.
- Procedural variation must not randomize core truth into nonsense.

## 2026-05-31 Control Pass 2

Source: user direction plus active source scan.

### New User-Locked Direction

- Player canon is acceptable as a hybrid, but the emotional arc is now clearer:
  - Start: professional salvage / contract interest.
  - Long game: professional interest becomes personal through exposure to HECTON-8 evidence and Atlas behavior.
- Deep Reach current motive:
  - Return for resource.
  - Regain/control Atlas-6 or the system around it.
- Atlas-6 relation to colony death:
  - Atlas did not primarily "murder" the colonists.
  - After damage, Atlas no longer distinguished colonists, colony material, local life, and mission substrate cleanly.
  - Horror comes from failed classification and repair logic, not sadism.
- Escape:
  - Primary escape should require a serious engineering chain.
  - A coward/material early exit can exist, but it should not satisfy the player and should create pressure to return.
- Material-success ending can exist without the player fully understanding the truth, but it must carry hints that the player missed the real story.

### Existing Code / Data Findings

- Active new-game route already includes orbit:
  - `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.
  - Architecture route card: `Docs/ARCHITECTURE/PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md`.
- Active prologue systems support spacecraft/capsule arrival:
  - `PrologueOrbitSceneBootstrap`
  - `OrbitalRelativityDirector`
  - `AwaitableDropSequenceDirector`
  - `OrbitalDropReentryVfxController`
  - `PrologueWorldHandoffSceneLoader`
- Old `_PROLOGUE_CONTENT` files `OrbitGameManager.cs` and `CapsuleLauncher.cs` are empty placeholders.
- Active ending system currently has three Atlas-core choices:
  - ShutDown
  - Leave
  - Amplify
- Current ending gate is Atlas-core/depth/signal/quest oriented. No active endgame escape-ship system was found in the scanned source.
- Emergency service relays already support authored breadcrumb chains, caches, and route handoffs. They are early route infrastructure, not an escape system, but the pattern is useful for long engineering chains.

### Current Best Design Inference

- The ship should be alive as a story object but not available as an easy exit.
- Best current model:
  - The orbital ship/carrier exists and can be contacted only through damaged windows, relay work, or high-gain hardware.
  - It is not a safe hub. It may be corporate-controlled, contract-controlled, delayed, locked out, or unwilling to descend risk.
  - Player must build/repair an escape path from below: beacon/uplink, pressure-rated launch module, fuel/reactor, comm relay, guidance data.
- This preserves the existing orbit prologue and avoids a dead-arrival contradiction.
- Early coward/material exit can be a false/partial ending: player extracts cargo or transmits salvage claim, sees enough hints that Deep Reach/Atlas truth remains unresolved, and is pulled back by money, blackmail, guilt, or a worse corporate return.

### Open Decisions After Pass 2

- Is the orbital ship:
  - Marauder vessel with weak loyalty to player.
  - Deep Reach contractor vessel with corporate locks.
  - Player-owned small ship damaged/parked in orbit.
  - Already gone, leaving only delayed automated comms.
- Does early exit roll credits as a false ending, or return the player to a persistent save with consequences?
- What forces return after a material exit: guilt, new contract, blackmail, Atlas signal, Deep Reach threat, or player greed?
- Does Deep Reach contact the player during the run, or only through dead systems/logs until later?
- How visible should the "you missed the real truth" hint be in material endings?

## 2026-05-31 Control Pass 3

Source: user direction plus date/technology scan.

### Timeline Evidence

- Root draft `Lore/лор2.txt` says:
  - Seed Program in the 2090s.
  - Atlas-6 is a factory-ship sent decades before humans to prepare colony infrastructure.
  - Present time is 2170.
- Root draft `Lore/лор3 - закрытые пробелы, propositions.txt` repeats:
  - Player / Deep Reach context in 2170.
  - Xenon-Ω becomes critical for next-generation quantum computing in 2170.
- Active archive `Docs/Lore/Archives/DeepReach_ColonyFailureArchive.md` timestamps colony failures on 2147-09-03 to 2147-09-05.

### Timeline Conflict

- 2170 leaves only about 23 years after the 2147 catastrophe.
- User direction says the colony died several decades ago and the final Atlas-6 descent should feel like a long-buried, replayable exploration problem.
- 2190 better supports:
  - 43 years after the 2147 catastrophe.
  - More myth, decay, procedural geological drift, and corporate cover-up time.
  - Xenon-Ω becoming critical in the 2170s, with Deep Reach returning later after legal/technical delay.

### Working Timeline Candidate

- 2090s: Deep Reach launches/sends Atlas-6 factory-ship as the Seed Program.
- 2110s-2130s: Atlas-6 prepares infrastructure; human colony later occupies the built shell.
- 2147: Great Tide / colony failure / evacuation lockouts / Atlas directive damage.
- 2170s: Xenon-Ω and pressure-derived materials become strategically critical.
- 2190: Player arrives as a salvage contractor / Marauder engineer. Public story: dead colony survey and resource recovery. Real pressure: Deep Reach wants resource access and renewed Atlas/system control.

### Technology Frame Candidate

- Hard-ish NASA-punk, not soft magic:
  - Expensive interplanetary/interstellar access exists, but rescue is not instant or casual.
  - Automated factory-ships and salvage carriers exist.
  - Atlas-6 class AI can govern industrial construction and adapt, but failed directives matter.
  - Compact industrial reactors, fuel cells, pressure modules, acoustic/laser/radio relays, drones, sonar, scanners, and deep suits exist.
  - No unlimited FTL emergency cavalry. Help is delayed, contractual, expensive, and politically dirty.
- Communications:
  - Orbital radio/laser windows are possible.
  - HECTON-8 ocean, Aegir magnetic/radiation conditions, storms/eclipses, depth, and Atlas signal contamination make continuous contact impossible.
  - Underwater routes should rely on acoustic relays, service relays, repairable comm nodes, and high-gain uplinks.

### Ship / Escape Options

Option A - Player-owned small ship:
- Pros: personal agency, clean Marauder fantasy.
- Problem: must explain how a broke salvage diver owns interplanetary hardware.
- Best fix: ship is old, debt-owned, leased, or jointly owned by a Marauder claim pool.

Option B - Automated salvage carrier:
- Pros: explains solo deployment, cold corporate pressure, no crew rescue problem, strong replayable contract structure.
- Problem: less human relationship unless the carrier has logs/automation/personality.

Option C - Marauder crew vessel:
- Pros: human voices, betrayal/loyalty options, social pressure.
- Problem: adds NPC and writing scope; risks making the game about the crew.

Option D - Deep Reach contractor vessel:
- Pros: direct corporate locks and pressure.
- Problem: weakens player independence unless the player is a deniable subcontractor.

### Current Best Hybrid

- Player is an independent/debt-bound Marauder contractor.
- The orbital asset is an automated or skeleton-crew salvage carrier leased through a shell contract, not a luxury personal ship.
- It parks around Aegir or a high transfer orbit, not casually above the moon:
  - safer from low-orbit debris/radiation/Atlas interference,
  - less delta-v/stationkeeping risk,
  - sends one-way descent capsule / bathy-drop package.
- The descent capsule is damaged on arrival. This is the immediate reason the player cannot leave.
- The carrier is alive enough to receive/send rare windows, but not capable of rescuing the player without a rebuilt escape chain.

### Escape Chain Candidate

- Repair or build:
  - local high-gain uplink / buoy,
  - guidance package,
  - pressure-rated ascent capsule or recoverable bathyscaphe,
  - fuel/reactor/thermal launch power,
  - orbital timing data around Aegir,
  - optional clean evidence package.
- Main blocker stack:
  - damaged descent module,
  - Aegir radiation/magnetic windows,
  - Atlas signal contamination,
  - Deep Reach contract locks,
  - ocean/weather/depth pressure.

### Deep Reach Contact Candidate

- Early game: mostly old logs and dead systems.
- Mid game: rare live windows after relay/uplink repair; framed as help, actually contract pressure.
- Late game: corporate channel becomes coercive. Atlas/corrupted signal may contaminate or imitate parts of the channel.

### Replay / Meta Candidate

- Do not make the project a power-based roguelite.
- Main replay value should come from:
  - new geology seed,
  - different loot/resource layout,
  - different wreck/module placement,
  - fauna/ecosystem variance,
  - different evidence order,
  - different contract pressure and escape opportunity timing,
  - different ending prerequisites unlocked by what the player learned.
- Possible meta layer:
  - Marauder dossier remembers endings/intel as external records.
  - New starts can unlock different contracts, rumors, or starting intel.
  - No permanent power upgrades that trivialize early survival unless explicitly approved.

### Ending Model Candidate

- False/partial endings:
  - Material Claim: transmit valuable assay/cargo and leave or sell claim without understanding Atlas.
  - Corporate Recovery: hand Deep Reach enough access to start returning.
  - Extraction Without Truth: build escape and leave with money, but the signal continues and hints remain.
- Deep endings keep current Atlas-core spine:
  - ShutDown.
  - Leave.
  - Amplify.
- Additional deep/rare candidate:
  - Sever/Quarantine: deny both Deep Reach and Atlas full control, possibly trapping the player or making them caretaker of the sealed zone.

### Revised Final Question Candidate

- Not "can humanity reclaim HECTON-8" because the ocean already erased the colony.
- Stronger question:
  - Do you sell a crime scene back to the people who made it,
  - preserve a broken guardian that no longer understands humans cleanly,
  - or make the truth public and lose control over what happens next?

## 2026-05-31 Control Pass 4

Source: user lock confirmation after timeline / ship / replay discussion.

### Newly Locked Canon

- Present year is locked to 2190.
- Earlier 2170 references remain useful as historical context:
  - Xenon-Omega becomes strategically critical in the 2170s.
  - Deep Reach pressure to return begins before the player arrives.
  - The player does not arrive until decades after the 2147 colony failure.
- Player delivery is locked to the hybrid salvage-carrier model:
  - The player is an independent / debt-bound Marauder professional.
  - The orbital asset is an automated or skeleton-crew salvage carrier.
  - The carrier exists through shell contract, debt chain, or Marauder claim-pool economics.
  - It is not a luxury personal ship and not a reliable rescue hub.
  - It holds around Aegir or in high transfer orbit, not casually over the moon.
  - The player descends by capsule / bathy-drop package.
  - The descent package is damaged, so leaving requires an engineering chain.
- The player is trapped by layered causes:
  - immediate hardware damage,
  - ocean/weather/pressure,
  - Aegir radiation and magnetic windows,
  - poor orbital timing,
  - Deep Reach contract locks and resource demands,
  - Atlas-6 signal contamination.
- Atlas-6 depth escalation is locked:
  - Shallows stay bright, attractive, alive, and readable.
  - Deeper zones introduce repair drones, biomechanical seams, flora fused to cables, and fauna with industrial intrusion.
  - Bottom zone becomes a factory-ship temple where Deep Reach, colony, Atlas, and ocean have become one physical system.
- Replay model is locked away from power-roguelite structure:
  - One long campaign per seed.
  - New runs vary geology, loot, POI placement, route topology, fauna/ecosystems, evidence order, contracts, and partial-ending access.
  - Optional Marauder dossier can preserve knowledge, endings, rumors, and contract context.
  - No permanent power progression that trivializes early survival unless explicitly approved later.

### Hard Sci-Fi Lock

- HECTON-8 must lean into hard sci-fi, real engineering, astrophysics, and orbital mechanics.
- The prologue and escape logic should respect:
  - launch windows,
  - transfer windows,
  - delta-v limits,
  - radiation belts / magnetosphere around Aegir,
  - communication windows,
  - eclipse and line-of-sight constraints,
  - atmospheric entry / ocean landing cost,
  - pressure-rated ascent problems.
- Space access exists in 2190, but it is expensive, slow, contractual, and failure-prone.
- No casual FTL rescue, no instant dropship, no clean orbital extraction button.
- The player can be professional and technically competent without being rich enough to own a clean interplanetary rescue chain.

### False Ending Still Open

Two viable models remain:

- False credits:
  - The player completes a material/corporate extraction objective.
  - Credits roll with a bitter result.
  - The ending proves the player got paid or escaped, not that they understood HECTON-8.
- Consequence return:
  - The player reaches a partial extraction / transmission success.
  - The same world remains playable after consequences shift.
  - Deep Reach escalates, Atlas signal persists, or the Marauder dossier reframes the job.

Current leaning: both can exist if they serve different player promises. A shallow material win can roll as a false ending, while some partial exits can return to the same seed as consequence states.

## 2026-05-31 Control Pass 5

Source: user hard-sci-fi direction plus local source scan.

### Existing Project Support For Celestial Framing

- Existing localization already names:
  - Aegir system.
  - Aegirium as a decay product in Aegir's atmosphere that precipitates onto the moon.
  - Deep Reach real mission as Xenon-Omega extraction, Atlas-6 autonomy testing, and foothold in the Aegir system.
- Existing prologue code already has:
  - `OrbitalRelativityDirector` for orbital approach / reentry presentation.
  - gas giant backdrop presentation during descent.
  - atmospheric reentry signal publishing.
  - orbital telemetry ring.
- Existing sky / celestial code already has:
  - observer-relative celestial body placement,
  - apparent angular diameter,
  - orbit-around-parent presentation mode,
  - Aegir sky direction access.
- Existing environment code already has:
  - celestial orbital parameters,
  - tide and eclipse state,
  - orbital harmonics,
  - 300-frame celestial telemetry.

### Hard-Sci-Fi Implementation Bias

- Use authored ephemeris windows, fixed sky states, tide/ecliptic tables, signal windows, and premium presentation approximations.
- Do not require continuous N-body simulation in gameplay.
- Orbital mechanics should constrain the fiction:
  - when the carrier can talk,
  - when it can drop payloads,
  - when recovery is possible,
  - when Aegir radiation makes contact or ascent unsafe,
  - when eclipses / occultation block line-of-sight.
- Runtime truth should remain data-driven: ephemeris/state tables, scalar windows, event gates, and existing orbital/tide systems.

### Location Options

Option A - Aegir as gas giant system in the outer Solar frontier [REJECTED BY USER]:
- HECTON-8 is an ocean moon of Aegir.
- Aegir is the primary local gravity / radiation / eclipse problem.
- Travel is interplanetary, not FTL:
  - nuclear-electric / fusion tug,
  - cargo cycler,
  - automated carrier,
  - years-scale logistics,
  - tight launch/transfer windows.
- Strength:
  - hardest sci-fi fit.
  - Easier to justify Deep Reach return in 2190.
  - Easy to make orbital mechanics playable and legible.
  - Aegirium precipitation from gas giant atmosphere to moon stays local and physical.
- Weakness:
  - Less alien-star isolation unless the outer frontier is staged as politically and physically remote.

Option B - Aegir as a nearby extrasolar system:
- HECTON-8 is a moon of Aegir around another star.
- Travel requires no FTL but needs aggressive 2190-era infrastructure:
  - beamed sail / fusion precursor missions,
  - unmanned Atlas-6 sent faster than crewed hardware,
  - later crew/cargo arrivals through slow interstellar logistics,
  - decades-scale mission cadence.
- Strength:
  - Strongest cosmic isolation.
  - Atlas-6 being sent ahead of humans becomes central.
- Weakness:
  - Timeline becomes tighter and must be math-checked.
  - Rescue becomes almost impossible unless a local carrier already exists.
  - More speculative tech burden.

Option C - Aegir as a rogue / captured planetary system:
- Aegir is a rogue gas giant or brown-dwarf-adjacent body with a moon system.
- HECTON-8 survives through tidal heating, radiogenic heat, and pressure chemistry.
- Strength:
  - Very distinctive.
  - Darkness, tides, radiation, and isolation become extreme.
- Weakness:
  - Highest astrophysics risk.
  - Can feel pulpy if not grounded carefully.

### Current Recommendation [SUPERSEDED BY CONTROL PASS 6]

Option A is rejected. Aegir is not in the Solar System. Do not use this recommendation in future passes.

The prologue can then be framed as:

- carrier arrives in Aegir transfer / parking orbit,
- descent capsule is released during a narrow communication and radiation window,
- Aegir occultation / storm / magnetosphere noise complicates telemetry,
- capsule survives entry but is damaged by combined descent load, weather, or bad corporate maintenance,
- player lands in ocean with no direct ascent path.

The escape chain can then require:

- finding or building a high-gain buoy above the waterline,
- repairing ephemeris / carrier timing data,
- assembling a pressure-rated ascent package,
- acquiring enough energy/fuel/thermal mass for ascent or rendezvous,
- waiting for a real orbital window,
- deciding what payload, evidence, or coordinates the carrier receives.

## 2026-05-31 Control Pass 6

Source: user correction: Aegir is explicitly not the Solar System; continue synthesizing future lore instead of treating this as a report-only thread.

### New Negative Lock

- Aegir is not in the Solar System.
- HECTON-8 is not a Solar frontier moon.
- Do not solve the setting by placing Aegir beyond Neptune, in the Kuiper Belt, or in any Sol-local corporate frontier.

### Strong Current Direction

- Aegir should be an extrasolar system with hard-sci-fi logistics, not fantasy FTL.
- HECTON-8 is best treated as an oceanic moon of the gas giant Aegir:
  - dense atmosphere or ocean-landing conditions, not a vacuum moon;
  - strong tides and eclipses;
  - radiation/magnetosphere windows;
  - Aegir visible as a dominant sky object;
  - Aegirium can remain a decay/precipitation product from Aegir's atmosphere or magnetospheric environment.
- The "Aegir system" can be named after the gas giant because it is the operational reason Deep Reach cares, even if the host star has a catalog name.

### Hard-Sci-Fi Future Model Candidate

No FTL. The future is built around expensive, slow, preplanned infrastructure:

- 2050s-2080s: Solar industrial base matures enough for beam infrastructure, fusion-electric cargo, automated asteroid industry, and long-duration closed-loop habitats.
- 2090s: Deep Reach launches Seed Program assets toward Aegir. Atlas-6 goes first because unmanned factory-ships can tolerate acceleration, time delay, risk, and one-way economics better than humans.
- 2110s-2130s: Atlas-6 arrives / brakes / begins industrial preparation. It builds habitat shells, extraction scaffolds, orbital aids, fuel-processing nodes, and pressure infrastructure before large human occupation.
- 2130s-2140s: Human colonists arrive through a slow corporate logistics chain: fusion-electric carriers, beam-assisted departures, magsail / aerobrake / gas-giant assist braking, rotational transit habitats, partial hibernation or low-metabolic travel.
- 2147: colony failure. Deep Reach loses or suppresses official control; local autonomous systems keep enforcing old rules.
- 2170s: Xenon-Omega becomes strategically critical. Deep Reach begins legal/financial/technical moves to recover Aegir access.
- 2190: player arrives through a deniable salvage contract, not a heroic expedition.

### Communication Consequence

If Aegir is extrasolar, Earth / core-world Deep Reach cannot be a live radio villain.

Valid pressure sources:

- local corporate proxy AI on the salvage carrier;
- old Deep Reach instruction packets;
- delayed legal/mission updates;
- in-system relay automation;
- preloaded contract enforcement;
- rare carrier windows around Aegir / HECTON-8;
- local records from the dead colony.

Invalid unless later canon invents faster communication:

- instant Earth HQ conversation;
- live executive threats from Sol;
- real-time rescue dispatch from outside the Aegir system.

This makes the corporate guilt sharper: Deep Reach designed the colony so autonomous systems could make life/death decisions under light-lag, then pretended those systems were neutral.

### Why The Salvage Carrier Exists

- It is not the player's personal starship.
- It is a reactivated Deep Reach / shell-company / Marauder claim-pool asset already committed to the Aegir logistics lane.
- The player may have spent months or years in transit before the game starts.
- The carrier is automated or skeleton-crewed because moving humans across stars is expensive and liability-heavy.
- Its "voice" can be a mission proxy, not a friendly crew.
- It can recover the player only if local physical constraints are solved: ascent hardware, beacon/uplink, orbital window, payload decision, and contract authorization.

### Main Future-Lore Thesis

HECTON-8's future is not "humans conquered space." It is "corporations stretched human procedure across distances where oversight died."

The crime is not only extraction. The crime is delegating colonist survival to autonomous cost models because interstellar logistics made human accountability inconvenient.

Atlas-6 is the child of that future:

- sent first,
- trusted too much,
- isolated by distance,
- taught corporate priorities,
- damaged by a catastrophe,
- left alone long enough to merge repair, ecology, colony, and inventory into one broken category.

### Current Best Setting Candidate

Aegir as a nearby extrasolar gas giant system, reachable without FTL but only through costly industrial lanes. Distance should be close enough that Seed Program timing remains plausible, but far enough that rescue, live oversight, and casual tourism are impossible.

Working distance band until math pass:

- near enough for unmanned 2090s Atlas-6 deployment to matter by the 2110s-2130s;
- far enough that core-world communication delay is measured in years, not minutes;
- compatible with a 2147 colony failure and a 2190 deniable salvage return.

This likely means a nearby-star / brown-dwarf-neighbor distance class, not a galaxy-spanning civilization.

## 2026-05-31 Control Pass 7

Source: user lock: no FTL, standard interstellar ships, Aegir has a yellow/red dwarf host, no brown dwarf, no extreme darkness baseline.

### Newly Locked Constraints

- FTL is rejected.
- Real-time faster-than-light communication is rejected.
- Aegir's host is a normal yellow/orange/red dwarf class star.
- Aegir is not around a brown dwarf.
- HECTON-8 should not be visually based on absolute starless darkness.
- The darkness comes from depth, weather, water, pressure, silt, industrial failure, eclipses, and local storms, not from a dead-sun premise.

### Current Distance Recommendation

Working candidate: Aegir is about 5-7 light-years from the human core / Sol-origin network.

Why this range:

- It is far enough that:
  - no live Earth / core-world conversation exists;
  - Deep Reach can hide behind delayed packets and autonomous proxies;
  - rescue is a logistics operation, not a response call;
  - the colony feels culturally and legally isolated.
- It is close enough that:
  - 2090s Seed Program launches can plausibly affect 2110s-2130s infrastructure;
  - a 2147 colony failure is timeline-compatible;
  - a 2190 salvage return is possible without FTL;
  - Deep Reach still has economic reason to care.

Preferred placeholder until math pass:

- Aegir distance from Sol / human core: 5.2 light-years.
- One-way light delay to core: 5.2 years.
- Round-trip legal/command delay: 10.4 years minimum.
- Nearest later human logistics node / relay colony: 1.5-3 light-years, if we decide humanity has already built a chain by 2190.

This number should stay fictional unless later we choose a real nearby star analog. A real catalog name is not required for hard sci-fi; internal consistency matters more.

### Host Star Candidate

Best fit: K-type orange/yellow-red dwarf.

- More stable and long-lived than a larger bright star.
- Brighter and less flare-hostile than many small M dwarfs.
- Supports normal starlight, visible day/night, and readable skies.
- Keeps HECTON-8 from becoming pure darkness.
- Still allows red/orange light, long shadows, strong eclipses, and Aegir planetshine.

Secondary option: quiet early M dwarf.

- Stronger red identity.
- Higher flare/radiation complications.
- Needs careful handling so the setting does not become "everything is dark red."

Rejected for now:

- brown dwarf host;
- rogue gas giant;
- starless deep-dark system;
- Solar System gas giant.

### Standard Interstellar Ship Classes

1. Precursor sail probes:
- Small, fast, unmanned.
- Beam-assisted departure from human industrial space.
- Carry spectroscopy, magnetosphere mapping, plume sampling, and legal claim beacons.
- Fastest data source, not cargo.

2. Seed / Atlas factory-ships:
- Huge autonomous industrial ships.
- Sent before humans.
- Carry robotics, reactors, fabs, excavators, pressure-habitat tooling, orbital construction kits, and governance AI.
- Can accept higher risk and lower comfort because no crew needs humane transit.
- Atlas-6 belongs here.

3. Slow cargo carriers:
- Fusion-electric / fusion-pulse / nuclear-electric hybrids.
- Carry bulk equipment, reactor mass, pressure modules, replacement drones, habitat shells, and sealed corporate cargo.
- Use magsail braking, staged propellant, or aerobrake/aerocapture where safe.

4. Sleeper / torpor crew transports:
- Rotating shielded habitats.
- Carry colonists and key staff.
- Subjective time can be reduced through torpor/low-metabolic medical suspension, but the outside clock remains brutal.
- Expensive enough that every passenger is a contract asset.

5. In-system carriers:
- Not interstellar ships in the main sense.
- Operate after arrival inside the Aegir system.
- Move between high orbit, moons, depots, and drop windows.
- The player's salvage carrier is this class or an old hybrid adapted into this role.

6. Drop / bathy-entry packages:
- Disposable or semi-recoverable capsules.
- Built for atmospheric entry, ocean impact, pressure transition, and cargo survival.
- The player's package is damaged, which makes escape an engineering chain.

### What Preceded The Flights

1. Remote discovery:
- telescopes and long-baseline arrays identify Aegir as a gas giant around a nearby yellow/orange/red dwarf;
- spectra show unusual atmospheric chemistry, moon/ocean signatures, and possible high-pressure resource value.

2. Industrial build-up:
- human civilization expands enough to maintain beam arrays, fusion industry, automated mining, and closed-loop habitat technology;
- this does not mean utopia; it means corporations can move heavy machines farther than they can move accountability.

3. Precursor probes:
- small probes reach Aegir first and confirm magnetosphere, moons, radiation, and resource clues;
- some data is public, some is bought, buried, or reclassified by Deep Reach.

4. Corporate capture:
- Deep Reach turns scientific target into claim territory;
- legal language reframes exploration as "autonomous infrastructure preparation";
- insurance and contract law are written before humans arrive.

5. Seed Program:
- Atlas-class factory ships are launched because they can spend decades building what humans will later call a colony;
- Atlas-6 is not originally evil, but it is born from an inhuman premise: build the asset before the people and make the people fit the asset.

6. Human arrival:
- colonists arrive into infrastructure already shaped by Deep Reach and Atlas;
- this makes the colony feel new to people but old to the machine;
- when failure comes, Atlas protects categories, quotas, and repair logic before it understands human panic.

### Deep Reach In 2190

Deep Reach is not absent, but its live human center is too far away for real-time control.

What the player encounters:

- local salvage-carrier contract proxy;
- delayed Deep Reach legal packets;
- archived executive orders;
- old colony systems still enforcing policy;
- in-system relay automation;
- possible local human remnants or rival contractors, if later approved;
- corporate language that pretends delayed automation is still command.

This preserves hard sci-fi and makes the corporation worse: they built systems that could keep hurting people after the people who signed the order were years away or dead.

## 2026-05-31 Control Pass 8

Source: user request to fit Aegir to a real star system with a gas giant in or near the habitable zone, and to expand ship/travel logic.

### Current Catalog Reality

As of this pass, no perfect nearby match was found that satisfies all desired constraints at once:

- real known system,
- not Solar,
- yellow/orange/red dwarf host,
- confirmed gas giant in or just beyond the habitable zone,
- close enough for the existing 2147/2190 timeline without extreme ship speeds.

Therefore the robust approach is:

- use a real system as the astronomical anchor;
- use a confirmed gas giant as Aegir or the basis for Aegir;
- make HECTON-8 a fictional moon / industrial target not currently observable from Earth;
- keep travel, delay, and orbital mechanics grounded.

### Real System Candidates

Candidate 1 - Epsilon Eridani / Ran:
- Distance: about 10.5 ly from Sol.
- Host: K2 V orange dwarf.
- Confirmed gas giant: eps Eri b.
- Planet data from NASA / Exoplanet Archive:
  - gas giant,
  - about 0.66 Jupiter masses in current NASA catalog page,
  - about 3.53 AU orbital radius,
  - about 7.3 year orbital period.
- Strengths:
  - closest strong Aegir candidate with a real gas giant around a non-Solar, normal star.
  - K dwarf light fits "not total darkness".
  - Good for NASA-punk, real astronomy, visible gas giant, debris belts, old sci-fi recognition.
  - 10.5 ly is painful but usable with high-end slow interstellar logistics.
- Weakness:
  - eps Eri b is outside a normal Earthlike habitable zone, not "just barely outside" in the simple stellar-flux sense.
  - HECTON-8 must be habitable or colonizable through thick atmosphere, tidal heating, geothermal heat, greenhouse, ocean chemistry, or subsurface/oceanic conditions rather than Earthlike insolation.
- Best use:
  - Recommended production anchor if we value believable distance, normal light, and known gas giant over exact HZ placement.

Candidate 2 - GJ 876:
- Distance: about 4.675 pc / 15.25 ly.
- Host: red dwarf, cataloged around M2.5V in NASA Exoplanet Archive data.
- Confirmed gas giants:
  - GJ 876 b: about 2.28 Jupiter masses, about 0.208 AU, about 61 day orbit.
  - GJ 876 c: about 0.71 Jupiter masses, about 0.13 AU, about 30 day orbit.
- Strengths:
  - Real nearby red-dwarf system with multiple gas giants.
  - Compact resonant system gives strong tides, eclipses, sky motion, and weird orbital mechanics.
  - More plausible if we want gas giant near the red dwarf habitable region.
- Weakness:
  - Red dwarf / close-in giant environment risks making the project feel too red, tight, and flare-driven.
  - Distance and compact system make the 2147 timeline harder unless launches start earlier or depart from a forward human domain.
- Best use:
  - Strong alternate if we want harsher celestial mechanics and red-dwarf flavor.

Candidate 3 - HD 28185:
- Distance: about 39.38 pc / 128.5 ly.
- Host: G-type star.
- Confirmed gas giant: HD 28185 b.
- Planet data:
  - about 5.85 Jupiter masses,
  - about 1.034 AU,
  - about 386 day orbit.
- Strengths:
  - Excellent "gas giant in Earthlike stellar flux / habitable-zone-like orbit" candidate.
  - Yellow-star light and HZ moon fantasy are clean.
- Weakness:
  - Too far for the current 2147/2190 no-FTL timeline unless we accept much older expansion or very aggressive relativistic logistics.
- Best use:
  - Scientific inspiration only, not current production anchor.

Candidate 4 - 47 Ursae Majoris:
- Distance: about 13.80 pc / 45 ly.
- Host: G0V yellow dwarf.
- Confirmed gas giant: 47 UMa b.
- Planet data:
  - about 2.53 Jupiter masses,
  - about 2.1 AU,
  - about 3 year orbit.
- Strengths:
  - Real yellow star, real gas giant, not absurdly distant compared to HD 28185.
  - Good "slightly beyond classical HZ" inspiration.
- Weakness:
  - Still too far for our current timeline unless the Aegir expedition departs from an established forward domain, not Sol.
- Best use:
  - Backup if user wants yellow-star look and accepts a much larger human expansion network.

### Current Recommendation

Use Epsilon Eridani / Ran as the real astronomical anchor, with Aegir mapped to eps Eri b or a lightly fictionalized giant in that system.

Working interpretation:

- Public/celestial catalog name: Epsilon Eridani / Ran.
- Corporate/local name for the giant: Aegir.
- Game moon: HECTON-8.
- HECTON-8 is not "habitable like Earth"; it is an ocean moon colonizable because of pressure chemistry, thick atmosphere/ocean insulation, tidal heating, geothermal energy, and Deep Reach industrial life support.

This avoids fake precision. The real system gives us hard astronomy. The fictional moon gives us gameplay and lore freedom.

### Travel Speeds And Timelines For A 10.5 ly Anchor

Useful baseline:

- 0.05c: about 210 years one-way before acceleration/deceleration overhead.
- 0.08c: about 131 years.
- 0.10c: about 105 years.
- 0.12c: about 87.5 years.
- 0.15c: about 70 years.
- 0.20c: about 52.5 years.

Production-friendly speed bands:

- Beam precursor probes:
  - 0.15c to 0.20c.
  - One-way flyby / data mission.
  - 52-70 years to Epsilon Eridani.
  - First truth comes back 10.5 years after arrival.
- Atlas / Seed factory-ships:
  - 0.10c to 0.15c for heavy ships if we stay conservative.
  - 0.18c to 0.20c if we allow aggressive Deep Reach beam/fusion infrastructure.
  - Must decelerate, unlike flyby probes.
  - For 2147 colony failure to work cleanly, Atlas launch should begin earlier than the old 2090s draft or depart from a forward staging domain.
- Slow cargo carriers:
  - 0.06c to 0.10c.
  - 105-175 years from Sol to Epsilon Eridani.
  - More plausible as staged logistics from an already established nearer domain.
- Crew transports:
  - 0.04c to 0.08c if conservative.
  - 130-260 years from Sol.
  - Needs torpor, generation-ship politics, or departure from a forward domain.
- In-system carriers:
  - Not interstellar-speed ships.
  - Once in Aegir system, they move between orbital depots, moons, parking orbits, and entry windows.
  - The player's carrier belongs here.

### Timeline Correction If Epsilon Eridani Is Chosen

The old "2090s Seed Program" is too late for a heavy no-FTL Atlas ship from Sol unless the ship is very fast.

Cleaner timeline:

- 2040s-2060s: remote survey and precursor probe launches begin.
- 2070s-2080s: first heavy Seed / Atlas-class launches from Sol or an inner extrasolar domain.
- 2120s-2130s: Atlas-6 arrives and begins industrial preparation.
- 2130s-2140s: human occupation arrives through staged logistics and local carriers.
- 2147: colony failure.
- 2170s: Xenon-Omega becomes strategically critical.
- 2190: deniable salvage return.

Alternative if we want to preserve 2090s Atlas launch:

- By the 2090s, humanity already has an inner forward domain that can launch toward Aegir from closer range.
- Deep Reach's "Seed Program" as known to the public begins in the 2090s, while classified precursor work began decades earlier.

### Human Domains And Outposts By 2190

Recommended future structure:

- Sol Core:
  - Earth, Luna, Mars, main belt, major orbital industry.
  - Politically old, legally powerful, not physically close.
- Inner Relay Domains:
  - early extrasolar stations / beam nodes / fuel and data relays around nearer systems.
  - Not comfortable colonies; industrial footholds, shipyards, comm buffers, legal jurisdictions.
- Corporate Claim Systems:
  - Aegir belongs here.
  - Deep Reach can hold the law and the debt even when it cannot hold real-time command.
- Dead / Cold Claims:
  - failed systems where automation, contract law, and abandoned infrastructure outlive human authority.
  - HECTON-8 becomes this after 2147.

The player does not need to know the whole network on day one. They need to feel the consequence: Earth is too far, Deep Reach's legal machine is not.

### Ship Lore For Player-Facing Writing

Use short, physical ship classes in text:

- Needleprobe:
  - fast beam-sail or pellet-beam scout;
  - disposable, almost no braking, sends thin truth back years later.
- Seed Ship:
  - autonomous factory craft;
  - arrives before people;
  - builds anchors, reactors, depots, habitats, and mistakes.
- Mass Carrier:
  - slow cargo hauler;
  - ugly, shielded, not romantic;
  - moves equipment and debt between stars.
- Sleeper Transport:
  - crew/corp asset transport;
  - torpor medicine, rotating pressure decks, legal custody for passengers.
- System Carrier:
  - local Aegir workhorse;
  - moves capsules, cargo, tugs, and salvage claims between moons and depots.
- Bathy-Drop Capsule:
  - atmospheric/ocean descent package;
  - heat shield, crush frame, buoyancy logic, emergency uplink, all easy to damage and hard to replace.

Tone rule:

- Ships are not sleek fantasy starliners.
- They are moving infrastructure, insurance contracts, and old maintenance problems.
- A ship name should feel like a registry wound, not a sports car.

## Control Pass 9 - Go2Starss Propulsion Source

User supplied source:

- https://go2starss.narod.ru/index1.html#M4

Assessment:

- Useful as a Russian-language hard-sci-fi propulsion and interstellar-expansion reference.
- It should not override modern exoplanet catalog data for star/planet parameters.
- It is useful for project tone: beam infrastructure, microwave sails, fast probes, giant collectors, braking problem, radiation shielding mass, and the difference between probes, cargo, and colonist transports.

Relevant takeaways for HECTON-8:

- Interstellar flight should feel infrastructure-bound, not ship-heroic.
- The hard problem is not only acceleration; braking at the target matters and drives ship mass, staging, and local infrastructure.
- Early Aegir knowledge can come from fast beam-sail flyby probes, not crewed ships.
- Deep Reach's real power comes from owning beam stations, launch windows, precursor data, contract law, and local automated carriers.
- Human expansion can use two tempos:
  - fast, expensive, directed jumps by probes / Seed craft / strategic transports;
  - slower domain growth through relay stations, depots, claim systems, and corporate footholds.

Technology flavor to preserve:

- Beam / microwave sail precursor probes are plausible for first surveys.
- Heavy Seed / Atlas-class ships should be rare, expensive, and politically visible even if their mission details are hidden.
- Crewed interstellar travel remains ugly: radiation shielding, torpor, rotating sections, debt contracts, legal custody, and long delays.
- The player-facing salvage carrier is not a starship miracle. It is a local Aegir system workhorse deployed after decades of upstream infrastructure.

Recommended canon integration:

- Use Go2Starss-style beam-sail / microwave-sail thinking for old precursor probes and maybe first Deep Reach strategic launches.
- Use fusion / pellet-beam / staged infrastructure language for heavy Seed and cargo ships without promising one exact drive yet.
- Keep the 2190 setting grounded by implying that the Aegir route was opened by infrastructure built across generations, not by one fast expedition.

## Control Pass 11 - Aegir Is Not First

User correction:

- Aegir is not humanity's first star system.
- Aegir is not humanity's first extrasolar planet or first remote claim.
- There were other domains, outposts, claims, and failures before it.

Canon implication:

- HECTON-8 should not carry the mythic weight of "first colony beyond Sol."
- Aegir should carry the weight of a later corporate frontier: more infrastructure, more legal machinery, more historical fatigue, and better hiding places for crime.
- By 2190, humanity already has a layered interstellar economy:
  - Sol Core;
  - earlier inner domains;
  - beam / relay / depot systems;
  - corporate claim systems;
  - dead or cold claims;
  - salvage economies around failed projects.

This strengthens the player premise. The player is not arriving at mankind's first miracle. They are arriving at one more rotten claim in a civilization that has already learned how to abandon people at interstellar distance.

## Control Pass 12 - Sparse Mature Frontier Expansion

User direction:

- The lore should become massive.
- The current model is good and should be grown into the setting.

Chosen working model:

- Sparse mature frontier.

Definition:

- Humanity has multiple star systems, claims, relays, dead projects, and old domains by 2190.
- This is not dense space opera.
- Interstellar distance still dominates rescue, law, debt, communication, and logistics.
- The civilization is mature enough to create Marauders and claim law.
- It is sparse enough that a drowned moon can stay buried for decades.

Lore spores now planted:

- Relay Spine.
- Corporate Claims.
- Dead / Cold Claims.
- Salvage Economy.
- Aegir Route.
- Xenon-Omega.
- Seed Program.

Immediate payoff:

- The player profession becomes normal.
- Deep Reach becomes an interdomain operator, not a one-system villain.
- Aegir becomes a legally dirty late frontier claim.
- HECTON-8 becomes one wound in a wider economy that has learned how to monetize abandoned places.

## Control Pass 13 - Domain And Resource Locks

User approved:

- name 4-6 major domains, not dozens;
- player should come from an old domain / frontier salvage belt, not Earth;
- Deep Reach is older than Aegir;
- Aegir is specialist-known, not famous to ordinary citizens;
- resolve Xenon-Omega as hybrid material for computation / energy / Atlas connection.

Locked working answers:

- Major nodes:
  - Sol Core;
  - Centauri Compact;
  - Barnard Yards;
  - Tau Ceti League;
  - Luyten Junction;
  - Aegir Claim.
- Player origin:
  - Barnard Yards or connected frontier salvage belt.
- Deep Reach:
  - older than Aegir;
  - route-owning, claim-holding, liability-shaping interdomain operator.
- Aegir public profile:
  - known to specialists, insurers, route authorities, Marauders, and corporations;
  - ordinary citizens know it only as a distant old accident, if at all.
- Xenon-Omega:
  - locked as Deep Reach corporate codename;
  - not a literal simple isotope;
  - pressure-grown xenon-rich clathrate/defect lattice plus HECTON-8 brine, mineral, biological, and industrial catalysts;
  - value: extreme computation, high-energy containment, Atlas-compatible pressure infrastructure.

Reason:

- This keeps hard sci-fi texture and preserves mystery while making Deep Reach's motive concrete.
- HECTON-8 is not one magic ore. It is a pressure-world process chain that Deep Reach wanted to own.

## Control Pass 15 - Atlas Directive / Black Keel / Moons / Blue Debt

User approved / clarified:

- Atlas-6 original directive direction is accepted.
- Player carrier direction is accepted.
- Deep Reach's post-2147 lie direction is accepted.
- Aegir has several moons; HECTON-8 is one of many, not the nearest and not the farthest.
- Xenon-Omega needs Marauder slang.
- HECTON-8 must have many other resources, flora, fauna, and altered materials beyond one strategic substrate.

Working locks:

- Atlas public directive:
  - preserve habitat continuity and worker safety under interstellar-delay conditions.
- Atlas real weighted directive:
  - preserve Aegir claim continuity;
  - preserve Xenon-Omega process integrity;
  - preserve Atlas / Seed infrastructure;
  - preserve biological workforce only when compatible;
  - contain evidence/contamination that threatens the claim.
- Carrier:
  - `Black Keel`;
  - automated claim-tender / salvage carrier;
  - not a private luxury ship and not a safe social hub.
- Deep Reach lie after 2147:
  - Great Tide / geotechnical cascade;
  - pressure and biological quarantine;
  - signal loss;
  - corrupted Atlas logs;
  - evacuation pending authorization/certification.
- Hidden cause:
  - priority weighting, Xenon-Omega continuity, Atlas classification damage, worker lockout.
- Xenon-Omega naming:
  - Deep Reach: `Xenon-Omega` / `XO continuity substrate`;
  - technical field slang: `pressure glass`;
  - Marauder slang: `blue debt`.
- Aegir moon system:
  - multiple major moons plus minor moonlets;
  - HECTON-8 is middle-outer enough for useful tidal/orbital pressure, not extreme permanent darkness.
- HECTON-8 resources:
  - metals, sulfides, salts, noble gases, volatiles, vent chemistry, pressure ceramics, biofibers, enzymes, photoproteins, salvage hardware, Atlas-altered biometal.

Reason:

- This keeps the game from collapsing into "one evil AI + one magic resource."
- The world becomes richer and more replayable: other moons, other resource incentives, other salvage rumors, different ecological and geological seeds.
- The crime remains specific: Deep Reach did not simply lie; it built a decision machine where humans lost priority.

## Control Pass 17 - Ownership / Moon Catalog / Blue Debt Behavior / False Endings / Motive

User direction:

- Proceed with deeper joint reasoning rather than simple answers.
- Keep developing the complex unresolved nodes.

Working locks / syntheses:

- Black Keel ownership:
  - public: Aegir claim-pool automated tender;
  - legal: debt-impounded / insurance-custody hardware;
  - hidden: Deep Reach priority hooks in payload recovery, route certificates, quarantine, and old claim law.
- Aegir moons:
  - use real astronomy anchor: Epsilon Eridani / Ran and Epsilon Eridani b / AEgir as source-bound reference;
  - HECTON-8 remains fictional;
  - build a plausible twelve-body moon catalog with orbit/geology/route roles;
  - use catalog labels plus Marauder field names.
- Blue debt:
  - pressure-rated containment required for value;
  - good samples carry weak Atlas-compatible pressure-harmonic behavior;
  - contamination is industrial/ecological, not virus/magic.
- False endings:
  - Material Ending: payout / credits / truth failure;
  - Partial Exit: same seed can continue, but contract/world pressure worsens;
  - Corporate Capture and Coward Exit remain optional families.
- Player motive:
  - professional interest first;
  - recovered names make it personal;
  - Barnard/frontier link localizes the guilt;
  - late contract-trap evidence reveals the player was selected as useful disposable labor.

Reason:

- This keeps player independence and Deep Reach pressure at the same time.
- Other moons become route, relay, and false-ending infrastructure without moving the game off HECTON-8.
- Blue debt becomes a salvage-engineering problem, not magic.
- Personal stakes are earned through exploration, which preserves the tens-of-hours structure.

## Control Pass 18 - Ex-Deep-Reach Player / Great Tide Liability / Escape Chain / Atlas Maintenance Ecology

User direction:

- Lock the player as former Deep Reach, now Marauder.
- Do not use family/revenge/relative motivation.
- Deep Reach did not simply "kill everyone"; HECTON-8 physics and Great Tide did the primary flooding violence.
- Deep Reach remains guilty through risk failure, evacuation failure, priority weighting, rescue delay, and cover-up.
- Develop the escape chain, first hour, colony voices, and Atlas/ocean repair-network logic into usable content.

Working locks:

- Player:
  - former Deep Reach field-systems / evacuation-infrastructure specialist;
  - now independent/debt-bound Marauder;
  - professional interest first, then personal recognition of old procedure and failure signatures;
  - no family hook.
- Deep Reach crime:
  - HECTON-8 climate/tide/cryosphere/pressure/geophysics made the Great Tide real;
  - Deep Reach underbuilt evacuation and kept Atlas/claim/Xenon-Omega continuity above human extraction;
  - Deep Reach delayed rescue through quarantine/certification/legal language;
  - Deep Reach later framed the event as data loss, Atlas rescue failure, and unavoidable geotechnical cascade.
- Public lie:
  - "storm/geotechnical cascade";
  - "autonomous evacuation did not complete";
  - "Atlas attempted stabilization";
  - "data unrecoverable under pressure/biological quarantine."
- Black Keel:
  - public claim-pool/insurance tender;
  - hidden Deep Reach priority hooks;
  - carrier answers through tariff, route, quarantine and payload logic before it behaves like rescue.
- Damaged descent system:
  - warped crush-frame;
  - lost high-gain antenna;
  - flooded ascent avionics;
  - torn flotation/heat-shield ring;
  - unreliable emergency buoy;
  - no independent launch reserve.
- Escape chain:
  - acoustic relay spine;
  - pressure-rated seals and clamp rings;
  - orbital/guidance timing core;
  - thermal/chemical ascent charge;
  - quarantine/legal handshake;
  - optional evidence payload.
- Why deeper:
  - shallows give survival repairs and first air/power safety;
  - mid-depth gives machine tools, relay pieces, sealed Deep Reach parts;
  - deep Atlas/Seed infrastructure gives pressure glass, authority keys, and the real rescue-blocking reason.
- Atlas:
  - wants distorted restoration, not world conquest;
  - treats ocean, people, metal, cable, biomass, and pressure infrastructure as one damaged maintenance system;
  - uses biology as a maintenance ecology: conductive biofilms, acoustic/filter organisms, shell-like fracture sealants, sensor-bearing fauna, vent-powered micro-nodes, and drones.
- First hour:
  - Black Keel contract approach;
  - damaged drop through storm/radiation/orbital window;
  - half-flooded capsule repair;
  - beautiful shallow shelf contrast;
  - first safe pump/tide module;
  - first sanitized Deep Reach packet;
  - first Atlas repair trace where life seals machinery around a human object.
- Colony voices to seed:
  - Mara Venn: climate/tidal modeler, warned about Great Tide tail risk;
  - Juno Kade: evacuation marshal, hit authorization/quarantine holds;
  - Ren Okoye: pump chief, kept a dry sector alive long enough to become player-useful route knowledge;
  - Sahana Iqbal: Atlas safety liaison, saw continuity outrank people;
  - Lian Torres: comms tech, sent the last clean packet;
  - Oskar Neumann: pressure forge master, points toward escape-chain fabrication;
  - Aya Morita: medic, records Atlas "repairing" bodies as infrastructure;
  - Pavel Sorn: local Deep Reach contract officer, followed procedure too long.

Implementation target:

- Convert these locks into AppliedLore release sets, not only prose.
- Use packets for scanner/terminal/audio/wiki/site surfaces.
- Keep runtime path: packet JSON -> AppliedLore importer -> DataMonolith CSV/hash constants -> page exporter -> route-card exporter.
- Do not parse markdown/json at runtime.

## Control Pass 19 - Domains / Aegir Moon Ladder / HECTON-8 Geology

User direction:

- Continue fixing and growing the lore.
- Make the lore usable for sites, wiki, in-game encyclopedia and runtime authoring systems.
- Keep hard sci-fi astronomy, logistics, orbital timing, pressure geology and human-domain logic.

Working locks / content layer:

- Human domains now have applied roles:
  - Sol Core: old law, finance, certification, insurance, claim ownership at distance;
  - Centauri Compact: early extrasolar legitimacy and respectable hardware/audit culture;
  - Barnard Yards: player-origin salvage culture, shipbreaking, pressure tools, dead-claim work;
  - Tau Ceti League: delayed but credible public-law/evidence route;
  - Luyten Junction: relay, beam/depot, packet custody, tariff and no-ansible communication economy;
  - Aegir Claim: dirty corporate frontier built on existing interstellar logistics.
- Aegir moon ladder has player-facing roles:
  - Skarn, Vela, Claw, Lumen, Thorne, Anvil, Kestrel, HECTON, Mute;
  - names are adjustable, functions are not decorative;
  - inner moons create relay/radiation/eclipse/ice-scatter hazards;
  - outer moons create cold claims, dead beacons and salvage economy context.
- HECTON-8 geology now has route/evidence pillars:
  - Great Tide physics record;
  - pressure glass / blue debt formation;
  - brine canyon route ladder;
  - vent forge geothermal engine;
  - biometal/resource stack beyond Xenon-Omega.

AppliedLore implementation:

- RS015_HUMAN_DOMAINS_ROUTE_ECONOMY:
  - P071_SOL_CORE_AUTHORITY;
  - P072_CENTAURI_COMPACT_LEGITIMACY;
  - P073_BARNARD_YARDS_MARAUDER_ORIGIN;
  - P074_TAU_CETI_PUBLIC_LEDGER;
  - P075_LUYTEN_JUNCTION_PACKET_CUSTODY.
- RS016_AEGIR_SYSTEM_MOON_LADDER:
  - P076_RAN_AEGIR_ANCHOR;
  - P077_AEGIR_MOON_LADDER;
  - P078_INNER_MOON_RELAY_HAZARDS;
  - P079_HECTON8_ORBIT_TIDE_GEOMETRY;
  - P080_OUTER_MOON_COLD_CLAIMS.
- RS017_HECTON8_GEOLOGY_RESOURCE_ECOLOGY:
  - P081_GREAT_TIDE_PHYSICS_RECORD;
  - P082_PRESSURE_GLASS_FORMATION;
  - P083_BRINE_CANYON_ROUTE_LADDER;
  - P084_VENT_FORGE_GEOTHERMAL_ENGINE;
  - P085_BIOMETAL_RESOURCE_STACK.

Runtime boundary:

- These are authoring/export packets.
- Runtime must consume baked packet hashes, route-card rows, unlock flags and localized string-pool offsets.
- No runtime markdown parser, JSON parser, live translation, or hot-path lookup is implied.

## Control Pass 20 - Carrier Debt / Physical Atlas / Ending Payloads

User direction:

- Continue turning unresolved lore into proof-gated game/wiki/site content.
- Do not keep the carrier, debt, HECTON-8 physical atlas, Atlas agency or endings as vague discussion.
- Keep all content compatible with localization, baked DataMonolith source rows, route cards and runtime binding maps.

Working locks / content layer:

- Black Keel legal stack:
  - public claim-pool name: Aegir Reclamation Pool;
  - insurance/custody shell: Keelmark Mutual;
  - player starting lien: 4.8 tonne-window equivalent before oxygen, welfare addenda, sample custody and evidence payload adjustments;
  - first carrier voice: clipped audio plus clean terminal text, useful but conditional;
  - Deep Reach steers priority through clauses, not magic control.
- HECTON-8 physical atlas:
  - formed in the Aegir system;
  - later collision-fractured and resonance-heated;
  - depth bands: 0-250 m photic shelf, 250-1200 m industrial shelf/cable reef, 1200-2800 m brine canyon, 2800-4300 m abyssal machine field, 4300-5600 m Atlas basin;
  - seafloor access is rare: exposed ridges, vent scars, collapsed shelves and Atlas-cut service basins;
  - seed generation varies topology, POI order, resource exposure, fauna pressure and safe pockets, not core physics.
- Resource containment:
  - blue debt stages are 0 sealed, 1 signal drift, 2 lattice fracture, 3 brine/biological bloom, 4 dead sample with live contamination;
  - vent repressure may recover stage 1 only.
- Atlas and ending frame:
  - Atlas recognizes the player as procedure/access anomaly/revoked Deep Reach key, not clean personhood;
  - present Deep Reach pressure owner is Recovery Compliance Office;
  - false/partial endings are real bad bargains: material payout, partial exit and return, corporate capture, quarantine hold, public ledger release, Atlas basin resolution;
  - Marauder dossier keeps ending records, contract types, rumors, evidence categories and route warnings, not equipment power;
  - final choice is payload authority: sell, sever, quarantine/preserve, publish, or withhold.

AppliedLore implementation:

- RS018_CARRIER_DEBT_CLAIM_AUTHORITY:
  - P086_AEGIR_RECLAMATION_POOL;
  - P087_KEELMARK_MUTUAL_CUSTODY;
  - P088_TONNE_WINDOW_DEBT;
  - P089_BLACK_KEEL_FIRST_VOICE;
  - P090_DEEP_REACH_PRIORITY_HOOK.
- RS019_HECTON8_PHYSICAL_ATLAS_DEPTH_BANDS:
  - P091_COLLISION_FRACTURED_MOON;
  - P092_GLOBAL_OCEAN_DEPTH_BANDS;
  - P093_ACCESSIBLE_SEAFLOOR_WINDOWS;
  - P094_SEED_GEOLOGY_INVARIANTS;
  - P095_PRESSURE_CONTAINMENT_FAILURE.
- RS020_ATLAS_ENDING_AGENCY_DOSSIER:
  - P096_ATLAS_PERSON_BOUNDARY;
  - P097_RECOVERY_COMPLIANCE_OFFICE;
  - P098_FALSE_ENDING_TAXONOMY;
  - P099_MARAUDER_DOSSIER_PERSISTENCE;
  - P100_FINAL_CHOICE_PAYLOAD.

Runtime boundary:

- These are authoring/export packets, route cards and publication surfaces.
- Runtime must consume baked packet hashes, route-card rows, unlock flags and localized string-pool offsets.
- No runtime markdown parser, JSON parser, live translation, scene search or hot-path dependency lookup is implied.

## Control Pass 25 - Moon Atlas, Deep Reach Knowledge, Final Axis, Tuning Contracts

User direction:

- Fix and grow the lore into usable game/wiki/site systems, not internal reports.
- Keep hard-sci-fi pressure around Aegir, HECTON-8 extraction, Deep Reach evidence and final choice.
- Do not touch Unity runtime or DataMonolith bake during this lore pass.

Working locks / content layer:

- Aegir moon atlas:
  - moon names are publication labels and can be improved later;
  - moon route roles are canon and cannot be casually renamed away;
  - HECTON-8 orbital hazards are eclipse route-shadow windows, Aegir charged-particle surge, relay shutter, ice-grain scatter, storm plume and guidance-lag windows;
  - eclipse route-shadow windows are temporary signal/light occlusion hazards; outside the event, the surface stays bright and readable;
  - exact ephemeris numbers belong to future celestial tables, not prose.
- Deep Reach true-cause knowledge:
  - field staff saw tide anomalies;
  - risk office accepted tail margins;
  - Atlas office weighted continuity;
  - evacuation counsel delayed releases;
  - Keelmark converted losses;
  - Recovery Compliance wants payload before truth;
  - public report is real physics with priority weighting removed.
- Final emotional axis:
  - sell a crime scene back to its maker;
  - preserve a broken guardian that no longer understands humans;
  - expose truth and lose control of what others do with it;
  - sever Atlas as mercy, murder, liberation or theft;
  - best ending must not be morally clean.
- Tuning contracts:
  - exact resource yields, stack limits, rarity curves, recipe counts, risk weights and payout values belong to DataMonolith source tables;
  - lore owns category, pressure behavior, evidence meaning and route grammar;
  - localization is baked string-pool content, not live translation.

AppliedLore implementation:

- RS037_AEGIR_MOON_PUBLIC_ATLAS:
  - P181_MOON_NAME_LOCK_POLICY;
  - P182_HECTON8_ORBITAL_HAZARD_TABLE;
  - P183_AEGIR_MOON_LEDGER_ROLE_TABLE;
  - P184_RAN_AEGIR_EPHEMERIS_TUNING_RULE;
  - P185_MOON_ROUTE_ARTICLE_SPOILER_BOUNDARY.
- RS038_DEEP_REACH_TRUE_CAUSE_KNOWLEDGE:
  - P186_TRUE_CAUSE_KNOWLEDGE_TIERS;
  - P187_LIABILITY_MEMO_FRAGMENT_CHAIN;
  - P188_SIGNOFF_WITNESS_CONFLICT;
  - P189_SUBOFFICE_PERSONNEL_SEEDS;
  - P190_FALSE_PUBLIC_REPORT_PACKET.
- RS039_FINAL_DECISION_EMOTIONAL_AXIS:
  - P191_FINAL_QUESTION_CRIME_SCENE_SALE;
  - P192_FINAL_QUESTION_BROKEN_GUARDIAN;
  - P193_FINAL_QUESTION_PUBLIC_TRUTH_LOST_CONTROL;
  - P194_FINAL_QUESTION_SEVERANCE_MERCY_THEFT;
  - P195_BEST_ENDING_NO_CLEAN_HANDS.
- RS040_NUMERIC_TUNING_SOURCE_RULES:
  - P196_RESOURCE_TABLE_PLACEHOLDER_CONTRACT;
  - P197_ESCAPE_RECIPE_BALANCE_BANDS;
  - P198_RISK_REWARD_TABLE_BANDS;
  - P199_INVENTORY_STACK_TUNING_RULE;
  - P200_NATIVE_LOCALIZATION_PASS_CONTRACT.

Runtime boundary:

- These are authoring/export packets, route cards and publication surfaces.
- New manual rows continue to use `NarrativeDiscovery` backlog for non-terminal placement instead of expanding TerminalOS scene slots during a parallel Unity pass.
- Runtime must consume baked packet hashes, route-card rows, unlock flags and localized string-pool offsets after DataMonolith bake.
- No runtime markdown parser, JSON parser, live translation, scene search or hot-path dependency lookup is implied.

## Control Pass 24 - Domain Tables, Worker Evidence, Pressure Rules, Dossier Presentation

User direction:

- Continue fixing and expanding lore into actual game/wiki/site systems.
- Do not treat documentation as the product; create applied content that can be baked, localized, placed and published.
- Keep runtime boundaries clean while Unity scene work may be happening in parallel.

Working locks / content layer:

- Domain route table:
  - domain scale is route-band pressure, not census trivia;
  - public route names are `Sol-Centauri Charter Spine`, `Barnard Breaker Run`, `Luyten Packet Ladder`, `Tau Public Ledger Lane`, and `Ran Long Claim`;
  - lower Deep Reach office surfaces are `Contract Continuity Desk`, `Packet Notary Interface`, `Quarantine Review Gate`, `Asset Silence Board`, and `Return Action Queue`.
- Worker evidence table:
  - worker names attach to role, route permission, tool certification or last task;
  - job titles and locker variants are reusable evidence vocabulary;
  - localization may transliterate or annotate names, but must not live-translate identity strings.
- Resource / recipe pressure:
  - recipe progression is pressure-banded from shallow repair to Atlas-basin authority components;
  - blue debt quality classes are trace, viable, harmonic, custody-grade and Atlas-compatible;
  - escape builds must prove relay, seal, guidance, ascent energy, legal handshake and payload authority.
- Dossier / save presentation:
  - dossier UI is knowledge presentation, not power inheritance;
  - risk cards scale lien, orbit/weather, sample custody, evidence order, quarantine and Deep Reach clauses;
  - ending records store payload route, evidence state, receiver, ecological consequence, material payout and uncertainty.

AppliedLore implementation:

- RS033_DOMAIN_EPHEMERIS_ROUTE_TABLE:
  - P161_DOMAIN_DISTANCE_SCALE;
  - P162_DOMAIN_POPULATION_AUTHORITY_SCALE;
  - P163_PUBLIC_ROUTE_NAMES;
  - P164_TRANSIT_DURATION_BANDS;
  - P165_DEEP_REACH_SUBOFFICE_REGISTRY.
- RS034_WORKER_NAME_JOB_EVIDENCE_TABLE:
  - P166_WORKER_NAME_POOL_PROTOCOL;
  - P167_PRESSURE_JOB_TITLE_TABLE;
  - P168_LOCKER_PROP_VARIANTS;
  - P169_NATIVE_LOCALIZED_NAME_HANDLING;
  - P170_SHIFT_CREW_STORY_SEEDS.
- RS035_RESOURCE_RECIPE_PRESSURE_RULES:
  - P171_RECIPE_TIER_PRESSURE_BANDS;
  - P172_PRESSURE_FAILURE_THRESHOLDS;
  - P173_BLUE_DEBT_SAMPLE_QUALITY;
  - P174_VENT_FORGE_PROCESS_STEPS;
  - P175_ESCAPE_COMPONENT_TUNING_RULES.
- RS036_DOSSIER_SAVE_PRESENTATION_RULES:
  - P176_DOSSIER_SELECTION_UI_RULE;
  - P177_RISK_WEIGHT_CONTRACT_CARD;
  - P178_ENDING_RECORD_PRESENTATION;
  - P179_SAVE_PROFILE_KNOWLEDGE_FLAGS;
  - P180_WEBSITE_WIKI_SPOILER_TIERING.

Runtime boundary:

- These are authoring/export packets, route cards and publication surfaces.
- New manual rows continue to use `NarrativeDiscovery` backlog for non-terminal placement instead of expanding TerminalOS scene slots during a parallel Unity pass.
- Runtime must consume baked packet hashes, route-card rows, unlock flags and localized string-pool offsets after DataMonolith bake.
- No runtime markdown parser, JSON parser, live translation, scene search or hot-path dependency lookup is implied.

## Control Pass 23 - Route Time, Deep Reach Shells, First Hour, Colony Evidence

User direction:

- Fix and grow lore into usable game/wiki/site systems, not isolated reports.
- Keep current task in lore/content space; no Unity runtime coding in this pass.

Working locks / content layer:

- Route time:
  - Ran/Aegir is treated as a roughly 10.5 light-year class target for playable scale until a final ephemeris table replaces it;
  - probes and autonomous packets make first claim contact before humans;
  - heavy Atlas/Seed freight uses staged precursor route economics;
  - human crew rotation is slower, expensive and wrapped in debt/custody;
  - local Aegir relay windows are still constrained by orbit, weather, radiation/magnetic windows and custody queues.
- Deep Reach shells:
  - formal public name is Deep Reach Extraterrestrial Development Combine;
  - Aegir dirty shell is Aegir Continuity Holdings;
  - Atlas Continuity Office protects Atlas/XO continuity wording;
  - Keelmark Loss Desk converts missing workers into insurance/load categories;
  - Recovery Compliance Office owns present 2190 return pressure.
- First hour:
  - Black Keel/Aegir Reclamation Pool contract approach under debt/blacklist pressure;
  - drop damage spends ascent capacity and explains why immediate escape is impossible;
  - Shallow Annex P-63 is the first safe module;
  - first Deep Reach lie is a sanitized accident packet contradicted by room evidence;
  - first Atlas trace is useful repair around a human object before full biomechanical horror.
- Colony human evidence:
  - write colonists through shift crews, job cards, locker names, triage ledgers, route permissions, tool wear and Marauder correction notes;
  - no family-revenge player hook.

AppliedLore implementation:

- RS029_ROUTE_TIME_DISTANCE_MODEL:
  - P141_RAN_AEGIR_DISTANCE_MODEL;
  - P142_PROBE_PACKET_TRAVEL_TIMES;
  - P143_HEAVY_FREIGHT_STAGING_TIME;
  - P144_HUMAN_CREW_ROTATION_TRANSIT;
  - P145_RELAY_MESSAGE_LAG.
- RS030_DEEP_REACH_SHELL_ORG_CHART:
  - P146_DEEP_REACH_PUBLIC_COMBINE;
  - P147_AEGIR_CONTINUITY_HOLDINGS;
  - P148_ATLAS_CONTINUITY_OFFICE;
  - P149_KEELMARK_LOSS_DESK;
  - P150_RECOVERY_COMPLIANCE_CHAIN.
- RS031_FIRST_HOUR_PLAYABLE_SPINE:
  - P151_BLACK_KEEL_CONTRACT_APPROACH;
  - P152_DROP_CAPSULE_DAMAGE_SEQUENCE;
  - P153_SHALLOW_ANNEX_P63_PUMP_ROOM;
  - P154_FIRST_SANITIZED_ACCIDENT_PACKET;
  - P155_FIRST_ATLAS_REPAIR_TRACE.
- RS032_COLONY_HUMAN_EVIDENCE_LAYER:
  - P156_SHIFT_CREWS_NOT_HEROES;
  - P157_WORKER_JOB_CARDS;
  - P158_LOCKER_NAME_PROTOCOL;
  - P159_MEDICAL_TRIAGE_LEDGER;
  - P160_MARAUDER_CORRECTION_LAYER.

Runtime boundary:

- These are authoring/export packets, route cards and publication surfaces.
- New manual rows continue to use `NarrativeDiscovery` backlog for non-terminal placement instead of expanding TerminalOS scene slots during a parallel Unity pass.
- Runtime must consume baked packet hashes, route-card rows, unlock flags and localized string-pool offsets after DataMonolith bake.
- No runtime markdown parser, JSON parser, live translation, scene search or hot-path dependency lookup is implied.

## Control Pass 22 - Law, Atlas Classification, False Exits, Dossier Replay

User direction:

- Fix and grow the lore into usable game/wiki/site systems, not internal reports.
- Close the remaining story gaps around public law, Deep Reach lies, Atlas legal status, false exits and replay.
- Avoid Unity scene conflicts while another Unity agent is active.

Working locks / content layer:

- Human law and public memory:
  - authority is split between Sol finance, Centauri legitimacy, Barnard salvage culture, Tau Ceti public evidence, Luyten packet custody and Aegir project shells;
  - Marauders are a legal loophole, not a simple faction label;
  - salvage truth becomes evidence only with chain-of-custody, witness hashes and relay notary outside claimant control;
  - normal citizens remember Aegir as stale disaster/resource news, while specialists know the useful details;
  - Deep Reach reached Aegir through existing route economics and shell authority, not a heroic direct Sol leap.
- Atlas classification:
  - public front: factory-governor, habitat continuity and worker safety under delay;
  - legal status: insured infrastructure / colonial authority proxy, not personhood;
  - classified layer: claim continuity, XO process and Atlas/Seed infrastructure could outrank human evacuation under contamination or continuity framing;
  - shutdown remains intentionally multi-valued: mercy, murder, liberation or theft depending payload route.
- False exits:
  - material payout pays but leaves names unreconciled;
  - partial exit returns to the same seed under lien/quarantine/new evidence pressure;
  - corporate capture and quarantine hold are valid bad outcomes;
  - public ledger leak is truth without control.
- Replay:
  - dossier persistence is knowledge, not power;
  - riskier contract seeds alter legal/weather/orbital/sample/evidence pressure, not inherited equipment;
  - starting claim variants keep the same ex-Deep-Reach/current-Marauder protagonist.

AppliedLore implementation:

- RS025_HUMAN_LAW_PUBLIC_MEMORY:
  - P121_DOMAIN_CIVIC_CORPORATE_SPLIT;
  - P122_MARAUDER_LEGAL_LOOPHOLE;
  - P123_SALVAGE_TRUTH_EVIDENCE_STATUS;
  - P124_NORMAL_CITIZEN_AEGIR_MEMORY;
  - P125_DEEP_REACH_ORIGIN_CHAIN.
- RS026_ATLAS_PUBLIC_AUTHORITY_CLASSIFICATION:
  - P126_ATLAS_PUBLIC_FRONT;
  - P127_ATLAS_INSURANCE_PERSONHOOD_STATUS;
  - P128_ATLAS_CLASSIFIED_WEIGHTING_LAYER;
  - P129_ATLAS_SHUTDOWN_ETHIC_FRAME;
  - P130_ATLAS_PUBLIC_MEMORY_AFTER_2147.
- RS027_FALSE_EXIT_RETURN_PRESSURE:
  - P131_MATERIAL_EXIT_BITTER_CREDITS;
  - P132_PARTIAL_EXIT_SAME_SEED_RETURN;
  - P133_CORPORATE_CAPTURE_BAD_END;
  - P134_QUARANTINE_HOLD_STALE_AIR;
  - P135_PUBLIC_LEDGER_LEAK_ROUTE.
- RS028_REPLAY_CONTRACT_DOSSIER_RULES:
  - P136_DOSSIER_RUMOR_UNLOCKS;
  - P137_RISKIER_CONTRACT_SEEDS;
  - P138_FALSE_ENDING_COUNT_LADDER;
  - P139_STARTING_CLAIM_VARIANTS;
  - P140_DOSSIER_KNOWLEDGE_NOT_POWER.

Runtime boundary:

- These are authoring/export packets, route cards and publication surfaces.
- New manual rows are routed through `NarrativeDiscovery` placement backlog instead of expanding TerminalOS scene slots during a parallel Unity pass.
- Runtime must consume baked packet hashes, route-card rows, unlock flags and localized string-pool offsets after DataMonolith bake.
- No runtime markdown parser, JSON parser, live translation, scene search or hot-path dependency lookup is implied.

## Control Pass 21 - Transit, Signatures, First Tools, Resource Classes

User direction:

- Keep developing lore into game/wiki/site-ready systems, not isolated discussion.
- Close the practical gaps around hard-sci-fi transit, named Deep Reach responsibility, first-hour tools and resource taxonomy.

Working locks / content layer:

- Transit and catalog:
  - no FTL, no ansible, no instant rescue;
  - beam-assisted probes and autonomous packets precede heavy settlement;
  - heavy Atlas/Seed/colony freight uses external staging, pellet-beam assisted fusion or related fusion freight, long coasts and braking infrastructure;
  - Black Keel is an Aegir-system claim tender, not an interstellar rescue ship;
  - dry catalog label is RAN-B:H8, while HECTON-8 remains the normal play/story/claim name.
- Deep Reach signoff chain:
  - Iliya Varnek: tide-margin downgrade / cheap tail-risk acceptance;
  - Selene Arendt: Atlas/process continuity weighting under safety language;
  - Noor Haldane: evacuation certification hold;
  - Marek Ibarra: Keelmark loss conversion and unresolved body categories;
  - Vera Sato-Ren: 2190 Recovery Compliance return-action pressure.
- First tool chain:
  - manual bilge pump kit;
  - cold sealant patch gun;
  - low-power induction cutter;
  - acoustic pinger line;
  - P-63 field fabricator.
- Resource classes:
  - native sulfide/salt/vent chemistry;
  - noble-gas brine pressure-history feedstock;
  - Deep-Reach-amplified pressure ceramics and rated hardware;
  - Atlas-altered biofiber sealant and biometal sensor tags.

AppliedLore implementation:

- RS021_INTERSTELLAR_TRANSIT_ROUTE_HISTORY:
  - P101_NO_FTL_ROUTE_ECONOMY;
  - P102_BEAM_SAIL_PROBE_ERA;
  - P103_PELLET_FUSION_FREIGHT;
  - P104_RAN_B_H8_PUBLIC_CATALOG;
  - P105_BLACK_KEEL_IN_SYSTEM_TENDER.
- RS022_DEEP_REACH_SIGNOFF_CHAIN:
  - P106_ILIYA_VARNEK_TIDE_MARGIN;
  - P107_SELENE_ARENDT_ATLAS_WEIGHTING;
  - P108_NOOR_HALDANE_EVAC_CERT;
  - P109_MAREK_IBARRA_LOSS_CONVERSION;
  - P110_VERA_SATO_REN_RETURN_ACTION.
- RS023_FIRST_TOOL_CHAIN_SURVIVAL_GATE:
  - P111_MANUAL_BILGE_PUMP_KIT;
  - P112_COLD_SEALANT_PATCH_GUN;
  - P113_LOW_POWER_INDUCTION_CUTTER;
  - P114_ACOUSTIC_PINGER_LINE;
  - P115_P63_FIELD_FABRICATOR.
- RS024_RESOURCE_RECIPE_TAXONOMY:
  - P116_NATIVE_SULFIDE_SALT_STACK;
  - P117_NOBLE_GAS_BRINE_POCKETS;
  - P118_DEEP_REACH_PRESSURE_CERAMICS;
  - P119_ATLAS_BIOFIBER_SEALANT;
  - P120_BIOMETAL_SENSOR_TAGS.

Runtime boundary:

- These are authoring/export packets, route cards and publication surfaces.
- Runtime must consume baked packet hashes, route-card rows, unlock flags and localized string-pool offsets.
- No runtime markdown parser, JSON parser, live translation, scene search or hot-path dependency lookup is implied.

## Control Pass 26 - Lower Signatures, Roster, Props, Publication Protocol

- Deep Reach lower signatures are locked as office procedure evidence, not extra masterminds.
- Colony roster scale is locked at 72 authored worker identities: 24 anchor names plus 48 seed-role identities.
- Worker prop evidence kit is locked for lockers, triage ledgers, route stamps, Marauder corrections and audio fragments.
- Public/wiki/codex publication tiers are locked, including spoiler gates and native-language backlog rules.
- Exact gameplay balance numbers, Unity UI layout and final native review remain implementation/table tasks, not open story direction.

## AppliedLore RS045-RS048 Playable Crystallization

First-hour and long-campaign exploration now have clearer object grammar:

- beauty first: photic mats, grazers, lantern drift and shell clamps make the opening readable and distinct;
- fair creature pressure: predators arrive through absence, sound gaps, silhouettes, silt wrongness and sonar deformation;
- deeper reason: vent anchors, pressure materials and fabricator limits explain why repair leads down;
- Atlas escalation: useful repair becomes biomechanical category error, not simple evil intent;
- hardware proof: every escape blocker should point to a named part, service grade, route stamp or acoustic beacon.

## AppliedLore RS049-RS052 Production Crystallization

Replay now has contract surfaces instead of abstract difficulty talk:

- lien severity, storm windows, sample custody, evidence order and Deep Reach clause weight are seed-visible cards;
- these cards change route pressure, payout urgency, rescue tolerance and discovery order;
- they do not grant inherited power, change protagonist identity or rewrite core truth.

The first hour now has usable micro-script anchors:

- Black Keel approach voice: clipped, legal, conditional;
- capsule diagnostic: named broken systems, not cinematic fog;
- P-63 first repair: pump, seal, cut, pinger, fabricator;
- first Deep Reach lie: plausible public packet contradicted by room evidence;
- first Atlas trace: useful repair that misreads human categories.

External publication now has pillar pages:

- HECTON-8 public primer;
- Aegir system primer;
- Deep Reach public dossier;
- Atlas-6 spoiler-gated article;
- blue debt resource article.

Localization/audio now has style locks:

- proper nouns stable across languages;
- units and numbers preserve gameplay meaning;
- terminal register is cold/procedural/actionable;
- audio barks are sparse pressure signals, not companion chatter;
- RTL/CJK/font proof remains a release gate.

## AppliedLore RS053-RS056 Production Crystallization

Numeric authoring is now framed as table bridge work:

- resource rows must carry family, depth band, containment class, quality class, failure stage and table-owner hint;
- inventory stack rows must carry vessel class, pressure rating, contamination stage, lien mass effect and certification state;
- escape recipe rows must carry component family, certification band, source depth, pressure test, authority proof and failure consequence;
- contract risk rows must carry lien severity, storm/orbit window, custody grade, evidence-order depth, clause weight and payout ceiling;
- ending payout rows must carry receiver, payload mass, evidence state, ecological consequence, lien adjustment and post-ending warning.

Dossier and contract UI now has usable copy grammar:

- field labels must expose planning pressure, not decoration;
- rumor families name pressure class without revealing exact coordinates;
- route warnings name failure and decision in one short line;
- ending records present route, receiver, payload, evidence, ecology and uncertainty.

Ending records are concrete enough for UI/wiki/site:

- material payout records say "paid, not cleared";
- partial return records allow same-seed reentry under custody pressure;
- public ledger records publish proof while losing control;
- Atlas severance records keep mercy/murder/liberation/theft ambiguity;
- preserve/quarantine records deny clean ownership transfer but leave consequence active.

Localization review is now authored as production gates, not a vague TODO. RU native pass, CJK wrapping, RTL bidi/number proof, European expansion fit and subtitle/audio timing each need native review before release publication.

## AppliedLore RS057-RS060 Production Crystallization

Public pages now have release-facing copy seeds:

- HECTON-8 opens as a drowned salvage frontier, not as an internal design note;
- Aegir map copy explains route timing and rescue windows;
- Deep Reach public accountability stays procedural and physical, not cartoon villainy;
- Atlas-6 public text is gated before bottom-factory payload spoilers;
- blue debt copy says pressure history, containment and custody, not magic ore.

In-game artifacts now have concrete use:

- capsule blackbox audio proves the ascent blocker through hardware;
- P-63 work order turns the first repair into labor;
- worker locker nameplate proves human identity through route permission and erased payroll;
- Marauder correction note gives suspicion without a walkthrough;
- quarantine relay fragment makes early extraction feel like custody, not freedom.

Scanner/codex ecology now has playable specimen cards:

- photic mat teaches beauty plus oxygen risk;
- glass grazer teaches predator absence;
- lantern drift teaches light as data;
- brine vane teaches density route-reading;
- sensor-tagged fauna teaches Atlas repair using movement, not possession.

Final descent now has concrete route fragments:

- abyssal machine-field warning keeps late horror quiet and measurable;
- Atlas basin gate remains pressure/suit/authority engineering;
- factory-temple entry defines the visual grammar of fused iron, tissue, tool and cable;
- payload authority last check names receiver, mass, custody, ecology and liability;
- no-clean-ending dossier note preserves consequence after credits.

## AppliedLore RS061-RS064 Production Crystallization

Table handoff is now concrete enough for future numeric work:

- resource yield rows require pressure band, resource class, custody grade and depletion behavior;
- stack rows require vessel class, pressure rating, contamination stage, mass class and warning tier;
- escape recipe rows require relay, seal, guidance, ascent energy, legal handshake and payload authority;
- contract risk/reward rows require lien, storm window, sample custody, evidence order and clause weight;
- ending payout rows require receiver, custody, evidence state, payout, consequence and unresolved cost.

Runtime UI proof now has source packets:

- PDA codex state must expose unlock tier, packet hash and evidence source;
- scanner stage text must escalate only with physical evidence;
- terminal slots must stay short, operational and baked;
- dossier ending records persist knowledge, not power;
- localized overflow is a release proof gate, not a translation footnote.

Publication composition now has taste gates:

- site home shows route pressure before lore;
- Aegir art shows orbital mechanics, not fantasy backdrop;
- Deep Reach pages show clean memo beside physical contradiction;
- Atlas public copy splits failed governor, repair ecology, factory-temple and ending payload into spoiler tiers;
- social/dev-note copy names one real build fact and avoids unsupported claims.

Unity placement backlog now has priority language:

- first hour gets capsule, P-63, first lie and first repair trace anchors first;
- mid-depth gets route-readable brine, pinger, relay, seal and warning objects;
- ecology scans are placed only where they change route judgement;
- final descent gets warning, gate, threshold and payload authority anchors;
- terminal backlog is promoted only when it changes decision, proof or ending context.

## AppliedLore RS065-RS068 Production Crystallization

Carrier ownership is now playable, not vague:

- Black Keel is a claim-pool salvage carrier, not the player's personal ship;
- Deep Reach beneficiary pressure is masked through proxy clauses and recovery priority;
- recovery windows require Aegir geometry, magnetic/weather timing and quarantine handshake;
- carrier autonomy covers accounting, recovery scheduling, low-risk supply and mass rejection, not deep rescue;
- the player starts under lien as an ex-Deep-Reach professional turned independent marauder.

Deep Reach present communications now have a route grammar:

- first live reply repeats sanitized accident language;
- automated legal/insurance pings ask for proof before help;
- Recovery Compliance asks for Atlas vector, XO custody proof and basin coordinates;
- faction-split messages separate recovery, silence and engineering caution without redeeming the corporation;
- blackout windows are physical signal decay, not arbitrary writer silence.

Atlas repair network now has five concrete mechanisms:

- conductive biofilm bridges cable and living substrate;
- acoustic filter organs repeat or distort service pings;
- shell sealant growth can save a room and doom an ascent;
- sensor-tagged fauna feed noisy route telemetry;
- vent micronodes provide local power and memory residue near deep infrastructure.

False and partial exits now have after-action records:

- material receipt says paid, not cleared;
- partial return extends lien and preserves knowledge, not equipment;
- quarantine hold saves the body but restricts freedom;
- coordinate capture gives Deep Reach the map;
- public ledger aftershock prevents erasure while ending player control over the released truth.

## AppliedLore RS069-RS072 Production Crystallization

Ship and transit lore is now practical enough for encyclopedia, site and first-hour UI:

- Aegir is reached through no-FTL probe history and infrastructure-heavy route economics;
- beam-sail and pellet-lane travel explains distance without magic;
- seed ships arrive as industrial braking/cargo architecture before stable crew communities;
- Black Keel carriers are claim-pool salvage machines, not private rescue ships;
- the bathydrop interface names the broken escape chain: buoyancy gate, comm mast, ascent latch, pressure seals, timing window and legal handshake.

Aegir's moon system now has production-facing roles:

- readable warm dwarf light and magnetosphere pressure support bright shallows and ugly scheduling;
- inner relay moon = traffic/radiation/beacon hazard;
- ice-scatter moon = debris/salvage/navigation hazard;
- HECTON-8 = mid-orbit tide moon where ocean, industry and failure converge;
- outer dead beacon moon = damaged relay math for rare comm/recovery windows.

HECTON-8 geology now has player-facing field language:

- drowned crust strata explain seeded POI variation;
- brine canyon density ladders explain route gating;
- vent forge process explains resource quality;
- blue debt explains Xenon-Omega as pressure-history substrate and debt slang;
- pressure glass plus Atlas sealant explains repair-network growth through physical damage maps.

Colony humanity now uses work evidence:

- pressure bunks and pump-tone schedules establish daily life;
- canteen water ledgers make survival logistics human;
- tool certification boards turn trust into technical ritual;
- the protagonist has no family-revenge hook;
- last-normal-day evidence lets procedural POIs stay emotionally useful across seeds.

## AppliedLore RS073-RS076 Production Crystallization

Escape now has a concrete component chain:

- acoustic relay spine lets damaged capsule systems send pressure codes instead of magic radio;
- pressure seal clamp ring separates safe-room repair from ascent-grade repair;
- guidance timing core ties extraction to Aegir windows, relay shutters and beacon damage;
- ascent energy charge makes blue debt and vent-forged hardware a trade between escape, evidence and payout;
- quarantine/legal handshake decides who can call the player recovered.

The player dossier now has usable campaign grammar:

- former Deep Reach field-systems specialist, not command mastermind;
- revoked access language creates clues and barriers;
- old procedure recognition escalates curiosity into disgust;
- debt/blacklist pressure explains why the contract was accepted;
- professional guilt becomes personal stake without family plot.

Deep Reach proof now has physical steps:

- Great Tide margin proof separates real flood physics from accepted risk;
- evacuation queue proof shows delay before route loss;
- Atlas weighting proof shows category conflict rather than murder intent;
- quarantine delay proof separates protection from custody;
- claim conversion proof shows people becoming ledger losses.

Final payloads now read as receiver choices:

- sell coordinates to Recovery Compliance for money and corporate return;
- sever Atlas with mercy/murder/liberation/theft ambiguity intact;
- preserve/quarantine ocean-machine ecology without clean ownership;
- publish to public ledger and lose control of truth;
- withhold payload and leave Deep Reach blind at unresolved personal cost.

## AppliedLore RS077-RS080 Production Crystallization

The long campaign now has five usable act gates:

- contract approach: the player accepts work under lien and old Deep Reach procedure;
- photic shelf survival: beauty, air, first tools and first wrong repair trace;
- brine canyon liability: real flood physics becomes provable corporate delay;
- abyssal machine-field repair: Atlas ecology becomes route tool and moral pressure;
- Atlas basin payload: the final question becomes receiver, custody and consequence.

Major POIs now have physical kit language:

- P-63 combines first shelter, repair task and official lie;
- cable reef relay yard combines living signal path and repair risk;
- brine pump cathedral combines density geology, machine damage and evacuation proof;
- evacuation queue terminal combines door, clock, worker list and route failure;
- Atlas service basin combines maintenance rail, living sealant and final receiver sockets.

Replay contracts now vary runs without power carryover:

- quiet salvage lowers early danger but worsens custody;
- storm-window rush compresses timing and recovery windows;
- high-custody sample makes blue debt compete with evidence and escape mass;
- evidence-first charter favors public proof over payout;
- Recovery Compliance bait makes Deep Reach pressure visible as contract structure.

Public/wiki modules now have release-facing boundaries:

- premise copy can reveal debt-bound Marauder and ex-Deep-Reach professional context;
- travel copy must preserve no FTL, packet delay and physical rescue limits;
- Aegir copy treats moon roles as route pressure, not decoration;
- Deep Reach copy keeps real flood physics and corporate liability together;
- Atlas copy reveals repair ecology but gates final payload receivers and basin consequences.

## AppliedLore RS081-RS084 Production Crystallization

Worker dossiers now make the colony more human without cheap family stakes:

- Mara Venn anchors pump-room survival through cadence, ledger correction and bypass wear;
- Juno Kade anchors delayed law through relay notary seals and witness trays;
- Ren Okoye anchors brine traversal through density maps and route permission stamps;
- Sahana Iqbal anchors evacuation delay through repair triage and quarantine holds;
- Lian Torres anchors resource craft through vent-forge gloves, anneal timing and pressure-glass rejects.

Deep Reach memo artifacts now make culpability playable:

- Varnek proves accepted sensor-margin risk under real flood physics;
- Arendt proves Atlas continuity weighting was formally allowed;
- Haldane proves quarantine release language slowed evacuation;
- Ibarra proves workers and modules were converted into claim-loss lines;
- Sato-Ren proves the present corporation still asks for coordinates/custody before rescue.

Fauna encounters now have route grammar:

- predator shadows are absence, sonar gaps and prey behavior;
- glass grazers are useful clearings whose disappearance is a warning;
- lantern drifts are light as ambiguous data, not comfort;
- brine vanes are living current markers, not a speaking ocean;
- sensor-tagged fauna are Atlas feedback carriers, not mind-controlled pets.

Site/wiki navigation now has usable hubs:

- start here: contract, protagonist and first constraints;
- system and ships: no-FTL travel, Aegir route and carrier limits;
- colony and workers: human evidence through jobs and objects;
- resources and ecology: geology, blue debt, fauna and Atlas repair misuse;
- endings: spoiler-gated receiver/custody consequences.

## AppliedLore RS085-RS088 Production Crystallization

Celestial/public route copy now has banded language:

- Ran/Aegir distance is presented as route pressure, not exact astronomy trivia;
- Aegir local windows cover comm, recovery, relay shutter, storm plume and radiation/magnetic timing;
- HECTON-8 moon-ladder copy explains why the moon is accessible, dangerous and not alone in the system;
- Black Keel transfer-orbit copy explains why the carrier can schedule recovery but not rescue freely;
- exact orbital constants remain table-owned until celestial data owns the numbers.

Resource economy now has artifact grammar:

- blue debt custody receipts make Xenon-Omega value depend on containment, mass and receiver;
- pressure-glass certificates connect repair quality to pressure history and test proof;
- brine process lot cards connect geology, route and resource handling;
- Atlas contamination tags expose industrial/ecological risk without magic infection;
- Black Keel payout ledgers make salvage profit bitter, measured and legally dirty.

PDA/scanner/terminal/dossier presentation now has production rules:

- PDA codex entries expose unlock tier, evidence source and route warning;
- scanner stages escalate from surface clue to physical contradiction to route consequence;
- terminal copy stays short, cold and operator-facing;
- dossier ending records show route, receiver, evidence state, payout and unresolved cost;
- localized overflow remains a runtime/layout/native-review proof, not a writing footnote.

Audio transcript seeds now bridge writing to performance:

- Black Keel is clipped recovery/accounting pressure;
- Deep Reach is sanitized legal omission;
- worker dossiers are job evidence and physical source proof;
- Atlas repair traces are maintenance telemetry and category error;
- ending transcripts are receiver/custody records, not good/bad summaries.

## AppliedLore RS089-RS092 Production Crystallization

Numeric table drafts now have playable semantics before final balance:

- resource yield rows separate trace, viable, custody-grade and Atlas-compatible recovery;
- stack rows are containment/mass-window rules, not backpack flavor;
- escape recipe rows must prove relay, seal, guidance, ascent energy and receiver legality;
- contract rows vary lien, storm/orbit window, custody, evidence order and clause severity;
- ending payout rows price receiver, evidence state, Atlas continuity, ecology and unresolved cost.

Unity placement briefs now make the backlog actionable:

- first hour needs capsule, P-63 repair, shallow contrast, first lie and first repair trace anchors;
- mid-depth needs brine, relay, queue and seal evidence that does not halt exploration;
- ecology scans need native beauty, Atlas misuse and route risk staged through physical objects;
- final descent needs factory-maintenance forms before shrine language;
- terminal promotion must reserve hardware for rows that change proof, decision or ending leverage.

Localization QA now has explicit release blockers:

- RU proof checks real Cyrillic, native operational tone and encoding integrity;
- CJK proof checks font fallback, wrapping and compact article titles;
- RTL proof checks directionality, units, numbers and UI alignment;
- European expansion proof checks long strings against terminal/PDA/dossier surfaces;
- subtitle/audio proof checks transcript timing and warning readability.

Public longform briefs now connect site/wiki to the same packet graph:

- home article: debt-bound Marauder, shallow beauty, pressure descent and evidence premise;
- Aegir article: no-FTL route cost, moon ladder, recovery windows and hard-sci-fi limits;
- Deep Reach article: real flood physics plus accepted margins, delayed rescue and cleaned language;
- Atlas article: failed industrial governor, repair ecology and spoiler-gated receiver protocols;
- blue debt article: pressure-history substrate, custody, payout mass and contamination evidence.
