using System.Collections.Concurrent;
using OpcUaServer.Web.Models;

namespace OpcUaServer.Web.Services;

/// <summary>
/// Service for managing OPC UA nodes in-memory
/// </summary>
public class OpcUaNodeService
{
    private readonly ConcurrentDictionary<string, OpcUaNode> _nodes = new();
    private int _nodeCounter = 0;

    public event EventHandler<OpcUaNode>? NodeCreated;
    public event EventHandler<OpcUaNode>? NodeUpdated;
    public event EventHandler<string>? NodeDeleted;
    public event EventHandler<(OpcUaNode Node, object? OldValue, object? NewValue)>? NodeValueChanged;

    public OpcUaNodeService()
    {
        InitializeDefaultNodes();
    }

    private void InitializeDefaultNodes()
    {
        // Create root folder
        var rootFolder = new OpcUaNode
        {
            NodeId = "ns=2;s=Root",
            DisplayName = "Root",
            BrowseName = "Root",
            Description = "Root folder for custom nodes",
            NodeClass = OpcUaNodeClass.Object,
            ParentNodeId = null
        };
        _nodes[rootFolder.NodeId] = rootFolder;

        // Create Sensors folder
        var sensorsFolder = new OpcUaNode
        {
            NodeId = "ns=2;s=Sensors",
            DisplayName = "Sensors",
            BrowseName = "Sensors",
            Description = "Folder containing sensor nodes",
            NodeClass = OpcUaNodeClass.Object,
            ParentNodeId = rootFolder.NodeId
        };
        _nodes[sensorsFolder.NodeId] = sensorsFolder;
        rootFolder.Children.Add(sensorsFolder);

        // Create Actuators folder
        var actuatorsFolder = new OpcUaNode
        {
            NodeId = "ns=2;s=Actuators",
            DisplayName = "Actuators",
            BrowseName = "Actuators",
            Description = "Folder containing actuator nodes",
            NodeClass = OpcUaNodeClass.Object,
            ParentNodeId = rootFolder.NodeId
        };
        _nodes[actuatorsFolder.NodeId] = actuatorsFolder;
        rootFolder.Children.Add(actuatorsFolder);

        // Create sample temperature sensor
        var tempSensor = new OpcUaNode
        {
            NodeId = "ns=2;s=Sensors.Temperature",
            DisplayName = "Temperature",
            BrowseName = "Temperature",
            Description = "Temperature sensor reading in Celsius",
            NodeClass = OpcUaNodeClass.Variable,
            DataType = OpcUaDataType.Double,
            Value = 22.5,
            IsWritable = false,
            ParentNodeId = sensorsFolder.NodeId
        };
        _nodes[tempSensor.NodeId] = tempSensor;
        sensorsFolder.Children.Add(tempSensor);

        // Create sample pressure sensor
        var pressureSensor = new OpcUaNode
        {
            NodeId = "ns=2;s=Sensors.Pressure",
            DisplayName = "Pressure",
            BrowseName = "Pressure",
            Description = "Pressure sensor reading in Bar",
            NodeClass = OpcUaNodeClass.Variable,
            DataType = OpcUaDataType.Double,
            Value = 1.013,
            IsWritable = false,
            ParentNodeId = sensorsFolder.NodeId
        };
        _nodes[pressureSensor.NodeId] = pressureSensor;
        sensorsFolder.Children.Add(pressureSensor);

        // Create sample valve actuator
        var valve = new OpcUaNode
        {
            NodeId = "ns=2;s=Actuators.Valve1",
            DisplayName = "Valve 1",
            BrowseName = "Valve1",
            Description = "Main valve control",
            NodeClass = OpcUaNodeClass.Variable,
            DataType = OpcUaDataType.Boolean,
            Value = false,
            IsWritable = true,
            ParentNodeId = actuatorsFolder.NodeId
        };
        _nodes[valve.NodeId] = valve;
        actuatorsFolder.Children.Add(valve);

        // Create sample motor speed setpoint
        var motorSpeed = new OpcUaNode
        {
            NodeId = "ns=2;s=Actuators.MotorSpeed",
            DisplayName = "Motor Speed",
            BrowseName = "MotorSpeed",
            Description = "Motor speed setpoint in RPM",
            NodeClass = OpcUaNodeClass.Variable,
            DataType = OpcUaDataType.Int32,
            Value = 1500,
            IsWritable = true,
            ParentNodeId = actuatorsFolder.NodeId
        };
        _nodes[motorSpeed.NodeId] = motorSpeed;
        actuatorsFolder.Children.Add(motorSpeed);
    }

    public IEnumerable<OpcUaNode> GetAllNodes()
    {
        return _nodes.Values.ToList();
    }

    public IEnumerable<OpcUaNode> GetRootNodes()
    {
        return _nodes.Values.Where(n => n.ParentNodeId == null).ToList();
    }

    public OpcUaNode? GetNode(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    public IEnumerable<OpcUaNode> GetChildren(string parentNodeId)
    {
        return _nodes.Values.Where(n => n.ParentNodeId == parentNodeId).ToList();
    }

    public OpcUaNode CreateNode(CreateNodeRequest request)
    {
        var nodeId = GenerateNodeId(request.DisplayName);
        
        var node = new OpcUaNode
        {
            NodeId = nodeId,
            DisplayName = request.DisplayName,
            BrowseName = request.BrowseName ?? request.DisplayName.Replace(" ", ""),
            Description = request.Description,
            ParentNodeId = request.ParentNodeId,
            NodeClass = request.NodeClass,
            DataType = request.DataType,
            Value = ConvertValue(request.InitialValue, request.DataType),
            IsWritable = request.IsWritable,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (!_nodes.TryAdd(nodeId, node))
        {
            throw new InvalidOperationException($"Failed to create node with ID {nodeId}");
        }

        // Add to parent's children
        if (request.ParentNodeId != null && _nodes.TryGetValue(request.ParentNodeId, out var parent))
        {
            parent.Children.Add(node);
        }

        NodeCreated?.Invoke(this, node);
        return node;
    }

    public OpcUaNode? UpdateNode(string nodeId, UpdateNodeRequest request)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            return null;
        }

        var oldValue = node.Value;

        if (request.DisplayName != null)
            node.DisplayName = request.DisplayName;
        
        if (request.BrowseName != null)
            node.BrowseName = request.BrowseName;
        
        if (request.Description != null)
            node.Description = request.Description;
        
        if (request.DataType.HasValue)
            node.DataType = request.DataType.Value;
        
        if (request.Value != null)
            node.Value = ConvertValue(request.Value, node.DataType);
        
        if (request.IsWritable.HasValue)
            node.IsWritable = request.IsWritable.Value;

        node.UpdatedAt = DateTime.UtcNow;

        NodeUpdated?.Invoke(this, node);

        if (request.Value != null && !Equals(oldValue, node.Value))
        {
            NodeValueChanged?.Invoke(this, (node, oldValue, node.Value));
        }

        return node;
    }

    public bool DeleteNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            return false;
        }

        // Recursively delete children
        var childrenToDelete = node.Children.ToList();
        foreach (var child in childrenToDelete)
        {
            DeleteNode(child.NodeId);
        }

        // Remove from parent's children list
        if (node.ParentNodeId != null && _nodes.TryGetValue(node.ParentNodeId, out var parent))
        {
            parent.Children.Remove(node);
        }

        if (_nodes.TryRemove(nodeId, out _))
        {
            NodeDeleted?.Invoke(this, nodeId);
            return true;
        }

        return false;
    }

    public bool MoveNode(string nodeId, string? newParentNodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            return false;
        }

        // Remove from old parent
        if (node.ParentNodeId != null && _nodes.TryGetValue(node.ParentNodeId, out var oldParent))
        {
            oldParent.Children.Remove(node);
        }

        // Add to new parent
        if (newParentNodeId != null && _nodes.TryGetValue(newParentNodeId, out var newParent))
        {
            newParent.Children.Add(node);
        }

        node.ParentNodeId = newParentNodeId;
        node.UpdatedAt = DateTime.UtcNow;

        NodeUpdated?.Invoke(this, node);
        return true;
    }

    public bool WriteValue(string nodeId, object value)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            return false;
        }

        if (!node.IsWritable)
        {
            return false;
        }

        var oldValue = node.Value;
        node.Value = ConvertValue(value, node.DataType);
        node.UpdatedAt = DateTime.UtcNow;

        if (!Equals(oldValue, node.Value))
        {
            NodeValueChanged?.Invoke(this, (node, oldValue, node.Value));
        }

        return true;
    }

    public object? ReadValue(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node.Value : null;
    }

    public int GetTotalNodeCount()
    {
        return _nodes.Count;
    }

    private string GenerateNodeId(string displayName)
    {
        var counter = Interlocked.Increment(ref _nodeCounter);
        var safeName = displayName.Replace(" ", "_").Replace(".", "_");
        return $"ns=2;s=Custom.{safeName}_{counter}";
    }

    private static object? ConvertValue(object? value, OpcUaDataType dataType)
    {
        if (value == null) return GetDefaultValue(dataType);

        try
        {
            return dataType switch
            {
                OpcUaDataType.Boolean => Convert.ToBoolean(value),
                OpcUaDataType.SByte => Convert.ToSByte(value),
                OpcUaDataType.Byte => Convert.ToByte(value),
                OpcUaDataType.Int16 => Convert.ToInt16(value),
                OpcUaDataType.UInt16 => Convert.ToUInt16(value),
                OpcUaDataType.Int32 => Convert.ToInt32(value),
                OpcUaDataType.UInt32 => Convert.ToUInt32(value),
                OpcUaDataType.Int64 => Convert.ToInt64(value),
                OpcUaDataType.UInt64 => Convert.ToUInt64(value),
                OpcUaDataType.Float => Convert.ToSingle(value),
                OpcUaDataType.Double => Convert.ToDouble(value),
                OpcUaDataType.String => Convert.ToString(value),
                OpcUaDataType.DateTime => value is DateTime dt ? dt : DateTime.Parse(value.ToString()!),
                OpcUaDataType.Guid => value is Guid g ? g : Guid.Parse(value.ToString()!),
                OpcUaDataType.ByteString => value is byte[] bytes ? bytes : Convert.FromBase64String(value.ToString()!),
                _ => value
            };
        }
        catch
        {
            return GetDefaultValue(dataType);
        }
    }

    private static object GetDefaultValue(OpcUaDataType dataType)
    {
        return dataType switch
        {
            OpcUaDataType.Boolean => false,
            OpcUaDataType.SByte => (sbyte)0,
            OpcUaDataType.Byte => (byte)0,
            OpcUaDataType.Int16 => (short)0,
            OpcUaDataType.UInt16 => (ushort)0,
            OpcUaDataType.Int32 => 0,
            OpcUaDataType.UInt32 => 0u,
            OpcUaDataType.Int64 => 0L,
            OpcUaDataType.UInt64 => 0UL,
            OpcUaDataType.Float => 0f,
            OpcUaDataType.Double => 0d,
            OpcUaDataType.String => string.Empty,
            OpcUaDataType.DateTime => DateTime.MinValue,
            OpcUaDataType.Guid => Guid.Empty,
            OpcUaDataType.ByteString => Array.Empty<byte>(),
            _ => string.Empty
        };
    }
}
