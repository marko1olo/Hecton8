#!/usr/bin/env python3
"""Commit + push L13.1 CS0246 FQN fix for WorldDriver."""
from __future__ import annotations

import os
import subprocess
import sys

REPO = r"C:\hades\Hecton8"
MSG_PATH = r"C:\hades\Hecton8\Tools\_cline_scratch\_commit_msg_l13_1.txt"
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_commit_l13_1_out.txt"

MSG = """fix(v0): fully-qualify HectonPlayerMovement in WorldDriver L13 register (CS0246)

L13 commit 8a8290c81 added EnsureDispatcherRegistration call site in
H8_HeadlessWorldDriver.EnsureGameplayLocomotionInputReady using bare
HectonPlayerMovement. Editor assembly has no `using Hecton8.Gameplay`,
so Tundra compile failed CS0246 and the L13 LIVE probe never reached Swim.

Product-only fix: use fully-qualified Hecton8.Gameplay.HectonPlayerMovement
(same pattern as field _movement and SampleObservables line ~1630). Drop
redundant `as` cast. No gameplay behavior change beyond unblocking compile.
"""


def run(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args,
        cwd=REPO,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )


def main() -> int:
    lines: list[str] = []
    open(MSG_PATH, "w", encoding="utf-8", newline="\n").write(MSG)

    # Stage only the driver + docs
    files = [
        "Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs",
        "Docs/V0_Playtest/V0_L13_1_CS0246_FQN.md",
        "Docs/V0_Playtest/NEXT_CHAT_L13.md",
    ]
    for f in files:
        p = os.path.join(REPO, f.replace("/", os.sep))
        if os.path.isfile(p):
            r = run(["git", "add", "--", f])
            lines.append(f"ADD {f} rc={r.returncode}")
            if r.stderr:
                lines.append(r.stderr.strip())
        else:
            lines.append(f"SKIP_MISSING {f}")

    st = run(["git", "status", "-sb"])
    lines.append("STATUS_PRE")
    lines.append(st.stdout)

    # Commit via -F to avoid shell quoting issues
    c = run(["git", "commit", "-F", MSG_PATH])
    lines.append(f"COMMIT rc={c.returncode}")
    lines.append(c.stdout)
    lines.append(c.stderr)

    if c.returncode != 0:
        open(OUT, "w", encoding="utf-8").write("\n".join(lines))
        print("\n".join(lines))
        return c.returncode

    log = run(["git", "log", "-1", "--oneline"])
    lines.append("LOG " + log.stdout.strip())

    # pull --rebase then push
    pull = run(["git", "pull", "--rebase", "gitlab", "main"])
    lines.append(f"PULL rc={pull.returncode}")
    lines.append(pull.stdout)
    lines.append(pull.stderr)
    if pull.returncode != 0:
        open(OUT, "w", encoding="utf-8").write("\n".join(lines))
        print("\n".join(lines))
        return pull.returncode

    push = run(["git", "push", "gitlab", "main"])
    lines.append(f"PUSH rc={push.returncode}")
    # redact any token-looking URLs
    def redact(s: str) -> str:
        import re

        return re.sub(r"://[^/@\s]+@", "://***@", s or "")

    lines.append(redact(push.stdout))
    lines.append(redact(push.stderr))

    log2 = run(["git", "log", "-1", "--oneline"])
    lines.append("HEAD " + log2.stdout.strip())
    st2 = run(["git", "status", "-sb"])
    lines.append(st2.stdout)

    open(OUT, "w", encoding="utf-8").write("\n".join(lines))
    print("\n".join(lines))
    return 0 if push.returncode == 0 else push.returncode


if __name__ == "__main__":
    sys.exit(main())
