from pathlib import Path
import sys


def patch_bootstrapper() -> bool:
    p = Path(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs")
    t = p.read_text(encoding="utf-8")
    old = (
        "                        BootstrapStatus.MarkMainMenuReached();\n"
        '                        Debug.Log("[GameBootstrapper-DEBUG] Headless SceneActivate short-circuit: MarkMainMenuReached on bootstrap");\n'
        "                        return true;"
    )
    new = (
        "                        BootstrapStatus.MarkMainMenuReached();\n"
        "                        // Headless short-circuit previously only MarkMainMenuReached.\n"
        "                        // SystemDispatcher.ShouldSkipLaneDuringBootstrap skips PriorityLayer.Player\n"
        "                        // while !BootstrapState.IsGameReady. LateFrame biomass drain is on Player\n"
        "                        // (HeadlessSimulationRunner), so ecology never advances without this.\n"
        "                        // Full ExecuteSceneActivationAsync publishes GameReady~7749; headless must mirror.\n"
        "                        BootstrapState.PublishGameReady(true);\n"
        "                        BootstrapState.PublishBootstrapPresence(false);\n"
        '                        Debug.Log("[GameBootstrapper-DEBUG] Headless SceneActivate short-circuit: MarkMainMenuReached + PublishGameReady on bootstrap");\n'
        "                        return true;"
    )
    if old not in t:
        idx = t.find("Headless SceneActivate short-circuit")
        print("BOOTSTRAPPER_OLD_NOT_FOUND idx=", idx)
        if idx >= 0:
            print(repr(t[max(0, idx - 120) : idx + 220]))
        # already patched?
        if "MarkMainMenuReached + PublishGameReady on bootstrap" in t:
            print("BOOTSTRAPPER_ALREADY_PATCHED")
            return True
        return False
    p.write_text(t.replace(old, new, 1), encoding="utf-8", newline="\n")
    print("BOOTSTRAPPER_PATCH_OK")
    return True


def patch_runner() -> bool:
    p = Path(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs")
    t = p.read_text(encoding="utf-8")
    old = (
        "        private void Update()\n"
        "        {\n"
        "            if (!_awaitingDispatcher || _finished)\n"
        "                return;\n"
        "\n"
        "            TryCompleteDispatcherWait();\n"
        "        }"
    )
    new = (
        "        private void Update()\n"
        "        {\n"
        "            if (_finished)\n"
        "                return;\n"
        "\n"
        "            // Wall-clock ecology/bootstrap timeout must NOT depend on ColdTick.\n"
        "            // ColdTick only fires after lanes are registered AND dispatcher cadence\n"
        "            // is unlocked. p0_dispfix (2026-07-30) proved lanes registered then\n"
        "            // BATCH_TIMEOUT with zero BOOTSTRAP_TIMEOUT — ticks were starved\n"
        "            // (Player LateFrame gated on !IsGameReady; possible origin-shift lock).\n"
        "            // Poll here so a stall always produces a named FailAndQuit instead of\n"
        "            // letting the batch runner win with BATCH_TIMEOUT.\n"
        "            if (_started &&\n"
        "                !_ecologyReady &&\n"
        "                Time.realtimeSinceStartupAsDouble - _startupTime > _startupTimeoutSeconds)\n"
        "            {\n"
        '                FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");\n'
        "                return;\n"
        "            }\n"
        "\n"
        "            if (!_awaitingDispatcher)\n"
        "                return;\n"
        "\n"
        "            TryCompleteDispatcherWait();\n"
        "        }"
    )
    if old not in t:
        idx = t.find("private void Update()")
        print("RUNNER_OLD_NOT_FOUND idx=", idx)
        if idx >= 0:
            print(repr(t[idx : idx + 220]))
        if "Wall-clock ecology/bootstrap timeout must NOT depend on ColdTick" in t:
            print("RUNNER_ALREADY_PATCHED")
            return True
        return False
    p.write_text(t.replace(old, new, 1), encoding="utf-8", newline="\n")
    print("RUNNER_PATCH_OK")
    return True


def main() -> int:
    ok1 = patch_bootstrapper()
    ok2 = patch_runner()
    return 0 if (ok1 and ok2) else 1


if __name__ == "__main__":
    sys.exit(main())
