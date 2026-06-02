# LOG_MONETIZATION_RESEARCH

## 2026-05-31 - Monetization Static Research

What was wrong:

- Monetization could be confused with store launch. The project is still pre-screenshot/G0, so selling now would create support debt before proof exists.
- Russia/Kazakhstan/crypto constraints were not captured in one decision map.
- A Kazakhstan card and VPN could be misread as a business solution. They are not. KYC, beneficiary match, tax, bank payout, sanctions review, and platform rules are the actual blockers.
- Crypto could be misused as a public checkout path. That route is high-risk and rejected.

What was done:

- Read authority/project/marketing docs and relevant mandates.
- Confirmed `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="MONETIZATION_RESEARCH">`; treated the user's direct prompt as the assignment.
- Checked current public platform/payment references for Steam, itch.io, Epic, GOG, Stripe, Patreon/Payoneer, Xsolla, and Russian digital-currency law.
- Created `Docs/Marketing/Monetization/MONETIZATION_RESEARCH_RU_KZ_CRYPTO_2026-05-31.md`.
- Linked the new monetization document from `Docs/Marketing/README.md`.
- Updated `Docs/Tasks/Status_MONETIZATION_RESEARCH.md`.
- Updated `Docs/AgentLogs/Rationale_MONETIZATION_RESEARCH.md`.

Cinematic Cheats used:

- No physical simulation was proposed.
- Marketing route forces the product proof toward cheap, readable visual decisions: instrument failure, pressure, route-risk, salvage cost, machine state.
- Rejected expensive realism language and public performance claims. Saved performance budget stays with the game.

Exact Microseconds saved:

- Runtime: 0 microseconds measured, because no runtime files were edited.
- Estimated runtime impact: 0 microseconds. Static documentation only.
- Process impact: not measured. No fake savings reported.

Route verdict:

- ADVANCE: custody setup, legal/bank feasibility, private waitlist/landing skeleton, proof-pack planning.
- HOLD: Steam page publication, itch.io paid build, RU-local support pages, creator outreach, press, paid ads, publisher/grant pitch.
- KILL: crypto storefront, VPN/KYC mismatch, grey-market key sellers, paid ads before proof, paid creators before gates, public performance claims.

Verification pending:

- Human legal/tax/bank/platform onboarding verification.

Validation results:

- Marketing file count: 86. Baseline was 85; the increase is the new linked monetization document and is intentional.
- CSV parse: OK, count 9.
- CRM rows/status: unchanged.
- Creator send-log fields: all empty.
- Asset metadata schema: required fields present.
- Forbidden-pattern scan: OK.
- Backtick path audit: two pre-existing wildcard archive references remain in `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`. New monetization doc is not part of the failure.
- `git diff --check`: OK for touched files. README line-ending warning only.

## 2026-05-31 - Pre-Screenshot Commercial Prep Packet

What was wrong:

- The user needed operational prep, not a report-only monetization analysis.
- Existing docs had the raw gates, but the pre-screenshot commercial setup path was split across inbox, social, owned-audience, Campaign 00, and backlog docs.
- The backlog also had two wildcard archive references that failed the marketing backtick path audit.

What was done:

- Added a pre-screenshot commercial prep order to `Docs/Marketing/PREP_DIRECTIONS_NOW.md`.
- Added commercial prep tasks and checklist to `Docs/Marketing/Campaigns/CAMPAIGN_00_PRE_SCREENSHOT_SETUP.md`.
- Added owner setup packet, vault item names, non-secret custody fields, and no-link holding page draft to `Docs/Marketing/Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md`.
- Added account reservation dry run and owner handoff packet to `Docs/Marketing/Social/SOCIAL_ACCOUNT_SETUP_AND_PLATFORM_PLAYBOOK.md`.
- Added draft form build packet to `Docs/Marketing/Audience/OWNED_AUDIENCE_EMAIL_AND_NEWSLETTER_PLAN.md`.
- Added P0 backlog rows MKT-P0-026 through MKT-P0-032 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`.
- Replaced two backtick-wrapped archive wildcard references in the backlog with plain text so the path audit can pass.

Cinematic Cheats used:

- No runtime work.
- Commercial prep is kept as no-link and proof-first. It buys future speed without spending current credibility.
- Public surfaces remain blocked until first real assets prove a readable player decision.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.
- Process time saved later: not measured; no fake savings reported.

Route verdict:

- ADVANCE: inbox custody packet, vault map, Steamworks feasibility packet, no-link holding page draft, owned audience form draft, social reservation dry run.
- HOLD: account creation, public site, signup form, social posting, Steam page, local support pages, itch paid build.
- KILL: account creation from personal sessions, public launch-looking placeholder, crypto checkout, VPN/KYC mismatch.

Validation results:

- Marketing file count: 86.
- CSV parse: OK, count 9.
- CRM rows/status: unchanged.
- Creator send-log fields: all 0.
- Asset metadata schema: required fields present.
- Forbidden-pattern scan: OK.
- Backtick path audit: OK.
- `git diff --check`: OK for touched files; line-ending warnings only.

## 2026-05-31 - Owner Browser / Gmail Authorization Pass

What was wrong:

- The owner wanted account work to proceed using personal Gmail/browser access, but direct account creation still requires secrets, 2FA/captcha, KYC, and custody rows that cannot be safely handled through docs/chat.

What was done:

- Opened browser tabs for Google account, Steam Direct, YouTube account, X signup, Bluesky, and Namecheap `hecton8.com` domain search.
- Ran public unauthenticated checks for YouTube, X, Reddit, Bluesky, and candidate domains.
- Recorded results in `Docs/Marketing/Social/SOCIAL_ACCOUNT_SETUP_AND_PLATFORM_PLAYBOOK.md`.
- Recorded owner-supplied personal Gmail accounts as candidate login/recovery inboxes only, without writing raw addresses into docs.
- Added the browser/secrets boundary to `Docs/Marketing/Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md` and `Docs/Marketing/PREP_DIRECTIONS_NOW.md`.

Cinematic Cheats used:

- No public surfaces opened. No fake launch page. Browser work was limited to onboarding surfaces and public checks.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: owner can complete browser login/2FA/captcha/KYC steps manually in opened tabs.
- HOLD: account creation status until post-registration custody rows are filled.
- KILL: storing passwords/2FA/backup codes/cookies in docs or chat.

## 2026-05-31 - Submerge Naming Boundary Pass

What was wrong:

- The owner selected `Submerge`, but exact-title conflict exists on Steam: app `4180620`, `Submerge`, by Neuron Activation.
- Existing marketing docs used `HECTON-8` as the public name everywhere, so a blind rename would damage file/asset continuity and hide the conflict.

What was done:

- Added `HOLD_NAMING_CONFLICT_REVIEW` to central brand, prep, website, social, monetization, and backlog surfaces.
- Set `Submerge` as public title candidate and `HECTON-8` as internal codename/legacy label until controlled rename.
- Added Submerge handle candidates: `PlaySubmerge`, `SubmergeGame`, `SubmergeH8`, `SubmergeBlackwater`, `SubmergeBelow`.
- Added domain candidates and DNS observations. DNS absence is recorded only as a registrar-check lead, not availability proof.
- Added backlog row `MKT-P0-033` for naming conflict review before public account/store/site publication.
- Opened browser tabs for Steam conflict and Namecheap domain checks.

Cinematic Cheats used:

- No runtime work.
- Name migration is staged through one brand gate instead of expensive doc churn and public identity damage.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: use `Submerge` in new public-title drafts.
- HOLD: naked `Submerge` store/social/site publication until conflict review.
- HOLD: domain purchases by owner directive; domain rows are reconnaissance only.
- KILL: blind global rename, naked-title registration, and public CTA under conflicted identity.

## 2026-05-31 - Existing X/Twitter Account Draft Pass

What was wrong:

- The owner says X/Twitter already exists, but the handle and custody fields are not recorded in project docs.
- The project still has no public screenshots/clips, Steam URL, or demo proof, so normal asset-led posting is not ready.

What was done:

- Opened X home, profile settings, login, and an intent composer in the owner browser.
- Staged a no-link pre-asset post draft:

```text
Working title: Submerge. A single-player deep-sea survival project about pressure, salvage, machinery, and black water. No Steam link yet. Public footage waits until current-build screenshots can prove the game without captions.
```

- Added X existing-account packet, profile fields, route class, and draft status to the social playbook.
- Added a narrow Campaign 00 allowance for owner-approved no-link account-reservation posts.

Cinematic Cheats used:

- No runtime work.
- The post avoids asset debt by refusing Steam/demo/screenshot claims until proof exists.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: no-link draft is ready in browser.
- HOLD: actual publish status until owner/browser completes the final visible action or a safe automation channel exists.
- KILL: blind keyboard posting, handle invention, Steam/wishlist CTA, demo claim, screenshot claim.

## 2026-05-31 - Multi-Platform Account Surface Pass

What was wrong:

- Account creation requests were broad, but the project lacks recorded handle, recovery, 2FA, captcha, phone, and KYC custody for non-X platforms.
- Existing HECTON-8 field kits did not yet provide a clean Submerge account setup matrix.

What was done:

- Opened YouTube channel/account, Bluesky, Reddit register, TikTok signup, Instagram signup, and Steam Direct surfaces in the owner browser.
- Added a Submerge multi-platform account field kit.
- Added a five-post no-link pre-asset bank for X/Bluesky-style dev notes.

Cinematic Cheats used:

- Account work is reduced to reusable field kits and opened browser surfaces until the owner completes platform gates.
- No public CTA and no asset claim before current-build proof.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: browser surfaces and field kits ready.
- HOLD: completion of accounts that require phone/captcha/2FA/KYC.
- KILL: blind signup automation, personal-session custody assumptions, hashtag spam before proof.

## 2026-05-31 - Temporary Submerge Text-Only Brand Pack

What was wrong:

- Account surfaces need avatar/banner/wordmark material, but there are no public-ready screenshots, capsule assets, or final logo.

What was done:

- Created temporary text-only SVG and PNG assets under `MarketingAssets/00_Brand`:
  - `Submerge_Avatar_TextOnly.svg`
  - `Submerge_Avatar_TextOnly_1024.png`
  - `Submerge_Banner_TextOnly_X.svg`
  - `Submerge_Banner_TextOnly_X_1500x500.png`
  - `Submerge_Wordmark_TextOnly.svg`
  - `Submerge_Wordmark_TextOnly_1800x500.png`
  - `README_Submerge_Temp_Brand.md`
- Added the avatar/banner paths to the social setup playbook.

Cinematic Cheats used:

- Text-only placeholder avoids pretending that concept art is gameplay.
- PNG exports make account setup usable on platforms that reject SVG.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: use for quiet profile setup if needed.
- HOLD: final logo/capsule/key art until naming review and asset proof.
- KILL: treating placeholders as Steam capsule, gameplay proof, or final visual identity.

## 2026-05-31 - Chat-Shared Password Boundary

What was wrong:

- The owner provided a password in chat and requested autonomous account creation/confirmation.
- Local Playwright is not installed, and current Edge is not exposed through a remote-debugging automation channel.
- The password value is exposed by being present in chat and is unsuitable for permanent public account custody.

What was done:

- Verified local browser automation state: `playwright:no`.
- Recorded a no-secret password boundary in the social playbook.
- Kept all password values out of docs/logs/scripts.

Cinematic Cheats used:

- No runtime work.
- Account prep continues through field kits, browser surfaces, and assets instead of risky secret handling.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: continue with visible browser forms, public fields, profile assets, and no-link drafts.
- HOLD: account completion at captcha/2FA/email/phone/KYC/payment gates.
- KILL: using a chat-shared password as permanent custody, storing secrets, blind keystroke automation, verification bypass.

## 2026-05-31 - Teni Games Studio Identity Pass

What was wrong:

- The owner created accounts as `Teni Games`, but the marketing docs still primarily treated accounts as `Submerge` or legacy `HECTON-8`.
- Public search found a near-name conflict: `TeNi Tiny Games` on Steam/itch/site.
- Japanese styling was requested, but kanji choices were unverified.

What was done:

- Added `Teni Games` as the studio/account speaker and kept `Submerge` as the game title candidate.
- Recorded `TeNi Tiny Games` as near-conflict and added separation rules.
- Added Japanese rendering guidance: preferred `テニゲームス`; kanji variants motif-only or avoid.
- Created temporary Teni Games text-only SVG/PNG avatar, banner, and wordmark.
- Opened X profile/intent, Bluesky, YouTube, Instagram edit, TikTok handle URL, Reddit user URL, Steam near-conflict, and itch near-conflict pages.

Cinematic Cheats used:

- Text-only studio assets avoid fake proof and avoid copying the near-name competitor.
- Kana rendering gives Japan-facing flavor without broken kanji branding.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: `Teni Games` as account/studio label candidate.
- HOLD: legal/public-credit use until owner verification.
- KILL: `TeNi` casing, `Tiny`, fake kanji logo, and using `転移` publicly.

## 2026-05-31 - Teni Human Voice And Title-Card Avatar

What was wrong:

- The first Teni visuals were usable but too generic for the user's desired Japanese/title-card direction.
- The post drafts were safe but still too much like official placeholder copy.
- The owner selected `天衣` and allowed `天意`; docs still treated kanji too cautiously for current visual experiments.

What was done:

- Recorded `天衣` as primary `ten'i` visual motif and `天意` as secondary providence/fate motif.
- Added Teni human-dev voice rules and a pre-screenshot post bank that avoids AI-marketing phrasing.
- Created title-card avatar variants:
  - `MarketingAssets/00_Brand/Teni_Games_Avatar_TitleCard_1024.png`
  - `MarketingAssets/00_Brand/Teni_Games_Avatar_TitleCard_B_1024.png`
  - `MarketingAssets/00_Brand/Teni_Games_Avatar_TitleCard_C_1024.png`
  - `MarketingAssets/00_Brand/Teni_Games_Avatar_TitleCard.svg`
- Selected `Teni_Games_Avatar_TitleCard_C_1024.png` as the current preferred social avatar.
- Copied a no-link human draft to clipboard and opened X intent plus Bluesky.

Cinematic Cheats used:

- Flat title-card typography and geometric fields replace fake concept art.
- Human process copy replaces launch-style hype while screenshots are not ready.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: use `Teni_Games_Avatar_TitleCard_C_1024.png` for current social profile setup.
- HOLD: final logo/legal identity/native Japanese brand review.
- KILL: exact anime-frame copying, fake gameplay art, "soon"/wishlist/hype posts, and kanji as platform username.

## 2026-05-31 - Teni Dark Wave Avatar Revision

What was wrong:

- The owner preferred the dark sonar/wave banner direction.
- The red title-card avatar was strong but less aligned with deep-sea noir.
- The non-circle-safe dark avatar risked losing important side details in X/Bluesky circular crops.

What was done:

- Created:
  - `MarketingAssets/00_Brand/Teni_Games_Avatar_DarkWave.svg`
  - `MarketingAssets/00_Brand/Teni_Games_Avatar_DarkWave_1024.png`
  - `MarketingAssets/00_Brand/Teni_Games_Avatar_DarkWave_CircleSafe_1024.png`
- Set `Teni_Games_Avatar_DarkWave_CircleSafe_1024.png` as the preferred current avatar in the social field kit and brand README.

Cinematic Cheats used:

- Sonar/wave linework and typography sell the identity without fake gameplay screenshots.
- Circle-safe crop keeps the mark readable on weak social surfaces.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: use dark-wave circle-safe avatar and dark-wave banner together.
- HOLD: final logo/native review/legal identity.
- KILL: using wide banner as avatar or relying on text near crop edges.

## 2026-05-31 - Background Account Queue And Focus-Safe Browser Ops

What was wrong:

- Account work was mixed with visible-browser actions, which risks focus theft and wrong-window input.
- Open Edge session still contains old X/Twitter intent URLs with copy the owner rejected.
- Public platform checks were not converted into a strict per-platform action queue.

What was done:

- Re-ran Edge session extraction without switching browser tabs:
  - `MarketingAssets/99_BrowserWork/edge_session_account_urls.json`
- Re-ran headless public profile checks:
  - `MarketingAssets/99_BrowserWork/public_profile_checks/public_profile_check_results.json`
- Created the account work queue:
  - `MarketingAssets/99_BrowserWork/account_work_queue_teni_games.json`
- Added a BrowserOps runbook:
  - `C:\hades\Tools\BrowserOps\README.md`
- Updated the social playbook with the current result:
  - Telegram `@teni_games`: public description is live.
  - X `@TeniGames`: public check currently says account does not exist; candidate only until logged-in profile flow confirms.
  - TikTok `@tenigames`: public check says account not found; candidate only.
  - YouTube/Instagram/Reddit/Bluesky: require logged-in owner-session verification.
- Refreshed the `Submerge` naming collision: exact Steam title `Submerge` is already live as app `4180620` by Neuron Activation with demo visibility and co-op horror positioning.
- Checked Edge process command line. No remote-debugging port is present, so true background mutation of logged-in pages is unavailable without deliberate browser relaunch/setup.
- Re-tested UIA Edge screenshot proof and rejected it: the saved image showed the active VS Code/Unity splash instead of Edge contents.
- Added a separate controlled Edge route for future work:
  - `C:\hades\Tools\BrowserOps\start_controlled_edge.ps1`
  - `C:\hades\Tools\BrowserOps\cdp_check.js`
  - port `9229`, isolated profile, no main-profile cookie extraction.

Cinematic Cheats used:

- Session-file extraction and headless public checks replace foreground browsing where possible.
- Prepared platform-specific assets/copy replace live posting while public proof assets are absent.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: continue background checks and queue maintenance.
- HOLD: logged-in profile edits unless the exact window is reliably screenshot-verified immediately before input. UIA screenshots are not accepted for Edge proof.
- KILL: old X intent URLs, blind UI automation, cookie extraction, and publish/send actions from shell automation.

## 2026-05-31 - Main Edge CDP Account Pass

What was wrong:

- Main Edge needed to be controlled through the owner's actual logged-in tabs, not a separate empty browser.
- Old X/Twitter intent tabs still contained rejected post drafts.
- Several services were open, but ownership/readiness was not objectively separated.

What was done:

- Saved Edge session state and restarted main Edge with:
  - `--remote-debugging-port=9222`
  - `--restore-last-session`
- Verified CDP endpoint:
  - `http://127.0.0.1:9222/json/version`
- Added direct CDP tooling:
  - `C:\hades\Tools\BrowserOps\teni_cdp_direct.js`
  - `C:\hades\Tools\BrowserOps\instagram_set_bio_cdp.js`
  - `C:\hades\Tools\BrowserOps\instagram_public_check_cdp.js`
- Navigated old X/Twitter intent tabs away from rejected drafts to `https://x.com/settings/profile`.
- Closed two duplicate X settings tabs created from old rejected intent drafts, leaving one X settings tab.
- Captured account state artifacts:
  - `MarketingAssets/99_BrowserWork/cdp_pages/cdp_account_pages.json`
  - per-platform PNG screenshots in `MarketingAssets/99_BrowserWork/cdp_pages/`
- Confirmed:
  - Telegram `@teni_games` is publicly present with saved description and avatar.
  - Instagram is logged in as `teni_games / Teni Games`.
  - Reddit `u/TeniGames` is not an existing visible user.
  - TikTok `@tenigames` is not found.
  - Bluesky is logged in, but Teni ownership is not proven.
  - YouTube Studio is logged in, but the visible channel is not Teni and must not be mutated.
- Attempted Instagram bio:
  - approved text: `Building Submerge. Pressure, machinery, salvage, black water.`
  - field set and Submit clicked through CDP;
  - public profile check still did not show the bio.

Cinematic Cheats used:

- CDP screenshots and page text replace focus-stealing manual inspection.
- Account-state matrix replaces blind account edits.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: Telegram confirmed; CDP route is usable for account state reads; X bad composer tabs cleaned.
- HOLD: Instagram bio/profile photo until a save route is proven; YouTube until a Teni channel is selected; Bluesky until profile ownership is visible.
- KILL: mutating personal YouTube channel, claiming Instagram save without public proof, and posting from X/Telegram before asset proof.

## 2026-05-31 - YouTube / Reddit Profile Conversion

What was wrong:

- YouTube was still carrying the old personal-channel public handle/profile surface.
- Reddit username was non-brand and immutable; display name alone did not make it a credible Teni account.

What was done:

- Converted YouTube public shell to `Teni Games`:
  - public URL: `https://www.youtube.com/@TeniGames`
  - handle: `@TeniGames`
  - description: `Teni Games is building Submerge: pressure, machinery, salvage, black water. Public footage waits for current-build proof.`
  - dark-wave banner and Teni avatar applied.
- Adapted Reddit `u/Expert-Try8516`:
  - display name: `Teni Games`
  - about text: `Making Submerge. Deep water, pressure, ugly machinery, and getting back alive.`
  - dark-wave avatar and banner applied.
- Converted Bluesky profile:
  - public URL: `https://bsky.app/profile/teni-games.bsky.social`
  - display name: `Teni Games`
  - bio: `Making Submerge. Pressure, salvage, ugly machinery, black water.`
  - dark-wave avatar and banner applied.
- Added reusable direct-CDP helpers:
  - `C:\hades\Tools\BrowserOps\cdp_open_snapshot.js`
  - `C:\hades\Tools\BrowserOps\cdp_snapshot_match.js`
  - `C:\hades\Tools\BrowserOps\cdp_click_xy.js`
  - `C:\hades\Tools\BrowserOps\cdp_insert_text.js`
  - `C:\hades\Tools\BrowserOps\cdp_set_first_file_input.js`

Cinematic Cheats used:

- Used the dark-wave `Teni Games / Submerge` identity pack as a shell, not as gameplay proof.
- Kept public text short, concrete, and human instead of campaign copy.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: YouTube, Reddit, and Bluesky public profile shells are now usable.
- HOLD: YouTube videos/Shorts/community posts, Bluesky first post, Reddit subreddit posting, contact email, website links, and any Steam CTA.
- KILL: pretending Reddit username can be renamed, posting without community rules, and reporting platform changes without public screenshot proof.

## 2026-05-31 - Instagram Finalization

What was wrong:

- Instagram had a correct local edit form, but earlier public verification did not show the bio.
- The public profile still had an empty camera avatar, so the shell looked unfinished.

What was done:

- Verified public Instagram profile:
  - public URL: `https://www.instagram.com/teni_games/`
  - display name: `Teni Games`
  - bio: `Building Submerge. Pressure, machinery, salvage, black water.`
- Applied the dark-wave Teni avatar through the edit page file-input path.
- Re-verified public profile screenshot after the avatar update.
- Sent no posts, Reels, stories, DMs, links, or contact claims.

Cinematic Cheats used:

- Used a branded dark-wave avatar as account identity only, not as gameplay proof.
- Kept Instagram as a silent visual archive shell until real current-build material exists.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: Instagram public profile shell is now usable.
- HOLD: Instagram posts/Reels/stories and website/contact links.
- KILL: filler posts, fake screenshots, or claiming website/contact readiness from the Instagram shell.

## 2026-06-01 - X Profile Conversion

What was wrong:

- X account `@submerge_game` still had old campaign-style copy and non-Teni visuals.
- Old X intent composer routes were unsafe because the owner already deleted a bad post.

What was done:

- Converted X public shell:
  - public URL: `https://x.com/submerge_game`
  - display name: `Teni Games`
  - bio: `Making Submerge. Pressure, salvage, ugly machinery, black water.`
  - dark-wave avatar and banner applied.
- Kept the handle `@submerge_game` unchanged.
- Sent no posts, DMs, replies, follows, links, or contact claims.
- Added `C:\hades\Tools\BrowserOps\cdp_set_file_input_index.js` for indexed upload fields.

Cinematic Cheats used:

- Used brand shell art only; no fake gameplay, no fake Steam CTA.
- Replaced hype phrasing with a restrained dev-account bio.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: X public shell is usable as a quiet branded account.
- HOLD: handle rename, first post, website/contact links, Steam CTA.
- KILL: X intent URLs, filler first posts, and blind handle rename.

## 2026-06-01 - Root Public Text Guide

What was wrong:

- Public-copy rules were spread across chat corrections and platform docs.
- The previous rejected post showed the exact failure mode: internal checklist language pretending to be public dev voice.

What was done:

- Created root `textes.md` with mandatory rules for:
  - English-default official copy.
  - Human developer voice.
  - banned hype phrases.
  - evidence boundaries before Steam/demo/screenshots.
  - platform-specific writing rules.
  - examples, rewrites, repair pass, and publish checklist.
- Added a global `AGENTS.md` rule requiring agents to read root `textes.md` before writing advertising copy, social posts, bios, store copy, creator outreach, or other marketing text.

Cinematic Cheats used:

- Copy direction sells readable pressure, machinery, route risk, and black water instead of unsupported "realistic ocean" claims.
- The guide keeps brand voice usable before screenshots without pretending profile art is gameplay proof.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: future public text has one root authority.
- HOLD: first public post until a real asset or a genuinely useful dev note exists.
- KILL: AI-marketing tone, platform boilerplate, fake CTAs, and unsupported demo/Steam/release claims.

## 2026-06-01 - First Public Brand-Art Posts

What was wrong:

- Teni social shells were branded but empty.
- The user wanted to try posting with the dark-wave GIF/profile art, but the first post still had to avoid fake launch energy.

What was done:

- Wrote first post text to `MarketingAssets/99_BrowserWork/first_public_post_20260601.txt`:
  - `Teni Games is the name. Submerge is the game.`
  - `Pressure, salvage, ugly machinery, black water, and a way back that keeps getting worse.`
  - `This is profile art, not gameplay.`
- Published on X:
  - URL: `https://x.com/submerge_game`
  - media: `MarketingAssets/00_Brand/Animated/Teni_Games_DarkWave_Avatar_GlitchSubtle_512.gif`
  - proof: `MarketingAssets/99_BrowserWork/cdp_pages/x_public_after_first_post_20260601.png`
- Published on Bluesky:
  - URL: `https://bsky.app/profile/teni-games.bsky.social`
  - media: `MarketingAssets/00_Brand/PlatformExports/bluesky_profile_1000.png`
  - proof: `MarketingAssets/99_BrowserWork/cdp_pages/bsky_public_after_first_post_20260601.png`
- Added CDP helpers:
  - `C:\hades\Tools\BrowserOps\cdp_x_compose_media.js`
  - `C:\hades\Tools\BrowserOps\cdp_bsky_compose_media.js`

Cinematic Cheats used:

- Used brand/profile art only; text says it is not gameplay.
- Used the animated GIF only where accepted; used static PNG on Bluesky after GIF/video was blocked by email confirmation.
- No Steam link, demo claim, release claim, wishlist ask, performance claim, or competitor comparison.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.

Route verdict:

- ADVANCE: X and Bluesky now have first real posts.
- HOLD: Telegram/Instagram/YouTube posts until platform-specific media flow and better asset choice are verified.
- HOLD: Bluesky GIF/video until email confirmation.
- KILL: Reddit broadcast posting before subreddit rule checks.

## 2026-06-01 - Queue Runner And Second Dev Note

What was wrong:

- Manual social posting was too slow for repeated work.
- X/Bluesky helpers required media, so a plain human dev note still needed separate browser work.
- Posting more empty brand lines would produce noise instead of useful account history.

What was done:

- Patched CDP compose helpers to accept text-only posts with `media=none`:
  - `C:\hades\Tools\BrowserOps\cdp_x_compose_media.js`
  - `C:\hades\Tools\BrowserOps\cdp_bsky_compose_media.js`
- Added queue runner:
  - `C:\hades\Tools\BrowserOps\social_queue_run.js`
- Added dated queue:
  - `MarketingAssets/99_BrowserWork/social_post_queue_20260601.json`
- Published second dev note on X and Bluesky:
  - `Opened the accounts before the good shots are ready.`
  - `For now I am mostly deleting frames that look cool and say nothing.`
- Public proof:
  - X: `MarketingAssets/99_BrowserWork/cdp_pages/x_public_after_devnote_cutting_frames_20260601.png`
  - Bluesky: `MarketingAssets/99_BrowserWork/cdp_pages/bsky_public_after_devnote_cutting_frames_20260601.png`

Cinematic Cheats used:

- Text sells the screenshot standard and real dev process, not an unsupported feature claim.
- Held the slogan-like and screenshot-rule drafts until real assets can carry them.
- No Steam link, demo claim, release claim, wishlist ask, performance claim, or fake gameplay implication.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.
- Workflow: future X/Bluesky text or media posts now use one queue command with dry-run and proof reports.

Route verdict:

- ADVANCE: X and Bluesky now have 2 public posts each and a repeatable queue path.
- HOLD: Instagram first post until a reliable web/mobile posting proof path exists.
- HOLD: Reddit until subreddit-specific rules are checked.
- HOLD: YouTube until real video/short/community eligibility exists.
- KILL: bulk posting for volume before screenshots.

## 2026-06-01 - Marketing Thinking / Proof-First Cadence

What was wrong:

- Two public posts are enough account pulse before screenshots.
- A third text-only post would read as filler.
- The current bottleneck is not copy volume; it is a first proof asset that makes pressure, machinery, and return risk readable without a paragraph.

What was done:

- Re-read `textes.md`, `TASTE.md`, social playbook, post bank, and shotlist.
- Re-checked exact-title `Submerge` Steam conflict through public web source; conflict remains active.
- Added blocked future queue items:
  - `20260601_006_pressure_room_first_proof`
  - `20260601_007_pretty_ocean_kill_rule`
  - `20260601_008_salvage_tool_witness`
- Validated queue JSON with Node and scanned it for banned marketing phrases.

Cinematic Cheats used:

- Treated marketing as proof composition, not lore volume.
- Selected pressure-room/readability proof as the next likely public asset lane.
- Held ocean beauty, slogans, and salvage philosophy until images can carry them.

Exact Microseconds saved:

- Runtime: 0 microseconds measured.
- Estimated runtime impact: 0 microseconds.
- Marketing waste avoided: no extra public post burned before proof assets.

Route verdict:

- ADVANCE: next post should be asset-led, preferably pressure-room proof.
- HOLD: all new drafts until a current-build image/clip exists.
- HOLD: naked `Submerge` Steam/site/public campaign until naming conflict review.
- KILL: daily filler cadence before screenshots.
