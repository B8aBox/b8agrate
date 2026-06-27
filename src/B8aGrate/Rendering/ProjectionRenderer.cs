namespace B8aGrate.Rendering;

public abstract class ProjectionRenderer<TProjection> : IProjectionRenderer
{
    #region Public Methods

    public bool CanRender(Type projectionType) => projectionType == typeof(TProjection);

    public void Render(object projection, TextWriter writer) => Render((TProjection)projection, writer);

    #endregion


    #region Protected Methods

    protected abstract void Render(TProjection projection, TextWriter writer);

    #endregion
}