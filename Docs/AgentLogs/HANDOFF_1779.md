# HANDOFF_1779

Evidence class: STATIC_SOURCE plus local HTTP smoke.

## Remaining Gaps

- Browser QA is still required. Playwright is not installed in this workspace, so no rendered layout, click-flow, RTL visual, overflow, or mobile viewport proof exists.
- Port 8788 was occupied and unresponsive during verification. Smoke used temporary port 8790 and stopped the server afterward.
- Source voice is not filterable because current publication indexes do not expose a stable source-voice column.
- Non-cluster article spoiler state is `not indexed`; only cluster-linked rows can show a spoiler tier.
- Markdown body search is cached only after pages are fetched. Packet JSON body search covers loaded packet surfaces after packet warm-up.
- Status vocabulary is bucketed in-reader for display, but future generated indexes should still provide canonical status category plus raw status.
- Packet JSON unresolved loads are surfaced as controller metadata, but the reader does not yet provide a full missing-packet report page.

## Future Index Requests

- Add `spoiler_tier` to `Publication_Surface_Index.csv`.
- Add `source_voice` to publication and packet indexes.
- Add `packet_json_path` to the surface index to avoid probing direct and release-set paths.
- Add canonical status category plus raw status field.
- Add per-packet available surfaces as explicit columns or a generated sidecar manifest.

## Browser QA Route

Run:

```text
python -m http.server 8788 --bind 127.0.0.1 --directory Docs/Lore/AppliedContent
```

Then verify:
- locale switches: `en_US`, `ru_RU`, `ja_JP`, `ar_SA`, `he_IL`, `pt_BR`;
- `ar_SA` and `he_IL` set RTL layout and keep Latin IDs readable;
- packet surface filter works for scanner, field_note, terminal, audio, in_game_wiki/codex, external_site;
- cluster filter starts at `site_wiki_start_here_cluster`;
- spoiler filter shows tiered cluster rows and `not indexed` rows;
- controller panel shows localization status without injecting draft labels into article bodies.
- controller panel reports packet warm-up counts, direct fallback counts, unresolved packet load count, and localization text-length risk warnings.

## C# Follow-Up Notes

- `ReadableMainMenuOverlay1428` now depends on `MainMenuController.TryGetReadableOverlayCamera` for overlay camera identity. Do not reintroduce `Camera.main` or scene tag search as a fallback.
- `MainMenuController.Tick` and `LateFrameTick` remain the phase boundary: input/state delta in `Tick`, visual presentation sync in `LateFrameTick`.
- Runtime validation is still required in Unity because this pass intentionally did not run `dotnet build` while Unity `dotnet.exe` was active.
- `ShinobuFloraFaunaSymbiosisSolver` read-side snapshot copies and tuning/counter reads now use existing per-buffer mutation guards with `finally` release. Keep that single-buffer guard shape; do not widen it into multi-buffer simultaneous locks.
- `VocalWarningSystem` now has separate dispatcher systems for simulation scheduling and visual-sync presentation. Do not collapse this back into post-simulation synchronous execution.
- Vocal warning job guards transfer ownership to the pending job and release only on completed visual-sync finalization or teardown/rebind force-complete.
- `SaveManager` WFC outpost storm drain now depends on bounded stackalloc unique-sector collection. Do not reintroduce backward duplicate scans or per-sector full-batch rescans.
- `SaveSlotManagerWindow` validates against `SaveManager.CollectAllKnownArtifactPaths`; a previous missing-method console entry was stale after source reload.
