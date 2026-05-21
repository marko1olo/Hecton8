# HECTON-8 Daily Agent Marketing Task Loop

Status: executable agent workflow
Owner lane: SHINOBU_81 / agent operations
Runtime impact: none

## Purpose

Agents are useful only if they turn public noise into verified rows, better copy, better asset decisions, and fewer bad guesses. This loop prevents "research theatre".

## Daily Roles

| Role | Output |
|---|---|
| Lead Verifier | Existing CRM rows rechecked only when asset proof creates a send need; raw-lead checks require an explicit source-backed sprint. |
| Pitch Writer | Asset-linked opener drafts tied to approved `PLAN-*` evidence and Promise Lint. |
| Community Scout | 5 communities checked for rules/fit. |
| Sentiment Miner | 20 relevant comments classified. |
| Asset Critic | 10 screenshots/clips scored. |
| Source Auditor | 5 source claims rechecked. |
| KPI Clerk | Dashboard rows updated with route-specific class, permission gate/source, `consent_provenance` / `reply_consent_provenance`, and agency-decision fields where feedback, forms, links, support routes, or cold reads are involved. |

One agent can hold multiple roles, but one output must exist per role before claiming the day was useful.

## Start-Of-Day Checklist

1. Read `Docs/Marketing/README.md`.
2. Read `Docs/Marketing/NO_COOP_PUBLIC_POSITIONING.md`.
3. Read current task-specific doc.
4. Check source ledger for stale platform claims.
5. Pick one measurable output.
6. Write output path before starting.

## 2026-05-21 Current Cut

The staged CRM-100 queue has 0 raw rows. Until the first real screenshot/clip packet exists, do not default to more lead verification. The bottleneck is asset proof, not lead volume.

SN2 currentness: V6 on 2026-05-21 is the active same-day freshness row. Any SN2-derived pain bucket used for asset priority must name V6 or a newer same-day monitoring row in `pain_freshness_source`, fill `pain_freshness_checked_at`, and still require `viewer_named_decision` plus valid `capture_verdict` before it can affect Campaign 01, creator sends, Steam movement, spend, or public routes.

Current default lane order:

1. `ASSET_GATE` if any capture exists or can be prepared; this includes the first-capture handoff packet, file paths, build ID, reject codes, `creator_rows_unlocked`, `creator_utility_score`, `creator_send_gate`, pain freshness fields, `public_comparison_gate`, `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, and `capture_next_actions` when the asset could touch creators or first-public surfaces.
2. `COPY_TEST` only if tied to one planned asset ID.
3. `SOURCE_RECHECK` only for platform/source facts that block a concrete gate.
4. `RISK_CLOSE` only if the risk register has no prevention/response.
5. `CRM_CLEANUP` only if Wave A needs exact official contact recheck after matching assets exist and send-log fields can be filled from proof.

Do not expand raw leads unless a human explicitly asks for another source-backed lead sprint.

## 2026-05-21 Active Control Tower Loop V2

This loop prevents agent labor from becoming more documents. Use it until the first real screenshot pack exists.

### Morning Cut

Pick exactly one lane for the day:

| Lane | When to pick | Required output |
|---|---|---|
| CRM_CLEANUP | Creator/press rows are stale or raw, or Wave A has proof to log. | Updated CSV rows with status, route, risk, next action, and send-log fields if any send is being prepared. |
| ASSET_GATE | Screenshots/clips exist or are about to exist. | First-capture handoff packet, QA scores, file paths, build ID, reject codes, asset metadata updates, creator utility/send gate fields, pain freshness, public comparison gate, agency proof fields, handoff packet ID, verdict, viewer-named decision, and next actions when relevant. |
| COPY_TEST | Asset/copy mismatch blocks public use. | 3-5 variants tied to one asset ID and one metric. |
| SOURCE_RECHECK | Platform rules, routes, or deadlines can change. | Source ledger addendum and affected doc correction. |
| RISK_CLOSE | A risk has no prevention/response owner. | Risk register update plus one backlog action. |
| CAMPAIGN_DECISION | Campaign 01/02/03 is being prepared. | `KEEP`, `REVISE`, or `KILL` decision fields filled. |

If a proposed task cannot produce one of these outputs, reject it.

### Evidence Gate

Each output must label evidence:

| Evidence | Allowed claim |
|---|---|
| INTERNAL_DOC | Project intent only. |
| THIRD_PARTY_INDEX | Prospecting seed only. |
| PUBLIC_CREATOR_PAGE | Fit/activity hint only. |
| OFFICIAL_PLATFORM_DOC | Platform rule as of check date. |
| ASSET_METADATA | Capture status and recorded asset-side gates only; quality still requires QA evidence. |
| HUMAN_COLD_READ | Clarity signal only. |
| STEAM_ANALYTICS | Funnel signal only after `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` and Steam destination custody pass. |

Do not write "verified" unless the route, date, source, and allowed claim are explicit.

### Noon Kill Check

At the halfway point, stop and answer:

```text
Does this change update a row, asset, risk, campaign decision, or source gate?
If no, stop.
Is this creating a new file?
If yes, stop unless the backlog explicitly names a missing file.
Does it imply unsupported multiplayer-scope, performance, or large-world proof?
If yes, rewrite.
Does creator-facing work lack `creator_utility_score`, `creator_send_gate`, named CRM row, or `asset_ids_sent`?
If yes, hold the send path.
Does feedback, tester, support, signup, or link work lack route-specific class, permission gate/source, plus `consent_provenance` or `reply_consent_provenance`?
If yes, hold the route and fix the source row before reporting signal.
Does a cold-read, first-public, creator, or campaign decision row use gameplay/pressure/route-risk proof without `what_decision_next`, `agency_decision_read`, `agency_decision_read_comments`, or `cold_read_agency_decision` as applicable?
If yes, hold the report and fix the dashboard row before claiming agency proof.
Does any press, community, showcase, demo, curator, or wishlist surface use threat/anomaly/mood proof without a readable player decision?
If yes, hold the surface and revise the asset/copy/prompt before send, submit, publish, or expand.
Does press or curator work infer permission from tracker `status` instead of `send_permission_gate`?
If yes, hold; press requires `ALLOW_PRESS_SEND_VERIFIED` and curators require `ALLOW_CURATOR_SEND_VERIFIED`.
Does showcase, festival, or Steam Next Fest work infer submission/registration/commitment/participation permission from `MONITOR`, `NOT_READY`, page readiness, demo readiness, CTA readiness, announcement approval, or Campaign 04 prose instead of `submission_permission_gate`?
If yes, hold; submissions and Next Fest commitment require `ALLOW_SHOWCASE_SUBMIT_VERIFIED` on the exact tracker row.
Does paid/spend work infer permission from PMT ID, budget tier, platform candidate, or organic-readiness prose instead of `spend_permission_gate`?
If yes, hold; paid microtests require `ALLOW_PAID_MICROTEST_VERIFIED`.
Does paid creator work infer permission from audience fit, rate-card reply, sponsorship policy, organic reply, or creator name instead of `paid_creator_permission_gate`?
If yes, hold; paid creator tests require `ALLOW_PAID_CREATOR_TEST_VERIFIED`.
Does account registration infer permission from candidate handle, browser state, chat permission, or preflight prose instead of `account_registration_permission_gate`?
If yes, hold; registration requires `ALLOW_ACCOUNT_REGISTRATION_VERIFIED`.
Does account, inbox, presskit contact, creator route, key/access, support, or paid route work infer official inbox custody from an address, browser session, or chat permission instead of `official_inbox_custody_gate`?
If yes, hold; inbox-dependent routes require `ALLOW_OFFICIAL_INBOX_USE_VERIFIED`.
Does any Steam Coming Soon/store page, visibility change, public demo/store surface, wishlist campaign claim, or "Steam page is live" work infer publication permission from asset existence, page draft, Steamworks app shell, candidate URL, CTA planning, announcement approval, or press release approval instead of `steam_page_publish_permission_gate`?
If yes, hold; Steam page publication requires app/page-specific `ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`.
Does any public Steam demo, public demo button, Next Fest demo availability, public Steam Playtest signup/tranche, demo-live claim, or public demo feedback route infer permission from build launch, Steam page publication, CTA approval, private access approval, known-issues draft, feedback form, announcement draft, or first-route-playable prose instead of `demo_public_access_permission_gate`?
If yes, hold; public demo/Playtest access requires surface-specific `ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`.
Does any public CTA link infer permission from page existence, placeholder text, candidate handle, private access route, or generic CTA prose instead of `public_cta_permission_gate`?
If yes, hold; public links require destination-specific `ALLOW_PUBLIC_CTA_VERIFIED`.
Does any private demo/key/playtest/preview/Curator Connect route infer permission from build existence, recipient fit, route prose, or access-log schema instead of `private_access_permission_gate`?
If yes, hold; private access requires recipient/batch-specific `ALLOW_PRIVATE_ACCESS_VERIFIED`.
Does any public post infer permission from draft existence, account existence, asset QA score, or no-link route class instead of `public_post_permission_gate`?
If yes, hold; public posting requires post-specific `ALLOW_PUBLIC_POST_VERIFIED`.
Does any signup/list/newsletter work infer permission from form existence, provider existence, public CTA, or imported contacts instead of `owned_audience_permission_gate`?
If yes, hold; owned audience use requires mode-specific `ALLOW_OWNED_AUDIENCE_VERIFIED`.
Does any Discord/server/invite/community-hub work infer permission from server draft, channel template, moderator willingness, community interest, public CTA, or post draft instead of `discord_open_permission_gate`?
If yes, hold; public Discord opening requires server-specific `ALLOW_DISCORD_OPEN_VERIFIED`.
Does any Steam forum/support/review-response work infer permission from Steam page existence, demo existence, known-issues draft, public CTA, Discord, or angry thread instead of `steam_support_permission_gate`?
If yes, hold; Steam support/forums/replies require surface-specific `ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED`.
Does any Steam announcement/news/event work infer permission from devlog draft, Steam page existence, demo existence, public post approval, CTA approval, or event template instead of `steam_announcement_permission_gate`?
If yes, hold; Steam announcements require post-specific `ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`.
Does any press release, public presskit, media one-pager, site presskit block, email release, wire copy, embargo note, social/blog release, or Steam-news reuse infer permission from template, presskit draft, Steam page existence, public CTA approval, public post approval, press tracker status, or send permission instead of `press_release_permission_gate`?
If yes, hold; release surfaces require surface-specific `ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`.
Does any localized/regional copy or outreach work infer permission from encoding repair, owner-native familiarity, draft translation, raw regional leads, or regional interest instead of `localization_public_permission_gate`?
If yes, hold; localized public use requires language/surface-specific `ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED`.
Does first-pack asset work lack the first-capture handoff packet, file path, build ID, `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, or `capture_next_actions`?
If yes, hold Campaign 01 and fix the asset metadata source row.
```

### End Cut

Every day ends with one of:

| Decision | Meaning |
|---|---|
| ADVANCE | The next public/asset/CRM gate can move one step. |
| HOLD | More proof is required; name the proof. |
| KILL | A route, asset, copy angle, or spend path is no longer worth work. |

No decision means the day produced process noise.

## Lead Verification Loop

Input:

- `Data/UNIQUE_CREATOR_VERIFICATION_QUEUE_2026-05-18.csv`
- `Data/PRIORITY_CREATOR_SHORTLIST_FROM_RAW_2026-05-18.csv`
- `AgentOps/VerificationBatches_2026-05-19/VERIFY_BATCH_01.md` as the first existing batch file; other batch files in the same folder follow the same parked raw-sprint rule. Batch `TODO`, checked boxes, contact notes, public-index metrics, `Required asset`, and `Custom opener` fields are scratch values only; they do not promote a row to live CRM or send readiness.

Steps:

1. Open public creator page.
2. Confirm channel still exists.
3. Confirm recent upload/stream activity.
4. Confirm game fit.
5. Confirm language.
6. Find public contact route only from creator-owned source.
7. Check sponsorship/coverage policy if visible.
8. Assign segment and pitch angle.
9. Mark risk.
10. Promote, hold, or reject.

Promotion means live CRM/schema work, not batch scratch completion. Do not copy batch scratch values into `Data/CREATOR_VERIFICATION_TEMPLATE.csv` until current asset metadata gates, creator utility, route class, permission gate, provenance, source-ledger trace, and live CRM fields are ready.

Forbidden:

- guessing email;
- using leaked contact lists;
- scraping private Discords;
- sending a pitch while verifying;
- inventing subscriber counts;
- treating index data as current truth.

## Pitch Writing Loop

Each pitch must include:

- creator-specific opener;
- audience fit reason;
- HECTON-8 hook matched to their content;
- asset/build status;
- honest boundary;
- CTA.

Bad opener:

> I love your channel and think your audience would like our game.

Acceptable opener:

> Your survival runs tend to focus on systems breaking under pressure rather than pure reaction scares, so HECTON-8's base-failure and salvage loop is the fit I would test first.

Never mention:

- "Subnautica killer";
- unsupported multiplayer scope;
- guaranteed FPS;
- massive world size;
- paid placement without disclosure.

## Community Scout Loop

For each community:

1. Record name and URL.
2. Read rules same day.
3. Mark self-promo allowed/limited/banned.
4. Mark media formats allowed.
5. Mark required flair.
6. Mark account age/karma gates if visible.
7. Add recommended post type.
8. Add the exact decision-read question if the post uses a threat, anomaly, darkness, or mood asset.
9. Add "do not post" notes.

Output goes to:

- `Community/REDDIT_COMMUNITY_RULES_TRACKER.md`

## Sentiment Mining Loop

Track current player language around:

- Subnautica 2 multiplayer-scope expectations;
- performance/stutter complaints;
- Early Access trust;
- base-building friction;
- vehicle fantasy;
- underwater horror fatigue;
- resource grind;
- inventory pain;
- "too clean" visual criticism;
- demand for large mobile bases.

Each signal must be classified:

| Class | Meaning |
|---|---|
| Confirmed recurring | Multiple independent signals. |
| Directional | Repeated but weak or context-dependent. |
| Anecdotal | Single post/comment. |
| Marketing echo | Copy repeated from trailer/store page. |
| Reject | Unsourced or unusable. |

## Asset Critic Loop

Use `QA/MARKETING_ASSET_QA_CHECKLIST.md`.

For each asset:

1. Score 0-12.
2. Write one-sentence diagnosis.
3. Identify the missing hook.
4. If the asset uses threat, anomaly, darkness, or mood, state whether the player decision reads without caption.
5. If the asset could touch creators, score creator utility 0-4 and name the CRM rows it unlocks.
6. Decide publish/revise/kill.
7. Store result in asset QA table and asset metadata, including `creator_send_gate`, `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, and `capture_next_actions`.

Agents do not "like" assets. Agents classify whether a cold viewer understands and cares.

## Source Auditor Loop

Recheck:

- Steam tags;
- Steam UTM;
- Steam Next Fest;
- Steam asset specs;
- FTC endorsement guidance;
- YouTube/TikTok disclosure rules;
- subreddit rules before posting;
- public creator page before contact.

If a source changed, update:

- `Data/SOURCE_LEDGER.md`;
- affected operational doc;
- status/rationale if the change changes strategy.

## End-Of-Day Report Template

```text
Date:
Agent:
Role(s):
Files changed:
Leads verified:
Pitches drafted:
Communities checked:
Assets scored:
Source claims rechecked:
Signals found:
Route/consent gaps:
Blocked items:
Next recommended action:
```

## End-Of-Change Validation Cut V1

Run this after any Marketing docs/data change. Do not run `dotnet build` for docs-only marketing work.

### Required Checks

```powershell
(Get-ChildItem -LiteralPath 'C:\hades\Hecton8\Docs\Marketing' -Recurse -File | Measure-Object).Count
```

Expected: `100` unless the control tower explicitly approved a new file.

```powershell
$files = Get-ChildItem -LiteralPath 'C:\hades\Hecton8\Docs\Marketing' -Recurse -Filter '*.csv' -File
$bad = @()
foreach ($f in $files) {
  try { Import-Csv -LiteralPath $f.FullName | Out-Null }
  catch { $bad += "$($f.FullName): $($_.Exception.Message)" }
}
if ($bad.Count) { $bad } else { "CSV_PARSE_OK count=$($files.Count)" }
```

Expected: `CSV_PARSE_OK count=9`.

```powershell
$rows = Import-Csv -LiteralPath 'C:\hades\Hecton8\Docs\Marketing\Data\CREATOR_VERIFICATION_TEMPLATE.csv'
'CRM rows=' + $rows.Count
($rows | Group-Object status | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join '; '
```

Expected while no outreach occurs: `CRM rows=100` and `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.

Creator send-log fields must also remain empty before a real human send:

```powershell
@('outreach_batch','sent_date','contact_route_verified_for_send','asset_ids_sent','creator_utility_score','send_route_class','reply_consent_provenance','reply_status_after_send') | ForEach-Object {
  $c = $_
  $n = ($rows | Where-Object { $_.$c -and $_.$c.Trim() }).Count
  "$c=$n"
}
```

Expected while no outreach occurs: every listed field returns `0`.

```powershell
$rows = Import-Csv -LiteralPath 'C:\hades\Hecton8\Docs\Marketing\Data\MARKETING_ASSET_METADATA_TEMPLATE.csv'
'ASSET rows=' + $rows.Count
'headers=' + (($rows[0].PSObject.Properties.Name) -join ',')
```

Expected before first capture: `ASSET rows=13`, header includes `multiplayer_scope_check`, `performance_claim_check`, `feature_truth_check`, `creator_utility_score`, `creator_send_gate`, `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, and `capture_next_actions`.

```powershell
$patterns = @(
  ('co_' + 'op_comments'),
  ('co_' + 'op_check'),
  ('IMPLIES_' + 'COOP'),
  ('HECT' + 'Oo'),
  ('it' + 'eam'),
  ('icreen' + 'shot'),
  ('o' + 'ext'),
  ('BRA' + 'oD'),
  ('i' + 'OURCE'),
  ('U' + 'iD'),
  ([char]0x00D0),
  ([char]0x00C2),
  ([char]0xFFFD)
)
rg -n ($patterns -join '|') C:\hades\Hecton8\Docs\Marketing
```

Expected: no hits, unless the search is intentionally scoped to archive/history files and explained in the report.

### Backtick Path Audit

Run the path audit when editing entry points, backlog, source ledger, campaign docs, presskit docs, or operation docs. Expected result: `BACKTICK_PATH_AUDIT_OK`.

Do not create placeholder files just to satisfy this audit. If a name is a future packet artifact rather than a repo file, remove code-style formatting and describe it as a packet.

```powershell
$root = 'C:\hades\Hecton8'
$marketing = Join-Path $root 'Docs\Marketing'
$ext = '\.(md|csv|txt|json|toml|h8bin)$'
$missing = New-Object System.Collections.Generic.List[string]
foreach ($f in Get-ChildItem -LiteralPath $marketing -Recurse -Filter '*.md' -File) {
  $inFence = $false
  $lineNo = 0
  foreach ($line in [System.IO.File]::ReadLines($f.FullName)) {
    $lineNo++
    if ($line.TrimStart().StartsWith('```')) { $inFence = -not $inFence; continue }
    if ($inFence) { continue }
    foreach ($m in [regex]::Matches($line, '`([^`]+)`')) {
      $tok = $m.Groups[1].Value.Trim().Trim('.', ',', ';', ':', ')', ']', '}')
      if ($tok -match '^(https?|mailto|steam):') { continue }
      $looksFile = $tok -match $ext -or $tok -match '^[A-Za-z]:[\\/]' -or $tok -match '^(Docs|Hecton8)[\\/]'
      if (-not $looksFile) { continue }
      $candidates = @()
      if ($tok -match '^[A-Za-z]:[\\/]') { $candidates += $tok }
      elseif ($tok -match '^Docs[\\/]') { $candidates += (Join-Path $root $tok) }
      elseif ($tok -match '^Hecton8[\\/]') { $candidates += (Join-Path 'C:\hades' $tok) }
      else {
        $candidates += (Join-Path $f.DirectoryName $tok)
        $candidates += (Join-Path $marketing $tok)
        $candidates += (Join-Path $root $tok)
      }
      $exists = $false
      foreach ($candidate in $candidates) {
        if ($candidate -match '[*?]') {
          if (@(Get-ChildItem -Path $candidate -File -ErrorAction SilentlyContinue).Count -gt 0) { $exists = $true; break }
        } elseif (Test-Path -LiteralPath $candidate) {
          $exists = $true
          break
        }
      }
      if (-not $exists) { $missing.Add(('{0}:{1} -> `{2}`' -f $f.FullName,$lineNo,$tok)) }
    }
  }
}
if ($missing.Count) { $missing } else { 'BACKTICK_PATH_AUDIT_OK' }
```

Expected: `BACKTICK_PATH_AUDIT_OK`.

### Rationale Order Audit

Run this after updating `Docs/AgentLogs/Rationale_SHINOBU_81.md`. Expected result format: `RATIONALE_ORDER_OK last=<decision_id> count=<count>`.

```powershell
$path = 'C:\hades\Hecton8\Docs\AgentLogs\Rationale_SHINOBU_81.md'
$ids = [regex]::Matches((Get-Content -LiteralPath $path -Raw), '^## Decision (\d+)', 'Multiline') | ForEach-Object { [int]$_.Groups[1].Value }
$gaps = @()
for ($i = 1; $i -lt $ids.Count; $i++) {
  if ($ids[$i] -ne ($ids[$i - 1] + 1)) { $gaps += "$($ids[$i - 1])->$($ids[$i])" }
}
if ($gaps.Count) { "RATIONALE_ORDER_FAIL gaps=$($gaps -join ',')" }
elseif ($ids.Count -eq 0) { 'RATIONALE_ORDER_FAIL no decisions found' }
else { "RATIONALE_ORDER_OK last=$($ids[-1]) count=$($ids.Count)" }
```

## Quality Bar

An agent day is rejected if it produces:

- a generic strategy paragraph;
- unverified creator names only;
- fake contacts;
- a copied pitch with no personalization;
- a post template that violates platform rules;
- a claim that cannot be traced to a source or project proof.
- a feedback/contact/link row with missing route-specific class plus `consent_provenance` or `reply_consent_provenance`.
- a cold-read or first-public row that claims agency proof without `what_decision_next`, `agency_decision_read`, `agency_decision_read_comments`, or `cold_read_agency_decision` as applicable.

## Minimum Daily Quota

If no screenshots exist and the CRM has raw rows:

- 25 lead verifications;
- 10 opener drafts;
- 5 community rule checks;
- 20 sentiment classifications;
- 1 source ledger update if a platform source was used.

If no screenshots exist and the CRM-100 staged queue has 0 raw rows:

- 1 planned asset packet or QA gate improvement;
- 1 copy lint or asset-linked copy test;
- 1 source/risk/backlog correction only if it changes execution;
- 0 new generic docs;
- 0 broad creator sends;
- 0 paid actions.

If screenshots exist:

- 10 asset scores;
- 5 copy variants;
- 10 lead verifications;
- creator utility, `creator_send_gate`, `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, and `capture_next_actions` fields for any asset considered for outreach or first-public testing;
- 1 A/B test brief;
- AB-009 dashboard fields if any agency-proof candidate was cold-read;
- 1 public-post candidate.

If `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` passes for the Steam destination:

- daily UTM/source table update;
- 5 creator follow-ups;
- 1 store-copy or screenshot improvement proposal;
- 1 feedback digest.
