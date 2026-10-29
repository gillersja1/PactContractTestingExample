using PactNet.Infrastructure.Outputters;

namespace Users.Provider.ContractTests;

/// <summary>Sends PactNet's log output to the NUnit test log.</summary>
public sealed class NUnitOutput : IOutput
{
    public void WriteLine(string line) => TestContext.Progress.WriteLine(line);
}

public static class RepoPaths
{
    /// <summary>pacts/ at the repository root, written by the consumer tests.</summary>
    public static string PactDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !dir.EnumerateFiles("*.slnx").Any())
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found"), "pacts");
        }
    }
}
