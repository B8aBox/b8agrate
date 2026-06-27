using System.Text.Json;
using System.Text.Json.Serialization;

namespace B8aGrate.Domain.ValueObjects;

public sealed class Configuration
{
    #region Constants

    private const string Filename = "b8agrate.json";

    #endregion


    #region Private Fields

    private static JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    #endregion


    #region Public Properties

    public CommandOptions Command { get; set; } = new();

    public EnvironmentVariableOptions EnvironmentVariables { get; set; } = new();

    public MigrationOptions Migration { get; set; } = new();

    public ProviderType Provider { get; set; } = ProviderType.SqlServer;

    public VersioningOptions Versioning { get; set; } = new();

    #endregion


    #region Public Methods

    public static string GetPath(string root) => Path.Combine(root, Filename);

    public static async Task<Configuration> Load(string root, CancellationToken cancellationToken)
    {
        var path = GetPath(root);

        if (!File.Exists(path))
            return new Configuration();

        var json = await File.ReadAllTextAsync(path, cancellationToken);

        return JsonSerializer.Deserialize<Configuration>(json, _jsonSerializerOptions) ?? new Configuration();
    }

    public string ToJson() => JsonSerializer.Serialize(this, _jsonSerializerOptions);

    #endregion
}