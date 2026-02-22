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
            var pkiRoot = Path.Combine(Path.GetTempPath(), "pki");
            Directory.CreateDirectory(Path.Combine(pkiRoot, "own", "certs"));
            Directory.CreateDirectory(Path.Combine(pkiRoot, "own", "private"));
            Directory.CreateDirectory(Path.Combine(pkiRoot, "issuer", "certs"));
            Directory.CreateDirectory(Path.Combine(pkiRoot, "issuer", "crl"));
            Directory.CreateDirectory(Path.Combine(pkiRoot, "trusted", "certs"));
            Directory.CreateDirectory(Path.Combine(pkiRoot, "trusted", "crl"));
            Directory.CreateDirectory(Path.Combine(pkiRoot, "rejected", "certs"));

            var hostName = System.Net.Dns.GetHostName();

            // Create application configuration
            var config = new ApplicationConfiguration
            {
                ApplicationName = "OPC UA Simulation Server",
                ApplicationUri = $"urn:{hostName}:OpcUaSimServer",
                ApplicationType = ApplicationType.Server,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(pkiRoot, "own"),
                        SubjectName = $"CN=OPC UA Simulation Server, O=OpcUaServer, DC={hostName}"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(pkiRoot, "issuer")
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(pkiRoot, "trusted")
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(pkiRoot, "rejected")
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024
                },
                TransportConfigurations = [],
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 120000,
                    MaxStringLength = 1048576,
                    MaxByteStringLength = 4194304,
                    MaxArrayLength = 65535,
                    MaxMessageSize = 4194304,
                    MaxBufferSize = 65535,
                    ChannelLifetime = 300000,
                    SecurityTokenLifetime = 3600000
                },
                ServerConfiguration = new ServerConfiguration
                {
                    // Use only localhost to avoid IPv4/IPv6 binding conflicts
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
                        {
                            PolicyId = "Anonymous"
                        }
                    ],
                    DiagnosticsEnabled = true,
                    MaxSessionCount = 100,
                    MinSessionTimeout = 10000,
                    MaxSessionTimeout = 3600000,
                    MaxBrowseContinuationPoints = 10,
                    MaxQueryContinuationPoints = 10,
                    MaxHistoryContinuationPoints = 100,
                    MaxRequestAge = 600000,
                    MinPublishingInterval = 100,
                    MaxPublishingInterval = 3600000,
                    PublishingResolution = 50,
                    MaxSubscriptionLifetime = 3600000,
                    MaxMessageQueueSize = 100,
                    MaxNotificationQueueSize = 100,
                    MaxNotificationsPerPublish = 1000,
                    MinMetadataSamplingInterval = 1000,
                    MaxPublishRequestCount = 20,
                    MaxSubscriptionCount = 100,
                    MaxEventQueueSize = 10000
                }
            };

            await config.ValidateAsync(ApplicationType.Server, cancellationToken);

            _application = new ApplicationInstance(DefaultTelemetry.Create(builder => builder.AddConsole()))
            {
                ApplicationName = "OPC UA Simulation Server",
                ApplicationType = ApplicationType.Server,
                ApplicationConfiguration = config
            };

            // Check and create application certificate if needed
            bool haveAppCertificate = await _application.CheckApplicationInstanceCertificatesAsync(silent: true, ct: cancellationToken);
            if (!haveAppCertificate)
            {
                _logger.LogWarning("Missing or invalid application certificate, connections may fail.");
            }
            else
            {
                _logger.LogInformation("Application certificate is valid.");
            }
            
            // Auto-accept untrusted certificates for development
            config.CertificateValidator.CertificateValidation += (validator, e) =>
            {
                e.Accept = true;
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
            ProductUri = "http://opcuaserver.web/SimulationServer",
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
    private readonly Dictionary<string, FolderState> _folderNodes = new();

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
            var rootFolder = CreateFolder(null, "SimulationServer", "Simulation Server", "SimulationServer");
            rootFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);

            AddExternalReference(ObjectIds.ObjectsFolder, ReferenceTypes.Organizes, false, rootFolder.NodeId, externalReferences);

            // Add existing nodes from the service - only root nodes (those without parent or with root parent)
            foreach (var node in _nodeService.GetRootNodes())
            {
                CreateOpcUaNode(rootFolder, node);
            }
            
            _logger.LogInformation("Address space created with {Count} variable nodes", _variableNodes.Count);
        }
    }

    private void CreateOpcUaNode(FolderState? parentFolder, OpcUaNode node)
    {
        // Extract the string identifier from the NodeId (e.g., "ns=2;s=Actuators.Valve1" -> "Actuators.Valve1")
        var stringId = ExtractStringIdentifier(node.NodeId);
        
        if (node.NodeClass == Models.OpcUaNodeClass.Object)
        {
            var folder = CreateFolder(parentFolder, node.BrowseName ?? node.DisplayName, node.DisplayName, stringId);
            if (!string.IsNullOrEmpty(node.Description))
            {
                folder.Description = node.Description;
            }
            
            _folderNodes[node.NodeId] = folder;

            foreach (var child in node.Children)
            {
                CreateOpcUaNode(folder, child);
            }
        }
        else if (node.NodeClass == Models.OpcUaNodeClass.Variable)
        {
            var variable = CreateVariable(parentFolder, node.BrowseName ?? node.DisplayName, node.DisplayName, 
                GetOpcUaDataType(node.DataType), ValueRanks.Scalar, stringId);
            
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
            _logger.LogDebug("Created variable node: {NodeId} with OPC UA NodeId: {OpcNodeId}", node.NodeId, variable.NodeId);
        }
    }

    private static string ExtractStringIdentifier(string nodeId)
    {
        // Parse "ns=2;s=Actuators.Valve1" to extract "Actuators.Valve1"
        if (nodeId.Contains(";s="))
        {
            var idx = nodeId.IndexOf(";s=", StringComparison.Ordinal);
            return nodeId[(idx + 3)..];
        }
        // Fallback: return the whole string if not in expected format
        return nodeId;
    }

    private FolderState CreateFolder(NodeState? parent, string browseName, string displayName, string stringId)
    {
        var folder = new FolderState(parent)
        {
            SymbolicName = browseName,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType,
            NodeId = new NodeId(stringId, NamespaceIndex),
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
        NodeId dataType, int valueRank, string stringId)
    {
        var variable = new BaseDataVariableState(parent)
        {
            SymbolicName = browseName,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId(stringId, NamespaceIndex),
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
            // Find parent folder
            FolderState? parentFolder = null;
            if (node.ParentNodeId != null && _folderNodes.TryGetValue(node.ParentNodeId, out var folder))
            {
                parentFolder = folder;
            }
            
            CreateOpcUaNode(parentFolder, node);
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
            else if (_folderNodes.TryGetValue(nodeId, out var folder))
            {
                DeleteNode(SystemContext, folder.NodeId);
                _folderNodes.Remove(nodeId);
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
