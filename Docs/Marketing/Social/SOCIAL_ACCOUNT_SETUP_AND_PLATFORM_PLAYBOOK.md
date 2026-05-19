# HECTON-8 Social Account Setup And Platform Playbook

Status: pre-public social ops
Owner lane: SHINOBU_81 / social publishing
Runtime impact: none

## Purpose

Social accounts exist to support Steam conversion, creator trust, and feedback loops. They are not the main product. Posting without assets wastes time.

## Account Priority

| Platform | Priority | Use |
|---|---|---|
| Steam News | P0 | Primary conversion and owner-controlled announcements. |
| YouTube | P0 | Trailer, devlogs, clips for creators/press. |
| Discord | P1 | Community only after proof assets or demo interest. |
| X/Twitter | P1 | Dev/press/creator visibility, short updates. |
| Bluesky | P1 | Dev/community visibility. |
| TikTok | P2 | Short clip tests if vertical clips exist. |
| Reddit | P2 | Critique/listening, not broadcast spam. |
| Mastodon | P3 | Optional dev presence. |
| Instagram | P3 | Visual archive, lower priority for PC survival. |

## Handle Policy

Preferred:

- `Hecton8Game`
- `PlayHecton8`
- `Hecton8`

Reserve consistent handles before public reveal if possible.

## 2026-05-19 Handle Reservation Work Order

Do this quietly before public screenshots. Do not post unless an account requires an initial placeholder.

| Platform | Preferred handle order | Public state now | Required owner record |
|---|---|---|---|
| YouTube | `@Hecton8Game`, `@PlayHecton8`, `@Hecton8` | Private/empty channel acceptable. | Owner email, recovery email, 2FA owner, brand asset path. |
| X/Twitter | `@Hecton8Game`, `@PlayHecton8`, `@Hecton8` | Locked/private or blank profile acceptable. | Login owner, 2FA, backup codes, reserved date. |
| Bluesky | `@hecton8game.*`, `@playhecton8.*`, `@hecton8.*` | Blank is acceptable. | Handle domain, recovery owner. |
| TikTok | `@hecton8game`, `@playhecton8`, `@hecton8` | Reserve only; no vertical spam. | Login owner, 2FA, phone/email custody. |
| Instagram | `@hecton8game`, `@playhecton8`, `@hecton8` | Reserve only. | Login owner, 2FA, Meta ownership if any. |
| Reddit | `u/Hecton8Game` or official dev account | Do not post marketing yet. | Account owner, disclosure policy, subreddit rule log. |

If `Hecton8` is unavailable, prefer `Hecton8Game` over novelty handles. Consistency beats cleverness.

## 2026-05-19 Quiet Public Handle Check

Boundary: this is not account registration. It is a public unauthenticated check only. Final reservation requires the human owner, project email, password manager, 2FA, recovery email, and backup codes.

| Platform | Handle | Public check result | Action |
|---|---|---|---|
| X/Twitter | `@Hecton8` | TAKEN. Public page shows unrelated `Hecton`, London, England, joined May 2017. | Do not use as official handle unless ownership changes. |
| X/Twitter | `@Hecton8Game` | Public unauthenticated fetch returned 404. | Candidate. Human must confirm while logged in and reserve first if available. |
| X/Twitter | `@PlayHecton8` | Public unauthenticated fetch returned 404. | Backup candidate. Reserve only if `@Hecton8Game` is unavailable. |
| YouTube | `@Hecton8` | TAKEN. Public fetch resolves to unrelated `Hector Covanti - YouTube`. | Do not use as official handle. |
| YouTube | `@Hecton8Game` | Public fetch returned 404. | Candidate for project channel. Human must confirm while logged in and reserve with brand account. |
| YouTube | `@PlayHecton8` | Public fetch returned 404. | Backup candidate. Use only if `@Hecton8Game` cannot be reserved. |
| Bluesky | `hecton8.bsky.social` | Public resolveHandle check returned 400/not resolved in this pass. | Candidate only; human must confirm during account creation. |
| Bluesky | `hecton8game.bsky.social` | Public resolveHandle check returned 400/not resolved in this pass. | Preferred candidate if `.bsky.social` handle is available. |
| Bluesky | `playhecton8.bsky.social` | Public resolveHandle check returned 400/not resolved in this pass. | Backup candidate. |
| TikTok | `@hecton8game`, `@playhecton8`, `@hecton8` | Public unauthenticated fetch returned generic TikTok HTML, not reliable handle availability. | Must be checked while logged in; do not claim availability from public fetch. |
| Instagram | `@hecton8game`, `@playhecton8`, `@hecton8` | Public unauthenticated fetch returned generic Instagram HTML, not reliable handle availability. | Must be checked while logged in; do not claim availability from public fetch. |

Registration order:

1. Try `Hecton8Game`.
2. If unavailable, try `PlayHecton8`.
3. Do not use `Hecton8` on X unless the existing unrelated account is no longer present.
4. Record owner email, recovery email, 2FA method, backup-code custody, and reservation date immediately after creation.
5. Keep the profile private/blank until the first screenshot pack exists.

## 2026-05-19 Owner Account Creation Handoff

This is the exact human-owned registration checklist. Do not delegate credential creation to an agent unless the project email, password manager, recovery channel, 2FA device, and backup-code storage are visible and controlled by the owner.

### Required Before Creating Any Account

```text
Project email:
Recovery email:
Password manager vault:
2FA method:
Backup-code storage path:
Owner:
Date:
```

Reject account creation if any field is missing. A blank social profile is acceptable; orphaned credentials are not.

### Reservation Order

| Priority | Platform | First try | Second try | Third try | Must record |
|---:|---|---|---|---|---|
| 1 | Steam developer/community links | HECTON-8 official name | N/A | N/A | Steamworks owner and public URL when live |
| 2 | YouTube brand channel | `@Hecton8Game` | `@PlayHecton8` | project-name variant approved by owner | Brand account owner, handle URL, recovery |
| 3 | X/Twitter | `@Hecton8Game` | `@PlayHecton8` | no novelty handle without approval | 2FA, backup codes, exact public URL |
| 4 | Bluesky | `hecton8game.bsky.social` | `playhecton8.bsky.social` | custom domain later | handle, DID/profile URL, recovery |
| 5 | TikTok | `@hecton8game` | `@playhecton8` | hold | phone/email custody, no first post |
| 6 | Instagram | `@hecton8game` | `@playhecton8` | hold | Meta owner, recovery, profile URL |
| 7 | Reddit | `u/Hecton8Game` | owner-named dev account | hold | disclosure policy and rule log |

### Reservation Notes To Paste Into Password Manager

```text
Project: HECTON-8
Public stance: single-player-first, no co-op promise, no Subnautica-killer copy, no performance claims without receipts.
Profile state: private/blank until first screenshot pack passes QA.
First public asset gate: Docs/Marketing/QA/MARKETING_ASSET_QA_CHECKLIST.md minimum 9/12 social, 10/12 Steam.
Primary links source: Steam page and presskit only after live.
```

### First Login Hardening

Do immediately after account creation:

1. Add recovery email.
2. Enable 2FA.
3. Store backup codes.
4. Set display name `HECTON-8`.
5. Set profile to private/blank if platform allows.
6. Do not upload AI/concept art as profile proof.
7. Do not follow/comment/post from the account before the first screenshot pack exists.
8. Record the exact public URL in this file or the account vault.

### Abort Conditions

Abort registration if:

- the platform asks for a personal phone number that the owner does not want tied to the project;
- handle availability cannot be confirmed while logged in;
- a captcha/2FA/recovery step cannot be completed by the owner;
- the account would need to publish a first post before assets exist;
- the available handle creates confusion with unrelated `Hecton8` accounts.

Minimum X profile if a profile must be public:

```text
HECTON-8
Single-player deep-sea survival about pressure, salvage, machinery, and black water.
Public assets not live yet.
```

Minimum profile while private/quiet:

```text
HECTON-8
Single-player deep-sea survival about pressure, salvage, machinery, and black water.
Public assets not live yet.
```

Check for accidental Cyrillic/Latin character substitutions before publishing the name.

## Profile Bio Template

```text
HECTON-8 - single-player deep-sea survival about pressure, salvage, machinery, and the Seed Ship anomaly.
Steam: [URL]
```

No co-op. No "Subnautica killer". No performance claim.

## 2026-05-19 Platform Launch Kit V0

Status: draft-only / blocked until handle custody, asset QA, and official links exist.

### Bio Variants

Use the shortest variant that fits each platform. Do not add claims to fill space.

| Platform | Bio |
|---|---|
| X / Bluesky | `Single-player deep-sea survival about pressure, salvage, machinery, and black water. Official HECTON-8 account.` |
| YouTube | `HECTON-8 is a single-player deep-sea survival game about pressure, salvage, machinery, and the Seed Ship anomaly. Official clips, trailers, and dev updates.` |
| TikTok / Shorts | `Deep-sea survival. Pressure, machinery, salvage, black water. Official HECTON-8 clips.` |
| Instagram | `Official HECTON-8 visuals: single-player deep-sea survival, pressure machinery, salvage, black water.` |
| Reddit profile | `Developer account for HECTON-8. Single-player deep-sea survival; no co-op promise; public posts disclose dev status.` |

### First Three Public Posts

Run only after `PLAN-SHOT-001`, `PLAN-SHOT-003`, and `PLAN-CAPSULE-001` pass their gates.

| Order | Platform | Asset | Copy | CTA | Kill if |
|---:|---|---|---|---|---|
| 1 | X / Bluesky | `PLAN-SHOT-001` | `First in-game look at HECTON-8. Single-player deep-sea survival about pressure, salvage, machinery, and black water. Blunt read wanted: does this feel like a distinct survival game, or just generic underwater sci-fi?` | Feedback question only. | Replies mostly say "what do you do?" or "AI/concept art". |
| 2 | YouTube Community / Short if available | `PLAN-CLIP-001` or `PLAN-CLIP-003` | `A pressure problem should read before the caption. If this clip needs explanation, it failed.` | Feedback question only. | First 3 seconds do not show action/consequence. |
| 3 | Steam News / X / Bluesky | `PLAN-CAPSULE-001` winner + Steam URL | `HECTON-8 now has an official Steam page: single-player deep-sea survival focused on pressure, salvage, machinery, and black-water route risk.` | Official Steam link only. | Steam URL not live, capsule not cold-read, or copy implies co-op/perf. |

### Pinned Post V0

Use only after official Steam URL exists.

```text
HECTON-8 is a single-player deep-sea survival game about pressure, salvage, machinery, and the cost of staying alive below the light.

Steam: [official URL]
Presskit: [official URL if live]

No co-op promise. No performance claims without measured build/hardware context.
```

### Cross-Post Rule

Do not paste the same text everywhere. Keep the same facts, but change the ask:

- X/Bluesky: one blunt question.
- YouTube: clip retention and comments.
- Reddit: critique only, with dev disclosure and no wishlist CTA unless rules allow.
- Steam: official update, no insecurity language.

## Pinned Post Template

```text
HECTON-8 is a single-player-first deep-sea survival game about pressure, salvage, machinery, and the cost of staying alive below the light.

Steam: [URL]
Presskit: [URL]
Discord: [URL if open]
```

## Platform Cadence

Before screenshots:

- 1-2 low-risk dev/process posts per week;
- no wishlist begging;
- no daily empty posts.

After screenshots:

- 2-3 posts per week;
- 1 asset per post;
- one CTA max.

After demo:

- daily short support/update window for first week;
- then 2-3 posts/week.

## Post Families

| Family | Use |
|---|---|
| Screenshot hook | first visual identity. |
| 20s gameplay clip | creator/social conversion. |
| Tech/dev note | dev credibility. |
| Failure/fix note | shows honesty. |
| Steam announcement | owner-controlled update. |
| Survey/feedback | only with real asset/build. |
| Patch notes | after demo/launch. |

## Social QA

Before posting:

- asset passed QA;
- platform rules checked if community-driven;
- CTA works;
- no duplicate copy across platforms;
- developer status is clear where needed;
- no fake player voice;
- no co-op implication;
- no unproved performance claim.

## First 10 Public Social Posts

1. First in-game machinery screenshot.
2. Pressure warning / hatch / leak screenshot.
3. "What is HECTON-8?" short thread.
4. Seed Ship anomaly teaser screenshot.
5. 15s salvage route clip.
6. Base pressure/failure clip.
7. Devlog: why pressure must be readable.
8. Steam page live announcement.
9. Demo/playtest signup announcement.
10. Known issues/feedback request after demo.

## Reply Rules

Use short direct replies.

### "Is it co-op?"

```text
No. HECTON-8 is single-player-first. We are not selling a co-op promise.
```

### "Is this Subnautica?"

```text
It shares underwater survival adjacency, but the lane is different: pressure, machinery, salvage, corrosion, and deep-sea noir.
```

### "Will it run on my PC?"

```text
We will publish performance details only with build, hardware, settings, and frame-time context. No empty FPS promises.
```

## Current HECTON-8 Decision

Reserve handles and prepare profile assets now. Do not start heavy posting until real screenshots exist.
