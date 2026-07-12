using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Tests;

public abstract class SqliteTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    protected SqliteTestBase()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    protected DbContextOptions<AppDbContext> Options { get; }

    protected AppDbContext CreateDbContext() => new(Options);

    protected AppUser CreateSeedUser(AppDbContext? db = null)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash"
        };
        var ctx = db ?? CreateDbContext();
        ctx.Users.Add(user);
        ctx.SaveChanges();
        if (db == null) ctx.Dispose();
        return user;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
        _connection.Dispose();
    }

    protected virtual void Cleanup()
    {
    }
}