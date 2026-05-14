# Status_GIT_CONFLICT_RESOLUTION

Mandates selected before code edits:
- QA_Evidence_Text_Filter_Audit.txt: classify evidence; no fake Unity/profiler claims.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt: any touched runtime code must preserve hot-path allocation rules.
- DATA_Save_Persistence_Binary_Delta_Checksum.txt: SaveManager diffs require atomic/checksum discipline.
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt: no new concrete cross-domain coupling while resolving merges.
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt: runtime edits must not add suspicious per-frame cost.
- DBG_Telemetry_Crash_Reporting_PostMortem.txt: do not remove crash/failure observability while reconciling.

Task loop 1:
- [ ] Preserve current dirty state | DOD: reversible Git stash/patch path before merge work | Rejected: blind checkout/reset | Estimate: 0 us runtime.
- [ ] Inspect local diffs and retained GitHub Desktop stash | DOD: STATIC_SOURCE/git evidence only | Rejected: assuming Desktop message is full state | Estimate: 0 us runtime.
- [ ] Reconcile dirty files against current HEAD | DOD: manual diff review; no conflict markers | Rejected: automatic ours/theirs blanket resolve | Estimate: pending.
- [ ] Verify repository status and conflict marker scan | DOD: `git status`, `git diff --check`, marker search | Rejected: chat-only claim | Estimate: 0 us runtime.
- [ ] Append final agent log | DOD: disk report in `Docs/AgentLogs/LOG_GIT_CONFLICT_RESOLUTION.md` | Rejected: chat-only report | Estimate: 0 us runtime.
