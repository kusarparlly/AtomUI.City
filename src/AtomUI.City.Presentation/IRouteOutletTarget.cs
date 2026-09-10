namespace AtomUI.City.Presentation;

public interface IRouteOutletTarget
{
    object? Content { get; }

    void SetContent(object? content);
}
