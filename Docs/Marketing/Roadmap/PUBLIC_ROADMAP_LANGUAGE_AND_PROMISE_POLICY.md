# HECTON-8 Public Roadmap Language And Promise Policy

Status: promise-control policy
Owner lane: SHINOBU_81 / public roadmap
Runtime impact: none

## Purpose

Roadmaps can build trust or destroy it. HECTON-8 must not publish a fantasy backlog as a promise.

## Promise Levels

| Level | Meaning | Public wording |
|---|---|---|
| Current | In current public build. | "Includes..." |
| In active development | Implemented enough to show but not stable. | "We are working on..." |
| Planned | Design intent, not guaranteed. | "Planned focus..." |
| Investigating | Possible direction. | "We are exploring..." |
| Not planned | Outside current scope. | "Not part of current public plan." |

Co-op is `Not planned` for public language unless the user/project changes scope with proof.

## Forbidden Roadmap Language

- guaranteed release dates without production lock;
- "will have co-op";
- "massive open world";
- "zero stutter";
- "fully simulated ocean";
- "all features complete soon";
- "monthly huge updates";
- "we promise";
- "final quality".

## Safe Roadmap Template

```text
Current build focus:
- [Feature present]
- [Feature present]
- [Feature present]

Next development focus:
- [Area]
- [Area]
- [Area]

Not in current public plan:
- co-op/multiplayer;
- [other excluded feature].

Dates and scope may change. We will update this page when build truth changes.
```

## Early Access Roadmap Rules

If EA is used:

- describe current game first;
- explain why EA is needed;
- list likely areas, not giant feature promises;
- avoid exact date unless locked;
- update after scope changes;
- keep price policy honest.

## Public Update Categories

| Category | Use |
|---|---|
| Now | Current build features and known issues. |
| Next | Near-term work with high confidence. |
| Later | Directional, no promise. |
| Not planned | Prevents repeated false expectation. |

## Roadmap Review Checklist

Before publishing:

- Does it imply co-op?
- Does it imply final performance?
- Does it imply dates?
- Does it sell future scope more than current build?
- Can every "current" item be shown in game?
- Would a player feel misled after buying today?

## 2026-05-19 Promise Lint Gate V0

Run this gate on every public roadmap, Steam block, site block, press pitch, creator pitch, social bio, pinned post, demo page, and launch announcement before publication.

### Lint Classification

Every public sentence must be tagged internally as one of:

| Tag | Meaning | Required proof |
|---|---|---|
| `CURRENT_BUILD` | Present in the build players can access now. | Build ID, screenshot/clip/demo proof, or release note. |
| `ACTIVE_WORK` | Implemented enough to show, but not stable. | Internal build evidence and owner approval. |
| `PLANNED_FOCUS` | Direction, not commitment. | Roadmap owner approval and non-date wording. |
| `INVESTIGATING` | Possible future direction. | No sales CTA tied to the sentence. |
| `NOT_PLANNED` | Explicit expectation control. | Scope owner approval. |
| `REMOVE` | Unproved, misleading, or too expensive to defend. | None. Delete or rewrite. |

### Forbidden-To-Allowed Rewrite Matrix

| Risk family | Reject wording | Safer replacement | Public proof required |
|---|---|---|---|
| Co-op | "co-op", "multiplayer", "play with friends" | "single-player-first" or "not part of the current public plan" | Scope decision only; no feature tease. |
| Scale | "massive open world", "100km ocean", "seamless world" | "large hostile underwater spaces" | In-game traversal clip or remove. |
| Performance | "zero stutter", "locked 60 FPS", "runs great on low-end" | "performance work is ongoing" | Hardware/settings matrix, profiler capture, build ID. |
| Simulation | "fully simulated ocean", "realistic ecosystem" | "systems-driven pressure, salvage, and machinery" | Gameplay capture showing player-facing behavior. |
| Visuals | "ray-traced", "cinematic", "AAA graphics" | "industrial deep-sea noir art direction" | Real screenshot/capture, not concept art. |
| Release timing | "coming soon", "monthly updates", "launching this year" | "date TBD" or omit | Production lock and platform page. |
| Demo/access | "demo now", "keys available" | "demo/access will be announced when ready" | Stable build, public CTA gate for public links, or recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` plus exact access-log fields for private access. |
| Competitor | "Subnautica killer", "better than Subnautica" | "for players who want pressure, machinery, and black-water survival" | Never use direct attack copy. |
| AI/assets | "handmade everything" when unverified | "real in-game capture only in public assets" | Asset metadata source and approval. |

### Mechanical Lint Procedure

1. Copy the public text into a review scratchpad.
2. Mark every sentence with one classification tag from the table above.
3. Search manually for these terms before publish: `co-op`, `multiplayer`, `zero`, `locked`, `massive`, `seamless`, `fully simulated`, `realistic ecosystem`, `Subnautica killer`, `Subnautica 2`, `SN2`, `EULA`, `privacy`, `desync`, `stutter`, `performance`, `coming soon`, `soon`, `guarantee`, `promise`.
4. For every `CURRENT_BUILD` sentence, attach a build ID or asset ID.
5. For every `ACTIVE_WORK` or `PLANNED_FOCUS` sentence, remove dates, guarantees, and sales pressure.
6. Delete any sentence that cannot survive the proof check in under five minutes.

### Grep Audit Boundary

The forbidden search terms may appear only in these contexts:

- `Do not use`, `Reject`, `Forbidden`, `Bad copy`, or `Kill rule` sections;
- monitoring/query sections marked listen-only or internal research;
- CRM/source evidence fields that are not final send copy;
- risk register entries describing what to prevent.

If a forbidden term appears in a subject line, final pitch body, Steam/page copy block, ad headline, press opening, public FAQ reply, caption, bio, or announcement draft, rewrite it before publication. Direct competitor-title personalization is allowed only as internal CRM evidence; public or creator-facing copy should use neutral audience-fit language.

### Negative Denial Copy Rule

Expectation control must not rely on proactive "not X / no Y" slogans.

Use positive proof-boundary language for public, creator, press, social, account-profile, signup, and campaign body copy:

- "single-player-first scope";
- "proof-first public scope";
- "scope stays inside what the current build can show";
- "performance claims require measured build/hardware context";
- "competitor-neutral positioning".

Use direct denial wording only when answering an explicit user question, in FAQ/moderation response blocks, or in reject/forbidden-copy lists. If the line can be pasted into a proactive post, subject, pitch body, profile bio, signup form, presskit quote, creator brief, or campaign announcement, rewrite it to proof-boundary language.

### Approved Public Scope Lines

```text
HECTON-8 is single-player-first.
Public scope stays inside what the current build can show.
We are not making performance claims without measured hardware and build proof.
Public screenshots and videos should be real in-game capture, not target renders.
Dates and scope may change until a build is publicly locked.
```

Direct Q&A only:

```text
Co-op is not part of the current public plan.
```

### Competitor-Pain Lint

Any sentence using competitor pain must be tagged `REMOVE` for public use.

Forbidden public claim families:

- `they have EULA/privacy issues`;
- `their co-op/desync is broken`;
- `they stutter/crash, we do not`;
- `they have too little content`;
- `they do not let players fight back`;
- `their building is clunky`;
- `we are the fix for SN2`.

Internal replacement:

```text
Private competitor signal informs which HECTON proof asset we prioritize.
Public copy stays about pressure, salvage, machinery, black water, and honest scope.
```

## Current HECTON-8 Decision

Do not publish public roadmap until a Steam page/demo state exists. Use this policy internally now.
