using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModBuilderPersistenceEditTests
    {
        [Test]
        public void OutputFilesCopyThroughAtomicTempPromotion()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("CopyFileAtomic(bundleOutputPath, finalBundlePath);", source);
            StringAssert.Contains("CopyFileAtomic(sourcePath, destinationPath);", source);
            StringAssert.Contains("File.Copy(sourcePath, tempPath, false);", source);
            StringAssert.Contains("File.Replace(tempPath, destinationPath, null, true);", source);
            StringAssert.DoesNotContain("File.Copy(bundleOutputPath, finalBundlePath, true);", source);
            StringAssert.DoesNotContain("File.Copy(sourcePath, destinationPath, true);", source);
        }

        [Test]
        public void ManifestWritesThroughAtomicTempPromotion()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("WriteTextAtomic(manifestPath, json);", source);
            StringAssert.Contains("File.WriteAllText(tempPath, text);", source);
            StringAssert.Contains("File.Replace(tempPath, destinationPath, null, true);", source);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", source);
            StringAssert.DoesNotContain("File.WriteAllText(manifestPath, json);", source);
        }
    }
}
