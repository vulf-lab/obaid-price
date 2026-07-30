using System.IO;
using CostWise.App.Services;

namespace CostWise.App.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_Then_Verify_Succeeds()
    {
        var (salt, hash) = PasswordHasher.Hash("CorrectHorseBattery1");
        Assert.True(PasswordHasher.Verify("CorrectHorseBattery1", salt, hash));
        Assert.False(PasswordHasher.Verify("wrong", salt, hash));
    }
}

public class AuthAccountStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "CostWiseAuthTests", Guid.NewGuid().ToString("N"), "user-account.dat");

    [Fact]
    public void Missing_File_NeedsSetup_And_CompleteSetup_Works()
    {
        var path = TempPath();
        var store = new AuthAccountStore(path);
        Assert.Equal(AuthLoadState.NeedsSetup, store.LoadState);

        Assert.True(store.TryCompleteSetup("Password123", "Password123", out _));
        Assert.Equal(AuthLoadState.Ready, store.LoadState);
        Assert.True(File.Exists(path));

        var reloaded = new AuthAccountStore(path);
        Assert.Equal(AuthLoadState.Ready, reloaded.LoadState);
        Assert.True(reloaded.TryAuthenticatePassword("Password123", out _));
    }

    [Fact]
    public void Corrupt_File_Is_Damaged_Not_Reseeded()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x01, 0x02, 0x03, 0x04, 0x05]);

        var store = new AuthAccountStore(path);
        Assert.Equal(AuthLoadState.Damaged, store.LoadState);
        Assert.False(store.TryAuthenticatePassword("anything", out _));
    }

    [Fact]
    public void Lockout_After_Repeated_Failures()
    {
        var path = TempPath();
        var store = new AuthAccountStore(path);
        Assert.True(store.TryCompleteSetup("Password123", "Password123", out _));

        for (var i = 0; i < 5; i++)
            Assert.False(store.TryAuthenticatePassword("bad", out _));

        Assert.True(store.IsLockedOut);
        Assert.False(store.TryAuthenticatePassword("Password123", out var error));
        Assert.Contains("Try again", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pin_Requires_Password_And_Unlocks()
    {
        var path = TempPath();
        var store = new AuthAccountStore(path);
        Assert.True(store.TryCompleteSetup("Password123", "Password123", out _));
        Assert.False(store.TrySetPin("wrong", "1234", out _));
        Assert.True(store.TrySetPin("Password123", "1234", out _));
        Assert.True(store.HasPin);
        Assert.True(store.TryAuthenticatePin("1234", out _));
        Assert.True(store.TryClearPin("Password123", out _));
        Assert.False(store.HasPin);
    }
}
