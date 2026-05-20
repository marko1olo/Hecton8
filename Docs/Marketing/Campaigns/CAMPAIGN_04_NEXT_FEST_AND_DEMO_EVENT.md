# Campaign 04   Next Fest And Demo Event

Status: future / platform rules and `SHOW-001` submission gate must be rechecked before commitment
Public stance: single player first scope / proof first campaign copy
Runtime impact: none

## Objective

Use a Steam demo event only when the demo can convert traffic and withstand public comparison. Next Fest should not be the first proof test. It should amplify an already working demo.

## Official Rule Boundary

Recheck Steamworks before scheduling:

  https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest?language=english
  https://partner.steamgames.com/doc/marketing/wishlist?language=english
  https://partner.steamgames.com/doc/store/earlyaccess

Platform rules are external facts and can change. Do not rely on old notes.

## Eligibility/Readiness Gate

Before committing:

  `SHOW-001` in `Press/SHOWCASE_SUBMISSION_TRACKER.csv` has `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`;
  public Steam page exists through `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and Official CTA Link Activation Gate V0 passes for any public link;
  demo exists and `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`;
  screenshots/capsule are proven;
  first demo route has a measurable player decision that feedback can code as `AGENCY_DECISION_READ`;
  creator batch has produced signal;
  first 20 minutes are stable;
  demo has clear CTA packet or no-link fallback;
  localized copy for target regions is ready;
  no unsupported multiplayer-scope confusion on store page.

Route separation: Next Fest/event traffic uses public Steam/demo CTA links only after the Steam page publish gate, public demo access gate, and CTA activation pass. Creator preview reminders can use private access routes only through review-key/access protocol and must not be pasted into public event posts, bios, or showcase materials.

Submission separation: Next Fest registration/commitment uses the showcase tracker row `SHOW-001`. A strong demo, public Steam page, CTA packet, public demo gate, or Steam announcement approval does not permit registration unless `SHOW-001` has `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`.

## Event Prep Timeline

| Time | Work |
|---|---|
|  10 weeks | Recheck eligibility, `SHOW-001` submission gate, demo scope, page state. |
|  8 weeks | Lock demo route and trailer/update beats. |
|  6 weeks | Start creator warm up, no key flood. |
|  4 weeks | Localize short pitch and Steam announcement skeletons. |
|  3 weeks | Capture fresh clips from demo build. |
|  2 weeks | Send preview/demo reminders to send verified creators after official route, asset fit, creator utility, `creator_send_gate`, and CRM send-log gates pass. |
|  1 week | Publish "demo coming" announcement only if platform rules allow it, `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`, `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`, and CTA/access route gates pass. |
| Event start | Push demo, Steam announcement, creator batch, community posts only after their exact permission gates pass. |
| Event mid | Publish systems update and fix critical blockers. |
| Event end | Recap, approved Steam CTA after CTA activation, feedback digest. |

## Event Content Beats

| Beat | Content |
|---|---|
| Day 1 | Future demo live beat: pressure decision, salvage, machinery, black water. |
| Day 2 | Base as machine systems post. |
| Day 3 | Seed Ship/anomaly teaser. |
| Day 4 | Community feedback fixes, if real. |
| Day 5 | Creator highlight, only if creator consent/coverage exists. |
| Final day | Last chance to play demo / approved Steam CTA after CTA activation. |

## Event Pitch

Subject:

Future subject after event/demo gate: HECTON-8 demo is live for Steam demo event

Message:

Hi [Name],

The HECTON-8 demo is live on Steam for [event name].

Your audience fits because [verified content pattern]. The demo is a single player first underwater survival slice focused on pressure, machinery, salvage, and black water exploration. Proof first scope, no competitor war pitch.

Steam/demo: [approved public Steam/demo URL after CTA activation packet]
Suggested angle: [segment angle]

If you cover it, disclosure: demo/key provided by developer where applicable.

## Metrics

Track:

  event impressions;
  demo downloads;
  demo launches;
  demo median playtime;
  demo completion;
  agency decision read rate;
  wishlists/day;
  creator coverage;
  top feedback issues;
  bug reports;
  region spikes.

## Kill Criteria

Do not enter Next Fest if:

  `SHOW-001` does not have `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`;
  demo is unstable;
  no strong Steam capsule exists;
  first screenshots still read as generic;
  no one outside the team can understand the first route or name one player decision;
  verified creator batch produces no signal;
  wishlist conversion is weak and unexplained.
