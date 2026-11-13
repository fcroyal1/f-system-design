using DotNet.Testcontainers.Configurations;
using Xunit;

namespace Procurement.TestSupport;

/// <summary>
/// Skips a SQL Server test with a clear message when no Docker endpoint is reachable,
/// instead of failing it. When Docker is running the tests run normally.
/// </summary>
public static class SkipIfNoDocker
{
    public const string Message =
        "Docker not available: SQL Server tests need a running Docker engine (Testcontainers).";

    private static readonly Lazy<bool> Available = new(Probe);

    public static bool IsDockerAvailable => Available.Value;

    /// <summary>Marks the current test as skipped when Docker is unavailable.</summary>
    public static void Check() => Assert.SkipUnless(IsDockerAvailable, Message);

    private static bool Probe()
    {
        try
        {
            return TestcontainersSettings.OS.DockerEndpointAuthConfig is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
