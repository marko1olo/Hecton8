# Rationale_MONETIZATION_RESEARCH

Date: 2026-05-31
Status: COMPLETE_STATIC_RESEARCH_PENDING_ACCOUNT_VERIFICATION

## Decision 1

Problem: The user asked for monetization strategy, not runtime implementation. The project rules still require evidence discipline and marketing taste compliance.
Solution: Treat this as a marketing/business artifact. Use `Docs/Marketing` as the write target. Do not edit runtime code, settings, scenes, or project assets.
Rejected Alternatives: Creating monetization code, SDK hooks, or storefront integration stubs would be premature and outside the requested scope.
Scalability potential: Strategy must support a weak solo/dev-budget path first, then scale into Steam/press/wishlist overkill when proof artifacts exist.
Hardware Impact: 0 microseconds runtime impact. No Unity files touched.

## Decision 2

Problem: The request includes Russia/Kazakhstan/crypto/payment constraints, which are legally and operationally time-sensitive.
Solution: Use current web sources for platform/payment facts and separate legal/financial caveats from engineering/product recommendations.
Rejected Alternatives: Relying on remembered platform rules would risk stale or false guidance.
Scalability potential: Low tier = free/low-cost community and direct channels; middle = paid creator/outreach ops; high/ultra = publisher, storefront, event, and creator pipeline.
Hardware Impact: 0 microseconds runtime impact. Business-path analysis only.

## Decision 3

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="MONETIZATION_RESEARCH">`, so there is no cover-to-cover XML batch assignment to extract.
Solution: Treat the user's direct Russian request as the authoritative assignment and preserve the missing XML fact in status/reporting.
Rejected Alternatives: Borrowing another agent's batch prompt or inventing a hidden batch would contaminate domain ownership.
Scalability potential: Low/middle/high/ultra route analysis remains valid because it is scoped to Marketing/Business Strategy only.
Hardware Impact: 0 microseconds runtime impact. No Unity files touched.

## Decision 4

Problem: The Marketing folder forbids document sprawl, but no existing document covers non-Steam monetization combined with RF/KZ/crypto payout feasibility.
Solution: Create one dedicated document under `Docs/Marketing/Monetization/` and link it from `Docs/Marketing/README.md`.
Rejected Alternatives: Scattering the analysis across Steam, Budget, CreatorOutreach, and Legal docs would make the decision map harder to audit.
Scalability potential: Low tier gets a no-spend setup path; middle tier gets gated creator/Steam testing; high/ultra tiers get publisher, multi-store, and localization expansion only after proof.
Hardware Impact: 0 microseconds runtime impact. Static docs only.

## Decision 5

Problem: The user has crypto and VPN access, but using either as a public game-sales bypass creates legal, tax, sanctions, refund, and account-termination risk.
Solution: Mark public crypto checkout and VPN/KYC mismatch as KILL. Keep crypto only as a private treasury/donation topic pending legal/tax verification.
Rejected Alternatives: "Sell keys for crypto" and "register where the VPN says" were rejected because they are fragile, hostile to consumer support, and likely to poison platform trust.
Scalability potential: Cheap path stays clean with owned audience and support-only candidates; high/ultra path keeps Steam/Epic/GOG/publisher credibility intact.
Hardware Impact: 0 microseconds runtime impact.

## Decision 6

Problem: Monetization work can become fake progress if it starts with accounts, ads, or public links before proof assets.
Solution: Order the plan as custody -> legal/bank feasibility -> proof pack -> gated store/audience routes -> controlled sales.
Rejected Alternatives: Paid ads, paid creators, public Discord, and public storefronts before G0 proof were rejected because they convert weak assets into permanent support and reputation debt.
Scalability potential: Low = private setup; middle = measured wishlist/demo tests; high = creator/festival/publisher routes; ultra = multi-store/localized/paid scale after proof.
Hardware Impact: 0 microseconds runtime impact.

## Decision 7

Problem: Marketing End-Of-Change backtick path audit reports two pre-existing wildcard archive references in `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`.
Solution: Record the audit caveat instead of changing unrelated backlog history during a monetization research task.
Rejected Alternatives: Editing unrelated archived-reference backlog lines would widen scope and risk altering old marketing operations history.
Scalability potential: New monetization doc remains linked and audit-clean; unrelated archive wildcard cleanup can be handled by a maintenance pass.
Hardware Impact: 0 microseconds runtime impact.

## Decision 8

Problem: The owner asked to prepare everything possible before screenshots, but the control tower forbids new strategy sprawl and public/account actions without custody gates.
Solution: Patch existing operating docs: prep directions, Campaign 00, website/inbox custody, social account playbook, owned-audience plan, and backlog. No new strategy file, no account creation, no public route.
Rejected Alternatives: Creating more standalone planning documents, registering accounts from chat permission, or publishing placeholder surfaces would create orphaned credentials and fake readiness.
Scalability potential: Low tier now has project inbox/vault/no-link setup; middle tier can unlock Steam/waitlist/handles once gates pass; high/ultra tiers keep clean custody for publisher, store, and creator scale.
Hardware Impact: 0 microseconds runtime impact. Static docs only.

## Decision 9

Problem: The owner authorized use of personal Gmail accounts and visible browser work for account creation, but passwords, 2FA, backup codes, captcha, KYC, and bank/payment entry cannot be safely stored or handled through docs/chat.
Solution: Open browser onboarding surfaces and record personal Gmail only as owner-supplied candidate login/recovery inboxes, without writing raw addresses into docs. Keep public identity pointed toward a domain/project inbox and keep account gates held until owner completes secrets/2FA/captcha/KYC steps.
Rejected Alternatives: Extracting browser cookies, asking for passwords in chat, storing Gmail addresses as public contacts, or forcing account creation without custody rows would create account-loss and privacy risk.
Scalability potential: Low tier gets manual owner handoff now; middle/high/ultra tiers can scale accounts only after custody is clean.
Hardware Impact: 0 microseconds runtime impact.

## Decision 10

Problem: The owner selected `Submerge` as the game name, but an existing Steam app already uses exact title `Submerge`, which creates store-search, SEO, support, and possible legal confusion.
Solution: Record `Submerge` as the public title candidate, preserve `HECTON-8` as internal codename/legacy label, and add `HOLD_NAMING_CONFLICT_REVIEW` before any public account/store/site publication. Prepare modifier candidates and Submerge-specific handle/domain candidates without claiming availability.
Rejected Alternatives: Mass-renaming all HECTON-8 references immediately, reserving naked `Submerge` accounts, or publishing under a conflicted exact title would create avoidable identity debt.
Scalability potential: Low tier can reserve conflict-safe candidate handles after custody; middle tier can choose a Steam-safe suffix before page build; high/ultra tiers keep search, press, creator, and publisher identity consistent.
Hardware Impact: 0 microseconds runtime impact. Static docs only.

## Decision 11

Problem: Domain candidates exist, but the owner explicitly does not want to buy domains now.
Solution: Keep domain work as reconnaissance only and record `HOLD_NO_DOMAIN_PURCHASE`. Use domain results to inform naming risk, not spending.
Rejected Alternatives: Buying speculative domains or creating urgency from DNS absence would spend money before proof assets and naming review.
Scalability potential: Low tier preserves cash; middle/high/ultra tiers can buy the final domain only after naming/store route is clear.
Hardware Impact: 0 microseconds runtime impact. Static docs only.

## Decision 12

Problem: The owner says an X/Twitter account already exists and wants autonomous posting/account work, but the current shell context cannot verify the handle/custody or safely press publish inside the logged-in browser without a browser automation channel.
Solution: Open X home/profile/login/intent surfaces and stage a no-link pre-asset post draft that makes no Steam, demo, screenshot, release, performance, or competitor claim. Record status as `POST_DRAFT_OPENED_OWNER_BROWSER`.
Rejected Alternatives: Guessing the handle, posting through blind keyboard automation, or publishing a hype post without assets would create avoidable identity and trust damage.
Scalability potential: Low tier gets a safe account-reservation voice; middle tier can convert to screenshot-led posts after proof; high/ultra tiers keep social history free of unsupported claims.
Hardware Impact: 0 microseconds runtime impact. Static docs/browser opening only.

## Decision 13

Problem: The owner wants accounts created across platforms, but each signup can require phone, captcha, 2FA, recovery, payment/KYC, or logged-in handle confirmation.
Solution: Open account setup/preflight surfaces and prepare platform-specific Submerge field values. Stop before secrets/phone/KYC/captcha and avoid blind UI automation.
Rejected Alternatives: Blindly sending keystrokes into the visible browser, creating accounts with unrecorded custody, or using personal sessions as permanent official infrastructure would create account loss and reputation risk.
Scalability potential: Low tier has ready fields for manual completion; middle tier can reserve the same identity once custody passes; high/ultra tiers preserve clean ownership for publisher/store/press diligence.
Hardware Impact: 0 microseconds runtime impact. Static docs/browser opening only.

## Decision 14

Problem: Empty social profiles look low-trust, but concept art or fake screenshots would imply proof the project does not have.
Solution: Create text-only Submerge avatar/banner/wordmark placeholders in SVG and PNG. Label them as temporary profile visuals only.
Rejected Alternatives: AI-looking key art, fake screenshots, or Steam capsule-like art before current-build assets would poison first impressions.
Scalability potential: Low tier can use clean placeholders; middle tier replaces them with proof assets; high/ultra tiers can commission final logo/capsule after naming conflict review.
Hardware Impact: 0 microseconds runtime impact. Static asset files only.

## Decision 15

Problem: The owner supplied a password in chat and requested autonomous account creation, but local browser automation is not available and a chat-shared password is exposed by definition.
Solution: Treat the password as unsuitable for permanent account custody, do not store it, and keep account creation blocked at captcha/2FA/email/phone/KYC gates. Continue only with opened browser surfaces, public field kits, assets, and no-link drafts.
Rejected Alternatives: Writing the password to scripts/logs, blind browser keystrokes, bypassing verification, or accepting platform/KYC/payment terms without visible owner control would create account-loss and legal/platform risk.
Scalability potential: Low tier uses manual owner verification with generated secrets; middle/high/ultra tiers keep platform accounts recoverable for Steam, press, publisher, and support diligence.
Hardware Impact: 0 microseconds runtime impact. Static docs/browser state only.

## Decision 16

Problem: The owner created accounts under `Teni Games`, but public search finds a near-name game developer conflict, `TeNi Tiny Games`, on Steam and itch.
Solution: Treat `Teni Games` as studio/account label candidate, not legal name. Keep visual separation: no `TeNi` casing, no `Tiny`, no VR/escape-room wording, and no claims of legal registration. Use `Teni Games` for the speaker and `Submerge` for the game.
Rejected Alternatives: Ignoring the near-conflict, changing the game title to `Teni`, or merging studio/game identities would make search and support messier.
Scalability potential: Low tier keeps account identity usable; middle tier can formalize legal/public credit; high/ultra tiers keep publisher/press diligence cleaner.
Hardware Impact: 0 microseconds runtime impact. Static docs/assets/browser tabs only.

## Decision 17

Problem: The owner wants Japanese styling for `Teni`, but random kanji can create wrong meaning or cringe-brand risk.
Solution: Use `テニゲームス` as the safe phonetic Japanese rendering. Record `天意`, `天衣`, `手に`, `転移`, and `転位` as meaning notes only; avoid public kanji branding without native review.
Rejected Alternatives: Using `天意` as official logo text, using `転移` despite metastasis/transfer meaning, or inventing ateji would create avoidable language risk.
Scalability potential: Low tier gets safe kana; middle/high/ultra can commission native Japanese localization/branding before Japan-facing outreach.
Hardware Impact: 0 microseconds runtime impact. Static docs/assets only.

## Decision 18

Problem: The owner selected `天衣` as the main `ten'i` motif and allowed `天意`, while also requesting a Shaft/Monogatari-like avatar feel. Directly copying a recognizable anime title-card composition would create derivative/copyright and trust risk.
Solution: Use original flat title-card principles: cream paper, black ink, vermilion block fields, hard rectangles, asymmetric typography, large `天衣`, and secondary `天意`. Record `天衣` as primary visual motif and `天意` as secondary heavier fate/providence motif, not legal identity.
Rejected Alternatives: Copying exact Monogatari/Shaft frames, using character silhouettes, using `天意` as the main studio name, or keeping the earlier generic dark studio avatar as the preferred profile image.
Scalability potential: Low tier gets a readable avatar without screenshots; middle tier can keep this as account identity while gameplay screenshots arrive; high/ultra can later commission native-reviewed final brand/capsule work.
Hardware Impact: 0 microseconds runtime impact. Static SVG/PNG only.

## Decision 19

Problem: Pre-screenshot posts sounded too much like official placeholder copy and not enough like a real developer. That makes the account feel automated and weak before proof assets exist.
Solution: Add a human dev voice rule set and no-link drafts built around concrete admissions: no Steam link, no demo claim, first screenshot must explain pressure/machinery/return route without a paragraph.
Rejected Alternatives: Polished campaign copy, "we are excited", "stay tuned", hashtag filler, wishlist begging, and daily empty posting.
Scalability potential: Low tier uses sparse human process posts; middle tier converts to screenshot-led dev notes; high/ultra keeps account history credible for creators, publishers, and store visitors.
Hardware Impact: 0 microseconds runtime impact. Static copy/browser staging only.

## Decision 20

Problem: The owner preferred the dark wave/banner direction over the red title-card avatar. The earlier title-card asset was stylish, but it did not match the deep-sea noir/sonar identity as cleanly.
Solution: Create `Teni_Games_Avatar_DarkWave_1024.png` and a circle-safe `Teni_Games_Avatar_DarkWave_CircleSafe_1024.png`, then make the circle-safe asset the preferred profile avatar.
Rejected Alternatives: Keeping red title-card as preferred, using the wide banner as avatar, or placing important text near the avatar crop edge.
Scalability potential: Low tier gets a coherent social identity without gameplay screenshots; middle/high/ultra can replace only the asset layer later while keeping the `天衣 / 天意` brand language.
Hardware Impact: 0 microseconds runtime impact. Static SVG/PNG only.

## Decision 21

Problem: The owner wanted moving sine/wave identity and a slight glitch, but the mark still has to read at avatar size.
Solution: Generate a clean 48-frame dark-wave loop and a separate subtle-glitch variant. Export 512/1024 GIF for social surfaces and MP4/WebM for site/game use. Keep source frames and phase previews for review.
Rejected Alternatives: Heavy glitch that damages `天衣`, replacing the clean loop, or pretending the animation is gameplay footage.
Scalability potential: Low tier uses 512 GIF/MP4; middle uses 1024 web loops; high/ultra can reuse the source-frame pipeline for higher-fidelity site/game stingers without changing the mark.
Hardware Impact: 0 microseconds runtime impact. Offline raster/video assets only.

## Decision 22

Problem: Browser/account work was error-prone: a previous X draft was posted with bad copy, a screenshot attempt captured VS Code instead of Edge, and Telegram required logged-in UI work.
Solution: Remove the separate BrowserAgent experiment and create `C:\hades\Tools\BrowserOps` with screenshot-first Win32/UIA helpers. For Telegram, use `tg://resolve?domain=teni_games`, verify title/screenshot, edit only the channel description, then save after visual confirmation.
Rejected Alternatives: Blind coordinate clicking, continuing with a separate browser profile for owner-account work, or posting more public text without proof.
Scalability potential: Low tier uses screenshot-first manual-safe operations; middle/high/ultra can add CDP/Playwright only for non-account public pages or a deliberately launched controlled browser, not for blind owner-session mutation.
Hardware Impact: 0 microseconds runtime impact. Tooling only; no Unity runtime files touched.

## Decision 23

Problem: Official-channel language needed correction after owner rejected sterile/AI-sounding public copy and Russian official posts.
Solution: Enforce English-only public copy for Teni/Submerge surfaces, replace Telegram description with a short human dev line, and keep rough process posts concrete: pressure, machinery, salvage, black water, screenshots earning the post.
Rejected Alternatives: Russian official posts, legal-gate phrases like "no demo claim", "studio account wired" wording, hashtag spam, and daily empty posting.
Scalability potential: Low tier keeps the account credible before screenshots; middle/high/ultra can scale cadence only when real screenshot/clip proof exists.
Hardware Impact: 0 microseconds runtime impact. Copy and account profile only.

## Decision 24

Problem: The owner wants all open browser accounts processed without stealing desktop focus, but the main Edge process has no remote-debugging port and logged-in profile mutation cannot be done headlessly without cookie extraction, relaunching Edge, or blind UI automation.
Solution: Use a background-first account loop: extract URLs from Edge session files, run headless public profile checks, write a platform queue, quarantine old X/Twitter intent URLs, and reserve foreground clicks for screenshot-verified short bursts only. Record that logged-in YouTube/Instagram/X/Reddit/Bluesky profile mutation still requires the owner browser or an explicitly launched controlled browser.
Rejected Alternatives: Blind clicking, sending posts from shell automation, extracting cookies from the owner's browser, relaunching Edge with remote debugging without explicit permission, or treating public not-found/login-wall results as ownership proof.
Scalability potential: Low tier gets non-invasive account reconnaissance and ready fields; middle tier can execute short verified profile setup bursts; high/ultra tiers can move to API/CDP-backed operations only after deliberate custody and browser setup.
Hardware Impact: 0 microseconds runtime impact. Offline docs/tooling and headless public checks only.

## Decision 25

Problem: The UIA Edge screenshot helper reported an Edge window title but captured the active VS Code/Unity splash, making it unsafe as visual proof for profile editing.
Solution: Mark Edge UIA screenshot proof as untrusted. Use headless public checks and session extraction for background work; use only a foreground Win32 screenshot or CDP-controlled browser for logged-in Edge mutation.
Rejected Alternatives: Continuing with UIA screenshots as proof, clicking controls based on an accessibility title alone, or pasting profile text without seeing the exact page/field.
Scalability potential: Low tier avoids accidental wrong-window edits; middle/high/ultra can upgrade to reliable CDP/browser automation once account custody and browser setup are deliberate.
Hardware Impact: 0 microseconds runtime impact. Tool validation only.

## Decision 26

Problem: The owner asked whether the existing `Submerge` namesake matters. Latest public search confirms an exact Steam app `Submerge` by Neuron Activation, with demo visibility, online co-op / horror positioning, Early Access plan, and AI-content disclosure. That creates storefront, SEO, support, and first-impression collision risk.
Solution: Keep `Submerge` as working public title candidate but block naked-title Steam/site/campaign publication until naming conflict review chooses a modifier or accepts the risk explicitly. Prefer modifier candidates that preserve the word while separating the game: `Submerge: Blackwater`, `Submerge: Pressure Vessel`, `Submerge: Below the Light`, `Submerge Protocol`.
Rejected Alternatives: Treating the conflict as harmless, rushing naked `Submerge` accounts/pages, or hiding behind studio name alone while Steam search is already occupied by another game.
Scalability potential: Low tier can keep Teni account identity and private drafts; middle/high/ultra need a conflict-safe title before Steam, press, creator links, paid spend, and publisher diligence.
Hardware Impact: 0 microseconds runtime impact. Naming/business decision only.

## Decision 27

Problem: The owner wants faster browser/account work, but mutating the already-open main Edge session in the background is not available without CDP, cookie extraction, or focus-stealing UI automation.
Solution: Add a separate controlled Edge route: `start_controlled_edge.ps1` launches an isolated profile with remote debugging on port `9229`, and `cdp_check.js` verifies pages. Do not run it against the owner's main profile and do not inherit cookies.
Rejected Alternatives: Relaunching main Edge, copying profile cookies, or using hidden UIA clicks against the active desktop.
Scalability potential: Low tier can keep current manual sessions untouched; middle/high/ultra can move repeated account/profile operations into CDP automation after a deliberate login to the controlled profile.
Hardware Impact: 0 microseconds runtime impact. Local tooling only.

## Decision 28

Problem: The owner allowed restarting the browser if needed, and the open account tabs were registered by the owner, but the previous background methods could not safely mutate logged-in pages.
Solution: Restart main Edge with `--remote-debugging-port=9222 --restore-last-session` after saving the session snapshot, then use direct Chrome DevTools Protocol with websocket control. Navigate old X intent tabs away from rejected post drafts, capture per-platform screenshots/text, and only attempt the Instagram bio because the account identity clearly showed `teni_games / Teni Games`.
Rejected Alternatives: Leaving rejected X composer URLs open, using foreground clicks, mutating YouTube despite a non-Teni channel being visible, or claiming Instagram bio saved from local field state only.
Scalability potential: Low tier now has background-readable account state; middle tier can process safe fields when platform UI accepts CDP; high/ultra can move repeatable account setup into audited CDP scripts.
Hardware Impact: 0 microseconds runtime impact. Browser/account operations only.

## Decision 29

Problem: Instagram web edit showed `teni_games / Teni Games` and accepted the bio text locally, but the public profile did not show the bio after Submit attempts.
Solution: Mark Instagram bio as not saved. Keep the approved bio text ready, but require a foreground verified browser action, mobile app path, or another proven save route before recording completion.
Rejected Alternatives: Reporting success based on the edit textarea value, retrying blind submits, or posting content to compensate for incomplete profile setup.
Scalability potential: Low tier avoids false profile-readiness claims; middle/high/ultra can still apply the same bio once the platform accepts the save path.
Hardware Impact: 0 microseconds runtime impact. Profile verification only.

## Decision 30

Problem: The owner explicitly allowed converting the visible YouTube channel to Teni, but channel edits can show local Studio state before public URLs update.
Solution: Use direct CDP only after the logged-in Studio page was visible, then verify against public `https://www.youtube.com/@TeniGames`. Apply name, handle, description, banner, and avatar; do not publish posts/videos and do not add contact/site links.
Rejected Alternatives: Trusting Studio field state without public proof, leaving the old Cyrillic handle live, adding a fake website/contact, or posting an empty community update.
Scalability potential: Low tier gets a clean public channel shell; middle/high/ultra can add trailers, Shorts, devlogs, and creator proof later only after current-build assets exist.
Hardware Impact: 0 microseconds runtime impact. Browser/account metadata only.

## Decision 31

Problem: Reddit username `u/Expert-Try8516` is already the owner account and cannot be renamed into a brand handle.
Solution: Adapt the account around the immutable username: display name `Teni Games`, dark-wave avatar/banner, and a short human dev about line. Treat it as a disclosed dev account, not a stealth official subreddit marketing account.
Rejected Alternatives: Chasing `u/TeniGames`, deleting/recreating account state, or posting marketing into subreddits before reading community rules.
Scalability potential: Low tier can listen and comment under a disclosed dev identity; middle/high/ultra can use Reddit for critique threads only when proof assets and subreddit rule logs exist.
Hardware Impact: 0 microseconds runtime impact. Browser/account metadata only.

## Decision 32

Problem: Browser profile work needed repeatability without stealing focus or trusting misleading UIA screenshots.
Solution: Add small direct-CDP tools for opening/snapshotting URLs, snapshotting existing tabs, XY clicks after screenshot proof, text insertion, and file-input uploads.
Rejected Alternatives: Blind coordinate clicks, foreground window scraping as proof, or one-off fragile scripts embedded in command history.
Scalability potential: Low tier keeps current account work controlled; middle/high/ultra can reuse the same CDP utilities for batch profile checks and asset swaps without creating new account secrets.
Hardware Impact: 0 microseconds runtime impact. Local tooling only.

## Decision 33

Problem: Bluesky was logged in and showed owner controls for `teni-games.bsky.social`, but it was still visually empty and not converted.
Solution: Use public visible profile controls through CDP, set display name, short human Submerge bio, dark-wave avatar, and dark-wave banner, then verify the public profile page. Do not post.
Rejected Alternatives: Reading or using session tokens, posting a launch/status skeet, or claiming the profile before the public page showed the saved identity.
Scalability potential: Low tier gets a credible dev presence; middle/high/ultra can use Bluesky for screenshot-led feedback posts only after proof assets exist.
Hardware Impact: 0 microseconds runtime impact. Browser/account metadata only.

## Decision 34

Problem: Instagram initially showed the new bio in the edit form but not on the public page, and its profile photo upload path did not expose a normal visible file input until the edit page was reopened through CDP.
Solution: Wait for public persistence, verify the public bio, then apply the dark-wave avatar through `DOM.setFileInputFiles` against the edit page and verify the public profile screenshot. Do not post anything.
Rejected Alternatives: Marking the earlier local textarea state as saved, using native file picker clicks without proof, or publishing a filler image just to make the profile look active.
Scalability potential: Low tier now has a quiet branded Instagram shell; middle/high/ultra can use it later as a visual archive only when real current-build proof assets exist.
Hardware Impact: 0 microseconds runtime impact. Browser/account metadata only.

## Decision 35

Problem: X rendered the profile editor and showed an existing `@submerge_game` account with old hype copy and non-Teni visuals, but handle renaming remains risky because exact-title `Submerge` is already occupied on Steam and `@TeniGames` ownership is not proven.
Solution: Convert only the public-facing shell: display name, bio, dark-wave avatar, and dark-wave banner. Keep `@submerge_game` unchanged and send no post.
Rejected Alternatives: Renaming the handle blindly, using an old X intent URL, posting a first update without proof assets, or keeping the old NASA-punk/Atlas-6 hype bio.
Scalability potential: Low tier gets a clean quiet X shell; middle/high/ultra can rename or split game/studio handles only after title conflict and account custody review.
Hardware Impact: 0 microseconds runtime impact. Browser/account metadata only.

## Decision 36

Problem: Public copy quality was relying on chat corrections and scattered marketing notes, so future agents could keep producing polished AI-sounding posts after context compression.
Solution: Create root `textes.md` as a single writing authority for Teni Games / Submerge public text and add an explicit `AGENTS.md` read requirement before any advertising, social, store, bio, creator outreach, or marketing copy.
Rejected Alternatives: Keeping the rule only in chat, burying it in one social playbook, or letting each platform invent its own voice rules would reproduce the same sterile-copy failure.
Scalability potential: Low tier gets sparse human dev notes before screenshots; middle tier converts into screenshot-led posts; high/ultra tiers keep store, creator, social, and press voice consistent without unsupported claims.
Hardware Impact: 0 microseconds runtime impact. Static docs only.

## Decision 37

Problem: The owner asked to try posting with the new animated/profile visuals before gameplay screenshots exist, but first posts can easily become fake launch energy or unsupported proof.
Solution: Publish one restrained introduction post on X and Bluesky using the approved text from `textes.md` constraints. X accepted the animated GIF; Bluesky rejected GIF/video until email confirmation, so Bluesky used the static PNG profile export. Both posts explicitly say the asset is profile art, not gameplay.
Rejected Alternatives: Posting to Reddit without subreddit rule checks, posting to Instagram/YouTube without platform-specific proof planning, forcing Bluesky video upload through an unconfirmed account, or adding Steam/demo/wishlist CTAs.
Scalability potential: Low tier establishes quiet account activity; middle tier can follow with screenshot-led posts; high/ultra tiers keep social history clean for creator, press, and publisher review.
Hardware Impact: 0 microseconds runtime impact. Browser/account metadata and public social posts only.

## Decision 38

Problem: Manual posting was too slow and encouraged one-off browser fiddling. Text-only dev notes also failed the old helper path because the CDP scripts required media.
Solution: Make X/Bluesky CDP helpers accept `none` media, add `social_queue_run.js`, and move post candidates into `MarketingAssets/99_BrowserWork/social_post_queue_20260601.json`. Publish only one second dev note after queue dry-run proof; mark it `PUBLISHED` after public verification to block accidental repeat.
Rejected Alternatives: Pushing every draft for volume, using Instagram/Telegram/Reddit/YouTube filler posts, or leaving reusable text in chat only.
Scalability potential: Low tier gets fast, proof-backed X/Bluesky posting without stealing desktop focus; middle tier can reuse the queue for screenshot-led posts; high/ultra tiers can add richer media variants without changing account custody or copy rules.
Hardware Impact: 0 microseconds runtime impact. Browser automation/tooling only; no Unity runtime files touched.

## Decision 39

Problem: After two public setup/dev-note posts, another text-only post would make the accounts look scheduled instead of developer-run. The owner asked to think through marketing rather than keep posting filler.
Solution: Hold cadence and make the next public beat asset-led. Add blocked queue drafts that require a pressure-room proof frame, rejected beauty-frame comparison, or salvage-tool frame before publication. Treat the first useful screenshot as a readability test: pump/hatch/gauge/return route before ocean mood.
Rejected Alternatives: Third same-day slogan post, Instagram brand-art filler, Telegram text drip, Reddit broadcast, YouTube empty community update, or naked `Submerge` store/site push before naming review.
Scalability potential: Low tier preserves credible sparse account history; middle tier converts held copy into screenshot-led posts; high/ultra tiers can expand to clips/creator/Steam only after the proof frame makes the player decision readable.
Hardware Impact: 0 microseconds runtime impact. Queue/doc work only.
