namespace IslandCaller.Views;

/// <summary>
/// Provides the window services shared by all hover themes.
/// </summary>
public interface IHoverWindow
{
    void RequestContentSizeUpdate();

    void BeginDrag();

    void EndDragAndClamp();
}
