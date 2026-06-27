using B8aGrate.Domain.Projections;

namespace B8aGrate.Rendering.ProjectionRenderers;

public sealed class MigrationSnapshotProjectionRenderer : ProjectionRenderer<MigrationSnapshotProjection>
{
    protected override void Render(MigrationSnapshotProjection projection, TextWriter writer)
    {
        writer.WriteLine($"Provider: {projection.Provider}");

        if (!string.IsNullOrWhiteSpace(projection.CurrentVersion))
            writer.WriteLine($"Current version: {projection.CurrentVersion}");

        MigrationTextWriter.RenderMigrations("Applied migrations:", projection.AppliedMigrations, writer);
        MigrationTextWriter.RenderMigrations("Pending migrations:", projection.PendingMigrations, writer);
        MigrationTextWriter.RenderErrors("Errors:", projection.Errors, writer);
    }
}