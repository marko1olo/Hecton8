using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class WorldWaterLevelProofToolEditTests
    {
        [Test]
        public void BuildWorldWaterLevelProofStrip_UsesMacroSamplerAndDoesNotMutateTerrain()
        {
            string source = ReadProjectFile("Tools", "BuildWorldWaterLevelProofStrip.py");

            StringAssert.Contains("from BuildWorldMacroGeologyPreview import", source);
            StringAssert.Contains("evaluate_height", source);
            StringAssert.Contains("write_png_rgb", source);
            StringAssert.Contains("WorldWaterLevelCalibration", source);
            StringAssert.Contains("\"terrainMutation\": False", source);
            StringAssert.Contains("Output prefix water-level token", source);
            StringAssert.Contains("strictCandidateLevels", source);
            StringAssert.Contains("bestLevels", source);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", source);
            StringAssert.DoesNotContain("TerrainData", source);
        }

        [Test]
        public void GeneratedProof_ContainsSixGameplayDepthPointsAndRasterPreview()
        {
            AssertGeneratedProof("WorldWaterLevelProof_Seed880031_WaterY560", 560.0);
            AssertGeneratedProof("WorldWaterLevelProof_Seed880031_WaterY0", 0.0);
            AssertGeneratedProof("WorldWaterLevelProof_Seed880031_WaterY-100", -100.0);
        }

        private static void AssertGeneratedProof(string artifactName, double waterLevelY)
        {
            string jsonPath = ProjectPath("Docs", "GeneratedAssets", "Water", artifactName + ".json");
            string svgPath = ProjectPath("Docs", "GeneratedAssets", "Water", artifactName + ".svg");
            string pngPath = ProjectPath("Docs", "GeneratedAssets", "Water", artifactName + ".png");

            Assert.That(File.Exists(jsonPath), Is.True, jsonPath);
            Assert.That(File.Exists(svgPath), Is.True, svgPath);
            Assert.That(File.Exists(pngPath), Is.True, pngPath);
            Assert.That(new FileInfo(pngPath).Length, Is.GreaterThan(4096));

            string json = File.ReadAllText(jsonPath);
            StringAssert.Contains("\"terrainMutation\": false", json);
            StringAssert.Contains("\"waterLevelY\": " + waterLevelY.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), json);
            StringAssert.Contains("\"proofPng\": \"Docs/GeneratedAssets/Water/" + artifactName + ".png\"", json);
            Assert.That(Regex.Matches(json, "\"key\": \"").Count, Is.EqualTo(6));
            AssertProofDepth(json, "shallow_shore", 5.0, 6.0);
            AssertProofDepth(json, "underwater_slope", 25.0, 6.0);
            AssertProofDepth(json, "playable_50m", 50.0, 6.0);
            AssertProofDepth(json, "transition_100m", 100.0, 6.0);
            AssertProofDepth(json, "upper_saturated_500m", 500.0, 20.0);
            AssertProofDepth(json, "deep_transition", 900.0, 36.0);
        }

        private static void AssertProofDepth(string json, string key, double expectedDepth, double tolerance)
        {
            Match match = Regex.Match(
                json,
                "\\{[^{}]*\"key\": \"" + Regex.Escape(key) + "\"[^{}]*\"sampledDepthMeters\": ([0-9.]+)",
                RegexOptions.Singleline);
            Assert.That(match.Success, Is.True, key);
            double actualDepth = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(Math.Abs(actualDepth - expectedDepth), Is.LessThanOrEqualTo(tolerance), key);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            return File.ReadAllText(ProjectPath(parts));
        }

        private static string ProjectPath(params string[] parts)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
        }
    }
}
