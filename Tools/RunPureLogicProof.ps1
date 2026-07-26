# RunPureLogicProof.ps1 — Hecton8 PureLogic verification, 2026-07-26
# Run it, paste the whole output back. Do not edit anything.

$ErrorActionPreference = 'Continue'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe'
$project = 'C:\hades\Hecton8'
$report  = Join-Path $project 'Docs\Reports'
$xml     = Join-Path $report 'PureLogic_EditMode_R2.xml'
$log     = Join-Path $report 'PureLogic_EditMode_R2.log'
$clog    = Join-Path $report 'Compile_R2.log'

New-Item -ItemType Directory -Force -Path $report | Out-Null
Remove-Item -Force -ErrorAction SilentlyContinue $xml, $log, $clog

Write-Output '================ HECTON8 PURELOGIC PROOF ================'

# ---------- STEP 0: process gate ----------
Write-Output '--- STEP 0: PREFLIGHT ---'
$busy = @(Get-Process Unity, dotnet, csc, msbuild -ErrorAction SilentlyContinue)
if ($busy.Count -gt 0) {
    Write-Output "BUILD_GATE_BLOCKED: these are running -> $($busy.Name -join ', ')"
    Write-Output 'STOP. Do not kill them. Report this line and exit.'
    exit 10
}
$cpu = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
Write-Output "CPU_LOAD=$cpu"
if ($cpu -gt 50) {
    Write-Output 'BUILD_GATE_BLOCKED: CPU above 50 percent.'
    Write-Output 'STOP. Wait and re-run later. Report this line and exit.'
    exit 11
}
Write-Output 'PREFLIGHT=OK'

# ---------- STEP 1: EditMode tests ----------
Write-Output '--- STEP 1: EDITMODE TESTS ---'
& $unity -projectPath $project -batchmode -quit `
    -runTests -testPlatform EditMode `
    -assemblyNames Hecton8.PureLogic.Tests `
    -testResults $xml -logFile $log
Write-Output "UNITY_EXIT=$LASTEXITCODE"

if (-not (Test-Path $log)) { Write-Output 'FATAL: no log file produced.'; exit 12 }

$licOk  = [bool](Select-String -Path $log -Pattern 'Successfully connected to LicensingClient' -Quiet)
$licBad = [bool](Select-String -Path $log -Pattern 'LicensingClient has failed validation' -Quiet)
Write-Output "LICENCE_OK=$licOk"
Write-Output "LICENCE_FAILED=$licBad"

if ((-not $licOk) -or $licBad) {
    Write-Output 'RESULT=VOID_NO_LICENCE'
    Write-Output 'The editor had no valid licence, so the Test Framework never ran.'
    Write-Output 'FIX: open Unity Hub once, confirm the account is signed in, close it, run this script again.'
    Write-Output 'Do NOT report the tests as passed or failed. Report exactly this block.'
    exit 13
}

$cscHits = @(Select-String -Path $log -Pattern 'Csc .*Hecton8\.PureLogic\.dll')
Write-Output "PURELOGIC_DLL_RECOMPILED=$($cscHits.Count -gt 0)"
$cscHits | ForEach-Object { Write-Output "  $($_.Line)" }

$csErrors = @(Select-String -Path $log -Pattern 'error CS[0-9]+')
Write-Output "TEST_LOG_CS_ERRORS=$($csErrors.Count)"
$csErrors | Select-Object -First 20 | ForEach-Object { Write-Output "  $($_.Line)" }

if (-not (Test-Path $xml)) {
    Write-Output 'RESULT=FAIL_NO_XML'
    Write-Output 'Unity exited but wrote no test results. Exit code 0 is NOT proof.'
    Write-Output 'Report exactly this block. Do not guess whether tests passed.'
    exit 14
}

# ---------- STEP 2: parse results ----------
Write-Output '--- STEP 2: TEST RESULTS ---'
[xml]$doc = Get-Content -Raw $xml
$run = $doc.'test-run'
Write-Output "TOTAL=$($run.total) PASSED=$($run.passed) FAILED=$($run.failed) SKIPPED=$($run.skipped) RESULT=$($run.result)"

$cases = @($doc.SelectNodes('//test-case'))
$wanted = @(
    'Test_MatchesNewtonCoolingLaw_Case06',
    'Test_SuitResistanceScalesCooling_Case07',
    'Test_MonotonicAndNoOvershoot_Case08',
    'Test_ConfigParametersNeverLeakNaN_Case06',
    'Test_DragIsNeverNegative_Case07',
    'Test_ValidConfigBehaviourUnchanged_Case08',
    'Test_DegenerateMaxLatitude_Case06',
    'Test_DefaultBandBehaviourUnchanged_Case07'
)
Write-Output '--- THE 8 NEW CASES ---'
$missing = 0
foreach ($w in $wanted) {
    $hit = @($cases | Where-Object { $_.name -eq $w })
    if ($hit.Count -eq 0) { Write-Output "MISSING  $w"; $missing++ }
    else { foreach ($h in $hit) { Write-Output "$($h.result.PadRight(8)) $w" } }
}
Write-Output "MISSING_COUNT=$missing"

Write-Output '--- FAILURES (full text) ---'
$fails = @($cases | Where-Object { $_.result -eq 'Failed' })
if ($fails.Count -eq 0) { Write-Output '(none)' }
foreach ($f in $fails) {
    Write-Output "FAIL: $($f.fullname)"
    Write-Output ($f.failure.message.'#text')
    Write-Output ($f.failure.'stack-trace'.'#text')
    Write-Output '---'
}

# ---------- STEP 3: compile check ----------
Write-Output '--- STEP 3: COMPILE CHECK ---'
& $unity -projectPath $project -batchmode -quit -logFile $clog
Write-Output "COMPILE_EXIT=$LASTEXITCODE"
if (Test-Path $clog) {
    $ce = @(Select-String -Path $clog -Pattern 'error CS[0-9]+')
    Write-Output "COMPILE_CS_ERRORS=$($ce.Count)"
    $ce | Select-Object -First 20 | ForEach-Object { Write-Output "  $($_.Line)" }
    Select-String -Path $clog -Pattern 'script compilation time' | ForEach-Object { Write-Output "  $($_.Line)" }
} else {
    Write-Output 'COMPILE_CS_ERRORS=UNKNOWN (no log)'
}

# ---------- VERDICT ----------
Write-Output '--- VERDICT ---'
if ($run.failed -eq '0' -and $missing -eq 0) {
    Write-Output 'RESULT=PASS'
} else {
    Write-Output 'RESULT=FAIL'
}
Write-Output "XML=$xml"
Write-Output "LOG=$log"
Write-Output "COMPILE_LOG=$clog"
Write-Output '================ END ================'
