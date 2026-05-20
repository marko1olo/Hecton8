# HECTON-8 Showcase And Festival Submission Playbook

Status: pre submission operating plan / not ready to submit
Owner lane: SHINOBU_81 / press and showcase ops
Public stance: single player first
Runtime impact: none

## Source Boundary

Primary current sources checked on 2026 05 19:

  Steam Upcoming Events: https://partner.steamgames.com/doc/marketing/upcoming_events?l=english&language=english
  Steam Themed Sale Events: https://partner.steamgames.com/doc/marketing/upcoming_events/themed_sales?l=english
  Steam Next Fest: https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest?language=english
  PC Gaming Show: https://www.pcgamingshow.com/
  Future Games Show: https://www.futuregamesshow.com/
  Day of the Devs Submissions: https://www.dayofthedevs.org/submit
  The MIX FAQ: https://archive.mediaindieexchange.com/faq/

Do not rely on this document for exact future deadlines. Showcase windows move. Recheck official pages before spending money, submitting, or announcing participation.

## 2026-05-20 Tracker Submission Permission Boundary V0

`Press/SHOWCASE_SUBMISSION_TRACKER.csv` now separates triage `status` from `submission_permission_gate`.

- Current pre-asset values must begin with `BLOCKED_`.
- `MONITOR` and `NOT_READY` are tracker states, not submission permission.
- No event submission can happen unless `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`.
- Steam Next Fest row `SHOW-001` is not exempt. Next Fest registration, commitment, participation claims, or event-beat reservation require the same `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED` in the tracker.
- The allow value requires same-day official rule/deadline check, fee/ROI decision, asset pack, Steam/CTA or private-review route custody, agency-decision proof fields where applicable, owner, and post-event measurement route.
- A private organizer review link can never become the public conversion path.

## Hard Truth

Showcases are not magic. A weak trailer inside a large stream is usually worse than a strong 20 second gameplay clip shown to the right creator. HECTON-8 should submit only when the asset package can survive a cold viewer who has never read the design docs.

## Submission Readiness Gates

HECTON-8 is not ready to submit until all are true:

  `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED` in the tracker row;
  public Steam page and CTA activation exist for public event traffic, or a private presskit URL exists only for non-public submission review;
  6 10 real in game screenshots pass QA score 10/12;
  45 75 second trailer or vertical cut exists;
  at least one gameplay loop is explainable in one sentence;
  one public asset proves a readable player decision under threat, leak, route cost, sonar pressure, or salvage failure, with AB-009/KPI decision-read fields recorded where the proof comes from first-page assets;
  no unsupported multiplayer-scope implication in assets/copy;
  no unproved performance claims;
  presskit has contact, factsheet, screenshots, logo, key/access policy;
  demo/build status is honest;
  no licensed placeholder asset is visible.

## Public Event vs Private Review Route Boundary V0

Showcases and festivals can request private review links, but public event traffic must never depend on private access routes.

| Route | Allowed link | Blocker |
|---|---|---|
| Public showcase trailer/event page | Official Steam/demo CTA only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED` where a demo is linked, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`. | Private preview build, unlisted key page, candidate handle, draft presskit URL, or any public CTA without its machine gate. |
| Organizer submission review | Private presskit or screener URL if the organizer requires it. | Missing owner custody, missing expiry/revocation, or unclear usage permission. |
| Creator/press pre-brief | Private access route only through review-key/access protocol. | Link can be forwarded publicly or is not logged to a recipient. |
| Post-event recap | Public Steam/demo/presskit URL only after the matching Steam/demo/press-release/public-CTA gates pass for the destination. | Metrics cannot map link/source/asset, the destination changed after the event, or the recap link lacks its machine gate. |

## Submission Asset Pack

Minimum package:

| Asset | Requirement | HECTON-8 note |
|---|---|---|
| 1 line pitch | 120 characters or less | Single player deep sea survival about pressure, salvage, machinery, and the Seed Ship anomaly. |
| Short description | 50 80 words | Must include player verbs. |
| Long description | 200 350 words | No roadmap fantasy. |
| Trailer | 45 75s | Show player action by second 10. |
| Gameplay clip | 20 30s clean capture | One hook only. |
| Screenshots | 6 10 | Real gameplay, no concept art, lead set includes one agency/decision proof asset with AB-009/KPI decision-read fields. |
| Logo | Transparent PNG/SVG if available | Readable on dark and light background. |
| Capsule/key art | If requested | No fake feature text. |
| Fact sheet | Markdown/PDF | Platform, genre, contact, current state. |
| Build | If requested | Stable route, no crash at first interaction. |
| Intro video | If requested | Day of the Devs style 2 3 minute dev intro only if actually needed. |
| Isolated audio | If requested | Some showcases request clean audio files. |

## Target Showcases

| Target | Fit | When to submit | Required proof | Risk |
|---|---|---|---|---|
| Steam Next Fest | High, if demo is strong | Only after `SHOW-001` has `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`; demo readiness or unreleased Steam state alone is not permission. | Public demo gate, Steam page publication gate, event eligibility, CTA custody, and tracker allow row. | One shot pressure; weak demo wastes the beat. |
| Steam Themed Fests | Medium/high depending theme | When theme matches tags and the tracker row has `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`. | Store page publication gate, eligible tags/demo gate where relevant, CTA custody, and official rule check. | Wrong theme attracts wrong audience. |
| PC Gaming Show | High if trailer looks PC premium | Strong trailer or demo beat | PC first visual hook, presskit | Very competitive; needs polish. |
| Future Games Show | High if trailer has broad visual punch | Trailer/world premiere/demo beat | Trailer and approved Steam CTA after activation | Broad audience; weak CTA loses traffic. |
| Day of the Devs | Medium/high if identity is distinctive | Playable or demo quality build | Playability, novelty, intro materials if selected | Highly selective; needs more than pretty underwater visuals. |
| The MIX | Medium/high for indie visibility | Demo/trailer and event budget clarity | Playable demo or strong trailer | Some events may have fees; verify ROI. |
| Guerrilla Collective | Medium if event open and relevant | Demo/trailer beat | Strong hook and approved Steam CTA after activation | Check current submission route and cost. |
| Indie Horror Showcase | High if horror angle is real | Horror forward demo/trailer | Dread, pressure, threat, industrial hostility. | Tag horror only if the current build proves the tone. |
| Gamescom indie/online beats | Medium | Regional/presskit gated asset pack | Trailer, demo, localization notes | Expensive if physical; online only until budget proof. |
| Tokyo Game Show / regional indie slots | Low/medium later | After localization and demo readiness | JP/KR/Asia one pager | Language/support expectations. |

## Event Fit Scoring

Score 0 3 each:

| Criterion | 0 | 1 | 2 | 3 |
|---|---|---|---|---|
| Audience fit | wrong audience | broad fit | strong genre fit | exact survival/horror/PC fit |
| Asset readiness | no asset | screenshots only | trailer | trailer + demo |
| Cost | unknown/high | paid unclear | low cost | free/Steam native |
| Timing | wrong beat | too early | workable | perfect asset/Steam timing |
| Conversion path | none | website only | Steam page | Steam page + approved demo/Steam CTA |
| Differentiation | generic | slight | strong | instantly distinct |

Submit only if score is 13/18 or higher. Exceptions require written rationale.

## HECTON-8 Showcase Angle By Venue

| Venue type | Angle |
|---|---|
| Steam demo event | "Try the pressure/salvage route yourself." |
| PC showcase | "A PC first industrial deep sea survival game with heavy machinery and hostile visibility." |
| Indie showcase | "Small team survival with a sharp identity: NASA punk below the light." |
| Horror showcase | "Deep sea pressure as dread, not jumpscare wallpaper." |
| Dev/industry showcase | "Fake first rendering and scalable systems; receipts only when measured." |
| Regional showcase | "Single player survival with localization ready pitch and regional creator hooks." |

## Timeline Backward From Event Date

| Time before event | Work |
|---|---|
| 12 weeks | Recheck submission rules, fees, asset requirements, eligibility. |
| 10 weeks | Choose asset hook, lock trailer beat, assign owner. |
| 8 weeks | Trailer rough cut, screenshot QA, Steam CTA activation check, and public/private route class check. |
| 6 weeks | Submit if ready; otherwise kill submission. |
| 4 weeks | Prepare event landing/Steam announcement/social queue. |
| 2 weeks | Prepare creator/press pre brief if allowed. |
| Event week | Monitor traffic, reply to comments, log sources. |
| 48h after | Post digest, evaluate UTM/wishlist/creator lift. |

## Submission Copy Template

```text
HECTON-8 is a single player first deep sea survival game about pressure, salvage, machinery, and a buried Seed Ship anomaly.

The player survives below the light by repairing industrial systems, scavenging hostile routes, and keeping bases and machines alive under pressure. The tone is NASA punk / deep sea noir: corrosion, black water, acoustic dread, heavy hardware, and failure states that are readable before they are fatal.

Current build status:
[honest build/demo state]

Best showcase angle:
[one sentence matched to venue]

Current public-scope boundary:
single player first scope, no live service progression promise, no unproved performance claims.
```

## Trailer Beat For Showcase

1. 0 3s: black water, pressure sound, machine light.
2. 3 8s: player action: repair, salvage, route, hatch, tool.
3. 8 18s: base/system pressure.
4. 18 30s: hostile route and environmental threat with a visible player decision.
5. 30 45s: Seed Ship anomaly or deep silhouette after agency proof exists.
6. 45 55s: short feature proof, not bullet spam.
7. Final 5s: title plus approved Steam/demo CTA after activation.

## Kill Rules

Do not submit if:

  trailer is mostly logos/text;
  no Steam page or CTA activation packet exists for a public event;
  a private review/access link would be exposed as the public conversion path;
  demo crashes in first route;
  asset uses concept art while implying gameplay;
  trailer or screenshots are atmospheric but do not show a player decision or lack the required AB-009/KPI decision-read field for first-page proof;
  the hook is "underwater survival like Subnautica";
  event fee consumes more than 20% of total marketing budget without a measurable Steam plan;
  no one is available to monitor the event live.

## Post Event Evaluation

Within 48 hours, write:

```text
Event:
Date:
Cost:
Assets shown:
CTA:
Route class:
Steam visits:
Wishlists:
Creator mentions:
Press mentions:
Useful comments:
Confusion comments:
Top negative signal:
Top positive signal:
Decision: repeat / revise / kill venue
```

## Current HECTON-8 Decision

Do not submit yet. Prepare asset pack and tracker now. The first legitimate submission point is after Steam page draft, screenshot pack, and trailer rough exist.
