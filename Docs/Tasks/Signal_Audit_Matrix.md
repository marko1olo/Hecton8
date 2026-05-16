# Signal Audit Matrix - SIGNAL_AUTHORITY_VALIDATOR

Date: 2026-05-16
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

## Layout Audit

| Item | Result | Action |
| --- | --- | --- |
| External signal structs moved from feature namespaces | 0 outside contract namespace | Done |
| Newly evicted external signal structs | Sequential Pack=1 fixed-size | Done |
| Legacy explicit signal structs | 130 explicit layouts remain | ABI exception recorded in rationale; union aliases require staged migration. |

## Build Evidence

| Command | Result |
| --- | --- |
| `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` | Succeeded: 0 warnings, 0 errors, `Hecton8.Core.dll` emitted. |
| `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` | Failed outside CORE/SIGNALS with `MSB4166` child-node shutdown and `MSB4242` SDK resolver failures in third-party project graph entries including GPUInstancer, RealtimeCSG, MoreMountains, and VolumetricLightBeam; leftover dotnet worker stopped. |

Audit conclusion: 0 duplicate signal names, 0 signal payloads outside `Hecton8.Core.Contracts.Signals`, and 0 decentralized SignalBus lane Configure calls remain.

Late drift note: final scans caught stale signal imports and local lane authority in compass/anomaly, lockstep/glitch, tether fire, physics determinism, and a thermal scalability adapter import. They were folded back to `Hecton8.Core.Contracts.Signals` and `GlobalSignals.InitializeAllQueues()` before this audit was finalized.

## Omega Polish

| Question | Result |
| --- | --- |
| 0.1 ms flush dictatorship | Flush path uses bounded native queue drain, NativeList snapshot writes, stress caps, and `NativeQueue<T>.Clear()` on overflow storms. No managed containers were added to the flush path. |
| Managed format strings in signal surface | `rg` found 0 interpolated strings and 0 `string.Format` calls in the signal authority files. |
| Status | VERIFIED MASTER GRADE, except task 4 ABI layout migration and task 18 integration build are explicitly blocked as recorded. |

## Finite Guard Coverage

Late Push-time guard expansion covers tether tension/snap/fire, visual flare, voxel carve, docking request/complete/fail, anomaly proximity, compass calibration, system glitch, deterministic input, state correction, sync fence, and KCC velocity payloads. Invalid numeric fields are sanitized to zero or safe defaults and emit math-guard telemetry instead of trusting producers.
