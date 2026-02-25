namespace SvgViewer.Web.Models;

public class SvgElement
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "rect"; // rect, circle, ellipse, line, polyline, polygon, path, text, group
    public string? ParentId { get; set; }
    public List<SvgElement> Children { get; set; } = [];
    
    // Display properties
    public Dictionary<string, object> Attributes { get; set; } = new();
    
    // Custom logic attributes (data-logic-*)
    public Dictionary<string, string> LogicAttributes { get; set; } = new();
    
    // Common properties for quick access
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string? Fill { get; set; }
    public string? Stroke { get; set; }
    public double StrokeWidth { get; set; } = 1;
    public double Opacity { get; set; } = 1;
    public string? Transform { get; set; }
    
    // Type-specific properties
    public double? Radius { get; set; } // For circles
    public double? RadiusX { get; set; } // For ellipses
    public double? RadiusY { get; set; } // For ellipses
    public string? Points { get; set; } // For polyline/polygon
    public string? PathData { get; set; } // For paths
    public string? Text { get; set; } // For text elements
    
    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public SvgElement()
    {
        Id = Guid.NewGuid().ToString("N")[..8];
    }
}

public enum SvgElementType
{
    Rectangle,
    Circle,
    Ellipse,
    Line,
    Polyline,
    Polygon,
    Path,
    Text,
    Group
}
