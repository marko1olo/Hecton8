using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonCelestialEngineAuthorityTests
    {
        private const string SourcePath = "Assets/_Project/Scripts/HectonCelestialEngine.cs";

        [Test]
        public void DataVaultHotSwap_RebindsCelestialTruthVaultThroughTheAuthorityCallback()
        {
            string source = File.ReadAllText(SourcePath);
            string callback = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("case GlobalRegistryServiceSlot.DataVault:", callback);
            StringAssert.Contains("CacheCelestialTruthVault(currentService as IDataVault);", callback);
        }

        private static string ExtractMethodBlock(string source, string methodSignature)
        {
            int start = source.IndexOf(methodSignature, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing method: {methodSignature}");

            int braceStart = source.IndexOf('{' , start);
            Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), $"Missing opening brace for: {methodSignature}");

            int depth = 0;
            for (int index = braceStart; index < source.Length; index++)
            {
                if (source[index] == '{')
                    depth++;
                else if (source[index] == '}')
                    depth--;

                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }

            Assert.Fail($"Unterminated method: {methodSignature}");
            return string.Empty;
        }
    }
}
