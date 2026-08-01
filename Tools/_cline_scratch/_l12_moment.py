# -*- coding: utf-8 -*-
import pathlib

t = pathlib.Path(r"C:/hades/Hecton8/Docs/AgentLogs/h8_playprobe_v0_L12.log").read_text(
    encoding="utf-8", errors="replace"
)
out_lines = []
for ln in t.splitlines():
    if "MOMENT" in ln and "Swim" in ln:
        out_lines.append("MOMENT_FULL:")
        out_lines.append(ln)
        out_lines.append("---")
    if "INPUTHOP" in ln:
        out_lines.append("HOP_FULL_LEN %d" % len(ln))
        out_lines.append(ln)
        out_lines.append("---")
    if any(
        k in ln
        for k in (
            "inputEnabled",
            "inputService",
            "FixedTick",
            "hop2",
            "IsMenu",
            "menuOpen",
            "IsPlayerInput",
            "SampleGameplay",
            "NoOp",
            "RegisteredInput",
            "lateFrameTick",
            "overrideRejected",
            "captureSkipped",
        )
    ):
        if "H8_" in ln or "PLAYPROBE" in ln or "WORLDDRIVER" in ln:
            out_lines.append("CTX: " + ln[:500])

out = pathlib.Path(r"C:/hades/Hecton8/Tools/_cline_scratch/_l12_moment.txt")
out.write_text("\n".join(out_lines), encoding="utf-8")
print("WROTE", out, "nlines", len(out_lines))
