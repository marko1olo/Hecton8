#!/usr/bin/env python3
"""
Deterministic lockstep jitter simulator for HECTON-8 NET_SYNC_MERKLE_ARCHITECT.

Offline tool only. It models 200 ms latency, packet loss, redundant input
bundles, prediction rollback, and integer-only MasterStateHash/Merkle roots.
"""

from __future__ import annotations

import argparse
import ast
import heapq
import json
import pathlib
from dataclasses import dataclass
from typing import Dict, Iterable, List, Optional, Tuple


MASK64 = (1 << 64) - 1
FNV64_OFFSET = 0xCBF29CE484222325
FNV64_PRIME = 0x100000001B3
HASH_FUNCTION_NAMES = {
    "mix64",
    "hash_input_state",
    "hash_state_chunk",
    "merkle_pair",
    "merkle_root",
    "master_state_hash",
}


@dataclass(frozen=True)
class InputState:
    tick: int
    player_id: int
    buttons: int
    axis_x: int
    axis_y: int
    aim_yaw_q: int
    tool_state: int


@dataclass(frozen=True)
class SimState:
    p0_x_mm: int
    p0_y_mm: int
    p1_x_mm: int
    p1_y_mm: int
    energy_q10: int
    flags: int


@dataclass(frozen=True)
class Packet:
    sender: int
    receiver: int
    sequence: int
    emit_tick: int
    records: Tuple[InputState, ...]


class XorShift32:
    def __init__(self, seed: int) -> None:
        self._state = seed & 0xFFFFFFFF
        if self._state == 0:
            self._state = 0x6D2B79F5

    def next_u32(self) -> int:
        value = self._state
        value ^= (value << 13) & 0xFFFFFFFF
        value ^= value >> 17
        value ^= (value << 5) & 0xFFFFFFFF
        self._state = value & 0xFFFFFFFF
        return self._state

    def range(self, max_exclusive: int) -> int:
        if max_exclusive <= 0:
            return 0
        return self.next_u32() % max_exclusive

    def signed_range(self, max_abs_inclusive: int) -> int:
        if max_abs_inclusive <= 0:
            return 0
        span = (max_abs_inclusive * 2) + 1
        return self.range(span) - max_abs_inclusive

    def chance_bps(self, basis_points: int) -> bool:
        if basis_points <= 0:
            return False
        if basis_points >= 10000:
            return True
        return self.range(10000) < basis_points


def parse_percent_bps(text: str) -> int:
    raw = text.strip()
    if raw.endswith("%"):
        raw = raw[:-1]
    if not raw:
        raise argparse.ArgumentTypeError("empty percent")

    if "." in raw:
        whole, frac = raw.split(".", 1)
        if not whole:
            whole = "0"
        frac = (frac + "00")[:2]
        if not whole.isdigit() or not frac.isdigit():
            raise argparse.ArgumentTypeError("percent must be numeric")
        return (int(whole) * 100) + int(frac)

    if not raw.isdigit():
        raise argparse.ArgumentTypeError("percent must be numeric")
    return int(raw) * 100


def mix64(hash_value: int, value: int) -> int:
    value &= MASK64
    hash_value ^= value & 0xFF
    hash_value = (hash_value * FNV64_PRIME) & MASK64
    hash_value ^= (value >> 8) & 0xFF
    hash_value = (hash_value * FNV64_PRIME) & MASK64
    hash_value ^= (value >> 16) & 0xFF
    hash_value = (hash_value * FNV64_PRIME) & MASK64
    hash_value ^= (value >> 24) & 0xFF
    hash_value = (hash_value * FNV64_PRIME) & MASK64
    hash_value ^= (value >> 32) & 0xFF
    hash_value = (hash_value * FNV64_PRIME) & MASK64
    hash_value ^= (value >> 40) & 0xFF
    hash_value = (hash_value * FNV64_PRIME) & MASK64
    hash_value ^= (value >> 48) & 0xFF
    hash_value = (hash_value * FNV64_PRIME) & MASK64
    hash_value ^= (value >> 56) & 0xFF
    return (hash_value * FNV64_PRIME) & MASK64


def hash_input_state(input_state: InputState) -> int:
    h = FNV64_OFFSET
    h = mix64(h, input_state.tick)
    h = mix64(h, input_state.player_id)
    h = mix64(h, input_state.buttons)
    h = mix64(h, input_state.axis_x & 0xFF)
    h = mix64(h, input_state.axis_y & 0xFF)
    h = mix64(h, input_state.aim_yaw_q)
    h = mix64(h, input_state.tool_state)
    return h


def hash_state_chunk(chunk_id: int, tick: int, values: Iterable[int]) -> int:
    h = FNV64_OFFSET
    h = mix64(h, 0x48384E455443484B)
    h = mix64(h, chunk_id)
    h = mix64(h, tick)
    for value in values:
        h = mix64(h, value)
    return h


def merkle_pair(left: int, right: int) -> int:
    h = FNV64_OFFSET
    h = mix64(h, 0x4D45524B4C455031)
    h = mix64(h, left)
    h = mix64(h, right)
    return h


def merkle_root(leaves: Tuple[int, ...]) -> int:
    if not leaves:
        return merkle_pair(0, 0)
    level = list(leaves)
    while len(level) > 1:
        next_level: List[int] = []
        for i in range(0, len(level), 2):
            left = level[i]
            right = level[i + 1] if i + 1 < len(level) else left
            next_level.append(merkle_pair(left, right))
        level = next_level
    return level[0]


def master_state_hash(tick: int, state: SimState, inputs: Tuple[InputState, ...]) -> int:
    input_hash = FNV64_OFFSET
    for input_state in inputs:
        input_hash = merkle_pair(input_hash, hash_input_state(input_state))

    player_leaf = hash_state_chunk(
        1,
        tick,
        (
            state.p0_x_mm,
            state.p0_y_mm,
            state.p1_x_mm,
            state.p1_y_mm,
        ),
    )
    gameplay_leaf = hash_state_chunk(2, tick, (state.energy_q10, state.flags))
    input_leaf = hash_state_chunk(3, tick, (input_hash,))
    return merkle_root((player_leaf, gameplay_leaf, input_leaf))


def generate_input(tick: int, player_id: int) -> InputState:
    seed = ((tick + 1) * 0x45D9F3B) ^ ((player_id + 1) * 0x9E3779B9)
    rng = XorShift32(seed)
    axis_x = rng.range(9) - 4
    axis_y = rng.range(9) - 4
    buttons = rng.range(256)
    aim_yaw_q = rng.range(4096)
    tool_state = ((buttons & 0x1F) << 3) | (rng.range(8))
    return InputState(
        tick=tick,
        player_id=player_id,
        buttons=buttons,
        axis_x=axis_x,
        axis_y=axis_y,
        aim_yaw_q=aim_yaw_q,
        tool_state=tool_state,
    )


def apply_inputs(state: SimState, tick: int, inputs: Tuple[InputState, ...]) -> SimState:
    p0_x = state.p0_x_mm
    p0_y = state.p0_y_mm
    p1_x = state.p1_x_mm
    p1_y = state.p1_y_mm
    energy = state.energy_q10
    flags = state.flags

    for input_state in inputs:
        thrust = 12 + (input_state.tool_state & 7)
        dx = input_state.axis_x * thrust
        dy = input_state.axis_y * thrust
        if input_state.player_id == 0:
            p0_x += dx
            p0_y += dy
        else:
            p1_x += dx
            p1_y += dy
        energy -= (abs(input_state.axis_x) + abs(input_state.axis_y) + (input_state.buttons & 3))
        flags ^= ((input_state.buttons << (input_state.player_id * 8)) ^ input_state.aim_yaw_q) & 0xFFFF

    if energy < 0:
        energy = 0
    if energy > 1024:
        energy = 1024

    flags ^= (tick * 0x1F123BB5) & 0xFFFFFFFF
    return SimState(p0_x, p0_y, p1_x, p1_y, energy, flags & 0xFFFFFFFF)


class Peer:
    def __init__(self, peer_id: int, client_count: int, rollback_window: int) -> None:
        self.peer_id = peer_id
        self.client_count = client_count
        self.rollback_window = rollback_window
        self.actual_inputs: Dict[Tuple[int, int], InputState] = {}
        self.predicted_inputs: Dict[Tuple[int, int], InputState] = {}
        self.used_inputs: Dict[int, Tuple[InputState, ...]] = {}
        self.hash_sequence: Dict[int, int] = {}
        self.state_snapshots: Dict[int, SimState] = {-1: SimState(0, 0, 0, 0, 1024, 0)}
        self.latest_tick = -1
        self.rollback_events = 0
        self.max_rollback_depth = 0
        self.too_old_corrections = 0
        self.predicted_slots = 0
        self.corrected_slots = 0

    def store_actual(self, input_state: InputState) -> None:
        key = (input_state.tick, input_state.player_id)
        old_actual = self.actual_inputs.get(key)
        if old_actual == input_state:
            return

        was_predicted = key in self.predicted_inputs
        old_prediction = self.predicted_inputs.get(key)
        self.actual_inputs[key] = input_state

        if was_predicted and old_prediction != input_state and input_state.tick <= self.latest_tick:
            depth = self.latest_tick - input_state.tick
            if depth > self.rollback_window:
                self.too_old_corrections += 1
                return
            old_latest = self.latest_tick
            self._rewind_to(input_state.tick - 1)
            self.rollback_events += 1
            if depth > self.max_rollback_depth:
                self.max_rollback_depth = depth
            self.corrected_slots += 1
            self.simulate_until(old_latest)

    def simulate_until(self, target_tick: int) -> None:
        while self.latest_tick < target_tick:
            next_tick = self.latest_tick + 1
            inputs = self._inputs_for_tick(next_tick)
            next_state = apply_inputs(self.state_snapshots[self.latest_tick], next_tick, inputs)
            self.latest_tick = next_tick
            self.used_inputs[next_tick] = inputs
            self.state_snapshots[next_tick] = next_state
            self.hash_sequence[next_tick] = master_state_hash(next_tick, next_state, inputs)

    def _inputs_for_tick(self, tick: int) -> Tuple[InputState, ...]:
        result: List[InputState] = []
        for player_id in range(self.client_count):
            key = (tick, player_id)
            actual = self.actual_inputs.get(key)
            if actual is not None:
                result.append(actual)
                continue

            predicted = self._predict_input(tick, player_id)
            self.predicted_inputs[key] = predicted
            self.predicted_slots += 1
            result.append(predicted)
        return tuple(result)

    def _predict_input(self, tick: int, player_id: int) -> InputState:
        previous_tick = tick - 1
        while previous_tick >= 0:
            actual = self.actual_inputs.get((previous_tick, player_id))
            if actual is not None:
                return InputState(
                    tick=tick,
                    player_id=player_id,
                    buttons=actual.buttons,
                    axis_x=actual.axis_x,
                    axis_y=actual.axis_y,
                    aim_yaw_q=actual.aim_yaw_q,
                    tool_state=actual.tool_state,
                )
            previous_tick -= 1
        return InputState(tick, player_id, 0, 0, 0, 0, 0)

    def _rewind_to(self, tick: int) -> None:
        remove_from = tick + 1
        keys = [key for key in self.used_inputs.keys() if key >= remove_from]
        for key in keys:
            del self.used_inputs[key]
        keys = [key for key in self.hash_sequence.keys() if key >= remove_from]
        for key in keys:
            del self.hash_sequence[key]
        keys = [key for key in self.state_snapshots.keys() if key >= remove_from]
        for key in keys:
            del self.state_snapshots[key]
        self.latest_tick = tick


def build_reference(ticks: int, client_count: int) -> Tuple[Dict[int, Tuple[InputState, ...]], Dict[int, int]]:
    state = SimState(0, 0, 0, 0, 1024, 0)
    inputs_by_tick: Dict[int, Tuple[InputState, ...]] = {}
    hashes_by_tick: Dict[int, int] = {}
    for tick in range(ticks):
        inputs = tuple(generate_input(tick, player_id) for player_id in range(client_count))
        state = apply_inputs(state, tick, inputs)
        inputs_by_tick[tick] = inputs
        hashes_by_tick[tick] = master_state_hash(tick, state, inputs)
    return inputs_by_tick, hashes_by_tick


def packet_records_for_tick(sender: int, wall_tick: int, max_tick: int, redundancy: int) -> Tuple[InputState, ...]:
    if wall_tick < 0:
        return ()
    newest_tick = wall_tick
    if newest_tick >= max_tick:
        newest_tick = max_tick - 1
    if newest_tick < 0:
        return ()
    oldest_tick = newest_tick - redundancy + 1
    if oldest_tick < 0:
        oldest_tick = 0
    return tuple(generate_input(tick, sender) for tick in range(oldest_tick, newest_tick + 1))


def schedule_packet(
    queue: List[Tuple[int, int, Packet]],
    rng: XorShift32,
    order: int,
    packet: Packet,
    emit_ms: int,
    latency_ms: int,
    jitter_ms: int,
    loss_bps: int,
) -> Tuple[int, int, int]:
    sent = 1
    if rng.chance_bps(loss_bps):
        return sent, 1, order
    delay = latency_ms + rng.signed_range(jitter_ms)
    if delay < 0:
        delay = 0
    heapq.heappush(queue, (emit_ms + delay, order, packet))
    return sent, 0, order + 1


def simulate(args: argparse.Namespace) -> Dict[str, object]:
    client_count = args.clients
    ticks = args.ticks
    flush_ticks = args.redundancy + args.input_delay_ticks + ((args.latency_ms + args.jitter_ms) // args.tick_ms) + 4
    end_wall_tick = ticks + flush_ticks
    loss_rng = XorShift32(args.seed ^ 0xA53C9E11)
    network_queue: List[Tuple[int, int, Packet]] = []
    peers = [Peer(peer_id, client_count, args.rollback_ticks) for peer_id in range(client_count)]
    reference_inputs, reference_hashes = build_reference(ticks, client_count)

    sequence_by_sender = [0 for _ in range(client_count)]
    packet_order = 0
    sent_packets = 0
    lost_packets = 0
    delivered_packets = 0
    payload_bytes = 0

    for wall_tick in range(end_wall_tick):
        wall_ms = wall_tick * args.tick_ms

        for player_id in range(client_count):
            if wall_tick < ticks:
                peers[player_id].store_actual(generate_input(wall_tick, player_id))

        for sender in range(client_count):
            records = packet_records_for_tick(sender, wall_tick, ticks, args.redundancy)
            if not records:
                continue
            for receiver in range(client_count):
                if receiver == sender:
                    continue
                packet = Packet(sender, receiver, sequence_by_sender[sender], wall_tick, records)
                sequence_by_sender[sender] += 1
                packet_bytes = 16 + (len(records) * 16)
                payload_bytes += packet_bytes
                sent, lost, packet_order = schedule_packet(
                    network_queue,
                    loss_rng,
                    packet_order,
                    packet,
                    wall_ms,
                    args.latency_ms,
                    args.jitter_ms,
                    args.loss_bps,
                )
                sent_packets += sent
                lost_packets += lost

        while network_queue and network_queue[0][0] <= wall_ms:
            _arrival_ms, _order, packet = heapq.heappop(network_queue)
            delivered_packets += 1
            peer = peers[packet.receiver]
            for input_state in packet.records:
                peer.store_actual(input_state)

        target_tick = wall_tick - args.input_delay_ticks
        if target_tick >= ticks:
            target_tick = ticks - 1
        if target_tick >= 0:
            for peer in peers:
                peer.simulate_until(target_tick)

    while network_queue:
        _arrival_ms, _order, packet = heapq.heappop(network_queue)
        delivered_packets += 1
        peer = peers[packet.receiver]
        for input_state in packet.records:
            peer.store_actual(input_state)

    for peer in peers:
        peer.simulate_until(ticks - 1)

    hash_mismatches: List[Tuple[int, int, int, int]] = []
    ring_mismatches: List[Tuple[int, int]] = []
    missing_actual = 0
    for peer in peers:
        for tick in range(ticks):
            peer_hash = peer.hash_sequence.get(tick)
            reference_hash = reference_hashes[tick]
            if peer_hash != reference_hash:
                hash_mismatches.append((peer.peer_id, tick, peer_hash or 0, reference_hash))
            if peer.used_inputs.get(tick) != reference_inputs[tick]:
                ring_mismatches.append((peer.peer_id, tick))
            for player_id in range(client_count):
                if (tick, player_id) not in peer.actual_inputs:
                    missing_actual += 1

    float_hash_audit = audit_hash_functions(pathlib.Path(__file__))
    rollback_events = sum(peer.rollback_events for peer in peers)
    max_rollback_depth = max(peer.max_rollback_depth for peer in peers)
    too_old_corrections = sum(peer.too_old_corrections for peer in peers)
    predicted_slots = sum(peer.predicted_slots for peer in peers)
    corrected_slots = sum(peer.corrected_slots for peer in peers)

    return {
        "status": "NETWORK PROTOCOL READY" if not hash_mismatches and not ring_mismatches and not missing_actual and float_hash_audit["status"] == "PASS" else "PENDING VERIFICATION",
        "config": {
            "tick_ms": args.tick_ms,
            "ticks": ticks,
            "clients": client_count,
            "latency_ms": args.latency_ms,
            "jitter_ms": args.jitter_ms,
            "loss_bps": args.loss_bps,
            "loss_percent_text": format_bps(args.loss_bps),
            "input_delay_ticks": args.input_delay_ticks,
            "rollback_ticks": args.rollback_ticks,
            "redundancy": args.redundancy,
            "seed": args.seed,
        },
        "network": {
            "sent_packets": sent_packets,
            "lost_packets": lost_packets,
            "delivered_packets": delivered_packets,
            "payload_bytes": payload_bytes,
            "estimated_payload_bytes_per_second": (payload_bytes * 1000) // max(1, end_wall_tick * args.tick_ms),
        },
        "rollback": {
            "events": rollback_events,
            "max_depth_ticks": max_rollback_depth,
            "too_old_corrections": too_old_corrections,
            "predicted_slots": predicted_slots,
            "corrected_slots": corrected_slots,
        },
        "verification": {
            "master_state_hash_mismatches": len(hash_mismatches),
            "input_ring_mismatches": len(ring_mismatches),
            "missing_actual_inputs": missing_actual,
            "float_hash_audit": float_hash_audit,
            "last_master_hash_hex": f"0x{reference_hashes[ticks - 1]:016X}" if ticks > 0 else "0x0000000000000000",
        },
        "samples": {
            "hash_mismatch_first": hash_mismatches[:3],
            "ring_mismatch_first": ring_mismatches[:3],
        },
    }


def format_bps(bps: int) -> str:
    whole = bps // 100
    frac = bps % 100
    if frac == 0:
        return f"{whole}%"
    return f"{whole}.{frac:02d}%"


def audit_hash_functions(path: pathlib.Path) -> Dict[str, object]:
    source = path.read_text(encoding="utf-8")
    tree = ast.parse(source, filename=str(path))
    violations: List[str] = []

    for node in tree.body:
        if not isinstance(node, ast.FunctionDef):
            continue
        if node.name not in HASH_FUNCTION_NAMES:
            continue
        for child in ast.walk(node):
            if isinstance(child, ast.Constant) and isinstance(child.value, float):
                violations.append(f"{node.name}: float constant at line {child.lineno}")
            elif isinstance(child, ast.Call) and isinstance(child.func, ast.Name) and child.func.id == "float":
                violations.append(f"{node.name}: float() call at line {child.lineno}")
            elif isinstance(child, ast.BinOp) and isinstance(child.op, (ast.Div, ast.FloorDiv)):
                violations.append(f"{node.name}: division operator at line {child.lineno}")

    return {
        "status": "PASS" if not violations else "CRIME",
        "functions": sorted(HASH_FUNCTION_NAMES),
        "violations": violations,
    }


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="HECTON-8 deterministic network jitter simulator.")
    parser.add_argument("--latency-ms", type=int, default=200)
    parser.add_argument("--jitter-ms", type=int, default=40)
    parser.add_argument("--loss-percent", dest="loss_bps", type=parse_percent_bps, default=parse_percent_bps("5"))
    parser.add_argument("--tick-ms", type=int, default=20)
    parser.add_argument("--ticks", type=int, default=600)
    parser.add_argument("--clients", type=int, default=2)
    parser.add_argument("--input-delay-ticks", type=int, default=16)
    parser.add_argument("--rollback-ticks", type=int, default=64)
    parser.add_argument("--redundancy", type=int, default=16)
    parser.add_argument("--seed", type=int, default=0x4E455431)
    parser.add_argument("--report", type=pathlib.Path)
    return parser


def validate_args(args: argparse.Namespace) -> None:
    if args.latency_ms < 0:
        raise SystemExit("latency-ms must be >= 0")
    if args.jitter_ms < 0:
        raise SystemExit("jitter-ms must be >= 0")
    if args.tick_ms <= 0:
        raise SystemExit("tick-ms must be > 0")
    if args.ticks <= 0:
        raise SystemExit("ticks must be > 0")
    if args.clients < 2:
        raise SystemExit("clients must be >= 2")
    if args.input_delay_ticks < 0:
        raise SystemExit("input-delay-ticks must be >= 0")
    if args.rollback_ticks < 1:
        raise SystemExit("rollback-ticks must be >= 1")
    if args.redundancy < 1:
        raise SystemExit("redundancy must be >= 1")


def main(argv: Optional[List[str]] = None) -> int:
    parser = build_arg_parser()
    args = parser.parse_args(argv)
    validate_args(args)
    report = simulate(args)

    rendered = json.dumps(report, indent=2, sort_keys=True)
    print(rendered)

    if args.report is not None:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(rendered + "\n", encoding="utf-8")

    return 0 if report["status"] == "NETWORK PROTOCOL READY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
