import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateVfxParticleBudgetCatalog as validator  # noqa: E402


class ValidateVfxParticleBudgetCatalogTests(unittest.TestCase):
    def test_tier_rows_accept_current_quality_constant_prefixes(self) -> None:
        data = {
            "computeSafety": {
                "mx350MaxThreadsPerDispatch": 262144,
                "defaultThreadsPerGroup": 64,
            },
            "tierBudgets": [
                self._tier("Low", 8192, 4096, 2048, 2048, 128, 1.25, 2, 4),
                self._tier("Mid", 32768, 16384, 8192, 8192, 512, 1.00, 4, 3),
                self._tier("High", 65536, 32768, 16384, 16384, 1024, 0.75, 6, 2),
                self._tier("Ultra", 131072, 65536, 32768, 32768, 2048, 0.50, 8, 1),
            ],
        }
        catalog = "\n".join(
            (
                self._tier_constants("MinimumQuality", 8192, 4096, 2048, 2048, 1.25, 2, 4),
                self._tier_constants("MiddleQuality", 32768, 16384, 8192, 8192, 1.00, 4, 3),
                self._tier_constants("MaximumQuality", 65536, 32768, 16384, 16384, 0.75, 6, 2),
                self._tier_constants("OverkillQuality", 131072, 65536, 32768, 32768, 0.50, 8, 1),
            )
        )

        validator.validate_tier_rows(data, catalog)

    def test_homeostasis_particle_advection_alias_is_valid(self) -> None:
        data = {
            "targetConsumer": "REND_DYNAMIC_RESOLUTION_ADAPTER",
            "systemBitBindings": [
                {"name": "ParticleAdvection", "bitIndex": 5, "bitHex": "0x20"},
                {"name": "VolumetricFogHighRes", "bitIndex": 6, "bitHex": "0x40"},
                {"name": "NonCriticalVfx", "bitIndex": 20, "bitHex": "0x100000"},
            ],
            "pressureGatePolicy": [
                {"pressureLevel": 1, "disableMaskHex": "0x20"},
                {"pressureLevel": 2, "disableMaskHex": "0x100060"},
                {"pressureLevel": 3, "disableMaskHex": "0x100060"},
            ],
        }
        catalog = "\n".join(
            (
                "public const ulong ParticleAdvectionMask = (ulong)SystemBit.ParticleAdvection;",
                "public const ulong VolumetricFogHighResMask = (ulong)SystemBit.VolumetricFogHighRes;",
                "public const ulong NonCriticalVfxMask = (ulong)SystemBit.NonCriticalVfx;",
                "PressureLevel1DisableMask",
                "PressureLevel2DisableMask",
                "PressureLevel3DisableMask",
                "ResolvePolicyKillSwitchMask",
            )
        )
        renderer = "ResolvePolicyKillSwitchMask"
        homeostasis = "\n".join(
            (
                "MicroDebrisAdvection = 1UL << 5,",
                "ParticleAdvection = MicroDebrisAdvection,",
                "VolumetricFogHighRes = 1UL << 6,",
                "NonCriticalVfx = 1UL << 20,",
            )
        )
        drs_adapter = "\n".join(
            (
                "public sealed class ThermalDynamicResolutionAdapter : IDynamicResolutionRuntime {}",
                "private const int TelemetryCapacity = 300;",
                "private const string DumpFilePrefix = \"Dump_THERMAL_DRS_\";",
                "private void DumpBlackBoxOnce() {}",
                "private void WriteDump() { NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount); }",
            )
        )

        validator.validate_handoff_contract(data, catalog, renderer, homeostasis, drs_adapter)

    def test_pressure_gate_rejects_stale_renderer_terms(self) -> None:
        data = {
            "pressureGatePolicy": [
                {"pressureLevel": 1, "forceBudgetTier": "Mid"},
                {"pressureLevel": 2, "forceBudgetTier": "Low"},
                {
                    "pressureLevel": 3,
                    "forceBudgetTier": "Low",
                    "emergencyMarineSnowMultiplier": 0.5,
                },
            ],
            "runtimeConsumers": ["Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute"],
        }
        catalog = "\n".join(
            (
                "ResolveContinuousBudget",
                "math.smoothstep(0f, 0.45f, q)",
                "math.smoothstep(0.35f, 0.85f, q)",
                "math.smoothstep(0.72f, 1f, q)",
                "pressureLevel >= 2",
                "pressureLevel == 1",
                "NonCriticalVfxMask",
                "EmergencyMarineSnowMultiplierPermille",
                "fluidType == VFXEmissionProfile.FluidType.Bubble",
                "fluidType == VFXEmissionProfile.FluidType.Debris",
                "ResolvePolicyKillSwitchMask",
                "PressureLevel2DisableMask",
                "PressureLevel3DisableMask",
                "pressureLevel >= 3",
            )
        )
        renderer = "HomeostasisBrain.PressureLevel"
        compute = "\n".join(
            (
                "float scalabilityQuality = saturate(_MarineSnowScalabilityParams.x * 0.5);",
                "float highDetailWeight = smoothstep(0.5, 1.0, scalabilityQuality);",
                "bool flowAdvectionEnabled = scalabilityQuality > EPSILON",
                "if (flowAdvectionEnabled)",
                "EvaluateShallowWaterFieldData(particle.Pos)",
                "particle.Vel.xz *= saturate(1.0 - dt * 2.0);",
            )
        )

        with self.assertRaises(SystemExit):
            validator.validate_pressure_gates(data, catalog, renderer, compute)

    def test_hlsl_blue_noise_rejects_static_array_fallback(self) -> None:
        data = {
            "hlslReadyBlueNoise4x4": {
                "normalizedThresholdMatrix": [[0.0, 0.5], [0.75, 0.25]],
                "hlslSnippet": (
                    "half HectonCoreLitBlueNoise4x4(uint index) { "
                    "static const half blueNoise4x4[16] = {0, 0.5, 0.75, 0.25}; "
                    "return blueNoise4x4[index]; }"
                ),
            }
        }
        hlsl = (
            "half HectonCoreLitBlueNoise4x4(uint index) { "
            "static const half blueNoise4x4[16] = {0, 0.5, 0.75, 0.25}; "
            "return blueNoise4x4[index]; }"
        )

        with self.assertRaises(SystemExit):
            validator.validate_hlsl_blue_noise(data, hlsl)

    @staticmethod
    def _tier(
        tier: str,
        particle_count: int,
        marine_snow: int,
        bubbles: int,
        debris: int,
        groups: int,
        step_distance: float,
        shadow_taps: int,
        flow_resample_frames: int,
    ) -> dict:
        return {
            "tier": tier,
            "particleCount": particle_count,
            "marineSnowCount": marine_snow,
            "bubbleCount": bubbles,
            "debrisCount": debris,
            "dispatchGroupsAt64Threads": groups,
            "stepDistanceMeters": step_distance,
            "shadowTaps": shadow_taps,
            "flowResampleFrames": flow_resample_frames,
        }

    @staticmethod
    def _tier_constants(
        prefix: str,
        particle_count: int,
        marine_snow: int,
        bubbles: int,
        debris: int,
        step_distance: float,
        shadow_taps: int,
        flow_resample_frames: int,
    ) -> str:
        return "\n".join(
            (
                f"public const int {prefix}ParticleCount = {particle_count};",
                f"public const int {prefix}MarineSnowCount = {marine_snow};",
                f"public const int {prefix}BubbleCount = {bubbles};",
                f"public const int {prefix}DebrisCount = {debris};",
                f"public const float {prefix}StepDistanceMeters = {step_distance:.2f}f;",
                f"public const int {prefix}ShadowTaps = {shadow_taps};",
                f"public const int {prefix}FlowResampleFrames = {flow_resample_frames};",
            )
        )


if __name__ == "__main__":
    unittest.main()
