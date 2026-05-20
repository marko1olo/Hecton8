# Status SHINOBU_81

Agent: SHINOBU_81
Domain: COMPETITIVE_INTELLIGENCE_AND_UX_ANALYST
Task count: 13
Evidence class: STATIC_DOC / STATIC_DATA
Runtime impact: none

## 2026-05-19 Active Marketing Work Addendum 120

- [x] CTA paste-surface bypass audited | DOD: searched active Marketing social, post-bank, community, trailer, campaign, Steam, audience, and press/showcase docs for paste-ready URL/wishlist/signup/Discord CTA placeholders after the Official CTA Link Activation Gate V0 landed. | Alternative rejected: relying on the analytics doc alone while copy banks still had raw `[URL]` or `Steam wishlist` snippets. | Estimate: 0us runtime impact.
- [x] CTA activation propagated | DOD: updated existing docs so public CTA surfaces use approved destination placeholders after CTA activation or no-link feedback/end-card fallbacks. | Alternative rejected: creating another CTA checklist instead of fixing the actual paste sources. | Estimate: 0us runtime impact.
- [x] Source/backlog/risk trace updated | DOD: added row 117 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, and broadened RISK-048 to trailer, bio, email, and showcase CTA surfaces. | Alternative rejected: terminal-only trace. | Estimate: 0us runtime impact.
- [x] End-of-change validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; CRM 100 rows with unchanged status split and 0 filled send-log fields; asset metadata 13 blocked planned rows; targeted text, legacy/corruption, UTM ID, CTA paste-surface, and backtick path audits clean. | Alternative rejected: claiming safety from static edits without running the daily validation cut. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no public link, Steam page, signup form, account/browser action, outreach, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 121

- [x] Public/private route separation added | DOD: updated demo/playtest, review-key/access, keys compliance, demo outreach, Next Fest, showcase, and playtester screening docs so public CTA traffic is separate from private Steam Playtest/demo/key/preview access. | Alternative rejected: letting `approved CTA` language accidentally bless private build/key links as public conversion routes. | Estimate: 0us runtime impact.
- [x] Access route logging fields strengthened | DOD: added route class to access/key flow, launch checklist, showcase recap, and private access message requirements. | Alternative rejected: relying on "known issues" and recipient verification without a machine-readable route class. | Estimate: 0us runtime impact.
- [x] Source/backlog/risk trace updated | DOD: added row 118 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, and RISK-049 for private access leaking into public CTA surfaces. | Alternative rejected: burying the boundary in individual docs only. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; CRM and asset metadata state unchanged; targeted text, legacy/corruption, UTM ID, public/private route, and backtick path audits clean. | Alternative rejected: route changes without static audits. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no public demo, key, Playtest, preview access, account/browser action, outreach, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 122

- [x] Consent/provenance route gates added | DOD: updated tester recruitment, owned-audience forms, feedback taxonomy, Steam support response, and launch war-room docs so forms/support/feedback rows require consent/provenance and route class. | Alternative rejected: treating every form/email/support route as one generic contact pool. | Estimate: 0us runtime impact.
- [x] Form provider custody added | DOD: newsletter/signup forms now require owner-controlled form/list provider, explicit purpose, consent, unsubscribe/delete route, export custody, and CTA activation where public. | Alternative rejected: allowing personal Google/agent/disposable forms because they are quick to create. | Estimate: 0us runtime impact.
- [x] Source/backlog/risk trace updated | DOD: added row 119 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, and RISK-050 for mixed contact consent/provenance. | Alternative rejected: leaving consent separation implicit in scattered docs. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; CRM and asset metadata state unchanged; targeted text, legacy/corruption, UTM ID, consent/provenance, and backtick path audits clean. | Alternative rejected: form/support changes without static audits. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no form published, tester recruited, account/browser action, outreach, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 123

- [x] Route/consent reporting gap closed | DOD: updated KPI dashboard, analytics/UTM plan, and daily agent loop so feedback, form, support, link, creator-reply, and press-reply rows require route class and consent/provenance before signals are reported. | Alternative rejected: leaving route/consent as narrative gate text while dashboards still counted generic feedback/contact rows. | Estimate: 0us runtime impact.
- [x] Public CTA vs private access measurement boundary added | DOD: measurement packet now states CTA activation is public-link only and private demo/key/playtest/preview routes use access logs and route-class fields instead of public UTM packets. | Alternative rejected: letting private access links inherit public campaign tracking language. | Estimate: 0us runtime impact.
- [x] Source/backlog/risk trace updated | DOD: added row 120 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, and expanded RISK-050/top-risk text to include dashboard rows, event logs, and weekly reports. | Alternative rejected: terminal-only trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; CRM 100 rows with unchanged status split and 0 filled send-log fields; asset metadata 13 blocked planned rows; targeted text, legacy/corruption, UTM ID, measurement-route table, route/consent field, and backtick path audits clean. | Alternative rejected: reporting process edits without static audits. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no public link, form, support route, account/browser action, outreach, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 124

- [x] Entry docs route/consent gap closed | DOD: updated the control tower, README, and prep directions so route class and consent/provenance requirements are visible from the first Marketing entry points, not only from KPI/analytics deep files. | Alternative rejected: assuming future agents will discover the reporting gate after already opening task-specific docs. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 121 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum; repaired stale `Marketing/...` backtick paths in the control tower while it was touched. | Alternative rejected: leaving entry-doc propagation untracked or creating placeholder files for bad links. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; CRM and asset metadata unchanged; targeted text, legacy/corruption, UTM ID, entry route/consent, and backtick path audits clean. | Alternative rejected: entry-point edits without path/static validation. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no public link, form, support route, account/browser action, outreach, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 125

- [x] Pasteable post reporting metadata added | DOD: updated post bank, community templates, and social playbook so no-link posts, critique templates, forced reservation post, asset-to-post queue, and first public posts name route class and consent/provenance handling before replies become KPI signal. | Alternative rejected: letting draft copy leave docs without a reporting route. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 122 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: untracked content-surface gate changes. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; CRM and asset metadata unchanged; targeted text, legacy/corruption, UTM ID, backtick path, markdown table, and pasteable route metadata audits clean. | Alternative rejected: content-surface edits without table/path validation. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no post, account/browser action, public CTA, outreach, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 126

- [x] Creator CRM route/provenance fields added | DOD: live CRM CSV and schema now include `send_route_class` and `reply_consent_provenance` so creator replies cannot be counted as generic contact/newsletter/playtest/press consent. | Alternative rejected: hiding creator route class in notes or KPI-only rows. | Estimate: 0us runtime impact.
- [x] Creator send workflow and validation updated | DOD: first human-send packet, send log fields, current send-state HOLD check, daily validation cut, control tower, README, and risks now require creator send-route and reply-provenance fields. | Alternative rejected: changing CSV header without updating operating docs. | Estimate: 0us runtime impact.
- [x] Source/backlog/risk trace updated | DOD: added row 123 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, and expanded RISK-042/RISK-050. | Alternative rejected: untracked schema migration. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; CRM 100 rows with unchanged status split, new headers present, and 0 filled creator send/provenance fields; asset metadata unchanged; targeted text, legacy/corruption, UTM ID, markdown table, and backtick path audits clean. | Alternative rejected: schema migration without CSV/header/table validation. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no CRM row status changed, no outreach, account/browser action, public CTA, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 127

- [x] Press/curator route-provenance fields added | DOD: press and curator CSV trackers now include `send_route_class` and `reply_consent_provenance`; seed map, key compliance, and access protocol route replies through their own provenance bucket unless explicit separate opt-in exists. | Alternative rejected: using press/curator notes as implicit consent records. | Estimate: 0us runtime impact.
- [x] Source/backlog/risk trace updated | DOD: added row 124 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, and expanded RISK-050/top-risk text to include curator/press trackers and key logs. | Alternative rejected: untracked tracker schema migration. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; press tracker 30 rows and curator tracker 20 rows have new headers with 0 filled route/provenance fields; CRM and asset metadata unchanged; targeted text, legacy/corruption, UTM ID, markdown table, and backtick path audits clean. | Alternative rejected: tracker schema migration without CSV/header/path validation. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no press send, curator send, key issue, account/browser action, public CTA, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 128

- [x] Empty asset directory skeleton created | DOD: repo-root `MarketingAssets/` directory tree now matches the documented asset library layout, with no media files and no `.gitkeep` placeholders. | Alternative rejected: waiting until capture day to invent folders or adding fake placeholder assets. | Estimate: 0us runtime impact.
- [x] Asset ops doc/source/backlog updated | DOD: `Operations/ASSET_LIBRARY_NAMING_AND_VERSION_CONTROL.md`, source ledger, and backlog row 125 record that the skeleton exists locally but is not asset proof. | Alternative rejected: filesystem-only change with no handoff note. | Estimate: 0us runtime impact.
- [x] Backlog table audit repaired | DOD: malformed P1/P2 table headers in `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` now match their two-column rows; row 126/source ledger track the repair. | Alternative rejected: ignoring table audit failures in a touched control file. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: `MarketingAssets/` skeleton exists locally with all documented empty directories and no media files; Marketing file count 100; CSV parse OK for 9 files; CRM/asset/press/curator state unchanged except the already-documented headers; targeted text, legacy/corruption, UTM ID, markdown table, and backtick path audits clean. | Alternative rejected: leaving completed validation as chat-only memory after context compaction. | Estimate: 0us runtime impact.
- Verification status: local empty directories and docs/data-only; no asset proof, media import, account/browser action, outreach, public CTA, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 129

- [x] SN2 Steam API V3 refreshed | DOD: fetched public Steam appdetails/review API summaries and recent negative samples for app `1962700`; recorded V3 in `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md` with all-language and English `Very Positive` counts plus directional term hits. | Alternative rejected: relying on the prior V2 snapshot while SN2 review volume is still changing during launch week. | Estimate: 0us runtime impact.
- [x] Stale-pain risk trace updated | DOD: updated `Data/MARKETING_RISK_REGISTER.md` RISK-046, `Data/MARKETING_BACKLOG_INDEX.md` row 127, and `Data/SOURCE_LEDGER.md` so V3 remains a private capture-priority signal, not public comparison copy. | Alternative rejected: changing the monitoring doc without control/risk trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; CRM row/status/send-field state unchanged; asset metadata still 13 blocked planned rows; press/curator route-provenance fields remain 0 filled; `MarketingAssets/` skeleton and metadata paths resolve; targeted text, legacy/corruption, UTM ID, markdown table, and backtick path audits clean. | Alternative rejected: recording live external data without checking local table/path integrity after edits. | Estimate: 0us runtime impact.
- Verification status: docs/data/API-read only; no public copy, outreach, account/browser login, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 130

- [x] Agency/decision proof gate bound to first capture execution | DOD: updated the shotlist V3 priority note, QA first-pack composition, and Campaign 01 required inputs so first public testing needs identity, player verb, base/machinery, and one agency/decision proof asset (`PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`). | Alternative rejected: letting Campaign 01 advance from attractive identity/base stills while the fresh agency signal remains unproved. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 128 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: changing first-capture gate without routing future agents to the changed files. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; touched markdown table audit clean; targeted text, legacy/corruption, UTM ID, backtick path, and asset metadata path audits clean. | Alternative rejected: leaving first-capture gate edits without table/path validation. | Estimate: 0us runtime impact.
- Verification status: docs-only; no screenshot, clip, public post, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 131

- [x] Post-bank 72-hour sequence aligned to agency/decision proof gate | DOD: updated `Content/POST_BANK_AND_HOOK_LIBRARY.md` so the first 72-hour sequence refuses to run without `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`, prioritizes decision proof if missing by Hour 24, and requires viewers to understand one agency/decision proof before Hour 72 proceed. | Alternative rejected: letting the execution queue bypass the Campaign 01 gate by choosing only identity/machinery posts. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 129 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: post-bank-only change without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; touched markdown table audit clean; targeted text, legacy/corruption, UTM ID, and backtick path audits clean. | Alternative rejected: leaving execution-queue edits without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only; no public post, outreach, account/browser action, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 132

- [x] Entry docs expose agency/decision proof gate | DOD: updated `MARKETING_CONTROL_TOWER.md`, `README.md`, and `PREP_DIRECTIONS_NOW.md` so the first-read route says the first public packet needs identity, player verb, base/machinery, and one agency/decision proof asset. | Alternative rejected: hiding the gate only in Campaign 01/shotlist where a future agent might not start. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 130 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: entry-doc propagation without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; touched markdown table audit clean; targeted text, legacy/corruption, UTM ID, and backtick path audits clean. | Alternative rejected: entry-doc edits without table/path validation. | Estimate: 0us runtime impact.
- Verification status: docs-only; no screenshot, clip, public post, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 133

- [x] Steam/social launch surfaces bound to agency/decision proof | DOD: updated social first-post kit, Steam store copy matrix, Steam asset checklist, and Campaign 02 launch gate so first public posts and Steam page launch require `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`; `PLAN-SHOT-007` cannot substitute for decision proof. | Alternative rejected: letting launch surfaces treat anomaly/mood proof as equivalent to player-decision proof. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 131 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: launch-surface patch without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; touched markdown table audit clean; targeted text, legacy/corruption, UTM ID, and backtick path audits clean. | Alternative rejected: launch-surface edits without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only; no Steam page, social post, outreach, account/browser action, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 134

- [x] Press/curator/demo/showcase agency bypass closed | DOD: updated Curator Connect, presskit, wishlist/Next Fest, wishlist conversion, Steam review packet, showcase, and demo/playtest plans so their send/launch/submission/expansion gates require one readable player decision under pressure. | Alternative rejected: leaving press and event docs with weaker "first three screenshots" or threat/anomaly language after Steam/social gates were tightened. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 132 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: multi-surface gate propagation without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; touched markdown table audit clean; targeted text, legacy/corruption, UTM ID, backtick path, CRM state, asset metadata state, press/curator tracker state, and decision-proof phrase audit clean. | Alternative rejected: marking the multi-file propagation complete before static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no curator send, press send, demo, public event submission, account/browser action, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 135

- [x] Community paste-surface agency bypass closed | DOD: updated community post templates, community target asks, and public FAQ responses so threat/anomaly/mood posts ask whether the player decision reads before first-public testing advances. | Alternative rejected: relying on Campaign/Post-bank gates while pasteable community copy could still ask only about mood or threat. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 133 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: community-surface propagation without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; touched markdown table audit clean; targeted text, legacy/corruption, UTM ID, backtick path, CRM/asset state, and community-decision phrase audit clean. | Alternative rejected: marking paste-surface edits done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no community post, public reply, account/browser action, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 136

- [x] Surface-bypass risk/control loop added | DOD: added RISK-051 and daily loop stop rules for press, community, showcase, demo, curator, and wishlist surfaces that use mood/threat/anomaly proof without readable player decision proof. | Alternative rejected: relying on scattered gate docs without a central risk trigger and daily kill-check. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 134 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: risk/control loop change without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; touched markdown table audit clean; targeted text, legacy/corruption, UTM ID, backtick path, CRM/asset state, press/curator tracker state, and RISK-051/daily-loop text audit clean. | Alternative rejected: marking risk/control loop edits done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no send, submit, publish, demo, community post, account/browser action, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 137

- [x] Website/newsletter/playtester agency bypass closed | DOD: updated one-page site/presskit plan, owned audience plan, and playtester screening so site/signup/devlog/playtest expansion cannot advance from screenshot/demo traffic without agency/decision proof or explicit feedback measurement. | Alternative rejected: allowing a holding page, devlog list, or playtest route to become a soft launch from mood-only assets. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 135 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: audience/site gate change without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; touched markdown table audit clean; targeted text, legacy/corruption, UTM ID, backtick path, CRM/asset state, press/curator state, and audience/site decision-proof audit clean. | Alternative rejected: marking audience/site edits done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no site, signup form, newsletter, tester recruitment, account/browser action, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 138

- [x] Structured agency-proof metadata added | DOD: added `agency_decision_proof_gate` and `agency_decision_notes` to all 13 planned asset rows; only `PLAN-SHOT-006`, `PLAN-CLIP-001`, and `PLAN-CLIP-003` are pre-capture `AGENCY_PROOF_CANDIDATE` rows. | Alternative rejected: leaving agency proof in narrative notes where a dashboard or campaign gate cannot filter it. | Estimate: 0us runtime impact.
- [x] Intake/dashboard/control docs aligned | DOD: updated asset library workflow, QA checklist, KPI asset gate, Campaign 01, control tower, and daily loop so the new fields are required during first capture intake and validation. | Alternative rejected: changing CSV header without the operating docs that force people to fill it. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 136 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: schema update without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; targeted text, legacy/corruption, UTM ID, and backtick path audits clean; asset metadata has 13 rows, no blank agency fields, exactly three `AGENCY_PROOF_CANDIDATE` rows, all 13 `creator_send_gate = BLOCKED_PLANNED_CAPTURE`, and all 13 creator utility scores remain 0; CRM, press, and curator send/route fields remain 0 filled. | Alternative rejected: claiming the schema migration without static audits. | Estimate: 0us runtime impact.
- Verification status: docs/data-only so far; no screenshot, clip, public post, outreach, account/browser action, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 139

- [x] Feedback taxonomy agency class added | DOD: added `AGENCY_DECISION_READ` to the feedback taxonomy, common translation table, demo survey, and weekly digest. | Alternative rejected: mixing "what do you do?" and "what choice do I have?" under one vague player-verb bucket. | Estimate: 0us runtime impact.
- [x] Launch/demo/event gates aligned | DOD: launch war-room, first demo outreach, and Next Fest docs now hold expansion when public/creator/player feedback cannot name a pressure decision. | Alternative rejected: treating agency clarity as only a screenshot-pack issue while demo/event surfaces can still fail it. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 137 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: untracked feedback taxonomy change. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; targeted text, legacy/corruption, UTM ID, and backtick path audits clean; `AGENCY_DECISION_READ`/pressure-decision audit finds the new feedback, launch, demo, and event gates; asset metadata agency fields remain intact. | Alternative rejected: marking launch/demo feedback gates without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no launch, demo, Next Fest commitment, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 140

- [x] Creator send gates bound to agency proof | DOD: updated mass workflow, CRM schema, segment matrix, A-tier drafts, pitch bank, and priority-50 drafts so gameplay/pressure/route-risk creator sends require one factual `AGENCY_PROOF_CANDIDATE` asset with `agency_decision_notes`. | Alternative rejected: letting creator send-readiness stop at creator utility and `creator_send_gate` while agency proof remains optional. | Estimate: 0us runtime impact.
- [x] Planned status cannot masquerade as proof | DOD: creator workflow and CRM schema now state that planned `AGENCY_PROOF_CANDIDATE` rows are not send proof until capture QA makes them factual. | Alternative rejected: using pre-capture metadata labels as a proxy for real footage. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 138 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: untracked creator send-gate propagation. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; targeted text, legacy/corruption, UTM ID, and backtick path audits clean; creator agency-gate audit finds the new gate in all touched creator docs; CRM stays 100 rows with send fields 0 filled; asset metadata agency fields remain intact with three planned candidates and all send gates blocked. | Alternative rejected: marking creator-send propagation without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no creator outreach, browser/account action, public post, asset approval, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 141

- [x] Agency-decision measurement fields added | DOD: measurement plan and A/B experiments now include `ab-009`, `what_decision_next`, `agency_decision_read`, `AGENCY_DECISION_READ` coding, and explicit targets/stop rules for whether cold viewers can name a player decision. | Alternative rejected: treating player verb or mood clarity as sufficient agency proof. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 139 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: measurement-gate change without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; agency-measurement field audit finds `ab-009`, `what_decision_next`, `agency_decision_read`, and `AGENCY_DECISION_READ`; legacy/corruption, UTM ID, and backtick path audits clean after repairing stale trace paths; CRM stays 100 rows with send fields empty; asset metadata remains 13 rows with three planned agency candidates and all send gates blocked; press/curator trackers remain 30/20 rows. | Alternative rejected: claiming measurement changes without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 142

- [x] Agency-decision KPI fields propagated | DOD: dashboard and daily KPI loop now require `cold_read_agency_decision`, `what_decision_next`, `agency_decision_read`, and `agency_decision_read_comments` before a cold-read, first-public, creator, or campaign row can claim gameplay/pressure/route-risk agency proof. | Alternative rejected: letting AB-009 fields live only in analytics/experiments while weekly dashboard reporting loses them. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 140 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: dashboard gate change without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; agency KPI text audit finds the new dashboard/daily fields; legacy/corruption, UTM ID, and backtick path audits clean after repairing stale wildcard trace paths; CRM stays 100 rows with send fields empty; asset metadata remains 13 rows with three planned agency candidates and all send gates blocked; press/curator trackers remain 30/20 rows. | Alternative rejected: claiming dashboard propagation without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 143

- [x] Agency-decision reporting guard propagated to entry/risk docs | DOD: control tower, README, prep directions, and RISK-052 now require the viewer-named decision field before gameplay/pressure/route-risk agency proof can be reported. | Alternative rejected: leaving field names only in KPI/Analytics where a future agent may not start. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 141 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: entry/risk propagation without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; entry/risk agency field audit finds RISK-052 and the AB-009/KPI field names; legacy/corruption, UTM ID, and backtick path audits clean after repairing stale README wildcard path; CRM stays 100 rows with send fields empty; asset metadata remains 13 rows with three planned agency candidates and all send gates blocked; press/curator trackers remain 30/20 rows. | Alternative rejected: claiming entry/risk propagation without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 144

- [x] Campaign 01 T-24h bound to AB-009 | DOD: Campaign 01 metrics, kill criteria, required inputs, T-24h pass, T+72h note, and `KEEP` decision now require one agency candidate with `what_decision_next` / `agency_decision_read` fields before first public screenshot drop can advance. | Alternative rejected: allowing Campaign 01 to pass on genre/player-verb/capsule clarity while agency proof remains unmeasured. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 142 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` and source ledger addendum. | Alternative rejected: campaign gate change without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; Campaign 01 AB-009 text audit finds agency candidate, `what_decision_next`, `agency_decision_read`, 60% agency-decision metric, and `KEEP` dependency; legacy/corruption, UTM ID, and backtick path audits clean; CRM stays 100 rows with send fields empty; asset metadata remains 13 rows with three planned agency candidates and all send gates blocked; press/curator trackers remain 30/20 rows. | Alternative rejected: claiming Campaign 01 binding without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no cold-read test, public post, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 145

- [x] Steam page launch and assembly bound to AB-009 | DOD: Campaign 02, Steam asset checklist, and Steam copy matrix require one agency candidate with AB-009/KPI viewer-named decision fields before Steam launch, review packet, or copy selection can advance. | Alternative rejected: allowing Steam page movement from generic blind-read or agency-asset prose without the measured decision field. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 143 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 172, and log section. | Alternative rejected: Steam gate change without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; targeted Steam AB-009 text audit finds AB-009 and viewer-named decision fields across Campaign 02, Steam asset checklist, and copy matrix; legacy/corruption, UTM/experiment ID, backtick path, and rationale-order audits clean; CRM stays 100 rows; asset metadata remains 13 rows with three planned agency candidates and all 13 send gates blocked; press/curator trackers remain 30/20 rows. | Alternative rejected: marking Steam page binding complete without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no Steam page, cold-read test, public post, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 146

- [x] Creator human-send gates bound to AB-009 | DOD: first human-send packet, CRM readiness schema, segment/pitch banks, A-tier drafts, priority-50 drafts, and post-bank Hour 48 creator route require AB-009/KPI viewer-named decision fields for gameplay/pressure/route-risk sends. | Alternative rejected: treating factual agency candidate metadata and `agency_decision_notes` as enough for outreach. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 144 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 173, and log section. | Alternative rejected: creator send-gate changes without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; creator AB-009 text audit finds the send gates; active old AB-001/002/004-only send gate absent; legacy/corruption, backtick path, and rationale-order audits clean; CRM remains 100 rows with 0 filled send fields. | Alternative rejected: marking creator-send binding complete without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no creator outreach, cold-read test, public post, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 147

- [x] Historical AB trace rows marked superseded | DOD: backlog/source rows 68-70 now point to rows 139/143/144 for AB-009/KPI agency-proof authority. | Alternative rejected: leaving historical AB-001/002/004 rows unqualified after current gates moved to AB-009. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 145 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 174, and log section. | Alternative rejected: trace repair without audit trail. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; supersession text audit finds rows 139/143/144/145 references; legacy/corruption, backtick path, and rationale-order audits clean. | Alternative rejected: marking historical trace repair complete without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no cold-read test, creator outreach, public post, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 148

- [x] Public soft-launch surfaces bound to AB-009 | DOD: devlog/signup, wishlist/Next Fest, presskit, curator, showcase, social, and launch war-room gates require AB-009/KPI decision-read fields when they use first-page agency proof. | Alternative rejected: allowing soft-launch movement from "agency proof asset" prose without the measured viewer-named decision field. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 146 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 175, and log section. | Alternative rejected: public surface gate changes without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; public surface AB-009 text audit finds 25 hits; legacy/corruption, backtick path, and rationale-order audits clean; asset metadata remains 13 rows with three agency candidates and all 13 send gates blocked. | Alternative rejected: marking public soft-launch binding complete without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no signup, Steam page movement, curator send, presskit publish, showcase submission, social post, launch action, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 149

- [x] Daily/KPI agency field-set drift repaired | DOD: daily loop and KPI counting rule include `cold_read_agency_decision` with the other agency-proof fields. | Alternative rejected: relying on control tower/risk register while daily execution uses a narrower field set. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 147 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 176, and log section. | Alternative rejected: field-set correction without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; field-set audit finds `cold_read_agency_decision`; legacy/corruption, backtick path, and rationale-order audits clean. | Alternative rejected: marking daily/KPI drift repair complete without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no cold-read test, report, public action, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 150

- [x] Website shell bound to AB-009 | DOD: website hero, launch gate, presskit minimum packet, and presskit kill conditions require AB-009/KPI decision-read fields for first-page agency proof. | Alternative rejected: allowing the public site to advance from readable-decision prose without the measured field. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 148 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 177, and log section. | Alternative rejected: website gate change without router trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; website AB-009 text audit finds 4 hits; legacy/corruption, backtick path, and rationale-order audits clean. | Alternative rejected: marking website binding complete without static validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no website publish, presskit send, signup, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 151

- [x] Outbound bypass surfaces bound to AB-009 | DOD: paid microtests, community templates/targets, Discord open gate, devlog/news pipeline, press email templates, and preview-access batches require `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` before gameplay/pressure/route-risk agency proof can drive public movement, spend, or access. | Alternative rejected: relying on broad public-surface gates while these paste/send/spend docs still had narrower readable-decision prose. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 149 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 178, and log section. | Alternative rejected: untracked bypass patch. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; outbound AB-009 text audit finds the field gate in 7 touched outbound files; stale outbound bypass grep returns no matches; actual corruption pattern audit, backtick path audit, and rationale-order audit clean; CRM stays 100 rows with 0 send fields; asset metadata remains 13 rows, 3 planned agency candidates, and 13 blocked send gates; press/curator trackers remain 30/20 rows. | Alternative rejected: marking outbound surface gates done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no paid spend, community post/server, devlog publish, press email, access send, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 152

- [x] Operator routers bound to AB-009 | DOD: AgentOps batch protocol, low-budget spend ladder, press angle bank, and paid creator terms now require AB-009/KPI decision-read fields and route/provenance handling before gameplay/pressure/route-risk proof can drive spend, press angles, paid creator briefs, or send packets. | Alternative rejected: letting future agents follow older top-level router docs after deeper send surfaces were fixed. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 150 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 179, and log section. | Alternative rejected: router-level changes without trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; router AB-009/route text audit finds the gate in 4 router files after repairing press-angle route/provenance text; actual corruption pattern audit, backtick path audit, and rationale-order audit clean; CRM stays 100 rows with 0 send fields; asset metadata remains 13 rows, 3 planned agency candidates, and 13 blocked send gates; press/curator trackers remain 30/20 rows. | Alternative rejected: marking router gates done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no spend, press send, creator contract, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 153

- [x] Broad calendars and brand plan bound to AB-009/route gates | DOD: outreach calendar, 90-day calendar, master plan, and brand bible now treat batch volumes as ceilings and require AB-009/KPI decision-read fields plus route/provenance custody before gameplay/pressure/route-risk proof can drive outreach, Steam movement, press/community scaling, paid tests, or public brand claims. | Alternative rejected: letting broad planning docs reopen quota-driven outreach after local send/spend docs were fixed. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 151 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 180, and log section. | Alternative rejected: untracked calendar-level gate repair. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; calendar AB-009/route text audit clean; actual corruption pattern audit, backtick path audit, and rationale-order audit clean through Decision 180; CRM stays 100 rows with 0 send fields; asset metadata remains 13 rows, 3 planned agency candidates, and 13 blocked send gates; press/curator trackers remain 30/20 rows. | Alternative rejected: marking calendar gates done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no outreach, public post, Steam movement, paid spend, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 154

- [x] Access routes bound to AB-009/route gates | DOD: key/access compliance, legal disclosure, playtester recruitment, demo telemetry, and Campaign 03 demo outreach now require AB-009/KPI decision-read fields plus route/provenance custody before gameplay/pressure/route-risk proof can be used in key sends, access pitches, tester recruitment, demo outreach, or reused feedback claims. | Alternative rejected: allowing private access routes to bypass public/creator proof gates. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 152 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 181, and log section. | Alternative rejected: untracked access-route gate repair. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; access-route AB-009/provenance text audit clean; actual corruption pattern audit, backtick path audit, and rationale-order audit clean through Decision 181; CRM stays 100 rows with 0 send fields; asset metadata remains 13 rows, 3 planned agency candidates, and 13 blocked send gates. | Alternative rejected: marking access-route gates done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no key send, access invite, tester recruitment, demo outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 155

- [x] Access proof rules propagated to entry/KPI/risk | DOD: control tower, README, prep directions, KPI dashboard, and risk register now expose that key/private-preview/Steam-Playtest/tester/demo outreach copy cannot use gameplay/pressure/route-risk proof without AB-009/KPI field source, route class, reply-provenance, and access logs where relevant. | Alternative rejected: leaving the new gate only in deeper access docs. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 153 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 182, and log section. | Alternative rejected: untracked entry/KPI/risk propagation. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; entry/KPI/risk access gate text audit clean; actual corruption pattern audit, backtick path audit, and rationale-order audit clean through Decision 182; RISK-053 present; CRM stays 100 rows; asset metadata remains 13 rows. | Alternative rejected: marking entry/KPI/risk propagation done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no key send, access invite, tester recruitment, demo outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 156

- [x] Loose CTA wording routed through activation gate | DOD: AgentOps, Reddit/community rules, experiment briefs, localization, prep directions, and capsule/trailer brief now require Official CTA Link Activation Gate V0 or no-link/private-access fallback before wishlist, Steam, demo, signup, presskit, regional, or trailer/capsule CTA use. | Alternative rejected: leaving weaker "page exists" / "CTA if allowed" shorthand in pasteable operational docs. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 154 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 183, and log section. | Alternative rejected: untracked CTA language repair. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; CTA loose-language text audit clean; actual corruption pattern audit, backtick path audit, and rationale-order audit clean through Decision 183; CRM stays 100 rows; asset metadata remains 13 rows. | Alternative rejected: marking CTA repair done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no post, CTA, signup, Steam movement, paid spend, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 157

- [x] Regional mojibake repaired | DOD: RU/CIS pitch text in Campaign 05 and regional outreach plan is now encoding-clean Russian instead of mojibake. | Alternative rejected: leaving broken draft text behind a review warning. | Estimate: 0us runtime impact.
- [x] Regional send gate aligned with AB-009/CTA/provenance | DOD: campaign, regional plan, lead list, and localization QA now require native/fluent review, AB-009/KPI decision-read field source, Official CTA Link Activation Gate V0 or no-link/private-access fallback, route class, access log where private, and reply-provenance custody before regional send or lead expansion. | Alternative rejected: treating regional outreach as a separate low-friction path after English gates. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 155 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 184, and log section. | Alternative rejected: regional gate repair without audit trail. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; regional mojibake audit clean; regional proof/CTA/provenance text audit clean; stale regional quota/link grep clean; diff whitespace check clean apart from existing CRLF normalization warnings; rationale-order audit clean through Decision 184; CRM stays 100 rows with 0 filled send fields; asset metadata remains 13 rows, 3 planned agency candidates, and 13 blocked send gates. | Alternative rejected: marking regional repair complete before validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no regional outreach, public post, CTA, Steam movement, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 158

- [x] Official-link shorthand collapsed into CTA activation | DOD: analytics, Campaign 01/02, Steam checklist, master plan, control tower, budget, social, launch, risk, backlog, and source ledger no longer use "official link exists" or "one Steam link" as the public-link gate; they route through Official CTA Link Activation Gate V0, Official CTA/contact preflight, or private access logs. | Alternative rejected: leaving old shorthand because the CTA gate exists elsewhere. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 156 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 185, and log section. | Alternative rejected: untracked official-link repair. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; official-link shorthand grep clean; diff whitespace check clean apart from existing CRLF normalization warnings; rationale-order audit clean through Decision 185; CRM stays 100 rows; asset metadata remains 13 rows, 3 planned agency candidates, and 13 blocked send gates. | Alternative rejected: marking official-link repair complete before validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no public link, Steam movement, post, paid spend, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 159

- [x] Page/link existence route-openers collapsed | DOD: spend, Next Fest, post bank, website, creator, CRM schema, pitch bank, press, partnership, daily loop, QA, wishlist, owned-audience, FAQ, support/forum, calendar, risk, backlog, and source-ledger surfaces now require Official CTA Link Activation Gate V0 or private access route custody instead of treating page/link existence as enough. | Alternative rejected: treating page existence as sufficient route permission. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 157 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 186, and log section. | Alternative rejected: untracked route-opener sweep. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; stale route-opener phrase audit clean; diff whitespace check clean apart from existing CRLF normalization warnings; rationale-order audit clean through Decision 186; CRM stays 100 rows; asset metadata remains 13 rows, 3 planned agency candidates, and 13 blocked send gates. | Alternative rejected: marking route-opener sweep complete before validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no public link, Steam movement, post, paid spend, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 160

- [x] KPI reply provenance field aligned | DOD: KPI access reporting now uses `reply_consent_provenance`, matching the creator CRM, press tracker, and curator tracker schemas. | Alternative rejected: keeping `reply_provenance` as a dashboard-only alias. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 158 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 187, and log section. | Alternative rejected: untracked schema-field drift repair. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; markdown table audit clean; `reply_provenance` alias audit clean; schema field audit confirms `reply_consent_provenance` in CRM 100 rows, press 30 rows, and curator 20 rows; rationale-order audit clean through Decision 187. | Alternative rejected: marking field alignment complete before validation. | Estimate: 0us runtime impact.
- Verification status: docs-only so far; no dashboard row, CRM row, tracker row, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 161

- [x] Route provenance shorthand replaced with exact fields | DOD: operating docs now name `send_route_class` for creator/press/curator sends, `access_route_class` for private key/demo/playtest/preview routes, and `reply_consent_provenance` before replies are reused outside their original route. | Alternative rejected: leaving "route/provenance" or "reply-provenance" prose that can push operators into notes-only logging. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 159 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, and Decision 188. | Alternative rejected: untracked wording migration across execution surfaces. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; exact shorthand audit for `reply-provenance`, `reply provenance`, `route/provenance`, and `reply_provenance` clean; markdown table pipe audit clean for touched table files; CRM 100 rows with 0 filled route/reply fields; press tracker 30 rows and curator tracker 20 rows with 0 filled route/reply fields; asset metadata 13 rows with 3 `AGENCY_PROOF_CANDIDATE` and 13 blocked send gates; mojibake audit clean; rationale order clean through Decision 188. | Alternative rejected: marking field wording correction complete from grep-only proof. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no CRM row, press row, curator row, key send, access invite, public post, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-19 Active Marketing Work Addendum 162

- [x] Public-comment consent boundary made exact | DOD: public post, community, social, KPI, analytics, entry, daily, playtest, and preview-access surfaces now separate `consent_provenance = public_comment` from `reply_consent_provenance` used by creator/press/curator/access replies. | Alternative rejected: leaving "consent/provenance" prose that can turn public replies into cross-route consent. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 160 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, and Decision 189. | Alternative rejected: untracked consent wording migration across paste/report surfaces. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; shorthand audit for `consent/provenance`, `Reply provenance`, `reply-provenance`, `route/provenance`, `reply_provenance`, and bare social `public_comment` wording clean; markdown table pipe audit clean; CRM 100 rows with 0 filled route/reply fields; press tracker 30 rows and curator tracker 20 rows with 0 filled route/reply fields; asset metadata 13 rows with 3 `AGENCY_PROOF_CANDIDATE` and 13 blocked send gates; rationale order clean through Decision 189. | Alternative rejected: marking public-comment consent separation complete without CSV/table/schema checks. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no post, signup, CRM import, key send, access invite, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 163

- [x] Live CRM status transitions bound to proof fields | DOD: KPI dashboard, raw queue README, mass lead workflow, and CRM schema now reject raw queue state leakage and require structured send/reply/coverage fields before `CONTACTED`, `REPLIED`, or `COVERED`. | Alternative rejected: notes-only status transitions or adding absent `VERIFIED_NOT_CONTACTED` live CRM state. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 161 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 190, and log section. | Alternative rejected: status-gate repair without owner-local trace. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; CSV parse OK for 9 files; live CRM status distribution unchanged; send/reply/coverage fields remain empty across all 100 CRM rows; forbidden positive `VERIFIED_NOT_CONTACTED` promotion audit clean; markdown table pipe audit clean for touched files; rationale order clean through Decision 190. | Alternative rejected: marking CRM status proof done without schema/data checks. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no CRM row promotion, outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 164

- [x] Live CRM mojibake removed | DOD: seven live CRM rows were normalized to ASCII-safe send-prep text while preserving row count, statuses, contact evidence, and route/send/reply/coverage emptiness. | Alternative rejected: editing raw archive dumps or leaving corrupted text in the operating CRM. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 162 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 191, and log section. | Alternative rejected: silent CRM data hygiene edits. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: live CRM mojibake audit clean; CSV parse OK for all 9 marketing CSVs; Marketing file count 100; CRM remains 100 rows with `VERIFY_BEFORE_CONTACT=23`, `NEEDS_ASSET=22`, `LOW_PRIORITY_VERIFY_LATER=52`, `DO_NOT_CONTACT=3`; send/reply/coverage proof fields remain empty across all 100 CRM rows; rationale order clean through Decision 191. | Alternative rejected: marking CRM encoding repair done without parse/status/send-field checks. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; raw archive CSVs remain source dumps; no CRM row promotion, outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 165

- [x] Key/access log schema bound to exact proof fields | DOD: preview/key protocol and key compliance tables now require `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and `agency_decision_field_source` where proof claims are used. | Alternative rejected: generic `contact_route`, `reply_status`, or notes-only access proof. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 163 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 192, and log section. | Alternative rejected: silent access-log schema correction. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; key/access schema grep confirms exact fields; old key-log `contact_route,purpose` and table `verified_contact` / `reply_status` shorthand absent; touched table pipe audit clean; touched-file mojibake audit clean; all 9 marketing CSVs parse; rationale order clean through Decision 192. | Alternative rejected: marking access schema repair done without text/table/CSV checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no key/access row creation, key send, outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 166

- [x] Exact private access-log field set propagated | DOD: demo/playtest, demo outreach, regional campaign/plan, playtester screening, control tower, and risk register now name `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and `agency_decision_field_source` where private access proof is involved. | Alternative rejected: leaving execution gates on "access log" or route-class shorthand. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added row 164 to `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`, source ledger addendum, Decision 193, and log section. | Alternative rejected: propagation without audit trail. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: stale private-access shorthand grep clean for the touched surfaces; markdown table pipe audit clean; touched-file mojibake audit clean; all 9 marketing CSVs parse; Marketing file count 100; CRM status and send fields unchanged; asset metadata still 13 rows with 3 agency candidates and 13 blocked creator send gates; rationale order clean through Decision 193. | Alternative rejected: marking propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no access row creation, key/playtest/demo invite, outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 167

- [x] SN2 same-day Steam API refresh recorded | DOD: monitoring file now has V4 public Steam review/appdetails snapshot: 72,071 all-language reviews and 42,920 English reviews, both `Very Positive`, with recent negative term-hit samples labeled directional only. | Alternative rejected: letting V3 from 2026-05-19 drive capture priorities on 2026-05-20. | Estimate: 0us runtime impact.
- [x] Risk/backlog/source trace updated | DOD: RISK-046 now references V4 instead of V3; backlog row 165, source ledger addendum, Decision 194, and log section record the refresh. | Alternative rejected: untracked volatile competitor signal update. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: table/CSV/mojibake/rationale-order checks were run after patching; marketing file count stayed at 100; no CRM/asset/send data changed. | Alternative rejected: marking SN2 refresh done without static validation. | Estimate: 0us runtime impact.
- Verification status: docs/web-evidence only; no public comparison copy, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 168

- [x] Asset pain freshness fields added | DOD: `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` now has `pain_freshness_source` and `pain_freshness_checked_at`; all 13 planned rows are `PENDING_SAME_DAY_REFRESH` / `PENDING_CAPTURE` until real capture QA fills source/date proof. | Alternative rejected: notes-only freshness proof. | Estimate: 0us runtime impact.
- [x] Execution gates propagated | DOD: asset ops, QA, shotlist, Campaign 01, KPI, control tower, creator segment gating, and monitoring now require source/date freshness fields when pain proof influences first-pack priority or creator send prep. | Alternative rejected: leaving the field only in CSV where operators would miss it. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 166, source ledger addendum, Decision 195, and log section. | Alternative rejected: schema change without owner-local trace. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no asset promotion, public copy, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 169

- [x] Creator-facing pain freshness gates propagated | DOD: first human-send workflow, post-bank creator warmup, pitch bank, A-tier drafts, and priority-50 drafts now require `pain_freshness_source` and `pain_freshness_checked_at` for pain-backed angles before any send or micro-feedback. | Alternative rejected: leaving freshness only in asset metadata. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 167, source ledger addendum, Decision 196, and log section. | Alternative rejected: untracked send-surface gate propagation. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: all 9 marketing CSVs parse, Marketing file count remains 100, touched table pipe audit clean, touched-file mojibake audit clean, CRM send/reply/coverage fields remain empty, asset metadata remains 13 rows with 13 pending freshness rows, and rationale order is clean through Decision 196. | Alternative rejected: marking send-surface propagation done without data and table checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 170

- [x] Direct-competitor pitch seed neutralized | DOD: live CRM, priority-50 drafts, priority-250 sheet, verification batch assignment files, and the LetsPlayIndex scraper template no longer carry the paste-risk `Your channel has already touched Subnautica/underwater survival` seed or `May compare directly to Subnautica` risk line. | Alternative rejected: leaving draft text because it was gated elsewhere. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 168, source ledger addendum, Decision 197, and log section. | Alternative rejected: silent operating-data rewrite. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: old direct-competitor pitch seed audit clean, all 9 marketing CSVs parse, Marketing file count remains 100, touched table pipe audit clean, touched-file mojibake audit clean, CRM status split unchanged, and CRM send/reply/coverage fields remain empty. | Alternative rejected: marking operating-data rewrite done without CSV and paste-risk checks. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; raw archive/source CSVs remain untouched; no outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 171

- [x] Pasteable competitor wording tightened | DOD: three live CRM personalized openers, one German priority-50 draft line, repeated priority-50 body copy, and the pitch-bank archetype subject now use neutral audience-fit wording instead of direct competitor wording. | Alternative rejected: removing source/evidence lists or leaving pasteable copy because gates exist elsewhere. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 169, source ledger addendum, Decision 198, and log section. | Alternative rejected: silent final-copy cleanup. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: live CRM personalized openers have 0 direct competitor hits; pasteable competitor-copy audit clean; all 9 marketing CSVs parse; Marketing file count remains 100; touched table pipe audit clean; mojibake audit clean; rationale order clean through Decision 198. | Alternative rejected: marking pasteable copy cleanup done without direct text audit. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; source/evidence lists remain intact; no outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 172

- [x] Account/browser custody preflight locked | DOD: social playbook now records `Account Registration Preflight Verdict V0` as `HOLD_ACCOUNT_CREATION`; entry/site/risk docs state that personal browser sessions, cookies, remembered passwords, and chat permission are not custody proof. | Alternative rejected: using user chat permission as enough to create official surfaces. | Estimate: 0us runtime impact.
- [x] Source/backlog/risk trace updated | DOD: added backlog row 170, source ledger addendum, RISK-054, Decision 199, and log section. | Alternative rejected: leaving the browser/account boundary only in chat history. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; account-preflight text audit confirms `HOLD_ACCOUNT_CREATION` plus personal browser/session/chat-permission boundary in entry/risk docs; touched markdown table-pipe audit clean; mojibake audit clean; rationale order clean through Decision 199. | Alternative rejected: marking custody gate tightened without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no private browser profile, cookie/session state, login, account creation, public post, outreach, credential storage, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 173

- [x] First capture packet aligned to V4 | DOD: shotlist first-session note now references 2026-05-20 V4 instead of 2026-05-19 V3, and `PLAN-SHOT-006` wording uses current agency/defensive-choice language. | Alternative rejected: leaving stale V3 language in the call sheet because monitoring already has V4. | Estimate: 0us runtime impact.
- [x] Campaign 01 social/target wording tightened | DOD: Campaign 01 target-audience label is competitor-neutral and social custody now explicitly blocks while account registration preflight is `HOLD_ACCOUNT_CREATION`. | Alternative rejected: relying on social playbook only. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 171, source ledger addendum, Decision 200, and log section. | Alternative rejected: untracked capture gate drift repair. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: V3 execution-drift grep clean for shotlist/Campaign 01; Marketing file count 100; all 9 marketing CSVs parse; touched markdown table-pipe audit clean; mojibake audit clean; rationale order clean through Decision 200. | Alternative rejected: marking V4 alignment done without static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no capture, public post, account action, outreach, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 174

- [x] RU/CIS regional mojibake repaired again | DOD: Campaign 05 and Regional Outreach Plan RU/CIS subject/body/ask drafts now use ASCII-safe transliteration and remain review-pending. | Alternative rejected: leaving mojibake in pasteable regional send surfaces or inserting unreviewed Cyrillic. | Estimate: 0us runtime impact.
- [x] Competitor-killer wording removed from RU/CIS draft | DOD: the repaired draft says the project is not a co-op promise or competitor-comparison pitch without naming a competitor in the body. | Alternative rejected: keeping a direct `Subnautica killer` denial in pasteable outreach. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 172, source ledger addendum, Decision 201, and log section. | Alternative rejected: silent regional copy repair. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: non-raw marketing mojibake audit clean; Marketing file count 100; all 9 marketing CSVs parse; touched markdown table-pipe audit clean; transliteration proof text present; rationale order clean through Decision 201. | Alternative rejected: marking regional copy repair done without full non-raw scan. | Estimate: 0us runtime impact.
- Verification status: docs-only; raw public lead/source CSVs untouched; no regional outreach, public post, account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 175

- [x] Competitor labels neutralized in execution surfaces | DOD: post-bank rules/kill checks, community post policy, segment pitch matrix rows, and Campaign 05 regional kill rule now use neutral competitor or adjacent-underwater-survival wording where the text is not an explicit FAQ trigger or source-evidence field. | Alternative rejected: deleting all competitor mentions, including valid FAQ/source/risk contexts. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 173, source ledger addendum, Decision 202, and log section. | Alternative rejected: untracked paste-surface wording repair. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: execution-surface competitor-label grep clean for the repaired strings; Marketing file count 100; all 9 marketing CSVs parse; touched markdown table-pipe audit clean; mojibake audit clean; rationale order clean through Decision 202. | Alternative rejected: marking paste-surface wording repair done without grep/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no outreach, public post, account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 176

- [x] Press/curator status boundary defined | DOD: press/curator owner doc now states tracker `status` values are triage labels, not send permission, and `READY_FOR_HUMAN_REVIEW_AFTER_*` still requires artifacts, same-day route check, inbox custody, `send_route_class`, reply-provenance, and agency-proof fields where relevant. | Alternative rejected: renaming tracker statuses in CSV without a route owner rule. | Estimate: 0us runtime impact.
- [x] Control tower propagated | DOD: control tower Press/curators row now exposes the same triage-only status boundary. | Alternative rejected: hiding this only in the press lane. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 174, source ledger addendum, Decision 203, and log section. | Alternative rejected: untracked tracker-status clarification. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: press-status boundary grep confirms owner doc and control tower carry triage-only wording; Marketing file count 100; all 9 marketing CSVs parse; touched markdown table-pipe audit clean; mojibake audit clean; rationale order clean through Decision 203. | Alternative rejected: marking tracker-status boundary done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no press send, curator offer, account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 177

- [x] Press/curator machine send gates added | DOD: `PRESS_TARGET_VERIFICATION_TRACKER.csv` and `STEAM_CURATOR_CANDIDATE_TRACKER.csv` now include `send_permission_gate` immediately after triage `status`; all 30 press rows are `BLOCKED_*`, 19 curator rows are `BLOCKED_*`, and the competitor curator row is `DO_NOT_CONTACT_COMPETITOR`. | Alternative rejected: relying on narrative `status` or prose-only owner-doc warnings. | Estimate: 0us runtime impact.
- [x] Owner/risk/control docs propagated | DOD: press/curator owner doc, control tower, RISK-055, backlog row 175, and source ledger now define `ALLOW_PRESS_SEND_VERIFIED` / `ALLOW_CURATOR_SEND_VERIFIED` as the only future send-permission values. | Alternative rejected: filling `send_route_class` before a send exists. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: press rows 30, curator rows 20; `send_permission_gate` bad-gate count 0; press/curator `send_route_class` and `reply_consent_provenance` still empty; all 9 marketing CSVs parse; Marketing file count 100; touched markdown table-pipe audit clean; non-raw mojibake audit clean; git diff check reports line-ending warnings only. | Alternative rejected: marking schema gate done without data and static checks. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no press send, curator offer, key issue, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 178

- [x] Showcase submission machine gate added | DOD: `SHOWCASE_SUBMISSION_TRACKER.csv` now includes `submission_permission_gate` after triage `status`; all 8 rows are blocked: 4 `BLOCKED_MONITOR_ONLY`, 4 `BLOCKED_NOT_READY`. | Alternative rejected: treating `MONITOR` / `NOT_READY` as enough to prevent automated submission. | Estimate: 0us runtime impact.
- [x] Owner/risk/control docs propagated | DOD: showcase playbook, control tower, RISK-056, backlog row 176, and source ledger define `ALLOW_SHOWCASE_SUBMIT_VERIFIED` as the only future submission permission value. | Alternative rejected: playbook prose without a tracker field. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: showcase rows 8; `submission_permission_gate` bad-gate count 0; all 9 marketing CSVs parse; touched markdown table-pipe audit clean; gate grep confirms owner/control/risk/backlog/source propagation. | Alternative rejected: marking event gate done without data/schema checks. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no showcase submission, public event claim, fee spend, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 179

- [x] Press/curator permission gates propagated to key/access docs | DOD: key compliance, Curator Connect playbook, review-key/preview-access protocol, ACC-002/ACC-003 batch rows, and press angle checklist now require `send_permission_gate` allow values before press/curator access or sends. | Alternative rejected: leaving the new gate only in the tracker owner doc. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 177, source ledger addendum, Decision 206, and log section. | Alternative rejected: silent propagation. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: allow-value grep confirms propagation in key/access and Curator Connect docs; all 9 marketing CSVs parse; touched markdown table-pipe audit clean; non-raw mojibake audit clean. | Alternative rejected: marking access propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no key/access row, Curator Connect offer, press send, curator send, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 180

- [x] Entry docs expose machine gates | DOD: README hard rules/directory map, PREP_DIRECTIONS_NOW forbidden list, and DAILY_AGENT_TASK_LOOP noon kill check now require `send_permission_gate` and `submission_permission_gate` instead of inferring permission from tracker `status`, `MONITOR`, or `NOT_READY`. | Alternative rejected: relying on control tower or owner docs only. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 178, source ledger addendum, Decision 207, and log section. | Alternative rejected: silent entry-doc propagation. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: gate grep confirms README/PREP/daily-loop propagation; all 9 marketing CSVs parse; touched markdown table-pipe audit clean; non-raw mojibake audit clean; Marketing file count remains 100. | Alternative rejected: marking first-read propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no press send, curator send, showcase submission, public event claim, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 181

- [x] Current-state labels refreshed | DOD: control tower current operating state, budget current spend ladder, budget current recommendation, and daily-loop current cut now use 2026-05-20 labels. | Alternative rejected: rewriting historical evidence addenda or leaving active entry headings stale. | Estimate: 0us runtime impact.
- [x] Source/backlog trace updated | DOD: added backlog row 179, source ledger addendum, Decision 208, and log section. | Alternative rejected: silent active-heading cleanup. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: grep confirms active 2026-05-20 labels and no stale active-current labels in the touched entry docs; all 9 marketing CSVs parse; touched markdown table-pipe audit clean. | Alternative rejected: marking date refresh done without checking current labels. | Estimate: 0us runtime impact.
- Verification status: docs-only; no spend, public post, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 182

- [x] Paid microtest machine spend gate added | DOD: paid microtest execution plan now has `spend_permission_gate` in the PMT table; all 4 current PMT rows remain `BLOCKED_*`, and only `ALLOW_PAID_MICROTEST_VERIFIED` can permit future paid ad spend. | Alternative rejected: treating PMT ID, budget tier, or platform candidate as enough permission. | Estimate: 0us runtime impact.
- [x] Entry/risk/budget propagation completed | DOD: budget ladder, control tower, README, prep directions, daily loop, RISK-057, backlog row 180, source ledger addendum, Decision 209, and log section all name the same `spend_permission_gate` boundary. | Alternative rejected: leaving the new field only in the paid ads doc. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; 4/4 PMT rows are blocked; gate grep confirms `spend_permission_gate`, `ALLOW_PAID_MICROTEST_VERIFIED`, RISK-057, and row 180 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; CRM rows/status/send fields unchanged; asset rows/gates unchanged. | Alternative rejected: marking spend-gate propagation done without table/data/static checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no spend, ad launch, public post, outreach, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 183

- [x] Paid creator recipient-level gate added | DOD: live creator CRM now includes `paid_creator_permission_gate` after `creator_utility_score`; all 100 current rows are `BLOCKED_NO_PAID_CREATOR_PROOF`, and 0 rows are `ALLOW_PAID_CREATOR_TEST_VERIFIED`. | Alternative rejected: reusing `creator_send_gate` or rate-card reply as payment approval. | Estimate: 0us runtime impact.
- [x] Paid creator consumer docs propagated | DOD: CRM schema, mass verification workflow, segment matrix, rate card, budget ladder, legal/compliance, key compliance, control tower, README, prep directions, daily loop, RISK-058, backlog row 181, source ledger addendum, Decision 210, and log section all name `ALLOW_PAID_CREATOR_TEST_VERIFIED` as the only paid creator allow value. | Alternative rejected: creating another tracker instead of using the recipient-owned CRM row. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: CRM rows 100; `paid_creator_permission_gate` header present; 100/100 rows blocked; 0 allow rows; all 9 marketing CSVs parse; gate grep confirms propagation; touched markdown table-pipe audit clean. | Alternative rejected: marking schema propagation done without CSV/header/table checks. | Estimate: 0us runtime impact.
- Verification status: docs/data-only; no paid creator deal, key/access row, outreach, public post, browser/account action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 184

- [x] Official inbox machine custody gate added | DOD: one-page/presskit owner doc now defines `official_inbox_custody_gate = HOLD_NO_PROJECT_INBOX_CUSTODY`, and only `ALLOW_OFFICIAL_INBOX_USE_VERIFIED` can permit future inbox use. | Alternative rejected: accepting address text, browser state, chat permission, or partial custody checklist as proof. | Estimate: 0us runtime impact.
- [x] Inbox-dependent consumer docs propagated | DOD: social setup, key/access, legal/compliance, control tower, README, prep directions, daily loop, RISK-059, backlog row 182, source ledger addendum, Decision 211, and log section all require the same allow value before account registration, public contact, presskit, creator/key/support, or paid routes. | Alternative rejected: leaving the gate only in the website owner doc. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `official_inbox_custody_gate`, `ALLOW_OFFICIAL_INBOX_USE_VERIFIED`, `HOLD_NO_PROJECT_INBOX_CUSTODY`, RISK-059, and row 182 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; CRM paid creator gate remains 100 blocked and 0 allowed. | Alternative rejected: marking custody-gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no login, account registration, public contact, key/access row, spend, browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 185

- [x] Social account registration permission gate added | DOD: social playbook now defines `account_registration_permission_gate = HOLD_ACCOUNT_CREATION`, and only `ALLOW_ACCOUNT_REGISTRATION_VERIFIED` can permit future account registration. | Alternative rejected: treating candidate handle availability, preflight prose, browser state, or chat permission as account creation approval. | Estimate: 0us runtime impact.
- [x] Account-gate consumer docs propagated | DOD: Campaign 01 social custody, control tower, README, prep directions, daily loop, RISK-060, backlog row 183, source ledger addendum, Decision 212, and log section all require the same allow value before account registration or handle reservation. | Alternative rejected: leaving the gate only inside the social playbook. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `account_registration_permission_gate`, `ALLOW_ACCOUNT_REGISTRATION_VERIFIED`, `HOLD_ACCOUNT_CREATION`, RISK-060, and row 183 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 212. | Alternative rejected: marking account-gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no login, account registration, public contact, follow, DM, post, browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 186

- [x] Public CTA machine permission gate added | DOD: analytics owner doc now defines `public_cta_permission_gate = HOLD_NO_PUBLIC_CTA`, and only destination-specific `ALLOW_PUBLIC_CTA_VERIFIED` can permit future public links. | Alternative rejected: treating page existence, placeholder text, candidate handles, private access routes, or generic CTA prose as link permission. | Estimate: 0us runtime impact.
- [x] Public CTA consumer docs propagated | DOD: control tower, README, prep directions, daily loop, RISK-061, backlog row 184, source ledger addendum, Decision 213, and log section all require the same allow value before wishlist, signup, Discord, presskit, creator-access, paid-traffic, trailer end-card, bio, email, or showcase links. | Alternative rejected: sweeping every historical CTA mention instead of tightening the owner gate and entry surfaces. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `public_cta_permission_gate`, `ALLOW_PUBLIC_CTA_VERIFIED`, `HOLD_NO_PUBLIC_CTA`, RISK-061, and row 184 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 213. | Alternative rejected: marking CTA-gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no public CTA, post, signup, spend, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 187

- [x] Private access machine permission gate added | DOD: review-key/preview-access owner doc now defines `private_access_permission_gate = HOLD_NO_PRIVATE_ACCESS`, and only recipient/batch-specific `ALLOW_PRIVATE_ACCESS_VERIFIED` can permit future demo/key/playtest/preview/Curator Connect access. | Alternative rejected: treating build existence, recipient fit, route prose, or access-log schema as access approval. | Estimate: 0us runtime impact.
- [x] Private access consumer docs propagated | DOD: key compliance, Steam demo/playtest telemetry, Campaign 03, control tower, README, prep directions, daily loop, RISK-062, backlog row 185, source ledger addendum, Decision 214, and log section all require the same allow value before access distribution. | Alternative rejected: reusing public CTA gates for private routes. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `private_access_permission_gate`, `ALLOW_PRIVATE_ACCESS_VERIFIED`, `HOLD_NO_PRIVATE_ACCESS`, RISK-062, and row 185 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 214. | Alternative rejected: marking private-access gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no key, private access, Playtest invite, Curator Connect copy, public CTA, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 188

- [x] Public post machine permission gate added | DOD: social owner doc now defines `public_post_permission_gate = HOLD_NO_PUBLIC_POST`, and only post-specific `ALLOW_PUBLIC_POST_VERIFIED` can permit future no-link or linked public posts. | Alternative rejected: treating draft existence, account existence, asset QA score, no-link route class, or CTA state as post permission. | Estimate: 0us runtime impact.
- [x] Public post consumer docs propagated | DOD: post bank, asset QA checklist, Campaign 01, control tower, README, prep directions, daily loop, RISK-063, backlog row 186, source ledger addendum, Decision 215, and log section all require the same allow value before posting. | Alternative rejected: reusing public CTA gates for no-link posts. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `public_post_permission_gate`, `ALLOW_PUBLIC_POST_VERIFIED`, `HOLD_NO_PUBLIC_POST`, RISK-063, and row 186 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 215. | Alternative rejected: marking public-post gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no public post, public CTA, account/browser action, outreach, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 189

- [x] Owned audience machine permission gate added | DOD: owned-audience owner doc now defines `owned_audience_permission_gate = HOLD_NO_OWNED_AUDIENCE`, and only mode-specific `ALLOW_OWNED_AUDIENCE_VERIFIED` can permit future signup forms, imports, list emails, or signup signal. | Alternative rejected: treating form/provider existence, public CTA, imported contacts, or vague newsletter consent as list permission. | Estimate: 0us runtime impact.
- [x] Owned audience consumer docs propagated | DOD: playtester recruitment, control tower, README, prep directions, daily loop, RISK-064, backlog row 187, source ledger addendum, Decision 216, and log section all require the same allow value before collection/send/reporting. | Alternative rejected: leaving consent separation only as prose in the owner doc. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `owned_audience_permission_gate`, `ALLOW_OWNED_AUDIENCE_VERIFIED`, `HOLD_NO_OWNED_AUDIENCE`, RISK-064, and row 187 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 216. | Alternative rejected: marking owned-audience gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no signup form, list import, email send, account/browser action, public post, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 190

- [x] Discord public-open machine permission gate added | DOD: Discord owner doc now defines `discord_open_permission_gate = HOLD_NO_DISCORD_PUBLIC_OPEN`, and only server-specific `ALLOW_DISCORD_OPEN_VERIFIED` can permit future public server opening, invite publication, announcement, member-count signal, demo-support routing, creator/press room, or regional server use. | Alternative rejected: treating server draft, invite URL, channel template, moderator willingness, community interest, public CTA, or post draft as server-open permission. | Estimate: 0us runtime impact.
- [x] Discord gate consumer docs propagated | DOD: control tower, README, prep directions, daily loop, RISK-014/RISK-065, backlog row 188, source ledger addendum, Decision 217, and log section all require the same allow value before public Discord use. | Alternative rejected: leaving the gate only in the Discord owner doc. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `discord_open_permission_gate`, `ALLOW_DISCORD_OPEN_VERIFIED`, `HOLD_NO_DISCORD_PUBLIC_OPEN`, RISK-065, and row 188 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 217. | Alternative rejected: marking Discord gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no Discord server, invite, public CTA, public post, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 191

- [x] Steam support/forum machine permission gate added | DOD: Steam reviews/forums/support owner doc now defines `steam_support_permission_gate = HOLD_NO_STEAM_SUPPORT_PUBLIC_ROUTE`, and only surface-specific `ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED` can permit future pinned Steam forum threads, support links, official review/forum replies, or support-signal reporting. | Alternative rejected: treating Steam page existence, demo existence, known-issues drafts, public CTA, Discord, or an angry thread as support-route permission. | Estimate: 0us runtime impact.
- [x] Steam support gate consumer docs propagated | DOD: launch war-room, demo/playtest checklist, control tower, README, prep directions, daily loop, RISK-018/RISK-032/RISK-066, backlog row 189, source ledger addendum, Decision 218, and log section all require the same allow value before public Steam support/forum/review use. | Alternative rejected: leaving the gate only in the Steam support owner doc. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `steam_support_permission_gate`, `ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED`, `HOLD_NO_STEAM_SUPPORT_PUBLIC_ROUTE`, RISK-066, and row 189 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 218. | Alternative rejected: marking Steam support gate propagation done without text/table/data/source checks. | Estimate: 0us runtime impact.
- Verification status: docs/source-only; official Steamworks review/event/community moderation docs rechecked; no Steam forum thread, support link, review/forum reply, public CTA, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 192

- [x] Steam announcement/news machine permission gate added | DOD: Devlog and Steam News owner doc now defines `steam_announcement_permission_gate = HOLD_NO_STEAM_ANNOUNCEMENT`, and only post-specific `ALLOW_STEAM_ANNOUNCEMENT_VERIFIED` can permit future Steam announcements/news/events. | Alternative rejected: treating devlog draft, Steam page existence, demo existence, public post approval, CTA approval, or event template as Steamworks publication permission. | Estimate: 0us runtime impact.
- [x] Steam announcement gate consumer docs propagated | DOD: Campaign 02, Campaign 04, demo/playtest checklist, launch war-room, control tower, README, prep directions, daily loop, RISK-067, backlog row 190, source ledger addendum, Decision 219, and log section all require the same allow value before Steam event/news publication. | Alternative rejected: relying on `public_post_permission_gate` alone. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `steam_announcement_permission_gate`, `ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`, `HOLD_NO_STEAM_ANNOUNCEMENT`, RISK-067, and row 190 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 219. | Alternative rejected: marking Steam announcement gate propagation done without text/table/data/source checks. | Estimate: 0us runtime impact.
- Verification status: docs/source-only; official Steamworks Events/Announcements source used from the support pass; no Steam announcement, news post, event, support route, public CTA, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 193

- [x] Localization public-use machine permission gate added | DOD: Localization owner doc now defines `localization_public_permission_gate = HOLD_LOCALIZED_PUBLIC_USE`, and only language/surface-specific `ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` can permit future localized/regional public copy. | Alternative rejected: treating encoding repair, owner-native familiarity, draft translation, raw regional leads, or regional interest as localization approval. | Estimate: 0us runtime impact.
- [x] Localization gate consumer docs propagated | DOD: regional outreach, regional creator leads, Campaign 05, control tower, README, prep directions, daily loop, RISK-038/RISK-068, backlog row 191, source ledger addendum, Decision 220, and log section all require the same allow value before localized/regional public use. | Alternative rejected: leaving the gate only inside the localization owner doc. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `localization_public_permission_gate`, `ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED`, `HOLD_LOCALIZED_PUBLIC_USE`, RISK-068, and row 191 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 220. | Alternative rejected: marking localization gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no localized send, regional outreach, public post, public CTA, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 194

- [x] Press release/public presskit publication machine gate added | DOD: press release/templates owner doc now defines `press_release_permission_gate = HOLD_NO_PRESS_RELEASE_PUBLICATION`, and only surface-specific `ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` can permit future public press releases, public presskit announcements, media one-pagers, email release copy, wire copy, embargo notes, social/blog release copy, or Steam-news reuse. | Alternative rejected: treating templates, presskit drafts, Steam page existence, CTA approval, public-post approval, press tracker status, or send permission as release approval. | Estimate: 0us runtime impact.
- [x] Release gate consumer docs propagated | DOD: presskit, website, Campaign 02, launch war-room, control tower, README, prep directions, daily loop, RISK-012/RISK-041/RISK-045/RISK-069, backlog row 192, source ledger addendum, Decision 221, and log section all require the same allow value before public release/presskit publication use. | Alternative rejected: leaving the gate only inside the template owner doc. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `press_release_permission_gate`, `ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`, `HOLD_NO_PRESS_RELEASE_PUBLICATION`, RISK-069, row 192, and Decision 221 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 221. | Alternative rejected: marking release gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no press release, presskit publication, press send, Steam news reuse, wire copy, public post, CTA, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 195

- [x] Steam page publication machine gate added | DOD: Steam page asset/checklist owner doc now defines `steam_page_publish_permission_gate = HOLD_NO_STEAM_PAGE_PUBLICATION`, and only app/page-specific `ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` can permit future public Coming Soon/store page publication, visibility changes, public demo/store surfaces, wishlist campaign claims, or page-live reporting. | Alternative rejected: treating asset existence, page draft, Steamworks app shell, candidate URL, CTA planning, announcement approval, press release approval, or wishlist readiness as page-publication approval. | Estimate: 0us runtime impact.
- [x] Steam page gate consumer docs propagated | DOD: store copy matrix, wishlist/Next Fest, wishlist conversion, Campaign 02, Campaign 04, demo/playtest, launch war-room, analytics, control tower, README, prep directions, daily loop, RISK-045/RISK-070, backlog row 193, source ledger addendum, Decision 222, and log section all require the same allow value before public page/store-surface use. | Alternative rejected: leaving the gate only inside the asset checklist. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `steam_page_publish_permission_gate`, `ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, `HOLD_NO_STEAM_PAGE_PUBLICATION`, RISK-070, row 193, and Decision 222 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 222. | Alternative rejected: marking Steam page publication gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no Steam page publication, visibility change, public demo/store surface, wishlist campaign, CTA, announcement, press release, public post, spend, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 196

- [x] Public demo/Steam Playtest access machine gate added | DOD: demo/playtest owner doc now defines `demo_public_access_permission_gate = HOLD_NO_PUBLIC_DEMO_ACCESS`, and only surface-specific `ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED` can permit future public Steam demo, public demo button, Next Fest demo availability, public Steam Playtest signup/tranche, demo-live claims, or public demo feedback routes. | Alternative rejected: treating build launch, Steam page publication, CTA approval, private access approval, known-issues draft, feedback form, announcement draft, or first-route-playable prose as public demo approval. | Estimate: 0us runtime impact.
- [x] Public demo gate consumer docs propagated | DOD: Campaign 03, Campaign 04, playtester recruitment, launch war-room, Steam support/forums, control tower, README, prep directions, daily loop, RISK-008/RISK-071, backlog row 194, source ledger addendum, Decision 223, and log section all require the same allow value before public demo/Playtest access. | Alternative rejected: leaving the gate only inside the demo owner doc. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `demo_public_access_permission_gate`, `ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`, `HOLD_NO_PUBLIC_DEMO_ACCESS`, RISK-071, row 194, and Decision 223 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 223. | Alternative rejected: marking public demo/Playtest gate propagation done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no public demo, Steam Playtest signup/tranche, Next Fest demo, demo-live claim, public feedback route, private access, CTA, announcement, press release, public post, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 197

- [x] Steam Next Fest commitment bound to existing tracker gate | DOD: Steam wishlist/Next Fest plan and Campaign 04 now require `SHOW-001` in `Press/SHOWCASE_SUBMISSION_TRACKER.csv` to carry `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED` before registration, commitment, participation claims, or event-beat reservation. | Alternative rejected: creating a duplicate Next Fest gate instead of using the owner-local showcase tracker row. | Estimate: 0us runtime impact.
- [x] Next Fest gate consumer docs propagated | DOD: showcase playbook, control tower, README, prep directions, daily loop, RISK-056/top-risk 27, backlog row 195, source ledger addendum, Decision 224, and log section all state that page readiness, demo readiness, CTA readiness, announcement approval, or Campaign 04 prose cannot replace the `SHOW-001` row gate. | Alternative rejected: relying on generic showcase language while Campaign 04 remained a bypass path. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; gate grep confirms `SHOW-001`, `submission_permission_gate = BLOCKED_NOT_READY`, `ALLOW_SHOWCASE_SUBMIT_VERIFIED`, row 195, and Decision 224 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 224. | Alternative rejected: marking Next Fest commitment binding done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs/CSV-only; no Next Fest registration, commitment, participation claim, event-beat reservation, public demo, CTA, announcement, submission, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 198

- [x] SN2 V5 currentness refreshed from official-platform surfaces | DOD: monitoring owner doc now records V5 Steam review API, Steam appdetails API, and public store page display counts from 2026-05-20; SN2 remains `Very Positive`, and Korean `Mixed` is labeled as a regional watch item only. | Alternative rejected: using stale V4 counts or treating one regional split as global collapse. | Estimate: 0us runtime impact.
- [x] Capture/risk/asset-intake docs bound to V5 without public comparison copy | DOD: RISK-046, first capture call sheet, `PLAN-SHOT-006` pain modifier, asset freshness-source example, backlog row 196, source ledger addendum, Decision 225, and log section all point to V5 while keeping SN2 pain private-only. | Alternative rejected: promoting planned asset rows or public copy without real HECTON capture proof. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; V5 grep confirms official counts, backlog row 196, Decision 225, and Addendum 198 propagation; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 225. | Alternative rejected: marking currentness refresh done without text/table/data checks. | Estimate: 0us runtime impact.
- Verification status: docs/source-only; no capture, asset promotion, public comparison copy, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 199

- [x] Creator pain-backed send rules bound to SN2 V5 currentness | DOD: mass outreach workflow now labels SN2 pain fit rules as V5-current, states that the official Steam API/page read remains competitor-positive, and blocks "players are angry" framing. | Alternative rejected: leaving the open-tab owner doc on 2026-05-19 wording. | Estimate: 0us runtime impact.
- [x] First human-send packet freshness source made concrete | DOD: pain-backed creator packets must name the current monitoring refresh, for example `Monitoring SN2 Steam API/Page Refresh V5`, plus the exact private bucket in `pain_freshness_source`; backlog row 197, source ledger addendum, Decision 226, and log section record the propagation. | Alternative rejected: filling CRM or asset metadata fields before a real asset/send exists. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; grep confirms V5 creator-send binding, row 197, Decision 226, and Addendum 199; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 226. | Alternative rejected: marking creator-send currentness done without table/data/static checks. | Estimate: 0us runtime impact.
- Verification status: docs/source-only; no creator send, CRM send-log fill, asset promotion, public comparison copy, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 200

- [x] Priority-50 stale SN2-current wording corrected | DOD: priority draft microbatch now treats 2026-05-19 RSS rows as dated recorded signals, not current-send proof. | Alternative rejected: leaving "currently covering SN2" in a paste-adjacent human-send file. | Estimate: 0us runtime impact.
- [x] Same-day route/currentness requirement added to the draft rows | DOD: each SN2-active row now requires same-day channel/contact-route recheck before send, and hard rules inherit `Monitoring SN2 Steam API/Page Refresh V5` pain-freshness source requirements. | Alternative rejected: filling live CRM rows without same-day verification. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; grep confirms row 198, Decision 227, Addendum 200, V5-gated heading, and no `Currently covering SN2` hits in the priority-50 file; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 227. | Alternative rejected: marking stale wording fixed without static checks. | Estimate: 0us runtime impact.
- Verification status: docs/source-only; no creator send, CRM send-log fill, asset promotion, public comparison copy, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 201

- [x] Raw lead expansion parked behind asset-gap proof | DOD: raw lead expansion queue now states current hold and resumes only after first capture/demo proves a segment gap the live CRM cannot cover. | Alternative rejected: continuing toward 300 rows while no creator-send-ready asset exists. | Estimate: 0us runtime impact.
- [x] Creator outreach database expansion target gated | DOD: "add Subnautica 2 launch streamers" is no longer current work; it requires direct-underwater-survival asset-gap proof plus same-day currentness/contact-route verification plan. | Alternative rejected: deleting useful future sourcing notes. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; grep confirms row 199, Decision 228, Addendum 201, `Current hold`, and `Parked Expansion Targets`; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 228. | Alternative rejected: marking expansion hold done without static/data checks. | Estimate: 0us runtime impact.
- Verification status: docs/source-only; no lead expansion, CRM send-log fill, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 202

- [x] Live CRM hot SN2-current wording converted to dated evidence | DOD: six hot microbatch rows now preserve 2026-05-18/19 RSS evidence and require current-channel recheck before send instead of saying `Currently covering SN2`. | Alternative rejected: leaving stale current-language in live CRM. | Estimate: 0us runtime impact.
- [x] CRM state left blocked | DOD: statuses, `paid_creator_permission_gate`, creator send fields, send-log fields, route fields, and asset fields remain blocked/empty; no row was promoted. | Alternative rejected: filling send fields without human route verification and asset proof. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; grep confirms no `Currently covering SN2`, no `Hot current`, row 200, Decision 229, and Addendum 202; CRM status/send-log counts unchanged; non-raw mojibake audit clean; rationale order clean through Decision 229. | Alternative rejected: marking CRM wording fixed without CSV/static checks. | Estimate: 0us runtime impact.
- Verification status: CSV/docs-only; no status promotion, send-log fill, asset promotion, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 203

- [x] Entry/raw lead docs aligned with asset-gated hold | DOD: README and raw leads README no longer present raw scaling or weekly top-250 verification as default current work. | Alternative rejected: deleting useful future raw source documentation. | Estimate: 0us runtime impact.
- [x] Campaign 00 lead-volume KPI made historical/parked | DOD: Campaign 00 now treats Top 250 verification batches as historical parked data and blocks new lead sprints until first assets prove a CRM segment gap. | Alternative rejected: leaving quota-looking KPI under pre-screenshot setup. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; grep confirms row 201, Decision 230, Addendum 203, parked raw lead wording, and no default `Verify only the top 250 per week`; touched markdown table-pipe audit clean; non-raw mojibake audit clean; rationale order clean through Decision 230. | Alternative rejected: marking entry alignment done without static/data checks. | Estimate: 0us runtime impact.
- Verification status: docs-only; no lead expansion, CRM promotion, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 204

- [x] Residual stale-current wording closed | DOD: `SEGMENT_PITCH_MATRIX.md`, `PRIORITY_50_MESSAGE_DRAFTS_FROM_RAW.md`, backlog/source trace, and live AgentOps raw-batch sheets no longer expose the exact stale SN2-current tokens caught by the targeted grep. | Alternative rejected: leaving false-positive audit noise around paste-adjacent creator docs. | Estimate: 0us runtime impact.
- [x] Raw batch sprint gate tightened | DOD: all ten `VerificationBatches_2026-05-19` sheets and the raw expansion queue now require first asset proof plus a segment gap the live CRM cannot cover before raw-lead sprint use. | Alternative rejected: allowing a vague "audience gap" to reopen verification volume. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; 15 touched markdown files pass table audit; touched mojibake audit clean; stale-bypass grep clean; CRM status split and send-log fields unchanged with 100 paid gates still blocked. | Alternative rejected: marking wording cleanup done without table/data/CRM-state proof. | Estimate: 0us runtime impact.
- Verification status: docs-only; no lead expansion, CRM promotion, send-log fill, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 205

- [x] Paid creator scenario bypass wording closed | DOD: budget scenarios, experiment spend order, demo outreach, brand budget reality, and backlog P3 table now require `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED` on the selected CRM row before paid creator spend. | Alternative rejected: relying on the owner gate while old scenario prose still said organic fit or demo stability was enough. | Estimate: 0us runtime impact.
- [x] Paid bypass grep clean | DOD: targeted search no longer finds the old strings that permitted paid creator tests from organic fit/replies, demo stability, strong retention, or generic paid-slot table wording alone. | Alternative rejected: manual spot-check without a reusable static audit pattern. | Estimate: 0us runtime impact.
- Verification status: docs-only; no payment, paid brief, key/access row, CRM promotion, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 206

- [x] Key/private-access shorthand bypass closed | DOD: A-tier pitch gates, outreach calendar, creator database, CRM scoring, legal, presskit key policy, review-key/access protocol, and Campaign 03 now require recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, access-log fields, and disclosure before key/private-preview/access copy. | Alternative rejected: allowing `key policy ready`, `tracking`, `verified recipient`, `small batch`, or `QA route` wording to substitute for machine permission. | Estimate: 0us runtime impact.
- [x] Key bypass grep clean | DOD: targeted search no longer finds the old shorthand strings for key/access readiness. | Alternative rejected: relying on scattered hard rules without checking paste-adjacent campaign/creator/press surfaces. | Estimate: 0us runtime impact.
- Verification status: docs-only; no key, private access, Playtest invite, Curator Connect copy, CRM promotion, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 207

- [x] Residual private-access/key-policy shorthand closed | DOD: website/signature, agent workflow, brand bible, mass outreach, segment matrix, pitch bank, Priority-250 sheet, Curator Connect playbook, press-angle bank, launch war-room, regional campaign, regional outreach, partnership terms, roadmap Promise Lint, prep directions, presskit plan, backlog, and source ledger now route public links through Official CTA Link Activation Gate V0 and private routes through recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, disclosure, and exact access-log fields. | Alternative rejected: leaving "private access log", "key/access log ready", or "key policy" as operator shorthand in paste-adjacent surfaces. | Estimate: 0us runtime impact.
- [x] Priority-250 mojibake touched-row cleanup | DOD: normalized the touched `DaddelBaerTV` display string to ASCII in `CreatorOutreach/PRIORITY_250_PITCH_SHEET_FROM_RAW.md`; no live CRM row was changed. | Alternative rejected: leaving a known encoding hit in a file already touched for route-gate cleanup. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; touched markdown table audit clean across 16 files; touched mojibake audit clean across 18 files; targeted access-shorthand bypass grep clean; CRM status split unchanged with 0 filled send fields and 100 paid creator gates still blocked. | Alternative rejected: claiming wording cleanup from manual spot check only. | Estimate: 0us runtime impact.
- Verification status: docs/static-data only; no key, private access, public CTA, public post, CRM promotion, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 208

- [x] Page-live/account-exists shorthand closed | DOD: budget spend, segment timing, social account field kit, first-post/pinned-post/first-10-post rules, post-bank Steam Page Live bundle, no-embargo template, backlog row 206, and source ledger now require surface-specific machine gates instead of treating Steam page live, account exists, Steam URL, or no-embargo language as permission. | Alternative rejected: leaving "live" and "exists" as operator shorthand because deeper gate docs already exist. | Estimate: 0us runtime impact.
- [x] Page/account shorthand audit clean | DOD: targeted search no longer finds the old exact strings for `Press kit/website polish | 200 | Steam page live`, account-field paste after account exists, official-Steam-URL-only pinned post, ungated Steam page live announcement, ungated press beat, old no-embargo copy, old Steam Coming Soon required-asset line, or the ungated Segment Matrix Steam page row. | Alternative rejected: broad Steam/link grep that would mix safe policy references with real bypass strings. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; touched markdown table audit clean across 6 files; touched mojibake audit clean across 7 files; CRM status split unchanged with 0 filled send fields and 100 paid creator gates still blocked. | Alternative rejected: recording the cleanup without data/table/static proof. | Estimate: 0us runtime impact.
- Verification status: docs/static-data only; no spend, post, public CTA, Steam page action, account/browser action, private access, creator/press send, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 209

- [x] Active current-state labels refreshed | DOD: `Data/MARKETING_BACKLOG_INDEX.md`, `MARKETING_CONTROL_TOWER.md`, and `Operations/DAILY_AGENT_TASK_LOOP.md` now use 2026-05-20/V5 labels for the current execution cut, external reality update, and active daily loop. | Alternative rejected: leaving top-level current labels on 2026-05-19 while deeper docs already reference V5. | Estimate: 0us runtime impact.
- [x] Active current-label audit clean | DOD: targeted search finds no `Status date: 2026-05-19`, `2026-05-19 External Reality Update`, or `2026-05-19 Active Control Tower Loop V0` in the active entry/loop files; historical source ledger lines remain dated to their evidence event. | Alternative rejected: rewriting historical source addenda as if their evidence dates changed. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; touched markdown table audit clean across 3 files; touched mojibake audit clean across 4 files. | Alternative rejected: current-label changes without table/data/static checks. | Estimate: 0us runtime impact.
- Verification status: docs/static-data only; no public copy, outreach, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 210

- [x] Residual approved-link/showcase/CTA shorthand closed | DOD: presskit email skeleton, A-tier pitch skeleton, pitch bank long email, showcase public/private route boundary, showcase target table, measurement Steam-page beat row, KPI unknown-route rules, Steam trailer beat sheets, asset QA clip/trailer CTA rules, and prep short-form pattern now name exact machine gates instead of `[approved link only]`, generic approved CTA, `Demo ready`, or store-page readiness. | Alternative rejected: relying on deeper gate owner docs while paste-adjacent templates still held weaker placeholders. | Estimate: 0us runtime impact.
- [x] Targeted CTA/showcase shorthand audit clean | DOD: targeted search finds no remaining `[approved link only]`, `[approved access route only]`, old generic approved Steam/demo CTA lines, `Demo ready, unreleased Steam game`, or `Store page and eligible tags/demo` strings under `Docs/Marketing`. | Alternative rejected: broad link grep that would mix safe policy references with real bypass strings. | Estimate: 0us runtime impact.
- [x] Validation cut clean | DOD: Marketing file count 100; all 9 marketing CSVs parse; touched markdown table audit clean across 9 files; touched mojibake audit clean across 9 files; CRM status split unchanged with 0 filled send fields and 100 paid creator gates still blocked. | Alternative rejected: logging the cleanup without table/data/static proof. | Estimate: 0us runtime impact.
- Verification status: docs/static-data only; no public CTA, post, showcase submission, press send, creator send, private access, account/browser action, runtime, or build action occurred.

## 2026-05-20 Active Marketing Work Addendum 211

- [x] Analytics reporting rows gained permission gate/source custody | DOD: campaign event, creator attribution, feedback coding, minimum measurement packets, weekly report template, and measurement rules now require permission gate/source plus non-unknown route/provenance fields before rows count. | Alternative rejected: letting route class and consent fields exist without the machine gate/source that allowed the route. | Estimate: 0us runtime impact.
- [x] KPI dashboard gained creator and quarantine fields | DOD: Creator Outreach dashboard rows now include `asset_ids_sent`, `creator_utility_score`, `creator_send_gate`, `send_route_class`, `reply_consent_provenance`, and `send_gate_source`; `unknown` route/provenance or blank permission source blocks reporting across dashboard rules. | Alternative rejected: counting reply/coverage rates from CRM status and reply text alone. | Estimate: 0us runtime impact.
- [x] Entry/control loop synchronized | DOD: README, control tower, and daily loop now require permission gate/source in reporting language, not only route/provenance fields. | Alternative rejected: leaving entry docs on the older route/consent-only wording. | Estimate: 0us runtime impact.
- Verification status: docs/static-data only; no KPI row fill, public CTA, post, send, private access, account/browser action, runtime, or build action occurred.
