using System;
using System.IO;
using System.Threading.Tasks;
using DbUp;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Tests;

public abstract class PostgreSqlIntegrationTestBase : IAsyncLifetime
{
    private readonly string _schemaName;
    private readonly string _baseConnectionString;
    protected string ConnectionString { get; private set; } = null!;
    protected DbContextOptions<AppDbContext> Options { get; private set; } = null!;

    protected PostgreSqlIntegrationTestBase()
    {
        _schemaName = $"test_{Guid.NewGuid():N}";
        _baseConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
    }

    public async Task InitializeAsync()
    {
        var csb = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            SearchPath = _schemaName
        };
        ConnectionString = csb.ConnectionString;

        // Create schema
        await using (var conn = new NpgsqlConnection(_baseConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{_schemaName}\";";
            await cmd.ExecuteNonQueryAsync();
        }

        // Run migrations inside schema
        var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
        if (Directory.Exists(migrationsPath))
        {
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(ConnectionString)
                .WithScriptsFromFileSystem(migrationsPath)
                .LogToNowhere()
                .Build();

            upgrader.PerformUpgrade();
        }

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
    }

    public async Task DisposeAsync()
    {
        // Drop schema
        try
        {
            await using var conn = new NpgsqlConnection(_baseConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{_schemaName}\" CASCADE;";
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }

    protected AppDbContext CreateDbContext() => new(Options);

    protected AppUser CreateSeedUser(AppDbContext? db = null)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash",
            IsActive = true
        };
        var ctx = db ?? CreateDbContext();
        ctx.Users.Add(user);
        ctx.SaveChanges();
        if (db == null) ctx.Dispose();
        return user;
    }
}
