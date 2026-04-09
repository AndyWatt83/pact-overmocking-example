using System.Reflection;
using BlogApi.Contract.Tests.ProviderStates;

namespace BlogApi.Contract.Tests.Middleware;

public class ProviderStateDispatcher(IEnumerable<IProviderStateHandler> handlers)
{
    public async Task DispatchAsync(string state)
    {
        foreach (var handler in handlers)
        {
            var method = handler.GetType()
                .GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<ProviderStateAttribute>()?.State == state);

            if (method == null)
                continue;

            var result = method.Invoke(handler, null);

            if (result is Task task)
                await task;

            return;
        }
    }
}
