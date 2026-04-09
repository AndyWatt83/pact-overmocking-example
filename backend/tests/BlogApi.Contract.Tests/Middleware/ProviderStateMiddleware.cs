using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BlogApi.Contract.Tests.Middleware;

public class ProviderStateMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/provider-states"))
        {
            await HandleProviderStatesRequest(context);
            return;
        }

        await next(context);
    }

    private async Task HandleProviderStatesRequest(HttpContext context)
    {
        try
        {
            context.Response.ContentType = "application/json";

            if (context.Request.Method != HttpMethod.Post.Method)
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var providerState = JsonSerializer.Deserialize<ProviderStateRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (providerState?.Action == null || providerState.Action.Equals("setup", StringComparison.OrdinalIgnoreCase))
            {
                var dispatcher = serviceProvider.GetService<ProviderStateDispatcher>();
                if (dispatcher != null && !string.IsNullOrEmpty(providerState?.State))
                {
                    await dispatcher.DispatchAsync(providerState.State);
                }
            }

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync("{}");
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync($"{{\"error\": \"{ex.Message}\"}}");
        }
    }

    private sealed class ProviderStateRequest
    {
        public string? State { get; set; }
        public string? Action { get; set; }
    }
}
