# Keys And Creator Compliance

Status: policy draft
Purpose: prevent key scams, legal slop, and trust damage

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- current platform policy pages
- fresh campaign logs and source artifacts

No Steam policy, FTC/legal, creator-contact, key-distribution, runtime build, Unity import, profiler, player-build, or marketing-performance proof is implied unless this document links a fresh evidence artifact. Historical campaign assumptions and older platform-rule summaries inside this file are subordinate to current official policy pages and current project authority docs.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Hard Rules

- No keys from build existence alone; a stable demo/review build is only a prerequisite, and key/access send still requires official inbox custody, exact recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, access-log fields, disclosure, and the relevant press/creator/curator send gate.
- No raw keys to unverified contacts.
- No key/access send before `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED` and key/access log fields exist.
- No key/access send before `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` for the exact recipient or batch.
- No key/access message can reference assets that have not passed asset metadata claim checks.
- No key/access message can claim gameplay, pressure, route-risk, threat, salvage, base-failure, or first-public agency proof unless AB-009/KPI has `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.
- No private access link can be used as a public CTA, social bio link, showcase CTA, or presskit download link.
- No press key/access can proceed unless the press tracker row has `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED`.
- No curator key/access can proceed unless the curator tracker row has `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED`.
- Prefer Steam Curator Connect for Steam curators.
- Track every key.
- Require disclosure for sponsored/free-key coverage where applicable.
- No paid creator placement can proceed unless the CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.
- Do not pay with keys.
- Do not buy reviews.
- Do not imply coverage is required for a free key.

## Key Types

| Type | Use | Batch Size |
|---|---|---:|
| Demo key | verified creator/press demo access | 10-50 |
| Preview key | embargoed pre-release preview | 10-30 |
| Review key | near-launch review coverage | 20-100 |
| Curator Connect | Steam curator review | as selected |
| Internal QA key | QA only | as needed |

## Key Request Red Flags

- Generic Gmail claiming to represent a huge creator.
- No public link chain from official channel/site.
- "We are curator group, send 50 keys."
- Domain registered recently.
- Refuses Curator Connect.
- Asks for Steam gifts instead of keys.
- Claims impossible audience numbers.
- Sends a copied template with wrong game name.
- No history covering similar games.
- Pushes resale-friendly wording.

## Verification Steps

1. Check official channel/site for business contact.
2. Confirm email/domain matches public contact.
3. Check recent videos/articles.
4. Check whether they covered similar games.
5. Check if they disclose sponsored/free-key content.
6. Confirm `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`.
7. Confirm `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` for the exact recipient or batch.
8. Confirm asset/build/access links only after asset metadata claim checks, build/access owner custody, recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, disclosure, and exact access-log fields pass.
9. Confirm AB-009/KPI decision-read proof for any gameplay/pressure/route-risk claim in the access message.
10. Confirm press/curator `send_permission_gate` allow value if the recipient is from either tracker.
11. Record `access_route_class` and `reply_consent_provenance`.
12. Assign key batch ID.
13. Log key issue date and access route.
14. Follow up once only through the verified route.
15. Revoke/disable unused campaign keys if possible.

## Disclosure Notes

Public sources to recheck before paid/free-key campaigns:

- FTC Endorsement Guides: https://www.ftc.gov/business-guidance/resources/ftcs-endorsement-guides-what-people-are-asking
- YouTube Paid Promotions: https://support.google.com/youtube/answer/10588440?hl=en
- TikTok Branded Content Policy: https://www.tiktok.com/legal/page/global/bc-policy/en
- Steam Keys: https://partner.steamgames.com/doc/features/keys

## Creator Copy Requirement

If a creator receives payment or a key under conditions requiring disclosure, the campaign brief must say:

> Please follow your platform and local disclosure rules. If you received a key or payment, disclose it clearly to your audience.

Do not write the disclosure for them unless asked. Do not hide it.

## Internal Key Log Schema

| Field | Required |
|---|---|
| key_id_or_batch | yes |
| recipient_name | yes |
| recipient_url | yes |
| verified_contact_route | yes |
| key_type | yes |
| date_sent | yes |
| embargo | optional |
| reply_status_after_send | yes |
| coverage_url | optional |
| notes | optional |
| access_route_class | yes |
| reply_consent_provenance | yes |
| agency_decision_field_source | yes if the access message claims gameplay/pressure/route-risk proof |

`reply_consent_provenance` defaults to creator/press/curator reply only. A key request, curator reply, or press reply is not newsletter, playtest, or public marketing consent unless the recipient explicitly opts into that separate route.

## Refusal Template

Use when a request is suspicious:

> Thanks for reaching out. We are not distributing keys through unverified requests. When a public demo/review build is ready, we will use verified creator contacts and Steam Curator Connect where possible.

Do not accuse. Do not argue.
