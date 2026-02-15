using CoffeeMaker.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddWorkflow();
builder.Services.AddWorkflowStepsFromAssembly();

builder.Services.AddSingleton<CoffeeMaker.Api.CoffeeMaker>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.AddCoffeeMakerEndpoints();

await app.StartupAsync();

app.Run();
