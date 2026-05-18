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

- No keys before a stable demo or review build exists.
- No raw keys to unverified contacts.
- Prefer Steam Curator Connect for Steam curators.
- Track every key.
- Require disclosure for sponsored/free-key coverage where applicable.
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
6. Assign key batch ID.
7. Log key issue date.
8. Follow up once.
9. Revoke/disable unused campaign keys if possible.

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
| verified_contact | yes |
| key_type | yes |
| date_sent | yes |
| embargo | optional |
| reply_status | yes |
| coverage_url | optional |
| notes | optional |

## Refusal Template

Use when a request is suspicious:

> Thanks for reaching out. We are not distributing keys through unverified requests. When a public demo/review build is ready, we will use verified creator contacts and Steam Curator Connect where possible.

Do not accuse. Do not argue.
