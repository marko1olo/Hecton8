# HECTON-8 Marketing Asset QA Checklist

Status: mandatory pre-publication gate
Owner lane: SHINOBU_81 / creative QA
Runtime impact: none

## Purpose

This checklist prevents expensive first-impression failure. Every screenshot, clip, capsule, post, press email, and creator pitch must pass a proof gate before public use.

## Universal Asset Gate

An asset is not publishable if it fails any of these:

- uses concept art while implying gameplay;
- implies unsupported multiplayer scope, live service scope, or MMO scope;
- claims performance without a build/profiler receipt;
- needs a paragraph to explain what the player is doing;
- looks like generic sci-fi plastic;
- uses "Subnautica killer" or similar public competitor-war language;
- hides the game behind logos, fog, or UI text;
- shows empty space without route, hazard, machinery, or goal;
- has illegible text at mobile size;
- uses broken localization or machine-translated slang;
- contains unreleased third-party asset/license risk.

## Screenshot QA

Each screenshot must answer at least two of these questions without caption help:

- Where is the player?
- What is dangerous?
- What can the player interact with?
- What is the scale?
- What makes this HECTON-8, not generic underwater sci-fi?
- What is the next action the player wants to take?

### Screenshot Scorecard

Score 0-2 each.

| Criterion | 0 | 1 | 2 |
|---|---|---|---|
| Genre clarity | unclear | partly clear | instantly clear |
| HECTON identity | generic | some identity | pressure/machinery/noir unmistakable |
| Gameplay implication | static scene | implied action | clear player goal/problem |
| Readability | noisy/dark | readable after looking | readable at thumbnail size |
| Novelty | familiar | decent | hard to confuse with competitor |
| Honesty | staged/fake-looking | plausible | real gameplay proof |

Minimum publish score: 9/12.
Minimum Steam screenshot score: 10/12.

### Creator Utility Gate

Creator unlock value can prioritize capture order, but it cannot override visual proof.

Score 0-1 each:

| Utility check | Pass condition |
|---|---|
| Unlocks a named CRM segment | The metadata row names a real `VERIFY_BEFORE_CONTACT` or `NEEDS_ASSET` group that would use the asset. |
| Matches the creator format | Screenshot for screenshot-tolerant rows; clip/demo for gameplay-first or `NEEDS_ASSET` rows. |
| Supports one pitch angle | The asset proves exactly one angle: pressure, salvage, base-as-machine, abyss dread, heavy machinery, or Seed Ship signal. |
| Does not create a new promise | The asset does not imply unsupported multiplayer scope, performance, world size, feature completeness, or roadmap scope. |

Minimum creator-utility score for outreach use: 3/4.

Hard rule: a screenshot below 9/12 or a Steam screenshot below 10/12 is not publishable even if it unlocks many creator rows. A clip that fails the clip kill tests is not outreach-usable even if it matches a high-value creator.

### SN2 Pain-Point Proof Gate

Subnautica 2 pain signals are internal capture-priority inputs only. They are not public attack copy.

Freshness check: before scoring a first-pack asset against this gate, confirm the pain bucket is still current in `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md`. Record the exact refresh or source row in `pain_freshness_source` and the check date in `pain_freshness_checked_at`. If a competitor patch, roadmap, or review trend reduces the pain, mark the bucket `MONITOR_ONLY` and do not use it to prioritize the asset.

Score 0-1 each:

| Proof check | Pass condition |
|---|---|
| Fresh pain signal | The monitoring file has a same-day or current-week entry showing the bucket is still active, not only historical launch-week noise. |
| Answers one real audience fear | The asset clearly addresses one private bucket from `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md`: thin loop, trust, no player verb, base friction, defensive agency, performance proof boundary, save/recovery fairness, or clone risk. |
| Keeps HECTON identity primary | The frame still reads as pressure, machinery, salvage, corrosion, black water, Seed Ship signal, or base-as-machine before it reads as "response to competitor complaint." |
| Does not quote competitor pain | Caption, pitch, and metadata do not mention SN2 bugs, EULA, multiplayer-scope issues, poor performance, or "we do it better." |
| Has a proof route | The asset can point to an internal proof field: asset ID, build/source, QA score, creator utility score, reject code, or later profiler/build artifact if performance is involved. |

Minimum pain-point proof score for first screenshot pack priority: 4/5, with `pain_freshness_source` and `pain_freshness_checked_at` filled from the monitoring source. Planned rows stay at `PENDING_SAME_DAY_REFRESH` / `PENDING_CAPTURE` until real capture QA.

Hard rule: this score is a priority modifier only. It cannot make a weak asset publishable, cannot override creator utility, and cannot create a performance, multiplayer-scope, content-hour, save-safety, or feature-completeness claim.

### Agency/Decision Proof Gate

This gate exists because threat, anomaly, darkness, and machinery can look strong while still showing no player choice. It is a first-packet gate, not a vibe score.

Use these metadata values:

| Gate value | Meaning |
|---|---|
| `AGENCY_PROOF_CANDIDATE` | A cold viewer should be able to name a player choice such as repair, retreat, reroute, scan, operate, abort, or recover without a caption. |
| `SUPPORTING_AGENCY_SIGNAL` | The asset contains pressure, route cost, tool use, or hazard context, but it is not enough alone for the first public packet. |
| `SUPPORTING_PLAYER_VERB_NOT_FIRST_GATE` | The asset shows action, but the consequence or choice is not explicit enough to satisfy the agency gate by itself. |
| `NOT_AGENCY_PROOF` / stricter non-proof value | Identity, machinery, anomaly, capsule, or internal proof only. It cannot satisfy Campaign 01 agency proof. |

First-packet agency proof must come from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` unless the control tower is updated with a new planned ID. `PLAN-SHOT-007` Seed Ship/anomaly can raise curiosity, but it cannot substitute for a player decision.

### First Screenshot Pack Capture Gate

Before capture, assign every planned shot one job. If a shot has no job, do not capture it.

| Shot job | Must show | Reject if |
|---|---|---|
| Identity hero | black water, industrial silhouette, pressure/machinery cue | it is just pretty dark water or a generic underwater vista |
| Player verb | tool, salvage target, repair, scan, build, pilot, or route decision | player action needs a caption to understand |
| Base system | gauge, leak, power conduit, pump, seal, pressure door, alarm, or maintenance surface | base reads as cozy room or clean sci-fi corridor |
| Threat/scale | sonar mark, huge silhouette, unsafe route, hull warning, depth/pressure implication | threat is only a monster pose with no player decision |
| Seed Ship/anomaly | instrument corruption, impossible structure, environmental distortion, route pull | anomaly is only abstract color/glow |
| Low-spec proof | same identity readable without overkill effects | used as public performance claim without measured build data |

Mandatory first pack composition:

- 2 identity/gameplay exterior shots;
- 2 base/interior machinery shots;
- 1 salvage/player-action shot;
- 1 agency/decision proof: threat/scale shot with a readable choice, pressure-leak decision clip, or salvage-failure decision clip;
- 1 Seed Ship/anomaly shot only if the build can show it honestly;
- 1 optional low-spec proof shot only for internal QA until profiler evidence exists.

Hard reject the pack if more than half the shots need captions to explain the player verb.

### First Capture Session QA Verdict

Use after the first capture session from `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md`. This is a triage verdict, not publication approval.

| Verdict | Minimum evidence | Marketing action |
|---|---|---|
| `KEEP_TESTING` | 3 stills score 9/12+; one still has a clear player verb; one still proves pressure/machinery; one candidate from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` has an agency/decision proof gate; no unsupported multiplayer-scope, performance, or competitor-war implication; metadata fields are factual. | Move into Campaign 01 cold-read testing. |
| `REVISE_SCENE` | Reject codes are concentrated in fixable issues: `TOO_DARK`, `UI_UNREADABLE`, `BAD_COMPOSITION`, missing affordance, or `AGENCY_MISSING_HOLD`. | Do not post. Send fix notes to capture/build owner. |
| `HOLD_ASSET` | Planned feature cannot be shown honestly in current build. | Keep metadata `PLANNED_CAPTURE`; do not replace with concept art. |
| `KILL_ANGLE` | Repeated `GENERIC_VISUAL`, `DERIVATIVE_COMPETITOR_READ`, `CONCEPT_NOT_GAMEPLAY`, or `NO_PLAYER_VERB` after three attempts. | Remove the angle from first pack and choose a smaller proof. |

Session cannot advance if any required metadata or handoff field would be guessed: file path, build/source, QA score, creator rows unlocked, creator utility score, creator send gate, pain proof score, pain freshness source/date, public comparison gate, agency decision proof gate, agency decision notes, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, `capture_next_actions`, or reject code.

## Clip QA

For 10-30 second clips:

- first 1.5 seconds must show motion or tension;
- first 3 seconds must communicate the hook;
- no dead camera panning unless it reveals threat/scale;
- no debug UI unless the point is a technical proof clip;
- no camera shake that hides mechanics;
- subtitles/captions must not cover the action;
- final frame must use a public Steam/demo CTA only after the exact Steam page or public demo gate passes and `Analytics/MEASUREMENT_AND_UTM_PLAN.md` records destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`; otherwise use no-link end copy;
- audio must sell pressure, machinery, or dread.

### Clip Kill Tests

Kill the clip if:

- it only works because the caption explains it;
- viewers ask if it is pre-rendered;
- it looks slower than intended;
- the UI distracts from the core action;
- the clip shows a system that will not ship in the next public build.

## Steam Capsule QA

Capsule must:

- show the project name clearly;
- carry one dominant image/shape;
- stay readable at small capsule size;
- avoid tiny face/details;
- avoid pure blue underwater sameness;
- avoid copycat composition;
- indicate pressure/industrial/noir identity;
- not include feature claims.

Capsule failure modes:

- too clean;
- too dark to read;
- too much text;
- generic diver silhouette;
- generic alien fish;
- looks like a horror movie poster instead of a game;
- title unreadable over texture.

## Trailer QA

Trailer must show actual gameplay truth early.

Required beats for first trailer:

1. 0-5s: visual identity and pressure hook.
2. 5-15s: player action, not just scenery.
3. 15-30s: survival/base/salvage system proof.
4. 30-45s: danger or failure state.
5. 45-60s: Seed Ship/anomaly curiosity.
6. End: gated Steam/demo CTA only after the exact Steam page or public demo gate plus destination-specific public CTA gate pass, or no-link title card if any gate has not passed.

Forbidden:

- starting with logos for more than 1 second;
- long black screens;
- lore text wall;
- cinematic-only montage;
- feature list not backed by footage;
- multiplayer-scope implication;
- wishlist or "coming soon" CTA without `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`.

## Post QA

Every public post needs:

- `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED` for the exact post;
- one hook;
- one asset;
- one gated CTA or one discussion question; no external link if the exact destination gate and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` have not passed;
- platform-native format;
- no repeated copy across communities;
- no fake "found this game" framing;
- disclosure if posted by developer/team.

### Reddit-Specific QA

Before posting:

- read subreddit rules the same day;
- check if self-promo is allowed;
- remove tracking links unless same-day platform/community rules permit them and the exact destination has `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`;
- disclose developer status;
- make the post useful without requiring a click;
- do not post the same asset to multiple subs in one wave;
- log removal/warnings.

## Creator Pitch QA

Do not send if:

- the creator has not been active recently;
- the channel language is not confirmed;
- the pitch does not name why their audience fits;
- the build/asset requested is unavailable;
- contact route is unverified;
- the message implies paid placement without disclosure;
- the creator is on denylist/fraud watch.

Minimum pitch components:

- one personalized opener;
- one fit reason;
- one asset/build offer;
- one honest boundary;
- one lightweight CTA;
- opt-out friendly ending.

## Press Kit QA

Press kit must include:

- factsheet;
- current screenshots;
- logo/key art;
- trailer link or placeholder date;
- contact email;
- Steam link only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`;
- disclosure of Early Access/pre-alpha state if relevant;
- no fake awards, quotes, or metrics.

Do not publish presskit with empty folders labeled as final.

## Localization QA

Every localized post must be checked by a native/fluent reviewer if it is used for paid spend, press, or creator outreach.

Machine translation is acceptable only for internal draft rows.

Risk terms:

- survival;
- pressure;
- base building;
- salvage;
- NASA-punk;
- noir;
- Seed Ship;
- demo;
- Early Access;
- wishlist.

These must not be mistranslated into promises the build cannot satisfy.

## Final Public-Asset Signoff

```text
Asset:
Date:
Owner:
Use case:
Build/source:
Proof type:
Pain bucket answered:
Pain proof score:
Pain freshness source:
Pain freshness checked at:
Public comparison gate:
Agency decision proof gate:
Agency decision notes:
Capture handoff packet ID:
Capture verdict:
Viewer-named decision:
Capture next actions:
Multiplayer-scope implication checked:
Performance claim checked:
Competitor copy checked:
Mobile readability checked:
Localization checked:
UTM/CTA activation packet checked:
Decision: Publish / Revise / Kill
Reason:
```
