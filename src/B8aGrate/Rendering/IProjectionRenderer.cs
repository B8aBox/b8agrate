namespace B8aGrate.Rendering;

public interface IProjectionRenderer
{
    bool CanRender(Type projectionType);

    void Render(object projection, TextWriter writer);
}