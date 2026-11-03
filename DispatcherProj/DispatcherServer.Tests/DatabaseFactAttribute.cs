using Xunit;

namespace DispatcherServer.Tests;

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPATCHER_TEST_CONNECTION_STRING")))
        {
            Skip = "Set DISPATCHER_TEST_CONNECTION_STRING to run read-only SQL Server integration tests.";
        }
    }
}
