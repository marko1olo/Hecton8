using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class MapMagicWaterlineFallbackEditTests
    {
        [Test]
        public void MapMagicRuntimeBridge_SanitizesWaterSurfaceBeforeTerrainConsumersReadIt()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "MapMagic",
                "MapMagicRuntimeBridge.cs");

            Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceLevel = 14.02f;"));
            Assert.That(source, Does.Contain("private float waterSurfaceLevel = DefaultWaterSurfaceLevel;"));
            Assert.That(source, Does.Contain("public override float WaterSurfaceLevel => SanitizeWaterSurfaceLevel(waterSurfaceLevel);"));
            Assert.That(source, Does.Contain("waterSurfaceLevel = SanitizeWaterSurfaceLevel(y);"));
            Assert.That(source, Does.Contain("return y < WaterSurfaceLevel;"));
            Assert.That(source, Does.Contain("return y < WaterSurfaceLevel && y > bottomHeight;"));
            Assert.That(source, Does.Contain("private static float SanitizeWaterSurfaceLevel(float y)"));
            Assert.That(source, Does.Not.Contain("private float waterSurfaceLevel = 0f;"));
            Assert.That(source, Does.Not.Contain("return y < waterSurfaceLevel;"));
            Assert.That(source, Does.Not.Contain("return y < waterSurfaceLevel && y > bottomHeight;"));
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(parts);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(projectRoot, path));
        }
    }
}
