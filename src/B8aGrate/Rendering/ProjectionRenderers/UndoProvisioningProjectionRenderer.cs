using B8aGrate.Domain.Projections;

namespace B8aGrate.Rendering.ProjectionRenderers;

public sealed class UndoProvisioningProjectionRenderer : ProjectionRenderer<UndoProvisioningProjection>
{
    protected override void Render(UndoProvisioningProjection projection, TextWriter writer)
    {
        if (!string.IsNullOrWhiteSpace(projection.Message))
            writer.WriteLine(projection.Message);

        MigrationTextWriter.RenderMigrations("Applied provisioning undo migrations:", projection.AppliedMigrations, writer);
    }
}