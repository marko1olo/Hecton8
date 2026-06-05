# Screenshot And Clip Shotlist

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Status: pre-capture plan
Rule: every asset must sell identity without a paragraph

## Screenshot Quality Bar

Every public screenshot must answer at least two:

- What can kill or ruin the player?
- If this is surface/photic water, is it beautiful and readable without becoming a generic aquarium?
- What machine keeps the player alive?
- What does pressure look like?
- What makes this HECTON-8, not generic underwater survival?
- What is the player doing?
- Why would a creator click this?

Reject screenshots that:

- show only empty water;
- show only pretty lighting;
- need lore explanation;
- look like clean plastic sci-fi;
- hide all gameplay verbs;
- use UI clutter to compensate for weak composition;
- imply co-op.

## First Screenshot Pack

### Shot 00 - Bright Photic Route Beauty

Purpose: prove HECTON-8 is not hiding weak surface/shallow art behind darkness.

Composition:

- bright surface-adjacent or photic water;
- readable terrain, waterline, sky/Aegir/moon context where visible;
- alien biota or coral-like forms if biome supports them;
- technogenic trace: cable, module remnant, route hardware, salvage cut, station mark, or instrument cue;
- one route cue or player decision, not only scenery.

Caption options:

- The shallow water is beautiful. The route still has a cost.
- The first lie is that clear water means safe water.
- Above the dark, HECTON-8 still wants to be crossed.

### Shot 01 - One Floodlight Against Black Water

Purpose: establish Deep Sea Noir.

Composition:

- player/light source in lower third;
- large negative space;
- partial industrial silhouette;
- particulate/silt readable but not noisy;
- no bright coral wonder.

Caption options:

- One floodlight against an ocean.
- There is no sky down here.
- The ocean is not empty. It is waiting.

### Shot 02 - Pressure Room Interior

Purpose: sell base as machinery.

Required elements:

- pipes;
- gauges;
- condensation;
- warning light;
- dirty glass;
- visible hull/door/seal.

Caption options:

- The safest room is still underwater.
- A base is a machine that keeps saying no to the ocean.
- Survival measured in seals, power, and oxygen.

### Shot 03 - Salvage Contact

Purpose: show player verb.

Required elements:

- tool in hand or readable interaction;
- wreck/material/object;
- hazard nearby;
- clear reward reason.

Caption options:

- Salvage what you can. Trust nothing.
- Every useful part has a cost.
- A normal salvage run until the hull complains.

### Shot 04 - Heavy Machine Hero

Purpose: sell NASA-punk machine fantasy.

Required elements:

- vehicle/exosuit/pump/ballast system if real;
- mass and scale;
- grime/wear;
- mechanical affordance.

Caption options:

- Built to survive. Not built to forgive.
- NASA-punk machinery under impossible pressure.
- The machine is louder than the player for a reason.

### Shot 05 - Base Under Stress

Purpose: sell pressure as gameplay, not backdrop.

Required elements:

- leak/flood/stress warning;
- player response path;
- readable system consequence;
- not just red UI.

Caption options:

- The base holds until it does not.
- One leak. One warning. One bad choice.
- Depth turns small mistakes into disasters.

### Shot 06 - Threat Silhouette

Purpose: sell dread without over-revealing.

Required elements:

- silhouette;
- scale reference;
- floodlight/sonar relation;
- partial occlusion.

Caption options:

- The sonar saw it first.
- That shadow was not terrain.
- The warning came too late.

### Shot 07 - Seed Ship Signal

Purpose: sell systemic anomaly hook.

Required elements:

- corrupted instrument/radar/log;
- environmental distortion;
- no lore wall;
- clear "something is wrong" read.

Caption options:

- Never follow a signal into black water.
- The dark has infrastructure.
- The deeper signal is not a beacon. It is a wound.

### Shot 08 - Minimum-Budget Proof Frame

Purpose: future performance/trust asset.

Required elements:

- same scene still readable without ultra effects;
- labeled hardware only if measured;
- no fake frame-rate claim.

Caption options:

- Built to scale. Proof when measured.
- Readability first. Overkill later.

## First 20-Second Clip Pack

### Clip 01 - Pressure Leak Decision

Timeline:

- 0-2s: pressure warning / leak appears;
- 3-8s: player chooses repair or reroute;
- 9-15s: consequence escalates;
- 16-20s: system stabilizes or fails.

Viewer takeaway:

Pressure is a gameplay problem.

### Clip 02 - Sonar Saw It First

Timeline:

- 0-2s: sonar ping;
- 3-8s: vague shape;
- 9-14s: floodlight catches partial silhouette;
- 15-20s: retreat, alarm, or signal loss.

Viewer takeaway:

The ocean hides threats that instruments reveal before eyes do.

### Clip 03 - Salvage Run Went Wrong

Timeline:

- 0-3s: tool hits salvage object;
- 4-9s: success/reward;
- 10-15s: nearby system or fauna reacts;
- 16-20s: player forced to leave or choose risk.

Viewer takeaway:

Progress has cost.

### Clip 04 - Heavy Machine Startup

Timeline:

- 0-3s: mechanical lever/pump/ballast;
- 4-10s: machine movement;
- 11-16s: environment pressure responds;
- 17-20s: title shot.

Viewer takeaway:

Machines are characters.

### Clip 05 - Seed Ship Corruption

Timeline:

- 0-3s: instrument anomaly;
- 4-9s: UI/audio/radar distortion;
- 10-15s: environment behavior changes;
- 16-20s: signal points downward.

Viewer takeaway:

The story is systemic, not a lore popup.

## 2026-05-19 Capture Packet V0

Use this table the moment a playable scene can produce images. It maps planned asset IDs to exact capture intent, minimum frame content, and reject codes. If a planned shot cannot be captured honestly, leave it `PLANNED_CAPTURE`; do not fake it with concept art.

| Asset ID | Capture intent | Camera/readability requirement | Must include | Reject code if failed | First use |
|---|---|---|---|---|---|
| `PLAN-SHOT-000` | Bright photic route beauty | 16:9 and Steam crop both read as beautiful surface-adjacent/photic water, not muddy darkness. | water/sky/Aegir or coastline context, terrain/material detail, biota or coral if present, technogenic trace, one route cue. | `SURFACE_TOO_DARK`, `GENERIC_AQUARIUM`, or `NO_ROUTE_CUE` | `POST-000`, Steam screenshot 1 candidate when build proves it. |
| `PLAN-SHOT-001` | Depth pressure identity | 16:9, player light or machine silhouette in lower third, structured black-water scale visible only if the shot is genuinely below-light/depth. | industrial silhouette, silt, one route/danger cue. | `GENERIC_VISUAL`, `NO_PLAYER_VERB`, or `SURFACE_MISGRADED_AS_ABYSS` | `POST-001`, Steam screenshot 2/depth candidate. |
| `PLAN-SHOT-002` | Pressure room | 16:9 and 4:3 crop both readable; no clean sci-fi corridor read. | gauge/seal/pipe/dirty glass/warning light. | `DERIVATIVE_COMPETITOR_READ` or `BAD_COMPOSITION` | `POST-002`, capsule `CAP-001`. |
| `PLAN-SHOT-003` | Salvage contact | Tool/target relationship must be readable at thumbnail size. | tool, salvage object, hazard/route cost, reward reason. | `NO_PLAYER_VERB` | `POST-003`, capsule `CAP-002`. |
| `PLAN-SHOT-004` | Heavy machine | Must imply mass; avoid toy-like scale. | vehicle/exosuit/pump/ballast or real heavy mechanism. | `FEATURE_NOT_PUBLIC` or `GENERIC_VISUAL` | `POST-011`, capsule `CAP-004`. |
| `PLAN-SHOT-005` | Base stress | Must show system consequence and response path. | leak/flood/warning/repair route, not only red UI. | `NO_PLAYER_VERB` or `UI_UNREADABLE` | `POST-004`, capsule `CAP-001`. |
| `PLAN-SHOT-006` | Threat silhouette | Threat must not be fully revealed and must not read as terrain. | scale reference, sonar/floodlight relation, unsafe route. | `TOO_DARK` or `GENERIC_VISUAL` | `POST-005`. |
| `PLAN-SHOT-007` | Seed Ship signal | Must look like system interference, not abstract poster art. | instrument corruption, anomaly cue, route pull. | `CONCEPT_NOT_GAMEPLAY` or `BAD_COMPOSITION` | `POST-006`, capsule `CAP-003`. |
| `PLAN-SHOT-008` | Low-spec readability | Internal proof frame only; same composition as a stronger asset if possible. | cheap settings identity read, no public FPS claim. | `PERF_CLAIM_UNPROVED` | internal QA only. |
| `PLAN-CLIP-001` | Pressure leak decision | First 3 seconds show warning/leak/action choice. | warning, leak, response, escalation. | `NO_PLAYER_VERB` | `POST-008`, creator warmup. |
| `PLAN-CLIP-002` | Sonar saw it first | Instrument change must be readable before visual reveal. | ping, vague shape, partial silhouette, retreat/loss. | `UI_UNREADABLE` | `POST-009`, capsule `CAP-003` stills. |
| `PLAN-CLIP-003` | Salvage failure | Escalation must follow player action, not random event. | tool hit, reward, reaction, forced choice. | `NO_PLAYER_VERB` | `POST-010`, creator warmup. |
| `PLAN-CLIP-004` | Heavy machine startup | Machine motion must show weight and underwater context. | lever/pump/ballast, slow mass movement, pressure response. | `GENERIC_VISUAL` | `POST-011`, capsule `CAP-004` stills. |

### Imageboard Candidate Mapping

Use this mapping before any 4chan/Dvach route request. The board question must test the asset's weakest visible claim. Do not use imageboards for a polished announcement, store link, Discord invite, wishlist ask, key ask, or "support the project" beat.

| Asset ID | Imageboard route | Best critique question | Danger read | Kill cue |
|---|---|---|---|---|
| `PLAN-SHOT-000` | Optional beauty stress test. | "Does this look like a beautiful alien route with pressure cost, or just generic pretty water?" | Generic aquarium or weak Subnautica-adjacent exterior. | No route cue, no material detail, no technogenic trace, or surface reads dark/muddy. |
| `PLAN-SHOT-001` | Optional depth identity stress test. | "Does this read as industrial deep-sea survival or generic diver ocean?" | Generic diver exterior or depth mood with no system. | Empty water, hero diver pose, blue/purple aquarium, no machine/route cue, or surface art crushed into fake abyss. |
| `PLAN-SHOT-002` | Optional Dvach `/gd` or technical critique if interior proof is strong. | "Does this room read as a pressure machine or clean sci-fi corridor?" | Decorative base shot with no affordance. | No gauge/seal/pipe/wet glass; red light doing all the work. |
| `PLAN-SHOT-003` | Strong first imageboard still candidate. | "Can you tell what the player is doing and what could go wrong?" | Salvage loop looks thin or loot-sparkle generic. | Tool/target/reward/hazard relationship not readable at thumbnail size. |
| `PLAN-SHOT-004` | Optional only if the machine is real in build. | "Does this read as heavy underwater machinery or a static prop?" | Feature promise without gameplay proof. | Vehicle/exosuit/pump is not usable yet or lacks underwater context. |
| `PLAN-SHOT-005` | Strong if response path is visible. | "Is there an obvious next move: seal, repair, reroute, or leave?" | Unfair failure or UI-only drama. | Leak/warning exists but no player response path. |
| `PLAN-SHOT-006` | Strong only when the threat creates a route decision. | "Would you continue, scan, retreat, or reroute from this frame?" | Passive monster poster. | Threat is terrain-like, too dark, or no route/sonar/floodlight relation. |
| `PLAN-SHOT-007` | Monitor/internal unless gameplay proof is already visible. | "Does this look like system interference or abstract concept art?" | AI-looking anomaly poster. | Instrument corruption is decorative and no route pull exists. |
| `PLAN-SHOT-008` | Internal only. | "Is the composition still readable on cheap settings?" | Unproven performance claim. | Any FPS, optimization, or hardware boast without profiler artifact. |
| `PLAN-CLIP-001` | Strongest pressure-system candidate. | "At second 3, what would you do next?" | Mood leak with no decision. | Warning/leak/action choice starts after second 3. |
| `PLAN-CLIP-002` | Optional atmosphere candidate. | "Does the instrument reveal work before the shape appears?" | Generic sonar monster setup. | Viewer only waits for monster reveal, not system read. |
| `PLAN-CLIP-003` | Strongest loop-depth candidate. | "Does the salvage success create a second decision?" | Thin harvest loop. | Success ends the clip or escalation feels random. |
| `PLAN-CLIP-004` | Optional machine-fantasy candidate. | "Does the machine feel heavy and useful?" | Beauty shot of machinery. | No interaction, no consequence, no underwater pressure response. |

### Imageboard Capture Notes

- Export one direct media file with no logo, title card, streamer border, marketing crop, or caption baked in.
- Keep the question outside the image/video. If the asset needs text overlay to work, it fails the route.
- Clips must make the first three seconds readable without audio; audio can strengthen, not rescue, the read.
- Stills must survive thumbnail-size review before public-route approval.
- Do not crop out the route cue, tool, gauge, warning, or return path just to make a prettier frame.
- Capture a low/mid/high visual comparison only for internal trust. Public performance use still needs profiler/GC/memory proof.

### Capture Session Checklist

```text
Build ID:
Scene/area:
Quality preset:
Resolution:
Capture tool:
Owner:
Date:
Asset IDs attempted:
Known build issues visible:
Performance claim planned? no unless measured:
```

### First Capture Session Call Sheet V0

Use this for the first real capture session. The goal is not a full campaign pack. The goal is to learn whether the current build can produce proof assets without lying.

V7 priority note: the 2026-05-26 Steam API/appdetails/screenshot refresh kept SN2 `Very Positive` while the API summary rose to 98,345 all-language reviews, 59,676 English reviews, and 90,944 appdetails recommendations. Official screenshots confirm competitor strength in bright/cozy alien-ocean readability, clean bases, and co-op presence, not in industrial pressure-vessel dread. HECTON must still prove its own bright surface/photic route beauty before using black-water depth as contrast. After photic beauty, identity, and player verb, do not spend the first session on mood-only anomaly footage if base/machinery, black-water severity, and agency/decision proof are still missing. If the session time collapses, prioritize `PLAN-SHOT-000`, `PLAN-SHOT-002` or `PLAN-SHOT-005`, then `PLAN-SHOT-006` or a decision clip (`PLAN-CLIP-001` / `PLAN-CLIP-003`) before `PLAN-SHOT-007`.

| Timebox | Attempt | Asset IDs | Required result | Stop if |
|---:|---|---|---|---|
| 0-10m | Setup log | all | Fill build ID, scene, quality preset, resolution, known visible build issues. | Build/source cannot be identified. |
| 10-22m | Bright route proof | `PLAN-SHOT-000` | 2-3 takes where surface/photic water, sky/Aegir/coastline or terrain, and route cost read without darkness. | All takes are muddy, generic aquarium, or scenery-only with no route cue. |
| 22-35m | Depth identity exterior | `PLAN-SHOT-001` | 2-3 takes with industrial silhouette, route cue, player/machine light, and structured depth water if used. | All takes are empty water, generic diver frames, or surface misgraded as abyss. |
| 35-50m | Player verb | `PLAN-SHOT-003` | 3 takes where tool/target/hazard/reward read at thumbnail size. | Player action needs a caption. |
| 50-65m | Machinery/base | `PLAN-SHOT-002`, then `PLAN-SHOT-005` | 2-4 takes showing gauge/seal/pipe/failure/response path. | Base reads as clean room, red UI, or decorative sci-fi. |
| 65-78m | Threat/anomaly | `PLAN-SHOT-006`, then `PLAN-SHOT-007` | 2-4 takes with sonar/floodlight/route or instrument corruption. | Threat reads as terrain or anomaly reads as concept art. |
| 78-87m | Motion proof | `PLAN-CLIP-001` or `PLAN-CLIP-003` | One 10-20s clip where first 3 seconds show action/consequence. | Clip only works after explanation. |
| 87-90m | Triage | all attempted | Assign reject code, provisional QA score, creator utility, pain proof, and next action. | Any field would be guessed. |

Minimum useful output from the first session:

- one identity candidate;
- one bright surface/photic beauty candidate when the build can honestly show it;
- one player-verb candidate;
- one machinery/base candidate;
- one agency/decision candidate from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`, or an explicit `AGENCY_MISSING_HOLD` note;
- every failed attempt has a reject code;
- metadata row updated only with facts from the capture, not wishes.
- next capture action list capped at three items: fix scene/readability, retry specific asset ID, or kill/replace angle.

First session verdicts:

| Verdict | Condition | Next action |
|---|---|---|
| `KEEP_TESTING` | At least 3 stills score 9/12+ and one clip has a readable first 3 seconds. | Run Campaign 01 cold-read/QA path. |
| `REVISE_SCENE` | Assets fail but reject codes point to fixable lighting, UI, composition, or missing affordance. | Fix scene/capture setup before more marketing work. |
| `HOLD_ASSET` | Build cannot honestly show the planned feature yet. | Keep row `PLANNED_CAPTURE`; do not fake with concept art. |
| `KILL_ANGLE` | The angle repeatedly reads as clone, empty water, concept art, or no player verb. | Remove from first pack and replace with a simpler proof angle. |

### Per-Asset Review Form

```text
Asset ID:
Filename:
File path:
Build:
Hook:
First 3-second read:
Viewer-named decision:
Player verb visible? yes/no
Pressure/machinery cue visible? yes/no
Multiplayer-scope implication? yes/no
Performance implication? yes/no
Clone-risk cue:
Creator rows unlocked:
Creator utility score:
Creator send gate:
Pain bucket answered:
Pain proof score:
Pain freshness source:
Pain freshness checked at:
Public comparison gate:
Agency decision proof gate:
Agency decision notes:
Capture handoff packet ID:
Capture verdict:
QA score:
Decision: APPROVED_INTERNAL / APPROVED_PUBLIC / REVISION / QA_FAIL
Rejection code:
Metadata row updated? yes/no
Next action:
```

### First Capture Handoff Packet

Do not create a new handoff document. Append the packet into the existing campaign/QA/log owner surfaces after the session.

Required packet fields:

- build ID, scene/area, quality preset, resolution, capture tool, owner, and date;
- asset IDs attempted, filenames, file paths, and reject codes for failed attempts;
- provisional QA score, creator utility score, creator rows unlocked, and `creator_send_gate` state for every kept candidate;
- pain bucket, pain proof score, freshness source/date, and `public_comparison_gate`;
- `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, and the exact `viewer_named_decision` or `AGENCY_MISSING_HOLD`;
- first session verdict: `KEEP_TESTING`, `REVISE_SCENE`, `HOLD_ASSET`, or `KILL_ANGLE`;
- next action list capped at three items.

Handoff kill rule: if the packet lacks file path, build ID, QA score, creator utility, pain freshness, public comparison gate, agency proof gate, packet ID, verdict, viewer-named decision, next actions, or reject code for any attempted asset, Campaign 01 stays `HOLD` and the metadata row remains `PLANNED_CAPTURE` / `BLOCKED_PLANNED_CAPTURE`.

### Capture Kill Rules

- If three attempts at an asset ID fail the same reject code, stop capturing and revise the scene/lighting/UI.
- If the only good frame is a cinematic camera that hides player context, use it as internal mood reference, not public gameplay proof.
- If a shot requires a lore caption to make sense, it is not a first-pack asset.
- If a clip is only impressive after 10 seconds, recut or kill it for short-form.
- If comments would likely ask "is this co-op?", remove extra player silhouettes or helpers.

### SN2 Pain-Point Capture Modifier

This modifier uses current competitor sentiment privately. It must not appear as public comparison copy.

Freshness rule: recheck `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md` on the same day as capture planning. If SN2 has patched, roadmapped, or sentiment-shifted a bucket, keep the asset only if it still proves HECTON identity on its own; otherwise mark the bucket `MONITOR_ONLY` and choose a stronger HECTON-native proof job.

| Asset ID | Private pain bucket answered | Capture adjustment |
|---|---|---|
| `PLAN-SHOT-000` | SN2 owns bright wonder and franchise scale; HECTON clone risk is high if beauty is generic. | Show beautiful alien surface/photic water with material detail, route cost, and technogenic trace. Reject if it is generic reef/aquarium beauty or if it hides weak surface art with darkness. |
| `PLAN-SHOT-001` | SN2 owns bright wonder and franchise scale; HECTON clone risk is high if depth identity is generic. | Add industrial silhouette, pressure hardware, corrosion, and structured black-water route cue. Reject if it could be a generic diver-in-ocean frame or if surface content was darkened to fake abyssal identity. |
| `PLAN-SHOT-002` | Base-building praise/friction means base visuals must be readable, not only decorative. | Show gauge/seal/pipe affordances and a maintenance surface. Reject clean corridor beauty. |
| `PLAN-SHOT-003` | Negative reviews mention thin loops and missing player agency. | Tool, salvage target, hazard, and reward reason must be visible in one frame. |
| `PLAN-SHOT-005` | Save/death-loop frustration makes unfair failure risky. | Failure state must show a response path: repair, seal, reroute, or escape. |
| `PLAN-SHOT-006` | V7 agency/defensive-choice and screenshot-gap signal makes passive monster shots weak. | Threat must create a player choice through sonar/floodlight/route relation. Do not use a helpless monster-pose thumbnail. |
| `PLAN-SHOT-008` | Performance trust is fragile. | Keep internal unless measured. It can prove readability at low settings, not frame rate. |
| `PLAN-CLIP-001` | Pressure must be a system, not mood. | First 3 seconds: warning, leak, and player choice. |
| `PLAN-CLIP-002` | Atmosphere praise means HECTON must not lose wonder, only change its contract. | Let instrument/audio dread carry the reveal before creature/shape visibility. |
| `PLAN-CLIP-003` | Short-content complaints require route depth. | Salvage success must create a second decision, not end the clip. |
| `PLAN-CLIP-004` | Vehicle/machine fantasy is a gap opportunity. | Show weight and consequence; reject if the machine reads as a static prop or toy. |

Use the matching pain bucket in asset metadata as internal context only. Captions must remain HECTON-positive: beautiful alien water when the asset shows surface/photic routes, pressure, salvage, machinery, route risk, black-water depth, Seed Ship signal.

## Capture Naming

Use:

`H8_YYYYMMDD_AREA_SCENE_INTENT_TIER_TAKE.ext`

Examples:

- `H8_20260601_ABYSS_FLOODLIGHT_NOIR_HIGH_T01.png`
- `H8_20260601_BASE_PRESSURELEAK_GAMEPLAY_MED_T03.mp4`
- `H8_20260601_SEEDSHIP_SIGNAL_UI_LOW_T02.webm`

## Human Review Checklist

- Does it avoid multiplayer-scope implication?
- Does it avoid "Subnautica clone" read?
- Does it show pressure/machinery/salvage/black water?
- Is UI readable?
- Is the thumbnail legible at small size?
- Is the first second of the clip strong?
- Does the caption add meaning without explaining the whole scene?
