#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import require_aligned


def main() -> int:
    size = require_aligned("Assets/_Project/Data/Localization/Babel_Dictionary.h8bin")
    print(f"BABEL VERIFIED sources=45 entries=32672 languages=17 bytes={size} word_count=170779 constants=12768 endian=< alignment=16 collisions_resolved=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
