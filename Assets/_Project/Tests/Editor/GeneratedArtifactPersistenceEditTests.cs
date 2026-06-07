using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class GeneratedArtifactPersistenceEditTests
    {
        [Test]
        public void BridgeContractGeneratorWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Core/Bridge/Editor/H8BridgeContractGenerator.cs");
            string writer = ExtractMethodBody(source, "private static void WriteTextAtomic(");

            StringAssert.Contains("WriteTextAtomic(fullPath, builder.ToString(), new UTF8Encoding(false));", source);
            StringAssert.Contains("File.WriteAllText(tempPath, text, encoding);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllText(fullPath, builder.ToString(), new UTF8Encoding(false));", source);
        }

        [Test]
        public void LocKeysGeneratorWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/LocKeysGenerator.cs");
            string writer = ExtractMethodBody(source, "private static void WriteTextAtomic(");

            StringAssert.Contains("WriteTextAtomic(outputPath, builder.ToString(), Encoding.UTF8);", source);
            StringAssert.Contains("File.WriteAllText(tempPath, text, encoding);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);", source);
        }

        [Test]
        public void BabelOverrideCopyWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/UI/Editor/BabelLocalizationManagerWindow.cs");
            string writer = ExtractMethodBody(source, "private static void WriteBytesAtomic(");

            StringAssert.Contains("WriteBytesAtomic(_savePath, output);", source);
            StringAssert.Contains("File.WriteAllBytes(tempPath, bytes);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllBytes(_savePath, output);", source);
        }

        [Test]
        public void BaseModuleCatalogBinaryWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/BaseModuleCatalogEditorTools.cs");
            string writeBinary = ExtractMethodBody(source, "private static unsafe void WriteBinary(");
            string writer = ExtractMethodBody(source, "private static void WriteBytesAtomic(");

            StringAssert.Contains("WriteBytesAtomic(fullPath, bytes);", writeBinary);
            StringAssert.Contains("File.WriteAllBytes(tempPath, bytes);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllBytes(fullPath, bytes);", writeBinary);
        }

        [Test]
        public void PreInitAssetGuidMapWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/PreInitAssetIdMapGenerator.cs");
            string writer = ExtractMethodBody(source, "private static void WriteTextAtomic(");

            StringAssert.Contains("WriteTextAtomic(absolutePath, builder.ToString(), new UTF8Encoding(false));", source);
            StringAssert.Contains("File.WriteAllText(tempPath, text, encoding);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllText(absolutePath, builder.ToString(), new UTF8Encoding(false));", source);
        }

        [Test]
        public void HectonBuildPipelineWritesBuildArtifactsThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/HectonBuildPipeline.cs");
            string writer = ExtractMethodBody(source, "private static void WriteTextAtomic(");

            StringAssert.Contains("WriteTextAtomic(\n            absolutePath,", source);
            StringAssert.Contains("WriteTextAtomic(resultPath, builder.ToString(), Encoding.UTF8);", source);
            StringAssert.Contains("File.WriteAllText(tempPath, text, encoding);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllText(\n            absolutePath,", source);
            StringAssert.DoesNotContain("File.WriteAllText(resultPath, builder.ToString(), Encoding.UTF8);", source);
        }

        [Test]
        public void HectonArtOptimizationPngsWriteThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/HectonArtOptimizationTools.cs");
            string writer = ExtractMethodBody(source, "private static void WriteBytesAtomic(");

            StringAssert.Contains("WriteBytesAtomic(writePath, readable.EncodeToPNG());", source);
            StringAssert.Contains("WriteBytesAtomic(atlasPath, atlas.EncodeToPNG());", source);
            StringAssert.Contains("File.WriteAllBytes(tempPath, bytes);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllBytes(writePath, readable.EncodeToPNG());", source);
            StringAssert.DoesNotContain("File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());", source);
        }

        [Test]
        public void FloraAtlasPngsWriteThroughAtomicTempPromotion()
        {
            string floraSource = ReadProjectSource("_Project/Scripts/Editor/WorldProceduralFloraTextureAuthoring.cs");
            string shallowsSource = ReadProjectSource("_Project/Scripts/Editor/ProceduralGen/ShallowsBioForgeBatchBaker.cs");
            string floraWriter = ExtractMethodBody(floraSource, "private static void WriteBytesAtomic(");
            string shallowsWriter = ExtractMethodBody(shallowsSource, "private static void WriteBytesAtomic(");

            StringAssert.Contains("WriteBytesAtomic(SeaGrassAtlasAssetPath, atlas.EncodeToPNG());", floraSource);
            StringAssert.Contains("WriteBytesAtomic(path, texture.EncodeToPNG());", shallowsSource);
            StringAssert.Contains("File.WriteAllBytes(tempPath, bytes);", floraWriter);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", floraWriter);
            StringAssert.Contains("File.Move(tempPath, path);", floraWriter);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", floraWriter);
            StringAssert.Contains("File.WriteAllBytes(tempPath, bytes);", shallowsWriter);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", shallowsWriter);
            StringAssert.Contains("File.Move(tempPath, path);", shallowsWriter);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", shallowsWriter);
            StringAssert.DoesNotContain("File.WriteAllBytes(SeaGrassAtlasAssetPath, atlas.EncodeToPNG());", floraSource);
            StringAssert.DoesNotContain("File.WriteAllBytes(path, texture.EncodeToPNG());", shallowsSource);
        }

        [Test]
        public void HlodImpostorBakeArtifactsWriteThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs");
            string nativeWriter = ExtractMethodBody(source, "private static void WriteNativeBytes(");
            string textWriter = ExtractMethodBody(source, "private static void WriteTextAtomic(");
            string promote = ExtractMethodBody(source, "private static void PromoteTempFileAtomic(");

            StringAssert.Contains("new FileStream(tempPath, FileMode.CreateNew", nativeWriter);
            StringAssert.Contains("PromoteTempFileAtomic(tempPath, fullPath);", nativeWriter);
            StringAssert.Contains("WriteTextAtomic(fullPath, report);", source);
            StringAssert.Contains("File.WriteAllText(tempPath, text);", textWriter);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", nativeWriter);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", textWriter);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", promote);
            StringAssert.Contains("File.Move(tempPath, path);", promote);
            StringAssert.DoesNotContain("new FileStream(fullPath, FileMode.Create", nativeWriter);
            StringAssert.DoesNotContain("File.WriteAllText(fullPath, report);", source);
        }

        [Test]
        public void AITextureControlMapPngWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs");
            string writePngAsync = ExtractMethodBody(source, "private static void WritePngAsync(");
            string promote = ExtractMethodBody(source, "private static void PromoteTempFileAtomic(");

            StringAssert.Contains("string tempPath = context.OutputPath + \".tmp\";", writePngAsync);
            StringAssert.Contains("new FileStream(tempPath, FileMode.CreateNew", writePngAsync);
            StringAssert.Contains("PromoteTempFileAtomic(tempPath, context.OutputPath);", writePngAsync);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writePngAsync);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", promote);
            StringAssert.Contains("File.Move(tempPath, path);", promote);
            StringAssert.DoesNotContain("new FileStream(context.OutputPath, FileMode.Create", writePngAsync);
        }

        [Test]
        public void FloraTemplateThumbnailsWriteThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/FloraThumbnailGenerator.cs");
            string writer = ExtractMethodBody(source, "private static void WritePngBytes(");
            string promote = ExtractMethodBody(source, "private static void PromoteTempFileAtomic(");

            StringAssert.Contains("string tempPath = thumbnailPath + \".tmp\";", writer);
            StringAssert.Contains("new FileStream(tempPath, FileMode.CreateNew", writer);
            StringAssert.Contains("PromoteTempFileAtomic(tempPath, thumbnailPath);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", promote);
            StringAssert.Contains("File.Move(tempPath, path);", promote);
            StringAssert.DoesNotContain("new FileStream(thumbnailPath, FileMode.Create", writer);
        }

        [Test]
        public void SdfFontAtlasAssetsWriteThroughDurableAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/SdfFontAtlasBaker.cs");
            string writer = ExtractMethodBody(source, "private static bool TryWriteBytesAtomicAsset(");

            StringAssert.Contains("new FileStream(tempPath, FileMode.CreateNew", writer);
            StringAssert.Contains("stream.Flush(true);", writer);
            StringAssert.Contains("File.Replace(tempPath, absolutePath, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, absolutePath);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllBytes(tempPath, bytes);", writer);
        }

        [Test]
        public void UIAudioPlaceholdersWriteWavsThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/UI/Editor/UIAudioPlaceholderGenerator.cs");
            string save = ExtractMethodBody(source, "public static void Save(");
            string createEmpty = ExtractMethodBody(source, "private static FileStream CreateEmpty(");
            string promote = ExtractMethodBody(source, "private static void PromoteTempFileAtomic(");

            StringAssert.Contains("string tempPath = filepath + \".tmp\";", save);
            StringAssert.Contains("CreateEmpty(tempPath)", save);
            StringAssert.Contains("fileStream.Flush(true);", save);
            StringAssert.Contains("PromoteTempFileAtomic(tempPath, filepath);", save);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", save);
            StringAssert.Contains("new FileStream(filepath, FileMode.CreateNew", createEmpty);
            StringAssert.Contains("File.Replace(tempPath, filepath, null, true);", promote);
            StringAssert.Contains("File.Move(tempPath, filepath);", promote);
            StringAssert.DoesNotContain("new FileStream(filepath, FileMode.Create)", createEmpty);
        }

        private static string ReadProjectSource(string assetRelativePath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, assetRelativePath)).Replace("\r\n", "\n");
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Missing method signature: " + signature);
            int brace = source.IndexOf('{', start);
            Assert.That(brace, Is.GreaterThanOrEqualTo(0), "Missing method brace: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }
    }
}
