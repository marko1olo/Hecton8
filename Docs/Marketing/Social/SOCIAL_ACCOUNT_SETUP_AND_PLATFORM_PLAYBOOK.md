# HECTON-8 Social Account Setup And Platform Playbook

Status: pre public social ops
Owner lane: Marketing / social publishing
Runtime impact: none

## Authority Boundary

Public voice routes through root `textes.md`. Quality, release, platform, Steam, wishlist, demo, performance, account, publish, and automation claims require `quality.md`, `release.md`, `platform.md`, and current proof artifacts. This file is an account and publishing plan only; account existence, handle candidates, browser/session notes, examples, or automation commands do not grant public posting, platform readiness, Steam, demo, or release claims.

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
| Telegram | P1 | Owner-created low-friction dev feed and asset drop channel. |
| TikTok | P2 | Short clip tests if vertical clips exist. |
| Reddit | P2 | Critique/listening, not broadcast spam. |
| Mastodon | P3 | Optional dev presence. |
| Instagram | P3 | Visual archive, lower priority for PC survival. |

## Handle Policy

Studio/account update 2026-05-31: owner created accounts as `Teni Games`. Treat `Teni Games` as the studio/account speaker and `Submerge` as the game title candidate.

Telegram update 2026-05-31: owner created `@teni_games`. Treat it as the current Telegram handle for Teni Games. Use English-only public copy and the dark-wave `天衣` identity.

Teni Games account order:

  `TeniGames`
  `TeniGamesStudio`
  `TeniGamesDev`
  `TeniGamesWorks`

Japanese account rendering:

  `テニゲームス`

Approved kanji motifs:

  `天衣` = `ten'i`, heavenly garment. Primary visual motif.
  `天意` = `ten'i`, divine will / providence. Secondary heavier motif; use sparingly.

Do not use `TeNi` mixed-case or `Tiny`; `TeNi Tiny Games` exists publicly as a separate indie game developer.

Naming update 2026-05-31: owner selected `Submerge` as the intended game name. Because another Steam app already uses exact title `Submerge`, naked `Submerge` handles are high-confusion candidates until `HOLD_NAMING_CONFLICT_REVIEW` clears.

Submerge candidate order:

  `PlaySubmerge`
  `SubmergeGame`
  `SubmergeH8`
  `SubmergeBlackwater`
  `SubmergeBelow`

Preferred:

  `Hecton8Game`
  `PlayHecton8`
  `Hecton8`

Keep a consistent candidate-handle list before public reveal. Actual reservation is blocked until `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`.

If a platform accepts `Submerge` itself, do not reserve or publish it without owner/legal/storefront approval. Exact-name confusion with the existing Steam app is a search/support risk, not a clever shortcut.

### 2026-05-31 Submerge Public Check Addendum V0

This check used public unauthenticated sources and DNS only. It is not a trademark, store-title, registrar, or logged-in handle availability proof.

Owner update: do not buy domains now. Domain rows below are naming/availability leads only; domain purchase state is `HOLD_NO_DOMAIN_PURCHASE`.

| Surface | Candidate | Result | Decision |
|---|---|---|---|
| Steam | `Submerge` | Existing app `4180620` by Neuron Activation, coming soon / Early Access planned, demo visible, online co-op / horror tags, AI-content disclosure on store page. | Exact naked title is blocked for now; require naming conflict review and likely modifier. |
| Domain DNS | `submerge.com` | Resolves to existing A records. | Treat as unavailable/high-cost unless registrar proves otherwise. |
| Domain DNS | `submergegame.com` | No A record in public DNS pass. | Registrar check required; good first domain candidate. |
| Domain DNS | `playsubmerge.com` | No A record in public DNS pass. | Registrar check required; good second domain candidate. |
| Domain DNS | `submerge.games` | No A record in public DNS pass. | Registrar check required; secondary candidate. |
| Domain DNS | `submerge.dev` | No A record in public DNS pass. | Registrar check required; dev-only candidate, weaker for players. |
| Domain DNS | `submergegame.dev` | No A record in public DNS pass. | Registrar check required; dev-only backup. |
| Domain DNS | `submerge8.com` | No A record in public DNS pass. | Registrar check required; internal-codename bridge. |
| Domain DNS | `submergeh8.com` | No A record in public DNS pass. | Registrar check required; lower-priority bridge. |
| Domain DNS | `submergebelow.com` | No A record in public DNS pass. | Registrar check required; title-modifier candidate. |
| Domain DNS | `submergebelowthelight.com` | No A record in public DNS pass. | Registrar check required; strong mood, long URL. |
| Domain DNS | `submergeblackwater.com` | No A record in public DNS pass. | Registrar check required; strong identity, narrower tone. |

New reservation pack if naming review clears `Submerge` route:

```text
Project public title candidate: Submerge
Internal codename: HECTON-8
Preferred handle: PlaySubmerge
Backup handle: SubmergeGame
Conflict-safe handles: SubmergeH8, SubmergeBlackwater, SubmergeBelow
Display name: Submerge
Short bio: Single player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth.
Website: HOLD_NO_PUBLIC_CTA
Contact: HOLD_NO_PROJECT_INBOX_PUBLICATION
Profile state: private/blank until first screenshot pack passes QA
Abort if platform forces first post before public_post_permission_gate allows it.
```

### 2026-05-31 Teni Games Public Check Addendum V0

This check used public search results only. It is not trademark clearance.

| Surface | Name | Result | Decision |
|---|---|---|---|
| Steam developer | `TeNi Tiny Games` | Existing Steam developer page with one VR escape-room game and 6 followers observed in public store view. | Near-name conflict. Avoid `TeNi` casing and `Tiny`. |
| itch.io | `TeNi Tiny Games` | Existing itch.io creator page; public snippet says it is a 2-person Copenhagen team. | Near-name conflict. Keep `Teni Games` visually separate. |
| Web/site | `TeNi Tiny Games` | Existing Wix site and public developer contact in snippets. | Do not imitate layout/copy. |
| Exact search | `Teni Games` | No stronger exact studio conflict found in this pass, but search is not legal clearance. | Usable as public label candidate with caution. |

Source links:

- Steam developer: `https://store.steampowered.com/developer/TeNiTinyGames`
- itch.io creator: `https://teni-tiny-games.itch.io/`
- website: `https://tenitinygames.wixsite.com/ttgames`
- `天意` dictionary: `https://jlearn.net/dictionary/%E5%A4%A9%E6%84%8F`
- `転移` dictionary: `https://www.nihongomaster.com/japanese/dictionary/word/67478/ten%27i-%E8%BB%A2%E7%A7%BB-%E3%81%A6%E3%82%93%E3%81%84`
- `天衣` dictionary: `https://japaneseenglish.aliendictionary.com/en/meaning/%E5%A4%A9%E8%A1%A3.html`
- `手` dictionary: `https://jlearn.net/dictionary/%E6%89%8B`

Studio profile field kit:

| Field | Value |
|---|---|
| Display name | `Teni Games` |
| Japanese display variant | `Teni Games / 天衣` |
| Japanese bio line | `テニゲームス / 天衣 / 天意` |
| Bio short | `Small game studio. Building Submerge: beautiful alien water, pressure, machinery, salvage, black-water depth.` |
| Bio no-game-link | `Building Submerge. No Steam link until the current build can carry the screenshot.` |
| Website | blank until official CTA gate |
| Contact | blank until official inbox custody gate |
| Static avatar | `MarketingAssets/00_Brand/Teni_Games_Avatar_DarkWave_CircleSafe_1024.png` |
| Animated avatar | `MarketingAssets/00_Brand/Animated/Teni_Games_DarkWave_Avatar_GlitchSubtle_512.gif` |
| Banner | `MarketingAssets/00_Brand/Teni_Games_TenI_Banner_X_1500x500.png` |
| Telegram handle | `@teni_games` |

Platform-sized export candidate pack, not platform readiness proof:

- `MarketingAssets/00_Brand/PlatformExports/teni_games_profile_fields.json`
- `MarketingAssets/00_Brand/PlatformExports/platform_exports_contact_sheet.png`
- `MarketingAssets/00_Brand/PlatformExports/x_avatar_400.png`
- `MarketingAssets/00_Brand/PlatformExports/x_banner_1500x500.png`
- `MarketingAssets/00_Brand/PlatformExports/youtube_profile_800.png`
- `MarketingAssets/00_Brand/PlatformExports/youtube_banner_2560x1440_safe.png`
- `MarketingAssets/00_Brand/PlatformExports/instagram_profile_1080.png`
- `MarketingAssets/00_Brand/PlatformExports/tiktok_profile_1024.png`
- `MarketingAssets/00_Brand/PlatformExports/reddit_profile_256.png`
- `MarketingAssets/00_Brand/PlatformExports/reddit_banner_1600x480.png`

Studio no-link post draft:

```text
I opened the studio account before the screenshots are ready because leaving the name empty felt worse.

Submerge is the game.
Right now most of the work is cutting underwater shots that look cool and explain nothing.
```

Do not use kanji as the platform username. `天衣` and `天意` are allowed in the visual identity, banner, avatar, profile description, and occasional posts.

### 2026-06-01 First Public Brand-Art Post

Published text:

```text
Teni Games is the name. Submerge is the game.

Beautiful alien water, pressure, salvage, ugly machinery, black-water depth, and a way back that keeps getting worse.

This is profile art, not gameplay.
```

Published surfaces:

| Platform | Public URL | Media | Proof |
|---|---|---|---|
| X | `https://x.com/submerge_game` | `MarketingAssets/00_Brand/Animated/Teni_Games_DarkWave_Avatar_GlitchSubtle_512.gif` | `MarketingAssets/99_BrowserWork/cdp_pages/x_public_after_first_post_20260601.png` |
| Bluesky | `https://bsky.app/profile/teni-games.bsky.social` | `MarketingAssets/00_Brand/PlatformExports/bluesky_profile_1000.png` | `MarketingAssets/99_BrowserWork/cdp_pages/bsky_public_after_first_post_20260601.png` |

Bluesky GIF/video upload is held until account email confirmation. Reddit remains hold until subreddit rule checks. Instagram/YouTube/Telegram posts need separate platform-specific media proof before use.

### Teni Human Dev Voice V0

The account should read like a developer with dirt under the nails, not a brand scheduler.

Owner correction 2026-05-31:

- public posts are English-only unless a specific Japanese-language post is requested;
- remove sterile phrasing like "studio account is being wired";
- remove internal legal-gate phrasing like "no demo claim";
- do not post Russian from official Teni/Submerge channels;
- keep the first-person/dev tone blunt and ordinary.

Rules:

- one concrete thing per post;
- no "we are excited";
- no "soon";
- no "stay tuned";
- no "revolutionary";
- no fake vulnerability thread;
- no hashtags before real footage;
- no Steam/wishlist CTA before the gate;
- no Japanese text dump unless the post actually needs it.

Good pre-screenshot post bank:

```text
Submerge has a simple rule for screenshots:
if the image does not tell you what the player should worry about, it is not ready.
```

```text
The first screenshot has one job:
make a stranger ask "what do I do next?"

If the answer is "swim forward and admire the mood", we cut it.
```

```text
I don't want Submerge to sell a pretty ocean.
Pretty ocean is cheap.

The shot has to show pressure, a machine, and a return route that already looks like a mistake.
```

```text
Teni = テニ.
天衣 is the mark now. Heavenly garment, but we are dragging it into black water and rust.
天意 stays as the heavier version: providence, bad luck, something deciding before you do.
```

```text
Still killing screenshots that look like mood boards.

If the player decision is not visible, the image is decoration.
```

```text
Trying to make black water readable without making it look safe.

That is most of the job right now.
```

```text
The current enemy is not a monster.
It is screenshots that look cool and explain nothing.
```

```text
天衣 is the mark.
Submerge is the game.
Everything else has to earn the screenshot.
```

Telegram-first description:

```text
Submerge development notes from Teni Games.
Beautiful alien water, pressure, machinery, salvage, black-water depth.
No fake trailers. No wishlist begging. Screenshots have to earn the post.
```

## 2026 05 19 Handle Reservation Work Order

Do candidate checks quietly before public screenshots. Do not register, reserve, or post unless `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`; if a platform requires an initial placeholder before custody is complete, abort instead of creating an orphaned surface.

| Platform | Preferred handle order | Public state now | Required owner record |
|---|---|---|---|
| YouTube | `@Hecton8Game`, `@PlayHecton8`, `@Hecton8` | Private/empty channel acceptable. | Owner email, recovery email, 2FA owner, brand asset path. |
| X/Twitter | `@Hecton8Game`, `@PlayHecton8`, `@Hecton8` | Locked/private or blank profile acceptable. | Login owner, 2FA, backup codes, reserved date. |
| Bluesky | `@hecton8game.*`, `@playhecton8.*`, `@hecton8.*` | Blank is acceptable. | Handle domain, recovery owner. |
| Telegram | `@teni_games` | Owner-created. Keep English-only profile and dev-feed copy. | Login owner, 2FA/passcode status, recovery phone/email custody, channel/admin ownership. |
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

## 2026-05-31 Account Reservation Dry Run V1

Prepare this now; do not create accounts until the gate changes.

| Step | Work | Pass state |
|---:|---|---|
| 1 | Select approved handle order. | `Hecton8Game` first, `PlayHecton8` second, no novelty handle without owner approval. |
| 2 | Prepare display fields. | Display name, short bio, long bio, blank location, no public links. |
| 3 | Prepare vault destinations. | One named vault item per platform before account creation. |
| 4 | Prepare recovery and 2FA owner. | Owner-controlled recovery, 2FA method, backup-code destination ready. |
| 5 | Prepare profile state. | Private/blank if possible; otherwise abort if a public profile or first post is forced. |
| 6 | Prepare post-registration row. | Platform, handle, URL, login alias, vault item, recovery check, 2FA, backup code, visibility, status. |

No public account should look like a launch surface before first asset proof. A blank/private reserved handle is acceptable only after custody. A public profile with no assets, no contact custody, and no Steam page is noise.

### 2026-05-31 Public Headless Recheck V2

This pass used public unauthenticated requests only. No login, cookie extraction, browser-profile inspection, credential entry, or account creation occurred.

| Surface | Candidate | Result | Decision |
|---|---|---|---|
| YouTube | `@hecton8game` | PowerShell public fetch returned no usable response. | Inconclusive; confirm in logged-in owner flow. |
| YouTube | `@playhecton8` | PowerShell public fetch returned no usable response. | Inconclusive; confirm in logged-in owner flow. |
| YouTube | `@hecton8` | PowerShell public fetch returned no usable response in this pass; earlier public check treated it as taken/unrelated. | Avoid unless owner flow proves otherwise. |
| X/Twitter | `@Hecton8Game` | Public fetch returned no usable response. | Inconclusive; confirm in logged-in owner flow. |
| X/Twitter | `@PlayHecton8` | Public fetch returned no usable response. | Inconclusive; backup only. |
| X/Twitter | `@Hecton8` | Public fetch returned no usable response in this pass; earlier public check treated it as taken/unrelated. | Avoid unless owner flow proves otherwise. |
| Reddit | `u/Hecton8Game` | Public JSON fetch returned HTTP 403. | Inconclusive; confirm in browser owner flow. |
| Reddit | `u/PlayHecton8` | Public JSON fetch returned HTTP 403. | Inconclusive; backup only. |
| Bluesky | `hecton8game.bsky.social` | `resolveHandle` returned HTTP 400/not resolved. | Candidate only; confirm during account creation. |
| Bluesky | `playhecton8.bsky.social` | `resolveHandle` returned HTTP 400/not resolved. | Backup candidate. |
| Domain DNS | `hecton8.com`, `hecton8game.com`, `playhecton8.com`, `hecton8.dev`, `hecton8.games`, `hecton8.ru` | No A record found by DNS query. | Registrar check required; DNS absence is not ownership proof. |

### 2026-05-31 Owner Browser Tabs Opened

The agent opened the following owner-browser surfaces for manual owner login/onboarding:

- Google account selector;
- Steam Direct / Steamworks onboarding;
- YouTube account page;
- X/Twitter signup;
- Bluesky app;
- Namecheap domain search for `hecton8.com`.

No account was created by the agent. Registration remains held until the owner completes secrets/2FA/captcha steps and the post-registration custody row is filled.

### 2026-05-31 X/Twitter Existing Account Work Packet V0

Owner update: an X/Twitter account already exists. The handle and custody fields are not recorded in docs. Do not invent the public URL.

Agent action:

- opened X home;
- opened X profile settings;
- opened an X/Twitter intent composer with the no-link draft below;
- did not click publish from shell automation.

Post status: `POST_DRAFT_OPENED_OWNER_BROWSER`.

Route class if posted by owner: `forced_reservation_no_link`.

Post draft:

```text
Working title: Submerge. A single-player underwater survival project about beautiful alien water, pressure, salvage, machinery, and black-water depth. No Steam link yet. Public footage waits until current-build screenshots can prove the game without captions.
```

Why this draft is the least bad pre-asset post:

- no Steam link;
- no wishlist ask;
- no release window;
- no screenshot/demo claim;
- no performance claim;
- no competitor attack;
- names `Submerge` as working title to reduce exact-title conflict risk.

Profile field draft for the existing account:

| Field | Value |
|---|---|
| Display name | `Submerge` |
| Bio | `Single-player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth. Public footage only when captured from the current build.` |
| Website | blank until Steam/site CTA gate |
| Location | blank |
| Avatar | `MarketingAssets/00_Brand/Submerge_Avatar_TextOnly_1024.png` |
| Banner | `MarketingAssets/00_Brand/Submerge_Banner_TextOnly_X_1500x500.png` |
| Pinned post | hold until first screenshot/clip proof |

If the account is personal or mixed-use, do not post from it as official unless the owner accepts that it becomes a visible project channel. If the account is not project-owned, keep it as founder/dev account and disclose that voice plainly.

### 2026-05-31 Multi-Platform Account Surface Pass V0

Agent opened these browser surfaces for owner-controlled account setup/preflight:

- YouTube channel switcher;
- YouTube account page;
- Bluesky app;
- Reddit register;
- TikTok signup;
- Instagram signup;
- Steam Direct.

No account creation was completed by the agent. Reasons to stop before completion:

- platform may require phone, captcha, 2FA, recovery email, or payment/KYC;
- exact handle acceptance is visible only in logged-in UI;
- profile ownership and backup-code custody must be owner-controlled;
- blind keyboard automation can bind the wrong account or post from the wrong profile.

Submerge account field kit:

| Platform | Display name | First handle | Backup handle | Bio |
|---|---|---|---|---|
| X/Twitter | `Submerge` | `PlaySubmerge` | `SubmergeGame` | `Single-player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth. Public footage waits for current-build proof.` |
| Bluesky | `Submerge` | `playsubmerge.bsky.social` | `submergegame.bsky.social` | `Single-player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth.` |
| YouTube | `Submerge` | `@PlaySubmerge` | `@SubmergeGame` | `Official development channel for Submerge, a single-player underwater survival project about beautiful alien water, pressure, salvage, machinery, and black-water depth.` |
| Telegram | `Teni Games / 天衣` | `@teni_games` | hold | `Submerge development notes from Teni Games. Beautiful alien water, pressure, machinery, salvage, black-water depth. Screenshots have to earn the post.` |
| TikTok | `Submerge` | `@playsubmerge` | `@submergegame` | `Underwater survival. Beautiful water, pressure, salvage, machinery, black-water depth.` |
| Instagram | `Submerge` | `@playsubmerge` | `@submergegame` | `Single-player underwater survival: beautiful alien water, pressure, salvage, machinery, black-water depth.` |
| Reddit | owner/dev account | `u/PlaySubmerge` | `u/SubmergeGame` | `Developer account for Submerge. Posts disclose dev status.` |

No website/contact field until official inbox and CTA gates pass. No avatar/banner unless it is text-only or a current approved logo mark; do not use concept art as proof.

Temporary profile art paths:

- `MarketingAssets/00_Brand/Submerge_Avatar_TextOnly_1024.png`;
- `MarketingAssets/00_Brand/Submerge_Banner_TextOnly_X_1500x500.png`;
- `MarketingAssets/00_Brand/Submerge_Wordmark_TextOnly_1800x500.png`.

These are placeholder account visuals only. They are not Steam capsule art, gameplay proof, final logo, or final key art.

Pre-asset no-link post bank:

```text
Working title: Submerge. A single-player underwater survival project about beautiful alien water, pressure, salvage, machinery, and black-water depth. No Steam link yet. Public footage waits until current-build screenshots can prove the game without captions.
```

```text
If a screenshot needs a paragraph to explain what the player does, it fails. Submerge public footage waits until pressure, machinery, and the next decision read from the image itself.
```

```text
The lane is not only colorful ocean wonder. Submerge must show beautiful alien surface/shallows when the asset earns it, then make the drop into pressure, machinery, and black water matter.
```

```text
No Steam link yet. First public assets need to prove a readable pressure problem from the current build, not a promise.
```

```text
Submerge is single-player-first. The useful fear is not a random jump scare; it is a bad reading, a bad route, and a machine that may not buy enough time.
```

Use one post at a time. Do not thread all of them. Do not add hashtags until the first real asset exists.

### 2026-05-31 Chat-Shared Password Boundary V0

Owner supplied a password in chat for account work. Do not write the password value into docs, scripts, logs, shell history, screenshots, browser notes, or account records.

Verdict: `REJECT_CHAT_SHARED_PASSWORD_FOR_PERMANENT_ACCOUNT_CUSTODY`.

Reason:

- any password posted in chat must be treated as exposed;
- the proposed password is weak for real public accounts;
- platform accounts should use a password manager generated secret plus 2FA and backup-code custody;
- account recovery and 2FA cannot be validated from shell-only automation.

Allowed:

- use browser forms already opened by the owner;
- paste approved public text fields;
- prepare assets, bios, handles, and post drafts;
- ask owner to complete visible captcha/2FA/email/phone/KYC steps in the browser.

Blocked:

- storing the chat-shared password;
- using it for permanent official accounts;
- bypassing captcha/2FA/email confirmation;
- accepting KYC/payment/Steamworks terms blindly;
- blind keyboard automation into the active browser window.

### Reservation Pack To Hand To Owner

```text
Project: HECTON-8
Preferred handle: Hecton8Game
Backup handle: PlayHecton8
Display name: HECTON-8
Short bio: Single player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth.
Website: HOLD_NO_PUBLIC_CTA
Contact: HOLD_NO_PROJECT_INBOX_PUBLICATION
Profile state: private/blank until first screenshot pack passes QA
Abort if platform forces first post before public_post_permission_gate allows it.
```

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
| YouTube | `@TeniGames` | `https://www.youtube.com/@TeniGames` | OWNER_SESSION_PERSONAL_GOOGLE | `Teni Games / YouTube` | NO | NO | NO | public branded shell | HOLD_NO_FIRST_PUBLIC_ASSET_POST | PUBLIC_PROFILE_BRANDED |
| X/Twitter | `@submerge_game` | `https://x.com/submerge_game` | OWNER_SESSION_X | `Teni Games / X` | NO | NO | NO | public branded shell | HOLD_NO_FIRST_PUBLIC_ASSET_POST | PUBLIC_PROFILE_BRANDED |
| Bluesky | `teni-games.bsky.social` | `https://bsky.app/profile/teni-games.bsky.social` | OWNER_SESSION_BLUESKY | `Teni Games / Bluesky` | NO | NO | NO | public branded shell | HOLD_NO_FIRST_PUBLIC_ASSET_POST | PUBLIC_PROFILE_BRANDED |
| Telegram | `@teni_games` | `https://t.me/teni_games` | OWNER_SESSION_UNVERIFIED | UNRECORDED_NOT_CREATED | NO | NO | NO | private/blank until checked | HOLD_NO_FIRST_PUBLIC_ASSET_POST | OWNER_CREATED_UNVERIFIED |
| TikTok | UNRESERVED_NOT_CREATED | UNRECORDED_NOT_CREATED | HOLD_OFFICIAL_INBOX_CUSTODY | UNRECORDED_NOT_CREATED | NO | NO | NO | private/blank | HOLD_NO_FIRST_PUBLIC_ASSET_POST | NOT_CREATED |
| Instagram | `teni_games` | `https://www.instagram.com/teni_games/` | OWNER_SESSION_INSTAGRAM | `Teni Games / Instagram` | NO | NO | NO | public branded shell | HOLD_NO_FIRST_PUBLIC_ASSET_POST | PUBLIC_PROFILE_BRANDED |
| Reddit | `u/Expert-Try8516` | `https://www.reddit.com/user/Expert-Try8516/` | OWNER_SESSION_REDDIT | `Teni Games / Reddit` | NO | NO | NO | public branded disclosed dev | HOLD_SUBREDDIT_RULE_AND_PUBLIC_POST_GATE | PUBLIC_PROFILE_BRANDED |

### 2026-05-31 Background Public Profile Check V0

Tooling:

- `C:\hades\Tools\BrowserOps\edge_session_urls.py`
- `C:\hades\Tools\BrowserOps\public_profile_check.js`

Result snapshot:

| Surface | Public check result | Action |
|---|---|---|
| Telegram `@teni_games` | Public page shows Teni Games, 3 subscribers, saved English description. | ADVANCE as owned low-friction feed. |
| TikTok `@tenigames` | Public page says account not found. | Candidate; confirm in logged-in flow before claiming. |
| X `@TeniGamesDev` | Public page says account does not exist. | Candidate; confirm in logged-in flow. |
| X `@TeniGames` | Latest public check says account does not exist. | Candidate only; confirm inside logged-in X flow before claiming. |
| Instagram `tenigames` | Public check redirects to login. | Inconclusive; requires logged-in owner flow. |
| Reddit `u/TeniGames` | Public headless check blocked by Reddit network security. | Inconclusive; requires logged-in owner flow or API token. |
| Bluesky `tenigames.bsky.social` | Public page loads without clear ownership proof in headless text. | Inconclusive; requires logged-in owner flow. |
| YouTube Studio profile | Headless check redirects to Google sign-in. | Requires logged-in owner browser session. |

### 2026-05-31 Background Account Work Queue V1

Queue artifact:

- `MarketingAssets/99_BrowserWork/account_work_queue_teni_games.json`

Current operating rule:

- background-first: use Edge session extraction and headless public checks before touching a visible logged-in UI;
- logged-in mutations require a screenshot-verified target window immediately before any click or paste;
- old X/Twitter intent URLs are quarantined and must not be used;
- no publish/send action from shell automation.

Current platform order:

| Priority | Platform | State | Next useful action |
|---:|---|---|---|
| 1 | X | Public `https://x.com/submerge_game` shows Teni Games display identity, bio, avatar, and banner. | Hold posts; handle rename remains held until naming/handle review. |
| 2 | YouTube | Public `https://www.youtube.com/@TeniGames` resolves and shows Teni Games identity. | Hold videos/community posts until first proof asset; owner must enable 2-Step Verification. |
| 3 | Instagram | Public `https://www.instagram.com/teni_games/` shows Teni Games bio and dark-wave avatar. | Hold posts/Reels/stories until first proof asset; web link editing remains mobile-only. |
| 4 | TikTok | Public `@tenigames` says account not found. | Confirm handle in logged-in flow; do not claim reservation from public not-found. |
| 5 | Reddit | Public `https://www.reddit.com/user/Expert-Try8516/` shows Teni Games display name, about text, avatar, and banner. | Use as disclosed dev/listening account only; no subreddit posting before rule checks. |
| 6 | Bluesky | Public `https://bsky.app/profile/teni-games.bsky.social` shows Teni Games display name, bio, avatar, and banner. | Hold first post until proof asset exists and exact composer text is screenshot-verified. |
| 7 | Telegram | `@teni_games` public page shows saved description. | Hold posts unless a compose-box screenshot verifies the exact text before send. |

### 2026-05-31 Main Edge CDP Account Pass V1

Main Edge was restarted with remote debugging on `127.0.0.1:9222` after saving the session snapshot. This allows background page reads and low-level CDP actions against the owner browser without blind UIA clicks.

Artifacts:

- `MarketingAssets/99_BrowserWork/cdp_pages/cdp_account_pages.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/youtube_studio_profile.png`
- `MarketingAssets/99_BrowserWork/cdp_pages/instagram_edit.png`
- `MarketingAssets/99_BrowserWork/cdp_pages/instagram_set_bio_result.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/instagram_public_check_result.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/reddit_tenigames.png`
- `MarketingAssets/99_BrowserWork/cdp_pages/bluesky_home.png`
- `MarketingAssets/99_BrowserWork/cdp_pages/tiktok_tenigames.png`
- `MarketingAssets/99_BrowserWork/cdp_pages/telegram_teni_games.png`

Results:

| Platform | CDP result | Action |
|---|---|---|
| X | Profile editor rendered for `@submerge_game`; display name, bio, avatar, and banner were updated through CDP and public proof was captured. | Keep away from composer. Do not rename handle until naming/handle review. |
| YouTube | Logged in; owner explicitly allowed converting the visible channel to Teni Games. Studio shows a 2-Step Verification warning. | Convert profile to Teni Games, but do not publish videos or add contact/site claims yet. |
| Instagram | Logged in as `teni_games` / `Teni Games`; later public verification shows bio and dark-wave avatar saved. | Treat as public branded shell. No posts/Reels/stories until proof asset exists. |
| Reddit | Owner confirmed existing account `u/Expert-Try8516` is available and display name is already `Teni Games`. | Use this as the disclosed dev account; do not chase `u/TeniGames` for now. |
| Bluesky | Logged-in feed is visible, but Teni profile ownership is not proven. | Do not mutate until profile ownership/control is visible. |
| TikTok | `@tenigames` still shows account not found and GDPR transfer notice. | Candidate only; needs logged-in registration/profile flow. |
| Telegram | Public page shows avatar, display name, 3 subscribers, and saved English description. | Confirmed profile surface; no posts without compose proof. |

### 2026-05-31 YouTube / Reddit Conversion Pass V1

Proof artifacts:

- `MarketingAssets/99_BrowserWork/cdp_pages/youtube_public_at_TeniGames_after_publish.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/youtube_public_at_TeniGames_after_publish.png`
- `MarketingAssets/99_BrowserWork/cdp_pages/reddit_public_expert_try8516_after_banner.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/reddit_public_expert_try8516_after_banner.png`

Results:

| Platform | Public result | Hold |
|---|---|---|
| YouTube | Public channel is now `Teni Games` at `https://www.youtube.com/@TeniGames`, with handle `@TeniGames`, English Submerge description, dark-wave banner, and Teni avatar. | No videos, Shorts, community posts, website, or contact email until proof assets and custody gates exist. Studio still warns that 2-Step Verification is not enabled. |
| Reddit | Existing account is now branded as `Teni Games` at `https://www.reddit.com/user/Expert-Try8516/`, with dark-wave avatar/banner and about text: `Making Submerge. Deep water, pressure, ugly machinery, and getting back alive.` | Username remains `u/Expert-Try8516`; do not chase a rename. No subreddit posting/comment outreach until each community rule page is checked. |
| Bluesky | Profile is now branded as `Teni Games` at `https://bsky.app/profile/teni-games.bsky.social`, with dark-wave avatar/banner and bio: `Making Submerge. Beautiful alien water, pressure, salvage, ugly machinery, black-water depth.` | No first post until a real proof asset exists and the exact composer text is screenshot-verified before send. |

### 2026-05-31 Instagram Finalization Pass V1

Proof artifacts:

- `MarketingAssets/99_BrowserWork/cdp_pages/instagram_public_teni_games_refresh_20260531_2318.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/instagram_public_teni_games_refresh_20260531_2318.png`
- `MarketingAssets/99_BrowserWork/cdp_pages/instagram_public_after_avatar_attempt_20260531_2342.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/instagram_public_after_avatar_attempt_20260531_2342.png`

Result:

| Platform | Public result | Hold |
|---|---|---|
| Instagram | Public profile is now `Teni Games` at `https://www.instagram.com/teni_games/`, with bio `Building Submerge. Beautiful alien water, pressure, machinery, salvage, black-water depth.` and dark-wave Teni avatar. | No posts, Reels, stories, links, or contact claims until current-build proof assets exist. Website/link edits are web-blocked by Instagram and require mobile. |

### 2026-06-01 X Profile Conversion Pass V1

Proof artifacts:

- `MarketingAssets/99_BrowserWork/cdp_pages/x_profile_text_update_result_20260531_2355.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/x_public_submerge_game_after_text_20260601_0000.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/x_public_submerge_game_after_assets_20260601_0011.json`
- `MarketingAssets/99_BrowserWork/cdp_pages/x_public_submerge_game_after_assets_20260601_0011.png`

Result:

| Platform | Public result | Hold |
|---|---|---|
| X | Public profile is now `Teni Games` at `https://x.com/submerge_game`, with bio `Making Submerge. Beautiful alien water, pressure, salvage, ugly machinery, black-water depth.`, dark-wave avatar, and dark-wave banner. | Handle remains `@submerge_game` until naming/handle review. No first post until current-build proof exists and exact composer text is screenshot-verified. |

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
Single player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth.
Public assets not live yet.
```

Minimum profile while private/quiet:

```text
HECTON-8
Single player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth.
Public assets not live yet.
```

Check for accidental Cyrillic/Latin character substitutions before publishing the name.

## 2026 05 19 Account Page Field Kit V0

Paste these fields only after `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`, the account exists under owner-controlled credentials, and the post-registration custody row has been filled.

| Field | Primary value | Backup value | Notes |
|---|---|---|---|
| Display name | `HECTON-8` | `HECTON-8 Game` | Keep hyphen consistent. |
| Username/handle | `Hecton8Game` | `PlayHecton8` | Do not use unrelated/taken `Hecton8` handles. |
| Short bio | `Single player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth.` | `Underwater survival. Beautiful water, pressure, salvage, machinery, black-water depth.` | Scope neutral; no FPS or competitor claim. |
| Long bio | `HECTON-8 is a single player deep sea survival game about pressure, salvage, machinery, and the Seed Ship anomaly.` | `Official HECTON-8 account. Public assets are released only when captured from the current build.` | Use on YouTube/About pages. |
| Location | blank | blank | Avoid fake studio geography. |
| Website | `[gated Steam URL after steam_page_publish + public_cta]` | `[gated presskit URL after press_release + public_cta]` | Do not link placeholders publicly. |
| Contact | `[owner-controlled project email]` | blank | Use only after inbox custody passes. |
| Avatar | approved logo mark | text only `HECTON-8` mark | Do not use concept art as proof. |
| Banner | approved in game screenshot/capsule | bright photic route or black-water machinery crop | Must pass asset QA. |

If a platform forces a first post after registration custody is already allowed, use only this no-link reservation placeholder and log it as a forced account-reservation artifact. If `public_post_permission_gate` is still held and the platform does not allow private/blank setup, abort registration instead.

```text
Official HECTON-8 account reserved.

Public gameplay assets are not live yet. HECTON-8 is single player underwater survival about beautiful alien water, pressure, salvage, machinery, and black-water depth.
```

Do not add a Steam link until `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, and UTM rules are ready. If this forced reservation post is used after custody is allowed, log it as `route_class = forced_reservation_no_link` and do not count replies as anything beyond `consent_provenance = public_comment`.

## Profile Bio Template

```text
HECTON-8   single player underwater survival about beautiful alien water, pressure, salvage, machinery, and the Seed Ship anomaly.
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
| X / Bluesky | `Single player underwater survival: beautiful alien water, pressure, salvage, machinery, and black-water depth. Official HECTON-8 account.` |
| YouTube | `HECTON-8 is a single player underwater survival game about beautiful water, pressure, salvage, machinery, and the Seed Ship anomaly. Official clips, trailers, and dev updates.` |
| TikTok / Shorts | `Underwater survival. Beautiful water, pressure, machinery, salvage, black-water depth. Official HECTON-8 clips.` |
| Instagram | `Official HECTON-8 visuals: beautiful alien water, pressure machinery, salvage, and black-water depth.` |
| Reddit profile | `Developer account for HECTON-8. Single player first deep sea survival; public posts disclose dev status.` |

### First Three Public Posts

Run only after `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`, `PLAN-SHOT-000`, `PLAN-SHOT-003`, one agency/decision proof clip from `PLAN-CLIP-001` or `PLAN-CLIP-003`, and `PLAN-CAPSULE-001` pass QA, asset metadata claim checks, non-pending `viewer_named_decision`, valid non-held `capture_verdict`, AB-009/KPI decision-read fields where the post claims gameplay/pressure/route-risk proof, and official link/custody gates. If no decision clip exists, keep the second public post held instead of replacing it with another mood still.

| Order | Platform | Asset | Copy | CTA | Reporting | Kill if |
|---|---|---|---|---|---|---|
| 1 | X / Bluesky | `PLAN-SHOT-000` | `First in game look at HECTON-8. The shallow water is beautiful. That is not the same thing as safe. Blunt read wanted: does this feel like a route in an underwater survival game, or just scenery?` | Feedback question only. | `route_class = no_link_feedback`; `consent_provenance = public_comment` only. | Replies mostly say "what do you do?", "generic pretty water", "AI/concept art", or no route cost. |
| 2 | YouTube Community / Short if available | `PLAN-CLIP-001` or `PLAN-CLIP-003` | `A pressure problem should read before the caption. If this clip needs explanation, it failed.` | Feedback question only. | `route_class = no_link_feedback`; `consent_provenance = public_comment` only. | First 3 seconds do not show action/consequence. |
| 3 | Steam News / X / Bluesky | `PLAN-CAPSULE-001` winner + Steam URL | HOLD_SOCIAL_STEAM_PAGE_LIVE_COPY - say HECTON-8 has an official Steam page only after `steam_page_publish_permission_gate`, destination-specific `public_cta_permission_gate`, and this post's `public_post_permission_gate` pass. | HOLD_SOCIAL_STEAM_LINK - official Steam link only after the same gates pass. | `route_class = public_cta`; requires `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, and `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED` before post. | Steam URL not live through the publish gate, capsule not cold read, public post gate missing, or copy implies unsupported multiplayer scope or performance. |

### Pinned Post V0

Use only after the official Steam URL exists through `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, the exact links pass destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, and the pinned post has `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`.

```text
HECTON-8 is a single player underwater survival game about beautiful alien water, pressure, salvage, machinery, and the cost of staying alive when the route drops below the light.

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

## 2026-06-01 Queue Posting State

Queue file:

  `MarketingAssets/99_BrowserWork/social_post_queue_20260601.json`

Runner:

  `node C:\hades\Tools\BrowserOps\social_queue_run.js <queue-json> dryrun READY x,bluesky 9222`

  `node C:\hades\Tools\BrowserOps\social_queue_run.js <queue-json> publish <item-id> x,bluesky 9222`

Published post 2 on X and Bluesky:

```text
Opened the accounts before the good shots are ready.
For now I am mostly deleting frames that look cool and say nothing.
```

Proof:

  X public proof: `MarketingAssets/99_BrowserWork/cdp_pages/x_public_after_devnote_cutting_frames_20260601.png`

  Bluesky public proof: `MarketingAssets/99_BrowserWork/cdp_pages/bsky_public_after_devnote_cutting_frames_20260601.png`

Current queue policy:

  Keep one or two human setup/dev notes public.
  Do not bulk-post slogans before screenshots.
  Use the queue for X/Bluesky only until other platform post flows are proven.
  Mark posted items as `PUBLISHED` to block accidental repeat.

## Current HECTON-8 Decision

Maintain candidate handle notes and prepare profile assets now. Do not register handles until `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`, and do not post until the exact `public_post_permission_gate` passes.
