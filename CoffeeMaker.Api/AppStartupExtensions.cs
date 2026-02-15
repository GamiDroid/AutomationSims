using Mqtt.Common;
using MQTTnet;
using System.Reflection;
using WorkflowCore.Interface;

namespace CoffeeMaker.Api;

public static class AppStartupExtensions
{
    public static void AddWorkflowStepsFromAssembly(this IServiceCollection services, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        // Register all IStepBody implementations from the specified assembly
        var stepBodyTypes = assembly.GetTypes()
            .Where(t => typeof(IStepBody).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in stepBodyTypes)
        {
            services.AddTransient(type);
        }
    }

    public static async Task StartupAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

        var coffieMaker = app.Services.GetRequiredService<CoffeeMaker>();

        var workflowHost = app.Services.GetRequiredService<IWorkflowHost>();

        workflowHost.RegisterWorkflow<BrewingWorkflow>();

        logger.LogInformation("Starting up the application...");

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost")
            .Build();

        await using var mqtt = new MqttConnection(options);

        await mqtt.StartAsync(CancellationToken.None);

        //var coffeeMakerStatusData = await mqtt.GetTopicDataAsync<CoffeeMakerStatusData>(
        //    "coffee-maker/status", CancellationToken.None);

        coffieMaker.Initialize(CoffeeMakerState.Off);

        await workflowHost.StartAsync(CancellationToken.None);
    }
}

public record CoffeeMakerStatusData(
    CoffeeMakerState State,
    string StateText,
    float Temperature
);