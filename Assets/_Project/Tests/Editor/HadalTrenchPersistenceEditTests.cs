using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HadalTrenchPersistenceEditTests
    {
        [Test]
        public void InvalidPayloadPreservationAvoidsDeleteMoveGap()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("File.Replace(tempPath, invalidPath, null, true);", source);
            StringAssert.Contains("File.Move(tempPath, invalidPath);", source);
            StringAssert.DoesNotContain("File.Delete(invalidPath);", source);
        }
    }
}
