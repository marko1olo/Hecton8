#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import verify_h8bd_manifest


def main() -> int:
    evidence = verify_h8bd_manifest(hash_audit=True)
    header = evidence["header"]
    print(
        "BABEL COMPILE CHECK OK "
        f"sources={evidence['sourceCount']} "
        f"entries={header['entryCount']} "
        f"languages={evidence['languageCount']} "
        f"bytes={evidence['size']} "
        f"payload={header['payloadBytes']} "
        f"word_count={header['wordCount']} "
        "source=existing-static-artifact"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
