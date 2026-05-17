param(
    [string]$ContractsPath = "Assets/_Project/Scripts/Core/Contracts",
    [string]$ScriptsPath = "Assets/_Project/Scripts",
    [string]$ShaderPath = "Assets/_Project/Art/Shaders",
    [string]$HandbookPath = "Docs/ARCHITECT_HANDBOOK.md"
)

$ErrorActionPreference = "Stop"
$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure {
    param([string]$Message)
    [void]$failures.Add($Message)
}

function Assert-NoRgMatch {
    param(
        [string]$Label,
        [string[]]$Arguments
    )

    $lines = @(& rg @Arguments)
    $exit = $LASTEXITCODE
    if ($exit -eq 0 -and $lines.Count -gt 0) {
        Add-Failure ($Label + "`n" + ($lines -join "`n"))
        return
    }

    if ($exit -gt 1) {
        Add-Failure ($Label + " rg failed with exit " + $exit)
    }
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Label
    )

    if (!(Test-Path -Path $Path)) {
        Add-Failure ($Label + " missing file: " + $Path)
        return
    }

    $match = Select-String -Path $Path -Pattern $Pattern -Quiet
    if (!$match) {
        Add-Failure ($Label + " missing pattern: " + $Pattern)
    }
}

function Update-Fnv32 {
    param(
        [uint32]$Hash,
        [int]$ByteValue
    )

    [uint64]$mixed = [uint64]($Hash -bxor ([uint32]($ByteValue -band 0xFF)))
    [uint64]$product = $mixed * 16777619
    return [uint32]($product % 4294967296)
}

Assert-NoRgMatch "FORBID public static float fields" @("--pcre2", "-n", "public\s+static\s+float\s+\w+\s*(=|;)", $ContractsPath, "-g", "*.cs")
Assert-NoRgMatch "FORBID non-readonly public static fields" @("--pcre2", "-n", "public\s+static\s+(?!readonly|class)[^=\(;\n]+\s+\w+\s*=\s*[^>]", $ContractsPath, "-g", "*.cs")
Assert-NoRgMatch "FORBID Unity tick/event/delegate/string surfaces in contracts" @("-n", "Update\s*\(|LateUpdate\s*\(|FixedUpdate\s*\(|string\.Format|Action<|Func<|delegate|EventBus", $ContractsPath, "-g", "*.cs")
Assert-NoRgMatch "FORBID local native container allocation in contracts" @("-n", "\b(new\s+NativeArray|NativeList<|NativeHashMap<|NativeParallelHashMap<|Allocator\.)", $ContractsPath, "-g", "*.cs")
Assert-NoRgMatch "FORBID comment-only contract anchors" @("-n", "Contract authority lives in HectonContractValidator", $ContractsPath, "-g", "*.cs")
Assert-NoRgMatch "FORBID generic comment-only authority anchors" @("-n", "authority is defined|project-generator anchor|comment-only", $ContractsPath, "-g", "*.cs")
Assert-NoRgMatch "FORBID non Pack=1 StructLayout in contracts" @("--pcre2", "-n", "StructLayout\([^\)]*Pack\s*=\s*(?!1\b)[0-9]+|StructLayout\((?![^\)]*Pack\s*=\s*1\b)[^\)]*\)", $ContractsPath, "-g", "*.cs")

$aupLines = @(& rg --pcre2 -n "(?<!\d)5000\.0(?!\d)" $ScriptsPath -g "*.cs")
$aupExit = $LASTEXITCODE
if ($aupExit -eq 0) {
    $badAupLines = @($aupLines | Where-Object { $_ -notmatch "Core[\\/]Contracts[\\/]HectonPhysicsContract\.cs" })
    if ($badAupLines.Count -gt 0) {
        Add-Failure ("FORBID external exact 5000.0 AUP sector literals`n" + ($badAupLines -join "`n"))
    }
} elseif ($aupExit -gt 1) {
    Add-Failure ("AUP literal rg failed with exit " + $aupExit)
}

$requiredAuthorityClasses = @(
    "HectonPhysicsContract",
    "HectonSurvivalContract",
    "HectonEcologyContract",
    "ScalabilityContract",
    "HectonMmfPagingContract",
    "HectonVaultOffsetContract",
    "HectonSignalLaneContract",
    "HectonEditorBreadcrumbContract",
    "HectonLoreContract",
    "HectonPlatformContract",
    "HectonDataSovereigntyContract",
    "HectonVisualOverkillContract",
    "HectonContractVersion"
)

foreach ($className in $requiredAuthorityClasses) {
    $fileName = $className + ".cs"
    Assert-FileContains (Join-Path $ContractsPath $fileName) ("public static class " + $className) ($className + " named-file authority")
    Assert-NoRgMatch ("FORBID hiding " + $className + " outside named file") @("-n", "public\s+static\s+class\s+$className", $ContractsPath, "-g", "*.cs", "-g", ("!" + $fileName))
    Assert-FileContains $HandbookPath $className ("Handbook sync for " + $className)
}

$signalContractPath = Join-Path $ContractsPath "HectonSignalLaneContract.cs"
$dataContractPath = Join-Path $ContractsPath "HectonDataSovereigntyContract.cs"
if (Test-Path -Path $signalContractPath) {
    $laneText = Get-Content -Raw -Path $signalContractPath
    $maxLane = 255
    if (Test-Path -Path $dataContractPath) {
        $maxMatch = [regex]::Match((Get-Content -Raw -Path $dataContractPath), "TypedSignalLaneMaxCount\s*=\s*(?<value>\d+)")
        if ($maxMatch.Success) {
            $maxLane = [int]$maxMatch.Groups["value"].Value
        }
    }

    $laneMatches = [regex]::Matches($laneText, "public\s+const\s+byte\s+(?<name>[A-Za-z0-9_]+)\s*=\s*(?<value>\d+)\s*;")
    $seenLanes = @{}
    [uint32]$laneRegistryHash = 2166136261
    foreach ($match in $laneMatches) {
        $laneName = $match.Groups["name"].Value
        $laneValue = [int]$match.Groups["value"].Value
        if ($laneValue -le 0 -or $laneValue -gt $maxLane) {
            Add-Failure ("FORBID invalid SignalBus lane id: " + $laneName + "=" + $laneValue + " max=" + $maxLane)
        }

        $laneKey = [string]$laneValue
        if ($seenLanes.ContainsKey($laneKey)) {
            Add-Failure ("FORBID duplicate SignalBus lane id " + $laneValue + ": " + $seenLanes[$laneKey] + " and " + $laneName)
        } else {
            $seenLanes[$laneKey] = $laneName
        }

        for ($i = 0; $i -lt $laneName.Length; $i++) {
            $laneRegistryHash = Update-Fnv32 $laneRegistryHash ([byte][char]$laneName[$i])
        }

        $laneRegistryHash = Update-Fnv32 $laneRegistryHash $laneValue

        Assert-FileContains $HandbookPath ("\| HectonSignalLaneContract \| " + $laneName + " \| byte \|") ("Handbook sync for signal lane " + $laneName)
    }

    if ($laneMatches.Count -eq 0) {
        Add-Failure "HectonSignalLaneContract has no public byte lane ids."
    }

    $declaredHashMatch = [regex]::Match($laneText, "SignalLaneRegistryHash\s*=\s*0x(?<value>[0-9A-Fa-f]+)u")
    if (!$declaredHashMatch.Success) {
        Add-Failure "HectonSignalLaneContract missing SignalLaneRegistryHash."
    } else {
        $declaredHash = [Convert]::ToUInt32($declaredHashMatch.Groups["value"].Value, 16)
        if ($declaredHash -ne $laneRegistryHash) {
            Add-Failure ("SignalLaneRegistryHash mismatch. declared=0x{0:X8} computed=0x{1:X8}" -f $declaredHash, $laneRegistryHash)
        }
    }
} else {
    Add-Failure ("Missing signal lane contract: " + $signalContractPath)
}

Assert-FileContains (Join-Path $ContractsPath "HectonContractVersion.cs") "HectonSignalLaneContract.SignalLaneRegistryHash" "Contract version mixes signal lane registry hash"
Assert-FileContains "Directory.Build.targets" "PlayerMovementPresentationSignals.cs" "Generated Core shim includes player movement presentation signals"

if (Test-Path -Path $ShaderPath) {
    $threadLines = @(& rg -n "\[numthreads\((\d+)\s*,\s*(\d+)\s*,\s*(\d+)\)\]" $ShaderPath -g "*.compute" -g "*.hlsl" -g "*.shader")
    $threadExit = $LASTEXITCODE
    $maxThreads = 0
    if ($threadExit -eq 0) {
        foreach ($line in $threadLines) {
            if ($line -match "numthreads\((\d+)\s*,\s*(\d+)\s*,\s*(\d+)\)") {
                $x = [int]$Matches[1]
                $y = [int]$Matches[2]
                $z = [int]$Matches[3]
                $product = $x * $y * $z
                if ($product -gt $maxThreads) {
                    $maxThreads = $product
                }

                if ($product -gt 1024 -or $z -gt 64) {
                    Add-Failure ("FORBID shader thread-group over platform contract: " + $line + " product=" + $product + " z=" + $z)
                }
            }
        }
    } elseif ($threadExit -gt 1) {
        Add-Failure ("Shader numthreads rg failed with exit " + $threadExit)
    }

    $rendererLines = @(& rg -n "#pragma\s+only_renderers|#pragma\s+exclude_renderers" $ShaderPath -g "*.compute" -g "*.hlsl" -g "*.shader")
    $rendererExit = $LASTEXITCODE
    if ($rendererExit -eq 0) {
        foreach ($line in $rendererLines) {
            if ($line -match "only_renderers" -and $line -match "d3d" -and $line -notmatch "metal|vulkan|gles|glcore") {
                Add-Failure ("FORBID DirectX-only shader renderer pragma: " + $line)
            }

            if ($line -match "exclude_renderers" -and $line -match "metal") {
                Add-Failure ("FORBID Metal-excluding shader pragma: " + $line)
            }
        }
    } elseif ($rendererExit -gt 1) {
        Add-Failure ("Shader renderer pragma rg failed with exit " + $rendererExit)
    }

    Write-Host ("Shader numthreads max product: " + $maxThreads)
}

if ($failures.Count -gt 0) {
    Write-Error ("CONTRACT AUTHORITY AUDIT FAILED`n" + ($failures -join "`n`n"))
    exit 1
}

Write-Host "CONTRACT AUTHORITY AUDIT PASSED"
