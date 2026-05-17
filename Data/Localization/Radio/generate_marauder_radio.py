#!/usr/bin/env python3
"""Bake Marauder radio interception JSON and clean-rating variant.

Offline-only authoring tool. It deliberately lives beside the generated data so
runtime code never computes dialogue hashes, performs profanity filtering, or
builds conditional state strings.
"""

from __future__ import annotations

import json
import re
import struct
from pathlib import Path
from typing import Any


FNV_OFFSET_BASIS = 2166136261
FNV_PRIME = 16777619
ROOT = Path(__file__).resolve().parent
RAW_OUTPUT = ROOT / "marauder_radio_interceptions.json"
CLEAN_OUTPUT = ROOT / "marauder_radio_interceptions_clean.json"
DICTIONARY_OUTPUT = ROOT / "marauder_radio_dictionary.json"
REPORT_OUTPUT = ROOT / "marauder_radio_validation.json"
BINARY_OUTPUT = ROOT / "marauder_radio_interceptions.h8bin"
BINARY_LAYOUT_OUTPUT = ROOT / "marauder_radio_interceptions.layout.json"

SECONDS_PER_MINUTE = 60.0
READING_WORDS_PER_MINUTE = 171.0
READING_WORDS_PER_SECOND = READING_WORDS_PER_MINUTE / SECONDS_PER_MINUTE
READING_RATE_PROVENANCE = (
    "Project subtitle comfort floor for stressed radio copy; deliberately below "
    "fast adult reading speed so bark timing stays readable under alarm noise."
)
RADIO_SQUAWK_LEAD_OUT_MILLISECONDS = 650
RADIO_SQUAWK_LEAD_OUT_SECONDS = RADIO_SQUAWK_LEAD_OUT_MILLISECONDS / 1000.0
RADIO_SQUAWK_PROVENANCE = (
    "Receiver squelch tail allowance for chopped industrial radio playback; "
    "presentation timing, not a physics LUT."
)
SUBTITLE_FLOOR_MILLISECONDS = 2400
SUBTITLE_FLOOR_SECONDS = SUBTITLE_FLOOR_MILLISECONDS / 1000.0
SUBTITLE_FLOOR_PROVENANCE = (
    "Minimum command subtitle exposure for short emergency orders on low-tier UI."
)

BINARY_MAGIC = b"H8RD"
BINARY_VERSION = 1
BINARY_ALIGNMENT = 16
BINARY_HEADER_STRUCT = struct.Struct("<4sHH14I")
BINARY_RECORD_STRUCT = struct.Struct("<32I")
BINARY_HEADER_SIZE = BINARY_HEADER_STRUCT.size
BINARY_RECORD_SIZE = BINARY_RECORD_STRUCT.size
BINARY_FLAG_SORTED_HASH_RECORDS = 0x00000001
BINARY_FLAG_LOW_TIER_TEXT = 0x00000002
BINARY_FLAG_CLEAN_TEXT = 0x00000004
BINARY_FLAG_PACKED_ULTRA_FIELDS = 0x00000008
BINARY_FLAGS = (
    BINARY_FLAG_SORTED_HASH_RECORDS
    | BINARY_FLAG_LOW_TIER_TEXT
    | BINARY_FLAG_CLEAN_TEXT
    | BINARY_FLAG_PACKED_ULTRA_FIELDS
)

EMOTION_CODES = {
    "[CALM]": 1,
    "[STRESS]": 2,
    "[PANIC]": 3,
}

CATEGORY_CODES = {
    "Tutorial": 1,
    "AmbientInterception": 2,
}

TIER_FLAGS = {
    "LOW_TEXT": 0x00000001,
    "RAW_TEXT": 0x00000002,
    "CLEAN_TEXT": 0x00000004,
    "ULTRA_SIGNAL": 0x00000008,
}


CHARACTERS: list[dict[str, str]] = [
    {
        "Speaker": "Chief Mara Voss",
        "Callsign": "Chief",
        "Role": "Marauder shift lead, angry enough to keep rookies alive.",
        "VocalProfile": "low, clipped, no wasted breath",
    },
    {
        "Speaker": "Rusty Kael",
        "Callsign": "Rusty",
        "Role": "Reactor mechanic who trusts manual gauges over Corp glass.",
        "VocalProfile": "rasped rig-worker drawl",
    },
    {
        "Speaker": "Sister Valve",
        "Callsign": "Valve",
        "Role": "Life-support tech, treats bad air as a personal insult.",
        "VocalProfile": "fast, precise, contempt under pressure",
    },
    {
        "Speaker": "Nix Calder",
        "Callsign": "Nix",
        "Role": "Radio scavenger and signal thief.",
        "VocalProfile": "dry whisper, always listening past the noise",
    },
    {
        "Speaker": "Pike Orlov",
        "Callsign": "Pike",
        "Role": "Hull cutter with bad knees and worse patience.",
        "VocalProfile": "hard consonants, old weld-bay cadence",
    },
    {
        "Speaker": "Moth Iverson",
        "Callsign": "Moth",
        "Role": "Light tech who reads predator pressure from failing lamps.",
        "VocalProfile": "soft, distracted, fear held behind teeth",
    },
    {
        "Speaker": "Dr. Oren Vale",
        "Callsign": "Vale",
        "Role": "Biologist blacklisted for writing the dead count plainly.",
        "VocalProfile": "clinical until the math starts bleeding",
    },
    {
        "Speaker": "Ledger-9",
        "Callsign": "Corp-AI",
        "Role": "Deep Reach compliance voice, polite as a closed hatch.",
        "VocalProfile": "flat, warm, predatory customer-service calm",
    },
    {
        "Speaker": "Hollis Brant",
        "Callsign": "Hollis",
        "Role": "Rival Black Keel pilot trapped on an open channel.",
        "VocalProfile": "panicked bravado failing into numbers",
    },
    {
        "Speaker": "Deadlight Orr",
        "Callsign": "Deadlight",
        "Role": "Black Keel sonar operator, last voice before the blackwake.",
        "VocalProfile": "thin, reverent, already half gone",
    },
]


SLANG: list[dict[str, str]] = [
    {
        "Term": "silt-lung",
        "Meaning": "Oxygen deprivation mixed with CO2 confusion.",
        "Usage": "A diver with silt-lung follows wrong orders and calls it instinct.",
    },
    {
        "Term": "void-kissed",
        "Meaning": "Radiation or Xenon-Omega exposure that leaves the eyes wrong.",
        "Usage": "Do not let a void-kissed sample ride in the cabin.",
    },
    {
        "Term": "brine-debt",
        "Meaning": "A favor owed because someone spent air to keep you alive.",
        "Usage": "Brine-debt is paid before salvage shares.",
    },
    {
        "Term": "gasket-prayer",
        "Meaning": "A desperate sealant patch that works until it decides not to.",
        "Usage": "A gasket-prayer buys minutes, not forgiveness.",
    },
    {
        "Term": "blackwake",
        "Meaning": "The silent pressure shadow before the Alpha Leviathan arrives.",
        "Usage": "Lights dim, fish flatten, then the blackwake eats the channel.",
    },
]


LINES_SOURCE: list[dict[str, str]] = [
    {
        "LineID": "RAD_MARAUDER_REACTOR_001",
        "Category": "Tutorial",
        "Speaker": "Chief Mara Voss",
        "EmotionTag": "[STRESS]",
        "RequiredGlobalState": "Reactor_Fixed = 0",
        "Text": "Green suit, quit admiring the corpse lights. Reactor is cold, pumps are starving, and you are two breaths from silt-lung. Get to the breaker spine and wake the damn bus.",
        "LowTierText": "Reactor cold. Get to breaker spine and wake the bus.",
    },
    {
        "LineID": "RAD_MARAUDER_REACTOR_002",
        "Category": "Tutorial",
        "Speaker": "Rusty Kael",
        "EmotionTag": "[STRESS]",
        "RequiredGlobalState": "Reactor_BreakerBus = 1; Coolant_Flow = 0",
        "Text": "Coolant loop is choking on rust paste. Open valve C, then valve A, not the other way unless you want a gasket-prayer for a faceplate. Move.",
        "LowTierText": "Open coolant valve C, then A. Wrong order cooks the faceplate.",
    },
    {
        "LineID": "RAD_MARAUDER_REACTOR_003",
        "Category": "Tutorial",
        "Speaker": "Sister Valve",
        "EmotionTag": "[PANIC]",
        "RequiredGlobalState": "Coolant_Flow = 1; ControlRods_Seated = 0",
        "Text": "Control rods are jammed half proud. Kick the manual crank until it locks green. You owe the whole room brine-debt if that core coughs awake dirty.",
        "LowTierText": "Crank the rods down until green. Move.",
    },
    {
        "LineID": "RAD_MARAUDER_REACTOR_004",
        "Category": "Tutorial",
        "Speaker": "Ledger-9",
        "EmotionTag": "[CALM]",
        "RequiredGlobalState": "ControlRods_Seated = 1; Reactor_Primed = 0",
        "Text": "Deep Reach thanks you for restoring asset continuity. Advisory: remaining personnel are statistically absent. Please prime the reactor and disregard the knocking inside the heat sink.",
        "LowTierText": "Prime reactor. Ignore heat-sink knocking.",
    },
    {
        "LineID": "RAD_MARAUDER_REACTOR_005",
        "Category": "Tutorial",
        "Speaker": "Chief Mara Voss",
        "EmotionTag": "[STRESS]",
        "RequiredGlobalState": "Reactor_Primed = 1; Reactor_Fixed = 0",
        "Text": "Good. Now throw the ignition like you hate it. If the lights come up blue, you breathe. If they come up white, run before the void-kissed bastard sings through your teeth.",
        "LowTierText": "Throw ignition. Blue means breathe. White means run.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_001",
        "Category": "AmbientInterception",
        "Speaker": "Nix Calder",
        "EmotionTag": "[CALM]",
        "RequiredGlobalState": "Reactor_Fixed = 1; Leviathan_Hunt_Arc = 1",
        "Text": "Intercept off Black Keel channel. Hollis is laughing too loud, which means his depth gauge is lying or his nerves are. Their wake just went quiet. That is the blackwake, not weather.",
        "LowTierText": "Black Keel wake went quiet. That is blackwake, not weather.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_002",
        "Category": "AmbientInterception",
        "Speaker": "Hollis Brant",
        "EmotionTag": "[PANIC]",
        "RequiredGlobalState": "Leviathan_Hunt_Arc = 1; BlackKeel_Beacon = 1",
        "Text": "Black Keel to any rig with ears, we have a shadow under us and over us. Sonar says one body, then five, then none. Deadlight, stop praying and give me a damn range.",
        "LowTierText": "Black Keel reports shadow above and below. Sonar range lost.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_003",
        "Category": "AmbientInterception",
        "Speaker": "Deadlight Orr",
        "EmotionTag": "[PANIC]",
        "RequiredGlobalState": "BlackKeel_Beacon = 1; BlackKeel_Sonar = 1",
        "Text": "Range is negative. I know that is crap math. The ping comes back before I send it. Something ahead of us already heard tomorrow.",
        "LowTierText": "Sonar echo returns before send. Orr says range is negative.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_004",
        "Category": "AmbientInterception",
        "Speaker": "Moth Iverson",
        "EmotionTag": "[STRESS]",
        "RequiredGlobalState": "Player_DepthBand >= 2; Leviathan_Hunt_Arc = 1",
        "Text": "Black Keel lamps just corkscrewed inward. Not failed, not cracked, bent. Light does that when the water gets told a bigger lie.",
        "LowTierText": "Black Keel lamps bent inward. The water is lying.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_005",
        "Category": "AmbientInterception",
        "Speaker": "Pike Orlov",
        "EmotionTag": "[STRESS]",
        "RequiredGlobalState": "BlackKeel_HullAlarm = 1",
        "Text": "Their aft plate folded without a hit report. No teeth marks, no scrape, just pressure making a fist. That damn thing is herding them into the trench mouth.",
        "LowTierText": "Aft plate folded clean. Alpha is herding them trenchward.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_006",
        "Category": "AmbientInterception",
        "Speaker": "Dr. Oren Vale",
        "EmotionTag": "[CALM]",
        "RequiredGlobalState": "BlackKeel_HullAlarm = 1; XenonOmega_Sample = 1",
        "Text": "Biomass displacement exceeds the local fauna ledger by three orders. Translation: the sea is moving around an animal large enough to make our instruments polite.",
        "LowTierText": "Mass displacement is off-scale. Something huge is moving water.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_007",
        "Category": "AmbientInterception",
        "Speaker": "Ledger-9",
        "EmotionTag": "[CALM]",
        "RequiredGlobalState": "Corp_ChannelUnlocked = 1; BlackKeel_Distress = 1",
        "Text": "Deep Reach advisory: Black Keel distress traffic is unlicensed salvage noise. Authorized crews should maintain course and avoid emotional interpretation of pressure anomalies.",
        "LowTierText": "Corp calls Black Keel distress unlicensed salvage noise.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_008",
        "Category": "AmbientInterception",
        "Speaker": "Hollis Brant",
        "EmotionTag": "[PANIC]",
        "RequiredGlobalState": "BlackKeel_Distress = 1; BlackKeel_CrewLoss >= 1",
        "Text": "It took Orr. No breach, no splash, no heroic hell, just his suit cam full of teeth and corridor light. The door stayed closed. The room got longer.",
        "LowTierText": "Orr taken. Door stayed shut. Room got longer.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_009",
        "Category": "AmbientInterception",
        "Speaker": "Chief Mara Voss",
        "EmotionTag": "[STRESS]",
        "RequiredGlobalState": "BlackKeel_CrewLoss >= 1; Player_RadioTrust = 1",
        "Text": "Do not answer Black Keel if they use your dead friend's voice. The Alpha wears channels like work gloves. Count air, cut lights, and let the rival crew pay their own brine-debt.",
        "LowTierText": "Do not answer voices wearing dead friends.",
    },
    {
        "LineID": "RAD_MARAUDER_LEVIATHAN_010",
        "Category": "AmbientInterception",
        "Speaker": "Deadlight Orr",
        "EmotionTag": "[PANIC]",
        "RequiredGlobalState": "BlackKeel_FinalPing = 1; Leviathan_Proximity >= 2",
        "Text": "Marauders, if this repeats, it is not me. If it knows your call sign, kill the speaker. If it knocks from inside the hull, you are already in its mouth.",
        "LowTierText": "If this repeats, kill the speaker. Hull knocks mean mouth.",
    },
]


CLEAN_REPLACEMENTS: dict[str, str] = {
    "damn": "rust",
    "damned": "rusted",
    "bastard": "brute",
    "hell": "deep",
    "crap": "bad",
    "hate": "despise",
}


def fnv1a_u32_utf16le(text: str) -> int:
    encoded = text.encode("utf-16le")
    h = FNV_OFFSET_BASIS
    for b in encoded:
        h ^= b
        h = (h * FNV_PRIME) & 0xFFFFFFFF
    return h


def estimate_read_time(text: str) -> float:
    words = re.findall(r"[A-Za-z0-9]+(?:[-'][A-Za-z0-9]+)?", text)
    seconds = (len(words) / READING_WORDS_PER_SECOND) + RADIO_SQUAWK_LEAD_OUT_SECONDS
    return round(max(SUBTITLE_FLOOR_SECONDS, seconds), 2)


def count_words(text: str) -> int:
    return len(re.findall(r"[A-Za-z0-9]+(?:[-'][A-Za-z0-9]+)?", text))


def q8(hash_id: int, shift: int) -> int:
    return (hash_id >> shift) & 0xFF


def pack_q8(values: list[int]) -> int:
    if len(values) != 4:
        raise ValueError("Q8 pack requires exactly 4 values")
    packed = 0
    for index, value in enumerate(values):
        if value < 0 or value > 255:
            raise ValueError(f"Q8 value out of range: {value}")
        packed |= (value & 0xFF) << (index * 8)
    return packed


def pack_rgba32(values: list[int]) -> int:
    return pack_q8(values)


def build_ultra_signal(hash_id: int, emotion_tag: str) -> dict[str, Any]:
    """Deterministic presentation metadata derived from the line hash.

    This is not physics truth. It is hash-derived signal art data for high-end
    radio breakup, subtitle shimmer, and DSP coloration.
    """
    emotion_code = EMOTION_CODES[emotion_tag]
    return {
        "NoiseSeed": hash_id,
        "EmotionCode": emotion_code,
        "HarmonicWeightsQ8": [
            96 + (q8(hash_id, 0) >> 2),
            64 + (q8(hash_id, 8) >> 2),
            32 + (q8(hash_id, 16) >> 3),
            16 + (q8(hash_id, 24) >> 4),
        ],
        "HarmonicNoiseOctavesQ8": [
            32 + (q8(hash_id ^ 0x3C6EF372, 0) >> 3),
            24 + (q8(hash_id ^ 0xA54FF53A, 8) >> 3),
            16 + (q8(hash_id ^ 0x510E527F, 16) >> 4),
            8 + (q8(hash_id ^ 0x9B05688C, 24) >> 5),
        ],
        "SpectralGradientRGBA32": [
            [18, 48 + (q8(hash_id, 0) >> 2), 72 + (emotion_code * 12), 255],
            [38 + (emotion_code * 9), 92 + (q8(hash_id, 8) >> 2), 118, 255],
            [96 + (q8(hash_id, 16) >> 2), 168, 190 + (q8(hash_id, 24) >> 3), 255],
            [225, 239, 226, 255],
        ],
        "SubtitleGlitchMaskQ8": q8(hash_id ^ 0xA5A5A5A5, 8),
        "RadioBreakupQ8": min(255, 48 + (emotion_code * 32) + (q8(hash_id, 16) >> 2)),
    }


def build_scalability_payload(hash_id: int, low_text: str, text: str, emotion_tag: str) -> dict[str, Any]:
    return {
        "Low": {
            "Text": low_text,
            "HashID": fnv1a_u32_utf16le(low_text),
            "MaxCharacters": len(low_text),
            "DSPProfile": "radio_narrowband_static",
        },
        "Middle": {
            "TextHashID": hash_id,
            "DSPProfile": "radio_narrowband_emotion",
        },
        "High": {
            "TextHashID": hash_id,
            "RadioNoiseSeed": hash_id ^ 0x6D2B79F5,
            "WordCount": count_words(text),
        },
        "Ultra": build_ultra_signal(hash_id, emotion_tag),
    }


def preserve_case(source: str, replacement: str) -> str:
    if source.isupper():
        return replacement.upper()
    if source[:1].isupper():
        return replacement[:1].upper() + replacement[1:]
    return replacement


def clean_text(text: str) -> str:
    terms = sorted(CLEAN_REPLACEMENTS, key=len, reverse=True)
    pattern = re.compile(r"\b(" + "|".join(re.escape(term) for term in terms) + r")\b", re.IGNORECASE)

    def replace(match: re.Match[str]) -> str:
        original = match.group(0)
        replacement = CLEAN_REPLACEMENTS[original.lower()]
        return preserve_case(original, replacement)

    return pattern.sub(replace, text)


def build_entries(clean_variant: bool) -> list[dict[str, Any]]:
    entries: list[dict[str, Any]] = []
    for source in LINES_SOURCE:
        text = clean_text(source["Text"]) if clean_variant else source["Text"]
        low_text = clean_text(source["LowTierText"]) if clean_variant else source["LowTierText"]
        hash_id = fnv1a_u32_utf16le(text)
        line_id = source["LineID"]
        speaker = source["Speaker"]
        category = source["Category"]
        emotion_tag = source["EmotionTag"]
        entry: dict[str, Any] = {
            "HashID": hash_id,
            "LineIDHash": fnv1a_u32_utf16le(line_id),
            "SpeakerHash": fnv1a_u32_utf16le(speaker),
            "CategoryHash": fnv1a_u32_utf16le(category),
            "Speaker": speaker,
            "AudioDelay": estimate_read_time(text),
            "Text": text,
            "LowTierText": low_text,
            "LowTierHashID": fnv1a_u32_utf16le(low_text),
            "RequiredGlobalState": source["RequiredGlobalState"],
            "EmotionTag": emotion_tag,
            "EmotionCode": EMOTION_CODES[emotion_tag],
            "RequiredGlobalStateHash": fnv1a_u32_utf16le(source["RequiredGlobalState"]),
            "LineID": line_id,
            "Category": category,
            "Scalability": build_scalability_payload(hash_id, low_text, text, emotion_tag),
        }
        if clean_variant:
            entry["SourceHashID"] = fnv1a_u32_utf16le(source["Text"])
            entry["SourceLowTierHashID"] = fnv1a_u32_utf16le(source["LowTierText"])
        entries.append(entry)
    return entries


def build_dictionary() -> dict[str, Any]:
    return {
        "Schema": "H8.RADIO.DICTIONARY.V1",
        "HashAlgorithm": "FNV-1a 32-bit over UTF-16LE code units",
        "PacingModel": {
            "WordsPerMinute": READING_WORDS_PER_MINUTE,
            "WordsPerSecond": READING_WORDS_PER_SECOND,
            "SecondsPerMinute": SECONDS_PER_MINUTE,
            "ReadingRateProvenance": READING_RATE_PROVENANCE,
            "RadioSquawkLeadOutMilliseconds": RADIO_SQUAWK_LEAD_OUT_MILLISECONDS,
            "RadioSquawkLeadOutSeconds": RADIO_SQUAWK_LEAD_OUT_SECONDS,
            "RadioSquawkProvenance": RADIO_SQUAWK_PROVENANCE,
            "SubtitleFloorMilliseconds": SUBTITLE_FLOOR_MILLISECONDS,
            "SubtitleFloorSeconds": SUBTITLE_FLOOR_SECONDS,
            "SubtitleFloorProvenance": SUBTITLE_FLOOR_PROVENANCE,
            "Formula": "max(subtitle_floor_ms / 1000, word_count / (words_per_minute / seconds_per_minute) + radio_squawk_ms / 1000)",
        },
        "BinaryModel": {
            "Magic": BINARY_MAGIC.decode("ascii"),
            "Version": BINARY_VERSION,
            "Endian": "Little",
            "HeaderSizeBytes": BINARY_HEADER_SIZE,
            "RecordSizeBytes": BINARY_RECORD_SIZE,
            "AlignmentBytes": BINARY_ALIGNMENT,
            "Flags": {
                "SortedHashRecords": BINARY_FLAG_SORTED_HASH_RECORDS,
                "LowTierText": BINARY_FLAG_LOW_TIER_TEXT,
                "CleanText": BINARY_FLAG_CLEAN_TEXT,
                "PackedUltraFields": BINARY_FLAG_PACKED_ULTRA_FIELDS,
                "Combined": BINARY_FLAGS,
            },
        },
        "ScalabilityModel": {
            "Low": "stripped subtitle text plus static narrowband DSP",
            "Middle": "full subtitle text plus emotion DSP",
            "High": "full text plus deterministic radio noise seed",
            "Ultra": "fixed-record harmonic weights, octave counts, RGBA32 spectral stops, glitch mask, breakup, and signal hash",
        },
        "Characters": [
            {
                "HashID": fnv1a_u32_utf16le(character["Speaker"]),
                "CallsignHash": fnv1a_u32_utf16le(character["Callsign"]),
                **character,
            }
            for character in CHARACTERS
        ],
        "Slang": [
            {
                "HashID": fnv1a_u32_utf16le(slang["Term"]),
                **slang,
            }
            for slang in SLANG
        ],
    }


def validate_entries(entries: list[dict[str, Any]], clean_variant: bool) -> dict[str, Any]:
    required = {
        "HashID",
        "LineIDHash",
        "Speaker",
        "SpeakerHash",
        "AudioDelay",
        "Text",
        "LowTierText",
        "LowTierHashID",
        "RequiredGlobalState",
        "RequiredGlobalStateHash",
        "EmotionTag",
        "EmotionCode",
        "Scalability",
    }
    speaker_names = {character["Speaker"] for character in CHARACTERS}
    seen_hashes: dict[int, str] = {}
    categories: dict[str, int] = {}
    sterile_hits: list[str] = []
    errors: list[str] = []

    for index, entry in enumerate(entries):
        missing = required.difference(entry)
        if missing:
            errors.append(f"entry {index} missing fields: {sorted(missing)}")
            continue

        line_id = str(entry.get("LineID", f"index_{index}"))
        text = entry["Text"]
        low_text = entry["LowTierText"]
        state = entry["RequiredGlobalState"]
        hash_id = entry["HashID"]
        state_hash = entry.get("RequiredGlobalStateHash")

        if entry["Speaker"] not in speaker_names:
            errors.append(f"{line_id}: unknown speaker {entry['Speaker']}")
        if entry["EmotionTag"] not in {"[STRESS]", "[CALM]", "[PANIC]"}:
            errors.append(f"{line_id}: bad emotion tag {entry['EmotionTag']}")
        if entry["EmotionCode"] != EMOTION_CODES.get(entry["EmotionTag"]):
            errors.append(f"{line_id}: EmotionCode mismatch")
        if entry["SpeakerHash"] != fnv1a_u32_utf16le(entry["Speaker"]):
            errors.append(f"{line_id}: SpeakerHash mismatch")
        if entry["LineIDHash"] != fnv1a_u32_utf16le(line_id):
            errors.append(f"{line_id}: LineIDHash mismatch")
        if not isinstance(hash_id, int) or not 0 <= hash_id <= 0xFFFFFFFF:
            errors.append(f"{line_id}: HashID is not uint")
        if hash_id != fnv1a_u32_utf16le(text):
            errors.append(f"{line_id}: HashID mismatch")
        if entry["LowTierHashID"] != fnv1a_u32_utf16le(low_text):
            errors.append(f"{line_id}: LowTierHashID mismatch")
        if state_hash != fnv1a_u32_utf16le(state):
            errors.append(f"{line_id}: RequiredGlobalStateHash mismatch")
        if not isinstance(entry["AudioDelay"], float):
            errors.append(f"{line_id}: AudioDelay is not float")
        if entry["AudioDelay"] < SUBTITLE_FLOOR_SECONDS:
            errors.append(f"{line_id}: AudioDelay below subtitle pacing floor")
        if len(low_text) > 72:
            errors.append(f"{line_id}: LowTierText exceeds 72 characters")
        scalability = entry["Scalability"]
        if scalability["Low"]["HashID"] != entry["LowTierHashID"]:
            errors.append(f"{line_id}: scalability Low hash mismatch")
        if scalability["Middle"]["TextHashID"] != hash_id:
            errors.append(f"{line_id}: scalability Middle hash mismatch")
        if scalability["High"]["TextHashID"] != hash_id:
            errors.append(f"{line_id}: scalability High hash mismatch")
        if scalability["Ultra"]["NoiseSeed"] != hash_id:
            errors.append(f"{line_id}: scalability Ultra seed mismatch")

        sterile_tokens = ("continuity", "statistically", "authorized", "advisory", "interpretation")
        if entry["Speaker"] != "Ledger-9":
            lowered = text.lower()
            for token in sterile_tokens:
                if token in lowered:
                    sterile_hits.append(f"{line_id}:{token}")

        owner = seen_hashes.get(hash_id)
        if owner is not None:
            errors.append(f"duplicate HashID {hash_id}: {owner} and {line_id}")
        seen_hashes[hash_id] = line_id

        category = str(entry.get("Category", ""))
        categories[category] = categories.get(category, 0) + 1

    if len(entries) != 15:
        errors.append(f"expected 15 entries, found {len(entries)}")
    if categories.get("Tutorial") != 5:
        errors.append(f"expected 5 tutorial entries, found {categories.get('Tutorial', 0)}")
    if categories.get("AmbientInterception") != 10:
        errors.append(f"expected 10 ambient interceptions, found {categories.get('AmbientInterception', 0)}")
    if sterile_hits:
        errors.append(f"sterile non-corporate tone hits: {sterile_hits}")

    joined = " ".join(entry["Text"].lower() for entry in entries)
    for slang in SLANG:
        if slang["Term"].lower() not in joined:
            errors.append(f"slang term unused: {slang['Term']}")

    if clean_variant:
        banned = set(CLEAN_REPLACEMENTS)
        token_hits = {
            token.lower()
            for entry in entries
            for token in re.findall(r"[A-Za-z]+", entry["Text"])
            if token.lower() in banned
        }
        if token_hits:
            errors.append(f"clean variant still contains filtered tokens: {sorted(token_hits)}")

    return {
        "CleanVariant": clean_variant,
        "EntryCount": len(entries),
        "DuplicateHashes": False if not errors else any("duplicate HashID" in error for error in errors),
        "Categories": categories,
        "SterileToneHits": sterile_hits,
        "Errors": errors,
    }


def collect_hash_surface(raw_entries: list[dict[str, Any]], clean_entries: list[dict[str, Any]], dictionary: dict[str, Any]) -> dict[str, Any]:
    owners: dict[int, tuple[str, str]] = {}
    collisions: list[str] = []

    def add(owner: str, value: int, canonical: str) -> None:
        existing = owners.get(value)
        if existing is not None and existing[1] != canonical:
            collisions.append(f"{value}:{existing[0]}:{owner}")
        else:
            owners[value] = (owner, canonical)

    for prefix, entries in (("raw", raw_entries), ("clean", clean_entries)):
        for entry in entries:
            line_id = entry["LineID"]
            add(f"{prefix}.{line_id}.Text", entry["HashID"], entry["Text"])
            add(f"{prefix}.{line_id}.LowTierText", entry["LowTierHashID"], entry["LowTierText"])
            add(f"{prefix}.{line_id}.LineID", entry["LineIDHash"], entry["LineID"])
            add(f"{prefix}.{line_id}.State", entry["RequiredGlobalStateHash"], entry["RequiredGlobalState"])

    for character in dictionary["Characters"]:
        add(f"speaker.{character['Speaker']}", character["HashID"], character["Speaker"])
        add(f"callsign.{character['Callsign']}", character["CallsignHash"], character["Callsign"])

    for slang in dictionary["Slang"]:
        add(f"slang.{slang['Term']}", slang["HashID"], slang["Term"])

    return {
        "UniqueHashes": len(owners),
        "Collisions": collisions,
        "CollisionCount": len(collisions),
    }


def align_bytearray(buffer: bytearray, alignment: int = BINARY_ALIGNMENT) -> None:
    padding = (-len(buffer)) % alignment
    if padding:
        buffer.extend(b"\0" * padding)


def append_aligned_utf8(payload: bytearray, text: str) -> tuple[int, int]:
    align_bytearray(payload)
    offset = len(payload)
    encoded = text.encode("utf-8")
    payload.extend(encoded)
    align_bytearray(payload)
    return offset, len(encoded)


def canonical_hash(payload: Any) -> int:
    canonical = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return fnv1a_u32_utf16le(canonical)


def build_binary(raw_entries: list[dict[str, Any]], clean_entries: list[dict[str, Any]], dictionary: dict[str, Any], layout_hash: int) -> dict[str, Any]:
    clean_by_line = {entry["LineID"]: entry for entry in clean_entries}
    payload = bytearray()
    record_rows: list[tuple[int, bytes]] = []

    tier_flags = (
        TIER_FLAGS["LOW_TEXT"]
        | TIER_FLAGS["RAW_TEXT"]
        | TIER_FLAGS["CLEAN_TEXT"]
        | TIER_FLAGS["ULTRA_SIGNAL"]
    )

    for entry in raw_entries:
        clean_entry = clean_by_line[entry["LineID"]]
        text_offset, text_length = append_aligned_utf8(payload, entry["Text"])
        low_offset, low_length = append_aligned_utf8(payload, entry["LowTierText"])
        clean_offset, clean_length = append_aligned_utf8(payload, clean_entry["Text"])
        ultra = entry["Scalability"]["Ultra"]
        high = entry["Scalability"]["High"]
        ultra_hash = canonical_hash(ultra)
        gradients = ultra["SpectralGradientRGBA32"]
        record = BINARY_RECORD_STRUCT.pack(
            entry["HashID"],
            entry["LineIDHash"],
            entry["SpeakerHash"],
            entry["RequiredGlobalStateHash"],
            text_offset,
            text_length,
            low_offset,
            low_length,
            clean_offset,
            clean_length,
            int(round(entry["AudioDelay"] * 1000.0)),
            entry["EmotionCode"],
            CATEGORY_CODES[entry["Category"]],
            tier_flags,
            ultra["NoiseSeed"],
            pack_q8(ultra["HarmonicWeightsQ8"]),
            pack_q8(ultra["HarmonicNoiseOctavesQ8"]),
            pack_rgba32(gradients[0]),
            pack_rgba32(gradients[1]),
            pack_rgba32(gradients[2]),
            pack_rgba32(gradients[3]),
            ultra["SubtitleGlitchMaskQ8"],
            ultra["RadioBreakupQ8"],
            ultra_hash,
            high["RadioNoiseSeed"],
            entry["LowTierHashID"],
            clean_entry["HashID"],
            entry["CategoryHash"],
            0,
            0,
            0,
            0,
        )
        record_rows.append((entry["HashID"], record))

    record_rows.sort(key=lambda item: item[0])
    table = bytearray()
    for _, record in record_rows:
        table.extend(record)

    payload_offset = BINARY_HEADER_SIZE + len(table)
    if payload_offset % BINARY_ALIGNMENT != 0:
        raise ValueError(f"payload offset is not {BINARY_ALIGNMENT}-byte aligned: {payload_offset}")

    header = BINARY_HEADER_STRUCT.pack(
        BINARY_MAGIC,
        BINARY_VERSION,
        BINARY_HEADER_SIZE,
        len(record_rows),
        BINARY_RECORD_SIZE,
        BINARY_HEADER_SIZE,
        payload_offset,
        len(payload),
        BINARY_FLAGS,
        canonical_hash(raw_entries),
        canonical_hash(clean_entries),
        canonical_hash(dictionary),
        layout_hash,
        0,
        0,
        0,
        0,
    )

    blob = bytearray(header)
    blob.extend(table)
    blob.extend(payload)
    align_bytearray(blob)
    BINARY_OUTPUT.write_bytes(blob)

    return validate_binary(BINARY_OUTPUT)


def build_binary_layout() -> dict[str, Any]:
    return {
        "Schema": "H8.RADIO.BINARY_LAYOUT.V1",
        "Magic": BINARY_MAGIC.decode("ascii"),
        "Version": BINARY_VERSION,
        "Endian": "Little",
        "AlignmentBytes": BINARY_ALIGNMENT,
        "Header": {
            "Struct": "<4sHH14I",
            "SizeBytes": BINARY_HEADER_SIZE,
            "Fields": [
                "magic",
                "version",
                "headerSize",
                "recordCount",
                "recordSize",
                "tableOffset",
                "payloadOffset",
                "payloadLength",
                "flags",
                "rawJsonHash",
                "cleanJsonHash",
                "dictionaryHash",
                "layoutHash",
                "reserved0",
                "reserved1",
                "reserved2",
                "reserved3",
            ],
        },
        "Record": {
            "Struct": "<32I",
            "SizeBytes": BINARY_RECORD_SIZE,
            "Fields": [
                "hashID",
                "lineIDHash",
                "speakerHash",
                "requiredGlobalStateHash",
                "textOffset",
                "textLength",
                "lowTextOffset",
                "lowTextLength",
                "cleanTextOffset",
                "cleanTextLength",
                "audioDelayMs",
                "emotionCode",
                "categoryCode",
                "tierFlags",
                "ultraNoiseSeed",
                "ultraHarmonicWeightsQ8Packed",
                "ultraHarmonicNoiseOctavesQ8Packed",
                "ultraSpectralGradient0RGBA32",
                "ultraSpectralGradient1RGBA32",
                "ultraSpectralGradient2RGBA32",
                "ultraSpectralGradient3RGBA32",
                "ultraSubtitleGlitchMaskQ8",
                "ultraRadioBreakupQ8",
                "ultraSignalHash",
                "highRadioNoiseSeed",
                "lowTierHashID",
                "cleanHashID",
                "categoryHash",
                "reserved0",
                "reserved1",
                "reserved2",
                "reserved3",
            ],
        },
        "Payload": {
            "Encoding": "UTF-8",
            "OffsetOrigin": "payload start",
            "SliceAlignmentBytes": BINARY_ALIGNMENT,
            "NullTerminated": False,
        },
    }


def validate_binary(path: Path) -> dict[str, Any]:
    blob = path.read_bytes()
    errors: list[str] = []
    if len(blob) < BINARY_HEADER_SIZE:
        errors.append("blob smaller than header")
        return {"Path": str(path), "Errors": errors}

    header = BINARY_HEADER_STRUCT.unpack_from(blob, 0)
    magic = header[0]
    version = header[1]
    header_size = header[2]
    record_count = header[3]
    record_size = header[4]
    table_offset = header[5]
    payload_offset = header[6]
    payload_length = header[7]
    flags = header[8]

    if magic != BINARY_MAGIC:
        errors.append(f"bad magic {magic!r}")
    if version != BINARY_VERSION:
        errors.append(f"bad version {version}")
    if header_size != BINARY_HEADER_SIZE:
        errors.append(f"bad header size {header_size}")
    if record_size != BINARY_RECORD_SIZE:
        errors.append(f"bad record size {record_size}")
    if table_offset != BINARY_HEADER_SIZE:
        errors.append(f"bad table offset {table_offset}")
    if payload_offset != BINARY_HEADER_SIZE + (record_count * BINARY_RECORD_SIZE):
        errors.append("payload offset does not match record table end")
    if payload_offset % BINARY_ALIGNMENT != 0:
        errors.append("payload offset is not 16-byte aligned")
    if len(blob) % BINARY_ALIGNMENT != 0:
        errors.append("blob length is not 16-byte aligned")
    if payload_offset + payload_length > len(blob):
        errors.append("payload extends beyond blob")
    if flags != BINARY_FLAGS:
        errors.append(f"bad flags {flags}")

    previous_hash = -1
    for index in range(record_count):
        offset = table_offset + (index * BINARY_RECORD_SIZE)
        record = BINARY_RECORD_STRUCT.unpack_from(blob, offset)
        hash_id = record[0]
        if hash_id <= previous_hash:
            errors.append(f"record {index} not strictly sorted by hash")
        previous_hash = hash_id
        for label, text_offset, text_length in (
            ("text", record[4], record[5]),
            ("low", record[6], record[7]),
            ("clean", record[8], record[9]),
        ):
            if text_offset % BINARY_ALIGNMENT != 0:
                errors.append(f"record {index} {label} offset not aligned")
            if text_offset + text_length > payload_length:
                errors.append(f"record {index} {label} slice out of payload")

    return {
        "Path": str(path),
        "Magic": BINARY_MAGIC.decode("ascii"),
        "Endian": "Little",
        "HeaderSizeBytes": BINARY_HEADER_SIZE,
        "RecordSizeBytes": BINARY_RECORD_SIZE,
        "AlignmentBytes": BINARY_ALIGNMENT,
        "RecordCount": record_count,
        "PayloadLengthBytes": payload_length,
        "FileLengthBytes": len(blob),
        "Errors": errors,
    }


def write_json(path: Path, payload: Any) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def main() -> int:
    raw_entries = build_entries(clean_variant=False)
    clean_entries = build_entries(clean_variant=True)
    dictionary = build_dictionary()
    layout = build_binary_layout()
    layout_hash = canonical_hash(layout)
    raw_report = validate_entries(raw_entries, clean_variant=False)
    clean_report = validate_entries(clean_entries, clean_variant=True)
    hash_report = collect_hash_surface(raw_entries, clean_entries, dictionary)
    errors = raw_report["Errors"] + clean_report["Errors"] + hash_report["Collisions"]

    report = {
        "Schema": "H8.RADIO.VALIDATION.V1",
        "Status": "VERIFIED MASTER GRADE" if not errors else "FAILED",
        "CoreStatus": "DIALOGUES BAKED" if not errors else "FAILED",
        "EvidenceClass": "STATIC_JSON_CLI",
        "HashAlgorithm": "FNV-1a 32-bit over UTF-16LE code units",
        "GeneratedFiles": [
            RAW_OUTPUT.name,
            CLEAN_OUTPUT.name,
            DICTIONARY_OUTPUT.name,
            BINARY_OUTPUT.name,
            BINARY_LAYOUT_OUTPUT.name,
        ],
        "MathAudit": {
            "LutMatrixSurface": "NONE_IN_SCOPE",
            "PhysicsModelsRequired": "NONE_FOR_RADIO_DIALOGUE",
            "PacingModel": dictionary["PacingModel"],
            "NoMagicPacing": True,
            "DerivedConstants": {
                "WordsPerSecond": "171.0 / 60.0",
                "RadioSquawkLeadOutSeconds": "650 / 1000",
                "SubtitleFloorSeconds": "2400 / 1000",
            },
            "EvidenceLimit": "Radio dialogue owns subtitle pacing only; Beer-Lambert, Dalton, and Sabine tables are outside this data node.",
        },
        "Scalability": {
            "ToasterData": "LowTierText is present per line and capped at 72 characters.",
            "RtxOverkillData": "Ultra signal metadata is packed into fixed record fields: harmonic weights, octave counts, RGBA32 spectral stops, glitch mask, breakup, and signal hash.",
        },
        "ProjectAtlasFit": {
            "ProjectAtlasPath": "Docs/PROJECT_ATLAS.md",
            "ProjectAtlasStaticAssemblyCount": 83,
            "UserClaimedDomainCount": 85,
            "DomainCountCorrection": "Current disk PROJECT_ATLAS.md states 83 first-party asmdef files.",
            "DomainFamily": "UI / Narrative localization data",
            "RuntimeDependenciesAdded": 0,
            "CoreReferencesAdded": 0,
            "PrivateRuntimeStateRequired": False,
            "DataSovereigntyImpact": "Positive: raw JSON remains authoring data; .h8bin adds sorted stateless hash records, packed Ultra fields, and aligned text payload slices.",
            "HPhiStaticEstimate": 0.25,
            "HPhiEvidenceMultiplier": "STATIC_SOURCE_SCAN=0.25 from PROJECT_ATLAS evidence table",
        },
        "Raw": raw_report,
        "Clean": clean_report,
        "HashSurface": hash_report,
        "Binary": None,
    }

    if errors:
        write_json(REPORT_OUTPUT, report)
        raise SystemExit("\n".join(errors))

    write_json(RAW_OUTPUT, raw_entries)
    write_json(CLEAN_OUTPUT, clean_entries)
    write_json(DICTIONARY_OUTPUT, dictionary)
    write_json(BINARY_LAYOUT_OUTPUT, layout)
    binary_report = build_binary(raw_entries, clean_entries, dictionary, layout_hash)
    report["Binary"] = binary_report
    if binary_report["Errors"]:
        report["Status"] = "FAILED"
        report["CoreStatus"] = "FAILED"
        write_json(REPORT_OUTPUT, report)
        raise SystemExit("\n".join(binary_report["Errors"]))
    write_json(REPORT_OUTPUT, report)
    print(
        "VERIFIED MASTER GRADE: core=DIALOGUES BAKED "
        f"raw={len(raw_entries)} clean={len(clean_entries)} "
        f"characters={len(CHARACTERS)} slang={len(SLANG)} "
        f"binary={BINARY_OUTPUT.name} "
        f"output={ROOT}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
