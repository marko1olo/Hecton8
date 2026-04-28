$skillsDir = '.agents-skills'
$skillFiles = Get-ChildItem -Path $skillsDir -Filter '*.txt' -File | Select-Object -ExpandProperty Name

Write-Host "TOTAL MANDATES: $($skillFiles.Count)"

$searchRoots = @('Assets/_Project/Scripts','Docs','AGENTS.md')
$brokenLinks = New-Object System.Collections.ArrayList
$confirmedLinks = New-Object System.Collections.ArrayList

foreach ($skill in $skillFiles) {
    $baseName = $skill -replace '\.txt$',''
    $found = $false
    foreach ($root in $searchRoots) {
        if (Test-Path $root) {
            if ((Get-Item $root) -is [System.IO.DirectoryInfo]) {
                $files = Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue
                foreach ($f in $files) {
                    $content = Get-Content -Raw $f.FullName -ErrorAction SilentlyContinue
                    if ($content -and $content.Contains($baseName)) {
                        $found = $true
                        break
                    }
                }
            } else {
                $content = Get-Content -Raw $root -ErrorAction SilentlyContinue
                if ($content -and $content.Contains($baseName)) {
                    $found = $true
                }
            }
        }
        if ($found) { break }
    }
    if ($found) {
        $confirmedLinks.Add($skill) | Out-Null
    } else {
        $brokenLinks.Add($skill) | Out-Null
    }
}

Write-Host "`nCONFIRMED_LINKS: $($confirmedLinks.Count)"
$confirmedLinks | Sort-Object | ForEach-Object { Write-Host "  $_" }

Write-Host "`nBROKEN_ORPHAN_MANDATES: $($brokenLinks.Count)"
$brokenLinks | Sort-Object | ForEach-Object { Write-Host "  $_" }
