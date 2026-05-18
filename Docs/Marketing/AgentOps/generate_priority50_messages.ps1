$ErrorActionPreference = "Stop"

$base = "C:\hades\Hecton8\Docs\Marketing"
$data = Join-Path $base "Data\PRIORITY_CREATOR_SHORTLIST_FROM_RAW_2026-05-18.csv"
$out = Join-Path $base "CreatorOutreach\PRIORITY_50_MESSAGE_DRAFTS_FROM_RAW.md"
$rows = Import-Csv -LiteralPath $data | Select-Object -First 50

function SubjectFor($segment) {
    switch ($segment) {
        "direct_underwater_survival" { return "HECTON-8 - darker single-player underwater survival for your audience" }
        "survival_route_risk" { return "HECTON-8 - survival route risk, pressure, and salvage" }
        "engineering_base_systems" { return "HECTON-8 - underwater survival where the base is a machine" }
        "abyss_horror_pressure" { return "HECTON-8 - deep-sea dread through instruments and pressure" }
        default { return "HECTON-8 - single-player deep-sea survival" }
    }
}

function AngleFor($segment) {
    switch ($segment) {
        "direct_underwater_survival" { return "underwater survival history" }
        "survival_route_risk" { return "survival decision-making and route risk" }
        "engineering_base_systems" { return "systems, base building, and machinery" }
        "abyss_horror_pressure" { return "horror tension, instruments, and hostile depth" }
        default { return "indie survival discovery" }
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Priority 50 Message Drafts From Raw Public Signals") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("Status: draft-from-public-index / must verify before sending") | Out-Null
$lines.Add("Generated: 2026-05-19") | Out-Null
$lines.Add("Public stance: single-player-first / no co-op promise") | Out-Null
$lines.Add("Runtime impact: none") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Boundary") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("These messages are not send-ready. They are first drafts generated from public index signals: source games, segment, country candidate, and repeated appearances. Before sending, verify the official channel, recent content, public contact route, brand safety, and a matching real HECTON-8 asset.") | Out-Null
$lines.Add("") | Out-Null

$i = 0
foreach ($row in $rows) {
    $i++
    $name = $row.channel_name
    $subject = SubjectFor $row.recommended_segment
    $angle = AngleFor $row.recommended_segment
    $games = $row.source_games

    $lines.Add("## $i. $name") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("- Public index profile: $($row.channel_profile_url)") | Out-Null
    $lines.Add("- Segment: $($row.recommended_segment)") | Out-Null
    $lines.Add("- Source games: $games") | Out-Null
    $lines.Add("- Country candidates: $($row.country_candidates)") | Out-Null
    $lines.Add('- Status: `RAW_PUBLIC_INDEX_NOT_CONTACT_READY`') | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("Subject: $subject") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add('```text') | Out-Null
    $lines.Add("Hi $name,") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("I found your channel while mapping public creator coverage around $games. Before any outreach this still needs manual verification, but the apparent fit is $angle.") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("HECTON-8 is a single-player-first underwater survival game about pressure, machinery, salvage, black-water exploration, and habitats that behave like survival infrastructure. This is not a co-op promise and not a `"Subnautica killer`" pitch.") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("The specific angle for your audience would be: $($row.personalized_pitch_stub)") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("Assets: [Steam/screenshots/clip/demo - TBD]") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("If this fits your current format, I can send a short press kit or demo when the playable slice is ready.") | Out-Null
    $lines.Add('```') | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("Verification notes:") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add('- Official contact route: `TODO`') | Out-Null
    $lines.Add('- Recent relevant upload: `TODO`') | Out-Null
    $lines.Add('- Matching HECTON-8 asset: `TODO`') | Out-Null
    $lines.Add('- Send / hold / reject: `TODO`') | Out-Null
    $lines.Add("") | Out-Null
}

$lines -join "`r`n" | Set-Content -LiteralPath $out -Encoding UTF8
Write-Output $out
