#!/usr/bin/env python3
"""Bake HECTON-8 protagonist voice lines into vocal_banks.h8bin.

Runtime contract:
- no JSON sidecar
- little-endian H8VB header
- 32-byte sorted hash index records
- payload bytes are mono PCM16, H8ADPCM, or Ogg/Vorbis
"""

from __future__ import annotations

import argparse
import csv
import math
import os
from pathlib import Path
import shlex
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import wave


MAGIC = 0x42563848  # H8VB
VERSION = 1
ENDIAN = 0xFEFF
BLOCK_SAMPLES = 64
CODEC_PCM16 = 0
CODEC_H8ADPCM = 1
CODEC_VORBIS = 2
FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
PAYLOAD_ALIGNMENT = 16


def fnv1a(text: str) -> int:
    h = FNV_OFFSET
    for b in text.encode("utf-8"):
        h = ((h ^ b) * FNV_PRIME) & 0xFFFFFFFF
    return h


def clamp(v: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, v))


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Bake XTTS/RVC voice CSV into H8 vocal bank.")
    p.add_argument("--csv", default="Docs/Audio/dialogue_script.csv")
    p.add_argument("--out", default="Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin")
    p.add_argument("--codec", choices=("h8adpcm", "pcm16", "vorbis"), default="h8adpcm")
    p.add_argument(
        "--allow-runtime-unsupported-vorbis",
        action="store_true",
        help="Allow archival Vorbis banks. Current Unity Burst runtime rejects Vorbis records closed.",
    )
    p.add_argument("--sample-rate", type=int, default=44100)
    p.add_argument("--xtts-command", default="", help="Command template with {text}, {out}, {speaker}, {language}.")
    p.add_argument("--rvc-command", default="", help="Optional command template with {in_wav}, {out}, {speaker}.")
    p.add_argument("--speaker", default="")
    p.add_argument("--language", default="en")
    p.add_argument("--mock-fallback", action="store_true", default=True)
    p.add_argument("--no-mock-fallback", action="store_false", dest="mock_fallback")
    return p.parse_args()


def read_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise FileNotFoundError(path)
    rows: list[dict[str, str]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f)
        required = {"StringID", "Text", "Priority", "RadioDistortionLevel"}
        missing = required.difference(reader.fieldnames or [])
        if missing:
            raise ValueError(f"CSV missing columns: {sorted(missing)}")
        for raw in reader:
            sid = (raw.get("StringID") or "").strip()
            text = (raw.get("Text") or "").strip()
            if not sid or not text:
                continue
            rows.append(raw)
    if not rows:
        raise ValueError(f"No voice rows in {path}")
    return rows


def expand_command(template: str, **values: str) -> list[str]:
    rendered = template.format(**{k: str(v) for k, v in values.items()})
    return shlex.split(rendered, posix=(os.name != "nt"))


def run_command(template: str, timeout_s: int, **values: str) -> bool:
    if not template:
        return False
    cmd = expand_command(template, **values)
    if not cmd:
        return False
    completed = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=timeout_s, check=False)
    if completed.returncode != 0:
        stderr = completed.stderr.decode("utf-8", errors="replace").strip()
        print(f"[voice_baker] command failed ({completed.returncode}): {stderr}", file=sys.stderr)
        return False
    return True


def synthesize_wav(row: dict[str, str], args: argparse.Namespace, work_dir: Path) -> Path:
    sid = row["StringID"].strip()
    text = row["Text"].strip()
    speaker = (row.get("Speaker") or args.speaker).strip()
    language = (row.get("Language") or args.language).strip()
    xtts_wav = work_dir / f"{sid}.xtts.wav"
    final_wav = work_dir / f"{sid}.wav"

    if run_command(args.xtts_command, 180, text=text, out=str(xtts_wav), speaker=speaker, language=language):
        if args.rvc_command and run_command(args.rvc_command, 180, in_wav=str(xtts_wav), out=str(final_wav), speaker=speaker):
            return final_wav
        if xtts_wav.exists():
            return xtts_wav

    if not args.mock_fallback:
        raise RuntimeError(f"XTTS/RVC failed and mock fallback disabled for {sid}")

    write_mock_voice_wav(final_wav, sid, text, args.sample_rate)
    return final_wav


def write_mock_voice_wav(path: Path, sid: str, text: str, sample_rate: int) -> None:
    seed = fnv1a(sid)
    duration = clamp(0.45 + len(text) * 0.025, 0.75, 4.5)
    total = int(duration * sample_rate)
    base = 115.0 + (seed & 31)
    samples = bytearray()
    for i in range(total):
        t = i / sample_rate
        attack = clamp(t * 8.0, 0.0, 1.0)
        release = clamp((duration - t) * 5.0, 0.0, 1.0)
        env = attack * release
        syllable = 0.72 + 0.28 * math.sin(t * 13.0 + (seed & 7))
        vibrato = 1.0 + 0.025 * math.sin(t * 5.1)
        carrier = math.sin(t * base * vibrato * math.tau)
        formant = 0.45 * math.sin(t * base * 1.91 * math.tau)
        nasal = 0.18 * math.sin(t * base * 3.03 * math.tau)
        value = int(clamp((carrier + formant + nasal) * 0.31 * env * syllable, -1.0, 1.0) * 32767)
        samples += struct.pack("<h", value)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sample_rate)
        w.writeframes(samples)


def read_wav_mono(path: Path, target_rate: int) -> list[float]:
    with wave.open(str(path), "rb") as w:
        channels = w.getnchannels()
        sample_width = w.getsampwidth()
        rate = w.getframerate()
        frames = w.getnframes()
        data = w.readframes(frames)
    if sample_width != 2:
        raise ValueError(f"{path} must be 16-bit PCM WAV")
    source: list[float] = []
    step = channels * 2
    for i in range(0, len(data), step):
        acc = 0.0
        for ch in range(channels):
            v = struct.unpack_from("<h", data, i + ch * 2)[0] / 32768.0
            acc += v
        source.append(acc / max(1, channels))
    if rate == target_rate:
        return source
    return resample_linear(source, rate, target_rate)


def resample_linear(samples: list[float], source_rate: int, target_rate: int) -> list[float]:
    if not samples:
        return [0.0]
    target_len = max(1, int(len(samples) * target_rate / max(1, source_rate)))
    scale = (len(samples) - 1) / max(1, target_len - 1)
    out: list[float] = []
    for i in range(target_len):
        pos = i * scale
        lo = int(pos)
        hi = min(len(samples) - 1, lo + 1)
        frac = pos - lo
        out.append(samples[lo] * (1.0 - frac) + samples[hi] * frac)
    return out


def encode_pcm16(samples: list[float]) -> bytes:
    out = bytearray()
    for s in samples:
        out += struct.pack("<h", int(clamp(s, -1.0, 1.0) * 32767))
    return bytes(out)


def encode_h8adpcm(samples: list[float]) -> bytes:
    out = bytearray()
    total = len(samples)
    for block_start in range(0, total, BLOCK_SAMPLES):
        first = int(clamp(samples[block_start], -1.0, 1.0) * 16000)
        predictor = max(-28000, min(28000, first))
        step = 9
        out += struct.pack("<hBB", predictor, step, 0)
        pack = 0
        side = 0
        current = predictor
        current_step = step
        block_end = min(total, block_start + BLOCK_SAMPLES)
        for i in range(block_start + 1, block_end):
            target = int(clamp(samples[i], -1.0, 1.0) * 16000)
            delta = int(clamp((target - current) / max(1, current_step), -8, 7))
            current = max(-32768, min(32767, current + delta * current_step))
            current_step = max(1, min(127, current_step + abs(delta) - 2))
            encoded = delta & 0x0F
            if side == 0:
                pack = encoded
                side = 1
            else:
                out.append(pack | (encoded << 4))
                side = 0
        if side:
            out.append(pack)
        while (len(out) % 36) != 0:
            out.append(0)
    return bytes(out)


def encode_vorbis(wav_path: Path, work_dir: Path, sid: str) -> bytes:
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("Vorbis codec requested but ffmpeg not found")
    ogg_path = work_dir / f"{sid}.ogg"
    cmd = [ffmpeg, "-y", "-v", "error", "-i", str(wav_path), "-ac", "1", "-c:a", "libvorbis", "-q:a", "3", str(ogg_path)]
    completed = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if completed.returncode != 0:
        raise RuntimeError(completed.stderr.decode("utf-8", errors="replace"))
    return ogg_path.read_bytes()


def align_payload_blob(blob: bytearray) -> None:
    while len(blob) % PAYLOAD_ALIGNMENT != 0:
        blob.append(0)


def build_bank(rows: list[dict[str, str]], args: argparse.Namespace, work_dir: Path) -> bytes:
    payloads: list[tuple[int, bytes, int, int, int, int]] = []
    codec_id = {"pcm16": CODEC_PCM16, "h8adpcm": CODEC_H8ADPCM, "vorbis": CODEC_VORBIS}[args.codec]
    for row in rows:
        sid = row["StringID"].strip()
        wav_path = synthesize_wav(row, args, work_dir)
        samples = read_wav_mono(wav_path, args.sample_rate)
        if args.codec == "pcm16":
            payload = encode_pcm16(samples)
        elif args.codec == "h8adpcm":
            payload = encode_h8adpcm(samples)
        else:
            payload = encode_vorbis(wav_path, work_dir, sid)
        priority = int(clamp(float(row.get("Priority") or 0), 0, 255))
        radio = int(clamp(float(row.get("RadioDistortionLevel") or 0.0), 0.0, 1.0) * 255)
        payloads.append((fnv1a(sid), payload, len(samples), priority, radio, codec_id))

    payloads.sort(key=lambda x: x[0])
    record_count = len(payloads)
    payload_offset = 64 + record_count * 32
    records = bytearray()
    payload_blob = bytearray()
    for hash_id, payload, total_samples, priority, radio, codec in payloads:
        align_payload_blob(payload_blob)
        byte_offset = payload_offset + len(payload_blob)
        records += struct.pack(
            "<IIQIIBBBBI",
            hash_id,
            len(payload),
            byte_offset,
            total_samples,
            args.sample_rate,
            codec,
            1,
            priority,
            radio,
            0,
        )
        payload_blob += payload
    align_payload_blob(payload_blob)

    bank_hash = FNV_OFFSET
    for b in records + payload_blob:
        bank_hash = ((bank_hash ^ b) * FNV_PRIME) & 0xFFFFFFFF
    header = struct.pack(
        "<IIIIIIQQIBBHIIII",
        MAGIC,
        VERSION,
        64,
        32,
        record_count,
        0,
        payload_offset,
        len(payload_blob),
        args.sample_rate,
        payloads[0][5] if payloads else CODEC_H8ADPCM,
        1,
        ENDIAN,
        bank_hash,
        BLOCK_SAMPLES,
        int(time.time()),
        0,
    )
    return header + bytes(records) + bytes(payload_blob)


def main() -> int:
    args = parse_args()
    if args.codec == "vorbis" and not args.allow_runtime_unsupported_vorbis:
        raise RuntimeError("Vorbis packing is archival-only in this pass; pass --allow-runtime-unsupported-vorbis or use --codec h8adpcm.")
    csv_path = Path(args.csv)
    out_path = Path(args.out)
    rows = read_rows(csv_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    temp_root = Path("Temp") / "SHINOBU_260"
    temp_root.mkdir(parents=True, exist_ok=True)
    bank = build_bank(rows, args, temp_root)
    temp_path = out_path.with_name(out_path.name + ".tmp")
    temp_path.write_bytes(bank)
    try:
        os.replace(temp_path, out_path)
    except PermissionError:
        if os.name != "nt":
            raise
        out_path.write_bytes(temp_path.read_bytes())
        try:
            temp_path.unlink()
        except PermissionError:
            print(f"[voice_baker] warning: could not remove temp file {temp_path}", file=sys.stderr)
    print(f"[voice_baker] wrote {out_path} ({len(bank)} bytes, rows={len(rows)}, codec={args.codec})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
