#!/usr/bin/env python3
"""Commit + push L14 Player-lane dispatch + Sample intent metric."""
from __future__ import annotations

import os
import re
import subprocess
import sys

REPO = r"C:\hades\Hecton8"
MSG_PATH = r"C:\hades\Hecton8\Tools\_cline_scratch\_commit_msg_l14.txt"
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_commit_l14_out.txt"

MSG = """fix(v0): Player lane always dispatches + Sample publishes intent (L14)

L13.1 LIVE still had hop2 ABSENT and movementIntent01max=0 while hop1/overrides
were healthy. Two product roots:

1) SystemDispatcher.ShouldSkipLaneDuringBootstrap skipped PriorityLayer.Player
   while !BootstrapState.IsGameReady, starving HPM.FixedTick -> Sample ->
   GetState (hop2) even when InputDispatcher held non-zero MoveDelta.
2) CurrentMovementIntent01 reads _lastPlayerKinematicsIntendedMovement written
   only post-suit in PrepareTransport; Sample never published raw intent, so
   probe intent stayed 0 on suit early-out.

L14: never skip Player lane during bootstrap; publish ResolveRawInputIntentVector
at Sample (and zero on menu block). Registration stays sticky-only — do not
Unregister+Register every WorldDriver Ensure tick (bucket.TryRegister is not
idempotent-true when Contains).

Swim PASS still requires LIVE probe (hop2 present, movementIntent01max>0).
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


def redact(s: str) -> str:
    return re.sub(r"://[^/@\s]+@", "://***@", s or "")


def main() -> int:
    lines: list[str] = []
    open(MSG_PATH, "w", encoding="utf-8", newline="\n").write(MSG)

    # Reset any accidental staged junk from prior sessions
    r0 = run(["git", "reset", "HEAD"])
    lines.append(f"RESET rc={r0.returncode}")
    if r0.stdout:
        lines.append(r0.stdout[:500])
    if r0.stderr:
        lines.append(r0.stderr[:500])

    files = [
        "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
        "Assets/_Project/Scripts/HectonPlayerMovement.cs",
        "Docs/V0_Playtest/V0_L14_PLAYER_LANE_AND_SAMPLE_INTENT.md",
        "Docs/V0_Playtest/NEXT_CHAT_L14.md",
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

    st = run(["git", "diff", "--cached", "--stat"])
    lines.append("CACHED_STAT")
    lines.append(st.stdout)
    if st.stderr:
        lines.append(st.stderr)

    # Guard: only expected paths staged
    name_only = run(["git", "diff", "--cached", "--name-only"])
    staged = [x.strip() for x in name_only.stdout.splitlines() if x.strip()]
    lines.append("STAGED " + repr(staged))
    allowed = set(files)
    bad = [x for x in staged if x.replace("\\", "/") not in allowed]
    if bad:
        lines.append("REFUSE_BAD_STAGED " + repr(bad))
        open(OUT, "w", encoding="utf-8").write("\n".join(lines))
        print("\n".join(lines))
        return 9
    if len(staged) < 3:
        lines.append("REFUSE_TOO_FEW_STAGED")
        open(OUT, "w", encoding="utf-8").write("\n".join(lines))
        print("\n".join(lines))
        return 10

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

    pull = run(["git", "pull", "--rebase", "gitlab", "main"])
    lines.append(f"PULL rc={pull.returncode}")
    lines.append(redact(pull.stdout))
    lines.append(redact(pull.stderr))
    if pull.returncode != 0:
        open(OUT, "w", encoding="utf-8").write("\n".join(lines))
        print("\n".join(lines))
        return pull.returncode

    push = run(["git", "push", "gitlab", "main"])
    lines.append(f"PUSH rc={push.returncode}")
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
