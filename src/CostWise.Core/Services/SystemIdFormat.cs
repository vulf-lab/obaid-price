namespace CostWise.Core.Services;

public static class SystemIdFormat
{
    public const string Prefix = "CW-";

    public static string FromNumber(int number) => $"{Prefix}{number:D6}";

    public static bool TryParseNumber(string systemId, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(systemId) || !systemId.StartsWith(Prefix, StringComparison.Ordinal))
            return false;
        return int.TryParse(systemId.AsSpan(Prefix.Length), out number);
    }
}
