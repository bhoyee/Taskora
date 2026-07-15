using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TodoApp.Infrastructure.Persistence;

namespace TodoApp.Api.Operations;

public sealed class DatabaseBackupService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<DatabaseBackupService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public bool Enabled => ReadBool(
        configuration["Operations:Backups:Enabled"]);

    public int RetentionDays => ReadPositiveInt(
        configuration["Operations:Backups:RetentionDays"],
        7);

    public string BackupDirectory => ResolveBackupDirectory(
        configuration["Operations:Backups:Directory"],
        environment.ContentRootPath);

    public async Task<DatabaseBackupFile> CreateBackupAsync(
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BackupDirectory);
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TodoAppDbContext>();
        var snapshot = await BuildSnapshotAsync(database, cancellationToken);
        var fileName = $"taskora-db-backup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}Z.json";
        var path = Path.Combine(BackupDirectory, fileName);

        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(snapshot, JsonOptions),
            cancellationToken);
        await EnforceRetentionAsync(cancellationToken);

        var info = new FileInfo(path);
        logger.LogInformation(
            "Database backup {FileName} created with {TableCount} tables and {Size} bytes.",
            fileName,
            snapshot.Tables.Count,
            info.Length);

        return ToBackupFile(info);
    }

    public Task<IReadOnlyCollection<DatabaseBackupFile>> ListBackupsAsync(
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BackupDirectory);
        var files = Directory
            .EnumerateFiles(BackupDirectory, "taskora-db-backup-*.json")
            .Select(path => ToBackupFile(new FileInfo(path)))
            .OrderByDescending(file => file.CreatedAt)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<DatabaseBackupFile>>(files);
    }

    public FileInfo? GetBackupFile(string fileName)
    {
        if (fileName != Path.GetFileName(fileName))
        {
            return null;
        }

        var path = Path.Combine(BackupDirectory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return new FileInfo(path);
    }

    public async Task EnforceRetentionAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BackupDirectory);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);
        foreach (var file in Directory
                     .EnumerateFiles(BackupDirectory, "taskora-db-backup-*.json")
                     .Select(path => new FileInfo(path))
                     .Where(file => file.CreationTimeUtc < cutoff.UtcDateTime))
        {
            cancellationToken.ThrowIfCancellationRequested();
            file.Delete();
            logger.LogInformation(
                "Deleted expired database backup {FileName}.",
                file.Name);
        }
    }

    private static async Task<DatabaseSnapshot> BuildSnapshotAsync(
        TodoAppDbContext database,
        CancellationToken cancellationToken)
    {
        var tables = database.Model
            .GetEntityTypes()
            .Select(entity => new
            {
                Schema = entity.GetSchema(),
                Name = entity.GetTableName()
            })
            .Where(table => !string.IsNullOrWhiteSpace(table.Name))
            .Distinct()
            .OrderBy(table => table.Schema)
            .ThenBy(table => table.Name)
            .ToArray();
        var connection = database.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var providerName = database.Database.ProviderName ?? string.Empty;
            var snapshotTables = new List<DatabaseBackupTable>();
            foreach (var table in tables)
            {
                var sql = $"SELECT * FROM {FormatTableName(providerName, table.Schema, table.Name!)}";
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                snapshotTables.Add(await ReadTableAsync(
                    table.Schema,
                    table.Name!,
                    reader,
                    cancellationToken));
            }

            return new DatabaseSnapshot(
                "Taskora",
                DateTimeOffset.UtcNow,
                providerName,
                snapshotTables);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<DatabaseBackupTable> ReadTableAsync(
        string? schema,
        string tableName,
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var columns = Enumerable
            .Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var column in columns)
            {
                var value = reader[column];
                row[column] = value == DBNull.Value ? null : value;
            }

            rows.Add(row);
        }

        return new DatabaseBackupTable(schema, tableName, columns, rows);
    }

    private static string FormatTableName(
        string providerName,
        string? schema,
        string tableName)
    {
        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(schema)
                ? Quote(tableName)
                : $"{Quote(schema)}.{Quote(tableName)}";
        }

        return QuoteSqlite(tableName);
    }

    private static string Quote(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static string QuoteSqlite(string value) =>
        $"[{value.Replace("]", "]]")}]";

    private static DatabaseBackupFile ToBackupFile(FileInfo file) =>
        new(
            file.Name,
            file.Length,
            new DateTimeOffset(file.CreationTimeUtc, TimeSpan.Zero),
            new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero));

    private static string ResolveBackupDirectory(
        string? configuredPath,
        string contentRoot)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(contentRoot, "App_Data", "backups")
            : configuredPath.Trim();
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(contentRoot, path));
    }

    private static bool ReadBool(string? value) =>
        bool.TryParse(value, out var result) && result;

    private static int ReadPositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var result) && result > 0
            ? result
            : fallback;
}

public sealed record DatabaseSnapshot(
    string Application,
    DateTimeOffset CreatedAt,
    string ProviderName,
    IReadOnlyCollection<DatabaseBackupTable> Tables);

public sealed record DatabaseBackupTable(
    string? Schema,
    string Name,
    IReadOnlyCollection<string> Columns,
    IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Rows);

public sealed record DatabaseBackupFile(
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt);
