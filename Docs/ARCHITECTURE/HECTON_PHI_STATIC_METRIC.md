# Hecton Phi Static Metric

Date: 2026-05-24
Owner domain: architecture/static metric audit
Status: STATIC METRIC CONTRACT / RUNTIME PROOF REQUIRED
Evidence class: STATIC_SOURCE

Full historical metric snapshot: `../_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/ARCHITECTURE_APEX_PRE_FILE_CAP_HECTON_PHI_STATIC_METRIC.md`.

## Tool

```powershell
Tools/Architecture/HectonPhiAudit.ps1
```

H-Phi is a static hygiene metric for coupling, tick discipline, ownership, and layout discipline. It is not profiler, compile, Unity Console, visual, or player proof.

## Runtime Scan Scope

| Scope | Rule |
|---|---|
| included | `Assets/_Project/Scripts/**/*.cs` |
| excluded | `Scripts/Editor` and stripped `#if UNITY_EDITOR` blocks |
| optional | `-LexicalScrub` masks comments and literals; not default monitoring input |
| reports | dated JSON is snapshot only |

## Core Formulae

| Score | Formula |
|---|---|
| `HPhiStaticNarrow` | `NarrowIntegration * ArchitecturalPurity * DataSovereignty * MemoryAlignment` |
| `HPhiStaticRisk` | `RiskIntegration * ArchitecturalPurity * DataSovereignty * MemoryAlignment * AupPrecisionIntegrity` |

Ratios return `0.0` on zero denominator. Tool rounds to nine decimals.

## Coefficients

| Coefficient | Meaning |
|---|---|
| `NarrowIntegration` | typed signal use versus registry get pressure |
| `RiskIntegration` | signal use versus registry, events, static instances, scene finds, component gets |
| `ArchitecturalPurity` | dispatcher/job discipline versus non-exempt Unity loop methods |
| `ArchitecturalPurityExpanded` | tick-interface-inclusive purity |
| `DataSovereignty` | Vault/access ownership versus scattered native buffers |
| `MemoryAlignment` | explicit layout coverage |
| `BinarySafeRatio` | explicit binary-safe coverage; diagnostic only |
| `AupPrecisionIntegrity` | double-safe AUP bridge usage versus float/offset-risk surfaces |

## Counter Families

| Family | Counter examples |
|---|---|
| signal | `SignalBusPush`, typed `GlobalSignals.Publish(...)`, confirmed SystemDispatcher-backed lanes |
| registry | `GlobalRegistry.Get<T>`, `GlobalRegistry.` surface |
| legacy events | `HectonEventBus`, `WaterTransitionEvents`, `SuitDamageEvents` |
| loops | non-exempt `Update`, `LateUpdate`, `FixedUpdate`; `Core/SystemDispatcher.cs` shell is tracked separately |
| data | `GlobalDataVault`, `IDataVault`, `VaultBufferHandle<T>`, `NativeArray<T>` |
| layout | `struct`, `[StructLayout(...)]`, `[BinaryBlittableSafe]` |
| AUP | double-safe AUP calls versus legacy float/root-offset risks |
| scene discovery | `FindObject*`, `GameObject.Find`, `GetComponent<T>` |

## Anti-Gaming Rules

- `SignalBusPush` increases count only when owner, phase, capacity, overflow, retention, layout, and telemetry are documented.
- `DataVaultRefs` increases count only when `BufferID`, `SystemID`, generation, lifetime, disposal, and stale-handle behavior are documented.
- `GlobalRegistry` removal is not valid if it hides ownership in direct singleton fields or unmanaged side channels.
- H-Phi movement is a triage signal, not route approval.

## Evidence Language

Use `STATIC_SOURCE` unless a current build/runtime/profiler artifact is linked with command, timestamp, environment, and output.
