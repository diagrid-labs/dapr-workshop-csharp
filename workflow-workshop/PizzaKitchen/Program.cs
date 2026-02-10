using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using PizzaKitchen;
using PizzaKitchen.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ICookService, CookService>();

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddDaprClient((daprBuilder) =>
{
    daprBuilder.UseJsonSerializationOptions(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    });
});

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapServiceEndpoints();
app.Run();