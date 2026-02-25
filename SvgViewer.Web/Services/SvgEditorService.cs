using System.Text;
using System.Xml.Linq;
using SvgViewer.Web.Models;

namespace SvgViewer.Web.Services;

public class SvgEditorService : ISvgEditorService
{
    private SvgDocument _currentDocument = new();
    private SvgElement? _selectedElement;

    public SvgDocument CurrentDocument => _currentDocument;
    public SvgElement? SelectedElement => _selectedElement;

    public event EventHandler<SvgDocument>? DocumentChanged;
    public event EventHandler<SvgElement>? ElementAdded;
    public event EventHandler<SvgElement>? ElementUpdated;
    public event EventHandler<string>? ElementDeleted;
    public event EventHandler<string>? ElementSelected;

    public SvgEditorService()
    {
        NewDocument("Default Document", 800, 600);
    }

    public void NewDocument(string name, double width, double height)
    {
        _currentDocument = new SvgDocument
        {
            Name = name,
            Width = width,
            Height = height,
            ViewBox = $"0 0 {width} {height}"
        };
        _selectedElement = null;
        DocumentChanged?.Invoke(this, _currentDocument);
    }

    public Task<bool> LoadDocumentAsync(string svgContent)
    {
        try
        {
            var doc = XDocument.Parse(svgContent);
            var svgElement = doc.Root;
            
            if (svgElement?.Name.LocalName != "svg")
                return Task.FromResult(false);

            var width = double.Parse(svgElement.Attribute("width")?.Value ?? "800");
            var height = double.Parse(svgElement.Attribute("height")?.Value ?? "600");
            
            _currentDocument = new SvgDocument
            {
                Name = "Imported Document",
                Width = width,
                Height = height,
                ViewBox = svgElement.Attribute("viewBox")?.Value ?? $"0 0 {width} {height}"
            };

            // Parse SVG elements (simplified - extend as needed)
            foreach (var element in svgElement.Elements())
            {
                var svgEl = ParseXElement(element);
                if (svgEl != null)
                {
                    _currentDocument.Elements.Add(svgEl);
                }
            }

            DocumentChanged?.Invoke(this, _currentDocument);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<string> ExportDocumentAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<svg width=\"{_currentDocument.Width}\" height=\"{_currentDocument.Height}\" viewBox=\"{_currentDocument.ViewBox}\" xmlns=\"http://www.w3.org/2000/svg\">");
        
        foreach (var element in _currentDocument.Elements)
        {
            AppendElement(sb, element, 1);
        }
        
        sb.AppendLine("</svg>");
        return Task.FromResult(sb.ToString());
    }

    public void SelectElement(string? elementId)
    {
        _selectedElement = elementId != null ? _currentDocument.FindElement(elementId) : null;
        ElementSelected?.Invoke(this, elementId ?? string.Empty);
    }

    public SvgElement AddElement(SvgElement element, string? parentId = null)
    {
        element.ParentId = parentId;
        element.CreatedAt = DateTime.UtcNow;
        element.UpdatedAt = DateTime.UtcNow;

        if (parentId == null)
        {
            _currentDocument.Elements.Add(element);
        }
        else
        {
            var parent = _currentDocument.FindElement(parentId);
            parent?.Children.Add(element);
        }

        _currentDocument.UpdatedAt = DateTime.UtcNow;
        ElementAdded?.Invoke(this, element);
        return element;
    }

    public void UpdateElement(SvgElement element)
    {
        var existing = _currentDocument.FindElement(element.Id);
        if (existing != null)
        {
            element.UpdatedAt = DateTime.UtcNow;
            
            // Update properties
            existing.Type = element.Type;
            existing.X = element.X;
            existing.Y = element.Y;
            existing.Width = element.Width;
            existing.Height = element.Height;
            existing.Fill = element.Fill;
            existing.Stroke = element.Stroke;
            existing.StrokeWidth = element.StrokeWidth;
            existing.Opacity = element.Opacity;
            existing.Transform = element.Transform;
            existing.Radius = element.Radius;
            existing.RadiusX = element.RadiusX;
            existing.RadiusY = element.RadiusY;
            existing.Points = element.Points;
            existing.PathData = element.PathData;
            existing.Text = element.Text;
            existing.Attributes = element.Attributes;
            existing.LogicAttributes = element.LogicAttributes;
            existing.UpdatedAt = element.UpdatedAt;

            _currentDocument.UpdatedAt = DateTime.UtcNow;
            ElementUpdated?.Invoke(this, existing);
        }
    }

    public void DeleteElement(string elementId)
    {
        var element = _currentDocument.FindElement(elementId);
        if (element == null) return;

        if (element.ParentId == null)
        {
            _currentDocument.Elements.Remove(element);
        }
        else
        {
            var parent = _currentDocument.FindElement(element.ParentId);
            parent?.Children.Remove(element);
        }

        if (_selectedElement?.Id == elementId)
        {
            _selectedElement = null;
        }

        _currentDocument.UpdatedAt = DateTime.UtcNow;
        ElementDeleted?.Invoke(this, elementId);
    }

    public SvgElement? GetElement(string elementId)
    {
        return _currentDocument.FindElement(elementId);
    }

    public IEnumerable<SvgElement> GetRootElements()
    {
        return _currentDocument.Elements;
    }

    public IEnumerable<SvgElement> GetAllElements()
    {
        return _currentDocument.GetAllElements();
    }

    public void SetLogicAttribute(string elementId, string key, string value)
    {
        var element = _currentDocument.FindElement(elementId);
        if (element != null)
        {
            element.LogicAttributes[key] = value;
            element.UpdatedAt = DateTime.UtcNow;
            ElementUpdated?.Invoke(this, element);
        }
    }

    public void RemoveLogicAttribute(string elementId, string key)
    {
        var element = _currentDocument.FindElement(elementId);
        if (element != null)
        {
            element.LogicAttributes.Remove(key);
            element.UpdatedAt = DateTime.UtcNow;
            ElementUpdated?.Invoke(this, element);
        }
    }

    public Dictionary<string, string> GetLogicAttributes(string elementId)
    {
        var element = _currentDocument.FindElement(elementId);
        return element?.LogicAttributes ?? new Dictionary<string, string>();
    }

    private SvgElement? ParseXElement(XElement xElement)
    {
        var element = new SvgElement
        {
            Type = xElement.Name.LocalName,
            Id = xElement.Attribute("id")?.Value ?? Guid.NewGuid().ToString("N")[..8]
        };

        // Parse common attributes
        if (double.TryParse(xElement.Attribute("x")?.Value, out var x))
            element.X = x;
        if (double.TryParse(xElement.Attribute("y")?.Value, out var y))
            element.Y = y;
        if (double.TryParse(xElement.Attribute("width")?.Value, out var width))
            element.Width = width;
        if (double.TryParse(xElement.Attribute("height")?.Value, out var height))
            element.Height = height;
        
        element.Fill = xElement.Attribute("fill")?.Value;
        element.Stroke = xElement.Attribute("stroke")?.Value;
        
        if (double.TryParse(xElement.Attribute("stroke-width")?.Value, out var strokeWidth))
            element.StrokeWidth = strokeWidth;
        if (double.TryParse(xElement.Attribute("opacity")?.Value, out var opacity))
            element.Opacity = opacity;

        element.Transform = xElement.Attribute("transform")?.Value;

        // Parse logic attributes (data-logic-*)
        foreach (var attr in xElement.Attributes().Where(a => a.Name.LocalName.StartsWith("data-logic-")))
        {
            var key = attr.Name.LocalName.Replace("data-logic-", "");
            element.LogicAttributes[key] = attr.Value;
        }

        // Parse children
        foreach (var child in xElement.Elements())
        {
            var childElement = ParseXElement(child);
            if (childElement != null)
            {
                childElement.ParentId = element.Id;
                element.Children.Add(childElement);
            }
        }

        return element;
    }

    private void AppendElement(StringBuilder sb, SvgElement element, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        var attrs = BuildAttributeString(element);
        
        if (element.Children.Count == 0 && string.IsNullOrEmpty(element.Text))
        {
            sb.AppendLine($"{indentStr}<{element.Type}{attrs} />");
        }
        else
        {
            sb.AppendLine($"{indentStr}<{element.Type}{attrs}>");
            
            if (!string.IsNullOrEmpty(element.Text))
            {
                sb.AppendLine($"{indentStr}  {element.Text}");
            }
            
            foreach (var child in element.Children)
            {
                AppendElement(sb, child, indent + 1);
            }
            
            sb.AppendLine($"{indentStr}</{element.Type}>");
        }
    }

    private string BuildAttributeString(SvgElement element)
    {
        var attrs = new List<string>
        {
            $"id=\"{element.Id}\""
        };

        // Add standard attributes based on type
        if (element.Type != "g" && element.Type != "group")
        {
            if (element.X != 0) attrs.Add($"x=\"{element.X}\"");
            if (element.Y != 0) attrs.Add($"y=\"{element.Y}\"");
        }

        if (element.Type == "rect" || element.Type == "ellipse" || element.Type == "image")
        {
            if (element.Width != 0) attrs.Add($"width=\"{element.Width}\"");
            if (element.Height != 0) attrs.Add($"height=\"{element.Height}\"");
        }

        if (element.Type == "circle" && element.Radius.HasValue)
        {
            attrs.Add($"cx=\"{element.X}\"");
            attrs.Add($"cy=\"{element.Y}\"");
            attrs.Add($"r=\"{element.Radius.Value}\"");
        }

        if (element.Type == "ellipse" && element.RadiusX.HasValue && element.RadiusY.HasValue)
        {
            attrs.Add($"cx=\"{element.X}\"");
            attrs.Add($"cy=\"{element.Y}\"");
            attrs.Add($"rx=\"{element.RadiusX.Value}\"");
            attrs.Add($"ry=\"{element.RadiusY.Value}\"");
        }

        if (!string.IsNullOrEmpty(element.Points))
            attrs.Add($"points=\"{element.Points}\"");

        if (!string.IsNullOrEmpty(element.PathData))
            attrs.Add($"d=\"{element.PathData}\"");

        if (!string.IsNullOrEmpty(element.Fill))
            attrs.Add($"fill=\"{element.Fill}\"");
        
        if (!string.IsNullOrEmpty(element.Stroke))
            attrs.Add($"stroke=\"{element.Stroke}\"");
        
        if (element.StrokeWidth != 1)
            attrs.Add($"stroke-width=\"{element.StrokeWidth}\"");
        
        if (element.Opacity != 1)
            attrs.Add($"opacity=\"{element.Opacity}\"");

        if (!string.IsNullOrEmpty(element.Transform))
            attrs.Add($"transform=\"{element.Transform}\"");

        // Add logic attributes
        foreach (var (key, value) in element.LogicAttributes)
        {
            attrs.Add($"data-logic-{key}=\"{value}\"");
        }

        return attrs.Count > 0 ? " " + string.Join(" ", attrs) : "";
    }
}
