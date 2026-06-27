using System.Text.Json;
using B8aGrate.Domain.Projections;
using B8aGrate.Domain.Validation;
using B8aGrate.Domain.ValueObjects;
using B8aGrate.Rendering;
using Xunit;

namespace B8aGrate.Tests;

public sealed class ResultRendererTests
{
    [Fact]
    public void Render_JsonSuccessWithoutContent_WritesEnvelope()
    {
        var writer = new StringWriter();
        var renderer = new ResultRenderer([]);

        var exitCode = renderer.Render(Results.Success(), "json", writer);

        using var json = JsonDocument.Parse(writer.ToString());
        var root = json.RootElement;

        Assert.Equal(0, exitCode);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("content").ValueKind);
        Assert.Empty(root.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void Render_JsonSuccessWithContent_WritesEnvelope()
    {
        var writer = new StringWriter();
        var renderer = new ResultRenderer([]);

        var exitCode = renderer.Render(Results.Success(new MigrationInformationProjection
        {
            Provider = ProviderType.PostgreSql,
            BaselineVersion = "000001"
        }), "json", writer);

        using var json = JsonDocument.Parse(writer.ToString());
        var root = json.RootElement;

        Assert.Equal(0, exitCode);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("PostgreSql", root.GetProperty("content").GetProperty("provider").GetString());
        Assert.Equal("000001", root.GetProperty("content").GetProperty("baselineVersion").GetString());
        Assert.Empty(root.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void Render_JsonFailure_WritesEnvelopeWithErrors()
    {
        var writer = new StringWriter();
        var errorWriter = new StringWriter();
        var renderer = new ResultRenderer([]);

        var exitCode = renderer.Render(Results.Failure("No applied provisioning record was found.", "NoProvision"), "json", writer, errorWriter);

        using var json = JsonDocument.Parse(writer.ToString());
        var root = json.RootElement;
        var error = root.GetProperty("errors").EnumerateArray().Single();

        Assert.Equal(1, exitCode);
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("content").ValueKind);
        Assert.Equal("No applied provisioning record was found.", error.GetProperty("message").GetString());
        Assert.Equal(string.Empty, errorWriter.ToString());
    }
}