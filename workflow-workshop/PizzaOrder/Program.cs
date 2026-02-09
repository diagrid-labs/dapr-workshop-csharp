using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using PizzaOrder;
using PizzaOrder.Services;
using Dapr.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IOrderStateService, OrderStateService>();

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
app.UseCloudEvents();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable Dapr pub/sub subscription endpoint discovery
app.MapSubscribeHandler();
app.MapDefaultEndpoints();
app.MapServiceEndpoints();
app.Run();