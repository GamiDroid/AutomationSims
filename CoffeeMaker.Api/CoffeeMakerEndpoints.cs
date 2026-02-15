namespace CoffeeMaker.Api;

public static class CoffeeMakerEndpoints
{
    public static void AddCoffeeMakerEndpoints(this WebApplication app)
    {
        app.MapGet("/coffeemaker/graph", (CoffeeMaker coffeeMaker) =>
        {
            return Results.Text(coffeeMaker.GetGraph());
        });

        app.MapGet("/coffeemaker/state", (CoffeeMaker coffeeMaker) =>
        {
            return Results.Ok(coffeeMaker.GetState());
        });

        app.MapGet("/coffeemaker/permitted-triggers", async (CoffeeMaker coffeeMaker) =>
        {
            return Results.Ok(await coffeeMaker.GetPermittedTriggers());
        });

        app.MapPut("/coffeemaker/trigger/{trigger:alpha}", (CoffeeMaker coffeeMaker, string trigger) =>
        {
            switch (trigger.ToLowerInvariant())
            {
                case "turnon": coffeeMaker.TurnOn(); break;
                case "brew": coffeeMaker.StartBrewing(); break;
                case "cancelbrew": coffeeMaker.CancelBrewing(); break;
                case "milkfrothing": coffeeMaker.StartMilkFrothing(); break;
                case "clean": coffeeMaker.StartCleaning(); break;
            }

            return Results.Ok(coffeeMaker.GetState());
        });

        app.MapPut("/coffeemaker/state/{state:alpha}", (CoffeeMaker coffeeMaker, string state) =>
        {
            switch (state.ToLowerInvariant())
            {
                case "off": coffeeMaker.Initialize(CoffeeMakerState.Off); break;
                case "on": coffeeMaker.Initialize(CoffeeMakerState.On); break;
                case "preheating": coffeeMaker.Initialize(CoffeeMakerState.Preheating); break;
                case "idle": coffeeMaker.Initialize(CoffeeMakerState.Idle); break;
                case "brewing": coffeeMaker.Initialize(CoffeeMakerState.Brewing); break;
                case "milkfrothing": coffeeMaker.Initialize(CoffeeMakerState.MilkFrothing); break;
                case "cleaning": coffeeMaker.Initialize(CoffeeMakerState.Cleaning); break;
            }

            return Results.Ok(coffeeMaker.GetState());
        });
    }
}
