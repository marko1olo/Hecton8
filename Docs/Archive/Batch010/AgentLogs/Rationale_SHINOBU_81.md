# Rationale_SHINOBU_81

Date: 2026-05-18
Status: ACTIVE / MARKETING PREP / DOCS ONLY / RUNTIME PENDING

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

## Decision 28 - Prepare The First Send Batch Without Sending It

Problem: The first six SN2-active creators were now verified enough for preparation but still not contact-ready. Generic raw drafts would waste the opportunity once screenshots exist, while sending now would be weak and premature.

Solution: Added a hot SN2-active microbatch to the existing priority-50 message file and updated the matching CSV rows. Each draft is tied to a required asset, a contact gate, and a forbidden angle. The German rows use German-safe copy and the Zombyra draft explicitly controls co-op expectation.

Rejected Alternatives: Sending now, writing another outreach document, using raw generic drafts, pitching co-op-adjacent language, or claiming HECTON-8 superiority without a demo.

Scalability potential: Low budget can use these six as the first human email-reveal/send candidates after asset proof exists. Middle/High/Ultra batches can duplicate this microbatch pattern for the next verified rows.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 29 - Browser Registration Needs Human Custody

Problem: The user allowed browser/platform work, but account registration requires credentials, 2FA, recovery email, backup codes, and could focus the user's active desktop. Doing it casually would create custody and security risk.

Solution: Performed only a public unauthenticated X handle check and wrote the result into the existing social playbook. `@Hecton8` is taken by an unrelated profile. `@Hecton8Game` and `@PlayHecton8` are candidates only because unauthenticated fetch returned 404; final reservation still requires logged-in human confirmation and credential custody.

Rejected Alternatives: Opening the user's browser, registering accounts without password-manager/2FA records, treating 404 as guaranteed availability, or using the taken `@Hecton8` name.

Scalability potential: Low budget can reserve handles with no spend once a human controls credentials. Middle/High/Ultra social operations stay clean because ownership and recovery are logged before public posting starts.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 30 - Press Targets Need Asset Gates, Not Hype Emails

Problem: The press tracker existed but the first 10 rows were generic seeds. Without state, a future agent could send the same weak pitch to PC press, demo sites, showcases, and marketing newsletters.

Solution: Updated the first 10 rows with concrete status, fit score, contact route state, asset requirement, risk note, and next action. Alpha Beta Gamer is demo-gated, The Indie Game Website is presskit-gated, PC Gamer needs a strong presskit and editor-route verification, GameDiscoverCo and How To Market A Game are learning/metrics sources, and PC/Future Games Show are showcase asset gates.

Rejected Alternatives: Sending broad press copy now, treating newsletters as review outlets, pitching showcases without trailer/Steam CTA, or claiming unverified contact routes.

Scalability potential: Low budget can focus on demo/presskit gates first instead of wasting outreach. Middle/High/Ultra press expansion should convert rows into these operational states before any send.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 31 - Press Expansion Must Separate Route Risk From Asset Readiness

Problem: The next 20 press rows included large sites, indie outlets, tech outlets, and route-ambiguous targets. A single "press target" state would cause two failures: premature sends to major outlets before assets exist, and false confidence in old or ambiguous contact routes.

Solution: Expanded `PRESS_TARGET_VERIFICATION_TRACKER.csv` to 30 rows and assigned each row a tactical state: `READY_FOR_HUMAN_REVIEW_AFTER_PRESSKIT`, `ROUTE_UNVERIFIED_HOLD`, `ROUTE_AGE_RISK_HOLD`, `TECH_PROOF_GATE`, `LOW_PRIORITY_UNLESS_WEIRD_ANGLE`, or `LOW_PRIORITY_RISK_REVIEW`. Big outlets are mostly gated by trailer/Steam/presskit; tech outlets are gated by measured performance proof; indie outlets can be first-wave after real screenshots/clip/demo exist.

Rejected Alternatives: Creating a second press spreadsheet, scraping paid databases, copying old public email lists, treating corporate PR addresses as editorial routes, or pitching tech-performance angles before measurements exist.

Scalability potential: Low budget now has 30 sorted press rows without spending. Middle budget can focus on indie/PC outlets after a Steam page and clip exist. High/Ultra press work can scale only after exact route rechecks, presskit quality, and demo/trailer proof justify the outreach.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 32 - Curator Connect Is A Gate, Not A Key Dump

Problem: Steam curator rows were still raw seeds. Curator outreach is a scam-prone lane because fake curators and external key requests can consume keys without coverage, while low-quality curator pages can waste scarce Curator Connect slots.

Solution: Rebuilt `STEAM_CURATOR_CANDIDATE_TRACKER.csv` into 20 operational rows. High-fit rows require Steam page/build first and use one Curator Connect copy each. Tag pages are discovery surfaces, not recipients. Low-reach, stale, formulaic, co-op-expectation, and competitor rows are held or denied. Steamworks Curator Connect rules are the platform boundary; external key sends are rejected.

Rejected Alternatives: Sending raw Steam keys, trusting external emails from curator pages, allocating copies by follower count alone, sending to the Unknown Worlds curator, or pitching co-op-heavy curators while HECTON-8 is single-player-first.

Scalability potential: Low budget can use 8 future Curator Connect copies with minimal risk after the Steam page/build exists. Middle/High/Ultra can expand to the 100-curator Steamworks limit only after first-wave curator response and review quality prove value.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 33 - Social Registration Requires Owner Custody

Problem: The user permitted browser/social-platform work, but account creation without owner-controlled credentials, 2FA, recovery, and backup-code custody is operationally unsafe. Public unauthenticated checks also produce false positives on JS-heavy platforms.

Solution: Performed only quiet public checks and recorded the result in the social playbook. X and YouTube `Hecton8` are taken by unrelated accounts. X/YouTube `Hecton8Game` and `PlayHecton8` remain candidates from public 404 checks. Bluesky default handles were not resolved in public resolveHandle checks. TikTok and Instagram checks are marked inconclusive because generic JS/login HTML does not prove availability.

Rejected Alternatives: Opening/focusing the user's browser, registering accounts without project email/password-manager/2FA records, treating 404 or generic HTML as final availability, or squatting novelty names that break brand consistency.

Scalability potential: Low budget gets a clean reservation order and no account-custody mess. Middle/High/Ultra social operations can scale only after owner custody is logged and first visual assets exist.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 34 - Risk Register Must Follow The Trackers

Problem: Active work changed the real risk map. The project now has specific handle collisions, public-fetch ambiguity, press-route staleness, and curator slot/key-scam risks. If these stay outside the risk register, future agents can repeat unsafe actions.

Solution: Added RISK-026 through RISK-030 to `MARKETING_RISK_REGISTER.md` and updated current top risks. The risk register now ties directly to the social, press, and curator trackers instead of remaining a static pre-screenshot list.

Rejected Alternatives: Keeping the discoveries only in chat/status logs, creating another risk document, or assuming humans will remember that public 404/generic HTML is not final availability.

Scalability potential: Low budget avoids losing account custody and curator copies. Middle/High/Ultra operations can scale only when risk controls remain attached to the execution trackers.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 35 - CRM Should Grow In Verified Chunks, Not New Lists

Problem: The first 50 creator rows were operationalized, but the larger raw shortlist still sat outside the primary CRM. Creating another list would increase sprawl, while jumping straight to outreach from raw rows would create spam risk.

Solution: Appended the next 50 non-duplicate priority shortlist rows into the existing `CREATOR_VERIFICATION_TEMPLATE.csv`. Every new row is explicitly `RAW_PUBLIC_INDEX_NOT_CONTACT_READY`, carries third-party index provenance, and points to official verification as the next action. The tracker now has 100 rows but only the first closed batch has contact/activity gates.

Rejected Alternatives: Creating another spreadsheet, marking rows 51-100 as verified, inventing official YouTube URLs or emails, or scraping more volume before the CRM has a repeatable verification cadence.

Scalability potential: Low budget can verify rows 51-75 next. Middle/High/Ultra outreach can scale only by repeating 25-50 row closure loops with official channel and asset-gate checks.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 36 - Public-Index Activity Is A Demotion Filter

Problem: Rows 51-75 were raw shortlist additions. Verifying every raw row externally would waste time if the existing scrape already shows stale or unparseable activity.

Solution: Enriched rows 51-75 from the raw public scrape with latest indexed activity and one indexed video URL. The result is mostly stale-risk or unparsed, so these rows stay raw and become demotion/official-resolution candidates instead of immediate outreach targets.

Rejected Alternatives: Treating the raw scrape as official activity proof, promoting stale rows to `VERIFY_BEFORE_CONTACT`, or spending human email-reveal time before resolving official channel URLs.

Scalability potential: Low budget can triage the next batch cheaply before any manual contact work. Middle/High/Ultra verification should use the same public-index enrichment as a first pass, then spend human time only where official activity looks promising.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 37 - Promote Only High-Signal Raw Rows

Problem: Rows 51-75 were mostly stale-risk, but two entries had enough public profile evidence to deserve different treatment: Keith Ballard and Wanderbots. Keeping them raw would bury useful candidates; treating them as send-ready would be unsafe.

Solution: Updated only those two rows. Keith Ballard moved to `VERIFY_BEFORE_CONTACT` because the public profile shows a YouTube channel link, current activity, and PR-email expectations, but still needs YouTube RSS/About verification. Wanderbots moved to `NEEDS_ASSET` because the fit is high, but the public profile warns about impersonators and AI assets, so HECTON-8 must have real footage and official-contact confirmation first.

Rejected Alternatives: Promoting all rows 51-75, storing/using a third-party-index email as final contact, sending to Wanderbots before gameplay assets exist, or ignoring the AI-asset/contact-policy caveat.

Scalability potential: Low budget can rescue high-value rows without opening a mass verification sink. Middle/High/Ultra outreach can repeat this pattern: promote only rows with current activity, fit, and safe official-contact path.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 38 - Close Raw Slices Completely

Problem: Rows 51-75 had been enriched, but leaving stale/unparsed rows as raw would pollute the next verification queue and force future agents to rediscover the same weak evidence.

Solution: Closed the entire 51-75 slice out of raw state. Stronger public-signal rows moved to `NEEDS_ASSET`, Keith Ballard stayed `VERIFY_BEFORE_CONTACT`, and stale/archive/low-fit rows moved to `LOW_PRIORITY_VERIFY_LATER`. The raw queue now cleanly points to rows 76-100.

Rejected Alternatives: Keeping every uncertain row raw, deleting low-fit rows, promoting public-stat pages as official contacts, or creating a new "maybe" list.

Scalability potential: Low budget verification can now advance in clean 25-row slices. Middle/High/Ultra outreach can scale only after each slice is closed with explicit verify/asset/low/DNC status.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 39 - Content Must Be Asset-Indexed

Problem: The hook bank had useful copy, but after first screenshots appear, unindexed copy causes improvisation and weak posts. The first public beat needs asset jobs, platform use, CTA, and kill criteria already wired together.

Solution: Added a 12-row asset-to-post queue to `POST_BANK_AND_HOOK_LIBRARY.md`. Each row names the required asset, platform, draft copy, CTA, and kill condition. Added first screenshot captions, creator warmup lines, and a 72-hour sequence that uses feedback to choose the next asset.

Rejected Alternatives: Posting generic dev updates, asking for wishlists before Steam proof, making another content document, or preparing captions that can ship without real screenshots/clips.

Scalability potential: Low budget can run a disciplined first asset wave with no spend. Middle/High/Ultra can expand content only after each asset maps to measurable response and CRM segment fit.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 40 - Account Creation Needs A Handoff, Not Agent Possession

Problem: The user authorized social registration, but an agent-created account without owner custody creates a future lockout/security problem. The correct deliverable is a registration path the owner can execute safely, not an invisible browser action.

Solution: Added an owner account-creation handoff to the social playbook: required fields, reservation order, password-manager note, first-login hardening, and abort conditions. This preserves autonomy while keeping credentials, recovery, and 2FA under human control.

Rejected Alternatives: Opening the user's browser, using existing browser cookies, creating accounts with unknown recovery/2FA, or posting placeholder content before assets exist.

Scalability potential: Low budget avoids account-custody damage. Middle/High/Ultra social operations can scale from owned, recoverable accounts with consistent handles and official links.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 41 - Stop Scraping Until The First 100 Are Closed

Problem: More lead volume would be fake progress while rows 76-100 were still raw. The useful marketing asset is a CRM where a human can trust the status column.

Solution: Enriched and closed rows 76-100. High-signal active/fit rows moved to `NEEDS_ASSET` or `VERIFY_BEFORE_CONTACT`; weak, stale, platform-mismatched, outlet-like, or low-signal rows moved to `LOW_PRIORITY_VERIFY_LATER`. The first 100 creator rows now have 0 raw rows.

Rejected Alternatives: Scraping another thousand leads, keeping rows 76-100 raw, using public-stat pages as contact permission, or treating PlayStation/press/essay/archive channels as first-wave creator targets.

Scalability potential: Low budget can now work from a clean 100-row CRM. Middle/High/Ultra outreach should only expand the queue after official verification and asset gates progress on the current verify/needs-asset buckets.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 42 - Drafts Must Follow CRM State

Problem: The old priority draft file still contained generic raw-public-index templates. After the CRM-100 closure, newly promoted rows needed more specific copy tied to their current status and asset gates.

Solution: Added a CRM-100 high-signal draft section for eleven rows and wrote references back into their CRM `next_action` fields. Every draft is asset-gated and contact-gated; none are send-ready.

Rejected Alternatives: Reusing generic raw templates, creating a separate pitch file, sending before assets, or treating public-stat contact hints as final contact permission.

Scalability potential: Low budget can now turn real screenshots/Steam/demo into a small targeted batch quickly. Middle/High/Ultra outreach can expand only by repeating CRM-state-specific draft preparation after verification.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 43 - Asset Slots Need IDs Before Capture

Problem: If screenshots and clips appear before asset metadata exists, people will post or send files by filename memory. That creates stale assets, wrong build claims, and unclear QA ownership.

Solution: Replaced the example-only metadata CSV with 13 `PLANNED_CAPTURE` rows for the first screenshot pack, first clip pack, and capsule rough test. Added `PLANNED_CAPTURE` to the asset status policy. Every row is explicitly not captured and not approved.

Rejected Alternatives: Keeping a single example row, adding a new asset-planning document, or marking planned assets as RAW/APPROVED before real files exist.

Scalability potential: Low budget can capture and review assets into predefined slots. Middle/High/Ultra asset production can scale from stable IDs, build IDs, QA scores, and rejection codes.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched.

## Decision 44 - Steam Page Needs A Pre-Capture Assembly Path

Problem: The Steam copy matrix had candidate text and separate asset requirements, but it did not yet say which exact asset slots prove the first page. When screenshots arrive, that gap would cause subjective page assembly and weak dark-water frames could pass because the copy sounded good.

Solution: Added `2026-05-19 Steam Page Assembly V0` to `STORE_PAGE_COPY_MATRIX.md`. Candidate A is the default until real assets prove a stronger angle. Each page surface now has required evidence, switch condition, and kill condition tied to planned asset IDs and cold-reader failure rules.

Rejected Alternatives: Keeping copy, screenshots, tags, and trailer assumptions in separate mental buckets; claiming a Steam page is ready without real assets; or using co-op/performance/large-world promises to make weak footage sound bigger.

Scalability potential: Low budget can assemble the first Steam draft from planned captures with no paid consultant. Middle/High/Ultra work can swap stronger captures into the same structure after QA, cold reads, and Steam spec rechecks.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 45 - Capture Tickets Need Reject Codes

Problem: Planned asset IDs existed, but the first capture pass still lacked a strict Steam use case and reject code per shot/clip. That lets "beautiful but useless" assets survive because they are emotionally liked.

Solution: Added `2026-05-19 Steam Page Build Ticket V0` to `STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md`. Every planned shot, clip, and capsule rough now has Steam use, required proof, pass threshold, and a named reject code. The review packet requires build ID, QA score, cold-reader notes, Steam spec recheck, and forbidden-claim cleanup.

Rejected Alternatives: Relying on a generic screenshot checklist, judging assets by taste, or using captions/copy to explain footage that fails thumbnail or cold-reader tests.

Scalability potential: Low budget can kill weak captures before edit time is spent. Middle/High/Ultra capture can scale by adding more candidates while preserving the same pass/fail vocabulary.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 46 - Paid Spend Must Be Experiment-Gated

Problem: The user has only a few thousand dollars for marketing. Without strict experiment gates, that money can disappear into boosted posts, capsule polish, or creator slots before the Steam page proves it can convert.

Solution: Added AB-001 through AB-008 to `A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md`. Each brief names the asset family, stage, audience, CTA, metric, and stop rule. The budget rule keeps spend at 0 USD until assets and cold reads pass, then allows only small measured tests.

Rejected Alternatives: Buying reach before proof, using likes as the main metric, changing multiple variables at once, or allowing a paid test without UTM/Steam baseline.

Scalability potential: Low budget gets protected by no-spend proof loops. Middle/High/Ultra spend can scale only from measured winners rather than taste or panic.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 47 - Experiment IDs Must Be Unique

Problem: The new executable AB-001 through AB-008 rows collided with the older generic A/B matrix rows AB-001 through AB-006. Duplicate IDs would break UTM naming, result logging, and future references.

Solution: Renamed the older generic rows to `CONCEPT-001` through `CONCEPT-006` and kept AB-001 through AB-008 as the canonical executable experiment IDs.

Rejected Alternatives: Leaving duplicate IDs in place, renumbering the new asset-gated briefs, or deleting the older concept seeds entirely.

Scalability potential: Low budget tests need unambiguous IDs. Middle/High/Ultra experiment scale requires stable IDs before any public traffic or paid spend.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 48 - Public Replies Must Route Back To Asset Fixes

Problem: First screenshots will trigger predictable objections: co-op, Subnautica comparison, darkness, AI-looking visuals, missing gameplay, and performance questions. If replies are improvised, the team can overpromise or argue instead of fixing the asset/copy source.

Solution: Added a first-screenshot response matrix to `PUBLIC_FAQ_AND_OBJECTION_HANDLING.md`. Every reply is short, avoids competitor attacks, and has an internal action that routes repeated confusion back to asset order, QA, metadata, or copy.

Rejected Alternatives: Letting agents write long public defenses, promising future features to calm comments, or treating repeated confusion as a community problem instead of an asset/copy problem.

Scalability potential: Low budget avoids reputation damage in early posts. Middle/High/Ultra community scale can reuse the same short-response and source-fix loop across Steam forums, Reddit, X, Bluesky, creators, and press.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 49 - Paid Rows Need Prerequisite Winners

Problem: The paid ads matrix had families and budget tiers but not executable spend rows tied to AB winners. That would let someone spend 50-150 USD on a creative family before knowing which screenshot, capsule, or clip passed.

Solution: Added PMT-001 through PMT-004 to `PAID_MICROTESTS_AND_AD_CREATIVE_MATRIX.md`. Each row requires a prior AB winner, names the planned asset/UTM content, caps spend, defines pass signals, and states stop rules.

Rejected Alternatives: Broad boosting, platform-first budgeting, or treating creative-family headings as permission to buy traffic.

Scalability potential: Low budget preserves cash until proof exists. Middle/High/Ultra paid work can scale only from PMT rows that produced measurable Steam behavior.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 50 - UTM Names Must Match Experiment IDs

Problem: AB, PMT, creator, press, and planned asset IDs now exist, but the measurement plan did not yet specify how those IDs appear in external links. Without a registry, later reports cannot compare assets, spend, and creator traffic cleanly.

Solution: Added an experiment/asset UTM registry to `MEASUREMENT_AND_UTM_PLAN.md`. It defines `utm_content` formats, canonical AB/PMT IDs, and kill rules forbidding competitor/co-op/fake-performance terms.

Rejected Alternatives: Letting each platform invent names, using human-readable campaign names without IDs, or encoding competitor terms into UTM for convenience.

Scalability potential: Low budget needs clean attribution on tiny samples. Middle/High/Ultra operations can only scale if public links and reports share the same ID vocabulary.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 51 - Press Angles Need Proof Assets

Problem: Press subject lines are dangerous before assets exist. A strong-sounding angle can force the project to overclaim or invite journalists to call out generic, dark, or unproven footage.

Solution: Added an asset-proof press angle matrix to `PRESS_ANGLE_AND_SUBJECT_LINE_BANK.md`. PA-001 through PA-010 now map to proof assets, outlet buckets, subject seeds, and hold conditions.

Rejected Alternatives: Pitching from design intent, using "Subnautica" as an explanatory crutch, or sending tech/performance angles without profiler/hardware receipts.

Scalability potential: Low budget avoids wasted press shots. Middle/High/Ultra press work can expand only after each angle has proof and a matching outlet bucket.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 52 - First Creator Send Batch Beats More Scraping

Problem: The CRM has 100 closed rows and high-signal drafts, but without a first send packet the next person could either scrape more or send too broadly once screenshots exist.

Solution: Added a first human-send packet to `MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md`: 10 Wave A candidates for screenshot/Steam proof and a Wave B list held for demo/preview only. Each row names CRM gate, required asset, angle, and caution.

Rejected Alternatives: Expanding raw lead volume now, sending to every `VERIFY_BEFORE_CONTACT` row, or contacting `NEEDS_ASSET` rows without matching proof.

Scalability potential: Low budget starts with 10 targeted messages. Middle/High/Ultra outreach can scale from reply quality and Steam signal, not from mass volume.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 53 - Social Launch Needs Custody And Asset Gates

Problem: Social handles and account handoff existed, but first-post copy was still too generic. Without platform-specific draft copy, the first real asset drop could become duplicated spam or premature public posting.

Solution: Added `Platform Launch Kit V0` to `SOCIAL_ACCOUNT_SETUP_AND_PLATFORM_PLAYBOOK.md`: bio variants, first three public posts, pinned post, and cross-post rules. It is blocked by handle custody, asset QA, and official links.

Rejected Alternatives: Posting from blank accounts before assets exist, duplicating identical copy across every platform, or using social posts as a substitute for Steam/presskit readiness.

Scalability potential: Low budget uses social as proof amplification, not the main engine. Middle/High/Ultra social can scale only after official links and measured asset quality exist.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 54 - Screenshot Drop Must End In Keep/Revise/Kill

Problem: A first screenshot drop can easily become vanity posting. Without a fixed sequence and final decision, the team may keep pushing unclear assets because they got some likes.

Solution: Added `Execution Checklist V0` to `CAMPAIGN_01_FIRST_SCREENSHOT_DROP.md`. It ties the drop to asset metadata, QA, Steam assembly, experiments, UTM, FAQ, creator wave, social custody, a 72-hour sequence, and exactly one keep/revise/kill decision.

Rejected Alternatives: Posting screenshots without preflight, moving to Steam page launch after weak feedback, or treating engagement as success without clarity.

Scalability potential: Low budget gets a cheap clarity test before spend. Middle/High/Ultra campaign scale can proceed only after a clear keep decision.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 55 - Steam Launch Depends On Screenshot Proof

Problem: The Steam page launch plan could still run after assets exist even if the first screenshot test failed. That would move confusion into the Steam funnel and waste creator/press/paid work.

Solution: Added `Steam Page Launch Gate V0` to `CAMPAIGN_02_STEAM_PAGE_LAUNCH.md`. It requires Campaign 01 `KEEP`, asset/capsule/tag/UTM/FAQ readiness, and a first-week expand/revise/stop decision before any expansion.

Rejected Alternatives: Launching Steam after asset production alone, treating wishlists as the only truth, or letting press/creator/paid work expand before the page survives warm traffic.

Scalability potential: Low budget avoids sending traffic to a confused page. Middle/High/Ultra launch scale can start only after the page proves clarity and conversion.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 56 - Cash Spend Stays Frozen Until Proof

Problem: A few thousand dollars is enough to improve a proven asset, but not enough to rescue weak positioning. The budget tree needed to say exactly when cash can be released.

Solution: Added a current spend release ladder to `LOW_BUDGET_SPEND_DECISION_TREE.md`. It freezes spend at 0 USD now and releases small amounts only after asset QA, AB proof, Campaign 01 `KEEP`, Steam/UTM readiness, PMT results, and creator/demo fit.

Rejected Alternatives: Spending on ads, creators, trailer polish, or key art before proof; treating scenario budgets as approval; or buying attention to compensate for unclear assets.

Scalability potential: Low budget protects cash. Middle/High/Ultra spend can scale from proof stages without changing the underlying gate logic.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 57 - Regional Copy Cannot Carry Mojibake

Problem: `CAMPAIGN_05_REGIONAL_PUSH.md` contained mojibake in the RU/CIS pitch. That is a hard trust failure for the user's native market and would make any regional send look careless.

Solution: Rewrote the campaign file with corrected RU/CIS copy and added a regional first-wave package: caps, proof requirements, one-pager fields, and copy kill rules. The file remains localization-review-pending and not send-ready.

Rejected Alternatives: Leaving the corrupted text, hiding regional copy in another document, or treating corrected Russian text as approval to send.

Scalability potential: Low budget can use native-region trust only if copy quality is clean. Middle/High/Ultra regional pushes can scale from reviewed one-pagers instead of raw translation.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 58 - Localization Needs A Review Gate, Not Warnings

Problem: After repairing mojibake, the remaining risk is procedural: future localized copy can still ship with broken encoding, unnatural wording, or added promises unless there is an explicit review gate.

Solution: Added `Localization QA Gate V0` to `LOCALIZATION_AND_REGIONAL_ASSET_PIPELINE.md`: encoding, scope, proof, native-read, CTA, creator-fit gates, a quick review form, and language risk states.

Rejected Alternatives: Relying on general "do not machine translate" warnings, approving copy by visual inspection, or letting regional sends proceed without reviewer/signoff fields.

Scalability potential: Low budget can safely use a few reviewed regional pitches. Middle/High/Ultra localization can scale only if every region passes the same compact QA form.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 59 - First Playtesters Must Be Selected For Signal

Problem: Early playtesters can pollute feedback if they are hype-driven, co-op-driven, unwilling to report, or unable to provide hardware context. That would make a small test wave noisy and expensive to interpret.

Solution: Added `Playtest Screening Score V0` to `PLAYTESTER_RECRUITMENT_AND_SCREENING_PLAN.md`: point scoring, hard rejects, segment quotas, and feedback tags. The first wave is designed to expose clone risk, readability, survival loop friction, low-spec pain, and control/accessibility issues.

Rejected Alternatives: Recruiting broad social traffic, accepting anyone who wants free early access, or treating playtest recruitment as community growth.

Scalability potential: Low budget gets high-signal testers first. Middle/High/Ultra playtest waves can scale only after the segment taxonomy and tags produce useful feedback.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 60 - Playtest Expansion Must Stop On Repeated Tags

Problem: A build that technically runs can still fail as a demo if players cannot state the verb, expect co-op, see a clone, hit unreadable darkness, or get blocked by performance. The demo plan needed an expansion gate tied to feedback tags.

Solution: Added `Playtest Decision Gate V0` to `DEMO_PLAYTEST_AND_TELEMETRY_PLAN.md`: required screening, playable route, known issues, feedback tags, no-coop onboarding, hardware context, and expand/revise/stop decisions.

Rejected Alternatives: Opening Steam Playtest because the build starts, expanding tester waves without segment results, or treating raw playtime/downloads as enough.

Scalability potential: Low budget keeps first waves small and diagnostic. Middle/High/Ultra demo exposure can scale only after repeated failure tags are cleared.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 61 - Press Access Must Be A Gate, Not A Favor

Problem: Press kits and preview access can leak into key spam if the process is only email templates. HECTON-8 needs a strict itemized kit gate and small access batches before any key or preview build exists.

Solution: Added Press Kit Build Ticket V0 and Preview Access Batch V0. Presskit files map to proof sources and reject conditions; access batches map to max sizes, access type, recipients, gates, and stop conditions.

Rejected Alternatives: Sending a partial press kit, giving raw keys to unverified contacts, using Curator Connect candidates as external-key recipients, or creating broad access before stable build proof.

Scalability potential: Low budget avoids key leakage and bad previews. Middle/High/Ultra press/creator access can scale from verified batches instead of ad hoc favors.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 62 - Email List Needs A Concrete Promise

Problem: A mailing list created before screenshots, Steam, demo, or playtest value exists becomes dead weight or spam pressure. It also risks mixing player, creator, and press contacts without consent.

Solution: Added Owned Audience Signup Gate V0 with four allowed modes, each tied to timing, fields, promise, and stop condition. Added list hygiene rules that prevent importing CRM rows or sending filler.

Rejected Alternatives: Generic "join our newsletter" CTA, adding creators/press to a general list, or asking for long survey fields before the user gets a clear benefit.

Scalability potential: Low budget builds only high-intent owned audience. Middle/High/Ultra email operations can scale by segment without damaging trust.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 63 - First Public Attention Needs Stop Rules

Problem: First screenshots or Steam beats can look successful by likes while actually creating clone, co-op, darkness, AI-looking, or performance-confusion damage. Without a triage gate, the team may amplify a bad beat because it appears active.

Solution: Added `First Public Incident Triage Gate V0` to `CRISIS_AND_MODERATION_PLAYBOOK.md`. It defines repeated-signal thresholds, first 30-minute response, 24-hour owner action, and a mandatory keep/revise/kill label.

Rejected Alternatives: Waiting for backlash before defining replies, treating likes as pass signals, or letting agents argue in comments.

Scalability potential: Low budget uses one moderator and one decision table. Middle/High/Ultra community scale can reuse the same thresholds across Steam, Reddit, X, Bluesky, YouTube, creator comments, and regional posts.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 64 - Steam Reviews And Forums Need Pinned Routing

Problem: Steam discussion/review surfaces can become a support treadmill or a reputation amplifier if every issue receives ad hoc replies. The project needs pinned categories and response limits before demo/page traffic exists.

Solution: Added `Steam Forum Launch Moderation Gate V0` to `STEAM_REVIEWS_FORUMS_AND_SUPPORT_RESPONSE_PLAYBOOK.md`: pinned thread requirements, review/forum triage buckets, first-week reply caps, and daily digest template.

Rejected Alternatives: Replying to every review, asking users to change reviews, using review replies as a roadmap, or opening forums without Known Issues and performance templates.

Scalability potential: Low budget keeps support focused on blockers and expectation mismatch. Middle/High/Ultra traffic can scale through pinned routing and daily digest categories instead of individual argument chains.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 65 - Agent Labor Needs A Control Tower

Problem: The user explicitly rejected archive churn and over-documentation. The remaining risk is that agents continue to "work" by generating more strategy text without changing rows, assets, source gates, risks, or public decisions.

Solution: Added `Active Control Tower Loop V0` to `DAILY_AGENT_TASK_LOOP.md`: one lane per day, evidence gate, noon kill check, and end-cut `ADVANCE/HOLD/KILL`. Updated risk/backlog/source spine.

Rejected Alternatives: More broad research files, unbounded scraping, or generic daily summaries that do not modify operational state.

Scalability potential: Low budget agent labor becomes a production line. Middle/High/Ultra marketing operations can scale only if every task ends in a row, asset score, source correction, risk closure, or campaign decision.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 66 - Capsule Art Must Follow Proof, Not Taste

Problem: Capsule/key art is one of the few places where a small budget can help, but spending before the game has readable screenshots can produce a polished lie or a generic dark-water poster.

Solution: Added capsule rough decision/test packets to `VISUAL_IDENTITY_AND_KEY_ART_DIRECTION.md` and `CAPSULE_TRAILER_THUMBNAIL_BRIEFS.md`. Each candidate is mapped to planned asset IDs, a cold-read question, pass threshold, clone-risk guard, and paid-art gate.

Rejected Alternatives: Commissioning final capsule art before in-game proof, judging by internal taste, or using abstract Seed Ship art before the build can show it honestly.

Scalability potential: Low budget can test roughs internally before cash spend. Middle/High/Ultra spend can scale only from a rough that survives small-capsule readability and clone-risk checks.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 67 - Competitor Monitoring Must Not Cherry-Pick Pain

Problem: Current SN2 public data shows huge launch strength and strong review sentiment. Cherry-picking stutter, co-op, or EULA complaints would create a false strategy and invite public pettiness.

Solution: Added a verification sprint addendum to `COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md`. It separates official Steam/press strength signals from mixed/anecdotal performance, co-op, and trust signals, then translates the result into HECTON-8 asset implications.

Rejected Alternatives: Publicly exploiting SN2 complaints, treating SEO fix guides as technical proof, or assuming UE5 shader stutter without recurring evidence.

Scalability potential: Low budget avoids wasting cash on competitor-attack messaging. Middle/High/Ultra marketing can scale around a distinct identity only after HECTON-8 assets prove that identity.

Hardware Impact: 0us measured runtime impact. Docs/web-research only; no Unity/runtime files touched.

## Decision 68 - Control Tower Must Override Stale Priorities

Problem: `MARKETING_CONTROL_TOWER.md` still contained stale execution language around first raw lead staging and old priorities. Since the marketing folder is intentionally anti-sprawl, the control tower must be accurate or it will route future agents into obsolete work.

Solution: Refreshed the control tower with current CRM-100 status, planned asset IDs, spend state, handle custody boundary, press/curator triage state, and proof-first priorities. Updated source ledger and backlog.

Rejected Alternatives: Leaving the stale text because the deeper docs were already updated, or creating a new executive summary file.

Scalability potential: Low budget work now points to the only real blocker: asset proof. Middle/High/Ultra operations can expand only after the gate state changes in this control file.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched.

## Decision 69 - Browser/Account Work Requires Credential Custody

Problem: The user allowed browser/account work, but creating accounts without project email, password manager, 2FA, and backup-code custody would produce orphaned official surfaces and future recovery risk.

Solution: Added an agent-assisted browser/account boundary and account page field kit to `SOCIAL_ACCOUNT_SETUP_AND_PLATFORM_PLAYBOOK.md`. The future mode is executable but blocked until owner-controlled credentials and recovery storage exist.

Rejected Alternatives: Inspecting private browser sessions, registering with temporary credentials, storing secrets in docs, or publishing placeholder posts from accounts without asset gates.

Scalability potential: Low budget gets safe handle reservation without credential loss. Middle/High/Ultra platform operations can expand only from owner-controlled official accounts.

Hardware Impact: 0us measured runtime impact. Docs-only; no private browser or account action.

## Decision 70 - Reserved Accounts Need Quiet Copy, Not Hype

Problem: If handles are reserved before screenshots, a blank account can look abandoned, but hype posts without assets create empty expectations.

Solution: Added `Pre-Asset Quiet Account Content Pack V0` to `POST_BANK_AND_HOOK_LIBRARY.md`: ten optional low-frequency text posts that state scope, standards, and asset gates without wishlist asks or competitor attacks.

Rejected Alternatives: Daily empty posting, wishlist begging before Steam, lore-only posts, or pretending screenshots are ready.

Scalability potential: Low budget can keep reserved handles intentional with 1-2 posts per week. Middle/High/Ultra social scale still waits for real assets and UTM/Steam gates.

Hardware Impact: 0us measured runtime impact. Docs-only; no public post made.

## Decision 71 - First Capture Must Be A Packet, Not A Taste Pass

Problem: The current bottleneck is real screenshots/clips. Without a capture packet, the first art pass can produce pretty dark frames that fail player-verb, clone-risk, and Steam-readability gates.

Solution: Added `Capture Packet V0` to `SCREENSHOT_AND_CLIP_SHOTLIST.md` and a planned-capture metadata workflow to `ASSET_LIBRARY_NAMING_AND_VERSION_CONTROL.md`. Every planned shot/clip now has intent, must-include content, reject code, review form, and status promotion path.

Rejected Alternatives: Capturing broad beauty shots, adding new asset IDs ad hoc, or approving raw captures before QA/source/build fields are filled.

Scalability potential: Low budget can turn the first capture session into a pass/fail asset gate. Middle/High/Ultra content scale can reuse the same metadata and reject-code system for larger campaigns.

Hardware Impact: 0us measured runtime impact. Docs-only; no asset capture or Unity runtime work.

## Decision 72 - First Public Data Must Be Structured Before It Exists

Problem: Once the first links/posts exist, the team can default to likes, impressions, and gut feeling. That loses the only useful early signal: whether strangers describe HECTON-8 correctly.

Solution: Added `Proof-Gate Dashboard V0` to `MARKETING_DASHBOARD_SPEC.md` and `Minimum Measurement Packet Before Public Links` to `MEASUREMENT_AND_UTM_PLAN.md`. First assets/posts now require asset IDs, beat IDs, useful comment counts, intended nouns, confusion, clone, co-op, and explicit decision states.

Rejected Alternatives: Setting up analytics after launch, changing IDs per platform, or treating small public tests as vibes instead of controlled signal.

Scalability potential: Low budget can learn from tiny samples. Middle/High/Ultra spend can scale only after the same structured packet proves what converted or confused players.

Hardware Impact: 0us measured runtime impact. Docs-only; no Steam URL, UTM link, or public post exists.

## Decision 73 - Public Execution Needs Lint, Owners, And Stop Rules

Problem: The marketing stack had asset, measurement, and campaign gates, but a future launch could still fail through ordinary operational mistakes: public copy promising fantasy scope, launch roles being implicit, demo keys going to famous-but-wrong creators, or a holding page/presskit implying readiness before proof exists.

Solution: Added four control gates inside existing docs: Promise Lint Gate V0, War Room Dry Run Gate V0, Demo Access Batch Scoring V0, and No-Link Holding State V0 with presskit minimums. These gates convert future public work into sentence tags, named owners, scored sends, and minimum packet checks.

Rejected Alternatives: Creating more strategy files, relying on memory under launch pressure, using raw creator rank as send priority, publishing a placeholder website, or sending a presskit that has no traceable in-game asset proof.

Scalability potential: Low budget runs the gates manually before any public beat. Middle/High/Ultra operations can scale only after the same gates survive more links, regions, creators, press, and paid traffic.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 74 - Control Tower Must Carry New Gates

Problem: The project relies on `MARKETING_CONTROL_TOWER.md` as the anti-sprawl entry point. If new promise/site/launch gates live only in deeper docs, the next agent can miss them and reintroduce public-copy or launch-readiness risk.

Solution: Propagated promise/copy, site/presskit, and launch/demo ops states into the control tower and added RISK-039 through RISK-041 to the risk register. The top-level map now points future work at linted copy, holding-only site behavior, named owners, and no public-surface assumptions.

Rejected Alternatives: Creating a new executive summary file, leaving the control tower stale, or relying on status logs as the operational map.

Scalability potential: Low budget gets one control map. Middle/High/Ultra operations can scale only if the same map prevents stale instructions as the docs grow.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 75 - Entry Docs Must Not Spawn Duplicate Work

Problem: `README.md` and `PREP_DIRECTIONS_NOW.md` are likely entry points for future agents. They still contained or omitted language that could steer work into duplicate docs instead of the existing control tower, Promise Lint, capsule, and UTM files.

Solution: Updated the README hard rules and directory descriptions. Replaced stale "Needed next" bullets in prep directions with explicit routes to existing files and explicit "do not create" notes for duplicate document names.

Rejected Alternatives: Leaving stale entry text because deeper docs are correct, or creating redirect documents for the missing names.

Scalability potential: Low budget agent work stays concentrated in existing gates. Middle/High/Ultra operations avoid multiplying documentation surfaces as the marketing system grows.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 76 - Lead Volume Is No Longer The Default Bottleneck

Problem: `DAILY_AGENT_TASK_LOOP.md` and Campaign 00 still implied that no-screenshot work should default to 25 lead verifications or first-250 verification. That conflicts with the current CRM-100 state and the control tower: the next bottleneck is real asset proof.

Solution: Added a current cut to the daily loop and Campaign 00. Work now routes toward planned capture, asset gates, asset-linked copy tests, and risk/source corrections unless the human explicitly requests another lead sprint or first assets reveal a source-backed segment gap.

Rejected Alternatives: Continuing to mine leads because it is easy measurable work, or deleting lead workflows entirely. The lead machinery stays available, but no longer owns the default lane.

Scalability potential: Low budget stops wasting agent hours on low-signal extra names. Middle/High/Ultra outreach can expand after asset proof shows which creator segments actually fit.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 77 - Broad Plans Must Be Subordinate To Current Gate State

Problem: `MARKETING_PREP_MASTER_PLAN.md` and the 90-day calendar are useful scaffolding, but their older lead-building language can override the current control tower in practice. That would waste the next work cycle on lead volume while the project still lacks first screenshot/clip proof.

Solution: Added current execution/scheduling overrides to both broad docs. The plan now states CRM-100/0 raw and 13 planned asset slots, then routes work to capture readiness, asset QA, Promise Lint, and concrete source/risk corrections.

Rejected Alternatives: Rewriting the entire plan, deleting the calendar, or leaving the broad plan as a conflicting authority.

Scalability potential: Low budget keeps sequence planning without losing current focus. Middle/High/Ultra operations can re-enable lead and outreach phases when asset gates prove fit.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 78 - Weekly Routine Must Match CRM-100 Closure

Problem: The master backlog still had a hidden weekly instruction to verify 25 raw leads before screenshots. That conflicts with the current CRM-100/0 raw state and can restart low-value lead work without a real asset gap.

Solution: Replaced the default weekly routine with asset packet, asset-to-lead matching, asset-linked hooks, Promise Lint, and source/risk changes. Raw lead verification now requires an explicit source-backed sprint.

Rejected Alternatives: Leaving the old routine because it is under a generic weekly section, or deleting all lead verification workflow. The flow remains available but is no longer automatic.

Scalability potential: Low budget keeps attention on the missing public proof. Middle/High/Ultra outreach can reopen lead work only when assets show a segment need.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 79 - Creator Utility Must Drive Capture Priority

Problem: Planned asset IDs existed and CRM-100 was triaged, but the two were not tightly connected. That leaves capture priority vulnerable to taste: pretty screenshots could be captured while the assets needed for high-fit creators remain missing.

Solution: Added `CRM-100 Asset Unlock Map V0` to the mass lead workflow. It maps current `VERIFY_BEFORE_CONTACT` and `NEEDS_ASSET` rows to the planned screenshot/clip IDs they require, then ranks capture priority by creator utility.

Rejected Alternatives: Expanding raw leads again, promoting creator rows without asset proof, or asking capture to interpret broad marketing goals.

Scalability potential: Low budget gets maximum value from the first small asset pack. Middle/High/Ultra outreach can scale by adding assets that unlock specific creator segments rather than blasting generic audiences.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 80 - Asset Metadata Must Carry Utility Context

Problem: The asset unlock map is useful, but capture and QA operators often start from `MARKETING_ASSET_METADATA_TEMPLATE.csv`. If utility context lives only in outreach docs, first captures can still be judged by aesthetics instead of which creator and campaign gates they unlock.

Solution: Added creator-unlock notes to the existing `notes` field for all planned asset rows. Internal-only/performance-risk rows explicitly state they unlock no public creator outreach until proof exists.

Rejected Alternatives: Adding new CSV columns that may break simple CSV consumers, creating a separate asset-to-creator tracker, or leaving metadata without outreach utility.

Scalability potential: Low budget capture can prioritize maximum unlock value. Middle/High/Ultra content scaling can add more assets while preserving one fact -> one route -> one proof through metadata.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched and no build was run.

## Decision 81 - Creator Utility Cannot Override Visual Proof

Problem: Mapping assets to creator unlocks introduces a new failure mode: the team might publish or send a weak asset because it unlocks a high-value creator row.

Solution: Added a creator utility gate to the marketing asset QA checklist. Utility can affect capture priority only. Public/outreach use still requires screenshot/clip QA thresholds and promise boundaries.

Rejected Alternatives: Adding creator utility as a replacement for visual QA, or leaving utility entirely outside the QA decision.

Scalability potential: Low budget protects first impressions while still prioritizing useful assets. Middle/High/Ultra campaigns can scale only when assets pass both clarity and utility gates.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 82 - Campaign 01 Must Enforce Creator Utility

Problem: The first screenshot campaign had visual QA and a creator wave, but the campaign itself did not require creator utility scoring. That leaves a gap where a visually acceptable screenshot pack could be sent to creators even if it does not map to their audience or a named CRM row.

Solution: Updated `CAMPAIGN_01_FIRST_SCREENSHOT_DROP.md` so required inputs include creator utility, the Wave A creator micro-feedback pack needs utility 3/4+, and the T+48h stop rule requires exact CRM row and exact contact route evidence.

Rejected Alternatives: Leaving creator utility only in the QA checklist, trusting outreach operators to cross-reference it manually, or sending the best-looking asset to every creator segment.

Scalability potential: Low budget avoids wasting scarce creator replies on mismatched assets. Middle/High/Ultra outreach can scale batches only after each asset proves both visual clarity and audience utility.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 83 - Post Copy Must Not Bypass Outreach Gates

Problem: The post bank contained usable public copy and creator warmup lines, but the warmup path did not explicitly inherit the new creator utility gate. That can split execution: social posts follow asset QA while creator notes drift back to generic segment fit.

Solution: Updated `POST_BANK_AND_HOOK_LIBRARY.md` so creator-facing use requires creator utility 3/4+, named CRM row mapping, exact contact route verification, and visual QA. The 72-hour sequence now routes Hour 48 through Campaign 01 Wave A only if those gates pass.

Rejected Alternatives: Adding another content tracker, relying on operators to remember utility gates from Campaign 01, or allowing creator warmup from public-comment momentum alone.

Scalability potential: Low budget protects scarce creator attention. Middle/High/Ultra content operations can reuse the same post bank without accidentally escalating public hooks into mismatched outreach.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 84 - Human Send Packets Need Utility Proof

Problem: The creator workflow had a Wave A human-send packet with required assets, but it did not carry per-recipient creator utility proof or log the score. That lets asset existence become a proxy for recipient fit.

Solution: Updated `MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` so every creator-facing asset must score utility 3/4+, Wave A rows state the utility gate per recipient, and the send log includes `creator_utility_score`.

Rejected Alternatives: Leaving the utility requirement in Campaign 01 only, relying on one broad "required asset" column, or making the CRM status imply asset fit.

Scalability potential: Low budget preserves each creator contact. Middle/High/Ultra outreach can expand batches only by repeating the same per-recipient proof rather than increasing send volume.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 85 - CRM Must Store Send Facts Structurally

Problem: The workflow required `creator_utility_score`, asset IDs, route verification, UTM, reply deadline, and follow-up state, but the CRM schema/live CSV had no columns for those facts. Without fields, send proof would drift into `next_action` prose.

Solution: Updated `CREATOR_CRM_SCHEMA_AND_SCORING.md` and added blank send-log columns to `CREATOR_VERIFICATION_TEMPLATE.csv`: `outreach_batch`, `sent_date`, `contact_route_verified_for_send`, `asset_ids_sent`, `creator_utility_score`, `utm_content`, `reply_deadline`, `followup_allowed`, `reply_status_after_send`, and `coverage_url`.

Rejected Alternatives: Keeping send proof in free-text notes, creating a second send-log CSV before any send exists, or promoting rows without structured proof fields.

Scalability potential: Low budget gets one CRM row per truth. Middle/High/Ultra outreach can add more waves without losing asset/utility/source accountability.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched and no build was run.

## Decision 86 - Asset Metadata Must Own Asset-Side Utility

Problem: CRM rows need `creator_utility_score`, but the asset metadata did not have a structured source for that score. Creator unlock notes existed only as prose, which is not enough for later filtering or audit.

Solution: Updated `ASSET_LIBRARY_NAMING_AND_VERSION_CONTROL.md` and `MARKETING_ASSET_METADATA_TEMPLATE.csv` with `creator_rows_unlocked`, `creator_utility_score`, and `creator_send_gate`. Planned assets start as score 0 and `BLOCKED_PLANNED_CAPTURE`.

Rejected Alternatives: Keeping asset utility only in notes, duplicating it only in CRM after send, or creating another asset-to-creator spreadsheet.

Scalability potential: Low budget keeps asset proof and creator send gates on one asset row. Middle/High/Ultra content scale can filter assets by send readiness without reopening every outreach doc.

Hardware Impact: 0us measured runtime impact. Docs/data-only; no Unity/runtime files touched and no build was run.

## Decision 87 - Control Tower Must Expose Utility Gates

Problem: Deeper docs and CSVs now enforce creator utility, but the control tower still described the old high-level state. Future agents start there, so stale top-level language could route work around the new structured gates.

Solution: Updated `MARKETING_CONTROL_TOWER.md` so the current state, G1 gate, immediate actions, and top priorities explicitly mention CRM send-log fields, `creator_send_gate`, utility 3/4+, and Wave A `asset_ids_sent`/`creator_utility_score` logging.

Rejected Alternatives: Trusting source ledger/status logs to communicate current execution state, or waiting for screenshots before updating the top-level map.

Scalability potential: Low budget keeps all future work pointed at the real blocker. Middle/High/Ultra operations can scale from one control map without bypassing utility proof.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 88 - Utility Bypass Must Be A Named Risk

Problem: The risk register had a broad creator mismatch risk, but not the newer operational failure: bypassing structured utility gates because a frame looks good or a creator is valuable.

Solution: Added RISK-042 to `MARKETING_RISK_REGISTER.md` with prevention tied to `creator_utility_score`, `creator_send_gate`, named CRM row mapping, and `asset_ids_sent`.

Rejected Alternatives: Relying on the workflow docs alone, merging the issue into generic creator spam, or waiting until a bad send happens.

Scalability potential: Low budget prevents a single bad high-value contact. Middle/High/Ultra outreach can scale only if every send respects the same named risk control.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 89 - Daily Loop Must Require Utility Fields

Problem: The daily agent loop still allowed ASSET_GATE and CRM_CLEANUP outputs to pass with generic QA/status updates. That would let future daily work omit `creator_send_gate` and send-log fields even though the deeper docs require them.

Solution: Updated `DAILY_AGENT_TASK_LOOP.md` so lane outputs, Noon Kill, Asset Critic, and screenshot-era quotas require creator utility, send gate, CRM mapping, and send-log fields before outreach escalation.

Rejected Alternatives: Trusting agents to remember the new fields from the control tower, or leaving utility scoring as a specialist-only workflow.

Scalability potential: Low budget keeps daily work operational. Middle/High/Ultra agent throughput can scale because the loop forces the same fields on every asset/CRM day.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 90 - README Must State Creator Utility Gate

Problem: The marketing README is an entry point. It did not state the newer creator utility/send-log gate, so future agents could start from the README and still treat asset existence as enough for outreach.

Solution: Updated `README.md` hard rules, directory descriptions, and First Asset Gate with explicit creator outreach requirements: asset QA, utility 3/4+, `creator_send_gate`, named CRM row, exact route, and send-log fields.

Rejected Alternatives: Assuming everyone starts from the control tower, or leaving utility gate details only in deep workflow docs.

Scalability potential: Low budget avoids new-agent routing error. Middle/High/Ultra documentation scale remains usable because both entry points state the same blocking rule.

Hardware Impact: 0us measured runtime impact. Docs-only; no Unity/runtime files touched and no build was run.

## Decision 91 - SN2 Pain Signals Must Become Proof Gates, Not Attack Copy

Problem: The user asked to keep working autonomously and the active competitive lane is SN2 pain analysis. Fresh Steam API samples show repeated pain buckets, but SN2 remains broadly Very Positive with massive review volume. Treating isolated negative reviews as public ammo would be dishonest and strategically weak.

Solution: Converted SN2 pain into private proof gates. `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md` now records the volatile Steam API snapshot and maps pain buckets to planned assets. QA, shotlist, creator workflow, FAQ, control tower, risk register, backlog, and source ledger now all state the same rule: pain points can prioritize which HECTON proof asset to capture, but cannot create public superiority claims, co-op hints, performance claims, or competitor attacks.

Rejected Alternatives: Creating a new pain-point document, quoting negative reviews in public copy, claiming statistical certainty from a 100-review API sample, or pivoting HECTON toward "we fix SN2" positioning. Standard marketing-agency behavior would turn competitor weakness into ads; this is rejected because it collapses when SN2 patches or when players like both games.

Scalability potential: Low budget gets more value from the first capture packet because every shot answers a known audience fear. Middle tier can reuse the same pain-to-proof matrix for Steam/capsule/cold-read testing. High and Ultra marketing spend can scale only after measured assets prove which pain bucket actually converts.

Hardware Impact: 0us measured runtime impact. This pass touched docs only. Future performance-related public language remains blocked until build/hardware/settings/frame-time proof exists.

## Decision 92 - Pain Proof Needs Structured Asset Metadata

Problem: The SN2 pain-to-proof map existed in monitoring, QA, and shotlist prose, but the planned asset CSV did not store which pain bucket each asset is supposed to answer. That would force future capture operators to re-read multiple docs and could let generic beauty shots pass because the machine-readable asset row lacked the private proof target.

Solution: Added `pain_bucket_answered`, `pain_proof_score`, and `public_comparison_gate` to `MARKETING_ASSET_METADATA_TEMPLATE.csv` and documented the workflow in `ASSET_LIBRARY_NAMING_AND_VERSION_CONTROL.md` plus the control tower. Planned rows keep score `0` until real capture/QA. `public_comparison_gate` defaults to `PRIVATE_ONLY_NO_COMPETITOR_COPY` or stricter.

Rejected Alternatives: A separate pain-proof tracker, encoding this only in free-text notes, or using pain buckets as public positioning. One asset row should carry one asset's operational truth.

Scalability potential: Low budget capture gets a direct row-level target. Middle/High/Ultra campaign operations can filter assets by pain bucket after data exists without adding more spreadsheets or rereading the entire monitoring file.

Hardware Impact: 0us measured runtime impact. Docs/data-only. No runtime, browser, account, screenshot, or build action occurred.

## Decision 93 - Forms Must Enforce The New Metadata

Problem: Adding `pain_bucket_answered`, `pain_proof_score`, and `public_comparison_gate` to the CSV is not enough. If Campaign 01, asset signoff, per-asset review, and dashboard advance rules do not ask for those fields, operators will leave them blank or treat them as optional.

Solution: Updated Campaign 01 required inputs, QA final signoff, shotlist per-asset review form, and KPI proof dashboard to require the structured pain-proof fields before first-pack advancement.

Rejected Alternatives: Relying on the metadata schema alone, adding another checklist, or allowing `pain_proof_score` to be filled after the campaign. The field must be present before the asset advances because the whole point is preventing generic first assets and public comparison drift.

Scalability potential: Low budget gets a stricter first-pack gate. Middle/High/Ultra campaign scale can keep the same fields in dashboards and reports without widening process debt.

Hardware Impact: 0us measured runtime impact. Docs-only. No runtime, browser, account, screenshot, or build action occurred.

## Decision 94 - Creator Context Must Not Become Competitor Piggybacking

Problem: Some creator drafts used recent SN2 coverage as a personalization cue. That can be valid CRM context, but in a final email it can read like HECTON-8 is exploiting a competitor launch or trying to harvest dissatisfaction.

Solution: Tightened hot creator drafts, segment pitch rules, and Promise Lint. SN2/current coverage can remain neutral audience-fit context in internal notes, but final sends should use broader `underwater-survival coverage` wording unless the direct title matters. Subject lines cannot mention SN2 or competitor pain. Public text now explicitly rejects EULA/privacy/desync/stutter/we-fix-SN2 framing.

Rejected Alternatives: Deleting all Subnautica references would erase useful audience-fit evidence. Keeping all explicit SN2 references would invite opportunistic positioning. The compromise is evidence in CRM, neutral wording in final sends.

Scalability potential: Low budget protects first creator outreach from sounding petty. Middle/High/Ultra outreach can still target adjacent audiences while preserving clean-room positioning and no-attack rules.

Hardware Impact: 0us measured runtime impact. Docs-only. No outreach, browser/account action, runtime, or build action occurred.

## Decision 95 - Forbidden-Term Grep Needs Context, Not Panic

Problem: A raw grep for `SN2`, `Subnautica 2`, `stutter`, and related terms finds both dangerous pasteable copy and legitimate control text: reject lists, monitoring queries, risk entries, and CRM evidence. Treating every hit as equal either deletes useful evidence or leaves unsafe final copy hidden in drafts.

Solution: Added a grep-audit boundary to Promise Lint, neutralized the hot email opener bodies that directly referenced Subnautica 2, and removed a pasteable zero-stutter phrase from the press reject-list examples. Direct competitor-title phrasing can remain in internal signal tables and source evidence; public and creator-facing text must use neutral audience-fit language unless a human explicitly approves the title reference.

Rejected Alternatives: Global removal of competitor names would damage targeting and evidence auditability. Leaving direct title references in pasteable email bodies would make opportunistic competitor piggybacking too easy during a rushed send.

Scalability potential: Low budget keeps creator trust intact without losing research signal. Middle/High/Ultra outreach can scale because grep results now have a deterministic triage rule: forbidden-example/internal evidence is allowed, public/send copy is rewritten.

Hardware Impact: 0us measured runtime impact. Docs-only. No outreach, account, browser, post, runtime, or build action occurred.

## Decision 96 - CRM Copy Fields Must Be Safer Than CRM Evidence Fields

Problem: `CREATOR_VERIFICATION_TEMPLATE.csv` contained direct competitor-title wording in `personalized_opener` and `pitch_angle`. Those fields are more likely to be copied into a human email than raw `risk_notes`, so storing direct competitor personalization there creates send-time risk even if the formal draft doc is clean.

Solution: Kept competitor-title evidence in internal status/risk fields, but rewrote hot CRM `pitch_angle`, `personalized_opener`, `next_action`, and verifier labels to neutral underwater-survival wording. Documented the boundary in the CRM schema: evidence fields can name the source; copy-like fields must be final-copy-safe.

Rejected Alternatives: Removing all competitor evidence from the CRM would damage auditability. Leaving direct titles in openers would make copy review dependent on human memory during a rushed first batch.

Scalability potential: Low budget can use the CRM directly without creating hostile or opportunistic email text. Middle/High/Ultra outreach can scale because the database itself now separates targeting evidence from send-copy language.

Hardware Impact: 0us measured runtime impact. Docs/data-only. No outreach, account, browser, post, runtime, or build action occurred.

## Decision 97 - Touched CRM Must Not Retain Corrupted Third-Party Text

Problem: After editing `CREATOR_VERIFICATION_TEMPLATE.csv`, targeted mojibake scanning exposed stale corrupted names/titles in held low-priority third-party-index rows. Even though those rows are not outreach-ready, corrupted text in a touched CRM undermines later filtering and human trust.

Solution: Normalized the affected creator names where safe and replaced unreliable corrupted third-party index titles with an explicit omission note. Rechecked CRM row count and status split so the cleanup did not promote or demote leads.

Rejected Alternatives: Leaving mojibake because the rows are low-priority would make the CRM less reliable. Guessing exact localized titles from corrupted text would create fake source precision.

Scalability potential: Low budget keeps the CRM usable without manual decoding during future review. Middle/High/Ultra outreach can scale only if data hygiene remains predictable and low-priority rows do not poison search/filter passes.

Hardware Impact: 0us measured runtime impact. Docs/data-only. No outreach, account, browser, post, runtime, or build action occurred.

## Decision 98 - Social Permission Is Not Credential Custody

Problem: The user granted broad permission to use the browser and create accounts, but public official accounts become project infrastructure. Creating them without project-owned email, recovery route, password-manager vault, 2FA owner, and backup-code custody would produce orphan credentials and long-term brand risk.

Solution: Performed only public unauthenticated handle rechecks and updated the social playbook with current candidate/taken state. Recorded that account creation remains blocked until custody fields exist, even with chat permission. Candidate handles remain `Hecton8Game` first and `PlayHecton8` second; direct `Hecton8` is treated as taken/unrelated on X/YouTube.

Rejected Alternatives: Registering accounts inside a personal browser profile, relying on cookie state, claiming availability from X 403 responses or generic platform HTML, or publishing placeholder social posts before owner-controlled credentials and asset proof exist.

Scalability potential: Low budget avoids losing official handles or recovery access. Middle/High/Ultra social operations can scale after the handles are reserved under durable ownership and tied to Steam/presskit proof.

Hardware Impact: 0us measured runtime impact. Docs/network-check only. No account, login, browser profile, post, outreach, runtime, or build action occurred.

## Decision 99 - Official Inbox Must Precede Accounts And Keys

Problem: The social/account blocker is not just handle availability. Without an owner-controlled project inbox, social accounts, presskit contact, creator access, keys, and support routing would either use personal mailboxes or fragmented throwaway addresses. That creates recovery, impersonation, privacy, and consent risk.

Solution: Added an Official Project Inbox Gate to the site/presskit plan and propagated it to social registration, compliance/key distribution, risk register, and control tower. The gate requires owner custody: durable project address, password-manager storage, recovery route, 2FA, backup-code storage, labels, and reply identity before any official surface depends on the inbox.

Rejected Alternatives: Creating accounts first and fixing email later, using a personal inbox temporarily, or letting each platform pick its own recovery route. Those shortcuts create irreversible recovery and trust failures.

Scalability potential: Low budget gets one durable contact route instead of scattered inboxes. Middle/High/Ultra outreach can scale creators, press, keys, support, and account recovery from the same controlled identity.

Hardware Impact: 0us measured runtime impact. Docs-only. No inbox, account, login, contact, key, post, runtime, or build action occurred.

## Decision 100 - First Capture Needs A Call Sheet, Not More Strategy

Problem: The active blocker is real screenshots/clips. The shotlist and QA docs were complete enough, but a first capture operator still had to translate broad criteria into a short session plan. That invites taste-based capture, repeated failed takes, and more docs instead of scene fixes.

Solution: Added a 90-minute first capture session call sheet, minimum useful output, and four triage verdicts: `KEEP_TESTING`, `REVISE_SCENE`, `HOLD_ASSET`, and `KILL_ANGLE`. Campaign 01 now requires `KEEP_TESTING` before public screenshot testing can run.

Rejected Alternatives: Creating a new capture document, asking capture to shoot all planned assets in one pass, or letting weak first frames move into public critique because they are the best available.

Scalability potential: Low budget gets maximum signal from one capture session. Middle/High/Ultra content scale can reuse the verdicts to avoid spending on polish before the scene proves identity, player verb, and readability.

Hardware Impact: 0us measured runtime impact. Docs-only. No screenshot, clip, account, outreach, runtime, or build action occurred.

## Decision 101 - Cold Reads Must Produce Structured Evidence

Problem: After the first capture session, the next risk is vague feedback. "Looks cool", "too dark", or "Subnautica-like" is useful only if it is captured as a structured signal that routes to asset QA, Steam copy, capsule choice, or kill/revise decisions.

Solution: Added a cold-read score sheet for AB-001, AB-002, AB-004, AB-006, and AB-007, with response fields for genre, player verb, identity nouns, mode assumption, proof belief, readability issue, click interest, and kill reason. Added a matching dashboard table and tied Campaign 01 to raw response logging before public posting.

Rejected Alternatives: Running informal chat polls, averaging likes, explaining the game before asking questions, or mixing pre-public clarity tests with public engagement metrics.

Scalability potential: Low budget gets hard clarity data before public traffic. Middle/High/Ultra spend can scale only after the winning screenshot/copy/capsule causes strangers to describe the game correctly in their own words.

Hardware Impact: 0us measured runtime impact. Docs-only. No cold-read test, account, public post, outreach, runtime, or build action occurred.

## Decision 102 - Steam Assembly Must Not Launder Weak Assets

Problem: Steam docs already required assets, but without explicit binding to `KEEP_TESTING`, `KEEP`, and cold-read evidence, a weak first capture could still be laundered into the store page because it was the best available material.

Solution: Bound Steam assembly and Campaign 02 to first-session verdicts, Campaign 01 decision, and AB-001/002/004 cold-read evidence. `HOLD_ASSET` and `KILL_ANGLE` rows now cannot feed first-page screenshot order, tags, copy, or capsule direction.

Rejected Alternatives: Letting Steam page assembly start once assets exist, patching weak scenes with stronger prose, or using concept/lore substitutes for missing proof.

Scalability potential: Low budget avoids wasting the first Steam impression. Middle/High/Ultra paid/press/creator expansion can scale only after the store page is built from assets that survived triage and cold reads.

Hardware Impact: 0us measured runtime impact. Docs-only. No Steam upload, public traffic, account, outreach, runtime, or build action occurred.

## Decision 103 - Creator Send Must Not Bypass Asset Verdicts

Problem: The first human-send workflow already required asset utility, but it still allowed a path where a strong CRM row plus a good-looking asset could skip official inbox custody, first-session verdicts, cold-read evidence, Steam/Campaign decision state, or exact CRM send-log fields.

Solution: Tightened `MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` so Wave A requires official project inbox custody, first-session `KEEP_TESTING`, Campaign 01 `KEEP`, AB-001/002/004 cold-read evidence when proof/Steam language is used, no `HOLD_ASSET`/`KILL_ANGLE` assets, open `creator_send_gate`, Promise Lint, and exact CRM send-log fields. CRM copy-like fields are now explicitly drafting hints, not send approval.

Rejected Alternatives: Letting creator outreach rely on asset presence, high creator priority, or `personalized_opener` text would recreate the same premature-send failure the asset gates were built to prevent. Creating another send checklist file would increase doc sprawl.

Scalability potential: Low budget can run one small human batch without losing proof state in notes. Middle/High/Ultra creator programs can scale because send authorization now has one route: asset verdict -> creator utility/send gate -> official route -> CRM send log.

Hardware Impact: 0us measured runtime impact. Docs-only. No contact, account, browser login, post, asset approval, Steam page, runtime, or build action occurred.

## Decision 104 - Schema Authority Must Match The Live CRM

Problem: After aligning the human-send workflow to the live CSV field `contact_route_verified_for_send`, the CRM schema doc still described stale/future aliases such as `contact_route_verified`, `reply_status`, `last_contacted`, `contact_value`, `personalization_note`, `country_region`, and `audience_fit`. That would split send proof across incompatible columns.

Solution: Rewrote the schema table in `CREATOR_CRM_SCHEMA_AND_SCORING.md` to match the live `CREATOR_VERIFICATION_TEMPLATE.csv` header and explicitly reject renamed duplicate aliases. The schema now names the actual source, creator, contact, verification, and send-log fields used by the CSV.

Rejected Alternatives: Updating only the workflow would leave schema drift. Adding compatibility aliases would make filters and send audits unreliable. Changing the live CSV again would create unnecessary churn after the current fields already parse and align with workflow needs.

Scalability potential: Low budget can audit one CSV without translation. Middle/High/Ultra outreach can scale through filters and batch scripts because field names are stable and proof does not fragment into parallel columns.

Hardware Impact: 0us measured runtime impact. Docs/data schema text only. No CRM row status, outreach state, account, browser login, post, asset approval, runtime, or build action occurred.

## Decision 105 - Content Calendar Must Not Become A Send Bypass

Problem: The post bank 72-hour sequence still named "Creator micro-feedback" at Hour 48 with only partial gates. That could let a sender treat the content calendar as a lighter route around the stricter first human-send packet.

Solution: Updated Hour 48 to require the full first human-send packet gates from `MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md`: official inbox custody, creator utility, named CRM row, exact contact route, open `creator_send_gate`, Promise Lint, and CRM send-log fields. If those fail, the beat stays a devlog draft.

Rejected Alternatives: Duplicating all workflow gates inside the post bank would drift. Leaving the weaker line would make the stricter workflow optional in practice.

Scalability potential: Low budget gets one consistent rule for the first micro-feedback batch. Middle/High/Ultra content programs can scale from the post calendar without splitting creator send authorization from CRM and asset metadata.

Hardware Impact: 0us measured runtime impact. Docs-only. No public post, creator contact, account, browser login, asset approval, Steam page, runtime, or build action occurred.

## Decision 106 - Negative Competitor Phrases Are Still Pasteable Copy

Problem: `PRIORITY_50_MESSAGE_DRAFTS_FROM_RAW.md` had many draft email bodies saying "not a co-op promise", "not a co-op pitch", or "not a Subnautica killer". These were meant as safety boundaries, but in a pasteable draft body the forbidden phrase still reaches the sender's clipboard.

Solution: Mechanically rewrote the draft bodies and subjects to use single-player-first and competitor-attack-neutral wording. Left guardrails and internal signal rows intact because they are control/evidence contexts, not final body copy.

Rejected Alternatives: Deleting the whole creator draft bank would waste useful segment work. Leaving negative forbidden phrases in body copy would make Promise Lint depend on sender memory under time pressure.

Scalability potential: Low budget can use drafts without a second copy-sanitization pass for every row. Middle/High/Ultra outreach can scale by keeping evidence in tables and final body copy neutral by default.

Hardware Impact: 0us measured runtime impact. Docs-only. No outreach, public copy publication, account, browser login, post, asset approval, Steam page, runtime, or build action occurred.

## Decision 107 - Readiness Status Must Not Invent A Missing CRM State

Problem: `CREATOR_CRM_SCHEMA_AND_SCORING.md` still said outreach-ready rows needed `READY_FOR_HUMAN_REVIEW`, but the live CRM currently uses `VERIFY_BEFORE_CONTACT`, `NEEDS_ASSET`, `LOW_PRIORITY_VERIFY_LATER`, and `DO_NOT_CONTACT`. Referencing an absent status makes future operators either invent a column/state or ignore the gate.

Solution: Rewrote the outreach readiness gate: `VERIFY_BEFORE_CONTACT` is the minimum pre-send review state, not send approval; `NEEDS_ASSET` remains blocked until matching asset proof exists. The gate now requires official inbox custody, same-day `contact_route_verified_for_send`, creator utility 3/4+, and open `creator_send_gate`.

Rejected Alternatives: Adding a new status during a docs-only pass would churn the live CSV. Leaving the absent status would split process language from data reality.

Scalability potential: Low budget can run CRM filters without state translation. Middle/High/Ultra outreach can later add a new explicit status only with a deliberate schema/data migration.

Hardware Impact: 0us measured runtime impact. Docs-only. No CRM row status, outreach, account, browser login, post, asset approval, runtime, or build action occurred.

## Decision 108 - Official Profile Fields Must Be Copy-Safe

Problem: The social account setup playbook had profile/password-manager fields containing literal negative forbidden phrases such as `no co-op promise` and `Subnautica killer`. Those fields may be pasted directly into platform profiles or account vault notes.

Solution: Rewrote the pasteable profile/setup fields to use single-player-first and competitor-neutral language. Left FAQ/guardrail contexts intact because they are question-handling controls, not profile body copy.

Rejected Alternatives: Relying on later profile review would be brittle during handle reservation. Removing the account setup field kit would slow the owner when credentials are ready.

Scalability potential: Low budget can reserve accounts without unsafe placeholder text. Middle/High/Ultra social operations can reuse the same safe fields across X, Bluesky, YouTube, Reddit, and account vault notes.

Hardware Impact: 0us measured runtime impact. Docs-only. No account registration, browser login, public post, outreach, asset approval, Steam page, runtime, or build action occurred.

## Decision 109 - Press And Site Stance Fields Must Not Carry Negative Scope Phrases

Problem: Press quote bank and website public-stance fields still contained `co-op as a promise` / `no co-op promise` language. Those are reusable public-surface fields, not only internal rejection examples.

Solution: Replaced the pasteable press quote and site stance with single-player-first/proof-first scope wording. Left reject lists and FAQ/question contexts intact because they document what to prevent or how to answer direct public questions.

Rejected Alternatives: Removing all scope language would weaken expectation control. Leaving the negative phrase in quote/stance fields would keep unsafe copy close to publication.

Scalability potential: Low budget can assemble press/site drafts without a second scope rewrite. Middle/High/Ultra launch operations keep the same public stance across site, press, socials, and creator sends.

Hardware Impact: 0us measured runtime impact. Docs-only. No press send, site publish, account registration, browser login, public post, asset approval, Steam page, runtime, or build action occurred.

## Decision 110 - Old Lead Quotas Must Not Override CRM-100/0 Raw State

Problem: Several active workflow docs still told agents to verify 20-25 raw leads or expand toward hundreds of leads. That conflicts with the current CRM state: 100 staged rows, 0 raw, and the real blocker is asset proof.

Solution: Rewrote the pre-screenshot/default cadence in the creator workflow, daily loop, outreach calendar, and prep directions. Default work now goes to planned asset readiness, asset-to-CRM matching, and route rechecks only when proof creates a send need. Raw expansion requires an explicit source-backed sprint after first assets reveal a real audience gap.

Rejected Alternatives: Keeping historical lead quotas would waste agent cycles and bury the asset bottleneck. Deleting raw-lead machinery entirely would remove useful future optionality.

Scalability potential: Low budget stops wasting time on volume. Middle/High/Ultra outreach can reopen lead expansion later with evidence from actual assets and segment response.

Hardware Impact: 0us measured runtime impact. Docs-only. No CRM row status, outreach, account, browser login, public post, asset approval, Steam page, runtime, or build action occurred.

## Decision 111 - Creator Workflow Status Table Must Not Reintroduce Legacy States

Problem: `MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` still listed legacy statuses such as `RAW_PUBLIC_INDEX_NOT_CONTACT_READY`, `VERIFYING`, and `VERIFIED_NOT_CONTACTED` even after the live CRM had moved to 100 staged rows and 0 raw. That contradicted the new readiness language and could reintroduce stale states.

Solution: Replaced the workflow status table with the live CRM status set and added a boundary that future raw sprints must use a separate raw queue or deliberate schema migration before adding legacy statuses to the live CRM.

Rejected Alternatives: Keeping both status systems would make filtering ambiguous. Changing the live CSV statuses during this docs pass would be unnecessary churn.

Scalability potential: Low budget keeps one CRM state model. Middle/High/Ultra lead expansion can still reopen through explicit migration instead of accidental status drift.

Hardware Impact: 0us measured runtime impact. Docs-only. No CRM row status, outreach, account, browser login, public post, asset approval, runtime, or build action occurred.

## Decision 112 - Raw Pitch Inventory Must Not Become Current Work By Accident

Problem: `PRIORITY_250_PITCH_SHEET_FROM_RAW.md` still looked operational: it told agents to assign 25 rows, used a legacy raw status, and repeated pasteable direct-underwater pitch seeds saying `not as a clone or co-op promise`. That conflicts with CRM-100/0 raw state and the current bottleneck: asset proof.

Solution: Parked the raw 250 sheet behind the asset-proof gate, documented that its raw status is local and cannot be copied into the live CRM, and mechanically rewrote the unsafe pitch seed phrase to distinct single-player identity wording. The sheet remains useful inventory, not a default work queue.

Rejected Alternatives: Deleting the raw sheet would waste source-index work. Promoting rows into CRM would create fake readiness. Leaving the old 25-row assignment line would restart lead-volume work before first capture proof exists.

Scalability potential: Low budget avoids spending agent time on raw expansion while assets are missing. Middle/High/Ultra outreach can reopen a bounded raw sprint later if captured assets reveal a segment gap, then move only verified rows through the live CRM status model.

Hardware Impact: 0us measured runtime impact. Docs-only. No lead promotion, CRM row status change, outreach, account, browser login, public post, asset approval, runtime, or build action occurred.

## Decision 113 - AgentOps Must Not Regenerate Old Raw-Lead Behavior

Problem: Cleaning the raw 250 pitch sheet was not enough because `AgentOps/AGENT_MARKETING_WORKFLOWS.md`, generated verification batches, and two generator scripts still encoded the old behavior: mine/verify raw leads pre-asset, use stale readiness statuses, and generate pasteable clone/co-op-promise denial copy.

Solution: Updated the AgentOps playbook to the current asset-proof loop and live CRM status model; parked all verification batch markdown behind explicit raw-lead sprint gates; labeled raw sheet statuses as non-CRM; and updated generator templates so reruns do not recreate the unsafe body copy.

Rejected Alternatives: Editing only generated batch markdown would leave the scripts as a regression source. Deleting the batch artifacts would destroy historical raw work. Running the scrapers again would add data churn without solving the asset bottleneck.

Scalability potential: Low budget keeps agents from spending another pass on raw volume. Middle/High/Ultra outreach can reopen raw mining later through a bounded sprint with generator templates that already respect proof-first copy and live CRM status boundaries.

Hardware Impact: 0us measured runtime impact. Docs/script-template only. No scraper run, no lead promotion, no CRM row status change, outreach, account, browser login, public post, asset approval, runtime, or build action occurred.

## Decision 114 - Post Bank Must Be Safe At The Clipboard Layer

Problem: `POST_BANK_AND_HOOK_LIBRARY.md` contained reusable post bodies and campaign snippets with negative scope phrases such as `No co-op promise`, `killer pitch`, and direct co-op-denial wording. These were intended as expectation control, but in a copy bank they can become public text with one paste.

Solution: Rewrote proactive post titles, X/Bluesky hooks, pre-asset posts, first screenshot copy, Steam page creator intro, playtest recruitment copy, and sequence notes to use proof-first/single-player-first language. Kept explicit Q&A contexts for direct user questions, because support/moderation still needs a clear answer path.

Rejected Alternatives: Deleting co-op FAQ contexts would weaken moderation. Leaving negative forbidden phrases in proactive copy would make final Promise Lint depend on sender memory. Creating a separate clean post bank would increase doc sprawl.

Scalability potential: Low budget can use the post bank without a second rewrite pass before every post. Middle/High/Ultra social and creator programs can scale because proactive copy is safe by default while direct Q&A remains controlled.

Hardware Impact: 0us measured runtime impact. Docs-only. No post, account, browser login, outreach, public traffic, asset approval, runtime, or build action occurred.

## Decision 115 - Campaign And Regional Templates Need The Same Copy Safety

Problem: Campaign launch, demo outreach, regional one-pagers, and community post templates still carried pasteable negative scope language: co-op denial, clone-war phrasing, and direct `Subnautica killer` copy. Those files are closer to public/send execution than policy docs.

Solution: Rewrote proactive campaign, regional, and community snippets to express single-player/proof-first scope without competitor-war or co-op-denial phrasing. Left reject-list examples intact where they are explicitly forbidden-copy controls.

Rejected Alternatives: Cleaning only English templates would leave regional sends unsafe. Removing all scope language would weaken expectation control. Leaving the negative phrasing would let old copy bypass the new post-bank and Promise Lint work.

Scalability potential: Low budget can prepare first Steam/demo/regional/community beats without a second rewrite pass. Middle/High/Ultra launch operations keep consistent scope language across campaign, regional, community, and creator surfaces.

Hardware Impact: 0us measured runtime impact. Docs-only. No campaign, regional send, community post, account, browser login, outreach, public traffic, asset approval, runtime, or build action occurred.

## Decision 116 - Master Press And Creator Banks Must Not Lag Behind Cleaned Campaigns

Problem: After cleaning campaign and post banks, the master press/creator/audience banks still had pasteable negative scope language in press skeletons, signup/welcome emails, reusable creator DMs, A-tier drafts, and paid creator brief copy. Those banks would re-seed unsafe copy into future campaigns.

Solution: Rewrote those reusable surfaces to proof-first/single-player-first language and updated one press tracker angle. Kept explicit reject-list examples in adjacent press docs where they serve as forbidden-copy controls.

Rejected Alternatives: Cleaning only campaign outputs would leave the source banks dirty. Removing all expectation-control language would weaken scope discipline. Editing the live CRM was unnecessary because the affected press tracker row is press data, not creator CRM.

Scalability potential: Low budget can assemble first press/creator/audience materials from one safe source bank. Middle/High/Ultra launch operations avoid re-sanitizing every future campaign made from these banks.

Hardware Impact: 0us measured runtime impact. Docs/data-only. No press send, newsletter setup, creator contact, account, browser login, public traffic, asset approval, runtime, or build action occurred.

## Decision 117 - Header Stance Lines Are Also Copy Seeds

Problem: Many marketing documents still had the old header `Public stance: single-player-first / no co-op promise`. Headers are not final copy, but agents routinely use them as source language, so they can reintroduce negative scope phrasing into future posts, emails, or profile fields.

Solution: Normalized the remaining old headers to proof-first scope wording across the active marketing tree. This was a mechanical header hygiene pass, not a change to feature scope or readiness.

Rejected Alternatives: Leaving headers alone would keep a low-grade copy regression source. Rewriting every internal mention of co-op would damage explicit FAQ/moderation contexts where direct answers are needed.

Scalability potential: Low budget and future agents now start from consistent proof-first language. Middle/High/Ultra campaign expansion can still answer direct co-op questions in FAQ contexts without copying negative phrasing from document headers.

Hardware Impact: 0us measured runtime impact. Docs-only. No public copy publication, outreach, account, browser login, asset approval, runtime, or build action occurred.

## Decision 118 - Negative Denial Is Not A Proactive Copy Strategy

Problem: The recent cleanup removed many pasteable negative denial phrases, but Promise Lint did not yet encode the root rule. Without the rule, future copy banks could reintroduce "not X / no Y" slogans as expectation control.

Solution: Added `Negative Denial Copy Rule` to `PUBLIC_ROADMAP_LANGUAGE_AND_PROMISE_POLICY.md`. Proactive public, creator, press, social, account-profile, signup, and campaign copy must use proof-boundary language. Direct denial wording is reserved for explicit user questions, FAQ/moderation response blocks, and reject/forbidden-copy lists.

Rejected Alternatives: Continuing manual cleanup would be fragile. Banning direct denial everywhere would make moderation evasive when users ask a direct scope question.

Scalability potential: Low budget agents can write safe first-pass copy without senior review for every line. Middle/High/Ultra launch work can scale copy volume while keeping public scope discipline centralized in Promise Lint.

Hardware Impact: 0us measured runtime impact. Docs-only. No public copy publication, outreach, account, browser login, asset approval, runtime, or build action occurred.

## Decision 119 - Creator And CRM Copy Seeds Must Obey The Same Rule

Problem: After adding the negative-denial Promise Lint rule, several creator-send templates and CRM/raw CSV copy-like fields still carried proactive denial phrases. Those fields are close to future outreach and can bypass policy if an operator copies from CRM, raw stubs, or pitch skeletons.

Solution: Rewrote active creator templates, live CRM personalized opener/pitch fields, parked raw pitch stubs, social/community setup snippets, website/localization/creative/audience scope fields to proof-boundary language. Kept explicit FAQ/reject-list behavior where the context is a direct user question or forbidden-copy control.

Rejected Alternatives: Leaving raw CSV stubs dirty would re-seed unsafe copy when a raw sprint reopens. Deleting raw inventory would waste source work. Changing live CRM statuses would be unrelated and risky.

Scalability potential: Low budget outreach can reuse CRM/raw stubs without another copy rewrite. Middle/High/Ultra creator operations can scale send preparation while keeping one public-scope language route.

Hardware Impact: 0us measured runtime impact. Docs/data-only. CSV parse passed; live CRM stayed 100 rows with unchanged status split. No lead promotion, outreach, account, browser login, public post, asset approval, runtime, or build action occurred.

## Decision 120 - Gate Labels Must Describe The Actual Boundary

Problem: Active gates used `no-coop` shorthand as an operational label. That is acceptable as private shorthand, but it creates weak instructions and can leak into public copy as a slogan.

Solution: Reworded active gates to `multiplayer-scope boundary`, `single-player-first proof-boundary`, and `unsupported multiplayer-scope language`. Kept the existing `NO_COOP_PUBLIC_POSITIONING.md` file path for compatibility while changing its title/content to the real boundary.

Rejected Alternatives: Renaming the file would break existing references. Leaving shorthand would keep future agents writing the same negative-denial copy this pass removed.

Scalability potential: Low budget teams get clearer checklist language. Middle/High/Ultra marketing operations can scale reviews across Steam, press, social, playtest, and creator gates without translating shorthand.

Hardware Impact: 0us measured runtime impact. Docs/data-only. CSV parse passed. No public copy publication, outreach, account, browser login, asset approval, runtime, or build action occurred.

## Decision 121 - First Captures Need Factual Intake Before Scoring

Problem: The docs had planned asset rows and QA gates, but the first real capture could still be entered loosely: a dashboard row might claim proof before metadata holds path/build/date/source facts.

Solution: Added `First Real Capture Intake Packet V0` to asset-library operations and a capture-intake join to the KPI dashboard. The metadata row remains the owner of asset facts; the dashboard can only mirror facts after metadata and QA are filled.

Rejected Alternatives: Creating a new intake spreadsheet would increase doc sprawl. Letting dashboard rows be independent would create two conflicting sources of truth. Waiting for a polished full pack would hide early capture failure.

Scalability potential: Low budget capture can intake a few weak assets and fail fast. Middle/High/Ultra marketing can scale asset review while preserving one owner for build/source/claim facts.

Hardware Impact: 0us measured runtime impact. Docs-only. No capture import, asset approval, public post, outreach, account, browser login, runtime, or build action occurred.

## Decision 122 - Account Creation Needs Non-Secret Custody Rows

Problem: The account handoff had preconditions, but after an account is created the docs did not provide a structured non-secret record for URL, vault item, 2FA state, backup-code custody, profile visibility, and current account status.

Solution: Added post-registration custody rows to the social playbook and an inbox custody record to the website/presskit plan. The docs record public/control state only; secrets remain in the owner password manager.

Rejected Alternatives: Creating accounts now would create orphan surfaces without project email/vault/2FA custody. Storing credentials or recovery details in docs would be a security failure.

Scalability potential: Low budget handle reservation can happen once without losing ownership facts. Middle/High/Ultra platform expansion can add accounts while keeping custody status visible and non-secret.

Hardware Impact: 0us measured runtime impact. Docs-only. No account registration, browser login, credential storage, public post, outreach, runtime, or build action occurred.

## Decision 123 - Steam Upload Readiness Is Not Launch Readiness

Problem: Campaign 02 and the Steam asset checklist already gated assets, but did not explicitly bind public Steam traffic to official URL/contact/presskit/social custody state. That could create a page announcement with broken or orphaned official routes.

Solution: Added official link/contact preflight to Campaign 02 and Steam asset requirements. Updated control tower G2 to require official Steam URL, inbox custody, presskit/contact state, social custody, UTM readiness, and asset-intake/dashboard proof before public traffic.

Rejected Alternatives: Relying on human memory during launch would be fragile. Blocking all Steam assembly until presskit is live would be too strict; the new gate allows presskit to be intentionally absent from copy.

Scalability potential: Low budget launch can stay minimal without broken links. Middle/High/Ultra launch operations can scale creator, press, and paid traffic only after route custody is factual.

Hardware Impact: 0us measured runtime impact. Docs-only. No Steam page, upload, public announcement, account/browser action, outreach, runtime, or build action occurred.

## Decision 124 - Broken Campaign Links Are Launch-Risk Debt

Problem: `CAMPAIGN_01_FIRST_SCREENSHOT_DROP.md` contained corrupted `C` characters in headings, HECTON-8 names, and required file paths. That would make the first screenshot drop route to non-existent files such as broken QA/Content/Steam paths. Paid-test stop rules also still used old co-op shorthand, and the SN2 monitoring snapshot had older volatile counts after a fresh Steam API check.

Solution: Repaired Campaign 01 headings, project names, path references, and checklist language. Rewrote its critique/micro-feedback snippets from `not asking` negative-denial phrasing to positive feedback-only / preview-route language. Normalized paid-test stop rules to unsupported multiplayer-scope expectation wording. Added Steam review API refresh V1 to the competitor monitoring doc. Added RISK-045 so public/paid traffic is blocked unless official Steam URL, contact, UTM, and social custody preflight is factual.

Rejected Alternatives: Treating corrupted Campaign 01 text as cosmetic would leave the first public campaign checklist unsafe. Replacing the whole campaign doc would risk losing existing gates. Using older SN2 counts after a fresh API call would weaken evidence labeling. Hiding route-custody traffic failure inside only Campaign 02 would make paid ads and CTAs easy to bypass.

Scalability potential: Low budget execution now has a usable first screenshot checklist with real links and no paid-spend ambiguity. Middle/High/Ultra launch operations can scale traffic only after official route custody passes, while competitor monitoring remains a volatile internal signal instead of public attack material.

Hardware Impact: 0us measured runtime impact. Docs-only. No campaign, ad spend, public post, outreach, account/browser action, runtime, or build action occurred.

## Decision 125 - Mode-Scope Shorthand Must Not Leak Into Send Gates

Problem: After Campaign 01 repair, active docs still used `co-op expectation/confusion/implication` as operational shorthand in experiments, Steam copy tests, playtest tags, capture/QA forms, crisis triage, access stop rules, curator status, and CRM copy-like notes. That shorthand is acceptable in direct FAQ contexts, but it is too easy to paste into public/send material or misread as a feature debate.

Solution: Normalized active gates and copy-like CRM notes to multiplayer-scope expectation/implication wording. Preserved direct FAQ/request handling, factual source/tag mentions, and explicit forbidden-copy examples. CSV parse passed and live CRM status distribution stayed unchanged.

Rejected Alternatives: Removing all `co-op` strings would damage direct support clarity and factual competitor/source descriptions. Leaving shorthand in test fields and CRM notes would make the previous Promise Lint cleanup incomplete.

Scalability potential: Low budget operators can run cold reads, playtest screening, and asset QA without translating shorthand. Middle/High/Ultra outreach can scale send packets while keeping public scope discipline consistent across CRM, Steam, QA, and access docs.

Hardware Impact: 0us measured runtime impact. Docs/data-only. No CRM status change, outreach, public post, account/browser action, runtime, or build action occurred.

## Decision 126 - Public Copy Seeds Must Carry The Positive Boundary

Problem: Several proactive copy seeds still used negative-denial phrasing such as `not asking`, `not a cozy base`, `not black screenshots`, and `not making performance claims`. Those phrases can be acceptable in direct Q&A or policy, but proactive posts, site copy, Steam copy seeds, and creator-facing proof notes should state the positive proof boundary directly.

Solution: Rewrote active post, site, community, brand, Steam, and creator-proof snippets to positive language: feedback-only, pressure-rated infrastructure, readable darkness, and performance language waiting for measured evidence. Preserved direct request/FAQ, policy, and forbidden-copy contexts.

Rejected Alternatives: A blind deletion of all negative wording would weaken support and policy clarity. Leaving proactive negative-denial snippets would keep violating the Promise Lint rule at the clipboard layer.

Scalability potential: Low budget public posts and site text can be pasted with less rewrite load. Middle/High/Ultra launch surfaces keep the same proof-boundary language across social, Steam, site, and creator notes.

Hardware Impact: 0us measured runtime impact. Docs-only. No public post, site publish, outreach, account/browser action, runtime, or build action occurred.

## Decision 127 - Corrupted Calendar Text Is Execution Risk

Problem: `Schedule/90_DAY_MARKETING_OPERATIONS_CALENDAR.md` had corrupted tokens in project names, headings, and file references. Even while the current state is G0/no public assets, a broken schedule can send a future agent toward non-existent routes or make old lead-volume timing look active.

Solution: Repaired the corrupted schedule text and kept the 2026-05-19 scheduling override explicit: the calendar is a sequence model only until rough in-game screenshots exist. Lead verification weeks now remain conditional on a real CRM/asset-fit gap rather than default volume work.

Rejected Alternatives: Deleting the schedule would lose useful launch sequencing. Leaving the corruption would preserve broken links. Re-enabling the old 90-day lead-volume calendar would conflict with the current asset-proof bottleneck.

Scalability potential: Low budget keeps agents on asset readiness and proof-first copy instead of raw lead churn. Middle/High/Ultra launch operations can reuse the repaired calendar later once asset, Steam, route custody, and creator utility gates are factual.

Hardware Impact: 0us measured runtime impact. Docs-only. No calendar execution, public post, outreach, account/browser action, runtime, or build action occurred.

## Decision 128 - Code-Styled Paths Must Resolve Or Stop Looking Like Paths

Problem: After the calendar repair, a Marketing backtick-path audit still found missing references: real files were cited without their subfolders, presskit output packet names were formatted as repo files, and old placeholder names in cleanup notes looked like files to create.

Solution: Converted real references to existing Marketing paths, changed presskit output filenames into packet labels where no repo file should exist, and removed code-style formatting from "do not create" placeholder names. The audit now returns `BACKTICK_PATH_AUDIT_OK`.

Rejected Alternatives: Creating empty placeholder presskit files would increase doc sprawl. Ignoring the audit would preserve broken routes. Removing all code formatting would weaken useful path readability.

Scalability potential: Low budget agents can navigate existing docs without chasing missing files. Middle/High/Ultra launch prep can assemble presskit/campaign work from real owners without duplicating docs.

Hardware Impact: 0us measured runtime impact. Docs-only. No public post, outreach, account/browser action, runtime, or build action occurred.

## Decision 129 - Asset Metadata Should Name The Actual Scope Boundary

Problem: The asset metadata schema still used an old mode-specific field name for multiplayer-scope review. The field values were already generalized, but the old column name can reintroduce shorthand into first capture intake and future dashboard joins.

Solution: Renamed the asset-side field to `multiplayer_scope_check` in the CSV template and asset-library schema. This was done before any real captured asset rows exist, so there is no historical send/evidence record to migrate.

Rejected Alternatives: Keeping the old name would preserve avoidable shorthand. Renaming later would be riskier after real rows exist. Creating a parallel alias column would increase schema ambiguity.

Scalability potential: Low budget capture uses one clear claim gate. Middle/High/Ultra launch operations can scale asset review without translating old shorthand across metadata, QA, and campaign docs.

Hardware Impact: 0us measured runtime impact. Docs/data-only. No asset approval, outreach, account/browser action, runtime, or build action occurred.

## Decision 130 - First-Capture Control Files Should Not Teach Old Shorthand

Problem: The QA checklist and daily loop still used narrow mode shorthand in non-FAQ operational gates. These files are likely to be read during first capture and first asset scoring, so their language should match the broader public-scope gate.

Solution: Rewrote the relevant QA and daily-loop checks to unsupported multiplayer-scope wording while leaving direct FAQ/support contexts elsewhere intact.

Rejected Alternatives: A broad repository-wide deletion would damage direct Q&A clarity. Keeping shorthand in first-capture control files would weaken the schema rename and Promise Lint cleanup.

Scalability potential: Low budget capture agents get one clear claim gate. Middle/High/Ultra campaign operations can scale asset review without translating old shorthand into public-copy risk.

Hardware Impact: 0us measured runtime impact. Docs-only. No asset approval, outreach, account/browser action, runtime, or build action occurred.

## Decision 131 - Reject Codes Are Part Of The Schema

Problem: The asset metadata field was renamed, but the fixed rejection-code list still carried a narrow legacy code for unsupported multiplayer-scope implication. Reject codes are copied into CSV rows and dashboards, so stale vocabulary becomes operational truth.

Solution: Renamed the fixed code to `UNSUPPORTED_MULTIPLAYER_SCOPE` and verified the old code and old field name no longer appear across Marketing/status/rationale/log.

Rejected Alternatives: Keeping the legacy code would preserve avoidable translation work. Adding both codes would split analytics and make dashboard filters ambiguous.

Scalability potential: Low budget capture review gets one scope-failure code. Middle/High/Ultra marketing analysis can aggregate reject causes without alias cleanup.

Hardware Impact: 0us measured runtime impact. Docs-only. No asset approval, outreach, account/browser action, runtime, or build action occurred.

## Decision 132 - Campaign And Dashboard Gates Must Consume The Renamed Fields

Problem: Renaming asset metadata is not enough if Campaign 01 and KPI tables still advance from partial metadata or track old narrow comment categories. That would split the first-public decision between asset truth and dashboard truth.

Solution: Added `multiplayer_scope_check`, `performance_claim_check`, and `feature_truth_check` to the Campaign 01 required asset metadata gate, and renamed first-public beat tracking to `multiplayer_scope_comments`.

Rejected Alternatives: Relying on QA score alone would ignore claim safety. Keeping a narrow dashboard comment field would undercount unsupported multiplayer-scope confusion.

Scalability potential: Low budget first screenshot tests can block false-scope assets before public traffic. Middle/High/Ultra campaign analysis can compare confusion themes without schema aliases.

Hardware Impact: 0us measured runtime impact. Docs-only. No campaign, public post, asset approval, outreach, account/browser action, runtime, or build action occurred.

## Decision 133 - Top-Level Control Must Name The Claim Gates

Problem: The control tower is the first file future agents read. If it only says build/source/QA/utility, agents can fill metadata while skipping claim-safety fields that are now required by Campaign 01 and the asset schema.

Solution: Updated the control tower asset row, immediate work list, and priority 1 to name `multiplayer_scope_check`, `performance_claim_check`, and `feature_truth_check`.

Rejected Alternatives: Leaving this buried in the asset-library doc would recreate the same top-level/deep-gate drift that caused prior cleanup work.

Scalability potential: Low budget capture agents see the full gate immediately. Middle/High/Ultra campaign operations keep one route from control tower to metadata to campaign decision.

Hardware Impact: 0us measured runtime impact. Docs-only. No campaign, public post, asset approval, outreach, account/browser action, runtime, or build action occurred.

## Decision 134 - README Is Also An Entry Point

Problem: The control tower was aligned, but `README.md` is still a likely first file for a fresh agent. If README only mentions QA and creator utility, a public asset can appear safe from the index while missing claim-check fields.

Solution: Added the three asset claim-check fields to README hard rules and first asset gate.

Rejected Alternatives: Depending on the control tower alone would ignore the documented entry index. Duplicating the full schema in README would be noisy; naming the required fields is enough.

Scalability potential: Low budget agents get the same gate from either entry point. Middle/High/Ultra launch prep reduces asset-review drift as more people touch the folder.

Hardware Impact: 0us measured runtime impact. Docs-only. No public post, asset approval, outreach, account/browser action, runtime, or build action occurred.

## Decision 135 - Validation Must Live In The Workflow

Problem: The cleanup work depends on a repeatable validation pattern: file count, CSV parsing, CRM split, asset metadata headers, legacy/corruption grep, and path audit. If this only exists in terminal history, the next agent will skip it or invent a weaker check.

Solution: Added `End-Of-Change Validation Cut V0` to the daily agent loop with exact checks and expected outcomes for docs/data-only Marketing work.

Rejected Alternatives: Creating a new validation document would add sprawl. Keeping commands only in status/log would make them harder to find during daily execution.

Scalability potential: Low budget agents can verify docs/data hygiene without asking. Middle/High/Ultra campaign operations can preserve CSV/path/schema consistency as more people touch the marketing folder.

Hardware Impact: 0us measured runtime impact. Docs-only. No public post, asset approval, outreach, account/browser action, runtime, or build action occurred.

## Decision 136 - AgentOps Must Not Bypass The Daily Loop

Problem: `AgentOps/AGENT_MARKETING_WORKFLOWS.md` is a likely starting point for future agents, but it did not point to the validation cut or require the new asset claim-check fields in the pre-asset loop and outreach batch protocol.

Solution: Added claim gates, creator utility/send gates, and Daily Loop validation to AgentOps. Outreach batches now require passing asset metadata claim checks before creator-facing use.

Rejected Alternatives: Depending on README/control tower alone would leave AgentOps as a bypass route. Creating another agent workflow doc would be sprawl.

Scalability potential: Low budget agent labor starts with proof assets and validation. Middle/High/Ultra outreach can scale batches without skipping metadata gates.

Hardware Impact: 0us measured runtime impact. Docs-only. No public post, asset approval, outreach, account/browser action, runtime, or build action occurred.

## Decision 137 - Draft Banks Must Not Reintroduce Naming Drift

Problem: Social, measurement, event, Steam, showcase, and press draft banks still contained inconsistent brand spelling, space-separated planned asset labels, malformed table delimiters, and touched-file legacy scope shorthand. These drafts are likely clipboard sources during public-account setup, event prep, and press outreach.

Solution: Normalized the touched files to consistent `HECTON-8` naming, hyphenated `PLAN-*` asset IDs, multiplayer-scope/proof-boundary language, and valid Markdown table delimiters. Kept the change inside existing Marketing files and updated backlog/source/status/log trace.

Rejected Alternatives: Creating a new style guide would add sprawl. Leaving draft banks inconsistent would make the existing Promise Lint and asset metadata gates weaker at the clipboard layer. Registering accounts or posting now remains rejected without project email, vault, recovery, 2FA, and backup-code custody.

Scalability potential: Low budget execution can copy profile/event/press drafts with fewer manual corrections. Middle/High/Ultra launch operations can scale social, press, and event prep from the same proof-bound vocabulary without alias cleanup.

Hardware Impact: 0us measured runtime impact. Docs-only. File count and CSV parse stayed clean; CRM row count/status split stayed unchanged. No public post, account/browser action, outreach, runtime, or build action occurred.

## Decision 138 - Fix Generators, Not Only Generated Copy

Problem: After active draft normalization, the CRM schema, raw-lead scraper, generated raw summary, curator CSV, launch war-room, pre-screenshot campaign, and Steam page iteration plan still had active readiness wording that could regenerate or copy stale scope phrasing. Two touched docs also had malformed HTML boundary comments.

Solution: Updated the owner schema, generator, generated summary, curator row, launch/campaign/page gates, and two doc-boundary markers. The generator and output now carry the same claim-check wording, so the cleanup survives future raw-lead regeneration.

Rejected Alternatives: Editing only the generated summary would leave the PowerShell generator as a regression source. Ignoring malformed boundary comments would weaken the documentation actuality marker. Editing historical ledger rows was rejected because those are audit context, not active send/copy gates.

Scalability potential: Low budget agent passes can rerun raw-lead tooling without reintroducing stale wording. Middle/High/Ultra campaign operations can scale curator, launch, and Steam page checks from the same claim vocabulary.

Hardware Impact: 0us measured runtime impact. Docs/data/tooling-only. CSV parse stayed clean; Marketing file count stayed 100; CRM row count/status split stayed unchanged. No public post, account/browser action, outreach, runtime, or build action occurred.

## Decision 139 - Active Gates Need The Same Scope Vocabulary

Problem: After the generator/schema cleanup, several active route gates still used narrow mode shorthand in Steam tag/page/pricing/demo plans, presskit/curator/angle plans, launch and campaign stop rules, trailer/prep/devlog notes, regional/audience/budget guidance, and creative gates.

Solution: Replaced the shorthand in active operational gates with multiplayer-scope or unsupported-mode wording. Preserved source data, competitor monitoring, direct FAQ/request handling, explicit forbidden-copy examples, and historical ledger/backlog rows as evidence or policy contexts.

Rejected Alternatives: Blind repository-wide deletion would damage direct support answers and source evidence. Leaving active route gates mixed would keep producing inconsistent send/page/campaign decisions.

Scalability potential: Low budget agents can run first asset, Steam, press, creator, and launch checks with one vocabulary. Middle/High/Ultra operations can scale campaigns without reclassifying old mode-specific aliases.

Hardware Impact: 0us measured runtime impact. Docs/data-only. No public post, account/browser action, outreach, runtime, or build action occurred.

## Decision 140 - High-Level Send Cadence Cannot Be Weaker Than The Send Packet

Problem: The first-human-send packet already required creator utility and asset send gates, but the phase-level bullets for screenshot, Steam-page, demo/key, and event reminder outreach still used broader "asset fit" wording. A future sender could follow the cadence section and skip the stricter packet below.

Solution: Updated the high-level cadence in the outreach workflow and Next Fest/event campaign so every creator-facing send path names creator utility, asset `creator_send_gate`, official route verification, asset-fit, and CRM send-log gates.

Rejected Alternatives: Relying on the lower section alone would leave an ordering hazard. Creating another send checklist would add sprawl. Blocking all reminder prep would be too strict; the gate now allows prep but keeps sends blocked.

Scalability potential: Low budget outreach stays small and gated. Middle/High/Ultra creator batches can scale only when each asset/recipient pairing has structured proof.

Hardware Impact: 0us measured runtime impact. Docs-only. No creator outreach, public post, account/browser action, runtime, or build action occurred.

## Decision 141 - Empty Send Fields Need Explicit HOLD Meaning

Problem: The CRM now has structured send-log fields, but all of them are currently empty because no outreach has happened. Without an explicit HOLD rule, a future agent could treat those blanks as admin gaps and fill them from draft intent instead of from a real human send backed by asset metadata.

Solution: Added current send-state safety rules to the outreach workflow and CRM schema owner doc: CRM send-log fields stay empty until real send proof exists, and all 13 planned asset rows remain blocked at `creator_send_gate = BLOCKED_PLANNED_CAPTURE` with `creator_utility_score = 0` until capture/QA/recipient utility review opens them. Also repaired ambiguous score-band text in the same schema owner doc.

Rejected Alternatives: Adding a new send-state document would be sprawl. Leaving the state only in terminal validation output would be lost after compaction. Filling the CRM preemptively would create false send evidence.

Scalability potential: Low budget outreach remains blocked and auditable. Middle/High/Ultra creator batches can scale from explicit asset-side gates and CRM send logs instead of inferred spreadsheet intent.

Hardware Impact: 0us measured runtime impact. Docs-only. No creator outreach, public post, account/browser action, runtime, or build action occurred.

## Decision 142 - Phase Names Cannot Open Creator Sends

Problem: Several alternate entry points still made creator sends sound phase-driven: segment timing, reusable pitch templates, Steam page launch, demo outreach, Next Fest readiness, measurement expansion, and AgentOps verifier language. Those routes could bypass the stricter first-human-send packet by treating "Steam page live" or "demo ready" as send authorization.

Solution: Propagated the same creator-send gate to those existing docs. Creator-facing sends now require asset metadata claim checks, creator utility 3/4+, open `creator_send_gate`, same-day official contact route, Promise Lint, and CRM send-log readiness regardless of whether the trigger is Steam page, demo, Next Fest, pitch bank, segment plan, or AgentOps.

Rejected Alternatives: Keeping the gate only in the main outreach workflow would leave alternate starting points unsafe. Creating a new master send checklist would add sprawl. Blocking prep entirely would waste useful copy work; the patch allows prep but blocks send execution.

Scalability potential: Low budget sends stay small and evidence-bound. Middle/High/Ultra campaigns can expand from reply/coverage evidence only while preserving the same per-recipient gate.

Hardware Impact: 0us measured runtime impact. Docs-only. No creator outreach, public post, account/browser action, runtime, or build action occurred.

## Decision 143 - Visual QA Alone Is Not A Public Post Gate

Problem: The post bank said public social posts could run from visual-only QA. That conflicts with the current asset metadata model: a visually strong frame can still imply unsupported scope, fake performance, missing feature proof, or a broken CTA/link route.

Solution: Replaced the visual-QA-only wording with a public asset gate: public posts require screenshot/clip QA plus `multiplayer_scope_check`, `performance_claim_check`, `feature_truth_check`, and relevant link/custody gates. Creator warmups add creator utility, open `creator_send_gate`, route proof, and CRM send-log readiness. The A-tier, Priority 50, presskit, and launch timeline copy surfaces now use approved links/assets only.

Rejected Alternatives: Relying on Campaign 01 or control tower alone would leave copy-bank users one paste away from bypassing claim checks. Deleting the copy banks would remove useful prep. Keeping `[TBD]` placeholders without "approved only" language would preserve false readiness.

Scalability potential: Low budget public posting remains proof-bound. Middle/High/Ultra campaign execution can reuse copy banks without weakening asset metadata authority.

Hardware Impact: 0us measured runtime impact. Docs-only. No public post, creator outreach, account/browser action, runtime, or build action occurred.

## Decision 144 - Access Sends Need Custody Before Recipient Fit

Problem: Review-key and preview-access docs verified recipient fit, route, and build state, but they did not make official inbox custody, asset metadata claim checks, approved links, and key/access log readiness explicit preconditions. That leaves a route where a stable build plus a verified recipient could trigger access from the wrong account or with unapproved assets.

Solution: Updated access protocol, key compliance, Curator Connect playbook, and press templates. Access sends now require owner-controlled inbox/contact, approved asset/build/access links, claim-checked referenced assets, and key/access log readiness. Curator Connect remains preferred for curators and raw external keys stay blocked by default.

Rejected Alternatives: Relying on general "verified contact" language is too weak for keys. Creating a separate key-custody document would add sprawl. Touching actual key systems or Steamworks was rejected because no project account/build/key approval exists in this context.

Scalability potential: Low budget outreach avoids key scams and orphan credentials. Middle/High/Ultra launch operations can scale preview access from a clean log and owner-controlled channel.

Hardware Impact: 0us measured runtime impact. Docs-only. No keys, access, public post, account/browser action, runtime, or build action occurred.

## Decision 145 - Account Permission Does Not Remove Custody Gates

Problem: The user explicitly allowed browser/account work, but the official-surface docs must still prevent orphan social accounts, premature public posts, dead email lists, and unmanaged Discord launch. Social first-public gates still named QA more strongly than asset metadata claim checks, and owned-audience/Discord gates did not fully name inbox/list/admin custody.

Solution: Updated social setup, owned audience, and Discord setup. Social posting now requires QA plus asset metadata claim checks and official link/custody gates. Email/signup waits for owner-controlled inbox/list provider, unsubscribe, and approved URLs. Discord open gate now requires claim-checked assets, moderation roles, and owner-controlled admin/recovery custody. Cadence ranges were made explicit.

Rejected Alternatives: Creating/logging into accounts now would create orphan credentials without project email/vault/2FA custody. Leaving account docs as "ask user later" would waste the current pass. Blocking all future agent browser assistance was rejected; the docs preserve a safe assisted-browser mode.

Scalability potential: Low budget social can reserve and stay quiet without trust damage. Middle/High/Ultra community operations can scale account, email, and Discord surfaces from owner-controlled custody and proof assets.

Hardware Impact: 0us measured runtime impact. Docs-only. No account registration, browser login, public post, signup form, Discord server, runtime, or build action occurred.
