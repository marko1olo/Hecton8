Add-Type -AssemblyName System.Drawing
$texDir = 'Assets/_Project/Art/TEXTURES'
$extensions = @('*.png','*.jpg','*.jpeg','*.tga','*.psd','*.exr','*.bmp')
$files = New-Object System.Collections.ArrayList
foreach ($ext in $extensions) {
    $found = Get-ChildItem -Path $texDir -Recurse -Filter $ext -File -ErrorAction SilentlyContinue
    if ($found) {
        if ($found -is [array]) { $files.AddRange($found) | Out-Null } else { $files.Add($found) | Out-Null }
    }
}

$totalBytes = 0
$count = 0
$detail = New-Object System.Collections.Generic.List[object]

foreach ($file in $files) {
    $metaPath = $file.FullName + '.meta'
    if (-not (Test-Path $metaPath)) { continue }
    
    $meta = Get-Content -Raw $metaPath
    $maxSize = 2048
    if ($meta -match 'maxTextureSize:\s*(\d+)') { $maxSize = [int]$Matches[1] }
    $texFormat = -1
    if ($meta -match 'textureFormat:\s*(-?\d+)') { $texFormat = [int]$Matches[1] }
    $mipMap = 1
    if ($meta -match 'enableMipMap:\s*(\d)') { $mipMap = [int]$Matches[1] }
    $alpha = 0
    if ($meta -match 'alphaIsTransparency:\s*(\d)') { $alpha = [int]$Matches[1] }
    
    $origW = 0; $origH = 0
    try {
        $img = [System.Drawing.Image]::FromFile($file.FullName)
        $origW = $img.Width; $origH = $img.Height
        $img.Dispose()
    } catch { continue }
    
    $loadedW = [math]::Min($origW, $maxSize)
    $loadedH = [math]::Min($origH, $maxSize)
    
    $bpp = 0.5
    switch ($texFormat) {
        -1 { if ($alpha -eq 1) { $bpp = 1.0 } else { $bpp = 0.5 } }
        1  { if ($alpha -eq 1) { $bpp = 1.0 } else { $bpp = 0.5 } }
        10 { $bpp = 0.5 }
        12 { $bpp = 1.0 }
        28 { $bpp = 1.0 }
        30 { $bpp = 1.0 }
        3  { $bpp = 3.0 }
        4  { $bpp = 4.0 }
        default { if ($alpha -eq 1) { $bpp = 1.0 } else { $bpp = 0.5 } }
    }
    
    $mipFactor = if ($mipMap -eq 1) { 1.333 } else { 1.0 }
    $bytes = $loadedW * $loadedH * $bpp * $mipFactor
    $totalBytes += $bytes
    $count++
    
    $detail.Add([PSCustomObject]@{
        File = $file.FullName.Replace($PWD.Path + '\', '')
        OrigW = $origW; OrigH = $origH
        LoadedW = $loadedW; LoadedH = $loadedH
        Format = $texFormat; Alpha = $alpha; Mip = $mipMap
        Bpp = $bpp; Bytes = $bytes
    })
}

$totalMB = [math]::Round($totalBytes / 1MB, 2)
Write-Host "TOTAL_TEXTURES: $count"
Write-Host "TOTAL_VRAM_MB: $totalMB"

$top20 = $detail | Sort-Object Bytes -Descending | Select-Object -First 20
Write-Host "`n--- TOP 20 VRAM CONSUMERS ---"
$top20 | ForEach-Object { Write-Host ("{0:F2} MB - {1}x{2} fmt={3} mip={4} -> {5}" -f ($_.Bytes/1MB), $_.LoadedW, $_.LoadedH, $_.Format, $_.Mip, $_.File) }

$detail | Export-Csv -Path 'Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/vram_detail.csv' -NoTypeInformation -Encoding UTF8
Write-Host "`nDetail exported to vram_detail.csv"
