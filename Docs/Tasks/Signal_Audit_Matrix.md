# Signal Audit Matrix - SIGNAL_AUTHORITY_VALIDATOR

Date: 2026-05-17
Domain: CORE/SIGNALS
Authority file: Assets/_Project/Scripts/Core/GlobalSignals.cs

## Duplicate Purge

| Check | Command | Result |
| --- | --- | --- |
| CombatEvent / HitSignal / DamageSignal live names | `rg -n "\b(CombatEvent|HitSignal|DamageSignal)\b" Assets/_Project/Scripts -g "*.cs"` | 0 matches |
| Old signal namespace | `rg -n "namespace Hecton8\.Core\.Signals|Hecton8\.Core\.Signals" Assets/_Project/Scripts -g "*.cs"` | 0 matches |
| ISignal structs outside contract namespace | PowerShell namespace scan over `struct ... : ISignal` | 0 matches |
| SignalBus lane Configure outside GlobalSignals | `rg "SignalBus<...>.Configure" ... | rg -v Core/GlobalSignals.cs` | 0 matches |
| PlayerInteraction managed events | `rg -n "UnityEvent|Action<|event Action|\bevent\b" Assets/_Project/Scripts/Interaction/PlayerInteraction.cs` | 0 matches |

## Canonical Lanes

| Lane | Status | Notes |
| --- | --- | --- |
| Combat damage | Canonical | Legacy `DamageSignal` payload and generated hash removed; producers write `CombatDamageSignal`. |
| High speed impact | Canonical | `KineticEnergy` added as alias for shader-readable energy while preserving `LostKineticEnergy` callers. |
| Non-critical VFX | Bounded | Stress >= 0.8 drops optional VFX; stress < 0.2 allows full propagation. |
| Visual flare / turbulence | Bounded | Late presentation lanes were added to non-critical stress shedding so MX350/Quest can cut visual flood first. |
| Audio transition | SPSC compatible | Existing native queues/ring buffers remain; signal path uses typed unmanaged payloads. |
| AUP shift | PRE_SIMULATION | `GlobalSignals.FlushPreSimulation()` drains lanes before render and applies AUP shift safety. |
| Lockstep / glitch / tether fire | Centralized | Late local Configure drift removed; capacity/hash authority now lives in `GlobalSignals.InitializeAllQueues()`. |
| Physics determinism | Centralized | Input, state correction, desync, sync fence, and KCC velocity lanes are configured only by `GlobalSignals`. |
| Laser cutter events | Centralized | Cutter heat/beam event payload lives in contracts and gameplay bridge only pushes/reads the central typed lane. |
| Splash / physics event / deferred impact | Centralized | Splash, transient physics, and deferred submarine impact payloads live in contracts and feature bridges only push/read central lanes. |
| Architect Eye debug visuals | Centralized | Debug visual payload lives in contracts, is registered centrally, and is marked non-critical for stress shedding. |
| Acoustic zone transitions | Centralized | `AcousticZoneChangedEvent` now lives in contracts, is Pack=1 Size=16, and is registered only by `GlobalSignals.InitializeAllQueues()`. |
| DirectorAI music cue | Centralized | `DirectorAIMusicSignal` is Pack=1 Size=32 and now has central capacity/hash/low-tier policy in `GlobalSignals.InitializeAllQueues()`. |
| Data-vault bridge updates | Centralized | `DataVaultUpdateSignal`, `PrefabAcousticSignatureSignal`, and `PrefabLoreLinkSignal` are Pack=1 Size=32 and now have central bounded SignalBus policies. |
| Scalability profile changes | Centralized | `ScalabilityChangedEvent` moved to contracts, padded to Pack=1 Size=16, and now enters through central SignalBus policy. |
| Brownout / debris / seismic / HUD / tool acoustic / lights / physiology / stress | Centralized | Residual active `SignalBus<T>` lanes now have explicit central capacity, hash, and low-tier policies instead of generic default policy fallback. |

## Layout Audit

| Item | Result | Action |
| --- | --- | --- |
| External signal structs moved from feature namespaces | 0 outside contract namespace | Done |
| Physical signal authority | 0 `ISignal` payload structs outside `Assets/_Project/Scripts/Core/GlobalSignals.cs` | Done; duplicate feature-file payload definitions were removed after centralization. |
| Newly evicted external signal structs | Sequential Pack=1 fixed-size | Done |
| All `ISignal` payload sizes | No non-16-byte payloads found in current targeted/validator checks | Done; tether snap/fire padded to 80/48 bytes and splash/physics/deferred impact validate at 64/80/48 bytes. |
| `SignalLaneTelemetry` ABI | Pack=1 Size=32 with explicit reserved fields | Done; non-ISignal telemetry packet now has stable NativeArray/DataVault layout. |
| Adjacent physics event DTO ABI | Explicit 80/32/48/48/48-byte strides | Done; pressure, EMP, acoustic ping, acoustic impulse, and large acoustic impulse DTOs no longer rely on implicit final stride. |
| Flood mass result ABI | Explicit 48-byte stride | Done; 44-byte result packet padded with reserved field for 16-byte multiple. |
| Debug visual signal ABI | Pack=1 Size=64 | Done; Architect Eye debug payload validates as a fixed 16-byte-multiple signal contract. |
| Scalability event ABI | Pack=1 Size=16 | Done; previous 2-byte payload was padded for ARM64/Quest-safe signal stride. |
| Legacy explicit signal structs | 105 explicit layouts without `Pack=1` remain | ABI exception recorded in rationale; union aliases require staged migration. |

## Build Evidence

| Command | Result |
| --- | --- |
| `dotnet restore Hecton8.Core.csproj /nr:false` | PASS; regenerated missing Temp obj NuGet target. |
| `dotnet build Hecton8.Core.csproj -t:Rebuild --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false` | HISTORICAL PASS, 0 warnings, 0 errors. No SignalBus registry or signal payload errors were emitted. |
| `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false /m:1` | CURRENT PASS on 2026-05-17, 0 warnings, 0 errors. Output logged to `Docs/AgentLogs/Build_SIGNAL_AUTHORITY_VALIDATOR_20260517_CurrentAfterFinalReclosure.txt`. |
| `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false /m:1` | CURRENT PASS after used-lane closure on 2026-05-17, 0 warnings, 0 errors, 46.07 seconds. Output logged to `Docs/AgentLogs/Build_SIGNAL_AUTHORITY_VALIDATOR_20260517_UsedLaneClosure.txt`. |
| `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false /m:1` | CURRENT PASS after physical contract and residual lane-policy closure on 2026-05-17, 0 warnings, 0 errors, 25.62 seconds. Output logged to `Docs/AgentLogs/Build_SIGNAL_AUTHORITY_VALIDATOR_20260517_PhysicalContractAndLanePolicyClosure.txt`. |
| `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false` | FAIL outside CORE/SIGNALS: `RealtimeCSG.csproj` references 216 missing third-party source files; 131 third-party warnings were emitted before failure. Signal/core assemblies compile before this wall. |

Audit conclusion: 0 duplicate signal names, 0 signal payload contract/layout violations, 0 `ISignal` payload structs outside `GlobalSignals.cs`, 0 decentralized SignalBus lane Configure calls, and 0 used `SignalBus<T>` types without central policy remain. Current core assembly verification passes with 0 warnings and 0 errors.

Late drift note: final scans caught stale signal imports and local lane authority in compass/anomaly, lockstep/glitch, tether fire, physics determinism, laser cutter events, and a thermal scalability adapter import. They were folded back to `Hecton8.Core.Contracts.Signals` and `GlobalSignals.InitializeAllQueues()` before this audit was finalized.

## Omega Polish

| Question | Result |
| --- | --- |
| 0.1 ms flush dictatorship | Flush path uses bounded native queue drain, NativeList snapshot writes, stress caps, and `NativeQueue<T>.Clear()` on overflow storms. No managed containers were added to the flush path; the unused SPSC fallback now uses `H8Memory.Allocate/Release` instead of direct NativeArray ownership. |
| Managed format strings in signal surface | `rg` found 0 interpolated strings and 0 `string.Format` calls in the signal authority files. |
| Status | VERIFIED MASTER GRADE FOR CORE/SIGNALS STATIC + CORE COMPILE. Current `ISignal` payloads have fixed 16-byte-multiple sizes by validator coverage and targeted checks; `SignalLaneTelemetry` is Pack=1 Size=32; adjacent physics event DTOs, flood mass result, debug visual signal, and acoustic-zone transition signal now use explicit 16-byte-multiple strides; legacy explicit-layout unions remain ABI-retained for offset safety. |

## Finite Guard Coverage

Late Push-time guard expansion covers tether tension/snap/fire, visual flare, voxel carve, docking request/complete/fail, anomaly proximity, compass calibration, system glitch, deterministic input, state correction, sync fence, KCC velocity, laser cutter, splash, physics event, deferred submarine impact, and debug visual payloads. Invalid numeric fields are sanitized to zero or safe defaults and emit math-guard telemetry instead of trusting producers.
