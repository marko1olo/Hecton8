# Validation 1772

Evidence class: STATIC_SOURCE.

## Commands And Results

`python Tools/AppliedLoreRuntimeAudit.py --root . --source-only`

Result: FAIL, unrelated publication-page blocker outside edited set.

Output:

```text
AppliedLore audit FAILED: Publication page C:\hades\Hecton8\Docs\Lore\AppliedContent\external_site\ru_RU\P456_SITE_HOME_LONGFORM_BRIEF.md missing frontmatter line: localization_status: source_ready
```

Finding: the file currently has `localization_status: draft_native_pass_pending`. No edited packet or selected in-game wiki page was named by this failure.

`python Tools/LoreTextBoundsVerifier.py --root . --json-report Docs/Lore/AppliedContent/production_audits/1772/lore_text_bounds_1772.json --csv-report Docs/Lore/AppliedContent/production_audits/1772/lore_text_bounds_1772.csv`

Result: PASS command, global issues remain in existing/draft content.

Output:

```text
lore_text_bounds packets=460 surfaces=48300 issues=60222 collisions=0 rewrites=0
json=Docs/Lore/AppliedContent/production_audits/1772/lore_text_bounds_1772.json
csv=Docs/Lore/AppliedContent/production_audits/1772/lore_text_bounds_1772.csv
```

Selected-row filter:

```text
selected_rows=630 selected_issues=399 selected_en_US_issues=0
```

Finding: edited en_US selected rows have zero text-bound issues after trimming. The remaining selected issues are non-English/draft rows already marked stale for native review.

`ConvertFrom-Json` parse check on edited packet files.

Result:

```text
JSON_OK Docs/Lore/AppliedContent/packets/RS010_PRESSURE_MACHINERY_RETURN_ROUTE.packets.json
JSON_OK Docs/Lore/AppliedContent/packets/RS012_PLAYER_LIABILITY_ESCAPE.packets.json
JSON_OK Docs/Lore/AppliedContent/packets/RS013_COLONY_ATLAS_MAINTENANCE.packets.json
JSON_OK Docs/Lore/AppliedContent/packets/RS045_PHOTIC_SHELF_NATIVE_ECOLOGY.packets.json
JSON_OK Docs/Lore/AppliedContent/packets/RS059_ECOLOGY_CODEX_SPECIMEN_CARDS.packets.json
```

`rg -n "defines|Codex card should|Use for|Presentation rule|horror-only" <six changed en_US wiki pages>`

Result: no matches. `rg` returned exit code 1 for no matches.

`git diff --check -- <edited packet/page/audit files>`

Result: no whitespace errors. Git emitted CRLF normalization warnings only.

Packet-to-page parity check:

```text
PARITY_OK P046_PUMP_ROOM_HANDSHAKE
PARITY_OK P049_SONAR_RETURN_ROUTE
PARITY_OK P060_FIRST_HOUR_SPINE
PARITY_OK P061_MAINTENANCE_ECOLOGY
PARITY_OK P221_PHOTIC_MAT_BASELINE
PARITY_OK P291_PHOTIC_MAT_CODEX_CARD
```

## Validation Boundary

- No Unity Editor run.
- No dotnet build.
- No runtime PDA database import/bake claimed.
- No native localization review claimed.

## Additional Pass - 2026-06-04

Scope:

- Additional selected IDs: `P017_AEGIR_MOON_LADDER`, `P018_HECTON8_DROWNED_GEOLOGY`, `P020_HECTON8_ECOLOGY_REGISTRY`, `P031_PHOTIC_SHELF_LIFE`, `P032_PRESSURE_LADDER_DEPTH_BANDS`, `P033_CABLE_REEF_SYMBIOSIS`.
- Edited source packet files: `RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY.packets.json`, `RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE.packets.json`.
- Edited page mirrors: the six matching `Docs/Lore/AppliedContent/in_game_wiki/en_US/*.md` pages.

Checks:

```text
all_packet_json_parse_pass 451
packet_page_parity_pass 6
selected_en_US_banned_phrase_pass 6
selected_markdown_shape_pass 6
selected_bounds_rows 42
selected_bounds_issues 0
inventory_rows 6900
locale_count 15
missing_expected_locales []
```

Blocked checks:

- `Tools/LoreTextBoundsVerifier.py --release-set RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY`: missing `Docs/Lore/AppliedContent/release_sets/RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY_manifest.json`.
- `Tools/LoreTextBoundsVerifier.py --release-set RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE`: missing `Docs/Lore/AppliedContent/release_sets/RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE_manifest.json`.
- `Tools/AppliedLoreRuntimeAudit.py --source-only`: unrelated `external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` frontmatter mismatch.

Boundary:

- No Unity Editor run.
- No dotnet build.
- No runtime PDA database import/bake claimed.
- No native localization review claimed.
