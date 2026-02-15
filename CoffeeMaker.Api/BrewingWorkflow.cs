using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace CoffeeMaker.Api;

public class BrewingWorkflow : IWorkflow
{
    public string Id => nameof(BrewingWorkflow);
    public int Version => 1;

    public void Build(IWorkflowBuilder<object> builder)
    {
        builder.Then<StartBrewingStep>()
            .Then<BrewingStep>()
            .Then<EndBrewingStep>();
    }

    class StartBrewingStep : IStepBody
    {
        public async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            // Simulate brewing process
            Console.WriteLine("Brewing started...");

            await Task.Delay(TimeSpan.FromSeconds(3));

            return ExecutionResult.Next();
        }
    }

    class BrewingStep : IStepBody
    {
        public async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            // Simulate brewing process
            Console.WriteLine("Brewing...");

            await Task.Delay(TimeSpan.FromSeconds(15), context.CancellationToken);

            return ExecutionResult.Next();
        }
    }

    class EndBrewingStep(CoffeeMaker coffeeMaker) : IStepBody
    {
        public async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var cancelled = context.CancellationToken.IsCancellationRequested;

            if (cancelled)
                Console.WriteLine("Workflow is already requested");

            // Simulate brewing process
            Console.WriteLine("End Brewing...");

            await coffeeMaker.BrewingComplete();

            return ExecutionResult.Next();
        }
    }
}


