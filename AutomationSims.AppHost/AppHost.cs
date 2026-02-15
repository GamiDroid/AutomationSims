var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.OpcUaServer_Web>("opcuaserver-web");

builder.AddProject<Projects.CoffeeMaker_Api>("coffeemaker-api");

builder.Build().Run();
