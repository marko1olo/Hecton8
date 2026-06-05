# 3204 UTF-8 Mojibake And Clone Audit

Date: 2026-06-05 local.
Worker: 3204 - UTF8_MOJIBAKE_AND_CLONE_AUDIT_OWNER.
Evidence class: STATIC_SOURCE / STATIC_DOC only.
Runtime/native claim: none.
Content edits: none.

## Scope

Read-only audit of:

- `Docs/Lore/AppliedContent/production_packets/*.md`
- `Docs/Lore/AppliedContent/in_game_wiki/*/P456-P460*.md`
- `Docs/Lore/AppliedContent/external_site/*/P456-P460*.md`

Allowed writes:

- `Docs/Reports/Batch32/3204_UTF8_MOJIBAKE_AND_CLONE_AUDIT.md`
- `Docs/Tasks/Status_3204.md`
- `Docs/AgentLogs/LOG_3204.md`

First-20 route blocker removed: static proof lane for AppliedContent text corruption and generated publication clone risk before lore packets become public/wiki/codex/scanner sources for the opening Black Keel / P-63 route.

## Authorities And Mandates

Authority docs read:

- `AGENTS.md`
- `writing.md`
- `localization.md`
- `quality.md`
- `Docs/Lore/Lore_Localization_Model.md`
- `Docs/Lore/Lore_Multilingual_Content_Architecture.md`

Mandates read:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`

Registry note: `.agents-skills/BATCH_INDEX.md` and `.agents-skills/BATCH_INDEX.txt` were not present. Mandates were selected from `.agents-skills/README.md` and the visible registry filenames.

## Marker Set

Required markers:

- `U+00C3`
- `U+00D0`
- `U+00D8`
- `U+00E6`
- `U+00EC`
- `U+00D7`
- `U+FFFD`

Additional review markers:

- `U+00C2`
- `U+00D1`
- `U+00E2`

Reason: these catch common visible mojibake stems such as `A-circumflex`, `N-tilde`, and `a-circumflex` families. They are not automatic corruption proof because the same codepoints can be valid in real localized text.

## Production Packet Inventory

Command:

```powershell
Get-ChildItem -LiteralPath 'Docs/Lore/AppliedContent/production_packets' -Filter '*.md' -File |
  Sort-Object Name |
  Select-Object -ExpandProperty Name
```

Output:

```csv
P418_SITE_WIKI_COLONY_AND_WORKERS_CLUSTER.production.md
P461_PACKET_CUSTODY_BRIDGE.production.md
P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE.production.md
P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE.production.md
P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE.production.md
```

RS093 current production packets: P461, P462, P463, P464. P418 remains in the directory and was included because the owned scope is `production_packets/*.md`.

## Production Marker Counts

Command:

```powershell
$markers = [ordered]@{
  'U+00C2'=[char]0x00C2; 'U+00C3'=[char]0x00C3; 'U+00D0'=[char]0x00D0; 'U+00D1'=[char]0x00D1; 'U+00D8'=[char]0x00D8;
  'U+00E2'=[char]0x00E2; 'U+00E6'=[char]0x00E6; 'U+00EC'=[char]0x00EC; 'U+00D7'=[char]0x00D7; 'U+FFFD'=[char]0xFFFD
}
Get-ChildItem -LiteralPath 'Docs/Lore/AppliedContent/production_packets' -Filter '*.md' -File | Sort-Object Name | ForEach-Object {
  $text = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8)
  $row = [ordered]@{ File = $_.Name; LastWriteUtc=$_.LastWriteTimeUtc.ToString('yyyy-MM-ddTHH:mm:ssZ') }
  foreach ($key in $markers.Keys) {
    $needle = [string]$markers[$key]
    $row[$key] = ([regex]::Matches($text, [regex]::Escape($needle))).Count
  }
  [pscustomobject]$row
} | ConvertTo-Csv -NoTypeInformation
```

Output:

```csv
"File","LastWriteUtc","U+00C2","U+00C3","U+00D0","U+00D1","U+00D8","U+00E2","U+00E6","U+00EC","U+00D7","U+FFFD"
"P418_SITE_WIKI_COLONY_AND_WORKERS_CLUSTER.production.md","2026-06-03T17:44:43Z","0","0","0","0","0","0","0","0","0","0"
"P461_PACKET_CUSTODY_BRIDGE.production.md","2026-06-04T22:33:10Z","0","0","0","0","0","0","0","0","0","0"
"P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE.production.md","2026-06-04T22:45:59Z","0","0","0","0","0","0","0","0","0","0"
"P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE.production.md","2026-06-04T22:56:56Z","0","0","0","0","0","0","0","0","0","0"
"P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE.production.md","2026-06-04T23:00:41Z","0","0","0","0","0","0","0","0","0","0"
```

Result: current production packet snapshot is clean for the required seven markers and the three additional review markers.

Concurrency note: an intermediate scan during concurrent packet work showed one `U+00C3` hit in P464. A later scan after the file's current last-write timestamp showed zero hits. Worker 3204 did not edit packet content. Final evidence uses the current static snapshot above.

## RS093 Header And Required Marker Check

Command:

```powershell
& {
$packets = @(
'Docs/Lore/AppliedContent/production_packets/P461_PACKET_CUSTODY_BRIDGE.production.md',
'Docs/Lore/AppliedContent/production_packets/P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE.production.md',
'Docs/Lore/AppliedContent/production_packets/P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE.production.md',
'Docs/Lore/AppliedContent/production_packets/P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE.production.md'
)
$required = '(en_US|ar_SA|de_DE|es_ES|fr_FR|he_IL|id_ID|ja_JP|ko_KR|nl_NL|pl_PL|pt_BR|ru_RU|uk_UA|zh_CN)'
$badMarkers = [ordered]@{'U+00C3'=[char]0x00C3;'U+00D0'=[char]0x00D0;'U+00D8'=[char]0x00D8;'U+00E6'=[char]0x00E6;'U+00EC'=[char]0x00EC;'U+00D7'=[char]0x00D7;'U+FFFD'=[char]0xFFFD}
foreach ($p in $packets) {
  $file = Get-Item -LiteralPath $p
  $text = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
  $row = [ordered]@{File=$file.Name; LocaleHeaders=([regex]::Matches($text, "(?m)^###\s+$required\s*$")).Count}
  foreach ($key in $badMarkers.Keys) { $row[$key] = ([regex]::Matches($text, [regex]::Escape([string]$badMarkers[$key]))).Count }
  [pscustomobject]$row
}
} | ConvertTo-Csv -NoTypeInformation
```

Output:

```csv
"File","LocaleHeaders","U+00C3","U+00D0","U+00D8","U+00E6","U+00EC","U+00D7","U+FFFD"
"P461_PACKET_CUSTODY_BRIDGE.production.md","15","0","0","0","0","0","0","0"
"P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE.production.md","15","0","0","0","0","0","0","0"
"P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE.production.md","15","0","0","0","0","0","0","0"
"P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE.production.md","15","0","0","0","0","0","0","0"
```

Result: RS093 P461-P464 each have 15 required locale headers and zero required bad-codepoint hits.

## P463 Row Check

Command:

```powershell
$path = 'Docs/Lore/AppliedContent/production_packets/P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE.production.md'
$text = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $path), [Text.Encoding]::UTF8)
[pscustomobject]@{
  LocaleHeaders = ([regex]::Matches($text, '(?m)^###\s+[a-z]{2}_[A-Z]{2}\s*$')).Count
  SourceAuthorityRows = ([regex]::Matches($text, '(?m)^Status:\s*source_authority\.\s*$')).Count
  DraftMachineOrLlmRows = ([regex]::Matches($text, '(?m)^Status:\s*draft_machine_or_llm\.\s*$')).Count
  RequiredLocaleHeaders = (([regex]::Matches($text, '(?m)^###\s+(en_US|ar_SA|de_DE|es_ES|fr_FR|he_IL|id_ID|ja_JP|ko_KR|nl_NL|pl_PL|pt_BR|ru_RU|uk_UA|zh_CN)\s*$')).Count)
} | ConvertTo-Csv -NoTypeInformation
```

Output:

```csv
"LocaleHeaders","SourceAuthorityRows","DraftMachineOrLlmRows","RequiredLocaleHeaders"
"15","1","14","15"
```

Codepoint sanity sample for P463 `ru_RU` title:

```csv
"Sample","First24Codepoints"
"P463 ru_RU title","U+0054 U+0069 U+0074 U+006C U+0065 U+003A U+0020 U+0421 U+043F U+043E U+0439 U+043B U+0435 U+0440 U+043D U+044B U+0439 U+0020 U+0448 U+043B U+044E U+0437"
```

Result: plain console output can visually mojibake non-English text. Codepoint scan shows real Cyrillic codepoints in this sample, not `U+00D0`/`U+00D1` mojibake characters.

## Generated Page Sample Inventory

Command:

```powershell
$roots = @('Docs/Lore/AppliedContent/in_game_wiki','Docs/Lore/AppliedContent/external_site')
$files = foreach ($root in $roots) {
  Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.md' | Where-Object { $_.Name -match '^P(456|457|458|459|460)_' }
}
$files | ForEach-Object {
  $rel = $_.FullName.Substring((Get-Location).Path.Length + 1)
  $parts = $rel -split '[\\/]'
  [pscustomobject]@{ Surface=$parts[3]; Locale=$parts[4]; File=$_.Name }
} | Group-Object Surface,Locale | Sort-Object Name | ForEach-Object {
  [pscustomobject]@{ Surface=($_.Group[0].Surface); Locale=($_.Group[0].Locale); Count=$_.Count }
} | ConvertTo-Csv -NoTypeInformation
```

Concise result:

```text
external_site: 15 locales x 5 files = 75 files.
in_game_wiki: 15 locales x 5 files = 75 files.
Total sampled generated pages: 150.
```

Files sampled:

- `P456_SITE_HOME_LONGFORM_BRIEF.md`
- `P457_AEGIR_HARD_SCIFI_LONGFORM_BRIEF.md`
- `P458_DEEP_REACH_LIABILITY_LONGFORM_BRIEF.md`
- `P459_ATLAS_SPOILER_LONGFORM_BRIEF.md`
- `P460_BLUE_DEBT_RESOURCE_LONGFORM_BRIEF.md`

Locales sampled on both surfaces:

- `en_US`
- `ar_SA`
- `de_DE`
- `es_ES`
- `fr_FR`
- `he_IL`
- `id_ID`
- `ja_JP`
- `ko_KR`
- `nl_NL`
- `pl_PL`
- `pt_BR`
- `ru_RU`
- `uk_UA`
- `zh_CN`

## Generated Page Marker Counts

Command:

```powershell
& {
$markers = [ordered]@{
  'U+00C2'=[char]0x00C2; 'U+00C3'=[char]0x00C3; 'U+00D0'=[char]0x00D0; 'U+00D1'=[char]0x00D1; 'U+00D8'=[char]0x00D8;
  'U+00E2'=[char]0x00E2; 'U+00E6'=[char]0x00E6; 'U+00EC'=[char]0x00EC; 'U+00D7'=[char]0x00D7; 'U+FFFD'=[char]0xFFFD
}
$files = Get-ChildItem -LiteralPath 'Docs/Lore/AppliedContent/in_game_wiki','Docs/Lore/AppliedContent/external_site' -Recurse -File -Filter '*.md' | Where-Object { $_.Name -match '^P(456|457|458|459|460)_' }
foreach ($root in @('external_site','in_game_wiki')) {
  $subset = $files | Where-Object { $_.FullName -match [regex]::Escape("Docs\Lore\AppliedContent\$root") }
  $row = [ordered]@{ Surface=$root; Files=$subset.Count }
  foreach ($key in $markers.Keys) { $row[$key] = 0 }
  foreach ($file in $subset) {
    $text = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
    foreach ($key in $markers.Keys) {
      $needle = [string]$markers[$key]
      $row[$key] += ([regex]::Matches($text, [regex]::Escape($needle))).Count
    }
  }
  [pscustomobject]$row
}
} | ConvertTo-Csv -NoTypeInformation
```

Output:

```csv
"Surface","Files","U+00C2","U+00C3","U+00D0","U+00D1","U+00D8","U+00E2","U+00E6","U+00EC","U+00D7","U+FFFD"
"external_site","75","0","0","0","0","0","0","0","0","0","0"
"in_game_wiki","75","0","0","0","0","0","0","0","0","0","0"
```

Result: sampled generated pages are clean for the marker set. This does not prove native localization.

## Generated Page Status Counts

Command:

```powershell
$files = Get-ChildItem -LiteralPath 'Docs/Lore/AppliedContent/in_game_wiki','Docs/Lore/AppliedContent/external_site' -Recurse -File -Filter '*.md' | Where-Object { $_.Name -match '^P(456|457|458|459|460)_' }
$files | ForEach-Object {
  $rel = $_.FullName.Substring((Get-Location).Path.Length + 1)
  $parts = $rel -split '[\\/]'
  $text = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8)
  $status = if ($text -match '(?m)^localization_status:\s*(.+?)\s*$') { $Matches[1] } else { 'MISSING' }
  [pscustomobject]@{Surface=$parts[3]; Locale=$parts[4]; Status=$status}
} | Group-Object Surface,Status | Sort-Object Name | ForEach-Object {
  [pscustomobject]@{Surface=$_.Group[0].Surface; Status=$_.Group[0].Status; Count=$_.Count}
} | ConvertTo-Csv -NoTypeInformation
```

Output:

```csv
"Surface","Status","Count"
"external_site","draft_native_pass_pending","70"
"external_site","source_ready","5"
"in_game_wiki","draft_native_pass_pending","70"
"in_game_wiki","source_ready","5"
```

Result: `en_US` rows are `source_ready`; all 14 non-English locale rows per packet/surface are `draft_native_pass_pending`.

## English-Clone Risk Check

Method:

- Remove frontmatter.
- Remove generated HTML comments because they embed locale identifiers and caused false negative body mismatches.
- Normalize whitespace.
- Compare title and body of each non-English page to `en_US` for the same surface and packet.

Command:

```powershell
function Get-PageRecord([IO.FileInfo]$file) {
  $rel = $file.FullName.Substring((Get-Location).Path.Length + 1)
  $parts = $rel -split '[\\/]'
  $text = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
  $status = if ($text -match '(?m)^localization_status:\s*(.+?)\s*$') { $Matches[1] } else { '' }
  $content = if ($text -match '(?s)^---\s*\r?\n.*?\r?\n---\s*\r?\n(?<content>.*)$') { $Matches['content'] } else { $text }
  $content = [regex]::Replace($content, '(?s)<!--.*?-->', '')
  $title = if ($content -match '(?m)^#\s+(.+?)\s*$') { $Matches[1] } else { '' }
  $body = ($content -replace '(?m)^#\s+.+?\s*$','') -replace '\s+',' '
  [pscustomobject]@{Surface=$parts[3]; Locale=$parts[4]; Packet=($file.Name -replace '\.md$',''); Status=$status; TitleNorm=($title -replace '\s+',' ').Trim(); BodyNorm=$body.Trim()}
}
$files = Get-ChildItem -LiteralPath 'Docs/Lore/AppliedContent/in_game_wiki','Docs/Lore/AppliedContent/external_site' -Recurse -File -Filter '*.md' | Where-Object { $_.Name -match '^P(456|457|458|459|460)_' }
$records = $files | ForEach-Object { Get-PageRecord $_ }
$en = @{}; foreach ($r in $records | Where-Object Locale -eq 'en_US') { $en["$($r.Surface)|$($r.Packet)"] = $r }
$comparisons = foreach ($r in $records | Where-Object Locale -ne 'en_US') {
  $base = $en["$($r.Surface)|$($r.Packet)"]
  [pscustomobject]@{Surface=$r.Surface; Locale=$r.Locale; Packet=$r.Packet; Status=$r.Status; TitleExact=($r.TitleNorm -ceq $base.TitleNorm); BodyExact=($r.BodyNorm -ceq $base.BodyNorm)}
}
$comparisons | Group-Object Surface,Locale | Sort-Object Name | ForEach-Object {
  $g = $_.Group
  [pscustomobject]@{Surface=$g[0].Surface; Locale=$g[0].Locale; Files=$g.Count; TitleExact=($g | Where-Object TitleExact).Count; BodyExact=($g | Where-Object BodyExact).Count; BothExact=($g | Where-Object { $_.TitleExact -and $_.BodyExact }).Count; DraftPending=($g | Where-Object { $_.Status -eq 'draft_native_pass_pending' }).Count}
} | ConvertTo-Csv -NoTypeInformation
```

Output:

```csv
"Surface","Locale","Files","TitleExact","BodyExact","BothExact","DraftPending"
"external_site","ar_SA","5","5","5","5","5"
"external_site","de_DE","5","5","5","5","5"
"external_site","es_ES","5","5","5","5","5"
"external_site","fr_FR","5","5","5","5","5"
"external_site","he_IL","5","5","5","5","5"
"external_site","id_ID","5","5","5","5","5"
"external_site","ja_JP","5","5","5","5","5"
"external_site","ko_KR","5","5","5","5","5"
"external_site","nl_NL","5","5","5","5","5"
"external_site","pl_PL","5","5","5","5","5"
"external_site","pt_BR","5","5","5","5","5"
"external_site","ru_RU","5","5","5","5","5"
"external_site","uk_UA","5","5","5","5","5"
"external_site","zh_CN","5","5","5","5","5"
"in_game_wiki","ar_SA","5","5","5","5","5"
"in_game_wiki","de_DE","5","5","5","5","5"
"in_game_wiki","es_ES","5","5","5","5","5"
"in_game_wiki","fr_FR","5","5","5","5","5"
"in_game_wiki","he_IL","5","5","5","5","5"
"in_game_wiki","id_ID","5","5","5","5","5"
"in_game_wiki","ja_JP","5","5","5","5","5"
"in_game_wiki","ko_KR","5","5","5","5","5"
"in_game_wiki","nl_NL","5","5","5","5","5"
"in_game_wiki","pl_PL","5","5","5","5","5"
"in_game_wiki","pt_BR","5","5","5","5","5"
"in_game_wiki","ru_RU","5","5","5","5","5"
"in_game_wiki","uk_UA","5","5","5","5","5"
"in_game_wiki","zh_CN","5","5","5","5","5"
```

Clone-risk summary:

- Non-English comparisons: 140.
- Exact title+body clones versus `en_US`: 140.
- Non-English files marked `draft_native_pass_pending`: 140.
- Risk state: high English-clone risk for generated non-English pages P456-P460 on both surfaces.
- This is not text corruption. It is publication/localization readiness risk.

## False-Positive Caveats

Do not delete or rewrite content based only on broad marker hits.

- `U+FFFD`: fail. It means replacement-character data loss in UTF-8 decode.
- `U+00C3`, `U+00C2`, `U+00E2`: warning/manual review unless found in exact mojibake sequences. These can be legitimate Latin characters in some locales.
- `U+00D0`, `U+00D1`: warning/manual review in broad scan. They are common Cyrillic UTF-8 mojibake stems when displayed as Latin-1, but real Cyrillic text uses `U+04xx` codepoints, not these markers.
- `U+00D8`, `U+00E6`, `U+00EC`, `U+00D7`: warning/manual review. They can be legitimate symbols/letters in valid localized text.
- Console display is not enough. Use `.NET UTF8 ReadAllText` and codepoint dumps for proof.
- CJK, Arabic, Hebrew, Cyrillic, and Latin-extended pages require script-aware validation. A broad non-ASCII grep is invalid.

## Recommended Future Validator

Fail:

- Any `U+FFFD` in production packets, source CSV, generated pages, or export rows.
- Exact known mojibake sequences in any source-authority or public/export row after script-aware check, including common UTF-8-as-Latin-1 punctuation patterns.
- Missing required locale headers in production AppliedContent packets.
- Non-English generated page marked publication-ready/native/runtime-ready while title+body match `en_US` exactly.

Warn:

- Broad single-codepoint marker hits for `U+00C2`, `U+00C3`, `U+00D0`, `U+00D1`, `U+00D8`, `U+00E2`, `U+00E6`, `U+00EC`, `U+00D7`.
- Any non-English generated page with `draft_native_pass_pending` and exact English clone body/title.
- Any output where console display appears mojibaked but codepoint scan is clean; require codepoint proof before action.

Sample manual review:

- Valid localized rows with Latin extended, Cyrillic, Hebrew, Arabic, or CJK scripts.
- Rows where marker hit occurs in a locale where the character may be valid.
- Generated publication pages where metadata/status says draft but content is English clone.

Do not claim:

- Native localization.
- Runtime readiness.
- Unity placement.
- DataMonolith/h8bin readiness.
- Publication deployment.

## Next Owner Action

1. Add a static validator stage that emits per-file codepoint counts for the required marker set plus `U+00C2/U+00D1/U+00E2`.
2. Add script-aware row validation: compare non-English generated title/body to `en_US` after stripping frontmatter and generated comments.
3. Block publication/export status upgrades for non-English pages that are exact English clones.
4. Keep source-authority English rows separate from non-English draft/native/runtime statuses.
5. Re-run validator after any packet rewrite or generated page export.

## Result

Production packet corruption status: STATIC_SOURCE clean for current P418/P461/P462/P463/P464 marker scan.

RS093 packet status: P461/P462/P463/P464 each have 15 required locale headers and zero required bad-codepoint hits.

Generated page clone-risk status: STATIC_SOURCE high risk. P456-P460 non-English generated pages are exact English clones across both `external_site` and `in_game_wiki`, correctly still marked `draft_native_pass_pending`.

Verification state: STATIC_SOURCE_REVIEWED for source text inspected by commands above. Runtime/native/publication readiness remains PENDING VERIFICATION.

## Controller Addendum After 3201 Export Update

3201 later replaced generated publication status vocabulary:

- `source_ready` -> `source_authority`
- `draft_native_pass_pending` -> `draft_machine_or_llm`

The clone-risk finding remains valid. Controller spot-check after 3201:

- `Docs/Lore/AppliedContent/in_game_wiki/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` body still exactly matches `en_US` after stripping frontmatter, title, generated comments, and whitespace.
- `Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` body still exactly matches `en_US` after the same normalization.

Interpretation: current non-English generated pages are honestly marked as machine/LLM draft after 3201, but many remain English clones and are not publication/native/runtime ready.
