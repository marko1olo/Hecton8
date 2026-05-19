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
