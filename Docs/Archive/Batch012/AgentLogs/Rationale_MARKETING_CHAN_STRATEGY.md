# Rationale_MARKETING_CHAN_STRATEGY

Date: 2026-05-23
Domain: Marketing/community strategy docs
Evidence class: STATIC_DOC + prior WEB_OSINT summary
Runtime impact: none

## Decision 1

Problem: Marketing docs had community and Reddit templates, but no dedicated operating model for 4chan/Dvach-style anonymous imageboards.
Solution: Write imageboard-specific rules into existing community, template, and monitoring owners instead of creating another doc.
Rejected Alternatives: A new `IMAGEBOARD_4CHAN_DVACH_PLAYBOOK.md` would be cleaner but violates the current Marketing README anti-sprawl default and would break the expected 100-file validation count unless separately approved.
Scalability potential: Low/Middle/High/Ultra all use the same public-boundary logic; richer assets only change what can be shown, not the honesty rules.
Hardware Impact: 0 runtime us on i3/MX350; docs-only strategy.

## Decision 2

Problem: Anonymous boards can produce useful harsh critique, but they also punish obvious marketing, fake user posts, weak assets, and AI-slop framing.
Solution: Treat 4chan/Dvach as monitor-first critique surfaces with no-link, no-CTA proof drops only after rule checks and real capture assets.
Rejected Alternatives: Wishlist pushes, Steam links, Discord invites, fake-player seeding, and "Subnautica killer" copy. These create backlash and cannot pass the marketing permission gates.
Scalability potential: Weak device path shows only readable low-cost screenshots/clips; high/ultra path can show stronger visual overkill, but every asset still needs a visible player decision.
Hardware Impact: 0 runtime us on i3/MX350; operational risk reduction only.

## Decision 3

Problem: Chan/Dvach comments are volatile and anonymous; using them as proof creates fake certainty.
Solution: Classify imageboard findings as anecdotal by default, upgraded only by repeated independent signals and source/date/thread capture.
Rejected Alternatives: Adoption percentages for AI agents, market-share claims, and public copy based on one thread.
Scalability potential: Monitoring cadence scales by operator time, not runtime cost; same taxonomy supports pre-screenshot through demo phases.
Hardware Impact: 0 runtime us on i3/MX350; docs-only strategy.

## Decision 4

Problem: Marketing Backtick Path Audit failed on two pre-existing references to a missing SHINOBU_81 rationale path.
Solution: Remove code-path formatting from the source-ledger prose reference and harden the rationale-order audit command with a Test-Path guard that returns a not-applicable result instead of encouraging placeholder file creation.
Rejected Alternatives: Creating an empty Rationale_SHINOBU_81.md would satisfy a path checker while corrupting the evidence trail; leaving the audit red would make this docs-only change report weaker.
Scalability potential: Validation now remains usable across future SHINOBU-numbered agents without requiring stale placeholder files.
Hardware Impact: 0 runtime us on i3/MX350; docs-only validation hygiene.

## Decision 5

Problem: Imageboard strategy in community docs alone does not stop future misuse in FAQ replies, crisis handling, feedback triage, post hooks, or risk decisions.
Solution: Propagate the same no-link, no-CTA, evidence-labeled imageboard route into FAQ, crisis, feedback, content, risk, and backlog docs.
Rejected Alternatives: A standalone long playbook that future agents must remember to cross-reference; broad "be careful on chans" prose with no execution hooks.
Scalability potential: Low/Middle/High/Ultra asset quality can vary, but the route gate is invariant: real media, one question, no fake identity, no public CTA, no private access.
Hardware Impact: 0 runtime us on i3/MX350; documentation and operations only.

## Decision 6

Problem: 4chan/Dvach can produce useful critique and also bad-faith pile-ons; without a triage ladder the team may overreact or over-report.
Solution: Add imageboard-specific feedback classes, upgrade rules, severity overrides, crisis scripts, and 24-hour keep/revise/kill decisions.
Rejected Alternatives: Counting hostile comments as votes, treating likes/replies as interest, or ignoring all chan critique as useless.
Scalability potential: The same triage route works before screenshots, after first clips, and after demo; confidence upgrades require independent sources.
Hardware Impact: 0 runtime us on i3/MX350; docs-only risk reduction.

## Decision 7

Problem: Generic post hooks are too broad for anonymous boards and can read as shilling.
Solution: Add imageboard-specific hook banks, asset-pairing rules, and kill rules that force one surface, one asset, one critique question.
Rejected Alternatives: Reusing Reddit/Steam/X hooks on chans; asking "do you like this?" instead of testing readability and player decision.
Scalability potential: Low-tier captures can test nouns and decisions; high/ultra captures can add visual overkill but still must answer the same critique question.
Hardware Impact: 0 runtime us on i3/MX350; no runtime path touched.

## Decision 8

Problem: Imageboard templates still needed an asset-level preflight so weak captures cannot be posted just because copy looks safe.
Solution: Add Imageboard Readiness Scorecard, hard blockers, rejection codes, and a preflight card to the asset QA checklist.
Rejected Alternatives: Reusing Reddit QA, accepting "no-link" as enough safety, or approving posts without board fit, media proof, decision read, and anti-shill wording.
Scalability potential: Low-tier captures must prove nouns/decision with cheap visuals; high/ultra captures may be visually richer but still require the same 10/12 imageboard readiness minimum.
Hardware Impact: 0 runtime us on i3/MX350; docs-only route control.

## Decision 9

Problem: Campaign 01 had no explicit place for an optional 4chan/Dvach critique lane, so future agents could treat chan posting as either forbidden or a normal launch beat.
Solution: Add optional lane that can only revise/kill/hold/support, never create Campaign `KEEP` by itself.
Rejected Alternatives: Treating imageboard reactions as positive campaign proof, or using them to rescue assets that failed blind cold-read.
Scalability potential: Works from first screenshot through first clip; confidence grows only through independent confirmation, not volume in one thread.
Hardware Impact: 0 runtime us on i3/MX350; docs-only campaign control.

## Decision 10

Problem: Anonymous/no-account surfaces could be mistaken for a loophole around public post permission, route class, KPI, and asset-library custody.
Solution: Add anonymous-surface post addendum, KPI Imageboard Feedback table, daily Imageboard Scout loop, asset-library route codes, and backlog row 248.
Rejected Alternatives: Recording chan comments in generic community feedback, importing anonymous users into CRM, or allowing no-account posts without approval record.
Scalability potential: Same route handles monitoring-only, one-asset critique, and post-demo thread analysis while blocking CTA/access misuse.
Hardware Impact: 0 runtime us on i3/MX350; no runtime or build impact.

## Decision 11

Problem: Safe imageboard behavior is not only a board-choice problem; the exact first prompt can convert a useful critique route into a shill, AI-process, engine-war, or access-bait thread.
Solution: Add AB-010 as a pre-post prompt safety test with required fields for shill read, likely derail, asset-specific answer, decision read, context need, and stop condition.
Rejected Alternatives: Reusing Reddit critique prompts, asking broad taste questions, or approving a post because the media asset passed QA while the copy still smelled promotional.
Scalability potential: Low/Middle/High/Ultra asset fidelity changes the media quality, not the copy route; every tier still needs one asset, one question, no CTA, and no fake discovery.
Hardware Impact: 0 runtime us on i3/MX350; docs-only route-risk reduction.

## Decision 12

Problem: The shotlist and creative briefs defined good assets, but they did not yet encode how hostile anonymous readers will attack generic diver frames, AI-looking surfaces, over-fogged darkness, clean sci-fi corridors, passive monsters, or thumbnail-like ad compositions.
Solution: Add asset-by-asset imageboard candidate mapping, imageboard capture notes, visual anti-patterns, corrective cues, and thumbnail/clip stress prompts.
Rejected Alternatives: Waiting until after a failed public thread, using final capsule/key art as proof, or treating imageboard approval as a positive validation metric.
Scalability potential: Low tier must prove nouns and player decision with cheap, readable visuals; middle/high/ultra can add richer fog, material wear, and visual overkill, but the hostile-read test still demands one visible player choice.
Hardware Impact: 0 runtime us on i3/MX350; no runtime, render, or build path touched.
