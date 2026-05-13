# Rationale_EVENT_PROJECTION_BRIDGE

## Decision 0 - Prompt Isolation And Authority

Problem: The user supplied an agent identity and prompt id, but the root `CURRENT_BATCH.md` path did not contain the target prompt.
Solution: Used `Docs/Tasks/CURRENT_BATCH.md` and an attribute-aware PowerShell raw-read regex to isolate exactly `<AGENT_PROMPT id="EVENT_PROJECTION_BRIDGE" role="MODDING_LEAD">`.
Rejected Alternatives: MCP/basic file reading and neighboring prompt context were rejected because batch files can truncate or bleed adjacent agent directives.
Scalability potential: Low/Middle/High/Ultra unchanged; this is governance work to prevent architectural bleed.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Decision 1 - Required Mandate Set

Problem: Mod event projection touches core signal routing, native queues, managed callbacks, AUP conversion, telemetry, and async loading.
Solution: Mandates selected before code edits: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `STRM_Async_Standard`, `ARCH_Project_Bootstrap_Sequence_Init_Safety`.
Rejected Alternatives: Reading all mandate files was rejected as context waste; reading only EventBus code was rejected because task spans managed/native boundary and loader timing.
Scalability potential: Low tier must sample fewer mod events; Ultra tier can project richer public metadata without taxing first-party simulation.
Hardware Impact: Expected target is bounded bridge overhead under 0.1 ms on i3/MX350 after cap/throttle; no measured proof yet.
