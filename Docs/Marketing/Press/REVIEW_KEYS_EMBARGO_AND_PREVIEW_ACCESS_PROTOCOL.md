# HECTON-8 Review Keys, Embargo, And Preview Access Protocol

Status: pre-key protocol / no keys ready
Owner lane: SHINOBU_81 / access control
Runtime impact: none

## Source Boundary

Primary current sources:

- Steam Keys: https://partner.steamgames.com/doc/features/keys
- Steam Curator Connect: https://partner.steamgames.com/doc/marketing/curators
- FTC Endorsement Guides: https://www.ftc.gov/business-guidance/resources/ftcs-endorsement-guides-what-people-are-asking
- YouTube Paid Promotions: https://support.google.com/youtube/answer/10588440?hl=en

Recheck before requesting keys or sending access. Platform limits and disclosure obligations are external rules.

## Access Types

| Access type | Use | HECTON-8 stance |
|---|---|---|
| Steam Curator Connect | Steam curator review copies | Preferred for curators. |
| Release State Override keys | Small press/influencer/beta access before release | Only after key request approval, recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, and strict log. |
| Steam Playtest | Larger public testing if needed | Consider for scale; separate from press keys. |
| Private build link | Emergency press preview only | Avoid unless secure, revocable, and recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`. |
| Default release keys | Post-release review/press access | Later. |
| Dev comp keys | Internal developer access | Never distribute to press/customers. |

## Key Rules

- Never sell pre-release/access keys.
- Never promise keys before Valve approval.
- Never send keys to unverified contacts.
- Never send access from a personal, throwaway, agent-owned, or unrecorded mailbox.
- Never use raw keys for Steam curators if Curator Connect works.
- Never send multiple keys because someone asks casually.
- Never use a private access link as a public CTA, bio link, showcase CTA, or presskit download link.
- Every key/access must have a reason, recipient, source, and status.
- Paid/sponsored coverage must disclose relationship.

## Private Access Permission Gate V0

Machine gate: `private_access_permission_gate = HOLD_NO_PRIVATE_ACCESS`. The only future allow value is `ALLOW_PRIVATE_ACCESS_VERIFIED`, and it is recipient/batch-specific: approving one creator, outlet, curator, tester, or batch does not approve another route.

Allow requires:

- stable build or explicit technical-test build state;
- known-issues copy ready;
- access type chosen;
- recipient or batch owner row exists;
- `verified_contact_route` recorded;
- `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`;
- `access_route_class` recorded as private access, not public CTA;
- `reply_status_after_send` and `reply_consent_provenance` fields ready;
- revocation/disable path known where possible;
- disclosure requirement identified;
- `agency_decision_field_source` recorded when access copy claims gameplay, pressure, route-risk, threat, salvage, base failure, or first-public agency proof.

If any field is missing, keep the access as protocol-only and do not send keys, links, Steam Playtest invites, preview access, or Curator Connect copies.

## Release State Override Boundary

Steamworks documentation currently describes Release State Override keys as intended for small beta tests and press/influencer access, with a general total limit of 2,500. HECTON-8 must treat that number as an upper ceiling, not a target.

Recommended HECTON-8 allocation before launch:

| Purpose | Max before reassessment |
|---|---:|
| Press previews | 30 |
| Send-verified creators | 50 |
| Technical QA/playtest | 50 |
| Emergency replacements | 10 |
| Total first request | 140 or less |

If the team needs hundreds/thousands of players, evaluate Steam Playtest instead.

## Access Approval Flow

1. `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` for the exact recipient or batch.
2. Recipient verified.
3. Fit score recorded.
4. Contact route verified from recipient-owned source.
5. Build route verified by QA.
6. Official project inbox custody verified.
7. Asset metadata claim checks pass for any asset/link in the message.
8. Any gameplay, pressure, route-risk, threat, salvage failure, or first-public proof claim has non-pending asset metadata `viewer_named_decision`, valid non-held `capture_verdict`, and AB-009/KPI decision-read fields (`what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`) unless the access is purely technical QA.
9. Press tracker has `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED` or curator tracker has `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED` when the recipient is from either tracker.
10. Public/private route class chosen and recorded.
11. Creator or press send-log row is ready.
12. Disclosure requirement identified.
13. Access type chosen.
14. Key/access logged.
15. Message sent.
16. Follow-up status tracked.
17. Unused/suspicious access reviewed.

## Verification Requirements

Press:

- publication exists;
- author or editor contact route is public/official;
- recent games coverage exists;
- outlet fits PC/indie/survival/horror;
- no key-resale pattern.

Creator:

- channel exists;
- recent relevant content;
- language and region known;
- public contact route;
- sponsorship policy if paid;
- no impersonator red flags.

Curator:

- use Curator Connect;
- recent review history;
- relevant tags;
- no external-key pressure.

## Embargo Use

Do not use embargoes for weak beats.

Use embargo only if:

- many recipients receive the same preview access;
- trailer/demo/release date is coordinated;
- assets are final enough;
- embargo time is concrete with timezone;
- recipient can realistically honor it;
- one person owns communication.

## Embargo Template

```text
Embargo:
You may publish coverage of HECTON-8 after [DATE] [TIME] [TIMEZONE].

Allowed before embargo:
- private questions by email;
- internal capture/prep;
- no public screenshots, clips, streams, posts, or reviews.

Allowed after embargo:
- gameplay footage from the provided build;
- screenshots from the provided asset pack;
- written/video impressions.

Please disclose any free review access according to your platform rules.
```

## No-Embargo Template

```text
For this already approved and logged HECTON-8 preview access, there is no embargo. You may publish whenever ready.

Please note the build state honestly:
[build state]

Please disclose free access according to your platform rules.
```

## Preview Build Boundary Copy

Every preview access email must include:

```text
Current build includes:
- [Feature 1]
- [Feature 2]
- [Feature 3]

Current build does not include:
- unsupported multiplayer modes;
- [Feature not present];
- final performance profile;
- final balance/content.
```

## Key Log CSV Schema

```csv
key_id,batch_id,key_type,recipient_name,recipient_url,verified_contact_route,access_route_class,purpose,region,language,build_version,sent_at,status,reply_status_after_send,reply_consent_provenance,coverage_url,disclosure_required,agency_decision_field_source,notes
```

Access rows are not valid if `verified_contact_route`, `access_route_class`, or `reply_consent_provenance` are collapsed into `notes`. `agency_decision_field_source` is required when the access message claims gameplay, pressure, route-risk, threat, salvage, base-failure, or first-public agency proof.

## 2026-05-19 Preview Access Batch V0

Status: protocol-only / no keys / no access authorized.

Use this as the first access batch design after stable preview/demo build proof is logged. Do not request or send keys from this table.

| Batch | Max size | Access type | Recipients | Required gates | Stop condition |
|---|---:|---|---|---|---|
| ACC-001 | 10 | Private preview or Steam Playtest invite | Wave A send-verified creators from CRM | Playtest decision gate passes, recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, first human-send packet gates pass, metadata handoff plus AB-009/KPI decision-read fields exist for any gameplay/pressure/route-risk pitch, contact routes verified, and build known-issues/rollback owner is logged. | Any recipient route cannot be verified or build has first-route blocker. |
| ACC-002 | 10 | Release State Override or approved preview access | Press tracker candidates after presskit gate | Press kit publish gate passes, asset claim checks pass, metadata handoff plus AB-009/KPI decision-read fields exist for first-page gameplay/pressure proof, official inbox custody exists, route rechecked same day, and press tracker `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED`. | Any outlet asks for extra raw keys or route mismatch appears. |
| ACC-003 | 8 | Steam Curator Connect | Curator tracker first-copy candidates | `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, build proof, first screenshots pass asset claim checks, metadata handoff plus AB-009/KPI decision-read fields exist for the agency proof asset, Curator Connect used, not raw keys, and curator tracker `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED`. | Curator asks for external keys or page is stale/formulaic. |
| ACC-004 | 10 | Technical QA/playtest | Low/mid-spec testers selected by score | Hardware specs collected, feedback tags ready, and recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` if access is private rather than public Playtest. | Performance blocks all content feedback. |

### Access Message Must Include

```text
Access type:
Build version:
Allowed capture:
Known issues:
Current build includes:
Current build does not include:
Disclosure requirement:
Feedback route:
Support contact:
access_route_class: private access only / not a public CTA
reply_consent_provenance: press/creator/curator reply only unless explicit separate opt-in exists
```

### Access Stop Rules

Stop sending access if:

- 2 recipients report a build blocker in the first route;
- key/access route leaks outside send-verified contacts;
- press/creator/curator replies are copied into newsletter, playtest, or public-audience lists without explicit separate opt-in;
- recipient asks for false talking points;
- disclosure language is rejected;
- feedback is dominated by multiplayer-scope expectation caused by our copy;
- access copy claims gameplay/pressure/route-risk proof while metadata handoff or AB-009/KPI decision-read fields are missing;
- Steam/Curator/YouTube/FTC rules have not been rechecked that week.

## Scam Red Flags

- "Send 5 keys for our review team" with no staff proof;
- Gmail-only contact claiming large outlet;
- Discord DM from curator;
- curator asks for external key after Curator Connect;
- no recent content;
- outlet/channel has unrelated content only;
- recipient refuses disclosure language;
- urgent pressure for keys before verification.

## Polite Denial

```text
Thanks for reaching out. We are only distributing HECTON-8 preview access through verified press/creator routes and Steam Curator Connect where applicable. I cannot send raw keys through this channel.
```

## Current HECTON-8 Decision

No keys yet. Prepare log and policy only. Actual key/access distribution waits for Steam page, playable build, verified recipient list, QA route, official inbox custody, recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, and exact access-log fields.
