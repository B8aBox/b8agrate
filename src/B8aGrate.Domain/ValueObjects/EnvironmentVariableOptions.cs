namespace B8aGrate.Domain.ValueObjects;

public sealed class EnvironmentVariableOptions
{
    public string AdminConnection { get; set; } = "B8AGRATE_ADMIN_CONNECTION";

    public string Connection { get; set; } = "B8AGRATE_CONNECTION";
}