using DotNet.Testcontainers.Images;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using Testcontainers.MsSql;
using Xunit;

namespace Procurement.TestSupport;

/// <summary>
/// One SQL Server container per test assembly (register with
/// <c>[assembly: AssemblyFixture(typeof(SqlServerFixture))]</c>). Each test gets its own database,
/// created by publishing Procurement.Database.dacpac with DacFx, seed included.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Image = "mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-24.04";

    /// <summary>The dacpac copied next to the test assembly by the sqlproj ProjectReference.</summary>
    public static string DacpacPath { get; } = Path.Combine(AppContext.BaseDirectory, "Procurement.Database.dacpac");

    // Msg 1807 ("Could not obtain exclusive lock on database 'model'"): CREATE DATABASE needs an exclusive
    // lock on 'model', so concurrent publishes fail. Static, so it serializes every fixture in the process.
    private static readonly SemaphoreSlim gate = new(1, 1);

    // The image is amd64 only; on Apple Silicon Docker Desktop runs it under Rosetta.
    private readonly MsSqlContainer? _container = SkipIfNoDocker.IsDockerAvailable
        ? new MsSqlBuilder(new DockerImage(Image, new Platform("linux/amd64"))).Build()
        : null;

    public async ValueTask InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Publishes the dacpac into a new database (schema + post-deployment seed) and returns its
    /// connection string. Skips the calling test when Docker is unavailable.
    /// </summary>
    public Task<string> CreateDatabaseAsync(string? name = null) =>
        PublishAsync(name ?? $"procurement_{Guid.NewGuid():N}", createNewDatabase: true);

    /// <summary>
    /// Publishes the dacpac again over an existing database, re-running the post-deployment seed
    /// the way a second <c>docker compose up</c> does.
    /// </summary>
    public Task<string> RedeployAsync(string name) => PublishAsync(name, createNewDatabase: false);

    private async Task<string> PublishAsync(string databaseName, bool createNewDatabase)
    {
        SkipIfNoDocker.Check();
        if (!File.Exists(DacpacPath))
        {
            throw new FileNotFoundException("Procurement.Database.dacpac was not copied to the test output.", DacpacPath);
        }

        var serverConnectionString = _container!.GetConnectionString();
        var options = new DacDeployOptions
        {
            CreateNewDatabase = createNewDatabase,
            BlockOnPossibleDataLoss = true,
        };

        // Both CreateDatabaseAsync and RedeployAsync funnel through here, so both are gated.
        if (!await gate.WaitAsync(TimeSpan.FromMinutes(5)))
        {
            throw new TimeoutException(
                $"Timed out after 5 minutes waiting for another test database publish to finish before publishing '{databaseName}'.");
        }

        try
        {
            await Task.Run(() =>
            {
                using var package = DacPackage.Load(DacpacPath);
                new DacServices(serverConnectionString).Deploy(package, databaseName, upgradeExisting: true, options);
            });
        }
        finally
        {
            gate.Release();
        }

        return new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = databaseName }.ConnectionString;
    }
}
