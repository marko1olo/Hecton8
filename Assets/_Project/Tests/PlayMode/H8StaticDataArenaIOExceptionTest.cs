using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NUnit.Framework;
using Hecton8.Data;

namespace Hecton8.Tests.PlayMode
{
    public class H8StaticDataArenaIOExceptionTest
    {
        [Test]
        public async Task TryStageStreamingAssetsUriToCacheAsync_WhenDirectoryCreateThrowsIOException_ReturnsNull()
        {
            string cacheDirectory = Path.Combine(Application.temporaryCachePath, "Hecton8", "DataMonolith");
            string cacheParent = Path.GetDirectoryName(cacheDirectory);
            if (!Directory.Exists(cacheParent))
                Directory.CreateDirectory(cacheParent);

            if (Directory.Exists(cacheDirectory))
                Directory.Delete(cacheDirectory, true);

            // Create a file where the directory should be to force IOException during CreateDirectory
            File.WriteAllText(cacheDirectory, "blocking file");

            try
            {
                // Call private method
                MethodInfo method = typeof(H8StaticDataArena).GetMethod("TryStageStreamingAssetsUriToCacheAsync", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(method, "Could not find TryStageStreamingAssetsUriToCacheAsync method.");

                string uri = "http://localhost/test.bin";
                CancellationToken token = CancellationToken.None;

                // Cast to Awaitable<string> and natively await
                string result = await (Awaitable<string>)method.Invoke(null, new object[] { uri, token });

                Assert.IsNull(result, "Expected null result when IOException is thrown.");
            }
            finally
            {
                if (File.Exists(cacheDirectory))
                    File.Delete(cacheDirectory);
            }
        }
    }
}
