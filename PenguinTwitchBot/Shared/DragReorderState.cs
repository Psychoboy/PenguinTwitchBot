namespace PenguinTwitchBot.Shared;

public sealed class DragReorderState
{
    public int? DraggingIndex { get; private set; }
    public int? DragOverIndex { get; private set; }
    public string? ActiveGroup { get; private set; }

    public void Start(int index, string? group = null)
    {
        DraggingIndex = index;
        DragOverIndex = index;
        ActiveGroup = group;
    }

    public bool Enter(int index, string? group = null)
    {
        if (DraggingIndex is null || !string.Equals(ActiveGroup, group, StringComparison.Ordinal))
        {
            return false;
        }

        if (DragOverIndex == index)
        {
            return false;
        }

        DragOverIndex = index;
        return true;
    }

    public void End()
    {
        DraggingIndex = null;
        DragOverIndex = null;
        ActiveGroup = null;
    }

    public bool IsDragging(int index, string? group = null) =>
        DraggingIndex == index && string.Equals(ActiveGroup, group, StringComparison.Ordinal);

    public string GetItemCssClass(int index, string? group = null)
    {
        if (DraggingIndex is null || !string.Equals(ActiveGroup, group, StringComparison.Ordinal))
        {
            return "drag-reorder-item";
        }

        if (DraggingIndex == index)
        {
            return "drag-reorder-item drag-reorder-item--dragging";
        }

        if (DragOverIndex == index)
        {
            return DraggingIndex < index
                ? "drag-reorder-item drag-reorder-item--drop-after"
                : "drag-reorder-item drag-reorder-item--drop-before";
        }

        return "drag-reorder-item";
    }

    public bool TryCompleteDrop<T>(IList<T> items, int targetIndex, out int fromIndex, out int toIndex, string? group = null)
    {
        fromIndex = DraggingIndex ?? -1;
        toIndex = targetIndex;
        var matchesGroup = string.Equals(ActiveGroup, group, StringComparison.Ordinal);
        End();

        if (!matchesGroup)
        {
            return false;
        }

        return MoveItem(items, fromIndex, toIndex);
    }

    public static bool MoveItem<T>(IList<T> items, int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= items.Count || toIndex < 0 || toIndex >= items.Count || fromIndex == toIndex)
        {
            return false;
        }

        var item = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, item);
        return true;
    }

    public static void ReassignDescendingZIndex<T>(IList<T> descendingOrderedItems, Action<T, int> setZIndex)
    {
        for (var i = 0; i < descendingOrderedItems.Count; i++)
        {
            setZIndex(descendingOrderedItems[i], descendingOrderedItems.Count - i);
        }
    }
}
