using Microsoft.AspNetCore.Mvc;
using OpcUaServer.Web.Models;
using OpcUaServer.Web.Services;

namespace OpcUaServer.Web;

public static class OpcUaEndpoints
{
    public static void MapOpcUaApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/opcua")
            .WithTags("OPC UA");

        // Server status
        api.MapGet("/status", (OpcUaServerService serverService) =>
        {
            return Results.Ok(serverService.GetStatus());
        })
        .WithName("GetServerStatus")
        .WithSummary("Get OPC UA server status");

        // Get all nodes
        api.MapGet("/nodes", (OpcUaNodeService nodeService) =>
        {
            var nodes = nodeService.GetAllNodes().Select(ToNodeResponse);
            return Results.Ok(nodes);
        })
        .WithName("GetAllNodes")
        .WithSummary("Get all OPC UA nodes");

        // Get root nodes (tree structure)
        api.MapGet("/nodes/tree", (OpcUaNodeService nodeService) =>
        {
            var roots = nodeService.GetRootNodes().Select(ToNodeResponse);
            return Results.Ok(roots);
        })
        .WithName("GetNodeTree")
        .WithSummary("Get OPC UA nodes as tree structure");

        // Get single node
        api.MapGet("/nodes/{nodeId}", (string nodeId, OpcUaNodeService nodeService) =>
        {
            var decodedNodeId = Uri.UnescapeDataString(nodeId);
            var node = nodeService.GetNode(decodedNodeId);
            if (node == null)
                return Results.NotFound($"Node with ID '{decodedNodeId}' not found");

            return Results.Ok(ToNodeResponse(node));
        })
        .WithName("GetNode")
        .WithSummary("Get a specific OPC UA node by ID");

        // Get children of a node
        api.MapGet("/nodes/{nodeId}/children", (string nodeId, OpcUaNodeService nodeService) =>
        {
            var decodedNodeId = Uri.UnescapeDataString(nodeId);
            var children = nodeService.GetChildren(decodedNodeId).Select(ToNodeResponse);
            return Results.Ok(children);
        })
        .WithName("GetNodeChildren")
        .WithSummary("Get children of a specific OPC UA node");

        // Create node
        api.MapPost("/nodes", ([FromBody] CreateNodeRequest request, OpcUaNodeService nodeService) =>
        {
            try
            {
                var node = nodeService.CreateNode(request);
                return Results.Created($"/api/opcua/nodes/{Uri.EscapeDataString(node.NodeId)}", ToNodeResponse(node));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
        .WithName("CreateNode")
        .WithSummary("Create a new OPC UA node");

        // Update node
        api.MapPut("/nodes/{nodeId}", (string nodeId, [FromBody] UpdateNodeRequest request, OpcUaNodeService nodeService) =>
        {
            var decodedNodeId = Uri.UnescapeDataString(nodeId);
            var node = nodeService.UpdateNode(decodedNodeId, request);
            if (node == null)
                return Results.NotFound($"Node with ID '{decodedNodeId}' not found");

            return Results.Ok(ToNodeResponse(node));
        })
        .WithName("UpdateNode")
        .WithSummary("Update an existing OPC UA node");

        // Delete node
        api.MapDelete("/nodes/{nodeId}", (string nodeId, OpcUaNodeService nodeService) =>
        {
            var decodedNodeId = Uri.UnescapeDataString(nodeId);
            var success = nodeService.DeleteNode(decodedNodeId);
            if (!success)
                return Results.NotFound($"Node with ID '{decodedNodeId}' not found");

            return Results.NoContent();
        })
        .WithName("DeleteNode")
        .WithSummary("Delete an OPC UA node");

        // Move node
        api.MapPost("/nodes/{nodeId}/move", (string nodeId, [FromBody] MoveNodeRequest request, OpcUaNodeService nodeService) =>
        {
            var decodedNodeId = Uri.UnescapeDataString(nodeId);
            var success = nodeService.MoveNode(decodedNodeId, request.NewParentNodeId);
            if (!success)
                return Results.NotFound($"Node with ID '{decodedNodeId}' not found");

            var node = nodeService.GetNode(decodedNodeId);
            return Results.Ok(ToNodeResponse(node!));
        })
        .WithName("MoveNode")
        .WithSummary("Move an OPC UA node to a new parent");

        // Read value
        api.MapGet("/nodes/{nodeId}/value", (string nodeId, OpcUaNodeService nodeService) =>
        {
            var decodedNodeId = Uri.UnescapeDataString(nodeId);
            var node = nodeService.GetNode(decodedNodeId);
            if (node == null)
                return Results.NotFound($"Node with ID '{decodedNodeId}' not found");

            return Results.Ok(new MonitoredNodeValue(
                node.NodeId,
                node.DisplayName,
                node.Value,
                node.DataType,
                node.UpdatedAt
            ));
        })
        .WithName("ReadNodeValue")
        .WithSummary("Read the current value of an OPC UA node");

        // Write value
        api.MapPost("/nodes/{nodeId}/value", (string nodeId, [FromBody] WriteValueRequest request, OpcUaNodeService nodeService) =>
        {
            var decodedNodeId = Uri.UnescapeDataString(nodeId);
            var success = nodeService.WriteValue(decodedNodeId, request.Value);
            if (!success)
            {
                var node = nodeService.GetNode(decodedNodeId);
                if (node == null)
                    return Results.NotFound($"Node with ID '{decodedNodeId}' not found");
                if (!node.IsWritable)
                    return Results.BadRequest($"Node with ID '{decodedNodeId}' is not writable");
            }

            var updatedNode = nodeService.GetNode(decodedNodeId);
            return Results.Ok(new MonitoredNodeValue(
                updatedNode!.NodeId,
                updatedNode.DisplayName,
                updatedNode.Value,
                updatedNode.DataType,
                updatedNode.UpdatedAt
            ));
        })
        .WithName("WriteNodeValue")
        .WithSummary("Write a value to an OPC UA node");
    }

    static NodeResponse ToNodeResponse(OpcUaNode node)
    {
        return new NodeResponse(
            node.NodeId,
            node.DisplayName,
            node.BrowseName,
            node.Description,
            node.ParentNodeId,
            node.NodeClass,
            node.DataType,
            node.Value,
            node.IsWritable,
            node.CreatedAt,
            node.UpdatedAt,
            node.Children.Select(ToNodeResponse).ToList()
        );
    }
}
