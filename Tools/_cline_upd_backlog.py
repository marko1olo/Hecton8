# -*- coding: utf-8 -*-
from pathlib import Path

p = Path(r"C:\Users\Admin\.claude\projects\c--hades\work-memory\dialogs\20260730_cline-hecton8-orchestrator\BACKLOG.md")
t = p.read_text(encoding="utf-8")

old = "[x] P1 | NEVER_COMPILE_TESTS dark asmdefs | Hecton8.EditModeTests/PlayModeTests | d5689745e UNITY_INCLUDE_TESTS"
new = (
    "[x] P1 | NEVER_COMPILE_TESTS dark asmdefs | EditModeTests=NEVER_COMPILE_TESTS "
    "(e29ab1438 reverts d5689745e 174CS batch-break); PlayMode still UNITY_INCLUDE_TESTS | e29ab1438"
)
if old in t:
    t = t.replace(old, new)
    print("fixed NEVER_COMPILE line")
else:
    print("WARN: NEVER_COMPILE line not found exact")

t = t.replace("ahead 35 behind 27", "ahead 37 behind 27")
t = t.replace("main ahead 35 behind 27", "main ahead 37 behind 27")

oh = (
    "[ ] P0 | Headless 5-day validate post-fence-fix | batchmode -h8headlessDays 5 | "
    "status!=ECOLOGY_UNAVAILABLE; sampledDayCount>0; timeDilationDelivered>0"
)
nh = (
    "[~] P0 | Headless 5-day validate post-fence-fix | batchmode -h8headlessDays 5 | "
    "LAUNCHED 2026-07-30~11:47 RunUnityBatchGate attempts=2 log=Logs/headless_ecology_fence_5day.log; proof JSON pending"
)
if oh in t:
    t = t.replace(oh, nh)
    print("marked headless ecology in-flight")
else:
    print("WARN: headless ecology line not found")

og = (
    "[ ] P0 | Headless sim 5 days validate timeDilationDelivered>0 biomass alive | "
    "Hecton8 batchmode -h8headless | HeadlessSimulationResult_*.json fields"
)
ng = (
    "[~] P0 | Headless sim 5 days validate timeDilationDelivered>0 biomass alive | "
    "Hecton8 batchmode -h8headless | LAUNCHED gate; proof pending"
)
if og in t:
    t = t.replace(og, ng)
    print("marked goal headless in-flight")
else:
    print("WARN: goal headless line not found")

note = (
    "\n- cycle 3 (2026-07-30 ~11:47): e29ab1438 restored NEVER_COMPILE_TESTS on EditModeTests "
    "after d5689745e caused 174 CS / batch exit 1. Headless 5-day ecology fence validate LAUNCHED. "
    "Geology p95 still open.\n"
)
if "cycle 3" not in t:
    t = t.replace("## Closed this session", note + "\n## Closed this session")
    print("added cycle 3 note")

row = "| e29ab1438 | restore NEVER_COMPILE_TESTS on EditModeTests (revert d5689745e batch-break) |\n"
if "e29ab1438 | restore" not in t:
    t = t.rstrip() + "\n" + row
    print("added closed row")

p.write_text(t, encoding="utf-8")
print("OK bytes", len(t))
