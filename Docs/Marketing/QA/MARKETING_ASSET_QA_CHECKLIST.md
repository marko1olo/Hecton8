# HECTON-8 Marketing Asset QA Checklist

Status: mandatory pre-publication gate
Owner lane: SHINOBU_81 / creative QA
Runtime impact: none

## Purpose

This checklist prevents expensive first-impression failure. Every screenshot, clip, capsule, post, press email, and creator pitch must pass a proof gate before public use.

## Universal Asset Gate

An asset is not publishable if it fails any of these:

- uses concept art while implying gameplay;
- implies co-op, multiplayer, live service, or MMO;
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
| Does not create a new promise | The asset does not imply co-op, performance, world size, feature completeness, or roadmap scope. |

Minimum creator-utility score for outreach use: 3/4.

Hard rule: a screenshot below 9/12 or a Steam screenshot below 10/12 is not publishable even if it unlocks many creator rows. A clip that fails the clip kill tests is not outreach-usable even if it matches a high-value creator.

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
- 1 threat/scale shot;
- 1 Seed Ship/anomaly shot only if the build can show it honestly;
- 1 optional low-spec proof shot only for internal QA until profiler evidence exists.

Hard reject the pack if more than half the shots need captions to explain the player verb.

## Clip QA

For 10-30 second clips:

- first 1.5 seconds must show motion or tension;
- first 3 seconds must communicate the hook;
- no dead camera panning unless it reveals threat/scale;
- no debug UI unless the point is a technical proof clip;
- no camera shake that hides mechanics;
- subtitles/captions must not cover the action;
- final frame must have a CTA only if platform/context allows it;
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
6. End: Steam wishlist CTA.

Forbidden:

- starting with logos for more than 1 second;
- long black screens;
- lore text wall;
- cinematic-only montage;
- feature list not backed by footage;
- co-op implication;
- "coming soon" without Steam page if the goal is wishlist conversion.

## Post QA

Every public post needs:

- one hook;
- one asset;
- one CTA or one discussion question, not both if it feels spammy;
- platform-native format;
- no repeated copy across communities;
- no fake "found this game" framing;
- disclosure if posted by developer/team.

### Reddit-Specific QA

Before posting:

- read subreddit rules the same day;
- check if self-promo is allowed;
- remove tracking links unless rules allow them;
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
- Steam link;
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
Co-op implication checked:
Performance claim checked:
Competitor copy checked:
Mobile readability checked:
Localization checked:
UTM/CTA checked:
Decision: Publish / Revise / Kill
Reason:
```
