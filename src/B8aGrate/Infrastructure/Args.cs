namespace B8aGrate.Infrastructure;

public sealed class Args
{
    #region Private Fields

    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    #endregion


    #region Public Properties

    public IEnumerable<string> Keys => _values.Keys;

    public Dictionary<string, string?> Values => _values.ToDictionary(x => x.Key, x => x.Value);

    #endregion


    #region Public Methods

    public string? Get(string key) => _values.GetValueOrDefault(key);

    public static Args Parse(string[] args)
    {
        var result = new Args();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (!arg.StartsWith("--") && !arg.StartsWith('-'))
                continue;

            var key = arg.StartsWith("--") ? arg[2..] : arg[1..];

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--") && !args[i + 1].StartsWith('-'))
                result._values[key] = args[++i];
            else
                result._values[key] = "true";
        }

        return result;
    }

    #endregion
}