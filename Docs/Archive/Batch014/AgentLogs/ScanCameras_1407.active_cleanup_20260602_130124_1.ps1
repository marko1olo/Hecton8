param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$OutputPath = "C:\hades\Hecton8\Docs\Reports\CameraLedger_1407.json"
)

$ErrorActionPreference = "Stop"

function Convert-ToProjectPath {
    param([string]$FullPath, [string]$Root)
    $resolvedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $resolvedPath = [System.IO.Path]::GetFullPath($FullPath)
    if ($resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $resolvedPath.Substring($resolvedRoot.Length + 1).Replace('\', '/')
    }
    return $resolvedPath.Replace('\', '/')
}

function Get-FieldValue {
    param([string]$Body, [string]$FieldName)
    $match = [regex]::Match($Body, "(?m)^\s*" + [regex]::Escape($FieldName) + "\s*:\s*(.+?)\s*$")
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }
    return $null
}

function Get-PPtrFileId {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }
    $match = [regex]::Match($Value, "fileID:\s*(-?\d+)")
    if ($match.Success) {
        return $match.Groups[1].Value
    }
    return $null
}

function Resolve-Role {
    param([string]$AssetPath, [string]$ObjectName, [string]$TargetTexture)
    $combined = $AssetPath + " " + $ObjectName
    if ($combined -eq $null) {
        $combined = ""
    }
    $haystack = $combined.ToLowerInvariant()
    if ($haystack.Contains("desktop") -or $haystack.Contains("flat")) { return "DesktopOnlyCandidate" }
    if ($haystack.Contains("cockpit") -or $haystack.Contains("submarine") -or $haystack.Contains("seamoth")) { return "Cockpit" }
    if ($haystack.Contains("terminal") -or $haystack.Contains("panel") -or $haystack.Contains("screen") -or $haystack.Contains("monitor")) { return "Terminal" }
    if ($haystack.Contains("minimap") -or $haystack.Contains("map") -or $haystack.Contains("radar") -or $haystack.Contains("sonar")) { return "DiegeticMapOrRadar" }
    if ($haystack.Contains("hud") -or $haystack.Contains("visor") -or $haystack.Contains("ui")) { return "HUDOrVisor" }
    if ($haystack.Contains("player") -or $haystack.Contains("main camera") -or $haystack.Contains("xr")) { return "PlayerXR" }
    if (-not [string]::IsNullOrWhiteSpace($TargetTexture) -and $TargetTexture -notmatch "fileID:\s*0") { return "RenderTextureCamera" }
    return "Unknown"
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$roots = @(
    (Join-Path $ProjectRoot "Assets\_Project\Scenes"),
    (Join-Path $ProjectRoot "Assets\_Project\Prefabs")
)

$files = New-Object System.Collections.Generic.List[System.IO.FileInfo]
foreach ($root in $roots) {
    if (Test-Path -LiteralPath $root) {
        Get-ChildItem -LiteralPath $root -Recurse -File |
            Where-Object { $_.Extension -eq ".unity" -or $_.Extension -eq ".prefab" } |
            ForEach-Object { $files.Add($_) }
    }
}

$cameras = New-Object System.Collections.Generic.List[object]
$parseNotes = New-Object System.Collections.Generic.List[object]
$documentsPattern = [regex]'(?ms)^--- !u!(\d+) &(-?\d+)\s*(.*?)(?=^--- !u!|\z)'

foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $prefixLength = [System.Math]::Min(64, $bytes.Length)
    $prefix = [System.Text.Encoding]::ASCII.GetString($bytes, 0, $prefixLength)
    $assetPath = Convert-ToProjectPath -FullPath $file.FullName -Root $ProjectRoot

    if (-not $prefix.StartsWith("%YAML", [System.StringComparison]::Ordinal)) {
        $parseNotes.Add([pscustomobject]@{
            path = $assetPath
            status = "BINARY_OR_NON_YAML"
            note = "Camera entries not trusted from raw text scan; requires Unity serialization API validation."
        })
        continue
    }

    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    $matches = $documentsPattern.Matches($text)
    $gameObjects = @{}
    $monoCameraData = @{}
    $cameraDocs = New-Object System.Collections.Generic.List[object]

    foreach ($match in $matches) {
        $typeId = $match.Groups[1].Value
        $fileId = $match.Groups[2].Value
        $body = $match.Groups[3].Value

        if ($typeId -eq "1") {
            $name = Get-FieldValue -Body $body -FieldName "m_Name"
            $gameObjects[$fileId] = $name
            continue
        }

        if ($typeId -eq "20") {
            $gameObject = Get-FieldValue -Body $body -FieldName "m_GameObject"
            $goFileId = Get-PPtrFileId -Value $gameObject
            $targetTexture = Get-FieldValue -Body $body -FieldName "m_TargetTexture"
            $cameraDocs.Add([pscustomobject]@{
                fileId = $fileId
                gameObjectFileId = $goFileId
                enabled = Get-FieldValue -Body $body -FieldName "m_Enabled"
                targetTexture = $targetTexture
                targetDisplay = Get-FieldValue -Body $body -FieldName "m_TargetDisplay"
                targetEye = Get-FieldValue -Body $body -FieldName "m_TargetEye"
                cullingMask = Get-FieldValue -Body $body -FieldName "m_CullingMask"
                depth = Get-FieldValue -Body $body -FieldName "m_Depth"
            })
            continue
        }

        if ($typeId -eq "114" -and $body.Contains("m_RendererIndex")) {
            $gameObject = Get-FieldValue -Body $body -FieldName "m_GameObject"
            $goFileId = Get-PPtrFileId -Value $gameObject
            if ($goFileId -ne $null) {
                $monoCameraData[$goFileId] = [pscustomobject]@{
                    rendererIndex = Get-FieldValue -Body $body -FieldName "m_RendererIndex"
                    renderPostProcessing = Get-FieldValue -Body $body -FieldName "m_RenderPostProcessing"
                    clearDepth = Get-FieldValue -Body $body -FieldName "m_ClearDepth"
                    allowXRRendering = Get-FieldValue -Body $body -FieldName "m_AllowXRRendering"
                    cameraType = Get-FieldValue -Body $body -FieldName "m_CameraType"
                    requiresDepthTexture = Get-FieldValue -Body $body -FieldName "m_RequiresDepthTexture"
                    requiresColorTexture = Get-FieldValue -Body $body -FieldName "m_RequiresColorTexture"
                }
            }
        }
    }

    foreach ($camera in $cameraDocs) {
        $objectName = $gameObjects[$camera.gameObjectFileId]
        $additionalData = $monoCameraData[$camera.gameObjectFileId]
        $cameras.Add([pscustomobject]@{
            path = $assetPath
            cameraFileId = $camera.fileId
            gameObjectFileId = $camera.gameObjectFileId
            name = $objectName
            targetDisplay = $camera.targetDisplay
            targetTexture = $camera.targetTexture
            targetTextureFileId = Get-PPtrFileId -Value $camera.targetTexture
            targetEye = $camera.targetEye
            cullingMask = $camera.cullingMask
            depth = $camera.depth
            rendererIndex = if ($additionalData -ne $null) { $additionalData.rendererIndex } else { $null }
            renderPostProcessing = if ($additionalData -ne $null) { $additionalData.renderPostProcessing } else { $null }
            clearDepth = if ($additionalData -ne $null) { $additionalData.clearDepth } else { $null }
            allowXRRendering = if ($additionalData -ne $null) { $additionalData.allowXRRendering } else { $null }
            cameraType = if ($additionalData -ne $null) { $additionalData.cameraType } else { $null }
            requiresDepthTexture = if ($additionalData -ne $null) { $additionalData.requiresDepthTexture } else { $null }
            requiresColorTexture = if ($additionalData -ne $null) { $additionalData.requiresColorTexture } else { $null }
            role = Resolve-Role -AssetPath $assetPath -ObjectName $objectName -TargetTexture $camera.targetTexture
        })
    }
}

$stopwatch.Stop()
$report = [pscustomobject]@{
    agent = "1407"
    generatedUtc = [System.DateTime]::UtcNow.ToString("O", [System.Globalization.CultureInfo]::InvariantCulture)
    scanMicroseconds = [int64]($stopwatch.Elapsed.TotalMilliseconds * 1000.0)
    searchedRoots = @("Assets/_Project/Scenes", "Assets/_Project/Prefabs")
    parsedCameraCount = $cameras.Count
    cameras = $cameras
    parseNotes = $parseNotes
}

$outputDirectory = [System.IO.Path]::GetDirectoryName($OutputPath)
if (-not [System.IO.Directory]::Exists($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$report | ConvertTo-Json -Depth 6
