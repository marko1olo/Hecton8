#!/usr/bin/env python3
"""Reference check for ReplayHasher.py.

This tool is deliberately separate from ReplayHasher.py so the save replay
oracle stays dependency-free. By default it checks the official XXH3-64 seeded
sanity vectors from Cyan4973/xxHash `cli/xsum_sanity_check.c`. Install the
third-party `xxhash` package into a temporary path and pass that path with
`--xxhash-path` when a fresh package comparison is required.
"""

from __future__ import annotations

import argparse
import importlib
import importlib.util
import pathlib
import sys
from typing import Iterator


MASK64 = 0xFFFFFFFFFFFFFFFF
PRIME32 = 2654435761
PRIME64 = 11400714785074694797
_MISSING_MODULE = object()

OFFICIAL_XXH3_64_VECTORS = (
    (0, 0, 0x2D06800538D394C2),
    (0, PRIME64, 0xA8A6B918B2F0364A),
    (1, 0, 0xC44BDFF4074EECDB),
    (1, PRIME64, 0x032BE332DD766EF8),
    (6, 0, 0x27B56A84CD2D7325),
    (6, PRIME64, 0x84589C116AB59AB9),
    (12, 0, 0xA713DAF0DFBB77E7),
    (12, PRIME64, 0xE7303E1B2336DE0E),
    (24, 0, 0xA3FE70BF9D3510EB),
    (24, PRIME64, 0x850E80FC35BDD690),
    (48, 0, 0x397DA259ECBA1F11),
    (48, PRIME64, 0xADC2CBAA44ACC616),
    (80, 0, 0xBCDEFBBB2C47C90A),
    (80, PRIME64, 0xC6DD0CB699532E73),
    (195, 0, 0xCD94217EE362EC3A),
    (195, PRIME64, 0xBA68003D370CB3D9),
    (403, 0, 0xCDEB804D65C6DEA4),
    (403, PRIME64, 0x6259F6ECFD6443FD),
    (512, 0, 0x617E49599013CB6B),
    (512, PRIME64, 0x3CE457DE14C27708),
    (2048, 0, 0xDD59E2C3A5F038E0),
    (2048, PRIME64, 0x66F81670669ABABC),
    (2099, 0, 0xC6B9D9B3FC9AC765),
    (2099, PRIME64, 0x184F316843663974),
    (2240, 0, 0x6E73A90539CF2948),
    (2240, PRIME64, 0x757BA8487D1B5247),
    (2367, 0, 0xCB37AEB9E5D361ED),
    (2367, PRIME64, 0xD2DB3415B942B42A),
)

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


def official_test_buffer(length: int) -> bytes:
    byte_gen = PRIME32
    data = bytearray(length)
    for index in range(length):
        data[index] = (byte_gen >> 56) & 0xFF
        byte_gen = (byte_gen * PRIME64) & MASK64
    return bytes(data)


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


def call_xxh3_digest(label: str, digest_func, payload: bytes, seed: int):
    try:
        return digest_func(payload, seed=seed)
    except Exception as exc:
        raise AssertionError(
            f"{label} raised {type(exc).__name__} len={len(payload)} "
            f"seed=0x{seed:016X}: {exc}"
        ) from exc


def call_replay_digest(label: str, digest_func, payload: bytes, seed: int):
    try:
        return digest_func(payload, seed)
    except Exception as exc:
        raise AssertionError(
            f"{label} raised {type(exc).__name__} len={len(payload)} "
            f"seed=0x{seed:016X}: {exc}"
        ) from exc


def call_u64_pair(
    label: str,
    pair_func,
    args: tuple[int, ...],
    context: str,
) -> tuple[int, int]:
    try:
        value = pair_func(*args)
    except Exception as exc:
        raise AssertionError(
            f"{label} raised {type(exc).__name__} {context}: {exc}"
        ) from exc

    return require_u64_pair(label, value, context)


def verify_xxh3(replay, xxhash_module, fuzz_count: int) -> int:
    checked = 0
    for payload, seed in reference_cases(fuzz_count):
        expected = require_u64_digest(
            "xxhash reference",
            call_xxh3_digest(
                "xxhash reference",
                xxhash_module.xxh3_64_intdigest,
                payload,
                seed,
            ),
            len(payload),
            seed,
        )
        actual = require_u64_digest(
            "ReplayHasher.py",
            call_replay_digest("ReplayHasher.py", replay.xxh3_64, payload, seed),
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


def verify_official_xxh3_vectors(replay) -> int:
    checked = 0
    for length, seed, expected in OFFICIAL_XXH3_64_VECTORS:
        payload = official_test_buffer(length)
        actual = require_u64_digest(
            "ReplayHasher.py",
            call_replay_digest("ReplayHasher.py", replay.xxh3_64, payload, seed),
            length,
            seed,
        )
        if actual != expected:
            raise AssertionError(
                f"official XXH3 vector mismatch len={length} seed=0x{seed:016X} "
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
        stored = call_u64_pair(
            "ReplayHasher.py shuffle_hash128",
            replay.shuffle_hash128,
            (plain_lo, plain_hi, world_seed, sector_hash),
            context,
        )
        recovered = call_u64_pair(
            "ReplayHasher.py unshuffle_hash128",
            replay.unshuffle_hash128,
            (stored[0], stored[1], world_seed, sector_hash),
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
        default="",
        help="temporary package directory containing the xxhash module",
    )
    parser.add_argument("--fuzz-count", type=int, default=128)
    return parser.parse_args(argv)


def module_candidate_paths(xxhash_module) -> list[pathlib.Path]:
    candidates: list[pathlib.Path] = []
    module_path = getattr(xxhash_module, "__file__", None)
    if module_path:
        candidates.append(pathlib.Path(module_path).resolve())

    spec = getattr(xxhash_module, "__spec__", None)
    spec_origin = getattr(spec, "origin", None)
    if spec_origin and spec_origin not in {"built-in", "frozen", "namespace"}:
        candidates.append(pathlib.Path(spec_origin).resolve())

    search_locations = getattr(spec, "submodule_search_locations", None)
    if search_locations:
        for location in search_locations:
            candidates.append(pathlib.Path(location).resolve())

    extension_module = getattr(xxhash_module, "_xxhash", None)
    extension_path = getattr(extension_module, "__file__", None)
    if extension_path:
        candidates.append(pathlib.Path(extension_path).resolve())

    return candidates


def verify_module_path(xxhash_module, package_root: pathlib.Path) -> None:
    module_paths = module_candidate_paths(xxhash_module)
    if not module_paths:
        raise RuntimeError("xxhash module has no file/spec path; cannot verify --xxhash-path containment")

    for module_file in module_paths:
        try:
            module_file.relative_to(package_root)
            return
        except ValueError:
            continue

    paths = ", ".join(str(path) for path in module_paths)
    raise RuntimeError(f"xxhash module resolved outside --xxhash-path: {paths} not under {package_root}")


def verify_module_api(xxhash_module) -> None:
    digest = getattr(xxhash_module, "xxh3_64_intdigest", None)
    if not callable(digest):
        raise RuntimeError("xxhash module does not expose callable xxh3_64_intdigest")


def module_is_loaded_from(module, package_root: pathlib.Path) -> bool:
    try:
        paths = module_candidate_paths(module)
    except OSError:
        return False

    for path in paths:
        try:
            path.relative_to(package_root)
            return True
        except ValueError:
            continue
    return False


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

    root = pathlib.Path(__file__).resolve().parents[2]
    replay = load_replay_hasher(root / "Tools" / "Security" / "ReplayHasher.py")
    if not args.xxhash_path:
        try:
            vector_cases = verify_official_xxh3_vectors(replay)
            shuffle_cases = verify_shuffle_inverse(replay, args.fuzz_count)
        except AssertionError as exc:
            print(str(exc), file=sys.stderr)
            return 1

        print(
            "XXH3_OFFICIAL_VECTORS_AND_SHUFFLE_FUZZ_OK "
            f"vectors={vector_cases} shuffle={shuffle_cases} "
            "source=Cyan4973/xxHash:cli/xsum_sanity_check.c"
        )
        return 0

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
