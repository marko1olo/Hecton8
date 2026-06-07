using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class VoxelTerrainSeamBinderPersistenceEditTests
    {
        [Test]
        public void SeamBinderAtomicWritersAvoidDeleteMoveGap()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scanner = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/Dynamic_Vertex_Scanner.cs"));
            string pipeline = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderPipeline.cs"));

            StringAssert.Contains("File.Replace(temp, path, null, true);", scanner);
            StringAssert.Contains("File.Replace(temp, fullPath, null, true);", pipeline);
            StringAssert.DoesNotContain("File.Delete(path);", scanner);
            StringAssert.DoesNotContain("File.Delete(fullPath);", pipeline);
        }
    }
}
