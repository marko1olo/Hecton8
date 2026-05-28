param(
    [string]$JsonPath = "Docs/AgentLogs/BuildGraphStaticHealth_1400.json"
)

$ErrorActionPreference = "Stop"

function New-List {
    $list = New-Object "System.Collections.Generic.List[object]"
    return ,$list
}

function New-StringSet {
    $set = New-Object "System.Collections.Generic.HashSet[string]"
    return ,$set
}

function Get-RelativePath {
    param(
        [string]$Root,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith($Root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($Root.Length).TrimStart('\', '/')
    }

    return $fullPath
}

function Load-XmlDocument {
    param([string]$Path)

    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $false
    $document.Load($Path)
    return $document
}

function Get-PatternLines {
    param(
        [string]$Path,
        [string]$Pattern
    )

    $lineMatches = New-List
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($Path)) {
        $lineNumber++
        if ($line -match $Pattern) {
            $lineMatches.Add([pscustomobject]@{
                line = $lineNumber
                text = $line.Trim()
            })
        }
    }

    return ,$lineMatches
}

function Add-Cycle {
    param(
        [System.Collections.Generic.List[object]]$Cycles,
        [string[]]$Stack,
        [string]$Node,
        [hashtable]$PathByNode
    )

    $start = [Array]::IndexOf($Stack, $Node)
    if ($start -lt 0) {
        return
    }

    $cycle = New-Object "System.Collections.Generic.List[string]"
    for ($i = $start; $i -lt $Stack.Length; $i++) {
        $cycle.Add($PathByNode[$Stack[$i]])
    }
    $cycle.Add($PathByNode[$Node])
    $Cycles.Add([string[]]$cycle.ToArray())
}

function Visit-Node {
    param(
        [string]$Node,
        [hashtable]$Edges,
        [hashtable]$State,
        [string[]]$Stack,
        [hashtable]$PathByNode,
        [System.Collections.Generic.List[object]]$Cycles
    )

    if ($State.ContainsKey($Node)) {
        if ($State[$Node] -eq 1) {
            Add-Cycle -Cycles $Cycles -Stack $Stack -Node $Node -PathByNode $PathByNode
        }
        return
    }

    $State[$Node] = 1
    $nextStack = @($Stack + $Node)
    foreach ($next in @($Edges[$Node])) {
        Visit-Node -Node $next -Edges $Edges -State $State -Stack $nextStack -PathByNode $PathByNode -Cycles $Cycles
    }
    $State[$Node] = 2
}

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$rootWithSeparator = $root.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$jsonFullPath = Join-Path $root $JsonPath
$jsonDirectory = Split-Path -Parent $jsonFullPath
if (-not (Test-Path -LiteralPath $jsonDirectory)) {
    New-Item -ItemType Directory -Path $jsonDirectory -Force | Out-Null
}

$solutionPath = Join-Path $root "Hecton8.slnx"
$targetsPath = Join-Path $root "Directory.Build.targets"

$parseFailures = New-List
$missingSolutionProjects = New-List
$duplicateSolutionProjects = New-List
$missingProjectReferences = New-List
$externalProjectReferences = New-List
$selfProjectReferences = New-List
$shimProjectReferences = New-List
$activeProjects = @{}
$pathByNode = @{}
$edges = @{}

$solutionXml = Load-XmlDocument -Path $solutionPath
$solutionSeen = New-StringSet
foreach ($projectNode in @($solutionXml.SelectNodes("//Project"))) {
    $relative = [string]$projectNode.GetAttribute("Path")
    if ([string]::IsNullOrWhiteSpace($relative)) {
        continue
    }

    $full = [System.IO.Path]::GetFullPath((Join-Path $root $relative))
    $key = $full.ToLowerInvariant()
    if (-not $solutionSeen.Add($key)) {
        $duplicateSolutionProjects.Add([pscustomobject]@{
            project = $relative
            fullPath = $full
        })
        continue
    }

    if (-not (Test-Path -LiteralPath $full)) {
        $missingSolutionProjects.Add([pscustomobject]@{
            project = $relative
            fullPath = $full
        })
        continue
    }

    $activeProjects[$key] = [pscustomobject]@{
        project = $relative
        fullPath = $full
    }
    $pathByNode[$key] = $relative
    $edges[$key] = New-Object "System.Collections.Generic.List[string]"
}

$targetsXml = Load-XmlDocument -Path $targetsPath
$shimValue = ""
foreach ($propertyNode in @($targetsXml.SelectNodes("//HectonUnityPackageCliShimProjects"))) {
    $shimValue = [string]$propertyNode.InnerText
    break
}

$shimProjects = New-StringSet
foreach ($part in $shimValue.Split([char]'|')) {
    $trimmed = $part.Trim()
    if ($trimmed.Length -gt 0) {
        [void]$shimProjects.Add($trimmed)
    }
}

foreach ($entry in @($activeProjects.GetEnumerator())) {
    $project = $entry.Value
    try {
        $projectXml = Load-XmlDocument -Path $project.fullPath
    } catch {
        $parseFailures.Add([pscustomobject]@{
            project = $project.project
            error = $_.Exception.Message
        })
        continue
    }

    foreach ($reference in @($projectXml.SelectNodes("//ProjectReference"))) {
        $include = [string]$reference.GetAttribute("Include")
        if ([string]::IsNullOrWhiteSpace($include)) {
            continue
        }

        $targetFull = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $project.fullPath) $include))
        $targetKey = $targetFull.ToLowerInvariant()
        $targetRelative = Get-RelativePath -Root $rootWithSeparator -Path $targetFull
        $targetName = [System.IO.Path]::GetFileNameWithoutExtension($targetFull)

        if ($shimProjects.Contains($targetName)) {
            $shimProjectReferences.Add([pscustomobject]@{
                project = $project.project
                include = $include
                target = $targetRelative
            })
        }

        if ($targetKey -eq $entry.Key) {
            $selfProjectReferences.Add([pscustomobject]@{
                project = $project.project
                include = $include
            })
        }

        if (-not (Test-Path -LiteralPath $targetFull)) {
            $missingProjectReferences.Add([pscustomobject]@{
                project = $project.project
                include = $include
                target = $targetRelative
            })
            continue
        }

        if ($activeProjects.ContainsKey($targetKey)) {
            $edges[$entry.Key].Add($targetKey)
        } else {
            $externalProjectReferences.Add([pscustomobject]@{
                project = $project.project
                include = $include
                target = $targetRelative
            })
        }
    }
}

$cycles = New-List
$state = @{}
foreach ($node in @($activeProjects.Keys)) {
    Visit-Node -Node $node -Edges $edges -State $state -Stack @() -PathByNode $pathByNode -Cycles $cycles
}

$targetNameCounts = @{}
foreach ($targetNode in @($targetsXml.SelectNodes("//Target[@Name]"))) {
    $name = [string]$targetNode.GetAttribute("Name")
    if (-not $targetNameCounts.ContainsKey($name)) {
        $targetNameCounts[$name] = 0
    }
    $targetNameCounts[$name]++
}

$duplicateTargets = New-List
foreach ($name in @($targetNameCounts.Keys | Sort-Object)) {
    if ($targetNameCounts[$name] -gt 1) {
        $duplicateTargets.Add([pscustomobject]@{
            target = $name
            count = $targetNameCounts[$name]
        })
    }
}

$directCompileIncludes = New-List
$missingDirectCompileIncludes = New-List
foreach ($compileNode in @($targetsXml.SelectNodes("//Compile[@Include]"))) {
    $include = [string]$compileNode.GetAttribute("Include")
    if ([string]::IsNullOrWhiteSpace($include) -or $include.Contains("*") -or $include.Contains('$(') -or $include.Contains('@(')) {
        continue
    }

    $directCompileIncludes.Add($include)
    $full = [System.IO.Path]::GetFullPath((Join-Path $root $include))
    if (-not (Test-Path -LiteralPath $full)) {
        $missingDirectCompileIncludes.Add([pscustomobject]@{
            include = $include
            fullPath = $full
        })
    }
}

$broadProjectReferenceRemoveLines = Get-PatternLines -Path $targetsPath -Pattern '<ProjectReference\s+Remove="@\(ProjectReference\)"'
$fixTargetLines = Get-PatternLines -Path $targetsPath -Pattern '<Target\s+Name="FixUnityCircularReferences"|_HectonUnityPackageProjectReference|<ProjectReference\s+Remove="@\(_HectonUnityPackageProjectReference\)"'

$activeEdgeCount = 0
foreach ($list in @($edges.Values)) {
    $activeEdgeCount += $list.Count
}

$status = "STATIC_BUILD_GRAPH_HEALTH_GREEN"
if ($missingSolutionProjects.Count -gt 0) {
    $status = "MISSING_SOLUTION_PROJECT"
} elseif ($parseFailures.Count -gt 0) {
    $status = "ACTIVE_PROJECT_XML_PARSE_FAILURE"
} elseif ($missingProjectReferences.Count -gt 0) {
    $status = "MISSING_PROJECT_REFERENCE"
} elseif ($selfProjectReferences.Count -gt 0) {
    $status = "SELF_PROJECT_REFERENCE"
} elseif ($cycles.Count -gt 0) {
    $status = "ACTIVE_PROJECT_REFERENCE_CYCLE"
} elseif ($duplicateTargets.Count -gt 0) {
    $status = "DUPLICATE_MSBUILD_TARGET"
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    status = $status
    evidence = "Static XML graph health audit only. No dotnet build and no dotnet msbuild executed."
    activeSolutionProjectCount = [int]$activeProjects.Count
    activeProjectReferenceEdgeCount = [int]$activeEdgeCount
    missingSolutionProjectCount = [int]$missingSolutionProjects.Count
    missingSolutionProjects = [object[]]$missingSolutionProjects.ToArray()
    duplicateSolutionProjectCount = [int]$duplicateSolutionProjects.Count
    duplicateSolutionProjects = [object[]]$duplicateSolutionProjects.ToArray()
    activeProjectXmlParseFailureCount = [int]$parseFailures.Count
    activeProjectXmlParseFailures = [object[]]$parseFailures.ToArray()
    missingProjectReferenceCount = [int]$missingProjectReferences.Count
    missingProjectReferences = [object[]]$missingProjectReferences.ToArray()
    externalProjectReferenceCount = [int]$externalProjectReferences.Count
    externalProjectReferences = [object[]]$externalProjectReferences.ToArray()
    selfProjectReferenceCount = [int]$selfProjectReferences.Count
    selfProjectReferences = [object[]]$selfProjectReferences.ToArray()
    activeProjectReferenceCycleCount = [int]$cycles.Count
    activeProjectReferenceCycles = [object[]]$cycles.ToArray()
    shimProjectReferenceCount = [int]$shimProjectReferences.Count
    shimProjectReferences = [object[]]$shimProjectReferences.ToArray()
    duplicateDirectoryBuildTargetCount = [int]$duplicateTargets.Count
    duplicateDirectoryBuildTargets = [object[]]$duplicateTargets.ToArray()
    directCompileIncludeCount = [int]$directCompileIncludes.Count
    missingDirectCompileIncludeCount = [int]$missingDirectCompileIncludes.Count
    missingDirectCompileIncludes = [object[]]$missingDirectCompileIncludes.ToArray()
    broadProjectReferenceRemoveLines = [object[]]$broadProjectReferenceRemoveLines.ToArray()
    fixUnityCircularReferenceLines = [object[]]$fixTargetLines.ToArray()
}

$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonFullPath -Encoding UTF8
exit 0
