using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.Data;
using System.Reflection;

[TestFixture]
public class CandiceSaveSystemTests
{
    private const string LegacyFileSerializationDisabledMessage = "Candice legacy file serialization is disabled. Vendor file saves are quarantined; use the first-party save authority.";

    [SetUp]
    public void SetUp()
    {
        var field = typeof(CandiceSaveSystem).GetField("s_loggedLegacyFileSerializationDisabled", BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, false);
        }
    }

    [Test]
    public void SaveToFile_LogsWarning_WhenCalled()
    {
        var saveSystem = new CandiceSaveSystem();

        LogAssert.Expect(LogType.Warning, LegacyFileSerializationDisabledMessage);

        saveSystem.SaveToFile(new object(), "test_file.bin");
    }

    [Test]
    public void SaveToFile_DoesNotLogWarning_WhenCalledMultipleTimes()
    {
        var saveSystem = new CandiceSaveSystem();

        LogAssert.Expect(LogType.Warning, LegacyFileSerializationDisabledMessage);
        saveSystem.SaveToFile(new object(), "test_file.bin");

        saveSystem.SaveToFile(new object(), "test_file.bin");
    }
}
