using Dapper;

namespace TimeClock.Api.Data;

public static class SchemaInitializer
{
    public static async Task ApplyAsync(IDbConnectionFactory db, IHostEnvironment env, ILogger logger)
    {
        var directory = ResolveSchemaDirectory(env);
        if (directory is null)
        {
            logger.LogWarning("Postgres schema directory was not found; skipping automatic schema apply.");
            return;
        }

        var files = Directory.GetFiles(directory, "*.sql").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0)
        {
            logger.LogWarning("No Postgres schema scripts were found in {Directory}.", directory);
            return;
        }

        using var conn = db.CreateConnection();
        conn.Open();
        foreach (var file in files)
        {
            var sql = await File.ReadAllTextAsync(file);
            await conn.ExecuteAsync(sql);
            logger.LogInformation("Applied Postgres schema from {Path}.", file);
        }
    }

    private static string? ResolveSchemaDirectory(IHostEnvironment env)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "database"),
            Path.Combine(env.ContentRootPath, "database"),
            Path.Combine(env.ContentRootPath, "..", "..", "database"),
            "/app/database",
        };

        return candidates.Select(Path.GetFullPath).FirstOrDefault(Directory.Exists);
    }
}
