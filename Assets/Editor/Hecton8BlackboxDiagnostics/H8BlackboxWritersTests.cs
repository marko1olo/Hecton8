using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Hecton8.BlackboxDiagnostics;

public class H8BlackboxWritersTests
{
    [Test]
    public void WriteRunSummary_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write run summary:.*"));
        H8Writers.WriteRunSummary(null, null); // Will throw ArgumentNullException in Path.Combine
    }

    [Test]
    public void WriteSnapshot_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write snapshot:.*"));
        H8Writers.WriteSnapshot(null, null, "test.json");
    }

    [Test]
    public void WriteFindings_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write findings:.*"));
        H8Writers.WriteFindings(null, new List<H8Finding>());
    }

    [Test]
    public void WriteReport_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write report:.*"));
        H8Writers.WriteReport(null, null, new List<H8Finding>());
    }

    [Test]
    public void WriteCompactHandoff_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write compact handoff:.*"));
        H8Writers.WriteCompactHandoff(null, null, new List<H8Finding>());
    }

    [Test]
    public void WriteNextSteps_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write next steps:.*"));
        H8Writers.WriteNextSteps(null, new List<H8Finding>());
    }

    [Test]
    public void WriteHierarchy_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write hierarchy:.*"));
        H8Writers.WriteHierarchy(null, new List<H8KeyObjectInfo>());
    }

    [Test]
    public void WriteConsoleLogs_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write console logs:.*"));
        H8Writers.WriteConsoleLogs(null, null);
    }

    [Test]
    public void WritePlayModeDiff_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write diff:.*"));
        H8Writers.WritePlayModeDiff(null, null, null, "test");
    }

    [Test]
    public void WriteDirectVsBootstrapDiff_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write direct vs bootstrap diff:.*"));
        H8Writers.WriteDirectVsBootstrapDiff(null, null, null);
    }

    [Test]
    public void WriteFullComparisonHandoff_ThrowsException_LogsWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[H8Blackbox\] Failed to write full comparison handoff:.*"));
        H8Writers.WriteFullComparisonHandoff(null, null, null, new List<H8Finding>(), new List<H8Finding>());
    }
}
