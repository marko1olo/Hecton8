# -*- coding: utf-8 -*-
"""Extract clean H8_WORLDDRIVER / probe lines from L14 log (no Burst PDB noise)."""
import json
import os
import re

LOG = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L14.log"
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_l14_clean_extract.txt"
JSON_ART = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L14.json"

def main():
    data = open(LOG, "rb").read().decode("utf-8", errors="replace")
    keep = []
    for ln in data.splitlines():
        if "BurstCache" in ln or "SymType" in ln or ".dll:" in ln:
            continue
        if re.search(
            r"H8_WORLDDRIVER|H8_PLAYPROBE|INPUTHOP|movementIntent|MOMENT |RESULT |waitingOn|"
            r"lastOverride|currentStateMove|immersionMax|readHop|hop2|PHASE |Swim |"
            r"Locomotion|EnsureGameplay|registeredFixed|FixedTick census|overrideCount|"
            r"PublishLocomotion|SampleObservables|GetState|TryReadFrame",
            ln,
            re.I,
        ):
            keep.append(ln[:500])
    # de-dupe preserve order
    seen = set()
    uniq = []
    for ln in keep:
        if ln not in seen:
            seen.add(ln)
            uniq.append(ln)
    text = "\n".join(uniq[-200:]) + "\n"
    open(OUT, "w", encoding="utf-8").write(text)
    print("clean_lines", len(uniq), "wrote", OUT)
    # print last 80
    for ln in uniq[-80:]:
        print(ln[:240])
    if os.path.isfile(JSON_ART):
        try:
            j = json.load(open(JSON_ART, encoding="utf-8"))
            print("---JSON_KEYS---", list(j.keys())[:40] if isinstance(j, dict) else type(j))
            s = json.dumps(j, indent=2)[:4000]
            print(s)
        except Exception as e:
            print("json err", e)

if __name__ == "__main__":
    main()
