namespace SvgViewer.Web.Models;

public class SvgDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Untitled";
    public double Width { get; set; } = 800;
    public double Height { get; set; } = 600;
    public string ViewBox { get; set; } = "0 0 800 600";
    public string? BackgroundColor { get; set; }
    
    public List<SvgElement> Elements { get; set; } = [];
    
    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Get all elements in a flat structure
    public IEnumerable<SvgElement> GetAllElements()
    {
        foreach (var element in Elements)
        {
            yield return element;
            foreach (var child in GetChildrenRecursive(element))
            {
                yield return child;
            }
        }
    }
    
    // Find element by ID
    public SvgElement? FindElement(string id)
    {
        return GetAllElements().FirstOrDefault(e => e.Id == id);
    }
    
    private IEnumerable<SvgElement> GetChildrenRecursive(SvgElement element)
    {
        foreach (var child in element.Children)
        {
            yield return child;
            foreach (var grandchild in GetChildrenRecursive(child))
            {
                yield return grandchild;
            }
        }
    }
}
