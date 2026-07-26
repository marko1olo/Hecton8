# .agent-locks — advisory write claims

Protocol: `Docs/ARCHITECTURE/MULTI_AGENT_FILE_OWNERSHIP_PROTOCOL.md`

## Layout

- `ACTIVE/<path_with_underscores>.lock` — a live claim on a file.
- `ACTIVITY.md` — append-only log of completed work.

## Before writing outside your zone

1. List `ACTIVE/`. A live claim by another agent on your target means **do not write**.
2. No claim? Write one, do the work, delete it.
3. A claim past `expires:` is dead. Anyone may delete it and proceed.

## Claim format

```text
agent:   <agent id>
started: <ISO8601 UTC>
expires: <ISO8601 UTC, keep it about an hour>
intent:  <one line>
files:   <repo-relative path>
```

Claims are advisory. They work because agents honour them. The rule that actually protects
you when someone ignores them is re-verifying file freshness immediately before every write.
