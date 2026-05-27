# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `BLOCKED_BASELINE_MISSING`
Source root: `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 0 |
| Allowed allocator-internal constructors | 0 |
| Forbidden system constructors | 0 |
| Runtime forbidden constructors | 0 |
| Editor/offline forbidden constructors | 0 |
| Editor/offline transient scratch constructors | 0 |
| Files with forbidden constructors | 0 |
| Editor/offline session scratch declarations | 0 |
| Editor/offline persistent preview declarations | 0 |
| Total field-like `NativeArray<T>` declarations | 0 |
| Allowed DataVault/H8Memory declarations | 0 |
| Forbidden system declarations | 0 |
| Persistent owner native collection declarations | 0 |
| Job input native collection declarations | 0 |
| Burst job input native collection declarations | 0 |
| Native view/payload/kernel struct declarations | 0 |
| Unknown struct native collection declarations | 0 |
| Files with forbidden declarations | 0 |

## Regression Findings

- Baseline missing; runtime no-regression gate fails closed.

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 0 | none | |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 0 | none | |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
