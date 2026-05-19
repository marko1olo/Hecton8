# Campaign 02 - Steam Page Launch

Status: future / requires Steam page assets
Public stance: single-player-first / no co-op promise
Runtime impact: none

## Objective

Launch a Coming Soon page that converts cold traffic to wishlists without overselling features. The Steam page is the funnel center; every creator, post, clip, and press beat should point there only after the page can survive cold-read QA.

## Required Before Launch

- Steam capsule readable at thumbnail size.
- 8 real screenshots in correct order.
- short description tested.
- About This Game text cleaned of false roadmap promises.
- tags selected.
- no co-op language.
- no performance claims without proof.
- press kit shell ready.
- tracking spreadsheet ready.

## Page Position

Short description candidate:

Single-player underwater survival below the light: pressure, salvage, machinery, black-water exploration, and a Seed Ship anomaly that makes the ocean stop feeling neutral.

## Launch Sequence

| Day | Action |
|---:|---|
| -7 | Final cold-read test: capsule + first 4 screenshots. |
| -5 | Verify store copy against forbidden claims. |
| -3 | Prepare Steam announcement, press note, creator micro-pitch. |
| -1 | Confirm press kit assets and no broken links. |
| 0 | Publish Coming Soon page, post one announcement, notify first verified creator batch. |
| +1 | Review traffic, wishlists, comments, capsule read. |
| +3 | Send second small creator batch only if page conversion is not terrible. |
| +7 | Publish one systems/dev update, not another generic screenshot dump. |

## Outreach Batch

Do not send to 5000 leads.

First Steam page batch:

- 10 direct underwater survival creators;
- 5 engineering/base systems creators;
- 5 horror/abyss creators;
- 5 regional leads with localized pitch;
- 5 press/newsletter/showcase targets.

Total: 30 sends max.

## Pitch

Subject:

HECTON-8 Steam page is live - pressure/machinery underwater survival

Message:

Hi [Name],

Your channel fits because [verified specific content pattern].

HECTON-8 is a single-player-first underwater survival game about pressure, machinery, salvage, and black-water exploration. This is not a co-op promise and not a "Subnautica killer" pitch.

The Steam page is now live: [link]

The specific angle for your audience is [segment-specific reason].

If this looks useful for future coverage, I can send the demo when the slice is ready.

## Steam Announcement

Title:

HECTON-8 Steam Page Is Live

Body:

HECTON-8 is now on Steam as a Coming Soon page.

The current focus is single-player underwater survival: pressure, machinery, salvage, black-water exploration, and habitats that act like survival infrastructure.

What HECTON-8 is not promising:

- co-op;
- "Subnautica killer" comparison-war marketing;
- performance claims without measured proof.

Wishlist if the direction fits what you want from darker underwater survival. Feedback on screenshot readability and capsule clarity is still useful.

## Metrics

Track daily:

- visits;
- wishlists;
- wishlist conversion;
- traffic source;
- region;
- screenshot click order;
- comments: "generic", "too dark", "clone", "interesting machinery", "want demo".

Initial useful targets:

- 5-10% visit-to-wishlist for warm traffic;
- 2-5% for cold traffic;
- 30 creator sends -> 3+ replies;
- 1-2 small press/newsletter mentions from the first wave.

## Kill Criteria

Pause outreach and revise page if:

- warm traffic wishlist conversion is below 3%;
- the capsule is repeatedly called unreadable;
- comments ask "what do you do?" after seeing the page;
- co-op confusion appears repeatedly;
- players think the screenshots are concept art or renders, not gameplay.

## 2026-05-19 Steam Page Launch Gate V0

Status: blocked until Campaign 01 returns `KEEP`.

Do not launch the Steam page because assets exist. Launch only when the first screenshot campaign proves the page can be understood.

### Required Upstream Decisions

| Dependency | Required result | Source |
|---|---|---|
| Screenshot campaign | `KEEP` decision, not `REVISE` or `KILL` | `Campaigns/CAMPAIGN_01_FIRST_SCREENSHOT_DROP.md` |
| Steam page assembly | Candidate A/B/C selected by evidence | `Steam/STORE_PAGE_COPY_MATRIX.md` |
| Steam asset ticket | 6+ public shots pass and rejects are logged | `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md` |
| Capsule test | AB-002 has a winner | `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md` |
| Tag proof | AB-007 does not create co-op/open-world/sim confusion | `SEO/STEAM_TAG_AND_SEARCH_STRATEGY.md` and experiment log |
| UTM registry | Official Steam URL can be used with canonical names | `Analytics/MEASUREMENT_AND_UTM_PLAN.md` |
| Objection handling | FAQ first-screenshot matrix is ready | `Community/PUBLIC_FAQ_AND_OBJECTION_HANDLING.md` |

### Launch Asset Minimum

| Steam surface | Minimum acceptable input |
|---|---|
| Short description | Candidate A unless first assets prove B or C harder. |
| First screenshot | `PLAN-SHOT-001` or winning AB-001 asset. |
| Player verb screenshot | `PLAN-SHOT-003`. |
| Base/machinery screenshot | One of `PLAN-SHOT-002`, `PLAN-SHOT-004`, or `PLAN-SHOT-005`. |
| Threat/anomaly shot | `PLAN-SHOT-006` or `PLAN-SHOT-007` only if readable. |
| Capsule | AB-002 winner from `PLAN-CAPSULE-001`. |
| Trailer | Optional; if included, first 3 seconds must be AB-006 winner. |

### First 7 Days Decision Rule

After launch, choose exactly one path:

- `EXPAND`: warm traffic is at or above 5% visit-to-wishlist, page comments understand the player verb, no major co-op/AI/darkness confusion.
- `REVISE_PAGE`: traffic exists but conversion/comments show capsule, screenshot order, or copy problem.
- `STOP_OUTREACH`: warm traffic is below 3%, page is misunderstood, or official links/assets are broken.

No press expansion, paid PMT test, or second creator batch before this decision.
