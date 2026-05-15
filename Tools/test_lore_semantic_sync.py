#!/usr/bin/env python3
"""Static semantic sync tests for NARRATIVE_SEMANTIC_AUDITOR outputs."""

from __future__ import annotations

import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
LORE_BIBLE = ROOT / "Docs" / "Lore" / "Lore_Bible.md"
LOCALIZATION = ROOT / "Data" / "Localization" / "en_US.json"
TERMINAL_BANK = ROOT / "Data" / "Lore" / "CockpitTerminalBootErrors.raw.txt"
SUIT_RESOLVER = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "SuitUpgradeResolver.cs"
SUIT_MANAGER = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "SuitUpgradeManager.cs"
SUIT_ASSET = ROOT / "Assets" / "_Project" / "Data" / "Lore" / "SuitUpgrades" / "SuitUpgrade_Oxygen_Tier1_AuxReservoir.asset"
GI_RELAY = ROOT / "Assets" / "_Project" / "Scripts" / "Lighting" / "HectonGIRelaySystem.cs"


class LoreSemanticSyncTests(unittest.TestCase):
    def read(self, path: Path) -> str:
        return path.read_text(encoding="utf-8")

    def localization_text(self, loc_id: str) -> str:
        root = json.loads(LOCALIZATION.read_text(encoding="utf-8"))
        for entry in root["entries"]:
            if entry.get("LocID") == loc_id:
                return entry["Text"]
        self.fail(f"Missing LocID: {loc_id}")

    def test_oxygen_tank_lore_matches_resolver_and_asset(self) -> None:
        resolver = self.read(SUIT_RESOLVER)
        asset = self.read(SUIT_ASSET)
        lore = self.read(LORE_BIBLE)
        audit_text = self.localization_text("LORE_AUDIT_SUIT_OXYGEN_TANK_DTO_SYNC")
        description = self.localization_text("SUIT_UPGRADE_SUIT_OXYGEN_T1_AUX_RESERVOIR_DESCRIPTION")

        self.assertRegex(resolver, r"HighCapacityTank\s*=\s*1UL\s*<<\s*0")
        self.assertIn('case "suit_oxygen_t1_aux_reservoir":', resolver)
        self.assertIn("return SuitUpgrades.HighCapacityTank;", resolver)
        self.assertIn("stats.MaxO2 += 4f;", resolver)
        self.assertIn("MaxO2 = 100f", resolver)
        self.assertIn("CrushDepth = 50f", resolver)
        self.assertIn("deltaMaxOxygen: 4", asset)
        self.assertIn("deltaSafeDepth: 0", asset)

        for text in (lore, audit_text, description):
            self.assertIn("suit_oxygen_t1_aux_reservoir", text)
            self.assertIn("HighCapacityTank", text)
            self.assertIn("+4 MaxO2", text)

        self.assertIn("0x0000000000000001", lore)
        self.assertIn("0x0000000000000001", description)
        self.assertIn("depth and pressure claims", audit_text)

    def test_suit_manager_oxygen_equipment_aliases_are_documented(self) -> None:
        manager = self.read(SUIT_MANAGER)
        lore = self.read(LORE_BIBLE)
        audit_text = self.localization_text("LORE_AUDIT_SUIT_OXYGEN_TANK_DTO_SYNC")

        self.assertIn("ItemEquipOxygenRigT1Hash = 0xF0B55FA2u", manager)
        self.assertIn("ItemEquipOxygenRigT2Hash = 0xEFB55E0Fu", manager)
        self.assertIsNotNone(
            re.search(
                r"case\s+SuitUpgrades\.HighCapacityTank:.*?ItemEquipOxygenRigT1Hash.*?ItemEquipOxygenRigT2Hash",
                manager,
                re.DOTALL,
            )
        )

        for text in (lore, audit_text):
            self.assertIn("ItemEquipOxygenRigT1Hash", text)
            self.assertIn("ItemEquipOxygenRigT2Hash", text)

    def test_gi_depth_palette_sync_is_documented(self) -> None:
        gi_relay = self.read(GI_RELAY)
        lore = self.read(LORE_BIBLE)
        localization = self.localization_text("LORE_GI_DEPTH_RELAY_SYNC")

        self.assertIn("DepthPaletteFullDepthMeters = 500f", gi_relay)
        self.assertIn("500 m", lore)
        self.assertIn("500 m", localization)
        self.assertIn("must not promise unique GI depth colors", lore)
        self.assertIn("must not promise new depth colors", localization)

    def test_ecological_collapse_has_exactly_eighty_sector_beats(self) -> None:
        lore = self.read(LORE_BIBLE)
        sectors = re.findall(r"(?m)^[0-9]{2}\. Sector ", lore)
        self.assertEqual(len(sectors), 80)

    def test_terminal_bank_has_exactly_fifty_raw_records(self) -> None:
        lines = [line for line in TERMINAL_BANK.read_text(encoding="utf-8").splitlines() if line.strip()]
        self.assertEqual(len(lines), 50)
        seen_ids: set[str] = set()
        boot_count = 0
        error_count = 0
        for line in lines:
            self.assertRegex(line, r"^(BOOT|ERROR)_[0-9]{3}\|.+")
            raw_id, text = line.split("|", 1)
            self.assertNotIn(raw_id, seen_ids)
            seen_ids.add(raw_id)
            self.assertTrue(text.strip())
            number = int(raw_id.rsplit("_", 1)[1])
            if raw_id.startswith("BOOT_"):
                boot_count += 1
                self.assertLessEqual(number, 25)
            else:
                error_count += 1
                self.assertGreaterEqual(number, 26)
        self.assertEqual(boot_count, 25)
        self.assertEqual(error_count, 25)
        self.assertEqual(sorted(seen_ids), [f"BOOT_{index:03d}" for index in range(1, 26)] + [f"ERROR_{index:03d}" for index in range(26, 51)])


if __name__ == "__main__":
    unittest.main()
