# APEX PASS 4 - 1302 Dump Route

Scope: `VehicleComponentDamageRuntime.cs`, `AsyncBuoyancyReadbackJobs.cs`.
Build launched: no.
Dotnet build launched: no.

## Code Change

- `VehicleComponentDamageRuntime.cs:24-25` adds fixed fault hashes: `VDFT`, `VSFT`.
- `VehicleComponentDamageRuntime.cs:903-904` routes vehicle fatal damage fault to `GlobalTelemetryBus.PushEvent(...)` and `GlobalTelemetryBus.TryDumpBlackboxNow(...)`.
- Deleted local `TryWriteBlackBoxDump(...)` from `VehicleComponentDamageRuntime.cs`; removed local `_dumpPath` and `DumpRelativePath`.

## Static Result

- Local Physics fault dump `FileStream` removed: yes.
- Remaining touched-source `FileStream`: `VehicleComponentDamageRuntime.cs:830`, editor CSV layout loader only.
- Remaining touched-source managed catches: `VehicleComponentDamageRuntime.cs:851`, `VehicleComponentDamageRuntime.cs:855`, editor CSV layout loader only.
- Runtime AUP patch remains double-first: `VehicleComponentDamageRuntime.cs:1018-1023`, `AsyncBuoyancyReadbackJobs.cs:190`.

## Residual Hard Limit

`GlobalTelemetryBus.TryDumpBlackboxNow` is Core-owned and still writes through managed `Directory`/`FileStream` internally. I did not hide this as Zero-GC native IO. A literal no-managed-exception crash writer still requires a Core/native dump plugin route. Physics now no longer owns the local vehicle dump writer.
