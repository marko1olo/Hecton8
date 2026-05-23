# Status_MARKETING_CHAN_STRATEGY

Date: 2026-05-23
Domain: Marketing/community strategy docs
Evidence class: STATIC_DOC + prior WEB_OSINT summary
Runtime impact: none

## Checklist

- [x] Task 1: Read active project rules, marketing index, relevant community/monitoring docs, and evidence/visual-fake mandates. DOD practice: evidence class separation before writing strategy. Alternative rejected: writing generic chan advice without reading existing gates. Microsecond estimate: 0 runtime us.
- [x] Task 2: Add imageboard strategy and hard rules to community target docs. DOD practice: no public post without platform rule check, asset proof, and permission gate. Alternative rejected: new standalone marketing file because Marketing README forbids new docs by default. Microsecond estimate: 0 runtime us.
- [x] Task 3: Add 4chan/Dvach post, reply, and kill-switch templates. DOD practice: no-link critique first, no astroturf, no competitor attack, no AI-as-marketing. Alternative rejected: wishlist/store templates for imageboards. Microsecond estimate: 0 runtime us.
- [x] Task 4: Add imageboard monitoring queries, signal taxonomy, and digest fields. DOD practice: chans are anecdotal sentiment unless independently repeated. Alternative rejected: treating anonymous posts as market percentages. Microsecond estimate: 0 runtime us.
- [x] Task 5: Run docs-only marketing validation and append final report. DOD practice: End-Of-Change Validation Cut V1, no dotnet/Unity build for docs-only change. Alternative rejected: runtime compile/build because no runtime code changed. Microsecond estimate: 0 runtime us.
- [x] Task 6: Deepen imageboard handling into FAQ, crisis, feedback, content, risk, and backlog owners. DOD practice: one route policy replicated at the actual execution points. Alternative rejected: leaving strategy isolated in community docs where future agents can bypass it. Microsecond estimate: 0 runtime us.
- [x] Task 7: Add imageboard objection and RU reply matrices. DOD practice: short factual replies, one clarification, then stop. Alternative rejected: long defensive replies and lore explanations in hostile threads. Microsecond estimate: 0 runtime us.
- [x] Task 8: Add imageboard incident scripts, 30-minute response, 24-hour decisions, and feedback taxonomy. DOD practice: classify anonymous feedback as anecdotal unless independently confirmed. Alternative rejected: treating hostile comments as direct product mandates. Microsecond estimate: 0 runtime us.
- [x] Task 9: Add imageboard hook bank, asset pairing matrix, risks RISK-073 through RISK-078, and backlog row 247. DOD practice: templates tied to asset proof and route gates. Alternative rejected: generic "post on chans" instructions. Microsecond estimate: 0 runtime us.
- [x] Task 10: Rerun docs-only marketing validation after deepening pass. DOD practice: End-Of-Change Validation Cut V1, no runtime build. Alternative rejected: dotnet/Unity build for documentation-only changes. Microsecond estimate: 0 runtime us.
- [x] Task 11: Add imageboard asset QA readiness scorecard and preflight card. DOD practice: asset-specific route approval before anonymous posting. Alternative rejected: using normal Reddit/social QA as sufficient for chans. Microsecond estimate: 0 runtime us.
- [x] Task 12: Add optional Campaign 01 imageboard critique lane. DOD practice: imageboard signal can revise/kill/hold but cannot produce Campaign KEEP by itself. Alternative rejected: using chan response to rescue failed cold-read assets. Microsecond estimate: 0 runtime us.
- [x] Task 13: Add anonymous-surface post permission rules, KPI imageboard table, daily Imageboard Scout loop, asset-library route codes, and backlog row 248. DOD practice: no-account surfaces still require approval, route class, provenance, and stop condition. Alternative rejected: treating anonymity as bypass for public_post_permission_gate. Microsecond estimate: 0 runtime us.
- [x] Task 14: Rerun docs-only validation after execution-preflight pass. DOD practice: End-Of-Change Validation Cut V1 plus git diff check. Alternative rejected: runtime build for docs-only route changes. Microsecond estimate: 0 runtime us.
- [x] Task 15: Add AB-010 imageboard prompt safety experiment and cold-read fields. DOD practice: prompt must be stress-tested for shill smell, asset specificity, and derail risk before a route request. Alternative rejected: copying Reddit/Steam question style into anonymous boards. Microsecond estimate: 0 runtime us.
- [x] Task 16: Add imageboard candidate mapping to screenshot/clip shotlist. DOD practice: every candidate gets one critique question, danger read, and kill cue tied to real `PLAN-*` assets. Alternative rejected: treating all screenshots as equally safe for 4chan/Dvach. Microsecond estimate: 0 runtime us.
- [x] Task 17: Add hostile-read visual anti-patterns and thumbnail/clip stress rules. DOD practice: imageboard critique can revise or kill but cannot validate capsule/thumbnail by itself. Alternative rejected: using final capsule/key art as anonymous-board proof. Microsecond estimate: 0 runtime us.
- [x] Task 18: Rerun docs-only validation after prompt/capture/creative stress pass. DOD practice: End-Of-Change Validation Cut V1 plus scoped git diff check for touched docs. Alternative rejected: runtime build for documentation-only changes; global whitespace cleanup of unrelated dirty Unity/.meta files. Microsecond estimate: 0 runtime us.

## Validation

- Marketing file count: 100.
- CSV parse: CSV_PARSE_OK count=9.
- CRM rows: 100; statuses DO_NOT_CONTACT=3, LOW_PRIORITY_VERIFY_LATER=52, NEEDS_ASSET=22, VERIFY_BEFORE_CONTACT=23.
- Creator send-log fields: all checked fields returned 0.
- Asset metadata rows: 13; required agency/creator/pain fields present.
- Encoding/pattern guard: no hits; rg returned exit 1 with empty output.
- Backtick Path Audit: BACKTICK_PATH_AUDIT_OK after correcting stale SHINOBU_81 code-path references in marketing validation docs.
- Rationale order audit: RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent; Rationale_SHINOBU_81.md does not exist and was not created as a placeholder.
- Git diff check: no whitespace errors; Git reported CRLF conversion warnings only.
- CURRENT_BATCH.md: absent; no XML batch prompt available for this unassigned marketing request.
- Deepening pass validation repeated after FAQ/crisis/feedback/content/risk/backlog edits: file count 100, CSV parse OK count=9, CRM rows/status unchanged, send-log fields all 0, asset rows 13, encoding/pattern guard no hits, Backtick Path Audit OK, rationale-order guard not applicable, git diff check clean except CRLF warnings.
- Execution-preflight pass validation repeated after QA/Campaign/Social/KPI/Operations/Backlog edits: file count 100, CSV parse OK count=9, CRM rows/status unchanged, send-log fields all 0, asset rows 13, encoding/pattern guard no hits, Backtick Path Audit OK, rationale-order guard not applicable, git diff check clean except CRLF warnings.
- Prompt/capture/creative stress pass validation repeated after Experiments/Shotlist/Creative/Backlog edits: file count 100, CSV parse OK count=9, CRM rows/status unchanged, send-log fields all 0, asset rows 13 with required agency/creator/pain fields, encoding/pattern guard no hits, Backtick Path Audit OK, rationale-order guard not applicable, scoped git diff check for touched docs clean except CRLF warnings. Full worktree `git diff --check` is red from unrelated Unity/.meta trailing whitespace already present in dirty files outside this marketing-doc pass; not fixed here.
