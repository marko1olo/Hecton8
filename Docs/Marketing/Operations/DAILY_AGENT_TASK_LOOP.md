# HECTON-8 Daily Agent Marketing Task Loop

Status: executable agent workflow
Owner lane: SHINOBU_81 / agent operations
Runtime impact: none

## Purpose

Agents are useful only if they turn public noise into verified rows, better copy, better asset decisions, and fewer bad guesses. This loop prevents "research theatre".

## Daily Roles

| Role | Output |
|---|---|
| Lead Verifier | 25 raw leads checked and classified. |
| Pitch Writer | 10 personalized opener drafts. |
| Community Scout | 5 communities checked for rules/fit. |
| Sentiment Miner | 20 relevant comments classified. |
| Asset Critic | 10 screenshots/clips scored. |
| Source Auditor | 5 source claims rechecked. |
| KPI Clerk | Dashboard rows updated. |

One agent can hold multiple roles, but one output must exist per role before claiming the day was useful.

## Start-Of-Day Checklist

1. Read `Docs/Marketing/README.md`.
2. Read `Docs/Marketing/NO_COOP_PUBLIC_POSITIONING.md`.
3. Read current task-specific doc.
4. Check source ledger for stale platform claims.
5. Pick one measurable output.
6. Write output path before starting.

## 2026-05-19 Current Cut

The staged CRM-100 queue has 0 raw rows. Until the first real screenshot/clip packet exists, do not default to more lead verification. The bottleneck is asset proof, not lead volume.

Current default lane order:

1. `ASSET_GATE` if any capture exists or can be prepared; this includes `creator_rows_unlocked`, `creator_utility_score`, and `creator_send_gate` when the asset could touch creators.
2. `COPY_TEST` only if tied to one planned asset ID.
3. `SOURCE_RECHECK` only for platform/source facts that block a concrete gate.
4. `RISK_CLOSE` only if the risk register has no prevention/response.
5. `CRM_CLEANUP` only if Wave A needs exact official contact recheck after matching assets exist and send-log fields can be filled from proof.

Do not expand raw leads unless a human explicitly asks for another source-backed lead sprint.

## 2026-05-19 Active Control Tower Loop V0

This loop prevents agent labor from becoming more documents. Use it until the first real screenshot pack exists.

### Morning Cut

Pick exactly one lane for the day:

| Lane | When to pick | Required output |
|---|---|---|
| CRM_CLEANUP | Creator/press rows are stale or raw, or Wave A has proof to log. | Updated CSV rows with status, route, risk, next action, and send-log fields if any send is being prepared. |
| ASSET_GATE | Screenshots/clips exist or are about to exist. | QA scores, reject codes, asset metadata updates, and creator utility/send gate fields when relevant. |
| COPY_TEST | Asset/copy mismatch blocks public use. | 3-5 variants tied to one asset ID and one metric. |
| SOURCE_RECHECK | Platform rules, routes, or deadlines can change. | Source ledger addendum and affected doc correction. |
| RISK_CLOSE | A risk has no prevention/response owner. | Risk register update plus one backlog action. |
| CAMPAIGN_DECISION | Campaign 01/02/03 is being prepared. | `KEEP`, `REVISE`, or `KILL` decision fields filled. |

If a proposed task cannot produce one of these outputs, reject it.

### Evidence Gate

Each output must label evidence:

| Evidence | Allowed claim |
|---|---|
| INTERNAL_DOC | Project intent only. |
| THIRD_PARTY_INDEX | Prospecting seed only. |
| PUBLIC_CREATOR_PAGE | Fit/activity hint only. |
| OFFICIAL_PLATFORM_DOC | Platform rule as of check date. |
| ASSET_METADATA | Capture status and recorded asset-side gates only; quality still requires QA evidence. |
| HUMAN_COLD_READ | Clarity signal only. |
| STEAM_ANALYTICS | Funnel signal only after page exists. |

Do not write "verified" unless the route, date, source, and allowed claim are explicit.

### Noon Kill Check

At the halfway point, stop and answer:

```text
Does this change update a row, asset, risk, campaign decision, or source gate?
If no, stop.
Is this creating a new file?
If yes, stop unless the backlog explicitly names a missing file.
Does it imply co-op/performance/large-world proof?
If yes, rewrite.
Does creator-facing work lack `creator_utility_score`, `creator_send_gate`, named CRM row, or `asset_ids_sent`?
If yes, hold the send path.
```

### End Cut

Every day ends with one of:

| Decision | Meaning |
|---|---|
| ADVANCE | The next public/asset/CRM gate can move one step. |
| HOLD | More proof is required; name the proof. |
| KILL | A route, asset, copy angle, or spend path is no longer worth work. |

No decision means the day produced process noise.

## Lead Verification Loop

Input:

- `Data/UNIQUE_CREATOR_VERIFICATION_QUEUE_2026-05-18.csv`
- `Data/PRIORITY_CREATOR_SHORTLIST_FROM_RAW_2026-05-18.csv`
- `AgentOps/VerificationBatches_2026-05-19/VERIFY_BATCH_*.md`

Steps:

1. Open public creator page.
2. Confirm channel still exists.
3. Confirm recent upload/stream activity.
4. Confirm game fit.
5. Confirm language.
6. Find public contact route only from creator-owned source.
7. Check sponsorship/coverage policy if visible.
8. Assign segment and pitch angle.
9. Mark risk.
10. Promote, hold, or reject.

Forbidden:

- guessing email;
- using leaked contact lists;
- scraping private Discords;
- sending a pitch while verifying;
- inventing subscriber counts;
- treating index data as current truth.

## Pitch Writing Loop

Each pitch must include:

- creator-specific opener;
- audience fit reason;
- HECTON-8 hook matched to their content;
- asset/build status;
- honest boundary;
- CTA.

Bad opener:

> I love your channel and think your audience would like our game.

Acceptable opener:

> Your survival runs tend to focus on systems breaking under pressure rather than pure reaction scares, so HECTON-8's base-failure and salvage loop is the fit I would test first.

Never mention:

- "Subnautica killer";
- co-op;
- guaranteed FPS;
- massive world size;
- paid placement without disclosure.

## Community Scout Loop

For each community:

1. Record name and URL.
2. Read rules same day.
3. Mark self-promo allowed/limited/banned.
4. Mark media formats allowed.
5. Mark required flair.
6. Mark account age/karma gates if visible.
7. Add recommended post type.
8. Add "do not post" notes.

Output goes to:

- `Community/REDDIT_COMMUNITY_RULES_TRACKER.md`

## Sentiment Mining Loop

Track current player language around:

- Subnautica 2 co-op expectations;
- performance/stutter complaints;
- Early Access trust;
- base-building friction;
- vehicle fantasy;
- underwater horror fatigue;
- resource grind;
- inventory pain;
- "too clean" visual criticism;
- demand for large mobile bases.

Each signal must be classified:

| Class | Meaning |
|---|---|
| Confirmed recurring | Multiple independent signals. |
| Directional | Repeated but weak or context-dependent. |
| Anecdotal | Single post/comment. |
| Marketing echo | Copy repeated from trailer/store page. |
| Reject | Unsourced or unusable. |

## Asset Critic Loop

Use `QA/MARKETING_ASSET_QA_CHECKLIST.md`.

For each asset:

1. Score 0-12.
2. Write one-sentence diagnosis.
3. Identify the missing hook.
4. If the asset could touch creators, score creator utility 0-4 and name the CRM rows it unlocks.
5. Decide publish/revise/kill.
6. Store result in asset QA table and asset metadata, including `creator_send_gate`.

Agents do not "like" assets. Agents classify whether a cold viewer understands and cares.

## Source Auditor Loop

Recheck:

- Steam tags;
- Steam UTM;
- Steam Next Fest;
- Steam asset specs;
- FTC endorsement guidance;
- YouTube/TikTok disclosure rules;
- subreddit rules before posting;
- public creator page before contact.

If a source changed, update:

- `Data/SOURCE_LEDGER.md`;
- affected operational doc;
- status/rationale if the change changes strategy.

## End-Of-Day Report Template

```text
Date:
Agent:
Role(s):
Files changed:
Leads verified:
Pitches drafted:
Communities checked:
Assets scored:
Source claims rechecked:
Signals found:
Blocked items:
Next recommended action:
```

## Quality Bar

An agent day is rejected if it produces:

- a generic strategy paragraph;
- unverified creator names only;
- fake contacts;
- a copied pitch with no personalization;
- a post template that violates platform rules;
- a claim that cannot be traced to a source or project proof.

## Minimum Daily Quota

If no screenshots exist and the CRM has raw rows:

- 25 lead verifications;
- 10 opener drafts;
- 5 community rule checks;
- 20 sentiment classifications;
- 1 source ledger update if a platform source was used.

If no screenshots exist and the CRM-100 staged queue has 0 raw rows:

- 1 planned asset packet or QA gate improvement;
- 1 copy lint or asset-linked copy test;
- 1 source/risk/backlog correction only if it changes execution;
- 0 new generic docs;
- 0 broad creator sends;
- 0 paid actions.

If screenshots exist:

- 10 asset scores;
- 5 copy variants;
- 10 lead verifications;
- creator utility and `creator_send_gate` fields for any asset considered for outreach;
- 1 A/B test brief;
- 1 public-post candidate.

If Steam page exists:

- daily UTM/source table update;
- 5 creator follow-ups;
- 1 store-copy or screenshot improvement proposal;
- 1 feedback digest.
