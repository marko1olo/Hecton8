#!/usr/bin/env python3
"""Tests for PlatformPortabilityProofAudit."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import PlatformPortabilityProofAudit as audit


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload), encoding="utf-8")


class PlatformPortabilityProofAuditTests(unittest.TestCase):
    def test_detects_quest_scaffold_but_missing_runtime_proof(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_") as tmp:
            root = Path(tmp)
            deps = {
                "com.unity.xr.management": "4.6.0",
                "com.unity.xr.openxr": "1.17.0",
                "com.unity.xr.meta-openxr": "2.5.0",
            }
            lock_deps = {key: {"version": value} for key, value in deps.items()}
            write_json(root / "Packages" / "manifest.json", {"dependencies": deps})
            write_json(root / "Packages" / "packages-lock.json", {"dependencies": lock_deps})
            settings = """
AndroidTargetSdkVersion: 35
AndroidMinSdkVersion: 25
AndroidTargetArchitectures: 2
m_BuildTargetVRSettings: []
applicationIdentifier:
  Android: com.test.hecton8
scriptingBackend:
  Android: 1
il2cppCompilerConfiguration: {}
"""
            (root / "ProjectSettings").mkdir(parents=True)
            (root / "ProjectSettings" / "ProjectSettings.asset").write_text(settings, encoding="utf-8")
            (root / "ProjectSettings" / "XRSettings.asset").write_text(
                '{"m_SettingKeys":["VR Device Disabled"],"m_SettingValues":["False"]}',
                encoding="utf-8",
            )

            payload = audit.build_payload(root)

        self.assertTrue(payload["readiness"]["androidQuestScaffold"])
        self.assertFalse(payload["readiness"]["xrProviderSerializedProof"])
        self.assertFalse(payload["readiness"]["addressablesContentPresent"])
        self.assertFalse(payload["readiness"]["dataMonolithPresent"])
        self.assertFalse(payload["readiness"]["buildArtifactPresent"])

    def test_detects_payloads_builds_and_plugins(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_platform_audit_full_") as tmp:
            root = Path(tmp)
            (root / "Assets" / "AddressableAssetsData").mkdir(parents=True)
            (root / "Assets" / "AddressableAssetsData" / "settings.asset").write_text("x", encoding="utf-8")
            monolith = root / "Assets" / "StreamingAssets" / "Hecton8" / "DataMonolith" / "static_data.h8bin"
            monolith.parent.mkdir(parents=True)
            monolith.write_bytes(b"h8")
            build = root / "Builds" / "Win" / "Hecton8.exe"
            build.parent.mkdir(parents=True)
            build.write_bytes(b"exe")
            plugin = root / "Assets" / "_Project" / "Plugins" / "Windows" / "x86_64" / "HectonAudioKernel.dll"
            plugin.parent.mkdir(parents=True)
            plugin.write_bytes(b"dll")

            payload = audit.build_payload(root)

        self.assertTrue(payload["readiness"]["addressablesContentPresent"])
        self.assertTrue(payload["readiness"]["dataMonolithPresent"])
        self.assertTrue(payload["readiness"]["buildArtifactPresent"])
        self.assertEqual(payload["nativePlugins"]["pluginFileCount"], 1)


if __name__ == "__main__":
    unittest.main()
