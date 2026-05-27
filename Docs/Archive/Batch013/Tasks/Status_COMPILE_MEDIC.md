# COMPILE_MEDIC Status

Operational ID: COMPILE_MEDIC
Domain: Echelon 9 / The Integrator (Compile Medic)
Prompt source: User requested latest dotnet compile error/warning repair. No matching compile-medic `<AGENT_PROMPT>` exists in `Docs/Tasks/CURRENT_BATCH.md`.
Status: PENDING VERIFICATION

## Mandates Read

- [x] CI_MATH_VIOLATIONS_Gate | Compile and static warning debt can block runtime quality; rejected broad refactor loop.
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init | Registry access must stay cold/cached; rejected hidden runtime dependency lookup as compile fix.
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate | Hot paths must remain 0 B/frame; rejected allocation-based quick fixes.
- [x] DATA_Runtime_Struct_Layout_ARM64 | DTO/native payload fixes must keep 8-byte alignment; rejected pack shortcuts.
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol | Job/native fixes must preserve ownership and fences; rejected local persistent NativeArray aliases.
- [x] PROJECT_LTS_Compatibility_Layer | Deprecated/obsolete warnings are technical debt; rejected warning suppression without proof.

## Loop 1

- [x] Extract newest dotnet compile logs from disk | DOD: evidence-first compile repair; rejected building before known-log triage | estimate: 5000 us
- [x] Build error ledger with file/line/symbol/warning groups | DOD: one defect cluster at a time; rejected shotgun edits | estimate: 8000 us
- [x] Read affected source/contracts before edits | DOD: source-proven fix only; rejected invented signatures | estimate: 15000 us
- [x] Patch first defect cluster | DOD: restore real source/project references; rejected dummy enum/attribute shims | estimate: 24000 us
- [x] Verify compiler state when CPU/dotnet guard allows | DOD: no build spam; rejected build during active csc/dotnet or high CPU | estimate: 540000000 us

## Loop 2

- [x] Re-run guarded compile after first-party/project-graph patch | DOD: objective compiler delta only | estimate: 540000000 us
- [x] Parse residual first-party errors before vendor work | DOD: never hide Hecton code behind vendor noise | estimate: 12000 us
- [x] Patch residual first-party contracts if build exposes them | DOD: keep ownership/routes unchanged | estimate: 220000 us
- [x] Decide vendor/generated project strategy from evidence | DOD: generated csproj must model Unity asmdef/asmref and package DLL ownership; rejected vendor behavior rewrites | estimate: 380000000 us
- [x] Re-read prompt/status/rationale before next report | DOD: anti-amnesia protocol | estimate: 3000 us

## Loop 3

- [x] Guarded `Hecton8.Core.csproj` compile after residual cluster patch | DOD: objective delta before vendor graph edits | estimate: 80000000 us
- [x] Parse new residuals from compiler, not stale log | DOD: avoid fixing already-dead errors | estimate: 5000 us
- [x] Patch next real first-party cluster | DOD: source/contract proven changes only | estimate: 14000 us
- [ ] Re-read `CURRENT_BATCH.md` assignment search after next three task groups | DOD: anti-amnesia protocol | estimate: pending
- [ ] Update final report log with exact deltas | DOD: CTO reads disk logs, not chat | estimate: pending

## Loop 4

- [x] Run full solution compile after Core is green | DOD: vendor/generated graph triage from fresh data | estimate: 360000000 us
- [x] Audit MapMagic and Crest compile/project graph specifically | DOD: MapMagic targeted runtime/editor projects green; Crest pending full-solution confirmation | estimate: 220000000 us
- [x] Patch project graph/vendor reference issues without source churn where possible | DOD: fix asmref coverage, Unity version defines, local DLL references, Burst compiler DLL bleed | estimate: 900000000 us
- [ ] Collect warning inventory with unsuppressed/structured logs | DOD: warnings need file-level evidence | estimate: pending
- [ ] Append detailed final log | DOD: disk report is authoritative | estimate: pending
