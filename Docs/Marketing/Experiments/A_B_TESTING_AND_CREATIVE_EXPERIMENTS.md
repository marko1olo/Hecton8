# HECTON-8 A/B Testing And Creative Experiments

## Authority Boundary

Static experiment plan only. Hypotheses, matrices, copy variants, sample thresholds, and stop rules do not prove quality, release, platform, Steam, wishlist, demo, performance, feedback, monitoring, operations, launch, or public-response readiness.
Public voice routes to root `textes.md`. Quality, release, platform, Steam, wishlist, demo, performance, feedback, review/forum response, legal/compliance, localization, monitoring, operations, partnership, contract, and launch claims route through root `quality.md`, `release.md`, and `platform.md` with current proof artifacts plus local permission gates where present.
No launch/release readiness, localized public use, public response, creator contract send, platform claim, Steam approval, wishlist claim, demo approval, paid-spend approval, or public send approval exists from experiment rows or static creative plans.

Status: pre-asset experiment plan
Owner lane: Marketing / marketing measurement
Runtime impact: none

## Purpose

This document turns the marketing plan into controlled experiments. The project has a small cash budget, so guessing is too expensive. Agent labor is cheap; paid reach is not.

No paid spend is allowed until an experiment has:

- one measurable hypothesis;
- one asset family;
- one audience segment;
- one CTA only after the exact destination permission gate and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` pass, or a no-link feedback/private access route if it has no public destination;
- one decision threshold;
- one stop rule.

## Experiment Ladder

| Stage | Cost | What is tested | Output |
|---|---:|---|---|
| Desk test | 0 USD | Screenshot/copy clarity with internal agents. | Kill weak assets before public use. |
| Cold-reader test | 0-50 USD | Does a stranger understand the game in 5 seconds? | Capsule/screenshot winner. |
| Organic micro-post | 0 USD | Do players comment with the intended nouns? | Hook clarity. |
| Creator micro-pitch | 0 USD | Do send-verified creators reply after official route and asset-fit gates pass? | Segment fit. |
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
| AB-001 | Cold-reader | `PLAN-SHOT-000` vs `PLAN-SHOT-003` | Identity hero beats salvage only if the viewer can name genre and player fantasy in 5 seconds. | 15 cold readers, no project context. | "What is this game?" | 70% correct genre read and fewer than 2 "Subnautica clone only" responses. | Kill the weaker first screenshot if it is pretty but actionless. |
| AB-002 | Cold-reader | `PLAN-CAPSULE-001` rough A/B/C | Small-size capsule readability matters more than cinematic detail. | 15 Steam-adjacent players or agents. | "Which would you click on Steam?" | Winner is +20% preference and title remains readable at tiny size. | Do not commission/polish if no variant beats plain logo + hero silhouette. |
| AB-003 | Organic micro-post | `PLAN-SHOT-005` vs `PLAN-SHOT-004` | Base risk will outperform heavy machinery only if viewers understand consequence, not just mood. | X/Bluesky/Reddit critique-safe surface after handle custody exists. | Comment question; external CTA waits for the exact destination gate plus `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`. | Useful comments naming system/verb per 100 views. | Kill if comments are mostly "looks cool" or "what do you do?" |
| AB-004 | Copy desk test | Short description A vs B vs C | Direct pressure/salvage copy should beat NASA-punk wording until visuals teach the term. | 10 internal reads + 15 humans after first screenshot exists. | Pick what the game is. | Lowest confusion and lowest unsupported multiplayer-scope assumption wins. | Remove any copy that implies multiplayer, simulation scope, or mood-only game. |
| AB-005 | Creator micro-pitch | `PLAN-CLIP-003` vs `PLAN-CLIP-004` | Survival/systems creators reply more to a complete salvage failure than a heavy-machine beauty shot. | 20 verified creator contacts from CRM after asset/contact gates pass. | "Do you want a private preview when ready?" | Reply quality, not raw reply count. | Stop batch if 3+ creators ask for missing features or asset proof. |
| AB-006 | Trailer opening | `PLAN-CLIP-001` pressure leak vs `PLAN-CLIP-002` sonar contact | A system problem in the first 3 seconds should beat a monster/contact reveal if HECTON-8 is selling machinery survival. | 15 cold viewers + 5 creator/editor opinions. | "Would you keep watching?" | Keep-watching count and correct game-description nouns. | Recut if viewers say "generic underwater horror" or cannot name player action. |
| AB-007 | Steam tag proof | First six passing `PLAN-SHOT-*` | Top tag stack should be decided by what screenshots prove, not by desired positioning. | 10 cold readers seeing capsule + top 5 tags only. | "What genre and mode is this?" | Pass if "single-player underwater/sci-fi survival with bases/exploration" is dominant. | Remove `Base Building`, `Horror`, `Physics`, or `Open World` if screenshots do not prove them. |
| AB-008 | Paid micro-test gate | Best capsule + best short description + Steam URL only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` | Paid traffic is allowed only after organic/cold-read proof, Steam baseline, `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`, Steam page gate, and public CTA gate exist. | Small paid audience, 25-150 USD maximum. | Steam page visit/wishlist after Steam page gate, spend gate, and public CTA gate. | Minimum 1000 impressions, 50 clicks, and useful Steam UTM signal. | Stop within 48h if tracked visits produce no useful actions or comments show premise confusion. |
| AB-009 | Cold-reader agency proof | `PLAN-SHOT-006` vs `PLAN-CLIP-001` vs `PLAN-CLIP-003` | Agency proof only counts if a cold viewer can name the pressure decision without prompt. | 15 cold readers, no project context. | "What decision would the player make next?" | 60%+ name repair, retreat, reroute, scan, operate, abort, or recover. | Hold first-public and creator gameplay sends if viewers name only mood, threat, or scenery. |

### AB-010 - Imageboard Prompt Safety Test

Status: pre-post / no public route / no CTA.

Hypothesis: a 4chan/Dvach post is safer when the prompt asks for one visible failure in a real asset instead of asking for support, taste, lore interest, wishlists, or broad "thoughts".

Asset gate: one candidate from `PLAN-SHOT-003`, `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` with matching QA metadata. `PLAN-SHOT-001` and `PLAN-SHOT-007` are allowed only as identity-risk tests, not agency proof. `PLAN-SHOT-008` is internal only.

Public-route gate: AB-010 does not authorize posting. It only chooses the least-bad prompt candidate for the existing imageboard permission lane in community, QA, campaign, KPI, and asset-library docs. The actual post still requires board rule check, asset route approval, `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`, no CTA, no fake discovery, and a recorded stop condition.

Prompt variants:

| Variant | Frame | Use | Kill if |
|---|---|---|---|
| A - Decision Read | "Would you know whether to repair, retreat, reroute, scan, operate, or abort from this frame?" | Agency candidates. | Readers answer with mood, lore, or support instead of a visible decision. |
| B - Identity / Clone Risk | "Does this read as industrial deep-sea survival or generic underwater sci-fi?" | Identity stills and capsule roughs. | It invites franchise argument instead of naming visual cues. |
| C - Technical / Fake-First | "Where does the visual fake break: instrument, lighting, silhouette, machine state, route cue, or interaction?" | Technical critique surfaces and internal agent review. | It becomes Unity/Godot/AI/process debate. |
| D - Shill-Smell Rewrite | "Rewrite this so it reads like a critique request, not an ad." | Copy preflight only. | The safest rewrite still needs a link, CTA, hype line, or fake-player voice. |

Required AB-010 response fields:

| Field | Type | Use |
|---|---|---|
| `prompt_variant` | A / B / C / D | Keeps copy result tied to exact wording. |
| `asset_id` | `PLAN-*` | Prevents prompt testing without a real asset. |
| `surface` | 4chan / Dvach / internal / unknown | Segment risk by culture and language. |
| `shill_read` | yes / no / unclear | If yes, rewrite or kill. |
| `likely_derail` | AI / engine / competitor / politics / access-bait / none | Predicts thread failure mode. |
| `decision_read` | yes / no / unclear | Only yes if a visible player choice is named. |
| `asset_specific_answer` | yes / no | Useful imageboard critique must name something in the asset. |
| `needs_context` | yes / no | If yes, the asset is too weak for anonymous boards. |
| `rewrite_needed` | yes / no | Holds copy until the route owner rewrites. |
| `stop_condition` | free text | Exact condition for leaving the thread. |

AB-010 advance threshold:

- 8 internal/agent reads minimum before any public route request;
- 6/8 say `shill_read = no`;
- 6/8 give asset-specific answers;
- no more than 2/8 predict AI or engine derail;
- one concrete stop condition is written before route approval.

AB-010 stop rules:

- Kill the prompt if it asks "do you like this?", "would you buy/wishlist?", "does this look cool?", or "what should I add?".
- Kill the prompt if it needs project lore to explain the asset.
- Kill the prompt if it names Subnautica, Unity, AI agents, sales, wishlists, Discord, playtest, keys, or Steam in the opening post.
- Kill the prompt if it cannot survive as a no-link text plus one direct media asset.
- Kill the prompt if the most likely answer is about the developer instead of the screenshot/clip.

## 2026-05-19 Cold-Read Score Sheet V0

Use this for AB-001, AB-002, AB-004, AB-006, AB-007, and AB-009. Do not explain HECTON-8 before the viewer answers. Show the asset for five seconds, hide it, then ask.

### Blindness Rules

The first pass must be blind. A valid cold reader has not seen the current marketing docs, target nouns, pitch language, or intended answer for the asset.

- Do not say pressure, machinery, salvage, black water, Seed Ship, single-player, Subnautica, or deep-sea noir before the first answer.
- Do not show the title, caption, post copy, Steam tags, or logo unless that is the exact thing being tested.
- If the reader already knows the target, mark the response `CONTEXT_EXPOSED` and keep it as qualitative feedback only.
- If a response mostly repeats words from the prompt, mark it `PROMPT_ECHO` and exclude it from pass percentages.
- Internal agent reads are useful for catching obvious failures, but human/player cold reads decide public readiness.

### Response Fields

| Field | Type | Use |
|---|---|---|
| `reader_id` | anonymous id | Do not collect private personal data. |
| `reader_type` | internal / player / creator / press / unknown | Segment the response; do not mix as one truth. |
| `context_exposure` | `NONE` / `CONTEXT_EXPOSED` / `PROMPT_ECHO` / `UNKNOWN` | Only `NONE` counts toward pass percentages. |
| `asset_id` | `PLAN-*` | Match metadata and dashboard. |
| `view_time_seconds` | number | Default 5 for screenshots/capsule, 10-20 for clips. |
| `what_genre` | free text | Checks genre clarity. |
| `what_do_you_do` | free text | Checks player verb. |
| `what_decision_next` | free text | Checks agency: repair, retreat, reroute, scan, operate, abort, recover, or equivalent. |
| `agency_decision_read` | yes / no / unclear | Yes only when the reader can name a decision without prompt or caption. |
| `what_is_different` | free text | Checks HECTON identity. |
| `mode_assumption` | single-player / co-op / multiplayer / unknown | Flags unsupported multiplayer-scope expectation. |
| `proof_belief` | gameplay / concept / AI-looking / unsure | Flags asset trust. |
| `readability_issue` | none / too dark / too busy / UI unclear / unknown | Routes scene fixes. |
| `imageboard_prompt_risk` | none / shill / AI-derail / engine-war / competitor-war / access-bait / unknown | Required for AB-010 and optional for public-post candidates. |
| `would_answer_asset_question` | yes / no / unclear | Checks whether the prompt invites concrete critique rather than broad opinion. |
| `click_interest` | 0-4 | 0 no interest, 4 would click/wishlist if Steam exists. |
| `kill_reason` | free text | One reason they would ignore it. |
| `verbatim_nouns` | comma text | Words they used: pressure, machinery, salvage, base, black water, Seed Ship, etc. |

### Scoring

| Score | Pass condition |
|---|---|
| Blind validity | Count only if `context_exposure` is `NONE`. |
| Genre clarity | Count if `what_genre` says underwater survival, survival, exploration survival, base/survival, or close equivalent. |
| Player verb | Count if `what_do_you_do` names salvage, repair, build, survive, descend, explore, manage pressure/power/oxygen, pilot, or route choice. |
| Agency decision read | Count if `what_decision_next` names a concrete pressure decision such as repair, retreat, reroute, scan, operate, abort, recover, or a close equivalent without prompting. |
| Identity read | Count if `what_is_different` names pressure, machinery, industrial/noir, salvage route risk, black water, Seed Ship, or base-as-machine. |
| Trust | Fail if `proof_belief` is concept or AI-looking for a gameplay asset. |
| Mode safety | Fail if 2+ readers assume multiplayer/co-op from a single-player asset/copy. |

### Decision Thresholds

| Test | Advance | Revise | Kill |
|---|---|---|---|
| AB-001 screenshot lead | 70% genre clarity, 50%+ player verb, fewer than 2 clone-only reads. | Genre clear but verb/identity weak. | Concept/AI suspicion or clone-only read dominates. |
| AB-002 capsule | Winner +20% click interest and title readable. | No winner but one variant has fixable readability. | No variant beats plain logo/hero silhouette. |
| AB-004 copy | Lowest confusion and zero unsupported multiplayer-scope implication. | Strong hook but unclear player verb. | Any variant implies multiplayer/co-op, massive scope, or performance proof. |
| AB-006 clip opening | Majority would keep watching and can name action by second 3. | Hook visible after second 3 but late. | Generic underwater horror or no player action. |
| AB-007 tags | Readers infer single-player underwater survival/exploration/base only from tags+assets. | One tag creates confusion. | Top tags imply missing feature or mode. |
| AB-009 agency proof | 60%+ valid blind readers name a player decision and the strongest asset has no AI/concept suspicion. | Viewers see danger but not a clear next decision. | Viewers name only mood, monster, darkness, or scenery. |
| AB-010 imageboard prompt | 6/8 internal reads show no shill read, asset-specific answers, and no dominant AI/engine derail. | Prompt works but one line smells promotional or needs a narrower asset question. | Any link/CTA/access request, fake-player voice, or developer/process debate dominates. |

Record raw responses. Do not summarize away harsh wording; it is the useful part.

### Budget Use Rule

Spend order for the first several thousand USD:

1. `0 USD` desk/cold-reader/organic tests until three screenshots and one capsule rough pass.
2. `25-150 USD` only for one paid micro-test after Steam page baseline exists.
3. `300-800 USD` capsule/key-art polish only if `PLAN-CAPSULE-001` cold-read winner is clear.
4. `300-1000 USD` trailer edit/audio polish only after `PLAN-CLIP-*` has a proven first 3 seconds.
5. `250-1500 USD` creator paid slot only after organic creator replies show fit, the demo/preview build is stable, and the selected CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.

Do not buy attention to compensate for unclear assets.

## Test Protocol

For each test:

1. Create a test brief with hypothesis, assets, audience, and CTA state.
2. Assign one owner.
3. Use unique UTM only where the exact destination permission gate, `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, and platform rules all allow it.
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
| Creator micro-pitch | 20 contacts verified for send after gates |
| Reddit critique post | 10 useful comments |
| Steam UTM | 100 visits or 10 tracked actions |
| Paid micro-test | 1000 impressions and 50 clicks minimum |

If the sample is smaller, treat the result as a signal only.

Contaminated reads do not count toward the minimum useful sample. They can explain why an asset failed, but they cannot prove an asset works.

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
- people ask whether it has multiplayer/co-op because the copy implied it;
- people ask "what do you do?" after seeing all assets;
- people cannot name a decision after seeing candidate agency-proof assets;
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
