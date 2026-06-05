# Rationale 1771

Evidence class: STATIC_DOC / STATIC_SOURCE

## Decisions

- Selected `P456_SITE_HOME_LONGFORM_BRIEF` because it is the public home entry and currently reads as an internal assembly brief. Rejected editing random late-game spoiler pages first because the home/start route has higher public value and lower dependency risk.
- Kept the pass scoped to `external_site` outputs and public indexes. Rejected editing `in_game_wiki` pages because the task explicitly forbids in-game wiki edits except for comparison.
- Treated non-English generated text as draft unless native/fluent proof exists. No native-reviewed or runtime-ready localization status is claimed.
- Treated static `rg` scans as text-presence evidence only. No runtime, export, font, RTL, or publication readiness is claimed from search hits.
- Rewrote `external_site/*/P456_SITE_HOME_LONGFORM_BRIEF.md` directly because the existing exporter overwrites both external-site and in-game-wiki pages. Rejected running `--overwrite` during the edit phase because it would violate the task boundary.
- Updated `Publication_Surface_Index.csv` and external-site `INDEX.md` entries only for P456. Rejected editing `Publication_Cluster_Index.csv` because P456 is not a cluster packet; spoiler routing is recorded in `public_site_editorial_map.md`.
- Left `P458_DEEP_REACH_LIABILITY_LONGFORM_BRIEF` marked as weak next-wave work. It is not a small passage patch; it needs a full public liability article pass.
- Replaced the last negative "black void" shelf framing in P456 with positive visible-work-zone language across all 15 locales. Darkness remains assigned to storms, eclipse windows, interiors, caves, and deeper pressure bands.
