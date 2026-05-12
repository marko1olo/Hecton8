# RECON_HABITAT_INTEGRITY

Scan command:
`rg -n "void\s+Update\s*\(|Update\s*\(" Assets/_Project/Scripts | rg "Flood|BaseModule|Habitat|WaterPump|ModuleIntegrity|ConstructionManager|BaseDegradation"`

Result:
No legacy habitat/base flooding `Update()` offenders found in the filtered runtime scan.

Status:
PENDING VERIFICATION. Static scan only; Unity Play Mode profiler not run.
