using System.Text.Json.Serialization;
using B8aGrate.Domain.ValueObjects;

namespace B8aGrate.Domain.Entities;

public sealed class Migration
{
    public string? Checksum { get; set; }

    public required string Description { get; set; }

    public long ExecutionMilliseconds { get; set; }

    public DateTimeOffset ExecutionMoment { get; set; }

    public long Id { get; set; }

    public bool IsSuccess { get; set; }

    public MigrationKind Kind { get; set; }

    public required string Script { get; set; }

    [JsonIgnore]
    public string? Sql { get; set; }

    public string? Version { get; set; }
}