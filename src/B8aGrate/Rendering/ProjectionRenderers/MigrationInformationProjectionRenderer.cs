using B8aGrate.Domain.Projections;

namespace B8aGrate.Rendering.ProjectionRenderers;

public sealed class MigrationInformationProjectionRenderer : ProjectionRenderer<MigrationInformationProjection>
{
    protected override void Render(MigrationInformationProjection projection, TextWriter writer)
    {
        writer.WriteLine($"Provider: {projection.Provider}");

        if (!string.IsNullOrWhiteSpace(projection.BaselineVersion))
            writer.WriteLine($"Baseline version: {projection.BaselineVersion}");

        MigrationTextWriter.RenderMigrations("Applied migrations:", projection.AppliedMigrations, writer);
        MigrationTextWriter.RenderMigrations("Pending migrations:", projection.PendingMigrations, writer);
        MigrationTextWriter.RenderErrors("Errors:", projection.Errors, writer);
    }
}