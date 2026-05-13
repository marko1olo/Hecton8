# HECTON_PHI_REPORT

Agent: HECTON_PHI_MONITOR
Domain: ECHELON 9 / Meta, Polish & Integration / Architecture Metrics
Status: H-PHI VERIFIED FOR STATIC-SOURCE AUDIT / RUNTIME PENDING VERIFICATION
Evidence Class: STATIC_SOURCE + STATIC_DOC + CLI_COMPILE_ATTEMPT
Date: 2026-05-13

## Evidence Window

Primary scan scope: `Assets/_Project/Scripts/**/*.cs`
Completed snapshot: 1,384 C# files, 873,077 source lines.
Concurrent-change warning: later live count moved to 1,416 C# files; a final full rescan timed out at 120 s. Treat this report as a completed static snapshot, not a frozen workspace truth.

Commands used:

```powershell
Get-Content C:\hades\Hecton8\AGENTS.md
Get-Content C:\hades\Hecton8\Docs\Actual Domains of Project.txt
Get-ChildItem C:\hades\Hecton8\.agents-skills -File
Select-String / regex extraction from Docs\Tasks\CURRENT_BATCH.md for HECTON_PHI_MONITOR
rg / PowerShell regex scan over Assets\_Project\Scripts
dotnet restore .\Hecton8.Core.csproj
dotnet build .\Hecton8.Core.csproj --no-restore
```

Batch prompt result: `Docs/Tasks/CURRENT_BATCH.md` did not contain `<AGENT_PROMPT id="HECTON_PHI_MONITOR">`. The in-chat XML prompt is the only complete assignment source.

## H-Phi Scores

| Coefficient | Static Formula Used | Count | Score |
|---|---:|---:|---:|
| Synaptic Density, narrow prompt metric | `SignalBus<T>.Push / (SignalBus<T>.Push + GlobalRegistry.Get<T>)` | 36 / 37 | 0.973 |
| Synaptic Density, risk-adjusted registry surface | `(SignalBus.Push + EventBus.Publish) / (+ GlobalRegistry.)` | 62 / 4,991 | 0.012 |
| Architectural Purity | `(ISlowTickable + IJob) / (+ Update-family)` | 526 / 528 | 0.996 |
| Architectural Purity, expanded tick view | `(ITickable + IFixedTickable + ISlowTickable + IJob) / (+ Update-family)` | 896 / 898 | 0.998 |
| Data Sovereignty | `GlobalDataVault / (GlobalDataVault + NativeArray<T>)` | 8 / 6,671 | 0.001 |
| Memory Alignment | DTO-like structs with `[StructLayout] / DTO-like structs` | 332 / 621 | 0.535 |

Static H-Phi, prompt-narrow:

```text
H-Phi_static = 0.973 * 0.996 * 0.001 * 0.535 = 0.00062
```

Verdict: the project is strong on tick discipline and weak on Data Vault sovereignty by the prompt formula. The global score collapses because `GlobalDataVault` adoption is near-zero compared with scattered `NativeArray<T>` ownership. This is a static smell, not proof every native allocation is wrong.

## Consciousness Map

Bright zones:

| Domain / System | Evidence | Why Bright |
|---|---|---|
| Core signal spine | 24 `SignalBus<T>.Push` in Core, one `GlobalRegistry.Get<T>` in all first-party scripts | The mandatory narrow synaptic metric is structurally strong. |
| Tick/job architecture | 2 Update-family methods, 896 tick/job interface hits | First-party runtime largely avoids raw per-MonoBehaviour Update. The two Update/LateUpdate methods are in `Core/SystemDispatcher.cs`, the central dispatcher. |
| Tools / VFX / Interaction / Logistics | zero Update-family hits in the completed snapshot; DTO alignment mostly clean in small domains | These domains are compact and do not dominate registry or DTO debt. |

Dark zones:

| Domain / System | Evidence | Why Dark |
|---|---|---|
| `_RootScripts` mega-domain | 336 files, 292,746 lines, 1,488 `GlobalRegistry.` refs, 185 DTO-like structs with only 75 aligned | Domain boundary is weak. Large root scripts hide ownership and drag differentiation down. |
| World | 163 files, 123,632 lines, 1,600 `NativeArray<T>` refs, 117 DTO-like structs with 66 aligned | Job/native density is high, but Data Vault sovereignty is low and DTO alignment is incomplete. |
| Editor | 180 files, 76,850 lines, 46 Find API hits, 245 GetComponent hits | Mostly editor-side, so not runtime proof of debt, but it pollutes architecture scans and needs quarantine clarity. |
| Audio | `PlayerCriticalProceduralAudioRenderer.cs` has 8,530 lines, 214 `NativeArray<T>` refs, 17 DTO-like structs with 4 aligned | High-value system with heavy local state and poor alignment ratio. |
| Save DTO layer | `SaveData.cs` has 37 DTO-like structs, 1 aligned | Persistence data has the highest layout-fragmentation risk in the scan. |

## Top 3 Systems Dragging H-Phi

1. `_RootScripts` monolith cluster
   Evidence: `HectonPlayerMovement.cs` 12,928 lines, `WorldProceduralScatterDirector.cs` 11,908 lines, `HectonVoxelEngine.cs` 8,673 lines, `HectonUnderwaterVisuals.cs` 7,134 lines.
   Damage: low differentiation. Domain ownership is encoded by file lore, not folder/asmdef boundary.

2. Save/persistence DTO alignment
   Evidence: `SaveData.cs` has 36 DTO-like structs missing nearby `[StructLayout]`.
   Damage: binary layout risk, save migration ambiguity, Burst/native copy distrust.

3. Data sovereignty gap
   Evidence: 6,663 `NativeArray<T>` references versus 8 `GlobalDataVault` references.
   Damage: ownership may be correct locally, but H-Phi cannot see a unified data organism. The metric reads scattered organs, not central circulation.

## Mathematical Surgery

1. Split root-script monoliths by ownership without public API mutation.
   Method: create thin facades only where interfaces already exist; move implementation behind existing `GlobalRegistry` interfaces or typed `SignalBus<T>` lanes. No signature mutation in `Hecton8.Core.Contracts` during batch.
   Rejected: broad refactor loop. Too risky under 20+ active agents.

2. Add a DTO layout gate.
   Method: CI/static analyzer checks DTO-like structs for `[StructLayout(LayoutKind.Sequential)]` or an explicit waiver comment. Start with `SaveData.cs`, `PlayerCriticalProceduralAudioRenderer.cs`, and `World/HectonMapMagicVegetationBridge.cs`.
   Rejected: manual spreadsheet tracking. It will rot.

3. Convert Data Vault from slogan to measurable adoption.
   Method: count buffer leases by `SystemID`, owner, bytes, and lifetime; expose a cold audit method. Do not move every `NativeArray<T>` blindly. Migrate only duplicated or cross-domain buffers first.
   Rejected: declaring all local native storage illegal. Native Memory mandate allows owned NativeArrays when lifetime and job handles are correct.

4. Separate prompt metric from operational metric.
   Method: keep narrow H-Phi for trend continuity, but add `H-Phi_RiskAdjusted` using all `GlobalRegistry.` refs, unaligned DTO count, and compile status.
   Rejected: one vanity number. It hides coupling.

## Regression Model

CPU: static audit adds no runtime CPU. Proposed future fixes must prove no new hot-path registry reads and no mid-frame `JobHandle.Complete`.

GC: no runtime code changed. Future DTO/layout analyzer runs in editor/CI only.

Memory: no runtime memory changed. Future Data Vault migration can reduce duplicate persistent buffers only after owner-by-owner audit.

Cadence: no tick cadence changed. The only Update-family methods found are dispatcher-owned.

Correctness: compile is currently blocked before Unity verification. Representative missing contracts: `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `BinaryBlittableSafeAttribute`, `SoundEmissionSignal`, `BrineLayerSample`, `MacroSwarm`, and acoustic propagation types.

## Verification

`dotnet restore .\Hecton8.Core.csproj`: succeeded.

`dotnet build .\Hecton8.Core.csproj --no-restore`: failed with 124 errors. This report did not edit runtime source. Compile failure is recorded as `[BLOCKED BY DEPENDENCY]`, not as caused or fixed by HECTON_PHI_MONITOR.

Unity Console / PlayMode / Profiler / GCMonitor: not verified. Static scan cannot prove runtime 0 GC, frame time, scene wiring, or memory stability.

Final status: H-PHI VERIFIED for static-source audit. Runtime H-Phi remains PENDING VERIFICATION until compile and Unity evidence exist.
