using Stateless;
using Stateless.Graph;
using WorkflowCore.Interface;

namespace CoffeeMaker.Api;

public enum CoffeeMakerState
{
    Off,
    On,

    Preheating,
    Idle,
    Brewing,
    MilkFrothing,
    Cleaning
}

public sealed class CoffeeMaker
{
    private enum CoffeeMakerTrigger
    {
        TurnOn,

        StartPreheating,
        PreheatingComplete,

        StartBrewing,
        CancelBrewing,
        BrewingComplete,

        StartMilkFrothing,
        MilkFrothingComplete,

        StartCleaning,
        CleaningComplete,

        TurnOff
    }

    private readonly IWorkflowHost _workflowHost;

    private readonly StateMachine<CoffeeMakerState, CoffeeMakerTrigger> _stateMachine;

    private enum TemperatureMode { Low, Middle, High }
    private enum MilkFrothingMode { Low, Middle, High }

    private CoffeeMakerState _state = CoffeeMakerState.Off;
    private float _temperature;
    private TemperatureMode _temperatureMode = TemperatureMode.Middle;
    private MilkFrothingMode _milkFrothingMode = MilkFrothingMode.Middle;

    private string? _runningWorkflowId;

    public CoffeeMaker(IWorkflowHost workflowHost)
    {
        _workflowHost = workflowHost;

        _stateMachine = new StateMachine<CoffeeMakerState, CoffeeMakerTrigger>(
            () => _state, s => _state = s);

        _stateMachine.Configure(CoffeeMakerState.Off)
            .Permit(CoffeeMakerTrigger.TurnOn, CoffeeMakerState.On);

        _stateMachine.Configure(CoffeeMakerState.On)
            .InitialTransition(CoffeeMakerState.Idle);

        _stateMachine.Configure(CoffeeMakerState.Preheating)
            .SubstateOf(CoffeeMakerState.On)
            .OnEntryAsync(Preheating, "Preheating until target temperature")
            .Permit(CoffeeMakerTrigger.PreheatingComplete, CoffeeMakerState.Idle);

        _stateMachine.Configure(CoffeeMakerState.Idle)
            .SubstateOf(CoffeeMakerState.On)
            .OnEntryAsync(async () =>
            {
                if (!HasTargetTemperature())
                    await _stateMachine.FireAsync(CoffeeMakerTrigger.StartPreheating);
            })
            .Permit(CoffeeMakerTrigger.StartPreheating, CoffeeMakerState.Preheating)
            .Permit(CoffeeMakerTrigger.StartBrewing, CoffeeMakerState.Brewing)
            .Permit(CoffeeMakerTrigger.StartCleaning, CoffeeMakerState.Cleaning)
            .Permit(CoffeeMakerTrigger.StartMilkFrothing, CoffeeMakerState.MilkFrothing);

        _stateMachine.Configure(CoffeeMakerState.Brewing)
            .SubstateOf(CoffeeMakerState.On)
            .OnEntryAsync(StartBrewingInternal)
            .OnExitAsync(async trans =>
            {
                if (trans.Trigger == CoffeeMakerTrigger.CancelBrewing)
                {
                    var terminated = await _workflowHost.TerminateWorkflow(_runningWorkflowId);
                    
                    if (!terminated)
                    {
                        Console.WriteLine("Brewing workflow could not be cancelled");
                        throw new SystemException("Brewing workflow is not cancelled");
                    }
                }

                _runningWorkflowId = null;
            })
            .Permit(CoffeeMakerTrigger.BrewingComplete, CoffeeMakerState.Idle)
            .Permit(CoffeeMakerTrigger.CancelBrewing, CoffeeMakerState.Idle);

        _stateMachine.Configure(CoffeeMakerState.MilkFrothing)
            .SubstateOf(CoffeeMakerState.On)
            .Permit(CoffeeMakerTrigger.MilkFrothingComplete, CoffeeMakerState.Idle);

        _stateMachine.Configure(CoffeeMakerState.Cleaning)
            .SubstateOf(CoffeeMakerState.On)
            .Permit(CoffeeMakerTrigger.CleaningComplete, CoffeeMakerState.Idle);
    }

    /// <summary>
    /// Initializes the coffee maker with the specified state.
    /// </summary>
    public void Initialize(CoffeeMakerState state)
    {
        _state = state;
    }

    public Task TurnOn() => _stateMachine.FireAsync(CoffeeMakerTrigger.TurnOn);
    public Task StartBrewing() => _stateMachine.FireAsync(CoffeeMakerTrigger.StartBrewing);
    public Task CancelBrewing() => _stateMachine.FireAsync(CoffeeMakerTrigger.CancelBrewing);
    public Task BrewingComplete() => _stateMachine.FireAsync(CoffeeMakerTrigger.BrewingComplete);
    public Task StartMilkFrothing() => _stateMachine.FireAsync(CoffeeMakerTrigger.StartMilkFrothing);
    public Task StartCleaning() => _stateMachine.FireAsync(CoffeeMakerTrigger.StartCleaning);

    private async Task Preheating()
    { 
        while (!HasTargetTemperature())
        {
            _temperature += _temperature < TargetTemperature ? 0.5f : -0.5f;
            await Task.Delay(100);
        }

        await _stateMachine.FireAsync(CoffeeMakerTrigger.PreheatingComplete);
    }

    private async Task StartBrewingInternal()
    {
        _runningWorkflowId = await _workflowHost.StartWorkflow(nameof(BrewingWorkflow));
    }

    private float TargetTemperature => _temperatureMode switch
    {
        TemperatureMode.Low => 75.0f,
        TemperatureMode.Middle => 80.0f,
        TemperatureMode.High => 85.0f,
        _ => throw new InvalidDataException("Not supported temperature mode")
    };

    private bool HasTargetTemperature()
    {
        return Math.Abs(_temperature - TargetTemperature) < 0.1f;
    }

    internal string GetGraph() => MermaidGraph.Format(_stateMachine.GetInfo());

    internal CoffeeMakerStatusData GetState() => new(_stateMachine.State, _stateMachine.State.ToString(), _temperature);

    internal async Task<IEnumerable<string>> GetPermittedTriggers()
    {
        var triggers = await _stateMachine.GetPermittedTriggersAsync();
        return triggers.Select(t => t.ToString());
    }
}
