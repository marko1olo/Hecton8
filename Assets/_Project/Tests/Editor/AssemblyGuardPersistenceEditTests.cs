using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AssemblyGuardPersistenceEditTests
    {
        [Test]
        public void CompileWallGeneratedArtifactsPromoteAtomically()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("PromoteGeneratedArtifact(tempPath, absolutePath);", source);
            StringAssert.Contains("File.Replace(tempPath, absolutePath, null, true);", source);
            StringAssert.Contains("File.Move(tempPath, absolutePath);", source);
            StringAssert.DoesNotContain("File.Copy(tempPath, absolutePath, true);", source);
        }
    }
}
