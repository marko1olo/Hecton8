# Steam Wishlist And Next Fest Plan

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Status: pre page preparation
Public stance: single player first scope / proof first public copy

## Sources

  Steam wishlists: https://partner.steamgames.com/doc/marketing/wishlist?language=english
  Steam Next Fest: https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest?language=english
  Steam graphical assets: https://partner.steamgames.com/doc/store/assets?language=english
  Steam trailers: https://partner.steamgames.com/doc/store/trailer?language=english
  Steam Early Access: https://partner.steamgames.com/doc/store/earlyaccess
  Steam keys: https://partner.steamgames.com/doc/features/keys

## R18 Official Source Check

Checked against Steamworks documentation on 2026 05 18:

  Next Fest remains a one shot event per title. Do not enter until the demo and store page can survive public traffic.
  Eligibility requires an upcoming unreleased base game, a public visible base game store page, and a publicly playable demo by the time the event begins.
  A prior/current Steam Playtest does not block eligibility, but splitting attention between Playtest and demo during Next Fest is a bad funnel.
  Early Access is not crowdfunding or a future promise vehicle. Public copy must describe the current playable value and avoid specific guarantees about future features, dates, or completion.

Recheck the official Steamworks pages before registration, store launch, demo publish, Early Access launch, key distribution, or paid campaign spend.

## Next Fest Commitment Boundary

Steam Next Fest is tracked as `SHOW-001` in `Press/SHOWCASE_SUBMISSION_TRACKER.csv`. Do not register, commit, announce participation, reserve the event beat, or report Next Fest readiness from this document, Campaign 04 prose, Steam page readiness, public demo readiness, or CTA readiness alone.

Machine route:

- current tracker value: `submission_permission_gate = BLOCKED_NOT_READY`;
- only future allow value: `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED` on `SHOW-001`;
- `steam_page_publish_permission_gate`, `demo_public_access_permission_gate`, `public_cta_permission_gate`, and `steam_announcement_permission_gate` are required downstream gates, but none of them can replace the `SHOW-001` submission gate.

The allow path requires same-day Steamworks Next Fest rule/deadline recheck, one-shot eligibility confirmation, Steam page publish gate, public demo access gate, public CTA gate for any public link, support route gate, announcement gate for event posts, asset pack proof, agency-decision fields where claimed, owner, rollback/withdrawal owner, and post-event measurement route.

## Steam Page Goal

The Steam page is the primary funnel. Social posts, creator videos, Reddit critique threads, and press mentions should drive to Steam wishlist only after the page has proof assets and `Analytics/MEASUREMENT_AND_UTM_PLAN.md` Official CTA Link Activation Gate V0 passes.

Machine page-publication gate:

- current value: `steam_page_publish_permission_gate = HOLD_NO_STEAM_PAGE_PUBLICATION`;
- future allow value: `ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`;
- a store draft, asset packet, candidate app URL, CTA packet, Steam announcement draft, or press release gate cannot publish the Steam page by itself.

Do not launch the page as a vague underwater project. Launch only when it has:

  `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`;
  key art/capsule draft;
  6 10 in game screenshots;
  short description that survives cold reader test;
  tags aligned with the actual game;
  one short trailer or at minimum a strong microtrailer plan;
  one agency/decision proof asset from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` with non-pending metadata `viewer_named_decision`, valid non-held `capture_verdict`, and AB-009/KPI viewer-named decision fields;
  single-player-first scope boundary;
  no fake performance claim.

## Store Page Copy Drafts

### Short Description Draft A

HECTON-8 is a single player first underwater survival game about a beautiful alien ocean, pressure, salvage, heavy machinery, and deep sea noir isolation. Build below the light, keep the systems alive, and find what is corrupting the black water.

### Short Description Draft B

Survive an industrial ocean nightmare where pressure is the enemy, machinery is shelter, and every meter below the light costs oxygen, power, and steel.

### Short Description Draft C

NASA punk underwater survival from bright alien shallows into black water. Salvage wrecks, maintain pressure rated habitats, pilot heavy machines, and follow a corrupted signal into the deep.

### Short Description Draft D

HECTON-8 is deep sea noir survival built around pressure, corrosion, machinery, salvage, and isolation below the light.

### Short Description Draft E

A single player underwater survival game where the safest room is still under crushing pressure.

## Long Description Skeleton

### Survive Below The Light

HECTON-8 is a single player first survival game set in an industrial deep ocean system. The ocean is hostile, contaminated, and unforgiving. Pressure, power, oxygen, salvage, and failing machinery define every expedition.

### Build Systems That Keep You Alive

Habitats are pressure-rated machines under load. Power, sealing, pressure risk, flooding, maintenance, and visibility must stay readable. Failure should feel earned, not random.

### Salvage The Wreckage

Progress comes from dangerous recovery: tools, sealed doors, broken modules, black box traces, damaged machines, and routes that become more hostile the deeper the player goes.

### Follow The Seed Ship Signal

Something at depth is corrupting instruments, wildlife, plant behavior, radar, and the ocean itself. The Seed Ship should be sold as a systemic threat, not as lore homework.

### Built For Proof-Bound Claims

Performance claims wait for measured build and hardware artifacts. Public copy may mention frame rate, low-end hardware, stutter, or GC only when the current evidence packet proves it.

## Tag Strategy

Primary candidates:

  Survival
  Open World Survival Craft
  Underwater
  Exploration
  Base Building
  Atmospheric
  Singleplayer
  Sci fi
  Horror
  Crafting
  Resource Management
  Simulation
  Immersive
  Adventure
  First Person

Avoid unless proven:

  Multiplayer-mode tags
  MMO
  PvP
  Souls like
  Roguelike
  Colony Sim
  Realistic

## Screenshot Requirements

Minimum first page batch:

1. exterior abyss + industrial structure;
2. cockpit/interior machinery;
3. base under pressure/flood warning;
4. salvage/wreck interaction;
5. hostile silhouette with a readable player choice;
6. Seed Ship anomaly hint after agency proof exists;
7. minimum-quality readable frame;
8. heavy vehicle or exosuit if actually present.

Every screenshot must answer at least one question:

  What is dangerous?
  What is mechanical?
  What is uniquely HECTON-8?
  What is the player doing?
  What decision is the player making under pressure?

`PLAN-SHOT-007` can strengthen mystery, but it cannot replace the agency/decision proof asset, metadata handoff, and AB-009/KPI decision-read fields required for the first public page.

## Trailer Requirements

First second must show conflict, not logo.

Structure:

  0 3s: pressure/machinery/threat hook;
  4 15s: survival loop: move, scan, salvage, maintain;
  16 30s: base/machine consequence;
  31 45s: Seed Ship/anomaly escalation;
  final 3s: title, single player first, gated Steam CTA after `steam_page_publish_permission_gate` and `public_cta_permission_gate` pass; otherwise no-link title card.

Forbidden:

  20 seconds of slow underwater swimming before gameplay;
  lore narration before visible systems;
  "wishlist now" before the game is understood or before CTA activation passes;
  unsupported multiplayer-scope hint;
  performance claims without overlay proof.

## Next Fest Readiness

Do not enter until:

  `SHOW-001` in `Press/SHOWCASE_SUBMISSION_TRACKER.csv` has `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`;
  Steam page publication has `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, asset pack proof, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for public links;
  public demo/Playtest access has `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED` and the reviewed build has a logged known-issues/rollback owner;
  opening minute sells pressure/machinery;
  demo and page assets show one readable player decision under threat, leak, route cost, sonar pressure, or salvage failure, with non-pending metadata `viewer_named_decision`, valid non-held `capture_verdict`, and `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` recorded for page assets where applicable;
  no blocker bugs in demo path;
  creator outreach batch passes creator utility, `creator_send_gate`, CRM send-log, and official route gates;
  daily reporting sheet is ready;
  livestream build proof is logged with owner, build ID, known-issues state, rollback owner, and exact event/post permission gates.

Risk: Steam Next Fest can only be used once for a game. Burning it with a weak demo is not marketing; it is waste.

## Daily Steam Metrics

Track:

  page visits;
  wishlists;
  visit to wishlist rate;
  source UTM where available;
  demo downloads;
  demo completion;
  discussion sentiment;
  top tags;
  refunds/negative review themes after launch;
  creator traffic spikes.

## Kill Criteria

  visit to wishlist under 5% after copy/art iteration;
  screenshot comments dominated by "Subnautica clone";
  tags drift toward multiplayer-mode promises;
  demo feedback dominated by confusion;
  performance questions cannot be answered with proof.
