using Npgsql;

namespace TodoApp.Infrastructure.Persistence;

internal static class ConnectionStringNormalizer
{
    public static string ForProvider(string provider, string connectionString)
    {
        var normalized = Clean(connectionString);
        if (!IsPostgres(provider) || !IsPostgresUrl(normalized))
        {
            return normalized;
        }

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

    public static bool IsPostgres(string provider) =>
        provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase);

    private static bool IsPostgresUrl(string connectionString) =>
        connectionString.StartsWith(
            "postgresql://",
            StringComparison.OrdinalIgnoreCase) ||
        connectionString.StartsWith(
            "postgres://",
            StringComparison.OrdinalIgnoreCase);

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
