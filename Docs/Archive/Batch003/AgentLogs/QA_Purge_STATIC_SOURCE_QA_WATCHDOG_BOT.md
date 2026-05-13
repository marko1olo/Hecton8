# QA Purge Static Source Report - QA_WATCHDOG_BOT

Evidence Class: STATIC_SOURCE
Scope: Assets/_Project/Scripts/Core
Commands:
- `rg -n "Debug\\.Log|Debug\\.LogWarning|Debug\\.LogError" Assets/_Project/Scripts/Core -g '*.cs'`
- `rg -n "private void Update\\(|public void Tick\\(|void Tick\\(|FastTick\\(|SlowTick\\(|LateFrameTick\\(|LateUpdate\\(" Assets/_Project/Scripts/Core -g '*.cs'`

## Hot-Path Risk Findings
- SystemDispatcher.cs:1260 Update() is the master hot lane; Debug.LogError appears at SystemDispatcher.cs:2133 and SystemDispatcher.cs:2153 in the same dispatcher source. Classification: STATIC_SOURCE, hot-lane owner.
- RuntimeWatchdog.cs has Tick() at 408 and LateFrameTick() at 645; exception logging routes through H8Debug.LogException at 1066, 1081, 1095. Classification: STATIC_SOURCE, watchdog tick owner.
- SceneRuntimeService.cs has Tick() at 311 and string-interpolated Debug.LogError/Warning at 909, 917, 925, 933. Classification: STATIC_SOURCE, tick owner.
- NativeMemorySentinel.cs contains multiple Debug.LogError/Exception sites from 533 through 1088. Classification: STATIC_SOURCE, memory sentinel service.
- GlobalRegistry.cs contains Debug.LogError/Warning/Exception sites at 5422, 5436, 5496, 5512, 5600, 5652, 5738, 5826, 5840, 6214. Classification: STATIC_SOURCE, global service registry.
- GlobalSignals.cs has Debug.LogError at 1323 and 1333 for managed-reference and size violations. Classification: STATIC_SOURCE, signal validation path.
- H8Debug.cs exposes generic Debug.Log, Debug.LogWarning, Debug.LogError, Debug.LogException wrappers at 20, 31, 42, 53. Classification: STATIC_SOURCE, global debug facade.

## Non-Claim
This report does not claim runtime allocation or frame-time proof. Runtime proof requires Unity compile and playmode/profiler artifacts.
