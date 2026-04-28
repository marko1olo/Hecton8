$scripts = Get-ChildItem -Path 'Assets/_Project/Scripts' -Recurse -Filter '*.cs' -File
$suspects = New-Object System.Collections.ArrayList
$structSuspects = New-Object System.Collections.ArrayList

$lifecycle = @('Awake','Start','OnEnable','OnDisable','OnDestroy','Update','FixedUpdate','LateUpdate','OnDrawGizmos','OnDrawGizmosSelected','OnValidate','OnTriggerEnter','OnTriggerExit','OnTriggerStay','OnCollisionEnter','OnCollisionExit','OnCollisionStay','OnParticleCollision','OnBecameVisible','OnBecameInvisible','OnApplicationPause','OnApplicationQuit','OnApplicationFocus','OnLevelWasLoaded','Reset','Main','ToString','Equals','GetHashCode','Clone','CompareTo','Dispose','GetEnumerator','MoveNext','Deconstruct','Invoke','BeginInvoke','EndInvoke','add_','remove_','get_','set_','op_','Finalize')

foreach ($file in $scripts) {
    $content = Get-Content -Raw $file.FullName
    if (-not $content) { continue }
    # Private method declarations
    $declPattern = '(?m)^\s*private\s+(?:static\s+)?(?:async\s+)?(?:override\s+)?(?:[\w<>\[\],\s]+\s)+(\w+)\s*\('
    $decls = [regex]::Matches($content, $declPattern)
    foreach ($d in $decls) {
        $name = $d.Groups[1].Value
        $skip = $false
        foreach ($lc in $lifecycle) { if ($name.StartsWith($lc)) { $skip = $true; break } }
        if ($skip) { continue }
        $occurrences = ([regex]::Matches($content, [regex]::Escape($name) + '\s*\(')).Count
        if ($occurrences -le 1) {
            $suspects.Add([PSCustomObject]@{ File=$file.FullName.Replace($PWD.Path+'\',''); Method=$name }) | Out-Null
        }
    }
    # Struct declarations
    $structPattern = '(?m)^\s*(?:public\s+|internal\s+)?struct\s+(\w+)'
    $structs = [regex]::Matches($content, $structPattern)
    foreach ($s in $structs) {
        $name = $s.Groups[1].Value
        $occurrences = ([regex]::Matches($content, '(?<!\.)\b' + [regex]::Escape($name) + '\b')).Count
        if ($occurrences -le 1) {
            $structSuspects.Add([PSCustomObject]@{ File=$file.FullName.Replace($PWD.Path+'\',''); Struct=$name }) | Out-Null
        }
    }
}

Write-Host "SUSPECT_DEAD_PRIVATE_METHODS: $($suspects.Count)"
Write-Host "SUSPECT_DEAD_STRUCTS: $($structSuspects.Count)"

Write-Host "`n--- TOP FILES BY DEAD METHOD COUNT ---"
$topFiles = $suspects | Group-Object File | Sort-Object Count -Descending | Select-Object -First 20
$topFiles | ForEach-Object { Write-Host "$($_.Count) : $($_.Name)" }

Write-Host "`n--- SAMPLE DEAD METHODS ---"
$suspects | Select-Object -First 30 | ForEach-Object { Write-Host "$($_.File) -> $($_.Method)" }

Write-Host "`n--- SAMPLE DEAD STRUCTS ---"
$structSuspects | Select-Object -First 20 | ForEach-Object { Write-Host "$($_.File) -> $($_.Struct)" }
