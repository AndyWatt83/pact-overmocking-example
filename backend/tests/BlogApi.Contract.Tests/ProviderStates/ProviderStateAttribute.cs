namespace BlogApi.Contract.Tests.ProviderStates;

[AttributeUsage(AttributeTargets.Method)]
public class ProviderStateAttribute(string state) : Attribute
{
    public string State { get; } = state;
}
