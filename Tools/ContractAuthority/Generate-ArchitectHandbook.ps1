param(
    [string]$ContractsPath = "Assets/_Project/Scripts/Core/Contracts",
    [string]$OutputPath = "Docs/ARCHITECT_HANDBOOK.md"
)

$ErrorActionPreference = "Stop"
$contractFiles = Get-ChildItem -Path $ContractsPath -Filter "*.cs" | Sort-Object Name
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add("# ARCHITECT HANDBOOK")
$rows.Add("")
$rows.Add("Generated from Core/Contracts. Edit constants in C# contracts, then regenerate this file.")
$rows.Add("")
$rows.Add("| Contract | Constant | Type | Value |")
$rows.Add("| --- | --- | --- | --- |")

foreach ($file in $contractFiles) {
    $currentContract = $file.BaseName
    foreach ($line in Get-Content -Path $file.FullName) {
        $classMatch = [regex]::Match($line, "public\s+(?:static\s+)?class\s+(?<name>[A-Za-z0-9_]+)")
        if ($classMatch.Success) {
            $currentContract = $classMatch.Groups["name"].Value.Trim()
            continue
        }

        $match = [regex]::Match(
            $line,
            "public\s+const\s+(?<type>[A-Za-z0-9_<>]+)\s+(?<name>[A-Za-z0-9_]+)\s*=\s*(?<value>[^;]+);")
        if (!$match.Success) {
            continue
        }

        $type = $match.Groups["type"].Value.Trim()
        $name = $match.Groups["name"].Value.Trim()
        $value = $match.Groups["value"].Value.Trim().Replace("|", "\|")
        $rows.Add(('| {0} | {1} | {2} | `{3}` |' -f $currentContract, $name, $type, $value))
    }
}

$rows.Add("")
$rows.Add("RU: Eto karta zakonov dvizhka. Menyat fiziku, vyzhivanie, ekologiyu, LOD i ABI offsety nuzhno zdes, ne v Burst jobah.")
$rows.Add("EN: This is the law map for the engine. Change physics, survival, ecology, LOD, and ABI offsets here, not inside Burst jobs.")
Set-Content -Path $OutputPath -Value $rows -Encoding UTF8
