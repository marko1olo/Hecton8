# Steam Page Asset Requirements Checklist

Status: working checklist / recheck official Steamworks docs before upload
Public stance: single-player-first / no co-op promise
Runtime impact: none

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R4 Interior Actuality Boundary

This document is active only where it agrees with current official Steamworks documentation, current project authority docs, and fresh asset/build evidence.

No Steam page, approved assets, trailer, demo, release date, Early Access plan, Next Fest eligibility, runtime feature proof, profiler proof, or player-build proof is implied unless this document links a fresh evidence artifact.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Official source to recheck before spending money or uploading:

- Steam Store Assets: https://partner.steamgames.com/doc/store/assets?language=english
- Steam Trailers: https://partner.steamgames.com/doc/store/trailer?language=english
- Steam Wishlists: https://partner.steamgames.com/doc/marketing/wishlist?language=english
- Steam Next Fest: https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest?language=english
- Steam Early Access: https://partner.steamgames.com/doc/store/earlyaccess

## Hard Boundary

This checklist does not mean HECTON-8 has a public Steam page, approved assets, trailer, demo, release date, Early Access plan, or Next Fest eligibility. Platform rules can change. Reread official Steamworks docs before upload or campaign commitments.

## Store Graphical Asset Working Sizes

These are planning targets from Steamworks documentation. Reverify before final export.

| Asset | Working size | Format notes | HECTON-8 direction |
|---|---:|---|---|
| Header capsule | 920 x 430 | PNG/JPG per Steamworks guidance | One floodlight, pressure silhouette, readable logo. |
| Small capsule | 462 x 174 | Must read at tiny size | Logo + one hard shape, no tiny detail. |
| Main capsule | 1232 x 706 | Feature/front-page style | Strong hero frame: vehicle/base/floodlight in black water. |
| Vertical capsule | Recheck official page before export | Steam may require current spec by surface | Build layout from layered 4K source. |
| Library capsule | 600 x 900 | Library portrait | Vertical pressure shaft / sub silhouette / strong logo. |
| Library hero | 3840 x 1240 | Wide background | No logo baked unless spec requires; leave room for overlay. |
| Library logo | 1280 wide and/or 720 tall transparent PNG | Transparent preferred | High-read HECTON-8 mark, white/salt/off-black variants. |
| Library header | 920 x 430 | Library detail header | Reuse capsule identity but less text clutter. |
| Screenshot | 1920 x 1080 minimum target | Real in-game only | Every shot must show a verb or a threat. |
| Trailer thumbnail | 1920 x 1080 working source | Must read in grid | One system problem, not generic monster face. |

## Steam Page Copy Blocks

### Short Description Candidate A

Single-player underwater survival below the light: pressure, salvage, machinery, black-water exploration, and a Seed Ship anomaly that makes the ocean stop feeling neutral.

### Short Description Candidate B

Build, repair, and survive in a NASA-punk deep-sea noir world where pressure, oxygen, power, and salvage routes decide whether you make it back alive.

### Short Description Candidate C

A single-player-first deep-sea survival game about pressure, machinery, hostile depth, and the cost of staying alive in black water.

## About This Game Structure

Use this order:

1. One paragraph promise.
2. What the player does.
3. What threatens the player.
4. What makes the world distinct.
5. What the build currently contains, once true.
6. No false roadmap claims.

Draft:

HECTON-8 is a single-player-first NASA-punk / deep-sea noir survival game about surviving under hostile depth. You scavenge, build, repair, and operate pressure-rated machinery while exploring black-water routes around a Seed Ship anomaly.

Depth is not just scenery. Pressure, oxygen, power, visibility, salvage risk, and base integrity shape every route. A habitat is not a cozy house; it is a machine keeping the ocean outside.

The world is industrial, corroded, and instrument-driven: floodlights, gauges, sonar, scratched glass, silt, wreckage, seals, pumps, and systems that fail loudly before they kill you.

## Feature Bullets

Use only when each feature is visible or playable.

- Single-player-first underwater survival focused on isolation and hostile depth.
- Pressure, oxygen, power, salvage, and base integrity as readable survival systems.
- Industrial NASA-punk visual language: metal, salt, grime, floodlights, gauges, and worn machinery.
- Black-water exploration built around instruments, route risk, and return planning.
- Habitat systems that feel like survival infrastructure, not decoration.
- Seed Ship anomaly layer affecting navigation, signals, and the long-term mystery.

Do not include:

- co-op;
- multiplayer;
- 100km ocean;
- zero stutter;
- realistic full-ocean simulation;
- "infinite procedural world";
- "Subnautica killer";
- "AI ecosystem" unless proved and explained.

## Screenshot Order For First Steam Page

| Slot | Goal | Required read |
|---:|---|---|
| 1 | Identity | Black-water, machinery, pressure, strong silhouette. |
| 2 | Player verb | Salvage, repair, build, scan, operate, or navigate. |
| 3 | Base systems | Habitat as machine: oxygen, seals, power, pressure, flooding. |
| 4 | Vehicle/tool | Heavy traversal or tool use under risk. |
| 5 | Threat | Creature/anomaly/environment shown through instrument/floodlight. |
| 6 | Seed Ship hook | Strange deep structure or corrupted systems. |
| 7 | Interior | Functional NASA-punk interior, not clean sci-fi lounge. |
| 8 | Scale | Player or machine dwarfed by abyss structure. |

Reject any first-page screenshot if it is:

- too dark to parse at thumbnail size;
- beautiful but actionless;
- derivative bright reef shot;
- placeholder UI;
- clean plastic lab;
- lore-only.

## 2026-05-19 Steam Page Build Ticket V0

Status: pre-capture / not upload-ready / use only after real build evidence exists.

This ticket converts the first `PLAN-*` metadata rows into a Steam page capture order. It exists so the first gameplay assets can be reviewed against a fixed bar instead of personal taste.

### Capture Order And Gate

| Order | Asset ID | Steam use | Required proof in frame/clip | Pass threshold | Reject code |
|---:|---|---|---|---|---|
| 1 | `PLAN-SHOT-001` | First screenshot / identity hero | Player-scale relation, black-water machinery silhouette, floodlight or pressure cue, no clean blue-ocean read. | 10/12 Steam QA and 4/5 cold readers can name genre plus mood. | `REJECT_EMPTY_DARK_WATER` or `REJECT_CLONE_FRAME` |
| 2 | `PLAN-SHOT-003` | Second screenshot / player verb | Salvage target, tool/interact path, hazard, reward, or visible consequence. | 10/12 Steam QA and 4/5 cold readers can name the verb without caption. | `REJECT_NO_PLAYER_VERB` |
| 3 | `PLAN-SHOT-002` | Third screenshot / pressure room | Gauges, seals, hatch/door, dirty glass, maintenance surfaces, pressure language visible. | 9/12 Steam QA; must not read as a clean sci-fi lounge. | `REJECT_CLEAN_SCI_FI` |
| 4 | `PLAN-SHOT-006` | Fourth screenshot / threat read | Threat, scale, or anomaly read through instrument/floodlight relation. | 9/12 Steam QA and 3/5 cold readers can identify danger source. | `REJECT_RANDOM_MONSTER_POSE` |
| 5 | `PLAN-SHOT-005` | Fifth screenshot / base under stress | Leak/flood/warning plus player response path; no purely decorative red UI. | 10/12 Steam QA only if the failure is honest in the current build. | `REJECT_FAKE_FAILURE` |
| 6 | `PLAN-SHOT-004` | Sixth screenshot / heavy machine | Vehicle/tool/pump/ballast mass and readable scale. | 9/12 Steam QA and no toy/plastic read. | `REJECT_TOY_MACHINE` |
| 7 | `PLAN-SHOT-007` | Seventh screenshot / Seed Ship signal | Instrument corruption, route pull, or anomaly effect visible in-world. | 8/12 Steam QA; promote earlier only if cold readers call it unique. | `REJECT_ABSTRACT_LORE_GLOW` |
| 8 | `PLAN-SHOT-008` | Internal low-spec/readability check | Same identity/verb clarity at lower settings; no public FPS implication. | Internal only until hardware/settings/profiler proof exists. | `REJECT_UNPROVED_PERF_SIGNAL` |
| 9 | `PLAN-CLIP-001` | Trailer beat / pressure leak | System problem, readable warning, player action, consequence. | First 3 seconds must work muted. | `REJECT_SLOW_START` |
| 10 | `PLAN-CLIP-002` | Trailer beat / sonar threat | Sonar/contact/instrument first, threat second, no generic monster reveal. | Viewer understands danger before title card. | `REJECT_GENERIC_THREAT` |
| 11 | `PLAN-CLIP-003` | Trailer beat / salvage failure | Attempt, friction, failure or near-failure, recovery hook. | Clip has a complete 5-12 second micro-story. | `REJECT_NO_ARC` |
| 12 | `PLAN-CLIP-004` | Trailer beat / heavy machine startup | Mechanical mass, delay, sound, motion, risk. | Reads heavy without caption. | `REJECT_WEIGHTLESS_MACHINE` |
| 13 | `PLAN-CAPSULE-001` | Capsule rough A/B/C | Logo readable, one unique HECTON-8 cue, not one-note blue/black. | Tiny-size cold read beats a plain logo and does not look derivative. | `REJECT_UNREADABLE_CAPSULE` |

### Review Packet Required Before Steam Draft

- Actual asset filename and build ID are filled in `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv`.
- QA score and rejection code are filled for every kept or killed asset.
- At least five cold-reader notes exist for the first capsule plus first three screenshots.
- Steam asset specs are rechecked on the official Steamworks pages the same week.
- No copy says co-op, multiplayer, 100km, zero stutter, infinite procedural world, or Subnautica killer.
- If fewer than three screenshots pass 10/12, do not publish the Steam page; recapture instead.

## Trailer Structure

Target: 55-75 seconds for reveal/trailer, 20 seconds for short-form cut.

### 60-Second Trailer Beat Sheet

| Time | Beat | Notes |
|---:|---|---|
| 0-5s | Cold open | One strong pressure/floodlight/machine image. No logo first. |
| 5-12s | Player context | Descent, base, tool, oxygen, route. |
| 12-22s | Machinery | Show pumps, locks, gauges, tools, habitat systems. |
| 22-34s | Exploration | Black-water route, salvage, signal, visibility loss. |
| 34-45s | Consequence | Leak, pressure warning, power loss, threat silhouette. |
| 45-55s | Seed Ship | Anomaly/instrument corruption, not lore dump. |
| 55-65s | Title + Steam CTA | HECTON-8, wishlist/demo only when real. |

### 20-Second Short Beat Sheet

| Time | Beat |
|---:|---|
| 0-2s | Warning or sonar contact. |
| 2-7s | Player sees machine/system problem. |
| 7-14s | Action under pressure. |
| 14-18s | Consequence/reveal. |
| 18-20s | Logo + Steam CTA. |

## Capsule Art Direction

Must include:

- high contrast silhouette;
- readable logo at small size;
- one unique HECTON-8 cue: pressure machinery, floodlight, black-water industrial shape, Seed Ship anomaly, or worn sub/habitat;
- color contrast beyond one-note blue/black;
- no tiny UI/detail dependence.

Avoid:

- generic diver in blue ocean;
- bright tropical reef;
- monster face only;
- clean sci-fi hallway;
- unreadable black rectangle;
- text-heavy capsule;
- "from fans of Subnautica" phrasing.

## Store Page QA

Cold viewer should answer in 10 seconds:

1. What genre is it?
2. Is it single-player or multiplayer?
3. What is the main fantasy?
4. What makes it different from other underwater survival games?
5. What can the player do?
6. Why should they wishlist now?

If answer 4 is "it looks like Subnautica but darker", revise capsule/screenshot order.

## Launch Readiness Checklist

- [ ] Store name final.
- [ ] Short description final.
- [ ] About text proofread.
- [ ] Tags selected and justified.
- [ ] Header capsule readable at tiny size.
- [ ] Small capsule readable at tiny size.
- [ ] Main capsule tested against survival competitors.
- [ ] Library assets exported from layered source.
- [ ] 8 real screenshots captured in final aspect.
- [ ] Trailer uploaded and thumbnail tested.
- [ ] No co-op language anywhere.
- [ ] No unproved performance claims.
- [ ] Early Access questionnaire accurate if applicable.
- [ ] Demo page/build rules checked if demo is used.
- [ ] Next Fest eligibility checked before event planning.
- [ ] Press kit linked only after assets are final.
- [ ] UTM/tracking plan ready outside Steam where allowed.

## Paid Spend Gate

Do not buy ads until:

- Steam page exists;
- capsule has been cold-read tested;
- at least 100 organic visitors exist for baseline;
- wishlist conversion is tracked;
- one creator/press batch produces signal;
- there is a demo or strong enough trailer/clip for landing traffic.

If paid traffic converts poorly, stop within 48 hours and fix capsule/page/assets before buying more.
