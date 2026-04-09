using System.Net;
using System.Net.Sockets;
using BlogApi.Contract.Tests.Middleware;
using BlogApi.Contract.Tests.ProviderStates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace BlogApi.Contract.Tests.Mocked;

public static class MockedHostBuilder
{
    public static (IHost Host, int Port) Build()
    {
        var builder = WebApplication.CreateBuilder();
        int port = GetFreeTcpPort();

        // Register a Moq mock of IPostRepository — this replaces the real one
        var repoMock = new Mock<IPostRepository>(MockBehavior.Strict);
        builder.Services.AddSingleton(repoMock);
        builder.Services.AddSingleton<IPostRepository>(sp => sp.GetRequiredService<Mock<IPostRepository>>().Object);

        // Provider state infrastructure
        builder.Services.AddSingleton<ProviderStateDispatcher>();
        builder.Services.AddSingleton<IProviderStateHandler, MockedStateHandlers>();

        var app = builder.Build();

        app.UseMiddleware<ProviderStateMiddleware>();

        app.MapGet("/api/posts", (IPostRepository repo) => Results.Ok(repo.GetAll()));

        app.MapGet("/api/posts/{id}", (string id, IPostRepository repo) =>
        {
            var post = repo.GetById(id);
            return post is not null ? Results.Ok(post) : Results.NotFound();
        });

        app.Urls.Add($"http://localhost:{port}");

        return (app, port);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
