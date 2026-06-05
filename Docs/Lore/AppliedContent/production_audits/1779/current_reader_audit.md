# Current Reader Audit - 1779

Evidence class: STATIC_SOURCE

## Existing Capabilities

- Loads `Publication_Surface_Index.csv` and `Publication_Cluster_Index.csv` through browser `fetch`.
- Parses CSV locally with a small custom parser.
- Asserts the full 15-locale contract and uses browser `dir`/`lang` for `ar_SA` and `he_IL` RTL.
- Filters by publication surface, locale, release set, raw localization status, spoiler state, packet surface, and search text.
- Lists clusters from `Publication_Cluster_Index.csv` and starts at `site_wiki_start_here_cluster`.
- Lists articles from `Publication_Surface_Index.csv`.
- Loads selected Markdown page by relative `page_path`.
- Loads packet JSON from direct packet files and release-set bundles.
- Shows packet title, scanner, field note, terminal, audio, in-game wiki/codex, and external-site surfaces.
- Renders a minimal Markdown subset: headings, lists, paragraphs, fenced code.
- Displays raw localization status plus ready/draft/blocked display buckets without rewriting player text.
- Displays cluster spoiler tier when indexed and `not indexed` when absent.
- Shows controller-facing surface brightness, packet load, status, direction, and localization length-risk warnings.
- Sets article body `dir` from locale/index direction.
- Has no CDN or external library dependency in current source.

## Remaining Gaps

- Browser-render QA is still missing because Playwright is not installed.
- Source voice is still not filterable because current indexes expose no stable source-voice column.
- Markdown body search is cached only after a page is fetched; packet JSON body search is available after packet warm-up.
- Surface index still lacks direct `spoiler_tier`, `packet_json_path`, and canonical status category columns.
- Reader surfaces unresolved packet JSON loads, but does not yet provide a full missing-packet report page.

## Data Sources Inspected

- `Publication_Surface_Index.csv`: 13,800 rows; surfaces `external_site`, `in_game_wiki`; 15 locales; statuses `source_ready`, `draft_native_pass_pending`.
- `Publication_Cluster_Index.csv`: 150 rows; 5 clusters per surface/locale; includes `spoiler_tier`, `truth_payload`, `player_question`, cluster packet/prereq/next packet ids.
- `Localization_Status_Index.md`: per-locale status counts; `ar_SA` and `he_IL` are RTL; draft state must stay out of player-visible prose.
- `packets/P001_CRASH_SHELF.json`: single-packet shape with `localized.{locale}.title/scanner/field_note/terminal/audio/in_game_wiki/external_site`.
- `packets/RS003_HUMAN_SPACE_AEGIR_ROUTE.packets.json`: release-set shape with root `packets[]`, each packet carrying localized surface text.

## Parse Risks

- CSV rows may be malformed; reader must keep working and report skipped rows.
- Packet files may be single-packet JSON or release-set JSON with `packets[]`.
- Packet file path cannot be derived perfectly from `packet_id` for release-set packets without scanning release bundles or probing direct packet files.
- Local `file://` loading blocks `fetch` in most browsers; local HTTP server remains the expected route.
- PowerShell console output showed mojibake for some non-ASCII text; source files are still consumed by browser as UTF-8 via HTML charset.

## Polish Verification

- Packet warm-up simulation: 92 release sets, 460 packets, 91 bundle files, one bundle fallback, nine direct fallback packets, zero unresolved packet JSON loads.
- HTTP smoke on temporary port 8791 returned 200 for reader, CSV indexes, localization status, sample Markdown, direct packet JSON, and release bundle JSON. Server stopped.
- `reader.html` JavaScript parsed with Node `new Function`.
- `dotnet build` was not run.
