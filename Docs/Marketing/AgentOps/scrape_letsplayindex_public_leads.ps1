param(
    [string]$OutputDir = "C:\hades\Hecton8\Docs\Marketing\Data",
    [int]$MaxTopPage = 300
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Add-Type -AssemblyName System.Net.Http

function Decode-Html([string]$s) {
    if ($null -eq $s) { return "" }
    $x = [System.Net.WebUtility]::HtmlDecode($s)
    $x = [regex]::Replace($x, "<.*?>", "")
    $x = [regex]::Replace($x, "\s+", " ").Trim()
    return $x
}

function To-IntOrNull([string]$s) {
    if ([string]::IsNullOrWhiteSpace($s)) { return $null }
    $clean = ($s -replace "[^0-9]", "")
    if ([string]::IsNullOrWhiteSpace($clean)) { return $null }
    return [int64]$clean
}

function Get-PitchMeta([string[]]$games, [string[]]$segments) {
    $joined = (($games + $segments) -join " ").ToLowerInvariant()
    if ($joined -match "subnautica|underwater") {
        return @{
            Segment = "direct_underwater_survival"
            Angle = "Underwater survival audience already proven."
            Stub = "Your channel has already touched Subnautica/underwater survival. HECTON-8 should be pitched as a single-player darker industrial pressure/salvage survival angle, not as a clone or co-op promise."
            Risk = "May compare directly to Subnautica; send only when screenshots clearly differentiate NASA-punk machinery and black-water pressure."
        }
    }
    if ($joined -match "barotrauma|iron lung|still wakes|dredge|horror") {
        return @{
            Segment = "abyss_horror_pressure"
            Angle = "Abyssal horror, instruments, isolation, and hostile depth."
            Stub = "Your audience responds to horror/abyss tension. HECTON-8 should be pitched through instruments, sound, pressure, and black-water dread rather than creature spam or jump scares."
            Risk = "Needs a strong atmospheric demo; weak survival grind will underperform with horror viewers."
        }
    }
    if ($joined -match "space engineers|satisfactory|planet crafter|forever skies|abiotic|engineering|factory|base") {
        return @{
            Segment = "engineering_base_systems"
            Angle = "Machines, habitats, resource routing, and survival infrastructure."
            Stub = "Your audience values systems and construction. HECTON-8 should be pitched as habitat/base survival where pumps, oxygen, power, salvage, and pressure-rated machinery visibly matter."
            Risk = "Do not pitch before base/tool systems are visible; engineering audiences punish decorative fake systems."
        }
    }
    if ($joined -match "raft|forest|long dark|pacific|survival") {
        return @{
            Segment = "survival_route_risk"
            Angle = "Expedition risk, resource pressure, return planning, and fair failure."
            Stub = "Your audience watches survival decisions. HECTON-8 should be pitched through routes, scarcity, oxygen, pressure warnings, salvage risk, and the decision to return before the ocean wins."
            Risk = "Avoid lore-first pitch; survival viewers need the playable loop fast."
        }
    }
    return @{
        Segment = "general_indie_survival"
        Angle = "Atmospheric single-player survival discovery."
        Stub = "Your channel is a possible indie/survival lead. HECTON-8 should be pitched only after verifying current fit and matching one real screenshot or clip to the channel format."
        Risk = "Manual verification required before outreach."
    }
}

$games = @(
    @{Slug="subnautica-2018"; Game="Subnautica"; Segment="direct_underwater_survival"},
    @{Slug="subnautica-below-zero-2021"; Game="Subnautica Below Zero"; Segment="direct_underwater_survival"},
    @{Slug="barotrauma-2019"; Game="Barotrauma"; Segment="abyss_pressure_horror"},
    @{Slug="raft-2022"; Game="Raft"; Segment="ocean_survival_crafting"},
    @{Slug="forever-skies-2023"; Game="Forever Skies"; Segment="vehicle_base_survival"},
    @{Slug="pacific-drive-2024"; Game="Pacific Drive"; Segment="machine_survival_anomaly"},
    @{Slug="the-forest-2018"; Game="The Forest"; Segment="hostile_survival"},
    @{Slug="sons-of-the-forest-2023"; Game="Sons of the Forest"; Segment="hostile_survival"},
    @{Slug="the-long-dark-2017"; Game="The Long Dark"; Segment="hardcore_survival"},
    @{Slug="space-engineers-2013"; Game="Space Engineers"; Segment="engineering_base_systems"},
    @{Slug="satisfactory-2020"; Game="Satisfactory"; Segment="factory_engineering_systems"},
    @{Slug="abiotic-factor-2024"; Game="Abiotic Factor"; Segment="facility_survival_systems"},
    @{Slug="dredge-2023"; Game="DREDGE"; Segment="ocean_dread"}
)

$surfaces = @(
    @{Path="lets-play-channels"; Surface="latest_letsplay"},
    @{Path="most-lets-play-channel-views"; Surface="most_lp_views"},
    @{Path="review-channels"; Surface="review_channels"}
)

$pages = @("")
foreach ($n in 200,300,400,500) {
    if ($n -le $MaxTopPage) { $pages += "top-$n" }
}

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(20)
$client.DefaultRequestHeaders.UserAgent.ParseAdd("HECTON8-public-lead-research/1.0")

$raw = New-Object System.Collections.Generic.List[object]
$fetchLog = New-Object System.Collections.Generic.List[object]
$rowRegex = [regex]::new('<tr class="format_row highlight">(?<row>.*?)</tr>', [System.Text.RegularExpressions.RegexOptions]::Singleline)

foreach ($g in $games) {
    foreach ($s in $surfaces) {
        foreach ($p in $pages) {
            $url = "https://www.letsplayindex.com/games/$($g.Slug)/$($s.Path)"
            if ($p -ne "") { $url = "$url/$p" }
            $before = $raw.Count
            try {
                $html = $client.GetStringAsync($url).GetAwaiter().GetResult()
                foreach ($m in $rowRegex.Matches($html)) {
                    $row = $m.Groups["row"].Value
                    if ($row -notmatch "detail_list_video") { continue }
                    $chan = [regex]::Match($row, '<a href="(?<path>/channels/[0-9]+-[^"]+)">(?<name>.*?)</a>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
                    if (-not $chan.Success) { continue }
                    $video = [regex]::Match($row, '<td class="format_cell detail_list_video"><a href="(?<path>[^"]+)"[^>]*>(?<title>.*?)</a>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
                    $date = [regex]::Match($row, '<td class="format_cell detail_list_date"(?: title="(?<title>[^"]*)")?[^>]*>(?<rel>.*?)</td>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
                    $country = [regex]::Match($row, '<img src="/images/flags/16/[^"/]+\.png"[^>]*alt="(?<country>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline)
                    $metricMatches = [regex]::Matches($row, '<td class="format_cell detail_list_views"[^>]*>(?<value>.*?)</td>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
                    $primaryMetric = ""
                    if ($metricMatches.Count -gt 0) { $primaryMetric = Decode-Html $metricMatches[0].Groups["value"].Value }
                    $duration = ""
                    if ($metricMatches.Count -gt 1) { $duration = Decode-Html $metricMatches[$metricMatches.Count - 1].Groups["value"].Value }
                    $raw.Add([pscustomobject]@{
                        scraped_utc = (Get-Date).ToUniversalTime().ToString("s") + "Z"
                        source_game = $g.Game
                        source_slug = $g.Slug
                        source_segment = $g.Segment
                        source_surface = $s.Surface
                        source_page = if ($p -eq "") { "top-100" } else { $p }
                        source_url = $url
                        channel_name = Decode-Html $chan.Groups["name"].Value
                        channel_path = $chan.Groups["path"].Value
                        channel_profile_url = "https://www.letsplayindex.com" + $chan.Groups["path"].Value
                        country = if ($country.Success) { Decode-Html $country.Groups["country"].Value } else { "UNKNOWN" }
                        primary_metric_raw = $primaryMetric
                        primary_metric_number = To-IntOrNull $primaryMetric
                        latest_or_ranked_video_title = if ($video.Success) { Decode-Html $video.Groups["title"].Value } else { "" }
                        latest_or_ranked_video_url = if ($video.Success) { "https://www.letsplayindex.com" + $video.Groups["path"].Value } else { "" }
                        published_or_rank_context = if ($date.Groups["title"].Value) { Decode-Html $date.Groups["title"].Value } else { Decode-Html $date.Groups["rel"].Value }
                        relative_context = Decode-Html $date.Groups["rel"].Value
                        duration = $duration
                        contact_route = "UNKNOWN_VERIFY_FROM_CREATOR_PAGE"
                        verification_status = "RAW_PUBLIC_INDEX_NOT_CONTACT_READY"
                    }) | Out-Null
                }
                $fetchLog.Add([pscustomobject]@{url=$url; status="OK"; rows=($raw.Count - $before); length=$html.Length}) | Out-Null
            } catch {
                $fetchLog.Add([pscustomobject]@{url=$url; status=("ERR " + $_.Exception.Message); rows=0; length=0}) | Out-Null
            }
            Start-Sleep -Milliseconds 100
        }
    }
}

$rawPath = Join-Path $OutputDir "RAW_PUBLIC_CREATOR_LEADS_2026-05-18.csv"
$raw | Sort-Object source_game, source_surface, source_page, channel_name | Export-Csv -LiteralPath $rawPath -NoTypeInformation -Encoding UTF8

$unique = $raw | Group-Object channel_path | ForEach-Object {
    $items = $_.Group
    $gamesList = @($items | Select-Object -ExpandProperty source_game -Unique | Sort-Object)
    $segmentsList = @($items | Select-Object -ExpandProperty source_segment -Unique | Sort-Object)
    $surfacesList = @($items | Select-Object -ExpandProperty source_surface -Unique | Sort-Object)
    $countries = @($items | Select-Object -ExpandProperty country -Unique | Where-Object { $_ -and $_ -ne "UNKNOWN" } | Sort-Object)
    $maxMetric = @($items | Where-Object { $null -ne $_.primary_metric_number } | ForEach-Object { $_.primary_metric_number } | Sort-Object -Descending | Select-Object -First 1)
    $meta = Get-PitchMeta $gamesList $segmentsList
    [pscustomobject]@{
        channel_name = ($items | Select-Object -First 1).channel_name
        channel_profile_url = ($items | Select-Object -First 1).channel_profile_url
        country_candidates = if ($countries.Count -gt 0) { $countries -join "; " } else { "UNKNOWN" }
        source_games = $gamesList -join "; "
        source_segments = $segmentsList -join "; "
        surfaces_seen = $surfacesList -join "; "
        raw_occurrences = $items.Count
        max_public_metric_seen = if ($maxMetric.Count -gt 0) { $maxMetric[0] } else { "" }
        recommended_segment = $meta.Segment
        pitch_angle = $meta.Angle
        personalized_pitch_stub = $meta.Stub
        risk_notes = $meta.Risk
        contact_route = "UNKNOWN_VERIFY_FROM_CREATOR_PAGE"
        verification_status = "RAW_PUBLIC_INDEX_NOT_CONTACT_READY"
        next_action = "Verify current channel activity, exact YouTube/Twitch URL, public business contact, language, brand safety, and whether HECTON-8 has a matching real asset."
    }
} | Sort-Object recommended_segment, channel_name

$uniquePath = Join-Path $OutputDir "UNIQUE_CREATOR_VERIFICATION_QUEUE_2026-05-18.csv"
$unique | Export-Csv -LiteralPath $uniquePath -NoTypeInformation -Encoding UTF8

$fetchPath = Join-Path $OutputDir "RAW_LEAD_FETCH_LOG_2026-05-18.csv"
$fetchLog | Export-Csv -LiteralPath $fetchPath -NoTypeInformation -Encoding UTF8

$summaryPath = Join-Path $OutputDir "RAW_LEAD_SCRAPE_SUMMARY_2026-05-18.md"
$topGames = $raw | Group-Object source_game | Sort-Object Count -Descending | Select-Object Name,Count
$topSegments = $unique | Group-Object recommended_segment | Sort-Object Count -Descending | Select-Object Name,Count
$topCountries = $unique | Group-Object country_candidates | Sort-Object Count -Descending | Select-Object -First 20 Name,Count
$sample = $unique | Sort-Object @{Expression="raw_occurrences";Descending=$true}, @{Expression="max_public_metric_seen";Descending=$true} | Select-Object -First 50
$md = New-Object System.Collections.Generic.List[string]
$md.Add("# Raw Lead Scrape Summary - 2026-05-18") | Out-Null
$md.Add("") | Out-Null
$md.Add("Status: raw public index extraction / not outreach-ready") | Out-Null
$md.Add("Public stance: single-player-first / no co-op promise") | Out-Null
$md.Add("Runtime impact: none") | Out-Null
$md.Add("") | Out-Null
$md.Add("## Scope") | Out-Null
$md.Add("") | Out-Null
$md.Add("This pass extracted public creator leads from LetsPlayIndex game/category pages for Subnautica and adjacent survival/horror/engineering games. It does not prove contact consent, current activity, sponsorship availability, language, or brand safety. Every row must be verified before outreach.") | Out-Null
$md.Add("") | Out-Null
$md.Add("## Outputs") | Out-Null
$md.Add("") | Out-Null
$md.Add("- Raw rows: ``Data/RAW_PUBLIC_CREATOR_LEADS_2026-05-18.csv`` ($($raw.Count) rows).") | Out-Null
$md.Add("- Unique verification queue: ``Data/UNIQUE_CREATOR_VERIFICATION_QUEUE_2026-05-18.csv`` ($(@($unique).Count) unique channel profiles).") | Out-Null
$md.Add("- Fetch log: ``Data/RAW_LEAD_FETCH_LOG_2026-05-18.csv``.") | Out-Null
$md.Add("") | Out-Null
$md.Add("## Raw Rows By Source Game") | Out-Null
$md.Add("") | Out-Null
$md.Add("| Source game | Rows |") | Out-Null
$md.Add("|---|---:|") | Out-Null
foreach($x in $topGames){ $md.Add("| $($x.Name) | $($x.Count) |") | Out-Null }
$md.Add("") | Out-Null
$md.Add("## Unique Queue By Recommended Segment") | Out-Null
$md.Add("") | Out-Null
$md.Add("| Segment | Unique leads |") | Out-Null
$md.Add("|---|---:|") | Out-Null
foreach($x in $topSegments){ $md.Add("| $($x.Name) | $($x.Count) |") | Out-Null }
$md.Add("") | Out-Null
$md.Add("## Top Country Candidate Buckets") | Out-Null
$md.Add("") | Out-Null
$md.Add("| Country candidate bucket | Unique leads |") | Out-Null
$md.Add("|---|---:|") | Out-Null
foreach($x in $topCountries){ $md.Add("| $($x.Name) | $($x.Count) |") | Out-Null }
$md.Add("") | Out-Null
$md.Add("## Top 50 Verification Priority Sample") | Out-Null
$md.Add("") | Out-Null
$md.Add("| Channel | Segment | Games | Occurrences | Metric | Pitch angle |") | Out-Null
$md.Add("|---|---|---|---:|---:|---|") | Out-Null
foreach($x in $sample){
    $safeName = ($x.channel_name -replace "\|","/")
    $safeGames = ($x.source_games -replace "\|","/")
    $safeAngle = ($x.pitch_angle -replace "\|","/")
    $md.Add("| [$safeName]($($x.channel_profile_url)) | $($x.recommended_segment) | $safeGames | $($x.raw_occurrences) | $($x.max_public_metric_seen) | $safeAngle |") | Out-Null
}
$md.Add("") | Out-Null
$md.Add("## Verification Gate") | Out-Null
$md.Add("") | Out-Null
$md.Add("A lead becomes outreach-ready only after: current channel activity checked, official YouTube/Twitch/site route found, language confirmed, fit confirmed, recent content reviewed, contact route logged, key-scam risk checked, pitch personalized to one real HECTON-8 asset, and no co-op/performance/competitor-war claims remain.") | Out-Null
$md.Add("") | Out-Null
$md.Add("## Sources") | Out-Null
$md.Add("") | Out-Null
$md.Add("- LetsPlayIndex Subnautica pages: https://www.letsplayindex.com/games/subnautica-2018/lets-play-channels") | Out-Null
$md.Add("- LetsPlayIndex category pages for Subnautica Below Zero, Barotrauma, Raft, Forever Skies, Pacific Drive, The Forest, Sons of the Forest, The Long Dark, Space Engineers, Satisfactory, Abiotic Factor, and DREDGE.") | Out-Null
$md -join "`r`n" | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Output "RAW=$($raw.Count)"
Write-Output "UNIQUE=$(@($unique).Count)"
Write-Output "RAW_PATH=$rawPath"
Write-Output "UNIQUE_PATH=$uniquePath"
Write-Output "SUMMARY_PATH=$summaryPath"
Write-Output "FETCH_LOG=$fetchPath"
