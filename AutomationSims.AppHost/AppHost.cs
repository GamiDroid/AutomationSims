var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.OpcUaServer_Web>("opcuaserver-web");

builder.Build().Run();
