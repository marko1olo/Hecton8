# Rationale_SHINOBU_81

Date: 2026-05-18
Status: COMPLETE / RESEARCH ONLY / CORE BUILD VERIFIED

## Decision 01 - Scope Control

Problem: The assignment asks for competitive intelligence, not runtime implementation, while the workspace is under heavy concurrent agent churn.

Solution: Treat this as a clean-room research and product-contract pass. Write only SHINOBU_81 status/rationale/log files and the requested `Docs/REPORTS/COMPETITIVE_GAP_ANALYSIS.md`. Use public web sources plus active HECTON-8 docs. Keep all runtime engine code untouched.

Rejected Alternatives: Editing engine systems directly from competitor observations would violate domain boundaries and would likely collide with active agents. Copying Subnautica 2 structures, visuals, UI, assets, code, or Unreal internals is rejected by clean-room rules.

Scalability potential: Low tier gets cheap but intentional contracts: fog LUTs, impostors, scalar pressure/audio cues. Middle tier gets authored route/content density. High tier gets richer materials and reactive presentation. Ultra gets visual overkill from the same gameplay truth, not separate gameplay.

Hardware Impact: 0us measured runtime impact. Research only. Any later implementation must prove MX350/i3 impact through profiler or static gates before claiming savings.

## Decision 02 - Evidence Discipline

Problem: The prompt demands brutal competitor findings, but YouTube comments, Reddit posts, and review snippets vary in access quality and can produce false certainty.

Solution: Label evidence classes. Official pages and Steam pages are `WEB_REFERENCE_OFFICIAL`; media/reviews are `WEB_REFERENCE_PRESS`; Reddit/Steam community is `COMMUNITY_SIGNAL`; footage inspection is `PUBLIC_FOOTAGE_OBSERVATION`; local HECTON docs are `STATIC_DOC`. No profiler, compile, or runtime truth is implied by any of these.

Rejected Alternatives: Reporting "top 20 most-liked comments" when comment like ordering is inaccessible would be fake evidence. Claiming shader compilation stutter solely because the engine is UE5 is rejected unless public player reports support it.

Scalability potential: Evidence discipline keeps engineering from chasing noise. Low/Middle/High/Ultra work orders must tie to recurring player pain, not isolated rage posts.

Hardware Impact: 0us measured runtime impact. Prevents future wasted implementation time; no microseconds saved are claimed.

## Decision 03 - HECTON-8 Counterposition

Problem: Subnautica 2 has a strong public surface: co-op, bright biomes, Early Access cadence, base building, vehicle exploration, and approachable UX. A simple "darker underwater game" response is weak.

Solution: Position HECTON-8 around NASA-punk / Deep Sea Noir: pressure, corrosion, acoustic dread, hostile visibility, black-box telemetry, industrial wrecks, heavy vehicle feel, deterministic state contracts, and 100km AUP ambition.

Rejected Alternatives: Palette-swap horror, feature parity panic, and high-tier-only spectacle. HECTON-8 must be readable and intentional on weak hardware, then spend saved cycles on sensory overload at high tiers.

Scalability potential: Low: fog LUT, sparse silhouettes, audio alarms. Middle: authored biome/object batches and first-hour route density. High: reactive fauna, silt wakes, richer materials. Ultra: salt, volumetric silt, hull dents, high-tier POM/raymarch/SSS.

Hardware Impact: 0us measured runtime impact. Future implementation target: keep mandatory systems under the documented 0.1ms suspicion threshold per system or supply profiler proof and load-shed behavior.

## Decision 04 - Seed Ship Narrative Hook

Problem: The self-audit requires checking the Seed Ship concept against SN2's story surface.

Solution: Use active batch prompt references as concept evidence only: Seed Ship is a 5km-deep corrupted terraforming vessel/anomaly whose influence can distort gravity, radar, flora/fauna aggression, lore readability, flow fields, and global narrative states through AUP-stable math. The competitive hook is systemic corruption, not lore exposition.

Rejected Alternatives: A boss arena trigger volume, cinematic-only endgame, or hardcoded boss scripts. The stronger hook is a deterministic anomaly field that makes every system misbehave in ways the player can diagnose through instruments.

Scalability potential: Low: scalar corruption intensity alters UI/audio/fog with no heavy simulation. Middle: route and biome overrides. High: predator/flow/hull system reactions. Ultra: overkill visor corruption, silt vortices, glitching text, and physical anomaly presentation from the same scalar fields.

Hardware Impact: 0us measured runtime impact in this report. Future implementation must use AUP-relative distance math and DataVault/SignalBus coupling; no giant physics triggers.

## Decision 05 - YouTube Evidence Boundary

Problem: Task 01 requested top 20 most-liked YouTube comments, but accessible public tooling did not expose reliable ranked comment data for the current videos.

Solution: Report the boundary explicitly. Use official videos, press footage, Steam reviews, Reddit threads, and delegated video review for sentiment taxonomy while refusing to fabricate a ranked comment list.

Rejected Alternatives: Scraping unreliable snippets, presenting "top comments" without like/order proof, or converting marketing comment sections into statistical truth.

Scalability potential: Low/Middle/High/Ultra product directives remain tied to repeated fear themes: Early Access trust, lost atmosphere, co-op dilution, live-service anxiety, thin content, and missing large-vehicle fantasy.

Hardware Impact: 0us measured runtime impact. Prevents engineering churn from false sentiment targets.

## Decision 06 - Co-op Attack Surface

Problem: SN2's co-op is a launch differentiator, but community signals already mention save issues, desync anecdotes, and base-builder/growbed non-host defects. Official roadmap items also leave voice chat, trading, revive, HUD signals, and co-op refinement for later updates.

Solution: Treat shared persistence as the attack surface, not raw player count. HECTON-8 must specify deterministic base ledgers, packet-budgeted state, reconciliation windows, owner/host migration rules, and last-300-frame black-box telemetry for co-op state.

Rejected Alternatives: Claiming 100km co-op superiority without local multiplayer test evidence, or blaming Unreal replication generally without packet traces.

Scalability potential: Low: coarse shared state and deterministic ledgers. Middle: richer shared base state and recovery hints. High: tighter reconciliation, replayable state deltas. Ultra: more sensory feedback and denser co-op telemetry visualization without changing gameplay truth.

Hardware Impact: 0us measured runtime impact in this report. Future implementation target is packet and CPU budget proof before any superiority claim.

## Decision 07 - Scarcity/Bounty Loop

Problem: Player complaints around grind, thin content, and recipe friction become fatal if HECTON-8 copies a linear resource treadmill.

Solution: Define a scalar scarcity/bounty curve using NeedPressure, RouteFatigue, DiscoveryBounty, ScarcityMultiplier, GuaranteedRecovery, SpawnWeight, and RouteHintWeight. The player should feel pressure, then receive evidence-driven recovery routes instead of blind repetition.

Rejected Alternatives: Flat resource respawns, pure random loot, hard scarcity spikes, or hand-authored relief that breaks systemic predictability.

Scalability potential: Low: same scalar math drives sparse hints and guaranteed recovery. Middle: route-aware resource nudges. High: biome-aware bounty and hazard weighting. Ultra: visual overkill around discovery without changing economy outcomes.

Hardware Impact: 0us measured runtime impact in this research pass. Future implementation should be deterministic, allocation-free, and bounded to cheap scalar evaluations per economy tick.

## Decision 08 - Co-op Hallucination Correction

Problem: Earlier competitive analysis discussed "100km co-op" as if it were a plausible HECTON-8 counter-claim. The user corrected this: co-op is not currently planned; agent references are speculative noise.

Solution: Added a scope correction to `Docs/Reports/COMPETITIVE_GAP_ANALYSIS.md` and created `Docs/Marketing/NO_COOP_PUBLIC_POSITIONING.md`. Marketing now treats HECTON-8 as single-player-first. Co-op may be studied as SN2's competitor advantage, but HECTON-8 must not promise it publicly.

Rejected Alternatives: Keeping "100km co-op" as public ambition, implying "multiplayer later", or using SN2's co-op pressure to force a fake feature promise.

Scalability potential: Low/Middle/High/Ultra messaging now focuses on pressure, machinery, salvage, base systems, Seed Ship anomaly, and proof-backed performance instead of network scope.

Hardware Impact: 0us measured runtime impact. This removes a production and trust hazard, not a runtime cost.

## Decision 09 - Low-Budget Marketing System

Problem: The user has a few thousand USD and agent labor, not a AAA marketing budget. Paid ads before proof assets would burn money without solving differentiation.

Solution: Built a `Docs/Marketing/` preparation system: master plan, Steam/wishlist plan, creator database, pitch bank, community templates, press kit shell, source ledger, and agent workflows. The system gates spending behind real screenshots, short gameplay clips, Steam conversion, and demo readiness.

Rejected Alternatives: Broad hype campaign, fake scarcity, generic "underwater survival" copy, purchased lists, paid ads before proof, and mass creator spam.

Scalability potential: Low budget starts with organic critique loops, targeted creators, Steam page conversion, and reusable clips. Middle budget buys capsule/key art and editing. High/Ultra spend only after metrics prove which hook works.

Hardware Impact: 0us measured runtime impact. Future performance marketing remains forbidden until profiler/GC/frame-time proof exists.

## Decision 10 - Exhaustive Prep Over Premature Outreach

Problem: The user requested long, autonomous preparation with many future streamer/YouTuber leads, pitches, post samples, and instructions. The risk is generating a giant fake-contact list or spending effort on outreach before screenshots/gameplay exist.

Solution: Expanded `Docs/Marketing/` into a preparation system instead of a campaign launch: prep directions, screenshot/clip shotlist, raw lead expansion queue, segment pitch matrix, Steam copy matrix, community rules, KPI dashboard, and regional outreach plan. Raw leads are explicitly not outreach-ready.

Rejected Alternatives: Mass-mailing creators, inventing email addresses, buying lists, treating LetsPlayIndex rows as contact permission, or asking communities for wishlists before proof assets exist.

Scalability potential: Low budget uses raw lead mining and organic critique. Middle budget verifies top segments and buys capsule/edit support. High/Ultra marketing only scales after Steam/clip/demo metrics prove which hook converts.

Hardware Impact: 0us measured runtime impact. This is documentation and operational planning only.

## Decision 11 - Raw Lead Expansion With Verification Wall

Problem: The user wants hundreds or thousands of future creator/press targets, but a large unverified list can create spam, scams, fake metrics, and reputational damage.

Solution: Added separate raw-lead documents for adjacent survival creators, press/Steam curators, and regional creators. Kept them outside the verified creator database and paired them with CRM scoring, outreach batch gates, and key compliance rules.

Rejected Alternatives: Combining raw seeds with verified leads, inventing email addresses, using bought lists, sending keys through unverified DMs, and treating high-profile creator names as realistic early outreach.

Scalability potential: Low budget verifies the top 20-50 leads manually. Middle budget verifies top 200. High/Ultra budget can scale to 1000+ only after CRM states, key logs, and metrics are working.

Hardware Impact: 0us measured runtime impact. Operational risk reduction only.

## Decision 12 - Marketing Directions Must Be Production Assets

Problem: The user requested "all directions" prepared now, with streamers/YouTubers, per-lead pitches, post samples, and enough detail to use later when screenshots exist. A shallow strategy memo would not survive execution.

Solution: Added production documents for brand positioning, hook/post libraries, A-tier personalized pitch drafts, Steam asset requirements, and a master marketing backlog. Each document is constrained by no-coop positioning, no fake performance claims, and no competitor-war copy.

Rejected Alternatives: Waiting for screenshots before structuring outreach, mass-spam templates, paid ads before proof, "Subnautica killer" public rhetoric, and unverified contact lists.

Scalability potential: Low budget uses agent labor to verify leads, score screenshots, personalize pitches, and avoid waste. Middle budget buys capsule/trailer polish after proof. High/Ultra marketing only scales after Steam page conversion, creator response, and demo retention data exist.

Hardware Impact: 0us measured runtime impact. This is docs-only operational infrastructure; no Unity/runtime files were touched.

## Decision 13 - Thousands Of Leads Need A Verification Factory

Problem: The user explicitly asked for hundreds/thousands of Subnautica and adjacent creator leads, but a huge raw list without verification would create spam, bad targeting, key-scam exposure, and brand damage.

Solution: Built a reproducible public-index scraper and generated two separate datasets: 7155 raw public rows and 4970 unique channel profiles. Added a 250-row priority shortlist, data readme, scrape summary, fetch log, and mass verification workflow. Each unique lead carries a pitch stub, segment, risk note, and verification action, but remains `RAW_PUBLIC_INDEX_NOT_CONTACT_READY` until checked.

Rejected Alternatives: Fabricating email addresses, treating LetsPlayIndex profile URLs as contact permission, hiding HTTP 429 rate limits, scraping private communities, or sending generic outreach to thousands of channels.

Scalability potential: Low budget verifies 25 leads per agent batch and promotes only high-fit candidates. Middle budget verifies 250-500 leads around screenshot/demo beats. High/Ultra marketing can scale to 10,000+ raw leads only through repeated staged crawls, dedupe, and weekly verification gates.

Hardware Impact: 0us measured runtime impact. Data/docs-only; no Unity/runtime files touched.

## Decision 14 - Leads Need Campaign Timing, Not Just Names

Problem: A large creator database is inert unless it is connected to campaign moments, asset gates, spend gates, and agent-verification batches. Raw leads without timing cause spam; assets without audience mapping waste the first impression.

Solution: Generated a top-250 pitch sheet, split it into ten 25-lead verification batches, and added campaign playbooks for pre-screenshot setup, first screenshot drop, Steam page launch, demo outreach, Next Fest/demo event, and regional push. Added low-budget spend rules and creative briefs for capsule/trailer/thumbnail production.

Rejected Alternatives: Sending outreach before assets exist, buying ads before Steam page conversion, spending on an expensive trailer before gameplay proof, and treating all leads as equal.

Scalability potential: Low budget uses verification batches and critique posts. Middle budget adds capsule/trailer polish after asset proof. High/Ultra spend can scale only after page/demo/creator metrics prove a working hook.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 15 - Public Interest Needs Operating Infrastructure

Problem: Creator lists and campaigns are not enough. If the first screenshots or Steam page create attention, HECTON-8 needs analytics, community structure, crisis replies, feedback triage, presskit/site structure, compliance, paid-creator terms, devlog cadence, and localization gates already documented.

Solution: Added dedicated operational docs for measurement/UTM, Discord/community setup, crisis moderation, player feedback triage, one-page site/presskit, devlog/Steam news pipeline, compliance/disclosure, creator contract terms, and localization/regional asset pipeline.

Rejected Alternatives: Opening a Discord too early, judging campaigns by raw likes, improvising disclosure language, paying creators without terms, translating copy with unchecked machine text, or reacting defensively to public criticism.

Scalability potential: Low budget uses lightweight tables and manual triage. Middle budget adds reviewed localization and paid creator tests only after conversion proof. High/Ultra budget can expand community, press, and regional operations after dashboards and moderation are working.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 16 - Marketing Must Become A Daily Machine

Problem: The marketing corpus was broad, but repeated user direction demanded "do everything" now. The remaining risk was operational drift: agents could keep producing generic research, assets could ship without QA, Steam tags could overpromise, Reddit posts could become spam, and small paid tests could run without hypotheses.

Solution: Added a hard operations layer: Steam tag/search strategy, A/B experiment plan, asset QA checklist, 90-day calendar, daily agent loop, Reddit rule tracker, competitor/sentiment monitoring queries, press release/email templates, and a creator verification CSV template. All public action remains gated by real screenshots, real demo state, verified contacts, same-day platform rules, and no-coop positioning.

Rejected Alternatives: Picking tags from competitor pages, spending on awareness ads before UTM/capsule proof, posting "organic" fake discovery threads, treating raw LetsPlayIndex leads as contacts, sending press emails before a presskit/Steam URL exists, or using likes/views as success without Steam/conversion context.

Scalability potential: Low budget uses agent labor for verified rows, QA scoring, and small experiments. Middle budget can fund capsule/edit/localization only after cold-reader and UTM proof. High/Ultra budget can scale paid tests and regional pushes only when the same measurement and community rules already hold.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 17 - Press Access Must Be Gated Harder Than Creator Outreach

Problem: Press, showcase, and Steam curator activity can burn credibility faster than normal creator outreach. A bad festival submission, weak trailer, raw key leak, fake curator request, unclear embargo, or generic press pitch creates permanent trust damage.

Solution: Added a dedicated press/showcase layer: showcase submission playbook, Steam Curator Connect playbook, review-key/embargo protocol, press angle/subject bank, showcase tracker, press verification tracker, and curator candidate tracker. Every press action is now gated by official-source recheck, real assets, Steam CTA, build state, key/access log, and no-coop/no-fake-performance language.

Rejected Alternatives: Paying showcase fees before a Steam conversion path exists, submitting weak trailers to broad showcases, using external keys for curator requests, sending press copy before a presskit URL exists, or pitching "Subnautica adjacency" as the main headline.

Scalability potential: Low budget uses Steam-native events, free/low-cost submissions, Curator Connect, and tight press batches. Middle budget can test paid showcase slots only after asset and UTM proof. High/Ultra budget can expand to regional/event PR after the same proof gates and tracking exist.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 18 - Launch Economics And Support Must Be Prepared Before Attention

Problem: The marketing machine now covers leads, press, campaigns, and assets, but the project would still be exposed at the moment money or public players enter the loop: price selection, discounts, Early Access copy, public demo, Steam Playtest, review/forum response, launch-day ownership, social handles, trailer production, asset versioning, and risk tracking.

Solution: Added dedicated docs for pricing/discount/EA policy, demo/playtest/telemetry, wishlist/page iteration, Steam review/forum/support response, launch war room, social account playbook, trailer script/capture/editing, asset library naming/version control, and marketing risk register. These docs keep commercial decisions tied to current build proof and prevent public-response improvisation.

Rejected Alternatives: Choosing price from perceived ambition, panic-discounting, launching a public demo before first-route proof, manipulating reviews, opening social channels with no assets, cutting a trailer with no gameplay verb, or using stale screenshots after build changes.

Scalability potential: Low budget uses Steam-native Playtest/demo tools, manual support templates, and strict asset versioning. Middle budget can fund trailer/capsule/localization only after page conversion and demo feedback. High/Ultra budget can scale launch operations, paid social, and showcase support only after the same war-room and risk controls exist.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 19 - Owned Audience Beats Algorithm Dependence

Problem: The plan had Steam, press, creators, and launch ops, but HECTON-8 still needed a direct audience channel and tighter pre-public proof loops. Depending only on Steam visibility, creator replies, Reddit posts, or paid ads is fragile, especially with a small budget and no public screenshots yet.

Solution: Added owned-audience/email plan, playtester recruitment/screening plan, paid microtest matrix, visual identity/key-art direction, public FAQ/objection handling, public roadmap promise policy, and asset metadata template. This creates a path from consent-based signup to segmented playtesting, proof-backed creative, and safe public language.

Rejected Alternatives: Buying/scraping email lists, recruiting random hype testers, running paid ads before Steam baseline, making generic underwater key art, answering objections defensively, publishing roadmap fantasy, or using assets without build/source/status metadata.

Scalability potential: Low budget builds direct list and playtester segments manually. Middle budget can test paid creative only after conversion proof. High/Ultra budget can scale owned audience, paid tests, and roadmap communications without changing the no-fake-claims discipline.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 20 - Stop Document Sprawl And Consolidate Execution

Problem: The marketing preparation set reached 99 files. Continuing to add more documents would create retrieval cost, duplicate strategy, and agent confusion. The user explicitly corrected the direction: work reasonably and do not create a billion docs.

Solution: Added one control document, `Docs/Marketing/MARKETING_CONTROL_TOWER.md`, and updated the marketing README to make it the entry point. The control tower defines gates, anti-sprawl rules, read-first files, lane-specific docs, current priorities, forbidden actions, and stop conditions for new document creation.

Rejected Alternatives: Adding more folder-specific strategy docs, deleting previous docs, or pretending the existing corpus is still easy to operate without a command layer.

Scalability potential: Low budget now gets a smaller read path and clearer current priorities. Middle/High/Ultra marketing can still scale later, but only after real artifacts appear: screenshot pack, Steam page, playable route, demo/playtest, or measurable campaign data.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 21 - Post-SN2-Launch Reality Shift

Problem: Subnautica 2 moved from future competitor surface to live Early Access product with strong Steam momentum. A marketing plan based on the assumption that SN2 will be weak is now structurally dangerous. At the same time, launch-week complaints are volatile and can turn into false attacks if treated as settled truth.

Solution: Updated the control tower and monitoring playbook with a 2026-05-19 launch-week baseline. Steam is used for current release/review/tag/platform facts. Press/community examples are labeled as volatile signals only. The marketing stance remains: learn from pain signals, never attack SN2 publicly, and do not claim HECTON-8 superiority without assets and profiler/build proof.

Rejected Alternatives: Publicly exploiting Reddit complaints, claiming UE5 failure without profiler evidence, assuming SN2 is collapsing despite Very Positive Steam reception, or creating another separate launch-analysis document.

Scalability potential: Low budget now focuses on sharp identity and proof prep, not enemy weakness. Middle/High/Ultra spend remains gated by screenshots, Steam page, demo/playtest, and measurable conversion evidence.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 22 - Active Creator Queue Without New Spreadsheet Sprawl

Problem: The creator system had thousands of raw leads and top-250 documents, but the main CSV tracker was still an example row. That creates friction: agents can read about verification but do not have a concrete first queue in the canonical tracker.

Solution: Reused `Docs/Marketing/Data/CREATOR_VERIFICATION_TEMPLATE.csv` and staged the first 50 priority raw public-index leads inside it. Every row is explicitly unverified and not contact-ready, with source file, profile URL, segment, rough language/country candidate, pitch angle, risk note, and next verification action.

Rejected Alternatives: Creating another CRM file, sending outreach from raw public-index data, inventing official YouTube/Twitch/contact URLs, or deleting the larger 250/4970 lead artifacts.

Scalability potential: Low budget agents can now verify 25-50 leads per pass. Middle/High/Ultra marketing can scale only after rows are promoted to verified status and matched to real HECTON-8 assets.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 23 - Execution Artifacts Over Archive Growth

Problem: The user explicitly rejected "archiving" behavior. The marketing folder already had enough plans; the next useful work had to change operational artifacts that reduce future labor: actual CRM statuses, selected Steam copy, capsule decisions, capture gates, handle reservation tasks, playtest form, and updated risk rows.

Solution: Worked inside existing files only. The first 13 creator rows were manually updated from public sources into actionable statuses. Steam copy was narrowed to three testable candidates. Key-art/capsule planning was converted into a proof-gated decision table. Screenshot QA now defines required shot jobs. Social/playtest/risk docs now have concrete work orders instead of broad strategy.

Rejected Alternatives: Creating more marketing folders, writing another report, marking leads as ready without official contact verification, contacting creators before assets, or using SN2 complaints as public attack material.

Scalability potential: Low budget now has concrete rows and tests to run without spending. Middle/High/Ultra marketing can scale from these rows only after proof assets exist and cold-read tests pass.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 24 - CRM And Post Bundles Are The Current Marketing Work

Problem: More broad preparation would not improve readiness. The actionable bottleneck is whether one human/agent can pick a lead, know whether it is safe to contact later, and pick a post bundle when the first asset appears.

Solution: Continued the same operational artifacts instead of opening new files. Rows 14-33 in `CREATOR_VERIFICATION_TEMPLATE.csv` were triaged from public sources into verify/hold/low/DNC/raw states. The post bank now has three campaign-ready bundles: first screenshot pack, Steam page live, and private playtest recruitment, each with required asset proof and kill rules.

Rejected Alternatives: Sending to creators now, marking vendor-sourced contacts as final, building one-off pitch docs, making public SN2 complaint posts, or adding another campaign document.

Scalability potential: Low budget can now run lead verification and post sequencing with zero spend. Middle/High/Ultra spend can enter only after asset gates pass and creator rows move from triage to official verification.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 25 - First 50 Leads Need Closure, Not More Lead Hunting

Problem: The first 50 staged creator rows still had 16 raw entries. More scraping would increase volume but not readiness. The useful work was to close the first batch into explicit states so future outreach cannot pretend unresolved public-index rows are usable contacts.

Solution: Completed the first-50 triage in `CREATOR_VERIFICATION_TEMPLATE.csv` using public LetsPlayIndex profile snapshots. Every row now has a non-raw status. Large/high-fit channels that need real footage moved to `NEEDS_ASSET`; relevant channels with official YouTube URLs moved to `VERIFY_BEFORE_CONTACT`; platform mismatch, archive, low-relevance, or co-op-expectation mismatch rows moved to low priority or no-contact.

Rejected Alternatives: Scraping another thousand names, inventing emails, using public-index profile URLs as contact permission, pitching multiplayer channels while HECTON-8 is single-player-first, or creating a new CRM file.

Scalability potential: Low budget can now manually verify the 24 `VERIFY_BEFORE_CONTACT` rows and hold the 12 `NEEDS_ASSET` rows until screenshots/Steam/demo exist. Middle/High/Ultra scaling should repeat this 50-row closure pattern before any bigger batch.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 26 - Activity Verification Is Not Contact Permission

Problem: The first-50 queue had non-raw statuses, but `VERIFY_BEFORE_CONTACT` still mixed live creators, stale creators, content-mismatch creators, and channels where only a public-index page had been checked. That is dangerous because a future sender could confuse activity with permission.

Solution: Used official YouTube RSS feeds to check current activity for the 24 `VERIFY_BEFORE_CONTACT` rows. Wrote RSS-backed activity statuses and latest video URLs into the CRM, resolved three channel URLs, demoted stale/content-mismatch rows, and marked six current SN2-active leads as high-priority official-contact checks.

Rejected Alternatives: Treating RSS as an email/contact source, scraping hidden YouTube emails, sending to VOD/archive channels, keeping stale channels in the same priority bucket, or creating another verification spreadsheet.

Scalability potential: Low budget now has a sharper 21-row contact-verification queue and 6 immediate SN2-active targets to check manually after assets exist. Middle/High/Ultra batches should repeat the same RSS/activity pass before any outreach.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 27 - Contact Gate Beats Contact Guessing

Problem: After the RSS pass, the active queue still lacked a hard answer to "where would a human send this later?" Without that, future agents may cold-DM Discord/Twitch links, treat social links as business permission, or invent emails.

Solution: Fetched the YouTube About pages for all 21 active verification rows and recorded the actual public contact gate. Twenty channels expose a YouTube business-email gate requiring login; one channel exposes only external links with no email gate found in the fetched page. The CRM now points to the About page and says exactly what a human must do after assets exist.

Rejected Alternatives: Scraping hidden emails, using Patreon/Discord/Twitch as default cold-pitch routes, marking contacts as send-ready before screenshots/Steam/demo, or splitting the result into another document.

Scalability potential: Low budget can now spend human time only on logged-in email reveal for asset-ready targets. Middle/High/Ultra outreach can repeat this gate-check pattern for the next 50 leads before any send.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.
