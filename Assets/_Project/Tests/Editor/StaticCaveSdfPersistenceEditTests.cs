using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class StaticCaveSdfPersistenceEditTests
    {
        [Test]
        public void BinaryWriterReplacesExistingOutputWithoutActiveGap()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("File.Replace(tempPath, fullPath, backupPath, true);", source);
            StringAssert.Contains("File.Move(tempPath, fullPath);", source);
            StringAssert.Contains("new FileStream(tempPath, FileMode.CreateNew", source);
            StringAssert.DoesNotContain("File.Move(fullPath, backupPath);", source);
            StringAssert.DoesNotContain("File.Move(backupPath, fullPath);", source);
            StringAssert.DoesNotContain("new FileStream(tempPath, FileMode.Create, FileAccess.Write", source);
        }
    }
}
