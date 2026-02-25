using SvgViewer.Web.Models;

namespace SvgViewer.Web.Services;

public interface ISvgEditorService
{
    // Document management
    SvgDocument CurrentDocument { get; }
    event EventHandler<SvgDocument>? DocumentChanged;
    void NewDocument(string name, double width, double height);
    Task<bool> LoadDocumentAsync(string svgContent);
    Task<string> ExportDocumentAsync();
    
    // Element management
    event EventHandler<SvgElement>? ElementAdded;
    event EventHandler<SvgElement>? ElementUpdated;
    event EventHandler<string>? ElementDeleted;
    event EventHandler<string>? ElementSelected;
    
    SvgElement? SelectedElement { get; }
    void SelectElement(string? elementId);
    SvgElement AddElement(SvgElement element, string? parentId = null);
    void UpdateElement(SvgElement element);
    void DeleteElement(string elementId);
    SvgElement? GetElement(string elementId);
    IEnumerable<SvgElement> GetRootElements();
    IEnumerable<SvgElement> GetAllElements();
    
    // Logic attribute management
    void SetLogicAttribute(string elementId, string key, string value);
    void RemoveLogicAttribute(string elementId, string key);
    Dictionary<string, string> GetLogicAttributes(string elementId);
}
