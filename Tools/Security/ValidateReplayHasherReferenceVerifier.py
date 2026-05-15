#!/usr/bin/env python3
"""Self-check the negative guards in VerifyReplayHasherReference.py.

Cold-path tooling only. This keeps verifier failure-mode evidence executable
without requiring the optional third-party xxhash package.
"""

from __future__ import annotations

import importlib.util
import pathlib
import sys
import tempfile
import types


def load_module(name: str, path: pathlib.Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {path}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def expect_assert(label: str, expected: str, func, errors: list[str]) -> None:
    try:
        func()
    except AssertionError as exc:
        text = str(exc)
        if expected not in text:
            errors.append(f"{label} wrong failure text: {text!r}")
            return
        return

    errors.append(f"{label} did not fail")


def expect_runtime(label: str, expected: str, func, errors: list[str]) -> None:
    try:
        func()
    except RuntimeError as exc:
        text = str(exc)
        if expected not in text:
            errors.append(f"{label} wrong failure text: {text!r}")
            return
        return

    errors.append(f"{label} did not fail")


def run_guard_checks(verifier, replay) -> list[str]:
    errors: list[str] = []
    original_xxh3 = replay.xxh3_64
    original_shuffle = replay.shuffle_hash128
    original_unshuffle = replay.unshuffle_hash128

    class MissingApiReference:
        __file__ = __file__

    class StringReference:
        @staticmethod
        def xxh3_64_intdigest(_payload, seed=0):
            return "bad"

    class RangeReference:
        @staticmethod
        def xxh3_64_intdigest(_payload, seed=0):
            return verifier.MASK64 + 1

    class BoolReference:
        @staticmethod
        def xxh3_64_intdigest(_payload, seed=0):
            return True

    class RaisingReference:
        @staticmethod
        def xxh3_64_intdigest(_payload, seed=0):
            raise ValueError("boom")

    class GoodReference:
        @staticmethod
        def xxh3_64_intdigest(payload, seed=0):
            return original_xxh3(payload, seed)

    class MismatchReference:
        @staticmethod
        def xxh3_64_intdigest(payload, seed=0):
            return original_xxh3(payload, seed) ^ 1

    expect_runtime(
        "XXHASH_API_SHAPE_GUARD",
        "does not expose callable xxh3_64_intdigest",
        lambda: verifier.verify_module_api(MissingApiReference),
        errors,
    )
    expect_runtime(
        "XXHASH_MODULE_FILE_GUARD",
        "has no __file__",
        lambda: verifier.verify_module_path(types.SimpleNamespace(), pathlib.Path.cwd()),
        errors,
    )
    expect_assert(
        "XXHASH_DIGEST_TYPE_GUARD",
        "xxhash reference returned non-int digest",
        lambda: verifier.verify_xxh3(replay, StringReference, 0),
        errors,
    )
    expect_assert(
        "XXHASH_DIGEST_RANGE_GUARD",
        "xxhash reference returned out-of-range digest",
        lambda: verifier.verify_xxh3(replay, RangeReference, 0),
        errors,
    )
    expect_assert(
        "XXHASH_DIGEST_BOOL_GUARD",
        "xxhash reference returned non-int digest",
        lambda: verifier.verify_xxh3(replay, BoolReference, 0),
        errors,
    )
    expect_assert(
        "XXHASH_DIGEST_EXCEPTION_GUARD",
        "xxhash reference raised ValueError",
        lambda: verifier.verify_xxh3(replay, RaisingReference, 0),
        errors,
    )
    expect_assert(
        "XXHASH_MISMATCH_CONTROLLED_FAILURE_GUARD",
        "XXH3 mismatch",
        lambda: verifier.verify_xxh3(replay, MismatchReference, 0),
        errors,
    )

    try:
        replay.xxh3_64 = lambda *_args: "bad"
        expect_assert(
            "REPLAY_DIGEST_TYPE_GUARD",
            "ReplayHasher.py returned non-int digest",
            lambda: verifier.verify_xxh3(replay, GoodReference, 0),
            errors,
        )

        replay.xxh3_64 = lambda *_args: verifier.MASK64 + 1
        expect_assert(
            "REPLAY_DIGEST_RANGE_GUARD",
            "ReplayHasher.py returned out-of-range digest",
            lambda: verifier.verify_xxh3(replay, GoodReference, 0),
            errors,
        )

        replay.xxh3_64 = lambda *_args: False
        expect_assert(
            "REPLAY_DIGEST_BOOL_GUARD",
            "ReplayHasher.py returned non-int digest",
            lambda: verifier.verify_xxh3(replay, GoodReference, 0),
            errors,
        )

        replay.xxh3_64 = lambda *_args: (_ for _ in ()).throw(RuntimeError("bad replay"))
        expect_assert(
            "REPLAY_DIGEST_EXCEPTION_GUARD",
            "ReplayHasher.py raised RuntimeError",
            lambda: verifier.verify_xxh3(replay, GoodReference, 0),
            errors,
        )

        replay.xxh3_64 = original_xxh3
        replay.shuffle_hash128 = lambda *_args: (1,)
        expect_assert(
            "SHUFFLE_PAIR_TYPE_GUARD",
            "shuffle_hash128 returned invalid lane pair",
            lambda: verifier.verify_shuffle_inverse(replay, 1),
            errors,
        )

        replay.shuffle_hash128 = lambda *_args: (0, verifier.MASK64 + 1)
        expect_assert(
            "SHUFFLE_PAIR_RANGE_GUARD",
            "shuffle_hash128 returned out-of-range hi lane",
            lambda: verifier.verify_shuffle_inverse(replay, 1),
            errors,
        )

        replay.shuffle_hash128 = lambda *_args: (True, 0)
        expect_assert(
            "SHUFFLE_PAIR_BOOL_GUARD",
            "shuffle_hash128 returned non-int lo lane",
            lambda: verifier.verify_shuffle_inverse(replay, 1),
            errors,
        )

        replay.shuffle_hash128 = lambda *_args: (_ for _ in ()).throw(ValueError("bad shuffle"))
        expect_assert(
            "SHUFFLE_PAIR_EXCEPTION_GUARD",
            "shuffle_hash128 raised ValueError",
            lambda: verifier.verify_shuffle_inverse(replay, 1),
            errors,
        )

        replay.shuffle_hash128 = original_shuffle
        replay.unshuffle_hash128 = lambda *_args: [0, 0]
        expect_assert(
            "UNSHUFFLE_PAIR_TYPE_GUARD",
            "unshuffle_hash128 returned invalid lane pair",
            lambda: verifier.verify_shuffle_inverse(replay, 1),
            errors,
        )

        replay.unshuffle_hash128 = lambda *_args: (0, verifier.MASK64 + 1)
        expect_assert(
            "UNSHUFFLE_PAIR_RANGE_GUARD",
            "unshuffle_hash128 returned out-of-range hi lane",
            lambda: verifier.verify_shuffle_inverse(replay, 1),
            errors,
        )

        replay.unshuffle_hash128 = lambda *_args: (0, False)
        expect_assert(
            "UNSHUFFLE_PAIR_BOOL_GUARD",
            "unshuffle_hash128 returned non-int hi lane",
            lambda: verifier.verify_shuffle_inverse(replay, 1),
            errors,
        )

        replay.unshuffle_hash128 = lambda *_args: (_ for _ in ()).throw(RuntimeError("bad unshuffle"))
        expect_assert(
            "UNSHUFFLE_PAIR_EXCEPTION_GUARD",
            "unshuffle_hash128 raised RuntimeError",
            lambda: verifier.verify_shuffle_inverse(replay, 1),
            errors,
        )
    finally:
        replay.xxh3_64 = original_xxh3
        replay.shuffle_hash128 = original_shuffle
        replay.unshuffle_hash128 = original_unshuffle

    with tempfile.TemporaryDirectory() as temp_dir:
        package_root = pathlib.Path(temp_dir)
        helper_name = "_h8_temp_xxhash_helper_probe"
        helper_module = types.ModuleType(helper_name)
        helper_module.__file__ = str(package_root / "helper.py")
        sys.modules[helper_name] = helper_module
        verifier.remove_new_modules_loaded_from(package_root, set(sys.modules) - {helper_name})
        if helper_name in sys.modules:
            errors.append("XXHASH_TEMP_HELPER_MODULE_CLEANUP_GUARD did not remove temp helper")

    return errors


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[2]
    verifier = load_module(
        "VerifyReplayHasherReference",
        root / "Tools" / "Security" / "VerifyReplayHasherReference.py",
    )
    replay = load_module("ReplayHasher", root / "Tools" / "Security" / "ReplayHasher.py")

    errors = run_guard_checks(verifier, replay)
    if errors:
        print("REPLAY_REFERENCE_VERIFIER_GUARD=FAIL", file=sys.stderr)
        for error in errors:
            print(error, file=sys.stderr)
        return 1

    print("REPLAY_REFERENCE_VERIFIER_GUARD=PASS checks=20")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
