using Hecton8.BuildTools;
using NUnit.Framework;

public class BuildPlaytestEntryTests
{
    [SetUp]
    public void SetUp()
    {
        BuildPlaytestLog.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        BuildPlaytestLog.Clear();
    }

    [Test]
    public void Create_NormalizesFields_AndWritesTimestamp()
    {
        BuildPlaytestEntry entry = BuildPlaytestEntry.Create(
            version: null,
            fpsMean: 58.25f,
            fpsWorst: 31.5f,
            mainIrritant: null,
            mainVisualFlaw: "  gas giant reads flat  ",
            mainUXFlaw: string.Empty,
            mainContentGap: "   ",
            isBlocker: true,
            notes: null);

        Assert.That(entry.Version, Is.EqualTo("unknown"));
        Assert.That(entry.MainIrritant, Is.EqualTo(string.Empty));
        Assert.That(entry.MainVisualFlaw, Is.EqualTo("gas giant reads flat"));
        Assert.That(entry.MainUXFlaw, Is.EqualTo(string.Empty));
        Assert.That(entry.MainContentGap, Is.EqualTo(string.Empty));
        Assert.That(entry.Notes, Is.EqualTo(string.Empty));
        Assert.That(entry.HasRecordedTimestamp, Is.True);
        Assert.That(entry.CreatedTimestamp, Is.GreaterThan(0));
        Assert.That(entry.ToString(), Does.Contain("unknown"));
    }

    [Test]
    public void Log_Record_Clear_AndExportMarkdown_WorkAsMinimalBuildScaffold()
    {
        BuildPlaytestEntry entry = BuildPlaytestEntry.Create(
            version: "2026-04-13-test-001",
            fpsMean: 60f,
            fpsWorst: 42f,
            mainIrritant: "surface hitch",
            mainVisualFlaw: "flat sky banding",
            mainUXFlaw: "load button hidden",
            mainContentGap: "missing QA pass",
            isBlocker: false,
            notes: "editor test");

        BuildPlaytestLog.RecordEntry(entry);

        Assert.That(BuildPlaytestLog.GetAllEntries().Count, Is.EqualTo(1));

        string markdown = BuildPlaytestLog.ExportToMarkdown();
        Assert.That(markdown, Does.Contain("# Build Playtest Log"));
        Assert.That(markdown, Does.Contain("2026-04-13-test-001"));
        Assert.That(markdown, Does.Contain("surface hitch"));
        Assert.That(markdown, Does.Contain("editor test"));

        BuildPlaytestLog.Clear();
        Assert.That(BuildPlaytestLog.GetAllEntries().Count, Is.EqualTo(0));
    }
}
