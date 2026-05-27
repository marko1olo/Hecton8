# Player Feedback Taxonomy And Triage

Status: feedback operating model / pre-public
Public stance: single-player-first scope / proof-first public copy
Runtime impact: none

## Objective

Convert comments, creator coverage, demo notes, Discord posts, and Steam forum threads into actionable product and marketing decisions.

## Feedback Classes

| Class | Meaning | Owner |
|---|---|---|
| `VISUAL_READ` | Screenshot/capsule/clip clarity. | Tech art / marketing |
| `PLAYER_VERB` | Viewer cannot tell what player does. | Design / marketing |
| `AGENCY_DECISION_READ` | Viewer/player cannot name the pressure decision, tradeoff, or consequence. | Design / marketing |
| `DIFFERENTIATION` | Looks derivative or generic. | Art direction / positioning |
| `SURVIVAL_LOOP` | Resource, pressure, base, route pacing. | Design |
| `UX_READABILITY` | UI, warnings, objectives, controls. | UX |
| `PERFORMANCE` | FPS, stutter, hardware, load, crash. | Engineering / QA |
| `AUDIO_ATMOSPHERE` | Soundscape, sonar, machinery, dread. | Audio |
| `NARRATIVE_HOOK` | Seed Ship, lore, motivation, mystery. | Narrative |
| `SCOPE_CONFUSION` | Co-op, multiplayer, roadmap, Early Access. | Marketing / product |
| `COMMUNITY_SAFETY` | Abuse, spam, scams, moderation. | Community |
| `IMAGEBOARD_SIGNAL` | Anonymous 4chan/Dvach feedback; anecdotal by default. | Community / marketing |
| `AI_SLOP_RISK` | Asset/copy/process reads as generated, lazy, or agent-made instead of authored/proven. | Marketing / art direction |
| `ENGINE_TRUST` | Unity/UE/Godot/toolchain argument hides asset critique or weakens trust. | Marketing / engineering comms |
| `CRAFT_GRIND_FATIGUE` | Survival/crafting loop reads like resource treadmill instead of route pressure. | Design / marketing |

## Severity

| Severity | Meaning |
|---|---|
| P0 | Blocks public demo/page trust. Fix or pause campaign. |
| P1 | Repeated issue that damages conversion. Fix before next push. |
| P2 | Useful improvement, not blocking. Backlog. |
| P3 | Taste/opinion/noise. Track only if repeated. |

## Triage Table

| Date | Source | Consent/provenance | Route class | Feedback quote/summary | Class | Severity | Product issue | Marketing issue | Owner | Action |
|---|---|---|---|---|---|---|---|---|---|---|

## Feedback Provenance Gate V0

Every feedback row must state where the feedback came from and whether it was invited, public, support, creator, press, playtest, or private. Do not merge identities or contact details across routes.

| Feedback source | Allowed use | Blocker |
|---|---|---|
| Public comments/reviews/forums | Aggregate product/marketing signal. | Adding commenters to CRM/newsletter/tester lists. |
| Playtest/screening form | Product research for the agreed test route. | Reusing contact for marketing email without separate opt-in. |
| Creator/press reply | Outreach and asset-fit learning. | Treating as newsletter or playtest consent. |
| Support/bug report | Repro, support, known-issues update. | Using support email for marketing or creator outreach. |
| Discord/community post | Community signal under server rules. | Moving private server content into public copy without permission. |
| 4chan/Dvach/imageboard thread | Anonymous public critique and language mining only. | Treating anonymous users as contacts, consent, creator leads, playtesters, or market percentages. |

## Repeat Pattern Rule

One comment is not a mandate. Three independent comments on the same issue are a signal. Ten are a problem. If creators and cold viewers independently say the same thing, treat it as a product/positioning issue, not "bad audience fit."

## Common Feedback Translation

| Raw comment | Likely meaning | Action |
|---|---|---|
| "Looks like Subnautica" | Color, diver silhouette, reef, UI, or creature framing is too familiar. | Identify visual cue and revise lead asset. |
| "Too dark" | Readability failed, not atmosphere succeeded. | Increase silhouette, contrast, navigation anchors. |
| "What do you do?" | Player verb absent. | Show tool, base, salvage, repair, route, or threat decision. |
| "What choice do I have?" / "Looks like a mood demo" | Agency decision absent; viewer sees atmosphere but not a tradeoff. | Use `AGENCY_DECISION_READ`; hold first-public or demo expansion until one repair/retreat/reroute/scan/operate/recover decision is visible. |
| "Looks like a walking sim" | Systems are not visible. | Show mechanics and consequences. |
| "I want co-op" | Audience expectation from genre/SN2. | Restate single-player-first, do not roadmap. |
| "Will it run well?" | Trust/performance concern. | Do not answer with claims; promise measured proof later. |
| "Base looks clean" | NASA-punk grime/material direction weak. | Add wear, labels, seals, utility, salt, damage. |
| "Monster game?" | Threat framing overpowered survival systems. | Rebalance copy toward pressure/machinery/salvage. |
| "AI slop" / "нейромусор" | Asset looks generated, concept-only, over-smoothed, incoherent, or process pitch overpowered build proof. | Mark `AI_SLOP_RISK`; audit source/capture labels; lead next beat with gameplay proof only. |
| "Asset flip" / "Unity trash" | Toolchain/store-asset suspicion or engine-war reflex. | Mark `ENGINE_TRUST`; remove engine as player-facing hook; prove custom material language and gameplay decision. |
| "Shill / ad / реклама" | Post format or placement violated community expectation. | Mark `COMMUNITY_SAFETY`; stop replying, revise route/template, do not repost same asset that day. |
| "Crafting trash to craft trash" | Survival loop reads as generic grind. | Mark `CRAFT_GRIND_FATIGUE`; require salvage route decision, pressure cost, or recovery loop proof before public copy. |
| "Procedural = empty" | Generated scale is being interpreted as low authorship. | Remove procedural-scale hook; show authored route density, landmarks, danger, and consequence. |
| "No readable choice" | Viewer sees mood/threat but cannot name decision. | Mark `AGENCY_DECISION_READ`; hold first-public expansion until repair/retreat/reroute/scan/operate/recover decision is visible. |

## Imageboard Feedback Triage

Default classification for 4chan/Dvach signal is `Anecdotal`. Upgrade to `Directional` only when the same issue appears across independent threads or platforms. Upgrade to `Recurring` only when imageboard signal matches Reddit, Steam review/forum, creator, cold-reader, or playtest feedback.

### What To Keep

- exact board/thread/date;
- asset shown;
- question asked;
- repeated product-relevant wording;
- clone-risk cue;
- AI-slop cue;
- engine/tool trust cue;
- whether a player decision was named;
- one actionable asset/product/copy change.

### What To Drop

- insults with no asset reference;
- identity/politics fights;
- slurs and unsafe content;
- personal data or handles;
- requests for keys/access/private routes;
- unsupported claims about market size or AI adoption.

### Imageboard Triage Table

| Date | Surface | Thread | Asset | Prompt | Signal | Class | Severity | Decision read | Action |
|---|---|---|---|---|---|---|---|---|---|

### Signal Upgrade Rules

| Starting signal | Upgrade only if | Action |
|---|---|---|
| One hostile imageboard comment | Never by itself. | Track as `P3` or reject. |
| Three independent imageboard comments in one thread | They identify the same asset-specific cue. | Treat as `P2/P1` depending on asset importance. |
| Same cue appears on imageboard + Reddit/cold-read | Independent source confirms. | Treat as `P1`; revise before next public beat. |
| Same cue appears in creator/press/demo feedback | High-trust source confirms. | Treat as `P0/P1`; hold affected route. |

### Imageboard Severity Overrides

- `P0`: a public false claim, private access leak, key/security issue, or official CTA leak occurred.
- `P1`: repeated clone/AI/readability issue affects first screenshot pack, Steam page, creator pitch, or demo route.
- `P2`: useful critique affects an asset not yet scheduled.
- `P3`: taste, insults, engine-war, or one-off noise.

## Creator Feedback Intake

After a creator plays or reviews:

- record video/stream link;
- timestamp issues;
- separate joke/entertainment from real issue;
- capture repeated chat questions;
- record whether Steam traffic changed;
- send thanks without arguing;
- do not request changed opinion.

## Demo Survey Questions

1. What did you think the main objective was?
2. What system did you understand first?
3. What confused you?
4. Did pressure/oxygen/power feel fair?
5. Did the base feel functional?
6. Did the world feel distinct from other underwater survival games?
7. What decision did you make under pressure, if any?
8. Did anything block you from continuing?
9. Would you wishlist after this demo?
10. What one screenshot or clip would you show someone else?

## 2026-05-26 Survey And Poll Plan V0

Surveys are research routes, not mailing-list capture. Keep feedback, tester recruitment, newsletter, creator, press, support, and private-access consent separate.

### Survey Gates

| Gate | Required state | Kill if |
|---|---|---|
| Route | `route_class` is known before collection. | Survey source is copied into CRM/newsletter without explicit opt-in. |
| Consent | `consent_provenance` is stated in the form/log. | Form asks for email, Discord, key interest, or newsletter in the same required path as critique. |
| Asset | Exact `asset_id` or build ID is named. | Respondents judge vague concept, lore, or future promise. |
| Blindness | Prompt avoids target nouns before first read. | Survey teaches the answer before asking. |
| Privacy | No personal data unless a separate owner-controlled form and deletion path exist. | Free-text asks for personal contact or account data. |
| Incentive | No reward unless route and disclosure are approved. | Reward/key chance turns feedback into promotion or access route. |

### Survey Packets

| Packet | When | Core question | Required fields | Pass / fail |
|---|---|---|---|---|
| `SURVEY_ASSET_5SEC` | First screenshots/capsules. | What is this game and what would you do? | `reader_id`, `asset_id`, `context_exposure`, `what_genre`, `what_do_you_do`, `what_decision_next`, `mode_assumption`, `proof_belief`. | Pass if genre and player verb clear; fail if clone, co-op, AI/concept, or mood-only dominates. |
| `SURVEY_CLIP_15SEC` | First clips. | What changed by second 3 and what should the player do? | `clip_id`, `view_time_seconds`, `first_event_read`, `decision_read`, `confusion_point`, `would_keep_watching`. | Pass if decision appears before caption/audio rescue. |
| `SURVEY_PAGE_READ` | itch/Steam/page draft. | What feature/mode/platform do you think exists right now? | `surface`, `page_url_or_mock`, `feature_assumption`, `mode_assumption`, `platform_assumption`, `misleading_claim`. | Fail if page implies unsupported mode/platform/performance. |
| `SURVEY_SURFACE_FIT` | DTF/Habr/Reddit/itch/wiki route prep. | Is this useful for this platform, or does it read as ad spam? | `surface`, `route_class`, `shill_read`, `asset_specific_answer`, `rule_risk`, `stop_condition`. | Pass only if readers name concrete value beyond promotion. |
| `SURVEY_DEMO_EXIT` | Demo/playtest. | What system did you understand first and what blocked you? | `build`, `route`, `consent`, `objective`, `first_understood_system`, `blocker`, `pressure_fairness`, `base_function`, `distinctiveness`, `wishlist_intent`. | Product triage only until public CTA/demo gates pass. |

### Minimal RU Poll For DTF / Dvach / RU Readers

```text
1. Что это за игра по первому кадру/гифке?
2. Что игрок должен сделать дальше?
3. Что здесь отличается от обычного underwater survival?
4. Кадр выглядит как gameplay, concept/AI, или непонятно?
5. Есть ли ложное ожидание коопа/мультиплеера?
6. Что первым ломает доверие: темнота, чистая sci-fi база, клоновость, отсутствие действия, UI, другое?
```

### Minimal EN Poll

```text
1. What genre do you think this is?
2. What would the player do next?
3. What looks distinct?
4. Does this look like gameplay, concept/AI, or unclear?
5. Does it imply co-op or multiplayer?
6. What breaks trust first: darkness, clean sci-fi, clone read, no action, UI, other?
```

Survey result rule: do not report percentages below 10 valid blind readers. Below that, report counts and exact nouns only.

## Weekly Feedback Digest

```md
## Feedback Digest - YYYY-MM-DD

- Sources reviewed:
- Consent/provenance gaps:
- Top repeated positive:
- Top repeated negative:
- P0 issues:
- P1 issues:
- Agency decision read gaps:
- Marketing copy issue:
- Asset issue:
- Product issue:
- Recommended next test:
```
