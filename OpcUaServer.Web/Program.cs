using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;
using OpcUaServer.Web;
using OpcUaServer.Web.Components;
using OpcUaServer.Web.Models;
using OpcUaServer.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add OPC UA services
builder.Services.AddSingleton<OpcUaNodeService>();
builder.Services.AddSingleton<OpcUaServerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OpcUaServerService>());

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

// Map OPC UA API endpoints
app.MapOpcUaApiEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
