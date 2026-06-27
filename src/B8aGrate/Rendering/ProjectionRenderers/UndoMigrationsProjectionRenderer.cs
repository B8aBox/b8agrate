using B8aGrate.Domain.Projections;

namespace B8aGrate.Rendering.ProjectionRenderers;

public sealed class UndoMigrationsProjectionRenderer : ProjectionRenderer<UndoMigrationsProjection>
{
    protected override void Render(UndoMigrationsProjection projection, TextWriter writer)
    {
        if (!string.IsNullOrWhiteSpace(projection.Message))
            writer.WriteLine(projection.Message);

        MigrationTextWriter.RenderMigrations("Applied undo migrations:", projection.AppliedMigrations, writer);
    }
}