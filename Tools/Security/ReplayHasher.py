#!/usr/bin/env python3
"""HECTON-8 save replay hash oracle.

Cold-path tool. No runtime dependency. The XXH3-64 implementation is a
scalar Python port of the public xxHash 0.8.x reference path used by
Unity.Mathematics.xxHash3.Hash64. All integer math is explicitly masked to
unsigned 64-bit lanes so the byte output stays stable across Python, Burst C#,
Windows, and ARM.
"""

from __future__ import annotations

import argparse
import pathlib
import struct
import sys
from typing import Tuple


MASK32 = 0xFFFFFFFF
MASK64 = 0xFFFFFFFFFFFFFFFF
MASK128 = (1 << 128) - 1

PRIME32_1 = 0x9E3779B1
PRIME32_2 = 0x85EBCA77
PRIME32_3 = 0xC2B2AE3D
PRIME64_1 = 0x9E3779B185EBCA87
PRIME64_2 = 0xC2B2AE3D27D4EB4F
PRIME64_3 = 0x165667B19E3779F9
PRIME64_4 = 0x85EBCA77C2B2AE63
PRIME64_5 = 0x27D4EB2F165667C5
PRIME_MX1 = 0x165667919E3779F9
PRIME_MX2 = 0x9FB21C651E98DF25

STRIPE_LEN = 64
SECRET_CONSUME_RATE = 8
ACC_NB = 8
SECRET_SIZE_MIN = 136
SECRET_DEFAULT_SIZE = 192
MIDSIZE_MAX = 240
MIDSIZE_STARTOFFSET = 3
MIDSIZE_LASTOFFSET = 17
SECRET_LASTACC_START = 7
SECRET_MERGEACCS_START = 11

SHUFFLE_DOMAIN_LO = b"H8SAVE_SHUFFLE_LO_V1"
SHUFFLE_DOMAIN_HI = b"H8SAVE_SHUFFLE_HI_V1"
MASTER_DOMAIN = b"H8SAVE_MASTER_V1"
MASTER_DOMAIN_LO = b"_LO"
MASTER_DOMAIN_HI = b"_HI"

XXH3_SECRET = bytes(
    [
        0xB8, 0xFE, 0x6C, 0x39, 0x23, 0xA4, 0x4B, 0xBE,
        0x7C, 0x01, 0x81, 0x2C, 0xF7, 0x21, 0xAD, 0x1C,
        0xDE, 0xD4, 0x6D, 0xE9, 0x83, 0x90, 0x97, 0xDB,
        0x72, 0x40, 0xA4, 0xA4, 0xB7, 0xB3, 0x67, 0x1F,
        0xCB, 0x79, 0xE6, 0x4E, 0xCC, 0xC0, 0xE5, 0x78,
        0x82, 0x5A, 0xD0, 0x7D, 0xCC, 0xFF, 0x72, 0x21,
        0xB8, 0x08, 0x46, 0x74, 0xF7, 0x43, 0x24, 0x8E,
        0xE0, 0x35, 0x90, 0xE6, 0x81, 0x3A, 0x26, 0x4C,
        0x3C, 0x28, 0x52, 0xBB, 0x91, 0xC3, 0x00, 0xCB,
        0x88, 0xD0, 0x65, 0x8B, 0x1B, 0x53, 0x2E, 0xA3,
        0x71, 0x64, 0x48, 0x97, 0xA2, 0x0D, 0xF9, 0x4E,
        0x38, 0x19, 0xEF, 0x46, 0xA9, 0xDE, 0xAC, 0xD8,
        0xA8, 0xFA, 0x76, 0x3F, 0xE3, 0x9C, 0x34, 0x3F,
        0xF9, 0xDC, 0xBB, 0xC7, 0xC7, 0x0B, 0x4F, 0x1D,
        0x8A, 0x51, 0xE0, 0x4B, 0xCD, 0xB4, 0x59, 0x31,
        0xC8, 0x9F, 0x7E, 0xC9, 0xD9, 0x78, 0x73, 0x64,
        0xEA, 0xC5, 0xAC, 0x83, 0x34, 0xD3, 0xEB, 0xC3,
        0xC5, 0x81, 0xA0, 0xFF, 0xFA, 0x13, 0x63, 0xEB,
        0x17, 0x0D, 0xDD, 0x51, 0xB7, 0xF0, 0xDA, 0x49,
        0xD3, 0x16, 0x55, 0x26, 0x29, 0xD4, 0x68, 0x9E,
        0x2B, 0x16, 0xBE, 0x58, 0x7D, 0x47, 0xA1, 0xFC,
        0x8F, 0xF8, 0xB8, 0xD1, 0x7A, 0xD0, 0x31, 0xCE,
        0x45, 0xCB, 0x3A, 0x8F, 0x95, 0x16, 0x04, 0x28,
        0xAF, 0xD7, 0xFB, 0xCA, 0xBB, 0x4B, 0x40, 0x7E,
    ]
)


def _u64(value: int) -> int:
    return value & MASK64


def _rotl64(value: int, bits: int) -> int:
    bits &= 63
    value &= MASK64
    return _u64((value << bits) | (value >> ((64 - bits) & 63)))


def _rotl128(value: int, bits: int) -> int:
    bits &= 127
    value &= MASK128
    if bits == 0:
        return value
    return ((value << bits) | (value >> (128 - bits))) & MASK128


def _rotr128(value: int, bits: int) -> int:
    bits &= 127
    value &= MASK128
    if bits == 0:
        return value
    return ((value >> bits) | (value << (128 - bits))) & MASK128


def _read_u32(data: bytes, offset: int) -> int:
    return int.from_bytes(data[offset : offset + 4], "little")


def _read_u64(data: bytes, offset: int) -> int:
    return int.from_bytes(data[offset : offset + 8], "little")


def _pack_u64(value: int) -> bytes:
    return struct.pack("<Q", value & MASK64)


def _pack_u32(value: int) -> bytes:
    return struct.pack("<I", value & MASK32)


def _pack_u16(value: int) -> bytes:
    return struct.pack("<H", value & 0xFFFF)


def _pack_u8(value: int) -> bytes:
    return struct.pack("<B", value & 0xFF)


def _mul128_fold64(lhs: int, rhs: int) -> int:
    product = (lhs & MASK64) * (rhs & MASK64)
    return _u64((product & MASK64) ^ (product >> 64))


def _xxh64_avalanche(hash_value: int) -> int:
    hash_value = _u64(hash_value ^ (hash_value >> 33))
    hash_value = _u64(hash_value * PRIME64_2)
    hash_value = _u64(hash_value ^ (hash_value >> 29))
    hash_value = _u64(hash_value * PRIME64_3)
    return _u64(hash_value ^ (hash_value >> 32))


def _xxh3_avalanche(hash_value: int) -> int:
    hash_value = _u64(hash_value ^ (hash_value >> 37))
    hash_value = _u64(hash_value * PRIME_MX1)
    return _u64(hash_value ^ (hash_value >> 32))


def _rrmxmx(hash_value: int, length: int) -> int:
    hash_value = _u64(hash_value ^ _rotl64(hash_value, 49) ^ _rotl64(hash_value, 24))
    hash_value = _u64(hash_value * PRIME_MX2)
    hash_value = _u64(hash_value ^ ((hash_value >> 35) + length))
    hash_value = _u64(hash_value * PRIME_MX2)
    return _u64(hash_value ^ (hash_value >> 28))


def _mix16(data: bytes, data_offset: int, secret: bytes, secret_offset: int, seed: int) -> int:
    input_lo = _read_u64(data, data_offset)
    input_hi = _read_u64(data, data_offset + 8)
    secret_lo = _u64(_read_u64(secret, secret_offset) + seed)
    secret_hi = _u64(_read_u64(secret, secret_offset + 8) - seed)
    return _mul128_fold64(input_lo ^ secret_lo, input_hi ^ secret_hi)


def _len_0to16(data: bytes, secret: bytes, seed: int) -> int:
    length = len(data)
    if length > 8:
        bitflip1 = _u64((_read_u64(secret, 24) ^ _read_u64(secret, 32)) + seed)
        bitflip2 = _u64((_read_u64(secret, 40) ^ _read_u64(secret, 48)) - seed)
        input_lo = _read_u64(data, 0) ^ bitflip1
        input_hi = _read_u64(data, length - 8) ^ bitflip2
        acc = _u64(length + int.from_bytes(_pack_u64(input_lo), "big") + input_hi)
        acc = _u64(acc + _mul128_fold64(input_lo, input_hi))
        return _xxh3_avalanche(acc)

    if length >= 4:
        seed = _u64(seed ^ (int.from_bytes(struct.pack("<I", seed & MASK32), "big") << 32))
        input1 = _read_u32(data, 0)
        input2 = _read_u32(data, length - 4)
        bitflip = _u64((_read_u64(secret, 8) ^ _read_u64(secret, 16)) - seed)
        input64 = _u64(input2 + (input1 << 32))
        return _rrmxmx(input64 ^ bitflip, length)

    if length:
        c1 = data[0]
        c2 = data[length >> 1]
        c3 = data[length - 1]
        combined = ((c1 << 16) | (c2 << 24) | c3 | (length << 8)) & MASK32
        bitflip = _u64((_read_u32(secret, 0) ^ _read_u32(secret, 4)) + seed)
        return _xxh64_avalanche(combined ^ bitflip)

    return _xxh64_avalanche(seed ^ (_read_u64(secret, 56) ^ _read_u64(secret, 64)))


def _len_17to128(data: bytes, secret: bytes, seed: int) -> int:
    length = len(data)
    acc = _u64(length * PRIME64_1)
    if length > 32:
        if length > 64:
            if length > 96:
                acc = _u64(acc + _mix16(data, 48, secret, 96, seed))
                acc = _u64(acc + _mix16(data, length - 64, secret, 112, seed))
            acc = _u64(acc + _mix16(data, 32, secret, 64, seed))
            acc = _u64(acc + _mix16(data, length - 48, secret, 80, seed))
        acc = _u64(acc + _mix16(data, 16, secret, 32, seed))
        acc = _u64(acc + _mix16(data, length - 32, secret, 48, seed))

    acc = _u64(acc + _mix16(data, 0, secret, 0, seed))
    acc = _u64(acc + _mix16(data, length - 16, secret, 16, seed))
    return _xxh3_avalanche(acc)


def _len_129to240(data: bytes, secret: bytes, seed: int) -> int:
    length = len(data)
    nb_rounds = length // 16
    acc = _u64(length * PRIME64_1)
    for i in range(8):
        acc = _u64(acc + _mix16(data, 16 * i, secret, 16 * i, seed))

    acc_end = _mix16(data, length - 16, secret, SECRET_SIZE_MIN - MIDSIZE_LASTOFFSET, seed)
    acc = _xxh3_avalanche(acc)
    for i in range(8, nb_rounds):
        acc_end = _u64(
            acc_end
            + _mix16(data, 16 * i, secret, 16 * (i - 8) + MIDSIZE_STARTOFFSET, seed)
        )

    return _xxh3_avalanche(_u64(acc + acc_end))


def _accumulate_512(acc: list[int], data: bytes, data_offset: int, secret: bytes, secret_offset: int) -> None:
    for lane in range(ACC_NB):
        data_value = _read_u64(data, data_offset + lane * 8)
        data_key = data_value ^ _read_u64(secret, secret_offset + lane * 8)
        acc[lane ^ 1] = _u64(acc[lane ^ 1] + data_value)
        product = (data_key & MASK32) * ((data_key >> 32) & MASK32)
        acc[lane] = _u64(acc[lane] + product)


def _accumulate(acc: list[int], data: bytes, data_offset: int, secret: bytes, stripe_count: int) -> None:
    for stripe_index in range(stripe_count):
        _accumulate_512(
            acc,
            data,
            data_offset + stripe_index * STRIPE_LEN,
            secret,
            stripe_index * SECRET_CONSUME_RATE,
        )


def _scramble_acc(acc: list[int], secret: bytes, secret_offset: int) -> None:
    for lane in range(ACC_NB):
        lane_value = acc[lane]
        lane_value = _u64(lane_value ^ (lane_value >> 47))
        lane_value ^= _read_u64(secret, secret_offset + lane * 8)
        acc[lane] = _u64(lane_value * PRIME32_1)


def _merge_accs(acc: list[int], secret: bytes, secret_offset: int, start: int) -> int:
    result = start & MASK64
    for i in range(4):
        result = _u64(
            result
            + _mul128_fold64(
                acc[2 * i] ^ _read_u64(secret, secret_offset + 16 * i),
                acc[2 * i + 1] ^ _read_u64(secret, secret_offset + 16 * i + 8),
            )
        )

    return _xxh3_avalanche(result)


def _hash_long(data: bytes, secret: bytes) -> int:
    length = len(data)
    acc = [
        PRIME32_3,
        PRIME64_1,
        PRIME64_2,
        PRIME64_3,
        PRIME64_4,
        PRIME32_2,
        PRIME64_5,
        PRIME32_1,
    ]
    stripe_count_per_block = (len(secret) - STRIPE_LEN) // SECRET_CONSUME_RATE
    block_length = STRIPE_LEN * stripe_count_per_block
    block_count = (length - 1) // block_length

    for block_index in range(block_count):
        _accumulate(acc, data, block_index * block_length, secret, stripe_count_per_block)
        _scramble_acc(acc, secret, len(secret) - STRIPE_LEN)

    stripe_count = ((length - 1) - (block_length * block_count)) // STRIPE_LEN
    _accumulate(acc, data, block_count * block_length, secret, stripe_count)
    _accumulate_512(
        acc,
        data,
        length - STRIPE_LEN,
        secret,
        len(secret) - STRIPE_LEN - SECRET_LASTACC_START,
    )
    return _merge_accs(
        acc,
        secret,
        SECRET_MERGEACCS_START,
        _u64(length * PRIME64_1),
    )


def xxh3_64(data: bytes, seed: int = 0) -> int:
    """Return the XXH3-64 digest as an unsigned little-endian 64-bit value."""

    seed = _u64(seed)
    length = len(data)
    if length <= 16:
        return _len_0to16(data, XXH3_SECRET, seed)
    if length <= 128:
        return _len_17to128(data, XXH3_SECRET, seed)
    if length <= MIDSIZE_MAX:
        return _len_129to240(data, XXH3_SECRET, seed)
    if seed == 0:
        return _hash_long(data, XXH3_SECRET)

    custom_secret = bytearray(SECRET_DEFAULT_SIZE)
    for i in range(SECRET_DEFAULT_SIZE // 16):
        lo = _u64(_read_u64(XXH3_SECRET, 16 * i) + seed)
        hi = _u64(_read_u64(XXH3_SECRET, 16 * i + 8) - seed)
        custom_secret[16 * i : 16 * i + 8] = _pack_u64(lo)
        custom_secret[16 * i + 8 : 16 * i + 16] = _pack_u64(hi)
    return _hash_long(data, bytes(custom_secret))


def low32_xxh3(data: bytes) -> int:
    return xxh3_64(data) & MASK32


def derive_shuffle_mask(world_seed: int, sector_hash: int) -> Tuple[int, int]:
    """Return the 128-bit save XOR mask as (lo64, hi64)."""

    seed_bytes = _pack_u64(world_seed)
    sector_bytes = _pack_u64(sector_hash)
    lo = xxh3_64(SHUFFLE_DOMAIN_LO + seed_bytes + sector_bytes)
    hi = xxh3_64(SHUFFLE_DOMAIN_HI + sector_bytes + seed_bytes + _pack_u64(lo))
    return lo, hi


def _join_u128(lo: int, hi: int) -> int:
    return ((hi & MASK64) << 64) | (lo & MASK64)


def _split_u128(value: int) -> Tuple[int, int]:
    return value & MASK64, (value >> 64) & MASK64


def shuffle_hash128(lo: int, hi: int, world_seed: int, sector_hash: int) -> Tuple[int, int]:
    mask_lo, mask_hi = derive_shuffle_mask(world_seed, sector_hash)
    rotation = (mask_lo ^ (mask_hi >> 1)) & 127
    value = _join_u128(lo, hi)
    mask = _join_u128(mask_lo, mask_hi)
    return _split_u128(_rotl128(value ^ mask, rotation))


def unshuffle_hash128(lo: int, hi: int, world_seed: int, sector_hash: int) -> Tuple[int, int]:
    mask_lo, mask_hi = derive_shuffle_mask(world_seed, sector_hash)
    rotation = (mask_lo ^ (mask_hi >> 1)) & 127
    value = _rotr128(_join_u128(lo, hi), rotation)
    return _split_u128(value ^ _join_u128(mask_lo, mask_hi))


def build_master_preimage(
    magic_value: int,
    version: int,
    compat_mask: int,
    flags: int,
    timestamp_unix_ms: int,
    checksum: int,
    delta_count: int,
    entity_count: int,
    player_offset: int,
    delta_offset: int,
    entity_offset: int,
    hash_payload64: int,
    world_seed: int,
    sector_hash: int,
) -> bytes:
    """Return the canonical V10 MasterStateHash preimage bytes."""

    return b"".join(
        (
            MASTER_DOMAIN,
            _pack_u32(magic_value),
            _pack_u16(version),
            _pack_u8(compat_mask),
            _pack_u8(flags),
            _pack_u64(timestamp_unix_ms),
            _pack_u32(checksum),
            _pack_u32(delta_count),
            _pack_u32(entity_count),
            _pack_u32(player_offset),
            _pack_u32(delta_offset),
            _pack_u32(entity_offset),
            _pack_u64(hash_payload64),
            _pack_u64(world_seed),
            _pack_u64(sector_hash),
        )
    )


def compute_master_state_hash(
    magic_value: int,
    version: int,
    compat_mask: int,
    flags: int,
    timestamp_unix_ms: int,
    checksum: int,
    delta_count: int,
    entity_count: int,
    player_offset: int,
    delta_offset: int,
    entity_offset: int,
    hash_payload64: int,
    world_seed: int,
    sector_hash: int,
) -> Tuple[int, int, int, int]:
    """Return (plain_lo, plain_hi, stored_lo, stored_hi) for the V10 master hash."""

    preimage = build_master_preimage(
        magic_value,
        version,
        compat_mask,
        flags,
        timestamp_unix_ms,
        checksum,
        delta_count,
        entity_count,
        player_offset,
        delta_offset,
        entity_offset,
        hash_payload64,
        world_seed,
        sector_hash,
    )
    plain_lo = xxh3_64(preimage + MASTER_DOMAIN_LO)
    plain_hi = xxh3_64(preimage + MASTER_DOMAIN_HI + _pack_u64(plain_lo))
    stored_lo, stored_hi = shuffle_hash128(plain_lo, plain_hi, world_seed, sector_hash)
    return plain_lo, plain_hi, stored_lo, stored_hi


def lanes_to_le_hex(lo: int, hi: int) -> str:
    return (_pack_u64(lo) + _pack_u64(hi)).hex()


def parse_lanes(value: str) -> Tuple[int, int]:
    text = value.strip().lower().replace("0x", "")
    if ":" in text:
        lo_text, hi_text = text.split(":", 1)
        return int(lo_text, 16) & MASK64, int(hi_text, 16) & MASK64

    raw = bytes.fromhex(text)
    if len(raw) != 16:
        raise ValueError("hash128 must be 16 bytes as hex or lo64:hi64 lanes")
    return int.from_bytes(raw[:8], "little"), int.from_bytes(raw[8:], "little")


def parse_int(value: str) -> int:
    return int(value, 0)


def _command_hash(args: argparse.Namespace) -> int:
    path = pathlib.Path(args.path)
    data = path.read_bytes()
    digest = xxh3_64(data)
    print(f"xxh3_64=0x{digest:016X}")
    print(f"low32=0x{digest & MASK32:08X}")
    print(f"length={len(data)}")
    return 0


def _command_mask(args: argparse.Namespace) -> int:
    lo, hi = derive_shuffle_mask(args.world_seed, args.sector_hash)
    print(f"mask_lo=0x{lo:016X}")
    print(f"mask_hi=0x{hi:016X}")
    print(f"mask_le={lanes_to_le_hex(lo, hi)}")
    return 0


def _command_shuffle(args: argparse.Namespace) -> int:
    lo, hi = parse_lanes(args.hash128)
    if args.reverse:
        out_lo, out_hi = unshuffle_hash128(lo, hi, args.world_seed, args.sector_hash)
    else:
        out_lo, out_hi = shuffle_hash128(lo, hi, args.world_seed, args.sector_hash)

    print(f"out_lo=0x{out_lo:016X}")
    print(f"out_hi=0x{out_hi:016X}")
    print(f"out_le={lanes_to_le_hex(out_lo, out_hi)}")
    return 0


def _command_master(args: argparse.Namespace) -> int:
    plain_lo, plain_hi, stored_lo, stored_hi = compute_master_state_hash(
        args.magic,
        args.version,
        args.compat_mask,
        args.flags,
        args.timestamp_unix_ms,
        args.checksum,
        args.delta_count,
        args.entity_count,
        args.player_offset,
        args.delta_offset,
        args.entity_offset,
        args.hash_payload64,
        args.world_seed,
        args.sector_hash,
    )
    print(f"plain_lo=0x{plain_lo:016X}")
    print(f"plain_hi=0x{plain_hi:016X}")
    print(f"stored_lo=0x{stored_lo:016X}")
    print(f"stored_hi=0x{stored_hi:016X}")
    print(f"stored_le={lanes_to_le_hex(stored_lo, stored_hi)}")
    return 0


def _command_self_test(_: argparse.Namespace) -> int:
    zero_seed_vectors = {
        0: 0x2D06800538D394C2,
        1: 0xE12EF9D2EB86CEEB,
        3: 0xB2B06C45EF888EF4,
        4: 0x8B806C96EC81F796,
        8: 0x65D8B6BD6573B7B0,
        9: 0xB5F2EED243EC7BCB,
        16: 0x49D5450D8A85F113,
        17: 0x8D676AB55FD41AF7,
        64: 0x126D7B47CBB1D0F0,
        128: 0x50ABCBCA6BB1912F,
        129: 0x94874E014AAD8E2D,
        240: 0x7E145804A9F93009,
        241: 0x31C2D8792B29ABB5,
        1024: 0x4986EA1C273817C6,
        4097: 0x0D009A3F10B46B2B,
    }
    for length, expected in zero_seed_vectors.items():
        payload = bytes((i * 31 + length) & 0xFF for i in range(length))
        actual = xxh3_64(payload)
        if actual != expected:
            print(
                f"SELFTEST_FAIL len={length} expected=0x{expected:016X} actual=0x{actual:016X}",
                file=sys.stderr,
            )
            return 1

    seeded_vectors = {
        (0, 0x0000000000000001): 0x4DC5B0CC826F6703,
        (1, 0x0000000000000001): 0x644AE62A32686D33,
        (4, 0x0000000000000001): 0x43AE41E2954D3A8B,
        (9, 0x0000000000000001): 0x12E69274D0687516,
        (17, 0x0000000000000001): 0xD4ED529FDF908A51,
        (129, 0x0000000000000001): 0xA0CBF569259DE49A,
        (240, 0x0000000000000001): 0xE86745C639596C17,
        (241, 0x0000000000000001): 0x7B5FCDFFE3B17B86,
        (4097, 0x0000000000000001): 0x22588E279FA90372,
        (0, 0x9E3779B185EBCA87): 0x07F70F819703314D,
        (1, 0x9E3779B185EBCA87): 0x20A721235B83753D,
        (4, 0x9E3779B185EBCA87): 0xC5676E1316EC5907,
        (9, 0x9E3779B185EBCA87): 0x308E51AFC56716C7,
        (17, 0x9E3779B185EBCA87): 0x252405BFD59ACA5F,
        (129, 0x9E3779B185EBCA87): 0x2638E2C1AA29D175,
        (240, 0x9E3779B185EBCA87): 0x98DF4C7B366C82D5,
        (241, 0x9E3779B185EBCA87): 0x765B94AD431FD9AC,
        (4097, 0x9E3779B185EBCA87): 0x5011A06EFDE64433,
        (0, 0xFFFFFFFFFFFFFFFF): 0x4C093276AE47A555,
        (1, 0xFFFFFFFFFFFFFFFF): 0x0E2534ADF99A4609,
        (4, 0xFFFFFFFFFFFFFFFF): 0x8E880C6B1A88D1F6,
        (9, 0xFFFFFFFFFFFFFFFF): 0x1691E98A41D4FD81,
        (17, 0xFFFFFFFFFFFFFFFF): 0xA5BA236D8EF26CFA,
        (129, 0xFFFFFFFFFFFFFFFF): 0x329B13981F8BECC5,
        (240, 0xFFFFFFFFFFFFFFFF): 0xC3749153347E6C99,
        (241, 0xFFFFFFFFFFFFFFFF): 0x19AA14338A94373E,
        (4097, 0xFFFFFFFFFFFFFFFF): 0xE70C25DEFAD0B7A5,
    }
    for (length, seed), expected in seeded_vectors.items():
        payload = bytes(((i * 31 + length * 17 + seed) & 0xFF) for i in range(length))
        actual = xxh3_64(payload, seed)
        if actual != expected:
            print(
                f"SELFTEST_FAIL seed=0x{seed:016X} len={length} expected=0x{expected:016X} actual=0x{actual:016X}",
                file=sys.stderr,
            )
            return 1

    plain = (0x0123456789ABCDEF, 0x0F1E2D3C4B5A6978)
    mask = derive_shuffle_mask(123456789, -987654321)
    if mask != (0x0E72B33300EEB5F5, 0x1623468F605621EE):
        print("SELFTEST_FAIL shuffle mask vector", file=sys.stderr)
        return 1

    shuffled = shuffle_hash128(plain[0], plain[1], 123456789, -987654321)
    if shuffled != (0x3D47D9522515E068, 0x64F5AECCAC312258):
        print("SELFTEST_FAIL shuffle vector", file=sys.stderr)
        return 1

    recovered = unshuffle_hash128(shuffled[0], shuffled[1], 123456789, -987654321)
    if recovered != plain:
        print("SELFTEST_FAIL shuffle inverse", file=sys.stderr)
        return 1

    master = compute_master_state_hash(
        0x48454354,
        0x000A,
        0x07,
        0x0C,
        0x0000018F3D123456,
        0xDEADBEEF,
        37,
        1024,
        72,
        4096,
        8192,
        0x0123456789ABCDEF,
        123456789,
        -987654321,
    )
    if master != (
        0x82C250ACAADCFCEE,
        0x750FEB3BE2F001A7,
        0x32C38E7EA8C9246D,
        0x8CB2B6D20A988126,
    ):
        print("SELFTEST_FAIL master hash vector", file=sys.stderr)
        return 1

    print("SELFTEST_OK")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="HECTON-8 save XXH3 replay oracle")
    sub = parser.add_subparsers(dest="command", required=True)

    hash_parser = sub.add_parser("hash", help="hash a file with XXH3-64")
    hash_parser.add_argument("path")
    hash_parser.set_defaults(func=_command_hash)

    mask_parser = sub.add_parser("mask", help="derive the 128-bit save shuffle mask")
    mask_parser.add_argument("--world-seed", type=parse_int, required=True)
    mask_parser.add_argument("--sector-hash", type=parse_int, required=True)
    mask_parser.set_defaults(func=_command_mask)

    shuffle_parser = sub.add_parser("shuffle", help="shuffle or unshuffle a 128-bit hash")
    shuffle_parser.add_argument("--world-seed", type=parse_int, required=True)
    shuffle_parser.add_argument("--sector-hash", type=parse_int, required=True)
    shuffle_parser.add_argument("--hash128", required=True, help="16-byte little-endian hex or lo64:hi64")
    shuffle_parser.add_argument("--reverse", action="store_true")
    shuffle_parser.set_defaults(func=_command_shuffle)

    master_parser = sub.add_parser("master", help="compute the V10 shuffled MasterStateHash")
    master_parser.add_argument("--magic", type=parse_int, default=0x48454354)
    master_parser.add_argument("--version", type=parse_int, default=0x000A)
    master_parser.add_argument("--compat-mask", type=parse_int, default=0x07)
    master_parser.add_argument("--flags", type=parse_int, required=True)
    master_parser.add_argument("--timestamp-unix-ms", type=parse_int, required=True)
    master_parser.add_argument("--checksum", type=parse_int, required=True)
    master_parser.add_argument("--delta-count", type=parse_int, required=True)
    master_parser.add_argument("--entity-count", type=parse_int, required=True)
    master_parser.add_argument("--player-offset", type=parse_int, required=True)
    master_parser.add_argument("--delta-offset", type=parse_int, required=True)
    master_parser.add_argument("--entity-offset", type=parse_int, required=True)
    master_parser.add_argument("--hash-payload64", type=parse_int, required=True)
    master_parser.add_argument("--world-seed", type=parse_int, required=True)
    master_parser.add_argument("--sector-hash", type=parse_int, required=True)
    master_parser.set_defaults(func=_command_master)

    test_parser = sub.add_parser("self-test", help="run deterministic local checks")
    test_parser.set_defaults(func=_command_self_test)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
