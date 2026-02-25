namespace SvgViewer.Web.Models;

public class TreeItemData<T>
{
    public T Value { get; set; } = default!;
    public HashSet<TreeItemData<T>> Children { get; set; } = [];
    public bool Expanded { get; set; }
}
