param(
    [string]$JsonPath = "Docs/AgentLogs/MsbuildInterceptionStaticProof_1400.json"
)

$ErrorActionPreference = "Stop"

function Convert-DelimitedSet {
    param([string]$Value)

    $items = New-Object "System.Collections.Generic.HashSet[string]"
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $items
    }

    foreach ($part in $Value.Split([char]'|')) {
        $trimmed = $part.Trim()
        if ($trimmed.Length -gt 0) {
            [void]$items.Add($trimmed)
        }
    }

    return $items
}

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$jsonFullPath = Join-Path $root $JsonPath
$jsonDirectory = Split-Path -Parent $jsonFullPath
if (-not (Test-Path -LiteralPath $jsonDirectory)) {
    New-Item -ItemType Directory -Path $jsonDirectory -Force | Out-Null
}

$targetsPath = Join-Path $root "Directory.Build.targets"
$targetsXml = New-Object System.Xml.XmlDocument
$targetsXml.Load($targetsPath)
$fixTargets = @($targetsXml.Project.Target | Where-Object { $_.Name -eq "FixUnityCircularReferences" })
$target = if ($fixTargets.Count -gt 0) { $fixTargets[0] } else { $null }

$propertyValue = ""
foreach ($group in @($targetsXml.Project.PropertyGroup)) {
    if ($null -ne $group.HectonUnityPackageCliShimProjects) {
        $propertyValue = [string]$group.HectonUnityPackageCliShimProjects
        break
    }
}

$shimProjects = Convert-DelimitedSet -Value $propertyValue
$solutionPath = Join-Path $root "Hecton8.slnx"
$activeSolutionProjectFullPaths = New-Object "System.Collections.Generic.HashSet[string]"
if (Test-Path -LiteralPath $solutionPath) {
    $solutionXml = New-Object System.Xml.XmlDocument
    $solutionXml.Load($solutionPath)
    foreach ($project in @($solutionXml.Solution.Project)) {
        $path = [string]$project.Path
        if (-not [string]::IsNullOrWhiteSpace($path)) {
            $fullPath = [System.IO.Path]::GetFullPath((Join-Path $root $path)).ToLowerInvariant()
            [void]$activeSolutionProjectFullPaths.Add($fullPath)
        }
    }
}

$expectedDlls = foreach ($projectName in $shimProjects) {
    $dllPath = Join-Path $root ("Library/ScriptAssemblies/" + $projectName + ".dll")
    [pscustomobject]@{
        projectName = $projectName
        dllPath = $dllPath
        exists = Test-Path -LiteralPath $dllPath
    }
}

$intercepted = New-Object "System.Collections.Generic.List[object]"
$parseFailures = New-Object "System.Collections.Generic.List[object]"
$activeParseFailures = New-Object "System.Collections.Generic.List[object]"
$excludedSyntheticProjects = New-Object "System.Collections.Generic.List[string]"

foreach ($projectFile in Get-ChildItem -LiteralPath $root -Recurse -Filter "*.csproj" -File) {
    $relativePath = $projectFile.FullName.Substring($root.Length + 1)
    if ($relativePath.StartsWith("Temp\Agent1400GraphFuzzer\", [System.StringComparison]::OrdinalIgnoreCase) -or
        $relativePath.StartsWith("Temp/Agent1400GraphFuzzer/", [System.StringComparison]::OrdinalIgnoreCase)) {
        $excludedSyntheticProjects.Add($relativePath)
        continue
    }

    $isActiveSolutionProject = $activeSolutionProjectFullPaths.Contains($projectFile.FullName.ToLowerInvariant())
    try {
        $projectXml = New-Object System.Xml.XmlDocument
        $projectXml.Load($projectFile.FullName)
    } catch {
        $failure = [pscustomobject]@{
            project = $relativePath
            activeSolutionProject = $isActiveSolutionProject
            error = $_.Exception.Message
        }
        $parseFailures.Add($failure)
        if ($isActiveSolutionProject) {
            $activeParseFailures.Add($failure)
        }
        continue
    }

    foreach ($reference in @($projectXml.Project.ItemGroup.ProjectReference)) {
        if ($null -eq $reference) {
            continue
        }

        $include = [string]$reference.Include
        if ([string]::IsNullOrWhiteSpace($include)) {
            continue
        }

        $fileName = [System.IO.Path]::GetFileNameWithoutExtension($include)
        if ($shimProjects.Contains($fileName)) {
            $dllPath = Join-Path $root ("Library/ScriptAssemblies/" + $fileName + ".dll")
            $intercepted.Add([pscustomobject]@{
                project = $relativePath
                activeSolutionProject = $isActiveSolutionProject
                projectReference = $include
                referenceFileName = $fileName
                replacementDll = $dllPath
                replacementDllExists = Test-Path -LiteralPath $dllPath
            })
        }
    }
}

$missingDlls = @($intercepted | Where-Object { -not $_.replacementDllExists })
$activeIntercepted = @($intercepted | Where-Object { $_.activeSolutionProject })
$passiveIntercepted = @($intercepted | Where-Object { -not $_.activeSolutionProject })
$activeMissingDlls = @($activeIntercepted | Where-Object { -not $_.replacementDllExists })
$beforeTargets = if ($null -ne $target) { $target.GetAttribute("BeforeTargets") } else { "" }
$targetReady = $null -ne $target -and $beforeTargets.Contains("ResolveProjectReferences")
$status = "GREEN_STATIC_INTERCEPTION_READY"
if (-not $targetReady) {
    $status = "TARGET_NOT_READY"
} elseif ($activeParseFailures.Count -gt 0) {
    $status = "ACTIVE_PROJECT_XML_PARSE_FAILURE"
} elseif ($activeMissingDlls.Count -gt 0) {
    $status = "ACTIVE_MISSING_REPLACEMENT_DLL"
}

$targetExists = [bool]($target)
$shimProjectCount = [int]$shimProjects.Count
$interceptedProjectReferenceCount = [int]$intercepted.Count
$activeSolutionProjectCount = [int]$activeSolutionProjectFullPaths.Count
$activeInterceptedProjectReferenceCount = [int]$activeIntercepted.Count
$passiveInterceptedProjectReferenceCount = [int]$passiveIntercepted.Count
$missingReplacementDllCount = [int]$missingDlls.Count
$activeMissingReplacementDllCount = [int]$activeMissingDlls.Count
$parseFailureCount = [int]$parseFailures.Count
$activeParseFailureCount = [int]$activeParseFailures.Count
$expectedDllArray = [object[]]@($expectedDlls)
$interceptedArray = [object[]]$intercepted.ToArray()
$activeInterceptedArray = [object[]]@($activeIntercepted)
$passiveInterceptedArray = [object[]]@($passiveIntercepted)
$parseFailureArray = [object[]]$parseFailures.ToArray()

$result = New-Object System.Collections.Specialized.OrderedDictionary
$result.Add("generatedAt", (Get-Date).ToString("o"))
$result.Add("status", $status)
$result.Add("targetExists", $targetExists)
$result.Add("targetBeforeTargets", $beforeTargets)
$result.Add("shimProjectCount", $shimProjectCount)
$result.Add("activeSolutionProjectCount", $activeSolutionProjectCount)
$result.Add("expectedDlls", $expectedDllArray)
$result.Add("interceptedProjectReferenceCount", $interceptedProjectReferenceCount)
$result.Add("activeInterceptedProjectReferenceCount", $activeInterceptedProjectReferenceCount)
$result.Add("passiveInterceptedProjectReferenceCount", $passiveInterceptedProjectReferenceCount)
$result.Add("interceptedProjectReferences", $interceptedArray)
$result.Add("activeInterceptedProjectReferences", $activeInterceptedArray)
$result.Add("passiveInterceptedProjectReferences", $passiveInterceptedArray)
$result.Add("missingReplacementDllCount", $missingReplacementDllCount)
$result.Add("activeMissingReplacementDllCount", $activeMissingReplacementDllCount)
$result.Add("parseFailureCount", $parseFailureCount)
$result.Add("activeParseFailureCount", $activeParseFailureCount)
$result.Add("parseFailures", $parseFailureArray)
$result.Add("excludedSyntheticProjectCount", [int]$excludedSyntheticProjects.Count)
$result.Add("excludedSyntheticProjects", [object[]]$excludedSyntheticProjects.ToArray())
$result.Add("evidence", "Static XML simulation only. No dotnet build, no dotnet msbuild, no Roslyn compile.")

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonFullPath -Encoding UTF8

exit 0
