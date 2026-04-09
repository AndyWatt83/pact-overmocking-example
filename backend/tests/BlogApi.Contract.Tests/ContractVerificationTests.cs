using BlogApi.Contract.Tests.Mocked;
using BlogApi.Contract.Tests.Real;
using Microsoft.Extensions.Hosting;
using PactNet;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace BlogApi.Contract.Tests;

public class ContractVerificationTests(ITestOutputHelper output) : IAsyncLifetime
{
    private IHost? _host;
    private int _port;

    public async Task InitializeAsync()
    {
        var mode = (Environment.GetEnvironmentVariable("PROVIDER_TEST_MODE") ?? "real")
            .ToLowerInvariant();

        (_host, _port) = mode switch
        {
            "mocked" => MockedHostBuilder.Build(),
            "real" => RealHostBuilder.Build(),
            _ => throw new InvalidOperationException(
                $"Unknown PROVIDER_TEST_MODE '{mode}'. Valid values: mocked, real")
        };

        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [Fact]
    public void EnsureConsumerContractsAreHonoured()
    {
        var pactPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "..",
            "frontend", "pacts", "blog-frontend-blog-api.json");

        var serverUri = new Uri($"http://localhost:{_port}");

        var verifier = new PactVerifier("blog-api", new PactVerifierConfig
        {
            LogLevel = PactLogLevel.Debug,
            Outputters = [new XunitOutput(output)]
        });

        verifier
            .WithHttpEndpoint(serverUri)
            .WithFileSource(new FileInfo(pactPath))
            .WithProviderStateUrl(new Uri(serverUri, "/provider-states"))
            .Verify();
    }
}

internal class XunitOutput(ITestOutputHelper output) : PactNet.Infrastructure.Outputters.IOutput
{
    public void WriteLine(string line) => output.WriteLine(line);
}
