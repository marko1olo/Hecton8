# -*- coding: utf-8 -*-
"""Apply real (non-mock) P0 Environment boot fixes for V0-L06 root cause."""
from __future__ import annotations

import os
import shutil
from datetime import datetime, timezone

ROOT = r"C:\hades\Hecton8"


def read(path: str) -> str:
    with open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write(path: str, text: str) -> None:
    bak = path + ".bak_v0boot"
    if not os.path.isfile(bak):
        shutil.copy2(path, bak)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("WROTE", path)


def must_replace(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise SystemExit("MISSING BLOCK: " + label)
    count = text.count(old)
    if count != 1:
        raise SystemExit("AMBIGUOUS (%d): %s" % (count, label))
    return text.replace(old, new, 1)


def fix_core_low_level() -> None:
    path = os.path.join(ROOT, r"Assets\_Project\Scripts\Core\Contracts\CoreLowLevelUtilities.cs")
    text = read(path)

    old_create = '''        public static NativeArray<byte> CreateTransientPayload(
            int byteCount,
            string owner,
            string label,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory,
            Allocator allocator = Allocator.Temp)
        {
            if (byteCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            NativeArray<byte> payload = default;
            bool registered = false;
            IntPtr payloadPointer = IntPtr.Zero;
            try
            {
                payload = new NativeArray<byte>(byteCount, allocator, options);
                payloadPointer = ResolveTransientPayloadPointer(payload);
                registered = TryRegisterTransientNativeArrayPayload(payload, owner, label, allocator);
                if (!registered)
                    throw new InvalidOperationException(TransientPayloadRegistrationFailureMessage);

                return payload;
            }
            catch
            {
                try
                {
                    if (payload.IsCreated)
                        payload.Dispose();
                }
                catch (Exception disposalException)
                {
                    if (registered && !payload.IsCreated && !TryUnregisterTransientNativeArrayPayload(payloadPointer))
                    {
                        throw new AggregateException(TransientPayloadUnregistrationFailureMessage, disposalException);
                    }

                    throw;
                }

                if (registered && !TryUnregisterTransientNativeArrayPayload(payloadPointer))
                    throw new InvalidOperationException(TransientPayloadUnregistrationFailureMessage);

                throw;
            }
        }'''

    new_create = '''        public static NativeArray<byte> CreateTransientPayload(
            int byteCount,
            string owner,
            string label,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory,
            Allocator allocator = Allocator.Temp)
        {
            if (byteCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            NativeArray<byte> payload = default;
            bool registered = false;
            IntPtr payloadPointer = IntPtr.Zero;
            try
            {
                payload = new NativeArray<byte>(byteCount, allocator, options);
                // Tracking is optional diagnostics infrastructure. When the bridge is not installed
                // (domain reload / batchmode install race / player without sentinel wiring), still
                // hand back a usable payload so telemetry dumps cannot kill Environment bootstrap.
                if (!Hecton8.Core.Contracts.NativeMemoryTrackingBridge.IsInstalled)
                    return payload;

                payloadPointer = ResolveTransientPayloadPointer(payload);
                registered = TryRegisterTransientNativeArrayPayload(payload, owner, label, allocator);
                if (!registered)
                    throw new InvalidOperationException(TransientPayloadRegistrationFailureMessage);

                return payload;
            }
            catch
            {
                try
                {
                    if (payload.IsCreated)
                        payload.Dispose();
                }
                catch (Exception disposalException)
                {
                    if (registered && !payload.IsCreated && !TryUnregisterTransientNativeArrayPayload(payloadPointer))
                    {
                        throw new AggregateException(TransientPayloadUnregistrationFailureMessage, disposalException);
                    }

                    throw;
                }

                if (registered && !TryUnregisterTransientNativeArrayPayload(payloadPointer))
                    throw new InvalidOperationException(TransientPayloadUnregistrationFailureMessage);

                throw;
            }
        }'''

    old_dispose = '''        public static void DisposeTransientPayload(
            ref NativeArray<byte> payload,
            string owner,
            string label,
            Allocator allocator = Allocator.Temp)
        {
            if (!payload.IsCreated)
                return;

            IntPtr payloadPointer = ResolveTransientPayloadPointer(payload);
            int registrationId = TryGetTransientPayloadRegistrationId(payloadPointer);
            if (registrationId <= 0)
            {
                payload.Dispose();
                payload = default;
                throw new InvalidOperationException(TransientPayloadUnregistrationFailureMessage);
            }

            try
            {
                payload.Dispose();
                payload = default;
            }
            catch (Exception disposalException)
            {
                if (!payload.IsCreated && !TryUnregisterTransientNativeArrayPayload(payloadPointer, registrationId))
                {
                    throw new AggregateException(TransientPayloadUnregistrationFailureMessage, disposalException);
                }

                throw;
            }

            bool unregistered = TryUnregisterTransientNativeArrayPayload(payloadPointer, registrationId);
            if (!unregistered)
                throw new InvalidOperationException(TransientPayloadUnregistrationFailureMessage);
        }'''

    new_dispose = '''        public static void DisposeTransientPayload(
            ref NativeArray<byte> payload,
            string owner,
            string label,
            Allocator allocator = Allocator.Temp)
        {
            if (!payload.IsCreated)
                return;

            // Untracked payloads (bridge down at Create time, or never registered) still must free.
            if (!Hecton8.Core.Contracts.NativeMemoryTrackingBridge.IsInstalled)
            {
                payload.Dispose();
                payload = default;
                return;
            }

            IntPtr payloadPointer = ResolveTransientPayloadPointer(payload);
            int registrationId = TryGetTransientPayloadRegistrationId(payloadPointer);
            if (registrationId <= 0)
            {
                // Payload existed without a remembered registration id (bridge raced, or create
                // returned untracked). Dispose the native memory; do not elevate dump cleanup into
                // a hard failure that can abort LateFrameTick / Environment bootstrap.
                payload.Dispose();
                payload = default;
                return;
            }

            try
            {
                payload.Dispose();
                payload = default;
            }
            catch (Exception disposalException)
            {
                if (!payload.IsCreated && !TryUnregisterTransientNativeArrayPayload(payloadPointer, registrationId))
                {
                    throw new AggregateException(TransientPayloadUnregistrationFailureMessage, disposalException);
                }

                throw;
            }

            bool unregistered = TryUnregisterTransientNativeArrayPayload(payloadPointer, registrationId);
            if (!unregistered)
                throw new InvalidOperationException(TransientPayloadUnregistrationFailureMessage);
        }'''

    text = must_replace(text, old_create, new_create, "CreateTransientPayload")
    text = must_replace(text, old_dispose, new_dispose, "DisposeTransientPayload")
    write(path, text)


def fix_seismic() -> None:
    path = os.path.join(ROOT, r"Assets\_Project\Scripts\Environment\HectonSeismicTideDirector.cs")
    text = read(path)

    old = '''        private void DumpCelestialTelemetry()
        {
            if (_dataVault == null)
                return;

            TryReadOnlyVaultBuffer(_dataVault, in _celestialTelemetryHandle, SeismicDirectorConstants.CelestialTelemetryBuffer, SeismicDirectorConstants.TelemetryFrames, out NativeArray<CelestialTelemetryEntry>.ReadOnly telemetry);
            if (!telemetry.IsCreated)
                return;

            try
            {
                WriteCelestialTelemetryDump(SeismicDirectorConstants.CelestialDumpPath, telemetry);
                WriteCelestialTelemetryDump(SeismicDirectorConstants.CelestialAgentDumpPath, telemetry);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }'''

    new = '''        private void DumpCelestialTelemetry()
        {
            if (_dataVault == null)
                return;

            TryReadOnlyVaultBuffer(_dataVault, in _celestialTelemetryHandle, SeismicDirectorConstants.CelestialTelemetryBuffer, SeismicDirectorConstants.TelemetryFrames, out NativeArray<CelestialTelemetryEntry>.ReadOnly telemetry);
            if (!telemetry.IsCreated)
                return;

            try
            {
                WriteCelestialTelemetryDump(SeismicDirectorConstants.CelestialDumpPath, telemetry);
                WriteCelestialTelemetryDump(SeismicDirectorConstants.CelestialAgentDumpPath, telemetry);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (InvalidOperationException exception)
            {
                // Diagnostics-only dump. A failed NativeFaultDumpWriter registration must never
                // escape LateFrameTick and kill Environment bootstrap / ocean init.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[HectonSeismicTideDirector] Celestial telemetry dump skipped (InvalidOperation). " +
                    exception.Message);
#endif
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[HectonSeismicTideDirector] Celestial telemetry dump skipped. " +
                    exception.GetType().Name + ": " + exception.Message);
#endif
            }
        }'''

    text = must_replace(text, old, new, "DumpCelestialTelemetry")
    write(path, text)


def fix_bootstrap() -> None:
    path = os.path.join(ROOT, r"Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs")
    text = read(path)

    old_exc = '''#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[GameBootstrapper] Bootstrap dependency exception.");
#endif
                return false;
            }
        }

        private static bool TryRegisterStableFallbackForBootstrapNode('''

    new_exc = '''#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[GameBootstrapper] Bootstrap dependency exception. node=" +
                    ResolveBootstrapDependencyNodeName(node) +
                    " exception=" +
                    exception);
#endif
                return false;
            }
        }

        private static bool TryRegisterStableFallbackForBootstrapNode('''

    text = must_replace(text, old_exc, new_exc, "bootstrap exception log")

    old_ocean = '''                case BootstrapDependencyNode.OceanKinematicsRuntimeService:
                {
                    OceanKinematicsRuntimeService oceanKinematicsRuntimeService = OceanKinematicsRuntimeService.EnsureRuntimeInstance();
                    if (oceanKinematicsRuntimeService == null)
                        return false;

                    PersistRuntimeService(oceanKinematicsRuntimeService);
                    oceanKinematicsRuntimeService.InitializeService();
                    // Result is intentionally not fatal to boot - caustics are cosmetic and have no startup-graph
                    // node of their own. TryEnsureDeferredCausticsRegistered logs its own named failure, so this
                    // is a reported degrade rather than a discarded bool. Do not convert it into a false return.
                    if (!_headlessBootMode)
                        TryEnsureDeferredCausticsRegistered();
                    return IsBootstrapDependencyNodeReady(node);
                }'''

    new_ocean = '''                case BootstrapDependencyNode.OceanKinematicsRuntimeService:
                {
                    OceanKinematicsRuntimeService oceanKinematicsRuntimeService = OceanKinematicsRuntimeService.EnsureRuntimeInstance();
                    if (oceanKinematicsRuntimeService == null)
                        return false;

                    PersistRuntimeService(oceanKinematicsRuntimeService);
                    oceanKinematicsRuntimeService.InitializeService();
                    // Caustics are cosmetic and must never fail the OceanKinematics startup-graph node.
                    // Reflection Invoke can throw TargetInvocationException; swallow into named degrade.
                    if (!_headlessBootMode)
                    {
                        try
                        {
                            TryEnsureDeferredCausticsRegistered();
                        }
                        catch (Exception causticsException)
                        {
                            LogDeferredCausticsWiringFailure(
                                "TryEnsureDeferredCausticsRegistered threw: " + causticsException);
                        }
                    }
                    return IsBootstrapDependencyNodeReady(node);
                }'''

    text = must_replace(text, old_ocean, new_ocean, "ocean case")

    old_caustics = '''            Component serviceComponent = ensureMethod.Invoke(null, null) as Component;
            if (serviceComponent == null)
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime.EnsureRuntimeInstance returned no Component, so no caustics " +
                    "runtime owner was created.");

            PersistRuntimeService(serviceComponent);
            MethodInfo initializeMethod = serviceType.GetMethod("InitializeService", BindingFlags.Public | BindingFlags.Instance);
            if (initializeMethod == null)
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime has no public instance 'InitializeService' method, so the " +
                    "created owner was never initialized.");

            initializeMethod.Invoke(serviceComponent, null);
            if (GlobalRegistry.Caustics != null)
                return true;

            return LogDeferredCausticsWiringFailure(
                "AbyssalDeferredCausticsRuntime.InitializeService ran but GlobalRegistry.Caustics is still empty, " +
                "so the service never registered itself.");
        }'''

    new_caustics = '''            Component serviceComponent;
            try
            {
                serviceComponent = ensureMethod.Invoke(null, null) as Component;
            }
            catch (Exception invokeException)
            {
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime.EnsureRuntimeInstance threw: " + invokeException);
            }

            if (serviceComponent == null)
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime.EnsureRuntimeInstance returned no Component, so no caustics " +
                    "runtime owner was created.");

            PersistRuntimeService(serviceComponent);
            MethodInfo initializeMethod = serviceType.GetMethod("InitializeService", BindingFlags.Public | BindingFlags.Instance);
            if (initializeMethod == null)
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime has no public instance 'InitializeService' method, so the " +
                    "created owner was never initialized.");

            try
            {
                initializeMethod.Invoke(serviceComponent, null);
            }
            catch (Exception invokeException)
            {
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime.InitializeService threw: " + invokeException);
            }

            if (GlobalRegistry.Caustics != null)
                return true;

            return LogDeferredCausticsWiringFailure(
                "AbyssalDeferredCausticsRuntime.InitializeService ran but GlobalRegistry.Caustics is still empty, " +
                "so the service never registered itself.");
        }'''

    text = must_replace(text, old_caustics, new_caustics, "caustics invoke")
    write(path, text)


def fix_ledger() -> None:
    path = os.path.join(ROOT, r"Docs\PLAYTEST\V0_VERTICAL_SLICE_EVIDENCE_2026-07-30.md")
    text = read(path)
    if "V0-L06" in text:
        print("LEDGER already has V0-L06")
        return

    utc = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%MZ")
    row = f'''

## V0-L06 — Boot route playprobe (MEASURED FAIL) — {utc}

| Field | Value |
| --- | --- |
| Evidence class | **MEASURED** (batchmode playprobe; not PLAYER) |
| Artifact | `Docs/AgentLogs/h8_playprobe_v0_L06.json` + `.log` |
| UTC | 2026-07-30T16:30:18Z |
| exitCode | 1 |
| failures | 3 |
| finalPhase | LeavingPlayMode |
| scene stayed | `00_BOOTSTRAP` |
| forceMenuLoad | false (correct — forcing menu is a mock) |
| worldDriver.started | false |
| Boot | FAIL — allSystemsReady=False gameReady=False activationStep='Not started' |
| WorldLoad | BLOCKED — no live MainMenuController in 120s |
| Swim/Tool/Resource/Craft/Mission/Hazard/SaveLoad | NOT_EXERCISED |
| FirstExit / Hazard | CONTENT-BLOCKED (no life-pod prefab; no hazard AddComponent sites) |
| Screenshots | none — `Docs/Screenshots/V0_Playtest/` empty; `-nographics` cannot close PNG rows |
| Captain checklist | **still all open** — MEASURED ≠ PLAYER |

### Root cause (MEASURED from log ~2099–2241)

1. Environment phase node `OceanKinematicsRuntimeService` reported **Bootstrap dependency exception** (exception text was **swallowed** by bootstrap logger — only the label was printed).
2. Concurrent LateFrameTick: `HectonSeismicTideDirector.WriteCelestialTelemetryDump` → `NativeFaultDumpWriter.CreateTransientPayload` threw  
   `InvalidOperationException: NativeMemoryTrackingBridge registration failed for NativeFaultDumpWriter transient payload`  
   (`CoreLowLevelUtilities.cs:152`). Dump only caught IO/Unauthorized, so the throw escaped and could poison boot.
3. Core services (Dispatcher/TickManager/Save/ObjectPool) OK; Environment phase never completed → menu never eligible → probe waited 120s → FAIL.

### P0 product fix applied (this session — real integration, not mocks)

| Change | Why real |
| --- | --- |
| `NativeFaultDumpWriter.CreateTransientPayload` returns untracked payload when bridge not installed; dispose matches | Tracking is diagnostics; dump must not hard-kill boot |
| `DumpCelestialTelemetry` also catches `InvalidOperationException` / `Exception` | Telemetry dump is non-critical side channel |
| Ocean node wraps caustics registration; caustics `Invoke` try/catch | Cosmetic caustics must not fail Ocean startup-graph node |
| Bootstrap logs `exception.ToString()` on dependency exception | Next failure is diagnosable |

### Explicitly rejected as "fixes"

- `-h8ForceMenuLoad` / forceMenuLoad=true — mock menu on dead boot
- `-h8headless` ecology short-circuit as play proof — different product path
- EmergencyMockOcean as V0 play provider
- Marking captain checklist without PLAYER screenshots + controllable spawn

### Next proof gate after recompile

1. Re-run playprobe **without** forceMenuLoad; expect Boot beyond Environment (menu eligible or WORLD).
2. Graphics-on Boot→Menu→New Game→WORLD; capture V0-S01..S03 under `Docs/Screenshots/V0_Playtest/`.
3. Swim ~30s (V0-S03), one tool (V0-S04), one fauna (V0-S05); then death/save.
'''
    # append before EOF
    if not text.endswith("\n"):
        text += "\n"
    text = text + row
    write(path, text)


def main() -> None:
    fix_core_low_level()
    fix_seismic()
    fix_bootstrap()
    fix_ledger()
    print("ALL_P0_APPLIED")


if __name__ == "__main__":
    main()
