# Rationale 1820

## Decisions

- Treated every non-English row as not native-final unless a concrete human/native review artifact was present. Reason: root localization authority and 1777 risk docs both say generated/non-English rows are review-gated; status labels alone are not release proof.
- Built the release queue across all six current release surfaces (`in_game_wiki`, `external_site`, `scanner`, `terminal`, `audio`, `field_note`) rather than only public pages. Reason: task requested game wiki, scanner facts, logs/audio, reader, and public site triage from current evidence.
- Marked generated-page/frontmatter mismatches as `blocked_status_drift` even when source/index rows were otherwise usable. Reason: release consumers cannot trust conflicting status channels.
- Did not run AppliedLore exporters, bakes, Unity, or source repairs. Reason: task was report-only and P151/exporter drift is serialized to a separate owner.
