# Status_CONSOLE_MEDIC

Agent: CONSOLE_MEDIC
Domain: INTEGRATION / UNITY CONSOLE TRIAGE
Task Count: 1
Batch Prompt: No `<AGENT_PROMPT id="CONSOLE_MEDIC">` exists in `Docs/Tasks/CURRENT_BATCH.md`; this is a direct user interrupt scoped to Unity Console diagnostics and minimal fixes.

## Selected Mandates

- [x] `QA_Evidence_Text_Filter_Audit.txt` | DOD: separate Unity Console evidence from static-source assumptions | Rejected: treating `rg` hits as proof | Estimate: 8 us
- [x] `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` | DOD: any runtime fix must preserve 0 B hot paths | Rejected: quick logging/string fixes in Tick | Estimate: 12 us
- [x] `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` | DOD: avoid new direct cross-domain dependencies | Rejected: direct concrete references for console-only patches | Estimate: 10 us
- [x] `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` | DOD: no frame/VRAM claims without profiler artifacts | Rejected: fake timing claims | Estimate: 18 us
- [x] `DBG_Telemetry_Crash_Reporting_PostMortem.txt` | DOD: critical runtime faults require bounded debug/log handling | Rejected: silent failure handling | Estimate: 22 us
- [x] `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt` | DOD: initialization/order errors must be fixed at boot contract level | Rejected: Awake dependency fixes | Estimate: 25 us

## Checklist

- [x] Identify domain and prompt source | DOD: scanned `CURRENT_BATCH.md`, no matching XML tag found; direct interrupt accepted under integration triage | Alternative rejected: hijacking another agent ID from neighboring prompts | Estimate: 15 us
- [x] Read project authority and selected mandates | DOD: read `AGENTS.md`, domain map, Unity MCP skill, six selected mandates | Alternative rejected: editing from console text without project law | Estimate: 55 us
- [ ] Read Unity Console messages, warnings, and errors completely | DOD: paginate Console until exhausted and classify by evidence class | Alternative rejected: only reading latest 10 entries | Estimate: pending
- [ ] Fix only defects with clear local ownership and low regression surface | DOD: inspect source before edit, write `[ANALYSIS]` before code patch | Alternative rejected: broad refactor loops | Estimate: pending
- [ ] Verify Unity compilation/Console after every patch group | DOD: wait for compile, reread Console errors/warnings | Alternative rejected: dotnet-only proof | Estimate: pending
- [ ] Append final report to `Docs/AgentLogs/LOG_CONSOLE_MEDIC.md` | DOD: bottom append with wrong/done/cheats/microseconds and evidence class | Alternative rejected: chat-only report | Estimate: pending

Status: PENDING VERIFICATION
