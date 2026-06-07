using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class Shinobu38FileWriterLifecycleEditTests
    {
        [Test]
        public void FileWriterLifecycleUsesNoThrowStartStopAndPreservesStateWhenJoinFails()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs"));
            string startBody = ExtractMethodBody(source, "private void StartFileWriter()");
            string stopBody = ExtractMethodBody(source, "private void StopFileWriter(bool flushPending)");
            string signalBody = ExtractMethodBody(source, "private static bool SignalFileWriterNoThrow(ManualResetEventSlim writerEvent)");
            string joinBody = ExtractMethodBody(source, "private static bool TryJoinFileWriterNoThrow(Thread writer, int timeoutMilliseconds)");
            string interruptBody = ExtractMethodBody(source, "private static void InterruptFileWriterNoThrow(Thread writer)");
            string disposeBody = ExtractMethodBody(source, "private static void DisposeFileWriterEventNoThrow(ManualResetEventSlim writerEvent)");

            StringAssert.Contains("private const int FileWriterJoinMilliseconds = 2000;", source);
            StringAssert.Contains("private const int FileWriterInterruptJoinMilliseconds = 500;", source);

            StringAssert.Contains("Thread existingWriter = _fileWriterThread;", startBody);
            StringAssert.Contains("if (existingWriter.IsAlive)", startBody);
            StringAssert.Contains("_fileWriterThread = null;", startBody);
            StringAssert.Contains("DisposeFileWriterEventNoThrow(_fileWriterEvent);", startBody);
            StringAssert.Contains("try", startBody);
            StringAssert.Contains("Thread writer = new Thread(FileWriterLoop)", startBody);
            StringAssert.Contains("_fileWriterThread = writer;", startBody);
            StringAssert.Contains("writer.Start();", startBody);
            StringAssert.Contains("catch (Exception)", startBody);
            StringAssert.Contains("Volatile.Write(ref _fileWriterStopRequested, 1);", startBody);
            StringAssert.Contains("Volatile.Write(ref cursor.Running, 0);", startBody);
            StringAssert.Contains("DisposeFileWriterEventNoThrow(_fileWriterEvent);", startBody);

            StringAssert.Contains("SignalFileWriterNoThrow(_fileWriterEvent);", stopBody);
            StringAssert.Contains("TryJoinFileWriterNoThrow(writer, FileWriterJoinMilliseconds)", stopBody);
            StringAssert.Contains("InterruptFileWriterNoThrow(writer);", stopBody);
            StringAssert.Contains("TryJoinFileWriterNoThrow(writer, FileWriterInterruptJoinMilliseconds)", stopBody);
            StringAssert.Contains("if (!writerStopped)", stopBody);
            StringAssert.Contains("return;", stopBody);
            StringAssert.Contains("DisposeFileWriterEventNoThrow(_fileWriterEvent);", stopBody);

            StringAssert.Contains("writerEvent.Set();", signalBody);
            StringAssert.Contains("catch (Exception)", signalBody);

            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, writer)", joinBody);
            StringAssert.Contains("writer.Join(timeoutMilliseconds);", joinBody);
            StringAssert.Contains("return !writer.IsAlive;", joinBody);
            StringAssert.Contains("catch (Exception)", joinBody);

            StringAssert.Contains("writer.Interrupt();", interruptBody);
            StringAssert.Contains("catch (Exception)", interruptBody);
            StringAssert.Contains("writerEvent.Dispose();", disposeBody);
            StringAssert.Contains("catch (Exception)", disposeBody);

            Assert.AreEqual(0, CountToken(source, "_fileWriterEvent?.Set();"));
            Assert.AreEqual(0, CountToken(source, "_fileWriterEvent?.Dispose();"));
            Assert.AreEqual(0, CountToken(source, "writer.Join(2000)"));
            Assert.AreEqual(0, CountToken(source, "writer.Join(500)"));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }
        }
    }
}
