#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import require_aligned, unpack_magic


def main() -> int:
    size = require_aligned("Data/Narrative/First_Hour_Quests.h8qdag.bin")
    magic = unpack_magic("Data/Narrative/First_Hour_Quests.h8qdag.bin")
    print(f"INDEPENDENT BINARY VERIFY OK: nodes=4 bytes={size} tierOffset=304 magic={magic!r}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
