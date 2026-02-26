using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Services.CurrentUser;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Respawn.Graph;
using System.Data.Common;
using Testcontainers.MsSql;
namespace Tests.Integration;

public class SqlServerFixture : IAsyncLifetime
{
    private const string SqlImage = "mcr.microsoft.com/mssql/server:2025-latest";

    private readonly MsSqlContainer _dbContainer =
        new MsSqlBuilder(SqlImage)
            .WithPassword("HiveStrongPassword123!")
            .Build();

    private DbConnection _connection = null!;

    private Respawner _respawner = null!;

    public string ConnectionString => _dbContainer.GetConnectionString();


    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using (var context = new ApplicationDbContext(options, new TestCurrentUserService()))
        {
            await context.Database.MigrateAsync();
        }

        _connection = new SqlConnection(ConnectionString);
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer, SchemasToInclude = ["dbo"], TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _dbContainer.StopAsync();
    }

    /// <summary>
    ///     Call this inside each test to reset DB state.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_connection);
    }
}

[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
}

public class TestCurrentUserService : ICurrentUserService
{
    public string UserId => "integration-test-user";

    public CurrentUserDetails GetCurrentUser()
    {
        return new CurrentUserDetails("integration-test-user", "test@hive.com", new List<string>
        {
            "ROLE_USER"
        });
    }
}