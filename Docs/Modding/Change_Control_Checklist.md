# Mod API Change Control Checklist

Status: STATIC_CHANGE_GATE / RUNTIME_PENDING
Owner prompt: MODDING_API_SCHEMA_BUILDER

## Rule

No mod API contract change is complete until the source, schema, audit matrix, runtime playbook, and static validator agree. A public C# type or method is not automatically a mod API right.

## Required Static Gate

Run after every mod API source or doc edit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1
```

Required result: `Status = PASS`.

## Change Matrix

| Change type | Required files | Required proof |
|---|---|---|
| Add/remove any `ISignal` struct | `Signal_Audit_Matrix.md`, `Signal_Schema.json`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md` | Source count, projected count, denied count, validator pass. |
| Add projected `SignalBus<T>` for mods | `Signal_Schema.json.allowedSignalBuses`, `Signal_Audit_Matrix.md`, `Mod_API_Specification.md`, projection bridge source, runtime playbook | Event hash, field projection, cap, finite guards, cull telemetry, Unity GC/profiler proof. |
| Add native byte event kind | `Event_Subscription_Audit_Matrix.md`, `Signal_Schema.json.eventSubscriptionAudit`, `Mod_API_Specification.md`, runtime playbook, `HectonEventBus` bridge source | Immutable byte-copy source owner, callback-scoped span rule, unsubscribe proof, GC proof. |
| Add unmanaged public event payload | `Event_Subscription_Audit_Matrix.md`, `Payload_Layout_Audit_Matrix.md`, `Signal_Schema.json`, runtime playbook | Blittable payload, no Unity/native handles, unsubscribe proof. |
| Add command opcode or target | `Command_Audit_Matrix.md`, `Signal_Schema.json.commandApi`, `Mod_API_Specification.md`, runtime playbook, dispatcher source | Engine-owned kernel, target validation, AUP rule, rejection reason, quota proof. |
| Change public `HectonAPI` facade | `API_Surface_Audit_Matrix.md`, `Signal_Schema.json.apiSurfaceAudit`, `Mod_API_Specification.md`, runtime playbook | Surface classification, hot-path risk, Unity object/native handle exposure check. |
| Change resource/content registration | `Resource_Content_Audit_Matrix.md`, `Signal_Schema.json.resourceContentAudit`, `Mod_API_Specification.md`, runtime playbook | Hash-only resource resolution, cold overlay path, registry capacity, no Unity object return. |
| Change payload byte layout | `Payload_Layout_Audit_Matrix.md`, `Signal_Schema.json.payloadLayoutAudit`, validator source | Fixed size/offsets, version strategy, AOT/Burst compatibility review. |
| Change loader manifest or lifecycle | `Loader_Save_Audit_Matrix.md`, `Signal_Schema.json.loaderSaveAudit`, `Mod_API_Specification.md`, runtime playbook | API version, manifest fields, callback order, unload/dispose proof. |
| Change mod save payload boundary | `Loader_Save_Audit_Matrix.md`, `Signal_Schema.json.loaderSaveAudit`, runtime playbook, save storage source | Payload cap, namespace prefix, active scope, no first-party save-owner mutation. |
| Change runtime verification criteria | `Runtime_Verification_Playbook.md`, `Signal_Schema.json.staticValidation`, this checklist | Exact Unity steps, GC/profiler evidence format, failure handling. |
| Change sample mod spec | `Sample_InfiniteO2_Mod.md`, `Mod_API_Specification.md`, `Signal_Schema.json.sampleModSpecs`, validator source | Public facade signatures, no direct gameplay authority, future kernel requirements. |

## Hard Stops

- Schema-only expansion is invalid.
- Markdown-only expansion is invalid.
- Runtime-verified status is invalid without Unity Console, GCMonitor, and profiler evidence.
- New event or command strings are invalid; use numeric hashes/enums.
- Direct `SignalBus<T>`, `NativeQueue`, `NativeArray`, DataVault, `GameObject`, `Transform`, prefab, material, texture, or audio clip exposure to mods is invalid.

## Audit Files

- `Docs/Modding/Signal_Audit_Matrix.md`
- `Docs/Modding/Command_Audit_Matrix.md`
- `Docs/Modding/API_Surface_Audit_Matrix.md`
- `Docs/Modding/Payload_Layout_Audit_Matrix.md`
- `Docs/Modding/Loader_Save_Audit_Matrix.md`
- `Docs/Modding/Event_Subscription_Audit_Matrix.md`
- `Docs/Modding/Resource_Content_Audit_Matrix.md`
- `Docs/Modding/Runtime_Verification_Playbook.md`
- `Docs/Modding/Sample_InfiniteO2_Mod.md`
