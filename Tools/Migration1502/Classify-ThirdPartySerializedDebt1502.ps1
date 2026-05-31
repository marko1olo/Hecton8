param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$ProjectLedgerPath = "Docs/AgentLogs/YamlDesync_1502_ProjectLedger.json",
    [string]$ReportPath = "Docs/AgentLogs/YamlThirdPartySerializedDebt_1502.json"
)

$ErrorActionPreference = "Stop"

function Resolve-UnderRoot {
    param(
        [string]$Root,
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($Root, $Path))
}

function Convert-ToProjectRelative {
    param(
        [string]$ProjectRoot,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $fullRoot = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($fullRoot.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar).Replace("\", "/")
    }

    return $Path.Replace("\", "/")
}

function Get-AssetRoot {
    param([string]$RelativePath)

    $path = $RelativePath.Replace("\", "/")
    if ($path.StartsWith("Assets/_Project/", [StringComparison]::Ordinal)) {
        return "Assets/_Project"
    }
    if ($path.StartsWith("Assets/Crest/", [StringComparison]::Ordinal)) {
        return "Assets/Crest"
    }
    if ($path.StartsWith("Assets/MapMagic/", [StringComparison]::Ordinal)) {
        return "Assets/MapMagic"
    }
    if ($path.StartsWith("Assets/VolumetricLightBeam/", [StringComparison]::Ordinal)) {
        return "Assets/VolumetricLightBeam"
    }
    if ($path.StartsWith("Packages/", [StringComparison]::Ordinal)) {
        return "Packages"
    }
    return "Other"
}

function Get-ScriptOwner {
    param([string]$ScriptPath)

    $path = $ScriptPath.Replace("\", "/")
    if ($path.StartsWith("Assets/_Project/", [StringComparison]::Ordinal)) {
        return "FIRST_PARTY"
    }
    if ($path.StartsWith("Assets/Crest/", [StringComparison]::Ordinal)) {
        return "THIRD_PARTY_CREST"
    }
    if ($path.StartsWith("Assets/MapMagic/", [StringComparison]::Ordinal)) {
        return "THIRD_PARTY_MAPMAGIC"
    }
    if ($path.StartsWith("Assets/VolumetricLightBeam/", [StringComparison]::Ordinal)) {
        return "THIRD_PARTY_VOLUMETRIC_LIGHT_BEAM"
    }
    if ($path.StartsWith("Packages/", [StringComparison]::Ordinal)) {
        return "PACKAGE_OR_EXTERNAL"
    }
    return "UNCLASSIFIED_EXTERNAL"
}

function Get-MutationPolicy {
    param(
        [string]$ScriptOwner
    )

    if ($ScriptOwner -eq "FIRST_PARTY") {
        return "ELIGIBLE_ONLY_WITH_FIELD_SCHEMA_PROOF"
    }

    if ($ScriptOwner.StartsWith("THIRD_PARTY_", [StringComparison]::Ordinal) -or $ScriptOwner -eq "PACKAGE_OR_EXTERNAL") {
        return "BLOCKED_THIRD_PARTY_ASSET_INTEGRITY"
    }

    return "READ_ONLY_UNCLASSIFIED_OWNER"
}

function Add-Count {
    param(
        [hashtable]$Map,
        [string]$Key,
        [int]$Delta = 1
    )

    if ([string]::IsNullOrWhiteSpace($Key)) {
        $Key = "<empty>"
    }

    if (!$Map.ContainsKey($Key)) {
        $Map[$Key] = 0
    }

    $Map[$Key] += $Delta
}

function Convert-MapToRows {
    param(
        [hashtable]$Map,
        [string]$KeyName
    )

    return @(
        $Map.GetEnumerator() |
            Sort-Object -Property Value, Name -Descending |
            ForEach-Object {
                [pscustomobject]@{
                    $KeyName = $_.Key
                    count = [int]$_.Value
                }
            }
    )
}

$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$ledgerFull = Resolve-UnderRoot $projectFull $ProjectLedgerPath
$reportFull = Resolve-UnderRoot $projectFull $ReportPath

if (!(Test-Path -LiteralPath $ledgerFull -PathType Leaf)) {
    throw "Project ledger missing: $ledgerFull"
}

$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()
$ledger = Get-Content -Raw -LiteralPath $ledgerFull | ConvertFrom-Json
$records = [System.Collections.Generic.List[object]]::new()
$byScript = @{}
$byScriptOwner = @{}
$byAssetRoot = @{}
$byPolicy = @{}
$byProperty = @{}
$byAssetAndScriptOwner = @{}
$firstPartyCount = 0
$knownThirdPartyCount = 0
$unclassifiedCount = 0

foreach ($file in @($ledger.files)) {
    foreach ($hit in @($file.orphanedSerializedProperties)) {
        if ($null -eq $hit) {
            continue
        }

        $assetRelative = Convert-ToProjectRelative $projectFull ([string]$hit.path)
        $scriptPath = ([string]$hit.scriptPath).Replace("\", "/")
        $scriptOwner = Get-ScriptOwner $scriptPath
        $assetRoot = Get-AssetRoot $assetRelative
        $policy = Get-MutationPolicy $scriptOwner
        $property = [string]$hit.property

        if ($scriptOwner -eq "FIRST_PARTY") {
            $firstPartyCount++
        } elseif ($scriptOwner.StartsWith("THIRD_PARTY_", [StringComparison]::Ordinal) -or $scriptOwner -eq "PACKAGE_OR_EXTERNAL") {
            $knownThirdPartyCount++
        } else {
            $unclassifiedCount++
        }

        Add-Count $byScript $scriptPath
        Add-Count $byScriptOwner $scriptOwner
        Add-Count $byAssetRoot $assetRoot
        Add-Count $byPolicy $policy
        Add-Count $byProperty $property
        Add-Count $byAssetAndScriptOwner "$assetRoot -> $scriptOwner"

        [void]$records.Add([pscustomobject]@{
            assetPath = $assetRelative
            assetRoot = $assetRoot
            line = [int]$hit.line
            componentFileID = [string]$hit.componentFileID
            scriptGuid = [string]$hit.scriptGuid
            scriptPath = $scriptPath
            scriptClass = [string]$hit.scriptClass
            scriptOwner = $scriptOwner
            property = $property
            reason = [string]$hit.reason
            mutationPolicy = $policy
            riskClass = if ($scriptOwner -eq "FIRST_PARTY") { "FIRST_PARTY_SCHEMA_DRIFT" } elseif ($assetRoot -eq "Assets/_Project") { "PROJECT_ASSET_REFERENCES_THIRD_PARTY_SCHEMA_DRIFT" } else { "THIRD_PARTY_PACKAGE_SCHEMA_DRIFT" }
        })
    }
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$report = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_THIRD_PARTY_CLASSIFICATION"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    sourceLedger = $ProjectLedgerPath
    totalOrphanProperties = $records.Count
    firstPartyOrphanProperties = $firstPartyCount
    knownThirdPartyOrphanProperties = $knownThirdPartyCount
    unclassifiedExternalOrphanProperties = $unclassifiedCount
    mutationPolicy = "READ_ONLY_REPORT_ONLY"
    decision = "DO_NOT_MUTATE_THIRD_PARTY_SERIALIZED_METADATA_WITH_AGENT_1502"
    decisionReason = "Remaining orphan serialized properties are not owned by first-party scripts. Crest, MapMagic, and VolumetricLightBeam metadata must be handled by package-specific upgrade/import policy, not raw field deletion from this agent."
    byScriptOwner = Convert-MapToRows $byScriptOwner "scriptOwner"
    byMutationPolicy = Convert-MapToRows $byPolicy "mutationPolicy"
    byAssetRoot = Convert-MapToRows $byAssetRoot "assetRoot"
    byAssetRootAndScriptOwner = Convert-MapToRows $byAssetAndScriptOwner "route"
    byScript = Convert-MapToRows $byScript "scriptPath"
    byProperty = Convert-MapToRows $byProperty "property"
    records = @($records.ToArray())
    elapsedMicroseconds = $elapsedUs
}

$reportDir = [System.IO.Path]::GetDirectoryName($reportFull)
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFull -Encoding UTF8
$report
