namespace OpcUaServer.Web.Models;

/// <summary>
/// Represents an OPC UA node in the server's address space
/// </summary>
public class OpcUaNode
{
    public string NodeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? BrowseName { get; set; }
    public string? Description { get; set; }
    public string? ParentNodeId { get; set; }
    public OpcUaNodeClass NodeClass { get; set; } = OpcUaNodeClass.Variable;
    public OpcUaDataType DataType { get; set; } = OpcUaDataType.String;
    public object? Value { get; set; }
    public bool IsWritable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<OpcUaNode> Children { get; set; } = [];
}

public enum OpcUaNodeClass
{
    Object,
    Variable,
    Method,
    ObjectType,
    VariableType,
    ReferenceType,
    DataType,
    View
}

public enum OpcUaDataType
{
    Boolean,
    SByte,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float,
    Double,
    String,
    DateTime,
    Guid,
    ByteString
}
