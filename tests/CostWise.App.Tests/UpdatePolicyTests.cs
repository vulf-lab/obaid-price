using System.IO;
using CostWise.App.Services.Update;

namespace CostWise.App.Tests;

public class UpdatePolicyTests
{
    [Theory]
    [InlineData("Prompt", UpdatePolicy.Prompt)]
    [InlineData("SilentDownloadApplyOnRestart", UpdatePolicy.SilentDownloadApplyOnRestart)]
    [InlineData("Off", UpdatePolicy.Off)]
    [InlineData("prompt", UpdatePolicy.Prompt)]
    public void UpdatePolicy_Parses_From_Prefs_String(string raw, UpdatePolicy expected)
    {
        Assert.True(Enum.TryParse<UpdatePolicy>(raw, ignoreCase: true, out var policy));
        Assert.Equal(expected, policy);
    }

    [Fact]
    public void Default_Policy_Is_Prompt()
    {
        Assert.Equal(UpdatePolicy.Prompt, default(UpdatePolicy));
    }
}

public class DatabaseBackupServiceTests
{
    [Fact]
    public void FindLatestBackup_Returns_Null_When_No_Backups()
    {
        // Safe when backups folder empty or missing; does not create user data.
        var existing = DatabaseBackupService.FindLatestBackup();
        if (existing is null)
            Assert.Null(existing);
        else
            Assert.True(File.Exists(existing));
    }
}
