# Discord And Community Server Setup

Status: future setup / do not open too early
Public stance: single-player-first scope / proof-first public copy
Runtime impact: none

## Hard Rule

Do not create an official public Discord surface, publish an invite, announce a server, or count Discord members/signups unless `discord_open_permission_gate = ALLOW_DISCORD_OPEN_VERIFIED` for that exact server and invite route. A private draft checklist is not an open gate.

## Objective

Create a small, useful community hub only when there is enough real material to discuss. A dead Discord damages trust. A noisy Discord burns time. The server should exist to collect feedback, coordinate testers, and make updates easy to follow.

## Open Gate

Machine gate: `discord_open_permission_gate = HOLD_NO_DISCORD_PUBLIC_OPEN`. The only future allow value is `ALLOW_DISCORD_OPEN_VERIFIED`, and it is server-specific: approving an internal draft or private moderator sandbox does not approve a public invite, public CTA, launch announcement, demo-support server, creator/press room, or regional community.

Do not open a public server until at least two proof conditions exist:

- Steam Coming Soon page with official URL;
- 6+ real screenshots with QA and asset metadata claim checks;
- 2 short gameplay clips with QA and asset metadata claim checks;
- one first-page agency-proof asset or clip has AB-009/KPI decision-read fields (`what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`) if the server launch uses gameplay, pressure, threat, route-risk, salvage failure, or first-public proof;
- demo test window;
- regular dev update cadence;

`ALLOW_DISCORD_OPEN_VERIFIED` additionally requires:

- owner-controlled admin account, 2FA, recovery, backup-code custody, and server owner record;
- `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED` for public contact/support routes;
- moderation roles, rules, private mod-log channel, and the first-hour moderator script assigned to named owners;
- FAQ pins for single-player-first scope, competitor-neutral positioning, performance-claim proof, key/access policy, and private-build leak policy;
- exact invite URL custody, invite expiry/permanent decision, and revocation owner;
- `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` before any public invite link is placed in a bio, Steam page, presskit, trailer card, showcase page, newsletter, or post;
- `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED` before any launch announcement or no-link Discord teaser goes live;
- `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` before any demo/key/playtest/preview route is connected to a private channel;
- no bought/fake members, no imported contacts, no scraped DMs, and no public member-count reporting before route/source fields exist.

## Server Structure

| Channel | Purpose |
|---|---|
| `announcements` | Official updates only. |
| `dev-updates` | Screenshots, clips, progress posts. |
| `faq` | Scope, multiplayer-scope boundary, performance claim boundary. |
| `screenshots-clips` | Official media. |
| `feedback-visuals` | Screenshot/capsule/clip critique. |
| `feedback-demo` | Demo notes when demo exists. |
| `bug-reports` | Structured bug reports only. |
| `survival-systems` | Pressure, base, machinery discussion. |
| `lore-seed-ship` | Narrative/world discussion. |
| `off-topic` | Keep community noise contained. |
| `mod-log` | Private moderation record. |
| `creator-press-private` | Private role-gated info if needed. |

## Roles

| Role | Meaning |
|---|---|
| `Developer` | Official team only. |
| `Moderator` | Can enforce rules. |
| `Tester` | Demo/test access if applicable. |
| `Creator` | Verified creator, no special promises. |
| `Press` | Verified press, no embargo unless explicitly agreed. |
| `Community` | Default. |

## Rules

1. No harassment, hate, threats, or personal attacks.
2. No piracy/key trading.
3. No leaked builds or private files.
4. No spam/self-promo outside allowed channels.
5. No demanding co-op roadmap promises.
6. No fake official statements.
7. Criticism is allowed; abuse is not.
8. Bug reports need build version, hardware, repro steps, and screenshot/video when possible.

## FAQ Pins

### Is HECTON-8 co-op?

Current public scope is single-player-first. We would rather be honest about scope than sell a feature before it exists in the build.

### Is this a Subnautica killer?

No. HECTON-8 is aimed at a different feeling: pressure, machinery, salvage, black-water exploration, and industrial isolation.

### What about performance?

We will not make public performance claims without build version, hardware, settings, and measurement method.

### Can creators get keys?

Later, through verified public business contact routes and logged key distribution. No random DM keys.

## Moderation Escalation

| Issue | Action |
|---|---|
| Feature demand / co-op pressure | Link FAQ, do not argue. |
| Subnautica comparison | Acknowledge setting overlap, restate differentiation. |
| Bug report | Move to structured report format. |
| Harassment | Warn or ban depending on severity. |
| Key scam | Ban, log, never send keys. |
| Leaked build | Remove, log, revoke access if possible. |
| Misinformation | Correct once with source; do not debate endlessly. |

## Launch Announcement

Use only after `discord_open_permission_gate = ALLOW_DISCORD_OPEN_VERIFIED`, `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for the invite destination, and `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED` for this exact announcement all pass.

HECTON-8 now has a small community server for development updates, screenshot/clip feedback, and future demo notes.

Scope reminder: HECTON-8 is single-player-first, proof-first, and competitor-neutral. Performance details wait for measured build proof.

Join if you want to help us make pressure, machinery, and black-water survival read clearly.

Do not use this announcement if the open gate relies on pressure/gameplay/route-risk footage without AB-009/KPI decision-read fields.

## Weekly Community Routine

- post one real update or stay quiet;
- summarize top feedback;
- move product issues into triage;
- remove spam;
- answer scope questions with FAQ;
- do not tease features that are not built.
