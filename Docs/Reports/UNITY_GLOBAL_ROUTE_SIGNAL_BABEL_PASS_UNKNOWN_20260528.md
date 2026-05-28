# Unity Global Route Signal/Babel Pass UNKNOWN - 2026-05-28

Status: STATIC SOURCE PROOF ONLY / RUNTIME PROOF ABSENT

## Scope

Domain: Core and memory infrastructure, SignalBus routes, Babel localization Vault ownership.

User constraint honored: full project compile errors are not fixed in this pass. Another agent owns the compile wall.

## What Was Wrong

`LocRegistry.TryResolveBabelVault()` read `GlobalRegistry.DataVault` from helpers that are reachable from runtime localization lookup and telemetry fallback paths.

That broke the global systems doctrine: `GlobalRegistry` is cold identity and dependency injection only, while lookup/read helpers must consume cached owner interfaces or handles.

`SignalBus<T>.TryPush()` can lazily call `EnsureInitialized()` for a lane that was not prewarmed. That path acquired per-lane frame snapshot storage through `GlobalRegistry.DataVault`.

That made the first publish on a late/uninitialized lane capable of polling the registry and opening Vault-backed snapshot storage from a publish route.

`SignalTelemetryRingBuffer.Initialize()` also resolved the Vault internally even though the caller already had the cold boot Vault.

## What Changed

`LocRegistry` now exposes `BindBabelVaultCold(IDataVault vault)`.

`TryResolveBabelVault()` reads only the cached `_babelVault` field and fails closed during compaction fences. It no longer touches `GlobalRegistry`.

`LocalizationManager` now:

- binds Babel Vault during `Awake()` before `LocRegistry.ReloadBinaryOrMock()`;
- registers as an `IGlobalRegistryHotSwapListener`;
- rebinds and reloads Babel registry on `GlobalRegistryServiceSlot.DataVault`;
- clears the static Babel Vault on owner destroy.

`SignalBusRegistry` now owns a cached DataVault route for per-lane snapshot buffers:

- `BindDataVaultCold(IDataVault vault)`;
- `TryGetBoundDataVault(out IDataVault vault)`.

`SignalBus<T>.TryFindFrameSnapshotVaultForBootstrap()` now uses that cached route instead of `GlobalRegistry.DataVault`.

`GlobalRegistry.RegisterDataVault()` and `UnregisterDataVault()` bind and clear the SignalBus cached Vault route.

`GlobalSignals.InitializeAllQueues()` also binds the boot Vault before lane initialization, and passes the same Vault into `SignalTelemetryRingBuffer.Initialize(IDataVault vault)`.

## Proof

Targeted direct DataVault reads after patch:

```text
LocRegistry.cs: 0
SignalBusRuntime.cs: 0
GlobalRegistry.cs: 0
LocalizationManager.cs: 1 cold Awake bind
GlobalSignals.RuntimeLifecycle.cs: 1 cold InitializeAllQueues bind
SignalWardenRuntime.cs: 1 editor CSV load; TryGetLatestCreated remains crash-dump fallback
```

SignalBus full audit:

```text
Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_GLOBAL_ROUTE_SIGNAL_BABEL_RECHECK.json
files=2446
shaders=71
errors=0
confirmedErrors=0
warnings=110
infos=1195
```

Warning composition in that live audit:

```text
SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW=69
RUNTIME_SYNC_FILE_IO_REVIEW=40
LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT=1
```

Touched-file audit warnings: `0` for `LocRegistry`, `LocalizationManager`, `SignalBusRuntime`, `GlobalSignals.RuntimeLifecycle`, `SignalWardenRuntime`, and `GlobalRegistry`.

Source hygiene:

```text
git diff --check -- touched source files
exit=0
only LF-to-CRLF working-copy warnings
```

Documentation gates:

```text
VerifyDocStructure.py pass=true activeDocCount=708 encodingWithoutUtf8Sig=0
OOP_Doc_Scanner.py finalPass=true activeFileCount=708 sourceSyncPass=true wordReductionPercent=30.358189496725423
```

Full solution build was not run by this agent.

## Residuals

The live full SignalBus audit warning count is higher than the previous `40` because concurrent source changes introduced `69` SRP material-review warnings outside this patch. This pass did not edit those files.

Remaining direct `GlobalRegistry.DataVault` reads in the touched route are cold/editor/crash boundaries, not hot lookup helpers.

No Unity Editor import, Play Mode, profiler, GCMonitor, player build, or device proof was produced.

## Runtime Claim Boundary

Runtime microseconds saved: `0 us` claimed.

Expected benefit is route determinism and lower first-publish hitch risk for late SignalBus lanes, not a measured frame-time result.
