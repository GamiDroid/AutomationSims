using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using OpcUaServer.Web.Models;
using OpcUaStatusCodes = Opc.Ua.StatusCodes;

namespace OpcUaServer.Web.Services;

/// <summary>
/// Service that manages the OPC UA server instance
/// </summary>
public class OpcUaServerService : IHostedService, IDisposable
{
    private readonly ILogger<OpcUaServerService> _logger;
    private readonly OpcUaNodeService _nodeService;
    private StandardServer? _server;
    private ApplicationInstance? _application;
    private bool _isRunning;
    private DateTime _startTime;
    private readonly string _serverUri = "opc.tcp://localhost:4840";

    public bool IsRunning => _isRunning;
    public string ServerUri => _serverUri;
    public DateTime StartTime => _startTime;

    public OpcUaServerService(ILogger<OpcUaServerService> logger, OpcUaNodeService nodeService)
    {
        _logger = logger;
        _nodeService = nodeService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting OPC UA Server...");

            // Create certificate directories if they don't exist
            Directory.CreateDirectory("./pki/own/certs");
            Directory.CreateDirectory("./pki/own/private");
            Directory.CreateDirectory("./pki/issuer/certs");
            Directory.CreateDirectory("./pki/trusted/certs");
            Directory.CreateDirectory("./pki/rejected/certs");

            // Create application configuration
            var config = new ApplicationConfiguration
            {
                ApplicationName = "OPC UA Simulation Server",
                ApplicationUri = Utils.Format("urn:{0}:OpcUaSimServer", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Server,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = @"Directory",
                        StorePath = @"./pki/own",
                        SubjectName = "CN=OPC UA Simulation Server, O=OpcUaServer, DC=localhost"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = @"./pki/issuer"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = @"./pki/trusted"
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = @"./pki/rejected"
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                TransportConfigurations = [],
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = { _serverUri },
                    MinRequestThreadCount = 5,
                    MaxRequestThreadCount = 100,
                    MaxQueuedRequestCount = 2000,
                    SecurityPolicies =
                    [
                        new ServerSecurityPolicy
                        {
                            SecurityMode = MessageSecurityMode.None,
                            SecurityPolicyUri = SecurityPolicies.None
                        }
                    ],
                    UserTokenPolicies =
                    [
                        new UserTokenPolicy(UserTokenType.Anonymous)
                    ]
                },
                TraceConfiguration = new TraceConfiguration
                {
                    OutputFilePath = "./logs/opcua.log",
                    DeleteOnLoad = true,
                    TraceMasks = 1
                }
            };

            await config.ValidateAsync(ApplicationType.Server, cancellationToken);

            _application = new ApplicationInstance()
            {
                ApplicationName = "OPC UA Simulation Server",
                ApplicationType = ApplicationType.Server,
                ApplicationConfiguration = config
            };

            // Create and start server
            _server = new CustomOpcUaServer(_nodeService, _logger);
            await _application.StartAsync(_server);

            _isRunning = true;
            _startTime = DateTime.UtcNow;

            _logger.LogInformation("OPC UA Server started successfully at {ServerUri}", _serverUri);

            // Subscribe to node changes
            _nodeService.NodeCreated += OnNodeCreated;
            _nodeService.NodeUpdated += OnNodeUpdated;
            _nodeService.NodeDeleted += OnNodeDeleted;
            _nodeService.NodeValueChanged += OnNodeValueChanged;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OPC UA Server");
            _isRunning = false;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OPC UA Server...");

        _nodeService.NodeCreated -= OnNodeCreated;
        _nodeService.NodeUpdated -= OnNodeUpdated;
        _nodeService.NodeDeleted -= OnNodeDeleted;
        _nodeService.NodeValueChanged -= OnNodeValueChanged;

        if (_server is not null)
            await _server.StopAsync(cancellationToken);
        _isRunning = false;

        _logger.LogInformation("OPC UA Server stopped");
    }

    public ServerStatusResponse GetStatus()
    {
        return new ServerStatusResponse(
            IsRunning: _isRunning,
            ServerUri: _serverUri,
            TotalNodes: _nodeService.GetTotalNodeCount(),
            StartTime: _startTime,
            CurrentTime: DateTime.UtcNow
        );
    }

    private void OnNodeCreated(object? sender, OpcUaNode node)
    {
        _logger.LogInformation("Node created: {NodeId} - {DisplayName}", node.NodeId, node.DisplayName);
    }

    private void OnNodeUpdated(object? sender, OpcUaNode node)
    {
        _logger.LogInformation("Node updated: {NodeId} - {DisplayName}", node.NodeId, node.DisplayName);
    }

    private void OnNodeDeleted(object? sender, string nodeId)
    {
        _logger.LogInformation("Node deleted: {NodeId}", nodeId);
    }

    private void OnNodeValueChanged(object? sender, (OpcUaNode Node, object? OldValue, object? NewValue) args)
    {
        _logger.LogDebug("Node value changed: {NodeId} from {OldValue} to {NewValue}", 
            args.Node.NodeId, args.OldValue, args.NewValue);
    }

    public void Dispose()
    {
        _server?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Custom OPC UA Server implementation
/// </summary>
internal class CustomOpcUaServer : StandardServer
{
    private readonly OpcUaNodeService _nodeService;
    private readonly ILogger _logger;

    public CustomOpcUaServer(OpcUaNodeService nodeService, ILogger logger)
    {
        _nodeService = nodeService;
        _logger = logger;
    }

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        var nodeManagers = new List<INodeManager>
        {
            new CustomNodeManager(server, configuration, _nodeService, _logger)
        };

        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }

    protected override ServerProperties LoadServerProperties()
    {
        return new ServerProperties
        {
            ManufacturerName = "OPC UA Simulation Server",
            ProductName = "OPC UA Simulation Server",
            ProductUri = "http://localhost/OpcUaSimServer",
            SoftwareVersion = Utils.GetAssemblySoftwareVersion(),
            BuildNumber = Utils.GetAssemblyBuildNumber(),
            BuildDate = Utils.GetAssemblyTimestamp()
        };
    }
}

/// <summary>
/// Custom Node Manager for managing the address space
/// </summary>
internal class CustomNodeManager : CustomNodeManager2
{
    private readonly OpcUaNodeService _nodeService;
    private readonly ILogger _logger;
    private readonly Dictionary<string, BaseDataVariableState> _variableNodes = new();

    public CustomNodeManager(IServerInternal server, ApplicationConfiguration configuration, 
        OpcUaNodeService nodeService, ILogger logger)
        : base(server, configuration, "http://opcua.simulation.server")
    {
        _nodeService = nodeService;
        _logger = logger;

        _nodeService.NodeCreated += (s, n) => AddNodeToAddressSpace(n);
        _nodeService.NodeDeleted += (s, id) => RemoveNodeFromAddressSpace(id);
        _nodeService.NodeValueChanged += (s, args) => UpdateNodeValue(args.Node);
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            base.CreateAddressSpace(externalReferences);

            // Create folder for custom nodes
            var rootFolder = CreateFolder(null, "SimulationServer", "Simulation Server");
            rootFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);

            AddExternalReference(ObjectIds.ObjectsFolder, ReferenceTypes.Organizes, false, rootFolder.NodeId, externalReferences);

            // Add existing nodes from the service
            foreach (var node in _nodeService.GetAllNodes())
            {
                CreateOpcUaNode(rootFolder, node);
            }
        }
    }

    private void CreateOpcUaNode(FolderState? parentFolder, OpcUaNode node)
    {
        if (node.NodeClass == Models.OpcUaNodeClass.Object)
        {
            var folder = CreateFolder(parentFolder, node.BrowseName ?? node.DisplayName, node.DisplayName);
            if (!string.IsNullOrEmpty(node.Description))
            {
                folder.Description = node.Description;
            }

            foreach (var child in node.Children)
            {
                CreateOpcUaNode(folder as FolderState, child);
            }
        }
        else if (node.NodeClass == Models.OpcUaNodeClass.Variable)
        {
            var variable = CreateVariable(parentFolder, node.BrowseName ?? node.DisplayName, node.DisplayName, 
                GetOpcUaDataType(node.DataType), ValueRanks.Scalar);
            
            variable.Value = node.Value;
            variable.AccessLevel = node.IsWritable 
                ? AccessLevels.CurrentReadOrWrite 
                : AccessLevels.CurrentRead;
            variable.UserAccessLevel = variable.AccessLevel;

            if (!string.IsNullOrEmpty(node.Description))
            {
                variable.Description = node.Description;
            }

            _variableNodes[node.NodeId] = variable;
        }
    }

    private FolderState CreateFolder(NodeState? parent, string browseName, string displayName)
    {
        var folder = new FolderState(parent)
        {
            SymbolicName = browseName,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType,
            NodeId = new NodeId(browseName, NamespaceIndex),
            BrowseName = new QualifiedName(browseName, NamespaceIndex),
            DisplayName = new LocalizedText("en", displayName),
            WriteMask = AttributeWriteMask.None,
            UserWriteMask = AttributeWriteMask.None,
            EventNotifier = EventNotifiers.None
        };

        parent?.AddChild(folder);
        AddPredefinedNode(SystemContext, folder);

        return folder;
    }

    private BaseDataVariableState CreateVariable(NodeState? parent, string browseName, string displayName, 
        NodeId dataType, int valueRank)
    {
        var variable = new BaseDataVariableState(parent)
        {
            SymbolicName = browseName,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId(browseName, NamespaceIndex),
            BrowseName = new QualifiedName(browseName, NamespaceIndex),
            DisplayName = new LocalizedText("en", displayName),
            WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            DataType = dataType,
            ValueRank = valueRank,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Historizing = false,
            StatusCode = OpcUaStatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };

        parent?.AddChild(variable);
        AddPredefinedNode(SystemContext, variable);

        return variable;
    }

    private void AddNodeToAddressSpace(OpcUaNode node)
    {
        lock (Lock)
        {
            _logger.LogDebug("Adding node to address space: {NodeId}", node.NodeId);
        }
    }

    private void RemoveNodeFromAddressSpace(string nodeId)
    {
        lock (Lock)
        {
            if (_variableNodes.TryGetValue(nodeId, out var variable))
            {
                DeleteNode(SystemContext, variable.NodeId);
                _variableNodes.Remove(nodeId);
            }
        }
    }

    private void UpdateNodeValue(OpcUaNode node)
    {
        lock (Lock)
        {
            if (_variableNodes.TryGetValue(node.NodeId, out var variable))
            {
                variable.Value = node.Value;
                variable.Timestamp = DateTime.UtcNow;
                variable.ClearChangeMasks(SystemContext, false);
            }
        }
    }

    private static NodeId GetOpcUaDataType(OpcUaDataType dataType)
    {
        return dataType switch
        {
            OpcUaDataType.Boolean => DataTypeIds.Boolean,
            OpcUaDataType.SByte => DataTypeIds.SByte,
            OpcUaDataType.Byte => DataTypeIds.Byte,
            OpcUaDataType.Int16 => DataTypeIds.Int16,
            OpcUaDataType.UInt16 => DataTypeIds.UInt16,
            OpcUaDataType.Int32 => DataTypeIds.Int32,
            OpcUaDataType.UInt32 => DataTypeIds.UInt32,
            OpcUaDataType.Int64 => DataTypeIds.Int64,
            OpcUaDataType.UInt64 => DataTypeIds.UInt64,
            OpcUaDataType.Float => DataTypeIds.Float,
            OpcUaDataType.Double => DataTypeIds.Double,
            OpcUaDataType.String => DataTypeIds.String,
            OpcUaDataType.DateTime => DataTypeIds.DateTime,
            OpcUaDataType.Guid => DataTypeIds.Guid,
            OpcUaDataType.ByteString => DataTypeIds.ByteString,
            _ => DataTypeIds.String
        };
    }
}
