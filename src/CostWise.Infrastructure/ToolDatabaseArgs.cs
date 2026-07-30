namespace CostWise.Infrastructure;

/// <summary>CLI helpers so tools do not silently mutate the live LocalAppData database.</summary>
public static class ToolDatabaseArgs
{
    public static bool TryResolve(
        string[] args,
        out string dbPath,
        out string error)
    {
        dbPath = string.Empty;
        error = string.Empty;

        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            error = HelpText;
            return false;
        }

        var dbArg = FindValue(args, "--db");
        var forceLocal = args.Any(a => string.Equals(a, "--force-local", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(dbArg))
        {
            dbPath = Path.GetFullPath(dbArg);
            return true;
        }

        if (forceLocal)
        {
            dbPath = DependencyInjection.GetDefaultDatabasePath();
            return true;
        }

        error =
            "Refusing to open the live LocalAppData database without an explicit path.\n" +
            "Pass --db <path> or --force-local to use %LocalAppData%\\CostWise\\costwise.db.\n\n" +
            HelpText;
        return false;
    }

    public static string HelpText =>
        """
        Database options:
          --db <path>       SQLite database file to open
          --force-local     Use %LocalAppData%\CostWise\costwise.db (explicit opt-in)
          -h | --help       Show help
        """;

    private static string? FindValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}
