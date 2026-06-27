using B8aGrate.Domain.Projections;

namespace B8aGrate.Rendering.ProjectionRenderers;

public sealed class ApplyMigrationsProjectionRenderer : ProjectionRenderer<ApplyMigrationsProjection>
{
    protected override void Render(ApplyMigrationsProjection projection, TextWriter writer)
    {
        if (!string.IsNullOrWhiteSpace(projection.Message))
            writer.WriteLine(projection.Message);

        MigrationTextWriter.RenderMigrations("Applied migrations:", projection.AppliedMigrations, writer);
        MigrationTextWriter.RenderMigrations("Skipped migrations:", projection.SkippedMigrations, writer);
    }
}