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
4. Decide publish/revise/kill.
5. Store result in asset QA table.

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

If no screenshots exist:

- 25 lead verifications;
- 10 opener drafts;
- 5 community rule checks;
- 20 sentiment classifications;
- 1 source ledger update if a platform source was used.

If screenshots exist:

- 10 asset scores;
- 5 copy variants;
- 10 lead verifications;
- 1 A/B test brief;
- 1 public-post candidate.

If Steam page exists:

- daily UTM/source table update;
- 5 creator follow-ups;
- 1 store-copy or screenshot improvement proposal;
- 1 feedback digest.
