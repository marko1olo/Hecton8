# HECTON-8 Social Account Setup And Platform Playbook

Status: pre public social ops
Owner lane: SHINOBU_81 / social publishing
Runtime impact: none

## Purpose

Social accounts exist to support Steam conversion, creator trust, and feedback loops. They are not the main product. Posting without assets wastes time.

## Account Priority

| Platform | Priority | Use |
|---|---|---|
| Steam News | P0 | Primary conversion and owner controlled announcements. |
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

  `Hecton8Game`
  `PlayHecton8`
  `Hecton8`

Keep a consistent candidate-handle list before public reveal. Actual reservation is blocked until `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`.

## 2026 05 19 Handle Reservation Work Order

Do candidate checks quietly before public screenshots. Do not register, reserve, or post unless `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`; if a platform requires an initial placeholder before custody is complete, abort instead of creating an orphaned surface.

| Platform | Preferred handle order | Public state now | Required owner record |
|---|---|---|---|
| YouTube | `@Hecton8Game`, `@PlayHecton8`, `@Hecton8` | Private/empty channel acceptable. | Owner email, recovery email, 2FA owner, brand asset path. |
| X/Twitter | `@Hecton8Game`, `@PlayHecton8`, `@Hecton8` | Locked/private or blank profile acceptable. | Login owner, 2FA, backup codes, reserved date. |
| Bluesky | `@hecton8game.*`, `@playhecton8.*`, `@hecton8.*` | Blank is acceptable. | Handle domain, recovery owner. |
| TikTok | `@hecton8game`, `@playhecton8`, `@hecton8` | Reserve only; no vertical spam. | Login owner, 2FA, phone/email custody. |
| Instagram | `@hecton8game`, `@playhecton8`, `@hecton8` | Reserve only. | Login owner, 2FA, Meta ownership if any. |
| Reddit | `u/Hecton8Game` or official dev account | Do not post marketing yet. | Account owner, disclosure policy, subreddit rule log. |

If `Hecton8` is unavailable, prefer `Hecton8Game` over novelty handles. Consistency beats cleverness.

## 2026 05 19 Quiet Public Handle Check

Boundary: this is not account registration. It is a public unauthenticated check only. Final reservation requires the human owner, project email, password manager, 2FA, recovery email, and backup codes.

| Platform | Handle | Public check result | Action |
|---|---|---|---|
| X/Twitter | `@Hecton8` | TAKEN. Public page shows unrelated `Hecton`, London, England, joined May 2017. | Do not use as official handle unless ownership changes. |
| X/Twitter | `@Hecton8Game` | Public unauthenticated fetch returned 404. | Candidate. Human must confirm while logged in and reserve first if available. |
| X/Twitter | `@PlayHecton8` | Public unauthenticated fetch returned 404. | Backup candidate. Reserve only if `@Hecton8Game` is unavailable. |
| YouTube | `@Hecton8` | TAKEN. Public fetch resolves to unrelated `Hector Covanti   YouTube`. | Do not use as official handle. |
| YouTube | `@Hecton8Game` | Public fetch returned 404. | Candidate for project channel. Human must confirm while logged in and reserve with brand account. |
| YouTube | `@PlayHecton8` | Public fetch returned 404. | Backup candidate. Use only if `@Hecton8Game` cannot be reserved. |
| Bluesky | `hecton8.bsky.social` | Public resolveHandle check returned 400/not resolved in this pass. | Candidate only; human must confirm during account creation. |
| Bluesky | `hecton8game.bsky.social` | Public resolveHandle check returned 400/not resolved in this pass. | Preferred candidate if `.bsky.social` handle is available. |
| Bluesky | `playhecton8.bsky.social` | Public resolveHandle check returned 400/not resolved in this pass. | Backup candidate. |
| TikTok | `@hecton8game`, `@playhecton8`, `@hecton8` | Public unauthenticated fetch returned generic TikTok HTML, not reliable handle availability. | Must be checked while logged in; do not claim availability from public fetch. |
| Instagram | `@hecton8game`, `@playhecton8`, `@hecton8` | Public unauthenticated fetch returned generic Instagram HTML, not reliable handle availability. | Must be checked while logged in; do not claim availability from public fetch. |

### 2026 05 19 Public Recheck Addendum V1

This recheck used only public unauthenticated requests. No private browser session, login, account creation, cookie inspection, or credential storage occurred.

| Platform | Handle | Recheck result | Decision |
|---|---|---|---|
| X/Twitter | `@Hecton8` | Public page still resolves to an unrelated account in web view. Raw `curl` requests returned 403 for X and are not useful as availability proof. | Treat as taken; do not use. |
| X/Twitter | `@Hecton8Game` | Public web view returned 404; raw `curl` returned 403. | Candidate only. Must be confirmed while owner is logged in before reservation. |
| X/Twitter | `@PlayHecton8` | Public web view returned 404; raw `curl` returned 403. | Backup candidate only. Must be confirmed while owner is logged in before reservation. |
| YouTube | `@hecton8` | Public page returns `Hector Covanti   YouTube`. | Treat as taken/unrelated. |
| YouTube | `@hecton8game` | Public request returned 404. | Candidate only; reserve with owner controlled brand account if available in logged in flow. |
| YouTube | `@playhecton8` | Public request returned 404. | Backup candidate only. |
| Bluesky | `hecton8.bsky.social` | Public `resolveHandle` returned 400/not resolved. | Candidate only; confirm during account creation. |
| Bluesky | `hecton8game.bsky.social` | Public `resolveHandle` returned 400/not resolved. | Preferred candidate if available in account creation. |
| Bluesky | `playhecton8.bsky.social` | Public `resolveHandle` returned 400/not resolved. | Backup candidate. |

Current action: no registration. The user has granted permission in chat, but the operating requirement is still project owned email, password manager vault, recovery email, 2FA owner, backup code custody, `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`, and `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`. Without those fields, account creation would produce orphaned official surfaces.

## 2026-05-20 Account Registration Preflight Verdict V0

This verdict overrides ad hoc browser/account requests until the fields below change. A logged-in personal browser session, cookies, or chat permission is not account custody proof.

Machine gate: `account_registration_permission_gate = HOLD_ACCOUNT_CREATION`. The only future allow value is `ALLOW_ACCOUNT_REGISTRATION_VERIFIED`, and it requires every row below to leave hold state plus a completed post-registration custody row immediately after creation.

| Requirement | Current state | Registration effect |
|---|---|---|
| `account_registration_permission_gate` | HOLD_ACCOUNT_CREATION | HOLD_ACCOUNT_CREATION |
| `official_inbox_custody_gate` | HOLD_NO_PROJECT_INBOX_CUSTODY | HOLD_ACCOUNT_CREATION |
| Owner-controlled project inbox recorded | NOT_RECORDED | HOLD_ACCOUNT_CREATION |
| Password manager vault item ready | NOT_RECORDED | HOLD_ACCOUNT_CREATION |
| Recovery owner verified | NOT_RECORDED | HOLD_ACCOUNT_CREATION |
| 2FA owner present and method chosen | NOT_RECORDED | HOLD_ACCOUNT_CREATION |
| Backup-code destination ready | NOT_RECORDED | HOLD_ACCOUNT_CREATION |
| Approved handle selected from candidate list | CANDIDATE_ONLY | HOLD_ACCOUNT_CREATION |
| Approved profile assets | NOT_READY_REAL_ASSET | HOLD_ACCOUNT_CREATION |
| Exact public URL destination for vault record | NOT_CREATED | HOLD_ACCOUNT_CREATION |

Current verdict: `HOLD_ACCOUNT_CREATION`; `account_registration_permission_gate = HOLD_ACCOUNT_CREATION`.

Candidate handles are notes only while this verdict holds. They are not reservation permission, posting permission, or proof that a logged-in platform flow will accept the handle.

Allowed agent work while this verdict holds:

- public unauthenticated handle checks;
- profile copy prep;
- account field kit maintenance;
- custody checklist maintenance;
- no-link content drafts that do not publish.

Blocked agent work while this verdict holds:

- account creation;
- login through personal browser sessions;
- cookie/session inspection;
- publishing first posts;
- following/commenting/DMing from official handles;
- storing passwords, 2FA seeds, backup codes, cookies, or session tokens in docs.

Registration order:

1. Try `Hecton8Game`.
2. If unavailable, try `PlayHecton8`.
3. Do not use `Hecton8` on X unless the existing unrelated account is no longer present.
4. Record owner email, recovery email, 2FA method, backup code custody, and reservation date immediately after creation.
5. Keep the profile private/blank until the first screenshot pack exists.

## 2026 05 19 Owner Account Creation Handoff

This is the exact human owned registration checklist. Do not delegate credential creation to an agent unless the project email, password manager, recovery channel, 2FA device, and backup code storage are visible and controlled by the owner.

### Required Before Creating Any Account

```text
Project email:
Recovery email:
Password manager vault:
2FA method:
Backup code storage path:
Owner:
Date:
```

Reject account creation if any field is missing. A blank social profile is acceptable; orphaned credentials are not.

Inbox source of truth: `Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md`  > `Official Project Inbox Gate V0` and `official_inbox_custody_gate`. Account registration source of truth: `account_registration_permission_gate` in this file. Use the same owner controlled inbox/custody record for social account registration, presskit contact, creator access, and future support routing. Do not create a social account from a personal, throwaway, agent owned, or undocumented email even if the platform would allow it.

## 2026 05 19 Agent Assisted Browser Work Boundary V0

Use this if the owner later wants an agent to help inside a browser session. This is not active now; it is the safe operating mode for future account work.

| Action | Agent may do | Agent must not do |
|---|---|---|
| Public handle check | Open public profile URLs, record taken/candidate/inconclusive. | Claim availability from login walls or JS placeholders. |
| Account registration | Fill fields only while owner controls project email, password manager, 2FA, and backup code storage. | Create accounts under agent owned, temporary, personal, or unrecorded credentials. |
| Login | Use credentials only when owner is present or has supplied a controlled handoff path. | Extract browser cookies, inspect personal sessions, or store secrets in docs. |
| Profile setup | Paste approved bio, display name, avatar/banner path, and links. | Publish posts, follow accounts, DM creators, or reveal unapproved assets. |
| Recovery hardening | Prompt owner to save 2FA and backup codes in the vault. | Move past recovery/2FA screens without documented custody. |
| Browser focus | Prefer background/public web checks and avoid visible windows when possible. | Hijack the active desktop workflow or open noisy tabs unnecessarily. |

Minimum proof before an agent registers anything:

```text
account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED:
Project email exists:
Password manager vault open:
2FA owner present:
Backup code destination ready:
Approved handle:
Approved display name:
Approved bio:
Approved avatar/banner:
Account URL destination in vault:
```

Abort if any field is blank. The correct fallback is a reservation checklist, not a half owned public account.

### Reservation Order

| Priority | Platform | First try | Second try | Third try | Must record |
|---:|---|---|---|---|---|
| 1 | Steam developer/community links | HECTON-8 official name | N/A | N/A | Steamworks owner and public URL when live |
| 2 | YouTube brand channel | `@Hecton8Game` | `@PlayHecton8` | project name variant approved by owner | Brand account owner, handle URL, recovery |
| 3 | X/Twitter | `@Hecton8Game` | `@PlayHecton8` | no novelty handle without approval | 2FA, backup codes, exact public URL |
| 4 | Bluesky | `hecton8game.bsky.social` | `playhecton8.bsky.social` | custom domain later | handle, DID/profile URL, recovery |
| 5 | TikTok | `@hecton8game` | `@playhecton8` | hold | phone/email custody, no first post |
| 6 | Instagram | `@hecton8game` | `@playhecton8` | hold | Meta owner, recovery, profile URL |
| 7 | Reddit | `u/Hecton8Game` | owner named dev account | hold | disclosure policy and rule log |

### Reservation Notes To Paste Into Password Manager

```text
Project: HECTON-8
Public stance: single player first scope, competitor neutral copy, performance claims require measured proof.
Profile state: private/blank until first screenshot pack passes QA.
First public asset gate: Docs/Marketing/QA/MARKETING_ASSET_QA_CHECKLIST.md minimum 9/12 social, 10/12 Steam, plus asset metadata claim checks and official link/custody gates.
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

### Post-Registration Custody Record V0

Fill one row immediately after each successful registration. Do not record passwords, 2FA seeds, backup codes, cookies, or session tokens in this document.

| Platform | Handle | Public URL | Login email alias | Vault item name | Recovery owner checked | 2FA enabled | Backup codes stored | Profile visibility | First public asset gate | Current status |
|---|---|---|---|---|---|---|---|---|---|---|
| YouTube | UNRESERVED_NOT_CREATED | UNRECORDED_NOT_CREATED | HOLD_OFFICIAL_INBOX_CUSTODY | UNRECORDED_NOT_CREATED | NO | NO | NO | private/blank | HOLD_NO_FIRST_PUBLIC_ASSET_POST | NOT_CREATED |
| X/Twitter | UNRESERVED_NOT_CREATED | UNRECORDED_NOT_CREATED | HOLD_OFFICIAL_INBOX_CUSTODY | UNRECORDED_NOT_CREATED | NO | NO | NO | private/blank | HOLD_NO_FIRST_PUBLIC_ASSET_POST | NOT_CREATED |
| Bluesky | UNRESERVED_NOT_CREATED | UNRECORDED_NOT_CREATED | HOLD_OFFICIAL_INBOX_CUSTODY | UNRECORDED_NOT_CREATED | NO | NO | NO | private/blank | HOLD_NO_FIRST_PUBLIC_ASSET_POST | NOT_CREATED |
| TikTok | UNRESERVED_NOT_CREATED | UNRECORDED_NOT_CREATED | HOLD_OFFICIAL_INBOX_CUSTODY | UNRECORDED_NOT_CREATED | NO | NO | NO | private/blank | HOLD_NO_FIRST_PUBLIC_ASSET_POST | NOT_CREATED |
| Instagram | UNRESERVED_NOT_CREATED | UNRECORDED_NOT_CREATED | HOLD_OFFICIAL_INBOX_CUSTODY | UNRECORDED_NOT_CREATED | NO | NO | NO | private/blank | HOLD_NO_FIRST_PUBLIC_ASSET_POST | NOT_CREATED |
| Reddit | UNRESERVED_NOT_CREATED | UNRECORDED_NOT_CREATED | HOLD_OFFICIAL_INBOX_CUSTODY | UNRECORDED_NOT_CREATED | NO | NO | NO | blank/disclosed dev | HOLD_SUBREDDIT_RULE_AND_PUBLIC_POST_GATE | NOT_CREATED |

Allowed `Current status` values:

- `NOT_CREATED`;
- `CREATED_PRIVATE`;
- `CREATED_PUBLIC_BLANK`;
- `PROFILE_READY_NOT_POSTING`;
- `READY_FOR_FIRST_ASSET_POST`;
- `LOCKED_RECOVERY_ISSUE`;
- `ABANDONED_DO_NOT_USE`.

If any row is `LOCKED_RECOVERY_ISSUE` or `ABANDONED_DO_NOT_USE`, add it to `Data/MARKETING_RISK_REGISTER.md` before creating a replacement account.

### Abort Conditions

Abort registration if:

  the platform asks for a personal phone number that the owner does not want tied to the project;
  handle availability cannot be confirmed while logged in;
  a captcha/2FA/recovery step cannot be completed by the owner;
  the account would need to publish a first post before assets exist;
  the available handle creates confusion with unrelated `Hecton8` accounts.

Minimum X profile if a profile must be public:

```text
HECTON-8
Single player deep sea survival about pressure, salvage, machinery, and black water.
Public assets not live yet.
```

Minimum profile while private/quiet:

```text
HECTON-8
Single player deep sea survival about pressure, salvage, machinery, and black water.
Public assets not live yet.
```

Check for accidental Cyrillic/Latin character substitutions before publishing the name.

## 2026 05 19 Account Page Field Kit V0

Paste these fields only after `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`, the account exists under owner-controlled credentials, and the post-registration custody row has been filled.

| Field | Primary value | Backup value | Notes |
|---|---|---|---|
| Display name | `HECTON-8` | `HECTON-8 Game` | Keep hyphen consistent. |
| Username/handle | `Hecton8Game` | `PlayHecton8` | Do not use unrelated/taken `Hecton8` handles. |
| Short bio | `Single player deep sea survival about pressure, salvage, machinery, and black water.` | `Deep sea survival. Pressure, salvage, machinery, black water.` | Scope neutral; no FPS or competitor claim. |
| Long bio | `HECTON-8 is a single player deep sea survival game about pressure, salvage, machinery, and the Seed Ship anomaly.` | `Official HECTON-8 account. Public assets are released only when captured from the current build.` | Use on YouTube/About pages. |
| Location | blank | blank | Avoid fake studio geography. |
| Website | `[gated Steam URL after steam_page_publish + public_cta]` | `[gated presskit URL after press_release + public_cta]` | Do not link placeholders publicly. |
| Contact | `[owner-controlled project email]` | blank | Use only after inbox custody passes. |
| Avatar | approved logo mark | text only `HECTON-8` mark | Do not use concept art as proof. |
| Banner | approved in game screenshot/capsule | black water machinery crop | Must pass asset QA. |

If a platform forces a first post after registration custody is already allowed, use only this no-link reservation placeholder and log it as a forced account-reservation artifact. If `public_post_permission_gate` is still held and the platform does not allow private/blank setup, abort registration instead.

```text
Official HECTON-8 account reserved.

Public gameplay assets are not live yet. HECTON-8 is single player deep sea survival about pressure, salvage, machinery, and black water.
```

Do not add a Steam link until `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, and UTM rules are ready. If this forced reservation post is used after custody is allowed, log it as `route_class = forced_reservation_no_link` and do not count replies as anything beyond `consent_provenance = public_comment`.

## Profile Bio Template

```text
HECTON-8   single player deep sea survival about pressure, salvage, machinery, and the Seed Ship anomaly.
Steam: HOLD_SOCIAL_STEAM_URL - fill only after `steam_page_publish_permission_gate` and `public_cta_permission_gate` pass for this exact profile surface.
```

Scope: single player first. No competitor attack copy. No performance claim.

## 2026 05 19 Platform Launch Kit V0

Status: draft only / blocked until `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`; public links also require `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`.

## 2026-05-20 Public Post Permission Gate V0

Machine gate: `public_post_permission_gate = HOLD_NO_PUBLIC_POST`. The only future allow value is `ALLOW_PUBLIC_POST_VERIFIED`, and it is post-specific: approving one no-link critique post does not approve a thread, trailer end card, Steam news item, creator ask, or another platform.

Allow requires:

- `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`;
- approved post target/platform and platform rule check where community-driven;
- real asset IDs for asset-led posts, or an approved quiet pre-asset row that does not imply public proof;
- asset QA and asset metadata claim checks for any referenced screenshot, clip, capsule, trailer, Steam page, demo, or presskit;
- Promise Lint pass for every public sentence;
- `route_class` and `consent_provenance` planned before posting;
- `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for any public link, otherwise no-link feedback copy only;
- no private access link;
- no unsupported multiplayer, performance, AI, feature-scope, or competitor-attack claim.

### Anonymous Surface Posting Addendum

4chan/Dvach-style surfaces may not have durable official accounts, but they are still public posts. Lack of an account system does not bypass the post gate.

For an anonymous/no-account surface, the post approval record must name:

- surface;
- board/thread;
- same-day rule/fit check;
- asset ID;
- route class `no_link_feedback`;
- exact critique question;
- developer disclosure wording;
- owner available for the reply window;
- stop condition.

Blocked on anonymous surfaces:

- fake-player discovery voice;
- sockpuppet follow-up replies;
- self-bumps;
- Steam/Discord/signup/presskit/key/access links;
- asking users to DM;
- importing comments into CRM, newsletter, playtester, creator, press, or support systems;
- using replies as positive KPI without route/provenance and independent confirmation.

Allowed anonymous-surface output:

- asset-specific critique;
- clone-risk cue;
- AI-looking cue;
- engine/tool trust cue;
- decision-read pass/fail note;
- template revision;
- `REVISE`, `KILL`, or `MONITOR_ONLY` campaign note.

An anonymous imageboard post cannot create a public CTA route, creator lead, private access route, or Steam movement.

### Bio Variants

Use the shortest variant that fits each platform. Do not add claims to fill space.

| Platform | Bio |
|---|---|
| X / Bluesky | `Single player deep sea survival about pressure, salvage, machinery, and black water. Official HECTON-8 account.` |
| YouTube | `HECTON-8 is a single player deep sea survival game about pressure, salvage, machinery, and the Seed Ship anomaly. Official clips, trailers, and dev updates.` |
| TikTok / Shorts | `Deep sea survival. Pressure, machinery, salvage, black water. Official HECTON-8 clips.` |
| Instagram | `Official HECTON-8 visuals: single player deep sea survival, pressure machinery, salvage, black water.` |
| Reddit profile | `Developer account for HECTON-8. Single player first deep sea survival; public posts disclose dev status.` |

### First Three Public Posts

Run only after `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`, `PLAN-SHOT-001`, `PLAN-SHOT-003`, one agency/decision proof clip from `PLAN-CLIP-001` or `PLAN-CLIP-003`, and `PLAN-CAPSULE-001` pass QA, asset metadata claim checks, non-pending `viewer_named_decision`, valid non-held `capture_verdict`, AB-009/KPI decision-read fields where the post claims gameplay/pressure/route-risk proof, and official link/custody gates. If no decision clip exists, keep the second public post held instead of replacing it with another mood still.

| Order | Platform | Asset | Copy | CTA | Reporting | Kill if |
|---|---|---|---|---|---|---|
| 1 | X / Bluesky | `PLAN-SHOT-001` | `First in game look at HECTON-8. Single player deep sea survival about pressure, salvage, machinery, and black water. Blunt read wanted: does this feel like a distinct survival game, or just generic underwater sci fi?` | Feedback question only. | `route_class = no_link_feedback`; `consent_provenance = public_comment` only. | Replies mostly say "what do you do?" or "AI/concept art". |
| 2 | YouTube Community / Short if available | `PLAN-CLIP-001` or `PLAN-CLIP-003` | `A pressure problem should read before the caption. If this clip needs explanation, it failed.` | Feedback question only. | `route_class = no_link_feedback`; `consent_provenance = public_comment` only. | First 3 seconds do not show action/consequence. |
| 3 | Steam News / X / Bluesky | `PLAN-CAPSULE-001` winner + Steam URL | HOLD_SOCIAL_STEAM_PAGE_LIVE_COPY - say HECTON-8 has an official Steam page only after `steam_page_publish_permission_gate`, destination-specific `public_cta_permission_gate`, and this post's `public_post_permission_gate` pass. | HOLD_SOCIAL_STEAM_LINK - official Steam link only after the same gates pass. | `route_class = public_cta`; requires `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, and `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED` before post. | Steam URL not live through the publish gate, capsule not cold read, public post gate missing, or copy implies unsupported multiplayer scope or performance. |

### Pinned Post V0

Use only after the official Steam URL exists through `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, the exact links pass destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, and the pinned post has `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`.

```text
HECTON-8 is a single player deep sea survival game about pressure, salvage, machinery, and the cost of staying alive below the light.

Steam: HOLD_SOCIAL_STEAM_URL - fill only after `steam_page_publish_permission_gate` and `public_cta_permission_gate` pass.
Presskit: HOLD_SOCIAL_PRESSKIT_URL - fill only after `press_release_permission_gate` and `public_cta_permission_gate` pass.

Single player first scope. Performance details require measured build/hardware context.
```

### Cross Post Rule

Do not paste the same text everywhere. Keep the same facts, but change the ask:

  X/Bluesky: one blunt question.
  YouTube: clip retention and comments.
  Reddit: critique only, with dev disclosure and no external CTA unless same-day platform rules, the exact destination gate, and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` all pass.
  Steam: official update, no insecurity language.

## Pinned Post Template

```text
HECTON-8 is a single player first deep sea survival game about pressure, salvage, machinery, and the cost of staying alive below the light.

Steam: HOLD_SOCIAL_STEAM_URL - fill only after `steam_page_publish_permission_gate` and `public_cta_permission_gate` pass.
Presskit: HOLD_SOCIAL_PRESSKIT_URL - fill only after `press_release_permission_gate` and `public_cta_permission_gate` pass.
Discord: HOLD_SOCIAL_DISCORD_URL - fill only after `discord_open_permission_gate` and `public_cta_permission_gate` pass.
```

## Platform Cadence

Before screenshots:

  1-2 low risk dev/process posts per week;
  no wishlist begging;
  no daily empty posts.

After screenshots:

  2-3 posts per week;
  1 asset per post;
  one CTA max.

After demo:

  daily short support/update window for first week;
  then 2-3 posts/week.

## Post Families

| Family | Use |
|---|---|
| Screenshot hook | first visual identity. |
| 20s gameplay clip | creator/social conversion. |
| Tech/dev note | dev credibility. |
| Failure/fix note | shows honesty. |
| Steam announcement | owner controlled update. |
| Survey/feedback | only with real asset/build. |
| Patch notes | after demo/launch. |

## Social QA

Before posting:

  public_post_permission_gate is ALLOW_PUBLIC_POST_VERIFIED for this exact post;
  asset passed QA and asset metadata claim checks;
  official account custody and CTA link gates still pass;
  platform rules checked if community driven;
  CTA works;
  no duplicate copy across platforms;
  developer status is clear where needed;
  no fake player voice;
  no unsupported multiplayer-scope implication;
  no unproved performance claim.

## First 10 Public Social Posts

1. First in game machinery screenshot.
2. Pressure warning / hatch / leak screenshot.
3. "What is HECTON-8?" short thread.
4. Seed Ship anomaly teaser screenshot.
5. 15s salvage route clip.
6. Base pressure/failure clip.
7. Devlog: why pressure must be readable.
8. Steam page live announcement only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, and `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`.
9. Demo/playtest signup announcement after approved URL, signup custody, the exact demo/signup destination gate, and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` pass.
10. Known issues/feedback request after demo.

## Reply Rules

Use short direct replies.

### "Is multiplayer planned?"

```text
Current public scope is single player first. We will only talk about additional modes if they are real in the build.
```

### "Is this Subnautica?"

```text
It shares underwater survival adjacency, but the lane is different: pressure, machinery, salvage, corrosion, and deep sea noir.
```

### "Will it run on my PC?"

```text
We will publish performance details only with build, hardware, settings, and frame time context. No empty FPS promises.
```

## Current HECTON-8 Decision

Maintain candidate handle notes and prepare profile assets now. Do not register handles until `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`, and do not post until the exact `public_post_permission_gate` passes.
