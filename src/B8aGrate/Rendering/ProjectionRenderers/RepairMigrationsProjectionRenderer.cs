using B8aGrate.Domain.Projections;

namespace B8aGrate.Rendering.ProjectionRenderers;

public sealed class RepairMigrationsProjectionRenderer : ProjectionRenderer<RepairMigrationsProjection>
{
    protected override void Render(RepairMigrationsProjection projection, TextWriter writer)
    {
        foreach (var message in projection.Messages)
            writer.WriteLine(message);

        MigrationTextWriter.RenderMigrations("Failed migrations:", projection.FailedMigrations, writer);
        MigrationTextWriter.RenderMigrations("Invalid migrations:", projection.InvalidMigrations, writer);
        MigrationTextWriter.RenderErrors("Errors:", projection.Errors, writer);
    }
}