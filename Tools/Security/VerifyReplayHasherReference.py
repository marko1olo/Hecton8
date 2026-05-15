#!/usr/bin/env python3
"""Optional external reference check for ReplayHasher.py.

This tool is deliberately separate from ReplayHasher.py so the save replay
oracle stays dependency-free. Install the third-party `xxhash` package into a
temporary path and pass that path with `--xxhash-path` when a fresh reference
comparison is required.
"""

from __future__ import annotations

import argparse
import importlib
import importlib.util
import pathlib
import sys
from typing import Iterator


MASK64 = 0xFFFFFFFFFFFFFFFF
_MISSING_MODULE = object()

DETERMINISTIC_LENGTHS = (
    0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17,
    31, 32, 33, 63, 64, 65, 95, 96, 97,
    127, 128, 129, 130, 159, 160, 191, 192,
    223, 224, 239, 240, 241, 242, 255, 256,
    511, 512, 1024, 2048, 4097,
)
DETERMINISTIC_SEEDS = (
    0,
    1,
    0x9E3779B185EBCA87,
    0x8000000000000000,
    0xFFFFFFFFFFFFFFFF,
)


def load_replay_hasher(script_path: pathlib.Path):
    spec = importlib.util.spec_from_file_location("ReplayHasher", script_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {script_path}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def payload_bytes(length: int, salt: int) -> bytes:
    return bytes(((index * 31) + (length * 17) + salt) & 0xFF for index in range(length))


def lcg_next(state: int) -> int:
    return (state * 6364136223846793005 + 1442695040888963407) & MASK64


def reference_cases(fuzz_count: int) -> Iterator[tuple[bytes, int]]:
    for length in DETERMINISTIC_LENGTHS:
        for seed in DETERMINISTIC_SEEDS:
            yield payload_bytes(length, seed), seed

    state = 0xC0DEC0FFEE123456
    for _ in range(fuzz_count):
        state = lcg_next(state)
        length = state % 8193
        state = lcg_next(state)
        seed = state
        yield payload_bytes(length, state >> 32), seed


def to_signed64(value: int) -> int:
    value &= MASK64
    if value & (1 << 63):
        return value - (1 << 64)
    return value


def require_u64_digest(label: str, value, length: int, seed: int) -> int:
    if type(value) is not int:
        raise AssertionError(
            f"{label} returned non-int digest len={length} "
            f"seed=0x{seed:016X} type={type(value).__name__}"
        )
    if value < 0 or value > MASK64:
        raise AssertionError(
            f"{label} returned out-of-range digest len={length} "
            f"seed=0x{seed:016X} value={value}"
        )

    return value


def require_u64_lane(label: str, value, context: str, lane: str) -> int:
    if type(value) is not int:
        raise AssertionError(
            f"{label} returned non-int {lane} lane {context} "
            f"type={type(value).__name__}"
        )
    if value < 0 or value > MASK64:
        raise AssertionError(
            f"{label} returned out-of-range {lane} lane {context} value={value}"
        )

    return value


def require_u64_pair(label: str, value, context: str) -> tuple[int, int]:
    if not isinstance(value, tuple) or len(value) != 2:
        raise AssertionError(
            f"{label} returned invalid lane pair {context} "
            f"type={type(value).__name__}"
        )

    return (
        require_u64_lane(label, value[0], context, "lo"),
        require_u64_lane(label, value[1], context, "hi"),
    )


def verify_xxh3(replay, xxhash_module, fuzz_count: int) -> int:
    checked = 0
    for payload, seed in reference_cases(fuzz_count):
        expected = require_u64_digest(
            "xxhash reference",
            xxhash_module.xxh3_64_intdigest(payload, seed=seed),
            len(payload),
            seed,
        )
        actual = require_u64_digest(
            "ReplayHasher.py",
            replay.xxh3_64(payload, seed),
            len(payload),
            seed,
        )
        if actual != expected:
            raise AssertionError(
                f"XXH3 mismatch len={len(payload)} seed=0x{seed:016X} "
                f"expected=0x{expected:016X} actual=0x{actual:016X}"
            )
        checked += 1

    return checked


def verify_shuffle_inverse(replay, fuzz_count: int) -> int:
    state = 0xD15EA5E5A17D00D5
    checked = 0
    for _ in range(fuzz_count):
        state = lcg_next(state)
        plain_lo = state
        state = lcg_next(state)
        plain_hi = state
        state = lcg_next(state)
        world_seed = to_signed64(state)
        state = lcg_next(state)
        sector_hash = to_signed64(state)

        context = f"world_seed={world_seed} sector_hash={sector_hash}"
        stored = require_u64_pair(
            "ReplayHasher.py shuffle_hash128",
            replay.shuffle_hash128(plain_lo, plain_hi, world_seed, sector_hash),
            context,
        )
        recovered = require_u64_pair(
            "ReplayHasher.py unshuffle_hash128",
            replay.unshuffle_hash128(stored[0], stored[1], world_seed, sector_hash),
            context,
        )
        if recovered != (plain_lo, plain_hi):
            raise AssertionError(
                "shuffle inverse mismatch "
                f"world_seed={world_seed} sector_hash={sector_hash}"
            )
        checked += 1

    return checked


def parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Compare ReplayHasher.py with the xxhash package")
    parser.add_argument(
        "--xxhash-path",
        required=True,
        help="temporary package directory containing the xxhash module",
    )
    parser.add_argument("--fuzz-count", type=int, default=128)
    return parser.parse_args(argv)


def verify_module_path(xxhash_module, package_root: pathlib.Path) -> None:
    module_path = getattr(xxhash_module, "__file__", None)
    if not module_path:
        raise RuntimeError("xxhash module has no __file__; cannot verify --xxhash-path containment")

    module_file = pathlib.Path(module_path).resolve()
    try:
        module_file.relative_to(package_root)
    except ValueError as exc:
        raise RuntimeError(
            f"xxhash module resolved outside --xxhash-path: {module_file} not under {package_root}"
        ) from exc


def verify_module_api(xxhash_module) -> None:
    digest = getattr(xxhash_module, "xxh3_64_intdigest", None)
    if not callable(digest):
        raise RuntimeError("xxhash module does not expose callable xxh3_64_intdigest")


def module_is_loaded_from(module, package_root: pathlib.Path) -> bool:
    module_path = getattr(module, "__file__", None)
    if not module_path:
        return False

    try:
        pathlib.Path(module_path).resolve().relative_to(package_root)
    except (OSError, ValueError):
        return False

    return True


def remove_new_modules_loaded_from(package_root: pathlib.Path, baseline_names: set[str]) -> None:
    for name, module in tuple(sys.modules.items()):
        if name in baseline_names:
            continue
        if module_is_loaded_from(module, package_root):
            sys.modules.pop(name, None)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    if args.fuzz_count < 0:
        print("fuzz-count must be non-negative", file=sys.stderr)
        return 2

    xxhash_path = pathlib.Path(args.xxhash_path).resolve()
    if not xxhash_path.is_dir():
        print(f"--xxhash-path does not exist or is not a directory: {xxhash_path}", file=sys.stderr)
        return 2

    path_entry = str(xxhash_path)
    previous_xxhash = sys.modules.get("xxhash", _MISSING_MODULE)
    baseline_module_names = set(sys.modules)
    sys.path.insert(0, path_entry)
    sys.modules.pop("xxhash", None)

    try:
        try:
            xxhash_module = importlib.import_module("xxhash")
        except ImportError:
            print(
                "Missing optional dependency `xxhash`. Install it into a temporary "
                "directory and pass --xxhash-path.",
                file=sys.stderr,
            )
            return 2

        try:
            verify_module_path(xxhash_module, xxhash_path)
        except RuntimeError as exc:
            print(str(exc), file=sys.stderr)
            return 2
        try:
            verify_module_api(xxhash_module)
        except RuntimeError as exc:
            print(str(exc), file=sys.stderr)
            return 2

        root = pathlib.Path(__file__).resolve().parents[2]
        replay = load_replay_hasher(root / "Tools" / "Security" / "ReplayHasher.py")
        try:
            xxh_cases = verify_xxh3(replay, xxhash_module, args.fuzz_count)
            shuffle_cases = verify_shuffle_inverse(replay, args.fuzz_count)
        except AssertionError as exc:
            print(str(exc), file=sys.stderr)
            return 1

        print(f"XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3={xxh_cases} shuffle={shuffle_cases}")
        return 0
    finally:
        try:
            sys.path.remove(path_entry)
        except ValueError:
            pass

        remove_new_modules_loaded_from(xxhash_path, baseline_module_names)

        if previous_xxhash is _MISSING_MODULE:
            sys.modules.pop("xxhash", None)
        else:
            sys.modules["xxhash"] = previous_xxhash


if __name__ == "__main__":
    raise SystemExit(main())
