# SHINOBU_107 Static Gate Residual Index

Source summary: `Docs/Reports/SHINOBU_107_StaticScan/SHINOBU_140_STATIC_GATE_SUMMARY.json`

## Current Counts

- Total critical: `352`
- Total warnings: `24`
- Regression critical: `0`
- Regression warnings: `0`
- Status: `PENDING VERIFICATION`

## Red Buckets

| Scanner | Critical | Warning | SHINOBU_107 Artifact | Current Interpretation |
| --- | ---: | ---: | --- | --- |
| `Burst_Job_Directives` | 272 | 0 | `SHINOBU_107_REMAINING_BURST_TRIAGE.md`, `SHINOBU_107_BURST_EXACT_ROUTE_AUDIT.md` | Deterministic-mode ownership/classifier debt: 269 synchronous Deterministic/Standard rows plus 3 Fast rows in untracked or in-flight ocean/save sources. |
| `Compile_Wall` | 71 | 0 | `SHINOBU_107_COMPILE_WALL_ROUTE_TRIAGE.md` | Live Core route dependencies. Needs contract/adapter migration, not import deletion. |
| `Runtime_Struct_Layout` | 9 | 0 | `SHINOBU_107_RUNTIME_STRUCT_LAYOUT_TRIAGE.md` | Serialized authoring schemas plus one persistent save DTO. Needs owner migration/version proof. |
| `Dev_Virtualization` | 0 | 24 | `SHINOBU_107_DEV_VIRTUALIZATION_TRIAGE.md` | Warning-only managed interface registries/callback caches. Power graph has real owner-domain managed dispatch debt. |

## Green Buckets

`AUP_Compliance`, `Vault_Sovereignty`, `Hot_Registry_Polling`, `Hot_Helper_Registry_Polling`, `Mid_Frame_Complete`, `Hot_Helper_Complete`, `Signal_Bus_Topology`, `Rollback_Fence_Compliance`, `Self_Audit_Proof`, and `Static_Gate_Regression` are at zero criticals and zero warnings in the current summary.

## Build Gate

No `dotnet build` or rebuild was launched by SHINOBU_107 after this index. The gate remains blocked until:

- CPU is below the project threshold.
- no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` is running.
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` exists again or the owning World compile blocker is otherwise resolved.

## Next Safe Work

- Do not convert deterministic Burst jobs to `FloatMode.Fast` without owner proof.
- Do not delete Core route imports without replacing type ownership through contracts.
- Do not change serialized bool fields without migration/version proof.
- Do not hide interface containers in wrapper structs unless virtual dispatch is actually removed.
