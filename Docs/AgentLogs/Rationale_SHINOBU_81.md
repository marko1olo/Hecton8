# Rationale SHINOBU_81

Agent: SHINOBU_81
Domain: COMPETITIVE_INTELLIGENCE_AND_UX_ANALYST
Task count: 13

## Decision 146 - CTA Gate Must Reach Clipboard Sources

Problem: The Official CTA Link Activation Gate existed in analytics/campaign policy, but pasteable surfaces still contained raw or semi-ready CTA language such as `Steam: [URL]`, `Presskit: [URL]`, `title + Steam wishlist`, and showcase `Steam CTA` requirements.

Solution: Patched existing social, post-bank, community, trailer, campaign, Steam, audience, and press/showcase docs so CTA text either names an approved CTA activation packet or falls back to no-link feedback/end-card copy. Updated row 117, source ledger, and RISK-048.

Rejected Alternatives: Creating a new CTA checklist would add sprawl and still leave old snippets pasteable. Browser/account work remains rejected without project email, password-manager custody, recovery, 2FA, and backup-code storage.

Scalability potential: Low budget execution can run critique and account warmup without public dead links. Middle/High/Ultra launch operations can scale Steam, trailer, newsletter, press, and showcase beats from the same approved CTA packet.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No public link, Steam page, signup form, account/browser action, outreach, runtime, or build action occurred.

## Decision 147 - Public CTA And Private Access Are Different Routes

Problem: Demo/playtest, key/access, campaign, and showcase docs had public CTA and private access language in the same operating area. Without an explicit route class, a private build/key/playtest link could be copied into a public event, social bio, presskit, or trailer CTA.

Solution: Added public/private route separation. Public Steam/demo links require CTA activation. Private Steam Playtest, demo key, review key, and preview access routes require recipient verification, access log readiness, route class, known-issues copy, and revocation/stop rules. Added RISK-049 and row 118.

Rejected Alternatives: Treating all approved links as equivalent was rejected because private access and public conversion links have different failure modes. Creating a new master access doc was rejected; the actual route-owner docs were patched.

Scalability potential: Low budget testing can stay invite-only without accidental public leakage. Middle/High/Ultra launch operations can run public events, creator previews, press access, and playtests in parallel without route contamination.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No public demo, key, Playtest, preview access, account/browser action, outreach, runtime, or build action occurred.

## Decision 148 - Feedback Contacts Are Not A Single Pool

Problem: Tester screening, newsletter signup, bug support, Steam feedback, creator replies, and press contacts can all collect emails or handles, but they do not grant the same permission. Without provenance fields, future operators could import support/playtest contacts into newsletter, creator CRM, press lists, Discord roles, or public marketing flows.

Solution: Added consent/provenance and route class requirements to recruitment, signup forms, feedback triage, support templates, and launch dry run. Added form provider custody and RISK-050. Contact routes stay separated unless explicit opt-in exists for the target route.

Rejected Alternatives: A single master "contacts" spreadsheet would be faster but unsafe. Publishing personal/agent-owned forms was rejected because the owner would not control data export, deletion, unsubscribe, or recovery.

Scalability potential: Low budget playtests can stay clean without list contamination. Middle/High/Ultra campaigns can scale support, newsletter, tester, creator, and press operations without consent ambiguity.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No form was published, no tester recruited, no account/browser action, no outreach, runtime, or build action occurred.

## Decision 149 - Route And Consent Must Survive Reporting

Problem: Consent/provenance and route class existed in recruitment, feedback, support, and launch procedures, but KPI and analytics tables could still summarize generic feedback/contact/link rows without those fields. That would let unsafe source mixing reappear as "dashboard signal".

Solution: Added route class and consent/provenance fields to dashboard community/first-beat rows, campaign event logs, feedback coding, measurement packets, weekly reports, and the daily KPI clerk/noon kill check. CTA activation is explicitly public-link only; private access uses access logs and route-class proof instead of public UTM packets.

Rejected Alternatives: A separate reporting checklist was rejected because it would not prevent the actual tables from accepting incomplete rows. Browser/account/form creation remains rejected without project-owned custody, recovery, 2FA, and explicit route ownership.

Scalability potential: Low budget operations can avoid polluting dashboards with support/tester/press/creator contacts. Middle/High/Ultra campaigns can scale public CTA, private access, support, playtest, creator, and press reporting without losing source legality or operational meaning.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No public link, form, support route, account/browser action, outreach, runtime, or build action occurred.

## Decision 150 - Entry Points Must Not Hide Reporting Gates

Problem: `MARKETING_CONTROL_TOWER.md`, `README.md`, and `PREP_DIRECTIONS_NOW.md` did not expose route class and consent/provenance as first-read reporting rules. A future agent could start from the correct entry docs, count feedback/contact/link signal, and never open the deeper KPI/analytics gate first.

Solution: Added route/consent reporting constraints to the control tower operating state, public boundaries, now-actions, top priorities, README hard rules/directory map, first asset gate note, and prep KPI direction. Added backlog row 121 and source ledger trace.

Rejected Alternatives: Duplicating the full KPI schema in entry docs would add noise. The entry docs now carry only the enforcement rule and route users to the authoritative KPI/analytics files.

Scalability potential: Low budget operations avoid bad manual summaries. Middle/High/Ultra campaigns can hand off from entry docs to analytics without losing route permission or contact provenance.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No public link, form, support route, account/browser action, outreach, runtime, or build action occurred.

## Decision 151 - Pasteable Posts Need Reporting Metadata

Problem: Safe no-link and first-public drafts existed, but the pasteable post surfaces did not consistently say how to log route class and consent/provenance after publication. The likely failure mode is counting replies as generic demand, creator interest, newsletter consent, or playtest interest without source permission.

Solution: Added route/reporting rules to the post bank, community templates, and social playbook. No-link posts default to `route_class = no_link_feedback`; public CTA posts require CTA activation; public replies are `consent_provenance = public_comment` only and cannot be imported into newsletter, creator CRM, press, or playtest routes.

Rejected Alternatives: Creating a separate posting report checklist was rejected because operators paste from the copy bank and social playbook. The route metadata now sits next to the text most likely to leave the docs.

Scalability potential: Low budget accounts can post critique questions without corrupting contact lists. Middle/High/Ultra campaigns can scale public posts and CTA traffic while keeping feedback source permissions machine-readable.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No post, account/browser action, public CTA, outreach, runtime, or build action occurred.

## Decision 152 - Creator Replies Need Their Own Provenance Fields

Problem: KPI/reporting gates required creator replies to stay separated from newsletter, playtest, press, and public-comment routes, but the live CRM had no structured field for send route or reply consent provenance. Operators would have to hide that fact in notes, making it unfilterable.

Solution: Added `send_route_class` and `reply_consent_provenance` to the live CRM, schema, first human-send packet, send-state HOLD check, validation cut, control tower, README, and risk register. Creator replies default to creator-route signal only unless explicit opt-in to another route is recorded.

Rejected Alternatives: A generic `route_class` column was rejected in the creator CRM because it would collide with dashboard vocabulary and obscure whether the field describes the send or the reply. Notes-only provenance was rejected because it is not machine-filterable.

Scalability potential: Low budget outreach can track replies without contaminating other lists. Middle/High/Ultra campaigns can run creator feedback, public CTA creator sends, and private access sends in parallel while preserving consent boundaries.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No CRM row status changed, no outreach, account/browser action, public CTA, runtime, or build action occurred.

## Decision 153 - Press And Curator Replies Need The Same Route Firewall

Problem: Press and curator trackers had contact/status/notes but no structured send-route or reply-provenance fields. That creates the same contamination risk as creator replies: a press reply, curator reply, or key request could be copied into newsletter, playtest, or public-audience reporting without explicit consent.

Solution: Added `send_route_class` and `reply_consent_provenance` to press and curator CSV trackers, and updated press seed map, key compliance, review/access protocol, and risk register. Press/curator replies stay in press/curator provenance unless an explicit separate opt-in exists.

Rejected Alternatives: Notes-only tracking was rejected because it cannot be filtered. A separate press consent file was rejected because the tracker is the operating surface for press/curator rows.

Scalability potential: Low budget press triage can stay clean without contact-list contamination. Middle/High/Ultra campaign operations can run press, curator, key, and private access workflows in parallel without merging permissions.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No press send, curator send, key issue, account/browser action, public CTA, runtime, or build action occurred.

## Decision 154 - Empty Asset Folders Are Useful, But Not Proof

Problem: Metadata and shotlist paths pointed to `MarketingAssets/...`, but the local directory tree did not exist. First capture work would have to create folders ad hoc, increasing the chance of wrong paths or scattered media. The opposing risk is worse: empty folders can be misread as asset readiness.

Solution: Created the documented empty `MarketingAssets/` skeleton at repo root and updated asset ops, backlog, and source ledger to state that this is directory custody only. No media files or `.gitkeep` placeholders were added.

Rejected Alternatives: Adding placeholder files was rejected because it would create fake artifacts. Leaving the folder absent was rejected because the first capture pass needs exact destinations already agreed.

Scalability potential: Low budget capture can drop raw files into known folders. Middle/High/Ultra asset production can scale into screenshots, video, Steam, presskit, creator packs, localized exports, and archive without path drift.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA plus empty local directories only. No asset proof, account/browser action, runtime, or build action occurred.

## Decision 155 - Touched Backlog Tables Must Be Auditable

Problem: `Data/MARKETING_BACKLOG_INDEX.md` had P1/P2 sections with `ID | Task | Output` headers but two-column rows. Once the file was touched for asset-directory trace, the malformed tables blocked a simple markdown table audit.

Solution: Changed only those P1/P2 headers to `ID | Task`, matching existing row content. No tasks, priorities, owners, or statuses changed.

Rejected Alternatives: Ignoring the audit failure was rejected because the backlog is the active task router. Adding empty output cells to every row was rejected because it would create fake or low-value output data.

Scalability potential: Low budget agent work can parse the backlog reliably. Middle/High/Ultra operations can add output columns later only where rows actually carry output data.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No runtime, build, account/browser action, outreach, or public action occurred.

## Decision 156 - SN2 Pain Signals Must Stay Fresh And Private

Problem: The SN2 pain-to-proof map was based on V2 review/API data while SN2 review volume was still changing during launch week. Stale pain buckets could push HECTON capture priorities toward yesterday's complaints or tempt public competitor-attack copy.

Solution: Fetched a fresh public Steam appdetails/review API snapshot and recorded V3 in `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md`: all-language 64,212 positive / 5,828 negative / 70,040 total and English 38,715 positive / 2,616 negative / 41,331 total, both `Very Positive`. Recent 100-negative samples still point directionally at agency/base/trust/content, but the risk register and backlog now keep that as private capture-priority evidence only.

Rejected Alternatives: Treating V2 as good enough was rejected because live competitor sentiment is volatile. Turning negative sample term hits into public claims was rejected because term counts are not percentages, not representative proof, and would make HECTON look petty.

Scalability potential: Low budget capture can prioritize the few assets that answer real current expectations. Middle/High/Ultra marketing can keep competitor monitoring current without changing the public stance: HECTON-positive, proof-first, competitor-neutral.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA plus public Steam API reads only. No public copy, outreach, account/browser login, asset approval, runtime, or build action occurred.

## Decision 157 - Campaign 01 Needs Agency Proof, Not Only Pretty Identity

Problem: QA first-pack composition already expected threat/scale, but Campaign 01 required inputs could still advance from `PLAN-SHOT-001`, `PLAN-SHOT-003`, and one base/machinery still. After SN2 V3, agency/no-weapon and base/content wording remained visible in recent negative samples. A first public test without a readable decision/agency proof would answer the wrong market fear.

Solution: Updated `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md` with a V3 priority note, changed QA first-pack composition to require one agency/decision proof, and changed Campaign 01 inputs to require `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` in addition to identity, player verb, and base/machinery proof.

Rejected Alternatives: Reordering the whole shotlist was rejected because identity and player verb remain mandatory. Adding another competitor memo was rejected because the failure mode is execution drift, not missing analysis.

Scalability potential: Low budget capture spends first-session time on fewer higher-proof assets. Middle/High/Ultra marketing can still expand into anomaly/capsule work later, but only after the first public packet proves player choice under pressure.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No screenshot, clip, public post, outreach, account/browser action, runtime, or build action occurred.

## Decision 158 - Post Queue Must Enforce The Same Agency Gate

Problem: The first 72-hour post-bank sequence could still pick identity, Reddit base critique, salvage/machinery, and then decide proceed/revise/kill without forcing an agency/decision proof beat. That would bypass the Campaign 01 gate at the exact copy surface operators use.

Solution: Updated `Content/POST_BANK_AND_HOOK_LIBRARY.md` so the sequence refuses to run without `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`, prioritizes POST-005/008/010 if agency proof is not visible by Hour 24, and blocks Hour 72 proceed unless viewers understand one decision/agency proof without a caption.

Rejected Alternatives: Leaving the rule only in Campaign 01 was rejected because operators paste from the post bank. Adding another checklist was rejected because the queue table itself is the execution surface.

Scalability potential: Low budget posting avoids wasting the first window on attractive but strategically weak assets. Middle/High/Ultra campaigns can still branch into Steam/news/creator work after the same proof gate is visible in public response.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No public post, outreach, account/browser action, asset approval, runtime, or build action occurred.

## Decision 159 - Entry Docs Must Carry The New First-Packet Shape

Problem: The agency/decision gate was enforced in Campaign 01, QA, shotlist, and post-bank, but `MARKETING_CONTROL_TOWER.md`, `README.md`, and `PREP_DIRECTIONS_NOW.md` still described the first asset packet in broader terms. A future agent could start from the entry docs and miss the new first-packet shape.

Solution: Propagated the requirement to entry docs: the first public packet needs identity, player verb, base/machinery, and one agency/decision proof asset before Campaign 01 or broad outreach can advance.

Rejected Alternatives: Duplicating the full Campaign 01 table was rejected because entry docs should carry only the enforcement rule and point into the owning docs. Leaving the rule deep-only was rejected because the control tower is the first-read map.

Scalability potential: Low budget execution starts with the minimum useful packet. Middle/High/Ultra marketing can expand the packet later without losing the first proof sequence.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No screenshot, clip, public post, outreach, account/browser action, runtime, or build action occurred.

## Decision 160 - Steam And Social Launch Surfaces Must Not Bypass Agency Proof

Problem: Social first-post setup and Steam page assembly could still start from identity, salvage/player verb, base/machinery, and capsule proof without requiring the agency/decision asset that Campaign 01 now demands. `PLAN-SHOT-007` anomaly flavor could also be treated as a threat/anomaly substitute despite not proving player choice.

Solution: Updated social first posts, Steam store copy matrix, Steam asset checklist, and Campaign 02 launch gate. First public posts and Steam launch surfaces now require `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`; `PLAN-SHOT-007` can add anomaly flavor but cannot satisfy agency proof.

Rejected Alternatives: Assuming Campaign 01 would protect downstream Steam/social surfaces was rejected because these docs are separate execution entry points. Treating anomaly proof as agency proof was rejected because mystery is not a player decision.

Scalability potential: Low budget launch prep avoids a weak first Steam page that looks atmospheric but passive. Middle/High/Ultra marketing can still use anomaly assets after decision proof exists.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No Steam page, social post, outreach, account/browser action, asset approval, runtime, or build action occurred.

## Decision 161 - Press, Curator, Demo, And Event Surfaces Need The Same Agency Gate

Problem: After Steam/social were tightened, presskit, Curator Connect, wishlist iteration, Next Fest, showcase, and demo/playtest docs still allowed weaker gates such as first three screenshots, threat, anomaly, or general gameplay loop. That leaves a second launch path where HECTON-8 could contact curators, publish a presskit, submit a showcase, or expand demo traffic with atmosphere but no visible player choice.

Solution: Propagated the agency/decision proof gate into the existing execution docs. Curator Connect, presskit publish, wishlist/Next Fest readiness, Steam review packet wording, Steam page iteration, showcase submission, and demo/playtest success now require one readable decision under threat, leak, route cost, sonar pressure, or salvage failure. `PLAN-SHOT-007` remains anomaly flavor only and cannot substitute for decision proof.

Rejected Alternatives: Creating another master gate document was rejected because these are the docs operators will use at the point of sending, submitting, or expanding. Leaving "threat/anomaly" wording in place was rejected because threat presence is not agency proof.

Scalability potential: Low budget execution avoids wasting scarce press/event shots on passive mood assets. Middle/High/Ultra campaigns can add anomaly, trailer, event, and curator beats only after a cold viewer can identify a player decision.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No curator send, press send, demo, public event submission, account/browser action, asset approval, runtime, or build action occurred.

## Decision 162 - Community Paste Surfaces Must Ask For Player Choice

Problem: Community critique templates and target-bucket prompts could still ask about mood, threat, anomaly, or darkness without asking whether the player decision is readable. That would produce noisy comments and let a passive asset look "validated" because people liked the atmosphere.

Solution: Updated community post templates, community target rules, and the public FAQ "where is gameplay" response. Threat/anomaly/mood posts now ask whether the player choice reads, and Seed Ship/anomaly critique is explicitly anomaly-only unless the first packet already has agency proof.

Rejected Alternatives: Leaving the rule in post-bank and campaign docs was rejected because humans paste directly from community templates. Asking only "is the threat clear?" was rejected because threat clarity still does not prove player agency.

Scalability potential: Low budget community testing gets better signal from fewer posts. Middle/High/Ultra campaigns can still test dread, anomaly, shorts, and Steam announcements, but the feedback loop records whether viewers saw a decision instead of only a vibe.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No community post, public reply, account/browser action, asset approval, runtime, or build action occurred.

## Decision 163 - Agency-Proof Bypass Needs A Named Risk And Daily Stop Rule

Problem: The agency/decision proof rule was now propagated to many execution surfaces, but the risk register still treated bypass mainly as creator utility. Without a named broader risk, a future agent could submit a showcase, publish a presskit, or post a community critique with mood/threat/anomaly proof and call it progress.

Solution: Added RISK-051 and inserted matching stop checks into the daily loop. Press, community, showcase, demo, curator, and wishlist surfaces now have an explicit hold condition when they lack one readable player decision under pressure.

Rejected Alternatives: Expanding RISK-042 was rejected because creator utility and public/event surface proof are different failure modes with different responses. A chat-only warning was rejected because future agents read the risk register and daily loop, not this transcript.

Scalability potential: Low budget teams avoid wasting scarce public beats on passive mood shots. Middle/High/Ultra operations can scale press, event, community, and demo surfaces while preserving the same proof floor.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No send, submit, publish, demo, community post, account/browser action, asset approval, runtime, or build action occurred.

## Decision 164 - Website, Newsletter, And Playtester Routes Are Soft Launch Surfaces

Problem: The one-page site, owned audience list, and playtester screening docs could advance from "after screenshots" or demo traffic without requiring the first packet to prove a player decision. That creates a soft public launch path outside Campaign 01.

Solution: Added agency/decision proof language to site hero/launch/presskit gates, devlog/signup modes, and playtester feedback tags/forms. Signup and playtest expansion now either require the first packet to include agency proof or explicitly measure whether players can name a pressure decision.

Rejected Alternatives: Treating website/newsletter/playtest as lower-risk internal surfaces was rejected because public links, signup forms, and playtest waves create expectations even without a Steam launch.

Scalability potential: Low budget launch prep avoids building a dead list from mood-only assets. Middle/High/Ultra campaigns can still run owned audience and playtest loops, but the first useful signal remains decision clarity, not vanity signup count.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No site, signup form, newsletter, tester recruitment, account/browser action, asset approval, runtime, or build action occurred.

## Decision 165 - Agency Proof Must Be A Structured Asset Field

Problem: Agency/decision proof had been propagated into Campaign 01, Steam, social, press, community, site, newsletter, and playtester docs, but `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` still had no structured field for it. A future capture operator could leave the decision proof buried in `notes`, and dashboards/campaign gates would be unable to filter mood-only assets from first-packet agency candidates.

Solution: Added `agency_decision_proof_gate` and `agency_decision_notes` to the asset metadata CSV and aligned the asset library workflow, QA checklist, KPI asset gate, Campaign 01, control tower, and daily loop. Only `PLAN-SHOT-006`, `PLAN-CLIP-001`, and `PLAN-CLIP-003` are pre-capture `AGENCY_PROOF_CANDIDATE` rows. Identity, machinery, capsule, anomaly, low-spec, and supporting player-verb rows now explicitly say why they cannot satisfy the first-packet agency gate alone.

Rejected Alternatives: Keeping the rule in prose was rejected because prose cannot be filtered by CSV validation or dashboard joins. Adding a new tracker was rejected because the metadata row is the owner-local truth for asset-side gates. Treating `PLAN-SHOT-007` as an agency proof was rejected because anomaly curiosity is not a player decision.

Scalability potential: Low budget capture can focus the first session on the three candidate proof assets instead of over-shooting mood frames. Middle/High/Ultra marketing can add richer anomaly, capsule, and cinematic surfaces after the same structured gate proves one readable player decision.

Hardware Impact: 0us measured runtime impact. STATIC_DATA/STATIC_DOC only. No screenshot, clip, public post, outreach, account/browser action, asset approval, runtime, or build action occurred.

## Decision 166 - Feedback Must Separate Player Verb From Player Choice

Problem: The feedback taxonomy already had `PLAYER_VERB`, but launch/demo/event surfaces can fail even when viewers see an action if they cannot name a decision, tradeoff, or consequence. Without a distinct class, "looks cool" and "I see a tool" could hide that the first route is still a passive mood demo.

Solution: Added `AGENCY_DECISION_READ` to the feedback taxonomy, demo survey, and weekly digest. Launch war-room, Campaign 03 demo outreach, and Campaign 04 Next Fest now require a readable pressure decision and block expansion when creators/players cannot name it.

Rejected Alternatives: Folding this into `PLAYER_VERB` was rejected because verb clarity and decision clarity are different failure modes. Treating it as a QA-only screenshot gate was rejected because demo and event traffic can still fail agency after assets pass visual QA.

Scalability potential: Low budget playtests can collect sharper signal from fewer people. Middle/High/Ultra campaigns can expand demo/event traffic only after the playable slice proves decision readability, not just atmosphere or production value.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No launch, demo, Next Fest commitment, outreach, account/browser action, runtime, or build action occurred.

## Decision 167 - Creator Sends Must Not Bypass Agency Proof

Problem: Creator send-readiness was gated by asset claim checks, creator utility, `creator_send_gate`, contact route, Promise Lint, and CRM send-log fields, but it did not yet consume `agency_decision_proof_gate`. That left a path where a creator-facing pressure/route-risk pitch could send identity, machinery, or mood assets without one visible decision.

Solution: Updated the mass workflow, CRM schema, segment matrix, A-tier drafts, pitch bank, and priority-50 drafts so gameplay, pressure, route-risk, threat, salvage-failure, demo-readiness, or first-public-feedback sends require one factual `AGENCY_PROOF_CANDIDATE` asset with `agency_decision_notes`. Planned candidate labels are explicitly non-proof until capture QA makes them factual.

Rejected Alternatives: Relying on `creator_utility_score` was rejected because a creator may be a good fit for a weak asset. Relying on `creator_send_gate` alone was rejected because it controls recipient fit, not whether the asset proves player choice. Editing individual pitch text was rejected because the template gates are the send surface operators reuse.

Scalability potential: Low budget outreach avoids burning high-fit creators with passive mood packets. Middle/High/Ultra campaigns can scale creator batches only after the asset packet proves player decision readability.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No creator outreach, browser/account action, public post, asset approval, runtime, or build action occurred.

## Decision 168 - Agency Proof Needs A Measurement Field, Not A Vibe

Problem: Asset metadata, creator sends, launch, demo, and feedback taxonomy now require agency proof, but `Analytics/MEASUREMENT_AND_UTM_PLAN.md` and `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md` still measured player verb and genre more directly than player decision readability. That left a measurement gap where "looks cool" could still be summarized as useful feedback while the viewer could not name a pressure decision.

Solution: Added `ab-009` as the agency/decision proof cold-read test, added `what_decision_next` and `agency_decision_read` fields to the cold-read score sheet, added `AGENCY_DECISION_READ` coding to the measurement plan, and added phase targets/stop rules for gameplay, pressure, and route-risk assets.

Rejected Alternatives: Reusing `what_do_you_do` was rejected because a visible action is not the same as a visible decision. Keeping this only in feedback taxonomy was rejected because experiment packets and weekly reports are the measurement surfaces that future operators will use.

Scalability potential: Low budget validation can reject passive mood assets before public posting. Middle/High/Ultra marketing can scale cold-read, creator, press, Steam, and paid tests only after the strongest asset proves one readable decision.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## Decision 169 - Dashboard Rows Must Not Lose Agency Proof

Problem: AB-009 and the measurement plan had agency-decision fields, but the KPI dashboard still exposed cold reads through genre/player-verb and engagement fields. A weekly report could therefore claim agency proof without preserving the raw answer or the counted decision-read field.

Solution: Added `cold_read_agency_decision`, `what_decision_next`, `agency_decision_read`, and `agency_decision_read_comments` to the dashboard spec and added KPI Clerk stop rules in the daily loop. Any gameplay, pressure, route-risk, creator, or first-public claim now has to carry the exact agency field before it can be counted.

Rejected Alternatives: Letting Analytics own the fields alone was rejected because operators report from the KPI dashboard. Adding a new reporting file was rejected because it would split owner-local measurement across two surfaces.

Scalability potential: Low budget reporting cannot inflate weak assets into proof. Middle/High/Ultra campaigns can scale cold-read, public, creator, and paid tests while keeping the same measurable agency floor.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## Decision 170 - Entry Docs Must Carry The Reporting Guard

Problem: Dashboard and analytics now had agency-decision fields, but the control tower, README, prep directions, and risk register still framed agency proof mostly as a readable decision. A future agent starting from entry docs could report the right concept without filling the machine-readable field.

Solution: Propagated the agency-decision reporting guard into the entry and risk layer. Control tower, README, and prep directions now name the AB-009/KPI fields; RISK-052 blocks agency-proof reporting without the viewer-named decision field.

Rejected Alternatives: Leaving the field names only in KPI/Analytics was rejected because entry docs are the first route after context compaction. Adding another measurement guide was rejected because the owner-local measurement files already exist.

Scalability potential: Low budget work avoids false-positive proof from soft comments. Middle/High/Ultra operations can scale reporting and outreach while the same field-level guard prevents mood-only inflation.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## Decision 171 - Campaign 01 Must Consume AB-009, Not Just Mention Agency

Problem: Campaign 01 required an agency/decision proof asset in metadata, but its T-24h blind cold-read pass still listed only identity/player-verb/capsule assets and AB-001/AB-002 notes. That allowed the first screenshot drop to advance from genre and verb clarity while the agency candidate never generated `what_decision_next` or `agency_decision_read`.

Solution: Bound Campaign 01 T-24h, metrics, kill criteria, Required Inputs, and `KEEP` decision to AB-009/KPI decision-read fields. Campaign 01 now stays `HOLD` unless one agency candidate stores a viewer-named pressure decision.

Rejected Alternatives: Relying on the earlier metadata gate was rejected because planned/factual asset labels do not prove viewer comprehension. Updating only KPI was rejected because Campaign 01 is the execution gate for first public traffic.

Scalability potential: Low budget public testing cannot burn the first drop on a passive screenshot pack. Middle/High/Ultra campaigns can still scale Steam, creator, and paid paths after Campaign 01 proves a decision, not just mood.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## Decision 172 - Steam Page Must Consume AB-009 Before Page Movement

Problem: Campaign 02, the Steam review packet, and the Steam copy matrix required agency/decision proof assets, but still allowed Steam movement from generic cold-read notes. That created a downstream bypass where Campaign 01 could demand `what_decision_next` while Steam assembly ignored the field.

Solution: Bound Campaign 02 upstream decisions, launch asset minimums, and first-week `EXPAND` rule to AB-009/KPI viewer-named decision fields. Bound the Steam asset checklist and store copy matrix to `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` before Steam draft review, copy selection, or page movement can claim gameplay/pressure/route-risk proof.

Rejected Alternatives: Relying on Campaign 01 alone was rejected because Steam docs are independent execution surfaces. Keeping "blind-read notes" language was rejected because notes can contain mood, monster, darkness, or scenery reads without proving player choice.

Scalability potential: Low budget Steam prep avoids burning the Coming Soon page on a passive-but-attractive screenshot set. Middle/High/Ultra campaigns can scale Steam, press, creator, and paid traffic only after page proof carries a measured player decision.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No Steam page, cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## Decision 173 - Creator Send Gates Must Consume AB-009 Before Outreach

Problem: Creator send gates required a factual `AGENCY_PROOF_CANDIDATE` and `agency_decision_notes`, but the first human-send packet and copy banks still allowed proof/Steam/gameplay language to pass with AB-001/002/004 and asset-side notes only. That let a creator email claim pressure or route-risk proof without a stored viewer-named decision.

Solution: Bound the first human-send packet, CRM readiness schema, segment matrix, pitch bank, A-tier drafts, priority-50 drafts, and post-bank Hour 48 creator route to AB-009/KPI `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` for gameplay/pressure/route-risk sends.

Rejected Alternatives: Relying on `agency_decision_notes` was rejected because it is asset-side intent, not viewer proof. Relying on Campaign 01 was rejected because creator docs are reused as direct send surfaces.

Scalability potential: Low budget outreach avoids burning verified creators on passive proof. Middle/High/Ultra campaigns can scale creator sends after the same decision-read evidence exists across asset, campaign, and CRM routes.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No creator outreach, cold-read test, public post, account/browser action, runtime, or build action occurred.

## Decision 174 - Historical AB Trace Must Point To Current Agency Authority

Problem: Backlog/source rows 68-70 still mentioned AB-001/002/004 as the historical cold-read, Steam, and creator-send gate. Even though active docs were fixed, a future agent could start from those rows and miss rows 139/143/144.

Solution: Marked rows/source addenda 68-70 as original bindings superseded by row 139 for AB-009 measurement, row 143 for Steam AB-009 page movement, and row 144 for creator AB-009 human-send gates.

Rejected Alternatives: Deleting or rewriting the historical rows was rejected because they are audit trail. Leaving them unqualified was rejected because they read like current authority after the AB-009 migration.

Scalability potential: Low budget operators avoid stale test plans. Middle/High/Ultra marketing can scale from the latest owner-local gate without reintroducing older proof thresholds.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No cold-read test, creator outreach, public post, account/browser action, runtime, or build action occurred.

## Decision 175 - Public Soft-Launch Surfaces Must Consume AB-009

Problem: Several public or semi-public gates still said "agency proof asset" without requiring the AB-009/KPI decision-read field. That left soft-launch paths through devlog/signup, wishlist/Next Fest, presskit, Curator Connect, showcase, social sequence, or launch war-room checks.

Solution: Bound the owned-audience, wishlist/Next Fest, wishlist iteration, presskit, curator, showcase, social, and launch war-room gates to AB-009/KPI decision-read fields whenever first-page agency proof is used to advance public movement.

Rejected Alternatives: Relying on the central control tower was rejected because operators execute from these surface docs. Repeating only "readable player decision" was rejected because it still permits unstructured mood notes.

Scalability potential: Low budget public surfaces cannot spend first attention on a passive asset. Middle/High/Ultra marketing can scale devlog, Steam events, curator, press, and social routes only after the same measured agency floor exists.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No signup, Steam page movement, curator send, presskit publish, showcase submission, social post, launch action, browser/account action, runtime, or build action occurred.

## Decision 176 - Daily And KPI Gates Need The Same Agency Field Set

Problem: Control tower and risk register accepted `cold_read_agency_decision`, but the daily loop and one KPI counting rule only named `what_decision_next`, `agency_decision_read`, and `agency_decision_read_comments`. That created a reporting drift where cold-read count fields could be omitted from daily enforcement.

Solution: Added `cold_read_agency_decision` to the daily agency-proof hold checks and KPI counting rule.

Rejected Alternatives: Relying on RISK-052 alone was rejected because the daily loop is the operational gate agents run. Leaving KPI narrower was rejected because it would undercount the accepted cold-read field.

Scalability potential: Low budget reporting keeps one field set. Middle/High/Ultra marketing can scale analytics without divergent dashboard rules.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No cold-read test, report, public action, browser/account action, runtime, or build action occurred.

## Decision 177 - Website Shell Must Not Bypass AB-009

Problem: The website/presskit shell required a readable player decision, but did not require the AB-009/KPI field that proves a cold viewer named that decision. That made the website a possible public bypass around Steam/Campaign gates.

Solution: Bound the website hero, site launch gate, presskit minimum screenshot row, and presskit kill conditions to `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` for first-page agency proof.

Rejected Alternatives: Relying on owned-audience or presskit docs was rejected because this website plan owns the public shell and presskit file requirements. Leaving "readable player decision" in prose was rejected because it is not machine-checkable.

Scalability potential: Low budget site publishing stays proof-first. Middle/High/Ultra marketing can safely reuse the site/presskit as the stable public hub only after the same decision-read evidence exists.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No website publish, presskit send, signup, browser/account action, runtime, or build action occurred.

## Decision 178 - Outbound Paste, Spend, And Access Surfaces Must Consume AB-009

Problem: After Steam, creator, presskit, showcase, and website gates were bound to AB-009, several lower-level outbound surfaces still had weaker prose: paid microtests, community templates, community target rules, Discord open gate, devlog/news reuse, press email templates, and preview-access batches could talk about readable decisions or pressure gameplay without requiring the measured viewer-named decision field.

Solution: Bound those surfaces to the AB-009/KPI field set: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`. Paid spend, community opening, devlog/news reuse, press email sends, and preview/key access now cannot use gameplay/pressure/route-risk agency proof unless the field exists.

Rejected Alternatives: Relying on the broad public-surface row 146 was rejected because operators paste/send/spend from these local docs. Leaving "ask whether the decision reads" in community docs was rejected because a question is not proof unless the answer is stored in the owner-local measurement fields.

Scalability potential: Low budget operations avoid burning money, communities, press goodwill, or key/access trust on passive mood proof. Middle/High/Ultra marketing can scale paid tests, community, press, devlogs, and access only after the same decision-read evidence exists.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No paid spend, community post/server, devlog publish, press email, access send, browser/account action, runtime, or build action occurred.

## Decision 179 - Operator Routers Must Not Lag Behind AB-009

Problem: The lower-level outbound surfaces were fixed, but router docs that future agents naturally follow still had older shorthand: AgentOps batch protocol, low-budget spend ladder, press angle bank, and paid creator terms could direct work from asset/spend/angle existence without preserving the AB-009 decision-read field and route/provenance proof.

Solution: Bound those routers to the same AB-009/KPI field set and route/provenance handling. Spend, paid creator briefs, press angles, and agent batches now require `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` whenever gameplay, pressure, route risk, threat, salvage failure, or first-public agency proof is used.

Rejected Alternatives: Relying on deeper send-surface gates was rejected because agents often start from AgentOps, budget, or press angle docs. Creating a new gate doc was rejected because the existing routers own the workflow.

Scalability potential: Low budget execution cannot spend or pitch from passive proof. Middle/High/Ultra marketing can scale press, paid creators, and ad tests only after measurement and route custody are preserved in the operating surface.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No spend, press send, creator contract, outreach, account/browser action, runtime, or build action occurred.

## Decision 180 - Calendars And Brand Plan Must Not Reopen Volume Quotas

Problem: After local send/spend/router docs were bound to AB-009, broad calendars and the brand/master plan still contained outreach-volume language such as 10-20 creators, 50-100 leads, 100-200 leads, and regional verification. A future agent could treat dates and batch sizes as authority and bypass the field-level gate.

Solution: Bound the outreach calendar, 90-day calendar, master plan, and brand bible to AB-009/KPI decision-read fields and route/provenance custody. Batch sizes now read as ceilings, not instructions. Gameplay, pressure, route-risk, threat, salvage, base-failure, or first-public agency claims now require `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` before creator, press, community, Steam, paid, or public scaling.

Rejected Alternatives: Relying on the control tower was rejected because calendars are execution surfaces. Deleting all future-volume language was rejected because it is useful as a later ceiling once proof exists. Adding a new calendar was rejected because the existing docs own the route.

Scalability potential: Low budget execution avoids quota-driven outreach before proof. Middle/High/Ultra marketing can still scale batches after measured agency proof and reply custody exist.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No outreach, public post, Steam movement, paid spend, browser/account action, runtime, or build action occurred.

## Decision 181 - Access Routes Must Not Become A Private Proof Bypass

Problem: Key, legal, playtester, demo telemetry, and demo outreach docs had strong route/disclosure language, but not every access route explicitly required AB-009/KPI field evidence before using pressure, route-risk, threat, salvage, or base-failure proof in key/access/recruitment copy. A private preview or playtest invite could therefore bypass public/creator gates.

Solution: Bound key/access compliance, legal disclosure, playtester recruitment, demo telemetry, and Campaign 03 demo outreach to the same AB-009/KPI decision-read field set and route/provenance custody. Access copy now needs `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` before it can claim gameplay/pressure/route-risk agency proof. Replies and tester feedback stay in their source route until provenance allows reuse.

Rejected Alternatives: Relying on public CTA gates was rejected because private access uses different links and logs. Relying only on `AGENCY_DECISION_READ` taxonomy was rejected because the access pitch needs a concrete owner-local field source. Blocking all private preview language was rejected because the later demo route still needs usable access mechanics.

Scalability potential: Low budget execution avoids burning keys, tester goodwill, or preview trust on unmeasured claims. Middle/High/Ultra campaigns can scale demo/key/playtest access only after proof and route custody survive the same field checks.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No key send, access invite, tester recruitment, demo outreach, public post, browser/account action, runtime, or build action occurred.

## Decision 182 - Entry And KPI Surfaces Must Carry Access Proof Rules

Problem: The access-route docs were fixed, but first-read and reporting surfaces still summarized demo/key/playtest gates more generally. A future agent could start from the control tower, README, prep directions, KPI dashboard, or risk register and miss the new AB-009/KPI field-source requirement for private access copy.

Solution: Propagated the access-route proof rule into control tower, README, prep directions, KPI dashboard fields, and RISK-053. Key, private preview, Steam Playtest, tester recruitment, and demo outreach copy now have a first-read rule: no gameplay/pressure/route-risk proof claim without AB-009/KPI field source, route class, reply-provenance, and access logs where relevant.

Rejected Alternatives: Relying on row 152 alone was rejected because compaction agents often start from first-read docs. Adding another access policy doc was rejected because the owner-local docs already exist.

Scalability potential: Low budget operators avoid accidental trust damage from private access copy. Middle/High/Ultra campaigns can scale access routes with the same KPI fields that preserve proof and consent.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No key send, access invite, tester recruitment, demo outreach, public post, browser/account action, runtime, or build action occurred.

## Decision 183 - Loose CTA Language Must Route Through Activation

Problem: Several operational docs still used shorthand like "one CTA", "Light CTA if allowed", "Steam page visit/wishlist", "title + wishlist", or "Clean CTA only if page exists". That language can bypass the owner-local Official CTA Link Activation Gate because "page exists" is weaker than destination, custody, public-state, UTM permission, and fallback proof.

Solution: Rewrote loose CTA surfaces in AgentOps, Reddit/community rules, experiment briefs, localization, prep directions, and capsule/trailer briefs. Public wishlist, Steam, demo, signup, presskit, regional, and trailer/capsule CTAs now require Official CTA Link Activation Gate V0, or a no-link feedback/private-access route where public CTA is not allowed.

Rejected Alternatives: Relying on Analytics alone was rejected because agents paste from local workflow docs. Removing all CTA references was rejected because later stages need the slots; they now point to the proper owner-local gate.

Scalability potential: Low budget work avoids accidental dead/placeholder links. Middle/High/Ultra campaigns can scale traffic without losing attribution, custody, or platform-rule context.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No post, CTA, signup, Steam movement, paid spend, browser/account action, runtime, or build action occurred.

## Decision 184 - Regional Outreach Must Be Encoding-Clean And Gate-Aligned

Problem: Regional docs had mojibake in the RU/CIS pitch and still left enough older language for regional sends, regional lead verification, Steam/demo mentions, or localized proof claims to bypass AB-009/KPI decision-read fields, Official CTA Link Activation Gate V0, and route/provenance custody.

Solution: Repaired the Russian pitch text in the campaign and regional plan. Added Regional Send Gate V0 across regional campaign, regional outreach plan, regional creator leads, and localization QA so regional sends require native/fluent review, AB-009/KPI source field (`what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`) for gameplay/pressure/route-risk proof, CTA activation or no-link/private-access fallback, route class, access log where private, and reply-provenance custody. Lead verification volumes are ceilings tied to a source-backed CRM asset/route gap, not quotas.

Rejected Alternatives: Leaving RU text as internal draft was rejected because mojibake in a pasteable campaign doc is a send hazard. Using only a localization warning was rejected because regional docs are execution surfaces. Removing regional plans entirely was rejected because later regional scaling needs owner-local gates, not silence.

Scalability potential: Low budget execution avoids damaging regional trust with broken text or dead CTA/access routes. Middle/High/Ultra marketing can scale region by region after the same measured agency proof and provenance fields survive localization.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No regional outreach, public post, CTA, Steam movement, account/browser action, runtime, or build action occurred.

## Decision 185 - Official-Link Shorthand Must Collapse Into CTA Activation

Problem: A second pass found leftover "official link exists", "official links", and "one Steam link" shorthand in analytics, Campaign 01/02, Steam checklist, master plan, control tower, budget, social, launch, risk, backlog, and source ledger text. That wording is weaker than the owner-local Official CTA Link Activation Gate V0 because it omits destination custody, public state, UTM permission, no-link fallback, and private access separation.

Solution: Replaced the shorthand with Official CTA Link Activation Gate V0, Official CTA/contact preflight, or private access log language. Steam launch now names CTA/contact preflight, first screenshot UTM usage waits for CTA activation, paid/social/launch surfaces require CTA packets for public links, and historical source/backlog rows are qualified so they do not reintroduce a "page exists" gate.

Rejected Alternatives: Leaving historical rows untouched was rejected because agents grep from backlog/source ledger during compaction. Adding a new CTA policy was rejected because `Analytics/MEASUREMENT_AND_UTM_PLAN.md` already owns the gate. Blocking all link language was rejected because later public launch still needs tracked, custody-backed CTAs.

Scalability potential: Low budget work avoids broken or uncustodied CTA links. Middle/High/Ultra marketing can scale paid traffic, social posts, Steam announcements, and creator/press routing from one owner-local link gate.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No public link, Steam movement, post, paid spend, browser/account action, runtime, or build action occurred.

## Decision 186 - Page Existence Is Not Route Permission

Problem: After official-link shorthand was repaired, many execution surfaces still used "Steam page exists", "link exists", or raw Steam/demo/presskit link language as if existence permitted posting, spend, wishlist asks, creator sends, press sends, or support/forum setup. That is weaker than CTA/access custody and can open public routes before UTM, ownership, consent, and fallback rules are true.

Solution: Rewrote spend, Next Fest, post bank, website, creator, CRM schema, pitch bank, press, partnership, daily loop, QA, wishlist, owned-audience, FAQ, support/forum, outreach calendar, backlog, risk, and source-ledger surfaces so public links require Official CTA Link Activation Gate V0 and private routes require access logs. Page existence may be factual state, but not route permission.

Rejected Alternatives: Treating page existence as harmless factual wording was rejected because these are execution docs. Removing link references entirely was rejected because later launch needs link slots. Leaving the issue in source/backlog trace was rejected because future agents grep from those files after compaction.

Scalability potential: Low budget execution does not burn a first impression on dead or unowned links. Middle/High/Ultra marketing can scale wishlist, press, creator, paid, demo, and support routes from one custody-backed link rule.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No public link, Steam movement, post, paid spend, outreach, browser/account action, runtime, or build action occurred.

## Decision 187 - KPI Reply Provenance Must Use The Live Schema Field

Problem: The KPI access-reporting fields used `reply_provenance`, while the live creator CRM, press tracker, and curator tracker use `reply_consent_provenance`. That drift would create a non-schema reporting field and make access/demo/playtest reply reuse harder to audit.

Solution: Renamed the KPI access field and counting rule to `reply_consent_provenance`, explicitly tying it to the live creator, press, and curator schemas.

Rejected Alternatives: Keeping both aliases was rejected because `CreatorOutreach/CREATOR_CRM_SCHEMA_AND_SCORING.md` already rejects schema aliases for live CSV work. Renaming live CSV fields was rejected because the trackers and docs already use `reply_consent_provenance` consistently.

Scalability potential: Low budget operators log one field name. Middle/High/Ultra reporting can aggregate creator, press, curator, playtest, and demo replies without schema translation.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No dashboard row, CRM row, tracker row, outreach, browser/account action, runtime, or build action occurred.

## Decision 188 - Provenance Shorthand Must Not Hide Schema Fields

Problem: After KPI field alignment, many execution surfaces still used loose prose such as "reply-provenance handling" or "route/provenance custody". That is not a CSV error, but it is an operating hazard: creator sends, press/curator sends, private access, and public feedback have different route fields, and generic prose invites notes-only logging.

Solution: Replaced operating shorthand with route-specific field names. Creator/press/curator send surfaces now name `send_route_class` plus `reply_consent_provenance`; key/demo/playtest/private-preview surfaces name `access_route_class` plus `reply_consent_provenance`; public/community feedback remains route-specific and separate from creator/press/newsletter/playtest consent.

Rejected Alternatives: Keeping the shorthand was rejected because `CreatorOutreach/CREATOR_CRM_SCHEMA_AND_SCORING.md` explicitly rejects schema aliases. Creating another provenance policy file was rejected because the failure is in paste/send/checklist surfaces, not missing policy.

Scalability potential: Low budget operations can route replies without notes-only cleanup. Middle/High/Ultra launch operations can run creator, press, curator, demo, playtest, public feedback, and regional paths in parallel without collapsing them into one consent bucket.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No CRM row, press row, curator row, key send, access invite, public post, outreach, browser/account action, runtime, or build action occurred.

## Decision 189 - Public Comments Are Not Cross-Route Consent

Problem: Public-post and reporting docs still used loose "consent/provenance" wording or short `public_comment` values without consistently naming `consent_provenance = public_comment`. That can make a public reply look reusable as newsletter, creator CRM, press, or playtest consent.

Solution: Updated public post, social, community, KPI, analytics, control tower, README, daily loop, playtester, preview-access, prep, schedule, master-plan, and risk surfaces to separate public feedback consent from reply consent. Public comments use `consent_provenance = public_comment`; creator/press/curator/access replies use `reply_consent_provenance`; route-specific class fields remain separate.

Rejected Alternatives: Leaving "consent/provenance" prose was rejected because it collapses public comments, support reports, creator replies, and tester feedback into one bucket. Adding a new consent document was rejected because the actual copy/reporting surfaces needed the field names inline.

Scalability potential: Low budget public critique can collect useful signal without polluting CRM or waitlists. Middle/High/Ultra launch ops can scale public posts, Steam comments, support, playtests, press, and creator replies without losing source permission.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No post, signup, CRM import, key send, access invite, outreach, browser/account action, runtime, or build action occurred.

## Decision 190 - CRM Status Transitions Need Structured Proof

Problem: KPI and workflow docs still had enough legacy status vocabulary for a future operator to treat `CONTACTED`, `REPLIED`, or `COVERED` as notes-only states, or to promote raw queue rows through absent live CRM states such as `VERIFIED_NOT_CONTACTED`. That bypasses the structured send/reply/coverage fields added to the live creator CRM.

Solution: Bound the KPI status enum, raw queue README, mass lead workflow, and CRM schema to the live creator CRM vocabulary. `CONTACTED` now requires an actual human send plus `outreach_batch`, `sent_date`, `contact_route_verified_for_send`, `asset_ids_sent`, `creator_utility_score`, and `send_route_class`. `REPLIED` requires `reply_status_after_send` and `reply_consent_provenance`. `COVERED` requires `coverage_url` when public coverage exists.

Rejected Alternatives: Keeping old shorthand was rejected because it hides status proof inside notes. Adding `VERIFIED_NOT_CONTACTED` was rejected because the live CRM has no raw rows and no such schema state. Auto-promoting raw queues was rejected because raw queue state is intentionally local and not contact-ready.

Scalability potential: Low budget outreach avoids false send counts and consent contamination. Middle/High/Ultra launch ops can scale creator batches while dashboard counts stay machine-filterable by structured send, reply, and coverage fields.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No CRM row was promoted, no outreach was sent, no public post, no browser/account action, no runtime action, and no build action occurred.

## Decision 191 - Live CRM Cannot Carry Mojibake Into Send Prep

Problem: A static data audit found seven live creator CRM rows with mojibake or decorative scraped Unicode in fields a human could later paste or read during send preparation. Raw archive CSVs may preserve source text, but the live CRM is an operating surface and cannot contain corrupted pitch text or unreadable display names.

Solution: Normalized only the live `Data/CREATOR_VERIFICATION_TEMPLATE.csv` rows. STAF_52 pitch text was changed to ASCII-safe English, MMO-Zone/PP Gaming/Dead Paddy/Maxim stale source-title notes were transliterated or stripped of broken emoji bytes, Tigerfrost decorative emoji mojibake was collapsed to `Tigerfrost`, and the corrupted DaddelBaerTV display name was normalized to `DaddelBaerTV`.

Rejected Alternatives: Cleaning raw archive CSVs was rejected because they are source evidence dumps. Leaving mojibake in live CRM was rejected because the CRM is a send-prep surface. Using non-ASCII display names was rejected because this file already suffered encoding drift and ASCII-safe normalization is safer for immediate operations.

Scalability potential: Low budget outreach avoids embarrassing corrupted paste text. Middle/High/Ultra creator operations can keep raw evidence immutable while the live CRM remains clean enough for machine filtering and human review.

Hardware Impact: 0us measured runtime impact. STATIC_DATA only. No status changed, no route/send/reply/coverage field was filled, no outreach was sent, no public post, no browser/account action, no runtime action, and no build action occurred.

## Decision 192 - Access Logs Must Not Collapse Route Proof Into Notes

Problem: The preview/key access protocol still showed a key log CSV schema with generic `contact_route` and no explicit reply/status/proof fields. The key compliance document also used `verified_contact` and `reply_status` shorthand. That is enough for a future access batch to log proof in notes, then count private access replies without exact route custody.

Solution: Replaced the key/access log schema with exact fields: `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and `agency_decision_field_source`. Added a rule that access rows are invalid when these fields are collapsed into notes, and aligned the key compliance field table to the same names.

Rejected Alternatives: Keeping `contact_route` was rejected because creator CRM already uses it for pre-send contact discovery, not private access custody. Keeping `reply_status` was rejected because it hides the after-send state and diverges from the live send-log vocabulary. Adding another access policy doc was rejected because the schema owner docs needed the correction inline.

Scalability potential: Low budget key/access work avoids orphaned proof and consent cleanup. Middle/High/Ultra press, creator, curator, and playtest access can scale from machine-filterable fields instead of notes.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No access/key row was created, no key was sent, no outreach occurred, no public post, no browser/account action, no runtime action, and no build action occurred.

## Decision 193 - Private Access Field Set Must Propagate To Execution Gates

Problem: After the key/access schema was corrected, demo, playtest, regional, audience-screening, control-tower, and risk surfaces still used shorthand such as "access log", "route class", or `access_route_class` / `reply_consent_provenance`. That leaves room for access proof to omit `verified_contact_route`, `reply_status_after_send`, or `agency_decision_field_source`.

Solution: Propagated the exact private access-log field set into the execution gates: `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and `agency_decision_field_source` where proof claims are used. Public feedback/signup routes keep `route_class` / `consent_provenance`; creator/press/curator sends keep `send_route_class` / `reply_consent_provenance`.

Rejected Alternatives: Leaving shorthand in execution docs was rejected because agents paste from those docs, not from the schema owner. Treating public `route_class` and private `access_route_class` as interchangeable was rejected because public CTA and private access have different custody and leak risks.

Scalability potential: Low budget access tests avoid mixed consent and missing proof fields. Middle/High/Ultra campaigns can scale playtest, regional, demo, curator, creator, and press access from one field set.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No access row was created, no key/playtest/demo invite was sent, no outreach occurred, no public post, no browser/account action, no runtime action, and no build action occurred.

## Decision 194 - SN2 Pain Buckets Need Same-Day Steam Refresh

Problem: SN2-derived pain buckets influence HECTON capture priorities, but Steam review counts and recent negative wording are volatile after launch. Using the 2026-05-19 V3 snapshot on 2026-05-20 would let stale competitor pain drive asset metadata or capture sequencing.

Solution: Fetched the public Steam review and appdetails APIs on 2026-05-20 and recorded V4 in the monitoring owner file and RISK-046. V4 shows 66,106 positive / 5,965 negative / 72,071 all-language reviews and 40,212 positive / 2,708 negative / 42,920 English reviews, both `Very Positive`. Recent negative term hits still keep agency, base readability, trust, and content-loop proof useful internally, but they remain directional samples only.

Rejected Alternatives: Keeping V3 was rejected because capture-priority docs explicitly require same-day freshness. Turning term hits into public copy was rejected because the monitoring boundary and RISK-024/RISK-043 forbid competitor attack language. Adding another competitor memo was rejected because the owner-local monitoring file already owns this signal.

Scalability potential: Low budget capture work avoids chasing stale enemy weakness. Middle/High/Ultra campaigns can still prioritize proof assets that answer real audience anxieties while staying HECTON-positive and evidence-labeled.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/WEB_EVIDENCE only. No public comparison copy, outreach, browser profile access, account creation, runtime action, or build action occurred.

## Decision 195 - Pain Freshness Must Be Structured Asset Metadata

Problem: Asset metadata had `pain_bucket_answered` and `pain_proof_score`, but no structured source/date for the competitor or market freshness check. That lets stale SN2 pain re-enter capture priority through notes or memory even after V4 refresh.

Solution: Added `pain_freshness_source` and `pain_freshness_checked_at` to the planned asset metadata schema and all 13 planned rows, defaulted to `PENDING_SAME_DAY_REFRESH` / `PENDING_CAPTURE`. Propagated the fields into asset ops, QA, shotlist, Campaign 01, KPI, control tower, creator segment gating, and monitoring. Nonzero pain proof now needs a source row and date, not notes-only justification.

Rejected Alternatives: Storing freshness in `notes` was rejected because notes are not machine-filterable. Using only the monitoring freshness rule was rejected because Campaign 01 and creator sends read asset metadata directly. Adding a separate freshness tracker was rejected because one asset fact should live in the asset row.

Scalability potential: Low budget capture work avoids stale competitor-driven frames. Middle/High/Ultra campaigns can filter first-pack assets by current proof source before scaling creator, press, Steam, or paid routes.

Hardware Impact: 0us measured runtime impact. STATIC_DATA/STATIC_DOC only. No asset was promoted, no public copy, no outreach, no browser/account action, no runtime action, and no build action occurred.

## Decision 196 - Creator Send Gates Must Read Pain Freshness Fields

Problem: After asset metadata gained source/date pain freshness fields, creator-facing send docs still let pain-backed pressure, route-risk, or salvage copy proceed from older gates that did not name `pain_freshness_source` or `pain_freshness_checked_at`.

Solution: Propagated the freshness fields into the first human-send workflow, post bank creator warmup rules, pitch bank, A-tier personalized drafts, and priority-50 message drafts. Pain-backed creator copy now requires asset claim checks, source/date pain freshness, creator utility, open `creator_send_gate`, agency proof where applicable, route verification, Promise Lint, and CRM send-log readiness.

Rejected Alternatives: Relying on asset metadata alone was rejected because operators paste from pitch/post docs. Blocking all pressure/route-risk copy was rejected because these angles are allowed after proof. Adding a new outreach policy was rejected because existing send surfaces needed inline gates.

Scalability potential: Low budget creator feedback avoids stale competitor-derived hooks. Middle/High/Ultra launch waves can scale from machine-filterable asset rows instead of copied draft language.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No outreach, public post, browser/account action, runtime action, or build action occurred.

## Decision 197 - Direct-Competitor Pitch Seeds Must Be Neutralized

Problem: Live CRM and generated operating pitch sheets still contained pasteable draft text that said creators had touched `Subnautica/underwater survival`. Even with no-send gates, that string is too close to final outreach and can leak direct competitor framing into human copy.

Solution: Replaced the direct-competitor pitch seed in live CRM, priority-50 drafts, priority-250 sheet, verification batch assignment files, and the future LetsPlayIndex scraper template. The new seed is neutral audience-fit language and explicitly requires one matched HECTON asset plus asset QA, pain freshness fields, and creator-send gates.

Rejected Alternatives: Leaving the old seed because it was "draft only" was rejected because send-prep docs are paste surfaces. Cleaning raw archive CSVs was rejected because they are evidence dumps. Removing all underwater-survival fit language was rejected because it is legitimate targeting data when kept neutral.

Scalability potential: Low budget outreach avoids opening with competitor comparison. Middle/High/Ultra creator waves can still target adjacent-audience creators while preserving HECTON-first positioning and machine-filterable gates.

Hardware Impact: 0us measured runtime impact. STATIC_DATA/STATIC_DOC only. No raw archive source was changed, no outreach occurred, no public post, no browser/account action, no runtime action, and no build action occurred.

## Decision 198 - Source Evidence Must Not Leak Into Pasteable Copy

Problem: After direct-competitor pitch seeds were neutralized, a smaller paste-risk remained: three live CRM personalized openers, one German priority-50 line, repeated priority-50 message body text, and the pitch-bank archetype subject still used direct competitor wording in places a human could copy into outreach.

Solution: Rewrote only pasteable final-copy lines to neutral audience-fit language. Source game lists, evidence notes, and CRM activity fields remain intact as proof data, but opener/body/subject text no longer needs direct competitor names to explain fit.

Rejected Alternatives: Removing all source/evidence references was rejected because targeting still needs auditability. Leaving the copy because it is gated was rejected because the specific problem is pasteable text. Editing raw archive CSVs was rejected again because raw source dumps are evidence.

Scalability potential: Low budget outreach avoids competitor-first framing even when rushed. Middle/High/Ultra creator waves retain targeting evidence while final copy stays HECTON-first and gate-driven.

Hardware Impact: 0us measured runtime impact. STATIC_DATA/STATIC_DOC only. No outreach, public post, browser/account action, runtime action, or build action occurred.

## Decision 199 - Browser Permission Is Not Account Custody

Problem: The operator explicitly allowed browser/account work in chat, while the standing social/inbox rules require project-owned email, password-manager vault, recovery, 2FA, and backup-code custody. If chat permission is treated as custody proof, an agent can create an official surface under personal sessions, cookies, or undocumented recovery and strand the handle.

Solution: Added an explicit account registration preflight verdict to the social playbook: `HOLD_ACCOUNT_CREATION` until the owner-controlled project inbox, vault item, recovery, 2FA, backup-code destination, approved handle, profile assets, and vault URL destination are recorded. Propagated the boundary into the website inbox gate, control tower, README, and risk register as RISK-054. Allowed work remains public handle checks and profile-copy preparation only.

Rejected Alternatives: Creating accounts from the current desktop/browser state was rejected because personal sessions and remembered credentials do not prove project custody. Storing secrets in docs was rejected because the docs are non-secret checklists only. Asking the user for credentials mid-run was rejected because the owner-custody fields are not recorded and the current useful work is documentation/data hardening.

Scalability potential: Low budget operations avoid losing official handles to unrecoverable accounts. Middle/High/Ultra marketing can later scale social, press, creator, support, and paid routes from one custody-backed identity instead of fragmented platform surfaces.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No private browser profile, cookie/session state, login, account creation, public post, outreach, credential storage, runtime action, or build action occurred.

## Decision 200 - First Capture Packet Must Use Current SN2 Evidence

Problem: The monitoring owner file and risk register moved to the 2026-05-20 Steam API V4 snapshot, but the first capture call sheet still referenced the 2026-05-19 V3 sample. That creates a stale planning surface exactly where the first real capture session will start.

Solution: Updated the shotlist first-session note to V4 and replaced the `PLAN-SHOT-006` V3-specific wording with current agency/defensive-choice language. Also tightened Campaign 01 so its audience label is competitor-neutral and its social custody gate explicitly blocks while account registration preflight remains `HOLD_ACCOUNT_CREATION`.

Rejected Alternatives: Leaving the stale V3 note was rejected because capture operators read the call sheet, not the monitoring file. Removing all SN2-derived pain context was rejected because the private pain buckets still help prioritize proof assets. Adding a new capture memo was rejected because the existing shotlist owns the call sheet.

Scalability potential: Low budget capture time is spent on durable HECTON proof instead of stale competitor reactions. Middle/High/Ultra campaigns can still scale the same asset IDs into Steam, creator, social, and press routes after freshness, utility, and custody gates pass.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No capture, public post, account action, outreach, runtime action, or build action occurred.

## Decision 201 - Regional Pasteable Drafts Cannot Carry Mojibake

Problem: Campaign 05 and the Regional Outreach Plan still contained mojibake in RU/CIS subject/body/ask draft text. These are pasteable regional send surfaces, so broken encoding would create immediate credibility damage if reused. The draft also carried a direct competitor-killer denial inside the body.

Solution: Replaced the broken RU/CIS text with ASCII-safe transliteration and kept it review-pending. The repaired copy preserves the intended scope: single-player underwater survival about pressure, machinery, resource search, and black water; no co-op promise; no competitor-comparison pitch; materials only after CTA activation or private access log custody.

Rejected Alternatives: Using unreviewed Cyrillic was rejected because the file already suffered encoding drift and ASCII-safe text is safer until native review. Leaving mojibake behind a review warning was rejected because operators copy from campaign docs. Editing raw public lead/source CSVs was rejected because those are evidence dumps.

Scalability potential: Low budget regional outreach avoids embarrassing broken text. Middle/High/Ultra regional campaigns can later replace transliteration with native-reviewed localized copy while keeping the same proof and custody gates.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No regional outreach, public post, account action, runtime action, or build action occurred.

## Decision 202 - Execution Surfaces Should Not Volunteer Competitor Names

Problem: Some non-FAQ execution surfaces still used direct competitor labels in post rules, post kill checks, segment rows, and regional copy kill rules. Those references are valid in source evidence, risk triggers, and direct FAQ responses, but they are unnecessary in instructions that shape future posts or creator segmentation.

Solution: Replaced direct competitor labels with neutral competitor or adjacent-underwater-survival wording in the post bank, community post policy, segment pitch matrix, and Campaign 05 regional kill rule. Explicit FAQ responses and raw/source evidence remain intact because they answer or preserve direct user/player language.

Rejected Alternatives: Removing every competitor mention was rejected because monitoring, raw lead evidence, risk registers, and FAQ triggers need exact language. Leaving direct labels in execution rows was rejected because operators paste and adapt from those surfaces under time pressure.

Scalability potential: Low budget public posting keeps HECTON-positive positioning. Middle/High/Ultra creator/social/press campaigns can still use internal audience-fit evidence without letting competitor framing become the hook.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No outreach, public post, account action, runtime action, or build action occurred.

## Decision 203 - Press Tracker Status Is Not Send Permission

Problem: Press tracker rows use statuses such as `READY_FOR_HUMAN_REVIEW_AFTER_PRESSKIT` and `READY_FOR_HUMAN_REVIEW_AFTER_PUBLIC_DEMO`. Those values are useful triage labels, but without an explicit status boundary a future operator could read them as send-ready states and bypass same-day route, inbox, asset, `send_route_class`, and reply-provenance gates.

Solution: Added a tracker status boundary to the press/curator owner doc. Press sends now require current official route, required artifact, official inbox custody, `send_route_class`, reply-provenance handling, and AB-009/KPI decision-read field source where proof claims are used. Curator sends require Steam page/build, Curator Connect where possible, asset QA/claim checks, agency proof when used, and route/provenance fields. Control tower now exposes the same boundary.

Rejected Alternatives: Renaming all CSV status values was rejected because the current values encode the artifact that must exist first. Leaving the rule only in source ledger was rejected because operators work from the press owner doc and control tower. Treating curator and press status as the same was rejected because Curator Connect has its own Steam/build/key custody path.

Scalability potential: Low budget press work avoids premature sends. Middle/High/Ultra press and curator waves can scale from triage rows without confusing "review after artifact exists" with "send today".

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No press send, curator offer, account action, runtime action, or build action occurred.

## Decision 204 - Press And Curator Trackers Need A Machine Send Gate

Problem: The owner doc now says press/curator `status` is triage-only, but the CSVs still had no separate permission field. That leaves a script or rushed operator free to filter `READY_FOR_HUMAN_REVIEW_AFTER_*` or `CURATOR_CONNECT_AFTER_*` as if it meant send-ready.

Solution: Added `send_permission_gate` to both press and curator trackers. All current press rows are `BLOCKED_*`; curator rows are `BLOCKED_*` except the competitor row, which is `DO_NOT_CONTACT_COMPETITOR`. The owner doc, control tower, backlog, source ledger, and RISK-055 now state that only `ALLOW_PRESS_SEND_VERIFIED` or `ALLOW_CURATOR_SEND_VERIFIED` can permit a future send after artifact, same-day route, inbox/custody, route-class, reply-provenance, and agency-proof checks pass.

Rejected Alternatives: Renaming `status` values was rejected because status still carries useful triage information. Relying on prose-only warnings was rejected because trackers are likely to be filtered mechanically. Filling `send_route_class` was rejected because no send exists and route class is not permission.

Scalability potential: Low budget press work avoids accidental early pitches. Middle/High/Ultra launch waves can automate routing safely because permission is a dedicated field instead of inferred from narrative status.

Hardware Impact: 0us measured runtime impact. STATIC_DATA/STATIC_DOC only. No press send, curator offer, key issue, outreach, browser/account action, runtime action, or build action occurred.

## Decision 205 - Showcase Submission Trackers Need A Separate Permission Gate

Problem: The showcase tracker had only `status` values such as `MONITOR` and `NOT_READY`. Those are harmless today, but the same failure mode as press/curator exists: a future checklist or script can treat a monitoring row as eligible for submission without proving assets, CTA custody, fee/ROI, owner, or measurement.

Solution: Added `submission_permission_gate` to `SHOWCASE_SUBMISSION_TRACKER.csv`. All 8 current rows are blocked: 4 `BLOCKED_MONITOR_ONLY` and 4 `BLOCKED_NOT_READY`. The showcase playbook, control tower, source ledger, backlog, and RISK-056 now define `ALLOW_SHOWCASE_SUBMIT_VERIFIED` as the only future allow value after same-day rules/deadline, fee/ROI, asset pack, Steam/CTA or private-review route custody, agency-proof, owner, and measurement checks pass.

Rejected Alternatives: Reusing `status` was rejected because it should remain a planning state. Adding another playbook note without a CSV field was rejected because event submissions are tracker-driven. Setting an allow value for any row was rejected because no Steam page, asset pack, trailer, public CTA, demo, fee decision, or event owner exists.

Scalability potential: Low budget operations avoid wasting scarce launch beats on premature events. Middle/High/Ultra campaigns can later filter event opportunities by a dedicated permission gate and route class instead of subjective status text.

Hardware Impact: 0us measured runtime impact. STATIC_DATA/STATIC_DOC only. No showcase submission, public claim, fee spend, browser/account action, runtime action, or build action occurred.

## Decision 206 - Key And Access Surfaces Must Consume Send Permission Gates

Problem: Press/curator trackers gained `send_permission_gate`, but downstream key, preview access, and Curator Connect docs still referenced older route/provenance requirements. That could let a key/access workflow satisfy old fields while bypassing the new machine permission gate.

Solution: Propagated the allow-value requirement into key compliance, Curator Connect readiness, review-key/preview-access approval flow, access batch rows ACC-002/ACC-003, and the press angle checklist. Press access now requires `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED`; curator access and Curator Connect require `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED`.

Rejected Alternatives: Leaving the rule only in the tracker owner doc was rejected because access operators use key and Curator Connect docs directly. Adding a new access tracker was rejected because the existing key/access schema is already the owner for distribution logs. Filling any allow values now was rejected because no assets, Steam page, build, route recheck, inbox custody, or key policy activation exists.

Scalability potential: Low budget key/access work avoids accidental copy leakage and key scam exposure. Middle/High/Ultra launch waves can scale press and curator access from the same permission gate used by tracker automation.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No key/access row, Curator Connect offer, press send, curator send, outreach, browser/account action, runtime action, or build action occurred.

## Decision 207 - Entry Docs Must Surface Machine Gates

Problem: The new press/curator and showcase machine gates were present in owner docs, but first-read and daily-loop docs did not name them. A future agent could start from README or the daily loop and still treat tracker `status`, `MONITOR`, or `NOT_READY` as operational state without opening the owner files.

Solution: Added hard-rule and directory-map references to README, added explicit forbidden actions to PREP_DIRECTIONS_NOW, and added noon-kill questions to DAILY_AGENT_TASK_LOOP. First-read docs now state that press/curator sends require `send_permission_gate` allow values and showcase/festival submissions require `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`.

Rejected Alternatives: Relying on control tower only was rejected because README remains a common entry point. Adding a new onboarding doc was rejected by the anti-sprawl rule. Leaving daily-loop checks unchanged was rejected because agents use them as execution guardrails.

Scalability potential: Low budget agent work avoids accidental send/submit lanes during pre-asset preparation. Middle/High/Ultra campaign execution can later scale from explicit permission gates instead of ambiguous status labels.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No press send, curator send, showcase submission, public event claim, outreach, browser/account action, runtime action, or build action occurred.

## Decision 208 - Current Labels Must Match Current Gate State

Problem: After the 2026-05-20 gate work, the control tower, spend ladder, spend recommendation, and daily cut still used 2026-05-19 current-state labels. That can make the active operating map look stale even when the embedded rows are already current.

Solution: Updated only active-current headings and recommendation labels to 2026-05-20. Historical source-ledger addenda remain dated to when their evidence was collected.

Rejected Alternatives: Rewriting historical evidence dates was rejected because source timestamps must stay stable. Leaving the stale current labels was rejected because the control tower and daily loop are entry points. Adding a new date note was rejected because the existing headings own the current-state label.

Scalability potential: Low budget agents start from the right current state. Middle/High/Ultra marketing execution can trust the control tower and spend ladder without re-reading every historical addendum.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No spend, public post, outreach, browser/account action, runtime action, or build action occurred.

## Decision 209 - Paid PMT Rows Need A Machine Spend Gate

Problem: Paid microtest docs had budget tiers, PMT rows, and stop rules, but no dedicated machine-readable permission field. That leaves the same failure mode as press/curator/showcase: a future operator or script can treat PMT ID, budget tier, platform candidate, or readiness prose as permission to spend.

Solution: Added `spend_permission_gate` to the paid microtest execution plan and kept all current PMT rows `BLOCKED_*`. Future paid ad spend requires `ALLOW_PAID_MICROTEST_VERIFIED` after Official CTA Link Activation Gate V0 for the Steam destination, Steam URL custody, UTM proof, Campaign 01 `KEEP` or equivalent organic/page baseline, asset QA, AB-009/KPI decision-read fields where gameplay/pressure/route-risk proof is sold, capped budget, written hypothesis, written stop rule, and 48h owner inspection. Propagated the boundary to the budget ladder, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Relying on `0 USD` prose was rejected because prose is not machine-filterable. Adding a new paid tracker file was rejected by the anti-sprawl rule; the existing PMT table owns the decision. Setting an allow value for any row was rejected because no Steam page, asset QA, CTA custody, UTM baseline, organic signal, or paid owner inspection proof exists.

Scalability potential: Low budget work keeps paid spend frozen while creative/UTM prep can continue. Middle/High/Ultra campaign execution can later run capped paid tests from explicit permission gates instead of subjective budget tiers.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No spend, public post, outreach, browser/account action, runtime action, or build action occurred.

## Decision 210 - Paid Creator Spend Needs A Recipient-Level Gate

Problem: Paid creator docs had rate ranges, disclosure rules, stop rules, and creator send gates, but no recipient-level machine permission field. A rate-card reply, sponsorship policy, audience fit, organic reply, or creator name could be mistaken for permission to spend money.

Solution: Added `paid_creator_permission_gate` to the live CRM after `creator_utility_score`; all 100 current rows are `BLOCKED_NO_PAID_CREATOR_PROOF`. Future paid creator spend requires `ALLOW_PAID_CREATOR_TEST_VERIFIED` after verified official route, owner-controlled inbox/access route, disclosure, demo or Steam baseline, matching asset QA, creator utility 3/4+, matching asset `creator_send_gate`, `send_route_class`, AB-009/KPI decision-read proof where relevant, capped payment, cancellation rule, and 48h result inspection owner. Propagated the boundary to CRM schema, mass verification workflow, segment pitch matrix, rate card, budget ladder, legal/compliance, key compliance, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Reusing `creator_send_gate` was rejected because send permission is not payment permission. Relying on rate-card or sponsorship-policy text was rejected because those are inputs, not approval. Creating a new paid creator tracker was rejected by owner-local data discipline; the live CRM row owns recipient-specific spend eligibility.

Scalability potential: Low budget work can record rates without accidentally opening paid deals. Middle/High/Ultra creator campaigns can later filter paid candidates from a dedicated CRM field while keeping disclosure, route, asset, and measurement proof attached to the same recipient row.

Hardware Impact: 0us measured runtime impact. STATIC_DATA/STATIC_DOC only. No paid creator deal, key/access row, outreach, public post, browser/account action, runtime action, or build action occurred.

## Decision 211 - Official Inbox Custody Needs A Machine Gate

Problem: The official inbox owner doc had custody requirements, but no single machine-readable permission field. That leaves address text, a partial checklist, remembered browser state, or chat permission vulnerable to being treated as custody before owner recovery, 2FA, backup-code custody, vault record, labels, reply identity, and public-contact approval exist.

Solution: Added `official_inbox_custody_gate = HOLD_NO_PROJECT_INBOX_CUSTODY` to the owner doc and defined `ALLOW_OFFICIAL_INBOX_USE_VERIFIED` as the only future allow value. Propagated the gate to social registration, key/access, legal/compliance, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger. Inbox-dependent routes now require the gate before account registration, public contact, presskit, creator/key/support, or paid route use.

Rejected Alternatives: Using an address field alone was rejected because email text is not custody. Creating a secret tracker was rejected because secrets do not belong in docs. Treating browser login or chat permission as proof was rejected because neither proves durable owner recovery, 2FA, backup-code, or password-vault custody.

Scalability potential: Low budget operations avoid orphaned official surfaces and scam-prone key/contact paths. Middle/High/Ultra campaigns can later scale account, press, creator, support, and paid routes from one custody-backed permission gate instead of fragmented account notes.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No login, account registration, public contact, key/access row, spend, browser action, runtime action, or build action occurred.

## Decision 212 - Social Account Registration Needs Its Own Permission Gate

Problem: Social setup had a correct `HOLD_ACCOUNT_CREATION` verdict, but that verdict was not a named permission field. A future operator or script could treat candidate handles, preflight prose, browser state, or chat permission as enough to register official accounts before durable custody and post-registration records exist.

Solution: Added `account_registration_permission_gate = HOLD_ACCOUNT_CREATION` to the social playbook and defined `ALLOW_ACCOUNT_REGISTRATION_VERIFIED` as the only future allow value. The allow path requires inbox custody, vault item, recovery, 2FA, backup-code destination, approved handle, approved profile assets, vault URL destination, and immediate post-registration custody row. Propagated the gate to Campaign 01 social custody, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Reusing `HOLD_ACCOUNT_CREATION` as prose-only was rejected because it is easy to filter incorrectly. Using public handle availability as permission was rejected because availability is not custody. Creating accounts from the user's personal browser session was rejected because it creates orphaned official surfaces and unclear recovery ownership.

Scalability potential: Low budget work can keep preparing copy and handle candidates without creating fragile accounts. Middle/High/Ultra launch operations can later register accounts from a single explicit permission gate and immediately link custody rows, assets, and CTA routes.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No login, account registration, public contact, follow, DM, post, browser action, runtime action, or build action occurred.

## Decision 213 - Public CTA Links Need A Destination-Specific Permission Gate

Problem: The analytics owner doc already had an Official CTA Link Activation packet, but no single machine-readable public-link permission field. A public Steam page, signup form, candidate handle, placeholder URL, private access route, or generic "CTA ready" prose could still be mistaken for permission to post a link.

Solution: Added `public_cta_permission_gate = HOLD_NO_PUBLIC_CTA` to the analytics owner doc and defined `ALLOW_PUBLIC_CTA_VERIFIED` as the only future allow value. The allow is destination-specific and requires exact URL, owner custody, public state, UTM permission, canonical UTM fields, and no-link fallback. Propagated the gate to control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Relying on page existence was rejected because a live page can still be wrong, private, untracked, or outside owner custody. Reusing private access logs was rejected because private access must never become public CTA. Sweeping every historical CTA mention was rejected; the owner gate now gives existing Official CTA references one machine value to resolve.

Scalability potential: Low budget posts can stay no-link feedback until a destination is real. Middle/High/Ultra campaigns can later scale Steam, presskit, signup, Discord, paid, and showcase traffic without mixing destinations or losing source attribution.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No public CTA, post, signup, spend, account/browser action, runtime action, or build action occurred.

## Decision 214 - Private Access Needs A Recipient Or Batch Permission Gate

Problem: Private demo/key/playtest/preview routes had required fields such as `verified_contact_route`, `access_route_class`, and `reply_consent_provenance`, but no single permission field. Build existence, recipient fit, send route prose, or the presence of an access-log schema could be mistaken for permission to distribute access.

Solution: Added `private_access_permission_gate = HOLD_NO_PRIVATE_ACCESS` to the review-key/preview-access owner doc and defined recipient/batch-specific `ALLOW_PRIVATE_ACCESS_VERIFIED` as the only future allow value. The allow path requires stable or explicit technical-test build state, known-issues copy, access type, recipient or batch owner row, verified route, official inbox custody, private access class, reply status/provenance fields, revocation path, disclosure requirement, and agency-proof field source where used. Propagated the gate to key compliance, Steam demo/playtest telemetry, Campaign 03, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Using `verified_contact_route` alone was rejected because route proof is not access approval. Using build availability was rejected because a runnable build can still be unsafe for recipients. Reusing `public_cta_permission_gate` was rejected because private access must never become a public link.

Scalability potential: Low budget access work stays protocol-only until one safe recipient or batch exists. Middle/High/Ultra demo and press waves can later scale from explicit batch gates without leaking links, mixing consent, or losing revocation control.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No key, private access, Playtest invite, Curator Connect copy, public CTA, account/browser action, runtime action, or build action occurred.

## Decision 215 - Public Posts Need Their Own Permission Gate

Problem: No-link posting had route/provenance rules, and asset-led posts had QA/metadata rules, but there was no single machine-readable permission field for the post itself. A draft row, account existence, asset QA score, no-link route class, or CTA state could be mistaken for permission to publish.

Solution: Added `public_post_permission_gate = HOLD_NO_PUBLIC_POST` to the social owner doc and defined post-specific `ALLOW_PUBLIC_POST_VERIFIED` as the only future allow value. The allow path requires account custody, platform rules, real asset IDs or approved quiet pre-asset row, asset QA and metadata claim checks where relevant, Promise Lint, route class/provenance, `public_cta_permission_gate` where linked, no private access link, and no unsupported scope/performance/AI/feature/competitor claim. Propagated the gate to the post bank, asset QA checklist, Campaign 01, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Relying on `route_class = no_link_feedback` was rejected because route class is reporting metadata, not permission. Relying on asset QA alone was rejected because a strong asset can still carry unsupported copy or wrong platform context. Reusing `public_cta_permission_gate` was rejected because no-link posts still need posting permission.

Scalability potential: Low budget public presence stays held until one specific post is safe. Middle/High/Ultra posting waves can later scale per post without conflating account custody, asset quality, CTA permission, and reporting provenance.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No public post, public CTA, account/browser action, outreach, runtime action, or build action occurred.

## Decision 216 - Owned Audience Needs A Mode-Specific Permission Gate

Problem: Owned-audience signup/list work had consent and provider rules, but no single machine-readable permission field. A form draft, list-provider workspace, public CTA, imported contact set, or playtest waitlist copy could be mistaken for permission to collect emails, import contacts, send newsletters, or count signup signal.

Solution: Added `owned_audience_permission_gate = HOLD_NO_OWNED_AUDIENCE` to the owned-audience owner doc and defined mode-specific `ALLOW_OWNED_AUDIENCE_VERIFIED` as the only future allow value. The allow path requires official inbox custody, owner-controlled provider workspace, visible signup mode/data purpose, matching consent checkbox, tested unsubscribe/delete route, export custody, separated contact buckets, route class, `consent_provenance`, no bought/scraped/imported lists, and `public_cta_permission_gate` or `private_access_permission_gate` where routes are linked. Propagated the gate to playtester recruitment, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Using provider existence was rejected because a provider account is not consent. Using public CTA approval was rejected because link permission is not list permission. Importing creator/press/CRM rows was rejected because outreach contact provenance is not newsletter or playtest consent.

Scalability potential: Low budget work can keep form copy ready without creating a dead or dirty list. Middle/High/Ultra launch operations can later scale demo alerts, playtest waitlists, devlog digests, regional alerts, and press/creator contact modes without mixing consent buckets.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No signup form, list import, email send, account/browser action, public post, runtime action, or build action occurred.

## Decision 217 - Discord Opening Needs A Server-Specific Permission Gate

Problem: Discord/community setup had a prose Open Gate, channel list, rules, and FAQ pins, but no single machine-readable permission field. A draft server, channel template, invite URL, moderator willingness, community interest, public CTA, or post draft could be mistaken for permission to open a public server, announce it, or report member signal.

Solution: Added `discord_open_permission_gate = HOLD_NO_DISCORD_PUBLIC_OPEN` to the Discord owner doc and defined server-specific `ALLOW_DISCORD_OPEN_VERIFIED` as the only future allow value. The allow path requires at least two proof conditions, owner-controlled admin account, 2FA/recovery/backup-code/server-owner custody, official inbox custody where public routes exist, named moderation owners/rules/mod-log, FAQ pins, invite custody/revocation owner, `public_cta_permission_gate`, `public_post_permission_gate`, private-access separation, and no bought/imported member path. Propagated the gate to control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Using the prose Open Gate was rejected because prose is not machine-filterable. Using public CTA approval was rejected because link permission is not server/community readiness. Creating a public server from a personal Discord/admin session was rejected because it creates an orphaned official surface and unclear recovery ownership.

Scalability potential: Low budget work can keep rules, FAQ, and moderation scripts ready without opening a dead server. Middle/High/Ultra launch operations can later scale Discord from one explicit server gate while keeping invite custody, moderation load, public posts, CTAs, private access, and reporting provenance separated.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No Discord server, invite, public post, CTA, account/browser action, runtime action, or build action occurred.

## Decision 218 - Steam Support And Forum Routes Need A Surface-Specific Permission Gate

Problem: Steam review/forum/support docs had templates, pinned-thread plans, response caps, and a support custody section, but no single machine-readable permission field. Steam page existence, demo existence, known-issues drafts, public CTA approval, Discord setup, or an angry thread could be mistaken for permission to create pinned threads, publish support links, make official review/forum replies, or count support signal.

Solution: Rechecked official Steamworks User Reviews, Events/Announcements, and Community Moderation docs on 2026-05-20, then added `steam_support_permission_gate = HOLD_NO_STEAM_SUPPORT_PUBLIC_ROUTE` to the Steam reviews/forums/support owner doc. Defined surface-specific `ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED` as the only future allow value. The allow path requires exact app/build/surface, Steamworks/admin custody, support owner coverage, official inbox custody where email/form routes exist, build/branch/known-issues state, pinned thread packet, no review manipulation/reward/in-app ask/review-change request/alt-account path, route class/provenance fields, response caps, `public_cta_permission_gate`, `discord_open_permission_gate` where Discord is used, and `private_access_permission_gate` where private access is connected. Propagated the gate to launch war-room, demo/playtest checklist, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Using Steam page or demo existence was rejected because surface existence is not support readiness. Using a known-issues draft was rejected because draft text is not route custody, owner coverage, or Steamworks rule compliance. Routing everything to Discord was rejected because Steam users need a public non-Discord support route and Discord itself is gated.

Scalability potential: Low budget launch prep can write templates without creating a support treadmill. Middle/High/Ultra release operations can later scale pinned threads, review replies, bug intake, performance templates, and daily digests from one explicit surface gate while preserving route provenance and avoiding review-system abuse.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_SOURCE only. No Steam forum thread, support link, review/forum reply, CTA, account/browser action, runtime action, or build action occurred.

## Decision 219 - Steam Announcements Need A Post-Specific Permission Gate

Problem: Devlog and Steam launch docs treated Steam announcement/news/event posts as reusable outputs, but no single machine-readable permission field separated draft content from Steamworks publication. A devlog draft, Steam page existence, demo existence, public post approval, CTA approval, or event template could be mistaken for permission to publish or schedule a Steam announcement.

Solution: Added `steam_announcement_permission_gate = HOLD_NO_STEAM_ANNOUNCEMENT` to the Devlog and Steam News owner doc and defined post-specific `ALLOW_STEAM_ANNOUNCEMENT_VERIFIED` as the only future allow value. The allow path requires exact Steam app, event/news type, visibility, publish time, owner, rollback/delete owner, Steamworks/admin custody, official event/announcement rule recheck, `public_post_permission_gate`, `public_cta_permission_gate`, `steam_support_permission_gate` where support/reviews/bugs/forums are mentioned, `private_access_permission_gate` where private access is referenced, `discord_open_permission_gate` where Discord is linked or named, `owned_audience_permission_gate` where signup/newsletter language appears, Promise Lint, asset IDs/build ID/source truth, AB-009/KPI decision-read fields where agency proof is claimed, route/UTM fields, and unsupported-claim checks. Propagated the gate to Steam page launch, Next Fest/demo event, demo/playtest checklist, launch war-room, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Reusing `public_post_permission_gate` alone was rejected because Steamworks publication also needs app/admin custody, event visibility, rollback, support, CTA, and platform-rule checks. Reusing `public_cta_permission_gate` was rejected because link permission is not content permission. Treating devlog drafts as convertible to Steam news by default was rejected because target surfaces have different blast radius and custody needs.

Scalability potential: Low budget work can keep devlog drafts ready without accidentally publishing through Steamworks. Middle/High/Ultra launch operations can later scale Coming Soon, demo, event, patch, and feedback-summary announcements from explicit per-post gates with route/source proof attached.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_SOURCE only. No Steam announcement, news post, event, support route, CTA, account/browser action, runtime action, or build action occurred.

## Decision 220 - Localization Needs A Language-Surface Permission Gate

Problem: Localization and regional outreach docs had native-review and encoding rules, but no single machine-readable permission field. Encoding repair, ASCII-safe transliteration, owner-native familiarity, draft translation, raw regional leads, or regional interest could be mistaken for permission to send or publish localized copy.

Solution: Added `localization_public_permission_gate = HOLD_LOCALIZED_PUBLIC_USE` to the localization owner doc and defined language/surface-specific `ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` as the only future allow value. The allow path requires exact language/region, surface, asset IDs, owner, reviewer, review timestamp, encoding clean pass, native/fluent approval, English source Promise Lint and proof gates, AB-009/KPI decision-read fields where agency proof is claimed, route-specific public post/CTA/Steam announcement/support/private access/owned audience/Discord gates where used, creator CRM row mapping, `creator_send_gate`, `send_route_class`, `reply_consent_provenance` for localized creator outreach, regional platform/payment/access check, reviewer notes, and provenance rows before reporting signal. Propagated the gate to regional outreach, regional creator leads, Campaign 05, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Treating RU owner-native familiarity as approval was rejected because public localization still needs asset, route, and proof custody. Treating encoding repair as approval was rejected because readable text can still sound unnatural or add promises. Treating regional leads as send-ready was rejected because source rows are prospecting seeds until localization and route gates pass.

Scalability potential: Low budget regional work can keep drafts and seed rows without damaging trust. Middle/High/Ultra regional campaigns can later scale RU/DE/PT-BR/ES/FR/PL/JP/KR surfaces from explicit language/surface gates while preserving proof, consent, route, and reviewer provenance.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No localized send, regional outreach, public post, CTA, account/browser action, runtime action, or build action occurred.

## Decision 221 - Press Releases Need A Publication-Surface Gate

Problem: Press release templates, presskit publish prose, site presskit blocks, Campaign 02 press notes, and launch war-room reminders could still be mistaken for publication permission. Existing gates protected targeted press sends, public CTAs, public posts, Steam announcements, localization, support, private access, and inbox custody, but no single field said whether release copy itself could be published, wired, cross-posted, emailed as a release, or used to announce a public presskit.

Solution: Added `press_release_permission_gate = HOLD_NO_PRESS_RELEASE_PUBLICATION` to the press release/templates owner doc and defined surface-specific `ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` as the only future allow value. The allow path requires exact surface/owner, real asset IDs, Campaign 01 `KEEP`, presskit minimum or explicit no-presskit/no-Steam state, official inbox custody, public CTA gates, press `send_permission_gate` for targeted email, Steam announcement gate for Steam reuse, public post gate for social/blog release copy, private access gate where access is referenced, Steam support gate where routes are named, localization gate where localized, Promise Lint, asset metadata claim checks, AB-009/KPI decision-read fields where agency proof is claimed, route/UTM/source fields, and wire/embargo rollback proof. Propagated the boundary to presskit, website, Campaign 02, launch war-room, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Reusing `send_permission_gate` was rejected because targeted press email permission is not public publication permission. Reusing `public_cta_permission_gate` was rejected because link permission is not content, claim, or distribution permission. Reusing `steam_announcement_permission_gate` was rejected because the same release can appear on site, email, social/blog, wire, localized one-pagers, or presskit pages outside Steamworks. Creating a separate presskit tracker was rejected by owner-local anti-sprawl; the release/template owner doc owns the copy boundary.

Scalability potential: Low budget work can keep release skeletons and presskit folders ready without accidentally creating a public launch signal. Middle/High/Ultra launch operations can later scale Steam page, demo, festival, presskit-live, wire, regional, and social/blog release surfaces from one explicit publication gate while preserving the separate send, CTA, Steam, support, access, localization, and post gates.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No press release, presskit publication, press send, Steam news reuse, wire copy, public post, CTA, account/browser action, runtime action, or build action occurred.

## Decision 222 - Steam Page Publication Needs An App/Page Gate

Problem: Steam page docs had a Launch Gate V0, asset checklist, copy matrix, CTA activation, and announcement gates, but no single machine-readable field for the act of publishing the public Coming Soon/store page. Asset existence, page draft completion, a Steamworks app shell, candidate URL, CTA planning, Steam announcement approval, press release approval, or wishlist readiness could be misread as permission to change page visibility or claim "Steam page is live".

Solution: Added `steam_page_publish_permission_gate = HOLD_NO_STEAM_PAGE_PUBLICATION` to the Steam page asset/checklist owner doc and defined app/page-specific `ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` as the only future allow value. The allow path requires exact app ID/URL, Steamworks/admin custody, owner, rollback owner, current official Steamworks rule recheck, Campaign 01 `KEEP`, first capture `KEEP_TESTING`, asset metadata/QA/source/build proof, one agency-decision proof asset with AB-009/KPI decision-read fields, copy/tag/capsule/trailer checks, Early Access/demo copy truth where used, official inbox/contact custody, support/bug-report owner plan, and no unsupported multiplayer/performance/large-world/date/AI-looking/competitor-war claims. Propagated the boundary to store copy, wishlist/Next Fest, wishlist conversion, Campaign 02, Campaign 04, demo/playtest, launch war-room, analytics, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Reusing `public_cta_permission_gate` was rejected because external link permission is not page publication permission. Reusing `steam_announcement_permission_gate` was rejected because a page can be published silently and still change public state. Reusing press release approval was rejected because release copy is a separate public surface. Treating the existing Launch Gate V0 prose as enough was rejected because future scripts/operators need a single filterable field.

Scalability potential: Low budget work can assemble copy/assets without accidentally opening Steam traffic. Middle/High/Ultra launch operations can later publish the page from one explicit app/page gate, while announcements, public links, paid traffic, showcase submissions, social posts, and press releases remain independently gated.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No Steam page publication, visibility change, public demo/store surface, wishlist campaign, CTA, announcement, press release, public post, spend, account/browser action, runtime action, or build action occurred.

## Decision 223 - Public Demo And Playtest Access Need A Surface Gate

Problem: Demo/playtest docs separated public CTA links from private access links, and private access already had a recipient/batch gate, but public demo exposure itself was still mostly prose. A build launching, Steam page publication, CTA approval, private access approval, known-issues draft, feedback form, announcement draft, or "first route playable" note could be mistaken for permission to expose a public Steam demo, public Playtest signup/tranche, Next Fest demo availability, demo-live claim, or public demo feedback route.

Solution: Added `demo_public_access_permission_gate = HOLD_NO_PUBLIC_DEMO_ACCESS` to the demo/playtest owner doc and defined surface-specific `ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED` as the only future allow value. The allow path requires exact app/demo/playtest surface, build ID, owner, rollback/disable owner, current Steamworks rule recheck, first route playable start to finish, no first-30-minute crash on target hardware, save/load boundary, settings/controls/accessibility boundary, known-issues copy, Steam page publish gate, destination-specific public CTA gate, Steam support gate, named support/bug triage owners, public route/consent fields, AB-009/KPI decision-read fields where public copy claims gameplay/pressure/route-risk proof, and no unsupported multiplayer/performance/date/feature/competitor claims. Propagated the boundary to Campaign 03, Campaign 04, playtester recruitment, launch war-room, Steam support/forums, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger.

Rejected Alternatives: Reusing `private_access_permission_gate` was rejected because public demo/Playtest access creates public support/review/wishlist exposure, not a recipient-bound preview. Reusing `public_cta_permission_gate` was rejected because link permission is not build readiness, support coverage, or demo-disable authority. Reusing `steam_page_publish_permission_gate` was rejected because a store page can be public while demo access remains unsafe. Keeping the Demo QA Gate prose-only was rejected because scripts/operators need one filterable field.

Scalability potential: Low budget work can prepare telemetry, forms, and Playtest copy without opening public access. Middle/High/Ultra launch operations can later open public demo, Playtest, Next Fest, announcement, creator, press, paid, and support surfaces from explicit gates without mixing public routes and private access.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No public demo, Steam Playtest signup/tranche, Next Fest demo, demo-live claim, public feedback route, private access, CTA, announcement, press release, public post, account/browser action, runtime action, or build action occurred.

## Decision 224 - Steam Next Fest Commitment Must Use The Existing Submission Gate

Problem: Steam Next Fest was tracked in the showcase CSV as `SHOW-001`, but the Steam wishlist plan and Campaign 04 readiness prose still allowed an operator to infer commitment from Steam page readiness, public demo readiness, CTA readiness, Steam announcement approval, or "before committing" prose. That would bypass the one-shot event nature of Next Fest and the existing `submission_permission_gate` owner-local tracker field.

Solution: Bound Next Fest registration, commitment, participation claims, and event-beat reservation to `SHOW-001` in `Press/SHOWCASE_SUBMISSION_TRACKER.csv`. Kept the existing gate instead of inventing a duplicate field: current state remains `submission_permission_gate = BLOCKED_NOT_READY`, and only `ALLOW_SHOWCASE_SUBMIT_VERIFIED` on `SHOW-001` can permit commitment. Updated the Steam wishlist/Next Fest plan, Campaign 04, showcase playbook, control tower, README, prep directions, daily loop, risk register, backlog, and source ledger to state that Steam page publish, public demo access, public CTA, support, announcement, and asset proof are prerequisites but not substitutes for the tracker row gate.

Rejected Alternatives: Creating a new `next_fest_commitment_permission_gate` was rejected because `SHOW-001` already owns the event route in the showcase tracker. Reusing `demo_public_access_permission_gate` was rejected because public demo readiness does not consume or protect the one-shot Next Fest slot. Reusing `steam_page_publish_permission_gate` or `public_cta_permission_gate` was rejected because page/link readiness is not event eligibility, deadline, owner, rollback, or measurement proof.

Scalability potential: Low budget work can continue Steam/demo preparation without accidentally burning the Next Fest option. Middle/High/Ultra launch operations can later connect event registration, public demo, Steam page, announcement, creator warmup, support, and measurement from one explicit `SHOW-001` row without duplicating authority.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_CSV only. No Next Fest registration, commitment, participation claim, event-beat reservation, public demo, CTA, announcement, submission, account/browser action, runtime action, or build action occurred.

## Decision 225 - SN2 Pain Buckets Need V5 Currentness Without Rewriting The Capture Thesis

Problem: Capture docs and RISK-046 still pointed at the V4 Steam API snapshot. SN2 review volume is moving quickly after launch, so first-capture priorities could be driven by stale competitor pain or, worse, misread as a competitor-collapse thesis.

Solution: Added a V5 official-platform currentness pass in the monitoring owner doc using Steam review API, Steam appdetails API, and the public Steam store page. Updated RISK-046, the first capture call sheet, the `PLAN-SHOT-006` pain modifier, asset-intake freshness example, backlog row 196, source ledger, status, and log. Kept V4 recent-negative buckets directional only because V5 refreshed volume/display state but did not sample new review text.

Rejected Alternatives: Replacing V4 negative samples with unsampled assumptions was rejected because review volume/currentness is not text evidence. Treating Korean `Mixed` as global weakness was rejected because the global and English reads remain `Very Positive`. Writing public copy around SN2 pain was rejected because the monitoring file is private proof-priority evidence only.

Scalability potential: Low budget capture stays focused on HECTON-native proof instead of chasing competitor drama. Middle/High/Ultra campaigns can later reuse the same freshness field to gate creator packets, Steam copy, and paid tests from current official-platform evidence without changing public positioning.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_SOURCE only. No capture, asset promotion, public comparison copy, outreach, browser/account action, runtime action, or build action occurred.

## Decision 226 - Creator Pain-Backed Sends Must Inherit SN2 V5 Currentness

Problem: The creator outreach owner doc still labeled the SN2 pain fit rules as 2026-05-19. It required freshness fields, but did not state that the current 2026-05-20 official-platform read remains competitor-positive. A future sender could use stale pain wording to justify hostile creator copy.

Solution: Renamed the section to V5, added the currentness boundary, and required pain-backed send packets to name `Monitoring SN2 Steam API/Page Refresh V5` plus the exact private pain bucket in `pain_freshness_source`. Added a hard gate that SN2-active rows are audience-fit evidence only and that current competitor-positive reads forbid "players are angry" framing. Logged backlog row 197 and source ledger addendum.

Rejected Alternatives: Leaving the date stale was rejected because open-tab operator work starts from this file. Copying V5 counts into every creator row was rejected because CRM rows are recipient state, not competitor monitoring truth. Filling live CRM send-log or asset metadata rows was rejected because no send occurred and no real asset exists.

Scalability potential: Low budget creator work avoids petty or stale SN2-pain messages. Middle/High/Ultra creator waves can later scale from the same currentness field without mixing audience-fit evidence, private pain buckets, and public copy.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_SOURCE only. No creator send, CRM send-log fill, asset promotion, public comparison copy, browser/account action, runtime action, or build action occurred.

## Decision 227 - Priority Creator Drafts Must Not Treat 2026-05-19 RSS As Current Send Proof

Problem: `PRIORITY_50_MESSAGE_DRAFTS_FROM_RAW.md` said six creators were "currently covering SN2" based on 2026-05-19 RSS checks. On 2026-05-20 that wording is stale enough to be dangerous: it can be pasted into outreach reasoning as current-send proof without same-day channel/contact verification.

Solution: Reworded the section to "2026-05-19 Data / 2026-05-20 V5-Gated", changed the table from `Current signal` to `Recorded signal`, rewrote all six SN2-active rows as dated RSS signals, and required same-day channel/route recheck before send. Added the V5 pain-freshness source requirement and logged backlog row 198 plus source ledger/status/log entries.

Rejected Alternatives: Leaving the draft untouched was rejected because it is a human-facing send draft file. Updating live CRM rows was rejected because no same-day channel verification happened. Removing the microbatch was rejected because the rows are still useful audience-fit evidence after proof assets exist.

Scalability potential: Low budget outreach avoids stale-current claims. Middle/High/Ultra creator waves can later scale from dated recorded signals plus same-day verification without treating old RSS as permission.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_SOURCE only. No creator send, CRM send-log fill, asset promotion, public comparison copy, browser/account action, runtime action, or build action occurred.

## Decision 228 - Raw Lead Expansion Must Stay Parked Until Asset-Gap Proof Exists

Problem: `RAW_LEAD_EXPANSION_QUEUE.md` still presented `Target: 300 rows` and `100 from Subnautica/Subnautica 2 indices` as the next pass, while `CREATOR_OUTREACH_DATABASE.md` still said to add Subnautica 2 launch streamers. That conflicts with the current bottleneck: CRM-100 exists, no raw rows are staged, and no creator-send-ready assets exist.

Solution: Parked both expansion surfaces behind first asset-gap proof. Raw expansion now resumes only after capture/demo evidence shows a segment gap the live CRM cannot cover, and SN2 launch-streamer sourcing requires a proven direct-underwater-survival asset gap plus planned same-day currentness/contact-route verification. Logged backlog row 199 and source ledger/status/log entries.

Rejected Alternatives: Expanding toward 300 rows now was rejected because it would create unverified volume without usable assets. Deleting the expansion docs was rejected because the source queues are still useful after asset proof. Promoting raw rows into live CRM was rejected because no same-day route verification happened.

Scalability potential: Low budget work stays focused on proof assets. Middle/High/Ultra outreach can later scale raw sourcing from specific segment gaps instead of generic lead quotas.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_SOURCE only. No lead expansion, CRM send-log fill, outreach, browser/account action, runtime action, or build action occurred.

## Decision 229 - Live CRM Hot SN2 Rows Must Preserve Dated Evidence Instead Of Current Claims

Problem: Six live CRM hot microbatch rows still contained prose such as "Currently covering SN2" and "Hot current". That language is stale after 2026-05-19 and can be read as send-time truth, even though the rows still require same-day channel/contact verification.

Solution: Updated only the wording fields in `Data/CREATOR_VERIFICATION_TEMPLATE.csv` for Kage848, AldemarHD, Neyreyan, Zombyra, HelyaLP, and SpielbaerLP. The rows now preserve the RSS evidence as 2026-05-18/19 recorded signals and explicitly require current-channel recheck before any send. Status, paid creator gate, send-log, route, and asset fields stayed unchanged. Logged backlog row 200 and source ledger/status/log entries.

Rejected Alternatives: Leaving stale CRM prose was rejected because live CRM is the highest-risk paste source. Filling send-log or route fields was rejected because no human route verification happened. Deleting the rows was rejected because they remain useful high-fit targets after proof assets exist.

Scalability potential: Low budget outreach keeps high-fit recipients without turning old RSS into current proof. Middle/High/Ultra creator waves can later filter by dated evidence plus same-day verification without corrupting CRM state.

Hardware Impact: 0us measured runtime impact. STATIC_CSV/STATIC_DOC only. No status promotion, send-log fill, asset promotion, outreach, browser/account action, runtime action, or build action occurred.

## Decision 230 - Entry And Raw Lead Docs Must Not Reopen Lead-Volume Work By Default

Problem: The Marketing README still described `RAW_LEAD_EXPANSION_QUEUE.md` as scaling toward 300-1000 leads, `RAW_PUBLIC_CREATOR_LEADS_README.md` still said to verify the top 250 per week, and Campaign 00 still listed Top 250 verification batches as a current-looking workstream/KPI. Those lines undermine the active CRM-100/asset-proof gate.

Solution: Reworded the README, raw leads README, and Campaign 00 so raw lead scaling is parked until first assets prove a CRM segment gap. Preserved the historical Top 250 data but made it explicit that it is parked/historical, not current default work. Logged backlog row 201 and source ledger/status/log entries.

Rejected Alternatives: Deleting raw lead documentation was rejected because it remains useful after asset proof. Leaving "verify top 250 per week" was rejected because it creates quota pressure. Expanding CRM from raw rows was rejected because no asset-gap proof or route verification happened.

Scalability potential: Low budget work stays on proof assets and existing CRM rows. Middle/High/Ultra outreach can later scale only from specific asset-proven audience gaps.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No lead expansion, CRM promotion, outreach, browser/account action, runtime action, or build action occurred.

## Decision 231 - Residual Currentness Tokens Must Not Poison The Next Audit

Problem: After the main CRM/Priority-50 cleanup, residual paste-adjacent wording still existed in `SEGMENT_PITCH_MATRIX.md` and one Priority-50 row. Raw verification batch sheets also used a vague "audience gap" phrase that could reopen verification volume without proving the live CRM cannot cover the segment.

Solution: Converted the remaining SN2-current wording to dated recorded audience-fit evidence, changed Wanderbots' Priority-50 angle to recorded fit, tightened the 1000-row/raw-batch gates to require first asset proof plus a segment gap the live CRM cannot cover, and removed exact stale tokens from backlog/source trace so targeted grep stays useful.

Rejected Alternatives: Leaving the strings because they were mostly internal was rejected because these files are operator-facing and paste-adjacent. Reopening raw lead work was rejected because CRM-100 still has no send-ready asset path and no send-log fields. Deleting raw batch sheets was rejected because they are useful only after a source-backed post-asset sprint.

Scalability potential: Low budget execution keeps agent time on proof assets and existing CRM fit. Middle/High/Ultra outreach can later scale raw verification only from a specific asset-proven CRM coverage gap, preserving creator segmentation instead of rebuilding volume for its own sake.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_SOURCE only. No lead expansion, CRM promotion, send-log fill, outreach, browser/account action, runtime action, or build action occurred.

## Decision 232 - Paid Creator Scenario Tables Must Not Override The CRM Gate

Problem: The paid creator owner gate existed, but several scenario and planning surfaces still said paid creator tests could happen after organic fit, organic replies, demo stability, or strong demo retention. Those phrases are weaker than the live CRM permission field and can be read as spend approval during a fast launch pass.

Solution: Updated the low-budget scenario tables, experiment spend order, first-demo outreach batch plan, brand budget reality, and P3 backlog paid-spend table so every paid creator path names the selected CRM row requirement: `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.

Rejected Alternatives: Leaving the older wording because the control tower already had the hard gate was rejected; operators often work from budget/campaign docs directly. Adding a new paid-spend file was rejected because the existing owner surfaces already exist and only needed tighter wording. Filling any CRM paid gate was rejected because no demo/Steam baseline, payment owner, route, disclosure, asset, or result-inspection proof exists.

Scalability potential: Low budget execution keeps spend at 0 USD until asset proof and CRM permission exist. Middle/High/Ultra campaigns can later test paid creators without losing row-level custody, disclosure, route class, creator utility, and stop-rule proof.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No paid creator deal, payment, paid brief, key/access row, CRM promotion, outreach, browser/account action, runtime action, or build action occurred.

## Decision 233 - Key And Private Access Shorthand Must Not Override Recipient Gates

Problem: Several creator, campaign, press, and legal docs still used shorthand such as key policy ready, keys with tracking, issue small batches, verified recipients, or QA route. Those are not permission. A key or private-preview route can leak before public launch even if no public post exists.

Solution: Replaced the shorthand in A-tier pitch gates, outreach calendar, creator database, CRM fraud checks, legal key distribution, presskit key policy, review-key/access protocol, and Campaign 03 with the explicit recipient/batch requirement: `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, exact access-log fields, and disclosure.

Rejected Alternatives: Leaving the older wording because the review-key owner doc already has the gate was rejected; operators paste from pitch, calendar, presskit, and campaign docs. Creating a new key checklist was rejected because the owner protocol and existing consumer docs already exist. Sending or logging any key/access row was rejected because no build, recipient allow row, inbox custody, or access log exists.

Scalability potential: Low budget testing avoids key leakage and orphaned access routes. Middle/High/Ultra campaigns can later scale private previews, press access, Steam Playtest, and Curator Connect from recipient-specific gates instead of broad readiness prose.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No key, private access, Playtest invite, Curator Connect copy, CRM promotion, outreach, browser/account action, runtime action, or build action occurred.

## Decision 234 - Copy Banks Must Not Treat Access Logs As Permission

Problem: After the first key/private-access cleanup, residual copy-bank and planning surfaces still used "private access log", "key/access log ready", "key policy", or demo/key shorthand. These phrases can be read as permission even though an access log is only evidence storage and a policy is not a recipient/batch allow decision.

Solution: Replaced the residual shorthand in website/signature copy, agent workflow, brand bible, mass outreach, segment matrix, pitch bank, Priority-250 sheet, Curator Connect playbook, press-angle bank, launch war-room, regional surfaces, partnership terms, roadmap Promise Lint, prep directions, presskit plan, backlog, and source ledger. Public links now remain under Official CTA Link Activation Gate V0. Private demo/key/playtest/preview/Curator Connect routes require recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, disclosure, and exact access-log fields. Normalized one touched Priority-250 display name to ASCII (`DaddelBaerTV`) to remove a known encoding hit.

Rejected Alternatives: Leaving historical or "obvious" shorthand was rejected because operators paste from these docs under launch pressure. Creating another access checklist was rejected because the owner protocol and permission field already exist. Filling any access or CRM send fields was rejected because no recipient/batch permission, inbox custody, asset proof, or human send exists.

Scalability potential: Low budget execution can prepare copy without accidentally opening controlled access. Middle/High/Ultra campaigns can later scale public CTA, private preview, creator, press, and Curator Connect routes from explicit machine gates instead of ambiguous log/policy readiness.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No key, private access, public CTA, public post, CRM promotion, outreach, browser/account action, runtime action, or build action occurred.

## Decision 235 - Live/Exists Wording Must Not Become Surface Permission

Problem: Several execution surfaces still used shorthand where `Steam page live`, `account exists`, `Steam URL`, or no-embargo wording could be read as enough to spend, post, run a creator/press batch, paste profile fields, or allow publication. Those states are facts, not permissions.

Solution: Updated the budget scenario, segment timing, social account field kit, social Steam page post row, pinned post rule, first-10-post list, post-bank Steam Page Live bundle, and no-embargo template. Added backlog row 206 and source ledger trace. The new wording requires `steam_page_publish_permission_gate`, Official CTA Link Activation Gate V0, `public_post_permission_gate`, `account_registration_permission_gate`, press/public-release gates, or logged preview-access gates for the exact surface.

Rejected Alternatives: Relying on existing owner gates alone was rejected because operators often work from budget/social/post-bank/segment docs directly. Broadly rewriting every safe `Steam page` mention was rejected because policy, CRM notes, and forbidden-copy examples still need those words. Performing account/browser work was rejected because official inbox custody, account registration permission, vault, recovery, 2FA, and backup-code records are still blocked.

Scalability potential: Low budget work can prepare spend and social copy without opening public surfaces. Middle/High/Ultra launch operations can later scale Steam page announcements, pinned posts, creator batches, press beats, and no-embargo previews from explicit surface gates and route logs.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No spend, post, public CTA, Steam page action, account/browser action, private access, creator/press send, runtime action, or build action occurred.

## Decision 236 - Current Labels Must Match The Active V5 State

Problem: Top-level marketing entry files still had active-looking 2026-05-19 labels even after 2026-05-20 V5 monitoring and gate cleanup landed. The stale labels were narrow, but they sit in files future agents open first.

Solution: Updated the backlog current execution cut to 2026-05-20, changed the control tower external reality section to V5 with the official-platform review counts already recorded in monitoring/source logs, and promoted the daily loop heading to `2026-05-20 Active Control Tower Loop V1`. Added backlog row 207 and source ledger trace. Historical source ledger entries were left dated to their original evidence events.

Rejected Alternatives: Leaving the labels stale was rejected because they are first-read current-state headers. Rewriting all historical `DONE/ACTIVE 2026-05-19` rows was rejected because those are completion dates, not current-state labels. Fetching new web data was rejected because this change only propagates the already-recorded V5 source boundary.

Scalability potential: Low budget agent loops start from the right current cut. Middle/High/Ultra launch prep can continue using historical evidence rows without confusing their dates with the active state.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No public copy, outreach, browser/account action, runtime action, or build action occurred.

## Decision 237 - Approved-Link And Demo-Ready Placeholders Must Name Their Machine Gates

Problem: After the main access/page/social cleanup, paste-adjacent templates still contained weaker placeholders: `[approved link only]`, `[approved access route only]`, generic approved Steam/demo CTA end cards, and showcase target rows that said `Demo ready` or store-page readiness. Those words are easy to paste into emails, trailer endings, or event planning as permission even though the owner gates are separate.

Solution: Replaced the weak placeholders in presskit email copy, A-tier pitch copy, Pitch Bank email copy, showcase public/private route boundaries, showcase target rows, measurement Steam-page beat row, KPI `unknown` route rules, Steam trailer beat sheets, asset QA CTA rules, and prep short-form pattern. The new text requires the exact surface gate: Steam page publication, public demo access, public CTA, private access, press release/public presskit, or showcase submission permission as applicable. Added backlog row 208 and source ledger/status/log entries.

Rejected Alternatives: Leaving the placeholders because deeper gate docs already exist was rejected because these are the copy surfaces humans paste from. Replacing all link mentions globally was rejected because policy examples and historical source ledger entries can safely mention links. Performing browser/account/public CTA work was rejected because the required project custody and machine allow rows are still absent.

Scalability potential: Low budget execution can keep preparing copy without opening public or private routes. Middle/High/Ultra campaigns can later reuse the same templates by filling exact gate-backed URLs, route classes, and provenance fields instead of rewriting from scratch.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No public CTA, post, showcase submission, press send, creator send, private access, browser/account action, runtime action, or build action occurred.

## Decision 238 - Metrics Need Permission Gate/Source, Not Only Route Class

Problem: Analytics and KPI schemas had route/provenance fields, but several report tables could still count campaign events, creator attribution, feedback, or weekly signals without recording the machine gate/source that allowed the route. That leaves a reporting loophole: a row can look measured while the public CTA, private access, creator send, press send, support route, or owned-audience permission is unknown.

Solution: Added permission gate/source columns and quarantine rules to the analytics event, creator attribution, feedback, minimum packet, weekly report, and rules sections. Expanded the KPI creator outreach dashboard fields to include `asset_ids_sent`, `creator_utility_score`, `creator_send_gate`, `send_route_class`, `reply_consent_provenance`, and `send_gate_source`. Synchronized the control tower, daily loop, and README so entry points no longer describe reporting as route/provenance-only.

Rejected Alternatives: Leaving the current schema was rejected because `unknown` route/provenance and blank permission sources would still be reportable by habit. Filling current KPI/CRM rows was rejected because no public route, send, access, CTA, or measured signal happened. Creating a new dashboard file was rejected because the existing analytics and KPI owner docs are the correct surfaces.

Scalability potential: Low budget work avoids false weekly wins. Middle/High/Ultra campaign operations can later scale paid, creator, press, support, and owned-audience reporting from machine-gated rows instead of manually reconciling screenshots and comments.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No KPI row fill, public CTA, post, send, private access, browser/account action, runtime action, or build action occurred.

## Decision 239 - Support Reply Templates Must Not Carry Generic Approved-Route Placeholders

Problem: Steam support/review and launch war-room response templates still used a generic support-route placeholder. The documents had a support gate above the templates, but the pasteable reply text itself could still be copied into a public response without route custody.

Solution: Replaced the generic support route placeholder with gated support-route wording in the performance and bug templates. Added explicit replacement rules requiring `steam_support_permission_gate = ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED`, owner-controlled inbox/form custody, `route_class = support_route`, `consent_provenance = support_report`, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` when the route is linked publicly. Added backlog row 210 and source ledger/status/log entries.

Rejected Alternatives: Leaving the template as-is was rejected because support replies are high-pressure paste surfaces. Creating a new support checklist was rejected because the support playbook already owns the gate. Filling a support route was rejected because no Steam surface, owner custody, or support allow gate exists.

Scalability potential: Low budget launch prep can keep support copy ready without exposing personal or unowned routes. Middle/High/Ultra launch operations can later insert a support URL only when the exact app/build/surface and route-provenance fields are known.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No support route, Steam/forum reply, public CTA, account/browser action, runtime action, or build action occurred.

## Decision 240 - Pasteable CTA URL Placeholders Must Name The Specific Gate

Problem: Multiple pasteable templates still used generic approved-after-CTA placeholders for Steam, demo, presskit, Discord, feedback, and asset URLs. The owner gate documents were stricter, but bracketed placeholders in email/social/release/event templates are the text humans copy during launch pressure.

Solution: Replaced the old generic approved-CTA placeholders in press release/email templates, social profile and pinned-post templates, owned-audience emails, demo outreach, Next Fest campaign copy, Steam wishlist clip ending, press follow-up copy, post-bank bundle, and localization review form. The replacement placeholders name the relevant machine gates: Steam page publication, public demo access, press release/public presskit, Discord open, owned-audience or Steam support feedback route, and public CTA.

Rejected Alternatives: Leaving generic placeholders was rejected because they compress too many permission decisions into one word. Replacing them with final URLs was rejected because no live URL or gate allow exists. Creating a new link-placeholder file was rejected because the current owner docs already hold the route rules.

Scalability potential: Low budget prep keeps templates copyable without opening public routes. Middle/High/Ultra launch operations can later swap placeholders for real URLs only after the exact surface gates pass.

Hardware Impact: 0us measured runtime impact. STATIC_DOC only. No public link, post, release, email, signup, Discord, account/browser action, runtime action, or build action occurred.

## Decision 241 - Residual Asset-Link And Event CTA Shorthand Must Not Survive In Adjacent Copy

Problem: After bracket-placeholder cleanup, residual paste-adjacent lines still said approved asset link, approved screenshots, Steam/presskit link after CTA activation, approved Steam CTA after activation, wishlist after CTA activation, or one asset link. These phrases are weaker than the actual permission graph and appear in the files operators copy from: trailer, creator pitch, showcase, paid test, key compliance, and demo planning surfaces.

Solution: Replaced those residual phrases with explicit gates on the same owner surfaces. Asset references now require asset metadata claim checks, QA, creator utility where creator-facing, and `creator_send_gate` where creator-facing. Public Steam/presskit/demo/trailer/event links now name `steam_page_publish_permission_gate`, `press_release_permission_gate`, `demo_public_access_permission_gate`, and destination-specific `public_cta_permission_gate`. Private access references name recipient/batch `private_access_permission_gate`, official inbox custody, disclosure, and access-log fields. Showcase and paid rows now also name `submission_permission_gate` and `spend_permission_gate`.

Rejected Alternatives: Leaving generic wording was rejected because these are operator-facing copy banks and tables, not archival analysis. Creating another gate document was rejected because the owner docs and machine fields already exist. Filling any URL, send-log, spend, or access field was rejected because no real asset, route custody, public CTA, spend approval, or human send exists.

Scalability potential: Low budget work can keep copy and event/spend planning ready without opening public routes. Middle/High/Ultra campaign execution can later substitute real links only through the same machine gates, avoiding last-minute permission reconstruction.

Hardware Impact: 0us measured runtime impact. STATIC_DOC/STATIC_DATA only. No public link, post, event submission, email, spend, creator send, key/access route, account/browser action, runtime action, or build action occurred.
