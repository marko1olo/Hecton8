from pathlib import Path
root = Path(r"C:\hades\Hecton8")
src = root / r"Tools\_cline_scratch\_l17_extract.txt"
dst = root / r"Tools\_cline_scratch\_l17_e_grep.txt"
t = src.read_text(encoding="utf-8")
want = (
    "ReleaseOrigin", "RequestOrigin", "ShouldSkip", "RunFixedStep", "gate @",
    "HSR", "PR:", "EnsureProbe", "EnsureHeadless", "TryFlush", "LateFrameTick",
    "PreSimulationInputTick", "ResumePhysics", "CompleteScene", "sceneRebase",
    "LOG tight", "SIMCLOCK", "INPUTHOP", "MOMENT", "SimulationHalted",
    "EnableStepBounded", "RequestSimulationPause", "DrainSimulation",
    "EnableStepBoundedTime", "blockGameplay", "dilatedDelta",
)
out = []
for l in t.splitlines():
    if any(w in l for w in want):
        out.append(l[:240])
dst.write_text("\n".join(out), encoding="utf-8")
print("OK", len(out), dst.stat().st_size)
