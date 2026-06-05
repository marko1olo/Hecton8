# Rationale 2016

Decision: use existing 2011 validator outputs and SHINOBU material audit CSVs as primary evidence, then corroborate top blockers with selected `.mat` YAML.

Reason: mission forbids Unity, asset edits, generated texture writes, and broad unrelated reads. Static validator outputs already quantify the material debt. Direct YAML checks prevent a report that merely repeats prior summaries.

No lightweight static scripts were run. Existing reports were sufficient, and the task requested triage rather than regeneration of audit artifacts.

Ranking rationale:
- Blocking sky/skybox/terrain/triplanar/wetness unresolved refs first because they affect surface, coast, sky, waterline, and medium-depth hero readability.
- Surface rock unresolved refs next because the audit explicitly marks them `BLOCKER`.
- Flora/coral channel issues ranked high, not blocker, when source maps exist but shader/channel acceptance is unproven.
- Placeholder route ranked blocker because any production binding to placeholders violates the visual floor even if individual placeholder materials share some texture refs.

Evidence boundary: static text/YAML only. No visual acceptance claimed.
