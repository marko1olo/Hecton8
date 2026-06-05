# Rationale 2305 - Visual Acceptance Rubric

Status: STATIC VERIFIED

## Decisions

- Reject packets by missing or false route view before taste grading. A mislabeled surface capture cannot earn partial underwater credit.
- Require six views for acceptance: surface, shoreline, underwater 0-5 m, underwater 20-50 m, Aegir/celestial, regression low-oblique.
- Treat mandatory references as direction: bright surface, readable photic water, caustic/foam/material response, textured geology, large atmospheric Aegir.
- Treat 1466-1473 named defects as explicit reject examples: acid green, flat tinted plane, black streaks, pale slab, false underwater label, debug foam sheet, primitive celestial dot/disc, empty seabed.
- Keep runtime acceptance gated by clean post-capture log. Static screenshots alone cannot clear repeated exceptions, forced-load exit, compile/import loop, or stale log risk.
- Screenshot metadata is mandatory to prevent fake packets: timestamp, scene, camera, coordinates/depth, quality weight, UI state, post stack, renderer path, log link, checksum.

## Evidence Boundary

All conclusions are static review of files, screenshots, and reports. No runtime health was proven.
