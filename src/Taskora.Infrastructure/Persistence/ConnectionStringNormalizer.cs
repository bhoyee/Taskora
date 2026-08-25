using Npgsql;

namespace TodoApp.Infrastructure.Persistence;

/// <summary>
/// Reconciles the connection string formats accepted across the app's
/// deployment targets. Local/dev SQLite strings and classic ADO.NET-style
/// Postgres strings are passed through unchanged, while Postgres URLs
/// (the "postgres://user:pass@host:port/db?..." form commonly supplied by
/// hosting providers such as Render/Heroku/Azure) are parsed and rewritten
/// into the key/value format Npgsql expects.
/// </summary>
internal static class ConnectionStringNormalizer
{
    /// <summary>
    /// Cleans and, when the provider is Postgres and the string is a URL,
    /// converts <paramref name="connectionString"/> into an Npgsql-compatible
    /// connection string. Non-Postgres providers and non-URL strings are
    /// returned as-is (after cleanup).
    /// </summary>
    public static string ForProvider(string provider, string connectionString)
    {
        var normalized = Clean(connectionString);
        if (!IsPostgres(provider) || !IsPostgresUrl(normalized))
        {
            return normalized;
        }

        // Parse the postgres://user:pass@host:port/db?params URL form into
        // its components so they can be re-assembled via NpgsqlConnectionStringBuilder.
        var uri = new Uri(normalized);
        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            SslMode = SslMode.Require
        };

        if (credentials.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(credentials[1]);
        }

        foreach (var pair in ParseQuery(uri.Query))
        {
            if (pair.Key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = pair.Value.Equals(
                    "require",
                    StringComparison.OrdinalIgnoreCase)
                    ? SslMode.Require
                    : builder.SslMode;
            }
            else if (pair.Key.Equals(
                         "channel_binding",
                         StringComparison.OrdinalIgnoreCase))
            {
                builder.ChannelBinding = pair.Value.Equals(
                    "require",
                    StringComparison.OrdinalIgnoreCase)
                    ? ChannelBinding.Require
                    : ChannelBinding.Prefer;
            }
        }

        return builder.ConnectionString;
    }

    /// <summary>True when the configured provider name refers to Postgres/Npgsql (case-insensitive, a few accepted spellings).</summary>
    public static bool IsPostgres(string provider) =>
        provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase);

    // Detects the "postgres://" / "postgresql://" URL scheme as opposed to a
    // classic ADO.NET "Key=Value;..." connection string.
    private static bool IsPostgresUrl(string connectionString) =>
        connectionString.StartsWith(
            "postgresql://",
            StringComparison.OrdinalIgnoreCase) ||
        connectionString.StartsWith(
            "postgres://",
            StringComparison.OrdinalIgnoreCase);

    // Some hosting environments inject the connection string with the
    // "ConnectionStrings__TodoApp=" env-var prefix still attached, and/or
    // wrapped in stray quotes. Strip both before parsing further.
    private static string Clean(string connectionString)
    {
        var cleaned = connectionString.Trim().Trim('"', '\'');
        const string environmentKey = "ConnectionStrings__TodoApp=";
        if (cleaned.StartsWith(
                environmentKey,
                StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[environmentKey.Length..].Trim().Trim('"', '\'');
        }

        return cleaned;
    }

    // Splits a URL query string ("?a=1&b=2") into key/value pairs, decoding
    // percent-escaped characters.
    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(
        string query)
    {
        foreach (var segment in query.TrimStart('?').Split(
                     '&',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]);
            var value = pair.Length > 1
                ? Uri.UnescapeDataString(pair[1])
                : string.Empty;
            yield return new KeyValuePair<string, string>(key, value);
        }
    }
}
