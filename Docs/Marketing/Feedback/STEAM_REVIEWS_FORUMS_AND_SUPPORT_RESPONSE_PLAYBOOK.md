# HECTON-8 Steam Reviews, Forums, And Support Response Playbook

Status: pre-launch response policy
Owner lane: SHINOBU_81 / community support
Runtime impact: none

## Source Boundary

Current primary source checked on 2026-05-19:

- Steam User Reviews: https://partner.steamgames.com/doc/store/reviews?l=english&language=english
- Steam Events and Announcements Tools: https://partner.steamgames.com/doc/marketing/event_tools?l=english&language=english

Steam explicitly warns developers not to manipulate reviews, not to solicit reviews for rewards, and not to ask customers to review from inside the app. Recheck before launch.

## Core Rule

Reviews are not a battlefield. They are expectation data. A bad developer response can amplify a negative review harder than the review itself.

## Forbidden Review Actions

- do not ask players to change reviews;
- do not reward reviews;
- do not ask for reviews inside the game;
- do not argue with negative reviewers;
- do not use alt accounts;
- do not brigade helpful/unhelpful votes;
- do not imply the player is stupid;
- do not copy-paste robotic apology spam;
- do not promise dates in review replies.

## When To Respond

Respond only if:

- the review reports a fixable bug and asks for help;
- the review contains misinformation that can be corrected calmly;
- the review identifies a real issue and the fix is already live;
- the review is high-visibility and response can help future readers;
- the review is abusive and needs reporting, not argument.

Do not respond to every review.

## Response Tone

Use:

- brief;
- factual;
- accountable;
- no defensiveness;
- no sales pitch;
- no request to change review.

## Negative Review Templates

### Performance Complaint

```text
Thanks for reporting this. We are tracking performance issues by hardware/settings/build version. If you are willing to send specs and the area where the drops happen, use [support route]. We will not make a performance claim here without measured build context.
```

### Content Too Thin

```text
Fair criticism. The current build is focused on [current scope]. We are using feedback like this to decide whether the loop has enough pressure, salvage, and base consequence before expanding scope.
```

### Co-op Request

```text
HECTON-8 is single-player-first. Co-op is not part of the current public plan, so we do not want to sell the game on a feature that is not in the build.
```

### "Subnautica Clone"

```text
The underwater survival comparison is understandable. Our target is a different lane: pressure, industrial machinery, salvage, base risk, and deep-sea noir rather than bright ocean exploration.
```

### Bug Report In Review

```text
Thanks. This sounds like [issue class]. If you can send the save/build version or steps through [support route], it will help us reproduce it. I am logging the issue under [ticket/tag].
```

## Forum Categories

Steam discussions should have pinned threads:

- Known Issues;
- Bug Reports;
- Feedback;
- Performance Reports;
- Demo Feedback;
- FAQ;
- Patch Notes.

## 2026-05-19 Steam Forum Launch Moderation Gate V0

Create these pinned threads before any public demo, Playtest, or Early Access launch. Do not create them before a real Steam page exists.

| Thread | Purpose | Required first post | Update cadence | Owner |
|---|---|---|---|---|
| Known Issues | Prevent duplicate anger and false silence. | Build number, top issues, workaround, status terms. | After every patch or S1 issue. | QA/support |
| Bug Reports | Route repro into useful fields. | Bug report template and where logs live. | Daily during first week. | QA/support |
| Performance Reports | Separate settings/hardware problems from general rage. | Performance template and no-performance-claim boundary. | Daily during first week. | QA/support |
| Demo Feedback | Capture loop/readability/scope signal. | Six-question feedback prompt and no-coop boundary. | 48-hour digest. | product/marketing |
| FAQ | Stop repeated feature expectation drift. | Co-op, base systems, Steam/demo timing, performance proof rule. | Same day if confusion repeats. | community |
| Patch Notes | Keep fixes factual and dated. | Build ID, fixed/changed/known issues. | Every build. | lead/QA |

Pinned thread rules:

- one issue per user thread is preferred;
- merge duplicates only when a clear canonical issue exists;
- never hide a bug thread because it looks bad;
- no roadmap promises in forum replies;
- no "send us your Discord" as the only support route;
- no defensive Subnautica comparison arguments.

## Review And Forum Triage Buckets

Every Steam review/forum signal gets one bucket before response:

| Bucket | Meaning | Response |
|---|---|---|
| BUG_BLOCKER | Crash, save loss, progression block, launch failure. | Reply if helpful, collect repro, escalate S1. |
| PERF_CONTEXT | FPS/stutter/loading/VRAM/hardware issue. | Ask for template data; do not claim fix without build proof. |
| EXPECTATION_MISMATCH | Player expected co-op, brighter exploration, bigger content, or different genre. | Correct scope once; update store copy if repeated. |
| CONTENT_DEPTH | Too thin, too grindy, weak loop. | Acknowledge and route to product digest. |
| UX_FRICTION | Inventory, controls, UI readability, base building friction. | Route to UX backlog; ask repro/details if needed. |
| ABUSE_SPAM | Harassment, scams, review manipulation bait. | Report/moderate by platform rules; do not argue. |
| PRAISE_SIGNAL | Positive theme worth preserving. | Usually no reply; record in weekly digest. |

## First-Week Response Limits

| Window | Max official replies | Rule |
|---|---:|---|
| First 24 hours | 10 high-signal replies | Prioritize blockers, false-scope confusion, and performance templates. |
| Days 2-7 | 5 replies/day | Update pinned threads before individual replies. |
| After week 1 | only high-signal or fixed issues | Do not create a support treadmill in reviews. |

Do not respond to rage when the real fix is a pinned Known Issues update.

## Performance Report Template

```text
Build:
CPU:
GPU:
RAM:
Storage:
Resolution:
Graphics settings:
Area/scene:
FPS/frame-time symptom:
Reproduction steps:
Screenshot/video:
```

## Bug Report Template

```text
Build:
Save/new game:
What happened:
Expected:
Steps:
Frequency:
Screenshot/video:
Logs if available:
```

## Known Issues Policy

Known Issues thread must:

- list current top issues;
- include workaround if any;
- include status: investigating/fixed in next/currently not reproducible;
- avoid promising dates;
- update after patches.

## Review Digest

Every week after demo/launch:

```text
Week:
Review count:
Positive themes:
Negative themes:
Performance reports:
Bug clusters:
Store expectation mismatch:
Copy/asset implication:
Product action:
Response actions taken:
```

## Escalation

Escalate to lead if:

- reviews mention crash/blocker repeatedly;
- performance complaints dominate;
- players accuse false advertising;
- co-op confusion repeats;
- "too little content" repeats;
- review score trends toward dangerous threshold.

## Daily Steam Digest Template

```text
Date:
Build:
New reviews:
Forum threads:
BUG_BLOCKER:
PERF_CONTEXT:
EXPECTATION_MISMATCH:
CONTENT_DEPTH:
UX_FRICTION:
PRAISE_SIGNAL:
Pinned updates made:
Store copy implication:
Product action:
Do not repeat:
```

## Current HECTON-8 Decision

Prepare templates now. Do not interact with reviews until the Steam page/demo exists.
