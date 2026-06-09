# Status 1616 - PROJECT_HARDENING_AND_LEAK_SENTRY

Status: CURRENT CONTINUATION LEDGER - previous 1616 ledgers archived to `Docs/Archive/Batch015`; `Docs/Tasks/CURRENT_BATCH.md` is absent in the live Tasks folder.
Domain: Echelon 1 Core Memory Infrastructure / Project Hardening and Leak Sentry
Task count: 19
Build policy: `dotnet build` not run; active dotnet/Unity processes remain present.

## Mandates Loaded

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Execution_Phases.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- CORE_Global_State_Reset_NonReload_Transitions.txt

## Current Continuation

Loop 16/5 APEX continuation complete: patched `H8BridgeLiveSyncScheduler` static runner to implement `IGlobalRegistryHotSwapListener`, require cold late-frame plus hotswap registration before accepting live-sync requests, clear queued design/input/prefab requests on DataVault replacement, clear design requests on MacroDatabase replacement, and re-register the runner after Dispatcher replacement when requests are pending. DOD: queued `IDataVault`/macro references cannot survive service replacement into `LateFrameTick`; the visual sync body remains a single `FlushLateFrame()` call with no registry lookup. Verification: Unity `validate_script` returned 0 errors / 0 warnings; filtered Unity console query for `H8BridgeLiveSyncScheduler` returned 0 entries; source balance `braces=37/37`, `parens=84/84`; `LateFrameHasGlobalRegistry=False`; SHA256 `685A0216B8E45290A5D8C59415DE7EB83A21133B6D91C3C48CA0FF17E93977DC`. `git diff --check` reported only CRLF normalization warning. Global console still contains unrelated `H8NarrativeApexVerifier` errors, so no global clean-console claim.

Loop 17/5 APEX continuation complete: patched `Arm64AlignmentFaultGizmo` so `OnDrawGizmos` no longer polls `GlobalRegistry.DataVault` on every gizmo repaint. The gizmo now caches the vault in cold lifecycle, implements `IGlobalRegistryHotSwapListener`, updates the cached vault on DataVault replacement, and unregisters/clears on disable/destroy. DOD: editor visualization reads cached memory telemetry only, matching the cached-data gizmo rule. Verification: Unity `validate_script` returned 0 errors / 0 warnings; filtered Unity console query for `Arm64AlignmentFaultGizmo` returned 0 entries; source balance `braces=11/11`; `OnDrawGizmos` body has `HasGlobalRegistry=False` and `HasGetComponent=False`; SHA256 `F72E965AC8339461EC47F9455EA6A23BCDC59F18AE0403E212F7A73BFC5A4E8F`. `git diff --check` for bridge+gizmo reported only CRLF normalization warnings. Estimate: 0 us/frame runtime; editor gizmo repaint saves one static registry read.

Loop 18/5 APEX continuation complete: patched `GlobalRegistry.UnregisterDataVault` so signal-domain DataVault-owned handles are released before the active vault slot is cleared. The unregister route now calls a narrow `ReleaseSignalDataVaultOwnedHandles()` helper covering `SignalTuningTable`, `SignalTelemetryRingBuffer`, and `SignalThreadLocalScratchpad`, then clears MathGuard, SignalBusRegistry, and telemetry cold binds. DOD: unregistering the active DataVault no longer leaves static signal tables with generation handles that still point at a retiring vault; typed signal lanes are not globally disposed by this narrow cold path. Verification: Unity `validate_script` returned 0 errors / 0 warnings for `GlobalRegistry.cs`; source balance `braces=715/715`, `parens=2393/2393`; unregister snippet has `UnregisterHasGetComponent=False` and `UnregisterHasGenericRegistryGet=False`; SHA256 `04134FB365DF90AEC0B90116FB2B686F2203BE837799CC76A81C689F1F898E14`. Filtered Unity console retry for `GlobalRegistry` could not complete because the Unity MCP session stopped answering ping after validation; no `dotnet build` was launched. Estimate: 0 us/frame runtime; scene unload/service teardown prevents stale vault handles without touching hot loops.
