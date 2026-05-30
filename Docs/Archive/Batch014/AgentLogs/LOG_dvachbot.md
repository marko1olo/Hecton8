# dvachbot / site_tgach log

## 2026-05-29

- What was wrong: site startup scheduled a 12h DB backup daemon that sends DB zip parts to Telegram admins.
- What was done: locating and patching scheduler call in `site_tgach/main.py`; traffic inspection pending.
- Cinematic Cheats used: none; backend scheduler change.
- Exact Microseconds saved: not measured; removed recurring upload path, startup task allocation only is negligible.
- Verification: `python -m py_compile site_tgach/main.py` passed. Old site PID 11300 was restarted by existing `start_site.bat`; new uvicorn PID 17292 is running patched source.
- Traffic audit: first monitor PID 24964 invalidated due PowerShell `$pid` automatic-variable collision in script grouping. Corrected script and restarted clean monitor PID 69560 at 2026-05-29 15:03:06 +04.

## 2026-05-29 - 30 minute traffic audit

- What was wrong: user reported unexplained traffic and the site had scheduled DB backup uploads to Telegram.
- What was done: measured 1804 seconds from 15:03:07 to 15:33:11 +04 with 5s samples. Summary: `Docs/AgentLogs/Traffic_dvachbot_20260529_150307.summary.json`; raw samples: `Docs/AgentLogs/Traffic_dvachbot_20260529_150307.jsonl`.
- Traffic totals: physical `Ethernet` 214.88 MB total, 138.55 MB received, 76.33 MB sent. WireGuard `tgach-pc` 195.78 MB total, 126.92 MB received, 68.86 MB sent. Do not sum them; WireGuard rides over Ethernet.
- Main connection owner: `python.exe` PID 18076, `C:\Users\danat\Desktop\dvachbot\venv\Scripts\python.exe -X utf8 -u main.py`, 724.33 connection-minutes to Telegram `149.154.166.110:443`.
- Second owner: `codex.exe` PID 68656, 311.42 connection-minutes to `172.64.155.209:443` and `104.18.32.47:443`.
- Other persistent lanes: SMB/System to `192.168.1.130`, `ssh.exe` to `62.84.100.97:22`, `stomchat` Python to Telegram `149.154.167.51:443`, VS Code network service to `77.88.55.88:443`.
- Short-lived noise: external `dotnet.exe build` processes touched Microsoft/CDN endpoints; I did not launch those builds.
- Cinematic Cheats used: none.
- Exact Microseconds saved: no CPU microsecond proof. Bandwidth saved: recurring scheduled DB backup upload path removed; old logs show successful backup broadcasts every roughly 12h through 2026-05-29 11:22:23.
