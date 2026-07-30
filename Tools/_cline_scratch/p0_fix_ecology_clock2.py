# Insert missing helper method bodies (v1 skipped because Update already added the name).
from pathlib import Path

RUNNER = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\QA\Headless\HeadlessSimulationRunner.cs")
text = RUNNER.read_text(encoding="utf-8")

if "private void TryArmEcologyWaitClock()" in text:
    print("helpers already present")
else:
    helper = '''
        /// <summary>
        /// Arms the ecology-ready wall clock once bootstrap has opened gameplay ticks.
        /// Uses IsGameReady so headless short-circuit and full ActivatePlayer paths both qualify.
        /// Does not arm during dependency init (p0_gameready burned 180s pre-GameReady).
        /// </summary>
        private void TryArmEcologyWaitClock()
        {
            if (_ecologyWaitStartRealtime > 0.0)
                return;

            // GameReady is the hard signal. HasActiveInstance==false alone is insufficient
            // during early boot before PublishBootstrapPresence(true).
            if (!BootstrapState.IsGameReady)
                return;

            _ecologyWaitStartRealtime = Time.realtimeSinceStartupAsDouble;
            LogRunnerLifecycle("ecology wait clock armed (GameReady)");
        }

        private void LogEcologyBootstrapTimeoutDiagnostics()
        {
            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            bool ecoNull = ecosystem == null;
            bool ecoInit = !ecoNull && ecosystem.IsInitialized;
            bool foFlushClean = HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
            // FailAndQuit muzzles Log after ecologyReady; we are pre-ready so Log is fine.
            LogRunnerLifecycle(
                "BOOTSTRAP_TIMEOUT diag ecoNull=" + (ecoNull ? "1" : "0") +
                " ecoInit=" + (ecoInit ? "1" : "0") +
                " foFlushClean=" + (foFlushClean ? "1" : "0") +
                " gameReady=" + (BootstrapState.IsGameReady ? "1" : "0") +
                " hasBootstrap=" + (BootstrapState.HasActiveInstance ? "1" : "0"));
        }

'''
    marker = "        private void TryMarkEcologyReady()"
    if marker not in text:
        raise SystemExit("FAIL: TryMarkEcologyReady missing")
    text = text.replace(marker, helper + marker, 1)
    RUNNER.write_text(text, encoding="utf-8")
    print("OK inserted helpers")

# Ensure BootstrapContracts namespace is reachable.
# BootstrapState lives in Hecton8.Core.BootstrapContracts or Hecton8.Core?
t2 = RUNNER.read_text(encoding="utf-8")
print("has private void TryArm:", "private void TryArmEcologyWaitClock()" in t2)
print("has private void LogEco:", "private void LogEcologyBootstrapTimeoutDiagnostics()" in t2)
print("BootstrapState count", t2.count("BootstrapState"))
print("TryFlush count", t2.count("TryFlushInitialSceneRebaseBeforeTicks"))

# Find BootstrapState namespace
root = Path(r"C:\hades\Hecton8")
bs = list(root.rglob("BootstrapState.cs"))
for p in bs:
    for i, l in enumerate(p.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        if l.startswith("namespace ") or "class BootstrapState" in l:
            print(f"{p}:{i}:{l.strip()}")
