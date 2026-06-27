using System.Text.Json;
using System.Text.Json.Serialization;
using YuckQi.Domain.Validation;
using YuckQi.Domain.Validation.Abstract.Interfaces;
using YuckQi.Domain.Validation.JsonConverters;

namespace B8aGrate.Rendering;

public sealed class ResultRenderer(IEnumerable<IProjectionRenderer> projectionRenderers)
{
    #region Private Fields

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new ResultCodeJsonConverter(),
            new ResultMessageJsonConverter()
        },
        WriteIndented = true
    };

    private readonly IReadOnlyCollection<IProjectionRenderer> _projectionRenderers = projectionRenderers.ToArray();

    #endregion


    #region Public Methods

    public int Render(IResult result, string output, TextWriter? writer = null, TextWriter? errorWriter = null)
    {
        writer ??= Console.Out;
        errorWriter ??= Console.Error;

        if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
        {
            RenderJson(result, writer);

            return result.IsValid ? 0 : 1;
        }

        if (!result.IsValid)
        {
            RenderErrors(result.Detail, errorWriter);

            return 1;
        }

        if (TryGetResultContent(result, out var content) && content is not null)
            RenderContent(content, writer);

        return 0;
    }

    #endregion


    #region Private Methods

    private void RenderContent(object content, TextWriter writer)
    {
        var renderer = _projectionRenderers.FirstOrDefault(x => x.CanRender(content.GetType()));

        if (renderer is not null)
        {
            renderer.Render(content, writer);

            return;
        }

        writer.WriteLine(JsonSerializer.Serialize(content, JsonOptions));
    }

    private static void RenderErrors(IEnumerable<ResultDetail> details, TextWriter writer)
    {
        foreach (var detail in details)
        {
            var code = detail.Code?.ToString();

            writer.WriteLine(string.IsNullOrWhiteSpace(code) ? detail.Message : $"{code}: {detail.Message}");
        }
    }

    private static void RenderJson(IResult result, TextWriter writer)
    {
        var response = new JsonResultEnvelope(
            result.IsValid,
            result.IsValid ? 0 : 1,
            result.IsValid && TryGetResultContent(result, out var content) ? content : null,
            result.IsValid ? [] : result.Detail);

        writer.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static bool TryGetResultContent(IResult result, out object? content)
    {
        var resultType = result.GetType();
        var genericResultInterface = resultType.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IResult<>));

        if (genericResultInterface is null)
        {
            content = null;

            return false;
        }

        content = genericResultInterface.GetProperty("Content")?.GetValue(result);

        return true;
    }

    #endregion


    #region Private Records

    private sealed record JsonResultEnvelope(bool Success, int ExitCode, object? Content, IEnumerable<ResultDetail> Errors);

    #endregion
}