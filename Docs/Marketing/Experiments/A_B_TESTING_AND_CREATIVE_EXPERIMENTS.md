# HECTON-8 A/B Testing And Creative Experiments

Status: pre-asset experiment plan
Owner lane: SHINOBU_81 / marketing measurement
Runtime impact: none

## Purpose

This document turns the marketing plan into controlled experiments. The project has a small cash budget, so guessing is too expensive. Agent labor is cheap; paid reach is not.

No paid spend is allowed until an experiment has:

- one measurable hypothesis;
- one asset family;
- one audience segment;
- one CTA;
- one decision threshold;
- one stop rule.

## Experiment Ladder

| Stage | Cost | What is tested | Output |
|---|---:|---|---|
| Desk test | 0 USD | Screenshot/copy clarity with internal agents. | Kill weak assets before public use. |
| Cold-reader test | 0-50 USD | Does a stranger understand the game in 5 seconds? | Capsule/screenshot winner. |
| Organic micro-post | 0 USD | Do players comment with the intended nouns? | Hook clarity. |
| Creator micro-pitch | 0 USD | Do verified creators reply? | Segment fit. |
| Paid micro-test | 25-150 USD | Does one asset drive tracked Steam visits/wishlists? | Spend or stop decision. |
| Scaled paid test | 250-750 USD | Does the winning hook survive a broader audience? | Launch support decision. |

## Core Hook Families

### H1 - Pressure And Machinery

Hypothesis: The strongest differentiation is "industrial survival under crushing pressure".

Assets:

- pressure hatch closeup;
- leaking bulkhead;
- compressor/pump room;
- vehicle docking arm;
- gauge/visor pressure warning.

Success comments:

- "looks heavy";
- "the machinery looks dangerous";
- "I want to hear how that sounds";
- "finally not clean sci-fi".

Failure comments:

- "where is the gameplay?";
- "generic submarine horror";
- "looks like a corridor".

### H2 - Seed Ship Mystery

Hypothesis: The Seed Ship anomaly can create story curiosity without lore dump.

Assets:

- distant silhouette under black water;
- corrupted scanner/radar;
- plant/fungal anomaly around machinery;
- instrument text distortion;
- route blocked by unnatural growth.

Success comments:

- "what is that thing?";
- "this looks wrong in a good way";
- "I want to go down there".

Failure comments:

- "AI-looking concept art";
- "generic alien artifact";
- "can't tell what the player does".

### H3 - Base Survival Risk

Hypothesis: Base building becomes stronger when the base can fail under pressure and bad engineering.

Assets:

- base module under external pressure;
- repair route;
- flooded compartment;
- power/routing UI;
- salvage-fed expansion.

Success comments:

- "base building with consequences";
- "I want to manage that system";
- "looks tense".

Failure comments:

- "too much UI";
- "looks tedious";
- "another crafting grid".

### H4 - Heavy Vehicle Feel

Hypothesis: Heavy vehicle traversal can own the "mechanical" lane SN2 may not fully satisfy.

Assets:

- slow exosuit step;
- vehicle mass turn;
- docking clamp;
- hydraulic arm;
- hull groan audio waveform/clip.

Success comments:

- "that thing feels heavy";
- "love the industrial movement";
- "more vehicle footage".

Failure comments:

- "looks slow";
- "clunky";
- "where is the speed/fun?"

## A/B Test Matrix

Legacy concept rows below are retained as idea seeds. Executable test IDs start in `2026-05-19 Asset-Gated Experiment Briefs`.

| Test ID | Asset A | Asset B | Audience | Metric | Winner threshold |
|---|---|---|---|---|---|
| CONCEPT-001 | Pressure hatch screenshot | Seed Ship silhouette screenshot | Internal cold readers | 5-second correct genre read | 70% correct read. |
| CONCEPT-002 | No text capsule | 3-word text capsule | Steam-adjacent players | Click intent vote | +20% preference. |
| CONCEPT-003 | Base failure clip | Vehicle weight clip | Survival creators | Reply/interest rate | 2x replies or stronger qualitative pull. |
| CONCEPT-004 | "Deep sea survival" short desc | "NASA-punk survival" short desc | Cold readers | Genre clarity | Lower confusion wins. |
| CONCEPT-005 | Reddit critique post | Reddit devlog post | Relevant communities | Useful comments per view | More concrete questions wins. |
| CONCEPT-006 | Trailer starts with danger | Trailer starts with beauty | Creator preview group | Watch-through intent | Higher "keep watching" count. |

## 2026-05-19 Asset-Gated Experiment Briefs

Status: pre-capture / 0 USD spend / do not run until matching `PLAN-*` assets exist.

These briefs are the first executable experiments. They are not creative suggestions; each has an asset gate, sample gate, metric, and stop rule.

| Test ID | Stage | Asset family | Hypothesis | Audience | CTA | Metric | Stop rule |
|---|---|---|---|---|---|---|---|
| AB-001 | Cold-reader | `PLAN-SHOT-001` vs `PLAN-SHOT-003` | Identity hero beats salvage only if the viewer can name genre and player fantasy in 5 seconds. | 15 cold readers, no project context. | "What is this game?" | 70% correct genre read and fewer than 2 "Subnautica clone only" responses. | Kill the weaker first screenshot if it is pretty but actionless. |
| AB-002 | Cold-reader | `PLAN-CAPSULE-001` rough A/B/C | Small-size capsule readability matters more than cinematic detail. | 15 Steam-adjacent players or agents. | "Which would you click on Steam?" | Winner is +20% preference and title remains readable at tiny size. | Do not commission/polish if no variant beats plain logo + hero silhouette. |
| AB-003 | Organic micro-post | `PLAN-SHOT-005` vs `PLAN-SHOT-004` | Base risk will outperform heavy machinery only if viewers understand consequence, not just mood. | X/Bluesky/Reddit critique-safe surface after handle custody exists. | Comment question, no wishlist CTA. | Useful comments naming system/verb per 100 views. | Kill if comments are mostly "looks cool" or "what do you do?" |
| AB-004 | Copy desk test | Short description A vs B vs C | Direct pressure/salvage copy should beat NASA-punk wording until visuals teach the term. | 10 internal reads + 15 humans after first screenshot exists. | Pick what the game is. | Lowest confusion and lowest co-op assumption wins. | Remove any copy that implies multiplayer, simulation scope, or mood-only game. |
| AB-005 | Creator micro-pitch | `PLAN-CLIP-003` vs `PLAN-CLIP-004` | Survival/systems creators reply more to a complete salvage failure than a heavy-machine beauty shot. | 20 verified creator contacts from CRM after asset/contact gates pass. | "Do you want a private preview when ready?" | Reply quality, not raw reply count. | Stop batch if 3+ creators ask for missing features or asset proof. |
| AB-006 | Trailer opening | `PLAN-CLIP-001` pressure leak vs `PLAN-CLIP-002` sonar contact | A system problem in the first 3 seconds should beat a monster/contact reveal if HECTON-8 is selling machinery survival. | 15 cold viewers + 5 creator/editor opinions. | "Would you keep watching?" | Keep-watching count and correct game-description nouns. | Recut if viewers say "generic underwater horror" or cannot name player action. |
| AB-007 | Steam tag proof | First six passing `PLAN-SHOT-*` | Top tag stack should be decided by what screenshots prove, not by desired positioning. | 10 cold readers seeing capsule + top 5 tags only. | "What genre and mode is this?" | Pass if "single-player underwater/sci-fi survival with bases/exploration" is dominant. | Remove `Base Building`, `Horror`, `Physics`, or `Open World` if screenshots do not prove them. |
| AB-008 | Paid micro-test gate | Best capsule + best short description + Steam URL | Paid traffic is allowed only after organic/cold-read proof and Steam baseline exist. | Small paid audience, 25-150 USD maximum. | Steam page visit/wishlist. | Minimum 1000 impressions, 50 clicks, and useful Steam UTM signal. | Stop within 48h if tracked visits produce no useful actions or comments show premise confusion. |

### Budget Use Rule

Spend order for the first several thousand USD:

1. `0 USD` desk/cold-reader/organic tests until three screenshots and one capsule rough pass.
2. `25-150 USD` only for one paid micro-test after Steam page baseline exists.
3. `300-800 USD` capsule/key-art polish only if `PLAN-CAPSULE-001` cold-read winner is clear.
4. `300-1000 USD` trailer edit/audio polish only after `PLAN-CLIP-*` has a proven first 3 seconds.
5. `250-1500 USD` creator paid slot only after organic creator replies show fit and the demo/preview build is stable.

Do not buy attention to compensate for unclear assets.

## Test Protocol

For each test:

1. Create a test brief with hypothesis, assets, audience, and CTA.
2. Assign one owner.
3. Use unique UTM where the CTA points to Steam.
4. Do not change two variables at once.
5. Run for a fixed window.
6. Record raw numbers and qualitative comments.
7. Decide: keep, revise, kill, or retest.

## Metrics That Count

Strong metrics:

- Steam tracked page visits;
- Steam wishlist conversions;
- creator reply quality;
- useful comments naming game systems;
- watch-through on short clips;
- Discord opt-ins after real proof;
- press replies asking for preview build.

Weak metrics:

- likes without comments;
- views without watch-through;
- "looks cool";
- follower count growth without Steam action;
- raw Reddit upvotes from general communities;
- creator "maybe later" replies.

## Minimum Sample Rules

Do not overfit tiny numbers.

| Channel | Minimum useful sample |
|---|---:|
| Internal agent cold read | 10 independent reads |
| Human cold read | 15 people |
| Creator micro-pitch | 20 verified contacts |
| Reddit critique post | 10 useful comments |
| Steam UTM | 100 visits or 10 tracked actions |
| Paid micro-test | 1000 impressions and 50 clicks minimum |

If the sample is smaller, treat the result as a signal only.

## Copy Variants To Test

### Short Description A - Direct Genre

Single-player deep-sea survival about pressure, salvage, machinery, and a buried Seed Ship anomaly.

### Short Description B - Atmosphere

Build, repair, and survive below the light in a NASA-punk ocean where pressure is the first enemy.

### Short Description C - Systems

An industrial underwater survival game about bases, salvage routes, heavy machines, and systems that fail under pressure.

### Short Description D - Mystery

Descend into a black-water survival zone where a buried Seed Ship bends instruments, routes, and life itself.

## Thumbnail Text Variants

Use 1-4 words, never paragraph text.

- BELOW THE LIGHT
- PRESSURE WARNING
- SALVAGE BELOW
- THE SEED SHIP
- BASE BREACH
- BLACK WATER
- HULL STRESS
- NO SIGNAL

## Stop Rules

Stop an asset line if:

- viewers cannot tell it is a game screenshot;
- comments compare it to AI concept art;
- people ask whether it has co-op because the copy implied it;
- people ask "what do you do?" after seeing all assets;
- the asset attracts mostly horror-only players while the demo is survival/crafting;
- Steam visits do not produce wishlists after enough tracked traffic;
- creator replies ask for features the build does not have.

## Experiment Log Template

```text
Experiment ID:
Date:
Owner:
Asset:
Audience:
Hypothesis:
CTA:
UTM:
Sample size:
Results:
Useful comments:
Confusion comments:
Decision:
Next action:
```

## Decision Rule

The winning hook is the one that makes players describe the game correctly in their own words. Not the prettiest image. Not the most liked post.
