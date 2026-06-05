# Rationale_1779

Evidence class: STATIC_SOURCE unless stated otherwise.

## Mandates Loaded

- `QA_Evidence_Text_Filter_Audit.txt`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Decisions

1. Static local reader remains plain HTML/JS/CSS.
   - Reason: task requires local repo use and no internet/CDN dependency.
   - Rejected: npm/build pipeline, CDN markdown parser, generated bundle.

2. Locale roster is hard-authoritative in the reader.
   - Reason: index currently has all 15 locales, but selector must not silently shrink if a malformed/missing row appears.
   - Consequence: unavailable locales are shown as zero-row states, not hidden.

3. RTL handling uses browser `dir`, `lang`, and logical source strings.
   - Reason: root localization docs forbid manual RTL reversal. Browser text engine is correct for this static editor page.
   - Scope: `ar_SA` and `he_IL` set document/article/panel direction to RTL.

4. Indexed publication surfaces and packet text surfaces are separate controls.
   - Reason: `Publication_Surface_Index.csv` has only `external_site` and `in_game_wiki`; packet JSON carries scanner, field_note, terminal, audio, title, wiki, site.
   - Consequence: add publication surface filter plus packet surface filter/detail panel.

5. Localization status is controller-facing metadata only.
   - Reason: docs forbid draft/native-review state inside player-visible prose.
   - Consequence: badges and summary panels show status; rendered Markdown/packet text is not rewritten.

6. Spoiler visibility comes from `Publication_Cluster_Index.csv` where available.
   - Reason: surface index has no spoiler field. Cluster index has `spoiler_tier`.
   - Consequence: non-cluster articles show `spoiler: not indexed`.

7. Surface brightness warning is static audit only.
   - Reason: task asks for editor/controller UI warning, not content mutation.
   - Consequence: selected text is scanned for suspect dark-surface terms and displayed as a warning panel when matched.

8. Status display preserves raw index values.
   - Reason: current AppliedContent index uses `source_ready` and `draft_native_pass_pending`; root/localization docs list broader future statuses.
   - Consequence: reader uses raw status filters and `Localization_Status_Index.md` summaries. Future normalized status categories require a generated status map.

9. Source-voice filter was not added.
   - Reason: neither `Publication_Surface_Index.csv` nor `Publication_Cluster_Index.csv` exposes a stable source-voice column.
   - Consequence: packet-surface filtering covers scanner, field note, terminal, audio, wiki/codex, and site. Source voice remains a future generated-index gap.

10. Packet library warm-up loads all known packet JSON files.
    - Reason: release-set packets cannot always be derived directly from `packet_id` without checking release-set JSON.
    - Consequence: search can use packet body text for all loaded packets. Markdown body search is available for pages already fetched by the reader.

11. Packet bundle fallback is not an error.
    - Reason: current AppliedContent stores one release-set family as direct packet JSON rather than a bundle file.
    - Consequence: reader reports bundle fallback and unresolved packet loads separately. Only missing direct packet JSON after fallback becomes a hard controller warning.

12. Status normalization is display-only.
    - Reason: generated indexes must keep raw status values for traceability, but controller QA needs ready/draft/blocked buckets.
    - Consequence: filters still use raw statuses; badges and summary counts include the derived bucket.

13. Localization overflow checks are warnings, not text edits.
    - Reason: static reader cannot authoritatively shorten translated/audio/scanner copy.
    - Consequence: selected packet surfaces expose controller warnings for long title, scanner, field note, terminal, audio, external-site, and long Markdown body cases.

## Verification Notes

- `reader.html` JS syntax parsed with Node.
- Static locale contract count: 15.
- `ar_SA` and `he_IL` are explicit RTL in reader and index.
- All packet JSON parsed through PowerShell: 100 files, 460 packets, zero parse failures.
- HTTP smoke on 8790 passed. Port 8788 was occupied and unresponsive, so the exact requested port could not be used for smoke in this environment.
- Polish HTTP smoke on 8791 passed and the temporary server was stopped.
- Packet warm-up simulation: 92 release sets, 460 packets, 91 bundle files, one bundle fallback, nine direct fallback packets, zero unresolved packet JSON loads.
- No `dotnet build` was run.
- Browser automation not run; Playwright package missing.

## C# Apex Follow-Up Decision

14. Overlay camera resolution must use the existing main menu owner.
    - Reason: `Camera.main` performs tag-based scene lookup and is not acceptable as a dependency route for a runtime overlay, even when the current call path is cold.
    - Change: `ReadableMainMenuOverlay1428.ResolveOverlayCameraCold` now asks `MainMenuController.TryGetReadableOverlayCamera`, caches the result, and returns null if no cached authored camera is valid.
    - Rejected: adding a new camera manager/helper, because `MainMenuController` already owns the authored menu camera.

15. No DataVault patch was made in this follow-up.
    - Reason: inspected related UI/atmosphere write helpers already use single acquired write views with `finally` release. `GasDynamicsSolver` state ownership uses one mutation guard mask and explicit release bookkeeping rather than stacking multiple write locks.
    - Consequence: no DTO layout changed, so no new `UnsafeUtility.SizeOf<T>()` validation was required.

16. Symbiosis read snapshots now use the existing Vault guard route.
    - Reason: `ShinobuFloraFaunaSymbiosisSolver` already fenced write publication with `TryAcquireSymbiosisMutationGuard`, but read-side snapshot copying and tuning/counter reads resolved Vault buffers without the same strict release boundary.
    - Change: `TryCopyVaultBufferToSnapshot`, `TryReadSymbiosisTuning`, and `TryReadSymbiosisCounter` now acquire one buffer guard and release it in `finally`.
    - Rejected: a new DataVault wrapper or parallel lock manager, because the solver already owns a local guard helper.
    - First-20-minutes effect: stabilizes photic-shallow flora/fauna symbiosis snapshots used by ambient ecology and scanner-belief presentation.

17. Vocal warnings must not block the hot frame with synchronous job execution.
    - Reason: `VocalWarningSystem` previously used job work in the frame path; dispatcher ownership requires scheduling during simulation and presentation after simulation settles.
    - Change: simulation phase schedules evaluate/dispatch jobs, visual sync finalizes only completed work and publishes presentation state. Teardown/rebind is the only force-complete path.
    - Rejected: a parallel audio warning runner, because the existing `VocalWarningSystem` already owns DTO buffers, dispatcher registration, and presentation publication.
    - Scalability: weak devices can skip a frame when the job is not complete; middle/high/ultra devices keep the same route but finish more often before visual sync without changing gameplay truth.

18. WFC outpost storm persistence needs bounded linear collection.
    - Reason: storm batches previously used duplicate-sector scans and per-sector forward walks, causing avoidable O(n^2) work and possible late-signal truncation when the batch exceeded scratch length.
    - Change: storm drain now collects unique sectors once into stackalloc scratch, hydrates and commits per unique sector, and records overflow via existing black-box telemetry.
    - Rejected: new persistence queues or helper managers, because `SaveManager` already owns WFC save identity, telemetry, and artifact routes.
    - Scalability: weak devices avoid storm spikes; middle/high/ultra devices spend saved CPU on visual/gameplay work without changing persistence identity.
