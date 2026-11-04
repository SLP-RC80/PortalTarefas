using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Data;

namespace PortalTarefas.Tests.Fixtures;

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TestDatabase(SqliteConnection connection)
    {
        _connection = connection;
    }

    public static async Task<TestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection(
            "Data Source=:memory:");

        await connection.OpenAsync();

        var database = new TestDatabase(connection);

        await using var context = database.CreateContext();

        await context.Database.EnsureCreatedAsync();

        return database;
    }

    public PortalTarefasDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<PortalTarefasDbContext>()
                .UseSqlite(_connection)
                .EnableDetailedErrors()
                .Options;

        return new PortalTarefasDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
