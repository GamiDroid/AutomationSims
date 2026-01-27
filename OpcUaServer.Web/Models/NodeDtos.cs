namespace OpcUaServer.Web.Models;

/// <summary>
/// DTO for creating a new OPC UA node
/// </summary>
public record CreateNodeRequest(
    string DisplayName,
    string? BrowseName,
    string? Description,
    string? ParentNodeId,
    OpcUaNodeClass NodeClass,
    OpcUaDataType DataType,
    object? InitialValue,
    bool IsWritable = true
);

/// <summary>
/// DTO for updating an existing OPC UA node
/// </summary>
public record UpdateNodeRequest(
    string? DisplayName,
    string? BrowseName,
    string? Description,
    OpcUaDataType? DataType,
    object? Value,
    bool? IsWritable
);

/// <summary>
/// DTO for moving a node to a new parent
/// </summary>
public record MoveNodeRequest(
    string NodeId,
    string? NewParentNodeId
);

/// <summary>
/// DTO for writing a value to a node
/// </summary>
public record WriteValueRequest(
    string NodeId,
    object Value
);

/// <summary>
/// DTO for node response
/// </summary>
public record NodeResponse(
    string NodeId,
    string DisplayName,
    string? BrowseName,
    string? Description,
    string? ParentNodeId,
    OpcUaNodeClass NodeClass,
    OpcUaDataType DataType,
    object? Value,
    bool IsWritable,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<NodeResponse> Children
);

/// <summary>
/// DTO for monitoring a node value
/// </summary>
public record MonitoredNodeValue(
    string NodeId,
    string DisplayName,
    object? Value,
    OpcUaDataType DataType,
    DateTime Timestamp
);

/// <summary>
/// DTO for server status
/// </summary>
public record ServerStatusResponse(
    bool IsRunning,
    string ServerUri,
    int TotalNodes,
    DateTime StartTime,
    DateTime CurrentTime
);
