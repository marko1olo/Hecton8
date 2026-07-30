from pathlib import Path
p = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\QA\Headless\HeadlessSimulationRunner.cs")
t = p.read_text(encoding="utf-8")
lines = t.splitlines()
print("lines", len(lines))
print("--- first 35 ---")
for i, l in enumerate(lines[:35], 1):
    print(f"{i}:{l}")
print("--- key hits ---")
for i, l in enumerate(lines, 1):
    if any(k in l for k in (
        "_ecologyWaitStartRealtime",
        "TryArmEcologyWaitClock",
        "LogEcologyBootstrapTimeoutDiagnostics",
        "TryFlushInitialSceneRebase",
        "ecology wait clock",
        "BOOTSTRAP_TIMEOUT",
        "BootstrapState",
        "IEcosystemDirectorService",
    )):
        print(f"{i}:{l}")
# compile-ish: ensure braces balance around helpers
print("TryFlush count", t.count("TryFlushInitialSceneRebaseBeforeTicks"))
print("BootstrapState count", t.count("BootstrapState"))
