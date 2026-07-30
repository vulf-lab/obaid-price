using System.Security.Cryptography;
using System.Text;

namespace CostWise.App.Services;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static (string SaltBase64, string HashBase64) Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Pbkdf2(secret, salt);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public static bool Verify(string secret, string saltBase64, string hashBase64)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(saltBase64) || string.IsNullOrEmpty(hashBase64))
            return false;

        try
        {
            var salt = Convert.FromBase64String(saltBase64);
            var expected = Convert.FromBase64String(hashBase64);
            var actual = Pbkdf2(secret, salt);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Pbkdf2(string secret, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
}
